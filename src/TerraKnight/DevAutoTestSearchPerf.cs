using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using TerraKnight.Items;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator SearchPerfTest()
        {
            if(!DevCopy.Verified)throw new InvalidOperationException("Isolated save required");
            yield return GoTo("Town","left1");yield return new WaitForSecondsRealtime(2f);
            var creative=mod.Creative;int stage=Shop.DevStage;
            try {
                mod.ApplyCreative(true);Shop.DevStage=5;PlayerData.instance.isInvincible=true;
                // The pinyin matcher on its own: full spellings, initials, mixed, polyphones and ü.
                foreach(var c in new[]{("天顶剑","tiandingjian"),("天顶剑","tdj"),("天顶剑","tdjian"),("灵魂","linghun"),("灵魂","lh"),("重庆","chongqing"),("重庆","zhongqing"),("女","nv"),("绿","lv")})
                    Check("拼音搜索 "+c.Item1+" / "+c.Item2, UI.PinyinSearch.Matches(c.Item1,c.Item2), c.Item1+" 应能用 "+c.Item2+" 搜到");
                Check("拼音不误报", !UI.PinyinSearch.Matches("天顶剑","tiandingjina"), "tiandingjina 不该命中 天顶剑");
                InventoryGui.Open=false;yield return SearchPerfFrames("closed");
                InventoryGui.Open=true;InventoryGui.DevBestiary(true);yield return SearchPerfFrames("book-cold");
                foreach(string query in new[]{"远古","木","灵魂","zzzz-no-result","远古"}) {
                    InventoryGui.DevBookSearch=query;
                    yield return SearchPerfFrames("query-"+query);
                    while(InventoryGui.DevSearchPending)yield return null;
                    var watch=Stopwatch.StartNew();int count=0;
                    foreach(string rec in EnemyDrops.DevRecords)if(InventoryGui.DevBestiarySearchMatches(rec))count++;
                    Plugin.Log.LogInfo($"[AutoTest] SEARCH_PERF query={query} scan={watch.Elapsed.TotalMilliseconds:F2}ms results={count}");
                    var rows=InventoryGui.DevBookRows();bool same=true;
                    foreach(string rec in rows)if(!InventoryGui.DevBestiarySearchMatches(rec))same=false;
                    Plugin.Log.LogInfo($"[AutoTest] SEARCH_PERF visible-results {(same?"通过":"不通过")} rows={rows.Count}");
                    int rebuilds=InventoryGui.DevBookRebuilds;InventoryGui.DevBookType(true);
                    yield return SearchPerfFrames("focus-"+query);InventoryGui.DevBookType(false);
                    Plugin.Log.LogInfo($"[AutoTest] SEARCH_PERF focus rebuilds={InventoryGui.DevBookRebuilds-rebuilds}");
                }
                var sw=Stopwatch.StartNew();double worst=0;
                while(!RecipeTracking.SourcesReady){var chunk=Stopwatch.StartNew();RecipeTracking.TickSources();worst=Math.Max(worst,chunk.Elapsed.TotalMilliseconds);yield return null;}
                bool farm=RecipeTracking.Farmable(ItemType.AncientChisel);
                Plugin.Log.LogInfo($"[AutoTest] SEARCH_PERF farmable total={sw.Elapsed.TotalMilliseconds:F2}ms worst-chunk={worst:F2}ms value={farm}");
                int probabilities=0,errors=0;
                foreach(string record in EnemyDrops.DevRecords) {
                    foreach(var home in BestiaryHabitats.Of(record)) {
                        var drops=InventoryGui.DevBookDropsIn(record,home.Scene);
                        var summary=new EnemyDrops.ChanceSummary(drops,5);var types=new HashSet<ItemType>();
                        foreach(var row in drops)if(types.Add(row.Type)) {
                            float expected=EnemyDrops.ItemChance(drops,row.Type,5);
                            float actual=summary.Items.TryGetValue(row.Type,out float p)?p:0;
                            probabilities++;if(Math.Abs(expected-actual)>.000001f)errors++;
                        }
                    }
                    yield return null;
                }
                Plugin.Log.LogInfo($"[AutoTest] SEARCH_PERF chance-equivalence {(errors==0?"通过":"不通过")} checks={probabilities} failures={errors}");
                InventoryGui.DevBookSearch="";InventoryGui.DevBookType(true);
                if(Keyboard.current!=null) {
                    foreach(char c in "灵魂") {InputSystem.QueueTextEvent(Keyboard.current,c);yield return null;}
                    yield return null;
                    Plugin.Log.LogInfo($"[AutoTest] SEARCH_PERF Chinese-text {(InventoryGui.DevBookSearch=="灵魂"?"通过":"不通过")} text={InventoryGui.DevBookSearch}");
                }
                InventoryGui.DevBookType(false);yield return new WaitForSecondsRealtime(.5f);Shot("Town","search_perf_final");
                InventoryGui.Open=false;yield return SearchPerfFrames("closed-after");
                yield return GuardedRecipeTracking();
            } finally {InventoryGui.Open=false;mod.ApplyCreative(creative);Shop.DevStage=stage;}
        }
        IEnumerator SearchPerfFrames(string label)
        {
            var frames=new List<float>();int gc=GC.CollectionCount(0);float until=Time.realtimeSinceStartup+2f;
            while(Time.realtimeSinceStartup<until){yield return null;frames.Add(Time.unscaledDeltaTime);}
            Plugin.Log.LogInfo($"[AutoTest] SEARCH_PERF {label} {FrameStats(frames)} GC={GC.CollectionCount(0)-gc}");
        }
    }
}
