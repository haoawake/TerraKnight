using System.Collections;
using System.Collections.Generic;
using System.IO;
using TerraKnight.Items;
using UnityEngine;
using UnityEngine.SceneManagement;
using USM = UnityEngine.SceneManagement.SceneManager;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suites "bookscan", "shotgun"). bookscan: every playable scene loaded on its own (additively) and each of its
    /// enemies (HealthManager, waiting ones too) looked up as the monster book does (EnemyHover.RecordOf, Bestiary.KeyOf):
    /// those it can't name or keeps out, by name, written to bookscan.txt. shotgun: a Shotgun and a Quad-Barrel Shotgun fired
    /// point-blank at a sturdy ordinary enemy: how many pellets flew, how many hit, how much life it lost.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly string[] scanPrefixes = { "Tutorial", "Town", "Crossroads", "Fungus", "Cliffs", "Ruins", "Waterways", "Mines", "RestingGrounds", "Deepnest",
            "Abyss", "Hive", "Room_", "Grimm_", "Dream_", "GG_", "White_Palace", "Mines" };

        IEnumerator BookScanTest()
        {
            var lines = new List<string>();
            var missing = new Dictionary<string, List<string>>();
            int scenes = 0, enemies = 0;
            for (int i = 0; i < USM.sceneCountInBuildSettings; i++)
            {
                string name = Path.GetFileNameWithoutExtension(UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i));
                bool play = false;
                foreach (var p in scanPrefixes) if (name.StartsWith(p)) { play = true; break; }
                if (!play || name == GameManager.instance.sceneName) continue;
                var scene = USM.GetSceneByName(name);
                bool loadedHere = !scene.isLoaded;
                if (loadedHere)
                {
                    var op = USM.LoadSceneAsync(name, LoadSceneMode.Additive);
                    float until = Time.realtimeSinceStartup + 20f;
                    while (op != null && !op.isDone && Time.realtimeSinceStartup < until) yield return null;
                    scene = USM.GetSceneByName(name);
                }
                if (!scene.isLoaded) { lines.Add($"{name}: 加载失败"); continue; }
                scenes++;
                PlayerData.instance.isInvincible = true;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var hm in root.GetComponentsInChildren<HealthManager>(true))
                    {
                        enemies++;
                        string rec = UI.EnemyHover.RecordOf(hm);
                        string key = Bestiary.KeyOf(hm);
                        string why = rec == null ? "认不出记录" : !EnemyDrops.Known(rec) ? $"记录 {rec} 不在表里" : key == null ? $"记录 {rec}（{EnemyDrops.KindOf(rec)}）不入图鉴" : null;
                        var death = hm.GetComponent<EnemyDeathEffects>();
                        string obj = System.Text.RegularExpressions.Regex.Replace(hm.gameObject.name, @"\s*\(\d+\)$", "");
                        lines.Add($"{name}\t{obj}\thp {hm.hp}\t记录 {rec ?? "-"}\t图鉴 {key ?? "-"}\t死亡效果 {(death != null ? "有" : "无")}\t击退 {TerraKnockback.DevWhy(hm)}{(why != null ? "\t!! " + why : "")}");
                        if (why == null) continue;
                        string k = obj + " → " + why;
                        if (!missing.TryGetValue(k, out var where)) missing[k] = where = new List<string>();
                        if (!where.Contains(name)) where.Add(name);
                    }
                if (loadedHere)
                {
                    var un = USM.UnloadSceneAsync(scene);
                    float until = Time.realtimeSinceStartup + 20f;
                    while (un != null && !un.isDone && Time.realtimeSinceStartup < until) yield return null;
                }
            }
            File.WriteAllLines(Path.Combine(DevCopy.SaveDir, "bookscan.txt"), lines);
            foreach (var kv in missing) Plugin.Log.LogInfo($"[AutoTest] 图鉴普查 {kv.Key}：{string.Join("、", kv.Value)}");
            Plugin.Log.LogInfo($"[AutoTest] 图鉴普查 {scenes} 个场景、{enemies} 个敌人对象，有问题的 {missing.Count} 种");
        }

        IEnumerator ShotgunTest()
        {
            yield return GoTo("Crossroads_01", "right1");
            PlayerData.instance.isInvincible = true;
            yield return new WaitForSeconds(2f);
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            inv.Add(ItemType.MusketBall, 500);
            HealthManager target = null;
            float best = float.MaxValue;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                if (hm.isDead || !hm.gameObject.activeInHierarchy || EnemyStats.Untouchable(hm)) continue;
                var info0 = EnemyStats.Of(hm);
                if (info0.Boss || info0.Elite) continue;
                float d = Vector2.Distance(hm.transform.position, hero.transform.position);
                if (d < best) { best = d; target = hm; }
            }
            if (target == null) { Plugin.Log.LogInfo("[AutoTest] 霰弹：没有敌人 不通过"); yield break; }
            var info = EnemyStats.Of(target);
            var trb = target.GetComponent<Rigidbody2D>();
            var col = hero.GetComponent<BoxCollider2D>();
            foreach (var gun in new[] { ItemType.Shotgun, ItemType.QuadBarrelShotgun, ItemType.Boomstick })
            {
                target.hp = 100000;
                info.MaxHp = target.hp;
                info.Carry = 0f;
                Vector2 hold = (Vector2)col.bounds.center + Vector2.right * 2.5f;
                int hits0 = TerraProjectiles.DevHit(ProjKind.Bullet), made0 = TerraProjectiles.DevMade(ProjKind.Bullet), hp0 = target.hp;
                int life0 = EnemyStats.Life(target, info);
                for (float t = 0f; t < 0.7f; t += Time.deltaTime)
                {
                    target.transform.position = new Vector3(hold.x, hold.y, target.transform.position.z);
                    if (trb != null) trb.linearVelocity = Vector2.zero;
                    if (t == 0f) mod.DevUse(gun, hold);
                    yield return null;
                }
                int made = TerraProjectiles.DevMade(ProjKind.Bullet) - made0, hits = TerraProjectiles.DevHit(ProjKind.Bullet) - hits0, lost = life0 - EnemyStats.Life(target, info);
                int per = Equipment.Damage(ItemDb.Get(gun), ItemDb.Get(ItemType.MusketBall));
                bool pass = made >= 4 && hits == made && lost >= per * made / 2;
                Plugin.Log.LogInfo($"[AutoTest] 霰弹 {gun}：射出 {made} 颗，命中 {hits} 颗，{target.name} 少了 {lost} 点生命（一颗约 {per}，减去防御后），HK 血 {hp0} → {target.hp} {(pass ? "通过" : "不通过")}");
                yield return new WaitForSeconds(0.5f);
            }
        }
    }
}
