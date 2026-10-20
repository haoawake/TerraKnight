using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// An in-game video skipped before CinematicPlayer saw it play (Hornet at the City of Tears fountain, Lurien's
    /// dream): its every-tenth-frame check (Update10) clears isWaitingToPlay only once it finds the video playing, and
    /// Finish comes only after that, so a skip that stops the video first (a low frame rate just after entering the
    /// room, or the video still loading) leaves its blanker black and the game in its cutscene state, the skip prompt
    /// coming back at every key. A skipped video that hasn't finished once its fade-out is over (and a moment more)
    /// is finished here, as its own end does: the blanker goes, play resumes and CINEMATIC END goes out to the
    /// scene's FSMs, which carry the scene on.
    /// </summary>
    [HarmonyPatch(typeof(CinematicPlayer))]
    internal static class CinematicGuard
    {
        static readonly AccessTools.FieldRef<CinematicPlayer, bool> didFinish = AccessTools.FieldRefAccess<CinematicPlayer, bool>("didFinish");
        static readonly AccessTools.FieldRef<CinematicPlayer, bool> videoTriggered = AccessTools.FieldRefAccess<CinematicPlayer, bool>("videoTriggered");
        static readonly System.Reflection.MethodInfo finish = AccessTools.Method(typeof(CinematicPlayer), "Finish");
        /// <summary>The skipped in-game videos and when they should have finished by.</summary>
        static readonly Dictionary<CinematicPlayer, float> skipped = new Dictionary<CinematicPlayer, float>();
        const float Grace = 1.5f;
        public static int DevFinished;

        [HarmonyPrefix]
        [HarmonyPatch(nameof(CinematicPlayer.Skip))]
        static void Skipping(CinematicPlayer __instance)
        {
            if (__instance == null || __instance.videoType != CinematicPlayer.VideoType.InGameVideo || !videoTriggered(__instance)) return;
            var dead = new List<CinematicPlayer>();
            foreach (var k in skipped.Keys) if (k == null) dead.Add(k);
            foreach (var k in dead) skipped.Remove(k);
            // Skip's own fade to black first (FadeOutSpeed: 0.5 s, 2.3 s, none).
            float fade = __instance.fadeOutSpeed == CinematicPlayer.FadeOutSpeed.SLOW ? 2.3f
                : __instance.fadeOutSpeed == CinematicPlayer.FadeOutSpeed.NORMAL ? 0.5f : 0f;
            if (!skipped.ContainsKey(__instance)) skipped[__instance] = Time.unscaledTime + fade + Grace;
        }

        [HarmonyPostfix]
        [HarmonyPatch("Update")]
        static void Watch(CinematicPlayer __instance)
        {
            if (skipped.Count == 0 || !skipped.TryGetValue(__instance, out float due)) return;
            if (didFinish(__instance) || !videoTriggered(__instance))
            {
                skipped.Remove(__instance);
                return;
            }
            if (Time.unscaledTime < due) return;
            skipped.Remove(__instance);
            DevFinished++;
            Plugin.Log.LogInfo($"[演出] {__instance.gameObject.scene.name}/{__instance.name} 跳过后没有收尾（没等到视频开播就停了）：补上收尾，恢复画面和操作");
            finish.Invoke(__instance, null);
            GameManager.UnsafeInstance?.ui?.HideCutscenePrompt();
        }
    }
}
