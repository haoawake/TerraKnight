using System;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.World;
using TerraKnight.Effects;
using TerraKnight.Terraria;
using TerraKnight.UI;
using UnityEngine;

namespace TerraKnight.Player
{
    internal sealed class MountLayer
    {
        internal readonly string Texture;
        internal readonly bool Front, Glow;
        internal MountLayer(string texture, bool front, bool glow) { Texture=texture; Front=front; Glow=glow; }
    }
    /// <summary>Where the rider sits (MountData): feet playerYOffsets[frame] px up, the body's frame, the mount's own offset, its dust.</summary>
    internal sealed class MountSeat
    {
        internal readonly int[] Rise;
        internal readonly int Body, X, Y, Dust;
        internal MountSeat(int[] rise,int body,int x,int y,int dust){Rise=rise;Body=body;X=x;Y=y;Dust=dust;}
    }
    internal sealed class MountDef
    {
        internal ItemType Item;
        internal int Native, ItemId, Stage, Den, Value, Rare, JumpTicks, Flight, Frames, Stand, WalkStart, WalkCount, WalkDelay, FlyStart, FlyCount, FlyDelay, Air, Lift;
        internal string Name, Tip;
        internal string[] Sources;
        internal MountLayer[] Layers;
        internal float Run, Dash, Accel, Jump, Fall;
        internal bool Hover, Infinite;
        internal bool Skates => Native>=57 && Native<=60;
        internal bool CanUseHooks => Skates || Native==54; // MountID.Sets.CanUseHooks
        // PlayerDrawSet: wolf, rat, bat and pixie hide the whole rider; raptor keeps head accessories.
        internal bool Transform => Native==52 || Native==54 || Native==55 || Native==56 || Native==61;
        internal bool HeadAccessoriesOnly => Native==54;
        internal bool HideRider => Transform && !HeadAccessoriesOnly;
        internal bool Swim => Native==4 || Native==37 || Native==49 || Native==12;
    }
    // Mounted physics is driven in the same FixedUpdate patch as the normal Terraria movement,
    // so wings/rocket boots cannot add a second flight budget or overwrite mount velocity.
    internal static partial class Mounts
    {
        internal const int Slot=3;
        internal static MountDef Current {get; private set;}
        internal static bool Riding => Current!=null;
        internal static bool OwnMovement => Riding && !Current.Skates;
        internal static bool LavaImmune => Riding && Current.Native==49;
        internal static float FallScale => Riding ? Current.Fall : 1f;
        internal static float Fuel {get; private set;}
        internal static float Lift => Current!=null ? Mathf.Max(0,Current.Lift)/32f : 0f;
        internal static bool HidePlayer => Riding && Current.HideRider;
        static HeroController owner;
        static BoxCollider2D collider;
        static Vector2 originalSize, originalOffset;
        static string scene;
        static float oldVx, oldVy, shotAt, originalGravity;
        static bool suppressGravity;
        static bool jumpWasHeld, extraJump;
        static readonly Dictionary<int,float> hitAt=new Dictionary<int,float>();
        static readonly Dictionary<string,Sprite> sprites=new Dictionary<string,Sprite>();
        internal static int DevHits, DevShots;
        internal static MountDef Get(int id) {foreach(var m in All)if(m.Native==id)return m;return null;}
        internal static MountSeat SeatOf(MountDef m)=>m!=null && seats.TryGetValue(m.Native,out var s)?s:null;
        /// <summary>Sat in the saddle: every mount but a transformation (wolf, raptor, rat, bat, pixie) and the skates.</summary>
        internal static bool Seated(MountDef m)=>m!=null && !m.Transform && !m.Skates && SeatOf(m)!=null;
        /// <summary>
        /// The rider's legs (Player.PlayerFrame, Pl:36428-36440): frame 6, sat astride; straight (0) on the broom, the Spooky
        /// Wood mount and the Dark Mage's tome; a transformation and the skates keep the stance.
        /// </summary>
        internal static int RiderLeg(MountDef m)=>m!=null&&m.Skates ? -1 : !Seated(m) || m.Native==23 || m.Native==45 || m.Native==48 ? 0 : 6;
        /// <summary>The rider's body (Pl:36766-36770): the mount's bodyFrame (3, the arms out on the reins, on most).</summary>
        internal static int RiderBody(MountDef m)=>m!=null&&m.Skates ? -1 : Seated(m) ? SeatOf(m).Body : 0;
        /// <summary>
        /// How far over the ground the rider's feet are (world units). Terraria centres the mount's frame (42 + heightBoost) / 2
        /// - yOffset px over the ground and lifts the rider playerYOffsets[frame] px (PlayerDrawSet: Position.Y -= mountOffSet);
        /// ours stands the mount's frame on the ground, so the rider goes as far over its middle as Terraria's does.
        /// </summary>
        internal static float RiderRise(MountDef m,Vector2 velocity,bool grounded)
        {
            if(m==null)return 0f;
            if(m.Skates)return SeatOf(m).Rise[0]/32f;
            if(!Seated(m) || m.Layers.Length==0)return Mathf.Max(0,m.Lift)/32f;
            var s=SeatOf(m);var tex=TerrariaAssets.Texture(m.Layers[0].Texture);
            if(tex==null)return Mathf.Max(0,m.Lift)/32f;
            int frameH=tex.height/Mathf.Max(1,m.Frames);
            int rise=s.Rise[Mathf.Clamp(VisualFrame(m,velocity,grounded),0,s.Rise.Length-1)];
            return (frameH/2f-(42f+m.Lift)/2f+s.Y+rise)/32f;
        }
        internal static MountDef Of(ItemType t) {foreach(var m in All)if(m.Item==t)return m;return null;}
        internal static void AddItems(List<ItemDef> list)
        {
            foreach(var m in All)list.Add(new ItemDef {
                Type=m.Item,Name=m.Name,Texture="Item_"+m.ItemId,MountId=m.Native,MaxStack=1,
                Source=ItemSource.Drop,Stage=m.Stage,Rare=m.Rare,Value=m.Value,AutoReuse=false,
                UseAnimation=.33f,UseTime=.33f,NoMelee=true,
                Tooltip=m.Tip+"\n右键装备到坐骑栏，按 R 上下坐骑；也可手持使用\n获取阶段："+m.Stage+"；每个来源独立掉率 "+(100f/m.Den).ToString("0.##")+"%"
            });
        }
        internal static void AddDrops(List<EnemyDrops.Drop> rows,string key,int stage,bool entry,bool final,string scene=null)
        {
            if(BossStats.InGodhome(scene) && VanillaBossProgression.All.ContainsKey(key) && !VanillaBossProgression.Cleared(key))return;
            foreach(var m in All)
                if(m.Stage<=stage && Array.IndexOf(m.Sources,key)>=0 && !CraftOnlyLoot.Blocked(m.Item)
                    && ProgressionBalance.Unlocked(m.Item,stage,entry,final))
                    rows.Add(new EnemyDrops.Drop {Type=m.Item,Num=1,Den=m.Den,Min=1,Max=1,LockStage=0});
        }
        internal static void Toggle(ItemDef item=null)
        {
            if(item==null && Riding) {Dismount();return;}
            var inv=TerraMod.Instance?.Inventory;
            if(inv==null)return;
            if(item==null) {
                item=ItemDb.Get(inv.Misc[Slot].Type);
                if(!item.IsMount) {
                    foreach(var stack in inv.Slots)if(ItemDb.Get(stack.Type).IsMount){item=ItemDb.Get(stack.Type);break;}
                }
            }
            var def=item!=null ? Of(item.Type) : null;
            if(def==null)return;
            if(Current==def){Dismount();return;}
            TryMount(def,HeroController.SilentInstance);
        }
        internal static bool TryMount(MountDef def,HeroController hero)
        {
            if(hero==null || def==null || hero.cState.dead || hero.cState.transitioning || !GameHooks.PlayerHasControl())return false;
            if(!ProgressionBalance.Unlocked(def.Item)) {
                CombatText.Show(hero.transform.position,"坐骑需要阶段 "+def.Stage,CombatText.PlayerHurt);return false;
            }
            if(TerraMovement.OnRope || (GrapplingHook.IsLatched&&!def.CanUseHooks) || GravityGlobe.Flipped)return false;
            Dismount();
            var box=hero.GetComponent<BoxCollider2D>();
            if(box==null)return false;
            // Keep the feet where they were; test the additional headroom before expanding.
            float lift=Mathf.Max(0,def.Lift)/32f;
            var b=box.bounds;
            if(lift>0 && Blocked(new Vector2(b.center.x,b.max.y+lift*.5f),new Vector2(b.size.x*.9f,lift*.95f))) {
                CombatText.Show(hero.transform.position,"这里空间不足，无法骑乘",CombatText.PlayerHurt);return false;
            }
            owner=hero;collider=box;originalSize=box.size;originalOffset=box.offset;
            box.size+=Vector2.up*lift;box.offset+=Vector2.up*lift*.5f;
            scene=GameManager.UnsafeInstance?.sceneName;
            Current=def;
            Fuel=hero.cState.onGround ? def.Flight : 0f;
            var rb=hero.GetComponent<Rigidbody2D>();originalGravity=rb.gravityScale;suppressGravity=false;oldVx=rb.linearVelocity.x/TerraMovement.PxT;oldVy=rb.linearVelocity.y/TerraMovement.PxT;
            jumpWasHeld=TerraMovement.JumpHeldNow;extraJump=hero.cState.onGround;shotAt=Time.time+.5f;hitAt.Clear();
            TerraMod.Instance?.ResetFall();
            if(def.Skates)SkateDust(hero);
            return true;
        }
        static bool Blocked(Vector2 center,Vector2 size)
        {
            foreach(var c in Physics2D.OverlapBoxAll(center,size,0f,1<<ChunkColliders.TerrainLayer))
                if(!c.isTrigger && c.GetComponentInParent<HeroController>()==null)return true;
            return false;
        }
        internal static void Dismount()
        {
            if(Current?.Skates==true&&owner!=null)SkateDust(owner);
            if(owner!=null && suppressGravity)owner.GetComponent<Rigidbody2D>().gravityScale=originalGravity;
            suppressGravity=false;
            if(collider!=null){collider.size=originalSize;collider.offset=originalOffset;}
            if(Riding)TerraMod.Instance?.ResetFall();
            Current=null;owner=null;collider=null;hitAt.Clear();dashDelay=0f;dashAsk=0;
        }
        internal static void Tick(bool gameplay,HeroController hero)
        {
            if(Riding && (!gameplay || owner!=hero || hero==null || hero.cState.dead || hero.cState.hazardRespawning || hero.cState.transitioning || (GrapplingHook.IsLatched&&!Current.CanUseHooks) || TerraMovement.OnRope || GravityGlobe.Flipped || scene!=GameManager.UnsafeInstance?.sceneName))Dismount();
            if(!gameplay || hero==null || GameManager.UnsafeInstance.isPaused)return;
            if(TerraKeys.Pressed(TerraKeys.Action.Mount) && !InventoryGui.Open && !TerraKeys.Listening.HasValue && GameHooks.PlayerHasControl())Toggle();
            if(Riding && hero.cState.onGround){Fuel=Current.Flight;extraJump=true;}
        }
        internal static void ApplyJump(HeroController hero)
        {
            if(!Riding || Current.Skates)return; // Skate additions are part of the shared jump stats, including extra jumps.
            hero.JUMP_SPEED=Current.Jump*TerraMovement.PxT;
            hero.JUMP_STEPS=Mathf.Max(1,Mathf.RoundToInt(Current.JumpTicks/60f/Time.fixedDeltaTime));
        }
        internal static bool Move(HeroController hero,Rigidbody2D rb,float direction)
        {
            if(!OwnMovement || hero!=owner || (Current.CanUseHooks&&GrapplingHook.IsLatched))return false;
            if(hero.cState.recoiling || !hero.acceptingInput){dashAsk=0;return true;}
            float ticks=Time.fixedDeltaTime*60f;
            // Mount.RunSpeed: the swimmers' speed in water, the Shrimpy Truffle's when wet or hurt.
            float run=Current.Run;
            bool wet=Wet(hero),ground=hero.cState.onGround;
            if(Current.Native==4 && wet)run=10f;
            if(Current.Native==37 && wet)run=6f;
            if(Current.Native==49)run=wet ? 17f : 2f;
            if(Current.Native==12 && (wet || TerraMod.Instance.Stats.Life<TerraMod.Instance.Stats.LifeMax/2))run=8f;
            // Player.HorizontalMovement with the mount's maxRunSpeed, accRunSpeed (its dashSpeed) and runAcceleration
            // (Pl:26850-26852): up to the run speed at the mount's acceleration, then on to the dash speed a fifth as fast, on
            // the ground (or flying) only and not inside a dash; let go, slowed 0.2 px a tick (half that in the air).
            float acc=Mathf.Max(Current.Dash,run),accel=Mathf.Max(.08f,Current.Accel);
            const float slow=.2f;
            bool fly=Current.Hover || Current.Flight>0 && Fuel>0;
            bool left=direction<-.1f,right=direction>.1f;
            float vx=oldVx;
            if(dashDelay>0f)dashDelay=Mathf.Max(0f,dashDelay-ticks);
            if(dashAsk!=0)vx=StartDash(hero,rb,vx,ground);
            dashAsk=0;
            if(dashDelay<0f)vx=DashStep(hero,vx,acc,ticks);
            else if(left && vx>-run){if(vx>slow)vx-=slow*ticks;vx-=accel*ticks;}
            else if(right && vx<run){if(vx<-slow)vx+=slow*ticks;vx+=accel*ticks;}
            else if(left && vx>-acc){if(ground || fly){if(vx>slow)vx-=slow*ticks;vx-=accel*.2f*ticks;}}
            else if(right && vx<acc){if(ground || fly){if(vx<-slow)vx+=slow*ticks;vx+=accel*.2f*ticks;}}
            else vx=Mathf.MoveTowards(vx,0f,(ground?slow:slow*.5f)*ticks);
            // Steady Body: the mount speeds up as it would, turned at once at the speed it had, stopped at once (the speed kept
            // half a second for the next key), as on foot; a dash keeps its own.
            oldVx=Items.CharmEffects.NoInertia && dashDelay>=0f ? Items.CharmEffects.SteadyVelocity(oldVx,vx,right?1:left?-1:0) : vx;
            rb.linearVelocity=new Vector2(oldVx*TerraMovement.PxT,rb.linearVelocity.y);
            return true;
        }

