using System.Collections;
using System.Collections.Generic;
using System.Text;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "sealstart", or "sealstart_Key1.Key2"): every boss seal used through the item route: straight into its arena,
    /// one spent on arrival, the fight begun with no step of the player's (BossSummonStart), the player alive. Each arena
    /// photographed two seconds after its start, its boss's FSM states logged.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator SealStartTest(string only)
        {
            if (!DevCopy.Verified) { Plugin.Log.LogError("[AutoTest] 召唤开战需要隔离存档，不通过"); yield break; }
            var inv = mod.Inventory;
            var saved = (ItemStack[])inv.Slots.Clone();
            bool infinite = inv.Infinite, dreamNail = PlayerData.instance.hasDreamNail;
            int stage = Shop.DevStage, selected = inv.Selected;
            int crystals = mod.Stats.LifeCrystals, fruits = mod.Stats.LifeFruits;
            var kills = new Dictionary<string, int>(Bestiary.Kills);
            // A boss of a later stage one-shots the test's gear: hits leave a life (a death in a dream wakes the Knight).
            Player.TerraStats.DevImmortal = true;
            int ok = 0, all = 0;
            var bad = new List<string>();
            void Check(string key, bool pass, string detail)
            {
                all++;
                if (pass) ok++; else if (!bad.Contains(key)) bad.Add(key);
                Plugin.Log.LogInfo($"[AutoTest] 召唤开战 {key} {detail} {(pass ? "通过" : "不通过")}");
            }
            var keys = only != null ? new HashSet<string>(only.Split('.')) : null;
            if (only == "world") { keys = new HashSet<string>(); foreach (var k in BossArena.DevKeys()) if (BossArena.DevWorldArena(k, out _, out _)) keys.Add(k); }
            try
            {
                yield return SealTravel("Town", "left1");
                inv.Infinite = false;
                Shop.DevStage = 5;
                // A full column of hearts: a warrior's blow at its stage outweighs starting life in one go.
                mod.Stats.LifeCrystals = TerraKnight.Player.TerraStats.MaxLifeCrystals;
                mod.Stats.LifeFruits = 20;
                PlayerData.instance.hasDreamNail = true;
                foreach (var s in BossSummons.All)
                {
                    if (keys != null && !keys.Contains(s.Key)) continue;
                    Bestiary.Kills[s.Key] = 1;
                    // The boss's own stage: its blows as there (one in starting gear lives through them, healed each frame).
                    Shop.DevStage = s.Stage;
                    inv.Slots[0] = ItemStack.Of(s.Type, 3);
                    inv.Selected = 0;
                    inv.Override = -1;
                    PlayerData.instance.isInvincible = true;
                    float ready = Time.realtimeSinceStartup + 15f;
                    while (!GameHooks.PlayerHasControl() && Time.realtimeSinceStartup < ready) yield return null;
                    if (!GameHooks.PlayerHasControl() && HeroController.instance != null && !GameManager.instance.IsInSceneTransition)
                    {
                        Plugin.Log.LogWarning($"[AutoTest] 召唤开战 {s.Key} 之前主角没有操作权（上一场留下的），交还后继续");
                        HeroController.instance.RegainControl();
                        HeroController.instance.StartAnimationControl();
                        yield return new WaitForSecondsRealtime(0.3f);
                    }
                    string why = BossSummons.Refusal(s);
                    if (why != null) { Check(s.Key, false, "不能使用：" + why); continue; }
                    int started = BossSummonStart.DevStarted, consumed = BossSummons.DevConsumed;
                    float t0 = Time.realtimeSinceStartup;
                    mod.DevUseSelected(HeroController.instance.transform.position);
                    // In the arena, one spent.
                    float there = t0 + 30f;
                    while (!BossArena.Arrived(s.Key) && Time.realtimeSinceStartup < there) yield return null;
                    float arrived = Time.realtimeSinceStartup - t0;
                    yield return new WaitForSecondsRealtime(0.3f);
                    string scene = GameManager.instance.sceneName;
                    Check(s.Key, BossArena.Arrived(s.Key) && inv.Slots[0].Count == 2 && BossSummons.DevConsumed == consumed + 1,
                        $"进竞技场并扣一个：{scene}（{arrived:F1} 秒），剩 {inv.Slots[0].Count}，{BossSummons.DevLast}");
                    // The fight on with nothing done by the player.
                    bool dreamOrGod = scene.StartsWith("GG_") || scene.StartsWith("Dream_") || scene.StartsWith("Grimm_Nightmare");
                    float fight = Time.realtimeSinceStartup + 30f;
                    while (!dreamOrGod && BossSummonStart.DevStarted == started && Time.realtimeSinceStartup < fight
                           && !BossSummonStart.DevLast.StartsWith(s.Key + " 未确认")) { mod.Stats.FullHeal(); yield return null; }
                    // A dream's challenge (the Radiance's) taken up before looking.
                    if (dreamOrGod && !scene.StartsWith("GG_"))
                        for (float t = Time.realtimeSinceStartup + 8f; !BossSummonStart.DevLast.StartsWith(s.Key) && Time.realtimeSinceStartup < t;) yield return null;
                    if (!dreamOrGod)
                        Check(s.Key, BossSummonStart.DevStarted == started + 1, $"立刻开战（用后 {Time.realtimeSinceStartup - t0:F1} 秒）：{BossSummonStart.DevLast}");
                    // A report: the one who used the seal stuck in a wall. Frames of the start in terrain, and where.
                    int walled = 0, frames = 0;
                    string wallWhat = null;
                    void Wall()
                    {
                        frames++;
                        if (HeroController.instance != null && NbInWall(HeroController.instance, out string w)) { walled++; wallWhat ??= $"{w} @{(Vector2)HeroController.instance.transform.position:F1}"; }
                    }
                    for (float hold = Time.realtimeSinceStartup + 2f; Time.realtimeSinceStartup < hold;) { mod.Stats.FullHeal(); Wall(); yield return null; }
                    // A roar or a warrior's title holds the player a moment.
                    for (float ctl = Time.realtimeSinceStartup + 8f; !GameHooks.PlayerHasControl() && Time.realtimeSinceStartup < ctl;) { mod.Stats.FullHeal(); Wall(); yield return null; }
                    for (float more = Time.realtimeSinceStartup + 1f; Time.realtimeSinceStartup < more;) { mod.Stats.FullHeal(); Wall(); yield return null; }
                    Check(s.Key, walled == 0, $"没卡在墙里：{walled}/{frames} 帧在地形里{(wallWhat != null ? "，" + wallWhat : "")}");
                    var sb = new StringBuilder();
                    string record = s.Key.Split('@')[0];
                    foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                        if (UI.EnemyHover.RecordOf(hm) == record)
                            sb.Append($" [{hm.name} hp{hm.hp}{FsmStates(hm.gameObject, false)}]");
                    var h = HeroController.instance;
                    Check(s.Key, !h.cState.dead && GameManager.instance.sceneName == scene && GameHooks.PlayerHasControl(), $"之后：首领{(sb.Length > 0 ? sb.ToString() : " 不在场")}，主角 {(Vector2)h.transform.position:F1} 可操作 {GameHooks.PlayerHasControl()}");
                    foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                        if (f.gameObject.name == "Boss Control" || f.FsmName == "Challenge Start")
                            Plugin.Log.LogInfo($"[AutoTest] 召唤开战 {s.Key} 状态机 {f.gameObject.scene.name}/{f.gameObject.name} '{f.FsmName}'：{f.ActiveStateName}");
                    Shot(scene, "sealstart_" + s.Key.Replace("@", "_"));
                    yield return new WaitForSecondsRealtime(0.3f);
                    // A dream's boss a few seconds on: into its attacks (the Radiance shows itself after its arena's set).
                    if (dreamOrGod && !scene.StartsWith("GG_"))
                    {
                        for (float later = Time.realtimeSinceStartup + 4f; Time.realtimeSinceStartup < later;) { mod.Stats.FullHeal(); yield return null; }
                        var lb = new StringBuilder();
                        foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                            if (UI.EnemyHover.RecordOf(hm) == record) lb.Append($" [{hm.name} {(Vector2)hm.transform.position:F1}{FsmStates(hm.gameObject, false)}]");
                        Plugin.Log.LogInfo($"[AutoTest] 召唤开战 {s.Key} 4 秒后：{lb}");
                        Shot(scene, "sealstart_later_" + s.Key.Replace("@", "_"));
                        yield return new WaitForSecondsRealtime(0.3f);
                    }
                    // Not away in the middle of a boss's entrance (the Grey Prince's holds the player): a teleport then
                    // leaves the player held in Dirtmouth, and every seal after refused.
                    var held = new StringBuilder();
                    for (float ctl = Time.realtimeSinceStartup + 20f, next = 0f; !GameHooks.PlayerHasControl() && Time.realtimeSinceStartup < ctl;)
                    {
                        mod.Stats.FullHeal();
                        if (Time.realtimeSinceStartup >= next && held.Length < 1500)
                        {
                            next = Time.realtimeSinceStartup + 1f;
                            var hh = HeroController.instance;
                            var bs = new StringBuilder();
                            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                                if (UI.EnemyHover.RecordOf(hm) == record) bs.Append(FsmStates(hm.gameObject, false));
                            held.Append($" [输入{(hh.acceptingInput ? 1 : 0)} 交出{(hh.controlReqlinquished ? 1 : 0)} 对话{(UI.HudDialogue.DevOpen ? 1 : 0)} 还操作{BossSummonStart.DevFreed}{bs}]");
                        }
                        yield return null;
                    }
                    if (held.Length > 0) Plugin.Log.LogInfo($"[AutoTest] 召唤开战 {s.Key} 离开前等操作权：{held} → {GameHooks.PlayerHasControl()}");
                    yield return SealTravel("Town", "left1");
                    float clean = Time.realtimeSinceStartup + 10f;
                    while ((BossArena.Visiting || BossSummons.Pending) && Time.realtimeSinceStartup < clean) yield return null;
                    mod.Stats.FullHeal();
                }
            }
            finally
            {
                System.Array.Copy(saved, inv.Slots, saved.Length);
                inv.Selected = selected;
                inv.Infinite = infinite;
                Shop.DevStage = stage;
                mod.Stats.LifeCrystals = crystals;
                mod.Stats.LifeFruits = fruits;
                PlayerData.instance.hasDreamNail = dreamNail;
                Bestiary.Kills.Clear();
                foreach (var kv in kills) Bestiary.Kills[kv.Key] = kv.Value;
                Player.TerraStats.DevImmortal = false;
                Plugin.Log.LogInfo($"[AutoTest] 召唤开战 合计 {ok}/{all} {(ok == all ? "通过" : "不通过：" + string.Join("、", bad))}");
            }
        }
    }
}
