using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using TerraKnight.Items;
using TerraKnight.Net;
using UnityEngine;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight
{
    /// <summary>
    /// Suites "netboss_host[_list]" and "netboss_client": a LAN game between two test copies (port 7796) that fights bosses
    /// together. The host uses each seal (the client is taken along), and strikes until the game counts the boss beaten; the
    /// client strikes too now and then. Both write what they see of the arena five times a second to DevCopy.SaveDir's
    /// netboss.jsonl (every enemy of the arena's scenes: its snapshot number, scene-local place, active, drawn, dead, health,
    /// clip, Terraria life and maximum, drawn parts; the battle gates; control), and a summary line per boss (kills, gates,
    /// control, the shared loot lying there), for a comparison of the two games afterwards (scratchpad netboss_compare.py).
    /// The list: "world" (the world arenas), "all", or keys joined by '.'; default world.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        const int NetBossPort = 7796;
        StreamWriter netBossOut;

        IEnumerator NetBossBoot()
        {
            if (suite.StartsWith("netboss_host") || suite.StartsWith("netslide_host"))
            {
                bool ok = NetSession.Host(NetBossPort, "", 8, out string error);
                NetCheck(ok, $"开服 {NetBossPort} {error}");
                for (float t = 0f; t < 10f && !NetSession.Active; t += Time.unscaledDeltaTime) yield return null;
                NetCheck(NetSession.Active, "主机加入自己的服务器");
                yield break;
            }
            float until = Time.realtimeSinceStartup + 120f;
            while (!NetSession.Active && Time.realtimeSinceStartup < until)
            {
                NetSession.Join("127.0.0.1", NetBossPort);
                for (float t = 0f; t < 6f && NetSession.State != NetSession.Phase.Off && !NetSession.Active; t += Time.unscaledDeltaTime) yield return null;
                if (!NetSession.Active) yield return new WaitForSecondsRealtime(3f);
            }
            NetCheck(NetSession.Active, $"客户端加入主机（槽位 {NetSession.MySlot}）");
        }

        static string NbRoom(string scene) => scene != null && scene.EndsWith("_boss") ? scene.Substring(0, scene.Length - 5) : scene;

        /// <summary>The loaded scenes of a seal's arena (its room and the room's boss scene).</summary>
        static List<UnityEngine.SceneManagement.Scene> NbScenes(string key)
        {
            var list = new List<UnityEngine.SceneManagement.Scene>();
            string room = NbRoom(BossArena.SceneOf(key));
            for (int i = 0; i < USM.sceneCount; i++)
            {
                var s = USM.GetSceneAt(i);
                if (s.isLoaded && NbRoom(s.name) == room) list.Add(s);
            }
            return list;
        }

        /// <summary>The boss's parts: every HealthManager under a root that holds one of its record (the False Knight's Head has none of its own).</summary>
        static List<HealthManager> NbParts(string key, bool activeOnly)
        {
            string record = key.Split('@')[0];
            var found = new List<HealthManager>();
            foreach (var s in NbScenes(key))
                foreach (var root in s.GetRootGameObjects())
                {
                    var all = root.GetComponentsInChildren<HealthManager>(true);
                    if (!System.Array.Exists(all, hm => UI.EnemyHover.RecordOf(hm) == record)) continue;
                    foreach (var hm in all)
                        if (!activeOnly || hm.gameObject.activeInHierarchy) found.Add(hm);
                }
            return found;
        }

        static long NbNow => System.DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        void NbWrite(object line)
        {
            if (netBossOut == null)
            {
                string dir = !string.IsNullOrEmpty(DevCopy.SaveDir) ? DevCopy.SaveDir : Path.Combine(BepInEx.Paths.BepInExRootPath, "TerraKnight", "dev");
                netBossOut = new StreamWriter(Path.Combine(dir, "netboss.jsonl"), false, new UTF8Encoding(false));
            }
            netBossOut.WriteLine(JsonConvert.SerializeObject(line));
            netBossOut.Flush();
        }

        /// <summary>One look at the arena: its enemies, gates, battle FSMs, the player.</summary>
        void NbSample(string key, string side)
        {
            var enemies = new List<object>();
            foreach (var s in NbScenes(key))
                foreach (var root in s.GetRootGameObjects())
                    foreach (var hm in root.GetComponentsInChildren<HealthManager>(true))
                    {
                        if (hm.name.StartsWith("TK ") || hm.transform.root.name == "TK Enemy Templates") continue;
                        bool puppet = Net.Enemies.IsPuppet(hm);
                        int id = puppet ? Net.Enemies.DevPuppetId(hm) : Net.Enemies.DevIdOf(hm);
                        if (!hm.gameObject.activeInHierarchy && id < 0) continue;
                        var info = EnemyStats.Of(hm);
                        var rend = hm.GetComponent<Renderer>();
                        var anim = hm.GetComponent<tk2dSpriteAnimator>();
                        var col = hm.GetComponent<Collider2D>();
                        int drawn = 0;
                        var drawnNames = new List<string>();
                        foreach (var r in hm.GetComponentsInChildren<Renderer>(false))
                            if (r.enabled && r.GetComponentInParent<HealthManager>() == hm && !(r is ParticleSystemRenderer))
                            {
                                drawn++;
                                if (r.transform != hm.transform && drawnNames.Count < 12) drawnNames.Add(r.name);
                            }
                        drawnNames.Sort();
                        Vector2 local = NetScenes.ToLocal(hm.gameObject.scene.name, hm.transform.position);
                        enemies.Add(new
                        {
                            id, n = hm.name, p = puppet, a = hm.gameObject.activeInHierarchy, r = rend != null && rend.enabled, d = hm.isDead,
                            hp = hm.hp, x = System.Math.Round(local.x, 2), y = System.Math.Round(local.y, 2),
                            c = anim != null && anim.CurrentClip != null ? anim.CurrentClip.name : null, f = anim != null && anim.CurrentClip != null ? anim.CurrentFrame : -1,
                            L = EnemyStats.Life(hm, info), M = EnemyStats.LifeMax(info), k = drawn, kn = UI.EnemyHover.RecordOf(hm) == key.Split('@')[0] || hm.name == "Head" ? drawnNames : null, col = col != null && col.enabled, inv = hm.IsInvincible,
                            rec = UI.EnemyHover.RecordOf(hm), sc = hm.gameObject.scene.name,
                            fsms = System.Array.ConvertAll(hm.GetComponentsInChildren<PlayMakerFSM>(true), f => $"{f.name}/{f.FsmName}:{f.ActiveStateName}:{f.enabled}"),
                            audio = System.Array.ConvertAll(hm.GetComponentsInChildren<AudioSource>(true), a => $"{a.name}:{a.isPlaying}:{a.loop}:{a.clip?.name}"),
                        });
                    }
            string room = NbRoom(BossArena.SceneOf(key));
            var gates = new List<string>();
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
            {
                if (f.FsmName != "BG Control" || NbRoom(f.gameObject.scene.name) != room) continue;
                var c = f.GetComponent<Collider2D>();
                gates.Add($"{f.name}@{(Vector2)NetScenes.ToLocal(f.gameObject.scene.name, f.transform.position):F0}:{f.ActiveStateName}:{(c != null && c.enabled ? 1 : 0)}");
            }
            gates.Sort();
            var battles = new List<string>();
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
            {
                if (NbRoom(f.gameObject.scene.name) != room || f.GetComponentInParent<HealthManager>() != null) continue;
                string n = f.gameObject.name + "/" + f.FsmName;
                if (n.Contains("Battle") || n.Contains("Boss Control") || n.Contains("Grimm Scene") || n.Contains("Initial Scene"))
                    battles.Add(n + ":" + f.ActiveStateName);
            }
            battles.Sort();
            var hero = HeroController.SilentInstance;
            Vector2 hp = hero != null ? NetScenes.ToLocal(NetScenes.Current, hero.transform.position) : Vector2.zero;
            // Enemies up near the player but in no scene of the arena (spawned from a pool into DontDestroyOnLoad):
            // never in any snapshot, so the other game can't have them.
            var elsewhere = new List<string>();
            var arena = new HashSet<string>();
            foreach (var sc in NbScenes(key)) arena.Add(sc.name);
            if (hero != null)
                foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                    if (!arena.Contains(hm.gameObject.scene.name) && !hm.isDead && Vector2.Distance(hm.transform.position, hero.transform.position) < 40f && elsewhere.Count < 20)
                        elsewhere.Add($"{hm.gameObject.scene.name}/{hm.name}");
            int owner = NetSession.Owners.TryGetValue(BossArena.SceneOf(key), out int o) ? o : NetSession.Owners.TryGetValue(BossArena.SceneOf(key) + "_boss", out o) ? o : -1;
            string wall = hero != null && NbInWall(hero, out string what) ? what : null;
            var w = nbWall.TryGetValue(key + side, out var had) ? had : (0, 0, null);
            nbWall[key + side] = (w.n + 1, w.wall + (wall != null ? 1 : 0), w.first ?? (wall != null ? $"{NbNow:F1}s {wall} @({hp.x:F1},{hp.y:F1}) 主人 {owner} 我 {NetSession.MySlot}" : null));
            NbWrite(new
            {
                t = NbNow, k = key, s = side, me = NetSession.MySlot, own = owner, scene = GameManager.instance.sceneName,
                ctl = GameHooks.PlayerHasControl(), hx = System.Math.Round(hp.x, 1), hy = System.Math.Round(hp.y, 1),
                e = enemies, g = gates, b = battles, gh = EnemyAttacks.DevLiveGhosts, x2 = elsewhere, wall,
                peer = Other()?.Visible, peerAge = Other() != null ? Time.unscaledTime - Other().LastStateAt : -1f,
            });
        }

        /// <summary>The shared items lying in the arena's scenes now: "type×count" sorted.</summary>
        static List<string> NbLoot(string key, bool personal = false)
        {
            var list = new List<string>();
            var scenes = new HashSet<string>();
            foreach (var s in NbScenes(key)) scenes.Add(s.name);
            foreach (var (net, stack, at, drawn) in WorldItems.DevItems)
            {
                if ((net == null) != personal) continue;
                var cell = World.CellGrid.WorldToCell(at);
                string sc = World.TerraWorld.RegionAt(cell.x, cell.y)?.SceneName;
                if (sc == null || !scenes.Contains(sc) && !scenes.Contains(NbRoom(sc))) continue;
                list.Add($"{stack.Type}×{stack.Count}{(drawn ? "" : "(不显示)")}");
            }
            list.Sort();
            return list;
        }

        void NbSummary(string key, string side, int kills0, int errors)
        {
            string record = key.Split('@')[0];
            var standing = new List<string>();
            foreach (var hm in NbParts(key, true))
                if (!hm.isDead && hm.hp > 0 && !EnemyStats.Defeated(hm) && UI.EnemyHover.RecordOf(hm) == record) standing.Add($"{hm.name}:{hm.hp}{(Net.Enemies.IsPuppet(hm) ? "(傀儡)" : "")}");
            int shut = 0;
            string room = NbRoom(BossArena.SceneOf(key));
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                if (f.FsmName == "BG Control" && NbRoom(f.gameObject.scene.name) == room && f.GetComponent<Collider2D>() is Collider2D c && c.enabled) shut++;
            var bag = TreasureBags.For(record, BossArena.SceneOf(key), null);
            int bagInventory = 0, bagVisible = 0;
            foreach (var stack in mod.Inventory.Slots) if (stack.Type == bag) bagInventory += stack.Count;
            foreach (var (net, stack, _, drawn) in WorldItems.DevItems)
                if (net == null && stack.Type == bag && drawn) bagVisible += stack.Count;
            foreach (var stack in EnemyDrops.PendingLoot()) if (stack.Type == bag) bagInventory += stack.Count;
            var line = new
            {
                t = NbNow, k = key, s = side, sum = true, kills = KillsOf(record) - kills0, standing, gatesShut = shut,
                ctl = GameHooks.PlayerHasControl(), scene = GameManager.instance.sceneName, loot = NbLoot(key), own = NbLoot(key, true), errors,
                net = Net.Enemies.DevReport(), life = mod.Stats.Life, dead = HeroController.instance.cState.dead,
                wall = nbWall.TryGetValue(key + side, out var w) ? $"{w.wall}/{w.n} 次在墙里，第一次 {w.first ?? "无"}" : "没取样",
                bag = bag.ToString(), bagInventory, bagVisible,
            };
            NbWrite(line);
            NetCheck(standing.Count == 0 && KillsOf(record) > kills0, $"{key} {side} 战斗结算且没有存活首领");
            NetCheck(errors == 0 && sampleErrors == 0, $"{key} {side} 战斗无异常（{errors}/{sampleErrors}）");
            NetCheck(Other() != null && Other().Visible && Time.unscaledTime - Other().LastStateAt < 2f, $"{key} {side} 战后仍看到对方且持续收到人物状态");
            if (TreasureBags.Active && bag != ItemType.None)
                NetCheck(bagInventory + bagVisible == 1, $"{key} {side} 恰有一个可领取个人宝藏袋（背包/待发 {bagInventory}，场景可见 {bagVisible}）");
            Plugin.Log.LogInfo($"[AutoTest] 联机首领 {side} {key} 小结：{JsonConvert.SerializeObject(line)}");
        }

        int sampleErrors;
        IEnumerator NbCollectPersonalBag(string key, string side)
        {
            var bag = TreasureBags.For(key.Split('@')[0], BossArena.SceneOf(key), null);
            if (!TreasureBags.Active || bag == ItemType.None) yield break;
            bool picked = false;
            for (float until = Time.realtimeSinceStartup + 6f; Time.realtimeSinceStartup < until;)
            {
                foreach (var stack in mod.Inventory.Slots) if (stack.Type == bag && stack.Count > 0) picked = true;
                if (picked) break;
                foreach (var (net, stack, at, drawn) in WorldItems.DevItems)
                    if (net == null && stack.Type == bag && drawn)
                    {
                        HeroController.instance.transform.position = (Vector3)(at + Vector2.up * 1.4f);
                        HeroController.instance.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                        break;
                    }
                yield return null;
            }
            NetCheck(picked, $"{key} {side} 个人宝藏袋真实拾取进入背包");
        }
        readonly HashSet<string> nbApproachedPhase = new HashSet<string>();
        // Soul Master's second phase waits for the player to approach below the
        // broken floor. A stationary damage fixture must follow that natural trigger.
        void NbApproachSecondPhase(string key)
        {
            if (key != "MageLord" || nbApproachedPhase.Contains(key) || !GameHooks.PlayerHasControl()) return;
            var parts = NbParts(key, true);
            foreach (var hm in parts)
                if (hm.name == "Mage Lord" && !hm.isDead && hm.hp > 0 && !EnemyStats.Defeated(hm)) return;
            foreach (var hm in parts)
            {
                if (hm.name != "Mage Lord Phase2" || hm.isDead || hm.GetComponent<Renderer>()?.enabled != false) continue;
                var hero = HeroController.instance;
                hero.transform.position = hm.transform.position + new Vector3(-3f, 1f, 0f);
                hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                nbApproachedPhase.Add(key);
                return;
            }
        }
        static readonly Dictionary<string, (int n, int wall, string first)> nbWall = new Dictionary<string, (int, int, string)>();

        /// <summary>The Knight's box (a fifth smaller, standing on a floor isn't in it) overlapping solid terrain: what.</summary>
        static bool NbInWall(HeroController hero, out string what)
        {
            what = null;
            var box = hero.GetComponent<BoxCollider2D>();
            if (box == null) return false;
            var b = box.bounds;
            foreach (var c in Physics2D.OverlapBoxAll(b.center, (Vector2)b.size * 0.8f, 0f, 1 << World.ChunkColliders.TerrainLayer))
                if (c != null && c.enabled && !c.isTrigger && !c.usedByEffector) { what = c.name; return true; }
            return false;
        }

        IEnumerator NbSampling(string key, string side, System.Func<bool> until)
        {
            sampleErrors = 0;
            float next = 0f;
            while (!until())
            {
                if (Time.realtimeSinceStartup >= next)
                {
                    next = Time.realtimeSinceStartup + 0.2f;
                    try { NbSample(key, side); }
                    catch (System.Exception e) { if (sampleErrors++ < 3) Plugin.Log.LogInfo($"[AutoTest] 联机首领 取样出错 {e.GetType().Name} {e.Message} {e.StackTrace}"); }
                }
                yield return null;
            }
        }

        List<BossSummons.Seal> NbList()
        {
            string rest = suite.StartsWith("netboss_host_") ? suite.Substring("netboss_host_".Length) : "world";
            if (rest.StartsWith("late_")) rest = rest.Substring(5);
            if (rest == "late") rest = "world";
            var list = new List<BossSummons.Seal>();
            var keys = rest == "world" || rest == "all" ? null : new HashSet<string>(rest.Split('.'));
            foreach (var s in BossSummons.All)
            {
                if (keys != null && !keys.Contains(s.Key)) continue;
                if (rest == "world" && s.Dream) continue;
                list.Add(s);
            }
            return list;
        }

        void NbPrepare()
        {
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            mod.Stats.LifeCrystals = Player.TerraStats.MaxLifeCrystals;
            mod.Stats.LifeFruits = 20;
            PlayerData.instance.hasDreamNail = true;
            mod.Inventory.Infinite = false;
        }

        /// <summary>TK_NB_IDLE: seconds of each fight left to the boss before the blows.</summary>
        static float NbIdle() => float.TryParse(System.Environment.GetEnvironmentVariable("TK_NB_IDLE"), out float s) ? s : 0f;

        IEnumerator NbAudioTick(System.Func<bool> over)
        {
            while (!over() && DevAudioWatch.On)
            {
                DevAudioWatch.Tick();
                yield return null;
            }
        }

        IEnumerator NetBossHost(string scene, HeroController hero)
        {
            bool done = false;
            mod.StartCoroutine(NetKeepAlive(() => done));
            var errors = new List<string>();
            void OnLog(string msg, string stack, LogType type)
            {
                if ((type == LogType.Exception || (type == LogType.Error && msg.Contains("Exception")) || msg.StartsWith("[Error")) && errors.Count < 50) errors.Add(msg);
            }
            Application.logMessageReceived += OnLog;
            try
            {
                NbPrepare();
                RemotePlayer other = null;
                for (float t = 0f; t < 180f && !(other != null && other.Visible); t += Time.unscaledDeltaTime) { other = Other(); yield return null; }
                NetCheck(other != null && other.Visible, $"看到客户端的人物：{(other != null ? other.Name : "没有")}");
                yield return SealTravel("Town", "left1");
                yield return WaitChat("nb:client ready", 120f);
                var inv = mod.Inventory;
                foreach (var s in NbList())
                {
                    string record = s.Key.Split('@')[0];
                    Bestiary.Kills[s.Key] = Mathf.Max(1, KillsOf(s.Key));
                    Shop.DevStage = s.Stage;
                    inv.Slots[0] = ItemStack.Of(s.Type, 3);
                    inv.Selected = 0;
                    inv.Override = -1;
                    for (float ready = Time.realtimeSinceStartup + 15f; !GameHooks.PlayerHasControl() && Time.realtimeSinceStartup < ready;) yield return null;
                    string why = BossSummons.Refusal(s);
                    if (why != null) { NetCheck(false, $"{s.Key} 不能召唤：{why}"); continue; }
                    NetChat.Say($"nb:go {s.Key} {s.Stage}|");
                    yield return WaitChat($"nb:set {s.Key}|", 30f);
                    errors.Clear();
                    int kills0 = KillsOf(record);
                    // "_late_": the client in the arena first (its game loaded it before the host's: it runs the fight), then the seal here.
                    if (suite.Contains("_late_"))
                    {
                        NetSession.SendBossSummon(s.Key);
                        yield return WaitChat($"nb:in {s.Key}|", 40f);
                        yield return new WaitForSecondsRealtime(2f);
                    }
                    mod.DevUseSelected(HeroController.instance.transform.position);
                    for (float there = Time.realtimeSinceStartup + 40f; !BossArena.Arrived(s.Key) && Time.realtimeSinceStartup < there;) yield return null;
                    bool arrived = BossArena.Arrived(s.Key);
                    NetCheck(arrived, $"{s.Key} 主机进竞技场 {GameManager.instance.sceneName}");
                    bool fightOver = false;
                    mod.StartCoroutine(NbSampling(s.Key, "host", () => fightOver));
                    DevAudioWatch.Begin();
                    mod.StartCoroutine(NbAudioTick(() => fightOver));
                    if (!suite.Contains("_late_")) yield return WaitChat($"nb:in {s.Key}|", 40f);
                    // TK_NB_IDLE seconds of the fight left to the boss before the blows (its sounds counted on both sides).
                    if (NbIdle() > 0f)
                    {
                        float nextTap = 0f;
                        for (float until = Time.time + NbIdle(); Time.time < until;)
                        {
                            if (!GameHooks.PlayerHasControl() && Time.time >= nextTap) { nextTap = Time.time + 1.5f; yield return Tap(UnityEngine.InputSystem.Key.Space); }
                            yield return null;
                        }
                        NetChat.Say($"nb:idle {s.Key}|");
                        Plugin.Log.LogInfo($"[AutoTest] 音效 {s.Key} 主机 空等：{DevAudioWatch.Report()}");
                        DevAudioWatch.Begin();
                    }
                    // Strike every part a weapon could touch until the game counts it beaten (no part of it left standing).
                    float t0 = Time.time, nextHit = 0f, lastChange = Time.time;
                    int lastHp = -1;
                    bool beaten = false;
                    while (arrived && Time.time - t0 < 180f)
                    {
                        NbApproachSecondPhase(s.Key);
                        if (KillsOf(record) > kills0)
                        {
                            bool standing = false;
                            foreach (var hm in NbParts(s.Key, true))
                                if (!hm.isDead && hm.hp > 0 && !EnemyStats.Defeated(hm) && UI.EnemyHover.RecordOf(hm) == record) standing = true;
                            if (!standing) { beaten = true; break; }
                        }
                        if (!GameHooks.PlayerHasControl() && Time.time - lastChange > 3f && Time.time >= nextHit)
                        {
                            nextHit = Time.time + 1f;
                            yield return Tap(UnityEngine.InputSystem.Key.Space);
                        }
                        int sum = 0;
                        foreach (var hm in NbParts(s.Key, true)) sum += Mathf.Max(0, hm.hp);
                        if (sum != lastHp) { lastHp = sum; lastChange = Time.time; }
                        if (Time.time >= nextHit && GameHooks.PlayerHasControl())
                        {
                            nextHit = Time.time + 0.35f;
                            var h = HeroController.instance;
                            foreach (var hm in NbParts(s.Key, true))
                            {
                                if (hm.isDead || EnemyStats.Defeated(hm)) continue;
                                var col = hm.GetComponent<Collider2D>();
                                if (col == null || !col.enabled)
                                {
                                    col = null;
                                    foreach (var c in hm.GetComponentsInChildren<Collider2D>())
                                        if (c.enabled && c.GetComponentInParent<HealthManager>() == hm) { col = c; break; }
                                }
                                if (col == null) continue;
                                var info = EnemyStats.Of(hm);
                                int dmg = Mathf.Max(150, EnemyStats.LifeWhole(info) / 150 + info.Defense);
                                mod.Melee.HitWith(col, hm.name == "Head" ? dmg * 3 : dmg, 0f, 0f, 1f, h.GetComponent<BoxCollider2D>().bounds.center);
                            }
                        }
                        yield return null;
                    }
                    float took = Time.time - t0;
                    NetCheck(beaten, $"{s.Key} 主机打倒（{took:F0}s，{Net.Enemies.DevReport()}）");
                    // The death's own scene, the loot, the gates.
                    for (float tail = Time.realtimeSinceStartup + 14f; Time.realtimeSinceStartup < tail;) yield return null;
                    fightOver = true;
                    Plugin.Log.LogInfo($"[AutoTest] 音效 {s.Key} 主机 开打：{DevAudioWatch.Report()}");
                    DevAudioWatch.On = false;
                    yield return NbCollectPersonalBag(s.Key, "host");
                    NbSummary(s.Key, "host", kills0, errors.Count);
                    if (errors.Count > 0) Plugin.Log.LogInfo($"[AutoTest] 联机首领 主机 {s.Key} 报错：{string.Join(" | ", errors.GetRange(0, Mathf.Min(5, errors.Count)))}");
                    NetChat.Say($"nb:end {s.Key}|");
                    yield return WaitChat($"nb:town {s.Key}|", 60f);
                    for (float ctl = Time.realtimeSinceStartup + 20f; !GameHooks.PlayerHasControl() && Time.realtimeSinceStartup < ctl;) yield return Tap(UnityEngine.InputSystem.Key.Space);
                    yield return SealTravel("Town", "left1");
                    for (float clean = Time.realtimeSinceStartup + 10f; (BossArena.Visiting || BossSummons.Pending) && Time.realtimeSinceStartup < clean;) yield return null;
                }
                NetChat.Say("nb:bye");
                yield return new WaitForSecondsRealtime(3f);
            }
            finally
            {
                done = true;
                Application.logMessageReceived -= OnLog;
                Player.TerraStats.DevImmortal = false;
                netBossOut?.Dispose();
                netBossOut = null;
            }
        }

        IEnumerator NetBossClient(string scene, HeroController hero)
        {
            bool done = false;
            mod.StartCoroutine(NetKeepAlive(() => done));
            var errors = new List<string>();
            void OnLog(string msg, string stack, LogType type)
            {
                if ((type == LogType.Exception || (type == LogType.Error && msg.Contains("Exception")) || msg.StartsWith("[Error")) && errors.Count < 50) errors.Add(msg);
            }
            Application.logMessageReceived += OnLog;
            try
            {
                NbPrepare();
                yield return SealTravel("Town", "left1");
                NetChat.Say("nb:client ready");
                var handled = new HashSet<string>();
                float idle = Time.realtimeSinceStartup;
                while (!NetChat.DevHas("nb:bye") && Time.realtimeSinceStartup - idle < 400f)
                {
                    BossSummons.Seal s = null;
                    foreach (var seal in BossSummons.All)
                        if (!handled.Contains(seal.Key) && NetChat.DevHas($"nb:go {seal.Key} {seal.Stage}|")) { s = seal; break; }
                    if (s == null) { yield return null; continue; }
                    handled.Add(s.Key);
                    idle = Time.realtimeSinceStartup;
                    string record = s.Key.Split('@')[0];
                    Shop.DevStage = s.Stage;
                    Bestiary.Kills[s.Key] = Mathf.Max(1, KillsOf(s.Key));
                    int kills0 = KillsOf(record);
                    errors.Clear();
                    int followed0 = BossSummons.DevFollowed;
                    NetChat.Say($"nb:set {s.Key}|");
                    for (float t = Time.realtimeSinceStartup + 40f; !BossArena.Arrived(s.Key) && Time.realtimeSinceStartup < t;) yield return null;
                    NetCheck(BossArena.Arrived(s.Key), $"{s.Key} 客户端被带进竞技场（跟随 {BossSummons.DevFollowed - followed0}，{GameManager.instance.sceneName}）");
                    bool over = false;
                    mod.StartCoroutine(NbSampling(s.Key, "client", () => over));
                    DevAudioWatch.Begin();
                    mod.StartCoroutine(NbAudioTick(() => over));
                    bool idling = NbIdle() > 0f;
                    for (float ctl = Time.realtimeSinceStartup + 10f; !GameHooks.PlayerHasControl() && Time.realtimeSinceStartup < ctl;) yield return null;
                    NetChat.Say($"nb:in {s.Key}|");
                    // In the fight: a blow now and then on a part of the boss (a puppet's goes to its owner).
                    float nextHit = Time.time + 2f, nextTalk = 0f;
                    while (!NetChat.DevHas($"nb:end {s.Key}|"))
                    {
                        NbApproachSecondPhase(s.Key);
                        if (idling && NetChat.DevHas($"nb:idle {s.Key}|"))
                        {
                            idling = false;
                            Plugin.Log.LogInfo($"[AutoTest] 音效 {s.Key} 客户端 空等：{DevAudioWatch.Report()}");
                            DevAudioWatch.Begin();
                        }
                        if (!GameHooks.PlayerHasControl() && Time.time >= nextTalk)
                        {
                            nextTalk = Time.time + 1.5f;
                            yield return Tap(UnityEngine.InputSystem.Key.Space);
                        }
                        if (!idling && Time.time >= nextHit && GameHooks.PlayerHasControl())
                        {
                            nextHit = Time.time + 1.2f;
                            var h = HeroController.instance;
                            var targets = NbParts(s.Key, true).FindAll(hm => !hm.isDead && hm.GetComponent<Collider2D>() is Collider2D c && c.enabled);
                            if (targets.Count > 0)
                                mod.Melee.HitWith(targets[Random.Range(0, targets.Count)].GetComponent<Collider2D>(), 300, 0f, 0f, 1f, h.GetComponent<BoxCollider2D>().bounds.center);
                        }
                        yield return null;
                    }
                    Plugin.Log.LogInfo($"[AutoTest] 音效 {s.Key} 客户端 开打：{DevAudioWatch.Report()}");
                    DevAudioWatch.On = false;
                    over = true;
                    yield return NbCollectPersonalBag(s.Key, "client");
                    NbSummary(s.Key, "client", kills0, errors.Count);
                    if (errors.Count > 0) Plugin.Log.LogInfo($"[AutoTest] 联机首领 客户端 {s.Key} 报错：{string.Join(" | ", errors.GetRange(0, Mathf.Min(5, errors.Count)))}");
                    for (float ctl = Time.realtimeSinceStartup + 20f; !GameHooks.PlayerHasControl() && Time.realtimeSinceStartup < ctl;) yield return Tap(UnityEngine.InputSystem.Key.Space);
                    yield return SealTravel("Town", "left1");
                    for (float clean = Time.realtimeSinceStartup + 10f; (BossArena.Visiting || BossSummons.Pending) && Time.realtimeSinceStartup < clean;) yield return null;
                    NetChat.Say($"nb:town {s.Key}|");
                }
            }
            finally
            {
                done = true;
                Application.logMessageReceived -= OnLog;
                Player.TerraStats.DevImmortal = false;
                netBossOut?.Dispose();
                netBossOut = null;
            }
        }
    }
}
