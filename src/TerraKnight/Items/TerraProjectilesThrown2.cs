using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The sweep's boomerangs and knives (S1.F; Terraria's space, px y down, per update): the Ice
    /// Boomerang (113), Fruitcake Chakram (333), Bloody Machete (320, dropping after 10 updates),
    /// Shroomerang (867), Trimarang (1000, three out), Axearang (1052) and Combat Wrench (902,
    /// twice a tick, back off the first thing it hits), all AI_003; the Sergeant United Shield
    /// (866) leaping from enemy to enemy; the Vampire Knives (304, the red healing orb 305), the
    /// Shadowflame Knife (497, AI_002 bouncing off what it hits) and the Flying Knife (491, the
    /// old AI_009: steered at the cursor while held, back to the hand once let go).
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        static void RegisterThrown2()
        {
            // P:2014-2023, 4132-4141, 4260-4269, 8327-8336, 9493-9502, 9980-9988, 20073: 10×10 against blocks.
            Spec Rang(string tex, int w, float scale = 1f) => new Spec
            {
                Ai = Ai.Boomerang, Texture = tex, W = w, H = w, Scale = scale, Time = 3600, Penetrate = -1, TileBox = new Vector2(10f, 10f),
                Draw = (t, fx, p, px) => DrawGenericLit(fx, p, px),
            };
            specs[ProjKind.IceBoomerangProj] = Rang("Projectile_113", 22);
            specs[ProjKind.IceBoomerangProj].Light = new Color(0.1f, 0.3f, 0.4f);
            specs[ProjKind.IceBoomerangProj].Debuffs = new[] { (EnemyDebuffs.Frostburn, 180, 2) };
            specs[ProjKind.FruitcakeChakramProj] = Rang("Projectile_333", 38, 0.9f);
            specs[ProjKind.FruitcakeChakramProj].ReturnSpeed = 12f;
            specs[ProjKind.FruitcakeChakramProj].ReturnAccel = 0.6f;
            specs[ProjKind.BloodyMacheteProj] = Rang("Projectile_320", 34);
            specs[ProjKind.BloodyMacheteProj].ReturnSpeed = 15f;
            specs[ProjKind.BloodyMacheteProj].ReturnAccel = 3f;
            // P:11295-11298: bleeding 3-5 s.
            specs[ProjKind.BloodyMacheteProj].OnHit = (t, p, hm) => EnemyDebuffs.Add(hm, EnemyDebuffs.Bleeding, 60 * Random.Range(3, 6));
            specs[ProjKind.ShroomerangProj] = Rang("Projectile_867", 22);
            specs[ProjKind.TrimarangProj] = Rang("Projectile_1000", 22);
            specs[ProjKind.TrimarangProj].MaxOut = 3;
            specs[ProjKind.TrimarangProj].ReturnSpeed = 9.5f;
            specs[ProjKind.TrimarangProj].Draw = (t, fx, p, px) => DrawCentredLit(fx, p, px);
            specs[ProjKind.AxearangProj] = Rang("Projectile_1052", 22);
            // P:8601-8611, 14105-14112, 20113: 20×20 (8×8 on blocks), twice a tick; what it hits is safe from the player 7 ticks and sends it home
            // without bouncing back (P:13101), knocked away from the player either way (P:13124-13130).
            specs[ProjKind.CombatWrenchProj] = new Spec
            {
                Ai = Ai.Boomerang, Texture = "Projectile_902", W = 20, H = 20, Extra = 1, Time = 3600, Penetrate = -1, TileBox = new Vector2(8f, 8f), OwnerImmune = 7,
                NoHitTurn = true, PushFromPlayer = true, TrailLength = 10, OnHit = (t, p, hm) => p.Returning = true, Draw = (t, fx, p, px) => DrawWrench(fx, p, px),
            };
            // P:8314-8326, 14142-14170, 19666: 30×30 (10×10 on blocks), twice a tick, each enemy once, off walls; five hits, then harmless home.
            specs[ProjKind.BouncingShieldProj] = new Spec
            {
                Ai = Ai.Boomerang, Texture = "Projectile_866", W = 30, H = 30, Extra = 1, Time = 3600, Penetrate = -1, TileBox = new Vector2(10f, 10f), OncePerNpc = true,
                NoHitTurn = true, WallBounce = true, SpinRate = 0f, ReturnSpeed = 16f, ReturnAccel = 1.2f, Light = new Color(0.15f, 0.15f, 0.15f), Frames = 4,
                CanDamage = p => p.Damage > 0, OnHit = (t, p, hm) => t.ShieldHit(p), Draw = (t, fx, p, px) => DrawBouncingShield(fx, p, px),
            };
            // P:3954-3966, 20123: 30×30 (4×4 on blocks), one enemy, seen at once.
            specs[ProjKind.VampireKnife] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_304", W = 30, H = 30, Time = 3600, Penetrate = 1, TileBox = new Vector2(4f, 4f), Light = new Color(0.2f, 0.04f, 0.02f),
                Update = (t, p) => StepVampireKnife(p), Draw = (t, fx, p, px) => DrawVampireKnife(fx, p, px), OnHit = (t, p, hm) => t.VampireHeal(p, hm),
                OnKill = (t, p) => KillVampireKnife(p),
            };
            // P:3967-3975: the healing orb, unseen, ten more updates a tick.
            specs[ProjKind.VampireHeal] = new Spec
            {
                Ai = Ai.Custom, W = 6, H = 6, Time = 600, Extra = 10, Penetrate = -1, TileCollide = false, OwnMove = true, CanDamage = p => false, Look = Look.Invisible,
                Update = (t, p) => StepVampireHeal(p), Draw = (t, fx, p, px) => { }, OnKill = (t, p) => { },
            };
            // P:5707-5716, 20073: 30×30 (10×10 on blocks), three enemies.
            specs[ProjKind.ShadowFlameKnifeProj] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_497", W = 30, H = 30, Time = 3600, Penetrate = 3, TileBox = new Vector2(10f, 10f), Debuffs = null,
                Update = (t, p) => StepShadowflameKnife(p), Draw = (t, fx, p, px) => DrawGenericLit(fx, p, px), OnHit = (t, p, hm) => ShadowflameKnifeHit(p, hm),
                OnKill = (t, p) => KillShadowflameKnife(p),
            };
            // P:5406-5413, 20073: 26×26 (10×10 on blocks), through everything, full bright.
            specs[ProjKind.FlyingKnifeProj] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_491", W = 26, H = 26, Time = 3600, Penetrate = -1, TileBox = new Vector2(10f, 10f), Held = true,
                Update = (t, p) => t.StepFlyingKnife(p), Draw = (t, fx, p, px) => DrawGeneric(fx, p, px, Color.white),
                TileHit = (t, p, hx, hy, before) => FlyingKnifeTile(p, hx, hy, before), OnKill = (t, p) => { },
            };
        }

        // ------------------------------------------------------------------ the boomerangs (AI_003)

        /// <summary>AI_003's per-type dust (P:39609-39690) and the shield's turn along its flight (P:40278-40286).</summary>
        void MoreBoomerangDust(Proj p)
        {
            var v = TV(p);
            var tl = CenterPx(p) - new Vector2(p.Spec.W * p.Spec.Scale, p.Spec.H * p.Spec.Scale) * 0.5f;
            int w = (int)(p.Spec.W * p.Spec.Scale), h = (int)(p.Spec.H * p.Spec.Scale);
            switch (p.Kind)
            {
                case ProjKind.IceBoomerangProj:
                {
                    int i = BoxDust(tl, w, h, 76, v.x * 0.15f, v.y * 0.15f, 0, 1.1f);
                    if (i >= 0) D(i).NoGravity = true;
                    BoxDust(tl, w, h, 15, v.x * 0.05f, v.y * 0.05f, 150, 0.6f);
                    break;
                }
                case ProjKind.BloodyMacheteProj:
                    if (Random.Range(0, 3) == 0)
                    {
                        int i = BoxDust(tl, w, h, 5, v.x * 0.25f, v.y * 0.25f, 0, 1.1f);
                        if (i < 0) break;
                        if (Random.Range(0, 2) == 0)
                        {
                            D(i).Scale = 0.9f;
                            D(i).Velocity *= 0.2f;
                        }
                        else D(i).NoGravity = true;
                    }
                    break;
                case ProjKind.ShroomerangProj:
                    if (Random.Range(0, 3) == 0)
                    {
                        int i = BoxDust(tl, w, h, 165, v.x, v.y, 50);
                        if (i < 0) break;
                        D(i).Velocity *= 0.5f;
                        D(i).NoGravity = true;
                    }
                    MushroomLight(p);
                    break;
                case ProjKind.TrimarangProj:
                    if (Random.Range(0, 3) == 0)
                    {
                        int pick = Random.Range(0, 3);
                        if (pick == 0)
                        {
                            int type = Random.Range(0, 3) switch { 1 => 57, 2 => 58, _ => 15 };
                            int i = BoxDust(tl, w, h, type, v.x * 0.25f, v.y * 0.25f, 150, 0.7f);
                            if (i >= 0) D(i).Velocity *= 0.5f;
                        }
                        else if (pick == 1)
                        {
                            int i = BoxDust(tl, w, h, 267, v.x, v.y, 50, 1.1f);
                            if (i >= 0)
                            {
                                D(i).Color = FxBatch.C(50, 50, 200);
                                D(i).FadeIn = 0.1f;
                                D(i).Velocity = v * 0.5f;
                                D(i).NoGravity = true;
                            }
                        }
                        else
                        {
                            int i = BoxDust(tl, w, h, 76, v.x * 0.15f, v.y * 0.15f, 0, 1.1f);
                            if (i >= 0) D(i).NoGravity = true;
                            BoxDust(tl, w, h, 15, v.x * 0.05f, v.y * 0.05f, 150, 0.6f);
                        }
                    }
                    MushroomLight(p);
                    break;
                case ProjKind.AnchorProj:
                    AnchorTurn(p);
                    break;
                case ProjKind.BouncingShieldProj:
                    p.TRot = ToRot(v);
                    if (Random.Range(0, 2) == 0)
                    {
                        int i = BoxDust(tl, w, h, 212);
                        if (i < 0) break;
                        D(i).Velocity *= 0.1f;
                        D(i).NoGravity = true;
                    }
                    break;
            }
        }

        /// <summary>
        /// Projectile light (P:16309-16324, light 0.6): the Shroomerang's blue and the Trimarang's
        /// glow breathe with the mouse text colour's pulse plus a little noise.
        /// </summary>
        static void MushroomLight(Proj p)
        {
            float n = Random.Range(28, 42) * 0.005f + (270f - (190f + 65f * (0.5f + 0.5f * Mathf.Sin(Time.time * 3f)))) / 500f;
            var col = p.Kind == ProjKind.ShroomerangProj ? new Color(0.1f, 0.3f + n / 2f, 0.6f + n) : new Color(0.3f + n * 0.25f, 0.35f + n * 0.75f, 0.5f + n);
            FxBatch.Instance?.Light(CenterPx(p), col * 0.6f, 3f);
        }

        /// <summary>AI_003 for 320 (P:39818-39836): after 10 updates out it drops (0.5 an update, to 16) and slows 5% a tick.</summary>
        static void MacheteDrop(Proj p)
        {
            if (p.Ai1 < 10f) return;
            var v = TV(p);
            v.y = Mathf.Min(16f, v.y + 0.5f);
            v.x *= 0.95f;
            p.Vel = Flip(v);
        }

        /// <summary>
        /// AI_003 for 866 (P:39930-39985): from its 10th to its 360th update it makes for the nearest
        /// enemy it can see (FindTargetWithLineOfSight, 800 px), at 12 px an update turning 0.25 a
        /// step (three times across zero); with none it flies on, home after 30.
        /// </summary>
        void StepBouncingShield(Proj p)
        {
            var c = CenterPx(p);
            if (p.Damage > 0) p.Target = p.LocalAi0 >= 10f && p.LocalAi0 <= 360f ? SightTarget(c, 800f, p) : null;
            p.LocalAi0++;
            var v = TV(p);
            Vector2 aim;
            if (p.Target != null && Chaseable(p.Target)) aim = EnemyCenterPx(p.Target);
            else
            {
                aim = c + v * 100f;
                if (p.LocalAi0 >= 30f)
                {
                    p.Returning = true;
                    p.Ai1 = 0f;
                }
            }
            var want = aim - c;
            want = want.sqrMagnitude > 1e-6f ? want.normalized * 12f : Vector2.zero;
            v.x = Approach(v.x, want.x, 0.25f, 2f);
            v.y = Approach(v.y, want.y, 0.25f, 2f);
            p.Vel = Flip(v);
        }

        /// <summary>Projectile.FindTargetWithLineOfSight: the nearest chaseable enemy in sight within a distance (px), not one the shot has already struck.</summary>
        static HealthManager SightTarget(Vector2 c, float range, Proj p)
        {
            HealthManager best = null;
            float bestD = range;
            foreach (var hm in EnemiesNear(c, range))
            {
                if (p != null && p.Immune.ContainsKey(hm.GetInstanceID())) continue;
                var e = EnemyCenterPx(hm);
                float d = Vector2.Distance(c, e);
                if (d < bestD && InSight(c, e))
                {
                    bestD = d;
                    best = hm;
                }
            }
            return best;
        }

        /// <summary>P:14142-14170: one hit less; the fifth leaves it harmless, heading home; otherwise it leaps at once for the next enemy in sight.</summary>
        void ShieldHit(Proj p)
        {
            if (++p.LocalAi1 >= 5f)
            {
                p.Damage = 0;
                p.Target = null;
                return;
            }
            var next = SightTarget(CenterPx(p), 800f, p);
            p.Target = next;
            if (next == null) return;
            var v = TV(p);
            p.Vel = Flip((EnemyCenterPx(next) - CenterPx(p)).normalized * v.magnitude);
            DevShieldLeaps++;
        }

        /// <summary>Main.DrawProj's generic rule: the texture's own width half across from the hitbox's left, half the hitbox down; lit.</summary>
        static void DrawGenericLit(FxBatch fx, Proj p, Vector2 px) => DrawGeneric(fx, p, px, new Color(L, L, L, 1f));

        static void DrawGeneric(FxBatch fx, Proj p, Vector2 px, Color c, bool flip = false)
        {
            var whole = FxBatch.Whole(p.Spec.Texture);
            if (whole == null) return;
            int tw = (int)whole.rect.width, th = (int)whole.rect.height;
            int w = (int)(p.Spec.W * p.Spec.Scale), h = (int)(p.Spec.H * p.Spec.Scale);
            fx.Draw(FxBatch.Frame(p.Spec.Texture, 0, 0, tw, th, tw * 0.5f, h * 0.5f), px + new Vector2((tw - w) * 0.5f, 0f), c * (1f - p.Alpha), p.TRot, p.Scale, flipX: flip);
        }

        /// <summary>M:33209-33640's plain branch: the whole texture about its centre, lit.</summary>
        static void DrawCentredLit(FxBatch fx, Proj p, Vector2 px) =>
            fx.Draw(FxBatch.Whole(p.Spec.Texture), px, new Color(L, L, L, 1f) * (1f - p.Alpha), p.TRot, p.Scale);

        /// <summary>
        /// M:30651-31427 for 902: five lit copies at its old places 1, 3 ... 9, fainter ((10 - i) / 15),
        /// shrinking toward 0.7 by the 15th and turned a fifth further per step; then itself.
        /// </summary>
        static void DrawWrench(FxBatch fx, Proj p, Vector2 px)
        {
            var spr = FxBatch.Whole("Projectile_902");
            if (spr == null) return;
            for (int i = 1; i < 10 && i < p.Trail.Count; i += 2)
                fx.Draw(spr, TerraParticles.ToPx(p.Trail[i]), new Color(L, L, L, 1f) * ((10 - i) / 15f), p.TRot + p.TRot * 0.2f * (i - 1), Mathf.Lerp(p.Scale, 0.7f, i / 15f));
            fx.Draw(spr, px, new Color(L, L, L, 1f), p.TRot, p.Scale);
        }

        /// <summary>M:33741-34005 for 866: its first frame about its middle shifted 14 px along it, lit, turned along its flight.</summary>
        static void DrawBouncingShield(FxBatch fx, Proj p, Vector2 px)
        {
            var whole = FxBatch.Whole("Projectile_866");
            if (whole == null) return;
            int w = (int)whole.rect.width, fh = (int)whole.rect.height / 4;
            fx.Draw(FxBatch.Frame("Projectile_866", 0, 0, w, fh, w / 2f + 14f, fh / 2f), px, new Color(L, L, L, 1f), p.TRot, p.Scale);
        }

        // ------------------------------------------------------------------ 304, 305: the Vampire Knives

        /// <summary>
        /// AI_002 for 304 (P:23456-23460, 23881-23897): shown at once, no fall; turned along its
        /// flight for 30 updates, then fading 10 an update, its damage and knockback 10% less
        /// each, gone without a trace once unseen.
        /// </summary>
        static void StepVampireKnife(Proj p)
        {
            var v = TV(p);
            if (++p.Ai0 >= 30f)
            {
                p.Alpha += 10f / 255f;
                p.Damage = (int)(p.Damage * 0.9f);
                p.Knockback *= 0.9f;
                if (p.Alpha >= 1f)
                {
                    p.Dead = true;
                    return;
                }
            }
            else p.TRot = ToRot(v) + Mathf.PI / 2f;
            if (v.y > 32f) p.Vel.y = -32f;
        }

        /// <summary>M:33209 plain branch, P:81261-81264: about its centre, white and a third added, as it shows.</summary>
        static void DrawVampireKnife(FxBatch fx, Proj p, Vector2 px)
        {
            float a = 1f - p.Alpha;
            fx.Draw(FxBatch.Whole("Projectile_304"), px, new Color(a, a, a, a / 3f), p.TRot, p.Scale);
        }

        /// <summary>P:75883-75893: three blood-red motes (182) as it breaks.</summary>
        static void KillVampireKnife(Proj p)
        {
            var tl = CenterPx(p) - new Vector2(15f, 15f);
            var v = p.Vel;
            for (int k = 0; k < 3; k++)
            {
                int i = BoxDust(tl, 30, 30, 182, 0f, 0f, 100, 0.8f);
                if (i < 0) continue;
                D(i).NoGravity = true;
                D(i).Velocity *= 1.2f;
                D(i).Velocity -= Flip(v) * 0.3f;
            }
        }

        /// <summary>
        /// Projectile.vampireHeal (P:12111-12120): 7.5% of the damage dealt, out of the player's
        /// life-steal budget, carried back by a red orb (305); at most 5 per hit and 20 per second.
        /// </summary>
        void VampireHeal(Proj p, HealthManager hm)
        {
            var stats = TerraMod.Instance?.Stats;
            if (stats == null || stats.IsDead || stats.Life >= stats.LifeMax || hm == null || EnemyStats.Of(hm).Dummy) return;
            int heal = VampireHealing.Reserve(Mathf.Min(MeleeHits.LastStruck * .075f, stats.LifeMax - stats.Life), Time.time);
            if (heal <= 0) return;
            var at = EnemyCenterPx(hm);
            var orb = Spawn(ProjKind.VampireHeal, TerraParticles.ToWorld(at), Vector2.zero, 0, 0f, 0f);
            if (orb != null) orb.Ai1 = heal;
            DevVampireOrbs++;
        }

        /// <summary>AI_052 for 305 (P:28930-28988): it homes on the player (4 px, a sixteenth a step), healing on touch; three crimson motes (183) an update.</summary>
        static void StepVampireHeal(Proj p)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null)
            {
                p.Dead = true;
                return;
            }
            var player = TerraParticles.ToPx(hero.GetComponent<BoxCollider2D>().bounds.center);
            var c = CenterPx(p);
            var to = player - c;
            if (to.magnitude < 50f && Mathf.Abs(to.x) < 13f && Mathf.Abs(to.y) < 24f)
            {
                int healed = VampireHealing.Heal((int)p.Ai1, Time.time);
                if (healed > 0) UI.CombatText.Show(hero.transform.position + Vector3.up * 0.9f, healed.ToString(), UI.CombatText.Heal);
                DevVampireHealed += healed;
                p.Dead = true;
                return;
            }
            var v = TV(p);
            v = (v * 15f + to.normalized * 4f) / 16f;
            p.Vel = Flip(v);
            p.Pos += p.Vel * Px;
            // The motes trail behind across and, by Terraria's sign, ahead down (position.Y -= -velocity.Y × 0.334 × k).
            for (int k = 0; k < 3; k++)
            {
                int d = PointDust(c + new Vector2(-v.x * 0.334f * k, v.y * 0.334f * k), 183, Vector2.zero, 100, 1.1f);
                if (d >= 0) D(d).NoGravity = true;
            }
        }

        // ------------------------------------------------------------------ 497: the Shadowflame Knife

        /// <summary>
        /// AI_002 for 497 (P:23654-23670): a shadowflame mote (27) each update; turned along its
        /// flight for 30 updates, then slowing 1% and falling 0.5 an update.
        /// </summary>
        static void StepShadowflameKnife(Proj p)
        {
            var v = TV(p);
            var c = CenterPx(p);
            int i = BoxDust(c - new Vector2(15f, 15f), 30, 30, 27, v.x, v.y, 100, 1.2f);
            if (i >= 0)
            {
                ref var d = ref D(i);
                d.Position = (d.Position + c) / 2f;
                d.NoGravity = true;
                d.Velocity *= 0.3f;
                d.Velocity -= v * 0.1f;
            }
            if (++p.Ai0 >= 30f)
            {
                v.x *= 0.99f;
                v.y += 0.5f;
            }
            else p.TRot = ToRot(v) + Mathf.PI / 2f;
            if (v.y > 32f) v.y = 32f;
            p.Vel = Flip(v);
        }

        /// <summary>P:11469-11472, 13407-13416: shadowflame 1-3 s; with hits to spare it springs back off the enemy at 90% speed, falling 5 updates later.</summary>
        static void ShadowflameKnifeHit(Proj p, HealthManager hm)
        {
            EnemyDebuffs.Add(hm, EnemyDebuffs.Shadowflame, Random.Range(60, 180));
            if (p.Penetrate == 1) return;
            p.Ai0 = 25f;
            var v = TV(p);
            var away = (EnemyCenterPx(hm) - CenterPx(p)).normalized * v.magnitude;
            p.Vel = Flip(-away * 0.9f);
            DevKnifeBounces++;
        }

        /// <summary>P:74989-75000: fifteen shadowflame motes (27) as it breaks.</summary>
        static void KillShadowflameKnife(Proj p)
        {
            var c = CenterPx(p);
            var v = TV(p);
            for (int k = 0; k < 15; k++)
            {
                int i = BoxDust(c, 10, 10, 27);
                if (i < 0) continue;
                D(i).NoGravity = true;
                D(i).Velocity *= 2f;
                D(i).Velocity -= v * 0.3f;
                D(i).Scale += Random.Range(0, 150) * 0.001f;
            }
        }

        // ------------------------------------------------------------------ 491: the Flying Knife (AI_009 old)

        /// <summary>
        /// AI_009_MagicMissiles_Old for 491 (P:44232-44360): while held it closes on the cursor at
        /// 20 px an update (a fifth of the way each), spinning up near it (within 100 px it speeds
        /// up 10% a step and spins); let go, after 15 updates it flies home at 25 (a sixth a step)
        /// through walls and is gone within 20 px. It turns along its flight while steered,
        /// spins otherwise; sparks (15, 57, 58); gone past 2000 px.
        /// </summary>
        void StepFlyingKnife(Proj p)
        {
            var mod = TerraMod.Instance;
            var c = CenterPx(p);
            var v = TV(p);
            if (p.Ai0 <= 0f)
            {
                if (mod != null && mod.UseHeld && mod.Inventory.Current.Type == ItemType.FlyingKnife)
                {
                    const float speed = 20f;
                    var to = AimPx() - c;
                    float dist = to.magnitude;
                    if (p.Ai0 < 0f) p.Ai0++;
                    if (dist < 100f)
                    {
                        if (v.magnitude < speed)
                        {
                            v *= 1.1f;
                            if (v.magnitude > speed) v = v.normalized * speed;
                        }
                        if (p.Ai0 == 0f) p.Ai0 = -10f;
                    }
                    else if (dist > speed) v = (v * 4f + to / dist * speed) / 5f;
                    else v = to;
                }
                else p.Ai0 = 1f;
            }
            if (++p.LocalAi0 > 15f && p.Ai0 > 0f && Holder(out var home))
            {
                p.Collide = false;
                var back = home - c;
                if (back.magnitude < 20f)
                {
                    Kill(p);
                    return;
                }
                v = (v * 5f + back.normalized * 25f) / 6f;
            }
            if (p.Ai0 < 0f || v.sqrMagnitude < 1e-6f) p.TRot += 0.3f;
            else if (p.Ai0 > 0f) p.TRot += 0.3f * (v.x < 0f ? -1f : 1f);
            else p.TRot = ToRot(v) + 1.57f;
            if (Holder(out var h) && Vector2.Distance(c, h) > 2000f)
            {
                Kill(p);
                return;
            }
            DevFlyingKnife = $"ai0={p.Ai0:F0} t={p.LocalAi0:F0} 距光标={(AimPx() - c).magnitude:F0} 距玩家={(Holder(out var hh) ? (hh - c).magnitude : -1f):F0} 速度={v.magnitude:F1}";
            if (Random.Range(0, 2) == 0)
            {
                int type = Random.Range(0, 3) switch { 0 => 15, 1 => 57, _ => 58 };
                var tl = c - new Vector2(13f, 13f);
                int i = BoxDust(tl, 26, 26, type, v.x * 0.25f, v.y * 0.25f, 255, 0.7f);
                if (i >= 0)
                {
                    D(i).Velocity *= 0.25f;
                    D(i).Position = (D(i).Position + tl) / 2f;
                }
            }
            p.Vel = Flip(v);
        }

        /// <summary>P:18577-18590: off a block it spins up and bounces straight back.</summary>
        static void FlyingKnifeTile(Proj p, bool hitX, bool hitY, Vector2 before)
        {
            if (p.Ai0 <= 0f) p.Ai0 = -10f;
            if (hitX && Mathf.Abs(before.x) > 0f) p.Vel.x = -before.x;
            if (hitY && Mathf.Abs(before.y) > 0f) p.Vel.y = -before.y;
        }

        /// <summary>The Vampire Knives' throw (Pl:48625-48647): four, and one more on each of 1/2, 1/4, 1/8, 1/16; the i-th up to 1.75 × i px off on each axis.</summary>
        public void ThrowVampireKnives(Vector2 from, Vector2 vel, int damage, float knockback, float crit)
        {
            int n = 4;
            if (Random.Range(0, 2) == 0) n++;
            if (Random.Range(0, 4) == 0) n++;
            if (Random.Range(0, 8) == 0) n++;
            if (Random.Range(0, 16) == 0) n++;
            var keep = SpawnClass;
            SpawnClass = DamageClass.Melee;
            for (int k = 0; k < n; k++)
            {
                float spread = 0.05f * k;
                var v = vel + new Vector2(Random.Range(-35, 36) * spread, Random.Range(-35, 36) * spread);
                var p = Spawn(ProjKind.VampireKnife, from, v, damage, knockback, crit);
                if (p != null) p.Melee = true;
            }
            SpawnClass = keep;
            DevVampireThrows++;
        }

        public static string DevFlyingKnife = "";
        public static int DevShieldLeaps, DevVampireOrbs, DevVampireHealed, DevKnifeBounces, DevVampireThrows;
    }
}
