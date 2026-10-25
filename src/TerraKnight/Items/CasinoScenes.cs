using System.Collections.Generic;
using TerraKnight.World;
using UnityEngine;
using UnityEngine.InputSystem;
namespace TerraKnight.Items
{
    /// <summary>
    /// The casino's machines full screen, in pixel art (CasinoPx_*, tools/casino_scene_art.py): each drawn at a whole number
    /// of screen pixels a pixel so it fills the screen's height, in the casino's room, its words in white. The slot's
    /// paytable on its marquee, its displays, its buttons and the lever the player pulls down; the wheel spun by dragging
    /// it, the chips picked from the rack on the right and laid on the left, the pot on its hub, the ball thrown from the hub
    /// into a pocket; the pinball's marbles loaded from the tray, its handle drawn out and let go (or fired on and on), each
    /// marble flying as a body (CasinoPinSim). Each machine can take everything one has (全额). Positions are the art's
    /// pixels (from its top left).
    /// </summary>
    internal static partial class Casino
    {
        // ---------------------------------------------------------------- the screen
        static float P=4,sceneW=236,now,dt,lastFrame;static Vector2 O,mouseArt;static bool mDown,mPressed,mReleased;
        static GUIStyle tSmall,tMid,tBig,tLcd,tHuge;static Font font;
        static readonly Color Soft=new Color(1,1,1,.78f),Faint=new Color(1,1,1,.5f),Off=new Color(.6f,.6f,.66f),Lit=new Color(1,.9f,.5f);
        static readonly string[] MarbleTex=Names("Marble",7),MarbleField=Names("MarbleS",7),ChipTex=Names("Chip",6),SymTex=Names("Sym",5),ReelTex=Names("ReelBig",3),BlurTex=Names("ReelBigBlur",3);
        static string[] Names(string head,int n){var a=new string[n];for(int i=0;i<n;i++)a[i]=head+i;return a;}
        static Rect R(float x,float y,float w,float h)=>new Rect(O.x+x*P,O.y+y*P,w*P,h*P);
        static Vector2 S(Vector2 art)=>O+art*P;
        static void Pic(string name,float x,float y,Color? tint=null){var t=CasinoPx.Tex(name);CasinoPaint.Image(t,R(x,y,t.width,t.height),tint??Color.white);}
        static void Pic(string name,Rect art,Color? tint=null,Rect? uv=null,float angle=0){CasinoPaint.Image(CasinoPx.Tex(name),R(art.x,art.y,art.width,art.height),tint??Color.white,uv,angle);}
        static void Box(string name,float x,float y,float w,float h,int cap,Color? tint=null)=>CasinoPaint.Pixel9(CasinoPx.Tex(name),R(x,y,w,h),cap,P,tint??Color.white);
        static void Fill(Rect art,Color c)=>CasinoPaint.Fill(R(art.x,art.y,art.width,art.height),c);
        static bool Over(float x,float y,float w,float h)=>mouseArt.x>=x&&mouseArt.x<x+w&&mouseArt.y>=y&&mouseArt.y<y+h;
        static bool Free=>pad==Pad.None;
        static void Say(float x,float y,float w,float h,string s,GUIStyle st,Color c){var r=R(x,y,w,h);float k=Mathf.Max(1,Mathf.Round(P*.5f));CasinoPaint.Text(new Rect(r.x+k,r.y+k,r.width,r.height),s,st,new Color(0,0,0,.85f*c.a));CasinoPaint.Text(r,s,st,c);}
        static void Styles()
        {
            if(font==null)font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei UI","Microsoft YaHei","Arial"},20);
            if(tSmall==null)
            {
                // White letters (a new style's are black, and GUI.color only tints them).
                tSmall=new GUIStyle{font=font,alignment=TextAnchor.MiddleCenter,wordWrap=false,clipping=TextClipping.Overflow,normal={textColor=Color.white}};
                tMid=new GUIStyle(tSmall){fontStyle=FontStyle.Bold};tBig=new GUIStyle(tMid);tHuge=new GUIStyle(tMid);tLcd=new GUIStyle(tMid){alignment=TextAnchor.MiddleRight};
            }
            tSmall.fontSize=Mathf.RoundToInt(3.6f*P);tMid.fontSize=Mathf.RoundToInt(4.8f*P);tBig.fontSize=Mathf.RoundToInt(7f*P);tHuge.fontSize=Mathf.RoundToInt(11f*P);tLcd.fontSize=Mathf.RoundToInt(5.2f*P);
        }
        // The machine's art fits the screen's height (and width), whole screen pixels a pixel.
        static void Frame(float width)
        {
            sceneW=width;now=Time.realtimeSinceStartup;dt=Mathf.Clamp(now-lastFrame,0,.05f);lastFrame=now;
            P=Mathf.Max(1,Mathf.Floor(Mathf.Min(Screen.height/270f,(Screen.width-24)/width)));
            O=new Vector2(Mathf.Round((Screen.width-width*P)/2),Mathf.Round((Screen.height-270*P)/2));
            var m=Mouse.current;var at=CasinoPaint.Capture?CasinoPaint.Mouse:UI.TerraCursor.GuiMouse();mouseArt=(at-O)/P;
            bool d=CasinoPaint.Capture?CasinoPaint.Click.HasValue:UI.TerraCursor.LeftHeld(m);
            mPressed=CasinoPaint.Capture?CasinoPaint.Click.HasValue:UI.TerraCursor.LeftPressed(m);mReleased=mDown&&!d;mDown=d;
            Styles();CasinoGuiFx.P=P;
        }
        static bool Space=>!CasinoPaint.Capture&&Keyboard.current!=null&&Keyboard.current.spaceKey.wasPressedThisFrame;
        static bool SpaceHeld=>!CasinoPaint.Capture&&Keyboard.current!=null&&Keyboard.current.spaceKey.isPressed;

        // A push button of the machine: its picture (down while pressed), its word; true when clicked.
        static bool Push(string skin,float x,float y,float w,float h,string label,bool enabled=true,bool lit=false)
        {
            bool over=enabled&&Free&&Over(x,y,w,h);bool down=over&&mDown;
            Box(enabled?skin+(down?"Down":""):"PushGrey",x,y,w,h,3,lit?new Color(1,1,1):over?new Color(1.15f,1.15f,1.15f):Color.white);
            if(lit){float a=.35f+.25f*Mathf.Sin(now*8);Fill(new Rect(x,y,w,h),new Color(1,.9f,.5f,a*.35f));}
            Say(x,y+(down?.5f:0)-.5f,w,h,label,h>=12?tMid:tSmall,enabled?Color.white:Off);
            bool hit=enabled&&Free&&mPressed&&Over(x,y,w,h);if(hit)TerraMod.Instance?.PlaySound("tk_casino_button",.7f);return hit;
        }
        // A display: its name small at the left, its value at the right, in white (a colour for a win).
        static void Lcd(float x,float y,float w,float h,string name,string value,Color? colour=null)
        {
            Box("Lcd",x-1,y-1,w+2,h+2,3);var c=colour??Color.white;
            Say(x+1.5f,y,w*.4f,h,name,tSmall,Soft);tLcd.alignment=TextAnchor.MiddleRight;Say(x,y,w-2,h,value,tLcd,c);
        }
        static string Geo(long copper)=>(copper/100).ToString("N0");

