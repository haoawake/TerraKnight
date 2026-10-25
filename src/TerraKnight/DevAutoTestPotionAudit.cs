using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using TerraKnight.Items;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator PotionAuditTest()
        {
            if(!DevCopy.Verified)throw new InvalidOperationException("Isolated save required");
            yield return GoTo("Town","left1");yield return new WaitForSecondsRealtime(1f);
            int checks=0,failures=0;
            void Check(string name,bool pass){checks++;if(!pass)failures++;Plugin.Log.LogInfo($"[AutoTest] POTION_AUDIT {name} {(pass?"通过":"不通过")}");}
            var pd=PlayerData.instance;var st=mod.Stats;var inv=mod.Inventory;
            var slots=(ItemStack[])inv.Slots.Clone();var boxes=inv.PotionBox.Select(p=>(ItemStack[])p.Clone()).ToArray();
            var bt=(int[])PlayerBuffs.Type.Clone();var times=(int[])PlayerBuffs.Time.Clone();
            var fields=typeof(TerraStats).GetFields(BindingFlags.Instance|BindingFlags.Public).Where(f=>!f.IsInitOnly).ToArray();
            var stats=fields.Select(f=>f.GetValue(st)).ToArray();
            int masks=pd.maxHealthBase,pieces=pd.heartPieces;bool creative=mod.Creative,infinite=TerraMod.DevAllInfinite,free=inv.Infinite;
            var drink=typeof(TerraMod).GetMethod("Drink",BindingFlags.Instance|BindingFlags.NonPublic);
            bool Drink(ItemType t)=>(bool)drink.Invoke(mod,new object[]{ItemDb.Get(t)});
            void ClearBag(){Array.Clear(inv.Slots,0,inv.Slots.Length);foreach(var page in inv.PotionBox)Array.Clear(page,0,page.Length);PlayerBuffs.Clear();st.PotionSickness=0;st.HoneyTicks=0;}
            try {
                mod.ApplyCreative(false);TerraMod.DevAllInfinite=false;mod.SetInfiniteItems(false);
                foreach(int crystals in new[]{0,7,15})foreach(int fruit in new[]{0,20})foreach(int shards in new[]{0,1,4,16}) {
                    pd.maxHealthBase=5+shards/4;pd.heartPieces=shards%4;
                    var s=new TerraStats{LifeCrystals=crystals,LifeFruits=fruit};s.UpdateMaximums(pd,0);
                    int baseMax=100+20*crystals+5*fruit+10*shards;
                    Check($"base/{crystals}/{fruit}/{shards}",s.LifeMax==baseMax);
                    int life=s.Life;s.LifeForce=true;s.UpdateMaximums(pd,0);
                    Check($"full-20-percent/{crystals}/{fruit}/{shards}",s.LifeMax==baseMax+baseMax/5&&s.Life==life);
                    Check($"cyan-share/{crystals}/{fruit}/{shards}",s.MaskLife==10*shards+baseMax/5-(baseMax-10*shards)/5);
                    s.Heal(9999);s.LifeForce=false;s.UpdateMaximums(pd,0);
                    Check("expiration-clamps",s.Life==baseMax&&s.LifeMax==baseMax);
                }
                pd.maxHealthBase=9;pd.heartPieces=0;st.LifeCrystals=15;st.LifeFruits=20;
                ClearBag();Equipment.Update(inv);st.LifeForce=false;st.UpdateMaximums(pd,0);st.Life=400;
                foreach(var def in ItemDb.All.Where(d=>Containers.IsPotion(d)&&(d.HealLife>0||d.HealMana>0||d.BuffType>0))) {
                    PlayerBuffs.Clear();st.PotionSickness=0;st.HoneyTicks=0;st.Life=1;st.Mana=0;
                    bool used=Drink(def.Type);
                    if(!PotionBuffs.Applicable(def.BuffType)){Check("unsupported-not-consumed/"+def.Type,!used&&!PlayerBuffs.Has(def.BuffType));continue;}
                    Check("drink/"+def.Type,used);
                    if(def.HealLife>0)Check("heal/"+def.Type,st.Life>1&&st.Life<=1+(def.Type==ItemType.StrangeBrew?120:def.HealLife));
                    if(def.HealMana>0)Check("mana/"+def.Type,st.Mana==Math.Min(st.ManaMax,def.HealMana)&&PlayerBuffs.Has(PlayerBuffs.ManaSickness));
                    if(def.PotionSick)Check("sickness/"+def.Type,st.PotionSickness>0);
                    if(def.BuffType>0)Check("buff/"+def.Type,def.BuffType==PlayerBuffs.Honey?st.HoneyTicks>0:PlayerBuffs.Has(def.BuffType));
                }
                ClearBag();st.Life=st.LifeMax;st.Mana=st.ManaMax;
                Check("full-health-refuses-heal",!Drink(ItemType.HealingPotion)&&st.PotionSickness==0);
                Check("full-mana-refuses-mana",!Drink(ItemType.ManaPotion)&&!PlayerBuffs.Has(PlayerBuffs.ManaSickness));
                st.Life=0;Check("dead-refuses-drink",!Drink(ItemType.HealingPotion)&&!Drink(ItemType.IronskinPotion));st.Life=100;
                Check("healing-starts-cooldown",Drink(ItemType.HealingPotion));int healed=st.Life;
                Check("cooldown-blocks-second-heal",!Drink(ItemType.HealingPotion)&&st.Life==healed);
                st.PotionSickness=0;st.Life=1;Drink(ItemType.RestorationPotion);
                Check("restoration-short-cooldown",Math.Abs(st.PotionSickness-45*CharmEffects.SicknessFactor(Equipment.Current.PStone))<.001f);
                PlayerBuffs.Clear();st.Mana=0;Drink(ItemType.ManaPotion);st.Mana=0;Drink(ItemType.ManaPotion);st.Mana=0;Drink(ItemType.ManaPotion);
                Check("mana-sickness-caps",PlayerBuffs.TimeLeft(PlayerBuffs.ManaSickness)==600&&Math.Abs(PlayerBuffs.ManaSicknessPenalty-.5f)<.0001f);
                ClearBag();var foods=ItemDb.All.Where(d=>PotionBuffs.FoodRank(d.BuffType)>0).GroupBy(d=>PotionBuffs.FoodRank(d.BuffType)).OrderBy(g=>g.Key).Select(g=>g.First()).ToArray();
                Check("three-food-tiers",foods.Length==3);
                inv.Slots[0]=ItemStack.Of(foods[0].Type,3);inv.Slots[1]=ItemStack.Of(ItemType.FlaskofFire,3);inv.Slots[2]=ItemStack.Of(ItemType.FlaskofIchor,3);
                inv.Slots[3]=ItemStack.Of(ItemType.PotionBox);inv.Slots[4]=ItemStack.Of(ItemType.LuckPotionLesser,3);
                inv.PotionBox[0][0]=ItemStack.Of(foods[2].Type,3);inv.PotionBox[0][1]=ItemStack.Of(ItemType.LuckPotionGreater,3);
                Check("quick-buff-one-per-group",mod.QuickBuff()==3);
                Check("best-food-across-box",PlayerBuffs.Has(foods[2].BuffType)&&inv.Slots[0].Count==3&&inv.PotionBox[0][0].Count==2);
                Check("one-imbue-consumed",PlayerBuffs.Has(74)&&!PlayerBuffs.Has(76)&&inv.Slots[1].Count==2&&inv.Slots[2].Count==3);
                Check("best-luck-across-box",PlayerBuffs.TimeLeft(PotionBuffs.Lucky)==ItemDb.Get(ItemType.LuckPotionGreater).BuffTime&&inv.Slots[4].Count==3&&inv.PotionBox[0][1].Count==2);
                Check("repeat-quick-buff-no-waste",mod.QuickBuff()==0);
                Check("manual-flask-switch",Drink(ItemType.FlaskofIchor)&&!PlayerBuffs.Has(74)&&PlayerBuffs.Has(76));
                ClearBag();inv.Slots[0]=ItemStack.Of(ItemType.HealingPotion,3);inv.Slots[1]=ItemStack.Of(ItemType.PotionBox);
                inv.PotionBox[0][0]=ItemStack.Of(ItemType.SuperHealingPotion,3);st.Life=1;
                var quick=typeof(TerraMod).GetMethod("QuickUse",BindingFlags.Instance|BindingFlags.NonPublic);
                quick.Invoke(mod,new object[]{(Func<ItemDef,bool>)(d=>d.HealLife>0)});
                Check("quick-heal-selects-box",st.Life==201&&inv.Slots[0].Count==3&&inv.PotionBox[0][0].Count==2);
                quick.Invoke(mod,new object[]{(Func<ItemDef,bool>)(d=>d.HealLife>0)});
                Check("quick-heal-sickness-keeps-items",inv.PotionBox[0][0].Count==2&&inv.Slots[0].Count==3);
                PlayerBuffs.Clear();st.PotionSickness=0;st.Mana=0;inv.PotionBox[0][0]=ItemStack.Of(ItemType.ManaPotion,3);
                quick.Invoke(mod,new object[]{(Func<ItemDef,bool>)(d=>d.HealMana>0)});
                Check("quick-mana-selects-box",st.Mana==st.ManaMax&&inv.PotionBox[0][0].Count==2);
                quick.Invoke(mod,new object[]{(Func<ItemDef,bool>)(d=>d.HealMana>0)});
                Check("quick-mana-full-keeps-item",inv.PotionBox[0][0].Count==2);
                ClearBag();inv.Slots[0]=ItemStack.Of(ItemType.IronskinPotion,3);
                for(int i=0;i<PlayerBuffs.MaxBuffs;i++){PlayerBuffs.Type[i]=PotionBuffs.Swiftness;PlayerBuffs.Time[i]=600;}
                Check("full-buff-slots-no-eviction",mod.QuickBuff()==0&&inv.Slots[0].Count==3&&PlayerBuffs.Type.All(t=>t==PotionBuffs.Swiftness));
                ClearBag();for(int i=0;i<PlayerBuffs.MaxBuffs;i++){PlayerBuffs.Type[i]=PlayerBuffs.PotionSickness;PlayerBuffs.Time[i]=600;}
                Check("full-debuff-slots-no-waste",!Drink(ItemType.IronskinPotion));
                ClearBag();st.LifeForce=false;st.UpdateMaximums(pd,0);st.Life=400;Drink(ItemType.LifeforcePotion);
                yield return new WaitForSecondsRealtime(.2f);
                Check("live-lifeforce-includes-masks",st.LifeMax==792&&st.MaskLife==192);
                st.Heal(9999);Shot("Town","potion_lifeforce_cyan");yield return new WaitForSecondsRealtime(.3f);
                PlayerBuffs.Remove(PotionBuffs.Lifeforce);yield return null;yield return null;
                Check("live-removal-clamps",st.LifeMax==660&&st.MaskLife==160&&st.Life<=660);
                yield return PotionEffectsAudit();
                PlayerBuffs.Clear();
                yield return GoTo("Crossroads_01",EntryGateOf("Crossroads_01"));
                yield return new WaitForSecondsRealtime(1f);
                inv.UseMouse=false;inv.Override=-1;inv.Selected=0;
                string recallFrom=GameManager.instance.sceneName,recallTo=pd.respawnScene;
                if(pd.respawnType!=1&&pd.respawnMarkerName=="Death Respawn Marker"&&(recallTo=="Town"||recallTo=="Tutorial_01"))recallTo="Tutorial_01";
                inv.Slots[0]=ItemStack.Of(ItemType.RecallPotion,2);
                mod.DevUseSelected(HeroController.instance.transform.position);
                yield return new WaitForSecondsRealtime(1f);
                float recallUntil=Time.realtimeSinceStartup+40f;
                while(Time.realtimeSinceStartup<recallUntil&&(GameManager.instance.IsInSceneTransition||GameManager.instance.sceneName!=recallTo||!GameHooks.PlayerHasControl()))yield return null;
                Check("recall-travel-consumes",GameManager.instance.sceneName==recallTo&&recallTo!=recallFrom&&inv.Slots[0].Count==1);
                yield return GoTo("Crossroads_01",EntryGateOf("Crossroads_01"));
                yield return new WaitForSecondsRealtime(1f);
                yield return Potions17();

            }finally{
                PlayerBuffs.Clear();Array.Copy(bt,PlayerBuffs.Type,bt.Length);Array.Copy(times,PlayerBuffs.Time,times.Length);
                Array.Copy(slots,inv.Slots,slots.Length);for(int i=0;i<boxes.Length;i++)Array.Copy(boxes[i],inv.PotionBox[i],boxes[i].Length);
                pd.maxHealthBase=masks;pd.heartPieces=pieces;for(int i=0;i<fields.Length;i++)fields[i].SetValue(st,stats[i]);
                mod.ApplyCreative(creative);TerraMod.DevAllInfinite=infinite;mod.SetInfiniteItems(free);Equipment.Update(inv);
            }
            Plugin.Log.LogInfo($"[AutoTest] POTION_AUDIT {checks} checks {failures} failures {(failures==0?"通过":"不通过")}");
        }
    }
}
