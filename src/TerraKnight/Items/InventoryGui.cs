using System.Collections.Generic;
using TerraKnight.Terraria;
using TerraKnight.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight.Items
{
    /// <summary>
    /// Terraria's inventory screen, drawn with IMGUI in the same places as Terraria: the hotbar in
    /// the top-left corner with the selected item's name above it, the rest of the inventory and
    /// the trash slot below it when open, the equipment column (armor, then accessories) down the
    /// right-hand side with the defense shield beside it, and the crafting column at the
    /// bottom-left, which holds everything unlocked (free while testing; for geo at a bench or in
    /// Dirtmouth otherwise). Clicks follow ItemSlot: left click picks up, puts down, merges or
    /// swaps; right click equips armor and accessories, otherwise takes one (held, faster and
    /// faster, Main.stackSplit); Alt+click marks a favourite; Ctrl+click throws into the trash (or,
    /// with the shop open, sells); Shift+click takes equipment off. Clicking the crafting column's
    /// selected item takes one onto the cursor, holding it keeps taking.
    /// </summary>
    internal static partial class InventoryGui
    {
        const float SlotSize = 44f;   // Terraria's 52 px slot at inventoryScale 0.85
        /// <summary>The ammo slots are drawn smaller, as Terraria's coin and ammo columns are (inventoryScale 0.6 of 0.85).</summary>
        const float AmmoSlotSize = SlotSize * 0.8f;
        /// <summary>The coin column's width (Main.DrawInventory's coins at x 497, the ammo at 534).</summary>
        const float CoinStep = AmmoSlotSize + 2f;
        /// <summary>Right of the bag: the coin and ammo columns, then the Geo line's notes and the book's button beyond them.</summary>
        internal static float RightOfAmmo(float ui) => Margin * ui + Inventory.Columns * SlotStep * ui + 8f * ui + CoinStep * ui + AmmoSlotSize * ui + 14f * ui;
        const float SlotStep = 47.6f; // 56 px spacing at the same scale
        const float Margin = 20f;
        const float RecipeStep = 65f;

        static bool open;

        /// <summary>The inventory is open. The monster book is folded away each time it opens (it would cover the bag).</summary>
        public static bool Open
        {
            get => open;
            set
            {
                if (value && !open) bestiaryOpen = false;
                open = value;
            }
        }

        /// <summary>The mouse was over a panel last frame; item use is blocked while it is.</summary>
        public static bool MouseOverUi { get; private set; }

        enum Kind { Slot, Armor, Accessory, Trash, Recipe, GridRecipe, GridToggle, Settings, Loadout, Buff, ChatButton, ShopSlot, BestiaryButton, BestiaryEntry, BestiaryPanel, ChestSlot, ChestButton, Pvp, Team, GuideSlot, InvButton, Misc, EquipPage, HkButton, HkBag, SellButton, SellSlot, ReforgeSlot, ReforgeButton, ForgeButton }

        struct Hit
        {
            public Rect Rect;
            public Kind Kind;
            public int Index;
        }

        static GUIStyle labelStyle, nameStyle, tipStyle, buttonStyle;
        static readonly List<Rect> panels = new List<Rect>();
        static readonly List<Hit> hits = new List<Hit>();
        static string notice;
        static float noticeUntil;

        // Crafting column: the recipes, the focused one and how far the column still has to slide.
        static readonly List<Recipe> recipes = new List<Recipe>();
        static int focus;
        static float focusY;
        static bool grid;
        static int gridRow;

        // Main.stackSplit and friends: the hold-to-repeat timing of right-click take-one and crafting.
        static int stackSplit, stackDelay = 7, stackCounter, superFastStack, timesTried;
        static bool preventCraft;
        static int craftingNow = -1;
        static float tickAccumulator;
        static bool leftPress, rightPress;

        static Inventory inv => TerraMod.Instance.Inventory;

        // ------------------------------------------------------------------ input (polled)

        /// <summary>Every frame of play: the inventory's clicks, held buttons and the wheel, on Terraria's 60 Hz tick.</summary>
        public static void Tick()
        {
            var mouse = Mouse.current;
            if (mouse == null || TerraMod.Instance == null) return;
            TickRename();
            leftPress |= UI.TerraCursor.LeftPressed(mouse);
            rightPress |= mouse.rightButton.wasPressedThisFrame;
            if (Open)
            {
                // Terraria: with the inventory open the wheel scrolls the crafting column.
                float wheel = mouse.scroll.ReadValue().y;
                if (bestiaryOpen)
                {
                    // The monster book's list scrolls instead while it is open; over a long list of drops, those.
                    if (OverDrops)
                    {
                        if (wheel > 0.1f) ScrollDrops(-1);
                        else if (wheel < -0.1f) ScrollDrops(1);
                    }
                    else if (wheel > 0.1f) ScrollBestiary(-1);
                    else if (wheel < -0.1f) ScrollBestiary(1);
                }
                else if (wheel > 0.1f) Scroll(grid ? -1 : 1);
                else if (wheel < -0.1f) Scroll(grid ? 1 : -1);
            }
            tickAccumulator += Mathf.Min(Time.unscaledDeltaTime, 0.1f) * 60f;
            while (tickAccumulator >= 1f)
            {
                tickAccumulator -= 1f;
                UiTick(mouse);
            }
        }

        static void UiTick(Mouse mouse)
        {
            // Main.cs, start of the frame: the repeat delay shortens the longer the button is held.
            if (stackSplit == 0)
            {
                timesTried = 0;
                stackCounter = 0;
                stackDelay = 7;
                superFastStack = 0;
            }
            else
            {
                stackCounter++;
                int threshold = stackDelay == 7 ? 30 : stackDelay == 6 ? 25 : stackDelay == 5 ? 20 : stackDelay == 4 ? 15 : stackDelay == 3 ? 10 : 5;
                if (stackCounter >= threshold)
                {
                    stackDelay = Mathf.Max(2, stackDelay - 1);
                    superFastStack++;
                    if (timesTried < 10) superFastStack = 0;
                    timesTried++;
                    stackCounter = 0;
                }
            }
            // The crafting column eases to its focused recipe (DrawRecipes_AdjustRecipeOffsetSnappy).
            float old = focusY;
            focusY = Mathf.MoveTowards(focusY * 0.97f, 0f, RecipeStep / 10f);
            if ((int)(old / RecipeStep) != (int)(focusY / RecipeStep)) Sound("Menu_Tick");

            bool left = UI.TerraCursor.LeftHeld(mouse), right = mouse.rightButton.isPressed;
            bool lp = leftPress, rp = rightPress;
            leftPress = rightPress = false;
            if (!left && !right)
            {
                preventCraft = false;
                craftingNow = -1;
            }
            if (TerraMod.Instance != null && GameHooks.IsGameplay(GameManager.UnsafeInstance) && !UI.FullMap.Open)
            {
                var m = UI.TerraCursor.ScreenMouse(mouse);
                var at = new Vector2(m.x, Screen.height - m.y);
                bool onUi = false;
                for (int i = hits.Count - 1; i >= 0; i--)
                {
                    if (!hits[i].Rect.Contains(at)) continue;
                    onUi = true;
                    Act(hits[i], lp, rp, left, right);
                    break;
                }
                // Player.dropItemCheck: a right click off the interface (and off a pylon) throws the cursor's stack.
                if (rp && Open && !onUi && !MouseOverUi && !inv.Mouse.IsEmpty && !World.Pylons.HoveringPylon)
                    TerraMod.Instance.ThrowHeld(true);
            }
            // End of the frame: let go of both buttons and the repeat starts over.
            if (!left && !right) stackSplit = 0;
            if (stackSplit > 0) stackSplit--;
        }

        static void RefreshStackSplitCooldown() => stackSplit = stackSplit == 0 ? 30 : stackDelay;

        static bool Shift => DevShift || Keyboard.current != null && !Net.NetChat.Typing && (Keyboard.current.leftShiftKey.isPressed || Keyboard.current.rightShiftKey.isPressed);
        static bool Ctrl => DevCtrl || Keyboard.current != null && (Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed);
        static bool Alt => DevAlt || Player.TerraKeys.Held(Player.TerraKeys.Action.Favorite);

        /// <summary>Dev: the topmost click region at a GUI point, what a click there acts on ("Kind#index").</summary>
        public static string DevHitAt(Vector2 gui)
        {
            for (int i = hits.Count - 1; i >= 0; i--)
                if (hits[i].Rect.Contains(gui)) return hits[i].Kind + "#" + hits[i].Index;
            return "无";
        }

        /// <summary>Dev: where a click region was last drawn (its kind's name and index).</summary>
        public static Rect? DevRectOf(string kind, int index)
        {
            foreach (var h in hits) if (h.Kind.ToString() == kind && h.Index == index) return h.Rect;
            return null;
        }

        /// <summary>Dev: modifier keys held for the autotest, which has no keyboard.</summary>
        public static bool DevShift, DevCtrl, DevAlt;
        public static string DevModifiers => $"Shift {Shift} Ctrl {Ctrl} Alt {Alt}";

        /// <summary>Dev: a left click on an inventory slot (with whatever Dev modifiers are set).</summary>
        public static void DevLeftClick(int slot) => LeftClickSlot(slot);

        /// <summary>Dev: a right click (its first press) on an inventory slot.</summary>
        public static void DevRightClick(int slot)
        {
            stackSplit = 0;
            RightClickSlot(slot, true);
        }

        static void Act(Hit hit, bool leftPress, bool rightPress, bool leftHeld, bool rightHeld)
        {
            switch (hit.Kind)
            {
                case Kind.Slot:
                    if (!Open)
                    {
                        if (leftPress) inv.Selected = hit.Index;
                        return;
                    }
                    if (leftPress) LeftClickSlot(hit.Index);
                    else if (rightHeld) RightClickSlot(hit.Index, rightPress);
                    return;
                case Kind.Armor:
                case Kind.Accessory:
                    if (leftPress) LeftClickEquip(hit.Kind == Kind.Armor, hit.Index);
                    else if (rightHeld && stackSplit <= 1)
                    {
                        // Right click on an equipped item: it simply comes off onto the cursor (no longer shared).
                        ref var slot = ref hit.Kind == Kind.Armor ? ref inv.Armor[hit.Index] : ref inv.Accessories[hit.Index];
                        if (!slot.IsEmpty && inv.Mouse.IsEmpty)
                        {
                            inv.Mouse = slot;
                            inv.Mouse.Favorite = false;
                            slot = default;
                            Sound("Menu_Tick");
                            RefreshStackSplitCooldown();
                        }
                    }
                    return;
                case Kind.Trash:
                    if (leftPress) ClickTrash();
                    return;
                case Kind.Recipe:
                    ClickRecipe(hit.Index, leftPress || rightPress, leftHeld || rightHeld);
                    return;
                case Kind.GridRecipe:
                    if (leftPress)
                    {
                        // The classic grid: clicking a recipe brings it to the column and closes the grid.
                        focus = hit.Index;
                        focusY = 0f;
                        grid = false;
                        Sound("Menu_Tick");
                    }
                    return;
                case Kind.GridToggle:
                    if (leftPress)
                    {
                        grid = !grid;
                        gridRow = Mathf.Max(0, focus / Mathf.Max(1, gridColumns) - 1);
                        Sound("Menu_Tick");
                    }
                    return;
                case Kind.Settings:
                    if (!leftPress) return;
                    if (hit.Index == 0) TerraMod.Instance?.OpenGameMenu();
                    else TerraMod.Instance?.OpenSettings();
                    return;
                case Kind.Loadout:
                    // Main.DrawLoadoutButtons: a fresh click switches (the cursor's item doesn't matter).
                    if (leftPress) TerraMod.Instance?.TrySwitchLoadout(hit.Index);
                    return;
                case Kind.Buff:
                    // Main.TryRemovingBuff: a right click takes a buff (not a debuff) away.
                    if (rightPress) Player.PlayerBuffs.TryRemoveAt(hit.Index);
                    return;
                case Kind.ChatButton:
                    if (!leftPress) return;
                    if (hit.Index == 0)
                    {
                        Town.TownNpcs.OpenShop();
                        // Main.OpenShop: the click that opened it can't buy.
                        stackSplit = 9999;
                    }
                    else if (hit.Index == 2) Town.TownNpcs.OpenGuideCraft();
                    else if (hit.Index == 3) Town.TownNpcs.AnglerQuest();
                    else if (hit.Index == 4) Town.TownNpcs.NurseHeal();
                    else if (hit.Index == 5) Town.TownNpcs.TaxCollect();
                    else if (hit.Index == 6) Town.TownNpcs.OpenReforge();
                    else if (hit.Index == 7) Town.TownNpcs.OpenForge();
                    else Town.TownNpcs.Close(true);
                    return;
                case Kind.GuideSlot:
                    ActGuideSlot(leftPress);
                    return;
                case Kind.ShopSlot:
                    ActShop(hit.Index, leftPress, rightPress, leftHeld, rightHeld);
                    return;
                case Kind.HkButton:
                    if (leftPress) ToggleHkBag();
                    return;
                case Kind.HkBag:
                    if (leftPress) ClickHkBag(hit.Index);
                    return;
                case Kind.SellButton:
                    if (leftPress) ToggleSellMode();
                    return;
                case Kind.SellSlot:
                    ActSold(hit.Index, leftPress);
                    return;
                case Kind.ReforgeSlot:
                    ActReforgeSlot(leftPress);
                    return;
                case Kind.ReforgeButton:
                    if (leftPress) Reforge();
                    return;
                case Kind.ForgeButton:
                    if (leftPress) Forge();
                    return;
                case Kind.BestiaryButton:
                    if (leftPress) ToggleBestiary();
                    return;
                case Kind.BestiaryEntry:
                    if (leftPress) SelectBestiary(hit.Index);
                    return;
                case Kind.BestiaryPanel:
                    return;
                case Kind.ChestSlot:
                    ActChest(hit.Index, leftPress, rightPress, rightHeld);
                    return;
                case Kind.ChestButton:
                    if (leftPress) ActChestButton(hit.Index);
                    return;
                case Kind.InvButton:
                    if (leftPress) ActInventoryButton(hit.Index);
                    return;
                case Kind.EquipPage:
                    if (leftPress)
                    {
                        equipPage = equipPage == 2 ? 0 : 2;
                        Sound("Menu_Open");
                    }
                    return;
                case Kind.Misc:
                    if (leftPress) LeftClickMisc(hit.Index);
                    else if (rightPress && inv.Mouse.IsEmpty && !inv.Misc[hit.Index].IsEmpty)
                    {
                        inv.Mouse = inv.Misc[hit.Index];
                        inv.Misc[hit.Index] = default;
                        Sound("Menu_Tick");
                    }
                    return;
                case Kind.Pvp:
                case Kind.Team:
                    if (leftPress) ClickPvp(hit.Index, hit.Kind == Kind.Team);
                    return;
            }
        }

        // --- Slots (ItemSlot.LeftClick / RightClick / OverrideLeftClick). ---

        static bool CanStack(ItemStack a, ItemStack b) => !a.IsEmpty && !b.IsEmpty && a.Type == b.Type && a.Def.MaxStack > 1;

        static void LeftClickSlot(int i)
        {
            ref var slot = ref inv.Slots[i];
            if (Alt && !slot.IsEmpty)
            {
                // ToggleFavorited.
                slot.Favorite = !slot.Favorite;
                Sound("Menu_Tick");
                return;
            }
            // Quick selling on: the click sells the stack (InventoryGuiSell.cs).
            if (SellFromSlot(ref slot)) return;
            // ItemSlot.OverrideHover / SellOrTrash: with an NPC's shop open, Shift+click sells the stack too.
            if (Shift && Town.TownNpcs.ShopOpen && !slot.IsEmpty && !slot.Favorite && !slot.Def.Permanent)
            {
                Town.TownNpcs.Sell(ref slot);
                return;
            }
            if (Ctrl && !slot.IsEmpty && !slot.Favorite && !slot.Def.Permanent)
            {
                if (Town.TownNpcs.ShopOpen)
                {
                    // An NPC's shop open: Ctrl+click sells instead (it waits in the shop to be bought back).
                    Town.TownNpcs.Sell(ref slot);
                    return;
                }
                inv.Trash = slot;
                inv.Trash.Favorite = false;
                slot = default;
                TrashSound();
                Sound("Grab");
                return;
            }
            // A Hallownest item: Shift+click sends it to its bag; put down only in the hotbar.
            if (Shift && ShiftHkToBag(ref slot)) return;
            if (i >= Inventory.HotbarSize && RefuseHkOutside()) return;
            // ItemSlot.ShiftClick with a chest open: the stack goes into it.
            if (Shift && World.Chests.IsOpen && !slot.IsEmpty && !slot.Favorite)
            {
                World.Chests.Put(ref slot);
                return;
            }
            if (inv.Mouse.IsEmpty && slot.IsEmpty) return;
            // An ammo slot takes ammo only (ItemSlot.PickItemMovementAction, context 2: FitsAmmoSlot), the coin slot money (context 1).
            if (Inventory.IsAmmoSlot(i) && !inv.Mouse.IsEmpty && !Inventory.FitsAmmoSlot(inv.Mouse.Def)) return;
            if (Inventory.IsCoinSlot(i) && !inv.Mouse.IsEmpty && !Inventory.FitsCoinSlot(inv.Mouse.Def)) return;
            // MouseItemSwap, then top the slot up from the cursor when they stack.
            if (CanStack(inv.Mouse, slot) && slot.Count < slot.Def.MaxStack && inv.Mouse.Count < inv.Mouse.Def.MaxStack)
            {
                int moved = Mathf.Min(inv.Mouse.Count, slot.Def.MaxStack - slot.Count);
                slot.Count += moved;
                inv.Mouse.Count -= moved;
                if (inv.Mouse.Count <= 0) inv.Mouse = default;
            }
            else (inv.Mouse, slot) = (slot, inv.Mouse);
            Sound("Grab");
        }

        static void RightClickSlot(int i, bool press)
        {
            ref var slot = ref inv.Slots[i];
            if (slot.IsEmpty) return;
            // ItemSlot.TryItemSwap (IS:1645-1652, 1951-1986): a Chaos Cylinder turns to its next mode.
            if (press && TrySwapItem(ref slot)) return;
            // ItemSlot.TryOpenContainer: a crate (or an Oyster) opens, one at a time.
            if (slot.Def.Opens && inv.Mouse.IsEmpty)
            {
                if (press && !TreasureBags.TryOpen(inv, i)) Fishing.TryOpen(inv, i);
                return;
            }
            // A single piece of armor or an accessory: right click puts it on (SwapEquip).
            if ((inv.Mouse.IsEmpty || !CanStack(slot, inv.Mouse)) && slot.Count == 1 && (slot.Def.IsArmor || slot.Def.IsAccessory))
            {
                if (press) SwapEquip(i);
                return;
            }
            // A hook, a pet, a light pet (the misc equipment's): right click puts it in its slot.
            int misc = slot.Count == 1 ? Inventory.MiscSlotOf(slot.Def) : -1;
            if ((inv.Mouse.IsEmpty || !CanStack(slot, inv.Mouse)) && misc >= 0)
            {
                if (press)
                {
                    (inv.Misc[misc], slot) = (slot, inv.Misc[misc]);
                    inv.Misc[misc].Favorite = false;
                    Sound("Grab");
                }
                return;
            }
            if (stackSplit > 1) return;
            for (int k = 0; k <= superFastStack && !slot.IsEmpty; k++)
            {
                if (!(inv.Mouse.IsEmpty || (CanStack(inv.Mouse, slot) && inv.Mouse.Count < inv.Mouse.Def.MaxStack))) break;
                // PickupItemIntoMouse: the cursor copy is a favourite only if the whole (single) item was.
                bool fav = slot.Favorite && slot.Count == 1;
                if (inv.Mouse.IsEmpty) inv.Mouse = new ItemStack { Type = slot.Type, Prefix = slot.Prefix, Forge = slot.Forge };
                inv.Mouse.Count++;
                inv.Mouse.Favorite = fav;
                slot.Count--;
                if (slot.Count <= 0) slot = default;
                Sound("Menu_Tick");
                RefreshStackSplitCooldown();
            }
        }

        /// <summary>The Chaos Cylinders' modes in turn: rows, full, random, simple; Flairon's two forms (IS:1967-1970); Unlock.</summary>
        static bool TrySwapItem(ref ItemStack slot)
        {
            ItemType next;
            switch (slot.Type)
            {
                case ItemType.ChaosCylinder: next = ItemType.ChaosCylinderFull; break;
                case ItemType.ChaosCylinderFull: next = ItemType.ChaosCylinderRandom; break;
                case ItemType.ChaosCylinderRandom: next = ItemType.ChaosCylinderSimple; break;
                case ItemType.ChaosCylinderSimple: next = ItemType.ChaosCylinder; break;
                case ItemType.Flairon: next = ItemType.FlaironFlail; break;
                case ItemType.FlaironFlail: next = ItemType.Flairon; break;
                default: return false;
            }
            if (slot.Count != 1) return false;
            slot.Type = next;
            Sound("Unlock");
            return true;
        }

        /// <summary>
        /// ItemSlot.SwapEquip: armor to its slot; an accessory to the slot holding the same one,
        /// else the first empty one, else the first. What was worn goes where the new piece was.
        /// </summary>
        static void SwapEquip(int i)
        {
            ref var slot = ref inv.Slots[i];
            if (slot.Def.IsArmor)
            {
                int a = (int)slot.Def.Armor - 1;
                var worn = inv.Armor[a];
                inv.Armor[a] = slot;
                inv.Armor[a].Favorite = false;
                slot = worn;
                slot.Favorite = false;
                Sound("Grab");
                return;
            }
            int target = -1;
            for (int k = 0; k < Inventory.OpenAccessories && target < 0; k++)
                if (!inv.Accessories[k].IsEmpty && inv.Accessories[k].Type == slot.Type) target = k;
            // Only one pair of wings: new wings take the old ones' place.
            for (int k = 0; k < Inventory.OpenAccessories && target < 0 && slot.Def.Wing > 0; k++)
                if (!inv.Accessories[k].IsEmpty && inv.Accessories[k].Def.Wing > 0) target = k;
            for (int k = 0; k < Inventory.OpenAccessories && target < 0; k++)
                if (inv.Accessories[k].IsEmpty) target = k;
            if (target < 0) target = 0;
            var old = inv.Accessories[target];
            inv.Accessories[target] = slot;
            inv.Accessories[target].Favorite = false;
            slot = old;
            slot.Favorite = false;
            Sound("Grab");
        }

        /// <summary>Equipment slots (PickItemMovementAction 1): no merging, and one item at a time from a stack.</summary>
        static void LeftClickEquip(bool armor, int index)
        {
            ref var slot = ref armor ? ref inv.Armor[index] : ref inv.Accessories[index];
            // ItemSlot.ToggleLoadoutShare: Alt+click shares a worn item with the other loadouts (only
            // one loadout can share a given slot).
            if (Alt && !slot.IsEmpty)
            {
                slot.Favorite = !slot.Favorite;
                if (slot.Favorite)
                {
                    ShareHintSeen = true;
                    foreach (var l in inv.Loadouts)
                    {
                        if (armor) l.Armor[index].Favorite = false;
                        else l.Accessories[index].Favorite = false;
                    }
                }
                Sound("Menu_Tick");
                return;
            }
            if (Shift)
            {
                // Unequip into the inventory (a reforged or forged one whole, its prefix and level kept).
                if (!slot.IsEmpty && BackToBag(slot)) slot = default;
                Sound("Menu_Tick");
                return;
            }
            if (!inv.Mouse.IsEmpty && !Fits(inv.Mouse, armor, index)) return;
            if (inv.Mouse.IsEmpty)
            {
                if (slot.IsEmpty) return;
                inv.Mouse = slot;
                inv.Mouse.Favorite = false;
                slot = default;
            }
            else if (slot.IsEmpty)
            {
                // The cursor's item itself, one of it: its prefix and forging go with it.
                slot = inv.Mouse;
                slot.Count = 1;
                slot.Favorite = false;
                inv.Mouse.Count--;
                if (inv.Mouse.Count <= 0) inv.Mouse = default;
            }
            else if (inv.Mouse.Count == 1)
            {
                (inv.Mouse, slot) = (slot, inv.Mouse);
                inv.Mouse.Favorite = false;
                slot.Favorite = false;
            }
            else return;
            Sound("Grab");
        }

        /// <summary>Dev: a click on the trash slot (with whatever Dev modifiers are set).</summary>
        public static void DevClickTrash() => ClickTrash();

        /// <summary>Into the bag, all or nothing: a plain stack by its type (onto stacks of it), a reforged or forged one whole.</summary>
        static bool BackToBag(ItemStack s) => s.Prefix == 0 && s.Forge == 0 ? inv.Add(s.Type, s.Count) : inv.Pickup(s) == 0;

        /// <summary>Dev: a left click on an equipment slot (with whatever Dev modifiers are set).</summary>
        public static void DevLeftClickEquip(bool armor, int index) => LeftClickEquip(armor, index);

        /// <summary>Dev: how many of each class the crafting column offers now.</summary>
        public static string DevRecipeKits()
        {
            RefreshRecipes();
            var n = new int[5];
            foreach (var r in recipes) n[(int)r.Def.Kit]++;
            return $"共 {recipes.Count}：通用 {n[0]} 近战 {n[1]} 远程 {n[2]} 魔法 {n[3]} 召唤 {n[4]}";
        }

        /// <summary>Dev: the crafting column's list made n times; milliseconds each.</summary>
        public static double DevRefreshMs(int n)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < n; i++) RefreshRecipes();
            return sw.Elapsed.TotalMilliseconds / Mathf.Max(1, n);
        }

        /// <summary>Dev: the items the crafting column offers now.</summary>
        public static List<ItemType> DevOffered()
        {
            RefreshRecipes();
            var list = new List<ItemType>();
            foreach (var r in recipes) list.Add(r.Result);
            return list;
        }

        /// <summary>Dev: whether an item could go in an accessory slot.</summary>
        public static bool DevFits(ItemStack item, int index) => Fits(item, false, index);

        /// <summary>CanEquipInArmorSlot: the right armor slot, or an accessory not worn in another slot already.</summary>
        static bool Fits(ItemStack item, bool armor, int index)
        {
            if (armor) return item.Def.IsArmor && (int)item.Def.Armor - 1 == index;
            if (!item.Def.IsAccessory) return false;
            for (int k = 0; k < Inventory.AccessorySlots; k++)
            {
                if (k == index || inv.Accessories[k].IsEmpty) continue;
                if (inv.Accessories[k].Type == item.Type) return false;
                // CanEquipBothAccessories: never two pairs of wings.
                if (item.Def.Wing > 0 && inv.Accessories[k].Def.Wing > 0) return false;
            }
            return true;
        }

        static void ClickTrash()
        {
            if (Shift)
            {
                if (!inv.Trash.IsEmpty && BackToBag(inv.Trash)) inv.Trash = default;
                Sound("Menu_Tick");
                return;
            }
            if (!inv.Mouse.IsEmpty)
            {
                // Kept for good (the Dream Nail): the trash won't take it.
                if (inv.Mouse.Def.Permanent) return;
                // The old trash is destroyed and the cursor's item takes its place.
                inv.Trash = inv.Mouse;
                inv.Trash.Favorite = false;
                inv.Mouse = default;
                TrashSound();
                Sound("Grab");
            }
            else if (!inv.Trash.IsEmpty)
            {
                inv.Mouse = inv.Trash;
                inv.Trash = default;
                Sound("Grab");
            }
        }

        static void TrashSound() => TerraMod.Instance?.PlaySound("Custom/trash_item_" + Random.Range(0, 2), 0.55f);

        // --- Crafting (Main.HoverOverCraftingItemButton). ---

        /// <summary>
        /// Recipe.FindRecipes: the recipes whose ingredients are all in the inventory and whose
        /// station the progress has reached, plus the basic items bought for geo; while testing,
        /// everything for free. Every loadout sees every recipe, of whichever class.
        /// </summary>
        static void RefreshRecipes()
        {
            int keep = focus >= 0 && focus < recipes.Count ? (int)recipes[focus].Result : -1;
            recipes.Clear();
            var guide = Town.TownNpcs.GuideCraftOpen ? Town.TownNpcs.GuideItem : default;
            if (!guide.IsEmpty)
            {
                // Recipe.CollectGuideRecipes: every recipe the Guide's item goes into, whatever can be made now.
                foreach (var r in Recipes.All)
                    if (!r.IsBought && System.Array.Exists(r.Ingredients, g => g.Type == guide.Type)) recipes.Add(r);
            }
            else if (Shop.Free)
            {
                foreach (var d in Shop.Everything) recipes.Add(Recipe.Bought(d, true));
            }
            else
            {
                // Terraria's own conditions: made by hand anywhere, else at its station set down within reach (Player.AdjTiles;
                // the altars stand in the Black Egg Temple). Only the basic items bought for geo want a bench or Dirtmouth.
                var hero = HeroController.SilentInstance;
                bool here = CraftingHere;
                if (here) foreach (var d in Shop.Basics) recipes.Add(Recipe.Bought(d, false));
                // The list is made again every frame the column is drawn: what every recipe asks the same of is found once
                // (the progress, the stations and waters in reach, what the bag and the chests near the player hold), as
                // CanMakeHere and Recipes.HasIngredients would find it for each.
                int stage = Shop.UnlockedStage();
                bool entry = ProgressionBalance.EntryClear, final = ProgressionBalance.FinalClear;
                nearStations.Clear();
                foreach (var s in World.Stations.NearNames(hero)) nearStations.Add(s);
                System.Array.Clear(environments, 0, environments.Length);
                CountCarried(World.Chests.ForCrafting(hero));
                foreach (var r in Recipes.All)
                {
                    if (r.StationStage > stage || !ProgressionBalance.Unlocked(r.Result, stage, entry, final)) continue;
                    if (r.IsBought ? !here : !ByHand(r) && !nearStations.Contains(r.Station)) continue;
                    if (!EnvironmentHere(r.Environment)) continue;
                    if (!inv.Infinite && !Carried(r)) continue;
                    recipes.Add(r);
                }
                // A basic item that can be made from what is carried isn't offered for money as well (a torch from wood and gel).
                var made = new HashSet<ItemType>();
                foreach (var r in recipes) if (!r.IsBought) made.Add(r.Result);
                recipes.RemoveAll(r => r.IsBought && made.Contains(r.Result));
            }
            // TryRefocusingRecipe: stay on the same item when the list changes.
            int found = recipes.FindIndex(r => (int)r.Result == keep);
            focus = found >= 0 ? found : Mathf.Clamp(focus, 0, Mathf.Max(0, recipes.Count - 1));
        }

        static readonly HashSet<string> nearStations = new HashSet<string>();
        static readonly Dictionary<ItemType, int> carried = new Dictionary<ItemType, int>();

        /// <summary>The crafting environments found this refresh (0 not asked yet, 1 here, -1 not).</summary>
        static readonly sbyte[] environments = new sbyte[16];

        static bool EnvironmentHere(CraftEnvironment e)
        {
            if (e == CraftEnvironment.None) return true;
            int i = (int)e;
            if (i < 0 || i >= environments.Length) return CraftEnvironments.Available(e);
            if (environments[i] == 0) environments[i] = (sbyte)(CraftEnvironments.Available(e) ? 1 : -1);
            return environments[i] > 0;
        }

        /// <summary>How many of each item the bag and the chests near the player hold (Recipes.Count, all at once).</summary>
        static void CountCarried(List<ItemStack[]> chests)
        {
            carried.Clear();
            Tally(inv.Slots);
            if (chests != null) foreach (var slots in chests) Tally(slots);
        }

        static void Tally(ItemStack[] slots)
        {
            foreach (var s in slots)
                if (!s.IsEmpty) carried[s.Type] = (carried.TryGetValue(s.Type, out int n) ? n : 0) + s.Count;
        }

        /// <summary>Recipes.HasIngredients' count against what CountCarried found.</summary>
        static bool Carried(Recipe r)
        {
            foreach (var (t, need) in r.Ingredients)
                if (!carried.TryGetValue(t, out int n) || n < need) return false;
            return true;
        }

        static bool CraftingHere => Shop.Free || Shop.Available(HeroController.SilentInstance);

        /// <summary>Made by hand (Recipe.requiredTile -1): anywhere.</summary>
        static bool ByHand(Recipe r) => !r.IsBought && (string.IsNullOrEmpty(r.Station) || r.Station == "徒手");

        /// <summary>
        /// A recipe can be made, as in Terraria: by hand anywhere, else at its station set down within reach, once the
        /// progress has reached its stage. A basic item bought for geo: at a Hallownest bench or in Dirtmouth.
        /// </summary>
        static bool CanMakeHere(Recipe r, bool here, int stage) =>
            ProgressionBalance.Unlocked(r.Result) && r.StationStage <= stage && CraftEnvironments.Available(r.Environment)
            && (r.IsBought ? here : ByHand(r) || World.Stations.Near(r.Station, HeroController.SilentInstance));

        static void Scroll(int delta)
        {
            if (grid)
            {
                gridRow = Mathf.Clamp(gridRow + delta, 0, Mathf.Max(0, (recipes.Count - 1) / Mathf.Max(1, gridColumns)));
                return;
            }
            if (recipes.Count == 0) return;
            int next = Mathf.Clamp(focus + delta, 0, recipes.Count - 1);
            if (next == focus) return;
            // The column slides from where it was (VisuallyRepositionRecipes).
            focusY += (next - focus) * RecipeStep;
            focus = next;
        }

        static void ClickRecipe(int i, bool press, bool held)
        {
            if (i < 0 || i >= recipes.Count) return;
            if (i != focus)
            {
                if (!press) return;
                focusY += (i - focus) * RecipeStep;
                focus = i;
                stackSplit = 15;
                preventCraft = true;
                Sound("Menu_Tick");
                return;
            }
            // The Guide's list is to look at: nothing is made from it (focusRecipe == recipeIndex && guideItem.IsAir).
            if (Town.TownNpcs.GuideCraftOpen && !Town.TownNpcs.GuideItem.IsEmpty) return;
            var r = recipes[i];
            var d = r.Def;
            if(!Shop.Free && !ProgressionBalance.Unlocked(r.Result)) return;
            // Unstackable results never repeat while held.
            bool blocked = craftingNow != -1 && (craftingNow != i || d.MaxStack == 1);
            // A recipe made by hand (no station) needs no bench, as in Terraria.
            if (!held || blocked || preventCraft || stackSplit > 1 || (!Shop.Free && !r.IsBought && !CanMakeHere(r, CraftingHere, Shop.UnlockedStage())) || (r.IsBought && !CraftingHere)) return;
            int count = (superFastStack + 1) * (Shift ? 10 : 1);
            int made = 0;
            var near = r.IsBought ? null : World.Chests.ForCrafting(HeroController.SilentInstance);
            for (int k = 0; k < count; k++)
            {
                // TryAllowingToCraftRecipe: an empty cursor, or the same item with room on it.
                if (!(inv.Mouse.IsEmpty || (inv.Mouse.Type == r.Result && inv.Mouse.Count + r.Count <= d.MaxStack))) break;
                if (r.IsBought)
                {
                    if (!Shop.Spend(r.Cost))
                    {
                        Notice(Lang.T("吉欧不够"));
                        break;
                    }
                }
                else
                {
                    // Recipe.Create: the ingredients go, the result comes onto the cursor.
                    if (!Recipes.HasIngredients(r, inv, near)) break;
                    Recipes.Consume(r, inv, near);
                }
                if (inv.Mouse.IsEmpty) inv.Mouse = ItemStack.Of(r.Result, 0);
                inv.Mouse.Count += r.Count;
                made++;
            }
            if (made == 0) return;
            craftingNow = i;
            RefreshStackSplitCooldown();
            Sound("Grab");
            mouseItemGlow = 1f;
        }

        static float mouseItemGlow;

        static void Notice(string text)
        {
            notice = text;
            noticeUntil = Time.unscaledTime + 1.5f;
        }

        // ------------------------------------------------------------------ drawing

        static int gridColumns = 1;

        public static void Draw(Inventory inv, bool smartCursor)
        {
            var e = Event.current;
            var view = GameView.Rect;
            float ui = GameView.Scale;
            EnsureStyles(ui);
            if (e.type != EventType.Repaint) return;
            panels.Clear();
            hits.Clear();
            if (!Open) sellMode = false;

            float slot = SlotSize * ui, step = SlotStep * ui;
            float x0 = view.x + Margin * ui, y0 = view.y + Margin * ui + 18f * ui;
            var mouse = UI.TerraCursor.GuiMouse();
            ItemStack hovered = default;
            string hoveredExtra = null;
            Kind hoveredKind = Kind.Slot;

            // Hotbar row (always) and the rest of the grid (open).
            int rows = Open ? Inventory.Rows : 1;
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < Inventory.Columns; c++)
            {
                int i = r * Inventory.Columns + c;
                bool selected = i == inv.Held;
                float s = !Open && selected ? slot * 1.15f : slot;
                var rect = new Rect(x0 + c * step - (s - slot) / 2f, y0 + r * step - (s - slot) / 2f, s, s);
                Add(rect, Kind.Slot, i);
                var st = inv.Slots[i];
                string back = st.Favorite && !st.IsEmpty ? "Inventory_Back10" : selected ? "Inventory_Back14" : Open && r == 0 ? "Inventory_Back9" : "Inventory_Back";
                DrawSlot(rect, st, back, inv.Infinite);
                if (r == 0) Outlined(new Rect(rect.x + 4f * ui, rect.y + 1f * ui, 20f * ui, 18f * ui), ((c + 1) % 10).ToString(), labelStyle, Color.white);
                if (rect.Contains(mouse))
                {
                    hovered = st;
                    hoveredKind = Kind.Slot;
                    if ((Town.TownNpcs.ShopOpen || sellMode) && !st.IsEmpty) hoveredExtra = SellLine(st);
                }
            }

            // Selected item name above the hotbar, like Terraria.
            var current = inv.Current;
            string title = Open ? (TerraMod.Instance != null && TerraMod.Instance.Creative ? Lang.T("物品栏（创造模式）") : Lang.T("物品栏")) : current.IsEmpty ? "" : current.Def.Name;
            Outlined(new Rect(x0, view.y + 2f * ui, Inventory.Columns * step, 22f * ui), title, nameStyle,
                Open || current.IsEmpty ? new Color(1f, 0.95f, 0.6f) : ItemDb.RarityColor(current.Def.Rare));
            // The buff bar under the hotbar while the inventory is closed; the smart cursor's note under it.
            int buffRows = Open ? 0 : DrawBuffs(view, ui, mouse);
            float smartY = buffRows > 0 ? view.y + (94f + buffRows * 50f) * ui : y0 + rows * step + 2f * ui;
            if (smartCursor)
                Outlined(new Rect(x0, smartY, 200f * ui, 20f * ui), Lang.T("智能光标：开"), labelStyle, new Color(1f, 0.85f, 0.3f));

            if (Open)
            {
                // The trash slot, under the last column of the grid (Main.DrawTrashItemSlot).
                var trash = new Rect(x0 + 9 * step, y0 + Inventory.Rows * step, slot, slot);
                Add(trash, Kind.Trash, 0);
                DrawSlot(trash, inv.Trash, "Inventory_Back7", false);
                if (inv.Trash.IsEmpty)
                {
                    var icon = TerrariaAssets.Texture("Trash");
                    var prev = GUI.color;
                    GUI.color = new Color(100 / 255f, 100 / 255f, 100 / 255f, 100 / 255f);
                    float w = icon.width * 0.85f * ui, h = icon.height * 0.85f * ui;
                    GUI.DrawTexture(new Rect(trash.center.x - w / 2f, trash.center.y - h / 2f, w, h), icon);
                    GUI.color = prev;
                }
                if (trash.Contains(mouse))
                {
                    hovered = inv.Trash;
                    hoveredKind = Kind.Trash;
                    if (inv.Trash.IsEmpty) DrawLines(mouse, new List<string> { Lang.T("垃圾桶") }, Color.white);
                }
                DrawInventoryButtons(new Vector2(x0 + Inventory.Columns * step + 8f * ui, y0 + Inventory.Rows * step + 4f * ui), ui, mouse);

                // Geo, where Terraria keeps the coin slots.
                float rx = x0 + Inventory.Columns * step + 8f * ui;
                Outlined(new Rect(rx, y0, 200f * ui, 22f * ui), Lang.F("吉欧：{0}", Shop.Geo), labelStyle, new Color(1f, 0.9f, 0.55f));
                var stats = TerraMod.Instance.Stats;
                if (stats.PotionSickness > 0f)
                    Outlined(new Rect(view.x + RightOfAmmo(ui), y0 + 22f * ui, 220f * ui, 22f * ui), Lang.F("药水病：{0} 秒", Mathf.CeilToInt(stats.PotionSickness)), labelStyle, new Color(1f, 0.6f, 0.6f));

                // The coin slot (Main.DrawInventory's coin column, one slot of its four): right of the bag level
                // with its second row, where the Geo picked up goes first.
                float ammoS = AmmoSlotSize * ui;
                {
                    int i = Inventory.CoinSlot;
                    Outlined(new Rect(rx - 4f * ui, y0 + step - 18f * ui, ammoS + 8f * ui, 18f * ui), Lang.T("钱币"), labelStyle, Color.white);
                    var rect = new Rect(rx, y0 + step + (slot - ammoS) / 2f, ammoS, ammoS);
                    Add(rect, Kind.Slot, i);
                    var st = inv.Slots[i];
                    DrawSlot(rect, st, st.Favorite && !st.IsEmpty ? "Inventory_Back10" : "Inventory_Back", inv.Infinite);
                    if (rect.Contains(mouse))
                    {
                        hovered = st;
                        hoveredKind = Kind.Slot;
                        if (st.IsEmpty) DrawLines(mouse, new List<string> { Lang.T("钱币") }, Color.white);
                    }
                }

                // The ammo slots (Main.DrawInventory's ammo column, inventory 54-57), right of the coin
                // column level with the bag's four lower rows: what a bow or a gun takes first, top to bottom.
                float ammoX = rx + CoinStep * ui;
                Outlined(new Rect(ammoX - 4f * ui, y0 + step - 18f * ui, ammoS + 8f * ui, 18f * ui), Lang.T("弹药"), labelStyle, Color.white);
                for (int k = 0; k < Inventory.AmmoSlots; k++)
                {
                    int i = Inventory.Size + k;
                    var rect = new Rect(ammoX, y0 + (k + 1) * step + (slot - ammoS) / 2f, ammoS, ammoS);
                    Add(rect, Kind.Slot, i);
                    var st = inv.Slots[i];
                    DrawSlot(rect, st, st.Favorite && !st.IsEmpty ? "Inventory_Back10" : "Inventory_Back", inv.Infinite);
                    if (rect.Contains(mouse))
                    {
                        hovered = st;
                        hoveredKind = Kind.Slot;
                        if ((Town.TownNpcs.ShopOpen || sellMode) && !st.IsEmpty) hoveredExtra = SellLine(st);
                        if (st.IsEmpty) DrawLines(mouse, new List<string> { Lang.T("弹药") }, Color.white);
                    }
                }

                // Equipment column on the right, under the life and mana display: armor, then accessories.
                float ax = view.xMax - 120f * ui, ay = view.y + 230f * ui;
                DrawEquipPageIcon(new Vector2(ax + 52f * ui, ay - 46f * ui), ui, mouse);
                if (equipPage == 2) DrawMiscEquips(ax, ay, slot, step, ui, mouse, ref hovered, ref hoveredKind);
                else {
                Outlined(new Rect(ax - 40f * ui, ay - 24f * ui, slot + 80f * ui, 20f * ui), Lang.T("装备"), labelStyle, Color.white);
                for (int i = 0; i < Inventory.ArmorSlots; i++)
                {
                    var rect = new Rect(ax, ay + i * step, slot, slot);
                    Add(rect, Kind.Armor, i);
                    DrawEquipSlot(rect, inv.Armor[i], i == 0 ? 0 : i == 1 ? 6 : 12, !inv.Mouse.IsEmpty && Fits(inv.Mouse, true, i), inv.EffectiveArmor[i], inv.ArmorSharedFrom[i]);
                    if (rect.Contains(mouse)) HoverEquip(inv.Armor[i], inv.EffectiveArmor[i], inv.ArmorSharedFrom[i], Kind.Armor, ref hovered, ref hoveredExtra, ref hoveredKind);
                }
                DrawDefense(new Vector2(ax - 40f * ui, ay + step), inv, ui, e);
                // A LAN game: PvP and the teams, left of the equipment (Main.DrawPVPIcons).
                DrawPvpIcons(ax, ay, step, ui, mouse);
                float accY = ay + Inventory.ArmorSlots * step + 4f * ui;
                for (int i = 0; i < Inventory.OpenAccessories; i++)
                {
                    var rect = new Rect(ax, accY + i * step, slot, slot);
                    Add(rect, Kind.Accessory, i);
                    DrawEquipSlot(rect, inv.Accessories[i], 11, !inv.Mouse.IsEmpty && Fits(inv.Mouse, false, i), inv.EffectiveAccessories[i], inv.AccessorySharedFrom[i]);
                    if (rect.Contains(mouse)) HoverEquip(inv.Accessories[i], inv.EffectiveAccessories[i], inv.AccessorySharedFrom[i], Kind.Accessory, ref hovered, ref hoveredExtra, ref hoveredKind);
                }

                // The loadout buttons beside the armor column (Main.DrawLoadoutButtons): 32 px, 36 apart.
                int overLoadout = -1;
                for (int i = 0; i < Inventory.LoadoutCount; i++)
                {
                    var btn = new Rect(ax + 52f * ui, ay + 36f * i * ui, 32f * ui, 32f * ui);
                    Add(btn, Kind.Loadout, i);
                    bool over = btn.Contains(mouse);
                    if (over) overLoadout = i;
                    DrawLoadoutButton(btn, i, i == inv.CurrentLoadout, over, ui);
                }
                // DoStatefulTickSound: a tick each time the pointer comes onto another button.
                if (overLoadout >= 0 && overLoadout != loadoutHover) Sound("Menu_Tick");
                loadoutHover = overLoadout;
                if (overLoadout >= 0 && hovered.IsEmpty) DrawLines(mouse, new List<string> { Lang.F("配装{0}（{1}）", overLoadout + 1, Lang.T(loadoutClass[overLoadout])) + (inv.BagPerLoadout ? Lang.T("：盔甲、饰品和背包") : "") }, Color.white);
                }

                // The crafting column gives way to an open shop; beside an open chest it stays (Main.DrawInventory draws the
                // recipes whatever chest is open, under its rows), the chest's items counted.
                // Quick selling's row takes the chest's place; a chest or a shop opened ends it.
                if (sellMode && (World.Chests.IsOpen || Town.TownNpcs.ShopOpen || Town.TownNpcs.ReforgeOpen)) sellMode = false;
                if (Town.TownNpcs.ShopOpen) DrawShopPanel(view, ui, mouse, ref hovered, ref hoveredExtra, ref hoveredKind);
                // The Goblin Tinkerer's reforging: its slot in the crafting column's place.
                else if (Town.TownNpcs.ForgeMode) DrawForge(view, ui, mouse, ref hovered, ref hoveredKind);
                else if (Town.TownNpcs.ReforgeOpen) DrawReforge(view, ui, mouse, ref hovered, ref hoveredKind);
                else
                {
                    if (World.Chests.IsOpen) DrawChestPanel(view, ui, mouse, ref hovered, ref hoveredExtra, ref hoveredKind);
                    else if (sellMode) DrawSellRow(view, ui, mouse, ref hovered, ref hoveredExtra, ref hoveredKind);
                    DrawCrafting(view, ui, mouse, ref hovered, ref hoveredExtra, ref hoveredKind);
                }
                DrawBestiaryButton(view, ui, mouse);
                DrawHkButton(view, ui, mouse);
                DrawSellButton(view, ui, mouse);

                // Terraria's Settings button in the bottom-right corner: "设置" opens the game's own menu, "泰拉瑞亚设置" over it
                // Terraria's (the autoswing switch, the key bindings).
                for (int k = 0; k < 2; k++)
                {
                    var settings = new Rect(view.xMax - 330f * ui, view.yMax - (k == 0 ? 50f : 92f) * ui, 310f * ui, 36f * ui);
                    Add(settings, Kind.Settings, k);
                    bool hover = settings.Contains(mouse);
                    buttonStyle.fontSize = Mathf.RoundToInt((hover ? 30 : 26) * ui);
                    Outlined(settings, Lang.T(k == 0 ? "设置" : "泰拉瑞亚设置"), buttonStyle, hover ? new Color(1f, 0.85f, 0.3f) : Color.white);
                }
            }

            DrawChat(view, ui, mouse);
            BookHint = null;
            if (Open && bestiaryOpen) DrawBestiary(view, ui, mouse, ref hovered, ref hoveredExtra, ref hoveredKind);
            if (Open && hkOpen && !bestiaryOpen) DrawHkBag(view, ui, mouse, ref hovered, ref hoveredExtra, ref hoveredKind);
            MouseOverUi = UI.WorldMap.MouseOnMinimap(mouse);
            foreach (var p in panels)
                if (p.Contains(mouse)) MouseOverUi = true;

            // Main.DrawInterface_ChestNameHover's: a named chest's name under the cursor in the world.
            if (!MouseOverUi && TerraMod.Instance != null)
            {
                string chestName = World.Chests.NameAt(TerraMod.Instance.AimPoint);
                if (chestName != null) DrawLines(mouse, new List<string> { chestName }, Color.white);
            }
            if (notice != null && Time.unscaledTime < noticeUntil)
                Outlined(new Rect(mouse.x + 20f * ui, mouse.y - 26f * ui, 300f * ui, 22f * ui), notice, labelStyle, new Color(1f, 0.5f, 0.45f));

            // The item held on the cursor follows the mouse (no background), glowing a moment after crafting.
            if (!inv.Mouse.IsEmpty)
            {
                var r = new Rect(mouse.x + 6f * ui, mouse.y + 6f * ui, slot, slot);
                if (mouseItemGlow > 0f)
                {
                    float g = mouseItemGlow * mouseItemGlow;
                    float k = 1f + (1f - g);
                    var prev = GUI.color;
                    GUI.color = new Color(1f, 231 / 255f, 69 / 255f, g * 0.6f);
                    var big = new Rect(r.center.x - r.width * k / 2f, r.center.y - r.height * k / 2f, r.width * k, r.height * k);
                    DrawIconOnly(big, inv.Mouse);
                    GUI.color = prev;
                    mouseItemGlow = Mathf.Max(0f, mouseItemGlow - 0.035f * Time.unscaledDeltaTime * 60f);
                }
                DrawIcon(r, inv.Mouse, inv.Infinite);
            }
            else if (!hovered.IsEmpty)
            {
                // A favourite mark on worn equipment means shared, not favourite.
                DrawTooltip(mouse, hovered.Def, hoveredExtra, hovered.Favorite && hoveredKind == Kind.Slot);
            }
            if (Open) DrawCursorOverride(mouse, hovered, hoveredKind, ui);
            // A townsperson under the cursor: its name (Main.HoverOverNPCs).
            string npcName = Town.TownNpcs.HoverName();
            if (npcName != null && hovered.IsEmpty && inv.Mouse.IsEmpty && !MouseOverUi) DrawLines(mouse, new List<string> { npcName }, Color.white);
            // A locked drop in the monster book: when it unlocks.
            if (BookHint != null && hovered.IsEmpty && inv.Mouse.IsEmpty) DrawLines(mouse, new List<string> { BookHint }, new Color(0.85f, 0.85f, 0.95f));
        }

        /// <summary>Main.buffAlpha: each icon brightens while hovered, fading back to 0.4.</summary>
        static readonly float[] buffAlpha = new float[Player.PlayerBuffs.MaxBuffs];
        static GUIStyle buffStyle;

        /// <summary>
        /// DrawInterface_Resources_Buffs (inventory closed): 11 icons a row, 38 px apart, rows 50
        /// apart, under the hotbar; under each its time ("3 秒", "2 分") or, for a minion buff, how
        /// many minions ("x3"). Hovering one shows its name and description and keeps the click
        /// off the world. Returns the rows drawn.
        /// </summary>
        static int DrawBuffs(Rect view, float ui, Vector2 mouse)
        {
            if (buffStyle == null) buffStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft, wordWrap = false };
            buffStyle.fontSize = Mathf.RoundToInt(13 * ui);
            int n = 0, hovered = -1;
            float step = Mathf.Min(Time.unscaledDeltaTime, 0.1f) * 60f;
            for (int i = 0; i < Player.PlayerBuffs.MaxBuffs; i++)
            {
                int type = Player.PlayerBuffs.Type[i];
                if (type == 0)
                {
                    buffAlpha[i] = 0.4f;
                    continue;
                }
                int col = n % 11, row = n / 11;
                var rect = new Rect(view.x + (32f + col * 38f) * ui, view.y + (94f + row * 50f) * ui, 32f * ui, 32f * ui);
                n++;
                Add(rect, Kind.Buff, i);
                bool over = rect.Contains(mouse);
                if (over) hovered = i;
                buffAlpha[i] = Mathf.Clamp(buffAlpha[i] + (over ? 0.1f : -0.05f) * step, 0.4f, 1f);
                float a = buffAlpha[i];
                var prev = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, a);
                GUI.DrawTexture(rect, TerrariaAssets.Texture("Buff_" + type));
                GUI.color = prev;
                string text = null;
                if (Player.PlayerBuffs.IsMinionBuff(type))
                {
                    int count = Minions.Instance != null ? Minions.Instance.BuffCount(type) : 0;
                    if (count > 0) text = "x" + count;
                }
                else if (!Player.PlayerBuffs.NoTime(type) && Player.PlayerBuffs.Time[i] > 2) text = Player.PlayerBuffs.Duration(Player.PlayerBuffs.Time[i]);
                if (text != null) Outlined(new Rect(rect.x, rect.yMax, 80f * ui, 18f * ui), text, buffStyle, new Color(a, a, a, a));
            }
            if (hovered >= 0)
            {
                int type = Player.PlayerBuffs.Type[hovered];
                DrawLines(mouse, new List<string> { Player.PlayerBuffs.Name(type), Player.PlayerBuffs.Desc(type) }, Color.white);
            }
            return n == 0 ? 0 : (n - 1) / 11 + 1;
        }

        /// <summary>Dev: whether a point (GUI coordinates) is on the interface as last drawn.</summary>
        public static bool DevOverUi(Vector2 p) => UI.WorldMap.MouseOnMinimap(p) || panels.Exists(r => r.Contains(p));

        static void Add(Rect rect, Kind kind, int index)
        {
            panels.Add(rect);
            hits.Add(new Hit { Rect = rect, Kind = kind, Index = index });
        }

        /// <summary>ItemSlot.OverrideHover: Alt shows the favourite star, Ctrl the trash can (or the sell coin), Shift on equipment the take-off arrow.</summary>
        static void DrawCursorOverride(Vector2 mouse, ItemStack hovered, Kind kind, float ui)
        {
            if (hovered.IsEmpty || !panels.Exists(p => p.Contains(mouse))) return;
            string icon = null;
            if (kind == Kind.Slot && Alt) icon = "UI/Cursor_3";
            else if (kind == Kind.Slot && hovered.Favorite) return;
            else if (kind == Kind.Slot && Ctrl) icon = Town.TownNpcs.ShopOpen ? "UI/Cursor_10" : "UI/Cursor_6";
            else if (kind == Kind.Slot && Shift && Town.TownNpcs.ShopOpen) icon = "UI/Cursor_10";
            else if (kind == Kind.Slot && sellMode && inv.Mouse.IsEmpty && Sellable(hovered, out _)) icon = "UI/Cursor_10";
            else if ((kind == Kind.Armor || kind == Kind.Accessory) && Alt) icon = "UI/Cursor_3";
            else if ((kind == Kind.Armor || kind == Kind.Accessory || kind == Kind.Trash) && Shift) icon = "UI/Cursor_7";
            if (icon == null) return;
            var tex = TerrariaAssets.Texture(icon);
            GUI.DrawTexture(new Rect(mouse.x, mouse.y, tex.width * ui * 1.1f, tex.height * ui * 1.1f), tex);
        }

        /// <summary>
        /// CraftingUI.DrawRecipesList: the focused recipe big at the top of the column with the
        /// others at three-quarter size fading out above and below, the label over it, and the
        /// grid toggle beside it; or, toggled, every recipe in a grid.
        /// </summary>
        /// <summary>Dev: the crafting column was drawn since this was last read.</summary>
        public static bool DevCraftingDrawn
        {
            get { bool d = craftingDrawn; craftingDrawn = false; return d; }
        }

        static bool craftingDrawn;

        static void DrawCrafting(Rect view, float ui, Vector2 mouse, ref ItemStack hovered, ref string hoveredExtra, ref Kind hoveredKind)
        {
            craftingDrawn = true;
            RefreshRecipes();
            float H = view.height / ui;
            float adjY = (H - 600f) / 2f, middleY = H / 600f * 250f;
            if (H < 700f) { adjY = (H - 508f) / 2f; middleY = H / 600f * 200f; }
            else if (H < 850f) middleY = H / 600f * 225f;
            Vector2 P(float x, float y) => new Vector2(view.x + x * ui, view.y + y * ui);

            var label = P(76f, 414f + adjY);
            DrawGuideSlot(P(73f, 331f + adjY), ui, mouse, ref hovered, ref hoveredKind);
            bool guideShown = Town.TownNpcs.GuideCraftOpen && !Town.TownNpcs.GuideItem.IsEmpty;
            if (recipes.Count == 0 && !Town.TownNpcs.GuideCraftOpen)
            {
                Outlined(new Rect(label.x, label.y, 400f * ui, 24f * ui), Lang.T("制作（徒手，或在所需的制作站旁）"), labelStyle, new Color(1f, 1f, 1f, 0.6f));
                return;
            }
            if (recipes.Count == 0) return;
            Outlined(new Rect(label.x, label.y, 200f * ui, 24f * ui), Lang.T("制作"), nameStyleLeft(ui), Color.white);
            // Say where the rest are made.
            if (!guideShown) Outlined(new Rect(label.x + 60f * ui, label.y + 2f * ui, 360f * ui, 22f * ui), Lang.T("（徒手，或在所需的制作站旁）"), labelStyle, new Color(1f, 1f, 1f, 0.55f));

            if (grid) DrawRecipeGrid(view, ui, mouse, ref hovered, ref hoveredExtra, ref hoveredKind);
            else
            {
                for (int i = 0; i < recipes.Count; i++)
                {
                    float Y = focusY + (i - focus) * RecipeStep;
                    if (Mathf.Abs(Y) > middleY) continue;
                    float s = Mathf.Max(0.75f, 100f / (Mathf.Abs(Y) + 100f));
                    float x = (int)(46f - 26f * s), y = (int)(410f + Y * s - 30f * s + adjY);
                    var p = P(x, y);
                    var rect = new Rect(p.x, p.y, 52f * s * ui, 52f * s * ui);
                    // GetItemSlotColors: fading towards the column's ends.
                    float a = 220f + 50f, itemAlpha = 255f;
                    if (Mathf.Abs(Y) > middleY - 100f)
                    {
                        float k = 100f - (Mathf.Abs(Y) - (middleY - 100f));
                        a = 150f * k / 100f;
                        itemAlpha = 255f * k / 100f;
                    }
                    a = Mathf.Max(0f, a - 50f);
                    bool focused = i == focus;
                    var prev = GUI.color;
                    GUI.color = focused ? Color.white : new Color(a / 255f, a / 255f, a / 255f, a / 255f / 0.85f);
                    GUI.DrawTexture(rect, TerrariaAssets.Texture(focused ? "Inventory_Back14" : "Inventory_Back4"));
                    GUI.color = new Color(1f, 1f, 1f, itemAlpha / 255f * prev.a);
                    DrawIconOnly(rect, ItemStack.Of(recipes[i].Result, recipes[i].Count));
                    GUI.color = prev;
                    Add(rect, Kind.Recipe, i);
                    if (rect.Contains(mouse))
                    {
                        hovered = ItemStack.Of(recipes[i].Result);
                        hoveredKind = Kind.Recipe;
                        hoveredExtra = RecipeExtra(recipes[i]);
                    }
                }
                // The focused recipe's ingredients beside it (or its price, for a basic item).
                if (!Shop.Free && focus < recipes.Count && Mathf.Abs(focusY) < 1f)
                {
                    var r = recipes[focus];
                    if (r.IsBought)
                    {
                        var p = P(80f, 380f + adjY);
                        Outlined(new Rect(p.x, p.y + 4f * ui, 300f * ui, 22f * ui), Shop.Money(r.Cost), labelStyle,
                            Shop.Purse >= r.Cost ? new Color(144 / 255f, 238 / 255f, 144 / 255f) : new Color(1f, 140 / 255f, 160 / 255f));
                    }
                    else
                    {
                        var near = World.Chests.ForCrafting(HeroController.SilentInstance);
                        for (int k = 0; k < r.Ingredients.Length; k++)
                        {
                            var (t, n) = r.Ingredients[k];
                            // Main.DrawInventory: inventoryScale 0.6, x = 80 + j*40, top-aligned with the big slot.
                            var p = P(80f + 40f * k, 380f + adjY);
                            var rect = new Rect(p.x, p.y, 52f * 0.6f * ui, 52f * 0.6f * ui);
                            GUI.DrawTexture(rect, TerrariaAssets.Texture("Inventory_Back4"));
                            DrawIconOnly(rect, ItemStack.Of(t));
                            int carried = Recipes.Count(inv, t), have = Recipes.Count(inv, t, near);
                            Outlined(new Rect(rect.x + 2f * ui, rect.yMax - 16f * ui, 60f * ui, 16f * ui), n.ToString(), labelStyle,
                                have >= n ? Color.white : new Color(1f, 140 / 255f, 160 / 255f));
                            panels.Add(rect);
                            if (rect.Contains(mouse))
                            {
                                hovered = ItemStack.Of(t);
                                hoveredKind = Kind.Recipe;
                                hoveredExtra = have > carried ? Lang.F("背包里有 {0}，附近的箱子里有 {1}", carried, have - carried) : Lang.F("背包里有 {0}", have);
                            }
                        }
                    }
                }
            }

            // The grid toggle (UI/Craft_Toggle_{2 normal, 0 grid open}, +1 under the mouse).
            var tc = P(94f, 450f + adjY);
            var toggle = TerrariaAssets.Texture("UI/Craft_Toggle_2");
            var tr = new Rect(tc.x - toggle.width * ui / 2f, tc.y - toggle.height * ui / 2f, toggle.width * ui, toggle.height * ui);
            bool over = tr.Contains(mouse);
            GUI.DrawTexture(tr, TerrariaAssets.Texture("UI/Craft_Toggle_" + ((grid ? 0 : 2) + (over ? 1 : 0))));
            Add(tr, Kind.GridToggle, 0);
            if (over) DrawLines(mouse, new List<string> { grid ? Lang.T("关闭制作窗口") : Lang.T("打开制作窗口") }, Color.white);
        }

        static string RecipeExtra(Recipe r)
        {
            if (Shop.Free) return Lang.T("点击拿取，按住连续拿取");
            if (r.IsBought) return r.Cost > 0 ? Lang.F("购买（不用材料）：{0}", Shop.Money(r.Cost)) : Lang.T("免费拿取（不用材料）");
            var sb = new System.Text.StringBuilder(Lang.T("需要："));
            for (int k = 0; k < r.Ingredients.Length; k++)
            {
                var (t, n) = r.Ingredients[k];
                if (k > 0) sb.Append("、");
                sb.Append($"{ItemDb.Get(t).Name} ×{n}");
            }
            if (r.Environment != CraftEnvironment.None) sb.Append("\n" + Lang.T(CraftEnvironments.Description(r.Environment)));
            if (!ByHand(r)) sb.Append("\n" + Lang.F("工作站：{0}（放在附近）", Lang.T(r.Station)));
            return sb.ToString();
        }

        /// <summary>The classic recipe grid (CraftingUI): 42 px apart at three-quarter size, paged by the wheel.</summary>
        static void DrawRecipeGrid(Rect view, float ui, Vector2 mouse, ref ItemStack hovered, ref string hoveredExtra, ref Kind hoveredKind)
        {
            float W = view.width / ui, H = view.height / ui;
            gridColumns = Mathf.Max(1, (int)((W - 310f - 280f) / 42f));
            int gridRows = Mathf.Max(1, (int)((H - 340f - 20f) / 42f));
            gridRow = Mathf.Clamp(gridRow, 0, Mathf.Max(0, (recipes.Count - 1) / gridColumns));
            var bg = new Rect(view.x + 300f * ui, view.y + 330f * ui, (gridColumns * 42f + 20f) * ui, (gridRows * 42f + 20f) * ui);
            var prev = GUI.color;
            GUI.color = new Color(0.1f, 0.12f, 0.25f, 0.6f);
            GUI.DrawTexture(bg, Texture2D.whiteTexture);
            GUI.color = prev;
            panels.Add(bg);
            for (int k = 0; k < gridColumns * gridRows; k++)
            {
                int i = gridRow * gridColumns + k;
                if (i >= recipes.Count) break;
                var rect = new Rect(view.x + (310f + (k % gridColumns) * 42f) * ui, view.y + (340f + (k / gridColumns) * 42f) * ui, 52f * 0.75f * ui, 52f * 0.75f * ui);
                GUI.DrawTexture(rect, TerrariaAssets.Texture(i == focus ? "Inventory_Back14" : "Inventory_Back4"));
                DrawIconOnly(rect, ItemStack.Of(recipes[i].Result, recipes[i].Count));
                Add(rect, Kind.GridRecipe, i);
                if (rect.Contains(mouse))
                {
                    hovered = ItemStack.Of(recipes[i].Result);
                    hoveredKind = Kind.GridRecipe;
                    hoveredExtra = Shop.Free ? Lang.T("点击选中") : RecipeExtra(recipes[i]);
                }
            }
        }

        static GUIStyle nameLeft;
        static GUIStyle nameStyleLeft(float ui)
        {
            if (nameLeft == null) nameLeft = new GUIStyle(nameStyle) { alignment = TextAnchor.UpperLeft };
            nameLeft.fontSize = Mathf.RoundToInt(20 * ui);
            return nameLeft;
        }

        /// <summary>Main.DrawDefenseCounter: the shield (Extra_58) with the defense on it.</summary>
        static void DrawDefense(Vector2 center, Inventory inv, float ui, Event e)
        {
            int defense = TerraMod.Instance.Stats.Defense;
            var tex = TerrariaAssets.Texture("Extra_58");
            float w = tex.width / 3f, h = tex.height / 2f;
            var rect = new Rect(center.x - w * 0.42f * ui, center.y - h * 0.42f * ui, w * 0.85f * ui, h * 0.85f * ui);
            GUI.DrawTextureWithTexCoords(rect, tex, new Rect(0f, 0.5f, 1f / 3f, 0.5f));
            Outlined(new Rect(rect.x, rect.y + rect.height * 0.25f, rect.width, 22f * ui), defense.ToString(), nameStyle, Color.white);
            if (!rect.Contains(UI.TerraCursor.GuiMouse())) return;
            var lines = new List<string> { Lang.F("{0} 防御力", defense), Lang.F("受到的伤害减少 {0}", defense / 2) };
            var set = Equipment.SetBonusText(Equipment.FullSet);
            if (set != null) lines.Add(Lang.T("套装奖励：") + Lang.T(set));
            DrawLines(UI.TerraCursor.GuiMouse(), lines, Color.white);
        }

        static void DrawSlot(Rect rect, ItemStack stack, string back, bool infinite)
        {
            var prev = GUI.color;
            GUI.color = new Color(prev.r, prev.g, prev.b, 0.85f * prev.a);
            GUI.DrawTexture(rect, TerrariaAssets.Texture(back));
            GUI.color = prev;
            if (!stack.IsEmpty) DrawIcon(rect, stack, infinite);
        }

        /// <summary>
        /// Equipment slots: Inventory_Back13 in the worn loadout's colour (whitened for half a second
        /// after a switch), the slot's silhouette (Extra_54) when empty, a highlight
        /// (Inventory_Back22) when the cursor's item would go in. An empty slot another loadout
        /// shares into shows that item faded with the sharing loadout's corner flag; a worn item
        /// being shared out has the worn loadout's flag.
        /// </summary>
        static void DrawEquipSlot(Rect rect, ItemStack stack, int silhouette, bool accepts, ItemStack effective, int sharedFrom)
        {
            var prev = GUI.color;
            var c = LoadoutSlotColors[inv.CurrentLoadout];
            const float k = 0.86f * 1.6f;
            var back = new Color(c.r / 255f * k, c.g / 255f * k, c.b / 255f * k, 0.86f);
            // ItemSlot.GetColorByLoadout: 12.5% whiter right after a switch, easing back over 30 ticks.
            float t = Mathf.Clamp01(1f - (Time.unscaledTime - lastLoadoutSwitch) * 2f) * 0.5f;
            GUI.color = Color.Lerp(back, Color.white, t * t * t);
            GUI.DrawTexture(rect, TerrariaAssets.Texture("Inventory_Back13"));
            if (accepts)
            {
                GUI.color = new Color(Mathf.Min(1f, c.r * 2f / 255f), Mathf.Min(1f, c.g * 2f / 255f), Mathf.Min(1f, c.b * 2f / 255f), 0.9f);
                GUI.DrawTexture(rect, TerrariaAssets.Texture("Inventory_Back22"));
            }
            GUI.color = prev;
            if (!stack.IsEmpty)
            {
                DrawIcon(rect, stack, false);
                if (stack.Favorite) DrawShareFlag(rect, inv.CurrentLoadout);
                return;
            }
            if (sharedFrom >= 0 && !effective.IsEmpty)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.6f);
                DrawIconOnly(rect, effective);
                GUI.color = prev;
                DrawShareFlag(rect, sharedFrom);
                return;
            }
            var tex = TerrariaAssets.Texture("Extra_54");
            float fw = tex.width / 3f, fh = tex.height / 7f;
            int col = silhouette % 3, row = silhouette / 3;
            var uv = new Rect((col * fw + 1f) / tex.width, 1f - (row * fh + fh - 1f) / tex.height, (fw - 2f) / tex.width, (fh - 2f) / tex.height);
            float ui = GameView.Scale, w = (fw - 2f) * 0.85f * ui, h = (fh - 2f) * 0.85f * ui;
            GUI.color = new Color(1f, 1f, 1f, 0.35f);
            GUI.DrawTextureWithTexCoords(new Rect(rect.center.x - w / 2f, rect.center.y - h / 2f, w, h), tex, uv);
            GUI.color = prev;
        }

        // --- Loadouts. ---

        /// <summary>ItemSlot.LoadoutSlotColors (functional column) for loadouts 1-3, and an amber fourth for the summoner's.</summary>
        static readonly Color32[] LoadoutSlotColors = { new Color32(50, 106, 64, 255), new Color32(35, 106, 126, 255), new Color32(122, 63, 83, 255), new Color32(126, 92, 38, 255) };
        static readonly string[] loadoutClass = { "近战", "远程", "魔法", "召唤" };
        static float lastLoadoutSwitch = -10f;
        static int loadoutHover = -1;
        static Texture2D loadoutButtons;
        static Texture2D[] shareFlags;

        /// <summary>The Alt-to-share hint has been acted on (it stops showing).</summary>
        public static bool ShareHintSeen;

        /// <summary>ItemSlot.RecordLoadoutChange: the equipment slots flash from now.</summary>
        public static void RecordLoadoutChange() => lastLoadoutSwitch = Time.unscaledTime;

        /// <summary>What hovering an equipment slot shows: the worn item or the one shared in, with the share lines.</summary>
        static void HoverEquip(ItemStack worn, ItemStack effective, int sharedFrom, Kind kind, ref ItemStack hovered, ref string extra, ref Kind hoveredKind)
        {
            hoveredKind = kind;
            if (worn.IsEmpty && sharedFrom >= 0)
            {
                hovered = effective;
                extra = Lang.F("共享自配装{0}", sharedFrom + 1);
                return;
            }
            hovered = worn;
            if (worn.IsEmpty) return;
            if (worn.Favorite) extra = Lang.T("与其他配装共享");
            else if (!ShareHintSeen) extra = Lang.T("按 Alt 在配装间共享");
        }

        /// <summary>
        /// The loadout buttons' art: Extra_259's 3×3 frames (idle grey, selected in colour, the
        /// hover outline; one row per loadout), with a fourth row made from the third: an "IV" in
        /// its numeral box and the figure in amber.
        /// </summary>
        static Texture2D LoadoutButtons()
        {
            if (loadoutButtons != null) return loadoutButtons;
            const int FW = 38, FH = 34;
            var src = TerrariaAssets.Pixels("Extra_259", out int w, out int h);
            int H = h + FH;
            var px = new Color32[w * H];
            // Unity keeps rows bottom-up: the source's three rows go on top, the new fourth row below them.
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) px[(y + FH) * w + x] = src[y * w + x];
            int SrcRow(int frameRow, int yInFrame) => h - (frameRow * FH + yInFrame) - 1;
            for (int yf = 0; yf < FH; yf++)
                for (int x = 0; x < w; x++) px[(FH - yf - 1) * w + x] = src[SrcRow(2, yf) * w + x];
            var D = new Color32(96, 96, 96, 255);
            var M = new Color32(147, 147, 147, 255);
            string[] iv = { "LDLDL", "LDLDL", "LDDLD", "MDDMD" };
            for (int col = 0; col < 2; col++)
            {
                var L = col == 0 ? new Color32(218, 218, 218, 255) : new Color32(231, 231, 231, 255);
                for (int r = 0; r < 4; r++)
                    for (int c = 0; c < 5; c++)
                    {
                        var ink = iv[r][c] == 'L' ? L : iv[r][c] == 'M' ? M : D;
                        for (int dy = 0; dy < 2; dy++)
                            for (int dx = 0; dx < 2; dx++)
                            {
                                int fx = col * FW + 22 + c * 2 + dx, fy = 4 + r * 2 + dy;
                                px[(FH - fy - 1) * w + fx] = ink;
                            }
                    }
            }
            // The selected frame's pink becomes amber.
            var from = new[] { new Color32(97, 30, 68, 255), new Color32(199, 92, 137, 255), new Color32(223, 167, 175, 255), new Color32(156, 53, 111, 255) };
            var to = new[] { new Color32(98, 52, 22, 255), new Color32(222, 164, 52, 255), new Color32(246, 226, 156, 255), new Color32(170, 106, 30, 255) };
            for (int y = 0; y < FH; y++)
                for (int x = FW; x < FW * 2; x++)
                {
                    ref var p = ref px[y * w + x];
                    for (int k = 0; k < from.Length; k++)
                        if (p.r == from[k].r && p.g == from[k].g && p.b == from[k].b && p.a > 0) { p = new Color32(to[k].r, to[k].g, to[k].b, p.a); break; }
                }
            return loadoutButtons = TerrariaAssets.Make("Extra_259#4", px, w, H);
        }

        static void DrawLoadoutButton(Rect btn, int index, bool selected, bool over, float ui)
        {
            var tex = LoadoutButtons();
            float fw = 38f / tex.width, fh = 34f / tex.height;
            var frame = new Rect(btn.x - 3f * ui, btn.y - 1f * ui, 38f * ui, 34f * ui);
            var prev = GUI.color;
            GUI.color = Color.white;
            GUI.DrawTextureWithTexCoords(frame, tex, new Rect((selected ? 1 : 0) * fw, 1f - (index + 1) * fh, fw, fh));
            if (over)
            {
                // The outline frame on top, in Main.OurFavoriteColor.
                GUI.color = new Color(1f, 231 / 255f, 69 / 255f, 1f);
                GUI.DrawTextureWithTexCoords(frame, tex, new Rect(2 * fw, 1f - (index + 1) * fh, fw, fh));
            }
            GUI.color = prev;
        }

        /// <summary>The corner flag of a sharing loadout (Extra_310-312; an amber one made for the fourth), at 70%.</summary>
        static void DrawShareFlag(Rect rect, int loadout)
        {
            if (shareFlags == null)
            {
                shareFlags = new Texture2D[Inventory.LoadoutCount];
                for (int i = 0; i < 3; i++) shareFlags[i] = TerrariaAssets.Texture("Extra_" + (310 + i));
                var px = TerrariaAssets.Pixels("Extra_312", out int w, out int h);
                var from = new[] { new Color32(92, 0, 73, 255), new Color32(255, 169, 152, 255), new Color32(61, 0, 67, 255), new Color32(255, 76, 94, 255) };
                var to = new[] { new Color32(110, 52, 0, 255), new Color32(255, 236, 160, 255), new Color32(70, 30, 0, 255), new Color32(255, 190, 60, 255) };
                for (int i = 0; i < px.Length; i++)
                    for (int k = 0; k < from.Length; k++)
                        if (px[i].a > 0 && px[i].r == from[k].r && px[i].g == from[k].g && px[i].b == from[k].b) { px[i] = new Color32(to[k].r, to[k].g, to[k].b, px[i].a); break; }
                shareFlags[3] = TerrariaAssets.Make("Extra_312#4", px, w, h);
            }
            var prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.7f);
            GUI.DrawTexture(rect, shareFlags[Mathf.Clamp(loadout, 0, shareFlags.Length - 1)]);
            GUI.color = prev;
        }

        static void DrawIcon(Rect rect, ItemStack stack, bool infinite)
        {
            float ui = GameView.Scale;
            DrawIconOnly(rect, stack);
            // ItemSlot.Draw: the number only for more than one (testing shows ∞ on anything that stacks).
            if (infinite ? stack.Def.MaxStack > 1 : stack.Count > 1)
                Outlined(new Rect(rect.x + 6f * ui, rect.yMax - 20f * ui, rect.width, 18f * ui), infinite ? "∞" : stack.Count.ToString(), labelStyle, Color.white);
        }

        static void DrawIconOnly(Rect rect, ItemStack stack)
        {
            float ui = GameView.Scale;
            var icon = TerrariaAssets.Texture(stack.Def.Texture);
            // Animated item sheets (the Fallen Star, souls, food) stack their frames: the one of the moment.
            float frameH = icon.height / (float)ItemFrames.FramesOf(stack.Def.Texture);
            float fit = Mathf.Min(1f, 32f / Mathf.Max(icon.width, frameH)) * ui * (rect.width / (SlotSize * ui));
            float w = icon.width * fit, h = frameH * fit;
            GUI.DrawTextureWithTexCoords(new Rect(rect.center.x - w / 2f, rect.center.y - h / 2f, w, h), icon, ItemFrames.CoordsNow(stack.Def.Texture));
        }

        static readonly string[] className = { "", "近战", "远程", "魔法", "召唤" };

        static void DrawTooltip(Vector2 mouse, ItemDef def, string extra, bool favorite)
        {
            var lines = new List<string> { def.Name };
            if (favorite)
            {
                lines.Add(Lang.T("已标记为收藏"));
                lines.Add(Lang.T("快速丢弃、堆叠和出售将被阻止"));
            }
            if (def.IsWeapon)
            {
                lines.Add(Lang.F("{0} " + className[(int)def.Class] + "伤害", Equipment.Damage(def)));
                // Summon items never crit (no crit line), and among them only whips show their speed.
                if (def.Class != DamageClass.Summon) lines.Add(Lang.F("{0}% 暴击率", Mathf.RoundToInt(Equipment.Crit(def))));
                if (def.Class != DamageClass.Summon || def.IsWhip)
                    lines.Add(Lang.F("{0} 使用时间", Mathf.RoundToInt(def.UseAnimation * 60f * (def.IsWhip ? Equipment.WhipScale() : 1f))));
            }
            if (def.IsAmmo && def.Damage > 0) lines.Add(Lang.F("{0} 远程伤害（叠加到武器上）", def.Damage));
            if (def.PickPower > 0) lines.Add(Lang.F("{0}% 镐力", def.PickPower));
            if (def.AxePower > 0) lines.Add(Lang.F("{0}% 斧力", def.AxePower * 5));
            if (def.HammerPower > 0) lines.Add(Lang.F("{0}% 锤力", def.HammerPower));
            if (def.FishingPower > 0) lines.Add(Lang.F("{0}% 渔力", def.FishingPower));
            if (def.BaitPower > 0) lines.Add(Lang.F("{0}% 鱼饵力", def.BaitPower));
            if (def.QuestFish) lines.Add(Lang.T("任务物品"));
            if (def.Mana > 0) lines.Add(Lang.F("消耗 {0} 魔力", Equipment.ManaCost(def)));
            if (def.UseAmmo != AmmoKind.None)
                lines.Add(Lang.T(def.UseAmmo switch
                {
                    AmmoKind.Arrow => "使用箭作为弹药", AmmoKind.FallenStar => "使用坠落之星作为弹药", AmmoKind.Rocket => "使用火箭作为弹药", AmmoKind.Dart => "使用飞镖作为弹药",
                    AmmoKind.Sand => "使用沙块作为弹药", AmmoKind.Gel => "使用凝胶作为弹药", AmmoKind.Flare => "使用照明弹作为弹药", AmmoKind.Snowball => "使用雪球作为弹药",
                    AmmoKind.StyngerBolt => "使用毒刺矢作为弹药", AmmoKind.CandyCorn => "使用玉米糖作为弹药", AmmoKind.JackOLantern => "使用爆炸杰克南瓜灯作为弹药",
                    AmmoKind.Stake => "使用尖桩作为弹药", AmmoKind.Nail => "使用钉子作为弹药", AmmoKind.Ale => "使用麦芽酒作为弹药", AmmoKind.Acorn => "使用橡实作为弹药",
                    _ => "使用子弹作为弹药",
                }));
            if (def.Defense > 0) lines.Add(Lang.F("{0} 防御", def.Defense));
            if (def.IsArmor) lines.Add(Lang.T(def.Armor == ArmorSlot.Head ? "头部装备" : def.Armor == ArmorSlot.Body ? "身体装备" : "腿部装备"));
            if (def.IsAccessory) lines.Add(Lang.T("可装备"));
            if (def.Places != World.CellKind.Empty || def.PlacesStation != World.StationKind.None || def.PlacesChest) lines.Add(Lang.T("可放置"));
            if (def.Consumable && !def.IsAmmo) lines.Add(Lang.T("消耗品"));
            // Item.material (Main.MouseText_DrawItemTooltip, tip[36]): whatever goes into a recipe, from the recipes themselves.
            if (Recipes.IsMaterial(def.Type)) lines.Add(Lang.T("材料"));
            if (!string.IsNullOrEmpty(def.Tooltip))
                foreach (var l in def.Tooltip.Split('\n'))
                    if (l != "材料" && l != Lang.T("材料")) lines.Add(l);
            // Main.MouseText_DrawItemTooltip's prefix lines: green what the prefix adds, red what it takes.
            Dictionary<int, Color> lineColors = null;
            if (def.Prefix > 0)
            {
                lineColors = new Dictionary<int, Color>();
                foreach (var (text, good) in Prefixes.TooltipLines(def.Unforged ?? def))
                {
                    lineColors[lines.Count] = good ? new Color(120 / 255f, 190 / 255f, 120 / 255f) : new Color(190 / 255f, 120 / 255f, 120 / 255f);
                    lines.Add(text);
                }
            }
            // Its forging: the level and what it adds, in the forge's gold.
            if (def.Forge > 0)
            {
                if (lineColors == null) lineColors = new Dictionary<int, Color>();
                lineColors[lines.Count] = new Color(1f, 0.82f, 0.35f);
                lines.Add(Lang.F("锻造 +{0}：伤害 +{1}%", def.Forge, Mathf.RoundToInt(def.Forge * Forging.DamagePerLevel * 100f)));
            }
            if (def.IsArmor && def.Set != ArmorSet.None && Equipment.FullSet != ArmorSet.None
                && (Equipment.FamilyOf(Equipment.FullSet) == def.Set || (def.Set == ArmorSet.Robe && (Equipment.FullSet == ArmorSet.Wizard || Equipment.FullSet == ArmorSet.MagicHat))))
                lines.Add(Lang.T("套装奖励：") + Lang.T(Equipment.SetBonusText(Equipment.FullSet)));
            if (extra != null) lines.AddRange(extra.Split('\n'));
            DrawLines(mouse, lines, ItemDb.RarityColor(def.Rare), lineColors);
        }

        static void DrawLines(Vector2 mouse, List<string> lines, Color first, Dictionary<int, Color> colors = null)
        {
            float ui = GameView.Scale;
            // Right of the cursor and level with its tip, clear of the arrow.
            float y = mouse.y - 8f * ui;
            // Keep the tooltip on screen near the right and bottom edges.
            float width = 0f;
            foreach (var l in lines) width = Mathf.Max(width, tipStyle.CalcSize(new GUIContent(l)).x);
            float x = mouse.x + 26f * ui;
            if (x + width > Screen.width - 10f) x = Mathf.Max(10f, mouse.x - width - 10f * ui);
            if (y + lines.Count * 22f * ui > Screen.height - 10f) y = Mathf.Max(10f, Screen.height - 10f - lines.Count * 22f * ui);
            y = Mathf.Max(4f, y);
            for (int i = 0; i < lines.Count; i++)
            {
                Outlined(new Rect(x, y, width + 20f * ui, 22f * ui), lines[i], tipStyle, i == 0 ? first : colors != null && colors.TryGetValue(i, out var lc) ? lc : new Color(0.85f, 0.85f, 0.85f));
                y += 22f * ui;
            }
        }

        static void Outlined(Rect r, string text, GUIStyle style, Color color)
        {
            if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(text)) return;
            float ui = GameView.Scale;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.85f * color.a);
            for (int ox = -1; ox <= 1; ox++)
            for (int oy = -1; oy <= 1; oy++)
                if (ox != 0 || oy != 0) GUI.Label(new Rect(r.x + ox * 1.5f * ui, r.y + oy * 1.5f * ui, r.width, r.height), text, style);
            style.normal.textColor = color;
            GUI.Label(r, text, style);
        }

        static void Sound(string name) => TerraMod.Instance?.PlaySound(name, 0.7f);

        static void EnsureStyles(float ui)
        {
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft };
                nameStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                tipStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft, wordWrap = false };
                buttonStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight, wordWrap = false };
            }
            labelStyle.fontSize = Mathf.RoundToInt(15 * ui);
            nameStyle.fontSize = Mathf.RoundToInt(20 * ui);
            tipStyle.fontSize = Mathf.RoundToInt(18 * ui);
        }

        /// <summary>Dev: takes one of an item from the crafting column onto the cursor, as a click on it would.</summary>
        public static bool DevCraft(ItemType type)
        {
            RefreshRecipes();
            int i = recipes.FindIndex(r => r.Result == type);
            if (i < 0) return false;
            focus = i;
            focusY = 0f;
            stackSplit = 0;
            preventCraft = false;
            craftingNow = -1;
            int before = inv.Mouse.Type == type ? inv.Mouse.Count : 0;
            ClickRecipe(i, true, true);
            return inv.Mouse.Type == type && inv.Mouse.Count > before;
        }

        /// <summary>Dev: the crafting column's recipes and the focused one.</summary>
        public static int RecipeCount => recipes.Count;
        public static int Focus => focus;
        public static void DevFocus(int i)
        {
            RefreshRecipes();
            focus = Mathf.Clamp(i, 0, Mathf.Max(0, recipes.Count - 1));
            focusY = 0f;
        }

        /// <summary>Dev: the crafting column scrolled by recipes, sliding as the wheel makes it.</summary>
        public static void DevScrollRecipes(int delta) => Scroll(delta);
    }
}
