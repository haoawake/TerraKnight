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
        // Native moving enemies/death FSM and native sequence completion. Large test
        // hits shorten the run; this checks payout flow, not balance or player survival.
        IEnumerator RewardFlowTest()
        {
            if(!DevCopy.Verified)throw new InvalidOperationException("Isolated save required");
            mod.ApplyCreative(false);mod.Inventory.Infinite=false;Shop.DevStage=5;
            mod.DevNoFallDamage=true;Player.TerraStats.DevImmortal=true;
            mod.Stats.LifeCrystals=15;mod.Stats.UpdateMaximums(PlayerData.instance,0);mod.Stats.Life=mod.Stats.LifeMax;
            GameDifficulty.Current=DifficultyLevel.Master;PlayerData.instance.isInvincible=true;
            int failures=0;var results=new List<object>();
            void Check(string name,bool pass,string detail="") {
                if(!pass)failures++;results.Add(new{Name=name,Pass=pass,Detail=detail});
                Plugin.Log.LogInfo($"[AutoTest] REWARD_FLOW {name} {(pass?"通过":"不通过")} {detail}");
            }
            BossSequenceController.Reset();
            BossSceneController.SetupEvent=self=>{self.BossLevel=0;self.DreamReturnEvent="DREAM RETURN";};
            PlayerData.instance.dreamReturnScene="Town";
            yield return GoTo("GG_Vengefly_V","door_dreamEnter");
            yield return new WaitForSecondsRealtime(4f);
            HealthManager[] Bodies(string rec) => UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                .Where(h=>h!=null&&!h.isDead&&h.hp>0&&h.gameObject.activeInHierarchy&&UI.EnemyHover.RecordOf(h)==rec).ToArray();
            int before=Bestiary.KillsOf("BigBuzzer"),bags=TreasureBags.DevLeft;
            var bodies=Bodies("BigBuzzer");
            Check("two-vengefly-bodies",bodies.Length==2,"bodies="+bodies.Length);
            var first=bodies.FirstOrDefault();
            for(float until=Time.realtimeSinceStartup+8;first!=null&&!first.isDead&&first.hp>0&&Time.realtimeSinceStartup<until;) {
                PlayerData.instance.isInvincible=true;
                var col=first.GetComponent<Collider2D>();
                if(col!=null)mod.Melee.HitWith(col,1000000,0,0,1,col.bounds.center);
                yield return new WaitForSecondsRealtime(.15f);
            }
            yield return new WaitForSecondsRealtime(.7f);
            Check("first-body-no-whole-reward",(first==null||first.isDead||first.hp<=0)&&Bestiary.KillsOf("BigBuzzer")==before&&TreasureBags.DevLeft==bags);
            for(float until=Time.realtimeSinceStartup+15;Bestiary.KillsOf("BigBuzzer")==before&&Time.realtimeSinceStartup<until;) {
                PlayerData.instance.isInvincible=true;
                foreach(var hm in Bodies("BigBuzzer")){var col=hm.GetComponent<Collider2D>();if(col!=null)mod.Melee.HitWith(col,1000000,0,0,1,col.bounds.center);}
                yield return new WaitForSecondsRealtime(.15f);
            }
            // Rewards deliberately wait for the native corpse/death animation.
            for(float until=Time.realtimeSinceStartup+BossLootTiming.HardCap+4;TreasureBags.DevLeft==bags&&Time.realtimeSinceStartup<until;)
                yield return new WaitForSecondsRealtime(.2f);
            Check("last-body-one-kill-one-bag",Bestiary.KillsOf("BigBuzzer")==before+1&&TreasureBags.DevLeft==bags+1,$"kills={Bestiary.KillsOf("BigBuzzer")-before} bags={TreasureBags.DevLeft-bags}");
            yield return GoTo("Town","left1");
            BossSequenceController.Reset();
            var original=Resources.LoadAll<BossSequence>("GG").First(s=>s.name=="Boss Sequence Tier 1");
            var seq=UnityEngine.Object.Instantiate(original);seq.name="Boss Sequence Tier 1";
            seq.achievementKey="";seq.customEndScene="";seq.useSceneUnlocks=false;
            var gruz=Enumerable.Range(0,original.Count).Select(original.GetBossScene).First(s=>s.sceneName=="GG_Gruz_Mother");
            AccessTools.Field(typeof(BossSequence),"bossScenes").SetValue(seq,new[]{gruz});
            mod.GrantedAbilities.Remove("godhome.door.completed.1");
            BossSequenceController.SetupNewSequence(seq,BossSequenceController.ChallengeBindings.None,"bossDoorStateTier1");
            yield return GoTo("GG_Gruz_Mother","door_dreamEnter");
            yield return new WaitForSecondsRealtime(3f);
            Check("native-sequence-active",BossSequenceController.IsInSequence&&BossSequenceController.BossCount==1);
            int main=TreasureBags.DevLeft;
            for(float until=Time.realtimeSinceStartup+35;!mod.GrantedAbilities.Contains("godhome.door.completed.1")&&Time.realtimeSinceStartup<until;) {
                PlayerData.instance.isInvincible=true;
                foreach(var hm in Bodies("BigFly")){var col=hm.GetComponent<Collider2D>();if(col!=null)mod.Melee.HitWith(col,1000000,0,0,1,col.bounds.center);}
                yield return new WaitForSecondsRealtime(.15f);
            }
            Check("native-completion-payout",BossSequenceController.WasCompleted&&mod.GrantedAbilities.Contains("godhome.door.completed.1"),"nativeFinish="+BossSequenceController.WasCompleted);
            Check("boss-bag-still-paid",TreasureBags.DevLeft==main+1);
            string save=Path.Combine(TerraMod.SaveDir,$"TerraKnight_slot{GameManager.instance.profileID}.json");
            Check("completion-persisted-before-return",File.Exists(save)&&File.ReadAllText(save).Contains("godhome.door.completed.1"));
            Shot("rewardflow","native_completion");
            BossSequenceController.Reset();Shop.DevStage=-1;BossSceneController.SetupEvent=null;
            File.WriteAllText(Path.Combine(DevCopy.SaveDir,"reward-flow.json"),JsonConvert.SerializeObject(new{Failures=failures,Results=results},Formatting.Indented));
        }
    }
}
