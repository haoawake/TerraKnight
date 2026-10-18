using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.UI;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "book"): the monster book. With the book emptied, an enemy brought on screen is
    /// discovered (its note at the bottom), killed it is collected (the second note); the book is
    /// photographed open on it, on an entry not seen yet and filtered to an area; the save keeps the
    /// sightings. The book is put back as it was afterwards.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator BookTest(string scene, HeroController hero)
        {
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            var col = hero.GetComponent<BoxCollider2D>();
            var rb = hero.GetComponent<Rigidbody2D>();
            var home = Clear(hero, (Vector2)hero.transform.position);
            hero.transform.position = home;
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            // The book as it was, to put back.
            var kills = new Dictionary<string, int>(Bestiary.Kills);
            var order = new List<string>(Bestiary.Order);
            var seenOrder = new List<string>(Bestiary.SeenOrder);
            var stats = new Dictionary<string, Bestiary.Seen>(Bestiary.Stats);
            Bestiary.Clear();

            HealthManager target = null;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                if (hm.isDead || !hm.gameObject.activeInHierarchy || hm.gameObject.scene.name != scene) continue;
                if (Bestiary.KeyOf(hm) == "Crawler") { target = hm; break; }
            }
            if (target == null)
            {
                Plugin.Log.LogInfo("[AutoTest] 图鉴：场景里没有小爬虫");
                yield break;
            }
            string rec = Bestiary.KeyOf(target);
            int disc0 = Bestiary.DevDiscovered, shown0 = BestiaryToast.DevShown;
            var tcol = target.GetComponent<Collider2D>();
            var at = new Vector2(tcol.bounds.min.x - col.bounds.extents.x - 1.5f, tcol.bounds.min.y + col.bounds.extents.y + 0.05f);
            hero.transform.position += (Vector3)(at - (Vector2)col.bounds.center);
            rb.linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(1.2f);
            Shot(scene, "book_1_discovered");
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 图鉴 发现：新发现 {Bestiary.DevDiscovered - disc0} 种，{rec} 已发现 {Bestiary.IsSeen(rec)}，提示 {BestiaryToast.DevShown - shown0} 条（{BestiaryToast.DevLast}）；" +
                               $"数值 生命 {Bestiary.StatsOf(rec).Life} 攻击 {Bestiary.StatsOf(rec).Damage} 防御 {Bestiary.StatsOf(rec).Defense} 击退 {Bestiary.StatsOf(rec).Knockback:F2}，区域 {World.Areas.AreaName(Bestiary.StatsOf(rec).Scene)}；" +
                               $"立绘 {(EnemyHover.PortraitOf(rec) != null ? EnemyHover.PortraitOf(rec).name : "无")}，描述 {EnemyHover.JournalText(rec, "DESC_")}");
            yield return new WaitForSeconds(3.4f);

            // Defeated: collected.
            int col0 = Bestiary.DevCollected;
            if (target != null)
            {
                target.hp = 1;
                mod.Melee.HitWith(tcol, 100000, 0f, 0f, 1f, col.bounds.center);
            }
            yield return new WaitForSeconds(0.5f);
            Shot(scene, "book_2_collected");
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 图鉴 击败：击败 {Bestiary.KillsOf(rec)} 次，已收集 {Bestiary.IsDefeated(rec)}，收集 {Bestiary.DevCollected - col0} 次，提示（{BestiaryToast.DevLast}）；笔记 {EnemyHover.JournalText(rec, "NOTE_")}");
            yield return new WaitForSeconds(3.4f);

            // The book open: on the crawlid, on an entry not seen, filtered to the Crossroads.
            InventoryGui.Open = true;
            InventoryGui.DevBestiaryShow(rec);
            yield return new WaitForSeconds(0.4f);
            Shot(scene, "book_3_entry");
            yield return null;
            InventoryGui.DevBestiaryShow("MantisLord");
            yield return new WaitForSeconds(0.3f);
            Shot(scene, "book_4_unseen");
            yield return null;
            // The real book again, so the grid shows what this save has seen.
            foreach (var kv in kills) Bestiary.Kills[kv.Key] = kv.Value;
            foreach (var r in order) if (!Bestiary.Order.Contains(r)) Bestiary.Order.Add(r);
            foreach (var kv in stats) Bestiary.Stats[kv.Key] = kv.Value;
            foreach (var r in seenOrder) if (!Bestiary.SeenOrder.Contains(r)) Bestiary.SeenOrder.Add(r);
            InventoryGui.DevBestiaryShow(rec);
            InventoryGui.DevBestiaryFilter(1);
            yield return new WaitForSeconds(0.3f);
            Shot(scene, "book_5_filtered");
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 图鉴 地区：{string.Join(" ", BestiaryRoute.Areas)}；{BestiaryRoute.Areas[1]} 分栏：\n{InventoryGui.DevBestiaryLayout()}");
            int bossArea = -1;
            for (int a = 0; a < BestiaryRoute.Areas.Count; a++) if (BestiaryRoute.Areas[a] == "泪水之城") bossArea = a;
            InventoryGui.DevBestiaryFilter(bossArea);
            yield return new WaitForSeconds(0.3f);
            Shot(scene, "book_6_city");
            yield return null;
            Plugin.Log.LogInfo($"[AutoTest] 图鉴 泪水之城 分栏：\n{InventoryGui.DevBestiaryLayout()}");

            // A creative save's book is all open.
            bool wasCreative = mod.Creative;
            mod.ApplyCreative(true);
            InventoryGui.DevBestiaryShow("MantisLord");
            yield return new WaitForSeconds(0.3f);
            Shot(scene, "book_7_creative");
            yield return null;
            int open = 0, total = 0;
            foreach (var r in EnemyDrops.DevRecords)
            {
                var k = EnemyDrops.KindOf(r);
                if (k == "non_enemy" || k == "player_shade") continue;
                total++;
                if (Bestiary.IsSeen(r) && Bestiary.IsDefeated(r)) open++;
            }
            Plugin.Log.LogInfo($"[AutoTest] 图鉴 创造模式：{open}/{total} 条全开");
            mod.ApplyCreative(wasCreative);
            InventoryGui.DevBestiary(false);
            InventoryGui.Open = false;

            // The save keeps the sightings.
            int seenBefore = Bestiary.Stats.Count, orderBefore = Bestiary.SeenOrder.Count;
            var keep = Bestiary.StatsOf(rec);
            int slot = GameManager.instance.profileID;
            Save.ModSave.Write(slot, mod);
            Save.ModSave.Read(slot, mod);
            yield return new WaitForSeconds(0.3f);
            var back = Bestiary.StatsOf(rec);
            Plugin.Log.LogInfo($"[AutoTest] 图鉴 存档：目击数据 {seenBefore} -> {Bestiary.Stats.Count} 条，顺序 {orderBefore} -> {Bestiary.SeenOrder.Count}，{rec} 生命 {keep.Life} -> {back.Life}，区域 {back.Scene}");
            hero = HeroController.instance;
            hero.transform.position = home;
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            mod.DevNoFallDamage = false;
        }
    }
}
