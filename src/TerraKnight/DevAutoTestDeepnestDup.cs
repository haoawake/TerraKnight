using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "deepnestdup"): a player's report — Deepnest's enemies multiply, one into two, two into four. A few of
    /// Deepnest's rooms each: the live enemies counted by kind on entering; three rounds of every one killed and back (the
    /// respawn wait cut to two seconds), counted after each; out to the hall and back twice, counted again. A kind whose
    /// count keeps rising is the report.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator DeepnestDupTest()
        {
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            Player.TerraStats.DevImmortal = true;
            float fixedWas = EnemyRespawn.DevFixedSeconds;
            int grew = 0, rooms = 0;
            try
            {
                foreach (string room in new[] { "Deepnest_01", "Deepnest_01b", "Deepnest_02", "Deepnest_03", "Deepnest_10", "Deepnest_14", "Deepnest_16", "Deepnest_17", "Deepnest_33", "Deepnest_34", "Deepnest_35", "Deepnest_38", "Deepnest_39", "Deepnest_41", "Deepnest_44" })
                {
                    string gate = EntryGateOf(room);
                    if (gate == null) continue;
                    yield return GoTo(room, gate);
                    if (GameManager.instance.sceneName != room) continue;
                    rooms++;
                    yield return new WaitForSeconds(3f);
                    var counts = new List<string>();
                    string Census()
                    {
                        var by = new SortedDictionary<string, int>();
                        foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                            if (hm != null && !hm.isDead && hm.gameObject.activeInHierarchy && hm.gameObject.scene.name == room)
                            {
                                string k = UI.EnemyHover.RecordOf(hm) ?? hm.name;
                                by[k] = by.TryGetValue(k, out int n) ? n + 1 : 1;
                            }
                        return string.Join(" ", by.Select(kv => $"{kv.Key}×{kv.Value}"));
                    }
                    int Total() => Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None).Count(hm => hm != null && !hm.isDead && hm.gameObject.activeInHierarchy && hm.gameObject.scene.name == room);
                    int first = Total();
                    counts.Add($"进门 {first}：{Census()}");
                    EnemyRespawn.DevFixedSeconds = 2f;
                    var hero = HeroController.instance;
                    for (int round = 1; round <= 3; round++)
                    {
                        foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                            if (hm != null && !hm.isDead && hm.gameObject.activeInHierarchy && hm.gameObject.scene.name == room && !Items.EnemyStats.Of(hm).Boss
                                && Vector2.Distance(hm.transform.position, hero.transform.position) > 3f)
                                hm.Die(0f, AttackTypes.Generic, true);
                        yield return new WaitForSeconds(7f);
                        counts.Add($"第 {round} 轮杀光后 {Total()}：{Census()}");
                    }
                    EnemyRespawn.DevFixedSeconds = fixedWas;
                    for (int visit = 1; visit <= 2; visit++)
                    {
                        yield return SealTravel("Deepnest_Spider_Town", EntryGateOf("Deepnest_Spider_Town") ?? "left1");
                        yield return GoTo(room, gate);
                        yield return new WaitForSeconds(3f);
                        counts.Add($"第 {visit} 次回来 {Total()}：{Census()}");
                    }
                    int last = Total();
                    bool rose = last > first + 1;
                    if (rose) grew++;
                    Plugin.Log.LogInfo($"[AutoTest] 深巢刷怪 {room}：\n    {string.Join("\n    ", counts)} {(rose ? "不通过" : "通过")}");
                }
            }
            finally
            {
                EnemyRespawn.DevFixedSeconds = fixedWas;
                Player.TerraStats.DevImmortal = false;
            }
            Plugin.Log.LogInfo($"[AutoTest] 深巢刷怪 {rooms} 个房间里数量越来越多的 {grew} 个 {(grew == 0 ? "通过" : "不通过")}");
        }
    }
}
