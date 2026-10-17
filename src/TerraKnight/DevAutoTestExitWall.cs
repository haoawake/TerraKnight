using System.Collections;
using System.Collections.Generic;
using System.Text;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fx_exitwall", or "fx_exitwall_Key1.Key2"): each world arena's boss fought through its seal and struck
    /// down, then, once its battle gates are open, the way out: for each of the arena's gates what stands between the
    /// Knight and it (solid colliders he collides with and their state machines, the edge of the loaded world), and the
    /// Knight walked out through the nearest one. A wall left after the win shows as a gate never reached.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator ExitWallTest(string only)
        {
            if (!DevCopy.Verified) { Plugin.Log.LogError("[AutoTest] 出口空气墙需要隔离存档，不通过"); yield break; }
            var inv = mod.Inventory;
            var saved = (ItemStack[])inv.Slots.Clone();
            bool dreamNail = PlayerData.instance.hasDreamNail;
            int stage = Shop.DevStage, selected = inv.Selected;
            var kills = new Dictionary<string, int>(Bestiary.Kills);
            var keys = new HashSet<string>();
            if (only != null) foreach (var k in only.Split('.')) keys.Add(k);
            else foreach (var k in BossArena.DevKeys()) if (BossArena.DevWorldArena(k, out _, out _)) keys.Add(k);
            if (only == null) keys.Remove("HollowKnight");
            bool alive = true;
            IEnumerator Alive()
            {
                while (alive)
                {
                    mod.Stats.FullHeal();
                    if (PlayerData.instance != null) PlayerData.instance.isInvincible = true;
                    yield return null;
                }
            }
            mod.StartCoroutine(Alive());
            var outMethod = HarmonyLib.AccessTools.Method(typeof(WorldBorder), "Out");
            try
            {
                yield return SealTravel("Town", "left1");
                PlayerData.instance.hasDreamNail = true;
                WorldItems.DevNoGrab = true;
                foreach (var s in BossSummons.All)
                {
                    if (!keys.Contains(s.Key)) continue;
                    string record = s.Key.Split('@')[0];
                    Bestiary.Kills[s.Key] = 1;
                    Shop.DevStage = s.Stage;
                    inv.Slots[0] = ItemStack.Of(s.Type, 3);
                    inv.Selected = 0;
                    inv.Override = -1;
                    for (float ready = Time.realtimeSinceStartup + 15f; !GameHooks.PlayerHasControl() && Time.realtimeSinceStartup < ready;) yield return null;
                    if (!GameHooks.PlayerHasControl() && HeroController.instance != null) { HeroController.instance.RegainControl(); HeroController.instance.StartAnimationControl(); }
                    string why = BossSummons.Refusal(s);
                    if (why != null) { FxCheck($"{s.Key} 出口", false, "不能使用：" + why); continue; }
                    mod.DevUseSelected(HeroController.instance.transform.position);
                    for (float there = Time.realtimeSinceStartup + 30f; !BossArena.Arrived(s.Key) && Time.realtimeSinceStartup < there;) yield return null;
                    if (!BossArena.Arrived(s.Key)) { FxCheck($"{s.Key} 出口", false, "没进竞技场"); continue; }
                    string scene = GameManager.instance.sceneName;
                    var h = HeroController.instance;
                    var parts = new List<HealthManager>();
                    bool beaten = false;
                    int kills0 = KillsOf(record);
                    float nextHit = 0f, nextTalk = 0f;
                    for (float end = Time.realtimeSinceStartup + 150f; Time.realtimeSinceStartup < end && !beaten;)
                    {
                        if (GameManager.instance.sceneName != scene) break;
                        parts.Clear();
                        foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                            if (hm != null && (UI.EnemyHover.RecordOf(hm) == record || hm.name == "Head" && hm.gameObject.scene.name.StartsWith(scene))) parts.Add(hm);
                        if (KillsOf(record) > kills0 && !parts.Exists(hm => !hm.isDead && hm.hp > 0 && !EnemyStats.Defeated(hm))) { beaten = true; break; }
                        if (!GameHooks.PlayerHasControl() && UI.HudDialogue.DevOpen && Time.time >= nextTalk)
                        {
                            nextTalk = Time.time + 1f;
                            yield return Tap(UnityEngine.InputSystem.Key.Space);
                        }
                        if (Time.time >= nextHit)
                        {
                            nextHit = Time.time + 0.35f;
                            foreach (var hm in parts)
                            {
                                if (hm.isDead || EnemyStats.Defeated(hm) || !hm.gameObject.activeInHierarchy) continue;
                                var col = hm.GetComponent<Collider2D>();
                                if (col == null || !col.enabled)
                                {
                                    col = null;
                                    foreach (var c in hm.GetComponentsInChildren<Collider2D>())
                                        if (c.enabled && c.GetComponentInParent<HealthManager>() == hm) { col = c; break; }
                                }
                                if (col != null) mod.Melee.HitWith(col, hm.name == "Head" ? 5000 : 1500, 0f, 0f, 1f, h.GetComponent<BoxCollider2D>().bounds.center);
                            }
                        }
                        yield return null;
                    }
                    if (!beaten)
                    {
                        FxCheck($"{s.Key} 出口", false, $"没打完（场景 {GameManager.instance.sceneName}）");
                        yield return SealTravel("Town", "left1");
                        continue;
                    }
                    // The death, the reward, BossArena's gates 5 s after the win: control back and a while more.
                    for (float settle = Time.realtimeSinceStartup + 25f; Time.realtimeSinceStartup < settle;)
                    {
                        if (!GameHooks.PlayerHasControl() && UI.HudDialogue.DevOpen && Time.time >= nextTalk) { nextTalk = Time.time + 1f; yield return Tap(UnityEngine.InputSystem.Key.Space); }
                        if (Time.realtimeSinceStartup > settle - 15f && GameHooks.PlayerHasControl()) break;
                        yield return null;
                    }
                    yield return new WaitForSeconds(7f);
                    h = HeroController.instance;
                    var hcol = h.GetComponent<BoxCollider2D>();
                    int heroMask = Physics2D.GetLayerCollisionMask(h.gameObject.layer);
                    // The battle gates and their state now.
                    var sb = new StringBuilder();
                    foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        if (f.FsmName != "BG Control" || !f.gameObject.scene.name.StartsWith(scene)) continue;
                        var c = f.GetComponent<Collider2D>();
                        sb.Append($"\n  战斗门 {PathOf(f.transform)} {f.ActiveStateName} 开着 {f.gameObject.activeInHierarchy} 碰撞 {(c != null && c.enabled && f.gameObject.activeInHierarchy)} 在 {(Vector2)f.transform.position:F1}");
                    }
                    // Through each battle gate from the arena's side: stood 1.5 units inside it on the ground, walked out.
                    var bgs = new List<PlayMakerFSM>();
                    foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                        if (f.FsmName == "BG Control" && f.gameObject.scene.name.StartsWith(scene)) bgs.Add(f);
                    float mid = 0f;
                    var zone = TerrainSystem.Find(scene)?.OwnZone;
                    if (zone != null) mid = zone.Value.center.x;
                    else if (bgs.Count > 0) { foreach (var f in bgs) mid += f.transform.position.x; mid /= bgs.Count; }
                    else mid = h.transform.position.x;
                    const int terrainMask = 1 << ChunkColliders.TerrainLayer;
                    foreach (var f in bgs)
                    {
                        var ren = f.GetComponent<Renderer>();
                        Vector2 gp = f.transform.position;
                        float top = ren != null && ren.bounds.size.y > 0.5f ? ren.bounds.max.y : gp.y + 3f;
                        float outward = gp.x < mid ? -1f : 1f;
                        var ground = Physics2D.Raycast(new Vector2(gp.x - outward * 1.5f, top), Vector2.down, 40f, terrainMask);
                        if (ground.collider == null) { Plugin.Log.LogInfo($"[AutoTest] 修复1016 出口 {s.Key} 战斗门 {f.name} 内侧找不到地面，跳过"); continue; }
                        float offset0 = h.transform.position.y - hcol.bounds.min.y;
                        h.transform.position = new Vector3(gp.x - outward * 1.5f, ground.point.y + offset0 + 0.1f, h.transform.position.z);
                        h.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                        yield return new WaitForSeconds(0.5f);
                        int stops = WorldBorder.DevStops;
                        Player.TerraMovement.DevMove = (int)outward;
                        float far = 0f;
                        for (float walk = Time.realtimeSinceStartup + 1.6f; Time.realtimeSinceStartup < walk && GameManager.instance.sceneName == scene && !GameManager.instance.IsInSceneTransition;)
                        {
                            far = Mathf.Max(far, (h.transform.position.x - gp.x) * outward);
                            yield return null;
                        }
                        Player.TerraMovement.DevMove = 0;
                        bool through = far > 0.6f || GameManager.instance.IsInSceneTransition || GameManager.instance.sceneName != scene;
                        var walls = new List<string>();
                        if (!through)
                            foreach (var hit in Physics2D.BoxCastAll((Vector2)hcol.bounds.center, hcol.bounds.size * 0.9f, 0f, new Vector2(outward, 0f), 2f, heroMask))
                            {
                                if (hit.collider == null || hit.collider.isTrigger || hit.collider == hcol || hit.collider.usedByEffector || hit.collider.GetComponentInParent<HeroController>() != null) continue;
                                walls.Add($"{PathOf(hit.collider.transform)}（{hit.collider.GetType().Name} 层{hit.collider.gameObject.layer} {hit.point:F1}{FsmStates(hit.collider.gameObject, false)}）");
                            }
                        FxCheck($"{s.Key} 赢了以后穿过战斗门 {f.name}", through,
                            $"门在 {gp:F1}（{f.ActiveStateName}），往{(outward < 0 ? "左" : "右")}走出门线 {far:F1}，世界边缘挡了 {WorldBorder.DevStops - stops} 次；挡着的 {(walls.Count == 0 ? "无" : string.Join("、", walls))}");
                        if (!through) { Shot(scene, $"fx_exitwall_bg_{s.Key.Replace("@", "_")}_{f.name.Replace(" ", "")}"); yield return null; }
                        if (GameManager.instance.sceneName != scene || GameManager.instance.IsInSceneTransition) break;
                    }
                    if (GameManager.instance.sceneName != scene || GameManager.instance.IsInSceneTransition) { yield return SealTravel("Town", "left1"); continue; }
                    // Each gate: what's between the Knight (4 units inside, on the ground) and it.
                    TransitionPoint nearest = null;
                    float nearestD = float.MaxValue;
                    Vector2 heroAt = h.transform.position;
                    int blockedGates = 0, gatesSeen = 0;
                    foreach (var tp in Object.FindObjectsByType<TransitionPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    {
                        if (tp == null || !tp.gameObject.scene.name.StartsWith(scene) || tp.name.StartsWith("door_dream") || tp.name.StartsWith("dream")) continue;
                        var tc = tp.GetComponent<Collider2D>();
                        bool live = tp.gameObject.activeInHierarchy && tc != null && tc.enabled;
                        Vector2 g = tc != null && tc.enabled ? (Vector2)tc.bounds.center : (Vector2)tp.transform.position;
                        gatesSeen++;
                        // Only side gates are walked to; a top/bottom/door gate is listed.
                        bool side = tp.name.StartsWith("left") || tp.name.StartsWith("right");
                        string line = $"\n  门 {tp.name} 在 {g:F1} 启用 {live}";
                        if (side)
                        {
                            float dirX = tp.name.StartsWith("left") ? -1f : 1f;
                            var from = new Vector2(g.x - dirX * 5f, g.y);
                            var hits = Physics2D.BoxCastAll(from, hcol.bounds.size * 0.9f, 0f, new Vector2(dirX, 0f), 5f, heroMask);
                            var walls = new List<string>();
                            foreach (var hit in hits)
                            {
                                if (hit.collider == null || hit.collider.isTrigger || hit.collider == hcol || hit.collider.usedByEffector) continue;
                                if (hit.collider.GetComponentInParent<HeroController>() != null) continue;
                                walls.Add($"{PathOf(hit.collider.transform)}（层{hit.collider.gameObject.layer} {hit.point:F1}{FsmStates(hit.collider.gameObject, false)}）");
                            }
                            var edge = new List<string>();
                            for (float t = 0f; t <= 5f; t += 0.5f)
                            {
                                var p = from + new Vector2(dirX * t, 0f);
                                if ((bool)outMethod.Invoke(null, new object[] { p })) edge.Add($"{p.x:F1}");
                            }
                            if (walls.Count > 0 || edge.Count > 0) blockedGates++;
                            line += $"：挡着的 {(walls.Count == 0 ? "无" : string.Join("、", walls))}；世界边缘 {(edge.Count == 0 ? "无" : string.Join(",", edge))}";
                            float d = Vector2.Distance(g, heroAt);
                            if (live && d < nearestD) { nearestD = d; nearest = tp; }
                        }
                        sb.Append(line);
                    }
                    Plugin.Log.LogInfo($"[AutoTest] 修复1016 出口 {s.Key} {scene}：{gatesSeen} 道门，{blockedGates} 道有东西挡着{sb}");
                    // Out through the nearest side gate, walking.
                    if (nearest == null) { FxCheck($"{s.Key} 出口", false, "没有能走的侧门"); yield return SealTravel("Town", "left1"); continue; }
                    var ntc = nearest.GetComponent<Collider2D>();
                    float dir = nearest.name.StartsWith("left") ? -1f : 1f;
                    Vector2 gc = ntc.bounds.center;
                    float offset = h.transform.position.y - hcol.bounds.min.y;
                    h.transform.position = new Vector3(gc.x - dir * 4f, ntc.bounds.min.y + offset + 0.3f, h.transform.position.z);
                    h.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                    yield return new WaitForSeconds(0.8f);
                    int stops0 = WorldBorder.DevStops;
                    Shot(scene, "fx_exitwall_" + s.Key.Replace("@", "_"));
                    yield return null;
                    Player.TerraMovement.DevMove = (int)dir;
                    float minDist = float.MaxValue;
                    for (float walk = Time.realtimeSinceStartup + 4f; Time.realtimeSinceStartup < walk && GameManager.instance.sceneName == scene && !GameManager.instance.IsInSceneTransition;)
                    {
                        if (ntc != null) minDist = Mathf.Min(minDist, Mathf.Abs(h.transform.position.x - gc.x));
                        yield return null;
                    }
                    Player.TerraMovement.DevMove = 0;
                    bool left = GameManager.instance.sceneName != scene || GameManager.instance.IsInSceneTransition;
                    string where = $"{(Vector2)h.transform.position:F1}";
                    FxCheck($"{s.Key} 赢了以后从 {nearest.name} 走得出去", left,
                        $"现在 {GameManager.instance.sceneName} {where}，离门最近 {minDist:F1}，世界边缘挡了 {WorldBorder.DevStops - stops0} 次（{WorldBorder.DevLast}）");
                    for (float t = Time.realtimeSinceStartup + 10f; Time.realtimeSinceStartup < t && GameManager.instance.IsInSceneTransition;) yield return null;
                    yield return SealTravel("Town", "left1");
                }
            }
            finally
            {
                alive = false;
                Player.TerraMovement.DevMove = 0;
                WorldItems.DevNoGrab = false;
                System.Array.Copy(saved, inv.Slots, saved.Length);
                inv.Selected = selected;
                Shop.DevStage = stage;
                PlayerData.instance.hasDreamNail = dreamNail;
                Bestiary.Kills.Clear();
                foreach (var kv in kills) Bestiary.Kills[kv.Key] = kv.Value;
            }
        }
    }
}
