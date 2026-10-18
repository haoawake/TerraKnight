using System.Collections.Generic;
using GlobalEnums;

namespace TerraKnight.World
{
    /// <summary>
    /// Which regional torch (World.RegionalTorches, styles 1..48) belongs to a scene: its area, or the part of it with a
    /// look of its own. The scene's name decides first (the rooms of each sub-area by name, then each area's prefix), as
    /// the map's own zones (SceneManager.mapZone) mostly name the bigger area a room is drawn in (the Grimm Troupe's
    /// tent says CROSSROADS, the Abyss's entrance WATERWAYS); a scene being played whose zone names one of the sub-areas
    /// (an Archives, Soul Sanctum, Watcher's Spire room...) takes that one; anything else the area World.Areas names. The
    /// Forgotten Crossroads, once infected (PlayerData.crossroadsInfected), give the Infected Crossroads' torch.
    /// </summary>
    internal static class RegionalTorchAreas
    {
        public const int KingsPass = 1, Dirtmouth = 2, Crossroads = 3, InfectedCrossroads = 4, Greenpath = 5, LakeOfUnn = 6, FogCanyon = 7,
            Archives = 8, QueensStation = 9, FungalWastes = 10, MantisVillage = 11, CityOfTears = 12, SoulSanctum = 13, WatchersSpire = 14,
            PleasureHouse = 15, TowerOfLove = 16, RoyalWaterways = 17, IsmasGrove = 18, JunkPit = 19, CrystalPeak = 20, HallownestsCrown = 21,
            RestingGrounds = 22, SpiritsGlade = 23, BlueLake = 24, HowlingCliffs = 25, JonisRepose = 26, Deepnest = 27, BeastsDen = 28,
            WeaversDen = 29, DistantVillage = 30, FailedTramway = 31, KingdomsEdge = 32, CastOffShell = 33, Colosseum = 34, Hive = 35,
            QueensGardens = 36, StoneSanctuary = 37, OvergrownMound = 38, AncestralMound = 39, AncientBasin = 40, WhitePalace = 41,
            PathOfPain = 42, Abyss = 43, Birthplace = 44, BlackEggTemple = 45, Dream = 46, Godhome = 47, GrimmTroupe = 48;

