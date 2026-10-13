using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The thrown spears (sweep S1.E; Terraria's space, px y down, per update): the Scourge of the
    /// Corruptor's javelin (306, AI_002), bursting into two to four tiny eaters (307, AI_036) that
    /// hunt what is near; and Daybreak (636, AI_113), sticking in what it hits, burning it while
    /// it stays (Daybroken, 100 a second for each one in it) and going off in a solar blast
    /// (953, AI_117) when it comes out.
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        static void RegisterJavelins()
        {
            // P:3976-3987: 14×14, fading in, one more update a tick, one enemy.
            specs[ProjKind.ScourgeJavelin] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_306", W = 14, H = 14, Extra = 1, Time = 3600, Penetrate = 1, AlphaStart = 1f,
                Update = (t, p) => StepScourge(p), Draw = (t, fx, p, px) => DrawScourge(fx, p, px), OnKill = (t, p) => t.KillScourge(p),
            };
            // P:3988-3998: 16×16, fading in, three more updates a tick at first, one enemy, 10 s; harmless until it has someone to chase.
            specs[ProjKind.TinyEater] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_307", W = 16, H = 16, Extra = 3, Time = 600, Penetrate = 1, AlphaStart = 1f, Frames = 2,
                CanDamage = p => p.LocalAi1 > 0f, Update = (t, p) => t.StepTinyEater(p), Draw = (t, fx, p, px) => DrawTinyEater(fx, p, px),
                TileHit = (t, p, hx, hy, before) => t.BounceEater(p, hx, hy, before), OnKill = (t, p) => { },
            };
            // P:6840-6852, 20073: 16×16 (10×10 on blocks), fading in, MaxUpdates 2, through everything; javelins leave no immunity (P:14044).
            specs[ProjKind.Daybreak] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_636", W = 16, H = 16, Extra = 1, Time = 3600, Penetrate = -1, AlphaStart = 1f, NoImmunity = true,
                TileBox = new Vector2(10f, 10f), TrailLength = 20, CanDamage = p => p.Ai0 == 0f, HitTest = (p, col) => p.Ai0 == 0f && DaybreakTouches(p, col),
                Update = (t, p) => t.StepDaybreak(p), Draw = (t, fx, p, px) => DrawDaybreak(fx, p, px), OnHit = (t, p, hm) => t.StickDaybreak(p, hm),
                OnKill = (t, p) => t.KillDaybreak(p),
            };
            // P:8990-9002: an 8×8 point that grows to 52 × its size as it goes off; each enemy once (local 80 ticks), knocked as the player faces.
            specs[ProjKind.DaybreakBlast] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_953", W = 8, H = 8, Time = 60, Penetrate = -1, TileCollide = false, OwnMove = true, AlphaStart = 1f, Frames = 5,
                LocalCooldown = 80, PushFromPlayer = true, Debuffs = new[] { (EnemyDebuffs.Daybroken, 300, 1) },
                CanDamage = p => p.Ai0 >= 1f, HitArea = p => (p.Pos, p.HitSize * Px),
                Update = (t, p) => StepSolarBlast(p), Draw = (t, fx, p, px) => DrawSolarBlast(fx, p, px), OnKill = (t, p) => { },
            };
        }

        /// <summary>A melee item's child: at a point (Terraria px), velocity y down, melee class.</summary>
        Proj MeleeChild(ProjKind kind, Vector2 atPx, Vector2 velYDown, int damage, float knockback, float crit)
        {
            var keep = SpawnClass;
            SpawnClass = DamageClass.Melee;
            var p = Spawn(kind, TerraParticles.ToWorld(atPx), Flip(velYDown), damage, knockback, crit);
            SpawnClass = keep;
            if (p != null) p.Melee = true;
            return p;
        }

        // ------------------------------------------------------------------ 306: the Scourge of the Corruptor's javelin (AI_002)

        /// <summary>P:23858-23880: no fall; once it shows (alpha 200 or less) four small motes (184) along its last step; fading in 50 an update, turned along its flight.</summary>
        static void StepScourge(Proj p)
        {
            var v = TV(p);
            var c = CenterPx(p);
            if (p.Alpha <= 200f / 255f)
                for (int k = 0; k < 4; k++)
                {
                    int i = BoxDust(c - new Vector2(7f, 7f), 14, 14, 184);
                    if (i < 0) continue;
                    ref var d = ref D(i);
                    d.Position = c - v / 4f * k;
                    d.Velocity = Vector2.zero;
                    d.Scale = 0.7f;
                }
            p.Alpha = Mathf.Max(0f, p.Alpha - 50f / 255f);
            if (p.Vel.y < -32f) p.Vel.y = -32f;
            p.TRot = ToRot(v) + 0.785f;
        }

        /// <summary>M:35194-35198: the whole texture about its centre, lit, fading in.</summary>
        static void DrawScourge(FxBatch fx, Proj p, Vector2 px) =>
            fx.Draw(FxBatch.Whole("Projectile_306"), px, new Color(L, L, L, 1f) * (1f - p.Alpha), p.TRot, p.Scale);

        /// <summary>
        /// P:78024-78058: the hit sound and fifty motes (184), then 2-4 tiny eaters (one time in a
        /// hundred 15) from its corner, each up to 7 px an update either way, at three quarters of
        /// its damage and 35% of its knockback.
        /// </summary>
        void KillScourge(Proj p)
        {
            Sound("NPC_Hit_1");
            var tl = CenterPx(p) - new Vector2(7f, 7f);
            for (int k = 0; k < 20; k++)
            {
                int i = BoxDust(tl, 14, 14, 184);
                if (i < 0) continue;
                D(i).Scale *= 1.1f;
                D(i).NoGravity = true;
            }
            for (int k = 0; k < 30; k++)
            {
                int i = BoxDust(tl, 14, 14, 184);
                if (i < 0) continue;
                D(i).Velocity *= 2.5f;
                D(i).Scale *= 0.8f;
                D(i).NoGravity = true;
            }
            int n = Random.Range(2, 5);
            if (Random.Range(1, 101) == 100) n = 15;
            DevEaters += n;
            for (int k = 0; k < n; k++)
            {
                var v = new Vector2(Random.Range(-35, 36) * 0.02f * 10f, Random.Range(-35, 36) * 0.02f * 10f);
                MeleeChild(ProjKind.TinyEater, tl, v, (int)(p.Damage * 0.75), (int)(p.Knockback * 0.35f), p.Crit);
            }
        }

        // ------------------------------------------------------------------ 307: the tiny eater (AI_036)

        /// <summary>
        /// AI_036 for 307 (P:27682-27850): fading in 50 an update, then down to one update a tick;
        /// turned along its flight, two frames of 6 updates, three small motes (184) along its last
        /// step; after 30 updates it makes for the nearest enemy in sight within 800 px (Manhattan),
        /// turning harmful once it has one, at 13 px an update (0.35 a step, three times across
        /// zero), else on its way.
        /// </summary>
        void StepTinyEater(Proj p)
        {
            if (p.Alpha > 0f) p.Alpha = Mathf.Max(0f, p.Alpha - 50f / 255f);
            else p.Extra = 0;
            var v = TV(p);
            p.TRot = ToRot(v) - 1.57f;
            if (++p.FrameCounter >= 6)
            {
                p.Frame++;
                p.FrameCounter = 0;
            }
            if (p.Frame >= 2) p.Frame = 0;
            var c = CenterPx(p);
            for (int k = 0; k < 3; k++)
            {
                int i = BoxDust(c - new Vector2(8f, 8f), 16, 16, 184);
                if (i < 0) continue;
                ref var d = ref D(i);
                d.Position = c - v / 3f * k;
                d.Velocity = Vector2.zero;
                d.Scale = 0.5f;
            }
            var aim = c + v * 100f;
            bool found = false;
            if (++p.Ai0 > 30f)
            {
                p.Ai0 = 30f;
                float best = 800f;
                foreach (var hm in EnemiesNear(c, 800f))
                {
                    var e = EnemyCenterPx(hm);
                    float m = Mathf.Abs(c.x - e.x) + Mathf.Abs(c.y - e.y);
                    if (m < best && InSight(c, e))
                    {
                        best = m;
                        aim = e;
                        found = true;
                    }
                }
            }
            if (found) p.LocalAi1 = 1f;
            var to = aim - c;
            var want = to.sqrMagnitude > 1e-6f ? to.normalized * 13f : Vector2.zero;
            v.x = Approach(v.x, want.x, 0.35f, 2f);
            v.y = Approach(v.y, want.y, 0.35f, 2f);
            p.Vel = Flip(v);
        }

        /// <summary>P:19044-19055: off a block it bounces back the way it met it, five times; the sixth it's gone.</summary>
        void BounceEater(Proj p, bool hitX, bool hitY, Vector2 before)
        {
            if (p.Ai1 >= 5f)
            {
                Kill(p);
                return;
            }
            p.Ai1++;
            if (hitX) p.Vel.x = -before.x;
            if (hitY) p.Vel.y = -before.y;
        }

        /// <summary>Main.DrawProj's framed branch: its frame about its centre, lit, fading in.</summary>
        static void DrawTinyEater(FxBatch fx, Proj p, Vector2 px)
        {
            var whole = FxBatch.Whole("Projectile_307");
            if (whole == null) return;
            int w = (int)whole.rect.width, fh = (int)whole.rect.height / 2;
            fx.Draw(FxBatch.Frame("Projectile_307", 0, p.Frame * fh, w, fh, w / 2f, fh / 2f), px, new Color(L, L, L, 1f) * (1f - p.Alpha), p.TRot, p.Scale);
        }

        // ------------------------------------------------------------------ 636: Daybreak (AI_113)

        /// <summary>P:15869-15872: it meets an enemy's box shrunk by an eighth on every side.</summary>
        static bool DaybreakTouches(Proj p, Collider2D col)
        {
            var b = col.bounds;
            var size = (Vector2)b.size;
            if (size.x > 8f * Px && size.y > 8f * Px) size *= 0.75f;
            var mine = new Vector2(p.Spec.W, p.Spec.H) * Px;
            var d = p.Pos - (Vector2)b.center;
            return Mathf.Abs(d.x) * 2f < size.x + mine.x && Mathf.Abs(d.y) * 2f < size.y + mine.y;
        }

        /// <summary>
        /// AI_113 for 636 (P:41290-41630): fading in 25 an update; in flight turned along it and,
        /// after 45 updates, slowing (0.995) and falling 0.15 an update. Stuck, it rides on its
        /// enemy where it went in, its enemy's hit effect every 30 updates, out after 600 updates
        /// (5 s) or with its enemy. A warm light either way.
        /// </summary>
        void StepDaybreak(Proj p)
        {
            p.Alpha = Mathf.Max(0f, p.Alpha - 25f / 255f);
            var v = TV(p);
            if (p.Ai0 == 0f)
            {
                if (++p.Ai1 >= 45f)
                {
                    v.x *= 0.995f;
                    v.y += 0.15f;
                    p.Vel = Flip(v);
                }
                p.TRot = ToRot(v) + Mathf.PI / 2f;
            }
            else
            {
                var hm = p.Target;
                if (++p.LocalAi0 >= 600f || hm == null || hm.isDead || !hm.gameObject.activeInHierarchy)
                {
                    Kill(p);
                    return;
                }
                // Center = the enemy's centre less twice the velocity, then UpdatePosition adds it once.
                p.Pos = TerraParticles.ToWorld(EnemyCenterPx(hm) - v * 2f);
                if (p.LocalAi0 % 30f == 0f)
                {
                    var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
                    var info = EnemyStats.Of(hm);
                    if (col != null && info != null) TerraHitFx.Bleed(hm, col.bounds, 1, 0f, EnemyStats.LifeMax(info));
                }
            }
            FxBatch.Instance?.Light(CenterPx(p), new Color(0.8f, 0.7f, 0.4f), 3f);
        }

        /// <summary>
        /// Projectile.Damage (P:13244-13251, 11345-11348, 14044-14056): in it goes: stuck (ai[0] 1),
        /// its velocity three quarters of the way to the enemy's centre, Daybroken for 5 s; past
        /// eight in one enemy the one with the least time left comes out (and goes off).
        /// </summary>
        void StickDaybreak(Proj p, HealthManager hm)
        {
            if (p.Ai0 != 0f) return;
            p.Ai0 = 1f;
            p.Target = hm;
            p.Collide = false;
            p.Vel = Flip((EnemyCenterPx(hm) - CenterPx(p)) * 0.75f);
            EnemyDebuffs.Add(hm, EnemyDebuffs.Daybroken, 300);
            DevDaybreakStuck++;
            Proj oldest = null;
            int n = 0;
            foreach (var q in live)
            {
                if (q == p || q.Dead || q.Kind != ProjKind.Daybreak || q.Ai0 != 1f || q.Target != hm) continue;
                n++;
                if (oldest == null || q.TimeLeft < oldest.TimeLeft) oldest = q;
            }
            if (n >= 8 && oldest != null) Kill(oldest);
        }

        /// <summary>
        /// M:30651-31427, 31620, P:80827-80830: seven copies at its old places 1, 4 ... 19, lit,
        /// fading (20 - i) / 30 and shrinking toward half size by the 15th; then itself, full white
        /// at a quarter alpha (mostly added); pivoting 10 px below its point.
        /// </summary>
        static void DrawDaybreak(FxBatch fx, Proj p, Vector2 px)
        {
            var whole = FxBatch.Whole("Projectile_636");
            if (whole == null) return;
            int w = (int)whole.rect.width, h = (int)whole.rect.height;
            var spr = FxBatch.Frame("Projectile_636", 0, 0, w, h, w / 2f, 10f);
            for (int i = 1; i < 20 && i < p.Trail.Count; i += 3)
                fx.Draw(spr, TerraParticles.ToPx(p.Trail[i]), new Color(L, L, L, 1f) * ((20 - i) / 30f), p.TRot, Mathf.Lerp(p.Scale, 0.5f, i / 15f));
            float a = 1f - p.Alpha;
            fx.Draw(spr, px, new Color(a, a, a, (64f - p.Alpha * 255f / 4f) / 255f), p.TRot, p.Scale);
        }

        /// <summary>
        /// P:73384-73404: five embers (6, 259, 158) thrown up and its way off its old places 0 and 3,
        /// then the solar blast at its centre with its damage, 10 knockback and a size of 0.85-2.
        /// </summary>
        void KillDaybreak(Proj p)
        {
            float dir = p.Vel.x < 0f ? -1f : 1f;
            for (int k = 0; k < 6; k += 3)
            {
                var at = k < p.Trail.Count ? TerraParticles.ToPx(p.Trail[k]) : CenterPx(p);
                for (int j = 0; j < 5; j++)
                {
                    int type = Random.Range(0, 3) switch { 0 => 6, 1 => 259, _ => 158 };
                    int i = BoxDust(at - new Vector2(8f, 8f), 16, 16, type, 2.5f * dir, -2.5f, 200);
                    if (i < 0) continue;
                    D(i).Velocity *= 2.4f;
                    D(i).Scale += Random.value;
                }
            }
            var blast = MeleeChild(ProjKind.DaybreakBlast, CenterPx(p), Vector2.zero, p.Damage, 10f, p.Crit);
            if (blast != null) blast.Ai1 = 0.85f + Random.value * 1.15f;
        }

        // ------------------------------------------------------------------ 953: the solar blast (AI_117)

        /// <summary>
        /// AI_117 for 612/953 (P:35095-35175): growing 0.01 a tick from its ai[1]; five frames of 3
        /// ticks, then gone; a warm light. On its first tick it swells to 52 × its size (and stays
        /// so), strikes, booms (Item_14) and throws smoke (31) and fire (6) through and round it.
        /// </summary>
        static void StepSolarBlast(Proj p)
        {
            p.Ai1 += 0.01f;
            p.Scale = p.Ai1;
            if (++p.Ai0 >= 15f)
            {
                TerraProjectiles.Instance?.Kill(p);
                return;
            }
            if (++p.FrameCounter >= 3)
            {
                p.FrameCounter = 0;
                p.Frame++;
            }
            p.Alpha = Mathf.Max(0f, p.Alpha - 63f / 255f);
            var c = CenterPx(p);
            FxBatch.Instance?.Light(c, new Color(0.9f, 0.8f, 0.6f), 3f);
            if (p.Ai0 != 1f) return;
            int size = (int)(52f * p.Scale);
            p.HitSize = new Vector2(size, size);
            DevBlasts++;
            Sound("Item_14");
            var tl = c - new Vector2(size / 2f, size / 2f);
            Vector2 Inside() => c + RotateBy(Vector2.up, Random.Range(-Mathf.PI, Mathf.PI)) * Random.value * size / 2f;
            Vector2 Edge() => c + RotateBy(Vector2.right, Random.Range(-Mathf.PI, Mathf.PI)) * size / 2f;
            for (int k = 0; k < 4; k++)
            {
                int i = BoxDust(tl, size, size, 31, 0f, 0f, 100, 1.5f);
                if (i >= 0) D(i).Position = Inside();
            }
            for (int k = 0; k < 10; k++)
            {
                int i = BoxDust(tl, size, size, 6, 0f, 0f, 200, 2.7f);
                if (i >= 0)
                {
                    D(i).Position = Inside();
                    D(i).NoGravity = true;
                    D(i).Velocity *= 3f;
                }
                i = BoxDust(tl, size, size, 6, 0f, 0f, 100, 1.5f);
                if (i < 0) continue;
                D(i).Position = Inside();
                D(i).Velocity *= 2f;
                D(i).NoGravity = true;
                D(i).FadeIn = 2.5f;
            }
            for (int k = 0; k < 5; k++)
            {
                int i = BoxDust(tl, size, size, 6, 0f, 0f, 0, 2.7f);
                if (i < 0) continue;
                D(i).Position = Edge();
                D(i).NoGravity = true;
                D(i).Velocity *= 3f;
            }
            for (int k = 0; k < 10; k++)
            {
                int i = BoxDust(tl, size, size, 31, 0f, 0f, 0, 1.5f);
                if (i < 0) continue;
                D(i).Position = Edge();
                D(i).NoGravity = true;
                D(i).Velocity *= 3f;
            }
        }

        /// <summary>M:33741-33760, P:80855-80858: its frame about its centre at its size, white at half alpha; hidden past the fifth frame.</summary>
        static void DrawSolarBlast(FxBatch fx, Proj p, Vector2 px)
        {
            if (p.Frame >= 5) return;
            var whole = FxBatch.Whole(p.Spec.Texture);
            if (whole == null) return;
            int w = (int)whole.rect.width, fh = (int)whole.rect.height / 5;
            fx.Draw(FxBatch.Frame(p.Spec.Texture, 0, p.Frame * fh, w, fh, w / 2f, fh / 2f), px, new Color(1f, 1f, 1f, 0.5f), 0f, p.Scale);
        }

        public static int DevEaters, DevDaybreakStuck, DevBlasts;

        /// <summary>The Daybreaks stuck in an enemy (NPC.AddStackedProjecitle's count).</summary>
        public int StuckDaybreaks(HealthManager hm)
        {
            int n = 0;
            foreach (var q in live) if (!q.Dead && q.Kind == ProjKind.Daybreak && q.Ai0 == 1f && q.Target == hm) n++;
            return n;
        }
    }
}
