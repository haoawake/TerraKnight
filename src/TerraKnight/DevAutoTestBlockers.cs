using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TerraKnight.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight
{
    /// <summary>
    /// Dev: the game's enemy blockers (World/EnemyBlockers). Suite "blockers": every gameplay
    /// scene loaded on its own beside the one played, each solid collider on the blocker layer
    /// listed (name, place, the gate nearest it, the enemies round it, what was done with it),
    /// the scene unloaded. Suite "blockfix": Cliffs_02's hidden passage, where the player found
    /// crawlers stuck and flyers stopping: the player inside it, the enemies outside watched,
    /// with the blockers on and then off.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly string[] skipScenePrefixes =
        {
            "Menu", "Cinematic", "Opening", "End_", "PermaDeath", "Quit", "BetaEnd", "Knight_Pickup", "Pre_Menu", "Cutscene",
            "Intro", "Prologue", "Demo", "Credits", "GG_", "Room_Final", "Dream_Final", "Abyss_Lighthouse", "Tutorial_01",
        };

        IEnumerator BlockerSurvey()
        {
            PlayerData.instance.isInvincible = true;
            var names = new List<string>();
            for (int i = 0; i < USM.sceneCountInBuildSettings; i++)
            {
                string name = Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
                bool skip = string.IsNullOrEmpty(name) || !name.Contains("_") && name != "Town";
                foreach (var p in skipScenePrefixes) if (name.StartsWith(p)) skip = true;
                if (!skip) names.Add(name);
            }
            Plugin.Log.LogInfo($"[AutoTest] 挡怪 要看 {names.Count} 个场景");
            float until = Time.realtimeSinceStartup + 720f;
            int scenes = 0, total = 0, off = 0, kept = 0;
            foreach (var name in names)
            {
                if (Time.realtimeSinceStartup > until)
                {
                    Plugin.Log.LogInfo($"[AutoTest] 挡怪 时间到，停在 {name}");
                    break;
                }
                if (USM.GetSceneByName(name).isLoaded) continue;
                var load = USM.LoadSceneAsync(name, LoadSceneMode.Additive);
                if (load == null) continue;
                while (!load.isDone) yield return null;
                yield return null;
                PlayerData.instance.isInvincible = true;
                var scene = USM.GetSceneByName(name);
                scenes++;
                var gates = new List<TransitionPoint>();
                var enemies = new List<HealthManager>();
                var found = new List<Collider2D>();
                foreach (var root in scene.GetRootGameObjects())
                {
                    gates.AddRange(root.GetComponentsInChildren<TransitionPoint>(true));
                    enemies.AddRange(root.GetComponentsInChildren<HealthManager>(true));
                    foreach (var c in root.GetComponentsInChildren<Collider2D>(true))
                        if (!c.isTrigger && c.gameObject.layer == EnemyBlockers.Layer) found.Add(c);
                }
                foreach (var c in found)
                {
                    total++;
                    string what = EnemyBlockers.Describe(c);
                    if (what == "关掉") off++;
                    else if (what.StartsWith("留着")) kept++;
                    var b = c.bounds;
                    string near = "无";
                    float best = float.MaxValue;
                    foreach (var g in gates)
                    {
                        var gc = g.GetComponent<Collider2D>();
                        if (gc == null) continue;
                        float d = Mathf.Sqrt(gc.bounds.SqrDistance(b.center));
                        if (d < best) { best = d; near = $"{g.name}→{g.targetScene} {d:F1}"; }
                    }
                    var sb = new StringBuilder();
                    foreach (var e in enemies)
                        if (e != null && Vector2.Distance(e.transform.position, b.ClosestPoint(e.transform.position)) < 8f) sb.Append(e.name).Append(' ');
                    Plugin.Log.LogInfo($"[AutoTest] 挡怪 {name}：{PathOf(c.transform)}（{c.GetType().Name}{(c.enabled ? "" : " 未启用")}{(c.gameObject.activeInHierarchy ? "" : " 物体未激活")}）" +
                                       $"{b.min.x:F1},{b.min.y:F1}..{b.max.x:F1},{b.max.y:F1}，最近的门 {near}，附近的怪 {(sb.Length > 0 ? sb.ToString() : "无")}，处理：{what}");
                }
                var unload = USM.UnloadSceneAsync(scene);
                while (unload != null && !unload.isDone) yield return null;
                if (scenes % 25 == 0)
                {
                    var gc2 = Resources.UnloadUnusedAssets();
                    while (!gc2.isDone) yield return null;
                }
            }
            Plugin.Log.LogInfo($"[AutoTest] 挡怪 看了 {scenes} 个场景，挡怪层的实心碰撞体 {total} 个：关掉 {off}，守门留着 {kept}");
            Plugin.Log.LogInfo($"[AutoTest] 地图框 {UI.MapFrames.DevAll()}");
        }

        static void Place(GameObject go, Vector2 at)
        {
            var rb = go.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.position = at;
                rb.linearVelocity = Vector2.zero;
            }
            go.transform.position = new Vector3(at.x, at.y, go.transform.position.z);
        }

        IEnumerator BlockerFixTest(HeroController hero)
        {
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            string gate = null;
            foreach (var tp in Object.FindObjectsByType<TransitionPoint>(FindObjectsSortMode.None))
                if (tp.targetScene == "Cliffs_02") gate = tp.entryPoint;
            if (gate == null)
            {
                Plugin.Log.LogInfo("[AutoTest] 挡怪 国王山道里没有去 Cliffs_02 的门");
                yield break;
            }
            yield return GoTo("Cliffs_02", gate);
            hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(1.5f);
            Collider2D passage = null;
            foreach (var c in Object.FindObjectsByType<Collider2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (c.isTrigger || c.gameObject.layer != EnemyBlockers.Layer || c.gameObject.scene.name != "Cliffs_02") continue;
                Plugin.Log.LogInfo($"[AutoTest] 挡怪 Cliffs_02：{PathOf(c.transform)} 启用 {c.enabled}，处理：{EnemyBlockers.Describe(c)}");
                if (c.name == "Enemy Blocker" && Mathf.Abs(c.transform.position.y - 6f) < 4f && c.transform.position.x > 90f && c.transform.position.x < 150f) passage = c;
            }
            if (passage == null)
            {
                Plugin.Log.LogInfo("[AutoTest] 挡怪 没找到暗道里的 Enemy Blocker");
                yield break;
            }
            float edge = EnemyBlockers.BoundsOf(passage).min.x;
            for (int pass = 0; pass < 2; pass++)
            {
                bool on = pass == 0;
                int n = EnemyBlockers.DevSet(on);
                passage.enabled = on;
                // The player just inside the passage, where the report was made.
                hero.transform.position = Clear(hero, new Vector2(97f, 6.5f));
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.5f);
                var watched = new List<(Transform t, string name, bool crawler)>();
                // A crawler and a flyer of the scene brought to just outside the passage, facing it.
                Crawler crawler = null;
                HealthManager flyer = null;
                foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                {
                    if (hm.gameObject.scene.name != "Cliffs_02" || hm.isDead) continue;
                    var cr = hm.GetComponent<Crawler>();
                    if (cr != null && crawler == null) crawler = cr;
                    else if (cr == null && flyer == null && hm.name.StartsWith("Buzzer")) flyer = hm;
                }
                if (crawler != null)
                {
                    // It walks on the way it faces, turning at walls: over the watch it reaches the passage either way.
                    Place(crawler.gameObject, new Vector2(edge - 3f, 5.8f));
                    watched.Add((crawler.transform, crawler.name, true));
                }
                if (flyer != null)
                {
                    // Low, at the passage's height, so it can't go over the blocker.
                    Place(flyer.gameObject, new Vector2(edge - 3.5f, 6.2f));
                    watched.Add((flyer.transform, flyer.name, false));
                }
                foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                {
                    if (hm.gameObject.scene.name != "Cliffs_02" || hm.isDead || crawler != null && hm.transform == crawler.transform || flyer != null && hm.transform == flyer.transform) continue;
                    var p = (Vector2)hm.transform.position;
                    if (p.x < 60f || p.x > 100f || p.y < -5f || p.y > 25f) continue;
                    watched.Add((hm.transform, hm.name, hm.GetComponent<Crawler>() != null));
                }
                var start = new List<Vector2>();
                var minX = new List<float>();
                var maxX = new List<float>();
                foreach (var w in watched) { start.Add(w.t.position); minX.Add(w.t.position.x); maxX.Add(w.t.position.x); }
                // How far in each got while at the blocker's height (y 3..8.8): over it doesn't count.
                var deepest = new List<float>();
                foreach (var w in watched) deepest.Add(float.MinValue);
                var lastMove = new List<float>();
                var lastPos = new List<Vector2>(start);
                foreach (var w in watched) lastMove.Add(0f);
                float t0 = Time.time;
                while (Time.time - t0 < 10f)
                {
                    PlayerData.instance.isInvincible = true;
                    for (int i = 0; i < watched.Count; i++)
                    {
                        if (watched[i].t == null) continue;
                        Vector2 p = watched[i].t.position;
                        if (p.y < 8.8f) deepest[i] = Mathf.Max(deepest[i], p.x);
                        minX[i] = Mathf.Min(minX[i], p.x);
                        maxX[i] = Mathf.Max(maxX[i], p.x);
                        if ((p - lastPos[i]).sqrMagnitude > 0.0004f) lastMove[i] = Time.time - t0;
                        lastPos[i] = p;
                    }
                    yield return null;
                }
                var sb = new StringBuilder();
                for (int i = 0; i < watched.Count; i++)
                {
                    if (watched[i].t == null) { sb.Append($"\n    {watched[i].name}：没了"); continue; }
                    Vector2 p = watched[i].t.position;
                    bool inside = deepest[i] > edge + 0.5f;
                    sb.Append($"\n    {watched[i].name}{(watched[i].crawler ? "（爬虫）" : "")}：从 {start[i].x:F1},{start[i].y:F1} 到 {p.x:F1},{p.y:F1}，x 范围 {minX[i]:F1}..{maxX[i]:F1}，" +
                              $"进了暗道 {inside}，最后一次在动 {lastMove[i]:F1}s（共 10s）");
                }
                Plugin.Log.LogInfo($"[AutoTest] 挡怪 {(on ? "挡怪开着（原版）" : "挡怪关掉（修复后）")}，切换了 {n} 个，暗道口 x={edge:F2}，主角 {hero.transform.position:F1}，盯着 {watched.Count} 只怪：{sb}");
                Shot("Cliffs_02", on ? "blockers_on" : "blockers_off");
                yield return null;
            }
            mod.DevNoFallDamage = false;
        }
    }
}
