using HarmonyLib;

namespace TerraKnight.Items
{
    /// <summary>
    /// Hallownest's own shops (Sly, Iselda, Salubra, the Leg Eater: ShopItemStats) ask five times the game's prices (the
    /// user's): geo comes in as Terraria's money here, from every drop sold. What Lemm pays for relics (relic entries)
    /// stays as it is. ShopItemStats.Awake reads its price from the language sheet ("Prices") for each item it builds, so
    /// the multiple never compounds. The prompts that ask geo (tolls, stags, the Nailsmith...) are raised too (RaiseToll).
    /// </summary>
    [HarmonyPatch]
    internal static class HkShopPrices
    {
        public const int Factor = 5;
        public static int DevRaised, DevTollsRaised;
        public static int DevLastToll;
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<HutongGames.PlayMaker.FsmInt, TollQuote> tolls
            = new System.Runtime.CompilerServices.ConditionalWeakTable<HutongGames.PlayMaker.FsmInt, TollQuote>();
        sealed class TollQuote { public int Price; public UnityEngine.GameObject Requester; }

        // Every fresh base-price assignment starts a quote, including the same numeric value.
        [HarmonyPrefix, HarmonyPatch(typeof(HutongGames.PlayMaker.FsmInt), "set_Value")]
        static void FreshToll(HutongGames.PlayMaker.FsmInt __instance)
        { if (__instance.Name == "Toll Cost") tolls.Remove(__instance); }

        /// <summary>
        /// Every yes/no prompt that asks geo asks five times it too: the toll gates and the toll benches, the stag stations,
        /// the Nailsmith, the Leg Eater's mending, Divine, Cornifer, the Colosseum's trials. They all go through the dialogue's
        /// "Text YN" (its Dialogue Page Control's "Toll Cost": shown, checked against the geo, taken by HeroController.TakeGeo),
        /// so it is raised as the prompt shows it (its ConvertIntToString), once a prompt. The bank's deposit stays as it is.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HutongGames.PlayMaker.Actions.ConvertIntToString), "OnEnter")]
        static void RaiseToll(HutongGames.PlayMaker.Actions.ConvertIntToString __instance)
        {
            var v = __instance.intVariable;
            if (v == null || v.Name != "Toll Cost" || v.Value <= 0) return;
            var fsm = __instance.Fsm;
            if (fsm == null || fsm.Name != "Dialogue Page Control" || fsm.GameObject == null || fsm.GameObject.name != "Text YN") return;
            var requester = fsm.Variables.FindFsmGameObject("Requester");
            if (requester != null && requester.Value != null && requester.Value.name.Contains("Banker")) return;
            var source = requester?.Value;
            if (tolls.TryGetValue(v, out var quote) && quote.Price == v.Value && quote.Requester == source) return;
            int vanilla = v.Value;
            v.Value = vanilla * Factor;
            tolls.Remove(v);
            tolls.Add(v, new TollQuote { Price = v.Value, Requester = source });
            DevTollsRaised++;
            DevLastToll = v.Value;
            Plugin.Log.LogInfo($"[商店] 吉欧确认框：{vanilla} → {v.Value}（{(requester != null && requester.Value != null ? requester.Value.name : "?")}）");
        }

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
