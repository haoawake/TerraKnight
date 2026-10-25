using HarmonyLib;
using HutongGames.PlayMaker;

namespace TerraKnight.UI
{
    /// <summary>
    /// The full maps take the Knight's control while open and give it back on closing (WorldMap, FullMap). A boss's roar
    /// begun with a map open was given back with it, the roar slipped (the user's, 2026-10-20: "部分boss仍然可以提前打开
    /// 地图来卡掉战吼"). A roar (the hero's ROAR ENTER) closes the map before the roar takes hold; any other hold the game
    /// takes with the map open (a boss's own RelinquishControl) is left to its taker, the map's closing giving nothing back.
    /// </summary>
    [HarmonyPatch]
    internal static class MapHold
    {
        static bool active, opening, takenMeanwhile;
        public static int DevKept, DevRoarClosed;

        /// <summary>A map opening: the Knight's control taken for it.</summary>
        public static void Take(HeroController hero)
        {
            active = true;
            takenMeanwhile = false;
            opening = true;
            try { hero.RelinquishControl(); }
            finally { opening = false; }
        }

        /// <summary>A map closing: the control given back, unless the game took it meanwhile.</summary>
        public static void Give(HeroController hero)
        {
            bool kept = active && takenMeanwhile;
            active = false;
            takenMeanwhile = false;
            if (hero == null) return;
            if (kept)
            {
                DevKept++;
                Plugin.Log.LogInfo("[地图] 地图开着时游戏收走了主角的控制（首领的吼叫等）：关地图不还，等它自己放开");
                return;
            }
            hero.RegainControl();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.RelinquishControl))]
        static void Taken()
        {
            if (active && !opening) takenMeanwhile = true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.RelinquishControlNotVelocity))]
        static void TakenKeepingVelocity()
        {
            if (active && !opening) takenMeanwhile = true;
        }

        /// <summary>A roar reaching the Knight with a map open: the map shut first, so the roar holds him as it should.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(Fsm), nameof(Fsm.ProcessEvent))]
        static void RoarCloses(Fsm __instance, FsmEvent fsmEvent)
        {
            if (!active || fsmEvent == null || fsmEvent.Name != "ROAR ENTER") return;
            var hero = HeroController.SilentInstance;
            if (hero == null || __instance.GameObject != hero.gameObject) return;
            DevRoarClosed++;
            Plugin.Log.LogInfo("[地图] 首领吼叫时地图开着：先关地图");
            WorldMap.Close();
            FullMap.Close();
        }
    }
}
