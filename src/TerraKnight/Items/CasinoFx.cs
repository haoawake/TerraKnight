using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>How well a round went, for its show: less than the stake back, about the stake back, a win, a jackpot (10x and more).</summary>
    internal enum CasinoTier { Lose, Small, Win, Jackpot }

    /// <summary>
    /// The casino's pixel art (Resources/CasinoPx_*.png, drawn by an offline generator in Terraria's way: the machines at the
    /// world's size, 48x64 for their 3x4 tiles, and at three times that for the casino's window; the reel strips and their
    /// smears, the wheels, the balls and marbles, the pins, the bars, the effect sprites, the window's frame and buttons),
    /// always sampled nearest, and where each moving part sits on each machine (in the world-size picture's pixels, from its
    /// top left; the window's picture is <see cref="Large"/> times that).
    /// </summary>
    internal static class CasinoPx
    {
        public const float PxPerUnit = 32f;
        public const int Large = 3;
        public const int Cells = 5;

        static readonly Dictionary<string, Texture2D> textures = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

        public static Texture2D Tex(string name)
        {
            if (textures.TryGetValue(name, out var t) && t != null) return t;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("TerraKnight.CasinoPx_" + name + ".png"))
            {
                if (stream == null) throw new FileNotFoundException("CasinoPx_" + name);
                var bytes = new byte[stream.Length];
                int read = 0;
                while (read < bytes.Length)
                {
                    int n = stream.Read(bytes, read, bytes.Length - read);
                    if (n <= 0) break;
                    read += n;
                }
                t = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "CasinoPx_" + name };
                ImageConversion.LoadImage(t, bytes, false);
            }
            t.filterMode = FilterMode.Point;
            // The reel strips go round: their pictures repeat up and down.
            t.wrapMode = name.StartsWith("Reel") && !name.StartsWith("ReelShade") ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
            return textures[name] = t;
        }

        /// <summary>A whole picture as a sprite at the world's scale (32 px a unit, as Terraria's blocks), its pivot as given.</summary>
        public static Sprite Sprite(string name, Vector2 pivot)
        {
            string key = name + "|" + pivot.x + "," + pivot.y;
            if (sprites.TryGetValue(key, out var s) && s != null) return s;
            var t = Tex(name);
            s = UnityEngine.Sprite.Create(t, new Rect(0, 0, t.width, t.height), pivot, PxPerUnit, 0, SpriteMeshType.FullRect);
            return sprites[key] = s;
        }

        static readonly Dictionary<string, Sprite> centred = new Dictionary<string, Sprite>();

        /// <summary>A whole picture as a sprite pivoted on its middle (looked up by name alone: asked for every frame).</summary>
        public static Sprite Centred(string name)
        {
            if (centred.TryGetValue(name, out var s) && s != null) return s;
            return centred[name] = Sprite(name, new Vector2(0.5f, 0.5f));
        }

        public static string Machine(StationKind kind) => kind == StationKind.CasinoSlots ? "Slots" : kind == StationKind.CasinoPinball ? "Pinball" : "Roulette";

        // ---------------------------------------------------------------- where things are (world-size pixels)

        public static readonly RectInt[] Reels = { new RectInt(9, 27, 9, 11), new RectInt(20, 27, 9, 11), new RectInt(31, 27, 9, 11) };
        /// <summary>The world's reel strip: cells 10 px tall; the window's: 27.</summary>
        public const int ReelCell = 10, ReelCellL = 27;
        public static readonly Vector2 Wheel = new Vector2(24.5f, 22.5f);
        public const float WheelRadius = 16.5f;
        /// <summary>Where the ball runs, as a share of the wheel's radius (its sector ring).</summary>
        public const float BallTrack = 0.80f;
        public static readonly RectInt Pegs = new RectInt(7, 12, 30, 30);
        public static readonly RectInt Bar = new RectInt(7, 41, 30, 3);

        /// <summary>The tray each machine pays out of (its middle).</summary>
        public static Vector2 Tray(StationKind kind) =>
            kind == StationKind.CasinoSlots ? new Vector2(24f, 50.5f) : kind == StationKind.CasinoPinball ? new Vector2(22f, 51.5f) : new Vector2(24f, 51f);

        /// <summary>Each machine's lamps (x, y, size: 1 a small bulb), in the order a chase runs through them.</summary>
        public static Vector3[] Lamps(StationKind kind)
        {
            if (kind == StationKind.CasinoSlots) return slotLamps;
            if (kind == StationKind.CasinoPinball) return pinLamps;
            return wheelLamps;
        }

        static readonly Vector3[] slotLamps = MakeSlotLamps();
        static readonly Vector3[] pinLamps =
        {
            new Vector3(5f, 10f, 1.4f), new Vector3(24f, 4f, 1.1f), new Vector3(43f, 10f, 1.4f),
            new Vector3(41f, 42f, 1f), new Vector3(41f, 35.5f, 1f), new Vector3(41f, 29f, 1f), new Vector3(41f, 22.5f, 1f), new Vector3(41f, 16f, 1f),
        };
        static readonly Vector3[] wheelLamps = MakeWheelLamps();

        static Vector3[] MakeSlotLamps()
        {
            var list = new List<Vector3> { new Vector3(7.5f, 18.2f, 1.3f), new Vector3(12.2f, 12.6f, 1.3f), new Vector3(24f, 8.5f, 1.7f), new Vector3(35.8f, 12.6f, 1.3f), new Vector3(40.5f, 18.2f, 1.3f) };
            // The marquee round the reels: along the top of their frame, then back along the bottom.
            for (int k = 0; k < 9; k++) list.Add(new Vector3(8.5f + 4f * k, 25.5f, 0.75f));
            for (int k = 8; k >= 0; k--) list.Add(new Vector3(8.5f + 4f * k, 38.5f, 0.75f));
            list.Add(new Vector3(24f, 41.5f, 1.1f));
            return list.ToArray();
        }

        static Vector3[] MakeWheelLamps()
        {
            var list = new List<Vector3>();
            for (int k = 0; k < 8; k++)
            {
                float a = -Mathf.PI / 2f + k * Mathf.PI / 4f + Mathf.PI / 8f;
                list.Add(new Vector3(Wheel.x + Mathf.Cos(a) * 20.3f, Wheel.y + Mathf.Sin(a) * 20.3f, 1f));
            }
            list.Add(new Vector3(Wheel.x, 2.5f, 1.2f));
            return list.ToArray();
        }

        // ---------------------------------------------------------------- rounds

        public static CasinoTier TierOf(long stake, long payout) =>
            payout < stake || payout <= 0 ? CasinoTier.Lose : payout >= stake * 10 ? CasinoTier.Jackpot : payout * 10 >= stake * 19 ? CasinoTier.Win : CasinoTier.Small;

        /// <summary>
        /// A reel's place on its strip (in cells: a whole number is that symbol in the middle), snapped to the strip's own
        /// pixels so the art moves a pixel at a time.
        /// </summary>
        public static float SnapReel(float phase, int cell) => Mathf.Round(phase * cell) / cell;

        /// <summary>
        /// The ball's way down the pins (x 0..1 across, y 0..1 from the top to the bar): a hop from row to row, a little to
        /// one side or the other each time (by the round), drifting to where it lands, quicker as it falls.
        /// </summary>
        public static Vector2 PinPath(int sample, int multiplier, long sequence, float t)
        {
            const int rows = 9;
            float landing = (sample + 0.5f) / (100f * Mathf.Max(2, multiplier));
            float progress = Mathf.Pow(Mathf.Clamp01(t), 1.3f) * rows;
            int i = Mathf.Min(rows - 1, (int)progress);
            float s = progress - i;
            float x = Mathf.Lerp(RowX(i, rows, landing, sequence), RowX(i + 1, rows, landing, sequence), s);
            // Each hop bounces up off the pin before it drops to the next row.
            float y = (i + s) / rows - Mathf.Sin(s * Mathf.PI) * 0.045f * (1f - i / (float)rows);
            return new Vector2(Mathf.Clamp01(x), Mathf.Clamp01(y));
        }

        static float RowX(int row, int rows, float landing, long sequence)
        {
            if (row >= rows) return landing;
            float k = row / (float)rows;
            uint h = (uint)(sequence * 2654435761L) ^ (uint)(row * 40503);
            h ^= h >> 13;
            h *= 1274126177u;
            float jitter = ((h & 0xFFFF) / 65535f - 0.5f) * 0.24f;
            return Mathf.Clamp01(Mathf.Lerp(0.5f, landing, k) + jitter * (1f - k));
        }

        /// <summary>A Terraria sound, the louder the nearer the hero is (the player's own round at full).</summary>
        public static void Sound(string name, float volume, Vector2 at, bool local, float pitch = 0f)
        {
            var mod = TerraMod.Instance;
            if (mod == null) return;
            if (!local)
            {
                // Others' machines carry across the room: loud near, softer far, gone past 28 tiles.
                var hero = HeroController.SilentInstance;
                if (hero == null) return;
                float d = Vector2.Distance(hero.transform.position, at);
                if (d > 28f) return;
                volume *= Mathf.Lerp(1f, 0.35f, d / 28f);
            }
            if (pitch != 0f) mod.PlaySound(name, volume, pitch);
            else mod.PlaySound(name, volume);
        }
    }

    /// <summary>
    /// The machines standing in the world, dressed: what each shows between rounds (the slot's last three symbols, the wheel
    /// drifting round with its ball in the pocket it fell in, the pins and the bar of the last odds), its lamps twinkling and
    /// its warm light; a round on it (the reels spinning, smeared at speed, stopping one by one with a click; the wheel and
    /// the ball; the ball hopping down the pins with a trail, the lamps chasing); and its end: geo flung out of the tray,
    /// sparkles, the lamps flashing gold (fireworks over it for a jackpot), or for nothing back the lamps blinking red and
    /// going dim with a puff of smoke from the tray.
    /// </summary>
    internal static class CasinoWorld
    {
        static readonly StationKind[] kinds = { StationKind.CasinoSlots, StationKind.CasinoPinball, StationKind.CasinoRoulette };
        static readonly List<(Rect Box, SpriteRenderer Renderer)> standing = new List<(Rect, SpriteRenderer)>();
        static readonly List<Dress> dresses = new List<Dress>();
        static Material reelMaterial, smearMaterial, plain;

        public static int DevSprites;

        sealed class Dress
        {
            public StationKind Kind;
            public Rect Box;
            public SpriteRenderer Station;
            public Transform Root;
            public bool Seen;
            public int Order;
            // Slots.
            public Mesh[] ReelMesh, SmearMesh;
            public Vector2[] Uv = new Vector2[4];
            public Color[] SmearColours = new Color[4];
            public int[] Shown = { 0, 2, 4 };
            public bool[] Stopped = new bool[3];
            // Roulette.
            public SpriteRenderer Wheel, Ball;
            public float Spin, SpinAtResult;
            public Vector2 BallRest = new Vector2(0f, 1f);
            public bool BallResting;
            // Pinball: the marbles flying in it (as many as are drawn), the channel the last one fell into.
            public SpriteRenderer BarLit;
            public SpriteRenderer[] Trail;
            public int FlashChannel;
            public float FlashAt = -99f;
            // Every machine.
            public SpriteRenderer[] Lamps;
            public long Sequence = -1;
            public float ResultAt = -99f;
            public CasinoTier Tier;
        }

        public static void Tick()
        {
            DevSprites = 0;
            foreach (var d in dresses) d.Seen = false;
            foreach (var kind in kinds)
            {
                Stations.StandingOf(kind, standing);
                foreach (var (box, renderer) in standing)
                {
                    if (renderer == null || !renderer.gameObject.activeInHierarchy) continue;
                    var d = Find(kind, box);
                    if (d == null)
                    {
                        d = new Dress { Kind = kind, Box = box };
                        dresses.Add(d);
                    }
                    if (d.Root == null || d.Station != renderer) Build(d, renderer);
                    d.Box = box;
                    d.Seen = true;
                    try { Animate(d); }
                    catch (System.Exception e) { Plugin.Log.LogWarning("[赌场] 机器动画出错：" + e.Message); }
                }
            }
            for (int i = dresses.Count - 1; i >= 0; i--)
            {
                if (dresses[i].Seen) continue;
                if (dresses[i].Root != null) Object.Destroy(dresses[i].Root.gameObject);
                dresses.RemoveAt(i);
            }
            CasinoBits.Tick();
        }

        public static void Clear()
        {
            foreach (var d in dresses) if (d.Root != null) Object.Destroy(d.Root.gameObject);
            dresses.Clear();
            CasinoBits.Clear();
        }

        /// <summary>A marble into a channel of a pinball machine (anyone's, as it flies here): that channel of the little machine flashes.</summary>
        public static void PinFlash(CasinoPinball.Machine m, int channel)
        {
            foreach (var d in dresses)
                if (d.Kind == StationKind.CasinoPinball && CasinoPinball.At(m, d.Box)) { d.FlashChannel = channel; d.FlashAt = Time.realtimeSinceStartup; }
        }

        static Dress Find(StationKind kind, Rect box)
        {
            foreach (var d in dresses)
                if (d.Kind == kind && (d.Box.center - box.center).sqrMagnitude < 0.0001f) return d;
            return null;
        }

        // ---------------------------------------------------------------- building

        /// <summary>A world-size picture's pixel (from its top left) in the machine's own space (its foot's middle at 0).</summary>
        static Vector3 Local(Vector2 px, float z = 0f) => new Vector3((px.x - 24f) / CasinoPx.PxPerUnit, (64f - px.y) / CasinoPx.PxPerUnit, z);

        static Vector3 Local(float x, float y, float z = 0f) => Local(new Vector2(x, y), z);

        static void Build(Dress d, SpriteRenderer station)
        {
            if (d.Root != null) Object.Destroy(d.Root.gameObject);
            d.Station = station;
            d.Order = station.sortingOrder;
            var root = new GameObject("Terra Casino " + d.Kind);
            root.transform.SetParent(station.transform, false);
            root.transform.localPosition = new Vector3(0f, 0f, -0.00005f);
            d.Root = root.transform;
            if (plain == null) plain = new Material(Shader.Find("Sprites/Default"));
            if (d.Kind == StationKind.CasinoSlots)
            {
                if (reelMaterial == null) reelMaterial = new Material(plain) { mainTexture = CasinoPx.Tex("ReelS") };
                if (smearMaterial == null) smearMaterial = new Material(plain) { mainTexture = CasinoPx.Tex("ReelSBlur") };
                d.ReelMesh = new Mesh[3];
                d.SmearMesh = new Mesh[3];
                for (int n = 0; n < 3; n++)
                {
                    var r = CasinoPx.Reels[n];
                    var centre = new Vector2(r.x + r.width / 2f, r.y + r.height / 2f);
                    d.ReelMesh[n] = Quad(d, "Reel " + n, reelMaterial, centre, r.width, r.height, 1, -0.00001f);
                    d.SmearMesh[n] = Quad(d, "Reel smear " + n, smearMaterial, centre, r.width, r.height, 2, -0.00002f);
                    var shade = Part(d, "Reel shade " + n, CasinoPx.Centred("ReelShadeS"), centre, 3, -0.00003f);
                    shade.transform.localScale = new Vector3(r.width / 9f, r.height / 11f, 1f);
                }
            }
            else if (d.Kind == StationKind.CasinoRoulette)
            {
                d.Wheel = Part(d, "Wheel", CasinoPx.Centred("WheelS"), CasinoPx.Wheel, 1, -0.00001f);
                d.Ball = Part(d, "Ball", CasinoPx.Centred("BallS"), CasinoPx.Wheel, 2, -0.00002f);
            }
            else
            {
                var pegs = CasinoPx.Pegs;
                Part(d, "Pins", CasinoPx.Centred("PegsS"), new Vector2(pegs.x + pegs.width / 2f, pegs.y + pegs.height / 2f), 1, -0.00001f);
                var bar = CasinoPx.Bar;
                Part(d, "Bar", CasinoPx.Centred("BarDarkS"), new Vector2(bar.x + bar.width / 2f, bar.y + bar.height / 2f), 1, -0.00001f);
                d.BarLit = Part(d, "Bar lit", CasinoPx.Sprite("BarLitS", new Vector2(0f, 0.5f)), new Vector2(bar.x, bar.y + bar.height / 2f), 2, -0.00002f);
                d.Trail = new SpriteRenderer[16];
                for (int k = 0; k < d.Trail.Length; k++)
                    d.Trail[k] = Part(d, "Marble " + k, CasinoPx.Centred("BallS"), new Vector2(22f, 12f), 3, -0.00003f);
            }
            var lamps = CasinoPx.Lamps(d.Kind);
            d.Lamps = new SpriteRenderer[lamps.Length];
            var additive = TerraParticles.AdditiveMaterial();
            for (int k = 0; k < lamps.Length; k++)
            {
                var sr = Part(d, "Lamp " + k, CasinoPx.Centred("Halo"), new Vector2(lamps[k].x, lamps[k].y), 4, -0.00004f);
                if (additive != null) sr.sharedMaterial = additive;
                float size = lamps[k].z * 0.75f;
                sr.transform.localScale = new Vector3(size, size, 1f);
                d.Lamps[k] = sr;
            }
        }

        static SpriteRenderer Part(Dress d, string name, Sprite sprite, Vector2 px, int order, float z)
        {
            var go = new GameObject(name);
            go.transform.SetParent(d.Root, false);
            go.transform.localPosition = Local(px, z);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingLayerID = d.Station.sortingLayerID;
            sr.sortingOrder = d.Order + order;
            return sr;
        }

        static Mesh Quad(Dress d, string name, Material material, Vector2 px, int w, int h, int order, float z)
        {
            var go = new GameObject(name);
            go.transform.SetParent(d.Root, false);
            go.transform.localPosition = Local(px, z);
            float hw = w / CasinoPx.PxPerUnit / 2f, hh = h / CasinoPx.PxPerUnit / 2f;
            var mesh = new Mesh { name = name };
            mesh.vertices = new[] { new Vector3(-hw, -hh), new Vector3(hw, -hh), new Vector3(-hw, hh), new Vector3(hw, hh) };
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.one };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.sortingLayerID = d.Station.sortingLayerID;
            mr.sortingOrder = d.Order + order;
            return mesh;
        }

        // ---------------------------------------------------------------- each frame

        static void Animate(Dress d)
        {
            if (d.Station.sortingOrder != d.Order) Build(d, d.Station);
            float now = Time.realtimeSinceStartup;
            bool showing = CasinoShow.ShowingAt(d.Kind, d.Box, out var round, out float age, out bool local);
            float t = showing ? Mathf.Clamp01(age / round.Duration) : 1f;
            bool spinning = showing && t < 1f;
            if (d.Kind == StationKind.CasinoSlots) Slots(d, showing ? round : null, t, local);
            else if (d.Kind == StationKind.CasinoRoulette) Roulette(d, showing ? round : null, t, spinning);
            else spinning = Pinball(d) > 0;
            Lamps(d, spinning, now);
            DevSprites += d.Root.childCount;
        }

        static void Slots(Dress d, CasinoShow.Round r, float t, bool local)
        {
            for (int n = 0; n < 3; n++)
            {
                float phase = d.Shown[n], speed = 0f;
                if (r != null && r.Sequence != d.Sequence)
                {
                    // The little reels show each reel's middle symbol (its own order: Casino.Strips).
                    int target = Casino.SymbolAt(n, r.Reels[n], 0);
                    phase = CasinoAnimation.Reel(target, n, t, r.Sequence);
                    float later = CasinoAnimation.Reel(target, n, Mathf.Min(1f, t + 0.02f), r.Sequence);
                    speed = Mathf.Abs(phase - later) / (0.02f * r.Duration);
                    // A click as each reel comes to rest.
                    bool stopped = t >= 0.70f + n * 0.15f;
                    if (stopped && !d.Stopped[n] && !(local && Casino.IsOpen)) CasinoPx.Sound("tk_casino_reel_" + n, 0.6f, d.Box.center, local);
                    d.Stopped[n] = stopped;
                }
                SetReel(d.ReelMesh[n], d.Uv, CasinoPx.SnapReel(phase, CasinoPx.ReelCell), CasinoPx.ReelCell, CasinoPx.Reels[n].height);
                SetReel(d.SmearMesh[n], d.Uv, CasinoPx.SnapReel(phase, CasinoPx.ReelCell), CasinoPx.ReelCell, CasinoPx.Reels[n].height);
                float smear = Mathf.Clamp01((speed - 3f) / 8f);
                for (int k = 0; k < 4; k++) d.SmearColours[k] = new Color(1f, 1f, 1f, smear);
                d.SmearMesh[n].colors = d.SmearColours;
            }
        }

        /// <summary>A reel's window on its strip: a window's height of pixels round the phase's cell (the higher cells below).</summary>
        static void SetReel(Mesh mesh, Vector2[] uv, float phase, int cell, int height)
        {
            float strip = cell * CasinoPx.Cells;
            float top = Mathf.Round((phase + 0.5f) * cell - height / 2f);
            float vTop = 1f - top / strip, vBottom = 1f - (top + height) / strip;
            uv[0] = new Vector2(0f, vBottom);
            uv[1] = new Vector2(1f, vBottom);
            uv[2] = new Vector2(0f, vTop);
            uv[3] = new Vector2(1f, vTop);
            mesh.uv = uv;
        }

        static void Roulette(Dress d, CasinoShow.Round r, float t, bool spinning)
        {
            const float pxR = CasinoPx.WheelRadius * CasinoPx.BallTrack;
            if (r != null && r.Sequence != d.Sequence)
            {
                d.Spin = -CasinoAnimation.Wheel(r.Wheel, t);
                var b = CasinoAnimation.Ball(t);
                // Ball() is in the window's way up (y down); a hop or two as it settles.
                float hop = t > 0.7f && t < 0.97f ? Mathf.Abs(Mathf.Sin(t * 70f)) * 0.06f * (1f - t) * 4f : 0f;
                d.Ball.transform.localPosition = Local(CasinoPx.Wheel + new Vector2(b.x, b.y) * pxR * (1f + hop), -0.00002f);
                d.BallResting = false;
            }
            else
            {
                // Between rounds the wheel drifts round, the ball riding in its pocket.
                d.Spin += 6f * Time.deltaTime;
                float turn = (d.Spin - d.SpinAtResult) * Mathf.Deg2Rad;
                var rest = d.BallResting ? d.BallRest : new Vector2(0f, -1f);
                var p = new Vector2(rest.x * Mathf.Cos(turn) + rest.y * Mathf.Sin(turn), -rest.x * Mathf.Sin(turn) + rest.y * Mathf.Cos(turn));
                d.Ball.transform.localPosition = Local(CasinoPx.Wheel + p * pxR, -0.00002f);
            }
            d.Wheel.transform.localRotation = Quaternion.Euler(0f, 0f, d.Spin);
        }

        /// <summary>
        /// The little pinball: the marbles flying in the machine (anyone's, as they really fly: CasinoPinball), its field
        /// shrunk to the pins and the bar; the channel the last one fell into flashing.
        /// </summary>
        static int Pinball(Dress d)
        {
            CasinoPinball.BallsAt(d.Box, flying);
            var pegs = CasinoPx.Pegs;
            var bar = CasinoPx.Bar;
            float now = Time.realtimeSinceStartup;
            for (int k = 0; k < d.Trail.Length; k++)
            {
                var sr = d.Trail[k];
                bool on = k < flying.Count;
                sr.enabled = on;
                if (!on) continue;
                var b = flying[k].Ball;
                // The window's field (x 15..172, y 17..233) over the little machine's pins and bar.
                float x = pegs.x + (float)((b.X - PinSim.WallL) / (PinSim.WallR - PinSim.WallL)) * pegs.width;
                float y = pegs.y + (float)((PinSim.DrawY(b) - 17) / 216.0) * (bar.y + bar.height - pegs.y);
                sr.transform.localPosition = Local(new Vector2(Mathf.Round(x), Mathf.Round(y)), -0.00003f);
                sr.color = flying[k].Info.Denomination == Casino.AllIn ? Color.HSVToRGB(Mathf.Repeat(now * 1.5f + k * 0.2f, 1f), 0.6f, 1f) : Color.white;
            }
            float flash = Mathf.Clamp01(1f - (now - d.FlashAt) / 0.6f);
            d.BarLit.transform.localPosition = Local(new Vector2(bar.x + d.FlashChannel * bar.width / (float)Casino.Channels, bar.y + bar.height / 2f), -0.00002f);
            d.BarLit.transform.localScale = new Vector3(flash > 0f ? 1f / Casino.Channels : 0f, 1f, 1f);
            d.BarLit.color = new Color(1f, 1f, 1f, flash);
            return flying.Count;
        }

        static readonly List<(PinSim.Ball Ball, CasinoPinball.Info Info, CasinoPinball.Machine Machine)> flying = new List<(PinSim.Ball, CasinoPinball.Info, CasinoPinball.Machine)>();

        static readonly Color Warm = new Color(1f, 0.78f, 0.38f), Gold = new Color(1f, 0.86f, 0.42f), Ember = new Color(1f, 0.22f, 0.16f);

        static void Lamps(Dress d, bool spinning, float now)
        {
            float since = now - d.ResultAt;
            bool won = d.Tier != CasinoTier.Lose && since < (d.Tier == CasinoTier.Jackpot ? 4.5f : 2.6f);
            bool lost = d.Tier == CasinoTier.Lose && since < 1.8f;
            float sum = 0f;
            for (int k = 0; k < d.Lamps.Length; k++)
            {
                float b;
                Color c = Warm;
                if (spinning)
                {
                    // A chase round the lamps.
                    int step = Mathf.FloorToInt(now * 14f);
                    b = ((step - k) % 4 + 4) % 4 == 0 ? 1f : 0.28f;
                    c = Gold;
                }
                else if (won)
                {
                    bool odd = (Mathf.FloorToInt(now * 8f) + k) % 2 == 0;
                    b = odd ? 1f : 0.45f;
                    c = d.Tier == CasinoTier.Jackpot ? Color.HSVToRGB(Mathf.Repeat(now * 0.6f + k * 0.07f, 1f), 0.55f, 1f) : Gold;
                }
                else if (lost)
                {
                    // Two red blinks, then dim, coming back.
                    b = since < 0.5f ? (Mathf.FloorToInt(since * 8f) % 2 == 0 ? 1f : 0.1f) : Mathf.Lerp(0.08f, 0.4f, (since - 0.5f) / 1.3f);
                    c = since < 0.5f ? Ember : Color.Lerp(Ember, Warm, (since - 0.5f) / 1.3f);
                }
                else b = 0.32f + 0.18f * Mathf.Sin(now * 1.6f + k * 1.3f);
                sum += b;
                c.a = b;
                d.Lamps[k].color = c;
            }
            // The machine's own warm light on what is round it (brighter as it pays).
            var fx = FxBatch.Instance;
            if (fx != null)
            {
                float avg = sum / Mathf.Max(1, d.Lamps.Length);
                var light = (won ? Gold * (0.7f + 0.6f * Mathf.Clamp01(1f - since / 2.6f)) : lost && since < 0.5f ? Ember * 0.5f : Warm * (0.22f + 0.3f * avg)) * 0.6f;
                fx.Light(TerraParticles.ToPx(d.Root.TransformPoint(Local(24f, 30f))), light, won ? 7f : 4.5f);
            }
        }

        // ---------------------------------------------------------------- a round's end

        /// <summary>A round shown on a machine has come to its end: what it paid, shown on and round the machine.</summary>
        public static void Result(StationKind kind, Rect box, CasinoShow.Round r, bool local)
        {
            var d = Find(kind, box);
            var tier = CasinoPx.TierOf(r.Stake, r.Payout);
            // All in and won: the room lit in every colour, the number in rainbow.
            bool rainbow = r.AllIn && r.Payout > r.Stake;
            bool pin = kind == StationKind.CasinoPinball;
            if (d == null || d.Root == null)
            {
                Popup(new Vector2(box.center.x, box.yMax + 0.2f), r.Payout - r.Stake, r.Payout, 0, rainbow);
                return;
            }
            d.Sequence = r.Sequence;
            d.ResultAt = Time.realtimeSinceStartup;
            d.Tier = tier;
            if (kind == StationKind.CasinoRoulette)
            {
                d.Spin = -CasinoAnimation.Wheel(r.Wheel, 1f);
                d.SpinAtResult = d.Spin;
                var b = CasinoAnimation.Ball(1f);
                d.BallRest = b.normalized;
                d.BallResting = true;
            }
            for (int n = 0; n < 3; n++) d.Stopped[n] = false;
            if (kind == StationKind.CasinoSlots) d.Shown = new[] { Casino.SymbolAt(0, r.Reels[0], 0), Casino.SymbolAt(1, r.Reels[1], 0), Casino.SymbolAt(2, r.Reels[2], 0) };
            var tray = (Vector2)d.Root.TransformPoint(Local(CasinoPx.Tray(kind)));
            var middle = (Vector2)d.Root.TransformPoint(Local(24f, 30f));
            var top = (Vector2)d.Root.TransformPoint(Local(24f, -4f));
            float floor = box.y + 0.02f, z = d.Root.position.z - 0.0002f;
            int order = d.Order + 6;
            // The window's own sounds play for the one at it; these are for the room.
            bool hear = !(local && Casino.IsOpen);
            Popup(top + (pin ? new Vector2(Random.Range(-0.5f, 0.5f), 0f) : Vector2.zero), r.Payout - r.Stake, r.Payout, order + 1, rainbow);
            if (rainbow)
            {
                CasinoBits.Colours(middle, z, order, 160);
                for (int k = 0; k < 6; k++)
                    CasinoBits.Firework(middle + new Vector2((k - 2.5f) * 0.9f, 2.2f + (k % 3) * 0.6f), z, order, k * 0.25f);
            }
            // Out loud for the room: every win and every loss (a marble less than its value back is a loss).
            switch (tier)
            {
                case CasinoTier.Lose:
                    CasinoBits.Smoke(tray, z, order, pin ? 3 : 5);
                    if (hear) CasinoPx.Sound("tk_casino_lose", 1f, box.center, local);
                    break;
                case CasinoTier.Small:
                    CasinoBits.Geo(tray, floor, z, order, pin ? 3 : 6, 0.8f);
                    CasinoBits.Colours(middle, z, order, 20);
                    if (hear) CasinoPx.Sound("tk_casino_win_small", 1f, box.center, local);
                    break;
                case CasinoTier.Win:
                    CasinoBits.Geo(tray, floor, z, order, 18, 1f);
                    CasinoBits.Stars(middle, new Vector2(1f, 1.2f), z, order, 16);
                    CasinoBits.Colours(middle, z, order, 50);
                    if (hear) CasinoPx.Sound("tk_casino_win_big", 1f, box.center, local);
                    break;
                default:
                    CasinoBits.Geo(tray, floor, z, order, 44, 1.25f);
                    CasinoBits.Stars(middle, new Vector2(1.3f, 1.5f), z, order, 30);
                    CasinoBits.Colours(middle, z, order, 90);
                    for (int k = 0; k < 4; k++)
                        CasinoBits.Firework(middle + new Vector2((k - 1.5f) * 0.8f, 2.4f + (k % 2) * 0.5f), z, order, k * 0.3f);
                    if (hear) CasinoPx.Sound("tk_casino_jackpot", 1f, box.center, local);
                    break;
            }
        }

        // ---------------------------------------------------------------- the big numbers over the machines

        struct Pop
        {
            public Vector2 At;
            public long Net;
            public string Text;
            public float Start, Side;
            public bool Rainbow;
        }

        static readonly List<Pop> pops = new List<Pop>();
        static GUIStyle popStyle;

        /// <summary>
        /// A win's or a loss's word bursting out over a machine, for everyone in the room: gold and flying up with what it
        /// paid for a win, black in a puff of smoke, sinking, for a loss.
        /// </summary>
        public static void Popup(Vector2 world, long net, long payout, int order, bool rainbow = false)
        {
            if (pops.Count > 40) pops.RemoveAt(0);
            // A win shows what it paid out; an even or a lost round, a word.
            string text = net > 0 ? (rainbow ? "全额中奖 +" : "中奖 +") + PopMoney(payout) : net == 0 ? "保本" : "失败";
            pops.Add(new Pop { At = world, Net = net, Text = text, Start = Time.realtimeSinceStartup, Side = Random.Range(-1f, 1f), Rainbow = rainbow });
            if (net < 0) CasinoBits.Smoke(world, 0f, order, 4);
        }

        /// <summary>Geo with thousands marks (copper in hundredths), as "12,500 吉欧".</summary>
        static string PopMoney(long copper) => Lang.F("{0} 吉欧", (copper / 100m).ToString(copper % 100 == 0 ? "N0" : "N2"));

        /// <summary>The numbers, drawn over the world (from TerraMod's interface, through CasinoShow.Draw).</summary>
        public static void DrawGui()
        {
            if (pops.Count == 0 || Event.current.type != EventType.Repaint) return;
            var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
            if (cam == null) return;
            if (popStyle == null) popStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = false, clipping = TextClipping.Overflow };
            float now = Time.realtimeSinceStartup, ui = UI.GameView.Scale;
            for (int i = pops.Count - 1; i >= 0; i--)
            {
                var p = pops[i];
                float age = now - p.Start;
                bool lost = p.Net < 0;
                float life = lost ? 2.4f : p.Rainbow ? 4f : 2.6f;
                if (age > life) { pops.RemoveAt(i); continue; }
                // A win leaps up and out, a loss rises a little and sinks; both pop in big and settle.
                var w = p.At + (lost ? new Vector2(p.Side * 0.3f * age, 0.5f * age - 0.25f * age * age) : new Vector2(p.Side * 0.8f * age, 2.4f * age - 0.45f * age * age));
                var s = cam.WorldToScreenPoint(new Vector3(w.x, w.y, 0f));
                if (s.z < 0f) continue;
                float big = (System.Math.Abs(p.Net) >= 1000000 ? 1.5f : System.Math.Abs(p.Net) >= 100000 ? 1.25f : 1f) * (p.Rainbow ? 1.6f : 1f);
                float pop = 1f + 0.6f * Mathf.Exp(-age * 7f) * Mathf.Cos(age * 22f);
                float alpha = Mathf.Clamp01((life - age) / 0.6f) * Mathf.Clamp01(age * 10f);
                popStyle.fontSize = Mathf.Max(12, Mathf.RoundToInt(30f * ui * big * pop));
                var r = new Rect(s.x - 300f, Screen.height - s.y - 40f, 600f, 80f);
                float o = Mathf.Max(1f, Mathf.Round(2f * ui));
                var edge = lost ? new Color(0.85f, 0.85f, 0.9f, alpha) : new Color(0.3f, 0.12f, 0f, alpha);
                var old = GUI.color;
                GUI.color = edge;
                GUI.Label(new Rect(r.x + o, r.y, r.width, r.height), p.Text, popStyle);
                GUI.Label(new Rect(r.x - o, r.y, r.width, r.height), p.Text, popStyle);
                GUI.Label(new Rect(r.x, r.y + o, r.width, r.height), p.Text, popStyle);
                GUI.Label(new Rect(r.x, r.y - o, r.width, r.height), p.Text, popStyle);
                var main = lost ? new Color(0.06f, 0.05f, 0.07f, alpha) : p.Rainbow ? Color.HSVToRGB(Mathf.Repeat(age * 1.2f, 1f), 0.65f, 1f) : Mathf.FloorToInt(age * 10f) % 2 == 0 ? new Color(1f, 0.92f, 0.45f, alpha) : new Color(1f, 0.78f, 0.2f, alpha);
                main.a = alpha;
                GUI.color = main;
                GUI.Label(r, p.Text, popStyle);
                GUI.color = old;
            }
        }
    }

    /// <summary>
    /// The casino's own little pixel particles in the world (they outlive the frame, so not FxBatch's): geo coins flung out
    /// of a tray that flip as they fly and bounce on the floor, sparkles, firework sparks, smoke.
    /// </summary>
    internal static class CasinoBits
    {
        enum Kind : byte { Geo, Star, Spark, Smoke }

        struct Bit
        {
            public bool Active;
            public Kind Kind;
            public Vector2 Pos, Vel;
            public float Life, Max, Size, Spin, Turn, Floor, Gravity, Drag, Delay, Z;
            public Color Colour;
            public int Order;
        }

        const int Most = 220;
        static readonly Bit[] bits = new Bit[Most];
        static readonly SpriteRenderer[] renderers = new SpriteRenderer[Most];
        static GameObject root;

        public static void Clear()
        {
            for (int i = 0; i < Most; i++) bits[i].Active = false;
            if (root != null) Object.Destroy(root);
            root = null;
        }

        static int Free()
        {
            for (int i = 0; i < Most; i++) if (!bits[i].Active) return i;
            return -1;
        }

        static void Add(Bit b)
        {
            int i = Free();
            if (i < 0) return;
            b.Active = true;
            bits[i] = b;
        }

        public static void Geo(Vector2 at, float floor, float z, int order, int count, float power)
        {
            for (int k = 0; k < count; k++)
                Add(new Bit
                {
                    Kind = Kind.Geo, Pos = at + new Vector2(Random.Range(-0.2f, 0.2f), 0.05f), Floor = floor, Z = z, Order = order,
                    Vel = new Vector2(Random.Range(-2.6f, 2.6f), Random.Range(4.2f, 8f)) * power, Gravity = 17f, Drag = 0.2f,
                    Life = 0f, Max = Random.Range(1.6f, 2.5f), Size = 1f, Spin = Random.Range(0f, 6f), Turn = Random.Range(8f, 15f),
                    Colour = Color.white, Delay = k * 0.012f,
                });
        }

        public static void Stars(Vector2 at, Vector2 spread, float z, int order, int count)
        {
            for (int k = 0; k < count; k++)
                Add(new Bit
                {
                    Kind = Kind.Star, Pos = at + new Vector2(Random.Range(-spread.x, spread.x), Random.Range(-spread.y, spread.y)), Z = z - 0.0001f, Order = order + 1,
                    Vel = new Vector2(0f, Random.Range(0.2f, 0.6f)), Max = Random.Range(0.45f, 0.9f), Size = Random.value < 0.3f ? 1.6f : 1f,
                    Colour = Random.value < 0.5f ? new Color(1f, 0.9f, 0.5f) : Color.white, Delay = Random.Range(0f, 0.6f),
                });
        }

        static readonly Color[] fireworks = { new Color(1f, 0.85f, 0.3f), new Color(1f, 0.35f, 0.75f), new Color(0.4f, 0.9f, 1f), new Color(0.5f, 1f, 0.5f) };

        public static void Firework(Vector2 at, float z, int order, float delay)
        {
            var c = fireworks[Random.Range(0, fireworks.Length)];
            const int sparks = 34;
            for (int k = 0; k < sparks; k++)
            {
                float a = k / (float)sparks * Mathf.PI * 2f + Random.Range(-0.05f, 0.05f);
                float speed = Random.Range(3f, 4.6f);
                Add(new Bit
                {
                    Kind = Kind.Spark, Pos = at, Z = z - 0.0001f, Order = order + 1, Vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed,
                    Gravity = 2.4f, Drag = 1.9f, Max = Random.Range(0.8f, 1.2f), Size = Random.value < 0.25f ? 1.5f : 1f,
                    Colour = Color.Lerp(c, Color.white, Random.Range(0f, 0.35f)), Delay = delay,
                });
            }
            Add(new Bit { Kind = Kind.Star, Pos = at, Z = z - 0.0002f, Order = order + 2, Max = 0.35f, Size = 3f, Colour = Color.white, Delay = delay });
        }

        /// <summary>Sparks of every colour out of a point: a win's light.</summary>
        public static void Colours(Vector2 at, float z, int order, int count)
        {
            for (int k = 0; k < count; k++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f), speed = Random.Range(2f, 6.5f);
                Add(new Bit
                {
                    Kind = Kind.Spark, Pos = at, Z = z - 0.0001f, Order = order + 1, Vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed,
                    Gravity = 1.5f, Drag = 1.6f, Max = Random.Range(0.7f, 1.4f), Size = Random.value < 0.3f ? 2f : 1f,
                    Colour = Color.HSVToRGB(Random.value, 0.75f, 1f), Delay = Random.Range(0f, 0.25f),
                });
            }
        }

        public static void Smoke(Vector2 at, float z, int order, int count)
        {
            for (int k = 0; k < count; k++)
                Add(new Bit
                {
                    Kind = Kind.Smoke, Pos = at + new Vector2(Random.Range(-0.25f, 0.25f), 0f), Z = z, Order = order,
                    Vel = new Vector2(Random.Range(-0.3f, 0.3f), Random.Range(0.45f, 0.8f)), Max = Random.Range(1.1f, 1.6f), Size = 1f,
                    Colour = new Color(0.55f, 0.55f, 0.62f), Delay = k * 0.08f,
                });
        }

        public static void Tick()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < Most; i++)
            {
                ref var b = ref bits[i];
                var sr = renderers[i];
                if (!b.Active)
                {
                    if (sr != null && sr.enabled) sr.enabled = false;
                    continue;
                }
                if (b.Delay > 0f)
                {
                    b.Delay -= dt;
                    if (sr != null && sr.enabled) sr.enabled = false;
                    continue;
                }
                b.Life += dt;
                if (b.Life >= b.Max)
                {
                    b.Active = false;
                    if (sr != null) sr.enabled = false;
                    continue;
                }
                b.Vel.y -= b.Gravity * dt;
                b.Vel *= Mathf.Exp(-b.Drag * dt);
                b.Pos += b.Vel * dt;
                if (b.Kind == Kind.Geo)
                {
                    if (b.Pos.y < b.Floor && b.Vel.y < 0f)
                    {
                        b.Pos.y = b.Floor;
                        b.Vel.y = Mathf.Abs(b.Vel.y) < 1f ? 0f : -b.Vel.y * 0.42f;
                        b.Vel.x *= 0.6f;
                        b.Turn *= 0.6f;
                    }
                    b.Spin += b.Turn * dt;
                }
                Draw(i, ref b);
            }
        }

        static void Draw(int i, ref Bit b)
        {
            if (root == null)
            {
                root = new GameObject("Terra Casino Bits");
                for (int k = 0; k < Most; k++) renderers[k] = null;
            }
            var sr = renderers[i];
            if (sr == null)
            {
                var go = new GameObject("Bit");
                go.transform.SetParent(root.transform, false);
                sr = go.AddComponent<SpriteRenderer>();
                renderers[i] = sr;
            }
            float t = b.Life / b.Max;
            float fade = Mathf.Clamp01((1f - t) / 0.3f);
            var additive = TerraParticles.AdditiveMaterial();
            switch (b.Kind)
            {
                case Kind.Geo:
                    Set(sr, "Geo", null);
                    sr.transform.localScale = new Vector3(Mathf.Max(0.2f, Mathf.Abs(Mathf.Cos(b.Spin))), 1f, 1f);
                    sr.color = new Color(1f, 1f, 1f, fade);
                    break;
                case Kind.Star:
                    Set(sr, b.Size > 1.2f ? "Star9" : "Star5", additive);
                    float pop = Mathf.Sin(t * Mathf.PI);
                    sr.transform.localScale = Vector3.one * Mathf.Max(0.1f, pop) * (b.Size > 1.2f ? b.Size / 1.6f : 1f);
                    sr.color = new Color(b.Colour.r, b.Colour.g, b.Colour.b, pop);
                    break;
                case Kind.Spark:
                    Set(sr, "Spark", additive);
                    sr.transform.localScale = Vector3.one * b.Size;
                    sr.color = new Color(b.Colour.r, b.Colour.g, b.Colour.b, fade * (0.75f + 0.25f * Mathf.Sin(b.Life * 40f)));
                    break;
                default:
                    Set(sr, "Smoke", null);
                    sr.transform.localScale = Vector3.one * Mathf.Lerp(0.8f, 1.8f, t);
                    sr.color = new Color(b.Colour.r, b.Colour.g, b.Colour.b, 0.45f * (1f - t));
                    break;
            }
            sr.sortingOrder = b.Order;
            // Whole Terraria pixels, as the game's own dust.
            float px = 1f / CasinoPx.PxPerUnit;
            sr.transform.position = new Vector3(Mathf.Round(b.Pos.x / px) * px, Mathf.Round(b.Pos.y / px) * px, b.Z);
            if (!sr.enabled) sr.enabled = true;
        }

        static void Set(SpriteRenderer sr, string sprite, Material material)
        {
            var s = CasinoPx.Centred(sprite);
            if (sr.sprite != s) sr.sprite = s;
            var m = material != null ? material : DefaultMaterial(sr);
            if (sr.sharedMaterial != m) sr.sharedMaterial = m;
        }

        static Material defaultMaterial;

        static Material DefaultMaterial(SpriteRenderer sr)
        {
            if (defaultMaterial == null) defaultMaterial = new Material(Shader.Find("Sprites/Default"));
            return defaultMaterial;
        }
    }

    /// <summary>
    /// The casino window's own show, on the screen (its pixels; sprites at the scene's <see cref="P"/> screen pixels a
    /// pixel): geo flung out of a tray that flips and falls, a rain of geo from the top, sparkles, firework sparks in every
    /// colour, smoke, a white flash; numbers that float up from where a marble lands; the big numbers that burst out beside
    /// the machine (gold for what it won, black with smoke for what it lost); the banner over the machine.
    /// </summary>
    internal static class CasinoGuiFx
    {
        public static float P = 4f;

        struct Bit
        {
            public bool Active;
            public byte Kind;
            public Vector2 Pos, Vel;
            public float Life, Max, Spin, Turn, Delay, Size;
            public Color Colour;
        }

        struct Word
        {
            public bool Active, Big, Lost, Rainbow;
            public string Text;
            public Vector2 Pos, Vel;
            public float Life, Max, Delay;
            public Color Colour;
        }

        static readonly Bit[] bits = new Bit[900];
        static readonly Word[] words = new Word[48];
        static float last, flash, flashAt = -99f, rainbowAt = -99f;
        static string head = "", detail = "";
        static CasinoTier tier;
        static Vector2 bannerAt;
        static float bannerFrom = -99f;
        static GUIStyle style, small;

        public static float Since => Time.realtimeSinceStartup - bannerFrom;

        public static void Reset()
        {
            for (int i = 0; i < bits.Length; i++) bits[i].Active = false;
            for (int i = 0; i < words.Length; i++) words[i].Active = false;
            bannerFrom = -99f;
            flash = 0f;
            rainbowAt = -99f;
        }

        /// <summary>All in, and won: the whole screen in every colour, fireworks all over, geo raining.</summary>
        public static void Rainbow()
        {
            rainbowAt = Time.realtimeSinceStartup;
            for (int k = 0; k < 8; k++) Colours(new Vector2(Random.Range(0f, Screen.width), Random.Range(0f, Screen.height)), 50);
            for (int k = 0; k < 9; k++)
                Firework(new Vector2(Random.Range(Screen.width * 0.08f, Screen.width * 0.92f), Random.Range(Screen.height * 0.08f, Screen.height * 0.6f)), k * 0.3f);
            Rain(160);
            Flash(0.9f);
        }

        static void Add(byte kind, Vector2 pos, Vector2 vel, float max, float delay, Color colour, float size = 1f)
        {
            for (int i = 0; i < bits.Length; i++)
            {
                if (bits[i].Active) continue;
                bits[i] = new Bit { Active = true, Kind = kind, Pos = pos, Vel = vel, Max = max, Delay = delay, Colour = colour, Size = size, Spin = Random.Range(0f, 6f), Turn = Random.Range(7f, 14f) };
                return;
            }
        }

        /// <summary>Geo flung up out of a point (screen), falling back.</summary>
        public static void Burst(Vector2 at, int count, float power)
        {
            for (int k = 0; k < count; k++)
                Add(0, at, new Vector2(Random.Range(-70f, 70f), Random.Range(-150f, -95f)) * P * power, Random.Range(1.6f, 2.4f), k * 0.012f, Color.white);
        }

        public static void Rain(int count)
        {
            for (int k = 0; k < count; k++)
                Add(0, new Vector2(Random.Range(0f, Screen.width), Random.Range(-40f, -5f) * P), new Vector2(Random.Range(-8f, 8f), Random.Range(25f, 60f)) * P,
                    Random.Range(2.4f, 3.6f), Random.Range(0f, 1.6f), Color.white);
        }

        public static void Stars(Rect around, int count)
        {
            for (int k = 0; k < count; k++)
                Add(1, new Vector2(Random.Range(around.xMin, around.xMax), Random.Range(around.yMin, around.yMax)), new Vector2(0f, -5f * P),
                    Random.Range(0.45f, 0.9f), Random.Range(0f, 1.2f), Color.HSVToRGB(Random.value, 0.35f, 1f), Random.value < 0.3f ? 1.8f : 1f);
        }

        /// <summary>A burst of sparks in one colour (and white), a flash at its middle.</summary>
        public static void Firework(Vector2 at, float delay)
        {
            var c = Color.HSVToRGB(Random.value, 0.7f, 1f);
            for (int k = 0; k < 34; k++)
            {
                float a = k / 34f * Mathf.PI * 2f;
                Add(2, at, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(40f, 62f) * P, Random.Range(0.8f, 1.3f), delay, Color.Lerp(c, Color.white, Random.Range(0f, 0.3f)));
            }
            Add(1, at, Vector2.zero, 0.35f, delay, Color.white, 3f);
        }

        /// <summary>Rays of every colour out of a point (a win's light).</summary>
        public static void Colours(Vector2 at, int count)
        {
            for (int k = 0; k < count; k++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                Add(2, at, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(30f, 90f) * P, Random.Range(0.6f, 1.2f), Random.Range(0f, 0.3f), Color.HSVToRGB(Random.value, 0.75f, 1f), 1.5f);
            }
        }

        public static void Smoke(Vector2 at, int count)
        {
            for (int k = 0; k < count; k++)
                Add(3, at + new Vector2(Random.Range(-8f, 8f) * P, 0f), new Vector2(Random.Range(-4f, 4f), Random.Range(-16f, -9f)) * P, Random.Range(1f, 1.6f), k * 0.06f, new Color(0.55f, 0.55f, 0.62f));
        }

        public static void Flash(float strength)
        {
            flash = Mathf.Max(flash, strength);
            flashAt = Time.realtimeSinceStartup;
        }

        /// <summary>A small number floating up (a marble's score).</summary>
        public static void Float(Vector2 at, string text, Color colour, float size)
        {
            AddWord(new Word { Text = text, Pos = at, Vel = new Vector2(0f, -14f * P), Max = 1.3f, Colour = colour, Big = size > 1.3f });
        }

        /// <summary>
        /// A big number bursting out beside the machine: what it won (gold, flying up and out with sparkles) or what it
        /// lost (black, sinking a little in its smoke).
        /// </summary>
        public static void Pop(Vector2 from, long net, float delay = 0f, bool rainbow = false)
        {
            bool lost = net < 0;
            string text = (lost ? "-" : "+") + (System.Math.Abs(net) / 100).ToString("N0");
            float side = Random.value < 0.5f ? -1f : 1f;
            var vel = lost ? new Vector2(side * Random.Range(4f, 10f), -Random.Range(6f, 12f)) * P : new Vector2(side * Random.Range(18f, 34f), -Random.Range(40f, 60f)) * P;
            AddWord(new Word { Text = text, Pos = from, Vel = vel, Max = lost ? 2.4f : rainbow ? 3.6f : 2.2f, Delay = delay, Big = true, Lost = lost, Rainbow = rainbow && !lost,
                Colour = lost ? new Color(0.08f, 0.06f, 0.08f) : new Color(1f, 0.85f, 0.25f) });
            if (lost) Smoke(from, 6);
        }

        static void AddWord(Word w)
        {
            w.Active = true;
            for (int i = 0; i < words.Length; i++)
                if (!words[i].Active) { words[i] = w; return; }
        }

        /// <summary>The round's banner over the machine: popping up big for a win, dropping in grey and shaking for a loss.</summary>
        public static void Banner(string headline, string under, CasinoTier how, Vector2 at)
        {
            head = headline;
            detail = under;
            tier = how;
            bannerAt = at;
            bannerFrom = Time.realtimeSinceStartup;
        }

        public static void Draw(Font font)
        {
            float now = Time.realtimeSinceStartup;
            float dt = Mathf.Clamp(now - last, 0f, 0.05f);
            last = now;
            if (style == null)
            {
                style = new GUIStyle { font = font, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow, normal = { textColor = Color.white } };
                small = new GUIStyle(style) { fontStyle = FontStyle.Normal };
            }
            // An all-in win: bands of every colour sweeping the screen, under everything else.
            float rs = now - rainbowAt;
            if (rs < 5f)
            {
                float a = Mathf.Clamp01(rs * 4f) * Mathf.Clamp01((5f - rs) / 1.5f) * 0.22f;
                int bands = 14;
                float bw = Mathf.Ceil(Screen.width / (float)bands);
                for (int i = 0; i < bands; i++)
                {
                    var c = Color.HSVToRGB(Mathf.Repeat(now * 0.6f + i / (float)bands, 1f), 0.8f, 1f);
                    c.a = a;
                    CasinoPaint.Fill(new Rect(i * bw, 0, bw, Screen.height), c);
                }
            }
            var geo = CasinoPx.Tex("Geo");
            var star5 = CasinoPx.Tex("Star5");
            var star9 = CasinoPx.Tex("Star9");
            var spark = CasinoPx.Tex("Spark");
            var smoke = CasinoPx.Tex("Smoke");
            for (int i = 0; i < bits.Length; i++)
            {
                ref var b = ref bits[i];
                if (!b.Active) continue;
                if (b.Delay > 0f) { b.Delay -= dt; continue; }
                b.Life += dt;
                if (b.Life >= b.Max) { b.Active = false; continue; }
                float t = b.Life / b.Max, fade = Mathf.Clamp01((1f - t) / 0.3f);
                switch (b.Kind)
                {
                    case 0:
                        b.Vel.y += 260f * P * dt;
                        b.Pos += b.Vel * dt;
                        b.Spin += b.Turn * dt;
                        float w = 8f * P * Mathf.Max(0.2f, Mathf.Abs(Mathf.Cos(b.Spin)));
                        CasinoPaint.Image(geo, new Rect(Mathf.Round(b.Pos.x - w / 2f), Mathf.Round(b.Pos.y - 4f * P), w, 8f * P), new Color(1f, 1f, 1f, fade));
                        break;
                    case 1:
                        b.Pos += b.Vel * dt;
                        float pop = Mathf.Sin(t * Mathf.PI), s = (b.Size > 1.2f ? 9f : 5f) * P * Mathf.Max(0.2f, pop) * (b.Size > 2f ? 2f : 1f);
                        CasinoPaint.Image(b.Size > 1.2f ? star9 : star5, new Rect(b.Pos.x - s / 2f, b.Pos.y - s / 2f, s, s), new Color(b.Colour.r, b.Colour.g, b.Colour.b, pop));
                        break;
                    case 2:
                        b.Vel *= Mathf.Exp(-2f * dt);
                        b.Vel.y += 30f * P * dt;
                        b.Pos += b.Vel * dt;
                        float sz = 2f * P * b.Size;
                        CasinoPaint.Image(spark, new Rect(Mathf.Round(b.Pos.x), Mathf.Round(b.Pos.y), sz, sz), new Color(b.Colour.r, b.Colour.g, b.Colour.b, fade));
                        break;
                    default:
                        b.Pos += b.Vel * dt;
                        float sm = Mathf.Lerp(8f, 16f, t) * P;
                        CasinoPaint.Image(smoke, new Rect(b.Pos.x - sm / 2f, b.Pos.y - sm / 2f, sm, sm), new Color(b.Colour.r, b.Colour.g, b.Colour.b, 0.55f * (1f - t)));
                        break;
                }
            }
            for (int i = 0; i < words.Length; i++)
            {
                ref var w = ref words[i];
                if (!w.Active) continue;
                if (w.Delay > 0f) { w.Delay -= dt; continue; }
                w.Life += dt;
                if (w.Life >= w.Max) { w.Active = false; continue; }
                float t = w.Life / w.Max;
                w.Vel *= Mathf.Exp(-(w.Lost ? 1.5f : 2.2f) * dt);
                if (w.Lost) w.Vel.y += 6f * P * dt;
                w.Pos += w.Vel * dt;
                float pop = w.Big ? 1f + 0.5f * Mathf.Exp(-w.Life * 7f) * Mathf.Cos(w.Life * 22f) : 1f;
                float alpha = Mathf.Clamp01((1f - t) / 0.35f);
                style.fontSize = Mathf.RoundToInt((w.Rainbow ? 18f : w.Big ? 12f : 6f) * P * pop);
                var r = new Rect(w.Pos.x - 80f * P, w.Pos.y - 10f * P, 160f * P, 20f * P);
                float o = Mathf.Max(1f, Mathf.Round(P * 0.6f));
                // An outline round it: light round the black, dark round the gold.
                var edge = w.Lost ? new Color(0.85f, 0.85f, 0.9f, alpha) : new Color(0.25f, 0.1f, 0f, alpha);
                for (int k = 0; k < 4; k++)
                {
                    var d = new Vector2(k == 0 ? o : k == 1 ? -o : 0f, k == 2 ? o : k == 3 ? -o : 0f);
                    CasinoPaint.Text(new Rect(r.x + d.x, r.y + d.y, r.width, r.height), w.Text, style, edge);
                }
                var c = w.Rainbow ? Color.HSVToRGB(Mathf.Repeat(w.Life * 1.3f, 1f), 0.6f, 1f) : w.Colour;
                if (!w.Lost && w.Big && !w.Rainbow && Mathf.FloorToInt(w.Life * 10f) % 2 == 0) c = Color.Lerp(c, Color.white, 0.35f);
                c.a = alpha;
                CasinoPaint.Text(r, w.Text, style, c);
            }
            Banner(now);
            if (flash > 0f)
            {
                float a = flash * Mathf.Clamp01(1f - (now - flashAt) / 0.35f);
                if (a <= 0f) flash = 0f;
                else CasinoPaint.Fill(new Rect(0, 0, Screen.width, Screen.height), new Color(1f, 0.95f, 0.8f, a * 0.45f));
            }
        }

        static void Banner(float now)
        {
            float since = now - bannerFrom, stay = tier == CasinoTier.Jackpot ? 4.5f : 3f;
            if (since > stay || string.IsNullOrEmpty(head)) return;
            bool lose = tier == CasinoTier.Lose;
            float alpha = Mathf.Clamp01((stay - since) / 0.5f);
            float scale = lose ? 1f : 1f + 0.35f * Mathf.Exp(-since * 6f) * Mathf.Cos(since * 18f) - 0.35f * Mathf.Clamp01(1f - since * 8f);
            float shake = lose && since < 0.9f ? Mathf.Sin(since * 40f) * 3f * P * (1f - since / 0.9f) : 0f;
            float drop = lose ? Mathf.Lerp(-8f * P, 0f, Mathf.Clamp01(since * 5f)) : 0f;
            float w = (tier == CasinoTier.Jackpot ? 110f : 90f) * P * scale, h = 26f * P * scale;
            var r = new Rect(bannerAt.x - w / 2f + shake, bannerAt.y - h / 2f + drop, w, h);
            var look = CasinoPx.Tex(lose ? "Button4" : tier == CasinoTier.Jackpot ? "Button1" : "Button3");
            CasinoPaint.Pixel9(look, r, 5, P * Mathf.Max(0.6f, scale), new Color(1f, 1f, 1f, alpha));
            Color main = lose ? new Color(0.82f, 0.84f, 0.9f) : tier == CasinoTier.Jackpot ? Color.HSVToRGB(Mathf.Repeat(since * 0.7f, 1f), 0.45f, 1f) : new Color(1f, 0.9f, 0.45f);
            main.a = alpha;
            style.fontSize = Mathf.RoundToInt((tier == CasinoTier.Jackpot ? 11f : 9f) * P * scale);
            small.fontSize = Mathf.RoundToInt(4.6f * P * scale);
            var top = new Rect(r.x, r.y + 2f * P * scale, r.width, 14f * P * scale);
            CasinoPaint.Text(new Rect(top.x + P * 0.7f, top.y + P * 0.7f, top.width, top.height), head, style, new Color(0f, 0f, 0f, 0.8f * alpha));
            CasinoPaint.Text(top, head, style, main);
            if (!string.IsNullOrEmpty(detail))
            {
                var sub = new Rect(r.x, r.y + 15f * P * scale, r.width, 8f * P * scale);
                CasinoPaint.Text(new Rect(sub.x + P * 0.5f, sub.y + P * 0.5f, sub.width, sub.height), detail, small, new Color(0f, 0f, 0f, 0.8f * alpha));
                CasinoPaint.Text(sub, detail, small, new Color(1f, 1f, 1f, 0.9f * alpha));
            }
        }
    }
}
