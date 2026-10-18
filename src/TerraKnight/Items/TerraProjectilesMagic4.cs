using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// More spells (Terraria's space, px y down, per update): the Magical Harp's notes (76-78,
    /// AI_021: sent towards the cursor, slowing by a pitch from how far it was), the Clinger
    /// Staff's wall of cursed flame (482, AI_087: one at a time, floor to ceiling up to 15 tiles),
    /// the Toxic Flask (510, thrown, bursting into 20-30 gas clouds, 511-513, AI_092, that drift
    /// apart and hurt once each), the Book of Skulls' homing skulls (837) and the Spirit Flame's
    /// wisps (659, AI_129: appearing round the player, chasing what they see, bursting on it).
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        static void RegisterMagic4()
        {
            void Note(ProjKind k, int tex, int w, int h) => specs[k] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_" + tex, W = w, H = h, Time = 180, Penetrate = 5, LocalCooldown = 30, AlphaStart = 100f / 255f,
                Light = new Color(0.3f, 0.09f, 0.18f), Update = (t, p) => StepHarpNote(p), Draw = (t, fx, p, px) => DrawHarpNote(fx, p, px), OnKill = (t, p) => { },
            };
            Note(ProjKind.HarpNote1, 76, 10, 22);
            Note(ProjKind.HarpNote2, 77, 18, 24);
            Note(ProjKind.HarpNote3, 78, 22, 24);
            specs[ProjKind.ClingerWall] = new Spec
            {
                Ai = Ai.Custom, W = 16, H = 200, Time = 18000, Penetrate = -1, TileCollide = false, TypeCooldown = 10, Look = Look.Invisible,
                Debuffs = new[] { (EnemyDebuffs.CursedInferno, 420, 1) }, Update = (t, p) => t.StepClingerWall(p), OnKill = (t, p) => { },
            };
            specs[ProjKind.ToxicFlask] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_510", W = 18, H = 18, Time = 3600, Penetrate = 1,
                Update = (t, p) => StepToxicFlask(p), Draw = (t, fx, p, px) => DrawTurning(fx, p, px, 18, 18), OnKill = (t, p) => t.KillToxicFlask(p),
            };
            specs[ProjKind.ToxicCloud] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_511", W = 32, H = 32, Scale = 1.1f, Time = 3600, Penetrate = -1, TileCollide = false, TypeCooldown = 10,
                CanDamage = p => p.Ai1 < 1f, OnHit = (t, p, hm) => p.Ai1++, Update = (t, p) => t.StepToxicCloud(p), Draw = (t, fx, p, px) => DrawToxicCloud(fx, p, px),
                OnKill = (t, p) => { },
            };
            specs[ProjKind.Skull] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_837", W = 26, H = 26, Time = 180, MaxTravel = 640, Penetrate = 3, LocalCooldown = 20, TileCollide = false, AlphaStart = 1f,
                Update = (t, p) => t.StepSkull(p), Draw = (t, fx, p, px) => DrawSkull(fx, p, px), OnKill = (t, p) => KillSkull(p),
            };
            specs[ProjKind.SpiritFlame] = new Spec
            {
                Ai = Ai.Custom, Texture = "Projectile_659", W = 8, H = 8, Time = 240, Penetrate = -1, TileCollide = false, AlphaStart = 1f, OwnerImmune = 5,
                OnHit = (t, p, hm) => p.TimeLeft = 1, Update = (t, p) => t.StepSpiritFlame(p), Draw = (t, fx, p, px) => DrawSpiritFlame(fx, p, px),
                OnKill = (t, p) => t.KillSpiritFlame(p),
            };
        }

        // ------------------------------------------------------------------ 76-78: Magical Harp (AI_021)

        /// <summary>A note (velocity px per update y up), its pitch (-1..1) and how fast it slows.</summary>
        public void ShootHarpNote(Vector2 from, Vector2 velocity, int damage, float knockback, float crit, float pitch, float slowing)
        {
            var kind = Random.Range(0, 3) switch { 1 => ProjKind.HarpNote2, 2 => ProjKind.HarpNote3, _ => ProjKind.HarpNote1 };
            var p = Spawn(kind, from, velocity, damage, knockback, crit);
            if (p == null) return;
            p.Ai0 = pitch;
            p.LocalAi1 = slowing;
        }

        /// <summary>AI_021: it tilts with its speed across; it slows (ai[2] an update) to 3|pitch/2 + 1/2| px; shadowflame motes; a harp note (Item26) as it starts.</summary>
        static void StepHarpNote(Proj p)
        {
            var v = TV(p);
            p.TRot = Mathf.Clamp(v.x, -6f, 6f) * 0.1f;
            float max = 3f * Mathf.Abs(p.Ai0 / 2f + 0.5f);
            if (v.magnitude > max) v = v.normalized * (v.magnitude - p.LocalAi1);
            p.Vel = Flip(v);
            if (Random.Range(0, 3) == 0)
            {
                int i = BoxDust(SpearBox(p, out int w, out int h), w, h, 27, 0f, 0f, 80);
                if (i >= 0)
                {
                    D(i).NoGravity = true;
                    D(i).Velocity *= 0.2f;
                }
            }
            if (p.SoundDelay == 0)
            {
                // The harp's note at Main.musicPitch: its pitch rounded to one of six notes either way.
                p.SoundDelay = -1;
                TerraMod.Instance?.PlaySound("Item_26", 1f, Mathf.Round(Mathf.Clamp(p.Ai0, -1f, 1f) * 6f) / 6f);
            }
        }

        static void DrawHarpNote(FxBatch fx, Proj p, Vector2 px)
        {
            var spr = Sprite(p.Spec.Texture);
            fx.Draw(spr, px, new Color(L, L, L, 1f) * (1f - p.Alpha), p.TRot, p.Scale, flipX: p.Dir > 0f);
        }

        // ------------------------------------------------------------------ 482: Clinger Staff (AI_087)

        /// <summary>A Clinger Staff wall: its top and height (Terraria px, y down from the world's px origin as ToPx gives).</summary>
        public void ShootClingerWall(float xPx, float topPx, float heightPx, int damage, float knockback, float crit)
        {
            var center = new Vector2(xPx + 8f, topPx + heightPx * 0.5f);
            var p = Spawn(ProjKind.ClingerWall, TerraParticles.ToWorld(center), Vector2.zero, damage, knockback, crit);
            if (p == null) return;
            p.Ai1 = heightPx;
            p.HitSize = new Vector2(16f, heightPx);
        }

        /// <summary>AI_087: a newer wall puts out the older ones; cursed flames rise all over it (4.5 per 1000 px² an update).</summary>
        void StepClingerWall(Proj p)
        {
            foreach (var q in live)
                if (q != p && !q.Dead && q.Kind == ProjKind.ClingerWall && q.TimeLeft > p.TimeLeft)
                {
                    Kill(p);
                    return;
                }
            float h = p.Ai1;
            var tl = CenterPx(p) - new Vector2(8f, h * 0.5f);
            float n = 16f * h * 0.0045f;
            for (int k = 0; k < n; k++)
            {
                int i = BoxDust(tl, 16, (int)h, 75, 0f, 0f, 100);
                if (i < 0) continue;
                D(i).NoGravity = true;
                D(i).Velocity = D(i).Velocity * 0.5f + new Vector2(0f, -0.5f);
                D(i).Scale = 1.4f;
                D(i).Position += new Vector2(6f, -2f);
            }
        }

        // ------------------------------------------------------------------ 510-513: Toxic Flask

        /// <summary>AI_002 for 510: after 20 updates it falls (0.4) with drag; it turns with its speed across.</summary>
        static void StepToxicFlask(Proj p)
        {
            var v = TV(p);
            if (++p.Ai0 >= 20f)
            {
                p.Ai0 = 20f;
                v.y += 0.4f;
                v.x *= 0.97f;
            }
            if (v.y > 16f) v.y = 16f;
            p.TRot += Mathf.Abs(v.x) * 0.04f * p.Dir;
            p.Vel = Flip(v);
        }

        /// <summary>P:75094: a glassy crash (Item107), two shards, and 20-30 gas clouds of the three kinds, each off at up to 2 px an update.</summary>
        void KillToxicFlask(Proj p)
        {
            Sound("Item_107");
            var v = TV(p);
            var c = CenterPx(p);
            TerraParticles.Instance?.NewGore(c, -v * 0.2f, 704);
            TerraParticles.Instance?.NewGore(c, -v * 0.2f, 705);
            int n = Random.Range(20, 31);
            for (int k = 0; k < n; k++)
            {
                var d = new Vector2(Random.Range(-100, 101), Random.Range(-100, 101));
                d = (d.sqrMagnitude > 0f ? d.normalized : Vector2.up) * Random.Range(10, 201) * 0.01f;
                var cloud = Spawn(ProjKind.ToxicCloud, p.Pos, Flip(d), p.Damage, 1f, p.Crit);
                if (cloud == null) continue;
                cloud.LocalAi1 = Random.Range(0, 3);
                cloud.HitSize = cloud.LocalAi1 == 1f ? new Vector2(44f, 41f) : cloud.LocalAi1 == 2f ? new Vector2(33f, 30f) : new Vector2(35f, 35f);
                cloud.Dir = Random.Range(0, 2) == 0 ? -1f : 1f;
                cloud.TRot = Random.value * Mathf.PI * 2f;
            }
        }

        /// <summary>
        /// AI_092 for 511-513: 260 updates (three times as fast once it has hurt someone, and it
        /// hurts no one after that); it shows in over the first 30% and fades over the rest (to 70%),
        /// turns slowly, slows 4% an update, and the clouds push apart where they overlap.
        /// </summary>
        void StepToxicCloud(Proj p)
        {
            p.Ai0++;
            if (p.Ai1 >= 1f) p.Ai0 += 2f;
            if (p.Ai0 > 260f)
            {
                Kill(p);
                return;
            }
            float t = p.Ai0 / 260f;
            p.Alpha = 1f - Remap(t, 0f, 0.3f, 0f, 1f) * Remap(t, 0.3f, 1f, 1f, 0f) * 0.7f;
            p.TRot += p.Dir * Mathf.PI * 2f / 480f;
            p.Vel *= 0.96f;
            var size = new Vector2(p.Spec.W, p.Spec.H) * Px;
            foreach (var q in live)
            {
                if (q == p || q.Dead || q.Kind != ProjKind.ToxicCloud) continue;
                var d = q.Pos - p.Pos;
                if (Mathf.Abs(d.x) > size.x || Mathf.Abs(d.y) > size.y) continue;
                if (d == Vector2.zero) d = new Vector2(1f, 1f);
                var push = Flip(d.normalized) * 0.005f;
                var pv = Flip(p.Vel);
                p.Vel = Flip(Vector2.Lerp(pv, pv - push, 0.6f));
                var qv = Flip(q.Vel);
                q.Vel = Flip(Vector2.Lerp(qv, qv + push, 0.6f));
            }
        }

        static void DrawToxicCloud(FxBatch fx, Proj p, Vector2 px)
        {
            int n = (int)p.LocalAi1;
            string tex = "Projectile_" + (511 + n);
            int w = n == 1 ? 40 : n == 2 ? 30 : 32, h = n == 1 ? 38 : n == 2 ? 28 : 32;
            fx.Draw(FxBatch.Frame(tex, 0, 0, w, h, w * 0.5f, h * 0.5f), px, new Color(L, L, L, 1f) * (1f - p.Alpha), p.TRot, p.Scale);
        }

        // ------------------------------------------------------------------ 837: Book of Skulls (AI_001)

        /// <summary>
        /// P:57759, bounded for progression: acquire within 240 px (Manhattan),
        /// release beyond 480 px; expire after 3 seconds or 640 px of total travel.
        /// It bends towards the target at its starting speed,
        /// (v·19 + aim)/20; through blocks, but a block takes 33 updates off its life; flames
        /// behind it; three frames, turned to its flight.
        /// </summary>
        void StepSkull(Proj p)
        {
            p.Alpha = Mathf.Max(0f, p.Alpha - 75f / 255f);
            if (++p.Frame > 2) p.Frame = 0;
            var v = TV(p);
            var c = CenterPx(p);
            int dd = BoxDust(c - new Vector2(9f, 9f) - v * 2f, 18, 18, 6, v.x * 0.2f, v.y * 0.2f, 100, 2f);
            if (dd >= 0)
            {
                D(dd).NoGravity = true;
                D(dd).Velocity *= 0.3f;
            }
            dd = BoxDust(c - new Vector2(9f, 9f) - v * 2f, 18, 18, 6, v.x * 0.2f, v.y * 0.2f, 100, 2f);
            if (dd >= 0)
            {
                D(dd).NoGravity = true;
                D(dd).Velocity *= 0.3f;
            }
            if (p.TimeLeft > 33 && TerraWorld.IsSolidAt(p.Pos)) p.TimeLeft -= 33;
            if (p.LocalAi0 == 0f) p.LocalAi0 = v.magnitude;
            if (p.Target == null)
            {
                float best = 240f;
                foreach (var hm in EnemiesNear(c, 240f))
                {
                    var e = EnemyCenterPx(hm);
                    float m = Mathf.Abs(c.x - e.x) + Mathf.Abs(c.y - e.y);
                    if (m < best && InSight(c, e))
                    {
                        best = m;
                        p.Target = hm;
                    }
                }
            }
            if (p.Target != null)
            {
                if (!Chaseable(p.Target)) p.Target = null;
                else
                {
                    var e = EnemyCenterPx(p.Target);
                    if (Mathf.Abs(c.x - e.x) + Mathf.Abs(c.y - e.y) < 480f)
                    {
                        var want = (e - c).normalized * p.LocalAi0;
                        v = (v * 19f + want) / 20f;
                    }
                    else p.Target=null;
                }
            }
            p.TRot = p.Dir < 0f ? Mathf.Atan2(-v.y, -v.x) : Mathf.Atan2(v.y, v.x);
            p.Vel = Flip(v);
        }

        /// <summary>GetAlpha for 837: nothing while fading in, then near white, mostly solid.</summary>
        static void DrawSkull(FxBatch fx, Proj p, Vector2 px)
        {
            if (p.Alpha > 0f) return;
            fx.Draw(FxBatch.Frame("Projectile_837", 0, p.Frame * 30, 26, 30, 13f, 15f), px, FxBatch.C(255, 255, 255, 200), p.TRot, p.Scale, flipX: p.Dir < 0f);
        }

        /// <summary>P:75817: a crack if it hit something, and bone and fire.</summary>
        static void KillSkull(Proj p)
        {
            if (p.Penetrate != p.Spec.Penetrate) Sound("Dig_" + Random.Range(0, 3));
            var v = TV(p);
            var tl = CenterPx(p) - new Vector2(13f, 13f);
            for (int k = 0; k < 20; k++)
            {
                int i = BoxDust(tl, 26, 26, 26, 0f, 0f, 100, 1.3f);
                if (i >= 0)
                {
                    D(i).NoGravity = true;
                    D(i).Velocity = D(i).Velocity * 1.2f - v * 0.3f;
                }
                i = BoxDust(tl + new Vector2(4f, 4f), 18, 18, 6, 0f, 0f, 100, 2f);
                if (i >= 0)
                {
                    D(i).NoGravity = true;
                    D(i).Velocity *= 3f;
                }
            }
        }

        // ------------------------------------------------------------------ 659: Spirit Flame (AI_129)

        /// <summary>A Spirit Flame wisp, still where it appears.</summary>
        public void ShootSpiritFlame(Vector2 at, int damage, float knockback, float crit)
        {
            var p = Spawn(ProjKind.SpiritFlame, at, Vector2.zero, damage, knockback, crit);
            if (p == null) return;
            p.Ai0 = -2f;
        }

        /// <summary>
        /// AI_129 for 659: every 5 updates it looks for the nearest enemy within 400 px it can see
        /// (centre or top), and with one its life starts over at 420; it closes in at 7.5 px an
        /// update, (v·2 + aim)/3. Without one it lingers at most 240 updates, losing one, 5. Gone
        /// in a block. It fades in, four frames, three shadowflame motes circling it, bigger near the end.
        /// </summary>
        void StepSpiritFlame(Proj p)
        {
            var c = CenterPx(p);
            if (p.LocalAi0 > 0f) p.LocalAi0--;
            if (p.LocalAi0 == 0f && p.Ai0 < 0f)
            {
                p.LocalAi0 = 5f;
                HealthManager best = null;
                float bd = 400f;
                foreach (var hm in EnemiesNear(c, 400f))
                {
                    var e = EnemyCenterPx(hm);
                    float d = Vector2.Distance(c, e);
                    if (d < bd && InSight(c, e))
                    {
                        bd = d;
                        best = hm;
                    }
                }
                if (best != null)
                {
                    p.Target = best;
                    p.Ai0 = 0f;
                    p.TimeLeft = 420;
                }
            }
            if (p.TimeLeft > 30) p.Alpha = Mathf.Max(0f, p.Alpha - 12f / 255f);
            else p.Alpha = 1f - p.TimeLeft / 30f;
            if (++p.FrameCounter > 4)
            {
                p.FrameCounter = 0;
                if (++p.Frame >= 4) p.Frame = 0;
            }
            float scale = p.TimeLeft < 60 ? 1.6f : p.TimeLeft < 120 ? 1.1f : 0.5f;
            p.Ai1++;
            for (int k = 0; k < 3; k++)
            {
                if (Random.Range(0, 3) != 0) continue;
                var at = c + RotateBy(Vector2.down, k * Mathf.PI * 2f / 3f + p.Ai1) * 10f;
                int i = PointDust(at, 27, (at - c).normalized, 200, scale);
                if (i < 0) continue;
                D(i).NoGravity = true;
                D(i).FadeIn = 0.5f;
            }
            if (p.TimeLeft > 2 && TerraWorld.IsSolidAt(p.Pos)) p.TimeLeft = 2;
            if (p.Ai0 >= 0f)
            {
                if (p.Target != null && Chaseable(p.Target))
                {
                    var e = EnemyCenterPx(p.Target);
                    if (Vector2.Distance(c, e) > 1f)
                    {
                        var v = TV(p);
                        p.Vel = Flip((v * 2f + (e - c).normalized * 7.5f) / 3f);
                    }
                    return;
                }
                p.Ai0 = -1f;
            }
            if (p.Ai0 == -1f && p.TimeLeft > 5) p.TimeLeft = 5;
            if (p.Ai0 == -2f && p.TimeLeft > 240) p.TimeLeft = 240;
        }

        /// <summary>GetAlpha for 659: white, half added, fading.</summary>
        static void DrawSpiritFlame(FxBatch fx, Proj p, Vector2 px)
        {
            int a = Mathf.RoundToInt(p.Alpha * 255f);
            fx.Draw(FxBatch.Frame("Projectile_659", 0, p.Frame * 30, 20, 30, 10f, 15f), px, FxBatch.C(255 - a, 255 - a, 255 - a, 128 - a / 2), 0f, p.Scale);
        }

        /// <summary>P:73798: once it has chased someone, it bursts over 80 px (Item14), a ring and a puff of shadowflame and smoke.</summary>
        void KillSpiritFlame(Proj p)
        {
            if (p.Ai0 < 0f) return;
            Blast(p, 80f, p.Damage, p.Knockback);
            Sound("Item_14");
            var c = CenterPx(p);
            for (int k = 0; k < 30; k++)
            {
                int i = PointDust(c, 27, Vector2.zero, 0, 2f + Random.value * 0.5f);
                if (i < 0) continue;
                D(i).NoGravity = true;
                if (k < 15)
                {
                    D(i).FadeIn = 1.5f + Random.value * 0.5f;
                    D(i).Velocity = RotateBy(Vector2.down, (k + 1) / 15f * Mathf.PI * 2f) * (5f + Random.value * 1.5f);
                }
                else
                {
                    D(i).Position = c + RotateBy(Vector2.down, Random.Range(-Mathf.PI, Mathf.PI)) * Random.value * 80f / 3f;
                    D(i).FadeIn = 0.5f + Random.value * 0.5f;
                    D(i).Velocity = Random.insideUnitCircle * 2f;
                }
            }
            for (int k = 0; k < 10; k++)
            {
                int i = PointDust(c + RotateBy(Vector2.right, Random.Range(-Mathf.PI, Mathf.PI)) * 80f / 3f, 31, Random.insideUnitCircle * 1.5f, 0, 1.5f);
                if (i < 0) continue;
                D(i).FadeIn = 0.5f + Random.value * 0.5f;
                D(i).NoGravity = true;
            }
        }
    }
}
