using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        IEnumerator EconomyProgressionTest()
        {
            if(!DevCopy.Verified)throw new InvalidOperationException("Isolated saves required");
            int checks=0,failed=0;void Check(string name,bool pass){checks++;if(!pass)failed++;Plugin.Log.LogInfo($"[AutoTest] Economy {name} {(pass?"PASS":"FAIL 不通过")}");}
            var weapons=new List<object>();var prices=new List<object>();var live=new List<object>();
            foreach(var d in ItemDb.All.Where(d=>d!=null&&d.Type!=ItemType.None))
            {
                long oldPrice=Shop.ResalePriceOf(d),price=Shop.PriceOf(d);
                Check("price range "+d.Type,price>=0&&price<=int.MaxValue);
                if(!d.HkItem&&oldPrice>0&&d.Type!=ItemType.WaterCandle&&d.Type!=ItemType.RandomPrefabHouse&&d.Type!=ItemType.SimplePrefabHouse)
                    Check("purchase increase "+d.Type,price>=oldPrice*5);
                if(d.IsAmmo&&oldPrice>0)Check("ammo minimum "+d.Type,price>=100);
                var stack=ItemStack.Of(d.Type);
                if(!d.HkItem&&!HallownestRecovery.IsUnique(d.Type)&&d.Value>0&&!Recipes.Worthless(d.Type))
                    Check("equipment trade-in or preserved liquidation "+d.Type,Shop.SellValue(stack)==Math.Max(1L,(Shop.ShopEquipmentResale(d)?price:oldPrice)/5));
                prices.Add(new {Type=d.Type.ToString(),d.Name,d.Stage,Before=oldPrice,After=price,Sale=Shop.SellValue(stack),d.IsAmmo,d.IsArmor,d.IsAccessory});
                if(!ProgressionBalance.BalancedWeapon(d))continue;
                Check("positive weapon damage "+d.Type,d.Damage>=3);
                ProgressionBalance.OriginalWeapon(d,out int native,out int rare);
                if(ProgressionBalance.TimberWeapon(d.Type))Check("wood cap "+d.Type,d.Damage<=native*2);
                if(d.Stage>=4&&rare>=10&&!ProgressionBalance.TimberWeapon(d.Type))Check("endgame native floor "+d.Type,d.Damage>=native);
                weapons.Add(new {Type=d.Type.ToString(),d.Name,d.Stage,Class=d.Class.ToString(),Before=ProgressionBalance.DamageBeforeProgression[d.Type],After=d.Damage,Native=native,Rarity=rare,d.UseTime,d.UseAnimation,Output=ProgressionBalance.OutputOf(d)});
            }
            for(int stage=0;stage<6;stage++)foreach(var stock in TownNpcs.DevSourcesAt(stage))
                Check("shop stock priced "+stage+"/"+stock.Npc+"/"+stock.Type,Shop.PriceOf(ItemDb.Get(stock.Type))>=100);
            Check("wood bow below metal bow",ItemDb.Get(ItemType.WoodenBow).Damage<ItemDb.Get(ItemType.IronBow).Damage);
            Check("zenith has final weapon hit",ItemDb.Get(ItemType.Zenith).Damage>=190&&ItemDb.Get(ItemType.Zenith).Damage>=ItemDb.Get(ItemType.WoodenBow).Damage*10);
            Check("fixed requested prices",Shop.PriceOf(ItemDb.Get(ItemType.SimplePrefabHouse))==50000&&Shop.PriceOf(ItemDb.Get(ItemType.RandomPrefabHouse))==100000&&Shop.PriceOf(ItemDb.Get(ItemType.WaterCandle))==300000);
            Check("20000 bullets require at least 20000 geo",Shop.PriceOf(ItemDb.Get(ItemType.MusketBall))*20000>=2000000);
            yield return GoTo("Town","left1");yield return new WaitForSeconds(3);
            var hero=HeroController.instance;var inv=mod.Inventory;var body=hero.GetComponent<Rigidbody2D>();
            var saved=(ItemStack[])inv.Slots.Clone();var armor=(ItemStack[])inv.Armor.Clone();var accessories=(ItemStack[])inv.Accessories.Clone();var mouse=inv.Mouse;
            bool infinite=inv.Infinite;int oldStage=Shop.DevStage;bool auto=Equipment.AutoReuseAll;var difficulty=GameDifficulty.Current;
            var originals=ItemDb.All.Where(d=>d!=null&&ProgressionBalance.BalancedWeapon(d)).ToDictionary(d=>d.Type,d=>d.Damage);
            try
            {
                TerraStats.DevImmortal=true;PlayerData.instance.isInvincible=true;mod.DevNoFallDamage=true;Shop.DevStage=0;TerraMod.DevAllInfinite=false;mod.ApplyCreative(false);inv.Infinite=false;
                if(!TownNpcs.DevPos("Merchant").HasValue){TownNpcs.DevPlace("Merchant");yield return new WaitForSeconds(.5f);}
                var at=TownNpcs.DevPos("Merchant");Check("merchant available",at.HasValue);
                if(at.HasValue)
                {
                    float feetOffset=hero.GetComponent<BoxCollider2D>().bounds.min.y-hero.transform.position.y;
                    hero.transform.position=Clear(hero,new Vector2(at.Value.x-1.3f,at.Value.y-20f/32f-feetOffset+.05f));body.linearVelocity=Vector2.zero;yield return new WaitForSeconds(.3f);
                    bool talked=TownNpcs.TryRightClick(TownNpcs.DevPos("Merchant").Value);TownNpcs.OpenShop();yield return null;
                    Plugin.Log.LogInfo($"[AutoTest] Economy merchant interaction talked={talked} open={TownNpcs.ShopOpen} stock="+string.Join(",",TownNpcs.ShopItems.Where(s=>!s.IsEmpty).Select(s=>s.Type)));
                    int gun=Array.FindIndex(TownNpcs.ShopItems,s=>s.Type==ItemType.FlintlockPistol),ammo=Array.FindIndex(TownNpcs.ShopItems,s=>s.Type==ItemType.MusketBall);
                    Check("merchant gun and ammo available",TownNpcs.ShopOpen&&gun>=0&&ammo>=0);
                    if(gun>=0&&ammo>=0)
                    {
                        Array.Clear(inv.Slots,0,inv.Slots.Length);inv.Mouse=default;Shop.Spend(Shop.Purse);Shop.Earn(TownNpcs.BuyPrice(gun));long before=Shop.Purse;
                        Check("actual gun purchase",TownNpcs.Buy(gun,1,inv)&&inv.Mouse.Type==ItemType.FlintlockPistol&&Shop.Purse==0&&before==Shop.PriceOf(ItemDb.Get(ItemType.FlintlockPistol)));
                        inv.Mouse=default;Check("empty wallet cannot buy ammunition",!TownNpcs.Buy(ammo,1,inv)&&Shop.Purse==0&&inv.Mouse.IsEmpty);
                        Shop.Earn(100000);before=Shop.Purse;bool bought=TownNpcs.Buy(ammo,20000,inv);
                        Check("1000 geo cannot buy 20000 bullets",bought&&inv.Mouse.Type==ItemType.MusketBall&&inv.Mouse.Count<=1000&&before-Shop.Purse==inv.Mouse.Count*TownNpcs.BuyPrice(ammo));
                        TownNpcs.DiscountLatched=true;Check("discount still limits ammo budget",TownNpcs.BuyPrice(ammo)>=80&&TownNpcs.BuyPrice(gun)==Shop.Discounted(Shop.PriceOf(ItemDb.Get(ItemType.FlintlockPistol)),true));TownNpcs.DiscountLatched=false;
                    }
                }
                TownNpcs.Close(true);InventoryGui.Open=false;inv.Infinite=true;Equipment.AutoReuseAll=true;Equip(Array.Empty<ItemType>(),Array.Empty<ItemType>());Equipment.Update(inv);GameDifficulty.Current=DifficultyLevel.Normal;
                for(float until=Time.realtimeSinceStartup+8;TargetDummy.DevAt==null&&Time.realtimeSinceStartup<until;)yield return null;
                Check("dummy exists",TargetDummy.DevAt.HasValue);
                if(TargetDummy.DevAt.HasValue)
                {
                    var dummy=TargetDummy.DevAt.Value;var aim=DummyCentre();var col=hero.GetComponent<BoxCollider2D>();
                    foreach(var t in new[]{ItemType.WoodenBow,ItemType.WoodenSword,ItemType.IronBow,ItemType.FlintlockPistol,ItemType.TerraBlade,ItemType.Zenith,ItemType.SDMG,ItemType.LastPrism,ItemType.Terraprisma})
                    {
                        var d=ItemDb.Get(t);float first=0,last=0;int afterHits=0;
                        foreach(bool before in new[]{true,false})
                        {
                            d.Damage=before?ProgressionBalance.DamageBeforeProgression[t]:originals[t];Shop.DevStage=d.Stage;
                            Minions.Instance.Clear();PlayerBuffs.Clear();SummonTags.Clear();EnemyDebuffs.Clear();TerraProjectiles.Instance.DevClear();Whips.Instance.Clear();SwordFx.Instance?.Clear();ZenithSwords.Instance?.DevClear();
                            Array.Clear(inv.Slots,0,inv.Slots.Length);inv.Mouse=default;inv.Override=-1;inv.UseMouse=false;inv.Selected=0;inv.Slots[0]=ItemStack.Of(t);
                            var bullet=ItemDb.All.Where(a=>a!=null&&a.Ammo!=AmmoKind.None&&a.Ammo==d.UseAmmo&&a.Stage<=d.Stage).OrderBy(a=>a.Stage).ThenBy(a=>a.Damage).FirstOrDefault();
                            if(bullet!=null)inv.Slots[Inventory.Size-1]=ItemStack.Of(bullet.Type,9999);
                            bool melee=d.Class==DamageClass.Melee&&!d.IsWhip;var stand=Clear(hero,dummy+Vector2.left*(melee?1.6f:5f)+Vector2.up*.5f);
                            hero.transform.position=stand;body.linearVelocity=Vector2.zero;hero.FaceRight();EnemyStats.DevDummyDefense=Areas.Defense(d.Stage);
                            mod.Stats.ManaCrystals=TerraStats.MaxManaCrystals;mod.Stats.UpdateMaximums(PlayerData.instance,Equipment.Current.ManaMax);mod.Stats.Mana=mod.Stats.ManaMax;
                            mod.DevHold=d.Channel?true:(bool?)null;mod.DevAim=aim;yield return new WaitForSeconds(.5f);
                            if(d.IsSummon){for(int n=0;n<(d.Sentry?1:Equipment.MaxMinions);n++){while(!mod.DevReady)yield return null;mod.DevUseSelected((Vector2)col.bounds.center+Vector2.up);yield return null;}yield return new WaitForSeconds(1);}
                            UnityEngine.Random.InitState(20261022+(int)t);int d0=TargetDummy.DevDamage,h0=TargetDummy.DevHits;float start=Time.time;float seconds=d.Channel||d.IsSummon?8:6;
                            while(Time.time-start<seconds)
                            {
                                if(((Vector2)hero.transform.position-(Vector2)stand).sqrMagnitude>.25f){hero.transform.position=stand;body.linearVelocity=Vector2.zero;}
                                mod.DevAim=aim;mod.Stats.Mana=mod.Stats.ManaMax;
                                if(!d.IsSummon&&mod.DevReady&&!mod.IsUsingItem)mod.DevUseSelected(aim);
                                yield return null;
                            }
                            mod.DevHold=null;mod.DevAim=null;yield return new WaitForSeconds(.6f);
                            int damage=TargetDummy.DevDamage-d0,hits=TargetDummy.DevHits-h0;float dps=damage/seconds;
                            live.Add(new {Type=t.ToString(),Before=before,d.Damage,Dps=dps,Hits=hits,Defense=EnemyStats.DevDummyDefense,Seconds=seconds});
                            Plugin.Log.LogInfo($"[AutoTest] Economy live {t} before={before} damage={d.Damage} dps={dps:F1} hits={hits}");
                            if(before)first=dps;else{last=dps;afterHits=hits;}
                        }
                        Check("actual attacks hit "+t,afterHits>0&&last>0);
                        if(t==ItemType.WoodenBow)Check("wood bow no longer inflated",last<first*.8f);
                        if(t==ItemType.Zenith)Check("zenith real damage increases",last>first*1.5f);
                    }
                }
            }
            finally
            {
                foreach(var kv in originals)ItemDb.Get(kv.Key).Damage=kv.Value;
                Minions.Instance.Clear();TerraProjectiles.Instance.DevClear();SwordFx.Instance?.Clear();ZenithSwords.Instance?.DevClear();Whips.Instance.Clear();
                mod.DevHold=null;mod.DevAim=null;EnemyStats.DevDummyDefense=0;Shop.DevStage=oldStage;GameDifficulty.Current=difficulty;Equipment.AutoReuseAll=auto;TownNpcs.DiscountLatched=false;TownNpcs.Close(true);
                Array.Copy(saved,inv.Slots,saved.Length);Array.Copy(armor,inv.Armor,armor.Length);Array.Copy(accessories,inv.Accessories,accessories.Length);inv.Mouse=mouse;inv.Infinite=infinite;Equipment.Update(inv);TerraStats.DevImmortal=false;
            }
            File.WriteAllText(Path.Combine(DevCopy.SaveDir,"economy-progression.json"),JsonConvert.SerializeObject(new{Checks=checks,Failures=failed,Weapons=weapons,Prices=prices,Live=live},Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] Economy TOTAL {checks-failed}/{checks} {(failed==0?"PASS":"FAIL 不通过")}");
        }
    }
}
