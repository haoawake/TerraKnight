using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "hivelooks", the user's 10-27 "小蜜蜂冲刺时贴图会变成蜂巢躯壳的贴图"): the Hive's enemies with the new enemy AI
    /// (Hive_04: Hivelings, Hive Soldiers, Husk Hives). Each kind's animation library and the clips its own scripts play are
    /// logged; then each Hiveling and Hive Soldier is made to lunge (EnemyAi.Brain.DevBegin) and every clip it plays for 1.5 s
    /// noted: a Hiveling's are its own "Bee …" ones (the Husk Hive's Jump Antic, Idle or Jump in the library they share are
    /// not). Run as before (Brain.DevAnyClip: any clip of the library) and now. Lines "[AutoTest] 蜂巢外观 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator HiveLooksTest()
        {
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            mod.DevNoFallDamage = true;
            World.EnemyAiMode.Current = World.EnemyAiKind.New;
            int ok = 0, all = 0;
            const string room = "Hive_04";
            yield return GoTo(room, EntryGateOf(room));
            yield return new WaitForSeconds(2f);
            var hero = HeroController.instance;
            var kinds = new[] { "Bee Stinger", "Bee Hatchling", "Big Bee", "Zombie Hive" };
            var logged = new HashSet<string>();
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                var a = hm != null ? hm.GetComponent<tk2dSpriteAnimator>() : null;
                string kind = hm != null ? kinds.FirstOrDefault(k => hm.name.StartsWith(k)) : null;
                if (a == null || a.Library == null || kind == null || !logged.Add(kind)) continue;
                Plugin.Log.LogInfo($"[AutoTest] 蜂巢外观 {kind}：动画库 {a.Library.name}：{string.Join(",", a.Library.clips.Where(c => c != null).Select(c => c.name))}");
            }
            foreach (bool fixedNow in new[] { true, false })
            {
                EnemyAi.Brain.DevAnyClip = !fixedNow;
                // The room afresh: its bees all there.
                yield return GoTo("Hive_03", EntryGateOf("Hive_03"));
                yield return new WaitForSeconds(1f);
                yield return GoTo(room, EntryGateOf(room));
                yield return new WaitForSeconds(2f);
                hero = HeroController.instance;
                int foreign = 0, lunges = 0;
                var seen = new HashSet<string>();
                var bees = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None)
                    .Where(h => h != null && !h.isDead && h.gameObject.activeInHierarchy && (h.name.StartsWith("Bee Hatchling") || h.name.StartsWith("Bee Stinger"))).ToList();
                foreach (var bee in bees.Take(6))
                {
                    if (bee == null || bee.isDead) continue;
                    var brain = bee.GetComponent<EnemyAi.Brain>();
                    var a = bee.GetComponent<tk2dSpriteAnimator>();
                    if (brain == null || a == null) { Plugin.Log.LogInfo($"[AutoTest] 蜂巢外观 {bee.name} 没有新版 AI（{brain == null}）或动画"); continue; }
                    hero.transform.position = Clear(hero, (Vector2)bee.transform.position + new Vector2(-4f, -1f));
                    yield return new WaitForSeconds(0.4f);
                    if (bee == null || bee.isDead) { Plugin.Log.LogInfo("[AutoTest] 蜂巢外观 这只已经死了"); continue; }
                    if (!brain.DevBegin(EnemyAi.Kind.Lunge)) { Plugin.Log.LogInfo($"[AutoTest] 蜂巢外观 {bee.name} 没有突进招式"); continue; }
                    lunges++;
                    bool hatchling = bee.name.StartsWith("Bee Hatchling");
                    for (float t = Time.time + 1.5f; Time.time < t && bee != null && !bee.isDead;)
                    {
                        var c = a.CurrentClip;
                        if (c != null)
                        {
                            seen.Add((hatchling ? "小蜂:" : "士兵:") + c.name);
                            if (hatchling && !c.name.StartsWith("Bee ")) foreign++;
                        }
                        yield return null;
                    }
                    if (!fixedNow && foreign > 0 && !logged.Contains("shot")) { logged.Add("shot"); Shot(room, "hivelooks_before"); }
                }
                bool pass = fixedNow ? foreign == 0 && lunges > 0 : true;
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 蜂巢外观 {(fixedNow ? "修好后" : "修好前（任意片段）")}：突进 {lunges} 次，播过 {string.Join(",", seen)}；小蜂用了别人的片段 {foreign} 帧，"
                    + $"拒绝别人的片段 {EnemyAi.Brain.DevForeignClipsRefused} 次 {(pass ? "通过" : "不通过")}");
                yield return new WaitForSeconds(1f);
            }
            EnemyAi.Brain.DevAnyClip = false;
            Shot(room, "hivelooks_after");
            Plugin.Log.LogInfo($"[AutoTest] 蜂巢外观 合计 {ok}/{all}");
        }
    }
}
