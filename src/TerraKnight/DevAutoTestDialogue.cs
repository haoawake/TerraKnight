using System.Collections;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "talkaway"): taken away mid-talk. Dirtmouth's Elderbug talked to (up beside him, as the player does),
    /// his words up; then recalled home by the mirror: the box down once there, nothing left on the screen.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator TalkAwayTest()
        {
            yield return GoTo("Town", "left1");
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(1f);
            var hero = HeroController.instance;
            GameObject elder = null;
            foreach (var go in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (go.name == "Elderbug") { elder = go.gameObject; break; }
            if (elder == null) { Plugin.Log.LogInfo("[AutoTest] 对话中传送 找不到老虫子 不通过"); yield break; }
            hero.transform.position = Clear(hero, (Vector2)elder.transform.position + new Vector2(-1.5f, 0.5f));
            yield return new WaitForSeconds(1.5f);
            bool open = false;
            for (int i = 0; i < 6 && !open; i++)
            {
                yield return Tap(UnityEngine.InputSystem.Key.W, 0.2f);
                yield return new WaitForSeconds(1.5f);
                open = UI.HudDialogue.DevOpen;
            }
            Shot("Town", "talkaway_0_talking");
            Plugin.Log.LogInfo($"[AutoTest] 对话中传送 和老虫子说话：对话框 {open}");
            bool recalled = Pylons.Recall();
            float until = Time.time + 20f;
            yield return new WaitForSeconds(1f);
            while (Time.time < until && (GameManager.instance.IsInSceneTransition || !GameHooks.PlayerHasControl())) yield return null;
            yield return new WaitForSeconds(1.5f);
            bool after = UI.HudDialogue.DevOpen;
            Shot(GameManager.instance.sceneName, "talkaway_1_arrived");
            bool pass = open && recalled && !after && GameHooks.PlayerHasControl();
            Plugin.Log.LogInfo($"[AutoTest] 对话中传送 回城（{recalled}，到了 {GameManager.instance.sceneName}）：对话框还在 {after}，可操作 {GameHooks.PlayerHasControl()}，收起 {UI.HudDialogue.DevClosed} 次 {(pass ? "通过" : "不通过")}");
        }
    }
}
