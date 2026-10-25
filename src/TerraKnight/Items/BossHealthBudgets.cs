using System.Collections.Generic;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>Single-player Master whole-fight budgets following equipment/reward progression.
    /// Native phases divide this total; difficulty and MP are applied separately.</summary>
    internal static class BossHealthBudgets
    {
        // Explicit Master anchors keep the early game gentle while the late game rises faster.
        internal const int MasterRadianceTarget = 200000;
        internal const float GatekeeperMultiplier = 1.2f;
        // Key progression fights: mobility upgrades, dive and the Watcher dreamer gate.
        internal static readonly HashSet<string> Gatekeepers = new HashSet<string>
        {
            "Hornet", "MantisLord", "MageLord", "MegaBeamMiner", "BlackKnight", "InfectedKnight", "Hornet@4",
        };

        internal static float WorldCurveBudget(string key) => key == "Hornet"
            ? World[key] / GatekeeperMultiplier : World[key];

        internal static float WorldMasterBudget(string key) => VanillaBossProgression.Combat.TryGetValue(key,out var combat)
            ? combat.Life : WorldCurveBudget(key) * (Gatekeepers.Contains(key) ? GatekeeperMultiplier : 1f);

        internal static readonly Dictionary<string, int> World = new Dictionary<string, int>
        {
            // Stage 0: starting equipment. False Knight's complete armour/head rounds
            // receive a modest premium, not a fresh boss budget for every refill.
            ["FalseKnight"] = 3000, ["BigFly"] = 2200, ["Mawlek"] = 2800, ["BigBuzzer"] = 2000,
            // Stage 1: early class equipment and the first mobility upgrade.
            ["Hornet"] = 4000, ["MegaMossCharger"] = 3600,
            ["GhostAladar"] = 3200, ["GhostNoEyes"] = 3600, ["GhostHu"] = 3800,
            // Stage 2: pre-hardmode class equipment. The early dream rematch belongs
            // here too, despite its @5 journal suffix (which denotes its variant).
            ["MantisLord"] = 7000, ["MageLord"] = 6500, ["MegaBeamMiner"] = 6200,
            ["DungDefender"] = 6000, ["FlukeMother"] = 5200, ["MegaJellyfish"] = 5800,
            ["GhostXero"] = 5500, ["FalseKnight@5"] = 8500, ["MageKnight"] = 5000,
            // Stage 3: entry to stronger equipment; the Broken Vessel is an entry
            // encounter according to the reward path, not an endgame health wall.
            ["BlackKnight"] = 14000, ["JarCollector"] = 11000, ["MimicSpider"] = 12500,
            ["GhostGalien"] = 9500, ["GhostMarmu"] = 9000,
            ["MageLord@5"] = 17000, ["InfectedKnight"] = 13000,
            // Stage 4: established hardmode equipment. Lost Kin is a modest step
            // above the other fights of this stage; Zote remains deliberately weak.
            ["HiveKnight"] = 25000, ["Hornet@4"] = 35000, ["GhostMarkoth"] = 30000,
            ["InfectedKnight@5"] = 40000, ["Oblobble"] = 32000, ["Zote"] = 1000,
            // Stage 5: successive late-game challenges, rather than an immediate
            // jump from early equipment straight to a six-figure fight.
            ["TraitorLord"] = 50000, ["LobsterLancer"] = 60000,
            ["Grimm"] = 65000, ["WhiteDefender"] = 80000, ["GreyPrince"] = 75000,
            ["HollowKnight"] = 110000, ["NightmareGrimm"] = 140000,
            ["NailBros"] = 125000, ["Paintmaster"] = 135000, ["Nailsage"] = 150000,
            ["HollowKnightPrime"] = 180000, ["FinalBoss"] = MasterRadianceTarget,
        };

        // Earlier bosses' endgame rematches get their own budgets too. Their world encounters stay unchanged.
        internal static readonly Dictionary<string, int> Rematches = new Dictionary<string, int>
        {
            ["FalseKnight"] = 100000, ["BigFly"] = 85000, ["Mawlek"] = 100000, ["BigBuzzer"] = 80000, ["MageKnight"] = 95000,
            ["Hornet"] = 100000, ["MegaMossCharger"] = 85000,
            ["GhostAladar"] = 80000, ["GhostNoEyes"] = 85000,
            ["DungDefender"] = 115000, ["TraitorLord"] = 130000,
            ["MantisLord"] = 120000, ["MageLord"] = 115000,
            ["MegaJellyfish"] = 100000, ["FlukeMother"] = 95000, ["MegaBeamMiner"] = 105000,
            ["GhostHu"] = 85000, ["GhostMarmu"] = 95000,
            ["BlackKnight"] = 140000, ["JarCollector"] = 115000, ["MimicSpider"] = 125000,
            ["GhostGalien"] = 100000, ["GhostXero"] = 90000,
            ["FalseKnight@5"] = 160000, ["MageLord@5"] = 180000, ["InfectedKnight@5"] = 200000,
            ["HiveKnight"] = 120000, ["GhostMarkoth"] = 140000, ["InfectedKnight"] = 125000,
            ["Oblobble"] = 135000, ["LobsterLancer"] = 145000,
            ["Grimm"] = 140000, ["WhiteDefender"] = 150000, ["GreyPrince"] = 150000,
            ["Zote"] = 12000,
        };

        internal static readonly Dictionary<string, int> Rooms = new Dictionary<string, int>
        {
            ["GG_Hornet_2"] = 150000, ["GG_Crystal_Guardian_2"] = 140000,
            ["GG_Mantis_Lords_V"] = 180000, ["GG_Nosk_Hornet"] = 160000,
            ["GG_Radiance"] = 235000,
        };

        // Fixed stage curve, never driven by the gear currently worn. The last ability
        // raises life by 80% rather than the old 238% jump from world-boss means.
        internal static readonly float[] GodhomeStageShares = { .025f, .045f, .08f, .16f, .30f, .54f };

        /// <summary>
        /// Godhome's fixed share of each boss's own rematch budget at the acquired-ability stage.
        /// </summary>
        internal static float StageShare(int stage)
        {
            return GodhomeStageShares[Mathf.Clamp(stage, 0, 5)];
        }

        // Fixed world milestones, never the equipment worn or an observed DPS value.
        // Stage 5 spans Plantera through post-Moon Lord; one ability band cannot budget all of them.
        internal static float GodhomeProgressShare(int stage)
        {
            if(stage<5)return 1f;
            var progress=VanillaBossProgression.Current;
            return progress>=VanillaBossProgression.Step.MoonLord ? 2.25f
                : progress>=VanillaBossProgression.Step.Pillars ? 1.5f
                : progress>=VanillaBossProgression.Step.Cultist ? 1.2f : 1f;
        }

        internal static bool TryTotal(BossStats.Ref boss, string scene, DifficultyLevel difficulty, out float total)
        {
            total = 0f;
            string key = boss.Key ?? boss.Record;
            bool godhome = BossStats.InGodhome(scene);
            int normal = 0;
            bool rematch = godhome && (Rooms.TryGetValue(scene, out normal) || Rematches.TryGetValue(key, out normal));
            // A god fought only in Godhome: its fight at stage 5 over that stage's share, which GodhomeLifeOf applies again.
            if (godhome && !rematch && VanillaBossProgression.GodhomeFights.TryGetValue(key, out int fight))
            {
                normal = Mathf.RoundToInt(fight / StageShare(5));
                rematch = true;
            }
            if (!rematch && !World.TryGetValue(key, out normal)) return false;
            float master = rematch ? normal : WorldMasterBudget(key);
            // Retain each stand-in's own Expert/Master curve; a budget replaces health alone.
            var curve = scene == "GG_Hornet_2" ? BossStats.Of("Hornet", 4).Boss : boss.Boss;
            total = master * (curve.LifeAt(BossStats.Tier(difficulty)) / (float)curve.LifeAt(BossStats.Tier(DifficultyLevel.Master)));
            return true;
        }

        internal static int UnitLife(BossStats.Ref boss, string scene, float total)
        {
            // EnemyStats applies Tune.Life and the native phase/body share later. Remove
            // them here so the sum is the requested total, not another compounded reduction.
            float tune = BossBalance.Of(boss.Record, EnemyDrops.VariantLevel(scene)).Life;
            return Mathf.Max(1, Mathf.RoundToInt(total / (tune * BossPhases.BudgetFactor(boss.Record, scene))));
        }
    }
}
