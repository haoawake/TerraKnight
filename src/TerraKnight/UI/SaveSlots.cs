using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace TerraKnight.UI
{
    /// <summary>
    /// Fifty save slots instead of four. The save profiles screen keeps the game's four slot
    /// buttons and turns them into pages ("‹ 第 1 / 13 页 ›" under the title, or Page Up / Page
    /// Down, or the wheel): each button stands for slot page × 4 + its own (SaveSlotButton's
    /// SaveSlotIndex), the number on it shows which, and a page change has the four read their
    /// saves again. The last page has slots 49 and 50 only. The game's saves are user1.dat ...
    /// user50.dat as its own four are (Platform.GetSaveSlotFileName takes any number); the
    /// screen opens on the page of the slot played last.
    /// </summary>
    [HarmonyPatch]
    internal static class SaveSlots
    {
        public const int Max = 50, PerPage = 4;
        public static int Pages => (Max + PerPage - 1) / PerPage;
        public static int Page { get; private set; }
        public static int FirstOfPage => Page * PerPage + 1;

        static readonly System.Reflection.MethodInfo indexGetter = AccessTools.PropertyGetter(typeof(SaveSlotButton), "SaveSlotIndex");
        static GUIStyle pageStyle;

        /// <summary>The slot a button stands for now (page included).</summary>
        public static int IndexOf(SaveSlotButton b) => b == null ? 0 : (int)indexGetter.Invoke(b, null);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SaveSlotButton), "get_SaveSlotIndex")]
        static void OnPage(ref int __result)
        {
            if (__result > 0) __result += Page * PerPage;
        }

        /// <summary>The screen opens on the page of the slot played last.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(UIManager), nameof(UIManager.UIGoToProfileMenu))]
        static void OpenOnLastPage()
        {
            int last = 0;
            try { last = global::Platform.Current.LocalSharedData.GetInt("lastProfileIndex", 0); }
            catch (System.Exception) { }
            var gm = GameManager.UnsafeInstance;
            if (gm != null && gm.profileID > 0) last = gm.profileID;
            int page = last > 0 ? Mathf.Clamp((last - 1) / PerPage, 0, Pages - 1) : Page;
            if (page != Page) SetPage(page, false);
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

        /// <summary>The pager under the title while the save profiles screen is up and no panel covers it.</summary>
        public static void Draw(bool covered)
        {
            if (!CreativeMenu.ProfilesShown() || covered) return;
            float ui = Screen.height / 1080f;
            CreativeMenu.Styles(ui);
            if (pageStyle == null) pageStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            pageStyle.fontSize = Mathf.RoundToInt(24 * ui);
            float w = 520f * ui, h = 44f * ui;
            var r = new Rect((Screen.width - w) / 2f, 236f * ui, w, h);
            string label = Lang.F("第 {0} / {1} 页", Page + 1, Pages) + "　" + Lang.F("存档 {0}-{1}", FirstOfPage, Mathf.Min(Max, FirstOfPage + PerPage - 1));
            CreativeMenu.Shadowed(new Rect(r.x + 60f * ui, r.y, r.width - 120f * ui, r.height), label, pageStyle, new Color(0.92f, 0.92f, 0.95f));
            if (Page > 0 && CreativeMenu.Button(new Rect(r.x, r.y, 52f * ui, h), "‹", ui)) SetPage(Page - 1);
            if (Page < Pages - 1 && CreativeMenu.Button(new Rect(r.xMax - 52f * ui, r.y, 52f * ui, h), "›", ui)) SetPage(Page + 1);
            var e = Event.current;
            if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.PageDown || e.keyCode == KeyCode.PageUp))
            {
                e.Use();
                SetPage(Page + (e.keyCode == KeyCode.PageDown ? 1 : -1));
            }
            else if (e.type == EventType.ScrollWheel && Mathf.Abs(e.delta.y) > 0.01f)
            {
                e.Use();
                SetPage(Page + (e.delta.y > 0f ? 1 : -1));
            }
        }

        /// <summary>Dev: turns to a page as its buttons would.</summary>
        public static void DevSetPage(int page) => SetPage(page);
    }
}
