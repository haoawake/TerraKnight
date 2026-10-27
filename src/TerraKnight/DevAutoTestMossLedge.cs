using System;
using System.Collections;
using System.Linq;
using TerraKnight.EnemyAi.Bosses;
using TerraKnight.Items;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "mossledge", the user's 10-25 "大型苔藓冲锋者跳到左边的平台上会卡好几秒"): the Massive Moss Charger
    /// with the new AI, in Greenpath's arena and Godhome's, the player held on the highest ground left (then right) of his
    /// grass line; for TK_MOSS_SECONDS (25) each, how long each of his leaps stays in the air, how often he hops off
    /// something over his ground. Lines "[AutoTest] 苔藓高台 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator MossLedgeTest()
        {
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            World.EnemyAiMode.Current = World.EnemyAiKind.New;
            float seconds = float.TryParse(Environment.GetEnvironmentVariable("TK_MOSS_SECONDS"), out var s) ? s : 25f;
            MegaMossChargerKit.DevNoHop = Environment.GetEnvironmentVariable("TK_MOSS_NOHOP") == "1";
            foreach (string hall in new[] { null, "GG_Mega_Moss_Charger" })
            {
                if (hall == null) BossArena.Fight("MegaMossCharger", true);
                else yield return GoTo(hall, "door_dreamEnter");
                BossKit kit = null;
                for (float until = Time.realtimeSinceStartup + 40f; Time.realtimeSinceStartup < until && (kit == null || GameManager.instance.IsInSceneTransition);)
                {
                    kit = Object.FindObjectsByType<BossKit>(FindObjectsSortMode.None).FirstOrDefault(k => k.Record == "MegaMossCharger");
                    yield return null;
                }
                string scene = GameManager.instance.sceneName;
                if (kit == null) { Plugin.Log.LogInfo($"[AutoTest] 苔藓高台 {scene}：没有首领"); continue; }
                var fsm = kit.GetComponents<PlayMakerFSM>().FirstOrDefault(f => f.FsmName == "Mossy Control");
                float xMin = fsm.FsmVariables.FindFsmFloat("X Min").Value, xMax = fsm.FsmVariables.FindFsmFloat("X Max").Value;
                float startY = fsm.FsmVariables.FindFsmFloat("Start Y").Value;
                var hero = HeroController.instance;
                var hrb = hero.GetComponent<Rigidbody2D>();
                foreach (int side in new[] { -1, 1 })
                {
                    // A surface up off his floor (2 or more over the floor under his line's middle), the furthest one this
                    // side from 8 past his line's end in to its middle; else the highest ground past the end.
                    var floorHit = Physics2D.Raycast(new Vector2((xMin + xMax) / 2f, startY + 1f), Vector2.down, 20f, 1 << 8);
                    float floor = floorHit.collider != null ? floorHit.point.y : startY - 3f;
                    Vector2 best = Vector2.zero;
                    bool found = false;
                    float mid = (xMin + xMax) / 2f;
                    for (float k = 0f; k <= (xMax - xMin) / 2f + 8f && !found; k += 0.5f)
                    {
                        float x = side < 0 ? xMin - 8f + k : xMax + 8f - k;
                        if (side < 0 ? x > mid : x < mid) break;
                        var hit = Physics2D.Raycast(new Vector2(x, floor + 12f), Vector2.down, 14f, 1 << 8);
                        if (hit.collider != null && hit.point.y > floor + 2f && hit.point.y < floor + 11f) { best = hit.point; found = true; }
                    }
                    if (!found) { Plugin.Log.LogInfo($"[AutoTest] 苔藓高台 {scene} {(side < 0 ? "左" : "右")}：没找到地面"); continue; }
                    hrb.constraints = RigidbodyConstraints2D.FreezeRotation;
                    hero.transform.position = Clear(hero, best + Vector2.up * 1.2f);
                    hrb.linearVelocity = Vector2.zero;
                    yield return new WaitForSeconds(0.6f);
                    hrb.constraints = RigidbodyConstraints2D.FreezeAll;
                    if (!kit.Engaged) BossWake.Rouse(kit.Hm);
                    int hops0 = MegaMossChargerKit.DevHops, leaps = 0;
                    float airSince = -1f, longest = 0f;
                    for (float end = Time.time + seconds; Time.time < end && kit != null && !kit.Hm.isDead;)
                    {
                        if (kit.Hm.hp < 200) kit.Hm.hp = 2000;
                        bool air = fsm.ActiveStateName == "In Air";
                        if (air && airSince < 0f) { airSince = Time.time; leaps++; }
                        if (!air && airSince >= 0f) { longest = Mathf.Max(longest, Time.time - airSince); airSince = -1f; }
                        yield return null;
                    }
                    if (airSince >= 0f) longest = Mathf.Max(longest, Time.time - airSince);
                    hrb.constraints = RigidbodyConstraints2D.FreezeRotation;
                    Plugin.Log.LogInfo($"[AutoTest] 苔藓高台 {scene} {(side < 0 ? "左" : "右")}：玩家站在 {best:F1}（他的地面 {xMin:F1}~{xMax:F1}，高 {startY:F1}），" +
                        $"跳 {leaps} 次，最长在空中 {longest:F1} 秒，从高处跳下 {MegaMossChargerKit.DevHops - hops0} 次 {(longest < 2.5f ? "通过" : "不通过")}");
                    // Put down on that ledge in the middle of a leap, as the player's report has it: how long until he is
                    // back on his own ground.
                    for (float until = Time.time + 20f; Time.time < until && kit != null && fsm.ActiveStateName != "In Air";) yield return null;
                    if (kit == null || fsm.ActiveStateName != "In Air") { Plugin.Log.LogInfo($"[AutoTest] 苔藓高台 {scene} {(side < 0 ? "左" : "右")} 放上高台：等不到他起跳"); continue; }
                    var krb = kit.GetComponent<Rigidbody2D>();
                    var kcol = kit.GetComponent<Collider2D>();
                    float lift = kcol != null ? kit.transform.position.y - kcol.bounds.min.y : 1.5f;
                    hero.transform.position = Clear(hero, best + new Vector2(side < 0 ? -4f : 4f, 1.2f));
                    kit.transform.position = new Vector3(best.x, best.y + lift + 0.05f, kit.transform.position.z);
                    if (krb != null) krb.linearVelocity = Vector2.zero;
                    int hops1 = MegaMossChargerKit.DevHops;
                    float put = Time.time;
                    while (Time.time - put < 8f && kit != null && fsm.ActiveStateName == "In Air") yield return null;
                    float stayed = Time.time - put;
                    Plugin.Log.LogInfo($"[AutoTest] 苔藓高台 {scene} {(side < 0 ? "左" : "右")} 放上高台：{stayed:F1} 秒后离开空中（现在 {fsm.ActiveStateName}），" +
                        $"从高处跳下 {MegaMossChargerKit.DevHops - hops1} 次 {(stayed < 2f ? "通过" : "不通过")}");
                }
            }
        }
    }
}
