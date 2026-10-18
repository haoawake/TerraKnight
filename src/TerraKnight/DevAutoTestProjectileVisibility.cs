using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json;
using TerraKnight.Effects;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator ProjectileVisibilityTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            PlayerData.instance.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            yield return GoTo("Town", "left1");
            var particles = TerraParticles.Instance;
            var shots = TerraProjectiles.Instance;
            var dust = (TerraParticles.Dust[])AccessTools.Field(typeof(TerraParticles), "dust").GetValue(particles);
            var renderers = (SpriteRenderer[])AccessTools.Field(typeof(TerraParticles), "dustRenderers").GetValue(particles);
            var rows = new List<object>();
            int checks = 0, failures = 0;
            void Check(string name, bool pass)
            {
                checks++; rows.Add(new { Name = name, Passed = pass });
                if (!pass) { failures++; Plugin.Log.LogError("[AutoTest] 弹幕可见性/" + name + " 不通过"); }
            }
            Vector2 at = HeroController.instance.transform.position + Vector3.up * 8f;
            void Fill()
            {
                particles.Clear();
                for (int i = 0; i < dust.Length; i++)
                    particles.NewDust(TerraParticles.ToPx(at), 0, 0, 0, scale: 10f);
            }
            Fill();
            Check("400格满池", dust.All(d => d.Active));
            int fresh = particles.NewDust(TerraParticles.ToPx(at), 0, 0, 172);
            Check("满池仍接受新粒子", fresh >= 0 && dust[fresh].Type == 172);
            // Do not allow natural expiry to mask starvation: top up old effects every frame.
            foreach (var kind in new[] { ProjKind.WaterBolt, ProjKind.LunarFlare, ProjKind.Spark })
            {
                shots.Clear();
                int type = kind == ProjKind.WaterBolt ? 172 : kind == ProjKind.LunarFlare ? 229 : 6;
                Fill();
                for (int frame = 0; frame < 180; frame++)
                {
                    if (frame % 6 == 0)
                    {
                        if (kind == ProjKind.LunarFlare) shots.ShootLunarFlare(at, Vector2.down, 1, 0f, 0f, -1000f);
                        else shots.Shoot(kind, at, Vector2.right, 1, 0f, 0f);
                    }
                    yield return null;
                    if (frame % 30 == 29)
                    {
                        Check(kind + "/持续施法帧" + frame, dust.Any(d => d.Active && d.Type == type));
                        Check(kind + "/精灵绘制帧" + frame, Enumerable.Range(0, dust.Length).Any(i => dust[i].Active && dust[i].Type == type
                            && renderers[i] != null && renderers[i].enabled && renderers[i].sprite != null && renderers[i].color.maxColorComponent > 0f));
                    }
                }
                // Isolate the actual dust renderers in a camera texture to prove visible pixels.
                var cameraObject = new GameObject("Dust verification camera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.enabled = false; camera.orthographic = true; camera.orthographicSize = 12f;
                camera.transform.position = new Vector3(at.x, at.y, -10f);
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                camera.cullingMask = 1 << 31;
                var layers = new Dictionary<GameObject, int>();
                for (int i = 0; i < dust.Length; i++)
                    if (dust[i].Active && dust[i].Type == type && renderers[i] != null)
                    { var go = renderers[i].gameObject; layers[go] = go.layer; go.layer = 31; }
                var rt = new RenderTexture(256, 256, 16);
                var previous = RenderTexture.active;
                var texture = new Texture2D(256, 256, TextureFormat.RGB24, false);
                try
                {
                    camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                    texture.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); texture.Apply();
                    int pixels = texture.GetPixels32().Count(c => c.r > 8 || c.g > 8 || c.b > 8);
                    Check(kind + "/实际渲染像素" + pixels, pixels > 0);
                    File.WriteAllBytes(Path.Combine(DevCopy.SaveDir, kind + "-dust.png"), texture.EncodeToPNG());
                }
                finally
                {
                    foreach (var pair in layers) pair.Key.layer = pair.Value;
                    RenderTexture.active = previous; camera.targetTexture = null;
                    UnityEngine.Object.Destroy(texture); UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(cameraObject);
                }
            }
            shots.Clear(); particles.Clear();
            yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            int after = particles.NewDust(TerraParticles.ToPx(HeroController.instance.transform.position), 0, 0, 172);
            Check("切场景后可继续分配", after >= 0);
            File.WriteAllText(Path.Combine(DevCopy.SaveDir, "projectile-visibility-result.json"), JsonConvert.SerializeObject(new { Checks = checks, Failures = failures, Evidence = rows }, Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] 弹幕可见性 {checks} 项，失败 {failures}");
        }
    }
}
