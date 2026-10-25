using System.Collections.Generic;
using TerraKnight.UI;
using UnityEngine;

namespace TerraKnight.Achievements
{
    /// <summary>
    /// The achievements (the inventory's "成就" button, or a click on a popup), as Terraria's achievements menu
    /// (UIAchievementsMenu, UIAchievementListItem): its title over a dark blue list panel, "返回" under it. Across the top
    /// the kinds (all, then docs/成就列表's twelve) and the filter (默认 all, 已获得 got, 未获得 not got); the list two to a
    /// row, each Terraria's list item: its icon in its border (a question mark until it is got), the kind's little sign and
    /// its name (gold got, silver not) over its condition (question marks until it is got), and the count with its bar for
    /// one reached by counting. A hidden one not got shows question marks for its name too. Escape, "返回" or the inventory closes it; the game
    /// pauses while it is open (as Terraria's settings window).
    /// </summary>
    internal static class AchievementPanel
    {
        public static bool Open { get; private set; }

        enum Filter { All, Got, NotGot }

        /// <summary>-1 all kinds, else the Cat.</summary>
        static int tab = -1;
        static Filter filter = Filter.All;
        static float scroll;
        static bool dragging;
        static float grab;
        static string focus;
        static int openedFrame;
        static float openedAt;
        static readonly List<Def> rows = new List<Def>();

        static readonly string[] TabZh = { "全部", "圣巢之旅", "首领", "挑战", "收集", "探索", "赌场", "钓鱼", "家园", "战斗", "花式去世", "趣味", "联机" };
        static readonly string[] TabEn = { "All", "Journey", "Bosses", "Challenges", "Collection", "Exploration", "Casino", "Fishing", "Home", "Combat", "Deaths", "Fun", "Multiplayer" };

        static string TabName(int i) => Lang.IsSource ? TabZh[i] : Lang.Culture.StartsWith("zh") ? Lang.T(TabZh[i]) : TabEn[i];

        public static void Show(Def at = null)
        {
            Open = true;
            openedFrame = Time.frameCount;
            openedAt = Time.unscaledTime;
            dragging = false;
            if (at != null)
            {
                tab = (int)at.Cat;
                filter = Filter.All;
                focus = at.Id;
            }
            else focus = null;
            scroll = 0f;
            TerraMod.Instance?.PlaySound("Menu_Open", 0.7f);
        }

        public static void Hide()
        {
            if (!Open) return;
            Open = false;
            TerraMod.Instance?.PlaySound("Menu_Close", 0.7f);
        }

        /// <summary>Dev: the tab and filter shown (as clicks would).</summary>
        public static void DevShow(int category, int filterIndex)
        {
            Show();
            tab = category;
            filter = (Filter)filterIndex;
        }

        static void Fill()
        {
            rows.Clear();
            foreach (var d in AchievementDefs.All)
            {
                if (tab >= 0 && (int)d.Cat != tab) continue;
                bool has = Achievements.Has(d.Id);
                if (filter == Filter.Got && !has || filter == Filter.NotGot && has) continue;
                rows.Add(d);
            }
        }

        static Color C(int r, int g, int b, float a = 1f) => TerraUi.C(r, g, b, a);

        public static void Draw()
        {
            if (!Open) return;
            var gm = GameManager.UnsafeInstance;
            if (gm == null || !GameHooks.IsGameplay(gm) || gm.isPaused)
            {
                Hide();
                return;
            }
            var e = Event.current;
            float ui = GameView.Scale;
            bool settled = Time.frameCount != openedFrame && Time.unscaledTime - openedAt > 0.3f;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape && settled)
            {
                e.Use();
                Hide();
                return;
            }

            var view = GameView.Rect;
            // UIAchievementsMenu: 80% wide (here a little wider for the kinds' row), its list panel under its title,
            // "返回" under that.
            float w = Mathf.Min(1320f * ui, view.width - 40f * ui);
            float top = view.y + Mathf.Max(70f * ui, view.height * 0.1f);
            float bottom = view.yMax - 80f * ui;
            var panel = new Rect(view.center.x - w / 2f, top, w, bottom - top);
            TerraUi.Panel(panel, C(33, 43, 79, 0.8f), Color.black);

            // The title, over the panel's top edge.
            string title = Lang.IsSource ? "成就" : Lang.Culture.StartsWith("zh") ? Lang.T("成就") : "Achievements";
            var ts = TerraUi.Measure(title, TerraUi.Title * ui);
            var titleRect = new Rect(panel.center.x - ts.x / 2f - 13f * ui, panel.y - 33f * ui - ts.y / 2f, ts.x + 26f * ui, ts.y + 26f * ui);
            TerraUi.Panel(titleRect, C(73, 94, 171), Color.black);
            TerraUi.Text(titleRect, title, TerraUi.Title * ui, Color.white, TextAnchor.MiddleCenter);

