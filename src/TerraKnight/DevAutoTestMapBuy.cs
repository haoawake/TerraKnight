using System.Collections;
using UnityEngine;
using TerraKnight.UI;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "mapbuy"): the Forgotten Crossroads' map bought (PlayerData.mapCrossroads, as Cornifer
    /// sells it): every room of the area on the map, those not visited from the atlas, and the Life
    /// Crystals there still not drawn where their spot hasn't been seen; the full map photographed.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator MapBuyTest()
        {
            yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            yield return new WaitForSeconds(2f);
            PlayerData.instance.isInvincible = true;
            const string area = "遗忘十字路";
            Plugin.Log.LogInfo($"[AutoTest] 买地图 买之前 {WorldMap.DevChart(area)}");
            PlayerData.instance.SetBool("mapCrossroads", true);
            yield return new WaitForSeconds(2f);
            string after = WorldMap.DevChart(area);
            string crystals = WorldMap.DevCrystals(area, out int drawnUnseen);
            Plugin.Log.LogInfo($"[AutoTest] 买地图 买之后 {after}；生命水晶：{crystals}");
            WorldMap.DevSetOpen(true);
            WorldMap.DevZoom(1.2f);
            WorldMap.DevCentreScene("Crossroads_04");
            yield return new WaitForSeconds(0.6f);
            Shot("Crossroads_01", "mapbuy_full");
            yield return null;
            WorldMap.DevSetOpen(false);
            bool pass = after.Contains("画出来的") && WorldMap.DevChartCount(area) > 30 && drawnUnseen == 0;
            Plugin.Log.LogInfo($"[AutoTest] 买地图 十字路买到的场景 {WorldMap.DevChartCount(area)} 个，没看到却画出来的生命水晶 {drawnUnseen} 个 {(pass ? "通过" : "不通过")}");
        }
    }
}
