using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Sweep R5d, the 1.4.5 spells (Terraria's space, px y down, per update): Weather Pain's storm
    /// cloud (969, AI_177) drifting after enemies, the Wand of Frosting's frost (979, the Wand of
    /// Sparking's spark in ice), the Pretty Mirror thrown to break (1085), the Daybloom Staff's
    /// motes (1114, AI_024), the Glacier Fang's icicle charged in hand then loosed (1115, AI_208)
    /// and the Petalstorm's petals (1116, AI_196, the Flower whip's petals' kind).
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        public static int DevGlacierReleases;

        static void RegisterMagic8()
        {
            // P:9171-9182: the storm cloud (56×64, through fifteen, 25 tick shared immunity, AP 10; an 18×32 box against blocks); 8 frames of 66.
            specs[ProjKind.WeatherPainCloud] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_969", W = 56, H = 64, Time = 3600, Penetrate = 15, TypeCooldown = 25, ArmorPen = 10, TileBox = new Vector2(18f, 32f),
                Update = (t, p) => t.StepWeatherPain(p), Draw = (t, fx, p, px) => DrawWeatherPain(fx, p, px), OnKill = (t, p) => { },
                // P:18615-18624: off blocks at three quarters, unless barely moving.
                TileHit = (t, p, hx, hy, ov) =>
                {
                    if (hx) p.Vel.x = Mathf.Abs(ov.x) > 1f ? -ov.x * 0.75f : 0f;
                    if (hy) p.Vel.y = Mathf.Abs(ov.y) > 1f ? -ov.y * 0.75f : 0f;
                },
            };
            // P:9227-9236: the frost (unseen, through two), the Wand of Sparking's spark in ice; P:11424 frostburn half the time.
            specs[ProjKind.FrostSpark] = new Spec
            {
                Ai = Ai.Spark, Look = Look.Invisible, Penetrate = 2, Time = 3600, Dust = 135, OnKill = (t, p) => { },
                OnHit = (t, p, hm) =>
                {
                    if (Random.Range(0, 2) == 0) EnemyDebuffs.Add(hm, EnemyDebuffs.Frostburn, Random.Range(0, 3) == 0 ? Random.Range(60, 240) : Random.Range(60, 120));
                },
            };
            // P:10127-10133: the Pretty Mirror, thrown (AI_002), spinning fast.
            specs[ProjKind.PrettyMirrorProj] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_1085", W = 18, H = 18, Time = 3600, Penetrate = 1,
                Update = (t, p) =>
                {
                    float before = p.TRot;
                    StepThrowAi2(p);
                    p.TRot = before + (p.Vel.x < 0f ? -1f : 1f) * 0.4f;
                },
                Draw = (t, fx, p, px) => fx.Draw(FxBatch.Frame("Projectile_1085", 0, 0, 26, 30, 13f, 26f), px, new Color(L, L, L, 1f), p.TRot, p.Scale),
                OnKill = (t, p) => KillPrettyMirror(p),
            };
            // P:10391-10402, 26319-26346: the Daybloom's mote (8 px, 1.2 big), drifting to a stop; dies on blocks.
            specs[ProjKind.DaybloomMote] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_1114", W = 8, H = 8, Scale = 1.2f, Time = 600, Penetrate = 1, AlphaStart = 50f / 255f, TrailLength = 10,
                Update = (t, p) => t.StepDaybloom(p), Draw = (t, fx, p, px) => DrawDaybloom(fx, p, px), OnKill = (t, p) => { },
            };
            // P:10403-10414: the Glacier Fang's icicle (each enemy once), only hurting once loosed (P:15482); 3 frames of 70; 34% weaker a hit (P:13468).
            specs[ProjKind.GlacierFangShot] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_1115", W = 10, H = 10, Time = 300, Penetrate = 1, OncePerNpc = true, OwnMove = true, Frames = 3,
                CanDamage = p => p.Ai1 == 1f, Held = true, HoldsWhile = p => p.Ai1 == 0f,
                HitArea = p =>
                {
                    float grow = Mathf.Lerp(0f, 22f, p.Ai0 / 60f) * 2f;
                    return (p.Pos, new Vector2(10f + grow, 10f + grow) * Px);
                },
                OnHit = (t, p, hm) => p.Damage = (int)(p.Damage * 0.66f),
                Update = (t, p) => t.StepGlacierFang(p), Draw = (t, fx, p, px) => DrawGlacierFang(fx, p, px), OnKill = (t, p) => KillGlacierFang(p),
            };
            // P:10415-10426: the Petalstorm's petal (18 px, 120 ticks, each enemy once), knocking from the player (P:13072); off blocks at a quarter (P:18180).
            specs[ProjKind.PetalstormPetal] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_1116", W = 18, H = 18, Time = 120, Penetrate = 1, OncePerNpc = true, PushFromPlayer = true, TileBox = new Vector2(6f, 6f),
                Update = (t, p) => StepFlowerPetal(p), Draw = (t, fx, p, px) => DrawFlowerPetal(fx, p, px), OnKill = (t, p) => KillPetalstorm(p),
                TileHit = (t, p, hx, hy, ov) =>
                {
                    if (hx) p.Vel.x = -ov.x * 0.25f;
                    if (hy) p.Vel.y = -ov.y * 0.25f;
                },
            };
        }

        // ------------------------------------------------------------------ 969: Weather Pain (AI_177)

        /// <summary>
        /// AI_177 for 969 (P:45957-46085): its howl as it starts; 570 updates (fading over the last
        /// 10), only the newest kept (the older fade from 555); it closes on the nearest enemy it can
        /// see within 400 px (8 px a tick, keeping 25 off), else keeps its heading; tilting with its
        /// speed, 8 frames of 5 ticks, a drizzle of cloud (16) every third tick.
        /// </summary>
        void StepWeatherPain(Proj p)
        {
            if (p.SoundDelay == 0)
            {
                p.SoundDelay = -1;
                Sound("Custom/dd2_book_staff_twister_loop");
            }
            p.Ai1++;
            if (p.Ai1 > 560f) p.Alpha = Mathf.Lerp(0f, 250f, (p.Ai1 - 560f) / 10f) / 255f;
            if (p.Ai1 >= 570f)
            {
                Kill(p);
                return;
            }
            foreach (var q in live)
                if (q != p && !q.Dead && q.Kind == ProjKind.WeatherPainCloud && p.TimeLeft > q.TimeLeft && q.Ai1 < 555f) q.Ai1 = 555f;
            var c = CenterPx(p);
            var v = TV(p);
            if (p.Target != null && (!Chaseable(p.Target) || p.Target.isDead)) p.Target = null;
            if (p.Target == null)
            {
                float best = 400f;
                foreach (var hm in EnemiesNear(c, 400f))
                {
                    var e = EnemyCenterPx(hm);
                    float d = Vector2.Distance(c, e);
                    if (d >= best || !TerraWorld.LineClearPx(c, e)) continue;
                    best = d;
                    p.Target = hm;
                }
            }
            float speed = 8f;
            if (p.Target != null)
            {
                var e = EnemyCenterPx(p.Target);
                float d = Vector2.Distance(c, e);
                if (speed > d) speed = d;
                if (d >= 25f && d > 1e-4f) v = Vector2.Lerp(v, (e - c) / d * speed, 0.075f);
            }
            else if (v.sqrMagnitude > 1e-6f) v = Vector2.Lerp(v, v.normalized * speed, 0.075f);
            p.Vel = Flip(v);
            p.TRot = v.x * 0.0125f;
            if (++p.FrameCounter > 4)
            {
                p.FrameCounter = 0;
                if (++p.Frame >= 8) p.Frame = 0;
            }
            if (p.TimeLeft % 3 != 0) return;
            var tl = c - new Vector2(28f, 32f);
            int i = BoxDust(tl, 56, 64, 16, v.x, v.y, 120, 0.5f);
            if (i >= 0)
            {
                D(i).NoGravity = true;
                D(i).FadeIn = 0.9f;
                D(i).Velocity = Random.insideUnitCircle * 2f + new Vector2(0f, -2f) + v * 0.75f;
            }
            for (int k = 0; k < 2; k++)
            {
                int j = BoxDust(tl, 56, 64, 16, v.x, v.y, 60, 0.5f);
                if (j < 0) continue;
                D(j).NoGravity = true;
                D(j).FadeIn = 0.7f;
                D(j).Velocity = Random.insideUnitCircle * 2f * 0.2f + new Vector2(0f, -0.4f) + v * 1.5f;
                D(j).Position -= v * 3f;
            }
        }

        /// <summary>M:33741ff, P:80693-80707: its frame about its middle, lit but never brighter than 210 grey.</summary>
        static void DrawWeatherPain(FxBatch fx, Proj p, Vector2 px)
        {
            float l = Mathf.Min(L, 210f / 255f), o = 1f - p.Alpha;
            fx.Draw(FxBatch.Frame("Projectile_969", 0, 66 * p.Frame, 62, 66, 31f, 33f), px, new Color(l, l, l, 1f) * o, p.TRot, p.Scale);
        }

        // ------------------------------------------------------------------ 1085: the Pretty Mirror

        /// <summary>P:72034-72052: Item106, glass (330) and four shards of the frame flung back where it broke.</summary>
        static void KillPrettyMirror(Proj p)
        {
            Sound("Item_106");
            var back = TV(p) * 0.2f;
            var c = CenterPx(p) - back;
            for (int k = 0; k < 10; k++)
            {
                int i = BoxDust(c - new Vector2(9f, 9f), 18, 18, 330);
                if (i >= 0) D(i).Velocity -= back;
            }
            for (int g = 1394; g <= 1397; g++) TerraParticles.Instance?.NewGore(c, -back, g, exact: true, falls: true);
        }

        // ------------------------------------------------------------------ 1114: Daybloom Staff (AI_024)

        /// <summary>AI_024 for 1114 (P:26293-26346): spinning with its speed, now and then a sparkle (57), slowing 1.5% an update; shrinking away after 130.</summary>
        void StepDaybloom(Proj p)
        {
            var v = TV(p);
            p.TRot += v.x * 0.2f;
            p.Ai1++;
            if (Random.Range(0, 4) == 0)
            {
                var c = CenterPx(p);
                int i = BoxDust(c - new Vector2(4.8f, 4.8f), 10, 10, 57);
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

        /// <summary>M:35347-35360, P:81008: ten fading copies at its old places once 6 updates old, then itself, 200 grey nearly added; a warm light by its size.</summary>
        static void DrawDaybloom(FxBatch fx, Proj p, Vector2 px)
        {
            var spr = FxBatch.Frame("Projectile_1114", 0, 0, 6, 6, 3f, 4f);
            var col = FxBatch.C(200, 200, 200, 25);
            if (p.Ai1 > 6f)
                for (int i = 0; i < 10 && i < p.Trail.Count; i++)
                {
                    float k = (9 - i) / 9f;
                    fx.Draw(spr, TerraParticles.ToPx(p.Trail[i]) + new Vector2(-1f, 0f), col * k, p.TRot, p.Scale * k);
                }
            fx.Draw(spr, px + new Vector2(-1f, 0f), col, p.TRot, p.Scale);
            float l = p.Scale * 0.5f;
            fx.Light(px, new Color(0.6f * l, 0.45f * l, 0.2f * l), 2.5f);
        }

        // ------------------------------------------------------------------ 1115: Glacier Fang (AI_208)

        /// <summary>
        /// AI_208 (P:37577-37720). Held while the button is: out along the aim (34 px, a little more
        /// as it charges), re-aimed every 3 ticks at 4-15 px; every 20 ticks a chime (Item210) paying
        /// the mana again (to 60); loosed at 60 or on letting go (Item201): truer, up to four times as
        /// hard and as heavy and through up to three as charged, falling less; then a frost trail (197).
        /// </summary>
        void StepGlacierFang(Proj p)
        {
            var mod = TerraMod.Instance;
            var def = ItemDb.Get(ItemType.GlacierFang);
            float before = p.Ai0;
            var v = TV(p);
            // At 60 it lets go of itself (player.channel = false).
            if (p.Ai1 == 0f && p.Ai0 < 60f && mod != null && Holder(out var centre) && Channel(def.Type))
            {
                p.TimeLeft = 300;
                if ((int)p.Ai0 % 3 == 0)
                {
                    var aim = AimPx() - centre;
                    v = (aim.sqrMagnitude > 1e-6f ? aim.normalized : new Vector2(mod.UseDirection, 0f)) * Mathf.Lerp(4f, 15f, p.Ai0 / 60f);
                }
                var dir = v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.down;
                p.Pos = TerraParticles.ToWorld(centre + dir * (34f + p.Ai0 / 10f));
                if ((int)p.Ai0 % 20 == 0 && p.Ai0 != 0f && p.SoundDelay <= 0)
                {
                    p.SoundDelay = 2;
                    Sound("Item_210");
                    if (p.Ai0 <= 60f) mod.CheckMana(def);
                }
                if (p.SoundDelay > 0) p.SoundDelay--;
                p.Dir = v.x >= 0f ? 1f : -1f;
                mod.KeepUsing(def, Flip(v));
                if (MagicTick(p)) p.Ai0++;
                p.Vel = Flip(v);
            }
            else
            {
                if (p.Ai1 != 1f)
                {
                    p.Ai1 = 1f;
                    Sound("Item_201", 0.2f);
                    float k = p.Ai0 < 10f ? 0f : Mathf.Clamp01(p.Ai0 / 60f);
                    v += Random.insideUnitCircle * (1f - k);
                    p.Damage = (int)(p.Damage * Mathf.Lerp(1f, 4f, k));
                    p.Knockback *= Mathf.Lerp(1f, 4f, k);
                    p.Penetrate = (int)Mathf.Lerp(1f, 3f, k);
                    DevGlacierReleases++;
                    if (Holder(out var c0) && !InSight(c0, CenterPx(p)))
                    {
                        Kill(p);
                        return;
                    }
                    var c = CenterPx(p);
                    for (int i = 0; i < 15; i++)
                    {
                        if (Random.Range(0, 2) != 0) continue;
                        int d = BoxDust(c - new Vector2(5f, 5f), 10, 10, 197);
                        if (d < 0) continue;
                        D(d).NoGravity = true;
                        var spread = RotateBy(new Vector2(Random.insideUnitCircle.x * 4f, Random.insideUnitCircle.y * 2f), ToRot(v));
                        D(d).Velocity = spread + v * Random.value;
                    }
                }
                p.Extra = p.Ai0 >= 60f ? 2 : 1;
                v.y = Mathf.Min(16f, v.y + Mathf.Lerp(0.15f, 0.025f, p.Ai0 / 45f));
                p.Vel = Flip(v);
                Move(p);
                if (p.Dead) return;
            }
            p.Frame = p.Ai0 >= 40f ? 2 : p.Ai0 >= 20f ? 1 : 0;
            p.TRot = ToRot(v) + Mathf.PI / 2f;
            int count = 1, odds = 3;
            if ((int)before != (int)p.Ai0 && (int)p.Ai0 % 20 == 0 && p.Ai1 == 0f && p.Ai0 < 50f)
            {
                count = 20;
                odds = 1;
            }
            var cc = CenterPx(p);
            for (int j = 0; j < count; j++)
            {
                if (Random.Range(0, odds) != 0) continue;
                int d = BoxDust(cc - new Vector2(5f, 5f), 10, 10, 197);
                if (d < 0) continue;
                D(d).Velocity *= 0.5f;
                D(d).NoGravity = true;
                if (p.Ai1 != 0f) continue;
                var spread = RotateBy(new Vector2(Random.insideUnitCircle.x * 4f, Random.insideUnitCircle.y * 2f), ToRot(v));
                D(d).Velocity = spread;
                if (odds != 1) D(d).Position = cc - spread * 15f;
            }
        }

        /// <summary>M:33741ff, 34446-34451: its frame about its middle, lit, and while charging a white flash swelling out every 20 ticks; a cold light growing with the charge.</summary>
        static void DrawGlacierFang(FxBatch fx, Proj p, Vector2 px)
        {
            var spr = FxBatch.Frame("Projectile_1115", 0, 70 * p.Frame, 22, 70, 11f, 35f);
            fx.Draw(spr, px, new Color(L, L, L, 1f), p.TRot, p.Scale, overPlayer: p.Ai1 == 0f);
            if (p.Ai1 == 0f)
            {
                float f = p.Ai0 % 20f;
                fx.Draw(spr, px, new Color(1f, 1f, 1f, 0f) * Mathf.Lerp(0.33f, 0f, f / 20f), p.TRot, p.Scale * Mathf.Lerp(1f, 1.5f, f / 20f), overPlayer: true);
            }
            fx.Light(px, new Color(0.2f, 0.5f, 0.8f) * Mathf.Lerp(0.5f, 1f, p.Ai0 / 60f), 2.5f);
        }

        /// <summary>P:77071-77086: Item27 and 10-30 frost motes (197), more and faster as charged.</summary>
        static void KillGlacierFang(Proj p)
        {
            Sound("Item_27");
            float k = Mathf.Clamp01(p.Ai0 / 60f);
            var c = CenterPx(p);
            var v = TV(p);
            for (int i = 10 + (int)(20f * k); i > 0; i--)
            {
                int d = BoxDust(c - new Vector2(5f, 5f), 10, 10, 197);
                if (d < 0) continue;
                D(d).Velocity = D(d).Velocity * (1f + k * 2f) + v * Random.value * k;
                D(d).NoGravity = true;
            }
        }

        // ------------------------------------------------------------------ 1116: Petalstorm (AI_196)

        /// <summary>
        /// Pl:47999-48018: three petals from just before the player (or his middle, in a wall), each
        /// flung any way upward twice over and twice along the aim, counting up from -1.
        /// </summary>
        public void ShootPetalstorm(Vector2 from, Vector2 velocity, int damage, float knockback, float crit, float face, float hue)
        {
            var c = TerraParticles.ToPx(from);
            var at = c + new Vector2(face * 15f, 3f);
            if (SolidPx(Mathf.FloorToInt(at.x / 16f), Mathf.FloorToInt(at.y / 16f))) at = c;
            var aim = Flip(velocity);
            for (int m = 0; m < 3; m++)
            {
                var e = Random.insideUnitCircle.normalized;
                var v = Random.insideUnitCircle + new Vector2(e.x * 3f, e.y * 2f);
                if (v.y > 0f) v.y = -v.y;
                v = v * 2f + aim * 2f;
                var p = Spawn(ProjKind.PetalstormPetal, TerraParticles.ToWorld(at), Flip(v), damage, knockback, crit);
                if (p == null) continue;
                p.Ai0 = -1f;
                p.Ai1 = hue;
            }
        }

        /// <summary>P:72202-72216: four blue glints (306, 50 170 255) where it fades, by how faded it is.</summary>
        static void KillPetalstorm(Proj p)
        {
            var c = CenterPx(p);
            float k = 1f - p.Alpha;
            for (int i = 0; i < 4; i++)
            {
                int d = PointDust(c + Random.insideUnitCircle * 10f * p.Scale, 306, TV(p) * 0.4f, 0, 1.5f + Random.value * 0.25f);
                if (d < 0) continue;
                D(d).NoGravity = true;
                D(d).Color = FxBatch.C(50, 170, 255);
                D(d).Scale *= k;
            }
        }
    }
}
