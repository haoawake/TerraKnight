using System.Collections;
using System.Text;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "spaenemies"): the rooms with a hot spring and a bench (the Crossroads', the Pleasure
    /// House's, Deepnest's, the Colosseum's): every enemy of the room (active or not, with its scene and
    /// where it is), right after arriving and 15 seconds later, so one that came in from next door or
    /// was brought back by the respawning shows.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static string EnemiesIn(string scene, Bounds room)
        {
            var sb = new StringBuilder();
            int n = 0;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (hm == null) continue;
                Vector2 p = hm.transform.position;
                bool mine = hm.gameObject.scene.name == scene;
                bool inside = p.x >= room.min.x && p.x <= room.max.x && p.y >= room.min.y && p.y <= room.max.y;
                if (!mine && !inside) continue;
                n++;
                sb.Append($"\n    {hm.gameObject.scene.name}/{PathOf(hm.transform)} {p:F1} 激活 {hm.gameObject.activeInHierarchy} 死 {hm.isDead} 血 {hm.hp}{(inside ? "" : "（在房间外）")}");
            }
            return $"{n} 个{sb}";
        }

        IEnumerator SpaEnemiesTest()
        {
            var pd = PlayerData.instance;
            bool infected0 = pd.GetBool("crossroadsInfected");
            foreach (var visit in new[] { "Crossroads_30", "Crossroads_30@感染", "Abyss_17", "Deepnest_Spa" })
            {
                // The Crossroads' hot spring again once the Crossroads are infected (crossroadsInfected).
                string scene = visit.Split('@')[0];
                pd.SetBool("crossroadsInfected", visit.EndsWith("感染") || infected0);
                // Whichever of its doors lets the player in.
                foreach (var gate in new[] { EntryGateOf(scene), "left1", "right1", "top1", "bot1", "left2", "right2" })
                {
                    yield return GoTo(scene, gate);
                    if (GameManager.instance.sceneName == scene) break;
                }
                yield return new WaitForSeconds(2f);
                PlayerData.instance.isInvincible = true;
                if (GameManager.instance.sceneName != scene) { Plugin.Log.LogInfo($"[AutoTest] 温泉敌人 {scene}：没进去"); continue; }
                var region = World.TerrainSystem.Find(scene);
                Bounds room = new Bounds(HeroController.instance.transform.position, new Vector3(80f, 50f, 1f));
                if (region != null)
                {
                    var g = region.Grid;
                    Vector2 o = region.Root.position;
                    room = new Bounds(o + new Vector2(g.Width, g.Height) * World.CellGrid.CellSize / 2f, new Vector3(g.Width * World.CellGrid.CellSize, g.Height * World.CellGrid.CellSize, 1f));
                }
                Plugin.Log.LogInfo($"[AutoTest] 温泉敌人 {visit} 刚进来：{EnemiesIn(scene, room)}");
                yield return new WaitForSeconds(15f);
                PlayerData.instance.isInvincible = true;
                Plugin.Log.LogInfo($"[AutoTest] 温泉敌人 {visit} 15 秒后：{EnemiesIn(scene, room)}");
                Shot(scene, "spaenemies" + (visit.EndsWith("感染") ? "_infected" : ""));
                yield return null;
            }
            pd.SetBool("crossroadsInfected", infected0);
        }
    }
}
