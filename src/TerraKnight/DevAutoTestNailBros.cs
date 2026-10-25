using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HutongGames.PlayMaker;
using TerraKnight.EnemyAi;
using TerraKnight.EnemyAi.Bosses;
using TerraKnight.Items;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "nailbros"): Godhome's Nailmasters (the user's 10-25: "梦境骨钉大师的左边有空气墙", the brothers dashing
    /// into the wall and stuck; "跳到玩家脸上往下砍根本砍不到玩家"). In GG_Nailmasters by the book's rematch: what stands
    /// along the arena's left (the grid's cells, every solid collider there, what stops the player's body and a brother's
    /// going left), then a fight watched: each brother's place (inside rock, past the room's walls), and each strike down
    /// (where he stops over the player, whether it hurt), first with the player standing, then held up in the air.
    /// TK_NAIL_SECONDS: each half of the watch (20 s).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator NailBrosTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            mod.ApplyCreative(false);
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            World.EnemyAiMode.Current = Environment.GetEnvironmentVariable("TK_NAIL_VANILLA") == "1" ? World.EnemyAiKind.Vanilla : World.EnemyAiKind.New;
            float half = float.TryParse(Environment.GetEnvironmentVariable("TK_NAIL_SECONDS"), out var s) ? s : 20f;
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 骨钉大师 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            BossArena.Fight("NailBros", true);
            float until = Time.realtimeSinceStartup + 60f;
            NailBrosKit[] kits = null;
            while (Time.realtimeSinceStartup < until)
            {
                if (!GameManager.instance.IsInSceneTransition)
                {
                    kits = Object.FindObjectsByType<NailBrosKit>(FindObjectsSortMode.None).Where(k => k.Hm != null && k.gameObject.activeInHierarchy).ToArray();
                    if (kits.Length >= 2 || World.EnemyAiMode.Current == World.EnemyAiKind.Vanilla && GameManager.instance.sceneName == "GG_Nailmasters") break;
                }
                yield return null;
            }
            yield return new WaitForSeconds(1.5f);
            var hero = HeroController.instance;
            var hcol = hero.GetComponent<Collider2D>();
            var hbody = hero.GetComponent<Rigidbody2D>();
            string scene = GameManager.instance.sceneName;
            var brothers = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                .Where(h => h != null && (h.name.StartsWith("Oro") || h.name.StartsWith("Mato"))).ToArray();
            Plugin.Log.LogInfo($"[AutoTest] 骨钉大师 场景 {scene}，兄弟 {string.Join("、", brothers.Select(b => $"{b.name}@({b.transform.position.x:F1},{b.transform.position.y:F1})"))}，" +
                $"新版招式 {kits?.Length ?? 0} 套{(kits != null && kits.Length > 0 ? $"，场地 {kits[0].Arena}" : "")}");

            // The arena's left: from the middle of the floor, each height swept to the room's left edge.
            var region = World.TerrainSystem.Find(scene);
            // Oro's (Mato waits off the arena, up at its left, until Oro's second phase).
            var oroKit = kits?.FirstOrDefault(k => k.name.StartsWith("Oro")) ?? kits?.FirstOrDefault();
            Rect arena = oroKit != null ? oroKit.Arena : new Rect(33f, 0f, 33f, 20f);
            foreach (var k in kits ?? new NailBrosKit[0]) Plugin.Log.LogInfo($"[AutoTest] 骨钉大师 {k.name} 场地 {k.Arena} 状态 {k.StateNow} 交战 {k.Engaged}");
            float mid = arena.center.x;
            var groundHit = Hostiles.NativeRay(new Vector2(mid, arena.center.y), Vector2.down, 30f);
            float floor = groundHit.collider != null ? groundHit.point.y : arena.yMin;
            int terrainMask = 1 << World.ChunkColliders.TerrainLayer;
            float wallL = float.MinValue, wallR = float.MaxValue;
            {
                var l = Hostiles.NativeRay(new Vector2(mid, floor + 1f), Vector2.left, 40f);
                var r = Hostiles.NativeRay(new Vector2(mid, floor + 1f), Vector2.right, 40f);
                if (l.collider != null) wallL = l.point.x;
                if (r.collider != null) wallR = r.point.x;
            }
            Plugin.Log.LogInfo($"[AutoTest] 骨钉大师 原版墙 左 {wallL:F2} 右 {wallR:F2}");
            var floorSlides = new List<(string label, float end, float half)>();
            Plugin.Log.LogInfo($"[AutoTest] 骨钉大师 地面 y {floor:F2}（{groundHit.collider?.name}），场地 x {arena.xMin:F1}~{arena.xMax:F1} y {arena.yMin:F1}~{arena.yMax:F1}，房间 {(region != null ? $"{region.Root.position} {region.Grid.Width}x{region.Grid.Height}" : "无")}");
            foreach (float up in new[] { 0.6f, 1.5f, 3f, 6f, 10f })
            {
                float y = floor + up;
                string grid = null, phys = null, native = null;
                for (float x = mid; x > arena.xMin - 15f; x -= 0.25f)
                {
                    var p = new Vector2(x, y);
                    var cell = World.CellGrid.WorldToCell(p);
                    if (grid == null && World.TerraWorld.IsSolid(cell.x, cell.y)) grid = $"{x:F2}（格 {cell.x},{cell.y} {World.TerraWorld.Get(cell.x, cell.y)}）";
                    if (native == null && Hostiles.NativeSolidAt(p)) native = $"{x:F2}";
                    if (phys == null)
                        foreach (var c in Physics2D.OverlapPointAll(p))
                            if (c != null && !c.isTrigger && c.attachedRigidbody?.GetComponent<HealthManager>() == null && c.gameObject != hero.gameObject)
                            {
                                phys = $"{x:F2}（{NailPath(c.transform)} 层 {c.gameObject.layer} {c.GetType().Name} {c.bounds.min.x:F1}..{c.bounds.max.x:F1}×{c.bounds.min.y:F1}..{c.bounds.max.y:F1}）";
                                break;
                            }
                }
                Plugin.Log.LogInfo($"[AutoTest] 骨钉大师 左侧 高 {up:F1}（y {y:F2}）：格子实心 {grid ?? "无"}；原版地形 {native ?? "无"}；碰撞体 {phys ?? "无"}");
            }
            int heroLayerMask = Physics2D.GetLayerCollisionMask(hero.gameObject.layer);
            for (float up = 0.5f; up <= 30f; up += 2.5f)
            {
                float y = floor + up;
                float gridX = float.NaN, nativeX = float.NaN, frameX = float.NaN;
                for (float x = mid; x > mid - 30f; x -= 0.1f)
                {
                    var p = new Vector2(x, y);
                    var cell = World.CellGrid.WorldToCell(p);
                    if (float.IsNaN(gridX) && World.TerraWorld.IsSolid(cell.x, cell.y)) gridX = x;
                    if (float.IsNaN(nativeX) && Hostiles.NativeSolidAt(p)) nativeX = x;
                    if (float.IsNaN(frameX))
                        foreach (var c in Physics2D.OverlapPointAll(p, 1 << 13))
                            if (c != null && !c.isTrigger) { frameX = x; break; }
                }
                Plugin.Log.LogInfo($"[AutoTest] 骨钉大师 左墙 y {y:F1}：格子 {gridX:F1} 原版 {nativeX:F1} 空气墙(层13) {frameX:F1}");
            }
            // Every solid collider on the left half within the fight's heights.
            var lefts = new List<string>();
            foreach (var c in Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
            {
                if (c == null || c.isTrigger || !c.enabled || !c.gameObject.activeInHierarchy) continue;
                var b = c.bounds;
                if (b.max.x > mid || b.max.x < arena.xMin - 15f || b.max.y < floor - 1f || b.min.y > floor + 14f) continue;
                if (c.attachedRigidbody != null && c.attachedRigidbody.bodyType == RigidbodyType2D.Dynamic) continue;
                lefts.Add($"{NailPath(c.transform)}[{c.gameObject.scene.name}] 层{c.gameObject.layer} {c.GetType().Name} x {b.min.x:F1}..{b.max.x:F1} y {b.min.y:F1}..{b.max.y:F1}");
            }
            Plugin.Log.LogInfo($"[AutoTest] 骨钉大师 左半边的实心碰撞体 {lefts.Count} 个：{string.Join("；", lefts.Take(40))}");
            foreach (var cl in Object.FindObjectsByType<CameraLockArea>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var bc = cl.GetComponent<Collider2D>();
                Plugin.Log.LogInfo($"[AutoTest] 骨钉大师 镜头锁定 {NailPath(cl.transform)} 触发区 {(bc != null ? bc.bounds.ToString() : "-")} 镜头 x {cl.cameraXMin:F2}..{cl.cameraXMax:F2} y {cl.cameraYMin:F2}..{cl.cameraYMax:F2} 开 {cl.isActiveAndEnabled}");
            }
            // The layers that matter and who meets whom; the Godhome room's "coward" walls' shape.
            int bossLayer = brothers.Length > 0 ? brothers[0].gameObject.layer : 11;
            Plugin.Log.LogInfo($"[AutoTest] 骨钉大师 层：玩家 {hero.gameObject.layer}={LayerMask.LayerToName(hero.gameObject.layer)} 兄弟 {bossLayer}={LayerMask.LayerToName(bossLayer)} 13={LayerMask.LayerToName(13)}；" +
                $"玩家碰 13 {!Physics2D.GetIgnoreLayerCollision(hero.gameObject.layer, 13)}，兄弟碰 13 {!Physics2D.GetIgnoreLayerCollision(bossLayer, 13)}，兄弟碰 8 {!Physics2D.GetIgnoreLayerCollision(bossLayer, 8)}");
            foreach (var c in Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
            {
                if (c == null || !c.transform.name.Contains("Roof") && (c.transform.parent == null || !c.transform.parent.name.Contains("coward"))) continue;
                string shape = c is PolygonCollider2D pc ? string.Join(" | ", Enumerable.Range(0, pc.pathCount).Select(i => string.Join(" ", pc.GetPath(i).Select(v => { var w = c.transform.TransformPoint(v); return $"({w.x:F1},{w.y:F1})"; }))))
                    : c is EdgeCollider2D ec ? string.Join(" ", ec.points.Select(v => { var w = c.transform.TransformPoint(v); return $"({w.x:F1},{w.y:F1})"; })) : c.bounds.ToString();
                Plugin.Log.LogInfo($"[AutoTest] 骨钉大师 {NailPath(c.transform)} 层 {c.gameObject.layer} 触发器 {c.isTrigger} 开 {c.enabled && c.gameObject.activeInHierarchy}：{shape}");
            }
            foreach (float y in new[] { 21f, 23f, 26f, 30f })
            {
                var at = new Vector2(mid, y);
                var there = Physics2D.OverlapPointAll(at).Where(c => c != null && !c.isTrigger)
                    .Select(c => $"{NailPath(c.transform)} 层{c.gameObject.layer} {c.GetType().Name} {c.bounds.min.x:F1}..{c.bounds.max.x:F1}×{c.bounds.min.y:F1}..{c.bounds.max.y:F1}");
                Plugin.Log.LogInfo($"[AutoTest] 骨钉大师 ({at.x:F1},{y})处的实心碰撞体：{string.Join("；", there)}");
            }
            // The player's body and a brother's cast left at several heights: what solid (no triggers) stops each.
            void CastLeft(string who, Collider2D body, int layer)
            {
                var filter = new ContactFilter2D { useTriggers = false, useLayerMask = true, layerMask = Physics2D.GetLayerCollisionMask(layer) };
                var hits = new RaycastHit2D[8];
                foreach (float up in new[] { 0.1f, 3f, 6f, 10f, 14f })
                {
                    var from = new Vector2(mid, floor + body.bounds.extents.y + up);
                    int n = Physics2D.BoxCast(from, (Vector2)body.bounds.size * 0.95f, 0f, Vector2.left, filter, hits, 40f);
                    RaycastHit2D first = default;
                    for (int i = 0; i < n; i++)
                    {
                        var h = hits[i];
                        if (h.collider == null || h.collider.attachedRigidbody == body.attachedRigidbody || h.collider.GetComponentInParent<HealthManager>() != null || h.collider.GetComponentInParent<HeroController>() != null) continue;
                        if (first.collider == null || h.distance < first.distance) first = h;
                    }
                    Plugin.Log.LogInfo($"[AutoTest] 骨钉大师 {who}往左 高 {up:F1}：{(first.collider != null ? $"在 x {first.point.x:F2} 被 {NailPath(first.collider.transform)}（层 {first.collider.gameObject.layer} {first.collider.GetType().Name}）挡住" : "40 格内没挡")}");
                }
            }
            if (hcol != null)
            {
                CastLeft("玩家", hcol, hero.gameObject.layer);
                var from = new Vector2(mid, floor + hcol.bounds.extents.y + 0.1f);
                hero.transform.position = new Vector3(from.x, from.y, hero.transform.position.z);
                Shot(scene, "nail_mid");
                yield return null;
                foreach (float up in new[] { 1f, 8f, 14f, 20f })
                {
                    hero.transform.position = new Vector3(arena.xMin + 1.5f, floor + up, hero.transform.position.z);
                    if (hbody != null) hbody.linearVelocity = Vector2.zero;
                    yield return new WaitForSeconds(0.4f);
                    Shot(scene, $"nail_left_{up:00}");
                    Plugin.Log.LogInfo($"[AutoTest] 骨钉大师 截图左侧 高 {up}：玩家放在 ({arena.xMin + 1.5f:F1},{floor + up:F1})，0.4 秒后在 ({hero.transform.position.x:F2},{hero.transform.position.y:F2})");
                }
            }
            if (brothers.Length > 0 && brothers[0].GetComponent<Collider2D>() is Collider2D bb) CastLeft("兄弟", bb, bossLayer);
            // A box the size of a brother's body pushed along the floor each way (a seam in the floor catches it as a wall).
            if (brothers.Length > 0 && brothers[0].GetComponent<BoxCollider2D>() is BoxCollider2D bbox)
            {
                foreach (var (startX, vx, label) in new[] { (mid, -12f, "往左"), (wallL + 2f, 12f, "往右") })
                {
                    var probe = new GameObject("Nail Floor Probe") { layer = bossLayer };
                    var box = probe.AddComponent<BoxCollider2D>();
                    box.size = Vector2.Scale(bbox.size, new Vector2(Mathf.Abs(bbox.transform.lossyScale.x), Mathf.Abs(bbox.transform.lossyScale.y)));
                    var prb = probe.AddComponent<Rigidbody2D>();
                    prb.gravityScale = 3f;
                    prb.constraints = RigidbodyConstraints2D.FreezeRotation;
                    probe.transform.position = new Vector3(startX, floor + box.size.y / 2f + 0.02f, 0f);
                    float lowest = float.MaxValue, highest = float.MinValue;
                    for (float t = 0f; t < 3.2f; t += Time.fixedDeltaTime)
                    {
                        prb.linearVelocity = new Vector2(vx, prb.linearVelocity.y);
                        yield return new WaitForFixedUpdate();
                        lowest = Mathf.Min(lowest, prb.position.x);
                        highest = Mathf.Max(highest, prb.position.x);
                    }
                    float end = prb.position.x;
                    Plugin.Log.LogInfo($"[AutoTest] 骨钉大师 地面滑块{label}：从 {startX:F1} 到 {end:F2}（墙 {wallL:F1}~{wallR:F1}，半宽 {box.size.x / 2f:F2}）");
                    floorSlides.Add((label, end, box.size.x / 2f));
                    Object.Destroy(probe);
                }
            }
            if (kits == null || kits.Length < 2) { Plugin.Log.LogInfo($"[AutoTest] 骨钉大师 合计 {ok}/{all} 没有新版招式"); yield break; }

            // The fight: standing, then held up in the air.
            var stab = new Dictionary<NailBrosKit, (float at, Vector2 top, bool hurt, string phase)>();
            int stabs = 0, stabHits = 0, stabsAir = 0, stabHitsAir = 0, stabsOpen = 0, stabsOpenAir = 0;
            int inRock = 0, pastWall = 0, frames = 0;
            float minX = float.MaxValue, maxX = float.MinValue;
            // Oro struck down once (his Death Type calls Mato in, then he gets up with his "P2 HP"): both fight.
            var oro = brothers.FirstOrDefault(b => b.name.StartsWith("Oro"));
            // He wakes first (struck down still resting, his body off, his death's launch never lands).
            for (float w = 0f; w < 25f && oroKit != null && !(oroKit.Engaged && oroKit.StateNow == "Idle"); w += Time.deltaTime) yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 骨钉大师 奥罗醒了：{oroKit?.StateNow} 交战 {oroKit?.Engaged}");
            if (oro != null)
            {
                var ofsm = oro.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "nailmaster");
                int p2 = ofsm != null && ofsm.FsmVariables.GetFsmInt("P2 HP") is var pv && pv != null ? pv.Value : 1000;
                oro.hp = 1;
                oro.Hit(new HitInstance { Source = hero.gameObject, AttackType = AttackTypes.Generic, DamageDealt = 10, Direction = 0f, Multiplier = 1f, MagnitudeMultiplier = 0f });
                // Whatever the hit did (the mod's own phase rules may hold his life): his own script's down.
                if (ofsm != null && ofsm.ActiveStateName != "Death Start") ofsm.SendEvent("ZERO HP");
                Plugin.Log.LogInfo($"[AutoTest] 骨钉大师 奥罗打倒一次，叫马托入场（P2 HP {p2}）");
            }
            var waiting = new System.Text.RegularExpressions.Regex("^(Init|Entry Wait|Rest|Wake|Dormant|P2 HP Adjust)$");
            var downed = new System.Text.RegularExpressions.Regex("Death|Explode|Getup|Recover|Reactivate|Call Mato|Defeated|Entry|Pos |Look|Roar");
            var lastState = new Dictionary<NailBrosKit, string>();
            var standAt = new Vector2(mid, floor + (hcol != null ? hcol.bounds.extents.y : 0.65f) + 0.05f);
            float t0 = Time.time, lastHurt = Player.TerraHurt.LastHurtAt;
            var stuckSince = new Dictionary<NailBrosKit, float>();
            var samples = new List<string>();
            int lastPhase = -1, shotsTaken = 0, dashTraced = 0;
            var lastDashX = new Dictionary<NailBrosKit, float>();
            bool trace = Environment.GetEnvironmentVariable("TK_NAIL_TRACE") == "1";
            var byPhase = new Dictionary<string, (int n, int hit, int open)>();
            string[] phaseNames = { "地面", "低空", "高空", "贴左墙", "来回飞" };
            // A brother's own hurting pieces (his body, his slashes) on the player now.
            bool HitBy(NailBrosKit k)
            {
                if (hcol == null) return false;
                var hb = hcol.bounds;
                hb.Expand(0.15f);
                foreach (var dh in k.GetComponentsInChildren<DamageHero>())
                    foreach (var c in dh.GetComponents<Collider2D>())
                        if (c != null && c.enabled && dh.gameObject.activeInHierarchy && c.bounds.Intersects(hb)) return true;
                return false;
            }
            while (Time.time - t0 < 5f * half)
            {
                int phase = Mathf.Min(4, (int)((Time.time - t0) / half));
                bool air = phase > 0;
                // On the floor; held 4 and 11 up in the middle; hanging on the left wall 12 up (the user's hook spot); flying
                // across the middle 10 up, back and forth at 5 a second.
                var want = phase == 0 ? standAt : phase == 1 ? standAt + Vector2.up * 4f : phase == 2 ? standAt + Vector2.up * 11f
                    : phase == 3 ? new Vector2(wallL + (hcol != null ? hcol.bounds.extents.x : 0.3f) + 0.25f, standAt.y + 12f)
                    : new Vector2(mid + Mathf.PingPong((Time.time - t0) * 5f, 20f) - 10f, standAt.y + 10f);
                if (phase != lastPhase) { lastPhase = phase; Plugin.Log.LogInfo($"[AutoTest] 骨钉大师 阶段 {phaseNames[phase]}：玩家在 ({want.x:F1},{want.y:F1})"); }
                // Kept in the middle (the strikes fall where he is); in the air held there.
                if (air || ((Vector2)hero.transform.position - want).sqrMagnitude > 9f)
                {
                    hero.transform.position = new Vector3(want.x, want.y, hero.transform.position.z);
                    if (hbody != null) hbody.linearVelocity = Vector2.zero;
                }
                if (mod.Stats.Life < mod.Stats.LifeMax / 2) mod.Stats.FullHeal();
                bool hurtNow = Player.TerraHurt.LastHurtAt != lastHurt;
                lastHurt = Player.TerraHurt.LastHurtAt;
                frames++;
                foreach (var k in kits)
                {
                    if (k == null || k.Hm == null || k.Hm.isDead) continue;
                    if (k.Hm.hp < 300 && !downed.IsMatch(k.StateNow ?? "")) k.Hm.hp = 3000;
                    string st = k.StateNow ?? "";
                    lastState.TryGetValue(k, out var was);
                    var body = k.GetComponent<Collider2D>();
                    Vector2 me = k.Me;
                    // Dashing without going anywhere: its body's numbers and what it touches, a few frames.
                    if ((st == "Dash" || st == "Dash Combo") && dashTraced < 12 && Mathf.Abs(me.x - k.transform.position.x) < 50f)
                    {
                        var rbk = k.GetComponent<Rigidbody2D>();
                        if (rbk != null && Mathf.Abs(rbk.position.x - lastDashX.GetValueOrDefault(k, -999f)) < 0.05f)
                        {
                            dashTraced++;
                            var contacts = new ContactPoint2D[8];
                            int nc = rbk.GetContacts(contacts);
                            var fs = k.GetComponents<PlayMakerFSM>().Select(f => $"{f.FsmName}:{f.ActiveStateName}{(f.Fsm.ManualUpdate ? "(手动)" : "")}{(f.enabled ? "" : "(关)")}");
                            Plugin.Log.LogInfo($"[AutoTest] 骨钉大师 冲刺不动 {k.name} {st} x {rbk.position.x:F2} 速度 {rbk.linearVelocity} {rbk.bodyType} 模拟 {rbk.simulated} 约束 {rbk.constraints} 缩放 {k.transform.localScale.x:F2} " +
                                $"碰到 {string.Join("、", Enumerable.Range(0, nc).Select(i => $"{NailPath(contacts[i].collider.transform)}@{contacts[i].point:F1} n{contacts[i].normal:F1}"))}；状态机 {string.Join(" ", fs)}；" +
                                $"Crt Speed {k.Float("Crt Speed")?.Value:F1} X Scale {k.Float("X Scale")?.Value:F1}");
                        }
                        if (rbk != null) lastDashX[k] = rbk.position.x;
                    }
                    if (st != was && Time.time - t0 < 25f) Plugin.Log.LogInfo($"[AutoTest] 骨钉大师 状态 {Time.time - t0:F1}s {k.name} {was} -> {st} ({me.x:F1},{me.y:F1}) hp {k.Hm.hp} 死 {k.Hm.isDead} 身体 {(body != null && body.enabled)}");
                    if (waiting.IsMatch(st) || body == null || !body.enabled) { lastState[k] = st; continue; }
                    minX = Mathf.Min(minX, me.x);
                    maxX = Mathf.Max(maxX, me.x);
                    if (me.x < wallL - 0.2f || me.x > wallR + 0.2f)
                    {
                        pastWall++;
                        if (samples.Count < 12 && pastWall % 25 == 1) samples.Add($"{k.name} 在墙外 {st} ({me.x:F2},{me.y:F2})");
                    }
                    bool rock = body != null && body.enabled && Physics2D.OverlapBoxAll(body.bounds.center, (Vector2)body.bounds.size * 0.6f, 0f, terrainMask)
                        .Any(c => c != null && !c.isTrigger && c.attachedRigidbody != k.GetComponent<Rigidbody2D>());
                    if (rock)
                    {
                        inRock++;
                        if (!stuckSince.ContainsKey(k)) stuckSince[k] = Time.time;
                        else if (Time.time - stuckSince[k] > 0.5f && samples.Count < 12)
                            samples.Add($"{k.name} 卡在地形里 {Time.time - stuckSince[k]:F1}s {st} ({me.x:F2},{me.y:F2})");
                    }
                    else stuckSince.Remove(k);
                    if (st != was)
                    {
                        lastState[k] = st;
                        if (trace && (st == "Jump" || st == "Dstab Antic") && phase >= 2)
                        {
                            var rb2 = k.GetComponent<Rigidbody2D>();
                            Plugin.Log.LogInfo($"[AutoTest] 骨钉大师 跟踪 {k.name} {st}：身体中心 ({me.x:F2},{me.y:F2}) 位置 y {k.transform.position.y:F2}，Topslash Y {k.Float("Topslash Y")?.Value:F2}，Jump Y Dstab {k.Float("Jump Y Dstab")?.Value:F1}，Jump X {k.Float("Jump X")?.Value:F1}，" +
                                $"速度 {rb2?.linearVelocity}，重力 {rb2?.gravityScale:F1}，玩家中心 {k.TargetCenter:F2} 离地 {k.TargetHeight:F1}，头顶天花板 {k.CeilingAbove(k.Me + Vector2.up * 0.5f, 40f):F2}");
                        }
                        if (st == "Dstab Antic")
                        {
                            stab[k] = (Time.time, me, false, phaseNames[phase]);
                        }
                        if (st == "Dstab" && stab.TryGetValue(k, out var d0))
                        {
                            stab[k] = (d0.at, me, d0.hurt, d0.phase);
                            if (phase >= 2 && shotsTaken < 6) Shot(scene, $"nail_stab_{shotsTaken++}_{d0.phase}");
                        }
                    }
                    if (stab.TryGetValue(k, out var d))
                    {
                        if (hurtNow && !d.hurt && HitBy(k)) stab[k] = (d.at, d.top, true, d.phase);
                        bool done = st != "Dstab Antic" && st != "Dstab" && st != "Dstab Land" && Time.time - d.at > 0.3f || Time.time - d.at > 2.5f;
                        if (done)
                        {
                            d = stab[k];
                            stab.Remove(k);
                            bool open = !hero.cState.invulnerable || d.hurt;
                            if (d.phase != "地面") { stabsAir++; if (d.hurt) stabHitsAir++; if (open) stabsOpenAir++; }
                            else { stabs++; if (d.hurt) stabHits++; if (open) stabsOpen++; }
                            Plugin.Log.LogInfo($"[AutoTest] 骨钉大师 下劈 {k.name} {d.phase}：停在 ({d.top.x:F2},{d.top.y:F2})，玩家 ({hero.transform.position.x:F2},{hero.transform.position.y:F2})，横差 {d.top.x - hero.transform.position.x:F2}，高差 {d.top.y - hero.transform.position.y:F2}，{(d.hurt ? "打中" : open ? "没打中" : "玩家无敌中")}");
                            if (!byPhase.ContainsKey(d.phase)) byPhase[d.phase] = (0, 0, 0);
                            var bp = byPhase[d.phase];
                            byPhase[d.phase] = (bp.n + 1, bp.hit + (d.hurt ? 1 : 0), bp.open + (open ? 1 : 0));
                        }
                    }
                }
                yield return null;
            }
            foreach (var line in samples) Plugin.Log.LogInfo($"[AutoTest] 骨钉大师 {line}");
            foreach (var k in kits) if (k != null) Plugin.Log.LogInfo($"[AutoTest] 骨钉大师 {k.name} 战斗中的场地 x {k.Arena.xMin:F1}~{k.Arena.xMax:F1} y {k.Arena.yMin:F1}~{k.Arena.yMax:F1}（原版墙 {wallL:F1}~{wallR:F1}）");
            Check("场地覆盖两墙之间", kits.All(k => k == null || k.Arena.xMin <= wallL + 0.5f && k.Arena.xMax >= wallR - 0.5f),
                string.Join("；", kits.Where(k => k != null).Select(k => $"{k.name} {k.Arena.xMin:F1}~{k.Arena.xMax:F1}")));
            foreach (var (label, end, halfW) in floorSlides)
                Check($"地面滑块{label}一路到墙", label == "往左" ? end <= wallL + halfW + 0.3f : end >= wallR - halfW - 0.3f, $"停在 {end:F2}（墙 {wallL:F2}~{wallR:F2}，半宽 {halfW:F2}）");
            Check("兄弟不进墙里、不过墙", pastWall == 0 && samples.Count == 0, $"活动范围 x {minX:F2}~{maxX:F2}（原版墙 {wallL:F2}~{wallR:F2}），过墙 {pastWall} 帧，碰着地形 {inRock}/{frames} 帧，卡住 {samples.Count} 次");
            Check("站着时下劈打得中", stabs == 0 || stabHits * 2 >= stabsOpen, $"{stabHits}/{stabs} 下（玩家没在无敌里的 {stabsOpen} 下）");
            Check("空中时下劈打得中", stabsAir == 0 || stabHitsAir * 2 >= stabsOpenAir, $"{stabHitsAir}/{stabsAir} 下（玩家没在无敌里的 {stabsOpenAir} 下）");
            foreach (var kv in byPhase) Plugin.Log.LogInfo($"[AutoTest] 骨钉大师 下劈 {kv.Key}：{kv.Value.hit}/{kv.Value.n} 下打中（玩家没在无敌里的 {kv.Value.open} 下）");
            Plugin.Log.LogInfo($"[AutoTest] 骨钉大师 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }

        static string NailPath(Transform t)
        {
            var names = new List<string>();
            for (int i = 0; t != null && i < 4; i++, t = t.parent) names.Add(t.name);
            names.Reverse();
            return string.Join("/", names);
        }
    }
}
