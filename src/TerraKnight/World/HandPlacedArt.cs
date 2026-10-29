using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// The art of the scene's hand-placed terrain (floating platforms, ledges, roof pieces) folded
    /// into the grid. Their sprites often sit on the collider's own object, on the terrain layer,
    /// where the hole cutter's scan of the scenery doesn't look; they are cut open cell by cell
    /// like the rest of the terrain art, and once every cell of a piece is gone its art goes too,
    /// edges and all, so nothing is left that looks like it could still be stood on.
    /// </summary>
    internal sealed class HandPlacedArt
    {
        sealed class Piece
        {
            public string Name;
            public int[] Cells;
            public List<Renderer> Art;
            public bool Gone;
            public bool[] Visible;
        }

        /// <summary>Art of pieces dug away, which nothing may switch back on (a neighbour's art being restored).</summary>
        public static readonly HashSet<Renderer> Destroyed = new HashSet<Renderer>();

        readonly CellGrid grid;
        readonly List<Piece> pieces = new List<Piece>();
        readonly Dictionary<int, List<Piece>> byCell = new Dictionary<int, List<Piece>>();

        public int PieceCount => pieces.Count;

        /// <param name="origin">World position of the grid's cell (0, 0).</param>
        /// <param name="sceneArt">The scene's renderers to pick each piece's art from.</param>
        public HandPlacedArt(CellGrid grid, IEnumerable<Collider2D> colliders, Vector2 origin, List<Renderer> sceneArt, HoleCutter holes)
        {
            this.grid = grid;
            const float c = CellGrid.CellSize;
            // Bounds are native calls; read each renderer's once, near the gameplay plane only.
            var near = new List<(Renderer r, Bounds b)>();
            foreach (var r in sceneArt)
            {
                if (r == null) continue;
                var rb = r.bounds;
                if (Mathf.Abs(rb.center.z) <= 2.5f) near.Add((r, rb));
            }
            foreach (var col in colliders)
            {
                if (col == null) continue;
                var b = col.bounds;
                var cells = new List<int>();
                int x0 = Mathf.Max(0, Mathf.FloorToInt((b.min.x - origin.x) / c)), x1 = Mathf.Min(grid.Width - 1, Mathf.FloorToInt((b.max.x - origin.x) / c));
                int y0 = Mathf.Max(0, Mathf.FloorToInt((b.min.y - origin.y) / c)), y1 = Mathf.Min(grid.Height - 1, Mathf.FloorToInt((b.max.y - origin.y) / c));
                bool area = !(col is EdgeCollider2D);
                for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    if (!grid.IsArt(x, y)) continue;
                    if (area && !col.OverlapPoint(origin + CellGrid.CellCenter(x, y))) continue;
                    cells.Add(y * grid.Width + x);
                }
                if (cells.Count == 0) continue;
                var art = ArtOf(b, near);
                if (art.Count == 0) continue;
                var piece = new Piece { Name = col.name, Cells = cells.ToArray(), Art = art };
                pieces.Add(piece);
                foreach (int i in piece.Cells)
                {
                    if (!byCell.TryGetValue(i, out var list)) byCell[i] = list = new List<Piece>();
                    list.Add(piece);
                }
                var sprites = new List<SpriteRenderer>();
                foreach (var r in art) if (r is SpriteRenderer sr) sprites.Add(sr);
                holes.AddArt(sprites);
            }
            // Saved digging may already have taken whole pieces away.
            foreach (var p in pieces) Check(p);
        }

        /// <summary>
        /// Renderers that make up a piece: near the gameplay plane and lying within its collider
        /// (give or take the art's overhang), so neither the scenery around it nor large backdrops
        /// count.
        /// </summary>
        static List<Renderer> ArtOf(Bounds collider, List<(Renderer r, Bounds b)> near)
        {
            const float overhang = 0.75f;
            var list = new List<Renderer>();
            foreach (var (r, b) in near)
            {
                if (b.min.x < collider.min.x - overhang || b.max.x > collider.max.x + overhang) continue;
                if (b.min.y < collider.min.y - overhang || b.max.y > collider.max.y + overhang) continue;
                list.Add(r);
            }
            return list;
        }

        public void CellChanged(int x, int y)
        {
            if (byCell.TryGetValue(y * grid.Width + x, out var list))
                foreach (var p in list) Check(p);
        }

        void Check(Piece p)
        {
            if (p.Gone)
            {
                bool restored = false;
                foreach (int i in p.Cells) if (grid.GetAt(i) == CellKind.Original && !grid.IsDugAt(i)) { restored = true; break; }
                if (!restored) return;
                p.Gone = false;
                for (int k = 0; k < p.Art.Count; k++)
                {
                    var r = p.Art[k];
                    if (r == null) continue;
                    r.enabled = p.Visible[k];
                    Destroyed.Remove(r);
                }
                return;
            }
            foreach (int i in p.Cells)
                if (Cells.Blocks(grid.GetAt(i))) return;
            p.Gone = true;
            p.Visible = p.Art.ConvertAll(r => r != null && r.enabled).ToArray();
            foreach (var r in p.Art)
            {
                if (r == null) continue;
                r.enabled = false;
                Destroyed.Add(r);
            }
            Plugin.Log.LogInfo($"[手放地形] {p.Name} 的 {p.Cells.Length} 格全挖掉了，藏起它的 {p.Art.Count} 个贴图");
        }
    }
}
