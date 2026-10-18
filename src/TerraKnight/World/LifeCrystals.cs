using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// Life Crystals lying in the world, as Terraria's caves have them (WorldGen.AddLifeCrystal):
    /// each save picks its own 24 scenes of the first two stages, outside the boss rooms (King's Pass,
    /// the Crossroads and Greenpath; the Fungal Wastes, Fog Canyon and the Howling Cliffs: past them
    /// the player has the life already), and in each, the first time it is loaded, a spot on
    /// two cells of even ground with room above, reachable from the scene's gates and away from
    /// spikes, acid and water. The crystal is drawn from its tile (12, ten frames of six ticks); a
    /// pickaxe knocks it out as a Life Crystal, and so does digging away the ground under it. Kept
    /// in the mod's save; a save with fewer is given the rest, and one still lying in a later stage's
    /// scene (from 2.4.1) is moved to one of the first two. How many the player can eat is
    /// <see cref="Allowance"/>: the ground gives more crystals than that, the rest of the fifteen
    /// open with masks and bosses.
    /// </summary>
    internal sealed partial class LifeCrystals : MonoBehaviour
    {
        public const int PerSave = 24;

        /// <summary>Crystals eaten without a mask or a boss to show for it.</summary>
        public const int GroundAllowance = 6;

        /// <summary>
        /// How many Life Crystals the player can have eaten by now: six, one more for each boss beaten
        /// (the Bestiary's kills), up to Terraria's fifteen. The Knight's masks are life of their own
        /// (TerraStats.MaskLife), not crystals.
        /// </summary>
        public static int Allowance()
        {
            int bosses = 0;
            foreach (var r in EnemyDrops.BossRecords()) if (Bestiary.IsDefeated(r)) bosses++;
            return Mathf.Min(Player.TerraStats.MaxLifeCrystals, GroundAllowance + bosses);
        }
        const float Px = Effects.TerraParticles.Px;
        // Behind the minions, pets and shots (FxBatch.BehindZ 0.0047), the townsfolk and the player, as the furniture (Stations.Z).
        const float Z = 0.00475f;
        const float Cell = CellGrid.CellSize;

        /// <summary>The stages whose scenes a crystal may lie in (Areas.LevelOf): the first two.</summary>
        public const int LastStage = 1;
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
        /// <summary>Dev: the save's crystals (lying, placed or not, and taken).</summary>
        public static int DevCount => crystals.Count;

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
                if (Arenas.Contains(s) || BossRooms.IsRoom(s) || s.StartsWith("GG_") || s.StartsWith("Dream_") || s.StartsWith("Room_")) continue;
                if (Areas.LevelOf(s) <= LastStage) list.Add(s);
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

        /// <summary>Where the crystals lie that are drawn now (the Metal Detector's treasure).</summary>
        public static IEnumerable<Vector2> Lying()
        {
            foreach (var c in crystals)
                if (!c.Taken && c.Renderer != null && c.Renderer.enabled && c.Renderer.gameObject.activeInHierarchy) yield return c.Renderer.transform.position;
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
            var have = new HashSet<string>(Scenes());
            // One still lying in a later stage's scene (2.4.1 spread them further): to a scene of the first two.
            int moved = 0;
            var order = Order(Seed);
            int next = 0;
            foreach (var c in crystals)
            {
                if (c.Taken || Areas.LevelOf(c.Scene) <= LastStage) continue;
                while (next < order.Count && have.Contains(order[next])) next++;
                if (next >= order.Count) break;
                have.Remove(c.Scene);
                c.Scene = order[next];
                c.Local = null;
                have.Add(c.Scene);
                moved++;
            }
            if (moved > 0) Plugin.Log.LogInfo($"[生命水晶] {moved} 个还没挖的生命水晶从后面阶段的场景挪到前两个阶段：{string.Join("、", Scenes())}");
            // A save from before there were 24: the rest from its own order, past the scenes it has.
            if (crystals.Count >= PerSave) return;
            int before = crystals.Count;
            foreach (var scene in order)
            {
                if (crystals.Count >= PerSave) break;
                if (have.Add(scene)) crystals.Add(new Crystal { Scene = scene });
            }
            Plugin.Log.LogInfo($"[生命水晶] 存档里有 {before} 个，补上 {crystals.Count - before} 个：{string.Join("、", Scenes())}");
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
            var spots = GroundSpots(region, 2, 2);
            if (spots.Count == 0) return false;
            var rng = new System.Random(seed ^ Hash(region.SceneName));
            local = spots[rng.Next(spots.Count)] - (Vector2)region.Root.position;
            return true;
        }

        /// <summary>
        /// The spots of a loaded scene for a thing two cells wide (its bottom middle, world coordinates): two solid cells
        /// side by side with this many empty cells over each, joined to a gate of the scene through open cells, and no
        /// spikes, acid, lava or water this many cells round.
        /// </summary>
        internal static List<Vector2> GroundSpots(TerrainRegion region, int headroom, int margin)
        {
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
                if (col != null && dh.hazardType >= 2 && dh.hazardType <= 4) bad.Add(Grow(col.bounds, margin));
            }
            foreach (var col in InScene<Collider2D>(scene, false))
                if (col.gameObject.layer == 4) bad.Add(Grow(col.bounds, margin));

            var spots = new List<Vector2>();
            for (int y = 0; y < h - 1 - headroom; y++)
                for (int x = 1; x < w - 2; x++)
                {
                    if (!Solid(x, y) || !Solid(x + 1, y)) continue;
                    bool room = true;
                    for (int dy = 1; dy <= headroom && room; dy++)
                        room = grid.Get(x, y + dy) == CellKind.Empty && grid.Get(x + 1, y + dy) == CellKind.Empty;
                    if (!room) continue;
                    if (!reached[(y + 1) * w + x] || !reached[(y + 1) * w + x + 1]) continue;
                    var bottom = new Vector2((region.CellOffset.x + x + 1) * Cell, (region.CellOffset.y + y + 1) * Cell);
                    var box = new Rect(bottom.x - Cell, bottom.y, 2f * Cell, 2f * Cell);
                    bool near = false;
                    foreach (var r in bad) if (r.Overlaps(box)) { near = true; break; }
                    if (!near) spots.Add(bottom);
                }
            return spots;
        }

        static IEnumerable<T> InScene<T>(UnityEngine.SceneManagement.Scene scene, bool inactive) where T : Component
        {
            if (!scene.IsValid() || !scene.isLoaded) yield break;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<T>(inactive)) yield return t;
        }

        static Rect Grow(Bounds b, int cells) => Rect.MinMaxRect(b.min.x - cells * Cell, b.min.y - cells * Cell, b.max.x + cells * Cell, b.max.y + cells * Cell);

        /// <summary>FNV-1a: the same for a name in every run (string.GetHashCode needn't be).</summary>
        internal static int Hash(string s)
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
            if (!Net.WorldSync.CanInteract) return false;
            if (pick == null || pick.PickPower <= 0 || hero == null) return false;
            var reach = Reach(hero);
            foreach (var c in crystals)
            {
                if (c.Taken || !c.Local.HasValue) continue;
                var region = TerrainSystem.Find(c.Scene);
                if (region == null) continue;
                var box = Box(c, region);
                if (!box.Contains(mouse) || !reach.Overlaps(box)) continue;
                if (Net.WorldSync.Guest)
                {
                    Net.WorldSync.Submit(new Net.WorldSync.Action { Op = "mineCrystal", Key = "l:" + c.Scene, Power = pick.PickPower });
                    return true;
                }
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
            UI.WorldMap.Refresh(c.Scene);
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

        /// <summary>The crystals placed and not yet taken: their scene and spot (bottom middle, scene coordinates), for the map.</summary>
        public static IEnumerable<(string Scene, Vector2 Local)> Standing()
        {
            foreach (var c in crystals)
                if (!c.Taken && c.Local.HasValue) yield return (c.Scene, c.Local.Value);
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
            if (!Net.WorldSync.CanInteract) return;
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
                    if (Net.WorldSync.Guest)
                    {
                        bool placed = FindSpot(region, Seed, out var suggested);
                        Net.WorldSync.Suggest(new Net.WorldSync.Action { Op = "locateCrystal", Scene = c.Scene, Placed = placed, X = suggested.x, Y = suggested.y });
                        continue;
                    }
                    if (FindSpot(region, Seed, out var local))
                    {
                        c.Local = local;
                        UI.WorldMap.Refresh(c.Scene);
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
                    if (Net.WorldSync.Guest)
                    {
                        Net.WorldSync.Suggest(new Net.WorldSync.Action { Op = "mineCrystal", Key = "l:" + c.Scene, Power = 100 });
                        continue;
                    }
                    Break(c, box);
                    continue;
                }
                // Beside the terrain, not under it (a boss room keeping the game's own ground switches the terrain root off).
                var holder = region.Props ?? region.Root;
                if (c.Renderer == null)
                {
                    var go = new GameObject("Terra Life Crystal");
                    go.transform.SetParent(holder, false);
                    c.Renderer = go.AddComponent<SpriteRenderer>();
                }
                if (c.Renderer.transform.parent != holder) c.Renderer.transform.SetParent(holder, false);
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
