using System;
using System.Collections.Generic;
using TerraKnight.Terraria;
using TerraKnight.UI;
using UnityEngine;

namespace TerraKnight.Items
{
    internal static partial class InventoryGui
    {
        static readonly Color JournalInk = new Color(.96f, .91f, .76f);
        static readonly Color JournalMuted = new Color(.63f, .69f, .84f);
        static readonly Color JournalMint = new Color(.66f, .83f, 1f);
        static float journalOpenedAt = -10f, journalPageAt;
        static int journalLastFrame = -10;
        static string journalRecord;
        static readonly Dictionary<string, float> journalHover = new Dictionary<string, float>();
        static readonly Dictionary<string, int> journalHoverFrame = new Dictionary<string, int>();
        public static Rect DevJournalPanel;
        public static float DevJournalOpenPhase, DevJournalPagePhase;
        public static int DevJournalTab => journalTab;
        static readonly Dictionary<string, string> journalWrapped = new Dictionary<string, string>();
        static GUIStyle journalCaption;
        static bool journalFramesQueued;
        static void WarmJournalFrames()
        {
            if (!journalFramesQueued)
            {
                foreach (string name in new[] { "UI/PanelBackground", "UI/PanelBorder", "UI/Achievement_Borders", "UI/Bestiary", "UI/Bestiary/Button_Wide_Border", "UI/Bestiary/Slot_Back", "UI/Bestiary/Slot_Front", "UI/Bestiary/Slot_Selection", "UI/Bestiary/Icon_Rank_Light", "UI/Bestiary/Stat_HP", "UI/Bestiary/Stat_Attack", "UI/Bestiary/Stat_Defense", "UI/Bestiary/Stat_Knockback", "Inventory_Back" })
                    TerrariaAssets.Prefetch(name);
                journalFramesQueued = true;
            }
            TerrariaAssets.TickDeferredOnce();
        }

        // Native Terraria frames are nine-sliced: their pixel corners retain their shape at every window size.
        static void JournalNine(Rect r, string name, float sourceBorder, float border, Color color)
        {
            var tex = TerrariaAssets.Texture(name);
            if (tex.width < 3 || tex.height < 3) return;
            sourceBorder = Mathf.Min(sourceBorder, Mathf.Min(tex.width, tex.height) * .49f);
            border = Mathf.Min(border, Mathf.Min(r.width, r.height) * .49f);
            float[] xs = { r.x, r.x + border, r.xMax - border, r.xMax };
            float[] ys = { r.y, r.y + border, r.yMax - border, r.yMax };
            float[] us = { 0, sourceBorder / tex.width, 1f - sourceBorder / tex.width, 1 };
            float[] vs = { 1, 1f - sourceBorder / tex.height, sourceBorder / tex.height, 0 };
            var previous = GUI.color;
            GUI.color = color;
            for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++)
                GUI.DrawTextureWithTexCoords(new Rect(xs[x], ys[y], xs[x + 1] - xs[x], ys[y + 1] - ys[y]), tex,
                    new Rect(us[x], vs[y + 1], us[x + 1] - us[x], vs[y] - vs[y + 1]));
            GUI.color = previous;
        }

        static void JournalPanel(Rect r, Color color, float ui, bool ornate = false)
        {
            Fill(r, color);
            JournalNine(r, "UI/PanelBackground", 8, 12f * ui, color);
            JournalNine(r, ornate ? "UI/Achievement_Borders" : "UI/PanelBorder", ornate ? 10 : 8,
                (ornate ? 17f : 10f) * ui, ornate ? Color.white : new Color(.38f, .44f, .66f));
        }

        static string JournalWrap(string text, float width, GUIStyle style)
        {
            if (string.IsNullOrEmpty(text)) return text;
            string key = style.fontSize + "|" + Mathf.RoundToInt(width) + "|" + text;
            if (journalWrapped.TryGetValue(key, out var wrapped)) return wrapped;
            var result = new System.Text.StringBuilder();
            float used = 0;
            var elements = System.Globalization.StringInfo.GetTextElementEnumerator(text);
            while (elements.MoveNext())
            {
                string element = elements.GetTextElement();
                if (element == "\n") { result.Append('\n'); used = 0; continue; }
                float size = style.CalcSize(new GUIContent(element)).x;
                if (used > 0 && used + size > width) { result.Append('\n'); used = 0; }
                result.Append(element); used += size;
            }
            if (journalWrapped.Count > 512) journalWrapped.Clear();
            wrapped = result.ToString(); journalWrapped[key] = wrapped; return wrapped;
        }

        static void JournalParagraph(Rect r, string text, Color color) => BookOutlined(r, JournalWrap(text, r.width, bookText), bookText, color);

        static void JournalCorner(Rect r, float ui, bool right)
        {
            // Hand-laid pixel flourishes join the native brass frame to the book's header.
            float sign = right ? -1f : 1f, x = right ? r.xMax : r.x;
            for (int n = 0; n < 6; n++)
            {
                float px = x + sign * (8f + n * 4f) * ui;
                float py = r.y + (8f + n * n * .9f) * ui;
                Fill(new Rect(px, py, 3f * ui, 3f * ui), n % 2 == 0 ? Gold : new Color(.65f, .45f, .21f));
                Fill(new Rect(x + sign * (12f + n * n * .9f) * ui, r.y + (8f + n * 4f) * ui, 3f * ui, 3f * ui), Gold * .8f);
            }
        }

