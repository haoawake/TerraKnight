using System;

namespace TerraKnight.World
{
    /// <summary>Covers the cells matching a predicate with as few rectangles as a greedy sweep finds.</summary>
    internal sealed class GreedyRects
    {
        readonly bool[] used;
        readonly int size;

        public GreedyRects(int maxSize)
        {
            size = maxSize;
            used = new bool[maxSize * maxSize];
        }

        /// <summary>Emits (x, y, w, h) for the region [x0, x1) x [y0, y1), which must fit in maxSize.</summary>
        public void Run(int x0, int y0, int x1, int y1, Func<int, int, bool> match, Action<int, int, int, int> emit)
        {
            Array.Clear(used, 0, used.Length);
            for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
            {
                if (used[(y - y0) * size + x - x0] || !match(x, y)) continue;

                int w = 1;
                while (x + w < x1 && !used[(y - y0) * size + x + w - x0] && match(x + w, y)) w++;
                int h = 1;
                while (y + h < y1 && RowFree(x, y + h, w, x0, y0, match)) h++;
                for (int yy = y; yy < y + h; yy++)
                for (int xx = x; xx < x + w; xx++)
                    used[(yy - y0) * size + xx - x0] = true;
                emit(x, y, w, h);
            }
        }

        bool RowFree(int x, int y, int w, int x0, int y0, Func<int, int, bool> match)
        {
            for (int i = 0; i < w; i++)
                if (used[(y - y0) * size + x + i - x0] || !match(x + i, y)) return false;
            return true;
        }
    }
}
