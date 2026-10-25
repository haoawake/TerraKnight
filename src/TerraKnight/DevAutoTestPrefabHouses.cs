using System.Collections;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.Town;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator PrefabHousesTest()
        {
            int ok=0,total=0;void Check(string name,bool pass){total++;if(pass)ok++;Plugin.Log.LogInfo($"[AutoTest] PrefabHouses {name} {(pass?"PASS":"FAIL 不通过")}");}
            bool immortal=Player.TerraStats.DevImmortal;Player.TerraStats.DevImmortal=true;mod.Stats.FullHeal();mod.DevNoFallDamage=true;PlayerData.instance.isInvincible=true;
            yield return GoTo("Town","left1");yield return new WaitForSeconds(4);
            var hero=HeroController.instance;var rb=hero.GetComponent<Rigidbody2D>();var inv=mod.Inventory;
            var savedInventory=(ItemStack[])inv.Slots.Clone();var mouse=inv.Mouse;bool infinite=inv.Infinite;var constraints=rb.constraints;var original=hero.transform.position;
            inv.Infinite=false;TerraMod.DevAllInfinite=false;mod.ApplyCreative(false);Shop.DevStage=0;
            var pos=TownNpcs.DevPos("Guide");
            if(!pos.HasValue){TownNpcs.DevPlace("Guide");yield return new WaitForSeconds(.5f);pos=TownNpcs.DevPos("Guide");}
            Check("guide exists at stage 0",pos.HasValue);
            if(!pos.HasValue)yield break;
            float feetOffset=hero.GetComponent<BoxCollider2D>().bounds.min.y-hero.transform.position.y;
            hero.transform.position=Clear(hero,new Vector2(pos.Value.x-1.3f,pos.Value.y-20f/32f-feetOffset+.05f));rb.linearVelocity=Vector2.zero;
            yield return new WaitForSeconds(.4f);TownNpcs.TryRightClick(TownNpcs.DevPos("Guide").Value);yield return null;
            Check("guide shop and crafting buttons",TownNpcs.TalkingIsGuide&&TownNpcs.TalkingHasShop);
            Shot("Town","prefab_guide_chat");TownNpcs.OpenShop();yield return new WaitForSeconds(.5f);
            int slot=System.Array.FindIndex(TownNpcs.ShopItems,s=>s.Type==ItemType.RandomPrefabHouse);
            Check("random house on guide shelf",slot>=0&&TownNpcs.ShopOpen);
            if(slot<0)yield break;
            Check("exact price",TownNpcs.BuyPrice(slot)==100000);
            TownNpcs.DiscountLatched=true;Check("discount cannot change requested fixed price",TownNpcs.BuyPrice(slot)==100000);TownNpcs.DiscountLatched=false;
            System.Array.Clear(inv.Slots,0,inv.Slots.Length);inv.Mouse=default;Shop.Spend(Shop.Purse);Shop.Earn(100000);
            long before=Shop.Purse;Check("actual one purchase",TownNpcs.Buy(slot,1,inv)&&PrefabHouses.IsKit(inv.Mouse.Type)&&before-Shop.Purse==100000);
            var first=inv.Mouse;inv.Mouse=default;before=Shop.Purse;
            Check("insufficient money gives nothing",!TownNpcs.Buy(slot,1,inv)&&Shop.Purse==before&&inv.Mouse.IsEmpty);
            Shop.Earn(1024L*100000+1000000);var randomState=Random.state;Random.InitState(20261021);var regions=new int[24];
            bool purchases=true;for(int i=0;i<1024;i++)
            {
                inv.Mouse=default;before=Shop.Purse;
                if(!TownNpcs.Buy(slot,1,inv)||!PrefabHouses.IsKit(inv.Mouse.Type)||before-Shop.Purse!=100000){purchases=false;break;}
                regions[PrefabHouses.Area(inv.Mouse.Type)]++;
            }
            Random.state=randomState;
            Check("1024 paid rolls and all 24 regions",purchases&&regions.All(n=>n>0));
            for(int i=0;i<Inventory.Size;i++)inv.Slots[i]=ItemStack.Of(ItemType.StoneBlock,9999);
            inv.Mouse=first;before=Shop.Purse;Check("full inventory no charge",!TownNpcs.Buy(slot,1,inv)&&Shop.Purse==before&&inv.Mouse.Type==first.Type);
            inv.Mouse=ItemStack.Of(ItemType.Gel);Check("unrelated cursor no charge",!TownNpcs.Buy(slot,1,inv)&&Shop.Purse==before);
            System.Array.Clear(inv.Slots,0,Inventory.Size);inv.Mouse=default;
            yield return new WaitForSeconds(3f);
            var shopRect=InventoryGui.DevRectOf("ShopSlot",slot);Check("shop slot visible and clickable",shopRect.HasValue);
            if(shopRect.HasValue){UI.TerraCursor.DevDrives=true;UI.TerraCursor.DevAt=shopRect.Value.center;}
            yield return new WaitForSeconds(.5f);Shot("Town","prefab_guide_shop");yield return new WaitForSeconds(.4f);UI.TerraCursor.DevDrives=false;
            TownNpcs.Close(true);InventoryGui.Open=false;inv.UseMouse=false;inv.Override=-1;inv.Selected=0;
            var root=new GameObject("Prefab House Verification Terrain");root.transform.position=new Vector3(1000,1000,0);root.AddComponent<TerraOwned>();
            var holder=new GameObject("Prefab House Verification System");var fixture=holder.AddComponent<TerrainSystem>();var grid=new CellGrid(64,40);
            const string scene="PrefabHouseFixture";var region=new TerrainRegion(scene,grid,root.transform,false,TerrainSystem.Current.Region.Material,new SpriteRenderer[0]);region.SetOffset(new Vector2Int(2000,2000));
            HarmonyLib.AccessTools.Property(typeof(TerrainSystem),"Region").SetValue(fixture,region,null);HarmonyLib.AccessTools.Property(typeof(TerrainSystem),"SceneName").SetValue(fixture,scene,null);TerrainSystem.All.Add(fixture);
            var cameraObject=new GameObject("Prefab House Verification Camera");var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=3.8f;camera.nearClipPlane=.01f;camera.farClipPlane=100;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.11f,.14f,.19f);camera.cullingMask=1<<30;
            var target=new RenderTexture(1280,720,24);camera.targetTexture=target;Player.TerraControls.DevNoRealKeys=true;rb.constraints=RigidbodyConstraints2D.FreezeAll;Stations.DevNoAutoDoors=true;
            try
            {
                for(int a=0;a<24;a++)
                {
                    Stations.Clear();for(int x=0;x<64;x++)for(int y=0;y<40;y++){grid.Set(x,y,CellKind.Empty);grid.SetWall(x,y,0);}
                    var origin=new Vector2Int(2012,2005);int w=PrefabHouses.Width(a);for(int x=2005;x<2045;x++)TerraWorld.Set(x,origin.y-1,AreaBlocks.Kind(a));region.Tick();Physics2D.SyncTransforms();
                    feetOffset=hero.GetComponent<BoxCollider2D>().bounds.min.y-hero.transform.position.y;
                    hero.transform.position=new Vector3((origin.x+9)*.5f,(origin.y+1)*.5f-feetOffset+.08f,original.z);rb.linearVelocity=Vector2.zero;Physics2D.SyncTransforms();
                    var aim=new Vector2((origin.x+w/2+.2f)*.5f,(origin.y+.2f)*.5f);var type=PrefabHouses.Kit(a);string key=AreaBlocks.All[a].Key;
                    Check(key+" valid empty site",PrefabHouses.CanBuild(a,origin,hero,out _));
                    inv.Slots[0]=ItemStack.Of(type,2);mod.DevUseSelected(aim);yield return null;region.Tick();yield return new WaitForSeconds(.15f);
                    Check(key+" actual use consumes one",inv.Slots[0].Type==type&&inv.Slots[0].Count==1);
                    Check(key+" matching region blocks and platform",PrefabHouses.Tiles(a).All(t=>TerraWorld.Get(origin.x+t.X,origin.y+t.Y)==t.Kind));
                    Check(key+" matching background walls",TerraWorld.GetWall(origin.x+2,origin.y+2)==AreaBlocks.WallId(a));
                    var saved=Stations.Save().Where(s=>s.Scene==scene).ToList();
                    Check(key+" table chair bench door lamp",saved.Count==5&&PrefabHouses.Furniture(a).All(f=>saved.Any(s=>s.Kind==RegionalFurniture.Kind(a,f.Piece))));
                    bool room=Housing.StartRoomCheck(origin.x+9,-(origin.y+2)-1);
                    Check(key+" housing enclosed and large enough",room&&Housing.NumRoomTiles>=Housing.MinRoomTiles);
                    Check(key+" housing furniture and light",room&&Housing.RoomNeeds());
                    if(room){Housing.ScoreRoom(null,0,new System.Collections.Generic.List<Housing.Occupant>());Check(key+" npc standing space",Housing.HiScore>0);}
                    mod.DevUseSelected(aim);Check(key+" occupied house replaced consumes one",inv.Slots[0].IsEmpty);
                    Stations.Load(saved);yield return null;
                    Check(key+" saved furniture restores",Stations.Save().Count(s=>s.Scene==scene)==5&&WorldEdits.For(scene).Count>0&&WorldWalls.For(scene).Count>0);
                    var restored=new CellGrid(64,40);TerrainRegion.ReplayEdits(restored,scene);
                    Check(key+" saved terrain and walls replay",PrefabHouses.Tiles(a).All(t=>restored.Get(origin.x-2000+t.X,origin.y-2000+t.Y)==t.Kind)&&restored.GetWall(origin.x-2000+2,origin.y-2000+2)==AreaBlocks.WallId(a)&&restored.GetWall(origin.x-2000+w-6,origin.y-2000+5)==21);
                    var texture=Terraria.TerrariaAssets.Texture("PrefabHouse_"+a);Check(key+" house icon and preview",texture.width==w*16&&texture.height==192&&texture.GetPixels32().Any(p=>p.a>0));
                    foreach(var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))renderer.gameObject.layer=30;
                    foreach(var station in Stations.Standing(k=>RegionalFurniture.IsRegional(k)))if(RegionalFurniture.Area(station.Kind)==a)
                    {
                        var list=new System.Collections.Generic.List<(Rect Box,SpriteRenderer Renderer)>();Stations.StandingOf(station.Kind,list);foreach(var sr in list)if(sr.Renderer!=null)foreach(var renderer in sr.Renderer.GetComponentsInChildren<SpriteRenderer>(true))renderer.gameObject.layer=30;
                    }
                    camera.transform.position=new Vector3((origin.x+w/2f)*.5f,(origin.y+5.5f)*.5f,-30);camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;
                    var image=new Texture2D(1280,720,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
                    Check(key+" rendered house",image.GetPixels32().Count(p=>p.r+p.g+p.b>200)>500);
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(outDir,"prefab_house_"+key+".png"),image.EncodeToPNG());Object.Destroy(image);RenderTexture.active=previous;
                    // A block the player set there breaks (its item dropped, the kit spent) and the house goes up over it.
                    Stations.Clear();for(int x=0;x<64;x++)for(int y=5;y<25;y++){grid.Set(x,y,CellKind.Empty);grid.SetWall(x,y,0);}
                    TerraWorld.Set(origin.x+4,origin.y+5,CellKind.Wood);region.Tick();
                    inv.Slots[0]=ItemStack.Of(type,2);mod.DevUseSelected(aim);yield return null;region.Tick();
                    Check(key+" placed block broken and house built",inv.Slots[0].Count==1&&PrefabHouses.DevLastDrops.Values.Sum()==1&&PrefabHouses.DevLastBroken==1&&PrefabHouses.Tiles(a).All(t=>TerraWorld.Get(origin.x+t.X,origin.y+t.Y)==t.Kind));
                    if(a==0)
                    {
                        // The scene's own ground refuses it until dug away; the air takes it; an altar keeps it off.
                        Stations.Clear();for(int x=0;x<64;x++)for(int y=0;y<40;y++){grid.Set(x,y,CellKind.Empty);grid.SetWall(x,y,0);}
                        for(int x=2005;x<2045;x++)TerraWorld.Set(x,origin.y-1,AreaBlocks.Kind(a));
                        grid.Set(origin.x-2000+5,origin.y-2000+3,CellKind.Original);region.Tick();
                        bool refused=!PrefabHouses.CanBuild(a,origin,hero,out var why);
                        Check("hallownest ground refuses it",refused&&why.Contains("原本"));
                        grid.Set(origin.x-2000+5,origin.y-2000+3,CellKind.Empty);region.Tick();
                        Check("dug-away ground takes it",PrefabHouses.CanBuild(a,origin,hero,out _));
                        for(int x=2005;x<2045;x++)TerraWorld.Set(x,origin.y-1,CellKind.Empty);region.Tick();
                        Check("in the air too",PrefabHouses.CanBuild(a,origin,hero,out _));
                        Stations.PlaceIn(StationKind.DemonAltar,scene,new Vector2((origin.x+w+1)*.5f-1000f,origin.y*.5f-1000f));yield return null;region.Tick();yield return null;
                        bool altar=!PrefabHouses.CanBuild(a,origin,hero,out var why2);
                        Check("not beside an altar",altar&&why2.Contains("祭坛"));
                        Stations.Clear();
                    }
                }
                Plugin.Log.LogInfo($"[AutoTest] PrefabHouses TOTAL {ok}/{total} {(ok==total?"PASS":"FAIL 不通过")}");
                System.IO.File.WriteAllText(System.IO.Path.Combine(outDir,"prefab-houses.json"),$"{{\"checks\":{total},\"passed\":{ok},\"regions\":24,\"paid_rolls\":1024}}");
            }
            finally
            {
                Stations.Clear();Stations.DevNoAutoDoors=false;TerrainSystem.All.Remove(fixture);Object.Destroy(root);Object.Destroy(holder);Object.Destroy(cameraObject);target.Release();Object.Destroy(target);
                rb.constraints=constraints;hero.transform.position=original;Player.TerraControls.DevNoRealKeys=false;Player.TerraStats.DevImmortal=immortal;inv.Infinite=infinite;System.Array.Copy(savedInventory,inv.Slots,savedInventory.Length);inv.Mouse=mouse;
            }
            yield return RegionalPlatformsTest();
        }
    }
}
