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
        IEnumerator MaterialContinuityTest()
        {
            if(!DevCopy.Verified)throw new InvalidOperationException("Isolated saves required");
            int oldStage=Shop.DevStage,checks=0,failed=0,transitions=0;
            var difficulty=GameDifficulty.Current;
            var rng=UnityEngine.Random.state;
            var sink=EnemyDrops.DevSink;
            var inputs=new HashSet<ItemType>(Recipes.All.SelectMany(r=>r.Ingredients.Select(i=>i.Type)));
            inputs.UnionWith(ProgressionBalance.ResourceStages.Keys);
            inputs.UnionWith(ItemDb.All.Where(d=>!d.Placeholder && d.Source!=ItemSource.Craft && d.MaxStack>1
                && !d.IsWeapon && !d.IsAmmo && !d.HkItem && !d.GameTool).Select(d=>d.Type));
            var losses=new List<object>();
            var samples=new List<object>();
            void Check(string label,bool pass){checks++;if(!pass){failed++;Plugin.Log.LogError("[AutoTest] 材料保留 "+label+" 不通过");}}
            void Verify(string record,string scene) {
                var seen=new HashSet<ItemType>();
                for(int stage=0;stage<=5;stage++) {
                    if(Areas.LevelOf(scene)>stage)continue;
                    Shop.DevStage=stage;
                    var rows=EnemyDrops.At(record,stage,false,stage>=4,stage>=5,scene);
                    var now=new HashSet<ItemType>(rows.Where(d=>inputs.Contains(d.Type) && !d.Master && !EnemyDrops.Locked(d,stage)
                        && EnemyDrops.MarginalChance(rows,d,stage)>0).Select(d=>d.Type));
                    foreach(var type in seen) {
                        transitions++;
                        bool pass=now.Contains(type);
                        if(!pass)losses.Add(new{Record=record,Scene=scene,Stage=stage,Type=type.ToString()});
                        Check(record+"/"+scene+"/"+stage+"/"+type,pass);
                    }
                    seen.UnionWith(now);
                }
            }
            try {
                GameDifficulty.Current=DifficultyLevel.Normal;
                UnityEngine.Random.InitState(10192027);
                foreach(var record in EnemyDrops.DevRecords.Where(r=>!EnemyDrops.IsBoss(r))) {
                    Verify(record,EnemyDrops.SceneOf(record));
                    if(checks%200<20)yield return null;
                }
                // The kill's actual region can differ from its roster's default.
                foreach(var record in new[]{"AcidFlyer","Crawler"})
                    foreach(var scene in new[]{"Fungus1_01","Crossroads_01","Mines_02","Deepnest_01"})Verify(record,scene);
                Check("干火箭原版购买来源",ItemDb.Get(ItemType.DryRocket).Source==ItemSource.Shop
                    && Town.TownNpcs.DevSourcesAt(5).Any(s=>s.Type==ItemType.DryRocket));
                foreach(int stage in new[]{4,5}) {
                    Shop.DevStage=stage;
                    foreach(var area in new[]{"蜂巢","Hive_01"}) {
                        var caught=Fishing.DevSimulate(area,false,100,1664,5000);
                        Check("蜂巢蜂蜜鱼实际可钓 "+area+"/"+stage,caught.TryGetValue(ItemType.Honeyfin,out int count) && count>0);
                        Plugin.Log.LogInfo($"[AutoTest] 蜂蜜鱼持续来源 {area}/{stage} 钓鱼5000次 蜂蜜鱼{count}");
                    }
                }
                foreach(int stage in new[]{1,5}) {
                    Shop.DevStage=stage;
                    foreach(var type in new[]{ItemType.Gel,ItemType.FallenStar,ItemType.Amethyst,ItemType.Cobweb}) {
                        const string record="AcidFlyer",scene="Fungus1_01";
                        var rows=EnemyDrops.At(record,stage,false,stage>=4,stage>=5,scene);
                        float chance=rows.Where(d=>d.Type==type).Sum(d=>EnemyDrops.MarginalChance(rows,d,stage));
                        Check("原地区实际掉率大于零 "+type+"/"+stage,chance>0);
                        int trials=chance>0 ? Math.Min(100000,Math.Max(5000,Mathf.CeilToInt(15/chance))) : 0;
                        int got=0;
                        EnemyDrops.DevSink=(t,n)=>{if(t==type)got+=n;};
                        for(int n=0;n<trials;n++) {
                            EnemyDrops.Roll(record,Vector2.zero,true,false,false,scene);
                            if(n%5000==0)yield return null;
                        }
                        Check("升阶段后实际刷到 "+type+"/"+stage,got>0);
                        samples.Add(new{Stage=stage,Record=record,Scene=scene,Type=type.ToString(),Trials=trials,Count=got,Chance=chance});
                    }
                }
                File.WriteAllText(Path.Combine(DevCopy.SaveDir,"material-continuity.json"),JsonConvert.SerializeObject(new{Checks=checks,Failures=failed,Transitions=transitions,Losses=losses,Samples=samples},Formatting.Indented));
                Plugin.Log.LogInfo($"[AutoTest] 材料持续来源汇总 {checks-failed}/{checks} 转阶段检查{transitions} {(failed==0 ? "通过" : "不通过")}");
            }
            finally{Shop.DevStage=oldStage;GameDifficulty.Current=difficulty;UnityEngine.Random.state=rng;EnemyDrops.DevSink=sink;}
        }
    }
}
