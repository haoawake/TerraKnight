using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "killscan"): rooms of every area in turn, each of their enemies struck down by the mod's own hits
    /// (HitWith, as any weapon's): each one's kill in the book (Bestiary.Credited, its record's kills) and its drops
    /// rolled, or why not (no record, kept out of its fight, still alive).
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static readonly string[] killRooms =
        {
            "Tutorial_01", "Crossroads_01", "Crossroads_07", "Crossroads_19", "Fungus1_01", "Fungus1_22", "Fungus2_01", "Fungus2_05", "Fungus3_02", "Fungus3_10",
            "Cliffs_01", "Ruins1_03", "Ruins2_01", "Waterways_01", "Waterways_04", "Mines_02", "Mines_23", "RestingGrounds_06", "Deepnest_01", "Deepnest_43",
            "Deepnest_East_01", "Abyss_04", "Abyss_09", "Hive_01", "Fungus3_34", "White_Palace_02",
        };

        IEnumerator KillScanTest()
        {
            int credited = 0, total = 0, dropsBefore = EnemyDrops.DevDropped + EnemyDrops.DevGiven;
            var bad = new List<string>();
            var perRecord = new Dictionary<string, int>();
            mod.DevNoFallDamage = true;
            foreach (var scene in killRooms)
            {
                yield return GoTo(scene, EntryGateOf(scene));
                PlayerData.instance.isInvincible = true;
                yield return new WaitForSeconds(2.5f);
                var hero = HeroController.instance;
                var targets = new List<HealthManager>();
                foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                {
                    if (hm == null || hm.isDead || !hm.gameObject.activeInHierarchy || hm.gameObject.scene.name != GameManager.instance.sceneName) continue;
                    string key = Bestiary.KeyOf(hm);
                    if (key == null) continue;
                    targets.Add(hm);
                }
                int here = 0, hereOk = 0;
                foreach (var hm in targets)
                {
                    if (hm == null || hm.isDead || Bestiary.Credited(hm)) continue;
                    string key = Bestiary.KeyOf(hm);
                    string name = hm.name;
                    int kills0 = Bestiary.KillsOf(key);
                    var col = hm.GetComponent<Collider2D>() ?? hm.GetComponentInChildren<Collider2D>();
                    if (col == null) continue;
                    here++;
                    total++;
                    // Up close (a fight's gates shut it in), then struck until it goes down.
                    hero.transform.position = new Vector3(col.bounds.center.x - 1.5f, col.bounds.center.y + 0.5f, hero.transform.position.z);
                    yield return new WaitForSeconds(0.15f);
                    for (int k = 0; k < 40 && hm != null && !hm.isDead && hm.hp > 0 && !Bestiary.Credited(hm); k++)
                    {
                        if (col != null && col.enabled && col.gameObject.activeInHierarchy) mod.Melee.HitWith(col, 999999, 0f, 0f, 1f, col.bounds.center);
                        yield return new WaitForSeconds(0.1f);
                    }
                    yield return new WaitForSeconds(0.2f);
                    bool ok = hm == null || Bestiary.Credited(hm) || Bestiary.KillsOf(key) > kills0;
                    if (ok) { credited++; hereOk++; perRecord[key] = perRecord.TryGetValue(key, out int n) ? n + 1 : 1; }
                    else bad.Add($"{scene}/{name}（{key}，生命 {(hm != null ? hm.hp : 0)}，死 {(hm != null && hm.isDead)}，场外 {World.ArenaGuard.Outside(hm)}）");
                }
                Plugin.Log.LogInfo($"[AutoTest] 击杀普查 {scene}：{hereOk}/{here} 记入图鉴");
            }
            int drops = EnemyDrops.DevDropped + EnemyDrops.DevGiven - dropsBefore;
            foreach (var b in bad) Plugin.Log.LogInfo($"[AutoTest] 击杀普查 没记上：{b}");
            mod.DevNoFallDamage = false;
            Plugin.Log.LogInfo($"[AutoTest] 击杀普查 合计 {credited}/{total} 记入图鉴，{perRecord.Count} 种，掉落 {drops} 件 {(credited == total ? "通过" : "不通过")}");
        }
    }
}
