using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "liftscan"): the game's lifts (the user's, 2026-10-20: minions set off the lifts' levers; the City's lift
    /// dug out, the Knight falls for ever). Each room of the areas with lifts loaded on its own: its RuinsLift and
    /// LiftPlatform objects, the objects and FSMs named for a lift, and, in a room with one, its gates at the bottom and
    /// its lifts' own.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator LiftScanTest()
        {
            var lines = new List<string>();
            foreach (var prefix in new[] { "Ruins1_", "Ruins2_", "Crossroads_", "Mines_", "RestingGrounds_", "Deepnest_East_", "Abyss_", "Fungus2_", "Fungus3_", "Waterways_", "Town" })
                for (int i = 0; i < USM.sceneCountInBuildSettings; i++)
                {
                    string name = Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
                    if (!name.StartsWith(prefix) || name == GameManager.instance.sceneName) continue;
                    var op = USM.LoadSceneAsync(name, LoadSceneMode.Additive);
                    float until = Time.realtimeSinceStartup + 20f;
                    while (op != null && !op.isDone && Time.realtimeSinceStartup < until) yield return null;
                    var scene = USM.GetSceneByName(name);
                    if (!scene.isLoaded) continue;
                    var sb = new StringBuilder();
                    var gatesSb = new StringBuilder();
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        foreach (var t in root.GetComponentsInChildren<Transform>(true))
                        {
                            bool lift = t.GetComponent<RuinsLift>() != null || t.GetComponent<LiftPlatform>() != null
                                || t.name.IndexOf("Lift", System.StringComparison.OrdinalIgnoreCase) >= 0 || t.name.IndexOf("Elevator", System.StringComparison.OrdinalIgnoreCase) >= 0;
                            var fsmNames = new List<string>();
                            foreach (var f in t.GetComponents<PlayMakerFSM>()) fsmNames.Add(f.FsmName);
                            bool liftFsm = fsmNames.Exists(n => n.IndexOf("Lift", System.StringComparison.OrdinalIgnoreCase) >= 0);
                            if (lift || liftFsm)
                            {
                                var col = t.GetComponent<Collider2D>();
                                sb.Append($"\n      {LiftPath(t)}{(t.GetComponent<RuinsLift>() != null ? " [RuinsLift]" : "")}{(t.GetComponent<LiftPlatform>() != null ? " [LiftPlatform]" : "")}" +
                                    $" FSM {string.Join("/", fsmNames)} 碰撞 {(col != null ? col.GetType().Name + " 层" + col.gameObject.layer + (col.isTrigger ? " 触发器" : "") : "无")} @{(Vector2)t.position:F1}");
                            }
                        }
                        foreach (var tp in root.GetComponentsInChildren<TransitionPoint>(true))
                            if (tp.name.StartsWith("bot") || tp.name.IndexOf("lift", System.StringComparison.OrdinalIgnoreCase) >= 0)
                                gatesSb.Append($"\n      门 {tp.name} @{(Vector2)tp.transform.position:F1} 去 {tp.targetScene}/{tp.entryPoint}");
                    }
                    if (sb.Length > 0) lines.Add($"  {name}:{sb}{gatesSb}");
                    var un = USM.UnloadSceneAsync(scene);
                    until = Time.realtimeSinceStartup + 20f;
                    while (un != null && !un.isDone && Time.realtimeSinceStartup < until) yield return null;
                }
            Plugin.Log.LogInfo($"[AutoTest] 电梯扫描 {lines.Count} 个房间有电梯：\n{string.Join("\n", lines)}");
            Plugin.Log.LogInfo("[AutoTest] 电梯扫描 完成 通过");
        }

        static string LiftPath(Transform t)
        {
            string p = t.name;
            for (var u = t.parent; u != null; u = u.parent) p = u.name + "/" + p;
            return p;
        }
    }
}
