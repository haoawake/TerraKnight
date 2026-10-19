using BepInEx.Configuration;
using HarmonyLib;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>
    /// Terraria's mouse cursor in place of the game's (Main.DrawCursor, DrawThickCursor,
    /// UpdateCursorColor): Cursor_0 (Cursor_1 with the smart cursor on) in the default pink
    /// (255, 50, 95), pulsing between 0.6 and 1 of its brightness and size by 0.015 a tick, over a
    /// darker copy a pixel down-right, inside Cursor_11's (Cursor_12's) thin light border drawn
    /// four times a pixel apart. The system cursor stays "visible" (the game's menus take the mouse
    /// only then) but its picture is made empty, and PlayMaker's GUI cursor is kept off.
    /// The player picks (泰拉瑞亚设置 · 光标) the picture (Terraria's, or Hollow Knight's own flourish), its size, its
    /// colour (Terraria's hue, saturation and lightness), its glow (none, Terraria's light rim, up to a halo of its colour)
    /// and whether it pulses.
    /// </summary>
    internal sealed class TerraCursor : MonoBehaviour
    {
        public static bool Enabled = true;
        /// <summary>Dev: draw the cursor at a GUI point instead of the mouse (pictures; the real cursor isn't moved).</summary>
        public static Vector2? DevAt;
        /// <summary>
        /// Dev: the interface answers to DevAt as it would to the mouse (hover, clicks), for the trailer's
        /// recordings: the mod's panels read the pointer through <see cref="GuiMouse"/>, <see cref="ScreenMouse"/>
        /// and <see cref="LeftPressed"/>. A click is <see cref="DevClick"/>; DevHeld keeps the button down.
        /// </summary>
        public static bool DevDrives, DevHeld;
        static int devPressFrame = -1;
        public static int DevDrawn, DevCalls;

        /// <summary>Dev: a left press by the pointer, seen by everything on the next frame.</summary>
        public static void DevClick() => devPressFrame = Time.frameCount + 1;

        static bool Driven => DevDrives && DevAt != null;

        /// <summary>The GUI point (top-left origin) the interface answers to, in OnGUI: the dev pointer when it drives, else the mouse.</summary>
        public static Vector2 GuiMouse() => Driven ? DevAt.Value : Event.current.mousePosition;

        /// <summary>The screen point (bottom-left origin) the interface answers to, outside OnGUI.</summary>
        public static Vector2 ScreenMouse(UnityEngine.InputSystem.Mouse m) =>
            Driven ? new Vector2(DevAt.Value.x, Screen.height - DevAt.Value.y) : m != null ? m.position.ReadValue() : Vector2.zero;

        /// <summary>The left button went down this frame (the mouse's, or the dev pointer's click).</summary>
        public static bool LeftPressed(UnityEngine.InputSystem.Mouse m) => m != null && m.leftButton.wasPressedThisFrame || DevDrives && Time.frameCount == devPressFrame;

        /// <summary>The left button is down (the mouse's, or the dev pointer's).</summary>
        public static bool LeftHeld(UnityEngine.InputSystem.Mouse m) => m != null && m.leftButton.isPressed || DevDrives && (DevHeld || Time.frameCount == devPressFrame);
        public static string DevInfo()
        {
            var main = TerrariaAssets.Texture("UI/Cursor_0");
            var border = TerrariaAssets.Texture("UI/Cursor_11");
            return $"画了 {DevDrawn} 次（OnGUI {DevCalls} 次），系统光标显示 {Cursor.visible}，Cursor_0 {(main != null ? main.width + "x" + main.height : "没有")}，Cursor_11 {(border != null ? border.width + "x" + border.height : "没有")}";
        }
        static readonly Color MouseColor = new Color(255 / 255f, 50 / 255f, 95 / 255f);
        static readonly Vector2[] borderOffsets = { new Vector2(0f, 1f), new Vector2(1f, 0f), new Vector2(0f, -1f), new Vector2(-1f, 0f) };

        static Texture2D blank;
        static float alpha = 1f, ticks;
        static int direction = -1;
        static Color color = Color.white;
        static float scale = 1f;

        public static void Install(GameObject host, Harmony harmony)
        {
            host.AddComponent<TerraCursor>();
            // PlayMaker draws a cursor of its own when a state machine sets one: never.
            try
            {
                var setter = AccessTools.PropertySetter(typeof(PlayMakerGUI), "MouseCursor");
                if (setter != null) harmony.Patch(setter, prefix: new HarmonyMethod(typeof(TerraCursor), nameof(NoPlayMakerCursor)));
            }
            catch (System.Exception e) { Plugin.Log.LogWarning($"[光标] 没能拦下 PlayMaker 的光标：{e.Message}"); }
        }

        static void NoPlayMakerCursor(ref Texture value)
        {
            if (Enabled) value = null;
        }

        // ------------------------------------------------------------------ the player's choices (泰拉瑞亚设置 · 光标)

        /// <summary>The cursor's picture: Terraria's, or Hollow Knight's own (the game's default cursor, a white flourish).</summary>
        public enum Style { Terraria = 0, HollowKnight = 1 }

        static ConfigEntry<int> styleSetting;
        static ConfigEntry<float> sizeSetting, glowSetting;
        static ConfigEntry<string> colorSetting;
        static ConfigEntry<bool> pulseSetting;

        public const float MinSize = 0.5f, MaxSize = 3f;
        /// <summary>The glow Terraria's own border has (MouseBorderColor's quarter white): above it a halo of the cursor's colour grows.</summary>
        public const float TerrariaGlow = 0.25f;

        public static Style CurrentStyle = Style.Terraria;
        public static float Size = 1f;
        public static float Glow = TerrariaGlow;
        public static bool Pulse = true;
        /// <summary>The colour chosen, or none for the style's own (Terraria's pink, Hollow Knight's white).</summary>
        public static Color? CustomColor;

        public static void Bind(ConfigFile cfg)
        {
            styleSetting = cfg.Bind("Cursor", "Style", 0,
                "鼠标指针的样式：0 = 泰拉瑞亚，1 = 空洞骑士自己的指针。游戏里“泰拉瑞亚设置 → 光标”也能改。\nThe mouse cursor's look: 0 = Terraria's, 1 = Hollow Knight's own. Also in game under \"Terraria settings → Cursor\".");
            sizeSetting = cfg.Bind("Cursor", "Size", 1f, $"鼠标指针的大小（{MinSize} 到 {MaxSize} 倍）。\nThe cursor's size ({MinSize} to {MaxSize} times).");
            colorSetting = cfg.Bind("Cursor", "Color", "",
                "鼠标指针的颜色（RRGGBB 十六进制，例如 FF325F）；留空用样式自己的颜色（泰拉瑞亚粉色、空洞骑士白色）。\nThe cursor's colour (RRGGBB hex, e.g. FF325F); empty for the style's own (Terraria's pink, Hollow Knight's white).");
            glowSetting = cfg.Bind("Cursor", "Glow", TerrariaGlow,
                "鼠标指针的发光强度（0 到 1）：0 没有描边，0.25 是泰拉瑞亚原样的浅色描边，再高加上同色光晕。\nThe cursor's glow (0 to 1): 0 no rim, 0.25 Terraria's own light rim, higher adds a halo of the cursor's colour.");
            pulseSetting = cfg.Bind("Cursor", "Pulse", true,
                "鼠标指针像泰拉瑞亚那样轻微地明暗、大小起伏（呼吸闪烁）。\nThe cursor gently pulses in brightness and size, as Terraria's does.");
            CurrentStyle = styleSetting.Value == 1 ? Style.HollowKnight : Style.Terraria;
            Size = Mathf.Clamp(sizeSetting.Value, MinSize, MaxSize);
            Glow = Mathf.Clamp01(glowSetting.Value);
            Pulse = pulseSetting.Value;
            CustomColor = ParseColor(colorSetting.Value);
        }

        /// <summary>The choices into the config file (after a slider is let go, not on every frame of a drag).</summary>
        public static void Save()
        {
            if (styleSetting == null) return;
            styleSetting.Value = (int)CurrentStyle;
            sizeSetting.Value = Size;
            glowSetting.Value = Glow;
            pulseSetting.Value = Pulse;
            colorSetting.Value = CustomColor.HasValue ? ColorUtility.ToHtmlStringRGB(CustomColor.Value) : "";
        }

        /// <summary>Every cursor choice back to its default (Terraria's cursor as Terraria draws it).</summary>
        public static void ResetLook()
        {
            CurrentStyle = Style.Terraria;
            Size = 1f;
            Glow = TerrariaGlow;
            Pulse = true;
            CustomColor = null;
            Save();
        }

        static Color? ParseColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return null;
            return ColorUtility.TryParseHtmlString("#" + hex.Trim().TrimStart('#'), out var c) ? new Color(c.r, c.g, c.b, 1f) : (Color?)null;
        }

        /// <summary>The style's own colour: Terraria's pink (Main.mouseColor), Hollow Knight's white.</summary>
        public static Color DefaultColor(Style s) => s == Style.Terraria ? MouseColor : Color.white;

        /// <summary>The colour the cursor is drawn in before its pulse.</summary>
        public static Color BaseColor => CustomColor ?? DefaultColor(CurrentStyle);

        // ------------------------------------------------------------------ Hollow Knight's own cursor

        static Texture2D hkCursor;
        static float nextHkSearch;
        static bool hkMissingLogged;

        /// <summary>
        /// The game's default cursor (PlayerSettings' "Cursor", 37×37, white on clear, its point at (3, 0)), found among
        /// the textures loaded; none while it can't be found (the game's own system cursor shows then).
        /// </summary>
        public static Texture2D HkCursor()
        {
            if (hkCursor != null || Time.unscaledTime < nextHkSearch) return hkCursor;
            nextHkSearch = Time.unscaledTime + 5f;
            foreach (var t in Resources.FindObjectsOfTypeAll<Texture2D>())
                if (t != null && t.name == "Cursor" && t.width == 37 && t.height == 37) { hkCursor = t; break; }
            if (hkCursor == null)
            {
                // A scan of every texture loaded: not every few seconds while it stays missing.
                nextHkSearch = Time.unscaledTime + 30f;
                if (!hkMissingLogged)
                {
                    hkMissingLogged = true;
                    Plugin.Log.LogWarning("[光标] 没找到空洞骑士自己的指针贴图，改用游戏的系统指针");
                }
            }
            return hkCursor;
        }

        static readonly Vector2 HkHotspot = new Vector2(3f, 0f);

        // ------------------------------------------------------------------ the glow: a blurred copy of the picture

        const int HaloPad = 6;
        static readonly System.Collections.Generic.Dictionary<Texture2D, Texture2D> halos = new System.Collections.Generic.Dictionary<Texture2D, Texture2D>();

        /// <summary>The picture's shape blurred (white, the alpha spread by three box blurs), padded by <see cref="HaloPad"/> each side.</summary>
        static Texture2D HaloOf(Texture2D src, string terrariaName)
        {
            if (src == null) return null;
            if (halos.TryGetValue(src, out var made)) return made;
            Color32[] px;
            int w, h;
            try
            {
                if (terrariaName != null) px = TerrariaAssets.Pixels(terrariaName, out w, out h);
                else { px = src.GetPixels32(); w = src.width; h = src.height; }
            }
            catch (System.Exception)
            {
                halos[src] = null;
                return null;
            }
            if (px == null) { halos[src] = null; return null; }
            int W = w + 2 * HaloPad, H = h + 2 * HaloPad;
            var a = new float[W * H];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) a[(y + HaloPad) * W + x + HaloPad] = px[y * w + x].a / 255f;
            var tmp = new float[W * H];
            for (int pass = 0; pass < 3; pass++)
            {
                BoxBlur(a, tmp, W, H, 2, true);
                BoxBlur(tmp, a, W, H, 2, false);
            }
            float max = 0f;
            foreach (var v in a) max = Mathf.Max(max, v);
            var outPx = new Color32[W * H];
            for (int i = 0; i < outPx.Length; i++) outPx[i] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(max > 0f ? a[i] / max : 0f) * 255f));
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = src.name + " Halo", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            tex.SetPixels32(outPx);
            tex.Apply(false, true);
            halos[src] = tex;
            return tex;
        }

        static void BoxBlur(float[] from, float[] to, int w, int h, int r, bool horizontal)
        {
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float sum = 0f;
                    int n = 0;
                    for (int k = -r; k <= r; k++)
                    {
                        int xx = horizontal ? x + k : x, yy = horizontal ? y : y + k;
                        if (xx < 0 || yy < 0 || xx >= w || yy >= h) { n++; continue; }
                        sum += from[yy * w + xx];
                        n++;
                    }
                    to[y * w + x] = sum / n;
                }
        }

        // ------------------------------------------------------------------ each frame

        static bool hardwareBlank;

        void Update()
        {
            if (!Enabled) return;
            // Main.UpdateCursorColor, 60 times a second.
            ticks += Time.unscaledDeltaTime * 60f;
            while (ticks >= 1f)
            {
                ticks -= 1f;
                alpha += direction * 0.015f;
                if (alpha >= 1f) { alpha = 1f; direction = -1; }
                if (alpha <= 0.6f) { alpha = 0.6f; direction = 1; }
            }
            float k = Pulse ? alpha : 1f;
            var c = BaseColor;
            color = new Color(c.r * k, c.g * k, c.b * k, k * 0.3f + 0.7f);
            scale = k * 0.3f + 0.8f;
            if (blank == null)
            {
                blank = new Texture2D(16, 16, TextureFormat.RGBA32, false) { name = "Empty Cursor" };
                blank.SetPixels32(new Color32[16 * 16]);
                blank.Apply();
            }
            // The system cursor's picture is made empty while the mod draws its own; Hollow Knight's style without its
            // texture found gives the game its own cursor back.
            bool wantBlank = !(CurrentStyle == Style.HollowKnight && HkCursor() == null);
            if (wantBlank != hardwareBlank)
            {
                Cursor.SetCursor(wantBlank ? blank : null, Vector2.zero, CursorMode.Auto);
                hardwareBlank = wantBlank;
            }
        }

        /// <summary>Drawn last of the mod's interface (TerraMod.OnGUI), so it is over all of it.</summary>
        public static void Draw()
        {
            DevCalls++;
            if (!Enabled || !Cursor.visible && DevAt == null || Event.current.type != EventType.Repaint) return;
            if (DrawAt(DevAt ?? Event.current.mousePosition, GameView.Scale, color, scale)) DevDrawn++;
        }

        /// <summary>The cursor as the current choices draw it, pulsing as it does (the settings' preview), its point at a GUI spot.</summary>
        public static void DrawPreview(Vector2 at, float ui) => DrawAt(at, ui, color, scale);

        /// <summary>
        /// Main.DrawCursor / DrawThickCursor: the halo (above Terraria's glow), the light rim (Cursor_11, or the flourish's
        /// own outline) drawn four times a pixel apart, a darker copy a pixel down-right, then the cursor in its colour.
        /// </summary>
        static bool DrawAt(Vector2 m, float ui, Color tint, float pulseScale)
        {
            bool hk = CurrentStyle == Style.HollowKnight;
            bool smart = !hk && TerraMod.Instance != null && TerraMod.Instance.SmartCursor && GameHooks.IsGameplay(GameManager.UnsafeInstance);
            string mainName = smart ? "UI/Cursor_1" : "UI/Cursor_0";
            var main = hk ? HkCursor() : TerrariaAssets.Texture(mainName);
            var border = hk ? null : TerrariaAssets.Texture(smart ? "UI/Cursor_12" : "UI/Cursor_11");
            if (main == null) return false;
            // Terraria's picture pulses between 0.8 and 1.1 of its size; the flourish is the game's 37 pixels at 1.1.
            float s = pulseScale * Size * ui * (hk ? 1f / 1.1f : 1f);
            var origin = hk ? m - HkHotspot * s : m + Vector2.one * ui;
            var prev = GUI.color;
            float halo = Mathf.Clamp01((Glow - TerrariaGlow) / (1f - TerrariaGlow)) * 0.9f;
            if (halo > 0.001f)
            {
                var haloTex = HaloOf(main, hk ? null : mainName);
                if (haloTex != null)
                {
                    var b = BaseColor;
                    GUI.color = new Color(b.r, b.g, b.b, halo);
                    float hs = s * 1.15f;
                    var center = origin + new Vector2(main.width, main.height) * s / 2f;
                    GUI.DrawTexture(new Rect(center.x - haloTex.width * hs / 2f, center.y - haloTex.height * hs / 2f, haloTex.width * hs, haloTex.height * hs), haloTex);
                }
            }
            float rim = Mathf.Clamp01(Glow / TerrariaGlow) * TerrariaGlow + Mathf.Max(0f, Glow - TerrariaGlow) * 0.6f;
            if (rim > 0.001f)
            {
                // MouseBorderColor (64, 64, 64, 64), premultiplied: white at a quarter, at the default glow.
                GUI.color = new Color(1f, 1f, 1f, Mathf.Clamp01(rim));
                if (border != null)
                {
                    float bs = s * 1.1f;
                    foreach (var o in borderOffsets)
                    {
                        var at = m + (o + Vector2.one * 2f) * ui - Vector2.one * 2f * bs;
                        GUI.DrawTexture(new Rect(at.x, at.y, border.width * bs, border.height * bs), border);
                    }
                }
                else
                {
                    foreach (var o in borderOffsets)
                        GUI.DrawTexture(new Rect(origin.x + o.x * ui * 1.5f, origin.y + o.y * ui * 1.5f, main.width * s, main.height * s), main);
                }
            }
            float shadowS = hk ? s : s * 1.1f;
            GUI.color = new Color(tint.r * 0.2f, tint.g * 0.2f, tint.b * 0.2f, tint.a * 0.5f);
            GUI.DrawTexture(new Rect(origin.x + ui, origin.y + ui, main.width * shadowS, main.height * shadowS), main);
            GUI.color = tint;
            GUI.DrawTexture(new Rect(origin.x, origin.y, main.width * s, main.height * s), main);
            GUI.color = prev;
            return true;
        }
    }
}
