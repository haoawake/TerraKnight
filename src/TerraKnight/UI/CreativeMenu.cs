using System.IO;
using HarmonyLib;
using TerraKnight.Terraria;
using TerraKnight.World;
using UnityEngine;
using UnityEngine.UI;

namespace TerraKnight.UI
{
    /// <summary>
    /// Starting a save. Every new game first asks its difficulty, as Terraria's world creation
    /// does (<see cref="GameDifficulty"/>: Normal, Hard, Expert, Master, Hollow). And the save profiles
    /// screen has a "创造模式" button in its bottom right: its panel lists the four save slots
    /// (empty, a save, a creative save); choosing one (a second click to confirm when it would
    /// overwrite a save), then a difficulty, clears it and starts a new game there in creative
    /// mode: items never run out and the crafting column makes anything, free (the mod's
    /// <see cref="TerraMod.Creative"/>). After the difficulty, a new game asks whether to follow the
    /// Voice Beyond the Door's guidance (<see cref="Items.BossGuide"/>). The game's own menu takes no
    /// clicks while a panel is open. A save's slot shows its mode and difficulty before its area's name.
    /// </summary>
    [HarmonyPatch]
    internal static class CreativeMenu
    {
        enum Step { Closed, Choose, Confirm, Difficulty, Guide }

        static Step step;
        static int chosen;
        static int choosePage;
        static int FirstChoice => choosePage*SaveSlots.PerPage+1;
        static int pendingSlot;
        /// <summary>The difficulty step is the creative panel's (else an ordinary new game's, started with these).</summary>
        static bool forCreative, askPermaDeath, askBossRush;
        static DifficultyLevel pendingDifficulty;
        static float pendingAt;
        /// <summary>The guidance chosen for the game starting now: followed or not.</summary>
        static bool pendingGuide;
        internal static GUIStyle button, title, text, small, big;
        static readonly bool[] hasSave = new bool[SaveSlots.Max + 1], creative = new bool[SaveSlots.Max + 1];

        /// <summary>A panel of this menu is open (the save pager hides under it).</summary>
        public static bool IsOpen => step != Step.Closed;

        public static int DevStarted, DevAsked;

        /// <summary>Dev: the panel opened, a slot chosen in it, the overwrite confirmed, a difficulty chosen (as their buttons would).</summary>
        public static void DevOpen() => Open();
        public static void DevChoose(int slot) => Choose(slot);
        public static void DevConfirm() { if (step == Step.Confirm) step = Step.Difficulty; }
        public static void DevPick(DifficultyLevel d)
        {
            Pick(d);
            // The autotests' games start without the guidance.
            if (step == Step.Guide) Follow(false);
        }

        /// <summary>Dev: the guidance step's answer, as its buttons would.</summary>
        public static void DevGuide(bool follow) { if (step == Step.Guide) Follow(follow); }
        public static string DevStep => step.ToString();

        /// <summary>Dev: where a part of the menu is drawn (GUI rect): "creative" (the profiles' button), "slot" s, "confirm", "difficulty" i (1-5).</summary>
        public static Rect DevRect(string what, int i = 0)
        {
            float ui = Screen.height / 1080f;
            if (what == "creative") return new Rect(Screen.width - 300f * ui, Screen.height - 120f * ui, 240f * ui, 58f * ui);
            float w = 760f * ui, h = (step==Step.Difficulty ? 640f : step==Step.Choose ? 620f : 560f) * ui;
            var p = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            if (what == "slot") return new Rect(p.x + 80f * ui, p.y + (224f + (i - FirstChoice) * 72f) * ui, w - 160f * ui, 58f * ui);
            if (what == "confirm") return new Rect(p.x + w / 2f - 230f * ui, p.y + 330f * ui, 210f * ui, 56f * ui);
            return new Rect(p.x + 70f * ui, p.y + (178f + (i - 1) * 74f) * ui, p.width - 140f * ui, 64f * ui);
        }
        /// <summary>Dev: the difficulty step's "返回", as its button would.</summary>
        public static void DevBack()
        {
            if (step != Step.Difficulty) return;
            if (forCreative) step = Step.Choose;
            else Close();
        }

        /// <summary>The slot a creative game was asked for, taken once when that game starts (GameHooks' StartNewGame).</summary>
        public static bool TakePending(int slot)
        {
            bool yes = pendingSlot != 0 && pendingSlot == slot;
            pendingSlot = 0;
            return yes;
        }

