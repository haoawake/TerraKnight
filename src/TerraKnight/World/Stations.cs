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
        Hellforge, AdamantiteForge, TitaniumForge, CrystalBall, Bookcase, ImbuingStation, Autohammer, AncientManipulator, Solidifier,
        Keg, CookingPot, AlchemyTable, HeavyWorkBench, BoneWelder, GlassKiln, LivingLoom, SkyMill, IceMachine, LihzahrdFurnace,
        SteampunkBoiler, FleshCloningVat, HoneyDispenser, BlendOMatic, MeatGrinder,
        /// <summary>A right click gives their buff (BuffOf).</summary>
        AmmoBox, BewitchingTable, SharpeningStation, WarTable, SliceOfCake,
        /// <summary>Fixed in the world (FixedStations), not an item: the altars.</summary>
        DemonAltar, CrimsonAltar,
    }

    /// <summary>
    /// Crafting stations set on the ground, as in Terraria: a Work Bench made by hand from wood, a
    /// Furnace and an anvil at the bench, and the rest. Standing near one (Player.AdjTiles: the
    /// player's tile reach, 5 tiles across and 4 up and down) makes its recipes. Each is drawn from
    /// its tile's sheet (its style's frame), stands on two or three cells of ground like a chest,
    /// and a pickaxe knocks it back into an item. The ones Terraria makes solid on top (Main.tileSolidTop:
    /// the Work Bench, the anvils, the Tinkerer's Workshop) are stood on like a platform (held down,
    /// dropped through) and carry other furniture (TileObject.CanPlace's SolidWithTop and Table
    /// anchors: an anvil on a bench, a bottle on a bench). One whose ground is dug out from under it
    /// falls back into its item, as Terraria's furniture breaks without its anchor, and whatever
    /// stood on it with it. A station belongs to the scene it stands in, in
    /// that scene's own coordinates, and is kept in the mod's save. A Hallownest bench or Dirtmouth
    /// still makes everything its stage has reached (Items.Recipes), these add to that anywhere.
    /// </summary>
    internal sealed partial class Stations : MonoBehaviour
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
            /// <summary>Main.tileSolidTop: stood on like a platform, and furniture goes on top.</summary>
            public bool SolidTop;
            /// <summary>The buff a right click on it gives (Player.TileInteractionsUse: Crystal Ball 29, Ammo Box 93, ...) and for how long, ticks.</summary>
            public int Buff, BuffTicks;
        }

        static readonly Dictionary<StationKind, Spec> specs = new Dictionary<StationKind, Spec>
        {
            // TileObjectData: Style2x1 with CoordinateHeights {18} (tiles 18, 16), Style3x2 {16, 18} (17, 114).
            [StationKind.WorkBench] = new Spec { Texture = "Tiles_18", W = 2, H = 1, Heights = new[] { 18 }, Item = ItemType.WorkBench, Counts = new[] { "工作台" }, SolidTop = true },
            [StationKind.Furnace] = new Spec { Texture = "Tiles_17", W = 3, H = 2, Heights = new[] { 16, 18 }, Item = ItemType.Furnace, Counts = new[] { "熔炉" } },
            [StationKind.IronAnvil] = new Spec { Texture = "Tiles_16", W = 2, H = 1, Heights = new[] { 18 }, Item = ItemType.IronAnvil, Counts = new[] { "砧" }, SolidTop = true },
            [StationKind.LeadAnvil] = new Spec { Texture = "Tiles_16", W = 2, H = 1, StyleX = 36, Heights = new[] { 18 }, Item = ItemType.LeadAnvil, Counts = new[] { "砧" }, SolidTop = true },
            // Tile 134: Style2x1, drawn 2 px down (DrawYOffset).
            [StationKind.MythrilAnvil] = new Spec { Texture = "Tiles_134", W = 2, H = 1, Heights = new[] { 16 }, DrawY = 2, Item = ItemType.MythrilAnvil, Counts = new[] { "砧", "秘银砧" }, SolidTop = true },
            [StationKind.OrichalcumAnvil] = new Spec { Texture = "Tiles_134", W = 2, H = 1, StyleX = 36, Heights = new[] { 16 }, DrawY = 2, Item = ItemType.OrichalcumAnvil, Counts = new[] { "砧", "秘银砧" }, SolidTop = true },
            [StationKind.Sawmill] = new Spec { Texture = "Tiles_106", W = 3, H = 3, Heights = new[] { 16, 16, 18 }, Item = ItemType.Sawmill, Counts = new[] { "锯木机" } },
            [StationKind.Loom] = new Spec { Texture = "Tiles_86", W = 3, H = 2, Heights = new[] { 16, 18 }, Item = ItemType.Loom, Counts = new[] { "织布机" } },
            [StationKind.TinkerersWorkshop] = new Spec { Texture = "Tiles_114", W = 3, H = 2, Heights = new[] { 16, 18 }, Item = ItemType.TinkerersWorkshop, Counts = new[] { "工匠作坊" }, SolidTop = true },
            // A placed bottle (tile 13 style 0) is the alchemy station.
            [StationKind.Bottle] = new Spec { Texture = "Tiles_13", W = 1, H = 1, Heights = new[] { 16 }, Item = ItemType.Bottle, Counts = new[] { "玻璃瓶" } },
            // Tile 29: Style2x1 with CoordinateHeights {18}; it counts as no station.
            [StationKind.PiggyBank] = new Spec { Texture = "Tiles_29", W = 2, H = 1, Heights = new[] { 18 }, Item = ItemType.PiggyBank, Counts = new string[0] },
            // TileObjectData: the Hellforge counts as a Furnace too, the forges as both (Recipe: adjTile 77 and 17); the Alchemy
            // Table as a placed bottle; the Living Loom as a Loom. Styles side by side for the second forge and the Crimson Altar.
            [StationKind.Hellforge] = new Spec { Texture = "Tiles_77", W = 3, H = 2, Heights = new[] { 16, 18 }, Item = ItemType.Hellforge, Counts = new[] { "地狱熔炉", "熔炉" } },
            [StationKind.AdamantiteForge] = new Spec { Texture = "Tiles_133", W = 3, H = 2, Heights = new[] { 16, 18 }, Item = ItemType.AdamantiteForge, Counts = new[] { "精金熔炉", "地狱熔炉", "熔炉" } },
            [StationKind.TitaniumForge] = new Spec { Texture = "Tiles_133", W = 3, H = 2, StyleX = 54, Heights = new[] { 16, 18 }, Item = ItemType.TitaniumForge, Counts = new[] { "精金熔炉", "地狱熔炉", "熔炉" } },
            [StationKind.CrystalBall] = new Spec { Texture = "Tiles_125", W = 2, H = 2, Heights = new[] { 16, 18 }, Item = ItemType.CrystalBall, Counts = new[] { "水晶球" }, Buff = 29, BuffTicks = 36000 },
            [StationKind.Bookcase] = new Spec { Texture = "Tiles_101", W = 3, H = 4, Heights = new[] { 16, 16, 16, 18 }, Item = ItemType.Bookcase, Counts = new[] { "书架" } },
            [StationKind.ImbuingStation] = new Spec { Texture = "Tiles_243", W = 3, H = 3, Heights = new[] { 16, 16, 18 }, Item = ItemType.ImbuingStation, Counts = new[] { "灌注站" } },
            [StationKind.Autohammer] = new Spec { Texture = "Tiles_247", W = 3, H = 3, Heights = new[] { 16, 16, 18 }, Item = ItemType.Autohammer, Counts = new[] { "自动锤炼机" } },
            [StationKind.AncientManipulator] = new Spec { Texture = "Tiles_412", W = 3, H = 3, Heights = new[] { 16, 16, 18 }, Item = ItemType.LunarCraftingStation, Counts = new[] { "远古操纵机" } },
            [StationKind.Solidifier] = new Spec { Texture = "Tiles_220", W = 3, H = 3, Heights = new[] { 16, 16, 18 }, Item = ItemType.Solidifier, Counts = new[] { "固化机" } },
            [StationKind.Keg] = new Spec { Texture = "Tiles_94", W = 2, H = 2, Heights = new[] { 16, 18 }, Item = ItemType.Keg, Counts = new[] { "酒桶" } },
            [StationKind.CookingPot] = new Spec { Texture = "Tiles_96", W = 2, H = 2, Heights = new[] { 16, 18 }, Item = ItemType.CookingPot, Counts = new[] { "烹饪锅" } },
            [StationKind.AlchemyTable] = new Spec { Texture = "Tiles_355", W = 3, H = 3, Heights = new[] { 16, 16, 18 }, Item = ItemType.AlchemyTable, Counts = new[] { "炼药桌", "玻璃瓶" } },
            [StationKind.HeavyWorkBench] = new Spec { Texture = "Tiles_283", W = 3, H = 3, Heights = new[] { 16, 16, 18 }, Item = ItemType.HeavyWorkBench, Counts = new[] { "重型组装机" } },
            [StationKind.BoneWelder] = new Spec { Texture = "Tiles_300", W = 3, H = 3, Heights = new[] { 16, 16, 18 }, Item = ItemType.BoneWelder, Counts = new[] { "骨头焊机" } },
            [StationKind.FleshCloningVat] = new Spec { Texture = "Tiles_301", W = 3, H = 3, Heights = new[] { 16, 16, 18 }, Item = ItemType.FleshCloningVaat, Counts = new[] { "血肉克隆台" } },
            [StationKind.GlassKiln] = new Spec { Texture = "Tiles_302", W = 3, H = 3, Heights = new[] { 16, 16, 18 }, Item = ItemType.GlassKiln, Counts = new[] { "玻璃窑" } },
            [StationKind.LihzahrdFurnace] = new Spec { Texture = "Tiles_303", W = 3, H = 3, Heights = new[] { 16, 16, 18 }, Item = ItemType.LihzahrdFurnace, Counts = new[] { "丛林蜥蜴熔炉" } },
            [StationKind.LivingLoom] = new Spec { Texture = "Tiles_304", W = 3, H = 3, Heights = new[] { 16, 16, 18 }, Item = ItemType.LivingLoom, Counts = new[] { "生命木织机", "织布机" } },
            [StationKind.SkyMill] = new Spec { Texture = "Tiles_305", W = 3, H = 3, Heights = new[] { 16, 16, 18 }, Item = ItemType.SkyMill, Counts = new[] { "天磨" } },
            [StationKind.IceMachine] = new Spec { Texture = "Tiles_306", W = 3, H = 3, Heights = new[] { 16, 16, 18 }, Item = ItemType.IceMachine, Counts = new[] { "冰雪机" } },
            [StationKind.SteampunkBoiler] = new Spec { Texture = "Tiles_307", W = 3, H = 3, Heights = new[] { 16, 16, 18 }, Item = ItemType.SteampunkBoiler, Counts = new[] { "蒸汽朋克锅炉" } },
            [StationKind.HoneyDispenser] = new Spec { Texture = "Tiles_308", W = 3, H = 3, Heights = new[] { 16, 16, 18 }, Item = ItemType.HoneyDispenser, Counts = new[] { "蜂蜜分配机" } },
            [StationKind.BlendOMatic] = new Spec { Texture = "Tiles_217", W = 3, H = 2, Heights = new[] { 16, 18 }, Item = ItemType.BlendOMatic, Counts = new[] { "搅拌机" } },
            [StationKind.MeatGrinder] = new Spec { Texture = "Tiles_218", W = 3, H = 2, Heights = new[] { 16, 18 }, Item = ItemType.MeatGrinder, Counts = new[] { "绞肉机" } },
            // The buff stations (buffs 93 Ammo Box, 150 Bewitched, 159 Sharpened, 348 Strategist: 10 minutes; 192 Sugar Rush: 2).
            [StationKind.AmmoBox] = new Spec { Texture = "Tiles_287", W = 2, H = 2, Heights = new[] { 16, 18 }, Item = ItemType.AmmoBox, Counts = new string[0], Buff = 93, BuffTicks = 36000 },
            [StationKind.BewitchingTable] = new Spec { Texture = "Tiles_354", W = 3, H = 3, Heights = new[] { 16, 16, 18 }, Item = ItemType.BewitchingTable, Counts = new string[0], Buff = 150, BuffTicks = 36000 },
            [StationKind.SharpeningStation] = new Spec { Texture = "Tiles_377", W = 3, H = 2, Heights = new[] { 16, 18 }, Item = ItemType.SharpeningStation, Counts = new string[0], Buff = 159, BuffTicks = 36000 },
            [StationKind.WarTable] = new Spec { Texture = "Tiles_464", W = 5, H = 4, Heights = new[] { 16, 16, 16, 18 }, Item = ItemType.WarTable, Counts = new string[0], Buff = 348, BuffTicks = 36000 },
            [StationKind.SliceOfCake] = new Spec { Texture = "Tiles_621", W = 2, H = 2, Heights = new[] { 16, 18 }, Item = ItemType.SliceOfCake, Counts = new string[0], Buff = 192, BuffTicks = 7200 },
            // The altars (tile 26: Demon Altar style 0, Crimson Altar style 1), fixed in the world, never knocked down.
            [StationKind.DemonAltar] = new Spec { Texture = "Tiles_26", W = 3, H = 2, Heights = new[] { 16, 18 }, Item = ItemType.None, Counts = new[] { "恶魔祭坛 / 猩红祭坛" } },
            [StationKind.CrimsonAltar] = new Spec { Texture = "Tiles_26", W = 3, H = 2, StyleX = 54, Heights = new[] { 16, 18 }, Item = ItemType.None, Counts = new[] { "恶魔祭坛 / 猩红祭坛" } },
        };

        sealed class Station
        {
            public StationKind Kind;
            public string Scene;
            /// <summary>Bottom middle, in the scene's own coordinates.</summary>
            public Vector2 Local;
            public int Damage;
            /// <summary>Set in the world by the mod (the altars): not the player's, never knocked down nor saved.</summary>
            public bool Fixed;
            public SpriteRenderer Renderer;
            /// <summary>Its top, a one-way platform on the terrain layer (the solid-top ones).</summary>
            public GameObject Top;
            public float TopOffUntil;
            /// <summary>Whether it had its ground under it at the last look, in which region (a load starts afresh).</summary>
            public bool Supported;
            public TerrainRegion SupportedIn;
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
            if (!Net.WorldSync.CanInteract) return false;
            if (hero == null || !specs.ContainsKey(kind)) return false;
            if (!FindSpot(kind, hero, aim, out var bottom)) return false;
            if (BossRooms.Locked(bottom + new Vector2(0f, 0.1f))) { BossRooms.Notice(bottom); return false; }
            var at = CellGrid.WorldToCell(bottom + new Vector2(0f, 0.1f));
            var region = TerraWorld.RegionAt(at.x, at.y) ?? TerrainSystem.Current?.Region;
            if (region == null) return false;
            if (Net.WorldSync.Guest)
            {
                var local = bottom - (Vector2)region.Root.position;
                Net.WorldSync.Submit(new Net.WorldSync.Action { Op = "station", Scene = region.SceneName, X = local.x, Y = local.y, Kind = (int)kind, Count = TerraMod.Instance.Inventory.Infinite ? 0 : 1 });
                return true;
            }
            stations.Add(new Station { Kind = kind, Region = region, Scene = region.SceneName, Local = bottom - (Vector2)region.Root.position });
            TerraMod.Instance?.PlaySound("Dig_" + Random.Range(0, 3));
            DevPlaced++;
            return true;
        }

        /// <summary>Dev: why the last FindSpot found no spot.</summary>
        public static string DevWhy;
        public static int DevDrops;

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
            if (down.collider == null) { DevWhy = "下面没有地"; return false; }
            float y = down.point.y;
            for (int i = 0; i < spec.W; i++)
            {
                float cx = x - w / 2f + (i + 0.5f) * cell;
                var side = Physics2D.Raycast(new Vector2(cx, y + 0.1f), Vector2.down, 0.2f, mask);
                if (side.collider == null || Mathf.Abs(side.point.y - y) > 0.06f) { DevWhy = $"第 {i + 1} 格下面不平（{(side.collider != null ? side.collider.name + " " + side.point.y.ToString("F2") : "无")}，地面 {y:F2}）"; return false; }
            }
            var centre = new Vector2(x, y + h / 2f + 0.02f);
            var inWay = Physics2D.OverlapBox(centre, new Vector2(w, h) * 0.9f, 0f, mask);
            if (inWay != null) { DevWhy = "挡着 " + inWay.name; return false; }
            var box = new Rect(x - w / 2f, y, w, h);
            // Inset a little: one standing on another's top only touches it.
            var inner = new Rect(box.x + 0.02f, box.y + 0.02f, box.width - 0.04f, box.height - 0.04f);
            foreach (var s in stations)
                if (s.Region != null && s.Box.Overlaps(inner)) { DevWhy = "和别的工作站重叠"; return false; }
            foreach (var c in Chests.Positions())
                if (box.Contains(c)) { DevWhy = "和箱子重叠"; return false; }
            if (!Reach(hero).Overlaps(box)) { DevWhy = "够不着"; return false; }
            DevWhy = null;
            bottom = new Vector2(x, y);
            return true;
        }

        /// <summary>
        /// A right click on a station in reach: a Piggy Bank opens (or shuts) with the inventory; a buff station gives its
        /// buff (Player.TileInteractionsUse: the Crystal Ball, the Ammo Box, the Bewitching Table, the Sharpening Station,
        /// the War Table, the Slice of Cake). True when the click was on one.
        /// </summary>
        public static bool TryRightClick(Vector2 mouse)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null) return false;
            var reach = Reach(hero);
            foreach (var s in stations)
            {
                if (s.Region == null || !s.Box.Contains(mouse) || !reach.Overlaps(s.Box)) continue;
                if (s.Kind == StationKind.PiggyBank)
                {
                    Chests.ToggleBank(s.Scene, s.Region, s.Local);
                    return true;
                }
                if (s.Spec.Buff > 0)
                {
                    Player.PlayerBuffs.Add(s.Spec.Buff, s.Spec.BuffTicks);
                    TerraMod.Instance?.PlaySound(s.Kind == StationKind.SliceOfCake ? "Item_2" : s.Kind == StationKind.AmmoBox ? "Item_149" : s.Kind == StationKind.SharpeningStation ? "Item_37" : "Item_4");
                    DevBuffsTaken++;
                    return true;
                }
            }
            return false;
        }

        public static int DevBuffsTaken;

        /// <summary>Dev: the recipe stations standing within reach.</summary>
        public static string DevNearNames(HeroController hero)
        {
            var names = new List<string>();
            var reach = Reach(hero);
            foreach (var s in stations)
                if (s.Region != null && reach.Overlaps(s.Box))
                    foreach (var c in s.Spec.Counts) if (!names.Contains(c)) names.Add(c);
            return names.Count == 0 ? "无" : string.Join("、", names);
        }

        /// <summary>Dev: the box of the last station of a kind in a loaded scene.</summary>
        public static Rect? DevBoxOf(StationKind kind)
        {
            for (int i = stations.Count - 1; i >= 0; i--)
                if (stations[i].Kind == kind && stations[i].Region != null) return stations[i].Box;
            return null;
        }

        /// <summary>
        /// A station the mod sets in the world (the altars, FixedStations): once per scene and kind, bottom middle in the
        /// scene's own coordinates; it is never knocked down, never falls and isn't saved.
        /// </summary>
        public static void PlaceFixed(StationKind kind, string scene, Vector2 local)
        {
            foreach (var s in stations) if (s.Fixed && s.Kind == kind && s.Scene == scene) return;
            stations.Add(new Station { Kind = kind, Scene = scene, Local = local, Fixed = true });
        }

        // ------------------------------------------------------------------ breaking

        /// <summary>A pickaxe swung at a station in reach: pick power until 100 knocks it down, its item where it stood. True when the swing was at one.</summary>
        public static bool TryMine(ItemDef pick, Vector2 mouse, HeroController hero)
        {
            if (!Net.WorldSync.CanInteract) return false;
            if (pick == null || pick.PickPower <= 0 || hero == null) return false;
            var reach = Reach(hero);
            for (int i = 0; i < stations.Count; i++)
            {
                var s = stations[i];
                if (s.Region == null || !s.Box.Contains(mouse) || !reach.Overlaps(s.Box)) continue;
                // WorldGen.CanKillTile: nothing under a chest goes; the world's own altars stay.
                if (Chests.RestsOn(s.Box) || s.Fixed) return true;
                if (Net.WorldSync.Guest)
                {
                    Net.WorldSync.Submit(new Net.WorldSync.Action { Op = "mineStation", Key = Net.WorldSync.Key("s", s.Scene, s.Local), Power = pick.PickPower });
                    return true;
                }
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
            FixedStations.Ensure();
            foreach (var s in stations) s.Region = RegionNamed(s.Scene);
            ticks += Mathf.Min(Time.deltaTime, 0.1f) * 60f;
            while (ticks >= 1f)
            {
                ticks -= 1f;
                foreach (var s in stations)
                    if (s.Damage > 0 && Random.Range(0, 60) == 0) s.Damage = 0;
            }
            // Ground dug out from under one: it falls back into its item (stacked ones follow over the next frames).
            for (int i = stations.Count - 1; i >= 0; i--)
            {
                var s = stations[i];
                if (s.Region == null) { s.SupportedIn = null; continue; }
                if (s.Fixed) continue;
                bool now = HasGround(s);
                if (s.SupportedIn == s.Region && s.Supported && !now && !Chests.RestsOn(s.Box))
                {
                    if (Net.WorldSync.Guest)
                    {
                        Net.WorldSync.Suggest(new Net.WorldSync.Action { Op = "mineStation", Key = Net.WorldSync.Key("s", s.Scene, s.Local), Power = 100 });
                        continue;
                    }
                    Dust(Effects.TerraParticles.ToPx(s.Box.center), s, 10);
                    WorldItems.Drop(ItemStack.Of(s.Spec.Item), s.Box.center);
                    TerraMod.Instance?.PlaySound("Dig_" + Random.Range(0, 3));
                    Remove(i);
                    DevFell++;
                    continue;
                }
                s.Supported = now;
                s.SupportedIn = s.Region;
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
                if (s.Spec.SolidTop)
                {
                    if (s.Top == null) s.Top = MakeTop(s);
                    else PlaceTop(s, s.Top);
                    // Back on only once the player is out of it or above it: switched on round a body inside,
                    // the one-way top pushes it up on to itself (a drop through would bounce back up).
                    bool on = Time.time >= s.TopOffUntil && !Inside(s);
                    if (s.Top.activeSelf != on) s.Top.SetActive(on);
                }
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

        public static int DevFell;

        /// <summary>
        /// Its anchor (AnchorBottom across its width): under each of its cells a block or a platform of
        /// the grid, a solid-top station, or the scene's own ground the grid doesn't hold.
        /// </summary>
        static bool HasGround(Station s)
        {
            var box = s.Box;
            const float cell = CellGrid.CellSize;
            for (int i = 0; i < s.Spec.W; i++)
            {
                float cx = box.xMin + (i + 0.5f) * cell;
                var under = CellGrid.WorldToCell(new Vector2(cx, box.yMin - 0.05f));
                if (TerraWorld.RegionAt(under.x, under.y) != null && (TerraWorld.IsSolid(under.x, under.y) || TerraWorld.IsPlatform(under.x, under.y))) continue;
                if (TopAt(cx, box.yMin, s)) continue;
                if (OwnGround(new Vector2(cx, box.yMin + 0.05f))) continue;
                return false;
            }
            return true;
        }

        /// <summary>A solid-top station (other than one) whose top is at that height over that point.</summary>
        static bool TopAt(float x, float y, Station except = null)
        {
            foreach (var o in stations)
            {
                if (o == except || o.Region == null || !o.Spec.SolidTop) continue;
                var b = o.Box;
                if (x > b.xMin && x < b.xMax && Mathf.Abs(b.yMax - y) < 0.06f) return true;
            }
            return false;
        }

        static readonly RaycastHit2D[] hits = new RaycastHit2D[8];

        /// <summary>Terrain the grid doesn't hold right under a point (the scene's own floors kept as they are): not ours.</summary>
        static bool OwnGround(Vector2 from)
        {
            int n = Physics2D.RaycastNonAlloc(from, Vector2.down, hits, 0.15f, 1 << ChunkColliders.TerrainLayer);
            for (int i = 0; i < n; i++)
                if (hits[i].collider != null && hits[i].collider.GetComponentInParent<TerraOwned>() == null && hits[i].collider.GetComponentInParent<Stations>() == null) return true;
            return false;
        }

        /// <summary>The player's body overlaps the station with the feet below its top (dropped through, walked into).</summary>
        static bool Inside(Station s)
        {
            if (s.Top == null || s.Top.activeSelf) return false;
            var hero = HeroController.SilentInstance;
            var col = hero != null ? hero.GetComponent<BoxCollider2D>() : null;
            if (col == null) return false;
            var h = col.bounds;
            var b = s.Box;
            return h.max.x > b.xMin && h.min.x < b.xMax && h.max.y > b.yMin && h.min.y < b.yMax - 0.02f;
        }

        /// <summary>A solid-top station's top: a one-way platform over its box on the terrain layer, as the grid's platforms are.</summary>
        static GameObject MakeTop(Station s)
        {
            var go = new GameObject("Terra Station Top") { layer = ChunkColliders.TerrainLayer };
            go.transform.SetParent(s.Renderer.transform, false);
            go.AddComponent<TerraOwned>();
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
            var box = go.AddComponent<BoxCollider2D>();
            box.usedByEffector = true;
            box.sharedMaterial = s.Region != null ? s.Region.Material : null;
            var effector = go.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 160f;
            effector.useSideFriction = false;
            effector.useSideBounce = false;
            PlaceTop(s, go);
            return go;
        }

        static void PlaceTop(Station s, GameObject top)
        {
            // In the renderer's space, which stands sunk by the sprite's extra pixels: the box is the station's own.
            var b = s.Box;
            var box = top.GetComponent<BoxCollider2D>();
            top.transform.position = new Vector3(b.center.x, b.center.y, s.Renderer.transform.position.z);
            box.size = b.size;
            box.offset = Vector2.zero;
        }

        /// <summary>
        /// Down held on a solid-top station: its top lets the player through for a moment, as the
        /// grid's platforms do. True when the feet were on one.
        /// </summary>
        public static bool DropThrough(Bounds feet, float seconds)
        {
            bool any = false;
            foreach (var s in stations)
            {
                if (s.Region == null || !s.Spec.SolidTop || s.Top == null) continue;
                var b = s.Box;
                if (feet.max.x <= b.xMin || feet.min.x >= b.xMax || Mathf.Abs(feet.min.y - b.yMax) > 0.15f) continue;
                s.TopOffUntil = Time.time + seconds;
                s.Top.SetActive(false);
                any = true;
                DevDrops++;
            }
            return any;
        }

        /// <summary>A loaded station takes up part of this box (touching its top or sides doesn't count).</summary>
        public static bool Overlaps(Rect box)
        {
            var inner = new Rect(box.x + 0.02f, box.y + 0.02f, box.width - 0.04f, box.height - 0.04f);
            foreach (var s in stations)
                if (s.Region != null && s.Box.Overlaps(inner)) return true;
            return false;
        }

        /// <summary>A Piggy Bank stands within this distance of a point in a loaded scene (quick stacking reaches it).</summary>
        public static bool PiggyBankWithin(Vector2 from, float range)
        {
            foreach (var s in stations)
                if (s.Kind == StationKind.PiggyBank && s.Region != null && Vector2.Distance(s.Box.center, from) <= range) return true;
            return false;
        }

        /// <summary>A station covers this cell (blocks can't go in it).</summary>
        public static bool Covers(Vector2Int cell)
        {
            var c = CellGrid.CellCenter(cell.x, cell.y);
            foreach (var s in stations)
                if (s.Region != null && s.Box.Contains(c)) return true;
            return false;
        }

        /// <summary>Dev: the box of the station nearest a point (empty when none is loaded).</summary>
        public static Rect DevBoxNear(Vector2 p)
        {
            Rect best = default;
            float d = float.MaxValue;
            foreach (var s in stations)
            {
                if (s.Region == null) continue;
                float e = Vector2.Distance(s.Box.center, p);
                if (e < d) { d = e; best = s.Box; }
            }
            return best;
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
            FixedStations.Reset();
            for (int i = stations.Count - 1; i >= 0; i--) Remove(i);
        }

        public static List<Saved> Save()
        {
            var list = new List<Saved>(stations.Count);
            foreach (var s in stations) if (!s.Fixed) list.Add(new Saved { Scene = s.Scene, Local = s.Local, Kind = s.Kind });
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
