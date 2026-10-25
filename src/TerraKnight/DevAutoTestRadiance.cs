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
    /// Dev (suite "radiance"): the user's Radiance (10-25) through the book's challenge, as they fought her: with the new AI
    /// (TK_RAD_VANILLA=1: the game's), struck down phase by phase, her climb watched from a platform partway up
    /// (TK_RAD_CLIMB seconds, 22 by default), then struck at her side. After: her life never ran out before her end, her
    /// platforms keep their colliders, her beams stop, the win is written down and paid, and the dream wakes the player
    /// back where the challenge was asked for, in control, the screen clear. A shot every 2.5 s named by her state.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator RadianceTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            mod.ApplyCreative(false);
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            bool vanilla = Environment.GetEnvironmentVariable("TK_RAD_VANILLA") == "1";
            World.EnemyAiMode.Current = vanilla ? World.EnemyAiKind.Vanilla : World.EnemyAiKind.New;
            float climbFor = float.TryParse(Environment.GetEnvironmentVariable("TK_RAD_CLIMB"), out var cf) ? cf : 22f;
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail = "")
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 辐光 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            var errors = new List<string>();
            void OnLog(string msg, string stack, LogType type)
            {
                if ((type == LogType.Exception || type == LogType.Error && msg.Contains("Exception")) && errors.Count < 20)
                    errors.Add((msg.Length > 160 ? msg.Substring(0, 160) : msg) + " @ " + (stack ?? "").Split('\n').FirstOrDefault(l => l.Contains("TerraKnight")));
            }
            static string Safe(string s) => string.IsNullOrEmpty(s) ? "none" : new string(s.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray());
            // Her platforms' colliders switched on now.
            static int Plats()
            {
                int n = 0;
                foreach (var c in Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
                {
                    if (c == null || !c.enabled || !c.gameObject.activeInHierarchy || c.isTrigger) continue;
                    for (var t = c.transform; t != null; t = t.parent)
                        if (t.name.StartsWith("Radiant Plat")) { n++; break; }
                }
                return n;
            }
            static float Ground(Vector2 from) =>
                Physics2D.Raycast(from, Vector2.down, 40f, 1 << 8) is RaycastHit2D h && h.collider != null ? h.point.y : from.y - 40f;

            Application.logMessageReceived += OnLog;
            try
            {
                var gm = GameManager.instance;
                var hero = HeroController.instance;
                string start = gm.sceneName;
                int kills0 = Bestiary.KillsOf("FinalBoss");
                int loot0 = BossLootTiming.DevByGone + BossLootTiming.DevByBurst + BossLootTiming.DevByTime + BossLootTiming.DevByLeft;
                int climbMoves0 = 0, moves0 = BossAi.DevMoves, wakes0 = RadianceEnding.DevChallengeWakes;
                Plugin.Log.LogInfo($"[AutoTest] 辐光 从 {start} 进图鉴挑战，{(vanilla ? "原版" : "新版")} AI");
                BossArena.Fight("FinalBoss", false);
                HealthManager rad = null;
                float until = Time.realtimeSinceStartup + 60f;
                while (Time.realtimeSinceStartup < until && rad == null)
                {
                    if (!gm.IsInSceneTransition)
                        rad = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                            .FirstOrDefault(h => h != null && h.gameObject.activeInHierarchy && UI.EnemyHover.RecordOf(h) == "FinalBoss");
                    yield return null;
                }
                if (rad == null)
                {
                    Check("出场", false, "60 秒没等到辐光");
                    yield break;
                }
                hero = HeroController.instance;
                var hb = hero.GetComponent<Rigidbody2D>();
                var control = PlayMakerFSM.FindFsmOnGameObject(rad.gameObject, "Control");
                var commands = PlayMakerFSM.FindFsmOnGameObject(rad.gameObject, "Attack Commands");
                var choices = PlayMakerFSM.FindFsmOnGameObject(rad.gameObject, "Attack Choices");
                var info = EnemyStats.Of(rad);
                int life = Mathf.Max(1, EnemyStats.LifeMax(info));
                string fight = gm.sceneName;
                Check("出场", true, $"{rad.name} 在 {rad.gameObject.scene.name}，血 {rad.hp}，泰拉血量 {life}");
                void Put(Vector2 at)
                {
                    hero.transform.position = new Vector3(at.x, at.y, hero.transform.position.z);
                    if (hb != null) hb.linearVelocity = Vector2.zero;
                }
                Collider2D Body()
                {
                    var c = rad.GetComponent<Collider2D>();
                    if (c == null || !c.enabled) c = rad.GetComponentsInChildren<Collider2D>().FirstOrDefault(x => x.enabled && !x.isTrigger);
                    return c ?? rad.GetComponent<Collider2D>();
                }
                void Strike(int damage)
                {
                    var c = Body();
                    if (c != null) mod.Melee.HitWith(c, damage, 0f, 0f, rad.transform.position.x > hero.transform.position.x ? 1f : -1f, hero.transform.position, 9999);
                }

                float t0 = Time.time, nextStrike = Time.time + 5f, nextShot = 0f, nextLog = 0f, nextPlace = 0f, climbAt = -1f;
                bool deadEarly = false;
                var trail = new List<string>();
                int platsClimb = -1, floorHp = -1;
                bool floorTried = false, floorDead = false;
                while (Time.time - t0 < 260f && Bestiary.KillsOf("FinalBoss") <= kills0 && rad != null)
                {
                    string st = control != null ? control.ActiveStateName : "?";
                    if (trail.Count == 0 || trail[trail.Count - 1] != st) trail.Add(st);
                    if (rad.isDead) deadEarly = true;
                    if (mod.Stats.Life < mod.Stats.LifeMax / 2) mod.Stats.FullHeal();
                    if (Time.time >= nextShot)
                    {
                        nextShot = Time.time + 2.5f;
                        Shot(gm.sceneName, $"radiance_{Time.time - t0:000}_{Safe(st)}");
                    }
                    if (Time.time >= nextLog)
                    {
                        nextLog = Time.time + 3f;
                        var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
                        string view = "-";
                        if (cam != null)
                        {
                            float halfH = -cam.transform.position.z * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad), halfW = halfH * cam.aspect;
                            view = $"{cam.transform.position.x - halfW:F1}..{cam.transform.position.x + halfW:F1} × {cam.transform.position.y - halfH:F1}..{cam.transform.position.y + halfH:F1}（{GameCameras.instance.cameraController?.mode}）";
                        }
                        Plugin.Log.LogInfo($"[AutoTest] 辐光 {Time.time - t0:F0}s 主控 {st}，命令 {commands?.ActiveStateName}，选招 {choices?.ActiveStateName}，血 {rad.hp}{(rad.isDead ? "（已死）" : "")}，" +
                            $"她 {(Vector2)rad.transform.position:F1}，玩家 {(Vector2)hero.transform.position:F1}，平台 {Plats()}，招式 {BossAi.DevMoves - moves0}，镜头 {view}");
                    }
                    if (st == "Ascend Cast")
                    {
                        if (climbAt < 0f)
                        {
                            climbAt = Time.time;
                            climbMoves0 = BossAi.DevMoves;
                            platsClimb = Plats();
                            Plugin.Log.LogInfo($"[AutoTest] 辐光 进入爬升：平台 {platsClimb}，她在 {(Vector2)rad.transform.position:F1}");
                        }
                        Vector2 her = rad.transform.position;
                        if (Time.time - climbAt < climbFor)
                        {
                            // Partway up: on a platform 9-15 under her, held there.
                            if (Time.time >= nextPlace)
                            {
                                nextPlace = Time.time + 1f;
                                Vector2 want = her + new Vector2(-3f, -11f);
                                Put(new Vector2(want.x, Ground(want + Vector2.up * 2f) + 1.2f));
                            }
                            // Her life near its end, struck from down here (a ranged player's): it keeps its last point.
                            if (!floorTried && Time.time - climbAt > 4f)
                            {
                                floorTried = true;
                                rad.hp = 3;
                                Strike(Mathf.Max(1, life / 10));
                                floorHp = rad.hp;
                                floorDead = rad.isDead;
                            }
                        }
                        else
                        {
                            // At her side: struck.
                            Put(her + new Vector2(-2.2f, -1.2f));
                            if (Time.time >= nextStrike)
                            {
                                nextStrike = Time.time + 0.5f;
                                Strike(Mathf.Max(1, life / 30));
                            }
                        }
                    }
                    else if (Time.time >= nextStrike && Time.time - t0 > 5f)
                    {
                        nextStrike = Time.time + 1.2f;
                        Vector2 her = rad.transform.position, me = hero.transform.position;
                        if (Vector2.Distance(me, her) > 14f || me.y < her.y - 12f)
                        {
                            float x = her.x + (her.x > 62f ? -4f : 4f);
                            Put(new Vector2(x, Ground(new Vector2(x, her.y + 1f)) + 1.2f));
                        }
                        Strike(Mathf.Max(1, life / 30));
                    }
                    yield return null;
                }
                bool won = Bestiary.KillsOf("FinalBoss") > kills0;
                if (control != null && (trail.Count == 0 || trail[trail.Count - 1] != control.ActiveStateName)) trail.Add(control.ActiveStateName);
                Plugin.Log.LogInfo($"[AutoTest] 辐光 主控走过：{string.Join(" → ", trail)}");
                // The challenge's taking down of the walls its fight raised (5 s after a win; this one wakes before), at once.
                int platsWin = Plats(), wallsOff0 = BossArena.DevWallsOff;
                typeof(BossArena).GetMethod("TakeDownFightWalls", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)?.Invoke(null, null);
                int platsTaken = Plats(), wallsTaken = BossArena.DevWallsOff - wallsOff0;
                // Final Impact goes on to the Knight's pull in the frame it comes in.
                bool impact = trail.Contains("Final Impact") || trail.Contains("Knight Pull") || RadianceEnding.DevChallengeWakes > wakes0;
                if (rad != null && rad.isDead && !impact) deadEarly = true;
                float wonAt = Time.time;
                int platsLast = Plats();
                bool beamsAfter = false, left = false, inControl = false, clear = false;
                string fade = "?", cmdLast = commands != null ? commands.ActiveStateName : "-";
                float white = 0f, nextAfterShot = 0f;
                float end = Time.realtimeSinceStartup + 45f;
                while (won && Time.realtimeSinceStartup < end)
                {
                    bool inDream = gm.sceneName == fight;
                    if (inDream && rad != null)
                    {
                        platsLast = Mathf.Min(platsLast, Plats());
                        if (commands != null) cmdLast = commands.ActiveStateName;
                        if (Time.time - wonAt > 2.5f && commands != null && commands.enabled && (cmdLast == "Aim" || cmdLast == "AB Start")) beamsAfter = true;
                    }
                    if (Time.time >= nextAfterShot && Time.time - wonAt < 8f)
                    {
                        nextAfterShot = Time.time + 1f;
                        Shot(gm.sceneName, $"radiance_after_{Time.time - wonAt:0}");
                    }
                    var f = GameCameras.instance != null ? GameCameras.instance.cameraFadeFSM : null;
                    fade = f != null ? f.ActiveStateName : "?";
                    white = WhiteOverlay();
                    left = !inDream && !gm.IsInSceneTransition;
                    inControl = GameHooks.PlayerHasControl() && !gm.IsInSceneTransition;
                    clear = fade == "Normal" && white < 0.05f;
                    if (left && inControl && clear && Time.time - wonAt > 3f) break;
                    yield return null;
                }
                Shot(gm.sceneName, "radiance_end");
                int loot = BossLootTiming.DevByGone + BossLootTiming.DevByBurst + BossLootTiming.DevByTime + BossLootTiming.DevByLeft - loot0;
                Check("血量在她的结局之前没见底", !deadEarly, deadEarly ? "她的血打空了（原版靠爬到顶上一击收尾）" : "");
                Check("爬升中远处打到只剩 3 点血：留住最后一点", floorTried && !floorDead && floorHp == 1, $"打完血 {floorHp}{(floorDead ? "（死了）" : "")}，留住 {RadianceEnding.DevHeld} 次");
                Check("爬到她身边一击就收尾", impact, $"爬升 {(climbAt >= 0f ? "到了" : "没到")}，身边一击 {RadianceEnding.DevTouches} 次，留住最后一点血 {RadianceEnding.DevHeld} 次，挑战梦醒 {RadianceEnding.DevChallengeWakes} 次");
                if (!vanilla) Check("爬升时有光束以外的招：光球、剑雨、剑扇都出过", climbAt >= 0f && RadianceKit.DevClimbOrbs > 0 && RadianceKit.DevClimbRain > 0 && RadianceKit.DevClimbFans > 0,
                    $"爬升里新招 {RadianceKit.DevClimbMoves} 次（光球 {RadianceKit.DevClimbOrbs}、剑雨 {RadianceKit.DevClimbRain}、剑扇 {RadianceKit.DevClimbFans}），招式计数 {BossAi.DevMoves - climbMoves0}");
                Check("赢下记入图鉴", won, $"图鉴 {kills0}→{Bestiary.KillsOf("FinalBoss")}");
                Check("撤战斗墙时平台不动", won && platsTaken == platsWin && wallsTaken == 0, $"赢时平台 {platsWin}，撤墙后 {platsTaken}，撤掉 {wallsTaken} 道");
                Check("平台的碰撞体都还在", platsLast >= platsWin, $"爬升开始 {platsClimb}，赢时 {platsWin}，之后最少 {platsLast}");
                Check("赢了以后光束停下", !beamsAfter, $"攻击命令最后在 {cmdLast}");
                Check("梦醒回到出发的房间", left && gm.sceneName == start, $"现在 {gm.sceneName}（出发 {start}）");
                Check("能动、画面干净", inControl && clear, $"能动 {inControl}，镜头 {fade}，白 {white:F2}");
                Check("发放掉落", loot > 0, $"{BossLootTiming.DevLastWhy}");
                var mine = errors.Where(e => e.Contains("TerraKnight")).ToList();
                Check("mod 没有异常", mine.Count == 0, string.Join(" | ", mine.Take(3)));
            }
            finally
            {
                Application.logMessageReceived -= OnLog;
            }
            Plugin.Log.LogInfo($"[AutoTest] 辐光 合计 {ok}/{all} {(ok == all ? "全部通过" : "有不通过")}");
        }
    }
}