        // ---------------------------------------------------------------- the room
        static readonly Vector3[] bokeh=MakeBokeh();
        static Vector3[] MakeBokeh(){var r=new System.Random(11);var a=new Vector3[46];for(int i=0;i<a.Length;i++)a[i]=new Vector3((float)r.NextDouble(),(float)r.NextDouble()*.75f,(float)r.NextDouble());return a;}
        static void Room()
        {
            var wall=CasinoPx.Tex("BgWall");float t=32*P;
            for(float y=Mathf.Repeat(O.y,t)-t;y<Screen.height;y+=t)for(float x=Mathf.Repeat(O.x,t)-t;x<Screen.width;x+=t)CasinoPaint.Image(wall,new Rect(x,y,t,t),Color.white);
            var floor=CasinoPx.Tex("BgFloor");float fy=O.y+256*P;
            for(float y=fy;y<Screen.height;y+=16*P)for(float x=Mathf.Repeat(O.x,t)-t;x<Screen.width;x+=t)CasinoPaint.Image(floor,new Rect(x,y,t,16*P),Color.white);
            CasinoPaint.Fill(new Rect(0,0,Screen.width,Screen.height),new Color(0,0,0,.42f));
            // The room's far lights, twinkling.
            var b=CasinoPx.Tex("Bokeh");
            for(int i=0;i<bokeh.Length;i++)
            {
                var v=bokeh[i];float x=v.x<.5f?v.x*O.x*2-20:O.x+sceneW*P+(v.x-.5f)*2*(Screen.width-O.x-sceneW*P);float s=(3+v.z*3)*P;
                var c=i%3==0?new Color(1,.78f,.45f):i%3==1?new Color(1,.45f,.62f):new Color(.55f,.75f,1);c.a=.25f+.25f*Mathf.Sin(now*(.6f+v.z)+i);
                CasinoPaint.Image(b,new Rect(x,v.y*Screen.height,s,s),c);
            }
            // A dark edge, the machine in the light.
            for(int k=0;k<6;k++){float w=Screen.width*.06f*(k+1),a=.07f;CasinoPaint.Fill(new Rect(0,0,w,Screen.height),new Color(0,0,0,a));CasinoPaint.Fill(new Rect(Screen.width-w,0,w,Screen.height),new Color(0,0,0,a));}
        }
        // Over the room, under nothing: leave, and what the casino says.
        static void Overlay()
        {
            var r=new Rect(12,12,Mathf.Max(120,30*P),Mathf.Max(34,9*P));bool over=Free&&r.Contains(O+mouseArt*P);
            CasinoPaint.Pixel9(CasinoPx.Tex(over?"PushGold":"PushGrey"),r,3,Mathf.Max(2,P*.75f),Color.white);
            CasinoPaint.Text(new Rect(r.x+2,r.y+2,r.width,r.height),"离开  Esc",tMid,new Color(0,0,0,.8f));CasinoPaint.Text(r,"离开  Esc",tMid,Color.white);
            if(over&&mPressed){Close();return;}
            string say=string.IsNullOrEmpty(Notice)?state.LastMessage:Notice;
            if(!string.IsNullOrEmpty(say)&&!Busy)
            {
                var n=new Rect(Screen.width/2-Screen.width*.3f,Screen.height-8*P,Screen.width*.6f,7*P);CasinoPaint.Fill(n,new Color(0,0,0,.55f));
                CasinoPaint.Text(new Rect(n.x+2,n.y+2,n.width,n.height),say,tSmall,new Color(0,0,0,.8f));CasinoPaint.Text(n,say,tSmall,Color.white);
            }
        }

        // ---------------------------------------------------------------- the window, each frame
        public static void Draw()
        {
            if(!IsOpen)return;var e=Event.current;
            if(e.type==EventType.MouseDown||e.type==EventType.MouseUp||e.type==EventType.MouseDrag||e.type==EventType.ScrollWheel||e.type==EventType.KeyDown){e.Use();return;}
            if(e.type!=EventType.Repaint)return;DevDrawFrames++;
            var old=GUI.matrix;int depth=GUI.depth;GUI.depth=-80;GUI.matrix=Matrix4x4.identity;
            try{DrawScene();}finally{GUI.matrix=old;GUI.depth=depth;}
        }
        static void DrawScene()
        {
            if(openGame==Game.Slots)SlotScene();else if(openGame==Game.Roulette)WheelScene();else PinScene();
            CasinoGuiFx.Draw(font);
            if(!IsOpen)return;Overlay();if(pad!=Pad.None)DrawPad();
        }
        static void SceneOpened()
        {
            CasinoGuiFx.Reset();leverPull=0;leverDrag=false;autoPull=-1;trayCoins=0;winShown=0;allIn=false;
            pot=0;potWon=false;potAllIn=false;
            sessionReturn=0;sessionStake=0;charging=false;autoFire=false;othersSeen.Clear();
            pinMachine=null;
            if(openGame==Game.Pinball){var region=TerrainSystem.Find(scene);if(region!=null)pinMachine=CasinoPinball.Ours(scene,new Vector2(machineBox.center.x,machineBox.y)-(Vector2)region.Root.position);}
        }
        static void SceneClosed(){CasinoGuiFx.Reset();SaveIfDue(true);}

        // A round's end, louder and brighter the more it paid: the big number beside the machine (gold up for a gain, black in
        // smoke for a loss), its sound, coins from the tray, sparkles, light in every colour, the banner; a jackpot's fireworks
        // and rain of geo; all in and won, the whole screen in rainbow. Less than the stake back is a consolation, not a win.
        static void Celebrate(CasinoTier tier,long stake,long payout,Rect around,Vector2 tray,string head,string detail,bool allInRound)
        {
            var mod=TerraMod.Instance;long net=payout-stake;bool rainbow=allInRound&&payout>stake;
            CasinoGuiFx.Pop(S(new Vector2(sceneW+30,110)),net,0,rainbow);if(net>0)CasinoGuiFx.Pop(S(new Vector2(-30,110)),net,.15f,rainbow);
            if(net>0)CasinoGuiFx.Colours(around.center,tier==CasinoTier.Jackpot?140:tier==CasinoTier.Win?80:35);
            if(rainbow)CasinoGuiFx.Rainbow();
            switch(tier)
            {
                case CasinoTier.Lose:
                    if(payout>0){mod?.PlaySound("tk_casino_win_small",.55f);CasinoGuiFx.Burst(tray,4,.6f);CasinoGuiFx.Smoke(tray,2);}
                    else{mod?.PlaySound("tk_casino_lose",.9f);CasinoGuiFx.Smoke(tray,5);}
                    break;
                case CasinoTier.Small:mod?.PlaySound("tk_casino_win_small",1f);CasinoGuiFx.Burst(tray,10,.8f);CasinoGuiFx.Stars(around,10);break;
                case CasinoTier.Win:mod?.PlaySound("tk_casino_win_big",1f);CasinoGuiFx.Burst(tray,26,1f);CasinoGuiFx.Stars(around,24);CasinoGuiFx.Rain(40);break;
                default:mod?.PlaySound("tk_casino_jackpot",1f);CasinoGuiFx.Burst(tray,44,1.25f);CasinoGuiFx.Stars(around,40);CasinoGuiFx.Rain(120);
                    for(int k=0;k<5;k++)CasinoGuiFx.Firework(new Vector2(Random.Range(Screen.width*.15f,Screen.width*.85f),Random.Range(Screen.height*.12f,Screen.height*.5f)),k*.28f);CasinoGuiFx.Flash(.6f);break;
            }
            // The banner just under the window or the wheel, the winning lines and the ball's pocket left in sight.
            CasinoGuiFx.Banner(head,detail,payout>0&&payout<stake?CasinoTier.Small:tier,new Vector2(around.center.x,around.yMax+12*P));
        }
        static string Head(long stake,long payout)=>payout<=0?"未中奖":payout<stake?"安慰奖 +"+Money(payout):payout==stake?"保本 +"+Money(payout):"+"+Money(payout);

