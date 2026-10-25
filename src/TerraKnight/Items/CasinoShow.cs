using System;
using System.Collections.Generic;
using System.IO;
using TerraKnight.Net;
using TerraKnight.World;
using UnityEngine;
namespace TerraKnight.Items
{
    /// <summary>Cosmetic round broadcasts only. Receiving one never reads or mutates a wallet or the saved ledger. The machines' animation and effects are CasinoWorld's.</summary>
    internal static class CasinoShow
    {
        internal sealed class Round
        {
            public string Scene;
            public float X,Y,Elapsed;
            public StationKind Kind;
            public long Sequence,Stake,Payout;
            public int Multiplier=1,Denomination,Wheel,Choice;
            public int[] Reels=new int[3];
            // 1: all in; 2: a pinball launch (its seed in Wheel; Clock, Force: the machine's clock then and the strike).
            public byte Flags;
            public double Clock,Force;
            public bool AllIn=>(Flags&1)!=0;
            public bool Launch=>(Flags&2)!=0;
            // A pinball round is one marble and all that came of it in (Wheel: what they won, in tenths): shown at once.
            public float Duration=>Kind==StationKind.CasinoSlots?2.6f:Kind==StationKind.CasinoPinball?.05f:3f;
            public void Write(BinaryWriter w)
            {
                w.Write(Scene);w.Write(X);w.Write(Y);w.Write((ushort)Kind);w.Write(Sequence);w.Write(Elapsed);w.Write(Stake);w.Write(Payout);
                w.Write((ushort)Multiplier);w.Write((byte)Denomination);w.Write(Wheel);w.Write((byte)Choice);for(int i=0;i<3;i++)w.Write((byte)Reels[i]);
                w.Write(Flags);w.Write(Clock);w.Write(Force);
            }
            public static Round Read(BinaryReader r)=>new Round{Scene=r.ReadString(),X=r.ReadSingle(),Y=r.ReadSingle(),Kind=(StationKind)r.ReadUInt16(),Sequence=r.ReadInt64(),Elapsed=r.ReadSingle(),Stake=r.ReadInt64(),Payout=r.ReadInt64(),Multiplier=r.ReadUInt16(),Denomination=r.ReadByte(),Wheel=r.ReadInt32(),Choice=r.ReadByte(),Reels=new[]{(int)r.ReadByte(),r.ReadByte(),r.ReadByte()},
                Flags=r.ReadByte(),Clock=r.ReadDouble(),Force=r.ReadDouble()};
            public bool Valid()
            {
                if(string.IsNullOrEmpty(Scene)||Scene.Length>128||!Casino.IsMachine(Kind)||Sequence<=0||!Finite(X)||!Finite(Y)||!Finite(Elapsed)||Elapsed<0||Elapsed>10||Math.Abs(X)>1000000||Math.Abs(Y)>1000000||Reels==null||Reels.Length!=3)return false;
                foreach(int n in Reels)if(n<0||n>4)return false;
                if(Multiplier<1||Multiplier>Casino.MaxMultiplier||Denomination<0||Denomination>Casino.AllIn||Choice<0||Choice>2||Flags>3||!Casino.ValidCopper(Stake))return false;
                if(double.IsNaN(Clock)||double.IsInfinity(Clock)||double.IsNaN(Force)||Force<0||Force>1)return false;
                if(Kind==StationKind.CasinoPinball)
                {
                    if(Denomination<Casino.AllIn&&Stake!=Casino.Denominations[Denomination]*100)return false;
                    return Launch?Payout==0:Wheel>=0&&Wheel<=PinSim.MostTenths&&Payout==Casino.TenthsPayout(Stake,Wheel);
                }
                if(Launch)return false;
                return Kind==StationKind.CasinoSlots?Payout==Casino.SlotPayout(Stake,Reels[0],Reels[1],Reels[2]):Wheel>=0&&Wheel<Casino.WheelSectors&&Payout==Casino.WheelPayout(Stake,(Casino.Colour)Choice,Wheel);
            }
            static bool Finite(float f)=>!float.IsNaN(f)&&!float.IsInfinity(f);
        }
        sealed class View
        {
            public int Owner;public Round Round;public float Began,NextSend;public bool Local,Announced,HasBox;public Rect Box;
        }
        static readonly List<View> views=new List<View>();
        static readonly Dictionary<int,long> latest=new Dictionary<int,long>();
        public static int DevReceived,DevStarted,DevResults;
        public static int DevViews=>views.Count;
        // The machines' own moving parts and effects are CasinoWorld's (every machine, idle or playing).
        public static int DevWorldSprites=>CasinoWorld.DevSprites;
        public static void PlayerLeft(int owner){latest.Remove(owner);for(int i=views.Count-1;i>=0;i--)if(views[i].Owner==owner)views.RemoveAt(i);CasinoPinball.PlayerLeft(owner);}
        public static void Clear(){views.Clear();latest.Clear();CasinoWorld.Clear();CasinoPinball.Clear();}
        public static void Start(StationKind kind,string scene,Rect box,Casino.Saved saved)
        {
            var region=TerrainSystem.Find(scene);if(region==null)return;
            var local=new Vector2(box.center.x,box.y)-(Vector2)region.Root.position;
            var round=new Round{Scene=scene,X=local.x,Y=local.y,Kind=kind,Sequence=saved.Sequence,Stake=saved.LastStake,Payout=saved.LastPayout,
                Multiplier=1,Denomination=0,Wheel=saved.LastWheel,Choice=saved.LastColour,Reels=(int[])saved.Reels.Clone(),Flags=(byte)(saved.LastAllIn?1:0)};
            if(!round.Valid())return;
            Add(NetSession.Active?NetSession.MySlot:-1,round,true);Send(round);
        }
        /// <summary>One of our marbles and all that came of it in: shown over its machine for everyone.</summary>
        public static void StartPinball(Casino.Marble m,int tenths,long payout,long sequence)
        {
            var round=new Round{Scene=m.Scene,X=m.X,Y=m.Y,Kind=StationKind.CasinoPinball,Sequence=sequence,Stake=m.Price,Payout=payout,Denomination=m.Denomination,Wheel=tenths,
                Flags=(byte)(m.Denomination==Casino.AllIn?1:0)};
            if(!round.Valid())return;
            Add(NetSession.Active?NetSession.MySlot:-1,round,true);Send(round);
        }
        /// <summary>One of our marbles struck: the others run its flight on their own screens.</summary>
        public static void SendLaunch(Casino.Marble m)
        {
            var round=new Round{Scene=m.Scene,X=m.X,Y=m.Y,Kind=StationKind.CasinoPinball,Sequence=Casino.Sequence,Stake=Math.Max(100,m.Price),Denomination=m.Denomination,
                Wheel=unchecked((int)m.Seed),Flags=(byte)(2|(m.Denomination==Casino.AllIn?1:0)),Clock=m.Clock,Force=m.Force};
            if(round.Valid())Send(round);
        }
        static void Send(Round round)
        {
            if(!NetSession.Active)return;var p=NetSession.Mine(Msg.CasinoRound);round.Write(p.W);NetSession.Send(p.Done());
        }
        public static void Receive(int owner,BinaryReader reader)
        {
            var round=Round.Read(reader);if(owner==NetSession.MySlot||!round.Valid())return;
            if(round.Launch){CasinoPinball.RemoteLaunch(owner,round);DevReceived++;return;}
            if(Add(owner,round,false))DevReceived++;
        }
        static bool Add(int owner,Round round,bool local)
        {
            if(latest.TryGetValue(owner,out long seq)&&round.Sequence<seq)return false;
            foreach(var v in views)if(v.Owner==owner&&v.Round.Sequence==round.Sequence)
            {
                if(v.Round.Kind!=round.Kind||v.Round.Scene!=round.Scene||v.Round.X!=round.X||v.Round.Y!=round.Y)return false;
                // Heartbeats catch a player arriving mid-round up to the same animation, without replaying a result.
                float elapsed=Time.realtimeSinceStartup-v.Began;if(round.Elapsed>elapsed)v.Began=Time.realtimeSinceStartup-round.Elapsed;return false;
            }
            if(latest.TryGetValue(owner,out seq)&&round.Sequence==seq)return false;
            latest[owner]=round.Sequence;
            // One actor's newest round replaces its own previous display. Different actors remain independent.
            for(int i=views.Count-1;i>=0;i--)if(views[i].Owner==owner)views.RemoveAt(i);
            if(views.Count>=32)views.RemoveAt(0);
            views.Add(new View{Owner=owner,Round=round,Local=local,Began=Time.realtimeSinceStartup-round.Elapsed,NextSend=Time.realtimeSinceStartup+1});DevStarted++;return true;
        }
        static bool Find(Round round,out Rect box)
        {
            box=default;var region=TerrainSystem.Find(round.Scene);if(region==null)return false;var world=new Vector2(round.X,round.Y)+(Vector2)region.Root.position;
            foreach(var s in Stations.Standing(k=>k==round.Kind))if((new Vector2(s.Box.center.x,s.Box.y)-world).sqrMagnitude<.0001f){box=s.Box;return true;}return false;
        }
        /// <summary>The other players' rounds at a machine (its kind and box), with how long each has run and whose (for the open window).</summary>
        public static void OthersAt(StationKind kind,Rect box,List<(Round Round,float Age,string Who)> into)
        {
            into.Clear();
            foreach(var v in views)
            {
                if(v.Local||!v.HasBox||v.Round.Kind!=kind||(v.Box.center-box.center).sqrMagnitude>=.0001f)continue;
                into.Add((v.Round,Time.realtimeSinceStartup-v.Began,NetSession.Player(v.Owner)?.Name??("玩家"+(v.Owner+1))));
            }
        }
        /// <summary>The newest round shown at a machine (its kind and box): how long it has run, whether it is this player's.</summary>
        public static bool ShowingAt(StationKind kind,Rect box,out Round round,out float age,out bool local)
        {
            round=null;age=0;local=false;float newest=float.MaxValue;
            foreach(var v in views)
            {
                if(!v.HasBox||v.Round.Kind!=kind||(v.Box.center-box.center).sqrMagnitude>=.0001f)continue;
                float a=Time.realtimeSinceStartup-v.Began;if(a>=newest)continue;newest=a;round=v.Round;age=a;local=v.Local;
            }
            return round!=null;
        }
        public static void Tick()
        {
            for(int i=views.Count-1;i>=0;i--)
            {
                var v=views[i];var r=v.Round;float age=Time.realtimeSinceStartup-v.Began;
                if(age>r.Duration+6){views.RemoveAt(i);continue;}
                if(v.Local&&Time.realtimeSinceStartup>=v.NextSend){r.Elapsed=age;Send(r);v.NextSend=Time.realtimeSinceStartup+1;}
                v.HasBox=Find(r,out v.Box);if(!v.HasBox)continue;
                if(age>=r.Duration&&!v.Announced){v.Announced=true;DevResults++;CasinoWorld.Result(r.Kind,v.Box,r,v.Local);}
            }
            CasinoWorld.Tick();
        }
        // Over a machine when a round ends: its word (CasinoWorld's), no plate.
        public static void Draw() => CasinoWorld.DrawGui();
    }
}
