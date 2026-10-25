using System;
using System.Collections;
using System.Linq;
using Newtonsoft.Json;
using TerraKnight.Items;
using TerraKnight.Net;
using TerraKnight.Town;
using TerraKnight.World;
using UnityEngine;
using Object=UnityEngine.Object;
namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator CasinoTest()
        {
            int ok=0,total=0;void Check(string name,bool pass){total++;if(pass)ok++;Plugin.Log.LogInfo($"[AutoTest] Casino {name} {(pass?"PASS":"FAIL 不通过")}");}
            long sum=0;int wins=0,jackpots=0;
            for(int a=0;a<5;a++)for(int b=0;b<5;b++)for(int c=0;c<5;c++){long p=Casino.SlotPayout(10000,a,b,c);sum+=p;if(p>0)wins++;if(p==93500)jackpots++;}
            Check("125 slots outcomes: RTP 95%, a third back, top 9.35x 4%",sum==1187500&&wins==41&&jackpots==5);
            foreach(var colour in new[]{Casino.Colour.Red,Casino.Colour.Black,Casino.Colour.Green})
            {sum=0;wins=0;for(int n=0;n<65;n++){var p=Casino.WheelPayout(10000,colour,n);sum+=p;if(p>0)wins++;}Check("65 wheel outcomes "+colour,sum==(colour==Casino.Colour.Green?630000:630400)&&wins==(colour==Casino.Colour.Green?1:32));}
            {
                // The marbles' physics: every launch strong enough settles (its family's tenths), the same launches the same again.
                var launches=new System.Collections.Generic.List<(double,uint,double)>();for(int n=0;n<40;n++)launches.Add((.2+.8*(n%10)/9.0,(uint)(n*2654435761u+1),n*1.5));
                var a=PinSim.Replay(launches);var b=PinSim.Replay(launches);
                Check("pinball physics settles every launch, the same again",a.All(c=>c>=0&&c<=PinSim.MostTenths)&&a.SequenceEqual(b));
                // The cup: a long auto-fire run meets it both ways (gold pays, rainbow brings marbles), every family settled.
                {
                    var sim=new PinSim{Clock=3};int gold=0,rain=0,done=0,free=0;
                    for(int s=0;s<200*480&&done<200;s++)
                    {
                        if(s%(int)(.55/PinSim.Dt)==0&&s<200*(int)(.55/PinSim.Dt))sim.Launch(.4+.5*((s/7)%10)/9.0,(uint)(s*2246822519u+3));
                        sim.ClearSeen();sim.Step();
                        foreach(var x in sim.Cupped){if(x.Cupped==PinSim.CupKind.Gold)gold++;else rain++;}
                        foreach(var x in sim.Done){done++;free+=x.Free;}
                    }
                    Plugin.Log.LogInfo($"[AutoTest] CASINO pinball cup: gold {gold} rainbow {rain} free {free} settled {done}");
                    Check("pinball cup gold and rainbow, free marbles settle with their family",gold>0&&rain>0&&free>0&&done==200);
                }
                Check("pinball too soft comes back",PinSim.Replay(new[]{(.02,7u,0.0)})[0]==-1);
                Check("pinball channel pays symmetric",Enumerable.Range(0,Casino.Channels).All(k=>Casino.ChannelTenths[k]==Casino.ChannelTenths[Casino.Channels-1-k]));
            }
            Check("stake presets and custom validation",Casino.Stakes.All(Casino.ValidStake)&&!Casino.ValidStake(101)&&!Casino.ValidStake(9999)&&!Casino.ValidStake(0)&&!Casino.ValidStake(1000001)&&Casino.ValidStake(10001)&&Casino.ValidStake(1000000));
            Check("invalid math input",Casino.SlotPayout(10000,-1,0,0)==0&&Casino.WheelPayout(10000,Casino.Colour.Green,-1)==0&&Casino.ChannelPayout(10000,-1)==0&&Casino.ChannelPayout(10000,Casino.Channels)==0&&Casino.ChannelPayout(10000,0)==50000&&Casino.ChannelPayout(10000,5)==1000&&Casino.TenthsPayout(10000,PinSim.MostTenths+1)==0);
            Player.TerraStats.DevImmortal=true;mod.Stats.FullHeal();mod.DevNoFallDamage=true;PlayerData.instance.isInvincible=true;
            yield return GoTo("Town","left1");yield return new WaitForSeconds(4);
            var hero=HeroController.instance;var rb=hero.GetComponent<Rigidbody2D>();var inv=mod.Inventory;
            TerraMod.DevAllInfinite=false;mod.ApplyCreative(false);inv.Infinite=false;Shop.DevStage=0;
            if(!TownNpcs.DevPos("TaxCollector").HasValue){TownNpcs.DevPlace("TaxCollector");yield return new WaitForSeconds(.5f);}
            var at=TownNpcs.DevPos("TaxCollector");Check("tax collector stage0",at.HasValue);
            if(at.HasValue)
            {
                float offset=hero.GetComponent<BoxCollider2D>().bounds.min.y-hero.transform.position.y;
                hero.transform.position=Clear(hero,new Vector2(at.Value.x-1.3f,at.Value.y-20f/32f-offset+.05f));rb.linearVelocity=Vector2.zero;yield return new WaitForSeconds(.3f);
                bool chat=TownNpcs.TryRightClick(TownNpcs.DevPos("TaxCollector").Value);
                Check("tax service and shop coexist",chat&&TownNpcs.TalkingIsTaxCollector&&TownNpcs.TalkingHasShop);TownNpcs.OpenShop();yield return null;
                int slot=Array.FindIndex(TownNpcs.ShopItems,s=>s.Type==ItemType.CasinoKit);Check("casino on shelf for5000geo",slot>=0&&TownNpcs.BuyPrice(slot)==Casino.HousePrice);
                if(slot>=0){inv.Clear();inv.Mouse=default;Shop.SpareCopper=0;Shop.Earn(Casino.HousePrice);Check("real kit purchase",TownNpcs.Buy(slot,1,inv)&&inv.Mouse.Type==ItemType.CasinoKit&&Shop.Purse==0);inv.Mouse=default;}
            }
            TownNpcs.Close(true);InventoryGui.Open=false;inv.UseMouse=false;inv.Override=-1;inv.Selected=0;inv.Mouse=default;Casino.Clear();
            var original=hero.transform.position;var constraints=rb.constraints;
            var root=new GameObject("Casino Verification Terrain");root.transform.position=new Vector3(1000,1000,0);root.AddComponent<TerraOwned>();
            var holder=new GameObject("Casino Verification System");var fixture=holder.AddComponent<TerrainSystem>();var grid=new CellGrid(80,40);
            const string scene="CasinoFixture";var region=new TerrainRegion(scene,grid,root.transform,false,TerrainSystem.Current.Region.Material,new SpriteRenderer[0]);region.SetOffset(new Vector2Int(2000,2000));
            HarmonyLib.AccessTools.Property(typeof(TerrainSystem),"Region").SetValue(fixture,region,null);HarmonyLib.AccessTools.Property(typeof(TerrainSystem),"SceneName").SetValue(fixture,scene,null);TerrainSystem.All.Add(fixture);
            Player.TerraControls.DevNoRealKeys=true;rb.constraints=RigidbodyConstraints2D.FreezeAll;Stations.DevNoAutoDoors=true;
            var origin=new Vector2Int(2012,2005);
            void Approach(StationKind k){var box=Stations.DevBoxOf(k);if(box.HasValue){hero.transform.position=new Vector3(box.Value.center.x,box.Value.y+1,original.z);Physics2D.SyncTransforms();}}
            bool Open(StationKind k){Approach(k);Casino.Close();var box=Stations.DevBoxOf(k);return box.HasValue&&Stations.TryRightClick(box.Value.center)&&Casino.IsOpen;}
            try
            {
                float feet=hero.GetComponent<BoxCollider2D>().bounds.min.y-hero.transform.position.y;
                hero.transform.position=new Vector3((origin.x+11)*.5f,(origin.y+1.1f)*.5f-feet,original.z);Physics2D.SyncTransforms();
                inv.Clear();inv.Infinite=false;inv.Slots[0]=ItemStack.Of(ItemType.CasinoKit,2);
                bool canBuild=PrefabHouses.CanBuild(Casino.HouseArea,origin,hero,out var why);Plugin.Log.LogInfo("[AutoTest] Casino build reason: "+why);Check("site accepts casino",canBuild);if(!canBuild)yield break;
                mod.DevUseSelected(new Vector2((origin.x+13.2f)*.5f,(origin.y+.2f)*.5f));yield return null;region.Tick();yield return new WaitForSeconds(.2f);
                Check("actual kit use consumes one",inv.Slots[0].Type==ItemType.CasinoKit&&inv.Slots[0].Count==1);
                var furnishings=Stations.Save().Where(s=>s.Scene==scene).ToList();
                Check("casino includes exactly three machines plus furniture",furnishings.Count==7&&furnishings.Count(s=>Casino.IsMachine(s.Kind))==3);
                Check("casino blocks wall platforms",PrefabHouses.Tiles(Casino.HouseArea).All(t=>TerraWorld.Get(origin.x+t.X,origin.y+t.Y)==t.Kind)&&TerraWorld.GetWall(origin.x+12,origin.y+2)==AreaBlocks.WallId(20)&&PrefabHouses.Tiles(Casino.HouseArea).Any(t=>Cells.IsPlatform(t.Kind)));
                var preview=PrefabHouses.Texture("PrefabHouse_25");System.IO.File.WriteAllBytes(System.IO.Path.Combine(outDir,"casino_prefab.png"),preview.EncodeToPNG());Check("casino preview416x192",preview.width==416&&preview.height==192);
                foreach(var k in new[]{StationKind.CasinoSlots,StationKind.CasinoPinball,StationKind.CasinoRoulette})
                {var t=CasinoArt.Texture(Casino.TextureOf(k));Check("pixel furniture sprite "+k,t.width==48&&t.height==64&&t.filterMode==FilterMode.Point&&t.GetPixel(0,63).a==0&&t.GetPixels().Any(c=>c.a>0));System.IO.File.WriteAllBytes(System.IO.Path.Combine(outDir,k+".png"),t.EncodeToPNG());Check("rightclick machine "+k,Open(k));yield return new WaitForSeconds(.5f);
                    // The pinball with marbles flying in it (struck from its window).
                    if(k==StationKind.CasinoPinball){Shop.Earn(300000);Casino.BuyBalls(1,6);for(int n=0;n<6;n++)Casino.LoadMarble(1);for(int n=0;n<6;n++){Casino.DevFire(.35f+.12f*n);yield return new WaitForSeconds(.35f);}yield return new WaitForSeconds(.6f);Shot("Town","casino_PinballFlying");yield return new WaitForSeconds(.9f);}
                    Shot("Town","casino_"+k);Casino.Close();
                    // Its marbles all in before the ledger's own checks start from a clean one.
                    if(k==StationKind.CasinoPinball){yield return new WaitForSeconds(6f);Casino.Clear();}}
                inv.Clear();Shop.SpareCopper=0;Shop.Earn(500000000);Open(StationKind.CasinoSlots);
                long money=Casino.Available;var before=Casino.Snapshot();Casino.DevFailSave=true;
                Check("save failure rolls back cash rng sequence",!Casino.PlaySlots(10000)&&Casino.Available==money&&Casino.Last.RandomState==before.RandomState&&Casino.Last.Sequence==before.Sequence);Casino.DevFailSave=false;
                Check("cash slots settle",Casino.PlaySlots(10000));var last=Casino.Last;
                Check("wallet delta equals payout minus stake",Casino.Available==money-10000+last.LastPayout&&last.Sequence==before.Sequence+1);
                Check("animation blocks double bet",!Casino.PlaySlots(10000)&&Casino.Last.Sequence==last.Sequence);
                CasinoShow.Tick();Check("public slot sprites while dialog closes",CasinoShow.DevWorldSprites>=3);
                Casino.Close();Check("closed machine rejects bets",!Casino.PlaySlots(10000));
                Check("reopen does not settle twice",Open(StationKind.CasinoSlots)&&Casino.Last.Sequence==last.Sequence);
                foreach(ulong seed in new ulong[]{1,2,15})
                {
                    Casino.Close();var vector=Casino.Last;vector.RandomState=seed;Casino.Load(vector);Open(StationKind.CasinoSlots);money=Casino.Available;
                    Check("slot deterministic settlement seed "+seed,Casino.PlaySlots(1000100));var result=Casino.Last;
                    Check("slot payout branch "+seed,result.LastPayout==(seed==15?1500150:0)&&Casino.Available==money-1000100+result.LastPayout);
                }
                Casino.Close();var green=Casino.Last;green.RandomState=18;Casino.Load(green);Open(StationKind.CasinoRoulette);money=Casino.Available;
                Check("green wheel custom stake",Casino.PlayWheel(1234500,Casino.Colour.Green));last=Casino.Last;
                Check("green jackpot cash result",last.LastWheel==0&&last.LastPayout==77773500&&Casino.Available==money-1234500+last.LastPayout&&last.LastGame==(int)Casino.Game.Roulette);
                Casino.Close();Open(StationKind.CasinoPinball);
                for(int i=0;i<6;i++){money=Casino.Available;long balls=Casino.Balls(i);Check("buy denomination "+i,Casino.BuyBalls(i,10)&&Casino.Balls(i)==balls+10&&Casino.Available==money-Casino.Denominations[i]*1000);Check("sell denomination "+i,Casino.SellBalls(i,3)&&Casino.Balls(i)==balls+7&&Casino.Available==money-Casino.Denominations[i]*700);}
                long nballs=Casino.Balls(0),credit0=Casino.Credit;
                Check("pinball loads marbles from the tray",Casino.LoadMarble(0)&&Casino.LoadMarble(0)&&Casino.LoadMarble(0)&&Casino.Balls(0)==nballs-3&&Casino.LoadedCount==3);
                Check("pinball loaded marble taken back",Casino.UnloadMarble(Casino.NextLoaded().Id)&&Casino.Balls(0)==nballs-2&&Casino.LoadedCount==2);
                var struck=Casino.Launch(.6,123.0);Check("pinball strike draws its numbers",struck!=null&&struck.Force==.6&&struck.Seed!=0&&Casino.LoadedCount==1);
                long got=Casino.Settle(struck.Id,80,2,out var fallen);last=Casino.Last;
                Check("pinball settles its family",got==80000&&fallen==struck&&Casino.Credit==credit0+80000&&last.LastWheel==80&&last.LastGame==(int)Casino.Game.Pinball);
                var soft=Casino.Launch(.02,130.0);Casino.Returned(soft.Id);Check("pinball marble back on the plunger",Casino.NextLoaded()==soft&&soft.Force<0);
                // A marble still flying at a save falls when the save loads, where its launch takes it.
                Casino.Close();var flyingSave=Casino.Last;flyingSave.Machine.Clear();flyingSave.Machine.Add(new Casino.Marble{Id=1,Denomination=0,Price=10000,Seed=99,Force=.7,Clock=50});flyingSave.NextMarble=2;
                long creditBefore=flyingSave.Credit;int into=PinSim.Replay(new[]{(.7,99u,50.0)})[0];Casino.Load(flyingSave);
                Check("pinball flight finishes on reload",Casino.Machine.Count==0&&Casino.Credit==creditBefore+Casino.TenthsPayout(10000,into));
                Open(StationKind.CasinoPinball);
                var serialized=JsonConvert.SerializeObject(last);Casino.Close();Casino.Load(JsonConvert.DeserializeObject<Casino.Saved>(serialized));Check("serialized ball rng and outcome persistence",Casino.Balls(0)==last.Balls[0]&&Casino.Last.RandomState==last.RandomState&&Casino.Last.LastWheel==last.LastWheel);
                Open(StationKind.CasinoPinball);before=Casino.Last;money=Casino.Available;Casino.DevFailSave=true;
                Check("ball sale rollback",!Casino.SellBalls(1,1)&&Casino.Balls(1)==before.Balls[1]&&Casino.Available==money);Casino.DevFailSave=false;
                // A full bag keeps winnings as ledger credit without world drops.
                for(int i=0;i<inv.Slots.Length;i++)inv.Slots[i]=ItemStack.Of(ItemType.Gel,9999);before=Casino.Last;
                long drops=WorldItems.Count;Check("full bag sale retained as credit",Casino.SellBalls(1,1)&&Casino.Credit==before.Credit+50000&&WorldItems.Count==drops);
                inv.Slots[0]=default;money=Shop.Purse;long credit=Casino.Credit;Check("cashout after freeing slot",Casino.CollectCredit()&&Casino.Credit==0&&Shop.Purse==money+credit);
                Casino.Close();var large=Casino.Last;large.Credit=2000000125;Casino.Load(large);Open(StationKind.CasinoPinball);inv.Clear();Shop.SpareCopper=0;Check("large exact payout without float rounding",Casino.CollectCredit()&&Shop.Purse==2000000125&&Casino.Credit==0);
                inv.Clear();Shop.SpareCopper=0;Casino.Close();Open(StationKind.CasinoSlots);before=Casino.Last;
                Check("insufficient cash rejects without rng advance",!Casino.PlaySlots(10000)&&Casino.Last.RandomState==before.RandomState&&Casino.Last.Sequence==before.Sequence);
                Casino.Close();var empty=Casino.Last;empty.Balls[5]=0;Casino.Load(empty);Open(StationKind.CasinoPinball);before=Casino.Last;
                Check("no balls rejects without rng advance",!Casino.LoadMarble(5)&&Casino.Last.RandomState==before.RandomState&&Casino.Last.Sequence==before.Sequence);
                // Commit/read the actual isolated mod save, not just JSON copies.
                Shop.Earn(1234500);Casino.BuyBalls(2);before=Casino.Last;money=Shop.Purse;
                Save.ModSave.Write(Save.ModSave.DevStateSlot,mod);Check("atomic save written",Save.ModSave.LastWriteSucceeded);
                Casino.Clear();inv.Clear();Save.ModSave.Read(Save.ModSave.DevStateSlot,mod);yield return null;region.Tick();
                Check("real save reload balls cash rng last result",Casino.Balls(2)==before.Balls[2]&&Casino.Last.RandomState==before.RandomState&&Casino.Last.Sequence==before.Sequence&&Shop.Purse==money);
                Check("machine restores after reload",Stations.Save().Count(s=>s.Scene==scene&&Casino.IsMachine(s.Kind))==3);
                var publicBox=Stations.DevBoxOf(StationKind.CasinoRoulette).Value;var local=new Vector2(publicBox.center.x,publicBox.y)-(Vector2)region.Root.position;
                var publicRound=new CasinoShow.Round{Scene=scene,X=local.x,Y=local.y,Kind=StationKind.CasinoRoulette,Sequence=98765,Stake=10000,Payout=630000,Wheel=0,Choice=2,Multiplier=2};
                Check("public event validates exact jackpot",publicRound.Valid());publicRound.Payout++;Check("public event rejects false payout",!publicRound.Valid());publicRound.Payout--;
                var packet=new Packet(Msg.CasinoRound);packet.W.Write((byte)7);publicRound.Write(packet.W);var body=packet.Done().Skip(2).ToArray();
                before=Casino.Last;money=Casino.Available;int seen=CasinoShow.DevReceived;
                HarmonyLib.AccessTools.Method(typeof(NetSession),"Handle").Invoke(null,new object[]{body});CasinoShow.Tick();
                Check("actual network handler creates public wheel",CasinoShow.DevReceived==seen+1&&CasinoShow.DevWorldSprites>0);
                Check("observer never changes wallet or balls",Casino.Available==money&&JsonConvert.SerializeObject(Casino.Last)==JsonConvert.SerializeObject(before));
                HarmonyLib.AccessTools.Method(typeof(NetSession),"Handle").Invoke(null,new object[]{body});Check("duplicate public event ignored",CasinoShow.DevReceived==seen+1);
                CasinoShow.PlayerLeft(7);publicRound.Sequence=1;var rejoin=new Packet(Msg.CasinoRound);rejoin.W.Write((byte)7);publicRound.Write(rejoin.W);HarmonyLib.AccessTools.Method(typeof(NetSession),"Handle").Invoke(null,new object[]{rejoin.Done().Skip(2).ToArray()});Check("reused player slot accepts new sequence",CasinoShow.DevReceived==seen+2);
                foreach(var kind in new[]{StationKind.CasinoSlots,StationKind.CasinoPinball})
                {
                    var b=Stations.DevBoxOf(kind).Value;var l=new Vector2(b.center.x,b.y)-(Vector2)region.Root.position;
                    var show=new CasinoShow.Round{Scene=scene,X=l.x,Y=l.y,Kind=kind,Sequence=1,Stake=10000,Payout=kind==StationKind.CasinoSlots?Casino.SlotPayout(10000,4,4,4):Casino.ChannelPayout(10000,0),Wheel=0,Reels=new[]{4,4,4}};
                    var p=new Packet(Msg.CasinoRound);p.W.Write((byte)(kind==StationKind.CasinoSlots?8:9));show.Write(p.W);HarmonyLib.AccessTools.Method(typeof(NetSession),"Handle").Invoke(null,new object[]{p.Done().Skip(2).ToArray()});
                }
                CasinoShow.Tick();Check("all three world presentations coexist",CasinoShow.DevWorldSprites>=3);
                // Render the actual built world and its public animated sprites through an isolated camera.
                foreach(var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))renderer.gameObject.layer=30;
                foreach(var k in furnishings.Select(f=>f.Kind).Distinct())
                {var list=new System.Collections.Generic.List<(Rect Box,SpriteRenderer Renderer)>();Stations.StandingOf(k,list);foreach(var s in list)if(s.Renderer!=null)s.Renderer.gameObject.layer=30;}
                foreach(var go in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))if(go.name.StartsWith("Terra Casino Round "))foreach(var sr in go.GetComponentsInChildren<SpriteRenderer>())sr.gameObject.layer=30;
                var cameraObject=new GameObject("Casino world capture");var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=4.4f;camera.nearClipPlane=.01f;camera.farClipPlane=100;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.1f,.16f);camera.cullingMask=1<<30;
                camera.transform.position=new Vector3((origin.x+13)*.5f,(origin.y+6)*.5f,-30);var target=new RenderTexture(1600,900,24);camera.targetTexture=target;camera.Render();
                var previous=RenderTexture.active;RenderTexture.active=target;var rendered=new Texture2D(1600,900,TextureFormat.RGBA32,false);rendered.ReadPixels(new Rect(0,0,1600,900),0,0);rendered.Apply();System.IO.File.WriteAllBytes(System.IO.Path.Combine(outDir,"casino_world.png"),rendered.EncodeToPNG());RenderTexture.active=previous;Object.Destroy(rendered);Object.Destroy(cameraObject);target.Release();Object.Destroy(target);
                CasinoShow.Clear();
                Open(StationKind.CasinoPinball);hero.transform.position+=Vector3.right*30;Casino.Tick();Check("walk away closes",!Casino.IsOpen);
                Open(StationKind.CasinoSlots);var box=Stations.DevBoxOf(StationKind.CasinoSlots).Value;
                var pick=new ItemDef{PickPower=100};Check("machine can be mined",Stations.TryMine(pick,box.center,hero));Casino.Tick();Check("removed machine closes",!Casino.IsOpen&&!Stations.DevBoxOf(StationKind.CasinoSlots).HasValue);
                Check("replace mined machine",Stations.TryPlace(StationKind.CasinoSlots,hero,new Vector2(box.center.x,box.y+.1f)));
                Plugin.Log.LogInfo("[AutoTest] Casino GUI repaint frames: "+Casino.DevDrawFrames+" (hidden-copy screenshots require separate visual review)");
                Plugin.Log.LogInfo($"[AutoTest] Casino TOTAL {ok}/{total} {(ok==total?"PASS":"FAIL 不通过")}");
            }
            finally{Casino.DevFailSave=false;Casino.Close();Stations.Clear();Stations.DevNoAutoDoors=false;TerrainSystem.All.Remove(fixture);Object.Destroy(root);Object.Destroy(holder);rb.constraints=constraints;hero.transform.position=original;Player.TerraControls.DevNoRealKeys=false;}
        }
    }
}
