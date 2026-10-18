using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using TerraKnight.Effects;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator CombatUpgradeTest()
        {
            if(!DevCopy.Verified)throw new InvalidOperationException("Isolated saves required");
            yield return GoTo("Town","left1");
            int previous=Shop.DevStage,checks=0,failed=0;
            var difficulty=GameDifficulty.Current;
            var report=new List<object>();
            void Check(string name,bool pass,string detail="")
            {checks++;if(!pass){failed++;Plugin.Log.LogError($"[AutoTest] 成长平衡 {name} {detail} 不通过");}}
            var stations=new Dictionary<string,HashSet<ItemType>>();
            var specs=(System.Collections.IDictionary)typeof(Stations).GetField("specs",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
            foreach(var value in specs.Values)
            {
                var t=value.GetType();var item=(ItemType)t.GetField("Item").GetValue(value);
                foreach(var name in (string[])t.GetField("Counts").GetValue(value))
                {if(!stations.TryGetValue(name,out var set))stations[name]=set=new HashSet<ItemType>();set.Add(item);}
            }
            bool Ready(Recipe r,HashSet<ItemType> stock)=>string.IsNullOrEmpty(r.Station) || r.Station=="徒手" || r.Station=="恶魔祭坛 / 猩红祭坛"
                || stations.TryGetValue(r.Station,out var set) && set.Any(stock.Contains);
            var records=EnemyDrops.DevRecords.ToArray();
            var prior=new Dictionary<string,HashSet<ItemType>>();
            try
            {
                GameDifficulty.Current=DifficultyLevel.Normal;
                for(int stage=0;stage<6;stage++)
                {
                    Shop.DevStage=stage;
                    var stock=new HashSet<ItemType>(ItemDb.All.Where(d=>d.Stage<=stage && d.Source==ItemSource.Basic).Select(d=>d.Type));
                    stock.UnionWith(Town.TownNpcs.DevSourcesAt(stage).Where(x=>ItemDb.Get(x.Type).Stage<=stage).Select(x=>x.Type));
                    foreach(var rec in records)
                    {
                        bool boss=ProgressionBalance.FormalBoss(rec);
                        if(boss ? ProgressionBalance.BossRewards[rec].Stage>stage : ProgressionLoot.NativeTier(rec)>stage || Areas.LevelOf(EnemyDrops.SceneOf(rec))>stage)continue;
                        var rows=EnemyDrops.At(rec,stage,false,false,false).Where(d=>!d.Master && d.Chance>0).ToArray();
                        var now=new HashSet<ItemType>(rows.Select(d=>d.Type));
                        if(prior.TryGetValue(rec,out var old))Check("升阶保留 "+rec+"/"+stage,old.IsSubsetOf(now),string.Join(",",old.Except(now)));
                        prior[rec]=now;stock.UnionWith(now);
                        Check("不提前掉落 "+rec+"/"+stage,rows.All(d=>ItemDb.Get(d.Type).Stage<=stage));
                        foreach(var g in rows.Where(d=>d.Group>0).GroupBy(d=>d.Group))
                            Check("组合池概率一致 "+rec+"/"+stage+"/"+g.Key,g.All(d=>Math.Abs(d.Chance-g.First().Chance)<.00002f));
                        if(!boss && !ProgressionLoot.NamedElites.Contains(rec))
                        {
                            var raw=(EnemyDrops.Drop[])typeof(ProgressionLoot).GetMethod("Build",BindingFlags.NonPublic|BindingFlags.Static)
                                .Invoke(null,new object[]{rec,EnemyDrops.EntryFor(rec),stage,false,false,false,false});
                            var current=new HashSet<ItemType>(raw.Select(d=>d.Type));
                            foreach(var g in rows.Where(d=>d.Group>0).GroupBy(d=>d.Group))
                            {
                                var retained=g.Where(d=>!current.Contains(d.Type)).ToArray();
                                if(retained.Length==0 || !g.Any(d=>current.Contains(d.Type)))continue;
                                float all=g.Sum(ProgressionLoot.Weight),historic=retained.Sum(ProgressionLoot.Weight);
                                Check("历史共享三成 "+rec+"/"+stage+"/"+g.Key,Math.Abs(historic/all-.3f)<.0001f);
                                Check("组概率与实抽一致 "+rec+"/"+stage+"/"+g.Key,
                                    Math.Abs(g.Sum(d=>EnemyDrops.MarginalChance(rows,d))-g.First().Chance)<.0001f);
                            }
                        }
                    }
                    bool changed;
                    do
                    {
                        changed=false;
                        foreach(var r in Recipes.All)
                            if(!r.IsBought && Ready(r,stock) && r.Ingredients.All(i=>stock.Contains(i.Type)))changed|=stock.Add(r.Result);
                    }while(changed);
                    var families=new List<ItemType>();
                    if(stage>=3)families.AddRange(new[]{ItemType.CobaltBar,ItemType.PalladiumBar,ItemType.MythrilAnvil});
                    if(stage>=4)families.AddRange(new[]{ItemType.AdamantiteForge,ItemType.ChlorophyteBar,ItemType.ChlorophyteSaber,ItemType.ChlorophyteClaymore,
                        ItemType.ChlorophytePartisan,ItemType.ChlorophyteShotbow,ItemType.ChlorophyteMask,ItemType.ChlorophyteHelmet,ItemType.ChlorophyteHeadgear,
                        ItemType.ChlorophytePlateMail,ItemType.ChlorophyteGreaves});
                    if(stage>=5)families.AddRange(new[]{ItemType.ShroomiteBar,ItemType.SpectreBar});
                    if(stage>=5)families.AddRange(new[]{ItemType.LunarCraftingStation,ItemType.LunarBar,ItemType.VortexBeater,ItemType.Phantasm,
                        ItemType.VortexHelmet,ItemType.VortexBreastplate,ItemType.VortexLeggings,ItemType.SolarFlareHelmet,ItemType.NebulaHelmet,ItemType.StardustHelmet});
                    foreach(var t in families)
                    {
                        Check("同期可达 "+t+"/"+stage,stock.Contains(t),"物品阶段="+ItemDb.Get(t).Stage);
                        if(Recipes.All.Any(r=>r.Result==t && !r.IsBought))Check("同期实际配方链 "+t+"/"+stage,
                            Recipes.All.Any(r=>r.Result==t && !r.IsBought && Ready(r,stock) && r.Ingredients.All(i=>stock.Contains(i.Type))));
                    }
                    var output=new List<object>();
                    foreach(var d in ItemDb.All.Where(d=>ProgressionBalance.BalancedWeapon(d) && d.Stage==stage))
                    {
                        var prefixed=d;
                        foreach(var p in Prefixes.DevRollable(d))
                        {var candidate=Prefixes.Apply(d,(byte)p.Id);if(ProgressionBalance.WeaponDps(candidate,Areas.Defense(stage))>ProgressionBalance.WeaponDps(prefixed,Areas.Defense(stage)))prefixed=candidate;}
                        var routine=Forging.CanForge(d)?Forging.Apply(prefixed,(byte)(2*(stage+1))):prefixed;
                        var maximum=Forging.CanForge(d)?Forging.Apply(prefixed,(byte)(5*(stage+1))):prefixed;
                        output.Add(new {Type=d.Type.ToString(),d.Name,d.Damage,d.Stage,Bare=ProgressionBalance.WeaponDps(d,Areas.Defense(stage)),
                            Reforged=ProgressionBalance.WeaponDps(prefixed,Areas.Defense(stage)),Routine=ProgressionBalance.WeaponDps(routine,Areas.Defense(stage)),
                            MaxForge=ProgressionBalance.WeaponDps(maximum,Areas.Defense(stage)),NormalBanner=Banners.DealtFactor(DifficultyLevel.Normal),ExpertBanner=Banners.DealtFactor(DifficultyLevel.Expert)});
                    }
                    var bosses=BossStats.DevAll.Where(p=>p.Value.Stage==stage).Select(p=>new {Record=p.Key,Life=BossStats.LifeOf(p.Value,"World",DifficultyLevel.Normal),
                        WholeLife=BossStats.LifeOf(p.Value,"World",DifficultyLevel.Normal)*BossBalance.Of(p.Value.Record,stage).Life*BossPhases.WholeFactor(p.Value.Record,EnemyDrops.SceneOf(p.Key))}).ToArray();
                    report.Add(new {Stage=stage,Families=families.Select(t=>new {Id=t.ToString(),ItemDb.Get(t).Stage,Reachable=stock.Contains(t)}).ToArray(),Weapons=output,Bosses=bosses});
                    yield return null;
                }
                Check("骷髅时间上限",TerraProjectiles.Instance.DevSkullLimit(false));
                Check("骷髅距离上限",TerraProjectiles.Instance.DevSkullLimit(true));
                Check("骷髅预算包含三次命中",ProgressionBalance.OutputOf(ItemDb.Get(ItemType.BookofSkulls)).Repeat==3);
                // Exercise the real UI crafting path at the actual stage, with no
                // within-stage boss-clear flags. Reachable sources were checked above.
                var inv=mod.Inventory;
                TerraMod.DevAllInfinite=false;mod.ApplyCreative(false);inv.Infinite=false;
                var region=TerrainSystem.Current.Region;
                var at=(Vector2)HeroController.instance.transform.position-(Vector2)region.Root.position+Vector2.right*2;
                Stations.Clear();
                foreach(StationKind kind in Enum.GetValues(typeof(StationKind)))
                    if(kind!=StationKind.None && !Relics.Looks.ContainsKey(kind))Stations.PlaceFixed(kind,region.SceneName,at);
                yield return null;
                InventoryGui.Open=true;inv.Override=-1;
                foreach(var t in new[]{ItemType.ChlorophyteBar,ItemType.ChlorophyteSaber,ItemType.ChlorophytePlateMail,ItemType.ShroomiteBar,
                    ItemType.LunarBar,ItemType.VortexBeater,ItemType.Phantasm,ItemType.VortexHelmet,ItemType.VortexBreastplate,ItemType.VortexLeggings})
                {
                    var r=Recipes.For(t);
                    foreach(int atStage in new[]{ItemDb.Get(t).Stage,0})
                    {
                        Shop.DevStage=atStage;
                        Array.Clear(inv.Slots,0,inv.Slots.Length);inv.Mouse=default;
                        foreach(var ing in r.Ingredients)inv.Add(ing.Type,ing.Count);
                        bool made=InventoryGui.DevCraft(t);
                        Check("有材料实际制作扣料 "+t+"/阶段"+atStage,made && inv.Mouse.Type==t && inv.Mouse.Count==r.Count
                            && r.Ingredients.All(i=>Recipes.Count(inv,i.Type)==0));
                    }
                }
                Array.Clear(inv.Slots,0,inv.Slots.Length);inv.Mouse=default;
                Check("无材料不能越阶合成",!InventoryGui.DevCraft(ItemType.VortexBeater));
                var stationRecipe=Recipes.For(ItemType.VortexBeater);
                foreach(var ing in stationRecipe.Ingredients)inv.Add(ing.Type,ing.Count);
                Stations.Clear();yield return null;
                Check("有材料仍需原工作站",!InventoryGui.DevCraft(ItemType.VortexBeater)
                    && stationRecipe.Ingredients.All(i=>Recipes.Count(inv,i.Type)==i.Count));
                InventoryGui.Open=false;
            }
            finally {Shop.DevStage=previous;GameDifficulty.Current=difficulty;TerraProjectiles.Instance.DevClear();}
            File.WriteAllText(Path.Combine(DevCopy.SaveDir,"combat-upgrade.json"),JsonConvert.SerializeObject(new {Checks=checks,Failures=failed,Stages=report},Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] 成长平衡 汇总 {checks}项 失败{failed} {(failed==0 ? "通过" : "不通过")}");
        }
    }
}

namespace TerraKnight.Items
{
    internal sealed partial class TerraProjectiles
    {
        internal bool DevSkullLimit(bool distance)
        {
            var p=Spawn(ProjKind.Skull,new Vector2(0,100),Vector2.right*10,1,0,0);
            if(distance)p.Travel=639;else p.TimeLeft=1;
            Step(p,true);
            return p.Dead && (distance ? p.Travel>=640 : p.TimeLeft<=0);
        }
        internal bool DevSkullLeash(HealthManager target,bool outside)
        {
            if(!Chaseable(target))return false;
            var at=TerraParticles.ToWorld(EnemyCenterPx(target)+Vector2.right*(outside ? 481 : 479));
            var p=Spawn(ProjKind.Skull,at,Vector2.right*10,1,0,0);
            p.Target=target;
            StepSkull(p);
            bool pass=outside ? p.Target==null : p.Target==target;
            Kill(p);
            return pass;
        }
    }
}
