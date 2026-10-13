using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// Turns the scene's terrain colliders into grid cells. tk2d builds terrain as closed
    /// EdgeCollider2D loops, so a cell is solid when its centre is inside an odd number of them.
    /// Coordinates are taken relative to the scene's origin, which is not world zero for a
    /// neighbouring scene moved into place.
    /// </summary>
    internal static class GridRasterizer
    {
        public static int Fill(CellGrid grid, IEnumerable<EdgeCollider2D> colliders, Vector2 origin, out int segmentCount)
        {
            var segments = new List<Vector4>();
            foreach (var col in colliders) AddSegments(col, origin, segments);
            segmentCount = segments.Count;
            return FillLoops(grid, segments, (x, y) => grid.Init(x, y, CellKind.Original));
        }

        /// <summary>
        /// The scene's hand-placed terrain colliders (boxes, polygons, closed edge loops on the
        /// terrain layer that aren't part of the tilemap): solid cells the scene's art draws.
        /// </summary>
        public static int FillExtra(CellGrid grid, IEnumerable<Collider2D> colliders, Vector2 origin)
        {
            int filled = 0;
            const float c = CellGrid.CellSize;
            foreach (var col in colliders)
            {
                if (col is EdgeCollider2D edge)
                {
                    var pts = edge.points;
                    if (pts.Length < 3 || (pts[0] - pts[pts.Length - 1]).sqrMagnitude > 0.01f) continue;
                    var segments = new List<Vector4>();
                    AddSegments(edge, origin, segments);
                    filled += FillLoops(grid, segments, (x, y) => grid.InitArt(x, y));
                    continue;
                }
                var b = col.bounds;
                int x0 = Mathf.Max(0, Mathf.FloorToInt((b.min.x - origin.x) / c)), x1 = Mathf.Min(grid.Width - 1, Mathf.FloorToInt((b.max.x - origin.x) / c));
                int y0 = Mathf.Max(0, Mathf.FloorToInt((b.min.y - origin.y) / c)), y1 = Mathf.Min(grid.Height - 1, Mathf.FloorToInt((b.max.y - origin.y) / c));
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var center = origin + CellGrid.CellCenter(x, y);
                    if (!col.OverlapPoint(center) || grid.Get(x, y) != CellKind.Empty) continue;
                    grid.InitArt(x, y);
                    filled++;
                }
            }
            return filled;
        }

        static void AddSegments(EdgeCollider2D col, Vector2 origin, List<Vector4> segments)
        {
            var points = col.points;
            var t = col.transform;
            Vector2 prev = (Vector2)t.TransformPoint(points[0] + col.offset) - origin;
            for (int i = 1; i < points.Length; i++)
            {
                Vector2 p = (Vector2)t.TransformPoint(points[i] + col.offset) - origin;
                segments.Add(new Vector4(prev.x, prev.y, p.x, p.y));
                prev = p;
            }
        }

        static int FillLoops(CellGrid grid, List<Vector4> segments, System.Action<int, int> mark)
        {
            const float c = CellGrid.CellSize;
            var crossings = new List<float>();
            int filled = 0;
            for (int y = 0; y < grid.Height; y++)
            {
                float cy = (y + 0.5f) * c;
                crossings.Clear();
                foreach (var s in segments)
                {
                    // Half-open test so a vertex shared by two segments is counted once.
                    if ((s.y <= cy) == (s.w <= cy)) continue;
                    float f = (cy - s.y) / (s.w - s.y);
                    crossings.Add(s.x + f * (s.z - s.x));
                }
                crossings.Sort();
                for (int k = 0; k + 1 < crossings.Count; k += 2)
                {
                    int x0 = Mathf.Max(0, Mathf.CeilToInt(crossings[k] / c - 0.5f));
                    int x1 = Mathf.Min(grid.Width - 1, Mathf.FloorToInt(crossings[k + 1] / c - 0.5f));
                    for (int x = x0; x <= x1; x++)
                    {
                        mark(x, y);
                        filled++;
                    }
                }
            }
            return filled;
        }
    }
}
