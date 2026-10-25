using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.Net;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Suites "netwalk_host_KEY.KEY…" and "netwalk_client" (port 7798): two players walking into a world boss's room, not by a
    /// seal (players' report 2026-10-20: "boss不同步，还有boss见面就自杀了然后不算图鉴也打不了"). For each boss, each case
    /// "beaten:first": whose save has the boss beaten already (none, host, client) and who walks in first (host, client), the
    /// other three seconds later. Both log each second what they see of the boss (each part: on, dead, health, a puppet, its
    /// owner), then both strike it for up to a minute; a summary per side (its kills, what stands, the owner).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        const int NetWalkPort = 7798;

        static readonly string[] netWalkCases = { "none:host", "host:host", "none:client" };

        IEnumerator NetWalkBoot()
        {
            if (suite.StartsWith("netwalk_host"))
            {
                bool ok = NetSession.Host(NetWalkPort, "", 8, out string error);
                NetCheck(ok, $"开服 {NetWalkPort} {error}");
                for (float t = 0f; t < 10f && !NetSession.Active; t += Time.unscaledDeltaTime) yield return null;
                NetCheck(NetSession.Active, "主机加入自己的服务器");
                yield break;
            }
            float until = Time.realtimeSinceStartup + 120f;
            while (!NetSession.Active && Time.realtimeSinceStartup < until)
            {
                NetSession.Join("127.0.0.1", NetWalkPort);
                for (float t = 0f; t < 6f && NetSession.State != NetSession.Phase.Off && !NetSession.Active; t += Time.unscaledDeltaTime) yield return null;
                if (!NetSession.Active) yield return new WaitForSecondsRealtime(3f);
            }
            NetCheck(NetSession.Active, $"客户端加入主机（槽位 {NetSession.MySlot}）");
        }

        /// <summary>The Knight set down in the middle of the boss's fighting ground (TerrainRegion.OwnZone), on its floor, as walking in brings him there.</summary>
        IEnumerator NwIntoArena(string key)
        {
            var hero = HeroController.instance;
            var region = World.TerrainSystem.Find(BossArena.SceneOf(key));
            if (hero == null || region?.OwnZone == null) { Plugin.Log.LogInfo($"[AutoTest] 联机走进 {key} 没有战斗范围"); yield break; }
            var z = region.OwnZone.Value;
            var hit = Physics2D.Raycast(new Vector2(z.center.x, z.center.y), Vector2.down, z.height, 1 << 8);
            hero.transform.position = hit.collider != null ? hit.point + Vector2.up * 1.3f : z.center;
            Plugin.Log.LogInfo($"[AutoTest] 联机走进 {key} 走到竞技场中间 {(Vector2)hero.transform.position:F1}（范围 {z.xMin:F0}~{z.xMax:F0} × {z.yMin:F0}~{z.yMax:F0}）");
            yield return new WaitForSecondsRealtime(1f);
        }

        /// <summary>One line of what this side sees of the boss now.</summary>
        string NwLook(string key)
        {
            string room = BossArena.SceneOf(key);
            string Own(string s) => NetSession.Owners.TryGetValue(s, out int o) ? (o == NetSession.MySlot ? "我" : o.ToString()) : "-";
            var parts = NbParts(key, false).Where(hm => hm.name != "Head" || UI.EnemyHover.RecordOf(hm) != null)
                .Select(hm => $"{hm.name}[{(hm.gameObject.activeInHierarchy ? "开" : "关")}{(hm.isDead ? " 死" : "")} {hm.hp}{(Net.Enemies.IsPuppet(hm) ? " 傀儡" : "")}{(EnemyStats.Defeated(hm) ? " 已败" : "")}]");
            return $"{GameManager.instance.sceneName} 主控 {Own(room)}/{Own(room + "_boss")} 击杀 {KillsOf(key.Split('@')[0])} 控制 {GameHooks.PlayerHasControl()} | {string.Join(" ", parts)}";
        }

        /// <summary>Strikes every part of the boss standing, a third of a second apart (a puppet's blows go to its owner), until it is counted beaten or the time is up.</summary>
        IEnumerator NwFight(string key, string side, int kills0, float seconds, float every)
        {
            string record = key.Split('@')[0];
            float until = Time.time + seconds, next = 0f, look = 0f;
            while (Time.time < until)
            {
                if (KillsOf(record) > kills0 && !NbParts(key, true).Any(hm => !hm.isDead && hm.hp > 0 && !EnemyStats.Defeated(hm) && UI.EnemyHover.RecordOf(hm) == record)) break;
                if (Time.time >= look) { look = Time.time + 3f; Plugin.Log.LogInfo($"[AutoTest] 联机走进 {side} {key} 打：{NwLook(key)}"); }
                if (!GameHooks.PlayerHasControl() && Time.time >= next) { next = Time.time + 1f; yield return Tap(UnityEngine.InputSystem.Key.Space); continue; }
                if (Time.time >= next)
                {
                    next = Time.time + every;
                    var h = HeroController.instance;
                    foreach (var hm in NbParts(key, true))
                    {
                        if (hm.isDead || EnemyStats.Defeated(hm)) continue;
                        var col = hm.GetComponent<Collider2D>();
                        if (col == null || !col.enabled) col = hm.GetComponentsInChildren<Collider2D>().FirstOrDefault(c => c.enabled && c.GetComponentInParent<HealthManager>() == hm);
                        if (col == null) continue;
                        var info = EnemyStats.Of(hm);
                        int dmg = Mathf.Max(150, EnemyStats.LifeWhole(info) / 150 + info.Defense);
                        mod.Melee.HitWith(col, hm.name == "Head" ? dmg * 3 : dmg, 0f, 0f, 1f, h.GetComponent<BoxCollider2D>().bounds.center);
                    }
                }
                yield return null;
            }
        }

        void NwSummary(string key, string side, string kase, int kills0, bool everOn)
        {
            string record = key.Split('@')[0];
            var standing = NbParts(key, true).Where(hm => !hm.isDead && hm.hp > 0 && !EnemyStats.Defeated(hm) && UI.EnemyHover.RecordOf(hm) == record)
                .Select(hm => $"{hm.name}:{hm.hp}{(Net.Enemies.IsPuppet(hm) ? "(傀儡)" : "")}").ToList();
            int kills = KillsOf(record) - kills0;
            Plugin.Log.LogInfo($"[AutoTest] 联机走进 小结 {side} {key} {kase}：击杀 {kills}，见过首领 {everOn}，还站着 [{string.Join(" ", standing)}]，{NwLook(key)}，{Net.Enemies.DevReport()} {(kills > 0 && standing.Count == 0 ? "通过" : "不通过")}");
        }

        /// <summary>This side's save for the case: the boss beaten or not (its flags as a win writes them).</summary>
        void NwSetBeaten(string key, bool beaten)
        {
            BossArena.DevSetBeaten(key, beaten);
            string record = key.Split('@')[0];
            Bestiary.Kills[record] = beaten ? Mathf.Max(1, KillsOf(record)) : 0;
        }

        IEnumerator NetWalkHost(string scene, HeroController hero)
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
                yield return WaitChat("nw:client ready", 120f);
                var keys = suite.Length > "netwalk_host_".Length ? suite.Substring("netwalk_host_".Length).Split('.') : new[] { "FalseKnight" };
                // "set1": twelve bosses players meet early and in the middle (a short name: a long one makes the run's paths too long).
                if (keys.Length == 1 && keys[0] == "set1")
                    keys = new[] { "Hornet", "Mawlek", "BigFly", "BigBuzzer", "MantisLord", "MegaMossCharger", "MageLord", "DungDefender", "MegaJellyfish", "BlackKnight", "HiveKnight", "MimicSpider" };
                // "set2": the two that went wrong (a beaten save's room, a special death) and two that didn't.
                if (keys.Length == 1 && keys[0] == "set2") keys = new[] { "MegaMossCharger", "MimicSpider", "FalseKnight", "Hornet" };
                // "set3": the room-state and "encountered" fights (the Gruz Mother, Brooding Mawlek, Nosk) and two others.
                if (keys.Length == 1 && keys[0] == "set3") keys = new[] { "MimicSpider", "BigFly", "Mawlek", "MegaMossCharger", "Hornet" };
                if (keys.Length == 1 && keys[0] == "set4") keys = new[] { "MimicSpider", "BigFly" };
                int n = 0;
                foreach (var key in keys)
                    foreach (var kase in netWalkCases)
                    {
                        n++;
                        var parts = kase.Split(':');
                        string tag = $"{key} {kase} #{n}";
                        if (!BossArena.DevWorldArena(key, out string gate, out var needs)) { NetCheck(false, $"{key} 没有世界竞技场"); continue; }
                        var seal = BossSummons.All.FirstOrDefault(s => s.Key == key);
                        Shop.DevStage = seal != null ? seal.Stage : 5;
                        // Out of the arena first: leaving it saves its states (a win's), which would undo the case's records.
                        if (NbRoom(GameManager.instance.sceneName) == NbRoom(BossArena.SceneOf(key))) yield return SealTravel("Town", "left1");
                        foreach (var (name, v) in needs ?? new (string, bool)[0]) PlayerData.instance.SetBool(name, v);
                        NwSetBeaten(key, parts[0] == "host");
                        int kills0 = KillsOf(key.Split('@')[0]);
                        NetChat.Say($"nw:go {tag}|");
                        yield return WaitChat($"nw:set {tag}|", 30f);
                        if (parts[1] == "client") { yield return WaitChat($"nw:in {tag}|", 60f); yield return new WaitForSecondsRealtime(3f); }
                        yield return GoTo(BossArena.SceneOf(key), gate);
                        yield return NwIntoArena(key);
                        NetChat.Say($"nw:hostin {tag}|");
                        if (parts[1] == "host") yield return WaitChat($"nw:in {tag}|", 60f);
                        bool everOn = false;
                        for (int i = 0; i < 5; i++)
                        {
                            Plugin.Log.LogInfo($"[AutoTest] 联机走进 主机 {tag} 看 {i}s：{NwLook(key)}");
                            everOn |= NbParts(key, true).Any(hm => !hm.isDead && UI.EnemyHover.RecordOf(hm) == key.Split('@')[0]);
                            yield return new WaitForSecondsRealtime(1f);
                        }
                        NetChat.Say($"nw:fight {tag}|");
                        yield return NwFight(key, "主机", kills0, 45f, 0.35f);
                        yield return new WaitForSecondsRealtime(2f);
                        NwSummary(key, "主机", kase, kills0, everOn);
                        NetChat.Say($"nw:end {tag}|");
                        yield return WaitChat($"nw:town {tag}|", 60f);
                        for (float ctl = Time.realtimeSinceStartup + 20f; !GameHooks.PlayerHasControl() && Time.realtimeSinceStartup < ctl;) yield return Tap(UnityEngine.InputSystem.Key.Space);
                    }
                NetChat.Say("nw:bye");
                yield return new WaitForSecondsRealtime(3f);
            }
            finally
            {
                done = true;
                Player.TerraStats.DevImmortal = false;
            }
        }

        IEnumerator NetWalkClient(string scene, HeroController hero)
        {
            bool done = false;
            mod.StartCoroutine(NetKeepAlive(() => done));
            try
            {
                NbPrepare();
                yield return SealTravel("Town", "left1");
                NetChat.Say("nw:client ready");
                var handled = new HashSet<string>();
                float idle = Time.realtimeSinceStartup;
                while (!NetChat.DevHas("nw:bye") && Time.realtimeSinceStartup - idle < 400f)
                {
                    string tag = NetChat.DevTexts().Select(l =>
                    {
                        int i = l.IndexOf("nw:go ");
                        if (i < 0) return null;
                        string rest = l.Substring(i + 6);
                        int bar = rest.IndexOf('|');
                        return bar < 0 ? null : rest.Substring(0, bar);
                    }).FirstOrDefault(t => t != null && !handled.Contains(t));
                    if (tag == null) { yield return null; continue; }
                    handled.Add(tag);
                    idle = Time.realtimeSinceStartup;
                    var bits = tag.Split(' ');
                    string key = bits[0];
                    var parts = bits[1].Split(':');
                    BossArena.DevWorldArena(key, out string gate, out var needs);
                    var seal = BossSummons.All.FirstOrDefault(s => s.Key == key);
                    Shop.DevStage = seal != null ? seal.Stage : 5;
                    if (NbRoom(GameManager.instance.sceneName) == NbRoom(BossArena.SceneOf(key))) yield return SealTravel("Town", "left1");
                    foreach (var (name, v) in needs ?? new (string, bool)[0]) PlayerData.instance.SetBool(name, v);
                    NwSetBeaten(key, parts[0] == "client");
                    int kills0 = KillsOf(key.Split('@')[0]);
                    NetChat.Say($"nw:set {tag}|");
                    if (parts[1] == "host") { yield return WaitChat($"nw:hostin {tag}|", 60f); yield return new WaitForSecondsRealtime(3f); }
                    yield return GoTo(BossArena.SceneOf(key), gate);
                    yield return NwIntoArena(key);
                    NetChat.Say($"nw:in {tag}|");
                    bool everOn = false;
                    for (int i = 0; i < 5 && !NetChat.DevHas($"nw:fight {tag}|"); i++)
                    {
                        Plugin.Log.LogInfo($"[AutoTest] 联机走进 客户端 {tag} 看 {i}s：{NwLook(key)}");
                        everOn |= NbParts(key, true).Any(hm => !hm.isDead && UI.EnemyHover.RecordOf(hm) == key.Split('@')[0]);
                        yield return new WaitForSecondsRealtime(1f);
                    }
                    yield return WaitChat($"nw:fight {tag}|", 30f);
                    var fight = mod.StartCoroutine(NwFight(key, "客户端", kills0, 45f, 0.6f));
                    yield return WaitChat($"nw:end {tag}|", 90f);
                    mod.StopCoroutine(fight);
                    NwSummary(key, "客户端", bits[1], kills0, everOn);
                    for (float ctl = Time.realtimeSinceStartup + 20f; !GameHooks.PlayerHasControl() && Time.realtimeSinceStartup < ctl;) yield return Tap(UnityEngine.InputSystem.Key.Space);
                    NetChat.Say($"nw:town {tag}|");
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
