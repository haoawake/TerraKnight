using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "knock"): every kind of common enemy is knocked back, bosses and elites aren't.
    /// The player is set beside one of each kind (in King's Pass, then the first Crossroads room)
    /// and hits it with the Copper Shortsword and the Copper Broadsword; how far it moves, and
    /// whether the game's recoil or the mod's own push (crawlids, wall crawlers) did it.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator KnockTest(string scene, HeroController hero)
        {
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            // Which kinds are elites (by the roster, never by health now).
            var elites = new System.Text.StringBuilder();
            int eliteCount = 0;
            foreach (var r in EnemyDrops.DevRecords)
                if (!EnemyStats.IsBossKind(r, 0) && EnemyStats.EliteKind(r, 0)) { eliteCount++; elites.Append(Bestiary.NameOf(r)).Append('(').Append(EnemyDrops.HpOf(r)).Append(") "); }
            Plugin.Log.LogInfo($"[AutoTest] 精英 共 {eliteCount} 种：{elites}");
            // A crawlid's health raised far past 70 stays a common enemy that is knocked back.
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                if (hm.isDead || !hm.gameObject.activeInHierarchy || Bestiary.KeyOf(hm) != "Crawler") continue;
                int before = hm.hp;
                hm.hp = 500;
                var inf = EnemyStats.Of(hm);
                Plugin.Log.LogInfo($"[AutoTest] 精英 血量抬高：小爬虫血量 {before} -> 500 后 精英 {inf.Elite}、首领 {inf.Boss}（都应 False）");
                hm.hp = before;
                break;
            }
            yield return KnockRoom(scene, hero);
            yield return GoTo("Crossroads_01", "top1");
            hero = HeroController.instance;
            yield return new WaitForSeconds(2f);
            PlayerData.instance.isInvincible = true;
            yield return KnockRoom(GameManager.instance.sceneName, hero);
            mod.DevNoFallDamage = false;
        }

        IEnumerator KnockRoom(string scene, HeroController hero)
        {
            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            var seen = new HashSet<string>();
            var targets = new List<HealthManager>();
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                if (hm.isDead || !hm.gameObject.activeInHierarchy || hm.gameObject.scene.name != scene) continue;
                var inf = EnemyStats.Of(hm);
                if (inf == null || !seen.Add(inf.Name)) continue;
                targets.Add(hm);
            }
            Plugin.Log.LogInfo($"[AutoTest] 击退 {scene}：{targets.Count} 种敌人");
            foreach (var hm in targets)
            {
                if (hm == null || hm.isDead) continue;
                var inf = EnemyStats.Of(hm);
                var ecol = hm.GetComponent<Collider2D>();
                if (ecol == null) continue;
                var line = new System.Text.StringBuilder($"[AutoTest] 击退 {inf.Name}（{hm.name}）：Boss/精英 {EnemyStats.Unstoppable(inf)}，自身后坐 {TerraKnockback.DevRecoil(hm) ?? "无"}");
                foreach (var w in new[] { ItemType.CopperShortsword, ItemType.CopperBroadsword })
                {
                    if (hm == null || hm.isDead) break;
                    // The test hits are too light to kill: its health is left as it is.
                    // The player just left of it, feet level with its bottom, facing it.
                    var eb = ecol.bounds;
                    var at = new Vector2(eb.min.x - col.bounds.extents.x - 0.35f, eb.min.y + col.bounds.extents.y + 0.05f);
                    hero.transform.position += (Vector3)(at - (Vector2)col.bounds.center);
                    rb.linearVelocity = Vector2.zero;
                    yield return new WaitForSeconds(0.1f);
                    if (hm == null) break;
                    Vector2 start = hm.transform.position;
                    int own0 = TerraKnockback.DevOwnPushes, fly0 = TerraKnockback.DevLaunches;
                    // A hit of the weapon's knockback straight on it (no swing to miss a moving target).
                    int hp0 = hm.hp, calls0 = TerraKnockback.DevPushCalls;
                    bool took = mod.Melee.HitWith(ecol, 5, ItemDb.Get(w).Knockback, 0f, 1f, col.bounds.center);
                    line.Append($"［命中 {took} 推力调用 {TerraKnockback.DevPushCalls - calls0} 次 速度 {TerraKnockback.DevPushSpeed:F2} 血 {hp0}->{hm.hp} 击退值 {ItemDb.Get(w).Knockback} 算出 {TerraKnockback.Launch(ItemDb.Get(w).Knockback, World.Areas.KnockbackTaken(inf.Level), false):F2} 刚体 {(hm.GetComponent<Rigidbody2D>() != null ? hm.GetComponent<Rigidbody2D>().bodyType.ToString() : "无")}］");
                    float far = 0f, high = 0f;
                    for (float t = 0f; t < 1.2f && hm != null; t += Time.deltaTime)
                    {
                        far = Mathf.Max(far, ((Vector2)hm.transform.position - start).magnitude);
                        high = Mathf.Max(high, hm.transform.position.y - start.y);
                        yield return null;
                    }
                    line.Append($"；{ItemDb.Get(w).Name} 位移 {far:F2}、最高 {high:F2}（打飞 {TerraKnockback.DevLaunches - fly0} 次 速度 {TerraKnockback.DevLastLaunch:F2} px/帧：腾空 {KnockFlight.DevAir} 帧、最高 {KnockFlight.DevPeak:F2}、退后 {KnockFlight.DevBack:F2}；贴面推 {TerraKnockback.DevOwnPushes - own0} 次）");
                    yield return new WaitForSeconds(0.5f);
                }
                Plugin.Log.LogInfo(line.ToString());
            }
        }
    }
}
