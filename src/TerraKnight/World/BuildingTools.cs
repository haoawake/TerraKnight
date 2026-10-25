using System.Collections.Generic;
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
            if (kind == CellKind.Original || kind == CellKind.MagicIce || (!Cells.Blocks(kind) && !TerraKnight.World.Cells.IsPlatform(kind))) return false;
            return TerraWorld.CanDig(cell.x, cell.y) && !Stations.Covers(cell + Vector2Int.up);
        }

        public static bool CanPlaceWall(Vector2Int cell)
        {
            int x = cell.x, y = cell.y;
            if (!TerraWorld.InWorld(x, y) || TerraWorld.HasWall(x, y)) return false;
            return TerraWorld.IsOccupied(x - 1, y) || TerraWorld.IsOccupied(x + 1, y)
                || TerraWorld.IsOccupied(x, y - 1) || TerraWorld.IsOccupied(x, y + 1)
                || TerraWorld.HasWall(x - 1, y) || TerraWorld.HasWall(x + 1, y)
                || TerraWorld.HasWall(x, y - 1) || TerraWorld.HasWall(x, y + 1);
        }

        // ------------------------------------------------------------------ the smart cursor's walls and torches

        static readonly HashSet<Vector2Int> shutDoors = new HashSet<Vector2Int>(), doorHinges = new HashSet<Vector2Int>();
        static readonly List<Vector2Int> torches = new List<Vector2Int>();

        /// <summary>
        /// IsHoveringOverAnInteractableTileThatBlocksSmartCursor: the mouse on a tile TileID.Sets.DisableSmartCursor holds,
        /// within reach (a torch, a door, a bed, a chest, a bank...), and the smart cursor picks nothing: the item goes
        /// where the mouse is.
        /// </summary>
        public static bool SmartCursorOffAt(Vector2Int mouseCell, RectInt reach)
        {
            if (!reach.Contains(mouseCell)) return false;
            return TerraWorld.Get(mouseCell.x, mouseCell.y) == CellKind.Torch || Stations.DisablesSmartCursorAt(mouseCell) || Chests.Covers(mouseCell);
        }

        /// <summary>
        /// SmartCursorHelper.Step_Walls: of the cells in reach that take a wall (CanPlaceWall: none there yet, a block or
        /// a wall beside it) and hold no solid block (a platform, a torch or nothing; not an open door's hinge column,
        /// WorldGen.IsOpenDoorAnchorFrame), the ones the player can see (Collision.CanHitWithCheck from his middle with
        /// DelegateMethods.NotDoorStand), the one nearest the mouse. Sight is what keeps the walls in the room the player
        /// stands in: the house's own blocks hide everything outside, and so do its doors, shut or open (an open door's
        /// hinge column stops the line as its frame).
        /// </summary>
        public static bool SmartWall(Bounds hitbox, RectInt reach, Vector2 mouse, out Vector2Int target)
        {
            target = default;
            Stations.DoorCells(shutDoors, doorHinges);
            var from = CellGrid.WorldToCell(hitbox.center);
            float best = float.MaxValue;
            bool found = false;
            for (int x = reach.xMin; x < reach.xMax; x++)
                for (int y = reach.yMin; y < reach.yMax; y++)
                {
                    var c = new Vector2Int(x, y);
                    if (!CanPlaceWall(c) || Cells.Blocks(TerraWorld.Get(x, y)) || shutDoors.Contains(c) || doorHinges.Contains(c)) continue;
                    float d = ((Vector2)CellGrid.CellCenter(x, y) - mouse).sqrMagnitude;
                    if (d >= best || !InSight(from, c)) continue;
                    best = d;
                    target = c;
                    found = true;
                }
            return found;
        }

        /// <summary>Dev: Step_Walls' look from a cell: whether the player there would see that cell.</summary>
        public static bool DevInSight(Vector2Int from, Vector2Int to)
        {
            Stations.DoorCells(shutDoors, doorHinges);
            return InSight(from, to);
        }

        /// <summary>
        /// Collision.CanHitWithCheck on the grid, cell by cell from a to b (the axis further to go first): a solid block
        /// or a shut door in the way stops it, and so does a step sideways between two full blocks above and below it (or
        /// up between two either side); the check (NotDoorStand) stops it on an open door's hinge column.
        /// </summary>
        static bool InSight(Vector2Int a, Vector2Int b)
        {
            int x = a.x, y = a.y;
            for (int n = 0; n < 1024; n++)
            {
                if (x == b.x && y == b.y) return true;
                if (Mathf.Abs(x - b.x) > Mathf.Abs(y - b.y))
                {
                    x += x < b.x ? 1 : -1;
                    if (FullBlock(x, y - 1) && FullBlock(x, y + 1)) return false;
                }
                else
                {
                    y += y < b.y ? 1 : -1;
                    if (FullBlock(x - 1, y) && FullBlock(x + 1, y)) return false;
                }
                if (BlocksSight(x, y) || doorHinges.Contains(new Vector2Int(x, y))) return false;
            }
            return false;
        }

        /// <summary>Main.tileSolid and not Main.tileSolidTop: a block (platforms aren't), or a shut door.</summary>
        static bool BlocksSight(int x, int y) => Cells.Blocks(TerraWorld.Get(x, y)) || shutDoors.Contains(new Vector2Int(x, y));

        /// <summary>That, with no slope and not a half brick.</summary>
        static bool FullBlock(int x, int y) => (Cells.Blocks(TerraWorld.Get(x, y)) && TerraWorld.GetSlope(x, y) == 0) || shutDoors.Contains(new Vector2Int(x, y));

        /// <summary>
        /// SmartCursorHelper.Step_Torch: of the cells in reach a torch can go in (TerraMod.CanPlace) and that hold it by
        /// IsValidSpotForTorch's rules (a wall behind; a solid block beside it whose slope doesn't fall away from it; a
        /// flat, whole block or platform under it), those with no torch within eight tiles either way, the one nearest the
        /// mouse. Never in liquid (CanPlace). A
        /// torch held with the smart cursor on lights a place that is dark, a torch's width apart from the last.
        /// </summary>
        public static bool SmartTorch(RectInt reach, Vector2 mouse, out Vector2Int target)
        {
            target = default;
            const int Apart = 8;
            torches.Clear();
            for (int x = reach.xMin - Apart; x < reach.xMax + Apart; x++)
                for (int y = reach.yMin - Apart; y < reach.yMax + Apart; y++)
                    if (TerraWorld.Get(x, y) == CellKind.Torch) torches.Add(new Vector2Int(x, y));
            float best = float.MaxValue;
            bool found = false;
            for (int x = reach.xMin; x < reach.xMax; x++)
                for (int y = reach.yMin; y < reach.yMax; y++)
                {
                    float d = ((Vector2)CellGrid.CellCenter(x, y) - mouse).sqrMagnitude;
                    if (d >= best) continue;
                    var c = new Vector2Int(x, y);
                    if (!TerraMod.CanPlace(c, CellKind.Torch) || !TorchSpot(x, y)) continue;
                    bool near = false;
                    foreach (var t in torches)
                        if (Mathf.Abs(t.x - x) <= Apart && Mathf.Abs(t.y - y) <= Apart) { near = true; break; }
                    if (near) continue;
                    best = d;
                    target = c;
                    found = true;
                }
            return found;
        }

        /// <summary>
        /// IsValidSpotForTorch: a wall behind; else a solid block on the left not of slope 1 or 3, or one on the right not
        /// of slope 2 or 4 (a half brick counts as unsloped there); else a flat, whole solid block or platform under it.
        /// </summary>
        static bool TorchSpot(int x, int y)
        {
            if (TerraWorld.HasWall(x, y)) return true;
            int Slope(int sx, int sy)
            {
                int s = TerraWorld.GetSlope(sx, sy);
                return s == CellGrid.HalfBrick ? 0 : s;
            }
            if (TerraWorld.IsSolid(x - 1, y) && (Slope(x - 1, y) == 0 || Slope(x - 1, y) % 2 != 1)) return true;
            if (TerraWorld.IsSolid(x + 1, y) && (Slope(x + 1, y) == 0 || Slope(x + 1, y) % 2 != 0)) return true;
            // Under it a flat block or a flat platform (TileID.Sets.Platforms).
            return (TerraWorld.IsSolid(x, y - 1) || TerraWorld.IsPlatform(x, y - 1)) && TerraWorld.GetSlope(x, y - 1) == 0;
        }

        // ------------------------------------------------------------------ hammers

        public static bool CanHammerWall(Vector2Int cell)
        {
            if (!TerraWorld.InWorld(cell.x, cell.y) || !TerraWorld.HasWall(cell.x, cell.y)) return false;
            if (BossRooms.ProtectsNativeArea(cell.x, cell.y) && TerraWorld.GetWall(cell.x, cell.y) <= 0) return false;
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
