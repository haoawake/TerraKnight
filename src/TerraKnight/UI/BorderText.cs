using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>
    /// Terraria's bordered text (Utils.DrawBorderString: the text eight times round in the border's colour, then itself
    /// over them), drawn through one kept GUIContent. A GUI.Label given a string allocates twice what a style drawing a
    /// kept content does, and a bordered text is nine draws, every frame; Hollow Knight keeps its collector off in play
    /// (GCManager) and collects all at once past a heap threshold, so the garbage comes back as a stall.
    /// </summary>
    internal static class BorderText
    {
        static readonly GUIContent content = new GUIContent();

        /// <summary>The text in <paramref name="color"/> over its border in <paramref name="border"/>, <paramref name="step"/> px out.</summary>
        public static void Draw(Rect r, string text, GUIStyle style, Color border, Color color, float step)
        {
            if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(text)) return;
            content.text = text;
            style.normal.textColor = border;
            for (int ox = -1; ox <= 1; ox++)
                for (int oy = -1; oy <= 1; oy++)
                    if (ox != 0 || oy != 0) style.Draw(new Rect(r.x + ox * step, r.y + oy * step, r.width, r.height), content, false, false, false, false);
            style.normal.textColor = color;
            style.Draw(r, content, false, false, false, false);
        }
    }
}
