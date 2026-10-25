using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>
    /// What a new save starts with, and the main menu's shared drawing. The start menu (<see cref="TerraMenu"/>,
    /// Terraria's world list and creation screens) sets the choices here: creative mode (Terraria's Journey, in the
    /// save's slot), the difficulty (<see cref="GameDifficulty"/>: Normal, Hard, Expert, Master, Hollow) and the Voice
    /// Beyond the Door's guidance (<see cref="Items.BossGuide"/>); the new game takes each once (GameHooks' StartNewGame).
    /// The helpers below draw the mod's own menu panels (the multiplayer menu, the settings' guidance tab) and keep the
    /// game's own menu from taking clicks while one is open.
    /// </summary>
    internal static class CreativeMenu
    {
        static int pendingSlot;
        static DifficultyLevel pendingDifficulty;
        /// <summary>The guidance chosen for the game starting now: followed or not.</summary>
        static bool pendingGuide;
        internal static GUIStyle button, title, text, small, big;

        /// <summary>A panel of the start menu is open.</summary>
        public static bool IsOpen => TerraMenu.IsOpen;

        public static int DevStarted, DevAsked;

        // The autotests' old creative panel steps, on the start menu now.
        public static void DevOpen() => TerraMenu.DevOpenCreative();
        public static void DevChoose(int slot) => TerraMenu.DevChoose(slot);
        public static void DevConfirm() { }
        public static void DevPick(DifficultyLevel d) => TerraMenu.DevPick(d);
        public static void DevGuide(bool follow) { }
        public static string DevStep => TerraMenu.DevStep;
        public static Rect DevRect(string what, int i = 0) => TerraMenu.DevRect(what, i);
        public static void DevBack() => TerraMenu.DevBack();

        /// <summary>The new game's choices (the start menu's): creative in this slot (0: not), its difficulty, the guidance.</summary>
        internal static void SetPending(int creativeSlot, DifficultyLevel difficulty, bool guide)
        {
            pendingSlot = creativeSlot;
            pendingDifficulty = difficulty;
            pendingGuide = guide;
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

        /// <summary>The start menu's world list is up (the multiplayer menu's button shows on it).</summary>
        internal static bool ProfilesShown() => InMainMenu() && TerraMenu.ListShown;

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

        /// <summary>From TerraMod.OnGUI, before its gameplay-only drawing: the start menu's screens.</summary>
        public static void Draw()
        {
            TickUnblock();
            TerraMenu.Draw();
        }

        static bool unblockPending;
        static int upSince = -1;

        public static bool DevMenuBlocked { get; private set; }

        /// <summary>
        /// The game's own menu under a panel takes no clicks. The panel acts on the press; the game's buttons act on the
        /// release, so the menu takes clicks again only once the button is up and a couple of frames have gone (else the
        /// release of "返回" lands on the button under it).
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
    }
}