        static void SetBookCatalogue(bool value)
        {
            if (bookCatalogue == value) return;
            bookCatalogue = value;
            bookKey = dropTextKey = journalDropKey = null;
            homeFor = null;
            bestiaryScroll = dropScroll = 0;
            bestiaryScrollTo = null;
        }

        static float JournalEase(float t) { t = Mathf.Clamp01(t); return 1f - (1f - t) * (1f - t) * (1f - t); }

        static void JournalRule(Rect r, Color color, float ui)
        {
            Fill(new Rect(r.x, r.center.y, r.width, ui), new Color(color.r, color.g, color.b, .34f));
            Fill(new Rect(r.x, r.center.y - 2f * ui, 4f * ui, 5f * ui), color);
            Fill(new Rect(r.xMax - 4f * ui, r.center.y - 2f * ui, 4f * ui, 5f * ui), color);
        }

        static void JournalButton(Rect r, string text, bool selected, int hit, float ui, Vector2 mouse)
        {
            bool over = r.Contains(mouse);
            JournalNine(r, "UI/PanelBackground", 8, 6f * ui, selected ? new Color(.25f, .28f, .48f) : over ? BookHeadColor : BookInner);
            JournalNine(r, selected ? "UI/Bestiary/Button_Wide_Border" : "UI/PanelBorder", selected ? 5 : 8, 6f * ui,
                selected ? Color.white : over ? new Color(.8f, .86f, 1f) : BookRim * .8f);
            BookOutlined(r, text, bookCentre, selected ? Gold : over ? JournalInk : JournalMuted);
            Add(r, Kind.BestiaryEntry, hit);
        }

        static void DrawJournalBestiary(Rect view, float baseUi, Vector2 mouse, ref ItemStack hovered, ref string hoveredExtra, ref Kind hoveredKind)
        {
            using var deferred = TerrariaAssets.DeferMissing();
            lastBookMouse = mouse;
            dropMaxScroll = 0;
            var timing = System.Diagnostics.Stopwatch.StartNew();
            float ui = Mathf.Min(baseUi * 1.2f, Mathf.Min((view.width - 32f * baseUi) / 1460f, (view.height - 110f * baseUi) / 810f));
            BookStyles(ui);
            bookTitle.fontSize = Mathf.RoundToInt(30f * ui);
            bookName.fontSize = Mathf.RoundToInt(29f * ui);
            bookSmall.fontSize = Mathf.RoundToInt(17f * ui);
            bookCentre.fontSize = Mathf.RoundToInt(16f * ui);
            bookCount.fontSize = Mathf.RoundToInt(15f * ui);
            bookText.fontSize = Mathf.RoundToInt(17f * ui);
            bookText.fontStyle = FontStyle.Normal;
            bookText.clipping = TextClipping.Clip;
            bookSmall.clipping = bookCount.clipping = bookCentre.clipping = bookName.clipping = TextClipping.Clip;
            if (journalCaption == null) journalCaption = new GUIStyle(bookCount) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(0, 0, 0, 0) };
            journalCaption.fontSize = bookCount.fontSize;
            if (Time.frameCount - journalLastFrame > 4) journalOpenedAt = Time.unscaledTime;
            journalLastFrame = Time.frameCount;
            DevJournalOpenPhase = JournalEase((Time.unscaledTime - journalOpenedAt) / .32f);
            float w = 1460f * ui, h = 810f * ui;
            var panel = new Rect(view.center.x - w * .5f, view.center.y - h * .5f + 28f * baseUi + (1f - DevJournalOpenPhase) * 18f * ui, w, h);
            DevJournalPanel = panel;
            Fill(view, new Color(0, 0, 0, .45f));
            Fill(new Rect(panel.x + 8f * ui, panel.y + 12f * ui, w, h), new Color(0, 0, 0, .55f));
            JournalPanel(panel, BookBack, ui, true);
            JournalPanel(new Rect(panel.x + 16f * ui, panel.y + 15f * ui, w - 32f * ui, 113f * ui), new Color(.105f, .13f, .25f), ui);
            JournalCorner(panel, ui, false); JournalCorner(panel, ui, true);
            Fill(new Rect(panel.x + 45f * ui, panel.y + 9f * ui, (w - 90f * ui) * DevJournalOpenPhase, ui), Gold * .65f);
            Add(panel, Kind.BestiaryPanel, 0);
            if (panel.Contains(mouse)) { hovered = default; hoveredExtra = null; }
            DrawTrackingReturn(panel, ui, mouse);

            var all = AllBookRecords();
            int groups = bestiaryByStage ? 6 : BestiaryRoute.Areas.Count;
            RefreshBook(all, groups);
            if (bestiaryChosen == null || !bestiaryRows.Contains(bestiaryChosen))
            {
                bestiaryChosen = bestiaryRows.Count > 0 ? bestiaryRows[0] : null;
                foreach (var rec in bestiaryRows) if (Bestiary.IsSeen(rec)) { bestiaryChosen = rec; break; }
            }
            if (journalRecord != bestiaryChosen)
            {
                journalRecord = bestiaryChosen; journalPageAt = Time.unscaledTime; dropScroll = 0;
            }
            DevJournalPagePhase = JournalEase((Time.unscaledTime - journalPageAt) / .24f);

