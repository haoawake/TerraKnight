using TerraKnight.Effects;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The walking minions (spec-minions-ground): AI_026 for the Baby Slime and the spiders,
    /// AI_067_FreakingPirates for the pirates, the Vampire Frog and the Flinx. Terraria px, y down.
    /// </summary>
    internal sealed partial class Minions
    {
        // ------------------------------------------------------------------ AI_026: Baby Slime, spiders

        void AI026(M m)
        {
            bool slime = m.Kind == Kind.BabySlime, spider = IsSpider(m.Kind), pygmy = IsPygmy(m.Kind);
            if (PlayerBuffs.Flag(m.S.Buff) && !ownerDead) m.TimeLeft = 2;
            m.HasTarget = false;
            m.Latched = false;
            // The idle wish: a spot 40 px per minion behind the player.
            float goalX = pCenter.x - 40f * (m.MinionPos + 1) * pDir;
            bool left = m.Center.x > goalX + 10f, right = m.Center.x < goalX - 10f;
            // Pets fall through platforms while the player is more than 12 px lower.
            m.FallThrough = pBox.yMax - 12f > m.Bottom;

            // Leash and teleport.
            float reach = 500f + 40f * m.MinionPos;
            if (m.LocalAi0 > 0f)
            {
                reach += 500f;
                if (slime) reach += 100f;
                if (spider) reach += 400f;
            }
            var d = pCenter - m.Center;
            float dist = d.magnitude;
            if (dist > 2000f) m.Center = pCenter;
            else if (dist > reach || (Mathf.Abs(d.y) > 300f && m.LocalAi0 <= 0f))
            {
                if (d.y > 0f && m.Vel.y < 0f) m.Vel.y = 0f;
                if (d.y < 0f && m.Vel.y > 0f) m.Vel.y = 0f;
                m.Ai0 = 1f;
            }

            if (m.Ai0 == 1f)
            {
                FlyBack026(m, slime, pygmy);
                return;
            }

            // --- On the ground: targeting (Manhattan from the minion, the nearest seen one preferred).
            m.LocalAi0 = Mathf.Max(0f, m.LocalAi0 - 1f);
            if (pygmy) PygmyThink(m, ref left, ref right);
            else
            {
                float bestVis = 100000f, bestAny = 100000f;
                Npc target = null;
                Vector2 aim = m.Pos;
                var owner = OwnerTarget;
                if (owner != null)
                {
                    float dT = Manhattan(m.Center, owner.Center);
                    if (dT < bestVis)
                    {
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
                }
                if (target == null)
                    foreach (var n in npcs)
                    {
                        float dN = Manhattan(m.Center, n.Center);
                        if (dN >= bestVis || !CanReach(m, n)) continue;
                        if (target == null && dN <= bestAny)
                        {
                            bestAny = dN;
                            aim = n.Center;
                        }
                        if (ClearLine(m.Center, n.Center))
                        {
                            bestVis = dN;
                            aim = n.Center;
                            target = n;
                        }
                    }
                if (spider && !SolidBox(m.Box)) m.TileCollide = true;
                if (target == null && bestAny < bestVis) bestVis = bestAny;
                else if (target != null)
                {
                    m.HasTarget = true;
                    m.ToTarget = aim - m.Center;
                    if (spider) Latch(m, target);
                }
                float sense = slime ? 300f : 500f;
                if (World.Areas.LevelOf(GameManager.UnsafeInstance?.sceneName) >= 1) sense = slime ? 150f : 250f;
                if (target == null && bestVis < sense + 40f * m.MinionPos)
                {
                    float dx = aim.x - m.Center.x;
                    if (dx < -5f) { left = true; right = false; }
                    else if (dx > 5f) { right = true; left = false; }
                }
                bool cooling = spider && m.LocalAi1 > 0f;
                if (cooling) m.LocalAi1--;
                if (target != null && bestVis < 800f + 40f * m.MinionPos)
                {
                    m.Friendly = true;
                    m.LocalAi0 = 60f;
                    float dx = aim.x - m.Center.x;
                    if (dx < -10f) { left = true; right = false; }
                    else if (dx > 10f) { right = true; left = false; }
                    // An enemy straight above: a jump at it.
                    if (aim.y < m.Center.y - 100f && dx > -50f && dx < 50f && m.Vel.y == 0f)
                        m.Vel.y = JumpFor(Mathf.Abs(aim.y - m.Center.y), m.Vel.y);
                    if (cooling)
                    {
                        m.Friendly = false;
                        if (m.Vel.x < 0f) left = true;
                        else if (m.Vel.x > 0f) right = true;
                    }
                }
                else m.Friendly = false;
            }

            // --- Walking.
            if (!m.Latched) m.Rot = 0f;
            if (!spider) m.TileCollide = true;
            float speed = 6f, accel = 0.2f;
            float ps = Mathf.Abs(pVel.x) + Mathf.Abs(pVel.y);
            if (ps > 6f)
            {
                speed = ps;
                accel = 0.3f;
            }
            if (spider || pygmy) accel *= 2f;
            if (left) m.Vel.x -= m.Vel.x > -3.5f ? accel : accel * 0.25f;
            else if (right) m.Vel.x += m.Vel.x < 3.5f ? accel : accel * 0.25f;
            else
            {
                m.Vel.x *= 0.9f;
                if (Mathf.Abs(m.Vel.x) <= accel) m.Vel.x = 0f;
            }
            bool wallAhead = false;
            int wi = TileOf(m.Center.x) + (left ? -1 : 0) + (right ? 1 : 0) + (int)m.Vel.x, wj = TileOf(m.Center.y);
            if (left || right) wallAhead = SolidTile(wi, wj);
            StepUp(m, m.FallThrough);
            if (m.Vel.y == 0f)
            {
                int fi = TileOf(m.Center.x), fj = TileOf(m.Bottom);
                if (wallAhead && (SolidTile(fi, fj) || PlatformTile(fi, fj))) m.Vel.y = WallJump(wi, wj);
                // The Baby Slime hops whenever it moves.
                else if (slime && (left || right)) m.Vel.y -= 6f;
            }
            m.Vel.x = Mathf.Clamp(m.Vel.x, -speed, speed);
            if (m.Vel.x < 0f) m.Dir = -1;
            if (m.Vel.x > 0f) m.Dir = 1;
            if (m.Vel.x > accel && right) m.Dir = 1;
            if (m.Vel.x < -accel && left) m.Dir = -1;
            m.SpriteDir = -m.Dir;
            bool stoodStill = m.Pos.x - m.OldPos.x == 0f;

            if (pygmy)
            {
                PygmyFrames(m, stoodStill);
                m.Vel.y = Mathf.Min(m.Vel.y + 0.4f, 10f);
                return;
            }
            if (slime)
            {
                if (m.Vel.y != 0f)
                {
                    if (++m.FrameCounter > 4)
                    {
                        m.FrameCounter = 0;
                        m.Frame++;
                    }
                    if (m.Frame == 0 || m.Frame >= 8) m.Frame = 1;
                }
                else
                {
                    if (stoodStill) m.SpriteDir = pCenter.x < m.Center.x ? 1 : -1;
                    if (++m.FrameCounter > 7)
                    {
                        m.FrameCounter = 0;
                        m.Frame++;
                        if (m.Frame == 1) m.Frame = 5;
                    }
                    if ((m.Frame >= 1 && m.Frame <= 4) || m.Frame >= 8) m.Frame = 0;
                }
                m.Vel.y = Mathf.Min(m.Vel.y + 0.4f, 10f);
                return;
            }

            // Spiders: three walled cells round it and it crawls freely, without gravity.
            int ci = TileOf(m.Center.x), cj = TileOf(m.Center.y);
            int walls = (WallTile(ci, cj) ? 1 : 0) + (WallTile(ci, cj - 1) ? 1 : 0) + (WallTile(ci, cj + 1) ? 1 : 0);
            if (walls > 1)
            {
                float near = 40f * (m.MinionPos + 1);
                var v = pCenter - m.Center;
                if (m.HasTarget)
                {
                    v = m.ToTarget;
                    near = 10f;
                }
                if (v.magnitude < near)
                {
                    m.Vel *= 0.9f;
                    if (Mathf.Abs(m.Vel.x) + Mathf.Abs(m.Vel.y) < 0.1f) m.Vel = Vector2.zero;
                }
                else if (v.magnitude < 800f || !m.HasTarget) m.Vel = (m.Vel * 9f + v.normalized * 9f) / 10f;
                if (v.magnitude >= near)
                {
                    m.SpriteDir = m.Dir;
                    m.Rot = Mathf.Atan2(m.Vel.y, m.Vel.x) + Mathf.PI / 2f;
                }
                else m.Rot = Mathf.Atan2(v.y, v.x) + Mathf.PI / 2f;
                m.FrameCounter += (int)(Mathf.Abs(m.Vel.x) + Mathf.Abs(m.Vel.y));
                if (m.FrameCounter > 8)
                {
                    m.Frame++;
                    m.FrameCounter = 0;
                }
                if (m.Frame < 4 || m.Frame > 7) m.Frame = 4;
                return;
            }
            if (!m.Latched) m.Rot = 0f;
            m.SpriteDir = -m.Dir;
            if (!m.Latched && m.Frame >= 4 && m.Frame <= 7)
            {
                // Just off a wall: a leap toward the prey or the player.
                var v = m.HasTarget ? m.ToTarget : pCenter - m.Center;
                if (v.y <= 0f) m.Vel.y = JumpFor(-v.y, m.Vel.y);
            }
            if (m.Latched)
            {
                if (++m.FrameCounter > 4)
                {
                    m.Frame++;
                    m.FrameCounter = 0;
                }
                if (m.Frame < 4 || m.Frame > 7) m.Frame = 4;
            }
            else if (m.Vel.y >= 0f && m.Vel.y <= 0.8f)
            {
                if (stoodStill) m.Frame = 0;
                else if (Mathf.Abs(m.Vel.x) > 0.8f)
                {
                    m.FrameCounter += (int)Mathf.Abs(m.Vel.x) + 1;
                    if (m.FrameCounter > 8)
                    {
                        m.Frame++;
                        m.FrameCounter = 0;
                    }
                    if (m.Frame > 2) m.Frame = 0;
                }
                else m.Frame = 0;
            }
            else m.Frame = 3;
            m.Vel.y = Mathf.Min(m.Vel.y + 0.4f, 10f);
        }

        /// <summary>
        /// AI_026's pygmy branch (P:62567): every 30 ticks a spear (195) at the nearest enemy in
        /// sight within 800 px (+40 per minion), aimed where its velocity takes it in 20 ticks,
        /// thrown from 8 px over its middle at 11 px with a little scatter and a lob; more than
        /// 450 px across and it walks closer instead. Nothing in sight: it walks to the nearest
        /// enemy within 400 px (200 underground). While the throw cools it stands; with nothing
        /// seen for a second it faces the way the player does.
        /// </summary>
        void PygmyThink(M m, ref bool left, ref bool right)
        {
            float extra = 40f * m.MinionPos;
            bool ready = m.Ai1 <= 0f;
            if (m.Ai1 > 0f) m.Ai1--;
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
                    m.HasTarget = true;
                    m.ToTarget = aim - m.Center;
                    float dx = aim.x - m.Center.x;
                    if (dx > 450f || dx < -450f)
                    {
                        if (dx < -50f) { left = true; right = false; }
                        else if (dx > 50f) { right = true; left = false; }
                    }
                    else
                    {
                        m.Ai1 = 30f;
                        var from = m.Center - new Vector2(0f, 8f);
                        float vx = aim.x - from.x + Random.Range(-20, 21);
                        float lob = Mathf.Abs(vx) * 0.1f * Random.Range(0, 100) * 0.001f;
                        float vy = aim.y - from.y + Random.Range(-20, 21) - lob;
                        var v = new Vector2(vx, vy);
                        v = v.sqrMagnitude > 0f ? v.normalized * 11f : new Vector2(11f * m.Dir, 0f);
                        TerraProjectiles.Instance?.ShootMinion(ProjKind.PygmySpear, TerraParticles.ToWorld(from), new Vector2(v.x, -v.y), m.Damage, m.Knockback);
                        if (v.x < 0f) m.Dir = -1;
                        if (v.x > 0f) m.Dir = 1;
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
        /// P:64661: throwing, frames 1-3 (4 ticks each) then back to 0; on the ground, 0 standing
        /// and 5-10 running (faster the faster it goes); in the air, 4.
        /// </summary>
        static void PygmyFrames(M m, bool stoodStill)
        {
            if (m.Ai1 > 0f)
            {
                if (m.LocalAi1 == 0f)
                {
                    m.LocalAi1 = 1f;
                    m.Frame = 1;
                }
                if (m.Frame != 0)
                {
                    if (++m.FrameCounter > 4)
                    {
                        m.Frame++;
                        m.FrameCounter = 0;
                    }
                    if (m.Frame >= 4) m.Frame = 0;
                }
            }
            else if (m.Vel.y == 0f)
            {
                m.LocalAi1 = 0f;
                if (stoodStill)
                {
                    m.Frame = 0;
                    m.FrameCounter = 0;
                }
                else if (Mathf.Abs(m.Vel.x) > 0.8f)
                {
                    m.FrameCounter += (int)Mathf.Abs(m.Vel.x) + 1;
                    if (m.FrameCounter > 6)
                    {
                        m.Frame++;
                        m.FrameCounter = 0;
                    }
                    if (m.Frame < 5 || m.Frame >= 11) m.Frame = 5;
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
        }

        /// <summary>AI_026's ai0 = 1: flying back through blocks to the line behind the player, landing near him.</summary>
        void FlyBack026(M m, bool slime, bool pygmy = false)
        {
            m.TileCollide = false;
            float tx = pCenter.x - m.Center.x - 40f * pDir;
            bool found = false;
            Npc visible = null;
            foreach (var n in npcs)
            {
                // P:61415: the pygmies look 100 px further for something to land and fight.
                if (Mathf.Abs(pCenter.x - n.Center.x) + Mathf.Abs(pCenter.y - n.Center.y) >= (pygmy ? 800f : 700f)) continue;
                if (ClearLine(m.Center, n.Center)) visible = n;
                found = true;
                break;
            }
            if (!found) tx -= 40f * m.MinionPos * pDir;
            if (found && visible != null) m.Ai0 = 0f;
            float ty = pCenter.y - m.Center.y;
            float dd = Mathf.Sqrt(tx * tx + ty * ty);
            if (dd < 200f && ownerGrounded && m.Bottom <= pBox.yMax && !SolidBox(m.Box))
            {
                m.Ai0 = 0f;
                if (m.Vel.y < -6f) m.Vel.y = -6f;
            }
            // P:61478: the pygmies fly back at 12 (or the player's speed), turning harder (0.8).
            float back = pygmy ? Mathf.Max(12f, Mathf.Abs(pVel.x) + Mathf.Abs(pVel.y)) : 10f;
            Vector2 g = dd < 60f ? m.Vel : new Vector2(tx, ty) * (back / dd);
            Steer(ref m.Vel, g, pygmy ? 0.8f : 0.2f);
            if (m.Vel.x > 0.5f) m.SpriteDir = -1;
            else if (m.Vel.x < -0.5f) m.SpriteDir = 1;
            if (pygmy)
            {
                // P:61624, 62467: flying back head first, in one of the frames 12-17.
                if (m.Frame < 12) m.Frame = Random.Range(12, 18);
                m.Rot = Mathf.Atan2(m.Vel.y, m.Vel.x) + (m.SpriteDir == -1 ? 0f : 3.14f);
                return;
            }
            if (slime)
            {
                if (++m.FrameCounter > 5)
                {
                    m.FrameCounter = 0;
                    m.Frame++;
                }
                if (m.Frame < 8 || m.Frame > 11) m.Frame = 8;
                m.Rot = m.Vel.x * 0.1f;
                return;
            }
            if (WallTile(TileOf(m.Center.x), TileOf(m.Center.y)))
            {
                m.Rot = Mathf.Atan2(m.Vel.y, m.Vel.x) + Mathf.PI / 2f;
                m.FrameCounter += (int)(Mathf.Abs(m.Vel.x) + Mathf.Abs(m.Vel.y));
                if (m.FrameCounter > 5)
                {
                    m.FrameCounter = 0;
                    m.Frame++;
                }
                if (m.Frame < 4 || m.Frame > 7) m.Frame = 4;
            }
            else
            {
                if (++m.FrameCounter > 2)
                {
                    m.FrameCounter = 0;
                    m.Frame++;
                }
                if (m.Frame < 8 || m.Frame > 10) m.Frame = 8;
                m.Rot = m.Vel.x * 0.1f;
            }
        }

        /// <summary>The spider's latch: hugging an enemy it touches, homing in and turning its head to it.</summary>
        void Latch(M m, Npc target)
        {
            // Prey below it: it drops through the platform it stands on.
            if (target.Box.yMin > m.Bottom && PlatformTile(TileOf(m.Center.x), TileOf(m.Bottom + 1f))) m.TileCollide = false;
            var box = new Rect(target.Box.x - 10f, target.Box.y - 10f, target.Box.width + 20f, target.Box.height + 20f);
            if (!m.Box.Overlaps(box)) return;
            m.Latched = true;
            var v = target.Center - m.Center;
            if ((m.Vel.y > 0f && v.y < 0f) || (m.Vel.y < 0f && v.y > 0f)) m.Vel.y *= 0.5f;
            if ((m.Vel.x > 0f && v.x < 0f) || (m.Vel.x < 0f && v.x > 0f)) m.Vel.x *= 0.5f;
            if (v.magnitude > 14f) v = v.normalized * 14f;
            m.Rot = (m.Rot * 5f + Mathf.Atan2(v.y, v.x) + Mathf.PI / 2f) / 6f;
            m.Vel = (m.Vel * 9f + v) / 10f;
            foreach (var o in live)
            {
                if (o == m || o.Dead || !IsSpider(o.Kind) || Vector2.Distance(o.Center, m.Center) >= 15f) continue;
                const float k = 0.5f;
                if (m.Center.y > o.Center.y) { o.Vel.y -= k; m.Vel.y += k; }
                else { o.Vel.y += k; m.Vel.y -= k; }
                // (vanilla pushes the other spider's y, not its x, in the second branch)
                if (m.Center.x > o.Center.x) { m.Vel.x += k; o.Vel.x -= k; }
                else { m.Vel.x -= k; o.Vel.y += k; }
            }
        }

        // ------------------------------------------------------------------ AI_067: pirates, Vampire Frog, Flinx

        void AI067(M m)
        {
            bool pirate = IsPirate(m.Kind), frog = m.Kind == Kind.VampireFrog, flinx = m.Kind == Kind.Flinx, cob = m.Kind == Kind.CobSpider;
            if (PlayerBuffs.Flag(m.S.Buff) && !ownerDead) m.TimeLeft = 2;
            // P:50028: the Cob Whip's spider lives while the whip's buff does.
            if (cob && PlayerBuffs.Has(PlayerBuffs.CobWhip) && !ownerDead) m.TimeLeft = 2;
            var idle = pCenter;
            if (flinx) idle.x -= (45f + 10f) * pDir + 30f * m.MinionPos * pDir;
            if (pirate) idle.x -= (15f + 10f) * pDir + 20f * m.MinionPos * pDir;
            if (frog) idle.x -= (35f + 10f) * pDir + 40f * m.MinionPos * pDir;
            m.FallThrough = pBox.yMax - 12f > m.Bottom;
            m.Friendly = false;
            int cooldown = 0, attackLen = 15;
            bool canStrike = true;
            if (flinx || cob)
            {
                canStrike = false;
                m.Friendly = true;
            }
            if (frog)
            {
                m.Friendly = true;
                attackLen = 20;
                cooldown = 60;
            }
            // The pirate's idle puff, every tick in every state.
            if (pirate)
            {
                m.LocalAi0++;
                if (m.Vel.x == 0f) m.LocalAi0++;
                if (m.LocalAi0 >= Random.Range(900, 1200))
                {
                    m.LocalAi0 = 0f;
                    Puff(m, 6, true);
                }
            }
            Npc target = null;
            // The spider looks 450 px round and goes for the nearest enemy it can reach, not the owner's target.
            if (m.Ai0 == 0f || (pirate && m.Ai0 == 5f)) target = cob ? FindTargetInRange(m, 450f, true, false) : FindTargetInRange(m, 800f, true);

            if (m.Ai0 == 1f)
            {
                // Flying back through blocks (the parrot carries a pirate; the frog tumbles; the Flinx spins).
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
                if (dist < 60f) { }
                else Steer(ref m.Vel, d / dist * speed, 0.2f);
                if (m.Vel.x != 0f) m.SpriteDir = m.Vel.x > 0f ? 1 : -1;
                if (flinx)
                {
                    if (++m.FrameCounter > 3)
                    {
                        m.FrameCounter = 0;
                        m.Frame++;
                    }
                    if (m.Frame < 2 || m.Frame > 11) m.Frame = 2;
                    m.Rot += 0.25f * m.SpriteDir;
                }
                else if (pirate)
                {
                    if (++m.FrameCounter > 3)
                    {
                        m.FrameCounter = 0;
                        m.Frame++;
                    }
                    if (m.Frame < 10 || m.Frame > 13) m.Frame = 10;
                    m.Rot = m.Vel.x * 0.1f;
                }
                else
                {
                    m.FrameCounter = (m.FrameCounter + 1) % 12;
                    m.Frame = 14 + m.FrameCounter / 3;
                    m.Rot = m.Vel.x * 0.15f;
                }
                return;
            }

            if (m.Ai0 == 2f)
            {
                if (m.Ai1 < 0f)
                {
                    // (vanilla: the frog's cooldown always ends at once)
                    m.Friendly = false;
                    m.Ai1++;
                    if (attackLen >= 0)
                    {
                        m.Ai1 = 0f;
                        m.Ai0 = 0f;
                        return;
                    }
                }
                else
                {
                    m.SpriteDir = m.Dir;
                    m.Rot = 0f;
                    if (pirate)
                    {
                        m.Friendly = true;
                        m.Frame = 4 + (int)(15f - m.Ai1) / 5;
                        if (m.Vel.y != 0f) m.Frame += 3;
                    }
                    if (frog)
                    {
                        float t = (20f - m.Ai1) / 20f;
                        int n = (int)(t * 5f);
                        if (n > 2) n = 4 - n;
                        m.Frame = (m.Vel.y != 0f ? 21 : 18) + n;
                        if (m.Vel.y == 0f) m.Vel.x *= 0.8f;
                    }
                    m.Vel.y = Mathf.Min(m.Vel.y + 0.4f, 10f);
                    m.Ai1--;
                    if (m.Ai1 <= 0f)
                    {
                        if (cooldown <= 0)
                        {
                            m.Ai1 = 0f;
                            m.Ai0 = 0f;
                            return;
                        }
                        m.Ai1 = -cooldown;
                    }
                }
                return;
            }

            if (m.Ai0 == 5f)
            {
                // The pirate's grappling hook: straight at a point on the enemy at 16 px a tick, slashing on arrival.
                if (target == null)
                {
                    m.Ai0 = 0f;
                    m.Ai1 = 0f;
                    m.LocalAi0 = 0f;
                    return;
                }
                float r = Mathf.Min(target.Box.width, target.Box.height) / 2f * 0.5f;
                float th = m.MinionPos * 2f * Mathf.PI / 0.5862069f;
                var spot = target.Center + new Vector2(Mathf.Cos(th), Mathf.Sin(th)) * r;
                m.Vel = Vector2.ClampMagnitude(spot - m.Center, 16f);
                m.LocalAi0 = (spot - m.Center).magnitude / 16f;
                m.Dir = m.SpriteDir = m.Vel.x >= 0f ? 1 : -1;
                if (m.LocalAi0 < 2f)
                {
                    m.Ai0 = 2f;
                    m.Ai1 = 15f;
                    m.Vel *= 0.3f;
                    m.Vel += Random.insideUnitCircle.normalized * 4f;
                    m.Vel.y = -3f;
                    m.Center = spot;
                    return;
                }
                if (++m.Ai1 >= 180f)
                {
                    m.Ai0 = 0f;
                    m.Ai1 = 0f;
                }
                return;
            }

            var goal = idle;
            if (target != null)
            {
                float reachStrike = pirate ? 20f : frog ? 50f : 20f;
                var tc = target.Center;
                goal = tc;
                if (InRangeOfMeOrOwner(m, target, 800f, out _, out _))
                {
                    // TryJumpingToTarget: drop through toward a lower enemy, jump to a higher one.
                    m.FallThrough = tc.y > m.Bottom;
                    if (tc.y < m.Center.y - 30f && m.Vel.y == 0f) m.Vel.y = -Mathf.Min(Mathf.Sqrt((m.Center.y - tc.y) * 2f * 0.4f), 26f);
                    if (pirate && m.Ai1 == 0f)
                    {
                        if (m.Vel.y > 2f && tc.y - m.Bottom < -250f)
                        {
                            if (Random.value < 0.5f)
                            {
                                m.Ai0 = 5f;
                                m.Ai1 = 0f;
                                return;
                            }
                            // A double jump, off a puff of cloud or smoke.
                            m.Vel.y = -Mathf.Min(Mathf.Sqrt((m.Center.y - tc.y) * 2f * 0.4f), 26f);
                            m.Vel.x += Random.Range(-1f, 1f) * Random.value * 5f;
                            if (Random.Range(0, 4) == 0) Puff(m, 14, false);
                            else CloudPuff(m);
                        }
                        if ((tc - m.Center).magnitude > 600f && target.Boss)
                        {
                            m.Ai0 = 5f;
                            m.Ai1 = 0f;
                            return;
                        }
                    }
                    if (canStrike && (m.Center - tc).magnitude < reachStrike && m.Ai0 != 2f)
                    {
                        bool brake = !(pirate && m.Vel.y != 0f);
                        if (brake && m.Vel.magnitude > 10f) m.Vel = m.Vel.normalized * 10f;
                        m.Ai0 = 2f;
                        m.Ai1 = attackLen;
                        m.Dir = tc.x > m.Center.x ? 1 : -1;
                    }
                }
                // The frog stops 20 px short, on its own side.
                if (frog) goal.x += 20f * (tc.x < m.Center.x ? 1f : -1f);
            }

            // Leash when idle (P:51172: never for the spider, which always walks).
            if (m.Ai0 == 0f && target == null && !cob)
            {
                var d = pCenter - m.Center;
                if (d.magnitude > 2000f) m.Center = pCenter;
                else if (d.magnitude > 500f || Mathf.Abs(d.y) > 300f)
                {
                    if ((m.Vel.y > 0f && d.y < 0f) || (m.Vel.y < 0f && d.y > 0f)) m.Vel.y = 0f;
                    m.Ai0 = 1f;
                    return;
                }
            }
            if (m.Ai0 != 0f && m.Ai0 != 2f) return;

            if (target == null) goal = IdleGoal(m, idle);
            Walk067(m, goal, target, pirate, frog, flinx);
        }

        /// <summary>AI_067's idle goal: the spot behind the player, dropped onto the floor and kept in reach of him.</summary>
        Vector2 IdleGoal(M m, Vector2 idle)
        {
            var goal = idle;
            if (Vector2.Distance(m.Center, pCenter) > 60f && Vector2.Distance(m.Center, idle) > 60f
                && Mathf.Sign(idle.x - pCenter.x) != Mathf.Sign(m.Center.x - pCenter.x)) goal = pCenter;
            var r = new Rect(goal.x - m.W / 2f, goal.y - m.H / 2f, m.W, m.H);
            for (int k = 0; k < 20; k++)
            {
                if (SolidBox(r)) break;
                r.y += 16f;
                goal.y += 16f;
            }
            var start = pCenter - new Vector2(m.W, m.H) / 2f;
            goal = pCenter + Sweep(start, goal - pCenter, m.W, m.H);
            if (Vector2.Distance(m.Center, goal) < 32f && (pCenter - m.Center).magnitude < (pCenter - goal).magnitude) goal = m.Center;
            if ((pCenter - goal).magnitude > 500f || Mathf.Abs(pCenter.y - goal.y) > 300f)
            {
                var last = pCenter;
                for (int k = 1; k <= 20; k++)
                {
                    var c = Vector2.Lerp(pCenter, goal, k * 0.05f);
                    if (SolidBox(c.x - m.W / 2f, c.y - m.H / 2f, m.W, m.H)) break;
                    last = c;
                }
                goal = last;
            }
            return goal;
        }

        /// <summary>Collision.TileCollision on a moved box: how much of the move it gets before blocks stop it.</summary>
        static Vector2 Sweep(Vector2 topLeft, Vector2 delta, int w, int h)
        {
            var probe = new M { W = w, H = h, Pos = topLeft, Vel = delta };
            TileCollision(probe, false);
            return probe.Pos - topLeft;
        }

        void Walk067(M m, Vector2 goal, Npc target, bool pirate, bool frog, bool flinx)
        {
            m.TileCollide = true;
            float accel = 0.5f, soft = 4f, max = 4f, creep = 0.1f;
            bool tiger = IsTiger(m.Kind);
            if (target != null)
            {
                if (tiger) { accel = 1f; soft = max = 8f; }
                if (flinx) { accel = 0.65f; soft = max = 5.5f; }
                if (pirate) { accel = 1f; soft = max = 8f; }
                if (frog) { accel = 0.7f; soft = max = 6f; }
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
                // The tigers hop only at an enemy 80 px over them.
                bool wantJump = pirate ? false : frog ? target != null : flinx ? target != null && target.Box.Overlaps(m.Box) : tiger ? dGoal.y < -80f : true;
                if (wantJump) jump = true;
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
            float extraG = Mathf.Clamp01(dGoal.y / 100f) * Mathf.Clamp01((m.Vel.y + 2f) / (-6f + 2f));
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
            m.Vel.x = Mathf.Clamp(m.Vel.x, -max, max);
            // (a strike that started this tick keeps the direction it set)
            if (m.Ai0 != 2f)
            {
                m.Dir = m.Vel.x < 0f ? -1 : m.Vel.x > 0f ? 1 : (pCenter.x > m.Center.x ? 1 : -1);
                if (m.Vel.x > accel && wish == 1) m.Dir = 1;
                if (m.Vel.x < -accel && wish == -1) m.Dir = -1;
            }
            m.SpriteDir = m.Dir;

            if (tiger)
            {
                // P:52006: standing frame 0, running from frame 2 (to 7, the big ones 9), in the air 1 (the big ones 9).
                int runEnd = m.Kind == Kind.Tiger1 ? 8 : 10;
                m.Rot = 0f;
                if (m.Vel.y == 0f)
                {
                    if (m.Vel.x == 0f)
                    {
                        m.Frame = 0;
                        m.FrameCounter = 0;
                    }
                    else if (Mathf.Abs(m.Vel.x) >= 0.5f)
                    {
                        m.FrameCounter += (int)Mathf.Abs(m.Vel.x) + 1;
                        if (m.FrameCounter > 10)
                        {
                            m.Frame++;
                            m.FrameCounter = 0;
                        }
                        if (m.Frame >= runEnd || m.Frame < 2) m.Frame = 2;
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
                    m.Frame = m.Kind == Kind.Tiger1 ? 1 : 9;
                }
            }
            else if (m.Kind == Kind.CobSpider)
            {
                // P:51673: its art faces left; standing frame 0, four running frames, frame 1 in the air.
                m.Rot = 0f;
                m.SpriteDir = -m.Dir;
                if (m.Vel.y == 0f)
                {
                    if (m.Vel.x == 0f) m.Frame = 0;
                    else if (++m.FrameCounter > 4)
                    {
                        m.FrameCounter = 0;
                        if (++m.Frame > 3) m.Frame = 0;
                    }
                }
                else m.Frame = 1;
            }
            else if (flinx)
            {
                if (m.Vel.y == 0f)
                {
                    m.Rot = Mathf.MoveTowardsAngle(m.Rot * Mathf.Rad2Deg, 0f, 0.3f * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                    if (m.Vel.x == 0f) m.Frame = 0;
                    else if (Mathf.Abs(m.Vel.x) >= 0.5f)
                    {
                        m.FrameCounter += (int)Mathf.Abs(m.Vel.x) + 1;
                        if (m.FrameCounter > 10)
                        {
                            m.Frame++;
                            m.FrameCounter = 0;
                        }
                        if (m.Frame < 2 || m.Frame > 11) m.Frame = 2;
                    }
                    else m.Frame = 0;
                }
                else
                {
                    m.Rot = Mathf.Min(4f, m.Vel.y) * -0.1f;
                    m.Frame = 1;
                }
            }
            else if (pirate)
            {
                m.Rot = 0f;
                if (m.Vel.y == 0f)
                {
                    if (m.Vel.x == 0f) m.Frame = 0;
                    else if (Mathf.Abs(m.Vel.x) >= 0.5f)
                    {
                        m.FrameCounter += (int)Mathf.Abs(m.Vel.x) + 1;
                        if (m.FrameCounter > 10)
                        {
                            m.Frame++;
                            m.FrameCounter = 0;
                        }
                        if (m.Frame > 3) m.Frame = 0;
                    }
                    else m.Frame = 0;
                }
                else m.Frame = 14;
            }
            else
            {
                m.Rot = 0f;
                if (m.Vel.y == 0f)
                {
                    if (m.Vel.x == 0f)
                    {
                        // The croak: 1-2-3-4-3-2-1, then a pause till a 1-in-50 restart.
                        if (++m.FrameCounter >= 28 && Random.Range(0, 50) == 0) m.FrameCounter = 0;
                        int n = m.FrameCounter / 4;
                        if (n >= 4) n = 6 - n;
                        if (n < 0) n = 0;
                        m.Frame = 1 + n;
                    }
                    else if (Mathf.Abs(m.Vel.x) >= 0.5f)
                    {
                        m.FrameCounter = (m.FrameCounter + (int)Mathf.Abs(m.Vel.x) + 1) % 120;
                        m.Frame = 5 + m.FrameCounter / 15;
                    }
                    else m.Frame = 0;
                }
                else if (m.Vel.y < 0f)
                {
                    if (m.Frame < 5 || m.Frame > 9) m.Frame = 5;
                    else if (m.Frame < 9) m.Frame++;
                }
                else
                {
                    if (m.Frame < 9 || m.Frame > 13)
                    {
                        m.Frame = 9;
                        m.FrameCounter = 0;
                    }
                    else if (++m.FrameCounter >= 2)
                    {
                        m.FrameCounter = 0;
                        if (m.Frame < 11) m.Frame++;
                    }
                }
            }
            m.Vel.y = Mathf.Min(m.Vel.y + 0.4f + extraG, 10f);
        }

        // ------------------------------------------------------------------ shared

        static float Manhattan(Vector2 a, Vector2 b) => Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

        /// <summary>Steering per axis toward a goal velocity: accel, and 1.5 times more while still going the other way.</summary>
        static void Steer(ref Vector2 v, Vector2 goal, float accel)
        {
            if (v.x < goal.x)
            {
                v.x += accel;
                if (v.x < 0f) v.x += accel * 1.5f;
            }
            else if (v.x > goal.x)
            {
                v.x -= accel;
                if (v.x > 0f) v.x -= accel * 1.5f;
            }
            if (v.y < goal.y)
            {
                v.y += accel;
                if (v.y < 0f) v.y += accel * 1.5f;
            }
            else if (v.y > goal.y)
            {
                v.y -= accel;
                if (v.y > 0f) v.y -= accel * 1.5f;
            }
        }

        /// <summary>The jump that clears an enemy this far above (120 / 210 / 270 / 310 / 380 px), or none past that.</summary>
        static float JumpFor(float h, float vy) =>
            h < 120f ? -10f : h < 210f ? -13f : h < 270f ? -15f : h < 310f ? -17f : h < 380f ? -18f : vy;

        /// <summary>The jump up a wall ahead: by how many free cells there are above the one in front.</summary>
        static float WallJump(int i, int j) =>
            !SolidTile(i, j - 1) && !SolidTile(i, j - 2) ? -5.1f
            : !SolidTile(i, j - 2) ? -7.1f
            : SolidTile(i, j - 5) ? -11.1f
            : SolidTile(i, j - 4) ? -10.1f
            : -9.1f;

        /// <summary>The pirate's puff of smoke behind him (dust 216 and a little brown gore).</summary>
        static void Puff(M m, int count, bool idle)
        {
            var p = TerraParticles.Instance;
            if (p == null) return;
            var at = m.Center + new Vector2(-m.Dir * 8f, 0f);
            for (int i = 0; i < count; i++)
            {
                int d = p.NewDust(at - new Vector2(5f, 5f) + new Vector2(0f, 8f), 3, 6, 216, -m.Dir, 1f);
                if (d < 0) continue;
                p[d].Velocity = idle ? p[d].Velocity / 2f : p[d].Velocity * 2f;
                p[d].Scale = 0.8f;
            }
            var v = new Vector2(Random.Range(-20, 21) * 0.1f, Random.Range(-30, -10) * 0.1f) / 2f;
            v.y = Mathf.Abs(v.y);
            v.x = -Mathf.Abs(v.x) * m.Dir;
            p.NewGore(at, v, Random.Range(580, 583), 1f, 1f, default, true);
        }

        /// <summary>The pirate's double jump off a cloud: dust 16 and two cloud gores.</summary>
        static void CloudPuff(M m)
        {
            var p = TerraParticles.Instance;
            if (p == null) return;
            var at = m.Center + new Vector2(-m.Dir * 8f, 0f);
            for (int i = 0; i < 10; i++)
            {
                int d = p.NewDust(at - new Vector2(5f, 5f) + new Vector2(0f, 8f), 6, -13, 16, -m.Dir, 1f);
                if (d < 0) continue;
                p[d].Velocity.y *= -0.5f;
                p[d].Scale = 1.2f;
            }
            for (int i = 0; i < 2; i++)
            {
                var v = new Vector2(Random.Range(-20, 21) * 0.1f, -Random.Range(10, 31) * 0.1f) / 3f;
                v.x = -Mathf.Abs(v.x) * m.Dir;
                p.NewGore(at - new Vector2(16f, 16f), v, Random.Range(11, 14), 1f, 1f, default, true);
            }
        }
    }
}
