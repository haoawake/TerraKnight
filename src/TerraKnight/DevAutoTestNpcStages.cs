using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json;
using TerraKnight.Items;
using TerraKnight.Player;
using TerraKnight.Town;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator NpcStagesTest()
        {
            if(!DevCopy.Verified)throw new InvalidOperationException("Isolated save required");
            mod.ApplyCreative(false);mod.Inventory.Infinite=false;mod.Inventory.FreeMaking=false;
            TerraStats.DevImmortal=true;PlayerData.instance.isInvincible=true;mod.DevNoFallDamage=true;
            int oldStage=Shop.DevStage;float oldOffset=TerraTime.NetOffset;bool? oldDay=TerraTime.DevForceDay;
            var oldClears=VanillaBossProgression.DevClears;
            var rows=new List<object>();var placements=new List<object>();var findings=new List<object>();var services=new List<object>();
            int checks=0,failed=0;
            void Check(string name,bool pass,string detail="")
            {checks++;if(!pass)failed++;Plugin.Log.LogInfo($"[AutoTest] NPC_STAGE {name} {(pass?"通过":"不通过")} {detail}");}
            void Find(string kind,string npc,string detail)
            {findings.Add(new {Kind=kind,Npc=npc,Detail=detail});Plugin.Log.LogInfo($"[AutoTest] NPC_STAGE_FINDING {kind} {npc} {detail}");}
            void Clock(int day,float now,bool? daytime=null)
            {TerraTime.NetOffset=day*TerraTime.FullDaySeconds+now-TerraTime.PlayTime;TerraTime.DevForceDay=daytime;}
            void Sync()
            {AccessTools.Method(typeof(TownNpcs),"Place").Invoke(null,new object[]{TerrainSystem.Find("Town"),Shop.UnlockedStage(),HeroController.instance});}
            bool Eligible(NpcDef d)=> (bool)AccessTools.Method(typeof(TownNpcs),"InTown").Invoke(null,new object[]{d,Shop.UnlockedStage()});
            string ActualTalk()
            {
                var value=AccessTools.Field(typeof(TownNpcs),"talk").GetValue(null);
                return value==null?null:((NpcDef)AccessTools.Field(value.GetType(),"Def").GetValue(value)).Internal;
            }
            bool Talk(string name)
            {
                TownNpcs.Close(false);InventoryGui.Open=false;
                var at=TownNpcs.DevPos(name);if(!at.HasValue)return false;
                var hero=HeroController.instance;hero.transform.position=Clear(hero,at.Value+Vector2.left*.8f);
                return TownNpcs.TryRightClick(at.Value)&&ActualTalk()==name;
            }
            try
            {
                Shop.DevStage=0;Clock(10,1000,false);VanillaBossProgression.DevClears=new HashSet<string>();
                yield return GoTo("Town","left1");yield return new WaitForSecondsRealtime(2);
                var defs=TownNpcs.AllDefs;
                Check("complete roster",defs.Length==39,"count="+defs.Length);
                foreach(var d in defs)
                {
                    bool texture=false;
                    try {var t=Terraria.TerrariaAssets.Texture(TownNpcs.BodyTexture(d));texture=t!=null&&t.width>0&&t.height>=TownNpcs.BodyFrames(d);}
                    catch(Exception ex){Find("body-texture-error",d.Internal,ex.Message);}
                    Check("body texture "+d.Internal,texture);
                }
                for(int stage=0;stage<=5;stage++)
                {
                    TownNpcs.Clear();Angler.Clear();Shop.DevStage=stage;Clock(10,1000,false);Sync();Sync();yield return null;
                    foreach(var d in defs)
                    {
                        bool present=TownNpcs.Present(d.Internal),auto=TownNpcs.AutoArrives(d);
                        Check("fresh stage gate "+stage+"/"+d.Internal,!present||d.Stage<=stage);
                        if(d.Kind==NpcKind.Resident||d.Kind==NpcKind.Pet)
                            Check("automatic arrival "+stage+"/"+d.Internal,present==(d.Stage<=stage&&auto));
                        if(d.Kind==NpcKind.Angler)Check("rescue required "+stage,!present);
                        if(d.Kind==NpcKind.Visitor)Check("night visitor absent "+stage,!present);
                        rows.Add(new {Stage=stage,d.Internal,d.Zh,ArrivalStage=d.Stage,Kind=d.Kind.ToString(),Auto=auto,Present=present,
                            Stock=d.Items.Length,Eligible=Eligible(d),Position=TownNpcs.DevPos(d.Internal)?.ToString()});
                    }
                    Angler.Saved=true;Sync();Check("rescued angler arrives "+stage,TownNpcs.Present("Angler"));
                    foreach(var d in defs.Where(d=>TownNpcs.AutoArrives(d)&&TownNpcs.Present(d.Internal)&&d.Kind==NpcKind.Resident))
                    {
                        if(!Talk(d.Internal)){Find("crowded-click",d.Internal,"Could not select this NPC at its current street position");continue;}
                        if(TownNpcs.TalkingHasShop)
                        {
                            TownNpcs.OpenShop();int available=0,locked=0;
                            for(int slot=0;slot<TownNpcs.ShopItems.Length;slot++)
                            {
                                var item=TownNpcs.ShopItems[slot];if(item.IsEmpty)continue;
                                if(TownNpcs.Locked(slot)){locked++;continue;}
                                available++;
                                Check("stock stage "+stage+"/"+d.Internal+"/"+item.Type,SpellRewards.Is(item.Type)||item.Def.Stage<=stage);
                            }
                            services.Add(new {Stage=stage,Npc=d.Internal,Shop=true,Available=available,Locked=locked});
                            if(available==0)Find("empty-unlocked-shop",d.Internal,"stage="+stage+" locked="+locked);
                        }
                        TownNpcs.Close(false);InventoryGui.Open=false;
                    }
                    yield return null;
                }
                Check("princess waits for mandatory residents",!TownNpcs.Present("Princess"));
                // Distinguish an arrival-stage mismatch from an unfulfilled world-Boss prerequisite.
                VanillaBossProgression.DevClears=new HashSet<string>(VanillaBossProgression.All.Keys);
                foreach(int stage in new[]{4,5})
                {
                    TownNpcs.Clear();Shop.DevStage=stage;Sync();
                    bool talked=Talk("Truffle");if(talked)TownNpcs.OpenShop();
                    var available=Enumerable.Range(0,TownNpcs.ShopItems.Length).Where(i=>!TownNpcs.ShopItems[i].IsEmpty&&!TownNpcs.Locked(i)).Select(i=>TownNpcs.ShopItems[i].Type.ToString()).ToArray();
                    services.Add(new {Stage=stage,Npc="Truffle",AllWorldBosses=true,Talk=talked,Available=available});
                    Check("truffle full progression talk "+stage,talked);
                    if(talked&&available.Length==0)Find("empty-shop-even-with-world-progress","Truffle","stage="+stage);
                    if(stage==5)Check("truffle shop opens after full progression",available.Length>0);
                    TownNpcs.Close(false);InventoryGui.Open=false;
                }
                VanillaBossProgression.DevClears=new HashSet<string>();
                // Old arrival history must not bypass functional stage gates.
                TownNpcs.Clear();Shop.DevStage=5;Angler.Saved=true;Sync();
                var residents=defs.Where(d=>d.Kind==NpcKind.Resident&&TownNpcs.Present(d.Internal)).ToArray();
                Shop.DevStage=0;Sync();
                Check("functional history respects stage",residents.All(d=>TownNpcs.Present(d.Internal)==(d.Stage==0)));
                Shop.DevStage=5;Sync();Check("residents return at unlocked stage",residents.All(d=>TownNpcs.Present(d.Internal)));
                // Exercise the actual two-click housing panel, not the DevPlace shortcut.
                foreach(var d in defs.Where(d=>d.Stage>0||d.Kind==NpcKind.Angler))
                {
                    TownNpcs.Clear();Angler.Clear();Shop.DevStage=0;Clock(10,1000,false);Sync();
                    TownNpcs.ClickHead(d,true,false);
                    bool handled=TownNpcs.PlaceClick(HeroController.instance.transform.position+Vector3.right,HeroController.instance);
                    bool immediate=TownNpcs.Present(d.Internal);Sync();bool retained=TownNpcs.Present(d.Internal);
                    placements.Add(new {d.Internal,d.Zh,ArrivalStage=d.Stage,Kind=d.Kind.ToString(),Handled=handled,Immediate=immediate,AfterReconcile=retained});
                    if(TownNpcs.AutoArrives(d))
                    {
                        Check("locked panel "+d.Internal,!handled&&!immediate&&!retained&&!TownNpcs.Placing);
                        // Bypass the head selection to exercise a stale cursor and the direct house entry independently.
                        AccessTools.Field(typeof(TownNpcs),"placing").SetValue(null,d.Internal);
                        TownNpcs.PlaceClick(HeroController.instance.transform.position+Vector3.right,HeroController.instance);
                        Check("stale cursor blocked "+d.Internal,!TownNpcs.Present(d.Internal)&&!TownNpcs.Arrived.Contains(d.Internal)&&!TownNpcs.Homes.ContainsKey(d.Internal));
                        bool housed=(bool)AccessTools.Method(typeof(TownNpcs),"TryHouse").Invoke(null,new object[]{d,(Vector2)HeroController.instance.transform.position});
                        Check("direct house blocked "+d.Internal,!housed&&!TownNpcs.Houses.ContainsKey(d.Internal)&&!TownNpcs.Arrived.Contains(d.Internal));
                        TownNpcs.Arrived.Add(d.Internal);Sync();
                        Check("legacy arrival blocked "+d.Internal,!TownNpcs.Present(d.Internal));
                    }
                    else Check("decorative placement allowed "+d.Internal,immediate);
                }
                // Actual service access is denied early, then works at the required stage.
                TownNpcs.Clear();Shop.DevStage=0;Angler.Saved=false;
                foreach(var d in defs)TownNpcs.Dismissed.Add(d.Internal);
                Sync();var nurse=defs.First(d=>d.Internal=="Nurse");TownNpcs.ClickHead(nurse,true,false);
                TownNpcs.PlaceClick(HeroController.instance.transform.position+Vector3.right,HeroController.instance);
                bool nurseTalk=Talk("Nurse");mod.Stats.Life=Mathf.Max(1,mod.Stats.LifeMax-40);Shop.Earn(10000);
                long price=TownNpcs.NursePrice(),before=Shop.Purse;TownNpcs.NurseHeal();
                bool earlyHeal=nurseTalk&&mod.Stats.Life==mod.Stats.LifeMax&&Shop.Purse==before-price;
                services.Add(new {Stage=0,Npc="Nurse",Manual=true,Talk=nurseTalk,Healed=earlyHeal,Price=price});
                Check("early nurse service denied",!nurseTalk&&!earlyHeal&&!TownNpcs.Present("Nurse"));
                Shop.DevStage=1;TownNpcs.ClickHead(nurse,true,false);
                TownNpcs.PlaceClick(HeroController.instance.transform.position+Vector3.right,HeroController.instance);
                bool unlockedTalk=Talk("Nurse");mod.Stats.Life=Mathf.Max(1,mod.Stats.LifeMax-40);
                price=TownNpcs.NursePrice();before=Shop.Purse;TownNpcs.NurseHeal();
                Check("unlocked nurse service works",unlockedTalk&&mod.Stats.Life==mod.Stats.LifeMax&&Shop.Purse==before-price);
                TownNpcs.Close(false);InventoryGui.Open=false;
                // Dismissal must prevent automatic respawn; selecting and placing deliberately restores it.
                TownNpcs.Clear();Shop.DevStage=0;Sync();bool removed=TownNpcs.Remove("Merchant");Sync();
                Check("dismissed resident stays away",removed&&!TownNpcs.Present("Merchant"));
                TownNpcs.ClickHead(defs.First(d=>d.Internal=="Merchant"),true,false);
                TownNpcs.PlaceClick(HeroController.instance.transform.position+Vector3.right,HeroController.instance);Sync();
                Check("deliberate return works",TownNpcs.Present("Merchant")&&!TownNpcs.Dismissed.Contains("Merchant"));
                foreach(var d in defs.Where(d=>TownNpcs.AutoArrives(d)&&d.Kind==NpcKind.Resident))
                {
                    TownNpcs.Clear();Shop.DevStage=d.Stage;Sync();TownNpcs.Remove(d.Internal);
                    TownNpcs.ClickHead(d,true,false);
                    TownNpcs.PlaceClick(HeroController.instance.transform.position+Vector3.right,HeroController.instance);Sync();
                    Check("unlocked relocation "+d.Internal,TownNpcs.Present(d.Internal)&&!TownNpcs.Dismissed.Contains(d.Internal));
                }
                TownNpcs.Clear();Shop.DevStage=5;Sync();
                var princess=defs.First(d=>d.Internal=="Princess");
                TownNpcs.ClickHead(princess,true,false);Check("princess prerequisites block placement",!TownNpcs.Placing);
                foreach(var d in defs.Where(d=>d.Kind==NpcKind.Resident&&!TownNpcs.AutoArrives(d)))
                    TownNpcs.DevPlace(d.Internal);
                Sync();TownNpcs.ClickHead(princess,true,false);
                TownNpcs.PlaceClick(HeroController.instance.transform.position+Vector3.right,HeroController.instance);
                Check("princess unlocked placement",TownNpcs.Present("Princess"));
                // Deterministic native-like visitor scheduling, plus two-resident and departure conditions.
                TownNpcs.Clear();Shop.DevStage=1;Angler.Saved=false;
                var visitor=defs.First(d=>d.Kind==NpcKind.Visitor);int visitDays=0,visitDay=-1;
                for(int day=0;day<1000;day++)
                {
                    Clock(day,449);bool came=Eligible(visitor);if(came){visitDays++;if(visitDay<0)visitDay=day;}
                }
                Check("visitor scheduling",visitDays>170&&visitDays<270,"visits="+visitDays+"/1000 days");
                TownNpcs.Clear();Shop.DevStage=1;Clock(visitDay,500);Sync();
                Check("visitor morning entry",TownNpcs.Present(visitor.Internal),"day="+visitDay);
                var atVisitor=TownNpcs.DevPos(visitor.Internal);
                if(atVisitor.HasValue)
                {
                    HeroController.instance.transform.position=Clear(HeroController.instance,atVisitor.Value+Vector2.left);
                    Clock(visitDay,950);Sync();Check("visitor waits for nearby player",TownNpcs.Present(visitor.Internal));
                    HeroController.instance.transform.position+=Vector3.right*60;Sync();Check("visitor leaves when player away",!TownNpcs.Present(visitor.Internal));
                }
                TownNpcs.Clear();Shop.DevStage=1;Clock(visitDay,400);
                foreach(var d in defs)if(d.Internal!="Guide")TownNpcs.Dismissed.Add(d.Internal);
                // Do not dismiss the visitor itself: only the population rule is under test.
                TownNpcs.Dismissed.Remove(visitor.Internal);Check("visitor needs two residents",!Eligible(visitor));
                // Full actual sleeping-Angler render/rescue/relocation and all-NPC service regression.
                TerraTime.NetOffset=oldOffset;TerraTime.DevForceDay=oldDay;TownNpcs.Clear();Shop.DevStage=0;
                yield return AnglerVisibilityTest();Check("actual rescue and town relocation",Angler.Saved&&TownNpcs.Present("Angler"));
                TownNpcs.Clear();Shop.DevStage=5;yield return TownAllTest();
                File.WriteAllText(Path.Combine(DevCopy.SaveDir,"npc-stage-audit.json"),JsonConvert.SerializeObject(new {Checks=checks,Failures=failed,Roster=defs.Select(d=>new {d.Internal,d.Zh,d.Stage,Kind=d.Kind.ToString(),Auto=TownNpcs.AutoArrives(d),Stock=d.Items.Length}),Rows=rows,Manual=placements,Findings=findings,Services=services,
                    VisitorDays=visitDays,Calendar=DateTime.Now,SeasonPositiveRuntimeTested=false,NativeHollowKnightStoryNpcsTested=false},Formatting.Indented));
                Plugin.Log.LogInfo($"[AutoTest] NPC_STAGE total checks={checks} failed={failed} findings={findings.Count}");
            }
            finally
            {TownNpcs.Close(false);InventoryGui.Open=false;Shop.DevStage=oldStage;TerraTime.NetOffset=oldOffset;TerraTime.DevForceDay=oldDay;VanillaBossProgression.DevClears=oldClears;TerraStats.DevImmortal=false;mod.DevNoFallDamage=false;}
        }
    }
}
