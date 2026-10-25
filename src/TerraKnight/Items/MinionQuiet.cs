using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// A summoner's minions and sentries take health and count for nothing more (the user's): they don't break off a boss,
    /// and they draw no enemy's aggression (a Moss Knight turned on the minion at its back, not on the player).
    /// HealthManager.TakeDamage tells the struck body's machines HIT and TOOK DAMAGE (and HIT to the body it forwards its
    /// hits to), on which some flinch, turn, block, dodge, counter or teleport, and a boss's Stun Control STUN DAMAGE,
    /// towards a stagger: while a minion's blow is dealt (MeleeHits.Deal), on any enemy, the first two are dropped here, and
    /// a boss's stun machine is held off there. The player's own blows (whips too) are untouched.
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
