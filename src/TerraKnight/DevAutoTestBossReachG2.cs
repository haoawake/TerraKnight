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
    /// Dev (suite "bossreachg2"): the "bossreach" measure for looking into one boss's misses. After its life is brought to
    /// two thirds, TK_REACH2_WARM seconds (6) pass uncounted (a rage or a phase's opening there); then the places of
    /// TK_REACH2_PLACES (';', 地面 低空 高空 贴墙6 贴墙9 贴墙12 by default 地面;低空;高空;贴墙6;贴墙9) for TK_REACH_SECONDS each. Each
    /// hit is logged with the boss's state; each pass within 0.8 that hurt nothing with its state and how near its own
    /// harmful colliders came (the nearest DamageHero collider of its own, enabled, at the closest moment).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator BossReachG2Test()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            mod.ApplyCreative(false);
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            World.EnemyAiMode.Current = World.EnemyAiKind.New;
            string pick = Environment.GetEnvironmentVariable("TK_REACH_BOSSES");
            var keys = string.IsNullOrWhiteSpace(pick) ? AllBossKits : pick.Split(';');
            float seconds = float.TryParse(Environment.GetEnvironmentVariable("TK_REACH_SECONDS"), out var s) ? s : 20f;
            float warm = float.TryParse(Environment.GetEnvironmentVariable("TK_REACH2_WARM"), out var w) ? w : 6f;
            string placeList = Environment.GetEnvironmentVariable("TK_REACH2_PLACES");
            var places = (string.IsNullOrWhiteSpace(placeList) ? "地面;低空;高空;贴墙6;贴墙9" : placeList).Split(';').Where(x => x.Length > 0).ToArray();
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
                if (kit == null) { Plugin.Log.LogInfo($"[BOSS_REACH2] {key} 没有首领"); continue; }
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
                if (kit.Info != null) kit.Hm.hp = Mathf.Min(kit.Hm.hp, Mathf.RoundToInt(kit.Info.MaxHp * 0.6f));
                // Uncounted: a rage or a phase's opening the life brought down starts.
                PlayerData.instance.isInvincible = true;
                for (float t0 = Time.time; Time.time - t0 < warm;) { yield return null; }
                PlayerData.instance.isInvincible = false;
                kit = KitOf(record) ?? kit;
                var line = new List<string>();
                foreach (var place in places)
                {
                    if (kit == null || kit.Hm == null || kit.Hm.isDead) break;
                    var floor = kit.ClampToArena(kit.Me, 3f);
                    floor.x += floor.x - 4f > kit.Arena.xMin + 1f ? -4f : 4f;
                    // The arena's floor there, looked for from over it (a burrower's body is under it: the ground under
                    // that is the rock below the arena).
                    // Its own floor (the ground under its feet, beside it), or for one in the ground, from the arena's middle.
                    float under = kit.GroundBelow(new Vector2(floor.x, kit.Me.y + 0.5f));
                    floor.y = (kit.Me.y - under < 5f && under < kit.Me.y ? under : kit.GroundBelow(new Vector2(floor.x, kit.Arena.center.y))) + 1.2f;
                    Vector2 spot;
                    if (place == "地面") spot = floor;
                    else if (place == "低空") spot = floor + Vector2.up * 3.5f;
                    else if (place == "高空") spot = new Vector2(floor.x, Mathf.Min(kit.CeilingAbove(floor, 40f), kit.Arena.yMax) - 1.6f);
                    else
                    {
                        float up = float.TryParse(place.Substring(2), out var u) ? u : 6f;
                        float side = kit.Me.x - kit.Arena.xMin < kit.Arena.xMax - kit.Me.x ? -1f : 1f;
                        float wallX = kit.WallTowards(new Vector2(kit.Arena.center.x, floor.y + up), side, kit.Arena.width);
                        spot = new Vector2(wallX - side * 0.45f, Mathf.Min(floor.y + up, kit.CeilingAbove(floor, 40f) - 1.6f));
                    }
                    hero.transform.position = new Vector3(spot.x, spot.y, hero.transform.position.z);
                    if (hbody != null) hbody.linearVelocity = Vector2.zero;
                    Physics2D.SyncTransforms();
                    mod.Stats.FullHeal();
                    Plugin.Log.LogInfo($"[BOSS_REACH2] {key} {place} 位置 ({spot.x:F1},{spot.y:F1}) 场地 x {kit.Arena.xMin:F1}~{kit.Arena.xMax:F1} y {kit.Arena.yMin:F1}~{kit.Arena.yMax:F1} 首领 ({kit.Me.x:F1},{kit.Me.y:F1})");
                    float lastHurt = Player.TerraHurt.LastHurtAt, nearSince = -1f, nearest = float.MaxValue;
                    int hits = 0, misses = 0;
                    var sources = new Dictionary<string, int>();
                    var missStates = new Dictionary<string, int>();
                    var order = new List<string>();
                    string nearState = null, nearestName = null;
                    for (float t0 = Time.time; Time.time - t0 < seconds;)
                    {
                        var other = KitOf(record);
                        if (other != null && other != kit && bossWaits.IsMatch(kit.StateNow ?? "")) kit = other;
                        if (kit == null || kit.Hm == null || kit.Hm.isDead) break;
                        string st = kit.StateNow ?? "";
                        if (order.Count == 0 || order[order.Count - 1] != st) order.Add(st);
                        if (place != "地面")
                        {
                            hero.transform.position = new Vector3(spot.x, spot.y, hero.transform.position.z);
                            if (hbody != null) hbody.linearVelocity = Vector2.zero;
                        }
                        if (Player.TerraHurt.LastHurtAt != lastHurt)
                        {
                            lastHurt = Player.TerraHurt.LastHurtAt;
                            hits++;
                            string src = Player.PlayerHooks.DevLastHit;
                            int cut = src.IndexOf(" 类型", StringComparison.Ordinal);
                            src = (cut > 0 ? src.Substring(0, cut) : src).Replace("(Clone)", "");
                            sources[src] = sources.TryGetValue(src, out int c) ? c + 1 : 1;
                            Plugin.Log.LogInfo($"[BOSS_REACH2] {key} {place} 中 {src} 状态 {st} 首领 ({kit.Me.x:F1},{kit.Me.y:F1}) 玩家 ({hero.transform.position.x:F1},{hero.transform.position.y:F1})");
                            nearSince = -1f;
                        }
                        var body = kit.Hm.GetComponent<Collider2D>();
                        bool near = body != null && body.enabled && hcol != null && !hero.cState.invulnerable && body.Distance(hcol).distance < 0.8f;
                        if (near)
                        {
                            if (nearSince < 0f) { nearSince = Time.time; nearState = st; nearest = float.MaxValue; nearestName = null; }
                            foreach (var dh in kit.Hm.GetComponentsInChildren<DamageHero>(false))
                            {
                                if (dh == null || dh.damageDealt <= 0) continue;
                                foreach (var c2 in dh.GetComponents<Collider2D>())
                                {
                                    if (c2 == null || !c2.enabled) continue;
                                    float d = c2.Distance(hcol).distance;
                                    if (d < nearest) { nearest = d; nearestName = dh.name; }
                                }
                            }
                        }
                        else if (nearSince >= 0f)
                        {
                            if (Player.TerraHurt.LastHurtAt < nearSince)
                            {
                                misses++;
                                string k = nearState ?? "?";
                                missStates[k] = missStates.TryGetValue(k, out int m) ? m + 1 : 1;
                                Plugin.Log.LogInfo($"[BOSS_REACH2] {key} {place} 擦身未中 状态 {nearState}，自身伤害碰撞最近 {(nearestName != null ? $"{nearestName} {nearest:F2}" : "无")}，首领 ({kit.Me.x:F1},{kit.Me.y:F1}) 玩家 ({hero.transform.position.x:F1},{hero.transform.position.y:F1})");
                            }
                            nearSince = -1f;
                        }
                        if (mod.Stats.Life < mod.Stats.LifeMax / 2) mod.Stats.FullHeal();
                        yield return null;
                    }
                    line.Add($"{place} 中 {hits}（{string.Join("、", sources.OrderByDescending(kv => kv.Value).Select(kv => kv.Key + "×" + kv.Value))}）擦身未中 {misses}" +
                        (missStates.Count > 0 ? "［" + string.Join("、", missStates.OrderByDescending(kv => kv.Value).Take(4).Select(kv => kv.Key + "×" + kv.Value)) + "］" : ""));
                    Plugin.Log.LogInfo($"[BOSS_REACH2] {key} {place} 状态顺序：{string.Join(">", order.Take(60))}");
                }
                Plugin.Log.LogInfo($"[BOSS_REACH2] {key}：{string.Join("；", line)}");
                PlayerData.instance.isInvincible = true;
                yield return new WaitForSeconds(0.5f);
            }
            Plugin.Log.LogInfo("[BOSS_REACH2] 完成");
        }
    }
}
