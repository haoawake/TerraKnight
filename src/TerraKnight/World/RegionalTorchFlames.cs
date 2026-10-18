using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// The flames of a region's placed regional torches (World.RegionalTorches), drawn as TileDrawing draws a torch's
    /// flame layer: seven copies over the torch, each added to what is behind it at the draw colour (100, 100, 100, 0)
    /// (premultiplied: 100/255 of the flame added), each moved by rand[-10, 10] × 0.15 px across and rand[-10, 0] × 0.35
    /// px up, the rolls seeded by the cell and the tick (Main.TileFrameSeed moves on every tick), so the flame flickers
    /// while the game runs and holds still while it is paused. One set per CellVisuals, under its root.
    /// </summary>
    internal sealed class RegionalTorchFlames
    {
        public const int Layers = 7;

        readonly Transform root;
        readonly Dictionary<int, Flame> flames = new Dictionary<int, Flame>();

        public RegionalTorchFlames(Transform root) { this.root = root; }

        /// <summary>Flames drawn now (dev).</summary>
        public int Count => flames.Count;

        /// <summary>The cell's torch has a style: its flame layer for the frame it stands in (fx, Tiles_4's column), over it at z.</summary>
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
            f.Set(x, y, RegionalTorches.Sprite(style, fx, true));
        }

        public void Hide(int i)
        {
            if (!flames.TryGetValue(i, out var f)) return;
            flames.Remove(i);
            if (f != null) Object.Destroy(f.gameObject);
        }

        static Material material;
        static bool additive;

        /// <summary>Particles/Additive where the game has it (its tint at half doubles back to one), Sprites/Default otherwise.</summary>
        static Material Material()
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

        /// <summary>One torch's seven flame copies, moved again each tick.</summary>
        internal sealed class Flame : MonoBehaviour
        {
            readonly SpriteRenderer[] layers = new SpriteRenderer[Layers];
            int cellX, cellY, lastTick = int.MinValue;

            /// <summary>Dev: the copies' offsets as last rolled (px, Terraria's: x right, y down).</summary>
            public readonly Vector2[] DevOffsets = new Vector2[Layers];

            public void Build()
            {
                var mat = Material();
                var tint = additive ? new Color(100f / 255f, 100f / 255f, 100f / 255f, 1f) : new Color(1f, 1f, 1f, 100f / 255f);
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
                Roll();
            }

            void Update()
            {
                int tick = Mathf.FloorToInt(Time.time * 60f);
                if (tick == lastTick) return;
                // Off screen it needn't move.
                if (lastTick != int.MinValue && !layers[0].isVisible) return;
                lastTick = tick;
                Roll();
            }

            void Roll()
            {
                ulong seed = ((ulong)(uint)Mathf.FloorToInt(Time.time * 60f) * 0x9E3779B97F4A7C15UL) ^ (((ulong)(uint)cellY << 32) | (uint)cellX);
                for (int k = 0; k < Layers; k++)
                {
                    float dx = RandomInt(ref seed, -10, 11) * 0.15f, dy = RandomInt(ref seed, -10, 1) * 0.35f;
                    DevOffsets[k] = new Vector2(dx, dy);
                    // Terraria's y grows down the screen.
                    layers[k].transform.localPosition = new Vector3(dx / CellVisuals.PixelsPerUnit, -dy / CellVisuals.PixelsPerUnit, 0f);
                }
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