        // ---------------------------------------------------------------- the slot
        static readonly float[] ReelX={52,92,132};
        const float ReelY=100,CellH=32,KnobRest=96,LeverTravel=60;
        static readonly Vector2[] LineLeft={new Vector2(35,148),new Vector2(35,116),new Vector2(35,180),new Vector2(35,104),new Vector2(35,192)};
        static readonly Vector2[] LineRight={new Vector2(184,148),new Vector2(184,116),new Vector2(184,180),new Vector2(184,192),new Vector2(184,104)};
        static readonly Color[] LineColour={new Color(1,.85f,.3f),new Color(.4f,.9f,1),new Color(1,.45f,.85f),new Color(.5f,1,.5f),new Color(1,.6f,.3f)};
        static readonly Vector2[] linePoints=new Vector2[5];
        static float leverPull,autoPull=-1,slotResultAt=-99,winShown;static bool leverDrag,leverArmed,allIn,pairLine;static int winLines,trayCoins;
        static readonly bool[] reelStopped=new bool[3];
        static bool CanPlay=>!Busy&&Free;
        // What a spin stakes: the bet, or all one has.
        static long SlotStake=>allIn?Available:stake*100;

        static void SlotScene()
        {
            Frame(236);Room();float t=AnimationProgress;bool spinning=Busy;
            OthersResults();
            Pic("SlotCab",0,0);
            for(int i=0;i<3;i++)
            {
                float phase=spinning?CasinoAnimation.Reel(state.Reels[i],i,t,state.Sequence):state.Reels[i],later=spinning?CasinoAnimation.Reel(state.Reels[i],i,Mathf.Min(1,t+.02f),state.Sequence):phase;
                float speed=spinning?Mathf.Abs(phase-later)/(.02f*Mathf.Max(.1f,animationDuration)):0;
                float top=Mathf.Repeat(Mathf.Round((phase-1)*CellH),160);var uv=new Rect(0,1-(top+96)/160f,1,96/160f);
                Pic(ReelTex[i],new Rect(ReelX[i],ReelY,36,96),Color.white,uv);float smear=Mathf.Clamp01((speed-3)/8);if(smear>0)Pic(BlurTex[i],new Rect(ReelX[i],ReelY,36,96),new Color(1,1,1,smear),uv);
                if(spinning&&t>=.70f+i*.15f-.001f&&!reelStopped[i]){reelStopped[i]=true;TerraMod.Instance?.PlaySound("tk_casino_reel_"+i,.9f);}
            }
            Pic("WindowShade",52,100);
            SlotResult();
            float since=now-slotResultAt;bool showing=!spinning&&since<5f;
            // The winning lines across the window, the winning symbols framed, flashing (a middle pair: its two).
            if(showing&&(winLines!=0||pairLine))
                for(int l=0;l<Lines.Length;l++)
                {
                    bool pair=l==0&&pairLine&&(winLines&1)==0;
                    if((winLines&(1<<l))==0&&!pair)continue;bool on=(Mathf.FloorToInt(since*5)+l)%3!=0;if(!on)continue;var c=pair?new Color(.85f,.9f,1):LineColour[l];
                    linePoints[0]=LineLeft[l];for(int k=0;k<3;k++)linePoints[k+1]=new Vector2(70+40*k,148+Lines[l][k]*CellH);linePoints[4]=LineRight[l];
                    int span=pair?2:4;for(int k=0;k<span;k++)PixelLine(linePoints[k],linePoints[k+1],c);
                    for(int k=0;k<(pair?2:3);k++){float x=ReelX[k],y=148+Lines[l][k]*CellH-16;Fill(new Rect(x,y,36,1),c);Fill(new Rect(x,y+31,36,1),c);Fill(new Rect(x,y,1,32),c);Fill(new Rect(x+35,y,1,32),c);}
                }
            // The payline lamps: their colours, chasing while it spins, the winners flashing.
            bool flop=showing&&winLines==0&&!pairLine&&since<1.2f;
            for(int l=0;l<Lines.Length;l++)
            {
                bool lit=(winLines&(1<<l))!=0||l==0&&pairLine;
                float b=spinning?((Mathf.FloorToInt(now*10)-l)%5+5)%5==0?1:.2f:showing&&lit?(Mathf.FloorToInt(since*8)%2==0?1:.4f):flop?(Mathf.FloorToInt(since*8)%2==0?.9f:.05f):.35f;
                var c=flop?new Color(1,.2f,.15f):LineColour[l];Lamp(LineLeft[l],c,b,1);Lamp(LineRight[l],c,b,1);
            }
            // The marquee's lamps and the crown's.
            bool rainbow=showing&&(CasinoPx.TierOf(state.LastStake,state.LastPayout)==CasinoTier.Jackpot||state.LastAllIn&&state.LastWin);
            for(int k=0;k<13;k++)
            {
                float b=spinning?((Mathf.FloorToInt(now*14)-k)%4+4)%4==0?1:.25f:showing&&winLines!=0?((Mathf.FloorToInt(since*9)+k)%2==0?1:.35f):flop?(Mathf.FloorToInt(since*8)%2==0?.8f:.05f):.3f+.2f*Mathf.Sin(now*1.7f+k);
                var c=flop?new Color(1,.2f,.15f):rainbow?Color.HSVToRGB(Mathf.Repeat(now*.6f+k*.07f,1),.5f,1):new Color(1,.82f,.42f);
                Lamp(new Vector2(14+16*k,85.5f),c,b,.7f);
            }
            Lamp(new Vector2(110,12),new Color(1,.85f,.5f),.6f+.4f*Mathf.Sin(now*2),1.3f);Lamp(new Vector2(66,11),new Color(1,.8f,.45f),.5f+.3f*Mathf.Sin(now*2+1),.9f);Lamp(new Vector2(154,11),new Color(1,.8f,.45f),.5f+.3f*Mathf.Sin(now*2+2),.9f);
            Paytable();
            // The displays: what there is, the bet, what this spin won (counting up).
            if(showing&&winShown<state.LastPayout)winShown=Mathf.Min(state.LastPayout,winShown+Mathf.Max(100,state.LastPayout*dt/1.2f));
            Lcd(16,211,62,10,"可用",Geo(Busy?animationMoney-state.LastStake:Available));
            bool betOver=Free&&!Busy&&Over(84,211,52,10);
            Lcd(84,211,52,10,allIn?"全额":"下注",allIn?Geo(spinning?state.LastStake:Available):stake.ToString("N0"),allIn?Color.HSVToRGB(Mathf.Repeat(now*.5f,1),.35f,1):betOver?Lit:(Color?)null);
            if(betOver&&mPressed)OpenPad(Pad.Amount);
            Lcd(142,211,62,10,"赢得",spinning?"…":Geo((long)winShown),winShown>0?Lit:(Color?)null);
            // The buttons.
            if(Push("PushBlue",16,225,20,12,"−",CanPlay)){allIn=false;StepStake(-1);}
            if(Push("PushBlue",38,225,20,12,"+",CanPlay)){allIn=false;StepStake(1);}
            if(Push("PushGold",60,225,26,12,"最大",CanPlay)){allIn=false;stake=Stakes[Stakes.Length-1];TerraMod.Instance?.PlaySound("tk_casino_bet",.8f);}
            if(Push("PushRed",88,225,28,12,"全额",CanPlay&&Available>=100,allIn)){allIn=!allIn;TerraMod.Instance?.PlaySound(allIn?"tk_casino_chip":"tk_casino_bet",.9f);}
            if(Push("PushRed",118,225,86,12,spinning?"转动中":"开始",CanPlay,CanPlay&&!spinning)&&autoPull<0)autoPull=0;
            // The coins in the tray from the last win.
            var geo=CasinoPx.Tex("Geo");
            for(int k=0;k<trayCoins;k++){float x=62+(k*37%96),y=246-(k/12)*2+(k*13%3);CasinoPaint.Image(geo,R(x,y,8,8),Color.white);}
            Lever(spinning);
        }
        static void StepStake(int way)
        {
            int at=System.Array.IndexOf(Stakes,stake);if(at<0){at=0;for(int i=0;i<Stakes.Length;i++)if(Stakes[i]<=stake)at=i;}
            at=Mathf.Clamp(at+way,0,Stakes.Length-1);stake=Stakes[at];TerraMod.Instance?.PlaySound("tk_casino_bet",.8f);
        }
        // A lamp: lit by its colour over its socket (the glow, then a bright middle).
        static void Lamp(Vector2 at,Color c,float bright,float size)
        {
            if(bright<=.02f)return;var t=CasinoPx.Tex("LampLit");float s=Mathf.Round(11*size);c.a=Mathf.Clamp01(bright);CasinoPaint.Image(t,R(at.x-s/2,at.y-s/2,s,s),c);
        }
        // A line in whole art pixels (two thick).
        static void PixelLine(Vector2 a,Vector2 b,Color c)
        {
            int n=Mathf.CeilToInt(Mathf.Max(Mathf.Abs(b.x-a.x),Mathf.Abs(b.y-a.y)));
            for(int i=0;i<=n;i++){var p=Vector2.Lerp(a,b,i/(float)Mathf.Max(1,n));Fill(new Rect(Mathf.Round(p.x)-1,Mathf.Round(p.y)-1,2,2),c);}
        }
        static readonly string[] lineNames={"中","上","下","斜","斜"};
        static void Paytable()
        {
            Say(15,20,190,8,"一条线三个相同中奖 · 下注平分到 5 条线",tSmall,Color.white);
            float k=Mathf.Max(1,Mathf.Floor(P/2))/P;
            for(int s=0;s<5;s++)
            {
                float y=29+s*9.4f;var sym=CasinoPx.Tex(SymTex[s]);
                for(int j=0;j<3;j++)CasinoPaint.Image(sym,R(18+j*9,y,16*k,16*k),Color.white);
                tLcd.alignment=TextAnchor.MiddleLeft;Say(46,y-.5f,30,9,"×"+(TripleQuarters[s]/4m).ToString("0.##"),tLcd,s==4?Color.HSVToRGB(Mathf.Repeat(now*.3f,1),.25f,1):Color.white);
                // The middle line's first two alike: a little back (the best, the stake).
                for(int j=0;j<2;j++)CasinoPaint.Image(sym,R(76+j*9,y,16*k,16*k),new Color(1,1,1,.85f));
                tLcd.alignment=TextAnchor.MiddleLeft;Say(95,y-.5f,24,9,(PairQuarters[s]/20m).ToString("0.#")+"倍",tSmall,s==4?Lit:Soft);
            }
            // The five lines, each as the three cells it runs through.
            for(int l=0;l<Lines.Length;l++)
            {
                float x=124+l*16,y=30;Fill(new Rect(x-1,y-1,15,15),new Color(0,0,0,.55f));var c=LineColour[l];
                for(int col=0;col<3;col++)for(int row=0;row<3;row++)Fill(new Rect(x+col*4.5f,y+row*4.5f,4,4),Lines[l][col]==row-1?c:new Color(1,1,1,.12f));
                Say(x-2,y+14,17,8,lineNames[l],tSmall,Color.white);
            }
            Say(122,55,84,8,"三连按线注赔 · 线注=下注÷5",tSmall,Soft);
            Say(122,62,84,8,"中线前两个相同：返还总注",tSmall,Soft);
            Say(122,70,84,8,"拉下右边的拉杆开始",tSmall,new Color(1,1,1,.6f+.3f*Mathf.Sin(now*3)));
        }
        static void Lever(bool spinning)
        {
            // Pulled by hand: from its knob down; let go far enough, the reels spin. The start button or the space bar
            // pull it on their own.
            var knob=new Vector2(222,KnobRest+leverPull*LeverTravel);
            if(CanPlay&&!spinning&&!leverDrag&&autoPull<0&&mPressed&&(mouseArt-knob).sqrMagnitude<12*12){leverDrag=true;leverArmed=false;}
            if(CanPlay&&!spinning&&autoPull<0&&Space)autoPull=0;
            if(leverDrag)
            {
                leverPull=Mathf.Clamp01((mouseArt.y-KnobRest)/LeverTravel);
                if(leverPull>.75f&&!leverArmed){leverArmed=true;TerraMod.Instance?.PlaySound("tk_casino_lever_pull",.9f);}
                if(!mDown){leverDrag=false;TerraMod.Instance?.PlaySound("tk_casino_lever_back",.8f);if(leverArmed)Spin();}
            }
            else if(autoPull>=0)
            {
                autoPull+=dt;leverPull=Mathf.Clamp01(autoPull/.22f);
                if(autoPull>=.22f){autoPull=-1;TerraMod.Instance?.PlaySound("tk_casino_lever_pull",.9f);Spin();}
            }
            else leverPull=Mathf.MoveTowards(leverPull,0,dt*5);
            knob=new Vector2(222,KnobRest+leverPull*LeverTravel);
            Pic("LeverBase",213,150);Pic("LeverRod",new Rect(219,knob.y,6,Mathf.Max(1,172-knob.y)));Pic("LeverKnob",knob.x-9,knob.y-9);
            if(CanPlay&&!spinning&&!leverDrag&&leverPull<.05f){float a=.5f+.5f*Mathf.Sin(now*4);Lamp(knob,new Color(1,.4f,.35f),a*.5f,1.6f);}
        }
        static void Spin()
        {
            trayCoins=0;winShown=0;winLines=0;pairLine=false;slotResultAt=-99;for(int i=0;i<3;i++)reelStopped[i]=false;
            if(PlaySlots(SlotStake,allIn))TerraMod.Instance?.PlaySound("tk_casino_spin",.8f);
            else TerraMod.Instance?.PlaySound("tk_casino_lose",.35f);
        }
        static void SlotResult()
        {
            if(pendingShow<0||Busy||state.Sequence!=pendingShow||state.LastGame!=(int)Game.Slots)return;pendingShow=-1;slotResultAt=now;
            int a=state.Reels[0],b=state.Reels[1],c=state.Reels[2];winLines=0;
            for(int l=0;l<Lines.Length;l++)if(LineTriple(l,a,b,c))winLines|=1<<l;
            pairLine=(winLines&1)==0&&SymbolAt(0,a,0)==SymbolAt(1,b,0);
            var tier=CasinoPx.TierOf(state.LastStake,state.LastPayout);var window=R(52,100,116,96);var tray=S(new Vector2(110,246));
            int lines=0;for(int l=0;l<5;l++)if((winLines&(1<<l))!=0)lines++;
            string detail=lines>0?$"{lines} 条线三连":pairLine?"中线前两个相同":"五条线都没有连上";
            Celebrate(tier,state.LastStake,state.LastPayout,window,tray,Head(state.LastStake,state.LastPayout),(state.LastAllIn?"全额 · ":"")+detail,state.LastAllIn);
            trayCoins=state.LastPayout<=0?0:tier==CasinoTier.Lose?2:tier==CasinoTier.Small?6:tier==CasinoTier.Win?18:36;
        }

