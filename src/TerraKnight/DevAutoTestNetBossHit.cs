using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.Net;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Suites "nbhit_host_KEY.KEY…" and "nbhit_client" (port 7799), players' 10-26 "boss会有很多bug，比如直接死亡，或者其中一个
    /// 人打不了": two players in a world boss's room, both saves fresh; once with the host walking in first (it runs the boss),
    /// once the client. Then (A) only the other one strikes for ten seconds, (B) the same with the runner's Knight held (as
    /// dead and waiting, or in a scene), (C) both strike till it falls. The runner logs the boss's health each second and
    /// what each phase took off; the striker its blows forwarded, held by BossWake and refused by ArenaGuard; both whether
    /// it fell, and a fall with much health left a second before ("直接死亡"). Lines "[AutoTest] 联机打首领 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        const int NbHitPort = 7799;

        IEnumerator NbHitBoot()
        {
            BossWake.DevOldNet = System.Environment.GetEnvironmentVariable("TK_NH_BEFORE") == "1";
            Plugin.Log.LogInfo($"[AutoTest] 联机打首领 旧规则 {BossWake.DevOldNet}");
            int port = int.TryParse(System.Environment.GetEnvironmentVariable("TK_NH_PORT"), out int envPort) ? envPort : NbHitPort;
            if (suite.StartsWith("nbhit_host"))
            {
                bool ok = NetSession.Host(port, "", 8, out string error);
                NetCheck(ok, $"开服 {port} {error}");
                for (float t = 0f; t < 10f && !NetSession.Active; t += Time.unscaledDeltaTime) yield return null;
                NetCheck(NetSession.Active, "主机加入自己的服务器");
                yield break;
            }
            float until = Time.realtimeSinceStartup + 120f;
            while (!NetSession.Active && Time.realtimeSinceStartup < until)
            {
                NetSession.Join("127.0.0.1", port);
                for (float t = 0f; t < 6f && NetSession.State != NetSession.Phase.Off && !NetSession.Active; t += Time.unscaledDeltaTime) yield return null;
                if (!NetSession.Active) yield return new WaitForSecondsRealtime(3f);
            }
            NetCheck(NetSession.Active, $"客户端加入主机（槽位 {NetSession.MySlot}）");
        }

        static int NhHealth(string key) => NbParts(key, true).Where(hm => !hm.isDead && UI.EnemyHover.RecordOf(hm) == key.Split('@')[0]).Sum(hm => Mathf.Max(0, hm.hp));

        bool NhRunner(string key)
        {
            string room = BossArena.SceneOf(key);
            return NetSession.Owners.TryGetValue(room, out int o) && o == NetSession.MySlot;
        }

        /// <summary>Strikes every standing part of the boss every so often, for a while or until told to stop (a chat line).</summary>
        /// <summary>
        /// Into the arena on foot from the gate, as a player comes in (a boss that waits for one to cross its trigger, the
        /// False Knight in the roof, starts as it does in play); still short of the middle after eight seconds: put there.
        /// </summary>
        IEnumerator NhWalkIn(string key)
        {
            var hero = HeroController.instance;
            var region = World.TerrainSystem.Find(BossArena.SceneOf(key));
            if (hero == null || region?.OwnZone == null) { yield return NwIntoArena(key); yield break; }
            var z = region.OwnZone.Value;
            // The False Knight's arena is the top of Crossroads_10 (he lands at y 31.6, the gate is at the bottom): onto its
            // floor beside him, where the room's battle trigger starts his fight as a player coming up does.
            var fk = key == "FalseKnight" ? Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                .FirstOrDefault(h => h != null && !h.isDead && UI.EnemyHover.RecordOf(h) == "FalseKnight" && h.name.StartsWith("False Knight")) : null;
            if (fk != null)
            {
                hero.transform.position = Clear(hero, new Vector2(fk.transform.position.x - 5f, 31.5f));
                hero.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
                for (float t = Time.time + 8f; Time.time < t && World.ArenaGuard.Hidden(fk);) yield return null;
                Plugin.Log.LogInfo($"[AutoTest] 联机走进 {key} 站到它的场地 {(Vector2)hero.transform.position:F1}：{(World.ArenaGuard.Hidden(fk) ? "还藏着" : "登场了")}，{World.ArenaGuard.DevWhy(fk)}");
                yield return new WaitForSecondsRealtime(2f);
                yield break;
            }
            float x0 = hero.transform.position.x;
            int way = z.center.x > x0 ? 1 : -1;
            for (float t = Time.time + 8f; Time.time < t && Mathf.Abs(hero.transform.position.x - z.center.x) > 1.5f; )
            {
                Player.TerraMovement.DevMove = way;
                yield return null;
            }
            Player.TerraMovement.DevMove = 0;
            bool walked = Mathf.Abs(hero.transform.position.x - z.center.x) <= 1.5f;
            Plugin.Log.LogInfo($"[AutoTest] 联机走进 {key} 从门口走进 {(walked ? "到了中间" : "没走到，放到中间")} {(Vector2)hero.transform.position:F1}");
            if (!walked) yield return NwIntoArena(key);
            else yield return new WaitForSecondsRealtime(1f);
        }

        /// <summary>
        /// The False Knight comes down only once the room's battle has had its first wave, the husks at his arena: the side
        /// running the fight kills them (as the players would) and waits for him to show, then tells the other; still in
        /// the roof after twelve seconds, his BATTLE START is sent. Other bosses: nothing.
        /// </summary>
        IEnumerator NhStage(string key, string tag, bool runner)
        {
            if (key != "FalseKnight") yield break;
            if (!runner)
            {
                yield return WaitChat($"nh:staged {tag}|", 60f);
                yield return new WaitForSecondsRealtime(1f);
                yield break;
            }
            HealthManager fk = null;
            int killed = 0;
            float until = Time.time + 12f;
            while (Time.time < until)
            {
                fk = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                    .FirstOrDefault(h => h != null && !h.isDead && UI.EnemyHover.RecordOf(h) == "FalseKnight" && h.name.StartsWith("False Knight"));
                if (fk != null && !World.ArenaGuard.Hidden(fk)) break;
                foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                    if (hm != null && !hm.isDead && hm.hp > 0 && hm.gameObject.activeInHierarchy && hm.gameObject.scene.name.StartsWith("Crossroads_10")
                        && UI.EnemyHover.RecordOf(hm) != "FalseKnight")
                    {
                        hm.Die(null, AttackTypes.Generic, true);
                        killed++;
                    }
                yield return new WaitForSeconds(0.5f);
            }
            bool sent = false;
            if (fk != null && World.ArenaGuard.Hidden(fk))
            {
                var control = fk.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "FalseyControl");
                control?.SendEvent("BATTLE START");
                sent = control != null;
                for (float t = Time.time + 8f; Time.time < t && World.ArenaGuard.Hidden(fk);) yield return null;
            }
            yield return new WaitForSeconds(2f);
            Plugin.Log.LogInfo($"[AutoTest] 联机打首领 {tag} 假骑士开场：清掉小怪 {killed} 只{(sent ? "，补发 BATTLE START" : "")}，{(fk == null ? "没找到他" : World.ArenaGuard.Hidden(fk) ? "还藏着" : "登场了")}，{(fk != null ? World.ArenaGuard.DevWhy(fk) : "-")}");
            NetChat.Say($"nh:staged {tag}|");
        }

        IEnumerator NhStrike(string key, float seconds, float every, string stopOn = null)
        {
            string record = key.Split('@')[0];
            float until = Time.time + seconds;
            while (Time.time < until && (stopOn == null || !NetChat.DevHas(stopOn)))
            {
                if (!NbParts(key, true).Any(hm => !hm.isDead && hm.hp > 0 && !EnemyStats.Defeated(hm) && UI.EnemyHover.RecordOf(hm) == record) && KillsOf(record) > 0) break;
                var h = HeroController.instance;
                foreach (var hm in NbParts(key, true))
                {
                    if (hm.isDead || EnemyStats.Defeated(hm) || UI.EnemyHover.RecordOf(hm) != record) continue;
                    var col = hm.GetComponent<Collider2D>();
                    if (col == null || !col.enabled) col = hm.GetComponentsInChildren<Collider2D>().FirstOrDefault(c => c.enabled && c.GetComponentInParent<HealthManager>() == hm);
                    if (col == null) continue;
                    var info = EnemyStats.Of(hm);
                    int dmg = Mathf.Max(40, EnemyStats.LifeWhole(info) / 90 + info.Defense);
                    mod.Melee.HitWith(col, dmg, 0f, 0f, 1f, h.GetComponent<BoxCollider2D>().bounds.center);
                }
                yield return new WaitForSeconds(every);
            }
        }

        string NhCounters() => $"转交 {Enemies.DevForwarded} 结算 {Enemies.DevApplied} 唤醒挡下 {BossWake.DevHeld} 叫醒 {BossWake.DevWoken} 场外拒绝 {World.ArenaGuard.DevBlocked}";

        IEnumerator NbHitHost(string scene, HeroController hero)
        {
            bool done = false;
            mod.StartCoroutine(NetKeepAlive(() => done));
            try
            {
                NbPrepare();
                RemotePlayer other = null;
                for (float t = 0f; t < 180f && !(other != null && other.Visible); t += Time.unscaledDeltaTime) { other = Other(); yield return null; }
                NetCheck(other != null && other.Visible, $"看到客户端的人物：{(other != null ? other.Name : "没有")}");
                yield return SealTravel("Town", "left1");
                yield return WaitChat("nh:client ready", 120f);
                var keys = suite.Length > "nbhit_host_".Length ? suite.Substring("nbhit_host_".Length).Split('.') : new[] { "Hornet" };
                int n = 0;
                foreach (var key in keys)
                    foreach (var first in new[] { "host", "client" })
                    {
                        n++;
                        string tag = $"{key} {first} #{n}";
                        if (!BossArena.DevWorldArena(key, out string gate, out var needs)) { NetCheck(false, $"{key} 没有世界竞技场"); continue; }
                        var seal = BossSummons.All.FirstOrDefault(s => s.Key == key);
                        Shop.DevStage = seal != null ? seal.Stage : 5;
                        if (NbRoom(GameManager.instance.sceneName) == NbRoom(BossArena.SceneOf(key))) yield return SealTravel("Town", "left1");
                        foreach (var (name, v) in needs ?? new (string, bool)[0]) PlayerData.instance.SetBool(name, v);
                        NwSetBeaten(key, false);
                        int kills0 = KillsOf(key.Split('@')[0]);
                        NetChat.Say($"nh:go {tag}|");
                        yield return WaitChat($"nh:set {tag}|", 30f);
                        if (first == "client") { yield return WaitChat($"nh:in {tag}|", 60f); yield return new WaitForSecondsRealtime(3f); }
                        yield return GoTo(BossArena.SceneOf(key), gate);
                        yield return NhWalkIn(key);
                        NetChat.Say($"nh:hostin {tag}|");
                        if (first == "host") yield return WaitChat($"nh:in {tag}|", 60f);
                        yield return new WaitForSecondsRealtime(4f);
                        bool runner = NhRunner(key);
                        yield return NhStage(key, tag, runner);
                        Plugin.Log.LogInfo($"[AutoTest] 联机打首领 主机 {tag}：我{(runner ? "" : "不")}是主控，{NwLook(key)}");
                        // A and B: the one not running the boss strikes; the runner watches its health (B: with its Knight held).
                        foreach (var phase in new[] { "A", "B" })
                        {
                            bool held = phase == "B";
                            if (runner && held) { hero.RelinquishControl(); hero.StopAnimationControl(); }
                            int before = NhHealth(key);
                            string c0 = NhCounters();
                            NetChat.Say($"nh:strike{phase} {tag}|{(runner ? "client" : "host")}");
                            if (!runner) yield return NhStrike(key, 10f, 0.4f);
                            else
                                for (int s = 0; s < 10; s++)
                                {
                                    yield return new WaitForSecondsRealtime(1f);
                                    Plugin.Log.LogInfo($"[AutoTest] 联机打首领 主机 {tag} {phase} {s + 1}s 首领生命 {NhHealth(key)}");
                                }
                            yield return WaitChat($"nh:done{phase} {tag}|", 20f);
                            yield return new WaitForSecondsRealtime(1f);
                            int after = NhHealth(key);
                            if (runner && held) { hero.RegainControl(); hero.StartAnimationControl(); }
                            if (runner)
                                NetCheck(after < before, $"{tag} {phase}{(held ? "（主控的骑士被定住）" : "")}：只有对方打，首领生命 {before} → {after}；{NhCounters()}（之前 {c0}）");
                            else Plugin.Log.LogInfo($"[AutoTest] 联机打首领 主机 {tag} {phase} 打完：{NhCounters()}（之前 {c0}）");
                        }
                        // C: both, till it falls; a fall with much health left a second before is the "dies at once".
                        NetChat.Say($"nh:fight {tag}|");
                        int last = NhHealth(key), lastMax = last;
                        bool sudden = false;
                        var fight = mod.StartCoroutine(NhStrike(key, 40f, 0.5f, $"nh:end {tag}|"));
                        for (float t = Time.time + 41f; Time.time < t;)
                        {
                            yield return new WaitForSecondsRealtime(1f);
                            int hp = NhHealth(key);
                            bool down = KillsOf(key.Split('@')[0]) > kills0 && hp <= 0;
                            if (down && last > lastMax / 2) sudden = true;
                            if (down) break;
                            last = hp;
                        }
                        if (fight != null) mod.StopCoroutine(fight);
                        yield return new WaitForSecondsRealtime(2f);
                        int kills = KillsOf(key.Split('@')[0]) - kills0;
                        NetCheck(kills > 0 && !sudden, $"{tag} C 一起打：主机击杀 {kills}，倒下前一秒生命 {last}/{lastMax}{(sudden ? "（直接死了）" : "")}；{NwLook(key)}；{Enemies.DevReport()}");
                        NetChat.Say($"nh:end {tag}|");
                        yield return WaitChat($"nh:town {tag}|", 60f);
                        for (float ctl = Time.realtimeSinceStartup + 20f; !GameHooks.PlayerHasControl() && Time.realtimeSinceStartup < ctl;) yield return Tap(UnityEngine.InputSystem.Key.Space);
                    }
                NetChat.Say("nh:bye");
                yield return new WaitForSecondsRealtime(3f);
            }
            finally
            {
                done = true;
                Player.TerraStats.DevImmortal = false;
            }
        }

        IEnumerator NbHitClient(string scene, HeroController hero)
        {
            bool done = false;
            mod.StartCoroutine(NetKeepAlive(() => done));
            try
            {
                NbPrepare();
                yield return SealTravel("Town", "left1");
                NetChat.Say("nh:client ready");
                var handled = new HashSet<string>();
                float idle = Time.realtimeSinceStartup;
                while (!NetChat.DevHas("nh:bye") && Time.realtimeSinceStartup - idle < 500f)
                {
                    string tag = NetChat.DevTexts().Select(l =>
                    {
                        int i = l.IndexOf("nh:go ");
                        if (i < 0) return null;
                        string rest = l.Substring(i + 6);
                        int bar = rest.IndexOf('|');
                        return bar < 0 ? null : rest.Substring(0, bar);
                    }).FirstOrDefault(t => t != null && !handled.Contains(t));
                    if (tag == null) { yield return null; continue; }
                    handled.Add(tag);
                    idle = Time.realtimeSinceStartup;
                    var bits = tag.Split(' ');
                    string key = bits[0], first = bits[1];
                    BossArena.DevWorldArena(key, out string gate, out var needs);
                    var seal = BossSummons.All.FirstOrDefault(s => s.Key == key);
                    Shop.DevStage = seal != null ? seal.Stage : 5;
                    if (NbRoom(GameManager.instance.sceneName) == NbRoom(BossArena.SceneOf(key))) yield return SealTravel("Town", "left1");
                    foreach (var (name, v) in needs ?? new (string, bool)[0]) PlayerData.instance.SetBool(name, v);
                    NwSetBeaten(key, false);
                    int kills0 = KillsOf(key.Split('@')[0]);
                    NetChat.Say($"nh:set {tag}|");
                    if (first == "host") { yield return WaitChat($"nh:hostin {tag}|", 60f); yield return new WaitForSecondsRealtime(3f); }
                    yield return GoTo(BossArena.SceneOf(key), gate);
                    yield return NhWalkIn(key);
                    NetChat.Say($"nh:in {tag}|");
                    yield return new WaitForSecondsRealtime(4f);
                    bool runner = NhRunner(key);
                    yield return NhStage(key, tag, runner);
                    Plugin.Log.LogInfo($"[AutoTest] 联机打首领 客户端 {tag}：我{(runner ? "" : "不")}是主控，{NwLook(key)}");
                    foreach (var phase in new[] { "A", "B" })
                    {
                        yield return WaitChat($"nh:strike{phase} {tag}|", 40f);
                        bool held = phase == "B";
                        if (runner && held) { hero.RelinquishControl(); hero.StopAnimationControl(); }
                        int before = NhHealth(key);
                        string c0 = NhCounters();
                        if (!runner) yield return NhStrike(key, 10f, 0.4f);
                        else
                            for (int s = 0; s < 10; s++)
                            {
                                yield return new WaitForSecondsRealtime(1f);
                                Plugin.Log.LogInfo($"[AutoTest] 联机打首领 客户端 {tag} {phase} {s + 1}s 首领生命 {NhHealth(key)}");
                            }
                        NetChat.Say($"nh:done{phase} {tag}|");
                        yield return new WaitForSecondsRealtime(1f);
                        int after = NhHealth(key);
                        if (runner && held) { hero.RegainControl(); hero.StartAnimationControl(); }
                        if (runner)
                            NetCheck(after < before, $"{tag} {phase}{(held ? "（主控的骑士被定住）" : "")}：只有对方打，首领生命 {before} → {after}；{NhCounters()}（之前 {c0}）");
                        else Plugin.Log.LogInfo($"[AutoTest] 联机打首领 客户端 {tag} {phase} 打完：{NhCounters()}（之前 {c0}）");
                    }
                    yield return WaitChat($"nh:fight {tag}|", 30f);
                    var fight = mod.StartCoroutine(NhStrike(key, 40f, 0.5f, $"nh:end {tag}|"));
                    yield return WaitChat($"nh:end {tag}|", 90f);
                    if (fight != null) mod.StopCoroutine(fight);
                    yield return new WaitForSecondsRealtime(1f);
                    int kills = KillsOf(key.Split('@')[0]) - kills0;
                    NetCheck(kills > 0, $"{tag} C 一起打：客户端击杀 {kills}；{NwLook(key)}；{Enemies.DevReport()}");
                    for (float ctl = Time.realtimeSinceStartup + 20f; !GameHooks.PlayerHasControl() && Time.realtimeSinceStartup < ctl;) yield return Tap(UnityEngine.InputSystem.Key.Space);
                    NetChat.Say($"nh:town {tag}|");
                }
            }
            finally
            {
                done = true;
                Player.TerraStats.DevImmortal = false;
            }
        }
    }
}
