using System.Collections;
using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.Town;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "progression"): the staged progression. The generated data (items, materials,
    /// stages, recipes); King's Pass's enemies doubled and respawning (a kill counted in the monster
    /// book with its drops, the empty slot filled again out of sight); in normal (non-test) mode
    /// the recipe column (a broadsword made from copper bars), geo with spare copper, and
    /// Dirtmouth's townsfolk by stage: talking, a shop's stock, buying, selling and buying back;
    /// pictures of the town, the chat, the shop and the monster book.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        /// <summary>Dev: what is drawn in front of the gameplay plane at these viewport points (true perspective rays), nearest first.</summary>
        static void ProbeOccluders(params Vector2[] points)
        {
            var cam = GameCameras.instance.mainCamera;
            var all = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            foreach (var vp in points)
            {
                var ray = cam.ViewportPointToRay(new Vector3(vp.x, vp.y, 0f));
                var list = new List<(float z, string s)>();
                foreach (var rd in all)
                {
                    if (!rd.enabled || !rd.gameObject.activeInHierarchy || rd.transform.root.name == "TerraKnight") continue;
                    var bb = rd.bounds;
                    float t = (bb.center.z - ray.origin.z) / ray.direction.z;
                    var pt = ray.origin + ray.direction * t;
                    if (!bb.Contains(new Vector3(pt.x, pt.y, bb.center.z))) continue;
                    Color c = rd is SpriteRenderer sr ? sr.color : rd.sharedMaterial != null && rd.sharedMaterial.HasProperty("_Color") ? rd.sharedMaterial.color : Color.white;
                    string what = rd is SpriteRenderer sr2 && sr2.sprite != null ? sr2.sprite.name : rd.sharedMaterial != null ? rd.sharedMaterial.name : "?";
                    list.Add((bb.center.z, $"{rd.gameObject.scene.name}:{PathOf(rd.transform)} [{rd.GetType().Name} {what} z={bb.center.z:F1} {rd.sortingLayerName}/{rd.sortingOrder} 层{rd.gameObject.layer} 队列{(rd.sharedMaterial != null ? rd.sharedMaterial.renderQueue : -1)} 色{c} 范围 {bb.min:F1}..{bb.max:F1}]"));
                }
                list.Sort((x, y) => x.z.CompareTo(y.z));
                var sb = new System.Text.StringBuilder();
                foreach (var e in list) if (sb.Length < 9000) sb.Append("\n    ").Append(e.s);
                Plugin.Log.LogInfo($"[AutoTest] 遮挡探针 视口 {vp}：射线上 {list.Count} 个渲染器（不含主角）：{sb}");
            }
        }

        IEnumerator ProgressionTest(string scene, HeroController hero)
        {
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            var inv = mod.Inventory;

            // --- Data.
            int materials = 0, total = 0;
            var stages = new int[6];
            var sources = new Dictionary<ItemSource, int>();
            foreach (var d in ItemDb.All)
            {
                total++;
                if (d.Material) materials++;
                stages[Mathf.Clamp(d.Stage, 0, 5)]++;
                sources[d.Source] = sources.TryGetValue(d.Source, out int n) ? n + 1 : 1;
            }
            var ne = Recipes.For(ItemType.NightsEdge);
            var neText = new System.Text.StringBuilder();
            foreach (var (t, c) in ne.Ingredients) neText.Append($"{ItemDb.Get(t).Name}×{c} ");
            Plugin.Log.LogInfo($"[AutoTest] 进度数据：物品 {total} 件（材料 {materials}），配方 {Recipes.All.Count} 条，各阶段 [{string.Join(",", stages)}]，来源 {string.Join(" ", sources)}；" +
                               $"永夜刃 = {neText}@{ne.Station}；克苏鲁护盾名={ItemDb.Get(ItemType.ShieldOfCthulhu).Name}；迷你鲨 {Shop.Money(ItemDb.Get(ItemType.Minishark).Value)}，火枪子弹 {Shop.Money(ItemDb.Get(ItemType.MusketBall).Value)}");

            // --- Normal mode from here (the recipe column, geo).
            mod.SetInfiniteItems(false);
            yield return null;
            PlayerData.instance.geo = 1000;
            Shop.SpareCopper = 0;

            // --- King's Pass: doubled enemies.
            var resp = EnemyRespawn.Instance;
            int clones = 0, originals = 0;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                if (hm.isDead || !hm.gameObject.activeInHierarchy) continue;
                if (hm.gameObject.name.Contains("(TK)")) clones++;
                else originals++;
            }
            Plugin.Log.LogInfo($"[AutoTest] 刷怪：刷新点 {resp.SlotCount}，模板 {EnemyRespawn.DevCaptured}，多出的 {EnemyRespawn.DevDoubled}，场景里活着的原版敌人 {originals}、复制体 {clones}");

            // A kill: counted, drops rolled.
            HealthManager victim = null;
            float best = float.MaxValue;
            foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
            {
                if (hm.isDead || !hm.gameObject.activeInHierarchy || UI.EnemyHover.RecordOf(hm) == null) continue;
                float d = Vector2.Distance(hm.transform.position, hero.transform.position);
                if (d < best) { best = d; victim = hm; }
            }
            int dropped0 = EnemyDrops.DevDropped, items0 = WorldItems.Count;
            string rec = victim != null ? UI.EnemyHover.RecordOf(victim) : null;
            Vector2 died = victim != null ? (Vector2)victim.transform.position : (Vector2)hero.transform.position;
            if (victim != null)
            {
                for (int k = 0; k < 20 && victim != null && !victim.isDead; k++)
                {
                    mod.Melee.HitWith(victim.GetComponent<Collider2D>() ?? victim.GetComponentInChildren<Collider2D>(), 99999, 0f, 0f, 1f, hero.transform.position);
                    yield return null;
                }
            }
            yield return new WaitForSeconds(0.5f);
            Plugin.Log.LogInfo($"[AutoTest] 击杀 {rec}（{Bestiary.NameOf(rec)}）：图鉴击杀 {Bestiary.KillsOf(rec)}，掉落 {EnemyDrops.DevDropped - dropped0} 件，地上物品 {items0} -> {WorldItems.Count}；掉落表 {EnemyDrops.Of(rec).Count} 项");
            // Its drops really land: thirty more rolls of its table where it died.
            if (rec != null)
            {
                int d0 = EnemyDrops.DevDropped, w0 = WorldItems.Count;
                for (int k = 0; k < 30; k++) EnemyDrops.Roll(rec, died);
                Plugin.Log.LogInfo($"[AutoTest] 再掷 30 次掉落：掉了 {EnemyDrops.DevDropped - d0} 件，地上物品 {w0} -> {WorldItems.Count}");
            }
            // Many kills of the same kind: the drop rate comes out near the table's.
            int rolls = 300, got = 0;
            var table = EnemyDrops.Of(rec);
            if (table.Count > 0)
            {
                for (int k = 0; k < rolls; k++) if (Random.Range(0, table[0].Den) < table[0].Num) got++;
                Plugin.Log.LogInfo($"[AutoTest] 掉落几率：{ItemDb.Get(table[0].Type).Name} 表上 {table[0].Num}/{table[0].Den}，模拟 {rolls} 次得 {got}（{got * 100f / rolls:F0}%）");
            }

            // Respawn, due at once. A kill in view (the player standing beside it) comes back from
            // just past the edge of the screen: a walker on a floor, a flyer in the air.
            float savedSeconds = EnemyRespawn.RespawnSeconds;
            EnemyRespawn.RespawnSeconds = 1f;
            yield return new WaitForSeconds(1.2f);
            var home = hero.transform.position;
            HealthManager Nearest(string record)
            {
                HealthManager found = null;
                float bestD = float.MaxValue;
                foreach (var hm in Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None))
                {
                    if (hm.isDead || !hm.gameObject.activeInHierarchy) continue;
                    string r = UI.EnemyHover.RecordOf(hm);
                    if (r == null || (record != null && r != record)) continue;
                    float d = Vector2.Distance(hm.transform.position, hero.transform.position);
                    if (d < bestD) { bestD = d; found = hm; }
                }
                return found;
            }
            foreach (var want in new[] { "Crawler", "Buzzer" })
            {
                var near = Nearest(want);
                if (near == null)
                {
                    Plugin.Log.LogInfo($"[AutoTest] 刷新：场景里没有活着的 {want}");
                    continue;
                }
                hero.transform.position = Clear(hero, (Vector2)near.transform.position + Vector2.left * 3f);
                hero.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
                yield return new WaitForSeconds(0.5f);
                Vector2 np = near.transform.position;
                bool inView = EnemyRespawn.DevView.Contains(np);
                int r0 = EnemyRespawn.DevRespawned, o0 = EnemyRespawn.DevOffScreen;
                float killedAt = Time.time;
                for (int k = 0; k < 20 && near != null && !near.isDead; k++)
                {
                    mod.Melee.HitWith(near.GetComponent<Collider2D>() ?? near.GetComponentInChildren<Collider2D>(), 99999, 0f, 0f, 1f, hero.transform.position);
                    yield return null;
                }
                while (EnemyRespawn.DevRespawned == r0 && Time.time - killedAt < 4f) yield return null;
                var seen = EnemyRespawn.DevView;
                var at = EnemyRespawn.DevLastSpawn;
                Plugin.Log.LogInfo($"[AutoTest] 刷新（{want} 死在屏幕里={inView}，主角就站在旁边）：{Time.time - killedAt:F1} 秒后重生 {EnemyRespawn.DevRespawned - r0} 只，屏幕外刷出 {EnemyRespawn.DevOffScreen - o0} 只，" +
                                   $"刷出点 {at:F1}（屏幕 x {seen.xMin:F1}..{seen.xMax:F1}，在屏幕外={!seen.Contains(at)}，比原地高 {at.y - np.y:F1}），离主角 {Vector2.Distance(at, hero.transform.position):F1}，脚下实心={TerraWorld.IsSolidAt(at + Vector2.down * 0.9f)}");
                yield return new WaitForSeconds(0.3f);
            }
            // Its place out of view: back where it stood.
            var victim2 = Nearest(null);
            if (victim2 != null)
            {
                int r0 = EnemyRespawn.DevRespawned, o0 = EnemyRespawn.DevOffScreen;
                for (int k = 0; k < 20 && victim2 != null && !victim2.isDead; k++)
                {
                    mod.Melee.HitWith(victim2.GetComponent<Collider2D>() ?? victim2.GetComponentInChildren<Collider2D>(), 99999, 0f, 0f, 1f, hero.transform.position);
                    yield return null;
                }
                hero.transform.position = Clear(hero, (Vector2)hero.transform.position + Vector2.right * 40f);
                yield return new WaitForSeconds(3f);
                Plugin.Log.LogInfo($"[AutoTest] 刷新：打死后走开 40 格，3 秒内重生 {EnemyRespawn.DevRespawned - r0} 只（原地刷出，屏幕外挪位 {EnemyRespawn.DevOffScreen - o0} 次）");
            }
            EnemyRespawn.RespawnSeconds = savedSeconds;
            hero.transform.position = home;
            yield return new WaitForSeconds(0.5f);

            // --- Geo with spare copper.
            long purse0 = Shop.Purse;
            bool paid = Shop.Spend(ItemDb.Get(ItemType.Torch).Value);
            Plugin.Log.LogInfo($"[AutoTest] 吉欧：{Shop.Money(purse0)} 买火把（{Shop.Money(ItemDb.Get(ItemType.Torch).Value)}）{paid} -> 吉欧 {Shop.Geo}，零头 {Shop.SpareCopper} 铜（{Shop.Money(Shop.Purse)}）");

            // --- Dirtmouth.
            yield return GoTo("Town", "left1");
            hero = HeroController.instance;
            yield return new WaitForSeconds(1f);
            Plugin.Log.LogInfo($"[AutoTest] 德特茅斯（阶段 {Shop.UnlockedStage()}）：{TownNpcs.Count} 位 NPC：{TownNpcs.DevDescribe()}；刷新点 {resp.SlotCount}（国王山道作为邻场景再接入后不应翻倍）");
            // --- Recipes (Dirtmouth counts as a station): copper bars make a copper broadsword.
            inv.Pickup(ItemType.CopperBar, 20);
            yield return null;
            string kits = InventoryGui.DevRecipeKits();
            // Copper bars the enemies dropped earlier on are in the bag too: what counts is how many the sword took.
            int barsBefore = Recipes.Count(inv, ItemType.CopperBar);
            bool made = InventoryGui.DevCraft(ItemType.CopperBroadsword);
            int barsAfter = Recipes.Count(inv, ItemType.CopperBar);
            Plugin.Log.LogInfo($"[AutoTest] 配方：制作栏 {kits}；做铜阔剑 {made}，光标 {(inv.Mouse.IsEmpty ? "空" : inv.Mouse.Def.Name)}，铜锭 {barsBefore} -> {barsAfter}，用掉 {barsBefore - barsAfter}（原版 6 个一把）");
            mod.ReturnOrDropMouseItem();

            // Later stages bring more of them.
            var visited = PlayerData.instance.scenesVisited;
            bool added = !visited.Contains("Deepnest_East_01");
            if (added) visited.Add("Deepnest_East_01");
            yield return new WaitForSeconds(0.5f);
            Plugin.Log.LogInfo($"[AutoTest] 去过王国边缘后（阶段 {Shop.UnlockedStage()}）：{TownNpcs.Count} 位：{TownNpcs.DevDescribe()}");
            // Next to a townsperson on the floor he stands on (left1 is high up on the cliff).
            Vector3 Beside(Vector2 npc)
            {
                var h = HeroController.instance;
                h.GetComponent<Rigidbody2D>().velocity = Vector2.zero;
                float bottom = h.GetComponent<BoxCollider2D>().bounds.min.y - h.transform.position.y;
                return Clear(h, new Vector2(npc.x - 1.5f, npc.y - 20f / 32f - bottom + 0.05f));
            }
            var mpos = TownNpcs.DevPos("Merchant");
            if (mpos.HasValue)
            {
                hero.transform.position = Beside(mpos.Value);
                yield return new WaitForSeconds(0.6f);
                Shot("Town", "p_town");
                yield return null;
                yield return null;
                ProbeOccluders(new Vector2(0.5f, 0.52f), new Vector2(0.3f, 0.6f), new Vector2(0.3f, 0.52f));
                mpos = TownNpcs.DevPos("Merchant");
                bool talked = TownNpcs.TryRightClick(mpos.Value);
                yield return new WaitForSeconds(0.3f);
                Plugin.Log.LogInfo($"[AutoTest] 右键商人：对话={talked}，说：{TownNpcs.ChatText}");
                Shot("Town", "p_chat");
                yield return null;
                yield return null;
                TownNpcs.OpenShop();
                yield return new WaitForSeconds(0.3f);
                Plugin.Log.LogInfo($"[AutoTest] 商人的商店：{TownNpcs.DevStock()}");
                int torchSlot = System.Array.FindIndex(TownNpcs.ShopItems, s => s.Type == ItemType.Torch);
                long before = Shop.Purse;
                bool bought = TownNpcs.Buy(torchSlot, 5, inv);
                Plugin.Log.LogInfo($"[AutoTest] 买 5 个火把：{bought}，光标 {(inv.Mouse.IsEmpty ? "空" : inv.Mouse.Def.Name + "×" + inv.Mouse.Count)}，花了 {Shop.Money(before - Shop.Purse)}（原版 5×50 铜 = 2.5 吉欧）");
                mod.ReturnOrDropMouseItem();
                // Selling: the copper pickaxe, then it waits in the shop to be bought back.
                int pick = System.Array.FindIndex(inv.Slots, s => s.Type == ItemType.CopperPickaxe);
                before = Shop.Purse;
                if (pick >= 0) TownNpcs.Sell(ref inv.Slots[pick]);
                Plugin.Log.LogInfo($"[AutoTest] 卖铜镐：得 {Shop.Money(Shop.Purse - before)}（原版 价值 5 银的五分之一 = 0.01 吉欧×100 铜…= 1 银 = 1 吉欧），商店现在：{TownNpcs.DevStock()}");
                Shot("Town", "p_shop");
                yield return null;
                yield return null;
                TownNpcs.Close(true);
                InventoryGui.Open = false;
            }
            var apos = TownNpcs.DevPos("ArmsDealer");
            if (apos.HasValue)
            {
                hero.transform.position = Beside(apos.Value);
                yield return new WaitForSeconds(0.5f);
                TownNpcs.TryRightClick(TownNpcs.DevPos("ArmsDealer").Value);
                TownNpcs.OpenShop();
                yield return null;
                Plugin.Log.LogInfo($"[AutoTest] 军火商的商店（阶段 4）：{TownNpcs.DevStock()}");
                TownNpcs.Close(true);
                InventoryGui.Open = false;
            }
            var spos = TownNpcs.DevPos("SkeletonMerchant");
            if (spos.HasValue)
            {
                hero.transform.position = Beside(spos.Value);
                yield return new WaitForSeconds(0.5f);
                TownNpcs.TryRightClick(TownNpcs.DevPos("SkeletonMerchant").Value);
                TownNpcs.OpenShop();
                yield return null;
                Plugin.Log.LogInfo($"[AutoTest] 骷髅商人（月相 {TownNpcs.MoonPhase}）：{TownNpcs.DevStock()}");
                TownNpcs.Close(true);
                InventoryGui.Open = false;
            }

            // --- The monster book.
            InventoryGui.Open = true;
            InventoryGui.DevBestiary(true, 0);
            yield return null;
            yield return null;
            Shot("Town", "p_bestiary");
            yield return null;
            yield return null;
            InventoryGui.DevBestiary(false);
            InventoryGui.Open = false;
            Plugin.Log.LogInfo($"[AutoTest] 图鉴：{Bestiary.Order.Count} 种（{string.Join("、", Bestiary.Order)}）");

            if (added) visited.Remove("Deepnest_East_01");
            mod.SetInfiniteItems(true);
            mod.DevNoFallDamage = false;
        }
    }
}
