using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// A boss's roar holds the Knight until the boss's FSM lets go at the roar's end: either through
    /// the hero's "Roar Lock" FSM (ROAR ENTER with the boss as its "Roar Object", then ROAR EXIT) or by
    /// taking control itself (RelinquishControl, then RegainControl: the Fluke Mother). Killed during
    /// it (a minion's hits, a shot already flying, a burn: nothing a nail could do), the boss's FSM
    /// goes to its death and the letting go never comes, and the Knight would stand there for good.
    /// Once the enemy that took hold has been dead a few seconds with the Knight still held, he is let
    /// go as its roar's end would. Only a hold taken while it was alive counts: a death's own scene
    /// (taken after the kill) runs as the game has it.
    /// Likewise a hold whose taker is gone altogether: a scene's FSM that took control just as the
    /// Knight left the scene (Hornet's words after the Greenpath fight, with a pylon's teleport or a
    /// mirror's recall at that moment) is unloaded with it and can never give it back.
    /// </summary>
    [HarmonyPatch]
    internal static class RoarRelease
    {
        public static int DevReleased;
        /// <summary>Dev: every taking and giving back of control logged, with the FSM doing it.</summary>
        public static bool DevTrace;
        static PlayMakerFSM lockFsm;
        /// <summary>The live enemy whose FSM took the Knight's control, until it is given back.</summary>
        static GameObject holder;
        static string holderState = "";
        /// <summary>Whatever FSM's object asked for control last (an enemy or not, held already or not), until it is given back.</summary>
        static GameObject taker;
        static string takerState = "";
        static float goneSince = -1f;
        static float deadSince = -1f;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.RelinquishControl))]
        static void Relinquishing(HeroController __instance, out bool __state) => __state = __instance.controlReqlinquished;

        /// <summary>Who took it: a live enemy's FSM (a second taking while held only replaces it with another enemy).</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.RelinquishControl))]
        static void Relinquished(HeroController __instance, bool __state)
        {
            if (!__instance.controlReqlinquished) return;
            var fsm = HutongGames.PlayMaker.FsmExecutionStack.ExecutingFsm;
            if (DevTrace) Plugin.Log.LogInfo($"[控制] 交出（之前已交出 {__state}）：{(fsm != null ? $"{fsm.GameObject?.name}/{fsm.Name}:{fsm.ActiveStateName}" : "不是状态机")}");
            var go = fsm != null ? fsm.GameObject : null;
            if (go != null)
            {
                taker = go;
                takerState = $"{go.scene.name}/{go.name}/{fsm.Name}:{fsm.ActiveStateName}";
            }
            var hm = HealthOf(go);
            bool enemy = hm != null && !hm.isDead && !EnemyStats.Defeated(hm);
            if (__state && !enemy) return;
            holder = enemy ? go : null;
            holderState = enemy ? $"{fsm.Name}:{fsm.ActiveStateName}" : "";
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.RegainControl))]
        static void Regained()
        {
            if (DevTrace)
            {
                var fsm = HutongGames.PlayMaker.FsmExecutionStack.ExecutingFsm;
                Plugin.Log.LogInfo($"[控制] 收回：{(fsm != null ? $"{fsm.GameObject?.name}/{fsm.Name}:{fsm.ActiveStateName}" : "不是状态机")}");
            }
            holder = null;
            taker = null;
        }

        public static void Tick(HeroController hero)
        {
            if (hero == null || GameHooks.PlayerHasControl()) { deadSince = -1f; goneSince = -1f; return; }
            // A cutscene that shows the Knight by a stand-in of its own (the Radiance's end, the Beast's Den web) holds him
            // on purpose, its boss's death included: it lets go by itself.
            if (Player.CutsceneAttack.KnightHidden()) { deadSince = -1f; goneSince = -1f; return; }
            // The taker destroyed (its scene unloaded) with the Knight still held, out of any transition.
            var gm = GameManager.UnsafeInstance;
            bool gone = taker is object && taker == null && hero.controlReqlinquished && gm != null && !gm.IsInSceneTransition
                && !hero.cState.transitioning && gm.gameState == GlobalEnums.GameState.PLAYING;
            if (!gone) goneSince = -1f;
            else if (goneSince < 0f) goneSince = Time.time;
            else if (Time.time - goneSince > 2f)
            {
                goneSince = -1f;
                DevReleased++;
                Plugin.Log.LogInfo($"[控制] {takerState} 收走了主角的控制，自己却已经不在了（场景已卸载）：还给主角");
                taker = null;
                hero.RegainControl();
                hero.StartAnimationControl();
                return;
            }
            bool byFsm = holder is object && hero.controlReqlinquished && Dead(holder);
            PlayMakerFSM held = null;
            if (!byFsm)
            {
                if (lockFsm == null || lockFsm.gameObject != hero.gameObject) lockFsm = PlayMakerFSM.FindFsmOnGameObject(hero.gameObject, "Roar Lock");
                // Held: past its waiting states (Init, then Detect: listening for ROAR ENTER).
                string ls = lockFsm != null ? lockFsm.ActiveStateName : null;
                var roarer = ls != null && ls != lockFsm.Fsm.StartState && ls != "Detect" && ls != "Init" ? lockFsm.FsmVariables.FindFsmGameObject("Roar Object") : null;
                if (roarer != null && Dead(roarer.Value)) held = lockFsm;
            }
            if (!byFsm && held == null) { deadSince = -1f; return; }
            if (deadSince < 0f) deadSince = Time.time;
            if (Time.time - deadSince < 4f) return;
            deadSince = -1f;
            DevReleased++;
            if (byFsm)
            {
                Plugin.Log.LogInfo($"[首领] {(holder != null ? holder.name : "（已销毁）")} 在 {holderState} 定住主角后死了：放开");
                holder = null;
                hero.RegainControl();
                hero.StartAnimationControl();
            }
            else
            {
                Plugin.Log.LogInfo($"[首领] 吼叫的首领已经死了，主角还被吼叫定住（{held.ActiveStateName}）：放开");
                FSMUtility.SendEventToGameObject(hero.gameObject, "ROAR EXIT");
            }
        }

        static HealthManager HealthOf(GameObject go) =>
            go == null ? null : go.GetComponentInParent<HealthManager>() ?? go.GetComponentInChildren<HealthManager>(true);

        static bool Dead(GameObject go)
        {
            if ((object)go == null) return false; // nothing set: not a boss's hold
            if (go == null) return true; // destroyed with its death
            var hm = HealthOf(go);
            return hm != null && (hm.isDead || EnemyStats.Defeated(hm));
        }
    }
}
