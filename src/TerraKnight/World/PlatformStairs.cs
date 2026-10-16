using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// Terraria's platforms beyond flat rows: the smart cursor building rows and stairs out of
    /// existing platforms (SmartCursorHelper.Step_Platforms), the slope a platform takes as it is
    /// placed (Player.PlaceThing_Tiles_PlaceIt_SpinSmartPlatform), and the frame each one is drawn
    /// with (WorldGen.TileFrame for tile 19). Global cells, y up: Terraria's (i, j-1) is (x, y+1).
    /// A platform's slope is the shape of its top: 1 "\" high on the left, 2 "/" high on the right.
    /// </summary>
    internal static class PlatformStairs
    {
        // --- The smart cursor. ---

        /// <summary>The direction and cell the line being built keeps to while the button is held.</summary>
        static Vector2Int? lockedDirection, lockedCell;

        /// <summary>The use button, smart cursor or held item changed: the line starts over.</summary>
        public static void Unlock()
        {
            lockedDirection = null;
            lockedCell = null;
        }

        /// <summary>A line is being built: nothing goes anywhere else while the button stays down (TileTargetDesired).</summary>
        public static bool Locked => lockedCell.HasValue;

        /// <summary>
        /// Step_Platforms: every platform in reach (and anything non-solid in a row beside one,
        /// sideways only) offers the empty cell next to it in the direction of the mouse as seen
        /// from it, one of eight; the nearest offer to the mouse wins, if it is in reach. A stair
        /// tile can't turn the opposite diagonal, and nothing goes straight up or down beside a
        /// row. The first pick fixes the direction until the button is let go.
        /// </summary>
        public static bool SmartTarget(Vector2 mouse, RectInt reach, out Vector2Int target)
        {
            target = default;
            var m = CellGrid.WorldToCell(mouse);
            if (TerraWorld.IsPlatform(m.x, m.y)) return false;
            // Torches turn the smart cursor off under the mouse (TileID.Sets.DisableSmartCursor).
            if (TerraWorld.Get(m.x, m.y) == CellKind.Torch && reach.Contains(m)) return false;
            bool found = false;
            float bestDist = float.MaxValue, bestDot = float.MinValue;
            Vector2Int best = default, bestDir = default;
            for (int x = reach.xMin; x < reach.xMax; x++)
                for (int y = reach.yMin; y < reach.yMax; y++)
                {
                    var c = CellGrid.CellCenter(x, y);
                    var d = lockedDirection ?? Direction8(mouse - c);
                    bool source = TerraWorld.IsPlatform(x, y);
                    if (!source && d.y == 0 && TerraWorld.IsOccupied(x, y) && !TerraWorld.IsSolid(x, y)
                        && (TerraWorld.IsPlatform(x - 1, y) || TerraWorld.IsPlatform(x + 1, y)))
                        source = true;
                    if (!source) continue;
                    // The slope that would make a corner with d (y up: down-right (1,-1) is "/").
                    int opposite = d.x == -d.y ? 2 : d.x == d.y ? 1 : 0;
                    if (opposite != 0 && TerraWorld.GetSlope(x, y) == opposite) continue;
                    if (d.x == 0 && (TerraWorld.IsPlatform(x - 1, y + d.y) || TerraWorld.IsPlatform(x + 1, y + d.y))) continue;
                    var t = new Vector2Int(x + d.x, y + d.y);
                    if (!TerraWorld.InWorld(t.x, t.y) || TerraWorld.Get(t.x, t.y) != CellKind.Empty || !AllowedForContinuity(t)) continue;
                    var off = mouse - CellGrid.CellCenter(t.x, t.y);
                    float dist = off.magnitude, dot = Vector2.Dot(off, d);
                    if (dist < bestDist || (dist == bestDist && dot > bestDot))
                    {
                        found = true;
                        bestDist = dist;
                        bestDot = dot;
                        best = t;
                        bestDir = d;
                    }
                }
            if (!found || !reach.Contains(best)) return false;
            lockedDirection = bestDir;
            lockedCell = best;
            target = best;
            return true;
        }

        /// <summary>With a line locked, only its last cell or the next two along it.</summary>
        static bool AllowedForContinuity(Vector2Int t)
        {
            if (!lockedCell.HasValue || !lockedDirection.HasValue) return true;
            var l = lockedCell.Value;
            var d = lockedDirection.Value;
            return t == l || t == l + d || t == l + d * 2;
        }

        /// <summary>
        /// GetDesiredDirectionFrom: which of eight 45° sectors (centred on the axes and diagonals)
        /// an offset points into, worked out with y down as Terraria does, so sector edges fall
        /// the same way.
        /// </summary>
        static Vector2Int Direction8(Vector2 offset)
        {
            float a = Mathf.Atan2(-offset.y, offset.x);
            if (a < 0f) a += Mathf.PI * 2f;
            int k = (int)(((a + Mathf.PI / 8f) % (Mathf.PI * 2f)) / (Mathf.PI / 4f));
            float vx = 1.5f * Mathf.Cos(k * Mathf.PI / 4f), vy = 1.5f * Mathf.Sin(k * Mathf.PI / 4f);
            return new Vector2Int((int)vx, -(int)vy);
        }

        // --- Slopes as platforms go down (smart cursor on). ---

        /// <summary>
        /// SpinSmartPlatform, after a platform is placed with the smart cursor on: if it has a
        /// platform among its eight neighbours it takes the slope GetSmartPlatformSlope votes
        /// for, and the stair's upper neighbour (a row's end) and a lone lower neighbour turn
        /// with it; placed flat, it flattens a stair's end beside it that has nowhere left to go.
        /// </summary>
        public static void Spin(int x, int y)
        {
            bool any = false;
            for (int dx = -1; dx <= 1 && !any; dx++)
                for (int dy = -1; dy <= 1 && !any; dy++)
                    if ((dx != 0 || dy != 0) && TerraWorld.IsPlatform(x + dx, y + dy)) any = true;
            if (!any) return;
            int s = SmartSlope(x, y);
            if (s != 0)
            {
                Slope(x, y, s, false);
                int ux = s == 1 ? -1 : 1;
                // The upper neighbour along the stair (up-left of a "\", up-right of a "/").
                if (IsFlatPlatform(x + ux, y + 1))
                {
                    Slope(x + ux, y + 1, s, true);
                    if (IsFlatPlatform(x + 2 * ux, y + 2)) Slope(x + 2 * ux, y + 2, s, true);
                }
                // The lower one, only when it stands alone (never a row's end).
                int lx = x - ux, ly = y - 1;
                if (IsFlatPlatform(lx, ly) && !TerraWorld.IsPlatform(lx + 1, ly) && !TerraWorld.IsPlatform(lx - 1, ly))
                    Slope(lx, ly, s, true);
                return;
            }
            for (int side = -1; side <= 1; side += 2)
            {
                int nx = x + side;
                int ns = TerraWorld.IsPlatform(nx, y) ? TerraWorld.GetSlope(nx, y) : 0;
                if (ns == 0) continue;
                // The stair's lower continuation: down-right of a "\", down-left of a "/".
                int cx = nx + (ns == 1 ? 1 : -1), cy = y - 1;
                if (!TerraWorld.IsPlatform(cx, cy) || TerraWorld.GetSlope(cx, cy) == (ns == 1 ? 2 : 1)) Slope(nx, y, 0, true);
            }
        }

        static bool IsFlatPlatform(int x, int y) => TerraWorld.IsPlatform(x, y) && TerraWorld.GetSlope(x, y) == 0;

        /// <summary>GetSmartPlatformSlope: diagonal platforms vote for the stair they'd continue; blocks beside it lean it; a platform beside it keeps it flat.</summary>
        static int SmartSlope(int x, int y)
        {
            bool above = TerraWorld.IsPlatform(x, y + 1), below = TerraWorld.IsPlatform(x, y - 1);
            int a = 0, b = 0; // votes for "\" and for "/"
            Vote(x - 1, y + 1, 2, above, ref a, 1);
            Vote(x + 1, y + 1, 1, above, ref b, 2);
            Vote(x - 1, y - 1, 1, below, ref b, 2);
            Vote(x + 1, y - 1, 2, below, ref a, 1);
            if (TerraWorld.IsSolid(x - 1, y)) a++;
            if (TerraWorld.IsSolid(x + 1, y)) b++;
            int r = a > b ? 1 : b > a ? 2 : 0;
            if (TerraWorld.IsPlatform(x - 1, y) || TerraWorld.IsPlatform(x + 1, y)) r = 0;
            return r;
        }

        static void Vote(int x, int y, int against, bool blocked, ref int votes, int slope)
        {
            if (!TerraWorld.IsPlatform(x, y)) return;
            int s = TerraWorld.GetSlope(x, y);
            if (s == against) return;
            votes += s == slope ? 2 : blocked ? 0 : 1;
        }

        /// <summary>WorldGen.SlopeTile: the Dig sound every time, and a few splinters for a neighbour turned with it.</summary>
        static void Slope(int x, int y, int slope, bool effects)
        {
            TerraWorld.SetSlope(x, y, slope);
            var mod = TerraMod.Instance;
            if (mod == null) return;
            mod.PlaySound("Dig_" + Random.Range(0, 3));
            if (effects) TerraMod.MineDust(new Vector2Int(x, y), CellKind.Platform, 3);
        }

        // --- Frames (Tiles_19, wood: row 0). ---

        /// <summary>
        /// WorldGen.TileFrame for a platform, from its neighbours in its own grid: flat pieces
        /// end in caps or braces against blocks and meet stairs with an underside; stair pieces
        /// are capped at an end that leads nowhere. Out of the grid counts as solid rock.
        /// </summary>
        public static int FrameX(CellGrid g, int x, int y)
        {
            int s = g.GetSlope(x, y);
            CellKind L = g.Get(x - 1, y), R = g.Get(x + 1, y);
            if (s == 1)
            {
                if (R == CellKind.Platform && g.GetSlope(x + 1, y) == 0) return 468;
                bool low = g.Get(x + 1, y - 1) != CellKind.Empty;
                bool up = L != CellKind.Empty || (g.Get(x - 1, y + 1) == CellKind.Platform && g.GetSlope(x - 1, y + 1) == 1);
                return low ? (up ? 180 : 396) : (up ? 360 : 432);
            }
            if (s == 2)
            {
                if (L == CellKind.Platform && g.GetSlope(x - 1, y) == 0) return 450;
                bool low = g.Get(x - 1, y - 1) != CellKind.Empty;
                bool up = R != CellKind.Empty || (g.Get(x + 1, y + 1) == CellKind.Platform && g.GetSlope(x + 1, y + 1) == 2);
                return low ? (up ? 144 : 378) : (up ? 342 : 414);
            }
            const int Self = 1, Other = 2, None = 0;
            int Side(CellKind k) => k == CellKind.Platform ? Self : Cells.Blocks(k) ? Other : None;
            int left = Side(L), right = Side(R);
            int ls = L == CellKind.Platform ? g.GetSlope(x - 1, y) : 0, rs = R == CellKind.Platform ? g.GetSlope(x + 1, y) : 0;
            // A "\" stair ending on our top-left corner, a "/" on our top-right.
            if (left == None && g.Get(x - 1, y + 1) == CellKind.Platform && g.GetSlope(x - 1, y + 1) == 1) left = Self;
            if (right == None && g.Get(x + 1, y + 1) == CellKind.Platform && g.GetSlope(x + 1, y + 1) == 2) right = Self;
            if (left == Self && ls == 2 && right != Self) right = None;
            if (right == Self && rs == 1 && left != Self) left = None;
            if (left == Self && right == Self) return ls == 2 && rs == 1 ? 252 : ls == 2 ? 216 : rs == 1 ? 234 : 0;
            if (left == Self && right == None) return ls != 2 ? 18 : 270;
            if (left == None && right == Self) return rs != 1 ? 36 : 288;
            if (left == Other && right == Self) return 54;
            if (left == Self && right == Other) return 72;
            if (left == Other && right == None) return 108;
            if (left == None && right == Other) return 126;
            return 90;
        }

        /// <summary>
        /// TileDrawing.DrawBasicTile: a stair piece resting on what continues it below (the next
        /// stair piece, a block or a row) gets a diagonal brace drawn in the cell under it, behind
        /// the tiles. -1 for none; also none where a block fills that cell anyway.
        /// </summary>
        public static int SupportFrameX(CellGrid g, int x, int y)
        {
            int s = g.GetSlope(x, y);
            if ((s != 1 && s != 2) || !g.InBounds(x, y - 1) || Cells.Blocks(g.Get(x, y - 1))) return -1;
            int nx = s == 1 ? x + 1 : x - 1;
            var n = g.Get(nx, y - 1);
            bool solid = Cells.Blocks(n) || n == CellKind.Platform;
            int ns = n == CellKind.Platform ? g.GetSlope(nx, y - 1) : 0;
            if (!solid || ns == (s == 1 ? 2 : 1)) return -1;
            bool flat = n == CellKind.Platform && ns == 0;
            return s == 1 ? (flat ? 324 : 198) : (flat ? 306 : 162);
        }
    }
}