        /// <summary>Whether the game starting now follows the guidance, taken once (not, when the menu didn't ask: the autotests' games).</summary>
        public static bool TakeGuide()
        {
            bool on = pendingGuide;
            pendingGuide = false;
            return on;
        }

        /// <summary>The difficulty chosen for the game starting now, taken once (Master when none was).</summary>
        public static DifficultyLevel TakeDifficulty()
        {
            var d = pendingDifficulty != 0 ? pendingDifficulty : DifficultyLevel.Master;
            pendingDifficulty = 0;
            return d;
        }

        internal static bool InMainMenu()
        {
            var gm = GameManager.UnsafeInstance;
            return UIManager.instance != null && gm != null && gm.gameState == GlobalEnums.GameState.MAIN_MENU;
        }

        internal static bool ProfilesShown()
        {
            var ui = UIManager.instance;
            return InMainMenu() && ui.menuState == GlobalEnums.MainMenuState.SAVE_PROFILES && ui.saveProfileScreen != null && ui.saveProfileScreen.alpha > 0.95f;
        }

        internal static void Styles(float ui)
        {
            if (button == null)
            {
                button = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                title = new GUIStyle(button);
                text = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft, wordWrap = true };
                small = new GUIStyle(button) { alignment = TextAnchor.MiddleLeft };
                big = new GUIStyle(button) { alignment = TextAnchor.MiddleLeft };
            }
            button.fontSize = Mathf.RoundToInt(22 * ui);
            title.fontSize = Mathf.RoundToInt(30 * ui);
            text.fontSize = Mathf.RoundToInt(17 * ui);
            small.fontSize = Mathf.RoundToInt(18 * ui);
            big.fontSize = Mathf.RoundToInt(26 * ui);
        }

