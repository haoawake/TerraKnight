using System.Collections;
using System.Collections.Generic;
using System.IO;
using TerraKnight.Items;
using UnityEngine;
using UnityEngine.SceneManagement;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "arenadoors"): each world arena's front door (BossArena's gate) loaded on its own: the room it opens
    /// into and that room's gate back, written to arenadoors.txt (key, arena, gate, room, room's gate, door or not).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator ArenaDoorsScan()
        {
            var lines = new List<string>();
            foreach (var key in BossArena.DevKeys())
            {
                if (!BossArena.DevWorldArena(key, out string gate, out _)) continue;
                string scene = BossArena.SceneOf(key);
                var sc = USM.GetSceneByName(scene);
                bool loadedHere = !sc.isLoaded;
                if (loadedHere)
                {
                    var op = USM.LoadSceneAsync(scene, LoadSceneMode.Additive);
                    float until = Time.realtimeSinceStartup + 20f;
                    while (op != null && !op.isDone && Time.realtimeSinceStartup < until) yield return null;
                    sc = USM.GetSceneByName(scene);
                }
                string found = null;
                if (sc.isLoaded)
                    foreach (var root in sc.GetRootGameObjects())
                        foreach (var tp in root.GetComponentsInChildren<TransitionPoint>(true))
                            if (tp.name == gate && found == null) found = $"{tp.targetScene}\t{tp.entryPoint}\t{(tp.isADoor ? "门" : "通道")}";
                lines.Add($"{key}\t{scene}\t{gate}\t{found ?? "?\t?\t?"}");
                if (loadedHere && sc.isLoaded)
                {
                    var un = USM.UnloadSceneAsync(sc);
                    float until = Time.realtimeSinceStartup + 20f;
                    while (un != null && !un.isDone && Time.realtimeSinceStartup < until) yield return null;
                }
            }
            File.WriteAllLines(Path.Combine(DevCopy.SaveDir, "arenadoors.txt"), lines);
            Plugin.Log.LogInfo($"[AutoTest] 首领房正门 {lines.Count} 个");
        }
    }
}
