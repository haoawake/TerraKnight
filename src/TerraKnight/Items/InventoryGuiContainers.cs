using TerraKnight.Terraria;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The Banner Case's and the Potion Box's panel (Containers, Chests.ToggleContainer): the chest panel with its pages
    /// ("&lt; 1/4 &gt;" under its buttons, the chest buttons 7 and 8), a favourite's slot drawn as the inventory draws one
    /// (Inventory_Back10), what the container doesn't take refused, Alt+click favouriting a Potion Box's buff potion of 20
    /// or more (ToggleFavorited), and stacking onto a favourite keeping it one.
    /// </summary>
    internal static partial class InventoryGui
    {
        const int PrevPage = 7, NextPage = 8;

        /// <summary>The page arrows and number under the panel's buttons (the Rename button's place, which a storage hasn't).</summary>
        static void DrawContainerPager(Rect view, float ui, float y0, Vector2 mouse)
        {
            if (!Chests.ContainerOpen) return;
            var style = nameStyleLeft(ui);
            int size = style.fontSize;
            float y = y0 + (30f + 5 * 28f) * ui;
            for (int k = 0; k < 2; k++)
            {
                var r = new Rect(view.x + (506f + k * 84f) * ui, y, 24f * ui, 24f * ui);
                Add(r, Kind.ChestButton, k == 0 ? PrevPage : NextPage);
                bool hover = r.Contains(mouse);
                style.fontSize = Mathf.RoundToInt((hover ? 20 : 17) * ui);
                Outlined(r, k == 0 ? "<" : ">", style, hover ? new Color(1f, 0.85f, 0.3f) : Color.white);
            }
            style.fontSize = Mathf.RoundToInt(17 * ui);
            Outlined(new Rect(view.x + 534f * ui, y, 56f * ui, 24f * ui), $"{Chests.ContainerPage + 1}/{Chests.ContainerPages}", style, Color.white);
            if (Chests.SellListOpen)
            {
                // How the list works, where the item buttons would be.
                style.fontSize = Mathf.RoundToInt(15 * ui);
                Outlined(new Rect(view.x + 506f * ui, y0 + 30f * ui, 300f * ui, 22f * ui), Lang.T("放进物品：加入名单并卖掉"), style, new Color(0.85f, 0.85f, 0.85f));
                Outlined(new Rect(view.x + 506f * ui, y0 + 52f * ui, 300f * ui, 22f * ui), Lang.T("点名单里的物品：移出名单"), style, new Color(0.85f, 0.85f, 0.85f));
            }
            style.fontSize = size;
        }

        /// <summary>The Auto-Sell Bag's list keeps no items: the buttons that move items (Loot All to Restock) aren't there.</summary>
        static bool HideChestButton(int i) => Chests.SellListOpen && i <= 3;

        /// <summary>A list entry is an item's picture alone, faded (no stack is kept).</summary>
        static void DrawChestIcon(Rect rect, ItemStack st)
        {
            if (!Chests.SellListOpen)
            {
                DrawIcon(rect, st, false);
                return;
            }
            var prev = GUI.color;
            GUI.color = new Color(prev.r, prev.g, prev.b, prev.a * 0.55f);
            DrawIconOnly(rect, st);
            GUI.color = prev;
        }

        /// <summary>The page arrows' press (the list's item buttons none); false for the panel's own buttons.</summary>
        static bool ActContainerButton(int index)
        {
            if (HideChestButton(index)) return true;
            if (index != PrevPage && index != NextPage) return false;
            Chests.TurnPage(index == PrevPage ? -1 : 1);
            Sound("Menu_Tick");
            return true;
        }

        /// <summary>A slot's back in the panel: a favourite's in a container (ItemSlot.Draw's InventoryBack10), else the chest's.</summary>
        static string ChestSlotBack(ItemStack st) => Chests.SellListOpen ? "Inventory_Back6"
            : Chests.ContainerOpen && !st.IsEmpty && st.Favorite ? "Inventory_Back10" : "Inventory_Back5";

        public static int DevRefused, DevFavorited, DevUnlisted;

        /// <summary>
        /// A click on a container's slot, before the chest's own: true when it was handled here. The Auto-Sell Bag's list
        /// takes every click: a stack on the cursor listed and sold, an entry clicked off the list.
        /// </summary>
        static bool ContainerAct(int index, ref ItemStack slot, bool leftPress)
        {
            if (!Chests.SellListOpen) return leftPress && ContainerClick(ref slot);
            if (!leftPress) return true;
            if (!inv.Mouse.IsEmpty)
            {
                var hero = HeroController.SilentInstance;
                Vector2 at = hero != null ? (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center : Vector2.zero;
                if (!Containers.ListAndSell(inv, ref inv.Mouse, Chests.OpenItems, index, at, out var why))
                {
                    DevRefused++;
                    if (why != null) Notice(Lang.T(why));
                    Sound("Menu_Tick");
                }
                return true;
            }
            if (slot.IsEmpty) return true;
            slot = default;
            DevUnlisted++;
            Notice(Lang.T("已移出自动出售名单"));
            Sound("Grab");
            return true;
        }

        /// <summary>A left click on a Banner Case's or a Potion Box's slot, before the chest's own: true when it was handled here.</summary>
        static bool ContainerClick(ref ItemStack slot)
        {
            if (!Chests.ContainerOpen) return false;
            int kind = Chests.OpenBankKind;
            if (Alt && !slot.IsEmpty)
            {
                if (Containers.CanFavorite(kind, slot))
                {
                    slot.Favorite = !slot.Favorite;
                    DevFavorited++;
                    Sound("Menu_Tick");
                }
                else Notice(Lang.T(kind == Containers.BoxKind ? "增益药水攒到 20 瓶才能收藏" : "旗帜带在身上就生效，不用收藏"));
                return true;
            }
            if (!inv.Mouse.IsEmpty && Chests.OpenRefuses(inv.Mouse))
            {
                DevRefused++;
                Notice(Lang.T(kind == Containers.CaseKind ? "旗帜盒只能放旗帜" : "药水箱只能放药水"));
                Sound("Menu_Tick");
                return true;
            }
            if (slot.Favorite && CanStack(inv.Mouse, slot) && slot.Count < slot.Def.MaxStack)
            {
                int moved = Mathf.Min(inv.Mouse.Count, slot.Def.MaxStack - slot.Count);
                slot.Count += moved;
                inv.Mouse.Count -= moved;
                if (inv.Mouse.Count <= 0) inv.Mouse = default;
                Sound("Grab");
                return true;
            }
            return false;
        }
    }
}
