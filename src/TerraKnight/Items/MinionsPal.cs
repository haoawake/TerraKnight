using TerraKnight.Effects;
using TerraKnight.Player;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The 1.4.5 summons (Terraria px, y down): the Mushroom Boi, the Cattiva and its Trusty kind,
    /// and the Clay Bud, all AI_067_FreakingPirates' walkers (the Mushroom Boi flinging itself at
    /// high prey and bursting, the Cattivas riding on the player's head when far behind, the
    /// Trusty one leaping at what it punches); the Foxsparks and its Trusty kind (AI_026, the
    /// pygmies' way: fireballs, the Trusty one's every third a soul fire; using the staff again
    /// takes one up to breathe fire) and the Forbidden relic (AI_206) circling the player's head
    /// and firing sand gems.
    /// </summary>
    internal sealed partial class Minions
    {
        public static int DevMushroomLaunches, DevCattivaRides;

        static bool IsPal067(Kind k) => k == Kind.MushroomBoi || k == Kind.Cattiva || k == Kind.TrustyCattiva || k == Kind.ClayPot;
        static bool IsFox(Kind k) => k == Kind.Foxsparks || k == Kind.TrustyFoxsparks;

        /// <summary>
        /// DoPalAppearEffect (P:39510-39544): fading in 15 an update, and while not half in, a cyan
        /// trail of glints (306) along a bend from beside the player to it.
        /// </summary>
        void PalAppear(M m, bool particles)
        {
            if (m.Alpha <= 0f) return;
            float before = 1f - m.Alpha;
            m.Alpha = Mathf.Max(0f, m.Alpha - 15f / 255f);
            float now = 1f - m.Alpha;
            before *= 2f;
            now *= 2f;
            if (!(before < 1f && particles)) return;
            var pp = TerraParticles.Instance;
            if (pp == null) return;
            var from = pCenter + new Vector2(pDir * 10f, 0f);
            Vector2 Hermite(float t)
            {
                float t2 = t * t, t3 = t2 * t;
                var tan = new Vector2(0f, -100f);
                return (2f * t3 - 3f * t2 + 1f) * from + (t3 - 2f * t2 + t) * tan + (-2f * t3 + 3f * t2) * m.Center + (t3 - t2) * tan;
            }
            var a = Hermite(Mathf.Min(before, 1f));
            var b = Hermite(Mathf.Min(now, 1f));
            for (float k = 0f; k <= 1f; k += 0.1f)
            {
                int d = pp.NewDust(Vector2.Lerp(a, b, k) - new Vector2(4f, 4f), 0, 0, 306);
                if (d < 0) continue;
                pp[d].Position = Vector2.Lerp(a, b, k);
                pp[d].Velocity = (b - a) * 0.1f;
                pp[d].Color = FxBatch.C(100, 227, 255, 127);
                pp[d].Scale = 1f + now;
                pp[d].NoGravity = true;
                if (now >= 0.9f) pp[d].Velocity = Random.insideUnitCircle * 3f;
            }
        }

        // ------------------------------------------------------------------ AI_067: Mushroom Boi, Cattivas, Clay Bud

        void AI067Pal(M m)
        {
            bool mush = m.Kind == Kind.MushroomBoi, cat = m.Kind == Kind.Cattiva || m.Kind == Kind.TrustyCattiva, trusty = m.Kind == Kind.TrustyCattiva, pot = m.Kind == Kind.ClayPot;
            if (PlayerBuffs.Flag(m.S.Buff) && !ownerDead) m.TimeLeft = 2;
            float range = pot ? 500f : 800f;
            if (cat)
            {
                // P:50094-50101: its call 20 ticks in, and the summoning chime once.
                if (--m.SoundDelay == -20) TerraMod.Instance?.PlaySound("Custom/pal_cattiva", 0.35f);
                if (m.SoundDelay == -1) TerraMod.Instance?.PlaySound("Custom/pal_summon", 0.15f);
            }
            var idle = pCenter;
            idle.x -= ((mush ? 45f : pot ? 24f : 30f) + 10f) * pDir + (mush ? 30f : 20f) * m.MinionPos * pDir;
            m.FallThrough = pBox.yMax - 12f > m.Bottom;
            int attackLen = cat ? 16 : pot ? 30 : 15;
            bool canStrike = !mush;
            m.Friendly = mush;
            if (cat) PalAppear(m, m.Ai0 != 1f);
            bool looking = m.Ai0 == 0f || (mush && m.Ai0 == 1f) || (cat && m.Ai0 == 2f);
            var target = looking ? FindTargetInRange(m, range, true) : null;

            // The Mushroom Boi's landing (P:50605-50636): on its prey while flung, a burst, and it pops up at the player.
            if (mush)
            {
                if (m.Ai2 > 0f && target != null && InRangeOfMeOrOwner(m, target, 60f, out _, out _))
                {
                    m.Ai2 = -120f;
                    TerraProjectiles.Instance?.ShootMushroomBlast(m.Center, m.Damage, m.Knockback);
                    m.Center = pCenter;
                    m.Vel = new Vector2(-2f + Random.value * 4f, 1f + Random.value);
                }
                if (m.Ai2 < 0f) m.Ai2++;
                else if (m.Vel.y == 0f) m.Ai2 = 0f;
                else if (--m.Ai2 < 0f) m.Ai2 = 0f;
            }

            if (m.Ai0 == 1f)
            {
                // Flying back (P:50639-50760): the Cattiva hops onto the player's head instead.
                m.TileCollide = false;
                float speed = Mathf.Max(10f, Mathf.Abs(pVel.x) + Mathf.Abs(pVel.y));
                var d = pCenter - m.Center;
                float dist = d.magnitude;
                if (dist > 2000f) m.Center = pCenter;
                if (dist < 200f && ownerGrounded && m.Bottom <= pBox.yMax && !SolidBox(m.Box))
                {
                    m.Ai0 = 0f;
                    if (m.Vel.y < -6f) m.Vel.y = -6f;
                }
                if (cat && dist < 200f && ownerGrounded)
                {
                    m.Ai0 = 0f;
                    m.Center = pCenter;
                }
                if (dist >= 60f) Steer(ref m.Vel, d / Mathf.Max(dist, 1e-4f) * speed, 0.2f);
                if (m.Vel.x != 0f) m.SpriteDir = m.Vel.x > 0f ? 1 : -1;
                if (mush)
                {
                    m.Frame = 1;
                    m.Rot = m.Vel.x * 0.1f;
                }
                if (cat && m.Ai0 == 1f)
                {
                    m.Frame = pVel.y < -6f ? 1 : pVel.y >= 6f ? 2 : 0;
                    m.Center = pCenter + new Vector2(pDir * -8f, -24f);
                    m.Pos = new Vector2(Mathf.Floor(m.Pos.x), Mathf.Floor(m.Pos.y));
                    m.Dir = m.SpriteDir = pDir;
                    m.Vel = Vector2.zero;
                    m.Rot = 0f;
                    DevCattivaRides++;
                }
                if (pot)
                {
                    if (++m.FrameCounter >= 9) m.FrameCounter = 0;
                    m.Frame = 17 + m.FrameCounter / 3;
                    m.Rot = m.Vel.x * 0.1f;
                }
            }

            if (m.Ai0 == 2f && m.Ai1 < 0f)
            {
                m.Friendly = false;
                m.Ai1++;
                m.Ai1 = 0f;
                m.Ai0 = 0f;
                return;
            }
            if (m.Ai0 == 2f)
            {
                m.SpriteDir = m.Dir;
                m.Rot = 0f;
                if (cat || pot)
                {
                    // The punch (P:50880-50910): stopping short, its frames 3-6, the Cattiva's puffs of dust; the Trusty one homes on its prey.
                    m.Friendly = true;
                    if (m.Ai1 == attackLen - 1 && m.Vel.y == 0f) m.Vel.x *= 0.1f;
                    if (m.Ai1 == attackLen - 1 && trusty) m.Vel *= 0.1f;
                    m.Frame = 3 + (int)(attackLen - m.Ai1) / (attackLen / 4);
                    if (cat && (int)m.Ai1 % 4 == 0)
                        for (int i = 0; i < 3; i++)
                        {
                            int dd = TerraParticles.Instance != null ? TerraParticles.Instance.NewDust(m.Center + new Vector2(m.Dir * 8f, -4f), 8, 8, 16, -m.Dir * 2f, 0f, 127) : -1;
                            if (dd < 0) continue;
                            TerraParticles.Instance[dd].Velocity *= 0.5f;
                            TerraParticles.Instance[dd].Scale *= 0.8f;
                            TerraParticles.Instance[dd].Velocity.y -= 0.2f;
                        }
                    if (trusty && target != null)
                    {
                        float pp = m.Ai1 / attackLen;
                        float ping = pp < 0.5f ? pp * 2f : 2f - pp * 2f;
                        var to = target.Center + new Vector2(0f, ping * -10f) - m.Center;
                        m.Vel = Vector2.ClampMagnitude(to, 20f);
                    }
                }
                m.Vel.y = Mathf.Min(m.Vel.y + 0.4f, 10f);
                if (--m.Ai1 <= 0f)
                {
                    m.Ai1 = 0f;
                    m.Ai0 = 0f;
                    return;
                }
            }

            if (m.Ai0 == 5f)
            {
                // A far boss: straight at it at 16 px a tick, punching on arrival (P:50954-50995).
                if (target == null)
                {
                    m.Ai0 = m.Ai1 = m.LocalAi0 = 0f;
                    return;
                }
                float r = Mathf.Min(target.Box.width, target.Box.height) / 2f * 0.5f;
                float th = m.MinionPos * 2f * Mathf.PI / 0.5862069f;
                var spot = target.Center + new Vector2(Mathf.Cos(th), Mathf.Sin(th)) * r;
                m.Vel = Vector2.ClampMagnitude(spot - m.Center, 16f);
                m.Dir = m.SpriteDir = m.Vel.x >= 0f ? 1 : -1;
                if ((spot - m.Center).magnitude / 16f < 2f)
                {
                    m.Ai0 = 2f;
                    m.Ai1 = attackLen;
                    m.Vel = m.Vel * 0.3f + Random.insideUnitCircle.normalized * 4f;
                    m.Vel.y = -3f;
                    m.Center = spot;
                    return;
                }
                if (++m.Ai1 >= 180f) m.Ai0 = m.Ai1 = 0f;
                return;
            }

            if (mush && target == null) m.LocalAi2 = 0f;
            var goal = idle;
            if (target != null)
            {
                var tc = target.Center;
                var aim = tc;
                var tCentre = tc;
                if (mush) aim += new Vector2(27f, 0f) * m.Dir;
                goal = aim;
                if (pot || cat)
                {
                    goal = new Vector2(tc.x > m.Center.x ? tc.x + 10f : tc.x - 10f, Mathf.Clamp(m.Center.y, target.Box.yMin + m.H / 2f, target.Box.yMax - m.H / 2f));
                    aim = tCentre = goal;
                }
                if (InRangeOfMeOrOwner(m, target, range, out _, out _))
                {
                    TryJumpPal(m, NpcVelocity(target), aim, tCentre);
                    if (cat && m.Ai1 == 0f)
                    {
                        // P:51083-51115: a hop up off a puff when its prey is well above.
                        if (m.Vel.y > 1f && tCentre.y - m.Bottom < -100f)
                        {
                            m.Vel.y = -Mathf.Min(Mathf.Sqrt((m.Center.y - aim.y) * 2f * 0.4f) + 1f, 26f);
                            m.Vel.x += (Random.value * 2f - 1f) * Random.value * 5f;
                            CloudPuff(m);
                        }
                        if ((aim - m.Center).magnitude > 600f && target.Boss)
                        {
                            m.Ai0 = 5f;
                            m.Ai1 = 0f;
                            return;
                        }
                    }
                    if (canStrike && Vector2.Distance(m.Center, goal) < 20f && m.Ai0 != 2f)
                    {
                        if (m.Vel.magnitude > 10f) m.Vel = m.Vel.normalized * 10f;
                        m.Ai0 = 2f;
                        m.Ai1 = attackLen;
                        m.Dir = aim.x > m.Center.x ? 1 : -1;
                    }
                }
            }

            if (m.Ai0 == 0f && target == null)
            {
                var d = pCenter - m.Center;
                if (d.magnitude > 2000f) m.Center = pCenter;
                else if (d.magnitude > 500f || Mathf.Abs(d.y) > 300f)
                {
                    if ((m.Vel.y > 0f && d.y < 0f) || (m.Vel.y < 0f && d.y > 0f)) m.Vel.y = 0f;
                    m.Ai0 = 1f;
                    if (cat) m.Alpha = 1f;
                    return;
                }
            }
            if (m.Ai0 != 0f) return;
            if (target == null) goal = IdleGoal(m, idle);
            WalkPal(m, goal, target, mush, cat, trusty, pot);
        }

        /// <summary>
        /// AI_067_FreakingPirates_TryJumpingToTarget (P:52110-52166): the Mushroom Boi, with prey
        /// 160 px over it (or flying back), marks it (an alert over its head) for 15 updates then
        /// flings itself at 32 px a tick where it'll meet it; any of them jumps for prey more than
        /// 30 px over it, standing.
        /// </summary>
        void TryJumpPal(M m, Vector2 targetVel, Vector2 toTarget, Vector2 targetCentre)
        {
            bool mush = m.Kind == Kind.MushroomBoi;
            m.FallThrough = targetCentre.y > m.Bottom;
            bool standing = m.Vel.y == 0f;
            bool flung = false;
            if (mush && (toTarget.y < m.Center.y - 160f || m.Ai0 == 1f) && m.Ai2 == 0f)
            {
                if (m.LocalAi2 == 0f) FxParticles.MushroomTargetFound(new Vector2(m.Center.x, m.Pos.y));
                if (++m.LocalAi2 >= 15f)
                {
                    const float speed = 32f;
                    m.Vel = Intercept(m.Center, speed, targetCentre, targetVel, out float time) && time < 120f
                        ? (targetCentre + targetVel * time - m.Center).normalized * speed
                        : (targetCentre.sqrMagnitude > 1e-6f ? targetCentre.normalized : Vector2.down) * speed;
                    m.Ai2 = 30f;
                    m.LocalAi2 = 0f;
                    DevMushroomLaunches++;
                }
                flung = true;
            }
            if (toTarget.y < m.Center.y - 30f && standing && !flung)
                m.Vel.y = -Mathf.Min(Mathf.Sqrt((m.Center.y - toTarget.y) * 2f * 0.4f), 26f);
        }

        /// <summary>Utils.GetChaseResults: when a chaser at this speed can meet a moving target (and whether it can).</summary>
        static bool Intercept(Vector2 from, float speed, Vector2 at, Vector2 vel, out float time)
        {
            var d = at - from;
            float a = vel.sqrMagnitude - speed * speed, b = 2f * Vector2.Dot(d, vel), c = d.sqrMagnitude;
            time = 0f;
            if (Mathf.Abs(a) < 1e-6f)
            {
                if (Mathf.Abs(b) < 1e-6f) return false;
                time = -c / b;
                return time > 0f;
            }
            float disc = b * b - 4f * a * c;
            if (disc < 0f) return false;
            float s = Mathf.Sqrt(disc), t1 = (-b - s) / (2f * a), t2 = (-b + s) / (2f * a);
            time = t1 > 0f && t2 > 0f ? Mathf.Min(t1, t2) : Mathf.Max(t1, t2);
            return time > 0f;
        }

        /// <summary>
        /// AI_067's walk (P:51300-52055) for these: slower on prey (the Mushroom Boi 6 at 0.8, the
        /// Cattiva 6 at 0.3, the Trusty one 12 at 9, the Clay Bud 3 at 0.2), never hopping as it
        /// goes, only up walls and toward a goal well above; a flung Mushroom Boi isn't held to its
        /// top speed nor pulled down extra. Their frames: standing 0, walking (Mushroom Boi 2-9 or,
        /// chasing, 10-15; Cattiva 7-12; Clay Bud 8-14), in the air 1 (2 falling; chasing Mushroom Boi 13).
        /// </summary>
        void WalkPal(M m, Vector2 goal, Npc target, bool mush, bool cat, bool trusty, bool pot)
        {
            m.TileCollide = true;
            float accel = 0.5f, soft = 4f, max = 4f, creep = 0.1f;
            if (target != null)
            {
                if (mush) { accel = 0.8f; soft = max = 6f; }
                if (cat) { accel = 0.3f; soft = max = 6f; }
                if (trusty) { accel = 9f; soft = 12f; max = 9f; }
                if (pot) { accel = 0.2f; soft = max = 3f; }
            }
            float ps = Mathf.Abs(pVel.x) + Mathf.Abs(pVel.y);
            if (ps > max)
            {
                max = ps;
                accel = 0.7f;
            }
            float dx = goal.x - m.Center.x;
            var dGoal = goal - m.Center;
            int wish = 0;
            bool jump = false;
            if (Mathf.Abs(dx) > 5f)
            {
                wish = dx > 0f ? 1 : -1;
                bool under = wish > 0 ? m.Vel.x < soft : m.Vel.x > -soft;
                m.Vel.x += wish * (under ? accel : creep);
            }
            else
            {
                m.Vel.x *= 0.9f;
                if (Mathf.Abs(m.Vel.x) < 2f * accel) m.Vel.x = 0f;
            }
            bool bigGap = Mathf.Abs(dGoal.x) >= 64f || (dGoal.y <= -48f && Mathf.Abs(dGoal.x) >= 8f);
            int ji = TileOf(m.Center.x) + wish + (int)m.Vel.x, jj = TileOf(m.Center.y);
            if (wish != 0 && bigGap)
            {
                int j0 = TileOf(m.Pos.y);
                for (int j = j0; j <= j0 + m.H / 16; j++)
                    if (SolidTile(ji, j)) jump = true;
            }
            StepUp(m, m.FallThrough);
            bool flung = mush && m.Ai2 != 0f;
            float extraG = Mathf.Clamp01(dGoal.y / 100f) * Mathf.Clamp01((m.Vel.y + 2f) / (-6f + 2f));
            if (flung) extraG = 0f;
            if (m.Vel.y == 0f)
            {
                if (jump)
                {
                    int fj = TileOf(m.Bottom);
                    foreach (float fx in new[] { m.Pos.x + 1f, m.Center.x, m.Pos.x + m.W - 1f })
                    {
                        int fi = TileOf(fx);
                        if (!SolidTile(fi, fj) && !PlatformTile(fi, fj)) continue;
                        m.Vel.y = WallJump(ji, jj);
                        break;
                    }
                }
                if (goal.y - m.Center.y < -48f)
                {
                    float h = m.Center.y - goal.y;
                    m.Vel.y = h < 60f ? -6f : h < 80f ? -7f : h < 100f ? -8f : h < 120f ? -9f : h < 140f ? -10f : h < 160f ? -11f
                        : h < 190f ? -12f : h < 210f ? -13f : h < 270f ? -14f : h < 310f ? -15f : -16f;
                }
            }
            if (!flung) m.Vel.x = Mathf.Clamp(m.Vel.x, -max, max);
            if (m.Ai0 != 2f)
            {
                m.Dir = m.Vel.x < 0f ? -1 : m.Vel.x > 0f ? 1 : (pCenter.x > m.Center.x ? 1 : -1);
                if (m.Vel.x > accel && wish == 1) m.Dir = 1;
                if (m.Vel.x < -accel && wish == -1) m.Dir = -1;
            }
            m.SpriteDir = m.Dir;
            m.Rot = 0f;
            if (mush && target != null)
            {
                if (m.Vel.y != 0f)
                {
                    m.Frame = 13;
                    m.FrameCounter = 0;
                }
                else
                {
                    if (++m.FrameCounter > 3)
                    {
                        m.Frame++;
                        m.FrameCounter = 0;
                    }
                    if (m.Frame < 10 || m.Frame > 15) m.Frame = 10;
                }
            }
            else if (m.Vel.y == 0f)
            {
                int walkFrom = mush ? 2 : cat ? 7 : 8, walkEnd = mush ? 10 : cat ? 13 : 15;
                if (m.Vel.x == 0f)
                {
                    m.Frame = 0;
                    m.FrameCounter = 0;
                }
                else if (Mathf.Abs(m.Vel.x) >= 0.5f)
                {
                    m.FrameCounter += (int)Mathf.Abs(m.Vel.x) + 1;
                    if (!mush && m.Frame < walkFrom) m.Frame = walkFrom;
                    if (m.FrameCounter > 10)
                    {
                        m.Frame++;
                        m.FrameCounter = 0;
                    }
                    if (mush ? (m.Frame < walkFrom || m.Frame >= walkEnd) : m.Frame >= walkEnd) m.Frame = walkFrom;
                }
                else
                {
                    m.Frame = 0;
                    m.FrameCounter = 0;
                }
            }
            else
            {
                m.FrameCounter = 0;
                m.Frame = mush ? 1 : m.Vel.y > 0f ? 2 : 1;
            }
            m.Vel.y = Mathf.Min(m.Vel.y + 0.4f + extraG, 10f);
        }

        // ------------------------------------------------------------------ AI_026: the Foxsparks

        /// <summary>
        /// TryUsingFoxsparksAbility (Pl:43677-43700): with one of this kind out, the staff takes it
        /// up to breathe fire instead of summoning another; its roar if it hasn't for a while.
        /// </summary>
        bool TryFoxsparksAbility(ItemDef staff)
        {
            var kind = staff.Minion == MinionKind.TrustyFoxsparks ? Kind.TrustyFoxsparks : Kind.Foxsparks;
            foreach (var m in live)
            {
                if (m.Dead || m.Kind != kind) continue;
                m.Ai0 = 1000f;
                m.Ai1 = 0f;
                m.LocalAi0 = m.LocalAi1 = 0f;
                if (m.SoundDelay <= -320)
                {
                    m.SoundDelay = -21;
                    TerraMod.Instance?.PlaySound("Custom/pal_foxparks_attack", 0.18f);
                }
                return true;
            }
            return false;
        }

        /// <summary>Whether the Foxsparks' staff is still being used (player.channel with it in hand).</summary>
        static bool FoxChannel(Kind kind)
        {
            var mod = TerraMod.Instance;
            if (mod == null || !mod.UseHeld) return false;
            var held = mod.Inventory.Current.Type;
            return kind == Kind.TrustyFoxsparks ? held == ItemType.TrustyFoxsparksStaff : held == ItemType.FoxsparksStaff;
        }

        void AI026Fox(M m)
        {
            bool trusty = m.Kind == Kind.TrustyFoxsparks;
            if (PlayerBuffs.Flag(m.S.Buff) && !ownerDead) m.TimeLeft = 2;
            // P:59118-59135: its call 20 ticks in and the summoning chime once; a warm light.
            if (--m.SoundDelay == -20) TerraMod.Instance?.PlaySound("Custom/pal_foxparks", 0.35f);
            if (m.SoundDelay == -1) TerraMod.Instance?.PlaySound("Custom/pal_summon", 0.15f);
            FxBatch.Instance?.Light(m.Center, new Color(0.5f, 0.3f, 0.1f), 2.5f);
            PalAppear(m, m.Ai0 != 1f);

            if (m.Ai0 >= 1000f)
            {
                // Held (P:60012-60098): at the player, turned to the cursor, a breath of fire (1106) every 5 ticks while the staff is used.
                m.LocalAi0 = 0f;
                bool channel = FoxChannel(m.Kind);
                if (m.Ai0 == 1000f && channel)
                {
                    var aim = TerraParticles.ToPx(TerraMod.Instance.DevAim ?? TerraMod.Instance.AimPoint) - pCenter;
                    m.Vel = (aim.sqrMagnitude > 1e-6f ? aim.normalized : new Vector2(0f, -1f)) * 10f;
                }
                m.Dir = m.Vel.x > 0f ? 1 : -1;
                if (m.Vel.x == 0f) m.Dir = pDir;
                m.Center = pCenter;
                // rotation = its aim + π + (π while its sprite faced left): along the aim facing right, a half turn more facing left.
                m.Rot = Mathf.Atan2(m.Vel.y, m.Vel.x) + (m.Dir == 1 ? 0f : Mathf.PI);
                m.SpriteDir = -m.Dir;
                m.TimeLeft = 2;
                if (channel) TerraMod.Instance?.KeepUsing(ItemDb.Get(trusty ? ItemType.TrustyFoxsparksStaff : ItemType.FoxsparksStaff), new Vector2(m.Vel.x, -m.Vel.y));
                if (m.Ai0 == 1000f && channel)
                {
                    var v = m.Vel.normalized * (trusty ? 10.5f : 5f);
                    TerraProjectiles.Instance?.ShootFoxFlame(m.Center, v, m.Damage, m.Knockback);
                }
                if (++m.Ai0 >= 1004f)
                {
                    m.Ai0 = FoxChannel(m.Kind) ? 1000f : 0f;
                    m.LocalAi0 = 30f;
                }
                m.Frame = 0;
                return;
            }

            // Lava sends it home (P:59165-59172), as does being too far.
            float reach = 500f + 40f * m.MinionPos;
            if (m.LocalAi0 > 0f) reach += 500f;
            var d = pCenter - m.Center;
            float dist = d.magnitude;
            if (dist > 2000f) m.Center = pCenter;
            else if (m.Ai0 != 1f && (dist > reach || (Mathf.Abs(d.y) > 300f && m.LocalAi0 <= 0f)))
            {
                if (d.y > 0f && m.Vel.y < 0f) m.Vel.y = 0f;
                if (d.y < 0f && m.Vel.y > 0f) m.Vel.y = 0f;
                m.Alpha = 1f;
                m.Ai0 = 1f;
            }
            if (m.Ai0 == 1f)
            {
                // Riding on the player's head (P:61640-61656) until he lands near enough.
                m.TileCollide = false;
                m.Frame = 0;
                m.FrameCounter = 0;
                m.Center = pCenter + new Vector2(0f, -10f);
                m.Pos = new Vector2(Mathf.Floor(m.Pos.x), Mathf.Floor(m.Pos.y));
                m.Rot = 0f;
                m.Dir = m.SpriteDir = pDir;
                m.Vel = new Vector2(m.Dir, 0f);
                if (ownerGrounded && dist < 200f)
                {
                    m.Ai0 = 0f;
                    m.Vel = Vector2.zero;
                }
                return;
            }

            float goalX = pCenter.x - (60f + 30f * m.MinionPos) * pDir;
            bool left = m.Center.x > goalX + 10f, right = m.Center.x < goalX - 10f;
            m.FallThrough = pBox.yMax - 12f > m.Bottom;
            m.LocalAi0 = Mathf.Max(0f, m.LocalAi0 - 1f);
            FoxThink(m, trusty, ref left, ref right);

            m.TileCollide = true;
            float speed = 6f, accel = 0.2f;
            float ps = Mathf.Abs(pVel.x) + Mathf.Abs(pVel.y);
            if (ps > 6f)
            {
                speed = ps;
                accel = 0.3f;
            }
            if (left) m.Vel.x -= m.Vel.x > -3.5f ? accel : accel * 0.25f;
            else if (right) m.Vel.x += m.Vel.x < 3.5f ? accel : accel * 0.25f;
            else
            {
                m.Vel.x *= 0.9f;
                if (Mathf.Abs(m.Vel.x) <= accel) m.Vel.x = 0f;
            }
            // Breathing out (ai1), it stands, sliding to a stop.
            if (m.Ai1 != 0f && m.Vel.y == 0f) m.Vel.x *= 0.7f;
            bool wallAhead = false;
            int wi = TileOf(m.Center.x) + (left ? -1 : 0) + (right ? 1 : 0) + (int)m.Vel.x, wj = TileOf(m.Center.y);
            if (left || right) wallAhead = SolidTile(wi, wj);
            StepUp(m, m.FallThrough);
            if (m.Vel.y == 0f)
            {
                int fi = TileOf(m.Center.x), fj = TileOf(m.Bottom);
                if (wallAhead && (SolidTile(fi, fj) || PlatformTile(fi, fj))) m.Vel.y = WallJump(wi, wj);
            }
            m.Vel.x = Mathf.Clamp(m.Vel.x, -speed, speed);
            if (m.Vel.x < 0f) m.Dir = -1;
            if (m.Vel.x > 0f) m.Dir = 1;
            if (m.Vel.x > accel && right) m.Dir = 1;
            if (m.Vel.x < -accel && left) m.Dir = -1;
            m.SpriteDir = m.Dir;
            bool still = m.Pos.x - m.OldPos.x == 0f;
            FoxFrames(m, still, trusty);
            m.Vel.y = Mathf.Min(m.Vel.y + 0.4f, 10f);
        }

        /// <summary>
        /// AI_026's pygmy branch for the Foxsparks (P:62567-62740): every 42 ticks (the Trusty one's
        /// 30), once fully in and not just shot, a fireball (1097) at the nearest enemy in sight
        /// within 800 px (+40 per minion, the Trusty one's 360 more), led by its velocity, from 10 px
        /// over its middle at 11 px with a lob but no scatter up or down; further than 450 px across
        /// (630) it walks closer. The Trusty one's every third is a soul fire.
        /// </summary>
        void FoxThink(M m, bool trusty, ref bool left, ref bool right)
        {
            float extra = 40f * m.MinionPos + (trusty ? 360f : 0f);
            int cooldown = trusty ? 30 : 42;
            if (m.Ai1 > 0f) m.Ai1--;
            bool ready = m.Ai1 <= 0f && m.Alpha <= 0f && m.LocalAi0 <= 0f;
            if (ready)
            {
                float bestVis = 100000f, bestAny = 100000f;
                Npc target = null;
                Vector2 aim = m.Pos;
                var owner = OwnerTarget;
                if (owner != null)
                {
                    float dT = Manhattan(m.Center, owner.Center);
                    if (dT <= bestAny)
                    {
                        bestAny = dT;
                        aim = owner.Center;
                    }
                    if (ClearLine(m.Center, owner.Center))
                    {
                        bestVis = dT;
                        aim = owner.Center;
                        target = owner;
                    }
                }
                if (target == null)
                    foreach (var n in npcs)
                    {
                        float dN = Manhattan(m.Center, n.Center);
                        if (dN >= bestVis || !CanReach(m, n)) continue;
                        var led = n.Center + NpcVelocity(n) * 20f;
                        if (target == null && dN <= bestAny)
                        {
                            bestAny = dN;
                            aim = led;
                        }
                        if (ClearLine(m.Center, n.Center))
                        {
                            bestVis = dN;
                            aim = led;
                            target = n;
                        }
                    }
                if (target == null && bestAny < bestVis) bestVis = bestAny;
                float sense = World.Areas.LevelOf(GameManager.UnsafeInstance?.sceneName) >= 1 ? 200f : 400f;
                if (target == null && bestVis < sense + extra)
                {
                    float dx = aim.x - m.Center.x;
                    if (dx < -5f) { left = true; right = false; }
                    else if (dx > 5f) { right = true; left = false; }
                }
                else if (target != null && bestVis < 800f + extra)
                {
                    m.LocalAi0 = 60f;
                    float dx = aim.x - m.Center.x;
                    float far = trusty ? 630f : 450f;
                    if (dx > far || dx < -far)
                    {
                        if (dx < -50f) { left = true; right = false; }
                        else if (dx > 50f) { right = true; left = false; }
                    }
                    else
                    {
                        m.Ai1 = cooldown;
                        var from = m.Center - new Vector2(0f, 10f);
                        float vx = aim.x - from.x + Random.Range(-20, 21);
                        float lob = Mathf.Abs(vx) * 0.1f * Random.Range(0, 100) * 0.001f;
                        float vy = aim.y - from.y - lob;
                        var v = new Vector2(vx, vy);
                        v = v.sqrMagnitude > 0f ? v.normalized * 11f : new Vector2(11f * m.Dir, 0f);
                        m.Dir = v.x < 0f ? -1 : 1;
                        from += v;
                        m.LocalAi0 = cooldown;
                        float kind = 0f;
                        if (trusty)
                        {
                            m.LocalAi2--;
                            kind = 3f;
                            if (m.LocalAi2 <= -3f)
                            {
                                m.LocalAi2 = 0f;
                                kind = 1f;
                            }
                        }
                        TerraProjectiles.Instance?.ShootFoxFireball(from, v, m.Damage, m.Knockback, kind);
                    }
                }
            }
            if (m.Ai1 != 0f)
            {
                left = false;
                right = false;
            }
            else if (m.LocalAi0 == 0f) m.Dir = pDir;
        }

        /// <summary>
        /// P:64729-64822: breathing out, frames 36-41 (forward and back, 3 ticks each) with sparks of
        /// fire off its mouth; on the ground 18-25 standing, 28-35 running; in the air 27.
        /// </summary>
        static void FoxFrames(M m, bool still, bool trusty)
        {
            if (m.Ai1 > 0f)
            {
                if (m.Frame < 36 || m.Frame >= 42)
                {
                    m.FrameCounter = 0;
                    m.Frame = 36;
                }
                if (m.Ai1 == 40f || (trusty && m.Ai1 == 30f)) m.FrameCounter = 0;
                m.FrameCounter++;
                int n = m.FrameCounter / 3;
                if (n >= 6) n = 11 - n;
                if (n < 0) n = 0;
                m.Frame = 36 + n;
                if (m.Frame != 36 && m.FrameCounter < 10 && TerraParticles.Instance != null)
                {
                    int d = TerraParticles.Instance.NewDust(new Vector2(m.Center.x - 2f - m.SpriteDir * 16f, m.Bottom - 8f), 4, 4, 6, m.SpriteDir * 2f, -0.25f);
                    if (d >= 0)
                    {
                        TerraParticles.Instance[d].NoGravity = true;
                        TerraParticles.Instance[d].Scale = 1.4f;
                        TerraParticles.Instance[d].Velocity *= 4f;
                    }
                }
            }
            else if (m.Vel.y == 0f)
            {
                if (still)
                {
                    if (m.Frame < 18 || m.Frame >= 26) m.Frame = 18;
                    if (++m.FrameCounter >= 4)
                    {
                        m.FrameCounter = 0;
                        if (++m.Frame >= 26) m.Frame = 18;
                    }
                }
                else if (Mathf.Abs(m.Vel.x) > 0.8f)
                {
                    m.FrameCounter += (int)Mathf.Abs(m.Vel.x) + 1;
                    if (m.Frame < 28 || m.Frame >= 36) m.Frame = 28;
                    if (m.FrameCounter >= 10)
                    {
                        m.FrameCounter = 0;
                        if (++m.Frame >= 36) m.Frame = 28;
                    }
                }
                else
                {
                    m.Frame = 18;
                    m.FrameCounter = 0;
                }
            }
            else
            {
                m.FrameCounter = 0;
                m.Frame = 27;
            }
        }

        // ------------------------------------------------------------------ AI_206: the Forbidden relic

        /// <summary>
        /// AI_206 (P:38183-38326): beside the player's head (spread round with the others, bobbing);
        /// with prey within 800 px, out to 200 px of it, turned to it, a gem (1120) at 32 px every
        /// 120-ish ticks after a 30 tick wind-up (slowing as it winds), kicked back by the shot.
        /// </summary>
        void AI206(M m)
        {
            if (PlayerBuffs.Flag(m.S.Buff) && !ownerDead) m.TimeLeft = 2;
            m.TileCollide = false;
            var home = new Vector2(pCenter.x, pBox.yMin) + new Vector2(-pDir * 16f, -25f);
            if (Vector2.Distance(m.Center, pCenter) > 2000f) m.Center = pCenter;
            var target = FindTargetInRange(m, 800f, false);
            int index = 0, total = 0;
            foreach (var o in live)
            {
                if (o.Dead || o.Kind != Kind.ForbiddenRelic) continue;
                if (o == m) index = total;
                total++;
            }
            total = Mathf.Max(1, total);
            float slice = Mathf.PI * 2f / total, period = total * 0.66f;
            var spread = new Vector2(30f, 6f) / 5f * (total - 1);
            float turn = total % 2 == 0 ? Mathf.PI / 2f : Mathf.PI;
            var round = RotPx(new Vector2(0f, 1f), slice * index + turn);
            round.y *= 1.2f;
            if (pDir == -1) home.x -= 6f;
            home += new Vector2(round.x * spread.x, round.y * spread.y);
            var bob = RotPx(new Vector2(0f, 1f), slice * index + Time.time % period / period * Mathf.PI * 2f);
            home.y += bob.y * 4f;
            home = new Vector2(Mathf.Floor(home.x), Mathf.Floor(home.y));
            if (m.Ai0 > 0f) m.Ai0--;
            m.Dir = m.SpriteDir = pDir;
            if (target != null)
            {
                home = Vector2.MoveTowards(target.Center, home, 200f);
                float p2 = total * 0.306f;
                home += RotPx(new Vector2(0f, 1f), slice * index + Time.time % p2 / p2 * Mathf.PI * 2f) * 20f;
                m.Dir = m.SpriteDir = target.Center.x > m.Center.x ? 1 : -1;
                var to = target.Center - m.Center;
                m.Rot = Mathf.Atan2(to.y, to.x) + (m.SpriteDir == -1 ? Mathf.PI : 0f);
            }
            else m.Rot = 0f;
            const int cycle = 150, windUp = 30, settle = 10;
            if (m.Ai0 == 0f && target != null) m.Ai0 = cycle;
            if (m.Ai0 == cycle - windUp && target != null)
            {
                m.Ai0 = Random.Range(cycle - windUp - settle - 5, cycle - windUp - settle + 5) - windUp;
                var v = Vector2.ClampMagnitude(target.Center - m.Center, 32f);
                if (v.sqrMagnitude > 1e-6f) v = v.normalized * 32f;
                TerraProjectiles.Instance?.ShootForbiddenGem(m.Center, v, m.Damage, m.Knockback);
                m.Vel = -v * 0.3f;
                return;
            }
            float speed = 16f;
            if (m.Ai0 > cycle - windUp) speed *= Mathf.InverseLerp(cycle - windUp, cycle, m.Ai0);
            var want = Vector2.ClampMagnitude(home - m.Center, speed);
            m.Vel = Vector2.MoveTowards(m.Vel, want, 2f);
            if (m.Ai0 == cycle - windUp + 4) m.Frame = 5;
            if (m.Frame >= 5 && m.Frame <= 8)
            {
                if (++m.FrameCounter >= 4)
                {
                    m.FrameCounter = 0;
                    if (++m.Frame >= 9) m.Frame = 0;
                }
                if (target == null)
                {
                    m.FrameCounter = 0;
                    m.Frame = 0;
                }
            }
            if (m.Frame > 4) return;
            if (m.Frame == 0 && Random.Range(0, 7) != 0) m.FrameCounter = 0;
            if (++m.FrameCounter >= 4)
            {
                m.FrameCounter = 0;
                if (++m.Frame >= 5) m.Frame = 0;
            }
        }

        static Vector2 RotPx(Vector2 v, float a) => new Vector2(v.x * Mathf.Cos(a) - v.y * Mathf.Sin(a), v.x * Mathf.Sin(a) + v.y * Mathf.Cos(a));

        // ------------------------------------------------------------------ drawing

        /// <summary>
        /// M:34836-34856: their frame about its middle (2 px up; the Cattivas 5, the Clay Bud 9),
        /// mirrored by facing, see-through as it fades in; the Mushroom Boi's glow (354) over it.
        /// </summary>
        static void DrawPal067(FxBatch fx, M m)
        {
            var tex = TerrariaAssets.Texture(m.S.Texture);
            if (tex == null) return;
            int w = tex.width, fh = tex.height / m.S.Frames;
            float up = m.Kind == Kind.ClayPot ? 9f : m.Kind == Kind.MushroomBoi ? 2f : 5f;
            var at = m.Center + new Vector2(0f, m.GfxOffY - up);
            float o = 1f - m.Alpha;
            fx.Draw(FxBatch.Frame(m.S.Texture, 0, fh * m.Frame, w, fh, w / 2f, fh / 2f), at, Lit * o, m.Rot, m.Scale, flipX: m.SpriteDir == -1);
            if (m.Kind == Kind.MushroomBoi)
                fx.Draw(FxBatch.Frame("Glow_354", 0, fh * m.Frame, w, fh, w / 2f, fh / 2f), at, FxBatch.C(250, 250, 250, 0) * o, m.Rot, m.Scale, flipX: m.SpriteDir == -1);
        }

        /// <summary>
        /// M:33776-33800, 33844-33854, 34625-34640: its frame from a sheet of six columns of nine
        /// (62×44 every 64×46), 11 px up and its pivot 8 px back; its glow layer twice, jittered;
        /// held to breathe, the open-mouthed one (Extra 303) turned to its aim.
        /// </summary>
        static void DrawFox(FxBatch fx, M m)
        {
            float o = 1f - m.Alpha;
            bool flip = m.SpriteDir == -1;
            var at = m.Center + new Vector2(0f, m.GfxOffY - 11f);
            if (m.Ai0 >= 1000f)
            {
                // XNA's origin (30 ∓ 10, 12) mirrored with the sprite comes to the same pivot either way (40, 12).
                var held = FxBatch.Frame("Extra_303", 0, 0, 60, 36, 40f, 12f);
                fx.Draw(held, m.Center, Lit * o, m.Rot, m.Scale, flipX: !flip, overPlayer: true);
                return;
            }
            int col = m.Frame / 9, row = m.Frame % 9;
            // XNA's origin (31 ± 8, 22), mirrored with the sprite, is the same pivot either way: 39.
            const float ox = 39f;
            fx.Draw(FxBatch.Frame(m.S.Texture, col * 64, row * 46, 62, 44, ox, 22f), at, Lit * o, m.Rot, m.Scale, flipX: flip);
            for (int k = 0; k < 2; k++)
            {
                var jitter = new Vector2(Random.Range(-1, 2), Random.Range(-1, 2));
                fx.Draw(FxBatch.Frame(m.S.Texture, col * 64, row * 46 + 414 * 2, 62, 44, ox, 22f), at + jitter, new Color(1f, 1f, 1f, 0f) * 0.5f * o, m.Rot, m.Scale, flipX: flip);
            }
        }

        /// <summary>M:33741ff, 34654-34668: its frame about its middle, turned to its prey, and a golden sparkle as it winds up a shot.</summary>
        static void DrawForbidden(FxBatch fx, M m)
        {
            var tex = TerrariaAssets.Texture(m.S.Texture);
            if (tex == null) return;
            int w = tex.width, fh = tex.height / m.S.Frames;
            fx.Draw(FxBatch.Frame(m.S.Texture, 0, fh * m.Frame, w, fh, w / 2f, fh / 2f), m.Center, Lit, m.Rot, m.Scale, flipX: m.SpriteDir == -1);
            const float cycle = 150f, windUp = 30f;
            float k = m.Ai0 >= cycle - windUp ? Mathf.InverseLerp(cycle, cycle - windUp, m.Ai0) : Mathf.InverseLerp(cycle - windUp - 10f, cycle - windUp, m.Ai0);
            if (k > 0f)
            {
                var at = m.Center + RotPx(new Vector2(m.SpriteDir * 2f, 0f), m.Rot);
                FxParticles.DrawStarSparkle(fx, k, at, Color.white, FxBatch.C(255, 215, 0), k, 0f, 1f, 1f, 1.2f, 0f, Vector2.one, Vector2.one, false);
            }
        }
    }
}
