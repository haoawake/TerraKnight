using System.Collections.Generic;
using TerraKnight.Player;
using TerraKnight.Terraria;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight.Items
{
    /// <summary>
    /// The crafting panel's filter, as Terraria's NewCraftingUI has it: the Creative menu's item
    /// categories (ItemFilters: the Weapon, Accessories, Armor, Consumables, BuildingBlock, the misc
    /// fallback, Tools, Furniture, Vanity, MiscAccessories and Materials entries) drawn with their own
    /// icons out of Images/UI/Creative/Infinite_Icons, and one filter at a time (OpenCloseFilter:
    /// choosing the chosen one again clears it; entry.ChooseFilter/ResetRecipes). Terraria's own search
    /// over the item's name (ItemFilters.BySearch) is gone: the panel is the categories alone, so
    /// nothing can be typed into it and an empty grid is just an empty grid.
    /// </summary>
    internal static partial class InventoryGui
    {
        /// <summary>One entry of ItemFilters: Terraria's own text key, the item test, its icon frame.</summary>
        sealed class Filter
        {
            public string Key, Zh;
            /// <summary>Its frame on Images/UI/Creative/Infinite_Icons (ItemFilters/*.GetImage).</summary>
            public int Frame;
            public System.Func<ItemDef, bool> Fits;
        }

        // ItemFilters' entries in their own order; the frames are the ones their GetImage asks for.
        static readonly Filter[] filters =
        {
            new Filter { Key = "CreativePowers.TabWeapons", Zh = "武器", Frame = 0, Fits = d => d.Damage > 0 },  // ItemFilters.Weapon: item.damage > 0
            new Filter { Key = "CreativePowers.TabAccessories", Zh = "饰品", Frame = 1, Fits = d => d.IsAccessory },
            new Filter { Key = "CreativePowers.TabArmor", Zh = "盔甲", Frame = 2, Fits = d => d.IsArmor },
            new Filter { Key = "CreativePowers.TabConsumables", Zh = "消耗品", Frame = 3, Fits = IsConsumable },
            new Filter { Key = "CreativePowers.TabBlocks", Zh = "建筑材料", Frame = 4, Fits = d => d.Places != World.CellKind.Empty || d.PlacesWall > 0 },
            new Filter { Key = "CreativePowers.TabMisc", Zh = "杂项", Frame = 5, Fits = IsMisc },               // the fallback: what no other entry takes
            new Filter { Key = "CreativePowers.TabTools", Zh = "工具", Frame = 6, Fits = IsTool },
            new Filter { Key = "CreativePowers.TabFurniture", Zh = "家具", Frame = 7, Fits = d => d.PlacesStation != World.StationKind.None || d.PlacesChest },
            new Filter { Key = "CreativePowers.TabVanity", Zh = "时装", Frame = 8, Fits = d => false },        // no vanity in Hallownest
            new Filter { Key = "CreativePowers.TabAccessoriesMisc", Zh = "其他装备", Frame = 9, Fits = d => GrapplingHook.Hooks.ContainsKey(d.Type) },
            new Filter { Key = "CreativePowers.TabMaterials", Zh = "材料", Frame = 10, Fits = d => d.Material },
        };

        /// <summary>The chosen entry, null for every recipe (RecipeFilter, null'd by OpenCloseFilter).</summary>
        static Filter filterChosen;

        /// <summary>The recipe grid's columns (the window's own 10 to a row, the bag's slot size).</summary>
        const int CraftWindowColumns = 10;

        static bool IsTool(ItemDef d) => d.PickPower > 0 || d.AxePower > 0 || d.HammerPower > 0 || d.FishingPower > 0 || d.BaitPower > 0 || d.GameTool;

        /// <summary>ItemFilters.Consumables: used up on use (potions, food, crystals, boosters, crates, boss seals).</summary>
        static bool IsConsumable(ItemDef d) => d.Places == World.CellKind.Empty && d.PlacesWall <= 0 && !d.PlacesChest
            && d.PlacesStation == World.StationKind.None
            && (d.Consumable || d.HealLife > 0 || d.HealMana > 0 || d.BuffType > 0 || d.Boost != PermanentBoost.None
                || d.LifeCrystal || d.ManaCrystal || d.Opens || d.BossSummonKey != null || d.QuestFish);

        /// <summary>ItemFilters.MiscFallback: what no other entry takes.</summary>
        static bool IsMisc(ItemDef d)
        {
            foreach (var f in filters)
                if (!ReferenceEquals(f, filters[5]) && f.Fits(d)) return false;
            return true;
        }

        /// <summary>The offered recipes cut down to the chosen entry (ResetRecipes).</summary>
        static void ApplyFilter()
        {
            if (filterChosen != null) recipes.RemoveAll(r => { var d = ItemDb.Get(r.Result); return d != null && !filterChosen.Fits(d); });
        }

        static string FilterName(Filter f) => TerrariaText.Get(Lang.Culture, f.Key) ?? Lang.T(f.Zh);

        /// <summary>Dev: opens the filter panel and pictures it (BepInEx/TerraKnight/dev).</summary>
        public static void DevFilterShot(string name, int index = 6)
        {
            filterChosen = index >= 0 && index < filters.Length ? filters[index] : null;
            RefreshRecipes();
            string dir = System.IO.Path.Combine(BepInEx.Paths.GameRootPath, "BepInEx", "TerraKnight", "dev");
            System.IO.Directory.CreateDirectory(dir);
            string path = System.IO.Path.Combine(dir, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            Plugin.Log.LogInfo($"[AutoTest] 截图 {path}");
        }

        /// <summary>Everything is offered again (the panel's 「全部」, a right click on the magnifier).</summary>
        static void ClearFilter()
        {
            if (filterChosen == null) return;
            filterChosen = null;
            focus = 0;
            focusY = 0f;
            RefreshRecipes();
        }

        // ------------------------------------------------------------------ the panel

        /// <summary>The filter's icon: the Creative icon sheet's frame (or the search icon).</summary>
        static void DrawFilterIcon(Rect r, int frame, float alpha)
        {
            var sheet = TerrariaAssets.Texture("UI/Creative/Infinite_Icons");
            if (sheet == null) return;
            float w = sheet.width / 11f;
            var prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha * prev.a);
            GUI.DrawTextureWithTexCoords(r, sheet, new Rect(frame * w / sheet.width, 0f, w / sheet.width, 1f));
            GUI.color = prev;
        }

        /// <summary>
        /// The crafting window, as Terraria's own is laid out (NewCraftingUI: the hammer it is closed with
        /// at the title's own left, the filter tabs under the title, the recipe grid filling the rest with
        /// a scrollbar down the right). The hammer beside the column opens it and the hammer in its own
        /// top-left corner — the same button, the same look — shuts it again; there is no × anywhere, and
        /// no separate filter button either, the tabs are the filter.
        /// </summary>
        static void DrawCraftWindow(Rect view, float ui, Vector2 mouse, ref ItemStack hovered, ref string hoveredExtra, ref Kind hoveredKind)
        {
            Vector2 P(float x, float y) => new Vector2(view.x + x * ui, view.y + y * ui);

            // The window takes the crafting column's place (the hammer beside it switches between the two):
            // Terraria's panel colour with a darker title bar, its tabs above the title, then the recipe
            // grid, with a scrollbar down the right-hand side. Standard slots, the size the bag's own are
            // drawn at, and the window starts under everything the top-left corner holds — the bag, the
            // trash slot and the two buttons beside them — so it covers nothing (Terraria's crafting
            // window sits below the inventory, it is never laid over it).
            float slot = SlotSize * ui, cell = SlotStep * ui;
            float W = (11f * SlotStep + 26f) * ui;
            float top = view.y + (Margin + 18f + Inventory.Rows * SlotStep + 56f) * ui;
            float H = Mathf.Max(cell + 40f * ui, view.yMax - 24f * ui - top);
            var panel = new Rect(view.x + 14f * ui, top, W, H);
            var prevFill = GUI.color;
            GUI.color = new Color(0.22f, 0.33f, 0.56f, 0.72f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = prevFill;
            Rim(panel, new Color(0.58f, 0.70f, 0.95f, 0.9f), 2f * ui);
            panels.Add(panel);
            // The title row across the very top: the hammer in the window's own top-left corner, then its
            // name. Terraria's window is closed with that hammer (the same "Craft_Toggle" the column has,
            // on its opened frame), not with an ×.
            var head = new Rect(panel.x + 5f * ui, panel.y + 4f * ui, panel.width - 10f * ui, 36f * ui);
            GUI.color = new Color(0.13f, 0.20f, 0.37f, 0.95f);
            GUI.DrawTexture(head, Texture2D.whiteTexture);
            GUI.color = prevFill;
            var toggle = TerrariaAssets.Texture("UI/Craft_Toggle_2");
            var shut = new Rect(head.x + 6f * ui, head.y + 1f * ui, toggle.width * ui, toggle.height * ui);
            bool overShut = shut.Contains(mouse);
            // The opened frame (Craft_Toggle_0, +1 while the pointer is on it), as the column's own button
            // shows the shut one (Craft_Toggle_2).
            GUI.DrawTexture(shut, TerrariaAssets.Texture("UI/Craft_Toggle_" + (overShut ? 1 : 0)));
            Add(shut, Kind.GridToggle, 0);
            if (overShut) DrawLines(mouse, new List<string> { Lang.T("关闭制作窗口") }, Color.white);
            Plain(new Rect(shut.xMax + 8f * ui, head.y + 6f * ui, 80f * ui, 24f * ui), Lang.T("制作"), nameStyleLeft(ui), Color.white);
            // 「全部」 at the title's right: every recipe offered, no tab chosen (gold while none is).
            var all = new Rect(head.xMax - 90f * ui, head.y + 6f * ui, 84f * ui, 24f * ui);
            bool overAll = all.Contains(mouse);
            Outlined(all, Lang.T("全部"), labelStyle, filterChosen == null || overAll ? new Color(1f, 0.85f, 0.3f) : Color.white);
            Add(all, Kind.FilterEntry, -1);
            if (overAll) DrawLines(mouse, new List<string> { Lang.T("显示全部可制作的物品") }, Color.white);
            // Which entries the offered recipes have anything for (ItemFilters' own entries are always
            // listed; an empty one is greyed, so the panel still looks like Terraria's).
            var has = new bool[filters.Length];
            foreach (var r in recipes)
            {
                var d = ItemDb.Get(r.Result);
                if (d == null) continue;
                for (int i = 0; i < filters.Length; i++) if (!has[i] && filters[i].Fits(d)) has[i] = true;
            }

            // The tabs, in their own row under the title, each in a standard slot.
            float ix = panel.x + 13f * ui, iy = panel.y + 44f * ui, step = cell;
            for (int i = 0; i < filters.Length; i++)
            {
                var f = filters[i];
                var rect = new Rect(ix + i * step, iy, slot, slot);
                bool chosen = ReferenceEquals(filterChosen, f);
                bool over = rect.Contains(mouse);
                var tab = TerrariaAssets.Texture("Inventory_Back4");
                var prev = GUI.color;
                GUI.color = chosen ? Color.white : new Color(1f, 1f, 1f, over ? 1f : 0.75f);
                if (tab != null) GUI.DrawTexture(rect, tab);
                GUI.color = prev;
                if (chosen) Rim(rect, new Color(1f, 0.85f, 0.3f), 2f * ui);
                DrawFilterIcon(new Rect(rect.x + 4f * ui, rect.y + 4f * ui, rect.width - 8f * ui, rect.height - 8f * ui), f.Frame,
                    has[i] || chosen ? 1f : 0.3f);
                Add(rect, Kind.FilterEntry, i);
                // Just the entry's name, as Terraria's own hover over a tab gives: no hints.
                if (over) DrawLines(mouse, new List<string> { FilterName(f) }, Color.white);
            }
            // The recipes, in the grid Terraria's crafting window draws: ten to a row of the bag's own
            // standard slots, the focused one on the brighter slot, a scrollbar when rows don't all fit.
            const int gridCols = CraftWindowColumns;
            var grid = new Rect(panel.x + 13f * ui, panel.y + 96f * ui, gridCols * cell, 0f);
            int gridRows = Mathf.Max(1, (int)((panel.height - 104f * ui) / cell));
            int rows = Mathf.Max(1, (recipes.Count + gridCols - 1) / gridCols);
            gridRow = Mathf.Clamp(gridRow, 0, Mathf.Max(0, rows - gridRows));
            int first = gridRow * gridCols;
            for (int i = first; i < recipes.Count && i < first + gridCols * gridRows; i++)
            {
                int k = i - first;
                var rect = new Rect(grid.x + k % gridCols * cell, grid.y + k / gridCols * cell, slot, slot);
                bool over = rect.Contains(mouse);
                GUI.DrawTexture(rect, TerrariaAssets.Texture(i == focus || over ? "Inventory_Back14" : "Inventory_Back4"));
                // The stack the recipe makes, in the slot's corner as the column's own recipes show it.
                DrawIcon(rect, ItemStack.Of(recipes[i].Result, recipes[i].Count), false);
                Add(rect, Kind.Recipe, i);
                if (over)
                {
                    hovered = ItemStack.Of(recipes[i].Result);
                    hoveredKind = Kind.Recipe;
                    hoveredExtra = RecipeExtra(recipes[i]);
                }
            }
            if (rows > gridRows)
            {
                var track = new Rect(panel.xMax - 10f * ui, grid.y, 6f * ui, gridRows * cell);
                Fill(track, new Color(0f, 0f, 0f, 0.35f));
                float span = gridRows / (float)rows;
                var thumb = new Rect(track.x, track.y + (track.height * (1f - span)) * (gridRow / (float)Mathf.Max(1, rows - gridRows)), track.width, track.height * span);
                Fill(thumb, new Color(0.75f, 0.85f, 1f, 0.85f));
            }
        }

        /// <summary>A plain label: Terraria's own windows don't shade their text, so neither does this one.</summary>
        static void Plain(Rect r, string text, GUIStyle style, Color color)
        {
            if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(text)) return;
            style.normal.textColor = color;
            GUI.Label(r, text, style);
        }

        /// <summary>A click on a filter tab, or on the panel's own 「全部」 (index -1).</summary>
        static void ClickFilter(int index, bool leftPress, bool rightPress)
        {
            if (!leftPress && !rightPress) return;
            if (index < 0) { ClearFilter(); Sound("Menu_Tick"); return; }   // the panel's 「全部」
            var f = index >= 0 && index < filters.Length ? filters[index] : null;
            if (f == null) return;
            // OpenCloseFilter: the same entry again clears it; a right click always clears.
            if (ReferenceEquals(filterChosen, f) || rightPress) filterChosen = null;
            else filterChosen = f;
            focus = 0;
            focusY = 0f;
            Sound("Menu_Tick");
            RefreshRecipes();
        }
    }
}
