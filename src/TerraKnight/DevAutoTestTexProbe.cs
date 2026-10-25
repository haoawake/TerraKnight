using System.Collections;
using System.IO;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "texprobe"): Terraria pictures a charm's look might use, written out as PNGs (tex_NAME.png beside the
    /// screenshots) with their sizes logged, to choose from by eye: bugs, bees, worms, spore and poison clouds, beams, honey.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator TexProbeTest()
        {
            yield return new WaitForSeconds(1f);
            string[] names =
            {
                "Projectile_181", "Projectile_566", "Projectile_189", "Projectile_190", "Projectile_374", "Projectile_375", "Projectile_307",
                "Projectile_567", "Projectile_568", "Projectile_569", "Projectile_570", "Projectile_571", "Projectile_228", "Projectile_229",
                "Projectile_132", "Projectile_156", "Projectile_157", "Projectile_173", "Projectile_973", "Projectile_984", "Projectile_985",
                "Projectile_297", "Projectile_316", "Projectile_265", "Projectile_303", "Projectile_664", "Projectile_665", "Projectile_666",
                "NPC_210", "NPC_211", "NPC_42", "NPC_176", "NPC_356", "NPC_595", "NPC_598", "Gore_420", "Extra_174", "Extra_89",
                "FlameRing", "Projectile_77", "Projectile_495", "Projectile_496", "Projectile_634", "Projectile_635", "Projectile_440",
            };
            foreach (var name in names)
            {
                string line;
                try
                {
                    var tex = Terraria.TerrariaAssets.Texture(name);
                    if (tex == null) line = "无";
                    else
                    {
                        // A readable copy (the loaded one may not be), written out.
                        var rt = RenderTexture.GetTemporary(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
                        Graphics.Blit(tex, rt);
                        var prev = RenderTexture.active;
                        RenderTexture.active = rt;
                        var copy = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false);
                        copy.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
                        copy.Apply();
                        RenderTexture.active = prev;
                        RenderTexture.ReleaseTemporary(rt);
                        File.WriteAllBytes(Path.Combine(outDir, $"tex_{name}.png"), copy.EncodeToPNG());
                        Object.Destroy(copy);
                        line = $"{tex.width}x{tex.height}";
                    }
                }
                catch (System.Exception e) { line = "读不到 " + e.GetType().Name; }
                Plugin.Log.LogInfo($"[AutoTest] 贴图 {name} {line}");
                yield return null;
            }
            Plugin.Log.LogInfo("[AutoTest] 贴图 结果 完成");
        }
    }
}
