using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        // Research export only. No balance values or production saves are modified.
        IEnumerator BalanceReviewTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated save required");
            yield return GoTo("Town", "left1");
            int oldStage = Shop.DevStage;
            var oldDifficulty = GameDifficulty.Current;
            var oldRandom = UnityEngine.Random.state;
            bool oldInfinite = mod.Inventory.Infinite, oldFree = mod.Inventory.FreeMaking;
            var world = new List<object>(); var godhome = new List<object>();
            var crates = new List<object>(); var catches = new List<object>();
            var loot = new List<object>(); var weapons = new List<object>();
            var sequences = new List<object>();
            try
            {
                GameDifficulty.Current = DifficultyLevel.Master;
                mod.Inventory.Infinite = false;
                mod.Inventory.FreeMaking = false;
                if (Shop.Free) throw new InvalidOperationException("Normal acquisition rules required");
                UnityEngine.Random.InitState(20261021);
                foreach (var kv in BossStats.DevAll)
                {
                    var r = kv.Value;
                    int stage = ProgressionBalance.BossRewards.TryGetValue(kv.Key, out var rw) ? rw.Stage : r.Stage;
                    Shop.DevStage = stage;
                    string scene = BestiaryHabitats.WorldScene(kv.Key) ?? BossArena.SceneOf(kv.Key) ?? EnemyDrops.SceneOf(kv.Key);
                    var tune = BossBalance.Of(r.Record, EnemyDrops.VariantLevel(scene));
                    world.Add(new { Key = kv.Key, Scene = scene, Stage = stage, RefStage = r.Stage,
                        MasterLife = Mathf.RoundToInt(BossStats.LifeOf(r, scene, DifficultyLevel.Master) * tune.Life * BossPhases.WholeFactor(r.Record, scene)),
                        Defense = EnemyStats.BossDefense(r, tune, scene), BaseDefense = r.Boss.Defense,
                        TuneDefense = tune.Defense, Geo = EnemyDrops.BossGeo(kv.Key, scene) });
                    foreach (bool first in new[] { false, true })
                    {
                        var rows = EnemyDrops.At(kv.Key, stage, first, false, false, scene);
                        var summary = new EnemyDrops.ChanceSummary(rows, stage);
                        foreach (var row in rows)
                            loot.Add(new { Key = kv.Key, First = first, Item = row.Type.ToString(), row.Group,
                                row.Weight, RawChance = row.Chance, EffectiveChance = EnemyDrops.EffectiveChance(row), MarginalChance = summary.Marginal(row),
                                Min = EnemyDrops.EffectiveMin(row), Max = EnemyDrops.EffectiveMax(row), row.MeanCount });
                    }
                }
                for (int stage = 0; stage <= 5; stage++)
                {
                    Shop.DevStage = stage;
                    foreach (var d in ItemDb.All.Where(d => ProgressionBalance.BalancedWeapon(d) && d.Stage == stage))
                        weapons.Add(new { Stage = stage, Item = d.Type.ToString(), Class = d.Class.ToString(), d.Damage, d.UseTime,
                            ArmorPen = d.Bonus.ArmorPen, BossOnly = ProgressionBalance.ExclusiveBossItems.Contains(d.Type) });
                    foreach (var kv in BossStats.DevAll)
                    {
                        var r = kv.Value;
                        string scene = kv.Key == "Hornet@4" ? "GG_Hornet_2" : BestiaryHabitats.Of(kv.Key).FirstOrDefault(h => Areas.GodhomeRoom(h.Scene)).Scene ?? "GG_review";
                        var tune = BossBalance.Of(r.Record, EnemyDrops.VariantLevel(scene));
                        foreach (var p in GodhomeBalance.Hall.Concat(GodhomeBalance.Pantheons))
                        {
                            int unprofiled = EnemyStats.BossDefense(r, tune, "GG_Workshop");
                            godhome.Add(new { Stage = stage, Key = kv.Key, Scene = scene, Profile = p.Key,
                                Life = Mathf.RoundToInt(BossStats.GodhomeLifeOf(r, scene, DifficultyLevel.Master, p) * tune.Life * BossPhases.WholeFactor(r.Record, scene)),
                                Defense = GodhomeBalance.Defense(unprofiled, p),
                                Hit = Mathf.RoundToInt(Areas.MasterDamagePerMask(stage) * Areas.BossDamage * tune.Damage * p.Damage) });
                        }
                    }
                    if (suite == "balancereviewdetail") continue;
                    foreach (var c in Fishing.DevCrateTypes)
                    {
                        if (ItemDb.Get(c).Stage > stage) continue;
                        double geo = 0, gear = 0, crystals = 0; int over = 0;
                        var counts = new Dictionary<string, int>();
                        const int samples = 1000;
                        for (int i = 0; i < samples; i++) foreach (var s in Fishing.DevContents(c))
                        {
                            var d = ItemDb.Get(s.Type);
                            if (s.Type == ItemType.Geo) geo += s.Count;
                            if (s.Type == ItemType.LifeCrystal) crystals += s.Count;
                            if (d.IsWeapon || d.IsArmor || d.IsAccessory) gear += s.Count;
                            if (d.Stage > stage) over++;
                            string name = s.Type.ToString();
                            counts[name] = (counts.TryGetValue(name, out int n) ? n : 0) + s.Count;
                        }
                        crates.Add(new { Stage = stage, Crate = c.ToString(), Samples = samples,
                            Geo = geo / samples, Gear = gear / samples, Crystals = crystals / samples, OverStage = over, Counts = counts });
                        yield return null;
                    }
                    foreach (string area in new[] { "遗忘十字路", "苍绿之径", "深巢", "泪水之城" })
                    foreach (int power in new[] { 30, 60, 100 }) foreach (bool buff in new[] { false, true })
                    {
                        var counts = Fishing.DevSimulate(area, false, power, 300, 20000, buff);
                        catches.Add(new { Stage = stage, Area = area, Power = power, Buff = buff, Checks = 20000,
                            Crates = counts.Where(k => Fishing.IsCrate(k.Key)).Sum(k => k.Value),
                            Counts = counts.ToDictionary(k => k.Key.ToString(), k => k.Value) });
                        yield return null;
                    }
                }
                foreach (var seq in Resources.LoadAll<BossSequence>("GG"))
                {
                    var fields = seq.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    var strings = new Dictionary<string, object>();
                    foreach (var f in fields) if (f.FieldType == typeof(string[]) || f.FieldType == typeof(string)) strings[f.Name] = f.GetValue(seq);
                    var scenes = new List<object>();
                    var native = fields.FirstOrDefault(f => f.FieldType == typeof(BossScene[]))?.GetValue(seq) as BossScene[];
                    if (native != null) foreach (var b in native) scenes.Add(new { b.name, b.sceneName, b.isHidden });
                    sequences.Add(new { Name = seq.name, seq.Count, seq.achievementKey, Fields = strings, Scenes = scenes });
                }
                var items = ItemDb.All.Select(d => new { Item = d.Type.ToString(), d.Name, d.Stage, d.IsWeapon, d.IsArmor, d.IsAccessory, d.MaxStack, d.Value }).ToArray();
                File.WriteAllText(Path.Combine(DevCopy.SaveDir, "balance-review.json"), JsonConvert.SerializeObject(new
                    { FreeMaking = Shop.Free, World = world, Godhome = godhome, Crates = crates, Catches = catches, Loot = loot, Weapons = weapons, Sequences = sequences, Items = items }, Formatting.Indented));
                Plugin.Log.LogInfo($"[AutoTest] BALANCE_REVIEW 导出 通过 world={world.Count} godhome={godhome.Count} crates={crates.Count} catches={catches.Count}");
            }
            finally { Shop.DevStage = oldStage; GameDifficulty.Current = oldDifficulty; UnityEngine.Random.state = oldRandom; mod.Inventory.Infinite = oldInfinite; mod.Inventory.FreeMaking = oldFree; }
        }
    }
}
