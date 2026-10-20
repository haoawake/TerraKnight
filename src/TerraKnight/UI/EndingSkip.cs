using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight.UI
{
    /// <summary>
    /// The endings' videos (the Hollow Knight's, the Radiance's, Godhome's: Cinematic_Ending_*) and the credits after them
    /// run for minutes and the game lets none be skipped the first time: holding Space for a second skips them (the
    /// user's), as the game's own skip does once allowed — the scene's skippable (its CinematicPlayer or CutsceneHelper)
    /// told to Skip, and it goes on to what follows it (the credits, the completion screen). A prompt in the corner says
    /// so, its bar filling while Space is held.
    /// </summary>
    internal static class EndingSkip
    {
        const float HoldTime = 1f;
        /// <summary>A skip that didn't take (the video not started yet) may be held again after this long.</summary>
        const float Retry = 4f;

        static float held, skippedAt;
        static string skippedScene;
        static GUIStyle style;
        public static int DevSkipped;

        static readonly AccessTools.FieldRef<GameManager, List<GameManager.ISkippable>> skippables =
            AccessTools.FieldRefAccess<GameManager, List<GameManager.ISkippable>>("skippables");

        static string SceneNow => GameManager.UnsafeInstance != null ? GameManager.UnsafeInstance.sceneName : null;

        static bool Ending(string s) => s != null && (s.StartsWith("Cinematic_Ending") || s == "End_Credits" || s == "GG_End_Sequence");

        /// <summary>Space held in an ending's scene, not yet skipped there.</summary>
        static bool Waiting(string s) => Ending(s) && (s != skippedScene || Time.unscaledTime - skippedAt > Retry);

        public static void Tick()
        {
            string s = SceneNow;
            if (!Waiting(s))
            {
                held = 0f;
                if (!Ending(s)) skippedScene = null;
                return;
            }
            var kb = Keyboard.current;
            if (kb == null || !kb.spaceKey.isPressed)
            {
                held = 0f;
                return;
            }
            held += Time.unscaledDeltaTime;
            if (held < HoldTime) return;
            held = 0f;
            var gm = GameManager.UnsafeInstance;
            var list = skippables(gm);
            if (list == null || list.Count == 0) return;
            skippedScene = s;
            skippedAt = Time.unscaledTime;
            DevSkipped++;
            var first = list[0];
            Plugin.Log.LogInfo($"[结局] 长按空格跳过 {s}（{first.GetType().Name}）");
            gm.ui?.HideCutscenePrompt();
            gm.StartCoroutine(first.Skip());
        }

        public static void Draw()
        {
            if (Event.current.type != EventType.Repaint || !Waiting(SceneNow)) return;
            float ui = GameView.Scale;
            var view = GameView.Rect;
            if (style == null) style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, wordWrap = false };
            style.fontSize = Mathf.RoundToInt(22 * ui);
            var r = new Rect(view.xMax - 420f * ui, view.yMax - 76f * ui, 380f * ui, 32f * ui);
            string text = Lang.T("长按空格跳过");
            float a = held > 0f ? 0.95f : 0.55f;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.8f * a);
            for (int ox = -1; ox <= 1; ox++)
                for (int oy = -1; oy <= 1; oy++)
                    if (ox != 0 || oy != 0) GUI.Label(new Rect(r.x + ox * 2f * ui, r.y + oy * 2f * ui, r.width, r.height), text, style);
            style.normal.textColor = new Color(1f, 1f, 1f, a);
            GUI.Label(r, text, style);
            if (held <= 0f) return;
            var bar = new Rect(r.xMax - 200f * ui, r.yMax + 4f * ui, 200f * ui, 5f * ui);
            var prev = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, 0.9f);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(held / HoldTime), bar.height), Texture2D.whiteTexture);
            GUI.color = prev;
        }
    }
}
