using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "xerolow", the user's 10-28 "神居泽诺…打到最后一点血的时候突然声音炸一下，然后泽诺会进入不攻击状态"): Xero in
    /// Godhome (GG_Ghost_Xero) with each enemy AI (TK_XERO_AI: vanilla, new or both), his scripts dumped once, then struck
    /// down bit by bit to his last life; below 40% every state change of his and his swords' scripts is written as it
    /// happens. At the end: whether he died, else how long his attack script stood still. Lines "[AutoTest] 泽若 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator XeroLowTest()
        {
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            pd.hasDreamNail = true;
            string which = System.Environment.GetEnvironmentVariable("TK_XERO_AI") ?? "both";
            var modes = new List<World.EnemyAiKind>();
            if (which != "new") modes.Add(World.EnemyAiKind.Vanilla);
            if (which != "vanilla") modes.Add(World.EnemyAiKind.New);
            var keepMode = World.EnemyAiMode.Current;
            // TK_XERO_HITS: "minion" (a minion's quiet blows after one of the player's), else the player's; TK_XERO_LEVEL the
            // statue's level (0 attuned, 1 ascended, 2 radiant); TK_XERO_SMALL=1 small blows all through (a long fight).
            bool minion = System.Environment.GetEnvironmentVariable("TK_XERO_HITS") == "minion";
            int level = int.TryParse(System.Environment.GetEnvironmentVariable("TK_XERO_LEVEL"), out int lv) ? lv : 0;
            bool small = System.Environment.GetEnvironmentVariable("TK_XERO_SMALL") == "1";
            FsmLoopGuards.DevOff = System.Environment.GetEnvironmentVariable("TK_XERO_NOGUARD") == "1";
            bool dumped = false;
            int ok = 0, all = 0;
            foreach (var mode in modes)
            {
                World.EnemyAiMode.Current = mode;
                string tag = mode == World.EnemyAiKind.New ? "新版" : "原版";
                yield return GoTo("Town", "left1");
                yield return new WaitForSeconds(1f);
                // As a statue's challenge (BossChallengeUI): its end takes the player back.
                BossSceneController.SetupEvent = self =>
                {
                    self.BossLevel = level;
                    self.DreamReturnEvent = "DREAM RETURN";
                    self.OnBossSceneComplete += () => { PlayerData.instance.bossReturnEntryGate = "door_dreamReturn"; self.DoDreamReturn(); };
                };
                pd.dreamReturnScene = "Town";
                yield return GoTo("GG_Ghost_Xero", "door_dreamEnter");
                GameManager.instance.FadeSceneIn();
                HealthManager hm = null;
                for (float until = Time.realtimeSinceStartup + 25f; Time.realtimeSinceStartup < until; )
                {
                    foreach (var h in Resources.FindObjectsOfTypeAll<HealthManager>())
                        if (h != null && h.gameObject.scene.IsValid() && h.name.Contains("Xero") && !h.isDead) hm = h;
                    if (hm != null && hm.gameObject.activeInHierarchy) break;
                    yield return null;
                }
                all++;
                if (hm == null || !hm.gameObject.activeInHierarchy)
                {
                    Plugin.Log.LogInfo($"[AutoTest] 泽若 {tag}：没找到出场的泽若（{GameManager.instance.sceneName}） 不通过");
                    continue;
                }
                var scene = hm.gameObject.scene;
                if (!dumped)
                {
                    dumped = true;
                    foreach (var root in scene.GetRootGameObjects())
                        if (root.name.Contains("Xero") || root.name.Contains("Warrior")) DumpFsms(root.transform);
                }
                yield return new WaitForSeconds(5f);
                if (hm == null || hm.isDead) { Plugin.Log.LogInfo($"[AutoTest] 泽若 {tag}：开打前泽若没了（{GameManager.instance.sceneName}） 不通过"); continue; }
                var hero = HeroController.instance;
                // His rage with all four swords away: the script must wait its beat, not go round in one frame until
                // PlayMaker turns it off (Items.FsmLoopGuards).
                if (System.Environment.GetEnvironmentVariable("TK_XERO_LOOP") != "0")
                {
                    var att = PlayMakerFSM.FindFsmOnGameObject(hm.gameObject, "Attacking");
                    if (att != null)
                    {
                        att.FsmVariables.GetFsmBool("Rage").Value = true;
                        att.FsmVariables.GetFsmBool("Summoned Swords").Value = true;
                        var held = new List<PlayMakerFSM>();
                        // The third and fourth wait unsummoned (inactive): theirs counted too.
                        foreach (var f in Resources.FindObjectsOfTypeAll<PlayMakerFSM>())
                            if (f != null && f.gameObject.scene == hm.gameObject.scene && f.FsmName == "xero_nail") held.Add(f);
                        foreach (var f in held) f.FsmVariables.GetFsmBool("Attacking").Value = true;
                        att.SetState("Antic");
                        yield return null;
                        bool alive = att.enabled;
                        string at = att.ActiveStateName;
                        all++;
                        bool loopOk = alive && at == "Wait Rage";
                        if (loopOk) ok++;
                        Plugin.Log.LogInfo($"[AutoTest] 泽若 {tag} 四把剑都在外时狂暴出手：脚本开着 {alive}，停在 {at}（剑 {held.Count} 把，守卫 {FsmLoopGuards.DevGuarded}） {(loopOk ? "通过" : "不通过")}");
                        if (!alive) att.enabled = true;
                        yield return new WaitForSeconds(1f);
                    }
                }
                var info = EnemyStats.Of(hm);
                int max = EnemyStats.LifeMax(info);
                var fsms = new List<PlayMakerFSM>();
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var f in root.GetComponentsInChildren<PlayMakerFSM>(true))
                        if (PathOf(f.transform).Contains("Xero") || PathOf(f.transform).Contains("Sword")) fsms.Add(f);
                var states = fsms.ToDictionary(f => f, f => f.ActiveStateName);
                Plugin.Log.LogInfo($"[AutoTest] 泽若 {tag}（{(minion ? "召唤物" : "玩家")}的打法，等级 {level}{(small ? "，小伤害" : "")}）：开打 生命 {EnemyStats.Life(hm, info)}/{max}，HK 血量 {hm.hp}，脚本 {fsms.Count} 个：{string.Join("；", fsms.Select(f => $"{PathOf(f.transform)}/{f.FsmName}@{f.ActiveStateName}"))}");
                var attack = fsms.FirstOrDefault(f => f.gameObject == hm.gameObject && f.FsmName == "Attacking");
                float start = Time.time, nextHit = 0f, lastAttackChange = Time.time, longestStill = 0f, lowAt = -1f;
                string lastAttack = attack?.ActiveStateName;
                bool watching = false;
                bool killed = false;
                while (hm != null && !hm.isDead && hm.gameObject.activeInHierarchy && Time.time - start < 240f)
                {
                    int life = EnemyStats.Life(hm, info);
                    float frac = life / (float)Mathf.Max(1, max);
                    if (!watching && frac < 0.4f) { watching = true; Plugin.Log.LogInfo($"[AutoTest] 泽若 {tag}：40% 以下，开始记状态"); }
                    foreach (var f in fsms)
                    {
                        if (f == null) continue;
                        string now = f.ActiveStateName;
                        if (states.TryGetValue(f, out var was) && was == now) continue;
                        states[f] = now;
                        if (watching) Plugin.Log.LogInfo($"[AutoTest] 泽若 {tag} {Time.time - start:F2}s 生命 {life}（HK {hm.hp}）{PathOf(f.transform)}/{f.FsmName}：{was} → {now}");
                    }
                    if (attack != null && attack.ActiveStateName != lastAttack) { lastAttack = attack.ActiveStateName; lastAttackChange = Time.time; }
                    if (watching) longestStill = Mathf.Max(longestStill, Time.time - lastAttackChange);
                    if (lowAt < 0f && frac < 0.05f) lowAt = Time.time;
                    // Stand by him, out of his way: the swords find the player anyway.
                    if (Time.time >= nextHit)
                    {
                        var col = hm.GetComponent<Collider2D>();
                        if (col != null && col.enabled)
                        {
                            int dmg = frac > 0.15f && !small ? Mathf.Max(30, Mathf.RoundToInt(max * 0.05f)) : Mathf.Max(8, Mathf.RoundToInt(max * (small ? 0.004f : 0.008f)));
                            int before = life;
                            mod.Melee.HitWith(col, dmg, 0f, 0f, hero.transform.position.x < col.bounds.center.x ? 1f : -1f, hero.transform.position, autonomous: minion && Time.time - start > 1f);
                            if (frac < 0.15f) Plugin.Log.LogInfo($"[AutoTest] 泽若 {tag} {Time.time - start:F2}s 打 {dmg}：生命 {before} → {EnemyStats.Life(hm, info)}，HK {hm.hp}，死了 {hm.isDead}");
                            if (hm.isDead) killed = true;
                        }
                        nextHit = Time.time + (frac > 0.15f && !small ? 0.3f : small ? 0.15f : 0.6f);
                    }
                    yield return null;
                }
                bool died = killed || hm != null && hm.isDead;
                Plugin.Log.LogInfo($"[AutoTest] 泽若 {tag}：{(died ? "打死了" : "没打死")}，用了 {Time.time - start:F1}s，40% 以下攻击脚本最长停在一个状态 {longestStill:F1}s，最后 {string.Join("；", fsms.Where(f => f != null).Select(f => $"{PathOf(f.transform)}/{f.FsmName}@{f.ActiveStateName}"))}");
                bool pass = died && longestStill < 8f;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 泽若 {tag} 打到最后一点血还在打、能打死 {(pass ? "通过" : "不通过")}");
                // After his death: whether Godhome's scene ends (the white way out back where the player came from).
                string room0 = GameManager.instance.sceneName;
                for (float w = Time.realtimeSinceStartup; died && Time.realtimeSinceStartup - w < 20f;)
                {
                    var gm = GameManager.instance;
                    var bsc = BossSceneController.Instance;
                    string ghost = string.Join("；", Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None)
                        .Where(f => f != null && f.gameObject.name.Contains("Ghost Death")).Select(f => $"{f.gameObject.name}/{f.FsmName}@{f.ActiveStateName}"));
                    Plugin.Log.LogInfo($"[AutoTest] 泽若 {tag} 死后 {Time.realtimeSinceStartup - w:F0}s 场景 {gm.sceneName} 过场 {gm.IsInSceneTransition} 首领场景 {(bsc != null ? $"有 出场中 {BossSceneController.IsTransitioning}" : "无")} 死亡形象 {ghost} 能动 {GameHooks.PlayerHasControl()}");
                    if (gm.sceneName != room0) break;
                    yield return new WaitForSecondsRealtime(1f);
                }
                bool left = GameManager.instance.sceneName != room0;
                Plugin.Log.LogInfo($"[AutoTest] 泽若 {tag} 死后离开了神居场景 {(left ? "通过" : "不通过")}");
                yield return new WaitForSeconds(3f);
            }
            BossSceneController.SetupEvent = null;
            World.EnemyAiMode.Current = keepMode;
            FsmLoopGuards.DevOff = false;
            Plugin.Log.LogInfo($"[AutoTest] 泽若 合计 {ok}/{all}");
        }
    }
}
