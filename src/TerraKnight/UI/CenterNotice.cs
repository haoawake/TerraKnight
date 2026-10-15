using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>
    /// A line of text in the middle of the screen, outlined in black, held a moment and faded out:
    /// what a refused action says (a boss struck from outside its arena). The same text shown again
    /// while up keeps it up.
    /// </summary>
    internal static class CenterNotice
    {
        static string text;
        static float shownAt = -10f;
        static GUIStyle style;
        const float Hold = 1.6f, Fade = 0.4f;

        public static string DevLast;

        public static void Show(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            text = message;
            shownAt = Time.unscaledTime;
            DevLast = message;
        }

        public static void Draw()
        {
            if (text == null || Event.current.type != EventType.Repaint) return;
            float age = Time.unscaledTime - shownAt;
            if (age > Hold + Fade) { text = null; return; }
            float a = age < Hold ? 1f : 1f - (age - Hold) / Fade;
            float ui = GameView.Scale;
            var view = GameView.Rect;
            if (style == null) style = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false };
            style.fontSize = Mathf.RoundToInt(26 * ui);
            var r = new Rect(view.center.x - 400f * ui, view.center.y - 24f * ui, 800f * ui, 48f * ui);
            style.normal.textColor = new Color(0f, 0f, 0f, 0.9f * a);
            for (int ox = -1; ox <= 1; ox++)
                for (int oy = -1; oy <= 1; oy++)
                    if (ox != 0 || oy != 0) GUI.Label(new Rect(r.x + ox * 2f * ui, r.y + oy * 2f * ui, r.width, r.height), text, style);
            style.normal.textColor = new Color(1f, 1f, 1f, a);
            GUI.Label(r, text, style);
        }
    }
}
