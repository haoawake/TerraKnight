using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.Terraria;
using UnityEngine;
namespace TerraKnight.World
{
    internal static class RegionalFurniture
    {
        public sealed class Piece
        {
            public readonly string Key, Name; public readonly StationKind Base; public readonly int Tile, W, Blocks, Torches; public readonly int[] Heights;
            public Piece(string key, string name, StationKind kind, int tile, int w, int[] heights, int blocks, int torches) { Key=key;Name=name;Base=kind;Tile=tile;W=w;Heights=heights;Blocks=blocks;Torches=torches; }
        }
        public static readonly Piece[] Pieces = {
            new Piece("Table", "桌", StationKind.Table, 14, 3, new[] { 16, 18 }, 8, 0),
            new Piece("Chair", "椅", StationKind.Chair, 15, 1, new[] { 16, 18 }, 4, 0),
            new Piece("WorkBench", "工作台", StationKind.WorkBench, 18, 2, new[] { 18 }, 10, 0),
            new Piece("Door", "门", StationKind.Door, 10, 1, new[] { 16, 16, 16 }, 6, 0),
            new Piece("Platform", "平台", StationKind.Platform, 19, 1, new[] { 16 }, 1, 0),
            new Piece("Bed", "床", StationKind.Bed, 79, 4, new[] { 16, 18 }, 15, 0),
            new Piece("Dresser", "梳妆台", StationKind.Dresser, 88, 3, new[] { 16, 16 }, 16, 0),
            new Piece("Bookcase", "书架", StationKind.Bookcase, 101, 3, new[] { 16, 16, 16, 16 }, 20, 0),
            new Piece("Sofa", "沙发", StationKind.Sofa, 89, 3, new[] { 16, 16 }, 5, 0),
            new Piece("Bathtub", "浴缸", StationKind.Bathtub, 90, 4, new[] { 16, 16 }, 14, 0),
            new Piece("Toilet", "马桶", StationKind.Toilet, 497, 1, new[] { 16, 18 }, 6, 0),
            new Piece("Sink", "水槽", StationKind.Sink, 172, 2, new[] { 16, 18 }, 6, 0),
            new Piece("Clock", "时钟", StationKind.Clock, 104, 2, new[] { 16, 16, 16, 16, 16 }, 10, 0),
            new Piece("Piano", "钢琴", StationKind.Piano, 87, 3, new[] { 16, 16 }, 15, 0),
            new Piece("Candle", "蜡烛", StationKind.Candle, 33, 1, new[] { 20 }, 4, 1),
            new Piece("Candelabra", "烛台", StationKind.Candelabra, 100, 2, new[] { 16, 16 }, 6, 3),
            new Piece("Chandelier", "吊灯", StationKind.Chandelier, 34, 3, new[] { 16, 16, 16 }, 4, 4),
            new Piece("Lantern", "灯笼", StationKind.Lantern, 42, 1, new[] { 16, 16 }, 6, 1),
            new Piece("Lamp", "落地灯", StationKind.Lamp, 93, 1, new[] { 16, 16, 16 }, 3, 1),
        };
        public const int PlatformIndex = 4, DresserIndex = 6, PerArea = 19, StationStride = 21;
        public static bool IsDresser(int style) => style >= 1001 && style <= 1024;
        public static int DresserStyle(int area) => 1001 + area;
        public static StationKind DresserKind(int style) => Kind(style-1001,DresserIndex);
        public static ItemType Item(int area, int piece) => ItemType.AreaKingsPassTable + area * PerArea + piece;
        public static StationKind Kind(int area, int piece) => (StationKind)(1000 + area * StationStride + piece);
        public static bool IsRegional(StationKind kind) => (int)kind >= 1000 && (int)kind < 1000 + AreaBlocks.All.Length * StationStride;
        public static int Area(StationKind kind) => ((int)kind - 1000) / StationStride;
        public static int Index(StationKind kind) => ((int)kind - 1000) % StationStride;
        public static StationKind BaseKind(StationKind kind) => !IsRegional(kind) ? kind : Index(kind) == 19 ? StationKind.DoorOpenRight : Index(kind) == 20 ? StationKind.DoorOpenLeft : Pieces[Index(kind)].Base;
        public static StationKind WithPose(StationKind kind, StationKind pose) => !IsRegional(kind) ? pose : Kind(Area(kind), pose == StationKind.DoorOpenRight ? 19 : pose == StationKind.DoorOpenLeft ? 20 : 3);
        public static int TileOf(StationKind kind) => Index(kind) >= 19 ? 11 : Pieces[Index(kind)].Tile;
        public static string Key(StationKind kind) => Index(kind) == 19 ? "DoorOpenRight" : Index(kind) == 20 ? "DoorOpenLeft" : Pieces[Index(kind)].Key;
        public static string Texture(StationKind kind) => "HkFurniture_" + AreaBlocks.All[Area(kind)].Key + "_" + Key(kind);
        public static string PlatformSheet(CellKind k) => k >= CellKind.AreaPlatformFirst && k <= CellKind.AreaPlatformLast ? "HkFurniturePlatform_" + AreaBlocks.All[(int)k - (int)CellKind.AreaPlatformFirst].Key : "Tiles_19";
        static readonly Color[] lightColors = {
            new Color(0.8751f,0.9204f,1.0000f), // KingsPass
            new Color(1.0000f,0.8168f,0.4321f), // Dirtmouth
            new Color(0.8779f,0.9180f,1.0000f), // HowlingCliffs
            new Color(0.8070f,0.8660f,1.0000f), // Crossroads
            new Color(0.8785f,1.0000f,0.6421f), // Greenpath
            new Color(1.0000f,0.8662f,0.2608f), // FungalWastes
            new Color(0.9688f,0.9484f,1.0000f), // FogCanyon
            new Color(0.5037f,1.0000f,0.5377f), // QueensGardens
            new Color(0.7086f,0.7737f,1.0000f), // CityOfTears
            new Color(0.7858f,1.0000f,0.9816f), // RoyalWaterways
            new Color(1.0000f,0.7147f,0.7420f), // CrystalPeak
            new Color(0.7972f,0.8609f,1.0000f), // RestingGrounds
            new Color(0.8438f,0.7885f,1.0000f), // Deepnest
            new Color(0.8444f,0.8934f,1.0000f), // BeastsDen
            new Color(1.0000f,0.9759f,0.9281f), // KingdomsEdge
            new Color(1.0000f,0.8331f,0.1955f), // Hive
            new Color(1.0000f,0.9719f,0.9012f), // AncientBasin
            new Color(0.8161f,0.8716f,1.0000f), // Abyss
            new Color(1.0000f,0.9980f,0.9804f), // WhitePalace
            new Color(0.5992f,0.7132f,1.0000f), // BlackEggTemple
            new Color(1.0000f,0.8905f,0.6210f), // Colosseum
            new Color(1.0000f,0.6676f,0.5876f), // GrimmTroupe
            new Color(1.0000f,0.9875f,0.9870f), // Dream
            new Color(1.0000f,0.8962f,0.5487f), // Godhome
        };
        public static Color LightColor(int area) => lightColors[area];
        static string Tooltip(Piece p)
        {
            if(p.Base==StationKind.Platform)return "可从下方跳上，按住下可穿过";
            if(p.Base==StationKind.Bed)return "右键设置或取消重生点";
            if(p.Base==StationKind.Chair||p.Base==StationKind.Sofa||p.Base==StationKind.Toilet)return "可放置\n可在房屋菜单中将居民安置在座位上";
            if(p.Base==StationKind.Door)return "右键开关；走近时自动开门";
            if(p.Base==StationKind.Dresser)return "右键打开 40 格储物空间\n空梳妆台可用镐挖回";
            if(p.Base==StationKind.Sink)return "在附近合成时可代替水";
            if(p.Base==StationKind.Clock)return "右键查看当前时间";
            if(p.Base==StationKind.WorkBench||p.Base==StationKind.Bookcase)return "可放置\n在附近可作为合成站";
            return p.Torches>0?"可放置\n发出与地区家具相配的彩色火焰和光":"可放置";
        }
        static readonly Dictionary<StationKind, Sprite> sprites = new Dictionary<StationKind, Sprite>();
        public static Sprite Sprite(StationKind kind)
        {
            if (sprites.TryGetValue(kind, out var sprite) && sprite != null) return sprite;
            var tex = TerrariaAssets.Texture(Texture(kind));
            if (tex == null) return null;
            sprite = UnityEngine.Sprite.Create(tex, new Rect(0,0,tex.width,tex.height),new Vector2(.5f,0),1f / Effects.TerraParticles.Px,0,SpriteMeshType.FullRect);
            return sprites[kind] = sprite;
        }
        public static void AddItems(List<ItemDef> list)
        {
            for (int a=0;a<AreaBlocks.All.Length;a++) for(int i=0;i<Pieces.Length;i++)
            {
                var p=Pieces[i];
                list.Add(new ItemDef { Type=Item(a,i),Name=AreaBlocks.All[a].Name+p.Name,Texture=Texture(Kind(a,i)),PlacesStation=i==PlatformIndex||i==DresserIndex?StationKind.None:Kind(a,i),PlacesChest=i==DresserIndex,ChestStyle=i==DresserIndex?DresserStyle(a):0,Places=i==PlatformIndex?(CellKind)((int)CellKind.AreaPlatformFirst+a):CellKind.Empty,Consumable=true,MaxStack=9999,Source=ItemSource.Craft,UseTime=10f/60f,UseAnimation=15f/60f,Tooltip=Tooltip(p) });
            }
        }
        public static IEnumerable<Recipe> Recipes()
        {
            for(int a=0;a<AreaBlocks.All.Length;a++) for(int i=0;i<Pieces.Length;i++)
            {
                var p=Pieces[i]; var station=i==PlatformIndex||p.Base==StationKind.WorkBench?"徒手":"工作台";
                if(p.Torches>0) yield return new Recipe(Item(a,i),1,station,0,(AreaBlocks.BlockItem(a),p.Blocks),(ItemType.Torch,p.Torches));
                else yield return new Recipe(Item(a,i),i==PlatformIndex?2:1,station,0,(AreaBlocks.BlockItem(a),p.Blocks));
            }
        }
    }
}
