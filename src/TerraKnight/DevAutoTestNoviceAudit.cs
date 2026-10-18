using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TerraKnight.Items;
using TerraKnight.Town;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator NoviceNumericalAudit()
        {
            if(!DevCopy.Verified)throw new InvalidOperationException("Isolated save required");
            yield return GoTo("Town","left1");
            TerraMod.DevAllInfinite=false;mod.ApplyCreative(false);mod.Inventory.Infinite=false;
            PlayerData.instance.isInvincible=true;mod.DevNoFallDamage=true;
            var previous=GameDifficulty.Current;int previousStage=Shop.DevStage,checks=0,failures=0;
            var mobs=new List<object>();var purchases=new List<object>();
            void Check(string label,bool pass){checks++;if(!pass){failures++;Plugin.Log.LogError("[AutoTest] 新手补充 "+label+" 不通过");}}
            try
            {
                for(int stage=0;stage<6;stage++)
                {
                    Shop.DevStage=stage;
                    foreach(string record in EnemyDrops.DevRecords.Where(r=>!EnemyDrops.IsBoss(r) && EnemyDrops.HpOf(r)>0))
                    {
                        int level=Areas.LevelOf(EnemyDrops.SceneOf(record));float masterHp=0;int masterDefense=0;
                        foreach(var difficulty in new[]{DifficultyLevel.Master,DifficultyLevel.Expert,DifficultyLevel.Hard,DifficultyLevel.Normal})
                        {
                            GameDifficulty.Current=difficulty;
                            var info=new EnemyStats.Info {Level=level,MaxHp=EnemyDrops.HpOf(record),Record=record,Scene=EnemyDrops.SceneOf(record)};
                            int hp=EnemyStats.LifeMax(info),defense=info.Defense;
                            if(difficulty==DifficultyLevel.Master){masterHp=info.MaxHp*info.Scale;masterDefense=defense;}
                            float expected=masterHp*GameDifficulty.FactorOf(difficulty);
                            Check("生命倍率/"+record+"/"+stage+"/"+difficulty,Math.Abs(hp-Mathf.CeilToInt(expected))<=1 && hp>0);
                            Check("防御不随难度重复倍率",defense==masterDefense && defense>=0);
                            int raw=Areas.DamagePerMask(Areas.StatLevel(level));
                            Check("伤害倍率",raw==Mathf.RoundToInt(Areas.MasterDamagePerMask(Areas.StatLevel(level))*GameDifficulty.FactorOf(difficulty)));
                            mobs.Add(new {Stage=stage,Record=record,Difficulty=difficulty.ToString(),Level=level,Life=hp,Defense=defense,OneMaskRaw=raw});
                        }
                    }
                }
                // Actual shop cursor, wallet and item transfer with only a finite earned
                // budget from the latest acquisition cohort. No free stock or gold.
                var artifacts=Directory.GetParent(Directory.GetParent(DevCopy.SaveDir).FullName).FullName;
                var source=NoviceAcquisitionSource(artifacts);
                Check("存在采集预算证据",source!=null);
                if(source!=null)
                {
                    Check("起步空瓶供应",TownNpcs.DevSourcesAt(0).Any(s=>s.Type==ItemType.Bottle && s.Npc=="Merchant"));
                    var data=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(source));
                    Check("采集账本无失败",(int)data["Failures"]==0);
                    foreach(var row in data["Rows"].Where(r=>(int)r["Seed"]==0))
                    {
                        int stage=(int)row["Stage"];Shop.DevStage=stage;GameDifficulty.Current=(DifficultyLevel)Enum.Parse(typeof(DifficultyLevel),(string)row["Difficulty"]);
                        yield return null;
                        var inv=mod.Inventory;Array.Clear(inv.Slots,0,inv.Slots.Length);Array.Clear(inv.Bank,0,inv.Bank.Length);inv.Mouse=default;
                        Shop.SpareCopper=0;Shop.Earn((long)row["Cash"]);long before=Shop.Purse;
                        var requested=row["Actions"].Select(a=>(string)a).Where(a=>a.StartsWith("buy:"))
                            .Select(a=>(ItemType)Enum.Parse(typeof(ItemType),a.Substring(4).Split('*')[0])).Distinct()
                            .OrderBy(t=>t==ItemType.Bottle?0:1).ToArray();
                        bool bought=false;ItemType type=ItemType.None;long price=0;string npcName=null;
                        foreach(var item in requested)
                        {
                            foreach(var candidate in TownNpcs.DevSourcesAt(stage).Where(s=>s.Type==item && s.Npc!="TravelingMerchant"))
                            {
                                var at=TownNpcs.DevPos(candidate.Npc);if(!at.HasValue)continue;
                                HeroController.instance.transform.position=Clear(HeroController.instance,at.Value+Vector2.left*.8f);yield return null;
                                TownNpcs.Close(false);TownNpcs.TryRightClick(at.Value);TownNpcs.OpenShop();yield return null;
                                int slot=Array.FindIndex(TownNpcs.ShopItems,s=>!s.IsEmpty && s.Type==item);if(slot<0)continue;
                                price=TownNpcs.BuyPrice(slot);if(price>before)continue;
                                bought=TownNpcs.Buy(slot,1,inv);type=item;npcName=candidate.Npc;
                                Check("实际商店扣款交付/"+stage+"/"+item,bought && inv.Mouse.Type==item && inv.Mouse.Count==1 && Shop.Purse==before-price);
                                break;
                            }
                            if(bought)break;
                        }
                        // Some checkpoints have no new purchases: that is recorded, not
                        // replaced with a fictional shopping trip.
                        purchases.Add(new {Stage=stage,Class=(int)row["Class"],Difficulty=(string)row["Difficulty"],Npc=npcName,Type=type.ToString(),Bought=bought,Before=before,After=Shop.Purse,Price=price});
                        inv.Mouse=default;TownNpcs.Close(false);InventoryGui.Open=false;
                    }
                }
            }
            finally {Shop.DevStage=previousStage;GameDifficulty.Current=previous;}
            File.WriteAllText(Path.Combine(DevCopy.SaveDir,"novice-audit.json"),JsonConvert.SerializeObject(new {Checks=checks,Failures=failures,Mobs=mobs,Purchases=purchases,
                Items=ItemDb.All.Select(d=>new {Id=(int)d.Type,Type=d.Type.ToString(),d.Name,d.Stage,d.Damage,d.Defense,d.UseTime,d.UseAnimation,d.ShootSpeed,d.BuffType,d.BuffTime})},Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] 新手补充 汇总 {checks}项 失败{failures} {(failures==0?"通过":"不通过")}");
        }
    }
}
