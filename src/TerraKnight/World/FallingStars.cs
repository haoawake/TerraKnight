using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// Stars falling from the night sky (WorldGen.UpdateWorld, W:72401-72436; Star.NightSetup;
    /// AI_148_StarSpawner). Each tick of the night a 10 in 8000 chance, a small world's, times the
    /// night's starfall boost (one night in ten 3 to 5, else one in three 1 to 1.5): a star from the top
    /// of the room, at a random column, or, one in 15, within 640 px of the player and coming his way
    /// (not after three minutes of his standing idle). None where the top is rock. It falls at 12 px a
    /// tick, 100 to 300 down to up to 100 sideways, shining and twinkling, and where it lands lies a
    /// Fallen Star; those, and the stars still falling, go at daybreak. Only the rooms under
    /// Hallownest's sky have them (Dirtmouth, King's Pass, the Howling Cliffs, Kingdom's Edge), and
    /// there only under a stretch of open top at least 24 tiles wide: the caves among them and the gaps
    /// of their doorways up have none.
    /// </summary>
    internal static class FallingStars
    {
        const float Px = Effects.TerraParticles.Px;
        const int AfkTicks = 10800; // Player.AFKTimeNeededForNoLuckyStars
        /// <summary>The narrowest open stretch of a room's top that counts as sky, in cells (a doorway up is 8).</summary>
        const int SkyWidth = 24;

        /// <summary>Star.starfallBoost: tonight's.</summary>
        public static float Boost = 1f;

        /// <summary>Dev: the chance taken this many times over (0: none; null: the game's own).</summary>
        public static float? DevRate;
        /// <summary>Dev: every star a lucky one (true) or none (false).</summary>
        public static bool? DevLucky;
        /// <summary>Dev: stars started, of them lucky.</summary>
        public static int DevStarted, DevLuckyStarted;
        /// <summary>Dev: where the last star started.</summary>
        public static Vector2 DevLastFrom;

        static bool? wasDay;
        static Vector2 heroWas;
        static int idle;

        /// <summary>A room under the open sky.</summary>
        public static bool SkyRoom(string scene)
        {
            if (string.IsNullOrEmpty(scene)) return false;
            return scene == "Town" || scene == "Tutorial_01" || scene.StartsWith("Cliffs_") || scene.StartsWith("Deepnest_East_");
        }

        /// <summary>A point in the current room's span, above its floor (the sky's stars fall within it).</summary>
        public static bool InRoom(Vector2 world)
        {
            var region = TerrainSystem.Current?.Region;
            if (region == null) return false;
            var r = region.CellRect;
            const float c = CellGrid.CellSize;
            return world.x >= r.xMin * c && world.x <= r.xMax * c && world.y >= r.yMin * c;
        }

        /// <summary>Star.NightSetup: the boost for the night that starts.</summary>
        static void NightSetup()
        {
            Boost = 1f;
            if (Random.Range(0, 10) == 0) Boost = Random.Range(300, 501) * 0.01f;
            else if (Random.Range(0, 3) == 0) Boost = Random.Range(100, 151) * 0.01f;
        }

        /// <summary>Once a Terraria tick, from the projectiles' clock.</summary>
        public static void Tick()
        {
            bool day = TerraTime.IsDay;
            if (wasDay != day && !day) NightSetup();
            wasDay = day;
            var hero = HeroController.SilentInstance;
            if (hero == null) return;
            Vector2 at = hero.transform.position;
            idle = (at - heroWas).sqrMagnitude > 1e-6f ? 0 : idle + 1;
            heroWas = at;
            if (day) return;
            var region = TerrainSystem.Current?.Region;
            if (region == null || !SkyRoom(region.SceneName) || Items.TerraProjectiles.Instance == null) return;
            float rate = 10f * Boost * (DevRate ?? 1f);
            if (Random.value * 8000f >= rate) return;
            Start(region, hero);
        }

        /// <summary>One star from the top of the room (WorldGen's part, then AI_148's).</summary>
        static void Start(TerrainRegion region, HeroController hero)
        {
            const float c = CellGrid.CellSize;
            var r = region.CellRect;
            float left = r.xMin * c + 2f * c, right = r.xMax * c - 2f * c;
            if (right <= left) return;
            var from = new Vector2(Random.Range(left, right), r.yMax * c - 16f * Px);
            bool lucky = DevLucky ?? (Random.Range(0, 15) == 0 && idle < AfkTicks);
            Vector2 heroAt = hero.GetComponent<BoxCollider2D>().bounds.center;
            if (lucky)
            {
                int reach = Random.Range(1, 640);
                from.x = Mathf.Clamp(heroAt.x + Random.Range(-reach, reach + 1) * Px, left, right);
            }
            // Collision.SolidCollision on its 16 px box: none out of rock, nor down a doorway.
            if (Solid(from, 8f * Px) || !UnderSky(region, CellGrid.WorldToCell(from).x)) return;
            float vx = Random.Range(-100, 101), vy = Random.Range(100, 300);
            var v = new Vector2(vx, vy).normalized * 12f;
            if (lucky) v.x = Mathf.Abs(v.x * 0.35f) * (from.x < heroAt.x ? 1f : -1f);
            Items.TerraProjectiles.Instance.SkyStar(from, v);
            DevStarted++;
            if (lucky) DevLuckyStarted++;
            DevLastFrom = from;
        }

        /// <summary>The room's top is open over at least <see cref="SkyWidth"/> cells around this column.</summary>
        static bool UnderSky(TerrainRegion region, int x)
        {
            var r = region.CellRect;
            int top = r.yMax - 1;
            if (TerraWorld.IsSolid(x, top)) return false;
            int a = x, b = x;
            while (a - 1 >= r.xMin && !TerraWorld.IsSolid(a - 1, top) && b - a + 1 < SkyWidth) a--;
            while (b + 1 < r.xMax && !TerraWorld.IsSolid(b + 1, top) && b - a + 1 < SkyWidth) b++;
            return b - a + 1 >= SkyWidth;
        }

        static bool Solid(Vector2 centre, float half)
        {
            var a = CellGrid.WorldToCell(centre - new Vector2(half, half));
            var b = CellGrid.WorldToCell(centre + new Vector2(half, half) - new Vector2(0.001f, 0.001f));
            for (int y = a.y; y <= b.y; y++)
                for (int x = a.x; x <= b.x; x++)
                    if (TerraWorld.IsSolid(x, y)) return true;
            return false;
        }

        /// <summary>Dev: how much of the room's top is open sky (columns under a wide enough opening) and how far down a star falls there on average, in cells.</summary>
        public static (int Open, int Columns, float Depth) DevSky()
        {
            var region = TerrainSystem.Current?.Region;
            if (region == null) return (0, 0, 0f);
            var r = region.CellRect;
            int open = 0;
            long depth = 0;
            for (int x = r.xMin; x < r.xMax; x++)
            {
                if (!UnderSky(region, x)) continue;
                open++;
                int y = r.yMax - 1;
                while (y > r.yMin && !TerraWorld.IsSolid(x, y)) y--;
                depth += r.yMax - 1 - y;
            }
            return (open, r.width, open > 0 ? depth / (float)open : 0f);
        }

        /// <summary>
        /// Dev: each column's lid (the rock from the room's top down to the first open cell) and the open run
        /// below it, in cells; the column with the longest run under the thinnest lid.
        /// </summary>
        public static string DevLids(out Vector2 bestUnderLid)
        {
            bestUnderLid = default;
            var region = TerrainSystem.Current?.Region;
            if (region == null) return "";
            var r = region.CellRect;
            var hist = new System.Collections.Generic.SortedDictionary<int, int>();
            int best = -1, bestScore = int.MinValue;
            for (int x = r.xMin; x < r.xMax; x++)
            {
                int y = r.yMax - 1, lid = 0;
                while (y >= r.yMin && TerraWorld.IsSolid(x, y)) { y--; lid++; }
                int top = y, run = 0;
                while (y >= r.yMin && !TerraWorld.IsSolid(x, y)) { y--; run++; }
                if (run < 12) continue;
                int bucket = lid == 0 ? 0 : lid <= 4 ? 4 : lid <= 8 ? 8 : lid <= 16 ? 16 : lid <= 32 ? 32 : 999;
                hist[bucket] = (hist.TryGetValue(bucket, out int n) ? n : 0) + 1;
                int score = run - lid * 4;
                if (score > bestScore)
                {
                    bestScore = score;
                    best = x;
                    bestUnderLid = new Vector2((x + 0.5f) * CellGrid.CellSize, (top - 6) * CellGrid.CellSize);
                }
            }
            var sb = new System.Text.StringBuilder();
            foreach (var kv in hist) sb.Append($" 盖≤{kv.Key}:{kv.Value}列");
            return sb.Length == 0 ? " 没有下面空 12 格以上的列" : sb.ToString();
        }
    }
}
