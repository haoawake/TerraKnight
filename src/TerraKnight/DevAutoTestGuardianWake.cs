using System.Collections;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "guardianwake"): the Crystal Guardian asleep on its bench, shot from the far end of its room (outside
    /// its Hero Range): it wakes (its Wake and roar), the waking blow takes one point, and the blows after its roar count.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator GuardianWakeTest()
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail = "")
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 水晶守卫叫醒 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            mod.DevNoFallDamage = true;
            yield return SealTravel("Town", "left1");
            PlayerData.instance.isInvincible = true;
            if (!BossArena.Fight("MegaBeamMiner")) { Check("进场", false); yield break; }
            float until = Time.realtimeSinceStartup + 30f;
            while (!BossArena.Arrived("MegaBeamMiner") && Time.realtimeSinceStartup < until) yield return null;
            yield return new WaitForSeconds(2f);
            HealthManager guard = null;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                if (UI.EnemyHover.RecordOf(hm) == "MegaBeamMiner" && !hm.isDead) guard = hm;
            if (guard == null) { Check("找到守卫", false, GameManager.instance.sceneName); yield break; }
            var fsm = PlayMakerFSM.FindFsmOnGameObject(guard.gameObject, "Beam Miner");
            var col = guard.GetComponent<Collider2D>();
            var hero = HeroController.instance;
            Vector2 at = col.bounds.center;
            // As far from it as the arena lets a blow land (inside the battle's gates), left of its Hero Range (x 19.4 to 40.5).
            float x = Mathf.Min(at.x - 22f, 8f);
            for (; x < 19f; x += 0.5f)
            {
                hero.transform.position = Clear(hero, new Vector2(x, at.y));
                yield return null;
                if (!World.ArenaGuard.Outside(guard)) break;
            }
            yield return new WaitForSeconds(1f);
            string s0 = fsm != null ? fsm.ActiveStateName : "无";
            int hp0 = guard.hp;
            Vector2 from = hero.GetComponent<BoxCollider2D>().bounds.center;
            mod.Melee.HitWith(col, 30, 0f, 0f, 1f, from);
            var seen = new System.Collections.Generic.List<string> { s0 };
            for (float t = 0f; t < 4f; t += Time.deltaTime)
            {
                PlayerData.instance.isInvincible = true;
                yield return null;
                string now = fsm != null ? fsm.ActiveStateName : "无";
                if (seen[seen.Count - 1] != now) seen.Add(now);
            }
            bool woke = seen.Exists(x => x == "Wake") && fsm.ActiveStateName != "Sleep";
            Check("远处一枪叫醒", s0 == "Sleep" && woke, $"主角 x {from.x:F1}，守卫 x {at.x:F1}，状态 {string.Join(">", seen)}，血 {hp0}→{guard.hp}");
            // After its waking (its First Idle): blows count, its crystal-rain roar too.
            // Into the arena the gates have shut (the player walks in to fight it).
            hero.transform.position = Clear(hero, new Vector2(at.x - 8f, at.y));
            yield return new WaitForSeconds(0.5f);
            int hp1 = guard.hp, held0 = BossWake.DevHeld, refused0 = World.ArenaGuard.DevBlocked;
            var timeline = new System.Collections.Generic.List<string>();
            string lastMark = "";
            for (float t = 0f; t < 6f; t += 0.4f)
            {
                PlayerData.instance.isInvincible = true;
                int before = guard.hp;
                mod.Melee.HitWith(col, 30, 0f, 0f, 1f, (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center);
                yield return new WaitForSeconds(0.4f);
                string mark = $"{fsm.ActiveStateName}{(guard.IsInvincible ? "(无敌)" : "")}{(guard.hp < before ? "-" + (before - guard.hp) : "")}";
                if (mark != lastMark) timeline.Add(mark);
                lastMark = mark;
            }
            Check("醒后打得动", guard.hp < hp1 - 5, $"血 {hp1}→{guard.hp}，被挡 {BossWake.DevHeld - held0} 下，场外拦下 {World.ArenaGuard.DevBlocked - refused0} 下，醒完 {BossWake.DevAwake}，{string.Join(" ", timeline)}");
            PlayerData.instance.isInvincible = false;
            yield return SealTravel("Town", "left1");
            Plugin.Log.LogInfo($"[AutoTest] 水晶守卫叫醒 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
