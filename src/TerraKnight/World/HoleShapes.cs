using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// How a dug hole looks (the Terraria settings' switch). Blended, the default: the hole's outline is a smooth curve
    /// through the grid (a Catmull-Rom surface through each cell's centre, open 1 / shut 0, cut at one half: round convex
    /// corners and filled-in concave ones, never a staircase), the scene's terrain art and its black fill both cut to it, a
    /// near-black ink line drawn on the terrain's side of it as Hollow Knight outlines its rock, a soft shadow inside where
    /// the terrain stands over the hole, and behind it the area's Terraria wall, softened and darker (WallSheet), running on
    /// behind the terrain so only the outline bounds it. Terraria's way: the cells cut square with the area's wall as it is
    /// (CellVisuals' plain path). Only the look: the collision stays the cells'.
    /// </summary>
    internal static class HoleShapes
    {
        public static ConfigEntry<int> Setting;

        /// <summary>Blended (0, the default), else Terraria's way (1).</summary>
        public static bool Hollow => Setting == null || Setting.Value != 1;

        public static void Bind(ConfigFile cfg) =>
            Setting = cfg.Bind("Gameplay", "HoleStyle", 0,
                "挖洞的样子：0 = 融合（平滑洞口、墨线描边、洞内阴影，背后是调暗调柔的泰拉瑞亚墙），1 = 泰拉瑞亚原样（方格洞口、原样的泰拉瑞亚墙）。只影响画面，不影响碰撞。\nHow dug holes look: 0 = blended (smooth outline, ink edge, inner shadow, the Terraria wall softened and darker behind), 1 = Terraria's as is (square cells, the wall as it is). Looks only.");

        /// <summary>Switched in the settings: every loaded region's holes drawn again.</summary>
        public static void Set(bool hollow)
        {
            if (Setting != null) Setting.Value = hollow ? 0 : 1;
            foreach (var t in TerrainSystem.All) t.Region?.RestyleHoles();
        }

        /// <summary>Pixels a cell's pictures have (a cell is half a unit).</summary>
        public const int Res = 32;
        public const float PixelsPerUnit = Res / CellGrid.CellSize;

        /// <summary>The ink's reach into the terrain, and the shadow's into the hole, in field units (the field crosses a cell from 0 to 1).</summary>
        const float InkBand = 0.11f, ShadowBand = 0.42f;
        static readonly Color32 Ink = new Color32(6, 6, 10, 255);

        internal sealed class Shape
        {
            /// <summary>The cut (a mask), the ink line, the inner shadow, the black fill left standing; null where there is none.</summary>
            public Sprite Mask, Rim, Shadow, Fill;
            /// <summary>The cut's alpha, Res×Res from the bottom row (HoleCutter draws it into its block's mask); null with no cut.</summary>
            public byte[] MaskAlpha;
            public bool AnyOpen, AnyShut;
        }

        static readonly Dictionary<(long, long, int), Shape> shapes = new Dictionary<(long, long, int), Shape>();
        public static int DevShapes => shapes.Count;

        /// <summary>
        /// The pictures of the cell at the middle of a 5×5 neighbourhood (row by row from the bottom left): which cells are
        /// open (cut), which are standing terrain (Hollow Knight's own); whether this cell takes the ink (terrain, or dug out of
        /// it) and the black fill (an untouched interior cell, or one dug out of it).
        /// </summary>
        public static Shape For(bool[] open, bool[] terrain, bool ink, bool fill)
        {
            long ko = 0, kt = 0;
            for (int i = 0; i < 25; i++)
            {
                if (open[i]) ko |= 1L << i;
                if (terrain[i]) kt |= 1L << i;
            }
            var key = (ko, kt, (ink ? 1 : 0) | (fill ? 2 : 0));
            if (shapes.TryGetValue(key, out var s)) return s;
            s = Build(open, terrain, ink, fill);
            shapes[key] = s;
            return s;
        }

        static float Cr(float p0, float p1, float p2, float p3, float t) =>
            0.5f * (2f * p1 + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t * t + (-p0 + 3f * p1 - 3f * p2 + p3) * t * t * t);

        /// <summary>The surface through the 5×5 cells' centres at a point of the middle cell (u, v from its lower left, 0..1).</summary>
        static float Field(float[] f, float u, float v)
        {
            // Centres sit at index + 0.5; the middle cell is index 2.
            float gx = 2f + u - 0.5f, gy = 2f + v - 0.5f;
            int ix = Mathf.FloorToInt(gx), iy = Mathf.FloorToInt(gy);
            float tx = gx - ix, ty = gy - iy;
            float At(int x, int y) => f[Mathf.Clamp(y, 0, 4) * 5 + Mathf.Clamp(x, 0, 4)];
            float Row(int y) => Cr(At(ix - 1, y), At(ix, y), At(ix + 1, y), At(ix + 2, y), tx);
            return Cr(Row(iy - 1), Row(iy), Row(iy + 1), Row(iy + 2), ty);
        }

        static Shape Build(bool[] open, bool[] terrain, bool ink, bool fill)
        {
            var fo = new float[25];
            var ft = new float[25];
            for (int i = 0; i < 25; i++) { fo[i] = open[i] ? 1f : 0f; ft[i] = terrain[i] ? 1f : 0f; }
            var mask = new Color32[Res * Res];
            var rim = new Color32[Res * Res];
            var shadow = new Color32[Res * Res];
            var black = new Color32[Res * Res];
            bool anyOpen = false, anyShut = false, anyRim = false, anyShadow = false, anyFill = false;
            for (int py = 0; py < Res; py++)
                for (int px = 0; px < Res; px++)
                {
                    float u = (px + 0.5f) / Res, v = (py + 0.5f) / Res;
                    float f = Field(fo, u, v);
                    int i = py * Res + px;
                    if (f >= 0.5f)
                    {
                        anyOpen = true;
                        mask[i] = new Color32(255, 255, 255, 255);
                        // The shadow, deepest by standing terrain, gone a little way into the hole.
                        float t = Field(ft, u, v);
                        float a = Mathf.Clamp01((t - 0.06f) / 0.44f) * Mathf.Clamp01(1f - (f - 0.5f) / ShadowBand);
                        if (a > 0.02f)
                        {
                            anyShadow = true;
                            shadow[i] = new Color32(0, 0, 0, (byte)(Mathf.Pow(a, 1.3f) * 170f));
                        }
                    }
                    else
                    {
                        anyShut = true;
                        if (fill) { black[i] = new Color32(0, 0, 0, 255); anyFill = true; }
                        if (ink && f > 0.5f - InkBand)
                        {
                            // Solid at the edge, its inner side softened.
                            float a = Mathf.Clamp01((f - (0.5f - InkBand)) / (InkBand * 0.45f));
                            anyRim = true;
                            rim[i] = new Color32(Ink.r, Ink.g, Ink.b, (byte)(a * 255f));
                        }
                    }
                }
            byte[] alpha = null;
            if (anyOpen && anyShut)
            {
                alpha = new byte[Res * Res];
                for (int i = 0; i < alpha.Length; i++) alpha[i] = mask[i].a;
            }
            return new Shape
            {
                AnyOpen = anyOpen, AnyShut = anyShut,
                Mask = anyOpen ? (anyShut ? Make("hole mask", mask) : FullMask()) : null,
                MaskAlpha = anyOpen ? alpha ?? FullAlpha : null,
                Rim = anyRim ? Make("hole ink", rim) : null,
                Shadow = anyShadow ? Make("hole shadow", shadow) : null,
                Fill = anyFill && anyOpen ? Make("hole fill", black) : null,
            };
        }

        /// <summary>A whole cell cut.</summary>
        public static readonly byte[] FullAlpha = Filled();

        static byte[] Filled()
        {
            var a = new byte[Res * Res];
            for (int i = 0; i < a.Length; i++) a[i] = 255;
            return a;
        }

        static Sprite fullMask;
        static Sprite FullMask()
        {
            if (fullMask != null) return fullMask;
            var px = new Color32[Res * Res];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            return fullMask = Make("hole mask full", px);
        }

        static Sprite Make(string name, Color32[] px)
        {
            var tex = new Texture2D(Res, Res, TextureFormat.RGBA32, false) { name = name, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, Res, Res), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        // ------------------------------------------------------------------ the wall behind

        /// <summary>How much of a wall's own light and shade its blended copy keeps, and how bright it is.</summary>
        const float WallContrast = 0.6f, WallLight = 0.55f;
        /// <summary>The same for an area's own wall (World/AreaBlocks.cs), drawn dark for a cave already: only eased a little.</summary>
        const float AreaWallContrast = 0.85f, AreaWallLight = 0.95f;

        static readonly HashSet<string> blended = new HashSet<string>();

        /// <summary>
        /// The area's Terraria wall (Areas.WallFor) as a hole's back in Hollow Knight's way: the same sheet and frames, its
        /// contrast eased toward its own average and darker, so it sits back in Hallownest's dim rock as a cave wall does;
        /// the name it is kept under (TerrariaAssets.Put).
        /// </summary>
        public static string WallSheet(string sheet)
        {
            string name = sheet + "#hollow";
            if (blended.Contains(name)) return name;
            blended.Add(name);
            try
            {
                var p = Terraria.TerrariaAssets.Pixels(sheet, out int w, out int h);
                double r = 0, g = 0, b = 0;
                int n = 0;
                foreach (var c in p)
                {
                    if (c.a < 128) continue;
                    r += c.r; g += c.g; b += c.b; n++;
                }
                if (n > 0) { r /= n; g /= n; b /= n; }
                bool own = sheet.StartsWith("Wall_") && int.TryParse(sheet.Substring(5), out int id) && AreaBlocks.IsAreaWall(id);
                float contrast = own ? AreaWallContrast : WallContrast, light = own ? AreaWallLight : WallLight;
                byte Ch(byte v, double mean) => (byte)Mathf.Clamp((float)(mean + (v - mean) * contrast) * light, 0f, 255f);
                for (int i = 0; i < p.Length; i++) p[i] = new Color32(Ch(p[i].r, r), Ch(p[i].g, g), Ch(p[i].b, b), p[i].a);
                Terraria.TerrariaAssets.Put(name, Terraria.TerrariaAssets.Make(name, p, w, h));
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[挖洞] {sheet} 的融合墙没做成：{e.Message}");
                return sheet;
            }
            return name;
        }
    }
}
