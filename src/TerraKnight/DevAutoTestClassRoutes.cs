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
        IEnumerator ClassRouteAudit(bool finalOnly=false)
        {
            if(!DevCopy.Verified)throw new InvalidOperationException("Isolated save required");
            var oldClears=VanillaBossProgression.DevClears;int oldStage=Shop.DevStage;
            var oldDifficulty=GameDifficulty.Current;var random=UnityEngine.Random.state;
            var snapshots=new List<object>();
            var stations=new Dictionary<string,HashSet<ItemType>>();
            var specs=(System.Collections.IDictionary)typeof(Stations).GetField("specs",BindingFlags.NonPublic|BindingFlags.Static).GetValue(null);
            foreach(var value in specs.Values)
            {
                var type=value.GetType();var item=(ItemType)type.GetField("Item").GetValue(value);
                foreach(var name in (string[])type.GetField("Counts").GetValue(value))
                {if(!stations.TryGetValue(name,out var set))stations[name]=set=new HashSet<ItemType>();if(item!=ItemType.None)set.Add(item);}
            }
            bool Station(Recipe r,HashSet<ItemType> reach)=>string.IsNullOrEmpty(r.Station)||r.Station=="徒手"||r.Station=="恶魔祭坛 / 猩红祭坛"||stations.TryGetValue(r.Station,out var set)&&set.Any(reach.Contains);
            try
            {
                mod.ApplyCreative(false);mod.Inventory.FreeMaking=false;mod.Inventory.Infinite=false;
                GameDifficulty.Current=DifficultyLevel.Normal;UnityEngine.Random.InitState(10232026);
                var records=EnemyDrops.DevRecords.ToArray();
                for(int stage=finalOnly?5:0;stage<6;stage++)
                {
                    Shop.DevStage=stage;
                    VanillaBossProgression.DevClears=new HashSet<string>(VanillaBossProgression.All.Where(p=>p.Value.Band<stage).Select(p=>p.Key));
                    var checkpoints=new List<(string Label,string[] Add)>{("entry",new string[0])};
                    foreach(var group in VanillaBossProgression.All.Where(p=>p.Value.Band==stage).GroupBy(p=>p.Value.Step).OrderBy(p=>p.Key))
                        checkpoints.Add(("after-"+group.Key,group.Select(p=>p.Key).ToArray()));
                    foreach(var checkpoint in checkpoints)
                    {
                        VanillaBossProgression.DevClears.UnionWith(checkpoint.Add);
                        var sources=new Dictionary<ItemType,List<string>>();
                        void Add(ItemType t,string why)
                        {if(t==ItemType.None||SpellRewards.Is(t)||!ProgressionBalance.Unlocked(t,stage,stage>=4,stage>=5))return;if(!sources.TryGetValue(t,out var list))sources[t]=list=new List<string>();if(!list.Contains(why))list.Add(why);}
                        foreach(var t in new[]{ItemType.CopperShortsword,ItemType.CopperPickaxe,ItemType.WoodenBow,ItemType.KnightNail,ItemType.Torch,ItemType.WoodenArrow,ItemType.GrapplingHook,ItemType.ClimbingClaws,ItemType.CloudinaBottle})Add(t,"starter");
                        foreach(var d in Shop.Basics)Add(d.Type,"basic-shop");
                        foreach(var p in Town.TownNpcs.DevSourcesAt(stage))Add(p.Type,"shop:"+p.Npc);
                        for(int a=0;a<AreaBlocks.All.Length;a++)if(AreaBlocks.AcquisitionStage(a)<=stage)Add(AreaBlocks.BlockItem(a),"mine:"+AreaBlocks.All[a].Name);
                        if(stage>=3)Add(ItemType.CrystalHeart,"ability-pickup");if(stage>=5)Add(ItemType.ShadeCloak,"ability-pickup");
                        var areas=new HashSet<string>();
                        foreach(var rec in records)
                        {
                            if(ProgressionBalance.FormalBoss(rec)&&(!VanillaBossProgression.DevClears.Contains(rec)))continue;
                            if(!ProgressionBalance.FormalBoss(rec)&&ProgressionLoot.NativeTier(rec)>stage)continue;
                            var scenes=BestiaryHabitats.Of(rec).Select(h=>h.Scene).Concat(new[]{EnemyDrops.SceneOf(rec)}).Distinct();
                            foreach(var scene in scenes)
                            {
                                bool legalPillar=VanillaBossProgression.All.TryGetValue(rec,out var profile)&&profile.Step==VanillaBossProgression.Step.Pillars&&VanillaBossProgression.Current>=VanillaBossProgression.Step.Cultist;
                                if(string.IsNullOrEmpty(scene)||BossStats.InGodhome(scene)&&!legalPillar||Areas.LevelOf(scene)>stage)continue;
                                areas.Add(Areas.AreaName(scene));
                                var drops=EnemyDrops.At(rec,stage,false,stage>=4,stage>=5,scene);
                                foreach(var row in drops)if(!row.Master&&row.Chance>0&&!CraftOnlyLoot.Blocked(row.Type))
                                    Add(row.Type,"drop:"+rec+"@"+scene+" p="+EnemyDrops.MarginalChance(drops,row).ToString("G6",System.Globalization.CultureInfo.InvariantCulture));
                            }
                        }
                        var caught=new HashSet<ItemType>();
                        foreach(var area in areas)
                        {
                            foreach(var t in Fishing.DevSimulate(area,false,100,1664,1000).Keys)caught.Add(t);
                            if(stage>=1)foreach(var t in Fishing.DevSimulate(area,true,100,1664,1000).Keys)caught.Add(t);
                            for(int n=0;n<60;n++)foreach(var stack in RegionalChests.Contents(area,n%6,stage))if(!stack.IsEmpty)Add(stack.Type,"regional-chest:"+area);
                        }
                        foreach(var t in caught)Add(t,"fishing-sampled");
                        foreach(var crate in caught.Where(Fishing.IsCrate))for(int n=0;n<300;n++)foreach(var stack in Fishing.DevContents(crate))Add(stack.Type,"crate-sampled:"+crate);
                        if(stage>=2)foreach(var t in Town.Angler.DevRewardSources())Add(t,"angler-quest");
                        var reach=new HashSet<ItemType>(sources.Keys);var crafted=new Dictionary<ItemType,string>();bool more;
                        bool Environment(Recipe r)=>r.Environment==CraftEnvironment.None||r.Environment==CraftEnvironment.Water||r.Environment==CraftEnvironment.Lava&&stage>=1
                            ||r.Environment==CraftEnvironment.Graveyard&&(stage>=2||reach.Contains(ItemType.Tombstone))
                            ||r.Environment==CraftEnvironment.Honey&&(stage>=4||reach.Contains(ItemType.HoneyDispenser));
                        do
                        {
                            more=false;
                            foreach(var r in Recipes.All)
                            {
                                // StationStage is documentation only. The live crafting UI checks ingredients, physical station and environment.
                                if(r.IsBought||!Station(r,reach)||!Environment(r)||!r.Ingredients.All(i=>reach.Contains(i.Type)))continue;
                                if(reach.Add(r.Result)){crafted[r.Result]=r.Station+":"+string.Join("+",r.Ingredients.Select(i=>i.Type+"*"+i.Count));more=true;}
                            }
                        }while(more);
                        snapshots.Add(new {Stage=stage,checkpoint.Label,Milestone=VanillaBossProgression.Current.ToString(),Clears=VanillaBossProgression.DevClears.OrderBy(x=>x).ToArray(),
                            Sources=sources.ToDictionary(p=>p.Key.ToString(),p=>p.Value),Reachable=reach.Select(t=>t.ToString()).ToArray(),Crafted=crafted.ToDictionary(p=>p.Key.ToString(),p=>p.Value)});
                        Plugin.Log.LogInfo($"[AutoTest] CLASS_ROUTE stage={stage} checkpoint={checkpoint.Label} milestone={VanillaBossProgression.Current} reachable={reach.Count}");
                        yield return null;
                    }
                }
                var export=new {Mode="normal; real milestone gates; main-world boss sources; existential source closure; sampled secondary loot",Snapshots=snapshots,
                    Items=ItemDb.All.ToDictionary(d=>d.Type.ToString(),d=>new {d.Name,d.Stage,Source=d.Source.ToString(),Class=d.Class.ToString(),Kit=d.Kit.ToString(),d.IsWeapon,d.IsArmor,d.IsAccessory,d.IsAmmo,d.GameTool,d.Placeholder,d.Damage,d.Defense,d.UseTime,d.UseAnimation,d.Mana,d.NoUseMana,Set=d.Set.ToString(),d.Bonus,d.SetBonus,
                        ArmorSlot=d.Armor.ToString(),UseAmmo=d.UseAmmo.ToString(),Ammo=d.Ammo.ToString(),d.IsWhip,d.HealMana,d.PickPower,d.AxePower,d.HammerPower,d.MaxStack,Price=Shop.PriceOf(d),Balanced=ProgressionBalance.BalancedWeapon(d),Minion=d.Minion.ToString(),Output=ProgressionBalance.BalancedWeapon(d)?ProgressionBalance.OutputOf(d).Rate:0,
                        ModelDps=Enumerable.Range(0,6).Select(s=>ProgressionBalance.BalancedWeapon(d)?ProgressionBalance.WeaponDps(d,Areas.Defense(s)):0).ToArray()}),
                    Recipes=Recipes.All.Where(r=>!r.IsBought).Select(r=>new {Result=r.Result.ToString(),r.Count,r.Station,r.StationStage,Environment=r.Environment.ToString(),Ingredients=r.Ingredients.Select(i=>new {Type=i.Type.ToString(),i.Count})}),
                    Stations=stations.ToDictionary(p=>p.Key,p=>p.Value.Select(t=>t.ToString()).ToArray()),
                    Core=ProgressionBalance.Core.Select(p=>p.Select(c=>c.Select(t=>t.ToString()).ToArray()).ToArray()).ToArray(),
                    Accessories=ProgressionBalance.AccessoryPaths.Select(p=>p.Select(c=>c.Select(t=>t.ToString()).ToArray()).ToArray()).ToArray(),
                    SpellSources=SpellRewards.All.Select(t=>new {Type=t.ToString(),Spell=SpellRewards.SpellOf(t),Name=SpellRewards.Name(SpellRewards.SpellOf(t)),Stage=ItemDb.Get(t).Stage}),
                    Bosses=VanillaBossProgression.All.ToDictionary(p=>p.Key,p=>new {p.Value.Band,Step=p.Value.Step.ToString(),p.Value.Role,p.Value.Upgrade}),
                    ArmorPaths=ProgressionBalance.ArmorPaths.Select(p=>new {p.Stage,Class=p.Class.ToString(),Items=p.Items.Select(t=>t.ToString()).ToArray()})};
                File.WriteAllText(Path.Combine(outDir,finalOnly?"class-routes-final.json":"class-routes.json"),JsonConvert.SerializeObject(export,Formatting.Indented));
                Plugin.Log.LogInfo("[AutoTest] CLASS_ROUTE export completed");
            }
            finally{VanillaBossProgression.DevClears=oldClears;Shop.DevStage=oldStage;GameDifficulty.Current=oldDifficulty;UnityEngine.Random.state=random;}
        }
    }
}
