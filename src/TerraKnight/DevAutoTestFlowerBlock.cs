using System;
using System.Collections;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.Player;
using TerraKnight.World;
using UnityEngine;
namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator FlowerBlockTest()
        {
            if(!DevCopy.Verified)throw new InvalidOperationException("Isolated save required");
            yield return GoTo("Town",EntryGateOf("Town"));yield return new WaitForSecondsRealtime(1);
            int checks=0,failures=0;
            void Check(string name,bool pass){checks++;if(!pass)failures++;Plugin.Log.LogInfo($"[AutoTest] FLOWER_TRAVEL {name} {(pass?"通过":"不通过")}");}
            var pd=PlayerData.instance;var inv=mod.Inventory;
            bool has=pd.hasXunFlower,broken=pd.xunFlowerBroken,immortal=pd.isInvincible,noFall=mod.DevNoFallDamage;
            string home=pd.respawnScene,marker=pd.respawnMarkerName;int type=pd.respawnType,stage=Shop.DevStage;
            var bag=(ItemStack[])inv.HkBag.Clone();var slots=(ItemStack[])inv.Slots.Clone();var mouse=inv.Mouse;
            var pylons=Pylons.Unlocked.ToArray();
            void Fresh(){pd.hasXunFlower=false;pd.xunFlowerBroken=false;HkItems.Tick(mod);pd.hasXunFlower=true;HkItems.Tick(mod);}
            int Count()=>inv.HkBag.Concat(inv.Slots).Count(s=>!s.IsEmpty&&s.Type==ItemType.HkDelicateFlower)+(inv.Mouse.Type==ItemType.HkDelicateFlower?1:0);
            IEnumerator Settle(string scene)
            {
                float end=Time.realtimeSinceStartup+45f;
                yield return new WaitForSecondsRealtime(1f);
                while(Time.realtimeSinceStartup<end&&(GameManager.instance.sceneName!=scene||GameManager.instance.IsInSceneTransition||!GameHooks.PlayerHasControl()))yield return null;
                Check("arrived-and-control/"+scene,GameManager.instance.sceneName==scene&&!GameManager.instance.IsInSceneTransition&&GameHooks.PlayerHasControl());
            }
            try
            {
                pd.isInvincible=true;mod.DevNoFallDamage=true;
                pd.hasXunFlower=false;pd.xunFlowerBroken=false;
                Check("without-flower-safe",!DelicateFlower.OnTeleport()&&!pd.xunFlowerBroken);
                Fresh();Check("fresh-flower-item",DelicateFlower.Carried&&Count()==1);
                Check("invalid-wormhole-keeps-flower",!Wormhole.Go(null)&&DelicateFlower.Carried);
                pd.respawnScene="";pd.respawnMarkerName="";
                Check("invalid-recall-keeps-flower",!Pylons.Recall()&&DelicateFlower.Carried);
                Check("invalid-rod-keeps-flower",!TeleportRods.ToPoint(HeroController.instance,new Vector2(-99999,-99999))&&DelicateFlower.Carried);
                Pylons.Teleport(null);Check("invalid-pylon-keeps-flower",DelicateFlower.Carried);
                var hero=HeroController.instance;var region=TerrainSystem.Current.Region;
                Vector2 local=region.Root.InverseTransformPoint(hero.transform.position);
                int before=DelicateFlower.DevBroken;
                Check("wormhole-allows-and-breaks",Wormhole.TravelTo(region.SceneName,local,"送花测试")&&pd.xunFlowerBroken&&!DelicateFlower.Carried&&Count()==0);
                Check("broken-once",!DelicateFlower.OnTeleport()&&DelicateFlower.DevBroken==before+1);
                Fresh();hero=HeroController.instance;
                Check("local-blink-allows-and-breaks",TeleportRods.ToPoint(hero,hero.GetComponent<BoxCollider2D>().bounds.center)&&pd.xunFlowerBroken&&Count()==0);
                Fresh();
                Check("random-potion-allows-and-breaks",PotionBuffs.TeleportRandom(HeroController.instance)&&pd.xunFlowerBroken&&Count()==0);
                pd.respawnScene="Town";pd.respawnMarkerName="RestBench";pd.respawnType=0;
                Fresh();Check("recall-allows-and-breaks",Pylons.Recall()&&pd.xunFlowerBroken&&Count()==0);
                yield return Settle("Town");
                Fresh();Check("return-potion-allows-and-breaks",Wormhole.DrinkReturn()&&pd.xunFlowerBroken&&Count()==0);
                yield return Settle("Town");
                float until=Time.realtimeSinceStartup+10f;while(Wormhole.DevPortal==null&&Time.realtimeSinceStartup<until)yield return null;
                Fresh();var portal=Wormhole.DevPortal;
                Check("return-portal-allows-and-breaks",portal.HasValue&&Wormhole.TryRightClick(portal.Value)&&pd.xunFlowerBroken&&Count()==0);
                yield return Settle("Town");
                Pylons.UnlockAllForCreative();var target=Pylons.Unlocked.First(p=>p.Scene=="Crossroads_30");
                Fresh();Pylons.Teleport(target);Check("pylon-allows-and-breaks",pd.xunFlowerBroken&&Count()==0);
                yield return Settle(target.Scene);
                yield return GoTo("Town",EntryGateOf("Town"));
                Shop.DevStage=5;
                Fresh();inv.Slots[0]=ItemStack.Of(ColosseumTicket.Type,1);inv.Selected=0;inv.Override=-1;
                Check("colosseum-ticket-allows-and-breaks",ColosseumTicket.TryUse(inv)&&pd.xunFlowerBroken&&Count()==0);
                yield return Settle(ColosseumTicket.Scene);
                yield return GoTo("Town",EntryGateOf("Town"));
                Fresh();Check("boss-arena-allows-and-breaks",BossArena.Fight("BigFly",true)&&pd.xunFlowerBroken&&Count()==0);
            }
            finally
            {
                BossArena.AbortEntry("BigFly");Shop.DevStage=stage;
                pd.hasXunFlower=has;pd.xunFlowerBroken=broken;pd.isInvincible=immortal;mod.DevNoFallDamage=noFall;
                pd.respawnScene=home;pd.respawnMarkerName=marker;pd.respawnType=type;
                Array.Copy(bag,inv.HkBag,bag.Length);Array.Copy(slots,inv.Slots,slots.Length);inv.Mouse=mouse;
                Pylons.Unlocked.Clear();Pylons.Unlocked.AddRange(pylons);
            }
            Plugin.Log.LogInfo($"[AutoTest] FLOWER_TRAVEL checks={checks} failures={failures} {(failures==0?"通过":"不通过")}");
        }
    }
}
