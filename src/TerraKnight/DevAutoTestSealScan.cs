using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "sealscan"): Dirtmouth and the Forgotten Crossroads' scenes, and in each the pickups
    /// (Shiny Control FSMs) with their item variables, the breakable walls (break_floor and the like) and
    /// the masks over hidden areas (Masker, Remasker), with where each is and whether it is drawn: where
    /// a hidden Hallownest Seal lies and what stands in its way.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static string FsmVars(PlayMakerFSM f)
        {
            var sb = new StringBuilder();
            foreach (var v in f.FsmVariables.StringVariables) if (!string.IsNullOrEmpty(v.Value)) sb.Append($" {v.Name}={v.Value}");
            foreach (var v in f.FsmVariables.IntVariables) if (v.Value != 0) sb.Append($" {v.Name}={v.Value}");
            foreach (var v in f.FsmVariables.BoolVariables) if (v.Value) sb.Append($" {v.Name}");
            return sb.ToString();
        }

        IEnumerator SealScanTest()
        {
            var scenes = new List<string> { "Town" };
            foreach (var s in UI.WorldMap.DevSceneNames()) if (s.StartsWith("Crossroads_")) scenes.Add(s);
            var gm = GameManager.instance;
            foreach (var scene in scenes)
            {
                yield return GoTo(scene, scene == "Town" ? "left1" : EntryGateOf(scene));
                yield return new WaitForSeconds(1.5f);
                PlayerData.instance.isInvincible = true;
                if (gm.sceneName != scene) { Plugin.Log.LogInfo($"[AutoTest] 印章 {scene}：没进去"); continue; }
                var sb = new StringBuilder();
                int shinies = 0;
                foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (f.gameObject.scene.name != scene) continue;
                    bool shiny = f.FsmName == "Shiny Control";
                    bool wall = f.FsmName == "break_floor" || f.FsmName.ToLowerInvariant().Contains("break") || f.gameObject.name.ToLowerInvariant().Contains("breakable");
                    if (!shiny && !wall) continue;
                    if (shiny) shinies++;
                    var r = f.GetComponentInChildren<Renderer>(true);
                    bool drawn = r != null && r.enabled && !r.forceRenderingOff && r.gameObject.activeInHierarchy;
                    sb.Append($"\n  {(shiny ? "拾取物" : "可破坏")} {PathOf(f.transform)} '{f.FsmName}' {(Vector2)f.transform.position:F1} 激活 {f.gameObject.activeInHierarchy} 画出 {drawn} 状态 {f.ActiveStateName}{(shiny ? FsmVars(f) : "")}");
                }
                foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (t.gameObject.scene.name != scene) continue;
                    string n = t.name.ToLowerInvariant();
                    if (!n.Contains("masker") && !n.Contains("remask")) continue;
                    var r = t.GetComponent<Renderer>();
                    var c = t.GetComponent<Collider2D>();
                    sb.Append($"\n  遮罩 {PathOf(t)} {(Vector2)t.position:F1} 激活 {t.gameObject.activeInHierarchy}" +
                              (r != null ? $" 画出 {r.enabled && !r.forceRenderingOff} 范围 {(Vector2)r.bounds.min:F1}..{(Vector2)r.bounds.max:F1}" : "") +
                              (c != null ? $" 触发区 {(Vector2)c.bounds.min:F1}..{(Vector2)c.bounds.max:F1}" : ""));
                }
                if (shinies > 0 || scene == "Town" || scene == "Crossroads_01")
                    Plugin.Log.LogInfo($"[AutoTest] 印章 {scene}：{shinies} 个拾取物{sb}");
            }
        }
    }
}