        /// <summary>The sub-areas' rooms, and the rooms the area prefixes would put elsewhere.</summary>
        static readonly Dictionary<string, int> rooms = new Dictionary<string, int>
        {
            // Dirtmouth's houses and shops.
            ["Room_shop"] = Dirtmouth, ["Room_mapper"] = Dirtmouth, ["Room_Jinn"] = Dirtmouth, ["Room_Ouiji"] = Dirtmouth,
            ["Room_Sly_Storeroom"] = Dirtmouth, ["Room_Bretta"] = Dirtmouth, ["Room_Bretta_Basement"] = Dirtmouth, ["Room_Town_Stag_Station"] = Dirtmouth,
            // Off the Crossroads.
            ["Crossroads_50"] = BlueLake, ["Crossroads_ShamanTemple"] = AncestralMound, ["Crossroads_46b"] = RestingGrounds, ["Crossroads_49b"] = CityOfTears,
            ["Room_temple"] = BlackEggTemple,
            // Greenpath's lake, its sanctuary and the gardens' edge.
            ["Fungus1_26"] = LakeOfUnn, ["Fungus1_Slug"] = LakeOfUnn, ["Room_Slug_Shrine"] = LakeOfUnn,
            ["Fungus1_34"] = StoneSanctuary, ["Fungus1_35"] = StoneSanctuary, ["Fungus1_36"] = StoneSanctuary, ["Fungus1_37"] = StoneSanctuary,
            ["Fungus1_23"] = QueensGardens, ["Fungus1_24"] = QueensGardens, ["Fungus1_28"] = HowlingCliffs,
            // Fog Canyon's and the gardens' sides.
            ["Fungus3_archive"] = Archives, ["Fungus3_archive_02"] = Archives, ["Fungus3_44"] = OvergrownMound, ["Room_Fungus_Shaman"] = OvergrownMound,
            // The Fungal Wastes'.
            ["Fungus2_01"] = QueensStation, ["Fungus2_02"] = QueensStation, ["Fungus2_34"] = QueensStation,
            ["Fungus2_14"] = MantisVillage, ["Fungus2_15"] = MantisVillage, ["Fungus2_31"] = MantisVillage,
            // The City's towers and houses.
            ["Ruins1_23"] = SoulSanctum, ["Ruins1_24"] = SoulSanctum, ["Ruins1_25"] = SoulSanctum, ["Ruins1_26"] = SoulSanctum,
            ["Ruins1_30"] = SoulSanctum, ["Ruins1_32"] = SoulSanctum,
            ["Ruins2_03"] = WatchersSpire, ["Ruins2_03b"] = WatchersSpire, ["Ruins2_Watcher_Room"] = WatchersSpire,
            ["Ruins_Bathhouse"] = PleasureHouse, ["Ruins2_11"] = TowerOfLove, ["Ruins2_11_b"] = TowerOfLove,
            ["Ruins2_10"] = RestingGrounds, ["Room_nailsmith"] = CityOfTears,
            // Under the City.
            ["Waterways_13"] = IsmasGrove, ["GG_Waterways"] = JunkPit, ["Abyss_01"] = RoyalWaterways, ["Abyss_02"] = RoyalWaterways,
            // The Peak's top, the Resting Grounds' glade.
            ["Mines_34"] = HallownestsCrown, ["RestingGrounds_08"] = SpiritsGlade,
            // The cliffs.
            ["Cliffs_05"] = JonisRepose, ["Room_nailmaster"] = HowlingCliffs,
            // Deepnest's.
            ["Deepnest_Spider_Town"] = BeastsDen, ["Room_spider_small"] = BeastsDen, ["Deepnest_45_v02"] = WeaversDen,
            ["Deepnest_09"] = DistantVillage, ["Deepnest_10"] = DistantVillage, ["Deepnest_26"] = FailedTramway, ["Deepnest_26b"] = FailedTramway,
            ["Room_Tram"] = FailedTramway, ["Room_Tram_RG"] = FailedTramway,
            // Kingdom's Edge's.
            ["Room_Wyrm"] = CastOffShell, ["GG_Lurker"] = KingdomsEdge, ["Room_nailmaster_03"] = KingdomsEdge,
            // The Abyss's own, under the Basin; the Birthplace and its dream.
            ["Abyss_15"] = Birthplace, ["Dream_Abyss"] = Birthplace,
            // The Path of Pain.
            ["White_Palace_17"] = PathOfPain, ["White_Palace_18"] = PathOfPain, ["White_Palace_19"] = PathOfPain, ["White_Palace_20"] = PathOfPain,
        };

        /// <summary>The areas by prefix, the longer before the shorter they start.</summary>
        static readonly (string Prefix, int Style)[] prefixes =
        {
            ("Tutorial", KingsPass),
            ("Town", Dirtmouth), ("Room_Town", Dirtmouth),
            ("Crossroads", Crossroads), ("Room_Charm_Shop", Crossroads), ("Room_Mender_House", Crossroads), ("Room_ruinhouse", Crossroads),
            // The game's own test rooms, drawn on the Crossroads' map.
            ("_test_", Crossroads),
            ("Fungus1", Greenpath), ("Room_nailmaster_02", Greenpath),
            ("Fungus2", FungalWastes),
            ("Fungus3", QueensGardens), ("Room_Queen", QueensGardens),
            ("Ruins", CityOfTears),
            ("GG_Pipeway", RoyalWaterways), ("Room_GG_Shortcut", RoyalWaterways), ("Waterways", RoyalWaterways),
            ("Mines", CrystalPeak),
            ("RestingGrounds", RestingGrounds), ("Room_Mansion", RestingGrounds),
            ("Cliffs", HowlingCliffs),
            ("Deepnest_East", KingdomsEdge),
            ("Deepnest", Deepnest), ("Room_Mask_Maker", Deepnest),
            ("Room_Colosseum", Colosseum),
            ("Hive", Hive),
            ("Abyss_17", AncientBasin), ("Abyss_18", AncientBasin), ("Abyss_19", AncientBasin),
            ("Abyss_06", Abyss), ("Abyss_08", Abyss), ("Abyss_09", Abyss), ("Abyss_1", Abyss), ("Abyss_Lighthouse", Abyss),
            ("Abyss", AncientBasin),
            ("White_Palace", WhitePalace),
            ("Room_Final", BlackEggTemple),
            ("Dream_", Dream),
            ("GG_", Godhome),
            ("Grimm_", GrimmTroupe),
        };

