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
            color = new Color(MouseColor.r * alpha, MouseColor.g * alpha, MouseColor.b * alpha, alpha * 0.3f + 0.7f);
            scale = alpha * 0.3f + 0.8f;
            if (blank == null)
            {
                blank = new Texture2D(16, 16, TextureFormat.RGBA32, false) { name = "Empty Cursor" };
                blank.SetPixels32(new Color32[16 * 16]);
                blank.Apply();
                Cursor.SetCursor(blank, Vector2.zero, CursorMode.Auto);
            }
        }

        /// <summary>Drawn last of the mod's interface (TerraMod.OnGUI), so it is over all of it.</summary>
        public static void Draw()
        {
            DevCalls++;
            if (!Enabled || !Cursor.visible && DevAt == null || Event.current.type != EventType.Repaint) return;
            bool smart = TerraMod.Instance != null && TerraMod.Instance.SmartCursor && GameHooks.IsGameplay(GameManager.UnsafeInstance);
            var main = TerrariaAssets.Texture(smart ? "UI/Cursor_1" : "UI/Cursor_0");
            var border = TerrariaAssets.Texture(smart ? "UI/Cursor_12" : "UI/Cursor_11");
            if (main == null) return;
            float ui = GameView.Scale;
            var m = DevAt ?? Event.current.mousePosition;
            var prev = GUI.color;
            if (border != null)
            {
                // MouseBorderColor (64, 64, 64, 64), premultiplied: white at a quarter.
                GUI.color = new Color(1f, 1f, 1f, 0.25f);
                float bs = scale * 1.1f * ui;
                foreach (var o in borderOffsets)
                {
                    var at = m + (o + Vector2.one * 2f) * ui - Vector2.one * 2f * bs;
                    GUI.DrawTexture(new Rect(at.x, at.y, border.width * bs, border.height * bs), border);
                }
            }
            var bonus = Vector2.one * ui;
            GUI.color = new Color(color.r * 0.2f, color.g * 0.2f, color.b * 0.2f, color.a * 0.5f);
            GUI.DrawTexture(new Rect(m.x + bonus.x + ui, m.y + bonus.y + ui, main.width * scale * 1.1f * ui, main.height * scale * 1.1f * ui), main);
            GUI.color = color;
            GUI.DrawTexture(new Rect(m.x + bonus.x, m.y + bonus.y, main.width * scale * ui, main.height * scale * ui), main);
            GUI.color = prev;
            DevDrawn++;
        }
    }
}