            Tex(new Rect(panel.x + 29f * ui, panel.y + 24f * ui, 33f * ui, 33f * ui), "UI/Bestiary", Color.white);
            BookOutlined(new Rect(panel.x + 72f * ui, panel.y + 13f * ui, 190f * ui, 42f * ui), Lang.T("猎人手记"), bookTitle, JournalInk);
            BookOutlined(new Rect(panel.x + 28f * ui, panel.y + 54f * ui, 240f * ui, 24f * ui), Lang.T("圣巢生物 · 战利品档案"), bookSmall, JournalMuted);
            DrawBookSearch(new Rect(panel.x + 280f * ui, panel.y + 20f * ui, 430f * ui, 38f * ui), ui, mouse);
            float modeX = panel.x + 738f * ui;
            JournalButton(new Rect(modeX, panel.y + 20f * ui, 132f * ui, 38f * ui), Lang.T("当前可得"), !bookCatalogue, -60, ui, mouse);
            JournalButton(new Rect(modeX + 136f * ui, panel.y + 20f * ui, 132f * ui, 38f * ui), Lang.T("全部资料"), bookCatalogue, -61, ui, mouse);
            BookOutlined(new Rect(modeX, panel.y + 61f * ui, 300f * ui, 20f * ui), Lang.T(bookCatalogue ? "含未开放来源，不改变探索记录" : "依据当前存档的真实掉落"), bookCount, bookCatalogue ? Gold : JournalMuted);
            BookOutlined(new Rect(panel.xMax - 380f * ui, panel.y + 22f * ui, 310f * ui, 26f * ui), Lang.F("发现 {0} / {1}   ·   击败 {2}", bookSeen, all.Count, bookDone), bookCount, JournalInk);
            var close = new Rect(panel.xMax - 52f * ui, panel.y + 19f * ui, 30f * ui, 32f * ui);
            BookOutlined(close, "×", bookName, close.Contains(mouse) ? Gold : JournalMuted);
            Add(close, Kind.BestiaryButton, 1);
            JournalRule(new Rect(panel.x + 25f * ui, panel.y + 84f * ui, panel.width - 50f * ui, 5f * ui), BookRim, ui);
            BookOutlined(new Rect(panel.x + 28f * ui, panel.y + 94f * ui, 180f * ui, 23f * ui), Lang.F("探索阶段  {0} / {1}", Shop.UnlockedStage(), World.Areas.MaxLevel), bookSmall, Gold);
            for (int s = 0; s <= 5; s++) Fill(new Rect(panel.x + (220f + s * 24f) * ui, panel.y + 101f * ui, 17f * ui, 5f * ui), s <= Shop.UnlockedStage() ? Gold : BookRim * .55f);
            BookOutlined(new Rect(panel.x + 398f * ui, panel.y + 94f * ui, 710f * ui, 23f * ui), Lang.F("圣巢战况  ·  {0}", ProgressionInfo.WorldName), bookSmall, JournalMint);
            BookOutlined(new Rect(panel.xMax - 340f * ui, panel.y + 94f * ui, 310f * ui, 23f * ui), Lang.T("能力进度与首领战况共同决定掉落"), bookCount, JournalMuted);

            float top = panel.y + 132f * ui, bottom = panel.yMax - 34f * ui;
            var side = new Rect(panel.x + 20f * ui, top, 184f * ui, bottom - top);
            JournalPanel(side, new Color(.09f, .12f, .24f), ui);
            JournalButton(new Rect(side.x, side.y, 90f * ui, 30f * ui), Lang.T("地区"), !bestiaryByStage, -2, ui, mouse);
            JournalButton(new Rect(side.x + 94f * ui, side.y, 90f * ui, 30f * ui), Lang.T("阶段"), bestiaryByStage, -3, ui, mouse);
            float rh = Mathf.Min(27f * ui, (side.height - 42f * ui) / (groups + 1));
            float rowY = side.y + 40f * ui;
            for (int f = -1; f < groups; f++)
            {
                int count = f + 1 < groupTotal.Length ? groupTotal[f + 1] : 0;
                if (count == 0) continue;
                var r = new Rect(side.x + 4f * ui, rowY, side.width - 8f * ui, rh - ui);
                bool on = f == ActiveBookFilter;
                if (on || r.Contains(mouse)) JournalNine(r, "UI/PanelBackground", 8, 5f * ui, on ? new Color(.24f, .28f, .49f) : BookHeadColor);
                if (on) Fill(new Rect(r.x, r.y + 3f * ui, 2f * ui, r.height - 6f * ui), Gold);
                string label = f < 0 ? Lang.T(bestiaryByStage ? "全部阶段" : "全部地区") : BookGroupName(f);
                BookOutlined(new Rect(r.x + 9f * ui, r.y, r.width - 46f * ui, r.height), label, bookSmall, on ? Gold : JournalInk);
                BookOutlined(new Rect(r.x, r.y, r.width - 7f * ui, r.height), count.ToString(), bookCount, JournalMuted);
                Add(r, Kind.BestiaryEntry, -100 - f);
                rowY += rh;
            }

