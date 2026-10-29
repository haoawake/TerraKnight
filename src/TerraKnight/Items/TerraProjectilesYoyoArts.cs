using System.Collections.Generic;
using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The yoyos' tricks (the user's 10-26 "把悠悠球的升级加进去…特效，动画，粒子效果，新颖，多样"): with the remade swings on
    /// (BladeArts.Enabled) a held yoyo goes through tricks of its own round the cursor, one after another on its own rhythm
    /// (睡眠 the sleeper's wobble, 环游世界 loops round the point, 遛狗 rolled along the ground under it, 摇篮摆荡 rocked
    /// across it, 射月 flung up and dropped on it, 八字 a figure eight, 抽送 snapped in and out along the string, 彗星环 a
    /// tilted orbit); round an enemy at the point the trick tightens, and a blow after time away lands harder (YoyoGapBlow). Early yoyos know two tricks, the yoyos of the end five.
    /// Its string reaches 30% further, glows in the yoyo's colours and ripples when a trick begins; the yoyo trails a ribbon
    /// and afterimages and sheds motes of its theme (BladeArts' themes: the Cascade's fire, the Amarok's frost, the Kraken's
    /// water, the Terrarian's green...), a burst where it strikes. The right-click arts (BladeSkillsYoyo.cs) work through the
    /// yoyo out: a loose copy rolling off, ricochets, orbiting copies, rings of power, a slam, a whirl round the player, its
    /// own shots in a burst. Nothing moves the player.
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        enum Trick : byte { Sleeper, AroundWorld, WalkDog, Rock, ShootMoon, Eight, Pump, Comet }

        static readonly string[] trickNames = { "睡眠", "环游世界", "遛狗", "摇篮摆荡", "射月", "八字", "抽送", "彗星环" };

        /// <summary>The string's reach over the game's, the remade yoyos.</summary>
        const float YoyoReach = 1.3f;

        /// <summary>A yoyo's look and tricks: its colours and motes (a BladeArts.Art of its own), its tricks in turn, seconds a trick.</summary>
        sealed class YoyoStyle
        {
            public BladeArts.Art Art;
            public Trick[] Tricks;
            public float Rhythm;
        }

        static YoyoStyle YS(ItemType t, BladeArts.Theme theme, Color main, Color light, int level, float rhythm, params Trick[] tricks) => new YoyoStyle
        {
            Art = new BladeArts.Art { Type = t, Theme = theme, Main = main, Light = light, Dark = main * 0.5f, Level = level, Rainbow = theme == BladeArts.Theme.Rainbow },
            Tricks = tricks, Rhythm = rhythm,
        };

        static Dictionary<ProjKind, YoyoStyle> yoyoStyles;

        static Dictionary<ProjKind, YoyoStyle> YoyoStyles => yoyoStyles ?? (yoyoStyles = new Dictionary<ProjKind, YoyoStyle>
        {
            [ProjKind.WoodenYoyo] = YS(ItemType.WoodYoyo, BladeArts.Theme.Wood, new Color(0.62f, 0.43f, 0.26f), new Color(0.9f, 0.72f, 0.48f), 0, 1.8f, Trick.Sleeper, Trick.Rock),
            [ProjKind.RallyYoyo] = YS(ItemType.Rally, BladeArts.Theme.Candy, new Color(0.95f, 0.25f, 0.25f), new Color(1f, 0.92f, 0.9f), 0, 1.7f, Trick.Pump, Trick.Rock),
            [ProjKind.MalaiseYoyo] = YS(ItemType.CorruptYoyo, BladeArts.Theme.Shadow, new Color(0.5f, 0.3f, 0.75f), new Color(0.8f, 0.6f, 1f), 1, 1.7f, Trick.AroundWorld, Trick.Sleeper),
            [ProjKind.ArteryYoyo] = YS(ItemType.CrimsonYoyo, BladeArts.Theme.Blood, new Color(0.8f, 0.12f, 0.15f), new Color(1f, 0.45f, 0.4f), 1, 1.7f, Trick.Rock, Trick.AroundWorld),
            [ProjKind.AmazonYoyo] = YS(ItemType.JungleYoyo, BladeArts.Theme.Leaf, new Color(0.35f, 0.75f, 0.2f), new Color(0.7f, 1f, 0.45f), 1, 1.6f, Trick.WalkDog, Trick.Eight),
            [ProjKind.Code1Yoyo] = YS(ItemType.Code1, BladeArts.Theme.Electric, new Color(0.3f, 0.55f, 1f), new Color(0.65f, 0.9f, 1f), 1, 1.6f, Trick.Eight, Trick.Pump, Trick.Sleeper),
            [ProjKind.HiveFiveYoyo] = YS(ItemType.HiveFive, BladeArts.Theme.Honey, new Color(1f, 0.7f, 0.15f), new Color(1f, 0.9f, 0.5f), 2, 1.6f, Trick.Eight, Trick.Rock, Trick.Sleeper),
            [ProjKind.ValorYoyo] = YS(ItemType.Valor, BladeArts.Theme.Star, new Color(0.3f, 0.5f, 1f), new Color(0.7f, 0.85f, 1f), 2, 1.6f, Trick.Pump, Trick.ShootMoon, Trick.AroundWorld),
            [ProjKind.CascadeYoyo] = YS(ItemType.Cascade, BladeArts.Theme.Fire, new Color(1f, 0.45f, 0.12f), new Color(1f, 0.82f, 0.35f), 2, 1.6f, Trick.AroundWorld, Trick.ShootMoon, Trick.WalkDog),
            [ProjKind.ChikYoyo] = YS(ItemType.Chik, BladeArts.Theme.Petal, new Color(1f, 0.45f, 0.85f), new Color(1f, 0.8f, 1f), 3, 1.5f, Trick.Eight, Trick.Comet, Trick.Rock),
            [ProjKind.AmarokYoyo] = YS(ItemType.Amarok, BladeArts.Theme.Ice, new Color(0.45f, 0.75f, 1f), new Color(0.85f, 0.97f, 1f), 3, 1.5f, Trick.WalkDog, Trick.Rock, Trick.AroundWorld),
            [ProjKind.HelFireYoyo] = YS(ItemType.HelFire, BladeArts.Theme.Fire, new Color(1f, 0.35f, 0.08f), new Color(1f, 0.75f, 0.3f), 3, 1.5f, Trick.Comet, Trick.ShootMoon, Trick.Eight, Trick.AroundWorld),
            [ProjKind.GradientYoyo] = YS(ItemType.Gradient, BladeArts.Theme.Rainbow, new Color(1f, 0.5f, 0.8f), new Color(0.7f, 0.9f, 1f), 3, 1.5f, Trick.Comet, Trick.Eight, Trick.AroundWorld, Trick.Rock),
            [ProjKind.FormatCYoyo] = YS(ItemType.FormatC, BladeArts.Theme.Holy, new Color(1f, 0.8f, 0.25f), new Color(1f, 0.95f, 0.65f), 3, 1.4f, Trick.Rock, Trick.Pump, Trick.Eight, Trick.Sleeper),
            [ProjKind.Code2Yoyo] = YS(ItemType.Code2, BladeArts.Theme.Electric, new Color(0.25f, 0.85f, 1f), new Color(0.7f, 1f, 1f), 4, 1.4f, Trick.Eight, Trick.Comet, Trick.Pump, Trick.WalkDog),
            [ProjKind.YeletsYoyo] = YS(ItemType.Yelets, BladeArts.Theme.Spore, new Color(0.55f, 0.85f, 0.25f), new Color(0.85f, 1f, 0.5f), 4, 1.4f, Trick.Rock, Trick.WalkDog, Trick.Eight, Trick.Comet),
            [ProjKind.RedsThrowYoyo] = YS(ItemType.RedsYoyo, BladeArts.Theme.Fire, new Color(0.95f, 0.15f, 0.1f), new Color(1f, 0.55f, 0.4f), 5, 1.3f, Trick.Comet, Trick.ShootMoon, Trick.Rock, Trick.Pump, Trick.Eight),
            [ProjKind.ValkyrieYoyo] = YS(ItemType.ValkyrieYoyo, BladeArts.Theme.Feather, new Color(0.85f, 0.85f, 1f), new Color(1f, 1f, 1f), 5, 1.3f, Trick.AroundWorld, Trick.Comet, Trick.Eight, Trick.ShootMoon, Trick.WalkDog),
            [ProjKind.EyeYoyo] = YS(ItemType.TheEyeOfCthulhu, BladeArts.Theme.Blood, new Color(0.85f, 0.2f, 0.2f), new Color(1f, 0.6f, 0.55f), 4, 1.4f, Trick.Comet, Trick.Pump, Trick.AroundWorld, Trick.ShootMoon),
            [ProjKind.KrakenYoyo] = YS(ItemType.Kraken, BladeArts.Theme.Spirit, new Color(0.25f, 0.45f, 1f), new Color(0.6f, 0.85f, 1f), 4, 1.4f, Trick.AroundWorld, Trick.Comet, Trick.WalkDog, Trick.Rock),
            [ProjKind.TerrarianYoyo] = YS(ItemType.Terrarian, BladeArts.Theme.Terra, new Color(0.3f, 0.9f, 0.45f), new Color(0.75f, 1f, 0.8f), 5, 1.3f, Trick.Comet, Trick.Eight, Trick.ShootMoon, Trick.AroundWorld, Trick.Rock),
        });

        /// <summary>A yoyo's style (null: the Abyss Yoyo and the counterweights keep their own).</summary>
        static YoyoStyle StyleOf(ProjKind k) => YoyoStyles.TryGetValue(k, out var s) ? s : null;

        /// <summary>A yoyo out with the remade swings: its trick, the string's ripple, and what an art has it doing.</summary>
        sealed class YoyoState
        {
            public int Trick = -1;
            public float TrickAt, WaveAt = -10f;
            /// <summary>An orbiting copy's yoyo, its place round it, until when.</summary>
            public Proj OrbitOf;
            public float OrbitPhase, OrbitUntil;
            /// <summary>The Rally's ricochet: the enemies still to strike, since when at the one now.</summary>
            public List<HealthManager> Ricochet;
            public float RicochetAt;
            /// <summary>A whirl round the player: since when, how long, which way.</summary>
            public float TetherAt = -10f, TetherFor, TetherSide;
            /// <summary>A slam on its way down: how full its charge was.</summary>
            public bool Slamming;
            public float SlamCharge, SlamAt;
            public ItemDef SlamDef;
            /// <summary>Format:C's focus: every blow a crit until then.</summary>
            public float FocusUntil;
            public int Motes;
            /// <summary>When it last struck each enemy (its own ticks), the last blow's weight (YoyoGapBlow).</summary>
            public readonly Dictionary<int, int> LastHit = new Dictionary<int, int>();
            public float LastBlow = 1f;
        }

        static readonly Dictionary<Proj, YoyoState> yoyoStates = new Dictionary<Proj, YoyoState>();

        /// <summary>Dev: the tricks begun by name, arts by kind, rings and slams landed, orbiting copies made, ricochet blows, burst shots.</summary>
        public static readonly HashSet<string> DevYoyoTricks = new HashSet<string>();
        public static int DevYoyoTrickChanges, DevYoyoPulses, DevYoyoSlams, DevYoyoOrbiters, DevYoyoRicochets, DevYoyoBursts, DevYoyoLoose, DevYoyoWhirls, DevYoyoFocus;
        public static string DevYoyoTrick = "";
        /// <summary>Dev (suite yoyoarts): the game's yoyos' ticks between blows on the same enemy (gaps under a second), by kind.</summary>
        public static readonly Dictionary<ProjKind, (float Sum, int N)> DevGaps = new Dictionary<ProjKind, (float, int)>();
        static readonly Dictionary<(Proj, int), int> devLastHit = new Dictionary<(Proj, int), int>();

        static void DevGap(Proj p, HealthManager hm)
        {
            var key = (p, hm.GetInstanceID());
            if (devLastHit.TryGetValue(key, out int at) && p.Ticks - at < 60)
            {
                DevGaps.TryGetValue(p.Kind, out var g);
                DevGaps[p.Kind] = (g.Sum + p.Ticks - at, g.N + 1);
            }
            devLastHit[key] = p.Ticks;
            if (devLastHit.Count > 256) devLastHit.Clear();
        }

        public static float DevMeanGap(ProjKind k) => DevGaps.TryGetValue(k, out var g) && g.N > 0 ? g.Sum / g.N : 0f;

        /// <summary>Dev: when the test's hold began (its filmstrip's clock).</summary>
        public static float DevShowStart;

        /// <summary>The yoyo's state at the start of its update (null with the remade swings off, or for a loose copy).</summary>
        YoyoState YoyoArtPre(Proj p)
        {
            if (!BladeArts.Enabled || StyleOf(p.Kind) == null || p.Ai0 == -2f) return null;
            if (!yoyoStates.TryGetValue(p, out var st))
            {
                if (yoyoStates.Count > 48)
                {
                    var gone = new List<Proj>();
                    foreach (var kv in yoyoStates) if (kv.Key.Dead) gone.Add(kv.Key);
                    foreach (var g in gone) yoyoStates.Remove(g);
                }
                st = new YoyoState();
                yoyoStates[p] = st;
            }
            // An orbiting copy lives by its art's clock, not the yoyo's.
            if (st.OrbitOf != null) p.LocalAi0 = 0f;
            return st;
        }

        static bool YoyoAway(Proj q) => q == null || q.Dead || q.Ai0 < 0f;

        /// <summary>The point the held yoyo makes for: the cursor's, moved by the trick it is in (Terraria px).</summary>
        Vector2 YoyoTrickPoint(Proj p, YoyoState st, Vector2 owner, Vector2 point, float R)
        {
            var style = StyleOf(p.Kind);
            float now = Time.time;
            // A short-lived yoyo goes through its tricks faster (the Wooden Yoyo's three seconds).
            float life = yoyos[p.Kind].Life;
            float rhythm = life > 0f ? Mathf.Min(style.Rhythm, life * 0.35f) : style.Rhythm;
            if (st.Trick < 0 || now - st.TrickAt > rhythm)
            {
                st.Trick = (st.Trick + 1) % style.Tricks.Length;
                st.TrickAt = now;
                st.WaveAt = now;
                var name = trickNames[(int)style.Tricks[st.Trick]];
                DevYoyoTricks.Add(name);
                DevYoyoTrick = name;
                DevYoyoTrickChanges++;
                // A ring of the yoyo's motes as the trick begins.
                var c = CenterPx(p);
                for (int k = 0; k < 6; k++)
                {
                    float a = k / 6f * Mathf.PI * 2f;
                    SwordFx.SpearMote(style.Art, c, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 1.6f, 1f);
                }
            }
            float t = now - st.TrickAt;
            var dir = Norm(point - owner);
            var perp = new Vector2(-dir.y, dir.x);
            // Round an enemy at the point the trick tightens, the yoyo going through it the more.
            bool onEnemy = EnemyNearPx(point, 44f);
            float amp = onEnemy ? 0.35f : 1f;
            Vector2 off;
            const float Tau = Mathf.PI * 2f;
            switch (style.Tricks[st.Trick])
            {
                case Trick.Sleeper: off = new Vector2(Mathf.Sin(t * 5f) * 7f, Mathf.Sin(t * 10f) * 4f); break;
                case Trick.AroundWorld: off = new Vector2(Mathf.Cos(t * Tau / 0.7f), Mathf.Sin(t * Tau / 0.7f)) * 48f; break;
                case Trick.WalkDog:
                {
                    float ground = GroundBelowPx(point, 220f);
                    if (float.IsNaN(ground)) { off = perp * 60f * Mathf.Sin(t * Tau / 0.5f); break; }
                    off = new Vector2(Mathf.Sin(t * Tau / 0.9f) * 64f * amp, ground - 9f - point.y);
                    amp = 1f;
                    break;
                }
                case Trick.Rock: off = perp * 62f * Mathf.Sin(t * Tau / 0.45f); break;
                case Trick.ShootMoon:
                {
                    float u = t % 0.9f / 0.9f;
                    off = new Vector2(Mathf.Sin(u * Tau) * 26f, -110f * Mathf.Sin(u * Mathf.PI));
                    break;
                }
                case Trick.Eight: off = new Vector2(Mathf.Sin(t * Tau / 1.1f) * 58f, Mathf.Sin(t * Tau / 0.55f) * 26f); break;
                case Trick.Pump: off = -dir * 72f * (0.5f - 0.5f * Mathf.Cos(t * Tau / 0.6f)); break;
                default:
                {
                    // Comet: a tilted ellipse round the point.
                    float w = t * Tau / 0.9f;
                    off = dir * Mathf.Cos(w) * 74f + perp * Mathf.Sin(w) * 34f;
                    break;
                }
            }
            var q = point + off * amp;
            if ((q - owner).magnitude > R - 1f) q = owner + Norm(q - owner) * (R - 1f);
            return q;
        }

        /// <summary>An enemy's box within that many px of a point.</summary>
        static bool EnemyNearPx(Vector2 px, float range)
        {
            foreach (var hm in AccessoryEffects.Enemies(TerraParticles.ToWorld(px), range, accNear))
                if (Chaseable(hm)) return true;
            return false;
        }

        /// <summary>The ground's top under a point within that many px (Terraria px, y down), NaN with none.</summary>
        static float GroundBelowPx(Vector2 px, float depth)
        {
            for (float d = 0f; d <= depth; d += 4f)
                if (TerraWorld.IsSolidAt(TerraParticles.ToWorld(px + new Vector2(0f, d))))
                    return px.y + d;
            return float.NaN;
        }

        /// <summary>
        /// The yoyo's way after its own steering: an art's (an orbiting copy round its yoyo, the ricochet from enemy to enemy,
        /// the whirl round the player, the slam's drop) where one has it; its motes.
        /// </summary>
        Vector2 YoyoArtSteer(Proj p, YoyoState st, Vector2 owner, Vector2 v, float R, float V)
        {
            var style = StyleOf(p.Kind);
            var c = CenterPx(p);
            float now = Time.time;
            if (st.OrbitOf != null)
            {
                if (now > st.OrbitUntil || YoyoAway(st.OrbitOf))
                {
                    st.OrbitOf = null;
                    p.Ai0 = -1f;
                }
                else
                {
                    var centre = CenterPx(st.OrbitOf);
                    float a = st.OrbitPhase + now * Mathf.PI * 2f / 0.75f;
                    var to = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 38f;
                    v = Vector2.ClampMagnitude((to - c) * 0.45f, Mathf.Max(V * 1.6f, 14f));
                    p.Collide = false;
                }
            }
            else if (st.Ricochet != null)
            {
                while (st.Ricochet.Count > 0 && (st.Ricochet[0] == null || st.Ricochet[0].isDead)) st.Ricochet.RemoveAt(0);
                if (st.Ricochet.Count == 0) st.Ricochet = null;
                else
                {
                    var e = EnemyCenterPx(st.Ricochet[0]);
                    if (Vector2.Distance(e, c) < 14f || now - st.RicochetAt > 0.3f)
                    {
                        st.Ricochet.RemoveAt(0);
                        st.RicochetAt = now;
                        p.Immune.Clear();
                        if (st.Ricochet.Count == 0) st.Ricochet = null;
                    }
                    v = Norm(e - c) * Mathf.Max(V * 1.8f, 20f);
                    p.Collide = false;
                    StreakTrailMote(style, c, v);
                }
            }
            else if (now - st.TetherAt < st.TetherFor)
            {
                float u = (now - st.TetherAt) / st.TetherFor;
                float a = st.TetherSide * u * Mathf.PI * 2f * 2.25f - Mathf.PI / 2f;
                float r = Mathf.Min(R * 0.62f, 150f) * Mathf.Sin(Mathf.Min(1f, u * 4f) * Mathf.PI / 2f);
                var to = owner + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                // Under him it skims along the ground rather than going through it.
                float ground = GroundBelowPx(owner, 120f);
                if (!float.IsNaN(ground)) to.y = Mathf.Min(to.y, ground - 9f);
                v = Vector2.ClampMagnitude((to - c) * 0.6f, 30f);
                p.Collide = false;
                if (p.Ticks % 3 == 0) p.Immune.Clear();
            }
            else if (st.Slamming)
            {
                v = new Vector2(v.x * 0.6f, 26f);
                bool landed = TerraWorld.IsSolidAt(TerraParticles.ToWorld(c + new Vector2(0f, 14f))) || now - st.SlamAt > 0.7f;
                if (landed)
                {
                    st.Slamming = false;
                    SlamLanding(p, st, c);
                    v = new Vector2(0f, -6f);
                }
            }
            // The sleeper spins fast where it hangs.
            if (st.Trick >= 0 && style.Tricks[st.Trick] == Trick.Sleeper && st.OrbitOf == null) p.TRot += 0.35f;
            // Motes off the yoyo as it goes, the more the faster.
            float speed = v.magnitude;
            if (speed > 2.5f && style != null && (++st.Motes % (speed > 9f ? 1 : 2)) == 0)
                SwordFx.SpearMote(style.Art, c - v * Random.value, -v * 0.08f + Random.insideUnitCircle * 0.4f, v.x < 0f ? -1f : 1f);
            return v;
        }

        static void StreakTrailMote(YoyoStyle style, Vector2 c, Vector2 v)
        {
            if (style == null) return;
            SwordFx.SpearMote(style.Art, c - v * 0.5f * Random.value, -v * 0.1f, v.x < 0f ? -1f : 1f);
        }

        /// <summary>
        /// A blow of a remade yoyo weighted by its trick's pace: the ticks since this yoyo last struck this enemy over the game's
        /// yoyo's own gap between blows on an enemy it sits on (YoyoGap), ×1 to ×1.25: a trick that takes the yoyo off the enemy
        /// lands that much harder on its return, one that keeps it there adds nothing, so the yoyo deals about what the game's
        /// does whatever its tricks. Not during an art (a copy, a ricochet, a
        /// whirl, a slam). Its weight is kept for the hit's look.
        /// </summary>
        static float YoyoGapBlow(Proj p, HealthManager hm)
        {
            if (!yoyoStates.TryGetValue(p, out var st) || st.OrbitOf != null || st.Ricochet != null || st.Slamming || Time.time - st.TetherAt < st.TetherFor) return 1f;
            int id = hm.GetInstanceID();
            float w = 1f;
            if (st.LastHit.TryGetValue(id, out int at)) w = Mathf.Clamp((p.Ticks - at) / YoyoGap(p), 1f, 1.25f);
            st.LastHit[id] = p.Ticks;
            st.LastBlow = w;
            return w;
        }

        /// <summary>
        /// The game's held yoyo's usual ticks between blows on an enemy it sits on (bounced off, steered back): the slower the
        /// yoyo the longer (the Valor's 12 at 14 px an update, the Rally's about 25 at 11), never under the enemy's 10 ticks.
        /// </summary>
        static float YoyoGap(Proj p)
        {
            float ms = YoyoMeleeSpeed(p);
            float v = yoyos[p.Kind].Speed / ((1f + ms * 3f) / 4f);
            return Mathf.Max(10f, 12f * Mathf.Exp(-0.24f * (v - 14f)));
        }

        /// <summary>A blow of a remade yoyo: Format:C's focus makes it a crit.</summary>
        float YoyoArtCrit(Proj p, float crit) =>
            p.Kind == ProjKind.FormatCYoyo && yoyoStates.TryGetValue(p, out var st) && Time.time < st.FocusUntil ? 100f : crit;

        /// <summary>A remade yoyo's blow landing: its theme's burst there.</summary>
        static void YoyoArtHit(Proj p, HealthManager hm)
        {
            var style = StyleOf(p.Kind);
            if (style == null || !BladeArts.Enabled) return;
            var e = EnemyCenterPx(hm);
            var c = CenterPx(p);
            SwordFx.SpearHit(style.Art, Vector2.Lerp(c, e, 0.4f), c.x < e.x ? 1f : -1f);
            // A heavy blow (the yoyo back on the enemy after a trick took it away): a second burst, the other way too.
            if (p.Ai0 >= 0f && yoyoStates.TryGetValue(p, out var st) && st.LastBlow >= 1.15f) SwordFx.SpearHit(style.Art, Vector2.Lerp(c, e, 0.4f), c.x < e.x ? -1f : 1f);
        }

        // ------------------------------------------------------------------ the look

        /// <summary>Under the remade yoyo: its ribbon through its last places and its afterimages, in its colours.</summary>
        static void DrawYoyoArt(FxBatch fx, Proj p, Vector2 px, bool held, Sprite spr)
        {
            var style = StyleOf(p.Kind);
            if (style == null) return;
            int n = Mathf.Min(p.Trail.Count, 10);
            if (n < 2) return;
            var main = BladeArts.Tint(style.Art, style.Art.Main, 0f);
            var light = BladeArts.Tint(style.Art, style.Art.Light, 0.4f);
            float speed = TV(p).magnitude;
            float w = Mathf.Clamp(speed * 0.7f, 3f, 11f);
            for (int i = 1; i < n; i++)
            {
                float f = i / (float)n;
                var a = i == 1 ? px : TerraParticles.ToPx(p.Trail[i - 1]);
                var b = TerraParticles.ToPx(p.Trail[i]);
                if ((a - b).sqrMagnitude > 90f * 90f) break;
                var col = style.Art.Rainbow ? BladeArts.Tint(style.Art, light, f) : Color.Lerp(light, main, f);
                StreakPx(fx, a, b, col, w * (1f - f * 0.75f), 0.5f * (1f - f));
            }
            // Afterimages (the Kraken, the Terrarian and the Hel-Fire have the game's own already).
            if (p.Kind == ProjKind.KrakenYoyo || p.Kind == ProjKind.TerrarianYoyo || p.Kind == ProjKind.HelFireYoyo || speed < 3f) return;
            for (int i = Mathf.Min(3, n - 1); i >= 1; i--)
            {
                float f = 0.3f - 0.08f * i;
                var c = new Color(light.r * f, light.g * f, light.b * f, 0f);
                fx.Draw(spr, TerraParticles.ToPx(p.Trail[i]), c, p.TRot - i * 0.4f, p.Scale, overPlayer: held);
            }
        }

        /// <summary>The string's ripple (a wave running out along it as a trick begins) for a link i links out; and its glow.</summary>
        static Vector2 YoyoStringRipple(Proj p, int i, Vector2 along)
        {
            if (!yoyoStates.TryGetValue(p, out var st)) return Vector2.zero;
            float age = Time.time - st.WaveAt;
            if (age > 0.8f) return Vector2.zero;
            float amp = 5f * Mathf.Exp(-age * 4f);
            return new Vector2(-along.y, along.x) * amp * Mathf.Sin(i * 0.9f - age * 24f);
        }

        static void YoyoStringGlow(FxBatch fx, Proj p, Vector2 a, Vector2 b)
        {
            var style = StyleOf(p.Kind);
            if (style == null) return;
            var c = BladeArts.Tint(style.Art, style.Art.Light, Random.value * 0.2f);
            StreakPx(fx, a, b, c, 3.5f, 0.22f);
        }

        // ------------------------------------------------------------------ the right-click arts' part (BladeSkillsYoyo)

        /// <summary>The held yoyo of an item out now (its first, not a copy), or null.</summary>
        Proj HeldYoyo(ItemDef def)
        {
            if (def == null) return null;
            foreach (var q in live)
                if (!q.Dead && q.Kind == def.Shoot && IsYoyo(q.Kind) && q.Ai0 >= 0f && q.Channel && yoyoStates.TryGetValue(q, out var st) && st.OrbitOf == null)
                    return q;
            return null;
        }

        /// <summary>The right-click art of the yoyo out (charge 0..1 for a held art, -1 pressed); false with no yoyo out.</summary>
        public bool YoyoArt(ItemDef def, float charge)
        {
            var p = HeldYoyo(def);
            if (p == null || !Holder(out var owner)) return false;
            var st = yoyoStates[p];
            var style = StyleOf(p.Kind);
            var c = CenterPx(p);
            int dmg = Equipment.Damage(def);
            float kb = Equipment.Knockback(def), crit = Equipment.Crit(def);
            float ch = Mathf.Clamp01(charge);
            var keep = SpawnClass;
            SpawnClass = DamageClass.Melee;
            try
            {
                switch (p.Kind)
                {
                    case ProjKind.WoodenYoyo: return LooseCopy(p, owner);
                    case ProjKind.RallyYoyo: return Ricochet(p, st, c, 5);
                    case ProjKind.MalaiseYoyo: return Orbiters(p, 2, 0.55f, 4f);
                    case ProjKind.Code1Yoyo: return Orbiters(p, 2, 0.6f, 4f);
                    case ProjKind.Code2Yoyo: return Orbiters(p, 3, 0.6f, 4.5f);
                    case ProjKind.ValkyrieYoyo: return Orbiters(p, 3, 0.65f, 5f);
                    case ProjKind.ArteryYoyo: Pulse(style, c, 78f, Mathf.RoundToInt(dmg * 1.6f), kb, crit, EnemyDebuffs.Ichor, 360); Sound("Item_17", 0.7f); return true;
                    case ProjKind.AmazonYoyo: Pulse(style, c, 84f, Mathf.RoundToInt(dmg * 1.6f), kb, crit, EnemyDebuffs.Poisoned, 420); Sound("Item_8", 0.7f); return true;
                    case ProjKind.AmarokYoyo: Pulse(style, c, 96f, Mathf.RoundToInt(dmg * 1.5f), kb, crit, EnemyDebuffs.Frostbite, 360); Sound("Item_30", 0.7f); return true;
                    case ProjKind.YeletsYoyo: Pulse(style, c, 92f, Mathf.RoundToInt(dmg * 1.5f), kb, crit, EnemyDebuffs.Venom, 360); Sound("Item_17", 0.7f); return true;
                    case ProjKind.HelFireYoyo:
                        Pulse(style, c, 80f + 70f * ch, Mathf.RoundToInt(dmg * (1.4f + 1.6f * ch)), kb + 3f, crit, EnemyDebuffs.Hellfire, 300);
                        Sound("Item_74", 0.6f);
                        Player.TerraCamera.Punch(0.08f + 0.1f * ch, 0.2f);
                        return true;
                    case ProjKind.HiveFiveYoyo: return BeeBurst(p, c, dmg, kb, crit);
                    case ProjKind.ValorYoyo: return Slam(p, st, def, ch);
                    case ProjKind.RedsThrowYoyo: return Whirl(p, st, owner, c, 1.3f);
                    case ProjKind.GradientYoyo: return Whirl(p, st, owner, c, 1.5f);
                    case ProjKind.FormatCYoyo:
                        st.FocusUntil = Time.time + 4f;
                        DevYoyoFocus++;
                        for (int k = 0; k < 12; k++)
                        {
                            float a = k / 12f * Mathf.PI * 2f;
                            SwordFx.SpearMote(style.Art, c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 18f, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.8f, 1f);
                        }
                        Sound("Item_4", 0.5f);
                        return true;
                    case ProjKind.CascadeYoyo: return CascadeVolley(p, c, dmg, kb, crit);
                    case ProjKind.ChikYoyo: return CrystalBurst(p, c, dmg, kb, crit);
                    case ProjKind.EyeYoyo: return EyeVolley(p, c, owner, dmg, kb);
                    case ProjKind.KrakenYoyo: return WaveFan(p, c, owner, dmg, kb, crit, ch);
                    case ProjKind.TerrarianYoyo: return TerraBurst(p, style, c, dmg, kb, crit, ch);
                    default: return false;
                }
            }
            finally { SpawnClass = keep; }
        }

        /// <summary>The Wooden Yoyo's: a loose copy flung off its string, rolling on along the ground through a few enemies.</summary>
        bool LooseCopy(Proj p, Vector2 owner)
        {
            var c = CenterPx(p);
            var way = Norm(c - owner);
            var v = new Vector2(way.x * 10f, Mathf.Min(way.y * 8f, -3f));
            var copy = Spawn(p.Kind, p.Pos, Flip(v), p.Damage, p.Knockback, p.Crit);
            if (copy == null) return false;
            copy.Ai0 = -2f;
            copy.Item = p.Item;
            copy.Melee = true;
            copy.Penetrate = 6;
            DevYoyoLoose++;
            Sound("Item_1", 0.6f);
            return true;
        }

        /// <summary>The Rally's: the yoyo darting from enemy to enemy near it, a blow on each.</summary>
        bool Ricochet(Proj p, YoyoState st, Vector2 c, int most)
        {
            var list = new List<HealthManager>();
            foreach (var hm in AccessoryEffects.Enemies(TerraParticles.ToWorld(c), 260f, accNear))
                if (Chaseable(hm) && TerraWorld.LineClearPx(c, EnemyCenterPx(hm))) list.Add(hm);
            if (list.Count == 0) return false;
            list.Sort((a, b) => Vector2.Distance(EnemyCenterPx(a), c).CompareTo(Vector2.Distance(EnemyCenterPx(b), c)));
            // Back and forth when there are few: each enemy struck again on the way back.
            var path = new List<HealthManager>();
            for (int k = 0; path.Count < most; k++) path.Add(list[k % list.Count]);
            st.Ricochet = path;
            st.RicochetAt = Time.time;
            p.Immune.Clear();
            DevYoyoRicochets += path.Count;
            Sound("Item_1", 0.6f);
            return true;
        }

        /// <summary>Copies of the yoyo orbiting it for a while, each at a share of its damage.</summary>
        bool Orbiters(Proj p, int count, float share, float seconds)
        {
            var style = StyleOf(p.Kind);
            int made = 0;
            for (int k = 0; k < count; k++)
            {
                var copy = Spawn(p.Kind, p.Pos, p.Vel, Mathf.Max(1, Mathf.RoundToInt(p.Damage * share)), p.Knockback * 0.6f, p.Crit);
                if (copy == null) continue;
                copy.Item = p.Item;
                copy.Channel = true;
                copy.Melee = true;
                copy.Point = CenterPx(p);
                yoyoStates[copy] = new YoyoState { OrbitOf = p, OrbitPhase = k * Mathf.PI * 2f / count, OrbitUntil = Time.time + seconds };
                made++;
            }
            var c = CenterPx(p);
            for (int k = 0; k < 10; k++)
            {
                float a = k / 10f * Mathf.PI * 2f;
                SwordFx.SpearMote(style.Art, c, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.2f, 1f);
            }
            DevYoyoOrbiters += made;
            Sound("Item_20", 0.5f);
            return made > 0;
        }

        /// <summary>A ring of the yoyo's power going out from a point: one blow to each enemy in it, a debuff, its motes.</summary>
        void Pulse(YoyoStyle style, Vector2 at, float radius, int dmg, float kb, float crit, int debuff, int ticks)
        {
            var ring = Spawn(ProjKind.YoyoPulse, TerraParticles.ToWorld(at), Vector2.zero, dmg, kb, crit);
            if (ring == null) return;
            ring.Melee = true;
            ring.Ai0 = debuff;
            ring.Ai1 = radius;
            ring.LocalAi1 = ticks;
            ring.HitSize = new Vector2(radius, radius) * 0.6f;
            pulseStyles[ring] = style;
            int n = 18 + style.Art.Level * 3;
            for (int k = 0; k < n; k++)
            {
                float a = k / (float)n * Mathf.PI * 2f;
                var w = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                SwordFx.SpearMote(style.Art, at + w * radius * 0.3f, w * radius / 22f, w.x < 0f ? -1f : 1f);
            }
            DevYoyoPulses++;
        }

        static readonly Dictionary<Proj, YoyoStyle> pulseStyles = new Dictionary<Proj, YoyoStyle>();

        void StepYoyoPulse(Proj p)
        {
            // Out to its full size in six updates.
            float f = Mathf.Clamp01((p.Ticks + 1) / 6f);
            p.HitSize = new Vector2(p.Ai1, p.Ai1) * 2f * (0.3f + 0.7f * f);
        }

        static void DrawYoyoPulse(FxBatch fx, Proj p, Vector2 px)
        {
            if (!pulseStyles.TryGetValue(p, out var style)) return;
            float f = Mathf.Clamp01(p.Ticks / 24f);
            float r = p.Ai1 * (0.3f + 0.7f * Mathf.Sqrt(Mathf.Clamp01(p.Ticks / 8f)));
            var col = BladeArts.Tint(style.Art, style.Art.Light, f);
            const int seg = 20;
            for (int k = 0; k < seg; k++)
            {
                float a0 = k / (float)seg * Mathf.PI * 2f, a1 = (k + 1) / (float)seg * Mathf.PI * 2f;
                StreakPx(fx, px + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * r, px + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * r, col, 10f * (1f - f) + 2f, 0.65f * (1f - f * f));
            }
        }

        /// <summary>The Hive-Five's: a swarm out of the yoyo, half a giant bee's share each.</summary>
        bool BeeBurst(Proj p, Vector2 c, int dmg, float kb, float crit)
        {
            for (int k = 0; k < 6; k++)
            {
                float a = k / 6f * Mathf.PI * 2f + Random.value * 0.4f;
                bool giant = k % 3 == 0;
                var bee = Spawn(giant ? ProjKind.GiantBee : ProjKind.Bee, p.Pos, Flip(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 3f), Mathf.Max(1, dmg / 2), kb * 0.5f, crit);
                if (bee != null) bee.Melee = true;
            }
            DevYoyoBursts += 6;
            Sound("Item_97", 0.6f);
            return true;
        }

        /// <summary>The Valor's: the yoyo driven down onto the ground under it; a shockwave there.</summary>
        bool Slam(Proj p, YoyoState st, ItemDef def, float charge)
        {
            st.Slamming = true;
            st.SlamAt = Time.time;
            st.SlamCharge = charge;
            st.SlamDef = def;
            Sound("Item_1", 0.7f);
            return true;
        }

        void SlamLanding(Proj p, YoyoState st, Vector2 c)
        {
            var style = StyleOf(p.Kind);
            var def = st.SlamDef ?? ItemDb.Get(p.Item);
            float ch = st.SlamCharge;
            var keep = SpawnClass;
            SpawnClass = DamageClass.Melee;
            Pulse(style, c, 70f + 70f * ch, Mathf.RoundToInt(Equipment.Damage(def) * (1.5f + 1.5f * ch)), Equipment.Knockback(def) + 6f * ch, Equipment.Crit(def), 0, 0);
            SpawnClass = keep;
            // The ground's dust thrown along it both ways.
            for (int k = 0; k < 14; k++)
            {
                float side = k % 2 == 0 ? 1f : -1f;
                SwordFx.SpearMote(style.Art, c + new Vector2(side * Random.value * 20f, 6f), new Vector2(side * (2f + Random.value * 4f), -1.5f - Random.value * 2f), side);
            }
            Sound("Item_14", 0.5f);
            Player.TerraCamera.Punch(0.12f + 0.12f * ch, 0.25f);
            DevYoyoSlams++;
        }

        /// <summary>A whirl round the player: the yoyo swung round him on its string, two turns and a quarter, striking all about.</summary>
        bool Whirl(Proj p, YoyoState st, Vector2 owner, Vector2 c, float seconds)
        {
            st.TetherAt = Time.time;
            st.TetherFor = seconds;
            st.TetherSide = c.x < owner.x ? -1f : 1f;
            p.Immune.Clear();
            DevYoyoWhirls++;
            Sound("Item_7", 0.7f);
            return true;
        }

        /// <summary>The Cascade's: a flame jet at each of the three enemies nearest the yoyo (or three ways).</summary>
        bool CascadeVolley(Proj p, Vector2 c, int dmg, float kb, float crit)
        {
            var targets = new List<Vector2>();
            foreach (var hm in AccessoryEffects.Enemies(TerraParticles.ToWorld(c), 240f, accNear))
                if (Chaseable(hm) && targets.Count < 3) targets.Add(EnemyCenterPx(hm));
            for (int k = targets.Count; k < 3; k++) targets.Add(c + new Vector2(Mathf.Cos(k * 2.1f), Mathf.Sin(k * 2.1f)) * 120f);
            foreach (var at in targets)
            {
                var burst = Spawn(ProjKind.CascadeBurst, p.Pos, Flip(Norm(at - c) * 1.1f), dmg, kb, crit);
                if (burst == null) continue;
                burst.Ai1 = Mathf.Clamp(Vector2.Distance(at, c) / 200f + 0.15f, 0.4f, 1.5f);
                burst.Melee = true;
                DevYoyoBursts++;
            }
            Sound("Item_74", 0.5f);
            return true;
        }

        /// <summary>The Chik's: its crystals shattering out of it all round.</summary>
        bool CrystalBurst(Proj p, Vector2 c, int dmg, float kb, float crit)
        {
            for (int k = 0; k < 10; k++)
            {
                float a = k / 10f * Mathf.PI * 2f;
                var cr = Spawn(ProjKind.ChikCrystal, p.Pos, Flip(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 7f), Mathf.RoundToInt(dmg * 0.8f), kb, crit);
                if (cr == null) continue;
                cr.Ai0 = k != 0 ? 1f : 0f;
                cr.Melee = true;
                DevYoyoBursts++;
            }
            Sound("Item_27", 0.6f);
            return true;
        }

        /// <summary>The Eye's: three of its servants lunging from the yoyo, at the enemies nearest it or toward the cursor.</summary>
        bool EyeVolley(Proj p, Vector2 c, Vector2 owner, int dmg, float kb)
        {
            var targets = new List<Vector2>();
            foreach (var hm in AccessoryEffects.Enemies(TerraParticles.ToWorld(c), 220f, accNear))
                if (Chaseable(hm) && targets.Count < 3) targets.Add(EnemyCenterPx(hm));
            var way = Norm(c - owner);
            for (int k = targets.Count; k < 3; k++) targets.Add(c + RotateBy(way, (k - 1) * 0.5f) * 120f);
            foreach (var at in targets)
            {
                var lunge = Spawn(ProjKind.EyeLunge, p.Pos, Flip(Norm(at - c) * 14f), dmg * 2, kb, 0f);
                if (lunge == null) continue;
                lunge.Melee = true;
                DevYoyoBursts++;
            }
            Sound("Roar_0", 0.3f);
            return true;
        }

        /// <summary>The Kraken's: its waves in a fan out of the yoyo, away from the player (three, five at a full charge).</summary>
        bool WaveFan(Proj p, Vector2 c, Vector2 owner, int dmg, float kb, float crit, float charge)
        {
            int n = charge >= 0.99f ? 5 : 3;
            var way = Norm(c - owner);
            for (int k = 0; k < n; k++)
            {
                var wv = RotateBy(way, (k - (n - 1) * 0.5f) * 0.32f) * (20f + 6f * charge);
                var w = Spawn(ProjKind.KrakenWave, p.Pos, Flip(wv), Mathf.RoundToInt(dmg * (0.7f + 0.5f * charge)), kb, crit);
                if (w == null) continue;
                w.Melee = true;
                DevYoyoBursts++;
            }
            Sound("Item_84", 0.6f);
            return true;
        }

        /// <summary>The Terrarian's: a ring of its orbs flung out of the yoyo (eight, sixteen at a full charge) and a ring of its power.</summary>
        bool TerraBurst(Proj p, YoyoStyle style, Vector2 c, int dmg, float kb, float crit, float charge)
        {
            int n = 8 + Mathf.RoundToInt(8f * charge);
            for (int k = 0; k < n; k++)
            {
                float a = k / (float)n * Mathf.PI * 2f;
                var orb = Spawn(ProjKind.TerrarianOrb, p.Pos, Flip(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 9f), Mathf.RoundToInt(dmg * 0.6f), kb, crit);
                if (orb == null) continue;
                orb.Melee = true;
                DevYoyoBursts++;
            }
            Pulse(style, c, 70f + 50f * charge, Mathf.RoundToInt(dmg * (1f + charge)), kb, crit, 0, 0);
            Sound("Item_60", 0.6f);
            return true;
        }

        static void RegisterYoyoPulse()
        {
            specs[ProjKind.YoyoPulse] = new Spec
            {
                Ai = Ai.Custom, W = 16, H = 16, Time = 26, Penetrate = -1, OncePerNpc = true, TileCollide = false, Look = Look.Invisible,
                CanDamage = p => p.Ticks < 7,
                Update = (t, p) => t.StepYoyoPulse(p), Draw = (t, fx, p, px) => DrawYoyoPulse(fx, p, px), OnKill = (t, p) => pulseStyles.Remove(p),
                OnHit = (t, p, hm) => { if (p.Ai0 > 0f) EnemyDebuffs.Add(hm, (int)p.Ai0, Mathf.Max(60, (int)p.LocalAi1)); },
            };
        }
    }
}
