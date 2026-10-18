using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The thrown consumables (sweep R3; Terraria's space, px y down, per update): the AI_002 throws
    /// (Poisoned Knife 54, the Holy, Unholy and Blood Waters 69/70/621, Rotten Egg 318, Star Anise
    /// 330, Bone Dagger 599, Frost Daggerfish 520, Magic Dagger 93), the Javelin (507, AI_001), the
    /// Bone Javelin (598, AI_113: sticking in, javelined), the AI_014 rollers (Spiky Ball 24,
    /// Beenade 183 bursting into bees), the Molotov Cocktail (399, AI_068, its flames 400-402) and
    /// the Mud Ball (1081, AI_010).
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        static void RegisterThrown3()
        {
            // P:3590-3600, 5600-5620 ... (SetDefaults): the AI_002 throws, each at its own size and pierce.
            Spec Throw(string tex, int w, int h, int pierce) => new Spec
            {
                Ai = Ai.Custom, Texture = tex, W = w, H = h, Time = 3600, Penetrate = pierce,
                Update = (t, p) => StepThrowAi2(p), Draw = (t, fx, p, px) => DrawGenericLit(fx, p, px), OnKill = (t, p) => KillThrowAi2(p),
            };
            specs[ProjKind.PoisonedKnifeProj] = Throw("Projectile_54", 12, 12, 2);
            specs[ProjKind.PoisonedKnifeProj].Debuffs = new[] { (EnemyDebuffs.Poisoned, 600, 2) };
            specs[ProjKind.HolyWaterProj] = Throw("Projectile_69", 14, 14, 1);
            specs[ProjKind.UnholyWaterProj] = Throw("Projectile_70", 14, 14, 1);
            specs[ProjKind.BloodWaterProj] = Throw("Projectile_621", 14, 14, 1);
            specs[ProjKind.RottenEggProj] = Throw("Projectile_318", 12, 14, 1);
            specs[ProjKind.StarAniseProj] = Throw("Projectile_330", 22, 22, 6);
            specs[ProjKind.BoneDaggerProj] = Throw("Projectile_599", 22, 22, 6);
            specs[ProjKind.FrostDaggerfishProj] = Throw("Projectile_520", 22, 22, 3);
            specs[ProjKind.MagicDaggerProj] = Throw("Projectile_93", 12, 12, 3);
            specs[ProjKind.MagicDaggerProj].LocalCooldown = 20;
            specs[ProjKind.MagicDaggerProj].Light = new Color(0.15f, 0.15f, 0.0015f);
            // P:5660-5668 (507): the Javelin, AI_001, three enemies.
            specs[ProjKind.JavelinProj] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_507", W = 16, H = 16, Time = 3600, Penetrate = 3,
                Update = (t, p) => StepJavelin(p), Draw = (t, fx, p, px) => DrawGenericLit(fx, p, px), OnKill = (t, p) => KillJavelin(p, 507),
            };
            // 598: the Bone Javelin, AI_113 as Daybreak's: in it sticks, javelined (3 a second each), six at most, 15 s.
            specs[ProjKind.BoneJavelinProj] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_598", W = 16, H = 16, Time = 3600, Penetrate = -1, AlphaStart = 1f, NoImmunity = true,
                CanDamage = p => p.Ai0 == 0f, HitTest = (p, col) => p.Ai0 == 0f && DaybreakTouches(p, col),
                Update = (t, p) => t.StepBoneJavelin(p), Draw = (t, fx, p, px) => DrawBoneJavelin(fx, p, px), OnHit = (t, p, hm) => t.StickBoneJavelin(p, hm),
                OnKill = (t, p) => KillJavelin(p, 598),
            };
            // P:1071-1080: the Spiky Ball, AI_014, seven enemies, 80 s.
            specs[ProjKind.SpikyBallProj] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_24", W = 14, H = 14, Time = 4800, Penetrate = 7,
                Update = (t, p) => StepRoller(p), Draw = (t, fx, p, px) => DrawGenericLit(fx, p, px),
                TileHit = (t, p, hx, hy, before) => RollerBounce(p, hx, hy, before, 0.5f), OnKill = (t, p) => KillRoller(p),
            };
            // 183: the Beenade, AI_014, one enemy, 3 s.
            specs[ProjKind.BeenadeProj] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_183", W = 14, H = 22, Time = 180, Penetrate = 1,
                Update = (t, p) => StepRoller(p), Draw = (t, fx, p, px) => DrawGenericLit(fx, p, px),
                TileHit = (t, p, hx, hy, before) => RollerBounce(p, hx, hy, before, 0.5f), OnKill = (t, p) => t.KillBeenade(p),
            };
            // 399: the Molotov, AI_068, one enemy; it shows after 3 updates.
            specs[ProjKind.MolotovProj] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_399", W = 14, H = 14, Time = 3600, Penetrate = 1, AlphaStart = 1f, Debuffs = new[] { (EnemyDebuffs.OnFire, 300, 1) },
                Update = (t, p) => StepMolotov(p), Draw = (t, fx, p, px) => DrawGenericLit(fx, p, px),
                TileHit = (t, p, hx, hy, before) => { p.Vel = Vector2.zero; p.Alpha = 1f; p.TimeLeft = 3; }, OnKill = (t, p) => t.KillMolotov(p),
            };
            // 400-402: its flames, AI_014, three enemies, 6 s.
            foreach (var (k, id, w, h) in new[] { (ProjKind.MolotovFire1, 400, 14, 16), (ProjKind.MolotovFire2, 401, 12, 14), (ProjKind.MolotovFire3, 402, 6, 12) })
                specs[k] = new Spec
                {
                    Ai = Ai.Custom, Texture = "Projectile_" + id, W = w, H = h, Time = 360, Penetrate = 3, Debuffs = new[] { (EnemyDebuffs.OnFire, 300, 1) },
                    Update = (t, p) => StepMolotovFire(p), Draw = (t, fx, p, px) => DrawGenericLit(fx, p, px),
                    TileHit = (t, p, hx, hy, before) => { if (hx) p.Vel.x = -before.x * 0.1f; }, OnKill = (t, p) => { },
                };
            // 1081: the Mud Ball, AI_010, through everything.
            specs[ProjKind.MudBallProj] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_1081", W = 12, H = 12, Time = 3600, Penetrate = -1,
                Update = (t, p) => StepMudBall(p), Draw = (t, fx, p, px) => DrawGenericLit(fx, p, px),
                TileHit = (t, p, hx, hy, before) => t.MudBallTile(p, hx, hy, before), OnKill = (t, p) => KillMudBall(p),
            };
        }

        // ------------------------------------------------------------------ AI_002 throws

        /// <summary>
        /// AI_002 (P:23419-23937): spinning with its speed (0.03 a px, its way); the Waters fall
        /// (0.25) after 10 updates, the rest (0.4, slowing 3%) after 20 (the Magic Dagger after
        /// 28-33); the knives, dagger and daggerfish point along their flight until then. The
        /// Poisoned Knife sheds a green mote (40) now and then, the Magic Dagger a sparkle (57).
        /// </summary>
        static void StepThrowAi2(Proj p)
        {
            var v = TV(p);
            float dir = v.x < 0f ? -1f : 1f;
            p.TRot += (Mathf.Abs(v.x) + Mathf.Abs(v.y)) * 0.03f * dir;
            bool water = p.Kind == ProjKind.HolyWaterProj || p.Kind == ProjKind.UnholyWaterProj || p.Kind == ProjKind.BloodWaterProj;
            if (water)
            {
                if (++p.Ai0 >= 10f)
                {
                    v.y += 0.25f;
                    v.x *= 0.99f;
                }
            }
            else
            {
                bool dagger = p.Kind == ProjKind.MagicDaggerProj;
                if (p.LocalAi1 == 0f) p.LocalAi1 = dagger ? 28 + Random.Range(0, 6) : 20;
                if (++p.Ai0 >= p.LocalAi1)
                {
                    if (dagger) p.Ai0 = 40f;
                    v.y += 0.4f;
                    v.x *= 0.97f;
                }
                else if (p.Kind == ProjKind.PoisonedKnifeProj || dagger || p.Kind == ProjKind.FrostDaggerfishProj || p.Kind == ProjKind.BoneDaggerProj)
                    p.TRot = ToRot(v) + 1.57f;
            }
            if (v.y > 32f) v.y = 32f;
            p.Vel = Flip(v);
            int w = p.Spec.W, h = p.Spec.H;
            var tl = CenterPx(p) - new Vector2(w / 2f, h / 2f);
            if (p.Kind == ProjKind.PoisonedKnifeProj && Random.Range(0, 20) == 0) BoxDust(tl, w, h, 40, v.x * 0.1f, v.y * 0.1f, 0, 0.75f);
            if (p.Kind == ProjKind.MagicDaggerProj && Random.Range(0, 5) == 0)
            {
                int i = BoxDust(tl, w, h, 57, v.x * 0.2f + dir * 3f, v.y * 0.2f, 100);
                if (i >= 0)
                {
                    D(i).NoGravity = true;
                    D(i).FadeIn = 1f;
                    D(i).Velocity *= 0.3f;
                }
            }
        }

        /// <summary>
        /// Projectile.Kill for the throws (P:75727, 76343, 77222-77236, 74703, 78729-78776): the dig
        /// sound and stone (1) or dirt (0) chips, the egg's yolk (30), the dagger's sparkles (57), the
        /// daggerfish's chime (Item50) and frost (252), the Waters' shatter (13) and splash (33 holy,
        /// 52 unholy, 266 blood) — here they convert no ground.
        /// </summary>
        static void KillThrowAi2(Proj p)
        {
            var v = TV(p);
            int w = p.Spec.W, h = p.Spec.H;
            var tl = CenterPx(p) - new Vector2(w / 2f, h / 2f);
            switch (p.Kind)
            {
                case ProjKind.PoisonedKnifeProj:
                case ProjKind.BoneDaggerProj:
                    Sound("Dig_" + Random.Range(0, 3));
                    for (int k = 0; k < 10; k++) BoxDust(tl, w, h, 1, v.x * 0.1f, v.y * 0.1f, 0, 0.75f);
                    break;
                case ProjKind.StarAniseProj:
                    Sound("Dig_" + Random.Range(0, 3));
                    for (int k = 0; k < 10; k++) BoxDust(tl, w, h, 0, v.x * 0.4f, v.y * 0.4f, 0, 0.75f);
                    break;
                case ProjKind.RottenEggProj:
                    Sound("Dig_" + Random.Range(0, 3));
                    for (int k = 0; k < 10; k++)
                    {
                        int i = BoxDust(tl, w, h, 30);
                        if (i >= 0 && Random.Range(0, 2) == 0) D(i).NoGravity = true;
                    }
                    break;
                case ProjKind.MagicDaggerProj:
                    Sound("Dig_" + Random.Range(0, 3));
                    for (int k = 0; k < 10; k++)
                    {
                        int i = BoxDust(tl, w, h, 57, 0f, 0f, 100);
                        if (i < 0) continue;
                        D(i).NoGravity = true;
                        D(i).FadeIn = 1f;
                        D(i).Velocity *= 2f;
                        D(i).Velocity.y -= Random.value * 1.5f;
                    }
                    break;
                case ProjKind.FrostDaggerfishProj:
                    Sound("Item_50");
                    for (int k = 0; k < 10; k++)
                    {
                        int i = BoxDust(tl, w, h, 252, v.x * 0.1f, v.y * 0.1f, 0, 0.75f);
                        if (i < 0) continue;
                        D(i).NoGravity = true;
                        D(i).Velocity -= v / 3f;
                    }
                    break;
                default:
                {
                    // The Waters.
                    int splash = p.Kind == ProjKind.HolyWaterProj ? 33 : p.Kind == ProjKind.UnholyWaterProj ? 52 : 266;
                    Sound("Shatter");
                    for (int k = 0; k < 5; k++) BoxDust(tl, w, h, 13);
                    for (int k = 0; k < 30; k++)
                    {
                        int i = BoxDust(tl, w, h, splash, 0f, -2f, 0, 1.1f);
                        if (i < 0) continue;
                        D(i).Alpha = 100;
                        D(i).Velocity.x *= 1.5f;
                        D(i).Velocity *= 3f;
                    }
                    break;
                }
            }
        }

        // ------------------------------------------------------------------ 507: the Javelin (AI_001)

        /// <summary>AI_001 for 507 (P:58454-58461): along its flight; after 60 updates slowing 2% and falling 0.3 an update.</summary>
        static void StepJavelin(Proj p)
        {
            var v = TV(p);
            if (++p.Ai0 > 60f)
            {
                v.x *= 0.98f;
                v.y += 0.3f;
            }
            if (v.y > 16f) v.y = 16f;
            p.Vel = Flip(v);
            p.TRot = ToRot(v) + 1.57f;
        }

        /// <summary>P:75393-75429: the dig sound and twenty bone-white chips (81) back along the shaft.</summary>
        static void KillJavelin(Proj p, int type)
        {
            Sound("Dig_" + Random.Range(0, 3));
            var n = type == 598 ? new Vector2(Mathf.Cos(p.TRot - Mathf.PI / 2f), Mathf.Sin(p.TRot - Mathf.PI / 2f)) : (TV(p).sqrMagnitude > 1e-6f ? TV(p).normalized : Vector2.down);
            var at = CenterPx(p) - new Vector2(8f, 8f) + n * 16f;
            var c = CenterPx(p);
            var v = TV(p);
            for (int k = 0; k < 20; k++)
            {
                int i = BoxDust(at, 16, 16, 81);
                if (i >= 0)
                {
                    ref var d = ref D(i);
                    d.Position = (d.Position + c) / 2f;
                    d.Velocity += type == 598 ? n * 2f : v * 0.4f;
                    d.Velocity *= 0.5f;
                    d.NoGravity = true;
                }
                at -= n * 8f;
            }
        }

        // ------------------------------------------------------------------ 598: the Bone Javelin (AI_113)

        /// <summary>
        /// AI_113 for 598 (P:41290-41630): shown 25 an update; in flight along it, after 45 updates
        /// slowing 2% and falling 0.35 an update; stuck, riding where it went in, its enemy's hit
        /// effect every 30 updates, out after 15 s or with its enemy.
        /// </summary>
        void StepBoneJavelin(Proj p)
        {
            p.Alpha = Mathf.Max(0f, p.Alpha - 25f / 255f);
            var v = TV(p);
            if (p.Ai0 == 0f)
            {
                if (++p.Ai1 >= 45f)
                {
                    v.x *= 0.98f;
                    v.y += 0.35f;
                    p.Vel = Flip(v);
                }
                p.TRot = ToRot(v) + Mathf.PI / 2f;
                return;
            }
            var hm = p.Target;
            if (++p.LocalAi0 >= 900f || hm == null || hm.isDead || !hm.gameObject.activeInHierarchy)
            {
                Kill(p);
                return;
            }
            p.Pos = TerraParticles.ToWorld(EnemyCenterPx(hm) - v * 2f);
            if (p.LocalAi0 % 30f == 0f)
            {
                var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
                var info = EnemyStats.Of(hm);
                if (col != null && info != null) TerraHitFx.Bleed(hm, col.bounds, 1, 0f, EnemyStats.LifeMax(info));
            }
        }

        /// <summary>P:13244-13251, 11341-11344, 14044-14056: in it goes, javelined 15 s; past six in one enemy the oldest comes out.</summary>
        void StickBoneJavelin(Proj p, HealthManager hm)
        {
            if (p.Ai0 != 0f) return;
            p.Ai0 = 1f;
            p.Target = hm;
            p.Collide = false;
            p.Vel = Flip((EnemyCenterPx(hm) - CenterPx(p)) * 0.75f);
            EnemyDebuffs.Add(hm, EnemyDebuffs.Javelined, 900);
            Proj oldest = null;
            int n = 0;
            foreach (var q in live)
            {
                if (q == p || q.Dead || q.Kind != ProjKind.BoneJavelinProj || q.Ai0 != 1f || q.Target != hm) continue;
                n++;
                if (oldest == null || q.TimeLeft < oldest.TimeLeft) oldest = q;
            }
            if (n >= 6 && oldest != null) Kill(oldest);
            DevBoneJavelinsStuck++;
        }

        /// <summary>The Bone Javelins stuck in an enemy (NPC.AddStackedProjecitle's count).</summary>
        public int StuckBoneJavelins(HealthManager hm)
        {
            int n = 0;
            foreach (var q in live) if (!q.Dead && q.Kind == ProjKind.BoneJavelinProj && q.Ai0 == 1f && q.Target == hm) n++;
            return n;
        }

        /// <summary>Main.DrawProj's generic branch: about its middle, lit, as it shows.</summary>
        static void DrawBoneJavelin(FxBatch fx, Proj p, Vector2 px) =>
            fx.Draw(FxBatch.Whole("Projectile_598"), px, new Color(L, L, L, 1f) * (1f - p.Alpha), p.TRot, p.Scale);

        // ------------------------------------------------------------------ AI_014 rollers

        /// <summary>AI_014's plain branch (P:25446-25463): after 5 updates falling 0.2, slowing 3% while rolling (stopping under 0.01), turning with its roll.</summary>
        static void StepRoller(Proj p)
        {
            var v = TV(p);
            if (++p.Ai0 > 5f)
            {
                p.Ai0 = 5f;
                if (Mathf.Abs(v.y) < 1e-4f && v.x != 0f)
                {
                    v.x *= 0.97f;
                    if (Mathf.Abs(v.x) < 0.01f) v.x = 0f;
                }
                v.y += 0.2f;
            }
            if (v.y > 16f) v.y = 16f;
            p.Vel = Flip(v);
            p.TRot += v.x * 0.1f;
        }

        /// <summary>P:19840-19860: off a block half its speed back across, and up off the floor only from a fall over 1 px.</summary>
        static void RollerBounce(Proj p, bool hitX, bool hitY, Vector2 before, float k)
        {
            if (hitX) p.Vel.x = -before.x * k;
            if (hitY)
            {
                // before.y is y up: a fall is negative.
                if (before.y < -1f) p.Vel.y = -before.y * k;
                else p.Vel.y = 0f;
            }
        }

        /// <summary>P:77913-77919: ten stone chips (1).</summary>
        static void KillRoller(Proj p)
        {
            var v = TV(p);
            var tl = CenterPx(p) - new Vector2(7f, 7f);
            for (int k = 0; k < 10; k++) BoxDust(tl, 14, 14, 1, v.x * 0.1f, v.y * 0.1f, 0, 0.75f);
        }

        /// <summary>
        /// P:78086-78124: the boom (Item14), twenty smoke puffs, four smoke gores and 15-24 bees (the
        /// Hive Pack's giant ones half the time) at the bees' damage from the grenade's, no knockback.
        /// </summary>
        void KillBeenade(Proj p)
        {
            Sound("Item_14");
            var c = CenterPx(p);
            var tl = c - new Vector2(7f, 11f);
            for (int k = 0; k < 20; k++) BoxDust(tl, 14, 22, 31, 0f, 0f, 100, 1.5f);
            var pp = TerraParticles.Instance;
            if (pp != null)
                foreach (var d in new[] { new Vector2(1f, 1f), new Vector2(-1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, -1f) })
                    pp.NewGore(tl, d, Random.Range(61, 64), 1f, 0.3f);
            var keep = SpawnClass;
            SpawnClass = DamageClass.Ranged;
            for (int k = Random.Range(15, 25); k > 0; k--)
            {
                var v = new Vector2(Random.Range(-35, 36) * 0.02f, Random.Range(-35, 36) * 0.02f);
                bool strong = Equipment.Current.StrongBees && Random.Range(0, 2) == 0;
                int dmg = p.Damage + (strong ? Random.Range(1, 4) : Random.Range(0, 2));
                Spawn(strong ? ProjKind.GiantBee : ProjKind.Bee, TerraParticles.ToWorld(tl), Flip(v), dmg, strong ? 0.5f : 0f, p.Crit);
                DevBeenadeBees++;
            }
            SpawnClass = keep;
        }

        // ------------------------------------------------------------------ 399-402: the Molotov Cocktail

        /// <summary>
        /// AI_068 (P:30655-30700): tumbling (0.25 a tick, its way); showing 40 a tick after 3
        /// updates; after 15 falling 0.2 (to 16) and slowing 1%; once seen, a flame (6) licks off its
        /// neck.
        /// </summary>
        static void StepMolotov(Proj p)
        {
            var v = TV(p);
            float dir = v.x < 0f ? -1f : 1f;
            p.TRot += 0.25f * dir;
            if (++p.Ai0 >= 3f) p.Alpha = Mathf.Max(0f, p.Alpha - 40f / 255f);
            if (p.Ai0 >= 15f)
            {
                v.y = Mathf.Min(16f, v.y + 0.2f);
                v.x *= 0.99f;
                p.Vel = Flip(v);
            }
            if (p.Alpha > 0f) return;
            var neck = RotateBy(new Vector2(4f * dir, -8f), p.TRot);
            int i = PointDust(CenterPx(p) + neck, 6, Vector2.zero, 100);
            if (i >= 0)
            {
                D(i).NoGravity = true;
                D(i).Velocity = v * 0.1f + new Vector2(0f, -1f);
            }
        }

        /// <summary>
        /// P:75348-75380: the shatter, red glass (12), smoke (31) and fire (6), and six flames
        /// (400-402) flung back up and its way at half its damage.
        /// </summary>
        void KillMolotov(Proj p)
        {
            Sound("Shatter");
            var c = CenterPx(p);
            var tl = c - new Vector2(10f, 10f);
            for (int k = 0; k < 5; k++)
            {
                int i = BoxDust(tl, 20, 20, 12);
                if (i >= 0) D(i).Color = Color.red;
            }
            for (int k = 0; k < 10; k++)
            {
                int i = BoxDust(tl, 20, 20, 31, 0f, 0f, 100, 1.5f);
                if (i >= 0) D(i).Velocity *= 1.4f;
            }
            for (int k = 0; k < 20; k++)
            {
                int i = BoxDust(tl, 20, 20, 6, 0f, 0f, 100, 2.5f);
                if (i >= 0)
                {
                    D(i).NoGravity = true;
                    D(i).Velocity *= 5f;
                }
                i = BoxDust(tl, 20, 20, 6, 0f, 0f, 100, 1.5f);
                if (i >= 0) D(i).Velocity *= 3f;
            }
            var v = TV(p);
            var keep = SpawnClass;
            SpawnClass = DamageClass.Ranged;
            for (int k = 0; k < 6; k++)
            {
                float vx = -v.x * Random.Range(20, 50) * 0.01f + Random.Range(-20, 21) * 0.4f;
                float vy = -Mathf.Abs(v.y) * Random.Range(30, 50) * 0.01f + Random.Range(-20, 5) * 0.4f;
                var kind = Random.Range(0, 3) switch { 0 => ProjKind.MolotovFire1, 1 => ProjKind.MolotovFire2, _ => ProjKind.MolotovFire3 };
                Spawn(kind, TerraParticles.ToWorld(c + new Vector2(vx, vy)), Flip(new Vector2(vx, vy)), (int)(p.Damage * 0.5f), 0f, p.Crit);
            }
            SpawnClass = keep;
            DevMolotovs++;
        }

        /// <summary>
        /// AI_014 for 400-402 (P:25463-25478, 25590-25625): falling and rolling as the rollers do,
        /// burning (6, mostly floating up), checked across while it hangs at the top of a hop, tilted
        /// by its speed; a warm light.
        /// </summary>
        static void StepMolotovFire(Proj p)
        {
            StepRoller(p);
            var v = TV(p);
            int w = p.Spec.W, h = p.Spec.H;
            var tl = CenterPx(p) - new Vector2(w / 2f, h / 2f);
            int i = BoxDust(tl, w, h, 6, 0f, 0f, 100);
            if (i >= 0)
            {
                ref var d = ref D(i);
                d.Position.x -= 2f;
                d.Position.y += 2f;
                d.Scale += Random.Range(0, 50) * 0.01f;
                d.NoGravity = true;
                d.Velocity.y -= 2f;
            }
            if (Random.Range(0, 2) == 0)
            {
                int j = BoxDust(tl, w, h, 6, 0f, 0f, 100);
                if (j >= 0)
                {
                    ref var d = ref D(j);
                    d.Position.x -= 2f;
                    d.Position.y += 2f;
                    d.Scale += 0.3f + Random.Range(0, 50) * 0.01f;
                    d.NoGravity = true;
                    d.Velocity *= 0.1f;
                }
            }
            if (v.y < 0.25f && v.y > 0.15f) v.x *= 0.8f;
            p.Vel = Flip(v);
            p.TRot = -v.x * 0.05f;
            FxBatch.Instance?.Light(CenterPx(p), new Color(1f, 0.5f, 0.1f) * 0.5f, 2f);
        }

        // ------------------------------------------------------------------ 1081: the Mud Ball (AI_010)

        /// <summary>AI_010 for 1081 (P:58828-58835, 58981-58988): mud (38) dripping off it; after 20 updates falling 0.3 and slowing 2%.</summary>
        static void StepMudBall(Proj p)
        {
            var v = TV(p);
            if (Random.Range(0, 2) == 0)
            {
                int i = BoxDust(CenterPx(p) - new Vector2(6f, 6f), 12, 12, 38, 0f, v.y / 2f, 0, 0.8f);
                if (i >= 0)
                {
                    D(i).Velocity *= 0.3f;
                    D(i).Velocity += v / 2f;
                }
            }
            if (++p.Ai0 >= 20f)
            {
                v.y += 0.3f;
                v.x *= 0.98f;
            }
            if (v.y > 16f) v.y = 16f;
            p.Vel = Flip(v);
            p.TRot += v.x * 0.1f;
        }

        /// <summary>P:19945-19964: landing on something it splats; off a wall it only stops climbing (the dig sound once).</summary>
        void MudBallTile(Proj p, bool hitX, bool hitY, Vector2 before)
        {
            if (hitY && before.y < 0f)
            {
                Kill(p);
                return;
            }
            if (p.Vel.y > 0f) p.Vel.y = 0f;
            if (hitX) p.Vel.x = 0f;
            if (p.LocalAi0 == 0f)
            {
                p.LocalAi0 = 1f;
                Sound("Dig_" + Random.Range(0, 3));
            }
        }

        /// <summary>P:79174-79250: it would set down a mud block where it lands; there's no mud here, so it splats into mud (38).</summary>
        static void KillMudBall(Proj p)
        {
            var tl = CenterPx(p) - new Vector2(6f, 6f);
            for (int k = 0; k < 6; k++) BoxDust(tl, 12, 12, 38);
        }

        public static int DevBoneJavelinsStuck, DevBeenadeBees, DevMolotovs;
    }
}
