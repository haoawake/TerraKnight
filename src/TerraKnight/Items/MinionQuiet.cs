using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// A summoner's minions and sentries don't break off a boss (the user's): their blows on a boss or an elite take its
    /// health and count for nothing more. HealthManager.TakeDamage tells the struck body's machines HIT and TOOK DAMAGE
    /// (and HIT to the body it forwards its hits to), on which some flinch, dodge, counter or teleport out of what they were
    /// doing, and its Stun Control STUN DAMAGE, towards a stagger: while such a blow is dealt (MeleeHits.Deal) the first two
    /// are dropped here, and the stun machine is held off there. The player's own blows (whips too) are untouched.
    /// </summary>
    [HarmonyPatch]
    internal static class MinionQuiet
    {
        /// <summary>The body a minion's blow is being dealt to now, and the one it forwards its hits to (null between blows).</summary>
        public static GameObject Body, Forward;
        public static int DevQuieted, DevDropped;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(FSMUtility), nameof(FSMUtility.SendEventToGameObject), typeof(GameObject), typeof(string), typeof(bool))]
        static bool Drop(GameObject go, string eventName)
        {
            if (Body == null || go == null || (go != Body && go != Forward)) return true;
            if (eventName != "HIT" && eventName != "TOOK DAMAGE") return true;
            DevDropped++;
            return false;
        }
    }
}
