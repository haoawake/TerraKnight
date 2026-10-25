using System.Collections.Generic;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.World
{
    // A diagonal platform is a continuous narrow deck. The old L-shaped atlas frames
    // are NOT complete diagonal sprites: drawing them unchanged creates tile-sized steps.
    internal static class RegionalPlatformVisuals
    {
        public static bool IsRegional(CellKind kind) => kind>=CellKind.AreaPlatformFirst&&kind<=CellKind.AreaPlatformLast;
        static readonly Dictionary<(CellKind,int,bool),Sprite> stairs=new Dictionary<(CellKind,int,bool),Sprite>();
        public static Sprite Stair(CellKind kind,int slope,bool clipBelow)
        {
            var key=(kind,slope,clipBelow);if(stairs.TryGetValue(key,out var sprite))return sprite;
            var source=TerrariaAssets.Pixels(RegionalFurniture.PlatformSheet(kind),out int width,out int height);
            // Eight pixels of underside must survive below a tile's low edge. A 16x16
            // crop would remove them and leave a gap every time the next tile begins.
            var pixels=new Color32[16*24];
            for(int x=0;x<16;x++)for(int depth=0;depth<8;depth++)
            {
                int down=(slope==1?x:15-x)+depth;
                if(clipBelow&&down>=16)continue;
                pixels[(23-down)*16+x]=source[(height-1-depth)*width+x];
            }
            var texture=TerrariaAssets.Make(RegionalFurniture.PlatformSheet(kind)+"#continuous"+slope,pixels,16,24);
            // Show positions sprites at cell centres: top is +8px, bottom extends -16px.
            sprite=Sprite.Create(texture,new Rect(0,0,16,24),new Vector2(.5f,16f/24f),CellVisuals.PixelsPerUnit,0,SpriteMeshType.FullRect);
            stairs[key]=sprite;return sprite;
        }
    }
}
