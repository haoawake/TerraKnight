using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// An enemy driven into the room's solid ground and held there: a Squit's dash (fast enough to go half a cell a
    /// physics step) took it into Greenpath's two-cell bridge in Fungus1_17, where the physics no longer pushed it out
    /// and its own machine kept driving it at the player, for good. Four times a second each moving enemy's body is
    /// looked at; one whose middle has been in a solid cell, its body mostly in the ground, for a second is set at the
    /// nearest place in its room that holds it. Bosses, those that live in the ground or walls (the burrowers, the
    /// Goam, the Mawlurk, the Fool Eater, the wall crawlers) and those the game lets through the ground are left alone.
    /// </summary>
    internal static class EnemyUnstick
    {
        const float Every = 0.25f, HeldFor = 1f, Mostly = 0.5f, Reach = 6f, Step = 0.25f;

        /// <summary>The monster book's records of enemies whose place is in the ground or a wall, or on its surface.</summary>
        static readonly HashSet<string> livesInGround = new HashSet<string>
        {
            "BabyCentipede", "BigCentipede", "CentipedeHatcher", "MossCharger", "MegaMossCharger", "Worm", "MawlekTurret",
            "SnapperTrap", "LaserBug", "Climber", "Crawler", "AbyssCrawler", "CrystalCrawler", "FungCrawler", "MinesCrawler",
            "JellyCrawler", "GrubMimic", "MimicSpider",
        };

        static float next;
        static readonly Dictionary<int, float> inGroundSince = new Dictionary<int, float>();
        /// <summary>
        /// Bodies with no room found near them, left until then: mostly what the game parks out of the map until it is
        /// wanted (the Gruz Mother's young under her room, the Flukemarm's fly spawners far off it), which would be searched
        /// for a place and reported every second for the whole visit; each is reported once.
        /// </summary>
        static readonly Dictionary<int, float> noRoomUntil = new Dictionary<int, float>();
        static readonly HashSet<int> reportedNoRoom = new HashSet<int>();
        const float NoRoomWait = 15f;
        public static int DevFreed, DevNoRoom;

        public static void Tick()
        {
            if (Time.time < next) return;
            next = Time.time + Every;
            foreach (var info in Items.EnemyStats.All)
            {
                var hm = info.Hm;
                if (hm == null || hm.isDead || !hm.gameObject.activeInHierarchy || info.Boss || Net.Enemies.IsPuppet(hm)) continue;
                if (hm.GetComponent<EnemyAi.BodyRecovery>() is EnemyAi.BodyRecovery recovering && recovering.Running) continue;
                if (info.Record != null && livesInGround.Contains(info.Record)) continue;
                int id = hm.GetInstanceID();
                if (noRoomUntil.TryGetValue(id, out float wait) && Time.time < wait) continue;
                var body = Body(hm, out var rb);
                if (body == null || !InGround(body.bounds))
                {
                    inGroundSince.Remove(id);
                    continue;
                }
                if (!inGroundSince.TryGetValue(id, out float since))
                {
                    if (inGroundSince.Count > 512) inGroundSince.Clear();
                    inGroundSince[id] = Time.time;
                    continue;
                }
                if (Time.time - since < HeldFor) continue;
                inGroundSince.Remove(id);
                var b = body.bounds;
                string scene = hm.gameObject.scene.name;
                if (FindClear(b, scene, out var delta))
                {
                    var recovery = hm.GetComponent<EnemyAi.BodyRecovery>() ?? hm.gameObject.AddComponent<EnemyAi.BodyRecovery>();
                    recovery.Begin(hm, null, body, rb, null, (Vector2)b.center + delta);
                    DevFreed++;
                    Plugin.Log.LogInfo($"[卡墙] {hm.name}（{scene}）卡在地形里 {(Vector2)b.center:F1}，连续脱困至 {(Vector2)b.center + delta:F1}");
                }
                else
                {
                    DevNoRoom++;
                    if (noRoomUntil.Count > 512) noRoomUntil.Clear();
                    noRoomUntil[id] = Time.time + NoRoomWait;
                    if (reportedNoRoom.Count > 512) reportedNoRoom.Clear();
                    if (reportedNoRoom.Add(id))
                        Plugin.Log.LogWarning($"[卡墙] {hm.name}（{scene}）卡在地形里 {(Vector2)b.center:F1}，{Reach} 格内没有能放下它的空位（{NoRoomWait:F0} 秒后再看）");
                }
            }
        }

        /// <summary>A moving body that meets the ground: dynamic, simulated, solid, on a layer the terrain stops.</summary>
        internal static Collider2D Body(HealthManager hm, out Rigidbody2D rb)
        {
            rb = hm.GetComponent<Rigidbody2D>();
            if (rb == null || rb.bodyType != RigidbodyType2D.Dynamic || !rb.simulated) return null;
            Collider2D body = null;
            foreach (var c in hm.GetComponents<Collider2D>())
                if (c.enabled && !c.isTrigger) { body = c; break; }
            if (body == null) return null;
            if (Physics2D.GetIgnoreLayerCollision(body.gameObject.layer, ChunkColliders.TerrainLayer)) return null;
            return body;
        }

        /// <summary>Its middle in a solid cell and most of its body in the ground.</summary>
        internal static bool InGround(Bounds b) => TerraWorld.IsSolidAt(b.center) && SolidShare(b) >= Mostly;

        /// <summary>The share of the cells a box covers (pulled in a little at its edges) that are solid.</summary>
        internal static float SolidShare(Bounds b)
        {
            const float inset = 0.1f;
            var lo = CellGrid.WorldToCell(new Vector2(b.min.x + inset, b.min.y + inset));
            var hi = CellGrid.WorldToCell(new Vector2(b.max.x - inset, b.max.y - inset));
            int n = 0, solid = 0;
            for (int y = lo.y; y <= hi.y; y++)
                for (int x = lo.x; x <= hi.x; x++)
                {
                    n++;
                    if (TerraWorld.IsSolid(x, y)) solid++;
                }
            return n > 0 ? solid / (float)n : 0f;
        }

        /// <summary>The nearest move (rings of <see cref="Step"/> out to <see cref="Reach"/>) that leaves the box clear, in its own room.</summary>
        static bool FindClear(Bounds b, string scene, out Vector2 delta)
        {
            for (float r = Step; r <= Reach; r += Step)
            {
                int around = Mathf.Max(8, Mathf.RoundToInt(2f * Mathf.PI * r / Step));
                for (int k = 0; k < around; k++)
                {
                    float a = k * Mathf.PI * 2f / around;
                    var d = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    var moved = new Bounds(b.center + (Vector3)d, b.size);
                    var cell = CellGrid.WorldToCell(moved.center);
                    var region = TerraWorld.RegionAt(cell.x, cell.y);
                    if (region == null || region.SceneName != scene) continue;
                    if (SolidShare(moved) > 0f || TerraWorld.IsSolidAt(moved.center)) continue;
                    delta = d;
                    return true;
                }
            }
            delta = Vector2.zero;
            return false;
        }
    }
}
