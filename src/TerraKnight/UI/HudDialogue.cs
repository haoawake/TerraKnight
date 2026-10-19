using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>
    /// Hollow Knight's dialogue box (the HUD's Dialogue Manager: the box raised and lowered by its "Box Open" FSMs, the
    /// words in its DialogueBox) is put down by the one talking, once the talk ends. Taken away mid-talk (a pylon, the
    /// mirror, a boss seal) the one talking is gone with its room and the box stayed up over the next one: the box goes
    /// down with the player instead, and once more when a room is ready, since no talk goes on past a room's change.
    /// </summary>
    internal static class HudDialogue
    {
        public static int DevClosed;

        static readonly HarmonyLib.AccessTools.FieldRef<DialogueBox, bool> hidden = HarmonyLib.AccessTools.FieldRefAccess<DialogueBox, bool>("hidden");

        /// <summary>Dev: words showing in a dialogue box now.</summary>
        public static bool DevOpen
        {
            get
            {
                foreach (var box in Object.FindObjectsByType<DialogueBox>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                    if (box != null && !hidden(box)) return true;
                return false;
            }
        }

        /// <summary>Puts the dialogue box down, whatever was being said; true when one was up.</summary>
        public static bool Close()
        {
            bool any = false;
            foreach (var box in Object.FindObjectsByType<DialogueBox>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (box == null) continue;
                if (!hidden(box)) any = true;
                box.HideText();
                MarksDown(box);
                var manager = box.transform.parent;
                if (manager == null) continue;
                foreach (var fsm in manager.GetComponents<PlayMakerFSM>())
                {
                    if (fsm == null || !fsm.FsmName.StartsWith("Box Open")) continue;
                    fsm.SendEvent("BOX DOWN");
                    fsm.SendEvent("BOX DOWN DREAM");
                    fsm.SendEvent("BOX DOWN YN");
                }
            }
            if (any) DevClosed++;
            return any;
        }

        public static int DevMarksDown;

        /// <summary>
        /// The page's arrow and the talk's end mark (the Dialogue Manager's Arrow and Stop, shown by the box's Dialogue Page
        /// Control at a page's end and put down by its End Conversation: their renderers off, DOWN to them) put down with
        /// the box, the pages back to Idle: a talk cut off left the arrow in the middle of the screen, waiting for a key.
        /// </summary>
        static void MarksDown(DialogueBox box)
        {
            var pages = FSMUtility.LocateFSM(box.gameObject, "Dialogue Page Control");
            if (pages == null) return;
            bool shown = false;
            foreach (var name in new[] { "Arrow", "Stop" })
            {
                var mark = pages.FsmVariables.FindFsmGameObject(name)?.Value;
                if (mark == null) continue;
                var r = mark.GetComponent<MeshRenderer>();
                if (r != null && r.enabled) { r.enabled = false; shown = true; }
                FSMUtility.SendEventToGameObject(mark, "DOWN");
            }
            string state = pages.ActiveStateName;
            if (state != "Init" && state != "Idle") pages.SetState("Idle");
            if (shown) DevMarksDown++;
        }

        static float nextLook;
        static float staleSince = -1f;

        /// <summary>
        /// Twice a second: an arrow or end mark still drawn under a box whose words are gone (the talk ended some other way
        /// than its own End Conversation) for over half a second is put down too.
        /// </summary>
        public static void Tick()
        {
            if (Time.unscaledTime < nextLook) return;
            nextLook = Time.unscaledTime + 0.5f;
            DialogueBox stale = null;
            foreach (var box in Object.FindObjectsByType<DialogueBox>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
                if (box != null && box.gameObject.name == "Text" && hidden(box) && MarkShown(box)) stale = box;
            if (stale == null) { staleSince = -1f; return; }
            if (staleSince < 0f) { staleSince = Time.unscaledTime; return; }
            if (Time.unscaledTime - staleSince < 0.5f) return;
            staleSince = -1f;
            MarksDown(stale);
        }

        static bool MarkShown(DialogueBox box)
        {
            var pages = FSMUtility.LocateFSM(box.gameObject, "Dialogue Page Control");
            if (pages == null) return false;
            foreach (var name in new[] { "Arrow", "Stop" })
            {
                var mark = pages.FsmVariables.FindFsmGameObject(name)?.Value;
                var r = mark != null ? mark.GetComponent<MeshRenderer>() : null;
                if (r != null && r.enabled && mark.activeInHierarchy) return true;
            }
            return false;
        }
    }
}
