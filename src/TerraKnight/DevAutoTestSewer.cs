using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "sewer"): the way down from the Relic Seeker's room (Ruins1_05b, bot1) to the Royal Waterways:
    /// what stands over the gate (colliders, state machines, the grid's cells), then the Knight dropped onto it,
    /// with the manhole shut and opened (the Simple Key's). Suite "wormscan": the rooms between Queen's Gardens and
    /// Deepnest watched for a while, every enemy in them counted each second with the respawn slots' states.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator SewerTest()
        {
            var pd = PlayerData.instance;
            foreach (bool open in new[] { false, true })
            {
                pd.SetBool("openedWaterwaysManhole", open);
                if (open) pd.SetInt("simpleKeys", Mathf.Max(1, pd.GetInt("simpleKeys")));
                yield return GoTo("Ruins1_05b", EntryGateOf("Ruins1_05b"));
                pd.isInvincible = true;
                var gate = Object.FindObjectsByType<TransitionPoint>(FindObjectsSortMode.None).FirstOrDefault(t => t.name == "bot1" && t.gameObject.scene.name == "Ruins1_05b");
                if (gate == null)
                {
                    Plugin.Log.LogInfo("[AutoTest] 下水道 找不到 bot1");
                    yield break;
                }
                var gb = gate.GetComponent<Collider2D>().bounds;
                var sb = new StringBuilder();
                sb.Append($"井盖{(open ? "开着" : "关着")}，bot1 {(Vector2)gb.min:F1}..{(Vector2)gb.max:F1}");
                var near = new Bounds(gb.center, gb.size + new Vector3(14f, 14f, 10f));
                foreach (var c in Object.FindObjectsByType<Collider2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (c.gameObject.scene.name != "Ruins1_05b" || !c.bounds.Intersects(near)) continue;
                    var fsms = string.Join(",", c.GetComponentsInParent<PlayMakerFSM>(true).Select(f => f.FsmName + ":" + f.ActiveStateName));
                    sb.Append($"\n  碰撞体 {PathOf(c.transform)} 层 {LayerMask.LayerToName(c.gameObject.layer)} 启用 {c.enabled && c.gameObject.activeInHierarchy} 触发 {c.isTrigger} {(Vector2)c.bounds.min:F1}..{(Vector2)c.bounds.max:F1} 状态机 [{fsms}]");
                }
                foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (f.gameObject.scene.name != "Ruins1_05b" || Vector2.Distance(f.transform.position, gb.center) > 12f) continue;
                    sb.Append($"\n  状态机 {PathOf(f.transform)} '{f.FsmName}' 状态 {f.ActiveStateName} 激活 {f.gameObject.activeInHierarchy}");
                }
                // The grid's cells over the gate: a column up from its middle and either side.
                var mid = CellGrid.WorldToCell(new Vector2(gb.center.x, gb.max.y));
                for (int dx = -3; dx <= 3; dx += 3)
                {
                    sb.Append($"\n  格子 x{dx:+0;-0;0}：");
                    for (int dy = -2; dy <= 16; dy++) sb.Append(TerraWorld.IsSolid(mid.x + dx, mid.y + dy) ? '#' : '.');
                }
                Plugin.Log.LogInfo("[AutoTest] 下水道 " + sb);
                // Down onto it.
                var hero = HeroController.instance;
                hero.transform.position = new Vector2(gb.center.x, gb.max.y + 6f);
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                yield return new WaitForSeconds(0.5f);
                Shot("Ruins1_05b", "sewer_" + (open ? "open" : "shut"));
                float until = Time.time + 6f;
                while (GameManager.instance.sceneName == "Ruins1_05b" && Time.time < until) yield return null;
                Plugin.Log.LogInfo($"[AutoTest] 下水道 井盖{(open ? "开着" : "关着")}：放到 bot1 上方 6 格，6 秒后在 {GameManager.instance.sceneName} {(Vector2)HeroController.instance.transform.position:F1} 站地 {HeroController.instance.cState.onGround}");
                if (GameManager.instance.sceneName == "Ruins1_05b") Shot("Ruins1_05b", "sewer_" + (open ? "open" : "shut") + "_after");
                yield return new WaitForSeconds(2f);
            }
            // Shut, with a Simple Key: stand on it and press W (the mod's up), Space through any prompt.
            pd.SetBool("openedWaterwaysManhole", false);
            pd.SetInt("simpleKeys", 1);
            yield return GoTo("Ruins1_05b", EntryGateOf("Ruins1_05b"));
            pd.isInvincible = true;
            PlayMakerFSM manhole = null;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (f.gameObject.scene.name == "Ruins1_05b" && f.FsmName == "Manhole Control") manhole = f;
            if (manhole == null) { Plugin.Log.LogInfo("[AutoTest] 下水道 找不到井盖的状态机"); yield break; }
            DumpFsms(manhole.transform);
            // Whoever opens it: every machine of the room naming the manhole's event or the Simple Key.
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (f.gameObject.scene.name != "Ruins1_05b" || f == manhole) continue;
                bool hit = f.FsmEvents.Any(e => e.Name.ToUpperInvariant().Contains("MANHOLE"));
                foreach (var s in f.FsmStates)
                    foreach (var act in s.Actions)
                        foreach (var field in act.GetType().GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
                        {
                            var v = field.GetValue(act);
                            string text = v is HutongGames.PlayMaker.FsmString fs ? fs.Value : v is string str ? str : v is HutongGames.PlayMaker.FsmEvent ev ? ev.Name : null;
                            if (text != null && (text.ToUpperInvariant().Contains("MANHOLE") || text.Contains("simpleKey"))) hit = true;
                        }
                if (hit)
                {
                    Plugin.Log.LogInfo($"[AutoTest] 下水道 开井盖的 {PathOf(f.transform)} '{f.FsmName}' {f.ActiveStateName} 激活 {f.gameObject.activeInHierarchy} 在 {(Vector2)f.transform.position:F1}");
                    DumpFsm(f);
                }
            }
            foreach (var f in manhole.GetComponentsInChildren<PlayMakerFSM>(true))
                Plugin.Log.LogInfo($"[AutoTest] 下水道 井盖下的状态机 {PathOf(f.transform)} '{f.FsmName}' {f.ActiveStateName} 激活 {f.gameObject.activeInHierarchy}");
            var h = HeroController.instance;
            var mb = manhole.GetComponent<Collider2D>().bounds;
            // The key goes into the machine beside it (Waterways Machine, its Conversation Control): the Knight by it, W.
            PlayMakerFSM machine = null;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (f.gameObject.scene.name == "Ruins1_05b" && f.gameObject.name == "Waterways Machine" && f.FsmName == "Conversation Control") machine = f;
            foreach (var f in machine != null ? machine.GetComponents<PlayMakerFSM>() : new PlayMakerFSM[0])
                Plugin.Log.LogInfo($"[AutoTest] 下水道 机器上的状态机 '{f.FsmName}' {f.ActiveStateName}");
            foreach (var c in machine != null ? machine.GetComponentsInChildren<Collider2D>(true) : new Collider2D[0])
                Plugin.Log.LogInfo($"[AutoTest] 下水道 机器的碰撞体 {PathOf(c.transform)} 触发 {c.isTrigger} 启用 {c.enabled && c.gameObject.activeInHierarchy} {(Vector2)c.bounds.min:F1}..{(Vector2)c.bounds.max:F1}");
            var at = machine != null ? new Vector2(machine.transform.position.x + 1.2f, mb.max.y + 0.8f) : new Vector2(mb.center.x, mb.max.y + 0.8f);
            h.transform.position = Clear(h, at);
            h.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            float st = Time.time + 3f;
            while (Time.time < st && !h.cState.onGround) yield return null;
            yield return new WaitForSeconds(0.8f);
            string before = manhole.ActiveStateName;
            var seen = new List<string>();
            string lastS = null;
            for (float held = 0f; held < 0.35f; held += Time.unscaledDeltaTime)
            {
                KeysDown(UnityEngine.InputSystem.Key.W);
                yield return null;
            }
            KeysDown();
            float t0 = Time.realtimeSinceStartup, next = t0 + 1.2f;
            while (Time.realtimeSinceStartup - t0 < 10f && GameManager.instance.sceneName == "Ruins1_05b")
            {
                string now = (manhole != null ? manhole.ActiveStateName : "-") + "/" + (machine != null ? machine.ActiveStateName : "-");
                if (now != lastS) { lastS = now; seen.Add($"{Time.realtimeSinceStartup - t0:F1}s {lastS}"); }
                if (Time.realtimeSinceStartup > next) { next = Time.realtimeSinceStartup + 0.8f; mod.StartCoroutine(Tap(UnityEngine.InputSystem.Key.Space)); }
                yield return null;
            }
            Plugin.Log.LogInfo($"[AutoTest] 下水道 带钥匙站在井盖上按 W：之前 {before}，之后 {string.Join(" → ", seen)}；钥匙 {pd.GetInt("simpleKeys")}，井盖开了 {pd.GetBool("openedWaterwaysManhole")}，现在在 {GameManager.instance.sceneName} {(Vector2)HeroController.instance.transform.position:F1}，能查看 {HeroController.instance.CanInspect()}");
            if (GameManager.instance.sceneName == "Ruins1_05b") Shot("Ruins1_05b", "sewer_key");
            // Opened now, in this same visit: onto it, down into the Waterways.
            if (GameManager.instance.sceneName == "Ruins1_05b" && pd.GetBool("openedWaterwaysManhole"))
            {
                yield return new WaitForSeconds(1f);
                HeroController.instance.transform.position = new Vector2(mb.center.x, mb.max.y + 3f);
                float fall = Time.time + 8f;
                while (GameManager.instance.sceneName == "Ruins1_05b" && Time.time < fall) yield return null;
                Plugin.Log.LogInfo($"[AutoTest] 下水道 刚打开就跳下去：现在在 {GameManager.instance.sceneName} {(Vector2)HeroController.instance.transform.position:F1} {(GameManager.instance.sceneName == "Waterways_01" ? "通过" : "不通过")}");
            }
            pd.SetBool("openedWaterwaysManhole", false);
        }

        IEnumerator WormScanTest()
        {
            var pd = PlayerData.instance;
            foreach (var scene in new[] { "Fungus3_08", "Deepnest_43", "Deepnest_42", "Deepnest_39", "Deepnest_41", "Fungus3_39", "Deepnest_01", "Deepnest_44", "Deepnest_26" })
            {
                yield return GoTo(scene, EntryGateOf(scene));
                pd.isInvincible = true;
                if (GameManager.instance.sceneName != scene)
                {
                    Plugin.Log.LogInfo($"[AutoTest] 白虫 {scene}：没进去");
                    continue;
                }
                var resp = EnemyRespawn.Instance;
                int r0 = EnemyRespawn.DevRespawned, l0 = EnemyRespawn.DevLost, d0 = EnemyRespawn.DevDoubled;
                var seen = new Dictionary<string, int>();
                var spawnedIds = new HashSet<int>();
                string last = "";
                for (int t = 0; t < 30; t++)
                {
                    var counts = new Dictionary<string, int>();
                    foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                    {
                        if (hm.gameObject.scene.name != scene || hm.isDead || !hm.gameObject.activeInHierarchy) continue;
                        string n = BareName(hm.name);
                        counts[n] = (counts.TryGetValue(n, out int c) ? c : 0) + 1;
                        if (spawnedIds.Add(hm.GetInstanceID())) seen[n] = (seen.TryGetValue(n, out int s) ? s : 0) + 1;
                    }
                    string now = string.Join(" ", counts.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}×{kv.Value}"));
                    if (now != last || t % 10 == 0)
                        Plugin.Log.LogInfo($"[AutoTest] 白虫 {scene} {t}s：{now}；刷新槽 {resp.DevReport(scene, out _, out _, out int inWall)}（卡墙 {inWall}）；累计重刷 {EnemyRespawn.DevRespawned - r0}、掉出地图 {EnemyRespawn.DevLost - l0}");
                    last = now;
                    yield return new WaitForSeconds(1f);
                }
                Plugin.Log.LogInfo($"[AutoTest] 白虫 {scene} 30 秒里出现过的敌人（不同实例）：{string.Join(" ", seen.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key}×{kv.Value}"))}；重刷 {EnemyRespawn.DevRespawned - r0}，多放 {EnemyRespawn.DevDoubled - d0}，掉出地图 {EnemyRespawn.DevLost - l0}");
                Shot(scene, "wormscan");
                yield return null;
            }
        }

        /// <summary>Dev (suite "wormkill"): in the rooms between the Gardens and Deepnest every enemy killed, then for 40 s each one that appears noted with when and where from.</summary>
        IEnumerator WormKillTest()
        {
            var pd = PlayerData.instance;
            foreach (var scene in new[] { "Deepnest_01b", "Deepnest_02", "Deepnest_17", "Deepnest_36", "Fungus3_50", "Deepnest_43", "Deepnest_34", "Deepnest_26" })
            {
                yield return GoTo(scene, EntryGateOf(scene));
                pd.isInvincible = true;
                if (GameManager.instance.sceneName != scene) { Plugin.Log.LogInfo($"[AutoTest] 白虫杀 {scene}：没进去"); continue; }
                var known = new HashSet<int>();
                var killedAt = new Dictionary<string, int>();
                foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                {
                    if (hm.gameObject.scene.name != scene || hm.isDead || !hm.gameObject.activeInHierarchy) continue;
                    known.Add(hm.GetInstanceID());
                    Plugin.Log.LogInfo($"[AutoTest] 白虫杀 {scene} 有 {PathOf(hm.transform)}（{UI.EnemyHover.RecordOf(hm)} {EnemyDrops.ZhName(UI.EnemyHover.RecordOf(hm) ?? "")}）在 {(Vector2)hm.transform.position:F1} 无敌 {hm.IsInvincible}");
                    if (hm.IsInvincible) continue;
                    string n = BareName(hm.name);
                    killedAt[n] = (killedAt.TryGetValue(n, out int c) ? c : 0) + 1;
                    Kill(hm);
                }
                Plugin.Log.LogInfo($"[AutoTest] 白虫杀 {scene}：打死 {string.Join(" ", killedAt.Select(kv => $"{kv.Key}×{kv.Value}"))}");
                float t0 = Time.time;
                var appeared = new Dictionary<string, List<float>>();
                int r0 = EnemyRespawn.DevRespawned;
                while (Time.time - t0 < 40f)
                {
                    foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                    {
                        if (hm.gameObject.scene.name != scene || hm.isDead || !hm.gameObject.activeInHierarchy || !known.Add(hm.GetInstanceID())) continue;
                        string n = BareName(hm.name);
                        if (!appeared.TryGetValue(n, out var l)) appeared[n] = l = new List<float>();
                        l.Add(Time.time - t0);
                        if (l.Count <= 3)
                            Plugin.Log.LogInfo($"[AutoTest] 白虫杀 {scene} {Time.time - t0:F1}s 出现 {PathOf(hm.transform)} 在 {(Vector2)hm.transform.position:F1}，对象池 {hm.deathReset}，生命 {hm.hp}");
                    }
                    yield return new WaitForSeconds(0.25f);
                }
                Plugin.Log.LogInfo($"[AutoTest] 白虫杀 {scene} 40 秒里新出现：{string.Join("；", appeared.Select(kv => $"{kv.Key}×{kv.Value.Count}（{string.Join(",", kv.Value.Take(8).Select(x => x.ToString("F0")))} 秒）"))}；我们的重刷 {EnemyRespawn.DevRespawned - r0}；等待 {EnemyRespawn.Instance.DevDelays(scene)}");
            }
        }

        /// <summary>Dev (suite "carvers"): Deepnest_01b's Dirtcarvers killed whenever they can be hit for 30 s, then watched for 30 s: every new one, when, where and whose.</summary>
        IEnumerator CarversTest()
        {
            const string scene = "Deepnest_01b";
            yield return GoTo(scene, EntryGateOf(scene));
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            mod.DevNoFallDamage = true;
            // Who sends the pool's Dirtcarvers: the room's machines naming them or spawning.
            int dumped = 0;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (f.gameObject.scene.name != scene || f.GetComponent<HealthManager>() != null) continue;
                string n = f.gameObject.name + "/" + f.FsmName;
                if (!n.Contains("Centipede") && !n.Contains("Spawn") && !n.Contains("Hatcher")) continue;
                Plugin.Log.LogInfo($"[AutoTest] 刻尘者 生成器 {PathOf(f.transform)} '{f.FsmName}' {f.ActiveStateName} 激活 {f.gameObject.activeInHierarchy} 在 {(Vector2)f.transform.position:F1}");
                if (dumped++ < 2) DumpFsm(f);
            }
            var one = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None).FirstOrDefault(x => x.gameObject.scene.name == scene && x.name.StartsWith("Baby Centipede") && !x.name.Contains("(TK)"));
            if (one != null) foreach (var f in one.GetComponents<PlayMakerFSM>()) DumpFsm(f);
            var known = new HashSet<int>();
            var dead = new HashSet<int>();
            int kills = 0, r0 = EnemyRespawn.DevRespawned, l0 = EnemyRespawn.DevLost;
            float t0 = Time.time;
            var log = new List<string>();
            float nextVisit = 0f;
            int visit = 0;
            while (Time.time - t0 < 60f)
            {
                // Up to the next one in turn every 2 s (they wake as the Knight comes near), in the first 30 s.
                if (Time.time - t0 < 30f && Time.time > nextVisit)
                {
                    var carvers = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                        .Where(x => x.gameObject.scene.name == scene && x.name.StartsWith("Baby Centipede") && !x.isDead && x.gameObject.activeInHierarchy)
                        .OrderBy(x => x.GetInstanceID()).ToList();
                    if (carvers.Count > 0)
                    {
                        var c = carvers[visit++ % carvers.Count];
                        var hero = HeroController.instance;
                        hero.transform.position = Clear(hero, (Vector2)c.transform.position + Vector2.right * 2.5f);
                        hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                    }
                    nextVisit = Time.time + 2f;
                }
                int alive = 0, hittable = 0;
                foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                {
                    if (hm.gameObject.scene.name != scene || !hm.name.StartsWith("Baby Centipede")) continue;
                    int id = hm.GetInstanceID();
                    if (known.Add(id) && Time.time - t0 > 0.5f) log.Add($"{Time.time - t0:F1}s 新 {PathOf(hm.transform)} 在 {(Vector2)hm.transform.position:F1} 死 {hm.isDead} 激活 {hm.gameObject.activeInHierarchy}");
                    if (hm.isDead || !hm.gameObject.activeInHierarchy) { dead.Add(id); continue; }
                    if (dead.Remove(id)) log.Add($"{Time.time - t0:F1}s 复活 {PathOf(hm.transform)} 在 {(Vector2)hm.transform.position:F1} 生命 {hm.hp}");
                    alive++;
                    if (hm.IsInvincible) continue;
                    hittable++;
                    if (Time.time - t0 < 30f)
                    {
                        Kill(hm);
                        kills++;
                        log.Add($"{Time.time - t0:F1}s 打死 {PathOf(hm.transform)} 在 {(Vector2)hm.transform.position:F1}，之后 死 {hm.isDead} 生命 {hm.hp}");
                    }
                }
                if (Mathf.FloorToInt(Time.time - t0) % 5 == 0 && Time.time - t0 - Mathf.Floor(Time.time - t0) < 0.26f)
                    log.Add($"{Time.time - t0:F0}s 活着 {alive}（能打 {hittable}）；我们的重刷 {EnemyRespawn.DevRespawned - r0}，掉出地图 {EnemyRespawn.DevLost - l0}；刷新槽 {EnemyRespawn.Instance.DevReport(scene, out _, out _, out _)}");
                yield return new WaitForSeconds(0.25f);
            }
            foreach (var l in log) Plugin.Log.LogInfo($"[AutoTest] 刻尘者 {l}");
            Plugin.Log.LogInfo($"[AutoTest] 刻尘者 合计：打死 {kills} 次，我们的重刷 {EnemyRespawn.DevRespawned - r0}，掉出地图 {EnemyRespawn.DevLost - l0}");
            mod.DevNoFallDamage = false;
        }

        /// <summary>An enemy's name without its copy number or clone marks.</summary>
        static string BareName(string name)
        {
            name = name.Replace("(Clone)", "").Trim();
            int i = name.IndexOf(" (");
            return i > 0 ? name.Substring(0, i) : name;
        }
    }
}