        // ---------------------------------------------------------------- the wheel
        static readonly Vector2 WheelC=new Vector2(150,92);
        const float WheelR=74,PocketR=60,RimR=70,LandShare=.82f;
        static float restAngle,finalAngle,wheelResultAt=-99;static bool wheelDrag,ballLanded;static Vector2 dragFrom;static float dragTurn;
        // The chips on the table (copper): laid from the rack, or everything (全额), or what the last turn won left there.
        static long pot;static bool potWon,potAllIn;
        static readonly List<int> history=new List<int>();
        static readonly float[] PadX={16,122,188},PadW={97,57,97};
        static readonly Colour[] PadColour={Colour.Red,Colour.Green,Colour.Black};
        static readonly long[] stackCount=new long[6];
        static void WheelScene()
        {
            Frame(300);Room();float t=AnimationProgress;bool spinning=Busy;int sample=state.LastGame==(int)Game.Roulette?state.LastWheel:0;
            OthersResults();
            if(!spinning&&pot>Available)pot=Available;
            if(spinning){restAngle=CasinoAnimation.Wheel(sample,t);finalAngle=CasinoAnimation.Wheel(sample,1);}
            else if(!wheelDrag)restAngle+=4*dt;
            WheelResult();
            float angle=restAngle+dragTurn;
            Pic("WheelBig",new Rect(WheelC.x-74.5f,WheelC.y-74.5f,149,149),Color.white,null,angle);
            // The pot on the hub's display.
            Say(WheelC.x-16,WheelC.y-10,32,7,potWon?"赢得":"场上",tSmall,Soft);
            string sum=pot>0?Geo(pot):"0";var st=sum.Length>7?tSmall:tMid;
            Say(WheelC.x-18,WheelC.y-3,36,10,sum,st,potWon?Color.Lerp(Lit,Color.white,.5f+.5f*Mathf.Sin(now*5)):Color.white);
            // The ball: thrown from the hub, round the rim against the wheel, into its pocket; then riding there.
            var ball=WheelC+BallAt(spinning,t,angle,sample);
            Pic("BallBig",Mathf.Round(ball.x)-4,Mathf.Round(ball.y)-4);
            float since=now-wheelResultAt;bool showing=!spinning&&since<5f;var landed=WheelColour(sample);
            if(showing&&state.LastGame==(int)Game.Roulette){var c=landed==Colour.Green?new Color(.4f,1,.5f):landed==Colour.Red?new Color(1,.35f,.3f):new Color(.7f,.75f,1);Lamp(ball,c,.6f+.4f*Mathf.Sin(since*10),2.2f);}
            Pic("WheelBowl",60,2);
            bool rainbow=showing&&state.LastAllIn&&state.LastWin;
            for(int k=0;k<16;k++)
            {
                float a=k/16f*Mathf.PI*2;var at=WheelC+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*86;
                float b=spinning?((Mathf.FloorToInt(now*16)-k)%4+4)%4==0?1:.2f:showing&&state.LastWin?((Mathf.FloorToInt(since*9)+k)%2==0?1:.3f):showing&&since<1.2f?(Mathf.FloorToInt(since*8)%2==0?.8f:.05f):.3f+.15f*Mathf.Sin(now*1.5f+k);
                var c=showing&&!state.LastWin&&since<1.2f?new Color(1,.2f,.15f):rainbow?Color.HSVToRGB(Mathf.Repeat(now*.7f+k/16f,1),.55f,1):new Color(1,.82f,.42f);Lamp(at,c,b,.8f);
            }
            Pic("Table",0,178);
            // The pads: a click puts the bet on that colour.
            for(int i=0;i<3;i++)
            {
                bool chosen=choice==PadColour[i],over=Free&&!spinning&&Over(PadX[i],194,PadW[i],43);
                if(chosen){float a=.5f+.3f*Mathf.Sin(now*4);var g=new Color(1,.9f,.45f,a);Fill(new Rect(PadX[i],194,PadW[i],1),g);Fill(new Rect(PadX[i],236,PadW[i],1),g);Fill(new Rect(PadX[i],194,1,43),g);Fill(new Rect(PadX[i]+PadW[i]-1,194,1,43),g);}
                else if(over)Fill(new Rect(PadX[i]+1,195,PadW[i]-2,41),new Color(1,1,1,.06f));
                Say(PadX[i]+6,200,PadW[i]-12,13,ColourName(PadColour[i])+(i==1?"  ×63":"  ×1.97"),tMid,Color.white);
                if(chosen)Say(PadX[i]+6,216,PadW[i]-12,10,"押这里",tSmall,new Color(1,1,1,.6f+.3f*Mathf.Sin(now*4)));
                if(over&&mPressed&&choice!=PadColour[i]){choice=PadColour[i];TerraMod.Instance?.PlaySound("tk_casino_chip",.9f);}
            }
            // The last turns along the rail, newest first.
            for(int k=0;k<history.Count&&k<16;k++)
            {
                var c=WheelColour(history[k]);float x=14+k*12;
                Fill(new Rect(x,251,9,9),new Color(0,0,0,.6f));Fill(new Rect(x+1,252,7,7),c==Colour.Green?new Color(.2f,.75f,.35f):c==Colour.Red?new Color(.85f,.18f,.2f):new Color(.12f,.12f,.16f));
                if(k==0)Fill(new Rect(x,261,9,1),Color.white);
            }
            Say(206,250,90,11,history.Count==0?"拖动转盘发力":"最近的落点",tSmall,Faint);
            PotPanel(spinning);ChipRack(spinning);
            // Dragged round by hand, let go: it spins.
            if(CanPlay&&!wheelDrag&&mPressed&&(mouseArt-WheelC).sqrMagnitude<WheelR*WheelR){wheelDrag=true;dragFrom=mouseArt;dragTurn=0;}
            if(wheelDrag)
            {
                var from=dragFrom-WheelC;var to=mouseArt-WheelC;dragTurn=Vector2.SignedAngle(to,from)*-1;
                if(!mDown){wheelDrag=false;bool flung=(mouseArt-dragFrom).magnitude>10;restAngle+=dragTurn;dragTurn=0;if(flung&&CanPlay)SpinWheel();}
            }
        }
        // The ball's place from the wheel's middle: thrown out of the hub, rolling round the rim against the wheel, dropping
        // in over the pockets' edges and settling in the middle of its own; then going round with it.
        static Vector2 BallAt(bool spinning,float t,float angle,int sample)
        {
            if(!spinning){float d=(angle-finalAngle)*Mathf.Deg2Rad;return new Vector2(Mathf.Sin(d),-Mathf.Cos(d))*PocketR;}
            if(t>=LandShare)
            {
                float v=(t-LandShare)/(1-LandShare),hop=3.5f*Mathf.Abs(Mathf.Sin(v*Mathf.PI*3))*(1-v)*(1-v);
                float a=(angle-finalAngle)*Mathf.Deg2Rad;return new Vector2(Mathf.Sin(a),-Mathf.Cos(a))*(PocketR+hop);
            }
            float u=t/LandShare,land=CasinoAnimation.Wheel(sample,LandShare)-finalAngle;
            float theta=(land+900*Mathf.Pow(1-u,1.6f))*Mathf.Deg2Rad;
            float r=u<.3f?Mathf.Lerp(14,RimR,Mathf.SmoothStep(0,1,u/.3f)):u<.72f?RimR:Mathf.Lerp(RimR,PocketR,(u-.72f)/.28f)+4*Mathf.Abs(Mathf.Sin((u-.72f)/.28f*Mathf.PI*2.5f))*(1-(u-.72f)/.28f);
            return new Vector2(Mathf.Sin(theta),-Mathf.Cos(theta))*r;
        }
        // The left: the chips on the table, stacked by colour, what they come to; clear them, or everything on the table.
        static void PotPanel(bool spinning)
        {
            Lcd(4,6,52,12,"可用",Geo(Busy?animationMoney-state.LastStake:System.Math.Max(0,Available-pot)));
            Box("Panel",2,22,56,128,12);Say(2,25,56,9,potWon?"赢来的筹码":"场上筹码",tSmall,Color.white);
            long left=pot/100;for(int k=Chips.Length-1;k>=0;k--){stackCount[k]=left/Chips[k];left%=Chips[k];}
            for(int k=0;k<6;k++)
            {
                float x=8+(k%3)*16,baseY=k<3?58:104;long n=stackCount[k];int shown=(int)System.Math.Min(n,14);
                for(int i=0;i<shown;i++)Pic(ChipTex[k],x,baseY-i*2.5f);
                if(n>0)Say(x-2,baseY+14,18,8,"×"+n,tSmall,Color.white);
            }
            Say(4,134,52,10,pot>0?Geo(pot):"点右边的筹码",pot>0?tMid:tSmall,pot>0?Color.white:Faint);
            if(Push("PushBlue",4,153,52,11,potWon?"取走":"清空",!spinning&&pot>0))
            {
                if(potWon&&Credit>0)CollectCredit();
                pot=0;potWon=false;potAllIn=false;TerraMod.Instance?.PlaySound("tk_casino_collect",.8f);
            }
            if(Push("PushRed",4,166,52,11,"全额",!spinning&&Available>=100,potAllIn)){pot=Available;potAllIn=true;potWon=false;TerraMod.Instance?.PlaySound("tk_casino_chip",1f);}
        }
        // The right: a chip of each value, a click lays one on the table; the turn and the payout.
        static void ChipRack(bool spinning)
        {
            Lcd(244,6,52,12,"赢得",spinning?"…":Geo(now-wheelResultAt<5?state.LastPayout:0),now-wheelResultAt<5&&state.LastWin?Lit:(Color?)null);
            Box("Panel",242,22,56,128,12);Say(242,25,56,9,"筹码",tSmall,Color.white);
            for(int k=0;k<Chips.Length;k++)
            {
                float y=36+k*18;long copper=Chips[k]*100;bool can=!spinning&&Free&&Available-pot>=copper,over=can&&Over(245,y-1,50,17);
                if(over)Fill(new Rect(245,y-1,50,17),new Color(1,1,1,.08f));
                Pic(ChipTex[k],248,y,can?Color.white:new Color(1,1,1,.4f));tLcd.alignment=TextAnchor.MiddleRight;Say(262,y+1,32,12,Chips[k].ToString("N0"),tSmall,can?Color.white:Off);
                if(over&&mPressed)
                {
                    if(potWon){potWon=false;}
                    pot+=copper;TerraMod.Instance?.PlaySound("tk_casino_chip",.9f);
                }
            }
            if(Push("PushRed",244,153,52,24,spinning?"转动中":"转动",CanPlay&&pot>0,CanPlay&&pot>0)||CanPlay&&pot>0&&Space)SpinWheel();
        }
        static void SpinWheel()
        {
            if(pot<=0){Notice="先点右边的筹码放到桌上。";return;}
            bool everything=potAllIn||pot>=Available;
            ballLanded=false;if(PlayWheel(pot,choice,everything)){wheelResultAt=-99;potWon=false;TerraMod.Instance?.PlaySound("tk_casino_wheel_spin",.9f);}
        }
        static void WheelResult()
        {
            if(Busy&&AnimationProgress>=LandShare&&!ballLanded){ballLanded=true;TerraMod.Instance?.PlaySound("tk_casino_ball_land",.9f);}
            if(pendingShow<0||Busy||state.Sequence!=pendingShow||state.LastGame!=(int)Game.Roulette)return;pendingShow=-1;wheelResultAt=now;
            history.Insert(0,state.LastWheel);if(history.Count>16)history.RemoveAt(16);
            // Won: what it paid stays on the table, to take or to play again; lost: the table is bare.
            if(state.LastPayout>0){pot=state.LastPayout;potWon=true;}else{pot=0;potWon=false;potAllIn=false;}
            var tier=CasinoPx.TierOf(state.LastStake,state.LastPayout);int padAt=System.Array.IndexOf(PadColour,choice);var at=R(PadX[padAt],194,PadW[padAt],43);
            Celebrate(tier,state.LastStake,state.LastPayout,R(WheelC.x-74,WheelC.y-74,148,148),at.center,Head(state.LastStake,state.LastPayout),
                (state.LastAllIn?"全额 · ":"")+"落在"+ColourName(WheelColour(state.LastWheel))+(state.LastPayout<=0?"":WheelColour(state.LastWheel)==Colour.Green?" ×63！":" ×1.97"),state.LastAllIn);
        }

