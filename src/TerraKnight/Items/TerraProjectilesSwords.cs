using System.Collections.Generic;
using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The shots of the hardmode swords that hit with their blade and shoot too, and the
    /// Horseman's Blade's jack-o'-lanterns, as Projectile.cs runs them (each worked out in
    /// Terraria's space: px y down, clockwise rotation, per update):
    /// the Seedler's seed (483, AI_14) bouncing until it bursts into seedlets (484) at the enemies
    /// in sight; the Influx Waver's blade (451, AI_81) that, once it has struck, fades out and
    /// strikes again from a ring round the nearest enemy; the Meowmere's cat (502, AI_8) bouncing
    /// with a meow and a rainbow behind it; the Star Wrath's falling stars (503, AI_5) and the
    /// flaming jack-o'-lantern (321, AI_55) homing on the enemy the Horseman's Blade struck.
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        /// <summary>Dev: Influx Waver strikes from the ring, Meowmere bounces, Star Wrath stars that reached the cursor's height.</summary>
        public static int DevInfluxRings, DevMeowBounces, DevStarsDown;

        static void RegisterSwords()
        {
            specs[ProjKind.FlamingJack] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_321", W = 30, H = 30, Time = 600, Penetrate = 1, TileCollide = false, Look = Look.HolyAdd,
                Update = (t, p) => t.StepFlamingJack(p), Draw = (t, fx, p, px) => DrawFlamingJack(fx, p, px), OnKill = (t, p) => t.KillFlamingJack(p),
            };
            specs[ProjKind.SeedlerSeed] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_483", W = 14, H = 14, Time = 3600, Penetrate = 1,
                Update = (t, p) => StepSeed(p), Draw = (t, fx, p, px) => DrawTurning(fx, p, px, 16, 14),
                TileHit = (t, p, hx, hy, ov) => BounceSeed(p, hx, hy, ov), OnKill = (t, p) => t.KillSeed(p),
            };
            specs[ProjKind.SeedlerSeedlet] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_484", W = 10, H = 10, Time = 3600, Penetrate = 1, Extra = 1,
                Update = (t, p) => StepSeedlet(p), Draw = (t, fx, p, px) => DrawTurning(fx, p, px, 10, 14), OnKill = (t, p) => KillSeedlet(p),
            };
            specs[ProjKind.InfluxWaver] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_451", W = 16, H = 16, Time = 3600, Penetrate = -1, AlphaStart = 1f, OneHitPerUpdate = true, NoImmunity = true,
                Update = (t, p) => t.StepInflux(p), Draw = (t, fx, p, px) => DrawInflux(fx, p, px), CanDamage = InfluxCanDamage,
                OnHit = (t, p, hm) => InfluxHit(p), TileHit = (t, p, hx, hy, ov) => InfluxWall(p, ov),
            };
            specs[ProjKind.Meowmere] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_502", W = 16, H = 16, Time = 3600, Penetrate = 5, LocalCooldown = 10, TrailLength = 25,
                Update = (t, p) => StepMeowmere(p), Draw = (t, fx, p, px) => DrawMeowmere(fx, p, px),
                TileHit = (t, p, hx, hy, ov) => t.BounceMeowmere(p, hx, hy, ov), OnKill = (t, p) => KillMeowmere(p),
            };
            specs[ProjKind.StarWrath] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_503", W = 24, H = 24, Time = 3600, Penetrate = 2, Extra = 1, LocalCooldown = 15, TileCollide = false,
                AlphaStart = 1f, TrailLength = 10,
                Update = (t, p) => StepStarWrath(p), Draw = (t, fx, p, px) => DrawStarWrath(fx, p, px), OnKill = (t, p) => KillStarWrath(p),
            };
        }

        // ------------------------------------------------------------------ shooting

        /// <summary>A Horseman's Blade jack-o'-lantern (world position, Terraria px per update y up) after one enemy.</summary>
        public void ShootFlamingJack(Vector2 from, Vector2 velocity, int damage, float knockback, HealthManager target)
        {
            var p = Spawn(ProjKind.FlamingJack, from, velocity, damage, knockback, 0f);
            if (p == null) return;
            p.Melee = true;
            p.Target = target;
        }

        /// <summary>The Influx Waver's blade, starting in its unseen first moments (ai[0] = -1).</summary>
        public void ShootInflux(Vector2 from, Vector2 velocity, int damage, float knockback, float crit)
        {
            var p = Spawn(ProjKind.InfluxWaver, from, velocity, damage, knockback, crit);
            if (p == null) return;
            p.Melee = true;
            p.Ai0 = -1f;
        }

        /// <summary>A Star Wrath star, through everything until below floorY (world).</summary>
        public void ShootStarWrath(Vector2 from, Vector2 velocity, int damage, float knockback, float crit, float floorY)
        {
            var p = Spawn(ProjKind.StarWrath, from, velocity, damage, knockback, crit);
            if (p == null) return;
            p.Melee = true;
            p.Ai1 = floorY;
            p.Dir = velocity.x < 0f ? -1f : 1f;
        }

        // ------------------------------------------------------------------ shared bits

        static Vector2 CenterPx(Proj p) => TerraParticles.ToPx(p.Pos);

        /// <summary>Terraria's velocity: y down.</summary>
        static Vector2 TV(Proj p) => Flip(p.Vel);

        static float ToRot(Vector2 v) => Mathf.Atan2(v.y, v.x);

        static Vector2 RotateBy(Vector2 v, float a)
        {
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        static ref TerraParticles.Dust D(int i) => ref TerraParticles.Instance[i];

        /// <summary>Dust.NewDust over a box (Terraria px, top-left), velocity y down.</summary>
        static int BoxDust(Vector2 topLeft, int w, int h, int type, float vx = 0f, float vy = 0f, int alpha = 0, float scale = 1f)
        {
            var pp = TerraParticles.Instance;
            return pp == null ? -1 : pp.NewDust(topLeft, w, h, type, vx, vy, alpha, scale);
        }

        /// <summary>Dust.NewDustPerfect: exactly at a point (Terraria px) with a velocity (y down).</summary>
        static int PointDust(Vector2 at, int type, Vector2 vel, int alpha = 0, float scale = 1f)
        {
            int i = BoxDust(at - new Vector2(4f, 4f), 0, 0, type, 0f, 0f, alpha, scale);
            if (i < 0) return -1;
            D(i).Position = at;
            D(i).Velocity = vel;
            return i;
        }

        /// <summary>NPC.CanBeChasedBy, here: a live enemy worth chasing, not invincible all round.</summary>
        static bool Chaseable(HealthManager hm)
        {
            if (hm == null || hm.isDead || !hm.gameObject.activeInHierarchy) return false;
            if (EnemyStats.Untouchable(hm)) return false;
            var info = EnemyStats.Of(hm);
            return info != null && EnemyStats.LifeMax(info) > 5;
        }

        static Vector2 EnemyCenterPx(HealthManager hm)
        {
            var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
            return TerraParticles.ToPx(col != null ? (Vector2)col.bounds.center : (Vector2)hm.transform.position);
        }

        static readonly List<HealthManager> chaseable = new List<HealthManager>();
        static readonly List<Collider2D> chaseOverlaps = new List<Collider2D>();

        /// <summary>The chaseable enemies within a distance (Terraria px) of a point.</summary>
        static List<HealthManager> EnemiesNear(Vector2 px, float within)
        {
            chaseable.Clear();
            chaseOverlaps.Clear();
            var filter = new ContactFilter2D { useTriggers = true, useLayerMask = true, layerMask = MeleeHits.HitMask };
            Physics2D.OverlapCircle(TerraParticles.ToWorld(px), within * Px, filter, chaseOverlaps);
            foreach (var c in chaseOverlaps)
            {
                var hm = c.GetComponentInParent<HealthManager>();
                if (hm != null && !chaseable.Contains(hm) && Chaseable(hm)) chaseable.Add(hm);
            }
            return chaseable;
        }

        /// <summary>Collision.CanHit: nothing solid on the line between two points (Terraria px).</summary>
        static bool InSight(Vector2 a, Vector2 b) =>
            !Physics2D.Linecast(TerraParticles.ToWorld(a), TerraParticles.ToWorld(b), 1 << ChunkColliders.TerrainLayer);

        /// <summary>A sprite turning with TRot, drawn lit at its centre.</summary>
        static void DrawTurning(FxBatch fx, Proj p, Vector2 px, int w, int h)
        {
            fx.Draw(FxBatch.Frame(p.Spec.Texture, 0, 0, w, h, w * 0.5f, h * 0.5f), px, new Color(L, L, L, 1f), p.TRot, p.Scale);
        }

        // ------------------------------------------------------------------ 321: the flaming jack-o'-lantern (AI_55)

        void StepFlamingJack(Proj p)
        {
            // A new frame every update, three frames.
            if (++p.FrameCounter > 0)
            {
                p.FrameCounter = 0;
                if (++p.Frame > 2) p.Frame = 0;
            }
            var v = TV(p);
            p.Dir = v.x < 0f ? -1f : 1f;
            p.TRot = v.x < 0f ? Mathf.Atan2(-v.y, -v.x) : Mathf.Atan2(v.y, v.x);
            var c = CenterPx(p);
            if (Chaseable(p.Target))
            {
                var d = EnemyCenterPx(p.Target) - c;
                var want = d.sqrMagnitude > 1e-4f ? d.normalized * 8f : Vector2.zero;
                v = (v * 14f + want) / 15f;
                p.Vel = Flip(v);
            }
            else
            {
                // The nearest it can see, by its Manhattan distance, within 1000 px.
                p.Target = null;
                float best = 1000f;
                foreach (var hm in EnemiesNear(c, 1000f))
                {
                    var e = EnemyCenterPx(hm);
                    float m = Mathf.Abs(c.x - e.x) + Mathf.Abs(c.y - e.y);
                    if (m < best && InSight(c, e))
                    {
                        best = m;
                        p.Target = hm;
                    }
                }
            }
            int i = BoxDust(c - new Vector2(15f, 15f) + new Vector2(8f, 8f), 30 - 16, 30 - 16, 6);
            if (i >= 0)
            {
                D(i).Velocity = D(i).Velocity * 0.5f + v * 0.5f;
                D(i).NoGravity = true;
                D(i).Scale = 1.4f;
            }
        }

        void KillFlamingJack(Proj p)
        {
            Sound("Item_10");
            var v = TV(p);
            var tl = CenterPx(p) - new Vector2(15f, 15f);
            for (int k = 0; k < 20; k++)
            {
                int i = BoxDust(tl, 30, 30, 6, -v.x * 0.2f, -v.y * 0.2f, 100, 2f);
                if (i >= 0)
                {
                    D(i).NoGravity = true;
                    D(i).Velocity *= 2f;
                }
                i = BoxDust(tl, 30, 30, 6, -v.x * 0.2f, -v.y * 0.2f, 100);
                if (i >= 0) D(i).Velocity *= 2f;
            }
        }

        static void DrawFlamingJack(FxBatch fx, Proj p, Vector2 px)
        {
            var frame = FxBatch.Frame("Projectile_321", 0, p.Frame * 32, 30, 32, 15f, 16f);
            fx.Draw(frame, px, FxBatch.C(200, 200, 200, 0), p.TRot, p.Scale, flipX: p.Dir < 0f);
        }

        // ------------------------------------------------------------------ 483/484: the Seedler's seed and seedlets

        static void StepSeed(Proj p)
        {
            p.Ai0++;
            if (p.Ai0 > 5f)
            {
                // It bursts at a random moment between 20 and 130 updates.
                if (p.Ai0 > Random.Range(20, 130))
                {
                    Instance.Kill(p);
                    return;
                }
                var v = TV(p);
                if (v.y == 0f && v.x != 0f)
                {
                    v.x *= 0.97f;
                    if (v.x > -0.01f && v.x < 0.01f) v.x = 0f;
                }
                v.y += 0.3f;
                v.x *= 0.99f;
                p.Vel = Flip(v);
            }
            p.TRot += TV(p).x * 0.05f;
        }

        /// <summary>aiStyle 14's bounce: half back off walls, and off the floor when falling faster than 1.</summary>
        static void BounceSeed(Proj p, bool hitX, bool hitY, Vector2 oldVel)
        {
            var o = Flip(oldVel);
            var v = TV(p);
            if (hitX) v.x = o.x * -0.5f;
            if (hitY) v.y = o.y > 1f ? o.y * -0.5f : 0f;
            p.Vel = Flip(v);
        }

        /// <summary>
        /// Projectile.Kill for 483: a burst (sound Item14) of 4 to 7 seedlets at 70% of its damage and
        /// knockback, one at each enemy in sight within 2000 px, the rest round where they lie; dust.
        /// </summary>
        void KillSeed(Proj p)
        {
            Sound("Item_14");
            var c = CenterPx(p);
            int n = Random.Range(4, 8);
            var targets = new List<Vector2>();
            foreach (var hm in EnemiesNear(c, 2000f))
            {
                var e = EnemyCenterPx(hm);
                if (!InSight(c, e)) continue;
                targets.Add(e);
                if (targets.Count == n) break;
            }
            // Shuffled (100 random swaps when there are two or more).
            for (int k = 0; targets.Count > 1 && k < 100; k++)
            {
                int a = Random.Range(0, targets.Count), b;
                do b = Random.Range(0, targets.Count); while (b == a);
                (targets[a], targets[b]) = (targets[b], targets[a]);
            }
            var lean = new Vector2(-1f, -1f);
            foreach (var e in targets) lean += (e - c).normalized;
            lean.Normalize();
            int left = targets.Count;
            for (int k = 0; k < n; k++)
            {
                float speed = Random.Range(8, 15);
                var v = new Vector2(Random.Range(-100, 101), Random.Range(-100, 101)).normalized;
                if (left > 0)
                {
                    v = (v + lean).normalized;
                    left--;
                    v = (targets[left] - c).normalized;
                }
                var s2 = Spawn(ProjKind.SeedlerSeedlet, p.Pos, Flip(v * speed), (int)(p.Damage * 0.7), p.Knockback * 0.7f, p.Crit);
                if (s2 != null) s2.Melee = true;
            }
            var tl = c - new Vector2(7f, 7f);
            for (int k = 0; k < 20; k++)
            {
                int i = BoxDust(tl, 14, 14, 78);
                if (i < 0) continue;
                D(i).NoGravity = true;
                D(i).Velocity *= 4f;
            }
            for (int k = 0; k < 7; k++)
            {
                int i = BoxDust(tl, 14, 14, 31, 0f, 0f, 100, 1.5f);
                if (i < 0) continue;
                D(i).Velocity *= 0.9f;
                D(i).Scale = 0.9f;
            }
            for (int k = 0; k < 3; k++)
            {
                int i = BoxDust(tl, 14, 14, 6, 0f, 0f, 100, 2.5f);
                if (i >= 0) D(i).NoGravity = true;
            }
        }

        /// <summary>AI_1 for 484: straight on (no gravity), pointing along its flight, shedding green dust.</summary>
        static void StepSeedlet(Proj p)
        {
            var v = TV(p);
            p.TRot = ToRot(v) + Mathf.PI / 2f;
            var c = CenterPx(p);
            int i = BoxDust(c - new Vector2(5f, 5f), 10, 10, 78);
            if (i < 0) return;
            D(i).NoGravity = true;
            D(i).Velocity *= 0.1f;
            D(i).Scale = 0.75f;
            D(i).Position = (D(i).Position + c) / 2f + v * Random.Range(0, 101) * 0.01f;
        }

        static void KillSeedlet(Proj p)
        {
            var v = TV(p);
            var tl = CenterPx(p) - new Vector2(5f, 5f);
            for (int k = 0; k < 5; k++)
            {
                int i = BoxDust(tl, 10, 10, 78);
                if (i < 0) continue;
                D(i).NoGravity = true;
                D(i).Velocity -= v / 5f;
                D(i).Scale = 0.85f;
            }
        }

        // ------------------------------------------------------------------ 451: the Influx Waver (AI_81)

        /// <summary>The blade's penetrate (3): not spent, it sizes the states ai[0] moves through.</summary>
        const int InfluxPen = 3;

        /// <summary>Projectile.cs:12463: it hurts only while flying (ai[0] = 0) or a few updates into a strike from the ring.</summary>
        static bool InfluxCanDamage(Proj p)
        {
            if (p.Ai0 == 0f) return true;
            return !((int)(p.Ai0 - 1f) / InfluxPen == 0 || p.Ai1 < 5f);
        }

        /// <summary>Projectile.cs:14116: a strike sends it to fade out and strike again (one hit an update).</summary>
        static void InfluxHit(Proj p)
        {
            if (p.Ai0 == 0f) p.Ai0 += InfluxPen;
            else p.Ai0 -= InfluxPen + 1;
            p.Ai1 = 0f;
        }

        /// <summary>Meeting a wall: off to fade out (for good), at half speed.</summary>
        static void InfluxWall(Proj p, Vector2 oldVel)
        {
            p.Ai0 = 1f;
            p.Ai1 = 0f;
            p.Vel = oldVel / 2f;
        }

        void InfluxDust(Proj p, float scale, bool alwaysNoGravity)
        {
            int type = Random.Range(0, 2) == 0 ? 226 : 229;
            var at = CenterPx(p) + RotateBy(new Vector2(-32f, 32f), p.TRot);
            int i = BoxDust(at - Vector2.one * 4f, 8, 8, type, 0f, 0f, 100, scale);
            if (i < 0) return;
            D(i).Velocity *= 0.1f;
            if (alwaysNoGravity || Random.Range(0, 6) != 0) D(i).NoGravity = true;
        }

        void StepInflux(Proj p)
        {
            var v = TV(p);
            float n647 = Mathf.Max(1f, v.magnitude / 11f);
            int alpha = Mathf.RoundToInt(p.Alpha * 255f);
            if (p.Ai0 == -1f)
            {
                p.Collide = true;
                p.LocalAi0++;
                if (p.LocalAi0 > 7f) InfluxDust(p, 1f, false);
                int maxA = 75, n655 = Mathf.Min(7, (int)(7f / n647));
                if (p.LocalAi0 > n655 && p.Ai1 == 0f)
                {
                    p.Scale -= 0.01f * n647;
                    alpha += (int)(5f * n647);
                    if (alpha > maxA)
                    {
                        alpha = maxA;
                        p.Ai0 = 0f;
                    }
                }
                p.TRot = ToRot(v) + Mathf.PI / 4f;
            }
            else if (p.Ai0 == 0f)
            {
                p.Collide = true;
                p.LocalAi0++;
                if (p.LocalAi0 > 7f) InfluxDust(p, 1f, false);
                int n664 = Mathf.Min(7, (int)(7f * n647));
                if (p.LocalAi0 > n664)
                {
                    // Breathing: fading to alpha 75 and shrinking, then back.
                    if (p.Bounces == 0)
                    {
                        p.Scale -= 0.01f;
                        alpha += 5;
                        if (alpha > 75)
                        {
                            alpha = 75;
                            p.Bounces = 1;
                        }
                    }
                    else
                    {
                        p.Scale += 0.01f;
                        alpha -= 5;
                        if (alpha <= 0)
                        {
                            alpha = 0;
                            p.Bounces = 0;
                        }
                    }
                }
                p.TRot = ToRot(v) + Mathf.PI / 4f;
            }
            else if (p.Ai0 >= 1f && p.Ai0 < 1 + InfluxPen)
            {
                // Fading out, then off to a ring 100 px round the nearest enemy within 250 px.
                p.Collide = false;
                alpha += (int)(15f * n647);
                v *= 0.98f / n647;
                p.LocalAi0 = 0f;
                var c = CenterPx(p);
                HealthManager near = null;
                float best = 250f;
                foreach (var hm in EnemiesNear(c, 250f))
                {
                    float d = Vector2.Distance(EnemyCenterPx(hm), c);
                    if (d < best)
                    {
                        best = d;
                        near = hm;
                    }
                }
                if (alpha >= 255)
                {
                    alpha = 255;
                    if (p.Ai0 == 1f || near == null)
                    {
                        p.Alpha = 1f;
                        Kill(p);
                        return;
                    }
                    p.Ai0 += InfluxPen;
                    DevInfluxRings++;
                    var e = EnemyCenterPx(near);
                    float a = Random.value * Mathf.PI * 2f;
                    c = e + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 100f;
                    p.Pos = TerraParticles.ToWorld(c);
                    v = (e - c).normalized * 15f;
                    p.TRot = ToRot(v) + Mathf.PI / 4f;
                }
                if (near != null) p.Pos += EnemyDrift(near);
                if (Random.Range(0, 3) == 0) InfluxDust(p, 0.6f, true);
            }
            else if (p.Ai0 >= 1 + InfluxPen && p.Ai0 < 1 + InfluxPen * 2)
            {
                // The strike from the ring: in sharply, out over the last updates.
                p.Scale = 0.9f;
                p.Collide = false;
                p.TRot = ToRot(v) + Mathf.PI / 4f;
                p.Ai1++;
                if (p.Ai1 >= 15f)
                {
                    alpha += 51;
                    v *= 0.8f;
                    if (alpha >= 255)
                    {
                        p.Alpha = 1f;
                        Kill(p);
                        return;
                    }
                }
                else
                {
                    alpha = Mathf.Max(0, alpha - 125);
                    v *= 0.98f;
                }
                p.LocalAi0++;
                var c = CenterPx(p);
                HealthManager near = null;
                float best = 250f;
                foreach (var hm in EnemiesNear(c, 250f))
                {
                    float d = Vector2.Distance(EnemyCenterPx(hm), c);
                    if (d < best)
                    {
                        best = d;
                        near = hm;
                    }
                }
                if (near != null) p.Pos += EnemyDrift(near);
                InfluxDust(p, 0.6f, true);
            }
            p.Vel = Flip(v);
            p.Alpha = Mathf.Clamp01(alpha / 255f);
        }

        /// <summary>How far an enemy moved since the last check (Main.npc[i].velocity, world units).</summary>
        static Vector2 EnemyDrift(HealthManager hm)
        {
            var rb = hm.GetComponent<Rigidbody2D>();
            return rb != null ? rb.linearVelocity * (1f / 60f) : Vector2.zero;
        }

        static void DrawInflux(FxBatch fx, Proj p, Vector2 px)
        {
            float a = p.Alpha;
            // Its light (0.2) and AI_81's blue one with alpha/255, which swells as the blade fades (the brighter of each).
            fx.Light(px, new Color(Mathf.Max(0.2f, 0.3f * a), Mathf.Max(0.2f, 0.4f * a), Mathf.Max(0.2f, a)), 3f);
            fx.Draw(FxBatch.Frame("Projectile_451", 0, 0, 48, 48, 24f, 24f), px, FxBatch.C(255, 255, 255, 200) * (1f - a), p.TRot, p.Scale);
        }

        // ------------------------------------------------------------------ 502: the Meowmere's cat (AI_8)

        static void StepMeowmere(Proj p)
        {
            var v = TV(p);
            p.Ai1++;
            if (p.Ai1 >= 20f) v.y += 0.2f;
            if (v.y > 16f) v.y = 16f;
            p.TRot = ToRot(v) + Mathf.PI / 2f;
            if (v.x != 0f) p.Dir = Mathf.Sign(v.x);
            p.Vel = Flip(v);
        }

        /// <summary>Meeting a block: a meow, a rainbow ring of dust and a bounce; the fifth bounce ends it.</summary>
        void BounceMeowmere(Proj p, bool hitX, bool hitY, Vector2 oldVel)
        {
            p.Ai0 = Mathf.Clamp(p.Ai0 + 1f, 1f, 5f);
            DevMeowBounces++;
            // LegacySoundPlayer type 37: Item_57 or 58, louder with each bounce, pitch ±0.4.
            Sound(Random.Range(0, 2) == 0 ? "Item_57" : "Item_58", 0.5f * (5f + p.Ai0) * 0.05f * 2f);
            if (p.Ai0 >= 5f)
            {
                p.Pos += oldVel * Px;
                Kill(p);
                return;
            }
            var o = Flip(oldVel);
            var v = TV(p);
            if (hitY) v.y = -o.y;
            if (hitX) v.x = -o.x;
            p.Vel = Flip(v);
            var spin = RotateBy(new Vector2(0f, -3f - p.Ai0), Random.Range(-Mathf.PI, Mathf.PI));
            float count = 10f + p.Ai0 * 4f;
            var c = CenterPx(p);
            for (float k = 0f; k < count; k++)
            {
                var dv = RotateBy(spin, Mathf.PI * 2f * k / count);
                int i = PointDust(c, 66, new Vector2(dv.x * 1.05f, dv.y) * (0.8f + Random.value * 0.4f));
                if (i < 0) continue;
                D(i).Color = Color.HSVToRGB(Random.value, 1f, 1f);
                D(i).NoGravity = true;
            }
        }

        static void KillMeowmere(Proj p)
        {
            foreach (var w in p.Trail)
            {
                int i = PointDust(TerraParticles.ToPx(w), 66, Vector2.zero, 150, 0.7f);
                if (i < 0) continue;
                D(i).Color = Color.HSVToRGB(Random.value, 1f, 1f);
                D(i).NoGravity = true;
            }
        }

        /// <summary>
        /// Main.DrawProj for 502: a rainbow (Projectile_250 stretched between its last 25 places,
        /// half see-through and fading towards the end) behind the cat, which flips with its heading.
        /// Its light is the disco colour, softened (AI_8).
        /// </summary>
        static void DrawMeowmere(FxBatch fx, Proj p, Vector2 px)
        {
            var disco = Color.HSVToRGB(Time.time * 0.25f % 1f, 1f, 1f);
            fx.Light(px, new Color((0.5f + disco.r) / 2f, (0.5f + disco.g) / 2f, (0.5f + disco.b) / 2f), 3f);
            var band = FxBatch.Frame("Projectile_250", 0, 0, 14, 32, 7f, 0f);
            int n = p.Trail.Count;
            for (int k = n - 1; k > 0; k--)
            {
                var a = TerraParticles.ToPx(p.Trail[k]);
                var b = TerraParticles.ToPx(p.Trail[k - 1]);
                float len = Vector2.Distance(a, b);
                if (len < 0.01f) continue;
                var c = FxBatch.C(255, 255, 255, 127) * (1f - (float)k / 25f);
                fx.Draw(band, a, c, ToRot(b - a) - Mathf.PI / 2f, new Vector2(1f, len / 32f));
            }
            fx.Draw(FxBatch.Frame("Projectile_502", 0, 0, 24, 24, 12f, 12f), px, FxBatch.C(255, 255, 255, 200), p.TRot, p.Scale, flipX: p.Dir < 0f);
        }

        // ------------------------------------------------------------------ 503: the Star Wrath's star (AI_5)

        static void StepStarWrath(Proj p)
        {
            // Through everything until its centre is below the cursor's height.
            if (p.Pos.y < p.Ai1 && !p.Collide)
            {
                p.Collide = true;
                DevStarsDown++;
            }
            if (p.SoundDelay > 0) p.SoundDelay--;
            else
            {
                p.SoundDelay = 20 + Random.Range(0, 40);
                Sound("Item_9");
            }
            int alpha = Mathf.RoundToInt(p.Alpha * 255f) - 15;
            int floor = p.Pos.y <= p.Ai1 ? 0 : 150;
            p.Alpha = Mathf.Max(alpha, floor) / 255f;
            var v = TV(p);
            p.LocalAi0 += (Mathf.Abs(v.x) + Mathf.Abs(v.y)) * 0.01f * p.Dir;
            p.TRot = ToRot(v) - Mathf.PI / 2f;
            var c = CenterPx(p);
            if (Random.Range(0, 16) == 0)
            {
                var d3 = RotateBy(RotateBy(Vector2.right, Random.Range(-Mathf.PI / 2f, Mathf.PI / 2f)), ToRot(v));
                int i = BoxDust(c - new Vector2(12f, 12f), 24, 24, 58, v.x * 0.5f, v.y * 0.5f, 150, 1.2f);
                if (i >= 0)
                {
                    D(i).Velocity = d3 * 0.66f;
                    D(i).Position = c + d3 * 12f;
                }
            }
            if (Random.Range(0, 48) == 0)
                TerraParticles.Instance?.NewGore(c - new Vector2(10f, 10f), v * 0.2f, 16, 1f, 0.66f, v * 0.3f);
        }

        static void KillStarWrath(Proj p)
        {
            Sound("Item_10");
            var v = TV(p) / 2f;
            var tl = CenterPx(p) - new Vector2(12f, 12f);
            for (int k = 0; k < 40; k++) BoxDust(tl, 24, 24, 58, v.x * 0.1f, v.y * 0.1f, 150, 1.2f);
            for (int k = 0; k < 2; k++) TerraParticles.Instance?.NewGore(tl, v * 0.05f, 16);
        }

        /// <summary>
        /// Main.DrawProj for 503: three fading copies at its 2nd, 5th and 8th last places, the star
        /// (pivot 70 px down its 90) in purple-white that pulses with its spin, and the star glow
        /// (Extra_36) turning over it.
        /// </summary>
        static void DrawStarWrath(FxBatch fx, Proj p, Vector2 px)
        {
            float o = 1f - p.Alpha;
            var lit = Color.Lerp(new Color(L, L, L, 1f), Color.white, 0.5f) * o;
            var purple = Color.Lerp(FxBatch.C(128, 0, 128), Color.white, 0.33f);
            var c = Color.Lerp(lit, purple, 0.25f + Mathf.Cos(p.LocalAi0) * 0.25f);
            var star = FxBatch.Frame("Projectile_503", 0, 0, 34, 90, 17f, 70f);
            for (int k = 1; k < 9; k += 3)
            {
                if (k >= p.Trail.Count) continue;
                fx.Draw(star, TerraParticles.ToPx(p.Trail[k]), c * ((9f - k) / 15f), p.TRot, Mathf.Lerp(p.Scale, 0.5f, k / 15f));
            }
            fx.Draw(star, px, c, p.TRot, p.Scale);
            fx.Draw(FxBatch.Frame("Extra_36", 0, 0, 34, 90, 17f, 70f), px, Color.white, p.LocalAi0, p.Scale);
        }
    }
}
