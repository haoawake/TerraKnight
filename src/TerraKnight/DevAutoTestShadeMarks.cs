using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "shademark"): the rift rifle's marks on enemies left free to move (the user's, 10-20: "有概率击中后叠不到层数").
    /// Shot after shot at the nearest enemy in Crossroads_01, then at Dirtmouth's dummy: what the ray struck (its hits, the
    /// marks' calls) against the stacks that went up on the enemies, each shot whose numbers don't meet logged; the marks
    /// burst every four shots so none sits at five.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator ShadeMarksTest(string scene, HeroController hero)
        {
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            var inv = mod.Inventory;
            int bad = 0, shots = 0;
            try
            {
                const int slot = Inventory.HotbarSize - 1;
                void Arm()
                {
                    mod.SetInfiniteItems(false);
                    inv.Slots[slot] = ItemStack.Of(ItemType.ShadeRiftRifle);
                    inv.Selected = slot;
                    inv.Slots[Inventory.Size - 1] = ItemStack.Of(ItemType.MusketBall, 999);
                }
                var tp = TerraProjectiles.Instance;

                IEnumerator Round(string room, int count, System.Func<HealthManager> pick)
                {
                    for (int s = 0; s < count; s++)
                    {
                        var target = pick();
                        if (target == null) { yield return new WaitForSeconds(0.3f); continue; }
                        var all = new List<HealthManager>();
                        // Kept alive (free to move): a dead one is no use to stack on.
                        foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                            if (!hm.isDead && hm.gameObject.activeInHierarchy) { hm.hp = Mathf.Max(hm.hp, 3000); all.Add(hm); }
                        var before = new Dictionary<HealthManager, int>();
                        foreach (var hm in all) before[hm] = tp.DevMarksOn(hm);
                        var col = target.GetComponent<Collider2D>();
                        Vector2 aim = col != null ? (Vector2)col.bounds.center : (Vector2)target.transform.position;
                        mod.DevAim = aim;
                        while (!mod.DevReady || mod.IsUsingItem) yield return null;
                        int hits0 = TerraProjectiles.DevHit(ProjKind.ShadeRayHit), calls0 = TerraProjectiles.DevMarkCalls, skip0 = TerraProjectiles.DevMarkSkipped;
                        int dummy0 = World.TargetDummy.DevHits, rays0 = TerraProjectiles.DevRays;
                        mod.DevUseSelected(aim);
                        for (int k = 0; k < 4; k++) yield return null;
                        shots++;
                        int hits = TerraProjectiles.DevHit(ProjKind.ShadeRayHit) - hits0, calls = TerraProjectiles.DevMarkCalls - calls0, skipped = TerraProjectiles.DevMarkSkipped - skip0;
                        int gained = 0;
                        var lines = new List<string>();
                        foreach (var hm in all)
                        {
                            if (hm == null) continue;
                            int a = before[hm], b = tp.DevMarksOn(hm);
                            if (b > a) gained += b - a;
                            if (a != b || hm == target) lines.Add($"{hm.name} {a}→{b}");
                        }
                        float dist = Vector2.Distance(aim, hero.GetComponent<BoxCollider2D>().bounds.center);
                        string what = $"{room} 第 {s + 1} 枪 瞄 {target.name}（{dist:F1}）：命中 {hits}，印记调用 {calls}（跳过 {skipped}），涨层 {gained}，射出 {TerraProjectiles.DevRays - rays0}，假人挨打 {World.TargetDummy.DevHits - dummy0}；{string.Join("，", lines)}";
                        if (hits != calls || calls - skipped != gained)
                        {
                            bad++;
                            Plugin.Log.LogInfo("[AutoTest] 印记 对不上 " + what);
                        }
                        else Plugin.Log.LogInfo("[AutoTest] 印记 " + what);
                        if (s % 4 == 3)
                        {
                            while (!mod.DevReady || mod.IsUsingItem) yield return null;
                            mod.DevUseSelected(aim, alt: true);
                            yield return new WaitForSeconds(0.5f);
                        }
                        if (s % 3 == 2) yield return new WaitForSeconds(0.4f);
                    }
                }

                yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
                yield return new WaitForSeconds(1f);
                hero = HeroController.instance;
                Arm();
                HealthManager Nearest()
                {
                    HealthManager best = null;
                    Vector2 c = hero.GetComponent<BoxCollider2D>().bounds.center;
                    foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                    {
                        if (hm.isDead || !hm.gameObject.activeInHierarchy || hm.gameObject.scene.name != "Crossroads_01") continue;
                        float d = Vector2.Distance(hm.transform.position, c);
                        if (d > 14f) continue;
                        if (best == null || d < Vector2.Distance(best.transform.position, c)) best = hm;
                    }
                    return best;
                }
                yield return Round("十字路", 32, Nearest);

                yield return GoTo("Town", "left1");
                for (float t = Time.realtimeSinceStartup + 10f; World.TargetDummy.DevAt == null && Time.realtimeSinceStartup < t;) yield return null;
                hero = HeroController.instance;
                Arm();
                if (World.TargetDummy.DevAt != null)
                {
                    hero.transform.position = Clear(hero, World.TargetDummy.DevAt.Value + Vector2.left * 6f + Vector2.up * 0.5f);
                    yield return new WaitForSeconds(0.5f);
                    yield return Round("假人", 16, () => World.TargetDummy.DevHm);
                    hero.transform.position = Clear(hero, World.TargetDummy.DevAt.Value + Vector2.right * 6f + Vector2.up * 0.5f);
                    yield return new WaitForSeconds(0.5f);
                    yield return Round("假人左射", 16, () => World.TargetDummy.DevHm);
                }
            }
            finally
            {
                mod.DevAim = null;
                Player.TerraStats.DevImmortal = false;
            }
            Plugin.Log.LogInfo($"[AutoTest] 印记 合计 {shots} 枪，对不上 {bad} 枪 {(bad == 0 ? "通过" : "不通过")}");
        }
    }
}
