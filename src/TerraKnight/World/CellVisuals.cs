using System.Collections.Generic;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// Draws the Terraria side of the grid: placed blocks and placed walls, and the dug holes in the style the settings
    /// choose (World.HoleShapes): Hollow Knight's, a smooth outline with an ink edge, an inner shadow and dark rock in the
    /// area's colour; or Terraria's, square cells with the area's Terraria wall behind. Also tells the hole cutter and the
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
        // Hollow Knight's holes: the ink on the terrain's edge, the shadow inside, the black fill cut to the outline.
        readonly Dictionary<int, SpriteRenderer> rims = new Dictionary<int, SpriteRenderer>();
        readonly Dictionary<int, SpriteRenderer> shadows = new Dictionary<int, SpriteRenderer>();
        readonly Dictionary<int, SpriteRenderer> fills = new Dictionary<int, SpriteRenderer>();
        readonly bool[] open5 = new bool[25], terrain5 = new bool[25];

        /// <summary>The cell's black fill is a shaped piece of this layer's (a smooth hole's edge), not the fill meshes' square.</summary>
        public bool FillElsewhere(int x, int y) => fills.ContainsKey(y * grid.Width + x);

        /// <summary>Every hole drawn again (the style switched): the cells within reach of a dug one.</summary>
        public void MarkAll()
        {
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                    if (grid.IsDug(x, y)) Mark(x, y, ShapeReach);
            foreach (var layer in new[] { rims, shadows, fills })
                foreach (int i in layer.Keys) dirty.Add(i);
        }

        /// <summary>How far a smooth hole's shapes reach from a changed cell: its 5×5 pictures and the overhang beside it.</summary>
        const int ShapeReach = 4;
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
        public void MarkDirty(int x, int y) => Mark(x, y, HoleShapes.Hollow ? ShapeReach : OverhangReach);

        void Mark(int x, int y, int reach)
        {
            for (int dy = -reach; dy <= reach; dy++)
            for (int dx = -reach; dx <= reach; dx++)
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
                Hide(rims, i);
                Hide(shadows, i);
                Hide(fills, i);
                flamesLayer?.Hide(i);
                return;
            }
            var kind = grid.Get(x, y);
            bool cut = grid.IsCut(x, y) || Overhang(x, y);
            var shape = HoleShapes.Hollow ? ShapeAt(x, y) : null;
            if (shape != null) holes.SetShape(i, x, y, shape.Mask, cut);
            else holes.SetCut(i, x, y, cut);

            // A wall set there; Terraria's way, the area's wall behind every dug cell.
            if (grid.GetWall(x, y) > 0 || (shape == null && grid.HasWall(x, y)))
            {
                var (fx, fy) = TileFraming.WallFrame(grid.HasWall(x, y + 1), grid.HasWall(x, y - 1),
                    grid.HasWall(x - 1, y), grid.HasWall(x + 1, y), TileFraming.Variant(x, y), x, y);
                Show(walls, i, x, y, WallZ, GetSprite(grid.GetWall(x, y) > 0 ? "Wall_" + grid.GetWall(x, y) : DugWallSheet, fx, fy, TileFraming.WallFrameSize), WallTint);
            }
            else if (shape != null && shape.Mask != null && grid.GetWall(x, y) != 0 && (grid.IsDug(x, y) || kind == CellKind.Original))
            {
                // Blended: the area's wall softened and darker, as if running on behind the terrain (inner frames only, no
                // ragged wall edge) and seen only through the holes, so their outline is its only edge; a wall knocked out
                // shows none.
                var (fx, fy) = TileFraming.WallFrame(true, true, true, true, TileFraming.Variant(x, y), x, y);
                Show(walls, i, x, y, WallZ, GetSprite(HoleShapes.WallSheet(DugWallSheet), fx, fy, TileFraming.WallFrameSize), Color.white);
                walls[i].maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            }
            else
            {
                Hide(walls, i);
            }
            if (shape?.Rim != null)
            {
                Show(rims, i, x, y, -0.001f, shape.Rim, Color.white);
                // Over the art it outlines, whatever that art's own order.
                var c = CellGrid.CellCenter(x, y);
                rims[i].sortingOrder = holes.ArtOrderAt(root.TransformPoint(new Vector3(c.x, c.y, 0f))) + 1;
            }
            else Hide(rims, i);
            if (shape?.Shadow != null) Show(shadows, i, x, y, WallZ - 0.002f, shape.Shadow, Color.white);
            else Hide(shadows, i);
            if (shape?.Fill != null) Show(fills, i, x, y, 0f, shape.Fill, Color.white);
            else Hide(fills, i);

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
                // Tiles_4, the plain torch: standing on a block, else on the wall beside it. A regional torch: its row of
                // RegionalTorches.TileSheet, its flame layer over it.
                int fx = Solid(x, y - 1) ? 0 : Solid(x - 1, y) ? 22 : Solid(x + 1, y) ? 44 : 0;
                int style = grid.TorchStyleAt(i);
                Show(blocks, i, x, y, BlockZ, style > 0 ? RegionalTorches.Sprite(style, fx, false) : GetTorchSprite(fx), Color.white);
                if (style > 0) flames.Show(i, x, y, style, fx, BlockZ - 0.0003f);
                else flames.Hide(i);
            }
            else
            {
                Hide(blocks, i);
            }
            if (kind != CellKind.Torch) flamesLayer?.Hide(i);
            if (kind == CellKind.Torch) ShowLight(i, x, y, grid.TorchStyleAt(i));
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
                case CellKind.Meteorite: return "Tiles_37";
                default: return AreaBlocks.Sheet(kind);
            }
        }

        // --- Torch light: a soft warm glow around each torch. ---

        readonly Dictionary<int, SpriteRenderer> lights = new Dictionary<int, SpriteRenderer>();
        // The style each light was made for (a regional torch's own colour).
        readonly Dictionary<int, int> lightStyles = new Dictionary<int, int>();
        RegionalTorchFlames flamesLayer;
        RegionalTorchFlames flames => flamesLayer ?? (flamesLayer = new RegionalTorchFlames(root));
        /// <summary>TorchID.TorchColor of the plain torch (1, 0.95, 0.8), warmed a touch for Hallownest's blue; the alpha is its strength.</summary>
        public static readonly Color TorchLight = new Color(1f, 0.88f, 0.66f, 0.2f);

        /// <summary>Dev: the regional torches' flames drawn in this region.</summary>
        public int DevFlames => flamesLayer?.Count ?? 0;

        void ShowLight(int i, int x, int y, int style)
        {
            // One left from before whose object is gone (its chunk rebuilt): made again; a torch of another style: its colour.
            if (lights.TryGetValue(i, out var had) && had != null && (lightStyles.TryGetValue(i, out int was) ? was : 0) == style) return;
            if (had != null) Object.Destroy(had.gameObject);
            var sr = style > 0 ? Glow.CreateLight(root, RegionalTorches.GlowColor(style), RegionalTorches.LightRgb(style)) : Glow.CreateLight(root, TorchLight);
            var c = CellGrid.CellCenter(x, y);
            sr.transform.localPosition = new Vector3(c.x, c.y + 0.1f, Glow.Z);
            sr.gameObject.AddComponent<Glow.Flicker>();
            lights[i] = sr;
            lightStyles[i] = style;
        }

        void HideLight(int i)
        {
            if (!lights.TryGetValue(i, out var sr)) return;
            lights.Remove(i);
            lightStyles.Remove(i);
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

        /// <summary>
        /// A smooth hole's pictures for the cell (World.HoleShapes), from its 5×5 neighbourhood: which cells are cut (dug,
        /// or the overhang beside a dig) and which are standing terrain; null with nothing cut that near.
        /// </summary>
        HoleShapes.Shape ShapeAt(int x, int y)
        {
            bool any = false;
            for (int dy = -2; dy <= 2; dy++)
                for (int dx = -2; dx <= 2; dx++)
                {
                    int k = (dy + 2) * 5 + dx + 2, cx = x + dx, cy = y + dy;
                    bool o = grid.InBounds(cx, cy) && (grid.IsCut(cx, cy) || Overhang(cx, cy));
                    open5[k] = o;
                    terrain5[k] = !o && IsOriginal(cx, cy);
                    any |= o;
                }
            if (!any) return null;
            bool ink = grid.IsDug(x, y) || (!open5[12] && grid.Get(x, y) == CellKind.Original);
            bool fill = !grid.IsArt(x, y) && (grid.IsDug(x, y) || grid.Get(x, y) == CellKind.Original);
            return HoleShapes.For(open5, terrain5, ink, fill);
        }

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
            sr.sortingOrder = 0;
            sr.maskInteraction = SpriteMaskInteraction.None;
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
