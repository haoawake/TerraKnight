using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The 1.4.5 minions' shots (Terraria's space, px y down, per update): the Mushroom Boi's burst
    /// (1044, AI_117), the Foxsparks' fireballs (1097: the Trusty one's soul fire bursting into
    /// eight) and its breath when held (1106, AI_193, the Flamethrower's flame its own way), and the
    /// Forbidden relic's sand gem (1120, AI_029, slowing like the Emerald Staff's).
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        public static int DevMushroomBlasts, DevFoxFireballs, DevSoulFireBursts, DevFoxFlames, DevForbiddenGems;

        static void RegisterPal()
        {
            // P:9877-9888, 35095-35140: the burst (unseen), 104 px (52 at its scale of 2), its blow once; gone after 3 updates.
            specs[ProjKind.MushroomBlast] = new Spec
            {
                Ai = Ai.Custom, W = 8, H = 8, Time = 60, Penetrate = -1, TileCollide = false, OncePerNpc = true, OwnMove = true, Look = Look.Invisible,
                Update = (t, p) =>
                {
                    p.Ai1 += 0.01f;
                    p.Scale = p.Ai1;
                    p.HitSize = new Vector2((int)(52f * p.Scale), (int)(52f * p.Scale));
                    if (++p.Ai0 >= 3f) t.Kill(p);
                },
                Draw = (t, fx, p, px) => { }, OnKill = (t, p) => { },
            };
            // P:10234-10240: the Foxsparks' fireball (through blocks while rising, one more update a tick); P:11315 on fire, the Trusty one's hellfire.
            specs[ProjKind.FoxFireball] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_1097", W = 10, H = 10, Time = 300, Extra = 1, Penetrate = 1, AlphaStart = 1f, TrailLength = 20,
                Update = (t, p) => t.StepFoxFireball(p), Draw = (t, fx, p, px) => DrawFoxFireball(fx, p, px), OnKill = (t, p) => t.KillFoxFireball(p),
                OnHit = (t, p, hm) => EnemyDebuffs.Add(hm, p.Ai1 > 0f ? EnemyDebuffs.Hellfire : EnemyDebuffs.OnFire, Random.Range(300, 600)),
            };
            // P:10305-10314: the Foxsparks' breath (the Flamethrower's flame, through four, each 10 ticks shared); 15% weaker a hit; hellfire when fast (P:11665).
            specs[ProjKind.FoxFlame] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_1106", W = 6, H = 6, Time = 3600, Extra = 2, Penetrate = 4, TypeCooldown = 10, AlphaStart = 1f, Frames = 7,
                CanDamage = p => p.LocalAi0 < 54f, Update = (t, p) => t.StepFlame(p), Draw = (t, fx, p, px) => DrawFoxFlame(fx, p, px), OnKill = (t, p) => { },
                OnHit = (t, p, hm) =>
                {
                    EnemyDebuffs.Add(hm, p.Vel.magnitude > 10f ? EnemyDebuffs.Hellfire : EnemyDebuffs.OnFire, 300);
                    p.Damage = (int)(p.Damage * 0.85f);
                },
                TileHit = (t, p, hx, hy, before) =>
                {
                    p.Vel = before * 0.95f;
                    p.Pos -= p.Vel * Px;
                },
            };
            // P:10481-10489: the relic's sand gem, 75 updates, one more a tick, each enemy every 60; sand (170) every update; its trail orange.
            specs[ProjKind.ForbiddenGem] = new Spec
            {
                Ai = Ai.GemBolt, Texture = "Projectile_1120", Time = 75, Extra = 1, Dust = 170, LocalCooldown = 60, Light = new Color(0.9f, 0.6f, 0.2f),
                Update = (t, p) => StartForbiddenGem(p), OnKill = (t, p) => KillForbiddenGem(p),
            };
        }

        Proj SpawnMinionShot(ProjKind kind, Vector2 fromPx, Vector2 velPx, int damage, float knockback)
        {
            var p = Spawn(kind, TerraParticles.ToWorld(fromPx), Flip(velPx), damage, knockback, 0f);
            if (p != null) p.MinionShot = true;
            return p;
        }

        // ------------------------------------------------------------------ 1044: the Mushroom Boi's burst

        /// <summary>P:50605-50624, 35275-35282: a burst where it lands on its prey, 33% harder and three times the knockback, and its red flash.</summary>
        public void ShootMushroomBlast(Vector2 atPx, int damage, float knockback)
        {
            var p = SpawnMinionShot(ProjKind.MushroomBlast, atPx, Vector2.zero, (int)(damage * 1.33f), knockback * 3f);
            if (p == null) return;
            p.Ai1 = 2f;
            DevMushroomBlasts++;
            FxParticles.MushroomExplosion(atPx);
        }

        // ------------------------------------------------------------------ 1097: the Foxsparks' fireball

        /// <summary>P:62700-62740: a fireball at 11 px; ai1 3 for the Trusty Foxsparks' hellfire, 1 its soul fire (every third).</summary>
        public void ShootFoxFireball(Vector2 fromPx, Vector2 velPx, int damage, float knockback, float kind)
        {
            var p = SpawnMinionShot(ProjKind.FoxFireball, fromPx, velPx, damage, knockback);
            if (p == null) return;
            p.Ai1 = kind;
            DevFoxFireballs++;
        }

        /// <summary>
        /// AI_001 for 1097 (P:57517-57580, 58256-58270): a roar and a flash as it starts (not the
        /// soul fire's sparks); fading in from its third update; falling 0.15 after 40, through blocks
        /// while rising; the soul fire bursts at 30; fire (6), smoke (30) and a red spark off its
        /// trail; shrinking away 0.01 an update.
        /// </summary>
        void StepFoxFireball(Proj p)
        {
            var c = CenterPx(p);
            var tl = c - new Vector2(5f, 5f);
            var v = TV(p);
            p.Ai0++;
            if (p.LocalAi0 == 0f)
            {
                p.LocalAi0 = 1f;
                if (p.Ai1 != 2f)
                {
                    Sound("Item_20");
                    Sound("Custom/dd2_betsy_fireball_shot_*");
                    for (int k = 0; k < 10; k++)
                    {
                        int i = BoxDust(tl, 10, 10, 6, 0f, 0f, 200);
                        if (i >= 0)
                        {
                            D(i).Scale *= 0.7f;
                            D(i).Velocity = D(i).Velocity * 0.5f + v * Random.value * 0.6f;
                        }
                        i = BoxDust(tl, 10, 10, 6, 0f, 0f, 50);
                        if (i >= 0)
                        {
                            D(i).Scale *= 1.25f;
                            D(i).Velocity += v * 0.45f * Random.value;
                        }
                    }
                }
            }
            if (p.Ai0 >= 2f) p.Alpha = Mathf.Max(0f, p.Alpha - 25f / 255f);
            if (Random.Range(0, 4) == 0)
            {
                int i = BoxDust(tl, 10, 10, 6, 0f, 0f, 200);
                if (i >= 0)
                {
                    D(i).Scale *= 1.3f;
                    D(i).Velocity += v * 0.4f;
                }
            }
            if (Random.Range(0, 2) == 0)
            {
                int i = BoxDust(tl, 10, 10, 30, 0f, 0f, 100);
                if (i >= 0)
                {
                    D(i).Scale *= 0.7f;
                    D(i).Velocity += v;
                }
            }
            if (Random.Range(0, 3) == 0 && p.Trail.Count > 9)
            {
                int i = BoxDust(TerraParticles.ToPx(p.Trail[9]) - new Vector2(5f, 5f), 10, 10, 55, 0f, 0f, 50);
                if (i >= 0)
                {
                    D(i).Scale *= 0.85f;
                    D(i).Velocity += v * 0.85f;
                    D(i).Color = FxBatch.C(255, 60, 20);
                }
            }
            if (p.Ai1 == 2f && p.Scale > 0.7f) p.Scale = 0.7f;
            p.Scale -= 0.01f;
            if (p.Scale < 0.3f)
            {
                Kill(p);
                return;
            }
            if (p.Ai0 >= 40f)
            {
                p.Ai0 = 40f;
                v.y += 0.15f;
                p.Collide = true;
            }
            else p.Collide = v.y > 0f;
            p.Vel = Flip(v);
            p.TRot = ToRot(v) + Mathf.PI / 2f;
            if (p.Ai1 == 1f && p.Ai0 >= 30f) Kill(p);
        }

        /// <summary>
        /// M:30687-30690, 30862-30868, 31278-31292, 31608-31611: like Betsy's fire, its copies from
        /// its own half-added white through orange-red to red (the soul fire's darkening to nothing,
        /// a quarter bigger); unseen on its first update.
        /// </summary>
        static void DrawFoxFireball(FxBatch fx, Proj p, Vector2 px)
        {
            var spr = FxBatch.Frame("Projectile_1097", 0, 0, 34, 90, 17f, 20f);
            if (spr == null) return;
            float o = 1f - p.Alpha;
            var self = new Color(o, o, o, 0.5f * o);
            var orangeRed = new Color(1f, 69f / 255f, 0f, 1f);
            bool soul = p.Ai1 == 1f;
            float shrinkTo = soul ? 0.625f : 0.5f;
            for (int i = 19; i > 0; i -= 3)
            {
                if (i >= p.Trail.Count) continue;
                float t = i / 19f;
                var col = t < 0.5f ? Color.Lerp(self, orangeRed, t / 0.5f) : Color.Lerp(orangeRed, Color.red, (t - 0.5f) / 0.5f);
                if (soul)
                {
                    col = Color.Lerp(col, Color.red, Mathf.Clamp01(t * 3f));
                    col = Color.Lerp(col, Color.black, Mathf.InverseLerp(0.33f, 0.66f, t));
                    col = Color.Lerp(col, Color.clear, Mathf.InverseLerp(0.66f, 1f, t));
                }
                fx.Draw(spr, TerraParticles.ToPx(p.Trail[i]), col * ((19 - i) / 30f), p.TRot, Mathf.LerpUnclamped(p.Scale, shrinkTo, i / 15f));
            }
            if (p.Ai0 > 0f) fx.Draw(spr, px, self, p.TRot, p.Scale);
            fx.Light(px, new Color(1f, 0.5f, 0.2f) * 0.6f * o, 2.5f);
        }

        /// <summary>P:75780-75795: Item20 and fire; the soul fire bursts into eight small ones (half as hard) flung its way.</summary>
        void KillFoxFireball(Proj p)
        {
            Sound("Item_20");
            var c = CenterPx(p);
            for (int k = 0; k < 6; k++) BoxDust(c - new Vector2(5f, 5f), 10, 10, 6);
            if (p.Ai1 != 1f) return;
            DevSoulFireBursts++;
            var v = TV(p);
            for (int k = 0; k < 8; k++)
            {
                var go = RotateBy(v, (Random.value * 2f - 1f) * Mathf.PI * 2f * 0.03f) + Random.insideUnitCircle * 3f;
                var child = SpawnMinionShot(ProjKind.FoxFireball, c, go, p.Damage / 2, p.Knockback / 2f);
                if (child != null) child.Ai1 = 2f;
            }
        }

        // ------------------------------------------------------------------ 1106: the Foxsparks' breath

        /// <summary>P:60065-60085: a breath of fire from the held Foxsparks along its aim, 5 px a tick (the Trusty one's 10.5).</summary>
        public void ShootFoxFlame(Vector2 fromPx, Vector2 velPx, int damage, float knockback)
        {
            var p = SpawnMinionShot(ProjKind.FoxFlame, fromPx, velPx, damage, knockback);
            if (p == null) return;
            DevFoxFlames++;
            // P:42102-42112: one breath in three crackles (FoxparksFlame, Item34 quietly).
            if (Random.Range(0, 3) == 0) Sound("Item_34", 0.15f);
        }

        /// <summary>
        /// DrawProj_Flamethrower_Foxsparks (M:35767-35842): the Flamethrower's spinning rings at half
        /// their size, but blue to cyan at first, drawn back toward the player (less so up close) and
        /// rising 46 px as they age; a fast breath's rings closer, half again bigger.
        /// </summary>
        static void DrawFoxFlame(FxBatch fx, Proj p, Vector2 px)
        {
            var whole = FxBatch.Whole(p.Spec.Texture);
            if (whole == null) return;
            const float life = 72f;
            float t = p.LocalAi0, age = Mathf.Clamp01(t / life);
            if (age >= 1f) return;
            int tw = (int)whole.rect.width, fh = (int)whole.rect.height / 7;
            var spr = FxBatch.Frame(p.Spec.Texture, 0, fh * 3, tw, fh, tw / 2f, fh / 2f);
            Color C(int r, int g, int b, int a) => FxBatch.C(r, g, b, a);
            Color c1 = C(95, 120, 255, 200), c4 = C(50, 180, 255, 70);
            Color c2 = Color.Lerp(C(255, 80, 20, 100), C(255, 255, 20, 70), 0.25f), c3 = C(80, 80, 80, 100);
            bool fast = p.Vel.magnitude > 10f;
            float step = (t > 50f ? 0.175f : 0.2f) * 0.7f;
            if (fast) step *= 0.5f;
            float fade = 1f - Mathf.Clamp01(Mathf.InverseLerp(60f, 72f, t));
            float back = Mathf.Min(t, 20f);
            float grow = Mathf.Lerp(0.25f, 1f, Mathf.Clamp01((age - 0.2f) / 0.3f)) * 0.5f * (fast ? 1.5f : 1f);
            Color tint = age < 0.1f ? Color.Lerp(Color.clear, c1, age / 0.1f) : age < 0.2f ? Color.Lerp(c1, c4, (age - 0.1f) / 0.1f) : age < 0.35f ? c4
                : age < 0.7f ? Color.Lerp(c4, c2, (age - 0.35f) / 0.35f) : age < 0.85f ? Color.Lerp(c2, c3, (age - 0.7f) / 0.15f) : Color.Lerp(c3, Color.clear, (age - 0.85f) / 0.15f);
            var v = TV(p);
            Holder(out var player);
            float nearPlayer = Mathf.Min(1f, Vector2.Distance(px, player) / 120f);
            var toPlayer = (player + v * (fast ? 3f : 6f) - px);
            var backDir = (toPlayer.sqrMagnitude > 1e-6f ? toPlayer.normalized : Vector2.zero) * -v.magnitude;
            float rise = Mathf.Pow(Mathf.Clamp01(Mathf.InverseLerp(20f, life, t)), 3f);
            float T = Time.time;
            for (int layer = 0; layer < 2; layer++)
                for (float k = 1f; k >= 0f; k -= step)
                {
                    float near = (1f - k) * Mathf.Clamp01(age / 0.2f);
                    var at = px + backDir * -back * k * nearPlayer + new Vector2(0f, -46f) * rise;
                    var col = tint * near;
                    var col6 = col;
                    col6.g /= 2f;
                    col6.b /= 2f;
                    col6.a = Mathf.Min(col.a + 80f / 255f * near, 1f);
                    float spinRate = 1f / step * (k + 1f);
                    float r1 = p.TRot + k * Mathf.PI / 2f + T * spinRate * 2f, r2 = p.TRot - k * Mathf.PI / 2f - T * spinRate * 2f;
                    if (layer == 0)
                    {
                        fx.Draw(spr, at - v * back * step * 0.5f, col6 * fade * 0.25f, r1 + Mathf.PI / 4f, grow);
                        fx.Draw(spr, at, col6 * fade, r2, grow);
                    }
                    else
                    {
                        fx.Draw(spr, at - v * back * step * 0.2f, col * fade * 0.25f, r1 + Mathf.PI / 2f, grow * 0.75f);
                        fx.Draw(spr, at, col * fade, r2 + Mathf.PI / 2f, grow * 0.75f);
                    }
                }
        }

        // ------------------------------------------------------------------ 1120: the Forbidden relic's gem

        /// <summary>AI_206 (P:38265-38284): the relic's gem at 32 px a tick toward its prey, slowing as the Emerald Staff's.</summary>
        public void ShootForbiddenGem(Vector2 fromPx, Vector2 velPx, int damage, float knockback)
        {
            if (SpawnMinionShot(ProjKind.ForbiddenGem, fromPx, velPx, damage, knockback) != null) DevForbiddenGems++;
        }

        /// <summary>P:38545-38560: as it starts, Item72 and eight sand motes flung out round it.</summary>
        static void StartForbiddenGem(Proj p)
        {
            if (p.SoundDelay != 0) return;
            p.SoundDelay = -1;
            Sound("Item_72");
            var c = CenterPx(p);
            var v = TV(p);
            for (float a = 0f; a < Mathf.PI * 4f; a += Mathf.PI / 2f)
            {
                int i = BoxDust(c - new Vector2(5f, 5f), 10, 10, 170, v.x, v.y, 50, 1.2f);
                if (i < 0) continue;
                D(i).NoGravity = true;
                D(i).Velocity = new Vector2(Mathf.Cos(Mathf.PI / 4f + a), Mathf.Sin(Mathf.PI / 4f + a)) * Random.value * 3f + v * 0.3f * Random.value;
            }
        }

        /// <summary>P:76926-76950: a dig and sand flung on along its way.</summary>
        static void KillForbiddenGem(Proj p)
        {
            Sound("Dig_" + Random.Range(0, 3));
            var c = CenterPx(p);
            var v = TV(p);
            var along = v.magnitude < 1f ? Vector2.zero : v.normalized;
            for (int k = 0; k < 15; k++)
            {
                int i = BoxDust(c - new Vector2(5f, 5f), 10, 10, 170, 0f, 0f, 50, 1.2f);
                if (i < 0) continue;
                D(i).NoGravity = true;
                D(i).Velocity = (along * 2.2f + Random.insideUnitCircle * 1.5f) * Random.value;
            }
        }
    }
}
