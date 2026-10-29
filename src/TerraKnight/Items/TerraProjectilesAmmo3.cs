using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The specialist ammo of sweep R4b5 (Terraria's space, px y down, per update): the Snowball
    /// (166), the Stynger's bolt (246) bursting into shrapnel (249), the Candy Corn (311) that
    /// bounces about, the Explosive Jack 'O Lantern (312), the Stake (323), the Nail (514, AI_093:
    /// it sticks in and goes off), the Ale Tosser's mug (669, AI_068) and the Acorn (1088, AI_016).
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        public static int DevStyngerShrapnel, DevNailsStuck, DevNailTargetBlasts;

        static void RegisterAmmo3()
        {
            // P:2551: the snowball, AI_002.
            specs[ProjKind.SnowballProj] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_166", W = 14, H = 14, Time = 3600, Penetrate = 1,
                Update = (t, p) => StepThrowAi2(p), Draw = (t, fx, p, px) => DrawGenericLit(fx, p, px), OnKill = (t, p) => KillSnowball(p),
            };
            // P:3291, 14089: the Stynger's bolt, twice a tick, an enemy every 7 ticks, its own hit doubled; P:3316: its shrapnel, AI_002.
            specs[ProjKind.StyngerBoltProj] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_246", W = 10, H = 10, Time = 3600, Extra = 1, Penetrate = 1, AlphaStart = 1f, OwnerImmune = 7,
                Update = (t, p) => StepStyngerBolt(p), Draw = (t, fx, p, px) => DrawGenericLit(fx, p, px), OnKill = (t, p) => t.KillStyngerBolt(p),
                ModifyHit = (t, p, hm, dealt, crit) => (p.TimeLeft > 3 ? dealt * 2 : dealt, crit),
            };
            specs[ProjKind.StyngerShrapnel] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_249", W = 12, H = 12, Time = 3600, Penetrate = 1, OwnerImmune = 7,
                Update = (t, p) => StepShrapnel(p), Draw = (t, fx, p, px) => DrawFramed(fx, p, px, 5, new Color(L, L, L, 1f) * (1f - p.Alpha)), OnKill = (t, p) => t.KillShrapnel(p),
            };
            // P:4035, 18848: the candy corn, three enemies each every 10 ticks, 5 s, off walls four times.
            specs[ProjKind.CandyCornProj] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_311", W = 10, H = 12, Time = 300, Penetrate = 3, LocalCooldown = 10, AlphaStart = 1f,
                Update = (t, p) => StepCandyCorn(p), Draw = (t, fx, p, px) => DrawGenericLit(fx, p, px), OnKill = (t, p) => KillCandyCorn(p),
                TileHit = (t, p, hx, hy, before) =>
                {
                    if (hx)
                    {
                        p.Vel.x = -before.x;
                        p.Ai1++;
                    }
                    if (hy)
                    {
                        p.Vel.y = -before.y;
                        p.Ai1++;
                    }
                    if (p.Ai1 > 4f) t.Kill(p);
                },
            };
            // P:4048, 18896: the jack 'o lantern, 32 px, 5 s, bouncing off walls; it bursts over 128 px.
            specs[ProjKind.JackOLanternProj] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_312", W = 32, H = 32, Time = 300, Penetrate = 1, AlphaStart = 1f, Light = new Color(0.6f, 0.4f, 0.3f),
                Update = (t, p) => StepJackOLantern(p), Draw = (t, fx, p, px) => DrawJackOLantern(fx, p, px), OnKill = (t, p) => t.KillJackOLantern(p),
                TileHit = (t, p, hx, hy, before) =>
                {
                    if (hx) p.Vel.x = -before.x;
                    if (hy) p.Vel.y = -before.y;
                },
            };
            // P:4162, 13557: the stake, four updates a tick, ten enemies (10% less a hit), at 0.8 size.
            specs[ProjKind.StakeProj] = new Spec
            {
                Ai = Ai.Arrow, Texture = "Projectile_323", W = 14, H = 14, Scale = 0.8f, Time = 3600, Extra = 3, Penetrate = 10, Falloff = 0.9f, AlphaStart = 1f,
                AlphaFade = 50f / 255f, PivotBack = 8f, OnKill = (t, p) => KillStake(p),
            };
            // P:5843, 13368, 14061: the nail, three enemies, AI_093.
            specs[ProjKind.NailProj] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_514", W = 10, H = 10, Time = 3600, Penetrate = 3, AlphaStart = 1f, NoImmunity = true,
                CanDamage = p => p.Ai0 == 0f || p.Ai0 == 2f, Update = (t, p) => t.StepNail(p), Draw = (t, fx, p, px) => DrawGenericLit(fx, p, px),
                OnHit = (t, p, hm) => NailStick(p, hm), OnKill = (t, p) => t.KillNail(p), TileHit = (t, p, hx, hy, before) => NailTile(p, before),
                ModifyHit = (t, p, hm, dealt, crit) => ((int)(dealt * (p.Ai0 == 2f ? 1.35f : 0.15f)), crit),
            };
            // P:7165, 13168: the mug, AI_068; a hit takes a fifth off and it bursts at once, 144 px.
            specs[ProjKind.AleMug] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_669", W = 14, H = 14, Time = 3600, Penetrate = -1, AlphaStart = 1f,
                Update = (t, p) => StepAleMug(p), Draw = (t, fx, p, px) => DrawGeneric(fx, p, px, new Color(L, L, L, 1f), p.Dir < 0f), OnKill = (t, p) => t.KillAleMug(p),
                TileHit = (t, p, hx, hy, before) =>
                {
                    p.Vel = Vector2.zero;
                    p.Alpha = 1f;
                    p.TimeLeft = 3;
                },
                OnHit = (t, p, hm) =>
                {
                    p.Damage = (int)(p.Damage * 0.8f);
                    p.TimeLeft = 1;
                    if (Random.Range(0, 20) == 0) EnemyDebuffs.Add(hm, EnemyDebuffs.Confused, 60 * Random.Range(2, 6));
                },
            };
            // P:10147, 14024, 19865: the acorn, 20 px, AI_016; gone on a hit, or once it stops.
            specs[ProjKind.AcornProj] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_1088", W = 20, H = 20, Time = 3600, Penetrate = -1, Frames = 2,
                Update = (t, p) => t.StepAcorn(p), Draw = (t, fx, p, px) => DrawRocket(fx, p, px), OnKill = (t, p) => HitTileDust(p),
                OnHit = (t, p, hm) => t.Kill(p),
                TileHit = (t, p, hx, hy, before) => RocketTile(p, hx, hy, before),
            };
            rockets[ProjKind.AcornProj] = (RocketFamily.Fragment, 0, 1088);
        }

        /// <summary>P:76269-76277: a soft crunch (Item51) and snow (76) flung back.</summary>
        static void KillSnowball(Proj p)
        {
            Sound("Item_51");
            var v = TV(p);
            var tl = CenterPx(p) - new Vector2(7f, 7f);
            for (int k = 0; k < 10; k++)
            {
                int i = BoxDust(tl, 14, 14, 76);
                if (i < 0) continue;
                D(i).NoGravity = true;
                D(i).Velocity -= v * 0.25f;
            }
        }

        // ------------------------------------------------------------------ 246 / 249: the Stynger

        /// <summary>AI_001 for 246 (P:58111-58122): showing 20 an update; after 60 falling 0.15. Turned along its flight.</summary>
        static void StepStyngerBolt(Proj p)
        {
            p.Alpha = Mathf.Max(0f, p.Alpha - 20f / 255f);
            var v = TV(p);
            if (++p.Ai0 >= 60f)
            {
                p.Ai0 = 60f;
                v.y += 0.15f;
            }
            p.TRot = ToRot(v) + 1.57f;
            p.Vel = Flip(v);
        }

        /// <summary>
        /// P:78645-78694: the boom (Item14), smoke, flames, a smoke gore; a 150 px blast; two to five
        /// pieces of shrapnel (249) flung out at 8 px.
        /// </summary>
        void KillStyngerBolt(Proj p)
        {
            Sound("Item_14");
            var c = CenterPx(p);
            var tl = c - new Vector2(5f, 5f);
            for (int k = 0; k < 10; k++)
            {
                int i = BoxDust(tl, 10, 10, 31, 0f, 0f, 100, 1.5f);
                if (i >= 0) D(i).Velocity *= 0.9f;
            }
            for (int k = 0; k < 5; k++)
            {
                int i = BoxDust(tl, 10, 10, 6, 0f, 0f, 100, 2.5f);
                if (i >= 0)
                {
                    D(i).NoGravity = true;
                    D(i).Velocity *= 3f;
                }
                i = BoxDust(tl, 10, 10, 6, 0f, 0f, 100, 1.5f);
                if (i >= 0) D(i).Velocity *= 2f;
            }
            TerraParticles.Instance?.NewGore(tl, Vector2.zero, Random.Range(61, 64), 1f, 0.3f, new Vector2(Random.Range(-1, 2), Random.Range(-1, 2)));
            Blast(p, 150f, p.Damage, p.Knockback);
            var from = TerraParticles.ToWorld(c - TV(p));
            var keep = SpawnClass;
            SpawnClass = p.Class;
            for (int k = Random.Range(2, 6); k > 0; k--)
            {
                var d = new Vector2(Random.Range(-100, 101) + 0.01f, Random.Range(-100, 101));
                if (d.sqrMagnitude < 1e-6f) d = Vector2.up;
                var q = Spawn(ProjKind.StyngerShrapnel, from, Flip(d.normalized * 8f), p.Damage, p.Knockback, p.Crit);
                if (q == null) continue;
                // P:11092: one of its five pieces (Main.projFrames[249] = 5).
                q.Frame = Random.Range(0, 5);
                DevStyngerShrapnel++;
            }
            SpawnClass = keep;
        }

        /// <summary>AI_002 for 249 (P:23736-23748): falling 0.25 from the start, tumbling.</summary>
        static void StepShrapnel(Proj p)
        {
            var v = TV(p);
            v.y += 0.25f;
            if (v.y > 32f) v.y = 32f;
            p.TRot += (Mathf.Abs(v.x) + Mathf.Abs(v.y)) * 0.03f * (v.x < 0f ? -1f : 1f);
            p.Vel = Flip(v);
        }

        /// <summary>P:78696-78727: a small boom, smoke, flames, a gore; a 100 px blast.</summary>
        void KillShrapnel(Proj p)
        {
            Sound("Item_14");
            var tl = CenterPx(p) - new Vector2(6f, 6f);
            for (int k = 0; k < 7; k++)
            {
                int i = BoxDust(tl, 12, 12, 31, 0f, 0f, 100, 1.5f);
                if (i >= 0) D(i).Velocity *= 0.8f;
            }
            for (int k = 0; k < 2; k++)
            {
                int i = BoxDust(tl, 12, 12, 6, 0f, 0f, 100, 2.5f);
                if (i >= 0)
                {
                    D(i).NoGravity = true;
                    D(i).Velocity *= 2.5f;
                }
                i = BoxDust(tl, 12, 12, 6, 0f, 0f, 100, 1.5f);
                if (i >= 0) D(i).Velocity *= 1.5f;
            }
            TerraParticles.Instance?.NewGore(tl, Vector2.zero, Random.Range(61, 64), 1f, 0.2f, new Vector2(Random.Range(-1, 2), Random.Range(-1, 2)));
            Blast(p, 100f, p.Damage, p.Knockback);
        }

        // ------------------------------------------------------------------ 311: the candy corn

        /// <summary>AI_001 for 311 (P:58124-58145, 58590-58598): showing 50 an update; after 30 falling 0.5 and tumbling (turned along its flight until then).</summary>
        static void StepCandyCorn(Proj p)
        {
            p.Alpha = Mathf.Max(0f, p.Alpha - 50f / 255f);
            var v = TV(p);
            if (++p.Ai0 >= 30f)
            {
                p.Ai0 = 30f;
                if (p.Ai1 == 0f) p.Ai1 = 1f;
                v.y += 0.5f;
            }
            if (p.Ai1 != 0f) p.TRot += v.x * 0.1f + Random.Range(-10, 11) * 0.025f;
            else p.TRot = ToRot(v) + 1.57f;
            p.Vel = Flip(v);
        }

        /// <summary>P:75750-75759: candy (189) scattered along its way.</summary>
        static void KillCandyCorn(Proj p)
        {
            var v = TV(p);
            var tl = CenterPx(p) - new Vector2(5f, 6f);
            for (int k = 0; k < 5; k++)
            {
                int i = BoxDust(tl, 10, 12, 189);
                if (i < 0) continue;
                D(i).Scale = 0.85f;
                D(i).NoGravity = true;
                D(i).Velocity += v * 0.5f;
            }
        }

        // ------------------------------------------------------------------ 312: the jack 'o lantern

        /// <summary>AI_001 for 312 (P:58152-58162, 58605-58608): shown after 5 updates; after 20 falling 0.5; lit warm; turning with its speed across.</summary>
        static void StepJackOLantern(Proj p)
        {
            var v = TV(p);
            if (++p.Ai0 >= 5f) p.Alpha = 0f;
            if (p.Ai0 >= 20f)
            {
                p.Ai0 = 30f;
                v.y += 0.5f;
            }
            p.TRot += v.x * 0.02f;
            p.Vel = Flip(v);
        }

        /// <summary>The lantern lit, its glowmask (Glow_257) added over it.</summary>
        static void DrawJackOLantern(FxBatch fx, Proj p, Vector2 px)
        {
            DrawGenericLit(fx, p, px);
            fx.Draw(FxBatch.Whole("Glow_257"), px, new Color(1f, 1f, 1f, 0f) * (1f - p.Alpha), p.TRot, p.Scale);
        }

        /// <summary>P:78584-78640: the boom (Item14), smoke, fire and smoke gores as Rocket I's; a 128 px blast.</summary>
        void KillJackOLantern(Proj p)
        {
            Sound("Item_14");
            var tl = CenterPx(p) - new Vector2(11f, 11f);
            for (int k = 0; k < 30; k++)
            {
                int i = BoxDust(tl, 22, 22, 31, 0f, 0f, 100, 1.5f);
                if (i >= 0) D(i).Velocity *= 1.4f;
            }
            for (int k = 0; k < 20; k++)
            {
                int i = BoxDust(tl, 22, 22, 6, 0f, 0f, 100, 3.5f);
                if (i >= 0)
                {
                    D(i).NoGravity = true;
                    D(i).Velocity *= 7f;
                }
                i = BoxDust(tl, 22, 22, 6, 0f, 0f, 100, 1.5f);
                if (i >= 0) D(i).Velocity *= 3f;
            }
            var pp = TerraParticles.Instance;
            if (pp != null)
                for (int r = 0; r < 2; r++)
                    foreach (var d in Diagonals) pp.NewGore(tl, Vector2.zero, Random.Range(61, 64), 1f, r == 0 ? 0.4f : 0.8f, d);
            Blast(p, 128f, p.Damage, p.Knockback);
        }

        /// <summary>P:75641-75650: a crack (the dig sound) and splinters of wood (7).</summary>
        static void KillStake(Proj p)
        {
            Sound("Dig_" + Random.Range(0, 3));
            var tl = CenterPx(p) - new Vector2(7f, 7f);
            for (int k = 0; k < 20; k++)
            {
                int i = BoxDust(tl, 14, 14, 7);
                if (i < 0 || Random.Range(0, 2) != 0) continue;
                D(i).NoGravity = true;
                D(i).Scale = 1.3f;
            }
        }

        // ------------------------------------------------------------------ 514: the nail (AI_093)

        /// <summary>
        /// AI_093 (P:33426-33487): showing 25 an update; in flight after 20 updates falling 0.1 and
        /// slowing 0.8%, turned along it; in a block it drifts to a stop; in an enemy it rides along
        /// (sinking in); either way it goes off 90 updates later. With its enemy gone it falls out.
        /// </summary>
        void StepNail(Proj p)
        {
            p.Alpha = Mathf.Max(0f, p.Alpha - 25f / 255f);
            var v = TV(p);
            if (v.y > 18f) v.y = 18f;
            if (p.Ai0 == 0f)
            {
                if (++p.Ai1 > 20f)
                {
                    v.y += 0.1f;
                    v.x *= 0.992f;
                }
                p.TRot = ToRot(v) + 1.57f;
                p.Vel = Flip(v);
                return;
            }
            p.Collide = false;
            if (p.Ai0 == 1f) v *= 0.6f;
            else
            {
                var t = p.Target;
                if (t == null || t.isDead || !t.gameObject.activeInHierarchy)
                {
                    p.Collide = true;
                    p.Ai0 = 0f;
                    p.Ai1 = 20f;
                    var d = new Vector2(Random.Range(-100, 101), Random.Range(-100, 101));
                    v = (d.sqrMagnitude > 1e-6f ? d.normalized : Vector2.up) * 6f;
                    p.Target = null;
                }
                else
                {
                    var col = t.GetComponent<Collider2D>();
                    float size = col != null ? (col.bounds.size.x + col.bounds.size.y) / Px / 3f : 20f;
                    if (v.magnitude > size) v *= 0.99f;
                    // Not moved on by its velocity while in an enemy (P:20552): where it struck, after the step adds it.
                    p.Pos = TerraParticles.ToWorld(EnemyCenterPx(t) - v) - Flip(v) * Px;
                }
            }
            p.Vel = Flip(v);
            if (p.Ai0 != 0f && ++p.Ai1 > 90f) Kill(p);
        }

        /// <summary>P:13368-13378: a nail that hits sticks in where it struck (at 15% for that hit).</summary>
        static void NailStick(Proj p, HealthManager hm)
        {
            if (p.Ai0 != 0f) return;
            p.Ai1 = 0f;
            p.Ai0 = -1f;
            p.Target = hm;
            p.Vel = Flip(EnemyCenterPx(hm) - CenterPx(p));
            DevNailsStuck++;
        }

        /// <summary>P:18932-18944: into a block it sinks by its step and creeps on at 3 px, slowing.</summary>
        static void NailTile(Proj p, Vector2 before)
        {
            if (p.Ai0 != 0f) return;
            p.Ai1 = 0f;
            p.Ai0 = 1f;
            p.Collide = false;
            p.Pos += before * Px;
            p.Vel = before.sqrMagnitude > 1e-6f ? before.normalized * 3f : Vector2.zero;
        }

        /// <summary>P:77986-78025: the boom (Item14), smoke and flames; a 112 px blast at 135%.</summary>
        void KillNail(Proj p)
        {
            Sound("Item_14");
            var tl = CenterPx(p) - new Vector2(5f, 5f);
            for (int k = 0; k < 10; k++)
            {
                int i = BoxDust(tl, 10, 10, 31, 0f, 0f, 100, 1.3f);
                if (i >= 0) D(i).Velocity *= 1.4f;
            }
            for (int k = 0; k < 6; k++)
            {
                int i = BoxDust(tl, 10, 10, 6, 0f, 0f, 100, 2.1f);
                if (i >= 0)
                {
                    D(i).NoGravity = true;
                    D(i).Velocity *= 4.6f;
                }
                i = BoxDust(tl, 10, 10, 6, 0f, 0f, 100, 1.3f);
                if (i >= 0) D(i).Velocity *= 3.3f;
                if (Random.Range(0, 2) == 0 && (i = BoxDust(tl, 10, 10, 6, 0f, 0f, 100, 1.1f)) >= 0) D(i).Velocity *= 2.7f;
            }
            p.Ai0 = 2f;
            // Blast normally excludes the enemy just hit by an immediate impact.
            // A nail detonates 90 ticks after sticking: its attached enemy is the
            // primary target of that explosion, not a permanently excluded victim.
            p.LastHit = 0;
            var hit = Blast(p, 112f, (int)(p.Damage * 1.35f), p.Knockback);
            if (p.Target != null && hit.Contains(p.Target.GetInstanceID())) DevNailTargetBlasts++;
        }

        // ------------------------------------------------------------------ 669: the Ale Tosser's mug (AI_068)

        /// <summary>
        /// AI_068 for 669 (P:30655-30700): tumbling its way; showing 40 an update after 3; after 15
        /// falling 0.2 (to 16) and slowing 1%; foam (4, white and golden) frothing off it once seen.
        /// </summary>
        static void StepAleMug(Proj p)
        {
            var v = TV(p);
            if (p.Ai0 == 0f) p.Dir = v.x < 0f ? -1f : 1f;
            p.TRot += 0.25f * p.Dir;
            if (++p.Ai0 >= 3f) p.Alpha = Mathf.Max(0f, p.Alpha - 40f / 255f);
            if (p.Ai0 >= 15f)
            {
                v.y = Mathf.Min(16f, v.y + 0.2f);
                v.x *= 0.99f;
            }
            if (p.Alpha <= 0f)
            {
                var c = CenterPx(p);
                for (int k = 0; k < 2; k++)
                {
                    var sp = RotateBy(new Vector2(Mathf.Lerp(-8f, 8f, Random.value), -4f), p.TRot);
                    int i = BoxDust(c + sp - Vector2.one * 5f, 4, 4, 4);
                    if (i < 0) continue;
                    D(i).Scale = 0.8f - Random.value * 0.2f;
                    D(i).Velocity = RotateBy(D(i).Velocity * 0.25f + sp.normalized, -Mathf.PI / 2f * p.Dir);
                    D(i).Color = Random.Range(0, 2) == 0 ? FxBatch.C(255, 255, 255, 110) : FxBatch.C(245, 200, 30, 110);
                }
            }
            p.Vel = Flip(v);
        }

        /// <summary>P:73195-73238: the mug shatters (Shatter): white and golden foam (4) everywhere, and the ale's 144 px splash.</summary>
        void KillAleMug(Proj p)
        {
            Sound("Shatter");
            var c = CenterPx(p);
            var tl = c - new Vector2(15f, 15f);
            for (int k = 0; k < 12; k++)
            {
                int i = BoxDust(tl, 30, 30, 4, 0f, 0f, 100, 1.1f);
                if (i < 0) continue;
                D(i).Color = FxBatch.C(255, 255, 255, 110);
                D(i).Velocity *= 1.5f;
            }
            for (int k = 0; k < 80; k++)
            {
                int i = BoxDust(tl, 30, 30, 4, 0f, 0f, 50, 1.2f);
                if (i >= 0)
                {
                    D(i).Color = FxBatch.C(245, 200, 30, 155);
                    D(i).NoGravity = true;
                    D(i).Velocity *= Random.Range(10, 71) * 0.1f;
                }
                i = BoxDust(tl, 30, 30, 4, 0f, 0f, 50, 0.8f);
                if (i < 0) continue;
                D(i).Color = FxBatch.C(245, 200, 30, 155);
                D(i).Velocity *= Random.Range(10, 41) * 0.1f;
            }
            Blast(p, 144f, p.Damage, p.Knockback);
        }

        // ------------------------------------------------------------------ 1088: the acorn (AI_016)

        /// <summary>AI_016 for 1088 (P:48031-48068, 48378-48390): one of two looks; after 10 updates falling 0.2, rolling to a stop (and gone once stopped), tumbling.</summary>
        void StepAcorn(Proj p)
        {
            var v = TV(p);
            if (p.LocalAi0 == 0f)
            {
                p.LocalAi0 = 1f;
                p.Frame = Random.Range(0, 2);
            }
            if (p.Ai0 > 0f && v.magnitude <= 0.01f)
            {
                Kill(p);
                return;
            }
            if (++p.Ai0 > 10f)
            {
                p.Ai0 = 10f;
                if (v.y == 0f && v.x != 0f)
                {
                    v.x *= 0.97f;
                    if (Mathf.Abs(v.x) < 0.01f) v.x = 0f;
                }
                v.y += 0.2f;
            }
            p.TRot += v.x * 0.1f;
            p.Vel = Flip(v);
        }
    }
}
