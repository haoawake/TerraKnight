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
        IEnumerator AccessoryDropsTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated save required");
            int stageWas=Shop.DevStage;
            var difficultyWas=GameDifficulty.Current;
            var rng=UnityEngine.Random.state;
            var sinkWas=EnemyDrops.DevSink;
            int checks=0,failures=0;
            var samples=new List<object>();
            void Check(string label,bool pass,string detail="")
            {
                checks++;
                if (!pass) {failures++;Plugin.Log.LogError($"[AutoTest] 饰品掉率 {label} {detail} 不通过");}
            }
            try
            {
                UnityEngine.Random.InitState(10162027);
                var records=EnemyDrops.DevRecords.ToArray();
                var legacyGear=new HashSet<ItemType>(records.SelectMany(EnemyDrops.LegacyOf).Select(d=>d.Type)
                    .Where(t=> {var d=ItemDb.Get(t); return d.Stage>=0 && d.Stage<=5 && (ProgressionLoot.CombatWeapon(d) || d.IsArmor || d.IsAccessory);}));
                foreach(var type in legacyGear)
                {
                    int native=ItemDb.Get(type).Stage;
                    bool found=records.Any(rec=>EnemyDrops.At(rec,native,false,true,true).Any(d=>d.Type==type && d.Chance>0));
                    Check($"装备来源覆盖 {type}/{native}",found);
                }
                foreach (DifficultyLevel difficulty in Enum.GetValues(typeof(DifficultyLevel)))
                {
                    GameDifficulty.Current=difficulty;
                    for (int s=0;s<6;s++)
                    {
                        Shop.DevStage=s;
                        foreach (var rec in EnemyDrops.DevRecords)
                        {
                            var entry=EnemyDrops.EntryFor(rec);
                            if (!ProgressionLoot.Eligible(entry)) continue;
                            foreach (bool first in new[] {false,true})
                            {
                                var rows=EnemyDrops.At(rec,s,first,true,true);
                                foreach(var d in rows)
                                {
                                    var def=ItemDb.Get(d.Type);
                                    bool single=def.MaxStack==1 || def.LifeCrystal || def.ManaCrystal || def.Boost!=PermanentBoost.None;
                                    Check($"掉落数量 {rec}/{s}/{difficulty}/{first}/{d.Type}",!single
                                        || d.Min==1 && d.Max==1 && d.MeanCount==0 && d.Group!=200 && d.Group!=201 && d.Group!=202);
                                }
                                var acc=rows.Where(d=>ItemDb.Get(d.Type).IsAccessory).ToArray();
                                if (!ProgressionBalance.FormalBoss(rec))
                                    Check($"原生阶段下限 {rec}/{s}/{difficulty}/{first}",
                                        rows.All(d=> !(ProgressionLoot.CombatWeapon(ItemDb.Get(d.Type)) || ItemDb.Get(d.Type).IsArmor || ItemDb.Get(d.Type).IsAccessory)
                                            || ItemDb.Get(d.Type).Stage>=ProgressionLoot.NativeTier(rec)));
                                if (acc.Length==0) continue;
                                float expected=ProgressionBalance.FormalBoss(rec) ? Mathf.Min(1f,(first ? 1f : .2f)*EnemyDrops.ChanceScale) : Mathf.Min(1f,ProgressionLoot.AccessoryPoolChance(ProgressionLoot.Elite(rec,entry),acc.Length)*EnemyDrops.ChanceScale);
                                Check($"全表 {rec}/{s}/{difficulty}/{first}",
                                    acc.All(d=>d.Group==103 && d.Min==1 && d.Max==1)
                                    && acc.Select(d=>d.Type).Distinct().Count()==acc.Length
                                    && Math.Abs(acc.Sum(d=>EnemyDrops.MarginalChance(rows,d))-expected)<.00001f);
                            }
                        }
                    }
                    foreach (var rec in new[] {"Crawler","ZombieRunner","ZombieGuard","BigBee"})
                    {
                        Shop.DevStage=rec=="BigBee" ? 4 : 0;
                        const int trials=20000;
                        int total=0,perKill=0,max=0,materials=0;
                        int maxSingle=0;
                        var singles=new Dictionary<ItemType,int>();
                        EnemyDrops.DevSink=(t,n)=> {
                            var def=ItemDb.Get(t);
                            if(def.MaxStack<=1 || def.LifeCrystal || def.ManaCrystal || def.Boost!=PermanentBoost.None)
                            {singles[t]=(singles.TryGetValue(t,out int old) ? old : 0)+n;maxSingle=Math.Max(maxSingle,singles[t]);}
                            if(def.IsAccessory) {total+=n;perKill+=n;} else if(ItemDb.IsOre(t) || t==ItemType.Wood || t==ItemType.BeeWax) materials+=n;
                        };
                        for (int n=0;n<trials;n++)
                        {
                            perKill=0;
                            singles.Clear();
                            EnemyDrops.Roll(rec,Vector2.zero,true,false);
                            max=Math.Max(max,perKill);
                            if (n%5000==0) yield return null;
                        }
                        bool elite=ProgressionLoot.Elite(rec,EnemyDrops.EntryFor(rec));
                        var pool=EnemyDrops.Of(rec).Where(d=>ItemDb.Get(d.Type).IsAccessory).ToArray();
                        float expected=Mathf.Min(1f,ProgressionLoot.AccessoryPoolChance(elite,pool.Length)*EnemyDrops.ChanceScale);
                        float actual=total/(float)trials;
                        float tolerance=Math.Max(.002f,6*Mathf.Sqrt(expected*(1-expected)/trials));
                        bool pass=max<=1 && maxSingle<=1 && Math.Abs(actual-expected)<tolerance && materials>0;
                        Check($"实际掉落 {rec}/{difficulty}",pass,$"实际{actual:P3} 预期{expected:P3} 单次最多{max}");
                        samples.Add(new {Record=rec,Difficulty=difficulty.ToString(),Trials=trials,Expected=expected,Actual=actual,MaxPerKill=max,Materials=materials,Pass=pass});
                        Plugin.Log.LogInfo($"[AutoTest] 饰品实际掉率 {rec}/{difficulty} {total}/{trials}={actual:P3} 单次最多{max} {(pass ? "通过" : "不通过")}");
                    }
                }
                GameDifficulty.Current=DifficultyLevel.Normal;
                foreach(var rec in new[] {"FalseKnight","Oblobble"})
                {
                    Shop.DevStage=rec=="FalseKnight" ? 0 : 4;
                    int hooks=0,max=0;
                    var singles=new Dictionary<ItemType,int>();
                    EnemyDrops.DevSink=(t,n)=> {
                        var def=ItemDb.Get(t);
                        if(def.MaxStack<=1 || def.LifeCrystal || def.ManaCrystal || def.Boost!=PermanentBoost.None)
                        {singles[t]=(singles.TryGetValue(t,out int old) ? old : 0)+n;max=Math.Max(max,singles[t]);}
                        if(t==ItemType.SlimeHook || t==ItemType.DualHook || t==ItemType.QueenSlimeHook) hooks+=n;
                    };
                    for(int n=0;n<5000;n++) {singles.Clear();EnemyDrops.Roll(rec,Vector2.zero,true,false);if(n%1000==0) yield return null;}
                    Check($"钩爪实际掉落 {rec}",hooks>0 && max==1,$"5000次 抓钩总数{hooks} 单条最多{max}");
                    Plugin.Log.LogInfo($"[AutoTest] 钩爪实际掉落 {rec} 5000次 总数{hooks} 单条最多{max} {(hooks>0 && max==1 ? "通过" : "不通过")}");
                }
                File.WriteAllText(Path.Combine(DevCopy.SaveDir,"accessory-drop-audit.json"),JsonConvert.SerializeObject(new {Checks=checks,Failures=failures,Samples=samples},Formatting.Indented));
                Plugin.Log.LogInfo($"[AutoTest] 饰品掉率汇总 {checks-failures}/{checks} {(failures==0 ? "通过" : "不通过")}");
            }
            finally {Shop.DevStage=stageWas;GameDifficulty.Current=difficultyWas;UnityEngine.Random.state=rng;EnemyDrops.DevSink=sinkWas;}
        }
    }
}
