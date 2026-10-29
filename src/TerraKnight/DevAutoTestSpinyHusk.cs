using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "spinyhusk", the user's 10-28 "多刺躯壳死了还在放刺"): a Spiny Husk (GardenZombie) in each enemy AI (TK_SPINY_AI:
    /// vanilla, new or both, TK_SPINY_SCENE the room) struck down; for eight seconds after its death every spike-like object
    /// that shows up (name with Spike/Thorn/Spine/Barb, active), the new AI's shots, and its corpse's scripts are written down.
    /// Lines "[AutoTest] 多刺躯壳 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator SpinyHuskTest()
        {
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            string which = System.Environment.GetEnvironmentVariable("TK_SPINY_AI") ?? "both";
            string room = System.Environment.GetEnvironmentVariable("TK_SPINY_SCENE") ?? "Fungus3_10";
            var modes = new List<World.EnemyAiKind>();
            if (which != "new") modes.Add(World.EnemyAiKind.Vanilla);
            if (which != "vanilla") modes.Add(World.EnemyAiKind.New);
            var keepMode = World.EnemyAiMode.Current;
            int ok = 0, all = 0;
            bool Spiky(string n) => n.Contains("Spike") || n.Contains("Thorn") || n.Contains("Spine") || n.Contains("Barb") || n.Contains("spike") || n.Contains("thorn");
            foreach (var mode in modes)
            {
                World.EnemyAiMode.Current = mode;
                string tag = mode == World.EnemyAiKind.New ? "新版" : "原版";
                yield return GoTo("Town", "left1");
                yield return new WaitForSeconds(1f);
                yield return GoTo(room, EntryGateOf(room));
                yield return new WaitForSeconds(2f);
                var hero = HeroController.instance;
                var husks = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                    .Where(h => h != null && !h.isDead && h.gameObject.activeInHierarchy && UI.EnemyHover.RecordOf(h) == "GardenZombie")
                    .OrderBy(h => Vector2.Distance(h.transform.position, hero.transform.position)).ToList();
                all++;
                if (husks.Count == 0) { Plugin.Log.LogInfo($"[AutoTest] 多刺躯壳 {tag}：{room} 里没有多刺躯壳 不通过"); continue; }
                var hm = husks.FirstOrDefault(h => h.GetComponents<PlayMakerFSM>().All(f => f.ActiveStateName != "Spawn Idle")) ?? husks[0];
                if (mode == modes[0]) DumpFsms(hm.transform);
                Plugin.Log.LogInfo($"[AutoTest] 多刺躯壳 {tag}：全部 {string.Join("；", husks.Select(h => $"{h.name}@{(Vector2)h.transform.position:F1} {string.Join(",", h.GetComponents<PlayMakerFSM>().Select(f => f.FsmName + ":" + f.ActiveStateName))} 碰撞 {h.GetComponent<Collider2D>()?.enabled}"))}");
                var col = hm.GetComponent<Collider2D>();
                Plugin.Log.LogInfo($"[AutoTest] 多刺躯壳 {tag}：{room} 有 {husks.Count} 只，打 {hm.name} 在 {(Vector2)hm.transform.position:F1}，脚本 {string.Join("；", hm.GetComponentsInChildren<PlayMakerFSM>(true).Select(f => f.FsmName + "@" + f.ActiveStateName))}");
                hero.transform.position = Clear(hero, (Vector2)hm.transform.position + new Vector2(-4f, 0.5f));
                // Beside it for ten seconds untouched: whether it gets up, and every spike while it lies or stands.
                var before = new HashSet<int>();
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                    if (t != null && Spiky(t.name)) before.Add(t.gameObject.GetInstanceID());
                var lyingStates = new Dictionary<PlayMakerFSM, string>();
                int beforeShots = EnemyAi.EnemyAi.DevShots;
                var diedAt = (Vector2)hm.transform.position;
                var body = hm.gameObject;
                // In the watch it may die by itself (lunging into the garden's thorns): then that death is the one watched.
                for (float w0 = Time.time; Time.time - w0 < 10f && hm != null && !hm.isDead;)
                {
                    diedAt = hm.transform.position;
                    foreach (var f in hm.GetComponentsInChildren<PlayMakerFSM>(true))
                    {
                        string now = f.ActiveStateName;
                        if (lyingStates.TryGetValue(f, out var was) && was == now) continue;
                        lyingStates[f] = now;
                        Plugin.Log.LogInfo($"[AutoTest] 多刺躯壳 {tag} 站旁边 {Time.time - w0:F2}s {f.FsmName}：{was} → {now}，HK 血量 {hm.hp} 无敌 {hm.IsInvincible}");
                    }
                    foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                    {
                        if (t == null || !t.gameObject.activeInHierarchy || !Spiky(t.name) || !before.Add(t.gameObject.GetInstanceID())) continue;
                        if (Vector2.Distance(t.position, hm.transform.position) > 25f) continue;
                        Plugin.Log.LogInfo($"[AutoTest] 多刺躯壳 {tag} 站旁边 {Time.time - w0:F2}s 出现 {PathOf(t)} 在 {(Vector2)t.position:F1}");
                    }
                    yield return null;
                }
                Plugin.Log.LogInfo($"[AutoTest] 多刺躯壳 {tag} 站旁边 10 秒：新版 AI 射击 {EnemyAi.EnemyAi.DevShots - beforeShots} 次");
                // Struck down from beside it.
                if (hm != null && !hm.isDead)
                {
                    var info = EnemyStats.Of(hm);
                    Plugin.Log.LogInfo($"[AutoTest] 多刺躯壳 {tag}：打之前 生命 {EnemyStats.Life(hm, info)}/{EnemyStats.LifeMax(info)}，碰撞 {col?.enabled}");
                    diedAt = hm.transform.position;
                    hm.hp = 1;
                    if (col != null && col.enabled) mod.Melee.HitWith(col, 9999, 0f, 0f, 1f, (Vector2)col.bounds.center + Vector2.left);
                    yield return new WaitForSeconds(0.3f);
                }
                else Plugin.Log.LogInfo($"[AutoTest] 多刺躯壳 {tag}：观察时自己死了");
                if (hm != null && !hm.isDead) Plugin.Log.LogInfo($"[AutoTest] 多刺躯壳 {tag}：一击没死（挡下 {MeleeHits.DevBlocked}），直接 Die");
                if (hm != null && !hm.isDead) HarmonyLib.Traverse.Create(hm).Method("Die", new[] { typeof(float?), typeof(AttackTypes), typeof(bool) }).GetValue(null, AttackTypes.Generic, true);
                if (hm != null && !hm.isDead) { Plugin.Log.LogInfo($"[AutoTest] 多刺躯壳 {tag}：打不死 不通过"); continue; }
                string bodyNow = body == null ? "已销毁" : body.activeInHierarchy ? "还开着" : "关了";
                Plugin.Log.LogInfo($"[AutoTest] 多刺躯壳 {tag}：死了，身体 {bodyNow}");
                int shots0 = EnemyAi.EnemyAi.DevShots;
                var seen = new HashSet<int>();
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                    if (t != null && Spiky(t.name)) seen.Add(t.gameObject.GetInstanceID());
                int late = 0, early = 0;
                float t0 = Time.time;
                var corpseStates = new Dictionary<PlayMakerFSM, string>();
                while (Time.time - t0 < 8f)
                {
                    foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                    {
                        if (t == null || !t.gameObject.activeInHierarchy || !Spiky(t.name)) continue;
                        if (Vector2.Distance(t.position, diedAt) > 25f) continue;
                        int id = t.gameObject.GetInstanceID();
                        if (!seen.Add(id)) continue;
                        float at = Time.time - t0;
                        if (at > 0.5f) late++; else early++;
                        Plugin.Log.LogInfo($"[AutoTest] 多刺躯壳 {tag} 死后 {at:F2}s 出现 {PathOf(t)} 在 {(Vector2)t.position:F1}");
                    }
                    foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                    {
                        if (f == null || Vector2.Distance(f.transform.position, diedAt) > 6f) continue;
                        string now = f.ActiveStateName;
                        if (corpseStates.TryGetValue(f, out var was) && was == now) continue;
                        corpseStates[f] = now;
                        Plugin.Log.LogInfo($"[AutoTest] 多刺躯壳 {tag} 死后 {Time.time - t0:F2}s {PathOf(f.transform)}/{f.FsmName}：{was} → {now}");
                    }
                    yield return null;
                }
                // Its corpse: every child with its components and every script; whether a brain was put on it.
                int brained = 0;
                foreach (var c in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                {
                    if (c == null || !c.name.StartsWith("Corpse Garden Zombie") || c.parent != null && c.parent.name.StartsWith("Corpse Garden Zombie")) continue;
                    if (Vector2.Distance(c.position, diedAt) > 30f) continue;
                    if (c.GetComponent<EnemyAi.Brain>() != null) brained++;
                    foreach (var k in c.GetComponentsInChildren<Transform>(true))
                        Plugin.Log.LogInfo($"[AutoTest] 多刺躯壳 {tag} 尸体 {PathOf(k)} 开着 {k.gameObject.activeInHierarchy} 层 {k.gameObject.layer} 在 {(Vector2)k.position:F1}：{string.Join(",", k.GetComponents<Component>().Select(x => x.GetType().Name + (x is Behaviour bh ? (bh.enabled ? "" : "(关)") : x is Collider2D cc ? (cc.enabled ? "" : "(关)") : "")))}");
                    if (tag == "原版") DumpFsms(c);
                }
                int shots = EnemyAi.EnemyAi.DevShots - shots0;
                bool pass = late == 0 && shots == 0 && brained == 0;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 多刺躯壳 {tag}：死后 0.5 秒内出现刺 {early} 根，之后 {late} 根，新版 AI 射击 {shots} 次，尸体挂了新版 AI {brained} 个，尸体落地没炸刺 {SpinyCorpse.DevQuiet} 次 {(pass ? "通过" : "不通过")}");
            }
            World.EnemyAiMode.Current = keepMode;
            Plugin.Log.LogInfo($"[AutoTest] 多刺躯壳 合计 {ok}/{all}");
        }
    }
}
