using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.EnemyAi.Bosses;
using TerraKnight.Items;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "vkwall", the user's 10-29 "反击蝇之王会卡墙"): the Vengefly King fought in his rooms (Greenpath's, then
    /// Godhome's hall; TK_VKW_ROOMS "room" or "room#gate", TK_VKW_VANILLA=1 the game's own AI), the player put on every
    /// floor of the room by turns (TK_VKW_HOLD s each, TK_VKW_SECONDS a room). The room's grid printed first (# rock, . open,
    /// A the arena's corners). Each frame his body against the room's rock: its middle inside rock by the grid (a run of
    /// such frames is "in the wall": the frames before it and its length logged), and a second and a half in which he
    /// pressed on the room's ground and moved under 0.5 ("against the wall"). Lines "[AutoTest] VkWall …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator VengeflyWallTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            mod.ApplyCreative(false);
            Player.TerraStats.DevImmortal = true;
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            bool vanilla = Environment.GetEnvironmentVariable("TK_VKW_VANILLA") == "1";
            VengeflyKingKit.DevWatch = true;
            World.EnemyAiMode.Current = vanilla ? World.EnemyAiKind.Vanilla : World.EnemyAiKind.New;
            string pick = Environment.GetEnvironmentVariable("TK_VKW_ROOMS");
            var rooms = string.IsNullOrWhiteSpace(pick) ? new[] { "Fungus1_20_v02", "GG_Vengefly#door_dreamEnter" } : pick.Split(';');
            float seconds = float.TryParse(Environment.GetEnvironmentVariable("TK_VKW_SECONDS"), out var s) ? s : 150f;
            float hold = float.TryParse(Environment.GetEnvironmentVariable("TK_VKW_HOLD"), out var h0) ? h0 : 5f;
            string ai = vanilla ? "原版AI" : "新版AI";
            int allRock = 0, allPressed = 0;
            foreach (string entry in rooms.Where(r => !string.IsNullOrWhiteSpace(r)))
            {
                string room = entry.Split('#')[0];
                string gate = entry.Contains("#") ? entry.Split('#')[1] : EntryGateOf(room);
                yield return GoTo(room, gate);
                HealthManager hm = null;
                float until = Time.realtimeSinceStartup + 40f;
                while (hm == null && Time.realtimeSinceStartup < until)
                {
                    hm = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None).FirstOrDefault(x => x != null && !x.isDead && x.gameObject.activeInHierarchy && EnemyStats.Of(x)?.Record == "BigBuzzer");
                    if (hm == null) yield return new WaitForSeconds(0.2f);
                }
                if (hm == null) { Plugin.Log.LogInfo($"[AutoTest] VkWall {room}：没有反击蝇之王"); continue; }
                string scene = hm.gameObject.scene.name;
                var hero = HeroController.instance;
                var hbody = hero.GetComponent<Rigidbody2D>();
                var region = World.TerrainSystem.Find(scene) ?? World.TerrainSystem.Find(GameManager.instance.sceneName);
                if (region == null || region.Root == null) { Plugin.Log.LogInfo($"[AutoTest] VkWall {room}：没有网格"); continue; }
                const float cell = World.CellGrid.CellSize;
                var roomRect = new Rect(region.Root.position, new Vector2(region.Grid.Width, region.Grid.Height) * cell);
                bool Rock(Vector2 p)
                {
                    var c = World.CellGrid.WorldToCell(p);
                    var r = World.TerraWorld.RegionAt(c.x, c.y);
                    return r != null && r.Grid.Get(c.x - r.CellOffset.x, c.y - r.CellOffset.y) == World.CellKind.Original;
                }
                // Woken (the first blow on him hanging), then his kit's arena.
                float wakeUntil = Time.time + 8f;
                var ctl = hm.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Big Buzzer");
                while (ctl != null && ctl.ActiveStateName == "Hanging" && Time.time < wakeUntil)
                {
                    if (!BossWake.Rouse(hm))
                    {
                        hm.Hit(new HitInstance { Source = hero.gameObject, AttackType = AttackTypes.Generic, DamageDealt = 1, Direction = 0f, Multiplier = 1f, MagnitudeMultiplier = 0f });
                        FSMUtility.SendEventToGameObject(hm.gameObject, "TAKE DAMAGE");
                    }
                    yield return new WaitForSeconds(1f);
                }
                yield return new WaitForSeconds(3f);
                var kit = vanilla ? null : KitOf("BigBuzzer");
                Rect arena = kit != null ? kit.Arena : roomRect;
                Plugin.Log.LogInfo($"[AutoTest] VkWall {room}（{ai}）{scene}：房间 {roomRect.xMin:F1}~{roomRect.xMax:F1} × {roomRect.yMin:F1}~{roomRect.yMax:F1}，场地 {arena.xMin:F1}~{arena.xMax:F1} × {arena.yMin:F1}~{arena.yMax:F1}，状态 {ctl?.ActiveStateName}，碰撞体 {string.Join("，", hm.GetComponentsInChildren<Collider2D>(true).Select(c => $"{c.gameObject.name}/{c.GetType().Name}{(c.enabled ? "" : "(关)")}{(c.isTrigger ? "(触发)" : "")} {c.bounds.size.x:F1}x{c.bounds.size.y:F1} 层{c.gameObject.layer}"))}，刚体 {hm.GetComponent<Rigidbody2D>()?.bodyType} 重力 {hm.GetComponent<Rigidbody2D>()?.gravityScale} 碰撞检测 {hm.GetComponent<Rigidbody2D>()?.collisionDetectionMode}");
                // The room's grid, a character a tile (2x2 cells: rock if any is), top row first.
                var map = new System.Text.StringBuilder();
                for (float y = roomRect.yMax - 1f; y >= roomRect.yMin; y -= 1f)
                {
                    map.Append($"\n  {y,5:F0} ");
                    for (float x = roomRect.xMin; x < roomRect.xMax; x += 1f)
                    {
                        bool corner = (Mathf.Abs(x - arena.xMin) < 0.5f || Mathf.Abs(x - arena.xMax) < 0.5f) && (Mathf.Abs(y - arena.yMin) < 0.5f || Mathf.Abs(y - arena.yMax) < 0.5f);
                        bool rock = Rock(new Vector2(x + 0.25f, y + 0.25f)) || Rock(new Vector2(x + 0.75f, y + 0.25f)) || Rock(new Vector2(x + 0.25f, y + 0.75f)) || Rock(new Vector2(x + 0.75f, y + 0.75f));
                        map.Append(corner ? 'A' : rock ? '#' : '.');
                    }
                }
                Plugin.Log.LogInfo($"[AutoTest] VkWall {room} 网格（x 从 {roomRect.xMin:F0} 起每格 1）：{map}");
                // Every floor of the arena (the room where it is all one): open by the grid two tiles high, the room's own
                // ground right under it by its colliders (Godhome's halls stand on colliders the grid only partly holds), a
                // spot every 4 tiles.
                var spots = new List<Vector2>();
                var span = Rect.MinMaxRect(Mathf.Max(arena.xMin, roomRect.xMin), Mathf.Max(arena.yMin, roomRect.yMin), Mathf.Min(arena.xMax, roomRect.xMax), Mathf.Min(arena.yMax, roomRect.yMax));
                for (float x = span.xMin + 2f; x < span.xMax - 2f; x += 4f)
                    for (float y = span.yMax - 3f; y > span.yMin; y -= 0.5f)
                    {
                        if (Rock(new Vector2(x, y + 0.25f)) || Rock(new Vector2(x, y + 1.25f)) || Rock(new Vector2(x, y + 2.25f))
                            || Rock(new Vector2(x - 0.6f, y + 0.75f)) || Rock(new Vector2(x + 0.6f, y + 0.75f))) continue;
                        var under = EnemyAi.Hostiles.NativeRay(new Vector2(x, y + 0.5f), Vector2.down, 0.75f);
                        if (under.collider == null || under.point.y < y - 0.2f) continue;
                        spots.Add(new Vector2(x, under.point.y + 1.2f));
                        y -= 2f;
                    }
                var rng = new System.Random(1029);
                spots = spots.OrderBy(_ => rng.Next()).ToList();
                // A probe (TK_VKW_PROBE "vx,vy,px,py"): he put at (vx,vy) each round, the player kept at (px,py), and the
                // room's colliders round his spot listed.
                var probe = (Environment.GetEnvironmentVariable("TK_VKW_PROBE") ?? "").Split(',').Select(v => float.TryParse(v, out var f) ? f : float.NaN).ToArray();
                bool probing = probe.Length == 4 && probe.All(v => !float.IsNaN(v));
                if (probing)
                {
                    spots = new List<Vector2> { new Vector2(probe[2], probe[3]) };
                    var near = Physics2D.OverlapBoxAll(new Vector2(probe[0], probe[1]), new Vector2(8f, 6f), 0f, EnemyAi.Hostiles.TerrainMask);
                    foreach (var c in near)
                    {
                        string pts = c is EdgeCollider2D e ? string.Join(" ", e.points.Select(q => (Vector2)c.transform.TransformPoint(q)).Where(q => Mathf.Abs(q.x - probe[0]) < 6f && Mathf.Abs(q.y - probe[1]) < 5f).Select(q => $"({q.x:F2},{q.y:F2})")) : "";
                        Plugin.Log.LogInfo($"[AutoTest] VkWall 探测 碰撞体 {c.name}/{c.GetType().Name} 层{c.gameObject.layer} 触发 {c.isTrigger} 效应器 {c.usedByEffector} 建 {World.ChunkColliders.IsBuilt(c)} {c.bounds.min:F2}~{c.bounds.max:F2} {pts}");
                    }
                    var cells = new System.Text.StringBuilder();
                    for (float y = probe[1] + 3f; y >= probe[1] - 3f; y -= 0.5f)
                    {
                        cells.Append($"\n    y {y,5:F1} ");
                        for (float x = probe[0] - 5f; x <= probe[0] + 5f; x += 0.5f) cells.Append(Rock(new Vector2(x + 0.01f, y + 0.01f)) ? '#' : '.');
                    }
                    Plugin.Log.LogInfo($"[AutoTest] VkWall 探测 格子 x {probe[0] - 5f:F1} 起每格 0.5：{cells}");
                }
                Plugin.Log.LogInfo($"[AutoTest] VkWall {room} 站位 {spots.Count} 个：{string.Join(" ", spots.Select(p => $"({p.x:F0},{p.y:F1})"))}");
                int unstuck0 = BossAi.DevUnstuck;
                int rockFrames = 0, frames = 0, episodes = 0, pressed = 0, shots = 0;
                float rockSince = -1f, longest = 0f, windowStart = Time.time;
                int windowFrames = 0, windowTouch = 0;
                Vector2 windowAt = hm.transform.position;
                var windowStates = new HashSet<string>();
                var trail = new List<string>();
                int spotIndex = 0, blind = 0;
                Vector2 spot = Vector2.zero;
                float nextSpot = 0f, t0 = Time.time, spotStart = 0f, sawAt = -1f;
                // How long after the player moves he has a clear line to him (none of the room's rock between).
                var sightTimes = new List<float>();
                while (Time.time - t0 < seconds && hm != null && !hm.isDead)
                {
                    yield return null;
                    if (spots.Count > 0 && Time.time >= nextSpot)
                    {
                        if (spotIndex > 0)
                        {
                            if (sawAt < 0f) { blind++; Plugin.Log.LogInfo($"[AutoTest] VkWall {room} 玩家在 ({spot.x:F0},{spot.y:F1}) {hold:F0} 秒里它一直隔着岩石，它在 ({hm.transform.position.x:F1},{hm.transform.position.y:F1})，状态 {ctl?.ActiveStateName}，{VengeflyKingKit.DevWay}，挡住 {VengeflyKingKit.DevBlock}"); }
                            else sightTimes.Add(sawAt);
                        }
                        nextSpot = Time.time + hold;
                        spot = spots[spotIndex++ % spots.Count];
                        if (probing)
                        {
                            hm.transform.position = new Vector3(probe[0], probe[1], hm.transform.position.z);
                            var hrb = hm.GetComponent<Rigidbody2D>();
                            if (hrb != null) hrb.linearVelocity = Vector2.zero;
                        }
                        spotStart = Time.time;
                        sawAt = -1f;
                        VengeflyKingKit.DevWay = "";
                    }
                    if (spots.Count > 0)
                    {
                        hero.transform.position = new Vector3(spot.x, spot.y, hero.transform.position.z);
                        if (hbody != null) hbody.linearVelocity = Vector2.zero;
                    }
                    mod.Stats.FullHeal();
                    if (EnemyStats.Of(hm) is EnemyStats.Info info && hm.hp < info.MaxHp * 0.3f) hm.hp = Mathf.RoundToInt(info.MaxHp * 0.6f);
                    Collider2D body = null;
                    foreach (var c in hm.GetComponentsInChildren<Collider2D>())
                        if (c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger && (body == null || c.bounds.size.sqrMagnitude > body.bounds.size.sqrMagnitude)) body = c;
                    if (body == null) continue;
                    var b = body.bounds;
                    var rb = hm.GetComponent<Rigidbody2D>();
                    string state = ctl != null ? ctl.ActiveStateName : "?";
                    float t = Time.time - t0;
                    var line = (Vector2)hero.transform.position - (Vector2)b.center;
                    if (sawAt < 0f && EnemyAi.Hostiles.NativeRay(b.center, line.normalized, line.magnitude).collider == null) sawAt = Time.time - spotStart;
                    frames++;
                    trail.Add($"{t:F2}s {state} 中 ({b.center.x:F2},{b.center.y:F2}) 速度 {(rb != null ? rb.linearVelocity : Vector2.zero):F1} 玩家 ({spot.x:F0},{spot.y:F1})");
                    if (trail.Count > 14) trail.RemoveAt(0);
                    bool inRock = Rock(b.center);
                    if (inRock)
                    {
                        rockFrames++;
                        if (rockSince < 0f)
                        {
                            rockSince = Time.time;
                            episodes++;
                            int corners = new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) }.Count(o => Rock((Vector2)b.center + Vector2.Scale(o, b.extents) * 0.9f));
                            Plugin.Log.LogInfo($"[AutoTest] VkWall {room} 进了岩壁 #{episodes}（{state}，角 {corners}/4 在岩里），此前几帧：\n    {string.Join("\n    ", trail)}");
                            if (shots < 6) { shots++; Shot(scene, $"vkwall_{room}_{(vanilla ? "v" : "n")}_{t:000.0}"); }
                        }
                    }
                    else if (rockSince >= 0f)
                    {
                        float len = Time.time - rockSince;
                        longest = Mathf.Max(longest, len);
                        Plugin.Log.LogInfo($"[AutoTest] VkWall {room} 出了岩壁 #{episodes}：在里面 {len:F2} 秒，出来时 {state} ({b.center.x:F1},{b.center.y:F1})，脱困 {BossAi.DevUnstuck - unstuck0}");
                        rockSince = -1f;
                    }
                    windowFrames++;
                    if (rb != null && rb.IsTouchingLayers(1 << 8)) windowTouch++;
                    windowStates.Add(state);
                    if (Time.time - windowStart >= 1.5f)
                    {
                        float moved = Vector2.Distance(windowAt, b.center);
                        if (moved < 0.5f && windowTouch >= windowFrames * 0.6f && !windowStates.Contains("Hanging"))
                        {
                            pressed++;
                            Plugin.Log.LogInfo($"[AutoTest] VkWall {room} 贴墙不动 {t:F1}s：1.5 秒移动 {moved:F2}，贴地形 {windowTouch}/{windowFrames} 帧，状态 {string.Join("、", windowStates)}，在 ({b.center.x:F1},{b.center.y:F1})，玩家 ({spot.x:F0},{spot.y:F1})，岩里 {inRock}，{VengeflyKingKit.DevWay}，挡住 {VengeflyKingKit.DevBlock}");
                            if (shots < 6) { shots++; Shot(scene, $"vkpress_{room}_{(vanilla ? "v" : "n")}_{t:000.0}"); }
                        }
                        windowStart = Time.time;
                        windowAt = b.center;
                        windowFrames = windowTouch = 0;
                        windowStates.Clear();
                    }
                }
                if (rockSince >= 0f) longest = Mathf.Max(longest, Time.time - rockSince);
                allRock += episodes;
                allPressed += pressed;
                Plugin.Log.LogInfo($"[AutoTest] VkWall {room}（{ai}）：{seconds:F0} 秒，进岩壁 {episodes} 次（{rockFrames}/{frames} 帧，最长 {longest:F1} 秒），贴墙不动 {pressed} 次，脱困 {BossAi.DevUnstuck - unstuck0}，玩家换位后看到玩家 {sightTimes.Count} 次（平均 {(sightTimes.Count > 0 ? sightTimes.Average() : 0f):F1} 秒，最长 {(sightTimes.Count > 0 ? sightTimes.Max() : 0f):F1} 秒），一直隔着岩石 {blind} 次");
                if (hm != null) hm.hp = 1;
                yield return new WaitForSeconds(1f);
            }
            Plugin.Log.LogInfo($"[AutoTest] VkWall 合计（{ai}）进岩壁 {allRock} 次，贴墙不动 {allPressed} 次");
        }
    }
}
