using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The Old One's Army sentries' shots (summon-hm sec-F; Terraria's space, px y down): the
    /// Flameburst towers' fireballs (664/666/668, AI_131: straight, a burning chance on what they
    /// hit, a 96 px blast when they die) and the Ballista's bolt (680, AI_001: straight for 60
    /// ticks, then dropping, through four enemies).
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        static void RegisterSentryShots()
        {
            for (int tier = 0; tier < 3; tier++)
            {
                specs[(ProjKind)((int)ProjKind.FlameburstShot1 + tier)] = new Spec
                {
                    Ai = Ai.Custom, Texture = "Projectile_" + (664 + tier * 2), W = 16, H = 16, Time = 3600, OncePerNpc = true, AlphaStart = 1f, TrailLength = 10,
                    Update = (t, p) => StepFlameburstShot(p), Draw = (t, fx, p, px) => DrawFlameburstShot(fx, p, px),
                    OnHit = (t, p, hm) => FlameburstBurn(p.Kind - ProjKind.FlameburstShot1, hm), OnKill = (t, p) => t.KillFlameburstShot(p),
                };
            }
            specs[ProjKind.FrostBlast] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_309", W = 14, H = 14, Time = 3600, Extra = 3, Penetrate = 3, LocalCooldown = 10, Look = Look.Invisible,
                Debuffs = new[] { (EnemyDebuffs.Frostbite, 300, 1) }, Update = (t, p) => StepFrostBlast(p), OnKill = (t, p) => KillFrostBlast(p),
            };
            specs[ProjKind.HoundiusFireball] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_967", W = 14, H = 14, Time = 3600,
                Update = (t, p) => StepHoundiusFireball(p), Draw = (t, fx, p, px) => DrawHoundiusFireball(fx, p, px), OnKill = (t, p) => KillHoundiusFireball(p),
            };
            specs[ProjKind.BarnacleShot] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_1026", W = 14, H = 14, Time = 3600, Extra = 2, TrailLength = 10,
                OnHit = (t, p, hm) => EnemyDebuffs.Add(hm, EnemyDebuffs.Poisoned, Random.Range(300, 600)),
                Update = (t, p) => StepBarnacleShot(p), Draw = (t, fx, p, px) => DrawBarnacleShot(fx, p, px), OnKill = (t, p) => KillBarnacleShot(p),
            };
            specs[ProjKind.BallistaBolt] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_680", W = 16, H = 16, Time = 3600, Penetrate = 4, OncePerNpc = true, TileBox = new Vector2(4f, 4f), TrailLength = 10,
                Update = (t, p) => StepBallistaBolt(p), Draw = (t, fx, p, px) => DrawBallistaBolt(fx, p, px), OnKill = (t, p) => KillBallistaBolt(p),
            };
        }

        // ------------------------------------------------------------------ 664/666/668: the Flameburst shot (AI_131)

        /// <summary>P:69513-69575: its shot sound once; fades in (25 a tick); fire dust (one, two or three rolls); turned along its flight.</summary>
        static void StepFlameburstShot(Proj p)
        {
            int tier = p.Kind - ProjKind.FlameburstShot1;
            if (p.Updates == 1) Sound("Custom/dd2_flameburst_tower_shot_" + Random.Range(0, 3));
            p.Alpha = Mathf.Max(0f, p.Alpha - 25f / 255f);
            var v = TV(p);
            for (int k = 0; k <= tier; k++)
            {
                if (Random.Range(0, 2) == 0) continue;
                int i = BoxDust(CenterPx(p) - new Vector2(8f, 8f), 16, 16, 6, v.x * 0.2f, v.y * 0.2f, 100, 2f);
                if (i < 0) continue;
                D(i).NoGravity = true;
                D(i).Velocity *= 0.3f;
                D(i).Velocity.y += Mathf.Sign(D(i).Velocity.y) * 1.2f;
                D(i).FadeIn += 0.5f;
            }
            p.TRot = Mathf.Atan2(v.y, v.x) + Mathf.PI / 2f;
        }

        /// <summary>StatusNPC (P:11325-11336): 664 one in three On Fire 3-5 s; 666 one in two Hellfire 3-8 s; 668 two in three Hellfire 6-8 s.</summary>
        static void FlameburstBurn(int tier, HealthManager hm)
        {
            if (tier == 0 && Random.Range(0, 3) == 0) EnemyDebuffs.Add(hm, EnemyDebuffs.OnFire, 60 * Random.Range(3, 6));
            if (tier == 1 && Random.Range(0, 2) == 0) EnemyDebuffs.Add(hm, EnemyDebuffs.Hellfire, 60 * Random.Range(3, 9));
            if (tier == 2 && Random.Range(0, 3) != 0) EnemyDebuffs.Add(hm, EnemyDebuffs.Hellfire, 60 * Random.Range(6, 9));
        }

        /// <summary>M:31177, 31574, P:81039: four fading copies at every other old place (shrinking to 0.72), then the shot, half toward white at alpha 200.</summary>
        static void DrawFlameburstShot(FxBatch fx, Proj p, Vector2 px)
        {
            var spr = FxBatch.Frame(p.Spec.Texture, 0, 0, 14, 28, 7f, 14f);
            var c = Color.Lerp(new Color(L, L, L, 1f), Color.white, 0.5f);
            c.a = 200f / 255f;
            c *= 1f - p.Alpha;
            for (int i = 1; i < 8; i += 2)
            {
                if (i - 1 >= p.Trail.Count) break;
                fx.Draw(spr, TerraParticles.ToPx(p.Trail[i - 1]), c * ((8 - i) / 15f), p.TRot, Mathf.Lerp(1f, 0.4f, i / 15f));
            }
            fx.Draw(spr, px, c, p.TRot, 1f);
        }

        /// <summary>
        /// P:73050-73136: whatever killed it, a 96 px blast on the spot (each enemy in it once, not
        /// the one it hit; the same burning chance), Item100, a burst of fire and smoke, four smoke gores.
        /// </summary>
        void KillFlameburstShot(Proj p)
        {
            int tier = p.Kind - ProjKind.FlameburstShot1;
            var melee = TerraMod.Instance?.Melee;
            if (melee != null)
            {
                float facing = p.Vel.x < 0f ? -1f : 1f;
                // The Apprentice's tier 3 set (P:73061-73074): a 13-tile blast instead of 6.
                foreach (var hm in EnemiesIn(p.Pos, (Equipment.Current.Ooa & OoaSet.ApprenticeT3) != 0 ? 208f : 96f))
                {
                    int id = hm.GetInstanceID();
                    if (p.Immune.ContainsKey(id)) continue;
                    var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
                    if (col == null) continue;
                    FlameburstBurn(tier, hm);
                    int dealt = SummonTags.Modify(hm, p.Damage, out bool crit, 1f, true, CenterPx(p));
                    melee.HitWith(col, dealt, p.Knockback, crit ? 100f : 0f, facing, p.Pos, Equipment.Current.ArmorPen, autonomous: true);
                    p.Immune[id] = int.MaxValue;
                    DevHits[p.Kind] = DevHit(p.Kind) + 1;
                    SummonTags.OnHit(hm, p.Damage, CenterPx(p));
                    Minions.DevHits++;
                    Minions.DevHitDamage += dealt;
                }
            }
            Sound("Item_100");
            var c = CenterPx(p);
            float flight = Mathf.Atan2(TV(p).y, TV(p).x);
            Vector2 Dir() => Rot(new Vector2(0f, 1f), Random.Range(-Mathf.PI, Mathf.PI));
            for (int k = 0; k < 4; k++) BoxDust(c + Dir() * Random.value * 48f, 0, 0, 31, 0f, 0f, 100, 1.5f);
            for (int k = 0; k < 20; k++)
            {
                int i = BoxDust(c + Dir() * Random.value * 9.6f, 0, 0, 6, 0f, 0f, 200, 2.5f);
                if (i < 0) continue;
                D(i).Velocity *= 16f;
                if (D(i).Velocity.y > -2f) D(i).Velocity.y *= -0.4f;
                D(i).NoGravity = true;
            }
            for (int k = 0; k < 20; k++)
            {
                int i = BoxDust(c + Dir() * Random.value * 48f, 0, 0, 6, 0f, 0f, 100, 1.5f);
                if (i < 0) continue;
                D(i).Velocity *= 2f;
                D(i).NoGravity = true;
                D(i).FadeIn = 1.5f;
            }
            for (int k = 0; k < 10; k++)
            {
                int i = BoxDust(c + Rot(Dir(), flight) * 48f, 0, 0, 6, 0f, 0f, 0, 2.7f);
                if (i < 0) continue;
                D(i).NoGravity = true;
                D(i).Velocity *= 3f;
            }
            for (int k = 0; k < 20; k++)
            {
                int i = BoxDust(c + Rot(Dir(), flight) * 48f, 0, 0, 31, 0f, 0f, 0, 1.5f);
                if (i < 0) continue;
                D(i).NoGravity = true;
                D(i).Velocity *= 3f;
            }
            var pp = TerraParticles.Instance;
            if (pp == null) return;
            for (int k = 0; k < 4; k++)
            {
                var at = c + Dir() * Random.value * 48f - new Vector2(16f, 16f);
                if (Random.Range(0, 2) == 0) at.y -= 30f;
                pp.NewGore(at, Vector2.zero, Random.Range(61, 64), 1f, 0.3f, new Vector2(Random.Range(-10, 11) * 0.05f, Random.Range(-10, 11) * 0.05f));
            }
        }

        static Vector2 Rot(Vector2 v, float a) => new Vector2(v.x * Mathf.Cos(a) - v.y * Mathf.Sin(a), v.x * Mathf.Sin(a) + v.y * Mathf.Cos(a));

        // ------------------------------------------------------------------ 309: the Frost Hydra's blast (aiStyle 28)

        /// <summary>P:26997-27023: unseen but for its frost (three 185 an update, four updates a tick); Item28 as it sets off.</summary>
        static void StepFrostBlast(Proj p)
        {
            if (p.Updates == 1) Sound("Item_28");
            var v = TV(p);
            for (int k = 0; k < 3; k++)
            {
                int i = BoxDust(CenterPx(p) - new Vector2(7f, 7f), 14, 14, 185, v.x, v.y, 50, 1.2f);
                if (i < 0) continue;
                D(i).NoGravity = true;
                D(i).Velocity *= 0.3f;
            }
        }

        /// <summary>P:76895-76911: Item27 and ten bits of frost, half of them flung.</summary>
        static void KillFrostBlast(Proj p)
        {
            Sound("Item_27");
            for (int k = 0; k < 10; k++)
            {
                int i = BoxDust(CenterPx(p) - new Vector2(7f, 7f), 14, 14, 185);
                if (i < 0 || Random.Range(0, 2) != 0) continue;
                D(i).Velocity *= 2f;
                D(i).NoGravity = true;
                D(i).Scale *= 1.75f;
            }
        }

        // ------------------------------------------------------------------ 967: Houndius Shootius's fireball (aiStyle 28)

        /// <summary>P:26947-26962, 27024-27033: a burst of light (219) and Item60 as it sets off, then a mote every third tick; 8 frames of 7 ticks; it hits 10 px beyond its box.</summary>
        static void StepHoundiusFireball(Proj p)
        {
            var v = TV(p);
            if (p.Updates == 1)
            {
                p.HitSize = new Vector2(34f, 34f);
                Sound("Item_60");
                for (int k = 0; k < 10; k++)
                {
                    int i = BoxDust(CenterPx(p) - new Vector2(7f, 7f), 14, 14, 219);
                    if (i < 0) continue;
                    D(i).Scale = 0.4f + Random.Range(-20, 40) * 0.01f;
                    D(i).NoGravity = true;
                    D(i).Velocity = Rot(v * 0.5f, Random.Range(-0.5f, 0.5f));
                }
            }
            if (++p.LocalAi0 >= 5f && p.TimeLeft % 3 == 0)
            {
                p.LocalAi0 = 5f;
                int i = BoxDust(CenterPx(p) - new Vector2(7f, 7f), 14, 14, 219, v.x, v.y);
                if (i >= 0)
                {
                    D(i).Scale = 0.35f + Random.Range(-5, 5) * 0.01f;
                    D(i).NoGravity = true;
                    D(i).Velocity = v * 1.5f;
                }
            }
            if (++p.FrameCounter > 6)
            {
                p.FrameCounter = 0;
                p.Frame = (p.Frame + 1) % 8;
            }
        }

        static void DrawHoundiusFireball(FxBatch fx, Proj p, Vector2 px) =>
            fx.Draw(FxBatch.Frame("Projectile_967", 0, 26 * p.Frame, 26, 26, 13f, 13f), px, Color.white, 0f, 1f);

        /// <summary>P:76818-76827: Item45 and a scatter of twenty motes.</summary>
        static void KillHoundiusFireball(Proj p)
        {
            Sound("Item_45");
            for (int k = 0; k < 20; k++)
            {
                int i = BoxDust(CenterPx(p) - new Vector2(7f, 7f), 14, 14, 219);
                if (i < 0) continue;
                D(i).Scale = 0.5f + Random.Range(-20, 40) * 0.01f;
                D(i).NoGravity = true;
                D(i).Velocity = Random.insideUnitCircle * 6f;
            }
        }

        // ------------------------------------------------------------------ 1026: the Barnacle's shot (aiStyle 28)

        /// <summary>P:26921-26946, 27034-27041, PO:2534-2564: a green spray and Item95 as it fires, then green motes; 4 frames of 5 updates, turned along its flight.</summary>
        static void StepBarnacleShot(Proj p)
        {
            var v = TV(p);
            var n = v.sqrMagnitude > 0f ? v.normalized : Vector2.right;
            if (p.Updates == 1)
            {
                Sound("Item_95");
                var c = CenterPx(p);
                for (int k = 0; k < 15; k++)
                {
                    int i = PointDust(c + new Vector2(Random.Range(-4, 4), Random.Range(-4, 4)), 2, Rot(n, Random.Range(-Mathf.PI / 16f, Mathf.PI / 16f)) * (k < 8 ? Random.Range(7f, 9f) : Random.Range(3f, 5f)), 50, 1.2f + Random.Range(-0.05f, 0.05f));
                    if (i < 0) continue;
                    D(i).FadeIn = 0.5f;
                    D(i).NoGravity = true;
                }
                for (int k = 0; k < 5; k++)
                {
                    int i = PointDust(c, 267, Rot(n, Random.Range(-Mathf.PI / 32f, Mathf.PI / 32f)) * Random.Range(0f, 8f), 127, 0.5f);
                    if (i < 0) continue;
                    D(i).Color = Color.Lerp(new Color(85f / 255f, 107f / 255f, 47f / 255f), Color.white, Random.value * 0.25f);
                    D(i).FadeIn = Random.Range(0.6f, 1.2f);
                    D(i).NoGravity = true;
                }
            }
            if (++p.LocalAi0 >= 8f && p.TimeLeft % 2 == 0)
            {
                p.LocalAi0 = 8f;
                int i = BoxDust(CenterPx(p) - new Vector2(7f, 7f) - n * 5f, 14, 14, 2, v.x, v.y, 50, 0.7f + Random.Range(-5, 5) * 0.01f);
                if (i >= 0)
                {
                    D(i).FadeIn = 0.5f;
                    D(i).NoGravity = true;
                    D(i).Velocity = v * 0.25f;
                }
            }
            if (++p.FrameCounter > 4)
            {
                p.FrameCounter = 0;
                p.Frame = (p.Frame + 1) % 4;
            }
            p.Dir = v.x < 0f ? -1f : 1f;
            p.TRot = Mathf.Atan2(v.y, v.x) + (p.Dir < 0f ? -Mathf.PI : 0f);
        }

        /// <summary>M:28602-28605, 30783-30789, 31574: a thin green streak along its last places, three blue-tinted afterimages, then the shot.</summary>
        static void DrawBarnacleShot(FxBatch fx, Proj p, Vector2 px)
        {
            var pixel = FxBatch.Frame("MagicPixel", 0, 0, 1, 1, 0.5f, 1f);
            for (int k = Mathf.Min(9, p.Trail.Count - 1); k >= 1; k--)
            {
                var a = TerraParticles.ToPx(p.Trail[k]);
                var b = TerraParticles.ToPx(p.Trail[k - 1]);
                var seg = b - a;
                if (seg.sqrMagnitude < 0.01f) continue;
                var c = new Color(140f / 255f, 234f / 255f, 180f / 255f, 1f) * (1f - k / 10f);
                fx.Draw(pixel, a, c, Mathf.Atan2(seg.y, seg.x) + Mathf.PI / 2f, new Vector2(4f, seg.magnitude));
            }
            var spr = FxBatch.Frame("Projectile_1026", 0, 18 * p.Frame, 30, 18, 15f, 9f);
            var lit = new Color(L, L, L, 1f);
            var tint = Color.Lerp(lit, Color.blue, 0.5f);
            bool flip = p.Dir < 0f;
            for (int i = 6; i >= 2; i -= 2)
            {
                if (i - 1 >= p.Trail.Count) continue;
                fx.Draw(spr, TerraParticles.ToPx(p.Trail[i - 1]), tint * ((8 - i) / 15f), p.TRot, Mathf.Lerp(1f, 0.5f, i / 15f), flipX: flip);
            }
            fx.Draw(spr, px, lit, p.TRot, 1f, flipX: flip);
        }

        /// <summary>P:76828-76839: Item167 and a scatter of green.</summary>
        static void KillBarnacleShot(Proj p)
        {
            Sound("Item_167");
            var v = TV(p);
            for (int k = 0; k < 20; k++)
            {
                int i = BoxDust(CenterPx(p) - new Vector2(7f, 7f), 14, 14, 2, v.x, v.y, 50, 1.2f + Random.Range(-20, 40) * 0.01f);
                if (i < 0) continue;
                D(i).FadeIn = 0.5f;
                D(i).NoGravity = true;
                D(i).Velocity = Random.insideUnitCircle * 5f;
            }
        }

        // ------------------------------------------------------------------ 680: the Ballista's bolt (AI_001)

        /// <summary>P:56926, 58454-58461, 58641, 58748: straight for 60 ticks, then it slows and drops (0.3 a tick, at most 16); turned along its flight.</summary>
        static void StepBallistaBolt(Proj p)
        {
            if (++p.Ai0 > 60f)
            {
                p.Vel.x *= 0.98f;
                p.Vel.y -= 0.3f;
            }
            p.Vel.y = Mathf.Max(p.Vel.y, -16f);
            var v = TV(p);
            p.TRot = Mathf.Atan2(v.y, v.x) + Mathf.PI / 2f;
        }

        /// <summary>M:31155-31160: three fading, shrinking copies at its old places 1, 4 and 7, then the bolt.</summary>
        static void DrawBallistaBolt(FxBatch fx, Proj p, Vector2 px)
        {
            var spr = FxBatch.Frame("Projectile_680", 0, 0, 10, 66, 5f, 33f);
            var c = new Color(L, L, L, 1f);
            for (int i = 1; i < 9; i += 3)
            {
                if (i - 1 >= p.Trail.Count) break;
                fx.Draw(spr, TerraParticles.ToPx(p.Trail[i - 1]), c * ((9 - i) / 15f), p.TRot, Mathf.Lerp(1f, 0.5f, i / 15f));
            }
            fx.Draw(spr, px, c, p.TRot, 1f);
        }

        /// <summary>P:73027-73049: a dig, splinters (11) back along its way, and one red one.</summary>
        static void KillBallistaBolt(Proj p)
        {
            Sound("Dig_" + Random.Range(0, 3));
            var v = TV(p);
            var n = v.sqrMagnitude > 0f ? v.normalized : Vector2.zero;
            var c = CenterPx(p);
            var at = c - new Vector2(8f, 8f) + n * 16f;
            for (int k = 0; k < 16; k++)
            {
                if (Random.Range(0, 2) == 0)
                {
                    at -= n * 8f;
                    continue;
                }
                int i = BoxDust(at, 16, 16, 11);
                if (i >= 0)
                {
                    D(i).Position = (D(i).Position + c) / 2f;
                    D(i).Velocity += v * 0.4f;
                    D(i).Velocity *= 0.5f;
                    D(i).NoGravity = true;
                }
                at -= n * 8f;
            }
            int r = BoxDust(c - new Vector2(8f, 8f), 16, 16, 11, 0f, 0f, 0, 1.6f);
            if (r >= 0)
            {
                D(r).Color = Color.red;
                D(r).NoGravity = true;
            }
        }
    }
}
