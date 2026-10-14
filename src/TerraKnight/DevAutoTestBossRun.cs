using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TerraKnight.Items;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "bossrun"): every boss and elite out in the world fought as a player would: into
    /// its room by a gate, a walk toward it to wake it, then struck through the mod's own hits (its
    /// frames after each hit apply) whenever the player could swing, in its armor too, until the game
    /// itself has it beaten (the book's kill, wherever it came from: HealthManager.Die or the boss's
    /// own FSM). Then: how long it took, the drops, a health bar left over the body, control back
    /// afterwards, the errors logged.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator BossRunTest(string[] only)
        {
            var list = new List<(string Record, string Scene)> { ("Hornet", "Fungus1_04_boss") };
            foreach (var part in bossParts) list.AddRange(part);
            // "bossrun_A+B": only those (by record; Hornet2 for the second Hornet).
            if (only != null)
                list = list.FindAll(e => System.Array.IndexOf(only, e.Scene == "Deepnest_East_Hornet_boss" ? "Hornet2" : e.Record) >= 0);
            var errors = new List<string>();
            void OnLog(string msg, string stack, LogType type)
            {
                if ((type == LogType.Exception || (type == LogType.Error && msg.Contains("Exception")) || msg.StartsWith("[Error")) && errors.Count < 20) errors.Add(msg.Length > 160 ? msg.Substring(0, 160) : msg);
            }
            Application.logMessageReceived += OnLog;
            RoarRelease.DevTrace = true;
            int good = 0;
            var bad = new StringBuilder();
            const int terrain = 1 << World.ChunkColliders.TerrainLayer;
            try
            {
                foreach (var (record, scene) in list)
                {
                    var gm = GameManager.instance;
                    string room = scene.EndsWith("_boss") ? scene.Substring(0, scene.Length - 5) : scene;
                    int kills0 = KillsOf(record), drops0 = EnemyDrops.DevDropped;
                    PlayerData.instance.isInvincible = true;
                    // In by the arena's own gate (the Watcher Knights wake only for one who comes down from the
                    // Watcher's room), with what the fight needs (the Traitor Lord's arena is there for a
                    // player who can get out of it): the creative challenge's.
                    string gate = EntryGateOf(room);
                    string key = scene == "Deepnest_East_Hornet_boss" ? "Hornet@4" : record;
                    if (BossArena.DevWorldArena(key, out var arenaGate, out var needs))
                    {
                        gate = arenaGate;
                        foreach (var (flag, v) in needs) PlayerData.instance.SetBool(flag, v);
                    }
                    TransitionPoint.lastEntered = gate;
                    float t0 = Time.time;
                    while (gm.IsInSceneTransition && Time.time - t0 < 10f) yield return null;
                    errors.Clear();
                    gm.BeginSceneTransition(new GameManager.SceneLoadInfo
                    {
                        SceneName = room, EntryGateName = gate, HeroLeaveDirection = GlobalEnums.GatePosition.unknown,
                        EntryDelay = 0f, WaitForSceneTransitionCameraFade = true, Visualization = GameManager.SceneLoadVisualizations.Default,
                    });
                    t0 = Time.time;
                    yield return new WaitForSeconds(0.5f);
                    while ((gm.sceneName != room || gm.IsInSceneTransition || !GameHooks.PlayerHasControl()) && Time.time - t0 < 20f) yield return null;
                    bool arrived = gm.sceneName == room;
                    yield return new WaitForSeconds(1.5f);

                    List<HealthManager> Parts(bool includeInactive)
                    {
                        var found = new List<HealthManager>();
                        for (int s = 0; s < USM.sceneCount; s++)
                        {
                            var sc = USM.GetSceneAt(s);
                            if (!sc.IsValid() || !sc.isLoaded) continue;
                            foreach (var root in sc.GetRootGameObjects())
                            {
                                bool ours = false;
                                foreach (var hm in root.GetComponentsInChildren<HealthManager>(true))
                                    if (UI.EnemyHover.RecordOf(hm) == record) ours = true;
                                if (!ours) continue;
                                foreach (var hm in root.GetComponentsInChildren<HealthManager>(includeInactive))
                                    if (includeInactive || hm.gameObject.activeInHierarchy) found.Add(hm);
                            }
                        }
                        return found;
                    }
                    // What each part is doing, for the log.
                    string PartsInfo()
                    {
                        var d = new StringBuilder();
                        foreach (var hm in Parts(false))
                        {
                            var col = hm.GetComponent<Collider2D>();
                            var rend = hm.GetComponent<Renderer>();
                            var body = hm.GetComponent<Rigidbody2D>();
                            d.Append($" [{hm.name} {(Vector2)hm.transform.position:F1} 血{hm.hp} 死{hm.isDead} 打倒{EnemyStats.Defeated(hm)} 无敌{hm.IsInvincible} 帧{MeleeHits.Immune(hm)} 碰撞{(col == null ? "无" : col.enabled ? (col.isTrigger ? "触发" : "开") : "关")} 显示{(rend != null && rend.enabled)} 速度{(body != null ? body.linearVelocity : Vector2.zero):F1}");
                            foreach (var f in hm.GetComponents<PlayMakerFSM>()) d.Append($" {f.FsmName}:{f.ActiveStateName}{(f.enabled ? "" : "(关)")}");
                            d.Append("]");
                        }
                        return d.ToString();
                    }
                    var all = Parts(true);
                    // An arena laid over its room may come a moment later.
                    for (float until = Time.time + 10f; all.Count == 0 && Time.time < until; all = Parts(true)) yield return new WaitForSeconds(0.5f);
                    if (all.Count == 0)
                    {
                        var seen = new StringBuilder();
                        for (int s = 0; s < USM.sceneCount; s++) seen.Append($" {USM.GetSceneAt(s).name}");
                        seen.Append("；敌人：");
                        int n = 0;
                        foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                            if (n++ < 30) seen.Append($" {hm.gameObject.scene.name}/{hm.name}={UI.EnemyHover.RecordOf(hm)}{(hm.gameObject.activeInHierarchy ? "" : "(关)")}");
                        Plugin.Log.LogInfo($"[AutoTest] 首领流程 {record}：没找到，载入的场景{seen}");
                    }
                    var h = HeroController.instance;
                    if (all.Count > 0)
                    {
                        Vector2 bp = Vector2.zero;
                        int near = 0;
                        foreach (var hm in all)
                            if (Vector2.Distance(hm.transform.position, all[0].transform.position) < 15f) { bp += (Vector2)hm.transform.position; near++; }
                        bp /= Mathf.Max(1, near);
                        int walk = 0;
                        foreach (int side in new[] { -1, 1 })
                        {
                            var probe = bp + new Vector2(side * 6f, 3f);
                            var down = Physics2D.Raycast(probe, Vector2.down, 30f, terrain);
                            if (down.collider == null) continue;
                            h.transform.position = new Vector3(probe.x, down.point.y + 0.7f, h.transform.position.z);
                            walk = -side;
                            break;
                        }
                        Player.TerraMovement.DevMove = walk;
                        yield return new WaitForSeconds(1.5f);
                        Player.TerraMovement.DevMove = 0;
                    }
                    // Up to forty seconds to wake: the room's waiting triggers crossed one by one (a battle's
                    // start: the Mantis Lords' arena), its words turned with jump.
                    t0 = Time.time;
                    bool woke = false;
                    int wakeTry = 0;
                    float nextPoke = Time.time + 4f;
                    while (all.Count > 0 && Time.time - t0 < 40f && !woke)
                    {
                        mod.Stats.FullHeal();
                        PlayerData.instance.isInvincible = true;
                        foreach (var hm in Parts(false)) if (!hm.isDead) woke = true;
                        if (woke) break;
                        if (!GameHooks.PlayerHasControl()) yield return Tap(UnityEngine.InputSystem.Key.Space);
                        else if (Time.time >= nextPoke)
                        {
                            nextPoke = Time.time + 5f;
                            var trig = WaitingTriggers(room);
                            // A challenge to take first (the Mantis Lords): up at its prompt, as the player accepts it.
                            var prompt = ChallengePrompt(room);
                            if (prompt != null && (wakeTry % 2 == 0 || trig.Count == 0))
                            {
                                wakeTry++;
                                h.transform.position = Clear(h, prompt.transform.position);
                                yield return new WaitForSeconds(1f);
                                yield return Tap(UnityEngine.InputSystem.Key.W, 0.3f);
                                Plugin.Log.LogInfo($"[AutoTest] 首领流程 {record}：在挑战提示 {(Vector2)prompt.transform.position:F1} 按上（{prompt.ActiveStateName}）");
                            }
                            else if (trig.Count > 0)
                            {
                                var tb = trig[wakeTry++ % trig.Count];
                                h.transform.position = Clear(h, tb.center);
                                Plugin.Log.LogInfo($"[AutoTest] 首领流程 {record}：还没醒，把主角放进触发区 {tb.center:F1}（{tb.size:F1}）");
                            }
                        }
                        yield return new WaitForSeconds(0.25f);
                    }
                    if (all.Count > 0 && !woke)
                    {
                        var d = new StringBuilder();
                        foreach (var hm in all) d.Append($" [{hm.name} {(Vector2)hm.transform.position:F1} 开{hm.gameObject.activeInHierarchy}{FsmStates(hm.gameObject, false)}]");
                        Plugin.Log.LogInfo($"[AutoTest] 首领流程 {record}：没醒，主角 {(Vector2)h.transform.position:F1}，它们{d}；场景里的{SceneFsmStates(room)}");
                        Logged(() => DumpBattleFsms(room));
                    }
                    // The fight: blows until the game counts it beaten.
                    int strikes = 0, side2 = 1, triggerTry = 0, quietStrikes = 0, diags = 0, lastHp = -1, hpStart = -1;
                    bool dumped = false;
                    float hpSince = Time.time;
                    t0 = Time.time;
                    float nextHit = 0f, wokeAt = Time.time, nextWalk = Time.time + 6f, nextTalk = 0f, nextDiag = Time.time + 10f;
                    bool beaten = false;
                    string lastStates = "";
                    while (woke && Time.time - t0 < 150f)
                    {
                        mod.Stats.FullHeal();
                        PlayerData.instance.isInvincible = true;
                        // Beaten: the book counted it and none of its kind is left standing (the six Watcher Knights, the three Mantis
                        // Lords; a special death, the Collector's, stays at no health without isDead).
                        if (KillsOf(record) > kills0)
                        {
                            bool standing = false;
                            foreach (var hm in Parts(false))
                                if (!hm.isDead && hm.hp > 0 && !EnemyStats.Defeated(hm) && UI.EnemyHover.RecordOf(hm) == record) standing = true;
                            if (!standing) { beaten = true; break; }
                        }
                        // A boss's words before the fight wait for a key, as the player turns them with jump.
                        if (!GameHooks.PlayerHasControl() && Time.time >= nextTalk)
                        {
                            nextTalk = Time.time + 1f;
                            yield return Tap(UnityEngine.InputSystem.Key.Space);
                        }
                        // Nothing taken for a while (asleep in its armor, or a trigger not crossed yet): a walk across past
                        // it, from the other side each time.
                        // Once it has taken damage the fight is on: no more triggers (the Collector up at the ceiling a while).
                        bool stalled = Time.time - hpSince > 5f;
                        bool started = hpStart >= 0 && lastHp >= 0 && lastHp < hpStart;
                        if (Time.time >= nextWalk)
                        {
                            nextWalk = Time.time + 6f;
                            // First the room's waiting triggers (a battle's start, a boss's wake), one each time, as walking in would cross them.
                            var trig = WaitingTriggers(room);
                            if (stalled && !started && GameHooks.PlayerHasControl() && trig.Count > 0 && triggerTry < trig.Count)
                            {
                                var tb = trig[triggerTry++];
                                h.transform.position = Clear(h, tb.center);
                                Plugin.Log.LogInfo($"[AutoTest] 首领流程 {record}：把主角放进触发区 {tb.center:F1}（{tb.size:F1}）");
                                yield return new WaitForSeconds(2f);
                            }
                            else if (stalled && GameHooks.PlayerHasControl())
                            {
                                var parts = Parts(false);
                                if (parts.Count > 0)
                                {
                                    var at = (Vector2)parts[0].transform.position + new Vector2(side2 * 8f, 3f);
                                    var down = Physics2D.Raycast(at, Vector2.down, 30f, terrain);
                                    if (down.collider != null) h.transform.position = new Vector3(at.x, down.point.y + 0.7f, h.transform.position.z);
                                    Player.TerraMovement.DevMove = -side2;
                                    yield return new WaitForSeconds(2f);
                                    Player.TerraMovement.DevMove = 0;
                                    side2 = -side2;
                                }
                            }
                        }
                        // Nothing struck for ten seconds: what each part is doing, for the log.
                        if (strikes == quietStrikes && Time.time >= nextDiag && diags < 6)
                        {
                            nextDiag = Time.time + 10f;
                            diags++;
                            Plugin.Log.LogInfo($"[AutoTest] 首领流程 {record}：10 秒没打到，主角 {(Vector2)h.transform.position:F1} 可操作 {GameHooks.PlayerHasControl()}{PartsInfo()}");
                        }
                        if (strikes != quietStrikes) { quietStrikes = strikes; nextDiag = Time.time + 10f; }
                        // Struck but nothing taken for twenty seconds (asleep in its armor): the fight's FSMs, once.
                        int hpNow = 0;
                        foreach (var hm in Parts(false)) hpNow += Mathf.Max(0, hm.hp);
                        if (hpStart < 0 && hpNow > 0) hpStart = hpNow;
                        if (hpNow != lastHp) { lastHp = hpNow; hpSince = Time.time; }
                        else if (!dumped && Time.time - hpSince > 20f)
                        {
                            dumped = true;
                            Plugin.Log.LogInfo($"[AutoTest] 首领流程 {record}：打了 {strikes} 下，20 秒没掉血，主角 {(Vector2)h.transform.position:F1}，它{PartsInfo()}；场景里的{SceneFsmStates(room)}");
                            var first = Parts(false);
                            Logged(() => { DumpBattleFsms(room); if (first.Count > 0) DumpFsms(first[0].transform); });
                        }
                        if (Time.time >= nextHit && GameHooks.PlayerHasControl())
                        {
                            nextHit = Time.time + 0.35f;
                            var states = new StringBuilder();
                            foreach (var hm in Parts(false))
                            {
                                if (hm.isDead || EnemyStats.Defeated(hm)) continue;
                                var fsm = hm.GetComponent<PlayMakerFSM>();
                                if (fsm != null) states.Append($"{hm.name}:{fsm.ActiveStateName} ");
                                // Struck in its armor too, as a player does (a blocked hit wakes the Crystal Guardian; the
                                // False Knight's rage holds his armor up by the mod).
                                // Whatever of it a weapon would touch: its own box, else a child's that is its.
                                var col = hm.GetComponent<Collider2D>();
                                if (col == null || !col.enabled)
                                {
                                    col = null;
                                    foreach (var c in hm.GetComponentsInChildren<Collider2D>())
                                        if (c.enabled && c.GetComponentInParent<HealthManager>() == hm) { col = c; break; }
                                }
                                if (col == null) continue;
                                if (mod.Melee.HitWith(col, hm.name == "Head" ? 5000 : 1500, 0f, 0f, 1f, h.GetComponent<BoxCollider2D>().bounds.center)) strikes++;
                            }
                            lastStates = states.ToString();
                        }
                        yield return null;
                    }
                    float took = Time.time - wokeAt;
                    // Control back after the death (its scene's own ending to it).
                    t0 = Time.time;
                    while (!GameHooks.PlayerHasControl() && Time.time - t0 < 30f) yield return Tap(UnityEngine.InputSystem.Key.Space);
                    yield return new WaitForSeconds(1.5f);
                    // Floating in a pool (the Fluke Mother's floor is water) the player swims and jumps out: that's control.
                    var swim = PlayMakerFSM.FindFsmOnGameObject(h.gameObject, "Surface Water");
                    bool floating = swim != null && (swim.ActiveStateName == "Idle" || swim.ActiveStateName.StartsWith("Swim"));
                    bool control2 = GameHooks.PlayerHasControl() || floating;
                    if (!control2)
                    {
                        Plugin.Log.LogInfo($"[AutoTest] 首领流程 {record}：打完不能操作：{HeroState(h)}；主角的状态机{FsmStates(h.gameObject, false)}；场景里的{SceneFsmStates(room)}");
                        var water = PlayMakerFSM.FindFsmOnGameObject(h.gameObject, "Surface Water");
                        Logged(() =>
                        {
                            if (water != null) DumpFsm(water);
                            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                                if (f.FsmName == "Surface Water Region") { DumpFsm(f); break; }
                        });
                    }
                    bool barLeft = false;
                    foreach (var hm in Parts(false))
                        if (!hm.isDead && hm.hp > 0 && !EnemyStats.Defeated(hm) && EnemyStats.Of(hm).Boss && hm.gameObject.activeInHierarchy && Vector2.Distance(hm.transform.position, h.transform.position) < 40f) barLeft = true;
                    int drops = EnemyDrops.DevDropped - drops0;
                    bool ok = arrived && woke && beaten && control2 && drops > 0 && !barLeft && errors.Count == 0;
                    if (ok) good++;
                    else bad.Append($" {record}");
                    string errs = errors.Count > 0 ? $"，报错 {errors.Count} 条：{string.Join(" | ", errors.GetRange(0, Mathf.Min(3, errors.Count)))}" : "";
                    Plugin.Log.LogInfo($"[AutoTest] 首领流程 {record}（{room}）：{(ok ? "好" : "坏")}，到达 {arrived}，找到 {all.Count} 个，醒了 {woke}，打了 {strikes} 下，{(beaten ? $"{took:F0} 秒打倒" : "没打倒")}，掉落 {drops} 样，尸体还有血条 {barLeft}，之后可操作 {control2}{(beaten ? "" : "，最后的状态 " + lastStates)}{errs}");
                    Shot(gm.sceneName, $"bossrun_{record}");
                    yield return null;
                }
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
                Player.TerraMovement.DevMove = 0;
                RoarRelease.DevTrace = false;
            }
            Plugin.Log.LogInfo($"[AutoTest] 首领流程 合计 {good}/{list.Count} 个正常{(bad.Length > 0 ? "，有问题的：" + bad : "")}");
        }

        /// <summary>The trigger boxes in a room (and its "_boss" scene) whose FSM waits in its state for the player to touch them.</summary>
        static List<Bounds> WaitingTriggers(string room)
        {
            var found = new List<Bounds>();
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
            {
                string sn = f.gameObject.scene.name;
                if (sn != room && sn != room + "_boss") continue;
                var state = f.Fsm?.ActiveState;
                if (state == null) continue;
                bool waits = false;
                foreach (var act in state.Actions)
                    if (act is HutongGames.PlayMaker.Actions.Trigger2dEvent te && (te.collideTag == null || string.IsNullOrEmpty(te.collideTag.Value) || te.collideTag.Value == "Player" || te.collideTag.Value == "HeroBox")) waits = true;
                if (!waits) continue;
                var col = f.GetComponent<Collider2D>();
                if (col != null && col.enabled && col.isTrigger) found.Add(col.bounds);
            }
            return found;
        }

        /// <summary>A dump for the log that mustn't end the run when an FSM in it is half gone.</summary>
        static void Logged(System.Action dump)
        {
            try { dump(); }
            catch (System.Exception e) { Plugin.Log.LogInfo($"[AutoTest] 首领流程 导出状态机出错：{e.GetType().Name} {e.Message}"); }
        }

        /// <summary>The fight's own FSMs of a room (its battle scene, what starts it), whole, for the log.</summary>
        static void DumpBattleFsms(string room)
        {
            int n = 0;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                string sn = f.gameObject.scene.name;
                if (sn != room && sn != room + "_boss") continue;
                string key = f.gameObject.name + " " + f.FsmName;
                if (key.IndexOf("Battle", System.StringComparison.OrdinalIgnoreCase) < 0 && key.IndexOf("Boss", System.StringComparison.OrdinalIgnoreCase) < 0
                    && key.IndexOf("Start", System.StringComparison.OrdinalIgnoreCase) < 0 && key.IndexOf("Wake", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (++n > 8) break;
                var col = f.GetComponent<Collider2D>();
                Plugin.Log.LogInfo($"[AutoTest] 首领流程 状态机 {sn}/{key}（碰撞 {(col == null ? "无" : $"{col.enabled}/{col.isTrigger} {col.bounds.center:F1} {col.bounds.size:F1}")}）");
                DumpFsm(f);
            }
        }

        /// <summary>A boss's challenge prompt in a room ("Challenge Start"), still waiting.</summary>
        static PlayMakerFSM ChallengePrompt(string room)
        {
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
            {
                string sn = f.gameObject.scene.name;
                if ((sn == room || sn == room + "_boss") && f.FsmName == "Challenge Start") return f;
            }
            return null;
        }

        /// <summary>The book's kills of a record, its stages' own tables included (Hornet@4).</summary>
        static int KillsOf(string record)
        {
            int n = 0;
            foreach (var kv in Bestiary.Kills) if (kv.Key == record || kv.Key.StartsWith(record + "@")) n += kv.Value;
            return n;
        }

        static string FsmStates(GameObject go, bool children)
        {
            var sb = new StringBuilder();
            foreach (var f in children ? go.GetComponentsInChildren<PlayMakerFSM>() : go.GetComponents<PlayMakerFSM>())
                sb.Append($" {f.gameObject.name}/{f.FsmName}:{f.ActiveStateName}");
            return sb.ToString();
        }

        /// <summary>The FSMs of a room (and its "_boss" scene) in a state that isn't an idle one, for a stuck control.</summary>
        static string SceneFsmStates(string room)
        {
            var sb = new StringBuilder();
            int n = 0;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
            {
                string sn = f.gameObject.scene.name;
                if (sn != room && sn != room + "_boss" && sn != "DontDestroyOnLoad") continue;
                string st = f.ActiveStateName ?? "";
                if (st == "" || st == "Idle" || st == "Init" || st == "Pause" || st == "Inactive" || st == "Wait" || st == "Dormant") continue;
                if (++n > 40) break;
                sb.Append($" {f.gameObject.name}/{f.FsmName}:{st}");
            }
            return sb.ToString();
        }

        static PlayMakerFSM FsmNamed(HealthManager hm, string name)
        {
            foreach (var f in hm.GetComponents<PlayMakerFSM>()) if (f.FsmName == name) return f;
            return null;
        }
    }
}
