using UnityEngine;

namespace TerraKnight.Effects
{
    /// <summary>The 10-21 batch's ParticleOrchestrator particles: the Arc Surge's (Spawn_ArcLightning, Red Lightning's spark) and the Resonance Scepter's.</summary>
    internal static partial class FxParticles
    {
        /// <summary>
        /// PO Spawn_LightningSmall with a colour (RedLightningSmall, Red Lightning's spark one tick in ten): one or two sparks of
        /// Projectile_916 in the Arc Surge's red fading to three quarters of it, and half the time a mote (331) of the same.
        /// </summary>
        public static void RedLightningSmall(Vector2 at, Vector2 movement)
        {
            var red = FxBatch.C(255, 60, 60, 0);
            var dark = red * 0.75f;
            int n = Random.Range(1, 3);
            float start = Random.value * Mathf.PI * 2f;
            for (int k = 0; k < n; k++)
            {
                float a = Mathf.PI * 2f * k / n + start + Random.Range(-1f, 1f) * 0.25f;
                float speed = Random.value * 4f + 0.1f;
                var v = new Vector2(Mathf.Cos(a), Mathf.Sin(a) * 0.5f) * speed * 0.2f + movement;
                var tint = Color.Lerp(red, dark, Random.value);
                var pos = at + Random.insideUnitCircle * 6f * 0.7f;
                Add(new Part
                {
                    Kind = Kind.Frame, Pos = pos, Vel = v, Rot = Mathf.Atan2(v.y, v.x), Scale = 1f, Grow = 0.025f, Life = 10, Frame = Random.Range(0, 6),
                    Tint = tint, FadeIn = 0.01f, FadeOut = 0.5f,
                });
                if (Random.Range(0, 2) != 0) continue;
                var p = TerraParticles.Instance;
                int i = p != null ? p.NewDust(pos, 0, 0, 331) : -1;
                if (i < 0) continue;
                p[i].Position = pos;
                p[i].Scale = 1f;
                p[i].NoGravity = true;
                p[i].Velocity = Random.insideUnitCircle * 4f;
                p[i].Color = tint;
            }
        }

        /// <summary>
        /// Spawn_ArcLightning's flash at the hand: a frame of Projectile_916 stretched along the bolt (twice as long as wide,
        /// growing), and a flickering one beside it, for the bolt's life.
        /// </summary>
        public static void ArcSurgeHand(Vector2 hand, float rotation, int life)
        {
            var red = FxBatch.C(255, 60, 60, 0);
            Add(new Part
            {
                Kind = Kind.Fading, Pos = hand + Random.insideUnitCircle * 6f * 0.7f, Rot = rotation, Scale2 = new Vector2(1f, 2f), ScaleVel2 = new Vector2(0.025f, 0.025f),
                Life = life, Tint = red, Tex = "Projectile_916", SrcY = 22 * Random.Range(0, 6), SrcSize = 22, FadeInN = 0.1f, FadeOutN = 0.9f,
            });
            Add(new Part
            {
                Kind = Kind.Frame, Pos = hand + Random.insideUnitCircle * 6f * 0.7f, Rot = rotation, Scale = 1f, Grow = 0.025f, Life = life + Random.Range(4, 9),
                Frame = Random.Range(0, 6), Tint = red, FadeIn = 0.01f, FadeOut = 0.5f,
            });
        }

        /// <summary>
        /// PO Spawn_PrincessWeapon: a pink 4-point sparkle and a white one inside it drifting along, slowing, and two pink motes
        /// (267) thrown with them.
        /// </summary>
        public static void PrincessWeapon(Vector2 at, Vector2 movement)
        {
            var v = movement * (0.6f + Random.value * 0.35f);
            float size = Random.value * 0.4f + 0.2f;
            var spot = at + Random.insideUnitCircle * 8f * size;
            var pink = HslToRgb((0.92f + Random.value * 0.02f) % 1f, 1f, 0.4f + Random.value * 0.25f);
            pink.a = 0f;
            var sparkle = Sparkle(spot, v * 0.66f, pink, size);
            sparkle.Accel = -v / 60f;
            Add(sparkle);
            var white = Sparkle(spot, v * 0.66f, new Color(1f, 1f, 1f, 0f), size * 0.6f);
            white.Accel = -v / 60f;
            Add(white);
            var p = TerraParticles.Instance;
            if (p == null) return;
            for (int k = 0; k < 2; k++)
            {
                int i = p.NewDust(at, 0, 0, 267);
                if (i < 0) continue;
                p[i].Position = at;
                p[i].Color = HslToRgb((0.92f + Random.value * 0.02f) % 1f, 1f, 0.4f + Random.value * 0.25f);
                p[i].Velocity = Random.insideUnitCircle * 2f + movement * (0.5f + 0.5f * Random.value) * 1.4f;
                p[i].NoGravity = true;
                p[i].Scale = 0.6f + Random.value * 0.4f;
            }
        }
    }
}
