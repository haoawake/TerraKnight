using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suites "boss1", "boss2"): the bosses and elites out in the world, each in its own arena.
    /// The player is brought in by a gate of the scene and put a few steps from the boss to wake it;
    /// once it is up it is struck down (as often as its phases need), and then: whether it died as it
    /// should (the book's kill), whether the game gave control back after its death, and the errors
    /// logged on the way.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly (string Record, string Scene)[][] bossParts =
        {
            new[]
            {
                ("FalseKnight", "Crossroads_10_boss"), ("BigFly", "Crossroads_04"), ("Mawlek", "Crossroads_09"), ("BigBuzzer", "Fungus1_20_v02"),
                ("MegaMossCharger", "Fungus1_29"), ("MantisLord", "Fungus2_15_boss"), ("MageKnight", "Ruins1_23"), ("MageLord", "Ruins1_24_boss"),
                ("DungDefender", "Waterways_05_boss"), ("FlukeMother", "Waterways_12_boss"),
            },
            new[]
            {
                ("MegaBeamMiner", "Mines_18_boss"), ("MegaJellyfish", "Fungus3_archive_02_boss"), ("BlackKnight", "Ruins2_03_boss"), ("JarCollector", "Ruins2_11_boss"),
                ("MimicSpider", "Deepnest_32"), ("TraitorLord", "Fungus3_23_boss"), ("HiveKnight", "Hive_05"), ("Hornet", "Deepnest_East_Hornet_boss"),
                ("InfectedKnight", "Abyss_19"),
            },
        };

        IEnumerator BossTest(HeroController hero, int part)
        {
            var errors = new List<string>();
            void OnLog(string msg, string stack, LogType type)
            {
                if ((type == LogType.Exception || (type == LogType.Error && msg.Contains("Exception")) || msg.StartsWith("[Error")) && errors.Count < 20) errors.Add(msg.Length > 160 ? msg.Substring(0, 160) : msg);
            }
            Application.logMessageReceived += OnLog;
            int good = 0;
            var bad = new System.Text.StringBuilder();
            const int terrain = 1 << World.ChunkColliders.TerrainLayer;
            var list = bossParts[part - 1];
            try
            {
                foreach (var (record, scene) in list)
                {
                    var gm = GameManager.instance;
                    // An arena "_boss" scene is laid over its room by the room itself: the room is where to go.
                    string room = scene.EndsWith("_boss") ? scene.Substring(0, scene.Length - 5) : scene;
                    int kills0 = 0;
                    foreach (var v in Items.Bestiary.Kills.Values) kills0 += v;
                    PlayerData.instance.isInvincible = true;
                    float t0 = Time.time;
                    while (gm.IsInSceneTransition && Time.time - t0 < 10f) yield return null;
                    errors.Clear();
                    gm.BeginSceneTransition(new GameManager.SceneLoadInfo
                    {
                        SceneName = room, EntryGateName = EntryGateOf(room), HeroLeaveDirection = GlobalEnums.GatePosition.unknown,
                        EntryDelay = 0f, WaitForSceneTransitionCameraFade = true, Visualization = GameManager.SceneLoadVisualizations.Default,
                    });
                    t0 = Time.time;
                    yield return new WaitForSeconds(0.5f);
                    while ((gm.sceneName != room || gm.IsInSceneTransition || !GameHooks.PlayerHasControl()) && Time.time - t0 < 20f) yield return null;
                    bool arrived = gm.sceneName == room;
                    yield return new WaitForSeconds(1.5f);

                    // The boss, awake or not yet.
                    List<HealthManager> Bosses(bool includeInactive)
                    {
                        var found = new List<HealthManager>();
                        for (int s = 0; s < USM.sceneCount; s++)
                        {
                            var sc = USM.GetSceneAt(s);
                            if (!sc.IsValid() || !sc.isLoaded) continue;
                                foreach (var root in sc.GetRootGameObjects())
                            {
                                // The boss and whatever else takes its hits under the same root (False Knight's head).
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
                    var all = Bosses(true);
                    bool found0 = all.Count > 0;
                    var h = HeroController.instance;
                    int walk = 0;
                    if (found0)
                    {
                        // Six steps from it (the middle of them all: the Mantis Lords' thrones) on the floor,
                        // then a walk toward it, through the arena's trigger as a player would come in.
                        Vector2 bp = Vector2.zero;
                        int near = 0;
                        foreach (var hm in all)
                            if (Vector2.Distance(hm.transform.position, all[0].transform.position) < 15f)
                            {
                                bp += (Vector2)hm.transform.position;
                                near++;
                            }
                        bp /= near;
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
                    // Up to twelve seconds to wake, then struck down until none is left standing.
                    t0 = Time.time;
                    bool woke = false;
                    while (found0 && Time.time - t0 < 15f)
                    {
                        PlayerData.instance.isInvincible = true;
                        foreach (var hm in Bosses(false)) if (!hm.isDead) woke = true;
                        if (woke) break;
                        yield return new WaitForSeconds(0.25f);
                    }
                    int strikes = 0, deaths = 0;
                    var dead = new HashSet<HealthManager>();
                    float quiet = 0f;
                    t0 = Time.time;
                    while (woke && Time.time - t0 < 90f)
                    {
                        PlayerData.instance.isInvincible = true;
                        bool standing = false;
                        foreach (var hm in Bosses(false))
                        {
                            if (hm.isDead)
                            {
                                if (dead.Add(hm)) deaths++;
                                continue;
                            }
                            standing = true;
                            // Not during its entrance (the roar, the reveal): the player has control back once it is over.
                            if (hm.IsInvincible || !GameHooks.PlayerHasControl()) continue;
                            hm.hp = 1;
                            hm.Hit(new HitInstance { Source = h.gameObject, AttackType = AttackTypes.Generic, DamageDealt = 100, Direction = 0f, Multiplier = 1f, MagnitudeMultiplier = 0f });
                            strikes++;
                        }
                        quiet = standing ? 0f : quiet + 0.4f;
                        if (quiet >= 4f) break;
                        yield return new WaitForSeconds(0.4f);
                    }
                    // Control back after the death (its scene's own ending to it).
                    t0 = Time.time;
                    while (!GameHooks.PlayerHasControl() && Time.time - t0 < 30f) yield return null;
                    yield return new WaitForSeconds(1f);
                    bool control = GameHooks.PlayerHasControl();
                    int kills1 = 0;
                    foreach (var v in Items.Bestiary.Kills.Values) kills1 += v;
                    // Killed: none left standing after it was struck, and the book counted it.
                    bool killed = woke && strikes > 0 && quiet >= 4f && kills1 > kills0;
                    bool ok = arrived && found0 && killed && control && errors.Count == 0;
                    var fadeFsm = GameCameras.instance != null ? GameCameras.instance.cameraFadeFSM : null;
                    string stuck = control ? "" : $"，卡住时：{HeroState(HeroController.instance)}，淡入 {(fadeFsm != null ? fadeFsm.ActiveStateName : "无")}，就位监听出错 {World.SafeHeroInPosition.DevFailures}";
                    if (ok) good++;
                    else bad.Append($" {record}");
                    string errs = errors.Count > 0 ? $"，报错 {errors.Count} 条：{string.Join(" | ", errors.GetRange(0, Mathf.Min(3, errors.Count)))}" : "";
                    Plugin.Log.LogInfo($"[AutoTest] 首领 {record}（{scene}）：{(ok ? "好" : "坏")}，到达 {arrived}，找到 {found0}（{all.Count} 个），醒了 {woke}，打了 {strikes} 下，死了 {deaths} 个，图鉴击杀 +{kills1 - kills0}，打完 {killed}，之后可操作 {control}，场景 {gm.sceneName}，主角 {(Vector2)HeroController.instance.transform.position:F1}{stuck}{errs}");
                    if (!ok)
                    {
                        Shot(gm.sceneName, $"boss_{record}");
                        yield return null;
                    }
                }
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
                Player.TerraMovement.DevMove = 0;
            }
            Plugin.Log.LogInfo($"[AutoTest] 首领 第{part}段：{good}/{list.Length} 个正常{(bad.Length > 0 ? "，有问题的：" + bad : "")}");
        }
    }
}
