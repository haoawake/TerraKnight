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
        IEnumerator BossRecipesTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            yield return GoTo("Town", "left1");
            TerraMod.DevAllInfinite = false;
            mod.ApplyCreative(false);
            PlayerData.instance.isInvincible = true;
            var hero = HeroController.instance;
            var region = TerrainSystem.Current.Region;
            Stations.PlaceFixed(StationKind.IronAnvil, region.SceneName, (Vector2)hero.transform.position - (Vector2)region.Root.position + Vector2.right * 2f);
            yield return null;
            int previousStage = Shop.DevStage, checks = 0, failures = 0;
            var previousDifficulty = GameDifficulty.Current;
            var previousSink = EnemyDrops.DevSink;
            var previousRandom = UnityEngine.Random.state;
            var report = new List<object>();
            void Check(string label, bool pass, string detail = "")
            {
                checks++;
                if (!pass) failures++;
                Plugin.Log.LogInfo($"[AutoTest] 首领当地材料 {label}：{detail} {(pass ? "通过" : "不通过")}");
            }
            try
            {
                GameDifficulty.Current = DifficultyLevel.Normal;
                UnityEngine.Random.InitState(10162026);
                InventoryGui.Open = true;
                var inv = mod.Inventory;
                inv.Override = -1;
                Check("40张配方覆盖", BossSummons.All.Length == 40 && BossSummons.All.Select(s => s.Type).Distinct().Count() == 40);
                Check("实际砧在制作范围内", Stations.Near("砧", hero));
                foreach (var seal in BossSummons.All)
                {
                    var materials = new[] { (Type: seal.Material, Count: seal.Amount, Source: seal.MaterialSource),
                        (Type: seal.SupportMaterial, Count: seal.SupportAmount, Source: seal.SupportSource) };
                    bool local = !string.IsNullOrEmpty(seal.Locality);
                    var proofs = new List<object>();
                    // The sources must stay available at all later stages, including before entry/final clears.
                    for (int stage = seal.Stage; stage <= 5; stage++)
                        foreach (var material in materials)
                        {
                            var entry = EnemyDrops.EntryFor(material.Source);
                            var rows = EnemyDrops.At(material.Source, stage, false, false, false);
                            var row = rows.FirstOrDefault(d => d.Type == material.Type && d.Chance > 0);
                            double chance = row.Type == material.Type ? EnemyDrops.MarginalChance(rows, row) : 0;
                            bool available = row.Type == material.Type && chance > 0 && row.Min > 0
                                && ProgressionBalance.Unlocked(material.Type, stage, false, false);
                            bool repeatable = !EnemyDrops.IsBoss(material.Source) && entry.Hp > 0
                                && ProgressionLoot.Eligible(entry) && !ProgressionLoot.Spawned(material.Source)
                                && entry.Kind != "quest" && entry.Kind != "unique" && entry.Kind != "miniboss"
                                && !ProgressionLoot.NamedElites.Contains(material.Source)
                                && EnemyDrops.StageOf(material.Source) <= seal.Stage;
                            local &= available && repeatable;
                            proofs.Add(new { Stage = stage, Material = material.Type.ToString(), material.Count,
                                Source = material.Source, SourceName = EnemyDrops.ZhName(material.Source),
                                Scene = EnemyDrops.SceneOf(material.Source), Chance = chance, row.Min, row.Max, row.MeanCount,
                                Available = available, Repeatable = repeatable });
                        }
                    Check(seal.Key + "所有后续阶段均有普通怪来源", local, seal.Locality);
                    Shop.DevStage = seal.Stage;
                    var recipe = Recipes.For(seal.Type);
                    bool matches = recipe != null && recipe.Count == 1 && recipe.Station == "砧" && recipe.StationStage == seal.Stage
                        && recipe.Ingredients.Length == 2 && recipe.Ingredients.All(i => materials.Any(m => m.Type == i.Type && m.Count == i.Count));
                    Array.Clear(inv.Slots, 0, inv.Slots.Length);
                    inv.Mouse = default;
                    foreach (var m in materials) inv.Add(m.Type, m.Count);
                    if (seal.Stage > 0)
                    {
                        Shop.DevStage = seal.Stage - 1;
                        Check(seal.Key + "不越阶段制作", !Recipes.HasIngredients(recipe, inv) && !InventoryGui.DevCraft(seal.Type));
                        Shop.DevStage = seal.Stage;
                    }
                    bool crafted = InventoryGui.DevCraft(seal.Type);
                    Check(seal.Key + "界面实际制作及精确扣料", matches && crafted && inv.Mouse.Type == seal.Type && inv.Mouse.Count == 1
                        && materials.All(m => Recipes.Count(inv, m.Type) == 0));
                    inv.Mouse = default;

                    // Actual normal-difficulty repeat-kill rolls, selecting only the authored nearby enemies.
                    const int trials = 200, killLimit = 240;
                    var kills = new int[trials];
                    int timeouts = 0;
                    var collected = new Dictionary<ItemType, int>();
                    EnemyDrops.DevSink = (t, n) =>
                    {
                        if (t == seal.Material || t == seal.SupportMaterial)
                            collected[t] = (collected.TryGetValue(t, out int have) ? have : 0) + n;
                    };
                    int Have(ItemType t) => collected.TryGetValue(t, out int value) ? value : 0;
                    for (int trial = 0; trial < trials; trial++)
                    {
                        collected.Clear();
                        int count = 0;
                        while ((Have(seal.Material) < seal.Amount || Have(seal.SupportMaterial) < seal.SupportAmount) && count < killLimit)
                        {
                            bool needFirst = Have(seal.Material) < seal.Amount, needSecond = Have(seal.SupportMaterial) < seal.SupportAmount;
                            string source = needFirst && (!needSecond || count % 2 == 0) ? seal.MaterialSource : seal.SupportSource;
                            EnemyDrops.Roll(source, Vector2.zero, true, false);
                            count++;
                        }
                        kills[trial] = count;
                        if (Have(seal.Material) < seal.Amount || Have(seal.SupportMaterial) < seal.SupportAmount) timeouts++;
                    }
                    Array.Sort(kills);
                    double mean = kills.Average();
                    int p95 = kills[(int)(trials * .95) - 1];
                    Check(seal.Key + "普通难度收集成本", timeouts == 0 && mean <= 35 && p95 <= 100,
                        $"200次收集：均值 {mean:F1} 次击杀，P95 {p95}，超限 {timeouts}");
                    report.Add(new { seal.Key, Item = seal.Type.ToString(), seal.Name, seal.Stage, seal.Locality, Proofs = proofs,
                        Collection = new { Trials = trials, AverageKills = mean, P95Kills = p95, Timeouts = timeouts } });
                    EnemyDrops.DevSink = previousSink;
                    yield return null;
                }
                File.WriteAllText(Path.Combine(DevCopy.SaveDir, "boss-recipe-audit.json"), JsonConvert.SerializeObject(new { Checks = checks, Failures = failures, Seals = report }, Formatting.Indented));
                Plugin.Log.LogInfo($"[AutoTest] 首领当地材料 合计 {checks - failures}/{checks} {(failures == 0 ? "通过" : "不通过")}");
                // Custom boss recipes must not change vanilla crafting and its environment restrictions.
                yield return VanillaRecipesTest();
            }
            finally
            {
                Shop.DevStage = previousStage;
                GameDifficulty.Current = previousDifficulty;
                EnemyDrops.DevSink = previousSink;
                UnityEngine.Random.state = previousRandom;
                InventoryGui.Open = false;
            }
        }
    }
}
