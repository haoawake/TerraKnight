using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace TerraKnight.Player
{
    /// <summary>
    /// The game's big "got it" message (UI Msg Get Item: the ability's name, what it does, the
    /// button to press, waiting for a key) for the abilities that are gone here: the Knight's dash,
    /// claw, wings, crystal heart and shade cloak, the spells, the nail arts, the dream nail and
    /// its gate. The message's FSM (Msg Control) picks its text by the item's name in its first
    /// state; for these it shows nothing and ends at once, so the pickup goes on as after the key
    /// press. What stands in for an ability (the Shield of Cthulhu for the cloak...) is told by the
    /// Terraria pickup text when it is given (TerraMod.GrantAbilityAccessories). The Hunter's
    /// Journal, the King's Brand and the other keys still show theirs.
    /// </summary>
    [HarmonyPatch]
    internal static class ObsoletePrompts
    {
        static readonly HashSet<string> gone = new HashSet<string>
        {
            "Dash", "Walljump", "Double Jump", "Super Dash", "Shadow Dash",
            "Fireball", "Fireball2", "Quake", "Quake 2", "Scream", "Scream 2",
            "NA Cyclone", "NA GSlash", "NA Dash", "Dream Nail", "DN Upgrade", "Dreamgate",
        };

        public static int DevSkipped;
        public static string DevLast;

        /// <summary>Whether an item message is for an ability that is gone.</summary>
        public static bool IsGone(string item) => item != null && gone.Contains(item);

        [HarmonyPrefix]
        [HarmonyPatch(typeof(StringSwitch), nameof(StringSwitch.OnEnter))]
        static bool SkipGone(StringSwitch __instance)
        {
            var fsm = __instance.Fsm;
            if (TerraMod.Instance == null || fsm == null || fsm.Name != "Msg Control" || __instance.stringVariable == null) return true;
            string item = __instance.stringVariable.Value;
            // The journal's names the game's inventory key: told in the chat instead (TutorialNotes), as the abilities' keys are.
            bool told = TutorialNotes.For(item) != null;
            if ((!IsGone(item) && !told) || !(fsm.Owner is PlayMakerFSM msg)) return true;
            foreach (var r in msg.GetComponentsInChildren<Renderer>(true)) r.enabled = false;
            DevSkipped++;
            DevLast = item;
            Plugin.Log.LogInfo($"[提示] 不弹原版的物品提示：{item}");
            TutorialNotes.Note(item);
            TerraMod.Instance.StartCoroutine(End(msg));
            return false;
        }

        /// <summary>A frame later, the message's own end: it tells the pickup it is done, and goes.</summary>
        static IEnumerator End(PlayMakerFSM msg)
        {
            yield return null;
            if (msg != null) msg.SetState("Done");
        }
    }
}
