using TerraKnight.Effects;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The flying minions (spec-minions-flying): AI_062 (Hornet, Imp, Abigail), AI_164 (Abigail's
    /// counters), AI_066 (the Optic Staff's twins), AI_156 (Sanguine Bat), AI_158 (Baby Finch),
    /// AI_121 (Stardust Dragon); and the Cool Whip's snowflake (AI_177) and the Firecracker's
    /// blast (AI_178). Terraria px, y down.
    /// </summary>
    internal sealed partial class Minions
    {
        // ------------------------------------------------------------------ AI_062: Hornet, Imp, Abigail

        void AI062(M m)
        {
            bool abigail = m.Kind == Kind.Abigail, imp = m.Kind == Kind.Imp, hornet = m.Kind == Kind.Hornet;
            float num = 0f, inertia = 20f, atkLen = 40f, atkDamp = 0.69f;
            int extra = 0;
            if (PlayerBuffs.Flag(m.S.Buff) && !ownerDead) m.TimeLeft = 2;
            if (abigail)
            {
                m.OriginalDamage = HighestCounterDamage();
                extra = AbigailExtra();
                inertia = Mathf.Max(4f, 18f - 1.75f * extra);
                num = 1.4f;
                atkLen = Mathf.Max(5f, 40f - 4f * extra);
                atkDamp = Mathf.Min(1f, 0.69f + 0.03f * extra);
            }
            // Separation from the others of its type (top-left corners, Manhattan).
            foreach (var q in live)
            {
                if (q == m || q.Dead || q.Kind != m.Kind) continue;
                if (Mathf.Abs(m.Pos.x - q.Pos.x) + Mathf.Abs(m.Pos.y - q.Pos.y) >= m.W) continue;
                m.Vel.x += m.Pos.x < q.Pos.x ? -0.05f : 0.05f;
                m.Vel.y += m.Pos.y < q.Pos.y ? -0.05f : 0.05f;
            }
            // Target: the owner's, else the nearest in sight of it.
            float range = abigail ? 700f : 2000f;
            bool hasTarget = false;
            var target = Vector2.zero;
            m.TileCollide = !abigail;
            var t = OwnerTarget;
            if (t != null && Vector2.Distance(t.Center, m.Center) < range * 3f && (ClearLine(m.Center, t.Center) || (abigail && CanReach(m, t))))
            {
                target = t.Center;
                hasTarget = true;
            }
            if (!hasTarget)
                foreach (var n in npcs)
                {
                    float d = Vector2.Distance(n.Center, m.Center);
                    if (d >= range || !CanReach(m, n)) continue;
                    range = d;
                    target = n.Center;
                    hasTarget = true;
                }
            // Leash.
            float leash = abigail ? 800f : 500f;
            if (hasTarget) leash = 1000f;
            if (m.Ai0 != 1f && Vector2.Distance(pCenter, m.Center) > leash) m.Ai0 = 1f;
            if (m.Ai0 == 1f) m.TileCollide = false;
            if (abigail)
            {
                // The petal aura's timer; a cry now and then while she moves.
                if (m.Ai0 <= 1f && m.LocalAi1 <= 0f) m.LocalAi1 = -1f;
                else
                {
                    m.LocalAi1 = Mathf.Clamp01(m.LocalAi1 + 0.05f);
                    if (m.LocalAi1 >= 1f) m.LocalAi1 = -1f;
                }
                if (m.Vel.magnitude > 0.1f && Random.Range(0, 1500) == 0) Sound("Custom/abigail_cry_" + Random.Range(0, 3), 0.4f);
            }

            if (m.Ai0 >= 2f)
            {
                // Abigail's attack: she glides to a halt, hurting all round her.
                if (m.Ai0 == 2f && abigail) Sound("Custom/abigail_attack", 0.35f);
                m.Ai0++;
                if (abigail) m.LocalAi1 = m.Ai0 / atkLen;
                if (!hasTarget) m.Ai0++;
                if (m.Ai0 > atkLen)
                {
                    m.Ai0 = 0f;
                    if (hasTarget && abigail && (target - m.Center).magnitude < 50f) m.Ai0 = 2f;
                }
                m.Vel *= atkDamp;
            }
            else if (hasTarget && m.Ai0 == 0f)
            {
                var v = target - m.Center;
                float d = v.magnitude;
                var u = d > 0f ? v / d : Vector2.zero;
                if (d > 200f)
                {
                    float k = 2f * inertia;
                    m.Vel = (m.Vel * k + u * (6f + extra * num)) / (k + 1f);
                }
                else if (abigail)
                {
                    if (d < 50f)
                    {
                        m.Vel *= 0.5f;
                        m.Ai0 = 2f;
                    }
                    else m.Vel = (m.Vel * inertia + u * (4f + extra * num)) / (inertia + 1f);
                }
                else if (imp)
                {
                    // The Imp keeps its distance.
                    if (d < 150f) m.Vel = (m.Vel * 40f - u * 4f) / 41f;
                    else m.Vel *= 0.97f;
                }
                else if (m.Vel.y > -1f) m.Vel.y -= 0.1f;
            }
            else
            {
                if (m.Ai0 != 1f && !abigail && !ClearLine(m.Center, pCenter)) m.Ai0 = 1f;
                float speed = 6f;
                if (m.Ai0 == 1f) speed = 15f;
                if (abigail) speed *= 0.8f;
                var off = pCenter - m.Center + new Vector2(0f, -60f);
                if (abigail) off += new Vector2(-40f * pDir, 40f);
                if (imp)
                {
                    m.Ai1 = 80f;
                    int k = 1;
                    foreach (var q in live) if (q != m && !q.Dead && q.Kind == Kind.Imp && q.Serial < m.Serial) k++;
                    off = pCenter - m.Center;
                    off.x -= 10f * pDir + 40f * k * pDir;
                    off.y -= 10f;
                }
                float d = off.magnitude;
                if (d > 200f && speed < 9f) speed = 9f;
                if (imp) speed = (int)(speed * 0.75f);
                if (d < 100f && m.Ai0 == 1f && !SolidBox(m.Box)) m.Ai0 = 0f;
                if (d > 2000f) m.Pos = pCenter - new Vector2(m.W / 2f, m.W / 2f);
                if (imp || abigail)
                {
                    if (d > 10f)
                    {
                        var u = off / d;
                        if (d < 50f) speed /= 2f;
                        m.Vel = (m.Vel * 20f + u * speed) / 21f;
                    }
                    else
                    {
                        m.Dir = pDir;
                        m.Vel *= 0.9f;
                    }
                }
                else if (d > 70f) m.Vel = (m.Vel * 20f + off / d * speed) / 21f;
                else
                {
                    if (m.Vel == Vector2.zero) m.Vel = new Vector2(-0.15f, -0.05f);
                    m.Vel *= 1.01f;
                }
            }

            m.Rot = m.Vel.x * 0.05f;
            m.FrameCounter++;
            // Hornet and Imp face left in their textures; Abigail right.
            if (m.Vel.x > 0f) m.SpriteDir = m.Dir = -1;
            else if (m.Vel.x < 0f) m.SpriteDir = m.Dir = 1;
            if (abigail)
            {
                m.SpriteDir = m.Dir = -m.Dir;
                if (!hasTarget && Mathf.Abs(m.Vel.x) < 0.1f) m.SpriteDir = pCenter.x > m.Center.x ? 1 : -1;
            }
            if (hornet)
            {
                if (m.FrameCounter > 1)
                {
                    m.Frame++;
                    m.FrameCounter = 0;
                }
                if (m.Frame > 2) m.Frame = 0;
            }
            else if (imp)
            {
                if (m.FrameCounter >= 20) m.FrameCounter = 0;
                m.Frame = m.FrameCounter / 5;
                // Casting, a while after each fireball.
                if (m.Ai1 > 0f && m.Ai1 < 24f) m.Frame += 4;
                if (Random.Range(0, 7) == 0)
                {
                    int dd = DustBox(m, 6, Vector2.zero, 0, 2f);
                    if (dd >= 0)
                    {
                        TerraParticles.Instance[dd].Velocity *= 0.3f;
                        TerraParticles.Instance[dd].NoGravity = true;
                    }
                }
            }
            else if (m.Ai0 < 2f)
            {
                if (m.FrameCounter > 6)
                {
                    m.Frame++;
                    m.FrameCounter = 0;
                }
                if (m.Frame > 7) m.Frame = 0;
            }
            else
            {
                if (m.Frame < 8) m.FrameCounter = 0;
                m.FrameCounter++;
                int idx = m.FrameCounter / 12;
                if (idx > 6)
                {
                    m.FrameCounter = 0;
                    idx = 5;
                }
                m.Frame = idx <= 2 ? 8 + idx : idx % 2 == 1 ? 11 : 12;
                if (Random.Range(0, 2) == 0) AbigailSwirl(m);
            }

            // The shot timers.
            if (hornet && m.Ai1 > 0f)
            {
                m.Ai1 += Random.Range(1, 4);
                // The Hive Pack's strong bees shoot sooner (P:66966-66975).
                if (m.Ai1 > (Equipment.Current.StrongBees ? 70f : 90f)) m.Ai1 = 0f;
            }
            if (imp && m.Ai1 > 0f)
            {
                m.Ai1 += 1f;
                if (Random.Range(0, 3) == 0) m.Ai1 += 1f;
                if (m.Ai1 > 90f) m.Ai1 = 0f;
            }
            if (m.Ai0 != 0f || !hasTarget || abigail) return;
            if (imp)
            {
                if (target.x > m.Center.x) m.SpriteDir = m.Dir = -1;
                else if (target.x < m.Center.x) m.SpriteDir = m.Dir = 1;
            }
            if (m.Ai1 != 0f) return;
            m.Ai1 = 1f;
            var dir = (target - m.Center).normalized * (hornet ? 10f : 11f);
            TerraProjectiles.Instance?.ShootMinion(hornet ? ProjKind.HornetStinger : ProjKind.ImpFireball, TerraParticles.ToWorld(m.Center), new Vector2(dir.x, -dir.y), m.Damage, m.Knockback);
        }

        /// <summary>Abigail's attack dust: ghostly smoke swirling round her feet on a flat ellipse.</summary>
        static void AbigailSwirl(M m)
        {
            var p = TerraParticles.Instance;
            if (p == null) return;
            float a = 1.1f + Random.value * 0.3f, th = Random.value * Mathf.PI * 2f;
            var e = new Vector2(Mathf.Cos(th) * 30f * a, Mathf.Sin(th) * -12f * a);
            var at = new Vector2(m.Center.x, m.Bottom) + e;
            int d = p.NewDust(at, 0, 0, 303, 0f, 0f, 50, 1.4f + Random.value * 0.4f);
            if (d < 0) return;
            float ang = Mathf.Atan2(e.y, e.x) + Mathf.PI / 2f;
            p[d].Position = at;
            p[d].Velocity = e * 0.0125f + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            p[d].NoGravity = true;
            p[d].Color = new Color(248 / 255f, 248 / 255f, 1f, 1f);
        }

        // ------------------------------------------------------------------ AI_164: Abigail's counters

        /// <summary>An invisible slot-holder per flower use; the second and later ones chime as they come.</summary>
        void AI164(M m)
        {
            if (PlayerBuffs.Flag(m.S.Buff) && !ownerDead) m.TimeLeft = 2;
            m.LocalAi1++;
            if (m.LocalAi0 == 0f && DevCount(Kind.AbigailCounter) > 1)
            {
                m.LocalAi0 = 1f;
                Sound("Custom/abigail_upgrade_" + Random.Range(0, 3), 0.5f);
            }
            m.TileCollide = false;
            m.Vel = Vector2.zero;
            m.Center = new Vector2(pCenter.x, pBox.yMin - 40f);
        }

        int AbigailExtra() => Mathf.Max(0, DevCount(Kind.AbigailCounter) - 1);

        int HighestCounterDamage()
        {
            int best = 0;
            foreach (var m in live) if (!m.Dead && m.Kind == Kind.AbigailCounter) best = Mathf.Max(best, m.OriginalDamage);
            return best;
        }

        /// <summary>
        /// UpdateAbigailStatus (while her buff is on): no counters, no Abigail; counters but no
        /// Abigail, she appears at the player's centre with the youngest counter's knockback.
        /// </summary>
        void UpdateAbigailStatus()
        {
            if (!PlayerBuffs.Has(PlayerBuffs.AbigailMinion) || ownerDead) return;
            M youngest = null, abigail = null;
            foreach (var m in live)
            {
                if (m.Dead) continue;
                if (m.Kind == Kind.Abigail) abigail = m;
                if (m.Kind == Kind.AbigailCounter && (youngest == null || m.LocalAi1 < youngest.LocalAi1)) youngest = m;
            }
            if (youngest == null)
            {
                foreach (var m in live) if (m.Kind == Kind.Abigail) Kill(m);
                return;
            }
            if (abigail != null) return;
            var a = Spawn(Kind.Abigail, null, pCenter, 0, youngest.Knockback);
            a.OriginalDamage = HighestCounterDamage();
            a.TileCollide = false;
        }

        // ------------------------------------------------------------------ AI_066: Retanimini, Spazmamini

        void AI066(M m)
        {
            bool spaz = m.Kind == Kind.Spazmamini;
            if (PlayerBuffs.Flag(m.S.Buff) && !ownerDead) m.TimeLeft = 2;
            foreach (var q in live)
            {
                if (q == m || q.Dead || !IsTwin(q.Kind)) continue;
                if (Mathf.Abs(m.Pos.x - q.Pos.x) + Mathf.Abs(m.Pos.y - q.Pos.y) >= m.W) continue;
                m.Vel.x += m.Pos.x < q.Pos.x ? -0.05f : 0.05f;
                m.Vel.y += m.Pos.y < q.Pos.y ? -0.05f : 0.05f;
            }
            if (spaz && m.Ai0 == 2f)
            {
                // The dash: twice an update, straight on, no steering.
                m.Ai1++;
                m.Extra = 1;
                m.Rot = Mathf.Atan2(m.Vel.y, m.Vel.x) + Mathf.PI;
                if (++m.FrameCounter > 1)
                {
                    m.Frame++;
                    m.FrameCounter = 0;
                }
                if (m.Frame > 2) m.Frame = 0;
                if (m.Ai1 > 40f)
                {
                    m.Ai1 = 1f;
                    m.Ai0 = 0f;
                    m.Extra = 0;
                }
                else return;
            }
            m.TileCollide = m.Ai0 != 1f;
            if (m.TileCollide && SolidTile(TileOf(m.Center.x), TileOf(m.Center.y))) m.TileCollide = false;
            float range = 2000f;
            bool hasTarget = false;
            Vector2 target = Vector2.zero, targetVel = Vector2.zero;
            var t = OwnerTarget;
            if (t != null)
            {
                float d = Vector2.Distance(t.Center, m.Center);
                if (d < range * 3f && ClearLine(m.Center, t.Center))
                {
                    range = d;
                    target = t.Center;
                    hasTarget = true;
                }
            }
            if (!hasTarget)
                foreach (var n in npcs)
                {
                    float d = Vector2.Distance(n.Center, m.Center);
                    if (d >= range || !CanReach(m, n)) continue;
                    range = d;
                    target = n.Center;
                    targetVel = NpcVelocity(n);
                    hasTarget = true;
                }
            if (Vector2.Distance(pCenter, m.Center) > (hasTarget ? 1200f : 800f))
            {
                m.Ai0 = 1f;
                m.TileCollide = false;
            }
            if (hasTarget && m.Ai0 == 0f)
            {
                // Hold about 200 px off the target.
                var v = target - m.Center;
                var u = v.normalized;
                if (v.magnitude > 200f) m.Vel = (m.Vel * 40f + u * (spaz ? 14f : 6f)) / 41f;
                else m.Vel = (m.Vel * 40f - u * 4f) / 41f;
            }
            else
            {
                bool returning = m.Ai0 == 1f;
                float speed = returning ? 15f : 6f, inertia = 40f;
                var off = pCenter - m.Center + new Vector2(0f, -60f);
                float d = off.magnitude;
                if (d > 200f && speed < 8f) speed = 8f;
                float ps = Mathf.Abs(pVel.x) + Mathf.Abs(pVel.y);
                if (speed < ps)
                {
                    inertia = 30f;
                    speed = ps;
                    if (d > 200f)
                    {
                        inertia = 20f;
                        speed += 4f;
                    }
                    else if (d > 100f) speed += 3f;
                }
                if (returning && d > 300f)
                {
                    speed += 6f;
                    inertia -= 10f;
                }
                if (d < 150f && returning && !SolidBox(m.Box)) m.Ai0 = 0f;
                if (d > 2000f) m.Center = pCenter;
                if (d > 70f) m.Vel = (m.Vel * inertia + off.normalized * speed) / (inertia + 1f);
                else if (m.Vel == Vector2.zero) m.Vel = new Vector2(-0.15f, -0.05f);
                if (m.Vel.magnitude > speed) m.Vel *= 0.95f;
            }
            if (spaz) m.Rot = Mathf.Atan2(m.Vel.y, m.Vel.x) + Mathf.PI;
            else
            {
                var look = m.Ai0 != 1f && hasTarget ? target - m.Center : m.Vel;
                m.Rot = Mathf.Atan2(look.y, look.x) + Mathf.PI;
            }
            if (++m.FrameCounter > 3)
            {
                m.Frame++;
                m.FrameCounter = 0;
            }
            if (m.Frame > 2) m.Frame = 0;
            if (m.Ai1 > 0f) m.Ai1 += Random.Range(1, 4);
            if (!spaz && m.Ai1 > 90f) m.Ai1 = 0f;
            if (spaz && m.Ai1 > 40f) m.Ai1 = 0f;
            if (m.Ai0 != 0f) return;
            if (!spaz && hasTarget && m.Ai1 == 0f)
            {
                // Retanimini's laser (the cooldown starts even without a clear shot).
                m.Ai1++;
                if (ClearLine(m.Center, target))
                {
                    var v = (target - m.Center).normalized * 8f;
                    TerraProjectiles.Instance?.ShootMinion(ProjKind.MiniRetinaLaser, TerraParticles.ToWorld(m.Center), new Vector2(v.x, -v.y), (int)(m.Damage * 1.15f), 0f);
                }
            }
            if (spaz && m.Ai1 == 0f && hasTarget && range < 500f)
            {
                // Spazmamini's dash, aimed to meet the target.
                m.Ai1++;
                m.Ai0 = 2f;
                var v = target - m.Center;
                m.Vel = (v.sqrMagnitude > 0f ? v.normalized : m.Vel.normalized) * 8f;
                Intercept(m, target, targetVel / 2f, 8f);
            }
        }

        /// <summary>AI_066_TryInterceptingTarget: where a target moving at half its speed meets a chaser at that speed, within 35 updates.</summary>
        static void Intercept(M m, Vector2 target, Vector2 targetVel, float speed)
        {
            // |target + v·t − c| = s·t  →  (v·v − s²) t² + 2 (d·v) t + d·d = 0
            var d = target - m.Center;
            float a = Vector2.Dot(targetVel, targetVel) - speed * speed, b = 2f * Vector2.Dot(d, targetVel), c = Vector2.Dot(d, d);
            float t = -1f;
            if (Mathf.Abs(a) < 1e-5f) t = Mathf.Abs(b) > 1e-5f ? -c / b : -1f;
            else
            {
                float disc = b * b - 4f * a * c;
                if (disc >= 0f)
                {
                    float r = Mathf.Sqrt(disc), t1 = (-b - r) / (2f * a), t2 = (-b + r) / (2f * a);
                    t = t1 > 0f && (t1 < t2 || t2 <= 0f) ? t1 : t2;
                }
            }
            if (t <= 0f || t > 35f) return;
            var meet = target + targetVel * t - m.Center;
            if (meet.sqrMagnitude > 0f) m.Vel = meet.normalized * speed;
        }

        Vector2 NpcVelocity(Npc n)
        {
            var rb = n.Hm != null ? n.Hm.GetComponent<Rigidbody2D>() : null;
            return rb != null ? new Vector2(rb.linearVelocity.x, -rb.linearVelocity.y) / Px / 60f : Vector2.zero;
        }

        // ------------------------------------------------------------------ AI_156: Sanguine Bat

        void AI156(M m)
        {
            if (PlayerBuffs.Flag(m.S.Buff) && !ownerDead) m.TimeLeft = 2;
            m.TileCollide = false;
            if (++m.FrameCounter >= 6)
            {
                m.FrameCounter = 0;
                if (++m.Frame >= 4) m.Frame = 0;
            }
            m.SpriteDir = m.Vel.x != 0f ? (m.Vel.x > 0f ? 1 : -1) : pDir;
            if (Vector2.Distance(pCenter, m.Center) > 2000f)
            {
                m.Ai0 = 0f;
                m.Ai1 = 0f;
            }
            if (m.Ai0 == 0f)
            {
                // Idle on an arc over the player's head; one chance in 20 a tick to swoop.
                int index = 0, total = 0;
                foreach (var q in live)
                {
                    if (q.Dead || q.Kind != Kind.Bat) continue;
                    total++;
                    if (q.Serial < m.Serial) index++;
                }
                float th = 4.3982296f / total * (index - (total - 1) / 2f);
                var idle = pCenter + new Vector2(Mathf.Sin(th), -Mathf.Cos(th)) * 40f;
                m.Vel = Vector2.zero;
                m.Center = Vector2.Lerp(m.Center, idle, 0.42525f);
                if (Random.Range(0, 20) == 0)
                {
                    var t = BatTarget(m);
                    if (t != null)
                    {
                        m.Immune.Clear();
                        m.Ai0 = 66f;
                        m.TargetId = t.Id;
                    }
                }
                return;
            }
            if (!npcById.TryGetValue(m.TargetId, out var npc))
            {
                m.Ai0 = 0f;
                return;
            }
            m.Ai0--;
            if (m.Ai0 >= 59f)
            {
                // Seven ticks of wind-up, standing still.
                m.Vel *= 0.8f;
                if (m.Ai0 == 59f) m.Start = m.Center;
                return;
            }
            // The swoop: an ellipse from where it started, through the enemy, to the player.
            float tt = (59f - m.Ai0) / 59f;
            var a = tt >= 0.5f ? pCenter : m.Start;
            var bb = npc.Center;
            var ab = bb - a;
            float phi = Mathf.Atan2(ab.y, ab.x);
            float s = bb.x > a.x ? -Mathf.PI : Mathf.PI;
            float alpha = s * (1f - 2f * tt);
            var u = Rotate(new Vector2(Mathf.Cos(alpha), Mathf.Sin(alpha) * 0.5f * Mathf.Sin(2.3f * m.Serial)), phi);
            m.Center = (a + bb) / 2f + u * ab.magnitude / 2f;
            float w = Mathf.Repeat(phi + alpha + Mathf.PI, 2f * Mathf.PI) - Mathf.PI;
            m.Vel = new Vector2(Mathf.Cos(w), Mathf.Sin(w)) * 10f;
            m.Pos -= m.Vel;
            if (m.Ai0 == 0f)
            {
                var t2 = BatTarget(m);
                if (t2 != null)
                {
                    m.Ai0 = 66f;
                    m.TargetId = t2.Id;
                    m.Immune.Clear();
                    return;
                }
                m.Ai1 = 0f;
            }
        }

        /// <summary>AI_156_TryAttackingNPCs: the owner's target, else the enemy nearest the player, within 1000 px of him and in the bat's sight.</summary>
        Npc BatTarget(M m)
        {
            var t = OwnerTarget;
            if (t != null && Vector2.Distance(t.Center, pCenter) < 1000f && (CanHitWithOwnBody(m, t) || CanReach(m, t))) return t;
            Npc best = null;
            float bestD = 1000f;
            foreach (var n in npcs)
            {
                float d = Vector2.Distance(n.Center, pCenter);
                if (d >= bestD || !CanReach(m, n)) continue;
                bestD = d;
                best = n;
            }
            return best;
        }

        // ------------------------------------------------------------------ AI_158: Baby Finch

        void AI158(M m)
        {
            if (PlayerBuffs.Flag(m.S.Buff) && !ownerDead) m.TimeLeft = 2;
            m.FallThrough = true;
            if (++m.FrameCounter >= 6)
            {
                m.FrameCounter = 0;
                if (++m.Frame >= 4) m.Frame = 0;
            }
            var target = FindTargetInRange(m, 800f, false);
            if (target != null && Vector2.Distance(pCenter, target.Center) > 800f) target = null;
            if (target != null)
            {
                // Diving and pecking back and forth through it.
                if (!SolidBox(m.Box)) m.TileCollide = true;
                var dv = target.Center - m.Center;
                float d = dv.magnitude;
                if (m.Box.Overlaps(target.Box))
                {
                    m.TileCollide = false;
                    if (Mathf.Abs(m.Vel.x) + Mathf.Abs(m.Vel.y) < 8f) m.Vel *= 1.1f;
                    if (m.Vel.magnitude > 8f) m.Vel = m.Vel.normalized * 8f;
                }
                else if (d > 150f) m.Vel = Vector2.Lerp(m.Vel, dv / d * 6f, 0.15f);
                else
                {
                    m.TileCollide = false;
                    m.Vel += new Vector2(Mathf.Sign(dv.x), Mathf.Sign(dv.y)) * 0.35f;
                    if (m.Vel.magnitude > 8f) m.Vel = m.Vel.normalized * 8f;
                }
                Separate(m, 30f, 0.025f);
                m.Rot = m.Vel.x * 0.1f;
                m.Dir = m.SpriteDir = m.Vel.x > 0f ? 1 : -1;
                return;
            }
            m.TileCollide = false;
            int index = PerchIndex(m);
            m.LocalAi0 = index;
            var home = FinchHome(index);
            float dist = Vector2.Distance(home, m.Center);
            if (dist > 2000f) Perch(m, home);
            else if (dist > 40f)
            {
                float vmax = 6f + dist * 0.006f;
                var to = (home - m.Center) / dist;
                m.Vel = Vector2.Lerp(m.Vel, to * Mathf.Lerp(1f, 5f, Mathf.InverseLerp(40f, 800f, dist)) * vmax, 0.025f);
                if (m.Vel.magnitude > vmax) m.Vel = m.Vel.normalized * vmax;
                Separate(m, 10f, 0.05f);
                m.Rot = m.Vel.x * 0.04f;
                m.Dir = m.SpriteDir = m.Vel.x > 0f ? 1 : -1;
            }
            else if (dist > 8f + pVel.magnitude)
            {
                var dv = home - m.Center;
                m.Vel += new Vector2(Mathf.Sign(dv.x), Mathf.Sign(dv.y)) * 0.05f;
                if (m.Vel.magnitude > 6f) m.Vel = m.Vel.normalized * 6f;
                m.Rot = m.Vel.x * 0.1f;
                m.Dir = m.SpriteDir = m.Vel.x > 0f ? 1 : -1;
            }
            else Perch(m, home);
        }

        void Perch(M m, Vector2 home)
        {
            m.Center = home;
            m.Frame = 4;
            m.FrameCounter = 0;
            m.Vel = Vector2.zero;
            m.Dir = m.SpriteDir = pDir;
            m.Rot = 0f;
        }

        void Separate(M m, float within, float push)
        {
            foreach (var q in live)
            {
                if (q == m || q.Dead || q.Kind != m.Kind) continue;
                if (Mathf.Abs(m.Pos.x - q.Pos.x) + Mathf.Abs(m.Pos.y - q.Pos.y) >= within) continue;
                m.Vel.x += m.Pos.x < q.Pos.x ? -push : push;
                m.Vel.y += m.Pos.y < q.Pos.y ? -push : push;
            }
        }

        /// <summary>How many of the player's perched finches came before this one.</summary>
        int PerchIndex(M m)
        {
            int index = 0;
            foreach (var q in live) if (q != m && !q.Dead && q.Kind == Kind.Finch && q.Frame == 4 && q.Serial < m.Serial) index++;
            return index;
        }

        /// <summary>AI_158_GetHomeLocation: on top of the player's head, six to a layer, each layer 16 px higher.</summary>
        Vector2 FinchHome(int index)
        {
            int dir = pDir;
            Vector2 slot;
            switch (index % 6)
            {
                case 1: slot = new Vector2(8f * dir, -2f); break;
                case 2: slot = new Vector2(-10f * dir, -2f); break;
                case 3: slot = new Vector2(0f, -10f); break;
                case 4: slot = new Vector2(10f * dir, -10f); break;
                case 5: slot = new Vector2(-12f * dir, -10f); break;
                default: slot = Vector2.zero; break;
            }
            var home = new Vector2(pCenter.x, pBox.yMin) + new Vector2(0f, -2f) + slot + new Vector2(0f, -16f) * (index / 6);
            return new Vector2(Mathf.Floor(home.x), Mathf.Floor(home.y));
        }

        /// <summary>The Finch buff with no bird and a slot free calls one from a Finch Staff in the inventory, on the player's head.</summary>
        public void ReviveFinch()
        {
            if (DevCount(Kind.Finch) > 0 || numMinions >= Equipment.MaxMinions || !hasOwner || ownerDead) return;
            var inv = TerraMod.Instance?.Inventory;
            if (inv == null || !InInventory(inv, ItemType.FinchStaff)) return;
            var def = ItemDb.Get(ItemType.FinchStaff);
            Spawn(Kind.Finch, def, new Vector2(pCenter.x, pBox.yMin), def.Damage, Equipment.Knockback(def));
        }

        // ------------------------------------------------------------------ AI_121: Stardust Dragon

        /// <summary>
        /// The staff's own summoning: a new dragon (head, two bodies, tail) at the raw cursor, or a
        /// body pair slipped in before the tail of the one there is.
        /// </summary>
        void SummonDragon(ItemDef staff, Vector2 cursor, int dmg, float kb)
        {
            M head = null, tail = null;
            foreach (var m in live)
            {
                if (m.Dead) continue;
                if (m.Kind == Kind.DragonHead) head = m;
                if (m.Kind == Kind.DragonTail) tail = m;
            }
            if (head == null && tail == null)
            {
                var h = Dragon(Kind.DragonHead, staff, cursor, dmg, kb);
                var b1 = Dragon(Kind.DragonBody1, staff, cursor, dmg, kb);
                b1.Leader = h;
                var b2 = Dragon(Kind.DragonBody2, staff, cursor, dmg, kb);
                b2.Leader = b1;
                var t = Dragon(Kind.DragonTail, staff, cursor, dmg, kb);
                t.Leader = b2;
            }
            else if (head != null && tail != null)
            {
                var b1 = Dragon(Kind.DragonBody1, staff, pCenter, dmg, kb);
                b1.Leader = tail.Leader;
                var b2 = Dragon(Kind.DragonBody2, staff, pCenter, dmg, kb);
                b2.Leader = b1;
                tail.Leader = b2;
            }
        }

        M Dragon(Kind kind, ItemDef staff, Vector2 at, int dmg, float kb)
        {
            var m = Spawn(kind, staff, at, dmg, kb);
            m.TileCollide = false;
            m.Alpha = 1f;
            m.W = m.H = 30;
            m.Center = at;
            return m;
        }

        void AI121(M m)
        {
            if (PlayerBuffs.Flag(m.S.Buff) && !ownerDead) m.TimeLeft = 2;
            m.TileCollide = false;
            if (Random.Range(0, 30) == 0)
            {
                int d = DustBox(m, 135, Vector2.zero, 0, 2f);
                if (d >= 0)
                {
                    TerraParticles.Instance[d].NoGravity = true;
                    TerraParticles.Instance[d].FadeIn = 2f;
                }
            }
            if (m.Kind == Kind.DragonHead)
            {
                if (Vector2.Distance(m.Center, pCenter) > 2000f) m.Center = pCenter;
                // No line of sight anywhere; and the last match by order, not the nearest.
                Npc target = null;
                var t = OwnerTarget;
                if (t != null && Vector2.Distance(t.Center, m.Center) < 1400f) target = t;
                else
                    foreach (var n in npcs)
                        if (Vector2.Distance(pCenter, n.Center) < 1000f && Vector2.Distance(m.Center, n.Center) < 700f && CanReach(m, n)) target = n;
                if (target != null)
                {
                    var v = target.Center - m.Center;
                    float acc = 0.4f;
                    if (v.magnitude < 600f) acc = 0.6f;
                    if (v.magnitude < 300f) acc = 0.8f;
                    if (v.magnitude > target.Box.size.magnitude * 0.75f)
                    {
                        m.Vel += v.normalized * acc * 1.5f;
                        if (Vector2.Dot(m.Vel, v) < 0.25f) m.Vel *= 0.8f;
                    }
                    if (m.Vel.magnitude > 30f) m.Vel = m.Vel.normalized * 30f;
                }
                else
                {
                    var p = pCenter;
                    float d = (p - m.Center).magnitude, acc = 0.2f;
                    if (d < 200f) acc = 0.12f;
                    if (d < 140f) acc = 0.06f;
                    if (d > 100f)
                    {
                        if (Mathf.Abs(p.x - m.Center.x) > 20f) m.Vel.x += acc * Mathf.Sign(p.x - m.Center.x);
                        if (Mathf.Abs(p.y - m.Center.y) > 10f) m.Vel.y += acc * Mathf.Sign(p.y - m.Center.y);
                    }
                    else if (m.Vel.magnitude > 2f) m.Vel *= 0.96f;
                    if (Mathf.Abs(m.Vel.y) < 1f) m.Vel.y -= 0.1f;
                    if (m.Vel.magnitude > 15f) m.Vel = m.Vel.normalized * 15f;
                }
                m.Rot = Mathf.Atan2(m.Vel.y, m.Vel.x) + Mathf.PI / 2f;
                m.Dir = m.SpriteDir = m.Vel.x > 0f ? 1 : -1;
                Resize(m, 1f + Mathf.Clamp(m.LocalAi0, 0f, 50f) * 0.01f);
                FadeIn(m);
                return;
            }
            var leader = m.Leader;
            if (leader == null || leader.Dead)
            {
                Kill(m);
                return;
            }
            var lc = leader.Center;
            float lrot = leader.Rot, ls = Mathf.Clamp(leader.Scale, 0f, 50f);
            leader.LocalAi0 = m.LocalAi0 + 1f;
            if (m.Kind == Kind.DragonTail && leader.Kind == Kind.DragonHead)
            {
                Kill(leader);
                Kill(m);
                return;
            }
            FadeIn(m);
            m.Vel = Vector2.zero;
            var dv = lc - m.Center;
            if (lrot != m.Rot) dv = Rotate(dv, WrapAngle(lrot - m.Rot) * 0.1f);
            m.Rot = Mathf.Atan2(dv.y, dv.x) + Mathf.PI / 2f;
            Resize(m, ls);
            if (dv != Vector2.zero) m.Center = lc - dv.normalized * 16f * ls;
            m.SpriteDir = dv.x > 0f ? 1 : -1;
        }

        static void Resize(M m, float scale)
        {
            var c = m.Center;
            m.Scale = scale;
            m.W = m.H = (int)(30f * scale);
            m.Center = c;
        }

        static void FadeIn(M m)
        {
            if (m.Alpha <= 0f) return;
            for (int i = 0; i < 2; i++)
            {
                int d = DustBox(m, 135, Vector2.zero, 100, 2f);
                if (d >= 0) TerraParticles.Instance[d].NoGravity = true;
            }
            m.Alpha = Mathf.Max(0f, m.Alpha - 42f / 255f);
        }

        /// <summary>SortStardustDragonProjectiles: after the tick each link sits exactly scale·16 px behind its leader.</summary>
        void SnapDragons()
        {
            M head = null;
            foreach (var m in live) if (!m.Dead && m.Kind == Kind.DragonHead) head = m;
            if (head == null) return;
            var cur = head;
            for (int guard = 0; guard < 200; guard++)
            {
                var next = Follower(cur);
                if (next == null) break;
                var dv = cur.Center - next.Center;
                if (dv != Vector2.zero) next.Center = cur.Center - dv.normalized * 16f * cur.Scale;
                cur = next;
            }
        }

        M Follower(M leader)
        {
            foreach (var m in live) if (!m.Dead && m.Leader == leader) return m;
            return null;
        }

        M DragonHeadOf(M m)
        {
            var cur = m;
            for (int guard = 0; guard < 200 && cur != null; guard++)
            {
                if (cur.Kind == Kind.DragonHead) return cur;
                cur = cur.Leader;
            }
            return null;
        }

        /// <summary>A body piece freed: the one behind it now follows the one ahead.</summary>
        void Unlink(M m)
        {
            var f = Follower(m);
            if (f != null) f.Leader = m.Leader;
        }

        // ------------------------------------------------------------------ AI_177: the Cool Whip's snowflake

        void AI177(M m)
        {
            if (ownerDead)
            {
                Kill(m);
                return;
            }
            if (PlayerBuffs.Has(PlayerBuffs.CoolWhip)) m.TimeLeft = 2;
            m.TileCollide = false;
            Npc t = null;
            if (m.TargetId >= 0 && npcById.TryGetValue(m.TargetId, out var cur) && !m.Immune.ContainsKey(cur.Id)) t = cur;
            else
            {
                // FindTargetWithinRange(400): the nearest it isn't immune to.
                float best = 400f;
                foreach (var n in npcs)
                {
                    if (m.Immune.ContainsKey(n.Id)) continue;
                    float d = Vector2.Distance(n.Center, m.Center);
                    if (d >= best) continue;
                    best = d;
                    t = n;
                }
                m.TargetId = t != null ? t.Id : -1;
            }
            if (t != null)
            {
                var dv = t.Center - m.Center;
                float speed = Mathf.Min(18f, dv.magnitude);
                m.Vel = Vector2.Lerp(m.Vel, dv.normalized * speed, 0.1f);
            }
            else m.Vel *= 0.92f;
            m.Rot += 0.020944f + Mathf.Abs(m.Vel.x) * 0.2f;
            if (Random.Range(0, 3) == 0)
            {
                var p = TerraParticles.Instance;
                if (p == null) return;
                var r = Random.insideUnitCircle;
                int d = p.NewDust(m.Center + r * 10f, 0, 0, 43, 0f, 0f, 254, 0.5f);
                if (d < 0) return;
                p[d].Position = m.Center + r * 10f;
                p[d].Velocity = r;
            }
        }

        /// <summary>A Cool Whip strike: the snowflake if there's none yet, and its buff (180 ticks) again.</summary>
        public void CoolWhipStrike(HealthManager hm)
        {
            RefreshOwner();
            if (DevCount(Kind.Snowflake) == 0)
            {
                var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
                var at = col != null ? TerraParticles.ToPx(col.bounds.center) : pCenter;
                var m = Spawn(Kind.Snowflake, null, at, 15, 0f);
                m.Damage = 15;
                m.TileCollide = false;
            }
            PlayerBuffs.Add(PlayerBuffs.CoolWhip, 180);
        }

        // ------------------------------------------------------------------ AI_178: the Firecracker's blast

        void AI178(M m)
        {
            m.Vel = Vector2.zero;
            if (m.LocalAi0 != 0f) return;
            m.LocalAi0 = 1f;
            Sound("Item_14");
            var p = TerraParticles.Instance;
            if (p == null) return;
            var c = m.Center;
            int Put(Vector2 at, int type, int alpha, float scale)
            {
                int d = p.NewDust(at, 0, 0, type, 0f, 0f, alpha, scale);
                if (d >= 0) p[d].Position = at;
                return d;
            }
            Vector2 Disc() => c + Random.insideUnitCircle * 48f;
            Vector2 Ring() => c + Random.insideUnitCircle.normalized * 48f;
            for (int i = 0; i < 4; i++) Put(Disc(), 31, 100, 1.5f);
            for (int i = 0; i < 30; i++)
            {
                int d = Put(Disc(), 6, 200, 3.7f);
                if (d >= 0) { p[d].NoGravity = true; p[d].Velocity *= 3f; }
                d = Put(Disc(), 6, 100, 1.5f);
                if (d >= 0) { p[d].NoGravity = true; p[d].Velocity *= 2f; p[d].FadeIn = 2.5f; }
            }
            for (int i = 0; i < 10; i++)
            {
                int d = Put(Ring(), 6, 0, 2.7f);
                if (d >= 0) { p[d].NoGravity = true; p[d].Velocity *= 3f; }
            }
            for (int i = 0; i < 10; i++)
            {
                int d = Put(Ring(), 31, 0, 1.5f);
                if (d >= 0) { p[d].NoGravity = true; p[d].Velocity *= 3f; }
            }
            for (int i = 0; i < 2; i++)
                p.NewGore(Disc() - new Vector2(16f, 16f), Vector2.zero, Random.Range(61, 64), 1f, 0.3f, new Vector2(Random.Range(-0.5f, 0.5f), Random.Range(-0.5f, 0.5f)));
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>Vector2.RotatedBy (y down: a positive angle turns clockwise on screen).</summary>
        static Vector2 Rotate(Vector2 v, float a)
        {
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        static float WrapAngle(float a) => Mathf.Repeat(a + Mathf.PI, 2f * Mathf.PI) - Mathf.PI;
    }
}
