using System;
using System.Collections.Generic;

namespace TerraKnight.Items
{
    /// <summary>
    /// The pinball's marbles as bodies: struck up the lane, turned over its top by the hood into the field (the harder the
    /// strike, the farther left they fly), down a funnel to the middle, flung off three windmills (each turning one way, then the other), past a moving
    /// baffle, over the cup baffle (a moving bar with a cup in its middle: gold, it pays three times the marble and keeps
    /// it; rainbow, two more marbles of the same worth come up out of it), through the pins, off the bottom baffle and off
    /// each other, into a channel. A marble and the marbles that came of it settle together, once the last of them is in.
    /// Positions are the cabinet art's pixels (y down; tools/casino_scene_art.py draws the same geometry). Fixed steps and
    /// each marble's own random numbers make a machine's flights the same wherever they are run again from their launches:
    /// after a reload, on another player's screen.
    /// </summary>
    internal sealed class PinSim
    {
        public const double Dt=1.0/480,G=1000,R=3.5;
        // Speeds as at a gravity of 600, scaled to this one (everything plays a little faster, the same way).
        static readonly double Pace=Math.Sqrt(G/600);
        public const double Lane=186.5,Seat=228,WallL=15,WallR=172,Floor=224,CapY=184,Ceiling=17;
        // The lane's wall ends at LaneTop; over it the hood, a quarter round (its middle HoodX,HoodY), turns a marble over
        // into the field. The divider between the lane and the field lets a marble out of the hood but never back.
        public const double LaneTop=40,HoodX=172,HoodY=40,HoodR=23,Divider=174;
        public const int Channels=11;
        /// <summary>What each channel gives back of a marble's worth, in tenths (Casino's paytable; tuned in the harness to ~95% back).</summary>
        public static readonly int[] ChannelTenths={50,18,7,4,2,1,2,4,7,18,50};
        public static readonly double[] RowY={108,119,130,141,152,163};
        public const double PinR=1.5;
        // The windmills: their hubs, each turning back and forth on its own time.
        // The middle one under the funnel's mouth, one either side of it lower down: the middle one flings onto them.
        public static readonly double[] MillX={93,70,116},MillY={62,73,73};
        // The side two turn as each other's mirror (the right one the left one's turn the other way): the field stays the
        // same both ways.
        const double MillPeriod0=7.3,MillPhase0=2.1,MillPeriodSide=8.6,MillPhaseSide=0;
        public const double MillHub=2,MillTip=9;
        // The funnel under the hood: a rail down from each wall to the mouth in the middle (its two short walls), whatever
        // the strike, every marble comes out there onto the middle windmill.
        public static readonly double[] FunnelL={15,30,87,44},FunnelR={176,29,99,44};
        public const double MouthL=87,MouthR=99,MouthTop=44,MouthFoot=52;
        // The baffles: A under the windmills, the cup baffle (a bar with a cup in its middle), B over the channels.
        public const double BaffleAY=84,BaffleHalf=10,CupY=96,CupHalf=15,CupMouth=5.5,BaffleBY=172;
        // The cup turns gold and rainbow by turns; a marble's family takes up to FreeMost free marbles from it.
        public const double CupEvery=6;
        public const int GoldTenths=30,FreeMost=6,FreeEach=2;
        public static int MostTenths{get{int most=0;foreach(int t in ChannelTenths)most=Math.Max(most,t);return(1+FreeMost)*Math.Max(most,GoldTenths);}}

        public enum Where : byte { Lane, Field, Cup, Back, Gone }
        public enum CupKind : byte { None, Gold, Rainbow }
        public sealed class Ball
        {
            public object Tag;
            public Where At;
            public double X,Y,Vx,Vy,T,Still,Clock0,Force,LandT,CupT,CupDx;
            public uint Rng;
            public int Channel=-1;
            public CupKind Cupped;
            // Hard knocks so far (pins, baffles, windmills: the window's tinks).
            public int Knocks;
            public bool Up,Passed;
            // Turned as it rolls (for its picture).
            public double Turn;
            // The marble struck from the plunger this one came of (itself for that one), and on it: its family's marbles
            // still to come in, the free ones it has had, what they have won so far (tenths of its worth).
            public Ball Root;
            public int Alive=1,Free,Tenths;
            public bool IsFree=>Root!=this;
        }
        public readonly List<Ball> Balls=new List<Ball>();
        // What the last steps saw: marbles into their channels, into the cup, back on the plunger, gone from sight, and the
        // struck marbles whose whole family is in (to settle).
        public readonly List<Ball> Landed=new List<Ball>(),Cupped=new List<Ball>(),Returned=new List<Ball>(),Finished=new List<Ball>(),Done=new List<Ball>();
        public double Clock;
        double owed;

