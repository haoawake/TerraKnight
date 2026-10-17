using System.Collections;
using System.Collections.Generic;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fx_meteormap"): a meteorite coming down while the player is elsewhere, as it does at midnight. A few
    /// rooms are walked through first (their cells all seen), the player goes back to Dirtmouth, one comes down in one of
    /// them; the map is opened there at once on the landing (photographed at two zooms, the cells it draws there logged),
    /// then the player goes to the crater: the scene photographed beside it, and the map again from inside the room.
    /// The room's edits are put back after.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator MeteorMapTest()
        {
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            var candidates = new[] { "Crossroads_01", "Crossroads_02", "Crossroads_03", "Crossroads_07", "Tutorial_01" };
            foreach (var room in candidates)
            {
                if (!entryGates.ContainsKey(room)) continue;
                yield return GoTo(room, entryGates[room]);
                yield return new WaitForSeconds(2f);
                PlayerData.instance.isInvincible = true;
                Plugin.Log.LogInfo($"[AutoTest] 陨石地图 走过 {room}：看全 {UI.WorldMap.DevSeeAll(room)}");
            }
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            string target = null;
            Dictionary<int, byte> before = null;
            foreach (var room in candidates)
            {
                before = new Dictionary<int, byte>(WorldEdits.For(room));
                if (Meteorites.Drop("Town", room)) { target = room; break; }
            }
            Meteorites.Pending = false;
            if (target == null) { FxCheck("陨石落地", false, "几个房间都没找到落点"); yield break; }
            var at = Meteorites.LastAt;
            FxCheck("陨石落地（人在德特茅斯）", true, Meteorites.DevLast);
            // The map at once, from Dirtmouth, on the landing.
            string drawn = UI.WorldMap.DevCellDrawn(target, at.x, at.y, out var c);
            int cells = UI.WorldMap.DevMeteorCellsIn(target);
            var hit = new List<string>();
            // The pit under the impact is dug out (drawn as dug rock); the Meteorite is round it.
            for (int dy = -12; dy <= 3; dy++)
                for (int dx = -12; dx <= 12; dx++)
                {
                    UI.WorldMap.DevCellDrawn(target, at.x + dx, at.y + dy, out var cc);
                    if (cc.a > 200 && cc.r > 200 && cc.g > 80 && cc.g < 150 && cc.b < 90) hit.Add($"{dx},{dy}");
                }
            Plugin.Log.LogInfo($"[AutoTest] 陨石地图 撞击点 {at}：{drawn}；地图上陨石格 {cells}；撞击点附近画成陨石色的格 {hit.Count} 个");
            FxCheck("地图上马上画出陨石（人在别处）", cells > 0 && hit.Count > 0, $"{target} 陨石格 {cells}，撞击点附近亮橙 {hit.Count} 格");
            UI.WorldMap.DevSetOpen(true);
            UI.WorldMap.DevCentreCell(target, at);
            UI.WorldMap.DevZoom(4f);
            yield return new WaitForSecondsRealtime(0.6f);
            Shot("Town", "meteormap_town_zoom4");
            yield return null;
            UI.WorldMap.DevZoom(10f);
            UI.WorldMap.DevCentreCell(target, at);
            yield return new WaitForSecondsRealtime(0.6f);
            Shot("Town", "meteormap_town_zoom10");
            yield return null;
            UI.WorldMap.DevSetOpen(false);
            UI.WorldMap.DevZoom(4f);
            yield return new WaitForSecondsRealtime(0.5f);

            // The crater itself: the player beside it.
            yield return GoTo(target, entryGates[target]);
            yield return new WaitForSeconds(2f);
            PlayerData.instance.isInvincible = true;
            var region = TerraWorld.Live;
            if (region == null || region.SceneName != target) { FxCheck("进到陨石坑的房间", false, region?.SceneName); RestoreEdits(target, before); yield break; }
            var off = region.CellOffset;
            Vector2Int top = new Vector2Int(int.MinValue, 0);
            int live = 0;
            for (int x = at.x - 20; x <= at.x + 20; x++)
                for (int y = at.y - 20; y <= at.y + 6; y++)
                {
                    if (TerraWorld.Get(off.x + x, off.y + y) != CellKind.Meteorite) continue;
                    live++;
                    if (!TerraWorld.IsSolid(off.x + x, off.y + y + 1) && !TerraWorld.IsSolid(off.x + x, off.y + y + 2) && !TerraWorld.IsSolid(off.x + x, off.y + y + 3)
                        && (top.x == int.MinValue || Mathf.Abs(x - at.x) < Mathf.Abs(top.x - at.x))) top = new Vector2Int(x, y);
                }
            FxCheck("房间里就是陨石坑", live > 0, $"{target} 场景里陨石 {live} 格");
            var hero = HeroController.instance;
            if (top.x != int.MinValue)
            {
                Vector2 topWorld = CellGrid.CellCenter(off.x + top.x, off.y + top.y);
                hero.transform.position = new Vector3(topWorld.x + 2.5f, topWorld.y + CellGrid.CellSize * 0.5f + 1.2f, hero.transform.position.z);
            }
            mod.DevNoFallDamage = true;
            yield return new WaitForSeconds(1.5f);
            PlayerData.instance.isInvincible = true;
            // What of the crater the room's foreground still covers from where the camera is now.
            var camPos = GameCameras.instance.mainCamera.transform.position;
            int covered = 0, seen = 0;
            for (int x = at.x - 20; x <= at.x + 20; x++)
                for (int y = at.y - 20; y <= at.y + 6; y++)
                {
                    if (TerraWorld.Get(off.x + x, off.y + y) != CellKind.Meteorite) continue;
                    Vector2 cc = CellGrid.CellCenter(off.x + x, off.y + y);
                    if (Mathf.Abs(cc.x - camPos.x) > 14f || Mathf.Abs(cc.y - camPos.y) > 8f) continue;
                    seen++;
                    if (TerraWorld.CoveredByScenery(new Vector3(cc.x, cc.y, 0f), camPos)) covered++;
                }
            FxCheck("陨石坑没被前景挡住", seen > 0 && covered == 0, $"画面里陨石 {seen} 格，被前景挡住 {covered} 格；撤掉的前景 {Meteorites.DevCoverCleared} 块");
            Shot(target, "meteormap_scene");
            yield return null;
            UI.WorldMap.DevSetOpen(true);
            UI.WorldMap.DevCentreCell(target, at);
            UI.WorldMap.DevZoom(10f);
            yield return new WaitForSecondsRealtime(0.6f);
            Shot(target, "meteormap_room_zoom10");
            yield return null;
            UI.WorldMap.DevSetOpen(false);
            UI.WorldMap.DevZoom(4f);
            yield return new WaitForSecondsRealtime(0.3f);
            Plugin.Log.LogInfo($"[AutoTest] 陨石地图 进房间后 撞击点 {UI.WorldMap.DevCellDrawn(target, at.x, at.y, out _)}，地图上陨石格 {UI.WorldMap.DevMeteorCellsIn(target)}");
            RestoreEdits(target, before);
            yield return GoTo("Town", "left1");
        }
    }
}
