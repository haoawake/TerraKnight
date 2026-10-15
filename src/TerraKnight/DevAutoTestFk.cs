using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using TerraKnight.Items;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "fkdeath"): the False Knight fought to the end the way a player does, hitting
    /// whatever of him is there to hit; every state change of his FSMs, his health managers' life
    /// and death, and whether the kill reached the monster book and dropped anything.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator FalseKnightDeathTest()
        {
            var gm = GameManager.instance;
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            mod.DevNoFallDamage = true;
            const string arena = "Crossroads_10";
            gm.BeginSceneTransition(new GameManager.SceneLoadInfo
            {
                SceneName = arena, EntryGateName = "left1", HeroLeaveDirection = GlobalEnums.GatePosition.right,
                EntryDelay = 0f, WaitForSceneTransitionCameraFade = true, Visualization = GameManager.SceneLoadVisualizations.Default,
            });
            float wait = Time.time + 30f;
            while ((gm.sceneName != arena || !GameHooks.PlayerHasControl()) && Time.time < wait) yield return null;
            wait = Time.time + 8f;
            while (!UnityEngine.SceneManagement.SceneManager.GetSceneByName(arena + "_boss").isLoaded && Time.time < wait) yield return null;
            yield return new WaitForSeconds(1f);
            var hero = HeroController.instance;

            // Into the arena's middle: the battle starts.
            hero.transform.position = Clear(hero, new Vector2(38f, 28f));
            HealthManager fk = null;
            wait = Time.time + 25f;
            while (Time.time < wait)
            {
                foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                    if (hm.name == "False Knight New" && hm.gameObject.activeInHierarchy) fk = hm;
                if (fk != null) break;
                yield return null;
            }
            if (fk == null)
            {
                Plugin.Log.LogInfo("[AutoTest] 假骑士死亡 等不到假骑士出场，不通过");
                yield break;
            }
            var all = new List<HealthManager>(fk.GetComponentsInChildren<HealthManager>(true));
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (hm.gameObject.scene == fk.gameObject.scene && !all.Contains(hm)) all.Add(hm);
            var hms = new StringBuilder();
            foreach (var hm in all) hms.Append($" {PathOf(hm.transform)}(hp {hm.hp}, 激活 {hm.gameObject.activeInHierarchy})");
            Plugin.Log.LogInfo($"[AutoTest] 假骑士死亡 出场：{fk.name} hp {fk.hp}；场景里的血量管理器：{hms}");
            var fsms = new List<PlayMakerFSM>(fk.GetComponentsInChildren<PlayMakerFSM>(true));
            foreach (var f in fsms) DumpFsm(f);

            int kills0 = Bestiary.Kills.TryGetValue("FalseKnight", out int k0) ? k0 : 0, dropped0 = EnemyDrops.DevDropped, others0 = EnemyDeathLog.DevOthers;
            var seen = new Dictionary<PlayMakerFSM, string>();
            var trail = new List<string>();
            float t0 = Time.time, nextHit = 0f, nextNote = 0f;
            bool defeated = false;
            while (Time.time - t0 < 150f && !defeated)
            {
                foreach (var f in fsms)
                {
                    if (f == null) continue;
                    string s = f.ActiveStateName;
                    if (!seen.TryGetValue(f, out var was) || was != s)
                    {
                        seen[f] = s;
                        if (trail.Count < 400) trail.Add($"{Time.time - t0:F1}s {f.name}/{f.FsmName}:{s}");
                    }
                }
                if (Time.time >= nextNote)
                {
                    nextNote = Time.time + 5f;
                    var sb = new StringBuilder();
                    foreach (var hm in all) if (hm != null) sb.Append($" {hm.name}={hm.hp}{(hm.isDead ? "死" : "")}{(hm.IsInvincible ? "无敌" : "")}{(hm.gameObject.activeInHierarchy ? "" : "藏")}");
                    Plugin.Log.LogInfo($"[AutoTest] 假骑士死亡 {Time.time - t0:F0}s：{sb}，falseKnightDefeated={pd.falseKnightDefeated}，可操作={GameHooks.PlayerHasControl()}");
                }
                defeated = pd.falseKnightDefeated && Time.time - t0 > 3f;
                mod.Stats.FullHeal();
                // A blow each half second at whatever of him can be hit, only while the player could swing; not
                // through his rage (a stun in it never reaches the rage's end, where his third stun breaks the floor).
                var control = fsms.Find(f => f != null && f.FsmName == "FalseyControl");
                string cs = control != null ? control.ActiveStateName : "";
                bool raging = cs.StartsWith("Rage") || cs == "Particle Pause" || cs == "Anim End" || cs == "R Attack Antic";
                if (Time.frameCount % 90 == 0 && fk != null && trail.Count < 400) trail.Add($"{Time.time - t0:F1}s 假骑士在 {(Vector2)fk.transform.position:F1}");
                if (Time.time >= nextHit && GameHooks.PlayerHasControl() && !raging)
                {
                    nextHit = Time.time + 0.5f;
                    foreach (var hm in all)
                    {
                        if (hm == null || hm.isDead || !hm.gameObject.activeInHierarchy) continue;
                        var col = hm.GetComponent<Collider2D>();
                        if (col == null || !col.enabled) continue;
                        hero.transform.position = Clear(hero, (Vector2)col.bounds.center + new Vector2(-2.5f, 0.5f));
                        // The head, out while he is stunned, takes a blow strong enough to end the stun.
                        mod.Melee.HitWith(col, hm.name == "Head" ? 5000 : 300, 0f, 0f, 1f, hero.GetComponent<BoxCollider2D>().bounds.center);
                    }
                }
                yield return null;
            }
            // Right after the kill, and once it has all settled: every head there is to see.
            Plugin.Log.LogInfo($"[AutoTest] 假骑士死亡 刚打完的头：{HeadsShown()}");
            Shot(arena, "fkdeath_moment");
            yield return new WaitForSeconds(6f);
            Plugin.Log.LogInfo($"[AutoTest] 假骑士死亡 6 秒后的头：{HeadsShown()}");
            int kills = Bestiary.Kills.TryGetValue("FalseKnight", out int k1) ? k1 : 0;
            var after = new StringBuilder();
            foreach (var hm in all) if (hm != null) after.Append($" {PathOf(hm.transform)}=hp {hm.hp}{(hm.isDead ? " 死" : "")}{(hm.gameObject.activeInHierarchy ? "" : " 藏")}");
            Plugin.Log.LogInfo($"[AutoTest] 假骑士死亡 状态变化：{string.Join(" | ", trail)}");
            bool barShown = fk != null && !Items.EnemyStats.Defeated(fk) && !fk.isDead && fk.hp > 0;
            Plugin.Log.LogInfo($"[AutoTest] 假骑士死亡 尸体：{(fk == null ? "没了" : $"hp {fk.hp}，算作已击败={Items.EnemyStats.Defeated(fk)}，还会显示血条={barShown}")}；特殊死亡记账 {SpecialDeaths.DevCredited} 次（{SpecialDeaths.DevLast}）");
            Plugin.Log.LogInfo($"[AutoTest] 假骑士死亡 打完：falseKnightDefeated={pd.falseKnightDefeated}；图鉴击杀 {kills0} → {kills}；掉落 {EnemyDrops.DevDropped - dropped0} 样；不是玩家打死的 {EnemyDeathLog.DevOthers - others0} 只；血量管理器{after}"
                + $"{(pd.falseKnightDefeated && kills > kills0 && EnemyDrops.DevDropped > dropped0 ? " 通过" : " 不通过")}");
            Player.TerraCamera.DevLook = fk != null ? (Vector2?)fk.transform.position : null;
            yield return new WaitForSeconds(0.4f);
            Shot(arena, "fkdeath_after");
            yield return null;
            Player.TerraCamera.DevLook = null;
        }
        /// <summary>Every object named for a head that is drawn now (any scene), with where it is and what it belongs to.</summary>
        static string HeadsShown()
        {
            var sb = new StringBuilder();
            int n = 0;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || r.forceRenderingOff || !r.isVisible) continue;
                string path = PathOf(r.transform);
                if (path.IndexOf("head", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                n++;
                string sprite = r is SpriteRenderer sr && sr.sprite != null ? sr.sprite.name : r is MeshRenderer ? "网格" : r.GetType().Name;
                sb.Append($"\n    {r.gameObject.scene.name}/{path} [{sprite}] {(Vector2)r.bounds.center:F1}");
            }
            return $"{n} 个{sb}";
        }
    }
}
