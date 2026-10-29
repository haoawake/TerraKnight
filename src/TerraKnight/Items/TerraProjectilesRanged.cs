using System.Collections.Generic;
using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// What a remade ranged shot carries on top of its kind (RangedFire, the right-click arts of BladeSkillsRanged.cs): its
    /// damage set at the start and how it falls off or grows with the way flown (a shotgun's pellets close in), more pierce,
    /// a pull toward the nearest enemy or toward a point (a barrage's rockets closing on the cursor), a copy of itself sent
    /// on to the next enemy from the one it struck, shards flying on through it, a burst on whoever stands round it, a sure
    /// critical hit; a trail of its weapon's motes. Made for each projectile from <see cref="SpawnRanged"/> as it is spawned.
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        internal sealed class RangedShot
        {
            /// <summary>The weapon it came from (its look: colours and motes).</summary>
            public ItemDef Item;
            /// <summary>Pierce added (a shot that dies on its hit keeps dying on it).</summary>
            public int Pierce;
            /// <summary>Speed and size times this as it starts.</summary>
            public float Speed = 1f, Size = 1f;
            /// <summary>Degrees a tick it turns toward the nearest enemy ahead of it.</summary>
            public float Home;
            /// <summary>A point it turns toward (world), from its Seek tick on, Seek degrees a tick; arrived within 1 block it holds its course.</summary>
            public Vector2? Point;
            public float Seek;
            public int SeekFrom;
            /// <summary>The least speed it keeps on its way to Point (px a tick; 0: its own): a shot that slows to a stop by itself still gets there.</summary>
            public float PointSpeed;
            /// <summary>Copies of itself sent on, one hit after another, each that much of the damage.</summary>
            public int Bounces;
            public float BounceDamage = 0.6f;
            /// <summary>Shards on its first hit: how many, their share of the damage each, their spread (degrees).</summary>
            public int Shards;
            public float ShardDamage = 0.3f, ShardSpread = 30f;
            /// <summary>The shards' kind (None: the shot's own).</summary>
            public ProjKind ShardKind;
            /// <summary>A burst on its hits (px across, its share of the damage), the struck enemy left out.</summary>
            public float Burst, BurstDamage;
            /// <summary>Damage times Near within 7 blocks of where it started, falling to Far at 20 (a shotgun's pellets).</summary>
            public float Near = 1f, Far = 1f;
            /// <summary>Always a critical hit.</summary>
            public bool SureCrit;
            /// <summary>Knockback times this.</summary>
            public float Knock = 1f;
            /// <summary>Its trail: motes a tick (0 none) and their size.</summary>
            public float Trail, TrailScale = 0.8f;
            /// <summary>A shard or a copy sent on: no shards or copies of its own.</summary>
            public bool Child;
            /// <summary>
            /// Fired along with others that reach the same enemy together (a twin, a burst, an art's fan): its own 10-tick
            /// immunity per enemy instead of the enemy's shared one, so one of them doesn't shut the others out.
            /// </summary>
            public bool Local;
            /// <summary>Its damage as made (for the falloff).</summary>
            public int Base;
            /// <summary>A debuff its hits give (EnemyDebuffs' id, 0 none), for that many ticks, that share of its hits.</summary>
            public int Debuff, DebuffTicks = 300;
            public float DebuffChance = 1f;
            /// <summary>What comes off it as it strikes (once) and as it ends.</summary>
            public Offshoot OnHit, OnDeath;
            /// <summary>Lightning arcing from what it strikes to that many enemies near (within 12 blocks), that share of its damage each.</summary>
            public int Chain;
            public float ChainDamage = 0.5f;
            /// <summary>Lightning on every that many of its hits (a lingering field's: an electrosphere's), counted in ChainHits.</summary>
            public int ChainEvery = 1, ChainHits;
            /// <summary>Fired together by one art (0 none): an electrosphere of it doesn't draw in its own volley's others.</summary>
            public int Volley;
            /// <summary>Its path drawn behind it as a fading line of light (SwordFxRanged).</summary>
            public bool Tracer;
            /// <summary>Weaving across its way, that many px either side.</summary>
            public float Weave;
            /// <summary>Its trail's own dust (-1: the weapon's).</summary>
            public int TrailDust = -1;
            /// <summary>Where it was last tick (its tracer's segment).</summary>
            public Vector2 LastPos;
            /// <summary>
            /// Ticks it first goes round the player (OrbitRadius px out, OrbitTurn degrees a tick from OrbitAngle, up from his
            /// facing), striking what it meets, then flies off at the aim at its own speed × LaunchK.
            /// </summary>
            public int Orbit;
            public float OrbitRadius = 30f, OrbitTurn = 9f, OrbitAngle;
            /// <summary>Ticks it first hangs where it was made (gathering), then flies off at the aim at its own speed × LaunchK.</summary>
            public int Hover;
            public float LaunchK = 1f;
            /// <summary>Where it hangs, its speed when let go, and whether it has been.</summary>
            public Vector2 HeldAt;
            public float LaunchSpeed;
            public bool Launched;

            /// <summary>
            /// A remade magic weapon's own doing (MageKits): on each enemy it strikes (where it is then), as it ends (where, its
            /// velocity in px an update), and each tick it flies (where, its velocity; false ends it). Tag: a count of its own.
            /// </summary>
            public System.Action<HealthManager, Vector2> OnStrike;
            public System.Action<Vector2, Vector2> OnEnd;
            public System.Func<Vector2, Vector2, bool> OnFly;
            /// <summary>A point it turns toward each tick (null: none), SteerTurn degrees a tick, at least SteerSpeed px an update.</summary>
            /// <summary>Off a wall (a beam's bounce): where, its new velocity in px an update.</summary>
            public System.Action<Vector2, Vector2> OnBounce;
            public System.Func<Vector2, Vector2, Vector2?> Steer;
            public float SteerTurn = 8f, SteerSpeed;
            /// <summary>At most this many px an update while it steers (0: no cap; a scythe's own speeding up held to it).</summary>
            public float SteerMax;
            public int Tag;

            public RangedShot Copy() => (RangedShot)MemberwiseClone();
        }

        internal enum SpawnWay : byte { Ring, Ahead, Up, Fall }

        /// <summary>
        /// Projectiles coming off a remade shot: a kind (None: the shot's own), how many, which way (round it, on ahead of it,
        /// up out of it, or down from the sky onto the spot), their share of its damage, their speed (px a tick; 0: the shot's),
        /// their size, that share of the times it happens.
        /// </summary>
        internal sealed class Offshoot
        {
            public ProjKind Kind;
            public int Count = 3;
            public SpawnWay Way;
            public float Damage = 0.3f, Speed, Size = 1f, Chance = 1f;
        }

        /// <summary>What the projectiles made now carry (RangedFire sets it about a shot), null for none.</summary>
        internal static RangedShot SpawnRanged;

        /// <summary>Dev: remade shots made, copies sent on, shards, bursts, homing turns.</summary>
        public static int DevRangedMade, DevRangedBounces, DevRangedShards, DevRangedBursts, DevRangedHomed;
        /// <summary>Dev: ticks a shot weaved, shots that reached their Point.</summary>
        public static int DevWeaveTicks, DevPointReached;

        /// <summary>A shot just spawned takes what SpawnRanged carries.</summary>
        static void TakeRanged(Proj p)
        {
            if (SpawnRanged == null) return;
            var r = SpawnRanged.Copy();
            p.Ranged = r;
            r.Base = p.Damage;
            if (r.Pierce > 0 && !p.Spec.DieOnHit && p.Penetrate > 0) p.Penetrate = p.MaxPenetrate = p.Penetrate + r.Pierce;
            if (r.Speed != 1f) p.Vel *= r.Speed;
            if (r.Size != 1f) { p.Scale *= r.Size; p.Grow *= r.Size; }
            if (r.SureCrit) p.Crit = 100f;
            p.Knockback *= r.Knock;
            if (r.Local && p.Cooldown == 0 && p.Spec.LocalCooldown == 0 && !p.Spec.OncePerNpc && p.Spec.TypeCooldown == 0) p.Cooldown = 10;
            r.LastPos = p.Pos;
            if (r.Orbit > 0 || r.Hover > 0)
            {
                // Held first: its time to fly kept whole, its way and speed taken up again as it goes.
                r.LaunchSpeed = p.Vel.magnitude * r.LaunchK;
                r.HeldAt = p.Pos;
                p.TimeLeft += r.Orbit + r.Hover;
                p.Vel = p.Vel.sqrMagnitude > 1e-8f ? p.Vel.normalized * 0.01f : Vector2.right * 0.01f;
            }
            DevRangedMade++;
        }

        /// <summary>Dev: shots let go from an orbit or a hover.</summary>
        public static int DevMageLaunched;

        /// <summary>
        /// An orbiting or hovering shot this tick: put round the player or back where it hangs (its own steps this tick moved
        /// it barely, at a hundredth of a px); false once let go, flying off at the aim.
        /// </summary>
        static bool RangedHeld(Proj p, RangedShot r)
        {
            if (r.Launched) return false;
            var mod = TerraMod.Instance;
            var hero = HeroController.SilentInstance;
            var aim = mod != null ? mod.DevAim ?? mod.AimPoint : p.Pos + Vector2.right;
            if (p.Ticks >= r.Orbit + r.Hover || hero == null)
            {
                r.Launched = true;
                var way = aim - p.Pos;
                p.Vel = (way.sqrMagnitude > 1e-6f ? way.normalized : Vector2.right) * Mathf.Max(1f, r.LaunchSpeed);
                p.StartPos = p.Pos;
                DevMageLaunched++;
                return false;
            }
            if (p.Ticks <= r.Orbit && r.Orbit > 0)
            {
                var centre = (Vector2)hero.transform.position + new Vector2(0f, 0.2f);
                float facing = hero.cState.facingRight ? 1f : -1f;
                float a = (r.OrbitAngle + r.OrbitTurn * p.Ticks) * Mathf.Deg2Rad;
                var off = new Vector2(Mathf.Cos(a) * facing, Mathf.Sin(a)) * r.OrbitRadius * Px;
                p.Pos = centre + off;
                var tangent = new Vector2(-off.y, off.x) * (r.OrbitTurn >= 0f ? 1f : -1f) * facing;
                p.Vel = tangent.normalized * 0.01f;
                r.HeldAt = p.Pos;
            }
            else
            {
                p.Pos = r.HeldAt;
                var way = aim - p.Pos;
                p.Vel = (way.sqrMagnitude > 1e-6f ? way.normalized : Vector2.right) * 0.01f;
            }
            r.LastPos = p.Pos;
            return true;
        }

        /// <summary>A remade shot's tick: its damage by the way flown, its turn toward an enemy or its point, its trail.</summary>
        void RangedTick(Proj p)
        {
            var r = p.Ranged;
            if (r.OnFly != null && !r.OnFly(p.Pos, p.Vel)) { Kill(p); return; }
            if (r.Steer != null && r.Steer(p.Pos, p.Vel) is Vector2 steerTo)
            {
                var way = steerTo - p.Pos;
                float v = p.Vel.magnitude;
                if (r.SteerSpeed > 0f && v < r.SteerSpeed) { p.Vel = (v > 0.01f ? p.Vel / v : way.normalized) * r.SteerSpeed; v = r.SteerSpeed; }
                if (r.SteerMax > 0f && v > r.SteerMax) { p.Vel = p.Vel / v * r.SteerMax; v = r.SteerMax; }
                if (way.sqrMagnitude > 1e-6f && v > 0.01f) TurnToward(p, way, r.SteerTurn);
            }
            if ((r.Orbit > 0 || r.Hover > 0) && RangedHeld(p, r)) return;
            if (r.Near != 1f || r.Far != 1f)
            {
                float blocks = Vector2.Distance(p.Pos, p.StartPos) / (16f * Px);
                p.Damage = Mathf.Max(1, Mathf.RoundToInt(r.Base * Mathf.Lerp(r.Near, r.Far, Mathf.Clamp01((blocks - 7f) / 13f))));
            }
            float speed = p.Vel.magnitude;
            if (r.Point.HasValue && p.Ticks >= r.SeekFrom && (speed > 0.01f || r.PointSpeed > 0f))
            {
                var to = r.Point.Value - p.Pos;
                if (to.magnitude > 16f * Px)
                {
                    // A shot that slows by itself (the Daybloom's motes) kept going to its point at PointSpeed px a tick.
                    if (r.PointSpeed > 0f && speed < r.PointSpeed * Px)
                    {
                        p.Vel = (speed > 0.01f ? p.Vel / speed : to.normalized) * r.PointSpeed * Px;
                        speed = r.PointSpeed * Px;
                    }
                    TurnToward(p, to, r.Seek);
                }
                else
                {
                    r.Point = null;
                    DevPointReached++;
                }
            }
            else if (speed > 0.01f)
            {
                if (r.Home > 0f && p.Ticks > 4)
                {
                    var hm = Nearest(p, 320f);
                    if (hm != null && Vector2.Dot(Centre(hm) - p.Pos, p.Vel) > 0f)
                    {
                        TurnToward(p, Centre(hm) - p.Pos, r.Home);
                        DevRangedHomed++;
                    }
                }
            }
            if (r.Weave > 0f && speed > 0.01f)
            {
                // Across its way by the change in a sine of its age: it weaves about its line.
                var across = new Vector2(-p.Vel.y, p.Vel.x) / speed;
                p.Pos += across * (Mathf.Sin(p.Ticks * 0.35f) - Mathf.Sin((p.Ticks - 1) * 0.35f)) * r.Weave * Px;
                DevWeaveTicks++;
            }
            if (r.Trail > 0f && Random.value < r.Trail && TerraParticles.Instance != null && r.Item != null)
            {
                var look = RangedArts.Of(r.Item);
                int type = r.TrailDust >= 0 ? r.TrailDust : look.Dust;
                var at = TerraParticles.ToPx(p.Pos);
                int d = TerraParticles.Instance.NewDust(at - new Vector2(4f, 4f), 0, 0, type, 0f, 0f, type == 278 ? 40 : 90, r.TrailScale * Random.Range(0.8f, 1.2f));
                if (d >= 0)
                {
                    ref var dust = ref TerraParticles.Instance[d];
                    dust.Position = at;
                    dust.Velocity = Flip(p.Vel) * -0.08f + Random.insideUnitCircle * 0.4f;
                    dust.NoGravity = true;
                    if (type == 278) dust.Color = Color.Lerp(look.A, look.B, Random.value * 0.6f);
                }
            }
            if (r.Tracer && r.Item != null)
            {
                var look = RangedArts.Of(r.Item);
                SwordFx.Instance?.RangedTracer(TerraParticles.ToPx(r.LastPos), TerraParticles.ToPx(p.Pos), Color.Lerp(look.A, look.B, 0.4f));
            }
            r.LastPos = p.Pos;
        }

        /// <summary>A remade shot ended: what comes off it as it ends (a cluster's fragments, a snowman's snowballs).</summary>
        void RangedDeath(Proj p)
        {
            var r = p.Ranged;
            if (r != null && r.OnEnd != null)
            {
                var end = r.OnEnd;
                r.OnEnd = null;
                end(p.Pos, p.Vel);
            }
            if (r == null || r.OnDeath == null || r.Child) return;
            var sp = r.OnDeath;
            r.OnDeath = null;
            SpawnOff(p, sp, p.Pos, 0, p.Pos);
        }

        /// <summary>
        /// What comes off a remade shot (Offshoot): from `at` (world), the struck enemy (id) left out of those going round or on
        /// (the ones falling from the sky are for it), each a child carrying the shot's look and nothing of its own to give off.
        /// </summary>
        void SpawnOff(Proj p, Offshoot sp, Vector2 at, int struck, Vector2 target)
        {
            if (sp == null || sp.Count <= 0 || Random.value > sp.Chance) return;
            var kind = sp.Kind == ProjKind.None ? p.Kind : sp.Kind;
            var child = p.Ranged.Copy();
            child.OnHit = child.OnDeath = null;
            child.Shards = child.Bounces = child.Chain = 0;
            child.Child = true;
            child.Local = true;
            child.Pierce = 0;
            child.Size = sp.Size;
            child.Speed = 1f;
            child.Point = null;
            child.Tracer = false;
            child.SureCrit = false;
            child.Near = child.Far = 1f;
            child.Trail = Mathf.Max(child.Trail, 0.3f);
            var saveRanged = SpawnRanged;
            var saveClass = SpawnClass;
            SpawnRanged = child;
            SpawnClass = ClassOf(p);
            var way = p.Vel.sqrMagnitude > 1e-4f ? p.Vel.normalized : Vector2.right;
            float speed = sp.Speed > 0f ? sp.Speed : Mathf.Clamp(p.Vel.magnitude, 4f, 14f);
            int damage = Mathf.Max(1, Mathf.RoundToInt(p.Ranged.Base * sp.Damage));
            const float Block = 16f * Px;
            for (int k = 0; k < sp.Count; k++)
            {
                var from = at;
                Vector2 vel;
                switch (sp.Way)
                {
                    case SpawnWay.Ring:
                        float a = (k + Random.value * 0.3f) / sp.Count * Mathf.PI * 2f;
                        vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed;
                        break;
                    case SpawnWay.Ahead:
                        vel = Rotate(way, sp.Count == 1 ? 0f : Mathf.Lerp(-28f, 28f, k / (sp.Count - 1f)) + Random.Range(-4f, 4f)) * speed;
                        break;
                    case SpawnWay.Up:
                        vel = Rotate(Vector2.up, sp.Count == 1 ? 0f : Mathf.Lerp(-35f, 35f, k / (sp.Count - 1f))) * speed;
                        break;
                    default:
                        var spot = target + new Vector2(Random.Range(-2.5f, 2.5f) * Block, 0f);
                        from = spot + new Vector2(Random.Range(-1.5f, 1.5f) * Block, 14f * Block);
                        vel = (spot - from).normalized * Mathf.Max(speed, 10f);
                        break;
                }
                var q = Spawn(kind, from, vel, damage, p.Knockback * 0.4f, p.Crit, p.Extra);
                if (q == null) continue;
                q.RangedForge = p.RangedForge;
                if (struck != 0 && sp.Way != SpawnWay.Fall) q.Immune[struck] = int.MaxValue;
                // Nothing that comes off lingers past 10 s; a hallowed star meets the ground from the spot's height down.
                if (q.TimeLeft > 600) q.TimeLeft = 600;
                if (kind == ProjKind.HallowStar) q.Ai1 = target.y + 0.5f;
                DevRangedSpawned++;
            }
            SpawnRanged = saveRanged;
            SpawnClass = saveClass;
        }

        /// <summary>
        /// A shot of a kind for an art (SpawnRanged carried, nothing of it lingering past 10 s); a hallowed star given the height
        /// it meets the ground from (`floor`, world).
        /// </summary>
        public void ShootFalling(ProjKind kind, Vector2 from, Vector2 velocity, int damage, float knockback, float crit, float? floor)
        {
            var p = Spawn(kind, from, velocity, damage, knockback, crit);
            if (p == null) return;
            if (p.TimeLeft > 600) p.TimeLeft = 600;
            if (floor.HasValue && kind == ProjKind.HallowStar) p.Ai1 = floor.Value;
        }

        static int volleys;

        /// <summary>A new id for the shots one art fires together (RangedShot.Volley).</summary>
        public static int NewVolley() => ++volleys;

        /// <summary>Dev: projectiles come off remade shots, lightning arcs, debuffs given.</summary>
        public static int DevRangedSpawned, DevRangedArcs, DevRangedDebuffs;

        /// <summary>Lightning from the struck enemy to the nearest others (each a hit of that share), drawn by SwordFxRanged.</summary>
        void ChainFrom(Proj p, HealthManager struck, int dealt)
        {
            var r = p.Ranged;
            var fromPx = TerraParticles.ToPx(Centre(struck));
            HealthManager not = struck;
            for (int k = 0; k < r.Chain; k++)
            {
                var next = NearestOther(fromPx, not, 192f, out _, out _);
                if (next == null) break;
                if (StrikeEnemy(p, next, Mathf.Max(1, Mathf.RoundToInt(dealt * r.ChainDamage)), p.Knockback * 0.3f))
                {
                    if (r.Debuff > 0) EnemyDebuffs.Add(next, r.Debuff, r.DebuffTicks);
                    var toPx = TerraParticles.ToPx(Centre(next));
                    SwordFx.Instance?.RangedArc(fromPx, toPx, r.Item);
                    DevRangedArcs++;
                    fromPx = toPx;
                }
                not = next;
            }
        }

        static void TurnToward(Proj p, Vector2 to, float degrees)
        {
            float speed = p.Vel.magnitude;
            float now = Mathf.Atan2(p.Vel.y, p.Vel.x) * Mathf.Rad2Deg, want = Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg;
            float a = Mathf.MoveTowardsAngle(now, want, degrees) * Mathf.Deg2Rad;
            p.Vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed;
            p.Rot = Angle(p.Vel);
        }

        /// <summary>A remade shot's hit (after the hit itself): a copy sent on, its shards, its burst.</summary>
        void RangedHit(Proj p, HealthManager hm, int dealt)
        {
            var r = p.Ranged;
            int id = hm.GetInstanceID();
            // The struck enemy is left out of its burst (set again after the hit, as for any shot).
            p.LastHit = id;
            r.OnStrike?.Invoke(hm, p.Pos);
            if (r.Debuff > 0 && Random.value < r.DebuffChance)
            {
                EnemyDebuffs.Add(hm, r.Debuff, r.DebuffTicks);
                DevRangedDebuffs++;
            }
            if (r.Chain > 0 && !r.Child && ++r.ChainHits % Mathf.Max(1, r.ChainEvery) == 0) ChainFrom(p, hm, dealt);
            if (r.OnHit != null && !r.Child)
            {
                var sp = r.OnHit;
                r.OnHit = null;
                SpawnOff(p, sp, p.Pos, id, Centre(hm));
            }
            var saveClass = SpawnClass;
            var saveRanged = SpawnRanged;
            SpawnClass = ClassOf(p);
            if (r.Bounces > 0 && !r.Child)
            {
                // The next enemy in sight within 20 blocks: a copy of the shot from here at it.
                var next = NearestOther(TerraParticles.ToPx(p.Pos), hm, 320f, out _, out var atPx);
                if (next != null)
                {
                    var child = r.Copy();
                    child.Bounces = r.Bounces - 1;
                    child.Child = child.Bounces <= 0;
                    child.Pierce = 0;
                    child.Near = child.Far = 1f;
                    child.Trail = Mathf.Max(child.Trail, 0.6f);
                    child.Local = true;
                    SpawnRanged = child;
                    var way = (TerraParticles.ToWorld(atPx) - p.Pos).normalized;
                    var q = Spawn(p.Kind, p.Pos, way * Mathf.Max(8f, p.Vel.magnitude), Mathf.Max(1, Mathf.RoundToInt(p.Damage * r.BounceDamage)), p.Knockback * 0.5f, p.Crit, p.Extra);
                    if (q != null) { q.Immune[id] = int.MaxValue; q.RangedForge = p.RangedForge; DevRangedBounces++; }
                    RangedSpark(p, 6);
                }
            }
            if (r.Shards > 0 && !r.Child)
            {
                var child = r.Copy();
                child.Shards = 0;
                child.Bounces = 0;
                child.Child = true;
                child.Pierce = 0;
                child.Size = 0.75f;
                child.Near = child.Far = 1f;
                child.Trail = 0.35f;
                child.Local = true;
                SpawnRanged = child;
                var v = p.Vel.sqrMagnitude > 1e-4f ? p.Vel : Vector2.right;
                for (int k = 0; k < r.Shards; k++)
                {
                    float off = r.Shards == 1 ? 0f : Mathf.Lerp(-r.ShardSpread, r.ShardSpread, k / (r.Shards - 1f)) + Random.Range(-4f, 4f);
                    var q = Spawn(r.ShardKind != ProjKind.None ? r.ShardKind : p.Kind, p.Pos, Rotate(v, off) * 0.85f, Mathf.Max(1, Mathf.RoundToInt(p.Damage * r.ShardDamage)), p.Knockback * 0.3f, p.Crit, p.Extra);
                    if (q != null) { q.Immune[id] = int.MaxValue; q.RangedForge = p.RangedForge; DevRangedShards++; }
                }
                r.Shards = 0;
                RangedSpark(p, 8);
            }
            if (r.Burst > 0f && r.BurstDamage > 0f)
            {
                Blast(p, r.Burst, Mathf.Max(1, Mathf.RoundToInt(dealt * r.BurstDamage)), p.Knockback * 0.5f);
                DevRangedBursts++;
                RangedRing(p, r.Burst);
            }
            SpawnRanged = saveRanged;
            SpawnClass = saveClass;
        }

        static Vector2 Rotate(Vector2 v, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        /// <summary>A few of the weapon's motes flung from the shot.</summary>
        static void RangedSpark(Proj p, int n)
        {
            var pp = TerraParticles.Instance;
            if (pp == null || p.Ranged?.Item == null) return;
            var look = RangedArts.Of(p.Ranged.Item);
            var at = TerraParticles.ToPx(p.Pos);
            for (int k = 0; k < n; k++)
            {
                int d = pp.NewDust(at - new Vector2(4f, 4f), 0, 0, look.Dust, 0f, 0f, look.Dust == 278 ? 40 : 90, Random.Range(0.7f, 1.1f));
                if (d < 0) continue;
                ref var dust = ref pp[d];
                dust.Position = at;
                dust.Velocity = Random.insideUnitCircle.normalized * Random.Range(1.5f, 3.5f);
                dust.NoGravity = true;
                if (look.Dust == 278) dust.Color = Color.Lerp(look.A, look.B, Random.value);
            }
        }

        /// <summary>A ring of the weapon's motes the burst's size, and a little smoke.</summary>
        static void RangedRing(Proj p, float sizePx)
        {
            var pp = TerraParticles.Instance;
            if (pp == null || p.Ranged?.Item == null) return;
            var look = RangedArts.Of(p.Ranged.Item);
            var at = TerraParticles.ToPx(p.Pos);
            for (int k = 0; k < 16; k++)
            {
                float a = k / 16f * Mathf.PI * 2f;
                var way = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                int d = pp.NewDust(at - new Vector2(4f, 4f), 0, 0, look.Dust, 0f, 0f, look.Dust == 278 ? 40 : 90, Random.Range(0.8f, 1.2f));
                if (d >= 0)
                {
                    ref var dust = ref pp[d];
                    dust.Position = at + way * 4f;
                    dust.Velocity = way * sizePx / 22f;
                    dust.NoGravity = true;
                    if (look.Dust == 278) dust.Color = Color.Lerp(look.A, look.B, Random.value);
                }
            }
            for (int k = 0; k < 4; k++)
            {
                int d = pp.NewDust(at - new Vector2(10f, 10f), 20, 20, 31, 0f, 0f, 120, Random.Range(0.9f, 1.3f));
                if (d >= 0) pp[d].Velocity *= 0.4f;
            }
        }

        /// <summary>Dev: the remade shots alive, and the ones of them with a sure critical hit.</summary>
        public int DevRangedLive(out int sure)
        {
            sure = 0;
            int n = 0;
            foreach (var p in live)
                if (!p.Dead && p.Ranged != null) { n++; if (p.Ranged.SureCrit) sure++; }
            return n;
        }
    }
}
