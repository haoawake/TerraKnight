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
        IEnumerator GuardedDropReliability()
        {
            var stack=new Stack<IEnumerator>();stack.Push(DropReliabilityTest());
            try {while(stack.Count>0){var current=stack.Peek();bool more;
                try{more=current.MoveNext();}catch(Exception ex){Plugin.Log.LogError("[AutoTest] DROP_RELIABILITY 不通过 "+ex);break;}
                if(!more){(current as IDisposable)?.Dispose();stack.Pop();continue;}
                if(current.Current is IEnumerator nested)stack.Push(nested);else yield return current.Current;
            }}finally{while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();}
        }

        IEnumerator DropReliabilityTest()
        {
            if(!DevCopy.Verified)throw new InvalidOperationException("Isolated save required");
            yield return GoTo("Town","left1");
            mod.ApplyCreative(false);TerraMod.DevAllInfinite=false;mod.DevNoFallDamage=true;
            int checks=0,failures=0,savedStage=Shop.DevStage,savedFilter=InventoryGui.DevBestiaryDropStage;
            var diff=GameDifficulty.Current;var sink=EnemyDrops.DevSink;var random=UnityEngine.Random.state;
            bool given=EssentialDrops.ManipulatorGiven;
            var pending=EnemyDrops.PendingLoot();var details=new List<object>();
            var got=new Dictionary<ItemType,int>();
            void Check(string label,bool pass){checks++;if(!pass)failures++;Plugin.Log.LogInfo($"[AutoTest] DROP_RELIABILITY {label} {(pass?"通过":"不通过")}");}
            int Count(ItemType t)=>got.TryGetValue(t,out int n)?n:0;
            void Reset(){mod.GrantedAbilities.Remove(EssentialDrops.ManipulatorKey);got.Clear();}
            try
            {
                InventoryGui.DevBestiarySelect(-10);Shop.DevStage=5;GameDifficulty.Current=DifficultyLevel.Normal;
                var independent=new[]{new EnemyDrops.Drop{Type=ItemType.BeeGun,Num=1,Den=4,Min=1,Max=1},new EnemyDrops.Drop{Type=ItemType.BeeGun,Num=1,Den=2,Min=1,Max=1}};
                Check("independent-item-chance",Mathf.Abs(EnemyDrops.ItemChance(independent,ItemType.BeeGun,5)-.625f)<.00001f);
                independent[0].Group=independent[1].Group=100;independent[0].Num=independent[1].Num=1;independent[0].Den=independent[1].Den=1;
                Check("exclusive-group-item-chance",EnemyDrops.ItemChance(independent,ItemType.BeeGun,5)==1f);
                EnemyDrops.DevSink=(t,n)=>got[t]=Count(t)+n;
                foreach(DifficultyLevel mode in Enum.GetValues(typeof(DifficultyLevel)))
                {
                    GameDifficulty.Current=mode;Reset();
                    const string scene="Dream_Final_Boss";Relics.ContextScene=scene;Relics.ContextName=null;
                    var page=InventoryGui.DevBookDropsIn("FinalBoss",scene);
                    var manip=page.Where(d=>d.Type==ItemType.LunarCraftingStation).ToArray();
                    Check(mode+"/radiance-preview-guaranteed",manip.Length==1&&manip[0].Group==0&&EnemyDrops.EffectiveChance(manip[0])==1f);
                    EnemyDrops.Roll("FinalBoss",Vector2.zero,true,false,scene:scene);
                    if(TreasureBags.Active){
                        var bag=got.Keys.FirstOrDefault(TreasureBags.Is);
                        Check(mode+"/bag-produced",bag!=ItemType.None&&Count(ItemType.LunarCraftingStation)==0&&!EssentialDrops.ManipulatorGiven);
                        got.Clear();mod.Inventory.Slots[30]=ItemStack.Of(bag);
                        Check(mode+"/bag-opened",TreasureBags.TryOpen(mod.Inventory,30));
                    }
                    Check(mode+"/manipulator-delivered-once",Count(ItemType.LunarCraftingStation)==1&&EssentialDrops.ManipulatorGiven);
                    Check(mode+"/claimed-hidden-in-book",!InventoryGui.DevBookDropsIn("FinalBoss",scene).Any(d=>d.Type==ItemType.LunarCraftingStation));
                    got.Clear();EnemyDrops.Roll("FinalBoss",Vector2.zero,true,false,fromBag:TreasureBags.Active,scene:scene);
                    Check(mode+"/no-repeat-manipulator",Count(ItemType.LunarCraftingStation)==0);
                    details.Add(new{Kind="radiance",Difficulty=mode.ToString(),Preview=manip.Length==1?manip[0].Chance:0});
                }
                GameDifficulty.Current=DifficultyLevel.Normal;
                // Every boss/area/stage candidate: a sure once-only item must actually roll,
                // including Godhome stages 0-4 where endgame supplies must be absent.
                foreach(string rec in EnemyDrops.DevRecords.Where(EnemyDrops.IsBoss))
                    foreach(var home in BestiaryHabitats.Of(rec))
                        for(int stage=0;stage<=5;stage++)
                        {
                            Shop.DevStage=stage;Reset();Relics.ContextScene=home.Scene;
                            var rows=EnemyDrops.In(rec,home.Scene);
                            bool expected=rows.Any(d=>d.Type==ItemType.LunarCraftingStation&&!EnemyDrops.Locked(d));
                            EnemyDrops.Roll(rec,Vector2.zero,true,false,scene:home.Scene);
                            Check($"one-off/{rec}/{home.Scene}/{stage}",Count(ItemType.LunarCraftingStation)==(expected?1:0)&&EssentialDrops.ManipulatorGiven==expected);
                            if(expected){got.Clear();EnemyDrops.Roll(rec,Vector2.zero,true,false,scene:home.Scene);Check($"repeat/{rec}/{home.Scene}/{stage}",Count(ItemType.LunarCraftingStation)==0);}
                        }
                // No sink: prove that ending rewards really reach inventory, rather than merely
                // appearing in a preview or a simulated result.
                Shop.DevStage=5;Reset();EnemyDrops.DevSink=null;EnemyDrops.RestorePendingLoot(null);
                Array.Clear(mod.Inventory.Slots,0,mod.Inventory.Slots.Length);
                EnemyDrops.Roll("FinalBoss",Vector2.zero,true,false,scene:"Dream_Final_Boss");
                Check("radiance-real-inventory",Recipes.Count(mod.Inventory,ItemType.LunarCraftingStation)==1&&EssentialDrops.ManipulatorGiven);
                Reset();for(int i=0;i<mod.Inventory.Slots.Length;i++)mod.Inventory.Slots[i]=ItemStack.Of(ItemType.DirtBlock,9999);
                EnemyDrops.Roll("FinalBoss",Vector2.zero,true,false,scene:"Dream_Final_Boss");
                Check("full-bag-reward-queued",EnemyDrops.PendingLoot().Any(s=>s.Type==ItemType.LunarCraftingStation)&&EssentialDrops.ManipulatorGiven);
                // Synchronous real save/load: no frame can empty the queue between write and read.
                Save.ModSave.Write(slot,mod);EnemyDrops.RestorePendingLoot(null);mod.GrantedAbilities.Remove(EssentialDrops.ManipulatorKey);
                Save.ModSave.Read(slot,mod);
                Check("full-bag-save-reload",EnemyDrops.PendingLoot().Any(s=>s.Type==ItemType.LunarCraftingStation)&&EssentialDrops.ManipulatorGiven);
                Save.ModSave.Read(slot+9,mod);
                Check("pending-clear-for-new-slot",EnemyDrops.PendingLoot().Length==0&&!EssentialDrops.ManipulatorGiven);
                Save.ModSave.Read(slot,mod);
                Check("pending-returns-with-original-slot",EnemyDrops.PendingLoot().Any(s=>s.Type==ItemType.LunarCraftingStation)&&EssentialDrops.ManipulatorGiven);
                EnemyDrops.RestorePendingLoot(null);
            }
            finally
            {
                Shop.DevStage=savedStage;InventoryGui.DevBestiarySelect(-11-savedFilter);GameDifficulty.Current=diff;
                EnemyDrops.DevSink=sink;UnityEngine.Random.state=random;Relics.ContextScene=Relics.ContextName=null;
                EnemyDrops.RestorePendingLoot(pending);
                if(given)mod.GrantedAbilities.Add(EssentialDrops.ManipulatorKey);else mod.GrantedAbilities.Remove(EssentialDrops.ManipulatorKey);
            }
            File.WriteAllText(Path.Combine(DevCopy.SaveDir,"drop-reliability.json"),JsonConvert.SerializeObject(new{Checks=checks,Failures=failures,Details=details},Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] DROP_RELIABILITY {checks} checks {failures} failures {(failures==0?"通过":"不通过")}");
            yield return RadianceSettlementProbe();
            yield return BookAuditTest();
        }

        IEnumerator RadianceSettlementProbe()
        {
            var previousDifficulty=GameDifficulty.Current;int previousStage=Shop.DevStage;
            bool wasGiven=EssentialDrops.ManipulatorGiven;int before=Bestiary.KillsOf("FinalBoss");
            bool entered=false,body=false,credited=false,delivered=false;
            try{
                GameDifficulty.Current=DifficultyLevel.Normal;Shop.DevStage=5;
                mod.GrantedAbilities.Remove(EssentialDrops.ManipulatorKey);EnemyDrops.RestorePendingLoot(null);
                Array.Clear(mod.Inventory.Slots,0,mod.Inventory.Slots.Length);
                PlayerData.instance.hasDreamNail=true;PlayerData.instance.isInvincible=true;
                entered=BossArena.Fight("FinalBoss");
                float until=Time.realtimeSinceStartup+30f;
                while(entered&&!BossArena.Arrived("FinalBoss")&&Time.realtimeSinceStartup<until)yield return null;
                yield return new WaitForSecondsRealtime(3f);
                body=UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                    .Any(h=>h.gameObject.scene.name=="Dream_Final_Boss"&&UI.EnemyHover.RecordOf(h)=="FinalBoss");
                if(body){
                    // The terminal signal emitted by Radiance's native win sequence. This probes
                    // credit/deferred-loot delivery; it is not a complete simulated boss battle.
                    PlayerData.instance.SetBool("killedFinalBoss",false);
                    PlayerData.instance.SetBool("killedFinalBoss",true);
                    until=Time.realtimeSinceStartup+17f;
                    while(Time.realtimeSinceStartup<until&&!EssentialDrops.ManipulatorGiven)yield return null;
                    credited=Bestiary.KillsOf("FinalBoss")==before+1;
                    delivered=Recipes.Count(mod.Inventory,ItemType.LunarCraftingStation)==1&&EssentialDrops.ManipulatorGiven;
                    Shot(GameManager.instance.sceneName,"radiance_loot_settlement");
                }
            }finally{
                GameDifficulty.Current=previousDifficulty;Shop.DevStage=previousStage;
                if(wasGiven)mod.GrantedAbilities.Add(EssentialDrops.ManipulatorKey);else mod.GrantedAbilities.Remove(EssentialDrops.ManipulatorKey);
            }
            bool pass=entered&&body&&credited&&delivered;
            Plugin.Log.LogInfo($"[AutoTest] RADIANCE_SETTLEMENT entered={entered} body={body} credited={credited} delivered={delivered} {(pass?"通过":"不通过")}");
            File.WriteAllText(Path.Combine(DevCopy.SaveDir,"radiance-settlement.json"),JsonConvert.SerializeObject(new{Entered=entered,BodyFound=body,Credited=credited,Delivered=delivered,Pass=pass,Scope="Native terminal flag -> Bestiary credit -> deferred loot -> real inventory; not a full boss battle."},Formatting.Indented));
            yield return GoTo("Town","left1");
        }
    }
}
