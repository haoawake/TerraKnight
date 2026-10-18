using System.Collections.Generic;
using TerraKnight.Terraria;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// An open chest's panel (Main.DrawInventory's chest, context 3; ChestUI.DrawButtons): its 40
    /// slots at 0.755 scale under the inventory, "箱子" beside them with the buttons Loot All,
    /// Deposit All, Quick Stack, Restock and Sort Items. Its slots take clicks as the inventory's do (a left click
    /// swaps with the cursor, a right click held takes one at a time, Shift sends the stack
    /// across); Shift on an inventory slot sends that stack into the chest.
    /// </summary>
    internal static partial class InventoryGui
    {
        static readonly string[] chestButtons = { "全部拿取", "全部存放", "快速堆叠", "补货", "排序物品", "重命名", "取消" };

        // ChestUI's Rename (buttons 5 and 6, RenameChest / RenameChestCancel): the name typed in place of the label.
        static bool renaming;
        static string nameDraft = "";
        static readonly UI.ImeTextEntry nameInput = new UI.ImeTextEntry(TypedChar);

        /// <summary>A chest's name is being typed: the keys are the text's.</summary>
        public static bool TypingName => renaming || Time.frameCount - renameEnded <= 1 || BookTyping;
        /// <summary>The frame the typing ended: its Enter or Esc closes nothing else.</summary>
        static int renameEnded = -10;

        static void StartRename()
        {
            if (Chests.OpenName == null) return;
            EndBookSearch();
            renaming = true;
            nameDraft = Chests.OpenName;
            nameInput.Begin();
            Sound("Menu_Tick");
        }

        static void TypedChar(char c)
        {
            if (!renaming || char.IsControl(c)) return;
            if (nameDraft.Length < Chests.MaxNameLength) nameDraft += c;
        }

        /// <summary>RenameChestSubmit (Enter, Save) or RenameChestCancel (Esc, Cancel).</summary>
        static void EndRename(bool keep)
        {
            if (!renaming) return;
            renaming = false;
            nameInput.End();
            renameEnded = Time.frameCount;
            if (keep) Chests.SetOpenName(nameDraft);
            Sound("Menu_Tick");
        }

        /// <summary>Each frame: Backspace, Enter and Esc while the name is typed; the chest shut ends it.</summary>
        static void TickRename()
        {
            if (!renaming) return;
            if (!Chests.IsOpen || !Open || Chests.BankOpen) { EndRename(false); return; }
            nameInput.Tick();
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null) return;
            if (nameInput.EditingComposition) return;
            if (kb.backspaceKey.wasPressedThisFrame && nameDraft.Length > 0) nameDraft = UI.ImeTextEntry.Backspace(nameDraft);
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) EndRename(true);
            else if (kb.escapeKey.wasPressedThisFrame) EndRename(false);
        }

        /// <summary>The chest panel's title: the name being typed (with its caret), the chest's name, else "箱子".</summary>
        static string ChestTitle()
        {
            if (Chests.BankOpen) return Chests.BankTitle;
            if (renaming) return nameDraft + nameInput.Composition + ((int)(Time.unscaledTime * 2f) % 2 == 0 ? "|" : " ");
            string name = Chests.OpenName;
            return string.IsNullOrEmpty(name) ? Lang.T("箱子") : name;
        }

        static string ChestButtonText(int i)
        {
            // Lang.inter: 29 Loot All, 30 Deposit All, 31 Quick Stack, 82 Restock; 61 Rename, 47 Save while renaming, 63 Cancel.
            if (i == 5) return renaming ? TT("LegacyInterface.47", "保存") : TT("LegacyInterface.61", "重命名");
            if (i == 6) return TT("LegacyInterface.63", "取消");
            return Lang.T(chestButtons[i]);
        }

        /// <summary>Dev: a chest renamed as typing and Enter would.</summary>
        public static void DevRename(string name)
        {
            StartRename();
            nameDraft = name.Length > Chests.MaxNameLength ? name.Substring(0, Chests.MaxNameLength) : name;
            EndRename(true);
        }
        static int chestButtonHover = -1, inventoryButtonHover = -1;

        /// <summary>
        /// Main.DrawInventory's two buttons beside the bag while no chest or shop is open: Quick
        /// Stack to Nearby Chests (ChestStack_0, gold when hovered) and Sort Inventory (Sort_0).
        /// Here under the ammo column, level with the trash.
        /// </summary>
        static void DrawInventoryButtons(Vector2 at, float ui, Vector2 mouse)
        {
            if (Chests.IsOpen || Town.TownNpcs.ShopOpen) return;
            int over = -1;
            for (int i = 0; i < 2; i++)
            {
                var tex = TerrariaAssets.Texture((i == 0 ? "UI/ChestStack_" : "UI/Sort_") + "0");
                var r = new Rect(at.x + i * 36f * ui, at.y, tex.width * ui, tex.height * ui);
                Add(r, Kind.InvButton, i);
                bool hover = r.Contains(mouse);
                if (hover) over = i;
                GUI.DrawTexture(r, TerrariaAssets.Texture((i == 0 ? "UI/ChestStack_" : "UI/Sort_") + (hover ? "1" : "0")));
                if (hover)
                    DrawLines(mouse, new List<string> { TerrariaText.Get(Lang.Culture, i == 0 ? "GameUI.QuickStackToNearby" : "GameUI.SortInventory") ?? Lang.T(i == 0 ? "快速堆叠至附近的箱子" : "排序物品") }, Color.white);
            }
            if (over != inventoryButtonHover && over >= 0) Sound("Menu_Tick");
            inventoryButtonHover = over;
        }

        /// <summary>Main.EquipPage: 0 the armor and accessories, 2 the misc equipment.</summary>
        static int equipPage;
        static readonly string[] miscNames = { "宠物", "照明宠物", "矿车", "坐骑", "钩爪" };
        /// <summary>ItemSlot.Draw's empty-slot icons (Extra_54 frames) for contexts 19, 20, 18, 17, 16.</summary>
        static readonly int[] miscIcons = { 10, 17, 7, 13, 4 };

        /// <summary>
        /// Main.DrawPageIcons: the misc equipment button (EquipPage 2: DisplaySlots_2, _3 while on it, _6 behind it hovered)
        /// and 48 px to its left the housing button (EquipPage 1: DisplaySlots_4, _5 while on it, _7 behind it hovered), whose
        /// page is the townspeople's list (Town.TownNpcs.PanelOpen) in the equipment column's place.
        /// </summary>
        static void DrawEquipPageIcon(Vector2 at, float ui, Vector2 mouse)
        {
            bool housing = Town.TownNpcs.PanelOpen;
            PageIcon(at, ui, mouse, 0, !housing && equipPage == 2 ? "UI/DisplaySlots_3" : "UI/DisplaySlots_2", "UI/DisplaySlots_6",
                !housing && equipPage == 2 ? "装备" : "其他装备");
            PageIcon(new Vector2(at.x - 48f * ui, at.y), ui, mouse, 1, housing ? "UI/DisplaySlots_5" : "UI/DisplaySlots_4", "UI/DisplaySlots_7",
                housing ? "装备" : "房屋");
        }

        static void PageIcon(Vector2 at, float ui, Vector2 mouse, int page, string texture, string glowTexture, string tip)
        {
            var tex = TerrariaAssets.Texture(texture);
            var r = new Rect(at.x, at.y, tex.width * 0.9f * ui, tex.height * 0.9f * ui);
            Add(r, Kind.EquipPage, page);
            if (r.Contains(mouse))
            {
                var glow = TerrariaAssets.Texture(glowTexture);
                var prev = GUI.color;
                GUI.color = new Color(1f, 0.85f, 0.3f);
                GUI.DrawTexture(new Rect(r.x - 2f * ui, r.y - 2f * ui, glow.width * 0.9f * ui, glow.height * 0.9f * ui), glow);
                GUI.color = prev;
                DrawLines(mouse, new List<string> { Lang.T(tip) }, Color.white);
            }
            GUI.DrawTexture(r, tex);
        }

        /// <summary>The misc page (Main.DrawInventory, EquipPage 2): pet, light pet, minecart, mount and hook, one under another.</summary>
        static void DrawMiscEquips(float ax, float ay, float slot, float step, float ui, Vector2 mouse, ref ItemStack hovered, ref Kind hoveredKind)
        {
            Outlined(new Rect(ax - 40f * ui, ay - 24f * ui, slot + 80f * ui, 20f * ui), Lang.T("其他装备"), labelStyle, Color.white);
            for (int i = 0; i < Inventory.MiscSlots; i++)
            {
                var rect = new Rect(ax, ay + i * step, slot, slot);
                Add(rect, Kind.Misc, i);
                DrawEquipSlot(rect, inv.Misc[i], miscIcons[i], !inv.Mouse.IsEmpty && Inventory.FitsMisc(inv.Mouse.Def, i), default, -1);
                if (!rect.Contains(mouse)) continue;
                hovered = inv.Misc[i];
                hoveredKind = Kind.Misc;
                if (inv.Misc[i].IsEmpty) DrawLines(mouse, new List<string> { Lang.T(miscNames[i]) }, Color.white);
            }
        }

        /// <summary>A left click on a misc slot: the cursor's item goes in if it is that slot's kind (swapping), else the slot's comes onto the cursor.</summary>
        static void LeftClickMisc(int i)
        {
            ref var s = ref inv.Misc[i];
            if (!inv.Mouse.IsEmpty)
            {
                if (!Inventory.FitsMisc(inv.Mouse.Def, i) || inv.Mouse.Count != 1) return;
                (s, inv.Mouse) = (inv.Mouse, s);
                s.Favorite = false;
                Sound("Grab");
                return;
            }
            if (s.IsEmpty) return;
            inv.Mouse = s;
            s = default;
            Sound("Grab");
        }

        static void ActInventoryButton(int index)
        {
            if (index == 0)
            {
                var hero = HeroController.SilentInstance;
                if (hero == null) return;
                int moved = Chests.QuickStackNearby(inv, hero.GetComponent<BoxCollider2D>().bounds.center);
                Sound(moved > 0 ? "Grab" : "Menu_Tick");
                return;
            }
            // ItemSorting.SortInventory: the bag below the hotbar, favourites left where they are.
            ItemSorting.Sort(inv.Slots, Inventory.HotbarSize, Inventory.Size, true);
            Sound("Grab");
        }

        static void DrawChestPanel(Rect view, float ui, Vector2 mouse, ref ItemStack hovered, ref string hoveredExtra, ref Kind hoveredKind)
        {
            var items = Chests.OpenItems;
            if (items == null) return;
            float s = 52f * 0.755f * ui, step = 56f * 0.755f * ui;
            float x0 = view.x + 73f * ui, y0 = view.y + (258f + 18f + SlotStep) * ui;   // a slot lower than Terraria's: the trash slot stays clear
            Outlined(new Rect(view.x + 504f * ui, y0 - 6f * ui, 300f * ui, 30f * ui), ChestTitle(), nameStyleLeft(ui), renaming ? new Color(1f, 0.85f, 0.3f) : Color.white);
            if (renaming)
            {
                float width = nameStyleLeft(ui).CalcSize(new GUIContent(nameDraft)).x;
                nameInput.Anchor(new Vector2(view.x + 504f * ui + Mathf.Min(width, 280f * ui), y0 + 24f * ui));
            }
            panels.Add(new Rect(x0, y0, 560f * 0.755f * ui, 224f * 0.755f * ui));
            var prev = GUI.color;
            for (int i = 0; i < Chests.Capacity; i++)
            {
                int col = i % 10, row = i / 10;
                var rect = new Rect(x0 + col * step, y0 + row * step, s, s);
                Add(rect, Kind.ChestSlot, i);
                GUI.color = new Color(220 / 255f, 220 / 255f, 220 / 255f, 220 / 255f);
                GUI.DrawTexture(rect, TerrariaAssets.Texture(ChestSlotBack(items[i])));
                GUI.color = prev;
                var st = items[i];
                if (st.IsEmpty) continue;
                DrawChestIcon(rect, st);
                if (!rect.Contains(mouse)) continue;
                hovered = st;
                hoveredKind = Kind.ChestSlot;
                // The Potion Box: how near a buff potion is to being kept for good.
                if (Chests.PotionBoxOpen && Containers.IsBuffPotion(st.Def))
                    hoveredExtra = st.Favorite ? Lang.T("已收藏：这种增益永久生效，Alt+左键取消收藏即失效") : Lang.F("已攒 {0}/{1} 瓶：满 {1} 瓶后 Alt+左键收藏，这种增益永久生效", Mathf.Min(st.Count, Containers.FavoriteAt), Containers.FavoriteAt);
            }
            // The buttons, one under another beside the slots; the hovered one bigger and gold.
            int over = -1;
            for (int i = 0; i < chestButtons.Length; i++)
            {
                // The bank has no name; Cancel only while renaming.
                if ((i == 5 && Chests.BankOpen) || (i == 6 && !renaming) || HideChestButton(i)) continue;
                var r = new Rect(view.x + 506f * ui, y0 + (30f + i * 28f) * ui, 120f * ui, 24f * ui);
                Add(r, Kind.ChestButton, i);
                bool hover = r.Contains(mouse);
                if (hover) over = i;
                var style = nameStyleLeft(ui);
                int size = style.fontSize;
                style.fontSize = Mathf.RoundToInt((hover ? 20 : 17) * ui);
                Outlined(r, ChestButtonText(i), style, hover ? new Color(1f, 0.85f, 0.3f) : Color.white);
                style.fontSize = size;
            }
            if (over != chestButtonHover && over >= 0) Sound("Menu_Tick");
            chestButtonHover = over;
            // A Banner Case's or a Potion Box's pages (InventoryGuiContainers.cs).
            DrawContainerPager(view, ui, y0, mouse);
        }

        static void ActChest(int index, bool leftPress, bool rightPress, bool rightHeld)
        {
            var items = Chests.OpenItems;
            if (items == null) return;
            ref var slot = ref items[index];
            // A container's own rules first: what it takes, Alt+click's favourite, the sell list's entries (InventoryGuiContainers.cs).
            if (ContainerAct(index, ref slot, leftPress)) return;
            if (leftPress)
            {
                if (Shift && !slot.IsEmpty)
                {
                    // ItemSlot.ShiftClick in a chest: the stack goes into the inventory, as much as fits.
                    int left = inv.Pickup(slot);
                    if (left != slot.Count) Sound("Grab");
                    if (left <= 0) slot = default;
                    else slot.Count = left;
                    return;
                }
                if (inv.Mouse.IsEmpty && slot.IsEmpty) return;
                if (RefuseHkOutside()) return;
                if (CanStack(inv.Mouse, slot) && slot.Count < slot.Def.MaxStack && inv.Mouse.Count < inv.Mouse.Def.MaxStack)
                {
                    int moved = Mathf.Min(inv.Mouse.Count, slot.Def.MaxStack - slot.Count);
                    slot.Count += moved;
                    inv.Mouse.Count -= moved;
                    if (inv.Mouse.Count <= 0) inv.Mouse = default;
                }
                else (inv.Mouse, slot) = (slot, inv.Mouse);
                slot.Favorite = false;
                Sound("Grab");
                return;
            }
            if (!rightHeld || slot.IsEmpty || stackSplit > 1) return;
            for (int k = 0; k <= superFastStack && !slot.IsEmpty; k++)
            {
                if (!(inv.Mouse.IsEmpty || (CanStack(inv.Mouse, slot) && inv.Mouse.Count < inv.Mouse.Def.MaxStack))) break;
                if (inv.Mouse.IsEmpty) inv.Mouse = new ItemStack { Type = slot.Type, Prefix = slot.Prefix, Forge = slot.Forge };
                inv.Mouse.Count++;
                slot.Count--;
                if (slot.Count <= 0) slot = default;
                Sound("Menu_Tick");
                RefreshStackSplitCooldown();
            }
        }

        static void ActChestButton(int index)
        {
            if (ActContainerButton(index)) return;
            if (index == 0) Chests.LootAll(inv);
            else if (index == 1) Chests.DepositAll(inv);
            else if (index == 2) Chests.QuickStack(inv);
            else if (index == 3) Chests.Restock(inv);
            else if (index == 5) { if (renaming) EndRename(true); else StartRename(); return; }
            else if (index == 6) { EndRename(false); return; }
            else Chests.SortOpen();
            Sound("Grab");
        }

        /// <summary>Dev: a left click (with the Dev modifiers) on a chest slot, and a press of one of its buttons.</summary>
        public static void DevChestClick(int slot) => ActChest(slot, true, false, false);
        public static void DevChestButton(int index) => ActChestButton(index);
    }
}
