using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TerraKnight.Items;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "bossflow", "bossflow_A+B"): every world boss the whole way a player goes. From the
    /// room next door, through the arena's own gate on foot (or by a drop, or a jump up); towards the
    /// boss until it wakes, its words turned with jump and a challenge taken with up at its prompt;
    /// fought with a real weapon (the Zenith, every swing a real use aimed at it, its swords' own hits);
    /// after its death whatever else of the fight is still up (the Broken Vessel's balloons, the
    /// Collector's jar spawn) fought down too and what it left picked up with up; then the battle's
    /// gates open again and out through the gate it came in by. Each help the test had to give (a
    /// teleport towards something it couldn't walk to) is counted and named.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        /// <summary>Dev: the weapon the flow fights with, and whether it aims at the body even while a weak point is out (a player's poor aim).</summary>
        static ItemType flowWeapon = ItemType.Zenith;
        /// <summary>The way a player comes in where the arena's own gate is its exit (the Gruz Mother's right gate opens only once she's beaten).</summary>
        static readonly Dictionary<string, string> playerGates = new Dictionary<string, string> { ["BigFly"] = "left1" };
        static bool flowAimBody;

        IEnumerator BossFlowTest(string[] only)
        {
            var list = new List<(string Record, string Scene)> { ("Hornet", "Fungus1_04_boss") };
            foreach (var part in bossParts) list.AddRange(part);
            if (only != null)
                list = list.FindAll(e => System.Array.IndexOf(only, e.Scene == "Deepnest_East_Hornet_boss" ? "Hornet2" : e.Record) >= 0);
            var errors = new List<string>();
            void OnLog(string msg, string stack, LogType type)
            {
                if ((type == LogType.Exception || (type == LogType.Error && msg.Contains("Exception")) || msg.StartsWith("[Error")) && errors.Count < 20) errors.Add(msg.Length > 160 ? msg.Substring(0, 160) : msg);
            }
            Application.logMessageReceived += OnLog;
            RoarRelease.DevTrace = true;
            World.VanillaGuards.DevGateTrace = true;
            var inv = mod.Inventory;
            inv.Infinite = true;
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
                    int assists = 0, deaths = 0;
                    var notes = new StringBuilder();
                    void Assist(string what) { assists++; notes.Append($" {what}"); Plugin.Log.LogInfo($"[AutoTest] 首领全流程 {record} 辅助：{what}"); }
                    TerraMod.DevLastFall = null;
                    World.VanillaGuards.DevGateClosers.Clear();
                    SpecialDeaths.DevTrace = record == "FalseKnight";
                    var pd = PlayerData.instance;
                    pd.isInvincible = true;
                    inv.Slots[0] = ItemStack.Of(flowWeapon);
                    inv.Slots[Inventory.Size] = ItemStack.Of(ItemType.MusketBall, 999);
                    inv.Selected = 0;

                    // The arena's own gate and what its fight needs (the Traitor Lord's: a way out of it).
                    string gate = EntryGateOf(room);
                    string key = scene == "Deepnest_East_Hornet_boss" ? "Hornet@4" : record;
                    if (BossArena.DevWorldArena(key, out var arenaGate, out var needs))
                    {
                        gate = arenaGate;
                        foreach (var (flag, v) in needs) pd.SetBool(flag, v);
                    }
                    if (playerGates.TryGetValue(record, out var pg)) gate = pg;
                    float t0 = Time.time;
                    while (gm.IsInSceneTransition && Time.time - t0 < 10f) yield return null;
                    errors.Clear();

                    // 1. Next door, then in through the gate.
                    bool walkedIn = false;
                    string outside = null, outGate = null, outSide = null, roomSide = null;
                    if (GateLink(room, gate, out var link) && !link.Door && !string.IsNullOrEmpty(link.Entry))
                    {
                        outside = link.To;
                        outGate = link.Entry;
                        roomSide = link.Side;
                        outSide = GateLink(outside, outGate, out var back) ? back.Side : Opposite(link.Side);
                    }
                    if (outside != null)
                    {
                        yield return GoTo(outside, outGate);
                        var res = new bool[1];
                        yield return ThroughGate(outside, outGate, outSide, room, res);
                        walkedIn = res[0];
                        if (!walkedIn)
                        {
                            Bounds ob = default;
                            foreach (var t in Object.FindObjectsByType<TransitionPoint>(FindObjectsSortMode.None))
                                if (t.gameObject.scene.name == outside && t.name == outGate && t.GetComponent<Collider2D>() != null) ob = t.GetComponent<Collider2D>().bounds;
                            Plugin.Log.LogInfo($"[AutoTest] 首领全流程 {record}：从 {outside} 的 {outGate} 没走进 {room}，主角 {(Vector2)HeroController.instance.transform.position:F1}，在 {gm.sceneName}；挡着的：{(gm.sceneName == outside ? Blockers(HeroController.instance, ob) : "-")}");
                        }
                    }
                    if (gm.sceneName != room)
                    {
                        Assist(outside == null ? "没有门外的房间，直接传进门口" : "走不进去，直接传进门口");
                        TransitionPoint.lastEntered = gate;
                        yield return GoTo(room, gate);
                    }
                    t0 = Time.time;
                    while (!GameHooks.PlayerHasControl() && Time.time - t0 < 20f) yield return Tap(UnityEngine.InputSystem.Key.Space);
                    bool arrived = gm.sceneName == room;
                    var h = HeroController.instance;
                    var gatesBefore = new Dictionary<string, string>();
                    foreach (var (gn, gs) in BattleGates(room)) gatesBefore[gn] = gs;
                    Plugin.Log.LogInfo($"[AutoTest] 首领全流程 {record}：进了 {gm.sceneName}（{(walkedIn ? $"从 {outside} 走进来" : "传进来")}），主角 {(Vector2)h.transform.position:F1}，铁门{string.Join("", System.Linq.Enumerable.Select(gatesBefore, kv => $" {kv.Key}:{kv.Value}"))}");
                    yield return new WaitForSeconds(1f);

                    List<HealthManager> Parts()
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
                                foreach (var hm in root.GetComponentsInChildren<HealthManager>(false)) found.Add(hm);
                            }
                        }
                        return found;
                    }
                    var roomGates = new List<Bounds>();
                    foreach (var t in Object.FindObjectsByType<TransitionPoint>(FindObjectsSortMode.None))
                        if (t.gameObject.scene.name == room && t.GetComponent<Collider2D>() != null) roomGates.Add(t.GetComponent<Collider2D>().bounds);
                    // A step that way would come within reach of one of the room's gates.
                    bool NearGate(int dir)
                    {
                        if (dir == 0) return false;
                        var hb = h.GetComponent<BoxCollider2D>().bounds;
                        foreach (var g in roomGates)
                        {
                            var e = g;
                            e.Expand(new Vector3(5f, 1f, 0f));
                            var ahead = new Vector3(hb.center.x + dir * 1.5f, hb.center.y, e.center.z);
                            if (e.Contains(ahead) && (g.center.x - hb.center.x) * dir > 0f) return true;
                        }
                        return false;
                    }
                    bool Hittable(HealthManager hm)
                    {
                        foreach (var c in hm.GetComponentsInChildren<Collider2D>())
                            if (c.enabled && c.GetComponentInParent<HealthManager>() == hm) return true;
                        return false;
                    }
                    bool Alive(HealthManager hm) => hm != null && !hm.isDead && hm.hp > 0 && hm.gameObject.activeInHierarchy && !EnemyStats.Defeated(hm);
                    // Anything else of the room's still fighting (not the boss): spawn, balloons, guards.
                    List<HealthManager> Others()
                    {
                        var found = new List<HealthManager>();
                        foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                        {
                            string sn = hm.gameObject.scene.name;
                            if ((sn != room && sn != room + "_boss") || !Alive(hm) || UI.EnemyHover.RecordOf(hm) == record || World.ArenaGuard.OffMap(hm)) continue;
                            if (Vector2.Distance(hm.transform.position, h.transform.position) > 45f) continue;
                            found.Add(hm);
                        }
                        return found;
                    }
                    Vector2 Aim(HealthManager hm)
                    {
                        var col = hm.GetComponent<Collider2D>();
                        if (col == null || !col.enabled)
                            foreach (var c in hm.GetComponentsInChildren<Collider2D>())
                                if (c.enabled && c.GetComponentInParent<HealthManager>() == hm) { col = c; break; }
                        return col != null ? (Vector2)col.bounds.center : (Vector2)hm.transform.position;
                    }
                    // A frame of the fight: towards the target on foot, the Zenith swung at it whenever it may.
                    float farSince = -1f;
                    int uses = 0;
                    IEnumerator FightToward(HealthManager target)
                    {
                        mod.Stats.FullHeal();
                        PlayerData.instance.isInvincible = true;
                        if (!GameHooks.PlayerHasControl()) { Player.TerraMovement.DevMove = 0; yield return Tap(UnityEngine.InputSystem.Key.Space); yield break; }
                        var at = Aim(target);
                        Vector2 me = h.GetComponent<BoxCollider2D>().bounds.center;
                        float dx = at.x - me.x;
                        int step = Mathf.Abs(dx) > 6f ? (int)Mathf.Sign(dx) : 0;
                        Stride(h, NearGate(step) ? 0 : step);
                        if (Vector2.Distance(at, me) > 14f) { if (farSince < 0f) farSince = Time.time; }
                        else farSince = -1f;
                        // Out of reach on foot a while (a ledge, a pit between): next to it.
                        if (farSince > 0f && Time.time - farSince > 8f)
                        {
                            farSince = -1f;
                            Assist($"走不到 {target.name} 旁边，传过去");
                            var probe = at + new Vector2(dx > 0 ? -5f : 5f, 3f);
                            var down = Physics2D.Raycast(probe, Vector2.down, 30f, terrain);
                            h.transform.position = Clear(h, down.collider != null ? down.point + Vector2.up * 0.7f : probe);
                        }
                        mod.DevAim = at;
                        if (mod.DevReady && !mod.IsUsingItem) { mod.DevUseSelected(at); uses++; }
                        yield return null;
                    }

                    // 2. Woken by walking up to it (its words turned with jump, a challenge taken with up).
                    t0 = Time.time;
                    bool woke = false;
                    float wokeAt = Time.time;
                    float nextPoke = Time.time + 20f;
                    int pokeTry = 0;
                    while (arrived && Time.time - t0 < 50f)
                    {
                        mod.Stats.FullHeal();
                        pd.isInvincible = true;
                        var parts = Parts();
                        // Up and fighting: out, alive and with a box a blow can land on (Hornet waits on her ledge without one).
                        foreach (var hm in parts) if (Alive(hm) && Hittable(hm)) woke = true;
                        if (woke) break;
                        if (!GameHooks.PlayerHasControl()) { Player.TerraMovement.DevMove = 0; yield return Tap(UnityEngine.InputSystem.Key.Space); continue; }
                        var prompt = ChallengePrompt(room);
                        Vector2 me = h.transform.position;
                        if (prompt != null)
                        {
                            float dx = prompt.transform.position.x - me.x;
                            if (Mathf.Abs(dx) < 1.2f) { Player.TerraMovement.DevMove = 0; yield return Tap(UnityEngine.InputSystem.Key.W, 0.3f); yield return new WaitForSeconds(0.5f); }
                            else Stride(h, (int)Mathf.Sign(dx));
                        }
                        else
                        {
                            // Towards the boss's body (still asleep or hidden), else on into the room.
                            Vector2 goal = me + new Vector2(roomSide == "left" ? 10f : roomSide == "right" ? -10f : 0f, 0f);
                            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                                if (UI.EnemyHover.RecordOf(hm) == record && (hm.gameObject.scene.name == room || hm.gameObject.scene.name == room + "_boss")) { goal = hm.transform.position; break; }
                            float dx = goal.x - me.x;
                            int step = Mathf.Abs(dx) > 2f ? (int)Mathf.Sign(dx) : 0;
                            Stride(h, NearGate(step) ? 0 : step);
                        }
                        // Walking didn't do it: the room's waiting triggers, then the prompt, by hand.
                        if (Time.time >= nextPoke)
                        {
                            nextPoke = Time.time + 5f;
                            var trig = WaitingTriggers(room);
                            if (prompt != null && (pokeTry % 2 == 0 || trig.Count == 0))
                            {
                                pokeTry++;
                                Assist("走到挑战提示前没接上，传到提示上");
                                h.transform.position = Clear(h, prompt.transform.position);
                            }
                            else if (trig.Count > 0)
                            {
                                var tb = trig[pokeTry++ % trig.Count];
                                Assist($"没醒，传进触发区 {tb.center:F0}");
                                h.transform.position = Clear(h, tb.center);
                            }
                        }
                        yield return null;
                    }
                    Player.TerraMovement.DevMove = 0;
                    if (woke) wokeAt = Time.time;
                    else
                    {
                        Plugin.Log.LogInfo($"[AutoTest] 首领全流程 {record}：没醒，主角 {(Vector2)h.transform.position:F1}；场景里的{SceneFsmStates(room)}");
                        Logged(() => DumpBattleFsms(room));
                    }
                    // 3. The fight, with the Zenith, until the game has it beaten.
                    bool beaten = false;
                    int hpStart = -1, lastHp = -1;
                    float hpSince = Time.time;
                    t0 = Time.time;
                    while (woke && Time.time - t0 < 240f)
                    {
                        if (h.cState.dead || gm.sceneName != room) break;
                        if (KillsOf(record) > kills0)
                        {
                            bool standing = false;
                            foreach (var hm in Parts()) if (Alive(hm) && UI.EnemyHover.RecordOf(hm) == record) standing = true;
                            if (!standing) { beaten = true; break; }
                        }
                        HealthManager target = null;
                        float best = float.MaxValue;
                        int hpNow = 0;
                        foreach (var hm in Parts())
                        {
                            if (!Alive(hm)) continue;
                            hpNow += hm.hp;
                            // A part that can be hurt now first, as a player goes for the False Knight's head when it's out.
                            float d = Vector2.Distance(hm.transform.position, h.transform.position) + (EnemyStats.Untouchable(hm) && !flowAimBody ? 1000f : 0f);
                            if (d < best) { best = d; target = hm; }
                        }
                        // The boss out of sight a moment (the Collector up in the roof, the Mantis Lords between turns): its helpers meanwhile.
                        if (target == null) foreach (var o in Others()) { target = o; break; }
                        if (hpStart < 0 && hpNow > 0) hpStart = hpNow;
                        if (hpNow != lastHp) { lastHp = hpNow; hpSince = Time.time; }
                        else if (Time.time - hpSince > 25f)
                        {
                            hpSince = Time.time;
                            var trig = WaitingTriggers(room);
                            Plugin.Log.LogInfo($"[AutoTest] 首领全流程 {record}：25 秒没掉血（{hpNow}），用了 {uses} 下，主角 {(Vector2)h.transform.position:F1}，可操作 {GameHooks.PlayerHasControl()}，{(target != null ? $"目标 {target.name} {(Vector2)target.transform.position:F1} 距离 {Vector2.Distance(target.transform.position, h.transform.position):F1} 无敌 {target.IsInvincible} 血 {target.hp} 层 {target.gameObject.layer}" : "没有目标")}；场景里的{SceneFsmStates(room)}");
                            if (trig.Count > 0)
                            {
                                var tb = trig[pokeTry++ % trig.Count];
                                Assist($"打不动，传进触发区 {tb.center:F0}");
                                h.transform.position = Clear(h, tb.center);
                            }
                        }
                        if (target == null) { if (!GameHooks.PlayerHasControl()) yield return Tap(UnityEngine.InputSystem.Key.Space); else yield return null; continue; }
                        yield return FightToward(target);
                    }
                    float took = Time.time - wokeAt;
                    mod.DevAim = null;
                    Player.TerraMovement.DevMove = 0;

                    // 4. After it: control back, the rest of the fight fought down, what it left picked up, the gates.
                    t0 = Time.time;
                    while (!h.cState.dead && gm.sceneName == room && !GameHooks.PlayerHasControl() && Time.time - t0 < 30f) yield return Tap(UnityEngine.InputSystem.Key.Space);
                    int cleared = 0, picked = 0;
                    var pickedNames = new StringBuilder();
                    var triedShiny = new HashSet<PlayMakerFSM>();
                    bool gateShut = false;
                    List<(string Name, string State)> gatesNow = BattleGates(room);
                    float afterFrom = Time.time;
                    int afterTries = 0;
                    while (beaten && !h.cState.dead && gm.sceneName == room && Time.time - afterFrom < 60f)
                    {
                        var others = Others();
                        if (others.Count > 0)
                        {
                            HealthManager near = others[0];
                            foreach (var o in others) if (Vector2.Distance(o.transform.position, h.transform.position) < Vector2.Distance(near.transform.position, h.transform.position)) near = o;
                            int before = others.Count;
                            yield return FightToward(near);
                            if (!Alive(near)) cleared++;
                            continue;
                        }
                        mod.DevAim = null;
                        // Something it left on the floor (a spell, a map): walked to and taken with up.
                        PlayMakerFSM shiny = null;
                        foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                        {
                            string sn = f.gameObject.scene.name;
                            if ((sn == room || sn == room + "_boss") && f.FsmName == "Shiny Control" && !triedShiny.Contains(f) && f.gameObject.activeInHierarchy) { shiny = f; break; }
                        }
                        if (shiny != null && GameHooks.PlayerHasControl())
                        {
                            triedShiny.Add(shiny);
                            float until = Time.time + 10f;
                            while (shiny != null && Time.time < until && Mathf.Abs(shiny.transform.position.x - h.transform.position.x) > 0.8f)
                            {
                                Player.TerraMovement.DevMove = (int)Mathf.Sign(shiny.transform.position.x - h.transform.position.x);
                                yield return null;
                            }
                            Player.TerraMovement.DevMove = 0;
                            if (shiny != null && Vector2.Distance(shiny.transform.position, h.transform.position) > 2.5f)
                            {
                                // On the floor under it: its inspect region takes up only from a Knight standing (Can Inspect?).
                                Assist($"走不到 {shiny.name}，传过去");
                                Vector2 sp = shiny.transform.position;
                                var floor = Physics2D.Raycast(sp + Vector2.up * 0.5f, Vector2.down, 10f, terrain);
                                h.transform.position = Clear(h, floor.collider != null ? floor.point + Vector2.up * 0.7f : sp);
                            }
                            string name = shiny != null ? shiny.name : "?";
                            bool gotIt = false;
                            for (int tryUp = 0; tryUp < 4 && shiny != null && !gotIt; tryUp++)
                            {
                                float stand = Time.time + 2f;
                                while (Time.time < stand && !h.cState.onGround) yield return null;
                                yield return new WaitForSeconds(0.4f);
                                string before = shiny != null ? shiny.ActiveStateName : "";
                                // W first (the mod's up), then the arrow and down, as the inspect region takes up or down.
                                var pressKey = tryUp == 0 || tryUp == 3 ? UnityEngine.InputSystem.Key.W : tryUp == 1 ? UnityEngine.InputSystem.Key.UpArrow : UnityEngine.InputSystem.Key.S;
                                // Held down afresh each frame for a moment (another tap ending meanwhile lets every key go).
                                var ua = GameHooks.Input.inputActions;
                                bool seenUp = false, seenDown = false;
                                for (float held = 0f; held < 0.35f; held += Time.unscaledDeltaTime)
                                {
                                    KeysDown(pressKey);
                                    yield return null;
                                    seenUp |= ua.up.IsPressed;
                                    seenDown |= ua.down.IsPressed;
                                }
                                KeysDown();
                                Plugin.Log.LogInfo($"[AutoTest] 首领全流程 {record}：按住 {pressKey} 捡 {name}：原版上 {seenUp} 下 {seenDown}");
                                yield return new WaitForSeconds(0.3f);
                                gotIt = shiny == null || shiny.ActiveStateName != before || !GameHooks.PlayerHasControl();
                            }
                            float wait = Time.time + 12f;
                            while (Time.time < wait && !GameHooks.PlayerHasControl()) yield return Tap(UnityEngine.InputSystem.Key.Space);
                            if (gotIt) { picked++; pickedNames.Append($" {name}"); }
                            else
                            {
                                string region = "";
                                if (shiny != null)
                                    foreach (var f in shiny.GetComponentsInChildren<PlayMakerFSM>(true))
                                        if (f != shiny) region += $" {f.name}/{f.FsmName}:{f.ActiveStateName}{(f.gameObject.activeInHierarchy ? "" : "(没开)")}";
                                Plugin.Log.LogInfo($"[AutoTest] 首领全流程 {record}：{name} 按上捡不起来，主角 {(Vector2)h.transform.position:F1} 在地上 {h.cState.onGround} 能查看 {h.CanInspect()} 状态 {h.hero_state} 接受输入 {h.acceptingInput} 攻击中 {h.cState.attacking}，它 {(shiny != null ? $"{(Vector2)shiny.transform.position:F1} {shiny.ActiveStateName}" : "没了")}；它的子状态机{region}");
                            }
                            continue;
                        }
                        gatesNow = BattleGates(room);
                        bool shut = false;
                        foreach (var (gn, gs) in gatesNow)
                            if (Shut(gs) && !(gatesBefore.TryGetValue(gn, out var b0) && Shut(b0))) shut = true;
                        if (!shut) break;
                        if (!GameHooks.PlayerHasControl()) { yield return Tap(UnityEngine.InputSystem.Key.Space); continue; }
                        // Still shut with nothing left to fight or take: the room's waiting triggers walked into one by
                        // one, as a player walking about it would (Hornet's gates open on her Dream Scene trigger once
                        // the cloak is taken).
                        var after = WaitingTriggers(room);
                        if (after.Count > 0 && Time.time - afterFrom > 3f && afterTries < after.Count * 2)
                        {
                            var tb = after[afterTries++ % after.Count];
                            Assist($"打完门没开，走进触发区 {tb.center:F0}（hasDash {PlayerData.instance.hasDash}）");
                            h.transform.position = Clear(h, tb.center);
                            yield return new WaitForSeconds(1.5f);
                            continue;
                        }
                        yield return new WaitForSeconds(0.5f);
                    }
                    Player.TerraMovement.DevMove = 0;
                    mod.DevAim = null;
                    var gatesText = new StringBuilder();
                    foreach (var (gn, gs) in gatesNow)
                    {
                        gatesText.Append($" {gn}:{(gatesBefore.TryGetValue(gn, out var b1) ? b1 + "→" : "")}{gs}");
                        if (beaten && Shut(gs) && !(gatesBefore.TryGetValue(gn, out var b2) && Shut(b2))) gateShut = true;
                    }
                    if (gateShut)
                        Logged(() =>
                        {
                            GateOpeners(record, room);
                            // The boss's own FSMs, whole (inactive ones too): where its way to the end of the fight stopped.
                            for (int s2 = 0; s2 < USM.sceneCount; s2++)
                                foreach (var root in USM.GetSceneAt(s2).GetRootGameObjects())
                                    foreach (var hm in root.GetComponentsInChildren<HealthManager>(true))
                                        if (UI.EnemyHover.RecordOf(hm) == record)
                                            foreach (var f in hm.GetComponents<PlayMakerFSM>()) DumpFsm(f);
                        });
                    // The False Knight's own FSM, whole: what ends his rage.
                    if (record == "FalseKnight")
                        Logged(() =>
                        {
                            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                                if (f.FsmName == "FalseyControl" && (f.gameObject.scene.name == room || f.gameObject.scene.name == room + "_boss")) { DumpFsm(f); break; }
                        });
                    int drops = EnemyDrops.DevDropped - drops0;
                    int kills = KillsOf(record) - kills0;
                    bool control = GameHooks.PlayerHasControl();

                    // 5. Out the way it came in (or another open way).
                    bool walkedOut = false;
                    string exitedBy = null;
                    if (beaten && !h.cState.dead && gm.sceneName == room && outside != null)
                    {
                        var res = new bool[1];
                        yield return ThroughGate(room, gate, roomSide, outside, res);
                        walkedOut = res[0];
                        if (!walkedOut && gm.sceneName == room)
                        {
                            Bounds gb = default;
                            foreach (var t in Object.FindObjectsByType<TransitionPoint>(FindObjectsSortMode.None))
                                if (t.gameObject.scene.name == room && t.name == gate && t.GetComponent<Collider2D>() != null) gb = t.GetComponent<Collider2D>().bounds;
                            Plugin.Log.LogInfo($"[AutoTest] 首领全流程 {record}：从 {gate} 没走出去，主角 {(Vector2)h.transform.position:F1}，挡着的：{Blockers(h, gb)}");
                            // Another way out, as a player takes the gate that opened (Hornet's Dream Scene opens one of hers).
                            foreach (var kv in gateLinks)
                            {
                                if (walkedOut || gm.sceneName != room) break;
                                if (!kv.Key.StartsWith(room + "/") || kv.Value.Door) continue;
                                string other = kv.Key.Substring(room.Length + 1);
                                if (other == gate) continue;
                                yield return ThroughGate(room, other, kv.Value.Side, kv.Value.To, res);
                                if (res[0]) { walkedOut = true; exitedBy = other; }
                            }
                        }
                    }
                    if (walkedOut && exitedBy != null) outside = GateLink(room, exitedBy, out var ex) ? ex.To : outside;
                    if (h.cState.dead || (gm.sceneName != room && gm.sceneName != outside))
                    {
                        deaths++;
                        Plugin.Log.LogInfo($"[AutoTest] 首领全流程 {record}：主角死了或被送走了（现在在 {gm.sceneName}）；最近的摔落：{TerraMod.DevLastFall ?? "无"}");
                        t0 = Time.time;
                        while (Time.time - t0 < 30f && (gm.IsInSceneTransition || !GameHooks.PlayerHasControl())) yield return Tap(UnityEngine.InputSystem.Key.Space);
                    }
                    bool ok = arrived && walkedIn && woke && beaten && kills > 0 && drops > 0 && control && (!gateShut || walkedOut) && (outside == null || walkedOut) && deaths == 0 && errors.Count == 0;
                    if (ok) good++;
                    else bad.Append($" {record}");
                    string errs = errors.Count > 0 ? $"，报错 {errors.Count} 条：{string.Join(" | ", errors.GetRange(0, Mathf.Min(3, errors.Count)))}" : "";
                    Plugin.Log.LogInfo($"[AutoTest] 首领全流程 {record}（{room}）：{(ok ? "好" : "坏")}，门外 {outside ?? "无"}/{outGate}，走进去 {walkedIn}，醒了 {woke}，天顶剑用了 {uses} 下，{(beaten ? $"{took:F0} 秒打倒" : "没打倒")}，清场 {cleared} 只，捡起 {picked} 样{pickedNames}，掉落 {drops} 样，图鉴 +{kills}，之后可操作 {control}，铁门{(gatesText.Length > 0 ? gatesText.ToString() : " 无")}，走出去 {walkedOut}{(exitedBy != null ? $"（从 {exitedBy}）" : "")}，死亡 {deaths}，辅助 {assists} 次{notes}{(TerraMod.DevLastFall != null ? "；" + TerraMod.DevLastFall : "")}{errs}");
                    Shot(gm.sceneName, $"bossflow_{record}");
                    yield return null;
                }
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
                Player.TerraMovement.DevMove = 0;
                mod.DevAim = null;
                RoarRelease.DevTrace = false;
                World.VanillaGuards.DevGateTrace = false;
            }
            Plugin.Log.LogInfo($"[AutoTest] 首领全流程 合计 {good}/{list.Count} 个正常{(bad.Length > 0 ? "，有问题的：" + bad : "")}");
        }

        /// <summary>
        /// The room's FSMs with a state that opens its battle gates (sends BG OPEN), with the state
        /// each is in now and the states that would send it: what the shut gates are waiting for.
        /// </summary>
        static void GateOpeners(string record, string room)
        {
            int n = 0;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                // Any loaded scene's (the opener may sit elsewhere: a boss scene, the hero, the game's own objects).
                if (f.FsmName == "BG Control") continue;
                var senders = new StringBuilder();
                foreach (var st in f.FsmStates)
                    foreach (var a in st.Actions)
                    {
                        // Any of the action's events or strings naming it (SendEventByName, SendEvent, SendEventToRegister, a broadcast...).
                        bool names = false;
                        foreach (var fi in a.GetType().GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
                        {
                            object v = fi.GetValue(a);
                            string text = v is HutongGames.PlayMaker.FsmEvent fe ? fe.Name : v is HutongGames.PlayMaker.FsmString fs ? fs.Value : v as string;
                            if (text != null && (text.Contains("BG OPEN") || text.Contains("BG QUICK OPEN") || text.Contains("BG DESTROY"))) { names = true; break; }
                        }
                        if (names) { senders.Append($" {st.Name}({a.GetType().Name})"); break; }
                    }
                if (senders.Length == 0) continue;
                if (++n > 10) break;
                Plugin.Log.LogInfo($"[AutoTest] 首领全流程 {record} 门没开：{PathOf(f.transform)} {f.FsmName} 现在 {f.ActiveStateName}{(f.gameObject.activeInHierarchy ? "" : "（物体没开）")}{(f.enabled ? "" : "（状态机关着）")}，会开门的状态{senders}");
            }
            if (n == 0) Plugin.Log.LogInfo($"[AutoTest] 首领全流程 {record} 门没开：房间里没有发 BG OPEN / QUICK OPEN / DESTROY 的状态机");
            // A battle waiting on its count: what's left of it, and the enemies it was given that haven't reported their deaths.
            var battleOf = HarmonyLib.AccessTools.FieldRefAccess<HealthManager, GameObject>("battleScene");
            var notified = HarmonyLib.AccessTools.FieldRefAccess<HealthManager, bool>("notifiedBattleScene");
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (f.FsmName != "Battle Control") continue;
                string sn = f.gameObject.scene.name;
                if (sn != room && sn != room + "_boss") continue;
                var left = f.FsmVariables.GetFsmInt("Battle Enemies");
                var owed = new StringBuilder();
                int given = 0;
                foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (battleOf(hm) != f.gameObject) continue;
                    given++;
                    if (!notified(hm)) owed.Append($" {PathOf(hm.transform)}（死 {hm.isDead} 血 {hm.hp} 在场 {hm.gameObject.activeInHierarchy} 打倒 {EnemyStats.Defeated(hm)} {(Vector2)hm.transform.position:F0}）");
                }
                Plugin.Log.LogInfo($"[AutoTest] 首领全流程 {record} 门没开：{PathOf(f.transform)} 还等 {(left != null ? left.Value : -99)} 只，分给它的敌人还在的 {given} 个，没报到的：{(owed.Length > 0 ? owed.ToString() : " 无")}");
            }
            // The ones that shut them, whole: where their way to opening them again stopped.
            foreach (var closer in World.VanillaGuards.DevGateClosers)
                if (closer != null)
                {
                    Plugin.Log.LogInfo($"[AutoTest] 首领全流程 {record} 关门的：{PathOf(closer.transform)} {closer.FsmName} 现在 {closer.ActiveStateName}");
                    DumpFsm(closer);
                }
        }

        float strideStuckSince = -1f, strideLastX;
        /// <summary>Dev: jumps a walk took to get over what stopped it.</summary>
        int strideJumps;

        /// <summary>
        /// A step of a walk the way a player takes it: towards dir, and a jump (Space, as pressed)
        /// when he has stood stopped against something a moment (a step, a ledge, a gap's edge).
        /// </summary>
        void Stride(HeroController h, int dir)
        {
            Player.TerraMovement.DevMove = dir;
            float x = h.transform.position.x;
            if (dir != 0 && h.cState.onGround && Mathf.Abs(x - strideLastX) < 0.02f)
            {
                if (strideStuckSince < 0f) strideStuckSince = Time.time;
                else if (Time.time - strideStuckSince > 0.35f)
                {
                    strideStuckSince = -1f;
                    strideJumps++;
                    mod.StartCoroutine(Tap(UnityEngine.InputSystem.Key.Space, 0.4f));
                }
            }
            else strideStuckSince = -1f;
            strideLastX = x;
        }

        static string Opposite(string side) => side == "left" ? "right" : side == "right" ? "left" : side == "top" ? "bottom" : "top";

        /// <summary>
        /// Through a gate of the scene the Knight is in, as a player goes: a few steps inside it, then
        /// walking out of a side gate, rising out of a top one (a jump or the wings), dropping into a
        /// bottom one. res[0]: the scene beyond reached and still there a moment later.
        /// </summary>
        IEnumerator ThroughGate(string scene, string gateName, string side, string beyond, bool[] res)
        {
            res[0] = false;
            var gm = GameManager.instance;
            TransitionPoint tp = null;
            foreach (var t in Object.FindObjectsByType<TransitionPoint>(FindObjectsSortMode.None))
                if (t.gameObject.scene.name == scene && t.name == gateName) tp = t;
            var col = tp != null ? tp.GetComponent<Collider2D>() : null;
            if (col == null) { Plugin.Log.LogInfo($"[AutoTest] 首领全流程 {scene} 找不到门 {gateName}"); yield break; }
            var box = col.bounds;
            var h = HeroController.instance;
            var rb = h.GetComponent<Rigidbody2D>();
            const int terrain = 1 << World.ChunkColliders.TerrainLayer;
            Vector2 dir = side == "left" ? Vector2.left : side == "right" ? Vector2.right : side == "top" ? Vector2.up : Vector2.down;
            Vector2 start = (Vector2)box.center - dir * (dir.y == 0f ? box.extents.x + 4f : box.extents.y + 3f);
            if (dir.y == 0f)
            {
                var down = Physics2D.Raycast(start + Vector2.up * 2f, Vector2.down, 12f, terrain);
                if (down.collider != null) start.y = down.point.y + 0.8f;
            }
            float t0 = Time.time;
            while (!GameHooks.PlayerHasControl() && Time.time - t0 < 10f) yield return Tap(UnityEngine.InputSystem.Key.Space);
            h.transform.position = Clear(h, start);
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.5f);
            t0 = Time.time;
            Player.TerraMovement.DevMove = (int)dir.x;
            while (gm.sceneName == scene && Time.time - t0 < 10f)
            {
                if (dir.y > 0f) rb.linearVelocity = new Vector2(0f, 12f);
                yield return null;
            }
            yield return new WaitForSeconds(0.5f);
            Player.TerraMovement.DevMove = 0;
            t0 = Time.time;
            while ((gm.IsInSceneTransition || !GameHooks.PlayerHasControl()) && Time.time - t0 < 15f) yield return null;
            yield return new WaitForSeconds(1f);
            res[0] = gm.sceneName == beyond;
        }
    }
}
