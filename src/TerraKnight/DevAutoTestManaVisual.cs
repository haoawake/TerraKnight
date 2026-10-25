using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator ManaVisualTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            bool oldNoFallDamage=mod.DevNoFallDamage;
            mod.DevNoFallDamage=true;
            yield return GoTo("Town", "left1");
            mod.ApplyCreative(false);
            PlayerData.instance.isInvincible = true;
            var inv = mod.Inventory;
            var saved = (ItemStack[])inv.Accessories.Clone();
            bool infinite = inv.Infinite;
            bool oldCameraEnabled=Player.TerraCamera.Enabled;
            var oldLook=Player.TerraCamera.DevLook;
            int checks = 0, failures = 0;
            var results = new List<object>();
            void Check(string name, bool pass, string detail)
            {
                checks++; if (!pass) failures++;
                results.Add(new { Name = name, Pass = pass, Detail = detail });
                Plugin.Log.LogInfo($"[AutoTest] 缺魔视觉 {name} {(pass ? "通过" : "不通过")} {detail}");
            }
            void Capture(string name)
            {
                var camera=GameCameras.instance.mainCamera;
                var oldTarget=camera.targetTexture;
                var oldActive=RenderTexture.active;
                var target=RenderTexture.GetTemporary(1280,720,24);
                var texture=new Texture2D(1280,720,TextureFormat.RGB24,false);
                try
                {
                    // A hidden window can yield an all-black swap-chain screenshot.
                    // Render the real scene camera and its effects into a readable target.
                    camera.targetTexture=target;
                    camera.Render();
                    RenderTexture.active=target;
                    texture.ReadPixels(new Rect(0,0,1280,720),0,0);
                    texture.Apply();
                    string path=Path.Combine(outDir,name+".png");
                    File.WriteAllBytes(path,texture.EncodeToPNG());
                    int visible=0;
                    foreach(var color in texture.GetPixels32())if(color.r>8||color.g>8||color.b>8)visible++;
                    Check("相机实景截图 "+name,visible>1000,$"visible pixels {visible}: {path}");
                }
                finally
                {
                    camera.targetTexture=oldTarget;
                    RenderTexture.active=oldActive;
                    RenderTexture.ReleaseTemporary(target);
                    UnityEngine.Object.Destroy(texture);
                }
            }
            try
            {
                Wear(); inv.Infinite = false;
                HeroController.instance.transform.position = new Vector2(110f, 12f);
                Player.TerraCamera.Enabled=true;
                Player.TerraCamera.DevLook=new Vector2(116f,12f);
                yield return new WaitForSeconds(0.5f);
                var aim = (Vector2)HeroController.instance.transform.position + new Vector2(20f, 0.5f);
                mod.DevAim = aim;
                inv.Slots[2] = ItemStack.Of(ItemType.LastPrism);
                inv.Selected = 2;
                mod.DevHold = true;
                for (float t = 0; t < 4.5f; t += Time.deltaTime)
                {
                    mod.Stats.Mana = mod.Stats.ManaMax;
                    if (mod.DevReady) mod.DevUseSelected(aim);
                    yield return null;
                }
                yield return new WaitForEndOfFrame();
                var full = TerraProjectiles.DevPrismVisual;
                int fullDamage = TerraProjectiles.DevPrismVisualDamage;
                Check("满蓝光束已聚合", full.x > 1.39f && full.y > 0.99f, full.ToString("F3"));
                Capture("mana_visual_prism_full");
                for (float t = 0; t < 0.8f; t += Time.deltaTime)
                {
                    mod.Stats.SpendMana(mod.Stats.Mana);
                    yield return null;
                }
                yield return new WaitForEndOfFrame();
                var empty = TerraProjectiles.DevPrismVisual;
                Check("缺魔光束宽度70%", mod.SlowMagicUse && Mathf.Abs(empty.x / full.x - 0.7f) < 0.001f, empty.ToString("F3"));
                Check("缺魔光束亮度25%", Mathf.Abs(empty.y / full.y - 0.25f) < 0.001f, empty.ToString("F3"));
                Check("视觉变化保留碰撞宽度", Mathf.Abs(empty.z-full.z)<0.001f, $"{full.z:F3} -> {empty.z:F3}");
                Check("视觉变化保留伤害", TerraProjectiles.DevPrismVisualDamage == fullDamage, $"{fullDamage} -> {TerraProjectiles.DevPrismVisualDamage}");
                Capture("mana_visual_prism_empty");
                for (float t = 0; t < 0.5f; t += Time.deltaTime)
                {
                    mod.Stats.Mana = mod.Stats.ManaMax;
                    yield return null;
                }
                yield return new WaitForEndOfFrame();
                var restored = TerraProjectiles.DevPrismVisual;
                Check("补蓝恢复宽度与亮度", !mod.SlowMagicUse && Mathf.Abs(restored.x-full.x)<0.001f && Mathf.Abs(restored.y-full.y)<0.001f, restored.ToString("F3"));
                Capture("mana_visual_prism_restored");
                mod.DevHold = false;
                TerraProjectiles.Instance.DevClear();
                yield return new WaitForSeconds(0.6f);

                foreach (bool slow in new[] {false, true})
                {
                    inv.Slots[2] = ItemStack.Of(ItemType.GlacierFang);
                    TerraProjectiles.DevGlacierVisual = new Vector4(-1, -1, -1, -1);
                    mod.DevHold = true;
                    bool observed = false;
                    for (float t = 0; t < 3f; t += Time.deltaTime)
                    {
                        if (slow) mod.Stats.SpendMana(mod.Stats.Mana);
                        else mod.Stats.Mana = mod.Stats.ManaMax;
                        if (mod.DevReady) mod.DevUseSelected(aim);
                        yield return new WaitForEndOfFrame();
                        var pulse = TerraProjectiles.DevGlacierVisual;
                        if (pulse.x < 10f || pulse.x >= 18f) continue;
                        observed = true;
                        Check(slow ? "寒霜尖牙缺魔脉冲提前淡出" : "寒霜尖牙满蓝脉冲仍亮",
                            slow ? pulse.w == 1f && pulse.y < 0.001f && pulse.z > 1.49f : pulse.w == 0f && pulse.y > 0.02f && pulse.z < 1.46f,
                            pulse.ToString("F3"));
                        Capture(slow ? "mana_visual_glacier_empty" : "mana_visual_glacier_full");
                        break;
                    }
                    Check("寒霜尖牙实际绘制被采样 "+slow, observed, "charge ticks 10..18");
                    mod.DevHold = false;
                    TerraProjectiles.Instance.DevClear();
                    yield return new WaitForSeconds(0.6f);
                }
                float[] Values(Vector4 v) => new[]{v.x,v.y,v.z,v.w};
                File.WriteAllText(Path.Combine(DevCopy.SaveDir, "mana-visual.json"), JsonConvert.SerializeObject(new {Checks=checks, Failures=failures, Full=Values(full), Empty=Values(empty), Restored=Values(restored), Results=results}, Formatting.Indented));
                Plugin.Log.LogInfo($"[AutoTest] 缺魔视觉合计 {checks-failures}/{checks} {(failures==0 ? "通过" : "不通过")}");
            }
            finally
            {
                mod.DevHold = false; mod.DevAim = null;
                TerraProjectiles.Instance?.DevClear();
                for (int i=0;i<saved.Length;i++) inv.Accessories[i]=saved[i];
                inv.Infinite=infinite;
                mod.DevNoFallDamage=oldNoFallDamage;
                Player.TerraCamera.Enabled=oldCameraEnabled;
                Player.TerraCamera.DevLook=oldLook;
                inv.Slots[2]=default;
                mod.Stats.Mana=mod.Stats.ManaMax;
            }
        }
    }
}