        /// <summary>The map's zones that name a sub-area (or an area) outright: a scene being played in one takes its torch.</summary>
        static readonly Dictionary<MapZone, int> subZones = new Dictionary<MapZone, int>
        {
            [MapZone.ACID_LAKE] = LakeOfUnn, [MapZone.MONOMON_ARCHIVE] = Archives, [MapZone.QUEENS_STATION] = QueensStation,
            [MapZone.MANTIS_VILLAGE] = MantisVillage, [MapZone.SOUL_SOCIETY] = SoulSanctum, [MapZone.MAGE_TOWER] = SoulSanctum,
            [MapZone.LURIENS_TOWER] = WatchersSpire, [MapZone.LOVE_TOWER] = TowerOfLove, [MapZone.ISMAS_GROVE] = IsmasGrove,
            [MapZone.GODSEEKER_WASTE] = JunkPit, [MapZone.PEAK] = HallownestsCrown, [MapZone.GLADE] = SpiritsGlade, [MapZone.BLUE_LAKE] = BlueLake,
            [MapZone.JONI_GRAVE] = JonisRepose, [MapZone.BEASTS_DEN] = BeastsDen, [MapZone.DISTANT_VILLAGE] = DistantVillage,
            [MapZone.RUINED_TRAMWAY] = FailedTramway, [MapZone.TRAM_UPPER] = FailedTramway, [MapZone.TRAM_LOWER] = FailedTramway,
            [MapZone.WYRMSKIN] = CastOffShell, [MapZone.NOEYES_TEMPLE] = StoneSanctuary, [MapZone.OVERGROWN_MOUND] = OvergrownMound,
            [MapZone.SHAMAN_TEMPLE] = AncestralMound,
        };

        /// <summary>Every zone, for a scene the names don't place.</summary>
        static readonly Dictionary<MapZone, int> zones = new Dictionary<MapZone, int>
        {
            [MapZone.KINGS_PASS] = KingsPass, [MapZone.TOWN] = Dirtmouth, [MapZone.CROSSROADS] = Crossroads, [MapZone.GREEN_PATH] = Greenpath,
            [MapZone.FOG_CANYON] = FogCanyon, [MapZone.WASTES] = FungalWastes, [MapZone.CITY] = CityOfTears, [MapZone.ROYAL_QUARTER] = CityOfTears,
            [MapZone.KINGS_STATION] = CityOfTears, [MapZone.WATERWAYS] = RoyalWaterways, [MapZone.MINES] = CrystalPeak, [MapZone.CRYSTAL_MOUND] = CrystalPeak,
            [MapZone.RESTING_GROUNDS] = RestingGrounds, [MapZone.CLIFFS] = HowlingCliffs, [MapZone.DEEPNEST] = Deepnest, [MapZone.OUTSKIRTS] = KingdomsEdge,
            [MapZone.COLOSSEUM] = Colosseum, [MapZone.HIVE] = Hive, [MapZone.ROYAL_GARDENS] = QueensGardens, [MapZone.BONE_FOREST] = StoneSanctuary,
            [MapZone.ABYSS] = AncientBasin, [MapZone.PALACE_GROUNDS] = AncientBasin, [MapZone.WHITE_PALACE] = WhitePalace, [MapZone.ABYSS_DEEP] = Abyss,
            [MapZone.FINAL_BOSS] = BlackEggTemple, [MapZone.DREAM_WORLD] = Dream, [MapZone.GODS_GLORY] = Godhome,
        };

