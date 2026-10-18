using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>Direct arena visits use the fighting Hornet, not her separate story entrance actor.</summary>
    [HarmonyPatch]
    internal static class BossEncounterCleanup
    {
        static readonly HashSet<GameObject> hidden = new HashSet<GameObject>();

        internal static bool IsStoryActor(string key, string scene, string name) => key == "Hornet@4"
            && scene == "Deepnest_East_Hornet" && name == "Hornet Outskirts Battle Encounter";

        static bool Hide(GameObject go)
        {
            if (go == null || !IsStoryActor(BossArena.VisitKey, go.scene.name, go.name)) return false;
            if (go.activeSelf)
            {
                hidden.Add(go);
                go.SetActive(false);
                Plugin.Log.LogInfo("[首领召唤物] 二见大黄蜂：关闭剧情出场对象，保留战斗本体");
            }
            return true;
        }

        // Catch the story FSM before its first state's actions can spawn dialogue or seize control.
        // The visit is registered before loading the scene, including its additive boss scene.
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HutongGames.PlayMaker.FsmState), nameof(HutongGames.PlayMaker.FsmState.OnEnter))]
        static bool BeforeStoryState(HutongGames.PlayMaker.FsmState __instance) => !Hide(__instance.Fsm?.GameObject);

        internal static void HideStoryActors()
        {
            if (BossArena.VisitKey != "Hornet@4") return;
            foreach (var fsm in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None)) Hide(fsm.gameObject);
        }

        internal static void RestoreStoryActors()
        {
            // Called after the visit and its temporary flags have been restored. This also
            // handles seamless rooms still loaded when the player leaves the arena.
            foreach (var go in hidden) if (go != null) go.SetActive(true);
            hidden.Clear();
        }
    }
}
