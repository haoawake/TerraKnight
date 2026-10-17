using System.Collections;
using System.Collections.Generic;
using System.Text;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fx_godhome", or "fx_godhome_Key1.Key2"): each of Godhome's fights the mod can take the player to
    /// (BossArena's Godhome arenas) entered; its room counted a boss room (the game's own ground, BossRooms) or not; then
    /// for half a minute, twice a second, each boss there: inside the room's solid ground (a wall), or above the room's
    /// top (up in the air out of the fight). Screenshots of both.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator GodhomeRoomsTest(string only)
        {
            var keys = only != null ? new List<string>(only.Split('.')) : new List<string> { "NailBros", "Paintmaster", "Nailsage", "Oblobble", "LobsterLancer", "HollowKnightPrime" };
            bool done = false;
            mod.StartCoroutine(NetKeepAlive(() => done));
            mod.DevNoFallDamage = true;
            PlayerData.instance.hasDreamNail = true;
            const int terrain = 1 << ChunkColliders.TerrainLayer;
            foreach (var key in keys)
            {
                yield return SealTravel("Town", "left1");
                if (!BossArena.Fight(key)) { FxCheck($"{key} 神居", false, "进不去"); continue; }
                for (float until = Time.realtimeSinceStartup + 30f; !BossArena.Arrived(key) && Time.realtimeSinceStartup < until;) yield return null;
                yield return new WaitForSeconds(3f);
                string scene = GameManager.instance.sceneName;
                var ts = TerrainSystem.Current;
                FxCheck($"{key} {scene} 算首领房（原版地面）", BossRooms.IsRoom(scene) && ts != null && ts.SceneOwnGround, $"IsRoom {BossRooms.IsRoom(scene)}，地面是原版的 {ts?.SceneOwnGround}，{ts?.OwnZoneHow}");
                var region = ts?.Region;
                float top = region != null ? region.Root.position.y + region.Grid.Height * CellGrid.CellSize : float.MaxValue;
                int samples = 0, inWall = 0, inSky = 0;
                var sb = new StringBuilder();
                bool shotWall = false, shotSky = false;
                for (float t = Time.realtimeSinceStartup + 30f; Time.realtimeSinceStartup < t && GameManager.instance.sceneName == scene;)
                {
                    foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                    {
                        if (hm == null || hm.isDead || hm.hp <= 0 || !hm.gameObject.activeInHierarchy || !EnemyStats.Of(hm).Boss) continue;
                        var col = hm.GetComponent<Collider2D>();
                        if (col == null || !col.enabled) continue;
                        samples++;
                        var b = col.bounds;
                        // Its middle third inside a solid wall of the room (not its own colliders, not a trigger).
                        bool wall = false;
                        foreach (var c in Physics2D.OverlapBoxAll(b.center, new Vector2(b.size.x / 3f, b.size.y / 3f), 0f, terrain))
                            if (c != null && !c.isTrigger && c.GetComponentInParent<HealthManager>() == null && !c.usedByEffector) { wall = true; break; }
                        bool sky = b.min.y > top + 2f;
                        if (wall) { inWall++; if (sb.Length < 600) sb.Append($" 墙里 {hm.name}@{(Vector2)b.center:F1}"); }
                        if (sky) { inSky++; if (sb.Length < 600) sb.Append($" 天上 {hm.name}@{(Vector2)b.center:F1}"); }
                        if (wall && !shotWall) { shotWall = true; Player.TerraCamera.DevLook = b.center; yield return null; Shot(scene, "fx_godhome_wall_" + key); yield return null; Player.TerraCamera.DevLook = null; }
                        if (sky && !shotSky) { shotSky = true; Shot(scene, "fx_godhome_sky_" + key); yield return null; }
                    }
                    yield return new WaitForSeconds(0.5f);
                }
                FxCheck($"{key} {scene} 首领没卡进墙里也没飞到天上", samples > 0 && inWall <= 2 && inSky == 0, $"看了 {samples} 次，墙里 {inWall}，天上 {inSky}，房顶 {top:F1}：{sb}");
                Shot(scene, "fx_godhome_" + key);
                yield return null;
            }
            done = true;
            yield return SealTravel("Town", "left1");
        }
    }
}