        /// <summary>Dash 6's: below 0 a Chillet's dash running, above it the ticks till the next may start (Player.dashDelay).</summary>
        static float dashDelay;
        static int dashAsk;
        internal static int DevDashes;
        /// <summary>Dev: the mount's run speed (px a tick) set outright (a test's charge without the room to run one up).</summary>
        internal static float DevVx { set => oldVx = value; }
        internal static bool Chillet => Riding && Current.Native>=62 && Current.Native<=65;
        internal static bool Dashing => Riding && dashDelay<0f;

        /// <summary>
        /// A dash asked for on a mount (a direction tapped twice, the Dash key): the Chillets' own (Player.DashMovement: dashType
        /// 6 on them, Pl:21209-21212); no other mount dashes, nor do the worn dashes on one (MountID.Sets.CanDash, Pl:21577).
        /// </summary>
        internal static void RequestDash(int direction){if(Chillet && dashDelay==0f && direction!=0)dashAsk=direction<0?-1:1;}

        /// <summary>
        /// Dash 6 starting (Pl:21700-21716): 16 px a tick the way tapped, a hop of 4 off the ground, a fall halved, half
        /// speed starting against a wall.
        /// </summary>
        static float StartDash(HeroController hero,Rigidbody2D rb,float vx,bool ground)
        {
            if(!Chillet || dashDelay!=0f)return vx;
            int dir=dashAsk;
            if(dir>0 && !hero.cState.facingRight)hero.FaceRight();
            else if(dir<0 && hero.cState.facingRight)hero.FaceLeft();
            float vy=rb.linearVelocity.y/TerraMovement.PxT;
            if(ground)vy=4f;
            vx=16f*dir;
            if(vy<0f)vy/=2f;
            var b=collider.bounds;float ahead=b.center.x+dir*(b.extents.x+2f/32f);
            if(TerraWorld.IsSolidAt(new Vector2(ahead,b.max.y-2f/32f)) || TerraWorld.IsSolidAt(new Vector2(ahead,b.center.y)))vx/=2f;
            rb.linearVelocity=new Vector2(rb.linearVelocity.x,vy*TerraMovement.PxT);
            oldVy=vy;dashDelay=-1f;hitAt.Clear();DevDashes++;
            return vx;
        }

