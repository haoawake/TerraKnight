using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The other sentries (summon-hm sec-E; Terraria px, y down): AI_053 for the Queen Spider's
    /// hive (377, eggs 378 hatching baby spiders 379, AI_063), the Frost Hydra (308, frost blasts
    /// 309) and Houndius Shootius (966, fireballs 967); AI_123 for the Lunar Portal (641, its
    /// sweeping laser 642) and the Rainbow Crystal (643, its blasts 644); AI_197 for the
    /// Barnacle (1025, shots 1026). Placed by PickSentryLandingSpot: never refused, at the cursor.
    /// </summary>
    internal sealed partial class Minions
    {
        static bool IsAI053(Kind k) => k == Kind.SpiderHiver || k == Kind.FrostHydra || k == Kind.Houndius;
        static bool IsDD2(Kind k) => k >= Kind.Flameburst1 && k <= Kind.Aura3;
        /// <summary>Main.DiscoColor's hue, cycling (7 of 1530 steps a tick).</summary>
        int discoTick;
        /// <summary>Player.miscCounter: 0-299, a tick.</summary>
        int miscCounter;

        static bool IsDD2Staff(MinionKind k) => k >= MinionKind.Flameburst1 && k <= MinionKind.Aura3;

        /// <summary>
        /// ItemCheck_Shoot for the other sentry staves (Pl:48661-48684) with PickSentryLandingSpot
        /// (Pl:50043-50061): at the cursor's x; the hive, hydra and Houndius 24 px over the row above
        /// the first ground under the cursor (falling at 15 to start), the portal and the crystal
        /// 24 px over the cursor's row, the Barnacle right at the cursor.
        /// </summary>
        void SpawnLandingSentry(ItemDef staff, Vector2 cursor)
        {
            float kb = Equipment.Knockback(staff);
            int dmg = staff.Damage;
            int tx = TileOf(cursor.x), ty = TileOf(cursor.y);
            Kind kind;
            switch (staff.Minion)
            {
                case MinionKind.QueenSpider: kind = Kind.SpiderHiver; break;
                case MinionKind.FrostHydra: kind = Kind.FrostHydra; break;
                case MinionKind.Houndius: kind = Kind.Houndius; break;
                case MinionKind.LunarPortal: kind = Kind.LunarPortal; break;
                case MinionKind.RainbowCrystal: kind = Kind.RainbowCrystal; break;
                case MinionKind.Barnacle: kind = Kind.Barnacle; break;
                default: return;
            }
            var at = new Vector2(cursor.x, ty * 16f - 24f);
            if (IsAI053(kind))
            {
                int j = ty, n = 0;
                while (n++ < 200 && !GroundTile(tx, j) && !GroundTile(tx - 1, j) && !GroundTile(tx + 1, j)) j++;
                // No ground in the room under it: from the cursor, falling.
                at = n > 200 ? cursor : new Vector2(cursor.x, (j - 1) * 16f - 24f);
            }
            else if (kind == Kind.Barnacle) at = cursor;
            var m = Spawn(kind, staff, at, dmg, kb);
            if (IsAI053(kind)) m.Vel = new Vector2(0f, 15f);
            if (kind == Kind.LunarPortal || kind == Kind.RainbowCrystal)
            {
                m.Alpha = 1f;
                m.TileCollide = false;
                // The spin before the first shot uses a leftover direction in vanilla (E open point 3): the player's.
                m.Dir = pDir;
            }
        }

        // ------------------------------------------------------------------ AI_053: 377, 308, 966

        /// <summary>
        /// AI_053 (P:37943-38172): 120 ticks before the first shot; the hive and the hydra take one of
        /// five aim poses (re-aimed every 8 ticks, 4 when ready) and turn to the enemy; a shot every
        /// 60 ticks (Houndius 90) from the pose's muzzle at the nearest enemy (Manhattan < 1000) in sight.
        /// </summary>
        void AI053(M m)
        {
            bool hound = m.Kind == Kind.Houndius, hydra = m.Kind == Kind.FrostHydra;
            if (m.LocalAi0 == 0f)
            {
                m.LocalAi1 = 1f;
                m.LocalAi0 = 1f;
                m.Ai0 = 120f;
                Sound("Item_46");
                var p = TerraParticles.Instance;
                if (p != null)
                {
                    if (hydra)
                        for (int k = 0; k < 80; k++)
                        {
                            int i = p.NewDust(new Vector2(m.Pos.x, m.Pos.y + 16f), m.W, m.H - 16, 185);
                            if (i < 0) continue;
                            p[i].Velocity *= 2f;
                            p[i].NoGravity = true;
                            p[i].Scale *= 1.15f;
                        }
                    else if (hound)
                    {
                        m.TargetId = -1;
                        m.Frame = 0;
                        for (int k = 0; k < 30; k++)
                        {
                            int i = p.NewDust(m.Center - new Vector2(25f, 30f), 50, 60, 219);
                            if (i < 0) continue;
                            p[i].Velocity *= 2f;
                            p[i].NoGravity = true;
                            p[i].Scale *= 0.5f;
                        }
                    }
                    else
                    {
                        m.Frame = 4;
                        for (int k = 0; k < 40; k++)
                        {
                            int i = p.NewDust(new Vector2(m.Pos.x, m.Pos.y + 16f), m.W, m.H - 16, 171, 0f, 0f, 100);
                            if (i < 0) continue;
                            p[i].Scale = Random.Range(1, 10) * 0.1f;
                            p[i].NoGravity = true;
                            p[i].FadeIn = 1.5f;
                            p[i].Velocity *= 0.75f;
                        }
                    }
                }
            }
            m.Vel.x = 0f;
            m.Vel.y = Mathf.Min(m.Vel.y + 0.2f, 16f);
            m.TileCollide = true;
            m.FallThrough = false;
            var off = hound ? new Vector2(0f, -16f) : Vector2.zero;
            var t = SentryNpcTarget(m.Center + off, 1000f);
            if (t != null)
            {
                if (hound) m.TargetId = t.Id;
                float x = t.Center.x - m.Center.x, y = t.Center.y - m.Center.y;
                int n = 0;
                if (!hound)
                {
                    if (m.FrameCounter > 0) m.FrameCounter--;
                    if (m.FrameCounter <= 0)
                    {
                        int frame0 = m.Frame, dir0 = m.SpriteDir;
                        m.SpriteDir = x < 0f ? -1 : 1;
                        float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
                        n = y > 0f ? 0 : ay > ax * 3f ? 4 : ay > ax * 2f ? 3 : ax > ay * 3f ? 0 : ax > ay * 2f ? 1 : 2;
                        m.Frame = hydra ? n * 2 : n;
                        if (hydra && m.Ai0 > 40f && m.LocalAi1 == 0f) m.Frame++;
                        if (m.Frame != frame0 || m.SpriteDir != dir0) m.FrameCounter = m.Ai0 <= 0f ? 4 : 8;
                    }
                }
                if (m.Ai0 <= 0f)
                {
                    m.Ai0 = hound ? 90f : 60f;
                    m.LocalAi1 = 0f;
                    if (!hound)
                    {
                        int sd = m.SpriteDir;
                        off += n == 0 ? new Vector2(24f * sd, 12f) : n == 1 ? new Vector2(24f * sd, 0f) : n == 2 ? new Vector2(24f * sd, -2f) : n == 3 ? new Vector2(14f * sd, -6f) : new Vector2(2f * sd, -14f);
                        if (sd < 0) off.x += 10f;
                    }
                    var from = m.Center + off;
                    var dir = (t.Center - from).sqrMagnitude > 0f ? (t.Center - from).normalized : Vector2.up;
                    if (m.Kind == Kind.SpiderHiver)
                    {
                        var egg = Spawn(Kind.SpiderEgg, null, from, m.Damage, m.Knockback);
                        egg.Damage = m.Damage;
                        egg.Vel = dir * 9f;
                        egg.Scale = 0.9f;
                        egg.TimeLeft = 60;
                    }
                    else
                    {
                        var v = dir * (hound ? 12.5f : 9f);
                        TerraProjectiles.Instance?.ShootMinion(hound ? ProjKind.HoundiusFireball : ProjKind.FrostBlast, TerraParticles.ToWorld(from), new Vector2(v.x, -v.y), m.Damage, m.Knockback);
                    }
                }
            }
            else if (hound) m.TargetId = -1;
            else if (m.Ai0 <= 60f && (m.Frame == 1 || m.Frame == 3 || m.Frame == 5 || m.Frame == 7 || m.Frame == 9)) m.Frame--;
            if (m.Ai0 > 0f) m.Ai0--;
        }

        /// <summary>
        /// AI_053_HandleSentryNPCTargeting (P:40921-40971): from a point, the owner's target if
        /// within a Manhattan distance and in sight, else the nearest (Manhattan) such enemy.
        /// </summary>
        Npc SentryNpcTarget(Vector2 from, float range)
        {
            var o = OwnerTarget;
            if (o != null && Manhattan(from, o.Center) < range && ClearLine(from, o.Center)) return o;
            Npc best = null;
            float bestD = range;
            foreach (var n in npcs)
            {
                float d = Manhattan(from, n.Center);
                if (d >= bestD || !ClearLine(from, n.Center)) continue;
                bestD = d;
                best = n;
            }
            return best;
        }

        /// <summary>The spider egg (378, aiStyle 14 branch P:25365-25400): flies straight for 10 ticks, then falls and rolls; it breaks on an enemy, at rest, or after 60 ticks.</summary>
        void AISpiderEgg(M m)
        {
            if (m.Ai1 == 0f)
            {
                m.Ai1 = 1f;
                Sound("Item_17");
            }
            foreach (var n in npcs)
                if (n.Box.Overlaps(m.Box))
                {
                    Kill(m);
                    return;
                }
            if (++m.Ai0 > 10f)
            {
                m.Ai0 = 90f;
                if (m.Vel.y == 0f && m.Vel.x != 0f)
                {
                    m.Vel.x *= 0.96f;
                    if (Mathf.Abs(m.Vel.x) < 0.01f)
                    {
                        Kill(m);
                        return;
                    }
                }
                m.Vel.y += 0.2f;
            }
            m.Rot += m.Vel.x * 0.1f;
            m.Vel.y = Mathf.Min(m.Vel.y, 16f);
            m.TileCollide = true;
            m.FallThrough = false;
        }

        /// <summary>P:75739-75749, 78969-78984: ten bits of shell, and two to four baby spiders (five sometimes) with its speed, give or take a fifth.</summary>
        void HatchEgg(M m)
        {
            for (int k = 0; k < 10; k++)
            {
                int i = DustBox(m, 30, Vector2.zero, 0, 1f);
                if (i >= 0 && Random.Range(0, 2) == 0) TerraParticles.Instance[i].NoGravity = true;
            }
            int count = Random.Range(2, 4) + (Random.Range(0, 5) == 0 ? 1 : 0);
            for (int k = 0; k < count; k++)
            {
                var b = Spawn(Kind.BabySpider, null, m.Center, m.Damage, m.Knockback);
                b.Damage = m.Damage;
                b.Vel = new Vector2(m.Vel.x * (1f + Random.Range(-20, 21) * 0.01f), m.Vel.y * (1f + Random.Range(-20, 21) * 0.01f));
                b.TimeLeft = 300;
            }
        }

        /// <summary>
        /// AI_063 for the baby spider (P:29796-29896): after the nearest enemy in sight (2000 px),
        /// jumping at one 50 px above it, dropping through platforms to a lower one; faster in the air.
        /// </summary>
        void AI063(M m)
        {
            Npc target = null;
            float best = 2000f;
            foreach (var n in npcs)
            {
                float d = Vector2.Distance(m.Center, n.Center);
                if (d >= best || !ClearLine(m.Center, n.Center)) continue;
                best = d;
                target = n;
            }
            m.TileCollide = true;
            if (target == null)
            {
                m.Vel.x *= 0.95f;
                m.FallThrough = false;
            }
            else
            {
                m.FallThrough = target.Center.y > m.Bottom;
                float max = 5f, acc = 0.08f;
                if (m.Vel.y == 0f)
                {
                    if (m.Center.y - 50f > target.Center.y) m.Vel.y = -6f;
                }
                else
                {
                    max = 8f;
                    acc = 0.12f;
                }
                m.Vel.x = Mathf.Clamp(m.Vel.x + Mathf.Sign(target.Center.x - m.Center.x) * acc, -max, max);
            }
            StepUp(m, m.FallThrough);
            if (m.Vel.y != 0f) m.Frame = 3;
            else
            {
                if (Mathf.Abs(m.Vel.x) > 0.2f) m.FrameCounter++;
                if (m.FrameCounter >= 9) m.FrameCounter = 0;
                m.Frame = m.FrameCounter >= 6 ? 2 : m.FrameCounter >= 3 ? 1 : 0;
            }
            if (m.Vel.x != 0f) m.Dir = m.Vel.x < 0f ? -1 : 1;
            m.SpriteDir = -m.Dir;
            m.Vel.y = Mathf.Min(m.Vel.y + 0.2f, 16f);
        }

        /// <summary>The egg and the babies bounce (P:19850-19858, 18566-18576): off walls (egg −0.5, babies −0.6), and off the floor when falling fast.</summary>
        void BounceSpiders(M m, Vector2 before)
        {
            bool egg = m.Kind == Kind.SpiderEgg;
            if (m.Vel.x == 0f && before.x != 0f) m.Vel.x = before.x * (egg ? -0.5f : -0.6f);
            if (m.Vel.y == 0f && before.y > (egg ? 1f : 2f)) m.Vel.y = before.y * (egg ? -0.5f : -0.6f);
        }

        // ------------------------------------------------------------------ AI_123: 641, 643

        /// <summary>
        /// AI_123 (P:37726-37941): fades in; the portal spins (a turn in 2 s) shedding green and dark
        /// dust; the crystal glitters. An enemy within 1000 px in sight (the owner's first): the
        /// portal fires a sweeping laser 9 ticks later, the crystal three blasts 4 ticks later, then
        /// 20 ticks' rest.
        /// </summary>
        void AI123(M m)
        {
            bool portal = m.Kind == Kind.LunarPortal;
            m.Vel = Vector2.zero;
            m.TileCollide = false;
            m.Alpha = Mathf.Max(0f, m.Alpha - 5f / 255f);
            var p = TerraParticles.Instance;
            if (portal)
            {
                m.Rot -= m.Dir * Mathf.PI * 2f / 120f;
                m.Scale = 1f - m.Alpha;
                if (p != null)
                {
                    if (Random.Range(0, 2) == 0)
                    {
                        var v = Rotate(new Vector2(0f, 1f), Random.Range(-Mathf.PI, Mathf.PI));
                        int i = p.NewDust(m.Center - v * Random.Range(10, 21), 0, 0, 229);
                        if (i >= 0)
                        {
                            p[i].Position = m.Center - v * Random.Range(10, 21);
                            p[i].NoGravity = true;
                            p[i].Velocity = Rotate(v, Mathf.PI / 2f) * 6f;
                            p[i].Scale = 0.5f + Random.value;
                            p[i].FadeIn = 0.5f;
                        }
                    }
                    if (Random.Range(0, 2) == 0)
                    {
                        var v = Rotate(new Vector2(0f, 1f), Random.Range(-Mathf.PI, Mathf.PI));
                        int i = p.NewDust(m.Center - v * 30f, 0, 0, 240);
                        if (i >= 0)
                        {
                            p[i].Position = m.Center - v * 30f;
                            p[i].NoGravity = true;
                            p[i].Velocity = Rotate(v, -Mathf.PI / 2f) * 3f;
                            p[i].Scale = 0.5f + Random.value;
                            p[i].FadeIn = 0.5f;
                        }
                    }
                    if (m.Ai0 < 0f)
                    {
                        int i = p.NewDust(m.Center - new Vector2(8f, 8f), 16, 16, 229);
                        if (i >= 0)
                        {
                            p[i].Velocity *= 2f;
                            p[i].NoGravity = true;
                            p[i].Scale = Random.Range(0, 2) == 0 ? 0.8f : 1.65f;
                        }
                    }
                }
            }
            else
            {
                if (++m.FrameCounter >= 3)
                {
                    m.FrameCounter = 0;
                    m.Frame = (m.Frame + 1) % 8;
                }
                if (p != null && m.Alpha == 0f && Random.Range(0, 15) == 0)
                {
                    var at = m.Center + Rotate(new Vector2(0f, 1f), Random.Range(-Mathf.PI, Mathf.PI)) * (26f + 4f * Random.value);
                    int i = p.NewDust(at, 0, 0, 261, 0f, 0f, 100);
                    if (i >= 0)
                    {
                        p[i].Velocity.x = 0f;
                        p[i].NoGravity = true;
                        p[i].FadeIn = 1f;
                        p[i].Scale = 0.5f;
                    }
                }
                m.LocalAi0 = (m.LocalAi0 + 1f) % 60f;
                m.LocalAi1++;
            }
            if (m.Ai0 < 0f)
            {
                m.Ai0++;
                return;
            }
            if (m.Ai0 == 0f)
            {
                var t = PortalTarget(m);
                if (t != null)
                {
                    m.Ai0 = 1f;
                    m.TargetId = t.Id;
                }
                return;
            }
            if (!npcById.TryGetValue(m.TargetId, out var npc))
            {
                m.Ai0 = 0f;
                m.TargetId = -1;
                return;
            }
            if (++m.Ai0 < (portal ? 10f : 5f)) return;
            var d = (npc.Center - m.Center).sqrMagnitude > 0f ? (npc.Center - m.Center).normalized : Vector2.up;
            int side = d.x > 0f ? 1 : -1;
            m.Dir = side;
            m.Ai0 = -20f;
            if (portal)
            {
                var laser = Spawn(Kind.PortalLaser, null, m.Center, m.Damage, m.Knockback);
                laser.Damage = m.Damage;
                laser.Leader = m;
                laser.Ai0 = Mathf.Atan2(d.y, d.x) + side * Mathf.PI / 6f;
                laser.TileCollide = false;
                laser.TimeLeft = 60;
                return;
            }
            var box = npc.Box;
            var off = new Vector2(box.x + box.width * Random.value, box.y + box.height * Random.value) - m.Center;
            var lead = NpcVelocity(npc) * 30f;
            float hue = (discoTick * 7 % 1530) / 1530f;
            for (int j = 0; j < 3; j++)
            {
                var at = m.Center + off + lead;
                float k = Mathf.LerpUnclamped(0.1f, 0.75f, (Vector2.Distance(m.Center, at) - 800f) / (200f - 800f));
                if (j > 0) at = m.Center + Rotate(off, Random.Range(-Mathf.PI / 4f, Mathf.PI / 4f)) * (Random.value * k + 0.5f);
                var b = Spawn(Kind.CrystalBlast, null, at, m.Damage, m.Knockback);
                b.Damage = m.Damage;
                b.Leader = m;
                b.Ai0 = hue;
                b.Alpha = 1f;
                b.TileCollide = false;
                b.TimeLeft = 120;
            }
        }

        /// <summary>AI_123's search: the owner's target, else the nearest enemy (Euclidean) within 1000 px, in a clear line.</summary>
        Npc PortalTarget(M m)
        {
            var o = OwnerTarget;
            if (o != null && Vector2.Distance(m.Center, o.Center) < 1000f && ClearLine(m.Center, o.Center)) return o;
            Npc best = null;
            float bestD = 1000f;
            foreach (var n in npcs)
            {
                float d = Vector2.Distance(m.Center, n.Center);
                if (d >= bestD || !ClearLine(m.Center, n.Center)) continue;
                bestD = d;
                best = n;
            }
            return best;
        }

        /// <summary>
        /// The portal's laser (642, aiStyle 84, P:31931-32326): on the portal, turning 60° over its
        /// 50 ticks through the enemy, as long as the way to the first block (2400 at most);
        /// sparks at its end. Hits along its line, 30 px thick, each enemy every 10 ticks.
        /// </summary>
        void AIPortalLaser(M m)
        {
            var portal = m.Leader;
            if (portal == null || portal.Dead)
            {
                Kill(m);
                return;
            }
            m.TileCollide = false;
            m.Vel = Vector2.zero;
            m.Center = portal.Center;
            m.Ai0 -= portal.Dir * (Mathf.PI / 3f) / 50f;
            if (++m.LocalAi0 >= 50f)
            {
                Kill(m);
                return;
            }
            m.Scale = Mathf.Min(Mathf.Sin(m.LocalAi0 * Mathf.PI / 50f) * 10f, 1f);
            var dir = new Vector2(Mathf.Cos(m.Ai0), Mathf.Sin(m.Ai0));
            m.Dir = dir.x < 0f ? -1 : 1;
            m.LocalAi1 = Mathf.Lerp(m.LocalAi1, RayToBlock(m.Center, dir, 2400f), 0.5f);
            var end = m.Center + dir * (m.LocalAi1 - 14f);
            var p = TerraParticles.Instance;
            if (p == null) return;
            for (int k = 0; k < 2; k++)
            {
                float a = Mathf.Atan2(dir.y, dir.x) + (Random.Range(0, 2) == 0 ? -1f : 1f) * Mathf.PI / 2f;
                int i = p.NewDust(end, 0, 0, 229);
                if (i < 0) continue;
                p[i].Position = end;
                p[i].Velocity = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (2f + Random.value * 2f);
                p[i].NoGravity = true;
                p[i].Scale = 1.7f;
            }
            if (Random.Range(0, 5) == 0)
            {
                var perp = new Vector2(-dir.y, dir.x);
                int i = p.NewDust(end + perp * (Random.value - 0.5f) * 18f - new Vector2(4f, 4f), 8, 8, 31, 0f, 0f, 100, 1.5f);
                if (i >= 0)
                {
                    p[i].Velocity *= 0.5f;
                    p[i].Velocity.y = -Mathf.Abs(p[i].Velocity.y);
                }
            }
        }

        /// <summary>Collision.LaserScan, here: the way from a point along a direction to the first block (px), at most max.</summary>
        static float RayToBlock(Vector2 from, Vector2 dir, float max)
        {
            for (float d = 8f; d < max; d += 8f)
            {
                var at = from + dir * d;
                if (SolidTile(TileOf(at.x), TileOf(at.y))) return d;
            }
            return max;
        }

        /// <summary>CheckAABBvLineCollision for the laser (P:16030-16040): every enemy its 30 px thick line crosses, each every 10 ticks.</summary>
        void LaserDamage(M m)
        {
            var melee = TerraMod.Instance?.Melee;
            if (melee == null || m.LocalAi1 <= 0f) return;
            var dir = new Vector2(Mathf.Cos(m.Ai0), Mathf.Sin(m.Ai0));
            var a = m.Center;
            var b = m.Center + dir * m.LocalAi1;
            float half = 15f * m.Scale;
            foreach (var n in npcs)
            {
                if (n.Hm == null || n.Hm.isDead || m.Immune.ContainsKey(n.Id)) continue;
                var box = new Rect(n.Box.x - half, n.Box.y - half, n.Box.width + half * 2f, n.Box.height + half * 2f);
                if (!SegmentHitsRect(a, b, box)) continue;
                int dmg = SummonTags.Modify(n.Hm, m.Damage, out bool crit, 1f, true, m.Center, m.ItemTag);
                melee.HitWith(n.Col, dmg, m.Knockback, crit ? 100f : 0f, m.Dir, TerraParticles.ToWorld(m.Center), Equipment.Current.ArmorPen + m.ItemPen, autonomous: true);
                DevHits++;
                DevHitDamage += dmg;
                SummonTags.OnHit(n.Hm, m.Damage, m.Center);
                m.Immune[n.Id] = 10;
            }
        }

        /// <summary>Whether a segment touches a box (Liang-Barsky).</summary>
        static bool SegmentHitsRect(Vector2 a, Vector2 b, Rect r)
        {
            float t0 = 0f, t1 = 1f;
            var d = b - a;
            bool Clip(float p, float q)
            {
                if (p == 0f) return q >= 0f;
                float t = q / p;
                if (p < 0f)
                {
                    if (t > t1) return false;
                    if (t > t0) t0 = t;
                }
                else
                {
                    if (t < t0) return false;
                    if (t < t1) t1 = t;
                }
                return true;
            }
            return Clip(-d.x, a.x - r.xMin) && Clip(d.x, r.xMax - a.x) && Clip(-d.y, a.y - r.yMin) && Clip(d.y, r.yMax - a.y);
        }

        /// <summary>
        /// The crystal's blast (644, aiStyle 112 branch P:34867-34997): fades in (8 a tick), shedding
        /// coloured sparks; it explodes on tick 30 and again as it dies on tick 60, a 60 px box each
        /// time, whatever its immunity says; gone with its crystal.
        /// </summary>
        void AICrystalBlast(M m)
        {
            m.TileCollide = false;
            m.Vel = Vector2.zero;
            if (m.LocalAi0 == 0f)
            {
                m.LocalAi0 = 0.8f + 0.8f * Random.value;
                m.Dir = Random.Range(0, 2) == 0 ? -1 : 1;
            }
            if (m.Leader != null && m.Leader.Dead) m.Leader = null;
            m.Rot = m.LocalAi1 / 40f * Mathf.PI * 2f * m.Dir;
            m.Alpha = Mathf.Max(0f, m.Alpha - 8f / 255f);
            float opacity = 1f - m.Alpha;
            var col = FxParticles.HslToRgb(m.Ai0, 1f, 0.5f);
            var p = TerraParticles.Instance;
            if (p != null)
            {
                for (int k = 0; k < 2; k++)
                {
                    if (Random.Range(0, 10) != 0) continue;
                    int i = p.NewDust(m.Center, 0, 0, 267, 0f, 0f, 225);
                    if (i < 0) continue;
                    p[i].Position = m.Center;
                    p[i].Color = col;
                    p[i].NoGravity = true;
                    p[i].Scale = opacity * m.LocalAi0;
                    p[i].Velocity = Rotate(new Vector2(0f, 1f), k * Mathf.PI + m.Rot) * 2.5f;
                }
                for (int k = 0; k < 2; k++)
                {
                    if (Random.Range(0, 10) != 0) continue;
                    int i = p.NewDust(m.Center, 0, 0, 267, 0f, 0f, 225);
                    if (i < 0) continue;
                    p[i].Position = m.Center;
                    p[i].Color = col;
                    p[i].NoGravity = true;
                    p[i].Scale = opacity * m.LocalAi0;
                    p[i].Velocity = new Vector2(0f, k == 0 ? 2.5f : -2.5f);
                }
                if (Random.Range(0, 10) == 0)
                {
                    var at = m.Center + Random.insideUnitCircle.normalized * (20f + 100f * Random.value);
                    if (!SolidTile(TileOf(at.x), TileOf(at.y)))
                    {
                        int i = p.NewDust(at, 0, 0, 267, 0f, 0f, 127);
                        if (i >= 0)
                        {
                            p[i].Position = at;
                            p[i].Color = col;
                            p[i].Velocity = new Vector2(0f, -(1f + 2f * Random.value) * (1.6f + 0.9f * Random.value));
                            p[i].FadeIn = 1f + Random.value;
                            p[i].Scale = 1f + Random.value;
                            int w = p.Clone(i);
                            if (w >= 0)
                            {
                                p[w].Scale *= 0.65f;
                                p[w].FadeIn *= 0.65f;
                                p[w].Color = Color.white;
                            }
                        }
                    }
                }
            }
            m.Scale = opacity / 2f * m.LocalAi0;
            m.LocalAi1++;
            if (m.LocalAi1 >= 60f) Kill(m);
            else if (m.LocalAi1 == 30f) CrystalExplode(m);
        }

        /// <summary>DoRainbowCrystalStaffExplosion (P:80317-80361) and the blast's hit: two rings of coloured sparks, everything in the 60 px box round it.</summary>
        void CrystalExplode(M m)
        {
            var col = FxParticles.HslToRgb(m.Ai0, 1f, 0.5f);
            var p = TerraParticles.Instance;
            if (p != null)
            {
                int n = Random.Range(7, 13);
                var sp = Rotate(new Vector2(0f, -3f), Random.Range(-Mathf.PI, Mathf.PI));
                for (int ring = 0; ring < 2; ring++)
                    for (int k = 0; k < n; k++)
                    {
                        var v = Rotate(sp, Mathf.PI * 2f * k / n);
                        v = new Vector2(v.x * 2.1f, v.y * 2f) * (0.8f + 0.4f * Random.value);
                        if (ring == 1) v *= Random.value * 0.8f;
                        int i = p.NewDust(m.Center, 0, 0, 267);
                        if (i < 0) continue;
                        p[i].Position = m.Center;
                        p[i].Velocity = v;
                        p[i].Color = col;
                        p[i].NoGravity = true;
                        p[i].Scale = ring == 0 ? 2f : Random.value;
                        p[i].FadeIn = ring == 0 ? 2f * Random.value : 0f;
                        int w = p.Clone(i);
                        if (w >= 0)
                        {
                            p[w].Scale *= 0.5f;
                            p[w].FadeIn *= 0.5f;
                            p[w].Color = Color.white;
                        }
                    }
            }
            var melee = TerraMod.Instance?.Melee;
            if (melee == null) return;
            var box = new Rect(m.Center.x - 30f, m.Center.y - 30f, 60f, 60f);
            foreach (var npc in npcs)
            {
                if (npc.Hm == null || npc.Hm.isDead || !npc.Box.Overlaps(box)) continue;
                int dmg = SummonTags.Modify(npc.Hm, m.Damage, out bool crit, 1f, true, m.Center, m.ItemTag);
                melee.HitWith(npc.Col, dmg, m.Knockback, crit ? 100f : 0f, m.Dir, TerraParticles.ToWorld(m.Center), Equipment.Current.ArmorPen + m.ItemPen, autonomous: true);
                DevHits++;
                DevHitDamage += dmg;
                SummonTags.OnHit(npc.Hm, m.Damage, m.Center);
            }
        }

        // ------------------------------------------------------------------ AI_197: 1025 Barnacle

        /// <summary>
        /// AI_197 (P:41034-41185): held up by three balloons it rises (0.1 a tick, at most 12) while
        /// there is ground within 15 tiles under it, and hovers where there is none; it sticks to a
        /// ceiling it bumps (the balloons pop) and lets go if the ceiling goes. A shot every 50 ticks
        /// at the nearest enemy (Manhattan < 1240) in sight of a point 10 px under it.
        /// </summary>
        void AI197(M m)
        {
            var p = TerraParticles.Instance;
            if (m.LocalAi1 == 0f)
            {
                m.LocalAi1 = 1f;
                m.Frame = 3;
                if (p != null)
                    for (int k = 0; k < 40; k++)
                    {
                        int i = p.NewDust(m.Center - new Vector2(10f, 10f), 20, 20, 2, 0f, 0f, 100);
                        if (i < 0) continue;
                        p[i].Velocity *= 2f;
                        p[i].NoGravity = true;
                    }
            }
            bool ceiling = SolidBox(m.Pos.x + m.Vel.x, m.Pos.y + m.Vel.y - 2f, m.W, m.H);
            bool ground = false;
            int cx = TileOf(m.Center.x), top = TileOf(m.Pos.y);
            for (int j = 0; j < 15 && !ground; j++) ground = SolidTile(cx, top + j);
            if (m.Ai0 == 1f && !ceiling) m.Ai0 = 0f;
            if (m.Ai0 == 1f)
            {
                if (m.Frame < 0 || m.Frame > 2) m.Frame = Random.Range(0, 3);
            }
            else m.Frame = 3;
            if (ground)
            {
                if (m.Ai0 == 1f) m.Vel = Vector2.zero;
                else m.Vel.y = Mathf.Max(m.Vel.y - 0.1f, -12f);
            }
            else
            {
                m.Vel *= 0.9f;
                if (m.Vel.magnitude < 0.01f) m.Vel = Vector2.zero;
            }
            m.TileCollide = true;
            m.FallThrough = true;
            var off = new Vector2(0f, 10f);
            var t = SentryNpcTarget(m.Center + off, 1240f);
            if (++m.LocalAi0 >= 50f)
            {
                if (t == null) m.LocalAi0 = 50f;
                else
                {
                    m.LocalAi0 = 0f;
                    var from = m.Center + off;
                    var v = ((t.Center - from).sqrMagnitude > 0f ? (t.Center - from).normalized : Vector2.up) * 12f;
                    TerraProjectiles.Instance?.ShootMinion(ProjKind.BarnacleShot, TerraParticles.ToWorld(from), new Vector2(v.x, -v.y), m.Damage, m.Knockback);
                }
            }
        }

        /// <summary>AI_197_HandleTileCollision (P:40973-41032): it catches on what it bumped: a dig, Item167, a spray of green, and the balloons pop.</summary>
        void BarnacleAttach(M m)
        {
            Sound("Dig_" + Random.Range(0, 3));
            Sound("Item_167");
            m.Ai0 = 1f;
            var p = TerraParticles.Instance;
            if (p == null) return;
            for (int k = 0; k < 30; k++)
            {
                int i = p.NewDust(m.Pos + new Vector2(0f, -22f), m.W, 28, 2, m.Vel.x, m.Vel.y, 50, 1.4f + Random.Range(-20, 40) * 0.01f);
                if (i < 0) continue;
                p[i].FadeIn = 0.5f;
                if (Random.Range(0, 3) != 0) p[i].NoGravity = true;
                else p[i].Scale *= 0.75f;
                p[i].Velocity = Random.insideUnitCircle * 5f + new Vector2(0f, -2f);
            }
            var top = new Vector2(m.Center.x, m.Pos.y - 62f);
            var spots = new[] { new Vector2(-20f, 6f), Vector2.zero, new Vector2(20f, 6f) };
            int[] kinds = { 1143, 1137, 1128 };
            for (int b = 0; b < 3; b++)
                for (int k = 0; k < 2; k++)
                {
                    var v = Random.insideUnitCircle * 2f;
                    v.y -= Random.value;
                    p.NewGore(top + spots[b], v * 2f, kinds[b] + Random.Range(0, 3), 1f, 1f, default, true);
                }
            p.NewGore(top, Random.insideUnitCircle, 1146, 1f, 0.5f, default, true);
        }

        // ------------------------------------------------------------------ drawing

        /// <summary>DrawProjDirect's framed branch (E.0.9) for the two-column hive and hydra sheets.</summary>
        void DrawAI053(FxBatch fx, M m)
        {
            if (m.Kind == Kind.Houndius)
            {
                DrawHoundius(fx, m);
                return;
            }
            bool hydra = m.Kind == Kind.FrostHydra;
            int fw = hydra ? 94 : 66, fh = hydra ? 78 : 50;
            float half = fw / 2f, oy = hydra ? 37f : 19f, dy = hydra ? 37f : 25f;
            // Carried on a sling: the sheet's second column (M:34997-35004).
            var spr = FxBatch.Frame(m.S.Texture, m.Carried ? fw : 0, fh * m.Frame, fw, fh - 1, half, oy);
            var color = hydra ? new Color(200f / 255f, 200f / 255f, 1f, 125f / 255f) : Lit;
            fx.Draw(spr, new Vector2(m.Pos.x + half, m.Pos.y + dy + m.GfxOffY), color, 0f, 1f, flipX: m.SpriteDir == -1);
            if (hydra) fx.Light(m.Center, new Color(0.025f, 0.175f, 0.25f) * 4f, 3f);
        }

        /// <summary>M:35076-35124: the golden base, the eye bobbing over it, and the pupil looking at its target (the player when idle).</summary>
        void DrawHoundius(FxBatch fx, M m)
        {
            var pos = new Vector2(m.Pos.x + 15f - 6f, m.Pos.y + 30f + m.GfxOffY);
            // Carried on a sling, only the eye shows (M:35076-35095).
            if (!m.Carried) fx.Draw(FxBatch.Frame("Projectile_966", 0, 0, 30, 39, 15f, 4f), pos, Lit, 0f, 1f);
            var eye = new Vector2(0f, -30f + (-1f + 2f * Mathf.Cos(mouseTextColor / 255f * Mathf.PI * 2f * 2f)));
            fx.Draw(FxBatch.Frame("Projectile_966", 0, 40, 30, 39, 15f, 4f), pos + eye, Lit, 0f, 1f);
            var look = m.Center + eye;
            var aim = m.TargetId >= 0 && npcById.TryGetValue(m.TargetId, out var t) ? t.Center : pCenter;
            var pupil = new Vector2(-1f, -1f) + ((aim - look).sqrMagnitude > 0f ? (aim - look).normalized : Vector2.zero) * 3f;
            fx.Draw(FxBatch.Frame("Projectile_966", 0, 80, 30, 39, 15f, 4f), pos + eye + pupil, Lit, 0f, 1f);
        }

        static void DrawSpiderEgg(FxBatch fx, M m) =>
            fx.Draw(FxBatch.Frame("Projectile_378", 0, 0, 24, 26, 12f, 13f), m.Center, Lit, m.Rot, 0.9f);

        static void DrawBabySpider(FxBatch fx, M m) =>
            fx.Draw(FxBatch.Frame("Projectile_379", 0, 14 * m.Frame, 24, 13, 12f, 5f), new Vector2(m.Pos.x + 12f, m.Pos.y + 5f + m.GfxOffY), Lit, 0f, 1f, flipX: m.SpriteDir == -1);

        /// <summary>M:33492-33520: dark and green swirls turning against each other, fading in; the last one added.</summary>
        static void DrawLunarPortal(FxBatch fx, M m)
        {
            float a = 1f - m.Alpha, r = m.Rot, s = m.Scale;
            var c = new Color(a, a, a, a);
            float k = 0.95f + Mathf.Sin(0.75f * r) * 0.1f;
            var c95 = new Color(a * 0.5f * k, a * 0.5f * k, a * 0.5f * k, a * k);
            var c94 = new Color(a * 0.8f, a * 0.8f, a * 0.8f, a * 0.4f);
            var dark = FxBatch.Frame("Extra_50", 0, 0, 60, 60, 30f, 30f);
            var green = FxBatch.Frame("Projectile_641", 0, 0, 60, 60, 30f, 30f);
            var at = m.Center + new Vector2(0f, m.GfxOffY);
            fx.Draw(dark, at, c95, -r + 0.35f, 0.6f + s * 0.6f * k, flipX: true);
            fx.Draw(dark, at, c, -r, s, flipX: true);
            fx.Draw(green, at, c94, -0.7f * r, s, flipX: true);
            fx.Draw(dark, at, c * 0.8f, 0.5f * r, s * 0.9f);
            fx.Draw(green, at, new Color(a, a, a, 0f), r, s);
            fx.Light(m.Center, new Color(0.3f, 0.9f, 0.7f) * a);
        }

        /// <summary>Utils.DrawLaser with TurretLaserDraw (M:32890-32909): head, scrolling body (every 2 ticks), end cap; half added.</summary>
        void DrawPortalLaser(FxBatch fx, M m)
        {
            if (m.LocalAi1 <= 0f) return;
            var dir = new Vector2(Mathf.Cos(m.Ai0), Mathf.Sin(m.Ai0));
            float s = m.Scale, len = m.LocalAi1 - s * 10.5f, rot = m.Ai0 - Mathf.PI / 2f;
            var c = new Color(1f, 1f, 1f, 0.5f);
            var start = new Vector2(Mathf.Floor(m.Center.x), Mathf.Floor(m.Center.y));
            fx.Draw(FxBatch.Frame("Projectile_642", 0, 0, 22, 20, 11f, 10f), start, c, rot, s);
            float d = 10f * s, end = len - 30f * s;
            int seg = 54000 - discoTick / 2;
            while (d < end)
            {
                float step = Mathf.Min(19f * s, end - d);
                int row = 22 * (seg++ % 5 + 1);
                fx.Draw(FxBatch.Frame("Projectile_642", 0, row, 22, Mathf.Max(1, Mathf.RoundToInt(step / Mathf.Max(s, 0.01f))), 11f, 0f), start + dir * d, c, rot, s);
                d += step;
            }
            fx.Draw(FxBatch.Frame("Projectile_642", 0, 132, 22, 30, 11f, 1f), start + dir * Mathf.Max(d, 10f * s), c, rot, s);
            for (float t = 0f; t < m.LocalAi1; t += 64f) fx.Light(m.Center + dir * t, new Color(0.3f, 0.65f, 0.7f), 2f);
        }

        /// <summary>M:34260, 34588-34597: the crystal, unlit and half added, fading in; four faint copies pulsing 5-7 px round it.</summary>
        static void DrawRainbowCrystal(FxBatch fx, M m)
        {
            float a = 1f - m.Alpha;
            var c = new Color(a, a, a, (127f - m.Alpha * 127.5f) / 255f);
            var spr = FxBatch.Frame("Projectile_643", 0, 50 * m.Frame, 26, 50, 13f, 25f);
            var at = m.Center + new Vector2(0f, m.GfxOffY);
            fx.Draw(spr, at, c, 0f, 1f);
            float r = Mathf.Cos(Mathf.PI * 2f * m.LocalAi0 / 60f) + 6f;
            for (int k = 0; k < 4; k++) fx.Draw(spr, at + Rotate(new Vector2(0f, 1f), k * Mathf.PI / 2f) * r, c * 0.2f, 0f, 1f);
        }

        /// <summary>M:34232-34236, 34260, 34493-34496: a coloured flare twice its size (turning and still), a beam back to the crystal after the blast, the white flare.</summary>
        static void DrawCrystalBlast(FxBatch fx, M m)
        {
            float op = 1f - m.Alpha;
            var col = FxParticles.HslToRgb(m.Ai0, 1f, 0.5f);
            var add = new Color(col.r, col.g, col.b, 0f);
            var spr = FxBatch.Frame("Projectile_644", 0, 0, 72, 72, 36f, 36f);
            var at = m.Center;
            fx.Draw(spr, at, add * op, m.Rot, m.Scale * 2f);
            fx.Draw(spr, at, add * op, 0f, m.Scale * 2f);
            var white = new Color(op, op, op, 0f);
            if (m.Leader != null && !m.Leader.Dead && op > 0.3f && m.LocalAi1 >= 30f)
            {
                var v = m.Leader.Center - at;
                float t = Mathf.Clamp01(Mathf.Abs(30f - m.LocalAi1) / 20f);
                if (t > 0f)
                {
                    float ang = Mathf.Atan2(v.y, v.x) + Mathf.PI / 2f;
                    fx.Draw(spr, at + v / 2f, add * t, ang, new Vector2(1f, v.magnitude / 72f));
                    fx.Draw(spr, at + v / 2f, white * t, ang, new Vector2(0.5f, v.magnitude / 144f));
                }
            }
            fx.Draw(spr, at, white, m.Rot, m.Scale);
            fx.Draw(spr, at, white, 0f, m.Scale);
            if (m.Alpha == 0f) fx.Light(at, col * 0.5f, 2f);
        }

        /// <summary>M:35007-35042: the barnacle (hanging frames, or bobbing under three balloons while it floats).</summary>
        void DrawBarnacle(FxBatch fx, M m)
        {
            float bob = m.Ai0 == 0f ? Mathf.Sin(Mathf.PI * 2f * (miscCounter % 100) / 100f) * 2f : 0f;
            if (m.Ai0 == 0f)
            {
                float t75 = miscCounter % 75 / 75f;
                int[] cols = { 2, 5, 7 };
                for (int i = 0; i < 3; i++)
                {
                    float rot = (i == 0 ? Mathf.PI / 9f : i == 2 ? -Mathf.PI / 9f : 0f) + (i == 1 ? Mathf.Sin(Mathf.PI * 2f * t75) : Mathf.Cos(Mathf.PI * 2f * t75)) * 0.025f;
                    fx.Draw(FxBatch.Frame("NPC_594", 32 * cols[i], 0, 32, 76, 16f, 76f), m.Center + new Vector2(0f, m.GfxOffY + bob - 10f), Lit, rot, 1f);
                }
            }
            if (m.Carried)
            {
                // Slung on the back, turned to hang off it (P:70173-70197).
                fx.Draw(FxBatch.Frame("Projectile_1025", 0, 62 * m.Frame, 46, 61, 23f, 41f), m.Center, Lit, m.Rot, 1f);
                return;
            }
            fx.Draw(FxBatch.Frame("Projectile_1025", 0, 62 * m.Frame, 46, 61, 23f, 41f), new Vector2(m.Pos.x + 23f, m.Pos.y + 19f + m.GfxOffY + bob), Lit, 0f, 1f);
        }
    }
}
