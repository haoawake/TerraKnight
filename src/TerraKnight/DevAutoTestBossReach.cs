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
    /// Dev (suite "bossreach"): the user's "虽然能到我身边但实际上我不动它打不到我" and "我在空中的时候，他们瞄准我但总是俯冲，
    /// 根本打不到我" (10-25). Each boss with the new AI, at two thirds of its life, against a player who does nothing in three
    /// places for TK_REACH_SECONDS each (20): standing on the floor where he was put, held 3.5 up (a jump's top) and held
    /// under the arena's top (on wings). Each place: the hits taken (their sources), and the boss's passes within 0.8 of him
    /// while he could be hurt that hurt nothing (reached, missed), with the states it was in then. TK_REACH_BOSSES picks some.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator BossReachTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            mod.ApplyCreative(false);
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            World.EnemyAiMode.Current = World.EnemyAiKind.New;
            string pick = Environment.GetEnvironmentVariable("TK_REACH_BOSSES");
            var keys = string.IsNullOrWhiteSpace(pick) ? AllBossKits : pick.Split(';');
            float seconds = float.TryParse(Environment.GetEnvironmentVariable("TK_REACH_SECONDS"), out var s) ? s : 20f;
            var places = new[] { "地面", "低空", "高空", "贴墙" };
            int none = 0, measured = 0;
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
                if (kit == null) { Plugin.Log.LogInfo($"[BOSS_REACH] {key} 没有首领"); continue; }
                var hero = HeroController.instance;
                var hbody = hero.GetComponent<Rigidbody2D>();
                var hcol = hero.GetComponent<Collider2D>();
                // Woken (a first blow), then at two thirds of its life.
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
                // Its fight under way a moment before the counting starts (an opening roar, a first burrow).
                yield return new WaitForSeconds(3f);
                var line = new List<string>();
                int zeroPlaces = 0;
                for (int p = 0; p < places.Length && kit != null && kit.Hm != null && !kit.Hm.isDead; p++)
                {
                    // Where he is put: beside the boss on its floor; 3.5 over that; under the arena's top over that; against the
                    // arena's nearer wall six over its floor (clinging, the user's "挂墙比8格高").
                    var floor = kit.ClampToArena(kit.Me, 3f);
                    floor.x += floor.x - 4f > kit.Arena.xMin + 1f ? -4f : 4f;
                    // The arena's floor looked for from its middle height: from a boss waiting under the floor (the Dung
                    // Defenders burrowed), its own height put him in the rock under the arena.
                    floor.y = kit.GroundBelow(new Vector2(floor.x, kit.Arena.center.y)) + 1.2f;
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
                    float lastHurt = Player.TerraHurt.LastHurtAt, nearSince = -1f;
                    int hits = 0, misses = 0;
                    var sources = new Dictionary<string, int>();
                    var missStates = new Dictionary<string, int>();
                    var states = new HashSet<string>();
                    string nearState = null;
                    for (float t0 = Time.time; Time.time - t0 < seconds;)
                    {
                        var other = KitOf(record);
                        if (other != null && other != kit && bossWaits.IsMatch(kit.StateNow ?? "")) kit = other;
                        if (kit == null || kit.Hm == null || kit.Hm.isDead) break;
                        if (!string.IsNullOrEmpty(kit.StateNow)) states.Add(kit.StateNow);
                        if (p > 0)
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
                            nearSince = -1f;
                        }
                        // The boss's body within 0.8 while he can be hurt: a pass; one that ends with no hit is a miss.
                        var body = kit.Hm.GetComponent<Collider2D>();
                        bool near = body != null && body.enabled && hcol != null && !hero.cState.invulnerable && body.Distance(hcol).distance < 0.8f;
                        if (near && nearSince < 0f) { nearSince = Time.time; nearState = kit.StateNow; }
                        else if (!near && nearSince >= 0f)
                        {
                            if (Player.TerraHurt.LastHurtAt < nearSince)
                            {
                                misses++;
                                string k = nearState ?? "?";
                                missStates[k] = missStates.TryGetValue(k, out int m) ? m + 1 : 1;
                            }
                            nearSince = -1f;
                        }
                        if (mod.Stats.Life < mod.Stats.LifeMax / 2) mod.Stats.FullHeal();
                        yield return null;
                    }
                    if (hits == 0) zeroPlaces++;
                    line.Add($"{places[p]} 中 {hits}（{string.Join("、", sources.OrderByDescending(kv => kv.Value).Select(kv => kv.Key + "×" + kv.Value))}）擦身未中 {misses}" +
                        (missStates.Count > 0 ? "［" + string.Join("、", missStates.OrderByDescending(kv => kv.Value).Take(4).Select(kv => kv.Key + "×" + kv.Value)) + "］" : "") +
                        $" 状态 {states.Count}");
                }
                measured++;
                if (zeroPlaces > 0) none++;
                Plugin.Log.LogInfo($"[BOSS_REACH] {key}：{string.Join("；", line)}");
                PlayerData.instance.isInvincible = true;
                yield return new WaitForSeconds(0.5f);
            }
            Plugin.Log.LogInfo($"[BOSS_REACH] 合计 {measured} 个首领，有打不中的位置的 {none} 个");
        }
    }
}
