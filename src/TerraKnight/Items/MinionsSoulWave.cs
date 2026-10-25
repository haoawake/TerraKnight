using TerraKnight.Effects;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The Wandering Soul Staff's little soul (Items/SoulWave.cs), in the pack's eight frames (32×32: 0-1 idle, 2-3 gathering,
    /// 4-5 pouncing, 6-7 recovering). Ai0 its state, Ai1 the state's clock, LocalAi1 1 while it flies back to its owner,
    /// LocalAi2 its own clock for the bob. With nothing to hunt it floats behind its owner's shoulder, bobbing. It hunts the
    /// enemies near its owner, through walls; finding one it draws back and gathers itself (souls drawn in), then pounces
    /// at it (its afterimages, a stream of souls, its touch hurting only now), pulls up when it strikes or its run ends,
    /// and goes for the next. When the owner goes far (SoulLeash) it breaks off whatever it was doing and flies back to
    /// them, hunting nothing until it is by them again; further still, or in another room, it is taken to them at once.
    /// </summary>
    internal sealed partial class Minions
    {
        /// <summary>Its states' lengths (ticks); the enemies it hunts, within SoulRange of its owner (px); SoulLeash, how far from them it breaks off; SoulBack, how near they bring it home; SoulTeleport, how far they take it along.</summary>
        const float SoulGather = 14f, SoulPounce = 20f, SoulRecover = 16f, SoulRange = 900f, SoulLeash = 1100f, SoulBack = 200f, SoulTeleport = 2000f;
        /// <summary>How hard the soul springs back off what it strikes (px a tick; about 100 px through its recovery).</summary>
        const float SoulRebound = 12f;
        /// <summary>The pounce's speed (px a tick): from its long way off, a full charge.</summary>
        const float SoulDash = 18f;
        public static int DevSoulPounces, DevSoulStrikes, DevSoulCarried, DevSoulRecalls;

        /// <summary>The soul called: a ring of souls where it comes into being.</summary>
        static void SoulAppears(M m)
        {
            for (int k = 0; k < 16; k++)
            {
                float a = k / 16f * Mathf.PI * 2f;
                int i = DustAt(m.Center - new Vector2(4f, 4f), 8, 8, 180, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.4f, 0, 1.2f);
                if (i >= 0) TerraParticles.Instance[i].NoGravity = true;
            }
        }

        /// <summary>Where the soul's quarry is (the owner's chosen one, else the enemy nearest the soul), within SoulRange of its owner; or false.</summary>
        bool SoulQuarry(M m, out Vector2 at)
        {
            at = default;
            var t = OwnerTarget;
            if (t != null && Vector2.Distance(pCenter, t.Center) < SoulRange * 1.4f)
            {
                at = t.Center;
                return true;
            }
            float best = float.MaxValue;
            bool found = false;
            foreach (var n in npcs)
            {
                if (Vector2.Distance(pCenter, n.Center) >= SoulRange) continue;
                float d = Vector2.Distance(m.Center, n.Center);
                if (d >= best) continue;
                best = d;
                at = n.Center;
                found = true;
            }
            return found;
        }

        /// <summary>The soul's place by its owner: behind their shoulder, the next one a little further back.</summary>
        Vector2 SoulIdle(int index) => pCenter + new Vector2(-pDir * (34f + index * 30f), -46f);

        /// <summary>Towards a point (px), quick from far and easing in near, as Terraria's flying minions go home.</summary>
        static void SoulFly(M m, Vector2 goal, float fast)
        {
            var d = goal - m.Center;
            float dist = d.magnitude;
            if (dist < 6f)
            {
                m.Vel *= 0.85f;
                return;
            }
            float speed = dist > 300f ? fast : dist > 80f ? 6f : Mathf.Max(1.2f, dist / 16f);
            float inertia = dist > 300f ? 16f : 24f;
            m.Vel = (m.Vel * (inertia - 1f) + d / dist * speed) / inertia;
        }

        void AISoul(M m)
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
            // Taken to the owner when they are far, or in another room.
            float toOwner = Vector2.Distance(m.Center, pCenter);
            if (toOwner > SoulTeleport || SoulRoom(m.Center) != SoulRoom(pCenter))
            {
                DevSoulCarried++;
                m.Pos = SoulIdle(index) - new Vector2(m.W / 2f, m.H / 2f);
                m.Vel = Vector2.zero;
                m.Ai0 = 0f;
                m.Ai1 = 0f;
                m.LocalAi1 = 0f;
                toOwner = Vector2.Distance(m.Center, pCenter);
            }
            // Left far behind: whatever it was doing, back to the owner, hunting nothing on the way.
            if (toOwner > SoulLeash && m.LocalAi1 == 0f)
            {
                m.LocalAi1 = 1f;
                m.Ai0 = 0f;
                m.Ai1 = 0f;
                m.Friendly = false;
                DevSoulRecalls++;
            }
            if (m.LocalAi1 == 1f && toOwner < SoulBack) m.LocalAi1 = 0f;
            Vector2 at = default;
            bool quarry = m.LocalAi1 == 0f && SoulQuarry(m, out at);
            var to = quarry ? at - m.Center : Vector2.zero;
            switch ((int)m.Ai0)
            {
                case 1:
                {
                    // Gathering: drawn back from it, souls closing on it.
                    m.Ai1++;
                    m.Friendly = false;
                    var away = quarry ? -to.normalized : new Vector2(-pDir, 0f);
                    m.Vel = m.Vel * 0.8f + away * 0.55f;
                    m.Frame = 2 + ((int)m.Ai1 / 7) % 2;
                    if (quarry && Mathf.Abs(to.x) > 2f) m.SpriteDir = to.x < 0f ? -1 : 1;
                    float ga = Random.value * Mathf.PI * 2f;
                    var from = m.Center + new Vector2(Mathf.Cos(ga), Mathf.Sin(ga)) * 22f;
                    int i = DustAt(from - Vector2.one * 2f, 4, 4, 180, (m.Center - from).normalized * 2f, 0, 0.8f);
                    if (i >= 0) TerraParticles.Instance[i].NoGravity = true;
                    if (m.Ai1 >= SoulGather)
                    {
                        if (!quarry) { m.Ai0 = 3f; m.Ai1 = 0f; break; }
                        m.Ai0 = 2f;
                        m.Ai1 = 0f;
                        m.Vel = to.normalized * SoulDash;
                        m.Friendly = true;
                        DevSoulPounces++;
                        Sound("tk_soul_dash_*", 0.5f);
                    }
                    break;
                }
                case 2:
                {
                    // The pounce: straight at it, turning a little after it; its touch hurts.
                    m.Ai1++;
                    m.Friendly = true;
                    if (quarry && to.sqrMagnitude > 1f) m.Vel = (m.Vel * 9f + to.normalized * SoulDash) / 10f;
                    m.Frame = 4 + ((int)m.Ai1 / 5) % 2;
                    if (Mathf.Abs(m.Vel.x) > 0.2f) m.SpriteDir = m.Vel.x < 0f ? -1 : 1;
                    for (int k = 0; k < 2; k++)
                    {
                        int i = DustAt(m.Center - Vector2.one * 4f, 8, 8, 180, -m.Vel * 0.12f + Random.insideUnitCircle * 0.5f, 0, Random.Range(0.7f, 1.1f));
                        if (i >= 0) TerraParticles.Instance[i].NoGravity = true;
                    }
                    if (m.Ai1 >= SoulPounce) { m.Ai0 = 3f; m.Ai1 = 0f; m.Friendly = false; }
                    break;
                }
                case 3:
                    // Recovering: slowing (gently after a strike's rebound, so it carries far), harmless, then after the next.
                    m.Ai1++;
                    m.Friendly = false;
                    m.Vel *= 0.9f;
                    m.Frame = 6 + ((int)m.Ai1 / 8) % 2;
                    if (m.Ai1 >= SoulRecover) { m.Ai0 = 0f; m.Ai1 = 4f; }
                    break;
                default:
                {
                    // Idle: behind the owner's shoulder, bobbing (flying back faster when it was left behind); a short rest after a pounce (Ai1).
                    m.Friendly = false;
                    SoulFly(m, SoulIdle(index), m.LocalAi1 == 1f ? 16f : 11f);
                    m.Vel.y += Mathf.Sin(m.LocalAi2 * 0.07f + index) * 0.04f;
                    m.Frame = ((int)m.LocalAi2 / 10) % 2;
                    if (Mathf.Abs(m.Vel.x) > 0.6f) m.SpriteDir = m.Vel.x < 0f ? -1 : 1;
                    else m.SpriteDir = pDir < 0f ? -1 : 1;
                    if (m.Ai1 > 0f) m.Ai1--;
                    else if (quarry)
                    {
                        m.Ai0 = 1f;
                        m.Ai1 = 0f;
                        Sound("tk_soul_gather", 0.4f);
                    }
                    break;
                }
            }
            m.Rot = 0f;
        }

        /// <summary>The room a point (px) lies in (TerraWorld's region), or null.</summary>
        /// <summary>Dev: the first little soul's centre (world units), or null.</summary>
        public Vector2? DevSoulAt()
        {
            foreach (var m in live)
                if (!m.Dead && m.Kind == Kind.WanderingSoul) return TerraParticles.ToWorld(m.Center);
            return null;
        }

        static string SoulRoom(Vector2 px)
        {
            var c = World.CellGrid.WorldToCell(TerraParticles.ToWorld(px));
            return World.TerraWorld.RegionAt(c.x, c.y)?.SceneName;
        }

        /// <summary>
        /// The pounce struck: the soul springs back off it, hard (the user's, 2026-10-20: each pounce a full charge), and
        /// drifts out to a long way off through its recovery, souls bursting off it; its timing as before.
        /// </summary>
        void SoulStruck(M m)
        {
            if (m.Ai0 != 2f) return;
            m.Ai0 = 3f;
            m.Ai1 = 0f;
            m.Friendly = false;
            var back = m.Vel.sqrMagnitude > 1e-4f ? -m.Vel.normalized : new Vector2(-pDir, 0f);
            m.Vel = (back + new Vector2(0f, -0.35f)).normalized * SoulRebound;
            DevSoulStrikes++;
            for (int k = 0; k < 8; k++)
            {
                int i = DustAt(m.Center - Vector2.one * 4f, 8, 8, 180, Random.insideUnitCircle * 3f, 0, 1.1f);
                if (i >= 0) TerraParticles.Instance[i].NoGravity = true;
            }
        }

        /// <summary>
        /// The soul: its frame (turned along the pounce, kept upright going left; else facing its way), bobbing while it
        /// waits, a cyan halo (brighter gathering and pouncing), its afterimages while it pounces, its light.
        /// </summary>
        static void DrawSoul(FxBatch fx, M m)
        {
            var frame = FxBatch.Frame(SoulWave.Tex("minion"), 0, (m.Frame & 7) * 32, 32, 32, 16f, 16f);
            bool pounce = m.Ai0 == 2f;
            bool left = m.SpriteDir == -1;
            float bob = m.Ai0 == 0f ? Mathf.Sin(m.LocalAi2 * 0.08f) * 2f : 0f;
            var c = m.Center + new Vector2(0f, bob);
            float rot = pounce && m.Vel.sqrMagnitude > 0.01f ? Mathf.Atan2(m.Vel.y, m.Vel.x) : 0f;
            bool flipY = pounce && m.Vel.x < 0f, flipX = !pounce && left;
            if (pounce)
                for (int k = 6; k >= 1; k--)
                {
                    var at = m.OldPosRing[k] + new Vector2(m.W / 2f, m.H / 2f);
                    if ((at - m.Center).sqrMagnitude > 140f * 140f) continue;
                    float f = 1f - k / 7f;
                    fx.Draw(frame, at, SoulWave.Glow(SoulWave.Pale, 0.32f * f), rot, 0.8f + 0.2f * f, flipY: flipY, flipX: flipX);
                }
            float halo = m.Ai0 == 1f ? 0.25f + 0.3f * Mathf.Clamp01(m.Ai1 / SoulGather) : pounce ? 0.4f : 0.18f;
            fx.Draw(frame, c, SoulWave.Glow(SoulWave.Cyan, halo), rot, 1.14f, flipY: flipY, flipX: flipX);
            fx.Draw(frame, c, Color.white, rot, 1f, flipY: flipY, flipX: flipX);
            fx.Light(c, new Color(0.3f, 0.42f, 0.62f), 2.5f);
        }
    }
}
