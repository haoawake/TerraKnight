using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suites "fx_&lt;name&gt;"): the 2026-10-16 batch of fixes — map reveal, the starting map, stalactites,
    /// autopause, mana stars, minions and Elder Baldur, shields, repeated knockback. "fx_shieldprobe" only
    /// records how the shielded enemies and the Elder Baldur block (their FSMs, invincibility over time, swings).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator Fix1016(string which)
        {
            if (which == "shieldprobe") yield return ShieldProbe();
            else if (which == "webprobe") yield return WebTrapProbe();
            else if (which == "listenscan") yield return ListenScan();
            else if (which == "platmix") { yield return PlatformMixTest(); Plugin.Log.LogInfo($"[AutoTest] 修复1016 合计 {fxOk}/{fxAll} {(fxOk == fxAll ? "通过" : "不通过")}"); }
            else if (which == "benchplat") { yield return BenchPlatformTest(); Plugin.Log.LogInfo($"[AutoTest] 修复1016 合计 {fxOk}/{fxAll} {(fxOk == fxAll ? "通过" : "不通过")}"); }
            else if (which == "meteormap") { yield return MeteorMapTest(); Plugin.Log.LogInfo($"[AutoTest] 修复1016 合计 {fxOk}/{fxAll} {(fxOk == fxAll ? "通过" : "不通过")}"); }
            else if (which == "fkdrops") yield return FalseKnightDropsTest();
            else if (which == "exitwall" || which.StartsWith("exitwall_")) { yield return ExitWallTest(which == "exitwall" ? null : which.Substring(9)); Plugin.Log.LogInfo($"[AutoTest] 修复1016 合计 {fxOk}/{fxAll} {(fxOk == fxAll ? "通过" : "不通过")}"); }
            else if (which == "projspecs") { var path = System.IO.Path.Combine(BepInEx.Paths.BepInExRootPath, "TerraKnight", "dev", "projspecs.txt"); System.IO.File.WriteAllText(path, Items.TerraProjectiles.DevSpecTable()); Plugin.Log.LogInfo($"[AutoTest] 修复1016 弹幕表写到 {path}"); FxCheck("弹幕表", true, path); }
            else if (which.StartsWith("17")) { yield return Fix1017(which.Substring(2)); Plugin.Log.LogInfo($"[AutoTest] 修复1016 合计 {fxOk}/{fxAll} {(fxOk == fxAll ? "通过" : "不通过")}"); }
            else if (which == "platdrop") { yield return PlatDropTest(); Plugin.Log.LogInfo($"[AutoTest] 修复1016 合计 {fxOk}/{fxAll} {(fxOk == fxAll ? "通过" : "不通过")}"); }
            else if (which.StartsWith("hitcadence")) { yield return HitCadenceTest(which.Length > 11 ? which.Substring(11) : null, false, 2f); Plugin.Log.LogInfo($"[AutoTest] 修复1016 合计 {fxOk}/{fxAll} {(fxOk == fxAll ? "通过" : "不通过")}"); }
            else if (which.StartsWith("magiccadence")) { yield return MagicCadenceTest(which.Length > 13 ? which.Substring(13) : null); Plugin.Log.LogInfo($"[AutoTest] 修复1016 合计 {fxOk}/{fxAll} {(fxOk == fxAll ? "通过" : "不通过")}"); }
            else if (which.StartsWith("ggstatuesold")) { World.BossRooms.DevNoGodhome = true; yield return GgStatuesTest(which.Length > 13 ? which.Substring(13) : null); World.BossRooms.DevNoGodhome = false; Plugin.Log.LogInfo($"[AutoTest] 修复1016 合计 {fxOk}/{fxAll} {(fxOk == fxAll ? "通过" : "不通过")}"); }
            else if (which.StartsWith("ggstatues")) { yield return GgStatuesTest(which.Length > 10 ? which.Substring(10) : null); Plugin.Log.LogInfo($"[AutoTest] 修复1016 合计 {fxOk}/{fxAll} {(fxOk == fxAll ? "通过" : "不通过")}"); }
            else if (which == "camjitter") { yield return CamJitterTest(); Plugin.Log.LogInfo($"[AutoTest] 修复1016 合计 {fxOk}/{fxAll} {(fxOk == fxAll ? "通过" : "不通过")}"); }
            else if (which.StartsWith("godhomeold_")) { World.BossRooms.DevNoGodhome = true; yield return GodhomeRoomsTest(which.Substring(11)); World.BossRooms.DevNoGodhome = false; Plugin.Log.LogInfo($"[AutoTest] 修复1016 合计 {fxOk}/{fxAll} {(fxOk == fxAll ? "通过" : "不通过")}"); }
            else if (which == "godhome" || which.StartsWith("godhome_")) { yield return GodhomeRoomsTest(which == "godhome" ? null : which.Substring(8)); Plugin.Log.LogInfo($"[AutoTest] 修复1016 合计 {fxOk}/{fxAll} {(fxOk == fxAll ? "通过" : "不通过")}"); }
            else if (which == "quirrelbench") { yield return QuirrelBenchTest(); Plugin.Log.LogInfo($"[AutoTest] 修复1016 合计 {fxOk}/{fxAll} {(fxOk == fxAll ? "通过" : "不通过")}"); }
            else if (which == "quirrelcity") { yield return QuirrelCityTest(); Plugin.Log.LogInfo($"[AutoTest] 修复1016 合计 {fxOk}/{fxAll} {(fxOk == fxAll ? "通过" : "不通过")}"); }
            else if (which == "saverprobe") { yield return SaverProbe(); Plugin.Log.LogInfo($"[AutoTest] 修复1016 合计 {fxOk}/{fxAll} {(fxOk == fxAll ? "通过" : "不通过")}"); }
            else if (which == "fkgates") { yield return FkGatesTest(); Plugin.Log.LogInfo($"[AutoTest] 修复1016 合计 {fxOk}/{fxAll} {(fxOk == fxAll ? "通过" : "不通过")}"); }
            else if (which == "ggdoor") { yield return GodhomeDoorTest(); Plugin.Log.LogInfo($"[AutoTest] 修复1016 合计 {fxOk}/{fxAll} {(fxOk == fxAll ? "通过" : "不通过")}"); }
            else if (which == "props") { yield return PropsRootTest(); Plugin.Log.LogInfo($"[AutoTest] 修复1016 合计 {fxOk}/{fxAll} {(fxOk == fxAll ? "通过" : "不通过")}"); }
            else if (which == "radiance" || which == "radiancefall") { yield return RadianceBlowTest(); Plugin.Log.LogInfo($"[AutoTest] 修复1016 合计 {fxOk}/{fxAll} {(fxOk == fxAll ? "通过" : "不通过")}"); }
            else if (which.StartsWith("fsm_")) yield return SceneFsmDump(which.Substring(4));
            else if (which == "web") { yield return WebTrapProbe(false); Plugin.Log.LogInfo($"[AutoTest] 修复1016 合计 {fxOk}/{fxAll} {(fxOk == fxAll ? "通过" : "不通过")}"); }
            else if (which == "all" || which == "map" || which == "pause" || which == "trap" || which == "guard" || which == "stars" || which == "knock" || which == "reflect" || which == "time" || which == "candle" || which == "meteoronce" || which == "maskreturn") yield return Fix1016Checks(which);
            else Plugin.Log.LogWarning($"[AutoTest] 修复1016 没有这一项：{which}");
        }

        static List<HealthManager> EnemiesNamed(string prefix)
        {
            var list = new List<HealthManager>();
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                if (hm != null && !hm.isDead && hm.gameObject.activeInHierarchy && hm.name.StartsWith(prefix)) list.Add(hm);
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            return list;
        }

        static readonly HarmonyLib.AccessTools.FieldRef<HealthManager, bool> hmInvincible = HarmonyLib.AccessTools.FieldRefAccess<HealthManager, bool>("invincible");
        static readonly HarmonyLib.AccessTools.FieldRef<HealthManager, int> hmFromDir = HarmonyLib.AccessTools.FieldRefAccess<HealthManager, int>("invincibleFromDirection");

        static string Guard(HealthManager hm)
        {
            var fsms = new System.Text.StringBuilder();
            foreach (var f in hm.GetComponents<PlayMakerFSM>()) fsms.Append(f.FsmName).Append('=').Append(f.ActiveStateName).Append(' ');
            return $"血 {hm.hp} 无敌 {hmInvincible(hm)} 方向 {hmFromDir(hm)} 朝 {(hm.transform.localScale.x > 0 ? "+" : "-")} {fsms}";
        }

        IEnumerator ShieldProbe()
        {
            HitTrace.On = true;
            foreach (var (scene, prefix) in new[] { ("Crossroads_15", "Zombie Shield"), ("Crossroads_11_alt", "Blocker") })
            {
                yield return GoTo(scene, EntryGateOf(scene));
                PlayerData.instance.isInvincible = true;
                var hero = HeroController.instance;
                var found = EnemiesNamed(prefix);
                Plugin.Log.LogInfo($"[AutoTest] 修复1016 盾 {scene} 找到 {found.Count} 个 {prefix}");
                if (found.Count == 0) continue;
                var hm = found[0];
                Logged(() => DumpFsms(hm.transform));
                var inv = mod.Inventory;
                for (int side = -1; side <= 1; side += 2)
                {
                    var c = (Vector2)hm.GetComponent<Collider2D>().bounds.center;
                    hero.transform.position = new Vector3(c.x + side * 2.6f, c.y + 0.2f, hero.transform.position.z);
                    hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                    if (side < 0) hero.FaceRight(); else hero.FaceLeft();
                    for (int i = 0; i < 8; i++)
                    {
                        PlayerData.instance.isInvincible = true;
                        Plugin.Log.LogInfo($"[AutoTest] 修复1016 盾 {prefix} 主角在{(side < 0 ? "左" : "右")} {i * 0.25f:F2}s：{Guard(hm)}");
                        yield return new WaitForSeconds(0.25f);
                    }
                    inv.Slots[0] = ItemStack.Of(ItemType.BreakerBlade);
                    inv.Selected = 0;
                    inv.Override = -1;
                    int hp0 = hm.hp;
                    for (int k = 0; k < 6 && !hm.isDead; k++)
                    {
                        c = hm.GetComponent<Collider2D>().bounds.center;
                        hero.transform.position = new Vector3(c.x + side * 1.4f, hero.transform.position.y, hero.transform.position.z);
                        float until = Time.time + 2f;
                        while (!mod.DevReady && Time.time < until) yield return null;
                        int before = hm.hp;
                        string guard = Guard(hm);
                        mod.DevUseSelected(c);
                        yield return new WaitForSeconds(0.45f);
                        Plugin.Log.LogInfo($"[AutoTest] 修复1016 盾 {prefix} 从{(side < 0 ? "左" : "右")}砍第{k + 1}下：挥前 {guard}，掉血 {before - hm.hp}");
                    }
                    Shot(scene, $"fx_shield_{prefix.Replace(' ', '_')}_{(side < 0 ? "L" : "R")}");
                    Plugin.Log.LogInfo($"[AutoTest] 修复1016 盾 {prefix} 从{(side < 0 ? "左" : "右")}共掉血 {hp0 - hm.hp}");
                    yield return new WaitForSeconds(1f);
                }
                if (prefix == "Blocker" && !hm.isDead)
                {
                    // The finch: summoned beside the player some way off, then left to pick its target.
                    var c = (Vector2)hm.GetComponent<Collider2D>().bounds.center;
                    hero.transform.position = new Vector3(c.x - 6f, c.y + 0.5f, hero.transform.position.z);
                    yield return new WaitForSeconds(0.5f);
                    inv.Slots[1] = ItemStack.Of(ItemType.FinchStaff);
                    inv.Selected = 1;
                    float until = Time.time + 2f;
                    while (!mod.DevReady && Time.time < until) yield return null;
                    mod.DevUseSelected((Vector2)hero.transform.position + Vector2.up);
                    int hp0 = hm.hp;
                    for (int i = 0; i < 24; i++)
                    {
                        PlayerData.instance.isInvincible = true;
                        if (i % 4 == 0) Plugin.Log.LogInfo($"[AutoTest] 修复1016 雀 {i * 0.25f:F1}s：{Minions.Instance?.DevDescribe()}，巴德尔 {Guard(hm)} 在 {(Vector2)hm.transform.position:F1}；{Minions.Instance?.DevWhyNot(hm, Minions.Kind.Finch)}");
                        yield return new WaitForSeconds(0.25f);
                    }
                    Plugin.Log.LogInfo($"[AutoTest] 修复1016 雀 共让巴德尔掉血 {hp0 - hm.hp}");
                    Shot(scene, "fx_finch_baldur");
                }
            }
        }
    }

    /// <summary>Dev: while on, every HealthManager.Hit is logged with its direction, its attack type and whether the target blocks it.</summary>
    [HarmonyLib.HarmonyPatch]
    internal static class HitTrace
    {
        public static bool On;

        [HarmonyLib.HarmonyPrefix]
        [HarmonyLib.HarmonyPatch(typeof(HealthManager), nameof(HealthManager.Hit))]
        static void Before(HealthManager __instance, HitInstance hitInstance)
        {
            if (!On || __instance == null) return;
            int card = DirectionUtils.GetCardinalDirection(hitInstance.GetActualDirection(__instance.transform));
            bool blocks = __instance.IsBlockingByDirection(card, hitInstance.AttackType);
            Plugin.Log.LogInfo($"[AutoTest] 修复1016 命中 {__instance.name} 方向 {hitInstance.Direction:F0}->{card} 类型 {hitInstance.AttackType} 伤害 {hitInstance.DamageDealt} 挡 {blocks} 无敌 {__instance.IsInvincible}/{__instance.InvincibleFromDirection} 标签 {__instance.tag} 来源 {(hitInstance.Source != null ? hitInstance.Source.name : "无")} 闪避 {hitInstance.IgnoreInvulnerable}");
        }
    }
}
