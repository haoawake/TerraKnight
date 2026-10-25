using System.Collections;
using System.Runtime.InteropServices;
using TerraKnight.Player;
using TerraKnight.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "ccslider"): the colour sliders (UI.CharCreationEditor) pressed, dragged and let go three ways: Windows'
    /// own mouse messages to this game's window (the IMGUI events a mouse gives the menus, with the Input System's mouse
    /// left alone, as a touch or a pen gives them), the same with the Input System's mouse pressed and moved alongside (a
    /// real mouse: Unity hands the IMGUI press to OnGUI before the Input System has read the frame), and the dev pointer
    /// (TerraCursor.DevClick / DevHeld). On the title's character creation: hair colour's three sliders one way each, then
    /// every colour category picked by a mouse message on its button and one of its sliders dragged. In the game: a
    /// dresser's clothes window (UI.AppearanceWindow), its shirt category picked by a mouse message and its sliders
    /// dragged the three ways, the shirt worn while it changes and the old look back on 取消. Each drag: the press moves the
    /// knob to the pointer, two moves while held move it with them, a move after letting go leaves it.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        delegate bool EnumWindowsProc(System.IntPtr hwnd, System.IntPtr param);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc proc, System.IntPtr param);
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(System.IntPtr hwnd, out uint pid);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(System.IntPtr hwnd, System.Text.StringBuilder name, int size);
        [DllImport("user32.dll")] static extern bool PostMessage(System.IntPtr hwnd, uint msg, System.IntPtr wParam, System.IntPtr lParam);

        const uint WmMouseMove = 0x0200, WmLButtonDown = 0x0201, WmLButtonUp = 0x0202;
        static System.IntPtr ownWindow;
        int sliderOk, sliderAll;

        /// <summary>This process's own Unity window (never another game's).</summary>
        static System.IntPtr OwnWindow()
        {
            if (ownWindow != System.IntPtr.Zero) return ownWindow;
            uint me = (uint)System.Diagnostics.Process.GetCurrentProcess().Id;
            EnumWindows((h, p) =>
            {
                GetWindowThreadProcessId(h, out uint pid);
                if (pid != me) return true;
                var sb = new System.Text.StringBuilder(64);
                GetClassName(h, sb, 64);
                if (sb.ToString() != "UnityWndClass") return true;
                ownWindow = h;
                return false;
            }, System.IntPtr.Zero);
            return ownWindow;
        }

        /// <summary>A mouse message to this game's window at a GUI point (its client area, top-left origin).</summary>
        static void PostMouse(uint msg, Vector2 gui, bool left)
        {
            var h = OwnWindow();
            if (h == System.IntPtr.Zero) return;
            int lp = (Mathf.RoundToInt(gui.y) << 16) | (Mathf.RoundToInt(gui.x) & 0xFFFF);
            PostMessage(h, msg, new System.IntPtr(left ? 1 : 0), new System.IntPtr(lp));
        }

        /// <summary>The Input System's mouse at a GUI point, its left button down or up.</summary>
        static void QueueMouse(Vector2 gui, bool left)
        {
            var m = Mouse.current;
            if (m == null) return;
            var st = new MouseState { position = new Vector2(gui.x, Screen.height - gui.y) };
            InputSystem.QueueStateEvent(m, left ? st.WithButton(MouseButton.Left, true) : st);
        }

        static readonly string[] pointerWays = { "窗口鼠标消息", "鼠标消息加输入系统", "测试指针" };

        static void PointerMove(int way, Vector2 gui, bool held)
        {
            if (way == 2) { TerraCursor.DevAt = gui; return; }
            PostMouse(WmMouseMove, gui, held);
            if (way == 1) QueueMouse(gui, held);
        }

        static void PointerDown(int way, Vector2 gui)
        {
            if (way == 2)
            {
                TerraCursor.DevAt = gui;
                TerraCursor.DevHeld = true;
                TerraCursor.DevClick();
                return;
            }
            PostMouse(WmLButtonDown, gui, true);
            if (way == 1) QueueMouse(gui, true);
        }

        static void PointerUp(int way, Vector2 gui)
        {
            if (way == 2) { TerraCursor.DevHeld = false; return; }
            PostMouse(WmLButtonUp, gui, false);
            if (way == 1) QueueMouse(gui, false);
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        void SliderCheck(string what, bool pass, string detail)
        {
            sliderAll++;
            if (pass) sliderOk++;
            Plugin.Log.LogInfo($"[AutoTest] 人物滑条 {what}：{detail} {(pass ? "通过" : "不通过")}");
        }

        /// <summary>
        /// One slider pressed, dragged and let go one way: pressed at one end (away from its value), moved to the middle
        /// and to the other end while held, let go and moved back to the middle.
        /// </summary>
        IEnumerator DragSlider(string tag, int way, CharCreationEditor ed, int slider, System.Func<int> color)
        {
            float Value() => slider == 0 ? ed.DevHsl.x : slider == 1 ? ed.DevHsl.y : ed.DevHsl.z;
            var area = ed.DevSlider(slider);
            // Pressed at the end away from the value (a press near it would leave the colour as it is).
            bool fromLow = Value() >= 0.5f;
            float from = fromLow ? 0.12f : 0.88f, to = fromLow ? 0.88f : 0.12f;
            Vector2 At(float k) => new Vector2(area.x + area.width * k, area.center.y);
            if (way == 2) TerraCursor.DevDrives = true;
            int c0 = color();
            float v0 = Value();
            PointerMove(way, At(from), false);
            yield return Frames(2);
            PointerDown(way, At(from));
            yield return Frames(3);
            int c1 = color();
            float v1 = Value();
            PointerMove(way, At(0.5f), true);
            yield return Frames(3);
            int c2 = color();
            float v2 = Value();
            PointerMove(way, At(to), true);
            yield return Frames(3);
            int c3 = color();
            float v3 = Value();
            PointerUp(way, At(to));
            yield return Frames(3);
            PointerMove(way, At(0.5f), false);
            yield return Frames(3);
            int c4 = color();
            float v4 = Value();
            TerraCursor.DevDrives = false;
            TerraCursor.DevHeld = false;
            TerraCursor.DevAt = null;
            bool pressed = c1 != c0 && Mathf.Abs(v1 - from) < 0.06f;
            bool follows = c2 != c1 && c3 != c2 && Mathf.Abs(v2 - 0.5f) < 0.06f && Mathf.Abs(v3 - to) < 0.06f;
            bool letGo = c4 == c3 && v4 == v3;
            SliderCheck($"{tag}（{pointerWays[way]}）", pressed && follows && letGo,
                $"滑块 {v0:F2} → 按下 {v1:F2}（指针 {from:F2}）→ 拖到 {v2:F2}（0.50）→ {v3:F2}（{to:F2}）→ 松开后移开 {v4:F2}；颜色 {c0:X6} → {c1:X6} → {c2:X6} → {c3:X6} → {c4:X6}；按下改了颜色 {pressed}，跟着拖 {follows}，松开不再动 {letGo}");
        }

        /// <summary>A category button pressed and let go by Windows' mouse messages (as the menus' buttons take a mouse).</summary>
        IEnumerator ClickCategory(string tag, CharCreationEditor ed, CharCreationEditor.Category c)
        {
            // Its picture in (one not loaded yet leaves the button without a size).
            float until = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < until && !(ed.DevCategory(c).width > 20f)) yield return null;
            var r = ed.DevCategory(c);
            PostMouse(WmMouseMove, r.center, false);
            yield return Frames(2);
            PostMouse(WmLButtonDown, r.center, true);
            yield return Frames(2);
            PostMouse(WmLButtonUp, r.center, false);
            yield return Frames(3);
            SliderCheck($"{tag} 点 {c} 按钮", ed.Selected == c, $"按钮 {r}，选中 {ed.Selected}");
        }

        /// <summary>The colour page's sliders drawn with their pictures in (a picture not loaded yet leaves a slider without a size).</summary>
        static IEnumerator SlidersReady(CharCreationEditor ed)
        {
            float until = Time.realtimeSinceStartup + 20f;
            while (Time.realtimeSinceStartup < until && !(ed.DevSlider(0).width > 20f && ed.DevSlider(2).width > 20f)) yield return null;
            yield return Frames(2);
        }

        static readonly CharCreationEditor.Category[] colourCategories =
        {
            CharCreationEditor.Category.HairColor, CharCreationEditor.Category.Eye, CharCreationEditor.Category.Skin, CharCreationEditor.Category.Shirt,
            CharCreationEditor.Category.Undershirt, CharCreationEditor.Category.Pants, CharCreationEditor.Category.Shoes,
        };

        /// <summary>The title's part (Boot, before the game is played): the character creation screen's sliders.</summary>
        IEnumerator CharSliderMenuTest()
        {
            var m = Mouse.current;
            Plugin.Log.LogInfo($"[AutoTest] 人物滑条 输入系统 {InputSystem.version}，{InputSystem.settings.updateMode}，鼠标 {(m == null ? "无" : m.displayName + (m.enabled ? "" : "（停用）"))}，本窗口 {OwnWindow()}，屏幕 {Screen.width}x{Screen.height}");
            // 开始游戏's new game: the start menu's character creation (TerraMenu.CreateFirst).
            UIManager.instance.StartNewGame(false, false);
            float until = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < until && !(CharacterCreation.IsOpen && CreativeMenu.DevStep == "Character")) yield return null;
            var ed = CharacterCreation.DevEditor;
            SliderCheck("开局 创建人物界面打开", CharacterCreation.IsOpen && ed != null, $"界面 {CreativeMenu.DevStep}");
            if (ed == null) yield break;
            yield return new WaitForSecondsRealtime(0.5f);

            yield return ClickCategory("开局", ed, CharCreationEditor.Category.HairColor);
            if (ed.Selected != CharCreationEditor.Category.HairColor) ed.Select(CharCreationEditor.Category.HairColor, CharacterCreation.Look);
            yield return SlidersReady(ed);
            Shot("Menu_Title", "ccslider_1_haircolor");
            yield return null;
            for (int way = 0; way < 3; way++)
                yield return DragSlider($"开局 头发 {(way == 0 ? "色相" : way == 1 ? "饱和度" : "亮度")}", way, ed, way,
                    () => CharacterCreation.Look.HairColor);
            Shot("Menu_Title", "ccslider_2_dragged");
            yield return null;

            // Every colour: its button, then one of its sliders by the real mouse's way (hue, saturation, lightness in turn).
            for (int k = 0; k < colourCategories.Length; k++)
            {
                var c = colourCategories[k];
                if (k > 0) yield return ClickCategory("开局", ed, c);
                if (ed.Selected != c) ed.Select(c, CharacterCreation.Look);
                yield return SlidersReady(ed);
                var cat = c;
                yield return DragSlider($"开局 {c}", 1, ed, k % 3, () => CharCreationEditor.ColorOf(CharacterCreation.Look, cat));
            }
            Shot("Menu_Title", "ccslider_3_colours");
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 人物滑条 开局 人物颜色 头发 {CharacterCreation.Look.HairColor:X6} 眼睛 {CharacterCreation.Look.EyeColor:X6} 皮肤 {CharacterCreation.Look.SkinColor:X6} 衬衫 {CharacterCreation.Look.ShirtColor:X6} 内衣 {CharacterCreation.Look.UnderShirtColor:X6} 裤装 {CharacterCreation.Look.PantsColor:X6} 鞋子 {CharacterCreation.Look.ShoeColor:X6}");

            // Back to the title, for the game's part (Boot starts the test slot's game next).
            CreativeMenu.DevBack();
            yield return new WaitForSecondsRealtime(0.3f);
            CreativeMenu.DevBack();
            yield return new WaitForSecondsRealtime(3f);
        }

        /// <summary>The game's part (Run): a dresser's clothes window.</summary>
        IEnumerator CharSliderGameTest()
        {
            PlayerData.instance.isInvincible = true;
            var worn = Appearance.Current.Clone();
            AppearanceWindow.OpenClothes();
            yield return new WaitForSecondsRealtime(0.6f);
            var ed = AppearanceWindow.DevEditor;
            var look = AppearanceWindow.DevEditing;
            SliderCheck("游戏里 衣服窗口打开", AppearanceWindow.Showing == AppearanceWindow.Kind.Clothes && ed != null, $"窗口 {AppearanceWindow.Showing}");
            if (ed == null) yield break;
            yield return ClickCategory("游戏里", ed, CharCreationEditor.Category.Shirt);
            if (ed.Selected != CharCreationEditor.Category.Shirt) ed.Select(CharCreationEditor.Category.Shirt, look);
            yield return SlidersReady(ed);
            Shot(GameManager.instance.sceneName, "ccslider_4_dresser");
            yield return null;
            for (int way = 0; way < 3; way++)
                yield return DragSlider($"游戏里 衬衫 {(way == 0 ? "色相" : way == 1 ? "饱和度" : "亮度")}", way, ed, way, () => look.ShirtColor);
            Shot(GameManager.instance.sceneName, "ccslider_5_dragged");
            yield return null;
            SliderCheck("游戏里 改色时穿在身上", AppearanceWindow.IsOpen && Appearance.Current.ShirtColor == look.ShirtColor && look.ShirtColor != worn.ShirtColor,
                $"窗口里 {look.ShirtColor:X6}，身上 {Appearance.Current.ShirtColor:X6}，原来 {worn.ShirtColor:X6}");
            AppearanceWindow.Cancel();
            yield return Frames(2);
            SliderCheck("游戏里 取消后换回原样", !AppearanceWindow.IsOpen && Appearance.Current.SameAs(worn), $"身上衬衫 {Appearance.Current.ShirtColor:X6}");
            Plugin.Log.LogInfo($"[AutoTest] 人物滑条 汇总 {sliderOk}/{sliderAll} {(sliderOk == sliderAll ? "通过" : "不通过")}");
        }
    }
}
