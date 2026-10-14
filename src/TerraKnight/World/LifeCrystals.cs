using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// Life Crystals lying in the early areas, as Terraria's caves have them (WorldGen.AddLifeCrystal):
    /// each save picks its own twelve scenes of King's Pass, the Crossroads, Greenpath and the
    /// Howling Cliffs, and in each, the first time it is loaded, a spot on two cells of even ground
    /// with room above, reachable from the scene's gates and away from spikes, acid and water. The
    /// crystal is drawn from its tile (12, ten frames of six ticks); a pickaxe knocks it out as a
    /// Life Crystal, and so does digging away the ground under it. Kept in the mod's save.
    /// </summary>
    internal sealed class LifeCrystals : MonoBehaviour
    {
        public const int PerSave = 12;
        const float Px = Effects.TerraParticles.Px;
        const float Z = 0.0045f;
        const float Cell = CellGrid.CellSize;

        /// <summary>The early scenes a crystal may lie in: the boss arenas are left out.</summary>
        static readonly string[] Prefixes = { "Tutorial_01", "Crossroads_", "Fungus1_", "Cliffs_" };
        static readonly HashSet<string> Arenas = new HashSet<string> { "Crossroads_04", "Crossroads_09", "Crossroads_10", "Fungus1_04", "Fungus1_20_v02", "Fungus1_35" };

        sealed class Crystal
        {
            public string Scene;
            /// <summary>Bottom middle, in the scene's own coordinates; null until the scene has been seen.</summary>
            public Vector2? Local;
            public bool Taken;
            public int Damage;
            public SpriteRenderer Renderer;
        }

        /// <summary>What the save keeps of the save's crystals.</summary>
        public struct Saved
        {
            public string Scene;
            public bool Placed, Taken;
            public Vector2 Local;
        }

        public static LifeCrystals Instance { get; private set; }
        /// <summary>The save's own: which scenes and which candidates come next for one that has no room.</summary>
        public static int Seed { get; private set; }
        static readonly List<Crystal> crystals = new List<Crystal>();
        static Sprite[] frames;
        float ticks;
        int frame, frameTicks;

        public static int DevBroken;

        public static LifeCrystals Create(Transform parent)
        {
            var go = new GameObject("Terra Life Crystals");
            go.transform.SetParent(parent, false);
            Instance = go.AddComponent<LifeCrystals>();
            return Instance;
        }

        // ------------------------------------------------------------------ the save's crystals

        static List<string> Candidates()
        {
            var list = new List<string>();
            foreach (var s in UI.WorldMap.DevSceneNames())
            {
                if (Arenas.Contains(s)) continue;
                foreach (var p in Prefixes) if (s.StartsWith(p)) { list.Add(s); break; }
            }
            list.Sort(System.StringComparer.Ordinal);
            return list;
        }

        /// <summary>The candidates in this save's own order: the first twelve get a crystal, the rest stand in for one with no room.</summary>
        static List<string> Order(int seed)
        {
            var list = Candidates();
            var rng = new System.Random(seed);
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
            return list;
        }

        /// <summary>A new game (or a save from before the crystals): its own twelve scenes.</summary>
        public static void NewWorld(int seed = 0)
        {
            Clear();
            Seed = seed != 0 ? seed : Random.Range(1, int.MaxValue);
            var order = Order(Seed);
            for (int i = 0; i < PerSave && i < order.Count; i++) crystals.Add(new Crystal { Scene = order[i] });
            Plugin.Log.LogInfo($"[生命水晶] 这个存档的 {crystals.Count} 个生命水晶在：{string.Join("、", Scenes())}");
        }

        static IEnumerable<string> Scenes()
        {
            foreach (var c in crystals) yield return c.Scene;
        }

        public static void Clear()
        {
            foreach (var c in crystals) if (c.Renderer != null) Destroy(c.Renderer.gameObject);
            crystals.Clear();
            Seed = 0;
        }

        public static List<Saved> Save()
        {
            var list = new List<Saved>(crystals.Count);
            foreach (var c in crystals) list.Add(new Saved { Scene = c.Scene, Placed = c.Local.HasValue, Local = c.Local ?? default, Taken = c.Taken });
            return list;
        }

        public static void Load(int seed, IEnumerable<Saved> saved)
        {
            Clear();
            if (seed == 0 || saved == null)
            {
                NewWorld();
                return;
            }
            Seed = seed;
            foreach (var s in saved)
                crystals.Add(new Crystal { Scene = s.Scene, Local = s.Placed ? s.Local : (Vector2?)null, Taken = s.Taken });
        }

        // ------------------------------------------------------------------ finding a spot

        /// <summary>
        /// A spot in a loaded scene: two solid cells side by side with two empty cells over each
        /// (AddLifeCrystal's EmptyTileCheck and solid floor), the crystal's cells joined to a gate of
        /// the scene through open cells, and no hazard or water within two cells. One of them at
        /// random, the same for the same save.
        /// </summary>
        static bool FindSpot(TerrainRegion region, int seed, out Vector2 local)
        {
            local = default;
            var grid = region.Grid;
            int w = grid.Width, h = grid.Height;
            bool Open(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && !Cells.Blocks(grid.Get(x, y));
            bool Solid(int x, int y) => x >= 0 && y >= 0 && x < w && y < h && Cells.Blocks(grid.Get(x, y));

            // Open cells reached from the gates.
            var reached = new bool[w * h];
            var queue = new Queue<Vector2Int>();
            var scene = region.Root.gameObject.scene;
            foreach (var gate in InScene<TransitionPoint>(scene, true))
            {
                var col = gate.GetComponent<Collider2D>();
                Vector2 at = col != null ? (Vector2)col.bounds.center : (Vector2)gate.transform.position;
                var c = CellGrid.WorldToCell(at) - region.CellOffset;
                for (int dy = -3; dy <= 3; dy++)
                    for (int dx = -3; dx <= 3; dx++)
                    {
                        int x = c.x + dx, y = c.y + dy;
                        if (!Open(x, y) || reached[y * w + x]) continue;
                        reached[y * w + x] = true;
                        queue.Enqueue(new Vector2Int(x, y));
                    }
            }
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var d in new[] { Vector2Int.left, Vector2Int.right, Vector2Int.up, Vector2Int.down })
                {
                    int x = c.x + d.x, y = c.y + d.y;
                    if (!Open(x, y) || reached[y * w + x]) continue;
                    reached[y * w + x] = true;
                    queue.Enqueue(new Vector2Int(x, y));
                }
            }

            // Spikes, acid, lava and water, two cells round.
            var bad = new List<Rect>();
            foreach (var dh in InScene<DamageHero>(scene, false))
            {
                var col = dh.GetComponent<Collider2D>();
                if (col != null && dh.hazardType >= 2 && dh.hazardType <= 4) bad.Add(Grow(col.bounds));
            }
            foreach (var col in InScene<Collider2D>(scene, false))
                if (col.gameObject.layer == 4) bad.Add(Grow(col.bounds));

            var spots = new List<Vector2>();
            for (int y = 0; y < h - 3; y++)
                for (int x = 1; x < w - 2; x++)
                {
                    if (!Solid(x, y) || !Solid(x + 1, y)) continue;
                    if (grid.Get(x, y + 1) != CellKind.Empty || grid.Get(x + 1, y + 1) != CellKind.Empty
                        || grid.Get(x, y + 2) != CellKind.Empty || grid.Get(x + 1, y + 2) != CellKind.Empty) continue;
                    if (!reached[(y + 1) * w + x] || !reached[(y + 1) * w + x + 1]) continue;
                    var bottom = new Vector2((region.CellOffset.x + x + 1) * Cell, (region.CellOffset.y + y + 1) * Cell);
                    var box = new Rect(bottom.x - Cell, bottom.y, 2f * Cell, 2f * Cell);
                    bool near = false;
                    foreach (var r in bad) if (r.Overlaps(box)) { near = true; break; }
                    if (!near) spots.Add(bottom);
                }
            if (spots.Count == 0) return false;
            var rng = new System.Random(seed ^ Hash(region.SceneName));
            local = spots[rng.Next(spots.Count)] - (Vector2)region.Root.position;
            return true;
        }

        static IEnumerable<T> InScene<T>(UnityEngine.SceneManagement.Scene scene, bool inactive) where T : Component
        {
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<T>(inactive)) yield return t;
        }

        static Rect Grow(Bounds b) => Rect.MinMaxRect(b.min.x - 2f * Cell, b.min.y - 2f * Cell, b.max.x + 2f * Cell, b.max.y + 2f * Cell);

        /// <summary>FNV-1a: the same for a name in every run (string.GetHashCode needn't be).</summary>
        static int Hash(string s)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char ch in s) { h ^= ch; h *= 16777619; }
                return (int)h;
            }
        }

        /// <summary>A scene with no room for its crystal hands it on to the next candidate of the save's order not yet holding one.</summary>
        static void HandOn(Crystal c)
        {
            var have = new HashSet<string>(Scenes());
            var tried = TriedScenes;
            tried.Add(c.Scene);
            foreach (var s in Order(Seed))
            {
                if (have.Contains(s) || tried.Contains(s)) continue;
                Plugin.Log.LogInfo($"[生命水晶] {c.Scene} 里找不到 2 格平地放生命水晶，改放到 {s}");
                c.Scene = s;
                return;
            }
            Plugin.Log.LogWarning($"[生命水晶] {c.Scene} 里找不到地方，也没有别的场景可换了");
            c.Taken = true;
        }

        /// <summary>Scenes found with no room this session (a save keeps only where crystals are).</summary>
        static readonly HashSet<string> TriedScenes = new HashSet<string>();

        // ------------------------------------------------------------------ breaking

        static Rect Box(Crystal c, TerrainRegion region)
        {
            var at = (Vector2)region.Root.position + c.Local.Value;
            return new Rect(at.x - Cell, at.y, 2f * Cell, 2f * Cell);
        }

        static Rect Reach(HeroController hero)
        {
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            return Rect.MinMaxRect(b.min.x - 5f * Cell, b.min.y - 4f * Cell, b.max.x + 5f * Cell, b.max.y + 4f * Cell);
        }

        /// <summary>A pickaxe swung at a crystal in reach: pick power until 100 knocks it out. True when the swing was at one.</summary>
        public static bool TryMine(ItemDef pick, Vector2 mouse, HeroController hero)
        {
            if (pick == null || pick.PickPower <= 0 || hero == null) return false;
            var reach = Reach(hero);
            foreach (var c in crystals)
            {
                if (c.Taken || !c.Local.HasValue) continue;
                var region = TerrainSystem.Find(c.Scene);
                if (region == null) continue;
                var box = Box(c, region);
                if (!box.Contains(mouse) || !reach.Overlaps(box)) continue;
                c.Damage += pick.PickPower;
                TerraMod.Instance?.PlaySound("Dig_" + Random.Range(0, 3));
                if (c.Damage < 100)
                {
                    Dust(box, 3);
                    return true;
                }
                Break(c, box);
                return true;
            }
            return false;
        }

        static void Break(Crystal c, Rect box)
        {
            Dust(box, 10);
            WorldItems.Drop(ItemStack.Of(ItemType.LifeCrystal), box.center);
            c.Taken = true;
            if (c.Renderer != null) c.Renderer.enabled = false;
            DevBroken++;
            Plugin.Log.LogInfo($"[生命水晶] {c.Scene} 的生命水晶打下来了（这个存档还剩 {Left} 个）");
        }

        static void Dust(Rect box, int n)
        {
            var p = Effects.TerraParticles.Instance;
            if (p == null) return;
            var px = Effects.TerraParticles.ToPx(box.center);
            // KillTile_MakeTileDust: tile 12's dust is 12.
            for (int k = 0; k < n; k++) p.NewDust(px - new Vector2(16f, 16f), 32, 32, 12);
        }

        public static int Left
        {
            get
            {
                int n = 0;
                foreach (var c in crystals) if (!c.Taken) n++;
                return n;
            }
        }

        // ------------------------------------------------------------------ each frame

        void Update()
        {
            if (TerrainSystem.Current == null || crystals.Count == 0) return;
            // Main.AnimateTiles: tile 12 steps a frame every 6 ticks through 10.
            ticks += Mathf.Min(Time.deltaTime, 0.1f) * 60f;
            while (ticks >= 1f)
            {
                ticks -= 1f;
                foreach (var c in crystals) if (c.Damage > 0 && Random.Range(0, 60) == 0) c.Damage = 0;
                if (++frameTicks > 5) { frameTicks = 0; frame = (frame + 1) % 10; }
            }
            foreach (var c in crystals)
            {
                if (c.Taken) continue;
                var region = TerrainSystem.Find(c.Scene);
                if (region == null)
                {
                    if (c.Renderer != null) c.Renderer.enabled = false;
                    continue;
                }
                if (!c.Local.HasValue)
                {
                    if (FindSpot(region, Seed, out var local))
                    {
                        c.Local = local;
                        Plugin.Log.LogInfo($"[生命水晶] {c.Scene} 的生命水晶放在 {local:F1}（场景坐标）");
                    }
                    else
                    {
                        HandOn(c);
                        continue;
                    }
                }
                var box = Box(c, region);
                // The ground dug away under it: it falls out, as a tile 12 does when its floor goes (WorldGen.CheckTile).
                var g = CellGrid.WorldToCell(new Vector2(box.xMin + Cell * 0.5f, box.yMin - Cell * 0.5f));
                if (!TerraWorld.IsSolid(g.x, g.y) && !TerraWorld.IsSolid(g.x + 1, g.y) && TerraWorld.InWorld(g.x, g.y))
                {
                    Break(c, box);
                    continue;
                }
                if (c.Renderer == null)
                {
                    var go = new GameObject("Terra Life Crystal");
                    go.transform.SetParent(region.Root, false);
                    c.Renderer = go.AddComponent<SpriteRenderer>();
                }
                if (c.Renderer.transform.parent != region.Root) c.Renderer.transform.SetParent(region.Root, false);
                var sprites = Frames();
                c.Renderer.enabled = sprites != null;
                if (sprites == null) continue;
                c.Renderer.sprite = sprites[frame % sprites.Length];
                c.Renderer.transform.localPosition = new Vector3(c.Local.Value.x, c.Local.Value.y, Z - region.Root.position.z);
            }
        }

        /// <summary>Tile 12's frames: 2x2 tiles 18 px apart, each frame 36 px below the last.</summary>
        static Sprite[] Frames()
        {
            if (frames != null) return frames.Length > 0 ? frames : null;
            Color32[] src;
            int sw, sh;
            try { src = TerrariaAssets.Pixels("Tiles_12", out sw, out sh); }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[生命水晶] 读不到 Tiles_12：{e.Message}");
                frames = new Sprite[0];
                return null;
            }
            int count = Mathf.Max(1, Mathf.Min(10, sh / 36));
            frames = new Sprite[count];
            for (int f = 0; f < count; f++)
            {
                var dst = new Color32[32 * 32];
                for (int r = 0; r < 2; r++)
                    for (int col = 0; col < 2; col++)
                        for (int y = 0; y < 16; y++)
                            for (int x = 0; x < 16; x++)
                            {
                                int sx = col * 18 + x, sy = f * 36 + r * 18 + y;
                                if (sx >= sw || sy >= sh) continue;
                                dst[(31 - (r * 16 + y)) * 32 + col * 16 + x] = src[(sh - 1 - sy) * sw + sx];
                            }
                var tex = TerrariaAssets.Make("Tiles_12#crystal" + f, dst, 32, 32);
                frames[f] = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0f), 1f / Px, 0, SpriteMeshType.FullRect);
            }
            Plugin.Log.LogInfo($"[生命水晶] Tiles_12 {sw}x{sh}，{count} 帧");
            return frames;
        }

        // ------------------------------------------------------------------ dev

        public static string DevDescribe()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var c in crystals) sb.Append($" {c.Scene}{(c.Taken ? "(已拿)" : c.Local.HasValue ? $"@{c.Local.Value:F1}" : "(未定点)")}");
            return sb.ToString();
        }

        /// <summary>Dev: the world box of a scene's crystal, if it stands there now.</summary>
        public static Rect? DevBox(string scene)
        {
            foreach (var c in crystals)
            {
                if (c.Scene != scene || c.Taken || !c.Local.HasValue) continue;
                var region = TerrainSystem.Find(scene);
                if (region != null) return Box(c, region);
            }
            return null;
        }

        public static List<string> DevScenes() => new List<string>(Scenes());
    }
}
