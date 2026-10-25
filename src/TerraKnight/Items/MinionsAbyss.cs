using TerraKnight.Effects;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The Dirge Mask's little dirge soul (Items/Abyss.cs), in the pack's eight frames (40×40: 0-1 idle, 2-3 drawing breath,
    /// 4-5 shrieking, 6-7 recovering; its mouth at 29, 20, facing +x). Ai0 its state, Ai1 the state's clock, Ai2 its tries
    /// to reach its place, LocalAi0 the way its laser goes (Terraria radians, locked once it draws breath), LocalAi1 1 while
    /// it flies back to its owner, LocalAi2 its own clock. It follows its owner as the choir soul does; with an enemy near
    /// them it flies to a place Abyss.StandoffPx off it (the owner's side, above), draws breath turned to it, and fires a
    /// laser from its mouth along that locked way for Abyss.LaserTicks (TerraProjectiles.DirgeLaser: one blow every
    /// Abyss.LaserEvery on the first enemy in its way, stopped there and at walls), then recovers and rests. Several take
    /// turns (their rests spaced at the summoning).
    /// </summary>
    internal sealed partial class Minions
    {
        const float DirgePrepare = 12f, DirgeRecover = 14f, DirgeRest = 10f;
        public static int DevDirgeShots, DevDirgeRecalls;

        static void DirgeAppears(M m)
        {
            for (int k = 0; k < 16; k++)
            {
                float a = k / 16f * Mathf.PI * 2f;
                int i = DustAt(m.Center - new Vector2(4f, 4f), 8, 8, k % 4 == 0 ? 240 : 109, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.6f, 0, 1.2f);
                if (i >= 0) TerraParticles.Instance[i].NoGravity = true;
            }
        }

        /// <summary>Its mouth (29, 20 in its frame: 9 px ahead of its middle along the laser).</summary>
        static Vector2 DirgeMouth(M m) => m.Center + new Vector2(Mathf.Cos(m.LocalAi0), Mathf.Sin(m.LocalAi0)) * 9f;

        void AIDirge(M m)
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
                DevDirgeRecalls++;
            }
            if (m.LocalAi1 == 1f && toOwner < SoulBack) m.LocalAi1 = 0f;
            Vector2 at = default;
            bool quarry = m.LocalAi1 == 0f && SoulQuarry(m, out at);
            var tp = TerraProjectiles.Instance;
            switch ((int)m.Ai0)
            {
                case 1:
                {
                    // Drawing breath: still, turned to it, its way then locked; shadow drawn into its mouth.
                    m.Ai1++;
                    m.Vel *= 0.85f;
                    if (quarry) m.LocalAi0 = AngleTo(m.Center, at);
                    m.Frame = 2 + ((int)m.Ai1 / 6) % 2;
                    float ga = Random.value * Mathf.PI * 2f;
                    var mouth = DirgeMouth(m);
                    var from = mouth + new Vector2(Mathf.Cos(ga), Mathf.Sin(ga)) * 16f;
                    int i = DustAt(from - Vector2.one * 2f, 4, 4, Random.value < 0.25f ? 240 : 109, (mouth - from).normalized * 2f, 0, 0.9f);
                    if (i >= 0) TerraParticles.Instance[i].NoGravity = true;
                    if (!quarry) { m.Ai0 = 3f; m.Ai1 = 0f; break; }
                    if (m.Ai1 >= DirgePrepare)
                    {
                        m.Ai0 = 2f;
                        m.Ai1 = 0f;
                        DevDirgeShots++;
                        Sound("tk_dirge_laser", 0.55f);
                    }
                    break;
                }
                case 2:
                {
                    // The laser: held in place, its way locked; a blow every LaserEvery.
                    m.Ai1++;
                    m.Vel *= 0.8f;
                    m.Frame = 4 + ((int)m.Ai1 / 5) % 2;
                    tp?.DirgeLaser(DirgeMouth(m), m.LocalAi0, m.Damage, m.Knockback, (int)m.Ai1 % Abyss.LaserEvery == 1);
                    if (m.Ai1 >= Abyss.LaserTicks) { m.Ai0 = 3f; m.Ai1 = 0f; }
                    break;
                }
                case 3:
                    m.Ai1++;
                    m.Vel *= 0.88f;
                    m.Frame = 6 + ((int)m.Ai1 / 7) % 2;
                    if (m.Ai1 >= DirgeRecover) { m.Ai0 = 0f; m.Ai1 = DirgeRest; }
                    break;
                default:
                {
                    m.Frame = ((int)m.LocalAi2 / 10) % 2;
                    if (m.Ai1 > 0f) m.Ai1--;
                    if (quarry && m.Ai1 <= 0f)
                    {
                        // A place to shoot from: StandoffPx off it on the owner's side, a little above, the next one higher.
                        float side = pCenter.x >= at.x ? 1f : -1f;
                        var goal = at + new Vector2(side * Abyss.StandoffPx, -40f - index * 26f);
                        SoulFly(m, goal, 12f);
                        m.SpriteDir = at.x < m.Center.x ? -1 : 1;
                        if (Vector2.Distance(m.Center, goal) < 36f || m.Ai2++ > 50f)
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

        /// <summary>Dev: the first dirge soul's centre (world units) and its state, or null.</summary>
        public (Vector2 at, int state)? DevDirge()
        {
            foreach (var m in live)
                if (!m.Dead && m.Kind == Kind.DirgeSoul) return (TerraParticles.ToWorld(m.Center), (int)m.Ai0);
            return null;
        }

        /// <summary>
        /// The dirge soul: its frame, turned along its laser while it draws breath and fires (mirrored when that is to the
        /// left), else upright facing its way and bobbing; a lilac breath, brighter as it fires; its light.
        /// </summary>
        static void DrawDirge(FxBatch fx, M m)
        {
            var frame = FxBatch.Frame(Abyss.Tex("dirge"), 0, (m.Frame & 7) * 40, 40, 40, 20f, 20f);
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
            float halo = m.Ai0 == 2f ? 0.38f : m.Ai0 == 1f ? 0.2f + 0.2f * Mathf.Clamp01(m.Ai1 / DirgePrepare) : 0.14f;
            fx.Draw(frame, c, SoulWave.Glow(Shade.Lilac, halo), rot, 1.12f, flipX: flip);
            fx.Draw(frame, c, Color.white, rot, 1f, flipX: flip);
            fx.Light(c, new Color(0.22f, 0.16f, 0.34f), 2.5f);
        }
    }
}
