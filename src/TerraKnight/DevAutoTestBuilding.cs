using System.Collections;
using System.Collections.Generic;
using System.IO;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator BuildingTest()
        {
            int ok = 0, all = 0;
            void Check(string label, bool pass, string detail = "")
            {
                all++; if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 建造 {label}：{detail} {(pass ? "通过" : "不通过")}");
            }
            yield return GoTo("Town", "left1");
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            var hero = HeroController.instance;
            hero.transform.position = new Vector2(110f, 12f);
            yield return new WaitForSeconds(1f);
            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            const float c = CellGrid.CellSize;
            int ox = Mathf.FloorToInt(col.bounds.center.x / c) + 4;
            int oy = Mathf.FloorToInt(col.bounds.min.y / c) + 3;
            var saved = new Dictionary<Vector2Int, (byte Tile, int Wall)>();
            for (int x = ox - 3; x <= ox + 26; x++)
            for (int y = oy - 3; y <= oy + 14; y++)
            {
                var r = TerraWorld.RegionAt(x, y);
                if (r == null) continue;
                int lx = x - r.CellOffset.x, ly = y - r.CellOffset.y;
                saved[new Vector2Int(x,y)] = (WorldEdits.Pack(r.Grid.Get(lx,ly), r.Grid.IsDug(lx,ly), r.Grid.GetSlope(lx,ly)), r.Grid.GetWall(lx,ly));
                TerraWorld.Set(x,y,CellKind.Empty);
                TerraWorld.SetWall(x,y,0);
            }
            void Set(int x, int y, CellKind kind, int shape = 0)
            {
                TerraWorld.Set(ox+x,oy+y,kind);
                TerraWorld.SetSlope(ox+x,oy+y,shape);
            }
            void Stand(int x, float feet)
            {
                var b = col.bounds;
                hero.transform.position += new Vector3((ox+x+0.5f)*c-b.center.x, (oy+feet)*c+0.005f-b.min.y, 0);
                rb.linearVelocity = Vector2.zero;
                Physics2D.SyncTransforms();
            }
            void ClearCourse()
            {
                for (int x=0;x<=23;x++) for(int y=0;y<=10;y++) Set(x,y,CellKind.Empty);
                for (int x=0;x<=23;x++) Set(x,-1,CellKind.Stone);
            }
            try
            {
                ClearCourse();
                // Every saved shape, including old bytes, survives restore and clone.
                bool packed = true;
                for (int shape=0;shape<=5;shape++)
                {
                    byte value=WorldEdits.Pack(CellKind.Stone,true,shape);
                    WorldEdits.Unpack(value,out var kind,out bool dug,out int result);
                    var grid=new CellGrid(2,2); grid.Restore(0,kind,dug,result);
                    packed &= grid.Clone().GetSlope(0,0)==shape && grid.IsDug(0,0);
                }
                Check("六种形状存档及克隆",packed);
                WorldEdits.Unpack(0xA5,out var oldKind,out bool oldDug,out int oldSlope);
                Check("旧平台存档兼容",oldKind==CellKind.Platform && oldDug && oldSlope==2);
                var at=new Vector2Int(ox+3,oy+2);
                Set(3,2,CellKind.Stone);
                var hammer=ItemDb.Get(ItemType.WoodenHammer);
                var sequence=new List<int>();
                for(int i=0;i<6;i++) { BuildingTools.PoundReady=true; BuildingTools.Hammer(hammer,at,false); sequence.Add(TerraWorld.GetSlope(at.x,at.y)); }
                Check("实心块锤击循环",string.Join(",",sequence)=="5,1,2,3,4,0",string.Join(",",sequence));
                Set(4,2,CellKind.Stone);
                sequence.Clear();
                for(int i=0;i<6;i++) { BuildingTools.PoundReady=true; BuildingTools.Hammer(hammer,at,false); sequence.Add(TerraWorld.GetSlope(at.x,at.y)); }
                Check("右贴墙改变塑形次序",string.Join(",",sequence)=="5,2,1,4,3,0",string.Join(",",sequence));
                Set(4,2,CellKind.Empty); Set(3,3,CellKind.Stone);
                sequence.Clear();
                for(int i=0;i<6;i++) { BuildingTools.PoundReady=true; BuildingTools.Hammer(hammer,at,false); sequence.Add(TerraWorld.GetSlope(at.x,at.y)); }
                Check("上实下空先塑形底斜",string.Join(",",sequence)=="5,3,4,1,2,0",string.Join(",",sequence));
                Set(3,3,CellKind.Empty);
                Set(3,2,CellKind.Platform);
                sequence.Clear();
                for(int i=0;i<4;i++) { BuildingTools.PoundReady=true; BuildingTools.Hammer(hammer,at,false); sequence.Add(TerraWorld.GetSlope(at.x,at.y)); }
                Check("平台锤击循环",string.Join(",",sequence)=="1,2,5,0",string.Join(",",sequence));
                Set(3,2,CellKind.Stone);
                BuildingTools.PoundReady=true; BuildingTools.Hammer(hammer,at,false); BuildingTools.Hammer(hammer,at,false);
                Check("按住不重复塑形",TerraWorld.GetSlope(at.x,at.y)==5);
                TerraWorld.SetWall(at.x,at.y,4);
                BuildingTools.Hammer(hammer,at,true); BuildingTools.Hammer(hammer,at,true);
                Check("木锤两下墙尚未破",TerraWorld.GetWall(at.x,at.y)==4);
                BuildingTools.Hammer(hammer,at,true);
                Check("木锤第三下拆墙且不改形",TerraWorld.GetWall(at.x,at.y)==0 && TerraWorld.GetSlope(at.x,at.y)==5);
                Set(3,2,CellKind.Empty);
                Check("无支撑不能放墙",!BuildingTools.CanPlaceWall(at));
                TerraWorld.SetWall(at.x-1,at.y,1);
                Check("邻墙可延伸",BuildingTools.CanPlaceWall(at));
                Stand(1,0);
                mod.DevSetSmartCursor(false);
                mod.DevUse(ItemType.WoodWall,CellGrid.CellCenter(at.x,at.y));
                Check("真实物品路径放墙",TerraWorld.GetWall(at.x,at.y)==4);
                var region=TerraWorld.RegionAt(at.x,at.y);
                var replay=new CellGrid(region.Grid.Width,region.Grid.Height);
                TerrainRegion.ReplayEdits(replay,region.SceneName);
                int ix=at.x-region.CellOffset.x, iy=at.y-region.CellOffset.y;
                Check("场景重建保留墙",replay.GetWall(ix,iy)==4);
                // Exercise the actual binary receive path, including half-brick and wall IDs >255.
                using(var ms=new MemoryStream())
                {
                    var w=new BinaryWriter(ms); w.Write(region.SceneName); w.Write((ushort)1);
                    w.Write(iy*region.Grid.Width+ix);w.Write(WorldEdits.Pack(CellKind.Stone,false,5));w.Write(329);w.Flush();ms.Position=0;
                    Net.TileSync.Receive(new BinaryReader(ms));
                }
                Check("联机接收半砖与高编号墙",TerraWorld.GetSlope(at.x,at.y)==5 && TerraWorld.GetWall(at.x,at.y)==329);
                Check("克隆包含墙层",region.Grid.Clone().GetWall(ix,iy)==329);
                Save.ModSave.Write(1,mod);
                var disk=Newtonsoft.Json.Linq.JObject.Parse(File.ReadAllText(Path.Combine(TerraMod.SaveDir,$"TerraKnight_slot{Save.ModSave.DevStateSlot}.json")));
                string index=(iy*region.Grid.Width+ix).ToString();
                Check("实际存档文件含独立墙层",(int?)disk["Version"]==7 && (int?)disk["Walls"]?[region.SceneName]?[index]==329);
                int wallCount=0;
                foreach(var d in ItemDb.All) if(d!=null && d.PlacesWall>0) wallCount++;
                Check("292种墙有物品定义",wallCount==292,wallCount.ToString());
                var ids=new HashSet<int>(); bool unique=true;
                foreach(var d in ItemDb.All) if(d!=null && d.PlacesWall>0) unique &= ids.Add(d.PlacesWall);
                Check("墙片编号无误合并",unique && ids.Count==292);
                Check("排除误识别为墙的前景材料",!System.Enum.IsDefined(typeof(ItemType),"DynastyWood") && !System.Enum.IsDefined(typeof(ItemType),"RedDynastyShingles"));

                ClearCourse();
                for(int x=3;x<=7;x++) Set(x,2,CellKind.Stone,CellGrid.HalfBrick);
                for(int x=10;x<=14;x++) Set(x,2,CellKind.Platform,CellGrid.HalfBrick);
                for(int x=17;x<=19;x++) Set(x,2,CellKind.Stone,3);
                for(int x=20;x<=22;x++) Set(x,2,CellKind.Stone,4);
                yield return new WaitForSeconds(0.3f);
                foreach(int x in new[]{5,12,18,21})
                {
                    Stand(x,5);yield return new WaitForSeconds(0.7f);
                    float top=(oy+(x<15?2.5f:3f))*c;
                    Check($"形状碰撞落地{x}",Mathf.Abs(col.bounds.min.y-top)<0.08f,$"feet={col.bounds.min.y:F3} top={top:F3}");
                }

                foreach(int slope in new[]{1,2})
                {
                    ClearCourse();
                    int X(int x)=>slope==1?x:23-x;
                    for(int x=3;x<=9;x++) Set(X(x),4,CellKind.Stone);
                    for(int k=0;k<4;k++) Set(X(10+k),4-k,CellKind.Stone,slope);
                    for(int x=14;x<=20;x++) Set(X(x),0,CellKind.Stone);
                    yield return new WaitForSeconds(0.3f);
                    Stand(X(17),1);yield return new WaitForSeconds(0.3f);
                    int dir=slope==1?-1:1;
                    Player.TerraMovement.DevMove=dir;
                    float target=(ox+X(6)+0.5f)*c;
                    for(float t=0;t<2.5f && (dir<0?col.bounds.center.x>target:col.bounds.center.x<target);t+=Time.fixedDeltaTime) yield return new WaitForFixedUpdate();
                    Player.TerraMovement.DevMove=0;yield return new WaitForSeconds(0.3f);
                    bool reached=dir<0?col.bounds.center.x<=target+0.3f:col.bounds.center.x>=target-0.3f;
                    Check($"实心坡{slope}上坡",reached && Mathf.Abs(col.bounds.min.y-(oy+5)*c)<0.08f,$"x={col.bounds.center.x:F3} feet={col.bounds.min.y:F3}");
                }

                foreach(int slope in new[]{1,2})
                {
                    ClearCourse();
                    int X(int x)=>slope==1?x:23-x;
                    for(int x=3;x<=9;x++) Set(X(x),4,CellKind.Platform);
                    for(int k=0;k<4;k++) Set(X(10+k),4-k,CellKind.Platform,slope);
                    for(int x=14;x<=20;x++) Set(X(x),0,CellKind.Platform);
                    yield return new WaitForSeconds(0.3f);
                    Stand(X(17),1);
                    yield return new WaitForSeconds(0.3f);
                    int dir=slope==1?-1:1;
                    Player.TerraMovement.DevMove=dir;
                    float target=(ox+X(6)+0.5f)*c;
                    int tick = 0;
                    for(float t=0;t<2.5f && (dir<0?col.bounds.center.x>target:col.bounds.center.x<target);t+=Time.fixedDeltaTime)
                    {
                        yield return new WaitForFixedUpdate();
                        if (tick++ % 5 == 0) Plugin.Log.LogInfo($"[AutoTest] 建造 trace 坡{slope} x={col.bounds.center.x:F3} feet={col.bounds.min.y:F3} v={rb.linearVelocity} ground={hero.cState.onGround} {Player.StairCollision.DevState}");
                    }
                    Player.TerraMovement.DevMove=0;
                    yield return new WaitForSeconds(0.5f);
                    bool reached=dir<0?col.bounds.center.x<=target+0.3f:col.bounds.center.x>=target-0.3f;
                    Check($"坡{slope}上坡衔接平平台",reached && Mathf.Abs(col.bounds.min.y-(oy+5)*c)<0.08f,$"x={col.bounds.center.x:F2} target={target:F2} feet={col.bounds.min.y:F3} top={(oy+5)*c:F3} ground={hero.cState.onGround}");
                    Shot("Town","building_stair"+slope);
                    Player.TerraMovement.DevMove=-dir;
                    float low=(ox+X(18)+0.5f)*c;
                    for(float t=0;t<2.5f && (dir<0?col.bounds.center.x<low:col.bounds.center.x>low);t+=Time.fixedDeltaTime) yield return new WaitForFixedUpdate();
                    Player.TerraMovement.DevMove=0;
                    yield return new WaitForSeconds(0.4f);
                    Check($"坡{slope}下坡衔接平平台",Mathf.Abs(col.bounds.min.y-(oy+1)*c)<0.08f,$"feet={col.bounds.min.y:F3}");
                    Stand(X(11),4.5f); yield return new WaitForSeconds(0.4f);
                    float before=col.bounds.min.y;
                    Player.TerraMovement.DevDown=true; yield return new WaitForSeconds(0.7f); Player.TerraMovement.DevDown=false;
                    Check($"坡{slope}按下穿过",col.bounds.min.y<before-0.3f);
                }
                // Three cells = 48px, player = 42px. Only six pixels of headroom remain.
                foreach(int slope in new[]{1,2})
                {
                    ClearCourse();
                    for(int x=1;x<=22;x++) Set(x,3,CellKind.Stone);
                    for(int y=0;y<3;y++) Set(12,y,CellKind.Platform,slope);
                    yield return new WaitForSeconds(0.3f);
                    int uphill=slope==1?-1:1;
                    Stand(slope==1?16:8,0);yield return new WaitForSeconds(0.3f);
                    Player.TerraMovement.DevMove=uphill;yield return new WaitForSeconds(1f);Player.TerraMovement.DevMove=0;
                    bool blocked=uphill<0?col.bounds.center.x>(ox+12)*c:col.bounds.center.x<(ox+13)*c;
                    Check($"坡{slope}低顶单向墙阻挡上坡侧",blocked,$"x={col.bounds.center.x:F3} wall={(ox+12)*c:F3}");
                    Stand(slope==1?8:16,0);yield return new WaitForSeconds(0.3f);
                    Player.TerraMovement.DevMove=-uphill;yield return new WaitForSeconds(1f);Player.TerraMovement.DevMove=0;
                    bool passed=uphill<0?col.bounds.center.x>(ox+14)*c:col.bounds.center.x<(ox+11)*c;
                    Check($"坡{slope}低顶单向墙放行下坡侧",passed,$"x={col.bounds.center.x:F3}");
                }
                ClearCourse();
                for(int shape=0;shape<=5;shape++) Set(5+shape*2,3,CellKind.Stone,shape);
                for(int x=3;x<=18;x++) for(int y=0;y<=2;y++) TerraWorld.SetWall(ox+x,oy+y,x<8?1:x<13?4:16);
                Stand(10,0);yield return new WaitForSeconds(0.7f);
                CaptureBuildingView("Town_building_shapes_camera");
            }
            finally
            {
                Player.TerraMovement.DevMove=0;Player.TerraMovement.DevDown=false;
                mod.DevNoFallDamage=false;
                foreach(var kv in saved)
                {
                    var r=TerraWorld.RegionAt(kv.Key.x,kv.Key.y); if(r==null) continue;
                    WorldEdits.Unpack(kv.Value.Tile,out var k,out bool dug,out int shape);
                    int lx=kv.Key.x-r.CellOffset.x,ly=kv.Key.y-r.CellOffset.y;
                    r.Grid.ApplyNetwork(ly*r.Grid.Width+lx,k,dug,shape);
                    r.Grid.SetWall(lx,ly,kv.Value.Wall);
                }
            }
            Plugin.Log.LogInfo($"[AutoTest] 建造 合计 {ok}/{all} {(ok==all?"通过":"不通过")}");
        }

        void CaptureBuildingView(string name)
        {
            var camera=GameCameras.instance.mainCamera;
            var previous=camera.targetTexture;
            var previousPosition=camera.transform.position;
            var active=RenderTexture.active;
            var target=new RenderTexture(1280,720,24);
            var picture=new Texture2D(1280,720,TextureFormat.RGB24,false);
            try
            {
                var hero=HeroController.instance.GetComponent<BoxCollider2D>().bounds;
                camera.transform.position=new Vector3(hero.center.x,hero.center.y+1f,previousPosition.z);
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                picture.ReadPixels(new Rect(0,0,1280,720),0,0);picture.Apply();
                File.WriteAllBytes(Path.Combine(outDir,name+".png"),ImageConversion.EncodeToPNG(picture));
                Plugin.Log.LogInfo($"[AutoTest] 建造手动相机截图 {name}");
            }
            finally
            {
                camera.targetTexture=previous;camera.transform.position=previousPosition;RenderTexture.active=active;
                Object.Destroy(target);Object.Destroy(picture);
            }
        }
    }
}
