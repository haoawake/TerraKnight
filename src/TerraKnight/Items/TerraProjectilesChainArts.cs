using System.Collections.Generic;
using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The chained weapons remade (the user's 10-26 "继续升级剩下的这些…特效，动画，粒子效果，新颖，多样"): with the remade swings on
    /// (BladeArts.Enabled) each use is a path of its own, the next while the uses follow on (within a second): the Golem Fist
    /// 直拳 · 勾拳 (out to one side, hooking back across) · 上勾拳 (low, rising); the KO Cannon 刺拳 · 刺拳 (short and quick) · 直拳;
    /// the Chain Knife 直刺 · 斜撩 · 回旋刺 (a loop on its chain); the Chain Guillotines 上弧 · 下弧 (weaving S-curves); the Anchor
    /// 抛锚 · 低扫 (flat and long before it sinks) · 高抛 (lobbed); the Solar Eruption 平抽 · 圆环 · 双环 · 上撩 (its tip's loop flat, round,
    /// a figure eight, an arch). The heads reach 30% further (the Guillotines, the Anchor and the Eruption 15%: they reach far
    /// already). Behind each head afterimages and a ribbon of its colours; the Fist trails smoke, the glove red motes, the
    /// blades sparks, the Guillotines blood, the Anchor sea spray, the Eruption a ribbon of sunfire along its tip's path.
    /// The left button never moves the player. Their right-click arts (BladeSkillsFlail) are here too (ChainArt).
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        enum ChainPath : byte { Straight, Hook, Uppercut, Jab, Loop, WaveUp, WaveDown, Low, High, Flat, Ring, Eight, Rise }
        enum ChainArtKind : byte { None, Rocket, Haymaker, Whirl, Harvest, Drop, Wheel }

        sealed class ChainStyle
        {
            public string Name;
            public ChainPath Path;
        }

        static ChainStyle CS(string name, ChainPath path) => new ChainStyle { Name = name, Path = path };

        static readonly Dictionary<ProjKind, ChainStyle[]> chainCombos = new Dictionary<ProjKind, ChainStyle[]>
        {
            [ProjKind.GolemFistProj] = new[] { CS("直拳", ChainPath.Straight), CS("勾拳", ChainPath.Hook), CS("上勾拳", ChainPath.Uppercut) },
            [ProjKind.BoxingGlove] = new[] { CS("刺拳", ChainPath.Jab), CS("刺拳", ChainPath.Jab), CS("直拳", ChainPath.Straight) },
            [ProjKind.ChainKnifeProj] = new[] { CS("直刺", ChainPath.Straight), CS("斜撩", ChainPath.Uppercut), CS("回旋刺", ChainPath.Loop) },
            [ProjKind.ChainGuillotine] = new[] { CS("上弧", ChainPath.WaveUp), CS("下弧", ChainPath.WaveDown) },
            [ProjKind.AnchorProj] = new[] { CS("抛锚", ChainPath.Straight), CS("低扫", ChainPath.Low), CS("高抛", ChainPath.High) },
            [ProjKind.SolarEruptionProj] = new[] { CS("平抽", ChainPath.Flat), CS("圆环", ChainPath.Ring), CS("双环", ChainPath.Eight), CS("上撩", ChainPath.Rise) },
        };

        /// <summary>A chained weapon's head out with the remade swings: its path or art, where it is in it, what it leaves behind.</summary>
        sealed class ChainRun
        {
            public ChainStyle Style;
            public int T;
            public float Side = 1f, Reach = 1.3f, SinkAfter = 10f, Length = 30f;
            /// <summary>Its blows' share of the damage now, and when no pulse is going.</summary>
            public float Share = 1f, BaseShare = 1f;
            /// <summary>An art: which; one that carries the head round the player itself (Orbit); its length, charge, aim, offset.</summary>
            public ChainArtKind Art;
            public bool Orbit, Exploded, Landed;
            public int ArtLength;
            public float Charge, BaseAngle, Phase;
            /// <summary>A pulse going (updates left): a box round where it burst, every enemy in it once more at Share.</summary>
            public int Pulse;
            public Vector2 PulseAt, PulseSize;
            /// <summary>How far the cursor was (px), the curve's turn an update, the knife's loop (its update, its centre).</summary>
            public float AimDist = 200f, Turn;
            public int LoopT;
            public Vector2 LoopCentre;
            /// <summary>The head's (the Eruption's tip's) last places (Terraria px), newest last.</summary>
            public readonly List<Vector2> Trail = new List<Vector2>();
        }

        static readonly Dictionary<Proj, ChainRun> chainRuns = new Dictionary<Proj, ChainRun>();
        static readonly List<Proj> chainGone = new List<Proj>();
        static ProjKind chainLastKind;
        static int chainLastIndex = -1;
        static float chainLastEnd = -10f;

        /// <summary>The Solar Eruption's reach at its loop's far end (px): the game's flattened loop goes out about this far.</summary>
        const float EruptionReach = 458f;

        /// <summary>Dev: the last path begun, paths begun, arts, blasts; the farthest a head went (px) and the game's reach for it.</summary>
        public static string DevChainStyle = "", DevChainArt = "";
        public static int DevChainStyles, DevChainArts, DevChainBlasts;
        public static float DevChainFar, DevChainVanilla;

        static float ChainVanillaReach(ProjKind k) => k switch
        {
            ProjKind.GolemFistProj => 500f, ProjKind.BoxingGlove => 200f, ProjKind.ChainKnifeProj => 150f, ProjKind.ChainGuillotine => 525f,
            ProjKind.AnchorProj => 800f, _ => EruptionReach,
        };

        /// <summary>The head's run: made as it is first stepped with the remade swings on (its path the next in turn), else null.</summary>
        ChainRun ChainRunOf(Proj p)
        {
            if (chainRuns.TryGetValue(p, out var r)) return r;
            if (!BladeArts.Enabled || ghostHolder.HasValue || !chainCombos.TryGetValue(p.Kind, out var combo)) return null;
            if (chainRuns.Count > 16)
            {
                chainGone.Clear();
                foreach (var kv in chainRuns) if (kv.Key.Dead) chainGone.Add(kv.Key);
                foreach (var q in chainGone) chainRuns.Remove(q);
            }
            int next = p.Kind == chainLastKind && chainLastIndex >= 0 && Time.time - chainLastEnd < 1f ? (chainLastIndex + 1) % combo.Length : 0;
            chainLastKind = p.Kind;
            chainLastIndex = next;
            chainLastEnd = Time.time + 999f;
            var v = TV(p);
            float side = v.x < 0f ? -1f : 1f;
            r = new ChainRun { Style = combo[next], Side = side };
            // How far the cursor is (the curves meet it there).
            r.AimDist = Mathf.Clamp(Holder(out var holder) ? Vector2.Distance(AimPx(), holder) : 200f, 70f, ChainVanillaReach(p.Kind) * 1.2f);
            if (p.Kind == ProjKind.ChainGuillotine || p.Kind == ProjKind.AnchorProj || p.Kind == ProjKind.SolarEruptionProj) r.Reach = 1.15f;
            // Each path's start: turned off the aim, quicker, flatter, lobbed.
            float speed = Mathf.Max(1f, v.magnitude);
            switch (r.Style.Path)
            {
                // Off the aim by an angle, curving so as to cross it again at the cursor: the chord of a circle.
                case ChainPath.Hook: v = RotateBy(v, -0.55f * side) * 1.15f; r.Turn = 2f * Mathf.Sin(0.55f) * speed * 1.15f / r.AimDist; break;
                case ChainPath.Uppercut: v = RotateBy(v, 0.4f * side) * 1.15f; r.Turn = 2f * Mathf.Sin(0.4f) * speed * 1.15f / r.AimDist; break;
                case ChainPath.WaveUp: v = RotateBy(v, -WaveAngle * side); break;
                case ChainPath.WaveDown: v = RotateBy(v, WaveAngle * side); break;
                case ChainPath.Jab: v *= 1.25f; r.Reach *= 0.78f; break;
                case ChainPath.Low: v = new Vector2(v.x * 1.15f, v.y * 0.35f + 0.8f); r.SinkAfter = 18f; break;
                case ChainPath.High: v = RotateBy(v, -0.45f * side) * 1.05f; r.SinkAfter = 7f; break;
            }
            if (p.Kind != ProjKind.SolarEruptionProj) p.Vel = Flip(v);
            // The Eruption's round loop sweeps wide but stays on one enemy less: its blows heavier (measured: about two of its three hits a use land where the others land three).
            // The Eruption's shapes sweep further off the aim than the game's flat loop, on one enemy a little less often: their
            // blows and the solar blasts they set off heavier to match it (1.12; the round loop, off the aim the most, 1.55).
            if (p.Kind == ProjKind.SolarEruptionProj) r.Share = r.BaseShare = r.Style.Path == ChainPath.Ring ? 1.55f : 1.12f;
            chainRuns[p] = r;
            DevChainStyle = r.Style.Name;
            DevChainStyles++;
            DevChainVanilla = ChainVanillaReach(p.Kind);
            return r;
        }

        /// <summary>The head home (or gone): the next use goes on to the next path if it comes soon.</summary>
        static void ChainGone(Proj p)
        {
            if (!chainRuns.TryGetValue(p, out var r)) return;
            chainRuns.Remove(p);
            if (r.Art == ChainArtKind.None && p.Kind == chainLastKind) chainLastEnd = Time.time;
        }

        /// <summary>The Chain Knife's flourish: updates of its loop round where it turned back, the loop's radius (px).</summary>
        const int LoopUpdates = 14;
        const float LoopRadius = 28f;
        /// <summary>The Guillotines' weave: the most its heading swings off the aim (radians), how fast (radians an update).</summary>
        const float WaveAngle = 0.5f, WaveRate = 0.3f;

        /// <summary>
        /// The Chain Knife's 回旋刺 turning back: first a loop round where it turned (striking again what it struck), then home.
        /// True while it loops (handled here).
        /// </summary>
        bool ChainFlourish(Proj p, ChainRun r)
        {
            if (r.Style.Path != ChainPath.Loop || r.Art != ChainArtKind.None || r.LoopT > LoopUpdates) return false;
            var c = CenterPx(p);
            if (r.LoopT == 0)
            {
                var v0 = TV(p);
                var n = v0.sqrMagnitude > 1e-4f ? v0.normalized : new Vector2(r.Side, 0f);
                r.LoopCentre = c + new Vector2(-n.y, n.x) * LoopRadius * -r.Side;
                p.Immune.Clear();
                Sound("Item_1", 0.35f);
            }
            float a0 = Mathf.Atan2(c.y - r.LoopCentre.y, c.x - r.LoopCentre.x);
            float a = a0 - r.Side * Mathf.PI * 2f / LoopUpdates;
            var next = r.LoopCentre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * LoopRadius;
            r.LoopT++;
            p.Collide = false;
            p.Vel = Flip(next - c);
            p.TRot = Mathf.Atan2(next.y - c.y, next.x - c.x) + 1.57f;
            return true;
        }

        /// <summary>A head flying out: turned along its path (the hook back across, the uppercut up, the loop round, the S-curves).</summary>
        void ChainPathTick(Proj p, ChainRun r, ref Vector2 v)
        {
            r.T++;
            float side = r.Side;
            switch (r.Style.Path)
            {
                case ChainPath.Hook: v = RotateBy(v, r.Turn * side); break;
                case ChainPath.Uppercut: v = RotateBy(v, -r.Turn * side); break;
                // A weave either side of the aim: its heading swings from one side to the other and back.
                case ChainPath.WaveUp: v = RotateBy(v, WaveAngle * WaveRate * Mathf.Sin(r.T * WaveRate) * side); break;
                case ChainPath.WaveDown: v = RotateBy(v, -WaveAngle * WaveRate * Mathf.Sin(r.T * WaveRate) * side); break;
            }
            p.Vel = Flip(v);
        }

        /// <summary>Each update of a head: its pulse running down, its trail, the motes off it.</summary>
        static void ChainRunTick(Proj p, ChainRun r, Vector2 centre)
        {
            if (r.Pulse > 0 && --r.Pulse == 0) r.Share = r.BaseShare;
            var c = CenterPx(p);
            r.Trail.Add(c);
            if (r.Trail.Count > 10) r.Trail.RemoveAt(0);
            if (!r.Orbit && p.Ai0 == 0f) DevChainFar = Mathf.Max(DevChainFar, Vector2.Distance(c, centre));
            if (p.Alpha > 0.5f) return;
            int n = r.Trail.Count;
            if (n > 1 && Vector2.Distance(r.Trail[n - 1], r.Trail[n - 2]) > 3f)
                ChainMote(p.Kind, c + Random.insideUnitCircle * 5f, TV(p) * 0.12f, r.Art == ChainArtKind.Rocket ? 2 : 1);
        }

        /// <summary>One or more motes of the head's own (Terraria px).</summary>
        static void ChainMote(ProjKind k, Vector2 at, Vector2 push, int n)
        {
            if (TerraParticles.Instance == null) return;
            for (int i = 0; i < n; i++)
            {
                var jitter = new Vector2(Random.value - 0.5f, Random.value - 0.5f);
                int d;
                switch (k)
                {
                    case ProjKind.GolemFistProj:
                        d = PointDust(at, Random.value < 0.6f ? 31 : 6, -push * 0.4f + jitter, 110, 1.1f + Random.value * 0.5f);
                        if (d >= 0) { D(d).NoGravity = true; D(d).FadeIn = 1f; }
                        break;
                    case ProjKind.BoxingGlove:
                        d = PointDust(at, 278, -push * 0.3f + jitter, 60, 0.65f);
                        if (d >= 0) { D(d).NoGravity = true; D(d).Color = Random.value < 0.7f ? new Color(1f, 0.25f, 0.2f) : Color.white; }
                        break;
                    case ProjKind.ChainGuillotine:
                        if (Random.value < 0.35f) PointDust(at, 5, push * 0.3f + jitter, 0, 1f);
                        else if ((d = PointDust(at, 278, push * 0.4f + jitter * 1.5f, 60, 0.5f)) >= 0) D(d).Color = new Color(0.85f, 0.9f, 1f);
                        break;
                    case ProjKind.AnchorProj:
                        d = PointDust(at, 33, -push * 0.3f + jitter, 80, 1.1f);
                        break;
                    case ProjKind.SolarEruptionProj:
                        d = PointDust(at, 6, push * 0.3f + jitter * 1.4f, 100, 1.8f);
                        if (d >= 0) { D(d).NoGravity = true; D(d).FadeIn = 1.4f; }
                        break;
                    default:
                        // The Chain Knife's: steel sparks.
                        d = PointDust(at, 278, push * 0.4f + jitter * 1.5f + new Vector2(0f, -0.5f), 60, 0.5f);
                        if (d >= 0) D(d).Color = Color.Lerp(new Color(1f, 0.9f, 0.6f), Color.white, Random.value);
                        break;
                }
            }
        }

        static void ChainColours(ProjKind k, out Color main, out Color light)
        {
            switch (k)
            {
                case ProjKind.GolemFistProj: main = new Color(0.85f, 0.45f, 0.15f); light = new Color(1f, 0.8f, 0.45f); break;
                case ProjKind.BoxingGlove: main = new Color(0.9f, 0.2f, 0.15f); light = new Color(1f, 0.7f, 0.65f); break;
                case ProjKind.ChainGuillotine: main = new Color(0.7f, 0.12f, 0.15f); light = new Color(0.9f, 0.9f, 1f); break;
                case ProjKind.AnchorProj: main = new Color(0.25f, 0.55f, 0.85f); light = new Color(0.8f, 0.95f, 1f); break;
                case ProjKind.SolarEruptionProj: main = new Color(1f, 0.5f, 0.1f); light = new Color(1f, 0.92f, 0.5f); break;
                default: main = new Color(0.7f, 0.75f, 0.85f); light = Color.white; break;
            }
        }

        /// <summary>A blow of a remade head landing: its burst; the rocket goes off, the haymaker's stars and shake.</summary>
        void ChainRunHit(Proj p, HealthManager hm)
        {
            if (!chainRuns.TryGetValue(p, out var r)) return;
            var at = EnemyCenterPx(hm);
            float side = Holder(out var owner) && at.x < owner.x ? -1f : 1f;
            for (int k = 0; k < 5; k++) ChainMote(p.Kind, at, new Vector2(side * (1f + Random.value * 3f), (Random.value - 0.6f) * 4f) * 3f, 1);
            if (r.Art == ChainArtKind.Rocket && !r.Exploded) RocketBlast(p, r, CenterPx(p));
            if (r.Art == ChainArtKind.Drop && !r.Landed) AnchorSmash(p, r, CenterPx(p));
            if (r.Art == ChainArtKind.Haymaker)
            {
                for (int k = 0; k < 14; k++)
                {
                    float a = k / 14f * Mathf.PI * 2f;
                    int d = PointDust(at, k % 2 == 0 ? 15 : 57, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (2f + Random.value * 3f), 60, 1.3f);
                    if (d >= 0) D(d).NoGravity = true;
                }
                Player.TerraCamera.Punch(0.1f + 0.05f * r.Charge, 0.22f);
                Sound("Item_10", 0.7f);
            }
            if (p.Kind == ProjKind.ChainGuillotine) for (int k = 0; k < 6; k++) PointDust(at, 5, new Vector2(side * Random.value * 3f, -Random.value * 3f), 0, 1.2f);
        }

        /// <summary>The rocket fist meeting a block goes off there too (false: the head's own tile handling as well).</summary>
        bool ChainRunTile(Proj p)
        {
            if (chainRuns.TryGetValue(p, out var r) && r.Art == ChainArtKind.Rocket && !r.Exploded) RocketBlast(p, r, CenterPx(p));
            return false;
        }

        /// <summary>The Golem Fist's rocket going off: a blast round it, the ground cracked, smoke and fire; then home.</summary>
        void RocketBlast(Proj p, ChainRun r, Vector2 at)
        {
            r.Exploded = true;
            StartPulse(p, r, at, new Vector2(150f + 50f * r.Charge, 110f + 30f * r.Charge), 3f + 2f * r.Charge);
            r.BaseShare = 1f;
            SurfaceDebris(at, 70f);
            Player.TerraCamera.Punch(0.14f + 0.05f * r.Charge, 0.28f);
            Sound("Item_14", 0.8f);
            for (int k = 0; k < 30; k++)
            {
                float a = k / 30f * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                int d = PointDust(at + dir * 6f, k % 3 == 0 ? 31 : 6, dir * (2.5f + Random.value * 4f), 100, 1.6f + Random.value);
                if (d >= 0) { D(d).NoGravity = true; D(d).FadeIn = 1.4f; }
            }
            p.Ai0 = 1f;
            p.LocalAi0 = -1f;
            DevChainBlasts++;
        }

        static void StartPulse(Proj p, ChainRun r, Vector2 at, Vector2 size, float share)
        {
            r.Pulse = 3;
            r.PulseAt = at;
            r.PulseSize = size;
            r.Share = share;
            p.Immune.Clear();
        }

        /// <summary>The box a head strikes in: a pulse's, else its own (bigger whirled round the player).</summary>
        static (Vector2, Vector2) ChainArea(Proj p)
        {
            chainRuns.TryGetValue(p, out var r);
            if (r != null && r.Pulse > 0) return (TerraParticles.ToWorld(r.PulseAt), r.PulseSize * Px);
            var size = p.HitSize != Vector2.zero ? p.HitSize : new Vector2((int)(p.Spec.W * p.Spec.Scale), (int)(p.Spec.H * p.Spec.Scale));
            if (r != null && r.Orbit) size *= 1.4f;
            return (p.Pos, size * Px);
        }

        static int ChainDamage(Proj p, int dmg) => chainRuns.TryGetValue(p, out var r) && r.Share != 1f ? Mathf.Max(1, Mathf.RoundToInt(dmg * r.Share)) : dmg;

        /// <summary>Under the head: a ribbon of its colours through its last places, afterimages when it goes fast.</summary>
        static void DrawChainRun(FxBatch fx, Proj p, Vector2 px)
        {
            if (!chainRuns.TryGetValue(p, out var r) || p.Alpha > 0.5f) return;
            int n = r.Trail.Count;
            if (n < 3) return;
            ChainColours(p.Kind, out var main, out var light);
            bool eruption = p.Kind == ProjKind.SolarEruptionProj;
            for (int k = 1; k < n; k++)
            {
                var a = r.Trail[k - 1];
                var b = r.Trail[k];
                if ((b - a).sqrMagnitude < 4f) continue;
                float f = k / (float)(n - 1);
                StreakPx(fx, a, b, Color.Lerp(main, light, f), (eruption ? 8f : 3f) + (eruption ? 14f : 8f) * f, (eruption ? 0.6f : 0.45f) * f);
            }
            if (eruption || Vector2.Distance(r.Trail[n - 1], r.Trail[n - 2]) < 6f) return;
            var whole = FxBatch.Whole(p.Spec.Texture);
            if (whole == null) return;
            for (int k = 1; k <= 3 && n - 1 - k >= 0; k++)
            {
                float age = k / 4f;
                var c = Color.Lerp(light, main, age) * (0.3f * (1f - age));
                c.a = 0f;
                fx.Draw(whole, r.Trail[n - 1 - k], c, p.TRot, p.Scale, flipX: p.Dir < 0f && (p.Kind == ProjKind.GolemFistProj || p.Kind == ProjKind.BoxingGlove));
            }
        }

        // ------------------------------------------------------------------ the Anchor

        /// <summary>The Anchor's drop (an art): straight down until it lands (true: handled here).</summary>
        bool AnchorRunStep(Proj p, ChainRun r, ref Vector2 v)
        {
            r.T++;
            if (r.Art != ChainArtKind.Drop || r.Landed) return false;
            v = new Vector2(0f, Mathf.Min(20f, v.y + 0.9f));
            p.Vel = Flip(v);
            p.TRot = Mathf.PI;
            return true;
        }

        /// <summary>Each update of the Anchor, out and back: its pulse, trail, spray.</summary>
        static void AnchorRunTick(Proj p, ChainRun r, Vector2 holder) => ChainRunTick(p, r, holder);

        /// <summary>The dropped Anchor landing: the ground smashed wide, the sea thrown up; then home (true: handled here).</summary>
        bool AnchorRunTile(Proj p, ChainRun r)
        {
            if (r.Art != ChainArtKind.Drop || r.Landed) return false;
            AnchorSmash(p, r, CenterPx(p) + new Vector2(0f, 6f));
            return true;
        }

        /// <summary>
        /// The dropped Anchor smashing down where it lands, on the ground or on an enemy's head (a boomerang turns back off
        /// what it strikes, so it goes off there): a blast round it, the ground's debris, the sea thrown up; then home.
        /// </summary>
        void AnchorSmash(Proj p, ChainRun r, Vector2 c)
        {
            r.Landed = true;
            StartPulse(p, r, c, new Vector2(220f + 60f * r.Charge, 120f), 2.5f + 2f * r.Charge);
            r.BaseShare = 1f;
            SurfaceDebris(c, 150f);
            Player.TerraCamera.Punch(0.16f + 0.06f * r.Charge, 0.3f);
            Sound("Item_14", 0.8f);
            for (int k = 0; k < 36; k++)
            {
                float side = k % 2 == 0 ? 1f : -1f;
                int d = PointDust(c + new Vector2(side * Random.value * 60f, 0f), k % 4 == 0 ? 31 : 33, new Vector2(side * (1f + Random.value * 4f), -2f - Random.value * 6f), 60, 1.4f);
                if (d >= 0 && k % 4 == 0) D(d).NoGravity = true;
            }
            p.Returning = true;
            DevChainBlasts++;
        }

        // ------------------------------------------------------------------ the Solar Eruption

        /// <summary>The Eruption's tip off the hand (px) at s of its length: the use's shape, or the art's wheel, turned to the aim.</summary>
        static Vector2 EruptionShape(ChainRun r, float s, float aim)
        {
            float L = EruptionReach * r.Reach;
            float side = r.Side;
            Vector2 q;
            if (r.Art == ChainArtKind.Wheel)
            {
                // Round the player twice, out and in at the ends.
                float ramp = Mathf.Min(1f, s * 5f, (1f - s) * 5f);
                float ang = r.BaseAngle + side * Mathf.PI * 4f * s;
                return Below(new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * L * 0.5f * Smooth01(ramp));
            }
            float x = L * (1f - Mathf.Cos(2f * Mathf.PI * s)) * 0.5f;
            switch (r.Style.Path)
            {
                case ChainPath.Ring: q = new Vector2(x, side * L * 0.22f * Mathf.Sin(2f * Mathf.PI * s)); break;
                case ChainPath.Eight: q = new Vector2(x, L * 0.12f * Mathf.Sin(4f * Mathf.PI * s)); break;
                // Out under the aim and back over it: rising, whichever way he faces.
                case ChainPath.Rise: q = new Vector2(x, side * L * 0.15f * Mathf.Sin(2f * Mathf.PI * s)); break;
                // Out over the aim and back under it: coming down.
                default: q = new Vector2(x, -side * L * 0.1f * Mathf.Sin(2f * Mathf.PI * s)); break;
            }
            // In the aim's frame (px, y down).
            return RotateBy(q, aim);
        }

        /// <summary>A tick of the Eruption's tip: its trail, the far reach for the dev.</summary>
        void EruptionRunTick(Proj p, ChainRun r, Vector2 v)
        {
            if (r.Pulse > 0 && --r.Pulse == 0) r.Share = r.BaseShare;
            var hand = CenterPx(p);
            r.Trail.Add(hand + v);
            if (r.Trail.Count > 12) r.Trail.RemoveAt(0);
            if (!r.Orbit) DevChainFar = Mathf.Max(DevChainFar, v.magnitude);
        }

        // ------------------------------------------------------------------ the right-click arts (BladeSkillsFlail)

        /// <summary>
        /// A chained weapon's right-click art toward the cursor (the head out put away first); charge (0..1) for the charged
        /// ones. False when it can't go.
        /// </summary>
        public bool ChainArt(ItemDef def, HeroController hero, string name, float charge)
        {
            if (def == null || hero == null || !Holder(out var centre)) return false;
            var kind = def.Shoot;
            if (!chainCombos.ContainsKey(kind)) return false;
            foreach (var q in live) if (!q.Dead && q.Kind == kind) Kill(q);
            var to = AimPx() - centre;
            if (to.sqrMagnitude < 1f) to = new Vector2(hero.cState.facingRight ? 1f : -1f, 0f);
            var dir = to.normalized;
            float side = dir.x < 0f ? -1f : 1f;
            int dmg = Equipment.Damage(def);
            float kb = Equipment.Knockback(def), crit = Equipment.Crit(def);
            charge = Mathf.Clamp01(charge);
            var made = new List<Proj>();
            ChainRun Run(Proj p, ChainArtKind art)
            {
                var r = new ChainRun { Style = chainCombos[kind][0], Art = art, Side = side, Charge = charge, BaseAngle = Mathf.Atan2(dir.y, dir.x) };
                if (kind == ProjKind.ChainGuillotine || kind == ProjKind.AnchorProj || kind == ProjKind.SolarEruptionProj) r.Reach = 1.15f;
                chainRuns[p] = r;
                made.Add(p);
                return r;
            }
            switch (name)
            {
                case "火箭重拳":
                {
                    var p = MeleeChild(ProjKind.GolemFistProj, centre, dir * 22f, dmg, kb * 1.5f, crit);
                    if (p == null) return false;
                    var r = Run(p, ChainArtKind.Rocket);
                    r.Reach = 1.8f;
                    r.Share = r.BaseShare = 1.5f;
                    p.Scale *= 1.15f;
                    // Its own cooldown on each enemy (not the enemy's shared one), so its blast strikes what the fist struck.
                    p.Cooldown = 10;
                    break;
                }
                case "致胜重拳":
                {
                    var p = MeleeChild(ProjKind.BoxingGlove, centre, dir * 17f, dmg, kb * 3f, crit);
                    if (p == null) return false;
                    var r = Run(p, ChainArtKind.Haymaker);
                    r.Reach = 1.7f;
                    r.Share = r.BaseShare = 3f + 3f * charge;
                    p.Cooldown = 10;
                    p.Scale = p.Spec.Scale * 2.3f;
                    p.HitSize = new Vector2(20f, 20f) * 1.2f * 2.4f;
                    break;
                }
                case "链刃旋风":
                {
                    var p = MeleeChild(ProjKind.ChainKnifeProj, centre, dir * 12f, dmg, kb, crit);
                    if (p == null) return false;
                    var r = Run(p, ChainArtKind.Whirl);
                    r.Orbit = true;
                    r.ArtLength = 60;
                    p.Cooldown = 8;
                    break;
                }
                case "血滴子收割":
                    for (int k = 0; k < 2; k++)
                    {
                        var p = MeleeChild(ProjKind.ChainGuillotine, centre, dir * 9f, dmg, kb, crit);
                        if (p == null) continue;
                        var r = Run(p, ChainArtKind.Harvest);
                        r.Orbit = true;
                        r.ArtLength = 120;
                        r.Phase = k * Mathf.PI;
                        p.Cooldown = 16;
                    }
                    if (made.Count == 0) return false;
                    break;
                case "落锚":
                {
                    var t = to.magnitude > 360f ? dir * 360f : to;
                    var land = GroundUnder(centre + t, 260f);
                    // Dropped from as high over it as the room lets (up to 250 px).
                    var top = land - new Vector2(0f, 40f);
                    for (float dy = 48f; dy <= 250f; dy += 8f)
                    {
                        var q = land - new Vector2(0f, dy);
                        if (TerraWorld.IsSolidAt(TerraParticles.ToWorld(q))) break;
                        top = q;
                    }
                    var p = MeleeChild(ProjKind.AnchorProj, top, new Vector2(0f, 10f), dmg, kb * 1.5f, crit);
                    if (p == null) return false;
                    var r = Run(p, ChainArtKind.Drop);
                    r.Share = r.BaseShare = 1.5f;
                    p.Cooldown = 10;
                    p.Ai1 = 10f;
                    break;
                }
                case "日轮":
                {
                    var p = MeleeChild(ProjKind.SolarEruptionProj, centre, dir * 24f, dmg, kb, crit);
                    if (p == null) return false;
                    var r = Run(p, ChainArtKind.Wheel);
                    r.Orbit = true;
                    r.Length = 60f;
                    r.Share = r.BaseShare = 0.8f;
                    break;
                }
                default:
                    return false;
            }
            DevChainArts++;
            DevChainArt = name;
            Sound("Item_1", 0.6f);
            return true;
        }

        /// <summary>A whirled head's tick: round the player on its chain, out and in at the ends (the Chain Knife twice, the Guillotines one and a half times, each opposite the other).</summary>
        void StepChainArt(Proj p, ChainRun r, Vector2 centre)
        {
            int t = r.T++;
            p.Collide = false;
            p.Alpha = Mathf.Max(0f, p.Alpha - 0.25f);
            if (p.Kind == ProjKind.ChainGuillotine) p.Extra = 1;
            float s = t / (float)r.ArtLength;
            float ramp = Smooth01(Mathf.Min(1f, s * 6f, (1f - s) * 6f));
            bool knife = r.Art == ChainArtKind.Whirl;
            // Out and in twice over the art, so the whole band round him is swept.
            float band = 0.4f + 0.6f * Mathf.Abs(Mathf.Sin(Mathf.PI * 2f * s));
            float radius = (knife ? 156f : 170f) * band * Mathf.Max(0.15f, ramp);
            float ang = r.BaseAngle + r.Phase + r.Side * Mathf.PI * 2f * (knife ? 2f : 1.5f) * s;
            var c = centre + Below(new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius);
            p.Pos = TerraParticles.ToWorld(c);
            p.Vel = Vector2.zero;
            p.TRot = ang + Mathf.PI / 2f * r.Side;
            TerraMod.Instance?.KeepUsing(ItemDb.Get(ChainItem(p.Kind)), Flip(c - centre));
            if (t >= r.ArtLength) Kill(p);
        }
    }
}
