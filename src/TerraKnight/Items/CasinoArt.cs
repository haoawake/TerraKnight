using TerraKnight.World;
using UnityEngine;
namespace TerraKnight.Items
{
    internal static class CasinoAnimation
    {
        public static float Ease(float t){t=Mathf.Clamp01(t);return 1-Mathf.Pow(1-t,4);}
        public static float Reel(int target,int reel,float t,long sequence)
        {
            float p=Mathf.Clamp01(t/(.70f+reel*.15f));float bounce=p>.88f?Mathf.Sin((p-.88f)/.12f*Mathf.PI*2)*.07f*(1-p)/.12f:0;
            return target+(24+reel*7+(int)(sequence%5))*(1-Ease(p))+bounce;
        }
        public static Vector2 Pin(int sample,int multiplier,float t)
        {
            float landing=(sample+.5f)/(100*multiplier);float x=Mathf.Lerp(.5f,landing,t)+Mathf.Sin(t*47)*.15f*(1-t)+Mathf.Sin(t*83)*.035f*(1-t);
            return new Vector2(Mathf.Clamp01(x),Mathf.Pow(t,1.17f));
        }
        public static float Wheel(int sample,float t)=>Mathf.Lerp(-1440,-CasinoArt.VisualSector(sample)*360f/Casino.WheelSectors,Ease(t));
        public static Vector2 Ball(float t){float a=Mathf.Lerp(2160,0,Ease(t))*Mathf.Deg2Rad;float r=Mathf.Lerp(.91f,.785f,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.45f,1,t)));return new Vector2(Mathf.Sin(a),-Mathf.Cos(a))*r;}
    }
    internal static class CasinoArt
    {
        // The reel symbols, the strip's cells in this order (CasinoPx_ReelL/ReelS).
        public static readonly string[] Symbols={"Item_8","Item_181","Item_75","Item_327","Item_4956"};
        public static string Key(StationKind k)=>CasinoPx.Machine(k);
        // Every picture is pixel art (CasinoPx): the furniture is the 48x64 world-size machine, 32 px a unit like Terraria's tiles.
        public static Sprite Sprite(StationKind k)=>CasinoPx.Sprite(Key(k)+"S",new Vector2(.5f,0));
        public static Texture2D Preview(StationKind kind)=>CasinoPx.Tex(Key(kind)+"S");
        public static Texture2D Texture(string name)
        {
            if(name=="Casino_Wheel")return CasinoPx.Tex("WheelL");if(name=="Casino_Ball")return CasinoPx.Tex("BallL");if(name=="Casino_Pegs")return CasinoPx.Tex("PegsL");
            return CasinoPx.Tex((name=="Casino_Slots"?"Slots":name=="Casino_Pinball"?"Pinball":"Roulette")+"S");
        }
        public static Texture2D Machine(StationKind kind)=>CasinoPx.Tex(Key(kind)+"L");
        public static Texture2D Button(int state)=>CasinoPx.Tex("Button"+Mathf.Clamp(state,0,4));
        public static Texture2D CharmBall(int denomination)=>CasinoPx.Tex("Marble"+Mathf.Clamp(denomination,0,5));
        public static int VisualSector(int outcome)=>outcome==0?0:outcome<=32?outcome*2-1:(outcome-32)*2;
    }
}
