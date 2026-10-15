using HarmonyLib;

namespace TerraKnight.Player
{
    /// <summary>
    /// The nail's teachers have nothing to teach here. The three nailmasters (Oro, Mato, Sheo) say
    /// their first words and their greetings but never offer their art: no price, no lesson, no
    /// "learned" message. Done on their conversation FSMs as they start: the talks that led on to
    /// the yes/no offer end there instead, and a nailmaster whose art isn't learned greets as one
    /// whose is. The Nailsmith offers his upgrades as in the game: each one raises the Nail item's
    /// damage (Items.NailItem.DamageNow).
    /// </summary>
    [HarmonyPatch]
    internal static class NailServices
    {
        public static int DevRedirects;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(PlayMakerFSM), "Start")]
        static void AfterStart(PlayMakerFSM __instance)
        {
            if (TerraMod.Instance == null || __instance == null || __instance.FsmName != "Conversation Control") return;
            string n = __instance.gameObject.name;
            if (n == "NM Oro NPC" || n == "NM Mato NPC" || n == "NM Sheo NPC")
            {
                Redirect(__instance, "Meet", "CONVO_FINISH", "Talk End");
                Redirect(__instance, "Reoffer", "CONVO_FINISH", "Talk End");
                Redirect(__instance, "Convo Choice", "REOFFER", "Greet");
            }
        }

        /// <summary>A state's transition on an event sent elsewhere (the cached target too: PlayMaker follows that).</summary>
        static void Redirect(PlayMakerFSM fsm, string state, string evt, string to)
        {
            var s = fsm.Fsm.GetState(state);
            var target = fsm.Fsm.GetState(to);
            if (s == null || target == null) return;
            foreach (var t in s.Transitions)
            {
                if (t.EventName != evt) continue;
                t.ToState = to;
                t.ToFsmState = target;
                DevRedirects++;
            }
        }

        /// <summary>Dev: where a state's transition on an event goes now.</summary>
        public static string DevTarget(PlayMakerFSM fsm, string state, string evt)
        {
            var s = fsm.Fsm.GetState(state);
            if (s == null) return "无此状态";
            foreach (var t in s.Transitions) if (t.EventName == evt) return t.ToState;
            return "无此跳转";
        }
    }
}
