using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json;
using TerraKnight.Items;
using TerraKnight.Town;
using TerraKnight.Terraria;
using TerraKnight.World;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator WikiDetailsExport()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Private export required");
            var roster = (NpcDef[])AccessTools.Property(typeof(TownNpcs), "All").GetValue(null);
            var npcs = roster.Select(n => new {
                n.Id, n.Internal, n.Zh, n.Stage, Kind = n.Kind.ToString(),
                Stocks = ProgressionBalance.ShopStock(n.Internal, n.Items).Select(s => new {
                    Type = s.Type.ToString(), s.Stage, s.Phases, Spell = SpellRewards.SpellOf(s.Type),
                    PriceCopper = Shop.PriceOf(ItemDb.Get(s.Type))
                }).ToArray(),
                Chat = (n.Chat ?? Array.Empty<string>()).Select(k => new { Key = k, Text = TerrariaText.Get("zh-Hans", k) ?? TerrariaText.Get("en-US", k) }).ToArray()
            }).ToArray();
            var godhome = new Dictionary<string, object>();
            int stageWas = Shop.DevStage;
            var clearsWas = VanillaBossProgression.DevClears;
            try
            {
                foreach (string key in EnemyDrops.DevRecords)
                {
                    int at = key.IndexOf('@');
                    string record = at < 0 ? key : key.Substring(0, at);
                    string scene = EnemyDrops.SceneOf(key);
                    int variant = at < 0 ? EnemyDrops.VariantLevel(scene) : int.Parse(key.Substring(at + 1));
                    var reference = BossStats.Of(record, variant);
                    if (reference == null) continue;
                    var rows = new List<object>();
                    foreach (int stage in Enumerable.Range(0, 6))
                    foreach (string progress in stage == 5 ? new[] { "PreCultist", "Cultist", "Pillars", "MoonLord" } : new[] { "PreCultist" })
                    {
                        Shop.DevStage = stage;
                        VanillaBossProgression.DevClears = new HashSet<string>();
                        if (progress != "PreCultist") VanillaBossProgression.DevClears.Add("HollowKnight");
                        if (progress == "Pillars" || progress == "MoonLord") VanillaBossProgression.DevClears.Add("NailBros");
                        if (progress == "MoonLord") VanillaBossProgression.DevClears.Add("FinalBoss");
                        foreach (DifficultyLevel difficulty in Enum.GetValues(typeof(DifficultyLevel)))
                        foreach (var profile in GodhomeBalance.Hall.Concat(GodhomeBalance.Pantheons))
                        {
                            int life = BossStats.GodhomeLifeOf(reference, "GG_", difficulty, profile);
                            rows.Add(new { Stage = stage, Progress = progress, Difficulty = difficulty.ToString(),
                                Challenge = profile.Name, UnitLife = life, PoolFactor = BossPhases.BudgetFactor(record,"GG_"),
                                LifeTuning = BossBalance.Of(record, variant).Life,
                                profile.Life, profile.Defense, profile.FlatDefense, profile.Damage });
                        }
                    }
                    godhome[key] = rows;
                    yield return null;
                }
            }
            finally { Shop.DevStage = stageWas; VanillaBossProgression.DevClears = clearsWas; }
            var prices = ItemDb.All.Where(d => d.Type != ItemType.None).ToDictionary(d => d.Type.ToString(), d => Shop.PriceOf(d));
            var travel = ((ValueTuple<ItemType, int>[])AccessTools.Field(typeof(TownNpcs), "travelPool").GetValue(null))
                .Select(s => new { Type = s.Item1.ToString(), Stage = s.Item2 }).ToArray();
            File.WriteAllText(Path.Combine(DevCopy.SaveDir,"wiki-details.json"),JsonConvert.SerializeObject(new { npcs, godhome, prices, travel },Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] Wiki 细节导出 {npcs.Length} NPC，{godhome.Count} Boss 进度/挑战组合");
        }
    }
}
