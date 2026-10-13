using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fsmdump"): the state machines behind the game's ability pickups, nail arts and
    /// nail upgrades, to see what they show: each scene loaded on its own beside the one played,
    /// the FSMs of its pickups, nailmasters, nailsmith and messages dumped, the scene unloaded.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly string[] dumpScenes =
        {
            "Room_nailmaster", "Room_nailmaster_02", "Room_nailmaster_03", "Room_nailsmith", "Mines_31", "Crossroads_ShamanTemple", "Ruins1_24",
        };

        static readonly string[] dumpWords = { "Shiny", "Msg", "Get", "Nail", "Learn", "Prompt", "Tutorial", "Item", "Convo", "Spell", "Upgrade", "Dream", "Orb", "Shaman" };

        /// <summary>Dev (suite "welldump"): Crossroads_01's and Dirtmouth's state machines that change scene, and their gates.</summary>
        IEnumerator WellDumpTest()
        {
            foreach (var name in new[] { "Crossroads_01", "Town" })
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(name);
                bool loadedHere = false;
                if (!scene.isLoaded)
                {
                    var load = USM.LoadSceneAsync(name, LoadSceneMode.Additive);
                    while (!load.isDone) yield return null;
                    scene = USM.GetSceneByName(name);
                    loadedHere = true;
                }
                yield return new WaitForSeconds(0.5f);
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var tp in root.GetComponentsInChildren<TransitionPoint>(true))
                    {
                        var c = tp.GetComponent<Collider2D>();
                        Plugin.Log.LogInfo($"[AutoTest] 井 {name} 门 {PathOf(tp.transform)} → {tp.targetScene}/{tp.entryPoint}，门 {tp.isADoor}，范围 {(c != null ? c.bounds.min.ToString("F1") + ".." + c.bounds.max.ToString("F1") : "无")}，启用 {tp.gameObject.activeInHierarchy}");
                    }
                    foreach (var f in root.GetComponentsInChildren<PlayMakerFSM>(true))
                    {
                        bool changes = false;
                        foreach (var s in f.FsmStates)
                            foreach (var a in s.Actions)
                                if (a.GetType().Name.Contains("SceneTransition") || a.GetType().Name.Contains("BeginScene")) changes = true;
                        if (!changes) continue;
                        Plugin.Log.LogInfo($"[AutoTest] 井 {name} 换场景的状态机：{PathOf(f.transform)} '{f.FsmName}'，位置 {f.transform.position:F1}");
                        DumpFsm(f);
                    }
                }
                if (loadedHere)
                {
                    var unload = USM.UnloadSceneAsync(scene);
                    while (unload != null && !unload.isDone) yield return null;
                }
            }
        }

        IEnumerator FsmDumpTest()
        {
            foreach (var name in dumpScenes)
            {
                var load = USM.LoadSceneAsync(name, LoadSceneMode.Additive);
                if (load == null)
                {
                    Plugin.Log.LogInfo($"[AutoTest] 状态机 {name} 载入不了");
                    continue;
                }
                while (!load.isDone) yield return null;
                yield return new WaitForSeconds(0.5f);
                var scene = USM.GetSceneByName(name);
                int n = 0;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var f in root.GetComponentsInChildren<PlayMakerFSM>(true))
                    {
                        string key = f.FsmName + " " + f.gameObject.name;
                        // The nailmasters' and the nailsmith's rooms: everything.
                        bool want = name.StartsWith("Room_nail") || f.FsmName == "FSM" || f.FsmName.StartsWith("Get ") || f.FsmName == "Quake Pickup";
                        foreach (var w in dumpWords) if (key.IndexOf(w, System.StringComparison.OrdinalIgnoreCase) >= 0) want = true;
                        if (!want) continue;
                        n++;
                        Plugin.Log.LogInfo($"[AutoTest] 状态机 {name}：{PathOf(f.transform)} '{f.FsmName}'");
                        DumpFsm(f);
                    }
                Plugin.Log.LogInfo($"[AutoTest] 状态机 {name} 导出了 {n} 个");
                var unload = USM.UnloadSceneAsync(scene);
                while (unload != null && !unload.isDone) yield return null;
                yield return new WaitForSeconds(0.3f);
            }
        }
    }
}
