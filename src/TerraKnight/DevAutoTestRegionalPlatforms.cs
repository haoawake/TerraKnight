using System.Collections;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        // An isolated world fixture: all 24 recipes, all 27 native frames, both stair slopes,
        // half bricks, actual collision/drop-through, and a rendered screenshot per region.
        IEnumerator RegionalPlatformsTest()
        {
            bool immortal=Player.TerraStats.DevImmortal;Player.TerraStats.DevImmortal=true;
            mod.Stats.FullHeal();mod.DevNoFallDamage=true;PlayerData.instance.isInvincible=true;
            // Stay in the already-loaded safe opening scene. A room transition resets native
            // health during its entry fall and can kill a fresh character before the fixture.
            for(float t=0;t<10f&&!GameHooks.PlayerHasControl();t+=Time.deltaTime)yield return null;
            var hero=HeroController.instance;var rb=hero.GetComponent<Rigidbody2D>();
            var original=hero.transform.position;var constraints=rb.constraints;
            mod.Stats.FullHeal();mod.DevNoFallDamage=true;PlayerData.instance.isInvincible=true;
            var root=new GameObject("Regional Platform Fixture");root.transform.position=new Vector3(1000,1000,0);root.AddComponent<TerraOwned>();
            var holder=new GameObject("Regional Platform Fixture System");var fixture=holder.AddComponent<TerrainSystem>();
            var grid=new CellGrid(64,40);
            var region=new TerrainRegion("RegionalPlatformFixture",grid,root.transform,false,TerrainSystem.Current.Region.Material,new SpriteRenderer[0]);
            region.SetOffset(new Vector2Int(2000,2000));
            HarmonyLib.AccessTools.Property(typeof(TerrainSystem),"Region").SetValue(fixture,region,null);
            HarmonyLib.AccessTools.Property(typeof(TerrainSystem),"SceneName").SetValue(fixture,"RegionalPlatformFixture",null);
            TerrainSystem.All.Add(fixture);
            int gx=2012,fy=2005,ok=0,total=0;const float c=CellGrid.CellSize;
            var inv=mod.Inventory;var saved=(ItemStack[])inv.Slots.Clone();var mouse=inv.Mouse;bool infinite=inv.Infinite;inv.Infinite=false;
            var cameraObject=new GameObject("Regional Platform Verification Camera");var camera=cameraObject.AddComponent<Camera>();
            camera.enabled=false;camera.orthographic=true;camera.orthographicSize=3f;camera.nearClipPlane=.01f;camera.farClipPlane=100;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.09f,.10f,.14f,0f);camera.cullingMask=1<<30;
            var target=new RenderTexture(1280,720,24);camera.targetTexture=target;
            void Check(string name,bool pass){total++;if(pass)ok++;Plugin.Log.LogInfo($"[AutoTest] RegionalPlatforms {name} {(pass?"PASS":"FAIL 不通过")}");}
            Player.TerraControls.DevNoRealKeys=true;
            try
            {
                for(int a=0;a<24;a++)
                {
                    var area=AreaBlocks.All[a];var item=RegionalFurniture.Item(a,RegionalFurniture.PlatformIndex);
                    var kind=(CellKind)((int)CellKind.AreaPlatformFirst+a);
                    var recipe=Recipes.All.Single(r=>r.Result==item&&!r.IsBought);
                    Check(area.Key+" recipe 1 block -> 2 platforms",recipe.Count==2&&recipe.Station=="徒手"&&recipe.Ingredients.Length==1&&recipe.Ingredients[0].Type==AreaBlocks.BlockItem(a)&&recipe.Ingredients[0].Count==1);
                    System.Array.Clear(inv.Slots,0,inv.Slots.Length);inv.Mouse=default;inv.Add(AreaBlocks.BlockItem(a),1);InventoryGui.Open=true;
                    bool made=InventoryGui.DevCraft(item);
                    Check(area.Key+" actual craft",made&&inv.Mouse.Type==item&&inv.Mouse.Count==2&&Recipes.Count(inv,AreaBlocks.BlockItem(a))==0);
                    System.Array.Clear(inv.Slots,0,inv.Slots.Length);inv.Mouse=default;inv.Add(AreaBlocks.BlockItem((a+1)%24),1);
                    Check(area.Key+" wrong regional block rejected",!InventoryGui.DevCraft(item));InventoryGui.Open=false;
                    var texture=Terraria.TerrariaAssets.Texture(RegionalFurniture.PlatformSheet(kind));
                    Check(area.Key+" sheet 486x18 point filtered",texture!=null&&texture.width==486&&texture.height==18&&texture.filterMode==FilterMode.Point);
                    for(int frame=0;frame<27;frame++)
                    {
                        var sprite=Terraria.TerrariaAssets.Sprite(RegionalFurniture.PlatformSheet(kind),frame*18,0,16,16,16,Vector2.zero);
                        Check(area.Key+" frame "+frame,sprite!=null&&sprite.rect.width==16&&sprite.rect.height==16);
                        Object.Destroy(sprite);
                    }
                    rb.constraints=RigidbodyConstraints2D.FreezeAll;
                    for(int x=gx-6;x<=gx+22;x++)for(int y=fy-2;y<=fy+16;y++)TerraWorld.Set(x,y,CellKind.Empty);
                    for(int x=gx-6;x<=gx+22;x++)TerraWorld.Set(x,fy,AreaBlocks.Kind(a));
                    for(int x=gx;x<gx+7;x++)TerraWorld.Set(x,fy+5,kind);
                    // Isolated tile, wall attachments, ascending and descending runs, half brick.
                    TerraWorld.Set(gx+10,fy+5,kind);TerraWorld.Set(gx+12,fy+5,AreaBlocks.Kind(a));TerraWorld.Set(gx+13,fy+5,kind);
                    TerraWorld.Set(gx+15,fy+5,kind);TerraWorld.SetSlope(gx+15,fy+5,CellGrid.HalfBrick);
                    for(int s=1;s<=2;s++)for(int k=0;k<5;k++)
                    {
                        int x=gx+k+(s==2?9:0),y=fy+12+(s==1?-k:k-4);
                        TerraWorld.Set(x,y,kind);TerraWorld.SetSlope(x,y,s);
                        Check(area.Key+" stair slope "+s+" step "+k,TerraWorld.GetSlope(x,y)==s&&PlatformStairs.FrameX(grid,x-2000,y-2000)%18==0);
                    }
                    // High and low landings join the stair at its actual top surface.
                    for(int k=1;k<=2;k++)
                    {
                        TerraWorld.Set(gx-k,fy+12,kind);TerraWorld.Set(gx+4+k,fy+7,kind);
                        TerraWorld.Set(gx+9-k,fy+7,kind);TerraWorld.Set(gx+13+k,fy+12,kind);
                    }
                    region.Tick();Physics2D.SyncTransforms();yield return null;
                    foreach(var renderer in root.GetComponentsInChildren<SpriteRenderer>(true))renderer.gameObject.layer=30;
                    camera.transform.position=new Vector3((gx+7)*c,(fy+8)*c,-30);camera.Render();
                    var previous=RenderTexture.active;RenderTexture.active=target;
                    var image=new Texture2D(1280,720,TextureFormat.RGBA32,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
                    Check(area.Key+" actual rendered terrain",image.GetPixels32().Count(p=>p.r+p.g+p.b>180)>100);
                    for(int slope=1;slope<=2;slope++)
                    {
                        int gaps=0;
                        // Sample the continuous walk line in the ACTUAL rendered image,
                        // including all four tile boundaries, independently of sprite masks.
                        for(int px=1;px<79;px++)
                        {
                            float wx=(gx+(slope==2?9:0))*c+(px+.5f)/32f;
                            float wy=slope==1?(fy+13)*c-(px+.5f)/32f-1f/32f:(fy+8)*c+(px+.5f)/32f-1f/32f;
                            var screen=camera.WorldToScreenPoint(new Vector3(wx,wy,0));var pixel=image.GetPixel(Mathf.RoundToInt(screen.x),Mathf.RoundToInt(screen.y));
                            // A transparent clear distinguishes an actual hole from a dark
                            // regional pixel whose RGB happens to match the backdrop.
                            if(pixel.a<.99f)gaps++;
                        }
                        Check(area.Key+" rendered diagonal continuity slope="+slope+" gaps="+gaps,gaps==0);
                    }
                    System.IO.File.WriteAllBytes(System.IO.Path.Combine(outDir,"regional_platform_"+area.Key+".png"),image.EncodeToPNG());
                    RenderTexture.active=previous;Object.Destroy(image);
                    hero.transform.position=new Vector3((gx+3)*c,(fy+9)*c,original.z);rb.linearVelocity=Vector2.zero;rb.constraints=constraints;Physics2D.SyncTransforms();
                    yield return new WaitForSeconds(2f);
                    Check(area.Key+" actual standing",hero.cState.onGround&&Mathf.Abs(hero.GetComponent<BoxCollider2D>().bounds.min.y-(fy+6)*c)<.12f);
                    Player.TerraMovement.DevDown=true;yield return new WaitForSeconds(.6f);Player.TerraMovement.DevDown=false;
                    Check(area.Key+" actual down-through",hero.GetComponent<BoxCollider2D>().bounds.min.y<(fy+6)*c-.2f);
                    // Walk the actual player up and down EACH diagonal, checking the feet
                    // against the stair surface throughout the traversal, not only the flag.
                    var box=hero.GetComponent<BoxCollider2D>();
                    foreach(int slope in new[]{1,2})foreach(bool up in new[]{true,false})
                    {
                        int direction=(slope==1?-1:1)*(up?1:-1);
                        int step=direction>0?1:3;
                        int sx=gx+step+(slope==2?9:0),sy=fy+12+(slope==1?-step:step-4);
                        float footOffset=box.bounds.min.y-hero.transform.position.y;
                        // The player's 20px-wide box is wider than a 16px stair tile: its
                        // uphill corner must start above the high edge, not the tile centre.
                        hero.transform.position=new Vector3((sx+.5f)*c,(sy+1f)*c-footOffset+.08f,original.z);
                        rb.linearVelocity=Vector2.zero;Physics2D.SyncTransforms();yield return new WaitForSeconds(.4f);
                        float startX=box.bounds.center.x,startFeet=box.bounds.min.y,worst=0;int onStair=0;var trace=new System.Text.StringBuilder();
                        Player.TerraMovement.DevMove=direction;
                        for(float t=0;t<1.5f&&Mathf.Abs(box.bounds.center.x-startX)<.8f;t+=Time.fixedDeltaTime)
                        {
                            yield return new WaitForFixedUpdate();
                            if(Player.StairCollision.DevSurface is float surface){onStair++;worst=Mathf.Max(worst,Mathf.Abs(box.bounds.min.y-surface));}
                            if(trace.Length<800)trace.Append($" x={box.bounds.center.x:F2} y={box.bounds.min.y:F2} vx={rb.linearVelocity.x:F2} stair={Player.StairCollision.OnStair}");
                        }
                        Player.TerraMovement.DevMove=0;
                        float dy=box.bounds.min.y-startFeet;
                        bool walked=onStair>0&&Mathf.Abs(box.bounds.center.x-startX)>.5f&&(up?dy>.2f:dy<-.2f)&&worst<.18f;
                        Check(area.Key+" walk slope="+slope+" up="+up+" samples="+onStair+" dy="+dy.ToString("F2")+" error="+worst.ToString("F3")+" start="+startX.ToString("F2")+","+startFeet.ToString("F2")+" trace="+trace,walked);
                    }
                    float jumpOffset=box.bounds.min.y-hero.transform.position.y;
                    hero.transform.position=new Vector3((gx+3)*c,(fy+1)*c-jumpOffset+.08f,original.z);
                    rb.linearVelocity=Vector2.zero;Physics2D.SyncTransforms();yield return new WaitForSeconds(.4f);
                    rb.linearVelocity=new Vector2(0,9f*Player.TerraMovement.PxT);float peak=box.bounds.min.y;
                    for(float t=0;t<2.4f;t+=Time.deltaTime){peak=Mathf.Max(peak,box.bounds.min.y);yield return null;}
                    Check(area.Key+" actual jump from below and land",peak>(fy+6)*c+.1f&&hero.cState.onGround&&Mathf.Abs(box.bounds.min.y-(fy+6)*c)<.12f);
                    Check(area.Key+" dropped item identity",AreaBlocks.DropFor(kind,new Vector2Int(gx,fy+5))==item);
                }
                Plugin.Log.LogInfo($"[AutoTest] RegionalPlatforms TOTAL {ok}/{total} {(ok==total?"PASS":"FAIL 不通过")}");
                System.IO.File.WriteAllText(System.IO.Path.Combine(outDir,"regional-platforms.json"),$"{{\"checks\":{total},\"passed\":{ok},\"regions\":24,\"connection_frames\":648}}");
            }
            finally
            {
                Player.TerraMovement.DevMove=0;Player.TerraMovement.DevDown=false;Player.TerraControls.DevNoRealKeys=false;Player.TerraStats.DevImmortal=immortal;
                rb.constraints=constraints;hero.transform.position=original;rb.linearVelocity=Vector2.zero;
                InventoryGui.Open=false;inv.Infinite=infinite;System.Array.Copy(saved,inv.Slots,saved.Length);inv.Mouse=mouse;
                TerrainSystem.All.Remove(fixture);Object.Destroy(root);Object.Destroy(holder);Object.Destroy(cameraObject);target.Release();Object.Destroy(target);
            }
        }
    }
}
