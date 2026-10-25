using System.Collections.Generic;
using TerraKnight.UI;
using UnityEngine;

namespace TerraKnight.Achievements
{
    /// <summary>
    /// Terraria's achievement popup (InGamePopups.AchievementUnlockedPopup): over the screen's bottom middle, the newest
    /// lowest and the rest pushed up over it; the achievement's name in gold on the inventory's blue panel, its icon in
    /// its border on the right. Five seconds each, scaling in over a quarter second and out over half a second; clicked,
    /// it opens the achievements at that one. Its sound is Terraria's (Sounds/Custom/achievement_complete).
    /// </summary>
    internal static class AchievementPopup
    {
        sealed class Note
        {
            public Def Def;
            public float Age;
        }

        static readonly List<Note> shown = new List<Note>();
        const float Life = 5f, In = 0.25f, Out = 0.5f;

        public static int DevShown;
        public static string DevLast;

        public static void Show(Def d)
        {
            if (d == null) return;
            shown.Insert(0, new Note { Def = d });
            DevShown++;
            DevLast = d.Id;
            TerraMod.Instance?.PlaySound("Custom/achievement_complete", 1f);
        }

        /// <summary>Terraria's Scale: up from nothing as it comes, down to nothing as it goes.</summary>
        static float ScaleOf(Note n)
        {
            float left = Life - n.Age;
            if (left < Out) return Mathf.Clamp01(left / Out);
            if (n.Age < In) return Mathf.Clamp01(n.Age / In);
            return 1f;
        }

        /// <summary>Its Opacity: none until it is half its size.</summary>
        static float OpacityOf(Note n)
        {
            float s = ScaleOf(n);
            return s <= 0.5f ? 0f : (s - 0.5f) / 0.5f;
        }

        public static void Draw()
        {
            if (shown.Count == 0) return;
            var e = Event.current;
            float ui = GameView.Scale;
            if (e.type == EventType.Repaint)
            {
                float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
                foreach (var n in shown) n.Age += dt;
                shown.RemoveAll(n => n.Age >= Life);
            }
            var view = GameView.Rect;
            // InGameNotificationsTracker: the anchor 40 over the screen's bottom, its middle; each popup pushes it up.
            var anchor = new Vector2(view.center.x, view.yMax - 40f * ui);
            var border = TerraUi.Tex("UI/Achievement_Borders");
            var back = TerraUi.Tex("Inventory_Back13");
            var mouse = TerraCursor.GuiMouse();
            Note clicked = null;
            foreach (var n in shown)
            {
                float opacity = OpacityOf(n);
                if (opacity > 0f)
                {
                    float k = ScaleOf(n) * 1.1f;
                    string title = n.Def.Name;
                    float textSize = TerraUi.Normal * ui * k * 0.9f;
                    var size = TerraUi.Measure(title, textSize);
                    var box = new Vector2(size.x + 58f * ui * k, size.y + 10f * ui * k);
                    var r = new Rect(anchor.x - box.x / 2f, anchor.y - box.y, box.x, box.y);
                    bool over = r.Contains(mouse);
                    var panel = new Color(64 / 255f, 109 / 255f, 164 / 255f, over ? 0.75f : 0.5f);
                    if (back != null) TerraUi.Nine(r, back, 10, 32, panel);
                    else TerraUi.Panel(r, panel, Color.black);
                    // Its icon at 0.3 of the scale, in its border, against the right edge.
                    float iconScale = k * 0.3f;
                    float icon = 64f * iconScale * ui;
                    var iconAt = new Vector2(r.xMax - k * (12f + iconScale * 64f) * ui, r.center.y);
                    var tint = new Color(1f, 1f, 1f, opacity);
                    var ir = new Rect(iconAt.x, iconAt.y - icon / 2f, icon, icon);
                    if (Achievements.IconDrawn(n.Def)) Achievements.DrawIcon(ir, n.Def, tint);
                    else TerraUi.Text(ir, "?", textSize, tint, TextAnchor.MiddleCenter);
                    if (border != null)
                    {
                        float bw = border.width * iconScale * ui, bh = border.height * iconScale * ui;
                        TerraUi.Whole(new Rect(iconAt.x - 4f * iconScale * ui, iconAt.y - bh / 2f, bw, bh), border, tint);
                    }
                    // Its name in Main.mouseTextColor's gold, right-aligned 10 short of the icon.
                    var gold = new Color(1f, 1f, 0.2f, opacity);
                    TerraUi.Text(new Rect(r.x, r.y, iconAt.x - 10f * ui - r.x, r.height), title, textSize, gold, TextAnchor.MiddleRight);
                    if (over && e.type == EventType.MouseDown && e.button == 0) clicked = n;
                }
                // PushAnchor: up by 50 times its opacity.
                anchor.y -= 50f * ui * opacity;
                if (anchor.y < -100f) break;
            }
            if (clicked != null)
            {
                e.Use();
                shown.Remove(clicked);
                AchievementPanel.Show(clicked.Def);
            }
        }
    }
}
