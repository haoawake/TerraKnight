using TerraKnight.Effects;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The later minions (summon-hm specs, Terraria px, y down, per update). The Deadly Sphere
    /// (533, AI_066's 533 branch): it floats 60 px over the player and, when an enemy it can see
    /// comes within 800 px, dashes straight at it (where it will be), hurting only while dashing;
    /// three dashes, then it changes form (blade disc → spike ball → spark → blade disc); dashes
    /// ricochet off walls, and two bounces close together halve its damage for a moment.
    /// </summary>
    internal sealed partial class Minions
    {
        // ------------------------------------------------------------------ 533: Deadly Sphere (AI_066)

        /// <summary>ai0 = 3·group + form: group 0 hover, 1 changing form, 2 dashing, 3 flying back; form 0 disc, 1 spikes, 2 spark.</summary>
        void AI533(M m)
        {
            if (PlayerBuffs.Flag(m.S.Buff) && !ownerDead) m.TimeLeft = 2;
            m.LocalAi2 = Mathf.Clamp(m.LocalAi2 - 1f, 0f, 60f);
            foreach (var q in live)
            {
                if (q == m || q.Dead || q.Kind != Kind.DeadlySphere) continue;
                if (Mathf.Abs(m.Pos.x - q.Pos.x) + Mathf.Abs(m.Pos.y - q.Pos.y) >= m.W) continue;
                m.Vel.x += m.Pos.x < q.Pos.x ? -0.05f : 0.05f;
                m.Vel.y += m.Pos.y < q.Pos.y ? -0.05f : 0.05f;
            }
            int form = (int)m.Ai0 % 3;
            // P:30191: changing form: slowing, 17 updates of the transform frames.
            if (m.Ai0 >= 3f && m.Ai0 <= 5f)
            {
                m.Vel *= 0.9f;
                m.Ai1++;
                int n = (int)m.Ai1 / 2 + ((int)m.Ai0 - 3) * 8;
                m.Frame = n < 4 ? 17 + n : n == 4 ? 0 : n < 8 ? n - 4 : n < 11 ? 11 - n : n == 11 ? 0 : n < 16 ? n - 2 : n < 20 ? 29 - n : n == 20 ? 0 : n - 4;
                if (m.Ai1 > 16f)
                {
                    m.Ai0 -= 3f;
                    m.Ai1 = 0f;
                }
                return;
            }
            // P:30240: dashing, twice an update, straight on.
            if (m.Ai0 >= 6f && m.Ai0 <= 8f)
            {
                m.Ai1++;
                m.Extra = 1;
                m.Rot = m.Ai0 == 7f ? Mathf.Atan2(m.Vel.y, m.Vel.x) + Mathf.PI : m.Rot + Mathf.PI / 6f;
                int frame = m.Ai0 == 6f ? 5 : m.Ai0 == 7f ? 13 : 17;
                int len = m.Ai0 == 6f ? 40 : 30;
                m.Frame = frame;
                bool dashing = true;
                if (m.Ai1 > len)
                {
                    m.Ai1 = 1f;
                    m.Ai0 -= 6f;
                    m.LocalAi0++;
                    m.Extra = 0;
                    dashing = false;
                }
                if (m.Ai0 == 8f) SphereSparks(m, false);
                if (dashing) return;
            }
            if (m.Ai0 < 9f) m.TileCollide = true;
            if (m.TileCollide && SolidTile(TileOf(m.Center.x), TileOf(m.Center.y))) m.TileCollide = false;
            float range = 2000f;
            bool hasTarget = false;
            Vector2 target = Vector2.zero, targetVel = Vector2.zero;
            var t = OwnerTarget;
            if (t != null)
            {
                float d = Vector2.Distance(t.Center, m.Center);
                if (d < 6000f && ClearLine(m.Center, t.Center))
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
            if (Vector2.Distance(pCenter, m.Center) > (hasTarget ? 1500f : 900f))
            {
                if (m.Ai0 < 9f) m.Ai0 += 3f * (3 - (int)(m.Ai0 / 3f));
                m.TileCollide = false;
            }
            // It never goes after enemies itself: it keeps by the player and only dashes.
            bool returning = m.Ai0 >= 9f;
            float speed = returning ? 15f : 12f, inertia = 40f;
            var off = pCenter - m.Center + new Vector2(0f, -60f);
            float dist = off.magnitude;
            float ps = Mathf.Abs(pVel.x) + Mathf.Abs(pVel.y);
            if (speed < ps)
            {
                inertia = 30f;
                speed = ps;
                if (dist > 200f)
                {
                    inertia = 20f;
                    speed += 4f;
                }
                else if (dist > 100f) speed += 3f;
            }
            if (returning && dist > 300f)
            {
                speed += 6f;
                inertia -= 10f;
            }
            if (returning && dist < 450f && !SolidBox(m.Box)) m.Ai0 -= 9f;
            if (dist > 2000f) m.Center = pCenter;
            if (dist > 70f) m.Vel = (m.Vel * inertia + off.normalized * speed) / (inertia + 1f);
            else if (m.Vel == Vector2.zero) m.Vel = new Vector2(-0.15f, -0.05f);
            if (m.Vel.magnitude > speed) m.Vel *= 0.95f;
            if (m.Ai0 < 3f || m.Ai0 >= 9f)
            {
                m.Rot += m.Vel.x * 0.04f;
                if (++m.FrameCounter >= 24) m.FrameCounter = 0;
                m.Frame = form == 0 ? 4 + m.FrameCounter / 4 : form == 1 ? 14 + m.FrameCounter / 8 : 17 + (m.FrameCounter / 3) % 4;
            }
            if (m.Ai0 == 2f && Random.Range(0, 2) == 0) SphereSparks(m, true);
            if (m.Ai1 > 0f && ++m.Ai1 > 10f) m.Ai1 = 0f;
            if (m.Ai0 >= 9f) return;
            if (!(m.Ai1 == 0f && hasTarget && range < 800f)) return;
            m.Ai1++;
            if (m.LocalAi0 >= 3f)
            {
                m.Ai0 += 4f;
                if (m.Ai0 == 6f) m.Ai0 = 3f;
                m.LocalAi0 = 0f;
            }
            else
            {
                m.Ai0 += 6f;
                float s = m.Ai0 == 8f ? 12f : 10f;
                var v = target - m.Center;
                m.Vel = v.sqrMagnitude > 0f ? v.normalized * s : Vector2.zero;
                Intercept(m, target, targetVel / 2f, s);
            }
        }

        /// <summary>
        /// P:30281 / 30534: the spark form's prongs crackling: four motes (electric, golden or cursed)
        /// at its four diagonals, flying outwards; while hovering only half of them, smaller and unlit.
        /// </summary>
        static void SphereSparks(M m, bool idle)
        {
            var p = TerraParticles.Instance;
            if (p == null) return;
            for (int k = 0; k < 4; k++)
            {
                if (idle && Random.Range(0, 2) != 0) continue;
                int type = Random.Range(0, 3) switch { 0 => 226, 1 => 228, _ => 75 };
                var d = Rotate(Rotate(new Vector2(1f, 1f), k * Mathf.PI / 2f), m.Rot);
                int i = p.NewDust(m.Center, 0, 0, type);
                if (i < 0) continue;
                p[i].Position = m.Center + d * 10f;
                p[i].Velocity = d;
                p[i].Scale = (idle ? 0.3f : 0.6f) + Random.value * 0.5f;
                p[i].NoGravity = true;
            }
        }

        /// <summary>
        /// P:18285: a dash (or a hover) that the blocks changed bounces: back the way it came across,
        /// halved up and down, kept to its speed; the blocks it hits crack while dashing; heat 30.
        /// </summary>
        void BounceSphere(M m, Vector2 before)
        {
            if (m.Vel == before) return;
            if (m.Vel.x != before.x) m.Vel.x = -before.x;
            if (m.Vel.y != before.y || m.Vel.y == 0f) m.Vel.y = -before.y * 0.5f;
            float k = m.Vel.magnitude > 0f ? before.magnitude / m.Vel.magnitude : 1f;
            if (k == 0f || float.IsNaN(k)) k = 1f;
            m.Vel /= k;
            if (m.Ai0 == 7f && m.Vel.y < -0.1f) m.Vel.y += 0.1f;
            if (m.Ai0 >= 6f && m.Ai0 < 9f)
            {
                // Collision.HitTiles: the touched blocks' dust and dig sound.
                for (int n = 0; n < 4; n++) DustBox(m, 1, Vector2.zero, 0, 1f);
                Sound("Dig_" + Random.Range(0, 3));
            }
            m.LocalAi2 += 30f;
        }

        /// <summary>
        /// Main.DrawProj for 533: while dashing, its last positions (7 for the disc, 3 else), the
        /// spinning forms fanning out further each copy; the sphere lit; its glow (Glow_128) at 30%.
        /// </summary>
        static void DrawDeadlySphere(FxBatch fx, M m)
        {
            var spr = FxBatch.Frame("Projectile_533", 0, 48 * m.Frame, 48, 48, 24f, 24f);
            if (m.Ai0 >= 6f && m.Ai0 <= 8f)
            {
                int n = m.Ai0 == 6f ? 8 : 4;
                float fan = m.Ai0 == 7f ? 0f : 0.2f;
                for (int k = 1; k < n && k < m.OldPosRing.Length; k++)
                {
                    float c = (n - k) / 15f;
                    fx.Draw(spr, m.OldPosRing[k] + new Vector2(10f, 10f + m.GfxOffY), new Color(Lit.r * c, Lit.g * c, Lit.b * c, c), m.Rot + m.Rot * fan * (k - 1), 1f);
                }
            }
            var at = m.Center + new Vector2(0f, m.GfxOffY);
            fx.Draw(spr, at, Lit, m.Rot, 1f);
            fx.Draw(FxBatch.Frame("Glow_128", 0, 48 * m.Frame, 48, 48, 24f, 24f), at, new Color(0.3f, 0.3f, 0.3f, 0.3f), m.Rot, 1f);
            fx.Light(at, (int)m.Ai0 % 3 != 2 ? new Color(0.8f, 0.3f, 0.1f) : new Color(0.3f, 0.5f, 0.7f), 3f);
        }

        // ------------------------------------------------------------------ 407 / 423 / 613: AI_062's later minions

        /// <summary>
        /// AI_062 for the Sharknado (407), the UFO (423) and the Stardust Cell (613): the enemy
        /// nearest the player that the minion can see (the UFO aims at its top); the UFO hovers 80 px
        /// over it and the cell 60 px off it, zipping there in one tick when 300-800 (cell 400-800) px
        /// away; the Sharknado drifts through blocks, faint inside them. Shots: the Sharknado's
        /// sharks (408, 20 px), the UFO's lasers (433, within 400 px), the cell's clinging cells
        /// (614, within 500 px, a kick back).
        /// </summary>
        void AI062b(M m)
        {
            bool shark = m.Kind == Kind.Sharknado, ufo = m.Kind == Kind.UFO, cell = m.Kind == Kind.StardustCell;
            if (PlayerBuffs.Flag(m.S.Buff) && !ownerDead) m.TimeLeft = 2;
            var p = TerraParticles.Instance;
            if (cell)
            {
                FxBatch.Instance?.Light(m.Center, new Color(0.2f, 0.6f, 0.7f), 2.5f);
                if (m.LocalAi1 > 0f) m.LocalAi1--;
            }
            if ((ufo || cell) && m.Ai0 == 2f)
            {
                // The zip: the whole way in one tick (extraUpdates = distance / 10), with a trail.
                m.Ai1--;
                m.TileCollide = false;
                if (m.Ai1 > 3f && p != null)
                {
                    if (ufo)
                    {
                        int i = p.NewDust(m.Center, 0, 0, 220 + Random.Range(0, 2), m.Vel.x, m.Vel.y, 100);
                        if (i >= 0)
                        {
                            p[i].Scale = 0.5f + Random.value * 0.3f;
                            p[i].Velocity /= 2.5f;
                            p[i].NoGravity = true;
                        }
                    }
                    else
                    {
                        // numUpdates counts down the zip's updates: the remaining ones are about ai[1].
                        float rem = m.Ai1, big = 2f - rem / 30f;
                        if (rem < 20f)
                            for (int k = 0; k < 3; k++)
                            {
                                int i = p.NewDust(m.Pos, m.W, m.H, 229);
                                if (i < 0) continue;
                                p[i].NoGravity = true;
                                p[i].Position = m.Center;
                                p[i].Velocity = p[i].Velocity * 3f + m.Vel * 3f;
                                p[i].FadeIn = 1f;
                            }
                        if (big > 0f)
                            for (int k = 0; k < 2; k++)
                            {
                                int i = p.NewDust(m.Pos, m.W, m.H, 229);
                                if (i < 0) continue;
                                p[i].NoGravity = true;
                                p[i].Position = m.Center + Rotate(Vector2.up, rem * Mathf.PI / 30f + m.Serial * Mathf.PI / 4f + Mathf.PI / 2f) * (m.H / 2f) - m.Vel * (k / 2f);
                                p[i].Velocity = m.Vel / 3f;
                                p[i].FadeIn = big / 2f;
                                p[i].Scale = big;
                            }
                    }
                }
                if (m.Ai1 != 0f) return;
                m.Ai1 = 30f;
                m.Ai0 = 0f;
                m.Vel /= 5f;
                m.Vel.y = 0f;
                m.Extra = 0;
                if (cell && p != null)
                    for (int k = 0; k < 30; k++)
                    {
                        int i = p.NewDust(m.Pos, m.W, m.H, 229);
                        if (i < 0) continue;
                        p[i].NoGravity = true;
                        p[i].Position = k < 15 ? m.Center - m.Vel * 5f : m.Center;
                        p[i].Velocity = p[i].Velocity * 3f + m.Vel * 3f;
                        p[i].FadeIn = 1f;
                        if (Random.Range(0, 3) != 0)
                        {
                            p[i].FadeIn = 2f;
                            p[i].Scale = 2f;
                            p[i].Velocity /= 8f;
                        }
                    }
            }
            if ((ufo || cell) && m.LocalAi0 > 0f) m.LocalAi0--;
            float push = shark ? 0.1f : 0.05f, near = shark ? m.W * 2f : m.W;
            foreach (var q in live)
            {
                if (q == m || q.Dead || q.Kind != m.Kind) continue;
                if (Mathf.Abs(m.Pos.x - q.Pos.x) + Mathf.Abs(m.Pos.y - q.Pos.y) >= near) continue;
                m.Vel.x += m.Pos.x < q.Pos.x ? -push : push;
                m.Vel.y += m.Pos.y < q.Pos.y ? -push : push;
            }
            float range = 2000f;
            bool hasTarget = false;
            var target = m.Pos;
            Npc targetNpc = null;
            m.TileCollide = !shark;
            if (shark)
            {
                // Through blocks, faint inside them.
                if (SolidBox(m.Box)) m.Alpha = Mathf.Min(150f / 255f, m.Alpha + 20f / 255f);
                else m.Alpha = Mathf.Max(60f / 255f, m.Alpha - 50f / 255f);
            }
            // The UFO aims at the enemy's top; distances are from the player.
            Vector2 Aim(Npc n) => new Vector2(n.Box.x + n.Box.width * 0.5f, n.Box.y + (ufo ? 0f : n.Box.height * 0.5f));
            var t = OwnerTarget;
            if (t != null)
            {
                float d = Vector2.Distance(Aim(t), pCenter);
                if (d < range * 3f && ClearLine(m.Center, t.Center))
                {
                    range = d;
                    target = Aim(t);
                    targetNpc = t;
                    hasTarget = true;
                }
            }
            if (!hasTarget)
                foreach (var n in npcs)
                {
                    float d = Vector2.Distance(Aim(n), pCenter);
                    if (d >= range || !CanReach(m, n)) continue;
                    range = d;
                    target = Aim(n);
                    targetNpc = n;
                    hasTarget = true;
                }
            float leash = hasTarget ? (ufo ? 1200f : cell ? 1350f : 1000f) : 500f;
            if (m.Ai0 != 1f && Vector2.Distance(pCenter, m.Center) > leash) m.Ai0 = 1f;
            if (m.Ai0 == 1f) m.TileCollide = false;
            bool keepShooting = ufo || shark;
            if (hasTarget && (keepShooting || m.Ai0 == 0f))
            {
                var v = target - m.Center;
                float dist = v.magnitude;
                v = dist > 0f ? v / dist : Vector2.zero;
                if (ufo || cell)
                {
                    Vector2 spot;
                    if (ufo) spot = target - new Vector2(0f, 80f);
                    else
                    {
                        var away = m.Center - target;
                        if (away == Vector2.zero) away = new Vector2(0f, -1f);
                        spot = target + away.normalized * 60f;
                    }
                    if (SolidTile(TileOf(spot.x), TileOf(spot.y)))
                    {
                        spot.y += 16f;
                        if (SolidTile(TileOf(spot.x), TileOf(spot.y))) spot.y += 16f;
                    }
                    v = spot - m.Center;
                    dist = v.magnitude;
                    v = dist > 0f ? v / dist : Vector2.zero;
                    if (dist > (ufo ? 300f : 400f) && dist <= 800f && m.LocalAi0 == 0f)
                    {
                        m.Ai0 = 2f;
                        m.Ai1 = (int)(dist / 10f);
                        m.Extra = (int)m.Ai1;
                        m.Vel = v * 10f;
                        m.LocalAi0 = 60f;
                        return;
                    }
                }
                if (shark)
                {
                    if (dist > 400f) m.Vel = (m.Vel * 20f + v * 3f) / 21f;
                    else m.Vel *= 0.96f;
                }
                if (dist > 200f) m.Vel = (m.Vel * 40f + v * 6f) / 41f;
                else if (ufo || cell)
                {
                    if (dist > 70f && dist < 130f)
                    {
                        var w = v * (dist < 100f ? -3f : 7f);
                        m.Vel = (m.Vel * 20f + w) / 21f;
                        if (Mathf.Abs(w.x) > Mathf.Abs(w.y)) m.Vel.x = (m.Vel.x * 10f + w.x) / 11f;
                    }
                    else m.Vel *= 0.97f;
                }
                else if (m.Vel.y > -1f) m.Vel.y -= 0.1f;
            }
            else
            {
                if (m.Ai0 != 1f && !ClearLine(m.Center, pCenter)) m.Ai0 = 1f;
                float speed = m.Ai0 == 1f ? 15f : 6f;
                if (shark) speed = 9f;
                var off = pCenter - m.Center + new Vector2(0f, shark ? -20f : -60f);
                float dist = off.magnitude;
                if (dist > 200f && speed < 9f) speed = 9f;
                if ((ufo || shark) && dist > 300f && speed < 12f) speed = 12f;
                if (dist < 100f && m.Ai0 == 1f && !SolidBox(m.Box)) m.Ai0 = 0f;
                if (dist > 2000f) m.Pos = pCenter - new Vector2(m.W / 2f, m.W / 2f);
                if (shark)
                {
                    if (Mathf.Abs(off.x) > 40f || Mathf.Abs(off.y) > 10f)
                    {
                        var w = off.normalized * speed;
                        m.Vel = (m.Vel * 20f + new Vector2(w.x * 1.25f, w.y * 0.65f)) / 21f;
                    }
                    else
                    {
                        if (m.Vel == Vector2.zero) m.Vel = new Vector2(-0.15f, -0.05f);
                        m.Vel *= 1.01f;
                    }
                }
                else if (dist > 70f) m.Vel = (m.Vel * 20f + off.normalized * speed) / 21f;
                else
                {
                    if (m.Vel == Vector2.zero) m.Vel = new Vector2(-0.15f, -0.05f);
                    m.Vel *= 1.01f;
                }
                if (dist > 250f && (ufo || shark))
                {
                    // Keeping up with a running player.
                    if ((m.Vel.x < 0f && pVel.x >= 0f) || (m.Vel.x >= 0f && pVel.x < 0f)) m.Vel.x *= 0.95f;
                    else m.Vel.x += pVel.x * 0.125f;
                    if ((m.Vel.y < 0f && pVel.y >= 0f) || (m.Vel.y >= 0f && pVel.y < 0f)) m.Vel.y *= 0.95f;
                    else m.Vel.y += pVel.y * 0.125f;
                    if (m.Vel.magnitude > speed) m.Vel = m.Vel.normalized * speed;
                }
            }
            m.Rot = m.Vel.x * 0.05f;
            m.FrameCounter++;
            if (shark)
            {
                if (m.FrameCounter >= 12) m.FrameCounter = 0;
                m.Frame = m.FrameCounter / 2;
                if (Random.Range(0, 5) == 0)
                {
                    int i = DustBox(m, 217, Vector2.zero, 100, 2f);
                    if (i >= 0)
                    {
                        p[i].Velocity *= 0.3f;
                        p[i].NoGravity = true;
                    }
                }
            }
            else
            {
                if (m.FrameCounter >= 12) m.FrameCounter = 0;
                m.Frame = m.FrameCounter / 3;
            }
            if (m.Vel.x > 0f) m.SpriteDir = m.Dir = -1;
            else if (m.Vel.x < 0f) m.SpriteDir = m.Dir = 1;
            if (m.Ai1 > 0f)
            {
                m.Ai1++;
                if (Random.Range(0, 3) != 0) m.Ai1++;
            }
            if (m.Ai1 > (shark ? 50f : ufo ? 45f : 60f)) m.Ai1 = 0f;
            if (!keepShooting && m.Ai0 != 0f) return;
            if (!hasTarget) return;
            var pr = TerraProjectiles.Instance;
            if (shark)
            {
                if (SolidBox(m.Box) || m.Ai1 != 0f) return;
                m.Ai1++;
                var v = (target - m.Center).normalized * 20f;
                pr?.ShootMinion(ProjKind.SharknadoShark, TerraParticles.ToWorld(m.Center), new Vector2(v.x, -v.y), m.Damage, m.Knockback);
                return;
            }
            if (ufo)
            {
                float ang = Mathf.Atan2(target.y - m.Center.y, target.x - m.Center.x);
                if (Mathf.Abs(ang - Mathf.PI / 2f) > Mathf.PI / 4f)
                {
                    var back = target - m.Center - new Vector2(0f, 80f);
                    if (back.sqrMagnitude > 0f) m.Vel += back.normalized;
                }
                if ((target - m.Center).magnitude > 400f || m.Ai1 != 0f) return;
                m.Ai1++;
                var v = (target - m.Center).normalized * 4f;
                pr?.ShootMinion(ProjKind.UfoLaser, TerraParticles.ToWorld(m.Center), new Vector2(v.x, -v.y), m.Damage, m.Knockback);
                return;
            }
            // The cell: its seed, a kick back, a ring of blue motes.
            if ((target - m.Center).magnitude > 500f || m.Ai1 != 0f) return;
            m.Ai1++;
            var s = (target - m.Center).normalized * 14f;
            pr?.ShootStardustCell(TerraParticles.ToWorld(m.Center), new Vector2(s.x, -s.y), m.Damage, m.Knockback, targetNpc != null ? targetNpc.Hm : null);
            m.Vel -= s / 3f;
            if (p == null) return;
            int r = m.W / 4;
            for (int k = 0; k < 5; k++)
            {
                int i = p.NewDust(m.Center - Vector2.one * r, r * 2, r * 2, 88);
                if (i < 0) continue;
                var dir = (p[i].Position - m.Center).normalized;
                p[i].Position = m.Center + dir * r - new Vector2(4f, 4f);
                p[i].Velocity = dir * p[i].Velocity.magnitude * 2f;
                p[i].NoGravity = true;
                p[i].Scale = 0.7f + Random.value;
            }
        }

        /// <summary>
        /// Main.DrawProj for 407 / 423 / 613: the frame, lit (the Sharknado faint by its alpha); the
        /// UFO 2 px up with its glow (Glow_0) added; the cell with its glow (Glow_189) half added.
        /// </summary>
        static void DrawAI062b(FxBatch fx, M m)
        {
            if (m.Kind == Kind.Sharknado)
            {
                float a = 1f - m.Alpha;
                fx.Draw(FxBatch.Frame("Projectile_407", 0, 44 * m.Frame, 44, 44, 22f, 22f), m.Center + new Vector2(0f, m.GfxOffY), Lit * a, m.Rot, 1f, flipX: m.SpriteDir == -1);
                return;
            }
            if (m.Kind == Kind.UFO)
            {
                var at = m.Center + new Vector2(0f, m.GfxOffY - 2f);
                fx.Draw(FxBatch.Frame("Projectile_423", 0, 32 * m.Frame, 38, 32, 19f, 16f), at, Lit, m.Rot, 1f, flipX: m.SpriteDir == -1);
                fx.Draw(FxBatch.Frame("Glow_0", 0, 32 * m.Frame, 38, 32, 19f, 16f), at, new Color(250f / 255f, 250f / 255f, 250f / 255f, 0f), m.Rot, 1f, flipX: m.SpriteDir == -1);
                return;
            }
            var c = m.Center + new Vector2(0f, m.GfxOffY);
            fx.Draw(FxBatch.Frame("Projectile_613", 0, 26 * m.Frame, 24, 26, 12f, 13f), c, Lit, m.Rot, 1f, flipX: m.SpriteDir == -1);
            fx.Draw(FxBatch.Frame("Glow_189", 0, 26 * m.Frame, 24, 26, 12f, 13f), c, new Color(0.5f, 0.5f, 0.5f, 0f), m.Rot, 1f, flipX: m.SpriteDir == -1);
        }

        // ------------------------------------------------------------------ 317: Raven (AI_054)

        /// <summary>
        /// P:28993: the enemy nearest it (Manhattan, under 900 px) that it can see; then it swoops at
        /// 16 px (10 inside 100 px), (v·14 + aim)/15, hurting while it does; a hit sends it off for 17
        /// updates (harmless, speeding up 5% an update) before the next swoop. Idle it flies 60 px
        /// over the player at 12 (8 near, 15 coming back through blocks). Off walls it bounces (0.6).
        /// </summary>
        void AI054(M m)
        {
            if (PlayerBuffs.Flag(m.S.Buff) && !ownerDead) m.TimeLeft = 2;
            foreach (var q in live)
            {
                if (q == m || q.Dead || q.Kind != m.Kind) continue;
                if (Mathf.Abs(m.Pos.x - q.Pos.x) + Mathf.Abs(m.Pos.y - q.Pos.y) >= m.W) continue;
                m.Vel.x += m.Pos.x < q.Pos.x ? -0.05f : 0.05f;
                m.Vel.y += m.Pos.y < q.Pos.y ? -0.05f : 0.05f;
            }
            var target = m.Pos;
            float range = 900f;
            bool found = false;
            float leash = m.Ai1 != 0f || m.Friendly ? 1400f : 500f;
            if (Mathf.Abs(m.Center.x - pCenter.x) + Mathf.Abs(m.Center.y - pCenter.y) > leash) m.Ai0 = 1f;
            if (m.Ai0 == 0f)
            {
                m.TileCollide = true;
                var t = OwnerTarget;
                if (t != null)
                {
                    float d = Manhattan(m.Center, t.Center);
                    if (d < range && ClearLine(m.Center, t.Center))
                    {
                        range = d;
                        target = t.Center;
                        found = true;
                    }
                }
                if (!found)
                    foreach (var n in npcs)
                    {
                        float d = Manhattan(m.Center, n.Center);
                        if (d >= range || !CanReach(m, n)) continue;
                        range = d;
                        target = n.Center;
                        found = true;
                    }
            }
            else m.TileCollide = false;
            if (!found)
            {
                float speed = m.Ai0 == 1f ? 12f : 8f;
                var off = pCenter - m.Center + new Vector2(0f, -60f);
                float dist = off.magnitude;
                if (dist < 100f && m.Ai0 == 1f && !SolidBox(m.Box)) m.Ai0 = 0f;
                if (dist > 2000f) m.Pos = pCenter - new Vector2(m.W / 2f, m.W / 2f);
                if (dist > 100f) speed = m.Ai0 == 1f ? 15f : 12f;
                if (dist > 70f) m.Vel = (m.Vel * 20f + off / dist * speed) / 21f;
                else
                {
                    if (m.Vel == Vector2.zero) m.Vel = new Vector2(-0.15f, -0.05f);
                    m.Vel *= 1.01f;
                }
                m.Friendly = false;
                m.Rot = m.Vel.x * 0.05f;
                if (++m.FrameCounter >= 4)
                {
                    m.FrameCounter = 0;
                    m.Frame++;
                }
                if (m.Frame > 3) m.Frame = 0;
                if (Mathf.Abs(m.Vel.x) > 0.2f) m.SpriteDir = -(m.Vel.x < 0f ? -1 : 1);
                return;
            }
            if (m.Ai1 == -1f) m.Ai1 = 17f;
            if (m.Ai1 > 0f) m.Ai1--;
            if (m.Ai1 == 0f)
            {
                m.Friendly = true;
                var v = target - m.Center;
                float dist = v.magnitude;
                float speed = dist < 100f ? 10f : 16f;
                if (dist > 0f) m.Vel = (m.Vel * 14f + v / dist * speed) / 15f;
            }
            else
            {
                m.Friendly = false;
                if (Mathf.Abs(m.Vel.x) + Mathf.Abs(m.Vel.y) < 10f) m.Vel *= 1.05f;
            }
            m.Rot = m.Vel.x * 0.05f;
            if (++m.FrameCounter >= 4)
            {
                m.FrameCounter = 0;
                m.Frame++;
            }
            if (m.Frame < 4 || m.Frame > 7) m.Frame = 4;
            if (Mathf.Abs(m.Vel.x) > 0.2f) m.SpriteDir = -(m.Vel.x < 0f ? -1 : 1);
        }

        /// <summary>P:18474: a raven the blocks stop bounces back at 0.6.</summary>
        static void BounceRaven(M m, Vector2 before)
        {
            if (m.Vel.x != before.x) m.Vel.x = before.x * -0.6f;
            if (m.Vel.y != before.y) m.Vel.y = before.y * -0.6f;
        }

        // ------------------------------------------------------------------ 946: Terraprisma (AI_156)

        /// <summary>The blades' places: the n-th of the player's blades, its index among them.</summary>
        void PrismaIndex(M m, out int index, out int total)
        {
            index = total = 0;
            foreach (var q in live)
            {
                if (q.Dead || q.Kind != Kind.Terraprisma) continue;
                total++;
                if (q.Serial < m.Serial) index++;
            }
        }

        /// <summary>
        /// AI_156_GetIdlePosition for 946: a fan of blades behind the player's head, each tilted a
        /// little further, bobbing on the player's misc counter.
        /// </summary>
        Vector2 PrismaIdle(M m, out float rot)
        {
            PrismaIndex(m, out int index, out int total);
            int n = index + 1;
            rot = WrapAngle(n * Mathf.PI * 2f / 60f * pDir + Mathf.PI / 2f);
            int k = n % Mathf.Max(1, total);
            float misc = (Time.time * 60f % 300f) / 300f;
            var bob = Rotate(new Vector2(0f, 0.5f), (misc * (2f + k) + k * 0.5f + pDir * 1.3f) * Mathf.PI * 2f) * 4f;
            var spot = new Vector2(Mathf.Cos(rot), Mathf.Sin(rot)) * 10f + pCenter + new Vector2(pDir * (n * -6 - 16), -15f) + bob;
            rot += Mathf.PI / 2f;
            return spot;
        }

        /// <summary>AI_156_TryAttackingNPCs for 946 (skipping the body check): the owner's target within 1000 px of the player, else the enemy nearest him.</summary>
        Npc PrismaTarget(M m)
        {
            var t = OwnerTarget;
            if (t != null && Vector2.Distance(t.Center, pCenter) <= 1000f && CanReach(m, t)) return t;
            Npc best = null;
            float bestD = 1000f;
            foreach (var n in npcs)
            {
                float d = Vector2.Distance(n.Center, pCenter);
                if (d > bestD || !CanReach(m, n)) continue;
                bestD = d;
                best = n;
            }
            return best;
        }

        /// <summary>
        /// AI_156 for 946: blades fanned behind the player; one in 20 ticks a blade starts an attack
        /// (always the dive first): the swing (40 updates, an arc from where it was through the
        /// enemy towards the player) or the dive (80: up 100 px, through the enemy, out past it);
        /// each attack hits each enemy once; then the next, or back to its place at 32 px a tick.
        /// </summary>
        void AI156b(M m)
        {
            if (PlayerBuffs.Flag(m.S.Buff) && !ownerDead) m.TimeLeft = 2;
            m.TileCollide = false;
            const int swing = 40, swingStart = swing - 1, dive = swing + 40, diveStart = dive - 1, diveFrom = swing + 1;
            FxBatch.Instance?.Light(m.Center, FxParticles.HslToRgb(PrismaHue(m), 1f, 0.5f) * 0.6f, 2.5f);
            if (Vector2.Distance(pCenter, m.Center) > 2000f)
            {
                m.Ai0 = 0f;
                m.Ai1 = 0f;
            }
            if (m.Ai0 == -1f)
            {
                var spot = PrismaIdle(m, out float rot);
                m.Vel = Vector2.zero;
                m.Center = Vector2.MoveTowards(m.Center, spot, 32f);
                m.Rot = Mathf.LerpAngle(m.Rot * Mathf.Rad2Deg, rot * Mathf.Rad2Deg, 0.2f) * Mathf.Deg2Rad;
                if (Vector2.Distance(m.Center, spot) < 2f) m.Ai0 = 0f;
                return;
            }
            if (m.Ai0 == 0f)
            {
                var spot = PrismaIdle(m, out float rot);
                m.Vel = Vector2.zero;
                m.Center = Vector2.Lerp(m.Center, spot, 0.42525f);
                m.Rot = Mathf.LerpAngle(m.Rot * Mathf.Rad2Deg, rot * Mathf.Rad2Deg, 0.45f) * Mathf.Deg2Rad;
                if (Random.Range(0, 20) == 0)
                {
                    var t = PrismaTarget(m);
                    if (t != null)
                    {
                        m.Immune.Clear();
                        m.Ai0 = dive;
                        m.TargetId = t.Id;
                    }
                }
                return;
            }
            bool diving = m.Ai0 >= diveFrom;
            int from = diving ? diveStart : swingStart, to = diving ? diveFrom : 0;
            if (!npcById.TryGetValue(m.TargetId, out var npc))
            {
                NextPrismaAttack(m);
                return;
            }
            m.Ai0--;
            if (m.Ai0 >= from)
            {
                m.Dir = m.Center.x < npc.Center.x ? 1 : -1;
                if (m.Ai0 == from) m.Start = m.Center;
            }
            float tt = Mathf.Clamp01(Mathf.InverseLerp(from, to, m.Ai0));
            if (!diving)
            {
                var a = tt >= 0.5f ? Vector2.Lerp(npc.Center, pCenter, 0.5f) : m.Start;
                var b = npc.Center;
                float phi = Mathf.Atan2(b.y - a.y, b.x - a.x);
                float s = m.Dir == 1 ? -Mathf.PI : Mathf.PI;
                float ang = s + -s * tt * 2f;
                var u = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang) * 0.5f * (0.8f + Mathf.Sin(m.Serial * 2.3f) * 0.2f));
                u = Rotate(u, phi);
                m.Center = Vector2.Lerp(a, b, 0.5f) + u * (b - a).magnitude / 2f;
                float w = WrapAngle(phi + ang);
                m.Rot = w + Mathf.PI / 2f;
                m.Vel = new Vector2(Mathf.Cos(w), Mathf.Sin(w)) * 10f;
                m.Pos -= m.Vel;
            }
            else
            {
                var start = m.Start + new Vector2(0f, Mathf.Clamp01(tt / 0.4f) * -100f);
                var v = npc.Center - start;
                var past = npc.Center + (v.sqrMagnitude > 0f ? v.normalized : Vector2.zero) * Mathf.Clamp(v.magnitude, 60f, 150f);
                float k1 = Mathf.Clamp01(Mathf.InverseLerp(0.4f, 0.6f, tt)), k2 = Mathf.Clamp01(Mathf.InverseLerp(0.6f, 1f, tt));
                float want = (v.sqrMagnitude > 0f ? Mathf.Atan2(v.y, v.x) : 0f) + Mathf.PI / 2f;
                m.Rot = Mathf.MoveTowardsAngle(m.Rot * Mathf.Rad2Deg, want * Mathf.Rad2Deg, 36f) * Mathf.Deg2Rad;
                m.Center = Vector2.Lerp(start, npc.Center, k1);
                if (k2 > 0f) m.Center = Vector2.Lerp(npc.Center, past, k2);
                m.Vel = Vector2.zero;
            }
            if (m.Ai0 == to) NextPrismaAttack(m);
        }

        /// <summary>After an attack (or a lost enemy): another enemy, a random swing or dive; none: back to its place.</summary>
        void NextPrismaAttack(M m)
        {
            var t = PrismaTarget(m);
            if (t != null)
            {
                m.Ai0 = Random.Range(0, 2) == 0 ? 40f : 80f;
                m.TargetId = t.Id;
                m.Immune.Clear();
            }
            else
            {
                m.Ai0 = -1f;
                m.Ai1 = 0f;
            }
        }

        /// <summary>Main.DrawProj's hue for 946: its place among the minion slots, turning once every 3 seconds.</summary>
        float PrismaHue(M m)
        {
            PrismaIndex(m, out int index, out _);
            float slots = Mathf.Max(1f, Equipment.MaxMinions);
            return (index % slots / slots + Time.time % 3f / 3f) % 1f;
        }

        /// <summary>GetFairyQueenWeaponsColorFull: hsl((hue + 0.5) % 1, 1, 0.5), its alpha scaled.</summary>
        static Color FairyColor(float hue, float alphaMult)
        {
            var c = FxParticles.HslToRgb((hue + 0.5f) % 1f, 1f, 0.5f);
            c.a = alphaMult;
            return c;
        }

        /// <summary>
        /// Main.DrawProj for 946: its EmpressBlade trail (its last 20 places, from one rainbow
        /// colour to the opposite one, fading, added), the dive's six afterimages, a halo of four
        /// copies, the white blade and a tinted copy over it.
        /// </summary>
        void DrawTerraprisma(FxBatch fx, M m)
        {
            float hue = PrismaHue(m);
            var half = new Vector2(m.W, m.H) / 2f;
            var streak = FxBatch.Whole("Extra_98");
            for (int k = 1; k < m.OldPosRing.Length; k++)
            {
                var a = m.OldPosRing[k - 1] + half;
                var b = m.OldPosRing[k] + half;
                var d = b - a;
                if (d.sqrMagnitude < 0.25f) continue;
                float p = (k - 0.5f) / m.OldPosRing.Length;
                var c = Color.Lerp(FairyColor(hue, 0f), FairyColor((hue + 0.5f) % 1f, 0f), Mathf.Clamp01(p / 0.7f)) * (1f - Mathf.Clamp01(p / 0.98f));
                c.a = 0f;
                fx.Draw(streak, (a + b) / 2f, c * 0.6f, Mathf.Atan2(d.y, d.x) + Mathf.PI / 2f, new Vector2(36f / 72f * 2f, d.magnitude / 72f * 1.6f));
            }
            var at = m.Center;
            var tex = FxBatch.Frame("Projectile_946", 0, 0, 94, 30, 47f, 15f);
            float rot = m.Rot - Mathf.PI / 2f;
            var tint = FairyColor(hue, 0.5f);
            tint.a *= 0.5f;
            const float glow = 0.3f;
            float back = Mathf.Clamp01(Mathf.InverseLerp(60f, 50f, m.Ai0));
            float fade = Mathf.Clamp01(Mathf.InverseLerp(70f, 50f, m.Ai0)) * Mathf.Clamp01(Mathf.InverseLerp(40f, 45f, m.Ai0));
            for (float n = 0f; n < 1f; n += 1f / 6f)
                fx.Draw(tex, at + new Vector2(Mathf.Cos(rot), Mathf.Sin(rot)) * -120f * n * back, tint * glow * (1f - n) * fade, rot, 1.5f);
            for (float n = 0f; n < 1f; n += 0.25f)
                fx.Draw(tex, at + new Vector2(Mathf.Cos(n * Mathf.PI * 2f + rot), Mathf.Sin(n * Mathf.PI * 2f + rot)) * 4f, tint * glow, rot, 1f);
            fx.Draw(tex, at, new Color(1f, 1f, 1f, 0.7f), rot, 1f);
            fx.Draw(tex, at, tint * glow * 0.5f, rot, 1f);
        }

        // ------------------------------------------------------------------ 831 / 833-835: Desert Tiger (AI_164, AI_067)

        static bool IsTiger(Kind k) => k == Kind.Tiger1 || k == Kind.Tiger2 || k == Kind.Tiger3;

        /// <summary>
        /// AI_164 for 831: a gem per staff use, circling above the player's head in rings of four
        /// (8 px out, 12 more each ring; the last ring holds still, fanned), six frames.
        /// </summary>
        void AITigerGem(M m)
        {
            if (PlayerBuffs.Flag(m.S.Buff) && !ownerDead) m.TimeLeft = 2;
            m.LocalAi1++;
            if (++m.FrameCounter >= 4)
            {
                m.FrameCounter = 0;
                if (++m.Frame >= 6) m.Frame = 0;
            }
            int index = 0, total = 0;
            foreach (var q in live)
            {
                if (q.Dead || q.Kind != Kind.TigerGem) continue;
                total++;
                if (q.Serial < m.Serial) index++;
            }
            float spin = (Time.time * 60f % 300f) / 300f * 2f;
            int ring = index / 4, rings = total / 4 + (total % 4 > 0 ? 1 : 0);
            int inRing = (total - ring * 4) % 4;
            if (inRing == 0) inRing = 4;
            if (rings - 1 != ring) inRing = 4;
            int at = index % inRing;
            float part = (float)at / inRing;
            spin += ring / 8f;
            if (index >= (rings - 1) * 4 && ring > 0)
            {
                spin = 0f;
                part = inRing switch { 1 => 0f, 2 => 0.25f + at * 0.5f, 3 => (at - 1) / 6f, _ => (at - 1.5f) / 6f };
            }
            var off = Rotate(new Vector2(0f, -8f - 12f * ring), (spin + part) * Mathf.PI * 2f) + new Vector2(0f, -40f);
            m.TileCollide = false;
            m.Vel = Vector2.zero;
            m.Center = new Vector2(Mathf.Floor(pCenter.x + off.x), Mathf.Floor(pCenter.y - 21f + off.y));
        }

        int TigerGems() => DevCount(Kind.TigerGem);

        int HighestGemDamage()
        {
            int best = 0;
            foreach (var m in live) if (!m.Dead && m.Kind == Kind.TigerGem) best = Mathf.Max(best, m.OriginalDamage);
            return best;
        }

        /// <summary>
        /// Player.UpdateStormTigerStatus (while the buff is on): one tiger for the gems, its size by
        /// their count (1-3, 4-6, 7 and up); a wrong-sized one goes, the right one comes at the player.
        /// </summary>
        void UpdateStormTigerStatus()
        {
            if (!PlayerBuffs.Has(PlayerBuffs.StormTiger) || ownerDead) return;
            int gems = TigerGems();
            var want = gems > 6 ? Kind.Tiger3 : gems > 3 ? Kind.Tiger2 : gems > 0 ? (Kind?)Kind.Tiger1 : null;
            bool wrong = want == null;
            foreach (var m in live) if (!m.Dead && IsTiger(m.Kind) && m.Kind != want) wrong = true;
            if (wrong)
            {
                foreach (var m in live) if (!m.Dead && IsTiger(m.Kind) && m.Kind != want) Kill(m);
                return;
            }
            if (DevCount(want.Value) > 0) return;
            M newest = null;
            foreach (var m in live) if (!m.Dead && m.Kind == Kind.TigerGem && (newest == null || m.LocalAi1 < newest.LocalAi1)) newest = m;
            var t = Spawn(want.Value, null, pCenter, 0, newest != null ? newest.Knockback : 2.5f);
            t.OriginalDamage = HighestGemDamage();
            t.LocalAi0 = 60f;
        }

        /// <summary>
        /// AI_067 for the tigers: it walks after enemies (8 px, a hop at those 80 px above, a bounce off
        /// whatever it touches); an enemy in the air (15 open tiles under it) it rides, spinning over
        /// it. Every 6, 5 or 4 seconds it pounces from enemy to enemy (818), unseen meanwhile, and
        /// lands at the last.
        /// </summary>
        void AITiger(M m)
        {
            bool big = m.Kind != Kind.Tiger1;
            if (PlayerBuffs.Flag(m.S.Buff) && !ownerDead) m.TimeLeft = 2;
            m.OriginalDamage = HighestGemDamage();
            m.Friendly = true;
            var p = TerraParticles.Instance;
            if (m.Ai0 != 4f)
            {
                if (m.Vel != Vector2.zero && Random.Range(0, 18) == 0 && p != null)
                {
                    int i = p.NewDust(m.Pos, m.W, m.H, 269);
                    if (i >= 0)
                    {
                        p[i].FadeIn = 0.5f;
                        p[i].Scale = 0.3f;
                        p[i].Velocity += m.Vel * 0.005f;
                    }
                }
                var light = m.Kind == Kind.Tiger1 ? Color.white * 0.5f : m.Kind == Kind.Tiger2 ? Color.white * 0.8f : Color.Lerp(FxBatch.C(255, 231, 69), Color.white, 0.8f);
                FxBatch.Instance?.Light(m.Center, light, 3f);
            }
            if (m.LocalAi0 <= 0f)
            {
                if (m.Damage != 0) m.LocalAi0 = TigerPounces(m) ? (m.Kind == Kind.Tiger2 ? 300f : m.Kind == Kind.Tiger3 ? 240f : 360f) : 10f;
            }
            else m.LocalAi0--;
            if (m.Ai0 == 4f)
            {
                // Unseen while its pounces run.
                m.Vel = Vector2.zero;
                m.Frame = big ? 11 : 9;
                if (--m.Ai1 > 0f) return;
                m.Ai0 = 0f;
                m.Ai1 = 0f;
            }
            var idle = pCenter;
            idle.x -= (15f + 10f) * pDir + 40f * m.MinionPos * pDir;
            m.FallThrough = pBox.yMax - 12f > m.Bottom;
            Npc target = null;
            if (m.Ai0 == 0f || m.Ai0 == 5f) target = FindTargetInRange(m, 800f, true);
            if (m.Ai0 == 5f)
            {
                if (target != null && InRangeOfMeOrOwner(m, target, 800f, out _, out _) && OpenBelow(target) >= 7)
                {
                    m.TileCollide = false;
                    if (m.Box.Overlaps(target.Box) && m.Vel.y >= 0f) m.Vel = new Vector2(m.Dir * 10f, -8f);
                    float bob = Mathf.Cos(Time.time * 60f / 40f * Mathf.PI * 2f);
                    if (bob > 0f) bob = -bob;
                    var top = new Vector2(target.Center.x, target.Box.yMin + bob * 40f);
                    var v = Vector2.ClampMagnitude(top - m.Center, 20f);
                    m.Vel = Vector2.MoveTowards(m.Vel, v, 4f);
                    m.Frame = big ? 10 : 8;
                    m.Rot += 0.6f * m.SpriteDir;
                    if (m.Vel.x != 0f) m.SpriteDir = m.Dir = m.Vel.x > 0f ? 1 : -1;
                }
                else
                {
                    m.Ai0 = 0f;
                    m.Ai1 = 0f;
                }
                return;
            }
            if (m.Ai0 == 1f)
            {
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
                if (dist >= 60f) Steer(ref m.Vel, d / dist * speed, 0.2f);
                if (m.Vel.x != 0f) m.SpriteDir = m.Vel.x > 0f ? 1 : -1;
                m.Frame = big ? 10 : 8;
                m.Rot += 0.6f * m.SpriteDir;
                return;
            }
            var goal = idle;
            if (target != null)
            {
                goal = target.Center;
                if (OpenBelow(target) >= 15)
                {
                    m.Ai0 = 5f;
                    m.Ai1 = 0f;
                    return;
                }
                if (m.Box.Overlaps(target.Box) && m.Vel.y >= 0f) m.Vel = new Vector2(m.Dir * 10f, -4f);
            }
            else
            {
                var d = pCenter - m.Center;
                if (d.magnitude > 2000f) m.Center = pCenter;
                else if (d.magnitude > 500f || Mathf.Abs(d.y) > 300f)
                {
                    if ((m.Vel.y > 0f && d.y < 0f) || (m.Vel.y < 0f && d.y > 0f)) m.Vel.y = 0f;
                    m.Ai0 = 1f;
                    return;
                }
                goal = IdleGoal(m, idle);
            }
            Walk067(m, goal, target, false, false, false);
        }

        /// <summary>How many open tiles there are straight down from an enemy's top, up to 15.</summary>
        static int OpenBelow(Npc n)
        {
            int i = TileOf(n.Center.x), j = TileOf(n.Box.yMin), k = 0;
            while (k < 15 && !SolidTile(i, j + k) && !PlatformTile(i, j + k)) k++;
            return k;
        }

        /// <summary>
        /// AI_067_TigerSpecialAttack: the enemies within 1600×800 of the player: the nearest to the
        /// player last, the nearest to the tiger first, others (one per gem) between, aimed ahead of
        /// their motion; a pounce (818) leaps from each to the next, 5 to 20 ticks each, one after
        /// another; the tiger waits unseen at the end.
        /// </summary>
        bool TigerPounces(M m)
        {
            var area = new Rect(pCenter.x - 800f, pCenter.y - 400f, 1600f, 800f);
            var list = new System.Collections.Generic.List<Npc>();
            foreach (var n in npcs) if (area.Overlaps(n.Box)) list.Add(n);
            if (list.Count == 0) return false;
            int count = TigerGems() + 1;
            Npc last = list[0];
            foreach (var n in list) if (Vector2.Distance(n.Center, pCenter) < Vector2.Distance(last.Center, pCenter)) last = n;
            list.Remove(last);
            Npc first = null;
            foreach (var n in list) if (first == null || Vector2.Distance(n.Center, m.Center) < Vector2.Distance(first.Center, m.Center)) first = n;
            if (first != null) list.Remove(first);
            var points = new System.Collections.Generic.List<Vector2>();
            if (first != null) points.Add(first.Center);
            int extra = 0;
            while (list.Count > 0 && points.Count < count - 1)
            {
                int i = Random.Range(0, list.Count);
                points.Add(list[i].Center + NpcVelocity(list[i]) * 4f * (extra + 1));
                list.RemoveAt(i);
                extra++;
            }
            points.Add(last.Center);
            if (SolidBox(new Rect(points[points.Count - 1] - new Vector2(m.W, m.H) / 2f, new Vector2(m.W, m.H)))) points.Add(pCenter);
            var from = m.Center;
            float each = Mathf.Min(20f, 30f / points.Count), wait = 0f;
            int damage = (int)(m.Damage * (1.5f + Mathf.Max(0, TigerGems() - 1) * 0.4f));
            foreach (var pt in points)
            {
                var to = pt;
                to.x += from.x < to.x ? 20f : -20f;
                var leap = to - from;
                int ticks = (int)Mathf.Min(each, 4f + Mathf.Ceil(leap.magnitude / 50f));
                if (ticks < 5) ticks = 5;
                TerraProjectiles.Instance?.ShootTigerPounce(TerraParticles.ToWorld(from), leap, damage, -wait * 4f, ticks * 4f);
                from = to;
                wait += ticks;
            }
            m.Center = from;
            m.Ai0 = 4f;
            m.Ai1 = wait;
            return true;
        }

        /// <summary>
        /// Main.DrawProj for 831-835: the gem's frame; the tiger standing on its feet (its spinning
        /// frame centred), lit, with up to six fading white ghosts behind it when it runs.
        /// </summary>
        static void DrawTiger(FxBatch fx, M m)
        {
            if (m.Kind == Kind.TigerGem)
            {
                fx.Draw(FxBatch.Frame("Projectile_831", 0, 12 * m.Frame, 12, 12, 6f, 6f), m.Center, Lit, 0f, 1f, overPlayer: true);
                return;
            }
            if (m.Ai0 == 4f) return;
            bool big = m.Kind != Kind.Tiger1;
            int w = big ? 64 : 54, h = big ? 44 : 36;
            bool spinning = m.Frame == (big ? 10 : 8);
            bool flip = m.SpriteDir == 1;
            var origin = spinning ? new Vector2(w / 2f, h / 2f) : new Vector2(w / 2f - (big ? 2f : 7f) * (flip ? 1f : -1f) * -1f, h - 4f);
            var at = m.Center + new Vector2(0f, m.GfxOffY + (spinning ? 0f : m.H / 2f));
            var spr = FxBatch.Frame(m.S.Texture, 0, h * m.Frame, w, h, origin.x, origin.y);
            float fast = Mathf.Clamp01(m.Vel.magnitude / 6f);
            if (fast > 0f)
                for (int k = 6; k >= 1; k--)
                {
                    var c = Color.Lerp(Lit, Color.white, fast * 0.5f) * fast * ((6 - k) / 30f);
                    c.a = 0f;
                    fx.Draw(spr, m.OldPosRing[k] + new Vector2(m.W / 2f, m.H / 2f + (spinning ? 0f : m.H / 2f)), c, m.Rot, Mathf.Lerp(1f, 1.5f, k / 15f), flipX: flip);
                }
            fx.Draw(spr, at, Lit, m.Rot, 1f, flipX: flip);
        }

        // ------------------------------------------------------------------ 864: Blade Staff (AI_169)

        /// <summary>
        /// AI_169 for 864: idle the daggers circle an ellipse 30 px over the player's head (wider the
        /// more there are), flying there at 10 px (up to 40 when far); an enemy within 800 px and it
        /// darts at it at 16 px; each hit knocks it off (a clink, sparkles, a random shove, 9 updates
        /// spinning to a stop) before the next dart. Lost or 900 px off the player: back to idle.
        /// </summary>
        void AI169(M m)
        {
            if (PlayerBuffs.Flag(m.S.Buff) && !ownerDead) m.TimeLeft = 2;
            m.TileCollide = false;
            var home = new Vector2(pCenter.x, pBox.yMin - 30f);
            if (m.Ai0 == 0f)
            {
                int index = 0, total = 0;
                foreach (var q in live)
                {
                    if (q.Dead || q.Kind != Kind.BladeStaff) continue;
                    total++;
                    if (q.Serial < m.Serial) index++;
                }
                float step = Mathf.PI * 2f / Mathf.Max(1, total), period = total * 0.66f;
                var size = new Vector2(30f, 6f) / 5f * (total - 1);
                var dir = Rotate(new Vector2(0f, 1f), step * index + (period > 0f ? Time.time % period / period : 0f) * Mathf.PI * 2f);
                home += new Vector2(dir.x * size.x, dir.y * size.y);
                home = new Vector2(Mathf.Floor(home.x), Mathf.Floor(home.y));
                var to = home - m.Center;
                float max = 10f + Mathf.Clamp01(Mathf.InverseLerp(200f, 600f, to.magnitude)) * 30f;
                if (to.magnitude >= 3000f) m.Center = home;
                m.Vel = Vector2.ClampMagnitude(to, max);
                var t = FindTargetInRange(m, 800f, false);
                if (t != null)
                {
                    m.Ai0 = 60f;
                    m.TargetId = t.Id;
                }
                float want = (m.Vel.sqrMagnitude > 0f ? Mathf.Atan2(m.Vel.y, m.Vel.x) : Mathf.PI / 2f) + Mathf.PI / 2f;
                if (to.magnitude < 40f) want = Mathf.PI;
                m.Rot = Mathf.LerpAngle(m.Rot * Mathf.Rad2Deg, want * Mathf.Rad2Deg, 0.2f) * Mathf.Deg2Rad;
                return;
            }
            if (m.Ai0 == -1f)
            {
                if (m.Ai1 == 0f)
                {
                    Sound("Dig_" + Random.Range(0, 3));
                    for (int k = 0; k < 2; k++)
                    {
                        int i = DustBox(m, 15, m.Vel * 0.2f, 0, 1.4f);
                        if (i < 0) continue;
                        var p = TerraParticles.Instance;
                        if (Random.Range(0, 3) != 0)
                        {
                            p[i].Scale *= 1.3f;
                            p[i].Velocity *= 1.1f;
                        }
                        p[i].NoGravity = true;
                        p[i].FadeIn = 0f;
                    }
                    m.Vel += Random.insideUnitCircle.normalized * 4f;
                }
                m.Ai1++;
                m.Rot += m.Vel.x * 0.1f + m.Vel.y * 0.05f;
                m.Vel *= 0.92f;
                if (m.Ai1 >= 9f)
                {
                    m.Ai0 = 0f;
                    m.Ai1 = 0f;
                }
                return;
            }
            if (!npcById.TryGetValue(m.TargetId, out var npc))
            {
                m.Ai0 = -1f;
                m.Ai1 = 0f;
            }
            else if (Vector2.Distance(pCenter, npc.Center) >= 900f)
            {
                m.Ai0 = 0f;
                m.Ai1 = 0f;
            }
            else
            {
                m.Vel = Vector2.ClampMagnitude(npc.Center - m.Center, 16f);
                float want = (m.Vel.sqrMagnitude > 0f ? Mathf.Atan2(m.Vel.y, m.Vel.x) : Mathf.PI / 2f) + Mathf.PI / 2f;
                m.Rot = Mathf.LerpAngle(m.Rot * Mathf.Rad2Deg, want * Mathf.Rad2Deg, 0.4f) * Mathf.Deg2Rad;
            }
            foreach (var q in live)
            {
                if (q == m || q.Dead || q.Kind != Kind.BladeStaff) continue;
                if (Mathf.Abs(m.Pos.x - q.Pos.x) + Mathf.Abs(m.Pos.y - q.Pos.y) >= m.W * 5f) continue;
                m.Vel.x += m.Pos.x < q.Pos.x ? -0.1f : 0.1f;
                m.Vel.y += m.Pos.y < q.Pos.y ? -0.1f : 0.1f;
            }
        }

        /// <summary>GetFloatingDaggerMinionGlowColor: one of six blue-to-violet hues by the dagger, at 70%.</summary>
        static Color DaggerGlow(M m) => FxParticles.HslToRgb(0.66f + 0.33f * (m.Serial % 6 / 6f), 0.7f, 0.6f) * 0.7f;

        /// <summary>
        /// Main.DrawProj for 864: its glow frame at its last 11 places (fading, growing to 1.4); the
        /// dagger in white; four glow copies 2 px round it, a quarter solid.
        /// </summary>
        static void DrawBladeStaff(FxBatch fx, M m)
        {
            var dagger = FxBatch.Frame("Projectile_864", 0, 0, 18, 32, 9f, 16f);
            var glowFrame = FxBatch.Frame("Projectile_864", 0, 32, 18, 32, 9f, 16f);
            var glow = DaggerGlow(m);
            glow.a *= 0.25f;
            var half = new Vector2(m.W, m.H) / 2f;
            for (int k = 11; k >= 1; k--)
                fx.Draw(glowFrame, m.OldPosRing[k] + half, glow * ((12 - k) / 90f), m.OldRot[k], Mathf.Lerp(1f, 1.4f, k / 15f));
            var at = m.Center + new Vector2(0f, m.GfxOffY);
            fx.Draw(dagger, at, Color.white, m.Rot, 1f);
            for (int k = 0; k < 4; k++)
                fx.Draw(glowFrame, at + Rotate(new Vector2(Mathf.Cos(m.Rot), Mathf.Sin(m.Rot)), Mathf.PI / 2f * k) * 2f, glow, m.Rot, 1f);
        }
    }
}
