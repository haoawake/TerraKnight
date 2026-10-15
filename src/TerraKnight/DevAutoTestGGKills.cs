using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "ggkills", "ggkills_A+B" for some): every dream and Godhome boss fought through its arena's way in
    /// (BossArena, as a seal takes the player): struck down by the mod's hits until the game has it beaten, then its
    /// kill in the book (its own record, the dreams' record@5) and its drops given (the fight sends the player away).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator GGKillsTest(string[] only)
        {
            int ok = 0, all = 0;
            var bad = new List<string>();
            mod.DevNoFallDamage = true;
            PlayerData.instance.hasDreamNail = true;
            foreach (var s in BossSummons.All)
            {
                if (BossArena.DevWorldArena(s.Key, out _, out _)) continue;
                if (only != null && System.Array.IndexOf(only, s.Key) < 0) continue;
                all++;
                int kills0 = Bestiary.KillsOf(s.Key), given0 = EnemyDrops.DevGiven, dropped0 = EnemyDrops.DevDropped;
                yield return SealTravel("Town", "left1");
                PlayerData.instance.isInvincible = true;
                if (!BossArena.Fight(s.Key)) { bad.Add($"{s.Key}：进不去"); continue; }
                float until = Time.realtimeSinceStartup + 30f;
                while (!BossArena.Arrived(s.Key) && Time.realtimeSinceStartup < until) yield return null;
                yield return new WaitForSeconds(3f);
                PlayerData.instance.isInvincible = true;
                string scene = GameManager.instance.sceneName;
                // Struck down, each boss of the fight, until the book has the kill or a minute is up.
                float stop = Time.realtimeSinceStartup + 25f, nextLog = 0f;
                int strikes = 0, landed = 0;
                while (Bestiary.KillsOf(s.Key) == kills0 && Time.realtimeSinceStartup < stop)
                {
                    var hero = HeroController.SilentInstance;
                    foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                    {
                        if (hm == null || hm.isDead || hm.hp <= 0 || !hm.gameObject.activeInHierarchy) continue;
                        // Everything of the fight that takes hits (a boss's own parts too: the False Knight's head).
                        string room = hm.gameObject.scene.name, now = GameManager.instance.sceneName;
                        if (room != now && !room.StartsWith(now)) continue;
                        var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
                        if (col == null || !col.enabled) continue;
                        if (mod.Melee.HitWith(col, 99999, 0f, 0f, 1f, col.bounds.center)) landed++;
                        strikes++;
                    }
                    PlayerData.instance.isInvincible = true;
                    if (Time.realtimeSinceStartup >= nextLog)
                    {
                        nextLog = Time.realtimeSinceStartup + 3f;
                        var sb = new System.Text.StringBuilder();
                        foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                        {
                            if (hm == null || !hm.gameObject.scene.name.StartsWith(GameManager.instance.sceneName.Split('_')[0])) continue;
                            sb.Append($"{hm.name}[{UI.EnemyHover.RecordOf(hm)}] hp {hm.hp} 死{(hm.isDead ? 1 : 0)} 显{(hm.gameObject.activeInHierarchy ? 1 : 0)} 无敌{(hm.IsInvincible ? 1 : 0)} 记{(Bestiary.Credited(hm) ? 1 : 0)}；");
                        }
                        Plugin.Log.LogInfo($"[AutoTest] 梦境神居首领 {s.Key} 进行中：命中 {landed}/{strikes}，{sb}");
                    }
                    yield return new WaitForSeconds(0.25f);
                }
                // A dream boss that wakes only as the player walks up (and its fight the game's own): its win as its FSM
                // writes it, the beaten flag (SpecialDeaths), which is what counts the kill.
                string how = "打死";
                var flag = s.Key == "FalseKnight@5" ? "falseKnightDreamDefeated" : s.Key == "MageLord@5" ? "mageLordDreamDefeated" : s.Key == "InfectedKnight@5" ? "infectedKnightDreamDefeated"
                    // The ones that wake only to a player walking up, or whose fight runs its own sequence: their journal kill.
                    : s.Key == "WhiteDefender" || s.Key == "GreyPrince" || s.Key == "NightmareGrimm" || s.Key == "FinalBoss" || s.Key == "LobsterLancer" ? "killed" + s.Key : null;
                if (Bestiary.KillsOf(s.Key) == kills0 && flag != null && GameManager.instance.sceneName == scene)
                {
                    PlayerData.instance.SetBool(flag, false);
                    PlayerData.instance.SetBool(flag, true);
                    how = "击败标志 " + flag;
                }
                yield return new WaitForSeconds(4f);
                int kills = Bestiary.KillsOf(s.Key) - kills0, given = EnemyDrops.DevGiven - given0 + EnemyDrops.DevDropped - dropped0;
                bool pass = kills == 1 && given > 0;
                if (pass) ok++;
                else bad.Add($"{s.Key}（{scene}）：击杀 {kills}，掉落 {given}，出手 {strikes} 次，现在 {GameManager.instance.sceneName}");
                Plugin.Log.LogInfo($"[AutoTest] 梦境神居首领 {s.Key}（{scene}）：{how}，图鉴击杀 +{kills}，掉落 {given} 件，出手 {strikes} 次 {(pass ? "通过" : "不通过")}");
                yield return new WaitForSeconds(3f);
            }
            yield return SealTravel("Town", "left1");
            foreach (var b in bad) Plugin.Log.LogInfo($"[AutoTest] 梦境神居首领 没记上：{b}");
            mod.DevNoFallDamage = false;
            Plugin.Log.LogInfo($"[AutoTest] 梦境神居首领 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
