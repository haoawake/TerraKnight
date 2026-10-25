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
    /// <summary>
    /// Dev (suite "rewardaudit"): what bosses, common enemies and fishing pay (the user's, 2026-10-20: "boss收益太低…刷boss还不如
    /// 刷小怪", "钓鱼的收益也太低了，箱子里面塞的东西太多了"). Normal and Master, each stage: every world boss reached by then and
    /// every other enemy in each of its areas, kills rolled into EnemyDrops.DevSink (repeat kills): the geo, the items and their
    /// sale value a kill, its life; every crate opened (DevContents): its geo, items, value and how many pieces its equipment is
    /// picked from; a thousand casts in each area (Fishing.DevSimulate): crates and value a catch. Written to rewardaudit.json.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator RewardAuditTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            int savedStage = Shop.DevStage;
            var difficulty = GameDifficulty.Current;
            var sink = EnemyDrops.DevSink;
            var random = UnityEngine.Random.state;
            var rows = new List<object>();
            var got = new List<(ItemType Type, int Count)>();
            // A take's worth: geo as it is, an item at its sale price (a fifth of its value), in geo.
            double Worth(ItemType t, int n) => t == ItemType.Geo ? n : Shop.SellValue(ItemStack.Of(t, n)) / 100.0;
            bool Gear(ItemType t) { var d = ItemDb.Get(t); return d != null && d.MaxStack == 1 && (d.IsWeapon && !d.IsAmmo || d.IsAccessory || d.IsArmor); }
            var crateWorth = new Dictionary<ItemType, double>();
            try
            {
                UnityEngine.Random.InitState(20261020);
                EnemyDrops.DevSink = (t, n) => got.Add((t, n));
                var records = EnemyDrops.DevRecords.Where(r => EnemyDrops.KindOf(r) != "non_enemy" && EnemyDrops.KindOf(r) != "player_shade").OrderBy(r => r, StringComparer.Ordinal).ToList();
                foreach (var diff in new[] { DifficultyLevel.Normal, DifficultyLevel.Master })
                {
                    GameDifficulty.Current = diff;
                    for (int stage = 0; stage <= Areas.MaxLevel; stage++)
                    {
                        Shop.DevStage = stage;
                        double bossValue = 0, bossLife = 0, bossItems = 0, bossGear = 0, bossGeo = 0; int bosses = 0;
                        double mobValue = 0, mobLife = 0, mobItems = 0, mobGear = 0, mobGeo = 0; int mobs = 0;
                        foreach (var rec in records)
                        {
                            bool boss = EnemyDrops.IsBoss(rec);
                            if (boss && (!ProgressionBalance.FormalBoss(rec) || ProgressionBalance.BossRewards[rec].Stage > stage)) continue;
                            var scenes = boss ? new List<string> { BestiaryHabitats.WorldScene(rec) } : BestiaryHabitats.Of(rec).Select(h => h.Scene).ToList();
                            foreach (var scene in scenes)
                            {
                                if (scene == null) continue;
                                int life = Bestiary.StatsOf(rec, stage).Life;
                                if (life <= 0) continue;
                                int geo = Mathf.Max(0, EnemyDrops.GeoOf(rec));
                                int kills = boss ? 300 : 120;
                                double value = 0, items = 0, gear = 0, paid = 0;
                                for (int i = 0; i < kills; i++)
                                {
                                    got.Clear();
                                    EnemyDrops.Roll(rec, Vector2.zero, true, false, scene: scene);
                                    // Expert and Master: the boss's bag, opened (TreasureBags.TryOpen's roll).
                                    int bag = got.FindIndex(x => TreasureBags.Is(x.Type));
                                    if (bag >= 0)
                                    {
                                        got.RemoveAt(bag);
                                        EnemyDrops.Roll(rec, Vector2.zero, direct: true, firstKill: false, fromBag: true, scene: scene);
                                    }
                                    // A boss's own geo (EnemyDrops.BossGeo) comes as a stack of geo: the geo's, not the items'.
                                    foreach (var (t, n) in got) { if (t == ItemType.Geo) { paid += n; continue; } value += Worth(t, n); items++; if (Gear(t)) gear++; }
                                    if (i % 100 == 99) yield return null;
                                }
                                value /= kills; items /= kills; gear /= kills;
                                geo += Mathf.RoundToInt((float)(paid / kills));
                                rows.Add(new { kind = boss ? "boss" : "mob", diff = diff.ToString(), stage, rec, name = Bestiary.NameOf(rec), area = Areas.AreaName(scene), life, geo, items, gear, value });
                                if (boss) { bosses++; bossValue += value; bossLife += life; bossItems += items; bossGear += gear; bossGeo += geo; }
                                else { mobs++; mobValue += value; mobLife += life; mobItems += items; mobGear += gear; mobGeo += geo; }
                            }
                        }
                        Plugin.Log.LogInfo($"[AutoTest] 收益 {diff} 阶段{stage} 首领{bosses}个：平均血 {bossLife / Mathf.Max(1, bosses):0} 吉欧 {bossGeo / Mathf.Max(1, bosses):0.#} 物品 {bossItems / Mathf.Max(1, bosses):0.##} 件（装备 {bossGear / Mathf.Max(1, bosses):0.##}）卖价 {bossValue / Mathf.Max(1, bosses):0.#}；"
                            + $"每千血 {(bossGeo + bossValue) * 1000 / Mathf.Max(1f, (float)bossLife):0.##} | 小怪{mobs}个：平均血 {mobLife / Mathf.Max(1, mobs):0} 吉欧 {mobGeo / Mathf.Max(1, mobs):0.#} 物品 {mobItems / Mathf.Max(1, mobs):0.##}（装备 {mobGear / Mathf.Max(1, mobs):0.###}）卖价 {mobValue / Mathf.Max(1, mobs):0.#}；每千血 {(mobGeo + mobValue) * 1000 / Mathf.Max(1f, (float)mobLife):0.##}");

                        // The crates: the tier ones and each area's (its coffer from stage 3).
                        crateWorth.Clear();
                        var crateTypes = new List<ItemType> { ItemType.WoodenCrate, ItemType.IronCrate, ItemType.GoldenCrate };
                        if (stage >= 3) crateTypes.AddRange(new[] { ItemType.WoodenCrateHard, ItemType.IronCrateHard, ItemType.GoldenCrateHard });
                        foreach (var area in Fishing.DevAreaCrateAreas()) crateTypes.Add(Fishing.AreaCrateFor(area, stage >= 3));
                        foreach (var crate in crateTypes.Distinct())
                        {
                            const int opens = 400;
                            double value = 0, items = 0, gear = 0, geo = 0;
                            var pieces = new HashSet<ItemType>();
                            for (int i = 0; i < opens; i++)
                            {
                                foreach (var s in Fishing.DevContents(crate))
                                {
                                    if (s.Type == ItemType.Geo) geo += s.Count; else { value += Worth(s.Type, s.Count); items++; }
                                    if (Gear(s.Type)) { gear++; pieces.Add(s.Type); }
                                }
                                if (i % 100 == 99) yield return null;
                            }
                            string area = Fishing.AreaOfCrate(crate);
                            var info = area != null ? Fishing.DevAreaCrate(area) : default;
                            crateWorth[crate] = (value + geo) / opens;
                            int pool = area != null ? Fishing.DevAreaPool(area, stage >= 3) : 0;
                            rows.Add(new { kind = "crate", diff = diff.ToString(), stage, crate = ItemDb.Get(crate).Name, area, geo = geo / opens, items = items / opens, gear = gear / opens, value = value / opens, seenPieces = pieces.Count, areaGear = info.Gear, theme = info.Theme, pool });
                        }
                        // A thousand casts in each fished area at fishing powers 30, 50 and 80.
                        foreach (int fl in new[] { 30, 50, 80 })
                        {
                            double value = 0, crates = 0, areaCrates = 0; int casts = 0;
                            foreach (var area in Fishing.DevFishingAreas)
                            {
                                var counts = Fishing.DevSimulate(area, false, fl, 1000, 1000);
                                foreach (var kv in counts)
                                {
                                    if (kv.Key == ItemType.None) continue;
                                    if (Fishing.IsCrate(kv.Key))
                                    {
                                        crates += kv.Value;
                                        if (Fishing.AreaOfCrate(kv.Key) != null) areaCrates += kv.Value;
                                        if (!crateWorth.TryGetValue(kv.Key, out var w))
                                        {
                                            double sum = 0;
                                            for (int i = 0; i < 100; i++) foreach (var s in Fishing.DevContents(kv.Key)) sum += Worth(s.Type, s.Count);
                                            crateWorth[kv.Key] = w = sum / 100;
                                        }
                                        value += w * kv.Value;
                                    }
                                    else value += Worth(kv.Key, 1) * kv.Value;
                                }
                                casts += 1000;
                                yield return null;
                            }
                            rows.Add(new { kind = "fishing", diff = diff.ToString(), stage, fl, casts, valuePerCast = value / casts, cratesPerCast = crates / casts, areaCratesPerCast = areaCrates / casts });
                            Plugin.Log.LogInfo($"[AutoTest] 收益 {diff} 阶段{stage} 钓鱼 渔力{fl}：每次抛竿 {value / casts:0.##} 吉欧，箱子 {crates / casts * 100:0.##}%（区域匣 {areaCrates / casts * 100:0.###}%）");
                        }
                    }
                }
            }
            finally
            {
                Shop.DevStage = savedStage;
                GameDifficulty.Current = difficulty;
                EnemyDrops.DevSink = sink;
                UnityEngine.Random.state = random;
            }
            File.WriteAllText(Path.Combine(DevCopy.SaveDir, "rewardaudit.json"), JsonConvert.SerializeObject(rows, Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] 收益 完成，{rows.Count} 行");
        }
    }
}
