using System;
using System.Collections;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator WardrobeTest()
        {
            PlayerData.instance.isInvincible = true;
            Player.TerraStats.DevImmortal = true;
            mod.Stats.FullHeal();
            yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            var hero = HeroController.instance;
            hero.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
            var inventory = mod.Inventory;
            int total = 0, passed = 0;
            void Check(string name, bool good)
            {
                total++; if (good) passed++;
                Plugin.Log.LogInfo($"[AutoTest] 衣柜 {name} {(good ? "通过" : "不通过")}");
            }
            inventory.Clear(); inventory.Infinite = false; Chests.Clear();
            for (int i = 0; i < Inventory.Size; i++) inventory.Slots[i] = ItemStack.Of(ItemType.StoneBlock, 9999);
            Check("未建衣柜、背包满仍收纳", inventory.Pickup(ItemType.TopHat, 1) == 0 && inventory.WardrobeItems.Contains(ItemType.TopHat) && inventory.Slots.Take(Inventory.Size).All(s => s.Type == ItemType.StoneBlock));
            long before = Shop.Purse;
            long sale = Shop.SellValue(ItemStack.Of(ItemType.TopHat));
            inventory.Pickup(ItemType.TopHat, 2);
            Check("重复两件按原售价出售", Shop.Purse - before == sale * 2);
            Array.Clear(inventory.Slots, 0, inventory.Slots.Length);
            var clothes = ItemDb.All.Where(d => d.Vanity).ToArray();
            foreach (var d in clothes)
            {
                bool add = inventory.Add(d.Type);
                var copy = Wardrobe.Copy(inventory, d.Type);
                Check("收纳/无限取用/无售价/保存/联机标记 " + d.Type,
                    add && inventory.WardrobeItems.Contains(d.Type) && copy.WardrobeCopy && Shop.SellValue(copy) == 0
                    && Save.ModSave.DevRoundTrip(copy).WardrobeCopy && Net.WorldSync.Stack.From(copy).Item().WardrobeCopy
                    && !Shimmer.CanShimmer(copy) && Wardrobe.Copy(inventory, d.Type).Count == 1);
            }
            Check("全部时装有套装格", clothes.All(d => Wardrobe.Outfits.Any(o => o.Items.Contains(d.Type))));
            Check("地区制作套装完整", Enumerable.Range(0, AreaBlocks.All.Length).All(a => Wardrobe.Outfits.Any(o => o.Regions.Contains(a) && o.Parts.All(t => t != ItemType.None && ItemDb.Get(t).Region == a + 1))));
            Check("单件保留头身脚空格", Wardrobe.Outfits.Any(o => o.Parts[0] == ItemType.SummerHat && o.Parts[1] == ItemType.None && o.Parts[2] == ItemType.None));
            var free = Wardrobe.Copy(inventory, ItemType.TopHat);
            before = Shop.Purse;
            Check("副本重新入包无收益", inventory.Pickup(free) == 0 && Shop.Purse == before && inventory.Slots.Any(s => s.Type == free.Type && s.WardrobeCopy));
            Check("副本不作为制作材料", Recipes.Count(inventory, ItemType.TopHat) == 0);
            inventory.Mouse = free;
            before = Shop.Purse;
            inventory.ReturnMouseItem();
            Check("收起鼠标副本不出售", inventory.Mouse.IsEmpty && Shop.Purse == before);
            Town.TownNpcs.GuideItem = free;
            Town.TownNpcs.ReturnGuideItem();
            Check("向导归还副本不出售", Shop.Purse == before && Town.TownNpcs.GuideItem.IsEmpty);
            inventory.Mouse = default;
            InventoryGui.DevTakeWardrobe(ItemType.TopHat);
            Check("界面取用不消耗收藏", inventory.Mouse.WardrobeCopy && inventory.WardrobeItems.Contains(ItemType.TopHat));
            inventory.Mouse = default;
            InventoryGui.DevTakeWardrobe(ItemType.TopHat, true);
            Check("界面右键穿戴", inventory.VanityArmor[0].Type == ItemType.TopHat && inventory.VanityArmor[0].WardrobeCopy);
            inventory.Bank[0] = free;
            Save.ModSave.Write(slot, mod);
            int collectionCount = inventory.WardrobeItems.Count;
            inventory.WardrobeItems.Clear(); inventory.Bank[0] = default;
            Save.ModSave.Read(slot, mod);
            Check("收藏和副本真正写盘重载", inventory.WardrobeItems.Count == collectionCount && inventory.Bank[0].WardrobeCopy && inventory.VanityArmor[0].WardrobeCopy);
            inventory.Slots[0] = ItemStack.Of(ItemType.RuneHat);
            inventory.VanityArmor[1] = ItemStack.Of(ItemType.RuneRobe);
            Wardrobe.Migrate(inventory);
            Check("旧包内衣服收纳、已穿衣服保留", inventory.Slots[0].IsEmpty && inventory.WardrobeItems.Contains(ItemType.RuneHat) && inventory.VanityArmor[1].WardrobeCopy);

            // Use all 24 real dresser styles, their placement and their ordinary right-click path.
            const int gx = 62, fy = 22;
            float c = CellGrid.CellSize;
            for (int x = gx - 8; x <= gx + 9; x++) for (int y = fy - 2; y <= fy + 10; y++) TerraWorld.Set(x, y, CellKind.Empty);
            for (int x = gx - 8; x <= gx + 9; x++) TerraWorld.Set(x, fy, CellKind.Stone);
            hero.transform.position = new Vector3((gx - 1) * c, (fy + 4) * c, hero.transform.position.z);
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(.5f);
            var loomAim = new Vector2((gx + 3) * c, (fy + 1) * c + .1f);
            Check("放置织布机", Stations.TryPlace(StationKind.Loom, hero, loomAim));
            yield return new WaitForSeconds(.3f);
            bool freeMaking = inventory.FreeMaking;
            inventory.FreeMaking = false;
            var crafted = ItemType.RegionVanityKingsPassHead;
            inventory.WardrobeItems.Remove(crafted);
            inventory.Slots[1] = ItemStack.Of(AreaBlocks.BlockItem(0), 100);
            inventory.Mouse = default;
            InventoryGui.Open = true;
            Check("实际制作消耗材料直接收纳", InventoryGui.DevCraft(crafted) && inventory.WardrobeItems.Contains(crafted) && inventory.Mouse.IsEmpty && inventory.Slots[1].Count == 85);
            before = Shop.Purse;
            Check("再次制作自动出售", InventoryGui.DevCraft(crafted) && inventory.Mouse.IsEmpty && inventory.Slots[1].Count == 70 && Shop.Purse - before == Shop.SellValue(ItemStack.Of(crafted)));
            inventory.FreeMaking = freeMaking;
            Stations.Clear();
            for (int a = 0; a < AreaBlocks.All.Length; a++)
            {
                Chests.Clear(); InventoryGui.Open = false;
                var aim = new Vector2((gx + 3) * c, (fy + 1) * c + .1f);
                bool placed = Chests.TryPlace(hero, aim, RegionalFurniture.DresserStyle(a));
                yield return new WaitForSeconds(.06f);
                var box = new Rect((gx + 1.5f) * c, (fy + 1) * c, 3 * c, 2 * c);
                bool click = placed && Chests.TryRightClick(box.center);
                Check(AreaBlocks.All[a].Key + "实际放置/右键全局收藏", click && InventoryGui.WardrobeOpen && !Chests.IsOpen && inventory.WardrobeItems.Count == collectionCount);
                InventoryGui.Open = false;
                bool candidate = Chests.SmartCandidate(hero, box.center, out _, out _);
                Chests.SmartWin(candidate);
                Check(AreaBlocks.All[a].Key + "智能交互打开衣柜", candidate && Chests.TrySmartOpen() && InventoryGui.WardrobeOpen && !Chests.IsOpen);
                InventoryGui.DevShift = true;
                bool drawers = Chests.TryRightClick(box.center);
                InventoryGui.DevShift = false;
                Check(AreaBlocks.All[a].Key + "旧抽屉入口保留", drawers && !InventoryGui.WardrobeOpen && Chests.OpenItems?.Length == 40);
            }
            InventoryGui.OpenWardrobe();
            yield return new WaitForSeconds(3f);
            Check("原生格子贴图", Terraria.TerrariaAssets.Texture("Inventory_Back") != null);
            Check("界面命中区域", InventoryGui.DevWardrobeRect.width > 0 && InventoryGui.DevRectOf("WardrobeFilter", 4).HasValue);
            Shot("Crossroads_01", "wardrobe_all");
            yield return new WaitForSeconds(.5f);
            InventoryGui.DevFilterWardrobe(4);
            yield return new WaitForSeconds(.3f);
            Check("按苍绿之径掉落筛选", InventoryGui.DevWardrobeVisible().Count > 0 && InventoryGui.DevWardrobeVisible().All(o => o.Regions.Contains(4)));
            Shot("Crossroads_01", "wardrobe_greenpath");
            yield return new WaitForSeconds(.5f);
            var lockedPreview = inventory.WardrobeItems.ToArray();
            inventory.WardrobeItems.Clear();
            yield return new WaitForSeconds(.3f);
            Shot("Crossroads_01", "wardrobe_uncollected");
            yield return new WaitForSeconds(.5f);
            foreach (var type in lockedPreview) inventory.WardrobeItems.Add(type);
            hero.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeRotation;
            Plugin.Log.LogInfo($"[AutoTest] 衣柜 合计 {passed}/{total}");
        }
    }
}