        // ---------------------------------------------------------------- the pinball
        // The cabinet's pixels (tools/casino_scene_art.py) and the marbles' world are the same: CasinoPinSim's coordinates,
        // the cabinet drawn from x 100 (Cab) of the scene. The tray on the right, the exchange on the left.
        const float Cab=100,HandleRest=249,HandleTravel=11;
        static readonly string[] ChannelText=MakeChannelText();
        static string[] MakeChannelText(){var a=new string[Channels];for(int k=0;k<Channels;k++)a[k]="×"+(ChannelTenths[k]/10m).ToString("0.#");return a;}
        static CasinoPinball.Machine pinMachine;
        static float charge,chargeFrom,snap,lastForce=.6f,nextAuto,nextTink;static bool charging,autoFire;static long sessionReturn,sessionStake;
        static readonly float[] channelFlash=new float[11];
        static readonly List<(PinSim.Ball Ball,CasinoPinball.Info Info,CasinoPinball.Machine Machine)> pinBalls=new List<(PinSim.Ball,CasinoPinball.Info,CasinoPinball.Machine)>();
        static readonly HashSet<long> othersSeen=new HashSet<long>();
        static readonly string[] PinSounds={"tk_casino_pin_0","tk_casino_pin_1","tk_casino_pin_2"};