        /// <summary>
        /// Dash 6 going (Pl:21523-21550, 21557-21573): turned against it, it ends slowed to 0.3 with 30 ticks to wait; else the
        /// Chillet's frost (or fire) dust ahead, the speed bled ×0.992 a tick above 12 and ×0.96 above its dash speed, and at
        /// that it ends with 20 ticks to wait.
        /// </summary>
        static float DashStep(HeroController hero,float vx,float top,float t)
        {
            int facing=hero.cState.facingRight?1:-1;
            if(!Chillet || Mathf.Sign(vx)!=facing){dashDelay=30f;return vx*.3f;}
            DashDust(hero,facing,t);
            if(Mathf.Abs(vx)>12f)vx*=Mathf.Pow(.992f,t);
            else if(Mathf.Abs(vx)>top)vx*=Mathf.Pow(.96f,t);
            else{dashDelay=20f;vx=Mathf.Sign(vx)*top;}
            return vx;
        }

        static float dustTicks;

        /// <summary>Dash 6's dust (Pl:21523-21549): eight of dust 306 a tick 80 px ahead, lilac on the Chillets, amber on the Ignis ones.</summary>
        static void DashDust(HeroController hero,int facing,float t)
        {
            var p=TerraParticles.Instance;if(p==null)return;
            bool fire=Current.Native==63 || Current.Native==65;
            var tl=TerraMovement.TopLeft(hero);var b=collider.bounds;
            int w=Mathf.RoundToInt(b.size.x*32f),h=Mathf.Max(4,Mathf.RoundToInt(b.size.y*32f)-20);
            for(dustTicks+=t;dustTicks>=1f;dustTicks-=1f)
                for(int k=0;k<8;k++) {
                    int i=p.NewDust(new Vector2(tl.x+facing*80f,tl.y+12f),w,h,306,0f,0f,100);
                    if(i<0)continue;
                    ref var d=ref p[i];
                    d.Velocity*=1.5f;d.Velocity.x+=facing*1.5f;d.Scale*=1f+UnityEngine.Random.Range(0,40)*.01f;
                    d.FadeIn=.5f+UnityEngine.Random.Range(0,20)*.01f;d.NoGravity=true;
                    float f=UnityEngine.Random.value;
                    d.Color=Color.Lerp(fire?new Color(1f,.7f,.5f):new Color(.9f,.7f,1f),Color.white,f*f*f);
                }
        }
        internal static bool FixedStep(HeroController hero,Rigidbody2D rb,float ticks)
        {
            if(!OwnMovement || hero!=owner)return false;
            if(Current.CanUseHooks&&GrapplingHook.IsLatched){RestoreGravity(rb);return false;}
            if(!hero.acceptingInput || hero.cState.dead || hero.cState.recoiling || hero.cState.transitioning){RestoreGravity(rb);return true;}
            var m=Current;var v=rb.linearVelocity/TerraMovement.PxT;
            bool ground=hero.cState.onGround,jump=TerraMovement.JumpHeldNow,wet=Wet(hero);
            if(ground){Fuel=m.Flight;extraJump=true;}
            bool swim=m.Swim && wet;
            bool hover=m.Hover && (m.Infinite || Fuel>0) && (!ground || jump) && (m.Native!=49 || wet);
            bool liftFlight=!ground && jump && m.Flight>0 && Fuel>0 && !m.Hover;
            if(hover || swim || liftFlight){suppressGravity=true;rb.gravityScale=0f;}else RestoreGravity(rb);
            if(hover || swim) {
                float vertical=(jump || TerraMovement.DevUp || TerraKeys.Held(TerraKeys.Action.Up) ? 1f:0f)-(TerraMovement.DevDown || TerraKeys.Held(TerraKeys.Action.Down) ? 1f:0f);
                float speed=swim ? (m.Native==49?12f:6f) : Mathf.Max(3f,m.Run);
                if(ground && vertical>0)v.y=Mathf.Max(v.y,m.Jump);
                v.y=Mathf.MoveTowards(v.y,vertical*speed,.3f*ticks);
                if(!m.Infinite && !swim)Fuel=Mathf.Max(0,Fuel-ticks);
                TerraMod.Instance.ResetFall();
            } else if(liftFlight) {
                v.y=Mathf.MoveTowards(v.y,Mathf.Max(3f,m.Jump*.6f),.35f*ticks);Fuel=Mathf.Max(0,Fuel-ticks);TerraMod.Instance.ResetFall();
            }
            bool doubleJump=m.Native==10 || m.Native==14 || m.Native==45 || m.Native==47;
            if(!ground && doubleJump && extraJump && jump && !jumpWasHeld){v.y=m.Jump;extraJump=false;}
            if(ground && jump && m.Native==43)v.y=m.Jump;
            rb.linearVelocity=v*TerraMovement.PxT;
            Contact(hero,v,oldVy);AutoFire(hero);
            oldVy=v.y;jumpWasHeld=jump;
            return true;
        }
        static void RestoreGravity(Rigidbody2D rb){if(suppressGravity){rb.gravityScale=originalGravity;suppressGravity=false;}}
        internal static bool Wet(HeroController hero)
        {
            var p=hero.GetComponent<BoxCollider2D>().bounds.center;var cell=CellGrid.WorldToCell(p);
            return TerraWorld.LiquidAt(cell.x,cell.y)>64 || AcidLiquid.Swimming(hero);
        }
        /// <summary>
        /// The mounts that hurt what they run into, Terraria's way (Player.cs 20234-20318, 20689-20720, 21351-21400), each at
        /// its damage times the summon damage, with its knockback and the times both sides stay immune: the slime mounts (the
        /// Slimy Saddle, the Gelatinous Pillion) stomp what they fall on, 40, knockback 5, bouncing up at 10 px a tick; the
        /// charging ones past their speed, reached only by running on a while (Move): the Blessed Apple and the goat skull 60,
        /// the horses 30 (knockback 7), the Black Spot 100 and the Spooky Wood mount 120 (knockback 12), the Ancient Horn 90,
        /// the golf cart 40, each trailing its dust as it charges; the Chillets only inside their dash (dash 6, Pl:21351-21400),
        /// 32, the reliable ones 150 (knockback 6) with their frost or fire, 60 px further ahead.
        /// </summary>
        static void Contact(HeroController hero,Vector2 velocity,float previousY)
        {
            var m=Current;int n=m.Native;
            bool stomp=(n==3 || n==50) && (velocity.y<-.5f || previousY<-.5f) && !Wet(hero);
            bool chillet=Chillet && dashDelay<0f;
            float ram=0f,knock=10f,past=float.MaxValue;int npcImmune=30,playerImmune=6;
            switch(n)
            {
                case 10: case 47: ram=60f;past=m.Dash-m.Run/2f;break;
                case 40: case 41: case 42: ram=30f;knock=7f;past=m.Dash-m.Run/2f;break;
                case 44: ram=100f;knock=12f;past=m.Dash-m.Run/4f;break;
                case 45: ram=120f;knock=12f;past=m.Dash*.9f;break;
                case 14: ram=90f;past=m.Run/2f;break;
                case 17: ram=40f;past=m.Run/2f;playerImmune=12;break;
            }
            if(chillet){ram=n>=64?150f:32f;knock=6f;npcImmune=20;playerImmune=10;}
            bool ramming=chillet || ram>0f && Mathf.Abs(velocity.x)>past;
            if(ramming && !chillet && hero.cState.onGround)RamDust(hero,velocity.x);
            if(!stomp && !ramming)return;
            if(stomp){ram=40f;knock=5f;npcImmune=10;playerImmune=6;}
            float summon=1f+Equipment.Current.MinionDamage;
            int raw=Mathf.Max(1,Mathf.RoundToInt(ram*summon));
            var b=collider.bounds;
            float dir=Mathf.Abs(velocity.x)>.01f?Mathf.Sign(velocity.x):hero.cState.facingRight?1f:-1f;
            // The stomp's reach: the feet's strip, 12 px out either side and 6 px up and down (rect.Inflate(12, 6)); the
            // Chillet's dash: the body 4 px out all round, half a tick's way on, and 60 px more ahead.
            Vector2 size=stomp?new Vector2(b.size.x+24f*TerraParticles.Px,14f*TerraParticles.Px)
                :chillet?new Vector2(b.size.x+68f*TerraParticles.Px,b.size.y+8f*TerraParticles.Px):new Vector2(b.size.x+.4f,b.size.y*.7f);
            Vector2 at=stomp?new Vector2(b.center.x,b.min.y)
                :chillet?(Vector2)b.center+velocity*.5f*TerraParticles.Px+Vector2.right*dir*30f*TerraParticles.Px:(Vector2)b.center;
            foreach(var c in Physics2D.OverlapBoxAll(at,size,0f)) {
                var hm=c.GetComponentInParent<HealthManager>();if(hm==null || hm.isDead)continue;
                int id=hm.GetInstanceID();if(hitAt.TryGetValue(id,out float until) && Time.time<until)continue;
                if(!TerraMod.Instance.Melee.HitWith(c,raw,knock,0f,dir,hero.transform.position,0,autonomous:true))continue;
                hitAt[id]=Time.time+npcImmune/60f;DevHits++;
                TerraHurt.Invulnerable(hero,playerImmune);
                if(n==64)EnemyDebuffs.Add(hm,EnemyDebuffs.Frostburn,120);
                if(n==65)EnemyDebuffs.Add(hm,EnemyDebuffs.OnFire,120);
                if(stomp){var rb=hero.GetComponent<Rigidbody2D>();rb.linearVelocity=new Vector2(rb.linearVelocity.x,10f*TerraMovement.PxT);oldVy=10f;break;}
            }
        }

