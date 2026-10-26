using System;
using System.Collections;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "minionphase"): bosses whose phases wait on a health check (TOOK DAMAGE, then GetHP against a line) go on
    /// to their next phase when only minions strike them: a minion's blow above the line tells the check nothing, the one
    /// that takes the health under it moves the phase on. TK_PHASE_BOSSES picks the bosses (';').
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator MinionPhaseTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            int pass = 0, all = 0;
            void Check(string name, bool ok, string detail = "")
            {
                all++; if (ok) pass++;
                Plugin.Log.LogInfo($"[AutoTest] 召唤物阶段 {name} {(ok ? "通过" : "不通过")} {detail}");
            }
            string pick = Environment.GetEnvironmentVariable("TK_PHASE_BOSSES");
            var keys = string.IsNullOrWhiteSpace(pick) ? new[] { "InfectedKnight", "JarCollector", "HiveKnight", "GhostMarkoth", "GhostXero", "DungDefender" } : pick.Split(';');
            foreach (var key in keys.Where(k => k.Length > 0))
            {
                PlayerData.instance.isInvincible = false;
                if (!BossArena.Has(key) || !BossArena.Fight(key, true)) { Check(key + " 开打", false, "没有竞技场"); continue; }
                HealthManager boss = null;
                float until = Time.realtimeSinceStartup + 60f;
                while (Time.realtimeSinceStartup < until)
                {
                    if (!GameManager.instance.IsInSceneTransition)
                        boss = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                            .FirstOrDefault(h => !h.isDead && h.gameObject.activeInHierarchy && EnemyStats.Of(h)?.Record == key.Split('@')[0] && EnemyStats.Of(h).Boss);
                    if (boss != null) break;
                    yield return null;
                }
                if (boss == null) { Check(key + " 找到首领", false); continue; }
                var hero = HeroController.instance;
                // Woken by a first blow, its entrance played out.
                BossWake.Rouse(boss);
                yield return new WaitForSeconds(6f);
                // A check in the fight's own machine is only reachable from its idle: waited for (the boss woken again if it sleeps).
                var (fsm, line) = MinionQuiet.DevNextLine(boss);
                for (float wait = Time.time + 25f; fsm == null && Time.time < wait && !boss.isDead;)
                {
                    if (BossWake.Asleep(boss)) BossWake.Rouse(boss);
                    yield return null;
                    (fsm, line) = MinionQuiet.DevNextLine(boss);
                }
                if (fsm == null) { Check(key + " 有等血量检查的状态机", false, string.Join("、", boss.GetComponents<PlayMakerFSM>().Select(f => f.FsmName + ":" + f.ActiveStateName))); continue; }
                string waiting = fsm.ActiveStateName;
                var col = boss.GetComponents<Collider2D>().FirstOrDefault(c => c.enabled) ?? boss.GetComponentInChildren<Collider2D>();
                Vector2 from = hero.GetComponent<BoxCollider2D>().bounds.center;
                // Above the line: the check hears nothing.
                boss.hp = line + 400;
                int told0 = MinionQuiet.DevPhaseTold;
                mod.Melee.HitWith(col, 30, 0f, 0f, 1f, from, autonomous: true);
                yield return new WaitForSeconds(0.6f);
                Check($"{key} 线上的一击不惊动 {fsm.FsmName}", fsm.ActiveStateName == waiting && MinionQuiet.DevPhaseTold == told0, $"{waiting} -> {fsm.ActiveStateName}，血 {boss.hp}，线 {line}");
                // Just above it, then a blow that takes it under.
                int tries = 0;
                boss.hp = line + 2;
                while (boss.hp >= line && tries++ < 10 && !boss.isDead)
                {
                    mod.Melee.HitWith(col, 60, 0f, 0f, 1f, from, autonomous: true);
                    yield return new WaitForSeconds(0.2f);
                }
                yield return new WaitForSeconds(1f);
                Check($"{key} 召唤物打过线后 {fsm.FsmName} 换阶段", fsm.ActiveStateName != waiting && MinionQuiet.DevPhaseTold > told0,
                    $"{waiting} -> {fsm.ActiveStateName}，血 {boss.hp}，线 {line}，告诉了 {MinionQuiet.DevPhaseTold - told0} 次");
                boss.hp = 1;
                yield return new WaitForSeconds(0.5f);
            }
            Player.TerraStats.DevImmortal = false;
            mod.DevNoFallDamage = false;
            Plugin.Log.LogInfo($"[AutoTest] 召唤物阶段 合计 {pass}/{all} {(pass == all ? "通过" : "不通过")}");
        }
    }
}
