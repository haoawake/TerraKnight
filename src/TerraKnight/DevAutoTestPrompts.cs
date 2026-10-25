using System.Collections;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "prompts"): the game's "got it" message for a gone ability (Dash) ends at once,
    /// shown for nothing, its end event sent; one still in use (Journal) shows as before. The
    /// message prefab is taken from Greenpath's cloak pickup (its CreateUIMsgGetItem).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator PromptsTest()
        {
            var load = USM.LoadSceneAsync("Fungus1_04", UnityEngine.SceneManagement.LoadSceneMode.Additive);
            while (load != null && !load.isDone) yield return null;
            GameObject prefab = null;
            foreach (var root in USM.GetSceneByName("Fungus1_04").GetRootGameObjects())
                foreach (var f in root.GetComponentsInChildren<PlayMakerFSM>(true))
                    if (f.FsmName == "Shiny Control")
                        foreach (var s in f.FsmStates)
                            foreach (var a in s.Actions)
                                if (a is CreateUIMsgGetItem c && c.gameObject.Value != null) prefab = c.gameObject.Value;
            if (prefab == null)
            {
                Plugin.Log.LogInfo("[AutoTest] 提示 没找到获得物品的消息预制体");
                yield break;
            }
            foreach (var item in new[] { "Dash", "Journal" })
            {
                int skipped = Player.ObsoletePrompts.DevSkipped;
                var msg = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity);
                msg.SetActive(false);
                PlayMakerFSM control = null;
                foreach (var f in msg.GetComponents<PlayMakerFSM>()) if (f.FsmName == "Msg Control") control = f;
                if (control != null) control.FsmVariables.GetFsmString("Item").Value = item;
                msg.SetActive(true);
                yield return new WaitForSeconds(1.2f);
                Shot(GameManager.instance.sceneName, "prompt_" + item);
                yield return null;
                Plugin.Log.LogInfo($"[AutoTest] 提示 {item}：被拦下 {Player.ObsoletePrompts.DevSkipped - skipped} 次（{Player.ObsoletePrompts.DevLast}），消息还在 {msg != null}，状态 {(control != null ? control.ActiveStateName : "-")}");
                if (msg != null) Object.Destroy(msg);
                yield return new WaitForSeconds(0.3f);
            }
            var unload = USM.UnloadSceneAsync("Fungus1_04");
            while (unload != null && !unload.isDone) yield return null;

            // Inspect services without changing their native teaching or upgrading offers.
            foreach (var room in new[] { "Room_nailmaster", "Room_nailmaster_02", "Room_nailmaster_03", "Room_nailsmith" })
            {
                var l = USM.LoadSceneAsync(room, UnityEngine.SceneManagement.LoadSceneMode.Additive);
                while (l != null && !l.isDone) yield return null;
                yield return new WaitForSeconds(0.5f);
                foreach (var root in USM.GetSceneByName(room).GetRootGameObjects())
                    foreach (var f in root.GetComponentsInChildren<PlayMakerFSM>(true))
                    {
                        if (f.FsmName != "Conversation Control") continue;
                        string n = f.gameObject.name;
                        if (n == "Nailsmith")
                            Plugin.Log.LogInfo($"[AutoTest] 提示 {room} 骨钉匠：初见第三段 -> {Player.NailServices.DevTarget(f, "Meet 3", "CONVO_FINISH")}，问候 -> {Player.NailServices.DevTarget(f, "Greet", "CONVO_FINISH")}（保留原版升级提议）");
                        else if (n.StartsWith("NM ") && !n.Contains("Modeller"))
                            Plugin.Log.LogInfo($"[AutoTest] 提示 {room} {n}：初见 -> {Player.NailServices.DevTarget(f, "Meet", "CONVO_FINISH")}，再提议 -> {Player.NailServices.DevTarget(f, "Reoffer", "CONVO_FINISH")}，没学过时 -> {Player.NailServices.DevTarget(f, "Convo Choice", "REOFFER")}（应为 Box Down2 / Box Down2 / Reoffer，保留授课）");
                    }
                var u = USM.UnloadSceneAsync(room);
                while (u != null && !u.isDone) yield return null;
            }
            Plugin.Log.LogInfo($"[AutoTest] 提示 骨钉服务改了 {Player.NailServices.DevRedirects} 处跳转");
        }
    }
}
