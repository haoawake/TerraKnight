using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// The Guide's prefab houses: a kit builds an area's little house wherever it is aimed (the user's, 2026-10-21), in the air
    /// too, without a reach limit. Existing player buildings and furniture in its footprint are dismantled. The walls
    /// inside the house with them); the scene's own ground goes only where it has been dug away, and a house doesn't go beside
    /// an altar or a pylon (lit or not), in a boss's fight, in water, over furniture or a chest, or over the player.
    /// </summary>
    internal static class PrefabHouses
    {
        public const long Price = 100000; // 1000 geo, exact, including while a Discount Card is worn.
        public const int Height = 12;
        public const int Simple = 24;
        public const long SimplePrice = 50000; // 500 geo.
        static int Style(int area) => area == Casino.HouseArea ? 20 : area == Simple ? 1 : area;
        public static int HouseHeight(int area) => area == Simple ? 6 : Height;
        static int WallLeft(int area) => area == Simple ? 1 : 2;
        static int WallRight(int area) => Width(area) - (area == Simple ? 1 : 2);
        static int WallTop(int area) => area == Simple ? 5 : 8;
        /// <summary>The cells kept clear round an altar and a pylon.</summary>
        const int KeepClear = 3;
        public static ItemType Kit(int area) => area == Casino.HouseArea ? ItemType.CasinoKit : area == Simple ? ItemType.SimplePrefabHouse : ItemType.PrefabHouseKingsPass + area;
        public static bool IsKit(ItemType type) => type == ItemType.CasinoKit || type == ItemType.SimplePrefabHouse || (type >= ItemType.PrefabHouseKingsPass && type <= ItemType.PrefabHouseGodhome);
        public static int Area(ItemType type) => type == ItemType.CasinoKit ? Casino.HouseArea : type == ItemType.SimplePrefabHouse ? Simple : (int)type-(int)ItemType.PrefabHouseKingsPass;
        public static int Width(int area) => area == Casino.HouseArea ? 26 : area == Simple ? 10 : area%3==0?18:16;
        public static int Wall(int area,int x,int y) => area != Simple && x>=Width(area)-6&&x<=Width(area)-5&&y>=5&&y<=6?21:AreaBlocks.WallId(Style(area));
        public struct Tile { public int X,Y; public CellKind Kind; public int Slope; public Tile(int x,int y,CellKind k,int s=0){X=x;Y=y;Kind=k;Slope=s;} }
        public struct Furnishing { public int Piece; public StationKind Kind; public float X,Y; public Furnishing(int p,float x,float y){Piece=p;Kind=StationKind.None;X=x;Y=y;} public Furnishing(StationKind k,float x,float y){Piece=-1;Kind=k;X=x;Y=y;} }
        public static StationKind FurnitureKind(int area,Furnishing f)=>f.Piece<0?f.Kind:RegionalFurniture.Kind(Style(area),f.Piece);
        public static List<Tile> Tiles(int area)
        {
            int w=Width(area);var list=new List<Tile>();var block=AreaBlocks.Kind(Style(area));
            if(area == Simple)
            {
                // 10 x 6 = the housing minimum of 60 tiles, including the enclosing shell.
                for(int x=0;x<w;x++){list.Add(new Tile(x,0,block));list.Add(new Tile(x,5,block));}
                for(int y=1;y<5;y++){if(y>3)list.Add(new Tile(0,y,block));list.Add(new Tile(w-1,y,block));}
                return list;
            }
            for(int x=0;x<w;x++)list.Add(new Tile(x,0,block));
            for(int y=1;y<=7;y++)
            {
                if(y>3)list.Add(new Tile(1,y,block)); // left door, three tiles high
                list.Add(new Tile(w-2,y,block));
            }
            for(int x=0;x<w;x++)list.Add(new Tile(x,8,block));
            int roof=area%4;
            if(roof==0)for(int y=9;y<12;y++)for(int x=2+(y-9)*2;x<w-2-(y-9)*2;x++)list.Add(new Tile(x,y,block));
            else if(roof==1)for(int y=9;y<11;y++)for(int x=2+(y-9)*3;x<w-2;x++)list.Add(new Tile(x,y,block));
            else if(roof==2){list.Add(new Tile(1,9,block));list.Add(new Tile(w-2,9,block));for(int x=4;x<w-4;x++)list.Add(new Tile(x,9,block));}
            else {for(int x=2;x<w-2;x++)list.Add(new Tile(x,9,block));for(int x=5;x<w-5;x++)list.Add(new Tile(x,10,block));}
            var platform=(CellKind)((int)CellKind.AreaPlatformFirst+Style(area));
            if(area==Casino.HouseArea){for(int x=4;x<w-4;x++)list.Add(new Tile(x,6,platform));}
            else for(int x=3;x<=7;x++)list.Add(new Tile(x,4,platform));
            return list;
        }
        public static Furnishing[] Furniture(int area) => area==Casino.HouseArea ? new[]{new Furnishing(3,1.5f,1),new Furnishing(2,4,1),new Furnishing(1,5.5f,1),new Furnishing(StationKind.CasinoSlots,8,1),new Furnishing(StationKind.CasinoPinball,14,1),new Furnishing(StationKind.CasinoRoulette,20,1),new Furnishing(18,23.5f,1)} : area==Simple ? new[]{
            new Furnishing(3,.5f,1),new Furnishing(2,3,1),new Furnishing(1,5.5f,1),new Furnishing(14,2.5f,2)
        } : new[]{
            new Furnishing(3,1.5f,1),new Furnishing(0,5.5f,1),new Furnishing(1,7.5f,1),
            new Furnishing(2,11,1),new Furnishing(18,Width(area)-3.5f,1)};
        public static void AddItems(List<ItemDef> list)
        {
            Casino.AddItems(list);
            list.Add(new ItemDef{Type=ItemType.RandomPrefabHouse,Name="随机地区预制房",Texture="PrefabHouse_Random",Source=ItemSource.Shop,Value=100000,MaxStack=1,Tooltip="1000 吉欧一座，每次购买随机获得一个地区的预制房套件\n24 地区等概率；包含地区方块、平台、墙壁、桌椅、工作台、门和灯具"});
            list.Add(new ItemDef{Type=ItemType.SimplePrefabHouse,Name="简洁小屋",Texture="PrefabHouse_24",Source=ItemSource.Shop,Value=50000,MaxStack=99,Consumable=true,NoMelee=true,NoUseGraphic=true,UseTime=.5f,UseAnimation=.5f,AutoReuse=false,TileBoost=4,Tooltip="500 吉欧一座，10×6 格（含外框），满足最低住房面积\n含方块、墙壁、门、工作台、椅子和台上蜡烛\n左键搭建；鼠标所指格为房屋底部中央，无安装距离限制；绿色可放、红色不可放\n悬空也可放；会打碎范围内已放置的方块并掉出物品\n可覆盖旧房、家具和箱子，物品掉出；屋顶与房底可共用同一格\n不覆盖原生地形、祭坛晶塔或使用中的联机箱子；放不下不消耗"});
            for(int a=0;a<24;a++)list.Add(new ItemDef{Type=Kit(a),Name=AreaBlocks.All[a].Name+"预制房",Texture="PrefabHouse_"+a,Source=ItemSource.Shop,Value=10000,MaxStack=99,Consumable=true,NoMelee=true,NoUseGraphic=true,UseTime=.5f,UseAnimation=.5f,AutoReuse=false,TileBoost=4,Tooltip="左键在任何地方搭建一座地区小屋（"+Width(a)+"×12 格），悬空也行\n鼠标所指格为房屋底部中央，无安装距离限制；绿色可放、红色不可放\n范围内旧方块、墙壁、家具和箱子会被覆盖，物品掉出；屋顶与房底可共用\n不能放进空洞原本的地形（挖开后可以），也不能放在祭坛和晶塔旁边\n含地区桌椅、工作台、门、灯具、墙壁与平台阁楼\n放不下时不消耗"});
        }
        // The shop never changes its displayed region on reopen: the roll occurs only after a paid purchase.
        public static bool Buy(Inventory inv,int count)
        {
            bool any=false;
            for(int n=0;n<count;n++)
            {
                if(!inv.Mouse.IsEmpty&&!IsKit(inv.Mouse.Type))break;
                bool empty=false;for(int s=0;s<Inventory.Size;s++)if(inv.Slots[s].IsEmpty){empty=true;break;}
                if(!inv.Mouse.IsEmpty&&!empty)break;
                if(Shop.Purse<Price)break;
                var item=Kit(Random.Range(0,24));
                if(!Shop.Spend(Price))break;
                if(inv.Mouse.IsEmpty)inv.Mouse=ItemStack.Of(item);else inv.Pickup(item,1);
                any=true;
            }
            if(any)TerraMod.Instance?.PlaySound("Coins",.7f);
            return any;
        }
        public static Vector2Int Origin(int area,Vector2 aim)
        {
            var p=CellGrid.WorldToCell(aim);
            return new Vector2Int(p.x-Width(area)/2,p.y);
        }
        public static bool CanBuild(int area,Vector2Int origin,HeroController hero,out string reason)
        {
            reason="这里放不下这座房子";
            if(area<0||area>Casino.HouseArea||!Net.WorldSync.CanInteract)return false;
            int w=Width(area);var r=TerraWorld.RegionAt(origin.x,origin.y);
            if(r==null||!r.Contains(origin.x+w-1,origin.y+HouseHeight(area)-1))return false;
            var box=new Rect(origin.x*CellGrid.CellSize,origin.y*CellGrid.CellSize,w*CellGrid.CellSize,HouseHeight(area)*CellGrid.CellSize);
            // An altar and a pylon (lit or not) keep the room round them.
            float keep=KeepClear*CellGrid.CellSize;
            var round=new Rect(box.x-keep,box.y-keep,box.width+keep*2f,box.height+keep*2f);
            foreach(var altar in Stations.Standing(k=>k==StationKind.DemonAltar||k==StationKind.CrimsonAltar))
                if(altar.Box.Overlaps(round)){reason="祭坛旁边不能放房子";return false;}
            if(Pylons.Near(box,keep)){reason="晶塔旁边不能放房子";return false;}
            // Anywhere, in the air too: what the player set there breaks (Build); the scene's own ground only where dug away.
            for(int x=origin.x;x<origin.x+w;x++)for(int y=origin.y;y<origin.y+HouseHeight(area);y++)
            {
                if(TerraWorld.Get(x,y)==CellKind.Original){reason="不能放进空洞原本的地形里（先把这里挖开）";return false;}
                if(r.Grid.GetLiquid(x-r.CellOffset.x,y-r.CellOffset.y)>0){reason="水里放不了房子";return false;}
            }
            // The game's own solid pieces (not the mod's grid): the scene's ground as well.
            foreach(var collider in Physics2D.OverlapBoxAll(box.center,box.size-Vector2.one*.04f,0,1<<ChunkColliders.TerrainLayer))
                if(!collider.isTrigger&&collider.GetComponentInParent<TerraOwned>()==null){reason="不能放进空洞原本的地形里（先把这里挖开）";return false;}
            // A live network chest lease must finish its inventory edits before demolition.
            if(Chests.PrefabBlocked(box)){reason="请先关闭房屋范围内正在使用的联机箱子";return false;}
            reason="";return true;
        }
        internal static bool Build(int area,Vector2Int origin,HeroController hero=null)
        {
            if(!CanBuild(area,origin,hero,out _))return false;
            var region=TerraWorld.RegionAt(origin.x,origin.y);int w=Width(area);
            // Preflight is complete before any mutation. All edits use existing save/network layers.
            // What the player set there breaks as mining it would: the blocks, and the walls the house's own take the place
            // of, dropped as their items, a pile a kind in the house's middle.
            var box=new Rect(origin.x*CellGrid.CellSize,origin.y*CellGrid.CellSize,w*CellGrid.CellSize,HouseHeight(area)*CellGrid.CellSize);
            var objectDrops=new List<(ItemStack Stack,Vector2 At)>();
            Chests.BreakForPrefab(box,objectDrops);Stations.BreakForPrefab(box,objectDrops);
            var planned=new Dictionary<Vector2Int,Tile>();foreach(var tile in Tiles(area))planned[new Vector2Int(tile.X,tile.Y)]=tile;
            var drops=new Dictionary<ItemType,int>();
            for(int x=origin.x;x<origin.x+w;x++)for(int y=origin.y;y<origin.y+HouseHeight(area);y++)
            {
                var kind=TerraWorld.Get(x,y);
                if(kind==CellKind.Empty||kind==CellKind.Original)continue;
                // A shared roof/floor remains the same physical cell, without a duplicate block drop.
                if(planned.TryGetValue(new Vector2Int(x-origin.x,y-origin.y),out var target)&&target.Kind==kind){TerraWorld.SetSlope(x,y,target.Slope);continue;}
                var item=AreaBlocks.DropFor(kind,new Vector2Int(x,y));
                TerraWorld.Set(x,y,CellKind.Empty);
                if(item!=ItemType.None)drops[item]=drops.TryGetValue(item,out int n)?n+1:1;
            }
            for(int x=0;x<w;x++)for(int y=0;y<HouseHeight(area);y++)
            {
                int old=TerraWorld.GetWall(origin.x+x,origin.y+y);
                int wall=x>=WallLeft(area)&&x<WallRight(area)&&y>=1&&y<WallTop(area)?Wall(area,x,y):0;
                if(old==wall)continue;
                if(old>0)foreach(var def in ItemDb.All)if(def!=null&&def.PlacesWall==old){drops[def.Type]=drops.TryGetValue(def.Type,out int n)?n+1:1;break;}
                TerraWorld.SetWall(origin.x+x,origin.y+y,wall);
            }
            DevLastBroken=0;
            if(TerraMod.Instance==null||!TerraMod.Instance.Inventory.Infinite)
            {
                var middle=new Vector2((origin.x+w/2f)*CellGrid.CellSize,(origin.y+2f)*CellGrid.CellSize);
                foreach(var kv in drops)
                {
                    int left=kv.Value,most=Mathf.Max(1,ItemDb.Get(kv.Key).MaxStack);
                    while(left>0){int n=Mathf.Min(left,most);WorldItems.Drop(ItemStack.Of(kv.Key,n),middle);left-=n;DevLastBroken+=n;}
                }
            }
            foreach(var drop in objectDrops)WorldItems.Drop(drop.Stack,drop.At);
            DevLastDrops=drops;
            for(int x=WallLeft(area);x<WallRight(area);x++)for(int y=1;y<WallTop(area);y++)TerraWorld.SetWall(origin.x+x,origin.y+y,Wall(area,x,y));
            // In a boss room its blocks go down as the room's area platforms, as any placed there do (BossRooms.Placed).
            foreach(var t in Tiles(area))TerraWorld.Set(origin.x+t.X,origin.y+t.Y,BossRooms.Placed(origin.x+t.X,origin.y+t.Y,t.Kind));
            foreach(var f in Furniture(area))Stations.PlaceIn(FurnitureKind(area,f),region.SceneName,new Vector2((origin.x+f.X)*CellGrid.CellSize,(origin.y+f.Y)*CellGrid.CellSize)-(Vector2)region.Root.position);
            return true;
        }
        /// <summary>Dev: what the last house broke (item, count) and how many items it dropped.</summary>
        public static Dictionary<ItemType,int> DevLastDrops=new Dictionary<ItemType,int>();
        public static int DevLastBroken;

        public static bool TryUse(ItemType type,HeroController hero,Vector2 aim)
        {
            int a=Area(type);var origin=Origin(a,aim);
            if(!CanBuild(a,origin,hero,out var reason))
            {UI.CombatText.Show(hero.transform.position+Vector3.up,Lang.T(reason),UI.CombatText.Notice);return false;}
            if(Net.WorldSync.Guest)
            {
                var r=TerraWorld.RegionAt(origin.x,origin.y);
                Net.WorldSync.Submit(new Net.WorldSync.Action{Op="prefabHouse",Scene=r.SceneName,X=origin.x-r.CellOffset.x,Y=origin.y-r.CellOffset.y,Kind=(int)type,Count=TerraMod.Instance.Inventory.Infinite?0:1});
                return true;
            }
            return Build(a,origin,hero);
        }
        internal static bool NetBuild(Net.WorldSync.Action action)
        {
            var type=(ItemType)action.Kind;if(!IsKit(type)||action.Count<0||action.Count>1)return false;
            var r=TerrainSystem.Find(action.Scene);if(r==null)return false;
            return Build(Area(type),r.CellOffset+new Vector2Int(Mathf.RoundToInt(action.X),Mathf.RoundToInt(action.Y)));
        }

        static readonly Dictionary<int,Texture2D> textures=new Dictionary<int,Texture2D>();
        static readonly Dictionary<string,Color32[]> rawPixels=new Dictionary<string,Color32[]>();
        static readonly Dictionary<int,Sprite> sprites=new Dictionary<int,Sprite>();
        public static Texture2D Texture(string name)
        {
            int area=name=="PrefabHouse_Random"?0:int.Parse(name.Substring("PrefabHouse_".Length));
            if(textures.TryGetValue(area,out var result))return result;
            int w=Width(area)*16,h=HouseHeight(area)*16;var pixels=new Color[w*h];
            void Blit(Texture2D source,int sx,int sy,int sw,int sh,int dx,int dy)
            {
                Color[] data=null;Color32[] raw=null;
                if(source.isReadable)data=source.GetPixels(sx,source.height-sy-sh,sw,sh);
                else if(!rawPixels.TryGetValue(source.name,out raw))
                {
                    raw=Terraria.TerrariaAssets.Pixels(source.name.Replace("Terraria/",""),out _,out _);rawPixels[source.name]=raw;
                }
                for(int y=0;y<sh;y++)for(int x=0;x<sw;x++)
                {Color p=data!=null?data[y*sw+x]:raw[(source.height-sy-sh+y)*source.width+sx+x];int xx=dx+x,yy=dy+y;if(xx>=0&&xx<w&&yy>=0&&yy<h&&p.a>.1f)pixels[yy*w+xx]=p;}
            }
            var wall=Terraria.TerrariaAssets.Texture("Wall_"+AreaBlocks.WallId(Style(area)));
            var glass=Terraria.TerrariaAssets.Texture("Wall_21");
            for(int x=WallLeft(area);x<WallRight(area);x++)for(int y=1;y<WallTop(area);y++)Blit(Wall(area,x,y)==21?glass:wall,324,108,16,16,x*16,y*16);
            var blocks=Terraria.TerrariaAssets.Texture(AreaBlocks.Sheet(AreaBlocks.Kind(Style(area))));
            var platforms=Terraria.TerrariaAssets.Texture(RegionalFurniture.PlatformSheet((CellKind)((int)CellKind.AreaPlatformFirst+Style(area))));
            foreach(var t in Tiles(area))Blit(Cells.IsPlatform(t.Kind)?platforms:blocks,Cells.IsPlatform(t.Kind)?0:18,Cells.IsPlatform(t.Kind)?0:18,16,16,t.X*16,t.Y*16);
            foreach(var f in Furniture(area))
            {
                var kind=FurnitureKind(area,f);var tex=Casino.IsMachine(kind)?CasinoArt.Preview(kind):Terraria.TerrariaAssets.Texture(RegionalFurniture.Texture(kind));
                Blit(tex,0,0,tex.width,tex.height,Mathf.RoundToInt(f.X*16-tex.width/2f),Mathf.RoundToInt(f.Y*16));
            }
            result=new Texture2D(w,h,TextureFormat.RGBA32,false){name=name,filterMode=FilterMode.Point};result.SetPixels(pixels);result.Apply();textures[area]=result;return result;
        }
        static SpriteRenderer ghost;
        public static void Preview(HeroController hero,ItemDef held,Vector2 aim,bool hidden)
        {
            if(hidden||hero==null||held==null||!IsKit(held.Type)){if(ghost!=null)ghost.enabled=false;return;}
            int a=Area(held.Type);var origin=Origin(a,aim);bool valid=CanBuild(a,origin,hero,out _);
            if(!sprites.TryGetValue(a,out var sprite)){var texture=Texture("PrefabHouse_"+a);sprites[a]=sprite=Sprite.Create(texture,new Rect(0,0,texture.width,texture.height),Vector2.zero,32);}
            if(ghost==null){var go=new GameObject("Terra Prefab House Preview");Object.DontDestroyOnLoad(go);ghost=go.AddComponent<SpriteRenderer>();ghost.sortingOrder=6;}
            ghost.enabled=true;ghost.sprite=sprite;ghost.color=valid?new Color(.6f,1,.6f,.5f):new Color(1,.3f,.3f,.45f);
            ghost.transform.position=new Vector3(origin.x*CellGrid.CellSize,origin.y*CellGrid.CellSize,-.002f);
        }
    }
}
