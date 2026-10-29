using System.Collections;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "pylonplace", Pylons.TryPlace, the user's 10-27 "晶塔可以在神居和梦境被放置，不然挖掉了放不回去"): a pylon set down
    /// in Godhome's Atrium, the White Palace and a dream (the believers' shrine), trying a few spots either side of the player
    /// till one fits; each placed (with a way in to travel there). Lines "[AutoTest] 晶塔放置 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator PylonPlaceTest(bool travel = false)
        {
            int ok = 0, all = 0;
            var rooms = new[] { ("GG_Atrium", "door_dreamEnter"), ("White_Palace_01", "left1"), ("Dream_Room_Believer_Shrine", "left1") };
            var item = ItemDb.Get(ItemType.PylonUniversal);
            foreach (var (scene, gate) in rooms)
            {
                yield return GoTo(scene, gate);
                yield return new WaitForSeconds(2.5f);
                var hero = HeroController.instance;
                if (GameManager.instance.sceneName != scene)
                {
                    all++;
                    Plugin.Log.LogInfo($"[AutoTest] 晶塔放置 {scene}：没进去（在 {GameManager.instance.sceneName}） 不通过");
                    continue;
                }
                int placed0 = Pylons.DevPlaced;
                var col = hero.GetComponent<BoxCollider2D>();
                bool placed = false;
                float used = 0f;
                foreach (float dx in new[] { 2.2f, -2.2f, 3.2f, -3.2f, 1.6f, -1.6f })
                {
                    var spot = new Vector2(col.bounds.center.x + dx, col.bounds.min.y + 0.3f);
                    if (Pylons.TryPlace(item, hero, spot)) { placed = true; used = dx; break; }
                    yield return null;
                }
                yield return new WaitForSeconds(0.8f);
                Shot(scene, "pylonplace");
                all++;
                if (placed && Pylons.DevPlaced > placed0) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 晶塔放置 {scene}：{(placed ? $"放下了（偏移 {used}）" : "放不下")} {(placed ? "通过" : "不通过")}");
                if (travel && placed)
                {
                    var destination = World.Pylons.Unlocked.Last(p => p.Placed && p.Scene == scene);
                    yield return GoTo("Town", "left1");
                    World.Pylons.Teleport(destination);
                    yield return new WaitForSeconds(.5f);
                    float until = Time.time + 30f;
                    while ((GameManager.instance.sceneName != scene || GameManager.instance.IsInSceneTransition || !GameHooks.PlayerHasControl()) && Time.time < until) yield return null;
                    yield return new WaitForSeconds(1f);
                    bool controlled = GameManager.instance.sceneName == scene && GameHooks.PlayerHasControl() && !PlayerData.instance.disablePause;
                    all++; if (controlled) ok++;
                    Plugin.Log.LogInfo($"[AutoTest] 晶塔扩展 {scene} 返回可操作：{(controlled ? "通过" : "不通过")}");
                    var region = World.TerrainSystem.Find(scene);
                    bool near = region != null && Vector2.Distance(HeroController.instance.transform.position, region.Root.TransformPoint(destination.Local)) < 8f;
                    all++; if (near) ok++;
                    Plugin.Log.LogInfo($"[AutoTest] 晶塔扩展 {scene} 返回实际晶塔旁：{(near ? "通过" : "不通过")}");
                    Shot(scene, "pylonextras_return"); yield return new WaitForSeconds(.15f);
                }
            }
            Plugin.Log.LogInfo($"[AutoTest] 晶塔放置 合计 {ok}/{all}");
        }
    }
}
