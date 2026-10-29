using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using TerraKnight.Items;
using TerraKnight.Town;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator PriceAuditTest()
        {
            if(!DevCopy.Verified)throw new InvalidOperationException("Isolated save required");
            mod.ApplyCreative(false);mod.Inventory.FreeMaking=false;mod.Inventory.Infinite=false;
            Player.TerraStats.DevImmortal=true;PlayerData.instance.isInvincible=true;
            var oldClears=VanillaBossProgression.DevClears;int oldStage=Shop.DevStage;var oldDifficulty=GameDifficulty.Current;
            var rows=new List<object>();var stockRows=new List<object>();var income=new List<object>();var arbitrage=new List<object>();var natives=new List<object>();
            var recovery=new List<object>();var live=new List<object>();
            int checks=0,failed=0;
            void Check(string name,bool pass){checks++;if(!pass)failed++;Plugin.Log.LogInfo($"[AutoTest] PRICE_CHECK {name} {(pass?"PASS":"PRICE_FAIL 不通过")}");}
            try
            {
                GameDifficulty.Current=DifficultyLevel.Normal;
                VanillaBossProgression.DevClears=new HashSet<string>(VanillaBossProgression.All.Keys);
                foreach(var d in ItemDb.All)
                    rows.Add(new {Type=d.Type.ToString(),d.Name,d.Stage,Source=d.Source.ToString(),d.Value,d.CannotSell,d.Permanent,d.HkItem,d.IsAmmo,d.IsWeapon,d.IsArmor,d.IsAccessory,d.MaxStack,
                        Buy=Shop.PriceOf(d),DiscountBuy=Shop.Discounted(Shop.PriceOf(d),true),Sell=Shop.SellValue(ItemStack.Of(d.Type)),Recover=HallownestRecovery.IsUnique(d.Type)?HallownestRecovery.Price(d):0});
                for(int stage=0;stage<6;stage++)
                {
                    Shop.DevStage=stage;
                    var stocks=TownNpcs.DevSourcesAt(stage).Where(p=>ProgressionBalance.Unlocked(p.Type,stage,true,true)).ToArray();
                    var buyable=new HashSet<ItemType>(stocks.Select(p=>p.Type));buyable.UnionWith(Shop.Basics.Where(d=>ProgressionBalance.Unlocked(d.Type,stage,true,true)).Select(d=>d.Type));
                    foreach(var t in buyable){var d=ItemDb.Get(t);Check("stock "+stage+"/"+t,Shop.PriceOf(d)>=0&&(!stocks.Any(p=>p.Type==t)||Shop.PriceOf(d)>0)&&Shop.SellValue(ItemStack.Of(t))<=Shop.Discounted(Shop.PriceOf(d),true));}
                    foreach(var p in stocks)stockRows.Add(new {Stage=stage,p.Npc,Type=p.Type.ToString(),Buy=Shop.PriceOf(ItemDb.Get(p.Type))});
                    foreach(var r in Recipes.All.Where(r=>!r.IsBought&&r.Ingredients.Length>0&&r.Ingredients.All(i=>buyable.Contains(i.Type))))
                    {
                        long cost=r.Ingredients.Sum(i=>Shop.PriceOf(ItemDb.Get(i.Type))*i.Count),discount=r.Ingredients.Sum(i=>Shop.Discounted(Shop.PriceOf(ItemDb.Get(i.Type)),true)*i.Count);
                        long sale=Shop.SellValue(ItemStack.Of(r.Result,r.Count));
                        if(sale>discount)arbitrage.Add(new {Stage=stage,Result=r.Result.ToString(),r.Count,r.Station,Cost=cost,DiscountCost=discount,Sale=sale,Ingredients=r.Ingredients.Select(i=>new {Type=i.Type.ToString(),i.Count})});
                    }
                    foreach(var rec in EnemyDrops.DevRecords.Where(r=>!EnemyDrops.IsBoss(r)&&ProgressionLoot.NativeTier(r)<=stage&&Areas.LevelOf(EnemyDrops.SceneOf(r))<=stage))
                    {
                        var drops=EnemyDrops.At(rec,stage,false,true,true,EnemyDrops.SceneOf(rec));
                        double sale=0;foreach(var row in drops)if(!row.Master&&!ItemDb.Get(row.Type).CannotSell)
                            sale+=EnemyDrops.MarginalChance(drops,row)*(row.Min+row.Max)*.5*Shop.SellValue(ItemStack.Of(row.Type))/100.0;
                        income.Add(new {Stage=stage,Record=rec,Scene=EnemyDrops.SceneOf(rec),Geo=EnemyDrops.GeoOf(rec),ExpectedDropSale=sale});
                    }
                    yield return null;
                }
                Check("no purchased-material recipe profit",arbitrage.Count==0);
                Check("fixed prices",Shop.PriceOf(ItemDb.Get(ItemType.CasinoKit))==500000&&Shop.PriceOf(ItemDb.Get(ItemType.RandomPrefabHouse))==100000&&Shop.PriceOf(ItemDb.Get(ItemType.SimplePrefabHouse))==50000&&Shop.PriceOf(ItemDb.Get(ItemType.WaterCandle))==300000);
                Check("bulk ammo budget",Shop.PriceOf(ItemDb.Get(ItemType.MusketBall))*20000>=2000000);
                yield return GoTo("Town","left1");yield return new WaitForSecondsRealtime(1f);
                // Same live native confirmation conversion, both immediately and after the guard window.
                var page=Resources.FindObjectsOfTypeAll<PlayMakerFSM>().FirstOrDefault(f=>f!=null&&f.FsmName=="Dialogue Page Control"&&f.gameObject.name=="Text YN"&&f.gameObject.scene.IsValid());
                if(page!=null)
                {
                    var cost=page.FsmVariables.FindFsmInt("Toll Cost");
                    var convert=page.FsmStates.SelectMany(s=>s.Actions).OfType<HutongGames.PlayMaker.Actions.ConvertIntToString>().FirstOrDefault(c=>c.intVariable!=null&&c.intVariable.Name=="Toll Cost");
                    if(cost!=null&&convert!=null)
                    {
                        int kept=cost.Value;var requester=page.FsmVariables.FindFsmGameObject("Requester");var oldRequester=requester?.Value;if(requester!=null)requester.Value=null;
                        cost.Value=150;convert.OnEnter();int first=cost.Value;convert.OnEnter();int immediate=cost.Value;
                        yield return new WaitForSecondsRealtime(2.2f);convert.OnEnter();int delayed=cost.Value;
                        Check("toll repeated conversion",first==750&&immediate==750&&delayed==750);
                        cost.Value=750;convert.OnEnter();Check("fresh base equals prior output",cost.Value==3750);
                        cost.Value=250;convert.OnEnter();Check("fresh base changed",cost.Value==1250);
                        var bank=new GameObject("Banker price probe");if(requester!=null)requester.Value=bank;
                        cost.Value=150;convert.OnEnter();Check("bank deposit unchanged",requester!=null&&cost.Value==150);
                        UnityEngine.Object.Destroy(bank);if(requester!=null)requester.Value=null;
                        cost.Value=0;convert.OnEnter();Check("zero toll",cost.Value==0);
                        live.Add(new {Test="native-toll-same-conversion-reentry",Base=150,First=first,Immediate=immediate,Delayed=delayed,Condition="direct re-entry of existing native conversion action; normal user flow not established"});
                        cost.Value=kept;if(requester!=null)requester.Value=oldRequester;
                    }
                    else Check("native toll action found",false);
                }
                else Check("native toll FSM found",false);
                // A character who has found an Arcane Egg already: can the recovery shop sell another one, and another?
                Shop.DevStage=5;TownNpcs.DevPlace("GoblinTinkerer");yield return new WaitForSecondsRealtime(.6f);
                var at=TownNpcs.DevPos("GoblinTinkerer");var inv=mod.Inventory;
                if(at.HasValue)
                {
                    var hero=HeroController.instance;float feet=hero.GetComponent<BoxCollider2D>().bounds.min.y-hero.transform.position.y;
                    hero.transform.position=Clear(hero,new Vector2(at.Value.x-1.3f,at.Value.y-20f/32f-feet+.05f));hero.GetComponent<Rigidbody2D>().linearVelocity=Vector2.zero;
                    yield return new WaitForSecondsRealtime(.3f);
                    foreach(var t in new[]{ItemType.HkArcaneEgg,ItemType.HkWanderersJournal,ItemType.HkHallownestSeal,ItemType.HkKingsIdol,ItemType.HkSimpleKey,ItemType.HkPaleOre,ItemType.HkRancidEgg,ItemType.HkCityCrest,ItemType.HkElegantKey,ItemType.HkLoveKey,ItemType.HkShopkeepersKey,ItemType.HkDelicateFlower})
                    {HallownestRecovery.Record(inv,t);Check("consumable not recoverable "+t,!HallownestRecovery.CanBuy(inv,t));}
                    HallownestRecovery.Record(inv,ItemType.HkLumaflyLantern);HallownestRecovery.Record(inv,ItemType.DreamNail);inv.Mouse=default;
                    Check("known tools recoverable",HallownestRecovery.CanBuy(inv,ItemType.HkLumaflyLantern)&&HallownestRecovery.CanBuy(inv,ItemType.DreamNail));
                    Check("unknown character locked",!HallownestRecovery.CanBuy(new Inventory(),ItemType.DreamNail));
                    bool talked=TownNpcs.TryRightClick(TownNpcs.DevPos("GoblinTinkerer").Value);TownNpcs.OpenRecoveryShop();
                    int slot=Array.FindIndex(TownNpcs.ShopItems,s=>s.Type==ItemType.HkArcaneEgg);
                    Check("recovery open without relic stock",talked&&TownNpcs.RecoveryShop&&slot<0);
                    // Also reject a stale stock slot, as well as hiding it from the UI.
                    slot=TownNpcs.ShopItems.Length-1;var keptStock=TownNpcs.ShopItems[slot];TownNpcs.ShopItems[slot]=ItemStack.Of(ItemType.HkArcaneEgg);
                    if(slot>=0)
                    {
                        Shop.Earn(1000000);long before=Shop.Purse;int oldNative=PlayerData.instance.trinket4;
                        long price=TownNpcs.BuyPrice(slot);bool bought=TownNpcs.Buy(slot,2,inv);
                        recovery.Add(new {Type="HkArcaneEgg",Talked=talked,Opened=TownNpcs.RecoveryShop,Price=price,Bought=bought,Count=inv.Mouse.Count,Spent=before-Shop.Purse,NativeBefore=oldNative,NativeAfter=PlayerData.instance.trinket4});
                        Check("stale relic purchase blocked",!bought&&inv.Mouse.IsEmpty&&before==Shop.Purse&&oldNative==PlayerData.instance.trinket4);
                        inv.Mouse=default;
                    }
                    TownNpcs.ShopItems[slot]=keptStock;
                    int lamp=Array.FindIndex(TownNpcs.ShopItems,s=>s.Type==ItemType.HkLumaflyLantern);
                    long lampBefore=Shop.Purse;long lampPrice=lamp>=0?TownNpcs.BuyPrice(lamp):0;
                    Check("actual lost lantern purchase",lamp>=0&&TownNpcs.Buy(lamp,1,inv)&&inv.Mouse.Type==ItemType.HkLumaflyLantern&&lampBefore-Shop.Purse==lampPrice);
                    inv.Mouse=default;
                    foreach(var t in new[]{ItemType.HkWanderersJournal,ItemType.HkHallownestSeal,ItemType.HkKingsIdol,ItemType.HkArcaneEgg})
                    {inv.Mouse=ItemStack.Of(t);long before=Shop.Purse;TownNpcs.Sell(ref inv.Mouse);Check("actual relic sale "+t,inv.Mouse.IsEmpty&&Shop.Purse-before==HallownestRecovery.RelicValue(t));}
                    TownNpcs.OpenShop();TownNpcs.ShopItems[0]=ItemStack.Of(ItemType.FlintlockPistol);TownNpcs.DiscountLatched=true;
                    long gunPrice=TownNpcs.BuyPrice(0),gunBefore=Shop.Purse;
                    Check("actual discounted gun purchase",TownNpcs.Buy(0,1,inv)&&inv.Mouse.Type==ItemType.FlintlockPistol&&gunBefore-Shop.Purse==gunPrice);
                    long refundBefore=Shop.Purse;TownNpcs.Sell(ref inv.Mouse);
                    Check("same-session refund preserved",inv.Mouse.IsEmpty&&Shop.Purse-refundBefore==gunPrice);
                    Check("second gun purchase",TownNpcs.Buy(0,1,inv));
                    TownNpcs.ClearBought();long resaleBefore=Shop.Purse;TownNpcs.Sell(ref inv.Mouse);
                    long resale=Shop.SellValue(ItemStack.Of(ItemType.FlintlockPistol));
                    Check("actual equipment trade-in",inv.Mouse.IsEmpty&&Shop.Purse-resaleBefore==resale&&resale==Shop.PriceOf(ItemDb.Get(ItemType.FlintlockPistol))/5&&resale<gunPrice);
                    TownNpcs.DiscountLatched=false;
                    TownNpcs.Close(true);InventoryGui.Open=false;
                }
                else Check("goblin present",false);
                foreach(var scene in new[]{"Room_shop","Room_mapper","Room_Charm_Shop","Fungus2_26","Ruins1_05b"})
                {
                    yield return GoTo(scene,EntryGateOf(scene));yield return new WaitForSecondsRealtime(.8f);
                    foreach(var item in Resources.FindObjectsOfTypeAll<ShopItemStats>().Where(x=>x!=null&&x.gameObject.scene.IsValid()&&x.gameObject.scene.name==scene))
                    {
                        if(!int.TryParse(TeamCherry.Localization.Language.Get(item.priceConvo,"Prices"),out int vanilla))continue;
                        // Awake reads a fresh language price. Invoke the real native initializer on inactive stock as well.
                        int original=item.cost;HarmonyLib.AccessTools.Method(typeof(ShopItemStats),"Awake").Invoke(item,null);int first=item.cost;
                        HarmonyLib.AccessTools.Method(typeof(ShopItemStats),"Awake").Invoke(item,null);int again=item.cost;
                        Check("native price "+scene+"/"+item.name,first==vanilla*HkShopPrices.Factor&&again==first);
                        natives.Add(new {Scene=scene,Object=item.name,item.playerDataBoolName,item.priceConvo,item.specialType,item.relic,item.relicPDInt,item.dungDiscount,Vanilla=vanilla,Original=original,First=first,Again=again,
                            Expected=vanilla*HkShopPrices.Factor});
                    }
                    Plugin.Log.LogInfo($"[AutoTest] PRICE_AUDIT native {scene} rows={natives.Count}");
                }
                File.WriteAllText(Path.Combine(outDir,"price-audit.json"),JsonConvert.SerializeObject(new {Items=rows,Stocks=stockRows,Income=income,RecipeArbitrage=arbitrage,Native=natives,Recovery=recovery,Live=live},Formatting.Indented));
                Check("native shops covered",natives.Count>=55);
                Plugin.Log.LogInfo($"[AutoTest] PRICE_AUDIT export items={rows.Count} native={natives.Count} recipeArbitrage={arbitrage.Count} checks={checks} failed={failed}");
            }
            finally{VanillaBossProgression.DevClears=oldClears;Shop.DevStage=oldStage;GameDifficulty.Current=oldDifficulty;TownNpcs.Close(true);InventoryGui.Open=false;Player.TerraStats.DevImmortal=false;}
        }
    }
}
