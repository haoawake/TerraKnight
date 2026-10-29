using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// A boss script that can go round with no wait in it, whichever enemy AI the save has (the user's 10-28 "神居泽诺…打到最后
    /// 一点血的时候突然声音炸一下，然后泽诺会进入不攻击状态"): Xero's Attacking in his rage, with all four swords away, goes
    /// Antic Rage → CANCEL → Check Hero Pos → Antic → Antic Rage in the same frame, his cast heard at each Antic, until
    /// PlayMaker's thousand-loop limit turns the script off for good. His CANCEL there waits his rage's own beat (Wait
    /// Rage) before he looks again, as the new AI's kit does.
    /// </summary>
    [HarmonyPatch]
    internal static class FsmLoopGuards
    {
        public static int DevGuarded;
        /// <summary>Dev (suite xerolow, TK_XERO_NOGUARD=1): the scripts left as the game has them.</summary>
        internal static bool DevOff;

        [HarmonyPostfix, HarmonyPatch(typeof(HealthManager), "Start")]
        static void Guard(HealthManager __instance)
        {
            if (DevOff || __instance == null || !__instance.gameObject.name.StartsWith("Ghost Warrior Xero")) return;
            var attacking = PlayMakerFSM.FindFsmOnGameObject(__instance.gameObject, "Attacking");
            if (attacking != null && Divert(attacking, "Antic Rage", "CANCEL", "Wait Rage")) DevGuarded++;
        }

        static bool Divert(PlayMakerFSM f, string state, string evt, string to)
        {
            var target = f.Fsm.GetState(to);
            var from = f.Fsm.GetState(state);
            if (target == null || from == null) return false;
            foreach (var t in from.Transitions)
                if (t.EventName == evt) { t.ToState = to; t.ToFsmState = target; return true; }
            return false;
        }
    }
}
