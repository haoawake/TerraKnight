using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace TerraKnight.Player
{
    /// <summary>Character-only cutscene art. Native clips and FSM events keep their original timing.</summary>
    internal static class HeroSpecialAnimations
    {
        public static readonly string[] Sheets = { "absorb_magic", "wake_ground", "focus", "collect_ability", "read_map", "roar_stagger", "dream_warp" };
        public static readonly int[] Counts = { 12,9,8,8,8,8,8 };
        public readonly struct Range
        {
            public readonly int Kind, First, Last;
            /// <summary>Played through once, a sheet frame per native frame, then held on its last (over a looping clip).</summary>
            public readonly bool Once;
            public Range(int kind,int first,int last,bool once=false) { Kind=kind;First=first;Last=last;Once=once; }
        }
        public static readonly Dictionary<string,Range> Clips = new Dictionary<string,Range>(StringComparer.Ordinal)
        {
            ["Collect Magical 1"]=new Range(0,0,6,once:true), ["Collect Magical 2"]=new Range(0,6,6),
            ["Collect Magical 3"]=new Range(0,7,9), ["Collect Magical 3b"]=new Range(0,7,9),
            ["Collect Magical Fall"]=new Range(0,10,10), ["Collect Magical Land"]=new Range(0,11,11),
            ["Wake Up Ground"]=new Range(1,0,8), ["Respawn Wake"]=new Range(1,0,8),
            ["Prostrate"]=new Range(1,0,0), ["Prostrate Rise"]=new Range(1,1,8),
            ["Focus"]=new Range(2,2,5), ["Focus Get"]=new Range(2,3,5), ["Focus Get Once"]=new Range(2,3,5), ["Focus End"]=new Range(2,6,7),
            ["Collect Normal 1"]=new Range(3,0,2), ["Collect Normal 2"]=new Range(3,5,5), ["Collect Normal 3"]=new Range(3,6,7),
            ["Collect Heart Piece"]=new Range(3,0,5), ["Collect Heart Piece End"]=new Range(3,6,7),
            ["Collect Shadow"]=new Range(3,0,7), ["Collect Acid"]=new Range(3,0,7), ["Scream 2 Get"]=new Range(3,0,7),
            ["Collect SD 1"]=new Range(3,0,2), ["Collect SD 1 Back"]=new Range(3,0,2),
            ["Collect SD 2"]=new Range(3,5,5), ["Collect SD 3"]=new Range(3,5,6), ["Collect SD 4"]=new Range(3,6,7),
            ["Collect StandToIdle"]=new Range(3,6,7), ["DJ Get Land"]=new Range(3,6,7),
            ["Map Open"]=new Range(4,0,3), ["Map Idle"]=new Range(4,4,5), ["Map Away"]=new Range(4,6,7), ["Map Update"]=new Range(4,4,5),
            ["Roar Lock"]=new Range(5,2,5), ["Stun"]=new Range(5,4,4),
            ["DG Warp Charge"]=new Range(6,0,4), ["DG Warp"]=new Range(6,5,5),
            ["DG Warp Cancel"]=new Range(6,6,7), ["DG Warp In"]=new Range(6,5,7),
        };
        static readonly Sprite[][] sprites = new Sprite[7][];

        // The existing signed-byte body field has unused negative values. Keep the packet layout,
        // and let the protocol revision reject clients that cannot decode these poses.
        public static int Encode(int kind,int frame) => -2-kind*12-frame;
        public static bool Decode(int code,out int kind,out int frame)
        {
            int value=-2-code;kind=value/12;frame=value%12;
            return code<=-2 && value>=0 && kind<Counts.Length && frame<Counts[kind];
        }
        public static int FrameFor(Range range,int nativeFrame,int nativeCount)
        {
            if(range.Once) return range.First+Mathf.Clamp(nativeFrame,0,range.Last-range.First);
            float t=nativeCount<=1?0f:Mathf.Clamp01(nativeFrame/(float)(nativeCount-1));
            return Mathf.RoundToInt(Mathf.Lerp(range.First,range.Last,t));
        }
        /// <summary>The native frame a range is read at: for one played once, the frames gone by since the clip began, its loops counted.</summary>
        public static int NativeFrame(Range range,tk2dSpriteAnimator anim,tk2dSpriteAnimationClip clip) =>
            range.Once ? Mathf.FloorToInt(anim.ClipTimeSeconds*(clip.fps>0f?clip.fps:1f)) : anim.CurrentFrame;
        public static int Code(HeroController hero)
        {
            if(hero==null || Mounts.Riding) return -1;
            var anim=hero.GetComponent<tk2dSpriteAnimator>();
            var clip=anim!=null?anim.CurrentClip:null;
            if(clip==null || !Clips.TryGetValue(clip.name,out var range)) return -1;
            // Bench and walking map poses already use Terraria's seated/walking layers.
            if(range.Kind==4 && (PlayerData.instance.atBench || Mathf.Abs(hero.GetComponent<Rigidbody2D>().linearVelocity.x)>.1f)) return -1;
            return Encode(range.Kind,FrameFor(range,NativeFrame(range,anim,clip),clip.frames.Length));
        }
        public static Sprite Frame(int kind,int frame)
        {
            if(sprites[kind]==null)
            {
                using(var stream=typeof(HeroSpecialAnimations).Assembly.GetManifestResourceStream("TerraKnight.heroanim_"+Sheets[kind]+".png"))
                {
                    if(stream==null) throw new FileNotFoundException("heroanim_"+Sheets[kind]);
                    using(var data=new MemoryStream())
                    {
                        stream.CopyTo(data);
                        var tex=new Texture2D(2,2,TextureFormat.RGBA32,false) { name="TerraKnight/Hero/"+Sheets[kind],filterMode=FilterMode.Point,wrapMode=TextureWrapMode.Clamp };
                        ImageConversion.LoadImage(tex,data.ToArray(),false);
                        sprites[kind]=new Sprite[Counts[kind]];
                        for(int i=0;i<Counts[kind];i++) sprites[kind][i]=Sprite.Create(tex,new Rect(0,tex.height-(i+1)*96,96,96),new Vector2(.5f,14f/96f),World.CellVisuals.PixelsPerUnit,0,SpriteMeshType.FullRect);
                    }
                }
            }
            return sprites[kind][frame];
        }
    }
}
