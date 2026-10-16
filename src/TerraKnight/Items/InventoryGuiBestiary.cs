using System.Collections.Generic;
using TerraKnight.Terraria;
using TerraKnight.UI;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The monster book, laid out as Terraria 1.4's bestiary with its own art (UI/Bestiary): the
    /// areas down the left in the order a first run reaches them (<see cref="BestiaryRoute"/>),
    /// and every enemy of the chosen area (or of all of them) as a slot in a grid, in three
    /// sections, common enemies, elites and bosses, each in the order they are met (its Hunter's
    /// Journal portrait once seen, a locked "?" before, a star once defeated, the frame tinted for
    /// bosses and elites); the chosen one on the right as a portrait over its area's map
    /// background in the bestiary's frame, its name and kind, a danger rating, the four stat
    /// panels (life, attack, defense, knockback) from when it was seen, the journal's
    /// description, and once it has been defeated the Hunter's notes and everything it can drop
    /// with the chance of each. A creative save's book is all open.
    /// </summary>
    internal static partial class InventoryGui
    {
        static bool bestiaryOpen;
        static int bestiaryScroll, bestiaryFilter = -1;
        static string bestiaryChosen, bestiaryScrollTo;
        static readonly List<string> bestiaryRows = new List<string>();
        static List<string> bestiaryAll;
        static readonly Dictionary<string, int> bookCategory = new Dictionary<string, int>();
        static GUIStyle bookTitle, bookName, bookText, bookSmall, bookValue, bookCentre, bookCount;

        /// <summary>A line of the grid: a section's heading (First -1), or a row of its entries (Count of them from First in the rows).</summary>
        struct BookLine
        {
            public string Head;
            public Color Color;
            public int First, Count;
        }

        static readonly List<BookLine> bookLines = new List<BookLine>();
        static int bookMaxScroll;

        public static bool BestiaryOpen => bestiaryOpen;

        /// <summary>Dev: open or close the book, and which entry (of the full list) is shown.</summary>
        public static void DevBestiary(bool open, int select = 0)
        {
            bestiaryOpen = open;
            var all = AllBookRecords();
            bestiaryFilter = -1;
            bestiaryScroll = 0;
            bestiaryChosen = all.Count > 0 ? all[Mathf.Clamp(select, 0, all.Count - 1)] : null;
        }

        /// <summary>Dev: open the book on a record (all areas, scrolled to its section).</summary>
        public static void DevBestiaryShow(string record)
        {
            bestiaryOpen = true;
            bestiaryFilter = -1;
            bestiaryChosen = record;
            bestiaryScrollTo = record;
        }

        /// <summary>Dev: where the creative spawn button was last drawn.</summary>
        public static Rect? DevSpawnRect;

        /// <summary>Dev: where a record's slot in the book's grid was last drawn.</summary>
        public static Rect? DevBestiaryEntryRect(string record)
        {
            foreach (var h in hits)
                if (h.Kind == Kind.BestiaryEntry && h.Index >= 0 && h.Index < bestiaryRows.Count && bestiaryRows[h.Index] == record) return h.Rect;
            return null;
        }

        /// <summary>Dev: the records whose slots were drawn last frame, in order.</summary>
        public static List<string> DevBestiaryVisible()
        {
            var list = new List<string>();
            foreach (var h in hits)
                if (h.Kind == Kind.BestiaryEntry && h.Index >= 0 && h.Index < bestiaryRows.Count) list.Add(bestiaryRows[h.Index]);
            return list;
        }

        /// <summary>Dev: the book's list scrolled by rows, as the wheel does.</summary>
        public static void DevScrollBestiary(int delta) => ScrollBestiary(delta);

        /// <summary>Dev: where a button of the interface was last drawn (its kind's name, and its index), for the trailer's pointer.</summary>
        public static Rect? DevHitRect(string kind, int index)
        {
            foreach (var h in hits)
                if (h.Kind.ToString() == kind && h.Index == index) return h.Rect;
            return null;
        }

        /// <summary>Dev: the indexes of a kind of button drawn last frame.</summary>
        public static List<int> DevHitIndexes(string kind)
        {
            var list = new List<int>();
            foreach (var h in hits)
                if (h.Kind.ToString() == kind) list.Add(h.Index);
            return list;
        }

        /// <summary>Dev: the area (-1 all, else its place in the route).</summary>
        public static void DevBestiaryFilter(int filter)
        {
            bestiaryFilter = filter;
            bestiaryScroll = 0;
        }

        /// <summary>Dev: what the grid shows now, a line per section ("heading: names").</summary>
        public static string DevBestiaryLayout()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var l in bookLines)
            {
                if (l.First < 0) sb.Append(sb.Length > 0 ? "\n" : "").Append(l.Head).Append("：");
                else for (int k = 0; k < l.Count; k++) sb.Append(Bestiary.NameOf(bestiaryRows[l.First + k])).Append(' ');
            }
            return sb.ToString();
        }

        static readonly string[] sectionNames = { "小怪", "精英", "首领" };

        const int BookColumns = 5;
        const float BookSlot = 70f, BookGap = 6f, BookHead = 30f, BookSide = 176f;

        static readonly Color BookBack = new Color(33 / 255f, 43 / 255f, 92 / 255f, 0.97f);
        static readonly Color BookHeadColor = new Color(44 / 255f, 57 / 255f, 122 / 255f, 1f);
        static readonly Color BookRim = new Color(89 / 255f, 116 / 255f, 213 / 255f, 1f);
        static readonly Color BookInner = new Color(23 / 255f, 31 / 255f, 70 / 255f, 0.95f);
        static readonly Color BossColor = new Color(1f, 0.58f, 0.35f), EliteColor = new Color(0.82f, 0.62f, 1f), Gold = new Color(1f, 0.85f, 0.35f);
        static readonly Color CommonColor = new Color(0.7f, 0.85f, 1f);

        static int CategoryOf(string record) => bookCategory.TryGetValue(record, out int c) ? c : Bestiary.CategoryOf(record);

        static Color CategoryColor(int c) => c == 2 ? BossColor : c == 1 ? EliteColor : CommonColor;

        /// <summary>Every enemy the book lists: by area in route order, the common ones, then elites, then bosses, each in the order met.</summary>
        static List<string> AllBookRecords()
        {
            if (bestiaryAll != null && bestiaryAll.Count > 0) return bestiaryAll;
            bestiaryAll = new List<string>();
            bookCategory.Clear();
            foreach (var r in EnemyDrops.DevRecords)
            {
                var k = EnemyDrops.KindOf(r);
                if (k == "non_enemy" || k == "player_shade") continue;
                bestiaryAll.Add(r);
                bookCategory[r] = Bestiary.CategoryOf(r);
            }
            bestiaryAll.Sort((a, b) =>
            {
                var ra = BestiaryRoute.Of(a);
                var rb = BestiaryRoute.Of(b);
                if (ra.Area != rb.Area) return ra.Area.CompareTo(rb.Area);
                int ca = bookCategory[a], cb = bookCategory[b];
                if (ca != cb) return ca.CompareTo(cb);
                return ra.Order != rb.Order ? ra.Order.CompareTo(rb.Order) : string.CompareOrdinal(a, b);
            });
            return bestiaryAll;
        }

        /// <summary>The grid's lines for the chosen area (or all): a heading per section, then its entries in rows.</summary>
        static void BuildBookLines(List<string> all)
        {
            bestiaryRows.Clear();
            bookLines.Clear();
            var areas = BestiaryRoute.Areas;
            int i = 0;
            while (i < all.Count)
            {
                int area = BestiaryRoute.Of(all[i]).Area, cat = CategoryOf(all[i]);
                int j = i;
                while (j < all.Count && BestiaryRoute.Of(all[j]).Area == area && CategoryOf(all[j]) == cat) j++;
                if (bestiaryFilter < 0 || area == bestiaryFilter)
                {
                    int first = bestiaryRows.Count, seen = 0;
                    for (int k = i; k < j; k++)
                    {
                        bestiaryRows.Add(all[k]);
                        if (Bestiary.IsSeen(all[k])) seen++;
                    }
                    int n = j - i;
                    string head = (bestiaryFilter < 0 ? World.Areas.Display(areas[area]) + " · " : "") + Lang.T(sectionNames[cat]) + $"  {seen}/{n}";
                    bookLines.Add(new BookLine { Head = head, Color = CategoryColor(cat), First = -1 });
                    for (int k = 0; k < n; k += BookColumns)
                        bookLines.Add(new BookLine { First = first + k, Count = Mathf.Min(BookColumns, n - k) });
                }
                i = j;
            }
        }

        static void ToggleBestiary()
        {
            bestiaryOpen = !bestiaryOpen;
            if (bestiaryOpen) hkOpen = false;
            Sound(bestiaryOpen ? "Menu_Open" : "Menu_Close");
        }

        /// <summary>A click in the book: an entry (its index in the rows), or an area (-100 - area, -99 for all).</summary>
        static void SelectBestiary(int i)
        {
            if (i <= -99)
            {
                int f = -100 - i;
                if (f == bestiaryFilter) return;
                bestiaryFilter = f;
                bestiaryScroll = 0;
                Sound("Menu_Tick");
                return;
            }
            if (i < 0 || i >= bestiaryRows.Count || bestiaryRows[i] == bestiaryChosen) return;
            bestiaryChosen = bestiaryRows[i];
            Sound("Menu_Tick");
        }

        static void ScrollBestiary(int delta)
        {
            bestiaryScroll = Mathf.Clamp(bestiaryScroll + delta, 0, bookMaxScroll);
        }

        // The chosen entry's drops, when more than fit: scrolled by rows with the wheel over them.
        static int dropScroll, dropMaxScroll;
        static string dropFor;
        static Rect dropArea;
        static Vector2 lastBookMouse;

        /// <summary>The wheel over the drops of the chosen entry: those scroll, not the book's list.</summary>
        static bool OverDrops => dropMaxScroll > 0 && dropArea.Contains(lastBookMouse);

        static void ScrollDrops(int delta)
        {
            dropScroll = Mathf.Clamp(dropScroll + delta, 0, dropMaxScroll);
        }

        /// <summary>Dev: the chosen entry's drops scrolled by rows, as the wheel over them does; and how far they go.</summary>
        public static void DevScrollDrops(int delta) => ScrollDrops(delta);
        public static int DevDropScroll => dropScroll;
        public static int DevDropMaxScroll => dropMaxScroll;

        static void DrawBestiaryButton(Rect view, float ui, Vector2 mouse)
        {
            float x = view.x + RightOfAmmo(ui), y = view.y + (Margin + 18f) * ui + 70f * ui;
            var r = new Rect(x, y, 90f * ui, 24f * ui);
            bool over = r.Contains(mouse);
            Outlined(r, Lang.T("怪物图鉴"), labelStyle, over || bestiaryOpen ? new Color(1f, 0.85f, 0.3f) : Color.white);
            Add(r, Kind.BestiaryButton, 0);
        }

        static void BookStyles(float ui)
        {
            if (bookTitle == null)
            {
                bookTitle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = false };
                bookName = new GUIStyle(bookTitle);
                bookSmall = new GUIStyle(bookTitle);
                bookValue = new GUIStyle(bookTitle) { alignment = TextAnchor.MiddleRight };
                bookCentre = new GUIStyle(bookTitle) { alignment = TextAnchor.MiddleCenter };
                bookCount = new GUIStyle(bookTitle) { alignment = TextAnchor.MiddleRight };
                bookText = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft, wordWrap = true };
            }
            bookTitle.fontSize = Mathf.RoundToInt(28 * ui);
            bookName.fontSize = Mathf.RoundToInt(25 * ui);
            bookSmall.fontSize = Mathf.RoundToInt(14 * ui);
            bookValue.fontSize = Mathf.RoundToInt(20 * ui);
            bookCentre.fontSize = Mathf.RoundToInt(15 * ui);
            bookCount.fontSize = Mathf.RoundToInt(13 * ui);
            bookText.fontSize = Mathf.RoundToInt(15 * ui);
        }

        static void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        static void Rim(Rect r, Color c, float t)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        static void Tex(Rect r, string name, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, TerrariaAssets.Texture(name));
            GUI.color = prev;
        }

        /// <summary>A progress bar: a dark trough, the share filled in its colour with a lighter top, the text over it.</summary>
        static void Bar(Rect r, float share, Color c, string text, float ui)
        {
            Fill(r, new Color(0f, 0f, 0f, 0.45f));
            var f = new Rect(r.x + 2f * ui, r.y + 2f * ui, (r.width - 4f * ui) * Mathf.Clamp01(share), r.height - 4f * ui);
            Fill(f, c * 0.8f);
            Fill(new Rect(f.x, f.y, f.width, f.height * 0.4f), new Color(c.r, c.g, c.b, 0.9f));
            Rim(r, BookRim, 1.5f * ui);
            Outlined(new Rect(r.x, r.y, r.width, r.height), text, bookCentre, Color.white);
        }

        /// <summary>How often it has been beaten ("尚未击败" if not; a creative save's book is open without it).</summary>
        static string DefeatsText(string rec) =>
            !Bestiary.IsDefeated(rec) ? (Bestiary.IsSeen(rec) ? Lang.T("尚未击败") : "") : Bestiary.KillsOf(rec) > 0 ? Lang.F("已击败 {0} 次", Bestiary.KillsOf(rec)) : Lang.T("创造模式：已解锁");

        static void DrawBestiary(Rect view, float ui, Vector2 mouse, ref ItemStack hovered, ref string hoveredExtra, ref Kind hoveredKind)
        {
            BookStyles(ui);
            var all = AllBookRecords();
            var areas = BestiaryRoute.Areas;
            if (bestiaryFilter >= areas.Count) bestiaryFilter = -1;
            BuildBookLines(all);
            if (bestiaryChosen == null || !bestiaryRows.Contains(bestiaryChosen))
            {
                // The first seen entry of the list, else its first.
                bestiaryChosen = bestiaryRows.Count > 0 ? bestiaryRows[0] : null;
                foreach (var r in bestiaryRows)
                    if (Bestiary.IsSeen(r)) { bestiaryChosen = r; break; }
            }

            // The window, centred in the view under the hotbar.
            float W = Mathf.Min(1220f * ui, view.width - 40f * ui), H = Mathf.Min(720f * ui, view.height - 130f * ui);
            var panel = new Rect(view.center.x - W / 2f, view.y + Mathf.Max(110f * ui, (view.height - H) / 2f + 30f * ui), W, H);
            if (panel.Contains(mouse))
            {
                hovered = default;
                hoveredExtra = null;
            }
            Fill(new Rect(panel.x + 6f * ui, panel.y + 8f * ui, W, H), new Color(0f, 0f, 0f, 0.4f));
            Fill(panel, BookBack);
            var head = new Rect(panel.x, panel.y, W, 70f * ui);
            Fill(head, BookHeadColor);
            Fill(new Rect(head.x, head.yMax - 2f * ui, W, 2f * ui), BookRim);
            Rim(panel, BookRim, 2.5f * ui);
            Add(panel, Kind.BestiaryPanel, 0);

            // The title, the two counts with their bars, and the close button.
            int seen = 0, done = 0;
            foreach (var r in all)
            {
                if (Bestiary.IsSeen(r)) seen++;
                if (Bestiary.IsDefeated(r)) done++;
            }
            Outlined(new Rect(panel.x + 22f * ui, panel.y + 6f * ui, 300f * ui, 36f * ui), Lang.T("怪物图鉴"), bookTitle, new Color(1f, 0.95f, 0.75f));
            Outlined(new Rect(panel.x + 24f * ui, panel.y + 42f * ui, 560f * ui, 20f * ui),
                Bestiary.AllUnlocked ? Lang.T("创造模式：图鉴已全部解锁") : Lang.T("敌人第一次出现在视野中时解锁资料，击败后解锁掉落和猎人笔记"), bookSmall, Bestiary.AllUnlocked ? Gold : new Color(0.75f, 0.8f, 1f));
            float bw = 260f * ui;
            Bar(new Rect(panel.xMax - 2f * bw - 80f * ui, panel.y + 22f * ui, bw, 26f * ui), all.Count > 0 ? seen / (float)all.Count : 0f, new Color(0.35f, 0.65f, 1f), Lang.F("已发现 {0} / {1}", seen, all.Count), ui);
            Bar(new Rect(panel.xMax - bw - 60f * ui, panel.y + 22f * ui, bw, 26f * ui), all.Count > 0 ? done / (float)all.Count : 0f, Gold, Lang.F("已收集 {0} / {1}", done, all.Count), ui);
            var close = new Rect(panel.xMax - 44f * ui, panel.y + 21f * ui, 28f * ui, 28f * ui);
            bool overClose = close.Contains(mouse);
            Fill(close, overClose ? new Color(0.75f, 0.25f, 0.25f, 0.9f) : new Color(0f, 0f, 0f, 0.35f));
            Tex(close, "UI/Bestiary/Button_Border", overClose ? Gold : Color.white);
            Outlined(close, "×", bookCentre, Color.white);
            Add(close, Kind.BestiaryButton, 1);

            // The areas down the left, in route order, each with how much of it has been seen.
            float sx = panel.x + 16f * ui, sy = head.yMax + 12f * ui, sw = BookSide * ui;
            var side = new Rect(sx - 6f * ui, sy - 6f * ui, sw + 12f * ui, panel.yMax - sy - 4f * ui);
            Fill(side, BookInner);
            Rim(side, new Color(BookRim.r, BookRim.g, BookRim.b, 0.6f), 1.5f * ui);
            float rh = Mathf.Min(27f * ui, (side.height - 8f * ui) / (areas.Count + 1));
            for (int f = -1; f < areas.Count; f++)
            {
                int total = 0, s = 0;
                foreach (var r in all)
                    if (f < 0 || BestiaryRoute.Of(r).Area == f) { total++; if (Bestiary.IsSeen(r)) s++; }
                if (total == 0) continue;
                var row = new Rect(sx, sy + (f + 1) * rh, sw, rh - 2f * ui);
                bool on = f == bestiaryFilter, over = row.Contains(mouse);
                if (on) Fill(row, new Color(89 / 255f, 116 / 255f, 213 / 255f, 0.75f));
                else if (over) Fill(row, new Color(1f, 1f, 1f, 0.12f));
                if (on) Rim(row, Gold, 1.5f * ui);
                bool full = s == total;
                Outlined(new Rect(row.x + 8f * ui, row.y, row.width - 50f * ui, row.height), f < 0 ? Lang.T("全部地区") : World.Areas.Display(areas[f]), bookSmall,
                    on ? Color.white : full ? Gold : s > 0 ? new Color(0.88f, 0.9f, 1f) : new Color(0.6f, 0.62f, 0.75f));
                Outlined(new Rect(row.x, row.y, row.width - 6f * ui, row.height), $"{s}/{total}", bookCount, full ? Gold : new Color(0.7f, 0.75f, 0.9f));
                Add(row, Kind.BestiaryEntry, -100 - f);
            }

            // The grid: each section's heading, then its entries; with its scroll bar.
            float gx = side.xMax + 14f * ui, gy = sy;
            float step = (BookSlot + BookGap) * ui, slot = BookSlot * ui, headH = BookHead * ui;
            var gridArea = new Rect(gx - 6f * ui, gy - 6f * ui, BookColumns * step + 6f * ui, panel.yMax - gy - 4f * ui);
            Fill(gridArea, BookInner);
            Rim(gridArea, new Color(BookRim.r, BookRim.g, BookRim.b, 0.6f), 1.5f * ui);
            float LineH(BookLine l) => l.First < 0 ? headH : step;
            // The furthest it scrolls: the last lines just fill the grid.
            float room = gridArea.height - 10f * ui, used = 0f;
            bookMaxScroll = bookLines.Count;
            while (bookMaxScroll > 0 && used + LineH(bookLines[bookMaxScroll - 1]) <= room) used += LineH(bookLines[--bookMaxScroll]);
            if (bestiaryScrollTo != null)
            {
                int at = bestiaryRows.IndexOf(bestiaryScrollTo);
                for (int l = 0; l < bookLines.Count && at >= 0; l++)
                    if (bookLines[l].First >= 0 && at >= bookLines[l].First && at < bookLines[l].First + bookLines[l].Count)
                    {
                        // Its section's heading at the top.
                        int top = l;
                        while (top > 0 && bookLines[top].First >= 0) top--;
                        bestiaryScroll = top;
                        break;
                    }
                bestiaryScrollTo = null;
            }
            bestiaryScroll = Mathf.Clamp(bestiaryScroll, 0, bookMaxScroll);
            string hoverRecord = null;
            float y = gy;
            for (int l = bestiaryScroll; l < bookLines.Count; l++)
            {
                var line = bookLines[l];
                if (y + (line.First < 0 ? headH : slot) > gridArea.yMax - 4f * ui) break;
                if (line.First < 0)
                {
                    Outlined(new Rect(gx + 2f * ui, y + 2f * ui, gridArea.width - 14f * ui, headH - 8f * ui), line.Head, bookSmall, line.Color);
                    Fill(new Rect(gx, y + headH - 6f * ui, gridArea.width - 12f * ui, 1.5f * ui), new Color(line.Color.r, line.Color.g, line.Color.b, 0.55f));
                }
                else
                {
                    for (int k = 0; k < line.Count; k++)
                    {
                        int i = line.First + k;
                        string rec = bestiaryRows[i];
                        var r = new Rect(gx + k * step, y, slot, slot);
                        DrawBookSlot(r, rec, rec == bestiaryChosen, r.Contains(mouse), ui);
                        if (r.Contains(mouse)) hoverRecord = rec;
                        Add(r, Kind.BestiaryEntry, i);
                    }
                }
                y += LineH(line);
            }
            if (bookMaxScroll > 0)
            {
                var track = new Rect(gridArea.xMax + 4f * ui, gridArea.y, 8f * ui, gridArea.height);
                Fill(track, new Color(0f, 0f, 0f, 0.4f));
                float kh = Mathf.Max(24f * ui, track.height * room / (room + bookMaxScroll * step));
                float ky = track.y + (track.height - kh) * bestiaryScroll / bookMaxScroll;
                Fill(new Rect(track.x + 1f * ui, ky, track.width - 2f * ui, kh), BookRim);
            }

            // The chosen entry.
            float dx = gridArea.xMax + 26f * ui;
            var detail = new Rect(dx, gridArea.y, panel.xMax - 18f * ui - dx, panel.yMax - gridArea.y - 10f * ui);
            if (bestiaryChosen != null) DrawBookEntry(detail, bestiaryChosen, ui, mouse, ref hovered, ref hoveredKind, ref hoveredExtra);
            if (hoverRecord != null && hovered.IsEmpty)
            {
                bool seenIt = Bestiary.IsSeen(hoverRecord);
                string state = Bestiary.IsDefeated(hoverRecord) ? DefeatsText(hoverRecord) : seenIt ? Lang.T("已发现") : Lang.T("尚未发现");
                DrawLines(mouse, new List<string> { seenIt ? Bestiary.NameOf(hoverRecord) : "？？？", state },
                    seenIt && CategoryOf(hoverRecord) > 0 ? CategoryColor(CategoryOf(hoverRecord)) : Color.white);
            }
        }

        /// <summary>One entry's slot: the bestiary's slot back, the portrait (or the lock), a star if defeated, the frame, the selection.</summary>
        static void DrawBookSlot(Rect r, string rec, bool chosen, bool over, float ui)
        {
            bool seen = Bestiary.IsSeen(rec), beaten = Bestiary.IsDefeated(rec);
            int cat = CategoryOf(rec);
            Tex(r, "UI/Bestiary/Slot_Back", seen ? Color.white : new Color(0.55f, 0.55f, 0.65f));
            if (seen)
            {
                var sprite = EnemyHover.PortraitOf(rec);
                if (sprite != null) BestiaryToast.DrawSprite(new Rect(r.x + 7f * ui, r.y + 7f * ui, r.width - 14f * ui, r.height - 14f * ui), sprite, Color.white);
                else Outlined(r, Bestiary.NameOf(rec), bookSmall, Color.white);
            }
            else
            {
                var lk = TerrariaAssets.Texture("UI/Bestiary/Icon_Locked");
                Tex(new Rect(r.center.x - lk.width * 0.6f * ui, r.center.y - lk.height * 0.6f * ui, lk.width * 1.2f * ui, lk.height * 1.2f * ui), "UI/Bestiary/Icon_Locked", new Color(1f, 1f, 1f, 0.45f));
            }
            if (over) Fill(r, new Color(1f, 1f, 1f, 0.1f));
            var frame = !seen || cat == 0 ? Color.white : CategoryColor(cat);
            Tex(r, "UI/Bestiary/Slot_Front", frame);
            if (beaten) Tex(new Rect(r.xMax - 21f * ui, r.y + 5f * ui, 16f * ui, 16f * ui), "UI/Bestiary/Icon_Rank_Light", Color.white);
            if (chosen) Tex(r, "UI/Bestiary/Slot_Selection", Color.white);
        }

        /// <summary>The right page: portrait, name and kind, danger, stats, description, notes and drops.</summary>
        /// <summary>The note under the cursor on a locked drop (when it unlocks), drawn after the book.</summary>
        public static string BookHint;

        static void DrawBookEntry(Rect d, string rec, float ui, Vector2 mouse, ref ItemStack hovered, ref Kind hoveredKind, ref string hoveredExtra)
        {
            bool seen = Bestiary.IsSeen(rec), beaten = Bestiary.IsDefeated(rec);
            var st = Bestiary.StatsOf(rec);
            int cat = CategoryOf(rec);
            string area = BestiaryRoute.AreaOf(rec);

            // The portrait: the area's map background inside the bestiary's portrait frame (193 × 112, doubled where it fits).
            float pw = Mathf.Min(386f * ui, d.width * 0.56f), ph = pw * 112f / 193f;
            var port = new Rect(d.x, d.y, pw, ph);
            float inset = 4f * pw / (193f * 1f);
            var inner = new Rect(port.x + inset, port.y + inset, port.width - 2f * inset, port.height - 2f * inset);
            var bg = World.Areas.BackgroundOfArea(area);
            Tex(inner, bg, seen ? Color.white : new Color(0.25f, 0.25f, 0.32f));
            // A soft vignette so the portrait stands out.
            Fill(new Rect(inner.x, inner.yMax - inner.height * 0.35f, inner.width, inner.height * 0.35f), new Color(0f, 0f, 0f, 0.25f));
            if (seen)
            {
                var sprite = EnemyHover.PortraitOf(rec);
                if (sprite != null)
                {
                    var box = new Rect(inner.x + 14f * ui, inner.y + 10f * ui, inner.width - 28f * ui, inner.height - 20f * ui);
                    BestiaryToast.DrawSprite(new Rect(box.x + 4f * ui, box.y + 6f * ui, box.width, box.height), sprite, new Color(0f, 0f, 0f, 0.45f));
                    BestiaryToast.DrawSprite(box, sprite, Color.white);
                }
            }
            else
            {
                var lk = TerrariaAssets.Texture("UI/Bestiary/Icon_Locked");
                Tex(new Rect(inner.center.x - lk.width * 1.25f * ui, inner.center.y - lk.height * 1.25f * ui - 8f * ui, lk.width * 2.5f * ui, lk.height * 2.5f * ui), "UI/Bestiary/Icon_Locked", new Color(1f, 1f, 1f, 0.7f));
                Outlined(new Rect(inner.x, inner.yMax - 34f * ui, inner.width, 24f * ui), Lang.T("尚未发现"), bookCentre, new Color(0.85f, 0.85f, 0.95f));
            }
            Tex(port, "UI/Bestiary/Portrait_Front", Color.white);

            // Name, kind and area, danger, defeats.
            float cx = port.xMax + 18f * ui, cw = d.xMax - cx;
            var nameColor = !seen ? new Color(0.75f, 0.75f, 0.85f) : cat > 0 ? CategoryColor(cat) : new Color(1f, 0.95f, 0.7f);
            Outlined(new Rect(cx, d.y, cw, 34f * ui), seen ? Bestiary.NameOf(rec) : "？？？", bookName, nameColor);
            float chipX = cx;
            void Chip(string text, Color c)
            {
                float w = bookSmall.CalcSize(new GUIContent(text)).x + 16f * ui;
                var chip = new Rect(chipX, d.y + 40f * ui, w, 22f * ui);
                Fill(chip, new Color(c.r * 0.35f, c.g * 0.35f, c.b * 0.35f, 0.9f));
                Rim(chip, c, 1.5f * ui);
                Outlined(chip, text, bookCentre, c);
                chipX += w + 6f * ui;
            }
            if (seen)
            {
                Chip(Lang.T(cat == 2 ? "首领" : cat == 1 ? "精英" : EnemyDrops.KindOf(rec) == "passive" ? "无害" : "小怪"), CategoryColor(cat));
                Chip(area, new Color(0.6f, 0.9f, 0.7f));
            }
            else Chip(Lang.T("未知"), new Color(0.7f, 0.7f, 0.8f));
            // Creative: the spawn button, at the right of the kind and area tags (on the danger row
            // when the tags reach it): a copy ten tiles in front of the player, or for a boss, the
            // player taken to its arena to fight it there (BossArena).
            if (TerraMod.Instance != null && TerraMod.Instance.Creative)
            {
                string label = Lang.T(BossArena.Has(rec) ? "传送并召唤" : "生成");
                float bw = Mathf.Max(72f * ui, bookCentre.CalcSize(new GUIContent(label)).x + 16f * ui);
                float bx = d.xMax - bw - 4f * ui;
                var spawn = new Rect(bx, bx >= chipX + 4f * ui ? d.y + 38f * ui : d.y + 68f * ui, bw, 26f * ui);
                DevSpawnRect = spawn;
                bool over = spawn.Contains(mouse);
                Fill(spawn, over ? new Color(0.32f, 0.55f, 0.28f, 0.95f) : new Color(0.18f, 0.32f, 0.16f, 0.9f));
                Rim(spawn, new Color(0.7f, 1f, 0.6f), 1.5f * ui);
                Outlined(spawn, label, bookCentre, Color.white);
                var m = UnityEngine.InputSystem.Mouse.current;
                if (over && Event.current.type == EventType.Repaint && UI.TerraCursor.LeftPressed(m)) BookSpawner.Spawn(rec);
            }
            Outlined(new Rect(cx, d.y + 70f * ui, 80f * ui, 20f * ui), Lang.T("危险度"), bookSmall, new Color(0.8f, 0.85f, 1f));
            int danger = seen ? Mathf.Clamp(st.Level + 1 + (cat == 2 ? 1 : 0), 1, 6) : 0;
            for (int s = 0; s < 6; s++)
                Tex(new Rect(cx + 62f * ui + s * 20f * ui, d.y + 72f * ui, 16f * ui, 16f * ui), s < danger ? "UI/Bestiary/Icon_Rank_Light" : "UI/Bestiary/Icon_Rank_Dim", Color.white);
            Outlined(new Rect(cx, d.y + 96f * ui, cw, 20f * ui), DefeatsText(rec), bookSmall, beaten ? Gold : new Color(0.8f, 0.8f, 0.85f));
            if (seen) Outlined(new Rect(cx, d.y + 118f * ui, cw, 20f * ui), Lang.F("第 {0} 阶段", st.Level), bookSmall, new Color(0.7f, 0.75f, 0.9f));

            // The four stat panels under the portrait (Stat_HP / Attack / Defense / Knockback: the icon kept, the panel stretched).
            float sy = port.yMax + 12f * ui, sw = (d.width - 3f * 8f * ui) / 4f, sh = 46f * ui;
            // Terraria's knockBackResist is the share of a push taken: shown as how much of it is shrugged off.
            string kb = !seen ? "???" : st.Knockback <= 0f ? Lang.T("免疫") : $"{Mathf.RoundToInt((1f - st.Knockback) * 100f)}%";
            var stats = new (string Tex, string Label, string Value)[]
            {
                ("UI/Bestiary/Stat_HP", Lang.T("生命"), seen ? st.Life.ToString() : "???"),
                ("UI/Bestiary/Stat_Attack", Lang.T("攻击"), seen ? (st.Damage > 0 ? st.Damage.ToString() : "0") : "???"),
                ("UI/Bestiary/Stat_Defense", Lang.T("防御"), seen ? st.Defense.ToString() : "???"),
                ("UI/Bestiary/Stat_Knockback", Lang.T("击退抗性"), kb),
            };
            for (int s = 0; s < 4; s++)
            {
                var r = new Rect(d.x + s * (sw + 8f * ui), sy, sw, sh);
                StatPanel(r, stats[s].Tex);
                Outlined(new Rect(r.x + sh + 4f * ui, r.y + 3f * ui, r.width - sh - 10f * ui, 18f * ui), stats[s].Label, bookSmall, new Color(0.8f, 0.85f, 1f));
                Outlined(new Rect(r.x + sh, r.y + 16f * ui, r.width - sh - 10f * ui, 28f * ui), stats[s].Value, bookValue, Color.white);
            }

            // The journal's description, then the Hunter's notes once defeated.
            float y = sy + sh + 14f * ui;
            var textBox = new Rect(d.x, y, d.width, 0f);
            string desc = seen ? EnemyHover.JournalText(rec, "DESC_") ?? Lang.T("猎人日志里没有这种敌人的描述。") : Lang.T("在视野中发现这种敌人后解锁它的资料。");
            string notes = beaten ? EnemyHover.JournalText(rec, "NOTE_") : null;
            float textW = d.width - 28f * ui;
            float hDesc = bookText.CalcHeight(new GUIContent(desc), textW);
            float hNotes = notes != null ? bookText.CalcHeight(new GUIContent(notes), textW) + 26f * ui : 0f;
            float dropsMin = 120f * ui;
            float avail = d.yMax - y - dropsMin;
            float boxH = Mathf.Min(hDesc + 34f * ui + hNotes, Mathf.Max(80f * ui, avail));
            textBox.height = boxH;
            Fill(textBox, BookInner);
            Rim(textBox, new Color(BookRim.r, BookRim.g, BookRim.b, 0.6f), 1.5f * ui);
            GUI.BeginGroup(textBox);
            Outlined(new Rect(14f * ui, 6f * ui, 300f * ui, 20f * ui), Lang.T("图鉴记录"), bookSmall, Gold);
            Outlined(new Rect(14f * ui, 28f * ui, textW, hDesc + 4f * ui), desc, bookText, seen ? new Color(0.95f, 0.95f, 1f) : new Color(0.7f, 0.7f, 0.8f));
            if (notes != null)
            {
                float ny = 34f * ui + hDesc;
                Outlined(new Rect(14f * ui, ny, 300f * ui, 20f * ui), Lang.T("猎人笔记"), bookSmall, new Color(0.6f, 0.95f, 0.75f));
                Outlined(new Rect(14f * ui, ny + 22f * ui, textW, hNotes), notes, bookText, new Color(0.82f, 0.9f, 0.85f));
            }
            GUI.EndGroup();

            // What it drops: shown once defeated, locked slots before.
            y = textBox.yMax + 12f * ui;
            var drops = EnemyDrops.Of(rec);
            Outlined(new Rect(d.x, y, 300f * ui, 22f * ui), beaten ? Lang.F("掉落物（{0} 种）", drops.Count) : Lang.T("掉落物"), bookSmall, Gold);
            if (!beaten) Outlined(new Rect(d.x + 90f * ui, y, 400f * ui, 22f * ui), Lang.T("击败后解锁掉落信息"), bookSmall, new Color(0.75f, 0.75f, 0.85f));
            y += 26f * ui;
            if (beaten && drops.Count == 0) Outlined(new Rect(d.x, y, 400f * ui, 22f * ui), Lang.T("只掉吉欧"), bookSmall, new Color(0.75f, 0.75f, 0.8f));
            int cols = Mathf.Clamp((int)(d.width / (170f * ui)), 1, 4);
            float cwid = d.width / cols, rowH = 46f * ui;
            int maxRows = Mathf.Max(1, (int)((d.yMax - y) / rowH));
            // More than fit: they scroll by rows (the wheel over them), with a bar at the side.
            int rows = (drops.Count + cols - 1) / cols;
            if (dropFor != rec) { dropFor = rec; dropScroll = 0; }
            dropMaxScroll = Mathf.Max(0, rows - maxRows);
            dropScroll = Mathf.Clamp(dropScroll, 0, dropMaxScroll);
            dropArea = new Rect(d.x, y, d.width, maxRows * rowH);
            lastBookMouse = mouse;
            if (dropMaxScroll > 0)
            {
                cwid = (d.width - 10f * ui) / cols;
                var track = new Rect(d.xMax - 6f * ui, y, 4f * ui, maxRows * rowH);
                Fill(track, new Color(1f, 1f, 1f, 0.12f));
                float thumbH = Mathf.Max(12f * ui, track.height * maxRows / rows);
                Fill(new Rect(track.x, track.y + (track.height - thumbH) * dropScroll / dropMaxScroll, track.width, thumbH), new Color(0.95f, 0.85f, 0.45f, 0.8f));
            }
            int first = dropScroll * cols;
            int shown = Mathf.Min(drops.Count, first + cols * maxRows);
            for (int k = first; k < shown; k++)
            {
                var dr = drops[k];
                float ix = d.x + ((k - first) % cols) * cwid, iy = y + ((k - first) / cols) * rowH;
                var s = new Rect(ix, iy, 40f * ui, 40f * ui);
                Tex(s, "Inventory_Back", Color.white);
                if (!beaten)
                {
                    var lk = TerrariaAssets.Texture("UI/Bestiary/Icon_Locked");
                    Tex(new Rect(s.center.x - lk.width * 0.45f * ui, s.center.y - lk.height * 0.45f * ui, lk.width * 0.9f * ui, lk.height * 0.9f * ui), "UI/Bestiary/Icon_Locked", new Color(1f, 1f, 1f, 0.5f));
                    Outlined(new Rect(ix + 46f * ui, iy + 10f * ui, cwid - 50f * ui, 20f * ui), "？？？", bookSmall, new Color(0.7f, 0.7f, 0.8f));
                    continue;
                }
                // A drop of a stage not unlocked yet: a question mark and when it comes, not the item.
                if (EnemyDrops.Locked(dr))
                {
                    Outlined(s, "?", bookCentre, new Color(0.85f, 0.85f, 0.95f, 0.9f));
                    Outlined(new Rect(ix + 46f * ui, iy, cwid - 50f * ui, 20f * ui), "？？？", bookSmall, new Color(0.7f, 0.7f, 0.8f));
                    Outlined(new Rect(ix + 46f * ui, iy + 20f * ui, cwid - 50f * ui, 20f * ui), Lang.F("阶段 {0} 解锁后掉落", dr.LockStage), bookSmall, new Color(0.65f, 0.65f, 0.75f));
                    if (s.Contains(mouse))
                    {
                        int need = dr.LockStage - Shop.UnlockedStage();
                        BookHint = Lang.F("再获得 {0} 种能力后解锁（通常是{1}）", need, Lang.T(Shop.GateAbility(dr.LockStage)));
                    }
                    continue;
                }
                DrawIconOnly(s, ItemStack.Of(dr.Type));
                // On the save's difficulty (EnemyDrops.EffectiveChance / EffectiveMax).
                int most = EnemyDrops.EffectiveMax(dr);
                string count = dr.Min == most ? (dr.Min > 1 ? $" ×{dr.Min}" : "") : $" ×{dr.Min}-{most}";
                float eff = EnemyDrops.EffectiveChance(dr);
                string chance = eff >= 1f ? Lang.T("必定掉落") : $"{eff * 100f:0.##}%";
                // One of a sure pick: one of the group's unlocked drops comes, at the group's chance.
                int of = EnemyDrops.GroupSize(drops, dr.Group);
                if (of > 0) chance = eff >= 1f ? Lang.F("{0} 选 1 必掉", of) : Lang.F("{0}% 掉 {1} 选 1", (eff * 100f).ToString("0.##"), of);
                if (dr.Master) chance = Lang.T("大师模式必掉");
                var def = ItemDb.Get(dr.Type);
                Outlined(new Rect(ix + 46f * ui, iy, cwid - 50f * ui, 20f * ui), def.Name + count, bookSmall, ItemDb.RarityColor(def.Rare));
                Outlined(new Rect(ix + 46f * ui, iy + 20f * ui, cwid - 50f * ui, 20f * ui), chance, bookSmall, new Color(0.8f, 0.8f, 0.85f));
                if (s.Contains(mouse))
                {
                    hovered = ItemStack.Of(dr.Type);
                    hoveredKind = Kind.BestiaryEntry;
                    hoveredExtra = null;
                }
            }
        }

        /// <summary>A Stat_* panel at any width: its left third (the icon) at the panel's height, the rest of its fill stretched.</summary>
        static void StatPanel(Rect r, string name)
        {
            var tex = TerrariaAssets.Texture(name);
            float k = r.height / tex.height;
            float iconW = 31f * k;
            const float cut = 31f / 96f;
            GUI.DrawTextureWithTexCoords(new Rect(r.x, r.y, iconW, r.height), tex, new Rect(0f, 0f, cut, 1f));
            GUI.DrawTextureWithTexCoords(new Rect(r.x + iconW, r.y, r.width - iconW, r.height), tex, new Rect(cut, 0f, 1f - cut, 1f));
        }
    }
}
