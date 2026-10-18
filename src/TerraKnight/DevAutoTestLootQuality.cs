using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using TerraKnight.Items;
using TerraKnight.Town;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator LootQualityTest()
        {
            if(!DevCopy.Verified)throw new InvalidOperationException("Isolated saves required");
            yield return GoTo("Town","left1");
            int checks=0,failures=0,carpets=0,stageWas=Shop.DevStage;
            var difficultyWas=GameDifficulty.Current;var rng=UnityEngine.Random.state;var sink=EnemyDrops.DevSink;
            void Check(string name,bool pass,string detail="")
            {checks++;if(!pass){failures++;Plugin.Log.LogError($"[AutoTest] 战利品品质 {name} {detail} 不通过");}}
            var explorationRoutes=new List<object>();var bossMapping=new List<object>();var mineralRoutes=new List<object>();var godhomeSamples=new List<object>();
            var samples=new List<object>();var shops=new List<object>();var resources=new List<object>();var catalogue=new List<object>();var regions=new List<object>();
            var all=(NpcDef[])typeof(TownNpcs).GetProperty("All",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
            var setup=typeof(TownNpcs).GetMethod("SetupShop",BindingFlags.NonPublic|BindingFlags.Static);
            var traveler=all.First(n=>n.Kind==NpcKind.Visitor);var travelItems=traveler.Items;
            var stockTravel=typeof(TownNpcs).GetMethod("StockTraveler",BindingFlags.NonPublic|BindingFlags.Static);
            var dayField=typeof(TownNpcs).GetField("travelDay",BindingFlags.NonPublic|BindingFlags.Static);
            var stageField=typeof(TownNpcs).GetField("travelStage",BindingFlags.NonPublic|BindingFlags.Static);
            var dayWas=dayField.GetValue(null);var travelStageWas=stageField.GetValue(null);
            var previousWeapons=new HashSet<ItemType>();
            try
            {
                GameDifficulty.Current=DifficultyLevel.Normal;UnityEngine.Random.InitState(101731);
                var records=EnemyDrops.DevRecords.ToArray();
                for(int stage=0;stage<6;stage++)
                {
                    Shop.DevStage=stage;
                    var shopWeapons=new HashSet<ItemType>(TownNpcs.DevSourcesAt(stage).Where(s=>ProgressionLoot.CombatWeapon(ItemDb.Get(s.Type))).Select(s=>s.Type));
                    Check("阶段商店有新武器/"+stage,shopWeapons.Except(previousWeapons).Any());
                    foreach(var source in TownNpcs.DevSourcesAt(stage).Where(s=>ProgressionLoot.CombatWeapon(ItemDb.Get(s.Type))).Distinct())
                    {
                        var d=ItemDb.Get(source.Type);
                        catalogue.Add(new {Stage=stage,source.Npc,NpcName=source.Npc=="TravelingMerchant" ? "旅商" : all.First(n=>n.Internal==source.Npc).Zh,
                            Type=source.Type.ToString(),d.Name,Price=Shop.PriceOf(d),Dps=ProgressionBalance.WeaponDps(d,Areas.Defense(stage)),
                            New=!previousWeapons.Contains(source.Type),Exclusive=ProgressionBalance.ShopOnlyWeapons.Contains(source.Type)});
                    }
                    foreach(var npc in all.Where(n=>n.Stage<=stage && n.Kind!=NpcKind.Visitor))
                    {
                        setup.Invoke(null,new object[]{npc});
                        foreach(var stock in ProgressionBalance.ShopStock(npc.Internal,npc.Items)
                            .Where(s=>s.Stage<=stage && ItemDb.Get(s.Type).Stage<=stage && ProgressionLoot.CombatWeapon(ItemDb.Get(s.Type))))
                        {
                            bool phase=stock.Phases==null || stock.Phases.Contains(TownNpcs.MoonPhase);
                            Check("上架武器可见/"+npc.Internal+"/"+stage+"/"+stock.Type,!phase || TownNpcs.ShopItems.Any(i=>i.Type==stock.Type));
                            shops.Add(new {Stage=stage,Npc=npc.Internal,NpcName=npc.Zh,Type=stock.Type.ToString(),ItemDb.Get(stock.Type).Name,
                                ItemStage=ItemDb.Get(stock.Type).Stage,SaleStage=stock.Stage,Price=Shop.PriceOf(ItemDb.Get(stock.Type)),
                                Dps=ProgressionBalance.WeaponDps(ItemDb.Get(stock.Type),Areas.Defense(stage)),
                                New=!previousWeapons.Contains(stock.Type),Exclusive=ProgressionBalance.ShopOnlyWeapons.Contains(stock.Type),Phases=stock.Phases});
                        }
                        Check("商店无越阶/"+npc.Internal+"/"+stage,TownNpcs.ShopItems.All(i=>i.IsEmpty || i.Def.Stage<=stage));
                    }
                    if(stage>=1)
                    {
                        stockTravel.Invoke(null,new object[]{traveler,stage});setup.Invoke(null,new object[]{traveler});
                        Check("同日转阶段刷新旅商/"+stage,(int)stageField.GetValue(null)==stage);
                        Check("旅商无未解锁占位/"+stage,traveler.Items.All(s=>ItemDb.Get(s.Type).Stage<=stage));
                        Check("旅商有武器/"+stage,traveler.Items.Any(s=>ProgressionLoot.CombatWeapon(ItemDb.Get(s.Type))));
                        if(stage==5)Check("五阶段旅商保证新武器",traveler.Items.Any(s=>s.Type==ItemType.PulseBow));
                        foreach(var s in traveler.Items.Where(s=>ProgressionLoot.CombatWeapon(ItemDb.Get(s.Type))))
                            shops.Add(new {Stage=stage,Npc="TravelingMerchant",NpcName="旅商",Type=s.Type.ToString(),ItemDb.Get(s.Type).Name,
                                ItemStage=ItemDb.Get(s.Type).Stage,SaleStage=s.Stage,Price=Shop.PriceOf(ItemDb.Get(s.Type)),
                                Dps=ProgressionBalance.WeaponDps(ItemDb.Get(s.Type),Areas.Defense(stage)),New=!previousWeapons.Contains(s.Type),
                                Exclusive=ProgressionBalance.ShopOnlyWeapons.Contains(s.Type),Phases=(int[])null});
                    }
                    previousWeapons=shopWeapons;
                    foreach(var rec in records)
                    {
                        var rows=EnemyDrops.At(rec,stage,false,false,false);
                        Check("商店特色不混普通掉落/"+rec+"/"+stage,!rows.Any(d=>ProgressionBalance.ShopOnlyWeapons.Contains(d.Type)));
                        foreach(var d in rows.Where(d=>d.Group==200 || d.Group==201 || d.Group==202))
                        {
                            ProgressionLoot.ResourceRange(d.Type,ProgressionLoot.Elite(rec,EnemyDrops.EntryFor(rec)),ProgressionBalance.FormalBoss(rec),out int lo,out int hi);
                            Check("材料独立数量范围/"+rec+"/"+stage+"/"+d.Type,d.Min>=lo && d.Max<=hi && d.MeanCount==0);
                        }
                        Check("表内鱼饵单只/"+rec+"/"+stage,rows.Where(d=>ItemDb.Get(d.Type).BaitPower>0).All(d=>d.Min==1 && d.Max==1 && d.MeanCount==0));
                    }
                    yield return null;
                }
                foreach(var type in new[]{ItemType.Gel,ItemType.FallenStar,ItemType.SoulofLight,ItemType.Ectoplasm,ItemType.BlackLens,
                    ItemType.BrokenHeroSword,ItemType.TurtleShell,ItemType.IronOre,ItemType.ChlorophyteOre,ItemType.LunarOre})
                foreach(bool elite in new[]{false,true})
                {
                    ProgressionLoot.ResourceRange(type,elite,false,out int min,out int max);
                    resources.Add(new {Type=type.ToString(),ItemDb.Get(type).Name,ItemDb.Get(type).Rare,Elite=elite,Min=min,Max=max});
                }
                // Terraria progression identity and actual Godhome death/bag paths.
                Check("所有首领有独特标志物",ProgressionBalance.BossRewards.Keys.Where(ProgressionBalance.FormalBoss)
                    .Select(k=>ProgressionBalance.BossSignatures[k]).Distinct().Count()==ProgressionBalance.BossRewards.Keys.Count(ProgressionBalance.FormalBoss));
                foreach(var pair in ProgressionBalance.BossRewards.Where(p=>ProgressionBalance.FormalBoss(p.Key))) {
                    Shop.DevStage=pair.Value.Stage;
                    var signature=ProgressionBalance.BossSignatures[pair.Key];
                    var rewardRows=EnemyDrops.At(pair.Key,pair.Value.Stage,false,false,false);
                    Check("首领专属标志物保证掉落/"+pair.Key,rewardRows.Any(d=>d.Type==signature && d.Group==0 && d.Chance==1));
                    Check("首领标志物同期/"+pair.Key,ItemDb.Get(signature).Stage<=pair.Value.Stage);
                    bossMapping.Add(new {Record=pair.Key,EnemyDrops.EntryFor(pair.Key).Zh,Stage=pair.Value.Stage,
                        Signature=signature.ToString(),SignatureName=ItemDb.Get(signature).Name,
                        Weapons=rewardRows.Where(d=>d.Group==100 && ProgressionLoot.CombatWeapon(ItemDb.Get(d.Type))).Select(d=>new{Type=d.Type.ToString(),ItemDb.Get(d.Type).Name,ItemDb.Get(d.Type).Stage}).ToArray()});
                }
                Check("Boss奖励不用探索杂物",ProgressionBalance.BossRewards.Keys.Where(ProgressionBalance.FormalBoss)
                    .All(k=>!ProgressionBalance.BasicBossExcluded.Contains(ProgressionBalance.BossSignatures[k])));
                foreach(var pair in ProgressionBalance.BossRewards.Where(p=>ProgressionBalance.FormalBoss(p.Key))) {
                    var rows=EnemyDrops.At(pair.Key,pair.Value.Stage,true,false,false);
                    Check("Boss表没有低级探索饰品/"+pair.Key,!rows.Any(d=>ProgressionBalance.BasicBossExcluded.Contains(d.Type)));
                    Check("Boss主装备不降档/"+pair.Key,rows.Where(d=>d.Group==100).All(d=>ItemDb.Get(d.Type).Stage==pair.Value.Stage && ItemDb.Get(d.Type).Rare>0));
                }
                Shop.DevStage=0;
                foreach(var source in ProgressionBalance.ExplorationSources) {
                    Check("探索物来源是普通怪/"+source.Key,EnemyDrops.DevRecords.Contains(source.Key) && !ProgressionBalance.FormalBoss(source.Key));
                    var rows=EnemyDrops.At(source.Key,0,false,false,false);
                    foreach(var type in source.Value) {
                        Check("开局探索物可刷/"+source.Key+"/"+type,ItemDb.Get(type).Stage==0 && rows.Any(d=>d.Type==type && d.Group==103));
                        var drop=rows.First(d=>d.Type==type && d.Group==103);
                        explorationRoutes.Add(new {Record=source.Key,EnemyDrops.EntryFor(source.Key).Zh,Type=type.ToString(),ItemDb.Get(type).Name,
                            Chance=EnemyDrops.MarginalChance(rows,drop),Stage=ItemDb.Get(type).Stage});
                    }
                }
                Shop.DevStage=0;
                carpets=0;
                EnemyDrops.DevSink=(t,n)=>{if(t==ItemType.FlyingCarpet)carpets+=n;};
                for(int i=0;i<20000;i++)EnemyDrops.Roll("Crawler",Vector2.zero,true,false);
                Check("开局小爬虫实际掉飞毯",carpets>0,"飞毯="+carpets);
                EnemyDrops.DevSink=null;
                foreach(var anchor in new[]{("Hornet",ItemType.DemoniteOre),("DungDefender",ItemType.ShadowScale),("FlukeMother",ItemType.TissueSample),("HiveKnight",ItemType.BeeWax)}) {
                    int phase=ProgressionBalance.BossRewards[anchor.Item1].Stage;
                    var row=EnemyDrops.At(anchor.Item1,phase,false,false,false).First(d=>d.Type==anchor.Item2);
                    Check("原版首领材料组合/"+anchor.Item1,row.Chance==1 && (row.Group==0 || row.Group==209));
                }
                Check("八种基础矿不依赖首个Boss",ProgressionLoot.MineralBands[0].Length==8 && ProgressionLoot.MineralBands[0].All(t=>ItemDb.Get(t).Stage==0));
                Check("世纪之花后加工链不提前",new[]{ItemType.Ectoplasm,ItemType.Autohammer,ItemType.ShroomiteBar,ItemType.SpectreBar}.All(t=>ItemDb.Get(t).Stage==5));
                Check("大黄蜂对应克苏鲁之眼",ProgressionBalance.BossSignatures["Hornet"]==ItemType.ShieldOfCthulhu);
                foreach(int phase in Enumerable.Range(0,6)) {
                    Shop.DevStage=phase;
                    var available=new HashSet<ItemType>();
                    foreach(var area in ProgressionLoot.RegionalProfiles.Keys.Where(a=>ProgressionLoot.AreaBand(a)<=phase)) {
                        var primary=ProgressionLoot.PrimaryOres(area,phase);
                        Check("地区矿物不越阶/"+phase+"/"+area,primary.All(t=>ItemDb.Get(t).Stage<=phase));
                        foreach(var t in primary)available.Add(t);
                    }
                    var reachable=records.Where(r=>ProgressionBalance.FormalBoss(r) ? ProgressionBalance.BossRewards[r].Stage<=phase
                        : ProgressionLoot.NativeTier(r)<=phase && Areas.LevelOf(EnemyDrops.SceneOf(r))<=phase).ToArray();
                    var allRows=reachable.SelectMany(r=>EnemyDrops.At(r,phase,false,false,false)).ToArray();
                    foreach(var ore in ProgressionLoot.MineralBands.SelectMany(b=>b).Where(t=>ItemDb.Get(t).Stage==phase))
                    {
                        var sources=reachable.Where(r=>EnemyDrops.At(r,phase,false,false,false).Any(d=>d.Type==ore)).ToArray();
                        Check("原版矿物有同期来源/"+ore,sources.Length>0);
                        mineralRoutes.Add(new {Stage=phase,Type=ore.ToString(),ItemDb.Get(ore).Name,Sources=sources});
                    }
                    foreach(var gem in new[]{ItemType.Amethyst,ItemType.Topaz,ItemType.Sapphire,ItemType.Emerald,ItemType.Ruby,ItemType.Diamond,ItemType.Amber,ItemType.Obsidian,ItemType.SturdyFossil}
                        .Where(t=>ItemDb.Get(t).Stage==phase))
                        Check("宝石黑曜石化石有同期来源/"+gem,allRows.Any(d=>d.Type==gem));
                    foreach(var r in reachable.Where(r=>!ProgressionBalance.FormalBoss(r) && !ProgressionLoot.NamedElites.Contains(r))) {
                        var area=ProgressionLoot.LootArea(r,EnemyDrops.EntryFor(r));
                        int cap=Math.Min(5,ProgressionLoot.AreaBand(area)+1);
                        Check("普通怪装备符合地区强度/"+phase+"/"+r,EnemyDrops.At(r,phase,false,false,false)
                            .Where(d=>ProgressionLoot.CombatWeapon(ItemDb.Get(d.Type)) || ItemDb.Get(d.Type).IsArmor).All(d=>ItemDb.Get(d.Type).Stage<=cap));
                    }
                }
                Shop.DevStage=5;
                foreach(var difficulty in new[]{DifficultyLevel.Normal,DifficultyLevel.Expert,DifficultyLevel.Master}) {
                    GameDifficulty.Current=difficulty;
                    int lunar=0,shield=0,bags=0;
                    EnemyDrops.DevSink=(t,n)=>{if(t==ItemType.LunarOre)lunar+=n;if(t==ItemType.ShieldOfCthulhu)shield+=n;if(TreasureBags.Is(t))bags+=n;};
                    EnemyDrops.Roll("Hornet",Vector2.zero,true,false,scene:"GG_Hornet_1");
                    Check("神居实杀夜明矿/"+difficulty,lunar>=70 && lunar<=120,"矿="+lunar);
                    int paidLunar=lunar;
                    // Opening table retains Godhome identity, but never duplicates the outside lunar payout.
                    var godRows=ProgressionLoot.Of("Hornet",EnemyDrops.EntryFor("Hornet"),5,false,false,false,"GG_Hornet_1");
                    Check("神居主奖励终局/"+difficulty,godRows.Where(d=>d.Group==100).All(d=>ItemDb.Get(d.Type).Stage==5));
                    Check("神居矿物放宝藏袋外",godRows.Where(d=>d.Type==ItemType.LunarOre).All(TreasureBags.Outside));
                    var inv=mod.Inventory;int bagSlot=30;var savedSlot=inv.Slots[bagSlot];
                    try {
                        var marked=ItemStack.Of(ItemType.BagHornet);marked.Forge=2;
                        var restored=Save.ModSave.DevRoundTrip(marked);
                        Check("神居宝藏袋保存来源/"+difficulty,restored.Forge==2);
                        Check("神居重战不误标首杀/"+difficulty,!(restored.Def.Tooltip ?? "").Contains(Lang.T("首次击败：必得首领武器")));
                        inv.Slots[bagSlot]=restored;
                        Check("神居宝藏袋在城镇实际打开/"+difficulty,TreasureBags.TryOpen(inv,bagSlot));
                        Check("神居开袋不重复发矿/"+difficulty,lunar==paidLunar,"矿="+lunar);
                        Check("神居开袋仍有大黄蜂专属盾/"+difficulty,shield>=1);
                        godhomeSamples.Add(new {Difficulty=difficulty.ToString(),LunarOre=lunar,Shield=shield,Bags=bags,
                            Main=godRows.Where(d=>d.Group==100).Select(d=>new{Type=d.Type.ToString(),ItemDb.Get(d.Type).Stage}).ToArray()});
                    } finally {inv.Slots[bagSlot]=savedSlot;}
                }
                GameDifficulty.Current=DifficultyLevel.Normal;
                EnemyDrops.DevSink=null;
                Check("商店特色获取分类",ProgressionBalance.ShopOnlyWeapons.All(t=>ItemDb.Get(t).Source==ItemSource.Shop));
                Shop.DevStage=5;
                var regionalSets=new Dictionary<string,HashSet<ItemType>>();
                foreach(var area in records.Where(r=>!ProgressionBalance.FormalBoss(r) && ProgressionLoot.Eligible(EnemyDrops.EntryFor(r)) && !ProgressionLoot.Spawned(r)).GroupBy(r=>ProgressionLoot.LootArea(r,EnemyDrops.EntryFor(r))))
                {
                    var set=new HashSet<ItemType>(area.SelectMany(r=>EnemyDrops.At(r,5,false,false,false)).Where(d=>!d.Master).Select(d=>d.Type)
                        .Where(t=>{var d=ItemDb.Get(t);return d.HealLife==0 && d.HealMana==0 && t!=ItemType.Heart && t!=ItemType.Wood && t!=ItemType.StoneBlock;}));
                    regionalSets[area.Key]=set;
                    var profile=ProgressionLoot.RegionalProfiles.TryGetValue(area.Key,out var p) ? p : Array.Empty<ItemType>();
                    Check("地区特产实际存在/"+area.Key,profile.All(set.Contains),string.Join(",",profile.Where(t=>!set.Contains(t))));
                    regions.Add(new {Area=area.Key,Records=area.ToArray(),Profile=profile.Select(t=>new {Type=t.ToString(),ItemDb.Get(t).Name,ItemDb.Get(t).Stage}).ToArray(),
                        Items=set.OrderBy(t=>t).Select(t=>new {Type=t.ToString(),ItemDb.Get(t).Name}).ToArray()});
                }
                foreach(var pair in new[]{("苍绿之径","真菌荒地"),("泪水之城","水晶山峰"),("王国边缘","深巢")})
                {
                    var left=regionalSets[pair.Item1];var right=regionalSets[pair.Item2];
                    float overlap=left.Intersect(right).Count()/(float)left.Union(right).Count();
                    Check("地区掉落差异/"+pair.Item1+"/"+pair.Item2,overlap<.8f,"交并比="+overlap);
                }
                var entry=EnemyDrops.EntryFor("Crawler");
                string sceneA=EnemyDrops.SceneOf("MossWalker"),sceneB=EnemyDrops.SceneOf("ZombieRunner");
                var a=ProgressionLoot.Of("Crawler",entry,5,false,false,false,sceneA);
                var b=ProgressionLoot.Of("Crawler",entry,5,false,false,false,sceneB);
                Check("同怪不同地区缓存隔离",!a.Select(d=>d.Type).ToHashSet().SetEquals(b.Select(d=>d.Type))
                    && a.SequenceEqual(ProgressionLoot.Of("Crawler",entry,5,false,false,false,sceneA)));
                var onlyA=a.Where(d=>ItemDb.IsOre(d.Type) && !b.Any(o=>o.Type==d.Type)).OrderByDescending(d=>EnemyDrops.MarginalChance(a,d)).First();
                var onlyB=b.Where(d=>ItemDb.IsOre(d.Type) && !a.Any(o=>o.Type==d.Type)).OrderByDescending(d=>EnemyDrops.MarginalChance(b,d)).First();
                foreach(string scene in new[]{sceneA,sceneB})
                {
                    int pa=0,pb=0;
                    EnemyDrops.DevSink=(t,n)=>{if(t==onlyA.Type)pa+=n;if(t==onlyB.Type)pb+=n;};
                    for(int i=0;i<2000;i++)EnemyDrops.Roll("Crawler",Vector2.zero,true,false,scene:scene);
                    Check("真实击杀采用死亡地区/"+scene,scene==sceneA ? pa>0 && pb==0 : pb>0 && pa==0,"A="+pa+" B="+pb);
                }
                foreach(var run in new[]{("Crawler",0),("ZombieGuard",0),("MossWalker",1),("BeamMiner",3),("GiantHopper",4),("MossmanRunner",5)})
                {
                    Shop.DevStage=run.Item2;
                    int weapons=0,bait=0,maxBait=0,maxResource=0,totalResource=0;
                    EnemyDrops.DevSink=(t,n)=>{var d=ItemDb.Get(t);if(ProgressionLoot.CombatWeapon(d))weapons++;
                        if(d.BaitPower>0){bait+=n;maxBait=Math.Max(maxBait,n);}
                        else if(d.MaxStack>1 && !d.IsWeapon && !d.IsAmmo && !d.HkItem && d.HealLife==0 && d.HealMana==0)
                        {totalResource+=n;maxResource=Math.Max(maxResource,n);}};
                    const int kills=20000;
                    for(int k=0;k<kills;k++)
                    {
                        EnemyDrops.Roll(run.Item1,Vector2.zero,true,false);
                        // Include the actual regional extra-drop path independent of the fixture's Town location.
                        Fishing.KillDrops(run.Item1,Vector2.zero,true,EnemyDrops.SceneOf(run.Item1));
                    }
                    Check("万次击杀鱼饵有界/"+run.Item1,bait<kills*.035f && maxBait<=1,"鱼饵="+bait);
                    if(run.Item1=="Crawler")Check("普通武器提升/实抽",Math.Abs(weapons/(float)kills-.07f)<.008f,"武器="+weapons);
                    if(run.Item1=="ZombieGuard")Check("精英武器提升/实抽",weapons/(float)kills>.12f,"武器="+weapons);
                    samples.Add(new {Record=run.Item1,Stage=run.Item2,Kills=kills,Weapons=weapons,Bait=bait,MaxBait=maxBait,TotalResources=totalResource,MaxResourceStack=maxResource});
                    yield return null;
                }
            }
            finally
            {
                EnemyDrops.DevSink=sink;Shop.DevStage=stageWas;GameDifficulty.Current=difficultyWas;UnityEngine.Random.state=rng;
                traveler.Items=travelItems;dayField.SetValue(null,dayWas);stageField.SetValue(null,travelStageWas);
                Array.Clear(TownNpcs.ShopItems,0,TownNpcs.ShopItems.Length);Array.Clear(TownNpcs.BuyBack,0,TownNpcs.BuyBack.Length);
            }
            File.WriteAllText(Path.Combine(DevCopy.SaveDir,"loot-quality.json"),JsonConvert.SerializeObject(new {Checks=checks,Failures=failures,ExplorationRoutes=explorationRoutes,FlyingCarpetsSample=carpets,BossMapping=bossMapping,MineralRoutes=mineralRoutes,GodhomeSamples=godhomeSamples,Shops=shops,Catalogue=catalogue,Regions=regions,Resources=resources,Samples=samples},Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] 战利品品质 汇总 {checks}项 失败{failures} {(failures==0 ? "通过" : "不通过")}");
        }
    }
}
