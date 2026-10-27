using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TerraKnight.UI
{
    /// <summary>
    /// The game's own menus out in the world: a Hall of Gods statue's challenge (BossChallengeUI) and a Pantheon door's
    /// bindings (BossDoorChallengeUI). Made for a pad, they come with their GraphicRaycaster off, so a click on a tier or a
    /// binding reached nothing (players' 10-26 "神居手办屋点开雕像按不了挑战键也按不了退出"). While one is up its raycasters
    /// are on (MenuButton takes a click as its submit), a right click is its cancel, and the cancel key its prompt shows
    /// (the game's cast, U) answers: Player.TerraControls keeps the cast off in play only. Esc is its cancel (no Terraria
    /// inventory opens over it) and Enter its confirm (no chat opens over it).
    /// </summary>
    [HarmonyPatch]
    internal static class HkMenus
    {
        static Component current;
        static UnityEngine.UI.GraphicRaycaster[] raycasters;

        /// <summary>One of those menus is up.</summary>
        public static bool Open => current != null && current.gameObject.activeInHierarchy;

        /// <summary>Dev: menus seen, right clicks taken as their cancel.</summary>
        public static int DevShown, DevRightCancels;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(BossChallengeUI), nameof(BossChallengeUI.Setup))]
        static void StatueShown(BossChallengeUI __instance) => Shown(__instance);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(BossDoorChallengeUI), nameof(BossDoorChallengeUI.Show))]
        static void DoorShown(BossDoorChallengeUI __instance) => Shown(__instance);

        static void Shown(Component ui)
        {
            if (ui == null) return;
            current = ui;
            raycasters = ui.GetComponentsInChildren<UnityEngine.UI.GraphicRaycaster>(true);
            DevShown++;
            Tick();
        }

        /// <summary>Each frame: its raycasters kept on while it is up, and a right click there its cancel on the selected button.</summary>
        public static void Tick()
        {
            if (!Open)
            {
                current = null;
                raycasters = null;
                return;
            }
            if (raycasters != null)
                foreach (var gr in raycasters)
                    if (gr != null && !gr.enabled) gr.enabled = true;
            var m = Mouse.current;
            if (m == null || !m.rightButton.wasPressedThisFrame) return;
            var es = EventSystem.current;
            var sel = es != null ? es.currentSelectedGameObject : null;
            if (sel == null) return;
            ExecuteEvents.Execute(sel, new BaseEventData(es), ExecuteEvents.cancelHandler);
            DevRightCancels++;
        }
    }
}
