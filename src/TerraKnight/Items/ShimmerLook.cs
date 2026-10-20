using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    internal static partial class Shimmer
    {
        /// <summary>What a piece of the spring's water is, for its colour.</summary>
        enum Part { Body, Surface, Light, Fog, Falls }

        static World.TerrainRegion lookOf;
        static readonly List<(SpriteRenderer R, float A, Part P)> sprites = new List<(SpriteRenderer, float, Part)>();
        static readonly List<(tk2dBaseSprite S, float A, Part P)> tk2ds = new List<(tk2dBaseSprite, float, Part)>();
        static float sparkleAt;
        public static int DevTinted, DevStars;

        /// <summary>The water's own pieces (the surface, the ripple, caustics, water glows, fog, the falls into it); the rocks round it aren't.</summary>
        static Part? PartOf(string name) =>
            name.StartsWith("hot_spring_layers_0032") ? Part.Body
            : name.StartsWith("acid_top_version") || name == "Ripple" ? Part.Surface
            : name.StartsWith("caustic_small") || name.StartsWith("hot_spring_layers_0033_water_glow") ? Part.Light
            : name.StartsWith("water_fog") ? Part.Fog
            : name.StartsWith("waterfall") ? Part.Falls
            : (Part?)null;

        /// <summary>The spring's particles: its glows and steam.</summary>
        static bool IsMist(string name) => name.StartsWith("Spa Glows") || name.StartsWith("Spa Steam") || name.StartsWith("steam");

        /// <summary>
        /// The shimmer liquid's own colours (LiquidRenderer.GetShimmerBaseColor: lavender to pale blue, in waves) for the water,
        /// paler for its surface; the light in it and the mist over it in the shimmer's sparkle colours.
        /// </summary>
        static readonly Color[] BodyColors = { new Color(165 / 255f, 130 / 255f, 238 / 255f), new Color(205 / 255f, 205 / 255f, 1f), new Color(180 / 255f, 160 / 255f, 245 / 255f) };
        static readonly Color[] SurfaceColors = { new Color(205 / 255f, 205 / 255f, 1f), new Color(230 / 255f, 215 / 255f, 1f), new Color(200 / 255f, 225 / 255f, 1f) };
        static readonly Color[] LightColors = { new Color(190 / 255f, 245 / 255f, 1f), new Color(1f, 150 / 255f, 1f), new Color(190 / 255f, 175 / 255f, 1f), new Color(1f, 1f, 210 / 255f) };
        static readonly Color[] FogColors = { new Color(0.80f, 0.70f, 1f), new Color(0.70f, 0.78f, 1f), new Color(0.88f, 0.72f, 1f) };
        /// <summary>LiquidRenderer.DrawShimmer's opacity in front of things (0.75).</summary>
        const float BodyAlpha = 0.75f;

        static Color[] PaletteOf(Part p) => p == Part.Body ? BodyColors : p == Part.Surface ? SurfaceColors : p == Part.Light ? LightColors : FogColors;

        /// <summary>
        /// The spring as the shimmer each frame while its room is loaded: its water in the shimmer's colours (Part), the room
        /// behind it the Aether's void with its stars (Background), shimmer sparkles (dust 309) rising off its surface.
        /// </summary>
        public static void Tick()
        {
            var region = World.TerrainSystem.Find(Scene);
            if (region == null)
            {
                lookOf = null;
                return;
            }
            if (region != lookOf)
            {
                lookOf = region;
                Collect(region);
            }
            float t = Time.time * 0.12f;
            for (int i = 0; i < sprites.Count; i++)
            {
                var (r, a, p) = sprites[i];
                if (r == null) continue;
                var c = Drift(PaletteOf(p), t + i * 0.37f);
                c.a = p == Part.Body ? BodyAlpha : a;
                r.color = c;
            }
            for (int i = 0; i < tk2ds.Count; i++)
            {
                var (s, a, p) = tk2ds[i];
                if (s == null) continue;
                var c = Drift(PaletteOf(p), t + (sprites.Count + i) * 0.37f);
                c.a = a;
                s.color = c;
            }
            TickStars();
            var box = Pool;
            if (box != null && Time.time >= sparkleAt)
            {
                sparkleAt = Time.time + 0.12f;
                var b = box.Value;
                var px = Effects.TerraParticles.ToPx(new Vector2(Random.Range(b.xMin, b.xMax), b.yMax));
                Sparkle(px - new Vector2(4f, 4f), 8, 8, 1f, 0.3f);
            }
        }

        /// <summary>Through the colours in turn, a unit of t each, eased.</summary>
        static Color Drift(Color[] palette, float t)
        {
            int k = Mathf.FloorToInt(t);
            float f = t - k;
            k = ((k % palette.Length) + palette.Length) % palette.Length;
            return Color.Lerp(palette[k], palette[(k + 1) % palette.Length], f * f * (3f - 2f * f));
        }

        static void Collect(World.TerrainRegion region)
        {
            sprites.Clear();
            tk2ds.Clear();
            ClearStars();
            var box = Pool;
            if (box == null) return;
            var near = new Rect(box.Value.xMin - 10f, box.Value.yMin - 8f, box.Value.width + 20f, box.Value.height + 16f);
            var gradient = new Gradient();
            var keys = new GradientColorKey[Colors.Length];
            for (int i = 0; i < Colors.Length; i++) keys[i] = new GradientColorKey(Colors[i], i / (float)(Colors.Length - 1));
            gradient.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            foreach (var root in region.Root.gameObject.scene.GetRootGameObjects())
            {
                foreach (var r in root.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    var p = PartOf(r.name);
                    if (p != null && near.Contains(r.transform.position)) sprites.Add((r, r.color.a, p.Value));
                }
                foreach (var s in root.GetComponentsInChildren<tk2dBaseSprite>(true))
                {
                    var p = PartOf(s.name);
                    if (p != null && near.Contains(s.transform.position)) tk2ds.Add((s, s.color.a, p.Value));
                }
                foreach (var ps in root.GetComponentsInChildren<ParticleSystem>(true))
                {
                    if (!IsMist(ps.name) || !near.Contains(ps.transform.position)) continue;
                    var main = ps.main;
                    main.startColor = new ParticleSystem.MinMaxGradient(gradient) { mode = ParticleSystemGradientMode.RandomColor };
                }
            }
            Background(region);
            DevTinted = sprites.Count + tk2ds.Count;
            Plugin.Log.LogInfo($"[微光] {Scene} 的温泉：水 {DevTinted} 层换上微光的颜色，背景换成以太的星空（{DevStars} 颗星）");
        }

        // ------------------------------------------------------------------ the Aether's sky behind it

        sealed class Star
        {
            public SpriteRenderer R;
            public int Index;
            public float Twinkle, TwinkleSpeed, Rotation, RotationSpeed, Scale;
        }

        static readonly List<Star> stars = new List<Star>();
        static GameObject sky;
        static Material starMaterial;
        static readonly Sprite[] starSprites = new Sprite[4];
        static float starTicks;

        /// <summary>The sky's depth behind the room's ground (z 0): behind the terrain and the near scenery, before the far background.</summary>
        const float SkyZ = 1.5f;
        /// <summary>Terraria's 200-400 stars to a 1920 × 1200 screen (Star.SpawnStars), here per screen-sized stretch of the room.</summary>
        const int StarsPerScreen = 300, MaxStars = 900;
        const float ScreenW = 29.2f, ScreenH = 16.6f;

        /// <summary>
        /// The room as Terraria's Aether shows it (Main.DrawBackground under shimmerAlpha 1): its own background gone to black
        /// behind the ground, and over the black the night's stars (DrawStarsInBackground, artificial): Star_0-3 at random
        /// turns, 0.42-0.78 of their size (one in forty twice that), each twinkling between 0.6 and 1 at its own pace and
        /// slowly turning (Star.Update), grey-white with one channel dimmed by its twinkle in turn (DrawStar's shimmer tints:
        /// magenta, gold, cyan), added to the dark.
        /// </summary>
        static void Background(World.TerrainRegion region)
        {
            var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
            float camZ = cam != null ? Mathf.Abs(cam.transform.position.z) : 38f;
            float grow = (camZ + SkyZ) / Mathf.Max(1f, camZ);
            Vector2 lo = (Vector2)region.CellOffset * World.CellGrid.CellSize, hi = lo + new Vector2(region.Grid.Width, region.Grid.Height) * World.CellGrid.CellSize;
            var centre = (lo + hi) / 2f;
            var size = (hi - lo + new Vector2(24f, 16f)) * grow;
            sky = new GameObject("TerraKnight Aether Sky");
            sky.transform.SetParent(region.Root, true);
            sky.transform.position = new Vector3(centre.x, centre.y, SkyZ + 0.05f);
            var black = sky.AddComponent<SpriteRenderer>();
            black.sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
            black.color = Color.black;
            sky.transform.localScale = new Vector3(size.x, size.y, 1f);
            if (starMaterial == null)
            {
                var shader = Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Particles/Additive") ?? Shader.Find("Sprites/Default");
                starMaterial = new Material(shader) { name = "TerraKnight Aether Stars" };
            }
            int n = Mathf.Min(MaxStars, Mathf.RoundToInt(StarsPerScreen * size.x * size.y / (ScreenW * grow * ScreenH * grow)));
            // A Terraria screen pixel at the sky's depth.
            float px = ScreenW / 1920f * grow;
            for (int i = 0; i < n; i++)
            {
                int type = Random.Range(0, 4);
                var go = new GameObject("Star");
                go.transform.SetParent(region.Root, true);
                go.transform.position = new Vector3(centre.x + Random.Range(-size.x, size.x) / 2f, centre.y + Random.Range(-size.y, size.y) / 2f, SkyZ);
                var r = go.AddComponent<SpriteRenderer>();
                r.sprite = StarSprite(type, px);
                r.sharedMaterial = starMaterial;
                var s = new Star
                {
                    R = r, Index = i, Rotation = Random.Range(0, 628) * 0.01f, Scale = Random.Range(70, 130) * 0.006f,
                    Twinkle = Random.Range(60, 101) * 0.01f, TwinkleSpeed = Random.Range(30, 110) * 0.0001f * (Random.Range(0, 2) == 0 ? -1f : 1f),
                    RotationSpeed = Random.Range(5, 50) * 0.0001f * (Random.Range(0, 2) == 0 ? -1f : 1f),
                };
                if (Random.Range(0, 40) == 0)
                {
                    s.Scale *= 2f;
                    s.TwinkleSpeed /= 2f;
                    s.RotationSpeed /= 2f;
                }
                stars.Add(s);
                Show(s);
            }
            DevStars = stars.Count;
        }

        /// <summary>Star.Update at Terraria's 60 a second, and each drawn as DrawStar has it.</summary>
        static void TickStars()
        {
            if (sky == null) return;
            starTicks += Mathf.Min(Time.deltaTime, 0.1f) * 60f;
            int steps = (int)starTicks;
            starTicks -= steps;
            if (steps == 0) return;
            foreach (var s in stars)
            {
                if (s.R == null) continue;
                for (int k = 0; k < steps; k++)
                {
                    s.Twinkle += s.TwinkleSpeed;
                    if (s.Twinkle > 1f) { s.Twinkle = 1f; s.TwinkleSpeed = -s.TwinkleSpeed; }
                    else if (s.Twinkle < 0.6f) { s.Twinkle = 0.6f; s.TwinkleSpeed = -s.TwinkleSpeed; }
                    s.Rotation += s.RotationSpeed;
                }
                Show(s);
            }
        }

        /// <summary>DrawStar against the void (ColorOfTheSkies × (1 − shimmerAlpha): black): its grey, one channel by its twinkle².</summary>
        static void Show(Star s)
        {
            float g = Mathf.Min(1f, 155f * s.Twinkle * 1.4f / 255f);
            float d = s.Twinkle * s.Twinkle;
            var c = new Color(g, g, g, 1f);
            if (s.Index % 3 == 0) c.g *= d;
            else if (s.Index % 3 == 1) c.b *= d;
            else c.r *= d;
            s.R.color = c;
            var tr = s.R.transform;
            tr.rotation = Quaternion.Euler(0f, 0f, -s.Rotation * Mathf.Rad2Deg);
            float sc = s.Scale * s.Twinkle;
            tr.localScale = new Vector3(sc, sc, 1f);
        }

        static Sprite StarSprite(int type, float worldPerPixel)
        {
            if (starSprites[type] != null) return starSprites[type];
            var tex = Terraria.TerrariaAssets.Texture("Star_" + type);
            if (tex == null) return null;
            return starSprites[type] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 1f / worldPerPixel);
        }

        static void ClearStars()
        {
            foreach (var s in stars) if (s.R != null) Object.Destroy(s.R.gameObject);
            stars.Clear();
            if (sky != null) Object.Destroy(sky);
            sky = null;
            DevStars = 0;
        }
    }
}
