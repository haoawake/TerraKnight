using System.Collections.Generic;
using System.Linq;
using TerraKnight.Terraria;
using TerraKnight.UI;
using TerraKnight.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight.Items
{
    internal static partial class InventoryGui
    {
        static string homeItemSearch = "";
        static bool homeItemSearchTyping;
        static Rect homePanelRect;
        static List<Chests.HomeSlot> homeVisibleSlots = new List<Chests.HomeSlot>();
        static readonly ImeTextEntry homeItemSearchInput = new ImeTextEntry(c =>
        {
            if (!homeItemSearchTyping || char.IsControl(c) || homeItemSearch.Length >= 64) return;
            homeItemSearch += c; homeStoragePage = 0;
        });
        public static bool HomeMergedVisible => Open && homePanel && !homeEditing && Stations.HomeActive;
        static void EndHomeSearch() { homeItemSearchTyping = false; homeItemSearchInput.End(); }
        static void BeginHomeSearch()
        {
            EndRename(false); EndBookSearch(); homeRenaming = false; homeNameInput.End();
            homeItemSearchTyping = true; homeItemSearchInput.Begin();
        }
        static void TickHomeSearch()
        {
            if (!homeItemSearchTyping) return;
            if (!HomeMergedVisible) { EndHomeSearch(); return; }
            homeItemSearchInput.Tick();
            var before = homeItemSearch;
            homeItemSearch = homeItemSearchInput.TakeBackspaces(homeItemSearch);
            if (before != homeItemSearch) homeStoragePage = 0;
            var kb = Keyboard.current;
            if (kb != null && !homeItemSearchInput.EditingComposition && (kb.enterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame))
            { EndHomeSearch(); homeRenameEnded = Time.frameCount; }
        }

        static void ActUnifiedHomeSlot(int index, bool left, bool right)
        {
            if (!HomeMergedVisible || index < 0 || index >= homeVisibleSlots.Count) return;
            if (!left && (!right || stackSplit > 1)) return;
            int mode = left ? (Shift ? 2 : 0) : 1;
            if (Chests.MoveHomeSlot(homeVisibleSlots[index], mode))
            {
                Sound(left ? "Grab" : "Menu_Tick");
                if (!left) RefreshStackSplitCooldown();
            }
        }

        static void HomeFrame(Rect r, float ui)
        {
            JournalNine(r, "UI/PanelBackground", 8, 12f * ui, new Color(.09f, .12f, .23f, .97f));
            JournalNine(r, "UI/PanelBorder", 8, 12f * ui, new Color(.74f, .79f, .94f, .9f));
        }

        static GUIStyle homeCentreStyle, homeLeftStyle;
        static GUIStyle HomeTextStyle(bool centre)
        {
            var style = centre ? homeCentreStyle : homeLeftStyle;
            if (style == null)
            {
                style = new GUIStyle(labelStyle) { alignment = centre ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft,
                    wordWrap = false, padding = new RectOffset(0, 0, 0, 0) };
                if (centre) homeCentreStyle = style; else homeLeftStyle = style;
            }
            style.fontSize = labelStyle.fontSize;
            return style;
        }

        static GUIStyle homeChipStyle;
        static readonly GUIContent homeChipText = new GUIContent();
        static GUIStyle HomeChipStyle()
        {
            if (homeChipStyle == null) homeChipStyle = new GUIStyle(chipStyle) { clipping = TextClipping.Clip, wordWrap = false };
            homeChipStyle.fontSize = chipStyle.fontSize;
            return homeChipStyle;
        }

        static void DrawHomes(Rect view, float ui, Vector2 mouse, ref ItemStack hovered, ref string extra, ref Kind hoveredKind)
        {
            if (!Stations.HasHomes) { CloseHomePanel(); Stations.LeaveHome(); return; }
            if (homePanel && (statsOpen || blacklistOpen || hkOpen || bestiaryOpen || sellMode || Town.TownNpcs.ShopOpen))
            { CloseHomePanel(); Stations.LeaveHome(); }
            // Exactly the same transparent icon rectangle as the amethyst instrument, next to it.
            var icon = UtilityButtonRect(view, ui, 5);
            Add(icon, Kind.HomeButton, 0);
            var tint = GUI.color;
            if (icon.Contains(mouse) || homePanel) GUI.color = new Color(1f, .9f, .66f);
            GUI.DrawTexture(icon, HomeEverywhereArt.Texture(false), ScaleMode.ScaleToFit);
            GUI.color = tint;
            if (icon.Contains(mouse)) DrawLines(mouse, new List<string> { Lang.T("哪都是家"), Lang.T("打开区域仓库") }, Color.white);
            if (!homePanel) return;
            var pv = BeginPane(Pane.Home, view, ui);
            DrawHomePanel(pv, ui, mouse, ref hovered, ref extra, ref hoveredKind);
            EndPane(Pane.Home);
        }

        static void DrawHomePanel(Rect view, float ui, Vector2 mouse, ref ItemStack hovered, ref string extra, ref Kind hoveredKind)
        {
            var homes = Stations.Homes();
            var editing = homeEditing ? Stations.Home(homeEditKey) : null;
            if (homeEditing && editing == null) { homeEditing = false; homeRenaming = false; homeNameInput.End(); }
            // The homes as chips with their names (4 or 8 a row), only colours once there are too many to name.
            int perRow = homeEditing || homes.Count > 24 ? 16 : homes.Count > 8 ? 8 : 4;
            int colorRows = homeEditing ? 4 : Mathf.Max(1, (homes.Count + perRow - 1) / perRow);
            float width = Mathf.Min(610f * ui, view.width - 90f * ui);
            float y = view.y + 315f * ui;
            float height = homeEditing ? (162f + colorRows * 25f) * ui : Mathf.Min(430f * ui, view.yMax - y - 12f * ui);
            // Right of the crafting column and the focused recipe's ingredients, which stay in sight.
            homePanelRect = new Rect(view.x + Mathf.Min(420f * ui, view.width - width - 140f * ui), y, width, height);
            var panel = homePanelRect;
            if (panel.Contains(mouse)) { hovered = default; extra = null; }
            panels.Add(panel); Add(panel, Kind.HomePanel, 0); HomeFrame(panel, ui);
            string title = homeEditing ? Lang.T("家园 · 光环与名称") : Stations.HomeActive ? Stations.Home(Stations.SelectedHome).Title + Lang.T(" · 区域仓库") : Lang.T("哪都是家 · 选择区域");
            float titleWidth = width - (homeEditing ? 75f : 270f) * ui;
            Outlined(new Rect(panel.x + 15f * ui, panel.y + 9f * ui, titleWidth, 27f * ui), title,
                Fitted(nameStyle, title, titleWidth), Color.white);
            HomeButton(new Rect(panel.xMax - 39f * ui, panel.y + 10f * ui, 24f * ui, 24f * ui), "×", 0, mouse);
            if (!homeEditing)
            {
                HomeButton(new Rect(panel.xMax - 137f * ui, panel.y + 10f * ui, 90f * ui, 24f * ui), "一键堆叠", 7, mouse,
                    "背包里快捷栏以下的物品（收藏的除外）堆进所有家园里已有同种物品的储物空间");
                if (Stations.HomeActive) HomeButton(new Rect(panel.xMax - 237f * ui, panel.y + 10f * ui, 92f * ui, 24f * ui), "颜色 / 名称", 6, mouse,
                    "更换这片区域的光环颜色，或给它改名");
            }
            var occupied = new Dictionary<int, Stations.HomeInfo>(); foreach (var h in homes) occupied[h.Color] = h;
            int visible = 0;
            float swatchStep = (width - 30f * ui) / perRow;
            for (int color = 0; color < 64; color++)
            {
                bool present = occupied.TryGetValue(color, out var home);
                if (!homeEditing && !present) continue;
                int pos = homeEditing ? color : visible++;
                bool available = !homeEditing || Stations.ColorAvailable(color, homeEditKey);
                var r = new Rect(panel.x + 15f * ui + (pos % perRow) * swatchStep, panel.y + (42f + pos / perRow * 25f) * ui, swatchStep - 5f * ui, (perRow < 16 ? 21f : 19f) * ui);
                var c = Stations.HomeColor(color); c.a = available ? .75f : .16f;
                JournalNine(r, "UI/PanelBackground", 8, 5f * ui, c);
                bool selected = homeEditing ? editing.Color == color : Stations.SelectedHome == home?.Key;
                JournalNine(r, "UI/PanelBorder", 8, 5f * ui, selected ? new Color(1f, .92f, .65f, .9f) : new Color(.7f, .75f, .9f, .35f));
                if (available) Add(r, Kind.HomeColor, color);
                if (!available) Outlined(r, "×", labelStyle, new Color(.65f, .65f, .7f));
                else if (present && perRow < 16)
                {
                    // Centred, or from its start and cut at the chip's edge when too long (the whole name over it on hover).
                    var nameRect = new Rect(r.x + 4f * ui, r.y, r.width - 8f * ui, r.height);
                    var style = HomeChipStyle(); homeChipText.text = home.Title;
                    style.alignment = style.CalcSize(homeChipText).x > nameRect.width ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
                    Outlined(nameRect, homeChipText.text, style, Color.white);
                }
                if (r.Contains(mouse)) DrawLines(mouse, new List<string> { present ? home.Title : Lang.F("未占用颜色 {0}", color + 1),
                    homeEditing ? Lang.T(available ? "点击更换光环颜色" : "此颜色已被其他家园占用") : Lang.T("左键打开区域，右键更改颜色和名称") }, Color.white);
            }
            float top = panel.y + (45f + colorRows * 25f) * ui;
            if (homeEditing)
            {
                var nameRect = new Rect(panel.x + 15f * ui, top + 7f * ui, width - 30f * ui, 32f * ui);
                JournalNine(nameRect, "UI/PanelBackground", 8, 6f * ui, new Color(.16f, .19f, .32f));
                string text = homeRenaming ? homeNameDraft + homeNameInput.Composition + "▏" : editing.Title;
                Outlined(nameRect, text, labelStyle, Color.white);
                if (homeRenaming) homeNameInput.Anchor(new Vector2(nameRect.x + 12f * ui, nameRect.yMax));
                HomeButton(new Rect(panel.x + 15f * ui, top + 49f * ui, 130f * ui, 27f * ui), "重命名", 2, mouse);
                HomeButton(new Rect(panel.x + 155f * ui, top + 49f * ui, width - 170f * ui, 27f * ui), homeRenaming ? "保存名称" : "返回家园列表", homeRenaming ? 3 : 1, mouse);
                Outlined(new Rect(panel.x + 15f * ui, top + 84f * ui, width - 30f * ui, 24f * ui), Lang.T("灰色的颜色已被其他家园占用"), labelStyle, new Color(.74f, .78f, .87f));
                return;
            }
            if (!Stations.HomeActive)
            {
                Outlined(new Rect(panel.x + 15f * ui, top + 18f * ui, width - 30f * ui, 70f * ui), Lang.T("选择上方色块打开区域仓库\n同一区域的所有储物空间合并显示"), labelStyle, new Color(.8f, .84f, .94f));
                return;
            }
            bool searching = !string.IsNullOrEmpty(homeItemSearch);
            var search = new Rect(panel.x + 15f * ui, top, width - (searching ? 175f : 143f) * ui, 28f * ui);
            JournalNine(search, "UI/PanelBackground", 8, 6f * ui, new Color(.13f, .16f, .28f));
            JournalNine(search, "UI/PanelBorder", 8, 6f * ui, homeItemSearchTyping ? new Color(.8f, .83f, 1f, .8f) : new Color(.5f, .57f, .73f, .7f));
            Add(search, Kind.HomeSearch, 0);
            string query = homeItemSearch + (homeItemSearchTyping ? homeItemSearchInput.Composition + "▏" : "");
            Outlined(new Rect(search.x + 8f * ui, search.y, search.width - 16f * ui, search.height), string.IsNullOrEmpty(query) ? Lang.T("按物品名称或拼音搜索…") : query,
                HomeTextStyle(false), string.IsNullOrEmpty(query) ? new Color(.62f, .67f, .8f) : Color.white);
            if (homeItemSearchTyping) homeItemSearchInput.Anchor(new Vector2(search.x + 12f * ui, search.yMax));
            if (searching) HomeButton(new Rect(search.xMax + 4f * ui, top, 28f * ui, 28f * ui), "×", 8, mouse, "清除搜索");
            HomeButton(new Rect(panel.xMax - 111f * ui, top, 96f * ui, 28f * ui), "全部存入", 11, mouse,
                "背包里快捷栏以下的物品（收藏的除外）全部存进这片区域");
            var sort = new Rect(panel.x + 15f * ui, panel.yMax - 33f * ui, 24f * ui, 24f * ui);
            Add(sort, Kind.HomeAction, 10);
            GUI.DrawTexture(sort, TerrariaAssets.Texture("UI/Sort_" + (sort.Contains(mouse) ? "1" : "0")), ScaleMode.ScaleToFit);
            if (sort.Contains(mouse)) DrawLines(mouse, new List<string> { Lang.T("排序区域内全部物品"), Lang.T("统一堆叠并排序所有箱子，不受搜索筛选影响") }, Color.white);
            homeVisibleSlots = Chests.UnifiedHomeSlots(Stations.SelectedHome, homeItemSearch);
            float slot = (width - 30f * ui) / 12f - 3f * ui, step = slot + 3f * ui;
            float gridY = top + 36f * ui;
            int rows = Mathf.Max(1, Mathf.FloorToInt((panel.yMax - 39f * ui - gridY) / step));
            int pageSize = rows * 12, pages = Mathf.Max(1, (homeVisibleSlots.Count + pageSize - 1) / pageSize);
            homeStoragePage = Mathf.Clamp(homeStoragePage, 0, pages - 1);
            for (int j = 0; j < pageSize; j++)
            {
                int i = homeStoragePage * pageSize + j; if (i >= homeVisibleSlots.Count) break;
                var entry = homeVisibleSlots[i];
                var r = new Rect(panel.x + 15f * ui + j % 12 * step, gridY + j / 12 * step, slot, slot);
                DrawSlot(r, entry.Item, "Inventory_Back5", false);
                Add(r, Kind.HomeSlot, i);
                if (r.Contains(mouse)) { hovered = entry.Item; hoveredKind = Kind.HomeSlot; extra = Lang.T("区域仓库 · 左键存取，右键取一个，Shift移入背包"); }
            }
            if (homeVisibleSlots.Count == 0) Outlined(new Rect(panel.x + 15f * ui, gridY, width - 30f * ui, 55f * ui), Lang.T("没有匹配的物品"), labelStyle, new Color(.76f, .8f, .89f));
            var stores = Chests.HomeStorages(Stations.SelectedHome);
            int capacity = stores.Sum(s => s.Items.Length), used = stores.Sum(s => s.Items.Count(i => !i.IsEmpty));
            string footer = Chests.HomeTransferPending ? Lang.T("正在同步物品…") : Lang.F("{0} / {1} 格", used, capacity);
            Outlined(new Rect(panel.x + 47f * ui, panel.yMax - 33f * ui, width - 242f * ui, 24f * ui), footer, HomeTextStyle(false), new Color(.77f, .82f, .92f));
            HomeButton(new Rect(panel.xMax - 186f * ui, panel.yMax - 33f * ui, 55f * ui, 24f * ui), "‹", 4, mouse);
            Outlined(new Rect(panel.xMax - 124f * ui, panel.yMax - 33f * ui, 50f * ui, 24f * ui), (homeStoragePage + 1) + " / " + pages, HomeTextStyle(true), Color.white);
            HomeButton(new Rect(panel.xMax - 69f * ui, panel.yMax - 33f * ui, 54f * ui, 24f * ui), "›", 5, mouse);
        }

        internal static void DevHomeSearch(string text) { homeItemSearch = text ?? ""; homeStoragePage = 0; }
        internal static int DevHomeSlotCount => homeVisibleSlots.Count;
        internal static string DevHomeSearchText => homeItemSearch;
        internal static void DevHomeSlotClick(int index, bool right = false) => ActUnifiedHomeSlot(index, !right, right);
    }
}
