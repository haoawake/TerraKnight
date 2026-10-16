using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "liquidscan"): every playable scene loaded on its own: its acid pools (the Acid Armour Check pairs: the
    /// Acid Box, the Surface Water Region), its water (the Water layer's colliders, the hot springs' Spa Region) and how
    /// big each is in cells, written to liquidscan.txt, to place fishing.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator LiquidScanTest()
        {
            var lines = new List<string>();
            int scenes = 0, acid = 0, water = 0;
            for (int i = 0; i < USM.sceneCountInBuildSettings; i++)
            {
                string name = Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
                bool play = false;
                foreach (var p in scanPrefixes) if (name.StartsWith(p)) { play = true; break; }
                if (!play || name == GameManager.instance.sceneName) continue;
                var scene = USM.GetSceneByName(name);
                bool loadedHere = !scene.isLoaded;
                if (loadedHere)
                {
                    var op = USM.LoadSceneAsync(name, LoadSceneMode.Additive);
                    float until = Time.realtimeSinceStartup + 20f;
                    while (op != null && !op.isDone && Time.realtimeSinceStartup < until) yield return null;
                    scene = USM.GetSceneByName(name);
                }
                if (!scene.isLoaded) continue;
                scenes++;
                foreach (var root in scene.GetRootGameObjects())
                {
                    foreach (var fsm in root.GetComponentsInChildren<PlayMakerFSM>(true))
                    {
                        if (fsm.FsmName == "Surface Water Region" && PlayMakerFSM.FindFsmOnGameObject(fsm.gameObject, "Acid Armour Check") == null)
                        {
                            var w = Box(fsm.GetComponent<BoxCollider2D>());
                            lines.Add($"{name}	swim	{w.center.x:F1}	{w.center.y:F1}	{w.size.x / World.CellGrid.CellSize:F0}x{w.size.y / World.CellGrid.CellSize:F0}	{fsm.gameObject.name}	{fsm.transform.parent?.name}");
                            water++;
                            continue;
                        }
                        if (fsm.FsmName != "Acid Armour Check" || fsm.GetComponent<DamageHero>() == null) continue;
                        var b = Box(fsm.GetComponent<BoxCollider2D>());
                        lines.Add($"{name}\tacid\t{b.center.x:F1}\t{b.center.y:F1}\t{b.size.x / World.CellGrid.CellSize:F0}x{b.size.y / World.CellGrid.CellSize:F0}\t{fsm.gameObject.name}");
                        acid++;
                    }
                    foreach (var c in root.GetComponentsInChildren<Collider2D>(true))
                    {
                        bool isWater = c.gameObject.layer == 4 || c.gameObject.name == "Spa Region";
                        if (!isWater) continue;
                        var b = c is BoxCollider2D bc ? Box(bc) : c.bounds;
                        lines.Add($"{name}\twater\t{b.center.x:F1}\t{b.center.y:F1}\t{b.size.x / World.CellGrid.CellSize:F0}x{b.size.y / World.CellGrid.CellSize:F0}\t{c.gameObject.name}");
                        water++;
                    }
                }
                if (loadedHere)
                {
                    var un = USM.UnloadSceneAsync(scene);
                    float until = Time.realtimeSinceStartup + 20f;
                    while (un != null && !un.isDone && Time.realtimeSinceStartup < until) yield return null;
                }
            }
            File.WriteAllLines(Path.Combine(DevCopy.SaveDir, "liquidscan.txt"), lines);
            Plugin.Log.LogInfo($"[AutoTest] 液体普查 {scenes} 个场景：酸池 {acid} 个，水面 {water} 个");
        }

        static Bounds Box(BoxCollider2D box)
        {
            if (box == null) return default;
            var t = box.transform;
            Vector2 c = t.TransformPoint(box.offset), s = Vector2.Scale(box.size, t.lossyScale);
            return new Bounds(c, new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), 0f));
        }
    }
}