            // The kinds, across the top in two rows (seven, then six), in big letters.
            float pad = 10f * ui;
            float tabsW = panel.width - 2f * pad;
            const int perRow = 7;
            float tabW = tabsW / perRow, tabH = 44f * ui;
            float fontTab = TerraUi.Large * ui * 0.75f;
            // Shrunk only as far as the longest name needs (measured at 20: the text's smallest size is 8).
            for (int i = 0; i < TabZh.Length; i++)
                fontTab = Mathf.Min(fontTab, 20f * (tabW - 16f * ui) / Mathf.Max(1f, TerraUi.Measure(TabName(i), 20f).x));
            for (int i = 0; i < TabZh.Length; i++)
            {
                int row = i / perRow, col = i % perRow;
                var r = new Rect(panel.x + pad + col * tabW, panel.y + pad + row * (tabH + 6f * ui), tabW - 6f * ui, tabH);
                bool chosen = tab == i - 1;
                bool over = TerraUi.Hover(r, "achtab" + i);
                TerraUi.Panel(r, chosen ? C(73, 94, 171) : over ? C(63, 82, 151) : C(63, 82, 151, 0.8f), chosen ? TerraUi.FatButtonHover : Color.black);
                TerraUi.Text(r, TabName(i), fontTab, chosen ? TerraUi.FatButtonHover : Color.white, TextAnchor.MiddleCenter);
                if (TerraUi.Clicked(r) && !chosen)
                {
                    tab = i - 1;
                    scroll = 0f;
                    focus = null;
                    TerraUi.Sound("Menu_Tick");
                }
            }

            // The filter and the count got, on the next line.
            float y2 = panel.y + pad + 2f * tabH + 6f * ui + 10f * ui;
            Fill();
            int gotHere = 0, allHere = 0;
            foreach (var d in AchievementDefs.All)
            {
                if (tab >= 0 && (int)d.Cat != tab) continue;
                allHere++;
                if (Achievements.Has(d.Id)) gotHere++;
            }
            string tally = Lang.IsSource ? $"已获得 {gotHere} / {allHere}" : Lang.Culture.StartsWith("zh") ? Lang.F("已获得 {0} / {1}", gotHere, allHere) : $"Got {gotHere} / {allHere}";
            TerraUi.Text(new Rect(panel.x + pad + 6f * ui, y2, 400f * ui, 40f * ui), tally, TerraUi.Normal * ui * 1.15f, Color.white);
            string[] fz = { "默认", "已获得", "未获得" }, fe = { "All", "Got", "Not Got" };
            float fw = 150f * ui;
            for (int i = 0; i < 3; i++)
            {
                var r = new Rect(panel.xMax - pad - (3 - i) * (fw + 6f * ui), y2, fw, 40f * ui);
                bool chosen = (int)filter == i;
                bool over = TerraUi.Hover(r, "achfilter" + i);
                TerraUi.Panel(r, chosen ? C(73, 94, 171) : over ? C(63, 82, 151) : C(63, 82, 151, 0.8f), chosen ? TerraUi.FatButtonHover : Color.black);
                string label = Lang.IsSource ? fz[i] : Lang.Culture.StartsWith("zh") ? Lang.T(fz[i]) : fe[i];
                TerraUi.Text(r, label, TerraUi.Normal * ui * 1.15f, chosen ? TerraUi.FatButtonHover : Color.white, TextAnchor.MiddleCenter);
                if (TerraUi.Clicked(r) && !chosen)
                {
                    filter = (Filter)i;
                    scroll = 0f;
                    focus = null;
                    TerraUi.Sound("Menu_Tick");
                }
            }

            // The list: two to a row, scrolled.
            float listTop = y2 + 50f * ui;
            var list = new Rect(panel.x + pad, listTop, panel.width - 2f * pad - 26f * ui, panel.yMax - pad - listTop);
            float itemH = 82f * ui, gap = 6f * ui;
            int cols = list.width > 900f * ui ? 2 : 1;
            float itemW = (list.width - (cols - 1) * gap) / cols;
            int lines = (rows.Count + cols - 1) / cols;
            float total = lines * (itemH + gap);
            if (focus != null)
            {
                int at = rows.FindIndex(d => d.Id == focus);
                if (at >= 0) scroll = Mathf.Clamp((at / cols) * (itemH + gap) - list.height / 3f, 0f, Mathf.Max(0f, total - list.height));
                focus = null;
            }
            if (e.type == EventType.ScrollWheel && panel.Contains(TerraCursor.GuiMouse()))
            {
                scroll = Mathf.Clamp(scroll + e.delta.y * 40f * ui, 0f, Mathf.Max(0f, total - list.height));
                e.Use();
            }
            var bar = new Rect(list.xMax + 8f * ui, list.y + 6f * ui, 20f * ui, list.height - 12f * ui);
            if (total > list.height) scroll = TerraUi.Scrollbar(bar, scroll, list.height, total, ref dragging, ref grab);
            else scroll = 0f;

