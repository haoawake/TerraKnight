using System.Collections.Generic;
using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The remade minions' hunting (the user's 10-28 "召唤物有点蠢，碰到那些会飞的怪以及瞬移的怪物追都追不上，但又别太强"),
    /// on top of each minion's own Terraria AI, for those summoned by a remade staff (M.Remade):
    /// - Each enemy's speed is followed tick by tick; a jump of more than three blocks in one tick is a teleport, after which
    ///   it is seen where it is (no lead) for a while.
    /// - While a minion's AI thinks, it sees every enemy where it will be by the time the minion (or its shot) gets there:
    ///   chases and shots go for that point. Its moves and its blows are still against the enemy where it really is; a still
    ///   enemy (a Target Dummy) is seen as it is, so nothing hits harder, it only misses less.
    /// - A flying minion falling behind a moving enemy, or one that has just teleported, sprints after it a moment (the
    ///   shooters only into their range), then rests; one far from an enemy that has just teleported goes after it the same
    ///   way, in a burst of sparks (every three seconds at most).
    /// - The walkers (the slime, the spiders, the pirates, the frog, the Flinx) throw something of Terraria's at an enemy that
    ///   stays out of their reach overhead, aimed at where it will be.
    /// Terraria px, y down, per tick, as the rest of Minions.
    /// </summary>
    internal sealed partial class Minions
    {
        sealed class Track
        {
            /// <summary>Where it was, its speed over the last few ticks, and over the last second or so.</summary>
            public Vector2 Last, Vel, Slow;
            public int Seen, Blink;
        }

        readonly Dictionary<int, Track> tracks = new Dictionary<int, Track>();
        readonly List<int> staleTracks = new List<int>();
        int huntTick;
        bool leading;

        public static int DevBlinks, DevSprints, DevTosses, DevFollows;
        /// <summary>Dev: each part of the hunting switched off on its own (suite minionchase, TK_HUNT_OFF), to see what each does.</summary>
        public static bool DevNoLead, DevNoSprint, DevNoToss;
        /// <summary>Dev: why walkers didn't throw, counted by reason.</summary>
        public static readonly Dictionary<string, int> DevTossWhy = new Dictionary<string, int>();
        static void Why(string r) => DevTossWhy[r] = DevTossWhy.TryGetValue(r, out int n) ? n + 1 : 1;

        /// <summary>A jump this far (px) in one tick is a teleport.</summary>
        const float BlinkStep = 48f;
        /// <summary>Ticks after a teleport an enemy is seen where it is.</summary>
        const int BlinkTicks = 40;

        /// <summary>FindNpcs, for each enemy found: its speed (px a tick, smoothed) and whether it has just teleported.</summary>
        void TrackNpc(Npc n)
        {
            if (!tracks.TryGetValue(n.Id, out var tr))
            {
                tracks[n.Id] = new Track { Last = n.Center, Seen = huntTick };
                return;
            }
            // Seen again after a while out of reach (a Grimmkin is untouchable as it teleports): where it was then counts.
            int gap = huntTick - tr.Seen;
            var step = gap <= 240 ? n.Center - tr.Last : Vector2.zero;
            if (gap > 1 && step.sqrMagnitude <= BlinkStep * BlinkStep)
            {
                tr.Vel = Vector2.zero;
                tr.Slow = Vector2.zero;
                if (tr.Blink > 0) tr.Blink--;
            }
            else if (step.sqrMagnitude > BlinkStep * BlinkStep)
            {
                tr.Vel = Vector2.zero;
                tr.Slow = Vector2.zero;
                tr.Blink = BlinkTicks;
                DevBlinks++;
            }
            else
            {
                tr.Vel = Vector2.Lerp(tr.Vel, step, 0.35f);
                tr.Slow = Vector2.Lerp(tr.Slow, step, 0.06f);
                if (tr.Blink > 0) tr.Blink--;
            }
            tr.Last = n.Center;
            tr.Seen = huntTick;
            n.Vel = tr.Vel;
            n.Blink = tr.Blink;
            // Steady: going the way it has gone for the last second, at much the speed (a hovering Aspid, a patrolling sentry); a
            // Vengefly turning back from its dive isn't, and is aimed at where it is.
            float fast = tr.Vel.magnitude, slow = tr.Slow.magnitude;
            n.Steady = fast > 0.2f && slow > fast * 0.5f && Vector2.Dot(tr.Vel, tr.Slow) > 0.8f * fast * slow;
        }

        /// <summary>Once a tick before the enemies are found: the tick counted, enemies unseen for two seconds forgotten.</summary>
        void NextHuntTick()
        {
            huntTick++;
            if (huntTick % 600 != 0) return;
            staleTracks.Clear();
            foreach (var kv in tracks) if (huntTick - kv.Value.Seen > 120) staleTracks.Add(kv.Key);
            foreach (var id in staleTracks) tracks.Remove(id);
        }

        /// <summary>The minions that hunt the remade way: every attacking minion but the Hallownest souls (their own designs) and the dragon's body.</summary>
        static bool Hunts(Kind k) =>
            k != Kind.AbigailCounter && k != Kind.DragonBody1 && k != Kind.DragonBody2 && k != Kind.DragonTail
            && k != Kind.WanderingSoul && k != Kind.ChoirSoul && k != Kind.ShadeSoul && k != Kind.DirgeSoul && k != Kind.CobSpider;

        /// <summary>
        /// The hunters that see enemies ahead of themselves: not those that lead their own aim already (the pygmies' spears,
        /// the tiger's leaps, the Pals' jumps, the Deadly Sphere's dashes: led twice they overshot), nor the UFO, which hovers
        /// over its enemy to zap it, nor the twins and the Stardust Dragon (worse with it).
        /// </summary>
        static bool Leads(Kind k) => !IsPygmy(k) && !IsTiger(k) && k != Kind.TigerGem && k != Kind.UFO && k != Kind.MushroomBoi && k != Kind.Cattiva
            && k != Kind.TrustyCattiva && k != Kind.ClayPot && k != Kind.Foxsparks && k != Kind.TrustyFoxsparks
            && !IsTwin(k) && k != Kind.DeadlySphere && k != Kind.DragonHead;

        /// <summary>
        /// How fast (px a tick) what the minion sends at an enemy gets there: its shot's speed for the shooters, else its own
        /// pace (at least this, more while it goes faster).
        /// </summary>
        static float Pace(Kind k) => k switch
        {
            Kind.Hornet => 10f,
            Kind.Imp => 11f,
            Kind.Retanimini => 15f,
            Kind.UFO => 20f,
            Kind.StardustCell => 12f,
            Kind.Sharknado => 10f,
            Kind.ForbiddenRelic => 12f,
            Kind.Pygmy1 or Kind.Pygmy2 or Kind.Pygmy3 or Kind.Pygmy4 => 11f,
            _ => 8f,
        };

        /// <summary>How far off an enemy a shooter stays (px): a sprint takes it into that range, not onto the enemy.</summary>
        static float Keep(Kind k) => k switch
        {
            Kind.Imp => 150f,
            Kind.Hornet => 120f,
            Kind.Retanimini => 160f,
            Kind.UFO => 120f,
            Kind.StardustCell => 200f,
            Kind.Sharknado => 100f,
            Kind.ForbiddenRelic => 160f,
            _ => 0f,
        };

        /// <summary>
        /// The flying minions that sprint and follow a teleport: not those that strike in dashes of their own (the Optic Staff's
        /// twins, the Deadly Sphere, the Stardust Cell, the Raven: a sprint broke their rhythm, the sphere's even at a Target
        /// Dummy) nor the fast ones through walls (Terraprisma's blades, the Blade Staff's daggers, the Stardust Dragon).
        /// </summary>
        static bool Flier(Kind k) => k == Kind.Hornet || k == Kind.Imp || k == Kind.Abigail || k == Kind.Bat || k == Kind.Finch
            || k == Kind.Sharknado || k == Kind.UFO || k == Kind.ForbiddenRelic;

        /// <summary>
        /// Before the minion's AI thinks: each enemy moved, for it, to where it will be when the minion's shot or the minion
        /// gets there (three quarters of the way, a third of a second ahead at most, 10 blocks, and not into a wall); only one
        /// going steadily (Npc.Steady), none just teleported.
        /// </summary>
        void LeadFor(M m)
        {
            if (DevNoLead) return;
            float pace = Mathf.Max(m.Vel.magnitude, Pace(m.Kind));
            foreach (var n in npcs)
            {
                n.Real = n.Center;
                n.RealBox = n.Box;
                if (n.Blink > 0 || !n.Steady) continue;
                // A third of a second at most, three quarters of the way: a Vengefly turns back from its dive at once.
                float t = Mathf.Min(Vector2.Distance(m.Center, n.Center) / pace, 20f) * 0.75f;
                var off = Vector2.ClampMagnitude(n.Vel * t, 160f);
                if (off.sqrMagnitude < 1f || !ClearLine(n.Center, n.Center + off)) continue;
                n.Center += off;
                n.Box.position += off;
            }
            leading = true;
        }

        /// <summary>After it: every enemy back where it is.</summary>
        void Unlead()
        {
            if (!leading) return;
            foreach (var n in npcs)
            {
                n.Center = n.Real;
                n.Box = n.RealBox;
            }
            leading = false;
        }

        /// <summary>
        /// A flying minion's sprint: with its enemy more than its range and 7 blocks off and the gap not closing over a third
        /// of a second (or the enemy just teleported), half a second at 1.6 times its speed (9 to 15 px a tick) straight at
        /// where the enemy will be, sparks behind it; then nearly two seconds' rest. Not through a wall it can't pass.
        /// </summary>
        void Pursue(M m)
        {
            if (!Flier(m.Kind) || DevNoSprint) return;
            var t = FindTargetInRange(m, 1400f, false);
            if (t == null)
            {
                m.Sprint = 0;
                if (m.SprintCool > 0) m.SprintCool--;
                return;
            }
            float dist = Vector2.Distance(m.Center, t.Center), keep = Keep(m.Kind);
            if (m.FollowCool > 0) m.FollowCool--;
            // Its enemy has just teleported far off: the minion goes after it the same way.
            if (t.Blink > BlinkTicks - 20 && dist > keep + 300f && m.FollowCool == 0 && FollowTeleport(m, t)) return;
            if (m.Sprint > 0)
            {
                m.Sprint--;
                if (dist <= keep + 40f)
                {
                    m.Sprint = 0;
                    return;
                }
                float speed = Mathf.Clamp(m.SprintSpeed, 9f, 15f);
                if (speed > m.Vel.magnitude * 1.05f) m.Vel = Vector2.Lerp(m.Vel, (t.Center - m.Center) / dist * speed, 0.3f);
                SprintSparks(m);
                return;
            }
            if (m.SprintCool > 0) m.SprintCool--;
            if (++m.GapTicks >= 20)
            {
                m.GapTicks = 0;
                m.Lagging = dist > m.GapWas - 24f;
                m.GapWas = dist;
            }
            if (m.SprintCool > 0 || dist <= keep + 120f) return;
            if (!(t.Blink > 0 || m.Lagging && dist > keep + 160f)) return;
            if (!Phases(m.Kind) && m.TileCollide && !ClearLine(m.Center, t.Center)) return;
            m.Sprint = 30;
            m.SprintCool = 100;
            m.SprintSpeed = Mathf.Max(m.Vel.magnitude, Pace(m.Kind)) * 1.6f;
            DevSprints++;
        }

        /// <summary>
        /// A flying minion's answer to its enemy's teleport: gone in a burst of sparks, back in another by the enemy (its range
        /// off, 3 blocks for one that strikes with its body), on the side it came from, else above, else the far side: a spot
        /// clear of rock (for one that collides) in sight of the enemy. Three seconds before the next.
        /// </summary>
        bool FollowTeleport(M m, Npc t)
        {
            var from = m.Center - t.Center;
            var away = from.sqrMagnitude > 1f ? from.normalized : Vector2.up;
            float r = Mathf.Max(Keep(m.Kind), 48f);
            foreach (var dir in new[] { away, new Vector2(0f, -1f), new Vector2(-away.x, away.y), new Vector2(away.x, -Mathf.Abs(away.y) - 0.5f).normalized })
            {
                var at = t.Center + dir * r;
                if (m.TileCollide && SolidBox(at.x - m.W / 2f, at.y - m.H / 2f, m.W, m.H)) continue;
                if (!ClearLine(at, t.Center)) continue;
                FollowSparks(m.Center);
                m.Center = at;
                m.OldPos = m.Pos;
                for (int k = 0; k < m.OldPosRing.Length; k++) m.OldPosRing[k] = m.Pos;
                m.Vel = (t.Center - at).normalized * 2f;
                m.Sprint = 0;
                m.FollowCool = 180;
                FollowSparks(at);
                DevFollows++;
                return true;
            }
            return false;
        }

        static void FollowSparks(Vector2 at)
        {
            for (int k = 0; k < 12; k++)
            {
                int d = DustAt(at - new Vector2(10f, 10f), 20, 20, 15, Random.insideUnitCircle * 3f, 100, 1.2f);
                if (d >= 0) TerraParticles.Instance[d].NoGravity = true;
            }
        }

        static void SprintSparks(M m)
        {
            for (int k = 0; k < 2; k++)
            {
                int d = DustBox(m, 15, -m.Vel * 0.15f, 100, 1.1f);
                if (d >= 0) TerraParticles.Instance[d].NoGravity = true;
            }
        }

        /// <summary>
        /// A walker's throw at what it can't reach: an enemy that has been out of its reach overhead (2 to 25 blocks up, at most
        /// 25 across, in sight) a third of a second longer than within it (one that dives at the player is met the game's way, under
        /// it) has something of Terraria's thrown at where it will be, aimed over for its fall: the slime spits a gel ball, the
        /// Flinx throws a snowball, a pirate a throwing knife, a spider or the frog a venom fang; then a rest. Nothing is thrown
        /// at an enemy within reach, so a walker hits no harder at one it can get to (a Target Dummy).
        /// </summary>
        void Toss(M m)
        {
            if (!Tosser(m.Kind, out var kind, out float speed, out float fall, out int rest, out float k) || DevNoToss) return;
            if (m.TossCool > 0) m.TossCool--;
            if (m.Ai0 != 0f || m.Latched) { m.Overhead = 0; Why("忙"); return; }
            var t = FindTargetInRange(m, 700f, false);
            if (t == null) { m.Overhead = 0; Why("没目标"); return; }
            float h = m.Bottom - t.Center.y, dx = t.Center.x - m.Center.x;
            // Out of reach overhead counts up, within reach down: a flyer bobbing in and out of reach still adds up.
            if (t.Box.yMax > m.Bottom - 32f || h < 30f) { m.Overhead = Mathf.Max(0, m.Overhead - 1); Why("够得着"); return; }
            if (h > 400f || Mathf.Abs(dx) > 400f) { m.Overhead = 0; Why("太远"); return; }
            var from = m.Center + new Vector2(0f, -m.H * 0.3f);
            if (!ClearLine(from, t.Center)) { Why("看不见"); return; }
            if (++m.Overhead < 20 || m.TossCool > 0) { Why("等"); return; }
            // Aimed over the target by what it falls on the way (px a tick², over the ticks it flies).
            var to = t.Center;
            float ticks = Vector2.Distance(from, to) / speed;
            to.y -= 0.5f * fall * ticks * ticks;
            var v = (to - from).normalized * speed;
            TerraProjectiles.Instance?.ShootMinion(kind, TerraParticles.ToWorld(from), new Vector2(v.x, -v.y), Mathf.Max(1, Mathf.RoundToInt(m.Damage * k)), 0f);
            m.TossCool = rest;
            m.Overhead = 10;
            m.Dir = v.x < 0f ? -1 : 1;
            m.SpriteDir = IsPirate(m.Kind) || m.Kind == Kind.VampireFrog || m.Kind == Kind.Flinx ? m.Dir : -m.Dir;
            Sound(m.Kind == Kind.BabySlime ? "NPC_Hit_1" : "Item_1", 0.35f);
            DevTosses++;
        }

        /// <summary>What a walker throws (Terraria's projectile), how fast (px a tick), what it falls by (px a tick²), its rest (ticks) and its share of the minion's damage.</summary>
        static bool Tosser(Kind k, out ProjKind kind, out float speed, out float fall, out int rest, out float share)
        {
            (kind, speed, fall, rest, share) = k switch
            {
                Kind.BabySlime => (ProjKind.GelBall, 9f, 0.07f, 35, 1f),
                Kind.Flinx => (ProjKind.SnowballProj, 10f, 0.2f, 40, 1f),
                Kind.OneEyedPirate or Kind.SoulscourgePirate or Kind.PirateCaptain => (ProjKind.ThrowingKnife, 11f, 0.2f, 40, 0.9f),
                Kind.VenomSpider or Kind.JumperSpider or Kind.DangerousSpider => (ProjKind.VenomFang, 12f, 0f, 45, 0.8f),
                Kind.VampireFrog => (ProjKind.VenomFang, 12f, 0f, 35, 0.8f),
                _ => (ProjKind.None, 0f, 0f, 0, 0f),
            };
            return kind != ProjKind.None;
        }
    }
}
