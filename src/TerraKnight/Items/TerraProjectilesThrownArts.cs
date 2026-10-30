using System.Collections.Generic;
using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The melee thrown weapons remade (the user's 10-26 "继续升级剩下的这些…依旧是原来的要求，特效，动画，粒子效果，新颖，多样",
    /// after the spears' "可以多几种枪法，也有一些右键特殊能力，也有更好的粒子特效…范围也搞大一点"), with the remade swings on
    /// (BladeArts.Enabled). Each throw of a boomerang flies a path of its own, in turn for its weapon (Fly: straight, an arc
    /// to one side, a loop round the cursor, a lob coming down on it, a corkscrew, a zigzag that strikes sparks at its
    /// corners), 30% further and home faster; it follows its path by its velocity, so walls still stop it as Terraria's,
    /// and a wall knocking it off its path leaves it to Terraria's flight. The knives and javelins vary their throws
    /// (TerraProjectilesThrown2.cs, TerraProjectilesJavelins.cs: KnifeFlight). Every one leaves afterimages of its own
    /// sprite, a ribbon of its colour and motes of its theme, and is caught with a flourish. The right-click arts
    /// (BladeSkillsThrown.cs) throw through ThrowArt: free of the one-out rule, on their own flights (power, hover, orbit
    /// round the player, dive from above, ricochet from enemy to enemy). Off: Terraria's throws.
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        internal enum Fly { Vanilla, Straight, Arc, Loop, Lob, Corkscrew, Zigzag, Power, Hover, Orbit, Dive, Chain }

        /// <summary>What an art's throw does on its way (Each), halfway (Mid) or at its end (End).</summary>
        internal enum ArtFx { None, IceBurst, AxeImpact, HolyImpact, Split, Mushrooms }

        /// <summary>One throw's flight (world units, y up).</summary>
        internal sealed class Flight
        {
            public Fly Path;
            public float Side = 1f;
            public Vector2 From, Dir, Centre;
            /// <summary>How far it goes (Straight, Power); where a curved path (Arc, Corkscrew, Zigzag, Loop) comes onto the aim's line (Aim);
            /// the bow's or wave's width, the loop's radius, the lob's height, the hover's circle, the orbit's radius (Lift).</summary>
            public float Reach, Aim, Lift;
            public int T, OutT;
            public bool Art, Broken, Caught, Ended, Homed;
            public float ReturnBoost = 1f;
            public Vector2 Expected;
            /// <summary>The orbit's starting angle (radians); the hover's travel share of its time.</summary>
            public float Phase, Travel = 0.25f;
            /// <summary>Ricochet: the enemy it is going for, those it has struck, how many more it may go for.</summary>
            public HealthManager Target;
            public readonly HashSet<HealthManager> Struck = new HashSet<HealthManager>();
            public int ChainLeft;
            /// <summary>What it does at its path's end (a burst, an impact), halfway (a split), each update (mushrooms dropped); how strong (an art's charge).</summary>
            public ArtFx End, Mid, Each;
            public float FxPower;
            public bool MidDone;
            /// <summary>The weapon it was thrown from (a split's new throws); its drawn size, when not its own.</summary>
            public ItemDef Def;
            public float Scale;
            /// <summary>Its places and turns as drawn (newest last), for its afterimages and ribbon.</summary>
            public readonly List<Vector2> Places = new List<Vector2>();
            public readonly List<float> Turns = new List<float>();
            public ThrowLook Look;
            /// <summary>Knives and javelins (KnifeFlight): their way turned Curve (radians) an update for CurveFor updates; gravity added
            /// each update; a tumble (the knife turning end over end as it flies).</summary>
            public float Curve, Gravity;
            public int CurveFor;
            public bool Tumble;
            /// <summary>An art's knife: turned this many degrees off the aim, curving back onto the cursor (CurveOnto).</summary>
            public float ConvergeTurn;
            public string Move = "";
            /// <summary>Its reach against the throw's own (an art's farther or shorter throw).</summary>
            public float ReachScale = 1f;
        }

        /// <summary>A thrown weapon's look: its colours, its theme's dusts, how many afterimages.</summary>
        internal sealed class ThrowLook
        {
            public Color Main, Light;
            public int Dust = -1, Dust2 = -1;
            public float Rate = 0.5f, DustScale = 1f;
            public int Images = 5;
            public float Ribbon = 1f;
        }

        static ThrowLook TL(Color main, Color light, int dust, int dust2 = -1, float rate = 0.5f, float scale = 1f, int images = 5, float ribbon = 1f) =>
            new ThrowLook { Main = main, Light = light, Dust = dust, Dust2 = dust2, Rate = rate, DustScale = scale, Images = images, Ribbon = ribbon };

        /// <summary>Each thrown weapon's look, by what it throws (colours from its sprite, dusts from Terraria's own for it).</summary>
        static readonly Dictionary<ProjKind, ThrowLook> throwLooks = new Dictionary<ProjKind, ThrowLook>
        {
            [ProjKind.Boomerang] = TL(new Color(0.35f, 0.55f, 1f), new Color(0.85f, 0.9f, 1f), 15, 58, 0.5f),
            [ProjKind.WoodenBoomerang] = TL(new Color(0.75f, 0.55f, 0.3f), new Color(1f, 0.9f, 0.7f), 7, -1, 0.25f, 0.9f, 4, 0.7f),
            [ProjKind.Flamarang] = TL(new Color(1f, 0.45f, 0.1f), new Color(1f, 0.85f, 0.4f), 6, 6, 1.2f, 1.6f, 5),
            [ProjKind.ThornChakram] = TL(new Color(0.35f, 0.8f, 0.25f), new Color(0.8f, 1f, 0.6f), 40, 2, 0.6f, 1.1f, 5),
            [ProjKind.IceBoomerangProj] = TL(new Color(0.45f, 0.8f, 1f), new Color(0.9f, 0.97f, 1f), 135, 185, 0.7f, 1.2f, 5),
            [ProjKind.FruitcakeChakramProj] = TL(new Color(0.95f, 0.4f, 0.4f), new Color(1f, 0.95f, 0.8f), 219, 221, 0.6f, 0.9f, 5),
            [ProjKind.BloodyMacheteProj] = TL(new Color(0.85f, 0.1f, 0.12f), new Color(1f, 0.55f, 0.5f), 5, -1, 0.5f, 1.1f, 5),
            [ProjKind.ShroomerangProj] = TL(new Color(0.25f, 0.45f, 1f), new Color(0.6f, 0.85f, 1f), 41, -1, 0.6f, 1f, 5),
            [ProjKind.CombatWrenchProj] = TL(new Color(0.7f, 0.75f, 0.85f), new Color(1f, 0.85f, 0.45f), 228, -1, 0.3f, 0.8f, 3, 0.8f),
            [ProjKind.TrimarangProj] = TL(new Color(0.9f, 0.75f, 0.35f), new Color(1f, 0.95f, 0.7f), 57, -1, 0.35f, 0.9f, 4),
            [ProjKind.AxearangProj] = TL(new Color(0.7f, 0.6f, 0.5f), new Color(1f, 0.9f, 0.75f), 7, 1, 0.3f, 1f, 4, 0.8f),
            [ProjKind.BouncingShieldProj] = TL(new Color(0.85f, 0.65f, 0.25f), new Color(1f, 0.95f, 0.7f), 228, -1, 0.3f, 0.9f, 4),
            [ProjKind.LightDisc] = TL(new Color(0.25f, 0.5f, 1f), new Color(0.75f, 0.9f, 1f), 226, -1, 0.5f, 0.7f, 6),
            [ProjKind.Bananarang] = TL(new Color(1f, 0.85f, 0.2f), new Color(1f, 1f, 0.7f), 64, -1, 0.3f, 0.9f, 4),
            [ProjKind.PossessedHatchet] = TL(new Color(0.75f, 0.65f, 0.35f), new Color(1f, 0.95f, 0.6f), 57, -1, 0.4f, 0.9f, 5),
            [ProjKind.PaladinsHammer] = TL(new Color(1f, 0.85f, 0.35f), new Color(1f, 1f, 0.85f), 57, 204, 0.6f, 1.1f, 6, 1.3f),
            [ProjKind.VampireKnife] = TL(new Color(0.85f, 0.08f, 0.15f), new Color(1f, 0.5f, 0.55f), 183, -1, 0.2f, 0.8f, 3, 0.6f),
            [ProjKind.ShadowFlameKnifeProj] = TL(new Color(0.6f, 0.2f, 0.95f), new Color(0.9f, 0.7f, 1f), 27, -1, 0.3f, 1f, 4),
            [ProjKind.FlyingKnifeProj] = TL(new Color(0.55f, 0.75f, 1f), new Color(1f, 1f, 1f), 15, 57, 0.4f, 0.8f, 5),
            [ProjKind.Daybreak] = TL(new Color(1f, 0.6f, 0.15f), new Color(1f, 0.95f, 0.6f), 6, 259, 0.6f, 1.2f, 5, 1.2f),
            [ProjKind.ScourgeJavelin] = TL(new Color(0.55f, 0.45f, 0.75f), new Color(0.85f, 0.8f, 1f), 184, 14, 0.4f, 1f, 4),
        };

        /// <summary>Each boomerang's throws in turn (a left button's): the paths that suit it.</summary>
        static readonly Dictionary<ProjKind, Fly[]> throwCycles = new Dictionary<ProjKind, Fly[]>
        {
            [ProjKind.Boomerang] = new[] { Fly.Straight, Fly.Arc, Fly.Loop },
            [ProjKind.WoodenBoomerang] = new[] { Fly.Straight, Fly.Arc, Fly.Lob },
            [ProjKind.Flamarang] = new[] { Fly.Corkscrew, Fly.Arc, Fly.Loop },
            // Thrown every quarter second: its curves between straight throws, so the next isn't kept waiting.
            [ProjKind.ThornChakram] = new[] { Fly.Straight, Fly.Zigzag, Fly.Straight, Fly.Arc },
            [ProjKind.IceBoomerangProj] = new[] { Fly.Arc, Fly.Lob, Fly.Straight },
            [ProjKind.FruitcakeChakramProj] = new[] { Fly.Loop, Fly.Corkscrew, Fly.Arc },
            [ProjKind.ShroomerangProj] = new[] { Fly.Lob, Fly.Arc, Fly.Loop },
            [ProjKind.CombatWrenchProj] = new[] { Fly.Straight, Fly.Zigzag, Fly.Arc },
            [ProjKind.TrimarangProj] = new[] { Fly.Arc, Fly.Straight, Fly.Arc, Fly.Corkscrew },
            [ProjKind.AxearangProj] = new[] { Fly.Lob, Fly.Straight, Fly.Arc },
            [ProjKind.LightDisc] = new[] { Fly.Zigzag, Fly.Straight, Fly.Arc },
            [ProjKind.Bananarang] = new[] { Fly.Arc, Fly.Corkscrew, Fly.Arc, Fly.Loop },
        };

        /// <summary>A boomerang's throws in turn for the wiki (WeaponWikiLines), by its projectile; null for none.</summary>
        internal static string[] WikiThrows(ProjKind k) => throwCycles.TryGetValue(k, out var s) ? System.Array.ConvertAll(s, FlyName) : null;

        internal static string FlyName(Fly f) => f switch
        {
            Fly.Straight => "疾投", Fly.Arc => "弧旋", Fly.Loop => "回环", Fly.Lob => "高抛", Fly.Corkscrew => "螺旋", Fly.Zigzag => "折射",
            Fly.Power => "蓄力投", Fly.Hover => "旋舞", Fly.Orbit => "护环", Fly.Dive => "天降", Fly.Chain => "弹射", _ => "原版",
        };

        static readonly Dictionary<Proj, Flight> flights = new Dictionary<Proj, Flight>();
        static readonly Dictionary<ProjKind, int> cycleAt = new Dictionary<ProjKind, int>();
        static readonly Dictionary<ProjKind, float> cycleTime = new Dictionary<ProjKind, float>();
        /// <summary>An art's projectile being made: the one-out rule doesn't hold it back.</summary>
        static bool thrownArtSpawn;

        /// <summary>Dev (suite thrownarts): the paths flown, the last; ricochet sparks; catches; the farthest a throw went (px).</summary>
        public static int DevThrowPaths, DevSparks, DevCatches, DevArtThrows;
        public static string DevLastPath = "";
        public static float DevThrowFar;
        /// <summary>Dev: each finished throw: its path, knocked off it (断), updates out of its time.</summary>
        public static readonly List<string> DevFlightLog = new List<string>();
        public static bool DevTrackVanilla;

        internal static bool IsThrownKind(ProjKind k) => throwLooks.ContainsKey(k);

        /// <summary>Dev: where each live projectile of a kind is (world).</summary>
        public IEnumerable<Vector2> DevAll(ProjKind kind)
        {
            foreach (var q in live) if (q.Kind == kind && !q.Dead) yield return q.Pos;
        }

        /// <summary>An art's throw, not counted against how many of its kind may be out (Holds).</summary>
        static bool IsArtThrow(Proj q) => flights.TryGetValue(q, out var f) && f.Art;

        static Vector2 HeroCentre(HeroController hero) => hero != null ? (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center : Vector2.zero;

        static Vector2 AimWorld()
        {
            var mod = TerraMod.Instance;
            return mod == null ? Vector2.zero : mod.DevAim ?? mod.AimPoint;
        }

        /// <summary>A left button's throw of a remade weapon: its flight, the next in its weapon's turn (null: none, the remade swings off).</summary>
        Flight NewFlight(Proj p)
        {
            if (!throwLooks.TryGetValue(p.Kind, out var look)) return null;
            if (!BladeArts.Enabled)
            {
                if (!DevTrackVanilla) return null;
                // Dev: Terraria's throw watched (its time), nothing of its own.
                var dev = new Flight { Path = Fly.Vanilla, Move = "原版", From = p.Pos };
                flights[p] = dev;
                return dev;
            }
            var f = new Flight { Look = look, From = p.Pos, Dir = p.Vel.sqrMagnitude > 1e-6f ? p.Vel.normalized : Vector2.right };
            if (throwCycles.TryGetValue(p.Kind, out var cycle))
            {
                int n = cycleAt.TryGetValue(p.Kind, out int c) && cycleTime.TryGetValue(p.Kind, out float t) && Time.time - t < 3f ? c : 0;
                cycleAt[p.Kind] = n + 1;
                cycleTime[p.Kind] = Time.time;
                f.Path = cycle[n % cycle.Length];
                f.Side = n / cycle.Length % 2 == 0 ? (n % 2 == 0 ? 1f : -1f) : (n % 2 == 0 ? -1f : 1f);
                ShapeFlight(p, f, 1f);
            }
            else f.Path = Fly.Vanilla;
            f.Move = FlyName(f.Path);
            flights[p] = f;
            return f;
        }

        /// <summary>A flight's reach, timing and shape from its throw (reachScale × 1.3 of Terraria's straight throw).</summary>
        void ShapeFlight(Proj p, Flight f, float reachScale)
        {
            var s = p.Spec;
            float speed = p.LaunchSpeed > 0.01f ? p.LaunchSpeed : p.Vel.magnitude;
            int outT = Mathf.Max(10, s.OutTicks);
            float reach = speed * outT * Px * 1.3f * reachScale * f.ReachScale;
            if (f.Reach <= 0f) f.Reach = reach;
            // Where a curved path comes onto the aim's line: the cursor, within the throw's reach (no nearer than 2½ tiles).
            float toAim = Vector2.Distance(AimWorld(), f.From);
            float l = Mathf.Clamp(toAim, Mathf.Min(f.Reach * 0.3f, 80f * Px), f.Reach);
            bool timed = true;
            // A throw near level: its bow, hops and loop on the sky's side of the line (the floor under it would stop it).
            if (!f.Art && Mathf.Abs(f.Dir.y) < 0.75f && f.Path != Fly.Lob) f.Side = f.Dir.x >= 0f ? 1f : -1f;
            switch (f.Path)
            {
                // Straight: Terraria's throw at its speed, 30% longer out (so 30% further; it strikes when Terraria's would).
                case Fly.Straight: f.OutT = Mathf.RoundToInt(outT * 1.3f); timed = false; break;
                case Fly.Arc: f.Aim = l; if (f.Lift <= 0f) f.Lift = l * 0.3f; break;
                case Fly.Corkscrew: f.Aim = l; if (f.Lift <= 0f) f.Lift = Mathf.Max(l * 0.12f, 18f * Px); break;
                case Fly.Zigzag: f.Aim = l; if (f.Lift <= 0f) f.Lift = l * 0.16f; break;
                case Fly.Loop:
                    f.Aim = l;
                    if (f.Lift <= 0f) f.Lift = Mathf.Clamp(l * 0.3f, 36f * Px, 80f * Px);
                    // A loop-the-loop on the cursor: out along the line to it, once round a circle sitting on the line there
                    // (on its Side), and on through it again.
                    f.Centre = f.From + f.Dir * l + new Vector2(-f.Dir.y, f.Dir.x) * f.Side * f.Lift;
                    break;
                case Fly.Lob:
                    if (f.Centre == default) f.Centre = f.From + f.Dir * l;
                    if (f.Lift <= 0f) f.Lift = Vector2.Distance(f.Centre, f.From) * 0.45f;
                    break;
                case Fly.Power: if (f.OutT <= 0) f.OutT = Mathf.RoundToInt(outT * 1.3f); timed = false; break;
                default: if (f.OutT <= 0) f.OutT = outT; timed = false; break;
            }
            // The time out: it comes onto the cursor when Terraria's straight throw would reach it (its curve flown that much
            // faster), so it strikes and is back as Terraria's and the next throw follows as quickly; a loop strikes as it
            // first passes through the cursor, its share of the time (by length) before the circle.
            if (timed && f.OutT <= 0)
            {
                float strike = l / (speed * Px);
                float share = f.Path == Fly.Loop ? f.Aim / (f.Aim + 2f * Mathf.PI * f.Lift) : 1f;
                f.OutT = Mathf.Max(8, Mathf.CeilToInt(strike / share));
            }
        }

        static float EaseOut(float u) => 1f - (1f - u) * (1f - u);

        /// <summary>Triangle wave: 0 at 0, +1 at ½, −1 at 1½, +1 at 2½, 0 at 3.</summary>
        static float Tri(float x)
        {
            float ph = Mathf.Repeat(x + 0.5f, 2f);
            return ph < 1f ? 2f * ph - 1f : 3f - 2f * ph;
        }

        /// <summary>Where the path puts it at u (0..1 of its time out), world.</summary>
        static Vector2 PathAt(Flight f, float u, HeroController hero)
        {
            var d = f.Dir;
            var n = new Vector2(-d.y, d.x) * f.Side;
            switch (f.Path)
            {
                case Fly.Arc: return f.From + d * f.Aim * u + n * f.Lift * Mathf.Sin(Mathf.PI * u);
                // Three smooth turns of a spiral seen side on (drawn swelling and shrinking with them), on one side of the line.
                case Fly.Corkscrew: return f.From + d * f.Aim * u + n * f.Lift * 0.5f * (1f - Mathf.Cos(6f * Mathf.PI * u));
                // Three sharp hops off the line, like a stone skipped along it (sparks where it touches).
                case Fly.Zigzag: return f.From + d * f.Aim * u + n * f.Lift * Mathf.Abs(Tri(3f * u));
                case Fly.Power: return f.From + d * f.Reach * EaseOut(u);
                case Fly.Loop:
                {
                    // Out to the cursor, then once round the circle on the line there (the shares by length), leaving as it came.
                    float a = f.Aim / (f.Aim + 2f * Mathf.PI * f.Lift);
                    var touch = f.From + d * f.Aim;
                    if (u < a) return Vector2.Lerp(f.From, touch, u / a);
                    float phi = 2f * Mathf.PI * (u - a) / (1f - a);
                    return f.Centre + (-n * Mathf.Cos(phi) + d * Mathf.Sin(phi)) * f.Lift;
                }
                case Fly.Lob: return Vector2.Lerp(f.From, f.Centre, u) + Vector2.up * f.Lift * 4f * u * (1f - u);
                case Fly.Hover:
                {
                    if (u < f.Travel) return Vector2.Lerp(f.From, f.Centre, EaseOut(u / f.Travel));
                    float phi = (u - f.Travel) * f.OutT * 0.35f * f.Side;
                    float r = f.Lift * Mathf.Clamp01((u - f.Travel) / 0.1f);
                    return f.Centre + new Vector2(Mathf.Cos(phi), Mathf.Sin(phi)) * r;
                }
                case Fly.Orbit:
                {
                    var c = hero != null ? HeroCentre(hero) : f.Centre;
                    float phi = f.Phase + u * f.OutT * 0.11f * f.Side;
                    float r = f.Lift * Mathf.Clamp01(u * f.OutT / 14f);
                    return c + new Vector2(Mathf.Cos(phi), Mathf.Sin(phi)) * r;
                }
                case Fly.Dive: return Vector2.Lerp(f.From, f.Centre, u * u);
                default: return f.From + d * f.Reach * u;
            }
        }

        /// <summary>
        /// A boomerang's update while it is out (StepBoomerang): its flight's path followed by its velocity; true while the
        /// flight decides when it turns home, false for Terraria's own (no flight, its own way out, knocked off its path).
        /// </summary>
        bool ThrownFlightOut(Proj p, HeroController hero)
        {
            if (!flights.TryGetValue(p, out var f))
            {
                if (p.Ai1 > 1f) return false;
                f = NewFlight(p);
                if (f == null) return false;
                DevLastPath = f.Move;
                if (f.Path != Fly.Vanilla) DevThrowPaths++;
            }
            if (f.Path == Fly.Vanilla || f.Broken || f.Ended) return false;
            f.T++;
            // Off its path: a wall turned it (one that bounces it: Terraria's flight from here).
            if (f.T > 1 && f.Path != Fly.Chain && (p.Pos - f.Expected).magnitude > 5f * Px)
            {
                f.Broken = true;
                return false;
            }
            if (f.Path == Fly.Chain) return ChainOut(p, f, hero);
            float u = Mathf.Clamp01(f.T / (float)f.OutT);
            if (f.Each == ArtFx.Mushrooms && f.T % 5 == 0 && f.T > f.OutT / 5) DropMushroom(p);
            if (f.Mid != ArtFx.None && !f.MidDone && u >= 0.5f)
            {
                f.MidDone = true;
                RunArtFx(p, f, f.Mid);
            }
            if (f.Path == Fly.Zigzag)
            {
                // A corner of the zigzag: sparks off it, as off a wall.
                float prev = (f.T - 1) / (float)f.OutT * 3f, now = u * 3f;
                if (Mathf.Floor(prev) != Mathf.Floor(now) && now > 0.9f && now < 2.5f) ZigzagSpark(p, f);
            }
            if (f.T >= f.OutT)
            {
                f.Ended = true;
                p.Returning = true;
                DevThrowFar = Mathf.Max(DevThrowFar, (p.Pos - f.From).magnitude / Px);
                RunArtFx(p, f, f.End);
                return true;
            }
            var next = PathAt(f, (f.T + 1f) / f.OutT, hero);
            p.Vel = (next - p.Pos) / Px;
            f.Expected = p.Pos + p.Vel * Px;
            // Round the player, whirling in place, coming down from the sky: through what is in the way.
            if (f.Path == Fly.Orbit || f.Path == Fly.Hover || f.Path == Fly.Dive) p.Collide = false;
            // Paladin's Hammer turns its own way out (Terraria's own out is held still): it tumbles down.
            if (p.Kind == ProjKind.PaladinsHammer) p.TRot += 0.3f * f.Side;
            DevThrowFar = Mathf.Max(DevThrowFar, (p.Pos - f.From).magnitude / Px);
            return true;
        }

        /// <summary>The ricochet: from enemy to enemy (each once, the nearest seen within 14 tiles of where it is), then home.</summary>
        bool ChainOut(Proj p, Flight f, HeroController hero)
        {
            var c = CenterPx(p);
            if (f.Target == null || f.Target.isDead || !f.Target.gameObject.activeInHierarchy || f.Struck.Contains(f.Target))
            {
                f.Target = null;
                float best = 14f * 16f;
                if (f.ChainLeft > 0)
                    foreach (var hm in EnemiesNear(c, best))
                    {
                        if (f.Struck.Contains(hm) || EnemyStats.Of(hm) == null) continue;
                        var e = EnemyCenterPx(hm);
                        float d = Vector2.Distance(c, e);
                        if (d < best && InSight(c, e)) { best = d; f.Target = hm; }
                    }
                if (f.Target == null)
                {
                    // Nothing to go for (at first: the cursor's way, then home).
                    if (f.T < 12 && f.Struck.Count == 0)
                    {
                        p.Vel = f.Dir * f.Reach / (f.OutT * Px);
                        return true;
                    }
                    f.Ended = true;
                    p.Returning = true;
                    return true;
                }
            }
            var to = EnemyCenterPx(f.Target) - c;
            float speed = Mathf.Max(14f, f.Reach / (f.OutT * Px) * 1.2f);
            var v = to.magnitude > 1f ? to.normalized * speed : Vector2.zero;
            p.Vel = Flip(v);
            if (to.magnitude < 20f)
            {
                f.Struck.Add(f.Target);
                f.ChainLeft--;
                ZigzagSpark(p, f);
                f.Target = null;
            }
            if (f.T > f.OutT * 3)
            {
                f.Ended = true;
                p.Returning = true;
            }
            return true;
        }

        /// <summary>
        /// A boomerang on its way home (StepBoomerang): how much faster it comes than Terraria's. A remade throw turned home
        /// by a strike that left it heading away from the player (a curve's strike from the side; a lob's from above, which
        /// reversed flings it up into the sky) is set heading for him at the speed it was thrown at — what Terraria's turn
        /// leaves a straight throw with — and homes on from there as Terraria's. One turned back toward him by its strike,
        /// or at its path's end (coasting on out as it turns, as Terraria's does), comes home as Terraria's.
        /// </summary>
        float ThrownReturning(Proj p)
        {
            if (!flights.TryGetValue(p, out var f) || f.Path == Fly.Vanilla) return 1f;
            if (!f.Homed)
            {
                f.Homed = true;
                var hero = HeroController.SilentInstance;
                if (hero != null)
                {
                    var to = HeroCentre(hero) - p.Pos;
                    float speed = p.LaunchSpeed > 0.01f ? p.LaunchSpeed : p.Vel.magnitude;
                    bool away = p.Vel.sqrMagnitude < 1e-6f || Vector2.Dot(p.Vel.normalized, to.normalized) < 0.5f;
                    if (to.sqrMagnitude > 1e-6f && (f.Path == Fly.Lob || away && !f.Ended)) p.Vel = to.normalized * speed;
                }
            }
            return f.ReturnBoost;
        }

        /// <summary>An art's throw still on its course (powering through, hovering, orbiting, ricocheting, diving, lobbed): striking doesn't turn it home.</summary>
        static bool ThrownKeepsOn(Proj p) => flights.TryGetValue(p, out var f) && f.Art && !f.Ended && !f.Broken && f.Path != Fly.Vanilla;

        void ZigzagSpark(Proj p, Flight f)
        {
            DevSparks++;
            var c = CenterPx(p);
            var pp = TerraParticles.Instance;
            if (pp == null) return;
            for (int k = 0; k < 6; k++)
            {
                int i = pp.NewDust(c - new Vector2(4f, 4f), 8, 8, k % 2 == 0 ? 228 : 6, 0f, 0f, 0, 0.9f);
                if (i < 0) continue;
                pp[i].Velocity = Random.insideUnitCircle * 3.5f;
                pp[i].NoGravity = true;
            }
            int m = pp.NewDust(c - new Vector2(4f, 4f), 8, 8, f.Look != null && f.Look.Dust >= 0 ? f.Look.Dust : 15, 0f, 0f, 0, 1.3f);
            if (m >= 0) pp[m].NoGravity = true;
            Sound("Item_10", 0.35f);
        }

        // ------------------------------------------------------------------ the arts' throws

        /// <summary>
        /// A right-click art's throw: what the weapon throws (or `kind`), from a place (world) the way given, at its speed ×
        /// speedScale, its damage × damage, on the flight given; free of the one-out rule. Null when it couldn't be made.
        /// </summary>
        internal bool ThrowArt(ItemDef def, Vector2 from, Vector2 dir, Flight f, float damage, float speedScale = 1f, ProjKind kind = ProjKind.None) =>
            ThrowArtProj(def, from, dir, f, damage, speedScale, kind) != null;

        Proj ThrowArtProj(ItemDef def, Vector2 from, Vector2 dir, Flight f, float damage, float speedScale, ProjKind kind)
        {
            if (def == null) return null;
            var k = kind == ProjKind.None ? def.Shoot : kind;
            if (dir.sqrMagnitude < 1e-6f) dir = Vector2.right;
            dir.Normalize();
            float speed = def.ShootSpeed / Equipment.UseScale(def) * speedScale;
            var keep = SpawnClass;
            SpawnClass = DamageClass.Melee;
            thrownArtSpawn = true;
            Proj p;
            try { p = Spawn(k, from, dir * speed, Mathf.Max(1, Mathf.RoundToInt(Equipment.Damage(def) * damage)), Equipment.Knockback(def), Equipment.Crit(def)); }
            finally { thrownArtSpawn = false; SpawnClass = keep; }
            if (p == null) return null;
            p.Melee = true;
            if (f != null)
            {
                f.Art = true;
                f.From = from;
                f.Def = def;
                if (f.Scale > 0f) p.Scale = f.Scale;
                if (f.Dir == default) f.Dir = dir;
                f.Look = throwLooks.TryGetValue(k, out var look) ? look : throwLooks[ProjKind.Boomerang];
                if (p.Spec.Ai == Ai.Boomerang) ShapeFlight(p, f, 1f);
                else if (f.ConvergeTurn != 0f) CurveOnto(p, f, f.ConvergeTurn, (TerraParticles.ToPx(AimWorld()) - TerraParticles.ToPx(from)).magnitude);
                else if (f.Reach <= 0f) f.Reach = ThrowReach(def) * f.ReachScale;
                f.Expected = from;
                f.Move = FlyName(f.Path);
                flights[p] = f;
            }
            DevArtThrows++;
            return p;
        }

        /// <summary>An art's throw's touch on its way: the burst, the impact, the split.</summary>
        void RunArtFx(Proj p, Flight f, ArtFx fx)
        {
            switch (fx)
            {
                case ArtFx.IceBurst: IceBurst(p, 6 + Mathf.RoundToInt(4f * f.FxPower)); break;
                case ArtFx.AxeImpact: ImpactBurst(p, 7, 1, 0.8f + 0.4f * f.FxPower); break;
                case ArtFx.HolyImpact: ImpactBurst(p, 57, 204, 1f + 0.5f * f.FxPower); break;
                case ArtFx.Split:
                    for (int k = -1; k <= 1; k += 2)
                    {
                        float a = k * 35f * Mathf.Deg2Rad;
                        var d = new Vector2(f.Dir.x * Mathf.Cos(a) - f.Dir.y * Mathf.Sin(a), f.Dir.x * Mathf.Sin(a) + f.Dir.y * Mathf.Cos(a));
                        ThrowArtProj(f.Def, p.Pos, d, new Flight { Path = Fly.Straight, ReachScale = 0.6f }, 0.6f, 1f, p.Kind);
                    }
                    ImpactBurst(p, 226, -1, 0.35f);
                    break;
            }
        }

        /// <summary>How far a remade throw of the weapon goes (world): 30% past Terraria's straight throw.</summary>
        internal float ThrowReach(ItemDef def)
        {
            if (def == null || !specs.TryGetValue(def.Shoot, out var s)) return 8f;
            return def.ShootSpeed / Equipment.UseScale(def) * Mathf.Max(10, s.OutTicks) * Px * 1.3f;
        }

        /// <summary>The Ice Boomerang's art at the end of its throw: a ring of ice bolts, a frosty puff.</summary>
        void IceBurst(Proj p, int count)
        {
            var c = CenterPx(p);
            int dmg = Mathf.Max(1, Mathf.RoundToInt(p.Damage * 0.45f));
            for (int k = 0; k < count; k++)
            {
                float a = k * Mathf.PI * 2f / count + Random.Range(-0.1f, 0.1f);
                MeleeShot(ProjKind.IceBolt, c, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 8f, dmg, p.Knockback * 0.5f, p.Crit);
            }
            Burst(c, 135, 16, 3.5f, 1.3f);
            Burst(c, 185, 8, 2f, 1f);
            Sound("Item_27", 0.6f);
        }

        /// <summary>A heavy landing (the Axearang's chop, Paladin's Hammer from the sky): a ring of its motes thrown low along the ground, dust, a jolt.</summary>
        void ImpactBurst(Proj p, int dust, int dust2, float strength)
        {
            var c = CenterPx(p);
            Burst(c, dust, 26, 5f * strength, 1.5f);
            if (dust2 >= 0) Burst(c, dust2, 14, 3.2f * strength, 1.2f);
            var pp = TerraParticles.Instance;
            if (pp != null)
            {
                // Thrown low both ways along the ground, and the dust it raises.
                for (int k = 0; k < 16; k++)
                {
                    int i = pp.NewDust(c + new Vector2(-8f, 6f), 16, 6, dust, 0f, 0f, 30, 1.3f);
                    if (i < 0) continue;
                    pp[i].Velocity = new Vector2((k % 2 == 0 ? 1f : -1f) * Random.Range(3f, 7f) * strength, Random.Range(-1.2f, 0f));
                    pp[i].NoGravity = true;
                }
                for (int k = 0; k < 12; k++)
                {
                    int i = pp.NewDust(c + new Vector2(-28f, 4f), 56, 8, 31, 0f, 0f, 80, 1.4f);
                    if (i >= 0) pp[i].Velocity = new Vector2(Random.Range(-4f, 4f), Random.Range(-1.8f, 0f));
                }
            }
            Player.TerraCamera.Punch(0.12f * strength, 0.18f);
            Sound("Item_14", 0.5f);
        }

        /// <summary>The Shroomerang's art over the cursor: a glowing mushroom dropped (the Mushroom Spear's own).</summary>
        void DropMushroom(Proj p)
        {
            MeleeShot(ProjKind.SpearMushroom, CenterPx(p), new Vector2(Random.Range(-1f, 1f), 1.5f), Mathf.Max(1, p.Damage / 3), 0f, p.Crit);
        }

        static void Burst(Vector2 c, int type, int count, float speed, float scale)
        {
            var pp = TerraParticles.Instance;
            if (pp == null || type < 0) return;
            for (int k = 0; k < count; k++)
            {
                float a = k / (float)count * Mathf.PI * 2f + Random.Range(-0.15f, 0.15f);
                int i = pp.NewDust(c - new Vector2(4f, 4f), 8, 8, type, 0f, 0f, 40, scale);
                if (i < 0) continue;
                pp[i].Velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * speed * Random.Range(0.7f, 1.1f);
                pp[i].NoGravity = true;
            }
        }

        /// <summary>
        /// The Flying Knife's art: knives circling the cursor (an orbit of Lift px, OutT updates), each its own way round;
        /// then let go, home as Terraria's. True while it circles.
        /// </summary>
        bool FlyingKnifeArt(Proj p)
        {
            if (!flights.TryGetValue(p, out var f) || !f.Art || f.Path != Fly.Orbit || f.Ended) return false;
            f.T++;
            if (f.T >= f.OutT)
            {
                f.Ended = true;
                p.Ai0 = 1f;
                p.LocalAi0 = 16f;
                p.Collide = false;
                return false;
            }
            p.Collide = false;
            var c = AimPx();
            float phi = f.Phase + f.T * 0.17f * f.Side;
            float r = f.Lift * Mathf.Clamp01(f.T / 14f);
            var want = c + new Vector2(Mathf.Cos(phi), Mathf.Sin(phi)) * r;
            var v = want - CenterPx(p);
            if (v.magnitude > 26f) v = v.normalized * 26f;
            p.Vel = Flip(v);
            p.TRot += 0.5f;
            return true;
        }

        /// <summary>The flight of a projectile, if it has one (the arts' own touches).</summary>
        static Flight FlightOf(Proj p) => p != null && flights.TryGetValue(p, out var f) ? f : null;

        // ------------------------------------------------------------------ the look

        static Sprite ThrownSprite(Proj p)
        {
            var whole = FxBatch.Whole(p.Spec.Texture);
            if (whole == null) return null;
            int tw = (int)whole.rect.width, th = (int)whole.rect.height;
            int frames = Mathf.Max(1, p.Spec.Frames);
            int fh = th / frames;
            return FxBatch.Frame(p.Spec.Texture, 0, 0, tw, fh, tw * 0.5f, fh * 0.5f);
        }

        /// <summary>
        /// Each frame before the projectiles are drawn: every remade throw's afterimages (its own sprite where it was, in its
        /// colour, fainter the older), a ribbon of light through its last places, its theme's motes; a throw caught at the
        /// hand: a small flourish of motes. Flights whose projectile is gone are let go.
        /// </summary>
        void DrawThrownArts(FxBatch fx)
        {
            if (flights.Count == 0) return;
            var hero = HeroController.SilentInstance;
            float rate = Mathf.Min(Time.deltaTime, 0.05f) * 60f;
            List<Proj> gone = null;
            foreach (var kv in flights)
            {
                var p = kv.Key;
                var f = kv.Value;
                if (p.Dead || !live.Contains(p))
                {
                    (gone ??= new List<Proj>()).Add(p);
                    if (DevFlightLog.Count < 60) DevFlightLog.Add($"{f.Move}{(f.Broken ? "/断" : "")} T{f.T}/{f.OutT} 共{p.Ticks}");
                    if (!f.Caught && p.Returning && hero != null && Vector2.Distance(p.Pos, HeroCentre(hero)) < 2.5f)
                    {
                        f.Caught = true;
                        CatchFlourish(p, f, hero);
                    }
                    continue;
                }
                if (p.Ticks == 0 || f.Look == null || p.Delay > 0) continue;
                if (f.Tumble) p.TRot = Mathf.Atan2(-p.Vel.y, p.Vel.x) + Mathf.PI / 2f + f.T * 0.55f;
                if (f.Path == Fly.Corkscrew && !f.Ended && f.OutT > 0) p.Scale = p.Spec.Scale * (1f + 0.22f * Mathf.Sin(6f * Mathf.PI * f.T / f.OutT));
                else if (f.Path == Fly.Corkscrew) p.Scale = p.Spec.Scale;
                f.Places.Add(p.Pos);
                f.Turns.Add(p.TRot);
                int keep = Mathf.Max(2, f.Look.Images + 2);
                while (f.Places.Count > keep) { f.Places.RemoveAt(0); f.Turns.RemoveAt(0); }
                DrawThrowTrail(fx, p, f);
                ThrowMotes(p, f, rate);
            }
            if (gone != null) foreach (var p in gone) flights.Remove(p);
        }

        void DrawThrowTrail(FxBatch fx, Proj p, Flight f)
        {
            var look = f.Look;
            int n = f.Places.Count;
            if (n < 2) return;
            var spr = ThrownSprite(p);
            // The ribbon: through its last places, brighter toward it.
            for (int k = 1; k < n; k++)
            {
                float fr = k / (float)(n - 1);
                var a = TerraParticles.ToPx(f.Places[k - 1]);
                var b = TerraParticles.ToPx(f.Places[k]);
                if ((b - a).sqrMagnitude < 1f) continue;
                StreakPx(fx, a, b, Color.Lerp(look.Main, look.Light, fr), (5f + 7f * fr) * look.Ribbon, 0.5f * fr * look.Ribbon);
            }
            if (spr == null) return;
            // Afterimages: its own picture where it was, its colour added, fainter the older.
            for (int k = n - 2, age = 1; k >= 0 && age <= look.Images; k--, age++)
            {
                float fade = 1f - age / (look.Images + 1f);
                var c = Color.Lerp(look.Light, look.Main, age / (float)look.Images) * (0.42f * fade);
                c.a = 0f;
                fx.Draw(spr, TerraParticles.ToPx(f.Places[k]), c, f.Turns[k], p.Scale * (1f - 0.04f * age), flipX: p.Kind == ProjKind.PossessedHatchet && p.Dir < 0f);
            }
        }

        void ThrowMotes(Proj p, Flight f, float rate)
        {
            var look = f.Look;
            var pp = TerraParticles.Instance;
            if (pp == null || look.Dust < 0 || rate <= 0f) return;
            float want = look.Rate * rate * (f.Art ? 1.4f : 1f);
            var c = TerraParticles.ToPx(p.Pos);
            var v = Flip(p.Vel);
            while (want > 0f)
            {
                if (Random.value < want)
                {
                    int type = look.Dust2 >= 0 && Random.value < 0.4f ? look.Dust2 : look.Dust;
                    int i = pp.NewDust(c - new Vector2(8f, 8f), 16, 16, type, 0f, 0f, 60, look.DustScale);
                    if (i >= 0)
                    {
                        pp[i].Velocity = pp[i].Velocity * 0.35f - v * 0.15f;
                        pp[i].NoGravity = true;
                    }
                }
                want -= 1f;
            }
            // A hover or an orbit: a ring of its colour as it whirls.
            if ((f.Path == Fly.Hover || f.Path == Fly.Orbit) && Random.value < 0.5f * rate)
            {
                float a = Random.value * Mathf.PI * 2f;
                var at = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 22f;
                int i = pp.NewDust(at - new Vector2(2f, 2f), 4, 4, look.Dust, 0f, 0f, 40, look.DustScale * 0.9f);
                if (i >= 0)
                {
                    pp[i].Velocity = new Vector2(-Mathf.Sin(a), Mathf.Cos(a)) * 2f;
                    pp[i].NoGravity = true;
                }
            }
        }

        /// <summary>Caught at the hand: a little ring of its motes, and its gleam.</summary>
        void CatchFlourish(Proj p, Flight f, HeroController hero)
        {
            DevCatches++;
            var pp = TerraParticles.Instance;
            if (pp == null || f.Look == null) return;
            var c = TerraParticles.ToPx(p.Pos);
            int type = f.Look.Dust >= 0 ? f.Look.Dust : 15;
            for (int k = 0; k < 8; k++)
            {
                float a = k / 8f * Mathf.PI * 2f;
                int i = pp.NewDust(c - new Vector2(2f, 2f), 4, 4, type, 0f, 0f, 40, f.Look.DustScale * 0.8f);
                if (i < 0) continue;
                pp[i].Velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.2f;
                pp[i].NoGravity = true;
            }
            int g = pp.NewDust(c - new Vector2(2f, 2f), 4, 4, 15, 0f, 0f, 0, 1.2f);
            if (g >= 0) { pp[g].Velocity = Vector2.zero; pp[g].NoGravity = true; }
        }

        // ------------------------------------------------------------------ the knives and javelins (KnifeFlight)

        /// <summary>The knives' and javelins' throws in turn (Vampire Knives: the scatter, a stream, crossing at the cursor; the Shadowflame Knife: straight, tumbling, an arc onto the cursor; Daybreak: straight, a lob onto the cursor, a fast flat throw; the Scourge: straight, tumbling, fast).</summary>
        static readonly Dictionary<ProjKind, string[]> knifeCycles = new Dictionary<ProjKind, string[]>
        {
            [ProjKind.VampireKnife] = new[] { "散射", "剑雨", "交叉" },
            [ProjKind.ShadowFlameKnifeProj] = new[] { "直掷", "旋掷", "弧掷" },
            [ProjKind.Daybreak] = new[] { "直投", "高抛", "疾掷" },
            // Its eaters do most of its harm: no late lob (their bursts would overlap), a tumbling throw instead.
            [ProjKind.ScourgeJavelin] = new[] { "直投", "旋掷", "疾掷" },
        };

        /// <summary>The next of a knife's or javelin's throws (one turn for a whole volley).</summary>
        static string NextKnifeMove(ProjKind kind)
        {
            if (!knifeCycles.TryGetValue(kind, out var cycle)) return "";
            int n = cycleAt.TryGetValue(kind, out int c) && cycleTime.TryGetValue(kind, out float t) && Time.time - t < 2f ? c : 0;
            cycleAt[kind] = n + 1;
            cycleTime[kind] = Time.time;
            return cycle[n % cycle.Length];
        }

        /// <summary>
        /// A knife's or javelin's first update with the remade swings on (from its own step): its look, and its throw's turn —
        /// sped up a quarter (it reaches as much further), tilted up for a lob, sent curving. Vampire Knives come by the
        /// volley (ThrowVampireKnives shapes them). An art's has its flight already.
        /// </summary>
        static void KnifeFirst(Proj p)
        {
            if (flights.ContainsKey(p) || !BladeArts.Enabled || !throwLooks.TryGetValue(p.Kind, out var look)) return;
            var f = new Flight { Look = look, From = p.Pos, Dir = p.Vel.sqrMagnitude > 1e-6f ? p.Vel.normalized : Vector2.right, Path = Fly.Vanilla };
            string move = p.Kind == ProjKind.FlyingKnifeProj ? "" : NextKnifeMove(p.Kind);
            f.Move = move;
            // A quarter faster (and so further), the fast throw nearly half; the Scourge's javelin, which flies on until it
            // strikes, only its fast throw (faster, it would pass through what it should strike between its updates).
            if (p.Kind == ProjKind.ScourgeJavelin) p.Vel *= move == "疾掷" ? 1.2f : 1f;
            else if (p.Kind != ProjKind.FlyingKnifeProj) p.Vel *= move == "疾掷" ? 1.45f : 1.25f;
            var aim = TerraParticles.ToPx(AimWorld()) - TerraParticles.ToPx(p.Pos);
            aim.y = -aim.y;
            switch (move)
            {
                case "旋掷": f.Tumble = true; break;
                case "弧掷":
                {
                    float side = (cycleAt.TryGetValue(p.Kind, out int c) ? c / 3 : 0) % 2 == 0 ? 1f : -1f;
                    CurveOnto(p, f, 16f * side, aim.magnitude);
                    break;
                }
                case "高抛": LobOnto(p, f, aim, 0.32f); break;
            }
            DevLastPath = move;
            DevThrowPaths++;
            flights[p] = f;
        }

        /// <summary>
        /// A throw turned `degrees` off its aim and curving back at a steady rate to cross the aim's line `distance` px out
        /// (lateral offset θ·s − ω·s²/2v is nought again at s = 2vθ/ω): it arrives on the cursor from the side.
        /// </summary>
        static void CurveOnto(Proj p, Flight f, float degrees, float distance)
        {
            float v = p.Vel.magnitude;
            float d = Mathf.Clamp(distance, 80f, 420f);
            if (v < 0.5f) return;
            p.Vel = (Vector2)(Quaternion.Euler(0f, 0f, degrees) * p.Vel);
            float theta = Mathf.Abs(degrees) * Mathf.Deg2Rad;
            f.Curve = -Mathf.Sign(degrees) * 2f * v * theta / d;
            f.CurveFor = Mathf.CeilToInt(d / v) + 2;
        }

        /// <summary>
        /// A lob onto the cursor (px, y up from the thrower): thrown up at 50° (70° when the cursor is above that line), at the
        /// speed that lands it there under `gravity` px an update² (v² = g·x² / (2·cos²θ·(x·tanθ − y))), kept within 0.6-1.3 ×
        /// the throw's own.
        /// </summary>
        static void LobOnto(Proj p, Flight f, Vector2 to, float gravity)
        {
            float v0 = p.Vel.magnitude, x = Mathf.Max(8f, Mathf.Abs(to.x)), y = to.y;
            float theta = 50f * Mathf.Deg2Rad;
            float denom = 2f * Mathf.Cos(theta) * Mathf.Cos(theta) * (x * Mathf.Tan(theta) - y);
            if (denom <= 1f)
            {
                theta = 70f * Mathf.Deg2Rad;
                denom = 2f * Mathf.Cos(theta) * Mathf.Cos(theta) * (x * Mathf.Tan(theta) - y);
            }
            float v = denom > 1f ? Mathf.Sqrt(gravity * x * x / denom) : v0;
            v = Mathf.Clamp(v, v0 * 0.6f, v0 * 1.3f);
            float dir = to.x < 0f ? -1f : 1f;
            p.Vel = new Vector2(Mathf.Cos(theta) * dir, Mathf.Sin(theta)) * v;
            f.Gravity = gravity;
        }

        /// <summary>A knife's or javelin's own turn each update (its curve, the lob's fall); from its step.</summary>
        static void KnifeSteer(Proj p)
        {
            if (!flights.TryGetValue(p, out var f)) return;
            // Daybreak stuck in an enemy goes with it.
            if (p.Kind == ProjKind.Daybreak && p.Ai0 != 0f) return;
            f.T++;
            if (f.CurveFor > 0 && f.T <= f.CurveFor) p.Vel = (Vector2)(Quaternion.Euler(0f, 0f, f.Curve * Mathf.Rad2Deg) * p.Vel);
            if (f.Gravity > 0f) p.Vel.y -= f.Gravity;
            DevThrowFar = Mathf.Max(DevThrowFar, (p.Pos - f.From).magnitude / Px);
        }

        /// <summary>
        /// A volley of Vampire Knives just thrown (ThrowVampireKnives): its turn's shape — the scatter as Terraria's, a stream
        /// along the aim, or two halves curving in to cross at the cursor; each a quarter faster.
        /// </summary>
        static void ShapeVampireVolley(List<Proj> knives, Vector2 vel)
        {
            if (!BladeArts.Enabled || knives.Count == 0) return;
            string move = NextKnifeMove(ProjKind.VampireKnife);
            DevLastPath = move;
            DevThrowPaths++;
            var look = throwLooks[ProjKind.VampireKnife];
            float speed = vel.magnitude;
            var dir = speed > 1e-4f ? vel / speed : Vector2.right;
            for (int k = 0; k < knives.Count; k++)
            {
                var p = knives[k];
                var f = new Flight { Look = look, From = p.Pos, Dir = dir, Path = Fly.Vanilla, Move = move };
                switch (move)
                {
                    case "剑雨":
                        p.Vel = (Vector2)(Quaternion.Euler(0f, 0f, Random.Range(-3f, 3f)) * dir) * speed * (0.95f + 0.09f * k);
                        break;
                }
                p.Vel *= 1.25f;
                if (move == "交叉")
                {
                    var aim = TerraParticles.ToPx(AimWorld()) - TerraParticles.ToPx(p.Pos);
                    p.Vel = dir * speed * 1.25f;
                    CurveOnto(p, f, (k % 2 == 0 ? 1f : -1f) * (14f + 4f * (k / 2)), aim.magnitude);
                }
                flights[p] = f;
            }
        }
    }
}
