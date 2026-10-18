using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Sweep R5a's spells (Terraria's space, px y down, per update): the Crimson Rod's cloud (243,
    /// then 244 raining blood, 245; one at a time), the Flower of Frost's bouncing frost ball (253,
    /// AI_008), the Bee Gun's bees and the Wasp Gun's wasps (181/566, 189, AI_036), the Nettle
    /// Burst's vine (150-152, AI_004), the Rainbow Gun's rainbow (an unseen head 250 laying 251), the
    /// Staff of Earth's boulder (261, AI_014) and the Razorpine's needles (336, AI_001).
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        public static int DevBoulderBursts, DevRainbowSegments;

        static void RegisterMagic5()
        {
            // P:3257-3290: the Crimson Rod's cloud on its way (243), raining (244, 54×28) and its blood (245: through two, 10 tick shared immunity).
            specs[ProjKind.CrimsonCloudMoving] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_243", W = 28, H = 28, Time = 3600, Penetrate = -1, TileBox = new Vector2(8f, 8f), CanDamage = p => false,
                Update = (t, p) => t.StepNimbusMoving(p), Draw = (t, fx, p, px) => DrawNimbus(fx, p, px, "Projectile_243"), OnKill = (t, p) => t.KillNimbusMoving(p),
            };
            specs[ProjKind.CrimsonCloud] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_244", W = 54, H = 28, Time = CloudLife + 60, Penetrate = -1, TileCollide = false, CanDamage = p => false,
                Update = (t, p) => t.StepNimbusCloud(p), Draw = (t, fx, p, px) => DrawNimbus(fx, p, px, "Projectile_244"), OnKill = (t, p) => { },
            };
            specs[ProjKind.BloodRain] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_245", W = 4, H = 40, Scale = 1.1f, Time = 300, Extra = 1, Penetrate = 2, TypeCooldown = 10,
                Update = (t, p) => { }, Draw = (t, fx, p, px) => DrawRain(fx, p, px), OnKill = (t, p) => KillRain(p),
            };
            // P:3360-3370: the frost ball, a Ball of Fire in frost (dust 135; P:16692 its light: no red, 40% green).
            specs[ProjKind.FrostBall] = new Spec
            {
                Ai = Ai.Fireball, Texture = "Projectile_253", W = 16, H = 16, Time = 3600, Penetrate = 1, Dust = 135, Look = Look.NearlyAdded, Light = new Color(0f, 0.32f, 0.8f),
                OnHit = (t, p, hm) => EnemyDebuffs.Add(hm, EnemyDebuffs.Frostbite, 480), OnKill = (t, p) => KillFrostBall(p),
            };
            // P:2766-2780: the wasp, a quicker bee (through three, AP 10, 10 tick shared immunity); 4 frames of 14.
            specs[ProjKind.Wasp] = new Spec
            {
                Ai = Ai.Bee, Texture = "Projectile_189", W = 8, H = 8, Time = 600, Penetrate = 3, Extra = 3, AlphaStart = 1f, TypeCooldown = 10, ArmorPen = 10,
                Frames = 4, Origin = new Vector2(7f, 4f), DrawOffset = new Vector2(3f, 0f), OnKill = (t, p) => KillBee(p),
            };
            // P:2400-2412: the Nettle Burst's vine, two kinds of stem and the tip (AP 10).
            void Nettle(ProjKind k, int tex) => specs[k] = new Spec
            {
                Ai = Ai.Vine, Texture = "Projectile_" + tex, W = 28, H = 28, Time = 3600, Penetrate = -1, TileCollide = false, AlphaStart = 1f, ArmorPen = 10,
                Origin = new Vector2(17f, 14f), DrawOffset = new Vector2(3f, 0f), OnKill = (t, p) => { },
            };
            Nettle(ProjKind.NettleStem, 150);
            Nettle(ProjKind.NettleStem2, 151);
            Nettle(ProjKind.NettleTip, 152);
            // P:3324-3350: the rainbow's head (unseen, harmless, 6 px against blocks) and its pieces (through everything, 10 tick shared immunity).
            specs[ProjKind.RainbowHead] = new Spec
            {
                Ai = Ai.Custom, W = 12, H = 12, Scale = 1.25f, Time = 2400, Penetrate = -1, TileBox = new Vector2(6f, 6f), Look = Look.Invisible, CanDamage = p => false,
                Update = (t, p) => t.StepRainbowHead(p), Draw = (t, fx, p, px) => { }, OnKill = (t, p) => { },
            };
            specs[ProjKind.RainbowPiece] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_251", W = 14, H = 14, Scale = 1.25f, Time = 2400, Penetrate = -1, TileCollide = false, TypeCooldown = 10, AlphaStart = 1f,
                Update = (t, p) => StepRainbowPiece(p), Draw = (t, fx, p, px) => DrawRainbowPiece(fx, p, px), OnKill = (t, p) => { },
            };
            // P:3454-3464: the boulder (through six, 26 px against blocks); harmless below 1.5 px a tick, half as hard below 3.5 (P:12463, 13622).
            specs[ProjKind.EarthBoulder] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_261", W = 32, H = 34, Time = 3600, Extra = 1, Penetrate = 6, TileBox = new Vector2(26f, 26f), TrailLength = 20,
                CanDamage = p => p.Vel.magnitude >= 1.5f, KnockbackScale = p => p.Vel.magnitude < 3.5f ? 0.5f : 1f,
                ModifyHit = (t, p, hm, dealt, crit) => (p.Vel.magnitude < 3.5f ? dealt / 2 : dealt, crit),
                Update = (t, p) => t.StepBoulder(p), TileHit = (t, p, hx, hy, ov) => t.BoulderTile(p, hx, hy, ov), Draw = (t, fx, p, px) => DrawBoulder(fx, p, px),
                OnKill = (t, p) => KillBoulder(p),
            };
            // P:4289-4298, 58098-58104: the pine needle, falling 0.5 px a tick once 50 updates old (it starts up to 10 per place in its volley older).
            specs[ProjKind.PineNeedle] = new Spec
            {
                Ai = Ai.Arrow, Texture = "Projectile_336", W = 4, H = 4, Scale = 0.8f, Time = 3600, Extra = 1, PivotBack = 14f, GravityDelay = 50, Gravity = 0.5f,
                OnKill = (t, p) => KillPineNeedle(p),
            };
        }

        // ------------------------------------------------------------------ 253: Flower of Frost

        /// <summary>P:77774-77786: Item10 and 20 pairs of frost dust, half floating, flung back.</summary>
        static void KillFrostBall(Proj p)
        {
            Sound("Item_10");
            var tl = CenterPx(p) - new Vector2(8f, 8f);
            var back = -TV(p) * 0.2f;
            for (int k = 0; k < 20; k++)
            {
                int i = BoxDust(tl, 16, 16, 135, back.x, back.y, 100, 2f);
                if (i >= 0)
                {
                    D(i).NoGravity = true;
                    D(i).Velocity *= 2f;
                }
                i = BoxDust(tl, 16, 16, 135, back.x, back.y, 100);
                if (i >= 0) D(i).Velocity *= 2f;
            }
        }

        // ------------------------------------------------------------------ 181/566, 189: Bee Gun, Wasp Gun

        /// <summary>
        /// Pl:48781-48818: the Bee Gun's 1-3 bees (one more in six, twice over, and one in three more
        /// with the Hive Pack, its bees half the time giant: beeType/beeDamage/beeKB), the Wasp Gun's
        /// 2-4 wasps (5 harder with the Hive Pack, P:12900); each up to 0.7 px off either way.
        /// </summary>
        public void ShootBeeGun(Vector2 from, Vector2 velocity, int damage, float knockback, float crit, bool wasp)
        {
            bool strongBees = Equipment.Current.StrongBees;
            int n;
            if (wasp) n = Random.Range(2, 5);
            else
            {
                n = Random.Range(1, 4);
                if (Random.Range(0, 6) == 0) n++;
                if (Random.Range(0, 6) == 0) n++;
                if (strongBees && Random.Range(0, 3) == 0) n++;
            }
            for (int k = 0; k < n; k++)
            {
                var v = velocity + new Vector2(Random.Range(-35, 36), Random.Range(-35, 36)) * 0.02f;
                if (wasp)
                {
                    Spawn(ProjKind.Wasp, from, v, damage + (strongBees ? 5 : 0), knockback, crit);
                    continue;
                }
                bool strong = strongBees && Random.Range(0, 2) == 0;
                if (strong) Spawn(ProjKind.GiantBee, from, v, damage + Random.Range(1, 4), 0.5f + knockback * 1.1f, crit);
                else Spawn(ProjKind.Bee, from, v, damage + Random.Range(0, 2), knockback, crit);
            }
        }

        // ------------------------------------------------------------------ 150-152: Nettle Burst (AI_004)

        /// <summary>
        /// AI_004 for the nettle (P:23954-24073): the first stem is the other kind half the time; each
        /// comes in 75 a tick, then lays the next (the stems taking turns, the tip after the tenth),
        /// and fades 3 a tick with a puff of green (7) on the way.
        /// </summary>
        void StepNettle(Proj p)
        {
            if (p.Ai1 == 0f && !p.Spent)
            {
                p.Spent = true;
                Sound("Item_8");
            }
            p.Rot = Angle(p.Vel);
            if (p.Ai0 == 0f)
            {
                if (p.Ai1 == 0f && p.Alpha >= 1f && p.Kind != ProjKind.NettleTip && Random.Range(0, 2) == 0)
                {
                    p.Kind = p.Kind == ProjKind.NettleStem ? ProjKind.NettleStem2 : ProjKind.NettleTip;
                    p.Spec = specs[p.Kind];
                }
                p.Alpha = (Mathf.Round(p.Alpha * 255f) - 75f) / 255f;
                if (p.Alpha > 0f) return;
                p.Alpha = 0f;
                p.Ai0 = 1f;
                if (p.Ai1 == 0f)
                {
                    p.Ai1 = 1f;
                    p.Pos += p.Vel * Px;
                }
                if (p.Kind == ProjKind.NettleStem || p.Kind == ProjKind.NettleStem2)
                {
                    var kind = p.Kind == ProjKind.NettleStem ? ProjKind.NettleStem2 : ProjKind.NettleStem;
                    if (p.Ai1 >= 10f && p.Kind == ProjKind.NettleStem2) kind = ProjKind.NettleTip;
                    var next = Spawn(kind, p.Pos + p.Vel * Px, p.Vel, p.Damage, p.Knockback, p.Crit);
                    if (next != null)
                    {
                        next.Ai1 = p.Ai1 + 1f;
                        next.Spent = true;
                    }
                }
                return;
            }
            int alpha = Mathf.RoundToInt(p.Alpha * 255f);
            if (alpha < 170 && alpha + 5 >= 170)
                for (int k = 0; k < 8; k++)
                {
                    int i = DustBox(p, 7, Flip(p.Vel) * 0.025f, 200, 1.3f, true);
                    if (i >= 0) D(i).Velocity *= 0.5f;
                }
            alpha += 3;
            p.Alpha = alpha / 255f;
            if (alpha >= 255) Kill(p);
        }

        // ------------------------------------------------------------------ 250/251: Rainbow Gun (AI_046)

        /// <summary>Pl:47716-47725: a new rainbow takes the old one away.</summary>
        public void ShootRainbow(Vector2 from, Vector2 velocity, int damage, float knockback, float crit)
        {
            for (int i = 0; i < live.Count; i++)
            {
                var q = live[i];
                if (!q.Dead && (q.Kind == ProjKind.RainbowHead || q.Kind == ProjKind.RainbowPiece)) Kill(q);
            }
            Spawn(ProjKind.RainbowHead, from, velocity, damage, knockback, crit);
        }

        /// <summary>
        /// AI_046 for 250 (P:28520-28566): a piece of rainbow (251) where it is every other update
        /// from the fourth; always 15.95 × 1.25 px a tick, it arcs over once its count (1 an update,
        /// more going up) passes 30: pulled down 0.5, slowing 5% falling or speeding 5% rising.
        /// </summary>
        void StepRainbowHead(Proj p)
        {
            if (++p.LocalAi0 > 4f)
            {
                p.LocalAi0 = 3f;
                var piece = Spawn(ProjKind.RainbowPiece, p.Pos, p.Vel * 0.001f, p.Damage, p.Knockback, p.Crit);
                if (piece != null) DevRainbowSegments++;
            }
            var v = TV(p);
            float add = 1f;
            if (v.y < 0f) add -= v.y / 3f;
            p.Ai0 += add;
            if (p.Ai0 > 30f)
            {
                v.y += 0.5f;
                v.x *= v.y > 0f ? 0.95f : 1.05f;
            }
            float len = v.magnitude;
            if (len > 1e-6f) v *= 15.95f * p.Scale / len;
            p.Vel = Flip(v);
        }

        /// <summary>
        /// AI_046 for 251 (P:28568-28600): turned along its way once (mirrored going right), it drifts
        /// to a stop (0.98 an update); it fades in over 10 updates and out over its last 10 of 2400.
        /// </summary>
        static void StepRainbowPiece(Proj p)
        {
            if (p.LocalAi0 == 0f)
            {
                var v0 = TV(p);
                p.Dir = v0.x > 0f ? -1f : 1f;
                p.TRot = Mathf.Atan2(v0.y, v0.x) - 1.57f;
                p.LocalAi0 = 1f;
                p.TimeLeft = 2400;
            }
            p.Vel *= 0.98f;
            float alpha;
            if (p.TRot == 0f) alpha = 255f;
            else if (p.TimeLeft < 10) alpha = 255f - (int)(255f * p.TimeLeft / 10f);
            else if (p.TimeLeft > 2390) alpha = 255f - (int)(255f * (2400 - p.TimeLeft) / 10f);
            else alpha = 0f;
            p.Alpha = alpha / 255f;
        }

        /// <summary>
        /// M:30331-30334 and GetAlpha (P:81319): drawn added in full, 18 px further down its origin
        /// and 10 px back; P:16727: lit in the shifting disco colour, 0.3 bright.
        /// </summary>
        static void DrawRainbowPiece(FxBatch fx, Proj p, Vector2 px)
        {
            float k = 1f - p.Alpha;
            fx.Draw(FxBatch.Frame("Projectile_251", 0, 0, 32, 32, 16f, 7f + 18f), px + new Vector2(-1f, 0f), new Color(k, k, k, 0f), p.TRot, p.Scale, flipX: p.Dir < 0f);
            var disco = Color.HSVToRGB(Time.time * 0.25f % 1f, 1f, 1f);
            fx.Light(px, new Color((disco.r + 1f) * 0.5f * 0.3f, (disco.g + 1f) * 0.5f * 0.3f, (disco.b + 1f) * 0.5f * 0.3f), 2.5f);
        }

        // ------------------------------------------------------------------ 261: Staff of Earth (AI_014)

        /// <summary>
        /// AI_014 for 261 (P:25290-25345): it rolls with its speed (a little more in the air); from its
        /// 16th update it falls 0.2 px a tick, slowing 3% an update while rolling on the ground and
        /// stopping dead at 0.01; smoke (31) and sparks (228) shed for 80 updates after it's made or
        /// bursts, more at first.
        /// </summary>
        void StepBoulder(Proj p)
        {
            var v = TV(p);
            if (p.LocalAi1 == 0f)
            {
                p.LocalAi1 = 1f;
                p.LocalAi0 = 80f;
            }
            if (v.x != 0f) p.Dir = v.x < 0f ? -1f : 1f;
            p.TRot += v.x * 0.05f;
            if (v.y != 0f) p.TRot += p.Dir * 0.01f;
            if (++p.Ai0 > 15f)
            {
                if (v.y == 0f && v.x != 0f)
                {
                    v.x *= 0.97f;
                    if (Mathf.Abs(v.x) <= 0.01f)
                    {
                        Kill(p);
                        return;
                    }
                }
                p.Ai0 = 15f;
                v.y = Mathf.Min(v.y + 0.2f, 16f);
            }
            p.Vel = Flip(v);
            if (p.LocalAi0 <= 0f) return;
            p.LocalAi0--;
            int max = p.LocalAi0 < 10f ? 15 : p.LocalAi0 < 20f ? 10 : 5;
            var c = CenterPx(p);
            if (Random.Range(0, max) == 0)
            {
                int i = PointDust(c + Circular(16f, 17f), 31, Vector2.zero);
                if (i >= 0) D(i).Scale = 0.7f;
            }
            if (Random.Range(0, max) == 0)
            {
                int i = PointDust(c + Circular(16f, 17f), 228, Circular(3f, 3f) * 0.33f);
                if (i >= 0) D(i).Scale = 0.6f;
            }
        }

        static Vector2 Circular(float rx, float ry)
        {
            var u = Random.insideUnitCircle;
            return new Vector2(u.x * rx, u.y * ry);
        }

        /// <summary>
        /// P:19759-19767, 19850-19858: off a wall at half its speed, off the ground at half when falling
        /// faster than 1 px; a blow harder than 3 px chips the blocks, digs (Dig) and bursts
        /// (MakeBoulderOfEarthExplosion).
        /// </summary>
        void BoulderTile(Proj p, bool hitX, bool hitY, Vector2 oldVel)
        {
            var ov = Flip(oldVel);
            var v = ov;
            if (hitX) v.x = ov.x * -0.5f;
            if (hitY) v.y = ov.y > 1f ? ov.y * -0.5f : 0f;
            p.Vel = Flip(v);
            if (!((hitX && Mathf.Abs(ov.x) > 3f) || (hitY && Mathf.Abs(ov.y) > 3f))) return;
            for (int k = 0; k < 10; k++) BoulderChips(p, ov);
            Sound("Dig_" + Random.Range(0, 3));
            BoulderBurst(p);
        }

        /// <summary>Collision.HitTiles: a mote of each block the boulder's box meets, a step on.</summary>
        static void BoulderChips(Proj p, Vector2 v)
        {
            var c = CenterPx(p) + v;
            int x0 = Mathf.FloorToInt((c.x - 16f) / 16f), x1 = Mathf.FloorToInt((c.x + 16f) / 16f);
            int y0 = Mathf.FloorToInt((c.y - 17f) / 16f), y1 = Mathf.FloorToInt((c.y + 17f) / 16f);
            var pp = TerraParticles.Instance;
            if (pp == null) return;
            for (int i = x0; i <= x1; i++)
                for (int j = y0; j <= y1; j++)
                {
                    if (!TerraWorld.IsSolid(i, -j - 1) || Random.Range(0, 3) != 0) continue;
                    pp.NewDust(new Vector2(i * 16f, j * 16f), 16, 16, TerraMod.TileDustType(TerraWorld.Get(i, -j - 1)));
                }
        }

        /// <summary>
        /// MakeBoulderOfEarthExplosion (P:20508-20548): 40 more updates of smoke, the ground round it
        /// thrown up (CreateGroundExplosion: wider and higher the faster it went) and ten sparks.
        /// </summary>
        void BoulderBurst(Proj p)
        {
            DevBoulderBursts++;
            p.LocalAi0 = 40f;
            var v = TV(p);
            float spread = 20f;
            int fluff = 10, dist = 50, start = 1, layers = 6;
            if (v.magnitude < 8f || Mathf.Abs(v.y) < 4f)
            {
                spread = 15f;
                fluff = 7;
                dist = 30;
                layers = 4;
            }
            if (v.magnitude < 4f || Mathf.Abs(v.y) < 2f)
            {
                spread = 15f;
                fluff = 7;
                dist = 30;
                layers = 2;
                start = 0;
            }
            GroundExplosion(p, spread, fluff, dist, start, layers, Mathf.Max(1, layers - 2));
            var c = CenterPx(p);
            for (int k = 0; k < 10; k++)
            {
                int i = PointDust(c + Circular(16f, 17f), 228, Circular(3f, 3f));
                if (i < 0) continue;
                D(i).Scale = k < 5 ? 1.8f : 0.6f;
                D(i).NoGravity = k < 5;
            }
        }

        /// <summary>
        /// CreateGroundExplosion (P:54340-54404): every open block top near it throws up three motes of
        /// itself (layer by layer, higher each), more from the upper layers, and mostly a puff of smoke
        /// (61-63) the bigger the further out.
        /// </summary>
        static void GroundExplosion(Proj p, float maxSpread, int fluff, int distFluff, int layerStart, int layerEnd, int layerJump)
        {
            var pp = TerraParticles.Instance;
            if (pp == null) return;
            var c = CenterPx(p);
            float hw = p.Spec.W * 0.5f, hh = p.Spec.H * 0.5f;
            int x0 = Mathf.FloorToInt((c.x - hw) / 16f) - fluff, y0 = Mathf.FloorToInt((c.y - hh) / 16f) - fluff;
            int x1 = Mathf.FloorToInt((c.x + hw) / 16f) + fluff, y1 = Mathf.FloorToInt((c.y + hh) / 16f) + fluff;
            int mid = x0 / 2 + x1 / 2;
            float reach = p.Spec.W / 2 + distFluff;
            for (int layer = layerStart; layer < layerEnd; layer += layerJump)
                for (int i = x0; i <= x1; i++)
                    for (int j = y0; j <= y1; j++)
                    {
                        if (Vector2.Distance(c, new Vector2(i * 16f, j * 16f)) > reach) continue;
                        if (!TerraWorld.IsSolid(i, -j - 1) || TerraWorld.IsSolid(i, -j)) continue;
                        int type = TerraMod.TileDustType(TerraWorld.Get(i, -j - 1));
                        var tl = new Vector2(i * 16f, j * 16f);
                        for (int k = 0; k < 3; k++)
                        {
                            int d = pp.NewDust(tl, 16, 16, type);
                            if (d < 0) continue;
                            pp[d].Velocity.y -= 3f + layer * 1.5f;
                            pp[d].Velocity.y *= Random.value;
                            pp[d].Scale += layer * 0.03f;
                        }
                        if (layer >= 2)
                            for (int k = 0; k < 2; k++)
                            {
                                int d = pp.NewDust(tl, 16, 16, type);
                                if (d < 0) continue;
                                pp[d].Velocity.y -= 1f + layer;
                                pp[d].Velocity.y *= Random.value;
                            }
                        if (Random.Range(0, 3) == 0) continue;
                        float s = Mathf.Abs(mid - i) / (maxSpread / 2f);
                        var gv = new Vector2(Random.Range(-20, 21) * 0.1f, (-Random.Range(10, 31) * 0.1f - (0.1f + layer * 0.5f + s * layer)) * Random.value);
                        pp.NewGore(new Vector2(i * 16f + 20f, j * 16f + 20f), gv, 61 + Random.Range(0, 3), 1f - layer * 0.15f + s * 0.5f, exact: true);
                    }
        }

        /// <summary>
        /// M:30802-30807, 31216-31219, 31634-31641: nine fading grey copies at its old places 18, 16 ... 2
        /// (growing toward 1.3), itself lit, then its glow (252) at 0.7, dimming as it rolls to a stop.
        /// </summary>
        static void DrawBoulder(FxBatch fx, Proj p, Vector2 px)
        {
            var spr = FxBatch.Frame("Projectile_261", 0, 0, 32, 32, 16f, 16f);
            if (spr == null) return;
            for (int i = 18; i > 0; i -= 2)
            {
                if (i >= p.Trail.Count) continue;
                fx.Draw(spr, TerraParticles.ToPx(p.Trail[i]), FxBatch.C(60, 60, 60, 60) * ((18 - i) / 30f), p.TRot, Mathf.LerpUnclamped(p.Scale, 1.3f, i / 15f));
            }
            fx.Draw(spr, px, new Color(L, L, L, 1f), p.TRot, p.Scale);
            float glow = 0.7f, speed = p.Vel.magnitude;
            if (speed < 0.3f && p.Vel.y == 0f) glow = Mathf.InverseLerp(0.02f, 0.3f, speed) * 0.7f;
            var mask = FxBatch.Frame("Glow_252", 0, 0, 32, 32, 16f, 16f);
            if (mask != null) fx.Draw(mask, px, new Color(glow, glow, glow, glow), p.TRot, p.Scale);
        }

        /// <summary>P:75906-75925: a dig, 15 motes of earth (148) and three sparks.</summary>
        static void KillBoulder(Proj p)
        {
            Sound("Dig_" + Random.Range(0, 3));
            var tl = CenterPx(p) - new Vector2(16f, 17f);
            for (int k = 0; k < 15; k++)
            {
                int i = BoxDust(tl, 32, 34, 148);
                if (i < 0) continue;
                D(i).Velocity *= 2f;
                D(i).Velocity.y -= 0.4f;
            }
            for (int k = 0; k < 3; k++)
            {
                int i = BoxDust(tl, 32, 34, 228);
                if (i < 0) continue;
                D(i).Velocity *= 2f;
                D(i).Scale *= 0.8f;
                D(i).Velocity.y -= 0.3f;
            }
        }

        // ------------------------------------------------------------------ 336: Razorpine

        /// <summary>
        /// Pl:48067-48085: 2-4 needles, each a little further off its aim (0.025 × its place) and set
        /// back to full speed, the first furthest out along the aim (1.75 steps per place), older by
        /// up to 10 per place.
        /// </summary>
        public void ShootRazorpine(Vector2 from, Vector2 velocity, int damage, float knockback, float crit)
        {
            int n = 2 + Random.Range(0, 3);
            float speed = velocity.magnitude;
            for (int k = 0; k < n; k++)
            {
                float off = 0.025f * k;
                var v = velocity + new Vector2(Random.Range(-35, 36), Random.Range(-35, 36)) * off;
                v = (v.sqrMagnitude > 1e-6f ? v.normalized : Vector2.right) * speed;
                var p = Spawn(ProjKind.PineNeedle, from + velocity * (n - k) * 1.75f * Px, v, damage, knockback, crit);
                if (p != null) p.Ai0 = Random.Range(0, 10 * (k + 1));
            }
        }

        /// <summary>P:75456-75463: six floating green motes (196).</summary>
        static void KillPineNeedle(Proj p)
        {
            var tl = CenterPx(p) - new Vector2(2f, 2f);
            for (int k = 0; k < 6; k++)
            {
                int i = BoxDust(tl, 4, 4, 196);
                if (i < 0) continue;
                D(i).NoGravity = true;
                D(i).Scale = p.Scale;
            }
        }
    }
}
