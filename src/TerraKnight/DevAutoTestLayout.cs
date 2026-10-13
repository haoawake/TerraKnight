using System.Collections;
using System.Collections.Generic;
using System.IO;
using GlobalEnums;
using Newtonsoft.Json;
using TerraKnight.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "layout"): what the world map's layout is solved from. Every gameplay scene is
    /// loaded on its own beside the one played; its grid built as the terrain system builds it;
    /// each gate's side, centre (in the grid's own units), seam and the floor beside it (as
    /// LiveWorld.Join measures them), where it leads; the scene's map zone. One "[AutoTest] 布局"
    /// line of JSON per scene, for scratchpad/maplayout/solve.py. Also the names of things worth a
    /// block on the map (benches, lamps, levers...), counted, for the map's legend.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly string[] layoutWords =
        {
            "bench", "lamp", "lantern", "light", "lever", "chest", "toll", "stag", "lift", "elevator", "grub", "totem", "spa", "door",
            "npc", "shop", "tram", "switch", "gate", "breakable", "geo rock", "shiny", "relic", "egg", "mask", "vessel", "chain", "sign", "statue",
        };

        /// <param name="missingOnly">Only the scenes the world map has no place for yet (suite "layoutmissing").</param>
        IEnumerator LayoutSurvey(bool missingOnly = false)
        {
            PlayerData.instance.isInvincible = true;
            var names = new List<string>();
            var placed = new HashSet<string>(UI.WorldMap.DevSceneNames());
            for (int i = 0; i < USM.sceneCountInBuildSettings; i++)
            {
                string name = Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
                bool skip = string.IsNullOrEmpty(name) || !name.Contains("_") && name != "Town";
                foreach (var p in skipScenePrefixes) if (name.StartsWith(p) && p != "Tutorial_01") skip = true;
                if (missingOnly && placed.Contains(name)) skip = true;
                if (!skip) names.Add(name);
            }
            if (missingOnly) Plugin.Log.LogInfo($"[AutoTest] 布局 只看地图上没有的 {names.Count} 个场景：{string.Join(" ", names)}");
            var wordCounts = new Dictionary<string, Dictionary<string, int>>();
            float until = Time.realtimeSinceStartup + 780f;
            int done = 0;
            foreach (var name in names)
            {
                if (Time.realtimeSinceStartup > until)
                {
                    Plugin.Log.LogInfo($"[AutoTest] 布局 时间到，停在 {name}");
                    break;
                }
                var already = USM.GetSceneByName(name);
                bool loadedHere = false;
                if (!already.isLoaded)
                {
                    var load = USM.LoadSceneAsync(name, LoadSceneMode.Additive);
                    if (load == null) continue;
                    while (!load.isDone) yield return null;
                    loadedHere = true;
                }
                yield return null;
                PlayerData.instance.isInvincible = true;
                var scene = USM.GetSceneByName(name);
                try { LayoutOf(scene, wordCounts); }
                catch (System.Exception e) { Plugin.Log.LogInfo($"[AutoTest] 布局 {name} 出错：{e.Message}"); }
                done++;
                if (!loadedHere) continue;
                var unload = USM.UnloadSceneAsync(scene);
                while (unload != null && !unload.isDone) yield return null;
                if (done % 25 == 0)
                {
                    var gc = Resources.UnloadUnusedAssets();
                    while (!gc.isDone) yield return null;
                }
            }
            // The atlas is every scene's: a survey of a few leaves the one there.
            Plugin.Log.LogInfo($"[AutoTest] 布局 看了 {done} 个场景；世界图集 {(missingOnly ? "没动" : UI.WorldMap.WriteAtlas())}");
            foreach (var kv in wordCounts)
            {
                var top = new List<KeyValuePair<string, int>>(kv.Value);
                top.Sort((a, b) => b.Value.CompareTo(a.Value));
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < top.Count && i < 40; i++) sb.Append($" {top[i].Key}×{top[i].Value}");
                Plugin.Log.LogInfo($"[AutoTest] 物件 {kv.Key}：{top.Count} 种：{sb}");
            }
        }

        static void LayoutOf(Scene scene, Dictionary<string, Dictionary<string, int>> wordCounts)
        {
            tk2dTileMap map = TerrainSystem.SceneTileMap(scene);
            SceneManager sm = null;
            var roots = scene.GetRootGameObjects();
            foreach (var go in roots)
                if (sm == null) sm = go.GetComponentInChildren<SceneManager>(true);
            if (map == null || map.data == null || sm == null || sm.sceneType != SceneType.GAMEPLAY)
            {
                string why = map == null ? "没有地形图" : map.data == null ? "地形图没数据" : sm == null ? "没有场景管理器" : $"场景类型 {sm.sceneType}";
                Plugin.Log.LogInfo($"[AutoTest] 布局 {scene.name} 不是有地形的游戏场景（{why}）");
                return;
            }
            var grid = TerrainSystem.BuildGrid(map, TerrainSystem.TerrainOutlines(map), TerrainSystem.HandPlacedTerrain(map), scene.name, out _, out _);
            var offset = TerrainSystem.CellOffsetOf(map);
            const float c = CellGrid.CellSize;
            var origin = (Vector2)offset * c;
            var gates = new List<object>();
            foreach (var go in roots)
                foreach (var tp in go.GetComponentsInChildren<TransitionPoint>(true))
                {
                    var col = tp.GetComponent<Collider2D>();
                    Vector2 centre = (col != null ? (Vector2)col.bounds.center : (Vector2)tp.transform.position) - origin;
                    string side = tp.name.Contains("top") ? "top" : tp.name.Contains("right") ? "right" : tp.name.Contains("left") ? "left" : tp.name.Contains("bot") ? "bottom" : "door";
                    int seam = side == "left" || side == "right"
                        ? Mathf.Clamp(Mathf.RoundToInt(centre.x / c), 0, grid.Width)
                        : side == "door" ? 0 : Mathf.Clamp(Mathf.RoundToInt(centre.y / c), 0, grid.Height);
                    int floor = side == "right" ? LiveWorld.FloorBelow(grid, seam - 4, centre.y) : side == "left" ? LiveWorld.FloorBelow(grid, seam + 3, centre.y) : -1;
                    gates.Add(new { n = tp.name, t = tp.targetScene, e = tp.entryPoint, d = tp.isADoor, s = side, x = Round(centre.x), y = Round(centre.y), seam, f = floor });
                }
            // Where the scene is open, in blocks of 8x8 cells (any open cell marks its block): what
            // the solver keeps rooms from piling onto.
            const int B = 8;
            int bw = (grid.Width + B - 1) / B, bh = (grid.Height + B - 1) / B;
            var bits = new byte[(bw * bh + 7) / 8];
            for (int by = 0; by < bh; by++)
                for (int bx = 0; bx < bw; bx++)
                {
                    bool open = false;
                    for (int y = by * B; y < by * B + B && y < grid.Height && !open; y++)
                        for (int x = bx * B; x < bx * B + B && x < grid.Width; x++)
                            if (!grid.IsSolid(x, y)) { open = true; break; }
                    int i = by * bw + bx;
                    if (open) bits[i >> 3] |= (byte)(1 << (i & 7));
                }
            // The scene on the world atlas; its benches (the respawn markers with a bench's state machine) for creative's pylons.
            UI.WorldMap.BakeScene(scene.name, grid, offset, scene);
            var benches = new List<object>();
            foreach (var go in roots)
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                {
                    if (!t.CompareTag("RespawnPoint")) continue;
                    bool bench = false;
                    foreach (var f in t.GetComponentsInChildren<PlayMakerFSM>(true)) if (f.FsmName == "Bench Control") bench = true;
                    if (!bench) continue;
                    benches.Add(new { n = t.name, x = Round(t.position.x - origin.x), y = Round(t.position.y - origin.y) });
                }
            Plugin.Log.LogInfo("[AutoTest] 布局 " + JsonConvert.SerializeObject(new { s = scene.name, w = grid.Width, h = grid.Height, ox = offset.x, oy = offset.y, zone = sm.mapZone.ToString(), g = gates, b = benches, mb = B, m = System.Convert.ToBase64String(bits) }));

            foreach (var go in roots)
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                {
                    string low = t.name.ToLowerInvariant();
                    foreach (var w in layoutWords)
                    {
                        if (!low.Contains(w)) continue;
                        if (!wordCounts.TryGetValue(w, out var d)) wordCounts[w] = d = new Dictionary<string, int>();
                        // Numbered copies ("lamp (3)") count as one name.
                        string key = System.Text.RegularExpressions.Regex.Replace(t.name, @"\s*\(\d+\)$", "");
                        d[key] = d.TryGetValue(key, out int k) ? k + 1 : 1;
                        break;
                    }
                }
        }

        static float Round(float v) => Mathf.Round(v * 100f) / 100f;
    }
}
