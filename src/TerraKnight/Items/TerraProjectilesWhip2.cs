using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// What the 1.4.5 whips' tags spawn (summon-hm sec-D D.7-D.11; Terraria's space, px y down):
    /// Starfall's meteor (1037: falls on the enemy through the ceiling, a 64 px blast), the Flower
    /// whip's petals (1038: brake, then home), the Constellation's stars (1039: curl, home, bounce;
    /// each hit re-tags) and the Moon Lord whip's phantom slash (1045: a curving cut, chaining up
    /// to three times). None of them is a minion shot: no tag damage on their hits.
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        static void RegisterWhip2()
        {
            specs[ProjKind.WhipMeteor] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_1037", W = 24, H = 24, Scale = 0.8f, Time = 3600, Extra = 1, OncePerNpc = true, TileCollide = false,
                AlphaStart = 254f / 255f, TrailLength = 10,
                Update = (t, p) => StepWhipMeteor(p), Draw = (t, fx, p, px) => DrawWhipMeteor(fx, p, px), OnKill = (t, p) => t.KillWhipMeteor(p),
            };
            specs[ProjKind.FlowerPetal] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_1038", W = 18, H = 18, Time = 120, OncePerNpc = true, TileCollide = false, ArmorPen = 50,
                Update = (t, p) => StepFlowerPetal(p), Draw = (t, fx, p, px) => DrawFlowerPetal(fx, p, px), OnKill = (t, p) => KillFlowerPetal(p),
            };
            specs[ProjKind.ConstellationStar] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_1039", W = 30, H = 30, Time = 240, Extra = 1, OncePerNpc = true, TileBox = new Vector2(4f, 4f),
                AlphaStart = 1f, TrailLength = 20,
                Update = (t, p) => StepConstellationStar(p), Draw = (t, fx, p, px) => DrawConstellationStar(fx, p, px), OnKill = (t, p) => KillConstellationStar(p),
                OnHit = (t, p, hm) => HitConstellationStar(p, hm),
                TileHit = (t, p, hitX, hitY, oldVel) =>
                {
                    if (hitX) p.Vel.x = -oldVel.x;
                    if (hitY) p.Vel.y = -oldVel.y;
                },
            };
            specs[ProjKind.PhantomSlash] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_1045", W = 20, H = 20, Time = 48, Extra = 1, OncePerNpc = true, TileCollide = false, ArmorPen = 50,
                AlphaStart = 1f, TrailLength = 30,
                Update = (t, p) => StepPhantomSlash(p), Draw = (t, fx, p, px) => DrawPhantomSlash(fx, p, px), OnHit = (t, p, hm) => t.HitPhantomSlash(p, hm),
            };
        }

        /// <summary>An enemy's collider in Terraria px (top-left, y down).</summary>
        static Rect EnemyRectPx(HealthManager hm)
        {
            var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
            if (col == null) return new Rect(EnemyCenterPx(hm), Vector2.zero);
            var b = col.bounds;
            return new Rect(b.min.x / Px, -b.max.y / Px, b.size.x / Px, b.size.y / Px);
        }

        /// <summary>An enemy's velocity in Terraria px a tick (y down).</summary>
        static Vector2 EnemyVelPx(HealthManager hm)
        {
            var rb = hm.GetComponent<Rigidbody2D>();
            return rb != null ? new Vector2(rb.linearVelocity.x, -rb.linearVelocity.y) / Px / 60f : Vector2.zero;
        }

        static Vector2 RotPx(Vector2 v, float a) => new Vector2(v.x * Mathf.Cos(a) - v.y * Mathf.Sin(a), v.x * Mathf.Sin(a) + v.y * Mathf.Cos(a));

        // ------------------------------------------------------------------ 1037: Starfall's meteor (AI_005 branch)

        /// <summary>WhipTagEffect_Starcrash.OnProcHit: 280 px off within 45° of straight up, at the enemy's centre at 8 px an update, through blocks until below its top.</summary>
        public void ShootWhipMeteor(HealthManager hm, int damage, float knockback)
        {
            if (hm == null) return;
            var c = EnemyCenterPx(hm);
            var from = c + RotPx(new Vector2(0f, -280f), Random.Range(-1f, 1f) * Mathf.PI * 2f * 0.125f);
            var v = (c - from).normalized * 8f;
            var p = Spawn(ProjKind.WhipMeteor, TerraParticles.ToWorld(from), Flip(v), damage, knockback, 0f);
            if (p == null) return;
            p.Ai1 = EnemyRectPx(hm).yMin;
            p.Collide = false;
        }

        /// <summary>P:24117-24364: blocks count once its bottom is below the enemy's top; it fades in (10 an update); flames and smoke off both flanks of its head.</summary>
        static void StepWhipMeteor(Proj p)
        {
            var c = CenterPx(p);
            p.Collide = c.y + 9.5f >= p.Ai1;
            p.Alpha = Mathf.Max(0f, p.Alpha - 10f / 255f);
            var v = TV(p);
            p.TRot = Mathf.Atan2(v.y, v.x) - Mathf.PI / 2f;
            var head = c + v.normalized * 10f;
            for (int side = -1; side <= 1; side += 2)
            {
                if (Random.Range(0, 2) != 0) continue;
                var perp = RotPx(v, side * Mathf.PI / 2f);
                int i = PointDust(head + perp, Random.Range(0, 4) == 0 ? 6 : 31, (perp * 0.25f + v / 3f) * 0.9f * Random.value);
                if (i < 0) continue;
                D(i).Scale *= 0.3f + 0.9f * Random.value;
                D(i).NoGravity = true;
            }
            int j = PointDust(c + Random.insideUnitCircle * 25f, 6, v * 0.5f, 0, 1.3f);
            if (j >= 0)
            {
                D(j).FadeIn = 1f;
                D(j).NoGravity = true;
            }
        }

        /// <summary>M:31061-31066, 31177: five copies at its odd old places, redder and more added further back, shrinking; the rock at 0.8.</summary>
        static void DrawWhipMeteor(FxBatch fx, Proj p, Vector2 px)
        {
            var spr = FxBatch.Frame("Projectile_1037", 0, 0, 36, 36, 18f, 18f);
            var lit = new Color(L, L, L, 1f);
            float a = 1f - p.Alpha;
            for (int i = 1; i < 10; i += 2)
            {
                if (i - 1 >= p.Trail.Count) break;
                var c = Color.Lerp(lit, Color.red, 0.5f) * a;
                c.b /= i;
                c.g /= i;
                c.a /= i;
                fx.Draw(spr, TerraParticles.ToPx(p.Trail[i - 1]), c * ((10 - i) / 15f), p.TRot, Mathf.Lerp(0.8f, 0.6f, i / 15f));
            }
            fx.Draw(spr, px, lit * a, p.TRot, 0.8f);
        }

        /// <summary>P:75283-75346: Item89 and a 64 px blast (not the one it hit), smoke and a burst of fire.</summary>
        void KillWhipMeteor(Proj p)
        {
            Sound("Item_89");
            var melee = TerraMod.Instance?.Melee;
            if (melee != null)
            {
                float facing = p.Vel.x < 0f ? -1f : 1f;
                foreach (var hm in EnemiesIn(p.Pos, 64f))
                {
                    int id = hm.GetInstanceID();
                    if (p.Immune.ContainsKey(id)) continue;
                    var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
                    if (col == null) continue;
                    melee.HitWith(col, p.Damage, p.Knockback, 0f, facing, p.Pos, Equipment.Current.ArmorPen);
                    p.Immune[id] = int.MaxValue;
                    DevHits[p.Kind] = DevHit(p.Kind) + 1;
                }
            }
            var c = CenterPx(p);
            for (int k = 0; k < 6; k++) BoxDust(c - new Vector2(32f, 32f), 64, 64, 31, 0f, 0f, 100, 1.5f);
            var pp = TerraParticles.Instance;
            if (pp != null)
                for (int k = 0; k < 3; k++)
                    pp.NewGore(c + Random.insideUnitCircle * 25f - new Vector2(20f, 20f), Vector2.zero, Random.Range(61, 64), 1f, 0.3f,
                        new Vector2(Random.Range(-10, 11) * 0.05f, Random.Range(-10, 11) * 0.05f));
            for (int k = 0; k < 24; k++)
            {
                int i = BoxDust(c - new Vector2(32f, 32f), 64, 64, 6, 0f, 0f, 100, 2.5f);
                if (i >= 0)
                {
                    D(i).NoGravity = true;
                    D(i).Velocity *= 3f;
                }
                i = BoxDust(c - new Vector2(32f, 32f), 64, 64, 6, 0f, 0f, 100, 1.5f);
                if (i >= 0)
                {
                    D(i).Velocity *= 2f;
                    D(i).NoGravity = true;
                }
            }
            float dir = p.Vel.x < 0f ? -1f : 1f;
            int[] kinds = { 6, 259, 158 };
            for (int k = 0; k < 4; k++)
            {
                int i = BoxDust(c - new Vector2(32f, 32f), 64, 64, kinds[Random.Range(0, 3)], 2.5f * dir, -2.5f, 200);
                if (i < 0) continue;
                D(i).Velocity *= 2.4f;
                D(i).Scale += Random.value;
            }
        }

        // ------------------------------------------------------------------ 1038: the Flower whip's petals (AI_196)

        /// <summary>
        /// WhipTagEffect_ViolentDisplayOfFlower.OnProcHit: three to six petals out of the enemy,
        /// sharing 2.25× the minion's hit, flung at a speed by its size, sparing it for 30 ticks.
        /// </summary>
        public void ShootFlowerPetals(HealthManager hm, int num7)
        {
            if (hm == null) return;
            var r = EnemyRectPx(hm);
            var c = r.center;
            float a0 = Random.value * Mathf.PI * 2f;
            int n = 3 + Random.Range(0, 4);
            int dmg = Mathf.CeilToInt((int)(num7 * 2.25f) / (float)n);
            float speed = Mathf.Max(r.width, r.height) / 8f;
            for (int i = 0; i < n; i++)
            {
                var v = RotPx(new Vector2(1f, 0f), (float)i / n * Mathf.PI * 2f + a0 + Random.Range(-1f, 1f) * Mathf.PI / 8f) * speed;
                var p = Spawn(ProjKind.FlowerPetal, TerraParticles.ToWorld(c), Flip(v), dmg, 0f, 0f);
                if (p == null) continue;
                p.Ai0 = Random.value * -20f;
                p.Immune[hm.GetInstanceID()] = 30;
            }
        }

        /// <summary>AI_196_Petal (P:41193-41289): it brakes for 20-40 ticks (glowing from the tenth), then homes at up to 24 px on the nearest enemy (in sight past 400 px), or flutters down.</summary>
        static void StepFlowerPetal(Proj p)
        {
            var v = TV(p);
            // The Petalstorm's (1116, P:41197-41201): fading over its last 30 ticks, smaller, silent.
            bool storm = p.Kind == ProjKind.PetalstormPetal;
            if (storm) p.Alpha = Mathf.InverseLerp(30f, 0f, p.TimeLeft);
            if (p.LocalAi0 == 0f)
            {
                p.LocalAi0 = 1f;
                if (!storm) Sound("Item_17");
                p.Frame = Random.Range(0, 8);
                p.Dir = v.x < 0f ? -1f : 1f;
                p.TRot = Random.value * Mathf.PI * 2f;
                p.Scale = 0.6f + 0.3f * Random.value;
                if (storm) p.Scale *= 0.75f;
            }
            if (++p.FrameCounter >= 4)
            {
                p.FrameCounter = 0;
                p.Frame = (p.Frame + 1) % 8;
            }
            p.TRot += v.x * 0.1f + v.y * 0.1f;
            bool waiting = p.Ai0 < 20f;
            p.Ai0++;
            if (waiting)
            {
                p.Vel *= 0.95f;
                return;
            }
            var c = CenterPx(p);
            HealthManager target = null;
            // The Petalstorm's look 400 px out, always needing sight, and home gently (6 px, inertia 30).
            float reach = storm ? 400f : 2000f, sight = storm ? 0f : 400f, best = reach;
            foreach (var hm in EnemiesNear(c, reach))
            {
                var e = EnemyCenterPx(hm);
                float d = Vector2.Distance(c, e);
                if (d >= best || (d > sight && !TerraWorld.LineClearPx(c, e))) continue;
                best = d;
                target = hm;
            }
            float speed = storm ? 6f : 24f, inertia = storm ? 30f : 15f;
            if (target != null) v = (v * inertia + (EnemyCenterPx(target) - c).normalized * speed) / (inertia + 1f);
            else if (storm) v = new Vector2(Mathf.Lerp(v.x, Mathf.Sin(p.Ai0 * 0.1f) * 1.5f, 0.1f), Mathf.Lerp(v.y, 0.3f, 0.1f));
            else v = new Vector2(Mathf.Lerp(v.x, Mathf.Sin(p.Ai0) * 2.5f, 0.1f), Mathf.Lerp(v.y, 0.3f, 0.1f));
            p.Vel = Flip(v);
        }

        /// <summary>M:33741ff, 34438-34441: the petal turning, and a magenta four-point flash while it brakes.</summary>
        static void DrawFlowerPetal(FxBatch fx, Proj p, Vector2 px)
        {
            bool storm = p.Kind == ProjKind.PetalstormPetal;
            float o = 1f - p.Alpha;
            fx.Draw(FxBatch.Frame(p.Spec.Texture, 0, 20 * p.Frame, 18, 20, 9f, 10f), px, new Color(L, L, L, 1f) * o, p.TRot, p.Scale, flipX: p.Dir < 0f);
            // The Petalstorm's flash is sky blue (M:34442-34445), and its light teal (P:41197-41232), dimming over its life.
            if (storm)
            {
                if (p.Ai0 >= 10f) fx.Light(px, new Color(0f, 0.6f, 0.6f) * (p.Ai0 < 20f ? 1f : Mathf.InverseLerp(0f, 120f, p.TimeLeft)), 2f);
                FxParticles.DrawStarSparkle(fx, o, px, new Color(1f, 1f, 1f, 0f) * 0.5f, FxBatch.C(0, 160, 255), p.Ai0, 0f, 5f, 5f, 20f, Mathf.PI / 2f,
                    new Vector2(0.7f, 1.5f), Vector2.one, false);
                return;
            }
            FxParticles.DrawStarSparkle(fx, 1f, px, new Color(1f, 1f, 1f, 0f) * 0.5f, FxBatch.C(255, 0, 77), p.Ai0, 0f, 5f, 5f, 20f, Mathf.PI / 2f,
                new Vector2(0.7f, 1.5f), Vector2.one, false);
            if (p.Ai0 >= 10f && p.Ai0 < 20f) fx.Light(px, new Color(0.5f, 0f, 0.2f), 2f);
        }

        /// <summary>P:72193-72201: four Plantera petals (166) where it was.</summary>
        static void KillFlowerPetal(Proj p)
        {
            var c = CenterPx(p);
            for (int k = 0; k < 4; k++)
            {
                int i = PointDust(c + Random.insideUnitCircle * 10f * p.Scale, 166, Vector2.zero, 0, 1.5f + 0.25f * Random.value);
                if (i >= 0) D(i).NoGravity = true;
            }
        }

        // ------------------------------------------------------------------ 1039: the Constellation's stars (AI_171)

        /// <summary>One star from the whip's chain (px, px an update), homing on an enemy, its hue offset.</summary>
        public void ShootConstellationStar(Vector2 fromPx, Vector2 velPx, int damage, float knockback, HealthManager target, float hue)
        {
            var p = Spawn(ProjKind.ConstellationStar, TerraParticles.ToWorld(fromPx), Flip(velPx), damage, knockback, 0f);
            if (p == null) return;
            p.Target = target;
            p.Ai1 = hue;
        }

        /// <summary>GetConstellationColor (P:21761): hue 0.52-0.61 (cyan to azure) by the star, faded by its opacity.</summary>
        static Color StarColor(Proj p, float aMul)
        {
            var c = FxParticles.HslToRgb((p.Ai1 + 0.5f) % 1f, 1f, 0.5f) * (1f - p.Alpha);
            c.a *= aMul;
            return c;
        }

        /// <summary>
        /// AI_171 (P:46287-46397): three sparks as it appears; for its first 30 ticks it drifts and
        /// curls; then for 40 it steers ever harder toward its enemy (or the nearest in sight within
        /// 1600 px), slowing as it ages; it fades in over its first 10 ticks.
        /// </summary>
        static void StepConstellationStar(Proj p)
        {
            var v = TV(p);
            var c = CenterPx(p);
            if (p.TimeLeft == 238)
                for (int k = 0; k < 3; k++)
                {
                    int i = PointDust(c, 267, Random.insideUnitCircle.normalized * 3f * (0.5f + 0.5f * Random.value));
                    if (i < 0) continue;
                    D(i).Color = FxParticles.HslToRgb((p.Ai1 + 0.5f) % 1f, 1f, 0.5f);
                    D(i).Scale *= 1.2f;
                    D(i).NoGravity = true;
                }
            if (p.TimeLeft > 180)
            {
                float w = Mathf.Cos(p.GetHashCode() % 6 / 6f + c.x / 320f + c.y / 160f);
                v = RotPx(v * 0.97f, w * Mathf.PI * 2f * 0.125f / 30f);
            }
            if (p.Target != null && !Chaseable(p.Target)) p.Target = null;
            if (p.Target == null)
            {
                // The Nightglow's streaks look 800 px out (P:46297-46301).
                float reach = p.Kind == ProjKind.NightglowStar ? 800f : 1600f, best = reach;
                foreach (var hm in EnemiesNear(c, reach))
                {
                    var e = EnemyCenterPx(hm);
                    float d = Vector2.Distance(c, e);
                    if (d >= best || p.Immune.ContainsKey(hm.GetInstanceID()) || !TerraWorld.LineClearPx(c, e)) continue;
                    best = d;
                    p.Target = hm;
                }
            }
            if (p.TimeLeft > 20 && p.TimeLeft <= 180)
            {
                Vector2 want;
                if (p.Target != null) want = (EnemyCenterPx(p.Target) - c).normalized * 30f;
                else
                {
                    want = v;
                    p.TimeLeft--;
                }
                float a = Mathf.Lerp(0.075f, 0.125f, Mathf.InverseLerp(180f, 30f, p.TimeLeft));
                v = Vector2.Lerp(v, want, a * a * (3f - 2f * a));
                v *= Mathf.Lerp(0.85f, 1f, Mathf.InverseLerp(0f, 90f, p.TimeLeft));
            }
            p.Alpha = 1f - Mathf.InverseLerp(240f, 220f, p.TimeLeft);
            p.Vel = Flip(v);
            p.TRot = Mathf.Atan2(v.y, v.x) + Mathf.PI / 2f;
        }

        /// <summary>P:14305-14320: a stardust punch where it struck, and the Constellation's tag again.</summary>
        static void HitConstellationStar(Proj p, HealthManager hm)
        {
            var c = CenterPx(p);
            var r = EnemyRectPx(hm);
            var near = new Vector2(Mathf.Clamp(c.x, r.xMin, r.xMax), Mathf.Clamp(c.y, r.yMin, r.yMax));
            float dir = p.Vel.x < 0f ? -1f : 1f;
            FxParticles.StardustPunch(Vector2.Lerp(c, near, 0.5f) + new Vector2(0f, Random.Range(-1f, 1f) * 10f),
                new Vector2(dir, Random.Range(-1f, 1f) * 0.5f) * (2f + 2f * Random.value));
            SummonTags.Apply(WhipKind.ConstellationWhip, hm);
        }

        /// <summary>M:30959-30965, 31177, 31448-31481: an azure trail of its last 19 places, an added body, a twinkling cross, a half-added white core.</summary>
        static void DrawConstellationStar(FxBatch fx, Proj p, Vector2 px)
        {
            var spr = FxBatch.Frame(p.Spec.Texture, 0, 0, 36, 36, 18f, 18f);
            float tail = Mathf.InverseLerp(0f, 20f, p.TimeLeft);
            for (int i = Mathf.Min(19, p.Trail.Count); i >= 1; i--)
            {
                var c = StarColor(p, 0.5f) * tail * ((19 - i) / 30f);
                fx.Draw(spr, TerraParticles.ToPx(p.Trail[i - 1]), c, p.TRot, Mathf.Lerp(1f, 0.7f, i / 20f));
            }
            var g = StarColor(p, 0f);
            fx.Draw(spr, px, g, p.TRot, 0.9f);
            float s = Mathf.InverseLerp(15f, 30f, p.TimeLeft) * Mathf.InverseLerp(240f, 200f, p.TimeLeft) * (1f + 0.2f * Mathf.Cos(Time.time % 30f / 0.5f * Mathf.PI * 2f * 3f)) * 0.8f;
            if (s > 0f)
            {
                var tex = FxBatch.Whole("Extra_98");
                var at = px + TV(p).normalized * 0.5f;
                fx.Draw(tex, at, g * s, Mathf.PI / 2f, new Vector2(0.2f, 2f) * s);
                fx.Draw(tex, at, g * s, 0f, new Vector2(0.2f, 0.8f) * s);
                fx.Draw(tex, at, g * 0.5f * s, Mathf.PI / 2f, new Vector2(0.12f, 1.2f) * s);
                fx.Draw(tex, at, g * 0.5f * s, 0f, new Vector2(0.12f, 0.48f) * s);
            }
            float o = 1f - p.Alpha;
            fx.Draw(spr, px, new Color(o, o, o, o * 0.5f), p.TRot, 1f);
        }

        /// <summary>P:72308-72353: Item10 and azure motes all along its trail, fewer and smaller toward the tail.</summary>
        static void KillConstellationStar(Proj p)
        {
            Sound("Item_10");
            var col = FxParticles.HslToRgb((p.Ai1 + 0.5f) % 1f, 1f, 0.5f);
            for (int j = 0; j < p.Trail.Count; j++)
            {
                int n = Random.Range(1, 3) - (j >= 6 ? 1 : 0) - (j >= 15 ? 2 : 0);
                float m = Mathf.Lerp(0.3f, 1f, Mathf.InverseLerp(20f, 0f, j));
                var at = TerraParticles.ToPx(p.Trail[j]) - new Vector2(15f, 15f);
                for (int k = 0; k < n; k++)
                {
                    float r = Random.value;
                    int i = BoxDust(at, 30, 30, 267);
                    if (i < 0) continue;
                    D(i).Color = col;
                    D(i).Velocity *= Random.value * 0.8f;
                    D(i).NoGravity = true;
                    D(i).Scale = (0.9f + 1.2f * r) * m;
                    D(i).FadeIn = r * 1.2f * m;
                    var pp = TerraParticles.Instance;
                    int w = pp.Clone(i);
                    if (w < 0) continue;
                    pp[w].Scale /= 2f;
                    pp[w].FadeIn *= 0.85f;
                    pp[w].Color = Color.white;
                }
            }
        }

        // ------------------------------------------------------------------ 1045: the Moon Lord whip's phantom slash (AI_152)

        /// <summary>
        /// SpawnMoonLordWhipProc (P:14978-15012): from a random point of the enemy's box (inflated
        /// 30 by 16, raised 8) toward its middle, turned 45°, walked back along a curve 16 steps,
        /// led by the enemy's motion; an eye where it starts; 20 damage, the chain counter.
        /// </summary>
        public void ShootPhantomSlash(HealthManager hm, int damage, int chain, Vector2 fallbackDir)
        {
            if (hm == null || hm.isDead) return;
            var box = EnemyRectPx(hm);
            box = new Rect(box.x - 30f, box.y - 16f - 8f, box.width + 60f, box.height + 32f);
            var a = new Vector2(Random.Range(box.xMin, box.xMax), Random.Range(box.yMin, box.yMax));
            var c = box.center;
            float speed = 7f + 8f * Random.value;
            var fb = fallbackDir.sqrMagnitude > 0f ? fallbackDir.normalized : new Vector2(0f, -1f);
            var v = ((c - a).sqrMagnitude > 0f ? (c - a).normalized : fb) * speed;
            float turn = (Random.Range(0, 2) * 2 - 1) * (Mathf.PI / 5f + 4f * Mathf.PI / 5f * Random.value) * 0.5f;
            v = RotPx(v, Mathf.PI / 4f);
            var p0 = c;
            for (int k = 0; k < 16; k++)
            {
                p0 -= v;
                v = RotPx(v, -turn / 24f);
            }
            p0 += EnemyVelPx(hm) * 16f;
            FxParticles.MoonLordWhipEye(p0, -v);
            var p = Spawn(ProjKind.PhantomSlash, TerraParticles.ToWorld(p0), Flip(v), damage, 0f, 0f);
            if (p == null) return;
            p.Ai0 = turn;
            p.Ai1 = chain;
            p.Scale = 1f + Random.Range(0, 30) * 0.01f;
            p.HitSize = Vector2.one * (int)(20f * p.Scale);
            // melee = true: it rolls the player's melee crit.
            p.Crit = 4f + (Equipment.Current.MeleeCrit + Equipment.Current.AllCrit) * 100f;
        }

        /// <summary>P:22433-22555: fades in (50 an update), one swish, curving through its turn over 48 updates and slowing; vortex motes now and then.</summary>
        static void StepPhantomSlash(Proj p)
        {
            p.Alpha = Mathf.Max(0f, p.Alpha - 50f / 255f);
            if (p.Updates == 1) Sound("Item_1");
            var v = TV(p);
            if (p.Ai0 != 0f) v = RotPx(v, p.Ai0 / 48f);
            v *= 0.96f;
            if (Random.Range(0, 8) == 0)
            {
                int i = PointDust(CenterPx(p), 229, Random.insideUnitCircle + v * 0.5f, 100, 0.9f);
                if (i >= 0) D(i).NoGravity = true;
            }
            p.Vel = Flip(v);
            p.TRot = Mathf.Atan2(v.y, v.x) + Mathf.PI / 2f;
        }

        /// <summary>P:13027-13031, 14015-14019: a streak where it cut, and while the chain is short another slash at the same enemy.</summary>
        void HitPhantomSlash(Proj p, HealthManager hm)
        {
            var r = EnemyRectPx(hm);
            var at = Vector2.Lerp(new Vector2(Random.Range(r.xMin, r.xMax), Random.Range(r.yMin, r.yMax)), r.center, 0.5f);
            var v = TV(p);
            FxParticles.MoonLordWhipHit(at, Mathf.Atan2(v.y, v.x));
            if (p.Ai1 < 2f) ShootPhantomSlash(hm, p.Damage, (int)p.Ai1 + 1, v);
        }

        /// <summary>M:30837-30843, 31177: fullbright and a fifth added, eight copies on its old places growing toward the tail, then the slash.</summary>
        static void DrawPhantomSlash(FxBatch fx, Proj p, Vector2 px)
        {
            var spr = FxBatch.Frame("Projectile_1045", 0, 0, 18, 72, 9f, 36f);
            var c0 = new Color(1f, 1f, 1f, 200f / 255f) * (1f - p.Alpha);
            for (int i = 18; i >= 2; i -= 2)
            {
                if (i - 1 >= p.Trail.Count) continue;
                fx.Draw(spr, TerraParticles.ToPx(p.Trail[i - 1]), c0 * ((18 - i) / 45f), p.TRot, Mathf.LerpUnclamped(p.Scale, 1.3f, i / 15f));
            }
            fx.Draw(spr, px, c0, p.TRot, p.Scale);
        }
    }
}
