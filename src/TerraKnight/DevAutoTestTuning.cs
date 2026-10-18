using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.UI;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "tuning"): pylons at benches the way the game seats the player (the respawn
    /// point is set a moment after sitting down); enemy toughness, bosses and elites (ranks, no
    /// stagger from a hail of hits, no knockback, harder hits); respawning (corpses fade, nothing
    /// set down in a wall, every slot back or why not); the Radar's count.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator TuningTest(string scene, HeroController hero)
        {
            mod.DevNoFallDamage = true;
            var inv = mod.Inventory;
            var pd = PlayerData.instance;
            pd.isInvincible = true;

            // --- Toughness.
            HealthManager weakest = null;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                if (!hm.isDead && UI.EnemyHover.RecordOf(hm) == "Crawler") weakest = hm;
            Plugin.Log.LogInfo($"[AutoTest] 血量：每点原版血 {Areas.HealthScale:F2} 泰拉血；小爬虫 {(weakest != null ? EnemyStats.LifeMax(EnemyStats.Of(weakest)).ToString() : "没找到")}（应为 150）；" +
                               $"首领伤害 ×{Areas.BossDamage}，精英 ×{Areas.EliteDamage}");

            // --- Pylons: sit first, the bench names itself the respawn point a moment later.
            foreach (var (benchScene, gate) in new[] { ("Town", "left1"), ("Fungus1_01b", "left1") })
            {
                yield return GoTo(benchScene, gate);
                hero = HeroController.instance;
                PlayMakerFSM bench = null;
                foreach (var fsm in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                    if (fsm.FsmName == "Bench Control" && fsm.gameObject.scene.name == benchScene) bench = fsm;
                if (bench == null)
                {
                    Plugin.Log.LogInfo($"[AutoTest] 晶塔：{benchScene} 没找到长椅");
                    continue;
                }
                int before = Pylons.Unlocked.Count;
                // The respawn point still another marker of this same scene (where a death left the player).
                foreach (var go in GameObject.FindGameObjectsWithTag("RespawnPoint"))
                    if (go.scene.name == benchScene && go != bench.gameObject && Vector2.Distance(go.transform.position, bench.transform.position) > 8f)
                    {
                        pd.SetBenchRespawn(go.name, benchScene, 0, true);
                        Plugin.Log.LogInfo($"[AutoTest] 晶塔：坐下前复活点是同场景的 {go.name}");
                        break;
                    }
                hero.transform.position = Clear(hero, (Vector2)bench.transform.position + Vector2.left * 2f);
                yield return new WaitForSeconds(0.5f);
                pd.atBench = true;
                yield return new WaitForSeconds(0.6f);
                pd.SetBenchRespawn(bench.gameObject.name, benchScene, 1, true);
                yield return new WaitForSeconds(0.6f);
                pd.atBench = false;
                yield return new WaitForSeconds(0.5f);
                var got = Pylons.Find(benchScene, bench.gameObject.name);
                Plugin.Log.LogInfo($"[AutoTest] 晶塔：{benchScene} 先坐下 0.6 秒后才设复活点 → 解锁 {Pylons.Unlocked.Count - before} 个，在长椅 {bench.gameObject.name} 旁={got != null}{(got != null ? $"（{got.Name}，场景坐标 {got.Local}）" : "")}；全部：{string.Join(" ", Pylons.Unlocked.ConvertAll(q => q.Scene + "/" + q.Bench))}");
                Shot(benchScene, "t_pylon_" + benchScene);
                yield return null;
                yield return null;
            }

            // --- Elites and bosses: rank, harder hits, no knockback, no stagger.
            foreach (var (s, gate) in new[] { ("Crossroads_21", "left1"), ("Crossroads_04", "left1"), ("Fungus1_20_v02", "left1"), ("Crossroads_09", "left1") })
            {
                yield return GoTo(s, gate);
                hero = HeroController.instance;
                yield return new WaitForSeconds(0.5f);
                var ranks = new System.Text.StringBuilder();
                HealthManager elite = null, stunned = null, plain = null;
                foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                {
                    if (hm.gameObject.scene.name != s || hm.isDead) continue;
                    var info = EnemyStats.Of(hm);
                    string rec = UI.EnemyHover.RecordOf(hm);
                    string rank = info.Boss ? "首领" : info.Elite ? "精英" : "普通";
                    ranks.Append($"{hm.gameObject.name}({rec},{rank},原版血 {info.MaxHp},泰拉 {EnemyStats.LifeMax(info)},伤害×{EnemyStats.DamageFactor(info)}) ");
                    if (!hm.gameObject.activeInHierarchy) continue;
                    if (info.Elite && elite == null) elite = hm;
                    if (!EnemyStats.Unstoppable(info) && plain == null && hm.GetComponent<Rigidbody2D>() != null) plain = hm;
                    if (EnemyStats.Unstoppable(info) && stunned == null)
                        foreach (var f in hm.GetComponents<PlayMakerFSM>())
                            if (f.FsmName == "Stun Control" || f.FsmName == "Stun") stunned = hm;
                }
                Plugin.Log.LogInfo($"[AutoTest] {s} 的敌人：{ranks}");
                // Knockback: an elite stays put where a common enemy is pushed.
                foreach (var hm in new[] { elite, plain })
                {
                    if (hm == null) continue;
                    var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
                    Vector2 p0 = hm.transform.position;
                    mod.Melee.HitWith(col, 5, 12f, 0f, 1f, (Vector2)hm.transform.position + Vector2.left * 2f);
                    yield return new WaitForSeconds(0.35f);
                    Plugin.Log.LogInfo($"[AutoTest] 击退 12 打 {hm.gameObject.name}（{(EnemyStats.Unstoppable(EnemyStats.Of(hm)) ? "精英/首领" : "普通")}）：移动 {Vector2.Distance(p0, hm.transform.position):F2}");
                }
                // A boss's or elite's hits: what one costs the player next to a common enemy's.
                if (elite != null || stunned != null)
                {
                    var strong = elite ?? stunned;
                    pd.isInvincible = false;
                    mod.Stats.FullHeal();
                    int life0 = mod.Stats.Life;
                    hero.TakeDamage(strong.gameObject, GlobalEnums.CollisionSide.left, 1, 1);
                    yield return null;
                    int strongHit = life0 - mod.Stats.Life;
                    yield return new WaitForSeconds(2.2f);
                    mod.Stats.FullHeal();
                    life0 = mod.Stats.Life;
                    hero.TakeDamage(plain != null ? plain.gameObject : hero.gameObject, GlobalEnums.CollisionSide.left, 1, 1);
                    yield return null;
                    int plainHit = life0 - mod.Stats.Life;
                    pd.isInvincible = true;
                    mod.Stats.FullHeal();
                    Plugin.Log.LogInfo($"[AutoTest] 挨一下（1 面具，区域每面具 {Areas.DamagePerMask(Areas.LevelOf(s))}，防御 {mod.Stats.Defense}）：{strong.gameObject.name} 打掉 {strongHit}，普通敌人 {plainHit}");
                    yield return new WaitForSeconds(2.2f);
                }
                if (stunned == null) continue;
                yield return StunCheck(stunned, hero);
                break;
            }

            // --- Hornet in Greenpath, whose stagger the player sees: walk into her arena, wait for the fight.
            yield return GoTo("Fungus1_04", "right1");
            hero = HeroController.instance;
            HealthManager hornet = null;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (hm.gameObject.name.StartsWith("Hornet Boss 1")) hornet = hm;
            if (hornet == null) Plugin.Log.LogInfo("[AutoTest] 大黄蜂：没找到");
            else
            {
                hero.transform.position = Clear(hero, (Vector2)hornet.transform.position + Vector2.right * 4f);
                float until = Time.time + 20f;
                while (Time.time < until && (!hornet.gameObject.activeInHierarchy || hornet.IsInvincible)) yield return null;
                yield return new WaitForSeconds(1f);
                var info = EnemyStats.Of(hornet);
                Plugin.Log.LogInfo($"[AutoTest] 大黄蜂：激活={hornet.gameObject.activeInHierarchy} 无敌={hornet.IsInvincible}，{(info.Boss ? "首领" : info.Elite ? "精英" : "普通")}，泰拉血 {EnemyStats.LifeMax(info)}，伤害×{EnemyStats.DamageFactor(info)}");
                if (hornet.gameObject.activeInHierarchy) yield return StunCheck(hornet, hero);
            }

            // --- Respawning in Greenpath: kill every enemy the slots set down, then wait.
            foreach (var (s, gate) in new[] { ("Fungus1_31", "right1"), ("Fungus1_03", "left1") })
            {
                yield return GoTo(s, gate);
                hero = HeroController.instance;
                yield return new WaitForSeconds(1f);
                var resp = EnemyRespawn.Instance;
                string start = resp.DevReport(s, out int alive0, out _, out int wall0);
                var live = resp.DevLive(s);
                int sched0 = CorpseCleanup.DevScheduled, gone0 = CorpseCleanup.DevRemoved, back0 = EnemyRespawn.DevRespawned;
                EnemyRespawn.DevFixedSeconds = 5f;
                foreach (var hm in live)
                {
                    var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
                    if (col != null) mod.Melee.HitWith(col, 999999, 0f, 0f, 1f, hm.transform.position);
                }
                yield return new WaitForSeconds(0.5f);
                Shot(s, "t_respawn_dead_" + s);
                yield return null;
                yield return new WaitForSeconds(4.2f);
                int faded = CorpseCleanup.DevRemoved - gone0;
                yield return new WaitForSeconds(Mathf.Max(0f, 5f + 3f - 4.7f));
                string after = resp.DevReport(s, out int alive1, out int waiting1, out int wall1);
                Plugin.Log.LogInfo($"[AutoTest] 刷新 {s}：开始 {start}（活着 {alive0}，在墙里 {wall0}）；打死 {live.Count} 只，尸体待清 {CorpseCleanup.DevScheduled - sched0} 具、4.7 秒内清掉 {faded} 具；" +
                                   $"{5f + 3f:F0} 秒后：{after}（活着 {alive1}，没回来 {waiting1}，在墙里 {wall1}），这期间重生 {EnemyRespawn.DevRespawned - back0} 次");
                Shot(s, "t_respawn_back_" + s);
                EnemyRespawn.DevFixedSeconds = -1f;
                yield return null;
                yield return null;
            }

            // --- The Radar.
            int near0 = InfoAccessories.DevEnemiesNear;
            inv.Pickup(ItemType.Radar, 1);
            yield return new WaitForSeconds(0.6f);
            hero = HeroController.instance;
            Plugin.Log.LogInfo($"[AutoTest] 雷达：背包里有={InfoAccessories.HasRadar(inv)}，显示附近 {InfoAccessories.DevEnemiesNear} 个敌人（直接数 2000 px 内 {InfoAccessories.Count(hero.transform.position)} 个）；物品 {ItemDb.Get(ItemType.Radar).Name}「{ItemDb.Get(ItemType.Radar).Tooltip}」阶段 {ItemDb.Get(ItemType.Radar).Stage}，" +
                               $"小爬虫掉落表 {EnemyDrops.Of("Crawler").Count} 项");
            Shot(GameManager.instance.sceneName, "t_radar");
            yield return null;
            yield return null;

            pd.isInvincible = false;
            mod.DevNoFallDamage = false;
        }

        /// <summary>
        /// No stagger: the boss's Stun Control FSM sees none of the mod's hits (25 quick ones);
        /// then, as the check, the same hits through the game's own path, which do stun it.
        /// </summary>
        IEnumerator StunCheck(HealthManager stunned, HeroController hero)
        {
            PlayMakerFSM stunFsm = null;
            foreach (var f in stunned.GetComponents<PlayMakerFSM>())
                if (f.FsmName == "Stun Control" || f.FsmName == "Stun") stunFsm = f;
            if (stunFsm == null)
            {
                Plugin.Log.LogInfo($"[AutoTest] 霸体 {stunned.gameObject.name}：没有 Stun Control");
                yield break;
            }
            var col = stunned.GetComponent<Collider2D>() ?? stunned.GetComponentInChildren<Collider2D>();
            var seen = new HashSet<string>();
            int hp0 = stunned.hp;
            for (int k = 0; k < 25 && !stunned.isDead; k++)
            {
                mod.Melee.HitWith(col, 3, 0f, 0f, 1f, stunned.transform.position);
                seen.Add(stunFsm.ActiveStateName);
                yield return new WaitForSeconds(0.05f);
            }
            yield return new WaitForSeconds(0.3f);
            seen.Add(stunFsm.ActiveStateName);
            var vanilla = new HashSet<string>();
            for (int k = 0; k < 25 && !stunned.isDead; k++)
            {
                stunned.hp += 1;
                stunned.Hit(new HitInstance { Source = hero.gameObject, AttackType = AttackTypes.Generic, DamageDealt = 1, Direction = 0f, Multiplier = 1f, MagnitudeMultiplier = 0f });
                vanilla.Add(stunFsm.ActiveStateName);
                yield return new WaitForSeconds(0.05f);
            }
            yield return new WaitForSeconds(0.3f);
            vanilla.Add(stunFsm.ActiveStateName);
            Plugin.Log.LogInfo($"[AutoTest] 霸体 {stunned.gameObject.name}（{stunFsm.FsmName}，原版血 {hp0}）：mod 连打 25 下状态机经过 [{string.Join(",", seen)}]；对照：原版受击 25 下经过 [{string.Join(",", vanilla)}]");
        }
    }
}
