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
        IEnumerator BestiaryPillarsTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            int savedStage = Shop.DevStage, savedSelection = LootStage.Selection;
            int savedPreview = InventoryGui.DevBestiaryDropStage;
            var savedClears = VanillaBossProgression.DevClears;
            var savedDifficulty = GameDifficulty.Current;
            var savedSink = EnemyDrops.DevSink;
            var savedRandom = UnityEngine.Random.state;
            bool savedCreative = mod.Creative;
            bool savedCatalogue = InventoryGui.DevBookCatalogue;
            int checks = 0, failures = 0;
            var observations = new List<object>();
            var pillars = new[] {
                (Key: "NailBros", Scene: "GG_Nailmasters", Fragment: ItemType.FragmentSolar),
                (Key: "Nailsage", Scene: "GG_Sly", Fragment: ItemType.FragmentVortex),
                (Key: "Paintmaster", Scene: "GG_Painter", Fragment: ItemType.FragmentNebula),
                (Key: "HollowKnightPrime", Scene: "GG_Hollow_Knight", Fragment: ItemType.FragmentStardust)
            };
            var fragments = new HashSet<ItemType>(pillars.Select(p => p.Fragment));
            void Check(bool ok, string label)
            {
                checks++;
                if (!ok) { failures++; Plugin.Log.LogError("[AutoTest] 四柱图鉴 " + label + " 不通过"); }
            }
            try
            {
                GameDifficulty.Current = DifficultyLevel.Normal;
                mod.ApplyCreative(false);
                LootStage.Load(-1);
                InventoryGui.DevBookSearch = "";
                InventoryGui.DevBestiarySelect(-10);
                foreach (bool progressed in new[] { false, true })
                {
                    VanillaBossProgression.DevClears = progressed
                        ? new HashSet<string>(VanillaBossProgression.All.Keys) : new HashSet<string>();
                    for (int stage = 0; stage <= 5; stage++)
                    {
                        Shop.DevStage = stage;
                        foreach (var p in pillars)
                        {
                            var rows = InventoryGui.DevBookDropsIn(p.Key, p.Scene);
                            var found = rows.Where(d => fragments.Contains(d.Type)).Select(d => d.Type).Distinct().ToArray();
                            Check(stage == 5 && progressed ? found.SequenceEqual(new[] { p.Fragment }) : found.Length == 0,
                                p.Key + "/stage=" + stage + "/cleared=" + progressed);
                            Check(rows.SequenceEqual(EnemyDrops.In(p.Key, p.Scene)), "预览与结算表一致/" + p.Key);
                            observations.Add(new { p.Key, p.Scene, stage, progressed,
                                Rows = rows.Select(d => new { Type = d.Type.ToString(), d.Min, d.Max, d.Group }).ToArray() });
                        }
                    }
                }
                Shop.DevStage = 5;
                foreach (var p in pillars)
                {
                    VanillaBossProgression.DevClears = new HashSet<string> { "HollowKnight" };
                    var paid = new Dictionary<ItemType, int>();
                    EnemyDrops.DevSink = (type, count) => paid[type] = paid.TryGetValue(type, out int n) ? n + count : count;
                    // Roll is the real kill settlement path, including world credit before the drop-table query.
                    EnemyDrops.Roll(p.Key, Vector2.zero, true, true, scene: p.Scene);
                    Check(VanillaBossProgression.Cleared(p.Key), "首次四柱胜利记入战况/" + p.Key);
                    Check(paid.TryGetValue(p.Fragment, out int amount) && amount >= 18, "首次胜利给对应碎片/" + p.Key);
                    Check(paid.Keys.Where(fragments.Contains).SequenceEqual(new[] { p.Fragment }), "不混入其他柱碎片/" + p.Key);
                    observations.Add(new { p.Key, FirstVictory = true, Paid = paid.ToDictionary(kv => kv.Key.ToString(), kv => kv.Value) });
                    yield return null;
                }
                VanillaBossProgression.DevClears = new HashSet<string>(VanillaBossProgression.All.Keys);
                foreach (var p in pillars)
                {
                    var reference = BestiaryCatalogue.Of(p.Key, p.Scene);
                    Check(reference.Any(d => d.Type == p.Fragment), "全部资料有对应碎片/" + p.Key);
                    Check(!reference.Any(d => fragments.Contains(d.Type) && d.Type != p.Fragment), "全部资料不串柱/" + p.Key);
                    InventoryGui.DevBestiaryShow(p.Key);
                    Check(InventoryGui.DevBestiaryDrops(p.Key).Any(d => d.Type == p.Fragment), "默认地区图鉴有对应碎片/" + p.Key);
                    Check(InventoryGui.DevJournalChance(p.Key, p.Fragment) != null, "最终显示卡片有对应碎片/" + p.Key);
                    foreach (var other in fragments.Where(t => t != p.Fragment))
                        Check(InventoryGui.DevJournalChance(p.Key, other) == null, "最终显示卡片不串柱/" + p.Key + "/" + other);
                }
                mod.ApplyCreative(true);
                InventoryGui.DevBookCatalogue = false;
                InventoryGui.Open = true;
                InventoryGui.DevBestiaryShow("NailBros");
                yield return new WaitForSecondsRealtime(2f);
                Shot(GameManager.instance.sceneName, "nailbros_current");
                yield return new WaitForSecondsRealtime(.3f);
                InventoryGui.DevBookCatalogue = true;
                yield return new WaitForSecondsRealtime(.7f);
                Shot(GameManager.instance.sceneName, "nailbros_catalogue");
                yield return new WaitForSecondsRealtime(.3f);
                observations.Add(new { Display = "NailBros", ExpectedName = ItemDb.Get(ItemType.FragmentSolar).Name,
                    Chance = InventoryGui.DevJournalChance("NailBros", ItemType.FragmentSolar), UiRendered = InventoryGui.DevBookFrames > 0 });
            }
            finally
            {
                Shop.DevStage = savedStage; LootStage.Load(savedSelection);
                InventoryGui.DevBestiarySelect(-11 - savedPreview);
                VanillaBossProgression.DevClears = savedClears;
                GameDifficulty.Current = savedDifficulty;
                EnemyDrops.DevSink = savedSink; UnityEngine.Random.state = savedRandom;
                mod.ApplyCreative(savedCreative);
                InventoryGui.DevBookCatalogue = savedCatalogue;
                InventoryGui.DevBestiary(false);
                InventoryGui.Open = false;
            }
            File.WriteAllText(Path.Combine(DevCopy.SaveDir, "bestiary-pillars.json"),
                JsonConvert.SerializeObject(new { checks, failures, observations }, Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] 四柱图鉴检查 {checks}，失败 {failures}");
        }
    }
}
