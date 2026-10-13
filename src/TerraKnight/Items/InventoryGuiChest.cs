using System.Collections.Generic;
using TerraKnight.Terraria;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// An open chest's panel (Main.DrawInventory's chest, context 3; ChestUI.DrawButtons): its 40
    /// slots at 0.755 scale under the inventory, "箱子" beside them with the buttons Loot All,
    /// Deposit All and Quick Stack. Its slots take clicks as the inventory's do (a left click
    /// swaps with the cursor, a right click held takes one at a time, Shift sends the stack
    /// across); Shift on an inventory slot sends that stack into the chest.
    /// </summary>
    internal static partial class InventoryGui
    {
        static readonly string[] chestButtons = { "全部拿取", "全部存放", "快速堆叠" };
        static int chestButtonHover = -1;

        static void DrawChestPanel(Rect view, float ui, Vector2 mouse, ref ItemStack hovered, ref string hoveredExtra, ref Kind hoveredKind)
        {
            var items = Chests.OpenItems;
            if (items == null) return;
            float s = 52f * 0.755f * ui, step = 56f * 0.755f * ui;
            float x0 = view.x + 73f * ui, y0 = view.y + (258f + 18f) * ui;
            Outlined(new Rect(view.x + 504f * ui, y0 - 6f * ui, 300f * ui, 30f * ui), Lang.T("箱子"), nameStyleLeft(ui), Color.white);
            panels.Add(new Rect(x0, y0, 560f * 0.755f * ui, 224f * 0.755f * ui));
            var prev = GUI.color;
            for (int i = 0; i < Chests.Capacity; i++)
            {
                int col = i % 10, row = i / 10;
                var rect = new Rect(x0 + col * step, y0 + row * step, s, s);
                Add(rect, Kind.ChestSlot, i);
                GUI.color = new Color(220 / 255f, 220 / 255f, 220 / 255f, 220 / 255f);
                GUI.DrawTexture(rect, TerrariaAssets.Texture("Inventory_Back5"));
                GUI.color = prev;
                var st = items[i];
                if (st.IsEmpty) continue;
                DrawIcon(rect, st, false);
                if (!rect.Contains(mouse)) continue;
                hovered = st;
                hoveredKind = Kind.ChestSlot;
            }
            // The buttons, one under another beside the slots; the hovered one bigger and gold.
            int over = -1;
            for (int i = 0; i < chestButtons.Length; i++)
            {
                var r = new Rect(view.x + 506f * ui, y0 + (30f + i * 28f) * ui, 120f * ui, 24f * ui);
                Add(r, Kind.ChestButton, i);
                bool hover = r.Contains(mouse);
                if (hover) over = i;
                var style = nameStyleLeft(ui);
                int size = style.fontSize;
                style.fontSize = Mathf.RoundToInt((hover ? 20 : 17) * ui);
                Outlined(r, Lang.T(chestButtons[i]), style, hover ? new Color(1f, 0.85f, 0.3f) : Color.white);
                style.fontSize = size;
            }
            if (over != chestButtonHover && over >= 0) Sound("Menu_Tick");
            chestButtonHover = over;
        }

        static void ActChest(int index, bool leftPress, bool rightPress, bool rightHeld)
        {
            var items = Chests.OpenItems;
            if (items == null) return;
            ref var slot = ref items[index];
            if (leftPress)
            {
                if (Shift && !slot.IsEmpty)
                {
                    // ItemSlot.ShiftClick in a chest: the stack goes into the inventory, as much as fits.
                    int left = inv.Pickup(slot.Type, slot.Count);
                    if (left != slot.Count) Sound("Grab");
                    if (left <= 0) slot = default;
                    else slot.Count = left;
                    return;
                }
                if (inv.Mouse.IsEmpty && slot.IsEmpty) return;
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
                if (inv.Mouse.IsEmpty) inv.Mouse = ItemStack.Of(slot.Type, 0);
                inv.Mouse.Count++;
                slot.Count--;
                if (slot.Count <= 0) slot = default;
                Sound("Menu_Tick");
                RefreshStackSplitCooldown();
            }
        }

        static void ActChestButton(int index)
        {
            if (index == 0) Chests.LootAll(inv);
            else if (index == 1) Chests.DepositAll(inv);
            else Chests.QuickStack(inv);
            Sound("Grab");
        }

        /// <summary>Dev: a left click (with the Dev modifiers) on a chest slot, and a press of one of its buttons.</summary>
        public static void DevChestClick(int slot) => ActChest(slot, true, false, false);
        public static void DevChestButton(int index) => ActChestButton(index);
    }
}
