using System.Collections;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "compassprobe", the user's 10-27 "指南针护符的很多指向依旧是错误的…毛里克的指引…指到了科尼法旁边"): the
    /// Wayward Compass worn, the whole map shown (the world atlas), pointed at one fight at a time (TK_COMPASS_FIGHTS, relic
    /// item names, default RelicMawlek): from Dirtmouth the way and its end, the full map centred there; then in the fight's
    /// room the bosses' (HealthManagers of 200+ health, asleep ones too), Cornifer's and the breakable things' map cells
    /// beside the way's end. Lines "[AutoTest] 指南针探测 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator CompassProbeTest()
        {
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            Charms.DevSetPage(true);
            Charms.DevAnywhere = true;
            pd.SetBool("gotCharm_2", true);
            if (!Charms.Worn(2)) Charms.Click(2);
            UI.WorldMap.RevealWorld();
            string list = System.Environment.GetEnvironmentVariable("TK_COMPASS_FIGHTS") ?? "RelicMawlek";
            foreach (var name in list.Split(','))
            {
                if (!System.Enum.TryParse(name.Trim(), out ItemType fight)) { Plugin.Log.LogInfo($"[AutoTest] 指南针探测 不认识 {name}"); continue; }
                UI.WorldMap.DevCompassOnly = fight;
                yield return GoTo("Town", "left1");
                yield return new WaitForSeconds(2f);
                UI.WorldMap.DevCompassForget();
                string way = UI.WorldMap.DevCompass();
                string room = UI.WorldMap.DevCompassRoom;
                var end = UI.WorldMap.DevCompassEnd;
                Plugin.Log.LogInfo($"[AutoTest] 指南针探测 {fight} 从德特茅斯：{way}；终点格 {end}；房间 {room} 在地图 {(room != null ? UI.WorldMap.DevRoomRect(room) : "")}");
                UI.FullMap.DevSetOpen(true);
                UI.WorldMap.DevPanTo(end);
                yield return new WaitForSeconds(0.6f);
                Shot("Town", $"compassprobe_{fight}_town");
                UI.FullMap.DevSetOpen(false);
                if (room == null) continue;
                yield return GoTo(room, EntryGateOf(room));
                yield return new WaitForSeconds(2.5f);
                var here = GameManager.instance.sceneName;
                foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (hm == null || hm.hp < 200 || !hm.gameObject.scene.name.StartsWith(room)) continue;
                    var c = UI.WorldMap.DevLiveCell(hm.transform.position);
                    Plugin.Log.LogInfo($"[AutoTest] 指南针探测 {fight} 首领 {hm.name}（{UI.EnemyHover.RecordOf(hm)}）在 {(Vector2)hm.transform.position:F1} 地图格 {c:F0}，离终点 {Vector2.Distance(c, end):F0} 格，激活 {hm.gameObject.activeInHierarchy}");
                }
                foreach (var go in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(t => t.gameObject))
                {
                    if (go.transform.parent != null && !go.name.Contains("Cornifer")) continue;
                    if (!go.name.Contains("Cornifer")) continue;
                    Plugin.Log.LogInfo($"[AutoTest] 指南针探测 {fight} 科尼法 {go.name}（{go.scene.name}）地图格 {UI.WorldMap.DevLiveCell(go.transform.position):F0}");
                }
                foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                {
                    string fn = f.FsmName.ToLowerInvariant();
                    if (!fn.Contains("break") && !fn.Contains("wall") && !f.gameObject.name.Contains("Breakable")) continue;
                    var col = f.GetComponent<Collider2D>() ?? f.GetComponentInChildren<Collider2D>();
                    Vector2 p = col != null ? (Vector2)col.bounds.center : (Vector2)f.transform.position;
                    Plugin.Log.LogInfo($"[AutoTest] 指南针探测 {fight} 可破 {f.gameObject.name}/{f.FsmName} 状态 {f.ActiveStateName} 在 {p:F1} 地图格 {UI.WorldMap.DevLiveCell(p):F0} 大小 {(col != null ? (Vector2)col.bounds.size : Vector2.zero):F1}");
                }
                foreach (var tp in Object.FindObjectsByType<TransitionPoint>(FindObjectsSortMode.None))
                    if (tp.gameObject.scene.name == here)
                        Plugin.Log.LogInfo($"[AutoTest] 指南针探测 {fight} 门 {tp.name} 地图格 {UI.WorldMap.DevLiveCell(tp.transform.position):F0}");
                UI.WorldMap.DevCompassForget();
                Plugin.Log.LogInfo($"[AutoTest] 指南针探测 {fight} 在房间里：{UI.WorldMap.DevCompass()}，主角格 {UI.WorldMap.DevHeadCell:F0}");
                UI.FullMap.DevSetOpen(true);
                UI.WorldMap.DevPanTo(end);
                yield return new WaitForSeconds(0.6f);
                Shot(here, $"compassprobe_{fight}_room");
                UI.FullMap.DevSetOpen(false);
            }
            UI.WorldMap.DevCompassOnly = null;
            Plugin.Log.LogInfo("[AutoTest] 指南针探测 完");
        }
    }
}
