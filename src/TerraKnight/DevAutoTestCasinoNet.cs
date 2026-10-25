using System.Collections;
using System.IO;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.Net;
using TerraKnight.World;
using UnityEngine;
namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator CasinoNetTest(bool hosting)
        {
            int ok=0,total=0;void Check(string name,bool pass){total++;if(pass)ok++;Plugin.Log.LogInfo($"[AutoTest] CasinoNet {name} {(pass?"PASS":"FAIL 不通过")}");}
            Player.TerraStats.DevImmortal=true;mod.DevNoFallDamage=true;PlayerData.instance.isInvincible=true;
            yield return GoTo("Town","left1");yield return new WaitForSeconds(3);
            var hero=HeroController.instance;var inv=mod.Inventory;var rb=hero.GetComponent<Rigidbody2D>();rb.constraints=RigidbodyConstraints2D.FreezeAll;Player.TerraControls.DevNoRealKeys=true;
            TerraMod.DevAllInfinite=false;mod.ApplyCreative(false);inv.Infinite=false;Casino.Clear();
            string common=Directory.GetParent(DevCopy.SaveDir).FullName;
            if(hosting)
            {
                Check("host starts",NetSession.Host(18866,"",4,out var error));
            }
            else
            {
                float until=Time.realtimeSinceStartup+90;while(!NetSession.Active&&Time.realtimeSinceStartup<until)
                {NetSession.Join("127.0.0.1",18866);float attempt=Time.realtimeSinceStartup+5;while(!NetSession.Active&&NetSession.State!=NetSession.Phase.Off&&Time.realtimeSinceStartup<attempt)yield return null;if(!NetSession.Active)yield return new WaitForSecondsRealtime(1);}
            }
            float deadline=Time.realtimeSinceStartup+60;while((!NetSession.Active||!WorldSync.Ready)&&Time.realtimeSinceStartup<deadline)yield return null;
            Check("shared world ready",NetSession.Active&&WorldSync.Ready);
            if(!NetSession.Active||!WorldSync.Ready)yield break;
            if(hosting)
            {
                var region=TerrainSystem.Current.Region;var at=Town.TownNpcs.DevPos("Guide")??(Vector2)hero.transform.position;
                var baseAt=new Vector2(at.x-8,at.y-20f/32f);int n=0;
                foreach(var k in new[]{StationKind.CasinoSlots,StationKind.CasinoPinball,StationKind.CasinoRoulette})
                {
                    var p=baseAt+Vector2.right*n*3;var c=CellGrid.WorldToCell(p+Vector2.down*.05f);
                    for(int x=-2;x<=2;x++)TerraWorld.Set(c.x+x,c.y,AreaBlocks.Kind(20));
                    Stations.PlaceIn(k,"Town",p-(Vector2)region.Root.position);n++;
                }
                yield return new WaitForSecondsRealtime(2);Check("seeded three shared machines",Stations.Standing(Casino.IsMachine).Count==3);
                File.WriteAllText(Path.Combine(common,"host-ready"),"ready");
                deadline=Time.realtimeSinceStartup+90;while(!File.Exists(Path.Combine(common,"client-ready"))&&Time.realtimeSinceStartup<deadline)yield return null;
                Check("observer ready",File.Exists(Path.Combine(common,"client-ready")));
                inv.Clear();Shop.Earn(100000000);
                foreach(var k in new[]{StationKind.CasinoSlots,StationKind.CasinoPinball,StationKind.CasinoRoulette})
                {
                    var box=Stations.DevBoxOf(k);Check("machine exists "+k,box.HasValue);if(!box.HasValue)continue;
                    hero.transform.position=new Vector3(box.Value.center.x,box.Value.y+1,0);Physics2D.SyncTransforms();
                    Check("open machine "+k,Stations.TryRightClick(box.Value.center)&&Casino.IsOpen);
                    bool played=k==StationKind.CasinoSlots?Casino.PlaySlots(10000):k==StationKind.CasinoRoulette?Casino.PlayWheel(50000,Casino.Colour.Green):Casino.BuyBalls(2,2)&&Casino.LoadMarble(2)&&Casino.DevStrike(.6);
                    Check("host bets "+k,played);Casino.Close();
                    yield return new WaitForSecondsRealtime(4);
                }
                File.WriteAllText(Path.Combine(common,"host-rounds-done"),"done");deadline=Time.realtimeSinceStartup+30;while(!File.Exists(Path.Combine(common,"client-done"))&&Time.realtimeSinceStartup<deadline)yield return null;
                Check("observer verified results",File.Exists(Path.Combine(common,"client-done")));
                long money=Casino.Available;var saved=Newtonsoft.Json.JsonConvert.SerializeObject(Casino.Last);int received=CasinoShow.DevReceived,results=CasinoShow.DevResults;int most=0;
                File.WriteAllText(Path.Combine(common,"host-observer-ready"),"ready");deadline=Time.realtimeSinceStartup+60;
                while((!File.Exists(Path.Combine(common,"client-rounds-done"))||CasinoShow.DevResults<results+3)&&Time.realtimeSinceStartup<deadline){most=Mathf.Max(most,CasinoShow.DevWorldSprites);yield return null;}
                Check("guest rounds visible on host",CasinoShow.DevReceived>=received+3&&CasinoShow.DevResults>=results+3&&most>=3);
                Check("host observer wallet and balls untouched",Casino.Available==money&&Newtonsoft.Json.JsonConvert.SerializeObject(Casino.Last)==saved);
                File.WriteAllText(Path.Combine(common,"host-observer-done"),"done");
            }
            else
            {
                deadline=Time.realtimeSinceStartup+90;while(Stations.Standing(Casino.IsMachine).Count<3&&Time.realtimeSinceStartup<deadline)yield return null;
                Check("three actual machines synced",Stations.Standing(Casino.IsMachine).Count==3);
                inv.Clear();Shop.Earn(432100);long money=Casino.Available;var saved=Newtonsoft.Json.JsonConvert.SerializeObject(Casino.Last);
                int received=CasinoShow.DevReceived,results=CasinoShow.DevResults;int mostSprites=0;
                File.WriteAllText(Path.Combine(common,"client-ready"),"ready");
                deadline=Time.realtimeSinceStartup+60;while((!File.Exists(Path.Combine(common,"host-rounds-done"))||CasinoShow.DevResults<results+3)&&Time.realtimeSinceStartup<deadline){mostSprites=Mathf.Max(mostSprites,CasinoShow.DevWorldSprites);yield return null;}
                Check("all three remote rounds received",CasinoShow.DevReceived>=received+3);Check("all three remote results shown",CasinoShow.DevResults>=results+3);
                Check("remote cabinet sprites created",mostSprites>=3);Check("observer wallet and balls untouched",Casino.Available==money&&Newtonsoft.Json.JsonConvert.SerializeObject(Casino.Last)==saved);
                Check("observer not forced into interface",!Casino.IsOpen);
                File.WriteAllText(Path.Combine(common,"client-done"),"done");deadline=Time.realtimeSinceStartup+30;while(!File.Exists(Path.Combine(common,"host-observer-ready"))&&Time.realtimeSinceStartup<deadline)yield return null;
                foreach(var k in new[]{StationKind.CasinoSlots,StationKind.CasinoPinball,StationKind.CasinoRoulette})
                {
                    var box=Stations.DevBoxOf(k);Check("guest shared machine exists "+k,box.HasValue);if(!box.HasValue)continue;
                    hero.transform.position=new Vector3(box.Value.center.x,box.Value.y+1,0);Physics2D.SyncTransforms();
                    Check("guest opens machine "+k,Stations.TryRightClick(box.Value.center)&&Casino.IsOpen);
                    bool played=k==StationKind.CasinoSlots?Casino.PlaySlots(10000):k==StationKind.CasinoRoulette?Casino.PlayWheel(50000,Casino.Colour.Red):Casino.BuyBalls(1,3)&&Casino.LoadMarble(1)&&Casino.DevStrike(.6);
                    Check("guest settles private bet "+k,played);Casino.Close();yield return new WaitForSecondsRealtime(4);
                }
                File.WriteAllText(Path.Combine(common,"client-rounds-done"),"done");deadline=Time.realtimeSinceStartup+30;while(!File.Exists(Path.Combine(common,"host-observer-done"))&&Time.realtimeSinceStartup<deadline)yield return null;
                Check("host acknowledged guest results",File.Exists(Path.Combine(common,"host-observer-done")));
            }
            NetSession.Leave(null);Plugin.Log.LogInfo($"[AutoTest] CasinoNet TOTAL {ok}/{total} {(ok==total?"PASS":"FAIL 不通过")}");
        }
    }
}
