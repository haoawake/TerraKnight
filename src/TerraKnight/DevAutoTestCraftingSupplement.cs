using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator CraftingSupplementTest()
        {
            int ok = 0, all = 0;
            void Check(string label, bool pass, string detail = "")
            {
                all++; if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 合成补全 {label}：{detail} {(pass ? "通过" : "不通过")}");
            }
            yield return GoTo("Town", "left1");
            var hero = HeroController.instance;
            PlayerData.instance.isInvincible = true;
            mod.DevNoFallDamage = true;
            TerraMod.DevAllInfinite = false;
            mod.ApplyCreative(false);
            Shop.DevStage = 5;
            var inv = mod.Inventory;
            inv.Override = -1; inv.Selected = 0;
            InventoryGui.Open = true;
            Wear(); Equipment.Update(inv);
            void Fill(Recipe r)
            {
                System.Array.Clear(inv.Slots, 0, inv.Slots.Length);
                inv.Mouse = default;
                foreach (var g in r.Ingredients) inv.Add(g.Type, g.Count);
            }
            bool Craft(Recipe r)
            {
                Fill(r);
                bool made = InventoryGui.DevCraft(r.Result);
                return made && inv.Mouse.Type == r.Result && inv.Mouse.Count == r.Count && r.Ingredients.All(g => Recipes.Count(inv, g.Type) == 0);
            }
            var added = Recipes.SupplementRecipes();
            // The recipes themselves: their stations counted as standing by (where they may be made is suite craftrule's).
            InventoryGui.DevAnyStation = true;
            foreach (var r in added.Where(r => r.Environment == CraftEnvironment.None))
                Check("制作 " + r.Result + " " + r.Station, Craft(r), "实际制作入口、产出和材料扣除");
            foreach (var r in added.Where(r => r.Environment != CraftEnvironment.None))
            {
                Fill(r);
                Check("环境限制 " + r.Result, !InventoryGui.DevCraft(r.Result) && r.Ingredients.All(g => Recipes.Count(inv, g.Type) == g.Count), "Town 无墓地/蜂蜜时不制作、不扣材料");
            }
            InventoryGui.DevAnyStation = false;
            foreach (var stock in Town.CraftingSupplementShops.All)
            {
                int count = Town.TownNpcs.DevStockAt(stock.Npc, 5, stock.Type, out bool has);
                // Four rows of ten, and a fifth once the stock on sale passes forty (TownNpcs.ShopItems).
                Check("商店 " + stock.Type, has && count <= Town.TownNpcs.ShopItems.Length, stock.Npc + " 货品 " + count);
            }
            var shop = new HashSet<ItemType>(Town.CraftingSupplementShops.All.Select(s => s.Type));
            var drops = new HashSet<ItemType>();
            foreach (var key in EnemyDrops.DevRecords) foreach (var drop in EnemyDrops.Of(key)) drops.Add(drop.Type);
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
            var travel = new HashSet<ItemType>((((ItemType Type, int Stage)[])typeof(Town.TownNpcs).GetField("travelPool", flags).GetValue(null)).Select(e => e.Type));
            var angler = new HashSet<ItemType>(Town.Angler.DevRewardSources());
            bool Sourced(ItemType t, HashSet<ItemType> visited)
            {
                if (ItemDb.Get(t).Source == ItemSource.Shop)
                    foreach (var npc in new[] { "Merchant", "GoblinTinkerer", "SkeletonMerchant", "PartyGirl", "ArmsDealer", "WitchDoctor", "Cyborg", "Steampunker", "BestiaryGirl", "Demolitionist", "DyeTrader", "DD2Bartender", "Stylist" })
                    { Town.TownNpcs.DevStockAt(npc, 5, t, out bool has); if (has) return true; }
                if (shop.Contains(t) || drops.Contains(t) || ItemDb.Get(t).Source == ItemSource.Basic) return true;
                // The Traveling Merchant's pool and the Angler's quest rewards (Player.GetAnglerReward) give it too.
                if (travel.Contains(t) || angler.Contains(t)) return true;
                if (!visited.Add(t)) return false;
                return Recipes.All.Where(r => r.Result == t).Any(r => r.Ingredients.All(g => Sourced(g.Type, new HashSet<ItemType>(visited))));
            }
            // Every boss beaten: whether a material can be had at all, not whether this test save has fought for it yet.
            var clearsWas = VanillaBossProgression.DevClears;
            VanillaBossProgression.DevClears = new HashSet<string>(VanillaBossProgression.All.Keys);
            drops.Clear();
            foreach (var key in EnemyDrops.DevRecords) foreach (var drop in EnemyDrops.Of(key)) drops.Add(drop.Type);
            foreach (var t in added.SelectMany(r => r.Ingredients).Select(g => g.Type).Distinct())
                Check("材料来源 " + t, Sourced(t, new HashSet<ItemType>()), "递归检查配方、掉落和实际新增商店货品");
            VanillaBossProgression.DevClears = clearsWas;

            Wear(ItemType.HandOfCreation); Equipment.Update(inv);
            Check("创造之手装备加成", Equipment.Current.BlockSpeed >= .5f && Equipment.Current.WallSpeed >= .5f && Equipment.Current.TileRangeAcc
                && Equipment.Current.PickSpeed >= .25f && Equipment.Current.TreasureMagnet && Equipment.Current.PortableStool);
            Wear(ItemType.LavaproofTackleBag, ItemType.FishingBobberGlowingRainbow); Equipment.Update(inv);
            Check("渔具袋和浮标", Equipment.Current.FishingLine && Equipment.Current.TackleBox && Equipment.Current.AcidFishing
                && Equipment.Current.FishingSkill == 10 && Equipment.Current.BobberKind == 8);
            Check("墙不再作为基础商品", !Shop.Basics.Any(d => d.PlacesWall > 0));
            Check("骑枪伤害随速度", Mathf.Approximately(TerraProjectiles.LanceMultiplier(Vector2.zero), 0.1f) && TerraProjectiles.LanceMultiplier(Vector2.right * 14f) > 1f && Mathf.Approximately(TerraProjectiles.LanceMultiplier(Vector2.left * 12f), TerraProjectiles.LanceMultiplier(Vector2.right * 12f)));

            // The Flymeal special acts on the custom town residents drawn without a HealthManager.
            var resident = Town.TownNpcs.DevPos("Merchant");
            if (resident.HasValue)
            {
                var oldPosition = hero.transform.position;
                hero.transform.position = resident.Value + Vector2.left * .6f;
                inv.Slots[0] = ItemStack.Of(ItemType.Flymeal); inv.Mouse = default;
                InventoryGui.Open = false;
                int strikes = Town.TownNpcs.FlymealStrikes;
                mod.DevUseSelected(resident.Value);
                yield return new WaitForSeconds(.3f);
                Check("臭虫剑驱赶居民", Town.TownNpcs.FlymealStrikes > strikes);
                hero.transform.position = oldPosition;
            }
            else Check("臭虫剑找到居民", false);
            // Real world-region detection and rod use, with a deterministic water fixture in an isolated save.
            System.Array.Clear(inv.Slots, 0, inv.Slots.Length); inv.Mouse = default;
            inv.Slots[0] = ItemStack.Of(ItemType.Fleshcatcher);
            inv.Slots[1] = ItemStack.Of(ItemType.Worm, 99);
            var water = new GameObject("Crafting test water");
            water.transform.position = hero.transform.position + Vector3.right * 3;
            var box = water.AddComponent<BoxCollider2D>(); box.size = new Vector2(3, 1); box.isTrigger = true;
            var fsm = water.AddComponent<PlayMakerFSM>(); fsm.Fsm.Name = "Surface Water Region"; fsm.enabled = false;
            Vector2 aim = (Vector2)water.transform.position + Vector2.up * .5f;
            int catches = Fishing.Catches;
            // The fixture is a sliver of water: counted as a pond of 1000 tiles (Terraria wants 75 at least, 300 for full power).
            Fishing.DevPondTiles = 1000;
            Fishing.DevRescan();
            Check("钓竿抛竿", Fishing.Use(hero, aim, inv.Current.Def) && Fishing.Active && !Fishing.Biting, "实际场景液面检测");
            float until = Time.time + 30;
            while (!Fishing.Biting && Fishing.Active && Time.time < until) yield return null;
            Check("等待咬钩", Fishing.Biting);
            bool reeled = Fishing.Use(hero, aim, inv.Current.Def);
            Check("收线获得鱼", reeled && Fishing.Catches == catches + 1 && !Fishing.Active, "背包有防断线装备，实际等待后收鱼");
            Fishing.Cancel();
            var acidCheck = water.AddComponent<PlayMakerFSM>(); acidCheck.Fsm.Name = "Acid Armour Check"; acidCheck.enabled = false;
            Fishing.DevRescan();
            Wear(); Equipment.Update(inv);
            Check("酸液拒绝普通钓竿", !Fishing.Use(hero, aim, inv.Current.Def));
            Wear(ItemType.LavaFishingHook); Equipment.Update(inv);
            Check("酸液钓钩允许抛竿", Fishing.Use(hero, aim, inv.Current.Def));
            Fishing.Cancel(); Object.Destroy(water);
            Fishing.DevPondTiles = 0;
            Fishing.DevRescan();

            // Cast through the actual item use path and check mana, projectile hits and lance lifetime.
            InventoryGui.Open = false;
            Wear(); Equipment.Update(inv);
            yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            hero = HeroController.instance;
            hero.transform.position = Clear(hero, hero.transform.position);
            var target = Object.FindObjectsByType<HealthManager>(FindObjectsSortMode.None).FirstOrDefault(h => !h.isDead && h.gameObject.activeInHierarchy && h.gameObject.scene.name == "Crossroads_01");
            Check("找到真实敌人", target != null);
            var at = (Vector2)hero.GetComponent<BoxCollider2D>().bounds.center + Vector2.right * 1.2f;
            var targetRb = target != null ? target.GetComponent<Rigidbody2D>() : null;
            foreach (var t in new[] { ItemType.ThunderStaff, ItemType.LightningStrike })
            {
                inv.Slots[0] = ItemStack.Of(t); inv.Selected = 0;
                mod.Stats.ManaCrystals = Player.TerraStats.MaxManaCrystals;
                mod.Stats.UpdateMaximums(PlayerData.instance, 0); mod.Stats.Mana = mod.Stats.ManaMax;
                var kind = inv.Current.Def.Shoot;
                int hits = TerraProjectiles.DevHit(kind), before = mod.Stats.Mana;
                if (target != null) { target.hp = 99999; Pin(target, targetRb, at); }
                mod.DevUseSelected(at);
                bool paid = mod.Stats.Mana < before;
                for (float t0 = 0; t0 < .6f; t0 += Time.deltaTime)
                { if (target != null) Pin(target, targetRb, at); yield return null; }
                Check("法杖 " + t, paid && TerraProjectiles.DevHit(kind) > hits, "实际消耗魔力并命中测试目标");
                TerraProjectiles.Instance.DevClear();
            }

            inv.Slots[0] = ItemStack.Of(ItemType.HallowJoustingLance); mod.DevHold = true;
            mod.DevAim = (Vector2)hero.transform.position + Vector2.right * 3;
            mod.DevUseSelected(mod.DevAim.Value);
            yield return new WaitForSeconds(.6f);
            Check("骑枪按住维持", TerraProjectiles.Instance.DevCount(ProjKind.HallowLance) == 1);
            mod.DevHold = false;
            // Let go, it draws back over the last third of the use (8 ticks) before it's gone.
            yield return new WaitForSeconds(.3f);
            Check("骑枪松开回收", TerraProjectiles.Instance.DevCount(ProjKind.HallowLance) == 0);
            mod.DevAim = null; mod.DevHold = null;
            inv.Mouse = default;
            // Positive environmental recipes through their ordinary scene and station gates.
            foreach (var environment in new[] { CraftEnvironment.Graveyard, CraftEnvironment.Honey })
            {
                string destination = environment == CraftEnvironment.Graveyard ? "RestingGrounds_05" : "Hive_01";
                yield return GoTo(destination, EntryGateOf(destination));
                yield return new WaitForSeconds(1f);
                hero = HeroController.instance;
                if (environment == CraftEnvironment.Graveyard)
                {
                    var c = CellGrid.WorldToCell((Vector2)hero.transform.position + Vector2.right * 2);
                    for (int x = c.x - 2; x <= c.x + 2; x++)
                        for (int y = c.y - 1; y <= c.y + 4; y++) TerraWorld.Set(x, y, y == c.y - 1 ? CellKind.Stone : CellKind.Empty);
                    yield return new WaitForSeconds(.5f);
                    bool placed = Stations.TryPlace(StationKind.TinkerersWorkshop, hero, new Vector2((c.x + .5f) * CellGrid.CellSize, (c.y + .25f) * CellGrid.CellSize));
                    Check("墓地放置工匠作坊", placed, Stations.DevWhy ?? "成功");
                }
                InventoryGui.Open = true;
                foreach (var r in added.Where(r => r.Environment == environment))
                    Check("环境中制作 " + r.Result, Craft(r), destination);
            }
            InventoryGui.Open = false;
            Shop.DevStage = -1;
            Plugin.Log.LogInfo($"[AutoTest] 合成补全 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