        // ---------------------------------------------------------------- the geometry
        public static double MillAngle(double clock,int k)=>k==0?12*Math.Sin(2*Math.PI*clock/MillPeriod0+MillPhase0):(k==1?1:-1)*12*Math.Sin(2*Math.PI*clock/MillPeriodSide+MillPhaseSide);
        static double MillSpin(double clock,int k)=>k==0?12*2*Math.PI/MillPeriod0*Math.Cos(2*Math.PI*clock/MillPeriod0+MillPhase0):(k==1?1:-1)*12*2*Math.PI/MillPeriodSide*Math.Cos(2*Math.PI*clock/MillPeriodSide+MillPhaseSide);
        public static double BaffleA(double clock)=>93+58*Math.Sin(2*Math.PI*clock/4.4+2.1);
        static double BaffleAV(double clock)=>58*2*Math.PI/4.4*Math.Cos(2*Math.PI*clock/4.4+2.1);
        public static double Cup(double clock)=>93+50*Math.Sin(2*Math.PI*clock/5.3+.4);
        static double CupV(double clock)=>50*2*Math.PI/5.3*Math.Cos(2*Math.PI*clock/5.3+.4);
        public static bool CupGold(double clock)=>((long)Math.Floor(clock/CupEvery)&1)==0;
        public static double BaffleB(double clock)=>93+60*Math.Sin(2*Math.PI*clock/3.7+1.3);
        static double BaffleBV(double clock)=>60*2*Math.PI/3.7*Math.Cos(2*Math.PI*clock/3.7+1.3);
        public static int ChannelAt(double x){int k=(int)Math.Floor((x-16)/14);return k<0?0:k>=Channels?Channels-1:k;}
        public static double PinX(int row,int j)=>row%2==0?16+14*j:23+14*j;
        // The even rows run to the walls (their end pins turn a marble running down a wall back in).
        public static bool PinExists(int row,int j)=>row%2==0?j>=0&&j<=11:j>=0&&j<=10;
        // Where a marble is drawn: in the cup it sinks out of sight.
        public static double DrawY(Ball b)=>b.At==Where.Cup?CupY-2+Math.Min(1,b.CupT/.25)*4:b.Y;

        // ---------------------------------------------------------------- launching
        /// <summary>A marble struck from the plunger now, force 0..1 (the handle drawn out).</summary>
        public Ball Launch(double force,uint seed,object tag=null)
        {
            if(force<0)force=0;if(force>1)force=1;
            var b=new Ball{Tag=tag,At=Where.Lane,X=Lane,Y=Seat,Vy=-(470+190*force)*Pace,Up=true,Rng=seed==0?0x9E3779B9u:seed,Clock0=Clock,Force=force};
            b.Root=b;
            Balls.Add(b);return b;
        }
        /// <summary>Runs time on in fixed steps (what is short of a step waits for the next call).</summary>
        public void Advance(double seconds,int mostSteps=960)
        {
            ClearSeen();
            owed+=seconds;int n=0;
            while(owed>=Dt&&n<mostSteps){Step();owed-=Dt;n++;}
            if(owed>Dt)owed=Dt;
        }
        public void ClearSeen(){Landed.Clear();Cupped.Clear();Returned.Clear();Finished.Clear();Done.Clear();}
        public void Step()
        {
            for(int i=0;i<Balls.Count;i++)Move(Balls[i]);
            for(int i=0;i<Balls.Count;i++)for(int j=i+1;j<Balls.Count;j++)Touch(Balls[i],Balls[j]);
            for(int i=Balls.Count-1;i>=0;i--)
            {
                var b=Balls[i];
                if(b.At==Where.Back){Balls.RemoveAt(i);Returned.Add(b);}
                else if(b.At==Where.Gone){Balls.RemoveAt(i);Finished.Add(b);}
            }
            Clock+=Dt;
        }

