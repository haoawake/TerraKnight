using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// What Terraria's world gives that a recipe, or a station every recipe of a kind is made at, needs and no drop table
    /// here gave: the Sky Mill (its sky islands' houses) from the vengeflies, the Ice Machine (its snow biome's chests)
    /// from Crystal Peak, the Ancient Manipulator (the Lunatic Cultist's, after Golem) from the Hollow Knight (Golem's
    /// stand-in), the bait critters and the gold ones the food recipes ask for from small enemies. Each is a roll of its
    /// own, added after the progression's drops (EnemyDrops.Of, At and Roll), from its stage on.
    /// </summary>
    internal static class EssentialDrops
    {
        static readonly Dictionary<string, (ItemType Type, float Chance, int Stage)[]> extra = new Dictionary<string, (ItemType, float, int)[]>
        {
            // The Sky Mill: the sky's own fliers.
            ["Buzzer"] = new[] { (ItemType.SkyMill, .02f, 1) },
            ["AngryBuzzer"] = new[] { (ItemType.SkyMill, .04f, 1) },
            ["BigBuzzer"] = new[] { (ItemType.SkyMill, .25f, 1) },
            // The Ice Machine: Crystal Peak's cold crystal.
            ["CrystalFlyer"] = new[] { (ItemType.IceMachine, .04f, 1) },
            ["CrystalCrawler"] = new[] { (ItemType.IceMachine, .04f, 1) },
            ["MegaBeamMiner"] = new[] { (ItemType.IceMachine, .25f, 1) },
            // The Ancient Manipulator: every time, as the Lunatic Cultist's.
            ["HollowKnight"] = new[] { (ItemType.LunarCraftingStation, 1f, 5) },
            // Bait critters (the Grub Soup's, the Escargot's) and the gold ones (the Golden Delight's).
            ["Crawler"] = new[] { (ItemType.Grubby, .005f, 0) },
            ["Climber"] = new[] { (ItemType.Snail, .005f, 0) },
            ["MossWalker"] = new[] { (ItemType.Sluggy, .005f, 1), (ItemType.GoldWorm, .001f, 1) },
            ["MiniSpider"] = new[] { (ItemType.Buggy, .005f, 3) },
            ["GrassHopper"] = new[] { (ItemType.GoldGrasshopper, .002f, 1) },
            ["MossFlyer"] = new[] { (ItemType.GoldButterfly, .002f, 1) },
            ["Mosquito"] = new[] { (ItemType.GoldDragonfly, .002f, 1) },
            ["MossmanRunner"] = new[] { (ItemType.GoldLadyBug, .002f, 1) },
            ["AcidWalker"] = new[] { (ItemType.GoldWaterStrider, .002f, 1) },
            // Buff furniture Terraria's world puts about: the Bast Statue (its desert) in Kingdom's Edge's dunes, the
            // Sunflower and the Garden Gnome (its surface) in Greenpath and the Queen's Gardens.
            ["Hopper"] = new[] { (ItemType.CatBast, .04f, 1) },
            ["GiantHopper"] = new[] { (ItemType.CatBast, .15f, 1) },
            ["SnapperTrap"] = new[] { (ItemType.Sunflower, .05f, 0) },
            ["MossKnight"] = new[] { (ItemType.GardenGnome, .06f, 1) },
        };

        static readonly Dictionary<(IReadOnlyList<EnemyDrops.Drop>, string, int), IReadOnlyList<EnemyDrops.Drop>> merged =
            new Dictionary<(IReadOnlyList<EnemyDrops.Drop>, string, int), IReadOnlyList<EnemyDrops.Drop>>();

        /// <summary>A record's drops at a stage with its essentials after them (the same list when it has none then).</summary>
        public static IReadOnlyList<EnemyDrops.Drop> With(string record, int stage, IReadOnlyList<EnemyDrops.Drop> drops)
        {
            var result = WithEssentials(record, stage, drops);
            if (!EnemyDrops.IsBoss(record)) return result;
            var list = new List<EnemyDrops.Drop>(result);
            list.RemoveAll(d => d.Type == ItemType.LifeCrystal);
            if (Net.BossLifeRewards.Eligible(record))
                list.Add(new EnemyDrops.Drop { Type = ItemType.LifeCrystal, Num = 1, Den = 1, Min = 1, Max = 1, LockStage = 0 });
            return list;
        }

        static IReadOnlyList<EnemyDrops.Drop> WithEssentials(string record, int stage, IReadOnlyList<EnemyDrops.Drop> drops)
        {
            if (record == null || !extra.TryGetValue(record, out var rows)) return drops;
            // A boss pays at its own stage, whenever it is fought (as ProgressionLoot has it).
            if (ProgressionBalance.FormalBoss(record) && ProgressionBalance.BossRewards.TryGetValue(record, out var reward))
                stage = Mathf.Max(stage, reward.Stage);
            var key = (drops, record, stage);
            if (merged.TryGetValue(key, out var done)) return done;
            var list = new List<EnemyDrops.Drop>(drops);
            foreach (var (type, chance, from) in rows)
                if (stage >= from && !list.Exists(d => d.Type == type))
                    list.Add(new EnemyDrops.Drop { Type = type, Num = Mathf.RoundToInt(chance * 100000), Den = 100000, Min = 1, Max = 1, Group = 0, LockStage = 0 });
            if (merged.Count > 4000) merged.Clear();
            IReadOnlyList<EnemyDrops.Drop> result = list.Count == drops.Count ? drops : list.ToArray();
            merged[key] = result;
            return result;
        }
    }
}
