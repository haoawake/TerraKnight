using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;
using UnityEngine.SceneManagement;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator Optimization25Test()
        {
            if(!DevCopy.Verified)throw new InvalidOperationException("Isolated save required");
            yield return GoTo("Town","left1");yield return new WaitForSecondsRealtime(1);
            int checks=0,failures=0;
            void Check(string name,bool pass,string detail=""){checks++;if(!pass)failures++;Plugin.Log.LogInfo($"[AutoTest] OPTIMIZE25 {name} {detail} {(pass?"通过":"不通过")}");}
            var inv=mod.Inventory;var hero=HeroController.instance;
            var slots=(ItemStack[])inv.Slots.Clone();var bank=(ItemStack[])inv.Bank.Clone();
            var d=GameDifficulty.Current;var bindings=PlayerChallenges.DevBindings;var resting=PlayerChallenges.DevResting;
            int oldLife=mod.Stats.Life,oldMax=mod.Stats.LifeMax;
            var budget=typeof(ArmorEffects).GetField("lifeSteal",BindingFlags.NonPublic|BindingFlags.Static);
            float oldBudget=(float)budget.GetValue(null);
            bool invincible=PlayerData.instance.isInvincible;
            try
            {
                PlayerData.instance.isInvincible=true;
                var box=hero.GetComponent<BoxCollider2D>().bounds;
                bool placed=Stations.PiggyBankWithin(box.center,8*CellGrid.CellSize);
                foreach(float dx in new[]{1.5f,-1.5f,2.5f,-2.5f})
                    if(!placed)placed=Stations.TryPlace(StationKind.PiggyBank,hero,new Vector2(box.center.x+dx,box.min.y+.2f));
                Check("bank-placed",placed);
                var sample=new Inventory();sample.Slots[Inventory.CoinSlot]=ItemStack.Of(ItemType.Geo,500);sample.Bank[0]=ItemStack.Of(ItemType.Geo,100);
                sample.Slots[1]=ItemStack.Of(ItemType.StoneBlock,20);sample.Slots[2]=ItemStack.Of(ItemType.DreamEssence,7);
                Check("bank-near",Chests.AutoDepositCoins(sample,box.center)>0&&sample.Slots[Inventory.CoinSlot].IsEmpty&&Currencies.Banked(sample,ItemType.Geo)==600);
                Check("bank-items-untouched",sample.Slots[1].Count==20&&sample.Slots[2].Count==7);
                sample.Slots[0]=ItemStack.Of(ItemType.Geo,17);sample.Slots[0].Favorite=true;
                Check("bank-favorite",Chests.AutoDepositCoins(sample,box.center)==0&&sample.Slots[0].Count==17);
                sample.Slots[0].Favorite=false;
                Check("bank-far",Chests.AutoDepositCoins(sample,(Vector2)box.center+Vector2.right*1000)==0&&sample.Slots[0].Count==17);
                for(int i=0;i<sample.Bank.Length;i++)sample.Bank[i]=ItemStack.Of(ItemType.StoneBlock,9999);
                sample.Bank[0]=ItemStack.Of(ItemType.Geo,int.MaxValue-2);
                long total=Currencies.Held(sample,ItemType.Geo)+Currencies.Banked(sample,ItemType.Geo);
                Chests.AutoDepositCoins(sample,box.center);
                Check("bank-full-no-loss",sample.Slots[0].Count==15&&sample.Bank[0].Count==int.MaxValue&&total==Currencies.Held(sample,ItemType.Geo)+Currencies.Banked(sample,ItemType.Geo));
                Array.Clear(inv.Slots,0,inv.Slots.Length);Array.Clear(inv.Bank,0,inv.Bank.Length);
                inv.Slots[Inventory.CoinSlot]=ItemStack.Of(ItemType.Geo,500);inv.Bank[0]=ItemStack.Of(ItemType.Geo,100);
                yield return new WaitForSecondsRealtime(1f);
                Check("bank-live-automatic",Currencies.Held(inv,ItemType.Geo)==0&&Currencies.Banked(inv,ItemType.Geo)==600&&PlayerData.instance.geo==600);
                PlayerChallenges.DevBindings=BossSequenceController.ChallengeBindings.None;PlayerChallenges.DevResting=false;
                VampireHealing.DevReset();budget.SetValue(null,70f);
                float now=Time.time;
                int requested=0;for(int i=0;i<100;i++)requested+=VampireHealing.Reserve(1000000,now);
                Check("knife-reservation-cap",requested==20);
                Check("knife-sliding-window",VampireHealing.Reserve(10000,now+.999f)==0&&VampireHealing.Reserve(10000,now+1.001f)==5);
                VampireHealing.DevReset();budget.SetValue(null,2f);
                Check("knife-budget-no-overshoot",VampireHealing.Reserve(10000,now)==2&&(float)budget.GetValue(null)==0f);
                VampireHealing.DevReset();mod.Stats.LifeMax=500;mod.Stats.Life=1;
                int healed=0;for(int i=0;i<100;i++)healed+=VampireHealing.Heal(500,now);
                Check("knife-delivery-cap",healed==20&&mod.Stats.Life==21);
                Check("knife-hit-cap",VampireHealing.Heal(500,now+1.01f)==5);
                PlayerChallenges.DevBindings=BossSequenceController.ChallengeBindings.Soul;
                Check("knife-recovery-binding",VampireHealing.Heal(500,now+2f)==0&&VampireHealing.Reserve(500,now+2f)==0);
                PlayerChallenges.DevBindings=BossSequenceController.ChallengeBindings.None;
                GameDifficulty.Current=DifficultyLevel.Master;
                foreach(var row in new[]{(Record:"FalseKnight",Scene:"Dream_01_False_Knight",Whole:18000),(Record:"MageLord",Scene:"Dream_02_Mage_Lord",Whole:30000),(Record:"InfectedKnight",Scene:"Dream_03_Infected_Knight",Whole:75000)})
                {
                    var reference=BossStats.Of(row.Record,EnemyDrops.VariantLevel(row.Scene));
                    float totalLife=BossStats.LifeOf(reference,row.Scene,DifficultyLevel.Master)*BossBalance.Of(row.Record,5).Life*BossPhases.WholeFactor(row.Record,row.Scene);
                    Check("dream-budget/"+row.Record,reference.Key==row.Record+"@5"&&Mathf.Abs(totalLife-row.Whole)<5,$"whole={totalLife:0}");
                }
                foreach(var row in new[]{(Record:"FalseKnight",Scene:"GG_Failed_Champion",Budget:160000),(Record:"MageLord",Scene:"GG_Soul_Tyrant",Budget:180000),(Record:"InfectedKnight",Scene:"GG_Lost_Kin",Budget:200000)})
                {
                    var reference=BossStats.Of(row.Record,EnemyDrops.VariantLevel(row.Scene));
                    Check("godhome-variant-budget/"+row.Record,BossHealthBudgets.TryTotal(reference,row.Scene,DifficultyLevel.Master,out float totalLife)&&Mathf.Abs(totalLife-row.Budget)<1);
                }
                Check("waking-budget-retained",BossHealthBudgets.WorldMasterBudget("FalseKnight")==3000&&BossHealthBudgets.WorldMasterBudget("MageLord")==9000&&BossHealthBudgets.WorldMasterBudget("InfectedKnight")==15500);
                foreach(string name in new[]{"Dream_01_False_Knight","Dream_02_Mage_Lord","Dream_03_Infected_Knight","Dream_04_White_Defender","Dream_Mighty_Zote","Grimm_Nightmare"})
                {
                    var scene=USM.GetSceneByName(name);bool loaded=!scene.isLoaded;
                    if(loaded){var op=USM.LoadSceneAsync(name,LoadSceneMode.Additive);while(op!=null&&!op.isDone)yield return null;scene=USM.GetSceneByName(name);}
                    yield return null;
                    int found=0;
                    foreach(var root in scene.GetRootGameObjects())foreach(var hm in root.GetComponentsInChildren<HealthManager>(true))
                    {
                        var info=EnemyStats.Of(hm);if(info.Ref==null)continue;
                        found++;
                        Check("dream-native/"+name+"/"+hm.name,info.Scene==name&&EnemyStats.LifeMax(info)>0,$"key={info.Ref.Key} pool={EnemyStats.LifeMax(info)} whole={EnemyStats.LifeWhole(info)}");
                    }
                    Check("dream-native-found/"+name,found>0);
                    if(loaded){foreach(var root in scene.GetRootGameObjects())root.SetActive(false);var op=USM.UnloadSceneAsync(scene);while(op!=null&&!op.isDone)yield return null;}
                }
            }
            finally
            {
                Array.Copy(slots,inv.Slots,slots.Length);Array.Copy(bank,inv.Bank,bank.Length);
                GameDifficulty.Current=d;PlayerChallenges.DevBindings=bindings;PlayerChallenges.DevResting=resting;
                mod.Stats.LifeMax=oldMax;mod.Stats.Life=oldLife;PlayerData.instance.isInvincible=invincible;
                budget.SetValue(null,oldBudget);VampireHealing.DevReset();
            }
            Plugin.Log.LogInfo($"[AutoTest] OPTIMIZE25 checks={checks} failures={failures} {(failures==0?"通过":"不通过")}");
        }
    }
}