        // ---------------------------------------------------------------- one marble, one step
        void Move(Ball b)
        {
            b.T+=Dt;
            switch(b.At)
            {
                case Where.Lane:
                    b.Vy+=G*Dt;b.Y+=b.Vy*Dt;
                    // Up past the lane's wall: the hood turns it over.
                    if(b.Up&&b.Y<=LaneTop){b.At=Where.Field;b.X=Lane;b.Vx=0;break;}
                    if(b.Up&&b.Vy>=0)b.Up=false;
                    if(!b.Up&&b.Y>=Seat){b.Y=Seat;b.At=Where.Back;}
                    break;
                case Where.Field:Field(b);break;
                case Where.Cup:
                    // Sinking into the cup as the cup goes; out of sight, gone.
                    b.CupT+=Dt;b.X=Cup(Clock)+b.CupDx*Math.Max(0,1-b.CupT/.2);b.Y=DrawY(b);if(b.CupT>.35)b.At=Where.Gone;
                    break;
            }
        }
        void Field(Ball b)
        {
            double py=b.Y;
            b.Vy+=G*Dt;b.X+=b.Vx*Dt;b.Y+=b.Vy*Dt;b.Turn+=b.Vx*Dt/R;
            double sp=b.Vx*b.Vx+b.Vy*b.Vy;if(sp>1200*1200){double f=1200/Math.Sqrt(sp);b.Vx*=f;b.Vy*=f;}
            if(b.X<WallR-R-2)b.Passed=true;
            // Not yet out of the hood and back down under the lane's top: down the lane again.
            if(!b.Passed&&b.Y>LaneTop&&b.X>Divider){b.At=Where.Lane;b.X=Lane;b.Vx=0;b.Up=false;return;}
            // The walls: the left; the divider on the right (all the way up once out of the hood); the hood's round; the top.
            if(b.X<WallL+R){b.X=WallL+R;if(b.Vx<0)b.Vx=-b.Vx*.4;}
            if((b.Passed||b.Y>LaneTop-R)&&b.X>WallR-R&&b.X<Divider+R){b.X=WallR-R;if(b.Vx>0)b.Vx=-b.Vx*.4;}
            if(b.X>HoodX&&b.Y<HoodY)
            {
                double dx=b.X-HoodX,dy=b.Y-HoodY,d=Math.Sqrt(dx*dx+dy*dy),most=HoodR-R;
                if(d>most){double nx=dx/d,ny=dy/d;b.X=HoodX+nx*most;b.Y=HoodY+ny*most;Bounce(b,-nx,-ny,0,0,.15,.02,0);}
            }
            else if(b.Y<Ceiling+R){b.Y=Ceiling+R;if(b.Vy<0)b.Vy=-b.Vy*.3;}
            double c=Clock;
            // The cup: a marble dropping into its mouth is taken.
            double cx=Cup(c);
            if(Math.Abs(b.X-cx)<CupMouth&&py<CupY-4&&b.Y>=CupY-4&&b.Vy>0){TakeCup(b,cx,c);return;}
            // The funnel and its mouth, the windmills, the baffles, the pins.
            if(b.Y<MouthFoot+R+2)
            {
                Capsule(b,FunnelL[0],FunnelL[1],FunnelL[2],FunnelL[3],1,0,.2,.1,0);
                Capsule(b,FunnelR[0],FunnelR[1],FunnelR[2],FunnelR[3],1,0,.2,.1,0);
                if(b.Y>MouthTop-R-1){Capsule(b,MouthL,MouthTop,MouthL,MouthFoot,1,0,.2,.1,0);Capsule(b,MouthR,MouthTop,MouthR,MouthFoot,1,0,.2,.1,0);}
            }
            for(int k=0;k<MillX.Length;k++)if(Math.Abs(b.X-MillX[k])<MillTip+R+2&&Math.Abs(b.Y-MillY[k])<MillTip+R+2)Mill(b,c,k);
            if(Math.Abs(b.Y-BaffleAY)<R+3){double ax=BaffleA(c);Capsule(b,ax-BaffleHalf,BaffleAY,ax+BaffleHalf,BaffleAY,2,BaffleAV(c),.55,.05,0);}
            if(Math.Abs(b.Y-CupY)<R+3)
            {
                double v=CupV(c);
                Capsule(b,cx-CupHalf,CupY,cx-CupMouth-1.5,CupY,1.5,v,.5,.05,0);
                Capsule(b,cx+CupMouth+1.5,CupY,cx+CupHalf,CupY,1.5,v,.5,.05,0);
            }
            if(Math.Abs(b.Y-BaffleBY)<R+3){double bx=BaffleB(c);Capsule(b,bx-BaffleHalf,BaffleBY,bx+BaffleHalf,BaffleBY,2,BaffleBV(c),.55,.05,0);}
            for(int r=0;r<RowY.Length;r++)
            {
                if(Math.Abs(b.Y-RowY[r])>=R+PinR)continue;
                int j0=(int)Math.Round((b.X-(r%2==0?16:23))/14);
                for(int j=j0-1;j<=j0+1;j++)if(PinExists(r,j))Circle(b,PinX(r,j),RowY[r],PinR,0,0,.35,.05,.07);
            }
            // The channels: the dividers' caps and posts, the floor.
            if(b.Y>CapY-R-3)
            {
                int k0=(int)Math.Round((b.X-16)/14);
                for(int k=k0-1;k<=k0+1;k++)
                {
                    if(k<0||k>Channels)continue;double x=16+14*k;
                    Circle(b,x,CapY,2,0,0,.4,.05,.08);
                    Capsule(b,x,CapY,x,Floor+9,1,0,.3,.05,0);
                }
            }
            if(b.Y>Floor-R){b.Y=Floor-R;if(b.Vy>0)b.Vy=-b.Vy*.25;b.Vx*=.9;}
            if(b.Channel<0&&b.Y>CapY+R+1.5){b.Channel=ChannelAt(b.X);b.LandT=b.T;Landed.Add(b);Decide(b,ChannelTenths[b.Channel]);}
            if(b.Channel>=0&&b.T-b.LandT>.75)b.At=Where.Gone;
            // Come to rest somewhere it should not (on a pin's top, in a corner): a nudge, as a shaken cabinet would.
            if(b.Channel<0&&sp<60){b.Still+=Dt;if(b.Still>.5){b.Still=0;b.Vx+=(Next(b)<.5?-1:1)*45*Pace;b.Vy=-70*Pace;}}else b.Still=0;
        }
        /// <summary>Into the cup: gold, three times its worth and it is spent; rainbow, two marbles of its worth come up out of it.</summary>
        void TakeCup(Ball b,double cx,double c)
        {
            b.At=Where.Cup;b.CupT=0;b.CupDx=b.X-cx;b.Vx=b.Vy=0;b.Cupped=CupGold(c)?CupKind.Gold:CupKind.Rainbow;Cupped.Add(b);
            var root=b.Root;
            if(b.Cupped==CupKind.Rainbow)
            {
                int n=Math.Min(FreeEach,FreeMost-root.Free);
                for(int i=0;i<n;i++)
                {
                    double side=i%2==0?-1:1;
                    var f=new Ball{Tag=b.Tag,At=Where.Field,X=cx+side*2,Y=CupY+R+1,Vx=side*(40+50*Next(b))*Pace,Vy=(20+30*Next(b))*Pace,Passed=true,
                        Rng=b.Rng*2654435761u+(uint)(i+1)*40503u,Clock0=Clock,Force=b.Force,Root=root,Alive=0};
                    if(f.Rng==0)f.Rng=1;
                    Balls.Add(f);
                }
                root.Free+=n;root.Alive+=n;
            }
            Decide(b,b.Cupped==CupKind.Gold?GoldTenths:0);
        }
        /// <summary>One of a family in: what it won added to its struck marble's; the last in, the family is done.</summary>
        void Decide(Ball b,int tenths)
        {
            var root=b.Root;root.Tenths+=tenths;root.Alive--;
            if(root.Alive<=0)Done.Add(root);
        }
        void Circle(Ball b,double cx,double cy,double rc,double cvx,double cvy,double e,double mu,double jitter)
        {
            double dx=b.X-cx,dy=b.Y-cy,rr=R+rc,d2=dx*dx+dy*dy;if(d2>=rr*rr)return;
            double d=Math.Sqrt(d2);if(d<1e-6){dx=0;dy=-1;d=1;}
            double nx=dx/d,ny=dy/d;b.X=cx+nx*rr;b.Y=cy+ny*rr;
            Bounce(b,nx,ny,cvx,cvy,e,mu,jitter);
        }
        // A windmill: each blade moving as it turns, flinging what falls on it.
        void Mill(Ball b,double c,int m)
        {
            double a=MillAngle(c,m),w=MillSpin(c,m),mx=MillX[m],my=MillY[m];
            for(int k=0;k<4;k++)
            {
                double ca=Math.Cos(a+k*Math.PI/2),sa=Math.Sin(a+k*Math.PI/2);
                double x0=mx+ca*MillHub,y0=my+sa*MillHub,ex=ca*(MillTip-MillHub),ey=sa*(MillTip-MillHub);
                double t=((b.X-x0)*ex+(b.Y-y0)*ey)/(ex*ex+ey*ey);if(t<0)t=0;if(t>1)t=1;
                double px=x0+ex*t,py=y0+ey*t;
                Circle(b,px,py,1.2,-w*(py-my),w*(px-mx),.5,.1,0);
            }
            Circle(b,mx,my,MillHub,0,0,.4,.1,0);
        }
        void Capsule(Ball b,double x0,double y0,double x1,double y1,double rc,double cvx,double e,double mu,double jitter)
        {
            double ex=x1-x0,ey=y1-y0,l2=ex*ex+ey*ey,t=l2>0?((b.X-x0)*ex+(b.Y-y0)*ey)/l2:0;if(t<0)t=0;if(t>1)t=1;
            Circle(b,x0+ex*t,y0+ey*t,rc,cvx,0,e,mu,jitter);
        }
        void Bounce(Ball b,double nx,double ny,double cvx,double cvy,double e,double mu,double jitter)
        {
            double rvx=b.Vx-cvx,rvy=b.Vy-cvy,vn=rvx*nx+rvy*ny;if(vn>=0)return;if(vn<-90)b.Knocks++;
            if(jitter>0)
            {
                // No pin is quite round or quite true: each knock turns a little its own way.
                double a=(Next(b)*2-1)*jitter,ca=Math.Cos(a),sa=Math.Sin(a),tx=nx*ca-ny*sa;ny=nx*sa+ny*ca;nx=tx;
                vn=rvx*nx+rvy*ny;if(vn>=0)return;e*=.8+.4*Next(b);
            }
            // Friction by the knock's strength: a hard knock grips, a marble rolling along a rail hardly slows.
            double tvx=rvx-vn*nx,tvy=rvy-vn*ny,tl=Math.Sqrt(tvx*tvx+tvy*tvy),grip=mu*(1+e)*-vn,keep=tl>grip?(tl-grip)/tl:0;
            b.Vx=cvx+tvx*keep-e*vn*nx;b.Vy=cvy+tvy*keep-e*vn*ny;
        }
        static void Touch(Ball a,Ball b)
        {
            if(a.At!=Where.Field||b.At!=Where.Field)return;
            double dx=b.X-a.X,dy=b.Y-a.Y,d2=dx*dx+dy*dy;if(d2>=4*R*R)return;
            double d=Math.Sqrt(d2);if(d<1e-6){dx=1;dy=0;d=1;}double nx=dx/d,ny=dy/d,push=(2*R-d)/2;
            a.X-=nx*push;a.Y-=ny*push;b.X+=nx*push;b.Y+=ny*push;
            double vrel=(b.Vx-a.Vx)*nx+(b.Vy-a.Vy)*ny;if(vrel>=0)return;double j=-(1+.7)*vrel/2;
            a.Vx-=j*nx;a.Vy-=j*ny;b.Vx+=j*nx;b.Vy+=j*ny;
        }
        static double Next(Ball b){uint x=b.Rng;x^=x<<13;x^=x>>17;x^=x<<5;b.Rng=x;return(x>>8)/16777216.0;}

