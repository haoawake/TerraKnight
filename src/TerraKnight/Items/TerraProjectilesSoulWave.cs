using TerraKnight.Effects;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The Vengeful Spirit's weapons' shots (Items/SoulWave.cs; Terraria's space, px y down, per update), drawn in the pack's
    /// bone white, pale blue and blue grey with its own frames and point-sampled pictures, the glow added (FxBatch, alpha 0):
    /// the Revenant Edge's crescent (Terraria's 1.4 swing slash, Projectile_982, recoloured; drawn only) and its soul blade (the
    /// wave's frames, small), the wraith a blow calls out of an enemy (rises, gathers itself, then flies at the nearest one); the
    /// rifle's laser (its muzzle star, its sparks), its charge (an orb swelling at the muzzle, souls drawn into it, three cells
    /// lighting, a shiver near the end) and the charged wave (afterimages, a stream of souls, motes about it, a burst where it
    /// ends); the spell's wave (its ring on casting, afterimages, wisps, coming apart in four or five wisps). Each draws from
    /// what a LAN game sends (its frame, its age, its parent's aim), so another player's shows the same.
    /// </summary>
    internal sealed partial class TerraProjectiles
    {
        static void RegisterSoulWave()
        {
            specs[ProjKind.SoulSlash] = new Spec
            {
                Ai = Ai.Custom, W = 4, H = 4, Time = 3600, Penetrate = -1, TileCollide = false, OwnMove = true, Held = true, HoldsWhile = q => false,
                CanDamage = p => false, Update = (t, p) => t.StepSoulSlash(p), Draw = (t, fx, p, px) => DrawSoulSlash(fx, p, px), OnKill = (t, p) => { },
            };
            specs[ProjKind.SoulBlade] = new Spec
            {
                Ai = Ai.Custom, Texture = SoulWave.Tex("flight"), W = 24, H = 24, Time = 32, Penetrate = 3, OncePerNpc = true, TrailLength = 6,
                TileBox = new Vector2(10f, 10f), Light = new Color(0.3f, 0.42f, 0.62f),
                Update = (t, p) => t.StepSoulBlade(p), Draw = (t, fx, p, px) => DrawSoulWave(fx, p, px, 0.7f, 32),
                OnHit = (t, p, hm) => t.SoulSpark(p, hm, 6, 0.55f), OnKill = (t, p) => t.SoulFade(p, 7, 0.6f, false),
            };
            specs[ProjKind.SoulWraith] = new Spec
            {
                Ai = Ai.Custom, Texture = SoulWave.Tex("minion"), W = 18, H = 18, Time = 180, Penetrate = 1, TileCollide = false, OncePerNpc = true, TrailLength = 7,
                Light = new Color(0.28f, 0.38f, 0.55f),
                // Harmless till it strikes: the sword's through its flight out and its circle, the split's through its rise.
                CanDamage = p => p.Ai0 >= (p.LocalAi0 == 1f ? 8f : WraithOut + WraithCircle),
                Update = (t, p) => t.StepWraith(p), Draw = (t, fx, p, px) => DrawWraith(fx, p, px),
                OnHit = (t, p, hm) => { Sound("tk_wraith_strike", 0.7f); t.SoulSpark(p, hm, 7, 0.5f); }, OnKill = (t, p) => t.SoulFade(p, 6, 0.5f, false),
            };
            // The beam's blow: unseen, along the line from the muzzle (Point) to where the beam stopped, once; gone on its hit or a moment after.
            specs[ProjKind.PulseLaser] = new Spec
            {
                Ai = Ai.Custom, Texture = SoulWave.Tex("laser"), W = 8, H = 8, Time = 3, Penetrate = 1, TileCollide = false, OwnMove = true,
                Beam = p => (p.Point, CenterPx(p), 6f),
                Update = (t, p) => { }, Draw = (t, fx, p, px) => { },
                OnHit = (t, p, hm) => t.LaserSpark(p, true), OnKill = (t, p) => { },
            };
            // The beam as seen: the whole line from the muzzle (Point) to where it stopped (Pos), fading.
            specs[ProjKind.PulseBeam] = new Spec
            {
                Ai = Ai.Custom, Texture = SoulWave.Tex("laser"), W = 4, H = 4, Time = BeamTicks, Penetrate = -1, TileCollide = false, OwnMove = true,
                Light = new Color(0.25f, 0.4f, 0.6f), CanDamage = p => false,
                Update = (t, p) => { }, Draw = (t, fx, p, px) => DrawPulseBeam(fx, p, px), OnKill = (t, p) => { },
            };
            specs[ProjKind.PulseCharge] = new Spec
            {
                Ai = Ai.Custom, W = 4, H = 4, Time = 2, Penetrate = -1, TileCollide = false, OwnMove = true, Held = true,
                CanDamage = p => false, Update = (t, p) => t.StepPulseCharge(p), Draw = (t, fx, p, px) => DrawPulseCharge(fx, p, px), OnKill = (t, p) => { },
            };
            specs[ProjKind.ChargedSoulWave] = new Spec
            {
                Ai = Ai.Custom, Texture = SoulWave.Tex("charged"), W = 40, H = 36, Time = 80, Penetrate = 1, TrailLength = 8,
                TileBox = new Vector2(14f, 14f), Light = new Color(0.5f, 0.66f, 0.9f),
                Update = (t, p) => t.StepChargedWave(p), Draw = (t, fx, p, px) => DrawChargedWave(fx, p, px),
                OnHit = (t, p, hm) => { t.SoulSpark(p, hm, 12, 0.85f); t.SoulBlast(p, hm, SoulWave.WaveBlastPx, SoulWave.WaveBlast, true); },
                OnKill = (t, p) => { if (p.LocalAi1 != 9f) t.SoulFade(p, 26, 1f, true); },
            };
            specs[ProjKind.SpiritWave] = new Spec
            {
                Ai = Ai.Custom, Texture = SoulWave.Tex("flight"), W = 28, H = 26, Time = 60, Penetrate = 1, TrailLength = 6,
                TileBox = new Vector2(12f, 12f), Light = new Color(0.42f, 0.56f, 0.8f),
                Update = (t, p) => t.StepSpiritWave(p), Draw = (t, fx, p, px) => DrawSpiritWave(fx, p, px),
                OnHit = (t, p, hm) => { t.SoulSpark(p, hm, 8, 0.7f); t.SoulBlast(p, hm, SoulWave.SpellBlastPx, SoulWave.SpellBlast, false); },
                OnKill = (t, p) => { if (p.LocalAi1 != 9f) t.SoulFade(p, 14, 0.8f, true); },
            };
        }

        static void RegisterSoulBurst()
        {
            specs[ProjKind.SoulBurst] = new Spec
            {
                Ai = Ai.Custom, W = 4, H = 4, Time = 16, Penetrate = -1, TileCollide = false, OwnMove = true, CanDamage = p => false,
                Update = (t, p) => { }, Draw = (t, fx, p, px) => DrawSoulBurst(fx, p, px), OnKill = (t, p) => { },
            };
        }

        static float Remap01(float v, float a, float b) => Mathf.Clamp01((v - a) / (b - a));

        public static int DevSoulBlasts, DevSoulBlastHits, DevSplits, DevSplitWraiths;

        /// <summary>
        /// A soul wave bursting on the enemy it touched (the rifle's charged one, the spell's): a blast round it of radiusPx (a
        /// box, as Terraria's explosions), its share of the wave's damage to every other enemy in it (the one touched took the
        /// wave's own), nothing to the player or the ground; its ring and flash (SoulBurst), souls flung out, its sound.
        /// </summary>
        /// <summary>Dev: the last blast's reach (px).</summary>
        public static float DevLastBlastPx;

        void SoulBlast(Proj p, HealthManager touched, float radiusPx, float share, bool big)
        {
            p.LocalAi1 = 9f;
            DevSoulBlasts++;
            var melee = TerraMod.Instance?.Melee;
            overlaps.Clear();
            var filter = new ContactFilter2D { useTriggers = true, useLayerMask = true, layerMask = MeleeHits.HitMask };
            // Shaman Stone's 30% on a magic blast's reach; its picture (the burst, a magic shot of its own) grows by its own.
            float reach = radiusPx * p.Grow;
            DevLastBlastPx = reach;
            Physics2D.OverlapBox(p.Pos, Vector2.one * (reach * 2f * Px), 0f, filter, overlaps);
            var done = new System.Collections.Generic.HashSet<int>();
            if (touched != null) done.Add(touched.GetInstanceID());
            int damage = Mathf.Max(1, Mathf.RoundToInt(p.Damage * share));
            if (melee != null)
                foreach (var col in overlaps)
                {
                    var hm = col.GetComponentInParent<HealthManager>();
                    if (hm == null || hm.isDead || !done.Add(hm.GetInstanceID())) continue;
                    float facing = col.bounds.center.x >= p.Pos.x ? 1f : -1f;
                    if (melee.HitWith(col, damage, p.Knockback * 0.5f, p.Crit, facing, p.Pos, Equipment.Current.ArmorPen + Forging.RangedPen(p.RangedForge),
                        critDamage: Forging.RangedCritDamage(p.RangedForge))) DevSoulBlastHits++;
                }
            var burst = Spawn(ProjKind.SoulBurst, p.Pos, Vector2.zero, 0, 0f, 0f);
            if (burst != null) burst.Ai0 = radiusPx;
            var at = CenterPx(p);
            int n = big ? 26 : 16;
            for (int k = 0; k < n; k++)
            {
                float a = k / (float)n * Mathf.PI * 2f;
                var v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(2.5f, 4.5f) * (big ? 1.2f : 1f);
                SoulDust(at, v, Random.Range(0.9f, 1.5f), k % 4 == 0 ? 226 : 180);
            }
            for (int k = 0; k < (big ? 6 : 4); k++) SoulDust(at + Random.insideUnitCircle * 8f, new Vector2(Random.Range(-1f, 1f), -Random.Range(1f, 2.2f)), 1.5f);
            Sound("tk_soul_burst", big ? 0.85f : 0.65f);
            if (big) Player.TerraCamera.Punch(0.08f, 0.18f);
        }

        /// <summary>The burst: a ring opening to its radius (Ai0), a flash at its heart, both fading over its 16 updates.</summary>
        static void DrawSoulBurst(FxBatch fx, Proj p, Vector2 px)
        {
            float k = Mathf.Clamp01(p.Ticks / 16f), o = 1f - k;
            float r = Mathf.Max(16f, p.Ai0);
            var ring = FxBatch.Whole(SoulWave.Tex("ring"));
            fx.Draw(ring, px, SoulWave.Glow(SoulWave.Pale, 0.9f * o), 0f, (r / 10f) * (0.35f + 0.65f * Mathf.Sqrt(k)), overPlayer: true);
            fx.Draw(ring, px, SoulWave.Glow(SoulWave.Cyan, 0.5f * o), 0f, (r / 10f) * (0.2f + 0.5f * k), overPlayer: true);
            var star = FxBatch.Whole(SoulWave.Tex("flash"));
            fx.Draw(star, px, SoulWave.Glow(SoulWave.Bone, o * o), 0f, 1.2f + r / 30f * (1f - o), overPlayer: true);
            // A bell's shock (Ai1 1): the ring above all, little of the white middle.
            float middle = p.Ai1 == 1f ? 0.25f : 1f;
            fx.Draw(FxBatch.Whole(SoulWave.Tex("orb")), px, SoulWave.Glow(SoulWave.White, 0.8f * o * o * middle), 0f, r / 16f * (0.6f + 0.4f * o) * (p.Ai1 == 1f ? 0.6f : 1f), overPlayer: true);
            fx.Light(px, new Color(0.6f, 0.75f, 1f) * o, r / 10f);
        }

        /// <summary>
        /// The right button with the Vengeful Spirit in hand: each of its waves still flying comes apart into 15 small wraiths,
        /// flung out all round and each going for the nearest enemy, a share of the wave's damage each.
        /// </summary>
        public void SplitSpiritWaves()
        {
            var waves = new System.Collections.Generic.List<Proj>();
            foreach (var q in live)
                if (!q.Dead && q.Kind == ProjKind.SpiritWave) waves.Add(q);
            foreach (var w in waves)
            {
                int dmg = Mathf.Max(1, Mathf.RoundToInt(w.Damage * SoulWave.SplitShare));
                var at = CenterPx(w);
                float spin = Random.value * Mathf.PI * 2f;
                for (int k = 0; k < SoulWave.SplitCount; k++)
                {
                    float a = spin + k / (float)SoulWave.SplitCount * Mathf.PI * 2f;
                    var vel = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(3.5f, 5f) + w.Vel * 0.25f;
                    var s = Spawn(ProjKind.SoulWraith, w.Pos, vel, dmg, w.Knockback * 0.3f, w.Crit);
                    if (s == null) continue;
                    s.LocalAi0 = 1f;
                    s.Class = DamageClass.Magic;
                    DevSplitWraiths++;
                }
                for (int k = 0; k < 18; k++) SoulDust(at, Random.insideUnitCircle * 4f, 1.2f);
                w.LocalAi1 = 9f;
                Kill(w);
                DevSplits++;
            }
            if (waves.Count > 0)
            {
                Sound("tk_wraith_rise", 0.7f);
                Sound("tk_spirit_fade", 0.5f);
            }
        }

        /// <summary>A soul dust (180, the dungeon spirits' pale blue) that floats.</summary>
        static void SoulDust(Vector2 at, Vector2 vel, float scale, int type = 180)
        {
            int i = PointDust(at, type, vel, 0, scale);
            if (i < 0) return;
            D(i).NoGravity = true;
            D(i).Velocity = vel;
        }

        // ------------------------------------------------------------------ the Revenant Edge

        public static int DevSlashes, DevWraiths;

        /// <summary>
        /// A swing of the Revenant Edge: its crescent over the player for the swing (Ai0 the way it faces, Ai1 its length in
        /// updates), drawn only: what it shoots is the wraiths its blows call out (SpawnWraith).
        /// </summary>
        public void SoulSwing(Vector2 from, Vector2 mouse, Vector2 vel, int damage, float knockback, float crit, int useTicks)
        {
            float d = mouse.x < from.x ? -1f : 1f;
            // The crescent is only drawn: it never holds the next swing back (HoldsWhile), the last one gives way to it.
            foreach (var q in live)
                if (!q.Dead && q.Kind == ProjKind.SoulSlash) Kill(q);
            var slash = Spawn(ProjKind.SoulSlash, from, new Vector2(d, 0f), 0, 0f, 0f);
            if (slash != null)
            {
                slash.Ai0 = d;
                slash.Ai1 = Mathf.Max(6f, useTicks);
                slash.Scale = ItemDb.Get(ItemType.RevenantEdge).Scale * Equipment.ItemScale(ItemDb.Get(ItemType.RevenantEdge));
                DevSlashes++;
            }
        }

        void StepSoulSlash(Proj p)
        {
            if (!Holder(out var centre))
            {
                Kill(p);
                return;
            }
            p.LocalAi0++;
            if (p.LocalAi0 > p.Ai1)
            {
                Kill(p);
                return;
            }
            p.Pos = TerraParticles.ToWorld(centre);
            p.TimeLeft = 2;
            // Souls flung off the blade's tip as it sweeps.
            float t = p.LocalAi0 / p.Ai1;
            if (t > 0.15f && t < 0.9f && Random.value < 0.7f)
            {
                float a = SlashRot(p, t) + p.Ai0 * Mathf.PI * 0.25f;
                var tip = centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 66f * p.Scale;
                var tangent = new Vector2(Mathf.Cos(a + p.Ai0 * Mathf.PI / 2f), Mathf.Sin(a + p.Ai0 * Mathf.PI / 2f));
                SoulDust(tip, tangent * Random.Range(1f, 3f), Random.Range(0.7f, 1.1f));
            }
        }

        /// <summary>AI_190's turn: half a circle over the swing, from behind the head to before the feet.</summary>
        static float SlashRot(Proj p, float t) => Mathf.PI * p.Ai0 * t + (p.Ai0 < 0f ? Mathf.PI : 0f) + p.Ai0 * Mathf.PI;

        /// <summary>Main.DrawProj_Excalibur's layers on Projectile_982, in the soul's colours: the grey body behind, the pale and bone glow, the white streaks, glints along its edge.</summary>
        static void DrawSoulSlash(FxBatch fx, Proj p, Vector2 px)
        {
            float t = Mathf.Clamp01(p.LocalAi0 / Mathf.Max(1f, p.Ai1));
            float n = Remap01(t, 0f, 0.5f) * (1f - Remap01(t, 0.6f, 1f));
            if (n <= 0.01f) return;
            float d = p.Ai0, rot = SlashRot(p, t), S = (0.74f + 0.42f * t) * p.Scale;
            bool flip = d < 0f;
            var r0 = FxBatch.Frame("Projectile_982", 0, 0, 170, 170, 85f, 85f);
            var r3 = FxBatch.Frame("Projectile_982", 0, 3 * 170, 170, 170, 85f, 85f);
            fx.Draw(r0, px, SoulWave.Slate * (0.75f * n), rot - d * (Mathf.PI / 4f) * (1f - t), S * 0.95f, flip);
            fx.Draw(r0, px, SoulWave.Glow(SoulWave.Pale, 0.5f * n), rot, S, flip);
            fx.Draw(r0, px, SoulWave.Glow(SoulWave.Bone, 0.45f * n), rot, S * 0.975f, flip);
            fx.Draw(r3, px, SoulWave.Glow(SoulWave.White, 0.6f * n), rot + d * 0.01f, S, flip);
            fx.Draw(r3, px, SoulWave.Glow(SoulWave.Pale, 0.45f * n), rot - d * 0.05f, S * 0.8f, flip);
            fx.Draw(r3, px, SoulWave.Glow(SoulWave.Cyan, 0.35f * n), rot - d * 0.1f, S * 0.6f, flip);
            for (int k = 0; k < 8; k++)
            {
                float a = rot - d * k * (2f * Mathf.PI * 0.025f) + Remap01(t, 0f, 1f) * (Mathf.PI / 4f) * d;
                fx.Sparkle(1f, px + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 79f * S, new Color(1f, 1f, 1f, 0f) * (n * (k / 9f)), SoulWave.Cyan, t, 0f, 0.5f, 0.5f, 1f,
                    a, new Vector2(0f, Mathf.Lerp(3f, 0f, t)) * S, new Vector2(S, S));
            }
            float am = rot + Remap01(t, 0f, 1f) * (Mathf.PI / 4f) * d;
            fx.Sparkle(1f, px + new Vector2(Mathf.Cos(am), Mathf.Sin(am)) * 81f * S, new Color(1f, 1f, 1f, 0f) * (n * 0.6f), SoulWave.Pale, t, 0f, 0.5f, 0.5f, 1f,
                0f, new Vector2(2f, Mathf.Lerp(4f, 1f, t)) * S, new Vector2(S, S));
        }

        void StepSoulBlade(Proj p)
        {
            AnimateWave(p);
            p.Vel *= 0.985f;
            p.TRot = Mathf.Atan2(-p.Vel.y, p.Vel.x);
            if (Random.value < 0.6f)
            {
                var c = CenterPx(p);
                var back = -TV(p).normalized;
                SoulDust(c + back * 12f + Random.insideUnitCircle * 4f, back * 0.6f + Random.insideUnitCircle * 0.3f, Random.Range(0.6f, 0.9f));
            }
        }

        /// <summary>The wave's eight frames at 12 a second (an update each 60th: a frame every 5).</summary>
        static void AnimateWave(Proj p)
        {
            if (++p.FrameCounter >= 5)
            {
                p.FrameCounter = 0;
                p.Frame = (p.Frame + 1) % 8;
            }
        }

        /// <summary>
        /// The soul wave's frame (48×32, facing along its flight, kept upright going left), its afterimages along its trail
        /// fading, a pale halo added round it; fading in over its first updates and out over its last (its age, Ticks).
        /// </summary>
        static void DrawSoulWave(FxBatch fx, Proj p, Vector2 px, float scale, int life)
        {
            float age = p.Ticks;
            float o = Remap01(age, 0f, 4f) * Remap01(life - age, 0f, 8f);
            if (o <= 0.01f) return;
            var dir = TV(p);
            bool left = dir.x < 0f;
            float rot = Mathf.Atan2(dir.y, dir.x);
            var frame = FxBatch.Frame(SoulWave.Tex("flight"), 0, (p.Frame & 7) * 32, 48, 32, 24f, 16f);
            int n = p.Trail.Count;
            for (int k = n - 1; k >= 1; k--)
            {
                float f = 1f - k / (float)(n + 1);
                fx.Draw(frame, TerraParticles.ToPx(p.Trail[k]), SoulWave.Glow(SoulWave.Pale, 0.32f * f * o), rot, scale * (0.8f + 0.2f * f), flipY: left);
            }
            float pulse = 1f + 0.06f * Mathf.Sin(age * 0.4f);
            fx.Draw(frame, px, SoulWave.Glow(SoulWave.Cyan, 0.3f * o), rot, scale * 1.12f * pulse, flipY: left);
            fx.Draw(frame, px, Color.white * o, rot, scale, flipY: left);
            fx.Light(px, new Color(0.35f, 0.5f, 0.75f) * o, 3f * scale);
        }

        /// <summary>Where a shot or blow lands on an enemy: souls bursting off it, pale sparkles, a struck sound.</summary>
        void SoulSpark(Proj p, HealthManager hm, int count, float strength)
        {
            var at = hm != null ? TerraParticles.ToPx(Centre(hm)) : CenterPx(p);
            var dir = TV(p).sqrMagnitude > 1e-6f ? TV(p).normalized : Vector2.right;
            for (int k = 0; k < count; k++)
            {
                var v = (dir * Random.Range(1f, 4f) + Random.insideUnitCircle * 2.5f) * strength;
                SoulDust(at + Random.insideUnitCircle * 6f, v, Random.Range(0.8f, 1.4f) * Mathf.Max(0.7f, strength));
            }
            for (int k = 0; k < Mathf.Max(1, count / 3); k++) SoulDust(at, Random.insideUnitCircle * 3f, 0.9f, 15);
            Sound(p.Kind == ProjKind.SpiritWave ? "tk_spirit_hit_*" : "tk_soul_hit_*", 0.45f + 0.35f * strength);
        }

        /// <summary>Where a soul shot comes apart: a ring of souls, and for the waves wisps drifting up and away, a fading sigh.</summary>
        void SoulFade(Proj p, int count, float strength, bool wisps)
        {
            var at = CenterPx(p);
            for (int k = 0; k < count; k++)
            {
                float a = k / (float)count * Mathf.PI * 2f + Random.value * 0.3f;
                SoulDust(at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 6f * strength, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(1.2f, 2.8f) * strength, Random.Range(0.8f, 1.3f));
            }
            if (!wisps) return;
            int w = Random.Range(4, 6);
            for (int k = 0; k < w; k++)
                SoulDust(at + Random.insideUnitCircle * 10f, new Vector2(Random.Range(-1.2f, 1.2f), -Random.Range(1f, 2.4f)) * strength, Random.Range(1.3f, 1.7f));
            Sound("tk_spirit_fade", 0.55f * strength);
        }

        /// <summary>A blow of the Revenant Edge that landed: a wraith out of the enemy (at most one in a fifth of a second), half the blow, going for the nearest enemy.</summary>
        public void SpawnWraith(Vector2 atPx, int damage, float knockback, float crit, float facing, HealthManager struck = null)
        {
            if (Time.time - lastWraith < 0.2f) return;
            lastWraith = Time.time;
            float side = facing < 0f ? -1f : 1f;
            // Out of the enemy's far side, a little above its middle, flying on out and up.
            var from = atPx + new Vector2(side * 10f, -8f);
            var outward = new Vector2(side * Random.Range(3.6f, 4.6f), -Random.Range(2.6f, 3.6f));
            var p = Spawn(ProjKind.SoulWraith, TerraParticles.ToWorld(from), Flip(outward), Mathf.Max(1, Mathf.RoundToInt(damage * SoulWave.WraithShare)), knockback * 0.5f, crit);
            if (p == null) return;
            p.Target = struck;
            // Its circle's way: on from its flight out (up and over, away from the player).
            p.Ai1 = -side;
            p.Melee = true;
            p.Class = DamageClass.Melee;
            DevWraiths++;
            Sound("tk_wraith_rise", 0.55f);
            for (int k = 0; k < 6; k++) SoulDust(atPx + Random.insideUnitCircle * 5f, new Vector2(0f, -1.5f) + Random.insideUnitCircle, 1.1f);
        }

        static float lastWraith = -1f;

        /// <summary>The sword's wraith's flight: out (updates), round its enemy once (updates, radians an update), then the dive.</summary>
        const float WraithOut = 14f, WraithCircle = 44f, WraithTurn = 0.145f;

        /// <summary>
        /// The sword's wraith: out of the enemy, flying out and slowing (WraithOut); a whole circle round it (WraithCircle,
        /// Ai1 its way; a radius by the enemy's size), chasing a point that runs round; then the dive straight at it, its
        /// touch hurting only now (the spec's CanDamage). Its enemy is the one struck (Proj.Target), the nearest in sight
        /// within 30 tiles when that one is gone; with none it drifts. Frames 4-5 throughout (turned along its flight).
        /// The split's little wraiths (LocalAi0 1): a short rise (8, frames 2-3), then they home.
        /// </summary>
        void StepWraith(Proj p)
        {
            p.Ai0++;
            bool small = p.LocalAi0 == 1f;
            if (!small)
            {
                var target = p.Target;
                if (target == null || target.isDead || !target.gameObject.activeInHierarchy) target = p.Target = Nearest(p, 480f);
                p.Frame = 4 + ((int)p.Ai0 / 4) % 2;
                var me = CenterPx(p);
                if (p.Ai0 <= WraithOut || target == null)
                {
                    p.Vel *= target == null && p.Ai0 > WraithOut ? 0.96f : 0.9f;
                    if (target == null && p.Ai0 > WraithOut) p.Frame = ((int)p.Ai0 / 10) % 2;
                }
                else
                {
                    var c = TerraParticles.ToPx(Centre(target));
                    var rel = me - c;
                    if (p.Ai0 <= WraithOut + WraithCircle)
                    {
                        var col = target.GetComponent<Collider2D>();
                        float r = Mathf.Clamp((col != null ? Mathf.Max(col.bounds.extents.x, col.bounds.extents.y) / Px : 24f) + 36f, 48f, 110f);
                        float a = Mathf.Atan2(rel.y, rel.x) + p.Ai1 * WraithTurn;
                        var goal = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                        var want = goal - me;
                        want = want.sqrMagnitude > 1e-4f ? want.normalized * Mathf.Min(want.magnitude, 11f) : Vector2.zero;
                        p.Vel = Vector2.Lerp(p.Vel, Flip(want), 0.4f);
                    }
                    else
                    {
                        if (p.Ai0 == WraithOut + WraithCircle + 1f) Sound("tk_wraith_rise", 0.35f);
                        var want = Flip((c - me).normalized) * 14f;
                        p.Vel = (p.Vel * 2f + want) / 3f;
                    }
                }
                if (Random.value < 0.7f) SoulDust(me - TV(p).normalized * 6f, -TV(p) * 0.15f, Random.Range(0.6f, 1f));
                p.TRot = Mathf.Atan2(-p.Vel.y, p.Vel.x);
                return;
            }
            if (p.Ai0 < 8f)
            {
                p.Vel *= 0.88f;
                p.Vel += new Vector2(Mathf.Sin(p.Ai0 * 0.7f) * 0.15f, 0f);
                p.Frame = 2 + ((int)p.Ai0 / 6) % 2;
            }
            else
            {
                var target = Nearest(p, 480f);
                if (target != null)
                {
                    var to = TerraParticles.ToPx(Centre(target)) - CenterPx(p);
                    var want = Flip(to.normalized) * (small ? 12f : 11f);
                    p.Vel = (p.Vel * (small ? 7f : 10f) + want) / (small ? 8f : 11f);
                    p.Frame = 4 + ((int)p.Ai0 / 4) % 2;
                    if (Random.value < 0.8f) SoulDust(CenterPx(p) - TV(p).normalized * 6f, -TV(p) * 0.15f, Random.Range(0.6f, 1f));
                }
                else
                {
                    p.Vel = p.Vel * 0.96f + new Vector2(0f, 0.04f);
                    p.Frame = ((int)p.Ai0 / 10) % 2;
                }
            }
            p.TRot = Mathf.Atan2(-p.Vel.y, p.Vel.x);
        }

        /// <summary>The wraith: the little soul's frame (32×32, three quarters), turned along its flight when it flies, its afterimages, a halo.</summary>
        static void DrawWraith(FxBatch fx, Proj p, Vector2 px)
        {
            float age = p.Ticks;
            float o = Remap01(age, 0f, 6f) * Remap01(150f - age, 0f, 12f);
            if (o <= 0.01f) return;
            var dir = TV(p);
            bool flying = p.Frame >= 4;
            bool left = dir.x < 0f;
            float rot = flying ? Mathf.Atan2(dir.y, dir.x) : 0f;
            var frame = FxBatch.Frame(SoulWave.Tex("minion"), 0, (p.Frame & 7) * 32, 32, 32, 16f, 16f);
            float size = p.LocalAi0 == 1f ? 0.5f : 0.75f;
            int n = p.Trail.Count;
            for (int k = n - 1; k >= 1; k--)
            {
                float f = 1f - k / (float)(n + 1);
                fx.Draw(frame, TerraParticles.ToPx(p.Trail[k]), SoulWave.Glow(SoulWave.Pale, 0.28f * f * o), rot, size * (0.75f + 0.25f * f), flipY: flying && left, flipX: !flying && left);
            }
            fx.Draw(frame, px, SoulWave.Glow(SoulWave.Cyan, 0.25f * o), rot, size * 1.13f, flipY: flying && left, flipX: !flying && left);
            fx.Draw(frame, px, Color.white * o, rot, size, flipY: flying && left, flipX: !flying && left);
        }

        // ------------------------------------------------------------------ the Revenant Pulse Rifle

        public static int DevLasers, DevCharges, DevWaves, DevCancels, DevShort, DevBeamStruck;

        /// <summary>
        /// The rifle's laser from its muzzle (from the player's centre if the muzzle is in a wall), with a kick and a burst
        /// of sparks there; its star drawn at the muzzle (Point) for its first updates.
        /// </summary>
        public void ShootPulseLaser(HeroController hero, Vector2 mouse, Vector2 vel, int damage, float knockback, float crit)
        {
            if (hero == null) return;
            var centre = TerraParticles.ToPx(hero.GetComponent<BoxCollider2D>().bounds.center);
            var dir = TerraParticles.ToPx(mouse) - centre;
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector2.right;
            float facing = dir.x < 0f ? -1f : 1f;
            var muzzle = MuzzleOrCentre(centre, dir, facing);
            // At once along the aim: the first enemy on the line stops it (nothing past it is hit), else a wall, else its reach.
            var from = TerraParticles.ToWorld(muzzle);
            var wdir = Flip(dir).normalized;
            float wall = BeamWall(from, wdir, BeamReachPx * Px);
            var struck = BeamTarget(from, wdir, wall, out var hitAt);
            var end = struck != null ? hitAt : from + wdir * wall;
            var p = Spawn(ProjKind.PulseBeam, end, Vector2.zero, 0, 0f, 0f);
            if (p == null) return;
            p.Point = muzzle;
            p.TRot = Mathf.Atan2(-wdir.y, wdir.x);
            var blow = Spawn(ProjKind.PulseLaser, struck != null ? hitAt + wdir * (4f * Px) : end, Vector2.zero, damage, knockback, crit);
            if (blow != null)
            {
                blow.Point = muzzle;
                blow.TRot = p.TRot;
            }
            if (struck != null) DevBeamStruck++;
            else
            {
                var endPx = TerraParticles.ToPx(end);
                for (int k = 0; k < 3; k++) SoulDust(endPx, -dir * Random.Range(1f, 3f) + Random.insideUnitCircle * 2f, Random.Range(0.5f, 0.8f), 226);
            }
            DevLasers++;
            SoulWave.Kick(2.5f, 3f);
            Sound("tk_pulse_laser_*", 0.6f);
            for (int k = 0; k < 4; k++) SoulDust(muzzle, (dir * Random.Range(1.5f, 4f) + Random.insideUnitCircle) * 1f, Random.Range(0.5f, 0.8f), 226);
            SoulDust(muzzle, dir * 0.8f, 1f);
        }

        /// <summary>The muzzle, unless a block stands between it and the player's centre: the centre then.</summary>
        static Vector2 MuzzleOrCentre(Vector2 centre, Vector2 dir, float facing)
        {
            var muzzle = SoulWave.RiflePoint(centre, dir, facing, SoulWave.Muzzle);
            return Physics2D.Linecast(TerraParticles.ToWorld(centre), TerraParticles.ToWorld(muzzle), 1 << ChunkColliders.TerrainLayer) ? centre : muzzle;
        }

        /// <summary>The beam's reach (the bolt it replaced went 14 px × 3 updates × 45 ticks) and how long it shows.</summary>
        const float BeamReachPx = 1890f;
        const int BeamTicks = 9;

        /// <summary>How far the beam goes before a wall: the terrain's first collider on the line, platforms passed (as shots pass them).</summary>
        static float BeamWall(Vector2 from, Vector2 dir, float reach)
        {
            var hits = Physics2D.RaycastAll(from, dir, reach, 1 << ChunkColliders.TerrainLayer);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (h.collider == null || h.collider.usedByEffector) continue;
                var cell = CellGrid.WorldToCell(h.point + dir * 0.02f);
                if (Cells.IsPlatform(TerraWorld.Get(cell.x, cell.y))) continue;
                return h.distance;
            }
            return reach;
        }

        static readonly System.Collections.Generic.List<RaycastHit2D> beamHits = new System.Collections.Generic.List<RaycastHit2D>();

        /// <summary>The first enemy the beam meets short of a wall (what any shot may hit), and where; or null.</summary>
        static HealthManager BeamTarget(Vector2 from, Vector2 dir, float within, out Vector2 at)
        {
            at = default;
            HealthManager best = null;
            float bestD = within;
            beamHits.Clear();
            var filter = new ContactFilter2D { useTriggers = true, useLayerMask = true, layerMask = MeleeHits.HitMask };
            Physics2D.CircleCast(from, 3f * Px, dir, filter, beamHits, within);
            foreach (var h in beamHits)
            {
                var hm = h.collider != null ? h.collider.GetComponentInParent<HealthManager>() : null;
                if (hm == null || hm.isDead || !hm.gameObject.activeInHierarchy || (EnemyStats.Untouchable(hm) && !BossWake.Asleep(hm)) || ArenaGuard.Outside(hm) || ArenaGuard.OffMap(hm)) continue;
                if (h.distance >= bestD) continue;
                bestD = h.distance;
                best = hm;
                at = from + dir * h.distance;
            }
            return best;
        }

        /// <summary>The laser bolt (bone core, pale edges) with a wider cyan glow; the muzzle's star while it is young.</summary>
        /// <summary>
        /// The beam: the laser's even middle stretched from the muzzle to its end, a wide cyan glow under a bone-white
        /// core, both narrowing and fading over its few ticks; a flash at each end, light along it.
        /// </summary>
        static void DrawPulseBeam(FxBatch fx, Proj p, Vector2 px)
        {
            if (p.Point == Vector2.zero) return;
            float k = Mathf.Clamp01(1f - p.Ticks / (float)BeamTicks);
            var d = px - p.Point;
            float len = d.magnitude;
            if (len < 1f || k <= 0f) return;
            float rot = Mathf.Atan2(d.y, d.x);
            var bar = FxBatch.Frame(SoulWave.Tex("laser"), 8, 0, 16, 5, 0f, 2.5f);
            fx.Draw(bar, p.Point, SoulWave.Glow(SoulWave.Cyan, 0.6f * k), rot, new Vector2(len / 16f, 2.6f * (0.45f + 0.55f * k)));
            fx.Draw(bar, p.Point, Color.white * k, rot, new Vector2(len / 16f, 0.35f + 0.65f * k));
            var star = FxBatch.Whole(SoulWave.Tex("flash"));
            fx.Draw(star, px, SoulWave.Glow(SoulWave.Pale, 0.7f * k), rot, 0.3f + 0.35f * k, overPlayer: true);
            for (float s = 0f; s <= len; s += 96f) fx.Light(p.Point + d * (s / len), new Color(0.25f, 0.4f, 0.6f) * k, 2f);
            if (p.Ticks < 5)
            {
                float m = 1f - p.Ticks / 5f;
                fx.Draw(star, p.Point, SoulWave.Glow(SoulWave.Bone, 0.9f * m), p.TRot, 0.6f + 0.6f * m, overPlayer: true);
                fx.Draw(star, p.Point, SoulWave.Glow(SoulWave.Cyan, 0.5f * m), p.TRot + Mathf.PI / 4f, 0.4f + 0.4f * m, overPlayer: true);
                fx.Light(p.Point, new Color(0.6f, 0.75f, 1f) * m, 3f);
            }
        }

        void LaserSpark(Proj p, bool enemy)
        {
            var at = CenterPx(p);
            var back = -TV(p).normalized;
            for (int k = 0; k < (enemy ? 5 : 3); k++) SoulDust(at, (back * Random.Range(1f, 3f) + Random.insideUnitCircle * 2f), Random.Range(0.5f, 0.8f), 226);
            if (enemy)
            {
                SoulDust(at, Random.insideUnitCircle, 1f);
                Sound("tk_spirit_hit_*", 0.3f);
            }
        }

        /// <summary>A charge on the rifle held now (this game's).</summary>
        public bool PulseCharging
        {
            get
            {
                foreach (var q in live)
                    if (!q.Dead && q.Kind == ProjKind.PulseCharge) return true;
                return false;
            }
        }

        /// <summary>The right button down with the rifle: a charge at its muzzle (one at a time), the hum starting.</summary>
        public void StartPulseCharge(HeroController hero, Vector2 mouse, int damage, float knockback, float crit)
        {
            if (hero == null || PulseCharging) return;
            var p = Spawn(ProjKind.PulseCharge, hero.GetComponent<BoxCollider2D>().bounds.center, Vector2.right * 0.01f, damage, knockback, crit);
            if (p == null) return;
            p.Ai0 = 0f;
            p.Ai1 = 0f;
            DevCharges++;
            Sound("tk_pulse_charge", 0.7f);
        }

        /// <summary>
        /// The charge (Ai0 its updates, to <see cref="SoulWave.ChargeTicks"/>; Ai1 1 once fired): kept at the muzzle, the
        /// rifle held along the aim; souls drawn in from a ring closing on the muzzle, more at each third, a shiver in the
        /// last fifth; let go early it fizzles (nothing spent); full, it fires the wave once (if the rounds are there) and
        /// waits for the button to come up.
        /// </summary>
        void StepPulseCharge(Proj p)
        {
            var mod = TerraMod.Instance;
            var def = ItemDb.Get(ItemType.RevenantPulseRifle);
            bool still = mod != null && mod.AltHeld && mod.Inventory.Current.Type == def.Type;
            if (mod == null || !Holder(out var centre) || !still)
            {
                if (p.Ai1 == 0f)
                {
                    DevCancels++;
                    mod?.StopSound("tk_pulse_charge");
                    Sound("tk_pulse_cancel", 0.6f);
                    if (p.Ai0 > 4f)
                        for (int k = 0; k < 5; k++) SoulDust(CenterPx(p), Random.insideUnitCircle * 1.5f, 0.7f);
                }
                Kill(p);
                return;
            }
            var aim = AimPx() - centre;
            var dir = aim.sqrMagnitude > 1e-6f ? aim.normalized : new Vector2(p.Dir, 0f);
            float facing = dir.x < 0f ? -1f : 1f;
            mod.KeepUsing(def, Flip(dir));
            var muzzle = SoulWave.RiflePoint(centre, dir, facing, SoulWave.Muzzle);
            p.Pos = TerraParticles.ToWorld(muzzle);
            p.Vel = Flip(dir) * 0.01f;
            p.TRot = Mathf.Atan2(dir.y, dir.x);
            p.Dir = facing;
            p.TimeLeft = 2;
            if (p.Ai1 == 0f)
            {
                p.Ai0++;
                float c = p.Ai0 / SoulWave.ChargeTicks;
                int stage = p.Ai0 >= 80f ? 2 : p.Ai0 >= 40f ? 1 : 0;
                for (int k = 0; k <= stage; k++)
                {
                    float a = Random.value * Mathf.PI * 2f;
                    float r = Mathf.Lerp(28f, 12f, c) + Random.Range(-3f, 3f);
                    var at = muzzle + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    SoulDust(at, (muzzle - at).normalized * Mathf.Lerp(1.4f, 3.2f, c), Random.Range(0.45f, 0.8f), k == 2 ? 226 : 180);
                }
                if (p.Ai0 == 40f || p.Ai0 == 80f)
                    for (int k = 0; k < 10; k++)
                    {
                        float a = k / 10f * Mathf.PI * 2f;
                        SoulDust(muzzle, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.2f, 0.8f, 226);
                    }
                if (p.Ai0 > 96f) SoulWave.Shiver = Mathf.Max(SoulWave.Shiver, 0.8f * (p.Ai0 - 96f) / 24f);
                LightMap.Add(p.Pos, new Color(0.35f, 0.5f, 0.8f) * ((0.4f + 0.8f * c)*WeaponEffects.Opacity));
                if (p.Ai0 >= SoulWave.ChargeTicks) FireChargedWave(p, mod, def, centre, dir, facing);
            }
            else p.LocalAi1++;
        }

        /// <summary>The rounds the rifle can draw on: all of its kind carried (an endless pouch: any number).</summary>
        static int RoundsFor(TerraMod mod, ItemDef def)
        {
            int n = 0;
            foreach (var s in mod.Inventory.Slots)
            {
                if (s.IsEmpty || s.Def.Ammo != def.UseAmmo) continue;
                if (!s.Def.Consumable && s.Def.MaxStack <= 1) return int.MaxValue;
                n += s.Count;
            }
            return n;
        }

        /// <summary>
        /// Full: the charged wave, once, for <see cref="SoulWave.WaveAmmo"/> rounds taken at once (PickAmmo each: the savings
        /// of the gear roll for each), six times a laser's damage, twice its push; the big star, the kick, the view punched; not
        /// enough rounds: a click and a word, no wave.
        /// </summary>
        void FireChargedWave(Proj p, TerraMod mod, ItemDef def, Vector2 centre, Vector2 dir, float facing)
        {
            p.Ai1 = 1f;
            p.LocalAi1 = 0f;
            mod.StopSound("tk_pulse_charge");
            if (RoundsFor(mod, def) < SoulWave.WaveAmmo)
            {
                DevShort++;
                Sound("tk_pulse_cancel", 0.7f);
                UI.CombatText.Show(p.Pos, Lang.T("弹药不足"), new Color(0.75f, 0.8f, 0.9f));
                p.Ai1 = 2f;
                return;
            }
            int damage = 0;
            float knockback = 0f;
            for (int k = 0; k < SoulWave.WaveAmmo; k++)
            {
                var kind = def.Shoot;
                float speed = 0f;
                if (!mod.PickAmmo(def, ref kind, ref speed, out int dmg, out float kb)) break;
                if (k == 0)
                {
                    damage = dmg;
                    knockback = kb;
                }
            }
            var from = MuzzleOrCentre(centre, dir, facing);
            var wave = Spawn(ProjKind.ChargedSoulWave, TerraParticles.ToWorld(from), Flip(dir) * 10f, damage * SoulWave.WaveDamage, knockback * 2f, p.Crit);
            if (wave != null) wave.Class = DamageClass.Ranged;
            DevWaves++;
            SoulWave.Kick(9f, 14f);
            Player.TerraCamera.Punch(0.14f, 0.28f);
            Sound("tk_pulse_wave", 0.85f);
            for (int k = 0; k < 22; k++)
            {
                float a = Mathf.Atan2(dir.y, dir.x) + Random.Range(-0.6f, 0.6f);
                var v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(2f, 7f);
                SoulDust(from, v, Random.Range(0.8f, 1.5f), k % 3 == 0 ? 226 : 180);
            }
        }

        /// <summary>
        /// The charge drawn over the held rifle: the orb at the muzzle swelling with the charge, pulsing, its halo; a star flickering
        /// on it; the rifle's three cells lighting one by one (the player's centre and the aim from the charge's own turn); once
        /// fired, the big star and a ring opening at the muzzle for 8 updates.
        /// </summary>
        static void DrawPulseCharge(FxBatch fx, Proj p, Vector2 px)
        {
            var orb = FxBatch.Whole(SoulWave.Tex("orb"));
            var star = FxBatch.Whole(SoulWave.Tex("flash"));
            if (p.Ai1 == 0f)
            {
                float c = Mathf.Clamp01(p.Ai0 / SoulWave.ChargeTicks);
                float pulse = 1f + 0.12f * Mathf.Sin(p.Ai0 * (0.25f + 0.35f * c));
                float s = (0.25f + 0.7f * c) * pulse;
                fx.Draw(orb, px, SoulWave.Glow(SoulWave.Pale, 0.3f + 0.35f * c), 0f, s * 1.55f, overPlayer: true);
                fx.Draw(orb, px, Color.white * (0.5f + 0.5f * c), 0f, s, overPlayer: true);
                fx.Draw(orb, px, SoulWave.Glow(SoulWave.Bone, 0.5f + 0.4f * c), 0f, s * 0.55f, overPlayer: true);
                float flick = 0.6f + 0.4f * Mathf.PerlinNoise(p.Ai0 * 0.3f, 0.5f);
                fx.Draw(star, px, SoulWave.Glow(SoulWave.Bone, (0.25f + 0.5f * c) * flick), p.Ai0 * 0.12f, (0.35f + 0.7f * c) * flick, overPlayer: true);
                fx.Light(px, new Color(0.45f, 0.6f, 0.95f) * (0.4f + 0.8f * c), 2f + 3f * c);
                if (Holder(out var centre))
                {
                    var dir = new Vector2(Mathf.Cos(p.TRot), Mathf.Sin(p.TRot));
                    for (int k = 0; k < SoulWave.Cells.Length; k++)
                    {
                        float lit = Remap01(p.Ai0, 40f * k, 40f * (k + 1));
                        if (lit <= 0f) continue;
                        var at = SoulWave.RiflePoint(centre, dir, p.Dir, SoulWave.Cells[k]);
                        fx.Draw(orb, at, SoulWave.Glow(SoulWave.Cyan, 0.9f * lit), 0f, 0.3f, overPlayer: true);
                        fx.Draw(orb, at, SoulWave.Glow(SoulWave.Bone, 0.7f * lit), 0f, 0.14f, overPlayer: true);
                    }
                }
                return;
            }
            if (p.Ai1 == 1f && p.LocalAi1 < 8f)
            {
                float k = 1f - p.LocalAi1 / 8f;
                fx.Draw(star, px, SoulWave.Glow(SoulWave.Bone, k), p.TRot, 1.2f + 1.4f * k, overPlayer: true);
                fx.Draw(star, px, SoulWave.Glow(SoulWave.Cyan, 0.7f * k), p.TRot + Mathf.PI / 4f, 0.8f + k, overPlayer: true);
                fx.Draw(FxBatch.Whole(SoulWave.Tex("ring")), px, SoulWave.Glow(SoulWave.Pale, 0.8f * k), 0f, 0.6f + (1f - k) * 2.2f, overPlayer: true);
                fx.Light(px, new Color(0.7f, 0.85f, 1f) * k, 6f);
            }
        }

        void StepChargedWave(Proj p)
        {
            p.TRot = Mathf.Atan2(-p.Vel.y, p.Vel.x);
            var c = CenterPx(p);
            var back = -TV(p).normalized;
            for (int k = 0; k < 2; k++)
                SoulDust(c + back * 30f + Random.insideUnitCircle * 10f, back * Random.Range(0.5f, 1.5f) + Random.insideUnitCircle * 0.4f, Random.Range(0.9f, 1.4f));
            if (p.Ticks % 3 == 0) SoulDust(c + Random.insideUnitCircle * 14f, Random.insideUnitCircle * 0.5f, 0.7f, 226);
        }

        /// <summary>
        /// The charged wave (80×48, grown from three fifths over its first 8 updates, breathing), its eight afterimages,
        /// a cyan halo, two motes circling it; fading out over its last 10 updates.
        /// </summary>
        static void DrawChargedWave(FxBatch fx, Proj p, Vector2 px)
        {
            float age = p.Ticks;
            float o = Remap01(80f - age, 0f, 10f);
            if (o <= 0.01f) return;
            float grow = Mathf.Lerp(0.6f, 1f, Remap01(age, 0f, 8f)) * (1f + 0.05f * Mathf.Sin(age * 0.5f));
            var dir = TV(p);
            bool left = dir.x < 0f;
            float rot = Mathf.Atan2(dir.y, dir.x);
            var tex = FxBatch.Whole(SoulWave.Tex("charged"));
            int n = p.Trail.Count;
            for (int k = n - 1; k >= 1; k--)
            {
                float f = 1f - k / (float)(n + 1);
                fx.Draw(tex, TerraParticles.ToPx(p.Trail[k]), SoulWave.Glow(SoulWave.Pale, 0.3f * f * o), rot, grow * (0.85f + 0.15f * f), flipY: left);
            }
            fx.Draw(tex, px, SoulWave.Glow(SoulWave.Cyan, 0.35f * o), rot, grow * 1.1f, flipY: left);
            fx.Draw(tex, px, Color.white * o, rot, grow, flipY: left);
            var mote = FxBatch.Whole(SoulWave.Tex("mote"));
            for (int k = 0; k < 2; k++)
            {
                float a = age * 0.22f + k * Mathf.PI;
                var at = px + new Vector2(Mathf.Cos(a) * 26f, Mathf.Sin(a) * 18f) * grow;
                fx.Draw(mote, at, SoulWave.Glow(SoulWave.Bone, 0.9f * o), 0f, 1f);
            }
            fx.Light(px, new Color(0.55f, 0.7f, 1f) * o, 6f);
        }

        // ------------------------------------------------------------------ the Vengeful Spirit

        /// <summary>The spell cast: the wave from 14 px ahead of the caster, a ring of souls flung out where it starts.</summary>
        public void CastSpiritWave(Vector2 from, Vector2 vel, int damage, float knockback, float crit)
        {
            var dir = vel.sqrMagnitude > 1e-6f ? vel.normalized : Vector2.right;
            var start = from + dir * 14f * Px;
            var p = Spawn(ProjKind.SpiritWave, start, vel, damage, knockback, crit);
            if (p == null) return;
            var at = TerraParticles.ToPx(start);
            p.Point = at;
            for (int k = 0; k < 14; k++)
            {
                float a = k / 14f * Mathf.PI * 2f;
                SoulDust(at, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 2.6f, 1.1f);
            }
            for (int k = 0; k < 4; k++) SoulDust(at, Flip(dir) * Random.Range(2f, 5f) + Random.insideUnitCircle, 0.9f, 15);
        }

        void StepSpiritWave(Proj p)
        {
            AnimateWave(p);
            p.TRot = Mathf.Atan2(-p.Vel.y, p.Vel.x);
            if (p.Ticks % 2 == 0)
            {
                var c = CenterPx(p);
                var back = -TV(p).normalized;
                SoulDust(c + back * 18f + Random.insideUnitCircle * 6f, back * Random.Range(0.4f, 1.2f) + new Vector2(0f, -0.3f), Random.Range(0.8f, 1.2f));
            }
        }

        /// <summary>The spell's wave (full size), and for its first 10 updates the ring opening where it was cast.</summary>
        static void DrawSpiritWave(FxBatch fx, Proj p, Vector2 px)
        {
            DrawSoulWave(fx, p, px, 1f, 60);
            if (p.Ticks < 10 && p.Point != Vector2.zero)
            {
                float k = 1f - p.Ticks / 10f;
                fx.Draw(FxBatch.Whole(SoulWave.Tex("ring")), p.Point, SoulWave.Glow(SoulWave.Pale, 0.8f * k), 0f, 0.5f + (1f - k) * 1.6f, overPlayer: true);
                fx.Draw(FxBatch.Whole(SoulWave.Tex("flash")), p.Point, SoulWave.Glow(SoulWave.Bone, 0.7f * k), 0f, 0.8f * k + 0.3f, overPlayer: true);
            }
        }
    }
}
