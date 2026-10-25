using System.Collections;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;
namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator PrefabOverwriteTest()
        {
            int ok=0,total=0;void Check(string name,bool pass){total++;if(pass)ok++;Plugin.Log.LogInfo($"[AutoTest] PrefabOverwrite {name} {(pass?"PASS":"FAIL 不通过")}");}
            Player.TerraStats.DevImmortal=true;mod.DevNoFallDamage=true;PlayerData.instance.isInvincible=true;
            yield return GoTo("Town","left1");yield return new WaitForSeconds(3);
            var hero=HeroController.instance;var inv=mod.Inventory;var rb=hero.GetComponent<Rigidbody2D>();var constraints=rb.constraints;
            inv.Clear();TerraMod.DevAllInfinite=false;mod.ApplyCreative(false);inv.Infinite=false;inv.UseMouse=false;inv.Override=-1;inv.Selected=0;InventoryGui.Open=false;
            var root=new GameObject("Overwrite fixture");root.transform.position=new Vector3(1000,1000,0);root.AddComponent<TerraOwned>();var holder=new GameObject("Overwrite terrain");var fixture=holder.AddComponent<TerrainSystem>();var grid=new CellGrid(80,64);
            const string scene="OverwriteFixture";var region=new TerrainRegion(scene,grid,root.transform,false,TerrainSystem.Current.Region.Material,new SpriteRenderer[0]);region.SetOffset(new Vector2Int(2000,2000));
            HarmonyLib.AccessTools.Property(typeof(TerrainSystem),"Region").SetValue(fixture,region,null);HarmonyLib.AccessTools.Property(typeof(TerrainSystem),"SceneName").SetValue(fixture,scene,null);TerrainSystem.All.Add(fixture);
            rb.constraints=RigidbodyConstraints2D.FreezeAll;Stations.DevNoAutoDoors=true;Player.TerraControls.DevNoRealKeys=true;
            try
            {
                var o=new Vector2Int(2012,2005);
                for(int a=0;a<=25;a++)
                {
                    Stations.Clear();Chests.Clear();WorldItems.Clear();for(int x=0;x<80;x++)for(int y=0;y<64;y++){grid.Set(x,y,CellKind.Empty);grid.SetWall(x,y,0);}
                    var type=PrefabHouses.Kit(a);int w=PrefabHouses.Width(a),roof=a==24?5:8;
                    inv.Slots[0]=ItemStack.Of(type,3);var aim=new Vector2((o.x+w/2+.2f)*.5f,(o.y+.2f)*.5f);
                    Check(a+" out of ordinary reach",!TerraMod.InReach(hero.GetComponent<BoxCollider2D>().bounds,ItemDb.Get(type),o));
                    mod.DevUseSelected(aim);yield return null;region.Tick();yield return null;
                    Check(a+" actual remote installation",inv.Slots[0].Count==2&&Stations.Save().Count(s=>s.Scene==scene)==PrefabHouses.Furniture(a).Length);
                    var upper=o+Vector2Int.up*roof;var topAim=new Vector2((upper.x+w/2+.2f)*.5f,(upper.y+.2f)*.5f);
                    Check(a+" roof cell origin does not move up",PrefabHouses.Origin(a,topAim)==upper);
                    mod.DevUseSelected(topAim);yield return null;region.Tick();yield return null;
                    Check(a+" shared floor consumes kit",inv.Slots[0].Count==1);
                    Check(a+" common roof floor has no extra row",PrefabHouses.Tiles(a).All(t=>TerraWorld.Get(upper.x+t.X,upper.y+t.Y)==t.Kind)&&TerraWorld.Get(o.x+w/2,o.y+roof)==AreaBlocks.Kind(a==24?1:a==25?20:a));
                    Check(a+" old house below and two furniture sets preserved",Stations.Save().Count(s=>s.Scene==scene)==2*PrefabHouses.Furniture(a).Length&&TerraWorld.Get(o.x+w/2,o.y)==AreaBlocks.Kind(a==24?1:a==25?20:a));
                    mod.DevUseSelected(aim);yield return null;region.Tick();yield return null;
                    Check(a+" replace lower occupied house",inv.Slots[0].IsEmpty&&Stations.Save().Count(s=>s.Scene==scene)==2*PrefabHouses.Furniture(a).Length);
                }
                Stations.Clear();Chests.Clear();WorldItems.Clear();for(int x=0;x<80;x++)for(int y=0;y<64;y++){grid.Set(x,y,CellKind.Empty);grid.SetWall(x,y,0);}
                var items=new ItemStack[Chests.Capacity];items[0]=ItemStack.Of(ItemType.Gel,37);items[1]=ItemStack.Of(ItemType.CopperShortsword);items[1].Prefix=1;items[1].Forge=2;
                Chests.Load(new[]{new Chests.Saved{Scene=scene,Local=new Vector2((o.x+5)*.5f-1000,(o.y+1)*.5f-1000),Items=items,Name="keep contents",Style=0}});yield return null;
                TerraWorld.Set(o.x+5,o.y,CellKind.Wood);TerraWorld.SetWall(o.x+9,o.y+5,AreaBlocks.WallId(2));region.Tick();
                inv.Slots[0]=ItemStack.Of(ItemType.SimplePrefabHouse,2);mod.DevUseSelected(new Vector2((o.x+5.2f)*.5f,(o.y+.2f)*.5f));yield return null;region.Tick();
                var drops=WorldItems.NetRecords().ToArray();Check("covered chest removed and shell returned",Chests.Count==0&&drops.Any(d=>d.Kind==(int)RegionalChests.ItemOf(0)&&d.Seed==1));
                Check("chest contents and equipment modifiers preserved",drops.Any(d=>d.Kind==(int)ItemType.Gel&&d.Seed==37)&&drops.Any(d=>d.Kind==(int)ItemType.CopperShortsword&&d.Prefix==1&&d.Forge==2));
                Check("old wall outside new interior cleared",TerraWorld.GetWall(o.x+9,o.y+5)==0);
                var before=Stations.Save();int count=inv.Slots[0].Count;TerraWorld.Set(o.x+4,o.y+3,CellKind.Original);region.Tick();mod.DevUseSelected(new Vector2((o.x+5.2f)*.5f,(o.y+.2f)*.5f));
                Check("native terrain not overwritten and no kit spent",inv.Slots[0].Count==count&&TerraWorld.Get(o.x+4,o.y+3)==CellKind.Original&&Stations.Save().Count==before.Count);
                Plugin.Log.LogInfo($"[AutoTest] PrefabOverwrite TOTAL {ok}/{total} {(ok==total?"PASS":"FAIL 不通过")}");
            }
            finally{Stations.Clear();Chests.Clear();TerrainSystem.All.Remove(fixture);Object.Destroy(root);Object.Destroy(holder);rb.constraints=constraints;Player.TerraControls.DevNoRealKeys=false;Stations.DevNoAutoDoors=false;}
        }
    }
}
