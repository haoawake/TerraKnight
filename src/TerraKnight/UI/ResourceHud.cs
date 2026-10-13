using System.Collections.Generic;
using TerraKnight.Player;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>
    /// Terraria's classic life and mana display in the top-right corner, replacing Hollow Knight's
    /// masks and soul vessel, which are kept running but never drawn.
    /// </summary>
    internal static class ResourceHud
    {
        const int HeartsPerRow = 10;
        const int LifePerHeart = 20;
        const int ManaPerStar = 20;
        const float HeartStep = 26f;
        const float StarStep = 28f;

        static GUIStyle textStyle;
        static readonly List<Renderer> hiddenHud = new List<Renderer>();
        static GameObject hiddenFor;

        /// <summary>Keeps the game's HUD invisible. Its FSMs still run, only the renderers are off.</summary>
        public static void HideGameHud()
        {
            var cams = GameCameras.instance;
            if (cams == null || cams.hudCanvas == null) return;
            if (hiddenFor != cams.hudCanvas || Time.frameCount % 60 == 0)
            {
                hiddenFor = cams.hudCanvas;
                hiddenHud.Clear();
                cams.hudCanvas.GetComponentsInChildren(true, hiddenHud);
            }
            foreach (var r in hiddenHud)
                if (r != null && r.enabled) r.enabled = false;
        }

        public static void Draw(TerraStats stats)
        {
            if (Event.current.type != EventType.Repaint) return;
            var view = GameView.Rect;
            float ui = GameView.Scale;
            if (textStyle == null) textStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            textStyle.fontSize = Mathf.RoundToInt(20 * ui);

            var heart = TerrariaAssets.Texture("Heart");
            var star = TerrariaAssets.Texture("Mana");
            float top = view.y + 16f * ui;

            // Life: text, then up to two rows of ten hearts to the left of the mana column.
            float right = view.xMax - 70f * ui;
            float hearts = Mathf.Min(HeartsPerRow, Mathf.CeilToInt(stats.LifeMax / (float)LifePerHeart));
            float rowWidth = Mathf.Max(hearts * HeartStep * ui, 200f * ui);
            float x0 = right - rowWidth;
            Outlined(new Rect(x0, top, rowWidth, 26f * ui), Lang.F("生命值：{0}/{1}", stats.Life, stats.LifeMax), Color.white);

            int heartCount = Mathf.CeilToInt(stats.LifeMax / (float)LifePerHeart);
            for (int i = 0; i < heartCount; i++)
            {
                float fill = Mathf.Clamp01((stats.Life - i * LifePerHeart) / (float)LifePerHeart);
                if (fill <= 0f) continue;
                // A part-filled heart shrinks and fades like Terraria's.
                float s = fill >= 1f ? 1f : Mathf.Lerp(0.5f, 1f, fill);
                float w = heart.width * ui * s, h = heart.height * ui * s;
                int col = i % HeartsPerRow, row = i / HeartsPerRow;
                float cx = right - (Mathf.Min(heartCount, HeartsPerRow) - col - 0.5f) * HeartStep * ui;
                float cy = top + 40f * ui + row * HeartStep * ui;
                var prev = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, fill >= 1f ? 1f : Mathf.Lerp(0.4f, 1f, fill));
                GUI.DrawTexture(new Rect(cx - w / 2f, cy - h / 2f, w, h), heart);
                GUI.color = prev;
            }

            // Mana: a vertical column of stars at the far right.
            float mx = view.xMax - 36f * ui;
            Outlined(new Rect(mx - 30f * ui, top, 60f * ui, 26f * ui), Lang.T("魔力"), Color.white);
            int stars = Mathf.CeilToInt(stats.ManaMax / (float)ManaPerStar);
            for (int i = 0; i < stars; i++)
            {
                float fill = Mathf.Clamp01((stats.Mana - i * ManaPerStar) / (float)ManaPerStar);
                float s = fill >= 1f ? 1f : Mathf.Lerp(0.5f, 1f, fill);
                float w = star.width * ui * s, h = star.height * ui * s;
                float cy = top + 40f * ui + i * StarStep * ui;
                var prev = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, fill <= 0f ? 0.25f : Mathf.Lerp(0.4f, 1f, fill));
                GUI.DrawTexture(new Rect(mx - w / 2f, cy - h / 2f, w, h), star);
                GUI.color = prev;
            }
        }

        static void Outlined(Rect r, string text, Color color)
        {
            float ui = GameView.Scale;
            textStyle.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            for (int ox = -1; ox <= 1; ox++)
            for (int oy = -1; oy <= 1; oy++)
                if (ox != 0 || oy != 0) GUI.Label(new Rect(r.x + ox * 1.5f * ui, r.y + oy * 1.5f * ui, r.width, r.height), text, textStyle);
            textStyle.normal.textColor = color;
            GUI.Label(r, text, textStyle);
        }
    }
}
