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
        IEnumerator LootBalanceTest(bool combatSample=true)
        {
            if(!DevCopy.Verified) {Plugin.Log.LogError("[AutoTest] 掉落平衡需要隔离存档，不通过");yield break;}
            int all=0,ok=0;
            var failures=new List<string>();
            void Check(string label,bool pass,string detail="")
            {
                all++;if(pass) ok++; else failures.Add(label+" "+detail);
                Plugin.Log.LogInfo($"[AutoTest] 掉落平衡 {label}：{detail} {(pass ? "通过" : "不通过")}");
            }
            var savedDifficulty=GameDifficulty.Current;
            int savedStage=Shop.DevStage;
            bool savedInfinite=mod.Inventory.Infinite;
            var rng=UnityEngine.Random.state;
            var oldSink=EnemyDrops.DevSink;
            var exports=new Dictionary<string,object>();
            try
            {
                mod.Inventory.Infinite=false;
                GameDifficulty.Current=DifficultyLevel.Normal;
                UnityEngine.Random.InitState(10162026);
                string[] records=EnemyDrops.DevRecords.ToArray();
                exports["sourceLevels"]=records.ToDictionary(r=>r,r=>Areas.LevelOf(EnemyDrops.SceneOf(r)));
                for(int s=0;s<6;s++)
                {
                    Shop.DevStage=s;
                    var drops=records.ToDictionary(r=>r,r=>EnemyDrops.At(r,s,false,s>=4,s>=5));
                    exports["stage"+s]=drops;
                    for(int cls=0;cls<4;cls++)
                    {
                        var core=ProgressionBalance.Core[s][cls];
                        Check($"阶段{s}职业{cls}四把可用武器",core.Length==4 && core.All(t=>ItemDb.Get(t).Stage<=s && ProgressionBalance.ClassIndex(ItemDb.Get(t).Class)==cls));
                        foreach(var t in core.Take(2))
                        {
                            var r=Recipes.For(t);
                            Check($"同期可取得 {s}/{t}",MaterialsReachable(t,s,drops,new HashSet<ItemType>(),true),r==null ? "沿用掉落/商店来源" : string.Join(",",r.Ingredients.Select(i=>i.Type+"×"+i.Count)));
                            if(r!=null)
                                Check($"原配方材料可达 {s}/{t}",r.Ingredients.All(i=>MaterialsReachable(i.Type,s,drops,new HashSet<ItemType>(),true)));
                        }
                        foreach(var t in ProgressionBalance.AccessoryPaths[s][cls])
                        {
                            var r=Recipes.For(t);
                            int sources=drops.Count(pair=>!ProgressionBalance.BossRewards.ContainsKey(pair.Key) && Areas.LevelOf(EnemyDrops.SceneOf(pair.Key))<=s && pair.Value.Any(d=>d.Type==t && d.Group==103 && d.Chance>=.0199f && d.Chance<.0201f));
                            Check($"关键饰品多来源 {s}/{t}",sources>=3,"普通种类="+sources+"；共享2%饰品池");
                            Check($"关键饰品同期可取得 {s}/{t}",MaterialsReachable(t,s,drops,new HashSet<ItemType>(),true));
                        }
                    }
                    foreach(var p in ProgressionBalance.ArmorPaths.Where(p=>p.Stage==s))
                    {
                        int def=p.Items.Sum(t=>ItemDb.Get(t).Defense);
                        Check($"整套防御 {s}/{p.Class}",def==p.Items.Sum(t=>VanillaArmorBonuses.Values[int.Parse(ItemDb.Get(t).Texture.Substring(5))].Defense),"实际="+def);
                        Check($"整套同期可取得 {s}/{p.Class}",p.Items.All(t=>MaterialsReachable(t,s,drops,new HashSet<ItemType>(),true)));
                    }
                    // A future boss's own reward is indexed in the export; ordinary rows must never leak.
                    bool normalLeak=drops.Any(p=>!ProgressionBalance.BossRewards.ContainsKey(p.Key) && p.Value.Any(d=>!d.Master && ItemDb.Get(d.Type).Stage>s));
                    Check("阶段"+s+"普通掉落无越阶",!normalLeak);
                    yield return null;
                }
                Check("跳序能力按种类计数",ProgressionBalance.AbilityStage(flag=>flag=="hasDoubleJump" || flag=="hasShadowDash")==2);
                Check("叶绿同期开放",ProgressionBalance.Unlocked(ItemType.ChlorophyteOre,4,false,false) && ProgressionBalance.Unlocked(ItemType.ChlorophyteOre,4,true,false));
                Check("夜明同期开放",ProgressionBalance.Unlocked(ItemType.LunarOre,5,true,false) && ProgressionBalance.Unlocked(ItemType.LunarOre,5,true,true));
                Check("终极成品同期开放",ProgressionBalance.Unlocked(ItemType.Meowmere,5,true,false) && ProgressionBalance.Unlocked(ItemType.Meowmere,5,true,true));
                var jungle0=EnemyDrops.At("MossmanRunner",0,false,false,false);
                var jungle4=EnemyDrops.At("MossmanRunner",4,false,true,false);
                Check("丛林早期孢子藤蔓",jungle0.Any(d=>d.Type==ItemType.JungleSpores) && jungle0.Any(d=>d.Type==ItemType.Vine) && !jungle0.Any(d=>d.Type==ItemType.ChlorophyteOre));
                Check("丛林后期保留老料并加叶绿",jungle4.Any(d=>d.Type==ItemType.JungleSpores) && jungle4.Any(d=>d.Type==ItemType.Vine) && jungle4.Any(d=>d.Type==ItemType.ChlorophyteOre));
                Check("蜂蜡地域限制",!EnemyDrops.At("AbyssCrawler",4,false,true,false).Any(d=>d.Type==ItemType.BeeWax));
                Check("蜂巢骑士阶段4",BossStats.Of("HiveKnight",4).Stage==4);
                Check("蜂套原版防御13",new[] {ItemType.BeeHeadgear,ItemType.BeeBreastplate,ItemType.BeeGreaves}.Sum(t=>ItemDb.Get(t).Defense)==13);
                Check("蜂套原版23%召唤增伤",Mathf.Abs(new[] {ItemType.BeeHeadgear,ItemType.BeeBreastplate,ItemType.BeeGreaves}.Sum(t=>ItemDb.Get(t).Bonus.MinionDamage)+Equipment.SetBonus(ArmorSet.Bee).MinionDamage-.23f)<.0001f);
                Check("蜂套原版额外2仆从",new[] {ItemType.BeeHeadgear,ItemType.BeeBreastplate,ItemType.BeeGreaves}.Sum(t=>ItemDb.Get(t).Bonus.MaxMinions)+Equipment.SetBonus(ArmorSet.Bee).MaxMinions==2);
                Check("同源升级饰品不叠加",ProgressionBalance.Superseded(ItemType.BandofStarpower,new[] {ItemStack.Of(ItemType.BandofStarpower),ItemStack.Of(ItemType.MagicCuffs)}));
                Check("独立饰品不互相屏蔽",!ProgressionBalance.Superseded(ItemType.PygmyNecklace,new[] {ItemStack.Of(ItemType.PygmyNecklace),ItemStack.Of(ItemType.SummonerEmblem)}));
                Shop.DevStage=0;
                Check("普通模式斗篷原版伤害",Player.TerraHurt.CloakDamage==75);
                var crawler=EnemyDrops.At("Crawler",0,false,false,false);
                float oreMean=crawler.Where(d=>ItemDb.IsOre(d.Type)).Sum(d=>EnemyDrops.MarginalChance(crawler,d)*(d.Min+d.Max)*.5f);
                Check("小爬虫矿量有界",oreMean>0 && oreMean<=2.4f,"平均矿数="+oreMean);
                var legacy=new HashSet<ItemType>(records.SelectMany(r=>EnemyDrops.LegacyOf(r)).Select(d=>d.Type));
                var lost=new List<string>();
                foreach(var t in legacy)
                {
                    var d=ItemDb.Get(t);
                    if(!(ProgressionLoot.CombatWeapon(d) || d.IsArmor || d.IsAccessory)) continue;
                    int s=Mathf.Clamp(d.Stage,0,5);
                    if(!records.Any(r=>EnemyDrops.At(r,s,false,true,true).Any(row=>row.Type==t))
                        && !Town.TownNpcs.DevSourcesAt(s).Any(row=>row.Type==t)) lost.Add(t.ToString());
                }
                Check("原有流派装备未因迁移断供",lost.Count==0,string.Join(",",lost));

                // The actual game picker, not a parallel implementation, for every boss and class.
                foreach(var pair in ProgressionBalance.BossRewards)
                {
                    if(!ProgressionBalance.FormalBoss(pair.Key)) continue;
                    var rows=EnemyDrops.At(pair.Key,pair.Value.Stage,true,true,true).Where(d=>d.Group==100).ToArray();
                    float total=rows.Sum(d=>ProgressionLoot.Weight(d));
                    Check("Boss必有有效主力 "+pair.Key,rows.Length>=4 && Mathf.Abs(total-1)<.0001f && rows.All(d=>d.Chance==1 && (ItemDb.Get(d.Type).IsWeapon || ItemDb.Get(d.Type).IsArmor)));
                    var weapons=rows.Where(d=>ItemDb.Get(d.Type).IsWeapon).ToArray();
                    Check("Boss四职业等权 "+pair.Key,Enumerable.Range(0,4).All(c=>Mathf.Abs(weapons.Where(d=>ProgressionBalance.ClassIndex(ItemDb.Get(d.Type).Class)==c).Sum(d=>d.Weight)-weapons.Sum(d=>d.Weight)/4f)<.0001f));
                }
                foreach(string rec in new[] {"FalseKnight","MantisLord","HiveKnight","FinalBoss"})
                {
                    var reward=ProgressionBalance.BossRewards[rec];
                    var options=EnemyDrops.At(rec,reward.Stage,true,true,true).Where(d=>d.Group==100 && ItemDb.Get(d.Type).IsWeapon).ToArray();
                    var count=new int[4];
                    for(int n=0;n<100000;n++) count[ProgressionBalance.ClassIndex(ItemDb.Get(EnemyDrops.Pick(options).Type).Class)]++;
                    Check("十万次职业随机 "+rec,count.All(n=>Math.Abs(n-25000)<700),string.Join("/",count));
                    yield return null;
                }
                // Exercise actual Roll/Put and amount sampling on an ordinary enemy and both boss modes.
                foreach(var difficulty in new[] {DifficultyLevel.Normal,DifficultyLevel.Hard,DifficultyLevel.Expert,DifficultyLevel.Master})
                {
                    GameDifficulty.Current=difficulty;
                    Shop.DevStage=0;
                    int weapons=0,accessories=0,oreCount=0;
                    EnemyDrops.DevSink=(t,n)=> {var d=ItemDb.Get(t);if(d.IsWeapon && !d.IsAmmo) weapons++;if(d.IsAccessory) accessories++;if(ItemDb.IsOre(t)) oreCount+=n;};
                    for(int n=0;n<100000;n++) EnemyDrops.Roll("Crawler",Vector2.zero,true,false);
                    float rate=weapons/100000f;
                    float expected=.07f*EnemyDrops.ChanceScale;
                    Check("十万次普通武器总率 "+difficulty,Math.Abs(rate-expected)<.002,rate.ToString("P3"));
                    Check("普通饰品总率受限 "+difficulty,Math.Abs(accessories/100000f-.02f*EnemyDrops.ChanceScale)<.002,"件数="+accessories);
                    Check("普通矿物有产出 "+difficulty,oreCount>0,"矿数="+oreCount);
                    yield return null;
                }
                GameDifficulty.Current=DifficultyLevel.Normal;
                foreach(bool first in new[] {true,false})
                {
                    Shop.DevStage=2;
                    int main=0;
                    int mainPicks=EnemyDrops.DevMainPicks;
                    EnemyDrops.DevSink=(t,n)=> {var d=ItemDb.Get(t);if(d.IsWeapon && !d.IsAmmo || d.IsArmor) main++;};
                    for(int n=0;n<1000;n++) EnemyDrops.Roll("MantisLord",Vector2.zero,true,first);
                    Check("实际Boss主力每次一件 first="+first,EnemyDrops.DevMainPicks-mainPicks==1000 && main>=1000,"主力池抽取="+(EnemyDrops.DevMainPicks-mainPicks)+"；含独立原版材料装备的总件数="+main);
                }
                var starter=new[] {ItemType.WoodenSword,ItemType.WoodenBow,ItemType.WandofSparking,ItemType.FinchStaff,ItemType.MusketBall};
                foreach(var t in starter)
                {
                    bool present;
                    Town.TownNpcs.DevStockAt("Merchant",0,t,out present);
                    Check("商人阶段0常驻 "+t,present);
                }
                var price=Shop.PriceOf(ItemDb.Get(ItemType.WandofSparking));
                Check("火花魔棒可负担",price<=3000,"铜="+price);
                EnemyDrops.DevSink=null;
                var combat=new List<object>();
                if(combatSample)yield return LootCombatTest(Check,combat);
                exports["combat"]=combat;
                exports["items"]=ItemDb.All.Where(d=>d!=null).Select(d=>new {Type=d.Type.ToString(),TypeId=(int)d.Type,d.Name,d.Texture,d.Stage,d.Rare,d.BaitPower,d.Damage,d.Defense,d.Mana,d.UseTime,d.UseAnimation,Class=d.Class.ToString(),d.Bonus,d.Tooltip}).ToArray();
                exports["recipes"]=Recipes.All.Where(r=>!r.IsBought).Select(r=>new {Result=r.Result.ToString(),r.Count,r.Station,r.StationStage,Environment=r.Environment.ToString(),Ingredients=r.Ingredients.Select(i=>new {Type=i.Type.ToString(),i.Count})}).ToArray();
                using(var sha=System.Security.Cryptography.SHA256.Create())
                    exports["pluginSha256"]=BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(typeof(TerraMod).Assembly.Location))).Replace("-","").ToLowerInvariant();
            }
            finally
            {
                EnemyDrops.DevSink=oldSink;
                Shop.DevStage=savedStage;
                GameDifficulty.Current=savedDifficulty;
                mod.Inventory.Infinite=savedInfinite;
                UnityEngine.Random.state=rng;
            }
            exports["summary"]=new {Checks=all,Passed=ok,Failures=failures,CombatPlaythroughVerified=false};
            string path=Path.Combine(DevCopy.SaveDir,"loot-balance-runtime.json");
            File.WriteAllText(path,JsonConvert.SerializeObject(exports,Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] 掉落平衡汇总 {ok}/{all}，导出 {path}");
        }
        static bool MaterialsReachable(ItemType t,int stage,Dictionary<string,IReadOnlyList<EnemyDrops.Drop>> drops,HashSet<ItemType> visiting,bool bosses)
        {
            if(ItemDb.Get(t).Source==ItemSource.Basic && ItemDb.Get(t).Stage<=stage) return true;
            if(Town.TownNpcs.DevSourcesAt(stage).Any(row=>row.Type==t))return true;
            if(drops.Any(p=>(!ProgressionBalance.BossRewards.TryGetValue(p.Key,out var b) ? Areas.LevelOf(EnemyDrops.SceneOf(p.Key))<=stage : bosses && b.Stage<=stage) && p.Value.Any(d=>d.Type==t))) return true;
            if(!visiting.Add(t)) return false;
            bool reachable=Recipes.All.Where(r=>r.Result==t && !r.IsBought)
                .Any(r=>r.Ingredients.All(i=>MaterialsReachable(i.Type,stage,drops,visiting,bosses)));
            visiting.Remove(t);
            return reachable;
        }
    }
}
