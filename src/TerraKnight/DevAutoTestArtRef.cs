using System.Collections;
using System.Collections.Generic;
using System.IO;
using TerraKnight.Items;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "artref"): Terraria's own pictures of the items the mod renames for Hallownest, as they are (the whole
    /// sheet, native size), with each one's size and frame count in artref.txt, for drawing their new art.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly int[] artRefItems =
        {
            // Acid for lava: the hook, the tackle bag, the Hotline, the three baits, the moss and its bobber.
            4881, 5064, 2422, 4845, 4847, 4849, 5141, 4354,
            // The biome crates and their hardmode ones; the acid crates.
            3205, 3984, 5002, 5003, 3203, 3982, 3204, 3983, 3207, 3986, 3208, 3987, 4405, 4406, 4407, 4408, 3206, 3985, 4877, 4878,
            // The tier crates (their contents change, their names don't).
            2334, 3979, 2335, 3980, 2336, 3981,
            // The Chum Caster (no Blood Moon).
            4325,
        };

        IEnumerator ArtRefExport()
        {
            string folder = Path.Combine(DevCopy.SaveDir, "art-ref");
            Directory.CreateDirectory(folder);
            var lines = new List<string>();
            var names = new List<string>();
            foreach (int id in artRefItems) names.Add("Item_" + id);
            // The bobbers in acid colours to be (the Acidline's, the acid moss bobber's), and the Sleeping Angler.
            names.AddRange(new[] { "Projectile_382", "Projectile_988", "NPC_376" });
            foreach (string name in names)
            {
                var px = TerrariaAssets.Pixels(name, out int w, out int h);
                if (px == null) { lines.Add($"{name}\t缺"); continue; }
                var image = new Texture2D(w, h, TextureFormat.RGBA32, false);
                image.SetPixels32(px);
                image.Apply();
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
                Object.Destroy(image);
                int frames = ItemFrames.FramesOf(name);
                lines.Add($"{name}\t{w}x{h}\t{frames}");
                yield return null;
            }
            File.WriteAllLines(Path.Combine(folder, "artref.txt"), lines);
            Plugin.Log.LogInfo($"[AutoTest] 贴图参考 导出 {lines.Count} 张");
        }
    }
}
