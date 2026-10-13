namespace TerraKnight.Terraria
{
    /// <summary>
    /// Picks the frame of a standard Terraria block sheet (Tiles_N, 16px frames on an 18px grid)
    /// from which of its four neighbours are solid, the same layout vanilla blocks use.
    /// </summary>
    internal static class TileFraming
    {
        public const int FrameSize = 16;
        public const int FrameStride = 18;

        // Indexed by (up << 3) | (down << 2) | (left << 1) | right. Each entry is the frame of
        // variant 0 and the step to variants 1 and 2, in sheet pixels.
        static readonly (int x, int y, int dx, int dy)[] table =
        {
            (162, 54, 18, 0),  // isolated
            (162, 0, 0, 18),   // right only
            (216, 0, 0, 18),   // left only
            (108, 72, 18, 0),  // left + right
            (108, 0, 18, 0),   // down only
            (0, 54, 36, 0),    // down + right: top-left corner
            (18, 54, 36, 0),   // down + left: top-right corner
            (18, 0, 18, 0),    // top edge
            (108, 54, 18, 0),  // up only
            (0, 72, 36, 0),    // up + right: bottom-left corner
            (18, 72, 36, 0),   // up + left: bottom-right corner
            (18, 36, 18, 0),   // bottom edge
            (90, 0, 0, 18),    // up + down
            (0, 0, 0, 18),     // left edge
            (72, 0, 0, 18),    // right edge
            (18, 18, 18, 0),   // surrounded
        };

        /// <summary>Top-left pixel of the frame inside the sheet. variant is 0..2.</summary>
        public static (int x, int y) Frame(bool up, bool down, bool left, bool right, int variant)
        {
            int index = (up ? 8 : 0) | (down ? 4 : 0) | (left ? 2 : 0) | (right ? 1 : 0);
            var f = table[index];
            return (f.x + f.dx * variant, f.y + f.dy * variant);
        }

        public const int WallFrameSize = 32;
        public const int WallFrameStride = 36;

        // Wall sheets (Wall_N) use 32px frames on a 36px grid, drawn 8px past the tile on every
        // side so neighbouring walls overlap. Indexed by up 1 | left 2 | right 4 | down 8; three
        // variants each, in grid units.
        static readonly (int x, int y)[][] wallTable =
        {
            new[] { (9, 3), (10, 3), (11, 3) },
            new[] { (6, 3), (7, 3), (8, 3) },
            new[] { (12, 0), (12, 1), (12, 2) },
            new[] { (1, 4), (3, 4), (5, 4) },
            new[] { (9, 0), (9, 1), (9, 2) },
            new[] { (0, 4), (2, 4), (4, 4) },
            new[] { (6, 4), (7, 4), (8, 4) },
            new[] { (1, 2), (2, 2), (3, 2) },
            new[] { (6, 0), (7, 0), (8, 0) },
            new[] { (5, 0), (5, 1), (5, 2) },
            new[] { (1, 3), (3, 3), (5, 3) },
            new[] { (4, 0), (4, 1), (4, 2) },
            new[] { (0, 3), (2, 3), (4, 3) },
            new[] { (0, 0), (0, 1), (0, 2) },
            new[] { (1, 0), (2, 0), (3, 0) },
            new[] { (1, 1), (2, 1), (3, 1) },
        };

        /// <summary>Top-left pixel of a wall frame inside a Wall_N sheet.</summary>
        public static (int x, int y) WallFrame(bool up, bool down, bool left, bool right, int variant)
        {
            int index = (up ? 1 : 0) | (left ? 2 : 0) | (right ? 4 : 0) | (down ? 8 : 0);
            var f = wallTable[index][variant];
            return (f.x * WallFrameStride, f.y * WallFrameStride);
        }

        /// <summary>Stable per-cell variant so frames don't flicker when a neighbour changes.</summary>
        public static int Variant(int cellX, int cellY)
        {
            unchecked
            {
                uint h = (uint)(cellX * 73856093) ^ (uint)(cellY * 19349663);
                return (int)(h % 3);
            }
        }
    }
}
