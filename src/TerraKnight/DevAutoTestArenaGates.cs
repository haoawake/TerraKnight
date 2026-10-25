using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "arenagates"): every world arena's rematch as a seal brings it (BossArena.Fight, a boss beaten before),
    /// and a few seconds into the fight each battle gate of its room (BG Control): its state and whether it walls the way.
    /// A gate left open while the boss fights is a way out. TK_GATES_KEYS (a;b) picks some.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator ArenaGatesTest()
        {
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            var only = (System.Environment.GetEnvironmentVariable("TK_GATES_KEYS") ?? "").Split(';').Where(s => s.Length > 0).ToArray();
            int open = 0, arenas = 0;
            foreach (var key in BossArena.DevKeys().ToArray())
            {
                if (only.Length > 0 && !only.Contains(key)) continue;
                if (!BossArena.DevWorldArena(key, out _, out _)) continue;
                string scene = BossArena.SceneOf(key);
                Bestiary.Kills[key] = Mathf.Max(1, Bestiary.KillsOf(key));
                int started = BossSummonStart.DevStarted;
                if (!BossArena.Fight(key, true)) { Plugin.Log.LogInfo($"[ARENA_GATES] {key} 进不去"); continue; }
                float until = Time.realtimeSinceStartup + 25f;
                while (!BossArena.Arrived(key) && Time.realtimeSinceStartup < until) yield return null;
                until = Time.realtimeSinceStartup + 15f;
                while (BossSummonStart.DevStarted == started && Time.realtimeSinceStartup < until) yield return null;
                bool fight = BossSummonStart.DevStarted > started;
                yield return new WaitForSeconds(4f);
                arenas++;
                var gates = new List<string>();
                bool anyOpen = false;
                foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (f == null || f.FsmName != "BG Control" || !f.gameObject.scene.name.StartsWith(scene)) continue;
                    var c = f.GetComponent<Collider2D>();
                    bool walls = f.gameObject.activeInHierarchy && c != null && c.enabled;
                    string st = f.gameObject.activeInHierarchy ? f.ActiveStateName : "(off)";
                    if (!walls) anyOpen = true;
                    gates.Add($"{f.gameObject.name}@{f.transform.position.x:F0},{f.transform.position.y:F0}:{st}{(walls ? "" : " 开着")}");
                }
                if (anyOpen) open++;
                Plugin.Log.LogInfo($"[ARENA_GATES] {key} 场景 {GameManager.instance.sceneName} 开战 {fight} 门 {gates.Count}：{string.Join("；", gates)}{(anyOpen ? "  ← 有门开着" : "")}");
            }
            Plugin.Log.LogInfo($"[ARENA_GATES] 完成：{arenas} 个竞技场，{open} 个开战后有门开着");
        }
    }
}
