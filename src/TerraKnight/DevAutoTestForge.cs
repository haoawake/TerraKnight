using System.Collections;
using TerraKnight.Items;
using TerraKnight.Town;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "forge"): the Goblin Tinkerer's forging. He is in Dirtmouth at stage 0; his "Forge" button opens his slot
    /// for a weapon or a piece of armor; a forging pays its price and, when it takes, adds a level (+5% damage, " +N" on the name, the
    /// tooltip's line), when it fails keeps the level and the money spent; the cap is a small upgrade by the weapon's own stage
    /// (the nails' the stage reached's), an old save's weapon past it kept and forged no further; the price and the odds
    /// follow the weapon and its level; a shop pays more for a higher level; the level stays through a reforge and the
    /// save's packing.
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
            bool wasNoFallDamage = mod.DevNoFallDamage;
            mod.DevNoFallDamage = true;
            mod.SetInfiniteItems(false);
            Shop.DevStage = 0;
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(4f);
            var hero = HeroController.instance;
            var at = TownNpcs.DevPos("GoblinTinkerer");
            Check("开局哥布林就在镇上", at.HasValue, "阶段 0");
            if (!at.HasValue) { Shop.DevStage = -1; mod.SetInfiniteItems(wasInfinite); mod.DevNoFallDamage = wasNoFallDamage; yield break; }
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
            { InventoryGui.DevReleaseHammer(); InventoryGui.DevForge(); }
            Check("成功：+1 级、伤害 +5%、名字带 +1", TownNpcs.ReforgeItem.Forge == 1 && TownNpcs.ReforgeItem.Def.Damage == Mathf.RoundToInt(dmg0 * (1f + Forging.DamagePerLevel)) && TownNpcs.ReforgeItem.Def.Name.EndsWith("+1") && money0 - Shop.Purse == cost,
                $"{TownNpcs.ReforgeItem.Def.Name}，伤害 {dmg0}→{TownNpcs.ReforgeItem.Def.Damage}，花了 {money0 - Shop.Purse}（应 {cost}）");
            long cost1 = Forging.Cost(TownNpcs.ReforgeItem);
            Forging.DevOutcome = false;
            long m1 = Shop.Purse;
            { InventoryGui.DevReleaseHammer(); InventoryGui.DevForge(); }
            Check("失败：钱照扣、等级不变", TownNpcs.ReforgeItem.Forge == 1 && m1 - Shop.Purse == cost1, $"等级 {TownNpcs.ReforgeItem.Forge}，扣 {m1 - Shop.Purse}");
            Forging.DevOutcome = true;
            for (int k = 0; k < 10; k++) { InventoryGui.DevReleaseHammer(); InventoryGui.DevForge(); }
            int ironStage = ItemDb.Get(ItemType.IronBroadsword).Stage;
            Check("阶段 0 的武器上限 +2", ironStage == 0 && TownNpcs.ReforgeItem.Forge == Forging.StageCap(0) && Forging.MaxLevelOf(ItemType.IronBroadsword) == Forging.StageCap(0) && Forging.StageCap(0) == 2,
                $"铁阔剑阶段 {ironStage}，等级 {TownNpcs.ReforgeItem.Forge}，上限 {Forging.MaxLevelOf(ItemType.IronBroadsword)}");
            Shop.DevStage = 3;
            { InventoryGui.DevReleaseHammer(); InventoryGui.DevForge(); }
            Check("玩家到了阶段 3，阶段 0 的武器满级了也不能再锻造", TownNpcs.ReforgeItem.Forge == Forging.StageCap(0) && Forging.AtMax(TownNpcs.ReforgeItem),
                $"等级 {TownNpcs.ReforgeItem.Forge}，满级 {Forging.AtMax(TownNpcs.ReforgeItem)}");
            Shop.DevStage = 5;
            // Each stage's weapons: their own stage's cap (+2, +3, +4, +5, +6, +6); the nails follow the stage reached.
            var capsWrong = new System.Collections.Generic.List<string>();
            var capsSeen = new System.Collections.Generic.List<string>();
            for (int st = 0; st <= 5; st++)
            {
                ItemDef sample = null;
                foreach (var d in ItemDb.All) if (d != null && Forging.CanForge(d) && d.Stage == st && d.Type != ItemType.KnightNail && d.Type != ItemType.DreamNail) { sample = d; break; }
                if (sample == null) { capsWrong.Add($"阶段 {st} 没有武器"); continue; }
                int cap = Forging.MaxLevelOf(sample.Type);
                capsSeen.Add($"{sample.Name}(阶段{st}) +{cap}");
                if (cap != new[] { 2, 3, 4, 5, 6, 6 }[st]) capsWrong.Add($"{sample.Name} {cap}");
            }
            Shop.DevStage = 3;
            int nailCap = Forging.MaxLevelOf(ItemType.KnightNail);
            Check("武器上限保持小幅成长（+2/+3/+4/+5/+6/+6），骨钉跟着玩家阶段", capsWrong.Count == 0 && nailCap == 5, $"{string.Join("，", capsSeen)}；玩家阶段 3 骨钉 +{nailCap}；不对的 {string.Join("、", capsWrong)}");
            // An old save's weapon past its cap: kept as it is, forged no further.
            var old = ItemStack.Of(ItemType.IronBroadsword);
            old.Forge = 20;
            long oldPurse = Shop.Purse;
            var tried = Forging.Try(ref old);
            Check("老存档里超过上限的武器保持原样、不能再锻造", tried == Forging.Result.AtMax && old.Forge == 20 && Shop.Purse == oldPurse && old.Def.Name.EndsWith("+20"),
                $"{old.Def.Name}，结果 {tried}");
            // A shop pays more the higher the level: a fifth of the levels' prices on top.
            var s0 = ItemStack.Of(ItemType.NightsEdge);
            var s5 = s0; s5.Forge = 5;
            var s15 = s0; s15.Forge = 15;
            long v0 = Shop.SellValue(s0), v5 = Shop.SellValue(s5), v15 = Shop.SellValue(s15);
            Check("锻造等级越高卖得越多", v0 > 0 && v5 > v0 && v15 > v5 && v5 - v0 == Forging.PricesTo(ItemType.NightsEdge, 5) / 5,
                $"永夜刃 +0 {Shop.Money(v0)}，+5 {Shop.Money(v5)}（锻造共 {Shop.Money(Forging.PricesTo(ItemType.NightsEdge, 5))}），+15 {Shop.Money(v15)}");
            // Reproduce the transaction, then audit every forgeable item against the cheapest successful path.
            var saleFailures = new System.Collections.Generic.List<string>();
            int saleChecks = 0, cappedAtOne = 0;
            ItemType arbitrageWeapon = ItemType.None;
            foreach (var definition in ItemDb.All)
            {
                if (!Forging.Forgeable(definition)) continue;
                var audit = ItemStack.Of(definition.Type);
                long minimumSpent = 0;
                for (int level = 1; level <= Forging.MaxLevelOf(audit.Type); level++)
                {
                    long stepCost = Forging.Cost(audit);
                    minimumSpent += Equipment.Current.Discount ? stepCost : Shop.Discounted(stepCost, true);
                    audit.Forge = (byte)level;
                    long sale = Shop.SellValue(audit);
                    saleChecks++;
                    if (sale > minimumSpent) saleFailures.Add($"{audit.Type}+{level}: {sale}>{minimumSpent}");
                    if (level == 1)
                    {
                        long oldSale = System.Math.Max(1L, Shop.ResalePriceOf(audit.Def) / 5) + Forging.SellBonus(audit);
                        if (definition.Value > 0 && !Recipes.Worthless(audit.Type) && oldSale > minimumSpent)
                        {
                            cappedAtOne++;
                            if (arbitrageWeapon == ItemType.None && Forging.CanForge(definition)) arbitrageWeapon = audit.Type;
                        }
                        if (Prefixes.CanHave(definition)) foreach (var prefixOption in Prefixes.DevRollable(definition))
                        {
                            var prefixed = audit; prefixed.Prefix = (byte)prefixOption.Id;
                            saleChecks++;
                            if (Shop.SellValue(prefixed) > minimumSpent) saleFailures.Add($"{audit.Type} prefix {prefixOption.Id}");
                        }
                    }
                }
                // Old saves can contain levels beyond today's cap.
                audit.Forge = byte.MaxValue;
                saleChecks++;
                if (Shop.SellValue(audit) > Forging.PricesTo(audit.Type, audit.Forge, minimumPaid: true))
                    saleFailures.Add($"{audit.Type}+255");
            }
            Check("全装备、词缀、旧等级出售不超过最低锻造投入", saleFailures.Count == 0,
                $"{saleChecks} 项；+1 原本可套利 {cappedAtOne} 种；{string.Join("，", saleFailures)}");
            var transaction = ItemStack.Of(arbitrageWeapon == ItemType.None ? ItemType.Zenith : arbitrageWeapon);
            long beforeForgeSale = Shop.Purse;
            Forging.DevOutcome = true;
            var transactionResult = Forging.Try(ref transaction);
            long invested = beforeForgeSale - Shop.Purse;
            var savedSlot = inv.Slots[20];
            inv.Slots[20] = transaction;
            bool soldForged = InventoryGui.DevSell(20);
            Check("实际锻造+1再出售不会增加钱包", transactionResult == Forging.Result.Forged && soldForged
                && inv.Slots[20].IsEmpty && Shop.Purse <= beforeForgeSale,
                $"{transaction.Type}，锻造花费 {Shop.Money(invested)}，出售 {Shop.Money(Shop.SellValue(transaction))}，净变化 {Shop.Purse - beforeForgeSale}");
            inv.Slots[20] = savedSlot;
            Forging.DevOutcome = null;

            // Price and odds by the weapon and its level.
            var low = ItemStack.Of(ItemType.IronBroadsword);
            var high = ItemStack.Of(ItemType.Zenith);
            var highLv = high;
            highLv.Forge = 10;
            Check("越高阶越贵、成功率都是 75%", Forging.Cost(high) > Forging.Cost(low) && Forging.Cost(highLv) > Forging.Cost(high)
                && Forging.Chance(low) == 0.75f && Forging.Chance(high) == 0.75f && Forging.Chance(highLv) == 0.75f,
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

            // Armor shares the six stage caps; old over-cap armor keeps its level without charging again.
            for (int st = 0; st <= 5; st++)
            {
                int expected = new[] { 2, 3, 4, 5, 6, 6 }[st];
                bool capsMatch = Forging.ArmorStageCap(st) == expected;
                foreach (var armorDef in ItemDb.All)
                    if (Forging.IsArmorPiece(armorDef) && armorDef.Stage == st)
                        capsMatch &= Forging.MaxLevelOf(armorDef.Type) == expected;
                Check($"阶段 {st} 所有盔甲上限 +{expected}", capsMatch);
            }
            var oldArmor = ItemStack.Of(ItemType.SolarFlareHelmet);
            oldArmor.Forge = 18;
            long oldArmorPurse = Shop.Purse;
            int oldArmorDefense = oldArmor.Def.Defense;
            Check("旧超限盔甲保留等级和防御，拒绝继续锻造且不扣款", Forging.Try(ref oldArmor) == Forging.Result.AtMax
                && oldArmor.Forge == 18 && oldArmor.Def.Defense == oldArmorDefense && Shop.Purse == oldArmorPurse);
            // Iron is warrior armor: +2 defense per level, with a level/cap label.
            {
                var helm = ItemStack.Of(ItemType.IronHelmet);
                int def0 = helm.Def.Defense, armorCap = Forging.MaxLevelOf(helm.Type);
                long helmCost = Forging.Cost(helm), swordCost = Forging.Cost(ItemStack.Of(ItemType.IronBroadsword));
                Forging.DevOutcome = true;
                for (int k = 0; k < armorCap + 2; k++) Forging.Try(ref helm);
                Forging.DevOutcome = null;
                Check("战士盔甲按装备阶段封顶，每级 +2 防御，名字带等级", Forging.Forgeable(ItemDb.Get(ItemType.IronHelmet)) && helm.Forge == armorCap && Forging.AtMax(helm) && helm.Def.Defense == def0 + 2 * armorCap && helm.Def.Name.EndsWith($"(Lv.{armorCap}/Lv.{armorCap})"),
                    $"{helm.Def.Name}，防御 {def0}→{helm.Def.Defense}，上限 +{Forging.MaxLevelOf(ItemType.IronHelmet)}");
                var solar = ItemStack.Of(ItemType.SolarFlareHelmet);
                Check("盔甲上限随装备阶段递增，敲满总价接近原来", Forging.MaxLevelOf(solar.Type) == Forging.ArmorStageCap(solar.Def.Stage) && Forging.MaxLevelOf(solar.Type) >= armorCap && Forging.PricesTo(solar.Type, Forging.MaxLevelOf(solar.Type)) >= Forging.PricesTo(helm.Type, armorCap),
                    $"铁头盔 +1 {Shop.Money(helmCost)}（铁阔剑 {Shop.Money(swordCost)}），日耀头盔 +1 {Shop.Money(Forging.Cost(solar))}；到各自上限共 {Shop.Money(Forging.PricesTo(ItemType.IronHelmet, armorCap))} / {Shop.Money(Forging.PricesTo(solar.Type, Forging.MaxLevelOf(solar.Type)))}");
                var packedHelm = Save.ModSave.DevRoundTrip(helm);
                var plainHelm = ItemStack.Of(ItemType.IronHelmet);
                Check("盔甲的锻造等级存档保留、出售遵守价值和投入上限", packedHelm.Forge == armorCap
                    && Shop.SellValue(helm) >= Shop.SellValue(plainHelm)
                    && Shop.SellValue(helm) <= Forging.PricesTo(helm.Type, armorCap, minimumPaid: true)
                    && (!Recipes.Worthless(helm.Type) || Shop.SellValue(helm) == 0),
                    $"读回 +{packedHelm.Forge}，卖 {Shop.Money(Shop.SellValue(helm))} / 原样 {Shop.Money(Shop.SellValue(plainHelm))}");
                int plain = 0, forged = 0;
                foreach (var t in new[] { ItemType.IronHelmet, ItemType.IronChainmail, ItemType.IronGreaves })
                {
                    var p = ItemStack.Of(t);
                    plain += p.Def.Defense;
                    p.Forge = (byte)Forging.MaxLevelOf(t);
                    forged += p.Def.Defense;
                }
                Check("战士头身腿同阶敲满增加对应防御", forged - plain == 6 * armorCap, $"{plain} → {forged}");
                TownNpcs.ReturnReforgeItem();
                bool placed = InventoryGui.DevPutReforge(ItemStack.Of(ItemType.IronChainmail));
                yield return new WaitForSeconds(0.3f);
                Check("哥布林的锻造格放得下盔甲", placed && TownNpcs.ReforgeItem.Type == ItemType.IronChainmail, TownNpcs.ReforgeItem.IsEmpty ? "空" : TownNpcs.ReforgeItem.Def.Name);
                Shot("Town", "forge_armor");
                yield return null;
            }

            TownNpcs.Close(false);
            Shop.DevStage = -1;
            mod.SetInfiniteItems(wasInfinite);
            mod.DevNoFallDamage = wasNoFallDamage;
            Plugin.Log.LogInfo($"[AutoTest] 锻造 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
