using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "ggreturn"): a book challenge in a Godhome scene (Oblobble's GG_Oblobbles, Paintmaster's GG_Painter) won: the
    /// scene's own end (BossSceneController: the white way out, then OnBossSceneComplete) wakes the player back in the room left
    /// from, with control and no white screen left over. TK_GG_KEYS picks the bosses (comma-separated book keys).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator GodhomeReturnTest()
        {
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            string env = System.Environment.GetEnvironmentVariable("TK_GG_KEYS");
            var keys = string.IsNullOrEmpty(env) ? new[] { "Oblobble", "Paintmaster" } : env.Split(',');
            int ok = 0, all = 0;
            bool dumped = false;
            foreach (var key in keys)
            {
                yield return GoTo("Town", "left1");
                var gm = GameManager.instance;
                string home = gm.sceneName;
                yield return new WaitForSeconds(1f);
                Bestiary.Kills[key] = Mathf.Max(1, Bestiary.KillsOf(key));
                int started = BossSummonStart.DevStarted, wakes = BossArena.DevGodhomeWakes;
                bool going = BossArena.Fight(key, true);
                float until = Time.realtimeSinceStartup + 30f;
                while (!BossArena.Arrived(key) && Time.realtimeSinceStartup < until) yield return null;
                until = Time.realtimeSinceStartup + 15f;
                while (BossSummonStart.DevStarted == started && Time.realtimeSinceStartup < until) yield return null;
                yield return new WaitForSeconds(2f);
                var bsc = BossSceneController.Instance;
                string arena = gm.sceneName;
                if (bsc != null && !dumped) { dumped = true; DumpFsms(bsc.transform); }
                // Every boss of the scene killed as a blow would (HealthManager.Die through the mod's own deaths).
                var bosses = bsc != null ? (Traverse.Create(bsc).Field("bosses").GetValue<HealthManager[]>() ?? new HealthManager[0]) : new HealthManager[0];
                foreach (var hm in bosses)
                {
                    if (hm == null || hm.isDead) continue;
                    hm.hp = 1;
                    mod.Melee.HitWith(hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>(), 9999, 0f, 0f, 1f, hm.transform.position);
                    yield return new WaitForSeconds(.5f);
                    if (!hm.isDead) Traverse.Create(hm).Method("Die", new[] { typeof(float?), typeof(AttackTypes), typeof(bool) }).GetValue(null, AttackTypes.Generic, true);
                    yield return new WaitForSeconds(1.5f);
                }
                var timeline = new List<string>();
                string last = null;
                float t0 = Time.realtimeSinceStartup;
                bool back = false;
                while (Time.realtimeSinceStartup - t0 < 40f)
                {
                    var fade = GameCameras.instance != null ? GameCameras.instance.cameraFadeFSM : null;
                    float white = WhiteOverlay();
                    string s = $"{gm.sceneName} 过场中={gm.IsInSceneTransition} 首领场景出场={BossSceneController.IsTransitioning} 镜头={fade?.ActiveStateName} 白={white:F2} 能动={GameHooks.PlayerHasControl()}";
                    if (s != last) { timeline.Add($"{Time.realtimeSinceStartup - t0:F1}s {s}"); last = s; }
                    if (gm.sceneName == home && !gm.IsInSceneTransition && GameHooks.PlayerHasControl() && fade != null && fade.ActiveStateName == "Normal" && white < .05f)
                    {
                        yield return new WaitForSeconds(1.5f);
                        back = gm.sceneName == home && GameHooks.PlayerHasControl() && WhiteOverlay() < .05f;
                        break;
                    }
                    yield return null;
                }
                all++;
                if (going && back) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 神居挑战返回 {key}：{arena}，首领 {bosses.Length} 个，场景完成回调 {BossArena.DevGodhomeWakes - wakes} 次：{string.Join(" | ", timeline)}");
                Plugin.Log.LogInfo($"[AutoTest] 神居挑战返回 {key} {(going && back ? "回到 " + home + "，能动、没有白屏 通过" : "没回来或白屏 不通过")}");
                Shot(gm.sceneName, "ggreturn_" + key);
            }
            // Chained: a second challenge asked for from inside the first's Godhome scene, before its end wakes him (its own
            // entry there would hold the Knight): the second's win takes him back to where the first was asked for from.
            if (keys.Length >= 2)
            {
                yield return GoTo("Town", "left1");
                var gm = GameManager.instance;
                string home = gm.sceneName;
                yield return new WaitForSeconds(1f);
                string first = keys[0], second = keys[1];
                int started = BossSummonStart.DevStarted;
                BossArena.Fight(first, true);
                float until = Time.realtimeSinceStartup + 30f;
                while (!BossArena.Arrived(first) && Time.realtimeSinceStartup < until) yield return null;
                until = Time.realtimeSinceStartup + 15f;
                while (BossSummonStart.DevStarted == started && Time.realtimeSinceStartup < until) yield return null;
                yield return new WaitForSeconds(2f);
                string arena = gm.sceneName;
                foreach (var hm in Traverse.Create(BossSceneController.Instance).Field("bosses").GetValue<HealthManager[]>() ?? new HealthManager[0])
                    if (hm != null && !hm.isDead) Traverse.Create(hm).Method("Die", new[] { typeof(float?), typeof(AttackTypes), typeof(bool) }).GetValue(null, AttackTypes.Generic, true);
                yield return new WaitForSeconds(1.5f);
                started = BossSummonStart.DevStarted;
                bool going = gm.sceneName == arena && BossArena.Fight(second, true);
                until = Time.realtimeSinceStartup + 30f;
                while (!BossArena.Arrived(second) && Time.realtimeSinceStartup < until) yield return null;
                until = Time.realtimeSinceStartup + 15f;
                while (BossSummonStart.DevStarted == started && Time.realtimeSinceStartup < until) yield return null;
                yield return new WaitForSeconds(2f);
                foreach (var hm in Traverse.Create(BossSceneController.Instance).Field("bosses").GetValue<HealthManager[]>() ?? new HealthManager[0])
                    if (hm != null && !hm.isDead) Traverse.Create(hm).Method("Die", new[] { typeof(float?), typeof(AttackTypes), typeof(bool) }).GetValue(null, AttackTypes.Generic, true);
                bool back = false;
                float t0 = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - t0 < 40f)
                {
                    var fade = GameCameras.instance != null ? GameCameras.instance.cameraFadeFSM : null;
                    if (gm.sceneName == home && !gm.IsInSceneTransition && GameHooks.PlayerHasControl() && fade != null && fade.ActiveStateName == "Normal" && WhiteOverlay() < .05f) { back = true; break; }
                    yield return null;
                }
                all++;
                if (going && back) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 神居挑战返回 连着打 {first} → {second}（在 {arena} 里开第二场）：{(going && back ? "回到 " + home + "，能动、没有白屏 通过" : $"现在 {gm.sceneName}，能动 {GameHooks.PlayerHasControl()} 不通过")}");
                Shot(gm.sceneName, "ggreturn_chain");
            }
            Plugin.Log.LogInfo($"[AutoTest] 神居挑战返回 {ok}/{all} {(ok == all ? "全部通过" : "有不通过")}");
        }

        /// <summary>How opaque the dream's white overlay (Blanker White) is drawn, 0 when there is none.</summary>
        static float WhiteOverlay()
        {
            float most = 0f;
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
            {
                if (f == null || !(f.gameObject.name == "Blanker White" || f.gameObject.name == "Blanker White(Clone)") || !f.gameObject.activeInHierarchy) continue;
                foreach (var r in f.GetComponentsInChildren<SpriteRenderer>()) if (r.enabled) most = Mathf.Max(most, r.color.a);
                foreach (var r in f.GetComponentsInChildren<tk2dBaseSprite>()) most = Mathf.Max(most, r.color.a);
            }
            return most;
        }
    }
}
