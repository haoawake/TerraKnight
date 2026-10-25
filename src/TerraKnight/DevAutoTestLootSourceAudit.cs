using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        // Exhaustive table export plus sampled secondary producers. Full clears mean
        // union of legal milestones within a band, not pre-boss availability proof.
        IEnumerator LootSourceAuditTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated save required");
            var oldClears = VanillaBossProgression.DevClears;
            int oldStage = Shop.DevStage;
            var oldDifficulty = GameDifficulty.Current;
            var oldRandom = UnityEngine.Random.state;
            bool oldFree = mod.Inventory.FreeMaking, oldInfinite = mod.Inventory.Infinite;
            bool oldManipulator = EssentialDrops.ManipulatorGiven;
            var drops = new List<object>();
            var secondary = new List<object>();
            try
            {
                mod.Inventory.FreeMaking = false;
                mod.Inventory.Infinite = false;
                if (Shop.Free) throw new InvalidOperationException("Normal acquisition required");
                VanillaBossProgression.DevClears = new HashSet<string>(VanillaBossProgression.All.Keys);
                yield return ProgressionAuditTest(false);
                var habitats = EnemyDrops.DevRecords.ToDictionary(r => r,
                    r => BestiaryHabitats.Of(r).Select(h => h.Scene).Concat(new[] { EnemyDrops.SceneOf(r) })
                        .Where(s => !string.IsNullOrEmpty(s)).Distinct().ToArray());
                foreach (var difficulty in new[] { DifficultyLevel.Normal, DifficultyLevel.Master })
                {
                    GameDifficulty.Current = difficulty;
                    for (int stage = 0; stage < 6; stage++)
                    {
                        Shop.DevStage = stage;
                        foreach (var pair in habitats)
                        {
                            string record = pair.Key;
                            foreach (string scene in pair.Value)
                            {
                                bool gg = BossStats.InGodhome(scene);
                                int native = ProgressionBalance.BossRewards.TryGetValue(record, out var reward)
                                    && ProgressionBalance.FormalBoss(record) ? reward.Stage : Areas.LevelOf(scene);
                                if (!gg && native > stage) continue;
                                foreach (bool first in new[] { false, true })
                                {
                                    var rows = EnemyDrops.At(record, stage, first, stage >= 4, stage >= 5, scene);
                                    var chances = new EnemyDrops.ChanceSummary(rows, stage);
                                    foreach (var row in rows)
                                        drops.Add(new { Record = record, Scene = scene, Stage = stage,
                                            Difficulty = difficulty.ToString(), First = first,
                                            Boss = ProgressionBalance.FormalBoss(record), Godhome = gg,
                                            Item = row.Type.ToString(), row.Group, row.Weight,
                                            Chance = chances.Marginal(row), ItemChance = chances.Items.TryGetValue(row.Type, out float chance) ? chance : 0,
                                            Min = EnemyDrops.EffectiveMin(row), Max = EnemyDrops.EffectiveMax(row),
                                            row.Master, Blocked = CraftOnlyLoot.Blocked(row.Type) });
                                }
                            }
                        }
                        yield return null;
                    }
                }
                GameDifficulty.Current = DifficultyLevel.Normal;
                UnityEngine.Random.InitState(20261023);
                for (int stage = 0; stage < 6; stage++)
                {
                    Shop.DevStage = stage;
                    foreach (var crate in Fishing.DevCrateTypes.Where(t => ItemDb.Get(t).Stage <= stage))
                    {
                        const int trials = 600;
                        var counts = new Dictionary<ItemType, int>();
                        for (int i = 0; i < trials; i++)
                            foreach (var type in Fishing.DevContents(crate).Select(s => s.Type).Distinct())
                                counts[type] = counts.TryGetValue(type, out int n) ? n + 1 : 1;
                        secondary.Add(new { Kind = "crate", Source = crate.ToString(), Stage = stage, Trials = trials,
                            Counts = counts.ToDictionary(p => p.Key.ToString(), p => p.Value) });
                        yield return null;
                    }
                    foreach (string area in Fishing.DevAreaCrateAreas())
                    {
                        const int trials = 120;
                        var counts = new Dictionary<ItemType, int>();
                        for (int i = 0; i < trials; i++)
                            foreach (var type in RegionalChests.Contents(area, i % 6, stage).Where(s => !s.IsEmpty).Select(s => s.Type).Distinct())
                                counts[type] = counts.TryGetValue(type, out int n) ? n + 1 : 1;
                        secondary.Add(new { Kind = "regionalChest", Source = area, Stage = stage, Trials = trials,
                            Counts = counts.ToDictionary(p => p.Key.ToString(), p => p.Value) });
                    }
                    var fishingChest = new Dictionary<ItemType, int>();
                    for (int i = 0; i < 600; i++)
                    {
                        var contents = new List<ItemStack>();
                        FishingChest.Contents(contents);
                        foreach (var type in contents.Select(s => s.Type).Distinct())
                            fishingChest[type] = fishingChest.TryGetValue(type, out int n) ? n + 1 : 1;
                    }
                    secondary.Add(new { Kind = "fishingChest", Source = "FishingChest", Stage = stage, Trials = 600,
                        Counts = fishingChest.ToDictionary(p => p.Key.ToString(), p => p.Value) });
                    yield return null;
                }
                var transforms = ItemDb.All.Select(d =>
                {
                    var to = Shimmer.TransformOf(d.Type, out bool late);
                    return new { From = d.Type.ToString(), To = to.ToString(), Late = late };
                }).Where(p => p.To != "None").ToArray();
                var petEggs = Pets.Eggs.Where(t => !CraftOnlyLoot.Blocked(t)).Select(t => new
                    { Item = t.ToString(), Chance = Pets.KillChance, Stage = ItemDb.Get(t).Stage }).ToArray();
                var banners = Banners.Types().Select(t => new
                    { Item = t.ToString(), Record = Banners.RecordOf(t), Kills = Banners.KillsPerBanner }).ToArray();
                var kinds = ItemDb.All.ToDictionary(d => d.Type.ToString(), d => new
                    { d.HkItem, d.PlacesWall, d.GameTool, d.IsAmmo, d.IsMount, Pet = Pets.Is(d.Type), d.Opens });
                File.WriteAllText(Path.Combine(DevCopy.SaveDir, "loot-source-audit.json"),
                    JsonConvert.SerializeObject(new { FreeMaking = Shop.Free, MilestoneMode = "all-clears-band-union", Drops = drops, Secondary = secondary,
                        Transforms = transforms, PetEggs = petEggs, Banners = banners, Kinds = kinds }, Formatting.Indented));
                Plugin.Log.LogInfo($"[AutoTest] LootSourceAudit 导出 {drops.Count} 条掉落、{secondary.Count} 组次级来源 通过");
                // Earlier source sampling intentionally exercised real payouts.
                // Reset this isolated save's one-time flag before testing first payout.
                mod.GrantedAbilities.Remove(EssentialDrops.ManipulatorKey);
                yield return VanillaLootTest();
            }
            finally
            {
                VanillaBossProgression.DevClears = oldClears; Shop.DevStage = oldStage;
                GameDifficulty.Current = oldDifficulty; UnityEngine.Random.state = oldRandom;
                mod.Inventory.FreeMaking = oldFree; mod.Inventory.Infinite = oldInfinite;
                if (oldManipulator) mod.GrantedAbilities.Add(EssentialDrops.ManipulatorKey);
                else mod.GrantedAbilities.Remove(EssentialDrops.ManipulatorKey);
            }
        }
    }
}
