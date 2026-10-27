using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "ggstatue", players' 10-26 "神居手办屋点开雕像按不了挑战键也按不了退出"): a Pantheon door in the Atrium and
    /// the Hall of Gods' statue nearest the Knight, each one's own FSM put in its UI state (ShowBossDoorChallengeUI,
    /// ShowBossChallengeUI) with control taken as its path takes it. For each: the game's menu input as the UI sees it
    /// (switches, selected button, the keys its submit and cancel answer to) and what a click at its selected button
    /// reaches; the door cancelled from its selected button; the statue's first tier clicked (a pointer click on what the
    /// ray reaches) and the fight it loads. Lines "[AutoTest] 挑战界面 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator GgStatueTest()
        {
            int ok = 0, all = 0;
            void Check(bool pass, string what)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 挑战界面 {what} {(pass ? "通过" : "不通过")}");
            }
            string Report()
            {
                var ih = InputHandler.Instance;
                var es = EventSystem.current;
                var im = UIManager.instance != null ? UIManager.instance.inputModule : null;
                var a = ih != null ? ih.inputActions : null;
                string Act(InControl.PlayerAction p) => p == null ? "无" : $"{(p.Enabled ? "开" : "关")}[{string.Join(",", p.Bindings.Select(b => b.Name))}]";
                return $"InputHandler 接受输入 {ih?.acceptingInput}，动作集 {(a != null && a.Enabled ? "开" : "关")}；导航 {es?.sendNavigationEvents}，模块 {es?.currentInputModule?.GetType().Name}，选中 {(es?.currentSelectedGameObject != null ? es.currentSelectedGameObject.name : "无")}，" +
                       $"鼠标 {im?.allowMouseInput} 光标 {Cursor.visible}；跳 {Act(a?.jump)} 施 {Act(a?.cast)} 确认 {Act(a?.menuSubmit)} 取消 {Act(a?.menuCancel)}；界面开着 {UI.HkMenus.Open}";
            }
            // A button's middle on the screen, and what a click there reaches.
            List<RaycastResult> ClickAt(Component ui, Component btn, out Vector2 screen)
            {
                var canvas = ui.GetComponentInParent<Canvas>();
                if (canvas == null) canvas = ui.GetComponentInChildren<Canvas>();
                var cam = canvas != null ? canvas.worldCamera : null;
                screen = RectTransformUtility.WorldToScreenPoint(cam, btn.transform.position);
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screen }, hits);
                return hits;
            }
            bool found = false;
            IEnumerator Open<T>(string scene, string gate) where T : HutongGames.PlayMaker.FsmStateAction
            {
                found = false;
                yield return GoTo(scene, gate);
                yield return new WaitForSeconds(2f);
                var hero = HeroController.instance;
                PlayMakerFSM fsm = null;
                string state = null;
                float best = float.MaxValue;
                foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (f == null || f.FsmStates == null || !f.gameObject.scene.IsValid()) continue;
                    foreach (var st in f.FsmStates)
                        foreach (var act in st.Actions)
                            if (act is T)
                            {
                                float d = Vector2.Distance(f.transform.position, hero.transform.position);
                                if (d < best) { best = d; fsm = f; state = st.Name; }
                            }
                }
                if (fsm == null) yield break;
                found = true;
                Plugin.Log.LogInfo($"[AutoTest] 挑战界面 {PathOf(fsm.transform)}（{(fsm.gameObject.activeInHierarchy ? "开着" : "关着")}）的 {fsm.FsmName} 从 {fsm.ActiveStateName} 进 {state}");
                if (!fsm.gameObject.activeInHierarchy)
                {
                    for (var tr = fsm.transform; tr != null; tr = tr.parent) if (!tr.gameObject.activeSelf) tr.gameObject.SetActive(true);
                    yield return null;
                }
                hero.RelinquishControl();
                hero.StopAnimationControl();
                fsm.SetState(state);
                yield return new WaitForSeconds(1.5f);
            }

            // The Pantheon door: opened, a click reaching its selected binding, cancelled.
            yield return Open<ShowBossDoorChallengeUI>("GG_Atrium", "door_dreamEnter");
            var door = Object.FindObjectsByType<BossDoorChallengeUI>(FindObjectsSortMode.None).FirstOrDefault(u => u.gameObject.activeInHierarchy);
            Plugin.Log.LogInfo($"[AutoTest] 挑战界面 万神殿门（FSM {(found ? "找到" : "没找到")}）：{Report()}");
            Shot("GG_Atrium", "ggdoor_open");
            yield return null;
            if (door != null)
            {
                var first = EventSystem.current.currentSelectedGameObject;
                var hits = first != null ? ClickAt(door, first.transform, out _) : new List<RaycastResult>();
                Check(hits.Count > 0 && hits.Any(h => ExecuteEvents.GetEventHandler<IPointerClickHandler>(h.gameObject) != null),
                    $"万神殿门：鼠标点在选中的 {(first != null ? first.name : "无")} 上打到 {hits.Count} 个（{string.Join("、", hits.Take(4).Select(h => h.gameObject.name))}）");
                Check(InputHandler.Instance.inputActions.cast.Enabled, "万神殿门：提示里的退出键（施法 U）开着");
                if (first != null) ExecuteEvents.Execute(first, new BaseEventData(EventSystem.current), ExecuteEvents.cancelHandler);
                yield return new WaitForSeconds(1.5f);
                Check(!door.gameObject.activeInHierarchy || !UI.HkMenus.Open, "万神殿门：取消后关上");
            }
            else Check(false, "万神殿门的界面打开");

            // The statue: opened, its first tier clicked, the fight loading.
            yield return Open<ShowBossChallengeUI>("GG_Workshop", "left1");
            var ui = Object.FindObjectsByType<BossChallengeUI>(FindObjectsSortMode.None).FirstOrDefault(u => u.gameObject.activeInHierarchy);
            Plugin.Log.LogInfo($"[AutoTest] 挑战界面 雕像（FSM {(found ? "找到" : "没找到")}）：{Report()}");
            Shot("GG_Workshop", "ggstatue_open");
            yield return null;
            if (ui != null)
            {
                Component btn = ui.tier1Button != null && ui.tier1Button.button != null ? (Component)ui.tier1Button.button : ui.firstSelected;
                var hits = ClickAt(ui, btn, out var at);
                var handler = hits.Count > 0 ? ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject) : null;
                Check(handler != null && handler.GetComponent<UnityEngine.UI.MenuButton>() != null, $"雕像：鼠标点在 {btn.name} 上打到 {hits.Count} 个，处理者 {(handler != null ? handler.name : "无")}");
                Check(InputHandler.Instance.inputActions.cast.Enabled && !Net.NetChat.Typing, "雕像：提示里的退出键（施法 U）开着");
                if (handler != null)
                {
                    var es = EventSystem.current;
                    var ped = new PointerEventData(es) { position = at, button = PointerEventData.InputButton.Left, pointerPress = handler, eligibleForClick = true };
                    ExecuteEvents.Execute(handler, ped, ExecuteEvents.pointerClickHandler);
                    for (float t = Time.realtimeSinceStartup + 15f; Time.realtimeSinceStartup < t && GameManager.instance.sceneName == "GG_Workshop";) yield return null;
                    Check(GameManager.instance.sceneName != "GG_Workshop", $"雕像：点击调谐级后进入战斗 {GameManager.instance.sceneName}");
                }
            }
            else Check(false, "雕像的界面打开");
            Plugin.Log.LogInfo($"[AutoTest] 挑战界面 合计 {ok}/{all}");
        }
    }
}
