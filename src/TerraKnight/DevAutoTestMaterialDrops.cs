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
        IEnumerator MaterialDropsTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated save required");
            int stageWas=Shop.DevStage,checks=0,failed=0;
            var difficultyWas=GameDifficulty.Current;
            var rng=UnityEngine.Random.state;
            var sinkWas=EnemyDrops.DevSink;
            var samples=new List<object>();
            void Check(string label,bool pass,string detail="")
            {
                checks++; if(!pass) {failed++;Plugin.Log.LogError($"[AutoTest] 材料掉率 {label} {detail} 不通过");}
            }
            var cases=new[] {
                ("Jellyfish",ItemType.SoulofLight),("CrystalCrawler",ItemType.SoulofLight),
                ("AbyssCrawler",ItemType.SoulofNight),("SlashSpider",ItemType.SoulofNight),
                ("BlowFly",ItemType.SoulofFlight),("GiantHopper",ItemType.SoulofFlight)};
            try
            {
                UnityEngine.Random.InitState(10162028);
                foreach (DifficultyLevel difficulty in Enum.GetValues(typeof(DifficultyLevel)))
                {
                    GameDifficulty.Current=difficulty;
                    foreach (var test in cases)
                    {
                        for(int s=0;s<3;s++)
                            Check($"魂不提前掉落 {test.Item1}/{s}/{difficulty}",!EnemyDrops.At(test.Item1,s,false,true,true).Any(d=>d.Type==test.Item2));
                        Shop.DevStage=3;
                        var rows=EnemyDrops.At(test.Item1,3,false,false,false);
                        var soul=rows.Single(d=>d.Type==test.Item2);
                        var laterSoul=EnemyDrops.At(test.Item1,4,false,true,true).Single(d=>d.Type==test.Item2);
                        Check($"原阶段魂掉率保持 {test.Item1}/{difficulty}",soul.Num==laterSoul.Num && soul.Den==laterSoul.Den
                            && soul.Min==laterSoul.Min && soul.Max==laterSoul.Max && soul.Group==laterSoul.Group);
                        bool elite=ProgressionLoot.Elite(test.Item1,EnemyDrops.EntryFor(test.Item1));
                        Check($"独立魂规则 {test.Item1}/{difficulty}",soul.Group==0 && soul.Min==(elite ? 2 : 1) && soul.Max==(elite ? 4 : 3)
                            && Math.Abs(soul.Chance-(elite ? 1 : .6f))<.00001f);
                        const int trials=20000;
                        int hits=0,count=0,perKill=0,min=int.MaxValue,max=0;
                        EnemyDrops.DevSink=(t,n)=>{if(t==test.Item2) {count+=n;perKill+=n;}};
                        for(int n=0;n<trials;n++)
                        {
                            perKill=0;EnemyDrops.Roll(test.Item1,Vector2.zero,true,false);
                            if(perKill>0) {hits++;min=Math.Min(min,perKill);max=Math.Max(max,perKill);}
                            if(n%5000==0) yield return null;
                        }
                        float expected=EnemyDrops.EffectiveChance(soul),rate=hits/(float)trials;
                        float expectedMean=expected*(soul.Min+EnemyDrops.EffectiveMax(soul))*.5f;
                        bool pass=Math.Abs(rate-expected)<Math.Max(.003,6*Math.Sqrt(expected*(1-expected)/trials))
                            && Math.Abs(count/(float)trials-expectedMean)<.05f && min>=soul.Min && max<=EnemyDrops.EffectiveMax(soul);
                        Check($"实际魂产量 {test.Item1}/{difficulty}",pass,$"{hits}/{trials} 魂{count}");
                        samples.Add(new {Record=test.Item1,Soul=test.Item2.ToString(),Difficulty=difficulty.ToString(),Trials=trials,Hits=hits,Count=count,Rate=rate,Mean=count/(float)trials,ExpectedRate=expected,ExpectedMean=expectedMean,Min=min,Max=max,Pass=pass});
                        Plugin.Log.LogInfo($"[AutoTest] 魂实际掉落 {test.Item1}/{difficulty} {rate:P2} 平均{count/(float)trials:F3}个/怪 {(pass ? "通过" : "不通过")}");
                    }
                }
                GameDifficulty.Current=DifficultyLevel.Normal;
                Shop.DevStage=0;
                foreach(var scene in new[]{"Fungus1_01","Fungus2_01"})
                {
                    var early=EnemyDrops.At("Crawler",0,false,false,false,scene);
                    foreach(var type in new[]{ItemType.JungleSpores,ItemType.Vine,ItemType.Stinger})
                        Check($"开局丛林材料 {scene}/{type}",early.Any(d=>d.Type==type && d.Chance>0));
                }
                foreach(var type in new[]{ItemType.AdamantiteOre,ItemType.TitaniumOre,ItemType.AdamantiteBar,ItemType.TitaniumBar,
                    ItemType.AdamantiteForge,ItemType.TitaniumForge,ItemType.CrystalShard,ItemType.PixieDust,ItemType.UnicornHorn,
                    ItemType.CursedFlame,ItemType.Ichor})
                    Check($"困难模式初期材料 {type}",!ProgressionBalance.Unlocked(type,2,false,false)
                        && ProgressionBalance.Unlocked(type,3,false,false));
                foreach(var type in new[]{ItemType.ChlorophyteOre,ItemType.HallowedBar,ItemType.SoulofMight,ItemType.SoulofSight,ItemType.SoulofFright})
                    Check($"机械阶段不提前 {type}",!ProgressionBalance.Unlocked(type,3,false,false));
                var ordinary=EnemyDrops.At("Crawler",0,false,false,false).Where(d=>d.Group==201).ToArray();
                Check("普通材料池产出",ordinary.Length>0 && ordinary.All(d=>d.Chance==1 && d.Min>0 && d.Max>=d.Min && d.Weight>0)
                    && Math.Abs(EnemyDrops.GroupWeightTotal(ordinary)-1f)<.00001f);
                foreach(var boss in ProgressionBalance.BossRewards.Where(p=>ProgressionBalance.FormalBoss(p.Key)))
                {
                    var first=EnemyDrops.At(boss.Key,boss.Value.Stage,true,true,true);
                    var repeat=EnemyDrops.At(boss.Key,boss.Value.Stage,false,true,true);
                    foreach(var row in first.Where(d=>d.Group==200 || d.Group==201))
                    {
                        var match=repeat.FirstOrDefault(d=>d.Group==row.Group && d.Type==row.Type);
                        Check($"重复材料不打折 {boss.Key}/{row.Type}",match.Type==row.Type && Math.Abs(match.MeanCount-row.MeanCount)<.00001f && match.Num==row.Num && match.Den==row.Den);
                    }
                }
                File.WriteAllText(Path.Combine(DevCopy.SaveDir,"material-drop-audit.json"),JsonConvert.SerializeObject(new {Checks=checks,Failures=failed,Samples=samples},Formatting.Indented));
                Plugin.Log.LogInfo($"[AutoTest] 材料掉率汇总 {checks-failed}/{checks} {(failed==0 ? "通过" : "不通过")}");
            }
            finally {Shop.DevStage=stageWas;GameDifficulty.Current=difficultyWas;UnityEngine.Random.state=rng;EnemyDrops.DevSink=sinkWas;}
        }
    }
}
