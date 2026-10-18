using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Effects
{
    /// <summary>
    /// Terraria's ParticleOrchestrator particles that the whips use (Terraria px, y down, 60 Hz):
    /// RandomizedFrameParticle (the black lightning: Projectile_916's frames, a new random one
    /// every 2 ticks) and PrettySparkleParticle (the rainbow rod's 4-point stars on Extra_98).
    /// ABasicParticle moves them each tick: position by velocity, velocity by acceleration,
    /// scale by its growth. Drawn over the player (ParticleSystem_World_OverPlayers).
    /// </summary>
    internal static class FxParticles
    {
        enum Kind { Frame, Sparkle, Fading, Bloody, Icon }

        struct Part
        {
            public Kind Kind;
            public Vector2 Pos, Vel, Accel;
            public float Rot, Scale, Grow;
            public int Age, Life, Frame, Counter;
            public Color Tint;
            public float FadeIn, FadeOut;
            /// <summary>PrettySparkleParticle: the flare's ticks (in-end, out-start, out-end), its additive share, horizontal arm only.</summary>
            public float FlareIn, FlareOut, FlareEnd, Additive;
            public bool NoVertical;
            /// <summary>Blue lightning (the eel's) rather than black; Fading: drawn behind the player.</summary>
            public bool Blue, Behind;
            /// <summary>PrettySparkleParticle.Scale as a vector (the Moon Lord whip's long streaks); zero: Scale both ways.</summary>
            public Vector2 Scale2;
            /// <summary>FadingParticle: its texture (Extra_270 when null), the scale's speed and its change (per tick), the spin, the ticks before it starts, its fade times (0: 0.01 and 0.1).</summary>
            public string Tex;
            public Vector2 ScaleVel2, ScaleAcc2;
            public float RotVel, RotAcc;
            public int Delay;
            public float FadeInN, FadeOutN;
            /// <summary>A FlameParticle: pivoted on its bottom, drawn four times jittered by twice its scale.</summary>
            public bool Flame;
            /// <summary>FadingParticle: a square of its texture this many px across from this row down (Extra_269's three), else the whole texture.</summary>
            public int SrcY, SrcSize;
            /// <summary>BloodyExplosionParticle: its blood burst (5) not yet thrown.</summary>
            public bool Burst;
        }

        static readonly List<Part> parts = new List<Part>();
        const int Max = 600;

        static void Add(Part p)
        {
            if (parts.Count >= Max) parts.RemoveAt(0);
            parts.Add(p);
        }

        /// <summary>PO:3554 BlackLightningHit: seven black and indigo sparks thrown out round a point.</summary>
        public static void BlackLightningHit(Vector2 at)
        {
            float start = Random.value * Mathf.PI * 2f;
            for (int k = 0; k < 7; k++)
            {
                float a = Mathf.PI * 2f * k / 7f + start + Random.Range(-1f, 1f) * 0.25f;
                float speed = Random.value * 4f + 0.1f;
                Add(Lightning(at + Random.insideUnitCircle * 12f * 0.7f, new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.5f) * speed, a, 1f, 0.05f));
            }
        }

        /// <summary>PO:3513 BlackLightningSmall: one or two small, slower sparks drifting along movement.</summary>
        public static void BlackLightningSmall(Vector2 at, Vector2 movement)
        {
            int n = Random.Range(1, 3);
            float start = Random.value * Mathf.PI * 2f;
            for (int k = 0; k < n; k++)
            {
                float a = Mathf.PI * 2f * k / n + start + Random.Range(-1f, 1f) * 0.25f;
                float speed = Random.value * 4f + 0.1f;
                var v = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.5f) * speed * 0.2f + movement;
                Add(Lightning(at + Random.insideUnitCircle * 12f * 0.7f, v, Mathf.Atan2(v.y, v.x), 0.5f, 0.025f));
            }
        }

        static Part Lightning(Vector2 at, Vector2 v, float rot, float scale, float grow)
        {
            float r = Random.value;
            Color tint;
            switch (Random.Range(0, 4))
            {
                case 1: tint = Color.Lerp(FxBatch.C(106, 90, 205, 127), Color.black, 0.1f + 0.7f * r); break;
                case 2: tint = Color.Lerp(FxBatch.C(106, 90, 205, 60), Color.black, 0.1f + 0.8f * r); break;
                default: tint = Color.black; break;
            }
            return new Part
            {
                Kind = Kind.Frame, Pos = at, Vel = v, Rot = rot, Scale = scale, Grow = grow, Life = 24, Frame = Random.Range(0, 6),
                Tint = tint, FadeIn = 0.01f, FadeOut = 0.5f,
            };
        }

        /// <summary>
        /// PO:3462 RainbowRodHit: six pairs of stars (a rainbow one and a smaller white one) flying
        /// out and slowing to a stop over 60 ticks, and twelve rainbow motes with white twins.
        /// </summary>
        public static void RainbowRodHit(Vector2 at)
        {
            float start = Random.value * Mathf.PI * 2f, hue = Random.value;
            for (int k = 0; k < 6; k++)
            {
                float s = 0.4f + 0.4f * Random.value, a = start + Random.value * Mathf.PI * 2f;
                var v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 1.5f * s;
                var off = Random.insideUnitCircle * 8f * s;
                var tint = HslToRgb((hue + Random.value * 0.33f) % 1f, 1f, 0.4f + Random.value * 0.25f);
                tint.a = 0f;
                Add(Sparkle(at + off, v, tint, s));
                Add(Sparkle(at + off, v, new Color(1f, 1f, 1f, 0f), 0.6f * s));
            }
            var p = TerraParticles.Instance;
            if (p == null) return;
            for (int k = 0; k < 12; k++)
            {
                int i = p.NewDust(at, 0, 0, 267);
                if (i < 0) continue;
                float r = Random.value;
                p[i].Position = at;
                p[i].Color = HslToRgb((hue + Random.value * 0.12f) % 1f, 1f, 0.4f + 0.25f * r);
                p[i].Velocity = Random.insideUnitCircle;
                p[i].NoGravity = true;
                p[i].Scale = 0.6f + 0.9f * r;
                p[i].FadeIn = 0.7f + 0.8f * r;
                var twin = p[i];
                int j = p.NewDust(at, 0, 0, 267);
                if (j < 0) continue;
                twin.Scale /= 2f;
                twin.FadeIn *= 0.75f;
                twin.Color = Color.white;
                p[j] = twin;
            }
        }

        static Part Sparkle(Vector2 at, Vector2 v, Color tint, float scale) => new Part
        {
            Kind = Kind.Sparkle, Pos = at, Vel = v, Accel = -v / 60f, Rot = Mathf.PI / 2f, Scale = scale, Life = 60, Tint = tint,
            FadeIn = 0.05f, FadeOut = 0.9f, FlareIn = 20f, FlareOut = 30f, FlareEnd = 45f, Additive = 1f,
        };

        /// <summary>
        /// PO:3619-3672 BlueLightningSmall (the eel's spark): one or two cyan-to-teal sparks of
        /// Projectile_916 living 10 ticks, swelling, and half the time an Electric dust (226).
        /// </summary>
        public static void BlueLightningSmall(Vector2 at, Vector2 movement)
        {
            int n = Random.Range(1, 3);
            float start = Random.value * Mathf.PI * 2f;
            for (int k = 0; k < n; k++)
            {
                float a = Mathf.PI * 2f * k / n + start + Random.Range(-1f, 1f) * 0.25f;
                float speed = Random.value * 4f + 0.1f;
                var v = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.5f) * speed * 0.2f + movement;
                var tint = Color.Lerp(FxBatch.C(133, 255, 255, 0), FxBatch.C(15, 100, 155, 0), Random.value);
                var pos = at + Random.insideUnitCircle * 6f * 0.7f;
                Add(new Part
                {
                    Kind = Kind.Frame, Pos = pos, Vel = v, Rot = Mathf.Atan2(v.y, v.x), Scale = 1f, Grow = 0.025f, Life = 10, Frame = Random.Range(0, 6),
                    Tint = tint, FadeIn = 0.01f, FadeOut = 0.5f, Blue = true,
                });
                if (Random.Range(0, 2) != 0) continue;
                var p = TerraParticles.Instance;
                int i = p != null ? p.NewDust(pos, 0, 0, 226) : -1;
                if (i < 0) continue;
                p[i].Position = pos;
                p[i].Scale = 1f;
                p[i].NoGravity = true;
                p[i].Velocity = Random.insideUnitCircle * 4f;
            }
        }

        /// <summary>PO:3595-3617 BlueLightningSmallLong: a dotted bolt of sparks every 25 px from a point along a vector.</summary>
        public static void BlueLightningSmallLong(Vector2 from, Vector2 movement)
        {
            float len = movement.magnitude;
            if (len < 40f) return;
            var dir = movement / len;
            for (float s = 0f; s < len; s += 25f) BlueLightningSmall(from + dir * s, dir * 2f);
        }

        /// <summary>
        /// PO:3412-3460 StardustPunch (a Constellation star's hit): an azure and a white star
        /// drifting off and stopping, and two azure motes with white twins.
        /// </summary>
        /// <summary>
        /// PO:3130-3152 ChlorophyteLeafCrystalShot: a 4×1 streak along the leaf shot for 30 ticks,
        /// its hue running from lime with the shot's age (lifeLeft 1 → 0), half added.
        /// </summary>
        public static void LeafCrystalShot(Vector2 at, Vector2 v, float lifeLeft)
        {
            float hue = ((1f - lifeLeft) * -6f * 0.85f + 0.33f) % 1f;
            if (hue < 0f) hue += 1f;
            var tint = Color.Lerp(HslToRgb(hue, 1f, 0.5f), new Color(0.75f, 1f, 0.3f), 0.5f);
            tint.a = 0.5f;
            Add(new Part
            {
                Kind = Kind.Sparkle, Pos = at, Vel = v, Rot = Mathf.Atan2(v.y, v.x), Scale = 1f, Life = 30, Tint = tint, FadeIn = 0.05f, FadeOut = 0.9f,
                FlareIn = 10f, FlareOut = 15f, FlareEnd = 30f, Additive = 0.5f, NoVertical = true, Scale2 = new Vector2(4f, 1f),
            });
        }

        /// <summary>PO:3703-3760 FormatC: a gold flash then a white one (Extra_98) where the crit landed, 15 ticks, drifting along the yoyo's flight.</summary>
        public static void FormatCHit(Vector2 at, Vector2 movement)
        {
            float a = Mathf.Atan2(movement.y, movement.x);
            var v = movement.sqrMagnitude > 1e-6f ? movement.normalized * 0.5f : Vector2.zero;
            Part Flash(Color tint, float k) => new Part
            {
                Kind = Kind.Sparkle, Pos = at, Vel = v, Rot = a, Scale = 1f, Life = 15, Tint = tint, FadeIn = 0.05f, FadeOut = 0.9f,
                FlareIn = 4f, FlareOut = 8f, FlareEnd = 15f, Additive = tint.a <= 0f ? 1f : 0.5f, Scale2 = new Vector2(0.5f, 1.2f) * 0.8f * 0.7f * k,
            };
            Add(Flash(new Color(1f, 0.8f, 0.2f, 0.5f), 1f));
            Add(Flash(new Color(1f, 1f, 1f, 0f), 0.48f / 0.7f));
        }

        public static void StardustPunch(Vector2 at, Vector2 movement)
        {
            float start = Random.value * Mathf.PI * 2f, r = Random.value;
            float s = 0.4f + 0.4f * Random.value, f = start + Random.value * Mathf.PI * 2f;
            var m = movement * (0.3f + 0.35f * Random.value);
            var dir = new Vector2(Mathf.Cos(f), Mathf.Sin(f));
            var off = Random.insideUnitCircle * 8f * s;
            var tint = HslToRgb((0.6f + 0.05f * r) % 1f, 1f, 0.4f + 0.25f * r);
            tint.a = 0f;
            var a = Sparkle(at + off, dir * 0.1f * s + m, tint, s);
            a.Accel = -dir * 0.1f * s / 60f - m / 60f;
            Add(a);
            var b = Sparkle(at + off, dir * 0.1f * s + m, new Color(1f, 1f, 1f, 0f), 0.6f * s);
            b.Accel = -dir * 0.1f * s / 60f - m / 30f;
            Add(b);
            var p = TerraParticles.Instance;
            if (p == null) return;
            for (int k = 0; k < 2; k++)
            {
                float q = Random.value;
                int i = p.NewDust(at, 0, 0, 267);
                if (i < 0) continue;
                p[i].Position = at + Random.insideUnitCircle * 16f;
                p[i].Color = HslToRgb((0.59f + 0.05f * q) % 1f, 1f, 0.4f + 0.25f * q);
                p[i].Velocity = Random.insideUnitCircle * 2f + movement * (0.5f + 0.5f * q) * 1.4f;
                p[i].NoGravity = true;
                p[i].Scale = 0.6f + 2f * Random.value;
                int w = p.Clone(i);
                if (w < 0) continue;
                p[w].Scale /= 2f;
                p[w].FadeIn *= 0.75f;
                p[w].Color = Color.white;
            }
        }

        /// <summary>
        /// PO:1653-1707 MoonLordWhipHit: two long thin streaks along an angle (a teal half-added one
        /// and a green one), growing to full at 15 ticks and gone at 30; vortex dust (229) both ways.
        /// </summary>
        public static void MoonLordWhipHit(Vector2 at, float angle)
        {
            var v = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 4f;
            var pos = at - v * 30f * 0.25f;
            Part Streak(Color tint, float additive, float scale) => new Part
            {
                Kind = Kind.Sparkle, Pos = pos, Vel = v, Rot = angle, Scale = scale, Life = 30, Tint = tint, FadeIn = 5e-6f, FadeOut = 0.95f,
                FlareIn = 15f, FlareOut = 15f, FlareEnd = 30f, Additive = additive, NoVertical = true, Scale2 = new Vector2(6f, 0.75f) * scale,
            };
            Add(Streak(new Color(0.3f, 0.6f, 0.7f, 0.5f), 0.35f, 1.1f));
            Add(Streak(new Color(0.2f, 1f, 0.4f, 1f), 1f, 0.7f));
            var p = TerraParticles.Instance;
            if (p == null) return;
            for (int k = 0; k < 2; k++)
                for (int side = -1; side <= 1; side += 2)
                {
                    float r = Random.Range(-1f, 1f) * Mathf.PI * 2f * 0.025f;
                    var dv = new Vector2(v.x * Mathf.Cos(r) - v.y * Mathf.Sin(r), v.x * Mathf.Sin(r) + v.y * Mathf.Cos(r)) * Random.value * side;
                    int i = p.NewDust(at, 0, 0, 229);
                    if (i < 0) continue;
                    p[i].Position = at + dv * 4f;
                    p[i].Velocity = dv;
                    p[i].NoGravity = true;
                    p[i].Scale = 1.2f;
                }
        }

        /// <summary>PO:1897-1918 MoonLordWhipEye: a cyan eye (Extra_270) by a point, drifting back and fading over 45 ticks, behind the player.</summary>
        public static void MoonLordWhipEye(Vector2 at, Vector2 movement)
        {
            float a = Random.value * Mathf.PI * 2f;
            Add(new Part
            {
                Kind = Kind.Fading, Pos = at + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 22.5f, Vel = movement * 0.05f, Accel = -movement * 0.25f / 45f,
                Scale = 0.7f + 0.45f * Random.value, Life = 45, Tint = new Color(1f, 1f, 1f, 0f), Behind = true,
            });
        }

        /// <summary>
        /// Main.DrawPrettyStarSparkle (M:35931-35947): a four-point flash of Extra_98, its arms
        /// (scale, fatness) swelling between fade-in start and end and shrinking from fade-out
        /// start to end of a counter; the shine tint half strength and added, a white core.
        /// </summary>
        public static void DrawStarSparkle(FxBatch fx, float opacity, Vector2 pos, Color drawColor, Color shine, float counter, float inStart, float inEnd,
            float outStart, float outEnd, float rotation, Vector2 scale, Vector2 fatness, bool overPlayer = true)
        {
            var col = shine * opacity * 0.5f;
            col.a = 0f;
            var col2 = drawColor * 0.5f;
            float f = Mathf.Clamp01(Mathf.InverseLerp(inStart, inEnd, counter)) * Mathf.Clamp01(Mathf.InverseLerp(outEnd, outStart, counter));
            if (f <= 0f) return;
            var v1 = new Vector2(fatness.x * 0.5f, scale.x) * f;
            var v2 = new Vector2(fatness.y * 0.5f, scale.y) * f;
            var tex = FxBatch.Whole("Extra_98");
            fx.Draw(tex, pos, col, Mathf.PI / 2f + rotation, v1, overPlayer: overPlayer);
            fx.Draw(tex, pos, col, rotation, v2, overPlayer: overPlayer);
            fx.Draw(tex, pos, col2, Mathf.PI / 2f + rotation, v1 * 0.6f, overPlayer: overPlayer);
            fx.Draw(tex, pos, col2, rotation, v2 * 0.6f, overPlayer: overPlayer);
        }

        /// <summary>Main.hslToRgb.</summary>
        public static Color HslToRgb(float h, float s, float l)
        {
            if (s == 0f) return new Color(l, l, l);
            float q = l < 0.5f ? l * (1f + s) : l + s - l * s, p = 2f * l - q;
            return new Color(Hue(p, q, h + 1f / 3f), Hue(p, q, h), Hue(p, q, h - 1f / 3f));
        }

        static float Hue(float p, float q, float t)
        {
            if (t < 0f) t += 1f;
            if (t > 1f) t -= 1f;
            if (t < 1f / 6f) return p + (q - p) * 6f * t;
            if (t < 0.5f) return q;
            if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
            return p;
        }

        /// <summary>
        /// PO:593-647 SnappingStone: Item72; two blue glows (Projectile_1121) swelling from nothing
        /// over 20 ticks, size to the target's (index × 3 ticks late); electric and spark dust.
        /// </summary>
        public static void SnappingStone(Vector2 at, float size, int index)
        {
            float num = Mathf.Min(0.12f, size / 256f);
            TerraMod.Instance?.PlaySound("Item_72");
            for (int i = 0; i < 2; i++)
                Add(Swell("Projectile_1121", at, FxBatch.C(30, 180, 255, 0), 20, 6f * num / 20f, 0.1f, 0.1f, index * 3));
            var p = TerraParticles.Instance;
            if (p == null) return;
            for (int j = 0; j < 20; j++)
            {
                bool electric = Random.Range(0, 3) != 0;
                int d = p.NewDust(at - new Vector2(4f, 4f), 0, 0, electric ? 226 : 15);
                if (d < 0) continue;
                p[d].Velocity *= 64f * num;
                p[d].NoGravity = true;
                p[d].Scale = 1f;
                p[d].FadeIn = 0f;
                if (!electric) continue;
                p[d].Velocity *= 0.5f;
                if (Random.Range(0, 6) == 0)
                {
                    p[d].NoGravity = false;
                    p[d].Velocity *= 0.5f;
                }
            }
        }

        /// <summary>
        /// PO:649-732 SnappingStoneMelee (the Pyroclastic Stone): Item_206; a red half-added and a
        /// yellow slash (PrettySparkle, 30 ticks) cutting down past the point; fire dust both ways.
        /// </summary>
        public static void SnappingStoneMelee(Vector2 at)
        {
            TerraMod.Instance?.PlaySound("Item_206", 0.5f);
            float tilt = Random.Range(-1f, 1f) * Mathf.PI * 2f * 0.05f, a = Mathf.PI / 4f + Mathf.PI / 4f + tilt;
            var v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 14f;
            Part Slash(Color tint, float scale, float additive)
            {
                var vel = v * 1.5f;
                return new Part
                {
                    Kind = Kind.Sparkle, Pos = at - v * 30f * 0.15f - vel * 4f, Vel = vel, Accel = -vel / 30f, Rot = a, Life = 30, Tint = tint,
                    FadeIn = 5e-6f, FadeOut = 0.95f, FlareIn = 15f, FlareOut = 15f, FlareEnd = 30f, Additive = additive, NoVertical = true,
                    Scale2 = new Vector2(8f, 2f) * scale * 1.5f, ScaleVel2 = new Vector2(-4f, 0f) / 30f,
                };
            }
            Add(Slash(new Color(1f, 0f, 0f, 0.5f), 1.1f, 0.35f));
            Add(Slash(new Color(1f, 0.95f, 0f, 1f), 0.7f, 0f));
            var p = TerraParticles.Instance;
            if (p == null) return;
            for (int i = 0; i < 2; i++)
                for (int side = -1; side <= 1; side += 2)
                {
                    float r = Random.Range(-1f, 1f) * Mathf.PI * 2f * 0.025f;
                    var dv = new Vector2(v.x * Mathf.Cos(r) - v.y * Mathf.Sin(r), v.x * Mathf.Sin(r) + v.y * Mathf.Cos(r)) * Random.value * side;
                    int d = p.NewDust(at - new Vector2(4f, 4f), 0, 0, 6);
                    if (d < 0) continue;
                    p[d].Position = at;
                    p[d].Velocity = dv;
                    p[d].NoGravity = true;
                    p[d].Scale = 1.4f;
                }
        }

        /// <summary>
        /// PO:734-813 SnappingStoneSummon (the Armlet of Ruin): Item_207; two blue glows
        /// (Projectile_1132) and a turning ring (Extra_34) swelling over 20 ticks; spark dust.
        /// </summary>
        public static void SnappingStoneSummon(Vector2 at, int index)
        {
            const float num = 0.12f;
            TerraMod.Instance?.PlaySound("Item_207", 0.5f);
            var tint = FxBatch.C(90, 110, 255, 90);
            for (int i = 0; i < 2; i++) Add(Swell("Projectile_1132", at, tint, 20, 9f * num / 20f, 0.1f, 0.3f, index * 3));
            var ring = Swell("Extra_34", at, tint, 20, 9f * num / 20f, 0.1f, 0.5f, index * 3);
            ring.Rot = Random.value * Mathf.PI * 2f;
            ring.RotVel = Random.Range(-1f, 1f) * Mathf.PI * 2f * 0.33f / 60f;
            Add(ring);
            var p = TerraParticles.Instance;
            if (p == null) return;
            for (int k = 0; k < 23; k++)
            {
                int d = p.NewDust(at - new Vector2(4f, 4f), 0, 0, k < 15 ? 172 : 211);
                if (d < 0) continue;
                p[d].Position = at;
                p[d].Velocity = Random.insideUnitCircle * 10f;
                p[d].Scale = Mathf.Max(0.75f, 2f * 10f * num);
                p[d].NoGravity = true;
                if (Random.Range(0, 6) != 0) continue;
                p[d].NoGravity = false;
                p[d].Scale *= 0.25f;
            }
        }

        /// <summary>
        /// PO:472-591 Spawn_HarpyCharm (kind 0 the Harpy Charm, 1 the Phoenix Quiver, 2 the Seraph
        /// Necklace): Item39 (Item20); four feathers flung any way and three along the arrow,
        /// shrinking, slowing and spinning down over 30 ticks (Item_320; Extra_306 / Extra_309,
        /// twice as many and fast, added); one vertical sparkle moving with it.
        /// </summary>
        public static void HarpyCharm(Vector2 at, Vector2 movement, int kind)
        {
            bool special = kind != 0;
            TerraMod.Instance?.PlaySound(kind == 1 ? "Item_20" : "Item_39");
            string tex = kind == 1 ? "Extra_306" : kind == 2 ? "Extra_309" : "Item_320";
            var tint = kind == 2 ? FxBatch.C(255, 220, 140, 100) : Color.white;
            if (special) tint.a = 0f;
            int wide = special ? 8 : 4, along = special ? 6 : 3;
            float speed = special ? 2f : 1f;
            for (int i = 0; i < wide + along; i++)
            {
                float spread = i < wide ? Mathf.PI * 2f : 0.31415927f;
                float r = Random.Range(-1f, 1f) * spread;
                var v = new Vector2(movement.x * Mathf.Cos(r) - movement.y * Mathf.Sin(r), movement.x * Mathf.Sin(r) + movement.y * Mathf.Cos(r))
                        * Random.value * (i < wide ? 0.25f : 1f) * speed;
                float spin = Random.Range(-1f, 1f) * Mathf.PI * 2f / 30f;
                Add(new Part
                {
                    Kind = Kind.Fading, Tex = tex, Pos = at, Vel = v, Accel = -v / 30f, Tint = tint, Life = 30, Scale2 = Vector2.one * 0.75f,
                    ScaleVel2 = Vector2.one * -0.3f / 30f, Rot = Random.value * Mathf.PI * 2f, RotVel = spin, RotAcc = -spin / 30f, FadeInN = 0.1f, FadeOutN = 0.1f,
                });
            }
            int life = Random.Range(20, 40);
            var shine = kind == 1 ? FxBatch.C(242, 42, 0, 20) : kind == 2 ? FxBatch.C(249, 225, 102) : HslToRgb(0.65f, 1f, 0.5f);
            if (kind == 0) shine.a = 0f;
            Add(new Part
            {
                Kind = Kind.Sparkle, Pos = at, Vel = movement * (special ? 0.3f : 1f), Rot = Mathf.PI / 2f, Life = life, Tint = shine, FadeIn = 0.1f, FadeOut = 0.5f,
                FlareIn = life / 2, FlareOut = life / 2, FlareEnd = life, Additive = 0.35f, Scale2 = new Vector2(8f, 0.4f),
            });
        }

        /// <summary>
        /// PO FlameWaders (the Hellfire Treads' trail): three FlameParticles across a cell's top,
        /// a random little flame each (Projectile_326/327/328), 60 ticks, shrinking to about half,
        /// faded from 40% of their life, drawn four times jittered, behind the player; now and then
        /// a fire dust.
        /// </summary>
        public static void FlameWaders(Vector2 at)
        {
            for (int k = -1; k <= 1; k++)
            {
                float r = Random.Range(0.1f, 1f);
                Add(new Part
                {
                    Kind = Kind.Fading, Tex = "Projectile_" + Random.Range(326, 329), Pos = at + new Vector2(k * 5.33f, 0f), Tint = FxBatch.C(120, 120, 120, 60), Life = 60,
                    Scale2 = Vector2.one * r, ScaleAcc2 = Vector2.one * (-r / 3600f), FadeInN = 0.001f, FadeOutN = 0.4f, Behind = true, Flame = true,
                });
                if (Random.Range(0, 16) != 0) continue;
                var p = TerraParticles.Instance;
                int d = p != null ? p.NewDust(at - new Vector2(4f, 4f), 0, 0, 6) : -1;
                if (d < 0) continue;
                p[d].Position = at + new Vector2(k * 5.33f, 0f);
                p[d].Velocity = p[d].Velocity * 0.6f + new Vector2(0f, -1.2f);
            }
        }

        /// <summary>A FadingParticle swelling from nothing: speed s a tick, slowing to a stop by the end of its life.</summary>
        static Part Swell(string tex, Vector2 at, Color tint, int life, float s, float fadeIn, float fadeOut, int delay) => new Part
        {
            Kind = Kind.Fading, Tex = tex, Pos = at, Tint = tint, Life = life, Scale2 = new Vector2(1e-4f, 1e-4f), ScaleVel2 = new Vector2(s, s),
            ScaleAcc2 = new Vector2(-s, -s) / life, FadeInN = fadeIn, FadeOutN = fadeOut, Delay = delay,
        };

        /// <summary>One 60 Hz tick: ABasicParticle.Update, the frame shuffles, the ageing.</summary>
        public static void Tick()
        {
            for (int i = parts.Count - 1; i >= 0; i--)
            {
                var p = parts[i];
                if (p.Delay > 0)
                {
                    p.Delay--;
                    parts[i] = p;
                    continue;
                }
                if (p.Kind != Kind.Icon) p.Pos += p.Vel;
                p.Vel += p.Accel;
                p.Scale += p.Grow;
                p.Scale2 += p.ScaleVel2;
                p.ScaleVel2 += p.ScaleAcc2;
                p.Rot += p.RotVel;
                p.RotVel += p.RotAcc;
                if (++p.Age >= p.Life)
                {
                    parts.RemoveAt(i);
                    continue;
                }
                if (p.Kind == Kind.Bloody && p.Burst && p.Age == 3)
                {
                    // Its burst: 50 motes of blood (5) from a 16 px box, half as fast every other, a third floating.
                    p.Burst = false;
                    var pp = TerraParticles.Instance;
                    if (pp != null)
                        for (int k = 0; k < 50; k++)
                        {
                            int d = pp.NewDust(p.Pos - new Vector2(8f, 8f), 16, 16, 5, 0f, 0f, 100, 1.25f + Random.value * 0.5f);
                            if (d < 0) continue;
                            pp[d].Velocity = Random.insideUnitCircle.normalized * 4f * (k % 2 == 0 ? 0.5f : 1f);
                            pp[d].NoGravity = k % 3 == 0;
                        }
                }
                if (p.Kind == Kind.Frame && ++p.Counter >= 2)
                {
                    p.Counter = 0;
                    p.Frame = Random.Range(0, 6);
                }
                parts[i] = p;
            }
        }

        public static void Draw(FxBatch fx)
        {
            if (fx == null) return;
            foreach (var p in parts)
            {
                if (p.Delay > 0) continue;
                float t = (float)p.Age / p.Life;
                if (p.Kind == Kind.Frame)
                {
                    float k = Mathf.Clamp01(Mathf.InverseLerp(0f, p.FadeIn, t)) * Mathf.Clamp01(Mathf.InverseLerp(1f, p.FadeOut, t));
                    fx.Draw(FxBatch.Frame("Projectile_916", 0, p.Frame * 22, 22, 22, 11f, 11f), p.Pos, p.Tint * k, p.Rot, p.Scale, overPlayer: true);
                    continue;
                }
                if (p.Kind == Kind.Fading)
                {
                    // FadingParticle: fading in over its first share of its life, out from its fade-out time on.
                    float fin = p.FadeInN > 0f ? p.FadeInN : 0.01f, fout = p.FadeOutN > 0f ? p.FadeOutN : 0.1f;
                    float k = Mathf.Clamp01(Mathf.InverseLerp(0f, fin, t)) * Mathf.Clamp01(Mathf.InverseLerp(1f, fout, t));
                    var scale = p.Scale2 != Vector2.zero ? p.Scale2 : Vector2.one * p.Scale;
                    if (scale.x <= 0f || scale.y <= 0f) continue;
                    if (p.Flame)
                    {
                        var whole = FxBatch.Whole(p.Tex);
                        if (whole == null) continue;
                        var spr = FxBatch.Frame(p.Tex, 0, 0, (int)whole.rect.width, (int)whole.rect.height, whole.rect.width * 0.5f, whole.rect.height);
                        for (int j = 0; j < 4; j++)
                            fx.Draw(spr, p.Pos + new Vector2(Random.Range(-2f, 2f), Random.Range(-2f, 2f)) * scale.x, p.Tint * k, 0f, scale, overPlayer: false);
                        continue;
                    }
                    var fadeTex = p.SrcSize > 0 ? FxBatch.Frame(p.Tex, 0, p.SrcY, p.SrcSize, p.SrcSize, p.SrcSize * 0.5f, p.SrcSize * 0.5f) : FxBatch.Whole(p.Tex ?? "Extra_270");
                    fx.Draw(fadeTex, p.Pos, p.Tint * k, p.Rot, scale, overPlayer: !p.Behind);
                    continue;
                }
                if (p.Kind == Kind.Bloody)
                {
                    // BloodyExplosionParticle.Draw: a bloom (Extra_174) over its burst (Extra_267), each by its own fade, swelling a little.
                    float grown = p.Scale * Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(t / 0.3f));
                    float outer = Mathf.Clamp01(Mathf.InverseLerp(0f, p.FadeIn, t) * Mathf.InverseLerp(1f, p.FadeOut, t)) * 0.4f;
                    float inner = Mathf.Clamp01(Mathf.InverseLerp(0f, p.FadeIn * 0.75f, t) * Mathf.InverseLerp(0.45f, 0.3f, t)) * 0.4f;
                    fx.Draw(FxBatch.Whole("Extra_174"), p.Pos, p.Tint * outer, p.Rot, grown * (1.1f + 0.15f * t), overPlayer: false);
                    fx.Draw(FxBatch.Whole("Extra_267"), p.Pos, p.Tint * inner, p.Rot, grown * (1f + 0.05f * t), overPlayer: false);
                    continue;
                }
                if (p.Kind == Kind.Icon)
                {
                    // ShockIconParticle.Draw: the alert (Extra_268) popping up (0.2, 0.9, 1.3, 0.9 across its life), half seen, rising off where it began.
                    float s4 = t < 1f / 3f ? Mathf.Lerp(0.2f, 0.9f, t * 3f) : t < 2f / 3f ? Mathf.Lerp(0.9f, 1.3f, t * 3f - 1f) : Mathf.Lerp(1.3f, 0.9f, t * 3f - 2f);
                    float o = Mathf.Clamp01(Mathf.InverseLerp(0f, p.FadeIn, t) * Mathf.InverseLerp(1f, p.FadeOut, t)) * 0.5f;
                    fx.Draw(FxBatch.Whole("Extra_268"), p.Pos + p.Vel * t, p.Tint * o, 0f, s4 * p.Scale, overPlayer: false);
                    continue;
                }
                // PrettySparkleParticle.Draw: fades in and out by its life, flares between its tick marks; its additive share.
                float opacity = Mathf.Clamp01(Mathf.InverseLerp(0f, p.FadeIn, t)) * Mathf.Clamp01(Mathf.InverseLerp(1f, p.FadeOut, t));
                float n = Mathf.Clamp01(Mathf.InverseLerp(0f, p.FlareIn, p.Age)) * Mathf.Clamp01(Mathf.InverseLerp(p.FlareEnd, p.FlareOut, p.Age));
                var c2 = p.Tint * opacity * 0.5f;
                c2.a *= 1f - p.Additive;
                var white = Color.white * opacity * 0.9f;
                white.a *= 0.5f;
                var c3 = white * 0.5f;
                var tex = FxBatch.Whole("Extra_98");
                var sv = p.Scale2 != Vector2.zero ? p.Scale2 : Vector2.one * p.Scale;
                var h = Vector2.Scale(new Vector2(0.3f, 2f), sv) * n;
                var u = Vector2.Scale(new Vector2(0.3f, 1f), sv) * n;
                fx.Draw(tex, p.Pos, c2 * n, Mathf.PI / 2f + p.Rot, h, overPlayer: true);
                if (!p.NoVertical) fx.Draw(tex, p.Pos, c2 * n, p.Rot, u, overPlayer: true);
                fx.Draw(tex, p.Pos, c3 * n, Mathf.PI / 2f + p.Rot, h * 0.6f, overPlayer: true);
                if (!p.NoVertical) fx.Draw(tex, p.Pos, c3 * n, p.Rot, u * 0.6f, overPlayer: true);
            }
        }

        /// <summary>
        /// PO Spawn_Keybrand: three gold-to-orange-red sparkles flung out and drawn back, three
        /// white-gold streaks (Extra_98) out the same way, and a cornflower bloom (Extra_174)
        /// swelling and shrinking, all over the player for 30-60 ticks.
        /// </summary>
        public static void DeadCellsFlintStreak(Vector2 at)
        {
            // PO:1859-1878: an orange streak rising off the smoke, horizontal only, 20-40 ticks, a third added.
            int life = Random.Range(20, 40);
            var tint = HslToRgb(0.12f, 1f, 0.5f);
            tint.a = 0f;
            Add(new Part
            {
                Kind = Kind.Sparkle, Pos = at, Vel = new Vector2(0f, -2f), Rot = Mathf.PI / 2f, Life = life, Tint = tint, FadeIn = 0.1f, FadeOut = 0.95f,
                FlareIn = life / 2, FlareOut = life / 2, FlareEnd = life, Additive = 0.35f, NoVertical = true,
                Scale2 = new Vector2(2f + Random.value * 3f, 0.7f + Random.value * 0.7f),
            });
        }

        public static void Keybrand(Vector2 at)
        {
            const float k = 0.7f;
            var gold = new Color(1f, 215f / 255f, 0f);
            var orangeRed = new Color(1f, 69f / 255f, 0f);
            float start = Random.value * Mathf.PI * 2f;
            for (int n = 0; n < 3; n++)
            {
                float a = Mathf.PI * 2f * n / 3f + start + Random.Range(-1f, 1f) * 0.1f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var s = Sparkle(at + Random.insideUnitCircle * 4f * k, dir * 1.5f * k, Color.Lerp(gold, orangeRed, Random.value), 1f);
                s.Accel = -dir * 1.5f * k / 60f;
                s.Rot = a;
                s.Scale2 = Vector2.one * k * 0.8f;
                Add(s);
            }
            start = Random.value * Mathf.PI * 2f;
            for (int n = 0; n < 3; n++)
            {
                float a = Mathf.PI * 2f * n / 3f + start + Random.Range(-1f, 1f) * 0.1f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var tint = Color.Lerp(Color.white, Color.Lerp(gold, orangeRed, Random.value), 0.5f);
                tint.a = 0f;
                Add(new Part
                {
                    Kind = Kind.Fading, Tex = "Extra_98", Pos = at + dir * k * k * 30f * 0.2f + Random.insideUnitCircle * 4f * k, Vel = dir * k, Accel = -dir * k / 30f,
                    Tint = tint, Life = 30, Rot = a + Mathf.PI / 2f, FadeInN = 0.3f, FadeOutN = 0.4f, Scale2 = new Vector2(0.5f, 1.2f) * 0.8f * k,
                });
            }
            var bloom = Color.Lerp(new Color(100f / 255f, 149f / 255f, 237f / 255f), Color.white, Random.value);
            bloom.a = 127f / 255f;
            var grow = new Vector2(Random.Range(0.7f, 1.3f), Random.Range(0.7f, 1.3f));
            Add(new Part
            {
                Kind = Kind.Fading, Tex = "Extra_174", Pos = at + Random.insideUnitCircle * 4f * k, Tint = bloom, Life = 30, Rot = Random.value * Mathf.PI * 2f + Mathf.PI / 2f,
                FadeInN = 0.1f, FadeOutN = 0.4f, Scale2 = Vector2.one * 0.1f * k, ScaleVel2 = grow / 60f, ScaleAcc2 = -grow / 60f / 60f,
            });
        }

        /// <summary>
        /// PO Spawn_DeadCellsMushroomBoiExplosion (PO:2426-2517): a red bloody burst behind the
        /// player; three red rings of Extra_269 spinning out and fading over 15 ticks, one wider;
        /// six red streaks (Extra_89) flung out and slowing; the burst's sound half the time.
        /// </summary>
        public static void MushroomExplosion(Vector2 at)
        {
            if (Random.Range(0, 2) == 0) TerraMod.Instance?.PlaySound("Custom/deadcells_mushroom_explode");
            Add(new Part { Kind = Kind.Bloody, Pos = at, Life = 10, Tint = FxBatch.C(255, 10, 10, 50) * 0.35f, FadeIn = 0.3f, FadeOut = 0.6f, Scale = 1f, Burst = true });
            var red = FxBatch.C(255, 10, 10, 127) * 0.45f;
            const float n = 15f;
            float spin = Mathf.PI * 2f / n * (Random.value < 0.5f ? -1f : 1f) * Random.value;
            var grow = Vector2.one * (1.2f / n);
            Add(new Part { Kind = Kind.Fading, Tex = "Extra_269", SrcY = 0, SrcSize = 100, Pos = at, Life = 15, Tint = red, Rot = Random.value * Mathf.PI * 2f, RotVel = spin, RotAcc = -spin / n,
                FadeInN = 0.01f, FadeOutN = 0.1f, Scale2 = Vector2.one * 0.6f, ScaleVel2 = grow, ScaleAcc2 = grow * (-1f / n) });
            Add(new Part { Kind = Kind.Fading, Tex = "Extra_269", SrcY = 101, SrcSize = 100, Pos = at, Life = 12, Tint = red * 0.25f,
                FadeInN = 0.1f, FadeOutN = 0.1f, Scale2 = Vector2.one * 0.3f, ScaleVel2 = Vector2.one * 6.5f / n, ScaleAcc2 = Vector2.one * 6.5f / n * (-1f / n) });
            Add(new Part { Kind = Kind.Fading, Tex = "Extra_269", SrcY = 101, SrcSize = 100, Pos = at, Life = 15, Tint = red * 0.3f,
                FadeInN = 0.4f, FadeOutN = 0.5f, Scale2 = Vector2.one * 0.6f, ScaleVel2 = grow, ScaleAcc2 = grow * (-1f / n) });
            Add(new Part { Kind = Kind.Fading, Tex = "Extra_269", SrcY = 202, SrcSize = 100, Pos = at, Life = 15, Tint = red,
                FadeInN = 0.01f, FadeOutN = 0.7f, Scale2 = Vector2.one * 0.54f, ScaleVel2 = grow * 1.5f, ScaleAcc2 = grow * 1.5f * (-1f / n) });
            for (float f = 0f; f < 1f; f += 1f / 6f)
            {
                float a = Mathf.PI * 2f * f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var v = dir * (4f + 7f * Random.value);
                Add(new Part { Kind = Kind.Fading, Tex = "Extra_89", Pos = at + dir * (10f + 90f * Random.value) - v * 4f, Vel = v, Accel = -v / n, Life = 15, Tint = red, Rot = a + Mathf.PI / 2f,
                    FadeInN = 0.2f, FadeOutN = 0.3f, Scale2 = new Vector2(0.4f, 0.8f) * (0.6f + 0.6f * Random.value), Behind = true });
            }
        }

        /// <summary>
        /// PO Spawn_DeadCellsBarrelExplosion: the Mushroom Boi's burst in cyan and bigger (25 ticks,
        /// the rings from twice as large), and 25 streaks round it behind the player. The seven
        /// sparks (267) are the projectile's to make.
        /// </summary>
        public static void DeadCellsBarrelExplosion(Vector2 at)
        {
            var cyan = FxBatch.C(52, 208, 254, 20);
            const float n = 25f;
            float spin = Mathf.PI * 2f / n * (Random.value * 2f - 1f);
            var grow = Vector2.one * (2f / n);
            Add(new Part { Kind = Kind.Fading, Tex = "Extra_269", SrcY = 0, SrcSize = 100, Pos = at, Life = (int)n, Tint = cyan, Rot = Random.value * Mathf.PI * 2f, RotVel = spin, RotAcc = -spin / n,
                FadeInN = 0.01f, FadeOutN = 0.1f, Scale2 = Vector2.one, ScaleVel2 = grow, ScaleAcc2 = grow * (-1f / n) });
            Add(new Part { Kind = Kind.Fading, Tex = "Extra_269", SrcY = 101, SrcSize = 100, Pos = at, Life = (int)(n * 0.8f), Tint = cyan * 0.25f,
                FadeInN = 0.1f, FadeOutN = 0.1f, Scale2 = Vector2.one * 0.5f, ScaleVel2 = Vector2.one * 8.5f / n, ScaleAcc2 = Vector2.one * 8.5f / n * (-1f / n) });
            Add(new Part { Kind = Kind.Fading, Tex = "Extra_269", SrcY = 101, SrcSize = 100, Pos = at, Life = (int)n, Tint = cyan * 0.3f,
                FadeInN = 0.4f, FadeOutN = 0.5f, Scale2 = Vector2.one, ScaleVel2 = grow, ScaleAcc2 = grow * (-1f / n) });
            Add(new Part { Kind = Kind.Fading, Tex = "Extra_269", SrcY = 202, SrcSize = 100, Pos = at, Life = (int)n, Tint = cyan,
                FadeInN = 0.01f, FadeOutN = 0.7f, Scale2 = Vector2.one * 0.9f, ScaleVel2 = grow * 1.5f, ScaleAcc2 = grow * 1.5f * (-1f / n) });
            for (float f = 0f; f < 1f; f += 0.04f)
            {
                float a = Mathf.PI * 2f * f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var v = dir * (4f + 7f * Random.value);
                Add(new Part { Kind = Kind.Fading, Tex = "Extra_89", Pos = at + dir * (10f + 90f * Random.value) - v * 4f, Vel = v, Accel = -v / n, Life = (int)n, Tint = cyan, Rot = a + Mathf.PI / 2f,
                    FadeInN = 0.2f, FadeOutN = 0.3f, Scale2 = new Vector2(0.4f, 0.8f) * (0.6f + 0.6f * Random.value), Behind = true });
            }
        }

        /// <summary>PO Spawn_CattivaHit (PO:3765-3793): six gold, orange or white streaks flung out round it and drawn back, horizontal only.</summary>
        public static void CattivaHit(Vector2 at)
        {
            float start = Random.value * Mathf.PI * 2f;
            const float life = 16f;
            for (float f = 0f; f < 1f; f += 1f / 6f)
            {
                float a = Mathf.PI * 2f * f + start + (Random.value * 2f - 1f) * 0.25f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                var v = dir * 4.5f * 0.7f;
                var tint = Random.Range(0, 2) == 0 ? Color.Lerp(Color.white, new Color(1f, 0.65f, 0f), Random.value) : Color.Lerp(new Color(1f, 0.84f, 0f), new Color(1f, 0.65f, 0f), Random.value);
                float s = 0.7f * (Random.value * 0.7f + 0.1f);
                Add(new Part
                {
                    Kind = Kind.Sparkle, Pos = at + Random.insideUnitCircle * 6f * 0.7f, Vel = v, Accel = -v / life * 0.5f, Rot = a, Life = (int)life, Tint = tint,
                    FadeIn = 0.1f, FadeOut = 0.5f, FlareIn = 0, FlareOut = 0, FlareEnd = life, NoVertical = true, Scale2 = new Vector2(s * 4f, s),
                });
            }
        }

        /// <summary>PO Spawn_DeadCellsMushroomBoiTargetFound (PO:2519-2532): a yellow alert rising 40 px off its head over 15 ticks.</summary>
        public static void MushroomTargetFound(Vector2 top)
        {
            Add(new Part { Kind = Kind.Icon, Pos = top, Vel = new Vector2(0f, -40f), Life = 15, Tint = FxBatch.C(255, 255, 10, 150), FadeIn = 0.2f, FadeOut = 0.7f, Scale = 1f });
        }

        public static void Clear() => parts.Clear();
    }
}
