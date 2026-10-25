"""Append-only IDs and native furniture registration. No existing item IDs are moved."""
from pathlib import Path
import json,re
root=Path(__file__).resolve().parents[1]
bundle=root/'空洞骑士地区家具素材包_v2_差异造型'
spec=json.loads((bundle/'原版参考/spec.json').read_text('utf-8'))
furn=[s for s in spec['furniture'] if s['key']!='Chest']
areas=spec['areas']
def edit(file,old,new):
 p=root/file;s=p.read_text('utf-8');assert old in s,(file,old[:100]);p.write_text(s.replace(old,new),encoding='utf-8')
names=['Area'+a+s['key'] for a,n in areas for s in furn]
edit('src/TerraKnight/Items/Items.cs','WaterBucket, AcidBucket, BottomlessWaterBucket, BottomlessAcidBucket, SuperAbsorbantSponge, AcidAbsorbantSponge,','WaterBucket, AcidBucket, BottomlessWaterBucket, BottomlessAcidBucket, SuperAbsorbantSponge, AcidAbsorbantSponge,\n\n        // Append only: 24 regional furniture sets, 19 craftable items each.\n        '+',\n        '.join(', '.join(names[i:i+5]) for i in range(0,len(names),5))+',')
edit('src/TerraKnight/Items/Items.cs','World.AreaBlocks.AddItems(list);','World.AreaBlocks.AddItems(list);\n            World.RegionalFurniture.AddItems(list);')
edit('src/TerraKnight/Items/Recipes.cs','result.AddRange(World.AreaBlocks.Recipes());','result.AddRange(World.AreaBlocks.Recipes());\n            result.AddRange(World.RegionalFurniture.Recipes());')
# Make platform physics/framing shared by both wood and region-specific grid kinds.
for p in (root/'src/TerraKnight').rglob('*.cs'):
 if p.name.startswith('DevAutoTest'):continue
 s=p.read_text('utf-8');original=s
 matches=list(re.finditer(r'\s*(==|!=)\s*CellKind\.Platform\b',s))
 for m in reversed(matches):
  end=m.start();i=end-1
  while i>=0 and s[i].isspace():i-=1
  depth=0;last=i+1
  while i>=0:
   c=s[i]
   if c in ')]':depth+=1
   elif c in '([':
    if depth:depth-=1
    else:break
   elif depth==0 and not (c.isalnum() or c in '._'):break
   i-=1
  start=i+1;expr=s[start:last]
  assert expr,(p,m.group())
  s=s[:start]+('!' if m.group(1)=='!=' else '')+'TerraKnight.World.Cells.IsPlatform('+expr+')'+s[m.end():]
 if s!=original:p.write_text(s,encoding='utf-8')
edit('src/TerraKnight/World/CellGrid.cs','AreaBlockLast = 55,','AreaBlockLast = 55,\n        AreaPlatformFirst = 64, AreaPlatformLast = 87,')
edit('src/TerraKnight/World/CellGrid.cs','internal static class Cells\n    {','internal static class Cells\n    {\n        public static bool IsPlatform(CellKind k) => k == CellKind.Platform || (k >= CellKind.AreaPlatformFirst && k <= CellKind.AreaPlatformLast);')
edit('src/TerraKnight/World/AreaBlocks.cs','if (kind == CellKind.Torch) return RegionalTorches.ItemAt(cell);','if (kind == CellKind.Torch) return RegionalTorches.ItemAt(cell);\n            if (kind >= CellKind.AreaPlatformFirst && kind <= CellKind.AreaPlatformLast) return RegionalFurniture.Item((int)kind - (int)CellKind.AreaPlatformFirst, RegionalFurniture.PlatformIndex);')
edit('src/TerraKnight/World/CellVisuals.cs','GetShapedSprite("Tiles_19", fx, 0, CellGrid.HalfBrick) : GetSprite("Tiles_19", fx, 0, TileFraming.FrameSize)','GetShapedSprite(RegionalFurniture.PlatformSheet(kind), fx, 0, CellGrid.HalfBrick) : GetSprite(RegionalFurniture.PlatformSheet(kind), fx, 0, TileFraming.FrameSize)')
edit('src/TerraKnight/World/CellVisuals.cs','GetSprite("Tiles_19", brace, 0, TileFraming.FrameSize)','GetSprite(RegionalFurniture.PlatformSheet(kind), brace, 0, TileFraming.FrameSize)')
edit('src/TerraKnight/World/Housing.cs','default: return 1;','default: return Cells.IsPlatform(k) ? 19 : 1;')
edit('src/TerraKnight/World/Stations.cs','enum StationKind : byte','enum StationKind : ushort')
edit('src/TerraKnight/World/Stations.cs','Door, DoorOpenRight, DoorOpenLeft, Table,','Door, DoorOpenRight, DoorOpenLeft, Table,\n        Dresser, Sofa, Bathtub, Toilet, Sink, Clock, Piano, Candle, Candelabra, Chandelier, Lantern, Lamp,\n        RegionalFirst = 1000, RegionalLast = 1503,')
edit('src/TerraKnight/World/Stations.cs','public string Custom;','public string Custom;\n            public string Native;\n            public bool Ceiling;')
edit('src/TerraKnight/World/Stations.cs','AddHousingSpecs();','AddHousingSpecs();\n            AddRegionalSpecs();')
edit('src/TerraKnight/World/Stations.cs','if (s.Spec.Custom != null)\n                    {','if (s.Spec.Custom != null)\n                    {') # keep relics separate
edit('src/TerraKnight/World/Stations.cs','s.Renderer.sprite = SpriteOf(s.Kind, s.Right);','s.Renderer.sprite = SpriteOf(s.Kind, s.Right);\n                s.Renderer.flipX = s.Spec.Native != null && s.Spec.Faces && s.Right;\n                UpdateRegionalLight(s);')
edit('src/TerraKnight/World/Stations.cs','var spec = specs[kind];\n            right &=','var spec = specs[kind];\n            if (spec.Native != null) return RegionalFurniture.Sprite(kind);\n            right &=')
# Functional identity remains the vanilla kind while the regional style is retained in save/net kind.
for file in ['src/TerraKnight/World/Stations.cs','src/TerraKnight/World/StationsHousing.cs']:
 p=root/file;s=p.read_text('utf-8')
 s=re.sub(r's\.Kind (==|!=) StationKind\.(Bed|Chair|Table|WorkBench|Door|DoorOpenRight|DoorOpenLeft)\b',lambda m:'RegionalFurniture.BaseKind(s.Kind) '+m[1]+' StationKind.'+m[2],s)
 if file.endswith('StationsHousing.cs'):
  s=s.replace('k == StationKind.Door','RegionalFurniture.BaseKind(k) == StationKind.Door')
  s=s.replace('s.Kind = direction == -1 ? StationKind.DoorOpenLeft : StationKind.DoorOpenRight;','s.Kind = RegionalFurniture.WithPose(s.Kind, direction == -1 ? StationKind.DoorOpenLeft : StationKind.DoorOpenRight);')
  s=s.replace('s.Kind = StationKind.Door;','s.Kind = RegionalFurniture.WithPose(s.Kind, StationKind.Door);')
  s=s.replace('var tex = s.Spec.Texture;','if (RegionalFurniture.IsRegional(s.Kind)) return RegionalFurniture.TileOf(s.Kind);\n            var tex = s.Spec.Texture;')
 p.write_text(s,encoding='utf-8')
