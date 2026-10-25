using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator CharmExtraAudit()
        {
            charmPass = charmAll = 0; charmTag = "联动";
            bool page = Charms.Enabled;
            yield return CharmPlace(); CharmAuditReset(); yield return null;
            var hero = HeroController.instance;
            WearOnly(1); WorldItems.DevClear(); WorldItems.DevNoGrab = true;
            var from = (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center;
            WorldItems.Drop(ItemStack.Of(ItemType.CopperOre), from + Vector2.right);
            WorldItems.Drop(ItemStack.Of(ItemType.TinOre), from + Vector2.right * 10f);
            WorldItems.DropIn(ItemStack.Of(ItemType.IronOre), "Crossroads_01", Vector2.one);
            var swarm = typeof(CharmSkills).GetMethod("Swarm", BindingFlags.Static | BindingFlags.NonPublic);
            swarm.Invoke(null, new object[] {hero});
            CharmSkills.Tick(hero);
            CharmCheck("蜂群尚未到达远处时不会隔空拾取", WorldItems.DevCount(ItemType.TinOre) == 1);
            yield return new WaitForSeconds(4.3f);
            CharmCheck("蜂群到达后拾取近远物品", WorldItems.DevCount(ItemType.CopperOre) == 0 && WorldItems.DevCount(ItemType.TinOre) == 0, WorldItems.DevDescribe());
            CharmCheck("蜂群不会拾取其他房间", WorldItems.DevCount(ItemType.IronOre) == 1);
            var rippleField = typeof(CharmSkills).GetField("ripple", BindingFlags.Static | BindingFlags.NonPublic);
            foreach (string stop in new[] {"关闭页面", "死亡", "换场景"})
            {
                swarm.Invoke(null, new object[] {hero});
                if (stop == "关闭页面") Charms.DevSetPage(false);
                if (stop == "死亡") hero.cState.dead = true;
                if (stop == "换场景") typeof(CharmSkills).GetField("rippleScene", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, "not-current-room");
                CharmSkills.Tick(hero);
                CharmCheck("蜂群终止：" + stop, !((GameObject)rippleField.GetValue(null)).activeSelf);
                hero.cState.dead = false; Charms.DevSetPage(true);
            }
            WorldItems.DevNoGrab = false; WorldItems.DevClear();
            CharmAuditReset(); yield return null;
            WearOnly(8, 9); yield return null;
            CharmEffects.Press(hero, 8);
            yield return new WaitForSeconds(5.3f);
            int heart = CharmEffects.DevHeart, core = CharmEffects.DevCore;
            CharmCheck("双生命血护盾能同时生成", heart > 0 && core > 0 && CharmEffects.ShieldTotal == heart + core, $"heart={heart} core={core}");
            int remainder = CharmEffects.Shields(core + heart + 500);
            CharmCheck("先消耗核心再碎心且大伤害不溢出", remainder == 0 && CharmEffects.ShieldTotal == 0, "remainder=" + remainder);
            WearOnly(13, 18, 25, 32); Equipment.Update(mod.Inventory);
            CharmCheck("近战四件范围乘算且不重复", Mathf.Abs(CharmEffects.MeleeReach - 1.725f) < 0.001f);
            WearOnly(19, 33, 36); Equipment.Update(mod.Inventory);
            var staff = ItemDb.Get(ItemType.AmethystStaff);
            CharmCheck("魔法三件减耗与弹幕尺寸", Mathf.Abs(CharmEffects.ManaFactor(staff) - 0.45f) < 0.001f && Mathf.Abs(CharmEffects.SpellSize - 1.3f) < 0.001f);
            WearOnly(20, 21, 24);
            CharmCheck("经济三件捕魂与噬魂基础加成为50%", CharmEffects.ScaleGeo(100) == 150);
            WearOnly(21, 39, 40); CharmEffects.DevEaterMode = 2;
            CharmCheck("召唤搭配允许工具和召唤杖并禁止剑", Mathf.Abs(CharmEffects.MinionScale - 1.4f) < 0.001f && !CharmEffects.WeaponLocked(ItemDb.Get(ItemType.CopperPickaxe)) && !CharmEffects.WeaponLocked(ItemDb.Get(ItemType.SlimeStaff)) && CharmEffects.WeaponLocked(ItemDb.Get(ItemType.CopperBroadsword)));
            Charms.DevSetPage(false); CharmSkills.Tick(hero);
            CharmCheck("关闭护符页恢复职业倍率和攻击权限", CharmEffects.MinionScale == 1f && !CharmEffects.WeaponLocked(ItemDb.Get(ItemType.CopperBroadsword)) && CharmEffects.MeleeReach == 1f);
            CharmAuditReset(); yield return null;
            var pd = PlayerData.instance;
            foreach (int state in new[] {0, 1, 2})
            {
                pd.royalCharmState = state; WearOnly(); pd.SetBool("gotCharm_36", true);
                CharmCheck("白色碎片不能装备 state=" + state, Charms.Click(36) == Charms.Answer.CantWear && !Charms.Worn(36));
            }
            pd.royalCharmState = 4; WearOnly(36);
            CharmCheck("虚空之心不占槽且不能卸下", Charms.Cost(36) == 0 && Charms.Click(36) == Charms.Answer.CantRemove && Charms.Worn(36));
            pd.royalCharmState = 3; CharmAuditReset(); yield return null;
            var levelDamage = new Dictionary<int, int>();
            foreach (int level in new[] {1, 2, 4})
            {
                WearOnly(); yield return new WaitForSeconds(1f);
                pd.grimmChildLevel = level; WearOnly(40);
                yield return ToDummy(4f); yield return new WaitForSeconds(1f);
                int before = CharmEffects.DevGrimmHits;
                yield return new WaitForSeconds(4f);
                int hits = CharmEffects.DevGrimmHits - before;
                levelDamage[level] = CharmEffects.DevLastCompanionDamage;
                CharmCheck("格林之子等级攻击 " + level, level == 1 ? hits == 0 : hits > 0, $"hits={hits} damage={levelDamage[level]}");
            }
            CharmCheck("四级格林之子伤害高于二级", levelDamage[4] > levelDamage[2], $"{levelDamage[2]} -> {levelDamage[4]}");
            WearOnly(); yield return new WaitForSeconds(1f);
            pd.grimmChildLevel = 5; WearOnly(40); yield return new WaitForSeconds(0.6f);
            CharmCheck("无忧旋律提示不误写成格林之子", !CharmSkills.Describe(40).Contains("0.9"));
            var nativeCarefree = typeof(HeroController).GetField("carefreeShieldEquipped", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            CharmCheck("无忧旋律启用原版格挡", (bool)nativeCarefree.GetValue(hero));
            pd.isInvincible = false; Player.TerraStats.DevImmortal = true;
            var shieldCounter = typeof(HeroController).GetField("hitsSinceShielded", BindingFlags.Instance | BindingFlags.NonPublic);
            int blocked = 0, hit = 0;
            for (int n = 0; n < 8; n++)
            {
                for (float end = Time.time + 4f; !CanBeHit(hero) && Time.time < end;) yield return null;
                mod.Stats.Life = mod.Stats.LifeMax;
                shieldCounter.SetValue(hero, 7);
                HitFromRight(10); yield return null;
                if (mod.Stats.Life == mod.Stats.LifeMax) blocked++; else hit++;
                yield return new WaitForSeconds(1f);
            }
            CharmCheck("无忧旋律实际拦截敌人伤害", blocked > 0, $"blocked={blocked} hit={hit}");
            WearOnly();
            CharmCheck("卸下无忧旋律关闭原版格挡", !(bool)nativeCarefree.GetValue(hero));
            pd.isInvincible = true; Player.TerraStats.DevImmortal = false;
            CharmAuditReset(); yield return null;
            yield return CompassTest();
            CharmAuditReset(); Charms.DevAnywhere = false; Charms.DevSetPage(page);
            Plugin.Log.LogInfo($"[AutoTest] 护符联动结果 {charmPass}/{charmAll}（指南针另计）");
        }

        static object CharmPrivate(string name) => typeof(CharmEffects).GetField(name, BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        string CharmSignature()
        {
            Equipment.Update(mod.Inventory);
            var fields = new List<string>();
            foreach (var b in new[] {CharmEffects.DynamicBonuses(), Equipment.Current})
                foreach (var field in typeof(Bonuses).GetFields(BindingFlags.Public | BindingFlags.Instance))
                    fields.Add(field.Name + "=" + Convert.ToString(field.GetValue(b), System.Globalization.CultureInfo.InvariantCulture));
            return string.Join(";", fields) + "|" + CharmEffects.MeleeReach + "|" + CharmEffects.SpellSize + "|" +
                CharmEffects.ManaFactor(ItemDb.Get(ItemType.AmethystStaff)) + "|" + CharmEffects.MagicUseScale + "|" +
                CharmEffects.RunSpeedMult + "|" + CharmEffects.MinionScale + "|" + CharmEffects.ScaleGeo(100) + "|" + CharmEffects.MaskLifeBonus(100);
        }

        void CharmAuditReset()
        {
            var pd = PlayerData.instance;
            pd.royalCharmState = 3;
            pd.charmCost_36 = 5;
            pd.brokenCharm_23 = pd.brokenCharm_24 = pd.brokenCharm_25 = false;
            pd.grimmChildLevel = 3;
            EndNailTest();
            CharmSkills.DevPress = CharmSkills.DevHeld = false;
            WearOnly();
            for (int id = 1; id <= 40; id++)
            {
                Player.PlayerBuffs.Remove(CharmSkills.CooldownBase + id);
                CharmSkills.EndActive(id);
            }
            mod.Stats.Life = mod.Stats.LifeMax;
        }

        IEnumerator CharmAudit()
        {
            charmPass = charmAll = 0; charmTag = "全量";
            var pd = PlayerData.instance;
            bool page = Charms.Enabled;
            pd.isInvincible = true; mod.DevNoFallDamage = true;
            yield return CharmPlace();
            CharmAuditReset();
            yield return null;
            string phase = Environment.GetEnvironmentVariable("TK_CHARM_AUDIT_PHASE") ?? "all";
            if (phase != "singles")
            {
                for (int id = 1; id <= 40; id++)
                {
                    WearOnly(id);
                    CharmCheck("单件装备 " + id, Charms.Worn(id) && Charms.Filled == Charms.Cost(id) && !string.IsNullOrEmpty(Charms.Name(id)), CharmSkills.Describe(id));
                }
                WearOnly();
                string empty = CharmSignature();
                int pairs = 0;
                for (int a = 1; a <= 40; a++) for (int b = a + 1; b <= 40; b++)
                {
                    WearOnly(a, b); string ab = CharmSignature();
                    Equipment.Update(mod.Inventory); string twice = CharmSignature();
                    bool equipped = Charms.Worn(a) && Charms.Worn(b) && pd.equippedCharms.Count == 2 && Charms.Filled == Charms.Cost(a) + Charms.Cost(b)
                        && !ab.Contains("NaN") && !ab.Contains("Infinity");
                    WearOnly(); WearOnly(b, a); string ba = CharmSignature();
                    WearOnly();
                    CharmCheck($"双件 {a}+{b}", equipped && ab == ba && ab == twice && CharmSignature() == empty, "顺序、重复结算、卸下还原");
                    if (++pairs % 20 == 0) yield return null;
                }
                foreach (int id in new[] {23, 24, 25})
                {
                    WearOnly(); pd.SetBool("brokenCharm_" + id, true);
                    var answer = Charms.Click(id);
                    CharmCheck("破碎护符禁止装备 " + id, !Charms.Worn(id) && Charms.Filled == 0, answer.ToString());
                    pd.SetBool("brokenCharm_" + id, false);
                }
                WearOnly(36);
                var motes = (IList)CharmPrivate("motes"); motes.Clear();
                typeof(TerraMod).GetMethod("BeginUse", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(mod, new object[] { ItemDb.Get(ItemType.AmethystStaff) });
                CharmCheck("一次魔法使用只产生三粒王魂微光", motes.Count == 3, "motes=" + motes.Count);
                WearOnly(6, 14, 29);
                Player.PlayerBuffs.Remove(CharmSkills.CooldownBase + 6);
                CharmEffects.Press(HeroController.instance, 6);
                CharmEffects.Press(HeroController.instance, 14);
                typeof(CharmEffects).GetField("hiveArmed", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, true);
                var hero = HeroController.instance;
                hero.cState.dead = true; CharmEffects.Tick(hero);
                CharmCheck("死亡状态终止狂怒", CharmEffects.ScaleOutgoing(100) == 100, "out=" + CharmEffects.ScaleOutgoing(100));
                CharmCheck("死亡状态清除蜂巢待用药水加成", !CharmEffects.DevHiveArmed);
                CharmCheck("死亡状态终止稳定之体穿透", !CharmEffects.DashPassActive);
                hero.cState.dead = false;
                WearOnly(1);
                typeof(CharmSkills).GetMethod("Swarm", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] {hero});
                var ripple = (GameObject)typeof(CharmSkills).GetField("ripple", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                WearOnly(); CharmSkills.Tick(hero);
                CharmCheck("卸下蜂群终止拾取波", ripple != null && !ripple.activeSelf);
                CharmAuditReset();
                yield return ToDummy(1.4f);
                WearOnly(6, 32); mod.Stats.Life = mod.Stats.LifeMax;
                CharmEffects.Press(hero, 6);
                CharmEffects.DevQuickDealt = CharmEffects.DevQuickBase = 0;
                yield return SwingNail(1.2f, (h, d) => {});
                float ratio = CharmEffects.DevQuickDealt / Mathf.Max(1f, CharmEffects.DevQuickBase);
                // AfterNailHit stores pre-frenzy weapon damage: x2 frenzy and x2 guaranteed crit here are both required.
                CharmCheck("狂怒加快斩追加刀各结算一次增伤和暴击", CharmEffects.DevQuickBase > 0 && ratio > 3.2f && ratio < 4.8f, "ratio=" + ratio + " base=" + CharmEffects.DevQuickBase);
                EndNailTest();
            }
            if (phase == "all" || phase == "singles")
            {
                int totalPass = charmPass, totalAll = charmAll;
                string selected = Environment.GetEnvironmentVariable("TK_CHARM_CASES");
                for (int id = 3; id <= 40; id++)
                {
                    if (!string.IsNullOrEmpty(selected) && Array.IndexOf(selected.Split(','), id.ToString()) < 0) continue;
                    CharmAuditReset(); yield return null;
                    yield return CharmsTest(id);
                    totalPass += charmPass; totalAll += charmAll;
                }
                charmPass = totalPass; charmAll = totalAll;
            }
            if (phase == "final")
            {
                int matrixPass = charmPass, matrixAll = charmAll;
                Plugin.Log.LogInfo($"[AutoTest] 护符矩阵结果 {matrixPass}/{matrixAll}");
                yield return CharmExtraAudit();
                int extraPass = charmPass, extraAll = charmAll;
                int sumPass = matrixPass + extraPass, sumAll = matrixAll + extraAll;
                foreach (string entry in (Environment.GetEnvironmentVariable("TK_CHARM_CASES") ?? "4").Split(','))
                {
                    if (!int.TryParse(entry, out int id)) continue;
                    CharmAuditReset(); yield return null;
                    yield return CharmsTest(id);
                    sumPass += charmPass; sumAll += charmAll;
                }
                charmPass = sumPass; charmAll = sumAll;
            }
            CharmAuditReset(); Charms.DevAnywhere = false; Charms.DevSetPage(page);
            Plugin.Log.LogInfo($"[AutoTest] 护符全量结果 {charmPass}/{charmAll} phase={phase}");
        }
    }
}
