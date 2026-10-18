using System.Collections;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "coinslot"): Terraria's coin column, its first slot. Geo picked up goes there first; Geo left in the bag by an
    /// older save is gathered into it; it takes money only; buying draws on it; the bag drawn with the coin column.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator CoinSlotTest()
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail = "")
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 钱币槽 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            var inv = mod.Inventory;
            bool wasInfinite = inv.Infinite;
            mod.SetInfiniteItems(false);
            yield return null;
            var saved = (ItemStack[])inv.Slots.Clone();
            int c = Inventory.CoinSlot;
            for (int i = Inventory.HotbarSize; i < Inventory.Total; i++) inv.Slots[i] = default;
            yield return new WaitForSeconds(0.3f);
            long before = GeoWallet.Held(inv);

            GeoWallet.Give(mod, 500);
            yield return new WaitForSeconds(0.3f);
            Check("捡到的吉欧进钱币槽", !inv.Slots[c].IsEmpty && inv.Slots[c].Type == ItemType.Geo && inv.Slots[c].Count >= 500,
                $"钱币槽 {inv.Slots[c].Type}×{inv.Slots[c].Count}，带着 {GeoWallet.Held(inv)}（之前 {before}）");

            // An older save's Geo, somewhere in the bag: gathered in.
            int inSlot = inv.Slots[c].Count;
            inv.Slots[20] = ItemStack.Of(ItemType.Geo, 123);
            inv.GatherCoins();
            Check("背包里的吉欧并进钱币槽", inv.Slots[20].IsEmpty && inv.Slots[c].Count == inSlot + 123, $"钱币槽 {inv.Slots[c].Count}");

            Check("只收钱", Inventory.FitsCoinSlot(ItemDb.Get(ItemType.Geo)) && !Inventory.FitsCoinSlot(ItemDb.Get(ItemType.IronBar))
                && !Inventory.IsAmmoSlot(c) && Inventory.IsAmmoSlot(Inventory.Size) && Inventory.IsAmmoSlot(Inventory.AmmoEnd - 1));
            // Other things never land there.
            inv.Pickup(ItemType.IronBar, 5);
            inv.Pickup(ItemType.WoodenArrow, 50);
            Check("别的东西不进钱币槽", inv.Slots[c].Type == ItemType.Geo && inv.Slots[Inventory.Size].Type == ItemType.WoodenArrow, $"弹药1 {inv.Slots[Inventory.Size].Type}");

            int have = inv.Slots[c].Count;
            bool paid = GeoWallet.Pay(inv, 100);
            yield return new WaitForSeconds(0.3f);
            Check("付钱从钱币槽扣", paid && inv.Slots[c].Count == have - 100 && PlayerData.instance.geo == GeoWallet.Held(inv) + GeoWallet.Banked(inv),
                $"钱币槽 {have} → {inv.Slots[c].Count}，游戏吉欧 {PlayerData.instance.geo}");

            InventoryGui.Open = true;
            yield return new WaitForSeconds(0.6f);
            Shot("Town", "coinslot_bag");
            yield return null;
            InventoryGui.Open = false;

            for (int i = 0; i < Inventory.Total; i++) inv.Slots[i] = saved[i];
            mod.SetInfiniteItems(wasInfinite);
            yield return new WaitForSeconds(0.3f);
            Plugin.Log.LogInfo($"[AutoTest] 钱币槽 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
