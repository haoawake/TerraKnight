using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// Terraria's lighting over Hallownest (LightingEngine / LightMap): each frame a light value per
    /// tile (a grid cell) over the view and a margin round it, from the room's own light and every
    /// light source, spread tile by tile as Terraria spreads it (×0.91 a tile through the air, ×0.56
    /// into a solid tile; LightMap.LightDecayThroughAir / ThroughSolid, two passes each way), then
    /// laid over the scene as a dark layer: clear where a tile is fully lit, black where no light
    /// reaches, smooth between. The light reveals what is there rather than painting a ball over it.
    /// The room's own light: the open air of a room the game keeps lit is lit fully, and its terrain
    /// art stays as the game painted it; a tunnel dug into the ground has no light of its own, only
    /// what comes in from its mouth or a torch; a room the game keeps dark (its darkness, which
    /// darkness regions raise, the Lumafly Lantern's rooms) has little light anywhere. Sources: torches
    /// placed or held, the mining helmet, the lantern in the dark, and Terraria's AddLight from shots,
    /// spells, sentries and potions (<see cref="Add"/>). The player, the townsfolk, items and enemies
    /// are lit by it; the weapons' effects and the interface stay over it.
    /// </summary>
    [HarmonyPatch]
    internal sealed class LightMap : MonoBehaviour
    {
        public static bool Enabled = true;
        public static LightMap Instance { get; private set; }

        /// <summary>Over everything of the world but under the weapons' effects (Effects.FxBatch.OnTopOrder).</summary>
        public const int Order = Effects.FxBatch.OnTopOrder - 10;
        const float DecayAir = 0.91f, DecaySolid = 0.56f;
        const int Margin = 24;

        /// <summary>The darkness the game asks for now (HeroController.SetDarkness: the scene's, or a darkness region's).</summary>
        public static int Darkness;
        static readonly List<(Vector2 At, Color Rgb)> added = new List<(Vector2, Color)>();
        static readonly HashSet<Source> sources = new HashSet<Source>();

        /// <summary>Whether the map is drawn this frame (a scene with Terraria's grid, in play).</summary>
        public static bool Active => Enabled && Instance != null && Instance.drawn;

        public static int DevCells, DevSources;
        public static float DevMs;
        public static string DevShader = "";

        MeshRenderer meshRenderer;
        Mesh mesh;
        Material material;
        Texture2D texture;
        Color32[] pixels;
        float[] r, g, b;
        bool[] solid, terrain;
        bool multiply;
        bool drawn;
        int mapX0, mapY0, mapW, mapH;

        /// <summary>
        /// A light that stands somewhere (a placed torch, a held one, the helmet's, a blast's flash):
        /// its Terraria colour, and the glow sprite it had, hidden while the map lights instead. One
        /// that follows its sprite dims as the sprite is faded (a blast's flash).
        /// </summary>
        public sealed class Source : MonoBehaviour
        {
            public Color Rgb;
            public bool FollowSprite;
            SpriteRenderer sprite;
            float baseLevel = -1f;

            void OnEnable() { sources.Add(this); sprite = GetComponent<SpriteRenderer>(); }
            void OnDisable() => sources.Remove(this);

            /// <summary>Its light now.</summary>
            public Color Now()
            {
                if (!FollowSprite || sprite == null) return Rgb;
                float level = Level(sprite.color);
                if (baseLevel < 0f) baseLevel = Mathf.Max(level, 1e-4f);
                float k = Mathf.Clamp01(level / baseLevel);
                return new Color(Rgb.r * k, Rgb.g * k, Rgb.b * k);
            }

            static float Level(Color c) => Mathf.Max(c.r, Mathf.Max(c.g, c.b)) * c.a;

            /// <summary>Its glow sprite off while the map draws the light, back on otherwise.</summary>
            public void ShowSprite(bool on)
            {
                if (sprite != null && sprite.enabled != on) sprite.enabled = on;
            }
        }

        /// <summary>A glow sprite as a light of the map: its Terraria colour; one that follows its sprite dims with it.</summary>
        public static Source Attach(SpriteRenderer sr, Color rgb, bool followSprite = false)
        {
            var s = sr.gameObject.AddComponent<Source>();
            s.Rgb = rgb;
            s.FollowSprite = followSprite;
            return s;
        }

        public static LightMap Create(Transform parent)
        {
            var go = new GameObject("Terra Light Map");
            go.transform.SetParent(parent, false);
            Instance = go.AddComponent<LightMap>();
            return Instance;
        }

        /// <summary>Lighting.AddLight: a light this frame at a point, Terraria's colour (1 a channel is a torch's full light).</summary>
        public static void Add(Vector2 world, Color rgb)
        {
            if (rgb.r + rgb.g + rgb.b <= 0.001f) return;
            added.Add((world, rgb));
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.SetDarkness))]
        static void DarknessSet(int darkness) => Darkness = darkness;

        /// <summary>The light of the room's own open air: full in a room the game keeps lit, little in a dark one.</summary>
        public static float Ambient => (Darkness <= 0 ? 1f : Darkness == 1 ? 0.45f : 0.08f) * Items.Tombstones.LightFactor;

        void Awake()
        {
            mesh = new Mesh { name = "Terra Light Map" };
            mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sortingOrder = Order;
            // Terraria's colour times the scene's where the game has a multiplying shader; else a black veil by brightness.
            foreach (var name in new[] { "Legacy Shaders/Particles/Multiply", "Particles/Multiply", "Mobile/Particles/Multiply" })
            {
                var sh = Shader.Find(name);
                if (sh == null) continue;
                material = new Material(sh) { name = "Terra Light Map (" + name + ")" };
                multiply = true;
                break;
            }
            if (material == null) material = new Material(Shader.Find("Sprites/Default")) { name = "Terra Light Map (veil)" };
            DevShader = material.name;
            meshRenderer.sharedMaterial = material;
            meshRenderer.enabled = false;
            Plugin.Log.LogInfo($"[光照] 光照图材质 {material.name}；排序层 {string.Join(" ", System.Array.ConvertAll(SortingLayer.layers, l => $"{l.name}={l.value}"))}");
        }

        void LateUpdate()
        {
            drawn = false;
            var region = TerrainSystem.Current;
            var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
            var hero = HeroController.SilentInstance;
            var gm = GameManager.UnsafeInstance;
            bool play = Enabled && region != null && cam != null && hero != null && GameHooks.IsGameplay(gm);
            foreach (var s in sources) if (s != null) s.ShowSprite(!play);
            if (!play)
            {
                if (meshRenderer.enabled) meshRenderer.enabled = false;
                added.Clear();
                return;
            }
            var t0 = Time.realtimeSinceStartup;

            // The view on the play plane, and a margin for lights beyond it to spread in from.
            Vector2 lo = World(cam, 0f, 0f), hi = World(cam, 1f, 1f);
            var c0 = CellGrid.WorldToCell(Vector2.Min(lo, hi));
            var c1 = CellGrid.WorldToCell(Vector2.Max(lo, hi));
            int x0 = c0.x - Margin, y0 = c0.y - Margin;
            int w = Mathf.Clamp(c1.x - c0.x + 1 + 2 * Margin, 8, 512), h = Mathf.Clamp(c1.y - c0.y + 1 + 2 * Margin, 8, 512);
            int n = w * h;
            mapX0 = x0; mapY0 = y0; mapW = w; mapH = h;
            if (r == null || r.Length < n)
            {
                r = new float[n]; g = new float[n]; b = new float[n];
                solid = new bool[n]; terrain = new bool[n];
                pixels = new Color32[n];
            }
            if (texture == null || texture.width != w || texture.height != h)
            {
                if (texture != null) Destroy(texture);
                texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "Terra Light Map" };
                material.mainTexture = texture;
            }

            // The room's own light in its open air (and in the void past its edges); none in dug tunnels and blocks.
            float amb = Ambient;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x, gx = x0 + x, gy = y0 + y;
                    var cellRegion = TerraWorld.RegionAt(gx, gy);
                    var kind = CellKind.Empty;
                    bool dug = false;
                    if (cellRegion != null)
                    {
                        int lx = gx - cellRegion.CellOffset.x, ly = gy - cellRegion.CellOffset.y;
                        kind = cellRegion.Grid.Get(lx, ly);
                        // A dug cell with no wall behind it (a spike's cell mined out of the air, a tunnel's wall hammered off) is open air.
                        dug = cellRegion.Grid.IsDug(lx, ly) && cellRegion.Grid.GetWall(lx, ly) != 0;
                    }
                    bool isSolid = Cells.Blocks(kind);
                    solid[i] = isSolid;
                    terrain[i] = kind == CellKind.Original && !dug;
                    float s = !isSolid && !dug ? amb : 0f;
                    r[i] = g[i] = b[i] = s;
                    // A placed torch: TorchID.TorchColor of the plain torch, a regional one's own colour (World.RegionalTorches).
                    if (kind == CellKind.Torch) Put(i, RegionalTorches.LightRgb(cellRegion.Grid.GetTorchStyle(gx - cellRegion.CellOffset.x, gy - cellRegion.CellOffset.y)));
                    // TileLightScanner's Meteorite: (0.56, 0.43, 0.15), five in a hundred either way.
                    else if (kind == CellKind.Meteorite) Put(i, MeteoriteRgb * Random.Range(0.95f, 1.05f));
                }
            int count = 0;
            foreach (var s in sources)
            {
                if (s == null || !s.isActiveAndEnabled) continue;
                PutAt(s.transform.position, s.Now(), x0, y0, w, h);
                count++;
            }
            foreach (var (at, rgb) in added) { PutAt(at, rgb, x0, y0, w, h); count++; }
            added.Clear();
            // The Lumafly Lantern in hand: Terraria's brightest light, the Suspicious Looking Tentacle's (Projectile.AI for
            // 650: (0.5, 0.9, 1) × 2 cast across the player, CastLightOpen), wherever it is.
            // In the dark the Knight still sees a little round itself, as the game's vignette lets it;
            // the lantern's room light (the mining helmet's, Night Owl's) much more.
            var pd = PlayerData.instance;
            var heroBox = hero.GetComponent<BoxCollider2D>().bounds;
            if (LanternHeld)
            {
                for (float x = heroBox.min.x; x <= heroBox.max.x + 0.01f; x += CellGrid.CellSize)
                    PutAt(new Vector2(Mathf.Min(x, heroBox.max.x), heroBox.center.y), LanternRgb, x0, y0, w, h);
                count++;
            }
            else if (Darkness > 0)
            {
                // Through GetBool: the mining helmet and Night Owl count as the lantern too (PlayerHooks).
                bool lantern = pd != null && pd.GetBool("hasLantern");
                PutAt(hero.GetComponent<BoxCollider2D>().bounds.center, lantern ? new Color(0.9f, 0.92f, 1f) : new Color(0.45f, 0.45f, 0.5f), x0, y0, w, h);
                count++;
            }

            // LightMap.Blur: each way along the rows, then the columns, twice.
            for (int pass = 0; pass < 2; pass++)
            {
                for (int y = 0; y < h; y++)
                {
                    int row = y * w;
                    for (int x = 1; x < w; x++) Spread(row + x - 1, row + x);
                    for (int x = w - 2; x >= 0; x--) Spread(row + x + 1, row + x);
                }
                for (int x = 0; x < w; x++)
                {
                    for (int y = 1; y < h; y++) Spread((y - 1) * w + x, y * w + x);
                    for (int y = h - 2; y >= 0; y--) Spread((y + 1) * w + x, y * w + x);
                }
            }

            // What it shows: the game's terrain art as lit as the room, the rest by its light.
            bool any = false;
            for (int i = 0; i < n; i++)
            {
                float lr = r[i], lg = g[i], lb = b[i];
                if (terrain[i]) { lr = Mathf.Max(lr, amb); lg = Mathf.Max(lg, amb); lb = Mathf.Max(lb, amb); }
                lr = Mathf.Clamp01(lr); lg = Mathf.Clamp01(lg); lb = Mathf.Clamp01(lb);
                if (lr < 0.995f || lg < 0.995f || lb < 0.995f) any = true;
                pixels[i] = multiply
                    ? new Color32((byte)(lr * 255f), (byte)(lg * 255f), (byte)(lb * 255f), 255)
                    : new Color32(0, 0, 0, (byte)((1f - Mathf.Max(lr, Mathf.Max(lg, lb))) * 255f));
            }
            // Fully lit everywhere (a lit room with nothing dug in view): nothing to lay over it.
            if (!any)
            {
                if (meshRenderer.enabled) meshRenderer.enabled = false;
                drawn = true;
                Stats(n, count, t0);
                return;
            }
            texture.SetPixels32(0, 0, w, h, pixels, 0);
            texture.Apply(false);
            const float c = CellGrid.CellSize;
            float wx0 = x0 * c, wy0 = y0 * c, wx1 = (x0 + w) * c, wy1 = (y0 + h) * c;
            mesh.Clear();
            var tr = transform;
            mesh.vertices = new[]
            {
                tr.InverseTransformPoint(new Vector3(wx0, wy0, 0f)), tr.InverseTransformPoint(new Vector3(wx1, wy0, 0f)),
                tr.InverseTransformPoint(new Vector3(wx0, wy1, 0f)), tr.InverseTransformPoint(new Vector3(wx1, wy1, 0f)),
            };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) };
            mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            mesh.RecalculateBounds();
            if (!meshRenderer.enabled) meshRenderer.enabled = true;
            drawn = true;
            Stats(n, count, t0);
        }

        /// <summary>Dev: the light shown at a point last frame (its brightest channel); -1 off the map or with the map off.</summary>
        public static float DevLightAt(Vector2 world)
        {
            var m = Instance;
            if (m == null || m.r == null || !m.drawn) return -1f;
            var cell = CellGrid.WorldToCell(world);
            int x = cell.x - m.mapX0, y = cell.y - m.mapY0;
            if (x < 0 || y < 0 || x >= m.mapW || y >= m.mapH) return -1f;
            int i = y * m.mapW + x;
            float v = Mathf.Max(m.r[i], Mathf.Max(m.g[i], m.b[i]));
            if (m.terrain[i]) v = Mathf.Max(v, Ambient);
            return Mathf.Clamp01(v);
        }

        /// <summary>Dev: the dark layer is laid over the scene now.</summary>
        public static bool DevOverlay => Instance != null && Instance.meshRenderer != null && Instance.meshRenderer.enabled;

        /// <summary>Dev: glow sprites of lights showing now (with the map on, none should be).</summary>
        public static int DevSpritesShown
        {
            get
            {
                int n = 0;
                foreach (var s in sources)
                    if (s != null && s.isActiveAndEnabled && s.GetComponent<SpriteRenderer>() is SpriteRenderer sr && sr.enabled) n++;
                return n;
            }
        }

        static void Stats(int cells, int sourcesNow, float t0)
        {
            DevCells = cells;
            DevSources = sourcesNow;
            DevMs = (Time.realtimeSinceStartup - t0) * 1000f;
        }

        /// <summary>The Lumafly Lantern held: the Suspicious Looking Tentacle's light, Terraria's brightest.</summary>
        public static readonly Color LanternRgb = new Color(1f, 1.8f, 2f);

        /// <summary>The Lumafly Lantern is the item in hand.</summary>
        public static bool LanternHeld
        {
            get
            {
                var mod = TerraMod.Instance;
                if (mod == null || mod.Inventory == null) return false;
                var held = mod.Inventory.Current;
                return !held.IsEmpty && held.Def != null && held.Def.Type == Items.ItemType.HkLumaflyLantern;
            }
        }

        /// <summary>The plain torch's light (TorchID.TorchColor 0).</summary>
        public static readonly Color TorchRgb = new Color(1f, 0.95f, 0.8f);
        public static readonly Color MeteoriteRgb = new Color(0.56f, 0.43f, 0.15f);

        void Put(int i, Color rgb)
        {
            if (rgb.r > r[i]) r[i] = rgb.r;
            if (rgb.g > g[i]) g[i] = rgb.g;
            if (rgb.b > b[i]) b[i] = rgb.b;
        }

        void PutAt(Vector2 world, Color rgb, int x0, int y0, int w, int h)
        {
            var cell = CellGrid.WorldToCell(world);
            int x = cell.x - x0, y = cell.y - y0;
            if (x < 0 || y < 0 || x >= w || y >= h) return;
            Put(y * w + x, rgb);
        }

        /// <summary>
        /// Light from one tile into the next (LightMap.BlurLine): lessened by the tile it passes
        /// through, so the face of a block beside the light is lit almost as the air, the one
        /// behind it much less.
        /// </summary>
        void Spread(int from, int to)
        {
            float d = solid[from] ? DecaySolid : DecayAir;
            float v = r[from] * d; if (v > r[to]) r[to] = v;
            v = g[from] * d; if (v > g[to]) g[to] = v;
            v = b[from] * d; if (v > b[to]) b[to] = v;
        }

        /// <summary>A viewport point on the play plane (z = 0) of the perspective camera.</summary>
        static Vector2 World(Camera cam, float vx, float vy)
        {
            var ray = cam.ViewportPointToRay(new Vector3(vx, vy, 0f));
            if (Mathf.Abs(ray.direction.z) < 1e-5f) return cam.transform.position;
            float t = -ray.origin.z / ray.direction.z;
            return ray.origin + ray.direction * t;
        }
    }
}
