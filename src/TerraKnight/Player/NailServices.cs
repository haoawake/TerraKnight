namespace TerraKnight.Player
{
    /// <summary>
    /// Inspection helpers for the native nail services. Keep the Nailmasters' conversation,
    /// offers, payment and lessons intact: the Nail item now supports all three learned arts.
    /// Item-acquisition messages are adapted separately by ObsoletePrompts/TutorialNotes.
    /// </summary>
    internal static class NailServices
    {
        public static int DevRedirects => 0;

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
