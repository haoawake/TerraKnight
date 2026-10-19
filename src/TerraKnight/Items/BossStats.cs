using System.Collections.Generic;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The bosses' life and defense, Terraria's own: each of Hallownest's bosses stands in for the
    /// Terraria boss of its stage (阶段对照.md) and has that boss's life on the save's difficulty, all
    /// its parts together, and its defense. Terraria's life on a difficulty is NPC.ScaleStats's: the
    /// base (NPC.SetDefaults) times GameDifficultyData.EnemyMaxLifeMultiplier (1, 2, 3 for Classic,
    /// Expert, Master), the boss's own Expert tweak (ScaleStats_ByDifficulty_Tweaks, Remap from
    /// Classic to Expert) and on Master CommonMasterBossLifeReduction's 0.85 (Remap from Expert to
    /// Master). Normal is Classic, Hard halfway to Expert, as Journey's slider would have it.
    /// A worm (the Eater of Worlds, the Destroyer) is taken at half: its life is spread over dozens of
    /// segments a piercing weapon hits at once, against one body here. A boss in phases has a whole one
    /// each phase, the bodies up together sharing it (<see cref="BossPhases"/>); the Oblobbles, two at
    /// once from the start, share one out. Godhome's
    /// gods drop the Moon Lord's spoils, one class each, and have the Moon Lord's life.
    /// </summary>
    internal static class BossStats
    {
        /// <summary>One Terraria boss: its parts' base life and Expert tweaks, and its defense.</summary>
        internal sealed class Boss
        {
            public string Name;
            public int Defense;
            public (int Life, float Expert, float Master)[] Parts;
            /// <summary>Of each one fought together, and of a worm.</summary>
            public float Share = 1f;

            /// <summary>Terraria's life on a difficulty (0 Classic, 1 Expert, 2 Master; Hard is 0.5), all parts, the share taken.</summary>
            public int LifeAt(float t)
            {
                double sum = 0;
                foreach (var p in Parts)
                {
                    double life = p.Life * (1 + t);
                    life *= Mathf.Lerp(1f, p.Expert, Mathf.Clamp01(t));
                    life *= Mathf.Lerp(1f, p.Master, Mathf.Clamp01(t - 1f));
                    sum += System.Math.Round(life);
                }
                return Mathf.Max(1, (int)System.Math.Round(sum * Share));
            }

            public Boss Of(float share, string note = null) =>
                new Boss { Name = note == null ? Name : $"{Name}（{note}）", Defense = Defense, Parts = Parts, Share = Share * share };
        }

        /// <summary>The difficulty's place between Classic (0), Expert (1) and Master (2).</summary>
        public static float Tier(DifficultyLevel d) => d switch
        {
            DifficultyLevel.Normal => 0f,
            DifficultyLevel.Hard => 0.5f,
            DifficultyLevel.Expert => 1f,
            _ => 2f,
        };

        const float M = 0.85f;  // NPC.CommonMasterBossLifeReduction

        static Boss T(string name, int defense, params (int, float, float)[] parts) => new Boss { Name = name, Defense = defense, Parts = parts };

        // NPC.SetDefaults' lifeMax and defense, ScaleStats_ByDifficulty_Tweaks' num2 and num3.
        static readonly Boss KingSlime = T("史莱姆王", 10, (2000, 0.7f, M));
        static readonly Boss EyeOfCthulhu = T("克苏鲁之眼", 12, (2800, 0.65f, M));
        static readonly Boss EaterOfWorlds = T("世界吞噬怪", 4, (10050, 0.7f, M)).Of(0.5f, "一半");
        static readonly Boss BrainOfCthulhu = T("克苏鲁之脑", 14, (1250, 0.85f, M), (100 * 20, 0.85f, M));
        static readonly Boss Skeletron = T("骷髅王", 10, (4400, 1f, M), (600 * 2, 1.3f, M));
        static readonly Boss QueenBee = T("蜂后", 8, (3400, 0.7f, M));
        static readonly Boss Deerclops = T("鹿角怪", 10, (7000, 0.85f, M));
        static readonly Boss WallOfFlesh = T("血肉墙", 12, (8000, 0.7f, M));
        static readonly Boss QueenSlime = T("史莱姆皇后", 26, (18000, 0.8f, M));
        static readonly Boss Destroyer = T("毁灭者", 30, (80000, 0.75f, M)).Of(0.5f, "一半");
        static readonly Boss Twins = T("双子魔眼", 10, (20000 + 23000, 0.75f, M));
        static readonly Boss Plantera = T("世纪之花", 14, (30000, 0.7f, M));
        static readonly Boss Pumpking = T("南瓜王", 40, (26000, 0.65f, M));
        static readonly Boss IceQueen = T("冰雪女王", 38, (34000, 0.65f, M));
        static readonly Boss Mothron = T("蛾怪", 30, (6000, 1f, 1f));
        static readonly Boss MartianSaucer = T("火星飞碟", 20, (10000 + 5000 * 2 + 3500 * 2, 0.85f, 1f));
        static readonly Boss Golem = T("石巨人", 26, (15000 + 25000, 0.75f, M));
        static readonly Boss Betsy = T("双足翼龙", 38, (50000, 0.75f, M));
        static readonly Boss DukeFishron = T("猪龙鱼公爵", 50, (60000, 0.65f, M));
        static readonly Boss EmpressOfLight = T("光之女皇", 50, (70000, 0.7f, M));
        static readonly Boss LunaticCultist = T("拜月教邪教徒", 42, (32000, 0.75f, M));
        static readonly Boss MoonLord = T("月亮领主", 50, (50000 + 45000 + 25000 * 2, 0.75f, M));

        /// <summary>A boss in Godhome's own rooms is an endgame fight: the Lunatic Cultist's life, by its stage's.</summary>
        static readonly Boss GodhomeRematch = LunaticCultist;

        /// <summary>Each stage's own boss: what a Godhome rematch is measured by.</summary>
        static readonly Boss[] stageBoss = { KingSlime, EyeOfCthulhu, EaterOfWorlds, WallOfFlesh, Plantera, GodhomeRematch };

        internal sealed class Ref
        {
            public Boss Boss;
            public string Record;
            public string Key;
            /// <summary>The stage the boss is fought at (its bestiary's).</summary>
            public int Stage;
        }

        static Ref R(int stage, Boss boss) => new Ref { Boss = boss, Stage = stage };

        /// <summary>By the bestiary's record; "record@stage" for a boss met again at another stage (Hornet in Kingdom's Edge, the dreams).</summary>
        static readonly Dictionary<string, Ref> table = new Dictionary<string, Ref>
        {
            // Stage 0: the Forgotten Crossroads, King Slime.
            ["FalseKnight"] = R(0, KingSlime),
            ["BigFly"] = R(0, KingSlime.Of(0.8f, "八成")),
            ["Mawlek"] = R(0, KingSlime),
            // Stage 1: Greenpath, the Eye of Cthulhu.
            ["Hornet"] = R(1, EyeOfCthulhu),
            ["MegaMossCharger"] = R(1, EyeOfCthulhu.Of(0.8f, "八成")),
            ["GhostAladar"] = R(1, EyeOfCthulhu.Of(0.6f, "六成")),
            ["GhostNoEyes"] = R(1, EyeOfCthulhu.Of(0.6f, "六成")),
            // Stage 2: the Eater of Worlds or the Brain of Cthulhu, Skeletron.
            ["DungDefender"] = R(2, EaterOfWorlds),
            ["TraitorLord"] = R(2, EaterOfWorlds),
            ["MantisLord"] = R(2, Skeletron),
            ["MageLord"] = R(2, BrainOfCthulhu),
            ["MegaJellyfish"] = R(2, BrainOfCthulhu),
            ["FlukeMother"] = R(2, BrainOfCthulhu.Of(0.8f, "八成")),
            ["MegaBeamMiner"] = R(2, Deerclops.Of(0.8f, "八成")),
            ["GhostHu"] = R(2, BrainOfCthulhu.Of(0.6f, "六成")),
            ["GhostMarmu"] = R(2, BrainOfCthulhu.Of(0.6f, "六成")),
            // Stage 3: the Queen Bee, the underworld, the Wall of Flesh.
            ["BlackKnight"] = R(3, WallOfFlesh),
            ["HiveKnight"] = R(4, QueenSlime.Of(0.6f, "蜂巢阶段4")),
            ["JarCollector"] = R(3, WallOfFlesh.Of(0.8f, "八成")),
            ["MimicSpider"] = R(3, WallOfFlesh.Of(0.8f, "八成")),
            ["GhostGalien"] = R(3, WallOfFlesh.Of(0.5f, "一半")),
            ["GhostXero"] = R(3, WallOfFlesh.Of(0.5f, "一半")),
            // The three dream bosses come with the Dream Nail, long before the end: each a step past its waking self
            // (the Failed Champion with Skeletron, the Soul Tyrant with the Wall of Flesh, the Lost Kin with the
            // Destroyer), in their dreams and in Godhome alike. (Their old stand-ins' spoils went to Godhome's gods.)
            ["FalseKnight@5"] = R(2, Skeletron),
            ["MageLord@5"] = R(3, WallOfFlesh),
            ["InfectedKnight@5"] = R(4, Destroyer),
            // Stage 4: hardmode, the Queen Slime (the Colosseum, in Kingdom's Edge), the mechanical bosses, Plantera.
            ["Oblobble"] = R(4, QueenSlime.Of(0.5f, "两只分")),
            ["LobsterLancer"] = R(4, QueenSlime.Of(0.6f, "六成")),
            ["Zote"] = R(4, QueenSlime.Of(0.2f, "两成")),
            ["Hornet@4"] = R(4, Destroyer),
            ["GhostMarkoth"] = R(4, Twins),
            ["InfectedKnight"] = R(4, Plantera),
            // Stage 5: after Plantera: the moons, the eclipse, the Martians, Golem, the Old One's Army, Duke Fishron, the Empress.
            ["Grimm"] = R(5, Pumpking),
            ["WhiteDefender"] = R(5, IceQueen),
            ["GreyPrince"] = R(5, Mothron),
            ["NightmareGrimm"] = R(5, DukeFishron),
            ["HollowKnight"] = R(5, Golem),

            // Godhome's gods, one class of the Moon Lord's spoils each, and the Radiance: the Moon Lord.
            ["Nailsage"] = R(5, MoonLord),
            ["NailBros"] = R(5, MoonLord),
            ["Paintmaster"] = R(5, MoonLord),
            ["HollowKnightPrime"] = R(5, MoonLord),
            ["FinalBoss"] = R(5, MoonLord),
        };

        static BossStats()
        {
            foreach(var pair in table)
            {
                int at=pair.Key.IndexOf('@');
                pair.Value.Record=at<0 ? pair.Key : pair.Key.Substring(0,at);
                pair.Value.Key=pair.Key;
            }
        }

        /// <summary>A boss's Terraria stand-in at a stage: its own entry there, else its usual one.</summary>
        public static Ref Of(string record, int stage)
        {
            if (record == null) return null;
            if (table.TryGetValue(record + "@" + stage, out var r)) return r;
            return table.TryGetValue(record, out r) ? r : null;
        }

        /// <summary>Godhome's rooms (the Hall of Gods, the pantheons): the game's rematches.</summary>
        public static bool InGodhome(string scene) => scene != null && scene.StartsWith("GG_");

        /// <summary>
        /// The health unit distributed by EnemyStats over native phases and bodies. Registered bosses
        /// have explicit progression-based whole-fight budgets, with the stand-in as fallback.
        /// Godhome's existing challenge profile is selected using godhomeTier.
        /// </summary>
        internal static float CombatLifeShare(int stage) => stage switch
            { 0 => .715f, 1 => .754f, 2 => .75f, 3 => .7425f, 4 => .725f, _ => .64f };

        public static int LifeOf(Ref r, string scene, DifficultyLevel d, float godhomeTier = 1f)
        {
            if (InGodhome(scene)) return GodhomeLifeOf(r, scene, d, GodhomeBalance.Current(scene, godhomeTier));
            if (BossHealthBudgets.TryTotal(r, scene, d, out float total))
                return BossHealthBudgets.UnitLife(r, scene, total);
            float t = Tier(d);
            float life = r.Boss.LifeAt(t);
            // Preserve the native FSM health and phases; reduce only displayed/combat life.
            life *= CombatLifeShare(r.Stage);
            return Mathf.Max(1, Mathf.RoundToInt(life));
        }

        internal static int GodhomeLifeOf(Ref r,string scene,DifficultyLevel d,GodhomeBalance.Profile profile)
        {
            if (BossHealthBudgets.TryTotal(r, scene, d, out float budget))
                return BossHealthBudgets.UnitLife(r, scene, budget * profile.Life);
            // Budget the whole fight, not a full floor for each native phase. Late bosses
            // retain their stronger stand-in; early and formerly fragile bosses get a floor.
            float world=r.Boss.LifeAt(Tier(d))*CombatLifeShare(r.Stage);
            float total=Mathf.Max(world*1.5f,GodhomeRematch.LifeAt(Tier(d))*1.5f)*profile.Life;
            float phases=BossPhases.WholeFactor(r.Record,scene);
            return Mathf.Max(1,Mathf.RoundToInt(total/Mathf.Max(.01f,phases)));
        }

        /// <summary>
        /// The healing potion a boss drops at its death and its phases' ends (NPC.DoDeathEvents_DropBossPotionsAndHearts's,
        /// by the boss's own stage, the user's): the Lesser Healing Potion at stages 0 and 1, the Healing Potion at 2 and 3,
        /// the Greater at 4, the Super at 5 (each no later than its own stage, so never locked out of the drop). A stage 5
        /// boss gives Ultimate Healing Potions besides (<see cref="UltimatePotion"/>).
        /// </summary>
        public static ItemType PotionOf(Ref r)
        {
            switch (Mathf.Clamp(r.Stage, 0, 5))
            {
                case 0: case 1: return ItemType.LesserHealingPotion;
                case 2: case 3: return ItemType.HealingPotion;
                case 4: return ItemType.GreaterHealingPotion;
                default: return ItemType.SuperHealingPotion;
            }
        }

        /// <summary>Whether the boss's death gives Ultimate Healing Potions too (one to three): a stage 5 boss's.</summary>
        public static bool UltimatePotion(Ref r) => r != null && r.Stage >= 5;

        /// <summary>The boss's Terraria defense (Master leaves it as it is).</summary>
        public static int DefenseOf(Ref r) => r.Boss.Defense;

        /// <summary>Dev: every entry, for the balance report.</summary>
        public static IEnumerable<KeyValuePair<string, Ref>> DevAll => table;
    }
}