        static void PinScene()
        {
            Frame(410);Room();
            Pic("PinCab",Cab,0);
            double clock=pinMachine!=null?pinMachine.Sim.Clock:Time.realtimeSinceStartup;
            for(int k=0;k<PinSim.MillX.Length;k++)
                Pic("Mill",new Rect(Cab+(float)PinSim.MillX[k]-11,(float)PinSim.MillY[k]-11,22,22),Color.white,null,(float)(PinSim.MillAngle(clock,k)*180/System.Math.PI));
            Pic("Baffle",Cab+Mathf.Round((float)PinSim.BaffleA(clock))-11,(float)PinSim.BaffleAY-2);
            // The cup baffle: its cup gold (three times) or rainbow (two more marbles), changing every few seconds.
            bool gold=PinSim.CupGold(clock);float cupX=Cab+Mathf.Round((float)PinSim.Cup(clock));
            Pic(gold?"CupGold":"CupRainbow",cupX-16,(float)PinSim.CupY-3,gold?Color.white:Color.HSVToRGB(Mathf.Repeat(now*.6f,1),.25f,1));
            Lamp(new Vector2(cupX,(float)PinSim.CupY+1),gold?new Color(1,.8f,.3f):Color.HSVToRGB(Mathf.Repeat(now*.8f,1),.6f,1),.35f+.2f*Mathf.Sin(now*5),.8f);
            Pic("Baffle",Cab+Mathf.Round((float)PinSim.BaffleB(clock))-11,(float)PinSim.BaffleBY-2);
            for(int k=0;k<Channels;k++)
            {
                float x0=Cab+16+14*k;channelFlash[k]=Mathf.Max(0,channelFlash[k]-dt*1.4f);
                if(channelFlash[k]>0)Fill(new Rect(x0+1,186,12,38),ChannelTenths[k]>=40?new Color(1,.8f,.3f,channelFlash[k]*.6f):ChannelTenths[k]<10?new Color(.4f,.4f,.5f,channelFlash[k]*.4f):new Color(.6f,.9f,1,channelFlash[k]*.5f));
                Say(x0,198,14,10,ChannelText[k],tSmall,ChannelTenths[k]>=40?Color.HSVToRGB(Mathf.Repeat(now*.4f+k*.1f,1),.3f,1):ChannelTenths[k]>=10?Color.white:Soft);
            }
            Lamp(new Vector2(Cab+30,11),new Color(1,.8f,.45f),.5f+.4f*Mathf.Sin(now*2),1);Lamp(new Vector2(Cab+150,11),new Color(1,.8f,.45f),.5f+.4f*Mathf.Sin(now*2+2),1);
            Marbles();
            Hopper();
            Launcher();
            Tray();
            Console();
        }
        // Every marble flying in this machine, ours and others' (their names over them a moment), as they really are.
        static void Marbles()
        {
            CasinoPinball.BallsAt(machineBox,pinBalls);
            foreach(var (b,info,_) in pinBalls)
            {
                float x=(float)b.X,y=(float)PinSim.DrawY(b);
                var tint=info.Denomination==AllIn?Color.HSVToRGB(Mathf.Repeat(now*1.5f,1),.45f,1):Color.white;
                float fade=b.At==PinSim.Where.Cup?Mathf.Clamp01(1-(float)b.CupT/.35f):b.Channel>=0?Mathf.Clamp01(1-(float)(b.T-b.LandT-.4)/.35f):1;tint.a=fade;
                Pic(MarbleField[Mathf.Clamp(info.Denomination,0,6)],Cab+Mathf.Round(x)-4,Mathf.Round(y)-4,tint);
                if(info.Ours&&b.Knocks>info.Heard){info.Heard=b.Knocks;if(now>=nextTink){nextTink=now+.045f;TerraMod.Instance?.PlaySound(PinSounds[Random.Range(0,3)],.35f);}}
                if(!info.Ours&&b.T<1&&!string.IsNullOrEmpty(info.Who))Say(Cab+x-20,y-12,40,8,info.Who,tSmall,new Color(1,1,1,1-(float)b.T));
            }
        }
        // A marble fallen into a channel at a machine (CasinoPinball): if its window is open here, its channel lights and what
        // it brings floats up; ours sound by what it paid.
        internal static void PinLanded(CasinoPinball.Machine m,PinSim.Ball b,CasinoPinball.Info info,long got)
        {
            if(!IsOpen||openGame!=Game.Pinball||!CasinoPinball.At(m,machineBox))return;
            int ch=b.Channel;channelFlash[ch]=1;var at=S(new Vector2(Cab+14+14*ch+9,200));var mod=TerraMod.Instance;bool big=ChannelTenths[ch]>=Channels;
            CasinoGuiFx.Float(at,(info.Ours?"":info.Who+" ")+"+"+Geo(got),ChannelTenths[ch]>=10?Lit:new Color(.85f,.85f,.9f),big?1.6f:1);
            if(!info.Ours){if(ChannelTenths[ch]>=10)CasinoGuiFx.Colours(at,big?30:12);return;}
            if(ChannelTenths[ch]>=40){mod?.PlaySound("tk_casino_score_top",1f);CasinoGuiFx.Burst(at,24,1f);CasinoGuiFx.Colours(at,50);CasinoGuiFx.Flash(.35f);}
            else if(ChannelTenths[ch]>=15){mod?.PlaySound("tk_casino_score_hi",.95f);CasinoGuiFx.Burst(at,10,.8f);CasinoGuiFx.Colours(at,24);}
            else if(ChannelTenths[ch]>=5)mod?.PlaySound("tk_casino_score_mid",.8f);
            else mod?.PlaySound("tk_casino_score_lo",.8f);
        }
        // Into the cup: gold, three times the marble in coins out of it; rainbow, two more marbles up out of it.
        internal static void PinCupped(CasinoPinball.Machine m,PinSim.Ball b,CasinoPinball.Info info)
        {
            if(!IsOpen||openGame!=Game.Pinball||!CasinoPinball.At(m,machineBox))return;
            var at=S(new Vector2(Cab+(float)b.X,(float)PinSim.CupY-6));var mod=TerraMod.Instance;
            if(b.Cupped==PinSim.CupKind.Gold)
            {
                CasinoGuiFx.Float(at,(info.Ours?"":info.Who+" ")+"金杯 ×3  +"+Geo(info.Price*3),Lit,1.6f);CasinoGuiFx.Burst(at,16,.9f);
                if(info.Ours)mod?.PlaySound("tk_casino_score_top",1f);
            }
            else
            {
                CasinoGuiFx.Float(at,(info.Ours?"":info.Who+" ")+"彩虹杯 +2 颗",Color.HSVToRGB(Mathf.Repeat(now*.6f,1),.35f,1),1.4f);CasinoGuiFx.Colours(at,30);
                if(info.Ours)mod?.PlaySound("tk_casino_score_hi",.95f);
            }
        }
        // One of our marbles and all that came of it in: this sitting's sum, and the big number beside the machine (its gain
        // in gold, its loss in black and smoke); all in and won, the screen in rainbow.
        internal static void PinSettled(CasinoPinball.Machine m,PinSim.Ball root,CasinoPinball.Info info,long got)
        {
            sessionReturn+=got;sessionStake+=info.Price;
            if(!IsOpen||openGame!=Game.Pinball||!CasinoPinball.At(m,machineBox))return;
            long net=got-info.Price;var mod=TerraMod.Instance;bool rainbow=info.Denomination==AllIn&&net>0;
            CasinoGuiFx.Pop(S(new Vector2(net>=0?Cab-8:Cab+218,90+Random.Range(-30,30))),net,0,rainbow);
            if(rainbow){CasinoGuiFx.Rainbow();mod?.PlaySound("tk_casino_jackpot",1f);}
            else if(info.Denomination==AllIn){mod?.PlaySound("tk_casino_lose",.9f);CasinoGuiFx.Smoke(S(new Vector2(Cab+93,200)),8);}
        }
        // ---- loading and firing
        static void Hopper()
        {
            // The first loaded marble sits on the plunger; the rest wait along the foot (a click takes one back to the tray).
            int i=0;bool first=true;
            foreach(var m in Machine)
            {
                if(m.Force>=0)continue;
                if(first)
                {
                    first=false;var tint=m.Denomination==AllIn?Color.HSVToRGB(Mathf.Repeat(now*1.5f,1),.45f,1):Color.white;
                    Pic(MarbleField[m.Denomination],Cab+Mathf.Round((float)PinSim.Lane)-4,(float)PinSim.Seat-4,tint);continue;
                }
                if(i>=14)break;float x=16+i*11;bool over=Free&&Over(Cab+x,244,11,12);
                Pic(MarbleTex[m.Denomination],Cab+x,245,over?new Color(1,1,1,.6f):Color.white);
                if(over&&mPressed){if(UnloadMarble(m.Id))TerraMod.Instance?.PlaySound("tk_casino_chip",.7f);break;}
                i++;
            }
            if(first)Say(Cab+13,243,156,14,"点右边的珠子装进机器",tSmall,new Color(1,1,1,.45f+.25f*Mathf.Sin(now*3)));
            // The one on the plunger can come back too.
            var next=NextLoaded();
            if(next!=null&&Free&&Over(Cab+(float)PinSim.Lane-5,(float)PinSim.Seat-5,10,10)&&mPressed&&UnloadMarble(next.Id))TerraMod.Instance?.PlaySound("tk_casino_chip",.7f);
        }
        static void Launcher()
        {
            // Pressed and held, the handle draws out and the force builds; let go, it strikes the marble.
            bool onHandle=Free&&Over(Cab+172,232,30,32);
            if(!charging&&Free&&(mPressed&&onHandle||Space)){charging=true;chargeFrom=now;TerraMod.Instance?.PlaySound("tk_casino_plunger",.8f);}
            bool held=charging&&(mDown||SpaceHeld);
            if(charging)charge=Mathf.Clamp01((now-chargeFrom)/1.1f);
            if(charging&&!held){charging=false;Fire(charge);snap=.15f;charge=0;}
            var next=NextLoaded();
            // Fired on and on at the last force (a little either way), never the all-in marble on its own.
            if(autoFire&&!charging&&now>=nextAuto&&next!=null&&next.Denomination!=AllIn){Fire(Mathf.Clamp(lastForce+Random.Range(-.08f,.08f),.25f,1));nextAuto=now+.55f;snap=.12f;}
            snap=Mathf.Max(0,snap-dt);
            float pull=charging?charge:snap>0?snap/.15f*.3f:0;
            float hy=HandleRest+pull*HandleTravel;float seat=(float)PinSim.Seat,lane=(float)PinSim.Lane;
            Pic("Spring",new Rect(Cab+181,seat+4,10,Mathf.Max(2,hy-seat-4)));Pic("PlungerTip",Cab+lane-4,seat+4+pull*HandleTravel*.6f);
            Pic("PlungerRod",new Rect(Cab+lane-2,seat+8+pull*HandleTravel*.6f,4,Mathf.Max(1,hy-seat-8)));Pic("PlungerKnob",Cab+lane-8,hy);
            // The force gauge beside it while it is drawn.
            if(charging){float h=40*charge;Fill(new Rect(Cab+200,262-40,4,40),new Color(0,0,0,.6f));Fill(new Rect(Cab+200,262-h,4,h),Color.HSVToRGB(.33f-charge*.33f,.8f,1));}
            if(!charging&&next!=null){float a=.5f+.5f*Mathf.Sin(now*4);Lamp(new Vector2(Cab+lane,hy+5),new Color(1,.4f,.35f),a*.5f,1.5f);}
        }
        /// <summary>Dev: a strike from the open pinball window, as the handle's.</summary>
        internal static void DevFire(float force)=>Fire(force);
        static void Fire(float force)
        {
            var next=NextLoaded();
            if(next==null||pinMachine==null){Notice="机器里没有珠子：点右边的珠子装进来。";TerraMod.Instance?.PlaySound("tk_casino_button",.6f);autoFire=false;return;}
            if(!CasinoPinball.Strike(pinMachine,force)){autoFire=false;return;}
            lastForce=force;
            TerraMod.Instance?.PlaySound("tk_casino_launch",.6f+.4f*force);
        }
        static void Tray()
        {
            // The marbles by their value in their dishes, and the all-in one: a click loads one into the machine.
            Box("Panel",314,18,94,234,12);Say(314,22,94,10,"珠子 · 点击装进机器",tMid,Color.white);
            for(int k=0;k<6;k++)
            {
                float y=34+k*30;long have=Balls(k);bool over=Free&&Over(318,y,86,28);
                if(over)Fill(new Rect(318,y,86,28),new Color(1,1,1,.07f));
                Pic("Bowl",320,y+4);var r=new System.Random(k*7+1);
                for(int m=0;m<Mathf.Min((int)System.Math.Min(have,7),7);m++)Pic(MarbleTex[k],326+r.Next(0,22),y+4+r.Next(0,6)-(m/4)*3);
                Say(362,y+2,42,10,Denominations[k].ToString("N0"),tMid,Color.white);Say(362,y+14,42,10,"×"+have.ToString("N0"),tSmall,have>0?Soft:Off);
                if(over&&mPressed){if(LoadMarble(k))TerraMod.Instance?.PlaySound("tk_casino_chip",.8f);else TerraMod.Instance?.PlaySound("tk_casino_button",.5f);}
            }
            // 全额: one marble worth everything one has (paid when it is struck).
            float ay=212;bool loadedAll=HasAllInMarble();
            Pic(MarbleTex[AllIn],322,ay+4,Color.HSVToRGB(Mathf.Repeat(now*.8f,1),.35f,1));
            Say(334,ay,70,10,loadedAll?"全额弹珠已装":"全额弹珠",tSmall,Color.white);Say(334,ay+10,70,10,Geo(Available)+" 吉欧",tSmall,Soft);
            if(Push("PushRed",318,ay+22,86,11,"全额",!loadedAll&&Available>=100,loadedAll)){if(LoadMarble(AllIn))TerraMod.Instance?.PlaySound("tk_casino_chip",1f);}
        }
        static void Console()
        {
            // The left: buying and selling, what there is, what this sitting gave back, the payout, auto-fire.
            Box("Panel",2,18,94,234,12);Say(2,22,94,10,"兑换弹珠",tMid,Color.white);
            for(int k=0;k<6;k++)
            {
                float y=34+k*24;Pic(MarbleTex[k],6,y);Say(17,y,40,10,Denominations[k].ToString("N0"),tSmall,Color.white);Say(54,y,38,10,"×"+Balls(k).ToString("N0"),tSmall,Soft);
                if(Push("PushBlue",6,y+11,26,10,"买1",Free))BuyBalls(k);
                if(Push("PushBlue",34,y+11,26,10,"买10",Free))BuyBalls(k,10);
                if(Push("PushGold",62,y+11,28,10,"全卖",Free&&Balls(k)>0))SellBalls(k,Balls(k));
            }
            Lcd(6,182,86,11,"可用",Geo(Available));
            long net=sessionReturn-sessionStake;Lcd(6,196,86,11,"盈亏",(net>=0?"+":"-")+Geo(System.Math.Abs(net)),net>0?Lit:net<0?new Color(1,.6f,.55f):(Color?)null);
            if(Push(autoFire?"PushRed":"PushBlue",6,212,86,14,autoFire?"自动发射：开":"自动发射：关",Free,autoFire)){autoFire=!autoFire;nextAuto=now+.2f;}
            Say(2,232,94,10,"按住右下拉杆蓄力，松开击球",tSmall,new Color(1,1,1,.5f+.3f*Mathf.Sin(now*3)));
        }

