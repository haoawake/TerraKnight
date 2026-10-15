using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TerraKnight.Items;
using TerraKnight.Player;
using TerraKnight.World;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suites "quakeall1".."quakeall3"): every scene of the world map, a third at a time, and in
    /// each the floors the Knight breaks with a dive (an FSM named for the quake, or one that answers a
    /// QUAKE event): the Ram Rune's slam from above each (a bomb on it if the slam leaves it), and
    /// whether it broke (its FSM's state, its collider), whether the player fell through, what a ray
    /// down through it meets after, and the grid's cells there. One left whole, or still in the way
    /// below, is logged as a problem and photographed. Suite "quakepick": a pickaxe on Crossroads_52's.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        const int QuakeParts = 3;

        static bool IsQuakeFsm(PlayMakerFSM fsm)
        {
            string n = fsm.FsmName.ToLowerInvariant();
            if (n.Contains("quake")) return true;
            if (fsm.gameObject.name.ToLowerInvariant().Contains("quake")) return true;
            foreach (var e in fsm.FsmEvents)
                if (e != null && (e.Name == "QUAKE" || e.Name == "QUAKE FALL START" || e.Name == "QUAKE FALL END")) return true;
            return false;
        }

        IEnumerator QuakeAllTest(int part, string resumeAt = null)
        {
            var scenes = new List<string>();
            foreach (var s in UI.WorldMap.DevSceneNames())
            {
                bool skip = false;
                foreach (var p in tourSkip) if (s.StartsWith(p)) skip = true;
                if (!skip) scenes.Add(s);
            }
            scenes.Sort(System.StringComparer.Ordinal);
            int from = scenes.Count * (part - 1) / QuakeParts, to = scenes.Count * part / QuakeParts;
            // "quakeall1@Scene": from that scene on (after a run that stopped); "quakeall0@A+B": just those scenes.
            if (part == 0 && resumeAt != null)
            {
                scenes = new List<string>(resumeAt.Split('+'));
                from = 0;
                to = scenes.Count;
            }
            else if (resumeAt != null) from = Mathf.Max(from, scenes.IndexOf(resumeAt));
            Plugin.Log.LogInfo($"[AutoTest] 下砸全图 第{part}段：{from}..{to - 1}（共 {scenes.Count} 个场景）");
            const int terrain = 1 << ChunkColliders.TerrainLayer;
            int floors = 0, problems = 0, scenesWith = 0;
            var bad = new StringBuilder();
            var kinds = new Dictionary<string, int>();
            int bySlam = 0, byBomb = 0, fellThrough = 0;
            var inv = mod.Inventory;
            var wornBefore = (ItemStack[])inv.Accessories.Clone();
            Wear(ItemType.DeadCellsRamRune);
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
                yield return new WaitForSeconds(1.2f);
                if (gm.sceneName != scene) { Plugin.Log.LogInfo($"[AutoTest] 下砸全图 {scene}：没进去"); continue; }
                var found = new List<(PlayMakerFSM Fsm, Collider2D Col)>();
                var seenObj = new HashSet<GameObject>();
                foreach (var fsm in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                {
                    if (fsm.gameObject.scene.name != scene || !IsQuakeFsm(fsm)) continue;
                    // Its solid collider: on it or its children (not a trigger: the detector beside it).
                    Collider2D solid = null;
                    foreach (var c in fsm.GetComponentsInChildren<Collider2D>(true))
                        if (!c.isTrigger && c.gameObject.layer == ChunkColliders.TerrainLayer) { solid = c; break; }
                    if (solid == null || !seenObj.Add(solid.gameObject)) continue;
                    if (!solid.enabled || !solid.gameObject.activeInHierarchy) continue;
                    found.Add((fsm, solid));
                }
                if (found.Count == 0) continue;
                scenesWith++;
                var targets = new List<(string Path, string Name)>();
                foreach (var (f0, _) in found) targets.Add((PathOf(f0.transform), f0.FsmName));
                foreach (var (path, fname) in targets)
                {
                    // A floor broken before this one may have dropped the player into the room below.
                    if (gm.sceneName != scene)
                    {
                        yield return GoTo(scene, EntryGateOf(scene));
                        yield return new WaitForSeconds(1.2f);
                        PlayerData.instance.isInvincible = true;
                    }
                    PlayMakerFSM fsm = null;
                    Collider2D col = null;
                    foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                        if (f.gameObject.scene.name == scene && f.FsmName == fname && PathOf(f.transform) == path) { fsm = f; break; }
                    if (fsm != null)
                        foreach (var c in fsm.GetComponentsInChildren<Collider2D>(true))
                            if (!c.isTrigger && c.gameObject.layer == ChunkColliders.TerrainLayer) { col = c; break; }
                    if (fsm == null || col == null || !col.enabled || !col.gameObject.activeInHierarchy)
                    {
                        Plugin.Log.LogInfo($"[AutoTest] 下砸全图 {scene} {path}：重新进来后已经是打开的");
                        continue;
                    }
                    var h = HeroController.instance;
                    floors++;
                    string kind = fsm.FsmName;
                    kinds[kind] = (kinds.TryGetValue(kind, out int k) ? k : 0) + 1;
                    var b = col.bounds;
                    Vector2 top = new Vector2(b.center.x, b.max.y + 0.3f);
                    string before = fsm.ActiveStateName;
                    var cellTop = CellGrid.WorldToCell(new Vector2(b.center.x, b.max.y - 0.1f));
                    bool cellSolidBefore = TerraWorld.IsSolid(cellTop.x, cellTop.y);
                    // The Ram Rune's slam from just above it (under whatever ledge is over it).
                    var rb = h.GetComponent<Rigidbody2D>();
                    h.transform.position = Clear(h, top + new Vector2(0f, 1.3f));
                    if (rb != null) rb.linearVelocity = Vector2.zero;
                    yield return null;
                    var feet = h.GetComponent<BoxCollider2D>().bounds;
                    var under = Physics2D.Raycast(new Vector2(feet.center.x, feet.min.y + 0.05f), Vector2.down, 3f, terrain);
                    string placed = $"放在 {(Vector2)h.transform.position:F1}，脚下 {(under.collider != null ? under.collider.name + " " + under.distance.ToString("F2") : "无")}";
                    // A little hop first, so the slam starts in the air.
                    if (rb != null) rb.linearVelocity = new Vector2(0f, 6f);
                    yield return new WaitForSeconds(0.15f);
                    int pounds0 = TerraMovement.DevPounds;
                    TerraMovement.DevDown = true;
                    TerraMovement.DevDownDash = true;
                    for (float t = 0f; t < 2f && TerraMovement.DevPounds == pounds0; t += Time.deltaTime) yield return null;
                    TerraMovement.DevDown = false;
                    bool pounded = TerraMovement.DevPounds > pounds0;
                    // Long enough for a floor's own breaking (Crystal Peak's: a pause, its hit, 0.85 s, then the boards go).
                    yield return new WaitForSeconds(2.0f);
                    bool gone = col == null || !col.enabled || !col.gameObject.activeInHierarchy;
                    var hb = h.GetComponent<BoxCollider2D>().bounds;
                    bool fell = gone && hb.max.y < b.min.y + 0.1f;
                    string how = gone ? "猛击" : null;
                    if (gone) bySlam++;
                    if (fell) fellThrough++;
                    if (!gone)
                    {
                        // The player off to the side, out of the blast.
                        h.transform.position = Clear(h, top + new Vector2(8f, 1f));
                        if (rb != null) rb.linearVelocity = Vector2.zero;
                        yield return new WaitForSeconds(0.3f);
                        Items.TerraProjectiles.Instance.Shoot(Items.ProjKind.Bomb, top, Vector2.zero, 0, 0f, 0f);
                        yield return new WaitForSeconds(3.6f);
                        gone = col == null || !col.enabled || !col.gameObject.activeInHierarchy;
                        if (gone) { how = "炸弹（猛击没砸开" + (pounded ? "" : "，也没落地猛击") + "）"; byBomb++; }
                    }
                    // What is still in the way going down through it (from above its top to a little below its bottom).
                    float depth = b.size.y + 0.6f;
                    var hit = Physics2D.Raycast(new Vector2(b.center.x, b.max.y + 0.2f), Vector2.down, depth, terrain);
                    string below = hit.collider == null ? "通" : $"{hit.collider.name}（往下 {hit.distance:F2}）";
                    bool cellSolid = false;
                    for (int dy = 0; dy >= -Mathf.CeilToInt(b.size.y / CellGrid.CellSize); dy--)
                        cellSolid |= TerraWorld.IsSolid(cellTop.x, cellTop.y + dy);
                    bool problem = !gone || hit.collider != null || how != "猛击";
                    string line = $"{scene} {path} [{kind}] {(Vector2)b.center:F1} 大小 {(Vector2)b.size:F1}：状态 {before}→{(fsm != null ? fsm.ActiveStateName : "没了")}，{(gone ? "被" + how + "打开" : "猛击和炸弹都没打开")}（{placed}），人掉下去 {fell}，往下 {below}，格子实心 炸前 {cellSolidBefore} 炸后 {cellSolid}；{Items.TerraProjectiles.DevLastBlast}";
                    if (problem) { problems++; if (bad.Length < 8000) bad.Append("\n  ").Append(line); }
                    Plugin.Log.LogInfo($"[AutoTest] 下砸全图 {line}{(problem ? " ← 有问题" : "")}");
                    if (problem)
                    {
                        Player.TerraCamera.DevLook = top;
                        yield return new WaitForSeconds(0.4f);
                        yield return CloseUpAt(scene, $"quake_{Clean(path)}_bad", top, 200, 2);
                        Player.TerraCamera.DevLook = null;
                    }
                }
            }
            for (int k = 0; k < wornBefore.Length; k++) inv.Accessories[k] = wornBefore[k];
            var kindText = new StringBuilder();
            foreach (var kv in kinds) kindText.Append($" {kv.Key}×{kv.Value}");
            Plugin.Log.LogInfo($"[AutoTest] 下砸全图 第{part}段合计：{scenesWith} 个场景里 {floors} 块地板（{kindText}），猛击砸开 {bySlam} 块（人掉下去 {fellThrough} 块），炸弹才打开 {byBomb} 块，{problems} 块有问题{bad}");
        }
        IEnumerator QuakePickTest()
        {
            yield return GoTo("Crossroads_52", "left1");
            yield return new WaitForSeconds(1.5f);
            PlayerData.instance.isInvincible = true;
            var hero = HeroController.instance;
            var qf = GameObject.Find("Quake Floor");
            var fsm = qf != null ? qf.GetComponent<PlayMakerFSM>() : null;
            var col = qf != null ? qf.GetComponent<Collider2D>() : null;
            if (fsm == null || col == null) { Plugin.Log.LogInfo("[AutoTest] 镐子挖下砸地板 没找到 Quake Floor 不通过"); yield break; }
            var b = col.bounds;
            hero.transform.position = Clear(hero, new Vector2(b.min.x - 1.2f, b.max.y + 1f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            var pick = ItemDb.Get(ItemType.CopperPickaxe);
            int swings = 0, broken0 = DiveFloors.DevBroken;
            Vector2 aim = new Vector2(b.min.x + 0.3f, b.center.y);
            while (swings < 20 && DiveFloors.DevBroken == broken0)
            {
                if (!DiveFloors.TryMine(pick, aim, hero)) break;
                swings++;
                yield return new WaitForSeconds(0.25f);
            }
            yield return new WaitForSeconds(1f);
            bool gone = !col.enabled || !col.gameObject.activeInHierarchy;
            bool pass = gone && swings == Mathf.CeilToInt(200f / pick.PickPower);
            Plugin.Log.LogInfo($"[AutoTest] 镐子挖下砸地板 Crossroads_52：铜镐（镐力 {pick.PickPower}）挖了 {swings} 下，状态 {fsm.ActiveStateName}，碰撞体{(gone ? "没了" : "还在")} {(pass ? "通过" : "不通过")}");
            Shot("Crossroads_52", "quakepick");
        }
        /// <summary>Dev (suite "quakedump"): Crystal Peak's entrance (Mines_01), its dive floor's state machines and what is under it.</summary>
        IEnumerator QuakeDumpTest()
        {
            yield return GoTo("Mines_01", EntryGateOf("Mines_01"));
            yield return new WaitForSeconds(2f);
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (f.gameObject.scene.name != "Mines_01") continue;
                string key = (PathOf(f.transform) + " " + f.FsmName).ToLowerInvariant();
                if (!key.Contains("quake") && !key.Contains("loose") && !key.Contains("floor")) continue;
                foreach (var c in f.GetComponentsInChildren<Collider2D>(true))
                    Plugin.Log.LogInfo($"[AutoTest] 下砸导出 {PathOf(c.transform)} 碰撞 {c.GetType().Name} 触发 {c.isTrigger} 层 {c.gameObject.layer} 启用 {c.enabled}/{c.gameObject.activeInHierarchy} {(Vector2)c.bounds.center:F1} {(Vector2)c.bounds.size:F1}");
                DumpFsm(f);
            }
        }
    }
}
