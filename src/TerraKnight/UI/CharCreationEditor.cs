using System.Collections.Generic;
using TerraKnight.Player;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>
    /// The inside of Terraria's character creation panel (UICharacterCreation's top and middle containers): the row of
    /// category buttons (info, clothing style, hair style, the seven colours), and under it the chosen one's page: the
    /// HSL sliders with the colour's hex code and its copy, paste and random buttons; the hair style grid; the ten
    /// clothing styles with the player template's copy, paste and randomize. The creation screen uses all of it (its
    /// info page drawn by <see cref="DrawInfo"/>); the Stylist's and the dresser's windows only the hair or the clothes.
    /// Positions are UICharacterCreation's, in Terraria pixels from the panel's top left (500 x 230), times <see cref="TerraUi.S"/>.
    /// </summary>
    internal sealed class CharCreationEditor
    {
        public enum Category { CharInfo, Clothing, HairStyle, HairColor, Eye, Skin, Shirt, Undershirt, Pants, Shoes }

        public Category Selected { get; private set; }
        /// <summary>The categories offered, at UICharacterCreation's fixed places (Compact: side by side in the middle).</summary>
        readonly Category[] offered;
        readonly bool compact;
        /// <summary>The hair styles offered (HairstyleUnlocksHelper's list for the creation screen or the Stylist).</summary>
        public List<int> Hairs = Appearance.CreationHairs;
        /// <summary>The creation screen's info page (name and play style), drawn in the middle container's rect.</summary>
        public System.Action<Rect> DrawInfo;
        /// <summary>The hover text of what the mouse is on (drawn last by the screen).</summary>
        public string Tooltip;
        /// <summary>_lastSelectedHairstyle: a hair style was picked, so a clothing style no longer brings its own.</summary>
        public bool HairPicked;

        Vector3 hsl;
        float hairView;
        bool hairDragging;
        float hairGrab;
        /// <summary>
        /// The slider held (UISliderBase.CurrentLockedSlider), whether the dev pointer holds it, and whether the Input
        /// System has had the button down since it was pressed.
        /// </summary>
        string lockedSlider;
        bool lockedByDev, lockSawDown;
        /// <summary>Where each slider takes a press (hue, saturation, lightness) and each category button, as last drawn.</summary>
        Rect sliderArea;
        readonly Rect[] sliderAreas = new Rect[3], categoryAreas = new Rect[10];

        public CharCreationEditor(Category[] categories, bool compact)
        {
            offered = categories;
            this.compact = compact;
            Selected = categories[0];
        }

        static Rect R(Rect panel, float x, float y, float w, float h) =>
            new Rect(panel.x + x * TerraUi.S, panel.y + y * TerraUi.S, w * TerraUi.S, h * TerraUi.S);

        static bool IsColor(Category c) => c >= Category.HairColor;

        // The hover ids, made once (the in-game windows draw every frame, and play makes no garbage).
        static readonly string[] hairIds = new string[Appearance.HairCount], catIds = new string[10], clothIds = new string[Appearance.VariantCount], hslIds = new string[3];
        static string Id(string[] cache, string prefix, int i) => cache[i] ??= prefix + i;

        internal static int ColorOf(Appearance a, Category c)
        {
            switch (c)
            {
                case Category.HairColor: return a.HairColor;
                case Category.Eye: return a.EyeColor;
                case Category.Skin: return a.SkinColor;
                case Category.Shirt: return a.ShirtColor;
                case Category.Undershirt: return a.UnderShirtColor;
                case Category.Pants: return a.PantsColor;
                default: return a.ShoeColor;
            }
        }

        /// <summary>ApplyPendingColor: the chosen category's colour set.</summary>
        static void SetColor(Appearance a, Category c, int rgb)
        {
            switch (c)
            {
                case Category.HairColor: a.HairColor = rgb; break;
                case Category.Eye: a.EyeColor = rgb; break;
                case Category.Skin: a.SkinColor = rgb; break;
                case Category.Shirt: a.ShirtColor = rgb; break;
                case Category.Undershirt: a.UnderShirtColor = rgb; break;
                case Category.Pants: a.PantsColor = rgb; break;
                case Category.Shoes: a.ShoeColor = rgb; break;
            }
        }

        /// <summary>SelectColorPicker / Click_ClothStyles / Click_HairStyles / Click_CharInfo.</summary>
        public void Select(Category c, Appearance a)
        {
            Selected = c;
            if (IsColor(c)) hsl = Appearance.RgbToScaledHsl(Appearance.ToColor(ColorOf(a, c)));
        }

        static readonly string[] categoryTextures =
        {
            "UI/CharCreation/CharInfo", null, "UI/CharCreation/HairStyle_Hair", "UI/CharCreation/ColorHair", "UI/CharCreation/ColorEye",
            "UI/CharCreation/ColorSkin", "UI/CharCreation/ColorShirt", "UI/CharCreation/ColorUndershirt", "UI/CharCreation/ColorPants",
            "UI/CharCreation/ColorShoes",
        };
        static readonly string[] categoryKeys =
        {
            "UI.PlayerCreateCategoryInfo", "UI.PlayerCreateCategoryBodyStyle", "UI.PlayerCreateCategoryHairStyle", "UI.PlayerCreateCategoryHairColor",
            "UI.PlayerCreateCategoryEyeColor", "UI.PlayerCreateCategorySkinColor", "UI.PlayerCreateCategoryShirtColor",
            "UI.PlayerCreateCategoryUndershirtColor", "UI.PlayerCreateCategoryPantsColor", "UI.PlayerCreateCategoryShoesColor",
        };
        static readonly string[] categoryZh = { "信息", "类型", "发型", "头发", "眼睛", "皮肤", "衬衫", "内衣", "裤装", "鞋子" };

        internal static string TT(string key, string zh) => TerrariaText.Get(Lang.Culture, key) ?? Lang.T(zh);

        /// <summary>The whole inside of the panel: the category row, its separator, the chosen page.</summary>
        public void Draw(Rect panel, Appearance a)
        {
            Tooltip = null;
            DrawCategories(panel, a);
            TerraUi.Separator(panel.x + 10f * TerraUi.S, panel.y + 52f * TerraUi.S, 475f * TerraUi.S);
            var middle = R(panel, 0f, 59f, 500f, 157f);
            switch (Selected)
            {
                case Category.CharInfo: DrawInfo?.Invoke(middle); break;
                case Category.Clothing: DrawClothStyles(panel, a); break;
                case Category.HairStyle: DrawHairStyles(panel, a); break;
                default: DrawHsl(panel, a); break;
            }
        }

        // ------------------------------------------------------------------ the category row (MakeCategoriesBar)

        void DrawCategories(Rect panel, Appearance a)
        {
            var back = TerraUi.Tex("UI/CharCreation/CategoryPanel");
            float bw = back != null ? back.width : 44f, bh = back != null ? back.height : 44f;
            for (int k = 0; k < offered.Length; k++)
            {
                var c = offered[k];
                // CreateColorPicker: Left = -240 + id * 48 from the middle; a window with fewer side by side, centred.
                float x = compact ? 250f - (offered.Length * 48f - 4f) / 2f + k * 48f : 250f - 240f + (int)c * 48f;
                var r = R(panel, x, 4f, bw, bh);
                categoryAreas[(int)c] = r;
                bool over = TerraUi.Hover(r, Id(catIds, "cat", (int)c));
                if (over) Tooltip = TT(categoryKeys[(int)c], categoryZh[(int)c]);
                ColoredImageButton(r, back, "UI/CharCreation/CategoryPanelBorder", over, Selected == c);
                var center = r.center;
                if (c == Category.Clothing)
                {
                    // The clothing button: a little character in the look's seven colours (ColorCharacter's seven frames).
                    var ch = TerraUi.Tex("UI/CharCreation/ColorCharacter");
                    if (ch != null)
                    {
                        float fh = ch.height / 7f;
                        var cols = new[] { a.HairColor, a.EyeColor, a.SkinColor, a.ShirtColor, a.UnderShirtColor, a.PantsColor, a.ShoeColor };
                        var dst = new Rect(center.x - ch.width * TerraUi.S / 2f, center.y - fh * TerraUi.S / 2f, ch.width * TerraUi.S, fh * TerraUi.S);
                        for (int i = 0; i < 7; i++) TerraUi.Part(dst, ch, 0f, i * fh, ch.width, fh, Appearance.ToColor(cols[i]));
                    }
                }
                else
                {
                    if (c == Category.Eye) Centered(center, TerraUi.Tex("UI/CharCreation/ColorEyeBack"), Color.white);
                    if (c == Category.HairStyle) Centered(center, TerraUi.Tex("UI/CharCreation/HairStyle_Arrow"), Color.white);
                    var tint = c == Category.CharInfo ? Color.white : c == Category.HairStyle ? Appearance.ToColor(a.HairColor) : Appearance.ToColor(ColorOf(a, c));
                    Centered(center, TerraUi.Tex(categoryTextures[(int)c]), tint);
                }
                if (TerraUi.Clicked(r))
                {
                    TerraUi.Sound("Menu_Tick");
                    Select(c, a);
                }
            }
        }

        /// <summary>UIColoredImageButton's back: the panel full under the mouse (0.4 else), its border then, its highlight when chosen.</summary>
        internal static void ColoredImageButton(Rect r, Texture2D back, string border, bool over, bool selected)
        {
            TerraUi.Whole(r, back, new Color(1f, 1f, 1f, over ? 1f : 0.4f));
            if (over) Centered(r.center, TerraUi.Tex(border), Color.white);
            if (selected) Centered(r.center, TerraUi.Tex("UI/CharCreation/CategoryPanelHighlight"), Color.white);
        }

        internal static void Centered(Vector2 center, Texture2D t, Color c)
        {
            if (t == null) return;
            float w = t.width * TerraUi.S, h = t.height * TerraUi.S;
            TerraUi.Whole(new Rect(center.x - w / 2f, center.y - h / 2f, w, h), t, c);
        }

        /// <summary>A small UIColoredImageButton (SmallPanel back, Copy/Paste/Randomize): true when pressed.</summary>
        internal static bool SmallButton(Rect r, string texture, string id, string tip, ref string tooltip)
        {
            bool over = TerraUi.Hover(r, id);
            if (over) tooltip = tip;
            ColoredImageButton(r, TerraUi.Tex("UI/CharCreation/SmallPanel"), "UI/CharCreation/SmallPanelBorder", over, false);
            Centered(r.center, TerraUi.Tex(texture), Color.white);
            if (!TerraUi.Clicked(r)) return false;
            TerraUi.Sound("Menu_Tick");
            return true;
        }

        internal static Vector2 SmallSize
        {
            get
            {
                var t = TerraUi.Tex("UI/CharCreation/SmallPanel");
                return t != null ? new Vector2(t.width, t.height) : new Vector2(32f, 32f);
            }
        }

        /// <summary>Dev: where a slider (0 hue, 1 saturation, 2 lightness) takes a press, as last drawn.</summary>
        internal Rect DevSlider(int i) => sliderAreas[i];
        /// <summary>Dev: a category's button, as last drawn.</summary>
        internal Rect DevCategory(Category c) => categoryAreas[(int)c];
        /// <summary>Dev: the colour page's hue, saturation and lightness (0 to 1, the sliders' values).</summary>
        internal Vector3 DevHsl => hsl;

        // ------------------------------------------------------------------ the colours (MakeHSLMenu)

        void DrawHsl(Rect panel, Appearance a)
        {
            // A 220 x 158 container at the top of the middle; the sliders' panel 104 high, 10 down.
            TerraUi.Panel(R(panel, 140f, 69f, 220f, 104f), TerraUi.PanelBack, Color.black);
            for (int id = 0; id < 3; id++)
            {
                var row = R(panel, 140f, 72f + 30f * id, 210f, 30f);
                float v = id == 0 ? hsl.x : id == 1 ? hsl.y : hsl.z;
                int which = id;
                float nv = Slider(row, v, Id(hslIds, "hsl", id), x => which == 0 ? Appearance.ScaledHslToRgb(x, 1f, 0.5f)
                    : which == 1 ? Appearance.ScaledHslToRgb(hsl.x, x, hsl.z) : Appearance.ScaledHslToRgb(hsl.x, hsl.y, x));
                sliderAreas[id] = sliderArea;
                if (nv != v)
                {
                    if (id == 0) hsl.x = nv;
                    else if (id == 1) hsl.y = nv;
                    else hsl.z = nv;
                    SetColor(a, Selected, Appearance.ToRgb(Appearance.ScaledHslToRgb(hsl.x, hsl.y, hsl.z)));
                }
            }
            // The hex code under them, right; copy, paste and a random colour, left.
            var hex = R(panel, 260f, 185f, 100f, 32f);
            TerraUi.Panel(hex, TerraUi.PanelBack, Color.black);
            TerraUi.Text(hex, Appearance.Hex(ColorOf(a, Selected)), TerraUi.Normal * TerraUi.S, Color.white, TextAnchor.MiddleCenter);
            var small = SmallSize;
            float y = 59f + 158f - small.y;
            if (SmallButton(R(panel, 140f, y, small.x, small.y), "UI/CharCreation/Copy", "hexcopy", TT("UI.CopyColorToClipboard", "将颜色代码复制到剪贴板"), ref Tooltip))
                GUIUtility.systemCopyBuffer = Appearance.Hex(ColorOf(a, Selected));
            if (SmallButton(R(panel, 180f, y, small.x, small.y), "UI/CharCreation/Paste", "hexpaste", TT("UI.PasteColorFromClipboard", "从剪贴板粘贴颜色代码"), ref Tooltip)
                && Appearance.ParseHex(GUIUtility.systemCopyBuffer, out int rgb))
            {
                hsl = Appearance.RgbToScaledHsl(Appearance.ToColor(rgb));
                SetColor(a, Selected, Appearance.ToRgb(Appearance.ScaledHslToRgb(hsl.x, hsl.y, hsl.z)));
            }
            if (SmallButton(R(panel, 220f, y, small.x, small.y), "UI/CharCreation/Randomize", "hexrandom", TT("UI.RandomizeColor", "随机颜色"), ref Tooltip))
            {
                hsl = new Vector3(Random.value, Random.value, Random.value);
                SetColor(a, Selected, Appearance.ToRgb(Appearance.ScaledHslToRgb(hsl.x, hsl.y, hsl.z)));
            }
        }

        /// <summary>
        /// UIColoredSlider.DrawValueBar: ColorBar at the row's right, 167 blips of the colour at each point along it, the
        /// highlight while the mouse is on it or holds it, the knob at the value. A press on the bar or its knob takes hold
        /// of it, and the value follows the mouse until the button is let go.
        /// The hold is the press's own IMGUI events, as the buttons' clicks are: MouseDown takes it, MouseUp ends it. Unity
        /// hands OnGUI the mouse's press before the Input System has read that frame (its button is still up on the press
        /// itself), so the Input System ends a hold only once it has had the button down. The dev pointer's click
        /// (TerraCursor.DevClick) holds it while TerraCursor.DevHeld.
        /// </summary>
        float Slider(Rect row, float value, string id, System.Func<float, Color> blip)
        {
            float S = TerraUi.S;
            var bar = TerraUi.Tex("ColorBar");
            var dot = TerraUi.Tex("ColorBlip");
            var knob = TerraUi.Tex("ColorSlider");
            float bw = (bar != null ? bar.width : 178f) * S, bh = (bar != null ? bar.height : 16f) * S;
            var barRect = new Rect(row.xMax - 10f * S - bw, row.y + 18f * S - bh / 2f, bw, bh);
            if (bar != null) TerraUi.Whole(barRect, bar, Color.white);
            float x0 = barRect.x + 5f * S, y0 = barRect.y + 4f * S;
            if (dot != null && Event.current.type == EventType.Repaint)
                for (int i = 0; i < 167; i++)
                    TerraUi.Whole(new Rect(x0 + i * S, y0, dot.width * S, dot.height * S), dot, blip(i / 167f));
            // The value's span (the strip of blips), and where a press takes it: the whole bar and the knob over it.
            var hit = new Rect(x0 - 2f * S, y0, bw - 4f * S, bh - 8f * S);
            float kw = knob != null ? knob.width * S : 0f, kh = knob != null ? knob.height * S : 0f;
            float top = Mathf.Min(barRect.y, y0 + 4f * S - kh / 2f), bottom = Mathf.Max(barRect.yMax, y0 + 4f * S + kh / 2f);
            sliderArea = new Rect(hit.x - kw / 2f, top, hit.width + kw, bottom - top);
            bool over = TerraUi.Hover(sliderArea, id);
            bool devPress = DevPressed(sliderArea);
            if (devPress || TerraUi.Clicked(sliderArea))
            {
                lockedSlider = id;
                lockedByDev = devPress;
                lockSawDown = false;
                TerraUi.Sound("Menu_Tick");
            }
            else if (lockedSlider == id && Released()) lockedSlider = null;
            if (over || lockedSlider == id) TerraUi.Whole(barRect, TerraUi.Tex("UI/Slider_Highlight"), TerraUi.FatButtonHover);
            if (lockedSlider == id) value = Mathf.Clamp01((TerraUi.Mouse.x - hit.x) / Mathf.Max(1f, hit.width));
            if (knob != null) TerraUi.Whole(new Rect(x0 + 167f * S * value - kw / 2f, y0 + 4f * S - kh / 2f, kw, kh), knob, Color.white);
            return value;
        }

        /// <summary>The dev pointer's click on it (TerraCursor.DevClick), taken in the frame's repaint.</summary>
        static bool DevPressed(Rect r) =>
            TerraCursor.DevDrives && Event.current.type == EventType.Repaint && TerraCursor.LeftPressed(null) && TerraUi.Over(r);

        /// <summary>
        /// The held slider let go: the button's MouseUp (wherever it is used), or the Input System's button up after it had
        /// it down (a release the window missed); held by the dev pointer, its button up.
        /// </summary>
        bool Released()
        {
            if (lockedByDev) return !TerraCursor.LeftHeld(null);
            var e = Event.current;
            if (e.rawType == EventType.MouseUp && e.button == 0) return true;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            bool down = mouse != null && mouse.leftButton.isPressed;
            if (down) lockSawDown = true;
            return lockSawDown && !down;
        }

        // ------------------------------------------------------------------ the hair styles (MakeHairstylesMenu)

        void DrawHairStyles(Rect panel, Appearance a)
        {
            float S = TerraUi.S;
            // The list 4 px in from a container 10 narrower than the panel, the scrollbar at its right.
            var list = R(panel, 9f, 66f, 464f, 146f);
            int count = Hairs.Count, rows = (count + 9) / 10;
            float total = rows * 48f * S + 4f * S;
            var e = Event.current;
            if (e.type == EventType.ScrollWheel && TerraUi.Input && list.Contains(e.mousePosition))
            {
                hairView += e.delta.y * 16f * S;
                e.Use();
            }
            hairView = Mathf.Clamp(hairView, 0f, Mathf.Max(0f, total - list.height));
            hairView = TerraUi.Scrollbar(R(panel, 475f, 72f, 20f, 130f), hairView, list.height, total, ref hairDragging, ref hairGrab);
            var back = TerraUi.Tex("UI/CharCreation/CategoryPanel");
            TerraUi.BeginClip(list);
            for (int i = 0; i < count; i++)
            {
                var r = new Rect(((i % 10) * 46f + 6f) * S, ((i / 10) * 48f + 1f) * S - hairView, 44f * S, 44f * S);
                if (r.yMax < 0f || r.y > list.height) continue;
                int hair = Hairs[i];
                bool over = TerraUi.Hover(r, Id(hairIds, "hair", hair));
                // UIImageButton's back (full under the mouse), the highlight on the one worn, the border under the mouse.
                TerraUi.Whole(r, back, new Color(1f, 1f, 1f, over ? 1f : 0.4f));
                if (a.Hair == hair) Centered(r.center, TerraUi.Tex("UI/CharCreation/CategoryPanelHighlight"), Color.white);
                if (over) Centered(r.center, TerraUi.Tex("UI/CharCreation/CategoryPanelBorder"), Color.white);
                // DrawPlayerHead with this hair, the head in the middle of the button.
                var head = new Rect(r.center.x - 20f * S, r.center.y - 16f * S, 40f * S, 28f * S);
                CharacterPortrait.DrawHead(head, a, Color.clear, true, 0, hair);
                if (TerraUi.Clicked(r))
                {
                    a.Hair = hair;
                    HairPicked = true;
                    TerraUi.Sound("Menu_Tick");
                }
            }
            TerraUi.EndClip();
        }

        // ------------------------------------------------------------------ the clothing styles (MakeClothStylesMenu)

        void DrawClothStyles(Rect panel, Appearance a)
        {
            float S = TerraUi.S;
            var styles = Appearance.ClothStyles;
            var back = TerraUi.Tex("UI/CharCreation/CategoryPanel");
            for (int i = 0; i < styles.Length; i++)
            {
                int v = styles[i];
                var r = R(panel, i * 46f + (i < styles.Length / 2 ? 11f : 29f), 59f, 44f, 80f);
                bool over = TerraUi.Hover(r, Id(clothIds, "cloth", v));
                TerraUi.Spliced(r, back, 10, new Color(1f, 1f, 1f, 0.5f));
                if (a.Variant == v) TerraUi.Spliced(new Rect(r.x + 3f * S, r.y + 3f * S, r.width - 6f * S, r.height - 6f * S), TerraUi.Tex("UI/CharCreation/CategoryPanelHighlight"), 10, Color.white);
                if (over) TerraUi.Spliced(r, TerraUi.Tex("UI/CharCreation/CategoryPanelBorder"), 10, Color.white);
                // PreparePreview_ClothStyle: facing as the style's body (men right, women left), in the style's own hair until one is picked.
                int hair = !HairPicked && Appearance.DefaultHairFor.TryGetValue(v, out int h) ? h : a.Hair;
                bool right = Appearance.IsMale(v);
                if (over) CharacterPortrait.DrawWalking(r, a, right, S, v, hair);
                else CharacterPortrait.Draw(r, a, 0, right, S, v, hair);
                if (TerraUi.Clicked(r))
                {
                    a.Variant = v;
                    if (!HairPicked) a.Hair = hair;
                    TerraUi.Sound("Menu_Tick");
                }
            }
            TerraUi.Separator(panel.x + 22f * S, panel.y + 145f * S, 462f * S);
            // The player template: copy, paste, randomize (Terraria's armor preview and voice buttons have no use here).
            var small = SmallSize;
            float y = 200f - small.y / 2f;
            if (SmallButton(R(panel, 200f, y, small.x, small.y), "UI/CharCreation/Copy", "tplcopy", TT("UI.CopyPlayerToClipboard", "将玩家模板复制到剪贴板"), ref Tooltip))
                GUIUtility.systemCopyBuffer = a.ToTemplate();
            if (SmallButton(R(panel, 250f - small.x / 2f, y, small.x, small.y), "UI/CharCreation/Paste", "tplpaste", TT("UI.PastePlayerFromClipboard", "从剪贴板粘贴玩家模板"), ref Tooltip))
            {
                int before = a.Hair;
                if (a.FromTemplate(GUIUtility.systemCopyBuffer, Hairs) && a.Hair != before) HairPicked = true;
            }
            if (SmallButton(R(panel, 300f - small.x, y, small.x, small.y), "UI/CharCreation/Randomize", "tplrandom", TT("UI.RandomizePlayer", "随机玩家"), ref Tooltip))
            {
                a.Randomize();
                HairPicked = true;
            }
        }
    }
}
