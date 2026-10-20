using System.Diagnostics;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Where a slow frame's time goes, as far as the mod is concerned: every five seconds, the frame rate and the longest
    /// frame, beside the mod's own Update, LateUpdate and OnGUI (their average and longest), logged when the frames were
    /// slow or the mod's share was large. It tells the mod's cost apart from the game's (a slow title screen with the mod
    /// at a millisecond is the game's, or the machine's).
    /// </summary>
    internal static class PerfProbe
    {
        public enum Part { Update, LateUpdate, OnGui }

        const float Window = 5f;
        static readonly double[] sum = new double[3], max = new double[3];
        static int frames, lastFrame = -1;
        static float since, longest;
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
            frames++;
            float dt = Time.unscaledDeltaTime;
            since += dt;
            if (dt > longest) longest = dt;
            if (since < Window) return;
            float fps = frames / since;
            double mod = (sum[0] + sum[1] + sum[2]) / Mathf.Max(1, frames);
            if (fps < 50f || longest > 0.05f || mod > 4.0)
            {
                var gm = GameManager.UnsafeInstance;
                Plugin.Log.LogInfo($"[性能] {since:F1} 秒 {frames} 帧：帧率 {fps:F0}，最长一帧 {longest * 1000f:F0} ms；mod 每帧平均 {mod:F1} ms" +
                    $"（Update 平均 {sum[0] / frames:F1} 最长 {max[0]:F1}，LateUpdate 平均 {sum[1] / frames:F1} 最长 {max[1]:F1}，OnGUI 平均 {sum[2] / frames:F1} 最长 {max[2]:F1}）" +
                    $"，{(gm != null ? gm.gameState.ToString() : "无 GameManager")}，场景 {gm?.sceneName}");
            }
            frames = 0;
            since = longest = 0f;
            for (int i = 0; i < 3; i++) sum[i] = max[i] = 0;
        }
    }
}
