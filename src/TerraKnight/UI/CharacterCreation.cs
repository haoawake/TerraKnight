using TerraKnight.Terraria;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>How a new save is played, picked on the character creation screen as Terraria's character difficulty is.</summary>
    internal enum PlayStyle { Classic = 0, Journey = 1, SteelSoul = 2, Godseeker = 3 }

    /// <summary>
    /// Terraria's character creation screen for a new save (UICharacterCreation, 1.4.5): the character walking on top of
    /// the panel, the category row (info, clothing style, hair style, hair, eyes, skin, shirt, undershirt, pants, shoes)
    /// and its page (<see cref="CharCreationEditor"/>), 返回 and 创建 under it. The info page has the name and the play
    /// style in Terraria's difficulty column: 创造模式 (in Journey's place), 经典, and Hollow Knight's Steel Soul and
    /// Godseeker once the game has them unlocked. Opened by the start-game menu (TerraMenu) before its world creation;
    /// Draw is called each OnGUI while it is open and reports Back or Done once (the menu plays their sounds).
    /// </summary>
    internal static class CharacterCreation
    {
        public enum Result { None, Back, Done }

        public static Appearance Look = new Appearance();
        public static string Name = "";
        public static PlayStyle Style = PlayStyle.Classic;

        public static bool IsOpen { get; private set; }

        /// <summary>UICharacterCreation.MAX_NAME_LENGTH.</summary>
        public const int MaxName = 20;

        static CharCreationEditor editor;
        static readonly ImeTextEntry nameInput = new ImeTextEntry(Typed);
        static bool naming, createAfterName;
        static string nameDraft = "", nameBefore = "";
        static int keysFrame = -1;

        static readonly CharCreationEditor.Category[] all =
        {
            CharCreationEditor.Category.CharInfo, CharCreationEditor.Category.Clothing, CharCreationEditor.Category.HairStyle,
            CharCreationEditor.Category.HairColor, CharCreationEditor.Category.Eye, CharCreationEditor.Category.Skin,
            CharCreationEditor.Category.Shirt, CharCreationEditor.Category.Undershirt, CharCreationEditor.Category.Pants,
            CharCreationEditor.Category.Shoes,
        };

        public static void Open(Appearance start, string name)
        {
            Look = (start ?? new Appearance()).Sanitized();
            Name = name ?? "";
            Style = PlayStyle.Classic;
            editor = new CharCreationEditor(all, false) { DrawInfo = DrawInfo, Hairs = Appearance.CreationHairs };
            // Coming back to a look already made: its hair stays when another clothing style is tried.
            editor.HairPicked = start != null && !start.SameAs(Appearance.Default());
            editor.Select(CharCreationEditor.Category.CharInfo, Look);
            EndNaming(false);
            IsOpen = true;
            Prefetch();
        }

        /// <summary>
        /// Every picture the screen may draw, queued to load in the background (each one decoded the first time it was
        /// drawn held a frame up to 0.4 s): the clothing styles' body sheets, then the hair styles in the grid's order.
        /// </summary>
        static void Prefetch()
        {
            foreach (int v in Appearance.ClothStyles)
                for (int p = 0; p < 16; p++) TerrariaAssets.Prefetch(Appearance.Sheet(v, p));
            foreach (int h in Appearance.CreationHairs) TerrariaAssets.Prefetch(Appearance.HairSheet(h));
        }

        public static void Close()
        {
            EndNaming(false);
            IsOpen = false;
        }

        /// <summary>Dev: the name as if typed, and the screen's buttons.</summary>
        public static void DevName(string name) => Name = (name ?? "").Trim();

        /// <summary>Dev: the screen's category row and pages (its sliders driven by the autotests).</summary>
        internal static CharCreationEditor DevEditor => editor;

        static float S => TerraUi.S;

        public static Result Draw()
        {
            if (!IsOpen) return Result.None;
            if (editor == null) Open(Look, Name);
            TickNaming();
            // Its pictures load a little each frame (Prefetch at Open); one not in yet is left out until it is.
            TerrariaAssets.TickDeferredOnce();
            using var deferred = TerrariaAssets.DeferMissing();
            // UICharacterCreation.BuildPage: a 500 x 384 element 220 down the screen; its panel 50 into it, 230 high.
            float x0 = Mathf.Round((Screen.width - 500f * S) / 2f), top = 220f * S;
            var panel = new Rect(x0, top + 50f * S, 500f * S, 230f * S);
            TerraUi.Panel(panel, TerraUi.ListBack, Color.black);
            // MakeCharPreview: UICharacter at 1.5x, animated, standing on the panel's top edge (its 80 x 80 box 70 above it).
            var preview = new Rect(panel.center.x - 40f * S, panel.y - 70f * S - 4.5f * S, 80f * S, 80f * S);
            CharacterPortrait.DrawWalking(preview, Look, true, 1.5f * S);
            editor.Draw(panel, Look);

            // MakeBackAndCreatebuttons: half the width less 10 each, 50 high, 45 up from the element's bottom.
            var result = Result.None;
            float by = top + (384f - 50f - 45f) * S, bw = 240f * S;
            if (TerraUi.BigButton(new Rect(x0, by, bw, 50f * S), CharCreationEditor.TT("UI.Back", "返回"), "cc_back"))
            {
                Close();
                result = Result.Back;
            }
            else if (TerraUi.BigButton(new Rect(x0 + 500f * S - bw, by, bw, 50f * S), CharCreationEditor.TT("UI.Create", "创建"), "cc_create"))
            {
                // Click_NamingAndCreating: no name yet, it is asked for first.
                if (string.IsNullOrEmpty(Name.Trim()))
                {
                    if (editor.Selected != CharCreationEditor.Category.CharInfo) editor.Select(CharCreationEditor.Category.CharInfo, Look);
                    TerraUi.Sound("Menu_Open");
                    BeginNaming(true);
                }
                else result = Finish();
            }
            if (result == Result.None && pendingDone)
            {
                pendingDone = false;
                result = Finish();
            }
            DrawTooltip(editor.Tooltip ?? infoTip);
            infoTip = null;
            return result;
        }

        static bool pendingDone;
        static string infoTip;

        static Result Finish()
        {
            EndNaming(true);
            Name = Name.Trim();
            // TryAutoAssigningHair: no hair picked, the clothing style's own.
            if (!editor.HairPicked && Appearance.DefaultHairFor.TryGetValue(Look.Variant, out int hair)) Look.Hair = hair;
            Look = Look.Sanitized();
            IsOpen = false;
            return Result.Done;
        }

        /// <summary>Main.DrawMouseOver's text: the hovered button's name by the mouse, outlined, kept on the screen.</summary>
        internal static void DrawTooltip(string text)
        {
            if (string.IsNullOrEmpty(text) || Event.current.type != EventType.Repaint) return;
            var size = TerraUi.Measure(text, TerraUi.Normal * S);
            var m = TerraCursor.GuiMouse() + new Vector2(16f, 16f) * S;
            if (m.x + size.x > Screen.width) m.x = Screen.width - size.x - 4f;
            if (m.y + size.y > Screen.height - 30f * S) m.y = Screen.height - 30f * S - size.y;
            float pulse = (222f + 32f * Mathf.Sin(Time.unscaledTime * 1.85f)) / 255f;
            TerraUi.Text(new Rect(m.x, m.y, size.x + 4f, size.y), text, TerraUi.Normal * S, new Color(pulse, pulse, pulse), TextAnchor.UpperLeft);
        }

        // ------------------------------------------------------------------ the info page (MakeInfoMenu)

        struct StyleChoice
        {
            public PlayStyle Style;
            public string Title, Description;
            public Color Color;
        }

        static readonly System.Collections.Generic.List<StyleChoice> choices = new System.Collections.Generic.List<StyleChoice>();

        /// <summary>Terraria's difficulty column, top to bottom (创造模式 at the top, in Journey's place), with the styles this game has.</summary>
        static System.Collections.Generic.List<StyleChoice> Choices()
        {
            choices.Clear();
            choices.Add(new StyleChoice
            {
                Style = PlayStyle.Journey, Title = Lang.T("创造模式"), Color = TerraUi.CreativeColor,
                Description = Lang.T("创造模式：制作栏随时随地免费做出任何物品，商店不要钱，怪物图鉴全部解锁，整张地图和全部晶塔都已开放；物品照常消耗。"),
            });
            choices.Add(new StyleChoice
            {
                Style = PlayStyle.Classic, Title = CharCreationEditor.TT("LegacyMenu.26", "经典"), Color = Color.cyan,
                Description = Lang.T("经典：材料靠收集，制作要有材料和制作站，买东西要花吉欧。死亡后在原地留下阴影，打败它取回吉欧。"),
            });
            if (TerraMenu.SteelSoulUnlocked)
                choices.Add(new StyleChoice
                {
                    Style = PlayStyle.SteelSoul, Title = Lang.T("钢铁之魂"), Color = TerraUi.HardcoreColor,
                    Description = Lang.T("钢铁之魂（空洞骑士原版模式）：只有一条命，死亡后这个存档就此结束。"),
                });
            if (TerraMenu.GodseekerUnlocked)
                choices.Add(new StyleChoice
                {
                    Style = PlayStyle.Godseeker, Title = Lang.T("寻神者"), Color = new Color(1f, 0.84f, 0.42f),
                    Description = Lang.T("寻神者（空洞骑士原版模式）：直接来到神居，在万神殿里挑战首领。"),
                });
            return choices;
        }

        static void DrawInfo(Rect middle)
        {
            // The name: a 40-high button over the whole width (10 in from each side).
            var nameRect = new Rect(middle.x + 10f * S, middle.y, middle.width - 20f * S, 40f * S);
            bool over = TerraUi.Hover(nameRect, "cc_name");
            TerraUi.Spliced(nameRect, TerraUi.Tex("UI/CharCreation/CategoryPanel"), 10, new Color(1f, 1f, 1f, 0.5f));
            if (over || naming) TerraUi.Spliced(nameRect, TerraUi.Tex("UI/CharCreation/CategoryPanelBorder"), 10, Color.white);
            string title = naming ? CharCreationEditor.TT("LegacyMenu.45", "输入人物名称：") : CharCreationEditor.TT("UI.PlayerNameSlot", "名字：");
            float tw = TerraUi.Measure(title, TerraUi.Normal * S).x;
            TerraUi.Text(new Rect(nameRect.x + 10f * S, nameRect.y, tw + 4f, nameRect.height), title, TerraUi.Normal * S, Color.white);
            var contentRect = new Rect(nameRect.x + 10f * S + tw + 20f * S, nameRect.y, nameRect.xMax - (nameRect.x + 30f * S + tw), nameRect.height);
            if (naming)
            {
                string shown = nameDraft + nameInput.Composition + ((int)(Time.unscaledTime * 2f) % 2 == 0 ? "|" : " ");
                TerraUi.Text(contentRect, shown, TerraUi.Normal * S, Color.white);
                nameInput.Anchor(new Vector2(contentRect.x + TerraUi.Measure(nameDraft, TerraUi.Normal * S).x, contentRect.yMax));
            }
            else if (string.IsNullOrEmpty(Name)) TerraUi.Text(contentRect, CharCreationEditor.TT("Net.EmptyName", "名称为空。"), TerraUi.Normal * S, Color.gray);
            else TerraUi.Text(contentRect, Name, TerraUi.Normal * S, Color.white);
            if (TerraUi.Clicked(nameRect))
            {
                TerraUi.Sound("Menu_Open");
                if (!naming) BeginNaming(false);
            }
            else if (naming && Event.current.type == EventType.MouseDown && Event.current.button == 0) EndNaming(true);

            // The styles' column (40% wide, 50 below the name) and the chosen one's description beside it.
            var list = Choices();
            if (!list.Exists(c => c.Style == Style)) Style = PlayStyle.Classic;
            var column = new Rect(middle.x + 10f * S, middle.y + 50f * S, (middle.width - 20f * S) * 0.4f - 4f * S, middle.height - 50f * S);
            float rowH = column.height / Mathf.Max(3, list.Count);
            StyleChoice chosen = list[0];
            for (int i = 0; i < list.Count; i++)
            {
                var c = list[i];
                if (c.Style == Style) chosen = c;
                var r = new Rect(column.x, column.y + i * rowH, column.width, rowH);
                if (DifficultyButton(r, c.Title, c.Color, c.Style == Style, "cc_style" + (int)c.Style))
                {
                    Style = c.Style;
                    chosen = c;
                }
            }
            // The description's box: CategoryPanelHighlight sliced, light grey at 0.7, the text 20 in and 15 down.
            float dx = middle.x + 10f * S + (middle.width - 20f * S) * 0.4f + 4f * S;
            var desc = new Rect(dx, column.y, middle.xMax - 10f * S - 4f * S - dx, column.height);
            TerraUi.Spliced(desc, TerraUi.Tex("UI/CharCreation/CategoryPanelHighlight"), 10, new Color(211 / 255f, 211 / 255f, 211 / 255f, 0.7f));
            TerraUi.Text(new Rect(desc.x + 20f * S, desc.y + 15f * S, desc.width - 40f * S, desc.height - 20f * S), chosen.Description, TerraUi.Small * S, Color.white,
                TextAnchor.UpperLeft, true);
        }

        /// <summary>
        /// UIDifficultyButton: PanelGrayscale in the style's colour darkened (0.5 opaque), its lighter inset when chosen,
        /// the white border under the mouse, its title at the top in the middle.
        /// </summary>
        static bool DifficultyButton(Rect r, string title, Color color, bool selected, string id)
        {
            bool over = TerraUi.Hover(r, id);
            var gray = TerraUi.Tex("UI/CharCreation/PanelGrayscale");
            var dark = Color.Lerp(Color.black, color, 0.8f);
            dark.a = 0.5f;
            TerraUi.Spliced(r, gray, 10, dark);
            if (selected)
            {
                float k = (r.height < 30f * S ? 5f : 7f) * S;
                var light = Color.Lerp(color, Color.white, 0.7f);
                light.a = 0.5f;
                TerraUi.Spliced(new Rect(r.x + k, r.y + k - 2f * S, r.width - 2f * k, r.height - 2f * k), gray, 10, light);
            }
            if (over) TerraUi.Spliced(r, TerraUi.Tex("UI/CharCreation/CategoryPanelBorder"), 10, Color.white);
            TerraUi.Text(r, title, TerraUi.Normal * 0.9f * S, Color.white, TextAnchor.MiddleCenter);
            if (!TerraUi.Clicked(r)) return false;
            TerraUi.Sound("Menu_Tick");
            return true;
        }

        // ------------------------------------------------------------------ the name (UIVirtualKeyboard's typing, in place)

        static void BeginNaming(bool thenCreate)
        {
            naming = true;
            createAfterName = thenCreate;
            nameBefore = Name;
            nameDraft = Name;
            nameInput.Begin();
        }

        static void EndNaming(bool keep)
        {
            if (!naming) return;
            naming = false;
            nameInput.End();
            if (keep) Name = nameDraft.Trim();
            else Name = nameBefore;
            createAfterName = false;
        }

        static void Typed(char c)
        {
            if (!naming || char.IsControl(c)) return;
            if (nameDraft.Length < MaxName) nameDraft += c;
        }

        /// <summary>Once a frame while the name is typed: Backspace, Enter (kept; with 创建 waiting, the character made), Esc (dropped).</summary>
        static void TickNaming()
        {
            if (!naming || keysFrame == Time.frameCount) return;
            keysFrame = Time.frameCount;
            nameInput.Tick();
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb == null || nameInput.EditingComposition) return;
            nameDraft = nameInput.TakeBackspaces(nameDraft);
            if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
            {
                bool create = createAfterName && nameDraft.Trim().Length > 0;
                EndNaming(true);
                if (create) pendingDone = true;
            }
            else if (kb.escapeKey.wasPressedThisFrame) EndNaming(false);
        }

        /// <summary>The name is being typed: the keys are the text's (the menu under it hears none of them).</summary>
        public static bool Typing => naming;
    }
}
