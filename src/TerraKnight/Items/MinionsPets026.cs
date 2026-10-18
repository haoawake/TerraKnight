using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// AI_026 for the pets (P:59086-66045), each pet by its own branches: the gap it leaves the player (P:59102-59160), which
    /// way it walks (P:59932-60000), the flying pets' own flights (P:60105-61255), the leash (P:61260-61345), the Truffle's,
    /// Bernie's and the Swarm Biter's fade to the player's feet (P:61346-61389), the flight back with each one's frames,
    /// turns and trails (P:61390-62590), and on the ground the walk, the hops (P:63176-63292: the slime royals and the
    /// Shadow Mimic hop all the while they move, the lizard climbs walls, the turtle takes off at one) and each one's
    /// frames and fall (P:63336-66045). Terraria px, y down; the player is the hero's box.
    /// </summary>
    internal sealed partial class Minions
    {
        /// <summary>No liquid on the grid: a pet is never wet (Projectile.wet).</summary>
        const bool PetWet = false;

        /// <summary>Projectile.alpha (0-255) kept in M.Alpha as a fraction.</summary>
        static int AlphaOf(M m) => Mathf.RoundToInt(m.Alpha * 255f);
        static void SetAlpha(M m, int a) => m.Alpha = Mathf.Clamp(a, 0, 255) / 255f;

        /// <summary>Terraria's -Vector2.UnitY (up, in y-down px).</summary>
        static readonly Vector2 UpT = new Vector2(0f, -1f);

        /// <summary>Vector2.SafeNormalize(fallback).ToRotation().</summary>
        static float Heading(Vector2 v, Vector2 fallback)
        {
            var n = v.sqrMagnitude > 0f ? v.normalized : fallback;
            return Mathf.Atan2(n.y, n.x);
        }

        /// <summary>WorldGen.SolidTileAllowBottomSlope / halfBrick / slope under a pet: a block, a platform, or the scene's own ground.</summary>
        static bool Floor026(int i, int j) => SolidTile(i, j) || PlatformTile(i, j) || SolidBox(i * 16f, j * 16f, 16f, 16f);

        /// <summary>The 12 dusts a flying pet leaves where it was when it jumps to the player past 2000 px.</summary>
        static void TeleportPuff(M m, int dustType)
        {
            var tp = TerraParticles.Instance;
            for (int k = 0; k < 12; k++)
            {
                float sx = 1f - Random.value * 2f, sy = 1f - Random.value * 2f;
                int d = DustAt(m.Pos, m.W, m.H, dustType, new Vector2(sx, sy), 0, 1f);
                if (d >= 0) tp[d].NoGravity = true;
            }
        }

        /// <summary>Gore.NewGore: a plain gore that falls (the pets' crowns and hats).</summary>
        static void PetGore(Vector2 at, Vector2 vel, int type) => TerraParticles.Instance?.NewGore(at, vel, type, 1f, 1f, default, false, 0, true);

        void Pet026(M m, PetProj p)
        {
            int type = p.Id;
            ref var velocity = ref m.Vel;
            bool flag2 = false, flag3 = false, flag5 = false;
            int num = 85;
            float myX = m.Pos.x + m.W / 2;
            // P:59102-59160: the gap each one leaves.
            if (type == 324) num = 120;
            if (type == 112) num = 100;
            if (type == 127) num = 50;
            switch (type)
            {
                case 816: case 821: case 825: case 854: case 858: case 859: case 860: case 885: case 889: case 891: case 897: case 899: case 900: case 934:
                    num = 95;
                    break;
                case 884: case 890:
                    num = 80;
                    break;
                // P:59156: 95, or 120 while the player owns an 881 (ownedProjectileCounts counts this one too).
                case 881:
                    num = 120;
                    break;
            }
            // P:59945-59999: Swarm Biter 28 px behind the player, Toy Golem 50 px ahead, Chester 70 px off on the player's
            // far side (the way he walks while he moves); the rest straight at him.
            if (type == 1027)
            {
                num = 10;
                float goal = pCenter.x + 28 * -pDir;
                if (goal < m.Center.x - num) flag2 = true;
                else if (goal > m.Center.x + num) flag3 = true;
            }
            else if (type == 891)
            {
                num = 30;
                float goal = pCenter.x + -50 * -pDir;
                if (goal < myX - num) flag2 = true;
                else if (goal > myX + num) flag3 = true;
            }
            else if (type == 960)
            {
                num = 10;
                int side = pCenter.x - m.Center.x > 0f ? 1 : -1;
                if (pVel.x != 0f) side = pDir;
                float goal = pCenter.x + -70 * side;
                if (goal < m.Center.x - num) flag2 = true;
                else if (goal > m.Center.x + num) flag3 = true;
            }
            else if (pCenter.x < myX - num) flag2 = true;
            else if (pCenter.x > myX + num) flag3 = true;

            // The flying pets: their own flights, never walking (P:60105-61255).
            switch (type)
            {
                case 175: PetEater(m); return;
                case 197: PetSkull(m); return;
                case 198: case 380: case 774: case 815: case 817: case 886: case 892: case 894: case 901: case 1046: PetHover(m, p); return;
                case 211: PetWisp(m); return;
                case 199: PetTiki(m); return;
                case 885: case 889: PetSkeletron(m, type); return;
            }

            // P:61260-61345: the leash. Past it, or 300 px above or below, it takes to the air (ai[0] = 1).
            {
                int num92 = 500;
                if (type == 127) num92 = 200;
                if (type == 208) num92 = 300;
                switch (type)
                {
                    case 816: case 825: case 854: case 858: case 859: case 860: case 881: case 884: case 890: case 891: case 897: case 900: case 934:
                        num92 = 400;
                        break;
                    case 821: case 899:
                        num92 = 500;
                        break;
                }
                float dx = pCenter.x - m.Center.x, dy = pCenter.y - m.Center.y;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist > 2000f) m.Pos = new Vector2(pCenter.x - m.W / 2, pCenter.y - m.H / 2);
                else if (dist > num92 || Mathf.Abs(dy) > 300f)
                {
                    if (type != 324)
                    {
                        if (dy > 0f && velocity.y < 0f) velocity.y = 0f;
                        if (dy < 0f && velocity.y > 0f) velocity.y = 0f;
                    }
                    m.Ai0 = 1f;
                }
            }

            if ((type == 209 || type == 956 || type == 1027) && m.Ai0 != 0f)
            {
                // P:61346-61389: the Truffle, Bernie and the Swarm Biter don't fly: they fade out where they are and, once
                // the player stands, come back at his feet.
                int alpha = AlphaOf(m);
                if (ownerGrounded && alpha >= 100)
                {
                    m.Pos = new Vector2(pCenter.x - m.W / 2, pBox.yMax - m.H);
                    m.Ai0 = 0f;
                }
                else
                {
                    velocity = Vector2.zero;
                    var tp = TerraParticles.Instance;
                    if (type == 1027 && alpha < 255)
                    {
                        alpha = 255;
                        SwarmBurst(m);
                    }
                    if (type == 956 && alpha < 100 && tp != null)
                    {
                        int d = DustAt(m.Pos, m.W, m.H, 6, velocity, 0, 1.2f);
                        if (d >= 0)
                        {
                            tp[d].Velocity.x += Random.value - 0.5f;
                            tp[d].Velocity.y += (Random.value + 0.5f) * -1f;
                            if (Random.Range(0, 3) != 0) tp[d].NoGravity = true;
                        }
                    }
                    alpha += 5;
                    SetAlpha(m, alpha);
                }
            }
            else if (m.Ai0 != 0f) FlyBack026(m, p);
            else Walk026(m, p, flag2, flag3, flag5);
            // (P:66031: the Toy Golem's light is its PetProj.Light, cast each tick by StepPet.)
        }

        /// <summary>The Swarm Biter's ring of 25 dusts (282) as it vanishes or appears (P:61361-61372, 63579-63592).</summary>
        static void SwarmBurst(M m)
        {
            var tp = TerraParticles.Instance;
            if (tp == null) return;
            const int n = 25;
            float start = Random.value * (Mathf.PI * 2f);
            for (int k = 0; k < n; k++)
            {
                bool even = k % 2 == 0;
                float a = start + k / (float)n * (Mathf.PI * 2f);
                var v = Rotated(new Vector2(0f, even ? 2f : 4f), a);
                int d = tp.NewDust(m.Center, 0, 0, 282);
                if (d < 0) continue;
                tp[d].Position = m.Center;
                tp[d].Velocity = v;
                tp[d].Scale = even ? 1.3f : 1.2f;
                tp[d].NoGravity = true;
            }
        }

        /// <summary>Vector2.RotatedBy (Terraria's y-down turn).</summary>
        static Vector2 Rotated(Vector2 v, float a)
        {
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        // ------------------------------------------------------------------ the flying pets

        /// <summary>
        /// The Eater of Souls baby (175, P:60105-60231): 7 px a tick at the player, gently (0.01, 2 px) within 150 px, harder
        /// (0.2) past 300; steering only the axis it is further off on; head first, two frames of 7 ticks.
        /// </summary>
        void PetEater(M m)
        {
            ref var velocity = ref m.Vel;
            float accel = 0.1f;
            m.TileCollide = false;
            float dx = pCenter.x - m.Center.x, dy = pCenter.y - m.Center.y;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            bool far = dist > 2000f;
            if (dist < 300f && ownerGrounded && m.Bottom <= pBox.yMax && !SolidBox(m.Box))
            {
                m.Ai0 = 0f;
                if (velocity.y < -6f) velocity.y = -6f;
            }
            if (dist < 150f)
            {
                if (Mathf.Abs(velocity.x) > 2f || Mathf.Abs(velocity.y) > 2f) velocity *= 0.99f;
                accel = 0.01f;
                dx = Mathf.Clamp(dx, -2f, 2f);
                dy = Mathf.Clamp(dy, -2f, 2f);
            }
            else
            {
                if (dist > 300f) accel = 0.2f;
                float k = 7f / dist;
                dx *= k;
                dy *= k;
            }
            if (far)
            {
                TeleportPuff(m, 17);
                m.Center = pCenter;
                velocity = Vector2.zero;
            }
            if (Mathf.Abs(dx) > Mathf.Abs(dy) || accel == 0.05f)
            {
                if (velocity.x < dx)
                {
                    velocity.x += accel;
                    if (accel > 0.05f && velocity.x < 0f) velocity.x += accel;
                }
                if (velocity.x > dx)
                {
                    velocity.x -= accel;
                    if (accel > 0.05f && velocity.x > 0f) velocity.x -= accel;
                }
            }
            if (Mathf.Abs(dx) <= Mathf.Abs(dy) || accel == 0.05f)
            {
                if (velocity.y < dy)
                {
                    velocity.y += accel;
                    if (accel > 0.05f && velocity.y < 0f) velocity.y += accel;
                }
                if (velocity.y > dy)
                {
                    velocity.y -= accel;
                    if (accel > 0.05f && velocity.y > 0f) velocity.y -= accel;
                }
            }
            m.Rot = Mathf.Atan2(velocity.y, velocity.x) - 1.57f;
            if (++m.FrameCounter > 6)
            {
                m.Frame++;
                m.FrameCounter = 0;
            }
            if (m.Frame > 1) m.Frame = 0;
        }

        /// <summary>
        /// The Skeletron head (197, P:60232-60398): 3 px a tick at the player (14 once it has been 500 px off), leaning with
        /// its speed; every so often (localAI[0] up by 0-9 a tick past 10000) it spins round for a while.
        /// </summary>
        void PetSkull(M m)
        {
            ref var velocity = ref m.Vel;
            float accel = 0.1f;
            m.TileCollide = false;
            float dx = pCenter.x - m.Center.x, dy = pCenter.y - m.Center.y;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float speed = 3f;
            if (dist > 500f) m.LocalAi0 = 10000f;
            if (m.LocalAi0 >= 10000f) speed = 14f;
            bool far = dist > 2000f;
            if (dist < 300f && ownerGrounded && m.Bottom <= pBox.yMax && !SolidBox(m.Box))
            {
                m.Ai0 = 0f;
                if (velocity.y < -6f) velocity.y = -6f;
            }
            if (dist < 150f)
            {
                if (Mathf.Abs(velocity.x) > 2f || Mathf.Abs(velocity.y) > 2f) velocity *= 0.99f;
                accel = 0.01f;
                dx = Mathf.Clamp(dx, -2f, 2f);
                dy = Mathf.Clamp(dy, -2f, 2f);
            }
            else
            {
                if (dist > 300f) accel = 0.2f;
                float k = speed / dist;
                dx *= k;
                dy *= k;
            }
            if (far)
            {
                TeleportPuff(m, 26);
                m.Center = pCenter;
                velocity = Vector2.zero;
            }
            if (velocity.x < dx)
            {
                velocity.x += accel;
                if (accel > 0.05f && velocity.x < 0f) velocity.x += accel;
            }
            if (velocity.x > dx)
            {
                velocity.x -= accel;
                if (accel > 0.05f && velocity.x > 0f) velocity.x -= accel;
            }
            if (velocity.y < dy)
            {
                velocity.y += accel;
                if (accel > 0.05f && velocity.y < 0f) velocity.y += accel;
            }
            if (velocity.y > dy)
            {
                velocity.y -= accel;
                if (accel > 0.05f && velocity.y > 0f) velocity.y -= accel;
            }
            m.LocalAi0 += Random.Range(0, 10);
            if (m.LocalAi0 > 10000f)
            {
                if (m.LocalAi1 == 0f) m.LocalAi1 = velocity.x < 0f ? -1f : 1f;
                m.Rot += 0.25f * m.LocalAi1;
                if (m.LocalAi0 > 12000f) m.LocalAi0 = 0f;
            }
            else
            {
                m.LocalAi1 = 0f;
                float lean = velocity.x * 0.1f;
                float step = (Mathf.Abs(velocity.x) + Mathf.Abs(velocity.y)) * 0.01f;
                if (m.Rot > lean)
                {
                    m.Rot -= step;
                    if (m.Rot < lean) m.Rot = lean;
                }
                if (m.Rot < lean)
                {
                    m.Rot += step;
                    if (m.Rot > lean) m.Rot = lean;
                }
            }
            if (m.Rot > 6.28f) m.Rot -= 6.28f;
            if (m.Rot < -6.28f) m.Rot += 6.28f;
        }

        /// <summary>
        /// The hovering pets (P:60399-60774): the Baby Hornet and the Queen Bee's, the Zephyr Fish, the Baby Shark, the
        /// harpy, the butterfly, the Duke Fishron, Moon Lord and Betsy pets and the Pufferfish. A spot 60 px behind and above
        /// the player (jittering 10-20 px each tick but for the fish and the Moonling), the shark and the pufferfish 70 px
        /// behind, bobbing with miscCounter; each with its speed, pull and frames.
        /// </summary>
        void PetHover(M m, PetProj p)
        {
            int type = p.Id;
            ref var velocity = ref m.Vel;
            float accel = 0.4f;
            if (type == 380) accel = 0.3f;
            if (type == 774 || type == 1046) accel = 0.3f;
            m.TileCollide = false;
            int land = 100;
            float slow = 50f, near = 400f, half = near / 2f, far = 2000f;
            bool jump = false;
            float dx = pCenter.x - m.Center.x, dy = pCenter.y - m.Center.y;
            if (type == 774 || type == 1046)
            {
                slow = 2f;
                float bob = Mathf.Sin(Mathf.PI * 2f * (miscCounter / 60f));
                float lift = Mathf.Clamp01(Mathf.InverseLerp(0.5f, 1f, bob));
                if (new Vector2(dx - 70 * pDir, dy - 60f).magnitude > 50f)
                {
                    bob = 0f;
                    lift = 0f;
                }
                dy += -60f + lift * -8f + bob * 8f;
                dx += 70 * -pDir;
            }
            else
            {
                if (type != 892 && type != 894)
                {
                    dy += Random.Range(-10, 21);
                    dx += Random.Range(-10, 21);
                }
                dx += 60 * -pDir;
                dy -= 60f;
            }
            var toSpot = new Vector2(dx, dy);
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float spotDist = dist;
            float speed = 14f;
            if (type == 380) speed = 6f;
            if (type == 815 || type == 817) speed = !(dist < near) ? 10f : 6f;
            if (type == 892 || type == 894 || type == 901)
            {
                if (dist < half) speed = 6f;
                speed = !(dist < near) ? 12f : 9f;
            }
            if (type == 774) speed = 5f;
            if (type == 1046)
            {
                speed = 5f;
                if (dist >= near) speed = 8f;
            }
            if (dist < land && ownerGrounded && m.Bottom <= pBox.yMax && !SolidBox(m.Box))
            {
                m.Ai0 = 0f;
                if (velocity.y < -6f) velocity.y = -6f;
            }
            if (dist < slow)
            {
                if (Mathf.Abs(velocity.x) > 2f || Mathf.Abs(velocity.y) > 2f) velocity *= type == 892 ? 0.95f : 0.99f;
                accel = 0.01f;
            }
            else
            {
                if (type == 892 || type == 894 || type == 901)
                {
                    if (dist < 100f) accel = 0.1f;
                    if (dist > far) jump = true;
                    else if (dist > near) accel = 0.7f;
                    else if (dist > half) accel = 0.5f;
                }
                else if (type == 815 || type == 817)
                {
                    if (dist < 100f) accel = 0.1f;
                    if (dist > far) jump = true;
                    else if (type != 815 && dist > near) accel = 0.5f;
                }
                else if (type == 380)
                {
                    if (dist < 100f) accel = 0.1f;
                    if (dist > 300f) accel = 0.4f;
                    if (dist > far) jump = true;
                }
                else if (type == 198 || type == 886)
                {
                    if (dist < 100f) accel = 0.1f;
                    if (dist > 300f) accel = 0.6f;
                    if (dist > far) jump = true;
                }
                else if (type == 774 || type == 1046)
                {
                    if (dist < 40f) accel = 0.1f;
                    if (dist > 300f) accel = 0.6f;
                    if (dist > far) jump = true;
                }
                float k = speed / dist;
                dx *= k;
                dy *= k;
            }
            if (velocity.x < dx)
            {
                velocity.x += accel;
                if (accel > 0.05f && velocity.x < 0f) velocity.x += accel;
            }
            if (velocity.x > dx)
            {
                velocity.x -= accel;
                if (accel > 0.05f && velocity.x > 0f) velocity.x -= accel;
            }
            if (velocity.y < dy)
            {
                velocity.y += accel;
                if (accel > 0.05f && velocity.y < 0f) velocity.y += accel * 2f;
            }
            if (velocity.y > dy)
            {
                velocity.y -= accel;
                if (accel > 0.05f && velocity.y > 0f) velocity.y -= accel * 2f;
            }
            if (velocity.x > 0.25f) m.Dir = -1;
            else if (velocity.x < -0.25f) m.Dir = 1;
            m.SpriteDir = m.Dir;
            m.Rot = velocity.x * 0.05f;
            if (jump)
            {
                int dust = 33;
                if (type == 198 || type == 886) dust = 147;
                if (type == 815) dust = 31;
                if (type == 817) dust = 21;
                TeleportPuff(m, dust);
                m.Center = pCenter;
                velocity = Vector2.zero;
            }
            // P:60660-60773: the frames, a frame every num57 ticks from num58 to num59 (the butterfly back and forth).
            bool pingPong = false;
            int ticks = 3, from = 0, to = 3;
            if (type == 380) ticks = 6;
            if (type == 815)
            {
                ticks = 5;
                from = 0;
                to = 5;
                if (spotDist > near)
                {
                    from = 6;
                    to = 9;
                }
            }
            if (type == 817)
            {
                ticks = 5;
                from = 0;
                to = 2;
                pingPong = true;
            }
            if (type == 901)
            {
                ticks = 4;
                from = 0;
                to = 5;
                if (spotDist > near / 2f)
                {
                    ticks = 3;
                    from = 6;
                    to = 11;
                }
            }
            if (type == 892)
            {
                ticks = 6;
                from = 0;
                to = p.Frames - 1;
            }
            if (type == 886 || type == 894)
            {
                ticks = 4;
                from = 0;
                to = p.Frames - 1;
            }
            if (type == 774)
            {
                // Out of the water the shark flaps its fins (frames 4-7).
                if (pVel.magnitude < 2f && toSpot.magnitude < 10f)
                {
                    m.Dir = -pDir;
                    m.SpriteDir = m.Dir;
                }
                ticks = 6;
                if (!PetWet)
                {
                    from += 4;
                    to += 4;
                }
                m.Rot = velocity.x * 0.05f + Mathf.Abs(velocity.y * -0.05f);
            }
            if (type == 1046)
            {
                from = 0;
                to = 3;
                if (pVel.magnitude < 2f && toSpot.magnitude < 10f)
                {
                    m.Dir = -pDir;
                    m.SpriteDir = m.Dir;
                }
                ticks = 7;
                if (spotDist >= near)
                {
                    from = 4;
                    to = 5;
                    ticks = 6;
                }
                if (!PetWet)
                {
                    from += 6;
                    to += 6;
                }
                m.Rot = velocity.x * 0.05f + Mathf.Abs(velocity.y * -0.05f);
            }
            if (pingPong)
            {
                int loop = ticks * (to - from) * 2;
                if (++m.FrameCounter >= loop) m.FrameCounter = 0;
                m.Frame = m.FrameCounter / ticks;
                if (m.Frame > to) m.Frame = to + (to - m.Frame);
                m.Frame = Mathf.Clamp(m.Frame, from, to);
            }
            else if (ticks > 0 && ++m.FrameCounter >= ticks)
            {
                m.FrameCounter = 0;
                m.Frame++;
                if (m.Frame < from || m.Frame > to) m.Frame = from;
            }
        }

        /// <summary>
        /// The Wisp (211, P:60775-60941): at the player's middle, 120 px out the way he holds left, right, down or up (else
        /// 60 px over him), 5 px a tick; over 200 px off it races at 12 a tick spinning, frames 5-9 and a trail of dust 156,
        /// until it is back within 10 px of a player standing still.
        /// </summary>
        void PetWisp(M m)
        {
            ref var velocity = ref m.Vel;
            float accel = 0.2f, speed = 5f;
            m.TileCollide = false;
            float dx = pCenter.x - m.Center.x, dy = pCenter.y - m.Center.y;
            var input = GameHooks.Input != null ? GameHooks.Input.inputActions : null;
            bool left = input != null && input.left.IsPressed, right = input != null && input.right.IsPressed;
            bool up = input != null && input.up.IsPressed, down = input != null && input.down.IsPressed;
            if (left) dx -= 120f;
            else if (right) dx += 120f;
            if (down) dy += 120f;
            else
            {
                if (up) dy -= 120f;
                dy -= 60f;
            }
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            if (dist > 1000f) m.Pos += new Vector2(dx, dy);
            if (m.LocalAi0 == 1f)
            {
                if (dist < 10f && Mathf.Abs(pVel.x) + Mathf.Abs(pVel.y) < speed && ownerGrounded) m.LocalAi0 = 0f;
                speed = 12f;
                if (dist < speed) velocity = new Vector2(dx, dy);
                else velocity = new Vector2(dx, dy) * (speed / dist);
                if (velocity.x > 0.5f) m.Dir = -1;
                else if (velocity.x < -0.5f) m.Dir = 1;
                m.SpriteDir = m.Dir;
                m.Rot -= (0.2f + Mathf.Abs(velocity.x) * 0.025f) * m.Dir;
                if (++m.FrameCounter > 3)
                {
                    m.Frame++;
                    m.FrameCounter = 0;
                }
                if (m.Frame < 5) m.Frame = 5;
                if (m.Frame > 9) m.Frame = 5;
                var tp = TerraParticles.Instance;
                for (int k = 0; k < 2; k++)
                {
                    int d = DustAt(m.Pos + new Vector2(3f, 4f), 14, 14, 156, Vector2.zero, 0, 1f);
                    if (d < 0) continue;
                    tp[d].Velocity *= 0.2f;
                    tp[d].NoGravity = true;
                    tp[d].Scale = 1.25f;
                }
                return;
            }
            if (dist > 200f) m.LocalAi0 = 1f;
            if (velocity.x > 0.5f) m.Dir = -1;
            else if (velocity.x < -0.5f) m.Dir = 1;
            m.SpriteDir = m.Dir;
            if (dist < 10f)
            {
                velocity = new Vector2(dx, dy);
                m.Rot = velocity.x * 0.05f;
                if (dist < speed)
                {
                    m.Pos += velocity;
                    velocity *= 0f;
                    accel = 0f;
                }
                m.Dir = -pDir;
            }
            float s = speed / dist;
            dx *= s;
            dy *= s;
            if (velocity.x < dx)
            {
                velocity.x += accel;
                if (velocity.x < 0f) velocity.x *= 0.99f;
            }
            if (velocity.x > dx)
            {
                velocity.x -= accel;
                if (velocity.x > 0f) velocity.x *= 0.99f;
            }
            if (velocity.y < dy)
            {
                velocity.y += accel;
                if (velocity.y < 0f) velocity.y *= 0.99f;
            }
            if (velocity.y > dy)
            {
                velocity.y -= accel;
                if (velocity.y > 0f) velocity.y *= 0.99f;
            }
            if (velocity.x != 0f || velocity.y != 0f) m.Rot = velocity.x * 0.05f;
            if (++m.FrameCounter > 3)
            {
                m.Frame++;
                m.FrameCounter = 0;
            }
            if (m.Frame > 4) m.Frame = 0;
        }

        /// <summary>
        /// The Tiki Spirit (199, P:60942-61088): 60 px over the player (2 px left), 4 px a tick (10 and a harder pull past
        /// 350); tilting with its rise; within 50 px its eight frames turn round its way, further off it looks left (frame 2)
        /// or right (6) as it goes.
        /// </summary>
        void PetTiki(M m)
        {
            ref var velocity = ref m.Vel;
            float accel = 0.1f;
            m.TileCollide = false;
            float dx = pCenter.x - m.Center.x, dy = pCenter.y - m.Center.y;
            dy -= 60f;
            dx -= 2f;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float speed = 4f, spotDist = dist;
            bool far = dist > 2000f;
            if (dist < 200f && ownerGrounded && m.Bottom <= pBox.yMax && !SolidBox(m.Box))
            {
                m.Ai0 = 0f;
                if (velocity.y < -6f) velocity.y = -6f;
            }
            if (dist < 4f)
            {
                velocity = new Vector2(dx, dy);
                accel = 0f;
            }
            else
            {
                if (dist > 350f)
                {
                    accel = 0.2f;
                    speed = 10f;
                }
                float k = speed / dist;
                dx *= k;
                dy *= k;
            }
            if (far)
            {
                TeleportPuff(m, 2);
                m.Center = pCenter;
                velocity = Vector2.zero;
            }
            if (velocity.x < dx)
            {
                velocity.x += accel;
                if (velocity.x < 0f) velocity.x += accel;
            }
            if (velocity.x > dx)
            {
                velocity.x -= accel;
                if (velocity.x > 0f) velocity.x -= accel;
            }
            if (velocity.y < dy)
            {
                velocity.y += accel;
                if (velocity.y < 0f) velocity.y += accel;
            }
            if (velocity.y > dy)
            {
                velocity.y -= accel;
                if (velocity.y > 0f) velocity.y -= accel;
            }
            m.Dir = -pDir;
            m.SpriteDir = 1;
            m.Rot = velocity.y * 0.05f * -m.Dir;
            if (spotDist >= 50f)
            {
                if (++m.FrameCounter <= 6) return;
                m.FrameCounter = 0;
                if (velocity.x < 0f)
                {
                    if (m.Frame < 2) m.Frame++;
                    if (m.Frame > 2) m.Frame--;
                }
                else
                {
                    if (m.Frame < 6) m.Frame++;
                    if (m.Frame > 6) m.Frame--;
                }
            }
            else
            {
                if (++m.FrameCounter > 6)
                {
                    m.Frame += m.Dir;
                    m.FrameCounter = 0;
                }
                if (m.Frame > 7) m.Frame = 0;
                if (m.Frame < 0) m.Frame = 7;
            }
        }

        /// <summary>
        /// The Skeletron and Skeletron Prime pets (885, 889; P:61089-61255): 65 px over the player and 30 behind, 8 px a tick
        /// (12 and a harder pull past 150), slowing within 10; facing the player's way. Far off the head spins (frame 6) and
        /// the Prime's arms reach out (localAI[0] up to 100); near, six frames of 5 ticks and the hands' pose cycling
        /// every 10 ticks (localAI[0] 0-1-2-1).
        /// </summary>
        void PetSkeletron(M m, int type)
        {
            ref var velocity = ref m.Vel;
            float accel = 0.15f;
            m.TileCollide = false;
            const int lazy = 150;
            float dx = pCenter.x - m.Center.x, dy = pCenter.y - m.Center.y;
            dy -= 65f;
            dx -= 30 * pDir;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float speed = 8f, spotDist = dist;
            bool far = dist > 2000f;
            if (dist < lazy && ownerGrounded && m.Bottom <= pBox.yMax && !SolidBox(m.Box) && velocity.y < -6f) velocity.y = -6f;
            if (dist < 10f)
            {
                velocity *= 0.9f;
                if (velocity.magnitude < 0.5f) velocity = Vector2.zero;
                accel = 0f;
            }
            else
            {
                if (dist > lazy)
                {
                    accel = 0.2f;
                    speed = 12f;
                }
                float k = speed / dist;
                dx *= k;
                dy *= k;
            }
            if (far)
            {
                TeleportPuff(m, type == 889 ? 60 : 234);
                m.Center = pCenter;
                velocity = Vector2.zero;
            }
            if (velocity.x < dx)
            {
                velocity.x += accel;
                if (velocity.x < 0f) velocity.x += accel;
            }
            if (velocity.x > dx)
            {
                velocity.x -= accel;
                if (velocity.x > 0f) velocity.x -= accel;
            }
            if (velocity.y < dy)
            {
                velocity.y += accel;
                if (velocity.y < 0f) velocity.y += accel;
            }
            if (velocity.y > dy)
            {
                velocity.y -= accel;
                if (velocity.y > 0f) velocity.y -= accel;
            }
            m.Dir = -pDir;
            m.SpriteDir = -m.Dir;
            const int reach = 100;
            if (spotDist >= lazy)
            {
                m.Rot += 0.5f;
                if (m.Rot > Mathf.PI * 2f) m.Rot -= Mathf.PI * 2f;
                m.Frame = 6;
                m.FrameCounter = 0;
                if (type == 885) m.LocalAi0 = 0f;
                if (type == 889) m.LocalAi0 = Mathf.Min(m.LocalAi0 + 3f, reach);
                return;
            }
            m.Rot *= 0.95f;
            if (m.Rot < 0.05f) m.Rot = 0f;
            m.FrameCounter++;
            if (type == 885)
            {
                switch (m.FrameCounter)
                {
                    case 10: m.LocalAi0 = 0f; break;
                    case 20: m.LocalAi0 = 1f; break;
                    case 30: m.LocalAi0 = 2f; break;
                    case 40: m.LocalAi0 = 1f; break;
                }
            }
            if (type == 889) m.LocalAi0 = Mathf.Max(0f, m.LocalAi0 - 3f);
            if (m.FrameCounter % 5 == 0)
            {
                m.Frame++;
                if (m.Frame > 5) m.Frame = 0;
            }
            if (m.FrameCounter >= 40) m.FrameCounter = 0;
        }

        // ------------------------------------------------------------------ flying back (ai[0] = 1)

        /// <summary>
        /// P:61390-62590: back through blocks to the player, landing within num102 (200; the turtle 100, the 1.4 pets 100,
        /// the Volt Bunny 100 at 2 px a tick of pull) once he stands and it is clear; each with its speed (10; bunny 11,
        /// turtle 9, Volt Bunny 16, Cursed Sapling 20, the 1.4.3+ pets 12 or the player's speed), its frames, turn and trail.
        /// </summary>
        void FlyBack026(M m, PetProj p)
        {
            int type = p.Id;
            ref var velocity = ref m.Vel;
            float accel = 0.2f;
            int land = 200;
            if (type == 127) land = 100;
            if (type == 816 || type == 821 || type == 825 || type == 854 || type == 858 || type == 859 || type == 860)
            {
                accel = 0.5f;
                land = 100;
            }
            if (type == 875)
            {
                accel = 2f;
                land = 100;
            }
            m.TileCollide = false;
            float dx = pCenter.x - m.Center.x;
            float dy = pCenter.y - m.Center.y;
            // The turtle aims for the player's head.
            if (type == 127) dy = pBox.yMin - m.Center.y;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            float spotDist = dist, speed = 10f, curDist = dist;
            if (type == 111) speed = 11f;
            if (type == 127) speed = 9f;
            if (type == 875)
            {
                accel = 1.8f;
                speed = 16f;
            }
            if (type == 324) speed = 20f;
            if (type == 816 || type == 821 || type == 825 || type == 854 || type == 858 || type == 859 || type == 860 || type == 956 || type == 958 || type == 959
                || type == 960 || type == 994 || type == 998 || type == 1003 || type == 1004 || type == 1027)
            {
                accel = 0.4f;
                speed = 12f;
                float ps = Mathf.Abs(pVel.x) + Mathf.Abs(pVel.y);
                if (speed < ps) speed = ps;
            }
            // The parrot keeps flying while the player is on the move.
            if (type == 208 && Mathf.Abs(pVel.x) + Mathf.Abs(pVel.y) > 4f) land = -1;
            if (dist < land && ownerGrounded && m.Bottom <= pBox.yMax && !SolidBox(m.Box))
            {
                m.Ai0 = 0f;
                if (velocity.y < -6f) velocity.y = -6f;
            }
            if (dist < 60f)
            {
                dx = velocity.x;
                dy = velocity.y;
            }
            else
            {
                float k = speed / dist;
                dx *= k;
                dy *= k;
            }
            if (type == 324)
            {
                // P:61510-61558: the Cursed Sapling eases its speed toward 20, softer the nearer it is.
                float v = Mathf.Abs(velocity.x) + Mathf.Abs(velocity.y);
                if (curDist > 1000f)
                {
                    if (v < speed - 1.25f) velocity *= 1.025f;
                    if (v > speed + 1.25f) velocity *= 0.975f;
                }
                else if (curDist > 600f)
                {
                    if (v < speed - 1f) velocity *= 1.05f;
                    if (v > speed + 1f) velocity *= 0.95f;
                }
                else if (curDist > 400f)
                {
                    if (v < speed - 0.5f) velocity *= 1.075f;
                    if (v > speed + 0.5f) velocity *= 0.925f;
                }
                else
                {
                    if (v < speed - 0.25f) velocity *= 1.1f;
                    if (v > speed + 0.25f) velocity *= 0.9f;
                }
                velocity.x = (velocity.x * 34f + dx) / 35f;
                velocity.y = (velocity.y * 34f + dy) / 35f;
            }
            else if (type == 875)
            {
                if (spotDist < land)
                {
                    if (velocity.x < dx) velocity.x += accel;
                    else if (velocity.x > dx) velocity.x -= accel;
                    if (velocity.y < dy) velocity.y += accel;
                    else if (velocity.y > dy) velocity.y -= accel;
                }
                else velocity = Vector2.Lerp(velocity, new Vector2(dx, dy), 0.75f);
            }
            else
            {
                if (velocity.x < dx)
                {
                    velocity.x += accel;
                    if (velocity.x < 0f) velocity.x += accel * 1.5f;
                }
                if (velocity.x > dx)
                {
                    velocity.x -= accel;
                    if (velocity.x > 0f) velocity.x -= accel * 1.5f;
                }
                if (velocity.y < dy)
                {
                    velocity.y += accel;
                    if (velocity.y < 0f) velocity.y += accel * 1.5f;
                }
                if (velocity.y > dy)
                {
                    velocity.y -= accel;
                    if (velocity.y > 0f) velocity.y -= accel * 1.5f;
                }
            }
            FlyFrames026(m, p);
            FlyTrail026(m, type);
        }

        /// <summary>P:61620-62469: each pet's frames and turn while it flies back.</summary>
        void FlyFrames026(M m, PetProj p)
        {
            int type = p.Id;
            ref var velocity = ref m.Vel;
            if (type == 111) m.Frame = 7;
            if (type != 313)
            {
                if (velocity.x > 0.5f) m.SpriteDir = -1;
                else if (velocity.x < -0.5f) m.SpriteDir = 1;
            }
            switch (type)
            {
                case 112:
                    m.FrameCounter++;
                    if (m.Frame < 3)
                    {
                        m.Frame = 3;
                        m.FrameCounter = 0;
                    }
                    if (m.FrameCounter > 3)
                    {
                        m.FrameCounter = 0;
                        if (++m.Frame >= p.Frames) m.Frame = 3;
                    }
                    m.Rot = velocity.x * 0.125f;
                    return;
                case 900:
                    if (velocity.x != 0f) m.SpriteDir = (int)Mathf.Sign(velocity.x);
                    LoopFrom(m, 10, p.Frames, 3);
                    m.Rot = velocity.x * 0.125f;
                    return;
                case 899:
                {
                    if (velocity.x != 0f) m.SpriteDir = (int)Mathf.Sign(velocity.x);
                    LoopFrom(m, 10, p.Frames, 3);
                    m.Rot = Heading(velocity, Vector2.right);
                    if (velocity.x < 0f) m.Rot += Mathf.PI;
                    return;
                }
                case 897:
                    m.SpriteDir = 1;
                    LoopFrom(m, 8, p.Frames, 3);
                    m.Rot = Heading(velocity, UpT) + Mathf.PI / 2f;
                    return;
                case 891:
                    m.SpriteDir = 1;
                    LoopFrom(m, 9, p.Frames, 3);
                    m.Rot = Heading(velocity, UpT) + Mathf.PI / 2f;
                    return;
                case 890:
                    if (velocity.x != 0f) m.SpriteDir = (int)Mathf.Sign(velocity.x);
                    LoopFrom(m, 9, p.Frames, 3);
                    m.Rot = velocity.x * 0.025f;
                    return;
                case 884:
                    if (velocity.x != 0f) m.SpriteDir = (int)Mathf.Sign(velocity.x);
                    LoopFrom(m, 9, p.Frames, 3);
                    m.Rot = Heading(velocity, UpT) + Mathf.PI / 2f;
                    return;
                case 881:
                case 934:
                    // Off the ground the slime royal's crown falls off (KingSlimePetCrown, QueenSlimePetCrown).
                    if (m.Frame < 6 || m.Frame > 11) PetGore(new Vector2(m.Center.x, m.Pos.y), velocity * 0.5f, type == 934 ? 1261 : 1226);
                    if (++m.FrameCounter > 4)
                    {
                        m.Frame++;
                        m.FrameCounter = 0;
                    }
                    if (m.Frame < 6 || m.Frame > 11) m.Frame = 6;
                    m.Rot = Heading(velocity, UpT) + Mathf.PI / 2f;
                    return;
                case 875:
                    if (++m.FrameCounter > 4)
                    {
                        m.Frame++;
                        m.FrameCounter = 0;
                    }
                    if (m.Frame < 7 || m.Frame > 10) m.Frame = 7;
                    m.Rot = Heading(velocity, Vector2.left * m.SpriteDir) + (m.SpriteDir == -1 ? 0f : Mathf.PI);
                    return;
                case 825:
                    Cycle(m, 4, 21, 25);
                    m.Rot = velocity.x * 0.025f;
                    return;
                case 854:
                    // The Plantero's sombrero comes off.
                    if (m.Frame < 13) PetGore(new Vector2(m.Center.x, m.Pos.y), velocity * 0.5f, 1269);
                    Cycle(m, 4, 13, 18);
                    m.Rot = Heading(velocity, UpT) + Mathf.PI / 2f;
                    return;
                case 858:
                    Cycle(m, 4, 10, 13);
                    m.Rot = Heading(velocity, Vector2.left * m.SpriteDir) + (m.SpriteDir == -1 ? 0f : Mathf.PI);
                    return;
                case 859:
                    Cycle(m, 4, 18, 23);
                    m.Rot = velocity.x * 0.025f;
                    return;
                case 860:
                    if (m.Frame < 6)
                    {
                        m.Frame = 8;
                        m.FrameCounter = 0;
                    }
                    if (++m.FrameCounter > 4)
                    {
                        m.Frame++;
                        m.FrameCounter = 0;
                    }
                    if (m.Frame > 13) m.Frame = 6;
                    m.Rot = Heading(velocity, UpT) + Mathf.PI / 2f;
                    return;
                case 816:
                    Cycle(m, 4, 11, 16);
                    m.Rot = velocity.x * 0.025f;
                    return;
                case 821:
                    Cycle(m, 4, 19, 22);
                    m.Rot = velocity.x * 0.025f;
                    return;
                case 958:
                    if (velocity.x != 0f) m.SpriteDir = (int)Mathf.Sign(velocity.x);
                    if (m.Frame < 13)
                    {
                        m.Frame = 13;
                        m.FrameCounter = 0;
                    }
                    if (++m.FrameCounter > 4)
                    {
                        m.Frame++;
                        m.FrameCounter = 0;
                    }
                    if (m.Frame > 16) m.Frame = 13;
                    m.Rot = Mathf.Clamp(velocity.x * 0.025f, -0.4f, 0.4f);
                    return;
                case 960:
                    if (velocity.x != 0f) m.SpriteDir = (int)Mathf.Sign(velocity.x);
                    m.Frame = 4;
                    m.FrameCounter = 0;
                    m.Rot = Heading(velocity, UpT) + Mathf.PI / 2f;
                    return;
                case 959:
                {
                    if (velocity.x != 0f) m.SpriteDir = (int)Mathf.Sign(velocity.x);
                    m.Frame = 11;
                    m.FrameCounter = 0;
                    m.Rot = Mathf.Clamp(velocity.x * 0.025f, -0.4f, 0.4f);
                    // P:61997: the pig's wings puff as it climbs.
                    float upward = Vector2.Dot(velocity.sqrMagnitude > 0f ? velocity.normalized : Vector2.right, new Vector2(0f, -1f));
                    var tp = TerraParticles.Instance;
                    if (upward > 0f && Random.value < 0.3f + upward * 0.3f && tp != null)
                    {
                        int d = tp.NewDust(m.Pos, m.W, m.H, 16, velocity.x * 0.7f, velocity.y * 1.2f, 150, 0.6f);
                        if (d >= 0)
                        {
                            tp[d].NoGravity = true;
                            tp[d].FadeIn = 1f;
                            tp[d].Velocity.x = velocity.x * 0.3f;
                            tp[d].Position = new Vector2(m.Center.x, m.Pos.y) + new Vector2(0f, -10f) + new Vector2(Random.Range(-1f, 1f) * 30f, Random.Range(-1f, 1f) * 10f);
                            tp[d].Velocity *= 0.7f;
                            tp[d].Position += tp[d].Velocity * 2f;
                        }
                    }
                    return;
                }
                case 994:
                    if (velocity.x != 0f) m.SpriteDir = (int)Mathf.Sign(velocity.x);
                    Cycle(m, 5, 13, 15);
                    m.Rot = Mathf.Clamp(velocity.x * 0.025f, -0.4f, 0.4f);
                    return;
                case 998:
                    if (velocity.x != 0f) m.SpriteDir = -(int)Mathf.Sign(velocity.x);
                    Cycle(m, 4, 6, 9);
                    m.Rot = Mathf.Clamp(velocity.x * 0.025f, -0.4f, 0.4f);
                    return;
                case 1003:
                    if (velocity.x != 0f) m.SpriteDir = -(int)Mathf.Sign(velocity.x);
                    Cycle(m, 3, 12, 15);
                    m.Rot = Mathf.Clamp(velocity.x * 0.025f, -0.35f, 0.35f);
                    return;
                case 1004:
                    if (velocity.x != 0f) m.SpriteDir = -(int)Mathf.Sign(velocity.x);
                    Cycle(m, 3, 10, 14);
                    m.Rot = Mathf.Clamp(velocity.x * 0.025f, -0.35f, 0.35f);
                    return;
                case 334:
                    Cycle(m, 1, 7, 10);
                    m.Rot = velocity.x * 0.1f;
                    return;
                case 353:
                    Cycle(m, 6, 10, 13);
                    m.Rot = velocity.x * 0.05f;
                    return;
                case 127:
                    m.FrameCounter += 3;
                    if (m.FrameCounter > 6)
                    {
                        m.Frame++;
                        m.FrameCounter = 0;
                    }
                    if (m.Frame <= 5 || m.Frame > 15) m.Frame = 6;
                    m.Rot = velocity.x * 0.1f;
                    return;
                case 269:
                    if (m.Frame == 6) m.FrameCounter = 0;
                    else if (m.Frame < 4 || m.Frame > 6)
                    {
                        m.FrameCounter = 0;
                        m.Frame = 4;
                    }
                    else if (++m.FrameCounter > 6)
                    {
                        m.Frame++;
                        m.FrameCounter = 0;
                    }
                    m.Rot = velocity.x * 0.05f;
                    return;
                case 324:
                {
                    Cycle(m, 1, 6, 9);
                    m.Rot = Mathf.Atan2(velocity.y, velocity.x) + 1.58f;
                    // P:62241-62260: flying, the Cursed Sapling burns (its light and two flames and smokes a tick).
                    FxBatch.Instance?.Light(m.Center, new Color(0.9f, 0.6f, 0.2f), 5f);
                    var tp = TerraParticles.Instance;
                    if (tp == null) return;
                    const int r = 4;
                    for (int k = 0; k < 2; k++)
                    {
                        int d = tp.NewDust(m.Center - new Vector2(r, r), r * 2, r * 2, 6, 0f, 0f, 100);
                        if (d >= 0)
                        {
                            tp[d].Scale *= 1.8f + Random.Range(0, 10) * 0.1f;
                            tp[d].Velocity *= 0.2f;
                            if (k == 1) tp[d].Position -= velocity * 0.5f;
                            tp[d].NoGravity = true;
                        }
                        d = tp.NewDust(m.Center - new Vector2(r, r), r * 2, r * 2, 31, 0f, 0f, 100, 0.5f);
                        if (d >= 0)
                        {
                            tp[d].FadeIn = 1f + Random.Range(0, 5) * 0.1f;
                            tp[d].Velocity *= 0.05f;
                            if (k == 1) tp[d].Position -= velocity * 0.5f;
                        }
                    }
                    return;
                }
                case 268:
                    Cycle(m, 4, 6, 7);
                    m.Rot = Mathf.Atan2(velocity.y, velocity.x) + 1.58f;
                    return;
                case 200:
                    m.FrameCounter += 3;
                    if (m.FrameCounter > 6)
                    {
                        m.Frame++;
                        m.FrameCounter = 0;
                    }
                    if (m.Frame <= 5 || m.Frame > 9) m.Frame = 6;
                    m.Rot = velocity.x * 0.1f;
                    return;
                case 208:
                    m.Rot = velocity.x * 0.075f;
                    if (++m.FrameCounter > 6)
                    {
                        m.Frame++;
                        m.FrameCounter = 0;
                    }
                    if (m.Frame > 4) m.Frame = 1;
                    if (m.Frame < 1) m.Frame = 1;
                    return;
                case 236:
                    m.Rot = velocity.y * 0.05f * m.Dir;
                    m.FrameCounter += velocity.y < 0f ? 2 : 1;
                    if (m.FrameCounter >= 6)
                    {
                        m.Frame++;
                        m.FrameCounter = 0;
                    }
                    if (m.Frame > 12) m.Frame = 9;
                    if (m.Frame < 9) m.Frame = 9;
                    return;
                case 499:
                    m.Rot = velocity.y * 0.05f * m.Dir;
                    m.FrameCounter += velocity.y < 0f ? 2 : 1;
                    if (m.FrameCounter >= 6)
                    {
                        m.Frame++;
                        m.FrameCounter = 0;
                    }
                    if (m.Frame >= 12) m.Frame = 8;
                    if (m.Frame < 8) m.Frame = 8;
                    return;
                case 765:
                    m.Rot = velocity.y * 0.05f * m.Dir;
                    if (++m.FrameCounter >= 8)
                    {
                        m.Frame++;
                        m.FrameCounter = 0;
                    }
                    if (m.Frame >= 10) m.Frame = 8;
                    if (m.Frame < 6) m.Frame = 6;
                    return;
                case 314:
                    m.Rot = Mathf.Atan2(velocity.y, velocity.x) + 1.58f;
                    if (++m.FrameCounter >= 3)
                    {
                        m.Frame++;
                        m.FrameCounter = 0;
                    }
                    if (m.Frame > 12) m.Frame = 7;
                    if (m.Frame < 7) m.Frame = 7;
                    return;
                case 319:
                    m.Rot = velocity.x * 0.05f;
                    if (++m.FrameCounter >= 6)
                    {
                        m.Frame++;
                        m.FrameCounter = 0;
                    }
                    if (m.Frame > 10) m.Frame = 6;
                    if (m.Frame < 6) m.Frame = 6;
                    return;
                case 210:
                    m.Rot = Mathf.Atan2(velocity.y, velocity.x) + 1.58f;
                    m.FrameCounter += 3;
                    if (m.FrameCounter > 6)
                    {
                        m.Frame++;
                        m.FrameCounter = 0;
                    }
                    if (m.Frame > 11) m.Frame = 7;
                    if (m.Frame < 7) m.Frame = 7;
                    return;
                case 313:
                    // The spider curls up into a 54 px ball (its box grown round its bottom middle).
                    Resize(m, 54, 54);
                    m.Rot += velocity.x * 0.01f;
                    m.FrameCounter = 0;
                    m.Frame = 11;
                    return;
                case 398:
                    if (velocity.x > 0.5f) m.SpriteDir = 1;
                    else if (velocity.x < -0.5f) m.SpriteDir = -1;
                    Cycle(m, 4, 7, 10);
                    return;
            }
            m.Rot = m.SpriteDir == -1 ? Mathf.Atan2(velocity.y, velocity.x) : Mathf.Atan2(velocity.y, velocity.x) + 3.14f;
        }

        /// <summary>The frames from `from` on, a frame each time the counter passes `ticks`, back to `from` at `wrap`.</summary>
        static void LoopFrom(M m, int from, int wrap, int ticks)
        {
            m.FrameCounter++;
            if (m.Frame < from)
            {
                m.Frame = from;
                m.FrameCounter = 0;
            }
            if (m.FrameCounter > ticks)
            {
                m.FrameCounter = 0;
                if (++m.Frame >= wrap) m.Frame = from;
            }
        }

        /// <summary>A frame each time the counter passes `ticks`, held in from..to.</summary>
        static void Cycle(M m, int ticks, int from, int to)
        {
            if (++m.FrameCounter > ticks)
            {
                m.Frame++;
                m.FrameCounter = 0;
            }
            if (m.Frame < from || m.Frame > to) m.Frame = from;
        }

        /// <summary>A new hitbox round the old one's bottom middle (the spider's: P:62341, 64987, 65034).</summary>
        static void Resize(M m, int w, int h)
        {
            m.Pos.y += m.H;
            m.H = h;
            m.Pos.y -= m.H;
            m.Pos.x += m.W / 2;
            m.W = w;
            m.Pos.x -= m.W / 2;
        }

        /// <summary>P:62470-62589: the trails flying back leaves (jet flames, sparks, shadow, a cloud).</summary>
        void FlyTrail026(M m, int type)
        {
            switch (type)
            {
                case 499: case 765: case 816: case 821: case 825: case 859: case 881: case 884: case 890: case 891: case 900: case 934: case 958: case 959: case 960:
                case 994: case 998: case 1003: case 1004: case 1027:
                    return;
            }
            var tp = TerraParticles.Instance;
            if (tp == null) return;
            ref var velocity = ref m.Vel;
            if (type == 899 || type == 897)
            {
                int dust = Random.Range(0, 4) == 0 ? 31 : 6;
                var at = m.Center + new Vector2(-8f, type == 897 ? -8f : 0f) - velocity * 0.25f;
                int d = tp.NewDust(at, 15, 15, dust, -velocity.x * 0.5f, velocity.y * 0.5f, 0, 1.3f);
                if (d < 0) return;
                tp[d].Velocity.x *= 0.2f;
                tp[d].Velocity.y = tp[d].Velocity.y * 0.2f - 0.2f;
                tp[d].NoGravity = true;
            }
            else if (type == 875)
            {
                // (the Volt Bunny's spark gores, 1225, have their own gore AI: only its dust)
                if (Random.Range(0, 3) != 0) return;
                var v = Random.insideUnitCircle.normalized * 2f + velocity * -0.5f;
                v *= 0.5f;
                int d = tp.NewDust(m.Center - velocity * 1.5f - new Vector2(7f, 7f), 15, 15, 226, v.x, v.y, 0, 0.65f);
                if (d >= 0) tp[d].NoGravity = true;
            }
            else if (type == 860)
            {
                int d = tp.NewDust(new Vector2(m.Pos.x + m.W / 2 - 4f, m.Pos.y + m.H / 2 - 4f) + velocity, 8, 8, 27, -velocity.x * 0.5f, velocity.y * 0.5f);
                if (d < 0) return;
                tp[d].Velocity.x *= 0.2f;
                tp[d].Velocity.y = tp[d].Velocity.y * 0.2f - 0.2f;
                tp[d].Velocity += new Vector2(Random.Range(-10, 10) * 0.4f, Random.Range(-10, 10) * 0.4f) * 0.5f;
                tp[d].NoGravity = true;
            }
            else if (type == 858)
            {
                int dust = Random.Range(0, 4) == 0 ? 31 : 6;
                int d = tp.NewDust(new Vector2(m.Pos.x + m.W / 2 - 4f, m.Pos.y + m.H / 2 - 4f) - velocity, 10, 10, dust, -velocity.x * 0.5f, velocity.y * 0.5f, 0, 1.3f);
                if (d < 0) return;
                tp[d].Velocity.x *= 0.2f;
                tp[d].Velocity.y = tp[d].Velocity.y * 0.2f - 0.2f;
                tp[d].NoGravity = true;
            }
            else if (type == 112)
            {
                // The penguin's jetpack.
                int dust = Random.Range(0, 4) == 0 ? 31 : 6;
                var at = m.Center + Rotated(new Vector2(12 * m.SpriteDir, 4f), m.Rot) + new Vector2(-5f, -5f);
                int d = tp.NewDust(at, 10, 10, dust, -velocity.x * 0.5f, velocity.y * 0.5f, 0, 1.3f);
                if (d < 0) return;
                tp[d].Velocity.x *= 0.2f;
                tp[d].Velocity.y = tp[d].Velocity.y * 0.2f - 0.2f;
                tp[d].NoGravity = true;
            }
            else if (type == 854)
            {
                // (the Plantero's falling leaves, gore 910, have their own gore AI)
            }
            else if (type != 398 && type != 127 && type != 200 && type != 208 && type != 210 && type != 236 && type != 268 && type != 269 && type != 313 && type != 314
                     && type != 319 && type != 324 && type != 334 && type != 353)
            {
                int d = tp.NewDust(new Vector2(m.Pos.x + m.W / 2 - 4f, m.Pos.y + m.H / 2 - 4f) - velocity, 8, 8, 16, -velocity.x * 0.5f, velocity.y * 0.5f, 50, 1.7f);
                if (d < 0) return;
                tp[d].Velocity.x *= 0.2f;
                tp[d].Velocity.y *= 0.2f;
                tp[d].NoGravity = true;
            }
        }

        // ------------------------------------------------------------------ walking (ai[0] = 0)

        /// <summary>
        /// P:63022-63335: on the ground. Its walk (0.08 a tick to 6.5; the turtle 0.04 to 2, the penguin 0.06 to 6, the puppy
        /// to 8, the Eyeball Spring 0.4 to 8, the Cursed Sapling 0.1 to 3, the Dynamite Kitten 0.3 to 7, the newer pets 0.2 to
        /// 6 or the player's speed at 0.3, the Volt Bunny 0.25 to 7); a block ahead makes it jump (-5.1 to -11.1 by the room
        /// over it), and the slime royals and the Shadow Mimic jump all the while they move; the lizard climbs at -3.1; the
        /// turtle takes off. Then each pet's frames and its fall.
        /// </summary>
        void Walk026(M m, PetProj p, bool flag2, bool flag3, bool flag5)
        {
            int type = p.Id;
            ref var velocity = ref m.Vel;
            if (type == 127)
            {
                if (m.Rot > -0.1f && m.Rot < 0.1f) m.Rot = 0f;
                else if (m.Rot < 0f) m.Rot += 0.1f;
                else m.Rot -= 0.1f;
            }
            else if (type != 313) m.Rot = 0f;
            m.TileCollide = true;
            float accel = 0.08f, max = 6.5f;
            if (type == 127)
            {
                max = 2f;
                accel = 0.04f;
            }
            if (type == 112)
            {
                max = 6f;
                accel = 0.06f;
            }
            if (type == 334)
            {
                max = 8f;
                accel = 0.08f;
            }
            if (type == 268)
            {
                max = 8f;
                accel = 0.4f;
            }
            if (type == 324)
            {
                accel = 0.1f;
                max = 3f;
            }
            if (type == 858)
            {
                accel = 0.3f;
                max = 7f;
            }
            float ps = Mathf.Abs(pVel.x) + Mathf.Abs(pVel.y);
            if (type == 816 || type == 821 || type == 825 || type == 859 || type == 860 || type == 881 || type == 884 || type == 890 || type == 891 || type == 897
                || type == 899 || type == 900 || type == 934 || type == 956 || type == 958 || type == 959 || type == 960 || type == 994 || type == 998 || type == 1003
                || type == 1004 || type == 1027)
            {
                max = 6f;
                accel = 0.2f;
                if (max < ps)
                {
                    max = ps;
                    accel = 0.3f;
                }
                if (type == 1027) accel *= 1.5f;
            }
            if (type == 875)
            {
                max = 7f;
                accel = 0.25f;
                if (max < ps)
                {
                    max = ps;
                    accel = 0.35f;
                }
            }
            if (flag2)
            {
                if (velocity.x > -3.5f) velocity.x -= accel;
                else velocity.x -= accel * 0.25f;
            }
            else if (flag3)
            {
                if (velocity.x < 3.5f) velocity.x += accel;
                else velocity.x += accel * 0.25f;
            }
            else
            {
                velocity.x *= 0.9f;
                if (velocity.x >= -accel && velocity.x <= accel) velocity.x = 0f;
            }
            // The parrot doesn't walk: it hops after the player only by flying.
            if (type == 208)
            {
                velocity.x *= 0.95f;
                if (velocity.x > -0.1f && velocity.x < 0.1f) velocity.x = 0f;
                flag2 = false;
                flag3 = false;
            }
            if (flag2 | flag3)
            {
                int i = TileOf(m.Pos.x + m.W / 2), j = TileOf(m.Pos.y + m.H / 2);
                if (type == 236) i += m.Dir;
                if (flag2) i--;
                if (flag3) i++;
                i += (int)velocity.x;
                if (SolidTile(i, j)) flag5 = true;
            }
            // The Eyeball Spring only springs once it has crouched (frameCounter 10).
            if (type == 268 && m.FrameCounter < 10) flag5 = false;
            // P:63206-63213: the Shadow Mimic and the slime royals hop all the while they move.
            if (type == 860 && velocity.x != 0f) flag5 = true;
            if ((type == 881 || type == 934) && velocity.x != 0f) flag5 = true;
            StepUp(m, m.FallThrough);
            if (velocity.y == 0f || type == 200)
            {
                if (flag5)
                {
                    int fi = TileOf(m.Pos.x + m.W / 2), fj = TileOf(m.Pos.y + m.H);
                    if (Floor026(fi, fj) || type == 200)
                    {
                        // The lizard climbs the wall.
                        if (type == 200) velocity.y = -3.1f;
                        else
                        {
                            int i = TileOf(m.Pos.x + m.W / 2), j = TileOf(m.Pos.y + m.H / 2);
                            if (flag2) i--;
                            if (flag3) i++;
                            i += (int)velocity.x;
                            velocity.y = WallJump(i, j);
                        }
                        // The turtle takes off at a wall.
                        if (type == 127) m.Ai0 = 1f;
                    }
                }
            }
            if (velocity.x > max) velocity.x = max;
            if (velocity.x < -max) velocity.x = -max;
            if (velocity.x < 0f) m.Dir = -1;
            if (velocity.x > 0f) m.Dir = 1;
            if (velocity.x > accel && flag3) m.Dir = 1;
            if (velocity.x < -accel && flag2) m.Dir = -1;
            if (type != 313)
            {
                if (m.Dir == -1) m.SpriteDir = 1;
                if (m.Dir == 1) m.SpriteDir = -1;
            }
            if (type == 398 || type == 958 || type == 960 || type == 956 || type == 959 || type == 994) m.SpriteDir = m.Dir;
            bool still = m.Pos.x - m.OldPos.x == 0f;
            WalkFrames026(m, p, still, flag2, flag3);
        }

        /// <summary>The player is right of the pet: 1, else -1 (the idle turn to face him).</summary>
        int FacingPlayer(M m) => pCenter.x < m.Center.x ? -1 : 1;

        /// <summary>The usual fall: 0.4 a tick to 10.</summary>
        static void Fall(M m)
        {
            m.Vel.y += 0.4f;
            if (m.Vel.y > 10f) m.Vel.y = 10f;
        }

        /// <summary>P:63336-66030: each pet's frames on the ground, and its fall.</summary>
        void WalkFrames026(M m, PetProj p, bool still, bool flag2, bool flag3)
        {
            int type = p.Id;
            ref var velocity = ref m.Vel;
            var tp = TerraParticles.Instance;
            switch (type)
            {
                case 956:
                {
                    // Bernie walks in again out of a puff of flame.
                    int alpha = AlphaOf(m);
                    if (alpha > 0)
                    {
                        int d = tp != null ? tp.NewDust(m.Pos, m.W, m.H, 6, velocity.x, velocity.y, 0, 1.2f) : -1;
                        if (d >= 0)
                        {
                            tp[d].Velocity.x += Random.value - 0.5f;
                            tp[d].Velocity.y += (Random.value + 0.5f) * -1f;
                            if (Random.Range(0, 3) != 0) tp[d].NoGravity = true;
                        }
                        SetAlpha(m, alpha - 5);
                    }
                    if (velocity.y != 0f) m.Frame = 10;
                    else if (still)
                    {
                        m.SpriteDir = FacingPlayer(m);
                        m.Frame = 0;
                    }
                    else
                    {
                        m.FrameCounter += (int)velocity.magnitude;
                        if (m.FrameCounter > 7)
                        {
                            m.Frame++;
                            m.FrameCounter = 0;
                        }
                        if (m.Frame < 1 || m.Frame > 9) m.Frame = 1;
                    }
                    Fall(m);
                    return;
                }
                case 958:
                    // The baby Deerclops looks round now and then while it stands (frames 1-2 after 200 ticks).
                    if (velocity.y != 0f)
                    {
                        m.LocalAi0 = 0f;
                        m.Frame = 4;
                    }
                    else if (still)
                    {
                        m.SpriteDir = FacingPlayer(m);
                        m.LocalAi0++;
                        if (m.LocalAi0 > 200f)
                        {
                            m.Frame = 1 + (int)(m.LocalAi0 - 200f) / 6;
                            if (m.LocalAi0 >= 218f)
                            {
                                m.Frame = 0;
                                m.LocalAi0 = Random.Range(0, 100);
                            }
                        }
                        else m.Frame = 0;
                    }
                    else
                    {
                        m.LocalAi0 = 0f;
                        m.FrameCounter += (int)velocity.magnitude;
                        if (m.FrameCounter > 6)
                        {
                            m.Frame++;
                            m.FrameCounter = 0;
                        }
                        if (m.Frame < 5 || m.Frame > 12) m.Frame = 5;
                    }
                    Fall(m);
                    return;
                case 959:
                    if (velocity.y != 0f) m.Frame = velocity.y > 0f ? 10 : 9;
                    else if (still)
                    {
                        m.SpriteDir = FacingPlayer(m);
                        m.Frame = 0;
                    }
                    else
                    {
                        m.FrameCounter += (int)velocity.magnitude;
                        if (m.FrameCounter > 6)
                        {
                            m.Frame++;
                            m.FrameCounter = 0;
                        }
                        if (m.Frame < 1 || m.Frame > 8) m.Frame = 1;
                    }
                    Fall(m);
                    return;
                case 998:
                case 1003:
                case 1004:
                {
                    int lo = type == 998 ? 0 : 2, hi = type == 998 ? 5 : type == 1003 ? 11 : 9;
                    if (velocity.y != 0f) m.Frame = 1;
                    else if (still)
                    {
                        m.SpriteDir = -FacingPlayer(m);
                        m.Frame = 0;
                    }
                    else
                    {
                        m.FrameCounter += (int)velocity.magnitude;
                        if (m.FrameCounter > 6)
                        {
                            m.Frame++;
                            m.FrameCounter = 0;
                        }
                        if (m.Frame < lo || m.Frame > hi) m.Frame = lo;
                    }
                    Fall(m);
                    return;
                }
                case 1027:
                    if (AlphaOf(m) > 0)
                    {
                        SetAlpha(m, 0);
                        SwarmBurst(m);
                    }
                    if (velocity.y < 0f) m.Frame = 2;
                    else if (velocity.y > 0f) m.Frame = 4;
                    else if (still)
                    {
                        m.SpriteDir = -FacingPlayer(m);
                        m.Frame = 0;
                    }
                    else
                    {
                        float half = velocity.magnitude / 2f;
                        m.FrameCounter += Mathf.Max(1, (int)half);
                        if (m.FrameCounter > 5)
                        {
                            m.Frame++;
                            m.FrameCounter = 0;
                        }
                        if (m.Frame < 0 || m.Frame > 3) m.Frame = 0;
                    }
                    m.Rot = 0f;
                    if (velocity.y != 0f) m.Rot = velocity.y / 10f * (Mathf.PI / 4f) * -m.SpriteDir;
                    Fall(m);
                    return;
                case 994:
                    // The Junimo bobs (frames 0-3) while it waits.
                    if (velocity.y != 0f) m.Frame = 4;
                    else if (still)
                    {
                        m.SpriteDir = FacingPlayer(m);
                        if (++m.FrameCounter > 5)
                        {
                            m.Frame++;
                            m.FrameCounter = 0;
                        }
                        if (m.Frame > 3) m.Frame = 0;
                    }
                    else
                    {
                        m.FrameCounter += (int)velocity.magnitude;
                        if (m.FrameCounter > 6)
                        {
                            m.Frame++;
                            m.FrameCounter = 0;
                        }
                        if (m.Frame < 5 || m.Frame > 12) m.Frame = 5;
                    }
                    Fall(m);
                    return;
                case 960:
                    ChesterFrames(m, still);
                    Fall(m);
                    return;
                case 816:
                    if (velocity.y != 0f) m.Frame = 4;
                    else if (still)
                    {
                        m.SpriteDir = -FacingPlayer(m);
                        Cycle(m, 5, 0, 3);
                    }
                    else
                    {
                        int ticks = 5;
                        float v = velocity.magnitude;
                        if (v > 4f) ticks = 3;
                        else if (v > 2f) ticks = 4;
                        Cycle(m, ticks, 4, 10);
                    }
                    Fall(m);
                    return;
                case 860:
                    ShadowMimicFrames(m);
                    Fall(m);
                    return;
                case 859:
                    // The baby werewolf howls (frame 6) on its way back to standing.
                    if (velocity.y != 0f) m.Frame = 4;
                    else if (still)
                    {
                        m.SpriteDir = -FacingPlayer(m);
                        if (m.Frame == 6)
                        {
                            if (++m.FrameCounter > 5)
                            {
                                m.Frame = 0;
                                m.FrameCounter = 0;
                            }
                        }
                        else if (m.Frame > 3)
                        {
                            m.Frame = 6;
                            m.FrameCounter = 0;
                        }
                        else Cycle(m, 5, 0, 3);
                    }
                    else
                    {
                        float v = velocity.magnitude;
                        int ticks = 8;
                        if (v < 3f) ticks = 4;
                        if (v < 1f) ticks = 2;
                        m.FrameCounter += (int)v;
                        if (m.FrameCounter > ticks)
                        {
                            m.Frame++;
                            m.FrameCounter = 0;
                        }
                        if (m.Frame < 5 || m.Frame > 17) m.Frame = 5;
                    }
                    Fall(m);
                    return;
                case 858:
                    if (velocity.y != 0f) m.Frame = 1;
                    else if (still)
                    {
                        m.SpriteDir = -FacingPlayer(m);
                        m.Frame = 0;
                    }
                    else
                    {
                        m.FrameCounter += (int)velocity.magnitude;
                        if (m.FrameCounter > 3)
                        {
                            m.Frame++;
                            m.FrameCounter = 0;
                        }
                        if (m.Frame < 2 || m.Frame > 9) m.Frame = 2;
                    }
                    Fall(m);
                    return;
                case 900:
                case 899:
                case 897:
                case 891:
                case 890:
                {
                    // P:64055-64260: the newer boss pets, frames 2 on (to 9, 7 or 8).
                    int last = type == 900 || type == 899 ? 9 : type == 891 ? 8 : 7;
                    m.SpriteDir = m.Dir;
                    if (velocity.y != 0f)
                    {
                        m.Frame = 1;
                        m.FrameCounter = 0;
                    }
                    else if (still)
                    {
                        m.SpriteDir = type == 891 ? pDir : FacingPlayer(m);
                        m.Frame = 0;
                        m.FrameCounter = 0;
                    }
                    else
                    {
                        m.FrameCounter += 1 + (int)Mathf.Abs(velocity.x * 0.3f);
                        if (m.Frame < 2)
                        {
                            m.Frame = 2;
                            m.FrameCounter = 0;
                        }
                        if (m.FrameCounter > 4)
                        {
                            m.Frame++;
                            m.FrameCounter = 0;
                        }
                        if (m.Frame > last) m.Frame = 2;
                    }
                    Fall(m);
                    return;
                }
                case 884:
                    m.SpriteDir = m.Dir;
                    if (velocity.y != 0f)
                    {
                        m.Frame = velocity.y < 0f ? 9 : 1;
                        m.FrameCounter = 0;
                    }
                    else if (still)
                    {
                        m.SpriteDir = FacingPlayer(m);
                        m.Frame = 0;
                        m.FrameCounter = 0;
                    }
                    else
                    {
                        m.FrameCounter += 1 + (int)Mathf.Abs(velocity.x * 0.5f);
                        if (m.FrameCounter > 6)
                        {
                            m.Frame++;
                            m.FrameCounter = 0;
                        }
                        if (m.Frame > 8) m.Frame = 2;
                    }
                    Fall(m);
                    return;
                case 881:
                case 934:
                    // P:64305-64370: the slime royals face the player and squash through their hop: rising 0-4, falling
                    // to 2, landed on to 5 and back to 0, three ticks a frame.
                    m.SpriteDir = FacingPlayer(m);
                    if (velocity.y > 0f)
                    {
                        if (++m.FrameCounter > 2)
                        {
                            m.Frame++;
                            if (m.Frame >= 2) m.Frame = 2;
                            m.FrameCounter = 0;
                        }
                    }
                    else if (velocity.y < 0f)
                    {
                        if (++m.FrameCounter > 2)
                        {
                            m.Frame++;
                            if (m.Frame >= 5) m.Frame = 0;
                            m.FrameCounter = 0;
                        }
                    }
                    else if (m.Frame == 0) m.Frame = 0;
                    else if (++m.FrameCounter > 3)
                    {
                        m.Frame++;
                        if (m.Frame >= 6) m.Frame = 0;
                        m.FrameCounter = 0;
                    }
                    if (PetWet && pBox.yMax < m.Bottom && m.LocalAi0 == 0f)
                    {
                        if (velocity.y > -4f) velocity.y -= 0.2f;
                        if (velocity.y > 0f) velocity.y *= 0.95f;
                    }
                    else velocity.y += 0.4f;
                    if (velocity.y > 10f) velocity.y = 10f;
                    return;
                case 875:
                    if (velocity.y != 0f)
                    {
                        m.Frame = velocity.y < 0f ? 3 : 6;
                        m.FrameCounter = 0;
                    }
                    else if (still)
                    {
                        m.SpriteDir = -FacingPlayer(m);
                        m.Frame = 0;
                        m.FrameCounter = 0;
                    }
                    else
                    {
                        m.FrameCounter += 1 + (int)Mathf.Abs(velocity.x * 0.75f);
                        if (m.FrameCounter > 6)
                        {
                            m.Frame++;
                            m.FrameCounter = 0;
                        }
                        if (m.Frame > 6) m.Frame = 0;
                    }
                    Fall(m);
                    return;
                case 854:
                    if (velocity.y != 0f) m.Frame = 7;
                    else if (still)
                    {
                        m.SpriteDir = -FacingPlayer(m);
                        Cycle(m, 5, 0, 3);
                    }
                    else
                    {
                        int ticks = 3;
                        float v = velocity.magnitude;
                        if (v > 4f) ticks = 1;
                        else if (v > 2f) ticks = 2;
                        Cycle(m, ticks, 4, 12);
                    }
                    Fall(m);
                    return;
                case 825:
                    RedPandaFrames(m, still);
                    Fall(m);
                    return;
                case 821:
                    // The baby imp, left standing 400 ticks, plays with its fire (frames 5-11, frame 9 held 25 ticks).
                    if (velocity.y != 0f)
                    {
                        m.LocalAi0 = 0f;
                        m.Frame = 12;
                    }
                    else if (still)
                    {
                        m.SpriteDir = -FacingPlayer(m);
                        m.LocalAi0++;
                        if (m.LocalAi0 > 400f)
                        {
                            int ticks = m.Frame == 9 ? 25 : 7;
                            if (++m.FrameCounter > ticks)
                            {
                                m.Frame++;
                                m.FrameCounter = 0;
                            }
                            if (m.Frame < 5) m.Frame = 5;
                            if (m.Frame > 11)
                            {
                                m.LocalAi0 = 0f;
                                m.Frame = 0;
                            }
                        }
                        else Cycle(m, 6, 0, 4);
                    }
                    else
                    {
                        m.LocalAi0 = 0f;
                        int ticks = 4;
                        float v = velocity.magnitude;
                        if (v > 3f) ticks = 3;
                        if (v > 5f) ticks = 2;
                        if (++m.FrameCounter > ticks)
                        {
                            m.Frame++;
                            m.FrameCounter = 0;
                        }
                        if (m.Frame < 13 || m.Frame > 18) m.Frame = 13;
                    }
                    Fall(m);
                    return;
                case 268:
                    EyeSpringFrames(m, still, flag2, flag3);
                    Fall(m);
                    return;
                case 269:
                    // The snowman leaves a trail of snow (dust 76) as it walks.
                    if (velocity.y >= 0f && velocity.y <= 0.8f)
                    {
                        if (still)
                        {
                            m.Frame = 0;
                            m.FrameCounter = 0;
                        }
                        else if (velocity.x < -0.8f || velocity.x > 0.8f)
                        {
                            int d = tp != null ? tp.NewDust(new Vector2(m.Pos.x, m.Pos.y + m.H - 2f), m.W, 6, 76) : -1;
                            if (d >= 0)
                            {
                                tp[d].NoGravity = true;
                                tp[d].Velocity *= 0.3f;
                            }
                            m.FrameCounter += (int)Mathf.Abs(velocity.x);
                            m.FrameCounter++;
                            if (m.FrameCounter > 6)
                            {
                                m.Frame++;
                                m.FrameCounter = 0;
                            }
                            if (m.Frame > 3) m.Frame = 0;
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
                        m.Frame = 2;
                    }
                    Fall(m);
                    return;
                case 313:
                    SpiderPetFrames(m, still);
                    return;
                case 314:
                    StepFrames(m, still, 6, -1, 6, 1, 7);
                    Fall(m);
                    return;
                case 319:
                    StepFrames(m, still, 8, -1, 5, 2, 1);
                    Fall(m);
                    return;
                case 236:
                    StepFrames(m, still, 6, 2, 8, 2, 1);
                    Fall(m);
                    return;
                case 499:
                    StepFrames(m, still, 6, 2, 7, 2, 1);
                    Fall(m);
                    return;
                case 765:
                    StepFrames(m, still, 6, 1, 5, 1, 0);
                    Fall(m);
                    return;
                case 334:
                    // The puppy wags (its frames run on to 6 and back to 0) once it stops.
                    if (velocity.y == 0f)
                    {
                        if (still)
                        {
                            if (m.Frame > 0)
                            {
                                m.FrameCounter += 2;
                                if (m.FrameCounter > 6)
                                {
                                    m.Frame++;
                                    m.FrameCounter = 0;
                                }
                                if (m.Frame >= 7) m.Frame = 0;
                            }
                            else
                            {
                                m.Frame = 0;
                                m.FrameCounter = 0;
                            }
                        }
                        else if (velocity.x < -0.8f || velocity.x > 0.8f)
                        {
                            m.FrameCounter += (int)Mathf.Abs(velocity.x * 0.75f);
                            m.FrameCounter++;
                            if (m.FrameCounter > 6)
                            {
                                m.Frame++;
                                m.FrameCounter = 0;
                            }
                            if (m.Frame >= 7 || m.Frame < 1) m.Frame = 1;
                        }
                        else if (m.Frame > 0)
                        {
                            m.FrameCounter += 2;
                            if (m.FrameCounter > 6)
                            {
                                m.Frame++;
                                m.FrameCounter = 0;
                            }
                            if (m.Frame >= 7) m.Frame = 0;
                        }
                        else
                        {
                            m.Frame = 0;
                            m.FrameCounter = 0;
                        }
                    }
                    else if (velocity.y < 0f)
                    {
                        m.FrameCounter = 0;
                        m.Frame = 2;
                    }
                    else if (velocity.y > 0f)
                    {
                        m.FrameCounter = 0;
                        m.Frame = 4;
                    }
                    Fall(m);
                    return;
                case 353:
                    GroundFrames(m, still, 0.8f, 6, 9, 2, 1, 1);
                    Fall(m);
                    return;
                case 111:
                    GroundFrames(m, still, 0.8f, 6, 6, 0, 4, 6);
                    Fall(m);
                    return;
                case 112:
                    GroundFrames(m, still, 0.8f, 6, 2, 0, 1, 1);
                    Fall(m);
                    return;
                case 127:
                    if (velocity.y == 0f)
                    {
                        if (still)
                        {
                            m.Frame = 0;
                            m.FrameCounter = 0;
                        }
                        else if (velocity.x < -0.1f || velocity.x > 0.1f)
                        {
                            m.FrameCounter += (int)Mathf.Abs(velocity.x);
                            m.FrameCounter++;
                            if (m.FrameCounter > 6)
                            {
                                m.Frame++;
                                m.FrameCounter = 0;
                            }
                            if (m.Frame > 5) m.Frame = 0;
                        }
                        else
                        {
                            m.Frame = 0;
                            m.FrameCounter = 0;
                        }
                    }
                    else
                    {
                        m.Frame = 0;
                        m.FrameCounter = 0;
                    }
                    Fall(m);
                    return;
                case 200:
                    // The lizard: frames 6-9 while it climbs or falls, and its own slow fall (0.1 a tick to 4).
                    if (velocity.y == 0f)
                    {
                        if (still)
                        {
                            m.Frame = 0;
                            m.FrameCounter = 0;
                        }
                        else if (velocity.x < -0.1f || velocity.x > 0.1f)
                        {
                            m.FrameCounter += (int)Mathf.Abs(velocity.x);
                            m.FrameCounter++;
                            if (m.FrameCounter > 6)
                            {
                                m.Frame++;
                                m.FrameCounter = 0;
                            }
                            if (m.Frame > 5) m.Frame = 0;
                        }
                        else
                        {
                            m.Frame = 0;
                            m.FrameCounter = 0;
                        }
                    }
                    else
                    {
                        m.Rot = velocity.x * 0.1f;
                        m.FrameCounter++;
                        if (velocity.y < 0f) m.FrameCounter += 2;
                        if (m.FrameCounter > 6)
                        {
                            m.Frame++;
                            m.FrameCounter = 0;
                        }
                        if (m.Frame > 9) m.Frame = 6;
                        if (m.Frame < 6) m.Frame = 6;
                    }
                    velocity.y += 0.1f;
                    if (velocity.y > 4f) velocity.y = 4f;
                    return;
                case 208:
                    // The parrot sits (frame 0) facing the player, or flaps (1-4), and falls slowly (0.1 to 4).
                    if (velocity.y == 0f && still)
                    {
                        if (pCenter.x < m.Pos.x + m.W / 2) m.Dir = -1;
                        else if (pCenter.x > m.Pos.x + m.W / 2) m.Dir = 1;
                        m.Rot = 0f;
                        m.Frame = 0;
                    }
                    else
                    {
                        m.Rot = velocity.x * 0.075f;
                        if (++m.FrameCounter > 6)
                        {
                            m.Frame++;
                            m.FrameCounter = 0;
                        }
                        if (m.Frame > 4) m.Frame = 1;
                        if (m.Frame < 1) m.Frame = 1;
                    }
                    velocity.y += 0.1f;
                    if (velocity.y > 4f) velocity.y = 4f;
                    return;
                case 209:
                {
                    int alpha = AlphaOf(m);
                    if (alpha > 0) SetAlpha(m, alpha - 5);
                    if (velocity.y == 0f)
                    {
                        if (still)
                        {
                            m.Frame = 0;
                            m.FrameCounter = 0;
                        }
                        else if (velocity.x < -0.1f || velocity.x > 0.1f)
                        {
                            m.FrameCounter += (int)Mathf.Abs(velocity.x);
                            m.FrameCounter++;
                            if (m.FrameCounter > 6)
                            {
                                m.Frame++;
                                m.FrameCounter = 0;
                            }
                            if (m.Frame > 11) m.Frame = 2;
                            if (m.Frame < 2) m.Frame = 2;
                        }
                        else
                        {
                            m.Frame = 0;
                            m.FrameCounter = 0;
                        }
                    }
                    else
                    {
                        m.Frame = 1;
                        m.FrameCounter = 0;
                        m.Rot = 0f;
                    }
                    Fall(m);
                    return;
                }
                case 324:
                    if (velocity.y == 0f)
                    {
                        if (velocity.x < -0.1f || velocity.x > 0.1f)
                        {
                            m.FrameCounter += (int)Mathf.Abs(velocity.x);
                            m.FrameCounter++;
                            if (m.FrameCounter > 6)
                            {
                                m.Frame++;
                                m.FrameCounter = 0;
                            }
                            if (m.Frame > 5) m.Frame = 2;
                            if (m.Frame < 2) m.Frame = 2;
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
                        m.Frame = 1;
                    }
                    // The Cursed Sapling falls faster (to 14).
                    velocity.y += 0.4f;
                    if (velocity.y > 14f) velocity.y = 14f;
                    return;
                case 210:
                    if (velocity.y == 0f)
                    {
                        if (velocity.x < -0.1f || velocity.x > 0.1f)
                        {
                            m.FrameCounter += (int)Mathf.Abs(velocity.x);
                            m.FrameCounter++;
                            if (m.FrameCounter > 6)
                            {
                                m.Frame++;
                                m.FrameCounter = 0;
                            }
                            if (m.Frame > 6) m.Frame = 0;
                        }
                        else
                        {
                            m.Frame = 0;
                            m.FrameCounter = 0;
                        }
                    }
                    else
                    {
                        m.Rot = velocity.x * 0.05f;
                        if (++m.FrameCounter > 6)
                        {
                            m.Frame++;
                            m.FrameCounter = 0;
                        }
                        if (m.Frame > 11) m.Frame = 7;
                        if (m.Frame < 7) m.Frame = 7;
                    }
                    Fall(m);
                    return;
                case 398:
                    if (velocity.y == 0f)
                    {
                        if (still)
                        {
                            m.Frame = 0;
                            m.FrameCounter = 0;
                        }
                        else if (velocity.x < -0.8f || velocity.x > 0.8f)
                        {
                            m.FrameCounter += (int)Mathf.Abs(velocity.x);
                            m.FrameCounter++;
                            if (m.FrameCounter > 10)
                            {
                                m.Frame++;
                                m.FrameCounter = 0;
                            }
                            if (m.Frame > 6) m.Frame = 0;
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
                        m.Frame = 5;
                    }
                    Fall(m);
                    return;
            }
            Fall(m);
        }

        /// <summary>
        /// The walkers of P:65253-65464 (314, 319, 236, 499, 765): standing still frame 0; walking over 0.8 a tick, from
        /// `start` if below it (-1: none), a frame each time the counter (sped by the walk) passes `ticks`, back to `to`
        /// past `last`; in the air `air`. Falling at up to 0.8 a tick still counts as on the ground.
        /// </summary>
        static void StepFrames(M m, bool still, int ticks, int start, int last, int to, int air)
        {
            const float min = 0.8f;
            if (m.Vel.y >= 0f && m.Vel.y <= min)
            {
                if (still)
                {
                    m.Frame = 0;
                    m.FrameCounter = 0;
                }
                else if (m.Vel.x < -min || m.Vel.x > min)
                {
                    if (start >= 0 && m.Frame < start) m.Frame = start;
                    m.FrameCounter += (int)Mathf.Abs(m.Vel.x);
                    m.FrameCounter++;
                    if (m.FrameCounter > ticks)
                    {
                        m.Frame++;
                        m.FrameCounter = 0;
                    }
                    if (m.Frame > last) m.Frame = to;
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
                m.Frame = air;
            }
        }

        /// <summary>
        /// The walkers on solid ground only (353, 111, 112; P:65599-65728): frame 0 standing, the walk's frames from 0 up to
        /// `last` and back to `to`, rising `up`, falling `down`.
        /// </summary>
        static void GroundFrames(M m, bool still, float min, int ticks, int last, int to, int up, int down)
        {
            if (m.Vel.y == 0f)
            {
                if (still)
                {
                    m.Frame = 0;
                    m.FrameCounter = 0;
                }
                else if (m.Vel.x < -min || m.Vel.x > min)
                {
                    m.FrameCounter += (int)Mathf.Abs(m.Vel.x);
                    m.FrameCounter++;
                    if (m.FrameCounter > ticks)
                    {
                        m.Frame++;
                        m.FrameCounter = 0;
                    }
                    if (m.Frame > last) m.Frame = to;
                }
                else
                {
                    m.Frame = 0;
                    m.FrameCounter = 0;
                }
            }
            else if (m.Vel.y < 0f)
            {
                m.FrameCounter = 0;
                m.Frame = up;
            }
            else
            {
                m.FrameCounter = 0;
                m.Frame = down;
            }
        }

        /// <summary>
        /// The Shadow Mimic (860, P:63881-63951): in the air frames 0-5; resting it sits shut, and after 800 ticks opens up
        /// (frames 1-3) and spits coins at tick 820 (ShadowMimicCoins, 1218: not drawn here, a three-frame gore), shutting
        /// again at 850.
        /// </summary>
        static void ShadowMimicFrames(M m)
        {
            if (m.Vel.y != 0f)
            {
                m.LocalAi0 = 0f;
                if (m.Frame >= 5)
                {
                    m.Frame = 5;
                    m.FrameCounter = 0;
                }
                else if (++m.FrameCounter > 5)
                {
                    m.Frame++;
                    m.FrameCounter = 0;
                }
            }
            else if (Mathf.Abs(m.Vel.x) < 1f)
            {
                if (m.LocalAi0 > 800f)
                {
                    if (++m.FrameCounter > 3)
                    {
                        m.FrameCounter = 0;
                        m.Frame++;
                        if (m.Frame > 3) m.Frame = 3;
                    }
                    m.LocalAi0++;
                    if (m.LocalAi0 > 850f) m.LocalAi0 = 0f;
                }
                else if (m.Frame == 0)
                {
                    m.LocalAi0++;
                    m.Frame = 0;
                    m.FrameCounter = 0;
                }
                else
                {
                    m.LocalAi0 = 0f;
                    if (m.Frame > 5)
                    {
                        m.Frame = 5;
                        m.FrameCounter = 0;
                    }
                    if (++m.FrameCounter > 4)
                    {
                        m.Frame--;
                        m.FrameCounter = 0;
                    }
                }
            }
        }

        /// <summary>
        /// The baby red panda (825, P:64465-64577): standing, after 300 ticks it may (1 in 50 a tick) scratch (frames 1-2)
        /// or eat bamboo (3-11), resting on frame 13 after; running 13-19.
        /// </summary>
        void RedPandaFrames(M m, bool still)
        {
            if (m.Vel.y != 0f)
            {
                m.LocalAi0 = 0f;
                m.Frame = 12;
            }
            else if (still)
            {
                m.SpriteDir = -FacingPlayer(m);
                if (m.Frame >= 1 && m.Frame <= 2)
                {
                    m.LocalAi0 = 0f;
                    if (++m.FrameCounter > 5)
                    {
                        m.Frame++;
                        m.FrameCounter = 0;
                    }
                    if (m.Frame > 2) m.Frame = 0;
                }
                else if (m.Frame >= 3 && m.Frame <= 11)
                {
                    m.LocalAi0 = 0f;
                    if (++m.FrameCounter > 5)
                    {
                        m.Frame++;
                        m.FrameCounter = 0;
                    }
                    if (m.Frame > 11) m.Frame = 0;
                }
                else
                {
                    if (m.Frame == 13)
                    {
                        if (++m.FrameCounter > 8)
                        {
                            m.Frame++;
                            m.FrameCounter = 0;
                        }
                        if (m.Frame == 14) m.Frame = 0;
                    }
                    m.Frame = m.Frame != 0 ? 13 : 0;
                    if (m.Frame == 0)
                    {
                        m.LocalAi0++;
                        if (m.LocalAi0 > 300f && Random.Range(0, 50) == 0) m.Frame = Random.Range(0, 2) == 0 ? 1 : 3;
                    }
                }
            }
            else
            {
                m.LocalAi0 = 0f;
                int ticks = 3;
                float v = m.Vel.magnitude;
                if (v > 4f) ticks = 2;
                else if (v > 2f) ticks = 1;
                if (++m.FrameCounter > ticks)
                {
                    m.Frame++;
                    m.FrameCounter = 0;
                }
                if (m.Frame < 13) m.Frame = 13;
                if (m.Frame > 19) m.Frame = 14;
            }
        }

        /// <summary>
        /// The Eyeball Spring (268, P:64834-64922): crouching over 12 ticks (frames 0-3), then, if it wants to move, a spring
        /// at -6.1 with twice its speed and a puff of blood (dust 5); frame 5 rising, 4 falling.
        /// </summary>
        static void EyeSpringFrames(M m, bool still, bool flag2, bool flag3)
        {
            if (m.Vel.y == 0f)
            {
                if (m.Frame > 5) m.FrameCounter = 0;
                const int t = 3;
                if (still)
                {
                    m.FrameCounter++;
                    if (m.FrameCounter < t) m.Frame = 0;
                    else if (m.FrameCounter < t * 2) m.Frame = 1;
                    else if (m.FrameCounter < t * 3) m.Frame = 2;
                    else if (m.FrameCounter < t * 4) m.Frame = 3;
                    else m.FrameCounter = t * 4;
                }
                else
                {
                    m.Vel.x *= 0.8f;
                    m.FrameCounter++;
                    if (m.FrameCounter < t) m.Frame = 0;
                    else if (m.FrameCounter < t * 2) m.Frame = 1;
                    else if (m.FrameCounter < t * 3) m.Frame = 2;
                    else if (m.FrameCounter < t * 4) m.Frame = 3;
                    else if (flag2 | flag3)
                    {
                        m.Vel.x *= 2f;
                        m.Frame = 4;
                        m.Vel.y = -6.1f;
                        m.FrameCounter = 0;
                        var tp = TerraParticles.Instance;
                        for (int k = 0; k < 4 && tp != null; k++)
                        {
                            int d = tp.NewDust(new Vector2(m.Pos.x, m.Pos.y + m.H - 2f), m.W, 4, 5);
                            if (d < 0) continue;
                            tp[d].Velocity += m.Vel;
                            tp[d].Velocity *= 0.4f;
                        }
                    }
                    else m.FrameCounter = t * 4;
                }
            }
            else if (m.Vel.y < 0f)
            {
                m.FrameCounter = 0;
                m.Frame = 5;
            }
            else
            {
                m.Frame = 4;
                m.FrameCounter = 3;
            }
        }

        /// <summary>
        /// The pet spider (313, P:64967-65091): on a background wall (two of the three cells down its middle) it crawls
        /// over it without falling, 4 px a tick at the player, turned along its way (frames 5-10, a 34 px box); else it
        /// walks (frames 0-3, 4 in the air, a 30 px box).
        /// </summary>
        void SpiderPetFrames(M m, bool still)
        {
            ref var velocity = ref m.Vel;
            int ci = TileOf(m.Center.x), cj = TileOf(m.Center.y);
            int walls = (WallTile(ci, cj) ? 1 : 0) + (WallTile(ci, cj - 1) ? 1 : 0) + (WallTile(ci, cj + 1) ? 1 : 0);
            if (walls > 1)
            {
                Resize(m, 34, 34);
                float dx = pCenter.x - m.Center.x, dy = pCenter.y - m.Center.y;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float k = 4f / dist;
                dx *= k;
                dy *= k;
                if (dist < 120f)
                {
                    velocity *= 0.9f;
                    if (Mathf.Abs(velocity.x) + Mathf.Abs(velocity.y) < 0.1f) velocity *= 0f;
                }
                else
                {
                    velocity.x = (velocity.x * 9f + dx) / 10f;
                    velocity.y = (velocity.y * 9f + dy) / 10f;
                }
                if (dist >= 120f)
                {
                    m.SpriteDir = m.Dir;
                    m.Rot = Mathf.Atan2(velocity.y * -m.Dir, velocity.x * -m.Dir);
                }
                m.FrameCounter += (int)(Mathf.Abs(velocity.x) + Mathf.Abs(velocity.y));
                if (m.FrameCounter > 6)
                {
                    m.Frame++;
                    m.FrameCounter = 0;
                }
                if (m.Frame > 10) m.Frame = 5;
                if (m.Frame < 5) m.Frame = 10;
                return;
            }
            m.Rot = 0f;
            if (m.Dir == -1) m.SpriteDir = 1;
            if (m.Dir == 1) m.SpriteDir = -1;
            Resize(m, 30, 30);
            if (velocity.y >= 0f && velocity.y <= 0.8f)
            {
                if (still)
                {
                    m.Frame = 0;
                    m.FrameCounter = 0;
                }
                else if (velocity.x < -0.8f || velocity.x > 0.8f)
                {
                    m.FrameCounter += (int)Mathf.Abs(velocity.x);
                    m.FrameCounter++;
                    if (m.FrameCounter > 6)
                    {
                        m.Frame++;
                        m.FrameCounter = 0;
                    }
                    if (m.Frame > 3) m.Frame = 0;
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
                m.Frame = 4;
            }
            Fall(m);
        }

        /// <summary>
        /// Chester (960, P:63714-63829): in the air frame 4; standing, after a walk it finishes its steps (5-11); while its
        /// Piggy Bank is open it opens (13) and pants with its lid up (14-18), and on closing shuts (19); otherwise it idles
        /// (0-3, six ticks each). Walking 5-12, at least 2 a tick on the counter.
        /// </summary>
        void ChesterFrames(M m, bool still)
        {
            if (m.Vel.y != 0f)
            {
                m.LocalAi0 = 0f;
                m.LocalAi1 = 0f;
                m.FrameCounter = 0;
                m.Frame = 4;
            }
            else if (still)
            {
                m.SpriteDir = FacingPlayer(m);
                if (m.Frame >= 5 && m.Frame < 12)
                {
                    if (++m.FrameCounter > 3)
                    {
                        m.Frame++;
                        m.FrameCounter = 0;
                    }
                    if (m.Frame >= 12) m.Frame = 0;
                }
                else if (ChesterTracked(m))
                {
                    if (m.LocalAi0 == 0f)
                    {
                        if (m.LocalAi1 == 0f)
                        {
                            m.LocalAi1 = 1f;
                            m.FrameCounter = 0;
                        }
                        m.Frame = 13;
                        if (++m.FrameCounter > 6)
                        {
                            m.LocalAi0 = 1f;
                            m.Frame = 14;
                            m.FrameCounter = 0;
                        }
                    }
                    else if (++m.FrameCounter > 6)
                    {
                        m.Frame++;
                        if (m.Frame > 18) m.Frame = 14;
                        m.FrameCounter = 0;
                    }
                }
                else
                {
                    m.LocalAi0 = 0f;
                    if (m.LocalAi1 == 1f)
                    {
                        m.LocalAi1 = 0f;
                        m.FrameCounter = 0;
                    }
                    if (m.Frame >= 12 && m.Frame <= 19)
                    {
                        m.Frame = 19;
                        if (++m.FrameCounter > 6)
                        {
                            m.Frame = 0;
                            m.FrameCounter = 0;
                        }
                    }
                    else
                    {
                        if (++m.FrameCounter >= 24) m.FrameCounter = 0;
                        m.Frame = m.FrameCounter / 6;
                    }
                }
            }
            else
            {
                m.LocalAi0 = 0f;
                m.LocalAi1 = 0f;
                m.FrameCounter += (int)Mathf.Max(2f, m.Vel.magnitude);
                if (m.FrameCounter > 6)
                {
                    m.Frame++;
                    m.FrameCounter = 0;
                }
                if (m.Frame < 5 || m.Frame > 12) m.Frame = 5;
            }
        }
    }
}
