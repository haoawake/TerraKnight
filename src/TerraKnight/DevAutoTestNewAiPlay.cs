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
    /// Dev (suite "newaiplay"): a new save's way through the game with the new enemy AI, boss by boss in the order a player
    /// meets them, each met for the first time (intro and all, BossArena.Fight without a rematch) and fought to its death by a
    /// player who can't die but takes every hit: its kit's moves, the hits it lands, its failures and rescues, then after the
    /// win the death played out, the player given control back, no black or white screen, the win written down (the book,
    /// the progression), the errors logged. The Hollow Knight last: his death goes on to the ending. TK_PLAY_BOSSES picks some
    /// (a;b), TK_PLAY_SECONDS the most a fight may take (150 s).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly string[] PlayOrder =
        {
            "FalseKnight", "Hornet", "MegaMossCharger", "BigFly", "Mawlek", "MageKnight", "BigBuzzer", "MantisLord", "MageLord",
            "GhostXero", "GhostAladar", "DungDefender", "FlukeMother", "InfectedKnight", "GhostMarmu", "MimicSpider", "GhostNoEyes",
            "MegaBeamMiner", "BlackKnight", "MegaJellyfish", "JarCollector", "GhostHu", "Hornet@4", "GhostMarkoth", "GhostGalien",
            "TraitorLord", "HiveKnight", "Oblobble", "GreyPrince", "Grimm", "NightmareGrimm", "WhiteDefender", "LobsterLancer",
            "NailBros", "Nailsage", "Paintmaster", "HollowKnightPrime", "HollowKnight",
        };

        IEnumerator NewAiPlay()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            mod.ApplyCreative(false);
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            World.EnemyAiMode.Current = World.EnemyAiKind.New;
            string pick = Environment.GetEnvironmentVariable("TK_PLAY_BOSSES");
            var keys = string.IsNullOrWhiteSpace(pick) ? PlayOrder : pick.Split(';');
            float most = float.TryParse(Environment.GetEnvironmentVariable("TK_PLAY_SECONDS"), out var s) ? s : 150f;
            var errors = new List<string>();
            void OnLog(string msg, string stack, LogType type)
            {
                if ((type == LogType.Exception || type == LogType.Error && msg.Contains("Exception")) && errors.Count < 30)
                    errors.Add((msg.Length > 140 ? msg.Substring(0, 140) : msg) + " @ " + (stack ?? "").Split('\n').FirstOrDefault(l => l.Contains("TerraKnight")));
            }
            Application.logMessageReceived += OnLog;
            int good = 0, all = 0;
            var bad = new List<string>();
            try
            {
                foreach (string key in keys.Where(k => !string.IsNullOrWhiteSpace(k)))
                {
                    all++;
                    errors.Clear();
                    string record = key.Split('@')[0];
                    var gm = GameManager.instance;
                    PlayerData.instance.isInvincible = false;
                    int kills0 = Bestiary.KillsOf(key), moves0 = BossAi.DevMoves, fails0 = BossAi.DevFails, unstuck0 = BossAi.DevUnstuck;
                    bool first = kills0 == 0;
                    if (BossArena.Has(key)) BossArena.Fight(key, false);
                    else
                    {
                        string scene = EnemyDrops.SceneOf(record);
                        if (string.IsNullOrEmpty(scene)) { Plugin.Log.LogInfo($"[NEWAI_PLAY] {key} 没有场景，跳过"); bad.Add(key + " 没有场景"); continue; }
                        yield return GoTo(scene, EntryGateOf(scene));
                    }
                    // Up and fighting (a wave before the boss put down as a fighter would).
                    BossKit kit = null;
                    float until = Time.realtimeSinceStartup + 80f, clearAt = Time.realtimeSinceStartup + 10f;
                    while (Time.realtimeSinceStartup < until)
                    {
                        kit = KitOf(record);
                        if (kit != null && !gm.IsInSceneTransition) break;
                        if (Time.realtimeSinceStartup >= clearAt && !gm.IsInSceneTransition)
                        {
                            clearAt = Time.realtimeSinceStartup + 2f;
                            foreach (var h in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                            {
                                var info = EnemyStats.Of(h);
                                if (h == null || h.isDead || !h.gameObject.activeInHierarchy || info == null || info.Boss || info.Record == record) continue;
                                if (!h.gameObject.scene.name.StartsWith(gm.sceneName)) continue;
                                h.Die(null, AttackTypes.Generic, true);
                            }
                        }
                        yield return null;
                    }
                    if (kit == null)
                    {
                        Plugin.Log.LogInfo($"[NEWAI_PLAY] {key} 没等到首领（{gm.sceneName}） 不通过");
                        bad.Add(key + " 没等到首领");
                        Shot(gm.sceneName, "newaiplay_" + key.Replace('@', '_') + "_none");
                        continue;
                    }
                    var hero = HeroController.instance;
                    var hbody = hero.GetComponent<Rigidbody2D>();
                    string fightScene = gm.sceneName;
                    // A dream's or Godhome's fight: its end wakes him back where he was (and its kill isn't the world's
                    // progression).
                    bool dreamy = BossSceneController.IsBossScene;
                    bool worldKill = !ProgressionLoot.GodhomeScene(fightScene);
                    // Where the fight's start set him down: back there when the place by the boss is under water (the
                    // Flukemarm hangs over a pool) or he is out of reach of it.
                    yield return new WaitForSeconds(0.5f);
                    Vector2 home = hero.transform.position;
                    float t0 = Time.time, lastHurt = Player.TerraHurt.LastHurtAt, nextStrike = Time.time + 3f, nextLook = 0f, nextTrace = 0f;
                    int hurts = 0, looked = 0;
                    // Fought: every boss body of the fight struck a tenth of its life a second and a half (its own invulnerable
                    // spells refuse the blows as they would a player's), the player kept near it.
                    while (Time.time - t0 < most && Bestiary.KillsOf(key) <= kills0)
                    {
                        if (Player.TerraHurt.LastHurtAt != lastHurt) { lastHurt = Player.TerraHurt.LastHurtAt; hurts++; }
                        if (Environment.GetEnvironmentVariable("TK_PLAY_TRACE") == "1" && Time.time >= nextTrace)
                        {
                            nextTrace = Time.time + 3f;
                            int flies = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None).Count(h => h != null && !h.isDead && h.gameObject.activeInHierarchy && h.name.StartsWith("Fluke Fly") && !h.name.Contains("Spawner"));
                            Plugin.Log.LogInfo($"[NEWAI_PLAY] {key} {Time.time - t0:F0}s 状态 {kit?.StateNow}，血 {kit?.Hm?.hp}，能动 {GameHooks.PlayerHasControl()}，飞吸虫 {flies}，玩家 {(Vector2)hero.transform.position:F1}");
                        }
                        if (mod.Stats.Life < mod.Stats.LifeMax / 2) mod.Stats.FullHeal();
                        if (Time.time >= nextLook)
                        {
                            nextLook = Time.time + 1f;
                            var k2 = KitOf(record);
                            if (k2 != null) kit = k2;
                            if (kit != null && kit.Hm != null && !kit.Hm.isDead)
                            {
                                var me = (Vector2)hero.transform.position;
                                if (!kit.Arena.Contains(me) || Vector2.Distance(me, kit.Me) > 14f)
                                {
                                    var at = kit.ClampToArena(kit.Me, 3f);
                                    at.y = kit.GroundBelow(at) + 1.2f;
                                    at.x += at.x - 4f > kit.Arena.xMin + 1f ? -4f : 4f;
                                    if (Fishing.NativeLiquidAt(at, out _) || World.TerraWorld.LiquidAt(World.CellGrid.WorldToCell(at).x, World.CellGrid.WorldToCell(at).y) > 0) at = home;
                                    hero.transform.position = new Vector3(at.x, at.y, hero.transform.position.z);
                                    if (hbody != null) hbody.linearVelocity = Vector2.zero;
                                }
                            }
                        }
                        if (Time.time >= nextStrike && Time.time - t0 > 6f)
                        {
                            nextStrike = Time.time + 1.5f;
                            foreach (var h in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                            {
                                if (h == null || h.isDead || !h.gameObject.activeInHierarchy) continue;
                                var info = EnemyStats.Of(h);
                                if (info == null || !(info.Boss || info.Record == record)) continue;
                                // Its own body, else the part of it that takes the nail (the Moss Charger's is a child).
                                var c = h.GetComponent<Collider2D>();
                                if (c == null || !c.enabled) c = h.GetComponentsInChildren<Collider2D>().FirstOrDefault(x => x.enabled && x.GetComponentInParent<HealthManager>() == h);
                                if (c == null) { if (looked++ < 6) Plugin.Log.LogInfo($"[NEWAI_PLAY] {key} 打不到 {h.name}：没有开着的碰撞体"); continue; }
                                bool hit = mod.Melee.HitWith(c, Mathf.Max(1, EnemyStats.LifeMax(info) / 10), 0f, 0f, 1f, hero.transform.position, 9999);
                                if (!hit && looked++ < 6)
                                    Plugin.Log.LogInfo($"[NEWAI_PLAY] {key} 没打中 {h.name}：在地图外 {World.ArenaGuard.OffMap(h)}，隔着战斗门 {World.ArenaGuard.Outside(h)}，无敌 {h.IsInvincible}，状态 {kit?.StateNow}，血 {h.hp}");
                            }
                        }
                        yield return null;
                    }
                    float fought = Time.time - t0;
                    bool won = Bestiary.KillsOf(key) > kills0;
                    // After the win: its death played out, the player's again, the screen clear.
                    bool control = false, clear = false;
                    string fade = "?";
                    float white = 0f;
                    bool ending = false;
                    if (won)
                    {
                        float end = Time.realtimeSinceStartup + 45f;
                        while (Time.realtimeSinceStartup < end)
                        {
                            var f = GameCameras.instance != null ? GameCameras.instance.cameraFadeFSM : null;
                            fade = f != null ? f.ActiveStateName : "?";
                            white = WhiteOverlay();
                            ending = gm.sceneName.StartsWith("Cinematic_Ending") || gm.sceneName.StartsWith("End_");
                            if (ending) break;
                            control = GameHooks.PlayerHasControl() && !gm.IsInSceneTransition;
                            clear = fade == "Normal" && white < 0.05f;
                            // Its reward (and the progression it writes) comes when its death has played out (the False
                            // Knight's burst, seconds after the kill).
                            bool credited = !worldKill || !VanillaBossProgression.All.ContainsKey(key) || VanillaBossProgression.Cleared(key);
                            bool back = !dreamy || gm.sceneName != fightScene;
                            if (control && clear && credited && back && Time.realtimeSinceStartup > end - 37f) break;
                            yield return null;
                        }
                    }
                    bool progressed = !worldKill || !VanillaBossProgression.All.ContainsKey(key) || VanillaBossProgression.Cleared(key);
                    int moves = BossAi.DevMoves - moves0;
                    var mine = errors.Where(e => e.Contains("TerraKnight")).ToList();
                    bool pass = won && (ending || control && clear) && progressed && mine.Count == 0;
                    if (pass) good++;
                    else bad.Add(key);
                    Plugin.Log.LogInfo($"[NEWAI_PLAY] {key} {(first ? "第一次" : "又一次")}在 {fightScene}：打了 {fought:F0} 秒 {(won ? "赢了" : "没打死")}，" +
                        $"首领招式 {moves} 次，打中玩家 {hurts} 次，招式失败 {BossAi.DevFails - fails0}，脱困 {BossAi.DevUnstuck - unstuck0}；" +
                        $"之后 {gm.sceneName}{(ending ? "（结局）" : "")}：能动 {control}，镜头 {fade}，白 {white:F2}，进度 {progressed}，" +
                        $"图鉴 {Bestiary.KillsOf(key)}；mod 异常 {mine.Count}{(mine.Count > 0 ? "：" + string.Join(" | ", mine.Take(3)) : "")} {(pass ? "通过" : "不通过")}");
                    if (!pass) Shot(gm.sceneName, "newaiplay_" + key.Replace('@', '_'));
                    if (ending) break;
                }
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
            }
            Plugin.Log.LogInfo($"[NEWAI_PLAY] 新版AI通关 {good}/{all} {(bad.Count == 0 ? "全部通过" : "不通过：" + string.Join("、", bad))}");
        }
    }
}