        // Other players at this slot or wheel: as each round of theirs ends, whose and what it won or lost, beside the machine.
        static readonly List<(CasinoShow.Round Round,float Age,string Who)> others=new List<(CasinoShow.Round,float,string)>();
        static void OthersResults()
        {
            CasinoShow.OthersAt(machineKind,machineBox,others);
            foreach(var (r,age,who) in others)
            {
                if(age<r.Duration||age>r.Duration+1.5f)continue;long key=r.Sequence*31+who.GetHashCode();if(othersSeen.Contains(key))continue;othersSeen.Add(key);if(othersSeen.Count>512)othersSeen.Clear();
                long net=r.Payout-r.Stake;var at=S(new Vector2(Random.value<.5f?-30:sceneW+30,60+Random.Range(0,140)));
                CasinoGuiFx.Float(at,who+"  "+(net>=0?"+":"-")+Geo(System.Math.Abs(net)),net>=0?Lit:new Color(.85f,.85f,.9f),net>0?1.6f:1);
                if(net>0){CasinoGuiFx.Colours(at,r.AllIn?80:30);TerraMod.Instance?.PlaySound("tk_casino_win_small",.5f);}else CasinoGuiFx.Smoke(at,3);
            }
        }

        // ---------------------------------------------------------------- the keypad (an amount of your own)
        static void DrawPad()
        {
            CasinoPaint.Fill(new Rect(0,0,Screen.width,Screen.height),new Color(0,0,0,.6f));float x0=(sceneW-120)/2,y0=40;
            CasinoPaint.Pixel9(CasinoPx.Tex("Panel"),R(x0,y0,120,170),12,P,Color.white);Say(x0,y0+6,120,12,"输入下注金额",tMid,Color.white);
            CasinoPaint.Pixel9(CasinoPx.Tex("Screen"),R(x0+10,y0+22,100,20),4,P,Color.white);tLcd.alignment=TextAnchor.MiddleRight;Say(x0+12,y0+22,94,20,padDigits.Length==0?"0":padDigits,tBig,Color.white);
            Say(x0,y0+44,120,9,padError.Length>0?padError:"10001～1000000 吉欧",tSmall,padError.Length>0?new Color(1,.55f,.5f):Soft);
            string[] keys={"1","2","3","4","5","6","7","8","9","清除","0","退格"};var was=pad;pad=Pad.None;
            for(int i=0;i<keys.Length;i++)if(Push("PushBlue",x0+10+(i%3)*34,y0+56+(i/3)*20,32,17,keys[i]))
            {
                if(i==9)padDigits="";else if(i==11){if(padDigits.Length>0)padDigits=padDigits.Substring(0,padDigits.Length-1);}else if(padDigits.Length<7){string digit=i==10?"0":keys[i];padDigits=padDigits=="0"?digit:padDigits+digit;}padError="";
            }
            bool cancel=Push("PushGrey",x0+10,y0+140,48,18,"取消"),ok=Push("PushRed",x0+62,y0+140,48,18,"确认");pad=was;
            if(cancel)pad=Pad.None;
            if(ok){if(long.TryParse(padDigits,out long value)&&value>10000&&value<=MaxBetGeo){stake=value;allIn=false;pad=Pad.None;padError="";}else padError="请输入10001至1000000吉欧";}
        }
    }
}
