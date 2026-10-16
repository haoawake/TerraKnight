using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// How deep into Hallownest a scene is, 0 to 3, as Terraria's world gets harder going down:
    /// the rock needs a stronger pickaxe, enemies take more hits and hit harder, and the shop
    /// sells better gear once the player has been that far.
    /// </summary>
    internal static class Areas
    {
        public static int TierOf(string scene)
        {
            if (string.IsNullOrEmpty(scene)) return 0;
            if (Starts(scene, "White_Palace", "GG_", "Dream_", "Grimm_", "Room_Final", "Room_Tram", "Abyss_15")) return 3;
            if (Starts(scene, "Deepnest", "Abyss", "Hive", "RestingGrounds", "Room_Colosseum", "Room_Mansion", "Ruins2_11")) return 2;
            if (Starts(scene, "Fungus2", "Fungus3", "Ruins", "Waterways", "Mines", "Room_Queen", "Room_Mender")) return 1;
            return 0;
        }

        /// <summary>
        /// The Terraria wall shown where the scene's own rock is dug away, in the area's colours:
        /// the blue-grey cave wall round King's Pass and the Crossroads, the jungle's in Greenpath,
        /// flowers in Queen's Gardens, pearlstone in Fog Canyon, orange sandstone in the Fungal
        /// Wastes, blue and green dungeon brick in the City and the Waterways, amethyst in Crystal
        /// Peak, the purple-grey cave wall in the Resting Grounds, spider walls in Deepnest, pale
        /// marble on Kingdom's Edge, the hive's own, granite in the Ancient Basin, obsidian in the
        /// Abyss and the Black Egg, pearlstone brick in the White Palace, gold brick in Godhome,
        /// hallowed sandstone in dreams, crimstone in the Grimm Troupe's tent, Lihzahrd brick in
        /// the Colosseum; plain stone in Dirtmouth, the cliffs and anywhere else.
        /// </summary>
        public static string WallFor(string scene)
        {
            if (string.IsNullOrEmpty(scene)) return "Wall_1";
            if (Starts(scene, "GG_Pipeway", "GG_Waterways", "Room_GG_Shortcut", "Waterways")) return "Wall_8";
            if (Starts(scene, "GG_Lurker", "Deepnest_East", "Room_Wyrm")) return "Wall_178";
            if (Starts(scene, "Room_Colosseum")) return "Wall_87";
            if (Starts(scene, "Deepnest", "Room_spider", "Room_Mask_Maker")) return "Wall_62";
            if (Starts(scene, "Hive")) return "Wall_86";
            if (Starts(scene, "Abyss_17", "Abyss_18", "Abyss_19")) return "Wall_180";
            if (Starts(scene, "Abyss_06", "Abyss_08", "Abyss_09", "Abyss_1", "Abyss_Lighthouse")) return "Wall_79";
            if (Starts(scene, "Abyss")) return "Wall_180";
            if (Starts(scene, "White_Palace")) return "Wall_22";
            if (Starts(scene, "Room_temple", "Room_Final")) return "Wall_20";
            if (Starts(scene, "GG_")) return "Wall_10";
            if (Starts(scene, "Dream_")) return "Wall_222";
            if (Starts(scene, "Grimm_")) return "Wall_83";
            if (Starts(scene, "Mines")) return "Wall_48";
            if (Starts(scene, "RestingGrounds", "Room_Mansion")) return "Wall_58";
            if (Starts(scene, "Ruins")) return "Wall_7";
            if (Starts(scene, "Fungus2", "Room_Fungus_Shaman")) return "Wall_187";
            if (Starts(scene, "Fungus3")) return IsFogCanyon(scene) ? "Wall_28" : "Wall_65";
            if (Starts(scene, "Room_Queen")) return "Wall_65";
            if (Starts(scene, "Fungus1", "Room_nailmaster_02", "Room_Slug_Shrine")) return "Wall_64";
            if (Starts(scene, "Tutorial", "Crossroads", "Room_Mender_House", "Room_Charm_Shop", "Room_Ouiji")) return "Wall_57";
            return "Wall_1";
        }

        /// <summary>The area a scene belongs to, as the map names it (the monster book's tag).</summary>
        /// <summary>Hollow Knight's own name for each of the mod's areas (its Map Zones sheet), for the other languages.</summary>
        static readonly System.Collections.Generic.Dictionary<string, string> zones = new System.Collections.Generic.Dictionary<string, string>
        {
            ["皇家水道"] = "WATERWAYS", ["王国边缘"] = "OUTSKIRTS", ["愚人斗兽场"] = "COLOSSEUM", ["斗兽场"] = "COLOSSEUM", ["野兽巢穴"] = "BEASTS_DEN",
            ["深巢"] = "DEEPNEST", ["蜂巢"] = "HIVE", ["远古盆地"] = "ABYSS", ["古老盆地"] = "ABYSS", ["深渊"] = "ABYSS_DEEP", ["白色宫殿"] = "WHITE_PALACE",
            ["黑卵神殿"] = "FINAL_BOSS", ["神居"] = "GODS_GLORY", ["梦境"] = "DREAM_WORLD", ["水晶山峰"] = "MINES", ["安息之地"] = "RESTING_GROUNDS",
            ["泪水之城"] = "CITY", ["真菌荒地"] = "WASTES", ["雾之峡谷"] = "FOG_CANYON", ["王后花园"] = "ROYAL_GARDENS", ["苍绿之径"] = "GREEN_PATH",
            ["呼啸悬崖"] = "CLIFFS", ["国王山道"] = "KINGS_PASS", ["德特茅斯"] = "TOWN", ["遗忘十字路"] = "CROSSROADS",
        };

        /// <summary>An area's name (AreaName's Chinese, the mod's key for it) as shown in the language now: Hollow Knight's own where it has one.</summary>
        public static string Display(string area)
        {
            if (string.IsNullOrEmpty(area) || Lang.IsSource) return area;
            if (zones.TryGetValue(area, out var key))
            {
                try
                {
                    if (TeamCherry.Localization.Language.Has(key, "Map Zones")) return TeamCherry.Localization.Language.Get(key, "Map Zones");
                }
                catch { }
            }
            return Lang.T(area);
        }

        public static string AreaName(string scene)
        {
            if (string.IsNullOrEmpty(scene)) return "未知";
            if (Starts(scene, "GG_Pipeway", "GG_Waterways", "Room_GG_Shortcut", "Waterways")) return "皇家水道";
            if (Starts(scene, "GG_Lurker", "Deepnest_East", "Room_Wyrm")) return "王国边缘";
            if (Starts(scene, "Room_Colosseum")) return "愚人斗兽场";
            if (Starts(scene, "Deepnest_Spider_Town", "Room_spider")) return "野兽巢穴";
            if (Starts(scene, "Deepnest", "Room_Mask_Maker")) return "深巢";
            if (Starts(scene, "Hive")) return "蜂巢";
            if (Starts(scene, "Abyss_17", "Abyss_18", "Abyss_19")) return "远古盆地";
            if (Starts(scene, "Abyss_06", "Abyss_08", "Abyss_09", "Abyss_1", "Abyss_Lighthouse")) return "深渊";
            if (Starts(scene, "Abyss")) return "远古盆地";
            if (Starts(scene, "White_Palace")) return "白色宫殿";
            if (Starts(scene, "Room_temple", "Room_Final")) return "黑卵神殿";
            if (Starts(scene, "GG_")) return "神居";
            if (Starts(scene, "Dream_")) return "梦境";
            if (Starts(scene, "Grimm_")) return "格林剧团";
            if (Starts(scene, "Mines")) return "水晶山峰";
            if (Starts(scene, "RestingGrounds", "Room_Mansion")) return "安息之地";
            if (Starts(scene, "Ruins")) return "泪水之城";
            if (Starts(scene, "Fungus2", "Room_Fungus_Shaman")) return "真菌荒地";
            if (Starts(scene, "Fungus3")) return IsFogCanyon(scene) ? "雾之峡谷" : "王后花园";
            if (Starts(scene, "Room_Queen")) return "王后花园";
            if (Starts(scene, "Fungus1", "Room_nailmaster_02", "Room_Slug_Shrine")) return "苍绿之径";
            if (Starts(scene, "Cliffs", "Room_nailmaster")) return "呼啸悬崖";
            if (Starts(scene, "Tutorial")) return "国王山道";
            if (Starts(scene, "Town", "Room_Town", "Room_shop", "Room_mapper", "Room_Bretta", "Room_Ouiji", "Room_Sly")) return "德特茅斯";
            if (Starts(scene, "Crossroads", "Room_Mender_House", "Room_Charm_Shop", "Room_ruinhouse")) return "遗忘十字路";
            return "圣巢";
        }

        /// <summary>
        /// The Terraria map background behind an entry's portrait in the monster book (MapBG_N), in
        /// the area's colours: dark caves round the Crossroads, the graveyard's grey on the surface,
        /// the jungle in Greenpath and the gardens, glowing mushrooms in the Fungal Wastes, crystal
        /// caves in Crystal Peak and Fog Canyon, the dungeon in the Waterways, spider caves in
        /// Deepnest, marble on Kingdom's Edge, the hive, granite in the Basin, the void's black, snow
        /// for the White Palace, the eclipse over Godhome, the pink moon over dreams, the blood moon
        /// over the Grimm Troupe, the temple in the Colosseum.
        /// </summary>
        public static string BestiaryBackground(string scene) => BackgroundOfArea(AreaName(scene));

        public static string BackgroundOfArea(string area)
        {
            switch (area)
            {
                case "德特茅斯": case "呼啸悬崖": return "MapBG27";
                case "苍绿之径": return "MapBG13";
                case "王后花园": return "MapBG9";
                case "雾之峡谷": return "MapBG36";
                case "真菌荒地": return "MapBG21";
                case "泪水之城": return "MapBG34";
                case "皇家水道": return "MapBG5";
                case "水晶山峰": return "MapBG22";
                case "安息之地": return "MapBG17";
                case "深巢": case "野兽巢穴": return "MapBG19";
                case "王国边缘": return "MapBG18";
                case "蜂巢": return "MapBG16";
                case "远古盆地": return "MapBG40";
                case "深渊": case "黑卵神殿": return "MapBG25";
                case "白色宫殿": return "MapBG12";
                case "神居": return "MapBG29";
                case "梦境": return "MapBG28";
                case "格林剧团": return "MapBG26";
                case "愚人斗兽场": return "MapBG14";
                default: return "MapBG32";
            }
        }

        /// <summary>Fog Canyon's rooms and the Teacher's Archives among the Fungus3 scenes (the rest are Queen's Gardens).</summary>
        static bool IsFogCanyon(string scene) =>
            Starts(scene, "Fungus3_01", "Fungus3_02", "Fungus3_03", "Fungus3_24", "Fungus3_25", "Fungus3_26", "Fungus3_27", "Fungus3_28", "Fungus3_30",
                   "Fungus3_35", "Fungus3_44", "Fungus3_47", "Fungus3_archive");

        /// <summary>Pickaxe power the scene's own rock needs (Terraria: ores and bricks need stronger picks).</summary>
        public static int PickRequired(int tier) => tier switch { 1 => 40, 2 => 55, 3 => 65, _ => 0 };

        public const int MaxLevel = 5;

        /// <summary>
        /// How dangerous a scene's enemies are, 0 to 5, by the Knight's own road through Hallownest:
        /// each stage is where he goes with the ability that opened it, matched to Terraria's
        /// progression. 0 King's Pass, Dirtmouth, the Crossroads and Greenpath, up to Hornet and the
        /// Mothwing Cloak (the Eye of Cthulhu and its shield); 1 with the cloak, the Fungal Wastes
        /// and the Mantis Village (the Mantis Claw), Fog Canyon and the Howling Cliffs; 2 with the
        /// claw, the City of Tears, the Royal Waterways, Crystal Peak (the Crystal Heart) and the
        /// Resting Grounds (the Eater of Worlds or the Brain, the dungeon); 3 with the heart,
        /// Deepnest, the Beasts' Den, the Ancient Basin (the Broken Vessel: the Wall of Flesh and
        /// the Monarch Wings), the Queen's Gardens and the tram; 4 with the wings, Kingdom's Edge,
        /// the Hive, the Colosseum and the Abyss (hardmode, to the Shade Cloak); 5 the White Palace,
        /// Godhome, dreams, the Grimm Troupe and the end. What the townsfolk sell and which
        /// recipes open goes by the abilities got (Items.Shop.UnlockedStage).
        /// </summary>
        public static int LevelOf(string scene)
        {
            if (string.IsNullOrEmpty(scene)) return 0;
            // Godhome-named rooms that are ordinary parts of the world (spec-hkenemies 1.4).
            if (Starts(scene, "GG_Pipeway", "GG_Waterways")) return 2;
            if (Starts(scene, "GG_Lurker")) return 4;
            // The dream bosses' rooms (and their Godhome halls) at their own stage (Items.BossStats); their tables stay "record@5" (EnemyDrops.VariantLevel).
            if (scene == "Dream_01_False_Knight" || scene == "GG_Failed_Champion") return 2;
            if (scene == "Dream_02_Mage_Lord" || scene == "GG_Soul_Tyrant") return 3;
            if (scene == "Dream_03_Infected_Knight" || scene == "GG_Lost_Kin") return 4;
            if (Starts(scene, "White_Palace", "GG_", "Dream_", "Grimm_", "Room_Final", "Abyss_15")) return 5;
            if (Starts(scene, "Room_Tram")) return 3;
            switch (AreaName(scene))
            {
                case "国王山道": case "德特茅斯": case "遗忘十字路": case "苍绿之径": case "黑卵神殿": return 0;
                case "真菌荒地": case "雾之峡谷": case "呼啸悬崖": return 1;
                case "泪水之城": case "皇家水道": case "水晶山峰": case "安息之地": return 2;
                case "深巢": case "野兽巢穴": case "远古盆地": case "王后花园": return 3;
                case "王国边缘": case "蜂巢": case "愚人斗兽场": case "深渊": return 4;
                case "白色宫殿": case "神居": case "梦境": case "格林剧团": return 5;
                default: return 0;
            }
        }

        /// <summary>A Master Mode blue slime's life: 25 (NPC.SetDefaults, type 1) times Master Mode's 3 (GameDifficultyData.EnemyMaxLifeMultiplier).</summary>
        const int MasterSlimeLife = 25 * 3;
        /// <summary>Hallownest's weakest enemies' own health: crawlids, tiktiks, vengeflies.</summary>
        const int WeakestEnemyHp = 8;

        /// <summary>One Master Mode slime's toughness for the weakest ordinary enemy.</summary>
        const int Toughness = 1;

        /// <summary>
        /// Terraria hit points per hit point of an enemy's own, the same everywhere: the weakest
        /// enemies come out as one Master Mode blue slime (75), and every other enemy in
        /// proportion to its own health, so the deeper ones are tougher as they are in the game.
        /// A husk (15) is 141 on Master before area scaling. The save's
        /// difficulty takes its share of it (<see cref="GameDifficulty"/>).
        /// </summary>
        public static float HealthScale => MasterHealthScale * GameDifficulty.EnemyFactor;

        const float MasterHealthScale = MasterSlimeLife * Toughness / (float)WeakestEnemyHp;

        /// <summary>
        /// Modest late-game toughness: 1.25 at stage 4 and 2 at stage 5 for ordinary enemies.
        /// This replaces the old 2/5 multipliers, which also compounded with catch-up scaling.
        /// </summary>
        public static float LevelHealth(int level, bool boss) => level == 4 ? 1.25f : level == 5 && !boss ? 2f : 1f;

        /// <summary>
        /// The level an area's enemies (not its bosses) are met at: the progress reached once it is past the area's own, so the
        /// earlier areas keep up with the stage unlocked (the user's, 2026-10-16), a little weaker (<see cref="CatchUp"/>).
        /// </summary>
        public static int StatLevel(int level) => Mathf.Max(level, Mathf.Clamp(Items.Shop.UnlockedStage(), 0, MaxLevel));

        /// <summary>How much of the stage's strength an area brought up to it has: nine tenths (1 at its own level).</summary>
        public static float CatchUp(int level) => StatLevel(level) > level ? 0.9f : 1f;

        /// <summary>
        /// An enemy's life per point of its own health at the level it is met at: its area's toughness, brought up to the
        /// stage's (the stage's common enemies' usual health over its area's, times the stage's toughness) and weakened a little.
        /// </summary>
        public static float LifeScale(int level)
        {
            int at = StatLevel(level);
            if (at == level) return HealthScale * LevelHealth(level, false);
            float lift = Items.EnemyDrops.TypicalHp(at) / Mathf.Max(1f, Items.EnemyDrops.TypicalHp(level));
            return HealthScale * LevelHealth(at, false) * Mathf.Max(1f, lift) * CatchUp(level);
        }

        /// <summary>
        /// Terraria defense of the level's enemies: each hit loses half of it (NPC.StrikeNPC).
        /// Master Mode leaves defense as it is, so the surface's is a blue slime's 2; deeper ones
        /// follow the common enemies of the matching Terraria stage (cave skeletons, hornets,
        /// fire imps, wraiths and armored skeletons, the post-Plantera dungeon).
        /// </summary>
        public static int Defense(int level) => level switch { 1 => 4, 2 => 8, 3 => 10, 4 => 20, 5 => 30, _ => 2 };

        /// <summary>
        /// The level's enemies' Terraria knockBackResist: about what the common enemies of the
        /// matching stage have (slimes 1, demon eyes 0.8, zombies 0.5 on the surface; skeletons
        /// 0.5 and bats 0.8 in the caves; angry bones 0.8, hornets 0.5; fire imps 0.5, demons 0.8;
        /// armored skeletons, werewolves, possessed armor 0.4, wraiths 0.7 in hardmode).
        /// </summary>
        public static float KnockbackTaken(int level) => level switch { 1 => 0.65f, 2 => 0.6f, 3 => 0.55f, 4 => 0.45f, 5 => 0.4f, _ => 0.75f };

        /// <summary>Terraria damage a hit costing one mask deals here (contact damage of Terraria's enemies at that stage, on Master; the save's difficulty takes its share).</summary>
        public static int DamagePerMask(int level) => Mathf.Max(1, Mathf.RoundToInt(MasterDamagePerMask(level) * GameDifficulty.EnemyFactor));

        /// <summary>A mask's Terraria damage on Master, before defense: what a hit costs is that less half the defense, times the difficulty's share (TerraStats.TakeMasks).</summary>
        public static int MasterDamagePerMask(int level) => level switch { 1 => 30, 2 => 42, 3 => 58, 4 => 76, 5 => 100, _ => 20 };

        /// <summary>Bosses and elites hit harder than the common enemies around them, as Terraria's bosses do (settings Gameplay.BossDamage / EliteDamage).</summary>
        public static float BossDamage = 2f, EliteDamage = 1.5f;

        public static string TierName(int tier) => tier switch
        {
            1 => "真菌荒地、雾之峡谷、泪水之城、水晶山峰、皇家水道",
            2 => "深巢、深渊、蜂巢、安息之地、斗兽场",
            3 => "白色宫殿、神居、梦境",
            _ => "国王山道、德特茅斯、遗忘十字路、苍绿之径、呼啸悬崖",
        };

        static bool Starts(string s, params string[] prefixes)
        {
            foreach (var p in prefixes) if (s.StartsWith(p)) return true;
            return false;
        }
    }
}
