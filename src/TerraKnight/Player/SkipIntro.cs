using HarmonyLib;

namespace TerraKnight.Player
{
    /// <summary>
    /// A new game goes straight in: the opening's cinematics (the prologue's poem, the Knight's
    /// walk to Hallownest) are skipped one after another as each begins, while the opening goes on
    /// loading the Knight and King's Pass as it always does and then enters it, where the player
    /// steps out of the summoning portal (<see cref="OpeningPortal"/>). Each is skipped on its own
    /// (ChainSequence.SkipSingle): skipping the whole chain would leave it playing for ever. And
    /// again every frame while it still plays: the prologue's poem is an animator run to its end
    /// by its skip, which does nothing on the frame it starts (before the animator has begun).
    /// </summary>
    [HarmonyPatch]
    internal static class SkipIntro
    {
        public static bool Enabled = true;
        public static int DevSkipped;
        static readonly AccessTools.FieldRef<OpeningSequence, ChainSequence> chainOf = AccessTools.FieldRefAccess<OpeningSequence, ChainSequence>("chainSequence");

        [HarmonyPostfix]
        [HarmonyPatch(typeof(OpeningSequence), "Update")]
        static void SkipEach(OpeningSequence __instance)
        {
            if (!Enabled || __instance == null) return;
            var chain = chainOf(__instance);
            if (chain == null || !chain.IsPlaying) return;
            if (!chain.IsCurrentSkipped)
            {
                DevSkipped++;
                Plugin.Log.LogInfo($"[开局] 跳过开场过场（第 {DevSkipped} 段）");
            }
            chain.SkipSingle();
        }
    }
}
