using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The guns' and bows' own shots (sweep R4a; Terraria's space, px y down, per update): the Onyx
    /// Blaster's dark blast (661), the Xenopopper's bubbles turning into its bullets (444), Aerial
    /// Bane's splitting arrows (710), the Bee's Knees' bee arrows (469), the Hellwing's bats (485),
    /// the Toxikarp's bubbles (523), the Pew-matic Horn's things (968), the Star Cannon's star (955),
    /// the Super Star Shooter's star (728) and the Harpoon (23, AI_013 on its chain).
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        public static int DevXenoBullets, DevBetsyShrapnel, DevBeeArrowBees, DevOnyxBlasts;

        /// <summary>A star falling from the night sky from a point, its velocity in px a tick (y down); it hurts nothing.</summary>
        public void SkyStar(Vector2 from, Vector2 velYDown)
        {
            var keep = SpawnClass;
            SpawnClass = DamageClass.None;
            Spawn(ProjKind.FallingStar, from, Flip(velYDown), 1000, 10f, 0f);
            SpawnClass = keep;
        }

        /// <summary>The sky's stars falling now.</summary>
        public int SkyStars()
        {
            int n = 0;
            foreach (var q in live) if (q.Kind == ProjKind.FallingStar && !q.Dead) n++;
            return n;
        }

        static void RegisterGuns()
        {
            // P:7110: the dark blast, AI_001 without gravity, 20 ticks, each enemy once, bursting over 160 px.
            specs[ProjKind.OnyxBlast] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_661", W = 10, H = 10, Time = 40, Extra = 1, AlphaStart = 1f, Penetrate = 1, OncePerNpc = true,
                Update = (t, p) => StepOnyxBlast(p), Draw = (t, fx, p, px) => DrawOnyxBlast(fx, p, px), OnKill = (t, p) => t.KillOnyxBlast(p),
            };
            // P:5152, 18367: the bubble, AI_078, off walls, 30 ticks at most.
            specs[ProjKind.XenoBubble] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_444", W = 10, H = 10, Time = 3600, Extra = 1, AlphaStart = 1f, Penetrate = 1,
                Update = (t, p) => StepXenoBubble(p), Draw = (t, fx, p, px) => DrawCentredLit(fx, p, px), OnKill = (t, p) => t.KillXenoBubble(p),
                TileHit = (t, p, hx, hy, before) =>
                {
                    if (hx) p.Vel.x = -before.x;
                    if (hy) p.Vel.y = -before.y;
                },
            };
            // P:7523, 13047, 14186: Aerial Bane's arrow, 5 s, each enemy once, half again as hard with no ground under it.
            specs[ProjKind.BetsyArrow] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_710", W = 10, H = 10, Time = 300, Extra = 1, AlphaStart = 1f, Penetrate = 1, OncePerNpc = true, TrailLength = 10,
                Update = (t, p) => StepBetsyArrow(p), Draw = (t, fx, p, px) => DrawBetsyArrow(fx, p, px), OnHit = (t, p, hm) => t.BetsySharpnel(p, hm),
                OnKill = (t, p) => t.KillBetsyArrow(p), ModifyHit = (t, p, hm, dealt, crit) => (Airborne(p) ? (int)(dealt * 1.5f) : dealt, crit),
            };
            // P:5437: the bee arrow, straight on, fading in.
            specs[ProjKind.BeeArrow] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_469", W = 10, H = 10, Time = 1200, AlphaStart = 1f, Penetrate = 1,
                Update = (t, p) => StepBeeArrow(p), Draw = (t, fx, p, px) => DrawGeneric(fx, p, px, new Color(L, L, L, 1f), p.Dir < 0f), OnKill = (t, p) => t.KillBeeArrow(p),
            };
            // P:5606: the Hellwing's bat, through everything, five frames.
            specs[ProjKind.HellwingBat] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_485", W = 24, H = 24, Time = 1200, Penetrate = -1, Frames = 5,
                Update = (t, p) => StepHellwingBat(p), Draw = (t, fx, p, px) => DrawHellwingBat(fx, p, px), OnKill = (t, p) => KillHellwingBat(p),
            };
            // P:5931, 11485, 20073: the toxic bubble, AI_095, poisoning 10-29 s; a 10 px box against blocks.
            specs[ProjKind.ToxicBubble] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_523", W = 32, H = 32, TileBox = new Vector2(10f, 10f), Time = 3600, AlphaStart = 1f, Penetrate = 1,
                Update = (t, p) => StepToxicBubble(p), Draw = (t, fx, p, px) => DrawCentredLit(fx, p, px), OnKill = (t, p) => KillToxicBubble(p),
                OnHit = (t, p, hm) => EnemyDebuffs.Add(hm, EnemyDebuffs.Poisoned, 60 * Random.Range(10, 30)),
            };
            // P:9162, 14540: the Pew-matic Horn's thing, AI_002, one of 24 frames, each hitting its own way hard.
            specs[ProjKind.PewMaticNote] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_968", W = 14, H = 14, Time = 3600, AlphaStart = 1f, Penetrate = 1, Frames = 24,
                Update = (t, p) => StepPewMatic(p), Draw = (t, fx, p, px) => DrawPewMatic(fx, p, px),
                OnKill = (t, p) =>
                {
                    HitTileDust(p);
                    Sound("Item_10");
                },
                ModifyHit = (t, p, hm, dealt, crit) => ((int)(dealt * PewMaticScale[Mathf.Clamp(p.Frame, 0, 23)]), crit),
            };
            // P:9015: the Star Cannon's star, AI_005, through everything, solid once out of the blocks it starts in.
            specs[ProjKind.StarCannonStar] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_955", W = 18, H = 18, Time = 3600, Penetrate = -1, AlphaStart = 50f / 255f, TileCollide = false,
                Light = new Color(0.9f, 0.9f, 0.9f), Update = (t, p) => t.StepCannonStar(p), Draw = (t, fx, p, px) => DrawFallingStar(fx, p, px),
                OnKill = (t, p) => KillFallingStar(p),
            };
            // P:926-939, 24224-24238: the star falling from the night sky, AI_005 as the Star Cannon's, its light
            // 0.9 × (0.9, 0.8, 0.1), hurting nothing here; where it lands lies a Fallen Star (P:79348), at daybreak
            // it comes down at once (P:24093). See World.FallingStars.
            specs[ProjKind.FallingStar] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_12", W = 18, H = 18, Time = 3600, Penetrate = -1, AlphaStart = 50f / 255f, TileCollide = false,
                Light = new Color(0.81f, 0.72f, 0.09f), CanDamage = p => false,
                Update = (t, p) => t.StepSkyStar(p), Draw = (t, fx, p, px) => DrawFallingStar(fx, p, px),
                OnKill = (t, p) =>
                {
                    KillFallingStar(p);
                    WorldItems.Drop(ItemStack.Of(ItemType.FallenStar, 1), p.Pos, fromSky: true);
                },
            };
            // P:7661: the Super Star Shooter's star, AI_151, through everything, each enemy once.
            specs[ProjKind.SuperStar] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_728", W = 24, H = 24, Time = 3600, Penetrate = -1, OncePerNpc = true, AlphaStart = 1f,
                Update = (t, p) => StepSuperStar(p), Draw = (t, fx, p, px) => DrawFallingStar(fx, p, px), OnKill = (t, p) => KillFallingStar(p),
            };
            // P:1059: the Harpoon, AI_013, three enemies then back (bleeding 8-12 s), each enemy every 12 ticks; one out at a time.
            specs[ProjKind.HarpoonProj] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_23", W = 4, H = 4, Time = 3600, Penetrate = -1, LocalCooldown = 12, AlphaStart = 1f, Held = true,
                Update = (t, p) => t.StepHarpoon(p), Draw = (t, fx, p, px) => DrawHarpoon(fx, p, px), OnHit = (t, p, hm) => HarpoonHit(p, hm),
                TileHit = (t, p, hx, hy, before) =>
                {
                    p.Vel = Vector2.zero;
                    ChainHeadTile(p);
                },
                OnKill = (t, p) => { },
            };
        }

        // ------------------------------------------------------------------ 661: the Onyx Blaster's blast

        /// <summary>
        /// P:56699-56738: turned along its flight; while it shows (55 a step, at 1.3 size) nothing,
        /// then a ring of 16 dark motes (62) stretched four times along it; after that three wisps
        /// (240) an update.
        /// </summary>
        static void StepOnyxBlast(Proj p)
        {
            var v = TV(p);
            p.TRot = ToRot(v) + 1.57f;
            var c = CenterPx(p);
            if (p.Alpha <= 0f)
                for (int k = 0; k < 3; k++)
                {
                    int i = BoxDust(c - new Vector2(5f, 5f), 10, 10, 240);
                    if (i < 0) continue;
                    D(i).NoGravity = true;
                    D(i).Velocity *= 0.3f;
                }
            if (p.Alpha > 0f)
            {
                p.Alpha -= 55f / 255f;
                p.Scale = 1.3f;
                if (p.Alpha <= 0f)
                {
                    p.Alpha = 0f;
                    float a = ToRot(v);
                    for (int k = 0; k < 16; k++)
                    {
                        var off = RotateBy(new Vector2(0f, -1f), k * Mathf.PI * 2f / 16f);
                        off = RotateBy(new Vector2(off.x, off.y * 4f), a);
                        int i = BoxDust(c, 0, 0, 62, 0f, 0f, 0, 1.5f);
                        if (i < 0) continue;
                        D(i).NoGravity = true;
                        D(i).Position = c + off;
                        D(i).Velocity = D(i).Velocity * 4f + v * 0.3f;
                    }
                }
            }
        }

        /// <summary>M:31429-31434: four purple glows (Extra_75, 120 40 222 120) 4 px out round it, then itself, lit.</summary>
        static void DrawOnyxBlast(FxBatch fx, Proj p, Vector2 px)
        {
            var whole = FxBatch.Whole(p.Spec.Texture);
            if (whole == null) return;
            float tw = whole.rect.width, th = whole.rect.height;
            var glow = FxBatch.Frame("Extra_75", 0, 0, (int)tw, (int)th, tw / 2f, th / 2f);
            var along = new Vector2(Mathf.Cos(p.TRot), Mathf.Sin(p.TRot));
            for (int k = 0; k < 4; k++) fx.Draw(glow, px + RotateBy(along, Mathf.PI / 2f * k) * 4f, FxBatch.C(120, 40, 222, 120), p.TRot, p.Scale);
            fx.Draw(whole, px, new Color(L, L, L, 1f) * (1f - p.Alpha), p.TRot, p.Scale);
        }

        /// <summary>
        /// P:73493-73560: the boom (Item14) and a 160 px blast, then dark smoke (240, 62) in a
        /// 20 px disc and a ring of it thrown out along its flight, some crimson.
        /// </summary>
        void KillOnyxBlast(Proj p)
        {
            Blast(p, 160f, p.Damage, p.Knockback);
            DevOnyxBlasts++;
            Sound("Item_14");
            var c = CenterPx(p);
            var tl = c - new Vector2(20f, 20f);
            float a = ToRot(TV(p));
            Vector2 Disc() => c + RotateBy(new Vector2(0f, 1f), Random.Range(-Mathf.PI, Mathf.PI)) * Random.value * 20f;
            Vector2 Ring() => c + RotateBy(RotateBy(Vector2.right, Random.Range(-Mathf.PI, Mathf.PI)), a) * 20f;
            Vector2 Out(Vector2 at) => (at - c).sqrMagnitude > 1e-6f ? (at - c).normalized : Vector2.zero;
            for (int k = 0; k < 4; k++)
            {
                int i = BoxDust(tl, 40, 40, 240, 0f, 0f, 100, 1.5f);
                if (i >= 0) D(i).Position = Disc();
            }
            for (int k = 0; k < 20; k++)
            {
                int i = BoxDust(tl, 40, 40, 62, 0f, 0f, 200, 3.7f);
                if (i >= 0)
                {
                    D(i).Position = Disc();
                    D(i).NoGravity = true;
                    D(i).Velocity = D(i).Velocity * 3f + Out(D(i).Position) * (2f + Random.value * 4f);
                }
                i = BoxDust(tl, 40, 40, 62, 0f, 0f, 100, 1.5f);
                if (i < 0) continue;
                D(i).Position = Disc();
                D(i).Velocity = D(i).Velocity * 2f + Out(D(i).Position) * 8f;
                D(i).NoGravity = true;
                D(i).FadeIn = 1f;
                D(i).Color = new Color(220f / 255f, 20f / 255f, 60f / 255f, 1f) * 0.5f;
            }
            for (int k = 0; k < 20; k++)
            {
                int i = BoxDust(tl, 40, 40, 62, 0f, 0f, 0, 2.7f);
                if (i < 0) continue;
                D(i).Position = Ring();
                D(i).NoGravity = true;
                D(i).Velocity = D(i).Velocity * 3f + Out(D(i).Position) * 2f;
            }
            for (int k = 0; k < 70; k++)
            {
                int i = BoxDust(tl, 40, 40, 240, 0f, 0f, 0, 1.5f);
                if (i < 0) continue;
                D(i).Position = Ring();
                D(i).NoGravity = true;
                D(i).Velocity = D(i).Velocity * 3f + Out(D(i).Position) * 3f;
            }
        }

        // ------------------------------------------------------------------ 444: the Xenopopper's bubble

        /// <summary>
        /// The Xenopopper's bubbles (Pl:48022-48044): ai0 the aim (y down), the bullet it carries and that
        /// bullet's speed.
        /// </summary>
        public void ShootXenoBubble(Vector2 from, Vector2 velocity, int damage, float knockback, float crit, float aim, ProjKind carried, float speed)
        {
            var p = Spawn(ProjKind.XenoBubble, from, velocity, damage, knockback, crit);
            if (p == null) return;
            p.Ai0 = aim;
            p.LocalAi0 = (float)(int)carried;
            p.LocalAi1 = speed;
        }

        /// <summary>AI_078 (P:31294-31324): showing 30 an update; turning a twentieth of the way to the aim and slowing 4% an update; 60 updates at most.</summary>
        static void StepXenoBubble(Proj p)
        {
            p.Alpha = Mathf.Max(0f, p.Alpha - 30f / 255f);
            var v = TV(p);
            float diff = Mathf.DeltaAngle(ToRot(v) * Mathf.Rad2Deg, p.Ai0 * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            v = RotateBy(v, diff * 0.05f) * 0.96f;
            p.TRot = ToRot(v) - Mathf.PI / 2f;
            if (p.TimeLeft > 60) p.TimeLeft = 60;
            p.Vel = Flip(v);
        }

        /// <summary>P:74778-74800: a pop (Item96), green motes (171), and the bullet it carried, at the cursor.</summary>
        void KillXenoBubble(Proj p)
        {
            Sound("Item_96");
            var c = CenterPx(p);
            for (int k = Random.Range(5, 9); k > 0; k--)
            {
                int i = BoxDust(c, 0, 0, 171, 0f, 0f, 100, 1.4f);
                if (i < 0) continue;
                D(i).Velocity *= 0.8f;
                D(i).Position = Vector2.Lerp(D(i).Position, c, 0.5f);
                D(i).NoGravity = true;
            }
            var to = AimPx() - c;
            to = to.sqrMagnitude > 1e-6f ? to.normalized : new Vector2(p.Dir, 0f);
            var bullet = Spawn((ProjKind)(int)p.LocalAi0, p.Pos, Flip(to * p.LocalAi1), p.Damage, p.Knockback, p.Crit);
            if (bullet == null) return;
            bullet.Class = p.Class;
            DevXenoBullets++;
        }

        // ------------------------------------------------------------------ 710: Aerial Bane's arrow

        /// <summary>
        /// P:56349-56366, 58532-58547: showing 25 an update; now and then smoke (31) and a flame (6)
        /// thrown its way; after 20 updates falling 0.2 (to 12), slowing 2% on the way down.
        /// </summary>
        static void StepBetsyArrow(Proj p)
        {
            var v = TV(p);
            p.Alpha = Mathf.Max(0f, p.Alpha - 25f / 255f);
            var tl = CenterPx(p) - new Vector2(5f, 5f);
            if (Random.Range(0, 4) == 0)
            {
                int i;
                if (Random.Range(0, 2) == 0 && (i = BoxDust(tl, 10, 10, 31, 0f, 0f, 100, 1.6f)) >= 0) D(i).NoGravity = true;
                i = BoxDust(tl, 10, 10, 6, 0f, 0f, 100, 1.2f);
                if (i >= 0)
                {
                    D(i).NoGravity = true;
                    D(i).Velocity = D(i).Velocity * 2f + v;
                    D(i).FadeIn = p.Ai1 == -1f ? 1.5f : 1.22f;
                }
            }
            if (p.Ai0 >= 0f && ++p.Ai0 >= 20f)
            {
                v.y += 0.2f;
                if (v.y > 0f) v.x *= 0.98f;
                if (v.y > 12f) v.y = 12f;
            }
            p.TRot = ToRot(v) + 1.57f;
            p.Vel = Flip(v);
        }

        /// <summary>P:13047: nothing solid within 12 blocks under it.</summary>
        static bool Airborne(Proj p) => !Physics2D.Raycast(p.Pos, Vector2.down, 12f * 16f * Px, 1 << World.ChunkColliders.TerrainLayer);

        /// <summary>BetsySharpnel (P:54912-54928): five more arrows fanned down a quarter turn either side, that don't split again, sparing who it hit.</summary>
        void BetsySharpnel(Proj p, HealthManager hm)
        {
            if (p.Ai1 == -1f) return;
            const float span = Mathf.PI / 4f;
            float step = -span * 2f / 4f;
            for (int k = 0; k < 5; k++)
            {
                var q = Spawn(ProjKind.BetsyArrow, p.Pos, Flip(RotateBy(new Vector2(0f, 6f), span + step * k)), p.Damage, p.Knockback, p.Crit);
                if (q == null) continue;
                q.Ai1 = -1f;
                q.Class = p.Class;
                foreach (var kv in p.Immune) q.Immune[kv.Key] = kv.Value;
                if (hm != null) q.Immune[hm.GetInstanceID()] = int.MaxValue;
                DevBetsyShrapnel++;
            }
        }

        /// <summary>P:72858-72886: a 40 px blast (Item14), smoke (31) and flames (6) in it, a ring of smoke along its flight.</summary>
        void KillBetsyArrow(Proj p)
        {
            p.Ai1 = -1f;
            Blast(p, 40f, p.Damage, p.Knockback);
            Sound("Item_14");
            var c = CenterPx(p);
            var tl = c - new Vector2(20f, 20f);
            Vector2 Disc() => c + RotateBy(new Vector2(0f, 1f), Random.Range(-Mathf.PI, Mathf.PI)) * Random.value * 20f;
            for (int k = 0; k < 2; k++)
            {
                int i = BoxDust(tl, 40, 40, 31, 0f, 0f, 100, 1.5f);
                if (i >= 0) D(i).Position = Disc();
            }
            for (int k = 0; k < 10; k++)
            {
                int i = BoxDust(tl, 40, 40, 6, 0f, 0f, 0, 2.5f);
                if (i < 0) continue;
                D(i).Position = Disc();
                D(i).NoGravity = true;
                D(i).Velocity *= 2f;
            }
            float a = ToRot(TV(p));
            for (int k = 0; k < 5; k++)
            {
                int i = BoxDust(tl, 40, 40, 31, 0f, 0f, 0, 1.5f);
                if (i < 0) continue;
                D(i).Position = c + RotateBy(RotateBy(Vector2.right, Random.Range(-Mathf.PI, Mathf.PI)), a) * 20f;
                D(i).NoGravity = true;
                D(i).Velocity *= 2f;
            }
        }

        /// <summary>
        /// M:30711, 31037, 31315: four copies at its old places 7, 5, 3, 1, more of them and redder the
        /// further back ((9 - i) / 15, blue, green and alpha over i), toward half size by the 15th;
        /// then itself in full white, pivoting 5 px below its tip.
        /// </summary>
        static void DrawBetsyArrow(FxBatch fx, Proj p, Vector2 px)
        {
            var whole = FxBatch.Whole(p.Spec.Texture);
            if (whole == null) return;
            float tw = whole.rect.width, th = whole.rect.height;
            var spr = FxBatch.Frame(p.Spec.Texture, 0, 0, (int)tw, (int)th, tw / 2f, 5f);
            for (int i = 7; i >= 1; i -= 2)
            {
                if (i >= p.Trail.Count) continue;
                float k = (9 - i) / 15f;
                fx.Draw(spr, TerraParticles.ToPx(p.Trail[i]), new Color(L * k, L / i * k, L / i * k, k / i), p.TRot, Mathf.Lerp(p.Scale, 0.5f, i / 15f));
            }
            float a = 1f - p.Alpha;
            fx.Draw(spr, px, new Color(a, a, a, a), p.TRot, p.Scale);
        }

        // ------------------------------------------------------------------ 469: the bee arrow

        /// <summary>P:56164-56174, 58653-58664: showing 50 an update after two; pointing along its flight, mirrored going right.</summary>
        static void StepBeeArrow(Proj p)
        {
            if (++p.LocalAi1 > 2f) p.Alpha = Mathf.Max(0f, p.Alpha - 50f / 255f);
            var v = TV(p);
            p.Dir = v.x > 0f ? -1f : 1f;
            p.TRot = ToRot(v) + 1.57f;
        }

        /// <summary>
        /// P:78066-78083: up to six bees (every other one only one time in three) strung back along its
        /// path 8 px apart, a sixth of its speed on, at a third of its damage, two enemies each.
        /// </summary>
        void KillBeeArrow(Proj p)
        {
            var v = TV(p);
            var back = v.sqrMagnitude > 1e-6f ? v.normalized * 8f : Vector2.zero;
            var keep = SpawnClass;
            SpawnClass = DamageClass.Ranged;
            var tl = CenterPx(p) - new Vector2(5f, 5f);
            for (int k = 0; k < 6; k++)
            {
                if (k % 2 == 1 && Random.Range(0, 3) != 0) continue;
                var at = tl - back * k;
                var bv = new Vector2(Random.Range(-35, 36) * 0.01f + v.x / 6f, Random.Range(-35, 36) * 0.01f + v.y / 6f);
                bool strong = Equipment.Current.StrongBees && Random.Range(0, 2) == 0;
                int dmg = p.Damage / 3 + (strong ? Random.Range(1, 4) : Random.Range(0, 2));
                var bee = Spawn(strong ? ProjKind.GiantBee : ProjKind.Bee, TerraParticles.ToWorld(at + new Vector2(4f, 4f)), Flip(bv), dmg, strong ? 0.5f : 0f, p.Crit);
                if (bee == null) continue;
                bee.Penetrate = 2;
                DevBeeArrowBees++;
            }
            SpawnClass = keep;
        }

        // ------------------------------------------------------------------ 485: the Hellwing's bat

        /// <summary>
        /// The Hellwing's shot (Pl:47990-47997): velocity a little off, and the way it bends toward
        /// (ai0, ai1, y down) a little more off.
        /// </summary>
        public void ShootHellwing(ProjKind kind, Vector2 from, Vector2 velocity, int damage, float knockback, float crit, Vector2 bendTo)
        {
            var p = Spawn(kind, from, velocity, damage, knockback, crit);
            if (p == null) return;
            p.Ai0 = bendTo.x;
            p.Ai1 = bendTo.y;
        }

        /// <summary>
        /// P:58689-58716: bending toward (ai0, ai1) by a fortieth an update, a flame (6) on it, five
        /// frames two updates each, pointing along its flight (mirrored going left).
        /// </summary>
        static void StepHellwingBat(Proj p)
        {
            var v = (TV(p) * 39f + new Vector2(p.Ai0, p.Ai1)) / 40f;
            var c = CenterPx(p);
            int i = BoxDust(c - new Vector2(12f, 12f), 24, 24, 6);
            if (i >= 0)
            {
                D(i).NoGravity = true;
                D(i).Velocity *= 0.2f;
                D(i).Position = (D(i).Position + c) / 2f;
            }
            if (++p.FrameCounter >= 2)
            {
                p.FrameCounter = 0;
                if (++p.Frame >= 5) p.Frame = 0;
            }
            if (v.x < 0f)
            {
                p.Dir = -1f;
                p.TRot = Mathf.Atan2(-v.y, -v.x);
            }
            else
            {
                p.Dir = 1f;
                p.TRot = Mathf.Atan2(v.y, v.x);
            }
            p.Vel = Flip(v);
        }

        /// <summary>P:80964: its frame about its middle, near full white (255 255 255 200), mirrored going left.</summary>
        static void DrawHellwingBat(FxBatch fx, Proj p, Vector2 px)
        {
            var whole = FxBatch.Whole(p.Spec.Texture);
            if (whole == null) return;
            int tw = (int)whole.rect.width, fh = (int)whole.rect.height / 5;
            fx.Draw(FxBatch.Frame(p.Spec.Texture, 0, fh * p.Frame, tw, fh, tw / 2f, fh / 2f), px, FxBatch.C(255, 255, 255, 200), p.TRot, p.Scale, flipX: p.Dir < 0f);
        }

        /// <summary>P:74494-74502: fifteen flames (6) thrown back against its flight.</summary>
        static void KillHellwingBat(Proj p)
        {
            var v = TV(p);
            var tl = CenterPx(p) - new Vector2(12f, 12f);
            for (int k = 0; k < 15; k++)
            {
                int i = BoxDust(tl, 24, 24, 6);
                if (i < 0) continue;
                D(i).NoGravity = true;
                D(i).Velocity -= v * Random.Range(20, 60) * 0.01f;
            }
        }

        // ------------------------------------------------------------------ 523: the Toxikarp's bubble

        /// <summary>The Toxikarp's bubble (Pl:48889-48900): ai0 starts 0-19, rising when it reaches 30.</summary>
        public void ShootToxicBubble(Vector2 from, Vector2 velocity, int damage, float knockback, float crit, int ai0)
        {
            var p = Spawn(ProjKind.ToxicBubble, from, velocity, damage, knockback, crit);
            if (p != null) p.Ai0 = ai0;
        }

        /// <summary>
        /// AI_095 (P:33573-33602): showing 20 an update (to 100) after two; after ai0 reaches 30
        /// (it starts 0-19) rising 0.05 (to 8) and slowing 2%; rocking with its speed.
        /// </summary>
        static void StepToxicBubble(Proj p)
        {
            if (p.LocalAi0 > 2f) p.Alpha = Mathf.Max(100f / 255f, p.Alpha - 20f / 255f);
            else p.LocalAi0++;
            var v = TV(p);
            if (p.Ai0 > 30f)
            {
                if (v.y > -8f) v.y -= 0.05f;
                v.x *= 0.98f;
            }
            else p.Ai0++;
            p.TRot = v.x * 0.1f;
            p.Vel = Flip(v);
        }

        /// <summary>P:74623-74636: a pop (Item54) and 25 toxic motes (256) sprayed every way.</summary>
        static void KillToxicBubble(Proj p)
        {
            Sound("Item_54");
            var c = CenterPx(p);
            var tl = c - new Vector2(16f, 16f);
            for (int k = 0; k < 25; k++)
            {
                int i = BoxDust(tl, 32, 32, 256);
                if (i < 0) continue;
                D(i).NoGravity = true;
                D(i).Position = (D(i).Position + tl) / 2f;
                var d = new Vector2(Random.Range(-100, 101), Random.Range(-100, 101));
                D(i).Velocity = (d.sqrMagnitude > 0f ? d.normalized : Vector2.up) * Random.Range(1, 30) * 0.1f;
                D(i).Alpha = (int)(p.Alpha * 255f);
            }
        }

        // ------------------------------------------------------------------ 968: the Pew-matic Horn's things

        /// <summary>P:14540-14590: each of the 24 things' damage.</summary>
        static readonly float[] PewMaticScale = { 1.5f, 1f, 1.5f, 0.1f, 1f, 1f, 1f, 0.25f, 1f, 0.75f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 0.5f, 1.5f, 1.6f, 1.8f, 1.9f, 1.7f, 2f, 1.9f };

        public void ShootPewMatic(Vector2 from, Vector2 velocity, int damage, float knockback, float crit, int frame)
        {
            var p = Spawn(ProjKind.PewMaticNote, from, velocity, damage, knockback, crit);
            if (p != null) p.Ai1 = p.Frame = frame;
        }

        /// <summary>
        /// AI_002 for 968 (P:23454-23474, 23908-23935): a puff of smoke (31) out of the horn as it
        /// goes, thrown with the player's run; showing 50 an update; spinning with its speed; after
        /// 20 updates falling 0.4 (to 32) and slowing 3%.
        /// </summary>
        static void StepPewMatic(Proj p)
        {
            var v = TV(p);
            if (p.LocalAi0 == 0f)
            {
                p.LocalAi0 = 1f;
                var hero = HeroController.SilentInstance;
                var pv = hero != null ? Flip(hero.GetComponent<Rigidbody2D>().linearVelocity / Px / 60f) : Vector2.zero;
                float boost = Mathf.Lerp(0f, 0.5f, Mathf.Clamp01(pv.magnitude / 3f));
                var at = CenterPx(p) - new Vector2(8f, 8f) + (v.sqrMagnitude > 1e-6f ? v.normalized * 25f : Vector2.zero);
                for (int k = 0; k < 8; k++)
                {
                    int i = BoxDust(at, 16, 16, 31, v.x, v.y, 50, 0.75f + Random.value * 0.3f);
                    if (i < 0) continue;
                    D(i).Velocity = (RotateBy(v * (0.2f + boost), Random.Range(-0.35f, 0.35f)) + pv * 0.25f) * Random.value;
                    D(i).FadeIn = 0.1f;
                    D(i).Position -= D(i).Velocity;
                    D(i).NoGravity = true;
                }
            }
            p.Alpha = Mathf.Max(0f, p.Alpha - 50f / 255f);
            p.Frame = (int)p.Ai1;
            p.TRot += (Mathf.Abs(v.x) + Mathf.Abs(v.y)) * 0.03f * p.Dir;
            if (++p.Ai0 >= 20f)
            {
                v.y += 0.4f;
                v.x *= 0.97f;
            }
            if (v.y > 32f) v.y = 32f;
            p.Vel = Flip(v);
        }

        /// <summary>M:33741-34005: its frame about its middle, lit.</summary>
        static void DrawPewMatic(FxBatch fx, Proj p, Vector2 px)
        {
            var whole = FxBatch.Whole(p.Spec.Texture);
            if (whole == null) return;
            int tw = (int)whole.rect.width, fh = (int)whole.rect.height / 24;
            fx.Draw(FxBatch.Frame(p.Spec.Texture, 0, fh * p.Frame, tw, fh, tw / 2f, fh / 2f), px, new Color(L, L, L, 1f) * (1f - p.Alpha), p.TRot, p.Scale);
        }

        /// <summary>Collision.HitTiles: a few chips of what it struck (the dirt's, here).</summary>
        static void HitTileDust(Proj p)
        {
            var c = CenterPx(p);
            int w = p.Spec.W;
            for (int k = 0; k < 5; k++) BoxDust(c - new Vector2(w / 2f, w / 2f), w, w, 0);
        }

        // ------------------------------------------------------------------ 955 / 728: the cannons' stars

        /// <summary>
        /// AI_005 for 955 (P:24082-24240): solid once out of the blocks it started in; a twinkle
        /// (Item9) every 20-60 updates; pulsing between solid and 200; spinning with its speed;
        /// star gores (16, 17) and now and then a sparkle (58).
        /// </summary>
        void StepCannonStar(Proj p)
        {
            if (p.Ai1 == 0f && !Blocked(p.Pos, TileBox(p) * 0.5f * Px, false, p)) p.Ai1 = 1f;
            if (p.Ai1 != 0f) p.Collide = true;
            StarTwinkle(p);
            if (p.LocalAi0 == 0f) p.LocalAi0 = 1f;
            float alpha = p.Alpha * 255f + 25f * p.LocalAi0;
            if (alpha > 200f)
            {
                alpha = 200f;
                p.LocalAi0 = -1f;
            }
            if (alpha < 0f)
            {
                alpha = 0f;
                p.LocalAi0 = 1f;
            }
            p.Alpha = alpha / 255f;
            var v = TV(p);
            p.TRot += (Mathf.Abs(v.x) + Mathf.Abs(v.y)) * 0.01f * p.Dir;
            var tl = CenterPx(p) - new Vector2(9f, 9f);
            if (Random.Range(0, 6) == 0) TerraParticles.Instance?.NewGore(tl, v * 0.2f, Random.Range(0, 4) == 0 ? 16 : 17);
            if (Random.Range(0, 20) == 0) BoxDust(tl, 18, 18, 58, v.x * 0.5f, v.y * 0.5f, 150, 1.2f);
        }

        /// <summary>Item9 every 20-60 updates (soundDelay).</summary>
        static void StarTwinkle(Proj p)
        {
            if (p.SoundDelay > 0)
            {
                p.SoundDelay--;
                return;
            }
            p.SoundDelay = 20 + Random.Range(0, 40);
            Sound("Item_9", Heard(p));
        }

        /// <summary>
        /// How loud a star is heard: the sky's by its distance from the view's middle (ActiveSound.DetermineIntendedVolume:
        /// 1 - distance / 2500 px), the player's own at full.
        /// </summary>
        static float Heard(Proj p)
        {
            var cam = Camera.main;
            if (p.Kind != ProjKind.FallingStar || cam == null) return 1f;
            return Mathf.Clamp01(1f - Vector2.Distance(p.Pos, cam.transform.position) / (2500f * Px));
        }

        /// <summary>
        /// The sky's star: AI_005 as the Star Cannon's. Out of the room's sides or below its floor it is gone; at
        /// daybreak it comes down where it is (P:24093, Kill, so the Fallen Star is dropped and vanishes in the light).
        /// </summary>
        void StepSkyStar(Proj p)
        {
            if (!FallingStars.InRoom(p.Pos))
            {
                p.Dead = true;
                return;
            }
            if (TerraTime.IsDay)
            {
                Kill(p);
                return;
            }
            StepCannonStar(p);
        }

        /// <summary>
        /// AI_151 (P:22338-22368): showing 10 an update (to 100); twinkling; spinning half as fast; star
        /// gores, and two golden sparkles (228) off either side of its flight now and then.
        /// </summary>
        static void StepSuperStar(Proj p)
        {
            p.Alpha = Mathf.Max(100f / 255f, p.Alpha - 10f / 255f);
            StarTwinkle(p);
            var v = TV(p);
            p.TRot += (Mathf.Abs(v.x) + Mathf.Abs(v.y)) * 0.005f * p.Dir;
            var tl = CenterPx(p) - new Vector2(12f, 12f);
            if (Random.Range(0, 6) == 0) TerraParticles.Instance?.NewGore(tl, v * 0.2f, Random.Range(0, 4) == 0 ? 16 : 17);
            for (int k = 0; k < 2; k++)
            {
                if (Random.Range(0, 8) != 0) continue;
                int i = BoxDust(tl, 24, 24, 228, 0f, 0f, 127);
                if (i < 0) continue;
                D(i).Velocity = D(i).Velocity * 0.25f + RotateBy(v, Mathf.PI / 8f * (1f - 2 * k)) * 0.2f;
                D(i).Scale = 1.3f;
                D(i).NoGravity = true;
            }
        }

        /// <summary>
        /// M:31752-31800: three comet streaks (Extra_91) circling 10 px about a step ahead of it
        /// (blue; orange for 728), two pulsing glows half a step behind, and the star (solid but
        /// for its alpha; 728 added) a tenth larger.
        /// </summary>
        static void DrawFallingStar(FxBatch fx, Proj p, Vector2 px)
        {
            bool super = p.Kind == ProjKind.SuperStar;
            var v = TV(p);
            float ang = Mathf.Atan2(v.y, v.x) + Mathf.PI / 2f;
            var comet = FxBatch.Frame("Extra_91", 0, 0, 34, 90, 17f, 10f);
            var streak = super ? new Color(0.2f, 0.13f, 0f, 0f) : new Color(0f, 0f, 0.2f, 0f);
            var glow = super ? new Color(0.5f, 0.42f, 0f, 50f / 255f) : new Color(0.5f, 0.5f, 0.5f, 0f);
            float less = super ? -0.2f : 0f, T = Time.time;
            float[] sizes = { 1.5f, 1.1f, 1.3f };
            for (int k = 0; k < 3; k++)
                fx.Draw(comet, px + v + RotateBy(new Vector2(0f, -10f), Mathf.PI * 2f * T + k * Mathf.PI * 2f / 3f), streak, ang, sizes[k] + less);
            for (float k2 = 0f; k2 < 1f; k2 += 0.5f)
            {
                float u = Frac(T % 0.5f / 0.5f + k2);
                float tri = u * 2f;
                if (tri > 1f) tri = 2f - tri;
                fx.Draw(comet, px - v * 0.5f, glow * tri, ang, 0.3f + u * 0.5f);
            }
            float o = 1f - p.Alpha;
            var star = super ? new Color(o, o, o, 0f) : new Color(1f, 1f, 1f, o);
            fx.Draw(FxBatch.Whole(p.Spec.Texture), px, star, p.TRot, p.Scale + 0.1f);
        }

        /// <summary>
        /// P:77260-77290, 76659-76685: a chime (Item10), sparkles (58), eight cornflower-blue and four
        /// golden streaks (278) flung out, seven star gores.
        /// </summary>
        static void KillFallingStar(Proj p)
        {
            Sound("Item_10", Heard(p));
            var v = TV(p);
            int w = p.Spec.W;
            var c = CenterPx(p);
            var tl = c - new Vector2(w / 2f, w / 2f);
            for (int k = 0; k < 7; k++) BoxDust(tl, w, w, 58, v.x * 0.1f, v.y * 0.1f, 150, 0.8f);
            var blue = new Color(100f / 255f, 149f / 255f, 237f / 255f, 1f);
            var gold = new Color(1f, 215f / 255f, 0f, 1f);
            for (float k = 0f; k < 1f; k += 0.125f)
            {
                int i = PointDust(c, 278, RotateBy(Vector2.up, k * Mathf.PI * 2f + Random.value * 0.5f) * (4f + Random.value * 4f), 150);
                if (i < 0) continue;
                D(i).Color = blue;
                D(i).NoGravity = true;
            }
            for (float k = 0f; k < 1f; k += 0.25f)
            {
                int i = PointDust(c, 278, RotateBy(Vector2.up, k * Mathf.PI * 2f + Random.value * 0.5f) * (2f + Random.value * 3f), 150);
                if (i < 0) continue;
                D(i).Color = gold;
                D(i).NoGravity = true;
            }
            var pp = TerraParticles.Instance;
            if (pp == null) return;
            for (int k = 0; k < 7; k++)
                pp.NewGore(tl, Random.insideUnitCircle.normalized * 0.5f * v.magnitude, Random.Range(0, 8) == 0 ? 16 : 17);
        }

        // ------------------------------------------------------------------ 23: the Harpoon

        /// <summary>
        /// AI_013 for 23 (P:24968-25125): back when the button's let go after 10 updates, or 1000 px
        /// out; shown after 5 updates; after 17 falling 0.6 and slowing half a percent; the gun
        /// pointed at it on the way out. Back at 20 px an update through everything, gone within 50.
        /// The chain's sway (M:30030-30040) eases toward its flight.
        /// </summary>
        void StepHarpoon(Proj p)
        {
            if (!Holder(out var centre))
            {
                Kill(p);
                return;
            }
            var toPlayer = centre - CenterPx(p);
            float dist = toPlayer.magnitude;
            var mod = TerraMod.Instance;
            var v = TV(p);
            if (p.Ai1 > 10f && mod != null && !mod.UseHeld) p.Ai0 = 1f;
            if (p.Ai0 == 0f)
            {
                if (dist > 1000f) p.Ai0 = 1f;
                p.TRot = ToRot(v) + 1.57f;
                if (++p.Ai1 > 5f) p.Alpha = 0f;
                if (p.Ai1 >= 17f)
                {
                    p.Ai1 = 17f;
                    v.y += 0.6f;
                    v.x *= 0.995f;
                }
                if (mod != null) mod.KeepUsing(ItemDb.Get(ItemType.Harpoon), Flip(-toPlayer));
                p.LocalAi0 = (-v.x + p.LocalAi0) / 2f;
                p.LocalAi1 = (-v.y + p.LocalAi1) / 2f;
            }
            else
            {
                p.Collide = false;
                p.TRot = Mathf.Atan2(toPlayer.y, toPlayer.x) - 1.57f;
                if (dist < 50f)
                {
                    Kill(p);
                    return;
                }
                v = toPlayer / dist * 20f;
                p.LocalAi0 = (v.x + p.LocalAi0 * 4f) / 5f;
                p.LocalAi1 = (v.y + p.LocalAi1 * 4f) / 5f;
            }
            p.Vel = Flip(v);
        }

        /// <summary>P:11291, 14213-14223: bleeding 8-12 s; on its third enemy it stops dead (if still going out) and comes back, hitting all the way.</summary>
        static void HarpoonHit(Proj p, HealthManager hm)
        {
            EnemyDebuffs.Add(hm, EnemyDebuffs.Bleeding, 60 * Random.Range(8, 13));
            if (p.Timer2 != 0f || ++p.Hits < 3) return;
            p.Timer2 = 1f;
            if (p.Ai0 == 0f) p.Vel = Vector2.zero;
            p.Ai0 = 1f;
        }

        /// <summary>
        /// Main.DrawProj for aiStyle 13 (M:30003-30080): the chain (Chain) from 40 px behind the head
        /// (ahead on the way back) to 20 px out from the player, a link every 12 px stopping 25 px
        /// short, each turned partly along the sway (up to half, by a thousandth a px); then the head.
        /// </summary>
        static void DrawHarpoon(FxBatch fx, Proj p, Vector2 px)
        {
            var link = FxBatch.Whole("Chain");
            if (link != null && Holder(out var player))
            {
                var v = TV(p);
                var at = px + new Vector2(6f, 0f);
                if (v.sqrMagnitude > 1e-6f) at += (p.Ai0 == 0f ? -v : v) * (40f / v.magnitude);
                var toward = at - player;
                if (toward.magnitude > 20f) player += toward.normalized * 20f;
                var d = player - at;
                if (d.sqrMagnitude > 1e-6f) at -= d.normalized * 6f;
                d = player - at;
                float rot = Mathf.Atan2(d.y, d.x) - 1.57f;
                var lit = new Color(L, L, L, 1f);
                var sway = new Vector2(p.LocalAi0, p.LocalAi1);
                for (int g = 0; g < 400; g++)
                {
                    float len = d.magnitude;
                    if (len < 25f || float.IsNaN(len)) break;
                    at += d / len * 12f;
                    d = player - at;
                    if (len > 12f && sway.sqrMagnitude > 1e-6f)
                    {
                        float k = Mathf.Min(0.5f, len / 1000f);
                        float l2 = d.magnitude;
                        var blend = d.normalized * l2 * (1f - k) + sway.normalized * l2 * k;
                        rot = Mathf.Atan2(blend.y, blend.x) - 1.57f;
                    }
                    fx.Draw(link, at, lit, rot, 1f);
                }
            }
            DrawGeneric(fx, p, px, new Color(L, L, L, 1f));
        }
    }
}