            var grid = new Rect(side.xMax + 18f * ui, top, 342f * ui, bottom - top);
            JournalPanel(grid, new Color(.08f, .105f, .21f), ui);
            float step = (BookSlot + BookGap) * ui;
            float LineH(BookLine l) => l.First < 0 ? BookHead * ui : step;
            float room = grid.height - 14f * ui, used = 0;
            bookMaxScroll = bookLines.Count;
            while (bookMaxScroll > 0 && used + LineH(bookLines[bookMaxScroll - 1]) <= room) used += LineH(bookLines[--bookMaxScroll]);
            if (bestiaryScrollTo != null)
            {
                int at = bestiaryRows.IndexOf(bestiaryScrollTo);
                for (int l = 0; l < bookLines.Count && at >= 0; l++)
                    if (bookLines[l].First >= 0 && at >= bookLines[l].First && at < bookLines[l].First + bookLines[l].Count)
                    { int first = l; while (first > 0 && bookLines[first].First >= 0) first--; bestiaryScroll = first; break; }
                bestiaryScrollTo = null;
            }
            bestiaryScroll = Mathf.Clamp(bestiaryScroll, 0, bookMaxScroll);
            float gy = grid.y + 6f * ui;
            string hoverRec = null;
            for (int l = bestiaryScroll; l < bookLines.Count; l++)
            {
                var line = bookLines[l];
                if (gy + LineH(line) > grid.yMax) break;
                if (line.First < 0)
                {
                    BookOutlined(new Rect(grid.x + 8f * ui, gy, grid.width - 20f * ui, 24f * ui), line.Head, bookSmall, JournalMuted);
                    Fill(new Rect(grid.x + 8f * ui, gy + 25f * ui, grid.width - 24f * ui, ui), BookRim * .55f);
                }
                else for (int k = 0; k < line.Count; k++)
                {
                    int index = line.First + k;
                    string rec = bestiaryRows[index];
                    var r = new Rect(grid.x + 6f * ui + k * step, gy, BookSlot * ui, BookSlot * ui);
                    DrawJournalSlot(r, rec, rec == bestiaryChosen, r.Contains(mouse), ui);
                    if (r.Contains(mouse)) hoverRec = rec;
                    Add(r, Kind.BestiaryEntry, index);
                }
                gy += LineH(line);
            }
            if (bookMaxScroll > 0)
            {
                var track = new Rect(grid.xMax - 5f * ui, grid.y, 3f * ui, grid.height);
                Fill(track, BookHeadColor);
                float thumb = Mathf.Max(25f * ui, track.height * room / (room + bookMaxScroll * step));
                Fill(new Rect(track.x, track.y + (track.height - thumb) * bestiaryScroll / bookMaxScroll, track.width, thumb), JournalMint);
            }
            var detail = new Rect(grid.xMax + 22f * ui, top + 8f * ui * (1f - DevJournalPagePhase), panel.xMax - 24f * ui - grid.xMax - 22f * ui, bottom - top);
            JournalPanel(detail, new Color(.075f, .095f, .19f), ui);
            detail = new Rect(detail.x + 12f * ui, detail.y + 10f * ui, detail.width - 24f * ui, detail.height - 20f * ui);
            if (bestiaryChosen != null) DrawJournalEntry(detail, bestiaryChosen, ui, mouse, ref hovered, ref hoveredKind, ref hoveredExtra);
            else
            {
                BookOutlined(new Rect(detail.x + 24f * ui, detail.y + 110f * ui, detail.width - 48f * ui, 50f * ui), Lang.T(BookSearching ? "正在翻阅手记…" : "当前条件下没有找到来源"), bookName, JournalInk);
                JournalParagraph(new Rect(detail.x + 24f * ui, detail.y + 169f * ui, detail.width - 48f * ui, 85f * ui), Lang.T("切换“全部资料”可查未发现的敌人、未开放的掉落与所需战况。商店、宝匣及制作来源请使用材料追踪。"), JournalMuted);
                JournalButton(new Rect(detail.x + 24f * ui, detail.y + 270f * ui, 240f * ui, 38f * ui), Lang.T("查看全部资料与解锁条件"), false, -61, ui, mouse);
            }
            BookOutlined(new Rect(panel.x + 27f * ui, panel.yMax - 28f * ui, 1000f * ui, 20f * ui), Lang.T("滚轮翻阅  ·  悬停物品查看条件  ·  地区不同，战利品也不同"), bookCount, JournalMuted);
            if (Event.current.type == EventType.Repaint) DevBookFrames++;
            DevBookTicks[0] += timing.ElapsedTicks;
            if (hoverRec != null && hovered.IsEmpty)
                DrawLines(mouse, new List<string> { Bestiary.IsSeen(hoverRec) || bookCatalogue ? Bestiary.NameOf(hoverRec) : "？？？", Bestiary.IsDefeated(hoverRec) ? DefeatsText(hoverRec) : Lang.T("尚未击败") }, JournalInk);
        }

        static void DrawJournalSlot(Rect r, string rec, bool chosen, bool over, float ui)
        {
            bool seen = Bestiary.IsSeen(rec), reveal = seen || bookCatalogue;
            journalHover.TryGetValue(rec, out float hover);
            if (!journalHoverFrame.TryGetValue(rec, out int frame) || frame != Time.frameCount)
            {
                hover = Mathf.Lerp(hover, over ? 1f : 0f, 1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f));
                journalHover[rec] = hover; journalHoverFrame[rec] = Time.frameCount;
            }
            Tex(r, "UI/Bestiary/Slot_Back", Color.Lerp(new Color(.7f, .78f, .96f), Color.white, hover));
            if (reveal)
            {
                var sprite = EnemyHover.PortraitOf(rec);
                if (sprite != null)
                {
                    float inset = (7f - hover * 2f) * ui;
                    BestiaryToast.DrawSprite(new Rect(r.x + inset, r.y + inset - hover * 2f * ui, r.width - 2 * inset, r.height - 2 * inset), sprite, seen ? Color.white : new Color(.63f, .72f, .73f));
                }
                else BookOutlined(r, "·", bookCentre, JournalInk);
            }
            else BookOutlined(r, "?", bookName, JournalMuted);
            int cat = CategoryOf(rec);
            Tex(r, "UI/Bestiary/Slot_Front", cat > 0 ? CategoryColor(cat) : Color.white);
            if (Bestiary.IsDefeated(rec)) Tex(new Rect(r.xMax - 16f * ui, r.y + 4f * ui, 12f * ui, 12f * ui), "UI/Bestiary/Icon_Rank_Light", Color.white);
            if (chosen)
            {
                Tex(r, "UI/Bestiary/Slot_Selection", new Color(1, 1, 1, .72f + .28f * Mathf.Sin(Time.unscaledTime * 2f)));
            }
        }

        static void DrawJournalEntry(Rect d, string rec, float ui, Vector2 mouse, ref ItemStack hovered, ref Kind hoveredKind, ref string hoveredExtra)
        {
            bool known = Bestiary.IsSeen(rec), seen = known || bookCatalogue, beaten = Bestiary.IsDefeated(rec);
            var st = Bestiary.StatsOf(rec, bestiaryDropStage < 0 ? Shop.UnlockedStage() : BookDropStage);
            var portrait = new Rect(d.x, d.y, 218f * ui, 139f * ui);
            Tex(portrait, World.Areas.BackgroundOfArea(BestiaryRoute.AreaOf(rec)), new Color(.50f, .63f, .69f));
            Fill(portrait, new Color(.02f, .04f, .06f, .3f));
            if (seen)
            {
                var sprite = EnemyHover.PortraitOf(rec);
                if (sprite != null)
                {
                    float bob = Mathf.Sin(Time.unscaledTime * 1.2f) * 2.5f * ui;
                    var box = new Rect(portrait.x + 22f * ui, portrait.y + 12f * ui + bob, portrait.width - 44f * ui, portrait.height - 24f * ui);
                    BestiaryToast.DrawSprite(new Rect(box.x + 3f * ui, box.y + 5f * ui, box.width, box.height), sprite, new Color(0, 0, 0, .55f));
                    BestiaryToast.DrawSprite(box, sprite, new Color(1, 1, 1, .55f + .45f * DevJournalPagePhase));
                }
            }
            else BookOutlined(new Rect(portrait.x + 90f * ui, portrait.y + 42f * ui, 50f * ui, 50f * ui), "?", bookName, JournalMuted);
            for (int n = 0; n < 9; n++)
            {
                float px = portrait.x + (18f + n * 23f) * ui;
                float py = portrait.yMax - Mathf.Repeat(Time.unscaledTime * (6f + n % 3) + n * 19f, 129f) * ui;
                Fill(new Rect(px, py, 1.5f * ui, 1.5f * ui), new Color(.7f, .86f, .79f, .14f + .15f * Mathf.Sin(Time.unscaledTime + n)));
            }
            JournalNine(portrait, "UI/Achievement_Borders", 10, 10f * ui, Color.white);
            float nx = portrait.xMax + 20f * ui;
            BookOutlined(new Rect(nx, d.y + 2f * ui, d.xMax - nx, 40f * ui), seen ? Bestiary.NameOf(rec) : Lang.T("尚未发现"), bookName, JournalInk);
            BookOutlined(new Rect(nx, d.y + 49f * ui, d.xMax - nx, 25f * ui), Lang.T(sectionNames[CategoryOf(rec)]) + "  /  " + World.Areas.Display(BestiaryRoute.AreaOf(rec)), bookSmall, JournalMint);
            BookOutlined(new Rect(nx, d.y + 79f * ui, d.xMax - nx, 25f * ui), known ? DefeatsText(rec) : bookCatalogue ? Lang.T("资料预览 · 尚未记录发现") : Lang.T("发现后记录，击败后开放掉落资料"), bookSmall, JournalMuted);
            if (TerraMod.Instance != null && TerraMod.Instance.Creative)
            {
                var spawn = new Rect(nx, d.y + 111f * ui, 150f * ui, 27f * ui);
                DevSpawnRect = spawn;
                Fill(spawn, BookHeadColor);
                BookOutlined(spawn, Lang.T(BossArena.Has(rec) ? "传送并召唤" : "生成"), bookCentre, JournalMint);
                if (spawn.Contains(mouse) && Event.current.type == EventType.Repaint && UI.TerraCursor.LeftPressed(UnityEngine.InputSystem.Mouse.current)) BookSpawner.Spawn(rec);
            }
            var values = new[] { seen ? st.Life.ToString() : "?", seen ? st.Damage.ToString() : "?", seen ? st.Defense.ToString() : "?", !seen ? "?" : st.Knockback <= 0 ? Lang.T("免疫") : Mathf.RoundToInt((1f - st.Knockback) * 100f) + "%" };
            var labels = new[] { "生命", "攻击", "防御", "击退抗性" };
            float statW = (d.width - 18f * ui) / 4f;
            for (int n = 0; n < 4; n++)
            {
                var box = new Rect(d.x + n * (statW + 6f * ui), d.y + 151f * ui, statW, 49f * ui);
                StatPanel(box, new[] { "UI/Bestiary/Stat_HP", "UI/Bestiary/Stat_Attack", "UI/Bestiary/Stat_Defense", "UI/Bestiary/Stat_Knockback" }[n]);
                BookOutlined(new Rect(box.x + 48f * ui, box.y + 4f * ui, box.width - 56f * ui, 20f * ui), Lang.T(labels[n]), bookCount, JournalMuted);
                BookOutlined(new Rect(box.x + 48f * ui, box.y + 20f * ui, box.width - 56f * ui, 26f * ui), values[n], bookValue, JournalInk);
            }
            JournalButton(new Rect(d.x, d.y + 213f * ui, 150f * ui, 32f * ui), Lang.T("战利品"), journalTab == 0, -63, ui, mouse);
            JournalButton(new Rect(d.x + 156f * ui, d.y + 213f * ui, 150f * ui, 32f * ui), Lang.T("猎人笔记"), journalTab == 1, -64, ui, mouse);
            JournalButton(new Rect(d.x + 312f * ui, d.y + 213f * ui, 150f * ui, 32f * ui), Lang.T("战况一览"), journalTab == 2, -65, ui, mouse);
            if (journalTab == 1) { DrawJournalNotes(new Rect(d.x, d.y + 260f * ui, d.width, d.height - 260f * ui), rec, seen, beaten, ui); return; }
            if (journalTab == 2) { DrawJournalWorld(new Rect(d.x, d.y + 260f * ui, d.width, d.height - 260f * ui), ui); return; }

            float y = d.y + 257f * ui;
            BookOutlined(new Rect(d.x, y, 86f * ui, 27f * ui), Lang.T("实际掉落"), bookSmall, Gold);
            float lootLeft = d.x + 88f * ui, lootWidth = Mathf.Min(66f * ui, (d.width - 88f * ui) / 7f);
            for (int stage = -1; stage <= 5; stage++)
            {
                var button = new Rect(lootLeft + (stage + 1) * lootWidth, y, lootWidth - 4f * ui, 27f * ui);
                bool chosen = stage < 0 ? LootStage.FollowsProgress : !LootStage.FollowsProgress && LootStage.Current == stage;
                if (LootStage.CanChoose && stage <= Shop.UnlockedStage())
                    JournalButton(button, stage < 0 ? Lang.T("当前") : stage.ToString(), chosen, -71 - stage, ui, mouse);
                else
                {
                    Fill(button, BookHeadColor);
                    BookOutlined(button, stage < 0 ? Lang.T("当前") : stage.ToString(), bookCentre, chosen ? Gold : JournalMuted * .55f);
                }
            }
            y += 31f * ui;
            BookOutlined(new Rect(d.x, y, d.width, 23f * ui), LootStage.CanChoose
                ? Lang.F("掉落阶段 {0} · 只改变掉落，角色与敌人数值保持当前进度", LootStage.Current)
                : Lang.F("掉落阶段 {0} · 由房主选择，角色与敌人数值保持当前进度", LootStage.Current), bookCount, JournalMint);
            y += 32f * ui;
            BookOutlined(new Rect(d.x, y, 75f * ui, 27f * ui), Lang.T("探索预览"), bookSmall, JournalMuted);
            float left = d.x + 88f * ui, bw = Mathf.Min(66f * ui, (d.width - 88f * ui) / 7f);
            for (int stage = -1; stage <= 5; stage++)
                JournalButton(new Rect(left + (stage + 1) * bw, y, bw - 4f * ui, 27f * ui), stage < 0 ? Lang.T("当前") : stage.ToString(), bestiaryDropStage == stage, -11 - stage, ui, mouse);
            y += 36f * ui;
            BookOutlined(new Rect(d.x, y, d.width, 23f * ui), Lang.T(bestiaryDropStage < 0 ? "战况沿用当前存档；切换地区查看对应来源" : "仅预览能力阶段；首领击败记录仍沿用当前存档"), bookCount, JournalMuted);
            y += 31f * ui;
            var homes = BestiaryHabitats.Of(rec);
            float hx = d.x;
            for (int k = 0; k < homes.Count && k < 20; k++)
            {
                string label = World.Areas.Display(homes[k].Area);
                float width = bookCentre.CalcSize(new GUIContent(label)).x + 20f * ui;
                if (hx + width > d.xMax && hx > d.x) { hx = d.x; y += 31f * ui; }
                JournalButton(new Rect(hx, y, width, 26f * ui), label, k == HomeIndex(rec), -30 - k, ui, mouse);
                hx += width + 5f * ui;
            }
            y += 38f * ui;
            if (!beaten && !bookCatalogue)
            {
                JournalParagraph(new Rect(d.x, y + 12f * ui, d.width, 70f * ui), Lang.T("击败后记录真实掉落。也可切换“全部资料”查询来源与解锁条件。"), JournalMuted);
                JournalButton(new Rect(d.x, y + 100f * ui, 240f * ui, 34f * ui), Lang.T("查看全部资料与解锁条件"), false, -61, ui, mouse);
                return;
            }
            DrawJournalDrops(new Rect(d.x, y, d.width, d.yMax - y), rec, ui, mouse, ref hovered, ref hoveredKind, ref hoveredExtra);
        }

        static void DrawJournalNotes(Rect r, string rec, bool seen, bool beaten, float ui)
        {
            EntryText(rec, seen, beaten, r.width - 32f * ui, out string desc, out string notes, out float hd, out float hn);
            desc = JournalWrap(desc, r.width - 32f * ui, bookText);
            notes = JournalWrap(notes, r.width - 32f * ui, bookText);
            hd = bookText.CalcHeight(new GUIContent(desc), r.width - 32f * ui);
            hn = notes == null ? 0 : bookText.CalcHeight(new GUIContent(notes), r.width - 32f * ui);
            float total = hd + hn + 110f * ui;
            dropMaxScroll = Mathf.Max(0, Mathf.CeilToInt((total - r.height) / (35f * ui)));
            dropScroll = Mathf.Clamp(dropScroll, 0, dropMaxScroll);
            dropArea = r;
            GUI.BeginGroup(r);
            float y = 12f * ui - dropScroll * 35f * ui;
            BookOutlined(new Rect(16f * ui, y, r.width - 32f * ui, 26f * ui), Lang.T("图鉴记录"), bookSmall, Gold);
            BookOutlined(new Rect(16f * ui, y + 36f * ui, r.width - 32f * ui, hd + 8f * ui), desc, bookText, JournalInk);
            if (notes != null)
            {
                y += 58f * ui + hd;
                BookOutlined(new Rect(16f * ui, y, r.width - 32f * ui, 26f * ui), Lang.T("猎人笔记"), bookSmall, JournalMint);
                BookOutlined(new Rect(16f * ui, y + 32f * ui, r.width - 32f * ui, hn + 8f * ui), notes, bookText, JournalInk);
            }
            GUI.EndGroup();
        }

        static void DrawJournalWorld(Rect r, float ui)
        {
            JournalParagraph(new Rect(r.x, r.y, r.width, 55f * ui), Lang.T("探索阶段相同，击败不同首领仍会开放新的材料和装备。以下记录以本存档的有效世界战胜利为准。"), JournalMuted);
            float top = r.y + 67f * ui, rowH = 40f * ui, cw = (r.width - 14f * ui) / 2f;
            int fit = Mathf.Max(1, Mathf.FloorToInt((r.yMax - top) / rowH));
            dropMaxScroll = Mathf.Max(0, Mathf.CeilToInt(VanillaBossProgression.All.Count / 2f) - fit);
            dropScroll = Mathf.Clamp(dropScroll, 0, dropMaxScroll);
            dropArea = new Rect(r.x, top, r.width, fit * rowH);
            int index = 0;
            foreach (var pair in VanillaBossProgression.All)
            {
                int i = index++ - dropScroll * 2;
                if (i < 0 || i >= fit * 2) continue;
                var box = new Rect(r.x + i % 2 * (cw + 10f * ui), top + i / 2 * rowH, cw, rowH - 5f * ui);
                bool clear = VanillaBossProgression.Cleared(pair.Key);
                JournalNine(box, "UI/PanelBackground", 8, 5f * ui, clear ? new Color(.14f, .22f, .36f) : BookInner);
                Fill(new Rect(box.x + 7f * ui, box.center.y - 3f * ui, 6f * ui, 6f * ui), clear ? JournalMint : BookRim);
                BookOutlined(new Rect(box.x + 23f * ui, box.y, box.width - 105f * ui, box.height), ProgressionInfo.BossName(pair.Key), bookSmall, clear ? JournalInk : JournalMuted);
                BookOutlined(new Rect(box.x, box.y, box.width - 8f * ui, box.height), Lang.T(clear ? "已击败" : "未击败"), bookCount, clear ? JournalMint : JournalMuted);
            }
            if (dropMaxScroll > 0) BookOutlined(new Rect(r.x, r.yMax - 22f * ui, r.width, 22f * ui), Lang.T("滚轮查看后续首领"), bookCount, JournalMuted);
        }

        sealed class JournalDrop
        {
            internal ItemType Type;
            internal int Stage;
            internal bool Available, MasterOnly;
            internal string Requirement, Chance, Detail;
        }
        static string journalDropKey;
        static readonly List<JournalDrop> journalDrops = new List<JournalDrop>();
        static void BuildJournalDrops(string rec)
        {
            string scene = HomeScene(rec);
            string key = rec + "|" + scene + "|" + BookDropStage + "|" + bestiaryDropStage + "|" + VanillaBossProgression.StateKey + "|" + bookCatalogue + "|" + Lang.Culture + "|" + World.GameDifficulty.Current + "|" + Bestiary.KillsOf(rec) + "|" + EssentialDrops.ManipulatorGiven;
            if (journalDropKey == key) return;
            journalDropKey = key;
            journalDrops.Clear();
            var actual = BookDropsIn(rec, scene);
            var summary = new EnemyDrops.ChanceSummary(actual, BookDropStage);
            var cards = new Dictionary<ItemType, JournalDrop>();
            var texts = DropTexts(rec, actual);
            for (int i = 0; i < actual.Count; i++)
            {
                var row = actual[i];
                if (!summary.Items.TryGetValue(row.Type, out float chance) || chance <= 0) continue;
                if (!cards.TryGetValue(row.Type, out var card))
                {
                    card = new JournalDrop { Type = row.Type, Available = true, Stage = BookDropStage,
                        Chance = Lang.F("{0}% · 至少获得一次", (chance * 100f).ToString("0.##")), Detail = Lang.T("当前存档、所选阶段与地区的单次击杀概率。") };
                    cards[row.Type] = card; journalDrops.Add(card);
                }
                if (!EnemyDrops.Locked(row, BookDropStage) && (!row.Master || World.GameDifficulty.MasterRules))
                    card.Detail += "\n" + texts[i].Name + " · " + texts[i].Chance;
            }
            if (bookCatalogue)
                foreach (var row in BestiaryCatalogue.Of(rec, scene))
                {
                    if (cards.ContainsKey(row.Type)) continue;
                    string missing = ProgressionInfo.Missing(row.Type, BookDropStage, row.FirstStage);
                    if (row.MasterOnly && !World.GameDifficulty.MasterRules) missing += (missing.Length > 0 ? Lang.T("；") : "") + Lang.T("大师或空洞难度");
                    if (missing.Length == 0) missing = Lang.T("此来源在当前战况下不开放；请查看其他阶段、地区或首领奖励条件");
                    journalDrops.Add(new JournalDrop { Type = row.Type, Stage = row.FirstStage, MasterOnly = row.MasterOnly,
                        Requirement = missing, Detail = Lang.T("资料中的潜在来源；未开放时不显示假设掉率，也不会改变存档或实际掉落。") + "\n" + missing });
                }
        }

        public static string DevJournalRequirement(string rec, ItemType type)
        {
            BuildJournalDrops(rec);
            foreach (var d in journalDrops) if (d.Type == type) return d.Available ? "" : d.Requirement;
            return null;
        }
        public static string DevJournalChance(string rec, ItemType type)
        {
            BuildJournalDrops(rec);
            foreach (var d in journalDrops) if (d.Type == type) return d.Chance;
            return null;
        }

        static void DrawJournalDrops(Rect area, string rec, float ui, Vector2 mouse, ref ItemStack hovered, ref Kind hoveredKind, ref string hoveredExtra)
        {
            BuildJournalDrops(rec);
            var cards = new List<JournalDrop>();
            int unlocked = 0, locked = 0;
            bool byItem = Searching && !Matches(Bestiary.NameOf(rec));
            foreach (var card in journalDrops)
            {
                if (byItem && !Matches(ItemDb.Get(card.Type).Name)) continue;
                cards.Add(card);
                if (card.Available) unlocked++; else locked++;
            }
            BookOutlined(new Rect(area.x, area.y, area.width, 24f * ui), Lang.F("当前可得 {0}  ·  待解锁 {1}", unlocked, locked), bookSmall, JournalMint);
            if (EnemyDrops.IsBoss(rec))
                BookOutlined(new Rect(area.x + area.width * .55f, area.y, area.width * .45f, 24f * ui), Lang.F("另得 {0} 吉欧", BookBossGeo(rec, HomeScene(rec))), bookCount, Gold);
            float startY = area.y + 32f * ui, rowH = 76f * ui;
            int columns = area.width >= 540f * ui ? 2 : 1;
            int fit = Mathf.Max(1, Mathf.FloorToInt((area.yMax - startY) / rowH));
            dropMaxScroll = Mathf.Max(0, Mathf.CeilToInt(cards.Count / (float)columns) - fit);
            if (dropFor != rec + "|" + HomeScene(rec) + "|" + bookCatalogue) { dropFor = rec + "|" + HomeScene(rec) + "|" + bookCatalogue; dropScroll = 0; }
            dropScroll = Mathf.Clamp(dropScroll, 0, dropMaxScroll);
            dropArea = new Rect(area.x, startY, area.width, Mathf.Max(0, fit * rowH));
            lastBookMouse = mouse;
            float cw = (area.width - (columns - 1) * 8f * ui - 7f * ui) / columns;
            int first = dropScroll * columns;
            for (int i = first; i < cards.Count && i < first + fit * columns; i++)
            {
                var c = cards[i];
                int relative = i - first;
                var r = new Rect(area.x + relative % columns * (cw + 8f * ui), startY + relative / columns * rowH, cw, rowH - 7f * ui);
                bool over = r.Contains(mouse);
                JournalPanel(r, over ? BookHeadColor : new Color(.105f, .13f, .26f), ui);
                Fill(new Rect(r.x, r.y + 8f * ui, 2f * ui, r.height - 16f * ui), c.Available ? JournalMint : Gold * .7f);
                var icon = new Rect(r.x + 10f * ui, r.y + 13f * ui, 43f * ui, 43f * ui);
                Tex(icon, "Inventory_Back", Color.white);
                DrawIconOnly(icon, ItemStack.Of(c.Type));
                if (!c.Available) Fill(icon, new Color(.02f, .04f, .05f, .24f));
                float x = icon.xMax + 11f * ui, tw = r.xMax - x - 8f * ui;
                BookOutlined(new Rect(x, r.y + 5f * ui, tw, 23f * ui), ItemDb.Get(c.Type).Name, bookSmall, c.Available ? JournalInk : new Color(.76f, .72f, .57f));
                BookOutlined(new Rect(x, r.y + 29f * ui, tw, 20f * ui), c.Available ? c.Chance : Lang.T("尚未开放 · 悬停查看条件"), journalCaption, c.Available ? JournalMint : Gold);
                BookOutlined(new Rect(x, r.y + 50f * ui, tw, 18f * ui), c.Available ? Lang.T("实际掉落 · 当前地区") : c.Requirement, journalCaption, JournalMuted);
                if (over)
                {
                    hovered = ItemStack.Of(c.Type); hoveredKind = Kind.BestiaryEntry; hoveredExtra = JournalWrap(c.Detail, 440f * ui, bookSmall);
                    Rim(r, c.Available ? JournalMint : Gold, ui);
                }
            }
            if (cards.Count == 0)
                BookOutlined(new Rect(area.x + 10f * ui, startY + 20f * ui, area.width - 20f * ui, 60f * ui), Lang.T("本地区在当前条件下没有匹配掉落；可切换地区或查看全部资料。"), bookText, JournalMuted);
            if (dropMaxScroll > 0)
            {
                float height = fit * rowH, thumb = Mathf.Max(24f * ui, height * fit / (fit + dropMaxScroll));
                Fill(new Rect(area.xMax - 4f * ui, startY, 3f * ui, height), BookHeadColor);
                Fill(new Rect(area.xMax - 4f * ui, startY + (height - thumb) * dropScroll / dropMaxScroll, 3f * ui, thumb), JournalMint);
            }
        }
    }
}
