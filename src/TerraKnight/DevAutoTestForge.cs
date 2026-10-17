using System.Collections;
using TerraKnight.Items;
using TerraKnight.Town;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "forge"): the Goblin Tinkerer's forging. He is in Dirtmouth at stage 0; his "Forge" button opens his slot
    /// for a weapon only; a forging pays its price and, when it takes, adds a level (+5% damage, " +N" on the name, the
    /// tooltip's line), when it fails keeps the level and the money spent; the cap is five levels a stage; the price and the
    /// odds follow the weapon and its level; the level stays through a reforge and the save's packing.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator ForgeTest()
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail = "")
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 锻造 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            var inv = mod.Inventory;
            bool wasInfinite = inv.Infinite;
            mod.SetInfiniteItems(false);
            Shop.DevStage = 0;
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(4f);
            var hero = HeroController.instance;
            var at = TownNpcs.DevPos("GoblinTinkerer");
            Check("开局哥布林就在镇上", at.HasValue, "阶段 0");
            if (!at.HasValue) { Shop.DevStage = -1; mod.SetInfiniteItems(wasInfinite); yield break; }
            hero.transform.position = Clear(hero, at.Value + Vector2.left * 0.8f);
            yield return null;
            TownNpcs.Close(false);
            bool talked = TownNpcs.TryRightClick(TownNpcs.DevPos("GoblinTinkerer").Value);
            TownNpcs.OpenForge();
            yield return null;
            Check("锻造按钮打开格子", talked && TownNpcs.ForgeMode && InventoryGui.Open);
            Check("只能放武器", !InventoryGui.DevPutReforge(ItemStack.Of(ItemType.HermesBoots)) && TownNpcs.ReforgeItem.IsEmpty);
            inv.Mouse = default;
            Check("放武器", InventoryGui.DevPutReforge(ItemStack.Of(ItemType.IronBroadsword)) && TownNpcs.ReforgeItem.Type == ItemType.IronBroadsword);

            int dmg0 = TownNpcs.ReforgeItem.Def.Damage;
            Shop.Earn(100000000);
            long money0 = Shop.Purse;
            long cost = Forging.Cost(TownNpcs.ReforgeItem);
            Forging.DevOutcome = true;
            InventoryGui.DevForge();
            Check("成功：+1 级、伤害 +5%、名字带 +1", TownNpcs.ReforgeItem.Forge == 1 && TownNpcs.ReforgeItem.Def.Damage == Mathf.RoundToInt(dmg0 * (1f + Forging.DamagePerLevel)) && TownNpcs.ReforgeItem.Def.Name.EndsWith("+1") && money0 - Shop.Purse == cost,
                $"{TownNpcs.ReforgeItem.Def.Name}，伤害 {dmg0}→{TownNpcs.ReforgeItem.Def.Damage}，花了 {money0 - Shop.Purse}（应 {cost}）");
            long cost1 = Forging.Cost(TownNpcs.ReforgeItem);
            Forging.DevOutcome = false;
            long m1 = Shop.Purse;
            InventoryGui.DevForge();
            Check("失败：钱照扣、等级不变", TownNpcs.ReforgeItem.Forge == 1 && m1 - Shop.Purse == cost1, $"等级 {TownNpcs.ReforgeItem.Forge}，扣 {m1 - Shop.Purse}");
            Forging.DevOutcome = true;
            for (int k = 0; k < 10; k++) InventoryGui.DevForge();
            Check("阶段 0 上限 +5", TownNpcs.ReforgeItem.Forge == 5 && Forging.MaxLevel == 5, $"等级 {TownNpcs.ReforgeItem.Forge}，上限 {Forging.MaxLevel}");
            Shop.DevStage = 3;
            Check("每解锁一个阶段多 5 级", Forging.MaxLevel == 20, $"阶段 3 上限 {Forging.MaxLevel}");
            InventoryGui.DevForge();
            Check("解锁后能继续锻造", TownNpcs.ReforgeItem.Forge == 6, $"等级 {TownNpcs.ReforgeItem.Forge}");
            Forging.DevOutcome = null;

            // Price and odds by the weapon and its level.
            var low = ItemStack.Of(ItemType.IronBroadsword);
            var high = ItemStack.Of(ItemType.Zenith);
            var highLv = high;
            highLv.Forge = 10;
            Check("越高阶越贵、成功率都是一半", Forging.Cost(high) > Forging.Cost(low) && Forging.Cost(highLv) > Forging.Cost(high)
                && Forging.Chance(low) == 0.5f && Forging.Chance(high) == 0.5f && Forging.Chance(highLv) == 0.5f,
                $"铁阔剑 {Shop.Money(Forging.Cost(low))} {Forging.Chance(low):P0}；天顶剑 {Shop.Money(Forging.Cost(high))} {Forging.Chance(high):P0}；天顶剑+10 {Shop.Money(Forging.Cost(highLv))} {Forging.Chance(highLv):P0}");

            // Kept through a reforge and the save's packing (the stack itself: the Goblin may have walked off by now).
            var sNow = TownNpcs.ReforgeItem;
            if (sNow.IsEmpty)
                foreach (var st in inv.Slots) if (st.Type == ItemType.IronBroadsword && st.Forge > 0) { sNow = st; break; }
            int lv = sNow.Forge;
            sNow.Prefix = (byte)Prefixes.Roll(ItemDb.Get(sNow.Type), out _);
            Check("重铸不影响锻造等级", lv > 0 && sNow.Forge == lv && sNow.Prefix > 0 && sNow.Def.Name.EndsWith("+" + lv), sNow.Def.Name);
            var packed = Save.ModSave.DevRoundTrip(sNow);
            Check("存档带上锻造等级", packed.Forge == lv && packed.Prefix == sNow.Prefix, $"读回 +{packed.Forge}");
            Check("锻造后的定义认得出原来的", sNow.Def.Forge == lv && sNow.Def.Unforged != null && sNow.Def.Unforged.Damage < sNow.Def.Damage,
                $"{sNow.Def.Unforged?.Damage} → {sNow.Def.Damage}");
            // The price table, for the record.
            foreach (var t in new[] { ItemType.CopperShortsword, ItemType.IronBroadsword, ItemType.Minishark, ItemType.WandofSparking, ItemType.SlimeStaff, ItemType.LeatherWhip,
                ItemType.MoltenPickaxe, ItemType.NightsEdge, ItemType.Megashark, ItemType.TerraBlade, ItemType.LastPrism, ItemType.StardustDragonStaff, ItemType.Zenith, ItemType.KnightNail })
            {
                var one = ItemStack.Of(t);
                var ten = one; ten.Forge = 10;
                var twenty = one; twenty.Forge = 20;
                Plugin.Log.LogInfo($"[AutoTest] 锻造价格 {ItemDb.Get(t).Name}：强度 {Forging.Strength(ItemDb.Get(t)):F0}，+1 {Shop.Money(Forging.Cost(one))}（{Forging.Chance(one):P0}），"
                    + $"到 +5 共 {Shop.Money(Forging.DevTotal(t, 5))}，到 +10 共 {Shop.Money(Forging.DevTotal(t, 10))}（+11 {Forging.Chance(ten):P0}），"
                    + $"到 +20 共 {Shop.Money(Forging.DevTotal(t, 20))}（+21 {Forging.Chance(twenty):P0}），到 +30 共 {Shop.Money(Forging.DevTotal(t, 30))}");
            }
            TownNpcs.Close(false);
            hero.transform.position = Clear(hero, TownNpcs.DevPos("GoblinTinkerer").Value + Vector2.left * 0.8f);
            yield return null;
            TownNpcs.TryRightClick(TownNpcs.DevPos("GoblinTinkerer").Value);
            TownNpcs.OpenForge();
            InventoryGui.DevPutReforge(sNow);
            yield return new WaitForSeconds(0.3f);
            Shot("Town", "forge_panel");
            yield return null;

            TownNpcs.Close(false);
            Shop.DevStage = -1;
            mod.SetInfiniteItems(wasInfinite);
            Plugin.Log.LogInfo($"[AutoTest] 锻造 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
