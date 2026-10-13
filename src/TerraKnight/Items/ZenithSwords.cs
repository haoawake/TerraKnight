using System.Collections.Generic;
using TerraKnight.Effects;
using TerraKnight.Terraria;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The Zenith's swords (projectile 933, AI_182_FinalFractal), ported from Terraria. Each use
    /// throws three: the Zenith itself, then two of the swords it is forged from, homing on the
    /// enemy nearest the cursor. A sword swoops along an ellipse from the player out past its
    /// target and back behind him, carried along as he moves, leaving a ribbon in its own colour
    /// over the area its blade swept, with three afterimages, a glow and a sparkle at the tip as it
    /// reaches the target. Worked in Terraria's space (pixels, y down) on its 60 Hz tick, two
    /// steps a tick, as the original does.
    /// </summary>
    internal sealed class ZenithSwords : MonoBehaviour
    {
        struct Profile
        {
            public int Item;
            public float Blade;
            public Color Trail;
            public Profile(int item, float blade, int r, int g, int b)
            {
                Item = item;
                Blade = blade;
                Trail = new Color(r / 255f, g / 255f, b / 255f);
            }
        }

        // FinalFractalHelper's profiles: item, blade length (px), trail colour. The Zenith is always first.
        static readonly Profile[] profiles =
        {
            new Profile(4956, 86, 178, 255, 180),
            new Profile(65, 60, 236, 62, 192), new Profile(1123, 75, 255, 231, 69), new Profile(46, 65, 122, 66, 191),
            new Profile(121, 110, 254, 158, 35), new Profile(190, 105, 107, 203, 0), new Profile(368, 75, 236, 200, 19),
            new Profile(674, 75, 236, 200, 19), new Profile(273, 70, 179, 54, 201), new Profile(675, 70, 179, 54, 201),
            new Profile(2880, 70, 84, 234, 245), new Profile(989, 48, 91, 158, 232), new Profile(1826, 76, 252, 95, 4),
            new Profile(3063, 76, 254, 194, 250), new Profile(3065, 70, 237, 63, 133), new Profile(757, 70, 80, 222, 122),
            new Profile(155, 70, 56, 78, 210), new Profile(795, 72, 237, 28, 36), new Profile(3018, 75, 143, 215, 29),
            new Profile(4144, 45, 178, 255, 180), new Profile(3507, 45, 235, 166, 135),
        };

        const int TrailLength = 60;
        const float Px = TerraParticles.Px;
        // Projectiles draw behind the player.
        const float Z = 0.0045f;
        const int HitCooldownTicks = 7;
        const int EnemyLayer = 11;

        sealed class Sword
        {
            public GameObject Root;
            public SpriteRenderer Main, Glow;
            public readonly SpriteRenderer[] Echoes = new SpriteRenderer[3];
            public readonly SpriteRenderer[] Sparkle = new SpriteRenderer[2];
            public Mesh Ribbon;
            public bool Active;
            public Vector2 D;
            public float Ai0, T, Increment;
            public Profile Profile;
            public ItemDef Item;
            public readonly Vector2[] TrailPos = new Vector2[TrailLength];
            public readonly float[] TrailAngle = new float[TrailLength];
            public int TrailCount;
            public Vector2 Rel;
            public float BladeAngle, Opacity;
            public int Spin;
            public readonly Dictionary<int, int> Cooldowns = new Dictionary<int, int>();
        }

        public static ZenithSwords Instance { get; private set; }

        /// <summary>Dev: swords in flight, and the farthest any is from the player (world units).</summary>
        public int ActiveCount { get { int n = 0; foreach (var s in swords) if (s.Active) n++; return n; } }
        public float FarthestReach
        {
            get
            {
                float best = 0f;
                foreach (var s in swords) if (s.Active) best = Mathf.Max(best, s.Rel.magnitude * Px);
                return best;
            }
        }

        readonly List<Sword> swords = new List<Sword>();
        readonly List<int> expired = new List<int>();
        readonly Collider2D[] overlaps = new Collider2D[16];
        Material additive;
        AudioSource voice;
        float tickAccumulator;
        Vector2 playerPx;

        public static ZenithSwords Create(Transform parent)
        {
            var go = new GameObject("Terra Zenith");
            go.transform.SetParent(parent, false);
            Instance = go.AddComponent<ZenithSwords>();
            return Instance;
        }

        void Awake()
        {
            var shader = Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Particles/Additive")
                         ?? Shader.Find("Mobile/Particles/Additive") ?? Shader.Find("Sprites/Default");
            additive = new Material(shader) { mainTexture = Texture2D.whiteTexture };
            voice = gameObject.AddComponent<AudioSource>();
            voice.playOnAwake = false;
            voice.spatialBlend = 0f;
        }

        // --- Throwing. ---

        /// <summary>Player.ItemCheck_Shoot for the Zenith: shot n (0, 1, 2) of a use, from the player towards the mouse.</summary>
        public void Throw(int n, ItemDef item, Vector2 playerWorld, Vector2 mouseWorld)
        {
            Vector2 p = TerraParticles.ToPx(playerWorld);
            // LimitPointToPlayerReachableArea: the cursor pulled into a 1920x1200 box round the player.
            Vector2 v = TerraParticles.ToPx(mouseWorld) - p;
            float k = 1f;
            if (Mathf.Abs(v.x) > 960f) k = Mathf.Min(k, 960f / Mathf.Abs(v.x));
            if (Mathf.Abs(v.y) > 600f) k = Mathf.Min(k, 600f / Mathf.Abs(v.y));
            v *= k;
            if (n >= 1)
            {
                bool found = NearestEnemy(p + v, 400f, out var enemy);
                if (found) v = enemy - p;
                if (n == 2 || !found)
                {
                    // Utils.NextVector2Circular: a random point in a 150 px circle, denser at the middle.
                    float a = Random.value * Mathf.PI * 2f, r = Random.value * 150f;
                    v += new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                }
            }
            var s = Get();
            s.Active = true;
            s.Root.SetActive(true);
            s.D = v / 2f;
            s.Ai0 = Random.Range(-100, 101);
            // The True Copper Shortsword throws its own sword every time, with the copper shortsword's trail (Pl:49194-49197, M:30660-30664).
            bool trueCopper = item != null && item.Type == ItemType.TrueCopperShortsword;
            s.Profile = trueCopper ? profiles[profiles.Length - 1] : n == 0 ? profiles[0] : profiles[Random.Range(1, profiles.Length)];
            s.Item = item;
            s.T = 0f;
            s.TrailCount = 0;
            s.Cooldowns.Clear();
            s.Main.sprite = SwordSprite(trueCopper ? 5669 : s.Profile.Item);
            s.Glow.sprite = s.Main.sprite;
            foreach (var e in s.Echoes) e.sprite = s.Main.sprite;
            // Each sword plays the swish as it leaves, cutting off the last one.
            var clip = TerrariaAssets.Sound("Item_169");
            if (voice.outputAudioMixerGroup == null && TerraMod.Instance != null) voice.outputAudioMixerGroup = TerraMod.Instance.SoundGroup;
            voice.Stop();
            voice.clip = clip;
            voice.pitch = Mathf.Pow(2f, -0.8f + Random.Range(-0.06f, 0.06f));
            voice.Play();
        }

        /// <summary>The living enemy nearest a point (Terraria pixels) within a radius, in pixels.</summary>
        static bool NearestEnemy(Vector2 px, float radius, out Vector2 center)
        {
            center = default;
            float best = float.MaxValue;
            bool found = false;
            foreach (var col in Physics2D.OverlapCircleAll(TerraParticles.ToWorld(px), radius * Px, 1 << EnemyLayer))
            {
                var hm = col.GetComponentInParent<HealthManager>();
                if (hm == null || hm.isDead || !hm.gameObject.activeInHierarchy) continue;
                var c = TerraParticles.ToPx(col.bounds.center);
                float d = Vector2.Distance(c, px);
                if (d < best)
                {
                    best = d;
                    center = c;
                    found = true;
                }
            }
            return found;
        }

        Sword Get()
        {
            foreach (var s in swords) if (!s.Active) return s;
            var sword = new Sword { Root = new GameObject("Zenith Sword") };
            sword.Root.transform.SetParent(transform, false);
            sword.Main = Renderer(sword.Root, "Blade", null);
            sword.Glow = Renderer(sword.Root, "Glow", additive);
            for (int i = 0; i < sword.Echoes.Length; i++) sword.Echoes[i] = Renderer(sword.Root, "Afterimage " + i, null);
            for (int i = 0; i < sword.Sparkle.Length; i++)
            {
                sword.Sparkle[i] = Renderer(sword.Root, "Sparkle " + i, additive);
                sword.Sparkle[i].sprite = TerrariaAssets.Sprite("Extra_98", CellVisuals.PixelsPerUnit);
            }
            var ribbon = new GameObject("Ribbon");
            ribbon.transform.SetParent(sword.Root.transform, false);
            sword.Ribbon = new Mesh();
            sword.Ribbon.MarkDynamic();
            ribbon.AddComponent<MeshFilter>().sharedMesh = sword.Ribbon;
            ribbon.AddComponent<MeshRenderer>().sharedMaterial = additive;
            swords.Add(sword);
            return sword;
        }

        static SpriteRenderer Renderer(GameObject parent, string name, Material material)
        {
            var sr = new GameObject(name).AddComponent<SpriteRenderer>();
            sr.transform.SetParent(parent.transform, false);
            if (material != null) sr.sharedMaterial = material;
            return sr;
        }

        static readonly Dictionary<int, Sprite> sprites = new Dictionary<int, Sprite>();

        static Sprite SwordSprite(int item)
        {
            if (sprites.TryGetValue(item, out var s)) return s;
            s = TerrariaAssets.Sprite("Item_" + item, CellVisuals.PixelsPerUnit);
            sprites[item] = s;
            return s;
        }

        // --- Flying, on Terraria's tick. ---

        void Update()
        {
            var hero = HeroController.SilentInstance;
            if (hero == null)
            {
                foreach (var s in swords) Stop(s);
                return;
            }
            playerPx = TerraParticles.ToPx(hero.GetComponent<BoxCollider2D>().bounds.center);
            tickAccumulator += Time.deltaTime * 60f;
            int ticks = Mathf.Min(Mathf.FloorToInt(tickAccumulator), 4);
            tickAccumulator -= Mathf.FloorToInt(tickAccumulator);
            for (int t = 0; t < ticks; t++)
                foreach (var s in swords)
                {
                    if (!s.Active) continue;
                    TickCooldowns(s);
                    // extraUpdates = 1: two AI steps a tick.
                    for (int step = 0; step < 2 && s.Active; step++)
                    {
                        Step(s);
                        if (s.Active) HitTest(s);
                    }
                }
            foreach (var s in swords) if (s.Active) Draw(s);
        }

        static void TickCooldowns(Sword s)
        {
            if (s.Cooldowns.Count == 0) return;
            var keys = new List<int>(s.Cooldowns.Keys);
            foreach (var k in keys)
                if (--s.Cooldowns[k] <= 0) s.Cooldowns.Remove(k);
        }

        void Step(Sword s)
        {
            float length = s.D.magnitude;
            float near = Mathf.Clamp01(1f - 2f * length / 900f);
            s.Increment = Mathf.Lerp(0.7f, 2f, near);
            s.T += s.Increment;
            if (s.T >= 120f)
            {
                Stop(s);
                return;
            }
            float p = Mathf.Clamp01(s.T / 60f);
            int side = s.D.x > 0f ? 1 : -1;
            s.Spin = s.Ai0 < 0f ? -side : side;
            float a = Mathf.Max(60f, length + Mathf.Clamp01((p - 0.5f) / 0.5f) * 40f);
            float b = Mathf.Abs(s.Ai0) * Mathf.Lerp(2f, 1f, near);
            float th = Mathf.PI + s.Spin * p * Mathf.PI * 2f;
            float ang = Mathf.Atan2(s.D.y, s.D.x);
            var e = Rotate(new Vector2(Mathf.Cos(th) * a, Mathf.Sin(th) * b), ang);
            var off = (1f - Mathf.Clamp01(p / 0.5f)) * new Vector2(-side * a * 0.1f, -s.Ai0 * 0.3f);
            s.Rel = s.D + e + off;
            s.BladeAngle = th + ang;
            s.Opacity = Mathf.Clamp01(s.T / 5f) * Mathf.Clamp01((120f - s.T) / 5f);
            // Newest first; kept relative to the player, so the trail moves with him.
            int n = Mathf.Min(s.TrailCount, TrailLength - 1);
            System.Array.Copy(s.TrailPos, 0, s.TrailPos, 1, n);
            System.Array.Copy(s.TrailAngle, 0, s.TrailAngle, 1, n);
            s.TrailPos[0] = s.Rel;
            s.TrailAngle[0] = s.BladeAngle;
            s.TrailCount = n + 1;
        }

        static Vector2 Rotate(Vector2 v, float a) =>
            new Vector2(v.x * Mathf.Cos(a) - v.y * Mathf.Sin(a), v.x * Mathf.Sin(a) + v.y * Mathf.Cos(a));

        /// <summary>
        /// The blade, 80 px long and 20 thick, at the sword and at four points back along its trail;
        /// each enemy is hit at most once every 7 ticks by each sword.
        /// </summary>
        void HitTest(Sword s)
        {
            var melee = TerraMod.Instance != null ? TerraMod.Instance.Melee : null;
            if (melee == null) return;
            CheckBlade(s, melee, s.Rel, s.BladeAngle);
            for (int k = 14; k < TrailLength; k += 15)
                if (k < s.TrailCount && s.T - k * s.Increment >= 0f) CheckBlade(s, melee, s.TrailPos[k], s.TrailAngle[k]);
        }

        void CheckBlade(Sword s, MeleeHits melee, Vector2 rel, float angle)
        {
            Vector2 world = TerraParticles.ToWorld(playerPx + rel);
            float deg = -angle * Mathf.Rad2Deg;
            int n = Physics2D.OverlapCapsuleNonAlloc(world, new Vector2(100f * Px, 20f * Px), CapsuleDirection2D.Horizontal, deg, overlaps, MeleeHits.HitMask);
            for (int i = 0; i < n; i++)
            {
                var col = overlaps[i];
                var hm = col.GetComponentInParent<HealthManager>();
                if (hm == null || hm.isDead) continue;
                int id = hm.GetInstanceID();
                if (s.Cooldowns.ContainsKey(id)) continue;
                s.Cooldowns[id] = HitCooldownTicks;
                melee.HitFrom(col, s.Item, s.Spin, world);
            }
        }

        static void Stop(Sword s)
        {
            s.Active = false;
            if (s.Root.activeSelf) s.Root.SetActive(false);
        }

        // --- Drawing. ---

        /// <summary>Main.DrawProj's fade for the Zenith's swords, by age in AI ticks.</summary>
        static float Fade(float x) => Mathf.Clamp01(x / 20f) * Mathf.Clamp01((68f - x) / 8f);

        void Draw(Sword s)
        {
            float fade = Fade(s.T);
            Place(s.Main, s, s.Rel, s.BladeAngle, 1f, new Color(1f, 1f, 1f, s.Opacity * fade), 0f);
            Place(s.Glow, s, s.Rel, s.BladeAngle, 1.25f, new Color(1f, 1f, 1f, 0.5f * s.Opacity * fade), -0.0001f);
            for (int i = 0; i < s.Echoes.Length; i++)
            {
                int k = 15 * (i + 1);
                var echo = s.Echoes[i];
                bool on = k < s.TrailCount;
                if (echo.enabled != on) echo.enabled = on;
                if (!on) continue;
                float alpha = (60 - k) / 60f * s.Opacity * Fade(s.T - k * s.Increment);
                Place(echo, s, s.TrailPos[k], s.TrailAngle[k], 1f, new Color(1f, 1f, 1f, alpha), 0.0001f * (i + 1));
            }
            DrawSparkle(s, fade);
            DrawRibbon(s);
        }

        /// <summary>
        /// A sword sprite at a point relative to the player. Terraria's sword art points up and
        /// right, so it is turned back a quarter-turn's half and mirrored when spinning the other way.
        /// </summary>
        void Place(SpriteRenderer sr, Sword s, Vector2 rel, float bladeAngle, float scale, Color color, float dz)
        {
            var w = TerraParticles.ToWorld(playerPx + rel);
            sr.transform.position = new Vector3(w.x, w.y, Z + dz);
            float rotation = bladeAngle + Mathf.PI / 2f - Mathf.PI / 4f * s.Spin;
            sr.transform.rotation = Quaternion.Euler(0f, 0f, -rotation * Mathf.Rad2Deg);
            sr.transform.localScale = Vector3.one * scale;
            sr.flipX = s.Spin == -1;
            sr.color = color;
        }

        /// <summary>Main.DrawPrettyStarSparkle at the blade's tip, brightest as the sword reaches its target.</summary>
        void DrawSparkle(Sword s, float fade)
        {
            float g = Mathf.Clamp01((s.T - 15f) / 15f) * Mathf.Clamp01((45f - s.T) / 15f);
            var tipPx = playerPx + s.Rel + new Vector2(Mathf.Cos(s.BladeAngle), Mathf.Sin(s.BladeAngle)) * s.Profile.Blade / 2f;
            var tip = TerraParticles.ToWorld(tipPx);
            var c = s.Profile.Trail * 0.5f * s.Opacity * fade;
            c.a = Mathf.Clamp01(s.Opacity * fade);
            for (int i = 0; i < s.Sparkle.Length; i++)
            {
                var sr = s.Sparkle[i];
                bool on = g > 0.01f;
                if (sr.enabled != on) sr.enabled = on;
                if (!on) continue;
                sr.transform.position = new Vector3(tip.x, tip.y, Z - 0.0002f);
                // One long streak across, a shorter one up and down.
                sr.transform.rotation = Quaternion.Euler(0f, 0f, i == 0 ? 90f : 0f);
                sr.transform.localScale = new Vector3(0.5f, i == 0 ? 5f : 2f, 1f) * g;
                sr.color = c;
            }
        }

        static readonly List<Vector3> verts = new List<Vector3>();
        static readonly List<Color> colors = new List<Color>();
        static readonly List<int> tris = new List<int>();

        /// <summary>
        /// FinalFractalHelper's ribbon: the area the blade swept over its last 60 steps, in the
        /// sword's colour, fading towards the old end (Terraria's shader approximated by additive colour).
        /// </summary>
        void DrawRibbon(Sword s)
        {
            verts.Clear();
            colors.Clear();
            tris.Clear();
            float half = s.Profile.Blade / 2f;
            for (int i = 0; i < s.TrailCount; i++)
            {
                var c = playerPx + s.TrailPos[i];
                var axis = new Vector2(Mathf.Cos(s.TrailAngle[i]), Mathf.Sin(s.TrailAngle[i])) * half;
                var a = TerraParticles.ToWorld(c + axis);
                var m = TerraParticles.ToWorld(c);
                var b = TerraParticles.ToWorld(c - axis);
                verts.Add(new Vector3(a.x, a.y, Z + 0.0003f));
                verts.Add(new Vector3(m.x, m.y, Z + 0.0003f));
                verts.Add(new Vector3(b.x, b.y, Z + 0.0003f));
                // Fading towards the old end, and (the shader's gradient, roughly) across the blade from its middle.
                var col = s.Profile.Trail;
                col.a = 0.35f * Mathf.Max(0f, 1f - (i / 59f) / 0.98f) * s.Opacity;
                var edge = col;
                edge.a = 0f;
                colors.Add(edge);
                colors.Add(col);
                colors.Add(edge);
                if (i > 0)
                {
                    int v = i * 3, u = v - 3;
                    for (int k = 0; k < 2; k++)
                    {
                        // Both faces: the strip twists as the sword turns.
                        tris.Add(u + k); tris.Add(u + k + 1); tris.Add(v + k);
                        tris.Add(u + k + 1); tris.Add(v + k + 1); tris.Add(v + k);
                        tris.Add(v + k); tris.Add(u + k + 1); tris.Add(u + k);
                        tris.Add(v + k); tris.Add(v + k + 1); tris.Add(u + k + 1);
                    }
                }
            }
            var mesh = s.Ribbon;
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
        }
    }
}
