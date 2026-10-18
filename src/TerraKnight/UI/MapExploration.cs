using System;

namespace TerraKnight.UI
{
    /// <summary>Exploration is a local visit, independent of the camera and purchased charts.</summary>
    internal static class MapExploration
    {
        /// <summary>The reach to the sides of the player, in cells.</summary>
        public const int Radius = 18;
        /// <summary>The reach above and below the player: five cells more than to the sides (an upright ellipse).</summary>
        public const int RadiusY = Radius + 5;
        // 70% transparent = 30% of the colour, rounded to a byte (WorldMap.ExplorationColor works it into the colour).
        public const byte ChartedAlpha = 77;

        public static byte Alpha(bool seen, bool charted) => seen ? (byte)255 : charted ? ChartedAlpha : (byte)0;

        public static int Reveal(int width, int height, int ox, int oy, Func<int, int, bool> blocks, Func<int, int, bool> mark)
        {
            if (ox < 0 || oy < 0 || ox >= width || oy >= height) return 0;
            int changed = 0;
            const int rx2 = Radius * Radius, ry2 = RadiusY * RadiusY;
            for (int y = Math.Max(0, oy - RadiusY); y <= Math.Min(height - 1, oy + RadiusY); y++)
                for (int x = Math.Max(0, ox - Radius); x <= Math.Min(width - 1, ox + Radius); x++)
                {
                    int dx = x - ox, dy = y - oy;
                    if (dx * dx * ry2 + dy * dy * rx2 > rx2 * ry2 || !Visible(ox, oy, x, y, blocks)) continue;
                    if (mark(x, y)) changed++;
                }
            return changed;
        }

        static bool Visible(int x, int y, int tx, int ty, Func<int, int, bool> blocks)
        {
            int dx = Math.Abs(tx - x), dy = -Math.Abs(ty - y);
            int sx = x < tx ? 1 : -1, sy = y < ty ? 1 : -1, error = dx + dy;
            while (x != tx || y != ty)
            {
                int step = error * 2;
                if (step >= dy) { error += dy; x += sx; }
                if (step <= dx) { error += dx; y += sy; }
                // The first wall is explored; cells behind it are not.
                if (x == tx && y == ty) return true;
                if (blocks(x, y)) return false;
            }
            return true;
        }
    }
}
