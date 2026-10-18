using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        /// <summary>
        /// The monster book open a minute and a half, the pointer going over its entries and drops and an entry chosen every
        /// two seconds: each ten seconds' frame times, the garbage collections and the managed heap, the textures in memory,
        /// the largest font atlas and the book's own drawing time, to see what grows the longer it stays open.
        /// </summary>
        IEnumerator BookLeak17()
        {
            var added = MarkAllBeaten();
            int vsync = QualitySettings.vSyncCount, fps = Application.targetFrameRate;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 1000;
            InventoryGui.Open = true;
            InventoryGui.DevBestiary(true, 0);
            UI.TerraCursor.DevDrives = true;
            yield return new WaitForSeconds(1f);
            int Textures() => Resources.FindObjectsOfTypeAll<Texture2D>().Length;
            string Atlas()
            {
                int best = 0;
                string name = "-";
                foreach (var f in Resources.FindObjectsOfTypeAll<Font>())
                {
                    var t = f != null && f.material != null ? f.material.mainTexture : null;
                    if (t != null && t.width * t.height > best) { best = t.width * t.height; name = $"{f.name} {t.width}×{t.height}"; }
                }
                return name;
            }
            var lines = new List<string>();
            var means = new List<float>();
            int entry = 0;
            for (int w = 0; w < 9; w++)
            {
                for (int i = 0; i < InventoryGui.DevBookTicks.Length; i++) InventoryGui.DevBookTicks[i] = 0;
                InventoryGui.DevBookFrames = 0;
                int gc0 = System.GC.CollectionCount(0), tex0 = Textures();
                var ms = new List<float>();
                float windowEnd = Time.realtimeSinceStartup + 10f, nextMove = 0f, nextPick = 0f;
                int hover = 0;
                while (Time.realtimeSinceStartup < windowEnd)
                {
                    float now = Time.realtimeSinceStartup;
                    if (now >= nextPick)
                    {
                        nextPick = now + 2f;
                        InventoryGui.DevBestiary(true, entry++);
                    }
                    if (now >= nextMove)
                    {
                        nextMove = now + 0.25f;
                        // Over the grid's entries in turn, now and then over the chosen entry's drops (the right half).
                        var r = InventoryGui.DevRectOf("BestiaryEntry", hover++ % 30);
                        UI.TerraCursor.DevAt = r.HasValue && hover % 4 != 0 ? r.Value.center : new Vector2(Screen.width * 0.72f, Screen.height * (0.62f + 0.06f * (hover % 5)));
                    }
                    yield return null;
                    ms.Add(Time.unscaledDeltaTime * 1000f);
                }
                int frames = Mathf.Max(1, InventoryGui.DevBookFrames);
                double toMs = 1000.0 / System.Diagnostics.Stopwatch.Frequency / frames;
                double book = (InventoryGui.DevBookTicks[0] + InventoryGui.DevBookTicks[1] + InventoryGui.DevBookTicks[2]) * toMs;
                means.Add(Mean(ms));
                string line = $"第 {w + 1} 个 10 秒：{Summary(ms)}，图鉴自己画 {book:F2} ms/帧（列表 {InventoryGui.DevBookTicks[0] * toMs:F2} 格子 {InventoryGui.DevBookTicks[1] * toMs:F2} 详情 {InventoryGui.DevBookTicks[2] * toMs:F2}），"
                    + $"GC {System.GC.CollectionCount(0) - gc0} 次，托管堆 {System.GC.GetTotalMemory(false) / 1048576f:F0} MB，贴图 {Textures()}（+{Textures() - tex0}），字体图集 {Atlas()}";
                lines.Add(line);
                Plugin.Log.LogInfo("[AutoTest] 图鉴久开 " + line);
            }
            UI.TerraCursor.DevDrives = false;
            UI.TerraCursor.DevAt = null;
            FxCheck("图鉴开久了不越来越卡", means.Count >= 2 && means[means.Count - 1] <= means[0] * 1.25f + 1f, string.Join("；", lines));
            InventoryGui.DevBestiary(false);
            InventoryGui.Open = false;
            QualitySettings.vSyncCount = vsync;
            Application.targetFrameRate = fps;
            foreach (var r in added) Bestiary.Kills.Remove(r);
        }
    }
}
