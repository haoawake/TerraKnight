using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The rest of the hardmode spells (Terraria's space, px y down, per update): the Laser Rifle's
    /// purple laser (88: growing longer, harder and more piercing as it flies), the Bat Scepter's
    /// bats (316, AI_036), the Crystal Vile Shard's crystal spikes (494/493, AI_004 like the
    /// Vilethorn), the Magnet Sphere (254, AI_047: drifting, zapping enemies within 300 px with
    /// unseen bolts, 255, shown as a line of dust), the Rainbow Rod (79: steered like the Magic
    /// Missile, a rainbow behind it) and the Nimbus Rod's cloud (237 to the cursor, then 238 raining
    /// 239 for minutes; three at most).
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        static void RegisterMagic3()
        {
            specs[ProjKind.PurpleLaser] = new Spec
            {
                Ai = Ai.Arrow, Texture = "Projectile_88", W = 6, H = 6, Scale = 0.65f, Time = 600, Extra = 4, Penetrate = 3, NoGravity = true,
                PivotBack = 28.5f, Look = Look.Additive, Light = new Color(0.525f, 0f, 0.75f), Update = (t, p) => GrowLaser(p), OnKill = (t, p) => Sound("Item_10"),
            };
            specs[ProjKind.Bat] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_316", W = 16, H = 16, Time = 600, Penetrate = 1, AlphaStart = 1f,
                Update = (t, p) => t.StepBat(p), Draw = (t, fx, p, px) => DrawBat(fx, p, px), OnKill = (t, p) => KillBat(p),
            };
            void Shard(ProjKind k, int tex) => specs[k] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_" + tex, W = 32, H = 32, Time = 3600, Penetrate = -1, TileCollide = false, AlphaStart = 1f, ArmorPen = 10,
                Light = new Color(0.2f, 0.06f, 0.2f), Update = (t, p) => t.StepCrystalVile(p), Draw = (t, fx, p, px) => DrawCrystalVile(fx, p, px), OnKill = (t, p) => { },
            };
            Shard(ProjKind.CrystalVileShard, 494);
            Shard(ProjKind.CrystalVileTip, 493);
            specs[ProjKind.MagnetSphere] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_254", W = 38, H = 38, Time = 900, Penetrate = -1, TileBox = new Vector2(2f, 2f), CanDamage = p => false,
                Light = new Color(0.05f, 0.5f, 0.5f), Update = (t, p) => t.StepMagnetSphere(p), Draw = (t, fx, p, px) => DrawMagnetSphere(fx, p, px),
                TileHit = (t, p, hx, hy, ov) => Reflect(p, hx, hy, ov), OnKill = (t, p) => { },
            };
            specs[ProjKind.MagnetBolt] = new Spec
            {
                Ai = Ai.Custom, W = 8, H = 8, Time = 100, Extra = 100, Penetrate = 1, Look = Look.Invisible,
                Update = (t, p) => BeamStart(p, 4), OnKill = (t, p) => BeamLine(p, 4),
            };
            specs[ProjKind.RainbowRod] = new Spec
            {
                Ai = Ai.Missile, W = 32, H = 32, Time = 3600, Penetrate = 3, LocalCooldown = 12, TileBox = new Vector2(4f, 4f), TrailLength = 60,
                OnKill = (t, p) => t.KillRainbowRod(p),
            };
            specs[ProjKind.NimbusCloudMoving] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_237", W = 28, H = 28, Time = 3600, Penetrate = -1, TileBox = new Vector2(8f, 8f), CanDamage = p => false,
                Update = (t, p) => t.StepNimbusMoving(p), Draw = (t, fx, p, px) => DrawNimbus(fx, p, px, "Projectile_237"), OnKill = (t, p) => t.KillNimbusMoving(p),
            };
            specs[ProjKind.NimbusCloud] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_238", W = 54, H = 28, Time = CloudLife + 60, Penetrate = -1, TileCollide = false, CanDamage = p => false,
                Update = (t, p) => t.StepNimbusCloud(p), Draw = (t, fx, p, px) => DrawNimbus(fx, p, px, "Projectile_238"), OnKill = (t, p) => { },
            };
            specs[ProjKind.NimbusRain] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_239", W = 4, H = 40, Scale = 1.1f, Time = 300, Extra = 1, Penetrate = 5, TypeCooldown = 10,
                Update = (t, p) => { }, Draw = (t, fx, p, px) => DrawRain(fx, p, px), OnKill = (t, p) => KillRain(p),
            };
        }

        // ------------------------------------------------------------------ 88: Laser Rifle (AI_001)

        /// <summary>P:54979: up to 4 times its size (0.053 an update), its knockback 0.45% more an update; every 8 updates 1-3 more damage and maybe one more pierce.</summary>
        static void GrowLaser(Proj p)
        {
            if (p.Scale >= 4f) return;
            p.Scale = Mathf.Min(4f, p.Scale + 0.053f);
            p.Knockback *= 1.0045f;
            if (++p.LocalAi0 < 8f) return;
            p.LocalAi0 = 0f;
            p.Damage += Random.Range(1, 4);
            p.Penetrate += Random.Range(0, 2);
        }

        // ------------------------------------------------------------------ 316: Bat Scepter (AI_036)

        /// <summary>
        /// AI_036 for 316: fades in fast (50 an update), faces against its flight, wings beat every 3
        /// updates; after 30 updates it turns for the nearest enemy in sight within 800 px
        /// (Manhattan) at 13 px an update (0.325 a step, three times across zero), else flies on at
        /// 10 (0.25).
        /// </summary>
        void StepBat(Proj p)
        {
            p.Alpha = Mathf.Max(0f, p.Alpha - 50f / 255f);
            var v = TV(p);
            if (v.x != 0f) p.Dir = v.x > 0f ? -1f : 1f;
            p.TRot = v.x * 0.1f;
            if (++p.FrameCounter >= 3)
            {
                p.FrameCounter = 0;
                if (++p.Frame >= 3) p.Frame = 0;
            }
            var c = CenterPx(p);
            Vector2 aim = c + v * 100f;
            bool found = false;
            p.Ai0++;
            if (p.Ai0 > 30f)
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
            float speed = found ? 13f : 10f, acc = found ? 0.325f : 0.25f;
            var want = (aim - c).sqrMagnitude > 1e-6f ? (aim - c).normalized * speed : Vector2.zero;
            v.x = Approach(v.x, want.x, acc, 2f);
            v.y = Approach(v.y, want.y, acc, 2f);
            p.Vel = Flip(v);
        }

        static void DrawBat(FxBatch fx, Proj p, Vector2 px)
        {
            fx.Draw(FxBatch.Frame("Projectile_316", 0, p.Frame * 15, 20, 15, 10f, 8f), px + new Vector2(2f, 0f), new Color(L, L, L, 1f) * (1f - p.Alpha), p.TRot, p.Scale, flipX: p.Dir < 0f);
        }

        static void KillBat(Proj p)
        {
            var v = TV(p);
            var tl = CenterPx(p) - new Vector2(8f, 8f);
            for (int k = 0; k < 5; k++)
            {
                int i = BoxDust(tl, 16, 16, 195);
                if (i < 0) continue;
                D(i).Scale = 0.85f;
                D(i).NoGravity = true;
                D(i).Velocity += v * 0.5f;
            }
        }

        // ------------------------------------------------------------------ 494/493: Crystal Vile Shard (AI_004)

        /// <summary>
        /// AI_004 for 494: a chime (Item101) for the first; each spike grows in over 3 updates (100
        /// an update) and sprouts the next 32 px on; after 7 or 8 the tip (493), a quarter harder;
        /// then they fade (4 an update) with a burst of crystal motes.
        /// </summary>
        void StepCrystalVile(Proj p)
        {
            if (p.Ai1 == 0f && p.LocalAi0 == 0f)
            {
                p.LocalAi0 = 1f;
                Sound("Item_101");
            }
            var v = TV(p);
            p.TRot = Mathf.Atan2(v.y, v.x) + 1.57f;
            if (p.Ai0 == 0f)
            {
                p.Alpha = Mathf.Max(0f, p.Alpha - 100f / 255f);
                if (p.Alpha > 0f) return;
                p.Ai0 = 1f;
                if (p.Ai1 == 0f)
                {
                    p.Ai1 = 1f;
                    p.Pos += p.Vel * Px;
                }
                if (p.Kind == ProjKind.CrystalVileShard)
                {
                    bool tip = p.Ai1 >= 7 + Random.Range(0, 2);
                    var next = Spawn(tip ? ProjKind.CrystalVileTip : ProjKind.CrystalVileShard, p.Pos + p.Vel * Px, p.Vel,
                        tip ? (int)(p.Damage * 1.25f) : p.Damage, tip ? p.Knockback * 1.25f : p.Knockback, p.Crit);
                    if (next != null)
                    {
                        next.Ai1 = p.Ai1 + 1f;
                        next.LocalAi0 = 1f;
                        next.Melee = p.Melee;
                    }
                }
                return;
            }
            int alpha = Mathf.RoundToInt(p.Alpha * 255f);
            if (alpha < 170 && alpha + 5 >= 170)
            {
                var tl = CenterPx(p) - new Vector2(16f, 16f);
                for (int k = 0; k < 8; k++)
                {
                    int i = BoxDust(tl, 32, 32, Random.Range(68, 71), v.x * 0.025f, v.y * 0.025f, 200, 1.3f);
                    if (i < 0) continue;
                    D(i).NoGravity = true;
                    D(i).Velocity *= 0.5f;
                }
            }
            alpha += 4;
            p.Alpha = alpha / 255f;
            if (alpha >= 255) Kill(p);
        }

        /// <summary>GetAlpha for 493/494: white, fading as one.</summary>
        static void DrawCrystalVile(FxBatch fx, Proj p, Vector2 px)
        {
            fx.Draw(FxBatch.Frame(p.Spec.Texture, 0, 0, 32, 32, 16f, 16f), px, Color.white * (1f - p.Alpha), p.TRot, p.Scale);
        }

        // ------------------------------------------------------------------ 254/255: Magnet Sphere (AI_047)

        /// <summary>
        /// AI_047_MagnetSphere: it slows to 2 px an update, turning a little, five frames of 7
        /// updates; a newer sphere sends the older ones away (30 updates left). Every 9 updates it
        /// zaps one of the enemies in sight within 300 px (Manhattan) with a bolt at 6 px an update
        /// from 4 of its steps ahead, while it has more than 30 updates to live.
        /// </summary>
        void StepMagnetSphere(Proj p)
        {
            var v = TV(p);
            p.TRot += (v.x > 0f ? 1f : -1f) * (Mathf.Abs(v.x) + Mathf.Abs(v.y)) * 0.001f;
            if (++p.FrameCounter > 6)
            {
                p.FrameCounter = 0;
                if (++p.Frame > 4) p.Frame = 0;
            }
            if (v.magnitude > 2f) p.Vel *= 0.98f;
            foreach (var q in live)
                if (q != p && !q.Dead && q.Kind == ProjKind.MagnetSphere && p.TimeLeft > q.TimeLeft && q.TimeLeft > 30) q.TimeLeft = 30;
            if (p.TimeLeft < 30) return;
            var c = CenterPx(p);
            var near = new System.Collections.Generic.List<Vector2>();
            foreach (var hm in EnemiesNear(c, 300f))
            {
                var e = EnemyCenterPx(hm);
                if (Mathf.Abs(c.x - e.x) + Mathf.Abs(c.y - e.y) < 300f && InSight(c, e) && near.Count < 20) near.Add(e);
            }
            if (near.Count == 0) return;
            if (++p.LocalAi0 <= 8f) return;
            p.LocalAi0 = 0f;
            var at = c + TV(p) * 4f;
            var to = near[Random.Range(0, near.Count)] - at;
            var bolt = Spawn(ProjKind.MagnetBolt, TerraParticles.ToWorld(at), Flip(to.normalized * 6f), p.Damage, p.Knockback, p.Crit);
            if (bolt != null) bolt.Melee = p.Melee;
        }

        /// <summary>Its frame (44 px of 220), 3 px lower than the middle; GetAlpha unlit and added, fading over its last 30 updates.</summary>
        static void DrawMagnetSphere(FxBatch fx, Proj p, Vector2 px)
        {
            float o = p.TimeLeft < 30 ? p.TimeLeft / 30f : 1f;
            fx.Draw(FxBatch.Frame("Projectile_254", 0, p.Frame * 44, 38, 44, 19f, 22f), px, new Color(1f, 1f, 1f, 0f) * o, p.TRot, p.Scale);
        }

        // ------------------------------------------------------------------ 79: Rainbow Rod (AI_009)

        /// <summary>
        /// Projectile.Kill for 79 (P:72426): everyone in a 128 px box takes its damage once; a
        /// rainbow of motes scattered back along its trail, thinning towards the end, and a ring of
        /// coloured sparks where it ended.
        /// </summary>
        void KillRainbowRod(Proj p)
        {
            Blast(p, 128f, p.Damage, p.Knockback);
            Sound("Item_10");
            float h0 = Random.value;
            int len = p.Spec.TrailLength;
            for (int k = 0; k < p.Trail.Count; k++)
            {
                float t = Remap(k, len, 0f, 0f, 1f);
                var col = Color.HSVToRGB((h0 + t) % 1f, 0.7f, 1f);
                int n = Random.Range(1, 4);
                if (k >= len * 0.3f) n--;
                if (k >= len * 0.65f) n -= 2;
                if (k >= len * 0.85f) n -= 3;
                var at = TerraParticles.ToPx(p.Trail[k]) - new Vector2(16f, 16f);
                for (int j = 0; j < n; j++)
                {
                    int i = BoxDust(at, 32, 32, 267);
                    if (i < 0) continue;
                    D(i).Color = col;
                    D(i).Velocity *= Random.value * 0.8f;
                    D(i).NoGravity = true;
                    D(i).Scale = 0.9f + Random.value * 1.2f;
                }
            }
            var tl = CenterPx(p) - new Vector2(16f, 16f);
            for (int k = 0; k < 14; k++)
            {
                int i = BoxDust(tl, 32, 32, 66, 0f, 0f, 100, 1.7f);
                if (i < 0) continue;
                D(i).Color = Color.HSVToRGB(Random.value, 1f, 1f);
                D(i).NoGravity = true;
                D(i).Velocity *= 3f;
            }
        }

        /// <summary>
        /// RainbowRodDrawer: a band up to 32 px wide along its last 60 places, white at the head and
        /// running through the colours behind (hue 1.6 times along the band, sliding with time),
        /// fading to the end, added; the rod's shot itself is unseen (GetAlpha is transparent). Its
        /// light is the disco colour.
        /// </summary>
        static void DrawRainbowRod(FxBatch fx, Proj p, Vector2 px)
        {
            var disco = Color.HSVToRGB(Time.time * 0.25f % 1f, 1f, 1f);
            fx.Light(px, disco * 0.8f, 3f);
            var dot = FxBatch.Whole("Extra_98");
            int len = p.Spec.TrailLength;
            for (int i = 1; i < p.Trail.Count; i++)
            {
                float t = (i - 0.5f) / len;
                float w = Remap(t, 0f, 0.2f, 0f, 1f);
                w = 32f * (1f - (1f - w) * (1f - w));
                if (w <= 0.01f) continue;
                var hue = Color.HSVToRGB(((t * 1.6f - Time.time) % 1f + 1f) % 1f, 1f, 1f);
                var col = Color.Lerp(Color.white, hue, Remap(t, -0.2f, 0.5f, 0f, 1f)) * (1f - Remap(t, 0f, 0.98f, 0f, 1f));
                col.a = 0f;
                var a = TerraParticles.ToPx(p.Trail[i - 1]);
                var b = TerraParticles.ToPx(p.Trail[i]);
                var seg = b - a;
                if (seg.sqrMagnitude < 0.01f) continue;
                fx.Draw(dot, (a + b) / 2f, col * 1.2f, Mathf.Atan2(seg.y, seg.x) + Mathf.PI / 2f, new Vector2(w / 72f * 3f, Mathf.Max(seg.magnitude, 1f) / 72f * 1.6f));
            }
        }

        // ------------------------------------------------------------------ 237/238/239: Nimbus Rod (AI_045)

        /// <summary>A Nimbus Rod cloud on its way to the cursor (world).</summary>
        public void ShootNimbus(Vector2 from, Vector2 velocity, int damage, float knockback, float crit, Vector2 cursor) =>
            ShootCloud(ProjKind.NimbusCloudMoving, from, velocity, damage, knockback, crit, cursor);

        /// <summary>A Nimbus or Crimson Rod cloud on its way to the cursor (world).</summary>
        public void ShootCloud(ProjKind kind, Vector2 from, Vector2 velocity, int damage, float knockback, float crit, Vector2 cursor)
        {
            var p = Spawn(kind, from, velocity, damage, knockback, crit);
            if (p == null) return;
            p.StartPos = cursor;
        }

        /// <summary>AI_045 for 237: each axis stops once past the cursor; with both stopped it becomes the raining cloud there. It turns with its speed, four frames of 5.</summary>
        void StepNimbusMoving(Proj p)
        {
            var target = TerraParticles.ToPx(p.StartPos);
            var c = CenterPx(p);
            var v = TV(p);
            bool sx = false, sy = false;
            if (v.x == 0f || (v.x < 0f && c.x < target.x) || (v.x > 0f && c.x > target.x))
            {
                v.x = 0f;
                sx = true;
            }
            if (v.y == 0f || (v.y < 0f && c.y < target.y) || (v.y > 0f && c.y > target.y))
            {
                v.y = 0f;
                sy = true;
            }
            p.Vel = Flip(v);
            if (sx && sy)
            {
                Kill(p);
                return;
            }
            p.TRot += v.x * 0.02f;
            if (++p.FrameCounter > 4)
            {
                p.FrameCounter = 0;
                if (++p.Frame > 3) p.Frame = 0;
            }
        }

        void KillNimbusMoving(Proj p)
        {
            var c = Spawn(p.Kind == ProjKind.CrimsonCloudMoving ? ProjKind.CrimsonCloud : ProjKind.NimbusCloud, p.Pos, Vector2.zero, p.Damage, p.Knockback, p.Crit);
            if (c != null) c.Melee = p.Melee;
        }

        /// <summary>A raining cloud's life, in updates: 30 seconds (Terraria's 18000, five minutes, outlasts a Hallownest room's fight).</summary>
        const int CloudLife = 1800;

        /// <summary>
        /// AI_045 for 238: rains a drop (239) 5 px an update down from within 14 px of its middle
        /// every 9 updates while nothing solid is within 20 px under it; six frames of 9 (three over
        /// blocks); after <see cref="CloudLife"/> updates it fades out; with more than three, the oldest fades. The
        /// Crimson Rod's (244) rains blood (245) every 11 and keeps only one.
        /// </summary>
        void StepNimbusCloud(Proj p)
        {
            bool crimson = p.Kind == ProjKind.CrimsonCloud;
            var c = CenterPx(p);
            float bottom = c.y + 14f;
            bool open = !(TerraWorld_SolidPx(new Vector2(c.x, bottom)) || TerraWorld_SolidPx(new Vector2(c.x, bottom + 19f)));
            if (++p.FrameCounter > 8)
            {
                p.FrameCounter = 0;
                if (++p.Frame > (open ? 5 : 2)) p.Frame = 0;
            }
            p.Ai1++;
            if (p.Ai1 >= CloudLife)
            {
                p.Alpha += 5f / 255f;
                if (p.Alpha >= 1f)
                {
                    p.Alpha = 1f;
                    Kill(p);
                    return;
                }
            }
            else if (open)
            {
                p.Ai0++;
                if (p.Ai0 > (crimson ? 10f : 8f))
                {
                    p.Ai0 = 0f;
                    var drop = Spawn(crimson ? ProjKind.BloodRain : ProjKind.NimbusRain, TerraParticles.ToWorld(new Vector2(c.x + Random.Range(-14, 15), bottom + 20f)), new Vector2(0f, -5f), p.Damage, 0f, p.Crit);
                    if (drop != null) drop.Melee = p.Melee;
                }
            }
            if (++p.LocalAi0 < 10f) return;
            p.LocalAi0 = 0f;
            Proj oldest = null;
            int n = 0;
            foreach (var q in live)
            {
                if (q.Dead || q.Kind != p.Kind || q.Ai1 >= 18000f) continue;
                n++;
                if (oldest == null || q.Ai1 > oldest.Ai1) oldest = q;
            }
            if (n > (crimson ? 1 : 2) && oldest != null) oldest.Ai1 = 18000f;
        }

        static bool TerraWorld_SolidPx(Vector2 px) => World.TerraWorld.IsSolidAt(TerraParticles.ToWorld(px));

        static void DrawNimbus(FxBatch fx, Proj p, Vector2 px, string tex)
        {
            bool moving = tex == "Projectile_237" || tex == "Projectile_243";
            int w = moving ? 28 : 54, fh = tex == "Projectile_244" ? 26 : 28;
            fx.Draw(FxBatch.Frame(tex, 0, p.Frame * fh, w, fh, w * 0.5f, 14f), px, new Color(L, L, L, 1f) * (1f - p.Alpha), moving ? p.TRot : 0f, p.Scale);
        }

        /// <summary>A rain drop (alpha 50) or drop of blood (alpha 100, 30 px), straight down.</summary>
        static void DrawRain(FxBatch fx, Proj p, Vector2 px)
        {
            if (p.Kind == ProjKind.BloodRain) fx.Draw(FxBatch.Frame("Projectile_245", 0, 0, 2, 30, 1f, 20f), px, new Color(L, L, L, 1f) * (155f / 255f), 0f, p.Scale);
            else fx.Draw(FxBatch.Frame("Projectile_239", 0, 0, 2, 40, 1f, 20f), px, new Color(L, L, L, 1f) * (205f / 255f), 0f, p.Scale);
        }

        /// <summary>P:75965-75992: a splash where it lands, if still falling (blood's floats, 114).</summary>
        static void KillRain(Proj p)
        {
            if (p.Vel.y >= 0f) return;
            var c = CenterPx(p);
            bool blood = p.Kind == ProjKind.BloodRain;
            int i = BoxDust(new Vector2(c.x - 2f, c.y + 20f * p.Scale - 2f), 2, 2, blood ? 114 : 154);
            if (i < 0) return;
            D(i).Alpha = 38;
            D(i).Velocity = D(i).Velocity * 0.1f - TV(p) * 0.25f;
            D(i).Scale = 0.95f;
            if (blood) D(i).NoGravity = true;
        }
    }
}
