using System.Collections;
using HarmonyLib;
using UnityEngine;
using UnityStandardAssets.ImageEffects;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator BloomGuardTest()
        {
            void Check(string label, bool pass) => Plugin.Log.LogInfo($"[AutoTest] Bloom {label}: {(pass ? "通过" : "不通过")}");
            Shader shader = null;
            foreach (var candidate in Resources.FindObjectsOfTypeAll<BloomOptimized>())
                if (candidate.fastBloomShader != null) { shader = candidate.fastBloomShader; break; }
            Check("Bloom shader available", shader != null);
            if (shader == null) yield break;
            var go = new GameObject("TK Bloom Guard Test");
            go.SetActive(false);
            var bloom = go.AddComponent<BloomOptimized>();
            go.GetComponent<Camera>().enabled = false;
            bloom.fastBloomShader = shader;
            go.SetActive(true);
            var render = AccessTools.Method(typeof(BloomOptimized), "OnRenderImage");
            var resolution = AccessTools.Field(typeof(BloomOptimized), "resolution");
            var previous = RenderTexture.active;
            var errors = new System.Collections.Generic.List<string>();
            void OnLog(string message, string stack, LogType type)
            {
                if (type == LogType.Exception || type == LogType.Error) errors.Add(message);
            }
            Application.logMessageReceived += OnLog;
            try
            {
                Check("Bloom resources supported", bloom.CheckResources());
                foreach (var mode in new[] { BloomOptimized.Resolution.Low, BloomOptimized.Resolution.High })
                {
                    resolution.SetValue(bloom, mode);
                    int divisor = mode == BloomOptimized.Resolution.Low ? 4 : 2;
                    foreach (var size in new[] { new Vector2Int(1, 1), new Vector2Int(1, 32), new Vector2Int(32, 1), new Vector2Int(3, 3), new Vector2Int(4, 4), new Vector2Int(64, 32) })
                    {
                        var source = RenderTexture.GetTemporary(size.x, size.y, 0, RenderTextureFormat.ARGB32);
                        var destination = RenderTexture.GetTemporary(size.x, size.y, 0, RenderTextureFormat.ARGB32);
                        var pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                        int start = Effects.BloomGuard.DevBypassed;
                        bool bypass = size.x < divisor || size.y < divisor;
                        try
                        {
                            RenderTexture.active = source;
                            GL.Clear(false, true, new Color(0.2f, 0.6f, 0.3f, 1f));
                            // Repeated tiny frames previously repeated the allocation exception every frame.
                            int repeats = bypass ? 100 : 1;
                            for (int i = 0; i < repeats; i++) render.Invoke(bloom, new object[] { source, destination });
                            Check($"Bloom {mode} {size} bypass count", Effects.BloomGuard.DevBypassed - start == (bypass ? repeats : 0));
                            RenderTexture.active = destination;
                            pixel.ReadPixels(new Rect(0, 0, 1, 1), 0, 0);
                            pixel.Apply();
                            var color = pixel.GetPixel(0, 0);
                            Check($"Bloom {mode} {size} frame preserved", color.g > 0.1f && color.g > color.r);
                            Check($"Bloom {mode} {size} remains enabled", bloom.enabled);
                        }
                        catch (System.Exception e) { Check($"Bloom {mode} {size}: {e.GetBaseException().Message}", false); }
                        finally
                        {
                            RenderTexture.active = previous;
                            RenderTexture.ReleaseTemporary(source);
                            RenderTexture.ReleaseTemporary(destination);
                            Object.Destroy(pixel);
                        }
                    }
                }
                Check("Bloom no render errors or exception log spam", errors.Count == 0);
                Plugin.Log.LogInfo($"[AutoTest] Bloom guard bypassed={Effects.BloomGuard.DevBypassed}, renderErrors={errors.Count}");
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
                RenderTexture.active = previous;
                Object.Destroy(go);
            }
            yield break;
        }
    }
}
