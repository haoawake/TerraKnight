using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json;
using TerraKnight.Items;
using TerraKnight.Player;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator GodhomeOptimizeTest()
        {
            if(!DevCopy.Verified)throw new InvalidOperationException("Isolated save required");
            mod.ApplyCreative(false);mod.Inventory.FreeMaking=false;
            var inv=mod.Inventory;var pd=PlayerData.instance;
            var savedClears=VanillaBossProgression.DevClears;int savedStage=Shop.DevStage;
            var savedDifficulty=GameDifficulty.Current;
            var results=new List<object>();var fights=new List<object>();
            int failed=0;
            void Check(string name,bool pass,string detail="")
            {if(!pass)failed++;results.Add(new {Name=name,Pass=pass,Detail=detail});Plugin.Log.LogInfo($"[AutoTest] GODHOME_OPT {name} {(pass?"通过":"不通过")} {detail}");}
            void Gear(bool maximized)
            {
                Equip(new[]{ItemType.SolarFlareHelmet,ItemType.SolarFlareBreastplate,ItemType.SolarFlareLeggings},
                    new[]{ItemType.FireGauntlet,ItemType.FrozenShield,ItemType.WarriorEmblem,ItemType.CrossNecklace,ItemType.CobaltShield});
                if(maximized)foreach(var slots in new[]{inv.Accessories,inv.EffectiveAccessories})
                    for(int i=0;i<slots.Length;i++)if(!slots[i].IsEmpty)slots[i].Prefix=72; // Menacing, native PrefixID.
                Array.Clear(inv.Slots,0,inv.Slots.Length);inv.Mouse=default;inv.Selected=0;inv.Override=-1;inv.UseMouse=false;
                inv.Slots[0]=ItemStack.Of(ItemType.Zenith);
                if(maximized){inv.Slots[0].Prefix=81;inv.Slots[0].Forge=(byte)Forging.MaxLevelOf(ItemType.Zenith);}
                Equipment.Update(inv);
                mod.Stats.LifeCrystals=15;mod.Stats.LifeFruits=20;mod.Stats.UpdateMaximums(pd,Equipment.Current.ManaMax);mod.Stats.FullHeal();
            }
            try
            {
                Shop.DevStage=5;GameDifficulty.Current=DifficultyLevel.Master;
                foreach(int stage in Enumerable.Range(0,5))
                {VanillaBossProgression.DevClears=new HashSet<string>(VanillaBossProgression.All.Keys);Check("early stage unchanged "+stage,BossHealthBudgets.GodhomeProgressShare(stage)==1);}
                var progression=new List<object>();
                foreach(var keys in new[]{Array.Empty<string>(),new[]{"HollowKnight"},new[]{"HollowKnight","NailBros"},new[]{"FinalBoss"}})
                {VanillaBossProgression.DevClears=new HashSet<string>(keys);progression.Add(new {Keys=keys,Step=VanillaBossProgression.Current.ToString(),Multiplier=BossHealthBudgets.GodhomeProgressShare(5)});}
                VanillaBossProgression.DevClears=new HashSet<string>(VanillaBossProgression.All.Keys);
                Check("post moon budget",BossHealthBudgets.GodhomeProgressShare(5)==2.25f);
                Check("stage five share retained",BossHealthBudgets.StageShare(5)==.54f);
                Check("world life retained",BossStats.LifeOf(BossStats.Of("FinalBoss",5),"Dream_Final_Boss",DifficultyLevel.Master)==200000);
                Check("fifth door no life discount",GodhomeBalance.Pantheons[4].Life>=GodhomeBalance.Hall[0].Life);
                Check("single hit retained",ItemDb.Get(ItemType.Zenith).Damage>=190);
                // Two real moving bosses under the native fifth-door controller. A one-fight copy
                // shortens the sequence; no boss FSM, native HP, arena or invulnerability is edited.
                foreach(var sample in new[]{(Scene:"GG_Gruz_Mother_V",Record:"BigFly",Maximized:false),(Scene:"GG_Hollow_Knight",Record:"HollowKnightPrime",Maximized:true)})
                {
                    mod.DevAim=null;mod.DevHold=null;Minions.Instance.Clear();TerraProjectiles.Instance.DevClear();ZenithSwords.Instance?.DevClear();
                    BossSequenceController.Reset();BossSceneController.SetupEvent=null;
                    TerraStats.DevImmortal=true;pd.isInvincible=true;mod.DevNoFallDamage=true;inv.Infinite=false;
                    yield return GoTo("Town","left1");Gear(sample.Maximized);
                    var original=Resources.LoadAll<BossSequence>("GG").First(s=>s.name=="Boss Sequence Tier 5");
                    var scene=Enumerable.Range(0,original.Count).Select(original.GetBossScene).First(s=>s.sceneName==sample.Scene);
                    var sequence=UnityEngine.Object.Instantiate(original);sequence.name="Boss Sequence Tier 5";
                    sequence.achievementKey="";sequence.customEndScene="";sequence.useSceneUnlocks=false;
                    AccessTools.Field(typeof(BossSequence),"bossScenes").SetValue(sequence,new[]{scene});
                    BossSequenceController.SetupNewSequence(sequence,BossSequenceController.ChallengeBindings.None,"bossDoorStateTier5");
                    yield return GoTo(sample.Scene,"door_dreamEnter");yield return new WaitForSecondsRealtime(3);
                    Check("fifth door active "+sample.Record,BossSequenceController.IsInSequence&&GodhomeBalance.Current(sample.Scene).Key=="pantheon5");
                    var boss=UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None).FirstOrDefault(h=>h!=null&&UI.EnemyHover.RecordOf(h)==sample.Record&&!h.isDead);
                    Check("boss spawned "+sample.Record,boss!=null);
                    int life=boss!=null?EnemyStats.LifeWhole(EnemyStats.Of(boss)):0;
                    int before=Bestiary.KillsOf(sample.Record);float start=Time.realtimeSinceStartup,firstHit=-1;int casts=0;
                    int nativeBefore=boss!=null?boss.hp:0;float nextLog=start+10;
                    while(Time.realtimeSinceStartup-start<150 && !BossSequenceController.WasCompleted && Bestiary.KillsOf(sample.Record)==before)
                    {
                        pd.isInvincible=true;TerraStats.DevImmortal=true;
                        if(boss!=null&&!boss.isDead&&boss.hp>0&&boss.gameObject.activeInHierarchy)
                        {
                            var col=boss.GetComponent<Collider2D>()??boss.GetComponentInChildren<Collider2D>();
                            if(col!=null)
                            {
                                mod.DevAim=col.bounds.center;
                                if(GameHooks.PlayerHasControl()&&mod.DevReady){mod.DevUseSelected(col.bounds.center);casts++;}
                            }
                            if(firstHit<0&&boss.hp<nativeBefore)firstHit=Time.realtimeSinceStartup;
                        }
                        if(Time.realtimeSinceStartup>=nextLog){nextLog+=10;Plugin.Log.LogInfo($"[AutoTest] GODHOME_OPT fight {sample.Record} elapsed={Time.realtimeSinceStartup-start:F1} nativeHp={boss?.hp} casts={casts}");}
                        yield return null;
                    }
                    float elapsed=Time.realtimeSinceStartup-start;
                    bool won=BossSequenceController.WasCompleted||Bestiary.KillsOf(sample.Record)>before;
                    fights.Add(new {sample.Scene,sample.Record,sample.Maximized,Life=life,Seconds=elapsed,FirstHit=firstHit<0?(float?)null:firstHit-start,Won=won,Casts=casts,
                        Weapon=inv.Slots[0].Type.ToString(),inv.Slots[0].Prefix,inv.Slots[0].Forge,PlayerInvincible=true,AutomatedAim=true,NativeMovingBoss=true});
                    Check("native moving fight "+sample.Record,won&&life>0&&casts>0,$"life={life} seconds={elapsed:F1} maximized={sample.Maximized}");
                    mod.DevAim=null;ZenithSwords.Instance?.DevClear();BossSequenceController.Reset();
                }
                // Native Radiant must retain its one-hit death after converting masks to Terraria health.
                yield return GoTo("Town","left1");BossSequenceController.Reset();
                BossSceneController.SetupEvent=self=>{self.BossLevel=2;self.DreamReturnEvent="DREAM RETURN";};pd.dreamReturnScene="Town";
                yield return GoTo("GG_Gruz_Mother","door_dreamEnter");yield return new WaitForSecondsRealtime(3);
                Gear(false);Array.Clear(inv.Accessories,0,inv.Accessories.Length);Equipment.Update(inv);mod.Stats.FullHeal();mod.Stats.Shield=100;
                var source=UnityEngine.Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None).FirstOrDefault(h=>UI.EnemyHover.RecordOf(h)=="BigFly"&&!h.isDead);
                var hero=HeroController.instance;bool attempted=false;int oldLife=mod.Stats.Life;
                for(float until=Time.realtimeSinceStartup+8;Time.realtimeSinceStartup<until && !attempted;)
                {
                    pd.isInvincible=false;TerraStats.DevImmortal=false;hero.cState.invulnerable=false;
                    if(source!=null&&(bool)AccessTools.Method(typeof(HeroController),"CanTakeDamage").Invoke(hero,null))
                    {attempted=true;hero.TakeDamage(source.gameObject,GlobalEnums.CollisionSide.left,1,1);}
                    else yield return null;
                }
                Check("radiant actual landed hit kills",attempted&&BossSceneController.Instance!=null&&BossSceneController.Instance.BossLevel==2&&mod.Stats.IsDead,$"attempted={attempted} life={oldLife}->{mod.Stats.Life} shield={mod.Stats.ShieldShown}");
                File.WriteAllText(Path.Combine(DevCopy.SaveDir,"godhome-optimize.json"),JsonConvert.SerializeObject(new {Failures=failed,Progression=progression,Results=results,Fights=fights,FullPantheonRun=false},Formatting.Indented));
                Plugin.Log.LogInfo($"[AutoTest] GODHOME_OPT total failed={failed} {(failed==0?"通过":"不通过")}");
            }
            finally
            {
                mod.DevAim=null;mod.DevHold=null;mod.DevNoFallDamage=false;TerraStats.DevImmortal=false;pd.isInvincible=false;
                BossSequenceController.Reset();BossSceneController.SetupEvent=null;VanillaBossProgression.DevClears=savedClears;Shop.DevStage=savedStage;GameDifficulty.Current=savedDifficulty;
            }
        }
    }
}
