using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TerraKnight.World;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suites "npcfloor1".."npcfloor3"): every scene of the world map, a third at a time, and in
    /// each the townsfolk and wanderers the player can listen to (the game's "npc_control" FSMs): what
    /// is under their feet. The grid's cells (solid, dug, the scene's art cut away there), the collider
    /// a ray down meets (the grid's own or a scene collider kept out of the grid), and a close-up of
    /// the spot. An NPC standing on nothing the grid draws is logged as a problem.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        const int NpcFloorParts = 3;

        IEnumerator NpcFloorTest(int part, string resumeAt = null)
        {
            var scenes = new List<string>();
            foreach (var s in UI.WorldMap.DevSceneNames())
            {
                bool skip = false;
                foreach (var p in tourSkip) if (s.StartsWith(p)) skip = true;
                if (!skip) scenes.Add(s);
            }
            scenes.Sort(System.StringComparer.Ordinal);
            int from = scenes.Count * (part - 1) / NpcFloorParts, to = scenes.Count * part / NpcFloorParts;
            // "npcfloor3@Scene": from that scene on (after a run that stopped).
            if (resumeAt != null) from = Mathf.Max(from, scenes.IndexOf(resumeAt));
            Plugin.Log.LogInfo($"[AutoTest] NPC脚下 第{part}段：{from}..{to - 1}（共 {scenes.Count} 个场景）");
            const int terrain = 1 << ChunkColliders.TerrainLayer;
            int npcs = 0, problems = 0, scenesWith = 0;
            var bad = new StringBuilder();
            for (int i = from; i < to; i++)
            {
                string scene = scenes[i];
                var gm = GameManager.instance;
                float t0 = Time.time;
                while (gm.IsInSceneTransition && Time.time - t0 < 10f) yield return null;
                PlayerData.instance.isInvincible = true;
                gm.BeginSceneTransition(new GameManager.SceneLoadInfo
                {
                    SceneName = scene, EntryGateName = EntryGateOf(scene), HeroLeaveDirection = GlobalEnums.GatePosition.unknown,
                    EntryDelay = 0f, WaitForSceneTransitionCameraFade = true, Visualization = GameManager.SceneLoadVisualizations.Default,
                });
                t0 = Time.time;
                yield return new WaitForSeconds(0.5f);
                while ((gm.sceneName != scene || gm.IsInSceneTransition) && Time.time - t0 < 20f) yield return null;
                PlayerData.instance.isInvincible = true;
                yield return new WaitForSeconds(1.5f);
                if (gm.sceneName != scene) { Plugin.Log.LogInfo($"[AutoTest] NPC脚下 {scene}：没进去"); continue; }
                var found = new List<PlayMakerFSM>();
                foreach (var fsm in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                    if (fsm.FsmName == "npc_control" && fsm.gameObject.scene.name == scene && fsm.gameObject.activeInHierarchy) found.Add(fsm);
                if (found.Count == 0) continue;
                scenesWith++;
                var h = HeroController.instance;
                foreach (var fsm in found)
                {
                    // Revek attacks whoever comes near (his dream ghost kills through invincibility).
                    if (fsm == null || fsm.gameObject.name.Contains("revek")) continue;
                    if (gm.sceneName != scene) break;
                    PlayerData.instance.isInvincible = true;
                    npcs++;
                    var root = fsm.transform;
                    // The NPC's body: its biggest renderer's bounds (the talk region's collider is wider than it).
                    Bounds body = new Bounds(root.position, Vector3.zero);
                    float best = 0f;
                    foreach (var r in root.GetComponentsInChildren<Renderer>())
                    {
                        if (!r.enabled || r is ParticleSystemRenderer) continue;
                        float a = r.bounds.size.x * r.bounds.size.y;
                        if (a > best && a < 60f) { best = a; body = r.bounds; }
                    }
                    if (best <= 0f) body = new Bounds(root.position, Vector3.one);
                    Vector2 feet = new Vector2(body.center.x, body.min.y);
                    // What a ray straight down from just above its feet meets.
                    var hit = Physics2D.Raycast(feet + Vector2.up * 0.4f, Vector2.down, 3f, terrain);
                    float drop = hit.collider != null ? hit.distance - 0.4f : 99f;
                    bool ours = hit.collider != null && hit.collider.name.StartsWith("Terra Chunk");
                    // The grid's cells under it.
                    var cell = CellGrid.WorldToCell(feet + Vector2.down * 0.25f);
                    bool solid = false, dug = false, cut = false;
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dy = 0; dy >= -2; dy--)
                        {
                            solid |= TerraWorld.IsSolid(cell.x + dx, cell.y + dy);
                            dug |= TerraWorld.IsDug(cell.x + dx, cell.y + dy);
                            cut |= TerraWorld.ArtCutAt(cell.x + dx, cell.y + dy);
                        }
                    bool floating = drop > 1.2f;
                    bool problem = floating || !solid || dug || cut;
                    string what = hit.collider != null ? $"{hit.collider.name}（{hit.collider.GetType().Name}{(ours ? "，格子" : "，场景自己的")}，往下 {drop:F2}）" : "下面 3 格内没有碰撞体";
                    string line = $"{scene} {root.name} 脚下 {feet:F1}：{what}，格子实心 {solid}，挖过 {dug}，贴图被抠 {cut}";
                    if (problem) { problems++; if (bad.Length < 6000) bad.Append("\n  ").Append(line); }
                    Plugin.Log.LogInfo($"[AutoTest] NPC脚下 {line}{(problem ? " ← 有问题" : "")}");
                    // A look at the spot: the player moved aside so it isn't in the picture.
                    h.transform.position = new Vector3(feet.x + 6f, feet.y + 3f, h.transform.position.z);
                    var rb = h.GetComponent<Rigidbody2D>();
                    if (rb != null) rb.linearVelocity = Vector2.zero;
                    Player.TerraCamera.DevLook = feet;
                    yield return new WaitForSeconds(0.5f);
                    yield return CloseUpAt(scene, $"npcfloor_{Clean(root.name)}{(problem ? "_bad" : "")}", feet + Vector2.up * 1f, 150, 2);
                    Player.TerraCamera.DevLook = null;
                }
            }
            Plugin.Log.LogInfo($"[AutoTest] NPC脚下 第{part}段合计：{scenesWith} 个场景里 {npcs} 个 NPC，{problems} 个脚下有问题{bad}");
        }

        static string Clean(string s)
        {
            var sb = new StringBuilder();
            foreach (char c in s) sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            return sb.ToString();
        }
    }
}
