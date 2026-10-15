using System.Collections;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "maplayers"): the map's layers. In Dirtmouth the corner map draws the shallow layer (Dirtmouth, not
    /// the station beside its door); in the station the deep one (the station, not Dirtmouth). The full map opens on
    /// the player's layer and its tabs show either. The player's head on the body's middle, and the neighbours loaded
    /// where the map has them (their join against the layout).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator MapLayersTest()
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 地图分层 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            UI.WorldMap.DevStyle(1);
            yield return GoTo("Town", "left1");
            PlayerData.instance.isInvincible = true;
            var hero = HeroController.instance;
            hero.transform.position = new Vector2(60f, 12f);
            yield return new WaitForSeconds(2f);
            Check("德特茅斯是浅层", !UI.WorldMap.DevDeep("Town") && !UI.WorldMap.DevViewDeep, $"深层 {UI.WorldMap.DevDeep("Town")}，画的层 深={UI.WorldMap.DevViewDeep}");
            Check("浅层画德特茅斯、不画车站", UI.WorldMap.DevShown("Town") && !UI.WorldMap.DevShown("Room_Town_Stag_Station"),
                $"德特茅斯 {UI.WorldMap.DevShown("Town")}，车站 {UI.WorldMap.DevShown("Room_Town_Stag_Station")}");
            HeadReport("Town");
            NeighbourReport();
            UI.WorldMap.DevMiniZoom(3f);
            yield return new WaitForSeconds(0.3f);
            Shot("Town", "maplayers_mini_town");

            yield return GoTo("Room_Town_Stag_Station", "left1");
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(2f);
            Check("车站是深层", UI.WorldMap.DevDeep("Room_Town_Stag_Station") && UI.WorldMap.DevViewDeep, $"画的层 深={UI.WorldMap.DevViewDeep}");
            Check("小地图只画深层", UI.WorldMap.DevShown("Room_Town_Stag_Station") && !UI.WorldMap.DevShown("Town") && !UI.WorldMap.DevShown("Crossroads_01"),
                $"车站 {UI.WorldMap.DevShown("Room_Town_Stag_Station")}，德特茅斯 {UI.WorldMap.DevShown("Town")}，十字路 {UI.WorldMap.DevShown("Crossroads_01")}");
            HeadReport("Room_Town_Stag_Station");
            Shot("Room_Town_Stag_Station", "maplayers_mini_station");
            UI.WorldMap.DevMiniZoom(1.05f);

            UI.FullMap.DevSetOpen(true);
            yield return new WaitForSeconds(0.4f);
            Check("大地图先开在当前层", UI.WorldMap.DevViewDeep && UI.WorldMap.DevShown("Room_Town_Stag_Station"), $"深={UI.WorldMap.DevViewDeep}");
            Check("图层按钮画出来", UI.WorldMap.DevLayerTab(0).width > 0f && UI.WorldMap.DevLayerTab(2).xMin > UI.WorldMap.DevLayerTab(1).xMax,
                $"{UI.WorldMap.DevLayerTab(0)} {UI.WorldMap.DevLayerTab(1)} {UI.WorldMap.DevLayerTab(2)}");
            Shot("Room_Town_Stag_Station", "maplayers_full_current");
            UI.WorldMap.DevPickLayer(1);
            yield return new WaitForSeconds(0.3f);
            Check("手动看浅层", !UI.WorldMap.DevViewDeep && UI.WorldMap.DevShown("Town") && !UI.WorldMap.DevShown("Room_Town_Stag_Station"),
                $"德特茅斯 {UI.WorldMap.DevShown("Town")}，车站 {UI.WorldMap.DevShown("Room_Town_Stag_Station")}");
            Shot("Room_Town_Stag_Station", "maplayers_full_shallow");
            UI.WorldMap.DevPickLayer(2);
            yield return new WaitForSeconds(0.3f);
            Check("手动看深层", UI.WorldMap.DevViewDeep && !UI.WorldMap.DevShown("Town"), $"深={UI.WorldMap.DevViewDeep}");
            Shot("Room_Town_Stag_Station", "maplayers_full_deep");
            UI.FullMap.DevSetOpen(false);
            yield return new WaitForSeconds(0.3f);
            UI.WorldMap.DevPickLayer(1);
            UI.FullMap.DevSetOpen(true);
            yield return new WaitForSeconds(0.3f);
            Check("重开回到当前层", UI.WorldMap.DevViewDeep, $"深={UI.WorldMap.DevViewDeep}");
            UI.FullMap.DevSetOpen(false);

            // Mato's hut sits over the Howling Cliffs on the map.
            yield return GoTo("Room_nailmaster", "left1");
            yield return new WaitForSeconds(1.5f);
            Check("骨钉大师小屋不叠在呼啸悬崖上", UI.WorldMap.DevViewDeep && !UI.WorldMap.DevShown("Cliffs_02"), $"深={UI.WorldMap.DevViewDeep}，悬崖 {UI.WorldMap.DevShown("Cliffs_02")}");
            yield return GoTo("Cliffs_02", "door1");
            yield return new WaitForSeconds(1.5f);
            Check("回到悬崖画浅层", !UI.WorldMap.DevViewDeep && UI.WorldMap.DevShown("Cliffs_02") && !UI.WorldMap.DevShown("Room_nailmaster"),
                $"悬崖 {UI.WorldMap.DevShown("Cliffs_02")}，小屋 {UI.WorldMap.DevShown("Room_nailmaster")}");
            HeadReport("Cliffs_02");
            NeighbourReport();
            Plugin.Log.LogInfo($"[AutoTest] 地图分层 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }

        /// <summary>The head's place on the map against the body's cell (the cells revealed round it).</summary>
        static void HeadReport(string scene)
        {
            var hero = HeroController.instance;
            var live = TerraWorld.Live;
            var body = hero.GetComponent<Collider2D>();
            Vector2 centre = body.bounds.center, pivot = hero.transform.position;
            UI.WorldMap.TryLayout(live?.SceneName, out var at);
            var cell = CellGrid.WorldToCell(centre) - (live != null ? live.CellOffset : Vector2Int.zero) + at;
            var head = UI.WorldMap.DevHeadCell;
            Plugin.Log.LogInfo($"[AutoTest] 地图分层 头像 {scene}：身体中心 {centre:F2}，原点 {pivot:F2}（差 {(centre - pivot).y:F2}），身体所在格 {cell}，头像格 {head:F1}" +
                $"（差 {head.x - cell.x - 0.5f:F2},{head.y - cell.y - 0.5f:F2} 格）");
        }

        /// <summary>The neighbours loaded beside the scene: where the world joins them against where the map has them.</summary>
        static void NeighbourReport()
        {
            var live = TerraWorld.Live;
            if (live == null || !UI.WorldMap.TryLayout(live.SceneName, out var at)) return;
            foreach (var ts in TerrainSystem.All)
            {
                var r = ts.Region;
                if (r == null || r == live || r.SceneName == null || !UI.WorldMap.TryLayout(r.SceneName, out var other)) continue;
                var joined = r.CellOffset - live.CellOffset;
                var mapped = other - at;
                Plugin.Log.LogInfo($"[AutoTest] 地图分层 邻居 {live.SceneName}→{r.SceneName}：世界里 {joined}，地图上 {mapped}，差 {mapped - joined}");
            }
        }
    }
}
