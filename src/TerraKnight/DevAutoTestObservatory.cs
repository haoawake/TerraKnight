using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TerraKnight.Items;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator ObservatoryTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated save required");
            var results = new List<object>();
            int failures = 0;
            void Check(string name, bool pass)
            {
                results.Add(new { Name = name, Passed = pass });
                Plugin.Log.LogInfo("[AutoTest] 观测仪/" + name + (pass ? " 通过" : " 不通过"));
                if (!pass) failures++;
            }
            yield return GoTo("Town", "left1");
            mod.ApplyCreative(false);
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            var inv = mod.Inventory;
            inv.Clear(); Bestiary.Clear();
            mod.GrantedAbilities.RemoveWhere(flag => flag == BossMastery.Initialized || flag.StartsWith(BossMastery.Prefix));
            yield return null;
            Check("无首杀时无永久加成", BossMastery.Count == 0 && BossMastery.Bonus == 0f);
            var clean = Equipment.Current;
            Bestiary.Kills["Crawler"] = 900;
            Bestiary.Kills["FalseKnight"] = 1;
            Bestiary.Order.Add("FalseKnight");
            BossMastery.Record("FalseKnight", "Crossroads_10", "False Knight");
            yield return null;
            Check("首次Boss加0.5%且普通怪不计", BossMastery.Count == 1 && Mathf.Abs(Equipment.Current.Endurance - clean.Endurance - .005f) < .00001f);
            Check("四职业增伤均获得首杀加成", Mathf.Abs(Equipment.Current.MeleeDamage - clean.MeleeDamage - .005f) < .00001f
                && Mathf.Abs(Equipment.Current.RangedDamage - clean.RangedDamage - .005f) < .00001f
                && Mathf.Abs(Equipment.Current.MagicDamage - clean.MagicDamage - .005f) < .00001f
                && Mathf.Abs(Equipment.Current.MinionDamage - clean.MinionDamage - .005f) < .00001f);
            Bestiary.Kills["FalseKnight"] = 999;
            BossMastery.Record("FalseKnight", "GG_False_Knight", "False Knight");
            yield return null;
            Check("同Boss重复击杀不叠加", BossMastery.Count == 1 && Mathf.Abs(BossMastery.Bonus - .005f) < .00001f);
            Bestiary.Kills["Hornet"] = 1; Bestiary.Kills["Hornet@4"] = 1;
            Bestiary.Kills["FalseKnight@5"] = 1;
            Bestiary.Kills["not_a_boss"] = 99; Bestiary.Kills["MageLord"] = 0;
            BossMastery.Record("Hornet", "Fungus1_04_boss", "Hornet");
            BossMastery.Record("Hornet@4", "Deepnest_East_Hornet", "Hornet");
            BossMastery.Record("FalseKnight@5", "Dream_01_False_Knight", "False Knight");
            yield return null;
            Check("独立强化版分别首杀且未知与零次不计", BossMastery.Count == 4 && Mathf.Abs(BossMastery.Bonus - .02f) < .00001f);
            int distinct = BossMastery.Count;
            BossMastery.Record("MegaBeamMiner", "Mines_18", "Mega Beam Miner");
            BossMastery.Record("MegaBeamMiner", "Mines_32", "Mega Beam Miner Rematch");
            Check("共用图鉴的水晶守卫与暴怒守卫分别计首杀", BossMastery.Count == distinct + 2);
            BossMastery.Record("MegaBeamMiner", "GG_Crystal_Guardian", "Mega Beam Miner");
            Check("普通神居重赛不重复发奖", BossMastery.Count == distinct + 2);
            mod.GrantedAbilities.Remove(BossMastery.Prefix + ItemType.RelicMegaBeamMiner);
            mod.GrantedAbilities.Remove(BossMastery.Prefix + ItemType.RelicEnragedGuardian);
            var plain = ItemStack.Of(ItemType.IronBroadsword);
            inv.Slots[0] = plain;
            yield return null;
            int plainDamage = Equipment.Damage(inv.Current.Def);
            var forged = plain; forged.Forge = 5; forged.Prefix = 81;
            inv.Slots[0] = forged;
            inv.Armor[0] = ItemStack.Of(ItemType.IronHelmet); inv.Armor[0].Forge = 2;
            inv.Accessories[0] = ItemStack.Of(ItemType.WormScarf);
            yield return null; yield return null;
            var weaponRows = PlayerStatReport.Capture(1);
            Check("观测使用含传奇词缀与锻造的手持定义", PlayerStatReport.CurrentWeapon(out _) == forged.Def
                && Equipment.Damage(forged.Def) > plainDamage && weaponRows.Any(r => r.Label == "实际武器伤害" && r.Value == Equipment.Damage(forged.Def).ToString()));
            Check("护甲锻造防御进入最终属性", mod.Stats.Defense >= ItemDb.Get(ItemType.IronHelmet).Defense + 2);
            Check("围巾和首杀免伤叠加", Mathf.Abs(mod.Stats.Endurance - ItemDb.Get(ItemType.WormScarf).Bonus.Endurance - .02f) < .00001f);
            inv.Slots[0] = ItemStack.Of(ItemType.WoodenBow);
            inv.Slots[Inventory.Size] = ItemStack.Of(ItemType.WoodenArrow, 99);
            yield return null;
            var bow = PlayerStatReport.CurrentWeapon(out var ammo);
            Check("远程显示当前实际弹药伤害", ammo != null && PlayerStatReport.Capture(1).Any(r => r.Value == Equipment.Damage(bow, ammo).ToString()));
            inv.Slots[0] = default;
            yield return null;
            Check("空手省略伤害并说明无手持武器", !PlayerStatReport.Capture(0).Any(r => r.Label == "当前武器伤害")
                && PlayerStatReport.Capture(1).Count == 1);
            int shields = ArmorEffects.SolarShields, orbs = ArmorEffects.BeetleOrbs;
            PlayerStatReport.Capture(0); PlayerStatReport.Capture(3);
            Check("查看属性不消耗护盾和甲虫层数", shields == ArmorEffects.SolarShields && orbs == ArmorEffects.BeetleOrbs);
            int slot = GameManager.instance.profileID;
            Save.ModSave.Write(slot, mod);
            Bestiary.Kills.Clear();
            mod.GrantedAbilities.RemoveWhere(flag => flag == BossMastery.Initialized || flag.StartsWith(BossMastery.Prefix));
            Save.ModSave.Read(slot, mod);
            yield return null;
            Check("实际存读档保留首杀及成长不二次叠加", BossMastery.Count == 4 && Mathf.Abs(BossMastery.Bonus - .02f) < .00001f);
            mod.GrantedAbilities.RemoveWhere(flag => flag == BossMastery.Initialized || flag.StartsWith(BossMastery.Prefix));
            Check("旧存档从已有首杀记录补计一次", BossMastery.Count == 4 && BossMastery.Count == 4);
            InventoryGui.Open = true;
            InventoryGui.DevUtilityClick(4);
            Check("第五快捷按钮打开观测仪", InventoryGui.DevStatsOpen);
            var texture = TerrariaAssets.Texture("UtilityStats");
            Check("用户PNG已嵌入且像素采样", texture.width == 1254 && texture.height == 1254 && texture.filterMode == FilterMode.Point);
            foreach (float ui in new[] { .65f, .85f, 1f, 1.25f })
            {
                var view = new Rect(0, 0, 1280, 720);
                var added = InventoryGui.UtilityButtonRect(view, ui, 4);
                Check("第五按钮不重叠/" + ui, Enumerable.Range(0, 4).All(i => !added.Overlaps(InventoryGui.UtilityButtonRect(view, ui, i))));
                var panel = InventoryGui.StatsPanelRect(view, ui);
                Check("面板完整位于画面/" + ui, view.Contains(panel.min) && panel.xMax <= view.xMax && panel.yMax <= view.yMax);
            }
            inv.Slots[0] = forged;
            yield return null;
            for (int tab = 0; tab < 4; tab++)
            {
                InventoryGui.DevStatsTab(tab);
                yield return new WaitForSecondsRealtime(.25f);
                Check("属性标签有可读内容/" + tab, PlayerStatReport.Capture(tab).All(r => !string.IsNullOrEmpty(r.Label) && r.Value != null));
                Shot("Town", "observatory_tab_" + tab);
                yield return new WaitForSecondsRealtime(.15f);
            }
            InventoryGui.DevUtilityClick(2);
            Check("原圣巢背包互斥打开", !InventoryGui.DevStatsOpen);
            InventoryGui.DevUtilityClick(4);
            InventoryGui.Open = false;
            Check("收起背包同时收起观测仪", !InventoryGui.DevStatsOpen);
            File.WriteAllText(Path.Combine(DevCopy.SaveDir, "observatory-results.json"), JsonConvert.SerializeObject(new { Checks = results.Count, Failures = failures, Results = results }, Formatting.Indented));
            if (failures > 0) Plugin.Log.LogError("[AutoTest] 观测仪合计 不通过");
        }
    }
}
