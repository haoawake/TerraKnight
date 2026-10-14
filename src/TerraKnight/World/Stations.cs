using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>The crafting stations that can be set down (the furniture Recipe.requiredTile asks for).</summary>
    internal enum StationKind : byte
    {
        None, WorkBench, Furnace, IronAnvil, LeadAnvil, MythrilAnvil, OrichalcumAnvil, Sawmill, Loom, TinkerersWorkshop, Bottle,
        /// <summary>Not a crafting station: the Piggy Bank, opened with a right click (Items.GeoWallet).</summary>
        PiggyBank,
    }

    /// <summary>
    /// Crafting stations set on the ground, as in Terraria: a Work Bench made by hand from wood, a
    /// Furnace and an anvil at the bench, and the rest. Standing near one (Player.AdjTiles: the
    /// player's tile reach, 5 tiles across and 4 up and down) makes its recipes. Each is drawn from
    /// its tile's sheet (its style's frame), stands on two or three cells of ground like a chest,
    /// and a pickaxe knocks it back into an item. A station belongs to the scene it stands in, in
    /// that scene's own coordinates, and is kept in the mod's save. A Hallownest bench or Dirtmouth
    /// still makes everything its stage has reached (Items.Recipes), these add to that anywhere.
    /// </summary>
    internal sealed class Stations : MonoBehaviour
    {
        const float Px = Effects.TerraParticles.Px;
        const float Z = 0.0045f;

        /// <summary>A station's look and what it counts as.</summary>
        sealed class Spec
        {
            public string Texture;
            public int W, H, StyleX;
            public int[] Heights;
            public int DrawY;
            public ItemType Item;
            /// <summary>The recipe stations it is (Recipe.Station's names): an anvil of mythril is an anvil too.</summary>
            public string[] Counts;
        }

        static readonly Dictionary<StationKind, Spec> specs = new Dictionary<StationKind, Spec>
        {
            // TileObjectData: Style2x1 with CoordinateHeights {18} (tiles 18, 16), Style3x2 {16, 18} (17, 114).
            [StationKind.WorkBench] = new Spec { Texture = "Tiles_18", W = 2, H = 1, Heights = new[] { 18 }, Item = ItemType.WorkBench, Counts = new[] { "工作台" } },
            [StationKind.Furnace] = new Spec { Texture = "Tiles_17", W = 3, H = 2, Heights = new[] { 16, 18 }, Item = ItemType.Furnace, Counts = new[] { "熔炉" } },
            [StationKind.IronAnvil] = new Spec { Texture = "Tiles_16", W = 2, H = 1, Heights = new[] { 18 }, Item = ItemType.IronAnvil, Counts = new[] { "砧" } },
            [StationKind.LeadAnvil] = new Spec { Texture = "Tiles_16", W = 2, H = 1, StyleX = 36, Heights = new[] { 18 }, Item = ItemType.LeadAnvil, Counts = new[] { "砧" } },
            // Tile 134: Style2x1, drawn 2 px down (DrawYOffset).
            [StationKind.MythrilAnvil] = new Spec { Texture = "Tiles_134", W = 2, H = 1, Heights = new[] { 16 }, DrawY = 2, Item = ItemType.MythrilAnvil, Counts = new[] { "砧", "秘银砧" } },
            [StationKind.OrichalcumAnvil] = new Spec { Texture = "Tiles_134", W = 2, H = 1, StyleX = 36, Heights = new[] { 16 }, DrawY = 2, Item = ItemType.OrichalcumAnvil, Counts = new[] { "砧", "秘银砧" } },
            [StationKind.Sawmill] = new Spec { Texture = "Tiles_106", W = 3, H = 3, Heights = new[] { 16, 16, 18 }, Item = ItemType.Sawmill, Counts = new[] { "锯木机" } },
            [StationKind.Loom] = new Spec { Texture = "Tiles_86", W = 3, H = 2, Heights = new[] { 16, 18 }, Item = ItemType.Loom, Counts = new[] { "织布机" } },
            [StationKind.TinkerersWorkshop] = new Spec { Texture = "Tiles_114", W = 3, H = 2, Heights = new[] { 16, 18 }, Item = ItemType.TinkerersWorkshop, Counts = new[] { "工匠作坊" } },
            // A placed bottle (tile 13 style 0) is the alchemy station.
            [StationKind.Bottle] = new Spec { Texture = "Tiles_13", W = 1, H = 1, Heights = new[] { 16 }, Item = ItemType.Bottle, Counts = new[] { "玻璃瓶" } },
            // Tile 29: Style2x1 with CoordinateHeights {18}; it counts as no station.
            [StationKind.PiggyBank] = new Spec { Texture = "Tiles_29", W = 2, H = 1, Heights = new[] { 18 }, Item = ItemType.PiggyBank, Counts = new string[0] },
        };

        sealed class Station
        {
            public StationKind Kind;
            public string Scene;
            /// <summary>Bottom middle, in the scene's own coordinates.</summary>
            public Vector2 Local;
            public int Damage;
            public SpriteRenderer Renderer;
            TerrainRegion region;
            public TerrainRegion Region
            {
                get => region != null && region.Root != null ? region : null;
                set => region = value;
            }
            public Spec Spec => specs[Kind];
            public Vector2 World => Region != null ? (Vector2)Region.Root.position + Local : Local;
            public Rect Box => new Rect(World.x - Spec.W * 8f * Px, World.y, Spec.W * 16f * Px, Spec.H * 16f * Px);
        }

        /// <summary>What the save keeps of a station.</summary>
        public struct Saved
        {
            public string Scene;
            public Vector2 Local;
            public StationKind Kind;
        }

        public static Stations Instance { get; private set; }
        static readonly List<Station> stations = new List<Station>();
        static readonly Dictionary<StationKind, Sprite> sprites = new Dictionary<StationKind, Sprite>();
        float ticks;

        public static Stations Create(Transform parent)
        {
            var go = new GameObject("Terra Stations");
            go.transform.SetParent(parent, false);
            Instance = go.AddComponent<Stations>();
            return Instance;
        }

        public static int Count => stations.Count;
        public static int DevPlaced, DevBroken;

        /// <summary>The item a station kind drops when knocked down.</summary>
        public static ItemType ItemOf(StationKind k) => specs.TryGetValue(k, out var s) ? s.Item : ItemType.None;

        // ------------------------------------------------------------------ near

        /// <summary>Player.AdjTiles: a station of this recipe name stands within the player's reach.</summary>
        public static bool Near(string station, HeroController hero)
        {
            if (string.IsNullOrEmpty(station) || hero == null) return false;
            var reach = Reach(hero);
            foreach (var s in stations)
            {
                if (s.Region == null || !reach.Overlaps(s.Box)) continue;
                foreach (var c in s.Spec.Counts) if (c == station) return true;
            }
            return false;
        }

        /// <summary>The recipe stations standing within reach now, for the crafting panel's note.</summary>
        public static List<string> NearNames(HeroController hero)
        {
            var names = new List<string>();
            if (hero == null) return names;
            var reach = Reach(hero);
            foreach (var s in stations)
                if (s.Region != null && reach.Overlaps(s.Box))
                    foreach (var c in s.Spec.Counts) if (!names.Contains(c)) names.Add(c);
            return names;
        }

        /// <summary>TileReachCheckSettings.Simple: the player's tiles, 5 more across and 4 more up and down.</summary>
        static Rect Reach(HeroController hero)
        {
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            const float cell = CellGrid.CellSize;
            return Rect.MinMaxRect(b.min.x - 5f * cell, b.min.y - 4f * cell, b.max.x + 5f * cell, b.max.y + 4f * cell);
        }

        // ------------------------------------------------------------------ placing

        /// <summary>A station set where the cursor points: snapped to the grid, on even ground under all its cells, room for it, in reach.</summary>
        public static bool TryPlace(StationKind kind, HeroController hero, Vector2 aim)
        {
            if (hero == null || !specs.ContainsKey(kind)) return false;
            if (!FindSpot(kind, hero, aim, out var bottom)) return false;
            if (BossRooms.Locked(bottom + new Vector2(0f, 0.1f))) { BossRooms.Notice(bottom); return false; }
            var at = CellGrid.WorldToCell(bottom + new Vector2(0f, 0.1f));
            var region = TerraWorld.RegionAt(at.x, at.y) ?? TerrainSystem.Current?.Region;
            if (region == null) return false;
            stations.Add(new Station { Kind = kind, Region = region, Scene = region.SceneName, Local = bottom - (Vector2)region.Root.position });
            TerraMod.Instance?.PlaySound("Dig_" + Random.Range(0, 3));
            DevPlaced++;
            return true;
        }

        public static bool FindSpot(StationKind kind, HeroController hero, Vector2 aim, out Vector2 bottom)
        {
            bottom = default;
            var spec = specs[kind];
            const int mask = 1 << ChunkColliders.TerrainLayer;
            const float cell = CellGrid.CellSize;
            float w = spec.W * cell, h = spec.H * cell;
            // An odd width centres on a cell, an even one on a cell edge.
            float x = spec.W % 2 == 1 ? (Mathf.Floor(aim.x / cell) + 0.5f) * cell : Mathf.Round(aim.x / cell) * cell;
            var down = Physics2D.Raycast(new Vector2(x, aim.y + 0.25f), Vector2.down, 1.5f, mask);
            if (down.collider == null) return false;
            float y = down.point.y;
            for (int i = 0; i < spec.W; i++)
            {
                float cx = x - w / 2f + (i + 0.5f) * cell;
                var side = Physics2D.Raycast(new Vector2(cx, y + 0.1f), Vector2.down, 0.2f, mask);
                if (side.collider == null || Mathf.Abs(side.point.y - y) > 0.06f) return false;
            }
            var centre = new Vector2(x, y + h / 2f + 0.02f);
            if (Physics2D.OverlapBox(centre, new Vector2(w, h) * 0.9f, 0f, mask) != null) return false;
            var box = new Rect(x - w / 2f, y, w, h);
            foreach (var s in stations)
                if (s.Region != null && s.Box.Overlaps(box)) return false;
            foreach (var c in Chests.Positions())
                if (box.Contains(c)) return false;
            if (!Reach(hero).Overlaps(box)) return false;
            bottom = new Vector2(x, y);
            return true;
        }

        /// <summary>A right click on a Piggy Bank in reach: the bank opens (or shuts) with the inventory. True when the click was on one.</summary>
        public static bool TryRightClick(Vector2 mouse)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null) return false;
            var reach = Reach(hero);
            foreach (var s in stations)
            {
                if (s.Kind != StationKind.PiggyBank || s.Region == null || !s.Box.Contains(mouse) || !reach.Overlaps(s.Box)) continue;
                Chests.ToggleBank(s.Scene, s.Region, s.Local);
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ breaking

        /// <summary>A pickaxe swung at a station in reach: pick power until 100 knocks it down, its item where it stood. True when the swing was at one.</summary>
        public static bool TryMine(ItemDef pick, Vector2 mouse, HeroController hero)
        {
            if (pick == null || pick.PickPower <= 0 || hero == null) return false;
            var reach = Reach(hero);
            for (int i = 0; i < stations.Count; i++)
            {
                var s = stations[i];
                if (s.Region == null || !s.Box.Contains(mouse) || !reach.Overlaps(s.Box)) continue;
                var px = Effects.TerraParticles.ToPx(s.Box.center);
                s.Damage += pick.PickPower;
                TerraMod.Instance?.PlaySound("Dig_" + Random.Range(0, 3));
                if (s.Damage < 100)
                {
                    Dust(px, s, 3);
                    return true;
                }
                Dust(px, s, 10);
                WorldItems.Drop(ItemStack.Of(s.Spec.Item), s.Box.center);
                Remove(i);
                DevBroken++;
                return true;
            }
            return false;
        }

        static void Dust(Vector2 px, Station s, int n)
        {
            var p = Effects.TerraParticles.Instance;
            if (p == null) return;
            // Wood for the wooden ones, stone and metal sparks for the rest.
            int dust = s.Kind == StationKind.WorkBench || s.Kind == StationKind.Loom || s.Kind == StationKind.Sawmill ? 7 : 1;
            float hw = s.Spec.W * 8f, hh = s.Spec.H * 8f;
            for (int k = 0; k < n; k++) p.NewDust(px - new Vector2(hw, hh), (int)(hw * 2f), (int)(hh * 2f), dust);
        }

        // ------------------------------------------------------------------ each frame

        void Update()
        {
            if (TerrainSystem.Current == null) return;
            foreach (var s in stations) s.Region = RegionNamed(s.Scene);
            ticks += Mathf.Min(Time.deltaTime, 0.1f) * 60f;
            while (ticks >= 1f)
            {
                ticks -= 1f;
                foreach (var s in stations)
                    if (s.Damage > 0 && Random.Range(0, 60) == 0) s.Damage = 0;
            }
            foreach (var s in stations)
            {
                if (s.Region == null)
                {
                    if (s.Renderer != null) s.Renderer.enabled = false;
                    continue;
                }
                if (s.Renderer == null)
                {
                    var go = new GameObject("Terra Station " + s.Kind);
                    go.transform.SetParent(s.Region.Root, false);
                    s.Renderer = go.AddComponent<SpriteRenderer>();
                }
                s.Renderer.enabled = true;
                s.Renderer.sprite = SpriteOf(s.Kind);
                // A bottom row 18 px tall sinks its last 2 px into the ground; DrawYOffset lowers it more.
                var spec = s.Spec;
                float sink = (spec.Heights[spec.Heights.Length - 1] - 16) + spec.DrawY;
                s.Renderer.transform.localPosition = new Vector3(s.Local.x, s.Local.y - sink * Px, Z - s.Region.Root.position.z);
            }
        }

        /// <summary>A station's look, put together from its tiles in the sheet (18 px apart across; down by each row's height and 2).</summary>
        static Sprite SpriteOf(StationKind kind)
        {
            if (sprites.TryGetValue(kind, out var sp) && sp != null) return sp;
            var spec = specs[kind];
            var src = TerrariaAssets.Pixels(spec.Texture, out int sw, out int sh);
            int w = spec.W * 16, h = 0;
            foreach (var th in spec.Heights) h += th;
            var dst = new Color32[w * h];
            int sy0 = 0, dy0 = 0;
            for (int r = 0; r < spec.H; r++)
            {
                int th = spec.Heights[Mathf.Min(r, spec.Heights.Length - 1)];
                for (int col = 0; col < spec.W; col++)
                    for (int y = 0; y < th; y++)
                        for (int x = 0; x < 16; x++)
                        {
                            int sx = spec.StyleX + col * 18 + x, sy = sy0 + y;
                            int dx = col * 16 + x, dy = dy0 + y;
                            if (sx >= sw || sy >= sh) continue;
                            dst[(h - 1 - dy) * w + dx] = src[(sh - 1 - sy) * sw + sx];
                        }
                sy0 += th + 2;
                dy0 += th;
            }
            var tex = TerrariaAssets.Make(spec.Texture + "#station" + spec.StyleX, dst, w, h);
            sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 1f / Px, 0, SpriteMeshType.FullRect);
            sprites[kind] = sp;
            return sp;
        }

        static TerrainRegion RegionNamed(string scene)
        {
            foreach (var t in TerrainSystem.All)
                if (t != null && t.Region != null && t.Region.SceneName == scene) return t.Region;
            return null;
        }

        static void Remove(int i)
        {
            var s = stations[i];
            if (s.Renderer != null) Object.Destroy(s.Renderer.gameObject);
            stations.RemoveAt(i);
        }

        public static void Clear()
        {
            for (int i = stations.Count - 1; i >= 0; i--) Remove(i);
        }

        public static List<Saved> Save()
        {
            var list = new List<Saved>(stations.Count);
            foreach (var s in stations) list.Add(new Saved { Scene = s.Scene, Local = s.Local, Kind = s.Kind });
            return list;
        }

        public static void Load(IEnumerable<Saved> saved)
        {
            Clear();
            if (saved == null) return;
            foreach (var s in saved)
                if (specs.ContainsKey(s.Kind)) stations.Add(new Station { Scene = s.Scene, Local = s.Local, Kind = s.Kind });
        }

        /// <summary>Dev: the stations standing in loaded scenes, by kind.</summary>
        public static string DevDescribe()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var s in stations) sb.Append($" {s.Kind}@{s.Scene}{(s.Region != null ? "" : "(未载入)")}");
            return sb.Length > 0 ? sb.ToString() : " 无";
        }
    }
}
