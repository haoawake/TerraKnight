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
    /// segments a piercing weapon hits at once, against one body here. Bosses fought together (the
    /// Watcher Knights, the Mantis Lords, the Nailmasters, the Oblobbles) share one boss out. Godhome's
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
            ["MantisLord"] = R(2, Skeletron.Of(1f / 3f, "三只分")),
            ["MageLord"] = R(2, BrainOfCthulhu),
            ["MegaJellyfish"] = R(2, BrainOfCthulhu),
            ["FlukeMother"] = R(2, BrainOfCthulhu.Of(0.8f, "八成")),
            ["MegaBeamMiner"] = R(2, Deerclops.Of(0.8f, "八成")),
            ["GhostHu"] = R(2, BrainOfCthulhu.Of(0.6f, "六成")),
            ["GhostMarmu"] = R(2, BrainOfCthulhu.Of(0.6f, "六成")),
            // Stage 3: the Queen Bee, the underworld, the Wall of Flesh.
            ["BlackKnight"] = R(3, WallOfFlesh.Of(1f / 6f, "六个分")),
            ["HiveKnight"] = R(3, QueenBee),
            ["JarCollector"] = R(3, WallOfFlesh.Of(0.8f, "八成")),
            ["MimicSpider"] = R(3, WallOfFlesh.Of(0.8f, "八成")),
            ["GhostGalien"] = R(3, WallOfFlesh.Of(0.5f, "一半")),
            ["GhostXero"] = R(3, WallOfFlesh.Of(0.5f, "一半")),
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
            ["FalseKnight@5"] = R(5, MartianSaucer),
            ["MageLord@5"] = R(5, Betsy),
            ["InfectedKnight@5"] = R(5, EmpressOfLight),
            // Godhome's gods, one class of the Moon Lord's spoils each, and the Radiance: the Moon Lord.
            ["Nailsage"] = R(5, MoonLord),
            ["NailBros"] = R(5, MoonLord.Of(0.5f, "两人分")),
            ["Paintmaster"] = R(5, MoonLord),
            ["HollowKnightPrime"] = R(5, MoonLord),
            ["FinalBoss"] = R(5, MoonLord),
        };

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
        /// A boss's Terraria life where it is met, on a difficulty: its stand-in's; in Godhome, where
        /// the player comes with the endgame's weapons, a boss of an earlier stage is as much of the
        /// Lunatic Cultist as it was of its own stage's boss, and Ascended's extra health over Attuned
        /// (<paramref name="godhomeTier"/>) is on top.
        /// </summary>
        public static int LifeOf(Ref r, string scene, DifficultyLevel d, float godhomeTier = 1f)
        {
            float t = Tier(d);
            float life = r.Boss.LifeAt(t);
            if (InGodhome(scene))
            {
                if (r.Stage < 5) life *= GodhomeRematch.LifeAt(t) / (float)stageBoss[r.Stage].LifeAt(t);
                life *= Mathf.Clamp(godhomeTier, 1f, 2f);
            }
            return Mathf.Max(1, Mathf.RoundToInt(life));
        }

        /// <summary>The boss's Terraria defense (Master leaves it as it is).</summary>
        public static int DefenseOf(Ref r) => r.Boss.Defense;

        /// <summary>Dev: every entry, for the balance report.</summary>
        public static IEnumerable<KeyValuePair<string, Ref>> DevAll => table;
    }
}
