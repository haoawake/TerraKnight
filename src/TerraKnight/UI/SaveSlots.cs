using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace TerraKnight.UI
{
    /// <summary>
    /// Fifty save slots instead of four: the game's saves are user1.dat ... user50.dat as its own four are
    /// (Platform.GetSaveSlotFileName takes any number), and every one of them is valid to read, load and clear.
    /// The start menu (TerraMenu) lists them all on one scrolling list. The save profiles screen's four buttons
    /// can still stand for any four (page × 4 + their own SaveSlotIndex) for the autotests that drive them.
    /// </summary>
    [HarmonyPatch]
    internal static class SaveSlots
    {
        public const int Max = 50, PerPage = 4;
        public static int Pages => (Max + PerPage - 1) / PerPage;
        public static int Page { get; private set; }
        public static int FirstOfPage => Page * PerPage + 1;

        static readonly System.Reflection.MethodInfo indexGetter = AccessTools.PropertyGetter(typeof(SaveSlotButton), "SaveSlotIndex");

        /// <summary>The slot a button stands for now (page included).</summary>
        public static int IndexOf(SaveSlotButton b) => b == null ? 0 : (int)indexGetter.Invoke(b, null);

        /// <summary>
        /// The game takes only slots 0-4 as valid (Platform.IsSaveSlotIndexValid) when it reads a
        /// save's stats for its button, loads it or clears it, though it writes any. Without this a
        /// save past the fourth was written fine and then shown as corrupted on the menu, with
        /// nothing loadable (the file itself untouched). All fifty are valid here.
        /// </summary>
        public static bool Valid(int slot) => slot >= 0 && slot <= Max;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(global::Platform), nameof(global::Platform.IsSaveSlotIndexValid))]
        static void AllValid(int slotIndex, ref bool __result) => __result = Valid(slotIndex);

        // The callers' own check too: the vanilla one is small enough to be inlined into them.
        [HarmonyTranspiler]
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.LoadGame))]
        static IEnumerable<CodeInstruction> ValidToLoad(IEnumerable<CodeInstruction> code) => ValidInCaller(code);

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.ClearSaveFile))]
        static IEnumerable<CodeInstruction> ValidToClear(IEnumerable<CodeInstruction> code) => ValidInCaller(code);

        [HarmonyTranspiler]
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.GetSaveStatsForSlot))]
        static IEnumerable<CodeInstruction> ValidForStats(IEnumerable<CodeInstruction> code) => ValidInCaller(code);

        static IEnumerable<CodeInstruction> ValidInCaller(IEnumerable<CodeInstruction> code)
        {
            var vanilla = AccessTools.Method(typeof(global::Platform), nameof(global::Platform.IsSaveSlotIndexValid));
            var ours = AccessTools.Method(typeof(SaveSlots), nameof(Valid));
            foreach (var ins in code)
            {
                if (ins.Calls(vanilla)) ins.operand = ours;
                yield return ins;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SaveSlotButton), "get_SaveSlotIndex")]
        static void OnPage(ref int __result)
        {
            if (__result > 0) __result += Page * PerPage;
        }

        /// <summary>A button past the fiftieth slot (the last page's last two) shows nothing and loads nothing.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(SaveSlotButton), "ShowRelevantModeForSaveFileState")]
        static bool HidePastLast(SaveSlotButton __instance)
        {
            ShowNumber(__instance);
            if (IndexOf(__instance) <= Max) return true;
            __instance.HideSaveSlot();
            __instance.interactable = false;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SaveSlotButton), nameof(SaveSlotButton.Prepare))]
        static bool NoSlotPastLast(SaveSlotButton __instance) => IndexOf(__instance) <= Max;

        static SaveSlotButton[] Buttons()
        {
            var ui = UIManager.instance;
            return ui == null ? new SaveSlotButton[0] : new[] { ui.slotOne, ui.slotTwo, ui.slotThree, ui.slotFour };
        }

        static void ShowNumber(SaveSlotButton b)
        {
            if (b == null || b.slotNumberText == null) return;
            int n = IndexOf(b);
            foreach (var t in b.slotNumberText.GetComponentsInChildren<Text>(true)) t.text = n + ".";
            foreach (var t in b.slotNumberText.GetComponentsInChildren<TMPro.TMP_Text>(true)) t.text = n + ".";
        }

        public static void SetPage(int page, bool reload = true)
        {
            page = Mathf.Clamp(page, 0, Pages - 1);
            if (page == Page) return;
            Page = page;
            Plugin.Log.LogInfo($"[存档] 第 {Page + 1} 页：存档 {FirstOfPage}-{Mathf.Min(Max, FirstOfPage + PerPage - 1)}");
            var gm = GameManager.UnsafeInstance;
            foreach (var b in Buttons())
            {
                if (b == null) continue;
                bool shown = reload && gm != null && b.gameObject.activeInHierarchy;
                // The button's states go one way only (a save shown → hidden → loading → empty or a save): hidden first,
                // as when the screen opens, then the new slot read.
                if (shown) b.HideSaveSlot();
                b.ClearCache();
                ShowNumber(b);
                if (IndexOf(b) > Max)
                {
                    // Past the fiftieth: no slot here.
                    b.interactable = false;
                    continue;
                }
                if (shown) b.ResetButton(gm);
            }
            // The highlight never stays on a button with no slot (the last page's last two).
            var es = UnityEngine.EventSystems.EventSystem.current;
            var selected = es != null && es.currentSelectedGameObject != null ? es.currentSelectedGameObject.GetComponent<SaveSlotButton>() : null;
            if (selected != null && IndexOf(selected) > Max && UIManager.instance != null && UIManager.instance.slotOne != null)
            {
                es.SetSelectedGameObject(null);
                UIManager.instance.slotOne.Select();
            }
        }

        /// <summary>Dev: turns to a page as its buttons would.</summary>
        public static void DevSetPage(int page) => SetPage(page);
    }
}