# Ceiling fixtures use top anchors instead of the ground placement path.
edit('src/TerraKnight/World/Stations.cs','if (!SpotGround(x, aim.y + 0.25f, out float y))','float y;\n            if (spec.Ceiling ? !CeilingSpot(x, aim.y, spec, out y) : !SpotGround(x, aim.y + 0.25f, out y))')
edit('src/TerraKnight/World/Stations.cs','for (int i = 0; i < spec.W; i++)\n            {\n                float cx = x - w / 2f','for (int i = 0; !spec.Ceiling && i < spec.W; i++)\n            {\n                float cx = x - w / 2f')
edit('src/TerraKnight/World/Stations.cs','static bool HasGround(Station s)\n        {','static bool HasGround(Station s)\n        {\n            if (s.Spec.Ceiling) return SolidTileAt(s.Box.center.x, s.Box.yMax, true);')
# Source-generated metadata, no table lookups during ItemDb initialization.
costs={'Table':8,'Chair':4,'WorkBench':10,'Door':6,'Platform':1,'Bed':15,'Dresser':16,'Bookcase':20,'Sofa':5,'Bathtub':14,'Toilet':6,'Sink':6,'Clock':10,'Piano':15,'Candle':4,'Candelabra':6,'Chandelier':4,'Lantern':6,'Lamp':3}
flames={'Candle':1,'Candelabra':3,'Chandelier':4,'Lantern':1,'Lamp':1}
rows=[]
for s in furn:
 k=s['key'];base='WorkBench' if k=='WorkBench' else k
 rows.append('            new Piece("%s", "%s", StationKind.%s, %d, %d, new[] { %s }, %d, %d),'%(k,s['name'],base,s['tile'],s['tiles'][0],', '.join(map(str,s['coordinate_heights'])),costs[k],flames.get(k,0)))
code='''using System.Collections.Generic;
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
ROWS
        };
        public const int PlatformIndex = 4, PerArea = 19, StationStride = 21;
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
                list.Add(new ItemDef { Type=Item(a,i),Name=AreaBlocks.All[a].Name+p.Name,Texture=Texture(Kind(a,i)),PlacesStation=i==PlatformIndex?StationKind.None:Kind(a,i),Places=i==PlatformIndex?(CellKind)((int)CellKind.AreaPlatformFirst+a):CellKind.Empty,Consumable=true,MaxStack=9999,Source=ItemSource.Craft,UseTime=10f/60f,UseAnimation=15f/60f,Tooltip=i==PlatformIndex?"可从下方跳上，按住下可穿过":"可放置\\n用对应地区方块制作" });
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
'''.replace('ROWS','\n'.join(rows))
(root/'src/TerraKnight/World/RegionalFurniture.cs').write_text(code,encoding='utf-8')
print('Registered',len(names),'items and recipes; append-only IDs')
