using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Sweep R5c's spells (Terraria's space, px y down, per update): the Shadowflame Hex Doll's
    /// curling tentacles (496, AI_091), the Tome of Infinite Wisdom's pages (712) and its twister
    /// (704, AI_145), the Laser Machinegun held out (439, AI_075) loosing stretched lasers (440), the
    /// Stellar Tune's stars swung out to their mark (856, AI_167) and the Nightglow's rainbow
    /// streaks (931, AI_171, the Constellation's stars' kind, in TerraProjectilesWhip2.cs).
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        public static int DevTentacles, DevBookStorms, DevMachinegunLasers, DevStellarStars;

        static void RegisterMagic7()
        {
            // P:5694-5706: the tentacle (unseen, dust only; three updates a tick, through three, each enemy once; a 10 px box against blocks).
            specs[ProjKind.ShadowflameTentacle] = new Spec
            {
                Ai = Ai.Custom, W = 40, H = 40, Time = 3600, Extra = 2, Penetrate = 3, OncePerNpc = true, AlphaStart = 1f, TileBox = new Vector2(10f, 10f), Look = Look.Invisible,
                Update = (t, p) => t.StepTentacle(p), Draw = (t, fx, p, px) => { }, OnKill = (t, p) => { },
                OnHit = (t, p, hm) => EnemyDebuffs.Add(hm, EnemyDebuffs.Shadowflame, Random.Range(240, 480)),
                // P:18823-18846: off a block (the speed the block leaves it is always under 1 px, so it never breaks).
                TileHit = (t, p, hx, hy, ov) =>
                {
                    if (hx) p.Vel.x = -ov.x;
                    if (hy) p.Vel.y = -ov.y;
                },
            };
            // P:7551-7561: the Tome's page (one more update a tick, 600 ticks); 8 frames of 18.
            specs[ProjKind.BookStaffShot] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_712", W = 10, H = 10, Time = 600, Extra = 1, Penetrate = 1, AlphaStart = 1f, Frames = 8,
                Update = (t, p) => StepBookShot(p), Draw = (t, fx, p, px) => DrawBookShot(fx, p, px), OnKill = (t, p) => KillBookShot(p),
            };
            // P:7449-7460: the twister, 60 by 210, each enemy once, through blocks.
            specs[ProjKind.BookStaffStorm] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_704", W = 60, H = 210, Time = 600, Penetrate = -1, TileCollide = false, OncePerNpc = true,
                Update = (t, p) => StepBookStorm(p), Draw = (t, fx, p, px) => DrawBookStorm(fx, p, px), OnKill = (t, p) => { },
            };
            // P:5093-5104: the Laser Machinegun held (harmless, 22 px); 6 frames of 60.
            specs[ProjKind.LaserMachinegunHeld] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_439", W = 22, H = 22, Frames = 6, Time = 2, Penetrate = -1, TileCollide = false, OwnMove = true, Held = true,
                CanDamage = p => false, Update = (t, p) => t.StepLaserMachinegun(p), Draw = (t, fx, p, px) => DrawLaserMachinegun(fx, p, px), OnKill = (t, p) => { },
            };
            // P:5106-5117: its laser, 5 px, two more updates a tick; stretching to 100 steps, it shrinks away once it meets a block (P:18390).
            specs[ProjKind.MachinegunLaser] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_440", W = 5, H = 5, Time = 600, Extra = 2, Penetrate = 1, AlphaStart = 1f, OwnMove = true,
                Update = (t, p) => t.StepMachinegunLaser(p), Draw = (t, fx, p, px) => DrawMachinegunLaser(fx, p, px), OnKill = (t, p) => KillMachinegunLaser(p),
                TileHit = (t, p, hx, hy, ov) =>
                {
                    if (p.Ai1 == 1f) return;
                    p.Ai1 = 1f;
                    p.Vel = ov;
                },
            };
            // P:8208-8219: the Stellar Tune's star (32 px, through blocks, two more updates a tick, each enemy once), weaker unseen by the player (P:15269).
            specs[ProjKind.StellarTuneStar] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_856", W = 32, H = 32, Time = 3600, Extra = 2, Penetrate = 1, OncePerNpc = true, TileCollide = false, OwnMove = true,
                AlphaStart = 1f, TrailLength = 2,
                ModifyHit = (t, p, hm, dealt, crit) => (Holder(out var c) && !InSight(CenterPx(p), c) ? (int)(dealt * 0.75f) : dealt, crit),
                Update = (t, p) => StepStellarStar(p), Draw = (t, fx, p, px) => DrawStellarStar(fx, p, px), OnKill = (t, p) => KillStellarStar(p),
            };
            // P:8828-8840: the Nightglow's streak (30 px, 240 ticks, through three, each enemy every 30 ticks; a 4 px box, off blocks); 20% weaker a hit (P:13551).
            specs[ProjKind.NightglowStar] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_931", W = 30, H = 30, Time = 240, Extra = 1, Penetrate = 3, LocalCooldown = 30, TileBox = new Vector2(4f, 4f),
                AlphaStart = 1f, TrailLength = 20,
                Update = (t, p) => StepConstellationStar(p), Draw = (t, fx, p, px) => DrawConstellationStar(fx, p, px), OnKill = (t, p) => KillConstellationStar(p),
                OnHit = (t, p, hm) =>
                {
                    p.Damage = (int)(p.Damage * 0.8f);
                    p.Target = null;
                },
                TileHit = (t, p, hitX, hitY, oldVel) =>
                {
                    if (hitX) p.Vel.x = -oldVel.x;
                    if (hitY) p.Vel.y = -oldVel.y;
                },
            };
        }

        // ------------------------------------------------------------------ 496: Shadowflame Hex Doll (AI_091)

        /// <summary>
        /// Pl:47955-47988: 4 px along the aim plus a step any way, set to the shot's speed, curling by
        /// up to 0.08 either way on each axis; the use's last tentacle goes straight.
        /// </summary>
        public void ShootTentacle(Vector2 from, Vector2 velocity, int damage, float knockback, float crit, bool last)
        {
            var v = Flip(velocity);
            float speed = v.magnitude;
            var dir = (v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.right) * 4f;
            if (!last)
            {
                var r = new Vector2(Random.Range(-100, 101), Random.Range(-100, 101));
                dir += r.sqrMagnitude > 0f ? r.normalized : Vector2.zero;
            }
            dir = (dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right) * speed;
            float c1 = Random.Range(10, 80) * 0.001f * (Random.Range(0, 2) == 0 ? -1f : 1f), c0 = Random.Range(10, 80) * 0.001f * (Random.Range(0, 2) == 0 ? -1f : 1f);
            var p = Spawn(ProjKind.ShadowflameTentacle, from, Flip(dir), damage, knockback, crit);
            if (p == null) return;
            p.Ai0 = last ? 0f : c0;
            p.Ai1 = last ? 0f : c1;
            DevTentacles++;
        }

        /// <summary>
        /// AI_091 (P:33098-33137): shrinking from 20 px (0.01 an update, then 0.025) and gone at 0.95;
        /// its curl grows 5% an update (to 16 px a tick); purple flame (27) all along it, more while big.
        /// </summary>
        void StepTentacle(Proj p)
        {
            float scale = 1f - p.LocalAi0;
            p.Scale = scale;
            p.HitSize = new Vector2((int)(20f * scale), (int)(20f * scale));
            p.LocalAi0 += p.LocalAi0 < 0.1f ? 0.01f : 0.025f;
            if (p.LocalAi0 >= 0.95f)
            {
                Kill(p);
                return;
            }
            var v = TV(p);
            v += new Vector2(p.Ai0, p.Ai1) * 1.5f;
            if (v.magnitude > 16f) v = v.normalized * 16f;
            p.Vel = Flip(v);
            p.Ai0 *= 1.05f;
            p.Ai1 *= 1.05f;
            if (scale >= 1f) return;
            var c = CenterPx(p);
            float w = 20f * scale;
            for (int k = 0; k < scale * 10f; k++)
            {
                int i = BoxDust(c - new Vector2(w / 2f, w / 2f), (int)w, (int)w, 27, v.x, v.y, 100, 1.1f);
                if (i < 0) continue;
                D(i).Position = (D(i).Position + c) / 2f;
                D(i).NoGravity = true;
                D(i).Velocity = D(i).Velocity * 0.1f - v * (1.3f - scale);
                D(i).FadeIn = 100f;
                D(i).Scale += scale * 0.75f;
            }
        }

        // ------------------------------------------------------------------ 712 / 704: Tome of Infinite Wisdom

        /// <summary>
        /// Pl:49555-49566: a page along the aim; with the right button, a twister (704) at the player,
        /// 100 px over his feet, drifting his way at the shot's speed, 75% harder.
        /// </summary>
        public void ShootBookStaff(Vector2 from, Vector2 velocity, int damage, float knockback, float crit, bool storm, float face, Vector2 feet)
        {
            if (!storm)
            {
                Spawn(ProjKind.BookStaffShot, from, velocity, damage, knockback, crit);
                return;
            }
            var at = new Vector2(TerraParticles.ToPx(from).x, TerraParticles.ToPx(feet).y - 100f);
            if (Spawn(ProjKind.BookStaffStorm, TerraParticles.ToWorld(at), new Vector2(face * velocity.magnitude, 0f), (int)(damage * 1.75f), knockback, crit) != null) DevBookStorms++;
        }

        /// <summary>
        /// AI_001 for 712 (P:56663-56700): a grey glow ahead of it; as it appears, four white sparks
        /// (15) ahead; its pages flutter (four frames of one book or the other, 4 ticks each).
        /// </summary>
        static void StepBookShot(Proj p)
        {
            var v = TV(p);
            p.TRot = ToRot(v) + Mathf.PI / 2f;
            if (p.Alpha >= 1f) p.Frame = Random.Range(0, 2) * 4;
            if (++p.FrameCounter >= 4)
            {
                p.FrameCounter = 0;
                p.Frame++;
                if (p.Frame == 4) p.Frame = 0;
                if (p.Frame >= 8) p.Frame = 4;
            }
            if (p.Alpha <= 0f) return;
            p.Alpha = 0f;
            p.Scale = 1.1f;
            p.Frame = Random.Range(0, 14) % 8;
            var c = CenterPx(p);
            var along = v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.zero;
            for (int k = 0; k < 4; k++)
            {
                var ring = RotateBy(new Vector2(0f, 1f), k * Mathf.PI * 2f / 4f);
                ring = RotateBy(new Vector2(ring.x, ring.y * 4f), ToRot(v));
                int i = PointDust(c + ring + along * 50f, 15, Vector2.zero, 0, 1.7f);
                if (i < 0) continue;
                D(i).NoGravity = true;
                D(i).Velocity = (D(i).Velocity * 2f + (ring.sqrMagnitude > 1e-6f ? ring.normalized : Vector2.down) * 0.3f + along * 3f) * 0.7f;
            }
        }

        /// <summary>M:33741-33755, P:81257: its frame about its middle, 40% toward white.</summary>
        static void DrawBookShot(FxBatch fx, Proj p, Vector2 px)
        {
            float o = 1f - p.Alpha;
            var lit = Color.Lerp(new Color(L, L, L, 1f), Color.white, 0.4f) * o;
            fx.Draw(FxBatch.Frame("Projectile_712", 0, Mathf.Clamp(p.Frame, 0, 7) * 18, 14, 18, 7f, 9f), px, lit, p.TRot, p.Scale);
            fx.Light(px, new Color(0.28f, 0.28f, 0.28f), 2.5f);
        }

        /// <summary>P:77714-77736: chips, Item10 and 6-11 faint white sparks.</summary>
        static void KillBookShot(Proj p)
        {
            if (p.HitTile) HitTileDust(p);
            Sound("Item_10");
            var v = TV(p);
            var c = CenterPx(p);
            for (int k = Random.Range(6, 12); k > 0; k--)
            {
                int i = PointDust(c, 15, Vector2.zero, 100);
                if (i < 0) continue;
                D(i).Velocity = Random.insideUnitCircle * 1.6f + new Vector2(0f, -1f) - v * (Random.value * 2f - 1f) * 0.5f;
                D(i).Scale = 1f;
                D(i).FadeIn = 1.5f;
                D(i).NoGravity = true;
                D(i).Color = new Color(0.3f, 0.3f, 0.3f, 0f);
            }
        }

        /// <summary>
        /// AI_145 (P:71421-71572): its howl as it starts; 300 updates (fewer once lifted off the
        /// ground long); it keeps its foot on the ground (rising while the lowest 160 px of its middle
        /// are in blocks, else falling, 2 then 4 px a tick, to 16); dust swirling up it, smoke at its
        /// foot, pages flung up now and then.
        /// </summary>
        static void StepBookStorm(Proj p)
        {
            const float life = 300f;
            if (p.SoundDelay == 0)
            {
                p.SoundDelay = -1;
                Sound("Custom/dd2_book_staff_twister_loop");
            }
            if (p.LocalAi0 >= 16f && p.Ai0 < life - 15f) p.Ai0 = life - 15f;
            if (++p.Ai0 >= life)
            {
                TerraProjectiles.Instance?.Kill(p);
                return;
            }
            var c = CenterPx(p);
            float top = c.y - 105f, bottom = c.y + 105f;
            var v = TV(p);
            bool grounded = false;
            for (int i = Mathf.FloorToInt((c.x - 8f) / 16f); i <= Mathf.FloorToInt((c.x + 8f) / 16f) && !grounded; i++)
                for (int j = Mathf.FloorToInt((bottom - 160f) / 16f); j <= Mathf.FloorToInt(bottom / 16f); j++)
                    if (SolidPx(i, j))
                    {
                        grounded = true;
                        break;
                    }
            if (grounded)
            {
                if (v.y > 0f) v.y = 0f;
                if (v.y > -4f) v.y -= 2f;
                else
                {
                    v.y -= 4f;
                    p.LocalAi0 += 2f;
                }
                v.y = Mathf.Max(v.y, -16f);
            }
            else
            {
                p.LocalAi0 = Mathf.Max(0f, p.LocalAi0 - 1f);
                if (v.y < 0f) v.y = 0f;
                v.y += v.y < 4f ? 2f : 4f;
                v.y = Mathf.Min(v.y, 16f);
            }
            p.Vel = Flip(v);
            var span = new Vector2((bottom - top) * 0.2f, bottom - top);
            if (p.Ai0 < life - 30f)
            {
                float a = Random.value;
                var k = new Vector2(Mathf.Lerp(0.1f, 1f, Random.value), Mathf.Lerp(-1f, 0.9f, a));
                k.x *= -Mathf.Lerp(2.2f, 0.6f, a);
                var at = c + new Vector2(span.x * k.x, span.y * k.y) * 0.5f + new Vector2(6f, 10f);
                int i = PointDust(at, 274, Vector2.zero, 211, 0.87f);
                if (i >= 0)
                {
                    D(i).FadeIn = 1.3f;
                    if (k.x > -1.2f) D(i).Velocity.x = 1f + Random.value;
                    D(i).NoGravity = true;
                    D(i).Velocity.y = Random.value * -0.5f - 1.3f;
                    D(i).Velocity.x += v.x * 2.1f;
                }
            }
            var foot = new Vector2(c.x - 25f, bottom - 25f);
            for (int n = 0; n < 4; n++)
            {
                int i = BoxDust(foot, 50, 25, 31, v.x, -2f, 100);
                if (i < 0) continue;
                D(i).FadeIn = 1.1f;
                D(i).NoGravity = true;
            }
            var pp = TerraParticles.Instance;
            if (pp == null) return;
            for (int n = 0; n < 3; n++)
            {
                if (Random.Range(0, n == 0 ? 5 : 7) != 0) continue;
                var gat = new Vector2(c.x - 30f + Random.value * 60f, top + Random.value * 210f);
                pp.NewGore(gat, new Vector2(v.x * 1.5f + v.x, -Random.value * 16f), Random.Range(0, 3) == 0 ? 1007 : 1008, exact: true, alpha: n == 0 ? 50 : 80, falls: true);
            }
        }

        /// <summary>
        /// M:33129-33201: slices of its swirl from its foot up every 4-5 px, each turned further,
        /// bigger up high, sand coloured (160 140 100) faint at the foot and the top, a pale blue
        /// (140 160 255) copy pulsing through once a second; fading in over 30 updates and out over its last 60.
        /// </summary>
        static void DrawBookStorm(FxBatch fx, Proj p, Vector2 px)
        {
            var spr = FxBatch.Frame("Projectile_704", 0, 0, 60, 60, 30f, 30f);
            if (spr == null) return;
            float ai = p.Ai0, fade = Mathf.Clamp01(ai / 30f);
            if (ai > 240f) fade = Mathf.Lerp(1f, 0f, (ai - 240f) / 60f);
            bool right = p.Vel.x > 0f;
            float spin = -Mathf.PI / 20f * ai * (right ? -1f : 1f);
            float step = Mathf.Max(4.11f, 5.01f - ai / 150f * 0.9f);
            var sand = new Color(160f / 255f, 140f / 255f, 100f / 255f, 127f / 255f);
            var blue = new Color(140f / 255f, 160f / 255f, 1f, 127f / 255f);
            float beat = ai % 60f;
            blue *= beat < 30f ? Mathf.InverseLerp(22f, 30f, beat) : Mathf.InverseLerp(38f, 30f, beat);
            bool pulse = blue.a > 0f;
            float top = px.y - 105f, bottom = px.y + 105f, h = bottom - top, up = 0f;
            for (float y = Mathf.Floor(bottom); y > Mathf.Floor(top); y -= step)
            {
                up += step;
                float t = up / h;
                float turn = up * Mathf.PI * 2f / -20f * (right ? -1f : 1f);
                float profile = t > 0.5f ? 2f - t * 2f : t * 2f;
                var at = new Vector2(px.x, y);
                float rot = spin + turn + (right ? Mathf.PI : 0f);
                if (pulse)
                {
                    var b = blue * Mathf.Clamp01(profile);
                    b.a *= 0.5f;
                    fx.Draw(spr, at, b * fade, rot, (1f + t - 0.35f) * 0.8f, flipX: right);
                }
                var s = sand * Mathf.Clamp01(profile);
                s.a *= 0.5f;
                fx.Draw(spr, at, s * fade, rot, 1f + t - 0.35f, flipX: right);
            }
        }

        // ------------------------------------------------------------------ 439 / 440: Laser Machinegun (AI_075)

        /// <summary>
        /// AI_075 for 439 (P:67185-67290): a pair of lasers every 24 ticks (6 fewer at 40, 80 and 120
        /// held), each paying the mana; 14 px a tick from up to 7 px about its middle, up to 5.6° off
        /// the aim; Item91 with each; violet sparks (135) at its muzzle after; six frames, faster as it
        /// speeds up; 20 px out along the aim.
        /// </summary>
        void StepLaserMachinegun(Proj p)
        {
            var mod = TerraMod.Instance;
            var def = ItemDb.Get(ItemType.LaserMachinegun);
            if (mod == null || !Holder(out var centre))
            {
                Kill(p);
                return;
            }
            // Spin-up is real time; only the shot clock, animation and sound
            // use Player.GetSlowMagicUseRate/Scalar (AI_075, 439).
            float slow = mod.SlowMagicUse ? TerraMod.SlowMagic : 1f;
            float rate = 1f / slow;
            p.Ai0++;
            int stage = (p.Ai0 >= 40f ? 1 : 0) + (p.Ai0 >= 80f ? 1 : 0) + (p.Ai0 >= 120f ? 1 : 0);
            int every = 24 - 6 * stage;
            p.Ai1 += rate;
            bool shoot = false;
            if (p.Ai1 >= every)
            {
                p.Ai1 = 0f;
                shoot = true;
            }
            p.FrameCounter += 1 + stage;
            if (p.FrameCounter >= 4f * slow)
            {
                p.FrameCounter = 0;
                if (++p.Frame >= 6) p.Frame = 0;
            }
            if (--p.SoundDelay <= 0)
            {
                p.SoundDelay = (int)(every * slow);
                if (p.Ai0 != 1f) Sound("Item_91");
            }
            var v = TV(p);
            if (p.Ai1 == rate && p.Ai0 != 1f)
            {
                var muzzle = centre + v + (v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.right) * 24f;
                for (int k = 0; k < 2; k++)
                {
                    int i = BoxDust(muzzle - new Vector2(8f, 8f), 16, 16, 135, v.x / 2f, v.y / 2f, 100);
                    if (i < 0) continue;
                    D(i).Velocity *= 0.66f;
                    D(i).NoGravity = true;
                    D(i).Scale = 1.4f;
                }
            }
            if (p.Ai0 == 1f || v == Vector2.zero) v = HeldAim(centre, def, p.Scale);
            if (shoot)
            {
                if (!Channel(def.Type) || !mod.CheckMana(def))
                {
                    Kill(p);
                    return;
                }
                v = HeldAim(centre, def, p.Scale);
                var keep = SpawnClass;
                SpawnClass = DamageClass.Magic;
                for (int k = 0; k < 2; k++)
                {
                    var from = centre + v + new Vector2(Random.Range(-7, 8), Random.Range(-7, 8));
                    var go = RotateBy(v.normalized * 14f, Random.value * 0.19634955f - 0.09817477f);
                    if (Spawn(ProjKind.MachinegunLaser, TerraParticles.ToWorld(from), Flip(go), p.Damage, p.Knockback, p.Crit) != null) DevMachinegunLasers++;
                }
                SpawnClass = keep;
            }
            float facing = v.x < 0f ? -1f : 1f;
            p.Pos = TerraParticles.ToWorld(centre + v);
            p.Vel = Flip(v);
            p.TRot = ToRot(v) + Mathf.PI / 2f;
            p.Dir = facing;
            p.TimeLeft = 2;
            mod.KeepUsing(def, Flip(v));
        }

        /// <summary>M:32633-32699: its frame about its middle, mirrored facing left, and its glow (35) added over it.</summary>
        static void DrawLaserMachinegun(FxBatch fx, Proj p, Vector2 px)
        {
            var at = new Vector2(Mathf.Floor(px.x), Mathf.Floor(px.y));
            bool flip = p.Dir < 0f;
            fx.Draw(FxBatch.Frame("Projectile_439", 0, 60 * p.Frame, 30, 60, 15f, 30f), at, new Color(L, L, L, 1f), p.TRot, p.Scale, flipX: flip, overPlayer: true);
            var glow = FxBatch.Frame("Glow_35", 0, 60 * p.Frame, 30, 60, 15f, 30f);
            if (glow != null) fx.Draw(glow, at, new Color(1f, 1f, 1f, 0f), p.TRot, p.Scale, flipX: flip, overPlayer: true);
        }

        /// <summary>
        /// AI_001 for 440 (P:55738-55790): coming in 25 an update, a blue light; its length grows 3 an
        /// update to 100 while it flies, and once it meets a block it stops and shrinks away.
        /// </summary>
        void StepMachinegunLaser(Proj p)
        {
            p.Alpha = Mathf.Max(0f, p.Alpha - 25f / 255f);
            var v = TV(p);
            p.TRot = ToRot(v) + Mathf.PI / 2f;
            if (p.Ai1 == 0f)
            {
                p.LocalAi0 = Mathf.Min(100f, p.LocalAi0 + 3f);
                Move(p);
            }
            else
            {
                p.LocalAi0 -= 3f;
                if (p.LocalAi0 <= 0f) Kill(p);
            }
        }

        /// <summary>M:32084-32107: a copy every 3 px back along its length, added, fading toward the tail.</summary>
        static void DrawMachinegunLaser(FxBatch fx, Proj p, Vector2 px)
        {
            var spr = FxBatch.Frame("Projectile_440", 0, 0, 6, 10, 3f, 2.5f);
            if (spr == null) return;
            var v = TV(p);
            var back = v.sqrMagnitude > 1e-6f ? v.normalized * 3f : Vector2.zero;
            float full = p.Ai1 == 1f ? (int)p.LocalAi0 : 100f, o = 1f - p.Alpha;
            for (int i = 1; i <= (int)p.LocalAi0; i++)
            {
                float k = o * (full - i) / full;
                fx.Draw(spr, px - back * i, new Color(k, k, k, 0f), p.TRot, p.Scale);
            }
            fx.Light(px, new Color(0.25f, 0.4f, 0.7f), 2.5f);
        }

        /// <summary>P:74946-74956: 3-6 violet sparks (135).</summary>
        static void KillMachinegunLaser(Proj p)
        {
            var c = CenterPx(p) - TV(p) / 2f;
            for (int k = Random.Range(3, 7); k > 0; k--)
            {
                int i = PointDust(c, 135, Random.insideUnitCircle * 2f, 100, 2.1f);
                if (i >= 0) D(i).NoGravity = true;
            }
        }

        // ------------------------------------------------------------------ 856: Stellar Tune (AI_167)

        /// <summary>
        /// Pl:48446-48474: a star for a random enemy in the 1000 by 800 px about the player (where it
        /// will be in 20 ticks), else near the cursor (700 px at most); it swings out on a random
        /// upward bend twice as long as the way there.
        /// </summary>
        public void ShootStellarTune(Vector2 from, int damage, float knockback, float crit, Vector2 cursor)
        {
            var c = TerraParticles.ToPx(from);
            var targets = new System.Collections.Generic.List<HealthManager>();
            foreach (var hm in EnemiesNear(c, 700f))
            {
                var e = EnemyCenterPx(hm);
                if (Mathf.Abs(e.x - c.x) <= 500f && Mathf.Abs(e.y - c.y) <= 400f) targets.Add(hm);
            }
            Vector2 mark;
            if (targets.Count > 0)
            {
                var hm = targets[Random.Range(0, targets.Count)];
                var rb = hm.GetComponent<Rigidbody2D>();
                mark = EnemyCenterPx(hm) + (rb != null ? new Vector2(rb.linearVelocity.x, -rb.linearVelocity.y) / 60f / Px * 20f : Vector2.zero);
            }
            else
            {
                mark = TerraParticles.ToPx(cursor);
                var d0 = mark - c;
                mark += Random.insideUnitCircle * 24f;
                if (d0.magnitude > 700f) mark = c + d0 * (700f / d0.magnitude);
            }
            var bend = Random.insideUnitCircle.normalized;
            if (bend.y > 0f) bend = -bend;
            if (Mathf.Abs(bend.y) < 0.5f) bend.y = -Random.value * 0.5f - 0.5f;
            bend *= (mark - c).magnitude * 2f;
            var p = Spawn(ProjKind.StellarTuneStar, from, Flip(bend), damage, knockback, crit);
            if (p == null) return;
            p.StartPos = TerraParticles.ToWorld(mark);
            DevStellarStars++;
        }

        /// <summary>
        /// Player.PlayGuitarChord (Pl:47073-47105): one of six chords (Item133-138, quieter) by how
        /// far off the cursor is, a tenth of the time out of tune.
        /// </summary>
        public static void GuitarChord(Vector2 from, Vector2 cursor)
        {
            var cam = Camera.main;
            float half = cam != null ? cam.orthographicSize * Mathf.Min(1f, cam.aspect) : 8.5f;
            float range = Mathf.Min(1f, Vector2.Distance(cursor, from) / half);
            const float n = 1f / 6f;
            int item = range > n * 5f ? 135 : range > n * 4f ? 134 : range > n * 3f ? 133 : range > n * 2f ? 137 : range > n ? 138 : 136;
            TerraMod.Instance?.PlaySound("Item_" + item, 0.45f, Random.Range(0, 10) == 0 ? Random.Range(-50, 51) * 0.01f : 0f);
        }

        /// <summary>
        /// AI_167 (P:47428-47448): over 90 updates along a Catmull-Rom curve from the player (as he is
        /// now) to its mark, bent by its velocity; spinning once round, a hot pink light.
        /// </summary>
        static void StepStellarStar(Proj p)
        {
            const float n = 90f;
            if (++p.LocalAi0 >= n - 1f)
            {
                TerraProjectiles.Instance?.Kill(p);
                return;
            }
            if (!Holder(out var player))
            {
                TerraProjectiles.Instance?.Kill(p);
                return;
            }
            float t = p.LocalAi0 / n;
            var mark = TerraParticles.ToPx(p.StartPos);
            var back = -TV(p);
            var a = player + back * 2f;
            var b = mark + back * (1f - t * 3f);
            p.Pos = TerraParticles.ToWorld(CatmullRom(a, player, mark, b, t));
            p.Alpha = 0f;
            p.TRot = Mathf.PI * 2f * t;
        }

        static Vector2 CatmullRom(Vector2 a, Vector2 b, Vector2 c, Vector2 d, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * (2f * b + (c - a) * t + (2f * a - 5f * b + 4f * c - d) * t2 + (3f * b - a - 3f * c + d) * t3);
        }

        /// <summary>
        /// DrawProjWithStarryTrail (M:38268-38376): three hot pink streaks (Extra 91) wheeling about
        /// its head and two white ones pulsing behind, all fading in and out over its flight and near
        /// the player; then the falling star itself (Item 75), white.
        /// </summary>
        static void DrawStellarStar(FxBatch fx, Proj p, Vector2 px)
        {
            var mark = TerraParticles.ToPx(p.StartPos);
            Holder(out var player);
            var step = p.Trail.Count > 1 ? px - TerraParticles.ToPx(p.Trail[1]) : Vector2.zero;
            var dir = step.sqrMagnitude > 1e-8f ? step * (5f / step.magnitude) : new Vector2(0f, 5f);
            float la = Mathf.InverseLerp(0f, 120f, Vector2.Distance(mark, player)) * Mathf.InverseLerp(0f, 120f, Vector2.Distance(px, player));
            float lb = Mathf.InverseLerp(90f, 75f, p.LocalAi0) * Mathf.InverseLerp(0f, 15f, p.LocalAi0);
            float lc = Mathf.InverseLerp(90f, 60f, p.LocalAi0) * Mathf.InverseLerp(0f, 20f, p.LocalAi0);
            float grow = (-0.3f * (1f - lc) - Mathf.InverseLerp(15f, 0f, p.LocalAi0)) * la, glow = lb * la;
            var pink = new Color(1f, 105f / 255f, 180f / 255f) * 0.15f * glow * glow;
            pink.a = 0f;
            var white = Color.white * 0.5f * glow;
            white.a = 0f;
            var whole = FxBatch.Whole("Extra_91");
            var streak = whole != null ? FxBatch.Frame("Extra_91", 0, 0, (int)whole.rect.width, (int)whole.rect.height, whole.rect.width / 2f, 10f) : null;
            float time = Time.time, rot = ToRot(dir) + Mathf.PI / 2f;
            var head = px + dir + dir * 0.5f;
            var wheel = new Vector2(0f, -2f);
            float[] sizes = { 1.5f, 1.1f, 1.3f };
            for (int k = 0; k < 3 && streak != null; k++)
                fx.Draw(streak, head + RotateBy(wheel, Mathf.PI * 2f * time + k * Mathf.PI * 2f / 3f), pink, rot, sizes[k] + grow);
            var tail = px + dir - dir * 0.5f;
            for (float s = 0f; s < 1f && streak != null; s += 0.5f)
            {
                float f = (time % 0.5f / 0.5f + s) % 1f, w = f * 2f;
                if (w > 1f) w = 2f - w;
                fx.Draw(streak, tail, white * w, rot, 0.3f + f * 0.5f);
            }
            var star = FxBatch.Whole("Item_75");
            if (star != null)
            {
                int sw = (int)star.rect.width, sh = (int)star.rect.height / 8;
                fx.Draw(FxBatch.Frame("Item_75", 0, 0, sw, sh, sw / 2f, sh / 2f), px, Color.white * (1f - p.Alpha), p.TRot, p.Scale);
            }
            fx.Light(px, new Color(1f, 105f / 255f, 180f / 255f) * 0.3f, 2.5f);
        }

        /// <summary>P:72804-72830: six sparkles (58), a ring of hot pink and one of orange glitter (278), a star gore.</summary>
        static void KillStellarStar(Proj p)
        {
            var c = CenterPx(p);
            for (int k = 0; k < 6; k++) BoxDust(c - new Vector2(16f, 16f), 32, 32, 58, 0f, 0f, 150, 0.8f);
            for (int ring = 0; ring < 2; ring++)
                for (float f = 0f; f < 1f; f += 0.34f)
                {
                    float speed = ring == 0 ? 4f + Random.value * 2f : 2f + Random.value;
                    var v = RotateBy(new Vector2(0f, 1f), f * Mathf.PI * 2f + Random.value * Mathf.PI) * speed;
                    int i = PointDust(c, 278, v, 150);
                    if (i < 0) continue;
                    D(i).NoGravity = true;
                    D(i).Color = Color.Lerp(Color.white, ring == 0 ? new Color(1f, 105f / 255f, 180f / 255f) : new Color(1f, 0.65f, 0f), Random.value * 0.5f + 0.5f);
                }
            TerraParticles.Instance?.NewGore(c - new Vector2(16f, 16f), Random.insideUnitCircle.normalized * 1.5f, 16);
        }

        // ------------------------------------------------------------------ 931: Nightglow

        /// <summary>
        /// Pl:48333-48350: a streak from just before the player (or his middle, in a wall) flung any
        /// way upward 2-4 px a tick, its hue moving along the use and the player's clock.
        /// </summary>
        public void ShootNightglow(Vector2 from, int damage, float knockback, float crit, float face, float hue)
        {
            var c = TerraParticles.ToPx(from);
            var at = c + new Vector2(face * 15f, 3f);
            if (SolidPx(Mathf.FloorToInt(at.x / 16f), Mathf.FloorToInt(at.y / 16f))) at = c;
            var v = Random.insideUnitCircle + Random.insideUnitCircle.normalized * 3f;
            if (v.y > 0f) v.y = -v.y;
            var p = Spawn(ProjKind.NightglowStar, TerraParticles.ToWorld(at), Flip(v), damage, knockback, crit);
            if (p == null) return;
            p.Ai1 = hue;
        }
    }
}
