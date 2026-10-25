using System.Collections;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "dd2acc"): the Tavernkeep's four accessories (the Apprentice's Scarf, Squire's Shield, Huntress's Buckler,
    /// Monk's Belt) worn side by side, each clicked from the cursor into its own slot, as vanilla allows
    /// (ItemSlot.CanEquipBothAccessories: different items, none in AccessoryIncompatibilityType); their bonus once
    /// (Player.dd2Accessory, a flag).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator Dd2AccTest(string scene, HeroController hero)
        {
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 哨兵饰品 {what} {(pass ? "通过" : "不通过")}");
            }
            var inv = mod.Inventory;
            var keep = (ItemStack[])inv.Accessories.Clone();
            bool open = InventoryGui.Open;
            try
            {
                InventoryGui.Open = true;
                for (int k = 0; k < inv.Accessories.Length; k++) inv.Accessories[k] = default;
                Equipment.Update(inv);
                int turrets0 = Equipment.Current.MaxTurrets;
                ItemType[] four = { ItemType.ApprenticeScarf, ItemType.SquireShield, ItemType.HuntressBuckler, ItemType.MonkBelt };
                for (int k = 0; k < four.Length; k++)
                {
                    inv.Mouse = ItemStack.Of(four[k]);
                    bool fits = InventoryGui.DevFits(inv.Mouse, k);
                    InventoryGui.DevLeftClickEquip(false, k);
                    Check(fits && !inv.Accessories[k].IsEmpty && inv.Accessories[k].Type == four[k] && inv.Mouse.IsEmpty,
                        $"{ItemDb.Get(four[k]).Name} 放进第 {k + 1} 格（可放 {fits}，格里 {(inv.Accessories[k].IsEmpty ? "空" : inv.Accessories[k].Def.Name)}）");
                    inv.Mouse = default;
                }
                Equipment.Update(inv);
                Plugin.Log.LogInfo($"[AutoTest] 哨兵饰品 四件都戴上：哨兵上限 +{Equipment.Current.MaxTurrets - turrets0}，召唤伤害 {Equipment.Current.MinionDamage:P0}");
                yield return null;
            }
            finally
            {
                for (int k = 0; k < keep.Length; k++) inv.Accessories[k] = keep[k];
                inv.Mouse = default;
                InventoryGui.Open = open;
                Equipment.Update(inv);
            }
            Plugin.Log.LogInfo($"[AutoTest] 哨兵饰品 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
