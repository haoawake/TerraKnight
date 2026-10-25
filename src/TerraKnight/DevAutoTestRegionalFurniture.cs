using System.Collections;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator RegionalFurnitureTest()
        {
            bool immortal=Player.TerraStats.DevImmortal;Player.TerraStats.DevImmortal=true;mod.Stats.FullHeal();mod.DevNoFallDamage=true;
            if(PlayerData.instance!=null)PlayerData.instance.isInvincible=true;
            yield return GoTo("Crossroads_01",EntryGateOf("Crossroads_01"));
            yield return new WaitForSeconds(1.5f);
            PlayerData.instance.isInvincible=true;mod.DevNoFallDamage=true;
            mod.Stats.FullHeal();
            QuietAround(HeroController.instance.transform.position,60);
            var hero=HeroController.instance;var rb=hero.GetComponent<Rigidbody2D>();
            var originalPosition=hero.transform.position;
            const string fixtureScene="TerraFurnitureFixture";
            var fixtureRoot=new GameObject("Terra Furniture Fixture Grid");fixtureRoot.transform.position=new Vector3(1000,1000,0);
            fixtureRoot.AddComponent<TerraOwned>();
            var fixtureHolder=new GameObject("Terra Furniture Fixture System");
            var fixture=fixtureHolder.AddComponent<TerrainSystem>();
            var grid=new CellGrid(64,40);
            var fixtureRegion=new TerrainRegion(fixtureScene,grid,fixtureRoot.transform,false,TerrainSystem.Current.Region.Material,new SpriteRenderer[0]);
            fixtureRegion.SetOffset(new Vector2Int(2000,2000));
            HarmonyLib.AccessTools.Property(typeof(TerrainSystem),"Region").SetValue(fixture,fixtureRegion,null);
            HarmonyLib.AccessTools.Property(typeof(TerrainSystem),"SceneName").SetValue(fixture,fixtureScene,null);
            TerrainSystem.All.Add(fixture);
            int gx=fixtureRegion.CellOffset.x+12,fy=fixtureRegion.CellOffset.y+5,ok=0,total=0;
            const float c=CellGrid.CellSize;
            void Check(string name,bool pass,string detail=""){total++;if(pass)ok++;Plugin.Log.LogInfo($"[AutoTest] 地区家具 {name}：{detail} {(pass?"通过":"不通过")}");}
            bool infinite=mod.Inventory.Infinite;mod.Inventory.Infinite=false;
            var constraints=rb.constraints;rb.constraints=RigidbodyConstraints2D.FreezeAll;Stations.DevNoAutoDoors=true;WorldItems.DevNoGrab=true;
            Player.TerraControls.DevNoRealKeys=true;
            var inventory=(ItemStack[])mod.Inventory.Slots.Clone();
            var probeObject=new GameObject("Terra Furniture Verification Camera");
            Object.DontDestroyOnLoad(probeObject);
            var probe=probeObject.AddComponent<Camera>();probe.enabled=false;probe.orthographic=true;probe.orthographicSize=2f;probe.nearClipPlane=.01f;probe.farClipPlane=100;
            probe.clearFlags=CameraClearFlags.SolidColor;probe.backgroundColor=new Color(.08f,.09f,.12f);probe.cullingMask=1<<30;
            var target=new RenderTexture(1280,720,24);probe.targetTexture=target;
            try
            {
                for(int a=0;a<AreaBlocks.All.Length;a++)
                {
                    var area=AreaBlocks.All[a];
                    for(int pose=0;pose<21;pose++)
                    {
                        var kind=RegionalFurniture.Kind(a,pose);var sprite=RegionalFurniture.Sprite(kind);
                        Check(area.Key+" 资源 "+RegionalFurniture.Key(kind),sprite!=null&&sprite.texture.filterMode==FilterMode.Point,sprite==null?"缺贴图":sprite.rect.ToString());
                    }
                    for(int i=0;i<RegionalFurniture.Pieces.Length;i++)
                    {
                        if(GameManager.instance.sceneName!="Crossroads_01")yield return GoTo("Crossroads_01",EntryGateOf("Crossroads_01"));
                        Stations.Clear();Chests.Clear();WorldItems.DevClear();InventoryGui.Open=false;
                        // Mine first, then build: replacing Original directly doesn't mark its painted collider as dug.
                        for(int x=gx-6;x<=gx+9;x++)for(int y=fy-2;y<=fy+10;y++)TerraWorld.Set(x,y,CellKind.Empty);
                        for(int x=gx-6;x<=gx+9;x++)TerraWorld.Set(x,fy,AreaBlocks.Kind(a));
                        hero.transform.position=new Vector3((gx-2)*c,(fy+4)*c,hero.transform.position.z);
                        var p=RegionalFurniture.Pieces[i];var kind=RegionalFurniture.Kind(a,i);var item=RegionalFurniture.Item(a,i);
                        var aim=new Vector2((gx+3)*c,(fy+1)*c+.1f);
                        if(p.Base==StationKind.Door)TerraWorld.Set(gx+3,fy+4,AreaBlocks.Kind(a));
                        if(p.Base==StationKind.Chandelier||p.Base==StationKind.Lantern)TerraWorld.Set(gx+3,fy+1+p.Heights.Length,AreaBlocks.Kind(a));
                        yield return new WaitForSeconds(.15f);
                        foreach(var terrain in TerrainSystem.All)terrain.Region?.Tick();Physics2D.SyncTransforms();
                        var recipe=Recipes.All.SingleOrDefault(r=>r.Result==item&&!r.IsBought);
                        System.Array.Clear(mod.Inventory.Slots,0,mod.Inventory.Slots.Length);
                        mod.Inventory.Slots[0]=ItemStack.Of(AreaBlocks.BlockItem(a),9999);mod.Inventory.Slots[1]=ItemStack.Of(ItemType.Torch,9999);
                        bool ingredients=recipe!=null&&recipe.Ingredients.All(t=>t.Type==AreaBlocks.BlockItem(a)||t.Type==ItemType.Torch)&&Recipes.HasIngredients(recipe,mod.Inventory);
                        if(ingredients){Recipes.Consume(recipe,mod.Inventory);mod.Inventory.Pickup(item,recipe.Count);}
                        Check(area.Key+" 合成 "+p.Key,ingredients&&Recipes.Count(mod.Inventory,item)==(recipe?.Count??-1),recipe?.Station);
                        if(i==RegionalFurniture.PlatformIndex)
                        {
                            var cell=new Vector2Int(gx+3,fy+3);var platform=(CellKind)((int)CellKind.AreaPlatformFirst+a);
                            TerraWorld.Set(cell.x-1,cell.y,platform);
                            bool can=TerraMod.CanPlace(cell,platform);TerraWorld.Set(cell.x,cell.y,platform);TerraWorld.SetSlope(cell.x,cell.y,1);
                            Check(area.Key+" 平台类型/斜面/掉落",can&&TerraWorld.IsPlatform(cell.x,cell.y)&&TerraWorld.GetSlope(cell.x,cell.y)==1&&AreaBlocks.DropFor(platform,cell)==item,$"can={can}, kind={TerraWorld.Get(cell.x,cell.y)}, slope={TerraWorld.GetSlope(cell.x,cell.y)}");
                            continue;
                        }
                        if(i==RegionalFurniture.DresserIndex)
                        {
                            bool placed=Chests.TryPlace(hero,aim,RegionalFurniture.DresserStyle(a));yield return new WaitForSeconds(.04f);
                            var saved=Chests.Save();var box=Chests.Boxes().LastOrDefault();
                            Check(area.Key+" 梳妆台放置",placed&&Mathf.Abs(box.width-3*c)<.01f,box.ToString());
                            bool opened=placed&&Chests.TryRightClick(box.center)&&Chests.OpenItems?.Length==40;
                            if(opened)Chests.OpenItems[0]=ItemStack.Of(AreaBlocks.BlockItem(a),7);
                            saved=Chests.Save();Chests.Load(saved);yield return null;
                            Check(area.Key+" 梳妆台储物存档",opened&&Chests.Save().Any(s=>s.Style==RegionalFurniture.DresserStyle(a)&&s.Items[0].Type==AreaBlocks.BlockItem(a)&&s.Items[0].Count==7));
                            Chests.TryMine(ItemDb.Get(ItemType.TitaniumPickaxe),box.center,hero);
                            Check(area.Key+" 非空梳妆台不能挖",Chests.Count==1);
                            Chests.TryRightClick(box.center);if(Chests.OpenItems!=null)System.Array.Clear(Chests.OpenItems,0,40);Chests.Close(false);
                            Chests.TryMine(ItemDb.Get(ItemType.TitaniumPickaxe),box.center,hero);
                            Check(area.Key+" 梳妆台挖回地区物品",Chests.Count==0&&WorldItems.DevCount(item)==1);
                            continue;
                        }
                        bool built=Stations.TryPlace(kind,hero,aim);
                        if(!built){yield return new WaitForSeconds(.25f);built=Stations.TryPlace(kind,hero,aim);}
                        // Native room art may cross one fixture cell: find a nearby valid cursor position, never force placement.
                        for(int step=1;!built&&step<=4;step++)built=Stations.TryPlace(kind,hero,aim+Vector2.left*(step*.25f));
                        yield return new WaitForSeconds(.04f);
                        var bb=Stations.DevBoxOf(kind);
                        Check(area.Key+" 放置 "+p.Key,built&&bb.HasValue,Stations.DevWhy??"");
                        if(!bb.HasValue)continue;
                        if(p.Base==StationKind.Chandelier||p.Base==StationKind.Lantern)Check(area.Key+" 顶挂位置 "+p.Key,Mathf.Abs(bb.Value.yMax-(fy+1+p.Heights.Length)*c)<.01f);
                        if(p.Torches>0)
                        {
                            probe.transform.position=new Vector3(bb.Value.center.x,bb.Value.center.y,-30);probe.Render();
                            var lights=Object.FindObjectsByType<RegionalFurnitureLight>(FindObjectsInactive.Exclude,FindObjectsSortMode.None);
                            foreach(var light in lights)foreach(var sr in light.GetComponentsInChildren<SpriteRenderer>())sr.gameObject.layer=30;
                            probe.Render();
                            Check(area.Key+" 彩焰 "+p.Key,lights.Length==1&&lights[0].Flame!=null&&lights[0].Rgb==RegionalFurniture.LightColor(a));
                            if(lights.Length==1&&lights[0].Flame!=null){var before=(Vector2[])lights[0].Flame.DevOffsets.Clone();yield return new WaitForSeconds(.12f);probe.Render();lights[0].Flame.SendMessage("Update");Check(area.Key+" 火焰动态 "+p.Key,before.Where((v,k)=>v!=lights[0].Flame.DevOffsets[k]).Any());}
                            if(i==18){probe.Render();var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(1280,720,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();Check(area.Key+" 灯具实际渲染",image.GetPixels32().Count(color=>color.r+color.g+color.b>180)>50);System.IO.File.WriteAllBytes(System.IO.Path.Combine(outDir,"regional_light_"+area.Key+".png"),image.EncodeToPNG());Object.Destroy(image);RenderTexture.active=previous;}
                        }
                        if(p.Base==StationKind.Door)
                        {
                            bool opened=Stations.TryRightClick(bb.Value.center);yield return null;
                            Check(area.Key+" 开门保留样式",opened&&Stations.Save().Any(s=>RegionalFurniture.Area(s.Kind)==a&&RegionalFurniture.BaseKind(s.Kind)==StationKind.DoorOpenRight));
                            var snapshot=Stations.Save();Stations.Load(snapshot);yield return null;
                            bool shut=Stations.TryRightClick(bb.Value.center);yield return null;
                            Check(area.Key+" 存档后关门",shut&&Stations.Save().Any(s=>s.Kind==kind));
                            bool faced=hero.cState.facingRight;hero.cState.facingRight=false;
                            Stations.TryRightClick(bb.Value.center);yield return null;
                            Check(area.Key+" 左开门保留样式",Stations.Save().Any(s=>RegionalFurniture.Area(s.Kind)==a&&RegionalFurniture.BaseKind(s.Kind)==StationKind.DoorOpenLeft));
                            var frames=new System.Collections.Generic.HashSet<int>();Stations.HousingTiles((x,y,t,f)=>{if(t==11)frames.Add(f);});
                            Check(area.Key+" 左开门住房门轴",frames.SetEquals(new[]{36,54}));
                            Stations.TryRightClick(bb.Value.center);hero.cState.facingRight=faced;yield return null;
                        }
                        if(p.Base==StationKind.Bed)
                        {
                            Stations.TryRightClick(bb.Value.center);
                            Check(area.Key+" 床设置重生点",Beds.Current(out var sc,out var lc,out _,out _,out _)&&sc==fixtureScene);
                        }
                        if(p.Base==StationKind.Chair)Check(area.Key+" 椅子安置居民",Stations.ChairNear(bb.Value.center,out _,out _,out _));
                        if(p.Base==StationKind.WorkBench||p.Base==StationKind.Bookcase||p.Base==StationKind.Sink)Check(area.Key+" 合成站 "+p.Key,Stations.Near(p.Base==StationKind.WorkBench?"工作台":p.Base==StationKind.Bookcase?"书架":"水槽",hero));
                        Stations.TryMine(ItemDb.Get(ItemType.TitaniumPickaxe),bb.Value.center,hero);
                        Check(area.Key+" 挖回 "+p.Key,WorldItems.DevCount(item)==1&&Stations.Count==0);
                    }
                }
                Stations.Clear();Chests.Clear();WorldItems.DevClear();
                for(int x=gx-6;x<=gx+9;x++)for(int y=fy-2;y<=fy+10;y++)TerraWorld.Set(x,y,CellKind.Empty);
                for(int x=gx-6;x<=gx+9;x++)TerraWorld.Set(x,fy,AreaBlocks.Kind(4));
                for(int x=gx;x<gx+7;x++)TerraWorld.Set(x,fy+4,(CellKind)((int)CellKind.AreaPlatformFirst+4));
                yield return new WaitForSeconds(.3f);
                foreach(var terrain in TerrainSystem.All)terrain.Region?.Tick();Physics2D.SyncTransforms();
                rb.constraints=constraints;hero.transform.position=new Vector3((gx+3)*c,(fy+9)*c,hero.transform.position.z);rb.linearVelocity=Vector2.zero;Physics2D.SyncTransforms();
                yield return new WaitForSeconds(3f);
                var hbox=hero.GetComponent<BoxCollider2D>().bounds;
                Check("苍绿平台实际承重",hero.cState.onGround&&Mathf.Abs(hbox.min.y-(fy+5)*c)<.12f,$"feet={hbox.min.y:F2} top={(fy+5)*c:F2}");
                Player.TerraMovement.DevDown=true;yield return new WaitForSeconds(.6f);Player.TerraMovement.DevDown=false;
                Check("苍绿平台按下穿过",hero.GetComponent<BoxCollider2D>().bounds.min.y<(fy+5)*c-.2f);
                Shot("Crossroads_01","regional_platform_physics");
                Plugin.Log.LogInfo($"[AutoTest] 地区家具 合计 {ok}/{total} {(ok==total?"通过":"不通过")}");
                System.IO.File.WriteAllText(System.IO.Path.Combine(outDir,"regional-furniture.json"),$"{{\"checks\":{total},\"passed\":{ok},\"regions\":24,\"craftable_items\":456}}");
            }
            finally
            {
                Stations.Clear();Chests.Clear();WorldItems.DevClear();WorldItems.DevNoGrab=false;Stations.DevNoAutoDoors=false;
                rb.constraints=constraints;Player.TerraMovement.DevDown=false;mod.Inventory.Infinite=infinite;System.Array.Copy(inventory,mod.Inventory.Slots,inventory.Length);Player.TerraCamera.DevLook=null;
                Player.TerraControls.DevNoRealKeys=false;
                Object.Destroy(probeObject);target.Release();Object.Destroy(target);
                hero.transform.position=originalPosition;rb.linearVelocity=Vector2.zero;
                TerrainSystem.All.Remove(fixture);Object.Destroy(fixtureHolder);
                Player.TerraStats.DevImmortal=immortal;
            }
            yield return RegionalFurnitureCraftUiTest();
            yield return VanillaRecipesTest();
        }

        IEnumerator RegionalFurnitureCraftUiTest()
        {
            yield return GoTo("Town","left1");
            TerraMod.DevAllInfinite=false;mod.ApplyCreative(false);
            var inv=mod.Inventory;inv.Override=-1;InventoryGui.Open=true;
            int total=0,ok=0;var hero=HeroController.instance;var region=TerrainSystem.Current.Region;
            void Check(string text,bool pass){total++;if(pass)ok++;Plugin.Log.LogInfo($"[AutoTest] 地区家具制造界面 {text} {(pass?"通过":"不通过")}");}
            void Fill(Recipe r,int area,bool wrong=false,bool omitTorch=false)
            {
                System.Array.Clear(inv.Slots,0,inv.Slots.Length);inv.Mouse=default;
                foreach(var ingredient in r.Ingredients)
                {
                    if(ingredient.Type==ItemType.Torch&&omitTorch)continue;
                    inv.Add(wrong&&ingredient.Type!=ItemType.Torch?AreaBlocks.BlockItem((area+1)%24):ingredient.Type,ingredient.Count);
                }
            }
            for(int a=0;a<24;a++)
            {
                var at=(Vector2)hero.transform.position-(Vector2)region.Root.position+Vector2.right;
                Stations.Clear();Stations.PlaceFixed(RegionalFurniture.Kind(a,2),region.SceneName,at);yield return null;
                for(int i=0;i<RegionalFurniture.Pieces.Length;i++)
                {
                    var p=RegionalFurniture.Pieces[i];var type=RegionalFurniture.Item(a,i);var r=Recipes.All.Single(x=>x.Result==type&&!x.IsBought);
                    bool hand=i==2||i==4;
                    if(hand){Stations.Clear();yield return null;}
                    Fill(r,a);bool made=InventoryGui.DevCraft(type);
                    Check(AreaBlocks.All[a].Key+" 制作 "+p.Key,made&&inv.Mouse.Type==type&&inv.Mouse.Count==r.Count&&r.Ingredients.All(x=>Recipes.Count(inv,x.Type)==0));
                    if(hand){Stations.PlaceFixed(RegionalFurniture.Kind(a,2),region.SceneName,at);yield return null;}
                    Fill(r,a,true);Check(AreaBlocks.All[a].Key+" 不能用其它地区方块制作 "+p.Key,!InventoryGui.DevCraft(type));
                    if(p.Torches>0){Fill(r,a,false,true);Check(AreaBlocks.All[a].Key+" 灯具需要火把 "+p.Key,!InventoryGui.DevCraft(type));}
                }
            }
            Stations.Clear();InventoryGui.Open=false;inv.Mouse=default;
            Plugin.Log.LogInfo($"[AutoTest] 地区家具制造界面 合计 {ok}/{total} {(ok==total?"通过":"不通过")}");
            System.IO.File.WriteAllText(System.IO.Path.Combine(outDir,"regional-craft-ui.json"),$"{{\"checks\":{total},\"passed\":{ok},\"craftable_items\":456}}");
        }
    }
}
