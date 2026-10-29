using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using TerraKnight.Terraria;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>
    /// The start menu after the title screen, as Terraria's: 开始游戏 opens Terraria's world list (UIWorldSelect) over the
    /// title's backdrop in place of the game's save profiles, every save of the fifty on one scrolling list (favourites
    /// first, then the latest played), each with its character, names, mode, difficulty, enemy AI and the game's own stats
    /// of it, and Terraria's play, favourite, rename and delete buttons. 新建 goes to Terraria's character creation
    /// (<see cref="CharacterCreation"/>), then to its world creation (name, the five difficulties, the enemy AI, the
    /// guidance), and 创建 starts the game in the first free slot. 返回 goes back to the title as the game's own back does.
    /// The game's menu takes no input while these are up.
    /// </summary>
    [HarmonyPatch]
    internal static partial class TerraMenu
    {
        enum Step { Closed, Opening, List, Character, World }

        static Step step;
        /// <summary>The play style a new game was asked with from outside (the game's own new game: Steel Soul, Godseeker).</summary>
        static PlayStyle askStyle;
        static string notice;
        static float noticeUntil;
        /// <summary>A save being cleared: the list waits for it.</summary>
        static bool busy;
        static readonly Dictionary<string, Rect> devRects = new Dictionary<string, Rect>();

        /// <summary>One of the start menu's screens is up (or coming up).</summary>
        public static bool IsOpen => step != Step.Closed;

        /// <summary>The world list is up with nothing over it (the multiplayer menu's button shows on it).</summary>
        public static bool ListShown => step == Step.List && deleting == null;

        /// <summary>The game's records unlock Steel Soul (RecPermadeathMode) and Godseeker (RecBossRushMode), as its play mode menu checks.</summary>
        internal static bool SteelSoulUnlocked => Record("RecPermadeathMode");
        internal static bool GodseekerUnlocked => Record("RecBossRushMode");

        static bool Record(string key)
        {
            try
            {
                var gm = GameManager.UnsafeInstance;
                return gm != null && gm.GetStatusRecordInt(key) == 1;
            }
            catch (System.Exception) { return false; }
        }

        /// <summary>A line in the language being played: Terraria's by key when it has one, else the mod's.</summary>
        static string TT(string key, string zh) => TerrariaText.Get(Lang.Culture, key) ?? Lang.T(zh);

        static void Sound(string name) => TerraUi.Sound(name);

        // ------------------------------------------------------------------ the game's menu

        /// <summary>开始游戏 (and every way back to the save profiles) opens the world list instead.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(UIManager), nameof(UIManager.UIGoToProfileMenu))]
        static bool ShowWorlds(UIManager __instance)
        {
            if (!CreativeMenu.InMainMenu()) return true;
            Open(__instance, Step.List);
            return false;
        }

        /// <summary>
        /// A new game from the menu goes through the creation screens first: one started by them passes straight on (again
        /// after the game's first-run screen and brightness settings), one from anywhere else opens them.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(UIManager), nameof(UIManager.StartNewGame))]
        static bool CreateFirst(UIManager __instance, bool permaDeath, bool bossRush)
        {
            if (pending != null || !CreativeMenu.InMainMenu()) return true;
            askStyle = bossRush ? PlayStyle.Godseeker : permaDeath ? PlayStyle.SteelSoul : PlayStyle.Classic;
            CreativeMenu.DevAsked++;
            Open(__instance, Step.Character);
            return false;
        }

        static void Open(UIManager ui, Step to)
        {
            if (ui == null) return;
            deleting = null;
            EndRename(false);
            EndNameTyping();
            step = Step.Opening;
            ui.StartCoroutine(HideGameMenu(ui, to));
        }

        /// <summary>The title and its menu fade as the game's own way to the profiles does; its input stays off while ours is up.</summary>
        static IEnumerator HideGameMenu(UIManager ui, Step to)
        {
            var ih = GameManager.instance != null ? GameManager.instance.inputHandler : null;
            ih?.StopUIInput();
            if (ui.menuState == GlobalEnums.MainMenuState.MAIN_MENU)
            {
                if (ui.gameTitle != null) ui.StartCoroutine(FadeTitle(ui.gameTitle));
                if (ui.subtitleFSM != null) ui.subtitleFSM.SendEvent("FADE OUT");
                if (ui.mainMenuScreen != null && ui.mainMenuScreen.gameObject.activeSelf) yield return ui.StartCoroutine(ui.FadeOutCanvasGroup(ui.mainMenuScreen));
            }
            else yield return ui.StartCoroutine(ui.HideCurrentMenu());
            // Back at the title from here is the game's own way from its main menu (UIGoToMainMenu fades it all in again).
            ui.menuState = GlobalEnums.MainMenuState.MAIN_MENU;
            ih?.StopUIInput();
            if (step != Step.Opening) yield break;
            Refresh();
            scroll = 0f;
            if (to == Step.Character) StartCreation(askStyle);
            else step = Step.List;
            askStyle = PlayStyle.Classic;
            Sound("Menu_Open");
        }

        static IEnumerator FadeTitle(SpriteRenderer title)
        {
            while (title != null && title.color.a > 0f)
            {
                var c = title.color;
                title.color = new Color(c.r, c.g, c.b, Mathf.Max(0f, c.a - Time.unscaledDeltaTime * 3.2f));
                yield return null;
            }
        }

        static void Close()
        {
            step = Step.Closed;
            deleting = null;
            EndRename(false);
            EndNameTyping();
            CharacterCreation.Close();
        }

        /// <summary>返回 from the world list: the title screen, as the game's back from its profiles.</summary>
        static void BackToTitle()
        {
            Close();
            Sound("Menu_Close");
            UIManager.instance?.UIGoToMainMenu();
        }

        // ------------------------------------------------------------------ each frame

        /// <summary>From CreativeMenu.Draw (TerraMod.OnGUI) while at the title.</summary>
        public static void Draw()
        {
            if (step == Step.Closed) return;
            if (!CreativeMenu.InMainMenu())
            {
                Close();
                return;
            }
            if (step == Step.Opening) return;
            TickTyping();
            if (step == Step.List) PumpStats();
            // The multiplayer menu over the list takes the clicks and keys.
            TerraUi.Input = !Net.NetMenu.IsOpen && !busy;
            try
            {
                CreativeMenu.Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.35f));
                switch (step)
                {
                    case Step.List:
                        if (deleting != null) DrawDelete();
                        else DrawList();
                        break;
                    case Step.Character:
                        DrawCharacter();
                        break;
                    case Step.World:
                        DrawWorld();
                        break;
                }
                DrawNotice();
            }
            finally
            {
                TerraUi.Input = true;
            }
            TerraUi.EndFrame();
        }

        /// <summary>Esc pressed (used up): the screen's back, unless a name is being typed.</summary>
        static bool Escaped()
        {
            var e = Event.current;
            if (!TerraUi.Input || Typing || e.type != EventType.KeyDown || e.keyCode != KeyCode.Escape) return false;
            e.Use();
            return true;
        }

        static void Notice(string s)
        {
            notice = s;
            noticeUntil = Time.unscaledTime + 4f;
            Sound("Menu_Close");
        }

        static void DrawNotice()
        {
            if (notice == null || Time.unscaledTime > noticeUntil) return;
            float s = TerraUi.S;
            TerraUi.Text(new Rect(0f, Screen.height - 150f * s, Screen.width, 40f * s), notice, TerraUi.Normal * s, new Color(1f, 0.6f, 0.5f), TextAnchor.MiddleCenter);
        }

        // ------------------------------------------------------------------ the world list

        static float scroll, grab;
        static bool dragging;
        /// <summary>The save asked about in Terraria's delete question (the legacy menu's "删除 … ?").</summary>
        static Row deleting;

        static void DrawList()
        {
            float s = TerraUi.S;
            float w = Mathf.Min(Screen.width * 0.8f, 650f * s);
            var box = new Rect((Screen.width - w) / 2f, 220f * s, w, Screen.height - 220f * s);
            var panel = new Rect(box.x, box.y, w, box.height - 110f * s);
            TerraUi.Panel(panel, TerraUi.ListBack, Color.black);
            var inner = new Rect(panel.x + 12f * s, panel.y + 12f * s, panel.width - 24f * s, panel.height - 24f * s);
            float rowH = 96f * s, gap = 5f * s;
            float total = rows.Count > 0 ? rows.Count * (rowH + gap) - gap : 0f;
            bool bar = total > inner.height;
            var area = new Rect(inner.x, inner.y, inner.width - (bar ? 25f * s : 0f), inner.height);
            var e = Event.current;
            // A click anywhere but the name being typed keeps the new name.
            if (renaming != 0 && e.type == EventType.MouseDown && !renameRect.Contains(e.mousePosition)) EndRename(true);
            if (e.type == EventType.ScrollWheel && TerraUi.Input && panel.Contains(e.mousePosition))
            {
                scroll += e.delta.y * 33f * s;
                e.Use();
            }
            scroll = Mathf.Clamp(scroll, 0f, Mathf.Max(0f, total - area.height));
            if (bar) scroll = TerraUi.Scrollbar(new Rect(inner.xMax - 20f * s, inner.y, 20f * s, inner.height), scroll, area.height, total, ref dragging, ref grab);
            else dragging = false;
            TerraUi.BeginClip(area);
            try
            {
                for (int i = 0; i < rows.Count; i++)
                {
                    float y = i * (rowH + gap) - scroll;
                    if (y + rowH < 0f || y > area.height) continue;
                    var r = new Rect(0f, y, area.width, rowH);
                    devRects["slot" + rows[i].Slot] = new Rect(r.x + area.x, r.y + area.y, r.width, r.height);
                    DrawRow(r, rows[i]);
                }
            }
            finally { TerraUi.EndClip(); }
            // Drawn over the list's top, as UIWorldSelect appends it after the panel.
            TerraUi.Header(box.center.x, box.y - 40f * s, TT("UI.SelectWorld", "选择世界"));
            float bw = w / 2f - 10f * s, by = box.yMax - 95f * s;
            var back = new Rect(box.x, by, bw, 50f * s);
            var create = new Rect(box.xMax - bw, by, bw, 50f * s);
            devRects["creative"] = create;
            if (TerraUi.BigButton(back, TT("UI.Back", "返回"), "back") || Escaped())
            {
                BackToTitle();
                return;
            }
            if (TerraUi.BigButton(create, TT("UI.New", "新建"), "new"))
            {
                Sound("Menu_Open");
                StartCreation(PlayStyle.Classic);
            }
        }

        /// <summary>UICharacterListItem / UIWorldListItem: the portrait, the names, the info boxes, the buttons along the bottom.</summary>
        static void DrawRow(Rect r, Row row)
        {
            float s = TerraUi.S;
            bool over = TerraUi.Over(r);
            TerraUi.Panel(r, over ? TerraUi.PanelHover : TerraUi.PanelBack, over ? TerraUi.RowBorderHover : TerraUi.RowBorder);
            var inner = new Rect(r.x + 6f * s, r.y + 6f * s, r.width - 12f * s, r.height - 12f * s);
            var back = TerraUi.Tex("UI/PlayerBackground");
            var portrait = new Rect(inner.x + 4f * s, inner.y, (back != null ? back.width : 59) * s, (back != null ? back.height : 58) * s);
            TerraUi.Whole(portrait, back, Color.white);
            CharacterPortrait.Draw(portrait, row.Head?.Look ?? Player.Appearance.Default(), 0, true, s);
            float nx = portrait.xMax;

            // The names: the world's in white (grey when it can't be played), the character's on the right.
            bool editing = renaming == row.Slot;
            string name = editing ? renameDraft + renameInput.Composition + Caret() : RowName(row);
            var nameRect = new Rect(nx + 6f * s, inner.y - 2f * s, inner.xMax - nx - 6f * s, 24f * s);
            TerraUi.Text(nameRect, name, TerraUi.Normal * s, row.Dead || row.StatsFailed ? Color.gray : editing ? TerraUi.FatButtonHover : Color.white, TextAnchor.UpperLeft);
            if (editing) renameInput.Anchor(new Vector2(nameRect.x + TerraUi.Measure(name, TerraUi.Normal * s).x, nameRect.yMax) + TerraUiOrigin());
            string who = row.Head?.PlayerName;
            if (!string.IsNullOrEmpty(who) && !editing) TerraUi.Text(nameRect, who, TerraUi.Small * s, new Color(0.78f, 0.82f, 0.95f), TextAnchor.UpperRight);
            var divider = TerraUi.Tex("UI/Divider");
            if (divider != null) TerraUi.Whole(new Rect(nx, inner.y + 21f * s, r.xMax - nx, divider.height * s), divider, Color.white);

            // The info boxes: play mode, difficulty, enemy AI, weapons, where it was saved, play time and completion.
            float x = nx + 6f * s, y = inner.y + 29f * s, h = TerraUi.InfoHeight, step5 = 5f * s;
            var mode = ModeLine(row);
            var diff = row.Head?.Difficulty ?? DifficultyLevel.Master;
            var ai = row.Head?.EnemyAi ?? EnemyAiKind.Vanilla;
            var rowWeapons = row.Head?.Weapons ?? WeaponKind.Vanilla;
            float wMode = 84f * s, wDiff = 64f * s, wAi = 96f * s, wWeapons = 96f * s, wTime = 118f * s;
            float wArea = inner.xMax - x - wMode - wDiff - wAi - wWeapons - wTime - 5f * step5;
            TerraUi.InfoBox(new Rect(x, y, wMode, h), mode.Text, mode.Color);
            x += wMode + step5;
            TerraUi.InfoBox(new Rect(x, y, wDiff, h), GameDifficulty.NameOf(diff), GameDifficulty.ColorOf(diff));
            x += wDiff + step5;
            TerraUi.InfoBox(new Rect(x, y, wAi, h), ai == EnemyAiKind.New ? Lang.T("新版AI") : Lang.T("原版AI"), ai == EnemyAiKind.New ? AiNewColor : Color.white);
            x += wAi + step5;
            TerraUi.InfoBox(new Rect(x, y, wWeapons, h), WeaponMode.NameOf(rowWeapons), rowWeapons == WeaponKind.New ? WeaponsNewColor : Color.white);
            x += wWeapons + step5;
            if (wArea >= 48f * s)
            {
                TerraUi.InfoBox(new Rect(x, y, wArea, h), AreaLine(row), Color.white);
                x += wArea + step5;
            }
            TerraUi.InfoBox(new Rect(x, y, inner.xMax - x, h), TimeLine(row), Color.white);

            // The buttons under the portrait (play, favourite, rename) and delete on the right, with what each does beside them.
            string label = null, deleteLabel = null;
            float bottom = inner.yMax, bx = inner.x + 4f * s;
            var play = TerraUi.Tex("UI/ButtonPlay");
            var pr = Bottom(play, bx, bottom);
            if (row.Dead) TerraUi.Whole(pr, play, new Color(1f, 1f, 1f, 0.15f));
            else
            {
                if (TerraUi.ImageButton(pr, play, "play" + row.Slot)) Play(row);
                if (TerraUi.Over(pr)) label = TT("UI.Play", "开始游戏");
            }
            bx += 24f * s;
            var fav = TerraUi.Tex(row.Favorite ? "UI/ButtonFavoriteActive" : "UI/ButtonFavoriteInactive");
            var fr = Bottom(fav, bx, bottom);
            if (TerraUi.ImageButton(fr, fav, "fav" + row.Slot, row.Favorite ? 0.8f : 0.4f)) ToggleFavorite(row);
            if (TerraUi.Over(fr)) label = row.Favorite ? TT("UI.Unfavorite", "取消收藏") : TT("UI.Favorite", "收藏");
            bx += 24f * s;
            var ren = TerraUi.Tex("UI/ButtonRename");
            var rr = Bottom(ren, bx, bottom);
            if (TerraUi.ImageButton(rr, ren, "ren" + row.Slot)) BeginRename(row);
            if (TerraUi.Over(rr)) label = TT("UI.Rename", "重命名");
            bx += 24f * s;
            var del = TerraUi.Tex("UI/ButtonDelete");
            var dr = Bottom(del, inner.xMax - (del != null ? del.width : 22) * s, bottom);
            if (TerraUi.ImageButton(dr, del, "del" + row.Slot) && !row.Favorite)
            {
                Sound("Menu_Open");
                EndRename(true);
                deleting = row;
            }
            if (TerraUi.Over(dr)) deleteLabel = row.Favorite ? TT("UI.CannotDeleteFavorited", "无法删除（已收藏）") : TT("UI.Delete", "删除");
            if (label != null) TerraUi.Text(new Rect(bx + 4f * s, bottom - 27f * s, inner.width, 24f * s), label, TerraUi.Normal * s, Color.white, TextAnchor.LowerLeft);
            if (deleteLabel != null) TerraUi.Text(new Rect(inner.x, bottom - 27f * s, dr.x - inner.x - 6f * s, 24f * s), deleteLabel, TerraUi.Normal * s, Color.white, TextAnchor.LowerRight);

            // A double click on the row plays it, as Terraria's lists do.
            if (editing) renameRect = new Rect(nameRect.position + TerraUiOrigin(), nameRect.size);
            if (!row.Dead && TerraUi.DoubleClicked(r, "row" + row.Slot)) Play(row);
        }

        static readonly Color AiNewColor = new Color(1f, 0.55f, 0.48f);
        static readonly Color WeaponsNewColor = new Color(1f, 0.84f, 0.45f);
        /// <summary>Where the name being renamed is drawn (screen coordinates).</summary>
        static Rect renameRect;

        static Rect Bottom(Texture2D t, float x, float bottom)
        {
            float s = TerraUi.S;
            float w = (t != null ? t.width : 22) * s, h = (t != null ? t.height : 22) * s;
            return new Rect(x, bottom - h, w, h);
        }

        /// <summary>Where the list is clipped from, for the IME's caret (screen coordinates).</summary>
        static Vector2 TerraUiOrigin() => TerraUi.ClipOrigin;

        static string Caret() => (int)(Time.unscaledTime * 2f) % 2 == 0 ? "|" : " ";

        static (string Text, Color Color) ModeLine(Row row)
        {
            var st = row.Stats;
            if (st != null && st.bossRushMode) return (Lang.T("寻神者"), new Color(1f, 0.85f, 0.4f));
            if (st != null && st.permadeathMode == 2) return (Lang.T("已陨落"), Color.gray);
            if (st != null && st.permadeathMode == 1) return (Lang.T("钢铁之魂"), TerraUi.HardcoreColor);
            if (row.Head != null && row.Head.Creative) return (Lang.T("创造模式"), TerraUi.CreativeColor);
            return (TT("UI.Softcore", "经典"), Color.white);
        }

        static string AreaLine(Row row)
        {
            if (row.StatsFailed) return Lang.T("读不了");
            if (row.Stats == null) return "…";
            try { return GameManager.instance.GetFormattedMapZoneString(row.Stats.mapZone).Replace("<br>", " ").Replace("\n", " "); }
            catch (System.Exception) { return "?"; }
        }

        static string TimeLine(Row row)
        {
            var st = row.Stats;
            if (st == null) return row.StatsFailed ? "-" : "…";
            string t = st.GetPlaytimeHHMM();
            return st.unlockedCompletionRate && !st.bossRushMode ? t + "  " + st.completionPercentage + "%" : t;
        }

        /// <summary>Terraria's delete question (menu 9): "删除 name?", 是, 否, in the legacy menu's big text.</summary>
        static void DrawDelete()
        {
            float s = TerraUi.S;
            float y = Screen.height * 0.32f;
            Legacy(TT("LegacyMenu.46", "删除") + " " + RowName(deleting) + "?", y, false, null);
            devRects["confirm"] = LegacyRect(TT("LegacyMenu.104", "是"), y + 80f * s);
            if (Legacy(TT("LegacyMenu.104", "是"), y + 80f * s, true, "yes"))
            {
                var row = deleting;
                deleting = null;
                Sound("Menu_Open");
                Delete(row);
                return;
            }
            if (Legacy(TT("LegacyMenu.105", "否"), y + 140f * s, true, "no") || Escaped())
            {
                deleting = null;
                Sound("Menu_Close");
            }
        }

        const float LegacySize = 44f;

        static Rect LegacyRect(string text, float y)
        {
            var m = TerraUi.Measure(text, LegacySize * TerraUi.S);
            return new Rect((Screen.width - m.x) / 2f, y, m.x, m.y);
        }

        /// <summary>A line of Terraria's old text menus: grey-white, gold and a little bigger under the mouse.</summary>
        static bool Legacy(string text, float y, bool button, string id)
        {
            var r = LegacyRect(text, y);
            bool over = button && TerraUi.Hover(r, id);
            float size = LegacySize * TerraUi.S * (over ? 1.1f : 1f);
            var m = TerraUi.Measure(text, size);
            var shown = new Rect(r.center.x - m.x / 2f, r.center.y - m.y / 2f, m.x, m.y);
            TerraUi.Text(shown, text, size, over ? TerraUi.FatButtonHover : new Color(0.9f, 0.9f, 0.9f), TextAnchor.MiddleCenter);
            return button && TerraUi.Clicked(r);
        }

        /// <summary>A save played: the game's own load of its slot (SaveSlotButton.OnSubmit).</summary>
        static void Play(Row row)
        {
            var gm = GameManager.instance;
            if (gm == null || row.Dead) return;
            EndRename(true);
            pending = null;
            Plugin.Log.LogInfo($"[开始菜单] 进入存档 {row.Slot}「{RowName(row)}」");
            Close();
            try { global::Platform.Current.LocalSharedData.SetInt("lastProfileIndex", row.Slot); }
            catch (System.Exception) { }
            gm.LoadGameFromUI(row.Slot);
        }

        // ------------------------------------------------------------------ the character and world creation

        /// <summary>What a new game started by 创建 is given when it begins (GameHooks' StartNewGame, <see cref="ApplyNewGame"/>).</summary>
        sealed class Pending
        {
            public Player.Appearance Look;
            public string WorldName, PlayerName;
            public EnemyAiKind Ai;
            public WeaponKind Weapons;
        }

        static Pending pending;

        /// <summary>新建: the character creation screen, then the world's (its options as Terraria's defaults).</summary>
        static void StartCreation(PlayStyle style)
        {
            EndRename(true);
            pending = null;
            CharacterCreation.Open(Player.Appearance.Default(), "");
            CharacterCreation.Style = style;
            ResetWorldOptions();
            step = Step.Character;
        }

        static void DrawCharacter()
        {
            if (!CharacterCreation.IsOpen)
            {
                step = Step.List;
                return;
            }
            var result = CharacterCreation.Draw();
            if (result == CharacterCreation.Result.Back)
            {
                Sound("Menu_Close");
                Refresh();
                step = Step.List;
            }
            else if (result == CharacterCreation.Result.Done)
            {
                Sound("Menu_Open");
                step = Step.World;
            }
        }

        /// <summary>From the world creation's 返回 or its character plate: the character screen again, as it was left.</summary>
        static void BackToCharacter()
        {
            EndNameTyping();
            var look = CharacterCreation.Look;
            string name = CharacterCreation.Name;
            var style = CharacterCreation.Style;
            CharacterCreation.Open(look, name);
            CharacterCreation.Style = style;
            step = Step.Character;
        }

        /// <summary>创建: the first free slot, the choices kept for the game's start, and the game's new game in it.</summary>
        static void Create()
        {
            EndNameTyping();
            int slot = FirstFreeSlot();
            if (slot == 0)
            {
                Notice(Lang.F("存档已满（最多 {0} 个世界）：先删除一个再新建。", SaveSlots.Max));
                return;
            }
            var gm = GameManager.instance;
            var ui = UIManager.instance;
            if (gm == null || ui == null) return;
            var style = CharacterCreation.Style;
            if (style == PlayStyle.SteelSoul && !SteelSoulUnlocked || style == PlayStyle.Godseeker && !GodseekerUnlocked) style = PlayStyle.Classic;
            string world = (worldName ?? "").Trim();
            if (world.Length == 0) world = RandomWorldName();
            pending = new Pending
            {
                Look = (CharacterCreation.Look ?? Player.Appearance.Default()).Clone(),
                PlayerName = (CharacterCreation.Name ?? "").Trim(),
                WorldName = world,
                Ai = ai,
                Weapons = weapons,
            };
            CreativeMenu.SetPending(style == PlayStyle.Journey ? slot : 0, difficulty, guide);
            CreativeMenu.DevStarted++;
            Plugin.Log.LogInfo($"[开始菜单] 新世界「{world}」在存档 {slot}：人物「{pending.PlayerName}」，{style}，难度{GameDifficulty.NameOf(difficulty)}，敌人AI {EnemyAiMode.NameOf(ai)}，{WeaponMode.NameOf(weapons)}，神的指引{(guide ? "听从" : "拒绝")}");
            // A mod save left by a game save deleted outside the game would be read as this one's.
            Save.ModSave.Delete(slot);
            Close();
            gm.profileID = slot;
            try { global::Platform.Current.LocalSharedData.SetInt("lastProfileIndex", slot); }
            catch (System.Exception) { }
            ui.StartNewGame(style == PlayStyle.SteelSoul, style == PlayStyle.Godseeker);
        }

        /// <summary>GameHooks' StartNewGame: the enemy AI, the weapons, the look and the names chosen (none: Hollow Knight's AI, Terraria's own weapons, Terraria's default character).</summary>
        public static void ApplyNewGame()
        {
            var p = pending;
            pending = null;
            EnemyAiMode.Current = p?.Ai ?? EnemyAiKind.Vanilla;
            WeaponMode.Current = p?.Weapons ?? WeaponKind.Vanilla;
            Player.Appearance.Current = p?.Look ?? Player.Appearance.Default();
            Save.ModSave.WorldName = p?.WorldName ?? "";
            Save.ModSave.PlayerName = p?.PlayerName ?? "";
            if (p != null) Plugin.Log.LogInfo($"[新存档] 世界「{p.WorldName}」，人物「{p.PlayerName}」，敌人AI {EnemyAiMode.NameOf(p.Ai)}，{WeaponMode.NameOf(p.Weapons)}");
        }

        // ------------------------------------------------------------------ dev

        /// <summary>Dev: the old creative panel's steps on this menu (the list opened, a slot taken, a difficulty picked starts the game).</summary>
        public static void DevOpenCreative()
        {
            devCreative = true;
            if (step == Step.Closed) Open(UIManager.instance, Step.List);
        }

        public static void DevChoose(int slot) => devSlot = slot;

        /// <summary>Dev (suite weaponmode): from the character creation on to the world's (its options reset), a weapons choice on it, its 创建.</summary>
        public static void DevToWorld()
        {
            if (step != Step.Character) return;
            ResetWorldOptions();
            step = Step.World;
        }

        public static void DevWeapons(WeaponKind k) => weapons = k;
        public static void DevCreate() => Create();

        /// <summary>Dev: a new game started at once at this difficulty (creative when the creative panel was asked for), no guidance, Hollow Knight's AI, the remade weapons.</summary>
        public static void DevPick(DifficultyLevel d)
        {
            var gm = GameManager.instance;
            var ui = UIManager.instance;
            if (gm == null || ui == null) return;
            int slot = devSlot > 0 ? devSlot : gm.profileID > 0 ? gm.profileID : FirstFreeSlot();
            bool creative = devCreative;
            var style = askStyle;
            devCreative = false;
            devSlot = 0;
            pending = new Pending { Look = Player.Appearance.Default(), WorldName = "", PlayerName = "", Ai = EnemyAiKind.Vanilla, Weapons = WeaponKind.New };
            CreativeMenu.SetPending(creative ? slot : 0, d, false);
            CreativeMenu.DevStarted++;
            Close();
            void Start()
            {
                Save.ModSave.Delete(slot);
                gm.profileID = slot;
                ui.StartNewGame(style == PlayStyle.SteelSoul, style == PlayStyle.Godseeker);
            }
            if (System.IO.File.Exists(GameSave(slot))) gm.ClearSaveFile(slot, _ => Start());
            else Start();
        }

        static bool devCreative;
        static int devSlot;

        /// <summary>Dev: the screen up ("List", "Character", "Difficulty" for the world creation, "Confirm" for the delete question, "Closed").</summary>
        public static string DevStep => step == Step.World ? "Difficulty" : step == Step.List && deleting != null ? "Confirm" : step.ToString();

        /// <summary>Dev: where something was drawn last: "creative" (新建), "slot" i (its row), "confirm" (是), "difficulty" i (1-5).</summary>
        public static Rect DevRect(string what, int i = 0)
        {
            string key = what == "slot" ? "slot" + i : what == "difficulty" ? "difficulty" + i : what;
            return devRects.TryGetValue(key, out var r) ? r : default;
        }

        /// <summary>Dev: the screen's 返回.</summary>
        public static void DevBack()
        {
            if (step == Step.World) BackToCharacter();
            else if (step == Step.Character)
            {
                CharacterCreation.Close();
                step = Step.List;
            }
            else if (step == Step.List)
            {
                if (deleting != null) deleting = null;
                else BackToTitle();
            }
        }
    }
}
