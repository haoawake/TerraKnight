using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using TerraKnight.Player;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "mousebind"): bindings on the mouse's side and middle buttons. Jump put on the back
    /// side button and that button pressed: the mod's key check and the Knight's own jump action both
    /// see it and he leaves the ground. A hotbar slot on the forward one. Then a binding chosen the
    /// player's way (listening, the middle button pressed) and what the config keeps. Defaults after.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static void MouseDown(params MouseButton[] buttons)
        {
            var m = Mouse.current;
            if (m == null) return;
            var st = new MouseState();
            foreach (var b in buttons) st = st.WithButton(b);
            InputSystem.QueueStateEvent(m, st);
        }

        IEnumerator MouseBindTest()
        {
            var h = HeroController.instance;
            var rb = h.GetComponent<Rigidbody2D>();
            PlayerData.instance.isInvincible = true;
            int ok = 0, all = 0;
            void Check(string what, bool pass) { all++; if (pass) ok++; Plugin.Log.LogInfo($"[AutoTest] 侧键 {what} {(pass ? "通过" : "不通过")}"); }

            // Jump on the back button.
            TerraKeys.Set(TerraKeys.Action.Jump, TerraKeys.MouseBack);
            yield return new WaitForSeconds(0.5f);
            float t0 = Time.time;
            while ((!h.cState.onGround || !GameHooks.PlayerHasControl()) && Time.time - t0 < 5f) yield return null;
            float y0 = h.transform.position.y;
            MouseDown(MouseButton.Back);
            yield return null;
            yield return null;
            bool mine = TerraKeys.Held(TerraKeys.Action.Jump);
            bool game = GameHooks.Input.inputActions.jump.IsPressed;
            float peak = y0;
            for (float t = 0f; t < 0.4f; t += Time.deltaTime) { peak = Mathf.Max(peak, h.transform.position.y); yield return null; }
            MouseDown();
            Check($"跳跃改成鼠标侧键（后）：mod 认到 {mine}，原版跳跃动作按下 {game}，跳起 {peak - y0:F2}", mine && game && peak - y0 > 0.5f);
            yield return new WaitForSeconds(1f);
            // The keyboard's space no longer jumps (it isn't the binding now)... the game's own Space binding may stay: not asserted.

            // Hotbar slot 3 on the forward button.
            TerraKeys.Set(TerraKeys.Action.Hotbar3, TerraKeys.MouseForward);
            mod.Inventory.Selected = 0;
            yield return null;
            MouseDown(MouseButton.Forward);
            yield return null;
            yield return null;
            MouseDown();
            yield return null;
            Check($"第 3 格快捷栏改成鼠标侧键（前）：选中第 {mod.Inventory.Selected + 1} 格", mod.Inventory.Selected == 2);

            // Chosen the player's way: listening for grapple, the middle button pressed.
            TerraKeys.Listen(TerraKeys.Action.Grapple);
            yield return null;
            yield return null;
            MouseDown(MouseButton.Middle);
            yield return null;
            yield return null;
            MouseDown();
            yield return null;
            string saved = TerraKeys.DevSaved(TerraKeys.Action.Grapple) ?? "?";
            Check($"改键时按鼠标中键：抓钩 = {TerraKeys.KeyName(TerraKeys.Get(TerraKeys.Action.Grapple))}，配置存成 {saved}，还在等按键 {TerraKeys.Listening.HasValue}",
                TerraKeys.Get(TerraKeys.Action.Grapple) == TerraKeys.MouseMiddle && saved == "Mouse3" && !TerraKeys.Listening.HasValue);

            TerraKeys.ResetAll();
            yield return null;
            Check($"恢复默认：跳跃 = {TerraKeys.KeyName(TerraKeys.Get(TerraKeys.Action.Jump))}", TerraKeys.Get(TerraKeys.Action.Jump) == Key.Space);
            Plugin.Log.LogInfo($"[AutoTest] 侧键 合计 {ok}/{all}");
        }
    }
}
