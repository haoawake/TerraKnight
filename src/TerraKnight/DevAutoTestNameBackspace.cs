using System.Collections;
using HarmonyLib;
using TerraKnight.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "namebs", the user's 10-25 "创建角色起名…不能删字"): the start menu's character creation, its name typed
    /// through the Input System's keyboard as a key does it (text events, then Backspace pressed and let go, once and
    /// held), and the name read back after each. Lines "[AutoTest] 起名删字 …：通过 / 不通过".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator NameBackspaceTest()
        {
            UIManager.instance.StartNewGame(false, false);
            float until = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < until && !(CharacterCreation.IsOpen && CreativeMenu.DevStep == "Character")) yield return null;
            var kb = Keyboard.current;
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 起名删字 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            Check("创建人物界面打开、有键盘", CharacterCreation.IsOpen && kb != null, $"界面 {CreativeMenu.DevStep}，键盘 {(kb != null ? kb.displayName : "无")}");
            if (kb == null || !CharacterCreation.IsOpen) yield break;
            var type = typeof(CharacterCreation);
            var draft = AccessTools.Field(type, "nameDraft");
            var input = (ImeTextEntry)AccessTools.Field(type, "nameInput").GetValue(null);
            AccessTools.Method(type, "BeginNaming").Invoke(null, new object[] { false });
            yield return new WaitForSecondsRealtime(0.3f);
            string Draft() => (string)draft.GetValue(null);
            IEnumerator Frames(int n) { for (int i = 0; i < n; i++) yield return null; }
            foreach (char c in "abc")
            {
                InputSystem.QueueTextEvent(kb, c);
                yield return Frames(2);
            }
            Check("打进 abc", Draft() == "abc", $"现在「{Draft()}」，输入法 {input.Backend}，候选「{input.Composition}」");
            // One press of Backspace, then let go.
            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.Backspace));
            yield return Frames(2);
            InputSystem.QueueStateEvent(kb, new KeyboardState());
            yield return Frames(3);
            Check("按一下退格删一个字", Draft() == "ab", $"现在「{Draft()}」，候选「{input.Composition}」，正在拼写 {input.EditingComposition}");
            // A Chinese word committed by the IME, then Backspace.
            foreach (char c in "你好")
            {
                InputSystem.QueueTextEvent(kb, c);
                yield return Frames(1);
            }
            yield return Frames(3);
            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.Backspace));
            yield return Frames(2);
            InputSystem.QueueStateEvent(kb, new KeyboardState());
            yield return Frames(3);
            Check("输入法打出中文后退格删一个字", Draft() == "ab你", $"现在「{Draft()}」，候选「{input.Composition}」，正在拼写 {input.EditingComposition}");
            // Held for a second: Terraria's text boxes repeat it.
            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.Backspace));
            yield return new WaitForSecondsRealtime(1.2f);
            InputSystem.QueueStateEvent(kb, new KeyboardState());
            yield return Frames(3);
            Check("按住退格连续删", Draft().Length == 0, $"现在「{Draft()}」");
            Plugin.Log.LogInfo($"[AutoTest] 起名删字 合计 {ok}/{all}");
            AccessTools.Method(type, "EndNaming").Invoke(null, new object[] { false });
        }
    }
}
