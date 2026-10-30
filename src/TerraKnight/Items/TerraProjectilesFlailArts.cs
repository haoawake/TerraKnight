using System.Collections.Generic;
using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The flails remade (the user's 10-26 "继续升级剩下的这些…特效，动画，粒子效果，新颖，多样", after the spears' "可以多几种
    /// 枪法，也有一些右键特殊能力，也有更好的粒子特效…范围也搞大一点"): with the remade swings on (BladeArts.Enabled) each use of a
    /// flail is one style of its own, the next while the uses follow on (within a second and a half of the last coming home):
    /// a way of spinning it while held (横轮 the game's wheel; 头顶回旋 a lasso over the head; 八字 a figure eight in front; 地扫
    /// a low wide sweep at the shins; 蓄势 a wind-up spinning faster and wider the longer it is held) and a way of throwing it
    /// (直抛 straight out; 弧砸 lobbed over to the cursor, smashing the ground there with a shock all round; 跳弹 skimmed low,
    /// skipping off the ground up to three times with a jolt at each; 回旋抛 hooked round in a curve). The spin and the
    /// chain reach 30% further; the chain sags with its weight as it slackens; a ribbon of the flail's colours follows the
    /// ball, afterimages behind a fast one, motes of its own (sparks, fire, corruption, blood, moonlight, sunfire, yin and
    /// yang, petals, sea foam) off it, a burst where it lands. The left button never moves the player. Each flail's
    /// right-click art (BladeSkillsFlail) runs its ball through a scripted path here (FlailArt). Damage as the game's: the
    /// spin's 1.2 times and the throw's twice, every 15 (12, 10) ticks; a shock's share once.
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        enum FlailSpin : byte { Wheel, Overhead, Eight, Sweep, Windup }
        enum FlailThrow : byte { Straight, Arc, Skip, Curve }
        enum FlailArtKind : byte { None, Slam, Storm, MoonWheel, Meteor, Spiral, BloodFan, PetalStorm, BubbleWhirl }

        sealed class FlailStyle
        {
            public string Name;
            public FlailSpin Spin;
            public FlailThrow Throw;
        }

        static FlailStyle FS(FlailSpin s, FlailThrow t) => new FlailStyle { Spin = s, Throw = t, Name = SpinName(s) + "·" + ThrowName(t) };

        static string SpinName(FlailSpin s) => s == FlailSpin.Wheel ? "横轮" : s == FlailSpin.Overhead ? "头顶回旋" : s == FlailSpin.Eight ? "八字" : s == FlailSpin.Sweep ? "地扫" : "蓄势";
        static string ThrowName(FlailThrow t) => t == FlailThrow.Straight ? "直抛" : t == FlailThrow.Arc ? "弧砸" : t == FlailThrow.Skip ? "跳弹" : "回旋抛";

        /// <summary>A flail's styles in turn for the wiki (WeaponWikiLines: "spin·throw"), by its projectile; null for none.</summary>
        internal static string[] WikiFlailStyles(ProjKind k) =>
            flailCombos.TryGetValue(k, out var s) ? System.Array.ConvertAll(s, f => f.Name) : null;

        /// <summary>Each flail's styles in turn, a use each (more of them the later the flail).</summary>
        static readonly Dictionary<ProjKind, FlailStyle[]> flailCombos = new Dictionary<ProjKind, FlailStyle[]>
        {
            [ProjKind.MaceFlail] = new[] { FS(FlailSpin.Wheel, FlailThrow.Straight), FS(FlailSpin.Overhead, FlailThrow.Arc) },
            [ProjKind.FlamingMaceFlail] = new[] { FS(FlailSpin.Overhead, FlailThrow.Arc), FS(FlailSpin.Wheel, FlailThrow.Straight) },
            [ProjKind.BallOHurtFlail] = new[] { FS(FlailSpin.Wheel, FlailThrow.Straight), FS(FlailSpin.Sweep, FlailThrow.Skip), FS(FlailSpin.Overhead, FlailThrow.Arc) },
            [ProjKind.MeatballFlail] = new[] { FS(FlailSpin.Sweep, FlailThrow.Skip), FS(FlailSpin.Wheel, FlailThrow.Straight), FS(FlailSpin.Overhead, FlailThrow.Arc) },
            [ProjKind.BlueMoonFlail] = new[] { FS(FlailSpin.Eight, FlailThrow.Curve), FS(FlailSpin.Wheel, FlailThrow.Straight), FS(FlailSpin.Overhead, FlailThrow.Arc) },
            [ProjKind.SunfuryFlail] = new[] { FS(FlailSpin.Windup, FlailThrow.Straight), FS(FlailSpin.Overhead, FlailThrow.Arc), FS(FlailSpin.Eight, FlailThrow.Curve) },
            [ProjKind.DaoOfPowFlail] = new[] { FS(FlailSpin.Eight, FlailThrow.Curve), FS(FlailSpin.Windup, FlailThrow.Straight), FS(FlailSpin.Sweep, FlailThrow.Skip) },
            [ProjKind.DripplerFlail] = new[] { FS(FlailSpin.Sweep, FlailThrow.Skip), FS(FlailSpin.Overhead, FlailThrow.Arc), FS(FlailSpin.Windup, FlailThrow.Straight) },
            [ProjKind.FlowerPowFlail] = new[] { FS(FlailSpin.Eight, FlailThrow.Curve), FS(FlailSpin.Overhead, FlailThrow.Arc), FS(FlailSpin.Sweep, FlailThrow.Skip), FS(FlailSpin.Windup, FlailThrow.Straight) },
            [ProjKind.FlaironFlail] = new[] { FS(FlailSpin.Windup, FlailThrow.Straight), FS(FlailSpin.Eight, FlailThrow.Curve), FS(FlailSpin.Overhead, FlailThrow.Arc), FS(FlailSpin.Sweep, FlailThrow.Skip) },
        };

        /// <summary>The remade flail's spin radius and chain over the game's.</summary>
        const float FlailReach = 1.3f;

        /// <summary>A flail out with the remade swings: its style or art, where it is in it, what it leaves behind.</summary>
        sealed class FlailRun
        {
            public FlailStyle Style;
            public ItemType Item;
            public float Angle, SpinT, Facing = 1f, Gravity;
            /// <summary>The curved throw's turn a tick and how much of it is left (radians).</summary>
            public float Turn, TurnLeft;
            public int ThrowTicks, Skips;
            public bool Landed;
            /// <summary>A shock going (ticks left): a box round where it struck, its share of the damage.</summary>
            public int Shock;
            public Vector2 ShockAt, ShockSize;
            public float Share = 1f;
            /// <summary>The ball's last places (Terraria px), newest last.</summary>
            public readonly List<Vector2> Trail = new List<Vector2>();
            /// <summary>A right-click art: which, its tick and length, the charge, its aim and where it goes, its blows' share.</summary>
            public FlailArtKind Art;
            public int ArtT, ArtLength;
            public float Charge, BaseAngle, ArtShare = 1f;
            public Vector2 ArtTarget, ArtFrom, ArcTarget;
            public bool Harmless, Fire, Confuse;
        }

        static readonly Dictionary<Proj, FlailRun> flailRuns = new Dictionary<Proj, FlailRun>();
        static readonly List<Proj> flailGone = new List<Proj>();
        static ItemType flailLastItem;
        static int flailLastIndex = -1;
        static float flailLastEnd = -10f;

        /// <summary>Dev: the last style begun, styles begun, shocks, skips, arts; the farthest spin and throw (px from the player) and the game's.</summary>
        public static string DevFlailStyle = "", DevFlailArt = "";
        public static int DevFlailStyles, DevFlailShocks, DevFlailSkips, DevFlailArts;
        public static float DevFlailSpinFar, DevFlailThrowFar, DevFlailVanillaSpin, DevFlailVanillaThrow;
        /// <summary>Dev: every use this style of the flail's (its index), -1 in turn as ever.</summary>
        public static int DevFlailForce = -1;

        /// <summary>The flail's run: made as its use begins with the remade swings on (its style the next in turn), else null.</summary>
        FlailRun FlailRunOf(Proj p, FlailInfo f)
        {
            if (flailRuns.TryGetValue(p, out var r)) return r;
            if (!BladeArts.Enabled || ghostHolder.HasValue || p.Item == ItemType.None || !flailCombos.TryGetValue(p.Kind, out var combo)) return null;
            if (flailRuns.Count > 16)
            {
                flailGone.Clear();
                foreach (var kv in flailRuns) if (kv.Key.Dead) flailGone.Add(kv.Key);
                foreach (var q in flailGone) flailRuns.Remove(q);
            }
            int next;
            if (p.OffHand)
            {
                // The off-hand flail of a pair spins and is thrown as the other.
                var main = FlailPartner(p);
                next = main != null && flailRuns.TryGetValue(main, out var mr) ? System.Array.IndexOf(combo, mr.Style) : 0;
                if (next < 0) next = 0;
            }
            else if (DevFlailForce >= 0) next = DevFlailForce % combo.Length;
            else
            {
                next = p.Item == flailLastItem && flailLastIndex >= 0 && Time.time - flailLastEnd < 1.5f ? (flailLastIndex + 1) % combo.Length : 0;
                flailLastItem = p.Item;
                flailLastIndex = next;
                flailLastEnd = Time.time + 999f;
            }
            r = new FlailRun { Style = combo[next], Item = p.Item };
            flailRuns[p] = r;
            DevFlailStyle = r.Style.Name;
            DevFlailStyles++;
            return r;
        }

        /// <summary>The flail home (or gone): the next use goes on to the next style if it comes soon.</summary>
        static void FlailGone(Proj p)
        {
            if (!flailRuns.TryGetValue(p, out var r)) return;
            flailRuns.Remove(p);
            if (r.Art == FlailArtKind.None && !p.OffHand && r.Item == flailLastItem) flailLastEnd = Time.time;
        }

        // ------------------------------------------------------------------ the spin

        /// <summary>Where the spun ball is this tick, off the player's centre (Terraria px), by the use's spin.</summary>
        Vector2 FlailSpinOffset(Proj p, FlailInfo f, FlailRun r, float dir)
        {
            float R = f.SpinRadius * FlailReach;
            const float w = Mathf.PI / 6f;
            r.SpinT++;
            r.Facing = dir;
            Vector2 o;
            switch (r.Style.Spin)
            {
                case FlailSpin.Overhead:
                    // A lasso over the head: a flat circle seen from the side.
                    r.Angle += w * 0.85f;
                    o = new Vector2(Mathf.Cos(r.Angle * dir) * 1.35f * R, -30f + Mathf.Sin(r.Angle) * 0.3f * R);
                    break;
                case FlailSpin.Eight:
                    // A figure eight lying in front of him.
                    r.Angle += w * 0.7f;
                    o = new Vector2(dir * 0.5f * R + Mathf.Sin(r.Angle) * 1.05f * R * dir, -6f + Mathf.Sin(r.Angle) * Mathf.Cos(r.Angle) * 1.1f * R);
                    break;
                case FlailSpin.Sweep:
                    // Low and wide, round the shins.
                    r.Angle += w * 0.9f;
                    o = new Vector2(Mathf.Cos(r.Angle * dir) * 1.55f * R, 14f + Mathf.Sin(r.Angle) * 0.22f * R);
                    break;
                case FlailSpin.Windup:
                {
                    // The game's wheel, slow at first, faster and wider over most of a second.
                    float g = Mathf.Clamp01(r.SpinT / 50f);
                    r.Angle += w * (0.45f + 0.85f * g);
                    o = WheelOffset(r.Angle, dir) * R * (0.75f + 0.35f * g);
                    break;
                }
                default:
                    r.Angle += w;
                    o = WheelOffset(r.Angle, dir) * R;
                    break;
            }
            DevFlailSpinFar = Mathf.Max(DevFlailSpinFar, o.magnitude);
            DevFlailVanillaSpin = Mathf.Max(DevFlailVanillaSpin, f.SpinRadius * 1.414f);
            return o;
        }

        /// <summary>The game's spin (P:44590-44600): round the player, flatter above and flatter still below.</summary>
        static Vector2 WheelOffset(float angle, float dir)
        {
            var o = RotateBy(new Vector2(dir, dir), angle * dir);
            o.y *= 0.8f;
            if (o.y > 0f) o.y *= 0.5f;
            return o;
        }

        /// <summary>The ground the spin strikes over: an ellipse (its middle, half-width, half-height; Terraria px).</summary>
        static void SpinEllipse(FlailInfo f, FlailRun r, Vector2 owner, out Vector2 centre, out float ax, out float ay)
        {
            float R = f.SpinRadius * FlailReach;
            switch (r.Style.Spin)
            {
                case FlailSpin.Overhead: centre = owner + new Vector2(0f, -30f); ax = 1.35f * R + 20f; ay = 0.3f * R + 26f; break;
                case FlailSpin.Eight: centre = owner + new Vector2(r.Facing * 0.5f * R, -6f); ax = 1.05f * R + 22f; ay = 0.55f * R + 22f; break;
                case FlailSpin.Sweep: centre = owner + new Vector2(0f, 14f); ax = 1.55f * R + 22f; ay = 0.22f * R + 22f; break;
                case FlailSpin.Windup: centre = owner; ax = ay = 1.83f * R * (0.75f + 0.35f * Mathf.Clamp01(r.SpinT / 50f)); break;
                default: centre = owner; ax = ay = 1.83f * R; break;
            }
        }

        // ------------------------------------------------------------------ the throw

        /// <summary>The ball let go: its throw's way and how long it flies (the chain 30% longer).</summary>
        void FlailThrowStart(Proj p, FlailInfo f, FlailRun r, ref Vector2 v, float launch, Vector2 owner, bool mirror)
        {
            float side = v.x < 0f ? -1f : 1f;
            r.Facing = side;
            // launch is the remade one (30% faster): the game's flight time, 30% further.
            r.ThrowTicks = f.Throw;
            DevFlailVanillaThrow = Mathf.Max(DevFlailVanillaThrow, launch / FlailReach * f.Throw);
            switch (r.Style.Throw)
            {
                case FlailThrow.Arc:
                {
                    // Lobbed over to the cursor (as far as the chain goes), to come down on it.
                    var to = AimPx() - owner;
                    if (mirror) to.x = -to.x;
                    float max = launch * f.Throw;
                    if (to.magnitude > max) to = to.normalized * max;
                    if (to.x * side < 40f) to.x = side * 40f;
                    int n = Mathf.Clamp(Mathf.RoundToInt(to.magnitude / Mathf.Max(1f, launch) * 1.6f), 10, 26);
                    r.Gravity = 0.6f;
                    v = new Vector2(to.x / n, to.y / n - 0.5f * r.Gravity * n);
                    r.ThrowTicks = n + 12;
                    r.ArcTarget = owner + to;
                    break;
                }
                case FlailThrow.Skip:
                    // Skimmed low along the ground.
                    v = new Vector2(side * launch, 1.5f);
                    r.Gravity = 0.45f;
                    r.ThrowTicks = Mathf.RoundToInt(f.Throw * 1.5f);
                    break;
                case FlailThrow.Curve:
                {
                    // Out to one side, hooked round to cross the aim again at the cursor (a circle's chord), then on straight.
                    var to = AimPx() - owner;
                    float d = Mathf.Clamp(to.magnitude, 60f, launch * f.Throw);
                    const float off = 0.45f;
                    v = RotateBy(v, -off * side);
                    r.Turn = 2f * Mathf.Sin(off) * Mathf.Max(1f, v.magnitude) / d;
                    r.TurnLeft = 2f * off;
                    break;
                }
            }
            Sound("Item_1", 0.3f);
        }

        /// <summary>A tick of the ball flying out: falling (the lob, the skims), hooking round (the curve).</summary>
        static void FlailThrowTick(Proj p, FlailRun r, ref Vector2 v, ref bool done)
        {
            switch (r.Style.Throw)
            {
                case FlailThrow.Arc:
                    v.y += r.Gravity;
                    if (r.Landed) done = true;
                    break;
                case FlailThrow.Skip:
                    v.y += r.Gravity;
                    break;
                case FlailThrow.Curve:
                    if (r.TurnLeft > 0f)
                    {
                        float turn = Mathf.Min(r.Turn, r.TurnLeft);
                        v = RotateBy(v, turn * r.Facing);
                        r.TurnLeft -= turn;
                    }
                    break;
            }
        }

        /// <summary>The ball meeting the ground as it is thrown: a skim skips off it, a lob smashes into it (true: handled here).</summary>
        static bool FlailRunTile(Proj p, FlailRun r, bool hitX, bool hitY, Vector2 oldVel)
        {
            if (r.Art != FlailArtKind.None) return true;
            if (p.Ai0 != 1f) return false;
            var c = CenterPx(p);
            // oldVel is the world's (y up): falling is below zero.
            if (r.Style.Throw == FlailThrow.Skip && hitY && oldVel.y < 0f && r.Skips < 3)
            {
                r.Skips++;
                p.Vel = new Vector2(oldVel.x * 0.9f, 3.2f + Mathf.Abs(oldVel.y) * 0.35f);
                StartShock(p, r, c + new Vector2(0f, 6f), new Vector2(84f, 46f), 0.8f, true);
                Sound("Dig_" + Random.Range(0, 3), 0.55f);
                DevFlailSkips++;
                return true;
            }
            if (r.Style.Throw == FlailThrow.Arc && hitY && oldVel.y < 0f && !r.Landed)
            {
                // The lob smashes down and is drawn straight back (no stun).
                r.Landed = true;
                StartShock(p, r, c + new Vector2(0f, 4f), new Vector2(150f, 70f), 2f, false);
                Sound("Item_14", 0.35f);
                Player.TerraCamera.Punch(FlailWeight(p.Kind) * 0.09f, 0.22f);
                SurfaceDebris(c, 48f);
                p.Ai0 = 2f;
                p.Ai1 = 0f;
                p.Vel = new Vector2(oldVel.x * 0.2f, 2.5f);
                return true;
            }
            return false;
        }

        /// <summary>A shock where the ball struck: every enemy in the box once more, at its share; its burst.</summary>
        static void StartShock(Proj p, FlailRun r, Vector2 at, Vector2 size, float share, bool small, bool clear = true)
        {
            r.Shock = small ? 3 : 5;
            r.ShockAt = at;
            r.ShockSize = size;
            r.Share = share;
            if (clear) p.Immune.Clear();
            DevFlailShocks++;
            ShockFx(p.Kind, at, size.x, small);
        }

        /// <summary>How heavy a flail looks (its shake), 0.5 the mace .. 1.4 the late ones.</summary>
        static float FlailWeight(ProjKind k) => k == ProjKind.MaceFlail || k == ProjKind.FlamingMaceFlail ? 0.5f : k == ProjKind.BallOHurtFlail || k == ProjKind.MeatballFlail ? 0.75f
            : k == ProjKind.BlueMoonFlail || k == ProjKind.SunfuryFlail ? 1f : 1.3f;

        // ------------------------------------------------------------------ each tick, the hits

        /// <summary>After the game's tick of the flail: the shock running down, the trail, the motes off the ball.</summary>
        void FlailRunTick(Proj p, FlailInfo f, FlailRun r, Vector2 c, Vector2 v, Vector2 owner)
        {
            if (r.Shock > 0 && --r.Shock == 0) r.Share = 1f;
            var at = c + v;
            r.Trail.Add(at);
            if (r.Trail.Count > 10) r.Trail.RemoveAt(0);
            if (p.Ai0 != 0f) DevFlailThrowFar = Mathf.Max(DevFlailThrowFar, Vector2.Distance(owner, at));
            int n = r.Trail.Count;
            float moved = n > 1 ? Vector2.Distance(r.Trail[n - 1], r.Trail[n - 2]) : 0f;
            if (moved > 2.5f)
            {
                var push = (r.Trail[n - 1] - r.Trail[n - 2]) * 0.15f;
                int motes = moved > 12f ? 2 : 1;
                for (int k = 0; k < motes; k++) FlailMote(p.Kind, at + Random.insideUnitCircle * 6f, push, 1f);
            }
        }

        /// <summary>A blow of a remade flail landing: its burst; an art's fire or confusion.</summary>
        void FlailRunHit(Proj p, HealthManager hm)
        {
            if (!flailRuns.TryGetValue(p, out var r)) return;
            var at = r.Shock > 0 ? EnemyCenterPx(hm) : CenterPx(p);
            float side = EnemyCenterPx(hm).x < (Holder(out var owner) ? owner.x : at.x) ? -1f : 1f;
            int n = p.Ai0 == 0f ? 3 : 6;
            for (int k = 0; k < n; k++) FlailMote(p.Kind, at, new Vector2(side * (1f + Random.value * 3f), (Random.value - 0.65f) * 4f), 1.1f);
            if (r.Fire) EnemyDebuffs.Add(hm, EnemyDebuffs.OnFire, 300);
            if (r.Confuse) EnemyDebuffs.Add(hm, EnemyDebuffs.Confused, 120);
        }

        /// <summary>The box the remade flail strikes in: a shock's, an art's ball (bigger), or the spin's ellipse round its middle.</summary>
        static bool RunArea(Proj p, FlailRun r, out (Vector2, Vector2) area)
        {
            area = default;
            if (r.Shock > 0)
            {
                area = (TerraParticles.ToWorld(r.ShockAt), r.ShockSize * Px);
                return true;
            }
            if (r.Art != FlailArtKind.None)
            {
                float size = Mathf.Max(p.Spec.W, p.Spec.H) * p.Spec.Scale * 1.6f;
                area = (p.Pos, new Vector2(size, size) * Px);
                return true;
            }
            if (p.Ai0 == 0f && flails.TryGetValue(p.Kind, out var f) && Holder(out var owner))
            {
                SpinEllipse(f, r, owner, out var c, out float ax, out float ay);
                area = (TerraParticles.ToWorld(c), new Vector2(ax * 2f, ay * 2f) * Px);
                return true;
            }
            return false;
        }

        /// <summary>Whether the remade flail touches an enemy: not in an art's wind-up; the spin within its ellipse; the rest by box.</summary>
        static bool RunTouch(Proj p, FlailRun r, Collider2D col, out bool touch)
        {
            touch = true;
            if (r.Harmless)
            {
                touch = false;
                return true;
            }
            if (r.Shock > 0 || r.Art != FlailArtKind.None) return true;
            if (p.Ai0 != 0f || !flails.TryGetValue(p.Kind, out var f) || !Holder(out var owner)) return false;
            SpinEllipse(f, r, owner, out var c, out float ax, out float ay);
            var d = TerraParticles.ToPx(col.ClosestPoint(TerraParticles.ToWorld(c))) - c;
            if (r.Style.Spin == FlailSpin.Wheel || r.Style.Spin == FlailSpin.Windup)
            {
                d.y /= d.y > 0f ? 0.4f : 0.8f;
                touch = d.magnitude <= ax;
            }
            else touch = (d.x / ax) * (d.x / ax) + (d.y / ay) * (d.y / ay) <= 1f;
            return true;
        }

        // ------------------------------------------------------------------ the look

        /// <summary>The flail's colours (its ribbon's body and light).</summary>
        static void FlailColours(ProjKind k, out Color main, out Color light)
        {
            switch (k)
            {
                case ProjKind.FlamingMaceFlail: main = new Color(1f, 0.42f, 0.1f); light = new Color(1f, 0.85f, 0.4f); break;
                case ProjKind.BallOHurtFlail: main = new Color(0.45f, 0.2f, 0.65f); light = new Color(0.75f, 0.5f, 1f); break;
                case ProjKind.MeatballFlail: main = new Color(0.75f, 0.1f, 0.12f); light = new Color(1f, 0.45f, 0.4f); break;
                case ProjKind.BlueMoonFlail: main = new Color(0.2f, 0.45f, 1f); light = new Color(0.6f, 0.85f, 1f); break;
                case ProjKind.SunfuryFlail: main = new Color(1f, 0.5f, 0.08f); light = new Color(1f, 0.95f, 0.5f); break;
                case ProjKind.DaoOfPowFlail: main = new Color(0.55f, 0.55f, 0.6f); light = new Color(1f, 1f, 1f); break;
                case ProjKind.DripplerFlail: main = new Color(0.6f, 0.05f, 0.08f); light = new Color(1f, 0.3f, 0.3f); break;
                case ProjKind.FlowerPowFlail: main = new Color(0.95f, 0.45f, 0.7f); light = new Color(0.65f, 1f, 0.5f); break;
                case ProjKind.FlaironFlail: main = new Color(0.2f, 0.85f, 0.75f); light = new Color(0.75f, 1f, 0.95f); break;
                default: main = new Color(0.7f, 0.74f, 0.82f); light = new Color(1f, 1f, 1f); break;
            }
        }

        /// <summary>One mote of the flail's own off the ball (Terraria px), carried by its push.</summary>
        static void FlailMote(ProjKind k, Vector2 at, Vector2 push, float scale)
        {
            if (TerraParticles.Instance == null) return;
            var jitter = new Vector2(Random.value - 0.5f, Random.value - 0.5f);
            int d;
            switch (k)
            {
                case ProjKind.FlamingMaceFlail:
                    d = PointDust(at, 6, push * 0.4f + jitter + new Vector2(0f, -1.2f), 100, (1.2f + Random.value * 0.5f) * scale);
                    if (d >= 0) { D(d).NoGravity = true; D(d).FadeIn = 1.1f; }
                    break;
                case ProjKind.BallOHurtFlail:
                    d = PointDust(at, Random.value < 0.7f ? 14 : 27, push * 0.3f + jitter, 100, (1.1f + Random.value * 0.4f) * scale);
                    if (d >= 0) D(d).NoGravity = true;
                    break;
                case ProjKind.MeatballFlail:
                    d = PointDust(at, Random.value < 0.6f ? 115 : 5, push * 0.35f + jitter, 100, (1.2f + Random.value * 0.4f) * scale);
                    break;
                case ProjKind.BlueMoonFlail:
                    d = PointDust(at, Random.value < 0.65f ? 172 : 15, push * 0.3f + jitter * 0.6f, 100, (1.3f + Random.value * 0.4f) * scale);
                    if (d >= 0) D(d).NoGravity = true;
                    break;
                case ProjKind.SunfuryFlail:
                    d = PointDust(at, 6, push * 0.45f + jitter * 1.5f, 100, (1.8f + Random.value * 0.8f) * scale);
                    if (d >= 0) { D(d).NoGravity = true; D(d).FadeIn = 1.4f; }
                    if (Random.value < 0.35f && (d = PointDust(at, 278, push * 0.6f + jitter, 80, 0.6f * scale)) >= 0) { D(d).Color = new Color(1f, 0.85f, 0.3f); D(d).NoGravity = true; }
                    break;
                case ProjKind.DaoOfPowFlail:
                {
                    // Yin and yang: dark smoke and white motes in turn.
                    bool dark = Random.value < 0.5f;
                    d = PointDust(at, dark ? 31 : 278, push * 0.3f + jitter * 0.8f, dark ? 120 : 60, (dark ? 1.1f : 0.7f) * scale);
                    if (d >= 0) { D(d).NoGravity = true; D(d).Color = dark ? new Color(0.12f, 0.12f, 0.15f) : new Color(1f, 1f, 1f); }
                    break;
                }
                case ProjKind.DripplerFlail:
                    d = PointDust(at, 5, push * 0.4f + jitter, 0, (1f + Random.value * 0.5f) * scale);
                    break;
                case ProjKind.FlowerPowFlail:
                    d = PointDust(at, 278, push * 0.25f + jitter * 1.2f + new Vector2(0f, 0.3f), 40, (0.7f + Random.value * 0.3f) * scale);
                    if (d >= 0) D(d).Color = Random.value < 0.6f ? Color.Lerp(new Color(1f, 0.55f, 0.75f), new Color(1f, 0.8f, 0.9f), Random.value) : new Color(0.45f, 0.85f, 0.35f);
                    break;
                case ProjKind.FlaironFlail:
                    d = PointDust(at, Random.value < 0.5f ? 33 : 212, push * 0.3f + jitter * 0.8f, 60, (1f + Random.value * 0.4f) * scale);
                    if (d >= 0) { D(d).NoGravity = true; D(d).Color = Color.Lerp(new Color(0.4f, 0.95f, 0.85f), Color.white, Random.value * 0.4f); }
                    break;
                default:
                    // The mace's: iron sparks, falling.
                    d = PointDust(at, 278, push * 0.5f + jitter * 1.5f + new Vector2(0f, -0.8f), 60, 0.5f * scale);
                    if (d >= 0) D(d).Color = Color.Lerp(new Color(1f, 0.85f, 0.55f), Color.white, Random.value);
                    break;
            }
        }

        /// <summary>A shock's look: the flail's motes and the ground's dust thrown up round where it struck.</summary>
        static void ShockFx(ProjKind k, Vector2 at, float width, bool small)
        {
            int n = small ? 8 : 18;
            for (int i = 0; i < n; i++)
            {
                float side = i % 2 == 0 ? 1f : -1f;
                var push = new Vector2(side * (1.5f + Random.value * 4f), -1.5f - Random.value * 3f);
                FlailMote(k, at + new Vector2((Random.value - 0.5f) * width * 0.5f, 0f), push * 1.4f, small ? 1f : 1.3f);
            }
            for (int i = 0; i < (small ? 4 : 10); i++)
            {
                int d = PointDust(at + new Vector2((Random.value - 0.5f) * width * 0.6f, 6f), 31, new Vector2((Random.value - 0.5f) * 3f, -1f - Random.value * 2f), 120, 1.2f);
                if (d >= 0) D(d).NoGravity = true;
            }
        }

        /// <summary>Under the ball: a ribbon of its colours through its last places, and afterimages behind it when it goes fast.</summary>
        static void DrawFlailRun(FxBatch fx, Proj p, Vector2 px, FlailRun r, bool over)
        {
            int n = r.Trail.Count;
            if (n < 3) return;
            FlailColours(p.Kind, out var main, out var light);
            for (int k = 1; k < n; k++)
            {
                var a = r.Trail[k - 1];
                var b = r.Trail[k];
                if ((b - a).sqrMagnitude < 4f) continue;
                float f = k / (float)(n - 1);
                StreakPx(fx, a, b, Color.Lerp(main, light, f), 3f + 9f * f, 0.5f * f);
            }
            float fast = Vector2.Distance(r.Trail[n - 1], r.Trail[n - 2]);
            if (fast < 5f) return;
            var sprite = Sprite(p.Spec.Texture);
            for (int k = 1; k <= 3 && n - 1 - k >= 0; k++)
            {
                float age = k / 4f;
                var c = Color.Lerp(light, main, age) * (0.35f * (1f - age));
                c.a = 0f;
                fx.Draw(sprite, r.Trail[n - 1 - k], c, p.TRot, p.Scale, flipX: p.Dir < 0f, overPlayer: over);
            }
        }

        /// <summary>
        /// The chain hanging from the hand to the ball as it slackens (out and taut it is nearly straight; coming back or lying
        /// it sags), its links laid along the curve; false while spun (drawn straight as ever).
        /// </summary>
        static bool DrawSaggingChain(FxBatch fx, Proj p, FlailInfo f, Vector2 arm, Vector2 ball, FlailRun r, bool over)
        {
            if (p.Ai0 == 0f && r.Art == FlailArtKind.None) return false;
            float dist = Vector2.Distance(arm, ball);
            if (dist < 6f) return true;
            float full = f.Launch * f.Throw * FlailReach;
            float slack = Mathf.Max(0f, full * 0.85f - dist);
            // Flying out it trails nearly straight; drawn back it hangs; lying stunned or dragged it hangs most.
            float sag = r.Art != FlailArtKind.None ? 3f : p.Ai0 == 1f ? Mathf.Min(18f, 2f + slack * 0.12f)
                : Mathf.Min(70f, 10f + slack * (p.Ai0 == 5f || p.Ai0 == 6f ? 0.45f : 0.35f));
            var control = (arm + ball) * 0.5f + new Vector2(0f, sag * 2f);
            bool flairon = p.Kind == ProjKind.FlaironFlail;
            float step = flairon ? 16f : f.ChainH + f.ChainExtra;
            const int samples = 48;
            var prev = ball;
            float carried = 0f;
            int link = 0;
            for (int i = 1; i <= samples && link < 200; i++)
            {
                float t = i / (float)samples, u = 1f - t;
                var q = u * u * ball + 2f * u * t * control + t * t * arm;
                var seg = q - prev;
                float len = seg.magnitude;
                while (len > 0f && carried + len >= step && link < 200)
                {
                    float need = step - carried;
                    var at = prev + seg / len * need;
                    var d = (arm - at).sqrMagnitude > 1e-4f ? (q - prev).normalized : Vector2.up;
                    float rot = Mathf.Atan2(d.y, d.x) + Mathf.PI / 2f;
                    if (flairon)
                        fx.Draw(FxBatch.Frame("Chain37", 0, 0, 10, 16, 5f, 8f), at, new Color(L, L, L, 1f) * (1f - p.Alpha), rot - Mathf.PI, 1f, overPlayer: over);
                    else
                    {
                        string tex = f.Chain;
                        int frame = f.ChainFrames > 1 ? link % f.ChainFrames : 0;
                        var col = new Color(L, L, L, 1f);
                        if (p.Kind == ProjKind.FlamingMaceFlail)
                        {
                            if (link < 4) { tex = "Chain43"; col = Color.white; }
                            else if (link < 6) { tex = "Chain42"; col = new Color(Mathf.Max(L, 140f / 255f), Mathf.Max(L, 140f / 255f), Mathf.Max(L, 140f / 255f), 1f); }
                        }
                        fx.Draw(FxBatch.Frame(tex, 0, frame * f.ChainH, f.ChainW, f.ChainH, f.ChainW * 0.5f, f.ChainH * 0.5f), at, col, rot, 1f, overPlayer: over);
                    }
                    link++;
                    prev = at;
                    seg = q - prev;
                    len = seg.magnitude;
                    carried = 0f;
                }
                carried += len;
                prev = q;
            }
            return true;
        }

        // ------------------------------------------------------------------ the right-click arts (BladeSkillsFlail)

        static FlailArtKind FlailArtNamed(string name)
        {
            switch (name)
            {
                case "重锤砸地": case "烈焰砸地": case "血肉重砸": return FlailArtKind.Slam;
                case "腐化风暴": return FlailArtKind.Storm;
                case "月轮": return FlailArtKind.MoonWheel;
                case "陨日坠击": return FlailArtKind.Meteor;
                case "太极螺旋": return FlailArtKind.Spiral;
                case "血泉三连": return FlailArtKind.BloodFan;
                case "花瓣风暴": return FlailArtKind.PetalStorm;
                case "泡泡漩涡": return FlailArtKind.BubbleWhirl;
                default: return FlailArtKind.None;
            }
        }

        /// <summary>
        /// A flail's right-click art: its ball (the one out put away first) through the art's path from the player toward the
        /// cursor; charge (0..1) for the charged ones. False when it can't go.
        /// </summary>
        public bool FlailArt(ItemDef def, HeroController hero, string name, float charge)
        {
            if (def == null || hero == null || !Holder(out var owner)) return false;
            var kind = def.Type == ItemType.Flairon ? ProjKind.FlaironFlail : def.Shoot;
            var art = FlailArtNamed(name);
            if (!flails.ContainsKey(kind) || art == FlailArtKind.None) return false;
            foreach (var q in live) if (!q.Dead && (q.Kind == kind || (q.Kind == ProjKind.FlaironHead && def.Type == ItemType.Flairon) || (IsFlail(q.Kind) && q.Item == def.Type))) Kill(q);
            var to = AimPx() - owner;
            if (to.sqrMagnitude < 1f) to = new Vector2(hero.cState.facingRight ? 1f : -1f, 0f);
            float side = to.x < 0f ? -1f : 1f;
            var keep = SpawnClass;
            SpawnClass = DamageClass.Melee;
            var p = Spawn(kind, TerraParticles.ToWorld(owner), Vector2.zero, Equipment.Damage(def), Equipment.Knockback(def), Equipment.Crit(def));
            SpawnClass = keep;
            if (p == null) return false;
            p.Item = def.Type;
            p.Melee = true;
            p.Channel = false;
            p.Ai0 = 7f;
            var r = new FlailRun { Art = art, Item = def.Type, Facing = side, Charge = Mathf.Clamp01(charge), BaseAngle = Mathf.Atan2(to.y, to.x), ArtFrom = owner };
            switch (art)
            {
                case FlailArtKind.Slam: r.ArtLength = 34; r.ArtShare = 1f; break;
                case FlailArtKind.Storm: r.ArtLength = 80; r.ArtShare = 1f; p.Cooldown = 8; break;
                case FlailArtKind.MoonWheel: r.ArtLength = 72; r.ArtShare = 1.3f; p.Cooldown = 10; break;
                case FlailArtKind.Meteor:
                {
                    r.ArtLength = 40;
                    var t = to.magnitude > 320f ? to.normalized * 320f : to;
                    r.ArtTarget = GroundUnder(owner + t, 220f);
                    r.Fire = true;
                    break;
                }
                case FlailArtKind.Spiral: r.ArtLength = 70; r.ArtShare = 1.1f; r.Confuse = true; p.Cooldown = 10; break;
                case FlailArtKind.BloodFan: r.ArtLength = 30; r.ArtShare = 1f; p.Cooldown = 10; break;
                case FlailArtKind.PetalStorm: r.ArtLength = 56; r.ArtShare = 1f; p.Cooldown = 10; break;
                case FlailArtKind.BubbleWhirl: r.ArtLength = 58; r.ArtShare = 1f; p.Cooldown = 10; break;
            }
            if (name == "烈焰砸地") r.Fire = true;
            r.Share = r.ArtShare;
            flailRuns[p] = r;
            DevFlailArts++;
            DevFlailArt = name;
            Sound("Item_1", 0.6f);
            return true;
        }

        /// <summary>The top of the ground under a point (Terraria px), looking down so far; the point itself with none.</summary>
        static Vector2 GroundUnder(Vector2 at, float down)
        {
            // From a little over it, so a point already in the ground finds the ground's top.
            for (float y = -60f; y <= down; y += 4f)
            {
                var q = at + new Vector2(0f, y);
                if (TerraWorld.IsSolidAt(TerraParticles.ToWorld(q))) return q - new Vector2(0f, 10f);
            }
            return at;
        }

        /// <summary>
        /// A whirled ball's place off the player's centre with its lower half pressed flat (as the game's own spin is): it sweeps
        /// just over the ground under his feet, never down through it.
        /// </summary>
        static Vector2 Below(Vector2 o) => o.y > 0f ? new Vector2(o.x, Mathf.Min(o.y * 0.22f, 16f)) : o;

        /// <summary>
        /// Debris off the top of the ground round a point (Terraria px), thrown up: each column's first block with open air over
        /// it within a few blocks of the point, its own dust (the game's CrackGround fills every block of the circle, which
        /// shows inside the ground).
        /// </summary>
        static void SurfaceDebris(Vector2 at, float radius)
        {
            var pp = TerraParticles.Instance;
            if (pp == null) return;
            int i0 = Mathf.FloorToInt((at.x - radius) / 16f), i1 = Mathf.FloorToInt((at.x + radius) / 16f);
            int j0 = Mathf.FloorToInt((at.y - 40f) / 16f), j1 = Mathf.FloorToInt((at.y + 48f) / 16f);
            for (int i = i0; i <= i1; i++)
                for (int j = j0; j <= j1; j++)
                {
                    if (!TerraWorld.IsSolid(i, -j - 1) || TerraWorld.IsSolid(i, -j)) continue;
                    int type = TerraMod.TileDustType(TerraWorld.Get(i, -j - 1));
                    float near = 1f - Mathf.Abs(i * 16f + 8f - at.x) / (radius + 16f);
                    for (int k = 0; k < 2 + (int)(near * 3f); k++)
                    {
                        int d = pp.NewDust(new Vector2(i * 16f, j * 16f - 6f), 16, 4, type);
                        if (d < 0) continue;
                        D(d).Velocity = new Vector2((Random.value - 0.5f) * 2.5f, -1.5f - Random.value * 3.5f * (0.5f + near));
                    }
                    break;
                }
        }

        /// <summary>A point at an angle (degrees from his facing, + up) and a distance from a place (Terraria px).</summary>
        static Vector2 FlailPolar(Vector2 from, float deg, float dist, float side) =>
            from + new Vector2(side * Mathf.Cos(deg * Mathf.Deg2Rad), -Mathf.Sin(deg * Mathf.Deg2Rad)) * dist;

        static float Smooth01(float u) { u = Mathf.Clamp01(u); return u * u * (3f - 2f * u); }

        /// <summary>A tick of a flail's art: its ball where the art has it, its shocks and what it lets go.</summary>
        void StepFlailArt(Proj p, FlailInfo f, ItemDef def, FlailRun r, Vector2 owner)
        {
            var mod = TerraMod.Instance;
            p.TimeLeft = 2;
            p.Collide = false;
            p.Ai0 = 7f;
            p.OwnerCheck = false;
            p.Alpha = Mathf.Max(0f, p.Alpha - 0.2f);
            if (r.Shock > 0 && --r.Shock == 0) r.Share = r.ArtShare;
            int t = r.ArtT++;
            float R = f.SpinRadius * FlailReach, side = r.Facing;
            var shoulder = owner + new Vector2(-side * 2f, -8f);
            var c = CenterPx(p);
            r.Harmless = false;
            switch (r.Art)
            {
                case FlailArtKind.Slam:
                    if (t < 12)
                    {
                        // Raised from behind up over the head.
                        c = FlailPolar(shoulder, Mathf.Lerp(200f, 100f, Smooth01(t / 12f)), 1.5f * R, side);
                        r.Harmless = true;
                    }
                    else if (t < 20)
                    {
                        float u = (t - 12) / 8f;
                        u *= u;
                        c = FlailPolar(shoulder, Mathf.Lerp(100f, -30f, u), Mathf.Lerp(1.5f, 3.2f, u) * R, side);
                    }
                    else
                    {
                        if (t == 20)
                        {
                            var land = GroundUnder(FlailPolar(shoulder, -30f, 3.2f * R, side), 70f);
                            r.ArtTarget = land;
                            StartShock(p, r, land, new Vector2(140f + 40f * r.Charge, 76f), 2f + 1.5f * r.Charge, false);
                            Player.TerraCamera.Punch(0.1f + 0.06f * r.Charge, 0.25f);
                            SurfaceDebris(land, 56f);
                            Sound("Item_14", 0.45f);
                        }
                        c = r.ArtTarget;
                        // The Flaming Mace's fire runs on along the ground; the Meatball's blood bursts up after it.
                        int after = t - 20;
                        if (p.Kind == ProjKind.FlamingMaceFlail && after >= 2 && after <= 12 && after % 2 == 0)
                        {
                            var at = r.ArtTarget + new Vector2(side * 26f * (after / 2), 0f);
                            at = GroundUnder(at, 40f);
                            StartShock(p, r, at + new Vector2(0f, -14f), new Vector2(34f, 64f), 0.8f, true, false);
                            for (int k = 0; k < 10; k++)
                            {
                                int d = PointDust(at + new Vector2((Random.value - 0.5f) * 16f, 4f), 6, new Vector2((Random.value - 0.5f) * 1.5f, -2.5f - Random.value * 3f), 100, 1.6f + Random.value);
                                if (d >= 0) { D(d).NoGravity = true; D(d).FadeIn = 1.3f; }
                            }
                        }
                        if (p.Kind == ProjKind.MeatballFlail && (after == 2 || after == 5 || after == 8))
                        {
                            var at = GroundUnder(r.ArtTarget + new Vector2(side * (18f + 26f * (after / 3)), 0f), 40f);
                            StartShock(p, r, at + new Vector2(0f, -44f), new Vector2(28f, 100f), 0.8f, true, false);
                            for (int k = 0; k < 16; k++)
                                PointDust(at + new Vector2((Random.value - 0.5f) * 10f, 0f), 5, new Vector2((Random.value - 0.5f) * 1.6f, -4f - Random.value * 5f), 0, 1.3f + Random.value * 0.5f);
                        }
                    }
                    break;
                case FlailArtKind.Storm:
                {
                    // Whirled round him fast and wide for over a second.
                    float ramp = Mathf.Min(1f, t / 8f, (r.ArtLength - t) / 8f);
                    r.Angle += side * Mathf.PI * 2f / 22f;
                    c = owner + Below(new Vector2(Mathf.Cos(r.Angle), Mathf.Sin(r.Angle) * 0.8f) * R * 1.9f * Mathf.Max(0.2f, ramp));
                    r.Harmless = t < 3;
                    break;
                }
                case FlailArtKind.MoonWheel:
                {
                    // Flung out into a wide circle round him and drawn back in.
                    float s = t / (float)r.ArtLength;
                    float out_ = Smooth01(t / 16f) * Smooth01((r.ArtLength - t) / 14f);
                    float ang = r.BaseAngle + side * Mathf.PI * 2f * 1.15f * s;
                    c = owner + Below(new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * R * (0.8f + 2.9f * out_));
                    break;
                }
                case FlailArtKind.Meteor:
                {
                    var top = r.ArtTarget + new Vector2(0f, -230f);
                    if (t < 16)
                    {
                        float u = Smooth01(t / 16f);
                        c = Vector2.Lerp(r.ArtFrom, top, u) + new Vector2(0f, -40f * Mathf.Sin(Mathf.PI * u));
                        r.Harmless = true;
                    }
                    else if (t < 26)
                    {
                        float u = (t - 16) / 10f;
                        c = Vector2.Lerp(top, r.ArtTarget, u * u);
                        for (int k = 0; k < 3; k++)
                        {
                            int d = PointDust(c + Random.insideUnitCircle * 10f, 6, new Vector2((Random.value - 0.5f) * 2f, -1.5f - Random.value * 2f), 100, 2.2f + Random.value);
                            if (d >= 0) { D(d).NoGravity = true; D(d).FadeIn = 1.5f; }
                        }
                    }
                    else
                    {
                        c = r.ArtTarget;
                        if (t == 26)
                        {
                            StartShock(p, r, r.ArtTarget, new Vector2(200f + 60f * r.Charge, 130f), 2.5f + 2f * r.Charge, false);
                            Player.TerraCamera.Punch(0.16f + 0.06f * r.Charge, 0.3f);
                            SurfaceDebris(r.ArtTarget, 80f);
                            Sound("Item_14", 0.8f);
                            for (int k = 0; k < 36; k++)
                            {
                                float a = k / 36f * Mathf.PI * 2f;
                                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.6f - 0.3f);
                                int d = PointDust(r.ArtTarget + dir * 8f, 6, dir * (3f + Random.value * 5f), 100, 2f + Random.value * 1.2f);
                                if (d >= 0) { D(d).NoGravity = true; D(d).FadeIn = 1.6f; }
                                if (k % 3 == 0 && (d = PointDust(r.ArtTarget, 31, dir * (1f + Random.value * 2f), 100, 1.8f)) >= 0) D(d).NoGravity = true;
                            }
                        }
                    }
                    break;
                }
                case FlailArtKind.Spiral:
                {
                    // Out and back along a spiral, black and white.
                    float s = t / (float)r.ArtLength;
                    float rad = R * (0.6f + 3.1f * Mathf.Sin(Mathf.PI * s));
                    float ang = r.BaseAngle + side * Mathf.PI * 2f * 2.2f * s;
                    c = owner + Below(new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rad);
                    // The ghost of the other half, opposite (its motes only).
                    var ghost = owner + Below(owner - c);
                    int d = PointDust(ghost, t % 2 == 0 ? 278 : 31, Vector2.zero, 60, 0.9f);
                    if (d >= 0) { D(d).NoGravity = true; D(d).Color = t % 2 == 0 ? Color.white : new Color(0.1f, 0.1f, 0.12f); }
                    break;
                }
                case FlailArtKind.BloodFan:
                    if (t < 20) { float a = r.BaseAngle + side * Mathf.PI * 2f * (t / 20f); c = owner + Below(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 1.6f * R); }
                    if (t == 20)
                    {
                        // Three more balls flung out in a fan.
                        var aimDir = new Vector2(Mathf.Cos(r.BaseAngle), Mathf.Sin(r.BaseAngle));
                        for (int k = -1; k <= 1; k++)
                        {
                            var v = RotateBy(aimDir, k * 16f * Mathf.Deg2Rad) * 13f;
                            MeleeChild(ProjKind.DripplerBall, c, v, p.Damage, p.Knockback, p.Crit);
                        }
                        for (int k = 0; k < 24; k++)
                            PointDust(c, 5, RotateBy(aimDir, (Random.value - 0.5f) * 1.2f) * (2f + Random.value * 5f), 0, 1.3f);
                        Sound("Item_1", 0.6f);
                    }
                    if (t > 20) c = Vector2.MoveTowards(c, owner, 8f);
                    break;
                case FlailArtKind.PetalStorm:
                {
                    // Spun over the head, loosing petals round him in a spiral.
                    c = owner + new Vector2(Mathf.Sin(t * 0.3f) * 6f, -40f);
                    r.Harmless = true;
                    if (t >= 4 && t % 4 == 0)
                    {
                        float a = r.BaseAngle + side * t * 0.42f;
                        var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                        MeleeChild(ProjKind.FlowerPowPetal, c + dir * 12f, dir * 12f, (int)(p.Damage / 1.5f), p.Knockback / 2f, p.Crit);
                    }
                    break;
                }
                case FlailArtKind.BubbleWhirl:
                {
                    r.Angle += side * Mathf.PI * 2f / 24f;
                    var o = new Vector2(Mathf.Cos(r.Angle), Mathf.Sin(r.Angle) * 0.8f);
                    c = owner + Below(o * R * 1.3f);
                    if (t >= 3 && t % 3 == 0)
                    {
                        var keepClass = SpawnClass;
                        SpawnClass = DamageClass.Melee;
                        var bubble = Spawn(ProjKind.FlaironBubble, TerraParticles.ToWorld(c), Flip(o.normalized * 5.5f), p.Damage, p.Knockback * 0.5f, p.Crit);
                        SpawnClass = keepClass;
                        if (bubble != null)
                        {
                            bubble.Ai0 = -10f;
                            bubble.Melee = true;
                        }
                    }
                    break;
                }
            }
            var was = CenterPx(p);
            p.Pos = TerraParticles.ToWorld(c);
            p.Vel = Vector2.zero;
            if (p.Kind == ProjKind.FlaironFlail)
            {
                var back = owner - c;
                p.TRot = Mathf.Atan2(back.y, back.x) - 1.57f;
                p.Dir = back.x < 0f ? 1f : -1f;
            }
            else p.TRot += 0.3f * side;
            mod?.KeepUsing(def, Flip(c - owner));
            r.Trail.Add(c);
            if (r.Trail.Count > 12) r.Trail.RemoveAt(0);
            float moved = Vector2.Distance(was, c);
            if (moved > 2.5f) FlailMote(p.Kind, c + Random.insideUnitCircle * 6f, (c - was) * 0.15f, 1.1f);
            if (r.ArtT >= r.ArtLength) Kill(p);
        }
    }
}
