using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The Nebula and lunar spells (Terraria's space, px y down, per update): the Nebula Blaze's
    /// bolt (634, and one time in five the big one, 635, at triple damage: they look for an enemy
    /// in sight, home on it and burst over 50 px) and the Lunar Flare's falling bolts (645: unseen
    /// until they meet an enemy, a block or come within 60 px of one, then a 140 px blast that hurts
    /// each enemy once).
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        static void RegisterLunar()
        {
            specs[ProjKind.NebulaBlaze] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_634", W = 40, H = 40, TileBox = new Vector2(8f, 8f), Time = 3600, Penetrate = 1, Extra = 2, AlphaStart = 1f, OwnerImmune = 5,
                Update = (t, p) => t.StepNebulaBlaze(p), Draw = (t, fx, p, px) => DrawNebulaBlaze(fx, p, px), OnKill = (t, p) => t.KillNebulaBlaze(p),
            };
            specs[ProjKind.NebulaBlazeBig] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_635", W = 40, H = 40, TileBox = new Vector2(8f, 8f), Time = 3600, Penetrate = 1, Extra = 3, AlphaStart = 1f, OwnerImmune = 5,
                Update = (t, p) => t.StepNebulaBlaze(p), Draw = (t, fx, p, px) => DrawNebulaBlaze(fx, p, px), OnKill = (t, p) => t.KillNebulaBlaze(p),
            };
            specs[ProjKind.LunarFlare] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_645", W = 10, H = 10, Time = 3600, Penetrate = -1, Extra = 5, TileCollide = false, OncePerNpc = true,
                Update = (t, p) => t.StepLunarFlare(p), Draw = (t, fx, p, px) => DrawLunarFlare(fx, p, px), OnHit = (t, p, hm) => ExplodeLunarFlare(p),
                TileHit = (t, p, hx, hy, ov) => ExplodeLunarFlare(p), OnKill = (t, p) => KillLunarFlare(p),
            };
            specs[ProjKind.NebulaArcanum] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_617", W = 32, H = 32, Time = 3600, Penetrate = 3, LocalCooldown = 8, AlphaStart = 1f, TileBox = new Vector2(20f, 20f),
                Update = (t, p) => t.StepNebulaArcanum(p), Draw = (t, fx, p, px) => DrawNebulaArcanum(fx, p, px), OnKill = (t, p) => t.KillNebulaArcanum(p),
                TileHit = (t, p, hx, hy, ov) => { if (hx) p.Vel.x = -ov.x * 0.35f; if (hy) p.Vel.y = -ov.y * 0.35f; },
            };
            specs[ProjKind.NebulaArcanumSub] = new Spec
            {
                Ai = Ai.Custom, W = 18, H = 30, Time = 420, Penetrate = -1, TileCollide = false, LocalCooldown = 20, Look = Look.Invisible,
                Update = (t, p) => t.StepNebulaArcanumSub(p), OnKill = (t, p) => KillNebulaShard(p, 86, 0.75f, 0.25f, 1f, 1.25f),
            };
            specs[ProjKind.NebulaArcanumShard] = new Spec
            {
                Ai = Ai.Custom, W = 8, H = 8, Time = 3600, Penetrate = 1, Look = Look.Invisible,
                Update = (t, p) => t.StepNebulaArcanumShard(p), OnKill = (t, p) => KillNebulaShard(p, (int)p.Ai0, 0.75f, 0.25f, 1f, 1.25f),
                TileHit = (t, p, hx, hy, ov) => { if (hx) p.Vel.x = -ov.x; if (hy) p.Vel.y = -ov.y; },
            };
        }

        // ------------------------------------------------------------------ 634/635: Nebula Blaze (AI_001)

        /// <summary>
        /// P:55427: a hum (Item34) as it sets off; every 4 updates it looks for an enemy in sight
        /// within 250 px (the big one 500); with one it bends towards it at 6 px an update,
        /// (v·29 + aim)/30, and bursts once within 20 px. It shows in fast, four frames, a pink (the
        /// big one blue) light, two motes spiralling into its head, and smoke.
        /// </summary>
        void StepNebulaBlaze(Proj p)
        {
            bool big = p.Kind == ProjKind.NebulaBlazeBig;
            float range = big ? 500f : 250f;
            var dustOff = big ? new Vector2(10f, 20f) : new Vector2(8f, 10f);
            int dustType = big ? 88 : 255, period = (big ? 3 : 4) * (p.Extra + 1);
            var c = CenterPx(p);
            var v = TV(p);
            if (p.Ai1 == 0f)
            {
                p.Ai1 = 1f;
                p.LocalAi0 = -Random.Range(0, 48);
                Sound("Item_34");
            }
            else if (p.Ai1 == 1f)
            {
                HealthManager found = null;
                foreach (var hm in EnemiesNear(c, range))
                {
                    var e = EnemyCenterPx(hm);
                    float d = Vector2.Distance(c, e);
                    if (d < range && InSight(c, e))
                    {
                        if (d < 20f)
                        {
                            Kill(p);
                            return;
                        }
                        found = hm;
                        break;
                    }
                }
                if (found != null)
                {
                    p.Ai1 = 6f;
                    p.Target = found;
                }
            }
            else if (p.Ai1 > 5f)
            {
                p.Ai1++;
                if (!Chaseable(p.Target)) p.Ai1 = 1f;
                else
                {
                    var to = EnemyCenterPx(p.Target) - c;
                    if (to.magnitude < 20f)
                    {
                        Kill(p);
                        return;
                    }
                    v = (v * 29f + to.normalized * 6f) / 30f;
                }
            }
            if (p.Ai1 >= 1f && p.Ai1 < 5f && ++p.Ai1 == 5f) p.Ai1 = 1f;
            p.Alpha = Mathf.Max(0f, p.Alpha - 40f / 255f);
            if (++p.FrameCounter >= period)
            {
                p.FrameCounter = 0;
                if (++p.Frame >= 4) p.Frame = 0;
            }
            p.Dir = v.x < 0f ? -1f : 1f;
            p.TRot = Mathf.Atan2(v.y, v.x);
            if (++p.LocalAi0 == 48f) p.LocalAi0 = 0f;
            else if (p.Alpha == 0f)
                for (int k = 0; k < 2; k++)
                {
                    var off = RotateBy(new Vector2(0f, -1f), p.LocalAi0 * Mathf.PI / 24f + k * Mathf.PI);
                    off = new Vector2(off.x * dustOff.x, off.y * dustOff.y) - new Vector2(Mathf.Cos(p.TRot), Mathf.Sin(p.TRot)) * 10f;
                    var at = c + off + v * 2f;
                    int i = PointDust(at, dustType, ((c + v * 16f - at).normalized * 2f) + v * 2f, 160, big ? 1f : 1.2f);
                    if (i >= 0) D(i).NoGravity = true;
                }
            // Smoke and motes off its tail, within ±π/16, π/8 or π/4 of straight back.
            var tl = c - new Vector2(20f, 20f);
            Vector2 Back(float spread) => RotateBy(Vector2.left, Random.Range(-spread, spread) + p.TRot);
            if (Random.Range(0, 12) == 0)
            {
                int i = BoxDust(tl, 40, 40, 31, 0f, 0f, 100);
                if (i >= 0)
                {
                    D(i).Velocity *= 0.1f;
                    D(i).Position = c + Back(Mathf.PI / 16f) * 20f + v * 2f;
                    D(i).FadeIn = 0.9f;
                }
            }
            if (Random.Range(0, 64) == 0)
            {
                int i = BoxDust(tl, 40, 40, 31, 0f, 0f, 155, 0.8f);
                if (i >= 0)
                {
                    D(i).Velocity *= 0.3f;
                    D(i).Position = c + Back(Mathf.PI / 8f) * 20f;
                    if (Random.Range(0, 2) == 0) D(i).FadeIn = 1.4f;
                }
            }
            if (Random.Range(0, 4) == 0)
                for (int k = 0; k < 2; k++)
                {
                    int[] kinds = big ? new[] { 242, 59, 88 } : new[] { 242, 73, 72, 71, 255 };
                    int i = BoxDust(tl, 40, 40, kinds[Random.Range(0, kinds.Length)], 0f, 0f, 0, 1.2f);
                    if (i < 0) continue;
                    D(i).Velocity *= 0.3f;
                    D(i).NoGravity = true;
                    D(i).Position = c + Back(Mathf.PI / 4f) * 20f;
                    if (Random.Range(0, 2) == 0) D(i).FadeIn = 1.4f;
                }
            if (!big && Random.Range(0, 12) == 0)
            {
                int i = BoxDust(tl, 40, 40, dustType, 0f, 0f, 100);
                if (i >= 0)
                {
                    D(i).Velocity *= 0.3f;
                    D(i).Position = c + Back(Mathf.PI / 16f) * 20f;
                    D(i).FadeIn = 0.9f;
                    D(i).NoGravity = true;
                }
            }
            if (big && Random.Range(0, 3) == 0)
            {
                int i = BoxDust(tl, 40, 40, dustType, 0f, 0f, 100);
                if (i >= 0)
                {
                    D(i).Velocity *= 0.3f;
                    D(i).Position = c + Back(Mathf.PI / 16f) * 20f;
                    D(i).FadeIn = 1.2f;
                    D(i).Scale = 1.5f;
                    D(i).NoGravity = true;
                }
            }
            p.Vel = Flip(v);
        }

        /// <summary>One of its four frames turned to its flight, white and half added; its light.</summary>
        static void DrawNebulaBlaze(FxBatch fx, Proj p, Vector2 px)
        {
            bool big = p.Kind == ProjKind.NebulaBlazeBig;
            fx.Light(px, big ? new Color(0.4f, 0.6f, 0.9f) : new Color(0.7f, 0.1f, 0.5f), 3f);
            int w = big ? 46 : 36, h = big ? 46 : 36;
            fx.Draw(FxBatch.Frame(p.Spec.Texture, 0, p.Frame * h, w, h, w * 0.5f, h * 0.5f), px, FxBatch.C(255, 255, 255, 127) * (1f - p.Alpha), p.TRot, p.Scale, flipX: p.Dir < 0f);
        }

        /// <summary>
        /// P:73243: a burst (Item14) over 50 px hitting everyone in it once (bar the one it just
        /// struck), nebula motes and sparks flung out and along its heading, a ring of bigger ones.
        /// </summary>
        void KillNebulaBlaze(Proj p)
        {
            bool big = p.Kind == ProjKind.NebulaBlazeBig;
            Sound("Item_14");
            Blast(p, 50f, p.Damage, p.Knockback);
            var c = CenterPx(p);
            var v = TV(p);
            var ahead = RotateBy(Vector2.right, p.TRot - Mathf.PI / 2f) * v.magnitude * (p.Extra + 1) * (big ? 0.5f : 1f);
            int[] kinds = big ? new[] { 242, 59, 88 } : new[] { 242, 73, 72, 71, 255 };
            int second = big ? 88 : 255;
            var tl = c - new Vector2(25f, 25f);
            for (int k = 0; k < 40; k++)
            {
                int i = BoxDust(tl, 50, 50, kinds[Random.Range(0, kinds.Length)], 0f, 0f, 200, big ? 3.7f : 1.7f);
                if (i >= 0)
                {
                    D(i).Position = c + RotateBy(Vector2.down, Random.Range(-Mathf.PI, Mathf.PI)) * Random.value * 25f;
                    D(i).NoGravity = true;
                    D(i).Velocity = D(i).Velocity * 3f + ahead * Random.value;
                }
                i = BoxDust(tl, 50, 50, second, 0f, 0f, 100, big ? 1.5f : 0.8f);
                if (i < 0) continue;
                D(i).Position = c + RotateBy(Vector2.down, Random.Range(-Mathf.PI, Mathf.PI)) * Random.value * 25f;
                D(i).Velocity = D(i).Velocity * 2f + ahead * Random.value;
                D(i).NoGravity = true;
                D(i).FadeIn = 1f;
                D(i).Color = new Color(220f / 255f, 20f / 255f, 60f / 255f) * 0.5f;
            }
            for (int k = 0; k < 20; k++)
            {
                int i = BoxDust(tl, 50, 50, second, 0f, 0f, 0, big ? 2.2f : 2f);
                if (i < 0) continue;
                D(i).Position = c + RotateBy(RotateBy(Vector2.right, Random.Range(-Mathf.PI, Mathf.PI)), Mathf.Atan2(v.y, v.x)) * 50f / 3f;
                D(i).NoGravity = true;
                D(i).Velocity = D(i).Velocity * 0.5f + ahead * (0.6f + 0.6f * Random.value);
            }
        }

        // ------------------------------------------------------------------ 645: Lunar Flare (AI_001)

        /// <summary>A Lunar Flare bolt, through blocks until below floorY (world).</summary>
        public void ShootLunarFlare(Vector2 from, Vector2 velocity, int damage, float knockback, float crit, float floorY)
        {
            var p = Spawn(ProjKind.LunarFlare, from, velocity, damage, knockback, crit);
            if (p == null) return;
            p.Ai1 = floorY;
        }

        /// <summary>The blast: still, 140 px across, hurting each enemy once.</summary>
        static void ExplodeLunarFlare(Proj p)
        {
            if (p.LocalAi1 != 0f) return;
            p.LocalAi1 = 1f;
            p.Ai0 = 0f;
            p.Alpha = 1f;
            p.Collide = false;
        }

        /// <summary>
        /// P:57938: an unseen bolt shedding light motes, solid once below the cursor's height,
        /// that blows up within 60 px of an enemy it can see (checked once a tick); the blast
        /// fades in over seven frames of 3 ticks and is gone.
        /// </summary>
        void StepLunarFlare(Proj p)
        {
            var c = CenterPx(p);
            int i = PointDust(c, 229, Vector2.zero);
            if (i >= 0) D(i).NoGravity = true;
            if (p.LocalAi1 != 0f)
            {
                p.Vel = Vector2.zero;
                p.HitSize = new Vector2(140f, 140f);
                p.Ai0++;
                p.Alpha = Mathf.Max(0f, p.Alpha - 10f / 255f);
                int per = (p.Extra + 1) * 3;
                if (++p.FrameCounter >= per)
                {
                    p.FrameCounter = 0;
                    p.Frame++;
                }
                if (p.Ai0 >= 7 * per) Kill(p);
                return;
            }
            if (p.Pos.y < p.Ai1) p.Collide = true;
            if (p.Updates % (p.Extra + 1) != 1) return;
            foreach (var hm in EnemiesNear(c, 60f))
            {
                var e = EnemyCenterPx(hm);
                if (Vector2.Distance(c, e) < 60f && InSight(c, e))
                {
                    ExplodeLunarFlare(p);
                    return;
                }
            }
        }

        /// <summary>GetAlpha for 645: white, half added, fading in.</summary>
        static void DrawLunarFlare(FxBatch fx, Proj p, Vector2 px)
        {
            if (p.LocalAi1 == 0f) return;
            int a = Mathf.RoundToInt(p.Alpha * 255f);
            fx.Draw(FxBatch.Frame("Projectile_645", 0, Mathf.Min(p.Frame, 6) * 98, 98, 98, 49f, 49f), px, FxBatch.C(255 - a, 255 - a, 255 - a, 127 - a / 2), 0f, p.Scale);
        }

        static void KillLunarFlare(Proj p)
        {
            var c = CenterPx(p);
            float s = p.LocalAi1 != 0f ? 140f : 10f;
            var tl = c - Vector2.one * s * 0.5f;
            for (int k = 0; k < 4; k++) BoxDust(tl, (int)s, (int)s, 31, 0f, 0f, 100, 1.5f);
            for (int k = 0; k < 4; k++)
            {
                int i = BoxDust(tl, (int)s, (int)s, 229, 0f, 0f, 0, 2.5f);
                if (i >= 0)
                {
                    D(i).NoGravity = true;
                    D(i).Velocity *= 3f;
                }
                i = BoxDust(tl, (int)s, (int)s, 229, 0f, 0f, 100, 1.5f);
                if (i >= 0)
                {
                    D(i).Velocity *= 2f;
                    D(i).NoGravity = true;
                }
            }
            TerraParticles.Instance?.NewGore(tl + new Vector2(s * Random.Range(0, 100) / 100f, s * Random.Range(0, 100) / 100f) - Vector2.one * 10f, Vector2.zero, Random.Range(61, 64), 1f, 0.3f,
                new Vector2(Random.Range(-10, 11) * 0.05f, Random.Range(-10, 11) * 0.05f));
        }

        // ------------------------------------------------------------------ 617-620: Nebula Arcanum (AI_118, AI_119, AI_029)

        /// <summary>
        /// AI_118: the sphere fades in, spinning; after 30 updates it slows 2% an update and grows
        /// to 1.3, until at 4 px an update it starts over slowly (the second phase): then a little
        /// orb (618) breaks off every 30 updates up to 240, it picks the nearest enemy it can see
        /// within 800 px once and drifts after it while within 1000, (v·7 + aim)/8 at 4 px. Gone at
        /// 600. Nebula motes swirl round it, carried along; a pink light.
        /// </summary>
        void StepNebulaArcanum(Proj p)
        {
            p.Ai0++;
            var v = TV(p);
            var c = CenterPx(p);
            bool slow = v.magnitude <= 4f;
            p.Alpha = Mathf.Max(0f, p.Alpha - 15f / 255f);
            p.TRot -= Mathf.PI / 30f;
            if (!slow)
            {
                if (Random.Range(0, 3) == 0)
                {
                    var r = RotateBy(Vector2.up, Random.Range(-Mathf.PI, Mathf.PI));
                    bool inner = Random.Range(0, 2) == 0;
                    int i = PointDust(c - r * (inner ? Random.Range(10, 21) : 30f), inner ? (Random.Range(0, 2) == 0 ? 86 : 90) : 240,
                        RotateBy(r, inner ? Mathf.PI / 2f : -Mathf.PI / 2f) * (inner ? 6f : 3f) + v, 0, 0.5f + Random.value);
                    if (i >= 0)
                    {
                        D(i).NoGravity = true;
                        D(i).FadeIn = 0.5f;
                    }
                }
                if (p.Ai0 >= 30f)
                {
                    v *= 0.98f;
                    p.Scale = Mathf.Min(1.3f, p.Scale + 0.0074468083f);
                    p.TRot -= Mathf.PI / 180f;
                }
                if (v.magnitude < 4.1f)
                {
                    v = v.normalized * 4f;
                    p.Ai0 = 0f;
                }
            }
            else
            {
                if (Random.Range(0, 2) == 0)
                    for (int k = 0; k < 2; k++)
                    {
                        var r = RotateBy(Vector2.up, Random.Range(-Mathf.PI, Mathf.PI));
                        int i = PointDust(c - r * Random.Range(10, 21), k == 0 ? 86 : 90, RotateBy(r, Mathf.PI / 2f) * 6f + v, 0, 0.9f + Random.value);
                        if (i < 0) continue;
                        D(i).NoGravity = true;
                        D(i).FadeIn = 0.5f;
                        if (k == 1) D(i).Color = new Color(220f / 255f, 20f / 255f, 60f / 255f);
                    }
                else
                {
                    var r = RotateBy(Vector2.up, Random.Range(-Mathf.PI, Mathf.PI));
                    int i = PointDust(c - r * Random.Range(20, 31), 240, RotateBy(r, -Mathf.PI / 2f) * 5f + v, 0, 0.9f + Random.value);
                    if (i >= 0)
                    {
                        D(i).NoGravity = true;
                        D(i).FadeIn = 0.5f;
                    }
                }
                if (p.Ai0 % 30f == 0f && p.Ai0 < 241f)
                {
                    var orb = Spawn(ProjKind.NebulaArcanumSub, p.Pos, Flip(RotateBy(Vector2.up, Random.Range(-Mathf.PI, Mathf.PI)) * 12f), p.Damage / 2, 0f, p.Crit);
                    if (orb != null) orb.Parent = p;
                }
                if (p.LocalAi1 == 0f)
                {
                    float best = 800f;
                    foreach (var hm in EnemiesNear(c, 800f))
                    {
                        var e = EnemyCenterPx(hm);
                        float d = Vector2.Distance(c, e);
                        if (d < best && InSight(c, e))
                        {
                            best = d;
                            p.Target = hm;
                        }
                    }
                    if (p.Target != null) p.LocalAi1 = 1f;
                }
                if (p.Target != null && Chaseable(p.Target))
                {
                    var to = EnemyCenterPx(p.Target) - c;
                    if (to.magnitude < 1000f && to.sqrMagnitude > 1e-6f) v = (v * 7f + to.normalized * 4f) / 8f;
                }
            }
            p.Vel = Flip(v);
            if (p.Ai0 >= 600f) Kill(p);
        }

        /// <summary>
        /// Main.DrawProj for 617: three turning layers of Extra_50 (a darker one pulsing with the
        /// spin, one against it, one with it, smaller) round the sphere's own sprite turning slower
        /// against them, half clear, and the sprite again added on top; all fading in, unlit.
        /// </summary>
        static void DrawNebulaArcanum(FxBatch fx, Proj p, Vector2 px)
        {
            float a = 1f - p.Alpha;
            if (a <= 0f) return;
            if (p.Alpha < 150f / 255f) fx.Light(px, new Color(0.7f, 0.2f, 0.6f), 3f);
            var c87 = new Color(a, a, a, a);
            var c96 = c87 * 0.8f;
            c96.a *= 0.5f;
            float pulse = 0.95f + Mathf.Sin(p.TRot * 0.75f) * 0.1f;
            var c97 = new Color(a * 0.5f, a * 0.5f, a * 0.5f, a) * pulse;
            var ring = Sprite("Extra_50");
            var orb = Sprite("Projectile_617");
            fx.Draw(ring, px, c97, -p.TRot + 0.35f, 0.6f + p.Scale * 0.6f * pulse, flipX: true);
            fx.Draw(ring, px, c87, -p.TRot, p.Scale, flipX: true);
            fx.Draw(orb, px, c96, -p.TRot * 0.7f, p.Scale, flipX: true);
            fx.Draw(ring, px, c87 * 0.8f, p.TRot * 0.5f, p.Scale * 0.9f);
            fx.Draw(orb, px, new Color(a, a, a, 0f), p.TRot, p.Scale);
        }

        /// <summary>
        /// P:73553: a 176 px burst (Item14) hurting all in it once, nebula and pink motes, smoke;
        /// its orbs go with it, and 10-16 shards (620) fly out of it, mostly upwards, half of one
        /// colour fast, half of the other slower, at 65% of its damage.
        /// </summary>
        void KillNebulaArcanum(Proj p)
        {
            Blast(p, 176f, p.Damage, p.Knockback);
            Sound("Item_14");
            var c = CenterPx(p);
            var tl = c - new Vector2(88f, 88f);
            float heading = Mathf.Atan2(TV(p).y, TV(p).x);
            Vector2 InDisc() => c + RotateBy(Vector2.down, Random.Range(-Mathf.PI, Mathf.PI)) * Random.value * 88f;
            Vector2 OnRing() => c + RotateBy(RotateBy(Vector2.right, Random.Range(-Mathf.PI, Mathf.PI)), heading) * 88f;
            for (int k = 0; k < 4; k++)
            {
                int i = BoxDust(tl, 176, 176, 240, 0f, 0f, 100, 1.5f);
                if (i >= 0) D(i).Position = InDisc();
            }
            for (int k = 0; k < 30; k++)
            {
                int i = BoxDust(tl, 176, 176, 62, 0f, 0f, 200, 3.7f);
                if (i >= 0)
                {
                    D(i).Position = InDisc();
                    D(i).NoGravity = true;
                    D(i).Velocity *= 3f;
                }
                i = BoxDust(tl, 176, 176, 90, 0f, 0f, 100, 1.5f);
                if (i < 0) continue;
                D(i).Position = InDisc();
                D(i).Velocity *= 2f;
                D(i).NoGravity = true;
                D(i).FadeIn = 1f;
                D(i).Color = new Color(220f / 255f, 20f / 255f, 60f / 255f) * 0.5f;
            }
            for (int k = 0; k < 20; k++)
            {
                int i = BoxDust(tl, 176, 176, k < 10 ? 62 : 240, 0f, 0f, 0, k < 10 ? 2.7f : 1.5f);
                if (i < 0) continue;
                D(i).Position = OnRing();
                D(i).NoGravity = true;
                D(i).Velocity *= 3f;
            }
            for (int k = 0; k < 2; k++)
                TerraParticles.Instance?.NewGore(InDisc(), Vector2.zero, Random.Range(61, 64), 1f, 0.3f, new Vector2(Random.Range(-10, 11) * 0.05f, Random.Range(-10, 11) * 0.05f));
            foreach (var q in live)
                if (!q.Dead && q.Kind == ProjKind.NebulaArcanumSub && q.Parent == p) Kill(q);
            int fast = Random.Range(5, 9), slow = Random.Range(5, 9);
            int first = Random.Range(0, 2) == 0 ? 86 : 90, second = first == 86 ? 90 : 86;
            for (int k = 0; k < fast + slow; k++)
            {
                bool f = k < fast;
                var at = c + new Vector2(Random.Range(-30f, 30f), Random.Range(-30f, 30f));
                var d = Vector2.zero;
                while (d == Vector2.zero) d = new Vector2(Random.Range(-100, 101), Random.Range(-100, 101));
                d.Normalize();
                if (d.y > (f ? 0.2f : 0.4f)) d.y = -d.y;
                d *= (f ? Random.Range(70, 101) : Random.Range(40, 81)) * 0.1f;
                var shard = Spawn(ProjKind.NebulaArcanumShard, TerraParticles.ToWorld(at), Flip(d), (int)(p.Damage * 0.65), p.Knockback * 0.8f, p.Crit);
                if (shard != null) shard.Ai0 = f ? first : second;
            }
        }

        /// <summary>AI_119 for 618: carried along by its sphere and pulled back towards it (0.15 a side, at most 6 px an update); unseen but for its motes; gone with it.</summary>
        void StepNebulaArcanumSub(Proj p)
        {
            var parent = p.Parent;
            if (parent == null || parent.Dead)
            {
                Kill(p);
                return;
            }
            p.Pos += parent.Vel * Px;
            var v = TV(p);
            var to = CenterPx(parent) - CenterPx(p);
            v += new Vector2(Mathf.Sign(to.x), Mathf.Sign(to.y)) * 0.15f;
            if (v.magnitude > 6f) v *= 6f / v.magnitude;
            p.Vel = Flip(v);
            if (Random.Range(0, 2) == 0)
            {
                int i = PointDust(CenterPx(p), 86, v + TV(parent), 0, 1.5f);
                if (i >= 0) D(i).NoGravity = true;
            }
        }

        /// <summary>AI_029 for 620: it falls (0.2, drag 2%) for 40 updates, shedding motes of its colour that shrink as it goes; it bounces off blocks.</summary>
        void StepNebulaArcanumShard(Proj p)
        {
            p.Ai1++;
            float left = (60f - p.Ai1) / 60f;
            if (p.Ai1 > 40f)
            {
                Kill(p);
                return;
            }
            var v = TV(p);
            v.y = Mathf.Min(v.y + 0.2f, 18f);
            v.x *= 0.98f;
            p.Vel = Flip(v);
            var c = CenterPx(p);
            var tl = c - new Vector2(4f, 4f);
            int type = (int)p.Ai0;
            for (int k = 0; k < 2; k++)
            {
                int i = BoxDust(tl, 8, 8, type, v.x, v.y, 50, 1.1f);
                if (i < 0) continue;
                D(i).Position = (D(i).Position + c) / 2f;
                D(i).NoGravity = true;
                D(i).Velocity *= 0.3f;
                D(i).Scale *= left;
            }
            int j = BoxDust(tl, 8, 8, type, v.x, v.y, 50, 0.6f);
            if (j < 0) return;
            D(j).Position = (D(j).Position + c * 5f) / 6f;
            D(j).Velocity *= 0.1f;
            D(j).NoGravity = true;
            D(j).FadeIn = 0.9f * left;
            D(j).Scale *= left;
        }

        /// <summary>P:73678: ten motes of its colour, a third of them small and bright (the next dust type), thrown back against its flight.</summary>
        static void KillNebulaShard(Proj p, int type, float fade, float small, float big, float speed)
        {
            var c = CenterPx(p);
            var v = TV(p);
            int w = (int)(p.Spec.W * p.Scale), h = (int)(p.Spec.H * p.Scale);
            for (int k = 0; k < 10; k++)
            {
                int i = BoxDust(c - new Vector2(w, h) * 0.5f, w, h, type, v.x * 0.1f, v.y * 0.1f, 0, 0.5f);
                if (i < 0) continue;
                if (Random.Range(0, 3) == 0)
                {
                    D(i).FadeIn = fade + Random.Range(-10, 11) * 0.01f;
                    D(i).Scale = small + Random.Range(-10, 11) * 0.005f;
                    D(i).Type++;
                }
                else D(i).Scale = big + Random.Range(-10, 11) * 0.01f;
                D(i).NoGravity = true;
                D(i).Velocity = D(i).Velocity * speed - v * 0.1f;
            }
        }
    }
}
