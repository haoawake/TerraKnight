using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TerraKnight.Items;
using TerraKnight.Town;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator RewardBalanceFixTest()
        {
            if(!DevCopy.Verified)throw new InvalidOperationException("Isolated save required");
            yield return GoTo("Town","left1");
            mod.ApplyCreative(false);var inv=mod.Inventory;inv.Infinite=false;PlayerData.instance.isInvincible=true;
            int checks=0,failures=0;var results=new List<object>();
            var oldClears=VanillaBossProgression.DevClears;
            int oldStage=Shop.DevStage;var oldDifficulty=GameDifficulty.Current;var oldRandom=UnityEngine.Random.state;
            try
            {
            VanillaBossProgression.DevClears=new HashSet<string>();
            void Check(string name,bool pass,string detail="") {
                checks++;if(!pass)failures++;
                results.Add(new {Name=name,Pass=pass,Detail=detail});
                if(!pass || checks<35)Plugin.Log.LogInfo($"[AutoTest] REWARD_FIX {name} {(pass?"通过":"不通过")} {detail}");
            }
            void EmptyBag(){Array.Clear(inv.Slots,0,inv.Slots.Length);inv.Mouse=default;}
            Shop.DevStage=0;int gear=0;
            for(int n=0;n<1000;n++) {
                var got=Fishing.DevContents(ItemType.GoldenCrate);
                var equipment=got.Where(s=>s.Def.IsWeapon||s.Def.IsArmor||s.Def.IsAccessory).ToArray();
                gear+=equipment.Length;
                Check("gold0-legal-"+n,equipment.Length==1 && equipment.All(s=>s.Def.Stage==0 && !ProgressionBalance.ShopOnlyWeapons.Contains(s.Type) && !ProgressionBalance.ExclusiveBossItems.Contains(s.Type)));
            }
            Check("gold0-equipment",gear==1000,"pieces="+gear);
            for(int stage=0;stage<=5;stage++) {
                Shop.DevStage=stage;
                foreach(var crate in Fishing.DevCrateTypes) {
                    if(ItemDb.Get(crate).Stage>stage)continue;
                    for(int n=0;n<20;n++) {
                        var got=Fishing.DevContents(crate);
                        var equipment=got.Where(s=>s.Def.IsWeapon||s.Def.IsArmor||s.Def.IsAccessory).ToArray();
                        Check($"crate-legal-{stage}-{crate}-{n}",equipment.All(s=>s.Def.Stage<=stage));
                        Check($"crate-no-terminal-{stage}-{crate}-{n}",got.All(s=>s.Type!=ItemType.LunarOre&&!GodhomeRewards.LunarEquipment(s.Type)));
                        if(crate.ToString().StartsWith("Coffer"))Check($"coffer-unique-{stage}-{crate}-{n}",equipment.Select(s=>s.Type).Distinct().Count()==equipment.Length);
                    }
                    yield return null;
                }
            }
            Shop.DevStage=0;inv.CollectedItems.Clear();inv.RecordCollection(ItemType.EnchantedBoomerang);
            // Vilethorn is now MageKnight's reserved reward, so it cannot be a crate-weight fixture.
            var weighted=new[]{ItemType.EnchantedBoomerang,ItemType.Spear};int missing=0;
            Check("collection-fixture-eligible",weighted.All(t=>ProgressionBalance.Unlocked(t)&&!BossDropPlan.BossOnly(t)&&!ProgressionBalance.ShopOnlyWeapons.Contains(t)));
            for(int n=0;n<12000;n++)if(Fishing.PickCollectionWeighted(weighted,ItemType.None)==ItemType.Spear)missing++;
            Check("collection-weight-2-to-1",missing>7600&&missing<8400,"missing="+missing+"/12000");
            Check("collection-exclusion",Fishing.PickCollectionWeighted(weighted,ItemType.Spear)==ItemType.EnchantedBoomerang);
            Check("collection-boss-reserved",Fishing.PickCollectionWeighted(new[]{ItemType.Vilethorn},ItemType.None)==ItemType.None);
            EmptyBag();inv.CollectedItems.Clear();inv.Pickup(ItemType.FeralClaws,1);EmptyBag();
            Check("sold-or-stored-history",inv.CollectedItems.Contains(ItemType.FeralClaws));
            inv.Safe[0]=ItemStack.Of(ItemType.FishFinder);inv.Forge[0]=ItemStack.Of(ItemType.LavaproofTackleBag);
            var owned=(HashSet<ItemType>)HarmonyLib.AccessTools.Method(typeof(Angler),"Owned").Invoke(null,new object[]{inv});
            Check("banks-recognized",owned.Contains(ItemType.FishFinder)&&owned.Contains(ItemType.LavaproofTackleBag));
            for(int i=0;i<inv.Slots.Length;i++)inv.Slots[i]=ItemStack.Of(ItemType.DirtBlock,9999);
            inv.CollectedItems.Remove(ItemType.IronBroadsword);inv.Pickup(ItemType.IronBroadsword,1);
            Check("failed-pickup-not-collected",!inv.CollectedItems.Contains(ItemType.IronBroadsword));EmptyBag();

            Shop.DevStage=5;GameDifficulty.Current=DifficultyLevel.Master;
            foreach(string key in new[]{"BigBuzzer","MageKnight"}) {
                var r=BossStats.Of(key,-1);string scene=BestiaryHabitats.WorldScene(key);
                Check(key+"-boss-classification",ProgressionBalance.FormalBoss(key)&&EnemyDrops.IsBoss(key)&&EnemyStats.IsBossKind(key,1)&&!EnemyDrops.IsEliteRecord(key));
                Check(key+"-repeat-main",EnemyDrops.At(key,5,false,false,false,scene).Any(d=>d.Group==100&&d.Chance==1));
                Check(key+"-boss-geo",EnemyDrops.BossGeo(key,scene)>0);
            }
            // Ability stage alone cannot unlock a world boss's exclusive Godhome rewards.
            foreach(bool cleared in new[]{false,true}) {
            VanillaBossProgression.DevClears.Clear();
            if(cleared)VanillaBossProgression.DevClears.UnionWith(VanillaBossProgression.All.Keys);
            for(int stage=0;stage<=5;stage++)foreach(var kv in BossStats.DevAll) {
                Shop.DevStage=stage;
                string scene=kv.Key=="Hornet@4"?"GG_Hornet_2":BestiaryHabitats.Of(kv.Key).FirstOrDefault(h=>Areas.GodhomeRoom(h.Scene)).Scene;
                if(scene==null)continue;
                var rows=EnemyDrops.At(kv.Key,stage,false,false,false,scene);
                bool terminal=cleared&&stage==5&&kv.Key=="FinalBoss";
                Check($"terminal-loot-{cleared}-{stage}-{kv.Key}",terminal ? rows.Any(d=>d.Type==ItemType.LunarOre) : rows.All(d=>d.Type!=ItemType.LunarOre&&!GodhomeRewards.LunarEquipment(d.Type)));
                if(kv.Key=="HollowKnightPrime")Check($"pillar-reward-{cleared}-{stage}",rows.Any(d=>d.Type==ItemType.FragmentStardust)==(cleared&&stage==5));
            }
            }
            Check("stage4-to-5-growth",Math.Abs(BossHealthBudgets.StageShare(5)/BossHealthBudgets.StageShare(4)-1.8f)<.001f);
            Check("world-anchors",Math.Abs(BossHealthBudgets.WorldMasterBudget("Hornet")-4800)<1&&Math.Abs(BossHealthBudgets.WorldMasterBudget("FinalBoss")-200000)<1);
            Shop.DevStage=5;
            foreach(string record in EnemyDrops.DevRecords) {
                string scene=BestiaryHabitats.WorldScene(record)??EnemyDrops.SceneOf(record);
                if(record=="FinalBoss" || GodhomeRewards.LunarFight(record,Areas.GodhomeRoom(scene),5))continue;
                var drops=EnemyDrops.At(record,5,false,false,false,scene);
                Check("world-no-terminal-"+record,drops.All(d=>d.Type!=ItemType.LunarOre&&!GodhomeRewards.LunarEquipment(d.Type)));
            }
            Check("old-bag-materials",GodhomeRewards.OfIndex(8,true).Materials==1f&&GodhomeRewards.OfIndex(8).Materials==.5f);
            var bag=ItemStack.Of(TreasureBags.For("BigFly","GG_Gruz_Mother","Gruz Mother"));
            bag.Forge=99;
            Check("new-bag-metadata-save",Save.ModSave.DevRoundTrip(bag).Forge==99);
            bag.Forge=35;
            Check("old-bag-metadata-save",Save.ModSave.DevRoundTrip(bag).Forge==35);
            Check("bag-policy-network-version",Net.Server.Protocol=="TerraKnight"+Plugin.Version+"/net14");
            object run=new object(),next=new object();
            Check("completion-once",PantheonRewards.TryClaim(run)&&!PantheonRewards.TryClaim(run)&&PantheonRewards.TryClaim(next)&&!PantheonRewards.TryClaim(run));
            foreach(bool cleared in new[]{false,true}) {
            VanillaBossProgression.DevClears.Clear();
            if(cleared)VanillaBossProgression.DevClears.UnionWith(VanillaBossProgression.All.Keys);
            for(int stage=0;stage<=5;stage++)for(int door=1;door<=5;door++) {
                var got=PantheonRewards.Contents(door,stage);
                Check($"completion-legal-{cleared}-{stage}-{door}",got.Count>0&&got.All(s=>s.Def.Stage<=stage));
                Check($"completion-terminal-{cleared}-{stage}-{door}",cleared&&stage==5&&door==5 ? got.Any(s=>s.Type==ItemType.LunarOre) : got.All(s=>s.Type!=ItemType.LunarOre&&!GodhomeRewards.LunarEquipment(s.Type)));
            }
            }

            EmptyBag();Angler.Clear();Angler.Saved=true;Shop.DevStage=0;
            int today=TerraTime.Day;Angler.RefreshAt(today);
            Check("angler-first-credit",Angler.Pending==1&&!Angler.QuestDone);
            var fish=Angler.Quest;Angler.RefreshAt(today+20);
            Check("angler-cap-and-retain",Angler.Pending==3&&Angler.Quest==fish);
            Angler.RefreshAt(today+20);Check("angler-no-repeat-refill",Angler.Pending==3);
            Angler.RefreshAt(today);Check("angler-old-host-clock-no-lock-or-refill",Angler.Pending==3&&Angler.QuestDay==today);
            for(int n=0;n<3;n++) {
                inv.Pickup(Angler.Quest,1);int before=Angler.QuestsDone;
                Angler.QuestButton(inv,"Angler");Check("angler-backlog-turnin-"+n,Angler.QuestsDone==before+1&&Angler.Pending==2-n);
            }
            int done=Angler.QuestsDone;Angler.QuestButton(inv,"Angler");Check("angler-no-fourth",Angler.QuestsDone==done&&Angler.QuestDone);
            Angler.QuestDay=today;Angler.Pending=2;Angler.QuestDone=false;Angler.Quest=fish;
            inv.RecordCollection(ItemType.FeralClaws);EmptyBag();
            int slot=Save.ModSave.DevStateSlot;
            Save.ModSave.Write(slot,mod);
            string path=Path.Combine(TerraMod.SaveDir,$"TerraKnight_slot{slot}.json");
            inv.CollectedItems.Clear();Angler.Clear();Save.ModSave.Read(slot,mod);
            Check("save-roundtrip",inv.CollectedItems.Contains(ItemType.FeralClaws)&&Angler.Pending==2&&Angler.Quest==fish);
            var file=JObject.Parse(File.ReadAllText(path));file.Remove("CollectedItems");file.Remove("AnglerPending");file["AnglerDone"]=false;
            File.WriteAllText(path,file.ToString());Save.ModSave.Read(slot,mod);
            Check("legacy-save-migration",Angler.Pending==1&&inv.CollectedItems.Contains(ItemType.FishFinder)&&inv.CollectedItems.Contains(ItemType.LavaproofTackleBag));
            Shop.DevStage=-1;
            File.WriteAllText(Path.Combine(DevCopy.SaveDir,"reward-fixes.json"),JsonConvert.SerializeObject(new{Checks=checks,Failures=failures,Results=results},Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] REWARD_FIX 汇总 {checks-failures}/{checks} {(failures==0?"通过":"不通过")}");
            }
            finally
            {
                VanillaBossProgression.DevClears=oldClears;Shop.DevStage=oldStage;
                GameDifficulty.Current=oldDifficulty;UnityEngine.Random.state=oldRandom;
            }
        }
    }
}
