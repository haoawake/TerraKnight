using System;
using System.Collections.Generic;
using TerraKnight.Net;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The pinball machines' marbles in flight, every machine, its window open or not: ours (struck from our window, each
    /// settling as it falls into its channel) and other players' (their launches run here the same way, to be seen). Each
    /// player's marbles at a machine fly together (knocking each other) in one simulation of their own.
    /// </summary>
    internal static class CasinoPinball
    {
        public sealed class Info
        {
            public long Id;
            public int Denomination;
            public long Price;
            public bool Ours;
            public string Who="";
            // Knocks heard so far (the window plays a tink for new ones).
            public int Heard;
        }
        public sealed class Machine
        {
            public string Scene;
            public Vector2 Local;
            // -1: ours.
            public int Owner;
            public string Who="";
            public readonly PinSim Sim=new PinSim();
            public readonly Queue<CasinoShow.Round> Waiting=new Queue<CasinoShow.Round>();
            public double Owed;
            public float Idle;
        }
        static readonly List<Machine> machines=new List<Machine>();
        public static int DevMachines=>machines.Count;

        public static void Clear()=>machines.Clear();
        public static void PlayerLeft(int owner){for(int i=machines.Count-1;i>=0;i--)if(machines[i].Owner==owner)machines.RemoveAt(i);}

        /// <summary>Our own machine at a place (made when first wanted, its clock running from the time it is made).</summary>
        public static Machine Ours(string scene,Vector2 local)
        {
            foreach(var m in machines)if(m.Owner<0&&m.Scene==scene&&(m.Local-local).sqrMagnitude<.0001f)return m;
            var made=new Machine{Scene=scene,Local=local,Owner=-1};made.Sim.Clock=Math.Round(Time.realtimeSinceStartup/PinSim.Dt)*PinSim.Dt;machines.Add(made);return made;
        }
        /// <summary>Strikes our next loaded marble at a machine: the ledger first (its random numbers, the all-in money), then the flight.</summary>
        public static bool Strike(Machine m,double force)
        {
            var marble=Casino.Launch(force,m.Sim.Clock);if(marble==null)return false;
            var info=new Info{Id=marble.Id,Denomination=marble.Denomination,Price=marble.Price,Ours=true};
            m.Sim.Launch(marble.Force,marble.Seed,info);
            CasinoShow.SendLaunch(marble);
            return true;
        }
        /// <summary>Another player's launch: run here from its own clock (a quarter second behind, so a late message is still in time).</summary>
        public static void RemoteLaunch(int owner,CasinoShow.Round r)
        {
            Machine m=null;var local=new Vector2(r.X,r.Y);
            foreach(var x in machines)if(x.Owner==owner&&x.Scene==r.Scene&&(x.Local-local).sqrMagnitude<.0001f){m=x;break;}
            if(m==null){m=new Machine{Scene=r.Scene,Local=local,Owner=owner};m.Sim.Clock=r.Clock-.25;machines.Add(m);}
            m.Who=NetSession.Player(owner)?.Name??("玩家"+(owner+1));
            if(m.Waiting.Count<64)m.Waiting.Enqueue(r);
        }

        public static void Tick()
        {
            if(machines.Count==0)return;
            if(GameManager.instance!=null&&GameManager.instance.isPaused)return;
            double dt=Math.Min(.1,Time.unscaledDeltaTime);
            for(int i=machines.Count-1;i>=0;i--)
            {
                var m=machines[i];var sim=m.Sim;
                sim.ClearSeen();
                m.Owed+=dt;int steps=0;
                while(m.Owed>=PinSim.Dt&&steps<960)
                {
                    while(m.Waiting.Count>0&&sim.Clock>=m.Waiting.Peek().Clock-PinSim.Dt/2)
                    {
                        var r=m.Waiting.Dequeue();
                        // On the owner's own clock to the last bit (its steps from here are the owner's steps).
                        if(Math.Abs(sim.Clock-r.Clock)<PinSim.Dt)sim.Clock=r.Clock;
                        sim.Launch(r.Force,unchecked((uint)r.Wheel),new Info{Denomination=r.Denomination,Price=r.Stake,Who=m.Who});
                    }
                    sim.Step();m.Owed-=PinSim.Dt;steps++;
                }
                if(m.Owed>PinSim.Dt)m.Owed=PinSim.Dt;
                foreach(var b in sim.Landed){var info=(Info)b.Tag;Casino.PinLanded(m,b,info,Casino.ChannelPayout(info.Price,b.Channel));CasinoWorld.PinFlash(m,b.Channel);if(info.Ours&&b.Channel>=0)Achievements.AchievementTracker.PinLanded(PinSim.ChannelTenths[b.Channel]);}
                foreach(var b in sim.Cupped){Casino.PinCupped(m,b,(Info)b.Tag);if(((Info)b.Tag).Ours)Achievements.AchievementTracker.PinCupped(b.Cupped==PinSim.CupKind.Gold);}
                foreach(var b in sim.Done)Done(m,b);
                foreach(var b in sim.Returned){var info=(Info)b.Tag;if(info.Ours)Casino.Returned(info.Id);}
                if(sim.Balls.Count==0&&m.Waiting.Count==0){m.Idle+=(float)dt;if(m.Owner>=0&&m.Idle>20)machines.RemoveAt(i);}else m.Idle=0;
            }
        }
        /// <summary>A struck marble and all that came of it in: ours settles (and shows over the machine for everyone).</summary>
        static void Done(Machine m,PinSim.Ball root)
        {
            var info=(Info)root.Tag;if(!info.Ours)return;
            long got=Casino.Settle(info.Id,root.Tenths,root.Free,out var marble);if(got<0)return;
            CasinoShow.StartPinball(marble,root.Tenths,got,Casino.Sequence);
            Casino.PinSettled(m,root,info,got);
        }

        // ---------------------------------------------------------------- where the machines are
        /// <summary>Whether a machine's place is the station standing at a box (its foot's middle).</summary>
        public static bool At(Machine m,Rect box)
        {
            var region=TerrainSystem.Find(m.Scene);if(region==null)return false;
            var world=m.Local+(Vector2)region.Root.position;return(new Vector2(box.center.x,box.y)-world).sqrMagnitude<.0001f;
        }
        /// <summary>Every marble flying at the machine standing at a box, ours and others'.</summary>
        public static void BallsAt(Rect box,List<(PinSim.Ball Ball,Info Info,Machine Machine)> into)
        {
            into.Clear();
            foreach(var m in machines){if(!At(m,box))continue;foreach(var b in m.Sim.Balls)into.Add((b,(Info)b.Tag,m));}
        }
        /// <summary>The machine's clock (its baffles' places): ours if we have played it, else the time.</summary>
        public static double ClockAt(Rect box)
        {
            foreach(var m in machines)if(m.Owner<0&&At(m,box))return m.Sim.Clock;
            return Time.realtimeSinceStartup;
        }
    }
}
