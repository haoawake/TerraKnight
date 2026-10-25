using System.Collections;
using System.Diagnostics;
using TerraKnight.World;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "buildlag"): a player's report — building houses in Dirtmouth lags. In Dirtmouth, a house built in steps
    /// in the open air beside the Knight (wood walls behind, wood blocks round each room), a few cells a frame as a player
    /// places them; after each step the cost of a placement, the frames' average and worst, the collections, the memory
    /// (the managed heap in use and reserved, Unity's) and the sprites, colliders and objects there are, logged, with the
    /// frame profiler's costliest and most allocating methods (<see cref="DevFrameProfiler.Allocations"/>).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator BuildLagTest()
        {
            mod.DevNoFallDamage = true;
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            // What one IMGUI call allocates, in a repaint of its own: plain labels, a kept GUIContent, the style drawn directly.
            var probe = new GameObject("Dev Label Probe").AddComponent<LabelProbe>();
            yield return new WaitForSeconds(1f);
            Plugin.Log.LogInfo($"[AutoTest] 造房卡顿 文字分配：{probe.Result}");
            Object.Destroy(probe.gameObject);
            DevFrameProfiler.Allocations = true;
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            var start = CellGrid.WorldToCell(hero.transform.position);
            // The open air right of the Knight, from a cell over his feet.
            int x0 = start.x + 4, y0 = start.y + 1;
            int placed = 0;
            int gc0 = 0;
            long used0 = 0;
            void Begin()
            {
                gc0 = System.GC.CollectionCount(0);
                used0 = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
                DevFrameProfiler.Begin();
            }
            void Measure(string label, double placeMs, int count)
            {
                string report = DevFrameProfiler.Report("造房 " + label, frameCount, 12);
                long used = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
                long heap = UnityEngine.Profiling.Profiler.GetMonoHeapSizeLong();
                long unity = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
                long reserved = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong();
                int sprites = Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None).Length;
                int meshes = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Length;
                int cols = Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None).Length;
                int gos = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length;
                Plugin.Log.LogInfo($"[AutoTest] 造房卡顿 {label}：累计 {placed} 格，这批 {count} 格每格 {(count > 0 ? placeMs / count : 0):F2} ms（最慢一格 {worstPlace:F1} ms），" +
                    $"{frameCount} 帧 平均 {avgFrame * 1000f:F1} ms 最长 {maxFrame * 1000f:F1} ms，GC {System.GC.CollectionCount(0) - gc0} 次，托管堆每帧涨 {(used - used0) / 1024f / Mathf.Max(1, frameCount):F1} KB，托管堆 用了 {used / 1048576} MB / 占着 {heap / 1048576} MB，" +
                    $"Unity 用了 {unity / 1048576} MB / 占着 {reserved / 1048576} MB，精灵 {sprites}，网格 {meshes}，碰撞体 {cols}，物体 {gos}");
                if (DevFrameProfiler.Installed) Plugin.Log.LogInfo(report);
            }
            Begin();
            yield return FrameStats(3f);
            Measure("开始", 0, 0);
            // Rooms 12 wide and 8 high side by side, then a second storey: 96 cells each.
            int[] roomsPerStep = { 2, 4, 8, 12 };
            int room = 0;
            foreach (int rooms in roomsPerStep)
            {
                double ms = 0;
                int count = 0;
                worstPlace = 0;
                Begin();
                for (; room < rooms; room++)
                {
                    int rx = x0 + (room % 6) * 12, ry = y0 + (room / 6) * 8;
                    for (int y = 0; y < 8; y++)
                        for (int x = 0; x < 12; x++)
                        {
                            int gx = rx + x, gy = ry + y;
                            if (!TerraWorld.InWorld(gx, gy)) continue;
                            bool edge = x == 0 || x == 11 || y == 0 || y == 7;
                            var sw = Stopwatch.StartNew();
                            if (edge) { if (TerraWorld.Get(gx, gy) == CellKind.Empty) TerraWorld.Set(gx, gy, CellKind.Wood); }
                            else TerraWorld.SetWall(gx, gy, 4);
                            double one = sw.Elapsed.TotalMilliseconds;
                            ms += one;
                            if (one > worstPlace) worstPlace = one;
                            count++;
                            placed++;
                            // A few a frame, as a player with a fast hand (and the builder's reach) does.
                            if (count % 3 == 0) yield return null;
                        }
                }
                yield return FrameStats(3f);
                Measure($"{rooms} 间房", ms, count);
            }
            // Standing still in the finished town for a while, and walking past the houses.
            Begin();
            yield return FrameStats(6f);
            Measure("站着不动", 0, 0);
            // The hotbar's numbers and the life line, bordered (UI.BorderText): looked at.
            Shot("Town", "buildgc_hud");
            yield return new WaitForSeconds(0.5f);
            // The inventory open (building has it open a lot), the mouse over the first slot's item: its tooltip up.
            Items.InventoryGui.Open = true;
            TerraMod.DevNoRealMouse = false;
            yield return null;
            yield return null;
            var slot = Items.InventoryGui.DevRectOf("Slot", 0);
            if (slot.HasValue) RealMouse(new Vector2(slot.Value.center.x, Screen.height - slot.Value.center.y), false);
            yield return new WaitForSeconds(0.5f);
            Begin();
            yield return FrameStats(4f);
            Measure($"背包打开、指着物品（{(slot.HasValue ? "找到格子" : "没找到格子")}）", 0, 0);
            Shot("Town", "buildgc_inventory");
            yield return new WaitForSeconds(0.5f);
            Items.InventoryGui.Open = false;
            TerraMod.DevNoRealMouse = true;
            // The full map open over the town.
            UI.WorldMap.Toggle();
            yield return new WaitForSeconds(0.5f);
            Begin();
            yield return FrameStats(3f);
            Measure($"全地图打开（{(UI.WorldMap.Open ? "开着" : "没打开")}）", 0, 0);
            if (UI.WorldMap.Open) UI.WorldMap.Toggle();
            yield return new WaitForSeconds(0.5f);
            Begin();
            float until = Time.time + 4f;
            while (Time.time < until)
            {
                hero.transform.position += new Vector3(6f * Time.deltaTime, 0f, 0f);
                yield return null;
            }
            yield return FrameStats(1f);
            // The walk's frames with its four seconds.
            frameCount += Mathf.RoundToInt(4f / Mathf.Max(0.001f, avgFrame));
            Measure("走过房子", 0, 0);
            // A plain room elsewhere, for the allocation the mod makes anywhere.
            yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            yield return new WaitForSeconds(2f);
            Begin();
            yield return FrameStats(6f);
            Measure("对照：十字路口第一间", 0, 0);
            DevFrameProfiler.Allocations = false;
            Plugin.Log.LogInfo("[AutoTest] 造房卡顿 完成");
        }

        sealed class LabelProbe : MonoBehaviour
        {
            public string Result = "没量到";
            int rounds;
            GUIStyle style;
            readonly GUIContent kept = new GUIContent("123");
            readonly System.Text.StringBuilder sb = new System.Text.StringBuilder();

            void OnGUI()
            {
                if (Event.current.type != EventType.Repaint || rounds >= 3) return;
                style ??= new GUIStyle(GUI.skin.label) { fontSize = 18 };
                const int n = 2000;
                var r = new Rect(-200f, -200f, 60f, 20f);
                long a = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
                for (int i = 0; i < n; i++) GUI.Label(r, "123", style);
                long b = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
                for (int i = 0; i < n; i++) GUI.Label(r, kept, style);
                long c = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
                for (int i = 0; i < n; i++) style.Draw(r, kept, false, false, false, false);
                long d = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
                for (int i = 0; i < n; i++) GUI.DrawTexture(r, Texture2D.whiteTexture);
                long e = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong();
                sb.Append($" 第{rounds + 1}次：GUI.Label(字符串) 每次 {(b - a) / (float)n:F1} B，GUI.Label(GUIContent) {(c - b) / (float)n:F1} B，style.Draw {(d - c) / (float)n:F1} B，DrawTexture {(e - d) / (float)n:F1} B；");
                Result = sb.ToString();
                rounds++;
            }
        }

        static double worstPlace;
        static float avgFrame, maxFrame;
        static int frameCount;

        static IEnumerator FrameStats(float seconds)
        {
            float until = Time.unscaledTime + seconds, sum = 0f, max = 0f;
            int n = 0;
            while (Time.unscaledTime < until)
            {
                yield return null;
                float dt = Time.unscaledDeltaTime;
                sum += dt;
                if (dt > max) max = dt;
                n++;
            }
            avgFrame = n > 0 ? sum / n : 0f;
            maxFrame = max;
            frameCount = n;
        }
    }
}
