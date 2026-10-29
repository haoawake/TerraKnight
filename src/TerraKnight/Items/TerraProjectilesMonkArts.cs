using System.Collections.Generic;
using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The monk staffs' remade techniques (the user's 10-26 "继续升级剩下的这些…依旧是原来的要求，特效，动画，粒子效果，新颖，多样";
    /// with "近战挥砍：新版" on): each use goes on to the staff's next technique when it comes within half a second of the
    /// last one's end, the first being the game's own (the whirl and slam, the ghostly thrust, the dragon's whirl). The others
    /// are the staff's own projectile (its picture, its hit cooldown, the Glaive's ghast on a hit) driven here: swept from
    /// its end through an arc (a sweep, a rising cut, an overhead chop that strikes the ground), whirled about its middle, or
    /// thrust; reaching about a third further than the game's (Reach); a streak of light along its head and middle (Extra_98),
    /// the staff's own motes (the Octopod's green ink, the Glaive's wisps, the Fury's sparks). The right-click arts
    /// (BladeSkillsZenith) go through the same: the Octopod's march of slams, the Glaive's ghasts, the Fury's dragon dash.
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        enum StaffShape : byte { Sweep, Spin, Thrust, Dash }

        enum StaffEvent : byte { None, Slam, GhostBurst, Bolt, BoltsUp, Ghasts, Quake, DashBolts }

        sealed class StaffMove
        {
            public string Name;
            public StaffShape Shape;
            /// <summary>Its length in uses; the arc's ends (degrees from the way he faces, + up; a whirl's start and turns).</summary>
            public float Length = 1f, From, To;
            /// <summary>Its blows' share of the staff's damage.</summary>
            public float Damage = 1f;
            /// <summary>A chop: raised a little further, then brought down faster and faster, held where it struck.</summary>
            public bool Chop;
            /// <summary>The game's own right-click thrust (the Fury's), taken into the left's techniques.</summary>
            public bool GameThrust;
            public StaffEvent Event;
            public float EventAt = -1f;
        }

        /// <summary>Each staff's techniques in turn; null is the game's own.</summary>
        static readonly Dictionary<ItemType, StaffMove[]> staffMoves = new Dictionary<ItemType, StaffMove[]>
        {
            [ItemType.MonkStaffT1] = new[]
            {
                null,
                new StaffMove { Name = "横扫千军", Shape = StaffShape.Sweep, From = 155f, To = -40f, Length = 1.15f, Damage = 2.7f },
                new StaffMove { Name = "上挑", Shape = StaffShape.Sweep, From = -60f, To = 120f, Length = 1f, Damage = 2.7f },
                new StaffMove { Name = "碎地击", Shape = StaffShape.Sweep, From = 115f, To = -62f, Length = 1.2f, Damage = 1.8f, Chop = true, Event = StaffEvent.Slam, EventAt = 0.72f },
            },
            [ItemType.MonkStaffT2] = new[]
            {
                null,
                new StaffMove { Name = "回旋斩", Shape = StaffShape.Spin, From = 100f, To = 1.5f, Length = 1.25f, Damage = 1f },
                new StaffMove { Name = "上挑", Shape = StaffShape.Sweep, From = -55f, To = 115f, Length = 1f, Damage = 1.15f },
                new StaffMove { Name = "断魂劈", Shape = StaffShape.Sweep, From = 110f, To = -58f, Length = 1.15f, Damage = 1.35f, Chop = true, Event = StaffEvent.GhostBurst, EventAt = 0.72f },
            },
            [ItemType.MonkStaffT3] = new[]
            {
                null,
                new StaffMove { Name = "龙突", Shape = StaffShape.Thrust, GameThrust = true },
                new StaffMove { Name = "龙尾扫", Shape = StaffShape.Sweep, From = 150f, To = -45f, Length = 1.1f, Damage = 1.2f, Event = StaffEvent.Bolt, EventAt = 0.8f },
                new StaffMove { Name = "升龙", Shape = StaffShape.Sweep, From = -60f, To = 125f, Length = 1.05f, Damage = 1.2f, Event = StaffEvent.BoltsUp, EventAt = 0.6f },
            },
        };

        static readonly Dictionary<string, StaffMove> staffSkillMoves = new Dictionary<string, StaffMove>
        {
            ["大地苏醒"] = new StaffMove { Name = "大地苏醒", Shape = StaffShape.Sweep, From = 125f, To = -62f, Length = 0.8f, Chop = true, Event = StaffEvent.Quake, EventAt = 0.7f },
            ["百鬼夜行"] = new StaffMove { Name = "百鬼夜行", Shape = StaffShape.Thrust, Length = 1f, Event = StaffEvent.Ghasts, EventAt = 0.3f },
            ["天龙冲"] = new StaffMove { Name = "天龙冲", Shape = StaffShape.Dash, Length = 1f, Event = StaffEvent.DashBolts, EventAt = 0.55f },
        };

        /// <summary>The game's whirl's reach from the player's centre (px) and the remade one's, by staff.</summary>
        static float StaffReach(ItemType t) => t == ItemType.MonkStaffT1 ? 95f : t == ItemType.MonkStaffT2 ? 120f : 143f;

        sealed class StaffArt
        {
            public StaffMove Move;
            public ItemDef Def;
            public float Dir, Tilt, T, N, Total, Reach, Len, Ang, Prev, Charge;
            public Vector2 Pivot, Head, Butt, Middle;
            public bool Skill, Fired;
            /// <summary>The ground's top under the player (px, y down; +inf with none): a head swept low stops on it.</summary>
            public float Floor = float.PositiveInfinity;
            public int Marched, March;
            public Vector2 DashFrom, DashTo;
            public readonly List<Vector2> Heads = new List<Vector2>(), Mids = new List<Vector2>();
        }

        static readonly Dictionary<Proj, StaffArt> staffArts = new Dictionary<Proj, StaffArt>();
        static readonly Dictionary<ItemType, (int Move, float Until)> staffCombo = new Dictionary<ItemType, (int, float)>();

        /// <summary>Dev: the last technique begun and how many; the farthest a remade one's head went from the player (px); the slams of the march.</summary>
        public static string DevStaffMove = "";
        public static int DevStaffMoves, DevQuakeSlams, DevStaffDashes;
        public static float DevStaffFar;

        static ProjKind StaffKind(ItemType t) => t == ItemType.MonkStaffT1 ? ProjKind.MonkStaffT1Spin : t == ItemType.MonkStaffT2 ? ProjKind.MonkStaffT2 : ProjKind.MonkStaffT3Spin;

        /// <summary>The staffs' own kinds made to run a remade technique when one is set (their update, picture, hits and death wrapped).</summary>
        static void RegisterMonkArts()
        {
            foreach (var k in new[] { ProjKind.MonkStaffT1Spin, ProjKind.MonkStaffT2, ProjKind.MonkStaffT3Spin })
            {
                var s = specs[k];
                var update = s.Update;
                var draw = s.Draw;
                var kill = s.OnKill;
                s.Update = (t, p) => { if (staffArts.TryGetValue(p, out var a)) t.StepStaffArt(p, a); else update(t, p); };
                s.Draw = (t, fx, p, px) => { if (staffArts.TryGetValue(p, out var a)) DrawStaffArt(fx, p, a); else draw(t, fx, p, px); };
                s.OnKill = (t, p) => { staffArts.Remove(p); kill?.Invoke(t, p); };
                if (s.Beam != null)
                {
                    var beam = s.Beam;
                    // The Glaive's line: along the staff from its head to its butt.
                    s.Beam = p => staffArts.TryGetValue(p, out var a) ? (a.Head, a.Butt, 30f) : beam(p);
                }
                else
                {
                    var area = s.HitArea;
                    var test = s.HitTest;
                    s.HitArea = p => staffArts.TryGetValue(p, out var a) ? (TerraParticles.ToWorld(a.Pivot), Vector2.one * (a.Reach + 40f) * 2f * Px) : area(p);
                    s.HitTest = (p, col) => staffArts.TryGetValue(p, out var a) ? StaffArtHits(a, col) : test(p, col);
                }
            }
        }

        /// <summary>
        /// A monk staff's use with the remade swings on: its next technique in turn (within half a second of the last one's
        /// end), made here; false for the game's own (the first, or the swings off), which the shot's code then makes.
        /// </summary>
        public bool StaffArtShoot(ItemDef def, Vector2 from, Vector2 vel, int damage, float knockback, float crit, Vector2 mouse, int useTicks)
        {
            if (def == null || !BladeArts.Enabled || !staffMoves.TryGetValue(def.Type, out var moves)) return false;
            int next = staffCombo.TryGetValue(def.Type, out var c) && Time.time <= c.Until ? (c.Move + 1) % moves.Length : 0;
            var move = moves[next];
            useTicks = Mathf.Max(3, useTicks);
            // The game's own: its end not known here, so its likely length (the whirl's 40-50 ticks, the thrust's use).
            float guess = def.Type == ItemType.MonkStaffT2 ? useTicks : 45f;
            staffCombo[def.Type] = (next, Time.time + (move == null || move.GameThrust ? guess : useTicks * move.Length) / 60f + 0.5f);
            DevStaffMove = move?.Name ?? (def.Type == ItemType.MonkStaffT1 ? "旋舞重击" : def.Type == ItemType.MonkStaffT2 ? "鬼刺" : "旋舞");
            DevStaffMoves++;
            if (move == null) return false;
            if (move.GameThrust)
            {
                // The Fury's own thrust (its right click in the game): half damage, three dragon bolts.
                float dir = mouse.x < from.x ? -1f : 1f;
                float ai0 = (0.3f + 0.7f * Random.value) * def.ShootSpeed * 1.75f * dir;
                ShootMonk(ProjKind.MonkStaffT3Thrust, from, vel * 1.5f, (int)(damage * 0.5f), knockback + 4f, crit, ai0, useTicks);
                return true;
            }
            SpawnStaffArt(def, move, from, mouse, Mathf.Max(1, Mathf.RoundToInt(damage * move.Damage)), knockback, crit, useTicks, false, 0f);
            return true;
        }

        /// <summary>A right-click art's staff (BladeSkillsZenith): the move named, its damage, how full the charge was.</summary>
        public bool StaffSkill(ItemDef def, HeroController hero, string move, int damage, float charge)
        {
            if (def == null || hero == null || !staffSkillMoves.TryGetValue(move, out var m)) return false;
            foreach (var q in live) if (!q.Dead && (q.Kind == StaffKind(def.Type) || q.Kind == ProjKind.MonkStaffT3Thrust)) Kill(q);
            var mod = TerraMod.Instance;
            Vector2 from = hero.GetComponent<BoxCollider2D>().bounds.center;
            var aim = mod != null ? mod.DevAim ?? mod.AimPoint : from + Vector2.right;
            // A dash goes the way he faces (the user's 10-26 "位移方向应该按照人物朝向而不是鼠标方向").
            if (m.Shape == StaffShape.Dash) aim = from + new Vector2(hero.cState.facingRight ? 1f : -1f, 0f);
            int use = Mathf.Max(12,Mathf.RoundToInt(def.UseTime * 60f));
            var p = SpawnStaffArt(def, m, from, aim, damage, def.Knockback, def.Crit, use, true, charge);
            return p != null;
        }

        Proj SpawnStaffArt(ItemDef def, StaffMove move, Vector2 from, Vector2 mouse, int damage, float knockback, float crit, int useTicks, bool skill, float charge)
        {
            var keep = SpawnClass;
            SpawnClass = DamageClass.Melee;
            float dir = mouse.x < from.x ? -1f : 1f;
            var p = Spawn(StaffKind(def.Type), from, new Vector2(dir, 0f), damage, knockback, crit);
            SpawnClass = keep;
            if (p == null) return null;
            p.Melee = true;
            p.Dir = dir;
            p.Ai1 = useTicks;
            var to = TerraParticles.ToPx(mouse) - TerraParticles.ToPx(from);
            float elevation = Mathf.Atan2(-to.y, Mathf.Abs(to.x) < 1e-3f ? 1e-3f : Mathf.Abs(to.x)) * Mathf.Rad2Deg;
            var a = new StaffArt
            {
                Move = move, Def = def, Dir = dir, Skill = skill, Charge = charge, Reach = StaffReach(def.Type),
                // An arc tilts a little toward the cursor; a thrust goes along it.
                Tilt = move.Shape == StaffShape.Thrust ? Mathf.Clamp(elevation, -70f, 70f) : move.Shape == StaffShape.Dash ? 0f : Mathf.Clamp(elevation, -40f, 40f) * 0.5f,
                N = Mathf.Max(6f, useTicks * move.Length),
            };
            var whole = FxBatch.Whole(p.Spec.Texture);
            a.Len = whole != null ? Mathf.Min(new Vector2(whole.rect.width, whole.rect.height).magnitude * p.Spec.Scale, a.Reach - 12f) : a.Reach * 0.7f;
            a.Total = a.N;
            if (move.Event == StaffEvent.Quake)
            {
                a.March = 3 + Mathf.RoundToInt(4f * charge);
                a.Total = a.N * move.EventAt + a.March * 5f + 6f;
            }
            if (move.Shape == StaffShape.Dash)
            {
                a.N = 22f;
                a.Total = 26f;
                var hero = HeroController.SilentInstance;
                if (hero != null)
                {
                    var b = hero.GetComponent<BoxCollider2D>().bounds;
                    var way = new Vector2(dir, 0f);
                    float far = 3.5f + 3.5f * charge;
                    var hit = Physics2D.BoxCast(b.center, b.size * 0.9f, 0f, way, far, (1 << 8) | (1 << ChunkColliders.TerrainLayer));
                    if (hit.collider != null) far = Mathf.Max(0f, hit.distance - 0.05f);
                    a.DashFrom = b.center;
                    a.DashTo = (Vector2)b.center + way * far;
                    Player.TerraHurt.Invulnerable(hero, 40);
                    DevStaffDashes++;
                }
            }
            a.Ang = AngleAt(a, 0f);
            a.Prev = a.Ang;
            if (Holder(out var centre)) Place(a, centre);
            staffArts[p] = a;
            TerraMod.Instance?.PlaySound(def.UseSound ?? "Item_1", 0.8f);
            return p;
        }

        /// <summary>The staff's way at a point of its technique (degrees from the way he faces, + up, before the tilt).</summary>
        static float AngleAt(StaffArt a, float t)
        {
            var m = a.Move;
            switch (m.Shape)
            {
                case StaffShape.Spin:
                    return m.From + m.To * 360f * Smooth(t);
                case StaffShape.Thrust:
                case StaffShape.Dash:
                    return 0f;
                default:
                    if (!m.Chop) return Mathf.Lerp(m.From, m.To, Smooth(t));
                    // Raised a little further first, then brought down faster and faster; held where it struck.
                    const float lift = 0.3f, fall = 0.42f;
                    if (t < lift) return m.From + 18f * Smooth(t / lift);
                    float u = Mathf.Clamp01((t - lift) / fall);
                    return Mathf.Lerp(m.From + 18f, m.To, u * u);
            }
        }

        static float Smooth(float t) => 0.5f - 0.5f * Mathf.Cos(Mathf.Clamp01(t) * Mathf.PI);

        /// <summary>A way in degrees (from the way he faces, + up, tilted toward the cursor) as a unit in Terraria px (y down).</summary>
        static Vector2 StaffAxis(StaffArt a, float deg)
        {
            float r = (deg + a.Tilt) * Mathf.Deg2Rad;
            return new Vector2(a.Dir * Mathf.Cos(r), -Mathf.Sin(r));
        }

        /// <summary>A staff held at its end swept low stops with its head on the ground (it strikes it rather than going into it).</summary>
        static float OffFloor(StaffArt a, float deg)
        {
            if (a.Move.Shape == StaffShape.Spin || float.IsInfinity(a.Floor)) return deg;
            float drop = a.Floor - 3f - a.Pivot.y;
            if (drop >= a.Reach) return deg;
            float lowest = -Mathf.Asin(Mathf.Clamp(drop / a.Reach, -1f, 1f)) * Mathf.Rad2Deg - a.Tilt;
            // Below level in front, or behind: the lowest way that keeps the head over the ground on that side.
            if (deg < lowest) return lowest;
            if (deg > 180f - lowest - 2f * a.Tilt) return 180f - lowest - 2f * a.Tilt;
            return deg;
        }

        /// <summary>How far out a thrust's head is (0 drawn back .. 1 at full stretch).</summary>
        static float ThrustOut(float t) => t < 0.35f ? Smooth(t / 0.35f) : t < 0.65f ? 1f : 1f - Smooth((t - 0.65f) / 0.35f) * 0.75f;

        static void Place(StaffArt a, Vector2 centre)
        {
            a.Pivot = centre;
            var axis = StaffAxis(a, a.Ang);
            switch (a.Move.Shape)
            {
                case StaffShape.Spin:
                {
                    a.Middle = centre + new Vector2(a.Dir * 10f, 0f);
                    // Whirled upright, the staff is lifted so its lower end stays over the ground.
                    float low = a.Middle.y + Mathf.Abs(axis.y) * a.Len * 0.5f - (a.Floor - 3f);
                    if (low > 0f) a.Middle.y -= low;
                    a.Head = a.Middle + axis * a.Len * 0.5f;
                    a.Butt = a.Middle - axis * a.Len * 0.5f;
                    break;
                }
                case StaffShape.Thrust:
                case StaffShape.Dash:
                {
                    float t = Mathf.Clamp01(a.T / a.N);
                    float reach = a.Move.Shape == StaffShape.Dash ? a.Reach * 0.85f : Mathf.Lerp(a.Len * 0.55f, a.Reach, ThrustOut(t));
                    a.Head = centre + axis * reach;
                    a.Butt = a.Head - axis * a.Len;
                    a.Middle = (a.Head + a.Butt) * 0.5f;
                    break;
                }
                default:
                    a.Head = centre + axis * a.Reach;
                    a.Butt = a.Head - axis * a.Len;
                    a.Middle = (a.Head + a.Butt) * 0.5f;
                    break;
            }
        }

        void StepStaffArt(Proj p, StaffArt a)
        {
            var mod = TerraMod.Instance;
            if (mod == null || !Holder(out var centre) || mod.Inventory.Current.Type != a.Def.Type)
            {
                Kill(p);
                return;
            }
            p.TimeLeft = 2;
            p.Alpha = 0f;
            a.T++;
            float t = Mathf.Clamp01(a.T / a.N);
            if (a.Move.Shape == StaffShape.Dash) centre = StepDash(a, centre);
            a.Prev = a.Ang;
            a.Floor = GroundUnder(centre) ?? float.PositiveInfinity;
            a.Ang = OffFloor(a, AngleAt(a, t));
            Place(a, centre);
            p.Pos = TerraParticles.ToWorld(a.Head);
            var axis = (a.Head - a.Butt).normalized;
            float rot = Mathf.Atan2(axis.y, axis.x);
            p.TRot = rot + Mathf.PI / 4f * a.Dir - (a.Dir < 0f ? Mathf.PI : 0f);
            p.Dir = a.Dir;
            p.Vel = new Vector2(a.Dir, 0f);
            a.Heads.Add(a.Head);
            a.Mids.Add(Vector2.Lerp(a.Butt, a.Head, 0.55f));
            if (a.Heads.Count > 9) { a.Heads.RemoveAt(0); a.Mids.RemoveAt(0); }
            DevStaffFar = Mathf.Max(DevStaffFar, (a.Head - centre).magnitude);
            StaffMotes(a, (a.Head - (a.Heads.Count > 1 ? a.Heads[a.Heads.Count - 2] : a.Head)));
            if (!a.Fired && a.Move.EventAt >= 0f && t >= a.Move.EventAt)
            {
                a.Fired = true;
                StaffEventGo(p, a);
            }
            if (a.Move.Event == StaffEvent.Quake && a.Fired) StepQuake(p, a);
            mod.KeepUsing(a.Def, Flip(axis), face: a.Dir);
            if (a.T >= a.Total)
            {
                Kill(p);
                mod.ReuseDelay(2);
                if (!a.Skill && staffCombo.TryGetValue(a.Def.Type, out var c)) staffCombo[a.Def.Type] = (c.Move, Time.time + 0.5f);
            }
        }

        /// <summary>The Fury's dash: the player carried along it for its first ten ticks, untouchable, then let go with a little speed.</summary>
        static Vector2 StepDash(StaffArt a, Vector2 centre)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null) return centre;
            var rb = hero.GetComponent<Rigidbody2D>();
            var b = hero.GetComponent<BoxCollider2D>().bounds;
            Vector2 offset = (Vector2)hero.transform.position - (Vector2)b.center;
            const float dashTicks = 10f;
            if (a.T <= dashTicks && rb != null)
            {
                var at = Vector2.Lerp(a.DashFrom, a.DashTo, Smooth(a.T / dashTicks)) + offset;
                rb.position = at;
                hero.transform.position = new Vector3(at.x, at.y, hero.transform.position.z);
                rb.linearVelocity = Vector2.zero;
                return TerraParticles.ToPx((Vector2)at - offset);
            }
            if (a.T - 1f <= dashTicks && rb != null) rb.linearVelocity = new Vector2(a.Dir * 3f * Player.TerraMovement.PxT, 0f);
            return centre;
        }

        /// <summary>Whether an enemy's box lies where the staff went this tick: the arc it swept, its whirl's disc, its line.</summary>
        static bool StaffArtHits(StaffArt a, Collider2D col)
        {
            var b = col.bounds;
            var tl = TerraParticles.ToPx(new Vector2(b.min.x, b.max.y));
            var br = TerraParticles.ToPx(new Vector2(b.max.x, b.min.y));
            var mid = (tl + br) * 0.5f;
            Vector2 Closest(Vector2 q) => new Vector2(Mathf.Clamp(q.x, tl.x, br.x), Mathf.Clamp(q.y, tl.y, br.y));
            // Along the staff's line now (and, for a dash, from where the run began).
            float width = a.Move.Shape == StaffShape.Dash ? 30f : 18f;
            for (int k = 0; k <= 6; k++)
            {
                var q = Vector2.Lerp(a.Butt, a.Head, k / 6f);
                if ((Closest(q) - q).magnitude <= width) return true;
            }
            if (a.Move.Shape == StaffShape.Spin) return (Closest(a.Middle) - a.Middle).magnitude <= a.Len * 0.5f + 12f;
            // The run's own line, while it runs.
            if (a.Move.Shape == StaffShape.Dash && a.T <= 18f)
            {
                var from = TerraParticles.ToPx(a.DashFrom);
                for (int k = 0; k <= 8; k++)
                {
                    var q = Vector2.Lerp(from, a.Pivot, k / 8f);
                    if ((Closest(q) - q).magnitude <= 28f) return true;
                }
                return false;
            }
            if (a.Move.Shape == StaffShape.Thrust || a.Move.Shape == StaffShape.Dash) return false;
            // The arc swept since the last tick: a point of the box between the two ways, within the staff's length.
            float lo = Mathf.Min(a.Prev, a.Ang), hi = Mathf.Max(a.Prev, a.Ang);
            foreach (var q in new[] { Closest(a.Pivot), mid, tl, br, new Vector2(tl.x, br.y), new Vector2(br.x, tl.y) })
            {
                var rel = q - a.Pivot;
                float d = rel.magnitude;
                if (d > a.Reach + 14f || d < a.Reach - a.Len - 14f) continue;
                float deg = Mathf.Atan2(-rel.y, rel.x * a.Dir) * Mathf.Rad2Deg - a.Tilt;
                while (deg < lo - 180f) deg += 360f;
                while (deg > hi + 180f) deg -= 360f;
                float pad = 10f + Mathf.Atan2(16f, Mathf.Max(d, 1f)) * Mathf.Rad2Deg;
                if (deg >= lo - pad && deg <= hi + pad) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ the techniques' happenings

        void StaffEventGo(Proj p, StaffArt a)
        {
            var axis = (a.Head - a.Butt).normalized;
            switch (a.Move.Event)
            {
                case StaffEvent.Slam:
                {
                    // The Octopod's own slam (698) where the head strikes, at twice the staff's blow.
                    var at = GroundUnder(a.Head + new Vector2(0f, -20f)) is float g ? new Vector2(a.Head.x, g - 100f) : a.Head + new Vector2(0f, -60f);
                    var slam = MeleeChild(ProjKind.MonkStaffT1Slam, at, new Vector2(a.Dir, 0f), Mathf.RoundToInt(p.Damage * 1.25f), 0f, p.Crit + 25f);
                    if (slam != null) slam.Vel = new Vector2(a.Dir, 0f);
                    Sound("Custom/dd2_monk_staff_ground_impact_" + Random.Range(0, 3));
                    Player.TerraCamera.Punch(0.12f, 0.15f);
                    DevMonkSlams++;
                    break;
                }
                case StaffEvent.GhostBurst:
                {
                    // A ring of wisps thrown off where the blade lands, and a ghast called through the nearest enemy.
                    for (int k = 0; k < 22; k++)
                    {
                        float ang = k / 22f * Mathf.PI * 2f;
                        int d = PointDust(a.Head, 228, new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.6f) * (2.5f + Random.value * 2f), 110, 1.1f + Random.value * 0.5f);
                        if (d >= 0) D(d).NoGravity = true;
                    }
                    SummonGhastAt(p, a.Head, 1);
                    Sound("Custom/dd2_ghastly_glaive_impact_ghost_" + Random.Range(0, 3));
                    break;
                }
                case StaffEvent.Bolt:
                    MeleeChild(ProjKind.SkyDragonBolt, a.Head, axis * 11f, p.Damage, 0f, p.Crit);
                    DevSkyBolts++;
                    break;
                case StaffEvent.BoltsUp:
                    // Three dragon bolts fanned up the way the staff rises.
                    for (int k = -1; k <= 1; k++)
                    {
                        var v = RotateBy(new Vector2(a.Dir * 0.55f, -1f).normalized, k * 0.32f * a.Dir) * (10f + Random.Range(0, 3));
                        MeleeChild(ProjKind.SkyDragonBolt, a.Head, v, p.Damage, 0f, p.Crit);
                        DevSkyBolts++;
                    }
                    break;
                case StaffEvent.Ghasts:
                    SummonGhastAt(p, AimPx(), 4);
                    break;
                case StaffEvent.Quake:
                    a.Marched = 0;
                    break;
                case StaffEvent.DashBolts:
                {
                    // Bolts loosed ahead as the run ends: three, five at a full charge; each the staff's own blow.
                    int n = 3 + (a.Charge >= 0.99f ? 2 : 0);
                    int bolt = Mathf.Max(1, Mathf.RoundToInt(p.Damage / (1.5f + 1.5f * a.Charge)));
                    // At the nearest enemy within reach of the run's end (one passed through, behind), else ahead.
                    var way = new Vector2(a.Dir, 0f);
                    var near = EnemiesNear(a.Head, 420f);
                    if (near.Count > 0)
                    {
                        var best = near[0];
                        foreach (var e in near) if ((EnemyCenterPx(e) - a.Head).sqrMagnitude < (EnemyCenterPx(best) - a.Head).sqrMagnitude) best = e;
                        way = (EnemyCenterPx(best) - a.Head).normalized;
                    }
                    for (int k = 0; k < n; k++)
                    {
                        float turn = (k - (n - 1) / 2f) * 0.22f;
                        MeleeChild(ProjKind.SkyDragonBolt, a.Head, RotateBy(way, turn) * (11f + Random.Range(0, 3)), bolt, 0f, p.Crit);
                        DevSkyBolts++;
                    }
                    break;
                }
            }
        }

        /// <summary>The Octopod's march: a slam every five ticks, each 56 px further along the ground the way he faces.</summary>
        void StepQuake(Proj p, StaffArt a)
        {
            float since = a.T - a.N * a.Move.EventAt;
            while (a.Marched < a.March && since >= a.Marched * 5f)
            {
                var x = a.Head.x + a.Dir * (16f + 56f * a.Marched);
                var probe = new Vector2(x, a.Head.y - 40f);
                var at = GroundUnder(probe) is float g ? new Vector2(x, g - 100f) : new Vector2(x, a.Head.y - 60f);
                var slam = MeleeChild(ProjKind.MonkStaffT1Slam, at, new Vector2(a.Dir, 0f), p.Damage, 0f, p.Crit + 10f);
                if (slam != null) slam.Vel = new Vector2(a.Dir, 0f);
                Geyser(new Vector2(x, at.y + 100f), a.Dir);
                if (a.Marched % 2 == 0) Sound("Custom/dd2_monk_staff_ground_impact_" + Random.Range(0, 3), 0.8f);
                if (a.Marched == 0) Player.TerraCamera.Punch(0.18f, 0.25f);
                a.Marched++;
                DevQuakeSlams++;
            }
        }

        /// <summary>A quake's spout where it breaks the ground: green ink thrown up in a narrow column, smoke after it.</summary>
        static void Geyser(Vector2 ground, float dir)
        {
            for (int k = 0; k < 16; k++)
            {
                int d = PointDust(ground + new Vector2(Random.Range(-10f, 10f), -2f), 55, new Vector2(Random.Range(-1.2f, 1.2f) + dir * 0.6f, -5f - Random.value * 6f), 50, 1.1f + Random.value * 0.6f);
                if (d < 0) continue;
                D(d).Color = FxBatch.C(20, 255, 100, 200);
                D(d).NoGravity = Random.value < 0.3f;
            }
            for (int k = 0; k < 5; k++)
            {
                int d = PointDust(ground + new Vector2(Random.Range(-14f, 14f), -6f), 31, new Vector2(Random.Range(-0.8f, 0.8f), -1.5f - Random.value * 2f), 120, 1.2f);
                if (d >= 0) D(d).NoGravity = true;
            }
        }

        /// <summary>The ground's top under a point (Terraria px, y down) within 12 blocks, if any.</summary>
        static float? GroundUnder(Vector2 px)
        {
            var w = TerraParticles.ToWorld(px);
            for (float dy = 0f; dy < 6f; dy += 0.125f)
                if (TerraWorld.IsSolidAt(new Vector2(w.x, w.y - dy)))
                    return TerraParticles.ToPx(new Vector2(w.x, w.y - dy + 0.0625f)).y;
            return null;
        }

        /// <summary>Ghasts (700) called through enemies near a point (else across the point itself), each with the staff's damage.</summary>
        void SummonGhastAt(Proj p, Vector2 near, int count)
        {
            var enemies = EnemiesNear(near, 360f);
            for (int k = 0; k < count; k++)
            {
                var centre = near + new Vector2(Random.Range(-40f, 40f), Random.Range(-30f, 30f));
                if (enemies.Count > 0) centre = EnemyCenterPx(enemies[k % enemies.Count]);
                int side = (k % 2 == 0 ? 1 : -1) * (Random.value < 0.5f ? 1 : -1);
                var from = centre + new Vector2(-side * 120f, Random.Range(-24f, 24f));
                var v = new Vector2(side * (4f + Random.Range(0, 3)), 0f) + (centre - from).normalized * 2f;
                MeleeChild(ProjKind.MonkStaffT2Ghast, from, v, p.Damage, 0f, p.Crit);
                DevGhasts++;
            }
        }

        // ------------------------------------------------------------------ the look

        static readonly Color OctopodGreen = new Color(0.25f, 1f, 0.5f), GlaiveGhost = new Color(0.6f, 0.95f, 1f), FuryBlue = new Color(0.45f, 0.8f, 1.15f);

        static Color StaffColour(ItemType t) => t == ItemType.MonkStaffT1 ? OctopodGreen : t == ItemType.MonkStaffT2 ? GlaiveGhost : FuryBlue;

        /// <summary>The staff's motes as it goes: the Octopod's green ink and smoke, the Glaive's wisps, the Fury's sparks and light.</summary>
        static void StaffMotes(StaffArt a, Vector2 moved)
        {
            var push = moved * 0.25f;
            var head = a.Head;
            switch (a.Def.Type)
            {
                case ItemType.MonkStaffT1:
                {
                    if (Random.value < 0.7f)
                    {
                        int d = PointDust(head + Random.insideUnitCircle * 6f, 55, push + Random.insideUnitCircle, 50, 0.9f + Random.value * 0.4f);
                        if (d >= 0) { D(d).NoGravity = true; D(d).Color = FxBatch.C(20, 255, 100, 160); }
                    }
                    if (Random.value < 0.25f)
                    {
                        int d = PointDust(Vector2.Lerp(a.Butt, head, Random.value), 31, push * 0.4f, 150, 1f);
                        if (d >= 0) D(d).NoGravity = true;
                    }
                    break;
                }
                case ItemType.MonkStaffT2:
                    for (int k = 0; k < 2; k++)
                    {
                        if (Random.value < 0.35f) continue;
                        int d = PointDust(Vector2.Lerp(a.Butt, head, 0.6f + 0.4f * Random.value), 228, push * 0.6f + Random.insideUnitCircle * 0.8f, 110, 0.9f + Random.value * 0.6f);
                        if (d >= 0) D(d).NoGravity = true;
                    }
                    break;
                default:
                {
                    FxBatch.Instance?.Light(head, new Color(0.4f, 0.75f, 1f), 3f);
                    if (Random.value < 0.75f)
                    {
                        int d = PointDust(head + Random.insideUnitCircle * 4f, 226, push * 0.5f + Random.insideUnitCircle * 1.5f, 100, 0.55f + Random.value * 0.3f);
                        if (d >= 0) D(d).NoGravity = Random.value < 0.75f;
                    }
                    if (a.Move.Shape == StaffShape.Dash)
                        for (int k = 0; k < 3; k++)
                        {
                            int d = PointDust(a.Pivot + Random.insideUnitCircle * 16f, 226, new Vector2(-a.Dir * (2f + Random.value * 3f), Random.Range(-1f, 1f)), 100, 0.7f);
                            if (d >= 0) D(d).NoGravity = true;
                        }
                    break;
                }
            }
        }

        /// <summary>
        /// The staff about its middle (turned, mirrored with the player, as the game draws its whirl), its glow (the Glaive's
        /// ghostly edge, the Fury's runes), and the streaks its head and middle leave, brightest at the newest.
        /// </summary>
        static void DrawStaffArt(FxBatch fx, Proj p, StaffArt a)
        {
            var colour = StaffColour(a.Def.Type);
            int n = a.Heads.Count;
            for (int k = 1; k < n; k++)
            {
                float f = k / (n - 1f);
                StreakPx(fx, a.Heads[k - 1], a.Heads[k], Color.Lerp(colour, Color.white, f * 0.35f), 6f + 12f * f, 0.75f * f);
                StreakPx(fx, a.Mids[k - 1], a.Mids[k], colour, 4f + 7f * f, 0.4f * f);
            }
            var spr = FxBatch.Whole(p.Spec.Texture);
            if (spr == null) return;
            bool flip = a.Dir < 0f;
            bool octopod = a.Def.Type == ItemType.MonkStaffT1;
            fx.Draw(spr, a.Middle, octopod || a.Def.Type == ItemType.MonkStaffT2 ? new Color(L, L, L, 1f) : Color.white, p.TRot, p.Scale, flipX: flip, overPlayer: true);
            if (a.Def.Type == ItemType.MonkStaffT2)
            {
                var glow = FxBatch.Whole("Glow_231");
                if (glow != null) fx.Draw(glow, a.Middle, new Color(1f, 1f, 1f, 127f / 255f), p.TRot, p.Scale, flipX: flip, overPlayer: true);
            }
            else if (!octopod) DrawFuryGlow(fx, p, a.Middle, ((int)a.T % 9) / 3, 0.6f);
        }

        /// <summary>Dev: the remade staffs' state cleared (a test's next weapon starts its techniques from the first).</summary>
        public static void DevStaffReset()
        {
            staffCombo.Clear();
            DevStaffFar = 0f;
        }
    }
}