        /// <summary>A charge you can see: the mount's own dust (its spawnDust) thrown up behind its feet while it rams.</summary>
        static void RamDust(HeroController hero,float vx)
        {
            var p=TerraParticles.Instance;var s=SeatOf(Current);
            if(p==null || s==null || UnityEngine.Random.value>.5f)return;
            var tl=TerraMovement.TopLeft(hero);var b=collider.bounds;
            int w=Mathf.RoundToInt(b.size.x*32f);
            int i=p.NewDust(new Vector2(tl.x-4f,tl.y+b.size.y*32f-6f),w+8,6,s.Dust,-vx*.5f,-1f,100,1.2f);
            if(i<0)return;
            ref var d=ref p[i];d.Velocity*=.4f;d.NoGravity=true;
        }
        static void AutoFire(HeroController hero)
        {
            int n=Current.Native;if(n!=9 && n!=44 && n!=46 || Time.time<shotAt)return;
            shotAt=Time.time+(n==46?.15f:n==44?1f:.4f);
            Vector2 from=collider.bounds.center,targetPoint=from;HealthManager nearest=null;float best=18f*18f;
            foreach(var c in Physics2D.OverlapCircleAll(from,18f)) {
                var hm=c.GetComponentInParent<HealthManager>();if(hm==null || hm.isDead || !hm.gameObject.activeInHierarchy)continue;
                if(!ProgressionLoot.Eligible(EnemyDrops.EntryFor(EnemyHover.RecordOf(hm))))continue;
                float d=((Vector2)c.bounds.center-from).sqrMagnitude;
                if(d>=best || !TerraWorld.LineClearPx(TerraParticles.ToPx(from),TerraParticles.ToPx(c.bounds.center)))continue;
                if(Physics2D.Linecast(from,c.bounds.center,1<<ChunkColliders.TerrainLayer).collider!=null)continue;
                nearest=hm;targetPoint=c.bounds.center;best=d;
            }
            if(nearest==null)return;
            Vector2 v=(targetPoint-from).normalized*(n==44?9f:12f);
            // Normal projectile path handles authority, defense and hit synchronization.
            var previousClass=TerraProjectiles.SpawnClass;
            TerraProjectiles.SpawnClass=DamageClass.None;
            try { TerraProjectiles.Instance?.Shoot(n==9?ProjKind.MountScutlixLaser:n==44?ProjKind.MountCannonball:ProjKind.Bullet,from,v,n==9?50:n==44?70:35,2f,0f); }
            finally {TerraProjectiles.SpawnClass=previousClass;}
            DevShots++;
        }
        internal static void Draw(int id,Vector2 feet,Vector2 velocity,bool grounded,float facing,float opacity=1f)
        {
            var m=Get(id);if(m==null || FxBatch.Instance==null)return;
            int frame=VisualFrame(m,velocity,grounded);
            // Seated, the mount stands its xOffset ahead of the rider (Mount.Draw: Position.X + XOffset × direction).
            float ahead=Seated(m)?SeatOf(m).X*(facing<0?-1f:1f):0f;
            foreach(var layer in m.Layers) {
                var tex=TerrariaAssets.Texture(layer.Texture);int h=tex.height/Mathf.Max(1,m.Frames);
                int fr=Mathf.Clamp(frame,0,m.Frames-1);string key=layer.Texture+":"+fr+":"+h;
                if(!sprites.TryGetValue(key,out var sprite))sprites[key]=sprite=TerrariaAssets.Sprite(layer.Texture,0,fr*h,tex.width,h,32f,new Vector2(.5f,0));
                var p=TerraParticles.ToPx(feet);p.x+=ahead;
                FxBatch.Instance.Draw(sprite,p,new Color(1f,1f,1f,opacity),0f,Vector2.one,false,facing<0,layer.Front);
            }
        }
    }
}
