using System.Collections.Generic;
using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The whips' own lashes (the user's 10-26 "给他们不同的鞭子加入不同的甩鞭动画…更丝滑更帅", then "完全不符合物理", "把墙也做进来",
    /// and "正常人用鞭子是先抬手收鞭，然后用力一抽从右上往左下抽，然后画一个八字顺手又从左上往右下抽，形成一个循环"): with the remade swing
    /// on (BladeArts.Enabled) every whip but the Moon Lord's is swung in a figure eight. A lash is half of it and the next lash
    /// the other half while the swings follow on: the forehand lifts the whip up and back over the shoulder, rolls it over the
    /// top and cracks it straight out at full reach on the aim (Terraria's reach), the follow-through dips and carries up the
    /// far side; the backhand cuts down the other diagonal from high in front to low and near, and loops up past the shoulder
    /// into the next forehand's lift. Slow through the lifts, fast through the strikes. The tip is led round the eight; the
    /// rope between is a rope (its links keep their length, have weight and way, fall, lose a little speed, fold only so far)
    /// drawn toward a curve from the hand to the tip that trails behind the tip's way. It goes through walls, and the ground
    /// under his feet, as Terraria's whips do, standing or in the air, and strikes only what the player can see (Terraria's
    /// sight lines; the user's 10-26 "还是恢复成原来无视墙体的样子吧…改回看不见就打不中", 10-27 "应该站着也能穿墙"): the walls' part
    /// below (Walls) is off. Its length on screen follows how far out the tip is (a
    /// whip turned toward or away from the eye looks shorter), straight at full stretch. With Walls on, what the rope hits in
    /// its strike catches it there, the rest flying on and wrapping round it, the next lash pulling it back in along itself.
    /// Each whip has its own eight (taller,
    /// flatter, tilted) and manner (the eel's swimming, the slime's bounce, the crimson whip's hook, its colours). Drawn
    /// between updates; the arm follows the handle; a ribbon of light after the tip, motes off the links, a small flash and a
    /// burst at the crack.
    /// </summary>
    internal sealed partial class Whips
    {
        /// <summary>One lash: half of the figure eight (the forehand from its start, the backhand from its middle) and the time of
        /// its crack (its strike's fastest moment).</summary>
        sealed class Move
        {
            public string Name;
            public float Start, Crack;
        }

        static readonly Move Forehand = new Move { Name = "正手抽", Start = 0f, Crack = 0.55f };
        static readonly Move Backhand = new Move { Name = "反手抽", Start = 1f, Crack = 0.42f };
        static readonly Move[] eight = { Forehand, Backhand };

        /// <summary>
        /// The figure eight's keys over its two lashes (time in lashes; place in reaches from the shoulder, along the aim and up
        /// from it): the forehand's lift up and back over the shoulder, over the top, its crack straight out on the aim, the
        /// follow-through low and up the far side into the backhand's lift high in front; the backhand down the other diagonal,
        /// across the aim and on to low and near, and round up past the shoulder. Keys close in time are a fast stretch.
        /// </summary>
        static readonly Vector3[] eightKeys =
        {
            new Vector3(0.00f, -0.08f, 0.68f), new Vector3(0.30f, 0.30f, 0.85f), new Vector3(0.47f, 0.85f, 0.40f), new Vector3(0.55f, 1.00f, 0.02f),
            new Vector3(0.68f, 0.88f, -0.10f), new Vector3(0.85f, 0.92f, 0.33f),
            new Vector3(1.00f, 0.84f, 0.52f), new Vector3(1.28f, 0.82f, 0.44f), new Vector3(1.42f, 0.76f, 0.05f), new Vector3(1.52f, 0.58f, -0.08f),
            new Vector3(1.72f, 0.30f, 0.25f), new Vector3(1.90f, 0.05f, 0.62f),
        };

        /// <summary>A whip's own eight (its height, its tilt up in degrees) and manner over it: a wave along it, a hook at the tip,
        /// a bounce in its length, its colours.</summary>
        sealed class Style
        {
            public float Height = 1f, Tilt, Wave, WaveK = 2f, WaveSpeed = 8f, Hook, Bounce;
            public Color Trail;
            public bool Rainbow;
        }

        static readonly Dictionary<WhipKind, Style> styles = new Dictionary<WhipKind, Style>
        {
            [WhipKind.Leather] = new Style { Trail = new Color(1f, 0.88f, 0.7f) },
            [WhipKind.Snapthorn] = new Style { Height = 0.8f, Tilt = -5f, Wave = 6f, WaveK = 2f, WaveSpeed = 1.5f, Trail = new Color(0.45f, 0.9f, 0.3f) },
            [WhipKind.SpinalTap] = new Style { Height = 1.15f, Trail = new Color(0.95f, 0.85f, 0.7f) },
            [WhipKind.Firecracker] = new Style { Tilt = 4f, Trail = new Color(1f, 0.5f, 0.15f) },
            [WhipKind.CoolWhip] = new Style { Height = 1.2f, Trail = new Color(0.6f, 0.9f, 1f) },
            [WhipKind.Durendal] = new Style { Height = 1.1f, Tilt = 5f, Trail = new Color(1f, 0.85f, 0.4f) },
            [WhipKind.MorningStar] = new Style { Height = 1.2f, Tilt = -3f, Trail = new Color(0.55f, 0.65f, 1f) },
            [WhipKind.DarkHarvest] = new Style { Tilt = -5f, Trail = new Color(0.6f, 0.3f, 0.7f) },
            [WhipKind.Kaleidoscope] = new Style { Height = 1.1f, Wave = 10f, WaveK = 2f, WaveSpeed = 1.5f, Trail = Color.white, Rainbow = true },
            [WhipKind.SlimeWhip] = new Style { Bounce = 0.06f, Trail = new Color(0.4f, 0.65f, 1f) },
            [WhipKind.CobWhip] = new Style { Height = 0.85f, Trail = new Color(0.9f, 0.9f, 0.82f) },
            [WhipKind.CorruptWhip] = new Style { Wave = 10f, WaveK = 2f, WaveSpeed = 2f, Trail = new Color(0.6f, 0.3f, 0.7f) },
            [WhipKind.CrimsonWhip] = new Style { Hook = 45f, Trail = new Color(1f, 0.2f, 0.2f) },
            [WhipKind.MeteorWhip] = new Style { Height = 1.25f, Tilt = 4f, Trail = new Color(1f, 0.55f, 0.2f) },
            [WhipKind.FlowerWhip] = new Style { Wave = 10f, WaveK = 1.5f, WaveSpeed = 1.5f, Trail = new Color(1f, 0.65f, 0.85f) },
            [WhipKind.EelWhip] = new Style { Height = 0.9f, Wave = 12f, WaveK = 2f, WaveSpeed = 2f, Trail = new Color(0.35f, 0.75f, 1f) },
            [WhipKind.ConstellationWhip] = new Style { Height = 1.1f, Tilt = 5f, Trail = new Color(0.5f, 0.7f, 1f) },
        };

        /// <summary>For the wiki (WeaponWikiLines): a whip lashed its own way with the remade weapons.</summary>
        internal static bool WikiStyled(ItemDef d) => d != null && d.IsWhip && d.Whip != WhipKind.MoonLordWhip && styles.ContainsKey(d.Whip);

        /// <summary>The whip lashes its own way: the remade swing on, a style for it, not one of the Moon Lord whip's.</summary>
        static bool Styled(Crack c) => WeaponRefitting.EnabledFor(c.Item) && c.Kind != WhipKind.MoonLordWhip && styles.ContainsKey(c.Kind);

        static Move MoveOf(Crack c) => eight[Mathf.Clamp(c.Move, 0, eight.Length - 1)];

        // ------------------------------------------------------------------ the lashes in turn

        static WhipKind lastKind;
        static int lastMove = -1;
        static float lastLashAt = -10f;

        /// <summary>The next lash of the whip's: the other half of the eight while the swings follow on (within a second and a bit),
        /// else the forehand from rest.</summary>
        static int NextMove(WhipKind kind)
        {
            if (!styles.ContainsKey(kind)) return 0;
            int next = kind == lastKind && Time.time - lastLashAt < 1.2f ? (lastMove + 1) % eight.Length : 0;
            lastKind = kind;
            lastMove = next;
            lastLashAt = Time.time;
            return next;
        }

        /// <summary>Dev: the reach (Terraria px) a whip's tip has at its crack, Terraria's and its own lash's alike; the last lash's name.</summary>
        public static float DevReachPx(ItemDef whip) => whip != null && settings.TryGetValue(whip.Whip, out var s)
            ? 2f * whip.ShootSpeed * Mathf.Round(whip.UseAnimation * 60f) * (1f + Equipment.Current.WhipRange) * s.RangeMult : 0f;
        public static string DevLastMove = "";

        /// <summary>Dev: links set back onto the ground; the lowest link below the feet's line (px).</summary>
        public static int DevClamped;
        public static float DevLowest;
        /// <summary>Dev: links drawn inside the ground or a wall; links drawn through one (in it from a quarter to three quarters along: not a corner cut).</summary>
        public static int DevInside, DevThreaded;
        public static string DevInsideWhy = "";

        /// <summary>Dev: the whips' swings drawn their own way, by whip.</summary>
        public static readonly HashSet<WhipKind> DevStyled = new HashSet<WhipKind>();

        // ------------------------------------------------------------------ the figure eight

        /// <summary>The eight's place at a time (lashes, wrapping every two), in reaches along the aim and up: a cubic through the
        /// keys, each key's tangent from its neighbours over the time between them.</summary>
        static Vector2 EightAt(float tau)
        {
            int n = eightKeys.Length;
            tau = Mathf.Repeat(tau, 2f);
            int k = 0;
            for (int i = 0; i < n; i++) if (eightKeys[i].x <= tau) k = i;
            var p0 = EightKey(k - 1);
            var a = EightKey(k);
            var b = EightKey(k + 1);
            var p3 = EightKey(k + 2);
            float h = b.x - a.x, u = (tau - a.x) / h;
            var m0 = new Vector2(b.y - p0.y, b.z - p0.z) / (b.x - p0.x);
            var m1 = new Vector2(p3.y - a.y, p3.z - a.z) / (p3.x - a.x);
            float u2 = u * u, u3 = u2 * u;
            return new Vector2(a.y, a.z) * (2f * u3 - 3f * u2 + 1f) + m0 * (h * (u3 - 2f * u2 + u)) + new Vector2(b.y, b.z) * (3f * u2 - 2f * u3) + m1 * (h * (u3 - u2));
        }

        static Vector3 EightKey(int i)
        {
            int n = eightKeys.Length, j = ((i % n) + n) % n;
            var v = eightKeys[j];
            v.x += 2f * Mathf.Floor((float)i / n);
            return v;
        }

        /// <summary>1 at the crack, easing to 0 a width away (a bell: no sudden pull starting or stopping).</summary>
        static float AtCrack(Move m, float t, float width)
        {
            float x = Mathf.Clamp01(Mathf.Abs(t - m.Crack) / width);
            float c = Mathf.Cos(x * Mathf.PI / 2f);
            return c * c;
        }

        static void Frame(Crack c, out Vector2 along, out Vector2 over)
        {
            along = c.Vel.sqrMagnitude > 1e-6f ? c.Vel.normalized : new Vector2(c.SpriteDir, 0f);
            over = Rotate(along, -c.SpriteDir * Mathf.PI / 2f);
        }

        Vector2 Shoulder => pCenter + new Vector2(0f, -4f);

        /// <summary>Where the tip is led a time into the lash: on the whip's own eight (its height, its tilt), at most its reach
        /// from the shoulder; with the walls on, never asked under the ground below his feet's line (pulled in and pushed out,
        /// it shook). Through walls the ground under his feet is no more in its way than any other (the user's 10-27 "鞭子站在
        /// 地上的时候还是会打到地上…应该站着也能穿墙").</summary>
        Vector2 TipGoal(Crack c, Style st, Move m, float t, Vector2 along, Vector2 over)
        {
            var e = EightAt(m.Start + t);
            e.y *= st.Height;
            if (st.Tilt != 0f) e = Rotate(e, st.Tilt * Mathf.Deg2Rad);
            if (e.sqrMagnitude > 0.99f) e *= 0.995f / e.magnitude;
            var q = Shoulder + (along * e.x + over * e.y) * c.Reach;
            return Walls || DevFloor ? OffFloor(q) : q;
        }

        /// <summary>Dev (suite whipfloor): the ground under his feet holds the rope up through walls, as before.</summary>
        internal static bool DevFloor;

        Vector2 OffFloor(Vector2 q)
        {
            if (q.y > pFeetY - 2f)
                for (float y = pFeetY - 8f; y < q.y + 2f && y < pFeetY + 48f; y += 4f)
                    if (Solid(new Vector2(q.x, y))) return new Vector2(q.x, Mathf.Min(q.y, y - 2f));
            return q;
        }

        /// <summary>The hand round its own small eight, a little ahead of the tip: up as the whip is lifted, down as it strikes.</summary>
        Vector2 Hand(Move m, float t, Vector2 along, Vector2 over)
        {
            var e = EightAt(m.Start + t + HandLead);
            return Shoulder + along * (2f + e.x * HandReach) + over * (e.y * HandReach);
        }

        static readonly Vector2[] target = new Vector2[64];
        static readonly Vector2[] curvePts = new Vector2[65];
        static readonly float[] curveLen = new float[65];

        /// <summary>
        /// The rope's shape: n + 1 points evenly along a curve from the hand to the tip as long as the rope, leaving the hand along
        /// hdir and coming into the tip from trail (behind its way; from below while it hangs). How far the curve bows is found by
        /// halving, so its length is the rope's: straight when the tip is out at full stretch, a trailing loop when it is nearer.
        /// </summary>
        static void TrailCurve(Vector2 hand, Vector2 tip, Vector2 hdir, Vector2 trail, float length, int n)
        {
            float lo = 0f, hi = length * 1.5f;
            for (int k = 0; k < 22; k++)
            {
                float mid = (lo + hi) * 0.5f;
                if (CurveLength(hand, tip, hdir, trail, mid) < length) lo = mid;
                else hi = mid;
            }
            CurveLength(hand, tip, hdir, trail, lo);
            int j = 1;
            for (int i = 0; i <= n; i++)
            {
                float at = curveLen[64] * i / n;
                while (j < 64 && curveLen[j] < at) j++;
                float seg = curveLen[j] - curveLen[j - 1];
                target[i] = Vector2.Lerp(curvePts[j - 1], curvePts[j], seg > 1e-6f ? (at - curveLen[j - 1]) / seg : 0f);
            }
        }

        static float CurveLength(Vector2 p0, Vector2 p3, Vector2 hdir, Vector2 trail, float bow)
        {
            var p1 = p0 + hdir * (bow * 0.6f);
            var p2 = p3 + trail * bow;
            curvePts[0] = p0;
            curveLen[0] = 0f;
            for (int j = 1; j <= 64; j++)
            {
                float u = j / 64f, v = 1f - u;
                var q = p0 * (v * v * v) + p1 * (3f * v * v * u) + p2 * (3f * v * u * u) + p3 * (u * u * u);
                curveLen[j] = curveLen[j - 1] + (q - curvePts[j - 1]).magnitude;
                curvePts[j] = q;
            }
            return curveLen[64];
        }

        /// <summary>The whip's manner on its shape: a wave along it (calmer round the crack), a hook curling its last third.</summary>
        static void Manner(Style st, Move m, float t, int n, float length)
        {
            if (st.Wave == 0f && st.Hook == 0f) return;
            float calm = 1f - 0.8f * AtCrack(m, t, 0.2f), loose = 1f - AtCrack(m, t, 0.2f);
            for (int i = 1; i < n; i++)
            {
                float f = (float)i / n;
                var d = target[i + 1] - target[i - 1];
                if (d.sqrMagnitude < 1e-6f) continue;
                d.Normalize();
                var side = new Vector2(-d.y, d.x);
                float off = 0f;
                if (st.Wave != 0f) off += length * st.Wave * Mathf.Deg2Rad * 0.35f * calm * f * Mathf.Sin(Mathf.PI * (st.WaveK * f - st.WaveSpeed * t));
                if (st.Hook != 0f) { float h = Mathf.Clamp01((f - 0.7f) / 0.3f); off += length * 0.12f * st.Hook / 45f * h * h * loose; }
                target[i] += side * off;
            }
        }

        // ------------------------------------------------------------------ the rope

        /// <summary>A tick's fall (px), the speed kept a tick, the pull toward the shape a tick, how much of a sharp fold (past
        /// FoldCos between two links) is eased and how much every bend is smoothed an iteration, the speed kept sliding on ground,
        /// the rope's length on screen over the straight to the tip, the hand's eight (px) and how far ahead of the tip it runs
        /// (lashes), a link's length (px), how much of its lead a link takes back a step once clear; the relaxations a step.</summary>
        const float Gravity = 0.35f, Damping = 0.985f, Follow = 0.5f, Unfold = 0.6f, Smooth = 0.2f, Friction = 0.9f, Slack = 0.12f, HandReach = 14f, HandLead = 0.1f,
            LinkPx = 8f, Regain = 0.12f;

        /// <summary>The rope stopped by walls, caught and wrapping on them, striking what it touches; off: through them, striking
        /// what the player sees (the user's 10-26 "恢复成原来无视墙体的样子").</summary>
        const bool Walls = false;
        static readonly float FoldCos = Mathf.Cos(35f * Mathf.Deg2Rad);
        const int Iterations = 14;

        void SimStart(Crack c, Style st, Move m)
        {
            Frame(c, out var along, out var over);
            float baseAnim = Mathf.Round((c.ReachItem ?? c.Item).UseAnimation * 60f);
            c.Reach = 2f * c.Vel.magnitude * baseAnim * (1f + Equipment.Current.WhipRange) * c.S.RangeMult;
            int n = PhysLinks(c);
            c.Lead = new float[n + 1];
            for (int i = 0; i <= n; i++) c.Lead[i] = 1f;
            c.Caught = n + 1;
            c.P = new Vector2[n + 1];
            c.Prev = new Vector2[n + 1];
            c.Before = new Vector2[n + 1];
            c.TickStart = new Vector2[n + 1];
            var hand = Hand(m, 0f, along, over);
            // Straight on from the last lash of this whip (within a moment of its end): its rope as it was, moving as it was (the
            // user's 10-26 "所有切换都应该是丝滑的"); the eight goes on where it was.
            var kept = rope;
            if (!c.Clone && c.ReachItem == null && kept != null && kept.Kind == c.Kind && kept.P.Length == n + 1 && Time.time - kept.At < 0.35f)
            {
                var shift = hand - kept.P[0];
                for (int i = 0; i <= n; i++)
                {
                    c.P[i] = kept.P[i] + shift * (1f - (float)i / n);
                    c.Prev[i] = kept.Prev[i] + shift * (1f - (float)i / n);
                }
                for (int i = 0; i <= n; i++) c.Before[i] = c.TickStart[i] = c.P[i];
                System.Array.Copy(kept.Lead, c.Lead, n + 1);
                // Caught on something in the last one's strike: this one pulls it back; after a pull back, lifted from rest.
                c.Reel = kept.Snagged;
                c.ReelFrom = kept.Held;
                c.Held = kept.Held;
                if (kept.Reeled)
                {
                    c.FromRest = true;
                    c.RestTip = c.P[n];
                }
                rope = null;
                DevLastMove = c.Reel ? "收鞭" : m.Name;
                if (c.Reel) DevReels++;
                DevCarried++;
                return;
            }
            // From rest: hanging from the hand a little before him; the lash lifts it from there onto the eight.
            c.FromRest = true;
            var rest = OffFloor(hand + along * Mathf.Min(c.Reach * 0.25f, 60f) + new Vector2(0f, 30f));
            c.P[0] = hand;
            for (int i = 1; i <= n; i++) c.P[i] = Reach(c.P[i - 1], Vector2.Lerp(hand, rest, (float)i / n));
            c.RestTip = c.P[n];
            for (int i = 0; i <= n; i++) c.Prev[i] = c.Before[i] = c.TickStart[i] = c.P[i];
            DevLastMove = m.Name;
        }

        /// <summary>The last lash's rope as it ended, for the next to carry on from.</summary>
        sealed class KeptRope
        {
            public WhipKind Kind;
            public Vector2[] P, Prev;
            public float[] Lead;
            public float At, Held;
            /// <summary>It was caught on something in its strike; it was a pull back.</summary>
            public bool Snagged, Reeled;
        }

        static KeptRope rope;

        /// <summary>Dev: lashes carried on from the last one's rope; lashes that pulled a caught rope back; strikes caught on
        /// something.</summary>
        public static int DevCarried, DevReels, DevSnags;

        /// <summary>Dev: frames drawn, links drawn, sharp folds, links behind his back.</summary>
        public static int DevFrames, DevLinks, DevKinks, DevBehind;

        /// <summary>A lash of the held whip ending: its rope kept a moment for the next.</summary>
        void KeepRope(Crack c)
        {
            if (c.P == null || c.Clone || c.ReachItem != null || !Styled(c)) return;
            rope = new KeptRope
            {
                Kind = c.Kind, P = (Vector2[])c.P.Clone(), Prev = (Vector2[])c.Prev.Clone(), Lead = (float[])c.Lead.Clone(), At = Time.time,
                Held = c.Held, Snagged = c.LetGo, Reeled = c.Reel,
            };
        }

        /// <summary>
        /// One update of the rope: the tip led along the eight (short of a wall in its way), the hand round its own; the links on
        /// with their speed and fall, drawn toward the trailing shape as far as each is led, kept their length, sharp folds eased
        /// and bends smoothed, set back out of what they went into with their speed along it kept; the handle's link along the
        /// handle. Caught on something in the strike: let go from there out (it flies on and wraps), the hand's side a line to
        /// it; a pull back draws the rope in along itself. A link is led again only with a clear way to its place.
        /// </summary>
        void SimStep(Crack c)
        {
            var st = styles[c.Kind];
            var m = MoveOf(c);
            if (c.P == null) SimStart(c, st, m);
            int n = PhysLinks(c), u = c.S.Updates;
            float t = Mathf.Clamp01(c.Ai0 / c.T);
            Frame(c, out var along, out var over);
            var hand = Hand(m, t, along, over);
            var goal = TipGoal(c, st, m, t, along, over);
            var ahead = TipGoal(c, st, m, t + 0.02f, along, over);
            if (c.FromRest)
            {
                // A lash from rest: its lift from where the whip lay up onto the eight.
                float e = Mathf.Clamp01(t / 0.45f);
                e = e * e * (3f - 2f * e);
                goal = Vector2.Lerp(c.RestTip, goal, e);
                ahead = Vector2.Lerp(c.RestTip, ahead, e);
            }
            // Trailing behind the tip's way (the faster, the more), else hanging; the handle toward a little ahead of the tip.
            var vel = ahead - goal;
            float sp = vel.magnitude, mix = Mathf.Min(1f, sp / Mathf.Max(1f, c.Reach * 0.05f));
            var trail = (sp > 1e-4f ? -vel / sp : Vector2.zero) * mix + new Vector2(0f, 0.6f * (1f - mix));
            trail = trail.sqrMagnitude > 1e-6f ? trail.normalized : Vector2.up;
            var hdir = goal + vel - hand;
            hdir = hdir.sqrMagnitude > 1e-6f ? hdir.normalized : along;
            float dist = (goal - hand).magnitude;
            float length = Mathf.Max(dist, Mathf.Clamp(dist * (1f + Slack), c.Reach * 0.3f, c.Reach));
            if (st.Bounce > 0f) length *= 1f + st.Bounce * Mathf.Sin(t * 8f);
            var P = c.P;
            var prev = c.Prev;
            if (c.Reel)
            {
                // Pulled back: shorter by a third of its length over the first third of the lash, drawn in along itself.
                float e = Mathf.Clamp01(t / 0.35f);
                e = e * e * (3f - 2f * e);
                float was = c.ReelFrom * (1f - 0.7f * c.ReelDone);
                length = c.ReelFrom * (1f - 0.7f * e);
                c.ReelDone = e;
                if (was > length) ReelIn(P, prev, n, was - length);
            }
            else if (c.LetGo) length = Mathf.Max(length, c.Held);
            c.Held = length;
            float link = length / n;
            TrailCurve(hand, goal, hdir, trail, length, n);
            Manner(st, m, t, n, length);
            if (c.LetGo && c.Caught < n)
            {
                // Caught: from the hand to where it is caught a line, hanging a little.
                var cp = P[c.Caught];
                var to = cp - hand;
                TrailCurve(hand, cp, to.sqrMagnitude > 1e-6f ? to.normalized : along, Vector2.up, Mathf.Max(to.magnitude * 1.02f, c.Caught * link), c.Caught);
            }
            if (Walls || DevFloor) for (int i = 1; i < n; i++) target[i] = OffFloor(target[i]);
            c.HandleDeg = Mathf.Atan2(Vector2.Dot(hdir, over), Vector2.Dot(hdir, along)) * Mathf.Rad2Deg;

            float g = Gravity / (u * u), keep = Mathf.Pow(Damping, 1f / u), k = 1f - Mathf.Pow(1f - Follow, 1f / u);
            System.Array.Copy(P, c.Before, n + 1);
            var lead = c.Lead;
            for (int i = 1; i <= n; i++)
            {
                if (i == n && lead[n] > 0.999f) continue;
                var x = P[i];
                var v = (x - prev[i]) * keep;
                prev[i] = x;
                var next = x + v + new Vector2(0f, g);
                P[i] = next + ((i == n ? goal : target[i]) - next) * (k * lead[i]);
            }
            P[0] = prev[0] = hand;
            bool tipLed = lead[n] > 0.999f;
            if (tipLed)
            {
                prev[n] = P[n];
                P[n] = Walls ? Reach(P[n], goal) : goal;
            }
            // The handle's link along the handle, short of the ground or a wall it would go into.
            var handleEnd = Walls ? Reach(hand, hand + hdir * link) : hand + hdir * link;
            int touched = n + 1;
            for (int it = 0; it < Iterations; it++)
            {
                P[1] = handleEnd;
                for (int i = 2; i <= n; i++)
                {
                    var d = P[i] - P[i - 1];
                    float len = d.magnitude;
                    if (len < 1e-4f) continue;
                    var fix = d * ((len - link) / len);
                    if (i == 2) P[i] -= fix;
                    else if (i == n && tipLed) P[i - 1] += fix;
                    else
                    {
                        P[i - 1] += fix * 0.5f;
                        P[i] -= fix * 0.5f;
                    }
                }
                // Sharp folds eased, every bend smoothed a little (more while pulled in): a rope's curve, no ripples.
                float smooth = c.Reel ? 0.5f : Smooth;
                for (int i = 2; i < n; i++)
                {
                    var u1 = P[i] - P[i - 1];
                    var u2 = P[i + 1] - P[i];
                    float l1 = u1.magnitude, l2 = u2.magnitude;
                    bool sharp = l1 > 1e-4f && l2 > 1e-4f && Vector2.Dot(u1, u2) < FoldCos * l1 * l2;
                    P[i] += ((P[i - 1] + P[i + 1]) * 0.5f - P[i]) * (sharp ? Unfold : smooth);
                }
                if (it == Iterations / 2 || it == Iterations - 1)
                {
                    if (Walls)
                    {
                        touched = Mathf.Min(touched, Ground(c, P, prev, n));
                        touched = Mathf.Min(touched, Unthread(c, P, prev, n));
                    }
                    else if (DevFloor) Floor(P, prev, n);
                }
            }
            // Once more: a stretch put back can leave the one before it cutting a corner.
            if (Walls) touched = Mathf.Min(touched, Unthread(c, P, prev, n));

            // Caught in the strike (before its last fifth): let go from the first link caught on out for the rest of the lash.
            if (!c.Reel && touched <= n && t < 0.8f && !c.LetGo)
            {
                c.LetGo = true;
                c.Caught = touched;
                DevSnags++;
            }
            else if (c.LetGo && touched < c.Caught && t < 0.8f) c.Caught = touched;
            for (int i = 1; i <= n; i++)
            {
                if (c.Reel || c.LetGo && i >= c.Caught) lead[i] = 0f;
                else if (c.LetGo) lead[i] = 1f;
                else if (lead[i] < 1f)
                {
                    // Led again only with a clear way to its place (one behind a wall is drawn round by its neighbours).
                    var to = i == n ? goal : target[i];
                    lead[i] = Walls && (Solid(to) || Blocked(P[i], to)) ? Mathf.Max(0f, lead[i] - 0.25f) : Mathf.Min(1f, lead[i] + Regain);
                }
            }
        }

        /// <summary>Dev (DevFloor), through walls as before: the ground under his feet held the rope up (a link under his feet's
        /// line set back onto it, its fall stopped, sliding on).</summary>
        void Floor(Vector2[] P, Vector2[] prev, int n)
        {
            for (int i = 2; i <= n; i++)
            {
                if (P[i].y <= pFeetY - 2f) continue;
                var q = OffFloor(P[i]);
                if (q.y >= P[i].y - 0.01f) continue;
                prev[i] = new Vector2(q.x - (P[i].x - prev[i].x) * Friction, q.y);
                P[i] = q;
                DevClamped++;
            }
        }

        /// <summary>The rope drawn in d px along its own line: each link moved to where the line was d nearer the hand (round the
        /// corners it went), its speed kept; links reaching the hand gather there.</summary>
        static void ReelIn(Vector2[] P, Vector2[] prev, int n, float d)
        {
            arc[0] = 0f;
            for (int i = 1; i <= n; i++)
            {
                arc[i] = arc[i - 1] + (P[i] - P[i - 1]).magnitude;
                old[i] = P[i];
            }
            old[0] = P[0];
            int j = 1;
            for (int i = 1; i <= n; i++)
            {
                float at = Mathf.Max(0f, arc[i] - d);
                while (j < n && arc[j] < at) j++;
                while (j > 1 && arc[j - 1] > at) j--;
                float seg = arc[j] - arc[j - 1];
                var q = Vector2.Lerp(old[j - 1], old[j], seg > 1e-6f ? (at - arc[j - 1]) / seg : 0f);
                prev[i] += q - P[i];
                P[i] = q;
            }
        }

        static readonly Vector2[] old = new Vector2[64];

        /// <summary>
        /// From the hand out, a link whose stretch to the one before it cuts through the ground or a wall (round a corner it is
        /// wrapped over, the two ends either side of it) is moved the least way up, down or to a side that clears it, hugging the
        /// corner (its speed kept, never across to the far side of what it was beside); failing that, it and the one before moved
        /// together the least way that clears them (a stretch over a wall's top that sank into it, lifted back onto it); failing
        /// that, both back where they were as the step began. The lash always runs through open air from the hand to the tip: it drapes over a
        /// wall, wraps round a block or piles against it, never through. The first link caught so (above his feet's line), or
        /// n + 1.
        /// </summary>
        int Unthread(Crack c, Vector2[] P, Vector2[] prev, int n)
        {
            int touched = n + 1;
            for (int i = 2; i <= n; i++)
            {
                var a = P[i - 1];
                var b = P[i];
                if (!Blocked(a, b)) continue;
                bool done = false;
                for (int k = 1; k <= 12 && !done; k++)
                    foreach (var dir in sides)
                    {
                        var cand = b + dir * (4f * k);
                        // Never to the far side of something from where it was as the step began (a thin post).
                        if (Solid(cand) || Blocked(a, cand) || Blocked(c.Before[i], cand)) continue;
                        prev[i] += cand - b;
                        P[i] = cand;
                        done = true;
                        break;
                    }
                // Both ends moved together (a stretch over a wall's top sunk into it: lifted back onto the top).
                for (int k = 1; k <= 12 && !done && i > 2; k++)
                    foreach (var dir in sides)
                    {
                        var off = dir * (4f * k);
                        var a2 = a + off;
                        var b2 = b + off;
                        if (Solid(a2) || Solid(b2) || Blocked(a2, b2) || Blocked(P[i - 2], a2) || Blocked(c.Before[i - 1], a2) || Blocked(c.Before[i], b2)) continue;
                        prev[i - 1] += off;
                        prev[i] += off;
                        P[i - 1] = a2;
                        P[i] = b2;
                        done = true;
                        break;
                    }
                if (!done)
                {
                    // Nowhere clear: both ends back where they were as the step began (the stretch was clear then).
                    for (int j = i - 1; j <= i; j++)
                    {
                        if (j < 2) continue;
                        prev[j] += c.Before[j] - P[j];
                        P[j] = c.Before[j];
                    }
                }
                if ((a.y + b.y) * 0.5f < pFeetY - 6f) touched = Mathf.Min(touched, i);
            }
            return touched;
        }

        /// <summary>The stretch from a to b goes through the ground or a wall: one of Hollow Knight's terrain colliders across it, or
        /// a block of the grid at any point of it between the ends (looked at every 4 px; a long link clipping a corner between
        /// three looks went by unseen).</summary>
        static bool Blocked(Vector2 a, Vector2 b)
        {
            if (Meets(a, b, out _, out _)) return true;
            int steps = Mathf.Max(2, Mathf.CeilToInt((b - a).magnitude / 4f));
            for (int k = 1; k < steps; k++)
                if (GridSolid(Vector2.Lerp(a, b, (float)k / steps))) return true;
            return false;
        }

        /// <summary>
        /// Links that went into the ground, a wall or a roof: back along the way they came this step (from where they were as it
        /// began) to the last free place short of it, then on along it the way that is open (along a floor, up or down a wall),
        /// their speed into it stopped and the rest slowed by friction; a fast tip can't pass a thin wall between two steps. One
        /// that began inside (a lash begun against a wall) out the nearest way. The first link caught so on something other than
        /// the floor under his feet, or n + 1.
        /// </summary>
        int Ground(Crack c, Vector2[] P, Vector2[] prev, int n)
        {
            int touched = n + 1;
            for (int i = 2; i <= n; i++)
            {
                var q = P[i];
                var from = c.Before[i];
                if (Solid(from))
                {
                    if (Solid(q) && Escape(q, P[i - 1], out var free)) { P[i] = free; prev[i] = free; DevClamped++; }
                    if (q.y < pFeetY - 6f) touched = Mathf.Min(touched, i);
                    continue;
                }
                // Hollow Knight's own terrain first (a hollow edge among it, nothing inside to find): the way stops short of where
                // it meets it; then the grid's blocks along what is left of the way.
                bool met = Meets(from, q, out var face, out var normal);
                var end = met ? Short(from, face) : q;
                var d = end - from;
                int steps = Mathf.Max(1, Mathf.CeilToInt(d.magnitude / 4f));
                var last = from;
                bool hit = false;
                for (int k = 1; k <= steps; k++)
                {
                    var at = from + d * ((float)k / steps);
                    if (GridSolid(at)) { hit = true; break; }
                    last = at;
                }
                if (!hit && met)
                {
                    last = end;
                    hit = true;
                }
                else met = false;
                if (!hit)
                {
                    // Under his feet's line over what looked open (a floor's cells may be only its crust): onto the first ground
                    // between that line and it. Over a pit, with no ground there, it goes on down.
                    if (q.y > pFeetY)
                        for (float y = pFeetY - 8f; y < q.y; y += 4f)
                            if (Solid(new Vector2(q.x, y)))
                            {
                                P[i] = new Vector2(q.x, y - 2f);
                                prev[i] = new Vector2(q.x - (q.x - prev[i].x) * Friction, y - 2f);
                                DevClamped++;
                                break;
                            }
                    continue;
                }
                // Which way is shut: down or up (a floor, a roof), or across (a wall); by the face met, for a collider's.
                bool shutY, shutX;
                if (met)
                {
                    shutY = Mathf.Abs(normal.y) >= Mathf.Abs(normal.x);
                    shutX = !shutY;
                }
                else
                {
                    shutY = Solid(new Vector2(last.x, q.y));
                    shutX = Solid(new Vector2(q.x, last.y));
                }
                var v = q - prev[i];
                var rest = last;
                if (shutY && !shutX)
                {
                    var along = new Vector2(q.x, last.y);
                    if (!Solid(along) && !Blocked(last, along)) rest = along;
                    v = new Vector2(v.x * Friction, 0f);
                }
                else if (shutX && !shutY)
                {
                    var along = new Vector2(last.x, q.y);
                    if (!Solid(along) && !Blocked(last, along)) rest = along;
                    v = new Vector2(0f, v.y * Friction);
                }
                else v = Vector2.zero;
                P[i] = rest;
                prev[i] = rest - v;
                DevClamped++;
                // Caught on it, unless it is the floor under his feet it came down onto.
                bool floor = shutY && !shutX && q.y > last.y && last.y >= pFeetY - 6f;
                if (!floor) touched = Mathf.Min(touched, i);
            }
            return touched;
        }

        static readonly ContactFilter2D terrain = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = 1 << World.ChunkColliders.TerrainLayer };
        static readonly Collider2D[] terrainHits = new Collider2D[8];
        static readonly RaycastHit2D[] crossings = new RaycastHit2D[8];

        /// <summary>Inside the ground or a wall: a block of the grid, or one of Hollow Knight's own solid terrain colliders (those the
        /// grid never took in among them; one-way platforms aside).</summary>
        static bool Solid(Vector2 px)
        {
            var w = TerraParticles.ToWorld(px);
            if (World.TerraWorld.IsSolidAt(w)) return true;
            int n = Physics2D.OverlapPoint(w, terrain, terrainHits);
            for (int i = 0; i < n; i++)
                if (!terrainHits[i].usedByEffector) return true;
            return false;
        }

        static bool GridSolid(Vector2 px) => World.TerraWorld.IsSolidAt(TerraParticles.ToWorld(px));

        /// <summary>The way from a to b meets one of Hollow Knight's terrain colliders (one-way platforms aside): where it first does
        /// and the way that face looks (Terraria px, y down).</summary>
        static bool Meets(Vector2 a, Vector2 b, out Vector2 at, out Vector2 normal)
        {
            at = b;
            normal = Vector2.zero;
            if ((b - a).sqrMagnitude < 1e-4f) return false;
            int n = Physics2D.Linecast(TerraParticles.ToWorld(a), TerraParticles.ToWorld(b), terrain, crossings);
            float first = 2f;
            for (int i = 0; i < n; i++)
            {
                var h = crossings[i];
                if (h.collider == null || h.collider.usedByEffector || h.fraction >= first) continue;
                first = h.fraction;
                at = Vector2.Lerp(a, b, h.fraction);
                normal = new Vector2(h.normal.x, -h.normal.y);
            }
            return first <= 1f;
        }

        /// <summary>1.5 px short of a face met on the way from a, not back past a.</summary>
        static Vector2 Short(Vector2 a, Vector2 face)
        {
            float len = (face - a).magnitude;
            return len <= 1.5f ? a : Vector2.Lerp(a, face, 1f - 1.5f / len);
        }

        /// <summary>From a to b as far as is free (short of the first ground or wall in the way).</summary>
        static Vector2 Reach(Vector2 a, Vector2 b)
        {
            if (Solid(a)) return b;
            if (Meets(a, b, out var face, out _)) b = Short(a, face);
            var d = b - a;
            int steps = Mathf.Max(1, Mathf.CeilToInt(d.magnitude / 4f));
            var last = a;
            for (int k = 1; k <= steps; k++)
            {
                var at = a + d * ((float)k / steps);
                if (Solid(at)) return last;
                last = at;
            }
            return b;
        }

        /// <summary>Out of the ground for one inside it: toward the link before it first (the rope's own side of a wall), then
        /// the nearest free place up, down or to either side within 4 blocks, else the top above it however deep (the ground
        /// under a floor is solid far down).</summary>
        static bool Escape(Vector2 q, Vector2 toward, out Vector2 free)
        {
            var back = toward - q;
            if (back.sqrMagnitude > 1e-4f)
            {
                back.Normalize();
                for (int k = 1; k <= 16; k++)
                {
                    var at = q + back * (4f * k);
                    if (!Solid(at)) { free = at; return true; }
                }
            }
            for (int k = 1; k <= 16; k++)
                foreach (var dir in sides)
                {
                    var at = q + dir * (4f * k);
                    if (!Solid(at)) { free = at; return true; }
                }
            for (int k = 17; k <= 100; k++)
            {
                var at = q - new Vector2(0f, 4f * k);
                if (!Solid(at)) { free = at; return true; }
            }
            free = q;
            return false;
        }

        static readonly Vector2[] sides = { new Vector2(0f, -1f), new Vector2(-1f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f) };

        /// <summary>
        /// The rope's links in its physics: about LinkPx long (short enough to go round a block's corner), twenty to forty-eight
        /// of them; the whip's own links drawn along a smooth curve through them.
        /// </summary>
        static int PhysLinks(Crack c) => c.P != null ? c.P.Length - 1 : Walls ? Mathf.Clamp(Mathf.CeilToInt(c.Reach / LinkPx), 20, 48) : Mathf.Min(c.S.Segments, 20);

        static readonly float[] arc = new float[64];

        /// <summary>The whip's links (count + 1 points) along a smooth curve (Catmull-Rom) through the rope's m + 1, evenly by length;
        /// where the curve would bulge into the ground or a wall (or across a hollow one's face), on the straight between the two;
        /// a drawn stretch that would cut a corner the rope goes round ends on the rope's point there.</summary>
        static void Resample(Vector2[] src, int m, int count, List<Vector2> dst)
        {
            dst.Clear();
            if (m == count)
            {
                for (int i = 0; i <= m; i++) dst.Add(src[i]);
                return;
            }
            arc[0] = 0f;
            for (int i = 1; i <= m; i++) arc[i] = arc[i - 1] + (src[i] - src[i - 1]).magnitude;
            float total = arc[m];
            int seg = 1;
            for (int k = 0; k <= count; k++)
            {
                float at = total * k / count;
                while (seg < m && arc[seg] < at) seg++;
                float len = arc[seg] - arc[seg - 1];
                float u = len > 1e-4f ? Mathf.Clamp01((at - arc[seg - 1]) / len) : 0f;
                var p0 = src[Mathf.Max(0, seg - 2)];
                var p1 = src[seg - 1];
                var p2 = src[seg];
                var p3 = src[Mathf.Min(m, seg + 1)];
                var curve = 0.5f * (2f * p1 + (p2 - p0) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u * u + (3f * p1 - p0 - 3f * p2 + p3) * u * u * u);
                var q = Walls && k >= 2 && (Solid(curve) || Meets(p1, curve, out _, out _)) ? Vector2.Lerp(p1, p2, u) : curve;
                // Drawn with fewer links than the rope has: a drawn stretch across a corner the rope goes round would cut it;
                // its end on the rope's own point at the corner instead.
                if (Walls && count < m && dst.Count > 0 && Blocked(dst[dst.Count - 1], q)) q = p1;
                dst.Add(q);
            }
        }

        /// <summary>The rope's links for Step and the rest (frameOffset −1: before the last update; else now).</summary>
        bool StylePoints(Crack c, List<Vector2> list, float frameOffset)
        {
            if (!styles.ContainsKey(c.Kind)) return false;
            if (c.P == null) SimStart(c, styles[c.Kind], MoveOf(c));
            DevStyled.Add(c.Kind);
            Resample(frameOffset < 0f ? c.Before : c.P, PhysLinks(c), c.S.Segments, list);
            return true;
        }

        static readonly Vector2[] drawn = new Vector2[64];

        /// <summary>The rope as drawn: between its place as this tick began and its place now, by the share of the tick gone.</summary>
        void StyleDrawPoints(Crack c, List<Vector2> list)
        {
            if (c.P == null) SimStart(c, styles[c.Kind], MoveOf(c));
            int m = PhysLinks(c);
            float a = Mathf.Clamp01(tickAccumulator);
            // Between the two, unless that would put a link in the ground or a wall or draw one through it (round a corner):
            // then where it is now.
            bool clear = true;
            for (int i = 0; i <= m; i++)
            {
                drawn[i] = Vector2.Lerp(c.TickStart[i], c.P[i], a);
                if (Walls && i >= 2 && (Solid(drawn[i]) || Blocked(drawn[i - 1], drawn[i]))) clear = false;
            }
            Resample(clear ? drawn : c.P, m, c.S.Segments, list);
            if (DevCount) DevCountFrame(c, list, clear ? drawn : c.P, m, clear);
        }

        /// <summary>Dev (suite whipstyle): count what the drawn rope does (off unless a test asks).</summary>
        public static bool DevCount;
        /// <summary>Dev: a place (world) no link should be drawn in (a test's wall of Hollow Knight's own kind); links drawn in it.</summary>
        public static Rect DevBox;
        public static int DevInBox;
        /// <summary>Dev: the most sides of DevBox the rope has lain against at once (3 or 4: wrapped round it).</summary>
        public static int DevSides;

        void DevCountFrame(Crack c, List<Vector2> list, Vector2[] src, int m, bool clear)
        {
            for (int i = 1; i < list.Count; i++) DevLowest = Mathf.Max(DevLowest, list[i].y - pFeetY);
            int sideMask = 0;
            for (int i = 2; i < list.Count; i++)
            {
                var w = TerraParticles.ToWorld(list[i]);
                if (DevBox.Contains(w)) DevInBox++;
                // Which sides of the dev box the rope lies against (within a third of a unit outside it).
                if (DevBox.width > 0f && w.x > DevBox.xMin - 0.35f && w.x < DevBox.xMax + 0.35f && w.y > DevBox.yMin - 0.35f && w.y < DevBox.yMax + 0.35f && !DevBox.Contains(w))
                    sideMask |= w.y > DevBox.yMax ? 1 : w.y < DevBox.yMin ? 4 : w.x < DevBox.xMin ? 8 : 2;
            }
            int sides = 0;
            for (int b = sideMask; b != 0; b >>= 1) sides += b & 1;
            DevSides = Mathf.Max(DevSides, sides);
            // Sharp folds (links turning over 60 degrees) and links behind his back, a drawn frame.
            DevFrames++;
            for (int i = 2; i < list.Count; i++)
            {
                DevLinks++;
                if ((list[i].x - pCenter.x) * c.SpriteDir < -22f) DevBehind++;
                if (i + 1 >= list.Count) continue;
                var u1 = list[i] - list[i - 1];
                var u2 = list[i + 1] - list[i];
                if (u1.sqrMagnitude > 1e-4f && u2.sqrMagnitude > 1e-4f && Vector2.Dot(u1.normalized, u2.normalized) < 0.5f) DevKinks++;
            }
            for (int i = 2; i < list.Count; i++)
                if (Solid(list[i]))
                {
                    DevInside++;
                    if (DevInsideWhy.Length == 0)
                    {
                        var sb = new System.Text.StringBuilder($"第 {i}/{list.Count - 1} 节 {list[i]:F0}，插值 {clear}，物理点：");
                        for (int j = 0; j <= m; j++) sb.Append($" {j}{(Solid(src[j]) ? "!" : "")}{src[j]:F0}{(j >= 1 && Blocked(src[j - 1], src[j]) ? "×" : "")}");
                        DevInsideWhy = sb.ToString();
                    }
                }
            for (int i = 2; i < list.Count; i++)
                if (Solid(Vector2.Lerp(list[i - 1], list[i], 0.25f)) && Solid((list[i - 1] + list[i]) * 0.5f) && Solid(Vector2.Lerp(list[i - 1], list[i], 0.75f))) DevThreaded++;
        }

        /// <summary>The rope's place as a tick begins (for drawing between ticks).</summary>
        static void TickBegins(Crack c)
        {
            if (c.P != null) System.Array.Copy(c.P, c.TickStart, c.P.Length);
        }

        /// <summary>The arm's way for the player's pose (degrees from level for the way he faces, + up), while the held whip lashes its own way.</summary>
        public bool ArmAngle(out float deg)
        {
            deg = 0f;
            foreach (var c in live)
            {
                if (c.Clone || c.ReachItem != null || !Styled(c)) continue;
                float aim = Mathf.Atan2(-c.Vel.y, Mathf.Abs(c.Vel.x) < 1e-4f ? 1e-4f : Mathf.Abs(c.Vel.x)) * Mathf.Rad2Deg;
                deg = aim + c.HandleDeg;
                return true;
            }
            return false;
        }

        // ------------------------------------------------------------------ the motes and the crack

        /// <summary>Each update of a styled whip: motes of its colour off the links (more toward the tip), and at the crack a burst.</summary>
        void StyleDust(Crack c)
        {
            if (!Styled(c) || points.Count < 3) return;
            var p = TerraParticles.Instance;
            if (p == null) return;
            var st = styles[c.Kind];
            var m = MoveOf(c);
            float t = c.Ai0 / c.T, before = (c.Ai0 - 1f) / c.T;
            var colour = Tint(st, Time.time);
            if (Random.value < 0.45f)
            {
                int k = Mathf.Clamp(Mathf.RoundToInt(Mathf.Sqrt(Random.value) * (points.Count - 1)), 1, points.Count - 1);
                var d = (points[k] - points[k - 1]).normalized;
                Mote(p, points[k], d * Random.Range(0.5f, 2f) + Random.insideUnitCircle * 0.6f, colour, Random.Range(0.45f, 0.75f));
            }
            if (before < m.Crack && t >= m.Crack && !c.Cracked && !c.Reel)
            {
                c.Cracked = true;
                c.CrackAt = Time.time;
                var tip = points[points.Count - 1];
                var d = (tip - points[points.Count - 2]).normalized;
                for (int k = 0; k < 16; k++)
                {
                    float a = k / 16f * Mathf.PI * 2f;
                    Mote(p, tip, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(1.5f, 3.5f) + d * 1.5f, k % 3 == 0 ? Color.white : colour, Random.Range(0.6f, 0.95f));
                }
            }
        }

        static void Mote(TerraParticles p, Vector2 at, Vector2 vel, Color colour, float scale)
        {
            int i = p.NewDust(at - new Vector2(2f, 2f), 4, 4, 278, 0f, 0f, 40, scale);
            if (i < 0) return;
            p[i].Velocity = vel;
            p[i].NoGravity = true;
            p[i].Color = colour;
            p[i].FadeIn = scale * 0.7f;
        }

        static Color Tint(Style st, float time) => st.Rainbow ? Color.HSVToRGB(Mathf.Repeat(time * 1.5f, 1f), 0.75f, 1f) : st.Trail;

        // ------------------------------------------------------------------ the ribbon and the flash

        static Sprite pixel;

        /// <summary>A white block pivoting on its left middle, for the ribbon's pieces.</summary>
        static Sprite Pixel
        {
            get
            {
                if (pixel != null) return pixel;
                var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "TerraKnight/whip_ribbon" };
                var px = new Color32[16];
                for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
                tex.SetPixels32(px);
                tex.Apply(false, true);
                pixel = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0f, 0.5f), 1f / Px, 0, SpriteMeshType.FullRect);
                return pixel;
            }
        }

        const float RibbonLife = 0.11f;

        /// <summary>The tip's ribbon (its places over the last tenth of a second, thinning and fading) and a small flash at the crack.</summary>
        void DrawStyle(FxBatch fx, Crack c)
        {
            if (!Styled(c) || drawPoints.Count < 2) return;
            var st = styles[c.Kind];
            var m = MoveOf(c);
            float now = Time.time;
            c.Ribbon.Add((drawPoints[drawPoints.Count - 1], now));
            while (c.Ribbon.Count > 0 && now - c.Ribbon[0].At > RibbonLife) c.Ribbon.RemoveAt(0);
            float t = (c.Ai0 + tickAccumulator * c.S.Updates) / c.T;
            // Strongest round the crack, never gone while it lashes.
            float strength = 0.35f + 0.65f * AtCrack(m, t, 0.25f);
            for (int k = 1; k < c.Ribbon.Count; k++)
            {
                var a = c.Ribbon[k - 1].Pos;
                var b = c.Ribbon[k].Pos;
                var d = b - a;
                float len = d.magnitude;
                // Not across the ground or a wall the tip went over.
                if (len < 0.5f || Walls && Blocked(a, b)) continue;
                float fresh = 1f - (now - c.Ribbon[k].At) / RibbonLife;
                var colour = st.Rainbow ? Tint(st, now + k * 0.03f) : st.Trail;
                float rot = Mathf.Atan2(d.y, d.x);
                fx.Draw(Pixel, a, Glow(colour, 0.45f * fresh * strength), rot, new Vector2((len + 1f) / 4f, (1.5f + 5f * fresh) / 4f), overPlayer: true);
                fx.Draw(Pixel, a, Glow(Color.white, 0.5f * fresh * strength), rot, new Vector2((len + 1f) / 4f, (0.6f + 1.6f * fresh) / 4f), overPlayer: true);
            }
            if (c.Cracked && now - c.CrackAt < 0.14f)
            {
                float f = (now - c.CrackAt) / 0.14f;
                var core = Color.Lerp(Tint(st, now), Color.white, 0.4f) * 0.7f;
                core.a = 0f;
                fx.Sparkle(1f, drawPoints[drawPoints.Count - 1], core, Tint(st, now), f, 0f, 0.2f, 0.45f, 1f, c.SpriteDir * 0.4f, new Vector2(1.3f, 1.3f), new Vector2(1f, 1f), true);
            }
        }

        static Color Glow(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 0f);
    }
}
