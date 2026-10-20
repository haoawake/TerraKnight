using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "drops2"): the reworked drop tables. Thousands of kills rolled at stages 0, 2 and 5 for a few
    /// normal enemies, elites and bosses: nothing above the stage drops, an elite's or boss's sure pick gives one
    /// item each kill, the early movement accessories come at stage 0. A real kill's geo is the table's. Quick
    /// stacking puts the geo into a Piggy Bank in range, else into a chest. The book with locked drops photographed.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator Drops2Test()
        {
            if (suite == "dropsgeo") { yield return EnemyGeoTest(); yield break; }
            yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            var hero = HeroController.instance;
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 掉落2 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }

            // 1. Rolled kills.
            var counts = new Dictionary<ItemType, int>();
            EnemyDrops.DevSink = (t, n) => counts[t] = (counts.TryGetValue(t, out int c) ? c : 0) + 1;
            var early = new[] { ItemType.HermesBoots, ItemType.FlyingCarpet, ItemType.SandBoots, ItemType.SandstorminaBottle, ItemType.CloudinaBottle, ItemType.AnkletoftheWind, ItemType.LuckyHorseshoe, ItemType.ShinyRedBalloon };
            foreach (var stage in new[] { 0, 2, 5 })
            {
                Shop.DevStage = stage;
                foreach (var rec in new[] { "Crawler", "ZombieRunner", "Climber", "Buzzer", "ZombieGuard", "FalseKnight", "Sentry", "GreatShieldZombie", "MageLord", "Mosquito" })
                {
                    var table = EnemyDrops.Of(rec);
                    int groups = table.Where(d => d.Group > 0).Select(d => d.Group).Distinct().Count();
                    int kills = groups > 0 ? 400 : 3000;
                    counts.Clear();
                    int picks0 = EnemyDrops.DevGroupPicks;
                    for (int i = 0; i < kills; i++) EnemyDrops.Roll(rec, hero.transform.position);
                    // What may drop at this stage: the rows unlocked now (an enemy's own rows always; added ones by their stage).
                    var allowed = new HashSet<ItemType>(table.Where(d => d.LockStage <= stage).Select(d => d.Type));
                    int above = counts.Keys.Count(t => !allowed.Contains(t));
                    int picks = EnemyDrops.DevGroupPicks - picks0;
                    int earlyGot = early.Count(t => counts.ContainsKey(t));
                    int locked = table.Count(d => EnemyDrops.Locked(d));
                    string got = string.Join(" ", counts.OrderByDescending(kv => kv.Value).Take(8).Select(kv => $"{ItemDb.Get(kv.Key)?.Name}×{kv.Value}"));
                    bool pass = above == 0 && (groups == 0 || picks == kills * groups || table.Where(d => d.Group > 0).All(d => EnemyDrops.Locked(d)));
                    Check($"阶段 {stage} {rec}（{EnemyDrops.ZhName(rec)}）", pass,
                        $"{kills} 次击杀，表 {table.Count} 项（锁住 {locked}），掉出超出阶段的 {above} 种，必掉组 {groups} 个、抽中 {picks} 次，前期机动饰品 {earlyGot} 种；最多的：{got}");
                }
            }
            EnemyDrops.DevSink = null;
            Shop.DevStage = -1;

            // 2. A real kill's geo.
            yield return new WaitForSeconds(1f);
            HealthManager target = null;
            foreach (var hm in EnemyRespawn.Instance.DevLive("Crossroads_01"))
                if (UI.EnemyHover.RecordOf(hm) == "ZombieRunner" || UI.EnemyHover.RecordOf(hm) == "Climber") { target = hm; break; }
            if (target != null)
            {
                string rec = UI.EnemyHover.RecordOf(target);
                int want = EnemyDrops.GeoOf(EnemyDrops.KeyFor(rec, EnemyDrops.VariantLevel("Crossroads_01")));
                EnemyDrops.DevGeo = -1;
                Kill(target);
                yield return new WaitForSeconds(0.5f);
                Check("打死一只的吉欧", EnemyDrops.DevGeo == want && want > 0, $"{rec}：表里 {want}，实际给 {EnemyDrops.DevGeo}");
            }
            else Check("打死一只的吉欧", false, "找不到游荡躯壳或滴答虫");

            // 3. Quick stacking the geo: a Piggy Bank in range takes it.
            var inv = mod.Inventory;
            float settle = Time.time + 3f;
            while (!hero.cState.onGround && Time.time < settle) yield return null;
            var feet = hero.GetComponent<BoxCollider2D>().bounds;
            bool bank = false;
            foreach (float dx in new[] { 1.5f, -1.5f, 2.5f, -2.5f, 3.5f })
                if (Stations.TryPlace(StationKind.PiggyBank, hero, new Vector2(feet.center.x + dx, feet.min.y + 0.2f))) { bank = true; break; }
            for (int i = 0; i < inv.Bank.Length; i++) inv.Bank[i] = default;
            int slot = Inventory.HotbarSize + 3;
            inv.Slots[slot] = ItemStack.Of(ItemType.Geo, 777);
            int moved = Chests.QuickStackNearby(inv, feet.center);
            long banked = GeoWallet.Banked(inv);
            Check("存钱罐在旁边时快速堆叠吉欧", bank && banked == 777 && inv.Slots[slot].IsEmpty, $"放存钱罐 {bank}，移动 {moved} 组，存钱罐里吉欧 {banked}，背包格 {(inv.Slots[slot].IsEmpty ? "空了" : inv.Slots[slot].Count.ToString())}");

            // 4. No Piggy Bank in range: a chest takes it.
            yield return GoTo("Crossroads_02", EntryGateOf("Crossroads_02"));
            hero = HeroController.instance;
            pd.isInvincible = true;
            settle = Time.time + 3f;
            while (!hero.cState.onGround && Time.time < settle) yield return null;
            feet = hero.GetComponent<BoxCollider2D>().bounds;
            bool chest = false;
            foreach (float dx in new[] { 1.2f, -1.2f, 2f, -2f, 2.8f, -2.8f })
                if (Chests.TryPlace(hero, new Vector2(feet.center.x + dx, feet.min.y + 0.3f))) { chest = true; break; }
            inv.Slots[slot] = ItemStack.Of(ItemType.Geo, 500);
            moved = Chests.QuickStackNearby(inv, feet.center);
            Chests.DevOpenNear(feet.center);
            int inChest = Chests.OpenItems != null ? Chests.OpenItems.Where(s => !s.IsEmpty && s.Type == ItemType.Geo).Sum(s => s.Count) : -1;
            Chests.Close(false);
            Check("没有存钱罐时快速堆叠吉欧", chest && inChest == 500 && inv.Slots[slot].IsEmpty, $"放箱子 {chest}，移动 {moved} 组，箱子里吉欧 {inChest}");

            // 5. The book on an elite with locked drops, at stage 0.
            Shop.DevStage = 0;
            Bestiary.Kills["ZombieGuard"] = Mathf.Max(1, Bestiary.Kills.TryGetValue("ZombieGuard", out int k) ? k : 0);
            InventoryGui.Open = true;
            InventoryGui.DevBestiaryShow("ZombieGuard");
            yield return new WaitForSeconds(0.8f);
            Shot("Crossroads_02", "drops2_book_stage0");
            yield return null;
            // Down to the locked rows and the sure pick.
            InventoryGui.DevScrollDrops(99);
            yield return new WaitForSeconds(0.4f);
            Shot("Crossroads_02", "drops2_book_stage0_bottom");
            yield return null;
            Shop.DevStage = 5;
            yield return new WaitForSeconds(0.5f);
            Shot("Crossroads_02", "drops2_book_stage5");
            yield return null;
            InventoryGui.DevBestiary(false);
            InventoryGui.Open = false;
            Shop.DevStage = -1;
            Plugin.Log.LogInfo($"[AutoTest] 掉落2 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }

        IEnumerator EnemyGeoTest()
        {
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            var previous = GameDifficulty.Current;
            int checks = 0, failed = 0;
            var small = HarmonyLib.AccessTools.FieldRefAccess<HealthManager, int>("smallGeoDrops");
            var medium = HarmonyLib.AccessTools.FieldRefAccess<HealthManager, int>("mediumGeoDrops");
            var large = HarmonyLib.AccessTools.FieldRefAccess<HealthManager, int>("largeGeoDrops");
            var prepare = HarmonyLib.AccessTools.Method(typeof(EnemyDrops), "MoreGeo");
            var probe = new GameObject("Geo reward fallback probe");
            var health = probe.AddComponent<HealthManager>();
            try
            {
                foreach (DifficultyLevel level in System.Enum.GetValues(typeof(DifficultyLevel)))
                {
                    GameDifficulty.Current = level;
                    float oldScale = level == DifficultyLevel.Hard ? 1.25f : level == DifficultyLevel.Expert ? 1.5f : level >= DifficultyLevel.Master ? 2f : 1f;
                    foreach (string record in EnemyDrops.DevRecords)
                    {
                        int raw = EnemyDrops.BaseGeoOf(record);
                        int expected = raw < 0 ? -1 : Mathf.RoundToInt(raw * oldScale * 1.5f);
                        checks++;
                        if (EnemyDrops.GeoOf(record) != expected) failed++;
                    }
                    foreach (int original in new[] { 0, 1, 5, 25, 57, 100 })
                    {
                        small(health) = original % 5;
                        medium(health) = original % 25 / 5;
                        large(health) = original / 25;
                        prepare.Invoke(null, new object[] { health });
                        int actual = small(health) + 5 * medium(health) + 25 * large(health);
                        checks++;
                        if (actual != Mathf.RoundToInt(original * oldScale * 1.5f)) failed++;
                    }
                }
                GameDifficulty.Current = DifficultyLevel.Master;
                yield return new WaitForSeconds(1f);
                var target = EnemyRespawn.Instance.DevLive("Crossroads_01").FirstOrDefault(hm =>
                    UI.EnemyHover.RecordOf(hm) == "ZombieRunner" || UI.EnemyHover.RecordOf(hm) == "Climber");
                checks++;
                if (target == null) failed++;
                else
                {
                    string record = UI.EnemyHover.RecordOf(target);
                    int expected = EnemyDrops.GeoOf(EnemyDrops.KeyFor(record, EnemyDrops.VariantLevel("Crossroads_01")));
                    EnemyDrops.DevGeo = -1;
                    Kill(target);
                    Plugin.Log.LogInfo($"[AutoTest] 吉欧实际击杀 {record}：期望 {expected}，实际 {EnemyDrops.DevGeo}");
                    if (expected <= 0 || EnemyDrops.DevGeo != expected) failed++;
                }
                Plugin.Log.LogInfo($"[AutoTest] 吉欧增量检查 {checks} 项、失败 {failed} {(failed == 0 ? "通过" : "不通过")}");
            }
            finally
            {
                GameDifficulty.Current = previous;
                Object.Destroy(probe);
            }
        }
    }
}
