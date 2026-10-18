using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The Hallownest bag: Hallownest's key items' own slots (<see cref="HkItems"/>), opened by its button under the monster
    /// book's. Its items move only between it and the hotbar (a click to pick up and put down, Shift+click across); the
    /// item in hand is where they work.
    /// </summary>
    internal static partial class InventoryGui
    {
        static bool hkOpen;
        public static bool HkBagOpen => hkOpen;
        const int HkColumns = 5;

        static Rect HkButtonRect(Rect view, float ui) =>
            UtilityButtonRect(view, ui, 2);

        static void DrawHkButton(Rect view, float ui, Vector2 mouse)
        {
            var r = HkButtonRect(view, ui);
            bool over = r.Contains(mouse);
            Outlined(r, Lang.T("圣巢背包"), labelStyle, over || hkOpen ? new Color(1f, 0.85f, 0.3f) : Color.white);
            Add(r, Kind.HkButton, 0);
        }

        static void ToggleHkBag()
        {
            hkOpen = !hkOpen;
            if (hkOpen) blacklistOpen = sellMode = false;
            if (hkOpen) bestiaryOpen = false;
            Sound(hkOpen ? "Menu_Open" : "Menu_Close");
        }

        static void DrawHkBag(Rect view, float ui, Vector2 mouse, ref ItemStack hovered, ref string hoveredExtra, ref Kind hoveredKind)
        {
            float slot = SlotSize * ui, step = SlotStep * ui;
            var b = HkButtonRect(view, ui);
            float x0 = b.x, y0 = b.yMax + 8f * ui;
            int rows = (HkItems.BagSlots + HkColumns - 1) / HkColumns;
            var panel = new Rect(x0 - 4f * ui, y0 - 4f * ui, HkColumns * step + 8f * ui, rows * step + 8f * ui);
            panels.Add(panel);
            for (int i = 0; i < HkItems.BagSlots; i++)
            {
                var rect = new Rect(x0 + (i % HkColumns) * step, y0 + (i / HkColumns) * step, slot, slot);
                Add(rect, Kind.HkBag, i);
                DrawSlot(rect, inv.HkBag[i], "Inventory_Back5", false);
                if (rect.Contains(mouse))
                {
                    hovered = inv.HkBag[i];
                    hoveredKind = Kind.HkBag;
                    if (hovered.IsEmpty) DrawLines(mouse, new System.Collections.Generic.List<string> { HkItems.BagSlotHint(i) }, Color.white);
                }
            }
        }

        static void NoticeHk(string zh)
        {
            notice = Lang.T(zh);
            noticeUntil = Time.unscaledTime + 2f;
            Sound("Menu_Tick");
        }

        /// <summary>A click on a bag slot: pick up, put down, stack or swap, Hallownest's items only; Shift+click to the hotbar.</summary>
        static void ClickHkBag(int i)
        {
            ref var slot = ref inv.HkBag[i];
            if (Shift && !slot.IsEmpty)
            {
                for (int k = 0; k < Inventory.HotbarSize && !slot.IsEmpty; k++)
                {
                    ref var h = ref inv.Slots[k];
                    if (h.IsEmpty) { h = slot; slot = default; }
                    else if (h.Type == slot.Type && h.Count < h.Def.MaxStack)
                    {
                        int m = Mathf.Min(slot.Count, h.Def.MaxStack - h.Count);
                        h.Count += m;
                        slot.Count -= m;
                        if (slot.Count <= 0) slot = default;
                    }
                }
                Sound("Grab");
                return;
            }
            if (!HkItems.BagAccepts(i, inv.Mouse))
            {
                NoticeHk(HkItems.BagSlotHint(i));
                return;
            }
            if (inv.Mouse.IsEmpty && slot.IsEmpty) return;
            if (CanStack(inv.Mouse, slot) && slot.Count < slot.Def.MaxStack)
            {
                int moved = Mathf.Min(inv.Mouse.Count, slot.Def.MaxStack - slot.Count);
                slot.Count += moved;
                inv.Mouse.Count -= moved;
                if (inv.Mouse.Count <= 0) inv.Mouse = default;
            }
            else (inv.Mouse, slot) = (slot, inv.Mouse);
            Sound("Grab");
        }

        /// <summary>A Hallownest item on the cursor put down outside the hotbar and the bag: refused. True when refused.</summary>
        static bool RefuseHkOutside()
        {
            if (!HkItems.Is(inv.Mouse)) return false;
            NoticeHk("圣巢的物品只能放在快捷栏和圣巢背包里");
            return true;
        }

        /// <summary>Shift+click on a Hallownest item in the hotbar: into the bag. True when it was one.</summary>
        static bool ShiftHkToBag(ref ItemStack slot)
        {
            if (!slot.IsEmpty && HkItems.AbilityBagSlot(slot.Type) >= 0)
            {
                if (HkItems.TryStoreAbility(inv, slot)) slot = default;
                Sound("Grab");
                return true;
            }
            if (slot.Type == ItemType.DreamNail && !slot.IsEmpty)
            {
                if (DreamNailItem.TryStoreInBag(inv, slot)) slot = default;
                Sound("Grab");
                return true;
            }
            if (!HkItems.Is(slot)) return false;
            for (int k = 0; k < HkItems.KeyBagSlots && !slot.IsEmpty; k++)
            {
                ref var b = ref inv.HkBag[k];
                if (b.IsEmpty) { b = slot; slot = default; }
                else if (b.Type == slot.Type && b.Count < b.Def.MaxStack)
                {
                    int m = Mathf.Min(slot.Count, b.Def.MaxStack - b.Count);
                    b.Count += m;
                    slot.Count -= m;
                    if (slot.Count <= 0) slot = default;
                }
            }
            Sound("Grab");
            return true;
        }

        /// <summary>Dev: a click on a bag slot.</summary>
        public static void DevClickHkBag(int i, bool shift = false)
        {
            DevShift = shift;
            try { ClickHkBag(i); } finally { DevShift = false; }
        }

        /// <summary>Dev: a left click on an inventory slot.</summary>
        public static void DevLeftClick(int i, bool shift = false)
        {
            DevShift = shift;
            try { LeftClickSlot(i); } finally { DevShift = false; }
        }
    }
}

namespace TerraKnight.Items
{
    internal static partial class InventoryGui
    {
        /// <summary>Dev: the Hallownest bag open or shut.</summary>
        public static void DevOpenHkBag(bool open)
        {
            hkOpen = open;
            if (open) bestiaryOpen = false;
        }
    }
}
