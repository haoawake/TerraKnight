using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator BestiarySearchAudit()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            int savedStage = Shop.DevStage;
            bool creative = mod.Creative;
            var savedClears = VanillaBossProgression.DevClears;
            var results = new List<object>();
            int failures = 0;
            try
            {
                yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
                mod.ApplyCreative(true);
                InventoryGui.Open = true;
                InventoryGui.DevBestiaryShow("ZombieMiner");
                var records = EnemyDrops.DevRecords.ToArray();
                string[] Direct(ItemType type, int stage) => records.Where(rec =>
                    BestiaryHabitats.Of(rec).Any(home => InventoryGui.DevBookDropsIn(rec, home.Scene)
                        .Any(d => d.Type == type && !EnemyDrops.Locked(d, stage)))).ToArray();
                foreach (var type in new[] { ItemType.Hellstone, ItemType.Meteorite, ItemType.ChlorophyteOre })
                {
                    int stage = type == ItemType.ChlorophyteOre ? 4 : 2;
                    Shop.DevStage = stage;
                    VanillaBossProgression.DevClears = new HashSet<string>();
                    InventoryGui.DevBookSearch = "";
                    InventoryGui.DevBookSearch = ItemDb.Get(type).Name;
                    var before = records.Where(InventoryGui.DevBestiarySearchMatches).ToArray();
                    // Change only boss progression: no query, inventory, stage or discovery-count change.
                    VanillaBossProgression.DevClears = new HashSet<string>(VanillaBossProgression.All
                        .Where(p => p.Value.Band <= stage).Select(p => p.Key));
                    var direct = Direct(type, stage);
                    var warm = records.Where(InventoryGui.DevBestiarySearchMatches).ToArray();
                    InventoryGui.DevBookSearch = "";
                    InventoryGui.DevBookSearch = ItemDb.Get(type).Name;
                    var fresh = records.Where(InventoryGui.DevBestiarySearchMatches).ToArray();
                    if (!direct.SequenceEqual(warm) || !direct.SequenceEqual(fresh))
                    {
                        failures++;
                        Plugin.Log.LogError($"[AutoTest] 搜索进度刷新 {type} 不通过");
                    }
                    results.Add(new { Type = type.ToString(), Stage = stage, Before = before,
                        Direct = direct, Warm = warm, Fresh = fresh,
                        WarmMatches = direct.SequenceEqual(warm), FreshMatches = direct.SequenceEqual(fresh) });
                    Plugin.Log.LogInfo($"[AutoTest] SEARCH_AUDIT {type} before={before.Length} direct={direct.Length} warm={warm.Length} fresh={fresh.Length}");
                }
                // Exhaustive item/drop-name matching across every habitat, with the stage's boss prerequisites met.
                int checkedItems = 0;
                var mismatches = new List<string>();
                for (int stage = 0; stage <= 5; stage++)
                {
                    Shop.DevStage = stage;
                    VanillaBossProgression.DevClears = new HashSet<string>(VanillaBossProgression.All
                        .Where(p => p.Value.Band <= stage).Select(p => p.Key));
                    var byType = new Dictionary<ItemType, HashSet<string>>();
                    foreach (var rec in records)
                        foreach (var home in BestiaryHabitats.Of(rec))
                            foreach (var d in InventoryGui.DevBookDropsIn(rec, home.Scene))
                            {
                                if (EnemyDrops.Locked(d, stage)) continue;
                                if (!byType.TryGetValue(d.Type, out var set)) byType[d.Type] = set = new HashSet<string>();
                                set.Add(rec);
                            }
                    foreach (var pair in byType)
                    {
                        InventoryGui.DevBookSearch = "";
                        InventoryGui.DevBookSearch = ItemDb.Get(pair.Key).Name;
                        foreach (var rec in pair.Value)
                            if (!InventoryGui.DevBestiarySearchMatches(rec)) mismatches.Add(stage + ":" + pair.Key + ":" + rec);
                        checkedItems++;
                    }
                    yield return null;
                }
                File.WriteAllText(Path.Combine(DevCopy.SaveDir, "bestiary-search-audit.json"),
                    JsonConvert.SerializeObject(new { Scenarios = results, CheckedItemStages = checkedItems, MissingSearchResults = mismatches, Failures = failures + mismatches.Count }, Formatting.Indented));
                if (mismatches.Count > 0) Plugin.Log.LogError("[AutoTest] 掉落名称搜索覆盖 不通过");
                Plugin.Log.LogInfo($"[AutoTest] SEARCH_AUDIT itemStages={checkedItems} missing={mismatches.Count}");
            }
            finally
            {
                InventoryGui.DevBookSearch = "";
                InventoryGui.DevBestiary(false);
                InventoryGui.Open = false;
                Shop.DevStage = savedStage;
                VanillaBossProgression.DevClears = savedClears;
                mod.ApplyCreative(creative);
            }
        }
    }
}
