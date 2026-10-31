using System.Collections.Generic;
using TerraKnight.Terraria;
using UnityEngine.InputSystem;
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
    /// with the chance of each, previewed at the selected drop stage independently of the list's grouping.
    /// A creative save's book is all open. The search box at the top finds an enemy by its
    /// name or by an item it drops (what the book shows of it: a seen one's name, a defeated one's unlocked drops).
    /// What the book lists and counts is worked out again only when something it shows changes.
    /// </summary>
    internal static partial class InventoryGui
    {
        static bool bestiaryOpen;
        static int bestiaryScroll, bestiaryFilter = -1;
        static bool bestiaryByStage;
        static int bestiaryStageFilter = -1;
        // Independent of the enemy list's area/stage grouping: -1 follows this save.
        static int bestiaryDropStage = -1;
        static bool bookCatalogue;
        static int journalTab;
        public static bool DevBookCatalogue { get => bookCatalogue; set => SetBookCatalogue(value); }
        static int BookDropStage => bestiaryDropStage < 0 ? LootStage.Current : bestiaryDropStage;
        /// <summary>The chosen entry's drops in the area chosen for it (a kill's there: BestiaryHabitats).</summary>
        static IReadOnlyList<EnemyDrops.Drop> BookDrops(string record) => BookDropsIn(record, HomeScene(record));

        static IReadOnlyList<EnemyDrops.Drop> BookDropsIn(string record, string scene)
        {
            if (bestiaryDropStage < 0) return WithKillRows(record, scene, EnemyDrops.In(record, scene));
            int stage = BookDropStage;
            // A fixed boss reward belongs to its own stage, even though At promotes it.
            if (!ProgressionLoot.GodhomeScene(scene) && ProgressionBalance.FormalBoss(record) && ProgressionBalance.BossRewards.TryGetValue(record, out var boss) && stage < boss.Stage)
                return System.Array.Empty<EnemyDrops.Drop>();
            return WithKillRows(record, scene, EnemyDrops.At(record, stage, Bestiary.KillsOf(record) == 0, stage >= 4, stage >= 5, scene));
        }

        /// <summary>
        /// The drops with what every kill there can bring besides (Fishing.KillRows: the area's bait and moss; a kill with no
        /// drops of its own brings none), made once for a list.
        /// </summary>
        static IReadOnlyList<EnemyDrops.Drop> WithKillRows(string record, string scene, IReadOnlyList<EnemyDrops.Drop> rows)
        {
            if (rows.Count == 0 || EnemyDrops.IsBoss(record)) return rows;
            var key = (rows, scene, BookDropStage);
            if (killRows.TryGetValue(key, out var done)) return done;
            var list = new List<EnemyDrops.Drop>(rows);
            Fishing.KillRows(record, scene, BookDropStage, list);
            if (killRows.Count > 2000) killRows.Clear();
            killRows[key] = list;
            return list;
        }

        static readonly Dictionary<(IReadOnlyList<EnemyDrops.Drop>, string, int), IReadOnlyList<EnemyDrops.Drop>> killRows =
            new Dictionary<(IReadOnlyList<EnemyDrops.Drop>, string, int), IReadOnlyList<EnemyDrops.Drop>>();

        static string bossGeoRec, bossGeoScene;
        static int bossGeoShown, bossGeoStage = -1;
        static World.DifficultyLevel bossGeoDifficulty;

        /// <summary>The boss's fight geo the page shows, worked out again only for another boss, room, difficulty or stage.</summary>
        static int BookBossGeo(string rec, string scene)
        {
            int stage = Shop.UnlockedStage();
            var difficulty = World.GameDifficulty.Current;
            if (rec != bossGeoRec || scene != bossGeoScene || stage != bossGeoStage || difficulty != bossGeoDifficulty)
            {
                bossGeoRec = rec; bossGeoScene = scene; bossGeoStage = stage; bossGeoDifficulty = difficulty;
                bossGeoShown = EnemyDrops.BossGeo(rec, scene);
            }
            return bossGeoShown;
        }

        static string homeFor, homeSearch;
        static int bookHome;
        static int homeDropStage = -2;
        static ulong homeProgression;
        static bool homeCatalogue;

        /// <summary>
        /// Which of the entry's areas its drops are shown for: the one picked by its button; for another entry or search,
        /// the first of its areas (the player's own first) whose drops the search finds when an item is looked for, else the
        /// area the book lists it under.
        /// </summary>
        static int HomeIndex(string rec)
        {
            var homes = BestiaryHabitats.Of(rec);
            if (homes.Count == 0) return -1;
            if (rec != homeFor || bookSearch != homeSearch || homeDropStage != BookDropStage || homeProgression != VanillaBossProgression.StateKey || homeCatalogue != bookCatalogue)
            {
                homeFor = rec;
                homeSearch = bookSearch;
                homeDropStage = BookDropStage;
                homeProgression = VanillaBossProgression.StateKey;
                homeCatalogue = bookCatalogue;
                bookHome = DefaultHome(rec, homes);
            }
            return Mathf.Clamp(bookHome, 0, homes.Count - 1);
        }

        static int DefaultHome(string rec, IReadOnlyList<BestiaryHabitats.Home> homes)
        {
            if (Searching && !Matches(Bestiary.NameOf(rec)))
            {
                int any = -1;
                for (int i = 0; i < homes.Count; i++)
                {
                    if (!HomeMatches(rec, homes[i].Scene)) continue;
                    if (BestiaryHabitats.Visited(homes[i].Area)) return i;
                    if (any < 0) any = i;
                }
                if (any >= 0) return any;
            }
            string listed = BestiaryRoute.AreaOf(rec);
            for (int i = 0; i < homes.Count; i++) if (homes[i].Area == listed) return i;
            // A boss listed under Godhome's area that has a world fight too: the world's.
            for (int i = 0; i < homes.Count; i++) if (!World.Areas.GodhomeRoom(homes[i].Scene)) return i;
            return 0;
        }

        static string HomeScene(string rec)
        {
            int i = HomeIndex(rec);
            return i < 0 ? EnemyDrops.SceneOf(rec) : BestiaryHabitats.Of(rec)[i].Scene;
        }

        /// <summary>Whether a kill in that scene can drop something the search finds (at the stage shown).</summary>
        static bool HomeMatches(string rec, string scene)
        {
            var era = (bookSearch, bestiaryDropStage, BookDropStage, Lang.Culture, World.GameDifficulty.Current,
                ProgressionBalance.EntryClear, ProgressionBalance.FinalClear, EssentialDrops.ManipulatorGiven, inv, Bestiary.Kills.Count, VanillaBossProgression.StateKey, bookCatalogue);
            if (era != homeMatchEra) { homeMatchEra = era; homeMatches.Clear(); }
            string key = rec + "|" + scene;
            if (homeMatches.TryGetValue(key, out bool yes)) return yes;
            if (bookCatalogue)
                foreach (var row in BestiaryCatalogue.Of(rec, scene))
                    if (Matches(ItemDb.Get(row.Type).Name)) { yes = true; break; }
            foreach (var dr in BookDropsIn(rec, scene))
                if (!EnemyDrops.Locked(dr, BookDropStage) && Matches(ItemDb.Get(dr.Type).Name)) { yes = true; break; }
            homeMatches[key] = yes;
            return yes;
        }

        static readonly Dictionary<string, bool> homeMatches = new Dictionary<string, bool>();
        static (string, int, int, string, World.DifficultyLevel, bool, bool, bool, Inventory, int, ulong, bool) homeMatchEra;
        public static int DevBestiaryDropStage => bestiaryDropStage;
        public static IReadOnlyList<EnemyDrops.Drop> DevBestiaryDrops(string record) => BookDrops(record);
        public static IReadOnlyList<EnemyDrops.Drop> DevBookDropsIn(string record, string scene) => BookDropsIn(record, scene);
        public static string DevBookHome(string record) => HomeScene(record);
        public static bool DevBestiarySearchMatches(string record) => MatchesSearch(record);
        static int ActiveBookFilter => bestiaryByStage ? bestiaryStageFilter : bestiaryFilter;
        /// <summary>
        /// The stage a record is listed under: a boss's place in the progression, any other's own stage in the roster (the
        /// roster's sample room can lie in a later area: the tiktik's is the Howling Cliffs, which put it under stage 1).
        /// </summary>
        static int BookStageOf(string record) => ProgressionBalance.BossRewards.TryGetValue(record,out var boss)
            ? boss.Stage : Mathf.Clamp(EnemyDrops.StageOf(record),0,5);
        static int BookGroupOf(string record) => bestiaryByStage ? BookStageOf(record) : BestiaryRoute.Of(record).Area;
        static string BookStageLine()
        {
            int stage = Shop.UnlockedStage(), last = World.Areas.MaxLevel;
            return stage >= last ? Lang.F("当前阶段 {0} / {1}（最终阶段）", stage, last)
                : Lang.F("当前阶段 {0} / {1}，再取得任意 1 种未获得的移动能力进入下一阶段", stage, last);
        }

        static string BookGroupName(int group) => bestiaryByStage ? Lang.F("阶段 {0}",group) : World.Areas.Display(BestiaryRoute.Areas[group]);
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
            bestiaryByStage = false;
            bestiaryScroll = 0;
            bestiaryChosen = all.Count > 0 ? all[Mathf.Clamp(select, 0, all.Count - 1)] : null;
        }

        /// <summary>Dev: open the book on a record (all areas, scrolled to its section).</summary>
        public static void DevBestiaryShow(string record)
        {
            bestiaryOpen = true;
            bestiaryFilter = -1;
            bestiaryStageFilter = -1;
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

        public static void DevBestiarySelect(int index) => SelectBestiary(index);
        public static bool DevBestiaryByStage => bestiaryByStage;
        public static List<string> DevBestiaryFiltered()
        {
            BuildBookLines(AllBookRecords());
            return new List<string>(bestiaryRows);
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
        const float BookSlot = 58f, BookGap = 8f, BookHead = 30f, BookSide = 174f;

        static readonly Color BookBack = new Color(.075f, .09f, .18f, .99f);
        static readonly Color BookHeadColor = new Color(.15f, .19f, .34f, 1f);
        static readonly Color BookRim = new Color(.53f, .59f, .82f, 1f);
        static readonly Color BookInner = new Color(.055f, .07f, .14f, .98f);
        static readonly Color BossColor = new Color(1f, 0.58f, 0.35f), EliteColor = new Color(0.82f, 0.62f, 1f), Gold = new Color(1f, 0.85f, 0.35f);
        static readonly Color CommonColor = new Color(0.7f, 0.85f, 1f);

        static int CategoryOf(string record) => bookCategory.TryGetValue(record, out int c) ? c : Bestiary.CategoryOf(record);

        static Color CategoryColor(int c) => c == 2 ? BossColor : c == 1 ? EliteColor : CommonColor;

        /// <summary>The share of the book's enemies collected (defeated), as its 已收集.</summary>
        internal static float BookCompletion
        {
            get
            {
                var all = AllBookRecords();
                if (all.Count == 0) return 0f;
                int done = 0;
                foreach (var r in all) if (Bestiary.IsDefeated(r)) done++;
                return done / (float)all.Count;
            }
        }

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
            if(bestiaryByStage)
            {
                all=new List<string>(all);
                all.Sort((a,b)=>
                {
                    int c=BookStageOf(a).CompareTo(BookStageOf(b));
                    if(c!=0) return c;
                    c=CategoryOf(a).CompareTo(CategoryOf(b));
                    if(c!=0) return c;
                    var ra=BestiaryRoute.Of(a);var rb=BestiaryRoute.Of(b);
                    c=ra.Area.CompareTo(rb.Area);
                    if(c!=0) return c;
                    return ra.Order!=rb.Order ? ra.Order.CompareTo(rb.Order) : string.CompareOrdinal(a,b);
                });
            }
            int i = 0;
            while (i < all.Count)
            {
                int area = BookGroupOf(all[i]), cat = CategoryOf(all[i]);
                int j = i;
                while (j < all.Count && BookGroupOf(all[j]) == area && CategoryOf(all[j]) == cat) j++;
                if (ActiveBookFilter < 0 || area == ActiveBookFilter || Searching)
                {
                    int first = bestiaryRows.Count, seen = 0;
                    for (int k = i; k < j; k++)
                    {
                        bestiaryRows.Add(all[k]);
                        if (Bestiary.IsSeen(all[k])) seen++;
                    }
                    int n = j - i;
                    string head = (ActiveBookFilter < 0 || Searching ? BookGroupName(area) + " · " : "") + Lang.T(sectionNames[cat]) + $"  {seen}/{n}";
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
            if (bestiaryOpen) journalOpenedAt = Time.unscaledTime;
            if (bestiaryOpen) statsOpen = hkOpen = blacklistOpen = sellMode = false;
            Sound(bestiaryOpen ? "Menu_Open" : "Menu_Close");
        }

        /// <summary>A click in the book: an entry (its index in the rows), or an area (-100 - area, -99 for all).</summary>
        static void SelectBestiary(int i)
        {
            if (i == -4) { if (!bookTyping) StartBookSearch(); return; }
            if (i == -5) { SetBookSearch(""); EndBookSearch(); Sound("Menu_Tick"); return; }
            EndBookSearch();
            if (i >= -76 && i <= -70)
            {
                if (LootStage.Choose(-71 - i))
                {
                    bestiaryDropStage = -1;
                    dropScroll = bestiaryScroll = 0;
                    bookKey = dropTextKey = null;
                    Sound("Menu_Tick");
                }
                return;
            }
            if (i == -60 || i == -61) { SetBookCatalogue(i == -61); Sound("Menu_Tick"); return; }
            if (i >= -65 && i <= -63) { journalTab = -63 - i; dropScroll = 0; Sound("Menu_Tick"); return; }
            // -30 - k: the drops of the entry's k-th area.
            if (i <= -30 && i >= -49)
            {
                int k = -30 - i;
                if (bookHome == k && homeFor == bestiaryChosen) return;
                bookHome = k;
                homeFor = bestiaryChosen;
                homeSearch = bookSearch;
                dropScroll = 0;
                dropTextKey = null;
                Sound("Menu_Tick");
                return;
            }
            // -10 follows the save, -11 through -16 preview stages 0 through 5.
            if (i <= -10 && i >= -16)
            {
                int stage = -11 - i;
                if (bestiaryDropStage == stage) return;
                bestiaryDropStage = stage;
                dropScroll = bestiaryScroll = 0;
                bestiaryScrollTo = null;
                bookKey = dropTextKey = null;
                Sound("Menu_Tick");
                return;
            }
            if(i==-2 || i==-3)
            {
                bool byStage=i==-3;
                if(bestiaryByStage==byStage) return;
                bestiaryByStage=byStage;
                bestiaryScroll=dropScroll=0;
                bestiaryScrollTo=null;
                Sound("Menu_Tick");
                return;
            }
            if (i <= -99)
            {
                int f = -100 - i;
                if (f == ActiveBookFilter) return;
                if(bestiaryByStage) bestiaryStageFilter=f; else bestiaryFilter = f;
                bestiaryScroll = 0;
                dropScroll=0;
                bestiaryScrollTo=null;
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
            BookOutlined(r, Lang.T("怪物图鉴"), labelStyle, over || bestiaryOpen ? new Color(1f, 0.85f, 0.3f) : Color.white);
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
            BookOutlined(new Rect(r.x, r.y, r.width, r.height), text, bookCentre, Color.white);
        }

        /// <summary>How often it has been beaten ("尚未击败" if not; a creative save's book is open without it).</summary>
        static string DefeatsText(string rec) =>
            !Bestiary.IsDefeated(rec) ? (Bestiary.IsSeen(rec) ? Lang.T("尚未击败") : "") : Bestiary.KillsOf(rec) > 0 ? Lang.F("已击败 {0} 次", Bestiary.KillsOf(rec)) : Lang.T("创造模式：已解锁");

        static void DrawBestiary(Rect view, float ui, Vector2 mouse, ref ItemStack hovered, ref string hoveredExtra, ref Kind hoveredKind)
            => DrawJournalBestiary(view, ui, mouse, ref hovered, ref hoveredExtra, ref hoveredKind);

        /// <summary>One entry's slot: the bestiary's slot back, the portrait (or the lock), a star if defeated, the frame, the selection.</summary>
        static void DrawBookSlot(Rect r, string rec, bool chosen, bool over, float ui)
            => DrawJournalSlot(r, rec, chosen, over, ui);

        /// <summary>The right page: portrait, name and kind, danger, stats, description, notes and drops.</summary>
        /// <summary>The note under the cursor on a locked drop (when it unlocks), drawn after the book.</summary>
        public static string BookHint;

        // ------------------------------------------------------------------ worked out once, drawn often

        /// <summary>
        /// Text with Terraria's four-way border (Utils.DrawBorderStringFourWay: the text drawn left, right, above and below
        /// in black, then itself): the book is mostly text, and the inventory's eight-way border cost it twice as much.
        /// </summary>
        static readonly GUIContent bookContent = new GUIContent();
        static void BookOutlined(Rect r, string text, GUIStyle style, Color color)
        {
            if (Event.current.type != EventType.Repaint || string.IsNullOrEmpty(text)) return;
            float o = 1.5f * GameView.Scale;
            bookContent.text = text;
            style.normal.textColor = new Color(0f, 0f, 0f, 0.85f * color.a);
            style.Draw(new Rect(r.x - o, r.y, r.width, r.height), bookContent, false, false, false, false);
            style.Draw(new Rect(r.x + o, r.y, r.width, r.height), bookContent, false, false, false, false);
            style.Draw(new Rect(r.x, r.y - o, r.width, r.height), bookContent, false, false, false, false);
            style.Draw(new Rect(r.x, r.y + o, r.width, r.height), bookContent, false, false, false, false);
            style.normal.textColor = color;
            style.Draw(r, bookContent, false, false, false, false);
        }

        /// <summary>A drop's two lines as the book shows them: its name with how many, its chance, its rarity's colour.</summary>
        struct DropText
        {
            public string Name, Chance;
            public Color Color;
        }

        static string dropTextKey;
        static readonly List<DropText> dropTexts = new List<DropText>();

        static readonly List<int> dropView = new List<int>();

        /// <summary>
        /// Which of an entry's drops the book shows (their places in the whole table, whose groups the chances are worked out
        /// over): all of them, or while it is found by an item it drops, not by its name, only the unlocked drops the search finds.
        /// </summary>
        static List<int> DropView(string rec, IReadOnlyList<EnemyDrops.Drop> drops)
        {
            dropView.Clear();
            var singles=new HashSet<ItemType>();
            bool byItem = Searching && !Matches(Bestiary.NameOf(rec));
            for (int k = 0; k < drops.Count; k++)
                if (!byItem || !EnemyDrops.Locked(drops[k], BookDropStage) && Matches(ItemDb.Get(drops[k].Type).Name)){
                    if(!EnemyDrops.Locked(drops[k],BookDropStage)&&EnemyDrops.SingleQuantity(ItemDb.Get(drops[k].Type))&&!singles.Add(drops[k].Type))continue;
                    dropView.Add(k);
                }
            return dropView;
        }

        /// <summary>The chosen entry's drops as text, worked out again only for another entry, language, difficulty or stage.</summary>
        static List<DropText> DropTexts(string rec, IReadOnlyList<EnemyDrops.Drop> drops)
        {
            var timing = System.Diagnostics.Stopwatch.StartNew();
            string key = rec + "|" + HomeScene(rec) + "|" + Lang.Culture + "|" + World.GameDifficulty.Current + "|" + bestiaryDropStage + "|" + BookDropStage + "|" + drops.Count
                + "|" + EssentialDrops.ManipulatorGiven + "|" + Net.BossLifeRewards.Eligible(rec) + "|" + ProgressionBalance.EntryClear + "|" + ProgressionBalance.FinalClear + "|" + VanillaBossProgression.StateKey;
            if (key == dropTextKey) return dropTexts;
            dropTextKey = key;
            dropTexts.Clear();
            var summary = new EnemyDrops.ChanceSummary(drops, BookDropStage);
            foreach (var dr in drops)
            {
                // On the save's difficulty (EnemyDrops.EffectiveChance / EffectiveMax).
                int most = EnemyDrops.EffectiveMax(dr);
                int least = EnemyDrops.EffectiveMin(dr);
                string count = least == most ? (least > 1 ? $" ×{least}" : "") : $" ×{least}-{most}";
                float eff = EnemyDrops.EffectiveChance(dr);
                string chance = eff >= 1f ? Lang.T("必定掉落") : $"{eff * 100f:0.##}%";
                // One of a sure pick: one of the group's unlocked drops comes, at the group's chance.
                int of = dr.Group>0&&summary.Groups.TryGetValue(dr.Group,out var group) ? group.Count : 0;
                if (of > 0) chance = (summary.Marginal(dr) * 100f).ToString("0.##") + "%";
                if(EnemyDrops.SingleQuantity(ItemDb.Get(dr.Type))){
                    float any=summary.Items.TryGetValue(dr.Type,out float value)?value:0;
                    chance=any>=1f?Lang.T("必定掉落"):(any*100f).ToString("0.##")+"%";
                }
                if (dr.Master) chance = Lang.T("大师模式必掉");
                if(dr.Type==ItemType.LunarCraftingStation)chance=Lang.T("必掉 · 每存档仅一次");
                var def = ItemDb.Get(dr.Type);
                dropTexts.Add(new DropText { Name = def.Name + count, Chance = chance, Color = ItemDb.RarityColor(def.Rare) });
            }
            if(timing.Elapsed.TotalMilliseconds>20)Plugin.Log.LogInfo($"[图鉴掉率] {rec} rows={drops.Count} {timing.Elapsed.TotalMilliseconds:F1}ms");
            return dropTexts;
        }

        /// <summary>Dev: time spent (Stopwatch ticks) on the lists and counts, the grid, the chosen entry; the book's repaints.</summary>
        public static readonly long[] DevBookTicks = new long[3];
        public static int DevBookFrames, DevBookRebuilds;

        static string bookKey;
        static string pendingBookKey;
        static int pendingBookAt, pendingBookFrame = -1;
        static readonly List<string> pendingBookRows = new List<string>();
        static bool BookSearching => pendingBookKey != null;
        internal static bool DevSearchPending => BookSearching;
        static int bookSeen, bookDone;
        static int[] groupTotal = new int[0], groupSeen = new int[0];

        /// <summary>
        /// The book's lines and counts, made again only when what they show changes: the area or stage chosen, the search,
        /// an enemy seen or beaten for the first time, the language, the creative book.
        /// </summary>
        static void RefreshBook(List<string> all, int groups)
        {
            if (groupTotal.Length != groups + 1) { groupTotal = new int[groups + 1]; groupSeen = new int[groups + 1]; }
            string key = ActiveBookFilter + "|" + bestiaryByStage + "|" + bookSearch + "|" + Bestiary.Stats.Count + "|" + Bestiary.Kills.Count
                + "|" + Lang.Culture + "|" + Bestiary.AllUnlocked + "|" + groups + "|" + all.Count + "|" + Shop.UnlockedStage() + "|" + bestiaryDropStage
                + "|" + World.GameDifficulty.Current + "|" + ProgressionBalance.EntryClear + "|" + ProgressionBalance.FinalClear + "|" + EssentialDrops.ManipulatorGiven + "|" + inv.GetHashCode() + "|" + VanillaBossProgression.StateKey + "|" + bookCatalogue;
            if (key == bookKey) return;
            List<string> found = all;
            if (Searching)
            {
                if (key != pendingBookKey)
                {
                    bookKey = null;
                    pendingBookKey = key; pendingBookAt = 0; pendingBookRows.Clear();
                    bestiaryRows.Clear(); bookLines.Clear(); bestiaryChosen = null;
                    System.Array.Clear(groupTotal,0,groupTotal.Length);
                    System.Array.Clear(groupSeen,0,groupSeen.Length);
                }
                // IMGUI can run several times in one frame. Spend the search budget once.
                if (pendingBookFrame == Time.frameCount) return;
                pendingBookFrame = Time.frameCount;
                var budget = System.Diagnostics.Stopwatch.StartNew();
                while(pendingBookAt < all.Count) {
                    if (MatchesSearch(all[pendingBookAt])) pendingBookRows.Add(all[pendingBookAt]);
                    pendingBookAt++;
                    if(budget.Elapsed.TotalMilliseconds >= 2)break;
                }
                if (pendingBookAt < all.Count) return;
                found = pendingBookRows;
            }
            pendingBookKey = null;
            bookKey = key;
            DevBookRebuilds++;
            // Searching: only what it finds, in the grid and in the list down the left (a group it finds nothing in goes).
            BuildBookLines(found);
            if (Searching && bestiaryRows.Count > 0 && !bestiaryRows.Contains(bestiaryChosen)) bestiaryChosen = bestiaryRows[0];
            bookSeen = bookDone = 0;
            foreach (var r in all)
            {
                if (Bestiary.IsSeen(r)) bookSeen++;
                if (Bestiary.IsDefeated(r)) bookDone++;
            }
            if (groupTotal.Length != groups + 1) { groupTotal = new int[groups + 1]; groupSeen = new int[groups + 1]; }
            System.Array.Clear(groupTotal, 0, groupTotal.Length);
            System.Array.Clear(groupSeen, 0, groupSeen.Length);
            foreach (var r in found)
            {
                bool s = Bestiary.IsSeen(r);
                int g = BookGroupOf(r);
                groupTotal[0]++;
                if (s) groupSeen[0]++;
                if (g >= 0 && g + 1 < groupTotal.Length) { groupTotal[g + 1]++; if (s) groupSeen[g + 1]++; }
            }
        }

        static string textFor, textCulture, descCached, notesCached;
        static bool textSeen, textBeaten;
        static float textWidth = -1f, hDescCached, hNotesCached;
        static int textFont;

        /// <summary>The chosen entry's description and notes, and their heights at this width (measured once for each).</summary>
        static void EntryText(string rec, bool seen, bool beaten, float width, out string desc, out string notes, out float hDesc, out float hNotes)
        {
            if (rec != textFor || seen != textSeen || beaten != textBeaten || Lang.Culture != textCulture)
            {
                textFor = rec; textSeen = seen; textBeaten = beaten; textCulture = Lang.Culture;
                descCached = seen ? EnemyHover.JournalText(rec, "DESC_") ?? Lang.T("猎人日志里没有这种敌人的描述。") : Lang.T("在视野中发现这种敌人后解锁它的资料。");
                notesCached = beaten ? EnemyHover.JournalText(rec, "NOTE_") : null;
                textWidth = -1f;
            }
            if (!Mathf.Approximately(width, textWidth) || bookText.fontSize != textFont)
            {
                textWidth = width;
                textFont = bookText.fontSize;
                hDescCached = bookText.CalcHeight(new GUIContent(descCached), width);
                hNotesCached = notesCached != null ? bookText.CalcHeight(new GUIContent(notesCached), width) : 0f;
            }
            desc = descCached; notes = notesCached; hDesc = hDescCached; hNotes = hNotesCached;
        }

        // ------------------------------------------------------------------ the search (UIBestiaryTest's search bar)

        static string bookSearch = "", bookComposing = "";
        static bool bookTyping;
        static readonly UI.ImeTextEntry bookInput = new UI.ImeTextEntry(BookAppend);
        static int bookTypingEnded = -10;

        /// <summary>Something is searched for.</summary>
        static bool Searching => SearchQuery.Length > 0;
        static string trimmedSearchFor, trimmedSearch = "";
        static string SearchQuery { get { if(trimmedSearchFor != bookSearch) { trimmedSearchFor=bookSearch; trimmedSearch=bookSearch.Trim(); } return trimmedSearch; } }

        /// <summary>The search box is being typed into (or was this frame or the last): the keys are the text's.</summary>
        internal static bool BookTyping => bookTyping || Time.frameCount - bookTypingEnded <= 1;

        /// <summary>Dev: what is searched for.</summary>
        public static string DevBookSearch
        {
            get => bookSearch;
            set { SetBookSearch(value ?? ""); }
        }

        // A new query must not inherit the previous query's stage or area restriction.
        static void ResetBookSearchFilters()
        {
            bestiaryFilter = bestiaryStageFilter = bestiaryDropStage = -1;
            bestiaryScroll = dropScroll = 0;
            bestiaryScrollTo = null;
            bookKey = dropTextKey = null;
        }

        static void SetBookSearch(string value)
        {
            if (bookSearch == value) return;
            bookSearch = value;
            ResetBookSearchFilters();
        }

        public static List<string> DevBookRows() => new List<string>(bestiaryRows);

        /// <summary>Dev: start or end typing into the search, as a click on the box and Enter do.</summary>
        public static void DevBookType(bool on) { if (on) StartBookSearch(); else EndBookSearch(); }

        // Pinyin too: "tie" finds 铁, "tdj" finds 天顶剑, and a Chinese query still matches the name literally.
        static bool Matches(string text) => text != null && UI.PinyinSearch.Matches(text, SearchQuery);

        /// <summary>An enemy the search finds: a seen one by its name, a defeated one by a drop the book shows (not a locked one).</summary>
        static bool MatchesSearch(string rec)
        {
            if (bookCatalogue)
            {
                if (Matches(Bestiary.NameOf(rec))) return true;
                foreach (var home in BestiaryHabitats.Of(rec)) if (HomeMatches(rec, home.Scene)) return true;
                return false;
            }
            if (!Bestiary.IsSeen(rec)) return false;
            if (Matches(Bestiary.NameOf(rec))) return true;
            if (!Bestiary.IsDefeated(rec)) return false;
            foreach (var home in BestiaryHabitats.Of(rec))
                if (HomeMatches(rec, home.Scene)) return true;
            return false;
        }

        static void StartBookSearch()
        {
            EndRename(false);
            bookTyping = true;
            bookInput.Begin();
            Sound("Menu_Tick");
        }

        static void BookAppend(char c)
        {
            if (!bookTyping || char.IsControl(c) || bookSearch.Length >= 32) return;
            SetBookSearch(bookSearch + c);
        }

        static void EndBookSearch()
        {
            if (!bookTyping) return;
            bookTyping = false;
            bookComposing = "";
            bookTypingEnded = Time.frameCount;
            bookInput.End();
        }

        /// <summary>Dev: the committed-text backend used by the active search.</summary>
        public static string DevBookIme => bookInput.Backend;

        /// <summary>Each frame while typed into: Backspace; Enter or Esc ends the typing (the text stays); the book shut ends it.</summary>
        static void TickBookSearch()
        {
            if (!bookTyping) return;
            if (!Open || !bestiaryOpen) { EndBookSearch(); return; }
            bookInput.Tick();
            bookComposing = bookInput.Composition;
            var kb = Keyboard.current;
            if (kb == null || bookInput.EditingComposition) return;
            if (bookComposing.Length == 0)
            {
                string left = bookInput.TakeBackspaces(bookSearch);
                if (left != bookSearch) SetBookSearch(left);
            }
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame) EndBookSearch();
        }

        /// <summary>The search box: what is typed (with what the input method is composing, and the caret), a hint when empty, a × to clear it.</summary>
        static void DrawBookSearch(Rect box, float ui, Vector2 mouse)
        {
            if (box.width < 80f * ui) return;
            bool over = box.Contains(mouse);
            Fill(box, bookTyping ? new Color(0.05f, 0.07f, 0.18f, 0.95f) : over ? new Color(0.1f, 0.13f, 0.3f, 0.95f) : new Color(0f, 0f, 0f, 0.35f));
            Rim(box, bookTyping ? Gold : BookRim, 1.5f * ui);
            var text = new Rect(box.x + 10f * ui, box.y, box.width - 40f * ui, box.height);
            if (bookSearch.Length == 0 && bookComposing.Length == 0 && !bookTyping)
                BookOutlined(text, Lang.T("搜索怪物或掉落物（支持拼音）"), bookSmall, new Color(0.6f, 0.65f, 0.8f));
            else
            {
                string caret = bookTyping && (int)(Time.unscaledTime * 2f) % 2 == 0 ? "|" : "";
                BookOutlined(text, bookSearch + bookComposing + caret, bookSmall, Color.white);
                // The input method's word list under the box, at the caret (as a text field puts it).
                if (bookTyping)
                {
                    float w = bookSmall.CalcSize(new GUIContent(bookSearch)).x;
                    bookInput.Anchor(new Vector2(text.x + Mathf.Min(w, text.width), box.yMax));
                }
            }
            Add(box, Kind.BestiaryEntry, -4);
            if (bookSearch.Length > 0)
            {
                var clear = new Rect(box.xMax - 28f * ui, box.y + 3f * ui, 24f * ui, box.height - 6f * ui);
                BookOutlined(clear, "×", bookCentre, clear.Contains(mouse) ? Gold : Color.white);
                Add(clear, Kind.BestiaryEntry, -5);
            }
            // How many it found, under the box.
            if (Searching)
                BookOutlined(new Rect(box.x, box.yMax + 2f * ui, box.width, 18f * ui),
                    BookSearching ? Lang.T("搜索中…") : bestiaryRows.Count > 0 ? Lang.F("找到 {0} 种", bestiaryRows.Count) : Lang.T("没有找到"), bookCount, new Color(0.8f, 0.85f, 1f));
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
