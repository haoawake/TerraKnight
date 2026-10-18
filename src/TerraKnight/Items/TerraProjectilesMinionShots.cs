using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The later minions' shots (Terraria's space, px y down, per update): the Sharknado's sharks
    /// (408: fading in, falling after 45 updates, a blood burst), the UFO's laser (433, AI_048:
    /// across 400 px in a tick, a green and blue line of motes), the Stardust Cell's seeds (614,
    /// AI_113: homing on their enemy and clinging to it, each one burning 20 a second while it does,
    /// ten at most on one enemy).
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        static void RegisterMinionShots()
        {
            specs[ProjKind.SharknadoShark] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_408", W = 10, H = 10, Time = 300, AlphaStart = 1f, TrailLength = 10,
                Update = (t, p) => StepSharknadoShark(p), Draw = (t, fx, p, px) => DrawSharknadoShark(fx, p, px), OnKill = (t, p) => KillSharknadoShark(p),
            };
            specs[ProjKind.UfoLaser] = new Spec
            {
                Ai = Ai.Custom, W = 8, H = 8, Time = 100, Extra = 100, Look = Look.Invisible,
                Update = (t, p) => BeamStart(p, 0), OnKill = (t, p) => UfoLine(p),
            };
            specs[ProjKind.TigerPounce] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_818", W = 132, H = 132, Time = 3600, Penetrate = -1, Extra = 3, TileCollide = false, OwnMove = true, OncePerNpc = true,
                AlphaStart = 1f, TrailLength = 20, CanDamage = p => false,
                Update = (t, p) => t.StepTigerPounce(p), Draw = (t, fx, p, px) => DrawTigerPounce(fx, p, px), OnKill = (t, p) => t.KillTigerPounce(p),
            };
            specs[ProjKind.PygmySpear] = new Spec
            {
                Ai = Ai.Arrow, Texture = "Projectile_195", W = 10, H = 10, Time = 300, Extra = 1, GravityDelay = 40, Gravity = 0.15f, TileCollide = false, PivotBack = 16f,
                OnHit = (t, p, hm) => EnemyDebuffs.Add(hm, EnemyDebuffs.Venom, 60 * Random.Range(5, 11)), OnKill = (t, p) => KillPygmySpear(p),
            };
            specs[ProjKind.StardustCellShot] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_614", W = 16, H = 16, Time = 300, Penetrate = -1, AlphaStart = 1f,
                CanDamage = p => p.LocalAi1 == 0f, OnHit = (t, p, hm) => t.StickCell(p, hm),
                Update = (t, p) => t.StepStardustCell(p), Draw = (t, fx, p, px) => DrawStardustCell(fx, p, px), OnKill = (t, p) => KillStardustCell(p),
            };
        }

        // ------------------------------------------------------------------ 408: the Sharknado's shark (AI_001)

        /// <summary>P:55961, 58347: it fades in, a gulp (NPC_Killed_19) as it sets off; after 45 updates it sags; turned along its flight.</summary>
        static void StepSharknadoShark(Proj p)
        {
            p.Alpha = Mathf.Max(0f, p.Alpha - 40f / 255f);
            if (p.Updates == 1) Sound("NPC_Killed_19");
            if (++p.Ai0 >= 45f)
            {
                p.Ai0 = 45f;
                p.Vel.y -= 0.05f;
            }
            var v = TV(p);
            p.Dir = v.x < 0f ? -1f : 1f;
            p.TRot = Mathf.Atan2(v.y, v.x) + (p.Dir < 0f ? Mathf.PI : 0f);
        }

        /// <summary>Main.DrawProj for 408: its second frame, with copies at every other of its last 8 places tinted half blue, fading.</summary>
        static void DrawSharknadoShark(FxBatch fx, Proj p, Vector2 px)
        {
            var spr = FxBatch.Frame("Projectile_408", 0, 28, 54, 28, 27f, 14f);
            float a = 1f - p.Alpha;
            bool flip = p.Dir < 0f;
            var tint = Color.Lerp(new Color(L, L, L, 1f), Color.blue, 0.5f);
            for (int n = 1; n < 8 && n < p.Trail.Count; n += 2)
                fx.Draw(spr, TerraParticles.ToPx(p.Trail[n]), tint * ((8 - n) / 15f) * a, p.TRot, p.Scale, flipX: flip);
            fx.Draw(spr, px, new Color(L, L, L, 1f) * a, p.TRot, p.Scale, flipX: flip);
        }

        /// <summary>P:75153: a burst of blood and the shark in three pieces.</summary>
        static void KillSharknadoShark(Proj p)
        {
            var c = CenterPx(p);
            var v = TV(p);
            for (int k = 0; k < 15; k++)
            {
                int i = BoxDust(c - new Vector2(10f, 10f), 50, 50, 5, 0f, -2f);
                if (i >= 0) D(i).Velocity /= 2f;
            }
            for (int k = 0; k < 3; k++) TerraParticles.Instance?.NewGore(c, v * (0.8f + 0.1f * k), 584 + k);
        }

        // ------------------------------------------------------------------ 195: the pygmies' spear (AI_001)

        /// <summary>P:56298, 58242: Item17 as it sets off; through blocks until it drops (40 updates) or heads down.</summary>
        static void StepPygmySpear(Proj p)
        {
            if (p.Updates == 1) Sound("Item_17");
            p.Collide = p.Ai0 >= 40f || p.Vel.y < 0f;
        }

        /// <summary>P:75772: a dig and five bits of wood.</summary>
        static void KillPygmySpear(Proj p)
        {
            Sound("Dig_" + Random.Range(0, 3));
            for (int k = 0; k < 5; k++) DustBox(p, 7, Vector2.zero, 0, 1f, false);
        }

        // ------------------------------------------------------------------ 433: the UFO's laser (AI_048)

        /// <summary>PO:1956 UFOLaser: from where it started to where it ended, two motes a step, lime green or cornflower blue.</summary>
        static void UfoLine(Proj p)
        {
            if (p.LocalAi1 == 0f) return;
            var from = TerraParticles.ToPx(p.StartPos);
            var to = CenterPx(p);
            float len = Vector2.Distance(from, to);
            var v = TV(p);
            float stride = Mathf.Max(4f, v.magnitude);
            var step = (to - from).sqrMagnitude > 1e-6f ? (to - from).normalized * v.magnitude : Vector2.zero;
            for (float d = 0f; d < len; d += stride)
            {
                var at = Vector2.Lerp(from, to, d / len);
                for (int k = 0; k < 2; k++)
                {
                    int i = PointDust(at - step * 0.25f * k, 160, Vector2.zero);
                    if (i < 0) continue;
                    D(i).Scale = Random.Range(70, 110) * 0.013f;
                    D(i).Velocity *= 0.2f;
                    D(i).Color = Random.Range(0, 2) == 0 ? FxBatch.C(50, 205, 50) : FxBatch.C(100, 149, 237);
                }
            }
        }

        // ------------------------------------------------------------------ 818: the Desert Tiger's pounce (AI_162)

        /// <summary>A pounce from a point along a leap (px), after a wait and over a time (updates, four a tick).</summary>
        public void ShootTigerPounce(Vector2 from, Vector2 leapPx, int damage, float wait, float updates)
        {
            var p = Spawn(ProjKind.TigerPounce, from, Flip(leapPx), damage, 0f, 0f);
            if (p == null) return;
            p.MinionShot = true;
            p.Ai0 = wait;
            p.Ai1 = updates;
        }

        /// <summary>
        /// AI_162: unseen while it waits; then along a Catmull-Rom arc from where it started to the
        /// end of its leap (control points 10% beyond each end and 1.5 leaps lower, so it rises over
        /// the way), spinning, fading in, shedding sparkles; at the end it strikes (Kill: Damage).
        /// </summary>
        void StepTigerPounce(Proj p)
        {
            if (p.Ai0 < 0f)
            {
                p.Alpha = 1f;
                p.Ai0++;
                return;
            }
            if (p.FrameCounter == 0)
            {
                p.FrameCounter = 1;
                p.StartPos = p.Pos;
            }
            var a = TerraParticles.ToPx(p.StartPos);
            var b = a + TV(p);
            float drop = Vector2.Distance(a, b) * 1.5f;
            var c0 = Vector2.LerpUnclamped(a, b, -0.1f) + new Vector2(0f, drop);
            var c3 = Vector2.LerpUnclamped(a, b, 1.1f) + new Vector2(0f, drop);
            p.Alpha = Mathf.Max(0f, p.Alpha - 0.1f);
            int dir = b.x > a.x ? 1 : -1;
            p.TRot += dir * -0.2f;
            p.Dir = dir;
            if (p.Ai1 < 1f) p.Ai1 = 60f;
            p.Ai0 += 1f / p.Ai1;
            float t = Mathf.Clamp01(p.Ai0);
            float t2 = t * t, t3 = t2 * t;
            var at = 0.5f * (2f * a + (-c0 + b) * t + (2f * c0 - 5f * a + 4f * b - c3) * t2 + (-c0 + 3f * a - 3f * b + c3) * t3);
            if (Random.Range(0, 2) == 0)
            {
                int i = BoxDust(CenterPx(p) - new Vector2(66f, 66f), 132, 132, 269, 0f, 0f, 0, 0.4f);
                if (i >= 0) D(i).FadeIn = 0.7f;
            }
            p.Pos = TerraParticles.ToWorld(at);
            if (p.Ai0 >= 1f) Kill(p);
        }

        /// <summary>Main.DrawProj for 818: the leaping tiger only as its trail, every other of its last 18 places, gold to white, added, fading in and out over the leap.</summary>
        static void DrawTigerPounce(FxBatch fx, Proj p, Vector2 px)
        {
            if (p.Ai0 < 0f) return;
            var spr = FxBatch.Frame("Projectile_818", 0, 0, 64, 44, 32f, 22f);
            float k = Mathf.Clamp01(Mathf.InverseLerp(0f, 0.3f, p.Ai0)) * Mathf.Clamp01(Mathf.InverseLerp(1f, 0.7f, p.Ai0));
            var gold = FxBatch.C(255, 231, 69, 0) * k;
            var col = Color.Lerp(gold, new Color(1f, 1f, 1f, 0f), k);
            for (int n = Mathf.Min(18, p.Trail.Count - 1); n > 0; n -= 2)
                fx.Draw(spr, TerraParticles.ToPx(p.Trail[n]), col * ((18 - n) / 30f), p.TRot, 1f, flipX: p.Dir < 0f);
        }

        /// <summary>P:72793, 72837: gold and white sparkles over its box, and the strike: everything in its 132 px once.</summary>
        void KillTigerPounce(Proj p)
        {
            var tl = CenterPx(p) - new Vector2(66f, 66f);
            for (int k = 0; k < 6; k++)
            {
                var at = tl + new Vector2(Random.value * 132f, Random.value * 132f);
                float a = k / 3f * Mathf.PI * 2f + Random.value * Mathf.PI;
                int i = PointDust(at, 278, new Vector2(-Mathf.Sin(a), Mathf.Cos(a)) * (k < 3 ? 2f + Random.value * 0.5f : 1f + Random.value * 0.5f), 150, k < 3 ? 0.4f : 0.7f);
                if (i < 0) continue;
                D(i).NoGravity = true;
                D(i).Color = Color.Lerp(Color.white, k < 3 ? new Color(1f, 0.84f, 0f) : FxBatch.C(255, 231, 69), Random.value);
            }
            foreach (int id in Blast(p, 132f, p.Damage, 0f)) DevHits[p.Kind] = DevHit(p.Kind) + 1;
        }

        // ------------------------------------------------------------------ 614: the Stardust Cell's seed (AI_113)

        /// <summary>A seed for an enemy (ai[1]).</summary>
        public void ShootStardustCell(Vector2 from, Vector2 velocity, int damage, float knockback, HealthManager target)
        {
            var p = Spawn(ProjKind.StardustCellShot, from, velocity, damage, knockback, 0f);
            if (p == null) return;
            p.MinionShot = true;
            p.Target = target;
        }

        /// <summary>
        /// AI_113 for 614: it shows in fast; flying, it bends to its enemy at 14 px an update,
        /// (v·4 + aim)/5, and is gone if that one is; clinging, it rides along on the spot it hit.
        /// </summary>
        void StepStardustCell(Proj p)
        {
            p.Alpha = Mathf.Max(0f, p.Alpha - 63f / 255f);
            if (p.LocalAi1 == 0f)
            {
                if (!Chaseable(p.Target))
                {
                    Kill(p);
                    return;
                }
                var to = EnemyCenterPx(p.Target) - CenterPx(p);
                var v = TV(p);
                if (to != Vector2.zero) v = (v * 4f + to.normalized * 14f) / 5f;
                p.Vel = Flip(v);
                p.TRot = Mathf.Atan2(v.y, v.x) + Mathf.PI / 2f;
                return;
            }
            if (p.Target == null || p.Target.isDead || ++p.LocalAi0 >= 900f)
            {
                Kill(p);
                return;
            }
            p.Pos = TerraParticles.ToWorld(EnemyCenterPx(p.Target) - TV(p) * 2f);
        }

        /// <summary>
        /// P:13244: the first enemy it hits holds it (velocity becomes three quarters of the way to
        /// that enemy's centre, its spot on it); the one it was sent at is celled for 900 ticks; past
        /// ten on one enemy the oldest goes.
        /// </summary>
        void StickCell(Proj p, HealthManager hm)
        {
            if (p.LocalAi1 != 0f) return;
            if (hm == p.Target) EnemyDebuffs.Add(hm, EnemyDebuffs.Celled, 900);
            p.LocalAi1 = 1f;
            p.Target = hm;
            p.Collide = false;
            p.Vel = Flip((EnemyCenterPx(hm) - CenterPx(p)) * 0.75f);
            Proj oldest = null;
            int n = 0;
            foreach (var q in live)
            {
                if (q.Dead || q.Kind != ProjKind.StardustCellShot || q.LocalAi1 == 0f || q.Target != hm) continue;
                n++;
                if (oldest == null || q.TimeLeft < oldest.TimeLeft) oldest = q;
            }
            if (n > 10 && oldest != null) Kill(oldest);
        }

        /// <summary>The seeds clinging to an enemy (NPC.AddStackedProjecitle's count).</summary>
        public int StuckCells(HealthManager hm)
        {
            int n = 0;
            foreach (var q in live) if (!q.Dead && q.Kind == ProjKind.StardustCellShot && q.LocalAi1 != 0f && q.Target == hm) n++;
            return n;
        }

        /// <summary>Main.DrawProj for 614: its frame, lit and fading in, and its glow (Glow_190) half added; a blue light.</summary>
        static void DrawStardustCell(FxBatch fx, Proj p, Vector2 px)
        {
            fx.Light(px, new Color(0.2f, 0.6f, 0.7f), 2.5f);
            float a = 1f - p.Alpha;
            fx.Draw(FxBatch.Frame("Projectile_614", 0, 0, 18, 20, 9f, 10f), px, new Color(L, L, L, 1f) * a, p.TRot, p.Scale);
            float g = (128f - p.Alpha * 255f / 2f) / 255f;
            fx.Draw(FxBatch.Frame("Glow_190", 0, 0, 18, 20, 9f, 10f), px, new Color(g, g, g, 0f), p.TRot, p.Scale);
        }

        /// <summary>P:73407: ten blue motes.</summary>
        static void KillStardustCell(Proj p)
        {
            var tl = CenterPx(p) - new Vector2(8f, 8f);
            for (int k = 0; k < 10; k++)
            {
                int i = BoxDust(tl, 16, 16, 229);
                if (i < 0) continue;
                D(i).NoGravity = true;
                D(i).Velocity *= 3f;
            }
        }
    }
}
