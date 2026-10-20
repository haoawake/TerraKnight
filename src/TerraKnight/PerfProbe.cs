using System.Diagnostics;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Where a slow frame's time goes, as far as the mod is concerned: every five seconds, the frame rate and the longest
    /// frame, beside the mod's own Update, LateUpdate and OnGUI (their average and longest), logged when the frames were
    /// slow or the mod's share was large, with the room's active enemies, moving bodies and colliders. It tells the
    /// mod's cost apart from the game's (a slow room with the mod at a millisecond is the game's, its physics or its
    /// drawing). With the FrameProfiler setting on (<see cref="Detailed"/>), each slow stretch also lists the costliest
    /// per-frame methods of every script (<see cref="DevFrameProfiler"/>).
    /// </summary>
    internal static class PerfProbe
    {
        public enum Part { Update, LateUpdate, OnGui }

        /// <summary>Every script's per-frame methods timed (the FrameProfiler setting): their list goes with each slow stretch.</summary>
        public static bool Detailed;

        const float Window = 5f;
        static readonly double[] sum = new double[3], max = new double[3];
        static int frames, lastFrame = -1;
        static float since, longest;
        static bool started;
        static readonly double tickMs = 1000.0 / Stopwatch.Frequency;

        public static long Begin() => Stopwatch.GetTimestamp();

        public static void End(Part part, long began)
        {
            double ms = (Stopwatch.GetTimestamp() - began) * tickMs;
            int k = (int)part;
            sum[k] += ms;
            if (ms > max[k]) max[k] = ms;
            if (part != Part.Update) return;
            // Once a frame (Update runs once a frame): the frame's own length, and the window's report.
            if (Time.frameCount == lastFrame) return;
            lastFrame = Time.frameCount;
            if (Detailed && !started)
            {
                started = true;
                DevFrameProfiler.Begin();
            }
            frames++;
            float dt = Time.unscaledDeltaTime;
            since += dt;
            if (dt > longest) longest = dt;
            if (since < Window) return;
            float fps = frames / since;
            double mod = (sum[0] + sum[1] + sum[2]) / Mathf.Max(1, frames);
            bool slow = fps < 50f || longest > 0.05f || mod > 4.0;
            if (slow)
            {
                var gm = GameManager.UnsafeInstance;
                Plugin.Log.LogInfo($"[性能] {since:F1} 秒 {frames} 帧：帧率 {fps:F0}，最长一帧 {longest * 1000f:F0} ms；mod 每帧平均 {mod:F1} ms" +
                    $"（Update 平均 {sum[0] / frames:F1} 最长 {max[0]:F1}，LateUpdate 平均 {sum[1] / frames:F1} 最长 {max[1]:F1}，OnGUI 平均 {sum[2] / frames:F1} 最长 {max[2]:F1}）" +
                    $"，{(gm != null ? gm.gameState.ToString() : "无 GameManager")}，场景 {gm?.sceneName}；{Counts()}");
                if (Detailed) Plugin.Log.LogInfo(DevFrameProfiler.Report($"{gm?.sceneName} 掉帧的 {since:F1} 秒", frames, 20));
            }
            if (Detailed) DevFrameProfiler.Begin();
            frames = 0;
            since = longest = 0f;
            for (int i = 0; i < 3; i++) sum[i] = max[i] = 0;
        }

        /// <summary>What the room holds: active enemies, moving (dynamic, simulated) bodies, enabled colliders; the physics step.</summary>
        static string Counts()
        {
            int enemies = 0, bodies = 0, colliders = 0;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                if (hm != null && !hm.isDead) enemies++;
            foreach (var rb in Object.FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None))
                if (rb != null && rb.simulated && rb.bodyType == RigidbodyType2D.Dynamic) bodies++;
            foreach (var c in Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
                if (c != null && c.enabled) colliders++;
            return $"活动敌人 {enemies}，动态刚体 {bodies}，碰撞体 {colliders}，物理步长 {Time.fixedDeltaTime * 1000f:F1} ms";
        }
    }
}
