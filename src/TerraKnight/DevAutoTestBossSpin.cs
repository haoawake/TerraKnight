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
    /// Dev (suite "bossspin", the user's 10-29 "有些boss会卡住现在，比如收藏家会原地自转，神驯也是，但是概率触发"): the Collector and
    /// the God Tamer with the new AI, TK_SPIN_SECONDS (90) each against a player put somewhere new every 3 s (on the floor
    /// either side, right over the boss, under the arena's top). Every boss body in the room watched each frame: a second
    /// in which it turned round 4 times or more while moving under 1.5 is a spin (its states then logged, the player's place
    /// against it); its main script 6 s in one state while engaged is a stall. Lines "[AutoTest] BossSpin …".
    /// TK_SPIN_BOSSES picks others ("key" or "key#hall").
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator BossSpinTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            mod.ApplyCreative(false);
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            World.EnemyAiMode.Current = World.EnemyAiKind.New;
            string pick = Environment.GetEnvironmentVariable("TK_SPIN_BOSSES");
            var keys = string.IsNullOrWhiteSpace(pick) ? new[] { "JarCollector", "LobsterLancer#GG_God_Tamer" } : pick.Split(';');
            float seconds = float.TryParse(Environment.GetEnvironmentVariable("TK_SPIN_SECONDS"), out var s) ? s : 150f;
            int spins = 0, stalls = 0;
            foreach (string entry in keys.Where(k => !string.IsNullOrWhiteSpace(k)))
            {
                string key = entry.Split('#')[0];
                string hall = entry.Contains("#") ? entry.Split('#')[1] : null;
                string record = key.Split('@')[0];
                PlayerData.instance.isInvincible = false;
                if (hall != null) yield return GoTo(hall, "door_dreamEnter");
                else if (BossArena.Has(key)) BossArena.Fight(key, true);
                BossKit kit = null;
                float until = Time.realtimeSinceStartup + 70f;
                while (Time.realtimeSinceStartup < until)
                {
                    kit = KitOf(record);
                    if (kit != null && !GameManager.instance.IsInSceneTransition) break;
                    yield return null;
                }
                if (kit == null) { Plugin.Log.LogInfo($"[AutoTest] BossSpin {key} 没有首领"); continue; }
                var hero = HeroController.instance;
                var hbody = hero.GetComponent<Rigidbody2D>();
                float wakeUntil = Time.time + 6f;
                while (!kit.Engaged && Time.time < wakeUntil)
                {
                    if (!BossWake.Rouse(kit.Hm))
                    {
                        kit.Hm.Hit(new HitInstance { Source = hero.gameObject, AttackType = AttackTypes.Generic, DamageDealt = 1, Direction = 0f, Multiplier = 1f, MagnitudeMultiplier = 0f });
                        FSMUtility.SendEventToGameObject(kit.gameObject, "TAKE DAMAGE");
                    }
                    kit = KitOf(record) ?? kit;
                    yield return new WaitForSeconds(1f);
                }
                yield return new WaitForSeconds(2f);
                Plugin.Log.LogInfo($"[AutoTest] BossSpin {key}：开打，状态 {kit.StateNow}，场地 {kit.Arena}");
                // Each boss body: its facing, where it was a second ago, its flips this second, its states this second.
                var facing = new Dictionary<HealthManager, float>();
                var flips = new Dictionary<HealthManager, int>();
                var at = new Dictionary<HealthManager, Vector2>();
                var seen = new Dictionary<HealthManager, HashSet<string>>();
                var stateSince = new Dictionary<HealthManager, (string State, float Since)>();
                float windowStart = Time.time, nextPlace = 0f;
                int place = 0, mySpins = 0, myStalls = 0;
                string placeName = "";
                Vector2 held = Vector2.zero;
                bool hold = false;
                float t0 = Time.time;
                while (Time.time - t0 < seconds)
                {
                    yield return null;
                    kit = KitOf(record) ?? kit;
                    if (kit == null || kit.Hm == null || kit.Hm.isDead) break;
                    if (kit.Info != null && kit.Hm.hp < kit.Info.MaxHp * 0.3f) kit.Hm.hp = Mathf.RoundToInt(kit.Info.MaxHp * 0.6f);
                    mod.Stats.FullHeal();
                    if (Time.time >= nextPlace)
                    {
                        nextPlace = Time.time + 3f;
                        var floor = kit.ClampToArena(kit.Me, 2f);
                        floor.y = kit.GroundBelow(new Vector2(floor.x, kit.Arena.center.y)) + 1.2f;
                        Vector2 spot;
                        switch (place++ % 4)
                        {
                            case 0: spot = new Vector2(Mathf.Clamp(floor.x - 4f, kit.Arena.xMin + 1f, kit.Arena.xMax - 1f), floor.y); placeName = "左边地面"; break;
                            case 1: spot = new Vector2(Mathf.Clamp(floor.x + 4f, kit.Arena.xMin + 1f, kit.Arena.xMax - 1f), floor.y); placeName = "右边地面"; break;
                            case 2: spot = new Vector2(kit.Me.x, Mathf.Min(kit.Me.y + 4.5f, kit.CeilingAbove(kit.Me, 40f) - 1.6f)); placeName = "正上方"; break;
                            default: spot = new Vector2(floor.x, Mathf.Min(kit.CeilingAbove(floor, 40f), kit.Arena.yMax) - 1.6f); placeName = "高处"; break;
                        }
                        hero.transform.position = new Vector3(spot.x, spot.y, hero.transform.position.z);
                        if (hbody != null) hbody.linearVelocity = Vector2.zero;
                        Physics2D.SyncTransforms();
                        // Kept up there (wings, a wall) the whole 3 s: the new AI's moves against the air come out.
                        held = spot;
                        hold = placeName == "正上方" || placeName == "高处";
                    }
                    else if (hold)
                    {
                        hero.transform.position = new Vector3(held.x, held.y, hero.transform.position.z);
                        if (hbody != null) hbody.linearVelocity = Vector2.zero;
                    }
                    foreach (var h in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                    {
                        if (h == null || h.isDead || !h.gameObject.activeInHierarchy) continue;
                        var info = EnemyStats.Of(h);
                        if (info == null || !info.Boss) continue;
                        float f = Mathf.Sign(h.transform.lossyScale.x);
                        if (facing.TryGetValue(h, out float was) && was != f) flips[h] = (flips.TryGetValue(h, out int n) ? n : 0) + 1;
                        facing[h] = f;
                        if (!at.ContainsKey(h)) at[h] = h.transform.position;
                        var fsm = h.GetComponents<PlayMakerFSM>().FirstOrDefault(m => m.FsmName == "Control" || m.FsmName.EndsWith(" Control") || m.FsmName == "Lancer" || m.FsmName == "Lobster");
                        string st = fsm != null ? fsm.FsmName + "/" + fsm.ActiveStateName : "?";
                        if (!seen.TryGetValue(h, out var set)) seen[h] = set = new HashSet<string>();
                        set.Add(st);
                        if (!stateSince.TryGetValue(h, out var ss) || ss.State != st) stateSince[h] = (st, Time.time);
                        else if (Time.time - ss.Since >= 6f && kit.Engaged)
                        {
                            myStalls++;
                            Plugin.Log.LogInfo($"[AutoTest] BossSpin {key} 停住：{h.name} 在 {st} 已 {Time.time - ss.Since:F1} 秒，位置 {(Vector2)h.transform.position:F1}，玩家{placeName}");
                            stateSince[h] = (st, Time.time);
                        }
                    }
                    if (Time.time - windowStart >= 1f)
                    {
                        foreach (var kv in flips)
                        {
                            var h = kv.Key;
                            if (h == null) continue;
                            float moved = at.TryGetValue(h, out var a) ? Vector2.Distance(a, h.transform.position) : 99f;
                            if (kv.Value >= 4 && moved < 1.5f)
                            {
                                mySpins++;
                                Plugin.Log.LogInfo($"[AutoTest] BossSpin {key} 自转：{h.name} 一秒转身 {kv.Value} 次，移动 {moved:F2}，状态 {string.Join("、", seen.TryGetValue(h, out var sts) ? sts : new HashSet<string>())}，玩家{placeName}（相对 {(Vector2)(hero.transform.position - h.transform.position):F1}）");
                            }
                        }
                        flips.Clear();
                        at.Clear();
                        seen.Clear();
                        windowStart = Time.time;
                    }
                }
                spins += mySpins;
                stalls += myStalls;
                Plugin.Log.LogInfo($"[AutoTest] BossSpin {key}：{seconds:F0} 秒，自转 {mySpins} 次，停住 {myStalls} 次，脱困 {BossAi.DevUnstuck}，对空 {BossAi.DevAntiAir}");
                if (kit != null && kit.Hm != null) kit.Hm.hp = 1;
                yield return new WaitForSeconds(1f);
            }
            Plugin.Log.LogInfo($"[AutoTest] BossSpin 合计 自转 {spins} 次，停住 {stalls} 次：{(spins == 0 && stalls == 0 ? "通过" : "不通过")}");
        }
    }
}
