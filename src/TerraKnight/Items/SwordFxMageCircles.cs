using System.Collections.Generic;
using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The magic weapons' circles (the user's 10-28 "更华丽更高大上的施法动画，越后期的武器我希望越伟大"): rings of runes made of the
    /// weapon's own motes (Terraria's dusts in its colours), turning, kept a while at a place or under the player. The later
    /// the weapon (MageArts.Grand), the more of them as it casts: a rune at the head (2), a circle under the feet (3), a
    /// standing circle behind turning the other way (4), a wide halo round him (5); kept while it is cast again, not made
    /// anew each time. Its echoes and great shots, its novas and its arts each open their own. Nothing here hits.
    /// </summary>
    internal sealed partial class SwordFx
    {
        sealed class Circle
        {
            /// <summary>Where (Terraria px), or with Follow its offset from the feet.</summary>
            public Vector2 At;
            public bool Follow;
            /// <summary>Its radius (px), how flat (1 round, less a ring on the floor), its turn (radians a second).</summary>
            public float R, Squash = 1f, Spin = 1.5f;
            public float Born, Until, FadeOut = 0.3f;
            public MageArts.Look Look;
            /// <summary>Runes: short strokes from the inner ring out, this many round it.</summary>
            public int Spokes;
            public string Key;
            /// <summary>Drawn behind the player (the standing circle at his back).</summary>
            public bool Behind;
        }

        readonly List<Circle> circles = new List<Circle>();

        /// <summary>Dev: circles opened, great shots', novas', runes' and arts' bursts.</summary>
        public static int DevCircles, DevGreatFx, DevNovaFx, DevRuneFx, DevArtFx;

        /// <summary>A circle (a keyed one kept going instead while it is still open).</summary>
        Circle OpenCircle(MageArts.Look look, Vector2 at, float r, float squash, float life, int spokes = 0, bool follow = false, float spin = 1.5f, string key = null)
        {
            float now = Time.time;
            if (key != null)
                foreach (var c in circles)
                    if (c.Key == key && now < c.Until + c.FadeOut)
                    {
                        c.Until = Mathf.Max(c.Until, now + life);
                        c.Look = look;
                        c.R = r;
                        return c;
                    }
            var n = new Circle { At = at, Follow = follow, R = r, Squash = squash, Spin = spin, Born = now, Until = now + life, Look = look, Spokes = spokes, Key = key };
            circles.Add(n);
            DevCircles++;
            return n;
        }

        void TickCircles(HeroController hero)
        {
            if (circles.Count == 0) return;
            var feet = FeetPx(hero);
            float now = Time.time;
            for (int i = circles.Count - 1; i >= 0; i--)
            {
                var c = circles[i];
                if (now > c.Until + c.FadeOut) { circles.RemoveAt(i); continue; }
                float alpha = Mathf.Clamp01((now - c.Born) / 0.15f) * Mathf.Clamp01((c.Until + c.FadeOut - now) / c.FadeOut);
                var at = c.Follow ? feet + c.At : c.At;
                float turn = (now - c.Born) * c.Spin;
                var look = c.Look;
                Color Tint() => Color.Lerp(look.A, look.B, Random.value * 0.7f);
                void Mote(Vector2 p, Vector2 v, float scale)
                {
                    int m = Dust(p, look.Dust, v, look.Dust == 278 ? 40 : 90, scale, true, Tint());
                    FadeIn(m, scale * 0.8f);
                }
                Vector2 On(float ang, float rr) => at + new Vector2(Mathf.Cos(ang) * rr, Mathf.Sin(ang) * rr * c.Squash);
                // The outer ring, its motes drifting round with it; a fainter inner one.
                int outer = Mathf.RoundToInt(c.R * 0.11f * alpha + Random.value);
                for (int k = 0; k < outer; k++)
                {
                    float ang = Random.value * Mathf.PI * 2f;
                    var tangent = new Vector2(-Mathf.Sin(ang), Mathf.Cos(ang) * c.Squash) * c.Spin * 0.5f;
                    Mote(On(ang, c.R), tangent, Random.Range(0.55f, 0.85f));
                }
                if (Random.value < c.R * 0.05f * alpha)
                {
                    float ang = Random.value * Mathf.PI * 2f;
                    Mote(On(ang, c.R * 0.62f), new Vector2(-Mathf.Sin(ang), Mathf.Cos(ang) * c.Squash) * -c.Spin * 0.4f, Random.Range(0.45f, 0.7f));
                }
                // The runes: a stroke from the inner ring to the outer at one of its turning places.
                if (c.Spokes > 0 && Random.value < 0.35f * alpha)
                {
                    float ang = turn + Random.Range(0, c.Spokes) * Mathf.PI * 2f / c.Spokes;
                    for (int k = 0; k < 3; k++) Mote(On(ang, Mathf.Lerp(c.R * 0.62f, c.R, k / 2f)), Vector2.zero, 0.6f);
                }
                Fx?.Light(at, look.A * (0.25f * alpha), Mathf.Max(2f, c.R / 14f), true);
            }
        }

        /// <summary>
        /// The circles' rings (DrawMage): each a ring of Terraria's soft light (Extra_98 streaks end to end) in the weapon's
        /// colours, turning; an inner ring turning the other way; rune strokes between them; the motes (TickCircles) over it.
        /// </summary>
        void DrawCircles(FxBatch fx, HeroController hero)
        {
            if (circles.Count == 0) return;
            var feet = FeetPx(hero);
            float now = Time.time;
            foreach (var c in circles)
            {
                float alpha = Mathf.Clamp01((now - c.Born) / 0.15f) * Mathf.Clamp01((c.Until + c.FadeOut - now) / c.FadeOut);
                if (alpha <= 0.01f) continue;
                var at = c.Follow ? feet + c.At : c.At;
                float turn = (now - c.Born) * c.Spin;
                // It opens out from small as it comes.
                float r = c.R * (0.6f + 0.4f * Mathf.Clamp01((now - c.Born) / 0.25f));
                var outer = Color.Lerp(c.Look.A, c.Look.B, 0.3f);
                var inner = Color.Lerp(c.Look.A, c.Look.B, 0.7f);
                bool over = !c.Behind;
                Vector2 P(float ang, float rr) => at + new Vector2(Mathf.Cos(ang) * rr, Mathf.Sin(ang) * rr * c.Squash);
                int seg = Mathf.Clamp(Mathf.RoundToInt(r / 2.5f), 12, 44);
                float step = Mathf.PI * 2f / seg;
                float thick = Mathf.Clamp(r / 9f, 2.5f, 5f);
                for (int i = 0; i < seg; i++)
                {
                    float a0 = turn + i * step;
                    Streak(fx, P(a0, r), P(a0 + step, r), outer, thick, 0.5f * alpha, over);
                    Streak(fx, P(-turn * 1.3f + a0 - turn, r * 0.62f), P(-turn * 1.3f + a0 - turn + step, r * 0.62f), inner, thick * 0.6f, 0.38f * alpha, over);
                }
                for (int k = 0; k < c.Spokes; k++)
                {
                    float ang = turn + k * Mathf.PI * 2f / c.Spokes;
                    Streak(fx, P(ang, r * 0.62f), P(ang, r * 0.98f), c.Look.B, thick * 0.7f, 0.45f * alpha, over);
                    // A rune's mark at the outer ring: a short stroke across it.
                    var tangent = new Vector2(-Mathf.Sin(ang), Mathf.Cos(ang) * c.Squash) * r * 0.08f;
                    Streak(fx, P(ang, r * 1.06f) - tangent, P(ang, r * 1.06f) + tangent, c.Look.B, thick * 0.6f, 0.4f * alpha, over);
                }
            }
        }

        static MageArts.Look LookOf(ItemDef def) => MageArts.Of(def);

        /// <summary>The cast's circles as a use begins, by how late the weapon is (MageArts.Grand); kept while it is cast again.</summary>
        void MageCircles(ItemDef def)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null) return;
            int g = MageArts.Grand(def);
            var look = LookOf(def);
            float facing = TerraMod.Instance != null ? TerraMod.Instance.UseDirection : 1f;
            float life = Mathf.Max(0.45f, mageDur + 0.25f);
            if (g >= 3) OpenCircle(look, new Vector2(0f, -1f), 14f + 6f * g, 0.28f, life, 6 + g, true, 1.4f, "mage_ground");
            if (g >= 4) OpenCircle(look, new Vector2(-facing * 12f, -30f), 18f + 4f * g, 1f, life, 8, true, -1.1f, "mage_back").Behind = true;
            if (g >= 5) OpenCircle(look, new Vector2(0f, -22f), 46f, 0.42f, life, 12, true, 2.2f, "mage_halo");
        }

        /// <summary>A rune at a point (world): a small circle opening and a ring of motes (an echo's, a pulse's, a rain's start).</summary>
        public void MageRune(ItemDef def, Vector2 at)
        {
            var look = LookOf(def);
            var px = TerraParticles.ToPx(at);
            OpenCircle(look, px, 9f + 2f * MageArts.Grand(def), 1f, 0.3f, 6, false, 3f);
            for (int k = 0; k < 10; k++)
            {
                float a = k / 10f * Mathf.PI * 2f;
                int m = Dust(px, look.Dust, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(1f, 2f), look.Dust == 278 ? 40 : 90, 0.7f, true, Color.Lerp(look.A, look.B, Random.value));
                FadeIn(m, 0.6f);
            }
            DevRuneFx++;
        }

        /// <summary>A great shot (Resonance): a wide burst at the weapon's head and a circle there.</summary>
        public void MageGreat(ItemDef def)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null) return;
            var look = LookOf(def);
            float facing = TerraMod.Instance.UseDirection;
            if (!MageTip(hero, def, look, facing, out var tip, out var way)) return;
            OpenCircle(look, tip + way * 6f, 16f, 1f, 0.35f, 8, false, 4f);
            for (int k = 0; k < 20; k++)
            {
                float a = k / 20f * Mathf.PI * 2f;
                var v = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(2f, 3.5f) + way * 1.5f;
                int m = Dust(tip, look.Dust, v, look.Dust == 278 ? 40 : 90, Random.Range(0.8f, 1.1f), true, Color.Lerp(look.A, look.B, Random.value));
                FadeIn(m, 0.9f);
            }
            DevGreatFx++;
        }

        /// <summary>A nova round the player (world): a ring of motes rushing out and a flat circle under him.</summary>
        public void MageNova(ItemDef def, Vector2 at)
        {
            var look = LookOf(def);
            var px = TerraParticles.ToPx(at);
            OpenCircle(look, px + new Vector2(0f, 20f), 34f, 0.3f, 0.4f, 10, false, 3f);
            for (int k = 0; k < 28; k++)
            {
                float a = k / 28f * Mathf.PI * 2f;
                int m = Dust(px, look.Dust, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(3f, 4.5f), look.Dust == 278 ? 40 : 90, Random.Range(0.8f, 1.1f), true, Color.Lerp(look.A, look.B, Random.value));
                FadeIn(m, 0.9f);
            }
            DevNovaFx++;
        }

        /// <summary>
        /// An art cast (BladeSkills' magic arts): a great circle under the player and one where it lands (world, or none), the
        /// bigger the later the weapon, its motes rushing in to the caster.
        /// </summary>
        public void MageArt(ItemDef def, Vector2? at, float size = 1f)
        {
            var hero = HeroController.SilentInstance;
            if (hero == null) return;
            var look = LookOf(def);
            int g = MageArts.Grand(def);
            float scale = (0.8f + 0.15f * g) * size;
            OpenCircle(look, new Vector2(0f, -1f), 34f * scale, 0.3f, 0.7f, 12, true, 2.5f);
            OpenCircle(look, new Vector2(0f, -34f), 22f * scale, 1f, 0.55f, 8, true, -2f).Behind = true;
            if (at.HasValue) OpenCircle(look, TerraParticles.ToPx(at.Value), 26f * scale, 1f, 0.7f, 10, false, 2.2f);
            var feet = FeetPx(hero);
            for (int k = 0; k < 18 + 4 * g; k++)
            {
                var off = Random.insideUnitCircle.normalized * Random.Range(40f, 70f) * scale;
                var from = feet + new Vector2(0f, -24f) + off;
                int m = Dust(from, look.Dust, -off * 0.07f, look.Dust == 278 ? 40 : 90, Random.Range(0.7f, 1f), true, Color.Lerp(look.A, look.B, Random.value));
                FadeIn(m, 0.8f);
            }
            DevArtFx++;
        }
    }
}
