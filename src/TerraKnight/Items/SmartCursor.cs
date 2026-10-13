using System.Collections.Generic;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Terraria's smart cursor for pickaxes (SmartCursorHelper.Step_Pickaxe_MineSolids), ported
    /// with y pointing up. The mouse doesn't pick the tile: it gives a direction (horizontal,
    /// vertical or diagonal, split at 3:1) and how far to look. Rays go from the player's leading
    /// edge, one per body row (or column), to the same rows on a copy of the hitbox set on the
    /// cursor; each ray's first solid block is a candidate and the one nearest the player's centre
    /// wins. Mining sideways therefore clears whole three-high columns, a tunnel to walk along,
    /// instead of boring in at the cursor's height. Works in Terraria pixels: one cell is a 16 px tile.
    /// </summary>
    internal static class SmartCursor
    {
        const int Tile = 16;
        const float PxPerUnit = Tile / CellGrid.CellSize;
        // Terraria's player hitbox.
        const int W = 20, H = 42, HalfW = W / 2;

        static readonly List<Vector2Int> starts = new List<Vector2Int>(), ends = new List<Vector2Int>();

        /// <summary>The block a pickaxe would mine with smart cursor on, if any is in reach.</summary>
        public static bool PickaxeTarget(Bounds hitbox, Vector2 mouseWorld, System.Func<Vector2Int, bool> inReach, out Vector2Int target)
        {
            target = default;
            float left = hitbox.center.x * PxPerUnit - W / 2f, bottom = hitbox.min.y * PxPerUnit;
            Vector2 m = mouseWorld * PxPerUnit;
            float cx = left + W / 2f, cy = bottom + H / 2f, vx = m.x - cx, vy = m.y - cy;
            int sx = vx > 0f ? 1 : vx < 0f ? -1 : 0, sy = vy > 0f ? 1 : vy < 0f ? -1 : 0;
            if (Mathf.Abs(vx) > Mathf.Abs(vy) * 3f) { sy = 0; m.y = cy; }
            if (Mathf.Abs(vy) > Mathf.Abs(vx) * 3f) { sx = 0; m.x = cx; }
            if (sx == 0 && sy == 0) return false;

            // Rows are walked down from the head, except when aiming diagonally upwards (up from the feet).
            int step = sy == 1 && sx != 0 ? 1 : -1;
            int rows = (H + Tile - 1) / Tile, cols = (W + Tile - 1) / Tile;
            int startRow = step == 1 ? Mathf.FloorToInt((bottom + 1f) / Tile) : Mathf.FloorToInt((bottom + H - 0.1f) / Tile);
            int frontCol = Mathf.FloorToInt((left + HalfW + (HalfW - 1) * sx) / (float)Tile);
            starts.Clear();
            ends.Clear();
            if (sx != 0) for (int i = 0; i < rows; i++) starts.Add(new Vector2Int(frontCol, startRow + i * step));
            if (sy != 0) for (int j = 0; j < cols; j++) starts.Add(new Vector2Int(Mathf.FloorToInt(left / Tile) + j, startRow));

            int endRow = step == 1 ? Mathf.FloorToInt((m.y - H / 2 + 1f) / Tile) : Mathf.FloorToInt((m.y + H / 2 + 0.9f) / Tile);
            if (sx == 0) endRow -= 1;
            int endCol = Mathf.FloorToInt((m.x + (HalfW - 1) * sx) / Tile);
            if (sx != 0) for (int k = 0; k < rows; k++) ends.Add(new Vector2Int(endCol, endRow + k * step));
            if (sy != 0) for (int l = 0; l < cols; l++) ends.Add(new Vector2Int(Mathf.FloorToInt((m.x - HalfW) / Tile) + l, endRow));

            bool found = false;
            float best = float.MaxValue;
            for (int i = 0; i < starts.Count && i < ends.Count; i++)
            {
                var c = HitLine(starts[i], ends[i], sx, sy);
                if (!Blocks(c.x, c.y) || !TerraWorld.CanDig(c.x, c.y)) continue;
                float d = Vector2.Distance(new Vector2(c.x * Tile + 8f, c.y * Tile + 8f), new Vector2(cx, cy));
                if (d < best)
                {
                    best = d;
                    target = c;
                    found = true;
                }
            }
            return found && inReach(target);
        }

        /// <summary>What stops a smart-cursor ray: a solid block (platforms don't).</summary>
        static bool Blocks(int x, int y)
        {
            var k = TerraWorld.Get(x, y);
            return k != CellKind.Empty && k != CellKind.Platform;
        }

        /// <summary>
        /// Collision.HitLine: walks a stepped line from a to b and returns the first blocking cell
        /// (or b). While stepping sideways it also checks the next cell up or down the line, so it
        /// can't slip through a diagonal gap.
        /// </summary>
        static Vector2Int HitLine(Vector2Int a, Vector2Int b, int ignX, int ignY)
        {
            int x = a.x, y = a.y;
            int dx = Mathf.Abs(b.x - a.x), dy = Mathf.Abs(b.y - a.y);
            if (dx == 0 && dy == 0) return a;
            float rx = dx == 0 ? 0f : dy == 0 ? 1f : dx > dy ? (float)dx / dy : 1f;
            float ry = dy == 0 ? 0f : dx == 0 ? 1f : dy > dx ? (float)dy / dx : 1f;
            float ax = 0f, ay = 0f;
            bool xMode = b.y < a.y;
            int nx = dx, ny = dy, gx = System.Math.Sign(b.x - a.x), gy = System.Math.Sign(b.y - a.y);
            bool done = false;
            for (int guard = 0; guard < 4096; guard++)
            {
                if (xMode)
                {
                    ax += rx;
                    int k = (int)ax;
                    ax -= k;
                    for (int s = 0; s < k; s++)
                    {
                        if (ignY != -1 && gy > 0 && Blocks(x, y + 1)) return new Vector2Int(x, y + 1);
                        if (ignY != 1 && gy < 0 && Blocks(x, y - 1)) return new Vector2Int(x, y - 1);
                        if (Blocks(x, y)) return new Vector2Int(x, y);
                        if (nx == 0 && ny == 0) { done = true; break; }
                        x += gx;
                        nx--;
                        if (nx == 0 && ny == 0 && k == 1) done = true;
                    }
                    if (ny != 0) xMode = false;
                }
                else
                {
                    ay += ry;
                    int k = (int)ay;
                    ay -= k;
                    for (int s = 0; s < k; s++)
                    {
                        if (ignX != -1 && gx < 0 && Blocks(x - 1, y)) return new Vector2Int(x - 1, y);
                        if (ignX != 1 && gx > 0 && Blocks(x + 1, y)) return new Vector2Int(x + 1, y);
                        if (Blocks(x, y)) return new Vector2Int(x, y);
                        if (nx == 0 && ny == 0) { done = true; break; }
                        y += gy;
                        ny--;
                        if (nx == 0 && ny == 0 && k == 1) done = true;
                    }
                    if (nx != 0) xMode = true;
                }
                if (Blocks(x, y) || done) return new Vector2Int(x, y);
            }
            return new Vector2Int(x, y);
        }

        static Sprite highlight;

        /// <summary>
        /// Main.DrawSmartCursor's mark: the tile filled with 60% yellow and a 2 px brighter bar just
        /// outside each edge, the corners left open. 20x20 px over a 16 px tile.
        /// </summary>
        public static Sprite Highlight()
        {
            if (highlight != null) return highlight;
            const int n = Tile + 4;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var fill = new Color32(255, 230, 26, 153);
            var bar = new Color32(255, 242, 77, 255);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                bool inX = x >= 2 && x < n - 2, inY = y >= 2 && y < n - 2;
                px[y * n + x] = inX && inY ? fill : inX || inY ? bar : new Color32(0, 0, 0, 0);
            }
            tex.SetPixels32(px);
            tex.Apply();
            highlight = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), PxPerUnit);
            return highlight;
        }
    }
}
