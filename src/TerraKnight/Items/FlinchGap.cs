using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Enemies whose machine answers every blow from wherever it is (a global transition on TOOK DAMAGE or TAKE DAMAGE: the
    /// Elder Baldur's Hit, the Crystallised Husk's Cancel, the Fat Fly's Attack Antic, the Flip Hopper's Flip) hear it at
    /// most once in <see cref="Gap"/> seconds; the blows between still take their health. Made for the nail's pace, a
    /// stream of Terraria shots (several a second) sent the Elder Baldur back to its idle wait on every one, and it never
    /// shot (the user's, 10-25).
    /// </summary>
    [HarmonyPatch]
    internal static class FlinchGap
    {
        public const float Gap = 3f;

        static readonly Dictionary<int, float> heardAt = new Dictionary<int, float>();
        static readonly Dictionary<int, bool> flinchesTook = new Dictionary<int, bool>(), flinchesTake = new Dictionary<int, bool>();
        public static int DevHeld;
        /// <summary>Dev: every blow heard, as before the gap (suite fix501's comparison).</summary>
        public static bool DevOff;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(FSMUtility), nameof(FSMUtility.SendEventToGameObject), typeof(GameObject), typeof(string), typeof(bool))]
        static bool Pass(GameObject go, string eventName)
        {
            if (DevOff || go == null || (eventName != "TOOK DAMAGE" && eventName != "TAKE DAMAGE")) return true;
            // A minion's blow is never heard at all (MinionQuiet): it doesn't use up the enemy's flinch.
            if (MinionQuiet.Body != null && (go == MinionQuiet.Body || go == MinionQuiet.Forward)) return true;
            if (!Flinches(go, eventName)) return true;
            int id = go.GetInstanceID();
            float now = Time.time;
            if (heardAt.TryGetValue(id, out float at) && now - at < Gap && now >= at)
            {
                DevHeld++;
                return false;
            }
            if (heardAt.Count > 2048) heardAt.Clear();
            heardAt[id] = now;
            return true;
        }

        /// <summary>Whether one of the object's machines leaves any state on this event.</summary>
        static bool Flinches(GameObject go, string eventName)
        {
            var cache = eventName == "TOOK DAMAGE" ? flinchesTook : flinchesTake;
            int id = go.GetInstanceID();
            if (cache.TryGetValue(id, out bool known)) return known;
            bool any = false;
            foreach (var f in go.GetComponents<PlayMakerFSM>())
            {
                var global = f.FsmGlobalTransitions;
                if (global == null) continue;
                foreach (var t in global)
                    if (t.EventName == eventName) { any = true; break; }
                if (any) break;
            }
            if (cache.Count > 4096) cache.Clear();
            cache[id] = any;
            return any;
        }
    }
}
