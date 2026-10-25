using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "ggvisible"): the user's 10-25 way (a book challenge of the Hive Knight won in Hive_05, then from there the
    /// God Tamer's in Godhome, won): back in Hive_05 by its ordinary gate (it has no dream-return door), the Knight is shown
    /// (his renderer on: the Terraria character is drawn by it), in control, the mod's input open (not a "hidden Knight"
    /// cutscene), the screen clear. A timeline of those from the win on. TK_GGV_FIRST / TK_GGV_SECOND pick the two keys.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator GodhomeVisibleTest()
        {
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            // The Hive Queen speaks only to a dream nail's bearer (her Conversation Control is inert without one).
            pd.SetBool("hasDreamNail", true);
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            string first = System.Environment.GetEnvironmentVariable("TK_GGV_FIRST") ?? "HiveKnight";
            string second = System.Environment.GetEnvironmentVariable("TK_GGV_SECOND") ?? "LobsterLancer";
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail = "")
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 神居返回显形 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            var gm = GameManager.instance;
            static bool Shown() => HeroController.SilentInstance is HeroController h && h.GetComponent<MeshRenderer>() is MeshRenderer mr && mr.enabled;
            static string DreamReturnState()
            {
                var h = HeroController.SilentInstance;
                if (h == null) return "-";
                foreach (var f in h.GetComponents<PlayMakerFSM>()) if (f.FsmName == "Dream Return") return f.ActiveStateName;
                return "-";
            }
            IEnumerator KillAll(string key)
            {
                // A Godhome scene's bosses (BossSceneController), else every boss of the record in the scene.
                var list = new List<HealthManager>();
                var bsc = BossSceneController.Instance;
                if (bsc != null) list.AddRange(Traverse.Create(bsc).Field("bosses").GetValue<HealthManager[]>() ?? new HealthManager[0]);
                foreach (var h in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                    if (h != null && !h.isDead && h.gameObject.activeInHierarchy && EnemyStats.Of(h) is EnemyStats.Info inf && inf.Boss && !list.Contains(h)) list.Add(h);
                foreach (var hm in list)
                {
                    if (hm == null || hm.isDead) continue;
                    hm.hp = 1;
                    var c = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
                    if (c != null) mod.Melee.HitWith(c, 9999, 0f, 0f, 1f, hm.transform.position);
                    yield return new WaitForSeconds(0.5f);
                    if (hm != null && !hm.isDead) Traverse.Create(hm).Method("Die", new[] { typeof(float?), typeof(AttackTypes), typeof(bool) }).GetValue(null, AttackTypes.Generic, true);
                    yield return new WaitForSeconds(1.5f);
                }
                Plugin.Log.LogInfo($"[AutoTest] 神居返回显形 {key}：打倒 {list.Count} 个首领身体");
            }
            IEnumerator Arrive(string key)
            {
                int started = BossSummonStart.DevStarted;
                float until = Time.realtimeSinceStartup + 30f;
                while (!BossArena.Arrived(key) && Time.realtimeSinceStartup < until) yield return null;
                until = Time.realtimeSinceStartup + 15f;
                while (BossSummonStart.DevStarted == started && Time.realtimeSinceStartup < until) yield return null;
                yield return new WaitForSeconds(2.5f);
            }

            // The first: a world arena's challenge, won there.
            Bestiary.Kills[first] = Mathf.Max(1, Bestiary.KillsOf(first));
            bool going = BossArena.Fight(first, true);
            yield return Arrive(first);
            string room = gm.sceneName;
            Check($"{first} 的挑战到场", going && BossArena.Arrived(first), $"在 {room}");
            yield return KillAll(first);
            yield return new WaitForSeconds(8f);
            var hero = HeroController.instance;
            Vector2 stand = hero.transform.position;
            Check($"{first} 赢了以后在 {room} 显形能动", Shown() && GameHooks.PlayerHasControl(), $"显形 {Shown()}，能动 {GameHooks.PlayerHasControl()}");
            // The second: a Godhome scene's, asked for from the first's room.
            Bestiary.Kills[second] = Mathf.Max(1, Bestiary.KillsOf(second));
            int wakes = BossArena.DevGodhomeWakes;
            going = BossArena.Fight(second, true);
            yield return Arrive(second);
            string arena = gm.sceneName;
            Check($"{second} 的挑战到场", going && BossArena.Arrived(second), $"在 {arena}");
            yield return KillAll(second);
            // The user's state at its death (TK_GGV_HIDE=0: left as it is): the Knight's renderer off, his character hidden.
            int shown0 = BossArena.DevRenderersShown;
            bool hid = System.Environment.GetEnvironmentVariable("TK_GGV_HIDE") != "0";
            if (hid && HeroController.instance.GetComponent<MeshRenderer>() is MeshRenderer off) off.enabled = false;
            var timeline = new List<string>();
            string last = null;
            float t0 = Time.realtimeSinceStartup;
            bool back = false;
            while (Time.realtimeSinceStartup - t0 < 45f)
            {
                var fade = GameCameras.instance != null ? GameCameras.instance.cameraFadeFSM : null;
                string s = $"{gm.sceneName} 过场={gm.IsInSceneTransition} 显形={Shown()} 能动={GameHooks.PlayerHasControl()} 藏起={Player.CutsceneAttack.KnightHidden()} 梦醒={DreamReturnState()} 镜头={fade?.ActiveStateName} 白={WhiteOverlay():F2}";
                if (s != last) { timeline.Add($"{Time.realtimeSinceStartup - t0:F1}s {s}"); last = s; }
                if (gm.sceneName == room && !gm.IsInSceneTransition && GameHooks.PlayerHasControl() && fade != null && fade.ActiveStateName == "Normal" && WhiteOverlay() < .05f)
                {
                    back = true;
                    if (Time.realtimeSinceStartup - t0 > 1f) break;
                }
                yield return null;
            }
            // A few seconds on (the Hive Queen's words, the room's own scripts).
            float settle = Time.realtimeSinceStartup + 6f;
            while (Time.realtimeSinceStartup < settle)
            {
                string s = $"{gm.sceneName} 显形={Shown()} 能动={GameHooks.PlayerHasControl()} 藏起={Player.CutsceneAttack.KnightHidden()} 梦醒={DreamReturnState()}";
                if (s != last) { timeline.Add($"{Time.realtimeSinceStartup - t0:F1}s {s}"); last = s; }
                yield return null;
            }
            Plugin.Log.LogInfo($"[AutoTest] 神居返回显形 时间线：{string.Join(" | ", timeline)}");
            Shot(gm.sceneName, "ggvisible_back");
            hero = HeroController.instance;
            Check($"{second} 赢了回到 {room}", back && gm.sceneName == room, $"现在 {gm.sceneName}，场景完成回调 {BossArena.DevGodhomeWakes - wakes} 次，离出发处 {Vector2.Distance(hero.transform.position, stand):F1}");
            Check("骑士显形（泰拉角色画得出来）", Shown(), $"渲染器 {Shown()}{(hid ? $"，赢时关掉渲染器，梦醒收尾打开 {BossArena.DevRenderersShown - shown0} 次" : "")}");
            Check("能动、能用物品（不是藏起骑士的过场）", GameHooks.PlayerHasControl() && !Player.CutsceneAttack.KnightHidden(), $"能动 {GameHooks.PlayerHasControl()}，藏起 {Player.CutsceneAttack.KnightHidden()}");
            // Then as the user: down to the Hive Queen and her words heard (her npc_control's Listen).
            PlayMakerFSM npc = null;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (f != null && f.FsmName == "npc_control" && f.gameObject.name == "Vespa NPC" && f.gameObject.scene.name == room) npc = f;
            if (npc != null)
            {
                if (!npc.gameObject.activeSelf) npc.gameObject.SetActive(true);
                var at = (Vector2)npc.transform.position + new Vector2(2f, 0f);
                hero.transform.position = new Vector3(at.x, at.y, hero.transform.position.z);
                float until = Time.realtimeSinceStartup + 6f;
                while (Time.realtimeSinceStartup < until && npc.ActiveStateName != "In Range" && npc.ActiveStateName != "In Range Turns") yield return null;
                Plugin.Log.LogInfo($"[AutoTest] 神居返回显形 女王：{npc.ActiveStateName}，她在 {(Vector2)npc.transform.position:F1}，玩家 {(Vector2)hero.transform.position:F1}");
                if (npc.ActiveStateName == "Idle") npc.SendEvent("IN RANGE");
                yield return null;
                yield return null;
                npc.SendEvent("UP PRESSED");
                var talk = new List<string>();
                string was = null;
                float tt = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - tt < 30f)
                {
                    var convo = PlayMakerFSM.FindFsmOnGameObject(npc.gameObject, "Conversation Control");
                    string s = $"npc={npc.ActiveStateName} 对话={convo?.ActiveStateName} 显形={Shown()} 能动={GameHooks.PlayerHasControl()} 藏起={Player.CutsceneAttack.KnightHidden()} 动画={hero.GetComponent<tk2dSpriteAnimator>()?.CurrentClip?.name}";
                    if (s != was) { talk.Add($"{Time.realtimeSinceStartup - tt:F1}s {s}"); was = s; }
                    if (Time.realtimeSinceStartup - tt > 2f && (npc.ActiveStateName == "Idle" || npc.ActiveStateName == "In Range" || npc.ActiveStateName == "Pause") && GameHooks.PlayerHasControl()) break;
                    // Her pages turned as a player would (the dialogue box's own advance).
                    if (Time.frameCount % 45 == 0)
                        foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                            if (f.FsmName == "Dialogue Page Control") f.SendEvent("NEXT");
                    yield return null;
                }
                yield return new WaitForSeconds(2f);
                Plugin.Log.LogInfo($"[AutoTest] 神居返回显形 女王对话：{string.Join(" | ", talk)}");
                Shot(gm.sceneName, "ggvisible_queen");
                Check("和女王说完话：显形、能动", Shown() && GameHooks.PlayerHasControl() && !Player.CutsceneAttack.KnightHidden(), $"显形 {Shown()}，能动 {GameHooks.PlayerHasControl()}，藏起 {Player.CutsceneAttack.KnightHidden()}");
            }
            else Plugin.Log.LogInfo("[AutoTest] 神居返回显形 女王：没找到 Vespa NPC");
            Plugin.Log.LogInfo($"[AutoTest] 神居返回显形 合计 {ok}/{all} {(ok == all ? "全部通过" : "有不通过")}");
        }
    }
}
