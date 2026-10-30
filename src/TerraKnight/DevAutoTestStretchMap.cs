using System.Collections;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "stretchmap", UI.WorldMapStretch, the user's 10-30 choice B): Terraria's map with its rooms stretched as
    /// the layout has them, the whole world revealed: how many gates meet and how many are marked apart, the player's head
    /// on his room's cells, then screenshots of the whole map and a few areas close up. Lines "[AutoTest] 伸缩地图 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator StretchMapTest()
        {
            yield return GoTo("Crossroads_04", EntryGateOf("Crossroads_04"));
            yield return new WaitForSeconds(2f);
            int ok = 0, all = 0;
            void Check(bool pass, string what) { all++; if (pass) ok++; Plugin.Log.LogInfo($"[AutoTest] 伸缩地图 {what} {(pass ? "通过" : "不通过")}"); }
            UI.WorldMap.DevExtendAtlas();
            UI.WorldMap.RevealWorld();
            string counts = UI.WorldMap.DevGateCounts(out int marked, out int met);
            Plugin.Log.LogInfo($"[AutoTest] 伸缩地图 {counts}，伸缩的房间 {UI.WorldMap.DevStretched} 个，Crossroads_04 伸缩 {UI.WorldMap.DevStretch("Crossroads_04")}");
            Check(UI.WorldMap.DevStretched > 0, "布局带着房间的伸缩");
            UI.FullMap.DevHollow = false;
            for (float until = Time.realtimeSinceStartup + 4f; !UI.WorldMap.Open && Time.realtimeSinceStartup < until;)
            {
                UI.WorldMap.TryOpen();
                yield return null;
            }
            UI.WorldMap.DevPickLayer(1);
            // The head on the room's own cells: what the map names under it is this room's.
            var hero = HeroController.instance;
            string under = UI.WorldMap.DevNameAtWorld(hero.transform.position);
            Plugin.Log.LogInfo($"[AutoTest] 伸缩地图 头像下：{under}");
            Check(under != null && under != "不在地图上", $"头像在自己房间的格子上（{under}）");

            IEnumerator Look(string scene, float zoom, string name)
            {
                UI.WorldMap.DevCentreScene(scene);
                UI.WorldMap.DevZoom(zoom);
                yield return new WaitForSecondsRealtime(2.5f);
                Shot("Crossroads_04", name);
                yield return null;
            }
            yield return Look("Crossroads_04", 0.45f, "stretch_all");
            yield return Look("Crossroads_04", 3f, "stretch_crossroads");
            yield return Look("Crossroads_13", 3f, "stretch_crossroads_east");
            yield return Look("Ruins1_27", 2.2f, "stretch_city");
            yield return Look("Fungus2_14", 2.2f, "stretch_fungus");
            yield return Look("Mines_25", 2.2f, "stretch_mines");
            yield return Look("Fungus1_10", 2.2f, "stretch_greenpath");
            UI.WorldMap.Close();
            UI.FullMap.DevHollow = null;
            Plugin.Log.LogInfo($"[AutoTest] 伸缩地图 合计 {ok}/{all}");
        }
    }
}
