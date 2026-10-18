using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator GodhomeBalanceTest()
        {
            if(!DevCopy.Verified)throw new InvalidOperationException("Isolated saves required");
            int checks=0,failures=0;
            void Check(string name,bool pass,string detail="")
            {checks++;if(!pass){failures++;Plugin.Log.LogError($"[AutoTest] 神居分级 {name} {detail} 不通过");}}
            var seqField=AccessTools.Field(typeof(BossSequenceController),"currentSequence");
            var dataField=AccessTools.Field(typeof(BossSequenceController),"currentData");
            var seqWas=seqField.GetValue(null);var dataWas=dataField.GetValue(null);
            var difficultyWas=GameDifficulty.Current;
            var rows=new List<object>();var actual=new List<object>();
            var profiles=GodhomeBalance.Hall.Concat(GodhomeBalance.Pantheons).ToArray();
            var sequences=Resources.LoadAll<BossSequence>("GG").Where(s=>GodhomeBalance.DoorOf(s)>0).ToArray();
            Check("五个真实序列",sequences.Length==5 && sequences.Select(GodhomeBalance.DoorOf).Distinct().Count()==5);
            try
            {
                dataField.SetValue(null,null);seqField.SetValue(null,null);
                foreach(var pair in BossStats.DevAll)
                foreach(DifficultyLevel difficulty in Enum.GetValues(typeof(DifficultyLevel)))
                {
                    var r=pair.Value;string rec=r.Record;
                    GameDifficulty.Current=difficulty;
                    int expected=Mathf.Max(1,Mathf.RoundToInt(r.Boss.LifeAt(BossStats.Tier(difficulty))*BossStats.CombatLifeShare(r.Stage)));
                    Check("地图生命不变/"+pair.Key+"/"+difficulty,BossStats.LifeOf(r,"World",difficulty)==expected);
                    var tune=BossBalance.Of(rec,pair.Key.Contains("@") ? int.Parse(pair.Key.Split('@')[1]) : r.Stage);
                    int baseDef=Mathf.RoundToInt(BossStats.DefenseOf(r)*Mathf.Min(1,tune.Defense)*.5f);
                    Check("地图防御不变/"+pair.Key,EnemyStats.BossDefense(r,tune,"World")==baseDef);
                    float whole=BossPhases.WholeFactor(rec,"GG_");
                    int worldWhole=Mathf.CeilToInt(expected*tune.Life*BossPhases.WholeFactor(rec,"World"));
                    int prevLife=0,prevDef=0;float prevDamage=0;
                    foreach(var profile in profiles)
                    {
                        int life=Mathf.CeilToInt(BossStats.GodhomeLifeOf(r,"GG_",difficulty,profile)*tune.Life*whole);
                        int defense=GodhomeBalance.Defense(baseDef,profile);
                        Check("高于地图/"+pair.Key+"/"+difficulty+"/"+profile.Key,life>worldWhole && defense>baseDef && profile.Damage>1);
                        float tier=BossStats.Tier(difficulty);
                        int cultist=(int)Math.Round(32000*(1+tier)*Mathf.Lerp(1,.75f,Mathf.Clamp01(tier))*Mathf.Lerp(1,.85f,Mathf.Clamp01(tier-1)));
                        float totalBudget=Mathf.Max(expected*1.5f,cultist*1.5f)*profile.Life*tune.Life;
                        Check("整场预算不按阶段重复/"+pair.Key+"/"+profile.Key,Math.Abs(life-totalBudget)<3*whole+3,$"actual={life} budget={totalBudget}");
                        if(profile.Key=="pantheon1"){prevLife=0;prevDef=0;prevDamage=0;}
                        if(profile.Key.StartsWith("pantheon"))
                        {
                            Check("逐门生命递增/"+pair.Key+"/"+profile.Key,life>prevLife);
                            Check("逐门防御递增/"+pair.Key+"/"+profile.Key,defense>prevDef);
                            Check("逐门伤害递增/"+pair.Key+"/"+profile.Key,profile.Damage>prevDamage);
                            prevLife=life;prevDef=defense;prevDamage=profile.Damage;
                        }
                        rows.Add(new {Record=pair.Key,Name=Bestiary.NameOf(rec),Stage=r.Stage,Difficulty=difficulty.ToString(),Profile=profile.Key,
                            Life=life,Defense=defense,DamageFactor=Areas.BossDamage*tune.Damage*profile.Damage,WorldLife=worldWhole,WorldDefense=baseDef,
                            PhaseFactor=whole,ChallengeName=profile.Name});
                    }
                }
                // Exercise real engine challenge state, including tier-five's ENDINGD asset.
                foreach(var sequence in sequences)
                {
                    int door=GodhomeBalance.DoorOf(sequence);
                    seqField.SetValue(null,sequence);dataField.SetValue(null,new BossSequenceController.BossSequenceData {bossSequenceName=sequence.name});
                    Check("真实门识别/"+sequence.name,GodhomeBalance.Current("GG_Grimm").Key=="pantheon"+door);
                    Check("门状态不泄漏地图/"+sequence.name,GodhomeBalance.Current("Fungus1_04_boss").Key=="world");
                }
                dataField.SetValue(null,null);seqField.SetValue(null,null);
                // A real native Godhome scene/controller, not a mock selector.
                PlayerData.instance.isInvincible=true;mod.DevNoFallDamage=true;
                var setupWas=BossSceneController.SetupEvent;
                BossSceneController.SetupEvent=self=>{self.BossLevel=0;self.DreamReturnEvent="DREAM RETURN";};
                yield return GoTo("GG_Grimm","door_dreamEnter");
                BossSceneController.SetupEvent=setupWas;
                var controller=BossSceneController.Instance;
                Check("原版神居控制器",controller!=null && controller.gameObject.scene.name=="GG_Grimm");
                HealthManager boss=null;
                // The hero becomes controllable before Grimm's entrance activates him.
                // Wait for the real live component instead of treating that intro as missing data.
                float until=Time.time+15;
                while(boss==null && Time.time<until)
                {
                    boss=UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                        .FirstOrDefault(h=>h.gameObject.scene.name=="GG_Grimm" && EnemyStats.Of(h).Ref!=null);
                    if(boss==null)yield return new WaitForSeconds(.25f);
                }
                Check("真实格林部件",boss!=null);
                if(controller!=null && boss!=null)
                {
                    var info=EnemyStats.Of(boss);int native=boss.hp;
                    int levelWas=controller.BossLevel;
                    foreach(var sequence in sequences.OrderBy(GodhomeBalance.DoorOf))
                    {
                        seqField.SetValue(null,sequence);dataField.SetValue(null,new BossSequenceController.BossSequenceData {bossSequenceName=sequence.name});
                        var p=GodhomeBalance.Current(info.Scene);
                        Check("真实生命接线/"+p.Key,EnemyStats.LifeWhole(info)==Mathf.CeilToInt(BossStats.GodhomeLifeOf(info.Ref,info.Scene,GameDifficulty.Current,p)*info.Tune.Life*info.Players*BossPhases.WholeFactor(info.Record,info.Scene)));
                        Check("真实防御接线/"+p.Key,info.Defense==GodhomeBalance.Defense(EnemyStats.BossDefense(info.Ref,info.Tune),p));
                        Check("真实伤害接线/"+p.Key,Mathf.Abs(EnemyStats.DamageFactor(info)-Areas.BossDamage*info.Tune.Damage*p.Damage)<.0001f);
                        Check("原生HP不改写/"+p.Key,boss.hp==native);
                        actual.Add(new {Profile=p.Key,Scene=info.Scene,Life=EnemyStats.LifeWhole(info),Defense=info.Defense,Damage=EnemyStats.DamageFactor(info),NativeHp=boss.hp});
                    }
                    seqField.SetValue(null,null);dataField.SetValue(null,null);
                    for(int level=0;level<3;level++)
                    {
                        controller.BossLevel=level;
                        var p=GodhomeBalance.Current(info.Scene,1.5f);
                        Check("原版众神堂档位/"+level,p.Key==GodhomeBalance.Hall[level].Key);
                        actual.Add(new {Profile=p.Key,Scene=info.Scene,Life=EnemyStats.LifeWhole(info),Defense=info.Defense,Damage=EnemyStats.DamageFactor(info),NativeHp=boss.hp});
                    }
                    controller.BossLevel=levelWas;
                }
            }
            finally
            {
                seqField.SetValue(null,seqWas);dataField.SetValue(null,dataWas);GameDifficulty.Current=difficultyWas;mod.DevNoFallDamage=false;
            }
            File.WriteAllText(Path.Combine(DevCopy.SaveDir,"godhome-balance.json"),JsonConvert.SerializeObject(new {Checks=checks,Failures=failures,
                Profiles=profiles.Select(p=>new {p.Key,p.Name,p.Life,p.Defense,p.FlatDefense,p.Damage}).ToArray(),Rows=rows,ActualScene=actual},Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] 神居分级汇总 {checks-failures}/{checks} {(failures==0 ? "通过" : "不通过")}");
        }
    }
}
