using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Fury of the Fallen (charm 6): at 35% of the life or below, every class's damage +25%, a faint crimson about the player
    /// and an ember rising now and then. Its skill (the user's, 2026-10-22): 30% of the most life is given up (never the
    /// last of it, so at one life it costs nothing and goes on), and for two seconds everything the player deals is doubled,
    /// a ring of fire bursting out round the player as it starts. Ten seconds before it comes again.
    /// </summary>
    internal static partial class CharmEffects
    {
        const float FuryLine = 0.35f, FuryBonus = 0.25f, FrenzyTime = 2f, FuryCooldown = 10f, FuryCost = 0.3f;
        static readonly Color FuryColor = new Color(0.95f, 0.16f, 0.12f);
        static readonly Color FuryFlame = new Color(1f, 0.55f, 0.12f), FuryCore = new Color(1f, 0.9f, 0.55f);
        static float frenzyUntil = -1f, frenzyAt = -10f, emberAt;
        public static int DevFrenzies, DevFrenzyCost;

        /// <summary>At the life's low line with Fury worn.</summary>
        static bool Furious
        {
            get
            {
                var st = Stats;
                return On(Fury) && st != null && st.Life > 0 && st.Life <= st.LifeMax * FuryLine;
            }
        }

        static bool Frenzied => On(Fury) && Time.time < frenzyUntil;

        /// <summary>Each frame's charm bonuses (Equipment.Update): Fury's +25% to every class at the low line.</summary>
        public static Bonuses DynamicBonuses()
        {
            var b = new Bonuses();
            if (Furious) { b.MeleeDamage = FuryBonus; b.RangedDamage = FuryBonus; b.MagicDamage = FuryBonus; b.MinionDamage = FuryBonus; }
            AddNailBonuses(ref b);
            AddClassBonuses(ref b);
            return b;
        }

        /// <summary>Every hit the player deals (MeleeHits.Deal): doubled in the frenzy's two seconds.</summary>
        public static int ScaleOutgoing(int damage) => Frenzied ? damage * 2 : damage;

        static void Frenzy(HeroController hero)
        {
            var st = Stats;
            if (st == null || st.Life <= 0) return;
            int cost = Mathf.RoundToInt(st.LifeMax * FuryCost);
            int left = Mathf.Max(1, st.Life - cost);
            cost = st.Life - left;
            st.Life = left;
            DevFrenzyCost = cost;
            frenzyUntil = Time.time + FrenzyTime;
            frenzyAt = Time.time;
            lifeLostAt = Time.time;
            DevFrenzies++;
            CharmSkills.Active(Fury, Charms.Name(Fury), Lang.T("造成的伤害翻倍"), FrenzyTime);
            CharmSkills.Cool(Fury, FuryCooldown);
            if (cost > 0) UI.CombatText.Show(hero.transform.position + Vector3.up * 0.9f, cost.ToString(), new Color(0.7f, 0.08f, 0.1f));
            Mod.PlaySound("NPC_Hit_1", 0.5f);
            Mod.PlaySound("Item_74", 0.6f);
            FlameBurst(Centre(hero));
        }

        /// <summary>
        /// The skill's burst: three rings of fire thrown outward at different speeds (torch fire, the solar flame, the
        /// inferno's sparks) and embers rising after them; the light (a flash, two widening rings) is DrawFury's.
        /// </summary>
        static void FlameBurst(Vector2 centre)
        {
            var p = TerraParticles.Instance;
            if (p == null) return;
            var px = TerraParticles.ToPx(centre);
            FlameRing(p, px, 40, 6, 6f, 8f, 1.8f, 2.4f);
            FlameRing(p, px, 28, 259, 3.5f, 5f, 1.4f, 1.8f);
            FlameRing(p, px, 20, 174, 9f, 11f, 1.1f, 1.4f);
            for (int k = 0; k < 14; k++)
            {
                int d = p.NewDust(px - new Vector2(14f, 4f), 28, 16, 6, Random.Range(-1.5f, 1.5f), Random.Range(-3.5f, -1.5f), 0, Random.Range(1.2f, 1.8f));
                if (d >= 0) { ref var dust = ref p[d]; dust.NoGravity = true; }
            }
        }

        /// <summary>A ring of dusts thrown out evenly round a point (px), each a little off its place and speed.</summary>
        static void FlameRing(TerraParticles p, Vector2 px, int count, int type, float speedMin, float speedMax, float scaleMin, float scaleMax)
        {
            float turn = Random.Range(0f, Mathf.PI * 2f);
            for (int k = 0; k < count; k++)
            {
                float a = turn + k / (float)count * Mathf.PI * 2f + Random.Range(-0.08f, 0.08f);
                float v = Random.Range(speedMin, speedMax);
                int d = p.NewDust(px - new Vector2(3f, 3f), 6, 6, type, Mathf.Cos(a) * v, Mathf.Sin(a) * v, 0, Random.Range(scaleMin, scaleMax));
                if (d >= 0) { ref var dust = ref p[d]; dust.NoGravity = true; dust.Velocity = new Vector2(Mathf.Cos(a) * v, Mathf.Sin(a) * v); }
            }
        }

        static void TickFury(HeroController hero)
        {
            if (!On(Fury) || hero == null || hero.cState.dead) { frenzyUntil = -1f; CharmSkills.EndActive(Fury); return; }
            // An ember now and then while furious (more in the frenzy): slow, rising, gone in a moment.
            if ((Furious || Frenzied) && Playing(hero) && Time.time >= emberAt)
            {
                emberAt = Time.time + (Frenzied ? 0.06f : 0.35f);
                var p = TerraParticles.Instance;
                if (p != null)
                {
                    var box = hero.GetComponent<BoxCollider2D>().bounds;
                    var tl = TerraParticles.ToPx(new Vector2(box.min.x, box.max.y));
                    int d = p.NewDust(tl, Mathf.RoundToInt(box.size.x / TerraParticles.Px), Mathf.RoundToInt(box.size.y / TerraParticles.Px), 6, 0f, -1.5f, 0, 1.2f);
                    if (d >= 0) { ref var dust = ref p[d]; dust.NoGravity = true; dust.Velocity = new Vector2(dust.Velocity.x * 0.2f, -1.1f); }
                }
            }
        }

        static void DrawFury(FxBatch fx, Vector2 c, float opacity)
        {
            if (!On(Fury)) return;
            float t = Time.time;
            float frenzy = Frenzied ? 1f : 0f;
            float since = t - frenzyAt;
            float burst = Mathf.Clamp01(1f - since / 0.55f);
            if (!Furious && frenzy <= 0f && burst <= 0f) return;
            // The low line: a faint crimson breathing behind the player; the frenzy: stronger, over it too.
            fx.OnTop = false;
            float breathe = 0.5f + 0.5f * Mathf.Sin(t * 2.4f);
            float k = (Furious ? 0.38f + 0.17f * breathe : 0f) + 0.3f * frenzy;
            fx.Draw(Dot, c, Glow(FuryColor, k * opacity), 0f, 2.4f, overPlayer: false);
            fx.Draw(Dot, c + new Vector2(0f, 10f), Glow(FuryColor, 0.6f * k * opacity), 0f, new Vector2(1.6f, 0.7f), overPlayer: false);
            if (frenzy > 0f || burst > 0f)
            {
                fx.OnTop = true;
                if (burst > 0f)
                {
                    // The burst's light: a white-gold flash at the heart, an orange ring racing out to 110 px and a crimson one
                    // behind it to 70, both fading as they go.
                    float flash = Mathf.Clamp01(1f - since / 0.25f);
                    if (flash > 0f) fx.Draw(Dot, c, Glow(FuryCore, flash * opacity), 0f, Mathf.Lerp(1.2f, 4.2f, 1f - flash), overPlayer: true);
                    float outer = 1f - burst;
                    fx.Draw(Ring, c, Glow(FuryFlame, burst * opacity), 0f, Mathf.Lerp(24f, 220f, 1f - (1f - outer) * (1f - outer)) / 64f, overPlayer: true);
                    float inner = Mathf.Clamp01(since / 0.45f);
                    if (inner < 1f) fx.Draw(Ring, c, Glow(FuryColor, 0.9f * (1f - inner) * opacity), 0f, Mathf.Lerp(16f, 140f, inner) / 64f, overPlayer: true);
                }
                fx.Draw(Ring, c, Glow(FuryColor, (0.25f + 0.15f * Mathf.Sin(t * 20f)) * frenzy * opacity), 0f, 52f / 64f, overPlayer: true);
            }
        }
    }
}
