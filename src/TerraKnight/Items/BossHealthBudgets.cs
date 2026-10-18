using System.Collections.Generic;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>Explicit single-player Classic whole-fight budgets, before difficulty/MP scaling.</summary>
    internal static class BossHealthBudgets
    {
        internal static readonly Dictionary<string, int> World = new Dictionary<string, int>
        {
            ["FalseKnight@5"] = 100000, ["MageLord@5"] = 120000, ["InfectedKnight@5"] = 110000,
            ["HiveKnight"] = 50000, ["Hornet@4"] = 80000, ["GhostMarkoth"] = 75000,
            ["InfectedKnight"] = 65000, ["Oblobble"] = 90000, ["LobsterLancer"] = 85000,
            ["Zote"] = 12000,
            ["Grimm"] = 100000, ["WhiteDefender"] = 120000, ["GreyPrince"] = 100000,
            ["NightmareGrimm"] = 160000, ["HollowKnight"] = 140000,
            ["NailBros"] = 140000, ["Paintmaster"] = 150000, ["Nailsage"] = 160000,
            ["HollowKnightPrime"] = 200000, ["FinalBoss"] = 220000,
        };

        // Earlier bosses' endgame rematches get their own budgets too. Their world encounters stay unchanged.
        internal static readonly Dictionary<string, int> Rematches = new Dictionary<string, int>
        {
            ["FalseKnight"] = 100000, ["BigFly"] = 70000, ["Mawlek"] = 85000,
            ["Hornet"] = 95000, ["MegaMossCharger"] = 80000,
            ["GhostAladar"] = 75000, ["GhostNoEyes"] = 85000,
            ["DungDefender"] = 105000, ["TraitorLord"] = 115000,
            ["MantisLord"] = 130000, ["MageLord"] = 115000,
            ["MegaJellyfish"] = 95000, ["FlukeMother"] = 85000, ["MegaBeamMiner"] = 95000,
            ["GhostHu"] = 80000, ["GhostMarmu"] = 85000,
            ["BlackKnight"] = 145000, ["JarCollector"] = 110000, ["MimicSpider"] = 115000,
            ["GhostGalien"] = 90000, ["GhostXero"] = 85000,
        };

        internal static readonly Dictionary<string, int> Rooms = new Dictionary<string, int>
        {
            ["GG_Hornet_2"] = 125000, ["GG_Crystal_Guardian_2"] = 120000,
            ["GG_Mantis_Lords_V"] = 170000, ["GG_Nosk_Hornet"] = 140000,
            ["GG_Radiance"] = 300000,
        };

        internal static bool TryTotal(BossStats.Ref boss, string scene, DifficultyLevel difficulty, out float total)
        {
            total = 0f;
            string key = boss.Key ?? boss.Record;
            bool godhome = BossStats.InGodhome(scene);
            int normal;
            if (!(godhome && Rooms.TryGetValue(scene, out normal))
                && !(godhome && Rematches.TryGetValue(key, out normal)) && !World.TryGetValue(key, out normal)) return false;
            // Retain each stand-in's own Expert/Master curve; a budget replaces health alone.
            var curve = scene == "GG_Hornet_2" ? BossStats.Of("Hornet", 4).Boss : boss.Boss;
            total = normal * (curve.LifeAt(BossStats.Tier(difficulty)) / (float)curve.LifeAt(0f));
            return true;
        }

        internal static int UnitLife(BossStats.Ref boss, string scene, float total)
        {
            // EnemyStats applies Tune.Life and the native phase/body share later. Remove
            // them here so the sum is the requested total, not another compounded reduction.
            float tune = BossBalance.Of(boss.Record, EnemyDrops.VariantLevel(scene)).Life;
            return Mathf.Max(1, Mathf.RoundToInt(total / (tune * BossPhases.WholeFactor(boss.Record, scene))));
        }
    }
}
