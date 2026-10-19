using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator SoulArrivalInspect()
        {
            if(!DevCopy.Verified)throw new InvalidOperationException("Isolated saves required");
            yield return GoTo("Town","left1");
            mod.ApplyCreative(false); PlayerData.instance.isInvincible=true; mod.DevNoFallDamage=true;
            Player.TerraStats.DevImmortal=true;
            Shop.DevStage=5; Bestiary.Kills["MageLord"]=1;
            PlayerData.instance.mageLordDefeated=true; PlayerData.instance.mageLordEncountered=true;
            // Read the native approach's transition, without moving the player into the fight.
            var approach=UnityEngine.SceneManagement.SceneManager.LoadSceneAsync("Ruins1_32",UnityEngine.SceneManagement.LoadSceneMode.Additive);
            while(approach!=null && !approach.isDone)yield return null;
            var nativeDoors=UnityEngine.Object.FindObjectsByType<TransitionPoint>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Where(t=>t.gameObject.scene.name=="Ruins1_32")
                .Select(t=>new {t.name,t.targetScene,t.entryPoint,X=t.transform.position.x,Y=t.transform.position.y}).ToArray();
            var approachScene=UnityEngine.SceneManagement.SceneManager.GetSceneByName("Ruins1_32");
            var unload=UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(approachScene);
            while(unload!=null && !unload.isDone)yield return null;
            var inv=mod.Inventory; inv.Slots[0]=ItemStack.Of(ItemType.BossSigilSoulMaster,3);inv.Selected=0;inv.Override=-1;inv.Mouse=default;
            while(!mod.DevReady)yield return null;
            mod.DevUseSelected(HeroController.instance.transform.position);
            var samples=new List<object>(); var gates=new List<object>(); float until=Time.realtimeSinceStartup+30f,next=0f;
            bool upperEntry=false,ready=false,phase2Attacked=false,phase1Alive=false;
            while(Time.realtimeSinceStartup<until)
            {
                if(GameManager.instance.sceneName=="Ruins1_24" && Time.realtimeSinceStartup>=next)
                {
                    next=Time.realtimeSinceStartup+.1f;
                    var hero=HeroController.instance;var b=hero.GetComponent<BoxCollider2D>().bounds;
                    var bodies=UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                        .Where(h=>h.gameObject.scene.name.StartsWith("Ruins1_24") && UI.EnemyHover.RecordOf(h)=="MageLord")
                        .Select(h=>new {h.name,h.hp,Y=h.transform.position.y,Active=h.gameObject.activeInHierarchy,
                            States=string.Join(";",h.GetComponents<PlayMakerFSM>().Select(f=>f.FsmName+":"+f.ActiveStateName))}).ToArray();
                    samples.Add(new {T=Time.realtimeSinceStartup,HeroX=b.center.x,FeetY=b.min.y,Control=GameHooks.PlayerHasControl(),Placed=BossSummonStart.DevPlaced,Start=BossSummonStart.DevLast,Bodies=bodies});
                    if(BossSummonStart.DevPlaced==0 && b.min.y>28f)upperEntry=true;
                    if(BossSummonStart.DevPlaced>0 && b.min.y>27.5f && GameHooks.PlayerHasControl())ready=true;
                    foreach(var body in bodies)
                    {
                        if(body.name=="Mage Lord" && body.hp>0)phase1Alive=true;
                        if(body.name.Contains("Phase2") && !body.States.Contains("Mage Lord 2:Init"))phase2Attacked=true;
                    }
                    if(gates.Count==0)foreach(var t in UnityEngine.Object.FindObjectsByType<TransitionPoint>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                        if(t.gameObject.scene.name=="Ruins1_24")gates.Add(new {t.name,t.targetScene,t.entryPoint,X=t.transform.position.x,Y=t.transform.position.y});
                }
                yield return null;
            }
            File.WriteAllText(Path.Combine(DevCopy.SaveDir,"soul-arrival.json"),JsonConvert.SerializeObject(new {UpperEntry=upperEntry,ReadyOnFirstFloor=ready,Phase1Alive=phase1Alive,Phase2Attacked=phase2Attacked,NativeApproach=nativeDoors,Gates=gates,Samples=samples},Formatting.Indented));
            bool pass=upperEntry && ready && phase1Alive && !phase2Attacked;
            Plugin.Log.LogInfo("[AutoTest] 灵魂大师入口/上层进入、一阶段开战、二阶段待机 "+(pass?"通过":"不通过"));
            Plugin.Log.LogInfo("[AutoTest] 灵魂大师入口观察完成 samples="+samples.Count);
        }
    }
}
