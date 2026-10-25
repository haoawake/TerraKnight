using System.Collections.Generic;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// The flames of a region's placed torches, the plain Terraria torch's (Flame_0, Main.tileFlame[4]) and the regional
    /// torches' (World.RegionalTorches), drawn as TileDrawing draws a torch's flame layer (DrawSingleTile_Flames,
    /// TileDrawing.cs:3478): seven copies over the torch, each added to what is behind it at the draw colour
    /// (100, 100, 100, 0) (premultiplied: 100/255 of the flame added), each moved by rand[-10, 10] × 0.15 px across and
    /// rand[-10, 0] × 0.35 px up, the rolls seeded by the cell and Main.TileFrameSeed, which moves on every fifth draw
    /// (Main.DoDraw, Main.cs:62714-62718): the flame flickers twelve times a second while the game runs and holds still
    /// while it is paused. The plain torch throws off its dust as it is drawn (DrawTiles_EmitParticles,
    /// TileDrawing.cs:7228). One set per CellVisuals, under its root.
    /// </summary>
    internal sealed class RegionalTorchFlames
    {
        public const int Layers = 7;

        /// <summary>Main.TileFrameSeed moves on every fifth draw.</summary>
        public const int SeedTicks = 5;

        /// <summary>TorchID.Dust[0]: the plain torch's dust.</summary>
        const int TorchDust = 6;

        readonly Transform root;
        readonly Dictionary<int, Flame> flames = new Dictionary<int, Flame>();

        public RegionalTorchFlames(Transform root) { this.root = root; }

        /// <summary>Flames drawn now (dev).</summary>
        public int Count => flames.Count;

        /// <summary>
        /// The cell's torch: its flame layer for the frame it stands in (fx, Tiles_4's column), over it at z: Flame_0's for
        /// the plain torch (style 0), its row of RegionalTorches.FlameSheet for a regional one.
        /// </summary>
        public void Show(int i, int x, int y, int style, int fx, float z)
        {
            if (!flames.TryGetValue(i, out var f) || f == null)
            {
                var go = new GameObject("Terra Torch Flame");
                go.transform.SetParent(root, false);
                f = go.AddComponent<Flame>();
                f.Build();
                flames[i] = f;
            }
            var c = CellGrid.CellCenter(x, y);
            f.transform.localPosition = new Vector3(c.x, c.y, z);
            // TileDrawing.cs:7228: a standing torch's dust 4 px in from the cell's left, one on a wall 6 or 2; this torch
            // stands 4 px higher in its cell than Terraria draws it (CellVisuals).
            f.Dust = style > 0 ? 0 : TorchDust;
            f.DustOffset = new Vector2(fx == 22 ? 6f : fx == 44 ? 2f : 4f, -4f);
            f.Set(x, y, style > 0 ? RegionalTorches.Sprite(style, fx, true) : PlainSprite(fx));
        }

        public void Hide(int i)
        {
            if (!flames.TryGetValue(i, out var f)) return;
            flames.Remove(i);
            if (f != null) Object.Destroy(f.gameObject);
        }

        static readonly Sprite[] plain = new Sprite[3];

        /// <summary>Flame_0's frame for the plain torch's column (laid out as Tiles_4), standing on the cell's floor as the torch does.</summary>
        static Sprite PlainSprite(int fx)
        {
            int k = Mathf.Clamp(fx / 22, 0, 2);
            if (plain[k] == null) plain[k] = TerrariaAssets.Sprite("Flame_0", k * 22, 0, 20, 20, CellVisuals.PixelsPerUnit, new Vector2(0.5f, 8f / 20f));
            return plain[k];
        }

        static Material material;
        static bool additive;

        /// <summary>Particles/Additive where the game has it (its tint at half doubles back to one), Sprites/Default otherwise.</summary>
        internal static Material AdditiveMaterial()
        {
            if (material != null) return material;
            foreach (var name in new[] { "Legacy Shaders/Particles/Additive", "Particles/Additive", "Mobile/Particles/Additive" })
            {
                var sh = Shader.Find(name);
                if (sh == null) continue;
                material = new Material(sh) { name = "Terra Torch Flame (" + name + ")" };
                if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
                additive = true;
                break;
            }
            if (material == null) material = new Material(Shader.Find("Sprites/Default")) { name = "Terra Torch Flame (alpha)" };
            Plugin.Log.LogInfo($"[火把] 火焰层材质 {material.name}");
            return material;
        }

        /// <summary>
        /// The tint for a copy drawn with <see cref="AdditiveMaterial"/> at Terraria's draw colour (r, g, b, 0), over 255: that
        /// much of the picture added; blended at its strongest channel where the game has no additive shader.
        /// </summary>
        internal static Color Added(float r, float g, float b)
        {
            AdditiveMaterial();
            if (additive) return new Color(r, g, b, 1f);
            float a = Mathf.Max(r, Mathf.Max(g, b));
            return a > 0f ? new Color(r / a, g / a, b / a, a) : Color.clear;
        }

        /// <summary>One torch's seven flame copies, moved again as the seed moves.</summary>
        internal sealed class Flame : MonoBehaviour
        {
            readonly SpriteRenderer[] layers = new SpriteRenderer[Layers];
            int cellX, cellY, lastTick = int.MinValue;

            /// <summary>The dust it throws off as it is drawn (0 for none), and where, from its cell's top left corner (Terraria px, y down).</summary>
            public int Dust;
            public Vector2 DustOffset;

            /// <summary>Dev: the copies' offsets as last rolled (px, Terraria's: x right, y down).</summary>
            public readonly Vector2[] DevOffsets = new Vector2[Layers];

            /// <summary>Dev: the dust thrown off by every flame.</summary>
            public static int DevDust;

            public void Build()
            {
                var mat = AdditiveMaterial();
                var tint = Added(100f / 255f, 100f / 255f, 100f / 255f);
                for (int k = 0; k < Layers; k++)
                {
                    var go = new GameObject("Flame " + k);
                    go.transform.SetParent(transform, false);
                    var sr = go.AddComponent<SpriteRenderer>();
                    sr.sharedMaterial = mat;
                    sr.color = tint;
                    layers[k] = sr;
                }
            }

            public void Set(int x, int y, Sprite sprite)
            {
                cellX = x;
                cellY = y;
                foreach (var sr in layers) if (sr.sprite != sprite) sr.sprite = sprite;
                lastTick = int.MinValue;
                Roll(Mathf.FloorToInt(Time.time * 60f) / SeedTicks);
            }

            void Update()
            {
                int tick = Mathf.FloorToInt(Time.time * 60f);
                if (tick == lastTick) return;
                int was = lastTick;
                lastTick = tick;
                // Off screen it needn't move.
                if (was != int.MinValue && !layers[0].isVisible) return;
                if (was == int.MinValue || tick / SeedTicks != was / SeedTicks) Roll(tick / SeedTicks);
                // DrawTiles_EmitParticles, 1 in 40 as the tiles are drawn again (Stations.RedrawTicks).
                if (Dust > 0 && was != int.MinValue && tick / 4 != was / 4 && Random.Range(0, 40) == 0) Emit();
            }

            void Roll(int step)
            {
                ulong seed = ((ulong)(uint)step * 0x9E3779B97F4A7C15UL) ^ (((ulong)(uint)cellY << 32) | (uint)cellX);
                for (int k = 0; k < Layers; k++)
                {
                    float dx = RandomInt(ref seed, -10, 11) * 0.15f, dy = RandomInt(ref seed, -10, 1) * 0.35f;
                    DevOffsets[k] = new Vector2(dx, dy);
                    // Terraria's y grows down the screen.
                    layers[k].transform.localPosition = new Vector3(dx / CellVisuals.PixelsPerUnit, -dy / CellVisuals.PixelsPerUnit, 0f);
                }
            }

            /// <summary>
            /// TileDrawing.cs:7228: the torch's dust in a 4 px square under its flame, two in three floating (noGravity),
            /// slowed to 0.3 and sent up 1.5 px a tick.
            /// </summary>
            void Emit()
            {
                const float half = CellGrid.CellSize / 2f;
                var corner = Effects.TerraParticles.ToPx((Vector2)transform.position + new Vector2(-half, half));
                int d = Stations.TileDust(corner + DustOffset, 4, 4, Dust, 100);
                if (d < 0) return;
                ref var dust = ref Effects.TerraParticles.Instance[d];
                if (Random.Range(0, 3) != 0) dust.NoGravity = true;
                dust.Velocity *= 0.3f;
                dust.Velocity.y -= 1.5f;
                DevDust++;
            }

            /// <summary>Utils.RandomInt(ref ulong seed, min, max): the 48-bit linear congruential step, an int in [min, max).</summary>
            static int RandomInt(ref ulong seed, int min, int max)
            {
                seed = (seed * 0x5DEECE66DUL + 11UL) & ((1UL << 48) - 1UL);
                return min + (int)((seed >> 17) % (ulong)(max - min));
            }
        }
    }
}
