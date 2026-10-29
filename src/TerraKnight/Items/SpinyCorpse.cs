using HarmonyLib;

namespace TerraKnight.Items
{
    /// <summary>
    /// A Spiny Husk is done once it is dead (the user's 10-28 "多刺躯壳死了还在放刺"): its corpse (CorpseSpineBurst) lies where
    /// it lands as any other. The game's had it shake a second after landing and burst its spines (Spine Hit, a DamageHero
    /// ring left on for good, its spines drawn out, three sounds), a dead husk still hurting whoever came near it.
    /// </summary>
    [HarmonyPatch(typeof(CorpseSpineBurst), "LandEffects")]
    internal static class SpinyCorpse
    {
        public static int DevQuiet;

        [HarmonyPrefix]
        static bool Quiet()
        {
            DevQuiet++;
            return false;
        }
    }
}
