using System.Collections.Generic;
using TerraKnight.Effects;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// One pylon as Terraria draws it (TileDrawing.DrawTeleportationPylons): the 3x4 tile base from
    /// Tiles_597, and the crystal from Extra_181 floating 24 px under the top, bobbing 4 px over five
    /// seconds, cycling its eight frames every eight ticks, drawn at 70% with six faint additive
    /// copies around it pulsing once a second, giving off its biome's light and the odd sparkle.
    /// </summary>
    internal sealed class PylonObject : MonoBehaviour
    {
        const float Px = TerraParticles.Px;
        // Pylon base: just behind the hero and the Terraria blocks' front; crystal in front of it.
        const float BaseZ = 0.0055f, CrystalZ = 0.0052f;
        const int CrystalW = 30, CrystalH = 46;

        /// <summary>The light each style gives (TileLightScanner, before Terraria's 0.75).</summary>
        static readonly Color[] Lights =
        {
            new Color(0.05f, 0.8f, 0.3f), new Color(0.7f, 0.8f, 0.05f), new Color(0.7f, 0.5f, 0.9f), new Color(0.6f, 0.6f, 0.8f),
            new Color(0.4f, 0.4f, 1.15f), new Color(0.85f, 0.45f, 0.1f), new Color(0.8f, 0.8f, 1f), new Color(0.5f, 0.8f, 1.2f),
            new Color(0.95f, 0.92f, 0.62f),
        };

        public Pylons.Pylon Pylon { get; private set; }
        /// <summary>World bounds of the whole pylon, crystal included.</summary>
        public Rect Bounds
        {
            get
            {
                var p = (Vector2)transform.position;
                return new Rect(p.x, p.y, Pylons.WidthCells * CellGrid.CellSize, Pylons.HeightCells * CellGrid.CellSize);
            }
        }

        SpriteRenderer crystal;
        readonly SpriteRenderer[] shine = new SpriteRenderer[6];
        readonly List<SpriteRenderer> baseParts = new List<SpriteRenderer>();
        /// <summary>How clear a pylon not lit yet stands (Pylons.Dormant).</summary>
        const float DormantAlpha = 0.38f;
        /// <summary>Its strength shown now, easing to its state's: half clear dormant, whole lit.</summary>
        float shown = 1f;
        SpriteRenderer light;
        /// <summary>Its style's light (TileLightScanner: the pylon tile's colour).</summary>
        Color lightColor;
        float appear = 1f;
        static readonly Dictionary<(int, int), Sprite> crystalFrames = new Dictionary<(int, int), Sprite>();
        static readonly Dictionary<(int, int, int), Sprite> baseTiles = new Dictionary<(int, int, int), Sprite>();

        public void Init(Pylons.Pylon p)
        {
            Pylon = p;
            gameObject.AddComponent<TerraOwned>();
            // The base: twelve 16 px tiles, 2 px of padding between them on the sheet, drawn 2 px low.
            for (int c = 0; c < Pylons.WidthCells; c++)
                for (int r = 0; r < Pylons.HeightCells; r++)
                {
                    var sr = new GameObject("Tile").AddComponent<SpriteRenderer>();
                    sr.transform.SetParent(transform, false);
                    sr.sprite = BaseTile(p.Style, c, r);
                    // Row 0 is the top; pivots are tile centres.
                    sr.transform.localPosition = new Vector3((c * 16 + 8) * Px, ((Pylons.HeightCells - 1 - r) * 16 + 8 - 2) * Px, BaseZ);
                    baseParts.Add(sr);
                }
            crystal = new GameObject("Crystal").AddComponent<SpriteRenderer>();
            crystal.transform.SetParent(transform, false);
            var additive = TerraParticles.AdditiveMaterial();
            for (int k = 0; k < shine.Length; k++)
            {
                shine[k] = new GameObject("Shine").AddComponent<SpriteRenderer>();
                shine[k].transform.SetParent(transform, false);
                if (additive != null) shine[k].sharedMaterial = additive;
            }
            var lc = Lights[Mathf.Clamp(p.Style, 0, Lights.Length - 1)];
            lightColor = new Color(lc.r * 0.75f, lc.g * 0.75f, lc.b * 0.75f);
            // Terraria's tile light, softened: Hallownest's painted dark has no light map to stop it at walls.
            light = Glow.Create(transform, new Color(lc.r * 0.75f, lc.g * 0.75f, lc.b * 0.75f, 0.3f), 2.4f);
            light.transform.localPosition = new Vector3(24 * Px, 40 * Px, Glow.Z);
            shown = p.Dormant ? DormantAlpha : 1f;
            Update();
        }

        /// <summary>Fades in, with a burst of sparkles: just unlocked.</summary>
        public void Appear()
        {
            // Lit where it stood half clear: from there up; new: from nothing.
            appear = shown < 1f ? shown : 0f;
            shown = 1f;
            Burst(transform.position + new Vector3(24 * Px, 32 * Px, 0f), 40, Pylon.Style);
        }

        static Sprite BaseTile(int style, int c, int r)
        {
            if (baseTiles.TryGetValue((style, c, r), out var s)) return s;
            var tex = TerrariaAssets.Texture("Tiles_597");
            int x = style * 54 + c * 18, yTop = r * 18;
            s = Sprite.Create(tex, new Rect(x, tex.height - yTop - 16, 16, 16), new Vector2(0.5f, 0.5f), 1f / Px, 0, SpriteMeshType.FullRect);
            baseTiles[(style, c, r)] = s;
            return s;
        }

        static Sprite CrystalFrame(int style, int row)
        {
            if (crystalFrames.TryGetValue((style, row), out var s)) return s;
            var tex = TerrariaAssets.Texture("Extra_181");
            int col = 3 + style;
            s = Sprite.Create(tex, new Rect(col * CrystalW, tex.height - (row + 1) * CrystalH, CrystalW, CrystalH), new Vector2(0.5f, 0.5f), 1f / Px, 0, SpriteMeshType.FullRect);
            crystalFrames[(style, row)] = s;
            return s;
        }

        void Update()
        {
            if (Pylon == null) return;
            if (appear < 1f) appear = Mathf.Min(1f, appear + Time.deltaTime / 0.6f);
            // Not lit yet: half clear all over, no light, no sparkles (its place taken all the same).
            bool dormant = Pylon.Dormant;
            float whole = dormant ? DormantAlpha : appear;
            foreach (var part in baseParts) if (part != null) part.color = new Color(1f, 1f, 1f, whole);
            float t = Time.time;
            // Main.tileFrameCounter[597]: 0..63 at a tick each, eight frames of eight ticks, offset per pylon.
            var cell = CellGrid.WorldToCell(transform.position);
            int counter = (Mathf.FloorToInt(t * 60f) + cell.x + cell.y) & 63;
            var frame = CrystalFrame(Pylon.Style, counter / 8);
            float bob = Mathf.Sin(t * 2f * Mathf.PI / 5f);
            var center = new Vector3(24 * Px, (40 + bob * 4f) * Px, CrystalZ);
            crystal.sprite = frame;
            crystal.transform.localPosition = center;
            crystal.color = new Color(0.86f, 0.86f, 0.9f, 0.7f * whole);
            float pulse = Mathf.Sin(t * 2f * Mathf.PI) * 0.2f + 0.8f;
            for (int k = 0; k < shine.Length; k++)
            {
                float a = k * Mathf.PI / 3f;
                var off = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * ((6f + bob * 2f) * Px);
                shine[k].sprite = frame;
                shine[k].transform.localPosition = center + off - Vector3.forward * 0.0001f;
                float v = dormant ? 0f : 0.1f * pulse * appear;
                shine[k].color = shine[k].sharedMaterial == crystal.sharedMaterial ? new Color(1f, 1f, 1f, v) : new Color(v, v, v, 1f);
            }
            // Terraria's lighting on: the pylon's colour lights the map round its crystal instead of the glow.
            if (LightMap.Active)
            {
                light.enabled = false;
                if (appear > 0.05f && !dormant) LightMap.Add(transform.TransformPoint(center), lightColor * appear);
            }
            else light.enabled = appear > 0.05f && !dormant;
            // TileDrawing: a 1 in 40 chance each frame of one sparkle drifting off the crystal.
            if (!dormant && Random.value < 1f / 40f) Burst(transform.TransformPoint(center), 1, Pylon.Style);
            ShowHighlight(counter / 8, center);
        }

        /// <summary>The smart interact highlight on it (Pylons.SmartWin): 0 none, 1 in reach (grey), 2 picked (gold).</summary>
        public int Highlight;

        SpriteRenderer crystalHighlight;
        readonly List<SpriteRenderer> baseOutlines = new List<SpriteRenderer>();
        static readonly Dictionary<int, Sprite> highlightFrames = new Dictionary<int, Sprite>();
        static readonly Dictionary<(int, int, int), Sprite> baseOutlineTiles = new Dictionary<(int, int, int), Sprite>();
        static bool outlineMissing;

        /// <summary>
        /// DrawTeleportationPylons' selection glow: the crystal's highlight frame (Extra_181, column 2, the same row) over it;
        /// the base's tiles their TileOutlines/Tiles_597 mask, as any highlighted tile.
        /// </summary>
        void ShowHighlight(int row, Vector3 center)
        {
            bool on = Highlight != 0;
            if (crystalHighlight == null && on)
            {
                crystalHighlight = new GameObject("Crystal Highlight").AddComponent<SpriteRenderer>();
                crystalHighlight.transform.SetParent(transform, false);
                for (int i = 0; i < baseParts.Count; i++)
                {
                    var o = new GameObject("Tile Outline").AddComponent<SpriteRenderer>();
                    o.transform.SetParent(baseParts[i].transform, false);
                    o.transform.localPosition = new Vector3(0f, 0f, -0.0001f);
                    int c = i / Pylons.HeightCells, r = i % Pylons.HeightCells;
                    o.sprite = BaseOutline(Pylon.Style, c, r);
                    baseOutlines.Add(o);
                }
            }
            if (crystalHighlight == null) return;
            crystalHighlight.enabled = on;
            foreach (var o in baseOutlines) o.enabled = on && o.sprite != null;
            if (!on) return;
            var color = SmartInteract.OutlineColor(Highlight == 2);
            if (!highlightFrames.TryGetValue(row, out var s))
            {
                var tex = TerrariaAssets.Texture("Extra_181");
                s = Sprite.Create(tex, new Rect(2 * CrystalW, tex.height - (row + 1) * CrystalH, CrystalW, CrystalH), new Vector2(0.5f, 0.5f), 1f / Px, 0, SpriteMeshType.FullRect);
                highlightFrames[row] = s;
            }
            crystalHighlight.sprite = s;
            crystalHighlight.transform.localPosition = center - Vector3.forward * 0.0002f;
            crystalHighlight.color = color;
            foreach (var o in baseOutlines) o.color = color;
        }

        static Sprite BaseOutline(int style, int c, int r)
        {
            if (outlineMissing) return null;
            if (baseOutlineTiles.TryGetValue((style, c, r), out var s)) return s;
            Texture2D tex;
            try { tex = TerrariaAssets.Texture("Misc/TileOutlines/Tiles_597"); }
            catch (System.Exception) { tex = null; }
            if (tex == null) { outlineMissing = true; return null; }
            int x = style * 54 + c * 18, yTop = r * 18;
            if (x + 16 > tex.width || yTop + 16 > tex.height) return null;
            s = Sprite.Create(tex, new Rect(x, tex.height - yTop - 16, 16, 16), new Vector2(0.5f, 0.5f), 1f / Px, 0, SpriteMeshType.FullRect);
            baseOutlineTiles[(style, c, r)] = s;
            return s;
        }

        /// <summary>TeleportPylonsSystem.SpawnInWorldDust: dust 43 in the pylon's colour, slowed, drifting up.</summary>
        internal static void Burst(Vector3 world, int count, int style)
        {
            var p = TerraParticles.Instance;
            if (p == null) return;
            var color = Lights[Mathf.Clamp(style, 0, Lights.Length - 1)];
            var corner = TerraParticles.ToPx(world) - new Vector2(15f, 23f);
            for (int i = 0; i < count; i++)
            {
                int d = p.NewDust(corner, 30, 46, 43, 0f, 0f, 254, 0.5f);
                if (d < 0) continue;
                ref var dust = ref p[d];
                dust.Velocity *= 0.1f;
                dust.Velocity.y -= 0.2f;
                dust.NoGravity = true;
                dust.Alpha = 0;
                dust.Color = new Color(Mathf.Min(1f, color.r), Mathf.Min(1f, color.g), Mathf.Min(1f, color.b), 1f);
                dust.Additive = true;
                dust.Scale = 0.9f + Random.value * 0.4f;
            }
        }

        /// <summary>Player.TeleportEffect for pylons: a burst of the pylon's sparkle round the player, and the magic mirror's sound.</summary>
        public static void TeleportEffect(HeroController hero, int style = 8)
        {
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            var p = TerraParticles.Instance;
            if (p != null)
            {
                var color = Lights[Mathf.Clamp(style, 0, Lights.Length - 1)];
                // The hitbox inflated by 15 px each way.
                var corner = TerraParticles.ToPx(new Vector2(b.min.x, b.max.y)) - new Vector2(15f, 15f);
                int w = Mathf.RoundToInt(b.size.x / Px) + 30, h = Mathf.RoundToInt(b.size.y / Px) + 30;
                for (int i = 0; i < 100; i++)
                {
                    int d = p.NewDust(corner, w, h, 43, 0f, 0f, 254, 0.5f);
                    if (d < 0) continue;
                    ref var dust = ref p[d];
                    dust.Velocity *= 0.1f;
                    dust.Velocity.y -= 0.2f;
                    dust.NoGravity = true;
                    dust.Alpha = 0;
                    dust.Color = new Color(Mathf.Min(1f, color.r), Mathf.Min(1f, color.g), Mathf.Min(1f, color.b), 1f);
                    dust.Additive = true;
                    dust.Scale = 0.8f + Random.value * 0.5f;
                }
            }
            TerraMod.Instance?.PlaySound("Item_6");
        }
    }
}
