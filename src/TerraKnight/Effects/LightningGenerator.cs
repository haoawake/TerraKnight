using System;
using System.Collections.Generic;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Effects
{
    /// <summary>
    /// Terraria's LightningGenerator (1.4.5, GameContent/LightningGenerator.cs) in Terraria px (y down): a bolt's path walked
    /// from a seed with the same LCG32Random, so a seed gives Terraria's shape, its forks and their rotations; a block on the
    /// way (the grid's cells, Hallownest's own ground by its colliders) stops it. StormLightningDrawer's strip is drawn as
    /// Extra_288's glow stretched along each segment, added.
    /// </summary>
    internal sealed class LightningGenerator
    {
        public sealed class Bolt
        {
            public Vector2[] Positions;
            public float[] Rotations;
            /// <summary>Its stretch of the main bolt's length (FloatRange progressRange).</summary>
            public float RangeMin, RangeMax;
            public int ForkDepth;
            public bool CollidedWithTile;
            public bool IsMainBolt => ForkDepth == 0;
        }

        /// <summary>Terraria.Utilities.LCG32Random.</summary>
        struct Lcg
        {
            public uint State;
            public Lcg(uint seed) { State = seed; }
            void Advance() => State = unchecked(State * 2438952949u + 1u);
            public double NextDouble()
            {
                Advance();
                return State / 4294967296.0;
            }
            public float NextFloat() => (float)NextDouble();
            public int Next(int max)
            {
                Advance();
                return (int)(((ulong)State * (ulong)max) >> 32);
            }
        }

        public bool SolidTileCollision;
        public float SourceRotationLimit = Mathf.PI / 9f, Length = 1000f, RotationStrength;
        public int StepSize, Layers;
        public float LayerStrengthFactor, PerpendicularDeviationFactor, ReduceRandomnessAfter, ForkGenerationThresholdAngleFraction,
            ForkReflectAngleMultiplier, ForkRotationStrengthMultiplier, ForkStepSizeMultiplier, ForkLengthMultiplier;
        public int MaxForksPerBolt, MaxForkDepth;
        public float ForkRangeMin, ForkRangeMax;

        /// <summary>GetArcSurgeWeaponGenerator: the Arc Surge's bolt from the hand to its end, wilder and forking more the shorter it is.</summary>
        public static LightningGenerator ArcSurge(Vector2 start, Vector2 end)
        {
            float d = Vector2.Distance(start, end);
            return new LightningGenerator
            {
                SourceRotationLimit = 0f, Length = 1f, RotationStrength = 0.7f, StepSize = 6, Layers = 5, LayerStrengthFactor = 1.2f,
                PerpendicularDeviationFactor = Remap(d, 0f, 1000f, 5f, 1f), ReduceRandomnessAfter = 0.7f,
                ForkGenerationThresholdAngleFraction = Remap(d, 0f, 1000f, 0.3f, 0.5f), ForkReflectAngleMultiplier = Remap(d, 0f, 1000f, 0.6f, 0.2f),
                ForkRotationStrengthMultiplier = 0.9f, ForkStepSizeMultiplier = 0.8f, ForkLengthMultiplier = 0.7f, MaxForksPerBolt = 3, MaxForkDepth = 3,
                ForkRangeMin = Remap(d, 0f, 1000f, 0.1f, 0.5f), ForkRangeMax = 0.8f, SolidTileCollision = true,
            };
        }

        /// <summary>GenerateMainBoltPath: the main bolt alone, no forks, no rotations (what it hits along).</summary>
        public Bolt MainPath(uint seed, Vector2 target, Vector2 direction) => Generate(null, seed, target, direction, false);

        /// <summary>Generate: the main bolt and its forks into the list (the main one last), with their rotations.</summary>
        public Bolt Generate(List<Bolt> bolts, uint seed, Vector2 target, Vector2 direction, bool rotations = true)
        {
            double r = new Lcg(seed).NextDouble();
            direction = Rotate(direction, (float)((r * 2.0 - 1.0) * SourceRotationLimit)) * Length;
            var main = GenerateBolt(bolts, seed, 0, target - direction, target, RotationStrength, StepSize, 0f, 1f);
            if (rotations && bolts != null)
                foreach (var b in bolts) b.Rotations = CalcRotations(b.Positions);
            return main;
        }

        Bolt GenerateBolt(List<Bolt> bolts, uint seed, int depth, Vector2 startPos, Vector2 targetPos, float rotationStrength, float stepSize, float rangeMin, float rangeMax)
        {
            var rng = new Lcg(seed);
            float turn = 0f;
            var layers = new float[Layers];
            var targetTile = TileOf(targetPos);
            Vector2 pos = startPos;
            Vector2 along = targetPos - startPos;
            float length = along.magnitude;
            along /= Mathf.Max(length, 1e-4f);
            var side = new Vector2(along.y, -along.x);
            int steps = (int)Mathf.Max(length * 2f / stepSize, 1f);
            int forks = 0;
            var points = new Vector2[steps];
            var bolt = new Bolt { Positions = points, ForkDepth = depth, RangeMin = rangeMin, RangeMax = rangeMax };
            int i;
            for (i = 0; i < steps; i++)
            {
                points[i] = pos;
                Vector2 toTarget = targetPos - pos;
                float ahead = Vector2.Dot(toTarget, along);
                if (ahead < stepSize) break;
                float progress = Mathf.Clamp01(1f - ahead / length);
                if (SolidTileCollision && TileOf(pos) != targetTile && TileCollision(pos))
                {
                    bolt.RangeMax = Mathf.Lerp(rangeMin, rangeMax, progress);
                    bolt.CollidedWithTile = true;
                    break;
                }
                toTarget /= toTarget.magnitude;
                float off = -Vector2.Dot(toTarget, side);
                float spread = Mathf.Max(0.01f, Mathf.Min(progress, 1f - progress) * PerpendicularDeviationFactor * 2f);
                float pull = Mathf.Clamp(off / spread, -1f, 1f);
                if (PickLayerToReroll(rng.NextDouble(), 0.5f, out int layer))
                {
                    float strength = rotationStrength;
                    for (int k = Layers - 1; k > layer; k--) strength /= LayerStrengthFactor;
                    float roll = (float)rng.NextDouble() * 2f - 1f;
                    roll += (pull - roll * Mathf.Abs(pull)) / 2f;
                    float now = roll * strength;
                    float change = now - layers[layer];
                    turn += change;
                    layers[layer] = now;
                    if (layer == Layers - 1)
                    {
                        float chance = rng.NextFloat();
                        float allowed = Remap(forks, 0f, MaxForksPerBolt, 1f, 0f);
                        float forkTurn = turn - change * (1f + ForkReflectAngleMultiplier);
                        if (bolts != null && Mathf.Abs(change) >= rotationStrength * ForkGenerationThresholdAngleFraction && progress >= ForkRangeMin && progress <= ForkRangeMax
                            && depth < MaxForkDepth && chance < allowed && Mathf.Abs(forkTurn) < Mathf.PI * 4f / 9f)
                        {
                            forks++;
                            float share = (1f - progress) * ForkLengthMultiplier;
                            Vector2 forkTarget = pos + Rotate(toTarget, forkTurn) * length * share;
                            GenerateBolt(bolts, unchecked(rng.State + 1u), depth + 1, pos, forkTarget, rotationStrength * ForkRotationStrengthMultiplier, stepSize * ForkStepSizeMultiplier,
                                Mathf.LerpUnclamped(rangeMin, rangeMax, progress), Mathf.LerpUnclamped(rangeMin, rangeMax, progress + share));
                        }
                    }
                }
                float calm = Remap(progress, ReduceRandomnessAfter, 1f, 0f, 1f) + Remap(Mathf.Abs(pull), 0.5f, 1f, 0f, 1f);
                if (PickHighLayerToReroll(rng.NextDouble(), calm, out layer))
                {
                    turn -= layers[layer];
                    layers[layer] = 0f;
                }
                pos += Rotate(toTarget, turn) * stepSize;
            }
            if (i < steps)
            {
                Array.Resize(ref points, i + 1);
                bolt.Positions = points;
            }
            if (bolts != null && (bolt.IsMainBolt || i > 2)) bolts.Add(bolt);
            return bolt;
        }

        /// <summary>A block at a point: the grid's, or Hallownest's own ground under it.</summary>
        static bool TileCollision(Vector2 px)
        {
            var world = TerraParticles.ToWorld(px);
            var cell = CellGrid.WorldToCell(world);
            return TerraWorld.IsSolid(cell.x, cell.y) || Physics2D.OverlapPoint(world, 1 << ChunkColliders.TerrainLayer) != null;
        }

        static Vector2Int TileOf(Vector2 px) => new Vector2Int(Mathf.FloorToInt(px.x / 16f), Mathf.FloorToInt(px.y / 16f));

        bool PickLayerToReroll(double r, float chance, out int layer)
        {
            for (layer = 0; layer < Layers; layer++)
            {
                if (r >= 1f - chance) return true;
                r /= chance;
            }
            return false;
        }

        bool PickHighLayerToReroll(double r, float chance, out int layer)
        {
            if (!PickLayerToReroll(r, chance, out layer)) return false;
            layer = Layers - 1 - layer;
            return true;
        }

        public static float[] CalcRotations(Vector2[] positions)
        {
            var rotations = new float[positions.Length];
            CalcRotations(positions, rotations);
            return rotations;
        }

        public static void CalcRotations(Vector2[] positions, float[] rotations)
        {
            if (rotations.Length < 2) return;
            int n = 0;
            float last = Angle(positions[0] - positions[1]);
            rotations[n++] = last;
            while (n < rotations.Length - 1)
            {
                float next = Angle(positions[n] - positions[n + 1]);
                rotations[n++] = last + Wrap(next - last) / 2f;
                last = next;
            }
            rotations[n] = last;
            float prev = rotations[0];
            for (int i = 1; i < rotations.Length - 1; i++)
            {
                float here = rotations[i], after = rotations[i + 1];
                rotations[i] = here + (Wrap(prev - here) + Wrap(after - here)) / 2f;
                prev = here;
            }
        }

        // ------------------------------------------------------------------ StormLightningDrawer

        /// <summary>StormLightningDrawer.AnimParams.</summary>
        public struct Anim
        {
            public float InitialOpacity, FadeStart, FadeMid, FadeEnd, FinalOpacity, WaveLength, WaveIn, WaveOut;
        }

        /// <summary>StormLightningDrawer.ArcSurge.</summary>
        public static readonly Anim ArcSurgeAnim = new Anim
        {
            InitialOpacity = 0.5f, FadeStart = 0.1f, FadeMid = 0.4f, FadeEnd = 1f, FinalOpacity = 0.3f, WaveLength = 0.3f, WaveIn = 0.3f, WaveOut = 0.5f,
        };

        /// <summary>
        /// StormLightningParticle.Draw: every bolt as its strip at this point of its life (0..1), forks fainter by depth; the
        /// strip's colour runs in along it and out again (WaveTransition), its width narrowing to the end and with age.
        /// Extra_288 (the StormLightning shader's image) is the glow across it; a white core of it runs down the middle.
        /// </summary>
        public static void Draw(FxBatch fx, List<Bolt> bolts, float progress, Color color, float width, float intensity, in Anim a)
        {
            if (fx == null || bolts == null) return;
            var glow = FxBatch.Whole("Extra_288");
            float opacity = Remap(progress, a.FadeStart, a.FadeMid, a.InitialOpacity, 1f) * Remap(progress, a.FadeMid, a.FadeEnd, 1f, a.FinalOpacity);
            float waveIn = Remap(progress, 0f, a.WaveIn, 0f, 1f), waveOut = Remap(progress, 1f - a.WaveOut, 1f, 0f, 1f);
            foreach (var b in bolts)
            {
                var pts = b.Positions;
                if (pts == null || pts.Length < 2) continue;
                float strength = (b.IsMainBolt ? 1f : 0.5f * Mathf.Pow(0.8f, b.ForkDepth - 1)) * intensity * opacity;
                bool taper = b.RangeMax < 1f;
                int last = pts.Length - 1;
                // Two points a piece: the segments are six px, the strip twelve wide.
                for (int i = 0; i < last; i += 2)
                {
                    int j = Mathf.Min(i + 2, last);
                    float t = Mathf.LerpUnclamped(b.RangeMin, b.RangeMax, (i + j) * 0.5f / last);
                    float shade = Wave(t, waveIn, a.WaveLength, 0f, 1f) * Wave(t, waveOut, a.WaveLength, 1f, 0f) * strength;
                    if (shade <= 0.004f) continue;
                    float w = width * Remap(t, 0.5f, 1f, 1f, 0.5f) * Remap(progress, 0.5f, 1f, 1f, 0.5f);
                    if (taper) w *= Remap(b.RangeMax - t, 0.1f, 0f, 1f, b.IsMainBolt ? 0.5f : 0f);
                    if (w <= 0.05f) continue;
                    Vector2 p0 = pts[i], p1 = pts[j], d = p1 - p0;
                    float len = d.magnitude;
                    if (len < 0.01f) continue;
                    float rot = Mathf.Atan2(d.y, d.x);
                    var mid = (p0 + p1) * 0.5f;
                    var scale = new Vector2((len + 2f) / 256f, w / 256f);
                    fx.Draw(glow, mid, new Color(color.r * shade, color.g * shade, color.b * shade, 0f), rot, scale, overPlayer: true);
                    float core = shade * 0.8f;
                    fx.Draw(glow, mid, new Color(core, core, core, 0f), rot, new Vector2(scale.x, scale.y * 0.4f), overPlayer: true);
                }
            }
        }

        /// <summary>StormLightningDrawer.WaveTransition.</summary>
        static float Wave(float onStrip, float wave, float length, float from, float to) =>
            Remap(onStrip, Mathf.Lerp(-length, 1f, wave), Mathf.Lerp(0f, 1f + length, wave), to, from);

        static float Remap(float v, float a, float b, float c, float d) => Mathf.Lerp(c, d, Mathf.Clamp01((v - a) / (b - a)));
        static float Angle(Vector2 v) => Mathf.Atan2(v.y, v.x);
        static float Wrap(float a) => Mathf.Repeat(a + Mathf.PI, Mathf.PI * 2f) - Mathf.PI;
        static Vector2 Rotate(Vector2 v, float a)
        {
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }
}
