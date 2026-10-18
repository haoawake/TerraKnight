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
        // Master Mode relics (Items/Relics.cs): to look at, nothing made at them.
        RelicAbsoluteRadiance, RelicBigBuzzer, RelicBigFly, RelicBlackKnight, RelicDungDefender, RelicEnragedGuardian,
        RelicFalseKnight, RelicFalseKnightAt5, RelicFinalBoss, RelicFlukeMother, RelicGhostAladar, RelicGhostGalien,
        RelicGhostHu, RelicGhostMarkoth, RelicGhostMarmu, RelicGhostNoEyes, RelicGhostXero, RelicGreyPrince,
        RelicGrimm, RelicHiveKnight, RelicHollowKnight, RelicHollowKnightPrime, RelicHornet, RelicHornetAt4,
        RelicInfectedKnight, RelicInfectedKnightAt5, RelicJarCollector, RelicLobsterLancer, RelicMageKnight, RelicMageLord,
        RelicMageLordAt5, RelicMantisLord, RelicMawlek, RelicMegaBeamMiner, RelicMegaJellyfish, RelicMegaMossCharger,
        RelicMimicSpider, RelicNailBros, RelicNailsage, RelicNightmareGrimm, RelicOblobble, RelicPaintmaster,
        RelicPaleLurker, RelicSistersOfBattle, RelicTraitorLord, RelicWhiteDefender, RelicWingedNosk, RelicZote, DyeVat,
        Campfire,
        // Tombstones (Tiles_85's eleven styles, dropped on death: Items.Tombstones) and the Abigail's Flower growing by them.
        Tombstone, GraveMarker, CrossGraveMarker, Headstone, Gravestone, Obelisk,
        RichGravestone1, RichGravestone2, RichGravestone3, RichGravestone4, RichGravestone5, AbigailsFlower,
        /// <summary>The bed: a right click sets the spawn point there (World.Beds).</summary>
        Bed,
        /// <summary>The player's other storages, opened as the Piggy Bank is (Chests: Player.bank2 .. bank4).</summary>
        Safe, DefendersForge, VoidVault,
        /// <summary>
        /// Not a crafting station either: Terraria's Wooden Chair (tile 15, ItemID 34), the furniture a
        /// townsperson is put on. It stands where the player set it and is knocked back into its item like
        /// any other furniture; the housing menu binds a townsperson to one (ChairNear).
        /// </summary>
        Chair,
        /// <summary>
        /// Terraria's Wooden Door (tile 10 shut, 11 open to either side) and Wooden Table (tile 14): what a house is
        /// built with besides the chair (World/StationsHousing.cs, World/Housing.cs).
        /// </summary>
        Door, DoorOpenRight, DoorOpenLeft, Table,
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
    /// that scene's own coordinates, and is kept in the mod's save. The pieces TileObjectData gives a
    /// direction (the chair, the bed, the relics) go down facing the way the player faces. A Hallownest bench or Dirtmouth
    /// still makes everything its stage has reached (Items.Recipes), these add to that anywhere.
    /// </summary>
    internal sealed partial class Stations : MonoBehaviour
    {
        const float Px = Effects.TerraParticles.Px;
        /// <summary>
        /// Behind what Terraria draws over its tiles: the minions, pets and shots (Effects.FxBatch.BehindZ, 0.0047, going
        /// nearer with each draw), the townsfolk (0.00395) and the player (0.0039); in front of the light's glow (0.0048)
        /// and of the blocks (0.006).
        /// </summary>
        const float Z = 0.00475f;

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
            /// <summary>A picture of the mod's own (a relic's), whole, 32 px a cell, instead of a Terraria sheet's tiles.</summary>
            public string Custom;
            /// <summary>
            /// TileObjectData.Direction: it is placed facing the way the player faces (PlaceLeft, and an alternate
            /// PlaceRight), the right-facing one RightX px further along its sheet (the chair's 18, the bed's 72); a
            /// relic's floating sculpture is drawn turned (DrawMasterTrophies' FlipHorizontally).
            /// </summary>
            public bool Faces;
            public int RightX;
        }

        /// <summary>The relics: three-cell pedestals with floating sculptures, occupying 3x4 cells.</summary>
        static Stations()
        {
            foreach (var kv in Items.Relics.Looks)
            {
                var h = new int[kv.Value.H];
                for (int i = 0; i < h.Length; i++) h[i] = 16;
                // Tile 617: Direction PlaceLeft with a PlaceRight alternate (frameY 72 on: the sculpture turned).
                specs[kv.Key] = new Spec { Custom = kv.Value.Texture, W = kv.Value.W, H = kv.Value.H, Heights = h, Item = kv.Value.Item, Counts = new string[0], Faces = true };
            }
            AddHousingSpecs();
        }

        static Spec Grave(int style, ItemType item) =>
            new Spec { Texture = "Tiles_85", W = 2, H = 2, StyleX = style * 36, Heights = new[] { 16, 16 }, DrawY = 2, Item = item, Counts = new string[0] };

        /// <summary>A tombstone (Tiles_85, any style).</summary>
        public static bool IsGrave(StationKind k) => k >= StationKind.Tombstone && k <= StationKind.RichGravestone5;

        static readonly Dictionary<StationKind, Spec> specs = new Dictionary<StationKind, Spec>
        {
            [StationKind.Tombstone] = Grave(0, ItemType.Tombstone),
            [StationKind.GraveMarker] = Grave(1, ItemType.GraveMarker),
            [StationKind.CrossGraveMarker] = Grave(2, ItemType.CrossGraveMarker),
            [StationKind.Headstone] = Grave(3, ItemType.Headstone),
            [StationKind.Gravestone] = Grave(4, ItemType.Gravestone),
            [StationKind.Obelisk] = Grave(5, ItemType.Obelisk),
            [StationKind.RichGravestone1] = Grave(6, ItemType.RichGravestone1),
            [StationKind.RichGravestone2] = Grave(7, ItemType.RichGravestone2),
            [StationKind.RichGravestone3] = Grave(8, ItemType.RichGravestone3),
            [StationKind.RichGravestone4] = Grave(9, ItemType.RichGravestone4),
            [StationKind.RichGravestone5] = Grave(10, ItemType.RichGravestone5),
            [StationKind.AbigailsFlower] = new Spec { Texture = "Tiles_624", W = 1, H = 1, Heights = new[] { 16 }, Item = ItemType.AbigailsFlower, Counts = new string[0] },
            // Tile 79: Style4x2 with CoordinateHeights {16, 18}; style 0 the wooden bed.
            // Tiles 97 (Style2x2 {16, 18}), 463 (Style3x4 {16, 16, 16, 16}, drawn 2 px down), 491 (Style3x3 {16, 16, 16}, 2 px down).
            [StationKind.Safe] = new Spec { Texture = "Tiles_97", W = 2, H = 2, Heights = new[] { 16, 18 }, Item = ItemType.Safe, Counts = new string[0] },
            [StationKind.DefendersForge] = new Spec { Texture = "Tiles_463", W = 3, H = 4, Heights = new[] { 16, 16, 16, 16 }, DrawY = 2, Item = ItemType.DefendersForge, Counts = new string[0] },
            [StationKind.VoidVault] = new Spec { Texture = "Tiles_491", W = 3, H = 3, Heights = new[] { 16, 16, 16 }, DrawY = 2, Item = ItemType.VoidVault, Counts = new string[0] },
            // Tile 79: Direction PlaceLeft, its PlaceRight alternate the next 72 px across (StyleMultiplier 2, StyleWrapLimit 2).
            [StationKind.Bed] = new Spec { Texture = "Tiles_79", W = 4, H = 2, Heights = new[] { 16, 18 }, Item = ItemType.Bed, Counts = new string[0], Faces = true, RightX = 72 },
            [StationKind.Campfire] = new Spec { Texture = "Tiles_215", W = 3, H = 2, Heights = new[] { 16, 16 }, Item = ItemType.Campfire, Counts = new[] { "篝火" } },
            [StationKind.DyeVat] = new Spec { Texture = "Tiles_228", W = 3, H = 2, Heights = new[] { 16, 18 }, Item = ItemType.DyeVat, Counts = new[] { "染缸" } },
            // TileObjectData: Style2x1 with CoordinateHeights {18} (tiles 18, 16), Style3x2 {16, 18} (17, 114).
            [StationKind.WorkBench] = new Spec { Texture = "Tiles_18", W = 2, H = 1, Heights = new[] { 18 }, Item = ItemType.WorkBench, Counts = new[] { "工作台" }, SolidTop = true },
            // Tile 15 (TileID.Chairs): Style1x2, CoordinateHeights {16, 18}; Direction PlaceLeft (frameX 0, the seat to the
            // left) and the PlaceRight alternate at frameX 18, each style a row of the sheet.
            [StationKind.Chair] = new Spec { Texture = "Tiles_15", W = 1, H = 2, Heights = new[] { 16, 18 }, Item = ItemType.Chair, Counts = new string[0], Faces = true, RightX = 18 },
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
            /// <summary>Placed facing right (Spec.Faces: the player faced right as it went down).</summary>
            public bool Right;
            public SpriteRenderer Renderer;
            public RelicDisplay Relic;
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
            /// <summary>Placed facing right (Station.Right).</summary>
            public bool Right;
        }

        public static Stations Instance { get; private set; }
        static readonly List<Station> stations = new List<Station>();
        static readonly Dictionary<StationKind, Sprite> sprites = new Dictionary<StationKind, Sprite>(), rightSprites = new Dictionary<StationKind, Sprite>();
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
        /// <summary>Dev: the reach box used for placing and breaking.</summary>
        public static Rect DevReach(HeroController hero) => Reach(hero);

        static Rect Reach(HeroController hero)
        {
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            const float cell = CellGrid.CellSize;
            return Rect.MinMaxRect(b.min.x - 5f * cell, b.min.y - 4f * cell, b.max.x + 5f * cell, b.max.y + 4f * cell);
        }

        // ------------------------------------------------------------------ placing

        /// <summary>
        /// Whether furniture of this kind goes down facing right: one TileObjectData gives a direction (Spec.Faces) and
        /// the player faces right (TileObject.CanPlace with Player.direction: the way he walked last, not the cursor).
        /// </summary>
        public static bool FacesRight(StationKind kind, HeroController hero) =>
            hero != null && specs.TryGetValue(kind, out var spec) && spec.Faces && hero.cState.facingRight;

        /// <summary>A station set where the cursor points: snapped to the grid, on even ground under all its cells, room for it, in reach; facing the way the player faces (FacesRight).</summary>
        public static bool TryPlace(StationKind kind, HeroController hero, Vector2 aim)
        {
            if (!Net.WorldSync.CanInteract) return false;
            if (hero == null || !specs.ContainsKey(kind)) return false;
            if (!FindSpot(kind, hero, aim, out var bottom)) return false;
            if (BossRooms.Locked(bottom + new Vector2(0f, 0.1f))) { BossRooms.Notice(bottom); return false; }
            var at = CellGrid.WorldToCell(bottom + new Vector2(0f, 0.1f));
            var region = TerraWorld.RegionAt(at.x, at.y) ?? TerrainSystem.Current?.Region;
            if (region == null) return false;
            bool right = FacesRight(kind, hero);
            if (Net.WorldSync.Guest)
            {
                var local = bottom - (Vector2)region.Root.position;
                Net.WorldSync.Submit(new Net.WorldSync.Action { Op = "station", Scene = region.SceneName, X = local.x, Y = local.y, Kind = (int)kind, Count = TerraMod.Instance.Inventory.Infinite ? 0 : 1, Placed = right });
                return true;
            }
            stations.Add(new Station { Kind = kind, Region = region, Scene = region.SceneName, Local = bottom - (Vector2)region.Root.position, Right = right });
            TerraMod.Instance?.PlaySound("Dig_" + Random.Range(0, 3));
            DevPlaced++;
            return true;
        }

        /// <summary>
        /// A station the world sets down where something came to rest (a tombstone rolled to a stop, a flower grown): even
        /// ground under it and room for it, as for the player's, but no reach; not in a boss's room. The player's own once
        /// set: saved, knocked down with a pickaxe. True when it stands.
        /// </summary>
        public static bool TryPlaceNatural(StationKind kind, Vector2 aim, out Vector2 bottom)
        {
            bottom = default;
            if (!specs.ContainsKey(kind) || !FindSpot(kind, null, aim, out bottom)) return false;
            if (BossRooms.Locked(bottom + new Vector2(0f, 0.1f))) return false;
            var at = CellGrid.WorldToCell(bottom + new Vector2(0f, 0.1f));
            var region = TerraWorld.RegionAt(at.x, at.y) ?? TerrainSystem.Current?.Region;
            if (region == null) return false;
            var local = bottom - (Vector2)region.Root.position;
            if (Net.WorldSync.Guest)
            {
                Net.WorldSync.Submit(new Net.WorldSync.Action { Op = "station", Scene = region.SceneName, X = local.x, Y = local.y, Kind = (int)kind, Count = 0 });
                return true;
            }
            stations.Add(new Station { Kind = kind, Region = region, Scene = region.SceneName, Local = local });
            return true;
        }

        /// <summary>A station into a scene by its own coordinates, loaded or not (it stands there once the scene comes in).</summary>
        public static void PlaceIn(StationKind kind, string scene, Vector2 local)
        {
            if (specs.ContainsKey(kind) && !string.IsNullOrEmpty(scene)) stations.Add(new Station { Kind = kind, Scene = scene, Local = local });
        }

        /// <summary>The boxes of the stations of some kinds standing in the loaded scenes (world units).</summary>
        public static List<(StationKind Kind, Rect Box)> Standing(System.Func<StationKind, bool> which)
        {
            var list = new List<(StationKind, Rect)>();
            foreach (var s in stations)
                if (s.Region != null && which(s.Kind)) list.Add((s.Kind, s.Box));
            return list;
        }

        /// <summary>Dev: why the last FindSpot found no spot.</summary>
        public static string DevWhy;
        public static int DevDrops;

        /// <summary>The stations a touch gives a buff by (Spec.Buff), with their items: carried, they give it too (Items.BuffStationItems).</summary>
        public static IEnumerable<(ItemType Item, int Buff)> BuffItems()
        {
            foreach (var kv in specs)
                if (kv.Value.Buff > 0 && kv.Value.Item != ItemType.None) yield return (kv.Value.Item, kv.Value.Buff);
        }

        /// <summary>
        /// The floor under a point: the scene's own colliders first (the exact contact point a raycast
        /// gives), and where the scene has none, the cell grid — the grid holds the player's own blocks
        /// the moment they are laid, while their colliders are only rebuilt per chunk later.
        /// </summary>
        static bool SpotGround(float x, float fromY, out float y)
        {
            const int mask = 1 << ChunkColliders.TerrainLayer;
            const float reach = 1.5f;
            var hit = Physics2D.Raycast(new Vector2(x, fromY), Vector2.down, reach, mask);
            if (hit.collider != null) { y = hit.point.y; return true; }
            y = 0f;
            // The grid within the same reach as the ray.
            var cell = CellGrid.WorldToCell(new Vector2(x, fromY));
            int lowest = CellGrid.WorldToCell(new Vector2(x, fromY - reach)).y;
            for (int cy = cell.y; cy >= lowest; cy--)
            {
                if (TerraWorld.RegionAt(cell.x, cy) == null || (!TerraWorld.IsSolid(cell.x, cy) && !TerraWorld.IsPlatform(cell.x, cy))) continue;
                y = CellGrid.CellCenter(cell.x, cy).y + CellGrid.CellSize / 2f;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Whether the cell grid holds a block in any cell this box covers. Outside the loaded regions
        /// nothing is read as solid (the same rule the grid itself is read by).
        /// </summary>
        static bool GridInside(Rect box)
        {
            var lo = CellGrid.WorldToCell(new Vector2(box.xMin, box.yMin));
            var hi = CellGrid.WorldToCell(new Vector2(box.xMax, box.yMax));
            for (int cx = lo.x; cx <= hi.x; cx++)
                for (int cy = lo.y; cy <= hi.y; cy++)
                {
                    if (TerraWorld.RegionAt(cx, cy) == null) continue;
                    if (TerraWorld.IsSolid(cx, cy) || TerraWorld.IsPlatform(cx, cy)) return true;
                }
            return false;
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
            if (!SpotGround(x, aim.y + 0.25f, out float y)) { DevWhy = "下面没有地"; return false; }
            for (int i = 0; i < spec.W; i++)
            {
                float cx = x - w / 2f + (i + 0.5f) * cell;
                if (!SpotGround(cx, y + 0.1f, out float gy) || Mathf.Abs(gy - y) > 0.06f) { DevWhy = $"第 {i + 1} 格下面不平（地面 {y:F2}）"; return false; }
            }
            var centre = new Vector2(x, y + h / 2f + 0.02f);
            var inWay = Physics2D.OverlapBox(centre, new Vector2(w, h) * 0.9f, 0f, mask);
            if (inWay != null) { DevWhy = "挡着 " + inWay.name; return false; }
            var box = new Rect(x - w / 2f, y, w, h);
            // Inset a little: one standing on another's top only touches it.
            var inner = new Rect(box.x + 0.02f, box.y + 0.02f, box.width - 0.04f, box.height - 0.04f);
            // And the cell grid: the player's own walls are in it the moment they are laid, while their
            // colliders are only rebuilt later, so the overlap above can still look straight through a
            // wall that has just been built and set the furniture down inside it.
            if (GridInside(inner)) { DevWhy = "那格里有方块"; return false; }
            foreach (var s in stations)
                if (s.Region != null && s.Box.Overlaps(inner)) { DevWhy = "和别的工作站重叠"; return false; }
            // The chest's own box, not its middle: a chest is two cells wide, so one set down in the cell
            // beside it used to slip through the point test while the two pictures overlapped.
            foreach (var c in Chests.Boxes())
                if (c.Overlaps(inner)) { DevWhy = "和箱子重叠"; return false; }
            if (hero != null && !Reach(hero).Overlaps(box)) { DevWhy = "够不着"; return false; }
            // A door goes between a floor and a ceiling (StationsHousing).
            if (!DoorFits(kind, box)) { DevWhy = "门要装在地面和天花板之间"; return false; }
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
                // A door opens or shuts (StationsHousing: WorldGen.OpenDoor / CloseDoor).
                if (IsDoor(s.Kind))
                {
                    ToggleDoor(s, hero);
                    return true;
                }
                if (s.Kind == StationKind.PiggyBank)
                {
                    Chests.ToggleBank(s.Scene, s.Region, s.Local);
                    return true;
                }
                if (s.Kind == StationKind.Bed)
                {
                    Beds.Toggle(s.Scene, s.Local);
                    return true;
                }
                if (s.Kind == StationKind.Safe || s.Kind == StationKind.DefendersForge || s.Kind == StationKind.VoidVault)
                {
                    Chests.ToggleBank(s.Scene, s.Region, s.Local, s.Kind == StationKind.Safe ? 1 : s.Kind == StationKind.DefendersForge ? 2 : 3, s.Spec.Item);
                    return true;
                }
                // Abigail's Flower, picked as a plant is (Main.tileCut): the flower to the bag.
                if (s.Kind == StationKind.AbigailsFlower && !Net.WorldSync.Guest)
                {
                    var at = s.Box.center;
                    Remove(stations.IndexOf(s));
                    var inv = TerraMod.Instance?.Inventory;
                    if (inv == null || inv.Pickup(ItemType.AbigailsFlower, 1) > 0) WorldItems.Drop(ItemStack.Of(ItemType.AbigailsFlower), at);
                    else UI.PopupText.Show(ItemDb.Get(ItemType.AbigailsFlower), 1, at);
                    TerraMod.Instance?.PlaySound("Grab");
                    DevFlowersPicked++;
                    return true;
                }
                if (s.Spec.Buff > 0)
                {
                    // The buff stations' last until death (the user's: a spike's death too); the cake's Sugar Rush its 2 minutes.
                    Player.PlayerBuffs.Add(s.Spec.Buff, Player.PlayerBuffs.UntilDeath(s.Spec.Buff) ? Player.PlayerBuffs.Forever : s.Spec.BuffTicks);
                    TerraMod.Instance?.PlaySound(s.Kind == StationKind.SliceOfCake ? "Item_2" : s.Kind == StationKind.AmmoBox ? "Item_149" : s.Kind == StationKind.SharpeningStation ? "Item_37" : "Item_4");
                    DevBuffsTaken++;
                    return true;
                }
            }
            return false;
        }

        public static int DevBuffsTaken, DevFlowersPicked;

        /// <summary>A bed stands at this bottom middle of a scene (its own coordinates), and where that is in the world now.</summary>
        public static bool BedAt(string scene, Vector2 local, out Vector2 world)
        {
            world = default;
            foreach (var s in stations)
            {
                if (s.Kind != StationKind.Bed || s.Scene != scene || (s.Local - local).sqrMagnitude > 0.0004f) continue;
                var region = s.Region ?? RegionNamed(scene);
                if (region == null) return false;
                world = region.Root.TransformPoint(s.Local);
                return true;
            }
            return false;
        }

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
                if (s.Kind == StationKind.Bed) Beds.Gone(s.Scene, s.Local);
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
            int dust = s.Kind == StationKind.WorkBench || s.Kind == StationKind.Chair || s.Kind == StationKind.Loom || s.Kind == StationKind.Sawmill || IsDoor(s.Kind) || s.Kind == StationKind.Table ? 7 : 1;
            float hw = s.Spec.W * 8f, hh = s.Spec.H * 8f;
            for (int k = 0; k < n; k++) p.NewDust(px - new Vector2(hw, hh), (int)(hw * 2f), (int)(hh * 2f), dust);
        }

        /// <summary>
        /// The chair a click landed on (a Wooden Chair the player set down), and where on it: a
        /// townsperson put there stands on the chair's seat. False when the click wasn't on a chair —
        /// the street or the air, which means its usual place along the street instead.
        /// </summary>
        public static bool ChairNear(Vector2 at, out float x, out float y) => ChairNear(at, out x, out y, out _);

        /// <summary>The chair a click landed on, where its seat is, and which way it faces (1 right, -1 left).</summary>
        public static bool ChairNear(Vector2 at, out float x, out float y, out int facing)
        {
            x = at.x;
            y = at.y;
            facing = 0;
            float best = float.MaxValue;
            bool found = false;
            foreach (var s in stations)
            {
                if (s.Kind != StationKind.Chair || s.Region == null) continue;
                var b = s.Box;
                // The chair's own box, a little wider all round: the whole seat is clickable, the tiles
                // beside it are not.
                // Its own box and a hand's width around it: the seat is clickable, the street and the
                // bench below it are not (a chair standing over the bench must not swallow its clicks).
                var reach = new Rect(b.xMin - 0.15f, b.yMin - 0.1f, b.width + 0.3f, b.height + 0.2f);
                if (!reach.Contains(at)) continue;
                float d = Vector2.Distance(b.center, at);
                if (d >= best) continue;
                best = d;
                x = b.center.x;
                // The seat, not the floor at the chair's feet: the top of the chair's lower cell.
                // Standing at b.yMin put the townsperson on the ground underneath the chair.
                y = b.yMin + CellGrid.CellSize;
                facing = s.Right ? 1 : -1;
                found = true;
            }
            return found;
        }

        // ------------------------------------------------------------------ each frame

        void Update()
        {
            if (TerrainSystem.Current == null) return;
            FixedStations.Ensure();
            foreach (var s in stations) s.Region = RegionNamed(s.Scene);
            // Doors walked into open, and shut again behind (StationsHousing).
            PushDoors();
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
                // A door hangs on its ceiling and its floor both (StationsHousing).
                bool now = IsDoor(s.Kind) ? DoorHeld(s) : HasGround(s);
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
                    if (s.Kind == StationKind.Bed) Beds.Gone(s.Scene, s.Local);
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
                    if (s.Relic != null) s.Relic.SetVisible(false);
                    continue;
                }
                if (s.Renderer == null)
                {
                    var go = new GameObject("Terra Station " + s.Kind);
                    go.transform.SetParent(s.Region.Props ?? s.Region.Root, false);
                    s.Renderer = go.AddComponent<SpriteRenderer>();
                    if (s.Spec.Custom != null)
                    {
                        s.Relic = go.AddComponent<RelicDisplay>();
                        s.Relic.Init(s.Spec.Custom, s.Renderer);
                    }
                }
                s.Renderer.enabled = true;
                if (s.Relic != null)
                {
                    s.Relic.SetVisible(true);
                    s.Relic.Face(s.Right);
                }
                s.Renderer.sprite = SpriteOf(s.Kind, s.Right);
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
                if (IsDoor(s.Kind)) DoorBody(s);
            }
        }

        /// <summary>
        /// A station's look, put together from its tiles in the sheet (18 px apart across; down by each row's height and
        /// 2), the right-facing alternate's columns (Spec.RightX on) for one placed facing right.
        /// </summary>
        static Sprite SpriteOf(StationKind kind, bool right = false)
        {
            var spec = specs[kind];
            right &= spec.RightX > 0 && spec.Custom == null;
            var cache = right ? rightSprites : sprites;
            if (cache.TryGetValue(kind, out var sp) && sp != null) return sp;
            if (spec.Custom != null)
            {
                var pic = Items.Relics.Texture(spec.Custom);
                // Only the pedestal stays fixed. RelicDisplay draws and animates the sculpture above it.
                sp = Sprite.Create(pic, new Rect(0, 0, pic.width, 16), new Vector2(0.5f, 0f), pic.width / (spec.W * 16f * Px), 0, SpriteMeshType.FullRect);
                sprites[kind] = sp;
                return sp;
            }
            var src = TerrariaAssets.Pixels(spec.Texture, out int sw, out int sh);
            int x0 = spec.StyleX + (right ? spec.RightX : 0);
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
                            int sx = x0 + col * 18 + x, sy = sy0 + y;
                            int dx = col * 16 + x, dy = dy0 + y;
                            if (sx >= sw || sy >= sh) continue;
                            dst[(h - 1 - dy) * w + dx] = src[(sh - 1 - sy) * sw + sx];
                        }
                sy0 += th + 2;
                dy0 += th;
            }
            var tex = TerrariaAssets.Make(spec.Texture + "#station" + x0, dst, w, h);
            sp = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0f), 1f / Px, 0, SpriteMeshType.FullRect);
            cache[kind] = sp;
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

        /// <summary>Dev: the right click on the nearest loaded Piggy Bank (the bank opens, or shuts).</summary>
        public static bool DevToggleBank(Vector2 from)
        {
            Station best = null;
            foreach (var s in stations)
                if (s.Kind == StationKind.PiggyBank && s.Region != null && (best == null || Vector2.Distance(s.Box.center, from) < Vector2.Distance(best.Box.center, from))) best = s;
            if (best == null) return false;
            Chests.ToggleBank(best.Scene, best.Region, best.Local);
            return true;
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
            foreach (var s in stations) if (!s.Fixed) list.Add(new Saved { Scene = s.Scene, Local = s.Local, Kind = s.Kind, Right = s.Right });
            return list;
        }

        public static void Load(IEnumerable<Saved> saved)
        {
            Clear();
            if (saved == null) return;
            foreach (var s in saved)
                if (specs.ContainsKey(s.Kind)) stations.Add(new Station { Scene = s.Scene, Local = s.Local, Kind = s.Kind, Right = s.Right && specs[s.Kind].Faces });
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
