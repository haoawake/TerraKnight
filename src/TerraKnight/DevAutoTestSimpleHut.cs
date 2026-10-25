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
        IEnumerator SimpleHutTest()
        {
            var type=ItemType.SimplePrefabHouse;
            long price=PrefabHouses.SimplePrice;
            int a=PrefabHouses.Simple;
            int furnishings=4;
            int ok=0,total=0;void Check(string name,bool pass){total++;if(pass)ok++;Plugin.Log.LogInfo($"[AutoTest] SimpleHut {name} {(pass?"PASS":"FAIL 不通过")}");}
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
            int slot=System.Array.FindIndex(TownNpcs.ShopItems,s=>s.Type==type);
            Check("simple and random houses both on shelf",slot>=0&&TownNpcs.ShopItems.Any(s=>s.Type==ItemType.RandomPrefabHouse)&&TownNpcs.ShopItems.Any(s=>s.Type==ItemType.SimplePrefabHouse));
            if(slot<0)yield break;
            Check("requested geo price",TownNpcs.BuyPrice(slot)==price);
            TownNpcs.DiscountLatched=true;Check("fixed price with discount",TownNpcs.BuyPrice(slot)==price);TownNpcs.DiscountLatched=false;
            System.Array.Clear(inv.Slots,0,Inventory.Size);inv.Mouse=default;Shop.Spend(Shop.Purse);Shop.Earn(price);
            Check("buy with exact price",TownNpcs.Buy(slot,1,inv)&&inv.Mouse.Type==type&&inv.Mouse.Count==1&&Shop.Purse==0);
            inv.Mouse=default;
            Check("insufficient funds no item",!TownNpcs.Buy(slot,1,inv)&&inv.Mouse.IsEmpty&&Shop.Purse==0);
            Shop.Earn(price*2);inv.Mouse=ItemStack.Of(ItemType.Gel);long before=Shop.Purse;
            Check("occupied cursor no charge",!TownNpcs.Buy(slot,1,inv)&&Shop.Purse==before);
            inv.Mouse=default;Check("multiple purchases stack",TownNpcs.Buy(slot,2,inv)&&inv.Mouse.Type==type&&inv.Mouse.Count==2&&Shop.Purse==0);
            var rect=InventoryGui.DevRectOf("ShopSlot",slot);if(rect.HasValue){UI.TerraCursor.DevDrives=true;UI.TerraCursor.DevAt=rect.Value.center;}
            yield return new WaitForSeconds(.5f);Shot("Town","simple_hut_shop");UI.TerraCursor.DevDrives=false;
            TownNpcs.Close(true);InventoryGui.Open=false;inv.UseMouse=false;inv.Override=-1;inv.Selected=0;
            var root=new GameObject("Prefab House Verification Terrain");root.transform.position=new Vector3(1000,1000,0);root.AddComponent<TerraOwned>();
            var holder=new GameObject("Prefab House Verification System");var fixture=holder.AddComponent<TerrainSystem>();var grid=new CellGrid(64,40);
            const string scene="PrefabHouseFixture";var region=new TerrainRegion(scene,grid,root.transform,false,TerrainSystem.Current.Region.Material,new SpriteRenderer[0]);region.SetOffset(new Vector2Int(2000,2000));
            HarmonyLib.AccessTools.Property(typeof(TerrainSystem),"Region").SetValue(fixture,region,null);HarmonyLib.AccessTools.Property(typeof(TerrainSystem),"SceneName").SetValue(fixture,scene,null);TerrainSystem.All.Add(fixture);
            var cameraObject=new GameObject("Prefab House Verification Camera");var camera=cameraObject.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=3.8f;camera.nearClipPlane=.01f;camera.farClipPlane=100;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.11f,.14f,.19f);camera.cullingMask=1<<30;
            var target=new RenderTexture(1280,720,24);camera.targetTexture=target;Player.TerraControls.DevNoRealKeys=true;rb.constraints=RigidbodyConstraints2D.FreezeAll;Stations.DevNoAutoDoors=true;
            try
            {
                var origin=new Vector2Int(2012,2005);
                hero.transform.position=new Vector3((origin.x+7)*.5f,(origin.y+2)*.5f-feetOffset,original.z);Physics2D.SyncTransforms();
                var aim=new Vector2((origin.x+5.2f)*.5f,(origin.y+.2f)*.5f);
                inv.Slots[0]=ItemStack.Of(type,2);inv.Mouse=default;
                Check("empty site valid",PrefabHouses.CanBuild(a,origin,hero,out _));
                mod.DevUseSelected(aim);yield return null;region.Tick();yield return new WaitForSeconds(.3f);
                Check("actual use consumes one",inv.Slots[0].Count==1);
                Check("10 by 6 shell",PrefabHouses.Width(a)==10&&PrefabHouses.HouseHeight(a)==6&&PrefabHouses.Tiles(a).All(t=>TerraWorld.Get(origin.x+t.X,origin.y+t.Y)==t.Kind));
                bool room=Housing.StartRoomCheck(origin.x+7,-(origin.y+2)-1);
                Check("housing exactly 60 cells",room&&Housing.NumRoomTiles==60);
                Check("housing furniture light entrance",room&&Housing.RoomNeeds());
                Housing.ScoreRoom(null,0,new System.Collections.Generic.List<Housing.Occupant>());
                Check("npc has standing space",room&&Housing.HiScore>0);
                var saved=Stations.Save().Where(s=>s.Scene==scene).ToList();
                Check("minimal furniture with required entrance",saved.Count==furnishings&&new[]{1,2,3,14}.All(p=>saved.Any(s=>s.Kind==RegionalFurniture.Kind(1,p))));
                mod.DevUseSelected(aim);Check("occupied house replaced consumes one",inv.Slots[0].IsEmpty);
                Stations.Load(saved);yield return null;region.Tick();
                Check("saved furniture restores",Stations.Save().Count(s=>s.Scene==scene)==furnishings);
                var restored=new CellGrid(64,40);TerrainRegion.ReplayEdits(restored,scene);
                Check("saved terrain and walls restore",PrefabHouses.Tiles(a).All(t=>restored.Get(origin.x-2000+t.X,origin.y-2000+t.Y)==t.Kind)&&restored.GetWall(origin.x-2000+7,origin.y-2000+2)==AreaBlocks.WallId(1));
                var texture=Terraria.TerrariaAssets.Texture("PrefabHouse_"+a);Check("compact icon",texture.width==160&&texture.height==96);
                foreach(var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))renderer.gameObject.layer=30;
                foreach(var station in Stations.Standing(k=>RegionalFurniture.IsRegional(k)))
                {
                    var list=new System.Collections.Generic.List<(Rect Box,SpriteRenderer Renderer)>();Stations.StandingOf(station.Kind,list);
                    foreach(var sr in list)if(sr.Renderer!=null)foreach(var renderer in sr.Renderer.GetComponentsInChildren<SpriteRenderer>(true))renderer.gameObject.layer=30;
                }
                camera.orthographicSize=2.2f;camera.transform.position=new Vector3((origin.x+5)*.5f,(origin.y+3)*.5f,-30);camera.Render();
                var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1280,720,TextureFormat.RGBA32,false);
                image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();System.IO.File.WriteAllBytes(System.IO.Path.Combine(outDir,"simple_hut.png"),image.EncodeToPNG());Object.Destroy(image);RenderTexture.active=previous;
                // Removing one column makes the area smaller than the minimum.
                for(int y=1;y<5;y++)TerraWorld.Set(origin.x+8,origin.y+y,AreaBlocks.Kind(1));
                Check("one column smaller fails housing",!Housing.StartRoomCheck(origin.x+7,-(origin.y+2)-1)&&Housing.Reason==Housing.Failure.RoomIsTooSmall);
                Plugin.Log.LogInfo($"[AutoTest] SimpleHut TOTAL {ok}/{total} {(ok==total?"PASS":"FAIL 不通过")}");
            }
            finally
            {
                Stations.Clear();Stations.DevNoAutoDoors=false;TerrainSystem.All.Remove(fixture);Object.Destroy(root);Object.Destroy(holder);Object.Destroy(cameraObject);target.Release();Object.Destroy(target);
                Player.TerraMovement.DevDown=false;rb.constraints=constraints;hero.transform.position=original;Player.TerraControls.DevNoRealKeys=false;Player.TerraStats.DevImmortal=immortal;inv.Infinite=infinite;System.Array.Copy(savedInventory,inv.Slots,savedInventory.Length);inv.Mouse=mouse;
            }
        }
    }
}
