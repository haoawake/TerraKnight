using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight.World
{
    internal static class BuildingTools
    {
        public static bool PoundReady = true;
        static TerrainRegion damagedRegion;
        static Vector2Int damagedCell;
        static int wallDamage, damagedWall;

        public static bool CanShape(Vector2Int cell)
        {
            var kind = TerraWorld.Get(cell.x, cell.y);
            if (kind == CellKind.Original || kind == CellKind.MagicIce || (!Cells.Blocks(kind) && kind != CellKind.Platform)) return false;
            return TerraWorld.CanDig(cell.x, cell.y) && !Stations.Covers(cell + Vector2Int.up);
        }

        public static bool CanPlaceWall(Vector2Int cell)
        {
            int x = cell.x, y = cell.y;
            if (!TerraWorld.InWorld(x, y) || BossRooms.Locked(x, y) || TerraWorld.HasWall(x, y)) return false;
            return TerraWorld.IsOccupied(x - 1, y) || TerraWorld.IsOccupied(x + 1, y)
                || TerraWorld.IsOccupied(x, y - 1) || TerraWorld.IsOccupied(x, y + 1)
                || TerraWorld.HasWall(x - 1, y) || TerraWorld.HasWall(x + 1, y)
                || TerraWorld.HasWall(x, y - 1) || TerraWorld.HasWall(x, y + 1);
        }

        public static bool CanHammerWall(Vector2Int cell)
        {
            if (!TerraWorld.InWorld(cell.x, cell.y) || BossRooms.Locked(cell.x, cell.y) || !TerraWorld.HasWall(cell.x, cell.y)) return false;
            if (TerraWorld.GetWall(cell.x, cell.y) > 0) return true;
            // Natural walls must be opened from their edge; they never drop a wall item.
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if (!TerraWorld.HasWall(cell.x + dx, cell.y + dy) || TerraWorld.GetWall(cell.x + dx, cell.y + dy) > 0) return true;
            return false;
        }

        public static int NextShape(Vector2Int cell)
        {
            int x = cell.x, y = cell.y, s = TerraWorld.GetSlope(x, y);
            bool right = TerraWorld.IsFullSolid(x + 1, y), left = TerraWorld.IsFullSolid(x - 1, y);
            if (TerraWorld.IsPlatform(x, y))
            {
                int first = TerraWorld.IsPlatform(x + 1, y + 1) || TerraWorld.IsPlatform(x - 1, y - 1) || (right && !left) ? 2 : 1;
                return s == CellGrid.HalfBrick ? 0 : s == 0 ? first : s == first ? 3 - first : CellGrid.HalfBrick;
            }
            int rs = TerraWorld.GetSlope(x + 1, y);
            int a = (right || rs == 1 || rs == 3) && !left ? 2 : 1, b = 3 - a;
            bool ceiling = TerraWorld.IsFullSolid(x, y + 1) && !TerraWorld.IsFullSolid(x, y - 1);
            if (ceiling) { a += 2; b += 2; }
            int c = ceiling ? a - 2 : a + 2, d = ceiling ? b - 2 : b + 2;
            return s == 0 ? CellGrid.HalfBrick : s == CellGrid.HalfBrick ? a : s == a ? b : s == b ? c : s == c ? d : 0;
        }

        /// <returns>True when this hit was on a wall (half useTime).</returns>
        public static bool Hammer(ItemDef hammer, Vector2Int cell, bool smart)
        {
            if (!Net.WorldSync.CanInteract) return false;
            if (!smart && PoundReady && CanShape(cell))
            {
                PoundReady = false;
                int shape = NextShape(cell);
                TerraWorld.SetSlope(cell.x, cell.y, shape);
                TerraMod.MineDust(cell, TerraWorld.Get(cell.x, cell.y), 3);
                TerraMod.Instance?.PlaySound("Dig_" + Random.Range(0, 3));
                // WorldGen.SlopeTile/PoundTile puts an overlapping player above a restored top.
                if (shape == 0 || shape == CellGrid.HalfBrick) LiftFromRestoredTop(cell, shape);
                return false;
            }
            PoundReady = false;
            if (!CanHammerWall(cell)) return false;
            var region = TerraWorld.RegionAt(cell.x, cell.y);
            int wall = TerraWorld.GetWall(cell.x, cell.y);
            if (region != damagedRegion || cell != damagedCell || wall != damagedWall)
            {
                damagedRegion = region; damagedCell = cell; damagedWall = wall; wallDamage = 0;
            }
            wallDamage += (int)(hammer.HammerPower * 1.5f);
            TerraMod.Instance?.PlaySound("Dig_" + Random.Range(0, 3));
            TerraMod.MineDust(cell, CellKind.Stone, wallDamage >= 100 ? 10 : 3);
            if (wallDamage >= 100)
            {
                TerraWorld.SetWall(cell.x, cell.y, 0);
                if (wall > 0)
                    foreach (var item in ItemDb.All)
                        if (item != null && item.PlacesWall == wall) { WorldItems.Drop(ItemStack.Of(item.Type), CellGrid.CellCenter(cell.x, cell.y)); break; }
                wallDamage = 0;
            }
            return true;
        }

        static void LiftFromRestoredTop(Vector2Int cell, int shape)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null) return;
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            float c = CellGrid.CellSize, left = cell.x * c, bottom = cell.y * c;
            float top = bottom + (shape == CellGrid.HalfBrick ? c / 2 : c);
            if (b.max.x <= left || b.min.x >= left + c || b.min.y >= top || b.max.y <= bottom) return;
            hero.transform.position += Vector3.up * (top - b.min.y + 0.001f);
        }

        public static void UnslopeForPlacement(Vector2Int cell, CellKind placed)
        {
            if (Cells.Blocks(placed))
            {
                int below = TerraWorld.GetSlope(cell.x, cell.y - 1), above = TerraWorld.GetSlope(cell.x, cell.y + 1);
                if (TerraWorld.IsSolid(cell.x, cell.y - 1) && (below == 1 || below == 2 || below == CellGrid.HalfBrick)) TerraWorld.SetSlope(cell.x, cell.y - 1, 0);
                if (TerraWorld.IsSolid(cell.x, cell.y + 1) && (above == 3 || above == 4)) TerraWorld.SetSlope(cell.x, cell.y + 1, 0);
            }
            if (placed == CellKind.Torch && TerraWorld.IsSolid(cell.x, cell.y - 1)) TerraWorld.SetSlope(cell.x, cell.y - 1, 0);
        }
    }
}
