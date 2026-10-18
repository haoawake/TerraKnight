using System.Collections.Generic;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.Effects
{
    /// <summary>
    /// Terraria's dust and gore: the puffs, smoke and clouds that movement, digging and accessories
    /// throw off. Positions and velocities are kept in Terraria's own units (pixels, y down, per
    /// 1/60 s tick) and advanced with Terraria's update rules for the types used here, so the
    /// effects keep their original timing; they are drawn as plain sprites just in front of the player.
    /// </summary>
    internal sealed class TerraParticles : MonoBehaviour
    {
        /// <summary>One Terraria pixel in world units (a 16 px tile is half a unit).</summary>
        public const float Px = 1f / 32f;
        const float Z = 0.003f;
        const int MaxDust = 400, MaxGore = 60;
        // Hallownest is dark; Terraria would light dust by the tile it is in.
        static readonly Color Light = new Color(0.86f, 0.86f, 0.9f);

        public struct Dust
        {
            public bool Active;
            public int Type;
            public Vector2 Position, Velocity;
            public float Scale, Rotation, FadeIn;
            public int Alpha;
            public bool NoGravity;
            public int Variant;
            /// <summary>Terraria's dust colour (newColor); clear means none (white).</summary>
            public Color Color;
            /// <summary>Drawn added to what's behind (glowing magic and sparkle dusts).</summary>
            public bool Additive;
        }

        struct Gore
        {
            public bool Active;
            public int Type;
            public Vector2 Position, Velocity;
            public float Scale, Rotation;
            public int Alpha;
            /// <summary>A plain gore (Gore.Update's default): it falls, turns with its speed and fades after its time.</summary>
            public bool Falls;
            public int TimeLeft;
        }

        public static TerraParticles Instance { get; private set; }

        readonly Dust[] dust = new Dust[MaxDust];
        readonly Gore[] gore = new Gore[MaxGore];
        readonly SpriteRenderer[] dustRenderers = new SpriteRenderer[MaxDust];
        readonly SpriteRenderer[] goreRenderers = new SpriteRenderer[MaxGore];
        readonly Dictionary<(int, int), Sprite> dustSprites = new Dictionary<(int, int), Sprite>();
        readonly Dictionary<int, Sprite> goreSprites = new Dictionary<int, Sprite>();
        float tickAccumulator;
        int nextDust;

        // Under sustained fire, retaining every old mote starved newly fired, dust-only
        // projectiles (Water Bolt, Lunar Flare). Prefer free slots, then recycle in a
        // round-robin instead of dropping all new effects until old ones expire.
        int AllocateDust()
        {
            for (int n = 0; n < MaxDust; n++)
            {
                int i = (nextDust + n) % MaxDust;
                if (dust[i].Active) continue;
                nextDust = (i + 1) % MaxDust;
                return i;
            }
            int recycled = nextDust;
            nextDust = (nextDust + 1) % MaxDust;
            return recycled;
        }

        public static TerraParticles Create(Transform parent)
        {
            var go = new GameObject("Terra Particles");
            go.transform.SetParent(parent, false);
            Instance = go.AddComponent<TerraParticles>();
            return Instance;
        }

        // --- Coordinates: Terraria pixels (y down) <-> world units (y up). ---

        public static Vector2 ToPx(Vector2 world) => new Vector2(world.x / Px, -world.y / Px);
        public static Vector2 ToWorld(Vector2 px) => new Vector2(px.x * Px, -px.y * Px);

        /// <summary>
        /// Terraria's Dust.NewDust: a dust of the given type somewhere in the rectangle (top-left in
        /// pixels), with Terraria's random spread on speed and scale. Returns its index, or -1.
        /// </summary>
        public int NewDust(Vector2 topLeft, int width, int height, int type, float speedX = 0f, float speedY = 0f, int alpha = 0, float scale = 1f)
        {
            int i = AllocateDust();
            width = Mathf.Max(width, 5);
            height = Mathf.Max(height, 5);
            dust[i] = new Dust
            {
                Active = true,
                Type = type,
                Alpha = alpha,
                Position = topLeft + new Vector2(Random.Range(0, width - 4) + 4f, Random.Range(0, height - 4) + 4f),
                Velocity = new Vector2(Random.Range(-20, 21) * 0.1f + speedX, Random.Range(-20, 21) * 0.1f + speedY),
                Scale = (1f + Random.Range(-20, 21) * 0.01f) * scale,
                Variant = Random.Range(0, 3),
                Additive = Glows(type),
            };
            if (type == 6 || type == 135 || type == 158 || type == 169)
            {
                // Torch dust: Dust.NewDust's own start for the torch family.
                dust[i].Velocity = new Vector2(dust[i].Velocity.x * 0.3f, Random.Range(-10, 6) * 0.1f);
            }
            return i;
        }

        public ref Dust this[int index] => ref dust[index];

        /// <summary>Dust.CloneDust: a copy of a dust in a free slot (-1 when none).</summary>
        public int Clone(int index)
        {
            int i = System.Array.FindIndex(dust, d => !d.Active);
            if (i < 0) return -1;
            dust[i] = dust[index];
            return i;
        }

        /// <summary>Dusts Terraria draws (almost) additively, unlit: fire, magic, sparkles.</summary>
        static bool Glows(int type) =>
            type == 6 || type == 15 || type == 27 || type == 43 || type == 57 || type == 58 || type == 107 || type == 135 || type == 158 || type == 169
            || type == 172 || type == 259 || type == 278 || (type >= 86 && type <= 92) || type == 45 || type == 67 || type == 267
            // The hardmode magic's flames and wisps: cursed flame, ichor, inferno, spectre, the Light Disc's sparks.
            || type == 75 || type == 170 || type == 174 || type == 175 || type == 206 || type == 306
            // The beams' lines (Shadowbeam, Heat Ray, Magnet Sphere), the typhoon's and the Sky Fracture's sparks.
            || type == 160 || type == 162 || type == 173 || type == 180 || type == 217
            // The Deadly Sphere's electric and golden sparks (drawn white-ish at alpha 25).
            || type == 226 || type == 228
            // The Flameburst towers' embers; the Frost Hydra's frost, Houndius's motes, the Lunar Portal's and the Rainbow Crystal's glitter.
            || type == 270 || type == 185 || type == 219 || type == 229 || type == 261
            // The Soul Drain's wisps; the rainbow's and the shimmer's sparks (GetAlpha: their own colour, alpha 0 / 25).
            || type == 235 || type == 66 || type == 310;

        /// <summary>Terraria's Gore.NewGore for the drifting smoke and cloud gores (11-13, 61-63).</summary>
        public void NewGore(Vector2 topLeft, Vector2 velocity, int type, float scale = 1f, float velocityScale = 1f, Vector2 velocityAdd = default, bool exact = false, int alpha = 0,
            bool falls = false)
        {
            int i = System.Array.FindIndex(gore, g => !g.Active);
            if (i < 0) return;
            var v = velocity;
            if (!exact)
            {
                v.y -= Random.Range(10, 31) * 0.1f;
                v.x += Random.Range(-20, 21) * 0.1f;
            }
            gore[i] = new Gore
            {
                Active = true,
                Type = type,
                Position = topLeft,
                Velocity = v * velocityScale + velocityAdd,
                Scale = scale,
                Alpha = alpha,
                Falls = falls,
                // Gore.goreTime 600 (and Gore.NewGore's timeLeft); no blocks to land on here, so a second before it fades.
                TimeLeft = 60,
            };
            // The star gores (Gore.NewGore): smaller, partly clear, and glowing.
            if (type == 16 || type == 17)
            {
                gore[i].Alpha = 100;
                gore[i].Scale = 0.7f;
            }
        }

        /// <summary>Moves everything by a world offset, when the world under the player is re-based.</summary>
        public void Shift(Vector2 worldDelta)
        {
            var d = new Vector2(worldDelta.x / Px, -worldDelta.y / Px);
            for (int i = 0; i < MaxDust; i++) if (dust[i].Active) dust[i].Position += d;
            for (int i = 0; i < MaxGore; i++) if (gore[i].Active) gore[i].Position += d;
        }

        public void Clear()
        {
            for (int i = 0; i < MaxDust; i++) dust[i].Active = false;
            for (int i = 0; i < MaxGore; i++) gore[i].Active = false;
        }

        void Update()
        {
            // Advance on Terraria's 60 Hz tick; deltaTime is 0 while the game is paused.
            tickAccumulator += Time.deltaTime * 60f;
            int ticks = Mathf.Min(Mathf.FloorToInt(tickAccumulator), 5);
            tickAccumulator -= Mathf.FloorToInt(tickAccumulator);
            var hero = HeroController.SilentInstance;
            hasHero = hero != null;
            if (hasHero) heroPx = ToPx(hero.GetComponent<BoxCollider2D>().bounds.center);
            for (int t = 0; t < ticks; t++)
            {
                for (int i = 0; i < MaxDust; i++) if (dust[i].Active) UpdateDust(ref dust[i]);
                for (int i = 0; i < MaxGore; i++) if (gore[i].Active) UpdateGore(ref gore[i]);
            }
            Draw();
        }

        /// <summary>Dust.UpdateDust, for the generic dusts (0, 1, 7...) and the smoke ones (16, 31).</summary>
        static Vector2 heroPx;
        static bool hasHero;

        static void UpdateDust(ref Dust d)
        {
            d.Position += d.Velocity;
            // The Soul Drain's wisp (235, Dust.cs:883-888, 2155-2172): drawn in to the player (fadeIn its index + 1),
            // shrinking as it nears; the rest as other dust.
            if (d.Type == 235)
            {
                d.Scale -= 0.01f;
                if (d.FadeIn > 0f && d.FadeIn < 100f && hasHero)
                {
                    d.Scale += 0.007f;
                    var to = d.Position - heroPx;
                    float len = to.magnitude, near = 100f - len;
                    if (near > 0f) d.Scale -= near * 0.0015f;
                    var pull = (len > 1e-4f ? to / len : Vector2.zero) * -((1f - d.Scale) * 20f);
                    d.Velocity = (d.Velocity * 4f + pull) / 5f;
                    if (d.Scale > d.FadeIn) d.FadeIn = 0f;
                    if (d.Scale < 0.1f) d.Active = false;
                    return;
                }
            }
            // Dust 43 (sparkles): spins, and shrinks away slowly, faster once small.
            if (d.Type == 43)
            {
                d.Rotation += 0.1f * d.Scale;
                d.Velocity *= 0.98f;
                d.Scale -= d.Scale < 0.5f ? 0.02f : 0.01f;
                if (d.Scale < 0.1f) d.Active = false;
                return;
            }
            // Abigail's ghost smoke (303): drifts, swells and fades out once past alpha 90.
            if (d.Type == 303)
            {
                d.Velocity *= 0.98f;
                if (d.NoGravity)
                {
                    d.Velocity *= 1.02f;
                    d.Scale += 0.03f;
                    if (d.Alpha < 90) d.Alpha = 90;
                    d.Alpha += 4;
                    if (d.Alpha > 255) d.Active = false;
                }
                else d.Velocity.y += 0.1f;
                return;
            }
            // The Flameburst embers (270): a slow wavy rise, swelling a little.
            if (d.Type == 270)
            {
                d.Velocity *= 1.0050251f;
                d.Scale += 0.01f;
                d.Velocity.y = (Mathf.Sin(d.Position.x * 0.0043982295f) * 2f - 3f) / 20f;
            }
            // The missiles' sparks (267) drag and swell a little.
            if (d.Type == 267)
            {
                d.Velocity *= 0.98f;
                d.Scale += 0.02f;
            }
            bool smoke = d.Type == 16 || d.Type == 31;
            if (smoke)
            {
                d.Velocity *= 0.98f;
                if (d.Type == 31 && d.NoGravity)
                {
                    d.Velocity *= 1.02f;
                    d.Scale += 0.02f;
                    d.Alpha += 4;
                    if (d.Alpha > 255)
                    {
                        d.Scale = 0.0001f;
                        d.Alpha = 255;
                    }
                }
            }
            else if (!d.NoGravity)
            {
                d.Velocity.y += 0.1f;
            }
            d.Velocity.x *= 0.99f;
            d.Rotation += d.Velocity.x * 0.5f;
            if (d.FadeIn > 0f && d.FadeIn < 100f)
            {
                d.Scale += 0.03f;
                if (d.Scale > d.FadeIn) d.FadeIn = 0f;
            }
            else
            {
                d.Scale -= 0.01f;
            }
            if (d.NoGravity)
            {
                d.Velocity *= 0.92f;
                if (d.FadeIn == 0f) d.Scale -= 0.04f;
            }
            if (d.Scale < 0.1f) d.Active = false;
        }

        /// <summary>Gore.Update for the non-sticky smoke gores: drift, shrink and fade.</summary>
        static void UpdateGore(ref Gore g)
        {
            if (g.Falls)
            {
                // Gore.Update: gravity 0.2 to 16 a tick, turning by its sideways speed; past its time it fades out.
                g.Velocity.y = Mathf.Min(16f, g.Velocity.y + 0.2f);
                g.Rotation += g.Velocity.x * 0.1f;
                g.Position += g.Velocity;
                if (--g.TimeLeft <= 0 && (g.Alpha += 5) >= 255) g.Active = false;
                return;
            }
            if (g.Type == 16 || g.Type == 17)
            {
                // The star gores shrink away without fading.
                g.Velocity *= 0.98f;
                g.Scale -= 0.01f;
                g.Position += g.Velocity;
                if (g.Scale < 0.1f) g.Active = false;
                return;
            }
            g.Velocity *= 0.98f;
            g.Scale -= 0.007f;
            if (g.Scale < 0.1f)
            {
                g.Scale = 0.1f;
                g.Alpha = 255;
            }
            g.Alpha += 2;
            g.Position += g.Velocity;
            if (g.Alpha >= 255) g.Active = false;
        }

        void Draw()
        {
            for (int i = 0; i < MaxDust; i++)
            {
                ref var d = ref dust[i];
                var sr = dustRenderers[i];
                if (!d.Active)
                {
                    if (sr != null && sr.enabled) sr.enabled = false;
                    continue;
                }
                if (sr == null) sr = dustRenderers[i] = MakeRenderer("Dust");
                sr.enabled = true;
                sr.sprite = DustSprite(d.Type, d.Variant);
                var faded = Faded(d.Alpha);
                if (d.Color.a > 0f) faded = new Color(faded.r * d.Color.r, faded.g * d.Color.g, faded.b * d.Color.b, faded.a * d.Color.a);
                var mat = d.Additive ? AdditiveMaterial() : null;
                if (mat != null)
                {
                    if (sr.sharedMaterial != mat) sr.sharedMaterial = mat;
                    faded = new Color(faded.r * faded.a, faded.g * faded.a, faded.b * faded.a, 1f);
                }
                else if (sr.sharedMaterial != defaultMaterial) sr.sharedMaterial = defaultMaterial;
                sr.color = faded;
                Place(sr.transform, ToWorld(d.Position), d.Rotation, d.Scale);
            }
            for (int i = 0; i < MaxGore; i++)
            {
                ref var g = ref gore[i];
                var sr = goreRenderers[i];
                if (!g.Active)
                {
                    if (sr != null && sr.enabled) sr.enabled = false;
                    continue;
                }
                if (sr == null) sr = goreRenderers[i] = MakeRenderer("Gore");
                sr.enabled = true;
                var sprite = GoreSprite(g.Type);
                sr.sprite = sprite;
                bool star = g.Type == 16 || g.Type == 17;
                var mat = star ? AdditiveMaterial() : null;
                if (mat != null)
                {
                    // Gore.GetAlpha: (255,255,255,0), added white; and its light, gold or blue.
                    if (sr.sharedMaterial != mat) sr.sharedMaterial = mat;
                    sr.color = Color.white;
                    var c = g.Type == 16 ? new Color(1f, 0.8f, 0.3f) : new Color(0.3f, 0.6f, 1f);
                    FxBatch.Instance?.Light(g.Position + new Vector2(sprite.rect.width, sprite.rect.height) * 0.5f * g.Scale, c * g.Scale, 2.5f);
                }
                else
                {
                    if (sr.sharedMaterial != defaultMaterial) sr.sharedMaterial = defaultMaterial;
                    sr.color = Faded(g.Alpha);
                }
                // Gore positions are the texture's top-left; it is drawn around its centre.
                var center = g.Position + new Vector2(sprite.rect.width, sprite.rect.height) * 0.5f;
                Place(sr.transform, ToWorld(center), g.Rotation, g.Scale);
            }
        }

        // GetAlpha: the light colour faded by alpha, which also lowers opacity.
        static Color Faded(int alpha)
        {
            float k = Mathf.Clamp01((255 - alpha) / 255f);
            return new Color(Light.r, Light.g, Light.b, k);
        }

        static void Place(Transform t, Vector2 world, float rotation, float scale)
        {
            t.position = new Vector3(world.x, world.y, Z);
            // Terraria rotates clockwise on a y-down screen.
            t.rotation = Quaternion.Euler(0f, 0f, -rotation * Mathf.Rad2Deg);
            t.localScale = new Vector3(scale, scale, 1f);
        }

        SpriteRenderer MakeRenderer(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            if (defaultMaterial == null) defaultMaterial = sr.sharedMaterial;
            // Dust over the game's scenery, as Terraria draws it over its tiles (with the weapons' effects).
            sr.sortingOrder = FxBatch.OnTopOrder + 1;
            return sr;
        }

        static Material defaultMaterial, additive;

        /// <summary>An additive sprite material (null where the game has no such shader).</summary>
        public static Material AdditiveMaterial()
        {
            if (additive != null) return additive;
            foreach (var name in new[] { "Legacy Shaders/Particles/Additive", "Particles/Additive", "Mobile/Particles/Additive" })
            {
                var sh = Shader.Find(name);
                if (sh == null) continue;
                additive = new Material(sh) { name = "Terra Additive" };
                if (additive.HasProperty("_TintColor")) additive.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
                break;
            }
            return additive;
        }

        // Dust.png holds every type in 10 px columns (8x8 frames, three variants down), wrapping
        // to a new block of rows every hundred types.
        Sprite DustSprite(int type, int variant)
        {
            if (dustSprites.TryGetValue((type, variant), out var s)) return s;
            int x = (type % 100) * 10, y = (type / 100) * 30 + variant * 10;
            s = TerrariaAssets.Sprite("Dust", x, y, 8, 8, 1f / Px, new Vector2(0.5f, 0.5f));
            dustSprites[(type, variant)] = s;
            return s;
        }

        Sprite GoreSprite(int type)
        {
            if (goreSprites.TryGetValue(type, out var s)) return s;
            s = TerrariaAssets.Sprite("Gore_" + type, 1f / Px);
            goreSprites[type] = s;
            return s;
        }
    }
}