            TerraUi.BeginClip(list);
            for (int i = 0; i < rows.Count; i++)
            {
                float ry = (i / cols) * (itemH + gap) - scroll;
                if (ry + itemH < 0f || ry > list.height) continue;
                var r = new Rect((i % cols) * (itemW + gap), ry, itemW, itemH);
                DrawItem(r, rows[i], ui);
            }
            TerraUi.EndClip();

            if (rows.Count == 0)
            {
                string none = Lang.IsSource ? "这里还没有成就" : Lang.Culture.StartsWith("zh") ? Lang.T("这里还没有成就") : "Nothing here yet";
                TerraUi.Text(list, none, TerraUi.Normal * ui, new Color(0.75f, 0.75f, 0.8f), TextAnchor.MiddleCenter);
            }

            // "返回", under the panel.
            var back = new Rect(panel.center.x - 160f * ui, panel.yMax + 14f * ui, 320f * ui, 50f * ui);
            string backText = Lang.IsSource ? "返回" : Lang.Culture.StartsWith("zh") ? Lang.T("返回") : "Back";
            bool backOver = TerraUi.Hover(back, "achback");
            TerraUi.Panel(back, backOver ? C(73, 94, 171) : C(63, 82, 151, 0.8f), backOver ? TerraUi.FatButtonHover : Color.black);
            TerraUi.Text(back, backText, TerraUi.Large * ui * 0.8f, Color.white, TextAnchor.MiddleCenter);
            if (TerraUi.Clicked(back) && settled) Hide();

            // Nothing under it takes the mouse while it is open.
            if (e.type == EventType.MouseDown || e.type == EventType.MouseUp || e.type == EventType.ScrollWheel) e.Use();
            TerraUi.EndFrame();
        }

