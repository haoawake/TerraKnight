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
                var manager = box.transform.parent;
                if (manager == null) continue;
                foreach (var fsm in manager.GetComponents<PlayMakerFSM>())
                {
                    if (fsm == null || !fsm.FsmName.StartsWith("Box Open")) continue;
                    fsm.SendEvent("BOX DOWN");
                    fsm.SendEvent("BOX DOWN YN");
                }
            }
            if (any) DevClosed++;
            return any;
        }
    }
}