        internal static void Fill(Rect r, Color c)
        {
            var prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        internal static void Rim(Rect r, Color c, float t)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        internal static void Shadowed(Rect r, string s, GUIStyle st, Color c)
        {
            float ui = Screen.height / 1080f;
            st.normal.textColor = new Color(0f, 0f, 0f, 0.85f * c.a);
            GUI.Label(new Rect(r.x + 2f * ui, r.y + 2f * ui, r.width, r.height), s, st);
            st.normal.textColor = c;
            GUI.Label(r, s, st);
        }

        internal static readonly Color Gold = new Color(1f, 0.86f, 0.45f), Back = new Color(0.04f, 0.04f, 0.07f, 0.92f), Line = new Color(0.82f, 0.74f, 0.55f, 0.9f);

        /// <summary>A click on this rect now (with the menu's tick).</summary>
        internal static bool Clicked(Rect r)
        {
            var e = Event.current;
            if (e.type != EventType.MouseDown || e.button != 0 || !r.Contains(e.mousePosition)) return false;
            e.Use();
            TerraMod.Instance?.PlaySound("Menu_Tick", 0.7f);
            return true;
        }

        /// <summary>A menu button in the game's own look: plain text in a thin frame, brighter when hovered.</summary>
        internal static bool Button(Rect r, string label, float ui, Color? tint = null)
        {
            bool over = r.Contains(TerraCursor.GuiMouse());
            Fill(r, over ? new Color(1f, 1f, 1f, 0.12f) : new Color(0f, 0f, 0f, 0.55f));
            Rim(r, over ? Gold : Line, 1.5f * ui);
            Shadowed(r, label, button, tint ?? (over ? Gold : Color.white));
            return Clicked(r);
        }

        /// <summary>From TerraMod.OnGUI, before its gameplay-only drawing.</summary>
        public static void Draw()
        {
            TickUnblock();
            bool profiles = ProfilesShown();
            // An ordinary new game's difficulty is asked wherever it was started from (the profiles, the play mode menu).
            bool shown = (step == Step.Difficulty || step == Step.Guide) && !forCreative ? InMainMenu() : profiles;
            if (!shown)
            {
                if (step != Step.Closed) Close();
                if (!profiles) return;
            }
            float ui = Screen.height / 1080f;
            Styles(ui);
            if (step == Step.Closed)
            {
                var r = new Rect(Screen.width - 300f * ui, Screen.height - 120f * ui, 240f * ui, 58f * ui);
                if (Button(r, Lang.T("创造模式"), ui, Gold)) Open();
                return;
            }
            // The panel over the menu.
            Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.55f));
            float w = 760f * ui, h = (step==Step.Difficulty ? 640f : step==Step.Choose ? 620f : 560f) * ui;
            var p = new Rect((Screen.width - w) / 2f, (Screen.height - h) / 2f, w, h);
            Fill(p, Back);
            Rim(p, Line, 2f * ui);
            Rim(new Rect(p.x + 8f * ui, p.y + 8f * ui, w - 16f * ui, h - 16f * ui), new Color(Line.r, Line.g, Line.b, 0.35f), 1f * ui);
            if (step == Step.Guide)
            {
                DrawGuide(p, ui);
                return;
            }
            Shadowed(new Rect(p.x, p.y + 24f * ui, w, 40f * ui), Lang.T(forCreative || step != Step.Difficulty ? "创造模式" : "新的存档"), title, Gold);
            string intro = forCreative || step != Step.Difficulty
                ? Lang.T("物品用不完，获得一次就是无数个；制作栏随时随地可以免费做出任何物品，商店也不要钱。")
                : Lang.T("像泰拉瑞亚创建世界那样，先为这个存档选一个难度。难度决定敌人的血量和伤害，之后不能再改。");
            Shadowed(new Rect(p.x + 48f * ui, p.y + 78f * ui, w - 96f * ui, 60f * ui), intro, text, new Color(0.9f, 0.9f, 0.9f));
            if (step == Step.Choose)
            {
                Shadowed(new Rect(p.x + 48f * ui, p.y + 140f * ui, w - 96f * ui, 30f * ui), Lang.T("选择一个存档位来创建创造模式存档（已有的存档会被覆盖）："), text, new Color(0.8f, 0.8f, 0.85f));
                // The modal owns its page: canceling must not leave the covered profiles' cached slots stale.
                SaveSlots.DrawPager(new Rect(p.x+80f*ui,p.y+176f*ui,w-160f*ui,36f*ui),ui,choosePage,
                    page=>choosePage=Mathf.Clamp(page,0,SaveSlots.Pages-1));
                for (int s = FirstChoice; s < FirstChoice + SaveSlots.PerPage && s <= SaveSlots.Max; s++)
                {
                    var r = new Rect(p.x + 80f * ui, p.y + (224f + (s - FirstChoice) * 72f) * ui, w - 160f * ui, 58f * ui);
                    string state = Lang.T(!hasSave[s] ? "空" : creative[s] ? "创造模式存档（将被覆盖）" : "已有存档（将被覆盖）");
                    if (Button(r, Lang.F("存档 {0}", s) + "　·　" + state, ui, hasSave[s] ? new Color(1f, 0.75f, 0.6f) : Color.white)) Choose(s);
                }
                if (Button(new Rect(p.x + (w - 200f * ui) / 2f, p.yMax - 78f * ui, 200f * ui, 50f * ui), Lang.T("取消"), ui)) Close();
            }
            else if (step == Step.Confirm)
            {
                Shadowed(new Rect(p.x + 48f * ui, p.y + 200f * ui, w - 96f * ui, 80f * ui),
                    Lang.F("存档 {0} 已经有存档了。覆盖后原有的进度会被删除，并在这里开始一个新的创造模式存档。", chosen), text, new Color(1f, 0.8f, 0.7f));
                if (Button(new Rect(p.x + w / 2f - 230f * ui, p.y + 330f * ui, 210f * ui, 56f * ui), Lang.T("覆盖并继续"), ui, new Color(1f, 0.7f, 0.55f))) step = Step.Difficulty;
                if (Button(new Rect(p.x + w / 2f + 20f * ui, p.y + 330f * ui, 210f * ui, 56f * ui), Lang.T("返回"), ui)) step = Step.Choose;
            }
            else DrawDifficulty(p, ui);
        }

        /// <summary>
        /// The guidance: whether the voice that pulled the player through the portal speaks after each boss fight's first
        /// win (the win, then where the next fight is). Changed later in the Terraria settings' guidance tab.
        /// </summary>
        static void DrawGuide(Rect p, float ui)
        {
            Shadowed(new Rect(p.x, p.y + 24f * ui, p.width, 40f * ui), Items.BossGuide.Title, title, new Color(0.82f, 0.7f, 1f));
            Shadowed(new Rect(p.x + 48f * ui, p.y + 90f * ui, p.width - 96f * ui, 150f * ui), Items.BossGuide.Question, text, new Color(0.9f, 0.9f, 0.9f));
            Shadowed(new Rect(p.x + 48f * ui, p.y + 250f * ui, p.width - 96f * ui, 60f * ui), Items.BossGuide.QuestionNote, text, new Color(0.75f, 0.75f, 0.82f));
            if (Button(new Rect(p.x + p.width / 2f - 230f * ui, p.y + 330f * ui, 210f * ui, 56f * ui), Items.BossGuide.FollowLabel, ui, new Color(0.82f, 0.7f, 1f))) Follow(true);
            if (Button(new Rect(p.x + p.width / 2f + 20f * ui, p.y + 330f * ui, 210f * ui, 56f * ui), Items.BossGuide.RefuseLabel, ui)) Follow(false);
            if (Button(new Rect(p.x + (p.width - 200f * ui) / 2f, p.yMax - 78f * ui, 200f * ui, 50f * ui), Lang.T("返回"), ui)) step = Step.Difficulty;
        }

        /// <summary>Five difficulty cards: its icon, its name in its colour, what it does to the enemies.</summary>
        static void DrawDifficulty(Rect p, float ui)
        {
            string head = forCreative ? Lang.F("存档 {0} 的难度（敌人的血量和伤害，大师是原本的数值）：", chosen) : Lang.T("选择难度（敌人的血量和伤害，大师是原本的数值）：");
            Shadowed(new Rect(p.x + 48f * ui, p.y + 140f * ui, p.width - 96f * ui, 30f * ui), head, text, new Color(0.8f, 0.8f, 0.85f));
            var mouse = TerraCursor.GuiMouse();
            for (int i = 1; i <= (int)DifficultyLevel.Hollow; i++)
            {
                var d = (DifficultyLevel)i;
                var c = GameDifficulty.ColorOf(d);
                var r = new Rect(p.x + 70f * ui, p.y + (178f + (i - 1) * 74f) * ui, p.width - 140f * ui, 64f * ui);
                bool over = r.Contains(mouse);
                Fill(r, over ? new Color(c.r, c.g, c.b, 0.16f) : new Color(0f, 0f, 0f, 0.55f));
                Rim(r, over ? c : new Color(Line.r, Line.g, Line.b, 0.6f), (over ? 2f : 1.5f) * ui);
                Texture2D icon = null;
                try { icon = TerrariaAssets.Texture(GameDifficulty.IconOf(d)); }
                catch (System.Exception) { }
                if (icon != null)
                {
                    float s = 48f * ui / Mathf.Max(icon.width, icon.height);
                    var ir = new Rect(r.x + 14f * ui, r.center.y - icon.height * s / 2f, icon.width * s, icon.height * s);
                    var prev = GUI.color;
                    // Hard has no icon of its own: Normal's, in Hard's colour.
                    GUI.color = d == DifficultyLevel.Hard || d == DifficultyLevel.Hollow ? c : Color.white;
                    GUI.DrawTexture(ir, icon);
                    GUI.color = prev;
                }
                Shadowed(new Rect(r.x + 78f * ui, r.y, 110f * ui, r.height), GameDifficulty.NameOf(d), big, c);
                int pct = Mathf.RoundToInt(GameDifficulty.FactorOf(d) * 100f);
                string what = d == DifficultyLevel.Hollow ? Lang.T("大师数值，恢复原版面具数量增伤")
                    : d == DifficultyLevel.Master ? Lang.T("敌人血量与伤害 100%，不叠加面具数量") : Lang.F("敌人血量与伤害为大师的 {0}%", pct);
                Shadowed(new Rect(r.x + 190f * ui, r.y, r.width - 200f * ui, r.height), what, small, over ? Color.white : new Color(0.82f, 0.82f, 0.86f));
                if (Clicked(r)) Pick(d);
            }
            if (Button(new Rect(p.x + (p.width - 200f * ui) / 2f, p.yMax - 70f * ui, 200f * ui, 50f * ui), Lang.T("返回"), ui))
            {
                if (forCreative) step = Step.Choose;
                else Close();
            }
        }

        static void Open()
        {
            choosePage=SaveSlots.Page;
            for (int s = 1; s <= SaveSlots.Max; s++)
            {
                hasSave[s] = File.Exists(Path.Combine(TerraMod.SaveDir, $"user{s}.dat"));
                creative[s] = hasSave[s] && Save.ModSave.IsCreative(s);
            }
            forCreative = true;
            step = Step.Choose;
            Block(true);
        }

        static void Choose(int slot)
        {
            chosen = slot;
            step = hasSave[slot] ? Step.Confirm : Step.Difficulty;
        }

        static void Close()
        {
            step = Step.Closed;
            forCreative = false;
            Block(false);
        }

        static bool unblockPending;
        static int upSince = -1;

        public static bool DevMenuBlocked { get; private set; }

        /// <summary>
        /// The menu under the panel takes no clicks (the profiles, or the play mode menu a new game was
        /// started from). The panel acts on the press; the game's buttons act on the release, so the menu
        /// takes clicks again only once the button is up and a couple of frames have gone (else the
        /// release of "返回" lands on the save slot under it and starts that game).
        /// </summary>
        internal static void Block(bool on)
        {
            if (!on)
            {
                unblockPending = true;
                upSince = -1;
                return;
            }
            unblockPending = false;
            SetBlocked(true);
        }

        static void TickUnblock()
        {
            if (!unblockPending) return;
            var m = UnityEngine.InputSystem.Mouse.current;
            if (m != null && (m.leftButton.isPressed || m.rightButton.isPressed))
            {
                upSince = -1;
                return;
            }
            if (upSince < 0) upSince = Time.frameCount;
            if (Time.frameCount - upSince < 2) return;
            unblockPending = false;
            SetBlocked(false);
        }

        static void SetBlocked(bool on)
        {
            DevMenuBlocked = on;
            var ui = UIManager.instance;
            if (ui == null) return;
            foreach (var cg in new[] { ui.saveProfileScreen, ui.playModeMenuScreen != null ? ui.playModeMenuScreen.GetComponent<CanvasGroup>() : null })
            {
                if (cg == null) continue;
                cg.interactable = !on;
                cg.blocksRaycasts = !on;
            }
        }

        /// <summary>A difficulty chosen: on to the guidance.</summary>
        static void Pick(DifficultyLevel d)
        {
            pendingDifficulty = d;
            step = Step.Guide;
        }

        /// <summary>The guidance answered: the game starts (the creative panel's in its slot).</summary>
        static void Follow(bool on)
        {
            pendingGuide = on;
            pendingAt = Time.realtimeSinceStartup;
            Plugin.Log.LogInfo($"[新存档] 难度：{GameDifficulty.NameOf(pendingDifficulty)}，神的指引：{(on ? "听从" : "拒绝")}");
            if (forCreative)
            {
                Begin(chosen);
                return;
            }
            Close();
            UIManager.instance?.StartNewGame(askPermaDeath, askBossRush);
        }

        /// <summary>The slot cleared (the game's save and the mod's), then a new game started in it, creative.</summary>
        static void Begin(int slot)
        {
            Close();
            var gm = GameManager.instance;
            var ui = UIManager.instance;
            if (gm == null || ui == null) return;
            pendingSlot = slot;
            DevStarted++;
            Plugin.Log.LogInfo($"[创造模式] 在存档 {slot} 开始创造模式新游戏，难度{GameDifficulty.NameOf(pendingDifficulty)}{(hasSave[slot] ? "（覆盖原存档）" : "")}");
            void Start()
            {
                Save.ModSave.Delete(slot);
                gm.profileID = slot;
                ui.StartNewGame(false, false);
            }
            if (hasSave[slot]) gm.ClearSaveFile(slot, _ => Start());
            else Start();
        }

        /// <summary>
        /// A new game from the menu asks its difficulty first: the game's start waits for the
        /// choice, then goes on as it would have (the choice's own call, or the creative panel's,
        /// passes straight through).
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(UIManager), nameof(UIManager.StartNewGame))]
        static bool AskDifficulty(bool permaDeath, bool bossRush)
        {
            if (pendingDifficulty != 0 && Time.realtimeSinceStartup - pendingAt < 20f) return true;
            pendingDifficulty = 0;
            if (!InMainMenu()) return true;
            askPermaDeath = permaDeath;
            askBossRush = bossRush;
            forCreative = false;
            step = Step.Difficulty;
            Block(true);
            DevAsked++;
            return false;
        }

        /// <summary>A save's slot shows its mode and difficulty before its area's name.</summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(SaveSlotButton), "PresentSaveSlot")]
        static void MarkMode(SaveSlotButton __instance)
        {
            if (__instance == null || __instance.locationText == null) return;
            // Its slot on the page shown (SaveSlots: fifty slots on pages of four).
            int slot = SaveSlots.IndexOf(__instance);
            if (slot <= 0 || slot > SaveSlots.Max) return;
            var mode = Save.ModSave.ModeOf(slot);
            string shown = __instance.locationText.text ?? "";
            if (!mode.Known || shown.StartsWith("<color=#")) return;
            string tag = (mode.Creative ? "<color=#FFD873>" + Lang.T("创造模式") + "</color> · " : "")
                + $"<color=#{ColorUtility.ToHtmlStringRGB(GameDifficulty.ColorOf(mode.Difficulty))}>{GameDifficulty.NameOf(mode.Difficulty)}</color> · ";
            __instance.locationText.supportRichText = true;
            __instance.locationText.text = tag + shown;
        }
    }
}