        /// <summary>
        /// UIAchievementListItem.DrawSelf: its panel (lighter under the mouse), the icon in its border on the left, the top
        /// strip with the kind's sign and the name, the bottom strip with the condition, the count's bar on the top strip.
        /// </summary>
        static void DrawItem(Rect r, Def d, float ui)
        {
            bool has = Achievements.Has(d.Id);
            bool over = TerraUi.Over(r);
            TerraUi.Panel(r, over ? C(46, 60, 119) : C(26, 40, 89, 0.8f), over ? C(20, 30, 56) : C(13, 20, 44, 0.8f));

            // PaddingTop 8, PaddingLeft 9: the icon there, its border 4 up and left of it.
            var icon = new Rect(r.x + 9f * ui, r.y + 8f * ui, 64f * ui, 64f * ui);
            if (has && Achievements.IconDrawn(d)) Achievements.DrawIcon(icon, d, Color.white);
            else
            {
                GUI.color = new Color(0.05f, 0.06f, 0.12f, 0.85f);
                GUI.DrawTexture(icon, Texture2D.whiteTexture);
                GUI.color = Color.white;
                TerraUi.Text(icon, "?", TerraUi.Title * ui, has ? Color.white : new Color(0.62f, 0.64f, 0.72f), TextAnchor.MiddleCenter);
            }
            var border = TerraUi.Tex(over ? "UI/Achievement_Borders_MouseHover" : "UI/Achievement_Borders");
            if (border == null) border = TerraUi.Tex("UI/Achievement_Borders");
            float bx = icon.x - 4f * ui, by = icon.y - 4f * ui;
            if (border != null) TerraUi.Whole(new Rect(bx, by, border.width * ui, border.height * ui), border, Color.white);
            float textX = bx + (border != null ? border.width : 72f) * ui + 7f * ui;
            float textW = r.xMax - 9f * ui - textX;

            bool secret = d.Hidden && !has;
            var strip = over ? Color.white : Color.gray;
            var titleColor = Color.Lerp(has ? new Color(1f, 0.84f, 0f) : new Color(0.75f, 0.75f, 0.75f), Color.white, over ? 0.5f : 0f);
            var condColor = Color.Lerp(has ? new Color(0.75f, 0.75f, 0.75f) : new Color(0.66f, 0.66f, 0.66f), Color.white, over ? 1f : 0f);

            // The top strip: the kind's little sign and the name (Achievement_InnerPanelTop: two-pixel ends).
            var topTex = TerraUi.Tex("UI/Achievement_InnerPanelTop");
            float topH = (topTex != null ? topTex.height : 26f) * ui;
            var topR = new Rect(textX, r.y + 6f * ui, textW, topH);
            if (topTex != null)
            {
                TerraUi.Part(new Rect(topR.x, topR.y, 2f * ui, topH), topTex, 0, 0, 2, topTex.height, strip);
                TerraUi.Part(new Rect(topR.x + 2f * ui, topR.y, topR.width - 4f * ui, topH), topTex, 2, 0, 2, topTex.height, strip);
                TerraUi.Part(new Rect(topR.xMax - 2f * ui, topR.y, 2f * ui, topH), topTex, 4, 0, 2, topTex.height, strip);
            }
            var cats = TerraUi.Tex("UI/Achievement_Categories");
            if (cats != null)
            {
                int frame = SignOf(d.Cat);
                float fw = cats.width / 4f, fh = cats.height / 2f;
                TerraUi.Part(new Rect(topR.x + 4f * ui, topR.y + 2f * ui, fw * 0.5f * ui, fh * 0.5f * ui), cats, frame * fw, 0, fw, fh, over ? Color.white : new Color(0.75f, 0.75f, 0.75f));
            }
            string name = secret ? "???" : d.Name;
            string stars = new string('★', Mathf.Max(1, d.Stars));
            TerraUi.Text(new Rect(topR.x + 25f * ui, topR.y, topR.width - 25f * ui, topH), name, TerraUi.Normal * ui * 0.95f, titleColor);

            // The count and its bar, at the top strip's right (one reached by counting, not got yet); else its stars.
            if (!has && !secret && d.Counter != null && d.Goal > 0)
            {
                long now = System.Math.Min(Achievements.Count(d.Counter), d.Goal);
                float barW = 80f * ui, barH = 8f * ui;
                var barR = new Rect(topR.xMax - barW - 8f * ui, topR.center.y - barH / 2f, barW, barH);
                GUI.color = new Color(0f, 0f, 0f, 0.9f);
                GUI.DrawTexture(new Rect(barR.x - ui, barR.y - ui, barR.width + 2f * ui, barR.height + 2f * ui), Texture2D.whiteTexture);
                GUI.color = Color.Lerp(new Color(100 / 255f, 1f, 100 / 255f), Color.black, over ? 0f : 0.25f);
                GUI.DrawTexture(new Rect(barR.x, barR.y, barR.width * Mathf.Clamp01(now / (float)d.Goal), barR.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
                string text = now.ToString("N0") + "/" + d.Goal.ToString("N0");
                float tw = TerraUi.Measure(text, TerraUi.Normal * ui * 0.75f).x;
                TerraUi.Text(new Rect(barR.x - tw - 8f * ui, topR.y, tw, topH), text, TerraUi.Normal * ui * 0.75f, titleColor);
            }
            else TerraUi.Text(new Rect(topR.xMax - 120f * ui, topR.y, 112f * ui, topH), stars, TerraUi.Normal * ui * 0.7f, has ? new Color(1f, 0.84f, 0f) : new Color(0.6f, 0.6f, 0.65f), TextAnchor.MiddleRight);

            // The bottom strip and the condition (Achievement_InnerPanelBottom: six-pixel ends).
            var botTex = TerraUi.Tex("UI/Achievement_InnerPanelBottom");
            float botY = r.y + 6f * ui + 27f * ui;
            float botH = Mathf.Min((botTex != null ? botTex.height : 40f) * ui, r.yMax - 6f * ui - botY);
            var botR = new Rect(textX, botY, textW, botH);
            if (botTex != null)
            {
                TerraUi.Part(new Rect(botR.x, botR.y, 6f * ui, botH), botTex, 0, 0, 6, botTex.height, strip);
                TerraUi.Part(new Rect(botR.x + 6f * ui, botR.y, botR.width - 12f * ui, botH), botTex, 6, 0, 7, botTex.height, strip);
                TerraUi.Part(new Rect(botR.xMax - 6f * ui, botR.y, 6f * ui, botH), botTex, 13, 0, 6, botTex.height, strip);
            }
            // Not got yet: its condition is a question (a hidden one's name too).
            string cond = has ? d.Condition : "???";
            TerraUi.Text(new Rect(botR.x + 8f * ui, botR.y + 2f * ui, botR.width - 14f * ui, botH - 4f * ui), cond, TerraUi.Normal * ui * 0.86f, condColor, TextAnchor.MiddleLeft, true);
        }

        /// <summary>Terraria's four kinds' signs (Slayer, Collector, Explorer, Challenger) for the twelve tabs.</summary>
        static int SignOf(Cat c)
        {
            switch (c)
            {
                case Cat.Boss: case Cat.Combat: case Cat.Death: return 0;
                case Cat.Collect: case Cat.Fishing: case Cat.Casino: return 1;
                case Cat.Journey: case Cat.Explore: case Cat.Home: return 2;
                default: return 3;
            }
        }
    }
}
