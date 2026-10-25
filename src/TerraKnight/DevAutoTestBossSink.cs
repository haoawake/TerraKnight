using System;
using System.Collections;
using System.Linq;
using TerraKnight.EnemyAi.Bosses;
using TerraKnight.Items;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "bosssink"): a boss on the ground fought with the new AI (the Pure Vessel; TK_SINK_KEYS picks others,
    /// TK_SINK_VANILLA=1 the game's own AI), the player put by turns on its floor and over it at the arena's left, right
    /// and middle: every frame the boss's body against the floor's top under it, a line and a screenshot each time its
    /// feet are well under it, to see where and in which state a boss sinks into its floor.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator BossSinkTest()
        {
            mod.ApplyCreative(false);
            Player.TerraStats.DevImmortal = true;
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            bool vanilla = Environment.GetEnvironmentVariable("TK_SINK_VANILLA") == "1";
            World.EnemyAiMode.Current = vanilla ? World.EnemyAiKind.Vanilla : World.EnemyAiKind.New;
            string pick = Environment.GetEnvironmentVariable("TK_SINK_KEYS");
            var keys = string.IsNullOrWhiteSpace(pick) ? new[] { "HollowKnightPrime" } : pick.Split(';');
            float seconds = float.TryParse(Environment.GetEnvironmentVariable("TK_SINK_SECONDS"), out var s) ? s : 60f;
            foreach (var key in keys)
            {
                int started = BossSummonStart.DevStarted;
                string record = key.Split('@')[0];
                // A boss with a seal is fought at once; one without (the Vengefly King, the Soul Warrior, the Hollow Knight)
                // in its own room, as the combat suite does.
                if (BossArena.Has(key))
                {
                    BossArena.Fight(key, true);
                    float until = Time.realtimeSinceStartup + 40f;
                    while (!BossArena.Arrived(key) && Time.realtimeSinceStartup < until) yield return null;
                    until = Time.realtimeSinceStartup + 15f;
                    while (BossSummonStart.DevStarted == started && Time.realtimeSinceStartup < until) yield return null;
                }
                else
                {
                    // The roster's first room for a record is where it was first met, not always its fight's (the Vengefly King's
                    // is Greenpath's arena).
                    string room = record == "BigBuzzer" ? "Fungus1_20_v02" : EnemyDrops.SceneOf(record);
                    if (string.IsNullOrEmpty(room)) { Plugin.Log.LogWarning($"[AutoTest] 首领陷地 {key}：没有它的房间"); continue; }
                    yield return GoTo(room, EntryGateOf(room));
                }
                HealthManager hm = null;
                BossKit kit = null;
                // Up to 70 s for it to come; an arena's waves before it (the Traitor Lord's Mantis Traitors) put down, as a
                // fighter would.
                float waitFrom = Time.realtimeSinceStartup, clearAt = waitFrom + 8f;
                while (hm == null && Time.realtimeSinceStartup - waitFrom < 70f)
                {
                    kit = vanilla ? null : KitOf(record);
                    hm = kit != null ? kit.Hm : Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None).FirstOrDefault(h => EnemyStats.Of(h)?.Record == record && h.gameObject.activeInHierarchy);
                    if (hm != null && !vanilla && kit == null) hm = null;
                    if (hm == null && Time.realtimeSinceStartup >= clearAt && !GameManager.instance.IsInSceneTransition)
                    {
                        clearAt = Time.realtimeSinceStartup + 2f;
                        foreach (var h in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                        {
                            var info = EnemyStats.Of(h);
                            if (h == null || h.isDead || !h.gameObject.activeInHierarchy || info == null || info.Boss || info.Record == record) continue;
                            if (!h.gameObject.scene.name.StartsWith(GameManager.instance.sceneName)) continue;
                            h.Die(null, AttackTypes.Generic, true);
                        }
                    }
                    if (hm == null) yield return new WaitForSeconds(0.1f);
                }
                var scene = GameManager.instance.sceneName;
                if (hm == null)
                {
                    Plugin.Log.LogWarning($"[AutoTest] 首领陷地 {key}：找不到首领");
                    continue;
                }
                var hero = HeroController.instance;
                var ctl = hm.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Control") ?? hm.GetComponent<PlayMakerFSM>();
                // The arena: the kit's, else round where the boss and the player start.
                Rect arena = kit != null ? kit.Arena : new Rect(Mathf.Min(hm.transform.position.x, hero.transform.position.x) - 15f, hero.transform.position.y - 3f, 30f + Mathf.Abs(hm.transform.position.x - hero.transform.position.x), 20f);
                // The floor's top under a point: the first ground straight down from it (from the arena's middle height for the player's spots).
                string floorName = "";
                float FloorTop(float x, float from)
                {
                    var hit = EnemyAi.Hostiles.NativeRay(new Vector2(x, from), Vector2.down, from - arena.yMin + 12f);
                    floorName = hit.collider != null ? $"{hit.collider.gameObject.name}/{hit.collider.GetType().Name.Replace("Collider2D", "")}" : "";
                    return hit.collider != null ? hit.point.y : float.NaN;
                }
                Collider2D Body()
                {
                    Collider2D best = null;
                    foreach (var c in hm.GetComponentsInChildren<Collider2D>())
                        if (c.enabled && c.gameObject.activeInHierarchy && c.bounds.size.y > 0.5f && (best == null || !c.isTrigger && best.isTrigger || c.isTrigger == best.isTrigger && c.bounds.size.y > best.bounds.size.y)) best = c;
                    return best;
                }
                Plugin.Log.LogInfo($"[AutoTest] 首领陷地 {key} 碰撞体：{string.Join("，", hm.GetComponentsInChildren<Collider2D>(true).Select(c => $"{c.gameObject.name}/{c.GetType().Name}{(c.enabled ? "" : "(关)")}{(c.isTrigger ? "(触发)" : "")} {c.bounds.size.x:F1}x{c.bounds.size.y:F1}"))}");
                Plugin.Log.LogInfo($"[AutoTest] 首领陷地 {key}（{(vanilla ? "原版AI" : "新版AI")}）{scene} 场地 {arena}");
                // The floor across the room: at each x the first thing straight down from the arena's middle height, whose
                // collider it is, and whether the new AI's bosses go through it (a cell the player made, ChunkColliders.Built).
                var profile = new System.Text.StringBuilder();
                var rayHits = new RaycastHit2D[16];
                for (float x = arena.xMin - 6f; x <= arena.xMax + 6f; x += 1f)
                {
                    int n = Physics2D.RaycastNonAlloc(new Vector2(x, arena.center.y), Vector2.down, rayHits, arena.height + 12f, 1 << 8);
                    var hit = rayHits.Take(n).Where(h => h.collider != null && !h.collider.isTrigger).OrderBy(h => h.distance).FirstOrDefault();
                    profile.Append($" {x:F0}:{(hit.collider == null ? "-" : $"{hit.point.y:F1}{(World.ChunkColliders.IsBuilt(hit.collider) ? "建" : "")}({hit.collider.name}/{hit.collider.GetType().Name.Replace("Collider2D", "")})")}");
                }
                Plugin.Log.LogInfo($"[AutoTest] 首领陷地 {key} 地面剖面（x:顶 y，建=新版AI首领会穿过）：{profile}");
                // The room's ground colliders (layer 8) near the arena's sides, and the grid's cells along its edges.
                foreach (var c in Object.FindObjectsByType<Collider2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (c.gameObject.layer != 8 || c.usedByComposite || c.gameObject.scene != hm.gameObject.scene) continue;
                    var cb = c.bounds;
                    if (c.enabled && (cb.max.x < arena.xMin - 8f || cb.min.x > arena.xMax + 8f)) continue;
                    string pts = c is EdgeCollider2D e ? string.Join(" ", e.points.Select(q => (Vector2)c.transform.TransformPoint(q)).Select(q => $"({q.x:F1},{q.y:F1})").Take(24)) : "";
                    Plugin.Log.LogInfo($"[AutoTest] 首领陷地 {key} 地形碰撞体 {c.gameObject.name}/{c.GetType().Name}{(c.enabled && c.gameObject.activeInHierarchy ? "" : "(关)")}{(c.isTrigger ? "(触发)" : "")} {cb.min.x:F1}..{cb.max.x:F1} × {cb.min.y:F1}..{cb.max.y:F1} {pts}");
                }
                    foreach (var ts in World.TerrainSystem.All)
                        if (ts.SceneName == hm.gameObject.scene.name)
                            Plugin.Log.LogInfo($"[AutoTest] HANDPLACED zone={ts.Region?.OwnZone} how={ts.OwnZoneHow} " + string.Join(" | ", ts.HandPlaced.Select(h => $"{h.Name} {h.Bounds.min.x:F2}..{h.Bounds.max.x:F2} x {h.Bounds.min.y:F2}..{h.Bounds.max.y:F2}")));
                    foreach (float side in new[] { arena.xMin, arena.xMax })
                    {
                        var rows = new System.Text.StringBuilder();
                        for (float y = 7f; y >= -1f; y -= 0.5f)
                        {
                            rows.Append($"\n    y {y,4:F1} ");
                            for (float x = side - 5f; x <= side + 5f; x += 0.5f)
                            {
                                var cell = World.TerraWorld.Get(Mathf.FloorToInt(x / 0.5f), Mathf.FloorToInt(y / 0.5f));
                                rows.Append(cell == World.CellKind.Empty ? '.' : cell == World.CellKind.Original ? '#' : 'b');
                            }
                        }
                        Plugin.Log.LogInfo($"[AutoTest] 首领陷地 {key} 格子 x {side - 5f:F1}..{side + 5f:F1}（# 原版，b 其他，. 空）：{rows}");
                    }
                int sunk = 0, samples = 0, shots = 0;
                float worst = 0f, nextShot = 0f, nextLine = 0f;
                string worstAt = "", lastState = null;
                bool wasUnder = false;
                var trail = new System.Collections.Generic.List<string>();
                float start = Time.time, nextWake = 1f;
                int wakes = 0;
                var spots = new[] { (arena.xMin + 1.5f, false), (arena.xMin + 1.5f, true), (arena.xMax - 1.5f, false), (arena.xMax - 1.5f, true), (arena.center.x, false) };
                while (Time.time - start < seconds && hm != null && !hm.isDead)
                {
                    float t = Time.time - start;
                    var (sx, air) = spots[(int)(t / 4f) % spots.Length];
                    float ground = FloorTop(sx, arena.center.y);
                    if (!float.IsNaN(ground) && (air || t % 4f < 0.12f))
                        hero.transform.position = new Vector3(sx, ground + (air ? 5f : 1.2f), hero.transform.position.z);
                    if (mod.Stats.Life < mod.Stats.LifeMax / 2) mod.Stats.FullHeal();
                    // A boss still waiting (the Vengefly King hanging) woken as a player's first blow would.
                    if (t >= nextWake && wakes < 3 && ctl != null && bossWaits.IsMatch(ctl.ActiveStateName ?? ""))
                    {
                        nextWake = t + 2f;
                        wakes++;
                        if (!BossWake.Rouse(hm))
                        {
                            hm.Hit(new HitInstance { Source = hero.gameObject, AttackType = AttackTypes.Generic, DamageDealt = 1, Direction = hm.transform.position.x > hero.transform.position.x ? 0f : 180f, Multiplier = 1f, MagnitudeMultiplier = 0f });
                            FSMUtility.SendEventToGameObject(hm.gameObject, "TAKE DAMAGE");
                        }
                    }
                    var col = Body();
                    if (col != null)
                    {
                        var b = col.bounds;
                        float top = FloorTop(b.center.x, b.max.y + 0.3f);
                        // Only its solid body counts (a range sensor in a teleport reaches far over the room).
                        float under = float.IsNaN(top) || col.isTrigger ? 0f : top - b.min.y;
                        samples++;
                        string state = ctl?.ActiveStateName;
                        var rb = hm.GetComponent<Rigidbody2D>();
                        string now = $"{t:F2}s {state} 根 ({hm.transform.position.x:F2},{hm.transform.position.y:F2}) 体中 x {b.center.x:F2} 体 {col.gameObject.name} 底 {b.min.y:F2} 地面 {top:F2} 速度 {(rb != null ? rb.linearVelocity : Vector2.zero)} 运动学 {(rb != null && rb.isKinematic)}";
                        // The frames as it goes in: the last ones before and the first one under.
                        trail.Add(now);
                        if (trail.Count > 12) trail.RemoveAt(0);
                        if (state != lastState) { Plugin.Log.LogInfo($"[AutoTest] 首领陷地 {key} 状态 {now}"); lastState = state; }
                        if (under > 0.5f && !wasUnder)
                            Plugin.Log.LogInfo($"[AutoTest] 首领陷地 {key} 陷进去了，此前几帧：\n    {string.Join("\n    ", trail)}");
                        wasUnder = under > 0.5f;
                        if (under > 0.3f)
                        {
                            sunk++;
                            if (under > worst) { worst = under; worstAt = $"{t:F1}s {state} ({b.center.x:F1},{b.min.y:F1}) 地面 {top:F1}"; }
                            if (Time.time >= nextLine)
                            {
                                nextLine = Time.time + 0.3f;
                                Plugin.Log.LogInfo($"[AutoTest] 首领陷地 {key} {t:F1}s 状态 {state} 脚底 {b.min.y:F2} 地面 {top:F2} 陷 {under:F2}（地面是 {floorName}） {col.gameObject.name} 中心 ({b.center.x:F1},{b.center.y:F1}) 速度 {hm.GetComponent<Rigidbody2D>()?.linearVelocity} 主角 ({hero.transform.position.x:F1},{hero.transform.position.y:F1})");
                            }
                            if (under > 0.5f && Time.time >= nextShot && shots < 12)
                            {
                                nextShot = Time.time + 1.5f;
                                shots++;
                                Shot(scene, $"sink_{key}_{(vanilla ? "v" : "n")}_{t:00.0}");
                            }
                        }
                    }
                    yield return null;
                }
                Plugin.Log.LogInfo($"[AutoTest] 首领陷地 {key}（{(vanilla ? "原版AI" : "新版AI")}）结果：{sunk}/{samples} 次脚底低于地面 0.3 以上，最深 {worst:F2}（{worstAt}）");
            }
            Plugin.Log.LogInfo("[AutoTest] 首领陷地 完成");
        }
    }
}
