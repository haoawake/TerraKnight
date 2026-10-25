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
        IEnumerator CraftProgressionTest()
        {
            if(!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            yield return GoTo("Town","left1");
            int stageWas=Shop.DevStage, checks=0, failures=0;
            var difficultyWas=GameDifficulty.Current;
            var rng=UnityEngine.Random.state;
            var sinkWas=EnemyDrops.DevSink;
            var clearsWas=VanillaBossProgression.DevClears;
            var report=new List<object>();
            void Check(string name,bool pass,string detail="")
            {checks++;if(!pass) {failures++;Plugin.Log.LogError($"[AutoTest] 制作阶段 {name} {detail} 不通过");}}
            // Read the actual placed-station compatibility table, independently of the
            // recipe schedule. A nominal item tier alone is not a usable workstation.
            var stations=new Dictionary<string,HashSet<ItemType>>();
            var specs=(System.Collections.IDictionary)typeof(Stations).GetField("specs",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
            foreach(var value in specs.Values)
            {
                var type=value.GetType();
                var item=(ItemType)type.GetField("Item").GetValue(value);
                foreach(var name in (string[])type.GetField("Counts").GetValue(value))
                {if(!stations.TryGetValue(name,out var set)) stations[name]=set=new HashSet<ItemType>();set.Add(item);}
            }
            bool Ready(Recipe r,HashSet<ItemType> stock) => r.Station=="徒手" || string.IsNullOrEmpty(r.Station)
                || r.Station=="恶魔祭坛 / 猩红祭坛" || stations.TryGetValue(r.Station,out var set) && set.Any(stock.Contains);
            try
            {
                GameDifficulty.Current=DifficultyLevel.Normal;
                UnityEngine.Random.InitState(10162031);
                var records=EnemyDrops.DevRecords.ToArray();
                for(int stage=0;stage<6;stage++)
                {
                    Shop.DevStage=stage;
                    // The bosses of this stage and before beaten (VanillaBossProgression's gates open behind them).
                    VanillaBossProgression.DevClears=new HashSet<string>(VanillaBossProgression.All.Where(b=>b.Value.Band<=stage).Select(b=>b.Key));
                    var stock=new HashSet<ItemType>(ItemDb.All.Where(d=>d.Stage<=stage && d.Source==ItemSource.Basic).Select(d=>d.Type));
                    stock.UnionWith(Town.TownNpcs.DevSourcesAt(stage).Where(x=>ItemDb.Get(x.Type).Stage<=stage).Select(x=>x.Type));
                    foreach(var rec in records)
                    {
                        bool boss=ProgressionBalance.FormalBoss(rec);
                        if(boss ? ProgressionBalance.BossRewards[rec].Stage>stage
                            : ProgressionLoot.NativeTier(rec)>stage || Areas.LevelOf(EnemyDrops.SceneOf(rec))>stage) continue;
                        foreach(var d in EnemyDrops.At(rec,stage,false,stage>=4,stage>=5))
                            if(!d.Master && d.Chance>0 && ProgressionBalance.Unlocked(d.Type,stage,stage>=4,stage>=5)) stock.Add(d.Type);
                    }
                    bool changed;
                    do
                    {
                        changed=false;
                        foreach(var r in Recipes.All)
                            if(!r.IsBought && Ready(r,stock) && r.Ingredients.All(i=>stock.Contains(i.Type))) changed|=stock.Add(r.Result);
                    } while(changed);
                    var targets=new HashSet<ItemType>(ProgressionBalance.Core[stage].SelectMany(x=>x));
                    targets.UnionWith(ProgressionBalance.AccessoryPaths[stage].SelectMany(x=>x));
                    foreach(var p in ProgressionBalance.ArmorPaths.Where(p=>p.Stage==stage)) targets.UnionWith(p.Items);
                    foreach(var t in targets)
                    {
                        bool pass=ItemDb.Get(t).Stage<=stage && stock.Contains(t);
                        Check($"成长装备可达 {t}/{stage}",pass,$"物品阶段{ItemDb.Get(t).Stage}");
                        if(ItemDb.Get(t).Source==ItemSource.Craft)
                        {
                            bool craftable=Recipes.All.Any(r=>r.Result==t && !r.IsBought && Ready(r,stock) && r.Ingredients.All(i=>stock.Contains(i.Type)));
                            Check($"成长装备原版制作链 {t}/{stage}",craftable);
                        }
                    }
                    report.Add(new {Stage=stage,Targets=targets.Select(t=>t.ToString()).ToArray(),Reachable=stock.Select(t=>t.ToString()).ToArray()});
                }
                // One normal-mode route funds its actual vanilla recipe before leaving
                // the tier: no later bosses, entry-clear flag, fishing crates or test gifts.
                foreach(var test in new[] {"Imp","Spider"})
                {
                    int stage=test=="Imp" ? 2 : 3;
                    Shop.DevStage=stage;
                    // The bosses of this stage and before beaten (VanillaBossProgression's gates open behind them).
                    VanillaBossProgression.DevClears=new HashSet<string>(VanillaBossProgression.All.Where(b=>b.Value.Band<=stage).Select(b=>b.Key));
                    var needs=test=="Imp" ? new Dictionary<ItemType,int>{{ItemType.Hellstone,51},{ItemType.Obsidian,17},{ItemType.Hellforge,1}}
                        : new Dictionary<ItemType,int>{{ItemType.MythrilOre,40},{ItemType.SpiderFang,76}};
                    var counts=new Dictionary<ItemType,int>();
                    var samples=new List<int>();
                    for(int trial=0;trial<200;trial++)
                    {
                        counts.Clear();
                        EnemyDrops.DevSink=(t,n)=>{if(needs.ContainsKey(t)) counts[t]=(counts.TryGetValue(t,out int old) ? old : 0)+n;};
                        int kills=0;
                        while(kills<500 && needs.Any(p=>!counts.TryGetValue(p.Key,out int n) || n<p.Value))
                        {
                            string rec=test=="Imp" ? "BeamMiner" : !counts.TryGetValue(ItemType.MythrilOre,out int ore) || ore<40 ? "BeamMiner" : "ShootSpider";
                            EnemyDrops.Roll(rec,Vector2.zero,true,false);
                            kills++;
                        }
                        samples.Add(kills);
                        Check($"材料收集 {test}/{trial}",kills<500 && needs.All(p=>counts.TryGetValue(p.Key,out int n) && n>=p.Value));
                        if(trial%20==0) yield return null;
                    }
                    samples.Sort();
                    int p95=samples[189];
                    Check($"材料收集成本 {test}",p95<=150,$"P95={p95}");
                    Plugin.Log.LogInfo($"[AutoTest] 制作阶段 {test} 200轮收集 平均{samples.Average():F1}次击杀 P95={p95} 最大{samples.Last()} {(p95<=150 ? "通过" : "不通过")}");
                    report.Add(new {Route=test,Stage=stage,Trials=200,MeanKills=samples.Average(),P95=p95,Maximum=samples.Last(),Ingredients=needs.ToDictionary(p=>p.Key.ToString(),p=>p.Value)});
                    EnemyDrops.DevSink=sinkWas;
                    var inv=mod.Inventory;
                    TerraMod.DevAllInfinite=false;
                    mod.ApplyCreative(false);
                    Array.Clear(inv.Slots,0,inv.Slots.Length);
                    inv.Mouse=default;
                    inv.Override=-1;
                    InventoryGui.Open=true;
                    foreach(var p in needs) inv.Add(p.Key,p.Value);
                    Stations.Clear();
                    var hero=HeroController.instance;
                    var region=TerrainSystem.Current.Region;
                    Vector2 position=(Vector2)hero.transform.position-(Vector2)region.Root.position+Vector2.right*2f;
                    Stations.PlaceFixed(StationKind.Furnace,region.SceneName,position);
                    Stations.PlaceFixed(StationKind.IronAnvil,region.SceneName,position);
                    Stations.PlaceFixed(StationKind.DemonAltar,region.SceneName,position);
                    yield return null; // Fixed fixtures bind to their live Region on the next station tick.
                    void Craft(ItemType t,int times=1)
                    {
                        for(int n=0;n<times;n++)
                        {
                            bool made=InventoryGui.DevCraft(t);
                            Check($"同期实物制作 {t}/{stage}/{n}",made && inv.Mouse.Type==t);
                            var produced=inv.Mouse;inv.Mouse=default;
                            if(!produced.IsEmpty) inv.Add(produced.Type,produced.Count);
                        }
                    }
                    if(test=="Imp")
                    {
                        Check("小鬼法杖二阶段",ItemDb.Get(ItemType.ImpStaff).Stage==2 && Recipes.For(ItemType.ImpStaff).StationStage==2);
                        Stations.PlaceFixed(StationKind.Hellforge,region.SceneName,position);
                        yield return null;
                        Craft(ItemType.HellstoneBar,17);
                        Craft(ItemType.ImpStaff);
                        Check("小鬼原料扣除",Recipes.Count(inv,ItemType.Hellstone)==0 && Recipes.Count(inv,ItemType.Obsidian)==0 && Recipes.Count(inv,ItemType.HellstoneBar)==0);
                    }
                    else
                    {
                        Craft(ItemType.MythrilBar,10);
                        Craft(ItemType.MythrilAnvil);
                        Check("原版秘银砧实际制成",Recipes.Count(inv,ItemType.MythrilAnvil)==1);
                        for(int n=0;n<inv.Slots.Length;n++) if(inv.Slots[n].Type==ItemType.MythrilAnvil) {inv.ConsumeAt(n);break;}
                        Stations.PlaceFixed(StationKind.MythrilAnvil,region.SceneName,position);
                        yield return null;
                        foreach(var t in new[] {ItemType.SpiderStaff,ItemType.QueenSpiderStaff,ItemType.SpiderMask,ItemType.SpiderBreastplate,ItemType.SpiderGreaves})
                        {
                            Check($"蜘蛛原版站与阶段 {t}",ItemDb.Get(t).Stage==3 && Recipes.For(t).Station=="秘银砧" && Recipes.For(t).StationStage==3);
                            Craft(t);
                        }
                        Check("蜘蛛原料扣除",Recipes.Count(inv,ItemType.SpiderFang)==0 && Recipes.Count(inv,ItemType.MythrilOre)==0 && Recipes.Count(inv,ItemType.MythrilBar)==0);
                    }
                    InventoryGui.Open=false;
                    yield return null;
                }
                File.WriteAllText(Path.Combine(DevCopy.SaveDir,"craft-progression-audit.json"),JsonConvert.SerializeObject(new {Checks=checks,Failures=failures,Routes=report},Formatting.Indented));
                Plugin.Log.LogInfo($"[AutoTest] 制作阶段 合计 {checks-failures}/{checks} {(failures==0 ? "通过" : "不通过")}");
            }
            finally {VanillaBossProgression.DevClears=clearsWas;Shop.DevStage=stageWas;GameDifficulty.Current=difficultyWas;UnityEngine.Random.state=rng;EnemyDrops.DevSink=sinkWas;InventoryGui.Open=false;}
        }
    }
}
