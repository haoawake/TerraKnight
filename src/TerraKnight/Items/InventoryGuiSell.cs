using System.Collections.Generic;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Quick selling (not Terraria's): the "快捷出售" button beside the Hallownest bag's turns it on; a left click on an
    /// item in the bag (or the cursor's item on the sale row) then sells the whole stack at once for what a shop pays
    /// (Shop.SellValue), into the sale row under the inventory where a chest's slots go. The row keeps the last
    /// <see cref="SoldSlots"/> sales, the newest first; a click on one buys it back whole for exactly what it fetched.
    /// Favourites, the Hallownest items and other permanent things and geo aren't sold. Closing the inventory, opening a
    /// chest or a shop, or the button again ends it; the row lasts until the save is left.
    /// </summary>
    internal static partial class InventoryGui
    {
        public const int SoldSlots = 20;
        static bool sellMode;
        static readonly List<(ItemStack Item, long Price)> sold = new List<(ItemStack, long)>();

        public static bool SellMode => sellMode;
        /// <summary>Dev: the sale row, newest first.</summary>
        public static IReadOnlyList<(ItemStack Item, long Price)> DevSold => sold;

        /// <summary>A new save or the title screen: the sales forgotten.</summary>
        public static void ClearSold()
        {
            sold.Clear();
            sellMode = false;
        }

        static Rect SellButtonRect(Rect view, float ui)
        {
            var b = HkButtonRect(view, ui);
            return new Rect(b.xMax + 12f * ui, b.y, b.width, b.height);
        }

        static void DrawSellButton(Rect view, float ui, Vector2 mouse)
        {
            var r = SellButtonRect(view, ui);
            bool over = r.Contains(mouse);
            Outlined(r, Lang.T("快捷出售"), labelStyle, over || sellMode ? new Color(1f, 0.85f, 0.3f) : Color.white);
            Add(r, Kind.SellButton, 0);
            if (over) DrawLines(mouse, new List<string> { Lang.T(sellMode ? "点击退出出售模式" : "点击进入出售模式：点背包里的物品直接卖出，卖错了可以在出售栏里按原价买回") }, Color.white);
        }

        static void ToggleSellMode()
        {
            sellMode = !sellMode;
            if (sellMode) statsOpen = blacklistOpen = hkOpen = bestiaryOpen = false;
            if (sellMode)
            {
                if (World.Chests.IsOpen) World.Chests.Close(false);
                if (Town.TownNpcs.ShopOpen) Town.TownNpcs.Close(false);
            }
            Sound(sellMode ? "Menu_Open" : "Menu_Close");
        }

        /// <summary>Whether a stack may be sold here; why not otherwise.</summary>
        static bool Sellable(ItemStack s, out string why)
        {
            why = null;
            if (s.IsEmpty) return false;
            if (s.Favorite) why = "收藏的物品不会卖出";
            else if (s.Def.CannotDiscard || s.Def.HkItem || Currencies.Is(s.Type)) why = "这件不能卖";
            return why == null;
        }

        /// <summary>Sells a stack onto the sale row (the newest first; past the row's end the oldest is gone for good).</summary>
        static bool SellStack(ref ItemStack s)
        {
            if (!Sellable(s, out var why))
            {
                if (why != null) Notice(Lang.T(why));
                return false;
            }
            long money = Shop.SellValue(s);
            Shop.Earn(money);
            var item = s;
            item.Favorite = false;
            sold.Insert(0, (item, money));
            if (sold.Count > SoldSlots) sold.RemoveAt(sold.Count - 1);
            s = default;
            TerraMod.Instance?.PlaySound(money > 0 ? "Coins" : "Grab", 0.7f);
            return true;
        }

        /// <summary>A left click on a bag slot in sell mode: its stack sold (true: the click is spent).</summary>
        static bool SellFromSlot(ref ItemStack slot)
        {
            if (!sellMode || slot.IsEmpty || !inv.Mouse.IsEmpty) return false;
            SellStack(ref slot);
            return true;
        }

        /// <summary>A click on the sale row: the cursor's item sold, or the sale bought back for what it fetched.</summary>
        static void ActSold(int index, bool leftPress)
        {
            if (!leftPress) return;
            if (!inv.Mouse.IsEmpty)
            {
                SellStack(ref inv.Mouse);
                return;
            }
            if (index < 0 || index >= sold.Count) return;
            var (item, price) = sold[index];
            if (!Shop.Spend(price))
            {
                Notice(Lang.T("钱不够"));
                return;
            }
            inv.Mouse = item;
            sold.RemoveAt(index);
            TerraMod.Instance?.PlaySound(price > 0 ? "Coins" : "Grab", 0.7f);
        }

        /// <summary>The sale row where a chest's slots go (DrawChestPanel's place and size), its name and how it works beside it.</summary>
        static void DrawSellRow(Rect view, float ui, Vector2 mouse, ref ItemStack hovered, ref string hoveredExtra, ref Kind hoveredKind)
        {
            float s = 52f * 0.755f * ui, step = 56f * 0.755f * ui;
            float x0 = view.x + 73f * ui, y0 = view.y + (258f + 18f + SlotStep) * ui;   // a slot lower than Terraria's: the trash slot stays clear
            Outlined(new Rect(view.x + 504f * ui, y0 - 6f * ui, 300f * ui, 30f * ui), Lang.T("出售栏"), nameStyleLeft(ui), Color.white);
            var style = nameStyleLeft(ui);
            int size = style.fontSize;
            style.fontSize = Mathf.RoundToInt(15 * ui);
            Outlined(new Rect(view.x + 506f * ui, y0 + 28f * ui, 300f * ui, 22f * ui), Lang.T("点背包里的物品卖出"), style, new Color(0.85f, 0.85f, 0.85f));
            Outlined(new Rect(view.x + 506f * ui, y0 + 50f * ui, 300f * ui, 22f * ui), Lang.T("点这里的物品按原价买回"), style, new Color(0.85f, 0.85f, 0.85f));
            style.fontSize = size;
            panels.Add(new Rect(x0, y0, 560f * 0.755f * ui, 2 * step));
            var prev = GUI.color;
            for (int i = 0; i < SoldSlots; i++)
            {
                var rect = new Rect(x0 + (i % 10) * step, y0 + (i / 10) * step, s, s);
                Add(rect, Kind.SellSlot, i);
                GUI.color = new Color(220 / 255f, 220 / 255f, 220 / 255f, 220 / 255f);
                GUI.DrawTexture(rect, TerrariaAssets.Texture("Inventory_Back6"));
                GUI.color = prev;
                if (i >= sold.Count) continue;
                var (item, price) = sold[i];
                DrawIcon(rect, item, false);
                if (!rect.Contains(mouse)) continue;
                hovered = item;
                hoveredKind = Kind.SellSlot;
                hoveredExtra = price > 0 ? Lang.F("买回价: {0}", Shop.Money(price)) : Lang.T("免费买回");
            }
        }

        /// <summary>Dev: sells a bag slot as a click in sell mode would; buys a sale back.</summary>
        public static bool DevSell(int slot)
        {
            bool was = sellMode;
            sellMode = true;
            bool done = SellFromSlot(ref inv.Slots[slot]);
            sellMode = was;
            return done;
        }

        public static void DevBuyBack(int index) => ActSold(index, true);
        public static void DevToggleSell() => ToggleSellMode();
    }
}
