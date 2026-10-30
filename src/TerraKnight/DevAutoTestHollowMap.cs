using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "hollowmap", UI.FullMap, the user's 10-30 "原版的地图能不能应用回来，不过是按m打开，然后晶塔，npc，虫子，长椅什么的依旧
    /// 显示"): Hollow Knight's map opened with nothing bought (only the rooms been in), with the Crossroads' map and every pin
    /// (Cornifer's sketch, Iselda's pins, grubs and benches), and with every room; Dirtmouth up close with its townsfolk; then
    /// Terraria's map still opening when that one is chosen. Counts and screenshots; the player's flags put back. Lines
    /// "[AutoTest] 原版地图 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator HollowMapTest()
        {
            yield return GoTo("Crossroads_04", EntryGateOf("Crossroads_04"));
            yield return new WaitForSeconds(2f);
            var pd = PlayerData.instance;
            string[] flags =
            {
                "hasMap", "mapCrossroads", "mapGreenpath", "mapAbyss", "mapCity", "mapCliffs", "mapMines", "mapDeepnest", "mapFogCanyon", "mapFungalWastes",
                "mapOutskirts", "mapRoyalGardens", "mapRestingGrounds", "mapWaterways", "mapAllRooms",
                "hasPinStag", "hasPinShop", "hasPinSpa", "hasPinTram", "hasPinGhost", "hasPinCocoon", "hasPinDreamPlant", "hasPinBench", "hasPinGrub",
            };
            var was = new Dictionary<string, bool>();
            foreach (var f in flags) was[f] = pd.GetBool(f);
            int ok = 0, all = 0;
            void Check(bool pass, string what) { all++; if (pass) ok++; Plugin.Log.LogInfo($"[AutoTest] 原版地图 {what} {(pass ? "通过" : "不通过")}"); }
            void Set(bool on, params string[] names) { foreach (var f in names) pd.SetBool(f, on); }
            void Report(string when) => Plugin.Log.LogInfo($"[AutoTest] 原版地图 {when}：房间 {UI.FullMap.RoomCount}，显示 {UI.FullMap.ShownRooms}（草图 {UI.FullMap.SketchedRooms}），" +
                $"原版图钉 {UI.FullMap.ShownPins}，幼虫 {UI.WorldMap.DevGrubPins}，长椅 {UI.WorldMap.DevBenchPins}，居民 {UI.WorldMap.DevNpcHeadCount}；{UI.FullMap.DevView()}；图钉：{UI.FullMap.DevPins()}；区域名：{UI.FullMap.DevLabels()}");
            IEnumerator Open(bool hollow)
            {
                UI.FullMap.DevHollow = hollow;
                // The map opens only while the game isn't holding the player: tried again for a few seconds.
                for (float until = Time.realtimeSinceStartup + 4f; !UI.WorldMap.Open && Time.realtimeSinceStartup < until;)
                {
                    UI.WorldMap.TryOpen();
                    yield return null;
                }
                yield return new WaitForSecondsRealtime(0.8f);
            }
            IEnumerator Close()
            {
                if (UI.WorldMap.Open) UI.WorldMap.Close();
                yield return new WaitForSecondsRealtime(0.3f);
            }

            Set(false, flags);
            UI.FullMap.DevZoomTo(0f);
            yield return Open(true);
            Check(UI.WorldMap.HollowShown, "M 打开的是空洞骑士地图");
            Plugin.Log.LogInfo($"[AutoTest] 原版地图 居民：{UI.WorldMap.DevNpcPlaces()}");
            Report("什么都没买");
            Plugin.Log.LogInfo($"[AutoTest] 原版地图 头像：{UI.FullMap.DevUnderHead()}；{UI.FullMap.DevHeadMath()}");
            Check(UI.FullMap.ShownRooms >= 1 && UI.FullMap.SketchedRooms == 0 && UI.FullMap.ShownPins == 0, "没买地图：只显示到过的房间，没有草图和图钉");
            UI.FullMap.DevZoom(1.6f);
            yield return new WaitForSecondsRealtime(0.5f);
            Shot("Crossroads_04", "hollowmap_plain");
            yield return null;
            yield return Close();

            // A creative save: every room, with nothing bought.
            bool creativeWas = TerraMod.Instance.Creative;
            TerraMod.Instance.ApplyCreative(true);
            yield return Open(true);
            Check(UI.FullMap.ShownRooms == UI.FullMap.RoomCount && UI.FullMap.SketchedRooms == 0, $"创造模式：所有房间都显示（{UI.FullMap.ShownRooms}/{UI.FullMap.RoomCount}）");
            yield return Close();
            TerraMod.Instance.ApplyCreative(creativeWas);

            Set(true, "hasMap", "mapCrossroads", "mapGreenpath", "hasPinStag", "hasPinShop", "hasPinSpa", "hasPinTram", "hasPinGhost", "hasPinCocoon", "hasPinDreamPlant", "hasPinBench", "hasPinGrub");
            yield return Open(true);
            Report("买了十字路和苍绿之径的地图、所有图钉");
            Check(UI.FullMap.SketchedRooms > 0, $"买了地图：显示柯尼法的草图（{UI.FullMap.SketchedRooms} 间）");
            Check(UI.FullMap.ShownPins > 0, $"伊塞尔达的图钉（{UI.FullMap.ShownPins} 个）");
            Check(UI.WorldMap.DevGrubPins > 0 && UI.WorldMap.DevBenchPins > 0, $"收藏家地图：幼虫 {UI.WorldMap.DevGrubPins}，长椅 {UI.WorldMap.DevBenchPins}");
            Shot("Crossroads_04", "hollowmap_crossroads");
            yield return null;
            UI.FullMap.DevZoom(2.5f);
            yield return new WaitForSecondsRealtime(0.5f);
            Shot("Crossroads_04", "hollowmap_close");
            yield return null;
            yield return Close();

            Set(true, flags);
            yield return Open(true);
            UI.FullMap.DevZoom(0.01f);
            yield return new WaitForSecondsRealtime(0.6f);
            Report("所有房间");
            Check(UI.FullMap.ShownRooms == UI.FullMap.RoomCount, $"所有房间都显示（{UI.FullMap.ShownRooms}/{UI.FullMap.RoomCount}）");
            Shot("Crossroads_04", "hollowmap_all");
            yield return null;
            bool town = UI.FullMap.DevCentre("Town", new Vector2(130f, 12f));
            UI.FullMap.DevZoom(30f);
            yield return new WaitForSecondsRealtime(0.6f);
            Report("德特茅斯近看");
            Check(town, "德特茅斯在地图上");
            Shot("Crossroads_04", "hollowmap_town");
            yield return null;
            yield return Close();

            yield return Open(false);
            Check(UI.WorldMap.Open && !UI.WorldMap.HollowShown, "选泰拉瑞亚地图时打开泰拉瑞亚地图");
            yield return Close();

            // Dirtmouth with its townsfolk: their heads on the game's map, the player's among them.
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(4f);
            int live = Town.TownNpcs.Count;
            yield return Open(true);
            UI.FullMap.DevZoomTo(0f);
            yield return new WaitForSecondsRealtime(0.6f);
            int heads = UI.WorldMap.DevNpcHeadCount;
            Report("德特茅斯");
            Plugin.Log.LogInfo($"[AutoTest] 原版地图 德特茅斯头像：{UI.FullMap.DevUnderHead()}；{UI.FullMap.DevHeadMath()}");
            Plugin.Log.LogInfo($"[AutoTest] 原版地图 居民：{UI.WorldMap.DevNpcHeads()}；{UI.WorldMap.DevNpcPlaces()}");
            Check(heads >= live, $"居民头像（镇上 {live} 个，画了 {heads} 个）");
            Shot("Town", "hollowmap_townsfolk");
            yield return null;
            yield return Close();

            // The Wayward Compass worn: its way to the next fight on the game's map.
            bool pageWas = Items.Charms.Enabled, gotWas = pd.GetBool("gotCharm_2"), wornWas = Items.Charms.Worn(2);
            Items.Charms.DevSetPage(true);
            Items.Charms.DevAnywhere = true;
            pd.SetBool("gotCharm_2", true);
            if (!Items.Charms.Worn(2)) Items.Charms.Click(2);
            yield return Open(true);
            UI.FullMap.DevZoomTo(Screen.height / 20f);
            yield return new WaitForSecondsRealtime(0.8f);
            Check(UI.WorldMap.DevFullCompassDots > 0, $"指南针路线画在原版地图上（{UI.WorldMap.DevFullCompassDots} 个点；{UI.WorldMap.DevCompass()}）");
            Shot("Town", "hollowmap_compass");
            yield return null;
            yield return Close();

            // The corner maps: the minimap and the overlay show the game's map too, Terraria's again when that one is chosen.
            int styleWas = UI.WorldMap.Style;
            UI.WorldMap.DevStyle(1);
            yield return new WaitForSecondsRealtime(0.6f);
            Check(UI.WorldMap.HollowCorner, $"小地图用原版地图（{UI.FullMap.DevCorner()}）");
            Shot("Town", "hollowmap_minimap");
            yield return null;
            UI.WorldMap.DevStyle(2);
            yield return new WaitForSecondsRealtime(0.6f);
            Check(UI.WorldMap.HollowCorner, $"覆盖地图用原版地图（{UI.FullMap.DevCorner()}）");
            Shot("Town", "hollowmap_overlay");
            yield return null;
            UI.FullMap.DevHollow = false;
            UI.WorldMap.DevStyle(1);
            yield return new WaitForSecondsRealtime(0.4f);
            Check(!UI.WorldMap.HollowCorner, "选泰拉瑞亚地图时小地图是泰拉瑞亚的");
            Shot("Town", "hollowmap_minimap_terraria");
            yield return null;
            UI.WorldMap.DevStyle(styleWas);
            if (Items.Charms.Worn(2) != wornWas) Items.Charms.Click(2);
            pd.SetBool("gotCharm_2", gotWas);
            Items.Charms.DevAnywhere = false;
            Items.Charms.DevSetPage(pageWas);

            foreach (var kv in was) pd.SetBool(kv.Key, kv.Value);
            UI.FullMap.DevHollow = null;
            Plugin.Log.LogInfo($"[AutoTest] 原版地图 合计 {ok}/{all}");
        }
    }
}