        // ---------------------------------------------------------------- playing launches over again
        /// <summary>
        /// Runs launches (force, seed, machine clock at launch) from the first to the end, together as they flew, and gives
        /// each one's family's winnings in tenths of its worth (-1: it came back to the plunger).
        /// </summary>
        public static int[] Replay(IList<(double Force,uint Seed,double Clock)> launches,double mostSeconds=180)
        {
            var result=new int[launches.Count];for(int i=0;i<result.Length;i++)result[i]=-2;
            if(launches.Count==0)return result;
            var order=new List<int>();for(int i=0;i<launches.Count;i++)order.Add(i);
            order.Sort((x,y)=>launches[x].Clock.CompareTo(launches[y].Clock));
            // As long as the launches go on, and then mostSeconds for the last of them to come in.
            double span=launches[order[order.Count-1]].Clock-launches[order[0]].Clock;
            var sim=new PinSim{Clock=launches[order[0]].Clock};int next=0,left=launches.Count;
            for(int step=0;step<(span+mostSeconds)/Dt&&left>0;step++)
            {
                while(next<order.Count&&sim.Clock>=launches[order[next]].Clock-Dt/2){var l=launches[order[next]];sim.Launch(l.Force,l.Seed,order[next]);next++;}
                sim.ClearSeen();sim.Step();
                foreach(var b in sim.Done){result[(int)b.Tag]=b.Tenths;left--;}
                foreach(var b in sim.Returned){result[(int)b.Tag]=-1;left--;}
            }
            // Still flying after all that time (never seen): what a marble gives back on the whole.
            for(int i=0;i<result.Length;i++)if(result[i]==-2)result[i]=10;
            return result;
        }
    }
}
