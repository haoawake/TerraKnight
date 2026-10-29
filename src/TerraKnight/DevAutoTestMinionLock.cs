using System.Collections;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "minionlock", the user's 10-27 "召唤物应该锁小怪才对，只是不去攻击首领，为什么你直接一刀切了？"): in the
    /// Forgotten Crossroads, the nearest small enemy held a little before the player, who never attacks: the Finch, Slime and
    /// Imp staffs' minions each summoned in turn go for it and strike it within 6 s (Minions.DevHits). The bosses' rule (a
    /// boss waits for the player's own blow) is suite bossguard's. Lines "[AutoTest] 召唤物锁敌 …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator MinionLockTest()
        {
            int ok = 0, all = 0;
            yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            yield return new WaitForSeconds(3f);
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            MinionEngage.DevAlways = false;
            string scene = GameManager.instance.sceneName;
            // The nearest living small enemy of the room (a new one for each staff: the last may have died).
            HealthManager Nearest()
            {
                HealthManager found = null;
                float best = float.MaxValue;
                foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                {
                    if (hm.isDead || !hm.gameObject.activeInHierarchy || hm.gameObject.scene.name != scene) continue;
                    var info = EnemyStats.Of(hm);
                    if (info == null || info.Boss || EnemyStats.LifeMax(info) <= 5) continue;
                    float d = Vector2.Distance(hm.transform.position, hero.transform.position);
                    if (d < best) { best = d; found = hm; }
                }
                return found;
            }
            var minions = Minions.Instance;
            try
            {
                foreach (var staff in new[] { ItemType.FinchStaff, ItemType.SlimeStaff, ItemType.ImpStaff })
                {
                    minions.Clear();
                    var target = Nearest();
                    if (target == null) { all++; Plugin.Log.LogInfo("[AutoTest] 召唤物锁敌 房间里没有小怪 不通过"); break; }
                    var trb = target.GetComponent<Rigidbody2D>();
                    string targetName = target.name + "（" + UI.EnemyHover.RecordOf(target) + "）";
                    hero.transform.position = Clear(hero, (Vector2)hero.transform.position);
                    hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
                    var at = (Vector2)hero.transform.position + new Vector2(4f, 0.5f);
                    yield return new WaitForSeconds(0.3f);
                    bool summoned = minions.Summon(ItemDb.Get(staff), (Vector2)hero.transform.position + Vector2.up);
                    int hits0 = Minions.DevHits;
                    float firstHit = -1f;
                    // The player does nothing: no swing, no shot, no target picked.
                    for (float t = 0f; t < 6f; t += Time.deltaTime)
                    {
                        if (target && !target.isDead)
                        {
                            target.hp = Mathf.Max(target.hp, 3000);
                            // Held there (a crawler has no body of its own).
                            if (trb != null) Pin(target, trb, at);
                            else target.transform.position = new Vector3(at.x, at.y, target.transform.position.z);
                        }
                        if (firstHit < 0f && Minions.DevHits > hits0) firstHit = t;
                        yield return null;
                    }
                    int hits = Minions.DevHits - hits0;
                    bool pass = summoned && hits > 0;
                    all++;
                    if (pass) ok++;
                    Plugin.Log.LogInfo($"[AutoTest] 召唤物锁敌 {ItemDb.Get(staff).Name}：玩家不出手，6 秒内召唤物打中 {targetName} {hits} 下{(firstHit >= 0f ? $"，第一下在 {firstHit:F1} 秒" : "")} {(pass ? "通过" : "不通过")}");
                    if (staff == ItemType.FinchStaff) Shot(scene, "minionlock_finch");
                }
            }
            finally
            {
                minions.Clear();
                Player.TerraStats.DevImmortal = false;
            }
            Plugin.Log.LogInfo($"[AutoTest] 召唤物锁敌 合计 {ok}/{all}");
        }
    }
}
