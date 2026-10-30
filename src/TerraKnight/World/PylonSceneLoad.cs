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
    /// <summary>
    /// A portal arrival goes in by its gate as by a door (the user's 10-29 "传送动画变长了吗？有人说有点卡卡的"): the game's
    /// walk in from a side, jump up from below or drop from above (HeroController.EnterScene) came first, the room faded in
    /// on it, then the player was put by the pylon, the view jumping after him. A door's way in moves nobody, so he is put by
    /// the pylon as he is placed (Pylons.Arrive at SendHeroInPosition) and the room fades in on him there. Only the arriving
    /// room's gates while that arrival is pending; the conditional loads above keep their rule.
    /// </summary>
    [HarmonyPatch(typeof(TransitionPoint), nameof(TransitionPoint.GetGatePosition))]
    internal static class PylonDoorEntry
    {
        internal static int DevDoorEntries;
        /// <summary>Dev (suite pylonspeed): the game's own way in by the gate, as before, to compare.</summary>
        internal static bool DevOff;
        [HarmonyPrefix]
        static bool Prefix(TransitionPoint __instance, ref GlobalEnums.GatePosition __result)
        {
            if (DevOff || __instance == null || !Pylons.LoadingByPortal(__instance.gameObject.scene.name)) return true;
            __result = GlobalEnums.GatePosition.door;
            DevDoorEntries++;
            return false;
        }
    }

    [HarmonyPatch(typeof(HeroController), "FinishedEnteringScene")]
    internal static class PylonArrivalAfterEntry
    {
        [HarmonyPostfix]
        static void Postfix(HeroController __instance) => Pylons.Arrive(__instance, true);
    }
}
