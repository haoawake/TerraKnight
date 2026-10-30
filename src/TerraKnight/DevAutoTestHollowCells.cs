using System.Collections;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "hollowcells", the user's 10-30 "既然原版地图都能完美拼接起来，那能不能想办法让现在的泰拉瑞亚地图也做到呢"): how the
    /// game's map draws each room against the scene's real size (hollow_scales.tsv in the copy's dev folder), and Terraria's map's
    /// cells stretched into the game's rooms (UI.FullMap.DevCells) beside the game's drawings and Terraria's own layout, whole
    /// and close up. Screenshots; the save's map flag put back. Lines "[AutoTest] 原版布局 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator HollowCellsTest()
        {
            yield return GoTo("Crossroads_04", EntryGateOf("Crossroads_04"));
            yield return new WaitForSeconds(2f);
            var pd = PlayerData.instance;
            bool allWas = pd.mapAllRooms;
            pd.mapAllRooms = true;
            UI.WorldMap.DevExtendAtlas();
            UI.WorldMap.RevealWorld();
            UI.FullMap.DevHollow = true;
            for (float until = Time.realtimeSinceStartup + 4f; !UI.WorldMap.Open && Time.realtimeSinceStartup < until;)
            {
                UI.WorldMap.TryOpen();
                yield return null;
            }
            string scales = UI.FullMap.DevScales();
            System.IO.File.WriteAllText(System.IO.Path.Combine(outDir, "hollow_scales.tsv"), scales);
            Plugin.Log.LogInfo($"[AutoTest] 原版布局 房间比例 {scales.Split('\n').Length - 1} 行写进 hollow_scales.tsv");

            IEnumerator Look(string name, bool cells)
            {
                UI.FullMap.DevCells = cells;
                yield return new WaitForSecondsRealtime(1.5f);
                Plugin.Log.LogInfo($"[AutoTest] 原版布局 {name}：{(cells ? $"泰拉瑞亚格子画进 {UI.FullMap.DevCellsDrawn} 个房间" : "原版画")}，{UI.FullMap.DevView()}");
                Shot("Crossroads_04", name);
                yield return null;
            }
            UI.FullMap.DevZoom(0.01f);
            yield return Look("hollowcells_all", true);
            yield return Look("hollowcells_all_hk", false);
            UI.FullMap.DevCentre("Crossroads_04", new Vector2(80f, 15f));
            UI.FullMap.DevZoomTo(Screen.height / 4.5f);
            yield return Look("hollowcells_crossroads", true);
            yield return Look("hollowcells_crossroads_hk", false);
            UI.FullMap.DevCentre("Ruins1_27", new Vector2(40f, 40f));
            yield return Look("hollowcells_city", true);
            yield return Look("hollowcells_city_hk", false);
            UI.FullMap.DevCentre("Fungus2_14", new Vector2(40f, 40f));
            yield return Look("hollowcells_fungus", true);
            yield return Look("hollowcells_fungus_hk", false);
            UI.FullMap.DevCells = false;
            UI.WorldMap.Close();
            yield return new WaitForSecondsRealtime(0.3f);

            // Terraria's own layout, the same places, for comparison.
            UI.FullMap.DevHollow = false;
            for (float until = Time.realtimeSinceStartup + 4f; !UI.WorldMap.Open && Time.realtimeSinceStartup < until;)
            {
                UI.WorldMap.TryOpen();
                yield return null;
            }
            UI.WorldMap.DevPickLayer(1);
            UI.WorldMap.DevCentreScene("Crossroads_04");
            UI.WorldMap.DevZoom(0.45f);
            yield return new WaitForSecondsRealtime(3f);
            Shot("Crossroads_04", "hollowcells_terraria_all");
            yield return null;
            UI.WorldMap.DevZoom(3f);
            yield return new WaitForSecondsRealtime(1.5f);
            Shot("Crossroads_04", "hollowcells_terraria_crossroads");
            yield return null;
            UI.WorldMap.Close();
            pd.mapAllRooms = allWas;
            UI.FullMap.DevHollow = null;
        }
    }
}
