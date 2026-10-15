using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// The grid as one continuous world: the scene being played plus the neighbouring scenes laid
    /// beside it. Cells are addressed in global coordinates (world position / CellSize); every
    /// region sits on the same lattice, so a global cell belongs to exactly one region or to none.
    /// Outside every region counts as solid, undiggable rock.
    /// </summary>
    internal static class TerraWorld
    {
        /// <summary>The grid of the scene being played.</summary>
        public static TerrainRegion Live => TerrainSystem.Current != null ? TerrainSystem.Current.Region : null;

        /// <summary>The game's own ground there (a boss room's): the grid's helpers (steps, unsticking) leave the player to it.</summary>
        public static bool OwnGroundAt(Vector2 world)
        {
            var c = CellGrid.WorldToCell(world);
            var r = RegionAt(c.x, c.y);
            return r != null && r.IsOwnCell(c.x - r.CellOffset.x, c.y - r.CellOffset.y);
        }

        public static TerrainRegion RegionAt(int gx, int gy)
        {
            var live = Live;
            if (live != null && live.Contains(gx, gy)) return live;
            foreach (var t in TerrainSystem.All)
                if (t.Region != null && t.Region != live && t.Region.Contains(gx, gy)) return t.Region;
            return null;
        }

        public static CellKind Get(int gx, int gy)
        {
            var r = RegionAt(gx, gy);
            return r == null ? CellKind.Original : r.Grid.Get(gx - r.CellOffset.x, gy - r.CellOffset.y);
        }

        /// <summary>A block in the way (platforms, torches and rope are not).</summary>
        public static bool IsSolid(int gx, int gy) => Cells.Blocks(Get(gx, gy));

        /// <summary>Anything at all in the cell.</summary>
        public static bool IsOccupied(int gx, int gy) => Get(gx, gy) != CellKind.Empty;

        public static bool IsPlatform(int gx, int gy) => Get(gx, gy) == CellKind.Platform;

        /// <summary>A platform's slope: 0 flat, 1 "\" (high on the left), 2 "/" (high on the right).</summary>
        public static int GetSlope(int gx, int gy)
        {
            var r = RegionAt(gx, gy);
            return r == null ? 0 : r.Grid.GetSlope(gx - r.CellOffset.x, gy - r.CellOffset.y);
        }

        public static void SetSlope(int gx, int gy, int slope)
        {
            var r = RegionAt(gx, gy);
            r?.Grid.SetSlope(gx - r.CellOffset.x, gy - r.CellOffset.y, slope);
        }

        /// <summary>A cell of some region changed (global coordinates).</summary>
        public static event System.Action<int, int, CellKind> Changed;

        internal static void RaiseChanged(int gx, int gy, CellKind kind) => Changed?.Invoke(gx, gy, kind);

        public static bool IsDug(int gx, int gy)
        {
            var r = RegionAt(gx, gy);
            return r != null && r.Grid.IsDug(gx - r.CellOffset.x, gy - r.CellOffset.y);
        }

        public static bool InWorld(int gx, int gy) => RegionAt(gx, gy) != null;

        /// <summary>
        /// The scene's own art is cut away at a cell: it was dug, or it is open ground next to a
        /// dig, where the terrain's overhanging edge goes with it.
        /// </summary>
        public static bool ArtCutAt(int gx, int gy)
        {
            var r = RegionAt(gx, gy);
            if (r == null) return false;
            int lx = gx - r.CellOffset.x, ly = gy - r.CellOffset.y;
            return r.Grid.IsDug(lx, ly) || r.Holes.HasMask(ly * r.Grid.Width + lx);
        }

        public static void Set(int gx, int gy, CellKind kind)
        {
            var r = RegionAt(gx, gy);
            r?.Grid.Set(gx - r.CellOffset.x, gy - r.CellOffset.y, kind);
        }

        public static bool IsSolidAt(Vector2 world)
        {
            var c = CellGrid.WorldToCell(world);
            return IsSolid(c.x, c.y);
        }

        /// <summary>
        /// Collision.CanHitLine on the grid, in Terraria px (y down, 16 px cells): no block in the
        /// cells a segment passes through, walked cell by cell. The two end cells don't count, so
        /// a body pressed into a wall is still in sight.
        /// </summary>
        public static bool LineClearPx(Vector2 a, Vector2 b)
        {
            int i = Mathf.FloorToInt(a.x / 16f), j = Mathf.FloorToInt(a.y / 16f);
            int i1 = Mathf.FloorToInt(b.x / 16f), j1 = Mathf.FloorToInt(b.y / 16f);
            if (i == i1 && j == j1) return true;
            var d = b - a;
            int si = d.x > 0f ? 1 : -1, sj = d.y > 0f ? 1 : -1;
            float ax = Mathf.Abs(d.x), ay = Mathf.Abs(d.y);
            // Amanatides-Woo: the fraction of the segment at the next column / row crossing.
            float stepX = ax > 0f ? 16f / ax : float.MaxValue, stepY = ay > 0f ? 16f / ay : float.MaxValue;
            float nextX = ax > 0f ? (si > 0 ? (i + 1) * 16f - a.x : a.x - i * 16f) / ax : float.MaxValue;
            float nextY = ay > 0f ? (sj > 0 ? (j + 1) * 16f - a.y : a.y - j * 16f) / ay : float.MaxValue;
            for (int n = 0; n < 512; n++)
            {
                if (nextX < nextY)
                {
                    nextX += stepX;
                    i += si;
                }
                else
                {
                    nextY += stepY;
                    j += sj;
                }
                if (i == i1 && j == j1) return true;
                if (nextX > 1f && nextY > 1f) return true;
                if (IsSolid(i, -j - 1)) return false;
            }
            return true;
        }

        /// <summary>
        /// A cell can be dug if it holds something, unless it lies within BorderCells of a region
        /// edge that no other region continues past (the edge of the known world).
        /// </summary>
        public static bool CanDig(int gx, int gy)
        {
            var r = RegionAt(gx, gy);
            if (r == null) return false;
            int lx = gx - r.CellOffset.x, ly = gy - r.CellOffset.y;
            if (r.Grid.Get(lx, ly) == CellKind.Empty) return false;
            // A boss room stays as it is (mining, explosions).
            if (BossRooms.Locked(gx, gy)) return false;
            // WorldGen.CanKillTile: the ground a chest stands on stays.
            if (Chests.Covers(new Vector2Int(gx, gy), orUnder: true)) return false;
            // Within BorderCells of where this region stops, the world must carry on in another region.
            for (int k = 1; k <= TerrainSystem.BorderCells; k++)
            {
                if (!r.Contains(gx - k, gy) && !Continues(r, gx - k, gy)) return false;
                if (!r.Contains(gx + k, gy) && !Continues(r, gx + k, gy)) return false;
                if (!r.Contains(gx, gy - k) && !Continues(r, gx, gy - k)) return false;
                if (!r.Contains(gx, gy + k) && !Continues(r, gx, gy + k)) return false;
            }
            return true;
        }

        /// <summary>Whether any loaded scene's foreground scenery is drawn over a point of the gameplay plane.</summary>
        public static bool CoveredByScenery(Vector3 point, Vector3 camera)
        {
            var c = CellGrid.WorldToCell(point);
            var at = RegionAt(c.x, c.y);
            // A hole's mask opens every scene's cuttable art at that spot, not just its own scene's.
            bool hole = at != null && at.Holes.HasMask((c.y - at.CellOffset.y) * at.Grid.Width + c.x - at.CellOffset.x);
            foreach (var t in TerrainSystem.All)
                if (t.Region != null && t.Region.Holes.Covers(point, camera, hole)) return true;
            return false;
        }

        static bool Continues(TerrainRegion from, int gx, int gy)
        {
            var other = RegionAt(gx, gy);
            return other != null && other != from;
        }
    }
}
