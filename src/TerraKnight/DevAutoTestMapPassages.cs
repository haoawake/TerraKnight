using System.Collections;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "mappassages", UI.WorldMapPassages, the user's 10-30 "地图要不就按照你说的试一试吧"): Terraria's map with the
    /// whole world revealed: every passage drawn between gates apart, how many gates still carry arrows, then each of a list
    /// of passages centred close up: drawn, its gates without arrows, a screenshot. TK_PASSAGES "Scene:gate@zoom,…" picks
    /// others. Lines "[AutoTest] 地图通道 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator MapPassagesTest()
        {
            yield return GoTo("Crossroads_04", EntryGateOf("Crossroads_04"));
            yield return new WaitForSeconds(2f);
            int ok = 0, all = 0;
            void Check(bool pass, string what) { all++; if (pass) ok++; Plugin.Log.LogInfo($"[AutoTest] 地图通道 {what} {(pass ? "通过" : "不通过")}"); }
            UI.WorldMap.DevExtendAtlas();
            UI.WorldMap.RevealWorld();
            var list = UI.WorldMap.DevPassages();
            Plugin.Log.LogInfo($"[AutoTest] 地图通道 共 {list.Count} 条，{UI.WorldMap.DevGateCounts(out _, out _)}");
            foreach (var line in list) Plugin.Log.LogInfo($"[AutoTest] 地图通道   {line}");
            Check(list.Count > 0, "有通道");
            UI.FullMap.DevHollow = false;
            for (float until = Time.realtimeSinceStartup + 4f; !UI.WorldMap.Open && Time.realtimeSinceStartup < until;)
            {
                UI.WorldMap.TryOpen();
                yield return null;
            }
            string picks = System.Environment.GetEnvironmentVariable("TK_PASSAGES")
                ?? "Crossroads_09:right1@8,Fungus2_14:bot1@7,Mines_04:right1@8,Fungus2_29:bot1@6,Crossroads_16:left1@6,Crossroads_05:left1@6,"
                 + "Cliffs_01:right1@5,Waterways_01:left1@5,Crossroads_01:top1@5,Deepnest_East_14:left1@5,Crossroads_03:left2@4,"
                 + "Fungus2_13:left3@4,Waterways_04b:left1@3.5,Crossroads_08:right2@3.5,Crossroads_13:right1@3.5,Mines_23:top1@3";
            foreach (var pick in picks.Split(','))
            {
                int at = pick.IndexOf('@'), colon = pick.IndexOf(':');
                if (colon < 0) continue;
                string scene = pick.Substring(0, colon), gate = pick.Substring(colon + 1, (at < 0 ? pick.Length : at) - colon - 1);
                float zoom = at < 0 ? 5f : float.Parse(pick.Substring(at + 1), System.Globalization.CultureInfo.InvariantCulture);
                if (!UI.WorldMap.DevCentrePassage(scene, gate)) { Check(false, $"{scene}:{gate} 没有通道"); continue; }
                UI.WorldMap.DevZoom(zoom);
                yield return new WaitForSecondsRealtime(1.5f);
                bool joined = UI.WorldMap.DevJoined(scene, gate);
                bool arrow = UI.WorldMap.DevGateScreen(scene, gate).HasValue;
                Check(UI.WorldMap.DevPassagesDrawn > 0 && joined && !arrow, $"{scene}:{gate} 画出 {UI.WorldMap.DevPassagesDrawn} 条，接上 {joined}，箭头 {arrow}");
                Shot("Crossroads_04", "passage_" + scene + "_" + gate);
                yield return null;
            }
            UI.WorldMap.Close();
            // The corner map by a passage's gate: drawn there too.
            UI.WorldMap.DevStyle(1);
            yield return GoTo("Crossroads_16", "left1");
            yield return new WaitForSeconds(2f);
            Check(UI.WorldMap.DevPassagesDrawn > 0 && UI.WorldMap.DevJoined("Crossroads_16", "left1"), $"小地图画出 {UI.WorldMap.DevPassagesDrawn} 条");
            Shot("Crossroads_16", "passage_minimap");
            yield return null;
            UI.FullMap.DevHollow = null;
            Plugin.Log.LogInfo($"[AutoTest] 地图通道 合计 {ok}/{all}");
        }
    }
}
