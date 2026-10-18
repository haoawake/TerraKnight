using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "menuflow"): saves made and left the way a player does, through the game's own
    /// menus: the title's start, a click on an empty slot, the play mode's classic, the difficulty
    /// panel, the opening watched (not skipped) into King's Pass, then Esc and quit to the menu; the
    /// same in creative mode from the profiles' panel; and the first save loaded again by a click
    /// on its slot. Every two seconds of each wait is logged (scene, state, frame), so a hang shows
    /// as where the log stops.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator MenuFlowTest()
        {
            var ui = UIManager.instance;
            PlayerData.instance.isInvincible = true;
            Plugin.Log.LogInfo("[AutoTest] 菜单流程 1：标题画面点开始游戏");
            (ui = UIManager.instance).UIGoToProfileMenu();
            yield return Waiting("存档界面", 10f, () => UIManager.instance != null && UIManager.instance.menuState == GlobalEnums.MainMenuState.SAVE_PROFILES);
            yield return new WaitForSecondsRealtime(1.5f);
            UnityEngine.UI.Selectable[] Slots() => new UnityEngine.UI.Selectable[] { UIManager.instance.slotOne, UIManager.instance.slotTwo, UIManager.instance.slotThree, UIManager.instance.slotFour };
            UI.SaveSlots.DevSetPage(0);
            yield return new WaitForSecondsRealtime(1.5f);
            Plugin.Log.LogInfo($"[AutoTest] 菜单流程 1：点空存档 1（{UIManager.instance.slotOne.saveFileState}）");
            Click(Slots()[0].gameObject);
            yield return new WaitForSecondsRealtime(1.5f);
            yield return PlayModeAndDifficulty("新存档");
            yield return IntoTheGame("新存档", 1);
            yield return QuitToMenu("新存档");

            Plugin.Log.LogInfo("[AutoTest] 菜单流程 2：存档界面的创造模式，存档 2");
            (ui = UIManager.instance).UIGoToProfileMenu();
            yield return Waiting("存档界面", 10f, () => UIManager.instance != null && UIManager.instance.menuState == GlobalEnums.MainMenuState.SAVE_PROFILES);
            yield return new WaitForSecondsRealtime(1.5f);
            UI.CreativeMenu.DevOpen();
            yield return new WaitForSecondsRealtime(0.6f);
            UI.CreativeMenu.DevChoose(2);
            yield return new WaitForSecondsRealtime(0.3f);
            UI.CreativeMenu.DevConfirm();
            yield return new WaitForSecondsRealtime(0.3f);
            UI.CreativeMenu.DevPick(World.DifficultyLevel.Normal);
            yield return IntoTheGame("创造模式", 2);
            yield return QuitToMenu("创造模式");

            Plugin.Log.LogInfo("[AutoTest] 菜单流程 3：点存档 1 读档");
            (ui = UIManager.instance).UIGoToProfileMenu();
            yield return Waiting("存档界面", 10f, () => UIManager.instance != null && UIManager.instance.menuState == GlobalEnums.MainMenuState.SAVE_PROFILES);
            yield return new WaitForSecondsRealtime(2f);
            Plugin.Log.LogInfo($"[AutoTest] 菜单流程 3：存档 1 是 {UIManager.instance.slotOne.saveFileState}");
            Click(Slots()[0].gameObject);
            yield return IntoTheGame("读档", 1);
            yield return QuitToMenu("读档");
            Plugin.Log.LogInfo("[AutoTest] 菜单流程 全部走完 通过");
        }

        /// <summary>A pointer click on a menu button, as the mouse gives it.</summary>
        static void Click(GameObject button)
        {
            var es = EventSystem.current;
            var data = new PointerEventData(es) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(button, data, ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.Execute(button, data, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(button, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button, data, ExecuteEvents.pointerClickHandler);
            ExecuteEvents.Execute(button, new BaseEventData(es), ExecuteEvents.submitHandler);
        }

        /// <summary>The play mode menu's classic (when the game shows it), then the mod's difficulty panel's Normal.</summary>
        IEnumerator PlayModeAndDifficulty(string tag)
        {
            var ui = UIManager.instance;
            float until = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < until && UI.CreativeMenu.DevStep != "Difficulty")
            {
                if (ui.playModeMenuScreen != null && ui.playModeMenuScreen.gameObject.activeInHierarchy)
                {
                    var names = new System.Text.StringBuilder();
                    UnityEngine.UI.Selectable classic = null;
                    foreach (var b in ui.playModeMenuScreen.GetComponentsInChildren<UnityEngine.UI.Selectable>())
                    {
                        names.Append($" {b.name}{(b.interactable ? "" : "(关)")}");
                        if (classic == null && b.interactable && b.gameObject.activeInHierarchy) classic = b;
                    }
                    Plugin.Log.LogInfo($"[AutoTest] 菜单流程 {tag}：游戏模式菜单：{names}，点 {classic?.name}");
                    if (classic != null) Click(classic.gameObject);
                    yield return new WaitForSecondsRealtime(1.5f);
                    break;
                }
                yield return null;
            }
            yield return Waiting($"{tag} 难度面板", 8f, () => UI.CreativeMenu.DevStep == "Difficulty");
            Plugin.Log.LogInfo($"[AutoTest] 菜单流程 {tag}：难度面板 {UI.CreativeMenu.DevStep}，选普通");
            UI.CreativeMenu.DevPick(World.DifficultyLevel.Normal);
        }

        /// <summary>The opening watched through, until King's Pass (or the save's bench) is played in.</summary>
        IEnumerator IntoTheGame(string tag, int slot)
        {
            float t0 = Time.realtimeSinceStartup;
            yield return Waiting($"{tag} 进游戏", 240f, () =>
            {
                var g = GameManager.UnsafeInstance;
                return g != null && g.gameState == GlobalEnums.GameState.PLAYING && !g.IsInSceneTransition && GameHooks.PlayerHasControl()
                    && g.sceneName != "Menu_Title" && g.sceneName != "Opening_Sequence";
            });
            var gm = GameManager.instance;
            Plugin.Log.LogInfo($"[AutoTest] 菜单流程 {tag}：{Time.realtimeSinceStartup - t0:F1} 秒进了 {gm.sceneName}，存档位 {gm.profileID}（应为 {slot}），创造 {mod.Creative}");
            Shot(gm.sceneName, $"menuflow_{tag}_in");
            yield return null;
            // A few seconds of play, a step either way.
            Player.TerraMovement.DevMove = 1;
            yield return new WaitForSeconds(2f);
            Player.TerraMovement.DevMove = -1;
            yield return new WaitForSeconds(2f);
            Player.TerraMovement.DevMove = 0;
            yield return new WaitForSeconds(2f);
        }

        IEnumerator QuitToMenu(string tag)
        {
            var gm = GameManager.instance;
            var pd = PlayerData.instance;
            yield return Waiting($"{tag} 能暂停", 20f, () => !pd.disablePause && gm.gameState == GlobalEnums.GameState.PLAYING);
            // As a player leaves: Esc for the inventory, its "游戏菜单" button (the game's pause menu).
            var ih = gm.inputHandler;
            Plugin.Log.LogInfo($"[AutoTest] 菜单流程 {tag}：按 Esc 前 原版暂停键启用 {ih?.inputActions?.pause?.Enabled}，全局输入是它 {InputHandler.Instance == ih}，mod 用的是它 {GameHooks.Input == ih}，{gm.gameState}");
            yield return Tap(Key.Escape);
            yield return new WaitForSecondsRealtime(0.5f);
            Plugin.Log.LogInfo($"[AutoTest] 菜单流程 {tag}：按 Esc 后 {gm.gameState}，原版暂停键启用 {gm.inputHandler?.inputActions?.pause?.Enabled}");
            Plugin.Log.LogInfo($"[AutoTest] 菜单流程 {tag}：Esc 后背包开着 {Items.InventoryGui.Open}，点游戏菜单");
            mod.OpenGameMenu();
            // Frame by frame for a moment: whether the pause holds, and what it is made of.
            var trace = new System.Text.StringBuilder();
            for (float until = Time.realtimeSinceStartup + 1.5f; Time.realtimeSinceStartup < until;)
            {
                trace.Append($" [{gm.gameState}{(gm.isPaused ? "停" : "")}{(gm.TimeSlowed ? "慢" : "")} {UIManager.instance?.uiState} t{Time.timeScale:F1} 禁{pd.disablePause} 背包{Items.InventoryGui.Open} 创造面板{UI.CreativeMenu.IsOpen}]");
                yield return null;
            }
            Plugin.Log.LogInfo($"[AutoTest] 菜单流程 {tag}：点游戏菜单后逐帧{trace}");
            yield return Waiting($"{tag} 暂停菜单", 6f, () => gm.gameState == GlobalEnums.GameState.PAUSED);
            yield return new WaitForSecondsRealtime(1f);
            var ui = UIManager.instance;
            var names = new System.Text.StringBuilder();
            UnityEngine.UI.Selectable quit = null;
            if (ui.pauseMenuScreen != null)
                foreach (var b in ui.pauseMenuScreen.GetComponentsInChildren<UnityEngine.UI.Selectable>())
                {
                    names.Append($" {b.name}");
                    if (quit == null && (b.name.Contains("Exit") || b.name.Contains("Quit") || b.name.Contains("Menu"))) quit = b;
                }
            Plugin.Log.LogInfo($"[AutoTest] 菜单流程 {tag}：暂停菜单 {ui.uiState}/{ui.menuState}，按钮{names}，点 {quit?.name}");
            float t0 = Time.realtimeSinceStartup;
            if (quit != null) Click(quit.gameObject);
            yield return new WaitForSecondsRealtime(1.5f);
            // Its "are you sure" prompt: yes.
            UnityEngine.UI.Selectable yes = null;
            var prompt = new System.Text.StringBuilder();
            foreach (var b in Object.FindObjectsByType<UnityEngine.UI.Selectable>(FindObjectsSortMode.None))
            {
                if (!b.gameObject.activeInHierarchy || !b.interactable) continue;
                prompt.Append($" {b.transform.parent?.name}/{b.name}");
                if (yes == null && (b.name.Contains("Yes") || b.name.Contains("Confirm") || b.name.Contains("Accept"))) yes = b;
            }
            Plugin.Log.LogInfo($"[AutoTest] 菜单流程 {tag}：点退出后 {ui.uiState}/{ui.menuState}，能点的{prompt}，点 {yes?.name}");
            if (yes != null) Click(yes.gameObject);
            else if (GameManager.UnsafeInstance?.sceneName != "Menu_Title")
            {
                Plugin.Log.LogInfo($"[AutoTest] 菜单流程 {tag}：没找到确认按钮，直接退出");
                UIManager.instance.UIReturnToMainMenu();
            }
            Items.InventoryGui.Open = false;
            yield return Waiting($"{tag} 回主菜单", 90f, () =>
            {
                var g = GameManager.UnsafeInstance;
                return g != null && g.sceneName == "Menu_Title" && UIManager.instance != null && UIManager.instance.menuState == GlobalEnums.MainMenuState.MAIN_MENU;
            });
            Plugin.Log.LogInfo($"[AutoTest] 菜单流程 {tag}：{Time.realtimeSinceStartup - t0:F1} 秒回到主菜单");
            yield return new WaitForSecondsRealtime(3f);
        }

        /// <summary>Waits for a condition, logging where things are every two seconds; a give-up is logged too.</summary>
        IEnumerator Waiting(string what, float seconds, System.Func<bool> done)
        {
            float t0 = Time.realtimeSinceStartup, note = t0 + 2f;
            int frame0 = Time.frameCount;
            while (!done())
            {
                if (Time.realtimeSinceStartup - t0 > seconds)
                {
                    Plugin.Log.LogInfo($"[AutoTest] 菜单流程 等{what}超时（{seconds} 秒）：{Where()} 不通过");
                    yield break;
                }
                if (Time.realtimeSinceStartup >= note)
                {
                    note += 2f;
                    Plugin.Log.LogInfo($"[AutoTest] 菜单流程 等{what} {Time.realtimeSinceStartup - t0:F0}s：{Where()}，帧 {Time.frameCount - frame0}");
                }
                yield return null;
            }
        }

        static string Where()
        {
            var g = GameManager.UnsafeInstance;
            var ui = UIManager.instance;
            var hero = HeroController.SilentInstance;
            return $"场景 {g?.sceneName}，{g?.gameState}，切换中 {g?.IsInSceneTransition}，界面 {ui?.uiState}/{ui?.menuState}，面板 {UI.CreativeMenu.DevStep}，主角 {(hero != null ? ((Vector2)hero.transform.position).ToString("F1") : "无")}，可操作 {GameHooks.PlayerHasControl()}";
        }
    }
}
