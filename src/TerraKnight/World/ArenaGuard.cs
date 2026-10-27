using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// A boss is fought from inside its arena: Terraria's blows on a boss, or on an enemy of a room's
    /// battle (its HealthManager given a battle scene), count only with the Knight on the same side
    /// of the room's battle gates as it. Standing outside the gates while minions, sentries or shots
    /// went in did the whole fight before (and woke it without the fight's start shutting him in).
    /// Such a blow does nothing at all (no damage, no TAKE DAMAGE to wake it) and says so.
    /// Nor does a blow reach an enemy the game keeps out of its room until it is wanted: the Gruz
    /// Mother's brood waits under the floor of her arena, out of the map, for her battle to release
    /// it; the Zenith's swords, which go through walls, killed it there, and her battle, counting it
    /// down from its start, never ended and never opened its gates.
    /// </summary>
    internal static class ArenaGuard
    {
        public static bool Enabled = true;
        public static int DevBlocked;

        static readonly AccessTools.FieldRef<HealthManager, GameObject> battleOf = AccessTools.FieldRefAccess<HealthManager, GameObject>("battleScene");

        struct Gate
        {
            public string Scene;
            public float X, Bottom, Top;
        }

        static readonly List<Gate> gates = new List<Gate>();
        static int gatesForScenes = -1;
        static float gatesAt = -10f;

        /// <summary>The loaded scenes' battle gates (their FSM "BG Control"), where they stand and how tall they are.</summary>
        static void Refresh()
        {
            int loaded = UnityEngine.SceneManagement.SceneManager.sceneCount;
            if (loaded == gatesForScenes && Time.time - gatesAt < 2f) return;
            gatesForScenes = loaded;
            gatesAt = Time.time;
            gates.Clear();
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
            {
                if (f.FsmName != "BG Control") continue;
                // An open gate has its collider off (no bounds then): its picture, or a door's height about its foot.
                Bounds b = default;
                var col = f.GetComponent<Collider2D>();
                var ren = f.GetComponent<Renderer>();
                if (col != null && col.enabled) b = col.bounds;
                else if (ren != null) b = ren.bounds;
                float x = f.transform.position.x;
                float bottom = b.size.y > 0.5f ? b.min.y : f.transform.position.y - 3f;
                float top = b.size.y > 0.5f ? b.max.y : f.transform.position.y + 3f;
                gates.Add(new Gate { Scene = Room(f.gameObject.scene.name), X = x, Bottom = bottom, Top = top });
            }
        }

        static string Room(string scene) => scene != null && scene.EndsWith("_boss") ? scene.Substring(0, scene.Length - 5) : scene;

        /// <summary>An enemy kept out of every loaded room's map (more than a little past its edges): nothing reaches it there.</summary>
        public static bool OffMap(HealthManager hm)
        {
            if (!Enabled || hm == null) return false;
            var c = hm.GetComponent<Collider2D>();
            Vector2 at = c != null && c.enabled ? (Vector2)c.bounds.center : (Vector2)hm.transform.position;
            const float margin = 3f;
            foreach (var p in new[] { at, at + new Vector2(0f, margin), at - new Vector2(0f, margin), at + new Vector2(margin, 0f), at - new Vector2(margin, 0f) })
            {
                var cell = CellGrid.WorldToCell(p);
                if (TerraWorld.RegionAt(cell.x, cell.y) != null) return false;
            }
            return TerrainSystem.Current != null;
        }

        /// <summary>Whether a blow on this enemy is from outside its fight: a boss or a battle's enemy, a battle gate of its room between it and the Knight.</summary>
        public static bool Outside(HealthManager hm)
        {
            if (!Enabled || hm == null) return false;
            var hero = HeroController.SilentInstance;
            if (hero == null) return false;
            var info = Items.EnemyStats.Of(hm);
            if (!(info != null && info.Boss) && battleOf(hm) == null) return false;
            // A boss not shown yet (or hidden by its own script): the False Knight up in the roof before his Start Fall, a
            // Terraria shot there broke his armour, and he fell stunned with his picture never put on (the user's 10-25).
            if (info != null && info.Boss && !DevNoHidden && Hidden(hm)) return true;
            // A boss is struck from inside its arena only (the user's 10-25 "进入boss房后才能攻击到敌人"): many have no gates
            // (Godhome's halls, open rooms) and wake by the Knight coming in, which a blow from afar never did.
            if (info != null && info.Boss && !BossArenaHolds(hm, hero.GetComponent<BoxCollider2D>().bounds.center)) return true;
            Refresh();
            if (gates.Count == 0) return false;
            string room = Room(hm.gameObject.scene.name);
            Vector2 a = hero.GetComponent<BoxCollider2D>().bounds.center, b = hm.transform.position;
            // The Knight between gates of the room on both sides (at his height): in the arena. What of the fight goes to
            // or past a gate is still fought from there (Grimm's firebats from the tent's two ends, beyond its gates).
            bool gateLeft = false, gateRight = false;
            foreach (var g in gates)
            {
                if (g.Scene != room || a.y < g.Bottom - 1.5f || a.y > g.Top + 1.5f) continue;
                if (g.X < a.x) gateLeft = true;
                else gateRight = true;
            }
            if (gateLeft && gateRight) return false;
            float lo = Mathf.Min(a.x, b.x), hi = Mathf.Max(a.x, b.x);
            foreach (var g in gates)
            {
                if (g.Scene != room) continue;
                if (g.X <= lo + 0.3f || g.X >= hi - 0.3f) continue;
                // Across the gate's doorway: the way from the Knight to it passes its height.
                float y = Mathf.Lerp(a.y, b.y, Mathf.InverseLerp(a.x, b.x, g.X));
                if (y < g.Bottom - 1.5f || y > g.Top + 1.5f) continue;
                return true;
            }
            return false;
        }

        static readonly Dictionary<int, (float At, Rect Arena)> bossArenas = new Dictionary<int, (float, Rect)>();

        /// <summary>
        /// Whether the Knight stands in a boss's arena: the view of the smallest camera lock its body is in
        /// (BossArenaZone.Around: a view round it without one), 3 past it on every side; taken again each second.
        /// </summary>
        static bool BossArenaHolds(HealthManager hm, Vector2 knight)
        {
            int id = hm.GetInstanceID();
            if (!bossArenas.TryGetValue(id, out var known) || Time.time - known.At > 1f || Time.time < known.At)
            {
                if (bossArenas.Count > 64) bossArenas.Clear();
                var col = hm.GetComponent<Collider2D>();
                Vector2 at = col != null && col.enabled ? (Vector2)col.bounds.center : (Vector2)hm.transform.position;
                string scene = hm.gameObject.scene.name;
                var region = TerrainSystem.Find(scene) ?? (scene.EndsWith("_boss") ? TerrainSystem.Find(scene.Substring(0, scene.Length - 5)) : null);
                UnityEngine.SceneManagement.Scene? main = null;
                if (scene.EndsWith("_boss"))
                {
                    var loaded = UnityEngine.SceneManagement.SceneManager.GetSceneByName(scene.Substring(0, scene.Length - 5));
                    if (loaded.IsValid() && loaded.isLoaded) main = loaded;
                }
                var room = region != null && region.Root != null
                    ? new Rect(region.Root.position, new Vector2(region.Grid.Width, region.Grid.Height) * CellGrid.CellSize)
                    : new Rect(at.x - 60f, at.y - 40f, 120f, 80f);
                var arena = BossArenaZone.Around(hm.gameObject.scene, room, at, main);
                // A camera lock's bottom can hang over the floor (Godhome's Gruz Mother, 6.7 over a floor at 0): the zone
                // goes down to the floor found under it, never through it (the False Knight's empty room under his).
                float floor = FloorUnder(arena);
                if (floor < arena.yMin) arena = new Rect(arena.xMin, floor, arena.width, arena.yMax - floor);
                // Wider to the sides than up and down: a zone from the camera locks can stop short of the arena's walls (the
                // Radiance's, 1.4 short of a player clinging there), while a room under the floor stays out (the False
                // Knight's empty one, 5 below).
                known = (Time.time, new Rect(arena.xMin - 6f, arena.yMin - 3f, arena.width + 12f, arena.height + 6f));
                bossArenas[id] = known;
            }
            // A lock over a whole long room (the Vengefly King's Greenpath hall, 190 wide) is no arena to shoot from across:
            // within about a view of the boss as well (an arena's two ends are within it).
            var c = hm.GetComponent<Collider2D>();
            Vector2 boss = c != null && c.enabled ? (Vector2)c.bounds.center : (Vector2)hm.transform.position;
            if (Mathf.Abs(knight.x - boss.x) > Reach.x || Mathf.Abs(knight.y - boss.y) > Reach.y) return false;
            if (known.Arena.Contains(knight)) return true;
            // Short of its zone (a camera lock can stop short of the arena's walls, the Radiance's by 7, or hang over its
            // floor), he is in where nothing solid stands between him and the boss; a room under the floor never is.
            return Seen(knight, boss);
        }

        /// <summary>Nothing solid between two points: the room's grid and its own terrain colliders (platforms aside).</summary>
        static bool Seen(Vector2 a, Vector2 b)
        {
            var d = b - a;
            int steps = Mathf.CeilToInt(d.magnitude / 0.4f);
            for (int i = 1; i < steps; i++)
            {
                var p = a + d * (i / (float)steps);
                if (TerraWorld.IsSolidAt(p)) return false;
                var hit = Physics2D.OverlapPoint(p, 1 << 8);
                if (hit != null && !hit.isTrigger && hit.GetComponent<PlatformEffector2D>() == null) return false;
            }
            return true;
        }

        /// <summary>
        /// The floor under a zone's bottom edge: from just inside it, straight down to the first solid (the room's grid or
        /// its own colliders) within 12 units at five places across, their middle one; the edge itself where it already
        /// stands on the floor or the place finds none.
        /// </summary>
        static float FloorUnder(Rect zone)
        {
            var found = new List<float>();
            for (int i = 0; i < 5; i++)
            {
                float x = Mathf.Lerp(zone.xMin + zone.width * 0.15f, zone.xMax - zone.width * 0.15f, i / 4f);
                for (float y = zone.yMin + 1f; y > zone.yMin - 12f; y -= 0.25f)
                {
                    var p = new Vector2(x, y);
                    if (TerraWorld.IsSolidAt(p) || Physics2D.OverlapPoint(p, 1 << 8) != null) { found.Add(y + 0.25f); break; }
                }
            }
            if (found.Count == 0) return zone.yMin;
            found.Sort();
            return Mathf.Min(zone.yMin, found[found.Count / 2]);
        }

        /// <summary>How far from a boss the Knight's blows on it count at most (a view's width and height and a little).</summary>
        static readonly Vector2 Reach = new Vector2(32f, 18f);

        /// <summary>Dev: the arena a boss is struck from (the last one taken), and whether the Knight is in it now.</summary>
        internal static string DevArena(HealthManager hm)
        {
            var hero = HeroController.SilentInstance;
            if (hm == null || hero == null) return "-";
            bool inside = BossArenaHolds(hm, hero.GetComponent<BoxCollider2D>().bounds.center);
            var r = bossArenas[hm.GetInstanceID()].Arena;
            return $"场地 {r.xMin:F1}~{r.xMax:F1}×{r.yMin:F1}~{r.yMax:F1}，主角 {(Vector2)hero.GetComponent<BoxCollider2D>().bounds.center:F1} 在内 {inside}";
        }

        static float nextNotice;

        /// <summary>Dev: why a boss can't be struck from where the Knight is now (隐藏 / 距离 / 场外 / 门外), or 可打.</summary>
        internal static string DevWhy(HealthManager hm)
        {
            var hero = HeroController.SilentInstance;
            if (hm == null || hero == null) return "-";
            Vector2 knight = hero.GetComponent<BoxCollider2D>().bounds.center;
            if (Hidden(hm)) return "隐藏";
            var c = hm.GetComponent<Collider2D>();
            Vector2 boss = c != null && c.enabled ? (Vector2)c.bounds.center : (Vector2)hm.transform.position;
            if (Mathf.Abs(knight.x - boss.x) > Reach.x || Mathf.Abs(knight.y - boss.y) > Reach.y) return $"距离 {boss:F1}";
            if (!BossArenaHolds(hm, knight)) return $"场外 {boss:F1} 框 {bossArenas[hm.GetInstanceID()].Arena}";
            return Outside(hm) ? $"门外 {boss:F1}" : $"可打 {boss:F1}";
        }

        /// <summary>Dev (suite fkhidden): hidden bosses struck as before 10-25.</summary>
        internal static bool DevNoHidden;

        /// <summary>A boss whose own picture is off (its MeshRenderer, as its script turns it on to come on stage).</summary>
        public static bool Hidden(HealthManager hm)
        {
            var mr = hm != null ? hm.GetComponent<MeshRenderer>() : null;
            return mr != null && !mr.enabled;
        }

        /// <summary>A blow from outside the fight: nothing done, and the refusal over it now and then.</summary>
        public static bool Refuse(HealthManager hm, bool quiet = false)
        {
            if (OffMap(hm)) { DevBlocked++; return true; }
            if (!Outside(hm)) return false;
            DevBlocked++;
            // In the middle of the screen, not over the boss (out of the Knight's arena, maybe out of sight); a minion's
            // blow says nothing (the player didn't strike).
            if (!quiet && !Hidden(hm) && Time.unscaledTime >= nextNotice)
            {
                nextNotice = Time.unscaledTime + 1f;
                UI.CenterNotice.Show(Lang.T("先进入首领的战斗区域"));
            }
            return true;
        }
    }
}
