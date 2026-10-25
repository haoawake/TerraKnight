using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Quick Focus (charm 7, two notches here): potion sickness 15% shorter (with the Philosopher's Stone's family 30% in all),
    /// and the natural regeneration starting a quarter sooner after a hit (3 s, not 4). Its look is the focus it was: while the
    /// life comes back of itself, motes of pale soul drift in to the player; a potion drunk draws a ring in.
    /// </summary>
    internal static partial class CharmEffects
    {
        const float QuickSickness = 0.85f, QuickWithStone = 0.7f, StoneSickness = 0.75f, QuickRegenStart = 3f, PlainRegenStart = 4f;
        static readonly Color FocusColor = new Color(0.72f, 0.95f, 1f);
        static float drinkAt = -10f, moteAt;

        /// <summary>Motes drifting in: where each starts (offset, Terraria px) and when.</summary>
        static readonly Vector2[] moteFrom = new Vector2[6];
        static readonly float[] moteStart = { -10f, -10f, -10f, -10f, -10f, -10f };
        static int moteNext;

        /// <summary>The potion sickness's length, by the shorteners worn (the Philosopher's Stone's family, Quick Focus).</summary>
        public static float SicknessFactor(bool stone)
        {
            bool quick = On(QuickFocus);
            return quick ? (stone ? QuickWithStone : QuickSickness) : stone ? StoneSickness : 1f;
        }

        /// <summary>Seconds after a hit the natural regeneration starts.</summary>
        public static float RegenStart => On(QuickFocus) ? QuickRegenStart : PlainRegenStart;

        /// <summary>A potion with sickness drunk (TerraMod.Drink).</summary>
        public static void OnDrink(ItemDef def)
        {
            if (On(QuickFocus)) drinkAt = Time.time;
        }

        static void TickQuickFocus(HeroController hero)
        {
            var st = Stats;
            if (!On(QuickFocus) || st == null || !st.RegeneratingNaturally || !Playing(hero)) return;
            if (Time.time < moteAt) return;
            moteAt = Time.time + 0.28f;
            float a = Random.Range(0f, Mathf.PI * 2f);
            moteFrom[moteNext] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Random.Range(30f, 40f);
            moteStart[moteNext] = Time.time;
            moteNext = (moteNext + 1) % moteFrom.Length;
        }

        static void DrawQuickFocus(FxBatch fx, Vector2 c, float opacity)
        {
            if (!On(QuickFocus)) return;
            float t = Time.time;
            fx.OnTop = true;
            for (int i = 0; i < moteFrom.Length; i++)
            {
                float k = (t - moteStart[i]) / 0.55f;
                if (k < 0f || k >= 1f) continue;
                // In along a slight curve, faster at the end, bright in the middle of the way.
                float ease = k * k;
                var side = new Vector2(-moteFrom[i].y, moteFrom[i].x) * 0.25f * Mathf.Sin(k * Mathf.PI);
                var at = c + moteFrom[i] * (1f - ease) + side;
                float glow = Mathf.Sin(k * Mathf.PI) * opacity;
                fx.Draw(Dot, at, Glow(FocusColor, 0.9f * glow), 0f, 0.26f, overPlayer: true);
                fx.Draw(Dot, at, Glow(Color.white, 0.7f * glow), 0f, 0.1f, overPlayer: true);
            }
            float drink = (t - drinkAt) / 0.45f;
            if (drink >= 0f && drink < 1f)
            {
                float r = Mathf.Lerp(44f, 8f, drink * drink);
                fx.Draw(Ring, c, Glow(FocusColor, 0.8f * (1f - drink * 0.6f) * opacity), 0f, r * 2f / 64f, overPlayer: true);
            }
        }
    }
}
