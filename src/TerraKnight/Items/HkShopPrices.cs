using HarmonyLib;

namespace TerraKnight.Items
{
    /// <summary>
    /// Hallownest's own shops (Sly, Iselda, Salubra, the Leg Eater: ShopItemStats) ask five times the game's prices (the
    /// user's): geo comes in as Terraria's money here, from every drop sold. What Lemm pays for relics (relic entries)
    /// stays as it is. ShopItemStats.Awake reads its price from the language sheet ("Prices") for each item it builds, so
    /// the multiple never compounds.
    /// </summary>
    [HarmonyPatch]
    internal static class HkShopPrices
    {
        public const int Factor = 5;
        public static int DevRaised;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(ShopItemStats), "Awake")]
        static void Raise(ShopItemStats __instance)
        {
            if (__instance == null || __instance.relic || __instance.cost <= 0) return;
            __instance.cost *= Factor;
            DevRaised++;
        }
    }
}
