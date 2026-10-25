using TerraKnight.Effects;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The Elegy Mask's little choir soul (Items/Howl.cs), in the pack's eight frames (40×40: 0-1 idle, 2-3 drawing breath,
    /// 4-5 shouting, 6-7 recovering; its mouth at 29, 20, facing +x). Ai0 its state, Ai1 the state's clock, LocalAi0 the
    /// way its shout goes (Terraria radians), LocalAi1 1 while it flies back to its owner, LocalAi2 its own clock. It
    /// follows its owner as the wandering soul does (behind the shoulder, back when they go far, taken along to another
    /// room); with an enemy near them it flies to a place to shout from (Howl.ChoirStandoff off it, on the owner's side,
    /// above), draws breath turned to it, then keeps up a 30° cone of sound on it (TerraProjectiles.ChoirPulse: every
    /// enemy in it it can see, through them all, Howl.ShoutReachPx long), turning slowly after it; then rests. Several
    /// take turns (their rests spaced at the summoning).
    /// </summary>
    internal sealed partial class Minions
    {
        const float ChoirPrepare = 12f, ChoirShout = 60f, ChoirRecover = 14f, ChoirStandoff = 150f;
        public static int DevChoirShouts, DevChoirRecalls;

        static void ChoirAppears(M m)
        {
            for (int k = 0; k < 16; k++)
            {
                float a = k / 16f * Mathf.PI * 2f;
                int i = DustAt(m.Center - new Vector2(4f, 4f), 8, 8, 180, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.6f, 0, 1.2f);
                if (i >= 0) TerraParticles.Instance[i].NoGravity = true;
            }
        }

        /// <summary>The way from it to a point, as a Terraria rotation.</summary>
        static float AngleTo(Vector2 from, Vector2 to) => Mathf.Atan2(to.y - from.y, to.x - from.x);

        /// <summary>Its mouth (29, 20 in its frame: 9 px ahead of its middle along the shout).</summary>
        static Vector2 ChoirMouth(M m) => m.Center + new Vector2(Mathf.Cos(m.LocalAi0), Mathf.Sin(m.LocalAi0)) * 9f;

        void AIChoir(M m)
        {
            if (PlayerBuffs.Flag(m.S.Buff) && !ownerDead) m.TimeLeft = 2;
            m.TileCollide = false;
            m.LocalAi2++;
            int index = 0;
            foreach (var q in live)
            {
                if (q == m || q.Dead || q.Kind != m.Kind) continue;
                if (q.Serial < m.Serial) index++;
                if (Mathf.Abs(m.Pos.x - q.Pos.x) + Mathf.Abs(m.Pos.y - q.Pos.y) >= m.W) continue;
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
            if (toOwner > SoulLeash && m.LocalAi1 == 0f)
            {
                m.LocalAi1 = 1f;
                m.Ai0 = 0f;
                m.Ai1 = 0f;
                DevChoirRecalls++;
            }
            if (m.LocalAi1 == 1f && toOwner < SoulBack) m.LocalAi1 = 0f;
            Vector2 at = default;
            bool quarry = m.LocalAi1 == 0f && SoulQuarry(m, out at);
            var tp = TerraProjectiles.Instance;
            switch ((int)m.Ai0)
            {
                case 1:
                {
                    // Drawing breath: still, turned to it, souls drawn in.
                    m.Ai1++;
                    m.Vel *= 0.85f;
                    if (quarry) m.LocalAi0 = AngleTo(m.Center, at);
                    m.Frame = 2 + ((int)m.Ai1 / 6) % 2;
                    float ga = Random.value * Mathf.PI * 2f;
                    var from = m.Center + new Vector2(Mathf.Cos(ga), Mathf.Sin(ga)) * 20f;
                    int i = DustAt(from - Vector2.one * 2f, 4, 4, 180, (m.Center - from).normalized * 2f, 0, 0.8f);
                    if (i >= 0) TerraParticles.Instance[i].NoGravity = true;
                    if (!quarry) { m.Ai0 = 3f; m.Ai1 = 0f; break; }
                    if (m.Ai1 >= ChoirPrepare)
                    {
                        m.Ai0 = 2f;
                        m.Ai1 = 0f;
                        DevChoirShouts++;
                        Sound("tk_choir_shout", 0.5f);
                    }
                    break;
                }
                case 2:
                {
                    // The shout: held in place, turning slowly after it; a pulse that strikes every ShoutCooldown, a ring every 5.
                    m.Ai1++;
                    m.Vel *= 0.85f;
                    m.Vel.y += Mathf.Sin(m.LocalAi2 * 0.2f) * 0.03f;
                    if (quarry)
                    {
                        float want = AngleTo(m.Center, at);
                        m.LocalAi0 += Mathf.Clamp(Mathf.DeltaAngle(m.LocalAi0 * Mathf.Rad2Deg, want * Mathf.Rad2Deg), -2.5f, 2.5f) * Mathf.Deg2Rad;
                    }
                    m.Frame = 4 + ((int)m.Ai1 / 6) % 2;
                    if (tp != null)
                    {
                        if ((int)m.Ai1 % Howl.ShoutCooldown == 1) tp.ChoirPulse(ChoirMouth(m), m.LocalAi0, m.Damage, m.Knockback, true);
                        else if ((int)m.Ai1 % 5 == 0) tp.ChoirPulse(ChoirMouth(m), m.LocalAi0, m.Damage, m.Knockback, false);
                    }
                    if (m.Ai1 >= ChoirShout || !quarry) { m.Ai0 = 3f; m.Ai1 = 0f; }
                    break;
                }
                case 3:
                    m.Ai1++;
                    m.Vel *= 0.88f;
                    m.Frame = 6 + ((int)m.Ai1 / 7) % 2;
                    if (m.Ai1 >= ChoirRecover) { m.Ai0 = 0f; m.Ai1 = 8f; }
                    break;
                default:
                {
                    m.Frame = ((int)m.LocalAi2 / 10) % 2;
                    if (m.Ai1 > 0f) m.Ai1--;
                    if (quarry && m.Ai1 <= 0f)
                    {
                        // A place to shout from: off it on the owner's side, above, the next one higher.
                        float side = pCenter.x >= at.x ? 1f : -1f;
                        var goal = at + new Vector2(side * ChoirStandoff, -50f - index * 30f);
                        SoulFly(m, goal, 12f);
                        m.SpriteDir = at.x < m.Center.x ? -1 : 1;
                        m.LocalAi1 = 0f;
                        if (Vector2.Distance(m.Center, goal) < 40f || m.Ai2++ > 50f)
                        {
                            m.Ai2 = 0f;
                            m.Ai0 = 1f;
                            m.Ai1 = 0f;
                            m.LocalAi0 = AngleTo(m.Center, at);
                        }
                    }
                    else
                    {
                        m.Ai2 = 0f;
                        SoulFly(m, SoulIdle(index), m.LocalAi1 == 1f ? 16f : 11f);
                        m.Vel.y += Mathf.Sin(m.LocalAi2 * 0.07f + index) * 0.04f;
                        if (Mathf.Abs(m.Vel.x) > 0.6f) m.SpriteDir = m.Vel.x < 0f ? -1 : 1;
                        else m.SpriteDir = pDir < 0f ? -1 : 1;
                    }
                    break;
                }
            }
            m.Friendly = false;
            m.Rot = 0f;
        }

        /// <summary>Dev: the first choir soul's centre (world units) and its state, or null.</summary>
        public (Vector2 at, int state)? DevChoir()
        {
            foreach (var m in live)
                if (!m.Dead && m.Kind == Kind.ChoirSoul) return (TerraParticles.ToWorld(m.Center), (int)m.Ai0);
            return null;
        }

        /// <summary>
        /// The choir soul: its frame, turned along its shout while it draws breath and shouts (mirrored when that is to the
        /// left), else upright facing its way and bobbing; a pale halo, brighter as it shouts; its light.
        /// </summary>
        static void DrawChoir(FxBatch fx, M m)
        {
            var frame = FxBatch.Frame(Howl.Tex("choir"), 0, (m.Frame & 7) * 40, 40, 40, 20f, 20f);
            bool aiming = m.Ai0 == 1f || m.Ai0 == 2f;
            float rot = 0f;
            bool flip;
            if (aiming)
            {
                float a = m.LocalAi0;
                flip = Mathf.Cos(a) < 0f;
                rot = flip ? a - Mathf.PI : a;
            }
            else flip = m.SpriteDir == -1;
            float bob = m.Ai0 == 0f ? Mathf.Sin(m.LocalAi2 * 0.08f) * 2f : 0f;
            var c = m.Center + new Vector2(0f, bob);
            float halo = m.Ai0 == 2f ? 0.38f : m.Ai0 == 1f ? 0.25f + 0.2f * Mathf.Clamp01(m.Ai1 / ChoirPrepare) : 0.16f;
            fx.Draw(frame, c, SoulWave.Glow(SoulWave.Pale, halo), rot, 1.12f, flipX: flip);
            fx.Draw(frame, c, Color.white, rot, 1f, flipX: flip);
            fx.Light(c, new Color(0.32f, 0.42f, 0.6f), 2.5f);
        }
    }
}
