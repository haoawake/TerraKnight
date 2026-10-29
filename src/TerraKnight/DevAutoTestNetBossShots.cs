using System.Collections;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.Net;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Suites "nbshot_host" and "nbshot_client" (port TK_NS_PORT, default 7801; the user's 10-27 "boss对联机队友依旧不造成伤害，
    /// 比如诺斯克的弹幕"): two players, a world boss (TK_NS_KEY, default Nosk "MimicSpider"); the host walks in first, so its game
    /// runs the boss, then the client. For 50 s the client stands in the arena, healed every frame: the attack hitboxes it
    /// gets from the host (Net.EnemyAttacks ghosts, the most at once) and the blows they land on it. TK_NS_OLD=1: the host
    /// sends its scenes' own objects only (EnemyAttacks.DevSceneOnly, as before: the pool's globs and orbs left out). Lines
    /// "[AutoTest] 联机首领弹幕 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator NbShotBoot()
        {
            int port = int.TryParse(System.Environment.GetEnvironmentVariable("TK_NS_PORT"), out int envPort) ? envPort : 7801;
            if (suite == "nbshot_host")
            {
                EnemyAttacks.DevSceneOnly = System.Environment.GetEnvironmentVariable("TK_NS_OLD") == "1";
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

        IEnumerator NbShot(bool host)
        {
            bool done = false, measuring = false;
            // The keep-alive makes the Knight invincible every frame: off while the client stands in the boss's attacks.
            mod.StartCoroutine(NetKeepAlive(() => done || measuring));
            string key = System.Environment.GetEnvironmentVariable("TK_NS_KEY") ?? "MimicSpider";
            try
            {
                NbPrepare();
                if (!host) NetChat.Say("ns:client ready");
                if (!BossArena.DevWorldArena(key, out string gate, out var needs)) { NetCheck(false, $"{key} 没有世界竞技场"); yield break; }
                var seal = BossSummons.All.FirstOrDefault(s => s.Key == key);
                Shop.DevStage = seal != null ? seal.Stage : 5;
                foreach (var (name, v) in needs ?? new (string, bool)[0]) PlayerData.instance.SetBool(name, v);
                NwSetBeaten(key, false);
                if (host)
                {
                    yield return SealTravel("Town", "left1");
                    yield return WaitChat("ns:client ready", 120f);
                    yield return GoTo(BossArena.SceneOf(key), gate);
                    yield return NhWalkIn(key);
                    NetChat.Say("ns:hostin|");
                    yield return WaitChat("ns:in|", 90f);
                    Plugin.Log.LogInfo($"[AutoTest] 联机首领弹幕 主机：我{(NhRunner(key) ? "" : "不")}是主控，旧规则 {EnemyAttacks.DevSceneOnly}");
                    for (float t = Time.time + 55f; Time.time < t && !NetChat.DevHas("ns:done|");)
                    {
                        mod.Stats.FullHeal();
                        yield return null;
                    }
                }
                else
                {
                    yield return WaitChat("ns:hostin|", 180f);
                    yield return new WaitForSecondsRealtime(2f);
                    yield return GoTo(BossArena.SceneOf(key), gate);
                    yield return NhWalkIn(key);
                    NetChat.Say("ns:in|");
                    bool runner = NhRunner(key);
                    int hits0 = EnemyAttacks.DevHits, received0 = EnemyAttacks.DevReceived, most = 0, over0 = EnemyAttacks.DevOverlaps, skip0 = EnemyAttacks.DevTickSkips, lost = 0;
                    var hero = HeroController.instance;
                    // Hurt for real (healed when low): the blows are counted as life lost.
                    measuring = true;
                    yield return null;
                    Player.TerraStats.DevImmortal = false;
                    PlayerData.instance.isInvincible = false;
                    for (float t = Time.time + 50f; Time.time < t;)
                    {
                        // Healed only when low: what it loses is counted.
                        int life = mod.Stats.Life;
                        yield return null;
                        if (mod.Stats.Life < life) lost += life - mod.Stats.Life;
                        if (mod.Stats.Life < mod.Stats.LifeMax / 2) mod.Stats.FullHeal();
                        most = Mathf.Max(most, EnemyAttacks.DevLiveGhosts);
                        yield return null;
                    }
                    int hits = EnemyAttacks.DevHits - hits0;
                    NetChat.Say("ns:done|");
                    bool old = System.Environment.GetEnvironmentVariable("TK_NS_OLD") == "1";
                    Plugin.Log.LogInfo($"[AutoTest] 联机首领弹幕 客户端（{key}，主机{(old ? "旧规则" : "新规则")}）：我{(runner ? "是" : "不是")}主控，收到判定 {EnemyAttacks.DevReceived - received0} 条，同时最多 {most} 个，重叠 {EnemyAttacks.DevOverlaps - over0} 次，跳过 {EnemyAttacks.DevTickSkips - skip0}，掉血 {lost}，被打中 {hits} 下 {(!runner && lost > 0 ? "通过" : "不通过")}");
                    Shot(GameManager.instance.sceneName, $"nbshot_{(old ? "old" : "new")}");
                }
            }
            finally { done = true; }
        }
    }
}
