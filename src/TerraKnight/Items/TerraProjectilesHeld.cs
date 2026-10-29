using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The spells held out while the button is held (Terraria's space, px y down, per update):
    /// the Last Prism (633, AI_075: six rainbow beams, 632, AI_084, that close in over three
    /// seconds into one beam of triple damage, the mana paid faster the longer it runs), the
    /// Charged Blaster Cannon (460: blaster orbs, 459, growing with the charge, then after three
    /// seconds a five second beam, 461) and the Medusa Head (535, AI_100: every quarter second
    /// its gaze, 536, strikes the three nearest enemies it can see within 320 px).
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        static void RegisterHeld()
        {
            specs[ProjKind.LastPrism] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_633", W = 14, H = 18, Time = 2, Penetrate = -1, TileCollide = false, OwnMove = true, Held = true,
                CanDamage = p => false, Update = (t, p) => t.StepLastPrism(p), Draw = (t, fx, p, px) => DrawLastPrism(fx, p, px), OnKill = (t, p) => { },
            };
            specs[ProjKind.LastPrismBeam] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_632", W = 18, H = 18, Time = 3600, Penetrate = -1, TileCollide = false, OwnMove = true, AlphaStart = 1f, OwnerImmune = 5,
                CanDamage = p => p.Parent != null && p.Parent.Ai0 > 30f, Beam = p => (CenterPx(p), CenterPx(p) + TV(p).normalized * p.LocalAi1, 22f * p.Scale),
                Update = (t, p) => t.StepPrismBeam(p), Draw = (t, fx, p, px) => DrawPrismBeam(fx, p, px), OnKill = (t, p) => { },
            };
            specs[ProjKind.ChargedBlaster] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_460", W = 14, H = 18, Time = 2, Penetrate = -1, TileCollide = false, OwnMove = true, Held = true,
                CanDamage = p => false, Update = (t, p) => t.StepChargedBlaster(p), Draw = (t, fx, p, px) => t.DrawChargedBlaster(fx, p, px), OnKill = (t, p) => { },
            };
            specs[ProjKind.ChargedBlasterOrb] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_459", W = 22, H = 22, Time = 3600, Extra = 1, AlphaStart = 1f, TrailLength = 10,
                Update = (t, p) => StepBlasterOrb(p), Draw = (t, fx, p, px) => DrawBlasterOrb(fx, p, px), OnKill = (t, p) => t.KillBlasterOrb(p),
            };
            specs[ProjKind.ChargedBlasterBeam] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_461", W = 18, H = 18, Time = 3600, Penetrate = -1, TileCollide = false, OwnMove = true,
                Beam = p => (CenterPx(p), CenterPx(p) + TV(p).normalized * p.LocalAi1, 22f * p.Scale),
                Update = (t, p) => t.StepBlasterBeam(p), Draw = (t, fx, p, px) => t.DrawBlasterBeam(fx, p, px), OnKill = (t, p) => { },
            };
            specs[ProjKind.MedusaHead] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_535", W = 18, H = 18, Time = 3, Penetrate = -1, TileCollide = false, OwnMove = true, Held = true,
                CanDamage = p => false, Update = (t, p) => t.StepMedusaHead(p), Draw = (t, fx, p, px) => t.DrawMedusaHead(fx, p, px), OnKill = (t, p) => { },
            };
            specs[ProjKind.MedusaRay] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_536", W = 10, H = 10, Time = 3600, Penetrate = -1, TileCollide = false, OwnMove = true,
                CanDamage = p => false, Update = (t, p) => StepMedusaRay(p), Draw = (t, fx, p, px) => { }, OnKill = (t, p) => { },
            };
        }

        // ------------------------------------------------------------------ the holder

        /// <summary>The player's centre (Terraria px), or false when there's no one to hold it.</summary>
        static bool Holder(out Vector2 centre)
        {
            // Another player's ghost being drawn: held by them.
            if (ghostHolder.HasValue)
            {
                centre = ghostHolder.Value;
                return true;
            }
            var hero = HeroController.SilentInstance;
            centre = default;
            if (hero == null || hero.cState.dead) return false;
            centre = TerraParticles.ToPx(hero.GetComponent<BoxCollider2D>().bounds.center);
            return true;
        }

        /// <summary>player.channel: the button still held with the item in hand.</summary>
        static bool Channel(ItemType type)
        {
            var mod = TerraMod.Instance;
            return mod != null && mod.UseHeld && mod.Inventory.Current.Type == type;
        }

        /// <summary>The cursor in Terraria px.</summary>
        static Vector2 AimPx()
        {
            var mod = TerraMod.Instance;
            return TerraParticles.ToPx(mod != null ? mod.DevAim ?? mod.AimPoint : Vector2.zero);
        }

        /// <summary>
        /// Player.GetArmPosition with the arm the aim gives (Main.OffsetsPlayerOnhand: up, ahead or
        /// down, as the player sprite draws it; or raised for useStyle 4), off the player's centre.
        /// </summary>
        static Vector2 ArmPx(Vector2 centre, float facing, int frame)
        {
            var on = frame == 2 ? new Vector2(12f, 10f) : frame == 4 ? new Vector2(12f, 19f) : new Vector2(13f, 17f);
            on *= 2f;
            if (facing < 0f) on.x = 40f - on.x;
            on -= new Vector2(10f, 7f);
            return centre - new Vector2(10f, 21f) + on;
        }

        /// <summary>The arm frame the player sprite takes for an aim along dir (px, y down).</summary>
        static int AimFrame(Vector2 dir)
        {
            float facing = dir.x < 0f ? -1f : 1f;
            float up = Mathf.Atan2(-dir.y, Mathf.Abs(dir.x) < 0.01f ? 0.01f : dir.x * facing) * Mathf.Rad2Deg;
            return up > 35f ? 2 : up < -35f ? 4 : 3;
        }

        /// <summary>Collision.LaserScan with no width: how far along dir (px) from a point the first block is, up to max.</summary>
        static float LaserScan(Vector2 fromPx, Vector2 dir, float max)
        {
            var hit = Physics2D.Raycast(TerraParticles.ToWorld(fromPx), Flip(dir).normalized, max * Px, 1 << ChunkColliders.TerrainLayer);
            return hit.collider != null ? hit.distance / Px : max;
        }

        /// <summary>
        /// Utils.DrawLaser with DelegateMethods.RainbowLaserDraw's framing (the 26 px wide beam
        /// sheets, 632 and 461): a 22 px start, 28 px middles laid end to end, a 22 px end.
        /// </summary>
        static void DrawLaser(FxBatch fx, string tex, Vector2 start, Vector2 end, Vector2 scale, Color c)
        {
            var d = end - start;
            float num = d.magnitude;
            if (num < 1e-4f) return;
            var dir = d / num;
            float rot = Mathf.Atan2(dir.y, dir.x) - Mathf.PI / 2f;
            var pos = start;
            fx.Draw(FxBatch.Frame(tex, 0, 0, 26, 22, 13f, 11f), pos, c, rot, scale);
            num -= 33f * scale.y;
            pos += dir * (22f - 11f) * scale.y;
            if (num > 0f)
            {
                float done = 0f;
                while (done + 1f < num)
                {
                    int h = 28;
                    float covered = 28f;
                    if (scale.y < 0.05f) scale.y = 0.05f;
                    if (num - done < h)
                    {
                        covered *= (num - done) / h;
                        h = (int)(num - done);
                    }
                    if (h > 0) fx.Draw(FxBatch.Frame(tex, 0, 25, 26, h, 13f, 0f), pos, c, rot, scale);
                    done += covered * scale.y;
                    pos += dir * covered * scale.y;
                }
            }
            fx.Draw(FxBatch.Frame(tex, 0, 56, 26, 22, 13f, 1f), pos, c, rot, scale);
        }

        /// <summary>DelegateMethods.CastLight along a beam: its colour every tile.</summary>
        static void BeamLight(FxBatch fx, Vector2 from, Vector2 dir, float length, Color c)
        {
            for (float t = 0f; t < length; t += 32f) fx.Light(from + dir * t, c, 2f);
        }

        // ------------------------------------------------------------------ 633: the Last Prism (AI_075)

        /// <summary>
        /// AI_075 for 633: each update it turns 8% of the way to the cursor (at 30 px); its damage
        /// follows the item's; mana every 30 updates, 15 past 90, 5 past 120; the six beams as it
        /// starts; a hum (Item15) every 20 updates; five frames, faster past 120; gone when let go
        /// or out of mana.
        /// </summary>
        void StepLastPrism(Proj p)
        {
            var mod = TerraMod.Instance;
            var def = ItemDb.Get(ItemType.LastPrism);
            if (mod == null || !Holder(out var centre))
            {
                Kill(p);
                return;
            }
            float period = p.Ai0 > 120f ? 5f : p.Ai0 > 90f ? 15f : 30f;
            p.Damage = Equipment.Damage(def);
            // AI_075's prism charges and pays on real ticks even in slow magic.
            // Its reduced rate of damage is the beam's shared owner immunity.
            p.Ai0++;
            bool pay = p.Ai0 % period == 0f;
            var v = TV(p);
            var aim = AimPx() - centre;
            var want = aim.sqrMagnitude > 1e-6f ? aim.normalized : new Vector2(0f, -1f);
            var cur = v.sqrMagnitude > 1e-6f ? v.normalized : want;
            v = Vector2.Lerp(want, cur, 0.92f).normalized * def.ShootSpeed * p.Scale;
            if (++p.FrameCounter >= (p.Ai0 < 120f ? 4 : 1))
            {
                p.FrameCounter = 0;
                if (++p.Frame >= 5) p.Frame = 0;
            }
            if (--p.SoundDelay <= 0)
            {
                p.SoundDelay = 20;
                if (p.Ai0 != 1f) Sound("Item_15");
            }
            if (!Channel(def.Type) || (pay && !mod.CheckMana(def)))
            {
                Kill(p);
                return;
            }
            if (p.Ai0 == 1f)
                for (int m = 0; m < 6; m++)
                {
                    var beam = Spawn(ProjKind.LastPrismBeam, TerraParticles.ToWorld(centre + v), Flip(v.normalized), p.Damage, p.Knockback, p.Crit);
                    if (beam == null) continue;
                    beam.Ai0 = m;
                    beam.Parent = p;
                }
            // Put on the player's centre, then (UpdatePosition, which aiStyle 75 keeps) moved by its velocity: 30 px out along the aim.
            p.Pos = TerraParticles.ToWorld(centre + v);
            p.Vel = Flip(v);
            p.TRot = Mathf.Atan2(v.y, v.x) + Mathf.PI / 2f;
            p.Dir = v.x < 0f ? -1f : 1f;
            p.TimeLeft = 2;
            mod.KeepUsing(def, Flip(v));
        }

        /// <summary>Main.DrawProj for 633: its frame, lit, turned along the aim; four faint added copies round it, swaying apart then held apart past 120.</summary>
        static void DrawLastPrism(FxBatch fx, Proj p, Vector2 px)
        {
            var at = new Vector2(Mathf.Floor(px.x), Mathf.Floor(px.y));
            var spr = FxBatch.Frame("Projectile_633", 0, p.Frame * 24, 26, 24, 13f, 12f);
            bool flip = p.Dir < 0f;
            fx.Draw(spr, at, new Color(L, L, L, 1f), p.TRot, p.Scale, flipX: flip, overPlayer: true);
            float r = p.Ai0 > 120f ? 4f : Mathf.Cos(Mathf.PI * 2f * (p.Ai0 / 30f)) * 2f + 2f;
            for (int k = 0; k < 4; k++)
                fx.Draw(spr, at + RotateBy(Vector2.up, k * Mathf.PI / 2f) * r, new Color(L * 0.03f, L * 0.03f, L * 0.03f, 0f), p.TRot, p.Scale, flipX: flip, overPlayer: true);
        }

        // ------------------------------------------------------------------ 632: the Last Prism's beams (AI_084)

        /// <summary>
        /// AI_084 for 632: each beam leaves the prism 16 px out, fanned up to ±30° by a sway that
        /// narrows over 180 updates as the beams close in and grow (to 1.4) and show (to full by
        /// 180); then one beam of triple damage measured from the prism. Its length eases (75%) to
        /// the first block within 2400 px; harmless for the first 30 updates; light and coloured
        /// sparks at its end.
        /// </summary>
        void StepPrismBeam(Proj p)
        {
            var prism = p.Parent;
            if (prism == null || prism.Dead)
            {
                Kill(p);
                return;
            }
            // Main.DrawProj reads the caster's slowMagicUse. Carry it in this
            // kind's unused localAI[0], already included in projectile snapshots,
            // so a remote beam never reads the viewing player's mana state.
            p.LocalAi0 = TerraMod.Instance?.SlowMagicUse == true ? 1f : 0f;
            p.OwnerCooldown = p.LocalAi0 != 0f ? 12 : 5; // Player.GetSlowMagicUseTime(5), truncated.
            float idx = (int)p.Ai0 - 2.5f;
            var pv = TV(prism);
            var dir = pv.sqrMagnitude > 1e-6f ? pv.normalized : new Vector2(0f, -1f);
            float t = prism.Ai0, spread, wobble, back, cycle, opacity;
            if (t < 180f)
            {
                spread = 1f - t / 180f;
                wobble = 20f - t / 180f * 14f;
                if (t < 120f)
                {
                    cycle = 20f - 4f * (t / 120f);
                    opacity = t / 120f * 0.4f;
                }
                else
                {
                    cycle = 16f - 10f * ((t - 120f) / 60f);
                    opacity = 0.4f + (t - 120f) / 60f * 0.6f;
                }
                back = -22f + t / 180f * 20f;
            }
            else
            {
                spread = 0f;
                cycle = 1.75f;
                wobble = 6f;
                opacity = 1f;
                back = -2f;
            }
            float phase = (t + idx * cycle) / (cycle * 6f) * Mathf.PI * 2f;
            // Vector2.UnitY.RotatedBy(phase): (−sin, cos) in px; its Y sways the beam, the vector sways its root.
            var swing = new Vector2(-Mathf.Sin(phase), Mathf.Cos(phase));
            float turn = swing.y * (Mathf.PI / 6f) * spread;
            float prismRot = Mathf.Atan2(dir.y, dir.x);
            var sway = RotateBy(new Vector2(swing.x * 4f, swing.y * wobble), prismRot);
            var prismCentre = CenterPx(prism);
            var c = prismCentre + dir * 16f + dir * back + sway;
            var v = RotateBy(dir, turn);
            p.Pos = TerraParticles.ToWorld(c);
            p.Vel = Flip(v);
            p.Scale = 1.4f * (1f - spread);
            p.Alpha = 1f - opacity;
            p.Damage = prism.Damage * (t >= 180f ? 3 : 1);
            var sample = t >= 180f ? prismCentre : c;
            if (Holder(out var owner) && !InSight(owner, prismCentre)) sample = owner;
            p.TRot = Mathf.Atan2(v.y, v.x) - Mathf.PI / 2f;
            float len = LaserScan(sample, v, 2400f);
            p.LocalAi1 = Mathf.Lerp(p.LocalAi1, len, 0.75f);
            if (Mathf.Abs(p.LocalAi1 - len) >= 100f || p.Scale <= 0.15f) return;
            var colour = Hsl((int)p.Ai0 / 6f, 1f, 0.5f);
            colour.a = 0f;
            var end = c + v * (p.LocalAi1 - 14.5f * p.Scale);
            float disco = Frac(Time.time * 0.25f);
            for (int k = 0; k < 2; k++)
            {
                float a = Mathf.Atan2(v.y, v.x) + (Random.Range(0, 2) == 1 ? -1f : 1f) * Mathf.PI / 2f;
                float sp = Random.value * 0.8f + 1f;
                int i = BoxDust(end, 0, 0, 267, Mathf.Cos(a) * sp, Mathf.Sin(a) * sp);
                if (i < 0) continue;
                D(i).Color = colour;
                D(i).Scale = 1.2f;
                if (p.Scale > 1f)
                {
                    D(i).Velocity *= p.Scale;
                    D(i).Scale *= p.Scale;
                }
                D(i).NoGravity = true;
                if (p.Scale != 1.4f)
                {
                    var copy = D(i);
                    copy.Color = Color.white;
                    copy.Scale /= 2f;
                    var pp = TerraParticles.Instance;
                    int j = pp != null ? pp.NewDust(copy.Position, 0, 0, 267) : -1;
                    if (j >= 0) pp[j] = copy;
                }
                D(i).Color = Color.Lerp(colour, Hsl(Frac(disco + Random.value * 0.4f), 1f, 0.75f), p.Scale / 1.4f);
            }
            if (Random.Range(0, 5) == 0)
            {
                var side = RotateBy(v, Mathf.PI / 2f) * (Random.value - 0.5f) * 18f;
                int i = BoxDust(end + side - Vector2.one * 4f, 8, 8, 31, 0f, 0f, 100, 1.5f);
                if (i >= 0)
                {
                    D(i).Velocity *= 0.5f;
                    D(i).Velocity = new Vector2(D(i).Velocity.x, -Mathf.Abs(D(i).Velocity.y));
                }
            }
        }

        /// <summary>
        /// Main.DrawProj for 632: the beam's sheet laid from 10.5 px out to its end in its colour,
        /// added, three quarters strong; a white core half as wide over it.
        /// </summary>
        internal static Vector4 DevPrismVisual;
        internal static int DevPrismVisualDamage;
        static void DrawPrismBeam(FxBatch fx, Proj p, Vector2 px)
        {
            var v = TV(p);
            if (v == Vector2.zero) return;
            v.Normalize();
            float opacity = 1f - p.Alpha;
            if (opacity <= 0f) return;
            var colour = Hsl((int)p.Ai0 / 6f, 1f, 0.5f);
            colour.a = 0f;
            var start = new Vector2(Mathf.Floor(px.x), Mathf.Floor(px.y)) + v * p.Scale * 10.5f;
            float len = p.LocalAi1 - p.Scale * 14.5f * p.Scale;
            var scale = new Vector2(p.Scale, p.Scale);
            // Main.DrawProj (632): low mana changes only the drawn width and
            // luminance, retaining the beam's length and collision scale.
            if (p.LocalAi0 != 0f) { scale.x *= 0.7f; opacity *= 0.25f; }
            DevPrismVisual = new Vector4(scale.x, opacity, p.Scale, len);
            DevPrismVisualDamage = p.Damage;
            DrawLaser(fx, "Projectile_632", start, start + v * len, scale, colour * 0.75f * opacity);
            DrawLaser(fx, "Projectile_632", start, start + v * len, scale / 2f, FxBatch.C(255, 255, 255, 127) * 0.75f * opacity);
            if (p.Scale > 0.15f) BeamLight(fx, CenterPx(p), v, p.LocalAi1, new Color(colour.r, colour.g, colour.b) * 0.3f);
        }

        // ------------------------------------------------------------------ 460: the Charged Blaster Cannon (AI_075)

        /// <summary>
        /// AI_075 for 460: the charge builds (stage 1 at 80 updates, 2 at 180); it acts every 5
        /// updates (once charged, every 5th) and turns to the cursor every update; it fires an orb at 1 and every 10 up to 50, then one
        /// every 30 from 80 (bigger with the stage, 2.5 times the damage from 1.0); mana at 80, 180
        /// and every 20 after; at 180 the beam (1.5 times the damage), and it goes when the beam
        /// does. A hum quickening with the charge; sparks drawn into its muzzle.
        /// </summary>
        void StepChargedBlaster(Proj p)
        {
            var mod = TerraMod.Instance;
            var def = ItemDb.Get(ItemType.ChargedBlasterCannon);
            if (mod == null || !Holder(out var centre))
            {
                Kill(p);
                return;
            }
            bool tick = MagicTick(p);
            if (tick) p.Ai0++;
            int stage = (p.Ai0 >= 80f ? 1 : 0) + (p.Ai0 >= 180f ? 1 : 0);
            int ai0 = (int)p.Ai0;
            bool pay = tick && (ai0 == 80 || ai0 == 180 || (p.Ai0 > 180f && ai0 % 20 == 0));
            bool full = p.Ai0 >= 180f;
            if (!full && tick) p.Ai1++;
            bool act = tick && (p.Ai0 == 1f || (full && p.Ai0 % 20f == 0f));
            var v = TV(p);
            p.OldVel = p.Vel;
            p.HeldAt = centre;
            if ((!full && p.Ai1 >= 5f) || (full && ai0 % 5 == 0) || v == Vector2.zero)
            {
                if (!full) p.Ai1 = 0f;
                act = true;
            }
            // AI_075 snaps it to the cursor on those updates only, and the beam swept in jumps twelve times a
            // second: it turns 30% of the way there every update instead.
            var aim = AimPx() - centre;
            var want = aim.sqrMagnitude > 1e-6f ? aim.normalized : new Vector2(0f, -1f);
            var cur = v.sqrMagnitude > 1e-6f ? v.normalized : want;
            v = Vector2.Lerp(want, cur, 0.7f).normalized * def.ShootSpeed * p.Scale;
            if (p.SoundDelay > 0) p.SoundDelay--;
            if (p.SoundDelay <= 0 && !full)
            {
                p.SoundDelay = (5 - stage) * 2;
                if (p.Ai0 != 1f) Sound("Item_15");
            }
            float rot = Mathf.Atan2(v.y, v.x) + Mathf.PI / 2f;
            float facing = v.x < 0f ? -1f : 1f;
            // Centre = arm − velocity, then UpdatePosition adds the velocity back: on the hand.
            var c = ArmPx(centre, facing, AimFrame(v));
            if (p.Ai0 > 10f && !full)
            {
                var muzzle = c + RotateBy(Vector2.right * 18f, rot - Mathf.PI / 2f);
                for (int k = 0; k < stage + 1; k++)
                {
                    var at = muzzle + RotateBy(Vector2.right, Random.value * Mathf.PI * 2f) * (12f - stage * 2);
                    int i = BoxDust(at - Vector2.one * 8f, 16, 16, 226, v.x / 2f, v.y / 2f);
                    if (i < 0) continue;
                    D(i).Velocity = (muzzle - at).normalized * 1.5f * (10f - stage * 2f) / 10f + v;
                    D(i).NoGravity = true;
                    D(i).Scale = k % 2 == 1 ? 0.65f : 0.4f;
                }
            }
            if (full)
            {
                var muzzle = c + RotateBy(Vector2.right * 14f, rot - Mathf.PI / 2f);
                for (int k = 0; k < 2; k++)
                {
                    float f = Random.Range(-1f, 1f);
                    var at = muzzle + RotateBy(Vector2.right, rot + f * Mathf.PI / 4f * 0.8f - Mathf.PI / 2f) * 6f;
                    int i = BoxDust(at - Vector2.one * 12f, 24, 24, 226, v.x / 2f, v.y / 2f);
                    if (i < 0) continue;
                    D(i).Velocity = (at - muzzle).normalized * Mathf.Lerp(1.5f, 9f, 1f - Mathf.Abs(f)) + v;
                    D(i).NoGravity = true;
                    D(i).Scale = k % 2 == 1 ? 0.45f : 0.35f;
                    D(i).FadeIn = 0.5f;
                }
            }
            if (act)
            {
                if (!Channel(def.Type) || (pay && !mod.CheckMana(def)))
                {
                    Kill(p);
                    return;
                }
                if (ai0 == 180)
                {
                    var beam = Spawn(ProjKind.ChargedBlasterBeam, TerraParticles.ToWorld(c), Flip(v.normalized), (int)(p.Damage * 1.5f), p.Knockback, p.Crit);
                    if (beam != null) beam.Parent = p;
                }
                else if (full)
                {
                    bool beamOut = false;
                    foreach (var q in live)
                        if (!q.Dead && q.Kind == ProjKind.ChargedBlasterBeam && q.Parent == p) beamOut = true;
                    if (!beamOut)
                    {
                        Kill(p);
                        return;
                    }
                }
                else if (p.Ai0 == 1f || (p.Ai0 <= 50f && ai0 % 10 == 0) || (p.Ai0 >= 80f && p.Ai0 < 180f && ai0 % 30 == 0))
                {
                    float size = 0.7f + stage * 0.3f;
                    var orb = Spawn(ProjKind.ChargedBlasterOrb, TerraParticles.ToWorld(c), Flip(v.normalized * 10f), size < 1f ? p.Damage : (int)(p.Damage * 2.5f), p.Knockback, p.Crit);
                    if (orb != null) orb.Ai1 = size;
                }
            }
            p.Pos = TerraParticles.ToWorld(c);
            p.Vel = Flip(v);
            p.TRot = rot;
            p.Dir = facing;
            p.TimeLeft = 2;
            mod.KeepUsing(def, Flip(v));
        }

        /// <summary>
        /// Where the player's own held cannon is drawn this frame (px) and along what: carried with the player as
        /// he is drawn (his rigidbody draws him between physics steps, the cannon moves on Terraria's updates, and
        /// it shook on him as he ran), its aim part of the way from the last update's to this one's by the time
        /// since (one update behind, never jumping). Another player's as it came.
        /// </summary>
        (Vector2 at, Vector2 dir) HeldDrawn(Proj p, Vector2 px)
        {
            var dir = TV(p);
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : new Vector2(0f, -1f);
            var hero = HeroController.SilentInstance;
            if (ghostHolder.HasValue || hero == null || p.HeldAt == Vector2.zero) return (px, dir);
            var was = Flip(p.OldVel);
            if (was.sqrMagnitude > 1e-6f)
            {
                float a = Mathf.LerpAngle(Mathf.Atan2(was.y, was.x) * Mathf.Rad2Deg, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg, Mathf.Clamp01(tickAccumulator)) * Mathf.Deg2Rad;
                dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            }
            return (px + TerraParticles.ToPx(Player.TerraCamera.HeroCenter(hero)) - p.HeldAt, dir);
        }

        /// <summary>Main.DrawProj for 460: the cannon, lit, turned along the aim; its glow (Glow_102) added over it, flipped when aimed left.</summary>
        void DrawChargedBlaster(FxBatch fx, Proj p, Vector2 px)
        {
            var (drawn, dir) = HeldDrawn(p, px);
            var at = new Vector2(Mathf.Floor(drawn.x), Mathf.Floor(drawn.y));
            float rot = Mathf.Atan2(dir.y, dir.x) + Mathf.PI / 2f;
            bool flip = dir.x < 0f;
            fx.Draw(FxBatch.Frame("Projectile_460", 0, 0, 14, 18, 7f, 9f), at, new Color(L, L, L, 1f), rot, p.Scale, flipX: flip, overPlayer: true);
            bool left = Mathf.Abs(rot - Mathf.PI / 2f) > Mathf.PI / 2f;
            fx.Draw(FxBatch.Frame("Glow_102", 0, 0, 14, 14, 7f, 9f), at, new Color(1f, 1f, 1f, 0f), rot - Mathf.PI / 2f, p.Scale, flipY: left, flipX: flip, overPlayer: true);
        }

        // ------------------------------------------------------------------ 459: the blaster orb (AI_001)

        /// <summary>
        /// P:55615: it shows in fast, three frames; its size (0.7 to 1.6) sets its box (22 px at 1);
        /// from 0.85 up it goes through enemies; cyan sparks behind it; turned along its flight.
        /// </summary>
        static void StepBlasterOrb(Proj p)
        {
            p.Alpha = Mathf.Max(0f, p.Alpha - 30f / 255f);
            var v = TV(p);
            p.Dir = v.x < 0f ? -1f : 1f;
            if (++p.FrameCounter >= 3)
            {
                p.FrameCounter = 0;
                if (++p.Frame >= 3) p.Frame = 0;
            }
            p.Ai1 = Mathf.Clamp(p.Ai1, 0.7f, 1.6f);
            p.Scale = p.Ai1;
            int size = (int)(22f * p.Scale);
            p.HitSize = new Vector2(size, size);
            int n = p.Scale < 0.85f ? (Random.Range(0, 3) == 0 ? 1 : 0) : 1;
            if (p.Scale >= 0.85f) p.Penetrate = p.MaxPenetrate = -1;
            var c = CenterPx(p);
            for (int k = 0; k < n; k++)
            {
                int i = BoxDust(c - Vector2.one * (size * 0.5f), size, size, 226, v.x);
                if (i < 0) continue;
                D(i).Position -= Vector2.one * 3f;
                D(i).Scale = 0.5f;
                D(i).NoGravity = true;
                D(i).Velocity = v / 3f;
                D(i).Alpha = 255 - (int)(255f * p.Scale);
            }
            p.TRot = Mathf.Atan2(v.y, v.x) + (p.Dir < 0f ? Mathf.PI : 0f);
        }

        /// <summary>Main.DrawProj for 459: its last positions, lit, fading and shrinking towards 0.3 (every scale·2 of the first scale·8), then the orb, near white, mostly solid.</summary>
        static void DrawBlasterOrb(FxBatch fx, Proj p, Vector2 px)
        {
            fx.Light(px, new Color(0.4f, 0.85f, 0.9f), 3f);
            var spr = FxBatch.Frame("Projectile_459", 0, p.Frame * 34, 36, 34, 18f, 17f);
            bool flip = p.Dir < 0f;
            float a = 1f - p.Alpha;
            int upTo = (int)(p.Scale * 8f), step = Mathf.Max(1, upTo / 4);
            for (int n = 1; n < upTo && n < p.Trail.Count; n += step)
            {
                float k = (upTo - n) / 15f;
                fx.Draw(spr, TerraParticles.ToPx(p.Trail[n]), new Color(L * k, L * k, L * k, k) * a, p.TRot, Mathf.Lerp(p.Scale, 0.3f, n / 15f), flipX: flip);
            }
            fx.Draw(spr, px, FxBatch.C(255, 255, 255, 200) * a, p.TRot, p.Scale, flipX: flip);
        }

        /// <summary>P:74714: an orb of full size or more bursts over 144 px; smoke and cyan sparks (and smoke clouds for the big burst).</summary>
        void KillBlasterOrb(Proj p)
        {
            int smoke = 3, sparks = 10, gores = 0;
            float size = (int)(22f * p.Scale);
            if (p.Scale >= 1f)
            {
                size = 144f;
                smoke = 7;
                sparks = 30;
                gores = 2;
                Blast(p, 144f, p.Damage, p.Knockback);
            }
            var c = CenterPx(p);
            var tl = c - Vector2.one * size * 0.5f;
            Vector2 Inside() => RotateBy(new Vector2(size / 2f, 0f), Random.value * Mathf.PI * 2f) * Random.value + c;
            for (int k = 0; k < smoke; k++)
            {
                int i = BoxDust(tl, (int)size, (int)size, 31, 0f, 0f, 100, 1.5f);
                if (i >= 0) D(i).Position = Inside();
            }
            for (int k = 0; k < sparks; k++)
            {
                int i = BoxDust(tl, (int)size, (int)size, 226, 0f, 0f, 0, 1.5f);
                if (i < 0) continue;
                D(i).Position = Inside();
                D(i).NoGravity = true;
            }
            for (int k = 0; k < gores; k++)
                TerraParticles.Instance?.NewGore(tl + new Vector2(size * Random.Range(0, 100) / 100f, size * Random.Range(0, 100) / 100f) - Vector2.one * 10f, Vector2.zero, Random.Range(61, 64), 1f, 0.3f,
                    new Vector2(Random.Range(-10, 11) * 0.05f, Random.Range(-10, 11) * 0.05f));
        }

        // ------------------------------------------------------------------ 461: the blaster beam (AI_084)

        /// <summary>
        /// AI_084 for 461: 16 px out of the cannon along its aim, for 300 updates, swelling in and out
        /// (sin·10, at most 1); its length eases (50%) to the first block within 2400 px; cyan sparks
        /// and smoke at its end, cyan light along it.
        /// </summary>
        void StepBlasterBeam(Proj p)
        {
            var cannon = p.Parent;
            if (cannon == null || cannon.Dead)
            {
                Kill(p);
                return;
            }
            var dir = TV(cannon);
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : new Vector2(0f, -1f);
            var c = CenterPx(cannon) + dir * 16f;
            p.Pos = TerraParticles.ToWorld(c);
            p.Vel = Flip(dir);
            if (++p.Ai0 >= 300f)
            {
                Kill(p);
                return;
            }
            p.Scale = Mathf.Min(1f, Mathf.Sin(p.Ai0 * Mathf.PI / 300f) * 10f);
            p.TRot = Mathf.Atan2(dir.y, dir.x) - Mathf.PI / 2f;
            float len = LaserScan(c, dir, 2400f);
            p.LocalAi1 = Mathf.Lerp(p.LocalAi1, len, 0.5f);
            var end = c + dir * (p.LocalAi1 - 8f);
            for (int k = 0; k < 2; k++)
            {
                float a = Mathf.Atan2(dir.y, dir.x) + (Random.Range(0, 2) == 1 ? -1f : 1f) * Mathf.PI / 2f;
                float sp = Random.value * 0.8f + 1f;
                int i = BoxDust(end, 0, 0, 226, Mathf.Cos(a) * sp, Mathf.Sin(a) * sp);
                if (i < 0) continue;
                D(i).NoGravity = true;
                D(i).Scale = 1.2f;
            }
            if (Random.Range(0, 5) == 0)
            {
                var side = RotateBy(dir, Mathf.PI / 2f) * (Random.value - 0.5f) * 18f;
                int i = BoxDust(end + side - Vector2.one * 4f, 8, 8, 31, 0f, 0f, 100, 1.5f);
                if (i >= 0)
                {
                    D(i).Velocity *= 0.5f;
                    D(i).Velocity = new Vector2(D(i).Velocity.x, -Mathf.Abs(D(i).Velocity.y));
                }
            }
        }

        /// <summary>Main.DrawProj for 461: its start at its centre, middles from 10.5 px on, its end; white, added, 90% strong.</summary>
        void DrawBlasterBeam(FxBatch fx, Proj p, Vector2 px)
        {
            var v = TV(p);
            if (v == Vector2.zero) return;
            v.Normalize();
            var from = CenterPx(p);
            // Out of the cannon as it is drawn this frame.
            if (p.Parent != null && !p.Parent.Dead)
            {
                var (cannon, dir) = HeldDrawn(p.Parent, CenterPx(p.Parent));
                v = dir;
                from = cannon + dir * 16f;
                px = from;
            }
            var colour = FxBatch.C(255, 255, 255, 0) * 0.9f;
            var at = new Vector2(Mathf.Floor(px.x), Mathf.Floor(px.y));
            float rot = Mathf.Atan2(v.y, v.x) - Mathf.PI / 2f;
            fx.Draw(FxBatch.Frame("Projectile_461", 0, 0, 26, 22, 13f, 11f), at, colour, rot, p.Scale, overPlayer: true);
            float len = p.LocalAi1 - 33f * p.Scale;
            var pos = at + v * p.Scale * 10.5f;
            if (len > 0f)
            {
                float done = 0f;
                while (done + 1f < len)
                {
                    int h = 28;
                    if (len - done < h) h = (int)(len - done);
                    if (h <= 0) break;
                    fx.Draw(FxBatch.Frame("Projectile_461", 0, 25, 26, h, 13f, 0f), pos, colour, rot, p.Scale, overPlayer: true);
                    done += h * p.Scale;
                    pos += v * h * p.Scale;
                }
            }
            fx.Draw(FxBatch.Frame("Projectile_461", 0, 56, 26, 22, 13f, 0f), pos, colour, rot, p.Scale, overPlayer: true);
            BeamLight(fx, from, v, p.LocalAi1, new Color(0.4f, 0.85f, 0.9f));
        }

        // ------------------------------------------------------------------ 535-536: the Medusa Head (AI_100, AI_101)

        /// <summary>
        /// AI_100 for 535: raised in the hand, facing the cursor; while some enemy within 320 px can
        /// be seen, every 15 updates it pays its mana and its gaze strikes the three nearest (each a
        /// ray, 536, drawn to it) with four stray rays about their mean direction, then glares for
        /// 60 updates (frames 8-11, a hiss as it starts); else it idles (frames 0-7). A gold light.
        /// </summary>
        internal static int DevMedusaTargetChecks;
        void StepMedusaHead(Proj p)
        {
            var mod = TerraMod.Instance;
            var def = ItemDb.Get(ItemType.MedusaHead);
            if (mod == null || !Holder(out var centre))
            {
                Kill(p);
                return;
            }
            // Preserve the original integer division: 15 normally, 37 in slow magic.
            float period = (int)(60f * (mod.SlowMagicUse ? TerraMod.SlowMagic : 1f)) / 4;
            bool glaring = p.Ai0 > 0f;
            if (p.Ai0 > 0f) p.Ai0--;
            if (glaring)
            {
                if (p.Frame < 8 || p.Frame >= 12) p.Frame = 8;
                p.FrameCounter += 2;
                if (p.FrameCounter >= 5)
                {
                    p.FrameCounter = 0;
                    if (++p.Frame >= 12) p.Frame = 8;
                }
            }
            else if (++p.FrameCounter >= 5)
            {
                p.FrameCounter = 0;
                if (++p.Frame >= 8) p.Frame = 0;
            }
            float dx = AimPx().x - centre.x;
            float facing = dx != 0f ? Mathf.Sign(dx) : (mod.UseDirection < 0f ? -1f : 1f);
            if (glaring && p.SoundDelay == 0) Sound("NPC_Killed_17");
            p.SoundDelay = glaring ? 4 : 0;
            if (!Channel(def.Type))
            {
                Kill(p);
                return;
            }
            // Centre = hand + (6, −14) − velocity, and UpdatePosition adds the velocity (facing, 0) back.
            var head = ArmPx(centre, facing, 2) + new Vector2(facing * 6f, -14f);
            if (!glaring || p.Ai0 % period == 0f)
            {
                DevMedusaTargetChecks++;
                var eye = centre + new Vector2(facing * 4f, 2f);
                bool any = false;
                foreach (var hm in EnemiesNear(head, 320f))
                    if (Vector2.Distance(head, EnemyCenterPx(hm)) < 320f && InSight(EnemyCenterPx(hm), eye))
                    {
                        any = true;
                        break;
                    }
                if (any && mod.CheckMana(def))
                {
                    var near = new System.Collections.Generic.List<(HealthManager Hm, float D)>();
                    foreach (var hm in EnemiesNear(head, 320f))
                    {
                        float d = Vector2.Distance(head, EnemyCenterPx(hm));
                        if (d < 320f && InSight(EnemyCenterPx(hm), head)) near.Add((hm, d));
                    }
                    near.Sort((a, b) => a.D.CompareTo(b.D));
                    float sum = 0f;
                    int count = 0;
                    for (int k = 0; k < near.Count && k < 3; k++)
                    {
                        var to = EnemyCenterPx(near[k].Hm) - head;
                        sum += Mathf.Atan2(to.y, to.x);
                        count++;
                        Gaze(p, head, to);
                        StrikeEnemy(p, near[k].Hm, p.Damage, p.Knockback);
                    }
                    float mean = count == 0 ? (facing > 0f ? 0f : Mathf.PI) : sum / count;
                    for (int k = 0; k < 4; k++)
                    {
                        var stray = Random.Range(0, 4) == 0
                            ? RotateBy(Vector2.right, Random.Range(-Mathf.PI, Mathf.PI))
                            : RotateBy(RotateBy(Vector2.right, Random.Range(-Mathf.PI / 2f, Mathf.PI / 2f)), mean);
                        stray = new Vector2(stray.x * 200f, stray.y * 50f) * (Random.value * 0.7f + 0.3f);
                        Gaze(p, head, stray);
                    }
                    p.Ai0 = 60f;
                }
            }
            p.Pos = TerraParticles.ToWorld(new Vector2(Mathf.Floor(head.x), Mathf.Floor(head.y)));
            p.Vel = new Vector2(facing, 0f);
            p.Dir = facing;
            p.TRot = 0f;
            p.TimeLeft = 3;
            mod.KeepUsing(def, new Vector2(facing, 0f), holdUp: true);
        }

        /// <summary>A ray (536) from the head along to (px).</summary>
        void Gaze(Proj head, Vector2 at, Vector2 to)
        {
            var ray = Spawn(ProjKind.MedusaRay, TerraParticles.ToWorld(at), Flip(to), 0, 0f, 0f);
            if (ray != null) ray.Parent = head;
        }

        /// <summary>AI_101 for 536: it stays on the head and fades out over 20 updates.</summary>
        static void StepMedusaRay(Proj p)
        {
            var head = p.Parent;
            if (++p.LocalAi0 >= 20f || head == null || head.Dead)
            {
                p.Dead = true;
                return;
            }
            p.Alpha = p.LocalAi0 / 20f;
            p.Pos = head.Pos;
            p.TRot = Mathf.Atan2(TV(p).y, TV(p).x) + Mathf.PI / 2f;
        }

        /// <summary>Main.DrawProj for 535: its frame, lit, facing its way; its rays over it (536: stretched from the head to their end, white fading, mostly added).</summary>
        void DrawMedusaHead(FxBatch fx, Proj p, Vector2 px)
        {
            fx.Light(px, new Color(0.9f, 0.75f, 0.1f), 3f);
            fx.Draw(FxBatch.Frame("Projectile_535", 0, p.Frame * 48, 48, 48, 24f, 24f), px, new Color(L, L, L, 1f), 0f, p.Scale, flipX: p.Dir < 0f, overPlayer: true);
            foreach (var q in live)
            {
                if (q.Dead || q.Kind != ProjKind.MedusaRay || q.Parent != p || q.LocalAi0 == 0f) continue;
                int a = Mathf.RoundToInt(q.Alpha * 255f);
                var v = TV(q);
                fx.Draw(FxBatch.Frame("Projectile_536", 0, 0, 20, 100, 10f, 100f), px, FxBatch.C(255 - a, 255 - a, 255 - a, 63 - a / 4), q.TRot,
                    new Vector2(1f, v.magnitude / 100f), overPlayer: true);
            }
        }
    }
}
