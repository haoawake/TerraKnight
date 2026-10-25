using HarmonyLib;
using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Lifeblood Heart (charm 8) and Lifeblood Core (charm 9), the charm bag's lifeblood (the game's own at a bench is theirs no
    /// more: PlayerData.UpdateBlueHealth gives none with the bag on).
    ///   The Heart's skill: a shield of a quarter of the life (not stacked: used again it only fills up). While it holds, a hit
    ///   can at most break it: a smaller hit wears it down, the one that breaks it goes no further, with a sound and its
    ///   pieces flying. A minute before it comes again.
    ///   The Core: five seconds without losing life, a layer of a tenth of the life, three at most, again every five seconds.
    /// Both worn, they stack (the Core's layers outside, the Heart's within). The shield shows round the player as rings of
    /// lifeblood, one for each layer (the Heart's the brightest); the life's silver hearts count them too.
    /// </summary>
    internal static partial class CharmEffects
    {
        const float HeartShare = 0.25f, HeartCooldown = 60f, CoreShare = 0.1f, CoreQuiet = 5f;
        const int CoreLayers = 3;
        static readonly Color BloodColor = new Color(0.45f, 0.85f, 1f);
        static int heartShield, coreShield;
        static float heartBreakAt = -10f, heartGotAt = -10f, coreGotAt = -10f, coreLayerAt;
        static Vector2 breakAt;
        public static int DevHeartBreaks, DevCoreLayersGot;

        static int CoreLayer => Stats != null ? Mathf.Max(1, Mathf.CeilToInt(Stats.LifeMax * CoreShare)) : 10;
        static int CoreCount => coreShield <= 0 ? 0 : Mathf.CeilToInt(coreShield / (float)CoreLayer);

        /// <summary>The charms' shields now (the life's silver hearts show them with lifeblood's).</summary>
        public static int ShieldTotal => heartShield + coreShield;
        public static int DevHeart => heartShield;
        public static int DevCore => coreShield;
        public static int DevCoreCount => CoreCount;

        /// <summary>What of a hit (after the shell and the guard) gets past the charms' shields: the Core's layers, then the Heart's.</summary>
        public static int Shields(int damage)
        {
            if (damage <= 0) return damage;
            if (coreShield > 0)
            {
                int held = Mathf.Min(coreShield, damage);
                coreShield -= held;
                damage -= held;
            }
            if (damage > 0 && heartShield > 0)
            {
                if (damage >= heartShield) BreakHeart();
                else heartShield -= damage;
                // The Heart held: nothing of this hit goes further.
                return 0;
            }
            return damage;
        }

        static void GiveHeart(HeroController hero)
        {
            var st = Stats;
            if (st == null) return;
            heartShield = Mathf.Max(heartShield, Mathf.CeilToInt(st.LifeMax * HeartShare));
            heartGotAt = Time.time;
            CharmSkills.Cool(LifebloodHeart, HeartCooldown);
            Mod.PlaySound("Item_29", 0.5f);
        }

        static void BreakHeart()
        {
            heartShield = 0;
            heartBreakAt = Time.time;
            DevHeartBreaks++;
            var hero = HeroController.SilentInstance;
            if (hero == null) return;
            breakAt = Centre(hero);
            Mod.PlaySound("Shatter", 0.7f);
            var p = TerraParticles.Instance;
            if (p == null) return;
            var px = TerraParticles.ToPx(breakAt);
            for (int k = 0; k < 16; k++)
            {
                float a = k / 16f * Mathf.PI * 2f + Random.Range(-0.15f, 0.15f);
                int d = p.NewDust(px + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 26f - new Vector2(2f, 2f), 4, 4, 180, Mathf.Cos(a) * 3f, Mathf.Sin(a) * 3f, 0, 1.2f);
                if (d >= 0) { ref var dust = ref p[d]; dust.NoGravity = true; }
            }
        }

        static void TickLifeblood(HeroController hero)
        {
            var st = Stats;
            // Taken off, or dead: its shield gone.
            if (!On(LifebloodHeart) || hero == null || hero.cState.dead) heartShield = 0;
            if (!On(LifebloodCore) || hero == null || hero.cState.dead) { coreShield = 0; return; }
            if (st == null || !Playing(hero)) return;
            if (coreShield >= CoreLayers * CoreLayer) { coreLayerAt = Time.time; return; }
            // Five quiet seconds (no life lost) since the last loss and the last layer: one more.
            if (Time.time - lifeLostAt < CoreQuiet || Time.time - coreLayerAt < CoreQuiet) return;
            coreShield = Mathf.Min(CoreLayers * CoreLayer, coreShield + CoreLayer);
            coreLayerAt = coreGotAt = Time.time;
            DevCoreLayersGot++;
        }

        static void DrawLifeblood(FxBatch fx, Vector2 c, float opacity)
        {
            float t = Time.time;
            float broke = Mathf.Clamp01(1f - (t - heartBreakAt) / 0.45f);
            int core = CoreCount;
            bool heart = heartShield > 0;
            if (!heart && core == 0 && broke <= 0f) return;
            fx.OnTop = true;
            int layers = core + (heart ? 1 : 0);
            // A faint body of lifeblood, a little more for each layer.
            if (layers > 0)
            {
                float body = (0.04f + 0.035f * layers) * opacity;
                fx.Draw(Dot, c, new Color(BloodColor.r * body, BloodColor.g * body, BloodColor.b * body, body), 0f, 2.2f, overPlayer: true);
            }
            float shimmer = 0.85f + 0.15f * Mathf.Sin(t * 3f);
            // The Heart's ring, the brightest, nearest; the Core's layers round it, the newest swelling in.
            if (heart)
            {
                float got = Mathf.Clamp01((t - heartGotAt) / 0.35f);
                float r = Mathf.Lerp(46f, 30f, got);
                fx.Draw(Ring, c, Glow(BloodColor, 0.8f * shimmer * opacity), t * 0.3f, r * 2f / 64f, overPlayer: true);
            }
            for (int i = 0; i < core; i++)
            {
                float swell = i == core - 1 ? Mathf.Clamp01((t - coreGotAt) / 0.4f) : 1f;
                float r = 37f + i * 4.5f + (1f - swell) * 8f;
                fx.Draw(Ring, c, Glow(BloodColor, (0.35f + 0.1f * i) * swell * shimmer * opacity), -t * 0.2f, r * 2f / 64f, overPlayer: true);
            }
            if (broke > 0f)
            {
                var at = TerraParticles.ToPx(breakAt);
                float r = Mathf.Lerp(30f, 60f, 1f - broke);
                fx.Draw(Ring, at, Glow(Color.white, 0.9f * broke * broke), 0f, r * 2f / 64f, overPlayer: true);
            }
        }

        /// <summary>The charm bag on: the Lifeblood charms give their shields through the bag, none of the game's blue masks.</summary>
        [HarmonyPatch(typeof(PlayerData), nameof(PlayerData.UpdateBlueHealth))]
        static class NoCharmBlueHealth
        {
            static void Postfix(PlayerData __instance)
            {
                if (Charms.Enabled) __instance.healthBlue = 0;
            }
        }
    }
}
