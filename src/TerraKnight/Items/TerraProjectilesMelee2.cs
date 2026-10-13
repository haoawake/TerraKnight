using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The plain swords' projectiles (sweep S1.A; Terraria's space, px y down): the Bone Sword's
    /// bone (1111), the Bladetongue's ichor stream (524), and the two that stick in what they hit
    /// (AI_113_TargetSticker): the Tentacle Spike's spike (971) and the Blood Butcherer's cut (975).
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        static void RegisterMelee2()
        {
            RegisterMelee2b();
            // P:10355-10363: aiStyle 2, 16×16 at scale 1.2, gone on its first hit; it falls once 20 updates old.
            specs[ProjKind.BoneSwordBone] = new Spec
            {
                Ai = Ai.Thrown2, Texture = "Projectile_1111", W = 16, H = 16, Scale = 1.2f, Time = 3600, Penetrate = 1, OnKill = (t, p) => KillBoneSwordBone(p),
            };
            // P:5941-5954: aiStyle 96, unseen but for its dust, through five, five more updates a tick, one hit in 10 ticks per enemy for all of them.
            specs[ProjKind.IchorSplash] = new Spec
            {
                Ai = Ai.Custom, W = 10, H = 10, Time = 3600, Penetrate = 5, Extra = 5, TypeCooldown = 10, Look = Look.Invisible,
                Update = (t, p) => t.StepIchorSplash(p), Draw = (t, fx, p, px) => { }, OnKill = (t, p) => { }, TileHit = (t, p, hx, hy, before) => BounceIchor(p, hx, hy, before),
                OnHit = (t, p, hm) =>
                {
                    // StatusNPC (P:11393): Ichor 3-7 s; a hit spends 50 of its 500 (P:13261).
                    EnemyDebuffs.Add(hm, EnemyDebuffs.Ichor, 60 * Random.Range(3, 8));
                    p.Ai0 += 50f;
                },
            };
            // P:9199-9209, 9289-9299: aiStyle 113, 16×16, through everything; stuck, they hurt only by the bleeding they cause.
            specs[ProjKind.TentacleSpikeStuck] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_971", W = 16, H = 16, Time = 3600, Penetrate = -1, AlphaStart = 1f, TileCollide = false, OwnMove = true,
                CanDamage = p => false, Update = (t, p) => t.StepStuck(p), Draw = (t, fx, p, px) => DrawSpike(fx, p, px), OnKill = (t, p) => KillSpike(p),
            };
            specs[ProjKind.BloodButchererStuck] = new Spec
            {
                Ai = Ai.Custom, W = 16, H = 16, Time = 3600, Penetrate = -1, TileCollide = false, OwnMove = true, Look = Look.Invisible,
                CanDamage = p => false, Update = (t, p) => t.StepStuck(p), Draw = (t, fx, p, px) => { }, OnKill = (t, p) => { },
            };
        }

        static void RegisterMelee2b()
        {
            // P:10097-10116: AI_201, the Mandible Blade's jaw (26×26, one enemy) and the Stylish Scissors (28×28, two).
            foreach (var (k, id, size, pen) in new[] { (ProjKind.MandibleBlade, 1082, 26, 1), (ProjKind.StylishScissors, 1083, 28, 2) })
                specs[k] = new Spec
                {
                    Ai = Ai.Custom, Texture = "Projectile_" + id, W = size, H = size, Time = 3600, Penetrate = pen, CanDamage = p => p.Ai1 == 0f,
                    Update = (t, p) => t.StepThrownMelee(p), Draw = (t, fx, p, px) => DrawThrownMelee(fx, p, px), OnKill = (t, p) => { },
                    TileHit = (t, p, hx, hy, before) => { p.Ai1 = 1f; p.Vel = before; },
                    // StatusNPC (P:11239-11242): the scissors cut, Bleeding 4-6 s.
                    OnHit = (t, p, hm) => { if (p.Kind == ProjKind.StylishScissors) EnemyDebuffs.Add(hm, EnemyDebuffs.Bleeding, 60 * Random.Range(4, 7)); },
                };
            // P:9626-9634: the waffle, aiStyle 2, 18×18, one enemy.
            specs[ProjKind.Waffle] = new Spec { Ai = Ai.Thrown2, Texture = "Projectile_1012", W = 18, H = 18, Time = 3600, Penetrate = 1, OnKill = (t, p) => KillWaffle(p) };
            // P:8619-8627, 4280-4288: the Christmas Tree Sword's ornament (aiStyle 30, two enemies) and the baubles it scatters (one).
            specs[ProjKind.ChristmasOrnament] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_907", W = 22, H = 22, Time = 3600, Penetrate = 2,
                Update = (t, p) => t.StepOrnament(p), Draw = (t, fx, p, px) => fx.Draw(FxBatch.Whole("Projectile_907"), px, new Color(1f, 1f, 1f, 150f / 255f) * (1f - p.Alpha), p.TRot, p.Scale),
                OnKill = (t, p) => KillOrnament(p),
            };
            specs[ProjKind.Ornament] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_335", W = 22, H = 22, Time = 3600, Penetrate = 1, Frames = 4,
                Update = (t, p) => StepBauble(p), Draw = (t, fx, p, px) => DrawBauble(fx, p, px), OnKill = (t, p) => KillBauble(p),
            };
        }

        /// <summary>The Mandible Blade's and Stylish Scissors' throw: it starts 0-5 updates nearer to dropping (Pl:49919-49922).</summary>
        public void ShootThrownMelee(ProjKind kind, Vector2 from, Vector2 velocity, int damage, float knockback, float crit)
        {
            var keep = SpawnClass;
            SpawnClass = DamageClass.Melee;
            var p = Spawn(kind, from, velocity, damage, knockback, crit);
            SpawnClass = keep;
            if (p == null) return;
            p.Melee = true;
            p.Ai0 = Random.Range(-5, 1);
        }

        /// <summary>
        /// AI_201_ThrownMelee (P:40675-40705): a random turn to start, spinning 0.45 a tick; past 15
        /// updates it slows and falls (0.95, 0.4); once it has met a block it goes on harmless,
        /// slowing to nothing and fading 7 a tick.
        /// </summary>
        void StepThrownMelee(Proj p)
        {
            if (p.LocalAi0 == 0f)
            {
                p.TRot = Random.value * Mathf.PI * 2f;
                p.LocalAi0 = 1f;
            }
            if (p.Ai1 == 0f)
            {
                p.TRot += p.Dir * 0.45f;
                if (p.Ai0 > 15f)
                {
                    p.Vel.x *= 0.95f;
                    p.Vel.y -= 0.4f;
                }
                else p.Ai0++;
                return;
            }
            p.Collide = false;
            p.Vel *= 0.6f;
            p.Alpha += 7f / 255f;
            if (p.Alpha >= 1f) Kill(p);
        }

        /// <summary>Main.DrawProj's generic branch: lit, fading, turned (AI_201 leaves spriteDirection at 1: never mirrored).</summary>
        static void DrawThrownMelee(FxBatch fx, Proj p, Vector2 px) =>
            fx.Draw(FxBatch.Whole(p.Spec.Texture), px, new Color(L, L, L, 1f) * (1f - p.Alpha), p.TRot, p.Scale);

        /// <summary>P:72727-72756: the dig sound and forty crumbs: a third of them batter-brown motes (284) fading in, the rest small orange ones (138).</summary>
        static void KillWaffle(Proj p)
        {
            Sound("Dig_" + Random.Range(0, 3));
            var tl = CenterPx(p) - new Vector2(9f, 9f);
            var v = TV(p);
            Color[] batter = { FxBatch.C(226, 116, 56), FxBatch.C(243, 175, 93), FxBatch.C(254, 210, 146) };
            for (int k = 0; k < 40; k++)
            {
                // Vector2.UnitY turned by whole turns: straight down, 2-2.5 px, plus some of its flight.
                var dv = new Vector2(0f, 2f + Random.value * 0.5f) + v * Random.value;
                var at = tl + Random.insideUnitCircle * 18f * p.Scale - v;
                if (Random.Range(0, 3) == 0)
                {
                    int i = BoxDust(at, 2, 2, 284, dv.x, dv.y, 0, Random.value + 1f);
                    if (i < 0) continue;
                    D(i).Color = batter[Random.Range(0, 3)];
                    D(i).FadeIn = 1f;
                }
                else BoxDust(at, 2, 2, 138, dv.x * 0.3f, dv.y * 0.3f, 0, Random.value * 0.3f + 0.6f);
            }
        }

        /// <summary>
        /// AI_030 for 907 (P:27053-27095): gold motes (43) as it spins out 0.2 a tick; every 10 of
        /// its first 50 updates a bauble (335) flies off square to its path, a side at a time,
        /// slower as it goes (5 or 9 px at first, never under 2.5); then it slows and stills, gone
        /// at 100.
        /// </summary>
        void StepOrnament(Proj p)
        {
            var v = TV(p);
            if (p.LocalAi1 == 0f) p.LocalAi1 = Random.Range(0, 2) == 0 ? 1f : -1f;
            if (Random.Range(0, Mathf.Max(4, 8 - (int)v.magnitude)) == 0)
            {
                int i = BoxDust(CenterPx(p) - new Vector2(6f, 6f), 12, 12, 43, 0f, 0f, 254);
                if (i >= 0)
                {
                    D(i).Color = FxBatch.C(255, 255, 0);
                    D(i).Velocity = v * 0.75f;
                }
            }
            const float spread = 50f;
            if (p.Ai0 > spread)
            {
                v *= 0.9f;
                p.TRot *= 0.9f;
            }
            else
            {
                p.TRot += 0.2f;
                if (p.TRot > Mathf.PI * 2f) p.TRot -= Mathf.PI * 2f;
            }
            p.Ai0++;
            if (p.Ai0 < spread && p.Ai0 % 10f == 0f && v.sqrMagnitude > 1e-6f)
            {
                float a = Mathf.PI / 2f * (p.Ai0 % 20f != 0f ? 1f : -1f) * p.LocalAi1;
                a += Random.Range(-5, 5) * Mathf.Lerp(0.2f, 0.03f, p.Ai0 / spread);
                var bv = RotateBy(v, a).normalized * Mathf.Max(2.5f, (spread - p.Ai0) / spread * (7f + (-2f + Random.Range(0, 2) * 2f)));
                var keep = SpawnClass;
                SpawnClass = DamageClass.Melee;
                var b = Spawn(ProjKind.Ornament, p.Pos, Flip(bv), p.Damage, p.Knockback * 0.25f, p.Crit);
                SpawnClass = keep;
                if (b != null)
                {
                    b.Melee = true;
                    b.Ai1 = Random.Range(0, 4);
                    b.Frame = (int)b.Ai1;
                }
            }
            p.Vel = Flip(v);
            if (p.Ai0 > 100f) Kill(p);
        }

        /// <summary>P:76716-76750: Item_27, ten red-gold motes (58), three star gores.</summary>
        static void KillOrnament(Proj p)
        {
            Sound("Item_27");
            var tl = CenterPx(p) - new Vector2(11f, 11f);
            var v = TV(p);
            for (int k = 0; k < 10; k++)
            {
                int i = BoxDust(tl, 22, 22, 58, v.x * 0.1f, v.y * 0.1f, 150, 1.2f);
                if (i >= 0) D(i).Velocity *= 0.75f;
            }
            for (int k = 0; k < 3; k++) TerraParticles.Instance?.NewGore(tl, v * 0.05f * 0.5f, Random.Range(16, 18));
        }

        static readonly Color[] baubleDust = { FxBatch.C(255, 100, 100), FxBatch.C(100, 255, 100), FxBatch.C(100, 100, 255), FxBatch.C(255, 255, 100) };

        /// <summary>
        /// AI_030 for 335 (P:27097-27145): it flies out slowing (0.95) with motes of its colour, stops,
        /// hangs swaying for 50 ticks, then drops (0.25, at most 14) until it hits something.
        /// </summary>
        static void StepBauble(Proj p)
        {
            var v = TV(p);
            int colour = Mathf.Clamp((int)p.Ai1, 0, 3);
            p.Frame = colour;
            if (p.Ai0 < 0f) v.y = Mathf.Min(14f, v.y + 0.25f);
            else
            {
                if (Random.Range(0, Mathf.Max(4, 8 - (int)v.magnitude)) == 0)
                {
                    int i = BoxDust(CenterPx(p) - new Vector2(6f, 6f), 12, 12, 43, 0f, 0f, 254);
                    if (i >= 0)
                    {
                        D(i).Color = baubleDust[colour];
                        D(i).Velocity = v * 0.75f;
                    }
                }
                v *= 0.95f;
            }
            if (p.Ai0 >= 0f && v.magnitude < 0.25f)
            {
                if (v != Vector2.zero)
                {
                    v = Vector2.zero;
                    p.Ai0 = 50f;
                }
                p.Ai0--;
            }
            p.LocalAi0++;
            p.TRot = Mathf.Sin(p.LocalAi0 / 10f);
            p.Vel = Flip(v);
        }

        /// <summary>Main.DrawProj (M:30223, 35126): its colour's frame hanging from 6 px below its centre, lit, and the same frame again added dimly.</summary>
        static void DrawBauble(FxBatch fx, Proj p, Vector2 px)
        {
            var whole = FxBatch.Whole("Projectile_335");
            if (whole == null) return;
            int w = (int)whole.rect.width, fh = (int)whole.rect.height / 4;
            var spr = FxBatch.Frame("Projectile_335", 0, p.Frame * fh, w, fh - 1, w / 2f, 11f + 6f);
            var at = px + new Vector2(0f, 6f);
            fx.Draw(spr, at, new Color(L, L, L, 1f), p.TRot, p.Scale);
            fx.Draw(spr, at, FxBatch.C(100, 100, 100, 0), p.TRot, p.Scale);
        }

        /// <summary>P:75716-75726: Item_27 and twenty motes of its colour (90 down to 87), unlit.</summary>
        static void KillBauble(Proj p)
        {
            Sound("Item_27");
            var tl = CenterPx(p) - new Vector2(11f, 11f);
            for (int k = 0; k < 20; k++)
            {
                int i = BoxDust(tl, 22, 22, 90 - (int)p.Ai1);
                if (i >= 0) D(i).Scale = 0.8f;
            }
        }

        /// <summary>A melee item's own shot: at a point (Terraria px), velocity y down, melee class.</summary>
        public void MeleeShot(ProjKind kind, Vector2 atPx, Vector2 velYDown, int damage, float knockback, float crit)
        {
            var keep = SpawnClass;
            SpawnClass = DamageClass.Melee;
            var p = Spawn(kind, TerraParticles.ToWorld(atPx), Flip(velYDown), damage, knockback, crit);
            SpawnClass = keep;
            if (p != null) p.Melee = true;
        }

        // ------------------------------------------------------------------ 1111: the bone

        /// <summary>P:77889: the dig sound and ten bone motes (26) over its box.</summary>
        static void KillBoneSwordBone(Proj p)
        {
            Sound("Dig_" + Random.Range(0, 3));
            var tl = CenterPx(p) - new Vector2(8f, 8f);
            for (int k = 0; k < 10; k++) BoxDust(tl, 16, 16, 26, 0f, 0f, 0, 0.8f);
        }

        // ------------------------------------------------------------------ 524: the ichor stream

        /// <summary>
        /// AI_096 (P:33613-33639): 0.6 of its 500 an update, two tries at an ichor mote (170) each,
        /// two in three, pulled halfway to its centre and slowed to a tenth, smaller as it ages; a
        /// slight fall (0.008).
        /// </summary>
        void StepIchorSplash(Proj p)
        {
            p.Ai0 += 0.6f;
            if (p.Ai0 > 500f)
            {
                Kill(p);
                return;
            }
            var c = CenterPx(p);
            var tl = c - new Vector2(5f, 5f);
            for (int k = 0; k < 2; k++)
            {
                if (Random.Range(0, 3) == 0) continue;
                int i = BoxDust(tl, 10, 10, 170, 0f, 0f, 100);
                if (i < 0) continue;
                ref var d = ref D(i);
                d.Position = (d.Position + c) / 2f;
                d.NoGravity = true;
                d.Velocity *= 0.1f;
                if (k == 1) d.Position += TV(p) / 2f;
                d.Scale *= (800f - p.Ai0) / 800f + 0.1f;
            }
            p.Vel.y -= 0.008f;
        }

        /// <summary>P:18920-18931: off a block it bounces back the way it met it, a hundred of its 500 spent.</summary>
        static void BounceIchor(Proj p, bool hitX, bool hitY, Vector2 before)
        {
            p.Ai0 += 100f;
            if (hitX) p.Vel.x = -before.x;
            if (hitY) p.Vel.y = -before.y;
        }

        // ------------------------------------------------------------------ 971, 975: stuck in an enemy (AI_113)

        /// <summary>
        /// Player.TentacleSpike_TrySpiking / BloodButcherer_TryButchering (Pl:44507-44536): from the
        /// point of the enemy's box nearest the player, aimed at its centre (the cut turned up to a
        /// quarter circle either way), already stuck; at most five of a kind in one enemy.
        /// </summary>
        public void StickInto(ProjKind kind, HealthManager hm, Vector2 playerPx, int damage, float knockback)
        {
            var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
            if (col == null) return;
            var centre = EnemyCenterPx(hm);
            var b = col.bounds;
            var tl = TerraParticles.ToPx(new Vector2(b.min.x, b.max.y));
            var br = TerraParticles.ToPx(new Vector2(b.max.x, b.min.y));
            var closest = new Vector2(Mathf.Clamp(playerPx.x, tl.x, br.x), Mathf.Clamp(playerPx.y, tl.y, br.y));
            var dir = centre - playerPx;
            var at = closest + (dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.zero);
            var v = (centre - at) * 0.8f;
            if (kind == ProjKind.BloodButchererStuck) v = RotateBy(v, Random.Range(-1f, 1f) * Mathf.PI * 0.25f);
            var keep = SpawnClass;
            SpawnClass = DamageClass.Melee;
            var p = Spawn(kind, TerraParticles.ToWorld(at), Flip(v), damage, knockback, 0f);
            SpawnClass = keep;
            if (p == null) return;
            p.Melee = true;
            p.Target = hm;
            p.Collide = false;
            // AI_113 sets its centre twice its velocity short of the enemy's, then UpdatePosition moves it on once.
            p.Pos = TerraParticles.ToWorld(centre - v);
            // Projectile.KillOldestJavelin: past five, the one with the least time left goes.
            Proj oldest = null;
            int n = 0;
            foreach (var q in live)
            {
                if (q.Dead || q.Kind != kind || q.Target != hm) continue;
                n++;
                if (oldest == null || q.TimeLeft < oldest.TimeLeft) oldest = q;
            }
            if (n > 5 && oldest != null) Kill(oldest);
        }

        /// <summary>The ones of a kind stuck in an enemy (NPC.AddStackedProjecitle's count).</summary>
        public int Stuck(HealthManager hm, ProjKind kind)
        {
            int n = 0;
            foreach (var q in live) if (!q.Dead && q.Kind == kind && q.Target == hm) n++;
            return n;
        }

        /// <summary>
        /// AI_113 stuck (P:41406-41630): fading in 25 a tick, riding on its enemy where it went in,
        /// its enemy's hit effect every 30 ticks; out after 9 s or with its enemy. The spike drips
        /// purple-black motes (4) every other tick; the cut bursts into blood (5) as it goes in and
        /// sprays it back out every other tick.
        /// </summary>
        void StepStuck(Proj p)
        {
            p.Alpha = Mathf.Max(0f, p.Alpha - 25f / 255f);
            var hm = p.Target;
            if (hm == null || hm.isDead || !hm.gameObject.activeInHierarchy || ++p.LocalAi0 >= 540f)
            {
                Kill(p);
                return;
            }
            var v = TV(p);
            var centre = EnemyCenterPx(hm);
            p.Pos = TerraParticles.ToWorld(centre - v);
            var c = CenterPx(p);
            if (p.LocalAi0 % 30f == 0f)
            {
                var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
                var info = EnemyStats.Of(hm);
                if (col != null && info != null) TerraHitFx.Bleed(hm, col.bounds, 1, 0f, EnemyStats.LifeMax(info));
            }
            var n = v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.zero;
            if (p.LocalAi1 == 0f)
            {
                p.LocalAi1 = 1f;
                p.TRot = ToRot(v) + Mathf.PI / 2f;
                if (p.Kind == ProjKind.BloodButchererStuck)
                    for (int k = 0; k < 10; k++)
                    {
                        float a = Random.value * Mathf.PI * 2f;
                        int i = PointDust(c, 5, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 4f, 0, 1.5f);
                        if (i >= 0) D(i).NoGravity = true;
                    }
            }
            if (p.TimeLeft % 2 != 0) return;
            if (p.Kind == ProjKind.TentacleSpikeStuck)
            {
                int i = PointDust(c - n * 8f, 4, new Vector2(Random.Range(-1f, 1f) * 0.35f, 2.5f), 0, 0.65f);
                if (i < 0) return;
                D(i).Color = FxBatch.C(120, 50, 120);
                D(i).FadeIn = 0.25f;
                D(i).NoGravity = true;
                return;
            }
            var spray = n * -7f + RotateBy(n, Random.Range(-1f, 1f) * Mathf.PI / 16f);
            var baseV = spray * 0.85f + new Vector2(0f, 0.85f);
            int j = PointDust(c, 5, RotateBy(baseV, Mathf.PI * 2f * Random.Range(-1f, 1f) * 0.025f), 0, 1.1f);
            if (j < 0) return;
            int j2 = TerraParticles.Instance.Clone(j);
            if (j2 < 0) return;
            D(j2).Velocity = RotateBy(baseV, Mathf.PI * 2f * Random.Range(-1f, 1f) * 0.025f);
            D(j2).Position += D(j2).Velocity;
            if (Random.Range(0, 3) != 0) return;
            int j3 = TerraParticles.Instance.Clone(j2);
            if (j3 >= 0) D(j3).Velocity = RotateBy(D(j3).Velocity, Random.Range(-1f, 1f) * Mathf.PI / 8f) * 0.9f;
        }

        /// <summary>Main.DrawProj's generic branch: the whole texture about its centre, lit, fading in, turned along its thrust.</summary>
        static void DrawSpike(FxBatch fx, Proj p, Vector2 px)
        {
            float a = 1f - p.Alpha;
            fx.Draw(FxBatch.Whole("Projectile_971"), px, new Color(L, L, L, 1f) * a, p.TRot, p.Scale);
        }

        /// <summary>P:75430-75445: six dark motes (4) along the spike from its point back.</summary>
        static void KillSpike(Proj p)
        {
            var dir = new Vector2(Mathf.Cos(p.TRot - Mathf.PI / 2f), Mathf.Sin(p.TRot - Mathf.PI / 2f));
            var at = CenterPx(p) - new Vector2(8f, 8f) + dir * 8f;
            var c = CenterPx(p);
            for (int k = 0; k < 6; k++)
            {
                int i = BoxDust(at, 16, 16, 4);
                if (i >= 0)
                {
                    ref var d = ref D(i);
                    d.Color = FxBatch.C(10, 10, 10, 150);
                    d.Position = (d.Position + c) / 2f;
                    d.Velocity += dir;
                    d.Velocity *= 0.5f;
                    d.NoGravity = true;
                }
                at -= dir * 4f;
            }
        }
    }
}
