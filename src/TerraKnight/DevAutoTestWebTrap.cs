using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suites "fx_webprobe", "fx_web"): the Beast's Den trap bench (Deepnest_Spider_Town). The player sits on it,
    /// struggles (jump), is carried off and hung in the web; there the left mouse button (Terraria's use, standing in
    /// for the Knight's attack the game listens for) breaks him free four presses later, he falls and gets control
    /// back. The probe also dumps the bench's FSMs.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static bool WebRelated(GameObject go, string fsm)
        {
            string n = go.name + "/" + fsm;
            foreach (var w in new[] { "Bench", "Trap", "Web", "Cocoon", "Silk", "Capture", "Strand", "Wake" })
                if (n.IndexOf(w, System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return false;
        }

        static IEnumerable<string> BindingNames(InControl.PlayerAction action)
        {
            if (action == null) yield break;
            foreach (var b in action.Bindings) yield return b.Name;
        }

        /// <summary>A left click as the mouse would make it (the Input System's own state, which TerraKeys reads).</summary>
        static IEnumerator ClickLeft()
        {
            var mouse = Mouse.current;
            if (mouse == null) yield break;
            InputSystem.QueueStateEvent(mouse, new MouseState { position = mouse.position.ReadValue() }.WithButton(UnityEngine.InputSystem.LowLevel.MouseButton.Left, true));
            yield return new WaitForSecondsRealtime(0.1f);
            InputSystem.QueueStateEvent(mouse, new MouseState { position = mouse.position.ReadValue() });
            yield return new WaitForSecondsRealtime(0.5f);
        }

        IEnumerator WebTrapProbe(bool dump = true)
        {
            yield return GoTo("Deepnest_Spider_Town", EntryGateOf("Deepnest_Spider_Town"));
            PlayerData.instance.isInvincible = true;
            var hero = HeroController.instance;
            var fsms = new List<PlayMakerFSM>();
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (f != null && f.gameObject.scene.name == "Deepnest_Spider_Town" && WebRelated(f.gameObject, f.FsmName)) fsms.Add(f);
            if (dump) foreach (var f in fsms) Logged(() => DumpFsm(f));
            PlayMakerFSM bench = null, fade = null;
            foreach (var f in fsms)
            {
                if (f.FsmName == "Bench Control Spider" && bench == null) bench = f;
                if (f.FsmName == "Fade" && f.gameObject.name == "RestBench Spider") fade = f;
            }
            if (bench == null || fade == null) { FxCheck("深巢蛛网陷阱", false, "没有陷阱长椅"); yield break; }
            var at = (Vector2)bench.transform.position;
            hero.transform.position = new Vector3(at.x + 0.3f, at.y + 0.5f, hero.transform.position.z);
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(1.5f);
            var ia = GameHooks.Input != null ? GameHooks.Input.inputActions : null;
            if (ia != null) Plugin.Log.LogInfo($"[AutoTest] 蛛网 原版按键：攻击 {string.Join(",", BindingNames(ia.attack))}（启用 {ia.attack.Enabled}）跳 {string.Join(",", BindingNames(ia.jump))}");
            yield return Tap(Key.W, 0.3f);
            Shot("Deepnest_Spider_Town", "fx_web_sit");
            float start = Time.unscaledTime, nextLog = 0f;
            int clicks = 0, presses0 = Player.CutsceneAttack.DevPresses;
            var webbed = bench.transform.Find("Webbed Knight");
            var webAnim = webbed != null ? webbed.GetComponent<tk2dSpriteAnimator>() : null;
            string Anim() => webAnim == null ? "无" : $"{webAnim.CurrentClip?.name} 播放 {webAnim.Playing} 暂停 {webAnim.Paused} 启用 {webAnim.enabled}/{webAnim.gameObject.activeInHierarchy} 帧 {(webAnim.CurrentClip != null ? webAnim.CurrentFrame : -1)}";
            string Line() => $"场景 {GameManager.instance.sceneName} 主角 {(Vector2)hero.transform.position:F1} 状态 {hero.hero_state} 地上 {hero.cState.onGround} 可操作 {GameHooks.PlayerHasControl()} 长椅 {bench.ActiveStateName} 茧 {fade.ActiveStateName} 刚体重力 {hero.GetComponent<Rigidbody2D>().gravityScale:F1} 时速 {Time.timeScale} 暂停 {GameManager.instance.isPaused} 背包 {Items.InventoryGui.Open} 茧动画 {Anim()}";
            bool hung = false, free = false;
            int wiggles = 0;
            int shot = 0;
            while (Time.unscaledTime - start < 90f)
            {
                float t = Time.unscaledTime - start;
                if (t >= nextLog)
                {
                    nextLog = t + 2f;
                    Plugin.Log.LogInfo($"[AutoTest] 蛛网 {t:F0}s {Line()}");
                }
                string state = fade.ActiveStateName;
                if (state == "Stuck") hung = true;
                if (hung && GameHooks.PlayerHasControl() && hero.cState.onGround) { free = true; break; }
                if (!hung && bench.ActiveStateName == "Neutral") yield return Tap(Key.Space, 0.1f);
                else if (state == "Stuck")
                {
                    // As a player would: a jump and the arrows first (the web wiggles), then the button that breaks it.
                    if (wiggles < 3)
                    {
                        wiggles++;
                        yield return Tap(wiggles == 1 ? Key.Space : wiggles == 2 ? Key.LeftArrow : Key.UpArrow, 0.1f);
                        yield return new WaitForSecondsRealtime(1.5f);
                        Plugin.Log.LogInfo($"[AutoTest] 蛛网 扭动 {wiggles} 后 {Line()}");
                    }
                    else
                    {
                        clicks++;
                        yield return ClickLeft();
                    }
                }
                else yield return new WaitForSecondsRealtime(0.25f);
                if (t > 6f * (shot + 1) && shot < 10) { Shot(GameManager.instance.sceneName, $"fx_web_{shot:D2}"); shot++; }
            }
            Plugin.Log.LogInfo($"[AutoTest] 蛛网 结束 {Line()}");
            Shot(GameManager.instance.sceneName, "fx_web_end");
            FxCheck("深巢蛛网陷阱：左键挣脱后落地能动", hung && free, $"吊起来 {hung}，扭动 {wiggles} 次，左键 {clicks} 下（当作攻击 {Player.CutsceneAttack.DevPresses - presses0} 次），落地可操作 {free}，{Time.unscaledTime - start:F0}s");
        }
    }
}
