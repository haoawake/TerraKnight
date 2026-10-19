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
        internal bool Transform => Native==52 || Native==56 || Native==61;
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
        internal static bool HidePlayer => Riding && Current.Transform;
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
        internal static void AddDrops(List<EnemyDrops.Drop> rows,string key,int stage,bool entry,bool final)
        {
            foreach(var m in All)
                if(m.Stage<=stage && Array.IndexOf(m.Sources,key)>=0)
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
            if(TerraMovement.OnRope || GrapplingHook.IsLatched || GravityGlobe.Flipped)return false;
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
            if(owner!=null && suppressGravity)owner.GetComponent<Rigidbody2D>().gravityScale=originalGravity;
            suppressGravity=false;
            if(collider!=null){collider.size=originalSize;collider.offset=originalOffset;}
            if(Riding)TerraMod.Instance?.ResetFall();
            Current=null;owner=null;collider=null;hitAt.Clear();
        }
        internal static void Tick(bool gameplay,HeroController hero)
        {
            if(Riding && (!gameplay || owner!=hero || hero==null || hero.cState.dead || hero.cState.hazardRespawning || hero.cState.transitioning || GrapplingHook.IsLatched || TerraMovement.OnRope || GravityGlobe.Flipped || scene!=GameManager.UnsafeInstance?.sceneName))Dismount();
            if(!gameplay || hero==null || GameManager.UnsafeInstance.isPaused)return;
            if(TerraKeys.Pressed(TerraKeys.Action.Mount) && !InventoryGui.Open && !TerraKeys.Listening.HasValue && GameHooks.PlayerHasControl())Toggle();
            if(Riding && hero.cState.onGround){Fuel=Current.Flight;extraJump=true;}
        }
        internal static void ApplyJump(HeroController hero)
        {
            if(!Riding)return;
            hero.JUMP_SPEED=(Current.Skates ? hero.JUMP_SPEED/TerraMovement.PxT+1f : Current.Jump)*TerraMovement.PxT;
            if(!Current.Skates)hero.JUMP_STEPS=Mathf.Max(1,Mathf.RoundToInt(Current.JumpTicks/60f/Time.fixedDeltaTime));
        }
        internal static bool Move(HeroController hero,Rigidbody2D rb,float direction)
        {
            if(!OwnMovement || hero!=owner)return false;
            if(hero.cState.recoiling || !hero.acceptingInput)return true;
            float ticks=Time.fixedDeltaTime*60f;
            float speed=Mathf.Max(Current.Run,Current.Dash);
            bool wet=Wet(hero);
            if(Current.Native==4 && wet)speed=10f;
            if(Current.Native==37 && wet)speed=6f;
            if(Current.Native==49)speed=wet ? 17f : 2f;
            if(Current.Native==12 && (wet || TerraMod.Instance.Stats.Life<TerraMod.Instance.Stats.LifeMax/2))speed=8f;
            float target=Mathf.Abs(direction)>.1f ? Mathf.Sign(direction)*speed : 0f;
            oldVx=Mathf.MoveTowards(oldVx,target,Mathf.Max(.08f,Current.Accel)*(target==0?2f:1f)*ticks);
            rb.linearVelocity=new Vector2(oldVx*TerraMovement.PxT,rb.linearVelocity.y);
            return true;
        }
        internal static bool FixedStep(HeroController hero,Rigidbody2D rb,float ticks)
        {
            if(!OwnMovement || hero!=owner)return false;
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
        /// charging ones past their speed: the Blessed Apple and the goat skull 60, the horses 30 (knockback 7), the Black Spot
        /// 100 and the Spooky Wood mount 120 (knockback 12), the Ancient Horn 90, the golf cart 40; the Chillets dashing 32,
        /// the reliable ones 150 (knockback 6) with their frost or fire.
        /// </summary>
        static void Contact(HeroController hero,Vector2 velocity,float previousY)
        {
            var m=Current;int n=m.Native;
            bool stomp=(n==3 || n==50) && (velocity.y<-.5f || previousY<-.5f) && !Wet(hero);
            float ram=0f,knock=10f,past=float.MaxValue;int npcImmune=30,playerImmune=6;
            switch(n)
            {
                case 10: case 47: ram=60f;past=m.Dash-m.Run/2f;break;
                case 40: case 41: case 42: ram=30f;knock=7f;past=m.Dash-m.Run/2f;break;
                case 44: ram=100f;knock=12f;past=m.Dash-m.Run/4f;break;
                case 45: ram=120f;knock=12f;past=m.Dash*.9f;break;
                case 14: ram=90f;past=m.Run/2f;break;
                case 17: ram=40f;past=m.Run/2f;playerImmune=12;break;
                case 62: case 63: ram=32f;knock=6f;past=m.Run;npcImmune=20;playerImmune=10;break;
                case 64: case 65: ram=150f;knock=6f;past=m.Run;npcImmune=20;playerImmune=10;break;
            }
            bool ramming=ram>0f && Mathf.Abs(velocity.x)>past;
            if(!stomp && !ramming)return;
            if(stomp){ram=40f;knock=5f;npcImmune=10;playerImmune=6;}
            float summon=1f+Equipment.Current.MinionDamage;
            int raw=Mathf.Max(1,Mathf.RoundToInt(ram*summon));
            var b=collider.bounds;
            // The stomp's reach: the feet's strip, 12 px out either side and 6 px up and down (rect.Inflate(12, 6)).
            Vector2 size=stomp?new Vector2(b.size.x+24f*TerraParticles.Px,14f*TerraParticles.Px):new Vector2(b.size.x+.4f,b.size.y*.7f);
            Vector2 at=stomp?new Vector2(b.center.x,b.min.y):(Vector2)b.center;
            float dir=Mathf.Abs(velocity.x)>.01f?Mathf.Sign(velocity.x):hero.cState.facingRight?1f:-1f;
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
            int frame=m.Stand;
            if(!grounded)frame=m.FlyCount>0 ? m.FlyStart+(int)(Time.time*60/Mathf.Max(1,m.FlyDelay))%m.FlyCount : m.Air;
            else if(Mathf.Abs(velocity.x)>.2f && m.WalkCount>0)frame=m.WalkStart+(int)(Time.time*60/Mathf.Max(1,m.WalkDelay))%m.WalkCount;
            foreach(var layer in m.Layers) {
                var tex=TerrariaAssets.Texture(layer.Texture);int h=tex.height/Mathf.Max(1,m.Frames);
                int fr=Mathf.Clamp(frame,0,m.Frames-1);string key=layer.Texture+":"+fr+":"+h;
                if(!sprites.TryGetValue(key,out var sprite))sprites[key]=sprite=TerrariaAssets.Sprite(layer.Texture,0,fr*h,tex.width,h,32f,new Vector2(.5f,0));
                var p=TerraParticles.ToPx(feet);
                FxBatch.Instance.Draw(sprite,p,new Color(1f,1f,1f,opacity),0f,Vector2.one,false,facing<0,layer.Front);
            }
            if(m.Skates) {
                var sprite=TerraMod.ItemSprite(m.Item);
                FxBatch.Instance.Draw(sprite,TerraParticles.ToPx(feet),new Color(1,1,1,opacity),0f,Vector2.one*.6f,false,facing<0,true);
            }
        }
    }
}