        /// <summary>World.Areas' areas (its AreaName), the last word.</summary>
        static readonly Dictionary<string, int> areas = new Dictionary<string, int>
        {
            ["国王山道"] = KingsPass, ["德特茅斯"] = Dirtmouth, ["遗忘十字路"] = Crossroads, ["苍绿之径"] = Greenpath, ["雾之峡谷"] = FogCanyon,
            ["真菌荒地"] = FungalWastes, ["泪水之城"] = CityOfTears, ["皇家水道"] = RoyalWaterways, ["水晶山峰"] = CrystalPeak, ["安息之地"] = RestingGrounds,
            ["呼啸悬崖"] = HowlingCliffs, ["深巢"] = Deepnest, ["野兽巢穴"] = BeastsDen, ["王国边缘"] = KingdomsEdge, ["愚人斗兽场"] = Colosseum,
            ["蜂巢"] = Hive, ["王后花园"] = QueensGardens, ["远古盆地"] = AncientBasin, ["白色宫殿"] = WhitePalace, ["深渊"] = Abyss,
            ["黑卵神殿"] = BlackEggTemple, ["梦境"] = Dream, ["神居"] = Godhome, ["格林剧团"] = GrimmTroupe,
        };

        /// <summary>A scene's name without an additive part's suffix (a boss room's "_boss", "_boss_defeated", "_preload").</summary>
        public static string Room(string scene)
        {
            if (string.IsNullOrEmpty(scene)) return scene;
            foreach (var suffix in new[] { "_boss_defeated", "_boss", "_preload" })
                if (scene.EndsWith(suffix) && scene.Length > suffix.Length) return scene.Substring(0, scene.Length - suffix.Length);
            return scene;
        }

        /// <summary>The scene's torch by its name alone (0 when the names don't place it), the Crossroads' uninfected.</summary>
        public static int ByName(string scene)
        {
            scene = Room(scene);
            if (string.IsNullOrEmpty(scene)) return 0;
            if (rooms.TryGetValue(scene, out int style)) return style;
            // Fog Canyon's rooms among the Fungus3 scenes, as World.Areas tells them from the gardens'.
            if (scene.StartsWith("Fungus3", System.StringComparison.Ordinal) && Areas.AreaName(scene) == "雾之峡谷") return FogCanyon;
            foreach (var (prefix, s) in prefixes)
                if (scene.StartsWith(prefix, System.StringComparison.Ordinal)) return s;
            return 0;
        }

        /// <summary>A zone's torch, from the sub-areas' or every zone's table; 0 for none.</summary>
        public static int ByZone(MapZone zone, bool subAreasOnly) =>
            subZones.TryGetValue(zone, out int s) ? s : !subAreasOnly && zones.TryGetValue(zone, out s) ? s : 0;

        /// <summary>World.Areas' area of the scene, as a torch; 0 for "圣巢" and the unknown.</summary>
        public static int ByArea(string scene) => areas.TryGetValue(Areas.AreaName(Room(scene)), out int s) ? s : 0;

        /// <summary>
        /// The torch of a scene's area: the scene being played's sub-area zone, else its name, else its zone, else
        /// World.Areas' area; the Crossroads' infected once PlayerData says so. 0 (the plain torch) for nowhere known.
        /// </summary>
        public static int StyleOf(string scene)
        {
            var gm = GameManager.instance;
            MapZone? zone = gm != null && gm.sm != null && Room(gm.sceneName) == Room(scene) ? gm.sm.mapZone : (MapZone?)null;
            int style = zone.HasValue ? ByZone(zone.Value, true) : 0;
            if (style == 0) style = ByName(scene);
            if (style == 0 && zone.HasValue) style = ByZone(zone.Value, false);
            if (style == 0) style = ByArea(scene);
            if (style == Crossroads && PlayerData.instance != null && PlayerData.instance.crossroadsInfected) style = InfectedCrossroads;
            return style;
        }
    }
}
