using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "gatecheck_A+B"): every gate of some scenes crossed as a player runs, falls or
    /// jumps through it: into the room, a few steps from the gate, then on through it. Whether the
    /// Knight ends up in the room beyond and stays there (not put back where he was), with his
    /// position over the few seconds after.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator GateCheckTest(string[] scenes)
        {
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            const int terrain = 1 << World.ChunkColliders.TerrainLayer;
            int good = 0, bad = 0;
            var blackTimes = new List<(string, float)>();
            foreach (var scene in scenes)
            {
                yield return GoTo(scene, EntryGateOf(scene));
                yield return new WaitForSeconds(1.5f);
                var gates = new List<(string Name, string Target, Bounds Box)>();
                foreach (var tp in Object.FindObjectsByType<TransitionPoint>(FindObjectsSortMode.None))
                {
                    if (tp.gameObject.scene.name != scene || tp.isADoor || string.IsNullOrEmpty(tp.targetScene)) continue;
                    var c = tp.GetComponent<Collider2D>();
                    if (c != null) gates.Add((tp.name, tp.targetScene, c.bounds));
                }
                Plugin.Log.LogInfo($"[AutoTest] 过门 {scene} 的门：{string.Join("，", gates.ConvertAll(g => $"{g.Name}->{g.Target} {g.Box.center:F1}"))}");
                foreach (var (name, target, box) in gates)
                {
                    if (GameManager.instance.sceneName != scene)
                    {
                        yield return GoTo(scene, EntryGateOf(scene));
                        yield return new WaitForSeconds(1.5f);
                    }
                    var h = HeroController.instance;
                    var rb = h.GetComponent<Rigidbody2D>();
                    // From inside the room toward the gate: four steps in, on the floor (or above a bottom gate).
                    Vector2 dir = name.StartsWith("left") ? Vector2.left : name.StartsWith("right") ? Vector2.right : name.StartsWith("top") ? Vector2.up : Vector2.down;
                    Vector2 start = (Vector2)box.center - dir * (dir.y == 0f ? box.extents.x + 4f : box.extents.y + 3f);
                    if (dir.y == 0f)
                    {
                        var down = Physics2D.Raycast(start + Vector2.up * 2f, Vector2.down, 12f, terrain);
                        if (down.collider != null) start.y = down.point.y + 0.8f;
                    }
                    h.transform.position = Clear(h, start);
                    rb.linearVelocity = Vector2.zero;
                    yield return new WaitForSeconds(0.6f);
                    var path = new System.Text.StringBuilder();
                    float t0 = Time.time;
                    Player.TerraMovement.DevMove = (int)dir.x;
                    float leftAt = -1f;
                    while (GameManager.instance.sceneName == scene && Time.time - t0 < 8f)
                    {
                        if (leftAt < 0f && GameManager.instance.IsInSceneTransition) leftAt = Time.realtimeSinceStartup;
                        if (dir.y > 0f) rb.linearVelocity = new Vector2(0f, 12f);
                        if ((int)((Time.time - t0) * 4f) != (int)((Time.time - t0 - Time.deltaTime) * 4f))
                            path.Append($" {(Vector2)h.transform.position:F1}");
                        yield return null;
                    }
                    string reached = GameManager.instance.sceneName;
                    // How long the way through took, from the gate to control again in the next room (the black between).
                    string black = "";
                    if (reached != scene)
                    {
                        if (leftAt < 0f) leftAt = Time.realtimeSinceStartup;
                        float until = Time.realtimeSinceStartup + 15f;
                        while (!GameHooks.PlayerHasControl() && Time.realtimeSinceStartup < until) yield return null;
                        black = $"，过门到能操作 {Time.realtimeSinceStartup - leftAt:F1} 秒";
                        blackTimes.Add((scene + " " + name, Time.realtimeSinceStartup - leftAt));
                    }
                    // On a little, then still: does he stay there?
                    yield return new WaitForSeconds(0.5f);
                    Player.TerraMovement.DevMove = 0;
                    var after = new System.Text.StringBuilder();
                    for (int i = 0; i < 12; i++)
                    {
                        yield return new WaitForSeconds(0.25f);
                        var hh = HeroController.instance;
                        after.Append($" {GameManager.instance.sceneName}@{(Vector2)hh.transform.position:F1}v{hh.GetComponent<Rigidbody2D>().linearVelocity:F1}{(hh.cState.onGround ? "地" : "")}");
                        if (i == 1) Shot(GameManager.instance.sceneName, $"gate_{scene}_{name}_arrive");
                    }
                    string end = GameManager.instance.sceneName;
                    bool ok = reached == target && end == target;
                    if (ok) good++; else bad++;
                    Plugin.Log.LogInfo($"[AutoTest] 过门 {scene} {name}->{target}：{(ok ? "过去了" : "没过去")}，到了 {reached}，最后在 {end}{black}；路上{path}；之后{after} {(ok ? "通过" : "不通过")}");
                    if (!ok && end == scene) Plugin.Log.LogInfo($"[AutoTest] 过门 {scene} {name} 挡着的：{Blockers(HeroController.instance, box)}");
                    Shot(end, $"gate_{scene}_{name}");
                    yield return null;
                }
            }
            Player.TerraMovement.DevMove = 0;
            blackTimes.Sort((x, y) => y.Item2.CompareTo(x.Item2));
            Plugin.Log.LogInfo($"[AutoTest] 过门 合计 过去 {good} 个，没过去 {bad} 个；地图点亮了 {UI.WorldMap.DevRevealed} 格；过门最慢：{string.Join("，", blackTimes.GetRange(0, Mathf.Min(5, blackTimes.Count)).ConvertAll(x => $"{x.Item1} {x.Item2:F1}s"))}");
            // The map, with what was walked through lit.
            if (!UI.WorldMap.Open) UI.WorldMap.Toggle();
            yield return new WaitForSecondsRealtime(1.5f);
            Shot(GameManager.instance.sceneName, "gate_worldmap");
            yield return null;
            if (UI.WorldMap.Open) UI.WorldMap.Toggle();
        }

        /// <summary>The solid colliders between the Knight and a gate: name, layer, parent and each FSM's state.</summary>
        static string Blockers(HeroController h, Bounds gate)
        {
            var hb = h.GetComponent<BoxCollider2D>().bounds;
            Vector2 a = hb.center, b = gate.center;
            var size = new Vector2(Mathf.Abs(b.x - a.x) + hb.size.x, Mathf.Abs(b.y - a.y) + hb.size.y);
            var sb = new System.Text.StringBuilder();
            foreach (var c in Physics2D.OverlapBoxAll((a + b) / 2f, size, 0f))
            {
                if (c.isTrigger || c.transform.IsChildOf(h.transform)) continue;
                var p = c.transform.parent;
                sb.Append($" {c.name}（层 {c.gameObject.layer}，{c.GetType().Name} {c.bounds.center:F1} {c.bounds.size:F1}{(p != null ? "，在 " + p.name : "")}");
                foreach (var f in c.GetComponentsInParent<PlayMakerFSM>()) sb.Append($"，{f.name}/{f.FsmName}:{f.ActiveStateName}");
                sb.Append("）");
            }
            return sb.Length == 0 ? "无" : sb.ToString();
        }
    }
}
