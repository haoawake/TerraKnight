using System;
using HarmonyLib;

namespace TerraKnight.World
{
    /// <summary>
    /// Native dream-gate travel ignores only the conditional loader's walking-door test.
    /// Pylon arrivals follow that same rule, including the bench/respawn path. All native
    /// progression, persistent victory and alternative-scene decisions stay in OnEnable.
    /// Scope the exemption to the destination while its portal arrival is pending; never
    /// change normal walking or an adjacent scene's background load.
    /// </summary>
    [HarmonyPatch(typeof(SceneAdditiveLoadConditional), "OnEnable")]
    internal static class PylonSceneLoad
    {
        internal static int DevExemptions;
        [HarmonyPrefix]
        static void Prefix(SceneAdditiveLoadConditional __instance, out string __state)
        {
            __state = null;
            if (__instance == null || !Pylons.LoadingByPortal(__instance.gameObject.scene.name) || string.IsNullOrEmpty(__instance.doorTrigger)) return;
            __state = __instance.doorTrigger;
            __instance.doorTrigger = "";
            DevExemptions++;
            Plugin.Log.LogInfo($"[晶塔] {__instance.gameObject.scene.name} 条件载入 {__instance.sceneNameToLoad}：按原生传送规则忽略步行入口 {__state}，保留剧情与胜利条件");
        }
        [HarmonyFinalizer]
        static Exception Finalizer(SceneAdditiveLoadConditional __instance, string __state, Exception __exception)
        {
            if (__state != null && __instance != null) __instance.doorTrigger = __state;
            return __exception;
        }
    }
    [HarmonyPatch(typeof(HeroController), "FinishedEnteringScene")]
    internal static class PylonArrivalAfterEntry
    {
        [HarmonyPostfix]
        static void Postfix(HeroController __instance) => Pylons.Arrive(__instance, true);
    }
}
