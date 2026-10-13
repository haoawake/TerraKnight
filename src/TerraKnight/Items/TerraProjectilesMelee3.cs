using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The swords' shots (sweep S1.C; Terraria's space, px y down): the Frostbrand's bolt (119,
    /// AI_028), the Beam Sword's beam (116, AI_027, bouncing off blocks three times), the Ice and
    /// Death Sickles' scythes (263, 274, AI_018), the Flying Dragon's sonic boom (684), and the
    /// Chlorophyte Claymore's spores (1128, AI_092) and the slam's impact (1129, AI_209).
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        static void RegisterMelee3()
        {
            // The plain spears (DefaultToSpear; AI_019_Spears_GetSpearOffsetRelativeToPlayer's terms, P:46947-47037).
            void Spear(ProjKind k, int tex, float scale, float n6, float n7, float n8) =>
                specs[k] = new Spec
                {
                    Ai = Ai.Spear, Texture = "Projectile_" + tex, W = 18, H = 18, Scale = scale, Time = 3600, Penetrate = -1, TileCollide = false,
                    OwnerHitCheck = true, SpearOffset = new Vector3(n6, n7, n8),
                };
            Spear(ProjKind.SpearThrust, 49, 1.2f, 4f, 0.85f, 1.1f);
            Spear(ProjKind.RottedForkThrust, 153, 1.1f, 4f, 1.3f, 1.5f);
            Spear(ProjKind.SwordfishThrust, 368, 1f, 3f, 1.4f, 1.5f);
            Spear(ProjKind.ObsidianSwordfishThrust, 367, 1.1f, 3f, 1.5f, 1.6f);
            Spear(ProjKind.SlimeSpearThrust, 1103, 1.2f, 4f, 0.85f, 1.1f);
            // The Rotted Fork's ichor-green motes (115) streaming off it (P:46883-46889).
            specs[ProjKind.RottedForkThrust].Update = (t, p) =>
            {
                var v = TV(p);
                int i = BoxDust(CenterPx(p) - new Vector2(9f, 9f) - v * 3f, 18, 18, 115, v.x * 0.4f, v.y * 0.4f, 140);
                if (i < 0) return;
                D(i).NoGravity = true;
                D(i).FadeIn = 1.25f;
                D(i).Velocity *= 0.25f;
            };
            // The Slime Spear (1.4.5): blue slime (4) off its tail one update in three (P:46893-46910); Slimed 3 s on a hit (P:11726-11729).
            specs[ProjKind.SlimeSpearThrust].Update = (t, p) =>
            {
                if (Random.Range(0, 3) != 0) return;
                var v = TV(p);
                int i = BoxDust(CenterPx(p) - new Vector2(9f, 9f) - v * 8f, 18, 18, 4, v.x, v.y, 84);
                if (i < 0) return;
                ref var d = ref D(i);
                d.Color = FxBatch.C(78, 136, 255, 127);
                d.Scale = 1f + Random.value * 0.3f;
                d.Velocity += v * 0.5f;
                if (Random.Range(0, 4) != 0)
                {
                    d.Velocity *= 0.7f;
                    d.NoGravity = true;
                    d.Scale += 0.2f;
                }
                else
                {
                    d.Velocity *= 0.3f;
                    d.Scale -= 0.5f;
                }
            };
            specs[ProjKind.SlimeSpearThrust].Debuffs = new[] { (EnemyDebuffs.Slimed, 180, 1) };
            // P:2082-2094: 14×14, unseen but for its frost, through three, one hit in 10 ticks for all of them; Frostbite 4 s (P:11303).
            specs[ProjKind.FrostbrandBolt] = new Spec
            {
                Ai = Ai.Custom, W = 14, H = 14, Time = 3600, Penetrate = 3, TypeCooldown = 10, Look = Look.Invisible, Debuffs = new[] { (EnemyDebuffs.Frostbite, 240, 1) },
                Update = (t, p) => StepFrostbrandBolt(p), Draw = (t, fx, p, px) => { }, OnKill = (t, p) => KillFrostbrandBolt(p),
            };
            // P:2049-2059: 16×16, through two, a yellow light.
            specs[ProjKind.BeamSwordBeam] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_116", W = 16, H = 16, Time = 3600, Penetrate = 2, AlphaStart = 1f, Light = new Color(0.5f, 0.5f, 0.125f),
                Update = (t, p) => StepBeamSwordBeam(p), Draw = (t, fx, p, px) => DrawBeamSwordBeam(fx, p, px), OnKill = (t, p) => KillBeamSwordBeam(p),
                TileHit = (t, p, hx, hy, before) => t.BounceBeamSwordBeam(p, hx, hy, before),
            };
            // P:3476-3492: the ice scythe, 34×34, through four, 180 ticks, one hit in 8 for all; P:3600-3615: the death scythe, 42×42 at 1.1, through five and through blocks.
            specs[ProjKind.IceSickleShot] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_263", W = 34, H = 34, Time = 180, Penetrate = 4, TypeCooldown = 8, Light = new Color(0.05f, 0.35f, 0.5f),
                Update = (t, p) => StepSickle(p, 0.95f), Draw = (t, fx, p, px) => DrawSickle(fx, p, px), OnKill = (t, p) => KillIceSickle(p),
            };
            specs[ProjKind.DeathSickleShot] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_274", W = 42, H = 42, Scale = 1.1f, Time = 180, Penetrate = 5, TypeCooldown = 10, TileCollide = false,
                Light = new Color(0.35f, 0.05f, 0.5f), Update = (t, p) => StepSickle(p, 0.96f), Draw = (t, fx, p, px) => DrawSickle(fx, p, px), OnKill = (t, p) => { },
            };
            // P:7296-7306: the sonic boom, 16×16, through five and through blocks; it hits along an 80 px line square to its flight (P:16107-16116).
            specs[ProjKind.SonicBoom] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_684", W = 16, H = 16, Time = 3600, Penetrate = 5, AlphaStart = 1f, TileCollide = false, TrailLength = 10,
                Update = (t, p) => StepSonicBoom(p), Draw = (t, fx, p, px) => DrawSonicBoom(fx, p, px), OnKill = (t, p) => KillSonicBoom(p),
                Beam = p =>
                {
                    var n = TV(p).sqrMagnitude > 1e-6f ? TV(p).normalized : Vector2.up;
                    var perp = new Vector2(n.y, -n.x) * p.Scale;
                    var c = CenterPx(p);
                    return (c - perp * 40f, c + perp * 40f, 16f * p.Scale);
                },
            };
            // P:10567-10581: a spore, 30×30 at 1.1, through everything, one hit in 30 ticks for all of them; only the ground's hurt (ai[1] 0, P:12463).
            specs[ProjKind.ClaymoreSpore] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_1128", W = 30, H = 30, Scale = 1.1f, Time = 3600, Penetrate = -1, TypeCooldown = 30, TileCollide = false, OwnMove = true, Frames = 5,
                CanDamage = p => p.Ai1 < 1f, Update = (t, p) => t.StepClaymoreSpore(p), Draw = (t, fx, p, px) => DrawClaymoreSpore(fx, p, px), OnKill = (t, p) => { },
                HitArea = p => (p.Pos, Vector2.one * p.HitSize.x * Px),
            };
            // P:10582-10598: the slam's strike, unseen, its box ai[1]×ai[2] (128×96), each enemy once, for 5 ticks.
            specs[ProjKind.ClaymoreSlam] = new Spec
            {
                Ai = Ai.Custom, W = 128, H = 96, Time = 5, Penetrate = -1, OncePerNpc = true, TileCollide = false, OwnMove = true, Look = Look.Invisible,
                Update = (t, p) => { if (++p.Ai0 > 20f) t.Kill(p); }, Draw = (t, fx, p, px) => { }, OnKill = (t, p) => { },
            };
        }

        // ------------------------------------------------------------------ 119: the Frostbrand's bolt

        /// <summary>AI_028 (P:26987-26995, 27028-27048): three frost motes (92) an update, the chime as it leaves.</summary>
        static void StepFrostbrandBolt(Proj p)
        {
            if (p.Updates == 1) Sound("Item_28");
            var tl = CenterPx(p) - new Vector2(7f, 7f);
            var v = TV(p);
            for (int k = 0; k < 3; k++)
            {
                int i = BoxDust(tl, 14, 14, 92, v.x, v.y, 50, 1.2f);
                if (i < 0) continue;
                D(i).NoGravity = true;
                D(i).Velocity *= 0.3f;
            }
        }

        /// <summary>P:76869-76891: Item_27 and twenty frost motes, two in three thrown wide and large.</summary>
        static void KillFrostbrandBolt(Proj p)
        {
            Sound("Item_27");
            var tl = CenterPx(p) - new Vector2(7f, 7f);
            for (int k = 0; k < 20; k++)
            {
                int i = BoxDust(tl, 14, 14, 92);
                if (i < 0) continue;
                ref var d = ref D(i);
                if (Random.Range(0, 3) != 0)
                {
                    d.Velocity *= 2f;
                    d.NoGravity = true;
                    d.Scale *= 1.75f;
                }
                else d.Scale *= 0.5f;
            }
        }

        // ------------------------------------------------------------------ 116: the Beam Sword's beam

        /// <summary>
        /// AI_027 for 116 (P:26862-26905): Item_8 as it leaves; from its 15th update a yellow mote
        /// (64) streams back off its tail and it pulses between solid and added light, a little
        /// smaller and larger; turned along its flight.
        /// </summary>
        static void StepBeamSwordBeam(Proj p)
        {
            if (p.Updates == 1) Sound("Item_8");
            var v = TV(p);
            if (p.LocalAi1 < 15f) p.LocalAi1++;
            else
            {
                var tl = CenterPx(p) - new Vector2(8f, 8f) - v * 5f + new Vector2(2f, 2f);
                int i = BoxDust(tl, 8, 8, 64, v.x, v.y, 100, 1.5f);
                if (i >= 0)
                {
                    D(i).Velocity *= -0.25f;
                    D(i).NoGravity = true;
                }
                if (p.LocalAi0 == 0f)
                {
                    p.Scale -= 0.02f;
                    p.Alpha += 30f / 255f;
                    if (p.Alpha >= 250f / 255f)
                    {
                        p.Alpha = 1f;
                        p.LocalAi0 = 1f;
                    }
                }
                else
                {
                    p.Scale += 0.02f;
                    p.Alpha -= 30f / 255f;
                    if (p.Alpha <= 0f)
                    {
                        p.Alpha = 0f;
                        p.LocalAi0 = 0f;
                    }
                }
            }
            p.TRot = ToRot(v) + 0.785f;
            p.Vel.y = Mathf.Max(p.Vel.y, -16f);
        }

        /// <summary>P:19919-19943: off a block it bounces, thirty yellow motes bursting each time; the fourth block ends it.</summary>
        void BounceBeamSwordBeam(Proj p, bool hitX, bool hitY, Vector2 before)
        {
            if (++p.Ai1 >= 4f)
            {
                Kill(p);
                return;
            }
            Sound("Item_10");
            var tl = CenterPx(p) - new Vector2(8f, 8f);
            for (int k = 0; k < 30; k++)
            {
                int i = BoxDust(tl, 16, 16, 64, 0f, 0f, 100, 1.8f);
                if (i < 0) continue;
                D(i).Color = Color.white;
                D(i).NoGravity = true;
                D(i).Velocity *= Random.Range(1, 7);
            }
            if (hitX) p.Vel.x = -before.x;
            if (hitY) p.Vel.y = -before.y;
        }

        /// <summary>GetAlpha (P:81265-81280): unseen for 5 updates, fading in over 10, then white with its alpha; pivoted at its top-right, tip on its centre.</summary>
        static void DrawBeamSwordBeam(FxBatch fx, Proj p, Vector2 px)
        {
            if (p.LocalAi1 < 5f) return;
            var whole = FxBatch.Whole(p.Spec.Texture);
            if (whole == null) return;
            float n = (p.LocalAi1 - 5f) / 10f;
            var c = p.LocalAi1 < 15f ? new Color(n, n, n, n) : new Color(1f, 1f, 1f, p.Alpha);
            float w = whole.rect.width, h = whole.rect.height;
            fx.Draw(FxBatch.Frame(p.Spec.Texture, 0, 0, (int)w, (int)h, w, 0f), px, c, p.TRot, p.Scale);
        }

        /// <summary>P:78812-78823: Item_10 and a streak of yellow motes back along its path.</summary>
        static void KillBeamSwordBeam(Proj p)
        {
            Sound("Item_10");
            var v = TV(p);
            var tl = CenterPx(p) - new Vector2(8f, 8f);
            for (int k = 4; k < 31; k++)
            {
                var at = tl - v * (30f / k);
                int i = BoxDust(at, 8, 8, 64, v.x, v.y, 100, 1.8f);
                if (i >= 0) D(i).NoGravity = true;
                i = BoxDust(at, 8, 8, 64, v.x, v.y, 100, 1.4f);
                if (i >= 0) D(i).NoGravity = true;
            }
        }

        // ------------------------------------------------------------------ 263, 274: the sickles' scythes

        /// <summary>AI_018 for 263 and 274 (P:25695-25707): spinning the faster the younger, slowing 5% (the death scythe 4%) a tick.</summary>
        static void StepSickle(Proj p, float drag)
        {
            p.TRot += p.Dir * 0.05f + p.Dir * 0.5f * (p.TimeLeft / 180f);
            p.Vel *= drag;
        }

        /// <summary>
        /// GetAlpha (P:81223-81240): the ice scythe white, as clear as it has ticks left; the death
        /// scythe mostly added light, darkening out over its last 85 ticks; turned, the death one
        /// mirrored when thrown leftwards.
        /// </summary>
        static void DrawSickle(FxBatch fx, Proj p, Vector2 px)
        {
            Color c;
            if (p.Kind == ProjKind.IceSickleShot) c = new Color(1f, 1f, 1f, Mathf.Min(255, p.TimeLeft) / 255f);
            else if (p.TimeLeft < 85)
            {
                float b = p.TimeLeft * 3f / 255f;
                c = new Color(b, b, b, 100f / 255f * b);
            }
            else c = new Color(1f, 1f, 1f, 100f / 255f);
            fx.Draw(FxBatch.Whole(p.Spec.Texture), px, c, p.TRot, p.Scale, flipX: p.Kind == ProjKind.DeathSickleShot && p.Dir < 0f);
        }

        /// <summary>P:75895-75904: Item_27 and fifteen frost motes, thrown out at twice its speed.</summary>
        static void KillIceSickle(Proj p)
        {
            Sound("Item_27");
            var v = TV(p);
            var tl = CenterPx(p) - new Vector2(17f, 17f);
            for (int k = 0; k < 15; k++)
            {
                int i = BoxDust(tl, 34, 34, 92, v.x, v.y, Random.Range(0, 101), 1f + Random.Range(0, 40) * 0.01f);
                if (i < 0) continue;
                D(i).NoGravity = true;
                D(i).Velocity *= 2f;
            }
        }

        // ------------------------------------------------------------------ 684: the sonic boom

        /// <summary>
        /// AI_001 for 684 (P:55936-55957): it shows in 40 a tick, a red-violet mote (60) somewhere on
        /// its long edge each tick, a strong red light; turned square to its flight; no fall.
        /// </summary>
        static void StepSonicBoom(Proj p)
        {
            p.Alpha = Mathf.Max(0f, p.Alpha - 40f / 255f);
            p.LocalAi0++;
            var v = TV(p);
            float a = ToRot(v);
            var off = RotateBy(new Vector2(Random.Range(-0.5f, 0.5f) * 20f, Random.Range(-0.5f, 0.5f) * 80f), a);
            int i = PointDust(CenterPx(p) + off, 60, Random.insideUnitCircle * 0.6f, 127, 1.3f);
            if (i >= 0)
            {
                ref var d = ref D(i);
                d.FadeIn = 1.5f;
                d.NoGravity = true;
                d.Color = new Color(1f, 1f, 1f, 0f);
            }
            p.TRot = a + Mathf.PI / 2f;
            FxBatch.Instance?.Light(CenterPx(p), new Color(1.1f, 0.3f, 0.4f), 3f);
        }

        /// <summary>
        /// Main.DrawProj (M:31025, 31303, 31491, 31581): seven echoes behind it, redder and smaller
        /// the older, at a fifteenth less each; eight red copies throbbing around it (2π every 30
        /// ticks); itself white at half alpha.
        /// </summary>
        static void DrawSonicBoom(FxBatch fx, Proj p, Vector2 px)
        {
            var spr = FxBatch.Whole(p.Spec.Texture);
            if (spr == null) return;
            float vis = 1f - p.Alpha;
            var baseC = new Color(1f, 1f, 1f, 200f / 255f) * vis;
            bool flip = p.Dir < 0f;
            for (int i = 1; i < 8 && i < p.Trail.Count; i++)
            {
                var c = baseC;
                if (i == 1)
                {
                    c.b /= 2f;
                    c.g /= 2f;
                    c.a /= 2f;
                }
                c.b /= i;
                c.g /= i;
                c *= (8 - i) / 15f;
                fx.Draw(spr, TerraParticles.ToPx(p.Trail[i]), c, p.TRot, Mathf.Lerp(p.Scale, 0.75f, i / 15f), flipX: flip);
            }
            float x = Mathf.Cos(p.LocalAi0 * Mathf.PI * 2f / 30f);
            var ring = FxBatch.C(220, 40, 30, 40) * (0.75f + 0.25f * x);
            var dir = new Vector2(Mathf.Cos(p.TRot), Mathf.Sin(p.TRot));
            for (int k = 0; k < 8; k++) fx.Draw(spr, px + RotateBy(dir, Mathf.PI / 4f * k) * (4f + x), ring, p.TRot, p.Scale, flipX: flip);
            var main = baseC;
            main.a = 127f / 255f;
            fx.Draw(spr, px, main, p.TRot, p.Scale, flipX: flip);
        }

        /// <summary>P:74838-74852: 15-24 motes (60) flung out from where it ends.</summary>
        static void KillSonicBoom(Proj p)
        {
            var c = CenterPx(p);
            for (int k = Random.Range(15, 25); k > 0; k--)
            {
                int i = PointDust(c, 60, Random.insideUnitCircle * 2f, 100, 1.3f);
                if (i < 0) continue;
                ref var d = ref D(i);
                d.Color = new Color(1f, 1f, 1f, 0f);
                d.Velocity *= 8f * (0.3f + 0.7f * Random.value);
                d.FadeIn = 1.3f + Random.value * 0.2f;
                d.NoGravity = true;
                d.Position += d.Velocity * 4f;
            }
        }

        // ------------------------------------------------------------------ 1128, 1129: the Claymore's spores and slam

        /// <summary>A spore from the Claymore: at a point (Terraria px), velocity y down; life 0 = a ground spore's 220 updates, and only those hurt.</summary>
        public void ClaymoreSpore(Vector2 atPx, Vector2 velYDown, int damage, float crit, float life)
        {
            var keep = SpawnClass;
            SpawnClass = DamageClass.Melee;
            var p = Spawn(ProjKind.ClaymoreSpore, TerraParticles.ToWorld(atPx), Flip(velYDown), damage, 0f, crit);
            SpawnClass = keep;
            if (p == null) return;
            p.Melee = true;
            p.Ai1 = life;
            p.Frame = Random.Range(0, 5);
            p.Dir = Random.Range(0, 2) == 0 ? -1f : 1f;
            p.TRot = Random.value * Mathf.PI * 2f;
            p.HitSize = new Vector2(30f * 0.25f / 1.1f, 0f);
        }

        /// <summary>The slam's strike: a box of a size (Terraria px) about a point, for five ticks.</summary>
        public void ClaymoreSlam(Vector2 atPx, int damage, float crit, Vector2 size)
        {
            var keep = SpawnClass;
            SpawnClass = DamageClass.Melee;
            var p = Spawn(ProjKind.ClaymoreSlam, TerraParticles.ToWorld(atPx), Vector2.zero, damage, 0f, crit);
            SpawnClass = keep;
            if (p == null) return;
            p.Melee = true;
            p.HitSize = size;
        }

        /// <summary>
        /// AI_092 for 1128 (P:33148-33340): it lives ai[1] updates, counting three a tick when that's
        /// set (the slash's harmless puffs) or one for 220 (the ground's); it swells over its first
        /// 10% and fades in over 25%, out over its second half (to 75%); it turns slowly, rises a
        /// touch, slows 3% a tick, pushes off other spores, and slides along blocks it drifts into.
        /// </summary>
        void StepClaymoreSpore(Proj p)
        {
            p.Ai0++;
            if (p.Ai1 >= 1f) p.Ai0 += 2f;
            float life = p.Ai1 == 0f ? 220f : p.Ai1;
            if (p.Ai0 > life)
            {
                Kill(p);
                return;
            }
            float t = p.Ai0 / life;
            p.Scale = Remap(t, 0f, 0.1f, 0.25f, 1.1f);
            p.HitSize = new Vector2((int)(30f * p.Scale / 1.1f), 0f);
            p.Alpha = 1f - Remap(t, 0f, 0.25f, 0f, 1f) * Remap(t, 0.5f, 1f, 1f, 0f) * 0.75f;
            var v = TV(p);
            v.y -= 0.01f;
            p.TRot += p.Dir * Mathf.PI * 2f / 480f;
            v *= 0.97f;
            float w = p.HitSize.x;
            foreach (var q in live)
            {
                if (q == p || q.Dead || q.Kind != ProjKind.ClaymoreSpore) continue;
                var d = TerraParticles.ToPx(q.Pos) - CenterPx(p);
                if (Mathf.Abs(d.x) > (w + q.HitSize.x) * 0.5f || Mathf.Abs(d.y) > (w + q.HitSize.x) * 0.5f) continue;
                if (d == Vector2.zero) d = new Vector2(1f, -1f);
                float len = d.magnitude;
                var push = d / len * (Mathf.Max(w - len, 0f) / Mathf.Max(1f, w)) * 0.005f;
                v -= push;
                q.Vel += Flip(push);
            }
            // Drifting into a block: along its face, or up or down round it (P:33326-33380).
            var c = CenterPx(p);
            var n = v.sqrMagnitude > 1e-8f ? v.normalized : Vector2.zero;
            if (n != Vector2.zero && SolidPx(c + n * 16f))
            {
                float speed = v.magnitude;
                var ahead = c + n * 16f;
                if (Mathf.Abs(n.x) > Mathf.Abs(n.y))
                {
                    var mirror = new Vector2(n.x, -n.y);
                    if (!SolidPx(c + mirror * 16f)) v = mirror * speed;
                    else
                    {
                        foreach (float dy in new[] { -16f, 16f, -32f, 32f })
                            if (!SolidPx(ahead + new Vector2(0f, dy)))
                            {
                                v = (ahead + new Vector2(0f, dy) - c).normalized * speed;
                                break;
                            }
                    }
                }
                else
                {
                    var mirror = new Vector2(-n.x, n.y);
                    if (!SolidPx(c + mirror * 16f)) v = mirror * speed;
                    else
                    {
                        foreach (float dx in new[] { -16f, 16f, -32f, 32f })
                            if (!SolidPx(ahead + new Vector2(dx, 0f)))
                            {
                                v = (ahead + new Vector2(dx, 0f) - c).normalized * speed;
                                break;
                            }
                    }
                }
            }
            p.Vel = Flip(v);
            p.Pos += p.Vel * Px;
        }

        /// <summary>Collision.IsWorldPointSolid (platforms not counted): whether a point (Terraria px) is in a block.</summary>
        static bool SolidPx(Vector2 px)
        {
            var cell = CellGrid.WorldToCell(TerraParticles.ToWorld(px));
            return TerraWorld.IsSolid(cell.x, cell.y);
        }

        /// <summary>Main.DrawProj's generic branch for 1128: its frame of five, lit, as faint as its fade, turning.</summary>
        static void DrawClaymoreSpore(FxBatch fx, Proj p, Vector2 px)
        {
            var whole = FxBatch.Whole("Projectile_1128");
            if (whole == null) return;
            int w = (int)whole.rect.width, fh = (int)whole.rect.height / 5;
            fx.Draw(FxBatch.Frame("Projectile_1128", 0, p.Frame * fh, w, fh, w / 2f, fh / 2f), px, new Color(L, L, L, 1f) * (1f - p.Alpha), p.TRot, p.Scale);
        }
    }
}
