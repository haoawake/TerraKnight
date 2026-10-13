using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The first hardmode spells (Terraria's space, px y down, per update): the Crystal Storm's
    /// shards (94, AI_024: slowing, spinning, shrinking away, off walls), the Cursed Flames' ball
    /// (95, AI_008: falls after 20, off five walls, Cursed Inferno), the Golden Shower's stream
    /// (280, AI_012: ichor dust, Ichor), the Frost Staff's bolt (359, AI_028: Frostbite), the Poison
    /// and Venom Staffs' fangs (265/355, AI_001), the Unholy Trident (114, AI_027: slows and fades,
    /// Shadowflame, through armour), the Meteor Staff's meteors (424-426: through blocks until near
    /// the player's height, bursting over 128 px), the Spectre Staff's wisps (297, AI_051: homing)
    /// and the Inferno Fork's bolt (295) and lingering blaze (296, AI_050: Hellfire).
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        static void RegisterMagic()
        {
            specs[ProjKind.CrystalStormShard] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_94", W = 8, H = 8, Scale = 1.2f, Time = 600, Penetrate = 1,
                Update = (t, p) => t.StepCrystalShard(p), Draw = (t, fx, p, px) => DrawCrystalShard(fx, p, px),
                TileHit = (t, p, hx, hy, ov) => Reflect(p, hx, hy, ov), OnKill = (t, p) => { },
            };
            specs[ProjKind.CursedFlame] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_95", W = 16, H = 16, Time = 3600, Penetrate = 2, Look = Look.NearlyAdded,
                Light = new Color(0.28f, 0.8f, 0f), Debuffs = new[] { (EnemyDebuffs.CursedInferno, 420, 1) },
                Update = (t, p) => StepCursedFlame(p), TileHit = (t, p, hx, hy, ov) => t.BounceCursedFlame(p, hx, hy, ov), OnKill = (t, p) => KillCursedFlame(p),
            };
            specs[ProjKind.GoldenShowerStream] = new Spec
            {
                Ai = Ai.Custom, W = 32, H = 32, Time = 3600, Penetrate = 5, Extra = 2, Look = Look.Invisible, TileBox = new Vector2(10f, 10f),
                Debuffs = new[] { (EnemyDebuffs.Ichor, 600, 1) }, Update = (t, p) => t.StepGoldenShower(p), OnKill = (t, p) => { },
            };
            specs[ProjKind.FrostBolt] = new Spec
            {
                Ai = Ai.Custom, W = 14, H = 14, Time = 3600, Penetrate = 2, Look = Look.Invisible, Debuffs = new[] { (EnemyDebuffs.Frostbite, 420, 1) },
                Update = (t, p) => StepFrostBolt(p), OnKill = (t, p) => KillFrostBolt(p),
            };
            specs[ProjKind.PoisonFang] = new Spec
            {
                Ai = Ai.Arrow, Texture = "Projectile_265", W = 12, H = 12, Time = 37, Penetrate = 3, NoGravity = true, LocalCooldown = 10, AlphaStart = 1f, AlphaFade = 50f / 255f,
                PivotBack = 3f, Debuffs = new[] { (EnemyDebuffs.Poisoned, 1800, 1) }, Update = (t, p) => FangDust(p, 163),
                Draw = (t, fx, p, px) => DrawFang(fx, p, px), OnKill = (t, p) => KillFang(p, 163),
            };
            specs[ProjKind.VenomFang] = new Spec
            {
                Ai = Ai.Arrow, Texture = "Projectile_355", W = 12, H = 12, Time = 58, Penetrate = 5, NoGravity = true, LocalCooldown = 10, AlphaStart = 1f, AlphaFade = 50f / 255f,
                PivotBack = 3f, Debuffs = new[] { (EnemyDebuffs.Venom, 1800, 1) }, Update = (t, p) => FangDust(p, 205),
                Draw = (t, fx, p, px) => DrawFang(fx, p, px), OnKill = (t, p) => KillFang(p, 205),
            };
            specs[ProjKind.UnholyTrident] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_114", W = 16, H = 16, Time = 180, Penetrate = 1, Extra = 2, AlphaStart = 1f, ArmorPen = 25,
                TileBox = new Vector2(4f, 4f), Light = new Color(0.25f, 0.025f, 0.5f),
                Update = (t, p) => t.StepUnholyTrident(p), Draw = (t, fx, p, px) => DrawUnholyTrident(fx, p, px),
                OnHit = (t, p, hm) => EnemyDebuffs.Add(hm, EnemyDebuffs.Shadowflame, Random.Range(300, 600)),
                TileHit = (t, p, hx, hy, ov) => StopUnholyTrident(p, ov), OnKill = (t, p) => KillUnholyTrident(p),
            };
            specs[ProjKind.Meteor] = new Spec
            {
                Ai = Ai.Custom, W = 24, H = 24, Time = 3600, Penetrate = 1, Extra = 2, TileCollide = false,
                Update = (t, p) => t.StepMeteor(p), Draw = (t, fx, p, px) => DrawMeteor(fx, p, px), OnKill = (t, p) => t.KillMeteor(p),
            };
            specs[ProjKind.SpectreWisp] = new Spec
            {
                Ai = Ai.Custom, W = 12, H = 12, Time = 3600, Penetrate = 3, Extra = 1, TypeCooldown = 10, Look = Look.Invisible,
                Update = (t, p) => StepSpectreWisp(p), OnKill = (t, p) => { },
            };
            specs[ProjKind.InfernoBolt] = new Spec
            {
                Ai = Ai.Custom, W = 12, H = 12, Time = 3600, Penetrate = 1, Look = Look.Invisible,
                OnHit = (t, p, hm) => EnemyDebuffs.Add(hm, EnemyDebuffs.Hellfire, 60 * Random.Range(8, 16)),
                Update = (t, p) => InfernoDust(p, 8, 1.2f), OnKill = (t, p) => t.KillInfernoBolt(p),
            };
            specs[ProjKind.InfernoBlast] = new Spec
            {
                Ai = Ai.Custom, W = 150, H = 150, Time = 3600, Penetrate = -1, TileCollide = false, Look = Look.Invisible, PushFromCentre = true,
                OnHit = (t, p, hm) => EnemyDebuffs.Add(hm, EnemyDebuffs.Hellfire, 60 * Random.Range(8, 16)),
                Update = (t, p) => t.StepInfernoBlast(p), OnKill = (t, p) => { },
            };
        }

        /// <summary>A Meteor Staff meteor (world position, px per update y up): which of the three sprites, and its size.</summary>
        public void ShootMeteor(Vector2 from, Vector2 velocity, int damage, float knockback, float crit, int variant, float scale)
        {
            var p = Spawn(ProjKind.Meteor, from, velocity, damage, knockback, crit);
            if (p == null) return;
            p.LocalAi1 = variant;
            p.Ai1 = scale;
            p.Scale = scale;
        }

        /// <summary>Straight back off whatever face it met.</summary>
        static void Reflect(Proj p, bool hitX, bool hitY, Vector2 oldVel)
        {
            if (hitX) p.Vel.x = -oldVel.x;
            if (hitY) p.Vel.y = -oldVel.y;
        }

        // ------------------------------------------------------------------ 94: Crystal Storm (AI_024)

        /// <summary>P:26296: purple sparks, 1.5% slower an update; after 130 updates it shrinks away. It spins with its speed across.</summary>
        void StepCrystalShard(Proj p)
        {
            var v = TV(p);
            p.TRot += v.x * 0.2f;
            p.Ai1++;
            if (Random.Range(0, 4) == 0)
            {
                int i = BoxDust(SpearBox(p, out int w, out int h), w, h, 70);
                if (i >= 0)
                {
                    D(i).NoGravity = true;
                    D(i).Velocity *= 0.5f;
                    D(i).Scale *= 0.9f;
                }
            }
            p.Vel *= 0.985f;
            if (p.Ai1 > 130f)
            {
                p.Scale -= 0.05f;
                if (p.Scale <= 0.2f)
                {
                    p.Scale = 0.2f;
                    Kill(p);
                }
            }
        }

        static void DrawCrystalShard(FxBatch fx, Proj p, Vector2 px)
        {
            // Light by its size (light = scale / 2, purple: ProjLight 0.5, 0, 1).
            float l = p.Scale * 0.5f;
            fx.Light(px, new Color(0.5f * l, 0f, l), 2.5f);
            fx.Draw(FxBatch.Frame("Projectile_94", 0, 0, 6, 6, 3f, 4.5f), px + new Vector2(-1.5f, 0f), FxBatch.C(200, 200, 200, 25), p.TRot, p.Scale);
        }

        // ------------------------------------------------------------------ 95: Cursed Flames (AI_008)

        static void StepCursedFlame(Proj p)
        {
            var v = TV(p);
            var tl = CenterPx(p) - new Vector2(8f, 8f) + v;
            int i = BoxDust(tl, 16, 16, 75, v.x, v.y, 100, 3f * p.Scale);
            if (i >= 0) D(i).NoGravity = true;
            p.Ai1++;
            if (p.Ai1 >= 20f) p.Vel.y = Mathf.Max(p.Vel.y - 0.2f, -16f);
            p.Rot -= 0.3f * Mathf.Rad2Deg * (p.Vel.x < 0f ? -1f : 1f);
        }

        /// <summary>P:19689: a hiss (Item10) off each wall, gone on the fifth.</summary>
        void BounceCursedFlame(Proj p, bool hitX, bool hitY, Vector2 oldVel)
        {
            Sound("Item_10");
            if (++p.Bounces >= 5)
            {
                p.Pos += p.Vel * Px;
                Kill(p);
                return;
            }
            Reflect(p, hitX, hitY, oldVel);
        }

        static void KillCursedFlame(Proj p)
        {
            Sound("Item_10");
            var v = TV(p);
            var tl = CenterPx(p) - new Vector2(8f, 8f);
            for (int k = 0; k < 20; k++)
            {
                int i = BoxDust(tl, 16, 16, 75, -v.x * 0.2f, -v.y * 0.2f, 100, 2f * p.Scale);
                if (i >= 0)
                {
                    D(i).NoGravity = true;
                    D(i).Velocity *= 2f;
                }
                i = BoxDust(tl, 16, 16, 75, -v.x * 0.2f, -v.y * 0.2f, 100, p.Scale);
                if (i >= 0) D(i).Velocity *= 2f;
            }
        }

        // ------------------------------------------------------------------ 280: Golden Shower (AI_012)

        /// <summary>P:24795: it dries up (scale -0.002 an update); after 3 updates it falls (0.075) shedding three ichor motes along its step.</summary>
        void StepGoldenShower(Proj p)
        {
            p.Scale -= 0.002f;
            if (p.Scale <= 0f)
            {
                Kill(p);
                return;
            }
            if (p.Ai0 <= 3f)
            {
                p.Ai0++;
                return;
            }
            var v = TV(p);
            v.y += 0.075f;
            p.Vel = Flip(v);
            var c = CenterPx(p);
            for (int k = 0; k < 3; k++)
            {
                int i = BoxDust(c - new Vector2(2f, 2f), 4, 4, 170, 0f, 0f, 100);
                if (i < 0) continue;
                D(i).NoGravity = true;
                D(i).Velocity = D(i).Velocity * 0.1f + v * 0.5f;
                D(i).Position -= v / 3f * k;
            }
            if (Random.Range(0, 8) == 0)
            {
                int i = BoxDust(c, 0, 0, 170, 0f, 0f, 100, 0.5f);
                if (i >= 0) D(i).Velocity = D(i).Velocity * 0.25f + v * 0.5f;
            }
        }

        // ------------------------------------------------------------------ 359: Frost Staff (AI_028)

        /// <summary>P:26987: unseen, three frost motes an update; a crisp sound (Item28) as it sets off.</summary>
        static void StepFrostBolt(Proj p)
        {
            if (p.LocalAi1 == 0f)
            {
                p.LocalAi1 = 1f;
                Sound("Item_28");
            }
            var v = TV(p);
            var tl = CenterPx(p) - new Vector2(7f, 7f);
            for (int k = 0; k < 3; k++)
            {
                int i = BoxDust(tl, 14, 14, 92, v.x, v.y, 50, 1.2f);
                if (i < 0) continue;
                D(i).NoGravity = true;
                D(i).Velocity *= 0.3f;
            }
        }

        static void KillFrostBolt(Proj p)
        {
            Sound("Item_27");
            var tl = CenterPx(p) - new Vector2(7f, 7f);
            for (int k = 0; k < 20; k++)
            {
                int i = BoxDust(tl, 14, 14, 92);
                if (i < 0 || Random.Range(0, 3) == 0) continue;
                D(i).Velocity *= 2f;
                D(i).NoGravity = true;
            }
        }

        // ------------------------------------------------------------------ 265/355: the fangs (AI_001)

        /// <summary>P:57691: once faded in (50 an update), one mote an update trailing behind.</summary>
        static void FangDust(Proj p, int type)
        {
            if (p.Alpha > 0f) return;
            var v = TV(p);
            int i = BoxDust(CenterPx(p) - new Vector2(6f, 6f), 12, 12, type, v.x, v.y, 100, 1.2f);
            if (i < 0) return;
            D(i).NoGravity = true;
            D(i).Velocity = D(i).Velocity * 0.3f - v * 0.4f;
        }

        /// <summary>GetAlpha for 265/355: nothing while fading in, then white and added.</summary>
        static void DrawFang(FxBatch fx, Proj p, Vector2 px)
        {
            if (p.Alpha > 0f) return;
            var rot = p.Rot * Mathf.Deg2Rad;
            var back = new Vector2(Mathf.Sin(rot), Mathf.Cos(rot)) * p.Spec.PivotBack * p.Scale;
            fx.Draw(Sprite(p.Spec.Texture), px + back, new Color(1f, 1f, 1f, 0f), -rot, p.Scale);
        }

        static void KillFang(Proj p, int type)
        {
            var v = TV(p);
            var tl = CenterPx(p) - new Vector2(6f, 6f);
            for (int k = 0; k < 15; k++)
            {
                int i = BoxDust(tl, 12, 12, type, 0f, 0f, 100, 1.2f);
                if (i < 0) continue;
                D(i).NoGravity = true;
                D(i).Velocity = D(i).Velocity * 1.2f - v * 0.3f;
            }
        }

        // ------------------------------------------------------------------ 114: Unholy Trident (AI_027)

        /// <summary>
        /// P:26779: drawn half again as big; after 20 updates it slows by 2% an update and is gone
        /// below 1 px; it shows (alpha from its speed: full until 2 px, none at 0) and sheds
        /// shadowflame while fast; it fades in over its first 15 updates (GetAlpha).
        /// </summary>
        void StepUnholyTrident(Proj p)
        {
            p.Scale = 1.5f;
            p.Ai0++;
            if (p.LocalAi0 == 0f) p.LocalAi0 = TV(p).magnitude;
            if (p.Ai0 >= 20f)
            {
                p.Vel *= 0.98f;
                p.LocalAi0 *= 0.98f;
                if (p.LocalAi0 < 1f)
                {
                    Kill(p);
                    return;
                }
            }
            float k = Mathf.Clamp01(p.LocalAi0 / 2f);
            p.Alpha = k;
            var v = TV(p);
            var tl = CenterPx(p) - new Vector2(8f, 4f);
            if (Random.Range(0, 2) == 0 && Random.value + 0.2f < k)
            {
                int i = BoxDust(tl, 8, 8, 27, v.x, v.y, 100, 1f + 0.6f * Random.value);
                if (i >= 0)
                {
                    D(i).Velocity *= 0.5f;
                    D(i).Position -= D(i).Velocity * 5f;
                    D(i).Position += Random.insideUnitCircle * 16f;
                }
            }
            if (p.LocalAi1 < 15f) p.LocalAi1++;
            else
            {
                int i = BoxDust(tl, 8, 8, 27, v.x, v.y, 100, 0.6f);
                if (i >= 0) D(i).Velocity *= -0.25f;
            }
            if (p.Ai1 == 0f)
            {
                p.Ai1 = 1f;
                Sound("Item_8");
            }
            p.TRot = Mathf.Atan2(v.y, v.x) + 0.785f;
        }

        /// <summary>P:18191: it stops dead against a wall (and slows to nothing there).</summary>
        static void StopUnholyTrident(Proj p, Vector2 oldVel)
        {
            p.Pos += oldVel * 0.99f * Px;
            p.Vel = oldVel.normalized * 0.0001f;
        }

        static void KillUnholyTrident(Proj p)
        {
            Sound("Item_10");
            var v = TV(p);
            var c = CenterPx(p) - new Vector2(8f, 8f);
            int alpha = Mathf.RoundToInt((1f - p.Alpha) * 255f);
            for (int k = 4; k < 31; k++)
            {
                if (Random.Range(0, 128) < alpha) continue;
                int i = BoxDust(c - v * (30f / k), 8, 8, 27, v.x, v.y, 100, 1.4f);
                if (i < 0) continue;
                D(i).NoGravity = true;
                D(i).Velocity *= 0.5f;
            }
        }

        /// <summary>
        /// Main.DrawProj for 114: pivot 12 px in from its top-right corner, pointing along its
        /// flight; GetAlpha: unseen for 5 updates, greying in until 15, then white, all faded by
        /// how slow it has got.
        /// </summary>
        static void DrawUnholyTrident(FxBatch fx, Proj p, Vector2 px)
        {
            if (p.LocalAi1 < 5f) return;
            Color c;
            if (p.LocalAi1 >= 15f) c = Color.white;
            else
            {
                float n = (p.LocalAi1 - 5f) / 10f;
                c = new Color(n, n, n, n);
            }
            fx.Draw(FxBatch.Frame("Projectile_114", 0, 0, 48, 48, 36f, 12f), px, c * p.Alpha, p.TRot, p.Scale);
        }

        // ------------------------------------------------------------------ 424-426: Meteor Staff

        /// <summary>
        /// P:58010: through blocks until within 300 px of the player's height; scale from ai[1]
        /// (0.5-0.8); it spins with its speed across and trails fire on both flanks.
        /// </summary>
        void StepMeteor(Proj p)
        {
            var hero = HeroController.SilentInstance;
            if (hero != null && p.Pos.y - p.Spec.H * 0.5f * Px < hero.GetComponent<BoxCollider2D>().bounds.max.y + 300f * Px) p.Collide = true;
            p.Scale = p.Ai1;
            var v = TV(p);
            p.TRot += v.x * 2f;
            var c = CenterPx(p);
            var head = c + (v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.down) * 10f;
            var side = new Vector2(-v.y, v.x);
            for (int s = -1; s <= 1; s += 2)
            {
                int i = PointDust(head + side * s, 6, side * s * 0.33f + v / 4f);
                if (i < 0) continue;
                D(i).FadeIn = 0.5f;
                D(i).NoGravity = true;
            }
            int j = BoxDust(c - new Vector2(12f, 12f), 24, 24, 6);
            if (j >= 0)
            {
                D(j).Velocity *= 0.5f;
                D(j).Scale *= 1.3f;
                D(j).FadeIn = 1f;
                D(j).NoGravity = true;
            }
        }

        /// <summary>
        /// Projectile.Kill for 424-426: a blast (Item89) over 128 px times its scale hitting
        /// everyone in it but the one it struck, smoke, fire and two smoke clouds.
        /// </summary>
        void KillMeteor(Proj p)
        {
            Sound("Item_89");
            float size = 128f * p.Scale;
            var c = CenterPx(p);
            var tl = c - new Vector2(size, size) * 0.5f;
            for (int k = 0; k < 8; k++) BoxDust(tl, (int)size, (int)size, 31, 0f, 0f, 100, 1.5f);
            for (int k = 0; k < 32; k++)
            {
                int i = BoxDust(tl, (int)size, (int)size, 6, 0f, 0f, 100, 2.5f);
                if (i >= 0)
                {
                    D(i).NoGravity = true;
                    D(i).Velocity *= 3f;
                }
                i = BoxDust(tl, (int)size, (int)size, 6, 0f, 0f, 100, 1.5f);
                if (i >= 0)
                {
                    D(i).Velocity *= 2f;
                    D(i).NoGravity = true;
                }
            }
            for (int k = 0; k < 2; k++)
                TerraParticles.Instance?.NewGore(tl + new Vector2(size * Random.Range(0, 100) / 100f, size * Random.Range(0, 100) / 100f) - Vector2.one * 10f,
                    new Vector2(Random.Range(-10, 11) * 0.05f, Random.Range(-10, 11) * 0.05f), Random.Range(61, 64), 1f, 0.3f);
            Blast(p, size, p.Damage, p.Knockback);
        }

        /// <summary>The meteor's own sprite (424, 425 or 426), lit, at Main.DrawProj's generic pivot, turning.</summary>
        static void DrawMeteor(FxBatch fx, Proj p, Vector2 px)
        {
            int n = (int)p.LocalAi1;
            int tw = n == 1 ? 32 : 36, th = n == 1 ? 34 : 36;
            fx.Draw(FxBatch.Frame("Projectile_" + (424 + n), 0, 0, tw, th, tw * 0.5f, 12f), px + new Vector2(tw * 0.5f - 12f, 0f), new Color(L, L, L, 1f), p.TRot, p.Scale);
        }

        // ------------------------------------------------------------------ 297: Spectre Staff (AI_051)

        /// <summary>
        /// P:28828: unseen; after 4 updates, five still wisps an update; it bends towards the nearest
        /// enemy in sight within 400 px (Manhattan) at 6 px an update, (v·20 + aim)/21.
        /// </summary>
        static void StepSpectreWisp(Proj p)
        {
            p.LocalAi0++;
            var c = CenterPx(p);
            if (p.LocalAi0 > 4f)
                for (int k = 0; k < 5; k++)
                {
                    int i = BoxDust(c - new Vector2(6f, 6f), 12, 12, 175, 0f, 0f, 100, 2f);
                    if (i < 0) continue;
                    D(i).NoGravity = true;
                    D(i).Velocity = Vector2.zero;
                }
            float best = 400f;
            Vector2? aim = null;
            foreach (var hm in EnemiesNear(c, 400f))
            {
                var e = EnemyCenterPx(hm);
                float m = Mathf.Abs(c.x - e.x) + Mathf.Abs(c.y - e.y);
                if (m < best && InSight(c, e))
                {
                    best = m;
                    aim = e;
                }
            }
            if (!aim.HasValue) return;
            var v = TV(p);
            var want = (aim.Value - c).normalized * 6f;
            p.Vel = Flip((v * 20f + want) / 21f);
        }

        // ------------------------------------------------------------------ 295/296: Inferno Fork (AI_050)

        /// <summary>Unseen; eight inferno motes an update (P:28770).</summary>
        static void InfernoDust(Proj p, int n, float scale)
        {
            var v = TV(p);
            var tl = CenterPx(p) - new Vector2(6f, 6f);
            for (int k = 0; k < n; k++)
            {
                int i = BoxDust(tl, 12, 12, 174, 0f, 0f, 100, scale);
                if (i < 0) continue;
                D(i).NoGravity = true;
                D(i).Velocity = D(i).Velocity * 0.5f + v * 0.1f;
            }
        }

        /// <summary>P:75810: where it ends, the blaze (296) at three quarters of its damage.</summary>
        void KillInfernoBolt(Proj p)
        {
            var b = Spawn(ProjKind.InfernoBlast, p.Pos, Vector2.zero, (int)(p.Damage * 0.75f), p.Knockback, p.Crit);
            if (b != null) b.Melee = p.Melee;
        }

        /// <summary>
        /// AI_050 for 296: a roar (Item74) as it starts; it burns for about 148 updates (ai[0]
        /// four an update, dying away from 540), throwing up to 17 sparks an update out from
        /// within 10 px of its centre.
        /// </summary>
        void StepInfernoBlast(Proj p)
        {
            if (p.LocalAi0 == 0f)
            {
                p.LocalAi0 = 1f;
                Sound("Item_74");
            }
            p.Ai0 += 4f;
            float n = 25f;
            if (p.Ai0 > 540f) n -= (p.Ai0 - 180f) / 2f;
            if (n <= 0f)
            {
                Kill(p);
                return;
            }
            n *= 0.7f;
            var c = CenterPx(p);
            for (int k = 0; k < n; k++)
            {
                var d = new Vector2(Random.Range(-10, 11), Random.Range(-10, 11));
                if (d.sqrMagnitude < 1e-4f) d = Vector2.right;
                d = d.normalized * Random.Range(3, 9);
                int i = PointDust(c + new Vector2(Random.Range(-10, 11), Random.Range(-10, 11)), 174, d, 100, 1.5f);
                if (i >= 0) D(i).NoGravity = true;
            }
        }
    }
}
