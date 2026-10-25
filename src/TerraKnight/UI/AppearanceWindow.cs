using System.Collections.Generic;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>
    /// Changing the look in the game, Terraria's way: the Stylist's hair window (Main.OpenHairWindow: hair style and hair
    /// colour, 10 gold for a new style and 2 for a new colour, Main.DrawHairWindow) and a dresser's clothes window
    /// (Main.OpenClothesWindow, from its lower half: clothing style and the clothes', eyes' and skin's colours, free).
    /// Both are the creation screen's panel with only those categories; the player wears the look while it is edited,
    /// and gets the old one back on 取消. The kept look is the save's (Save.ModSave writes Appearance.Current) and goes to
    /// the others in a LAN game.
    /// </summary>
    internal static class AppearanceWindow
    {
        public enum Kind { None, Hair, Clothes }

        public static Kind Showing { get; private set; }
        public static bool IsOpen => Showing != Kind.None;

        static Appearance editing, before;
        static CharCreationEditor editor;
        static Vector2 openedAt;
        static string message;

        static readonly CharCreationEditor.Category[] hairCategories = { CharCreationEditor.Category.HairStyle, CharCreationEditor.Category.HairColor };
        static readonly CharCreationEditor.Category[] clothesCategories =
        {
            CharCreationEditor.Category.Clothing, CharCreationEditor.Category.Shirt, CharCreationEditor.Category.Undershirt,
            CharCreationEditor.Category.Pants, CharCreationEditor.Category.Shoes, CharCreationEditor.Category.Eye, CharCreationEditor.Category.Skin,
        };

        /// <summary>Dev: the window's category row and pages, and the look being edited in it.</summary>
        internal static CharCreationEditor DevEditor => editor;
        internal static Appearance DevEditing => editing;

        /// <summary>The Stylist's 发型 button.</summary>
        public static void OpenHair()
        {
            int stage = Items.Shop.UnlockedStage();
            Open(Kind.Hair, hairCategories, Appearance.StylistHairs(stage >= 5, stage >= 5, stage >= World.Areas.MaxLevel));
        }

        /// <summary>A dresser's lower half right-clicked.</summary>
        public static void OpenClothes() => Open(Kind.Clothes, clothesCategories, Appearance.CreationHairs);

        /// <summary>
        /// The window's mouse events. The inventory draws the window in its repaint only, so a press, a drag, a release or
        /// the wheel reaches it from an OnGUI of its own, laid out the same, while it is on the screen: its buttons, colour
        /// sliders and hair list take them as the creation screen's do.
        /// </summary>
        sealed class EventPass : MonoBehaviour
        {
            readonly List<Rect> unused = new List<Rect>();

            void Awake() => useGUILayout = false;

            void OnGUI()
            {
                var t = Event.current.type;
                if (!IsOpen || t == EventType.Repaint || t == EventType.Layout || t == EventType.Used || Time.frameCount - drawnFrame > 1) return;
                unused.Clear();
                Draw(GameView.Rect, unused);
            }
        }

        static EventPass events;
        /// <summary>The frame the window was last drawn in (the inventory's repaint): the events go to it only while it shows.</summary>
        static int drawnFrame = -10;

        static void Open(Kind kind, CharCreationEditor.Category[] categories, List<int> hairs)
        {
            if (events == null)
            {
                var host = new GameObject("TerraKnight Appearance Window");
                Object.DontDestroyOnLoad(host);
                events = host.AddComponent<EventPass>();
            }
            if (IsOpen) Cancel(true);
            // Main.playerInventory = false, as Terraria opens either window.
            Items.InventoryGui.Open = false;
            before = Appearance.Current.Clone();
            editing = Appearance.Current.Clone();
            editor = new CharCreationEditor(categories, true) { Hairs = hairs, HairPicked = true };
            editor.Select(categories[0], editing);
            Showing = kind;
            message = null;
            var hero = HeroController.SilentInstance;
            openedAt = hero != null ? (Vector2)hero.transform.position : Vector2.zero;
            TerraMod.Instance?.PlaySound("Menu_Open", 0.7f);
        }

        /// <summary>CancelHairWindow / CancelClothesWindow: the old look back.</summary>
        public static void Cancel(bool quiet = false)
        {
            if (!IsOpen) return;
            Showing = Kind.None;
            if (before != null && !before.SameAs(Appearance.Current)) Appearance.Current = before;
            editor = null;
            if (!quiet) TerraMod.Instance?.PlaySound("Menu_Close", 0.7f);
        }

        /// <summary>BuyHairWindow's price (Main.DrawHairWindow): 10 gold for a new style, 2 for a new colour, in whole gold.</summary>
        static long Price()
        {
            long copper = 0;
            if (editing.Hair != before.Hair) copper += 100000;
            if (editing.HairColor != before.HairColor) copper += 20000;
            return (long)System.Math.Round(copper / 10000.0) * 10000;
        }

        /// <summary>The player walked off, opened the inventory or left the scene: the window closes as Terraria's does.</summary>
        static bool ShouldClose()
        {
            var hero = HeroController.SilentInstance;
            if (hero == null || Items.InventoryGui.Open || Town.TownNpcs.ChatOpen) return true;
            return Vector2.Distance(hero.transform.position, openedAt) > 8f;
        }

        /// <summary>
        /// From InventoryGui's drawing, over the game: the panel, its preview and its buttons (their rects block the world's
        /// clicks); the same from <see cref="EventPass"/> for the mouse's events.
        /// </summary>
        internal static void Draw(Rect view, List<Rect> panels)
        {
            if (!IsOpen) return;
            if (ShouldClose())
            {
                Cancel();
                return;
            }
            if (Event.current.type == EventType.Repaint) drawnFrame = Time.frameCount;
            float S = TerraUi.S;
            bool input = TerraUi.Input;
            TerraUi.Input = true;
            float x0 = Mathf.Round(view.center.x - 250f * S), top = view.y + 150f * S;
            var panel = new Rect(x0, top + 50f * S, 500f * S, 230f * S);
            string title = Showing == Kind.Hair ? CharCreationEditor.TT("GameUI.HairStyle", "发型") : CharCreationEditor.TT("LegacyMenu.21", "衣服");
            TerraUi.Header(panel.center.x - 140f * S, top - 50f * S, title);
            TerraUi.Panel(panel, TerraUi.ListBack, Color.black);
            var preview = new Rect(panel.center.x - 40f * S, panel.y - 70f * S - 4.5f * S, 80f * S, 80f * S);
            CharacterPortrait.DrawWalking(preview, editing, true, 1.5f * S);
            editor.Draw(panel, editing);
            // Worn while it is chosen, as Terraria shows it on the player.
            if (!editing.SameAs(Appearance.Current)) Appearance.Current = editing;

            float by = panel.yMax + 9f * S, bw = 240f * S;
            var cancel = new Rect(x0, by, bw, 50f * S);
            var ok = new Rect(x0 + 500f * S - bw, by, bw, 50f * S);
            panels.Add(new Rect(panel.x, top - 50f * S, panel.width, by + 50f * S - (top - 50f * S)));
            if (TerraUi.BigButton(cancel, CharCreationEditor.TT("UI.Cancel", "取消"), "aw_cancel"))
            {
                Cancel();
                TerraUi.Input = input;
                TerraUi.EndFrame();
                return;
            }
            if (Showing == Kind.Hair)
            {
                long price = Price();
                string label = price > 0 ? CharCreationEditor.TT("GameUI.Buy", "购买") + " (" + Items.Shop.Money(price) + ")" : CharCreationEditor.TT("GameUI.Buy", "购买");
                if (TerraUi.BigButton(ok, label, "aw_ok", price > 0))
                {
                    if (Items.Shop.Spend(price)) Keep("Coins");
                    else message = Lang.T("你没有足够的钱。");
                }
            }
            else if (TerraUi.BigButton(ok, CharCreationEditor.TT("UI.Save", "保存"), "aw_ok")) Keep("Grab");
            if (message != null)
                TerraUi.Text(new Rect(panel.x, by + 54f * S, panel.width, 30f * S), message, TerraUi.Normal * S, new Color(1f, 0.55f, 0.5f), TextAnchor.MiddleCenter);
            CharacterCreation.DrawTooltip(editor?.Tooltip);
            TerraUi.Input = input;
            TerraUi.EndFrame();
        }

        /// <summary>BuyHairWindow / SaveClothesWindow: the look kept, the others told.</summary>
        static void Keep(string sound)
        {
            Appearance.Current = editing;
            before = null;
            Showing = Kind.None;
            editor = null;
            TerraMod.Instance?.PlaySound(sound, 0.7f);
            Plugin.Log.LogInfo($"[外观] 换了{(sound == "Coins" ? "发型" : "衣服")}：发型 {Appearance.Current.Hair}，服装 {Appearance.Current.Variant}");
        }
    }
}

namespace TerraKnight.Items
{
    internal static partial class InventoryGui
    {
        /// <summary>The Stylist's or a dresser's window (UI.AppearanceWindow), its rect one of the panels the world's clicks don't go through.</summary>
        static void DrawAppearanceWindow(Rect view) => UI.AppearanceWindow.Draw(view, panels);
    }
}
