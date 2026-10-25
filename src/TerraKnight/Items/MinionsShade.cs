using TerraKnight.Effects;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The Shade Soul Staff's little shade (Items/Shade.cs), in the pack's eight frames (32×32: 0-1 idle, 2-3 gathering,
    /// 4-5 charging, 6-7 recovering) and its charged wave (80×48). Ai0 its state, Ai1 the state's clock, Ai2 1 once its wave
    /// has struck, LocalAi1 1 while it flies back to its owner, LocalAi2 its own clock; TargetId the enemy it goes for (0 none: an instance id, negative for what the mod made, never 0). It
    /// follows its owner as the wandering soul does (behind the shoulder, back when they go far, taken along to another
    /// room). With an enemy near them it gathers itself a moment, charges at it, and within Shade.StrikeRangePx turns into
    /// a shock wave that strikes that enemy once (the minion's own touch, Minions.Damage), flies on past it, turns back into
    /// the shade flying off, and rests Shade.ShadeRest before the next. Any other enemy it meets on the way, charging or
    /// as the wave, takes Shade.PathShare of a blow (the user's: hurt in passing; ShadeMayHit).
    /// </summary>
    internal sealed partial class Minions
    {
        const float ShadeGather = 10f, ShadeDashMax = 70f, ShadeWaveMax = 34f, ShadeWaveOn = 10f, ShadeRecover = 16f;
        public static int DevShadeCharges, DevShadeWaves, DevShadeStrikes, DevShadeRecalls;

        static void ShadeAppears(M m)
        {
            for (int k = 0; k < 18; k++)
            {
                float a = k / 18f * Mathf.PI * 2f;
                int i = DustAt(m.Center - new Vector2(4f, 4f), 8, 8, k % 4 == 0 ? 240 : 109, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.6f, 0, 1.2f);
                if (i >= 0) TerraParticles.Instance[i].NoGravity = true;
            }
        }

        static void ShadeMote(Vector2 at, Vector2 vel, float scale, int type = 109)
        {
            int i = DustAt(at - Vector2.one * 3f, 6, 6, type, vel, 0, scale);
            if (i >= 0) TerraParticles.Instance[i].NoGravity = true;
        }

        /// <summary>The shade's quarry: the owner's chosen one, else the enemy nearest it, within SoulRange of its owner; or null.</summary>
        Npc ShadeQuarry(M m)
        {
            var t = OwnerTarget;
            if (t != null && Vector2.Distance(pCenter, t.Center) < SoulRange * 1.4f) return t;
            Npc best = null;
            float bestD = float.MaxValue;
            foreach (var n in npcs)
            {
                if (Vector2.Distance(pCenter, n.Center) >= SoulRange) continue;
                float d = Vector2.Distance(m.Center, n.Center);
                if (d >= bestD) continue;
                bestD = d;
                best = n;
            }
            return best;
        }

        void AIShade(M m)
        {
            if (PlayerBuffs.Flag(m.S.Buff) && !ownerDead) m.TimeLeft = 2;
            m.TileCollide = false;
            m.LocalAi2++;
            int index = 0;
            foreach (var q in live)
            {
                if (q == m || q.Dead || q.Kind != m.Kind) continue;
                if (q.Serial < m.Serial) index++;
                if (m.Ai0 != 0f || Mathf.Abs(m.Pos.x - q.Pos.x) + Mathf.Abs(m.Pos.y - q.Pos.y) >= m.W) continue;
                m.Vel.x += m.Pos.x < q.Pos.x ? -0.08f : 0.08f;
                m.Vel.y += m.Pos.y < q.Pos.y ? -0.08f : 0.08f;
            }
            float toOwner = Vector2.Distance(m.Center, pCenter);
            if (toOwner > SoulTeleport || SoulRoom(m.Center) != SoulRoom(pCenter))
            {
                m.Pos = SoulIdle(index) - new Vector2(m.W / 2f, m.H / 2f);
                m.Vel = Vector2.zero;
                m.Ai0 = 0f;
                m.Ai1 = 0f;
                m.LocalAi1 = 0f;
                toOwner = Vector2.Distance(m.Center, pCenter);
            }
            if (toOwner > SoulLeash && m.LocalAi1 == 0f && m.Ai0 != 3f)
            {
                m.LocalAi1 = 1f;
                m.Ai0 = 0f;
                m.Ai1 = 0f;
                DevShadeRecalls++;
            }
            if (m.LocalAi1 == 1f && toOwner < SoulBack) m.LocalAi1 = 0f;
            var quarry = m.LocalAi1 == 0f ? ShadeQuarry(m) : null;
            // Its own enemy once the charge has begun, while that one lives.
            Npc mine = null;
            if (m.Ai0 >= 1f && m.TargetId != 0) npcById.TryGetValue(m.TargetId, out mine);
            m.Friendly = false;
            switch ((int)m.Ai0)
            {
                case 1:
                {
                    // Gathering: drawn back from it, shadow closing on it.
                    m.Ai1++;
                    var at = mine ?? quarry;
                    var away = at != null ? (m.Center - at.Center).normalized : new Vector2(-pDir, 0f);
                    m.Vel = m.Vel * 0.8f + away * 0.5f;
                    m.Frame = 2 + ((int)m.Ai1 / 6) % 2;
                    if (at != null && Mathf.Abs(at.Center.x - m.Center.x) > 2f) m.SpriteDir = at.Center.x < m.Center.x ? -1 : 1;
                    float ga = Random.value * Mathf.PI * 2f;
                    var from = m.Center + new Vector2(Mathf.Cos(ga), Mathf.Sin(ga)) * 20f;
                    ShadeMote(from, (m.Center - from).normalized * 2f, 0.9f);
                    if (m.Ai1 >= ShadeGather)
                    {
                        if (at == null) { m.Ai0 = 4f; m.Ai1 = 0f; break; }
                        m.TargetId = at.Id;
                        m.Ai0 = 2f;
                        m.Ai1 = 0f;
                        m.Vel = (at.Center - m.Center).normalized * Shade.ShadeDash;
                        DevShadeCharges++;
                        Sound("tk_shade_dash", 0.5f);
                    }
                    break;
                }
                case 2:
                {
                    // The charge: at it, turning after it; near enough, the wave.
                    m.Ai1++;
                    m.Frame = 4 + ((int)m.Ai1 / 5) % 2;
                    m.Friendly = true;
                    if (mine == null) { m.Ai0 = 4f; m.Ai1 = 0f; m.Friendly = false; break; }
                    var to = mine.Center - m.Center;
                    m.Vel = (m.Vel * 6f + to.normalized * Shade.ShadeDash) / 7f;
                    if (Mathf.Abs(m.Vel.x) > 0.2f) m.SpriteDir = m.Vel.x < 0f ? -1 : 1;
                    ShadeMote(m.Center + Random.insideUnitCircle * 4f, -m.Vel * 0.12f + Random.insideUnitCircle * 0.4f, Random.Range(0.8f, 1.1f));
                    if (to.magnitude <= Shade.StrikeRangePx)
                    {
                        // Into the wave: faster, straight at it.
                        m.Ai0 = 3f;
                        m.Ai1 = 0f;
                        m.Ai2 = 0f;
                        m.Vel = to.normalized * Shade.WaveSpeed;
                        DevShadeWaves++;
                        Sound("tk_shade_wave", 0.55f);
                        for (int k = 0; k < 12; k++)
                        {
                            float a = k / 12f * Mathf.PI * 2f;
                            ShadeMote(m.Center, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.4f, 1.2f, k % 4 == 0 ? 240 : 109);
                        }
                    }
                    else if (m.Ai1 >= ShadeDashMax) { m.Ai0 = 4f; m.Ai1 = 0f; }
                    break;
                }
                case 3:
                {
                    // The wave: homing on it till it strikes (that one only, Minions.Hit), then on past it, a little slower.
                    m.Ai1++;
                    m.Friendly = true;
                    if (m.Ai2 == 0f && mine != null)
                    {
                        var to = mine.Center - m.Center;
                        m.Vel = (m.Vel * 3f + to.normalized * Shade.WaveSpeed) / 4f;
                    }
                    else m.Vel *= 0.97f;
                    if (Random.value < 0.8f) ShadeMote(m.Center - m.Vel.normalized * 14f + Random.insideUnitCircle * 6f, -m.Vel * 0.1f, Random.Range(0.9f, 1.3f), Random.value < 0.2f ? 240 : 109);
                    if ((m.Ai2 == 1f && m.Ai1 >= ShadeWaveOn) || m.Ai1 >= ShadeWaveMax || (m.Ai2 == 0f && mine == null))
                    {
                        m.Ai0 = 4f;
                        m.Ai1 = 0f;
                        m.Friendly = false;
                        ShadeAppears(m);
                    }
                    break;
                }
                case 4:
                    // Back into the shade, flying off, slowing; then its rest.
                    m.Ai1++;
                    m.Vel *= 0.9f;
                    m.Frame = 6 + ((int)m.Ai1 / 8) % 2;
                    if (Mathf.Abs(m.Vel.x) > 0.3f) m.SpriteDir = m.Vel.x < 0f ? -1 : 1;
                    if (m.Ai1 >= ShadeRecover)
                    {
                        m.Ai0 = 0f;
                        m.Ai1 = Shade.ShadeRest;
                        m.TargetId = 0;
                    }
                    break;
                default:
                {
                    m.Frame = ((int)m.LocalAi2 / 10) % 2;
                    SoulFly(m, SoulIdle(index), m.LocalAi1 == 1f ? 16f : 11f);
                    m.Vel.y += Mathf.Sin(m.LocalAi2 * 0.07f + index) * 0.04f;
                    if (Mathf.Abs(m.Vel.x) > 0.6f) m.SpriteDir = m.Vel.x < 0f ? -1 : 1;
                    else m.SpriteDir = pDir < 0f ? -1 : 1;
                    if (m.Ai1 > 0f) m.Ai1--;
                    else if (quarry != null)
                    {
                        m.Ai0 = 1f;
                        m.Ai1 = 0f;
                        m.TargetId = quarry.Id;
                        Sound("tk_soul_gather", 0.35f);
                    }
                    break;
                }
            }
            m.Rot = 0f;
        }

        /// <summary>Whom its touch may hit now: on the charge, any but its own enemy (the wave's to strike); as the wave, its own once and any other.</summary>
        static bool ShadeMayHit(M m, int id)
        {
            if (m.Ai0 == 2f) return id != m.TargetId;
            if (m.Ai0 == 3f) return id != m.TargetId || m.Ai2 == 0f;
            return false;
        }

        /// <summary>The wave struck its own enemy (Minions.Damage): it strikes that one no more, and flies on.</summary>
        void ShadeStruck(M m)
        {
            if (m.Ai0 != 3f || m.Ai2 == 1f) return;
            m.Ai2 = 1f;
            m.Ai1 = 0f;
            DevShadeStrikes++;
            for (int k = 0; k < 10; k++) ShadeMote(m.Center, Random.insideUnitCircle * 3.2f, Random.Range(1f, 1.4f), k % 3 == 0 ? 240 : 109);
        }

        /// <summary>Dev: the first little shade's centre (world units) and its state, or null.</summary>
        public (Vector2 at, int state)? DevShade()
        {
            foreach (var m in live)
                if (!m.Dead && m.Kind == Kind.ShadeSoul) return (TerraParticles.ToWorld(m.Center), (int)m.Ai0);
            return null;
        }

        static readonly Vector2[] shadeTrail = new Vector2[7];

        /// <summary>
        /// The little shade: as the wave while it is one (TerraProjectiles.DrawShadeWave); else its frame (turned along the
        /// charge, kept upright going left; facing its way otherwise), bobbing while it waits, a lilac breath, its black
        /// afterimages while it charges.
        /// </summary>
        static void DrawShade(FxBatch fx, M m)
        {
            if (m.Ai0 == 3f)
            {
                for (int k = 0; k < shadeTrail.Length; k++) shadeTrail[k] = m.OldPosRing[k] + new Vector2(m.W / 2f, m.H / 2f);
                TerraProjectiles.DrawShadeWave(fx, m.Center, m.Vel, shadeTrail, Mathf.Clamp01(m.Ai1 / 2f + 0.4f));
                return;
            }
            var frame = FxBatch.Frame(Shade.Tex("minion"), 0, (m.Frame & 7) * 32, 32, 32, 16f, 16f);
            bool charge = m.Ai0 == 2f;
            bool left = m.SpriteDir == -1;
            float bob = m.Ai0 == 0f ? Mathf.Sin(m.LocalAi2 * 0.08f) * 2f : 0f;
            var c = m.Center + new Vector2(0f, bob);
            float rot = charge && m.Vel.sqrMagnitude > 0.01f ? Mathf.Atan2(m.Vel.y, m.Vel.x) : 0f;
            bool flipY = charge && m.Vel.x < 0f, flipX = !charge && left;
            if (charge)
                for (int k = 6; k >= 1; k--)
                {
                    var at = m.OldPosRing[k] + new Vector2(m.W / 2f, m.H / 2f);
                    if ((at - m.Center).sqrMagnitude > 140f * 140f) continue;
                    float f = 1f - k / 7f;
                    fx.Draw(frame, at, Shade.Dark(Shade.Ink, 0.4f * f), rot, 0.8f + 0.2f * f, flipY: flipY, flipX: flipX);
                }
            float halo = m.Ai0 == 1f ? 0.2f + 0.25f * Mathf.Clamp01(m.Ai1 / ShadeGather) : charge ? 0.3f : 0.14f;
            fx.Draw(frame, c, SoulWave.Glow(Shade.Lilac, halo), rot, 1.14f, flipY: flipY, flipX: flipX);
            fx.Draw(frame, c, Color.white, rot, 1f, flipY: flipY, flipX: flipX);
            fx.Light(c, new Color(0.2f, 0.15f, 0.3f), 2.2f);
        }
    }
}
