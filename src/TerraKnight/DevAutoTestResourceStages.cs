using System;
using System.Collections;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator ResourceStagesTest()
        {
            if(!DevCopy.Verified)throw new InvalidOperationException("Isolated saves required");
            int oldStage=Shop.DevStage,checks=0,failed=0;
            var difficulty=GameDifficulty.Current;
            void Check(string label,bool pass) {
                checks++;if(!pass){failed++;Plugin.Log.LogError("[AutoTest] 材料阶段 "+label+" 不通过");}
            }
            try {
                GameDifficulty.Current=DifficultyLevel.Normal;
                foreach(var pair in ProgressionBalance.ResourceStages) {
                    var d=ItemDb.Get(pair.Key);
                    Check("定义存在 "+pair.Key,d!=null && !d.Placeholder);
                    if(d.Source!=ItemSource.Craft)Check("统一门槛 "+pair.Key,d.Stage==pair.Value);
                }
                for(int stage=0;stage<=5;stage++) {
                    Shop.DevStage=stage;
                    foreach(var rec in EnemyDrops.DevRecords) {
                        var e=EnemyDrops.EntryFor(rec);
                        if(ProgressionBalance.FormalBoss(rec) || World.Areas.LevelOf(e.Scene)>stage)continue;
                        foreach(var row in EnemyDrops.At(rec,stage,false,false,false))
                            if(!row.Master && row.Chance>0)
                                Check("实际地区不越级 "+rec+"/"+row.Type+"/"+stage,ItemDb.Get(row.Type).Stage<=stage);
                    }
                    yield return null;
                }
                foreach(var sample in new[]{("BeamMiner",ItemType.AdamantiteOre),("BeamMiner",ItemType.TitaniumOre),("CrystalCrawler",ItemType.FrostCore),
                    ("Jellyfish",ItemType.PixieDust),("Jellyfish",ItemType.UnicornHorn),
                    ("FlyingSentryJavelin",ItemType.SoulofFlight)}) {
                    Check("阶段3可达来源 "+sample.Item1+"/"+sample.Item2,
                        World.Areas.LevelOf(EnemyDrops.SceneOf(sample.Item1))<=3 &&
                        EnemyDrops.At(sample.Item1,3,false,false,false).Any(d=>d.Type==sample.Item2 && d.Chance>0));
                }
                foreach(var stage in new[]{2,3}) {
                    Shop.DevStage=stage;
                    foreach(var item in new[]{ItemType.CrystalBall,ItemType.SpellTome,ItemType.Harp,ItemType.EmptyBullet,ItemType.GoldDust,ItemType.ExplosivePowder})
                        Check("商店同步 "+item+"/"+stage,Town.TownNpcs.DevSourcesAt(stage).Any(s=>s.Type==item)==(stage==3));
                }
                foreach(var rec in EnemyDrops.DevRecords.Where(r=>!EnemyDrops.IsBoss(r)))
                    for(int stage=0;stage<=5;stage++)
                        foreach(var first in new[]{false,true})
                            foreach(var row in EnemyDrops.At(rec,stage,first,true,true))
                                Check("小怪不掉制造成品 "+rec+"/"+stage+"/"+first+"/"+row.Type,!CraftOnlyLoot.OnlyCrafted(row.Type));
                foreach(var t in new[]{ItemType.BladeofGrass,ItemType.Excalibur,ItemType.TerraBlade,ItemType.AnkhShield,ItemType.Furnace})
                    Check("制造成品分类 "+t,CraftOnlyLoot.OnlyCrafted(t));
                foreach(var t in new[]{ItemType.BandofStarpower,ItemType.BlizzardinaBottle,ItemType.SandstorminaBottle})
                    Check("有配方但仍有原版世界来源 "+t,!CraftOnlyLoot.OnlyCrafted(t));
                var group=new[]{
                    new EnemyDrops.Drop{Type=ItemType.BladeofGrass,Group=999,Num=2,Den=5,Min=1,Max=1,Weight=3},
                    new EnemyDrops.Drop{Type=ItemType.Stinger,Group=999,Num=2,Den=5,Min=1,Max=1,Weight=1},
                    new EnemyDrops.Drop{Type=ItemType.Vine,Group=999,Num=2,Den=5,Min=1,Max=1,Weight=2}};
                var clean=CraftOnlyLoot.Filter(group);
                Check("删除成品保留其他条目概率",clean.Count==2 && Math.Abs(EnemyDrops.MarginalChance(clean,clean[0],5)-.4f/6)<.00001f
                    && Math.Abs(EnemyDrops.MarginalChance(clean,clean[1],5)-.8f/6)<.00001f);
                var rng=UnityEngine.Random.state;
                try {
                    UnityEngine.Random.InitState(10192026);
                    int empty=0,stinger=0,vine=0;
                    for(int n=0;n<30000;n++) {
                        var pick=EnemyDrops.Pick(clean);
                        if(pick.Type==ItemType.None)empty++;else if(pick.Type==ItemType.Stinger)stinger++;else if(pick.Type==ItemType.Vine)vine++;
                    }
                    Check("移除成品保留空结果与抽样权重",Math.Abs(empty/30000f-.5f)<.015f && Math.Abs(stinger/30000f-1f/6)<.015f
                        && Math.Abs(vine/30000f-1f/3)<.015f);
                } finally{UnityEngine.Random.state=rng;}
                Plugin.Log.LogInfo($"[AutoTest] 材料阶段全表汇总 {checks-failed}/{checks} {(failed==0 ? "通过" : "不通过")}");
            }
            finally{Shop.DevStage=oldStage;GameDifficulty.Current=difficulty;}
        }
    }
}
