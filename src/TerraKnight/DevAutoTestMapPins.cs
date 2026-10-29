using System.Collections;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "mappins", UI.WorldMapPins, the user's 10-28 "让收藏家的地图有用一点吧…高亮所有毛毛虫和座椅"): the full map
    /// opened without the pins, then with the Collector's Map (grubs and benches), then with only the Bench Pin, and with two of
    /// the grubs freed: how many pins are drawn each time; a screenshot. The player's flags put back. Lines "[AutoTest] 地图图钉 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator MapPinsTest()
        {
            yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            yield return new WaitForSeconds(2f);
            var pd = PlayerData.instance;
            bool grub0 = pd.hasPinGrub, bench0 = pd.hasPinBench;
            var freed0 = new System.Collections.Generic.List<string>(pd.scenesGrubRescued ?? new System.Collections.Generic.List<string>());
            int ok = 0, all = 0;
            void Check(bool pass, string what) { all++; if (pass) ok++; Plugin.Log.LogInfo($"[AutoTest] 地图图钉 {what} {(pass ? "通过" : "不通过")}"); }
            IEnumerator Look(System.Action<int, int> got)
            {
                // The map opens only while the game isn't holding the player: tried again for a few seconds.
                for (float until = Time.realtimeSinceStartup + 4f; !UI.WorldMap.Open && Time.realtimeSinceStartup < until;)
                {
                    UI.WorldMap.TryOpen();
                    yield return null;
                }
                yield return new WaitForSecondsRealtime(0.6f);
                got(UI.WorldMap.DevGrubPins, UI.WorldMap.DevBenchPins);
                Plugin.Log.LogInfo($"[AutoTest] 地图图钉 诊断：地图开着 {UI.WorldMap.Open}，贴图 {UI.WorldMap.DevPinSprites}，不在本层 {UI.WorldMap.DevPinsOffLayer}，不在地图上 {UI.WorldMap.DevPinsOffMap}，画面外 {UI.WorldMap.DevPinsClipped}");
            }
            IEnumerator Close()
            {
                if (UI.WorldMap.Open) UI.WorldMap.Toggle();
                yield return new WaitForSecondsRealtime(0.3f);
            }
            pd.scenesGrubRescued.Clear();
            pd.hasPinGrub = false;
            pd.hasPinBench = false;
            int g = 0, b = 0;
            yield return Look((x, y) => { g = x; b = y; });
            Check(g == 0 && b == 0, $"没有地图图钉：幼虫 {g}，长椅 {b}");
            yield return Close();
            pd.hasPinGrub = true;
            yield return Look((x, y) => { g = x; b = y; });
            // The map shown (this test save's rooms revealed), the Crossroads in view and then a close look at one grub.
            UI.WorldMap.RevealWorld();
            UI.WorldMap.DevCentreCell("Crossroads_01", new Vector2Int(40, 20));
            UI.WorldMap.DevZoom(0.9f);
            yield return new WaitForSecondsRealtime(5f);
            Shot("Crossroads_01", "mappins_collector");
            yield return null;
            UI.WorldMap.DevCentreCell("Crossroads_31", new Vector2Int(19, 13));
            UI.WorldMap.DevZoom(3f);
            yield return new WaitForSecondsRealtime(3f);
            Shot("Crossroads_01", "mappins_close");
            yield return null;
            Check(g >= 40 && b >= 35, $"收藏家地图：幼虫 {g} 个（共 46），长椅 {b} 张");
            int all0 = g;
            yield return Close();
            pd.scenesGrubRescued.Add("Crossroads_31");
            pd.scenesGrubRescued.Add("Ruins2_11");
            yield return Look((x, y) => { g = x; b = y; });
            Check(g == all0 - 2, $"救出两只后：幼虫 {g} 个（之前 {all0}）");
            yield return Close();
            pd.hasPinGrub = false;
            pd.hasPinBench = true;
            yield return Look((x, y) => { g = x; b = y; });
            Check(g == 0 && b >= 35, $"只有长椅图钉：幼虫 {g}，长椅 {b}");
            yield return Close();
            pd.hasPinGrub = grub0;
            pd.hasPinBench = bench0;
            pd.scenesGrubRescued.Clear();
            pd.scenesGrubRescued.AddRange(freed0);
            Plugin.Log.LogInfo($"[AutoTest] 地图图钉 合计 {ok}/{all}");
        }
    }
}
