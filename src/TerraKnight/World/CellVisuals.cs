using System.Collections.Generic;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// Draws the Terraria side of the grid: placed blocks, a Terraria wall behind every dug-out
    /// cell, and a ring of Terraria blocks on the original terrain around each hole so the edge
    /// of a dig reads as a Terraria cave cut into Hallownest. Also tells the hole cutter and the
    /// fill meshes which cells are open.
    /// </summary>
    internal sealed class CellVisuals
    {
        // Gameplay happens around z = 0; the hero sits at 0.004 and anything nearer the camera
        // (smaller z) draws over him. Keep the Terraria layers just behind him.
        public const float BlockZ = 0.006f;
        public const float WallZ = 0.009f;
        public const float PixelsPerUnit = TileFraming.FrameSize / CellGrid.CellSize;

        // Hallownest is dark; full-bright Terraria art glows against it.
        static readonly Color BlockTint = new Color(0.82f, 0.82f, 0.88f);
        // Neutral, so each area's wall (Areas.WallFor) keeps its own colour, only darker.
        static readonly Color WallTint = new Color(0.7f, 0.7f, 0.72f);

        readonly CellGrid grid;
        readonly Transform root;
        readonly HoleCutter holes;
        readonly FillMeshes fill;
        readonly Dictionary<int, SpriteRenderer> blocks = new Dictionary<int, SpriteRenderer>();
        readonly Dictionary<int, SpriteRenderer> walls = new Dictionary<int, SpriteRenderer>();
        // A stair piece's brace, drawn in the cell under it (keyed by the stair cell).
        readonly Dictionary<int, SpriteRenderer> supports = new Dictionary<int, SpriteRenderer>();
        readonly HashSet<int> dirty = new HashSet<int>();
        readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();
        static readonly Dictionary<(string, int, int), Sprite> sprites = new Dictionary<(string, int, int), Sprite>();

        public string DugWallSheet = "Wall_1";

        /// <summary>Another wall behind the dug cells (dev: each area's wall in one hole).</summary>
        public void SetDugWall(string sheet)
        {
            if (sheet == DugWallSheet) return;
            DugWallSheet = sheet;
            for (int y = 0; y < grid.Height; y++)
            for (int x = 0; x < grid.Width; x++)
                if (grid.IsDug(x, y)) dirty.Add(y * grid.Width + x);
            RefreshDirty();
        }

        public CellVisuals(CellGrid grid, Transform root, HoleCutter holes, FillMeshes fill)
        {
            this.grid = grid;
            this.root = root;
            this.holes = holes;
            this.fill = fill;
        }

        /// <summary>
        /// A cell changed: its neighbours may need new frames, and open cells up to
        /// <see cref="OverhangReach"/> away may now show or hide the terrain's overhanging edge.
        /// </summary>
        public void MarkDirty(int x, int y)
        {
            for (int dy = -OverhangReach; dy <= OverhangReach; dy++)
            for (int dx = -OverhangReach; dx <= OverhangReach; dx++)
                if (grid.InBounds(x + dx, y + dy)) dirty.Add((y + dy) * grid.Width + x + dx);
        }

        /// <summary>How far (cells) the scene's terrain art reaches past its colliders.</summary>
        const int OverhangReach = 2;

        public void RefreshDirty()
        {
            if (dirty.Count == 0) return;
            foreach (int i in dirty) Refresh(i % grid.Width, i / grid.Width);
            dirty.Clear();
        }

        void Refresh(int x, int y)
        {
            int i = y * grid.Width + x;
            fill.MarkDirty(x, y);
            if (!grid.InClip(x, y))
            {
                // Past a joined gate the neighbour draws this space.
                holes.SetCut(i, x, y, false);
                Hide(walls, i);
                Hide(blocks, i);
                Hide(supports, i);
                return;
            }
            var kind = grid.Get(x, y);
            bool cut = grid.IsCut(x, y) || Overhang(x, y);
            holes.SetCut(i, x, y, cut);

            // Wall behind any cell that was dug out of the original terrain.
            if (grid.HasWall(x, y))
            {
                var (fx, fy) = TileFraming.WallFrame(grid.HasWall(x, y + 1), grid.HasWall(x, y - 1),
                    grid.HasWall(x - 1, y), grid.HasWall(x + 1, y), TileFraming.Variant(x, y), x, y);
                Show(walls, i, x, y, WallZ, GetSprite(grid.GetWall(x, y) > 0 ? "Wall_" + grid.GetWall(x, y) : DugWallSheet, fx, fy, TileFraming.WallFrameSize), WallTint);
            }
            else
            {
                Hide(walls, i);
            }

            // The scene's own terrain keeps its own art; only placed blocks are drawn.
            string sheet = SheetFor(kind);
            if (sheet != null)
            {
                var (fx, fy) = TileFraming.Frame(Connects(x, y, 0, 1), Connects(x, y, 0, -1), Connects(x, y, -1, 0), Connects(x, y, 1, 0),
                    TileFraming.Variant(x, y));
                Show(blocks, i, x, y, BlockZ, GetShapedSprite(sheet, fx, fy, grid.GetSlope(x, y)), BlockTint);
            }
            else if (kind == CellKind.Platform)
            {
                // Tiles_19, wood (row 0): the frame its neighbours call for, flat or stair.
                int fx = PlatformStairs.FrameX(grid, x, y);
                var sprite = grid.GetSlope(x, y) == CellGrid.HalfBrick ? GetShapedSprite("Tiles_19", fx, 0, CellGrid.HalfBrick) : GetSprite("Tiles_19", fx, 0, TileFraming.FrameSize);
                Show(blocks, i, x, y, BlockZ, sprite, BlockTint);
            }
            else if (kind == CellKind.Rope)
            {
                // Tiles_213: a straight length, the knotted end at the bottom.
                bool end = grid.Get(x, y - 1) != CellKind.Rope;
                var (fx, fy) = end ? (108, 54) : (90, TileFraming.Variant(x, y) * 18);
                Show(blocks, i, x, y, BlockZ, GetSprite("Tiles_213", fx, fy, TileFraming.FrameSize), BlockTint);
            }
            else if (kind == CellKind.Torch)
            {
                // Tiles_4, the plain torch: standing on a block, else on the wall beside it.
                int fx = Solid(x, y - 1) ? 0 : Solid(x - 1, y) ? 22 : Solid(x + 1, y) ? 44 : 0;
                Show(blocks, i, x, y, BlockZ, GetTorchSprite(fx), Color.white);
            }
            else
            {
                Hide(blocks, i);
            }
            if (kind == CellKind.Torch) ShowLight(i, x, y);
            else HideLight(i);
            int brace = kind == CellKind.Platform ? PlatformStairs.SupportFrameX(grid, x, y) : -1;
            if (brace >= 0) Show(supports, i, x, y - 1, BlockZ + 0.0005f, GetSprite("Tiles_19", brace, 0, TileFraming.FrameSize), BlockTint);
            else Hide(supports, i);
        }

        static string SheetFor(CellKind kind)
        {
            switch (kind)
            {
                case CellKind.Dirt: return "Tiles_0";
                case CellKind.Stone: return "Tiles_1";
                case CellKind.Wood: return "Tiles_30";
                case CellKind.Sand: return "Tiles_53";
                case CellKind.MagicIce: return "Tiles_127";
                default: return null;
            }
        }

        // --- Torch light: a soft warm glow around each torch. ---

        readonly Dictionary<int, SpriteRenderer> lights = new Dictionary<int, SpriteRenderer>();
        /// <summary>TorchID.TorchColor of the plain torch (1, 0.95, 0.8), warmed a touch for Hallownest's blue; the alpha is its strength.</summary>
        public static readonly Color TorchLight = new Color(1f, 0.88f, 0.66f, 0.2f);

        void ShowLight(int i, int x, int y)
        {
            if (lights.ContainsKey(i)) return;
            var sr = Glow.CreateLight(root, TorchLight);
            var c = CellGrid.CellCenter(x, y);
            sr.transform.localPosition = new Vector3(c.x, c.y + 0.1f, Glow.Z);
            sr.gameObject.AddComponent<Glow.Flicker>();
            lights[i] = sr;
        }

        void HideLight(int i)
        {
            if (!lights.TryGetValue(i, out var sr)) return;
            lights.Remove(i);
            if (sr != null) Object.Destroy(sr.gameObject);
        }

        static Sprite GetTorchSprite(int fx)
        {
            if (sprites.TryGetValue(("Tiles_4", fx, -1), out var s)) return s;
            // Torch frames are 20 wide and 20 tall on a 22 px grid, standing on the cell's floor.
            s = TerrariaAssets.Sprite("Tiles_4", fx, 0, 20, 20, PixelsPerUnit, new Vector2(0.5f, 8f / 20f));
            sprites[("Tiles_4", fx, -1)] = s;
            return s;
        }

        /// <summary>
        /// Open air beside a dug edge. The scene's terrain art overhangs its colliders (grass, rock
        /// lips), and with the terrain under it dug away that lip would be left floating, so it is
        /// cut too. Air right beside intact terrain keeps it: that lip still belongs to something.
        /// </summary>
        bool Overhang(int x, int y)
        {
            if (grid.Get(x, y) != CellKind.Empty || grid.IsDug(x, y)) return false;
            if (IsOriginal(x + 1, y) || IsOriginal(x - 1, y) || IsOriginal(x, y + 1) || IsOriginal(x, y - 1)) return false;
            for (int dy = -OverhangReach; dy <= OverhangReach; dy++)
                for (int dx = -OverhangReach; dx <= OverhangReach; dx++)
                    if (grid.IsDug(x + dx, y + dy)) return true;
            return false;
        }

        bool IsOriginal(int x, int y) => grid.InBounds(x, y) && grid.Get(x, y) == CellKind.Original;

        bool Solid(int x, int y) => Cells.Blocks(grid.Get(x, y));

        bool Connects(int x, int y, int dx, int dy)
        {
            if (!Solid(x + dx, y + dy)) return false;
            int s = grid.GetSlope(x, y), n = grid.GetSlope(x + dx, y + dy);
            if (s == CellGrid.HalfBrick && (dy > 0 || (dx != 0 && n != CellGrid.HalfBrick))) return false;
            if (dx > 0 && (s == 1 || s == 3 || n == 2 || n == 4)) return false;
            if (dx < 0 && (s == 2 || s == 4 || n == 1 || n == 3)) return false;
            if (dy > 0 && (s == 1 || s == 2 || n == 3 || n == 4 || n == CellGrid.HalfBrick)) return false;
            if (dy < 0 && (s == 3 || s == 4 || n == 1 || n == 2)) return false;
            return true;
        }

        void Show(Dictionary<int, SpriteRenderer> layer, int i, int x, int y, float z, Sprite sprite, Color tint)
        {
            if (!layer.TryGetValue(i, out var sr))
            {
                sr = pool.Count > 0 ? pool.Pop() : CreateRenderer();
                sr.gameObject.SetActive(true);
                layer[i] = sr;
            }
            var center = CellGrid.CellCenter(x, y);
            sr.transform.localPosition = new Vector3(center.x, center.y, z);
            sr.sprite = sprite;
            sr.color = tint;
        }

        void Hide(Dictionary<int, SpriteRenderer> layer, int i)
        {
            if (!layer.TryGetValue(i, out var sr)) return;
            layer.Remove(i);
            sr.gameObject.SetActive(false);
            pool.Push(sr);
        }

        SpriteRenderer CreateRenderer()
        {
            var go = new GameObject("Terra Cell");
            go.transform.SetParent(root, false);
            return go.AddComponent<SpriteRenderer>();
        }

        static Sprite GetSprite(string sheet, int fx, int fy, int size)
        {
            if (sprites.TryGetValue((sheet, fx, fy), out var s)) return s;
            s = TerrariaAssets.Sprite(sheet, fx, fy, size, size, PixelsPerUnit, new Vector2(0.5f, 0.5f));
            sprites[(sheet, fx, fy)] = s;
            return s;
        }

        static readonly Dictionary<(string, int, int, int), Sprite> shaped = new Dictionary<(string, int, int, int), Sprite>();
        static Sprite GetShapedSprite(string sheet, int fx, int fy, int shape)
        {
            if (shape == 0) return GetSprite(sheet, fx, fy, TileFraming.FrameSize);
            var key = (sheet, fx, fy, shape);
            if (shaped.TryGetValue(key, out var sprite)) return sprite;
            var src = TerrariaAssets.Pixels(sheet, out int w, out int h);
            var dst = new Color32[16 * 16];
            // TileDrawing.DrawBasicTile: translate each two-pixel strip, retaining the frame's
            // top/bottom edge. Merely masking a square cuts away that edge on the diagonal.
            void Copy(int x, int fromY, int toY)
            {
                dst[(15 - toY) * 16 + x] = src[(h - fy - 1 - fromY) * w + fx + x];
            }
            if (shape == CellGrid.HalfBrick)
            {
                for (int x = 0; x < 16; x++) for (int y = 0; y < 8; y++) Copy(x, y, y + 8);
            }
            else
            {
                for (int i = 0; i < 8; i++)
                {
                    int x = shape == 1 || shape == 3 ? 2 * i : 14 - 2 * i;
                    int from = shape <= 2 ? 0 : 2 * i, to = shape <= 2 ? 2 * i : 0;
                    int length = shape <= 2 ? 14 - 2 * i : 16 - 2 * i;
                    for (int y = 0; y < length; y++) { Copy(x, from + y, to + y); Copy(x + 1, from + y, to + y); }
                }
                int edge = shape <= 2 ? 14 : 0;
                for (int x = 0; x < 16; x++) for (int y = edge; y < edge + 2; y++) Copy(x, y, y);
            }
            var tex = TerrariaAssets.Make(sheet + "#shape" + shape, dst, 16, 16);
            sprite = Sprite.Create(tex, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
            shaped[key] = sprite;
            return sprite;
        }
    }
}
