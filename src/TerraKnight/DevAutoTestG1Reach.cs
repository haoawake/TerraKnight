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
    /// Dev (suite "g1reach"): bossreach's places (standing, 3.5 up, under the arena's top, against the nearer wall 6 up), each
    /// boss's moves told apart: for every state of its main script, how often it came, the hits taken in it, and the closest
    /// its own harmful colliders (DamageHero under its root) came to the player then — an aim that never comes near against
    /// one that comes near and misses. TK_REACH_BOSSES picks the bosses, TK_REACH_SECONDS the time a place (20),
    /// TK_REACH_PLACES which places (0123).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        sealed class StateTally
        {
            public int Count, Hits;
            public float Closest = float.MaxValue, SumClosest;
        }

        IEnumerator G1ReachTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            mod.ApplyCreative(false);
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            World.EnemyAiMode.Current = World.EnemyAiKind.New;
            string pick = Environment.GetEnvironmentVariable("TK_REACH_BOSSES");
            var keys = string.IsNullOrWhiteSpace(pick) ? new[] { "BigFly", "BigBuzzer", "MageKnight", "Hornet", "MantisLord", "TraitorLord" } : pick.Split(';');
            float seconds = float.TryParse(Environment.GetEnvironmentVariable("TK_REACH_SECONDS"), out var s) ? s : 20f;
            string placesWanted = Environment.GetEnvironmentVariable("TK_REACH_PLACES") ?? "0123";
            var names = new[] { "地面", "低空", "高空", "贴墙" };
            foreach (string key in keys.Where(k => !string.IsNullOrWhiteSpace(k)))
            {
                string record = key.Split('@')[0];
                PlayerData.instance.isInvincible = false;
                if (BossArena.Has(key)) BossArena.Fight(key, true);
                else
                {
                    string scene = EnemyDrops.SceneOf(record);
                    if (string.IsNullOrEmpty(scene)) continue;
                    yield return GoTo(scene, EntryGateOf(scene));
                }
                BossKit kit = null;
                float until = Time.realtimeSinceStartup + 70f, clearAt = Time.realtimeSinceStartup + 8f;
                while (Time.realtimeSinceStartup < until)
                {
                    kit = KitOf(record);
                    if (kit != null && !GameManager.instance.IsInSceneTransition) break;
                    if (Time.realtimeSinceStartup >= clearAt && !GameManager.instance.IsInSceneTransition)
                    {
                        clearAt = Time.realtimeSinceStartup + 2f;
                        foreach (var h in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                        {
                            var info = EnemyStats.Of(h);
                            if (h == null || h.isDead || !h.gameObject.activeInHierarchy || info == null || info.Boss || info.Record == record) continue;
                            if (!h.gameObject.scene.name.StartsWith(GameManager.instance.sceneName)) continue;
                            h.Die(null, AttackTypes.Generic, true);
                        }
                    }
                    yield return null;
                }
                if (kit == null) { Plugin.Log.LogInfo($"[G1_REACH] {key} 没有首领"); continue; }
                var hero = HeroController.instance;
                var hbody = hero.GetComponent<Rigidbody2D>();
                var hcol = hero.GetComponent<Collider2D>();
                float wakeUntil = Time.time + 6f;
                while (!kit.Engaged && Time.time < wakeUntil)
                {
                    if (!BossWake.Rouse(kit.Hm))
                    {
                        kit.Hm.Hit(new HitInstance { Source = hero.gameObject, AttackType = AttackTypes.Generic, DamageDealt = 1, Direction = 0f, Multiplier = 1f, MagnitudeMultiplier = 0f });
                        FSMUtility.SendEventToGameObject(kit.gameObject, "TAKE DAMAGE");
                    }
                    yield return new WaitForSeconds(1f);
                    kit = KitOf(record) ?? kit;
                }
                Plugin.Log.LogInfo($"[G1_REACH] {key} 开战={kit.Engaged} 状态 {kit.StateNow} 场地 {kit.Arena}");
                if (kit.Info != null) kit.Hm.hp = Mathf.Min(kit.Hm.hp, Mathf.RoundToInt(kit.Info.MaxHp * 0.6f));
                for (int p = 0; p < names.Length && kit != null && kit.Hm != null && !kit.Hm.isDead; p++)
                {
                    if (placesWanted.IndexOf((char)('0' + p)) < 0) continue;
                    var floor = kit.ClampToArena(kit.Me, 3f);
                    floor.x += floor.x - 4f > kit.Arena.xMin + 1f ? -4f : 4f;
                    floor.y = kit.GroundBelow(floor) + 1.2f;
                    Vector2 spot;
                    if (p == 0) spot = floor;
                    else if (p == 1) spot = floor + Vector2.up * 3.5f;
                    else if (p == 2) spot = new Vector2(floor.x, Mathf.Min(kit.CeilingAbove(floor, 40f), kit.Arena.yMax) - 1.6f);
                    else
                    {
                        float side = kit.Me.x - kit.Arena.xMin < kit.Arena.xMax - kit.Me.x ? -1f : 1f;
                        float wallX = kit.WallTowards(new Vector2(kit.Arena.center.x, floor.y + 6f), side, kit.Arena.width);
                        spot = new Vector2(wallX - side * 0.45f, Mathf.Min(floor.y + 6f, kit.CeilingAbove(floor, 40f) - 1.6f));
                    }
                    hero.transform.position = new Vector3(spot.x, spot.y, hero.transform.position.z);
                    if (hbody != null) hbody.linearVelocity = Vector2.zero;
                    Physics2D.SyncTransforms();
                    mod.Stats.FullHeal();
                    float lastHurt = Player.TerraHurt.LastHurtAt;
                    int hits = 0;
                    var tally = new Dictionary<string, StateTally>();
                    var sources = new Dictionary<string, int>();
                    string state = null;
                    float closestNow = float.MaxValue;
                    void Close()
                    {
                        if (state == null) return;
                        if (!tally.TryGetValue(state, out var t)) tally[state] = t = new StateTally();
                        t.Count++;
                        if (closestNow < float.MaxValue)
                        {
                            t.Closest = Mathf.Min(t.Closest, closestNow);
                            t.SumClosest += closestNow;
                        }
                        else t.SumClosest += 99f;
                    }
                    for (float t0 = Time.time; Time.time - t0 < seconds;)
                    {
                        var other = KitOf(record);
                        if (other != null && other != kit && bossWaits.IsMatch(kit.StateNow ?? "")) kit = other;
                        if (kit == null || kit.Hm == null || kit.Hm.isDead) break;
                        if (p > 0)
                        {
                            hero.transform.position = new Vector3(spot.x, spot.y, hero.transform.position.z);
                            if (hbody != null) hbody.linearVelocity = Vector2.zero;
                        }
                        string now = kit.StateNow ?? "?";
                        if (now != state)
                        {
                            Close();
                            state = now;
                            closestNow = float.MaxValue;
                        }
                        // Its own harmful colliders under its root, on now.
                        if (hcol != null)
                            foreach (var c in kit.transform.root.GetComponentsInChildren<Collider2D>(false))
                            {
                                if (c == null || !c.enabled || c.GetComponent<DamageHero>() == null) continue;
                                float d = c.Distance(hcol).distance;
                                if (d < closestNow) closestNow = d;
                            }
                        if (Player.TerraHurt.LastHurtAt != lastHurt)
                        {
                            lastHurt = Player.TerraHurt.LastHurtAt;
                            hits++;
                            if (!tally.TryGetValue(now, out var t)) tally[now] = t = new StateTally();
                            t.Hits++;
                            string src = Player.PlayerHooks.DevLastHit;
                            int cut = src.IndexOf(" 类型", StringComparison.Ordinal);
                            src = (cut > 0 ? src.Substring(0, cut) : src).Replace("(Clone)", "");
                            sources[src] = sources.TryGetValue(src, out int c2) ? c2 + 1 : 1;
                        }
                        if (mod.Stats.Life < mod.Stats.LifeMax / 2) mod.Stats.FullHeal();
                        yield return null;
                    }
                    Close();
                    Plugin.Log.LogInfo($"[G1_REACH] {key} {names[p]} 位置 ({spot.x:F1},{spot.y:F1}) 中 {hits}（{string.Join("、", sources.OrderByDescending(kv => kv.Value).Select(kv => kv.Key + "×" + kv.Value))}）");
                    foreach (var kv in tally.OrderByDescending(kv => kv.Value.Count))
                        Plugin.Log.LogInfo($"[G1_REACH]   {kv.Key} ×{kv.Value.Count} 中 {kv.Value.Hits} 最近 {(kv.Value.Closest < float.MaxValue ? kv.Value.Closest.ToString("F2") : "-")} 平均 {kv.Value.SumClosest / Mathf.Max(1, kv.Value.Count):F2}");
                }
                PlayerData.instance.isInvincible = true;
                yield return new WaitForSeconds(0.5f);
            }
            Plugin.Log.LogInfo("[G1_REACH] 完成");
        }
    }
}
