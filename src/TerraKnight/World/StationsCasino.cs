using TerraKnight.Items;
using UnityEngine;
namespace TerraKnight.World
{
    internal sealed partial class Stations
    {
        static void AddCasinoSpecs()
        {
            foreach(var k in new[]{StationKind.CasinoSlots,StationKind.CasinoPinball,StationKind.CasinoRoulette})
                specs[k]=new Spec{Texture=Casino.TextureOf(k),W=3,H=4,Heights=new[]{16,16,16,16},Item=k==StationKind.CasinoSlots?ItemType.CasinoSlots:k==StationKind.CasinoPinball?ItemType.CasinoPinball:ItemType.CasinoRoulette,Counts=new string[0]};
        }
        public static Vector2 SizeOf(StationKind kind){if(!specs.TryGetValue(kind,out var spec))return Vector2.zero;int h=0;foreach(int row in spec.Heights)h+=row;return new Vector2(spec.W*16f*Px,h*Px);}
    }
}
