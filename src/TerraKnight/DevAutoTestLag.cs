using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using UnityEngine.InputSystem;
using TerraKnight.World;
using UnityEngine;
using USceneManager = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight
{
    /// <summary>
    /// Dev diagnostics. Suite "lag": the frames in Deepnest's tram pass rooms (Deepnest_26b, Deepnest_26)
    /// against a plain room, with every script timed (<see cref="DevFrameProfiler"/>) and what the room holds.
    /// Suite "sentryscan": every Husk Sentry and Heavy Sentry of the City, originals and copies, watched
    /// with the hero away and near: their FSM states, how far they move, the sounds they start.
    /// Suite "dreamers": the three Dreamers' rooms, their dream entry FSMs, and a Dream Nail swing at each.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator LagTest()
        {
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            mod.DevNoFallDamage = true;
            foreach (var (scene, label, seconds) in new[] { ("Crossroads_01", "对照：十字路口第一间", 8f), ("Deepnest_26b", "电车票房间", 20f), ("Deepnest_26", "失败的电车道", 20f), ("Deepnest_26b", "电车票房间（再进一次）", 12f) })
            {
                var frames = new List<float>();
                bool watching = true;
                mod.StartCoroutine(WatchFrameTimes(frames, () => watching));
                float t0 = Time.realtimeSinceStartup;
                yield return GoTo(scene, EntryGateOf(scene));
                watching = false;
                float load = Time.realtimeSinceStartup - t0;
                pd.isInvincible = true;
                Plugin.Log.LogInfo($"[AutoTest] 卡顿 {label} 进场 {load:F1} 秒：{FrameStats(frames)}");
                Plugin.Log.LogInfo($"[AutoTest] 卡顿 {label} 进场时：{Census()}");
                yield return Measure(label + " 站在入口", seconds);
                // To the Tram Pass (its shiny) or the room's middle.
                var hero = HeroController.instance;
                Vector2? spot = null;
                foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                    if (f.FsmName == "Shiny Control" && f.gameObject.scene.name == scene) { spot = f.transform.position; break; }
                if (spot == null && TerrainSystem.All.Find(t => t != null && t.Region != null && t.Region.SceneName == scene) is TerrainSystem ts)
                    spot = (Vector2)ts.Region.CellRect.center * CellGrid.CellSize;
                if (spot != null)
                {
                    hero.transform.position = Clear(hero, spot.Value + Vector2.up * 0.5f);
                    hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                    yield return Measure(label + $" 在 {spot.Value:F0}", seconds * 0.5f);
                }
            }
        }

        IEnumerator Measure(string label, float seconds)
        {
            var frames = new List<float>();
            int gc0 = System.GC.CollectionCount(0);
            DevFrameProfiler.Begin();
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                frames.Add(Time.unscaledDeltaTime);
            }
            string report = DevFrameProfiler.Report(label, frames.Count);
            Plugin.Log.LogInfo($"[AutoTest] 卡顿 {label}：{FrameStats(frames)}，GC {System.GC.CollectionCount(0) - gc0} 次");
            Plugin.Log.LogInfo(report);
            Plugin.Log.LogInfo($"[AutoTest] 卡顿 {label} 之后：{Census()}");
        }

        static IEnumerator WatchFrameTimes(List<float> frames, System.Func<bool> on)
        {
            while (on())
            {
                yield return null;
                frames.Add(Time.unscaledDeltaTime);
            }
        }

        static string FrameStats(List<float> frames)
        {
            if (frames.Count == 0) return "没有帧";
            var sorted = new List<float>(frames);
            sorted.Sort();
            float sum = 0f;
            foreach (var f in frames) sum += f;
            int slow = frames.Count(f => f > 0.05f), verySlow = frames.Count(f => f > 0.25f);
            return $"{frames.Count} 帧，平均 {1f / (sum / frames.Count):F0} fps（{sum / frames.Count * 1000f:F1} ms），中位 {sorted[sorted.Count / 2] * 1000f:F1} ms，95% {sorted[(int)(sorted.Count * 0.95f)] * 1000f:F1} ms，最慢 {sorted[sorted.Count - 1] * 1000f:F0} ms，超过 50ms 的 {slow} 帧，超过 250ms 的 {verySlow} 帧";
        }

        /// <summary>What the loaded scenes hold now: enemies by name, bodies, colliders, renderers, particles, sounds, the mod's terrain.</summary>
        static string Census()
        {
            var sb = new System.Text.StringBuilder();
            var scenes = new List<string>();
            for (int i = 0; i < USceneManager.sceneCount; i++) scenes.Add(USceneManager.GetSceneAt(i).name);
            sb.Append($"场景 {string.Join(",", scenes)}；");
            var hms = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None);
            var byName = new Dictionary<string, int>();
            int alive = 0;
            foreach (var hm in hms)
            {
                if (hm == null || !hm.gameObject.activeInHierarchy) continue;
                if (!hm.isDead) alive++;
                string n = hm.name;
                int cut = n.IndexOf(" (");
                if (cut > 0) n = n.Substring(0, cut);
                byName[n] = byName.TryGetValue(n, out int k) ? k + 1 : 1;
            }
            sb.Append($"敌人 {alive} 活着（激活 {byName.Values.Sum()}）：{string.Join(" ", byName.OrderByDescending(kv => kv.Value).Take(10).Select(kv => kv.Key + "×" + kv.Value))}；");
            var bodies = Object.FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None);
            var byBody = new Dictionary<string, int>();
            foreach (var rb in bodies)
            {
                string n = rb.name;
                int cut = n.IndexOf(" (");
                if (cut > 0) n = n.Substring(0, cut);
                if (rb.transform.parent != null) n = rb.transform.parent.name + "/" + n;
                byBody[n] = byBody.TryGetValue(n, out int k) ? k + 1 : 1;
            }
            sb.Append($"刚体最多的：{string.Join(" ", byBody.OrderByDescending(kv => kv.Value).Take(6).Select(kv => kv.Key + "×" + kv.Value))}；");
            sb.Append($"刚体 {Object.FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None).Length}，碰撞体 {Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None).Length}，");
            sb.Append($"渲染器 {Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Count(r => r.enabled && r.isVisible)} 可见，");
            sb.Append($"粒子 {Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Count(p => p.isPlaying)} 在放，");
            sb.Append($"声音 {Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Count(a => a.isPlaying)} 在响，");
            sb.Append($"状态机 {Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None).Length}，");
            sb.Append($"地形块 {TerrainSystem.All.Count}，刷新点 {(EnemyRespawn.Instance != null ? EnemyRespawn.Instance.SlotCount : -1)}，");
            sb.Append($"物体 {Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).Length}");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ the sentries

        IEnumerator SentryScanTest()
        {
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            mod.DevNoFallDamage = true;
            var scenes = entryGates.Keys.Where(s => s.StartsWith("Ruins1_") || s.StartsWith("Ruins2_") || s.StartsWith("Ruins_") || s == "Abyss_02" || s == "Abyss_03" || s == "Abyss_01").OrderBy(s => s).ToList();
            int found = 0, bad = 0;
            foreach (var scene in scenes)
            {
                if (scene.EndsWith("_boss") || scene.Contains("Elevator")) continue;
                yield return GoTo(scene, EntryGateOf(scene));
                if (GameManager.instance.sceneName != scene) continue;
                pd.isInvincible = true;
                var hero = HeroController.instance;
                var sentries = new List<HealthManager>();
                foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                {
                    if (hm == null || hm.isDead || !hm.gameObject.activeInHierarchy || hm.gameObject.scene.name != scene) continue;
                    string rec = UI.EnemyHover.RecordOf(hm);
                    if (rec == "Sentry" || rec == "SentryFat") sentries.Add(hm);
                }
                Plugin.Log.LogInfo($"[AutoTest] 哨兵 {scene}：{sentries.Count} 只（{string.Join("，", sentries.Select(h => h.name + " " + ((Vector2)h.transform.position).ToString("F1")))}）；刷新点 {EnemyRespawn.Instance.DevReport(scene, out _, out _, out int inWall)}，卡在墙里 {inWall}");
                if (sentries.Count == 0) continue;
                found += sentries.Count;
                if (found <= 2) foreach (var f in sentries[0].GetComponents<PlayMakerFSM>()) DumpFsm(f);
                // The hero stays at the gate first (away), then stands a few steps from a copy (near).
                var away = new Watch(sentries);
                yield return away.Run(5f);
                var target = sentries.FirstOrDefault(h => h != null && h.name.EndsWith("(TK)")) ?? sentries[0];
                if (target != null)
                {
                    var tb = target.GetComponent<Collider2D>().bounds;
                    hero.transform.position = Clear(hero, new Vector2(tb.center.x - 5f, tb.min.y + 0.2f));
                    hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                }
                var near = new Watch(sentries);
                yield return near.Run(8f);
                bad += away.Report($"{scene} 主角在门口", hero) + near.Report($"{scene} 主角在 {(target != null ? target.name : "?")} 旁边", hero);
                Shot(scene, "sentry_near");
                yield return null;
            }
            Plugin.Log.LogInfo($"[AutoTest] 哨兵 合计 {found} 只，可疑 {bad} 处 {(bad == 0 ? "通过" : "不通过")}");
        }

        /// <summary>Some enemies watched for a while: their FSM states, the ground they cover, the sounds near them.</summary>
        sealed class Watch
        {
            readonly List<HealthManager> enemies;
            readonly Dictionary<HealthManager, List<string>> states = new Dictionary<HealthManager, List<string>>();
            readonly Dictionary<HealthManager, Rect> covered = new Dictionary<HealthManager, Rect>();
            readonly Dictionary<HealthManager, Dictionary<string, int>> sounds = new Dictionary<HealthManager, Dictionary<string, int>>();
            float seconds;

            public Watch(List<HealthManager> enemies) { this.enemies = enemies; }

            public IEnumerator Run(float seconds)
            {
                this.seconds = seconds;
                DevFrameProfiler.Recording = false;
                DevFrameProfiler.Sounds.Clear();
                DevFrameProfiler.Recording = true;
                var last = new Dictionary<PlayMakerFSM, string>();
                float until = Time.time + seconds;
                while (Time.time < until)
                {
                    foreach (var hm in enemies)
                    {
                        if (hm == null) continue;
                        if (!states.TryGetValue(hm, out var list)) states[hm] = list = new List<string>();
                        foreach (var f in hm.GetComponents<PlayMakerFSM>())
                        {
                            string s = f.ActiveStateName;
                            if (last.TryGetValue(f, out var was) && was == s) continue;
                            last[f] = s;
                            list.Add($"{f.FsmName}:{s}");
                        }
                        Vector2 p = hm.transform.position;
                        covered[hm] = covered.TryGetValue(hm, out var r) ? Rect.MinMaxRect(Mathf.Min(r.xMin, p.x), Mathf.Min(r.yMin, p.y), Mathf.Max(r.xMax, p.x), Mathf.Max(r.yMax, p.y)) : new Rect(p, Vector2.zero);
                    }
                    yield return null;
                }
                DevFrameProfiler.Recording = false;
                foreach (var s in DevFrameProfiler.Sounds)
                {
                    HealthManager best = null;
                    float bestD = 2.5f;
                    foreach (var hm in enemies)
                    {
                        if (hm == null) continue;
                        float d = Vector2.Distance(s.at, hm.transform.position);
                        if (s.by.Contains(hm.name)) d = 0f;
                        if (d < bestD) { best = hm; bestD = d; }
                    }
                    if (best == null) continue;
                    if (!sounds.TryGetValue(best, out var by)) sounds[best] = by = new Dictionary<string, int>();
                    by[s.clip] = by.TryGetValue(s.clip, out int n) ? n + 1 : 1;
                }
            }

            /// <summary>One line per enemy; the number of suspicious ones (many state changes or sounds while hardly moving).</summary>
            public int Report(string label, HeroController hero)
            {
                int bad = 0;
                foreach (var hm in enemies)
                {
                    if (hm == null) continue;
                    states.TryGetValue(hm, out var list);
                    covered.TryGetValue(hm, out var r);
                    sounds.TryGetValue(hm, out var by);
                    int changes = list?.Count ?? 0, played = by?.Values.Sum() ?? 0;
                    bool stuck = (changes > seconds * 4f || played > seconds * 2f) && r.width < 1.5f;
                    if (stuck) bad++;
                    var col = hm.GetComponent<Collider2D>();
                    bool inWall = col != null && TerraWorld.IsSolidAt(col.bounds.center);
                    string tail = list == null ? "" : string.Join(" → ", list.Skip(Mathf.Max(0, list.Count - 14)));
                    Plugin.Log.LogInfo($"[AutoTest] 哨兵 {label}：{hm.name} 位置 {(Vector2)hm.transform.position:F1} 走过 {r.width:F1}×{r.height:F1}，身体中心在方块里 {inWall}，状态变了 {changes} 次，放音 {played} 次（{(by == null ? "" : string.Join(" ", by.Select(kv => kv.Key + "×" + kv.Value)))}），最近的状态 {tail}{(stuck ? " 可疑" : "")}");
                }
                return bad;
            }
        }

        // ------------------------------------------------------------------ the Dreamers

        /// <summary>
        /// Suite "dreamreturn": coming back from a dream as the game does it (the Knight's Dream Return told
        /// he is returning, in through the room's door_dreamReturn), straight and by way of the Black Egg's
        /// door cutscene; face down, Space now and then: does he get up.
        /// </summary>
        IEnumerator DreamReturnTest()
        {
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            const string room = "Fungus3_archive_02";
            var gm = GameManager.instance;
            foreach (var viaDoor in new[] { false, true })
            {
                yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
                var hero = HeroController.instance;
                var ret = hero.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Dream Return");
                pd.dreamReturnScene = room;
                ret.FsmVariables.GetFsmBool("Dream Returning").Value = true;
                hero.EnterWithoutInput(true);
                gm.BeginSceneTransition(new GameManager.SceneLoadInfo
                {
                    SceneName = viaDoor ? "Cutscene_Boss_Door" : room, EntryGateName = viaDoor ? "door1" : "door_dreamReturn", HeroLeaveDirection = GlobalEnums.GatePosition.unknown,
                    EntryDelay = 0f, WaitForSceneTransitionCameraFade = true,
                    Visualization = viaDoor ? GameManager.SceneLoadVisualizations.Custom : GameManager.SceneLoadVisualizations.Dream,
                });
                var timeline = new List<string>();
                string last = null;
                float t0 = Time.realtimeSinceStartup;
                int taps = 0;
                bool up = false;
                float readyAt = -1f;
                while (Time.realtimeSinceStartup - t0 < 70f)
                {
                    var h = HeroController.SilentInstance;
                    var r = h != null ? h.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Dream Return") : null;
                    var a = GameHooks.Input != null ? GameHooks.Input.inputActions : null;
                    string s = $"{gm.sceneName} 可操作={GameHooks.PlayerHasControl()} 返回={(r != null ? r.ActiveStateName : "?")} 暂停={gm.isPaused} 动作集={(a != null && a.Enabled)}/{(a != null && a.jump.Enabled)} 时间={Time.timeScale:F1}";
                    if (s != last) { timeline.Add($"{Time.realtimeSinceStartup - t0:F1}s {s}"); last = s; }
                    if (r != null && r.ActiveStateName == "Ready" && gm.sceneName == room)
                    {
                        if (readyAt < 0f) readyAt = Time.realtimeSinceStartup;
                        if (Time.realtimeSinceStartup - readyAt > 2f && taps < 6 && Mathf.Repeat(Time.realtimeSinceStartup - readyAt, 2f) < Time.unscaledDeltaTime)
                        {
                            taps++;
                            mod.StartCoroutine(Tap(Key.Space));
                            timeline.Add($"{Time.realtimeSinceStartup - t0:F1}s 按空格（第 {taps} 次）");
                        }
                    }
                    if (readyAt > 0f && gm.sceneName == room && GameHooks.PlayerHasControl()) { up = true; break; }
                    yield return null;
                }
                Plugin.Log.LogInfo($"[AutoTest] 梦里回来 {(viaDoor ? "经过封印门过场" : "直接")}：{string.Join(" | ", timeline)}");
                Plugin.Log.LogInfo($"[AutoTest] 梦里回来 {(viaDoor ? "经过封印门过场" : "直接")} 按了 {taps} 次空格 {(up ? "起来了 通过" : "没起来 不通过")}");
                if (!up)
                {
                    var r = HeroController.instance.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Dream Return");
                    var st = r != null ? System.Array.Find(r.FsmStates, x => x.Name == r.ActiveStateName) : null;
                    var lj = st != null ? System.Array.Find(st.Actions, x => x is HutongGames.PlayMaker.Actions.ListenForJump) as HutongGames.PlayMaker.Actions.ListenForJump : null;
                    if (lj != null)
                    {
                        var its = Traverse.Create(lj).Field("inputHandler").GetValue<InputHandler>();
                        var itsGm = Traverse.Create(lj).Field("gm").GetValue<GameManager>();
                        Plugin.Log.LogInfo($"[AutoTest] 梦里回来 Ready 的 ListenForJump：输入 {(its == null ? "空" : its == GameHooks.Input ? "同一个" : "另一个")}，GameManager {(itsGm == null ? "空/已销毁" : itsGm == gm ? "同一个" : "另一个")}，动作 {(its != null && its.inputActions != null ? its.inputActions.Enabled.ToString() : "?")}");
                    }
                    Shot(gm.sceneName, viaDoor ? "dreamreturn_door" : "dreamreturn_straight");
                    yield return null;
                    if (r != null) r.SetState("Regain Control");
                    yield return new WaitForSeconds(1f);
                }
            }
        }

        /// <summary>
        /// Suite "getup": the Knight face down as he comes back from a dream (his Dream Return FSM put in
        /// Prostrate), then each key a player would press, one at a time: which of them gets him up.
        /// </summary>
        IEnumerator GetUpTest()
        {
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            var hero = HeroController.instance;
            var ret = hero.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Dream Return");
            if (ret == null) { Plugin.Log.LogInfo("[AutoTest] 起身 找不到 Dream Return 不通过"); yield break; }
            int ok = 0, all = 0;
            foreach (var (key, name) in new[] { (Key.Space, "空格"), (Key.W, "W"), (Key.S, "S"), (Key.A, "A"), (Key.D, "D") })
            {
                ret.SetState("Prostrate");
                float t0 = Time.time;
                while (Time.time - t0 < 3f && ret.ActiveStateName != "Ready") yield return null;
                yield return new WaitForSeconds(0.5f);
                string before = ret.ActiveStateName;
                var a = GameHooks.Input.inputActions;
                bool sawPressed = false, sawWas = false;
                tapHeld.Add(key);
                KeysDown(tapHeld.ToArray());
                float t1 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t1 < 0.2f)
                {
                    var act = key == Key.Space ? a.jump : key == Key.W ? a.up : key == Key.S ? a.down : key == Key.A ? a.left : a.right;
                    sawPressed |= act.IsPressed;
                    sawWas |= act.WasPressed;
                    yield return null;
                }
                tapHeld.Remove(key);
                KeysDown(tapHeld.ToArray());
                yield return new WaitForSeconds(1.5f);
                string after = ret.ActiveStateName;
                bool up = before == "Ready" && after != "Ready";
                all++;
                if (up) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 起身 按 {name}：{before} → {after}，动作 IsPressed={sawPressed} WasPressed={sawWas}，可操作 {GameHooks.PlayerHasControl()}{(up ? " 起来了" : " 没起来")}");
                // Back to normal for the next key.
                float t2 = Time.time;
                while (Time.time - t2 < 4f && !GameHooks.PlayerHasControl()) yield return null;
                if (!GameHooks.PlayerHasControl()) { ret.SetState("Regain Control"); yield return new WaitForSeconds(0.5f); }
            }
            Plugin.Log.LogInfo($"[AutoTest] 起身 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }

        IEnumerator DreamersTest(string only = null)
        {
            var pd = PlayerData.instance;
            pd.hasDreamNail = true;
            pd.isInvincible = true;
            pd.defeatedMegaJelly = true;
            pd.killedMegaJellyfish = true;
            pd.killedBlackKnight = true;
            // Monomon brought up already (Quirrel talked to): her dream's door open (dreamwalk goes through the talk).
            pd.summonedMonomon = true;
            mod.DevNoFallDamage = true;
            yield return new WaitForSeconds(1.5f);
            int itemSlot = SlotOf(Items.ItemType.DreamNail);
            if (itemSlot < 0) { Plugin.Log.LogError("[AutoTest] 守梦人 梦钉物品缺失 不通过"); yield break; }
            (mod.Inventory.Slots[0], mod.Inventory.Slots[itemSlot]) = (mod.Inventory.Slots[itemSlot], mod.Inventory.Slots[0]);
            mod.Inventory.Selected = 0;
            int ok = 0;
            foreach (var (room, dream, who) in new[] { ("Fungus3_archive_02", "Dream_Guardian_Monomon", "莫诺蒙"), ("Ruins2_Watcher_Room", "Dream_Guardian_Lurien", "卢瑞恩"), ("Deepnest_Spider_Town", "Dream_Guardian_Hegemol", "赫拉") })
            {
                if (only != null && only != room) continue;
                yield return GoTo(room, EntryGateOf(room));
                pd.isInvincible = true;
                mod.Inventory.Selected = SlotOf(Items.ItemType.DreamNail);
                var hero = HeroController.instance;
                PlayMakerFSM entry = null;
                foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (f.gameObject.scene.name != room) continue;
                    bool toDream = false;
                    foreach (var v in f.FsmVariables.StringVariables) if (v.Value == dream) toDream = true;
                    foreach (var state in f.FsmStates)
                        foreach (var action in state.Actions)
                            foreach (var field in action.GetType().GetFields())
                                if (field.GetValue(action) is HutongGames.PlayMaker.FsmString fs && fs.Value == dream) toDream = true;
                    string path = PathOf(f.transform);
                    string lower = path.ToLowerInvariant();
                    if (toDream || lower.Contains("dreamer") || lower.Contains("dream enter") || lower.Contains(who == "赫拉" ? "hegemol" : who == "卢瑞恩" ? "lurien" : "monomon"))
                    {
                        Plugin.Log.LogInfo($"[AutoTest] 守梦人 {who} 候选 {path} FSM={f.FsmName} 状态={f.ActiveStateName} 激活={f.gameObject.activeInHierarchy} 进梦={toDream}");
                        DumpFsm(f);
                    }
                    if (toDream && f.gameObject.activeInHierarchy && entry == null) entry = f;
                }
                if (entry == null) { Plugin.Log.LogInfo($"[AutoTest] 守梦人 {who} 找不到进 {dream} 的状态机 不通过"); continue; }
                var col = entry.GetComponent<Collider2D>() ?? entry.GetComponentInChildren<Collider2D>();
                Vector2 at = col != null ? (Vector2)col.bounds.center : (Vector2)entry.transform.position;
                var colliders = new List<string>();
                foreach (var c in Physics2D.OverlapCircleAll(at, 3f)) colliders.Add($"{PathOf(c.transform)}[{LayerMask.LayerToName(c.gameObject.layer)}{(c.isTrigger ? ",触发" : "")}]");
                Plugin.Log.LogInfo($"[AutoTest] 守梦人 {who} 入口 {PathOf(entry.transform)} 在 {at:F1}，周围的碰撞体：{string.Join(" ", colliders)}");
                foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                    if (hm != null && !hm.isDead && Vector2.Distance(hm.transform.position, at) < 20f) Kill(hm);
                bool entered = false;
                foreach (float side in new[] { -1f, 1f })
                {
                    hero.transform.position = Clear(hero, at + new Vector2(side * 1.8f, 0f));
                    hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                    if (side < 0f) hero.FaceRight(); else hero.FaceLeft();
                    yield return new WaitForSeconds(1.5f);
                    var dfsm = DreamFsm(hero);
                    Plugin.Log.LogInfo($"[AutoTest] 守梦人 {who} 挥前 主角={(Vector2)hero.transform.position:F1} 目标={at:F1} 地面={hero.cState.onGround} 入口状态={entry.ActiveStateName} 拿着梦钉={Items.DreamNailItem.Holding()} CanDreamNail={hero.CanDreamNail()}");
                    var states = new List<string>();
                    string last = null;
                    Items.DreamNailItem.DevHold = true;
                    float t0 = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - t0 < 2.6f)
                    {
                        string now = dfsm != null ? dfsm.ActiveStateName : "?";
                        if (now != last) { states.Add($"{Time.realtimeSinceStartup - t0:F2}s {now}"); last = now; }
                        if (Time.realtimeSinceStartup - t0 > 2.2f) Items.DreamNailItem.DevHold = false;
                        yield return null;
                    }
                    Items.DreamNailItem.DevHold = null;
                    float until = Time.realtimeSinceStartup + 25f;
                    var entryStates = new List<string>();
                    string lastEntry = null;
                    while (Time.realtimeSinceStartup < until && (GameManager.instance.sceneName != dream || !GameHooks.PlayerHasControl()))
                    {
                        if (entry != null && entry.ActiveStateName != lastEntry) { lastEntry = entry.ActiveStateName; entryStates.Add(lastEntry); }
                        if (GameManager.instance.sceneName == room && Time.realtimeSinceStartup - t0 > 8f && entryStates.Count <= 1) break;
                        yield return null;
                    }
                    entered = GameManager.instance.sceneName == dream;
                    Plugin.Log.LogInfo($"[AutoTest] 守梦人 {who} 从{(side < 0f ? "左" : "右")}边挥梦钉：梦钉 {string.Join(" → ", states)}；入口 {string.Join(" → ", entryStates)}；场景 {GameManager.instance.sceneName}{(entered ? " 进了梦" : "")}");
                    if (entered) break;
                }
                if (!entered) { Shot(room, "dreamer_fail"); yield return null; Plugin.Log.LogInfo($"[AutoTest] {who} 进梦 不通过"); continue; }
                yield return new WaitForSeconds(2f);
                Plugin.Log.LogInfo($"[AutoTest] {who} 进梦 通过");
                yield return InDream(who, dream, room);
                if (DevDreamerDone(who)) ok++;
            }
            Plugin.Log.LogInfo($"[AutoTest] 守梦人 打败 {ok}/3");
        }

        static bool DevDreamerDone(string who)
        {
            var pd = PlayerData.instance;
            return who == "莫诺蒙" ? pd.monomonDefeated : who == "卢瑞恩" ? pd.lurienDefeated : pd.hegemolDefeated;
        }

        /// <summary>
        /// In a Dreamer's dream, as the player goes: on to the Dreamer's platform, a sword's swings
        /// until it is struck down, then the Dream Nail in hand and its button held (the focus) until
        /// the Dreamer is drained and the game records it.
        /// </summary>
        IEnumerator InDream(string who, string dream, string room)
        {
            var gm = GameManager.instance;
            var hero = HeroController.instance;
            var pd = PlayerData.instance;
            var inv = mod.Inventory;
            pd.isInvincible = true;
            float t0 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t0 < 15f && (!GameHooks.PlayerHasControl() || !hero.cState.onGround)) yield return null;
            Shot(dream, "dreamer_in_dream");
            yield return null;
            PlayMakerFSM control = null;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                if (f.gameObject.scene.name == dream && f.gameObject.name == "Dreamer NPC" && f.FsmName == "Control") control = f;
            var spell = hero.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Spell Control");
            if (spell != null && who == "莫诺蒙") DumpFsm(spell);
            if (control == null) { Plugin.Log.LogInfo($"[AutoTest] 守梦人 {who} 梦里找不到 Dreamer NPC 不通过"); yield break; }
            var col = control.GetComponent<Collider2D>();
            var b = col.bounds;
            // On its platform, a sword's reach to its left.
            hero.transform.position = Clear(hero, new Vector2(b.min.x - 1.2f, b.min.y + 0.2f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            hero.FaceRight();
            float t1 = Time.time;
            while (Time.time - t1 < 4f && !hero.cState.onGround) yield return null;
            yield return new WaitForSeconds(0.5f);
            Plugin.Log.LogInfo($"[AutoTest] 守梦人 {who} 站到 {(Vector2)hero.transform.position:F1}，守梦人 {(Vector2)b.min:F1}..{(Vector2)b.max:F1}，地面 {hero.cState.onGround}，可操作 {GameHooks.PlayerHasControl()}，状态 {control.ActiveStateName}");

            // Struck down with a sword (the nail's tag on its swing).
            var states = new List<string>();
            string last = null;
            void Note(float since)
            {
                string s = $"{control.ActiveStateName}/{(spell != null ? spell.ActiveStateName : "?")}";
                if (s != last) { states.Add($"{Time.realtimeSinceStartup - since:F1}s {s}"); last = s; }
            }
            int swordSlot = 1;
            inv.Slots[swordSlot] = Items.ItemStack.Of(Items.ItemType.CopperBroadsword);
            inv.Selected = swordSlot;
            yield return new WaitForSeconds(0.3f);
            var covered = new List<string>();
            Items.MeleeHits.DebugLog = s => { if (covered.Count < 20) covered.Add(s); };
            int swings = 0;
            float t2 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t2 < 8f && !Items.DreamNailItem.DreamerWounded())
            {
                Note(t2);
                if (GameHooks.PlayerHasControl() && mod.DevReady && !mod.IsUsingItem) { mod.DevUseSelected(col.bounds.center); swings++; }
                yield return null;
            }
            Items.MeleeHits.DebugLog = null;
            Plugin.Log.LogInfo($"[AutoTest] 守梦人 {who} 用铜阔剑挥了 {swings} 下：{string.Join(" → ", states)}；挥砍覆盖 {string.Join("；", covered)}；守梦人的层 {LayerMask.LayerToName(col.gameObject.layer)}({col.gameObject.layer})，打伤了 {Items.DreamNailItem.DreamerWounded()}");
            if (!Items.DreamNailItem.DreamerWounded())
            {
                inv.Slots[swordSlot] = Items.ItemStack.Of(Items.ItemType.KnightNail);
                yield return new WaitForSeconds(0.3f);
                states.Clear();
                last = null;
                int s0 = Items.NailItem.DevSwings;
                Items.NailItem.DevHold = true;
                float t2b = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t2b < 10f && !Items.DreamNailItem.DreamerWounded())
                {
                    Note(t2b);
                    yield return null;
                }
                Items.NailItem.DevHold = null;
                Plugin.Log.LogInfo($"[AutoTest] 守梦人 {who} 用骨钉挥了 {Items.NailItem.DevSwings - s0} 下：{string.Join(" → ", states)}；打伤了 {Items.DreamNailItem.DreamerWounded()}");
            }
            if (!Items.DreamNailItem.DreamerWounded()) { Shot(dream, "dreamer_not_wounded"); yield return null; inv.Selected = SlotOf(Items.ItemType.DreamNail); yield break; }

            // The focus: first with the sword still in hand (nothing), then the Dream Nail's button held.
            yield return new WaitForSeconds(1f);
            Items.DreamNailItem.DevHold = true;
            yield return new WaitForSeconds(1.5f);
            string withSword = control.ActiveStateName;
            Items.DreamNailItem.DevHold = false;
            yield return new WaitForSeconds(0.5f);
            int nail = SlotOf(Items.ItemType.DreamNail);
            inv.Selected = nail;
            yield return new WaitForSeconds(0.3f);
            string hint = UI.CenterNotice.DevLast;
            states.Clear();
            last = null;
            Items.DreamNailItem.DevHold = true;
            float t3 = Time.realtimeSinceStartup;
            bool shotFocus = false;
            while (Time.realtimeSinceStartup - t3 < 25f && !DevDreamerDone(who))
            {
                Note(t3);
                if (!shotFocus && control.ActiveStateName == "Flashing") { shotFocus = true; Shot(dream, "dreamer_focus"); }
                yield return null;
            }
            Items.DreamNailItem.DevHold = null;
            bool done = DevDreamerDone(who);
            Plugin.Log.LogInfo($"[AutoTest] 守梦人 {who} 拿着剑按住鼠标后：{withSword}；提示「{hint}」；拿梦之钉按住聚集：{string.Join(" → ", states)}；打败 {done}");
            // The rest is the game's (the cinematic, the mask, the door scene, waking up in the room): no key pressed
            // for the first 90 seconds, as a player watching; then Space now and then. Each change of scene, control or
            // the hero's state on the way.
            var timeline = new List<string>();
            string lastT = null;
            bool dumped = false;
            float readyAt = -1f;
            int taps = 0;
            float t4 = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - t4 < 120f && (gm.sceneName != room || !GameHooks.PlayerHasControl()))
            {
                var h = HeroController.SilentInstance;
                var ret = h != null ? h.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Dream Return") : null;
                string s = $"{gm.sceneName} 可操作={GameHooks.PlayerHasControl()} {(h != null ? h.hero_state.ToString() : "?")} 放手={(h != null && h.controlReqlinquished)} 返回={(ret != null ? ret.ActiveStateName : "?")}";
                if (s != lastT) { timeline.Add($"{Time.realtimeSinceStartup - t4:F1}s {s}"); lastT = s; }
                if (!dumped && ret != null && gm.sceneName == room) { dumped = true; if (who == "莫诺蒙") DumpFsm(ret); }
                if (ret != null && gm.sceneName == room && (ret.ActiveStateName == "Ready" || ret.ActiveStateName == "Ready 2"))
                {
                    if (readyAt < 0f) readyAt = Time.realtimeSinceStartup;
                    if (Time.realtimeSinceStartup - readyAt > 3f && taps < 6 && Mathf.Repeat(Time.realtimeSinceStartup - readyAt, 2f) < Time.unscaledDeltaTime)
                    {
                        taps++;
                        var a = GameHooks.Input != null ? GameHooks.Input.inputActions : null;
                        var st = System.Array.Find(ret.FsmStates, x => x.Name == ret.ActiveStateName);
                        var lj = st != null ? System.Array.Find(st.Actions, x => x is HutongGames.PlayMaker.Actions.ListenForJump) as HutongGames.PlayMaker.Actions.ListenForJump : null;
                        var its = lj != null ? Traverse.Create(lj).Field("inputHandler").GetValue<InputHandler>() : null;
                        var itsGm = lj != null ? Traverse.Create(lj).Field("gm").GetValue<GameManager>() : null;
                        timeline.Add($"{Time.realtimeSinceStartup - t4:F1}s 按空格（第 {taps} 次；按住的键 {string.Join(",", tapHeld)}；动作集 {(a != null && a.Enabled)}/{(a != null && a.jump.Enabled)}；监听的输入 {(its == null ? "空" : its == GameHooks.Input ? "同一个" : "另一个")}，GameManager {(itsGm == null ? "空" : itsGm == gm ? "同一个" : "另一个")} 暂停 {(itsGm != null && itsGm.isPaused)}；状态机启用 {ret.enabled}）");
                        mod.StartCoroutine(Tap(Key.Space));
                    }
                }
                yield return null;
            }
            Plugin.Log.LogInfo($"[AutoTest] 守梦人 {who} 之后：{string.Join(" | ", timeline)}");
            Plugin.Log.LogInfo($"[AutoTest] 守梦人 {who} 之后：在 {gm.sceneName}，可操作 {GameHooks.PlayerHasControl()}，guardiansDefeated {pd.guardiansDefeated} {(done && GameHooks.PlayerHasControl() ? "通过" : "不通过")}");
            Shot(gm.sceneName, "dreamer_after");
            yield return null;
        }

        /// <summary>
        /// The Dreamers' rooms entered as the player enters them: Monomon's dropped into from the
        /// Archives above (and Quirrel talked to with W and Space, which brings her up), Lurien's
        /// risen into from the Watcher Knights' hall below; then the Dream Nail swung at the dreamer.
        /// </summary>
        IEnumerator DreamersWalkInTest()
        {
            var pd = PlayerData.instance;
            pd.hasDreamNail = true;
            pd.isInvincible = true;
            pd.defeatedMegaJelly = true;
            pd.killedMegaJellyfish = true;
            pd.killedBlackKnight = true;
            pd.summonedMonomon = false;
            mod.DevNoFallDamage = true;
            yield return new WaitForSeconds(1f);
            int itemSlot = SlotOf(Items.ItemType.DreamNail);
            if (itemSlot < 0) { Plugin.Log.LogError("[AutoTest] 守梦人走进 梦钉物品缺失 不通过"); yield break; }
            if (itemSlot != 0) (mod.Inventory.Slots[0], mod.Inventory.Slots[itemSlot]) = (mod.Inventory.Slots[itemSlot], mod.Inventory.Slots[0]);
            mod.Inventory.Selected = 0;
            int ok = 0;
            var gm = GameManager.instance;
            foreach (var (room, dream, who, from, fromGate, up) in new[] { ("Fungus3_archive_02", "Dream_Guardian_Monomon", "莫诺蒙", "Fungus3_archive", "bot1", false), ("Ruins2_Watcher_Room", "Dream_Guardian_Lurien", "卢瑞恩", "Ruins2_03", "top1", true) })
            {
                yield return GoTo(from, fromGate);
                pd.isInvincible = true;
                var hero = HeroController.instance;
                TransitionPoint gate = null;
                foreach (var tp in Object.FindObjectsByType<TransitionPoint>(FindObjectsSortMode.None))
                    if (tp.gameObject.scene.name == from && tp.name == fromGate) gate = tp;
                if (gate == null) { Plugin.Log.LogInfo($"[AutoTest] 守梦人走进 {who} {from} 里找不到 {fromGate} 不通过"); continue; }
                var gb = gate.GetComponent<Collider2D>().bounds;
                yield return new WaitForSeconds(2f);
                var rb = hero.GetComponent<Rigidbody2D>();
                if (up)
                {
                    hero.transform.position = Clear(hero, new Vector2(gb.center.x, gb.min.y - 3f));
                    yield return Lift(hero, from);
                }
                else
                {
                    hero.transform.position = new Vector3(gb.center.x, gb.max.y + 1.5f, hero.transform.position.z);
                    rb.linearVelocity = Vector2.zero;
                    float t0 = Time.time;
                    while (gm.sceneName == from && Time.time - t0 < 8f) yield return null;
                }
                float t1 = Time.time;
                while (Time.time - t1 < 6f && !hero.cState.onGround) yield return null;
                yield return new WaitForSeconds(2f);
                bool walked = gm.sceneName == room;
                Plugin.Log.LogInfo($"[AutoTest] 守梦人走进 {who} 从 {from} {(up ? "升" : "掉")}进 {gm.sceneName}{(walked ? "" : " 没进去")}，主角 {(Vector2)hero.transform.position:F1}");
                if (!walked) continue;

                PlayMakerFSM control = null, appear = null;
                foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (f.gameObject.scene.name != room || f.gameObject.name != "Dream Enter") continue;
                    if (f.FsmName == "Control") control = f;
                    if (f.FsmName == "Appear") appear = f;
                }
                if (control == null) { Plugin.Log.LogInfo($"[AutoTest] 守梦人走进 {who} 找不到 Dream Enter 不通过"); continue; }
                Collider2D Trigger() => control != null ? control.GetComponent<Collider2D>() : null;
                Plugin.Log.LogInfo($"[AutoTest] 守梦人走进 {who} Dream Enter：Control={control.ActiveStateName} 启用 {control.enabled}，Appear={(appear != null ? appear.ActiveStateName : "无")}，碰撞体启用 {(Trigger() != null && Trigger().enabled)}，summonedMonomon={pd.summonedMonomon}");

                if (who == "莫诺蒙")
                {
                    // Her tank inspected first (W at it, Space through the lines): Quirrel leaps in.
                    PlayMakerFSM inspect = null;
                    foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                        if (f.gameObject.scene.name == room && f.gameObject.name == "Inspect Region" && f.FsmName == "npc_control") inspect = f;
                    if (inspect != null)
                    {
                        var ib = inspect.GetComponent<Collider2D>().bounds;
                        hero.transform.position = Clear(hero, new Vector2(ib.center.x, ib.min.y + 0.3f));
                        rb.linearVelocity = Vector2.zero;
                        float ti = Time.time;
                        while (Time.time - ti < 3f && !hero.cState.onGround) yield return null;
                        yield return new WaitForSeconds(1.2f);
                        var seen = new List<string>();
                        string lastI = null;
                        string inRange = inspect.ActiveStateName;
                        yield return Tap(Key.W);
                        float t2 = Time.realtimeSinceStartup, nextTap = t2 + 1.5f;
                        while (Time.realtimeSinceStartup - t2 < 15f && (!GameHooks.PlayerHasControl() || Time.realtimeSinceStartup - t2 < 2f))
                        {
                            if (inspect.ActiveStateName != lastI) { lastI = inspect.ActiveStateName; seen.Add($"{Time.realtimeSinceStartup - t2:F1}s {lastI}"); }
                            if (Time.realtimeSinceStartup > nextTap) { nextTap = Time.realtimeSinceStartup + 0.8f; mod.StartCoroutine(Tap(Key.Space)); }
                            yield return null;
                        }
                        yield return new WaitForSeconds(3f);
                        Plugin.Log.LogInfo($"[AutoTest] 守梦人走进 莫诺蒙 检查水缸：按 W 前 {inRange}，之后 {string.Join(" → ", seen)}，可操作 {GameHooks.PlayerHasControl()}");
                    }
                    // Quirrel by her tank: W to talk, Space through his lines.
                    PlayMakerFSM q = null, convo = null;
                    foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                        if (f.gameObject.scene.name == room && f.gameObject.name == "Quirrel")
                        {
                            if (f.FsmName == "npc_control") q = f;
                            if (f.FsmName == "Conversation Control") convo = f;
                        }
                    if (q != null)
                    {
                        hero.transform.position = Clear(hero, (Vector2)q.transform.position + Vector2.left * 1.2f);
                        rb.linearVelocity = Vector2.zero;
                        hero.FaceRight();
                        yield return new WaitForSeconds(1.2f);
                        var talk = new List<string>();
                        string lastQ = null;
                        yield return Tap(Key.W);
                        float t2 = Time.realtimeSinceStartup;
                        float nextTap = t2 + 1.5f;
                        while (Time.realtimeSinceStartup - t2 < 30f && !pd.summonedMonomon)
                        {
                            string s = $"{q.ActiveStateName}/{(convo != null ? convo.ActiveStateName : "?")}";
                            if (s != lastQ) { talk.Add($"{Time.realtimeSinceStartup - t2:F1}s {s}"); lastQ = s; }
                            if (Time.realtimeSinceStartup > nextTap) { nextTap = Time.realtimeSinceStartup + 0.8f; mod.StartCoroutine(Tap(Key.Space)); }
                            yield return null;
                        }
                        float t3 = Time.realtimeSinceStartup;
                        while (Time.realtimeSinceStartup - t3 < 10f && !GameHooks.PlayerHasControl())
                        {
                            string s = $"{q.ActiveStateName}/{(convo != null ? convo.ActiveStateName : "?")}";
                            if (s != lastQ) { talk.Add($"{Time.realtimeSinceStartup - t2:F1}s {s}"); lastQ = s; }
                            if (Time.realtimeSinceStartup > nextTap) { nextTap = Time.realtimeSinceStartup + 0.8f; mod.StartCoroutine(Tap(Key.Space)); }
                            yield return null;
                        }
                        yield return new WaitForSeconds(4f);
                        Plugin.Log.LogInfo($"[AutoTest] 守梦人走进 莫诺蒙 跟奎若对话：{string.Join(" → ", talk)}；summonedMonomon={pd.summonedMonomon}，Appear={(appear != null ? appear.ActiveStateName : "无")}，碰撞体启用 {(Trigger() != null && Trigger().enabled)}，可操作 {GameHooks.PlayerHasControl()}");
                    }
                    if (!pd.summonedMonomon) { Plugin.Log.LogInfo("[AutoTest] 守梦人走进 莫诺蒙 对话没唤出莫诺蒙 不通过"); continue; }
                }

                var tb = Trigger() != null ? Trigger().bounds : new Bounds(control.transform.position, Vector3.one);
                bool entered = false;
                foreach (float side in new[] { -1f, 1f })
                {
                    hero.transform.position = Clear(hero, new Vector2(tb.center.x + side * (tb.extents.x + 1.4f), tb.center.y));
                    rb.linearVelocity = Vector2.zero;
                    if (side < 0f) hero.FaceRight(); else hero.FaceLeft();
                    float t4 = Time.time;
                    while (Time.time - t4 < 3f && !hero.cState.onGround) yield return null;
                    yield return new WaitForSeconds(0.6f);
                    Plugin.Log.LogInfo($"[AutoTest] 守梦人走进 {who} 挥前 主角={(Vector2)hero.transform.position:F1} 入口框 {(Vector2)tb.min:F1}..{(Vector2)tb.max:F1} 地面={hero.cState.onGround} Control={control.ActiveStateName}");
                    Items.DreamNailItem.DevHold = true;
                    float t5 = Time.realtimeSinceStartup;
                    while (Time.realtimeSinceStartup - t5 < 2.4f)
                    {
                        if (Time.realtimeSinceStartup - t5 > 2.0f) Items.DreamNailItem.DevHold = false;
                        yield return null;
                    }
                    Items.DreamNailItem.DevHold = null;
                    float until = Time.realtimeSinceStartup + 20f;
                    while (Time.realtimeSinceStartup < until && gm.sceneName != dream)
                    {
                        if (control != null && control.ActiveStateName == "Idle" && Time.realtimeSinceStartup - t5 > 4f) break;
                        yield return null;
                    }
                    while (Time.realtimeSinceStartup < until && gm.sceneName == dream && !GameHooks.PlayerHasControl()) yield return null;
                    entered = gm.sceneName == dream;
                    Plugin.Log.LogInfo($"[AutoTest] 守梦人走进 {who} 从{(side < 0f ? "左" : "右")}边挥梦钉：场景 {gm.sceneName}{(entered ? " 进了梦" : "")}");
                    if (entered) break;
                }
                if (entered) ok++;
                else { Shot(room, "dreamer_walkin_fail"); yield return null; }
                Plugin.Log.LogInfo($"[AutoTest] 守梦人走进 {who} {(entered ? "通过" : "不通过")}");
                if (entered) yield return InDream(who, dream, room);
            }
            Plugin.Log.LogInfo($"[AutoTest] 守梦人走进 {ok}/2");
        }
    }
}
