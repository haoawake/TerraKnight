using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        sealed class RegionPerfRow
        {
            public string Area, Scene, Phase, RenderMode;
            public int Samples, Slow50, Slow100, GC0, Enemies, Objects;
            public double Seconds, Fps, MedianMs, P95Ms, P99Ms, WorstMs, ModMeanMs, ManagedMB, UnityMB;
            public bool Control, Arrived;
            public float Travel, RenderLuma;
        }

        static bool PerfIgnoreDamage() => false;

        IEnumerator ReleasePerfTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            var rows = new List<RegionPerfRow>();
            var previousAi = EnemyAiMode.Current;
            var benchmarkAi = suite.StartsWith("releaseperfnew") ? EnemyAiKind.New : EnemyAiKind.Vanilla;
            EnemyAiMode.Current = benchmarkAi;
            var errors = new List<string>();
            var candidates = UI.WorldMap.DevSceneNames().Concat(entryGates.Keys).Distinct().ToList();
            var preferred = new[] { "Tutorial_01", "Town", "Room_shop", "Cliffs_01", "Crossroads_01", "Crossroads_50",
                "Fungus1_01", "Fungus1_13", "Fungus2_04", "Fungus2_14", "Fungus3_01", "Fungus3_archive",
                "Fungus3_10", "Ruins1_01", "Ruins1_23", "Ruins2_03", "Waterways_01", "Waterways_04",
                "Mines_02", "Mines_18", "RestingGrounds_04", "RestingGrounds_08", "Deepnest_26b", "Deepnest_26",
                "Deepnest_Spider_Town", "Deepnest_East_01", "Deepnest_East_14", "Hive_01", "Hive_03",
                "Abyss_17", "Abyss_19", "Abyss_06_Core", "Abyss_12", "White_Palace_03_hub", "White_Palace_06",
                "Room_temple", "GG_Atrium", "Dream_Nailcollection", "Grimm_Main_Tent", "Room_Colosseum_01" };
            var selected = new List<string>();
            foreach (var area in AreaBlocks.All)
            {
                var homes = candidates.Where(s => Areas.AreaName(s) == area.Name).OrderBy(s => s, StringComparer.Ordinal).ToList();
                var picks = preferred.Where(s => Areas.AreaName(s) == area.Name && (homes.Contains(s) || Application.CanStreamedLevelBeLoaded(s))).Distinct().ToList();
                if (picks.Count == 0 && homes.Count > 0) picks.Add(homes[0]);
                selected.AddRange(picks);
            }
            // Optional room-only diagnostic rerun; full sweep remains recorded independently.
            if (suite.StartsWith("releaseperf_")) selected = suite.Substring("releaseperf_".Length).Split(',').ToList();
            if (suite.StartsWith("releaseperfnew_")) selected = suite.Substring("releaseperfnew_".Length).Split(',').ToList();
            if (suite == "releaseperfnew") selected = selected.GroupBy(s => Areas.AreaName(s)).Select(g => g.First()).ToList();
            int cap = mod.TargetFrameRate;
            int sync = QualitySettings.vSyncCount, appCap = Application.targetFrameRate;
            mod.TargetFrameRate = 0; QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            mod.DevNoFallDamage = true; PlayerData.instance.isInvincible = true;
            // Native invincibility does not protect the mod's life pool. Keep the benchmark
            // alive without removing enemies, their AI, particles or collision processing.
            var safety = new Harmony("TerraKnight.isolated-regional-performance");
            var refuse = new HarmonyMethod(typeof(DevAutoTest).GetMethod(nameof(PerfIgnoreDamage), System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic));
            safety.Patch(AccessTools.Method(typeof(HeroController), "TakeDamage"), prefix: refuse);
            safety.Patch(AccessTools.Method(typeof(PlayerData), "TakeHealth"), prefix: refuse);
            mod.Stats.Life = mod.Stats.LifeMax;
            InventoryGui.Open = false;
            var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            rt.Create();
            Camera attached = null; RenderTexture previousTarget = null;
            var blackout = GameObject.Find("Masker Blackout")?.GetComponent<Renderer>();
            bool blackoutEnabled = blackout != null && blackout.enabled;
            if (blackout != null) blackout.enabled = false;
            void Save()
            {
                File.WriteAllText(Path.Combine(outDir, "regional-perf.json"), JsonConvert.SerializeObject(new {
                    Version = Plugin.Version, Suite = suite, EnemyAI = benchmarkAi.ToString(), GPU = SystemInfo.graphicsDeviceName, CPU = SystemInfo.processorType,
                    SystemMemoryMB = SystemInfo.systemMemorySize, OS = SystemInfo.operatingSystem, Unity = Application.unityVersion,
                    Width = 1920, Height = 1080, VSync = 0, Cap = -1,
                    Selected = selected, CoveredAreas = rows.Select(r => r.Area).Distinct().ToArray(), Errors = errors, Rows = rows,
                    Note = "Uncapped isolated background run, main camera rendered into a 1920x1080 target, with the game's full-screen Masker Blackout excluded from the diagnostic render. UI/presentation and other processes can affect comparison. Manual fallback is explicitly labeled."
                }, Formatting.Indented));
            }
            void OnLog(string msg, string stack, LogType type)
            {
                if (type == LogType.Exception || type == LogType.Error)
                    if (errors.Count < 300) errors.Add(GameManager.UnsafeInstance?.sceneName + ": " + msg);
            }
            Application.logMessageReceived += OnLog;
            try
            {
                foreach (string scene in selected)
                {
                    Player.TerraMovement.DevMove = 0;
                    if (attached != null) attached.targetTexture = previousTarget;
                    attached = null;
                    PlayerData.instance.isInvincible = true;
                    float settleUntil = Time.realtimeSinceStartup + 8f;
                    while (GameManager.instance.IsInSceneTransition && Time.realtimeSinceStartup < settleUntil) yield return null;
                    float start = Time.realtimeSinceStartup;
                    var loading = new List<float>(); bool watching = true;
                    mod.StartCoroutine(WatchFrameTimes(loading, () => watching));
                    yield return GoTo(scene, scene == "GG_Atrium" ? "door_dreamEnter" : EntryGateOf(scene));
                    watching = false;
                    float load = Time.realtimeSinceStartup - start;
                    rows.Add(PerfStats(scene, "load", loading, load, 0, 0, 0, "window-hidden", -1));
                    var hero = HeroController.instance;
                    hero.transform.position = PerfAnchor(scene, hero);
                    hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                    attached = GameCameras.instance?.mainCamera;
                    if (attached == null) { errors.Add(scene + ": no main camera"); Save(); continue; }
                    previousTarget = attached.targetTexture; attached.targetTexture = rt;
                    // An actual render target prevents an occluded window from masquerading as fast rendering.
                    yield return new WaitForSecondsRealtime(2f);
                    if (blackout != null) blackout.enabled = false;
                    yield return new WaitForEndOfFrame();
                    // Hidden windows may leave a target containing the PREVIOUS room.
                    // Force a fresh main-camera draw every frame, even when its old pixels look valid.
                    attached.Render();
                    float luma = PerfCapture(rt, null);
                    bool manual = true;
                    Plugin.Log.LogInfo($"[AutoTest] REGIONAL_RENDER {scene} luma={luma:F4} camera={attached.enabled} manual={manual} GPU={SystemInfo.graphicsDeviceName}");
                    if (luma < .005f) errors.Add(scene + ": render target black; cannot validate rendered FPS");
                    yield return PerfWindow(scene, "idle", 6f, attached, rt, manual, rows);
                    yield return PerfWindow(scene, "move", 8f, attached, rt, manual, rows);
                    PerfCapture(rt, Path.Combine(outDir, "render-" + scene + ".png"));
                    Save();
                }
            }
            finally
            {
                Player.TerraMovement.DevMove = 0;
                if (attached != null) attached.targetTexture = previousTarget;
                if (blackout != null) blackout.enabled = blackoutEnabled;
                mod.TargetFrameRate = cap; QualitySettings.vSyncCount = sync; Application.targetFrameRate = appCap;
                Application.logMessageReceived -= OnLog;
                safety.UnpatchSelf();
                EnemyAiMode.Current = previousAi;
                rt.Release(); Object.Destroy(rt); Save();
            }
            Plugin.Log.LogInfo($"[AutoTest] REGIONAL_PERF rooms={selected.Count} areas={rows.Select(r=>r.Area).Distinct().Count()}/24 rows={rows.Count} errors={errors.Count}");
            bool valid = rows.Where(r => r.Phase != "load").All(r => r.Arrived && r.Control && r.Samples > 120 && r.RenderLuma > .005f && (r.Phase != "move" || r.Travel > 1f));
            bool coverage = (suite != "releaseperf" && suite != "releaseperfnew") || rows.Select(r => r.Area).Distinct().Count() == 24;
            Plugin.Log.LogInfo($"[AutoTest] REGIONAL_PERF valid-render-and-control {(valid && coverage ? "通过" : "不通过")}");
        }

        IEnumerator PerfWindow(string scene, string phase, float seconds, Camera camera, RenderTexture target, bool manual, List<RegionPerfRow> rows)
        {
            var frames = new List<float>(); double modMs = 0;
            int gc = GC.CollectionCount(0);
            float start = Time.realtimeSinceStartup;
            var hero = HeroController.instance;
            Vector2 origin = hero.transform.position;
            Vector2 last = origin;
            float travel = 0;
            int direction = 1;
            var endOfFrame = new WaitForEndOfFrame();
            while (Time.realtimeSinceStartup - start < seconds)
            {
                if (GameManager.instance.sceneName != scene) break;
                if (phase == "move")
                {
                    float offsetX = hero.transform.position.x - origin.x;
                    if (offsetX > 2.5f) direction = -1;
                    if (offsetX < -2.5f) direction = 1;
                    Player.TerraMovement.DevMove = direction;
                    // Keep the benchmark in its named room, avoiding accidental gate crossings/falls.
                    if (Mathf.Abs(hero.transform.position.x - origin.x) > 3.5f || hero.transform.position.y < origin.y - 3f)
                        hero.transform.position = Clear(hero, origin);
                }
                yield return endOfFrame;
                if (manual && camera != null) camera.Render();
                frames.Add(Time.unscaledDeltaTime);
                modMs += PerfProbe.LastMilliseconds.Sum();
                travel += Vector2.Distance(last, hero.transform.position); last = hero.transform.position;
            }
            Player.TerraMovement.DevMove = 0;
            float luma = PerfCapture(target, null);
            var row = PerfStats(scene, phase, frames, Time.realtimeSinceStartup - start, GC.CollectionCount(0) - gc, modMs, travel,
                manual ? "manual-camera-render" : "auto-camera-target", luma);
            rows.Add(row);
            Plugin.Log.LogInfo($"[AutoTest] REGIONAL_PERF {row.Area}/{scene}/{phase} fps={row.Fps:F1} p95={row.P95Ms:F2} p99={row.P99Ms:F2} max={row.WorstMs:F1} slow50={row.Slow50}/{row.Samples} GC={row.GC0} mod={row.ModMeanMs:F2}ms control={row.Control} travel={row.Travel:F1}");
        }

        static Vector3 PerfAnchor(string scene, HeroController hero)
        {
            var region = TerrainSystem.Find(scene);
            if (region == null) return hero.transform.position;
            var rect = region.CellRect;
            Vector2 wish = scene == "Town" ? new Vector2(132f, 12f) : (Vector2)rect.center*CellGrid.CellSize;
            var gates = TransitionPoint.TransitionPoints.Where(g => g != null && g.gameObject.scene.name == scene).Select(g => (Vector2)g.transform.position).ToArray();
            float best = float.MaxValue; Vector2 point = wish; bool found = false;
            float footOffset = hero.GetComponent<BoxCollider2D>().bounds.min.y - hero.transform.position.y;
            // A supported corridor well inside this room, wide enough for a six-unit walking loop.
            for (int x = rect.xMin + 10; x < rect.xMax - 10; x += 2)
                for (int y = rect.yMin + 3; y < rect.yMax - 5; y++)
                {
                    if (!TerraWorld.IsSolid(x,y-1) || !TerraWorld.IsSolid(x-6,y-1) || !TerraWorld.IsSolid(x+6,y-1)) continue;
                    bool clear = true;
                    for (int dx = -6; dx <= 6 && clear; dx++)
                        for (int dy = 0; dy < 4; dy++) if (TerraWorld.IsSolid(x+dx,y+dy)) { clear=false; break; }
                    if (!clear) continue;
                    var at = new Vector2((x+.5f)*CellGrid.CellSize,y*CellGrid.CellSize-footOffset+.1f);
                    if (gates.Any(g => Vector2.Distance(g,at) < 7f)) continue;
                    float score = (at-wish).sqrMagnitude;
                    if (score >= best) continue;
                    point=at; best=score; found=true;
                }
            Plugin.Log.LogInfo($"[AutoTest] REGIONAL_ANCHOR {scene} supported={found} at={point} rect={rect}");
            return found ? Clear(hero,point) : Clear(hero,(Vector2)hero.transform.position + Vector2.right*(EntryGateOf(scene).StartsWith("right")?-5f:5f));
        }

        static RegionPerfRow PerfStats(string scene, string phase, List<float> frames, double seconds, int gc, double modMs, float travel, string renderMode, float luma)
        {
            var sorted = frames.OrderBy(v => v).ToArray();
            double P(double q) => sorted.Length == 0 ? 0 : sorted[Math.Min(sorted.Length - 1, (int)Math.Ceiling(sorted.Length * q) - 1)] * 1000.0;
            return new RegionPerfRow { Area = Areas.AreaName(scene), Scene = scene, Phase = phase, Samples = frames.Count, Seconds = seconds,
                Fps = frames.Count / Math.Max(.001, frames.Sum(v => (double)v)), MedianMs = P(.5), P95Ms = P(.95), P99Ms = P(.99), WorstMs = P(1),
                Slow50 = frames.Count(v => v > .05f), Slow100 = frames.Count(v => v > .1f), GC0 = gc,
                ModMeanMs = modMs / Math.Max(1, frames.Count), Travel = travel,
                Control = GameHooks.PlayerHasControl(), Arrived = GameManager.instance.sceneName == scene,
                RenderMode = renderMode, RenderLuma = luma,
                Enemies = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None).Count(h => h != null && !h.isDead),
                Objects = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length,
                ManagedMB = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() / 1048576.0,
                UnityMB = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576.0 };
        }

        static float PerfCapture(RenderTexture target, string path)
        {
            var prior = RenderTexture.active;
            var small = RenderTexture.GetTemporary(64, 36, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(target, small); RenderTexture.active = small;
            var tex = new Texture2D(64, 36, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 64, 36), 0, 0); tex.Apply();
            float luma = 0; foreach (var c in tex.GetPixels32()) luma += (c.r + c.g + c.b) / (3f * 255f * 64f * 36f);
            Object.Destroy(tex); RenderTexture.ReleaseTemporary(small);
            if (path != null)
            {
                RenderTexture.active = target;
                var full = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                full.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); full.Apply();
                File.WriteAllBytes(path, full.EncodeToPNG()); Object.Destroy(full);
            }
            RenderTexture.active = prior;
            return luma;
        }
    }
}
