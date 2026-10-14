using System.Collections;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using UnityEngine;
using TerraKnight.Items;
using TerraKnight.World;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "batch5"): the misc equipment page and the hook in it, the Terraria keys coming
    /// back after the game's own menu clears them, crafts of worthless things, the ores and the
    /// new metal sets, Geo as an item (the game's geo kept equal, the shop, the Piggy Bank), the
    /// chest's sort and restock and quick stacking to nearby chests, and boss rooms left as they are.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        static void Check(string what, bool ok, string detail) => Plugin.Log.LogInfo($"[AutoTest] 批5 {what}：{detail}{(ok ? " 通过" : " 不通过")}");

        IEnumerator Batch5Test()
        {
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            mod.DevNoFallDamage = true;
            var inv = mod.Inventory;
            var hero = HeroController.instance;

            // --- The hook in its slot of the misc equipment page.
            var hookNow = Player.GrapplingHook.HookItem(inv);
            Check("钩爪格", !inv.Misc[Inventory.HookSlot].IsEmpty && hookNow == ItemType.GrapplingHook, $"钩爪格 {inv.Misc[Inventory.HookSlot].Type}，按 E 用的 {hookNow}");
            AccessTools.Field(typeof(InventoryGui), "equipPage").SetValue(null, 2);
            InventoryGui.Open = true;
            yield return new WaitForSeconds(0.4f);
            Shot(GameManager.instance.sceneName, "batch5_misc_page");
            yield return null;
            AccessTools.Field(typeof(InventoryGui), "equipPage").SetValue(null, 0);
            yield return new WaitForSeconds(0.2f);
            Shot(GameManager.instance.sceneName, "batch5_inventory_buttons");
            yield return null;
            InventoryGui.Open = false;

            // --- The game's keyboard menu clearing "left" and putting its arrow back.
            var actions = InputHandler.Instance.inputActions;
            actions.left.ClearBindings();
            actions.left.AddBinding(new InControl.KeyBindingSource(InControl.Key.LeftArrow));
            yield return null;
            yield return null;
            bool hasA = actions.left.HasBinding(new InControl.KeyBindingSource(InControl.Key.A));
            Check("键位", hasA, $"原版清掉“左”的绑定后，A 键{(hasA ? "自己补回来了" : "没补回来")}，方向键 {actions.left.HasBinding(new InControl.KeyBindingSource(InControl.Key.LeftArrow))}");

            // --- Worthless crafts.
            long bench = Shop.SellValue(ItemStack.Of(ItemType.WorkBench)), helmet = Shop.SellValue(ItemStack.Of(ItemType.WoodHelmet)), torch = Shop.SellValue(ItemStack.Of(ItemType.Torch)), furnace = Shop.SellValue(ItemStack.Of(ItemType.Furnace));
            Check("无价值合成", bench == 0 && helmet == 0 && torch > 0, $"工作台 {bench}、木头盔 {helmet}、火把 {torch}、熔炉 {furnace}（铜）");

            // --- Ores.
            var copper = Recipes.For(ItemType.CopperBar);
            var hell = Recipes.For(ItemType.HellstoneBar);
            string Ing(Recipe r) { if (r == null) return "无"; var sb = new StringBuilder(r.Station + "："); foreach (var (t, n) in r.Ingredients) sb.Append($"{t}×{n} "); return sb.ToString(); }
            int barDrops = 0;
            foreach (var rec in EnemyDrops.DevRecords) foreach (var d in EnemyDrops.Of(rec)) if (ItemDb.OreOf(d.Type, out _)) barDrops++;
            Check("矿石", EnemyDrops.DevBarsToOre > 0 && barDrops == 0 && copper != null && copper.Ingredients[0].Type == ItemType.CopperOre,
                $"掉落表里改成矿的 {EnemyDrops.DevBarsToOre} 处，还掉锭的 {barDrops} 处；铜锭 {Ing(copper)}；狱石锭 {Ing(hell)}；铜矿 {ItemDb.Get(ItemType.CopperOre).Name} 价值 {ItemDb.Get(ItemType.CopperOre).Value}");
            var oldArmor = (ItemStack[])inv.Armor.Clone();
            var sets = new StringBuilder();
            bool setsOk = true;
            foreach (var (h, b, l, def) in new[] { (ItemType.TinHelmet, ItemType.TinChainmail, ItemType.TinGreaves, 2 + 2 + 1 + 2), (ItemType.SilverHelmet, ItemType.SilverChainmail, ItemType.SilverGreaves, 3 + 4 + 3 + 3), (ItemType.PlatinumHelmet, ItemType.PlatinumChainmail, ItemType.PlatinumGreaves, 5 + 6 + 5 + 4) })
            {
                inv.Armor[0] = ItemStack.Of(h); inv.Armor[1] = ItemStack.Of(b); inv.Armor[2] = ItemStack.Of(l);
                inv.RefreshEffective();
                yield return null;
                yield return null;
                int got = Equipment.Current.Defense;
                sets.Append($"{ItemDb.Get(h).Name}套 防御 {got}（应至少 {def}，套装 {Equipment.FullSet}） ");
                setsOk &= got >= def;
            }
            for (int i = 0; i < oldArmor.Length; i++) inv.Armor[i] = oldArmor[i];
            inv.RefreshEffective();
            Check("金属套装", setsOk, sets.ToString());

            // --- Geo as an item.
            long held0 = GeoWallet.Held(inv);
            hero.AddGeo(50);
            yield return null; yield return null;
            long held1 = GeoWallet.Held(inv);
            hero.TakeGeo(10);
            yield return null; yield return null;
            long held2 = GeoWallet.Held(inv);
            bool spent = Shop.Spend(2000);
            yield return null; yield return null;
            long held3 = GeoWallet.Held(inv);
            int pd3 = pd.geo;
            Shop.Earn(1234);
            yield return null; yield return null;
            long held4 = GeoWallet.Held(inv);
            Check("吉欧物品", held1 == held0 + 50 && held2 == held1 - 10 && spent && held3 == held2 - 20 && pd3 == held3 && held4 == held3 + 12,
                $"开始 {held0}；原版加 50 → {held1}；原版扣 10 → {held2}；商店花 20 → {held3}（原版吉欧 {pd3}）；收入 12.34 → {held4}，零头 {Shop.SpareCopper} 铜，最大叠 {ItemDb.Get(ItemType.Geo).MaxStack}");
            // Into the Piggy Bank: the bag empties, the game's geo follows, buying draws on the bank.
            for (int i = 0; i < inv.Slots.Length; i++)
                if (!inv.Slots[i].IsEmpty && inv.Slots[i].Type == ItemType.Geo) { inv.Bank[0] = inv.Slots[i]; inv.Slots[i] = default; }
            yield return null; yield return null;
            int pdAfterBank = pd.geo;
            long purse = Shop.Purse;
            bool paid = Shop.Spend(500);
            yield return null;
            Check("存钱罐", pdAfterBank == 0 && paid && GeoWallet.Banked(inv) == held4 - 5,
                $"钱放进存钱罐后原版吉欧 {pdAfterBank}，总钱 {purse} 铜；花 5 吉欧 {(paid ? "成功" : "失败")}，存钱罐剩 {GeoWallet.Banked(inv)}");
            inv.Slots[Inventory.HotbarSize + 5] = inv.Bank[0];
            inv.Bank[0] = default;

            // --- A chest: placed, sorted, restocked, quick stacked into.
            float settle = Time.time + 3f;
            while (!hero.cState.onGround && Time.time < settle) yield return null;
            var feet = hero.GetComponent<BoxCollider2D>().bounds;
            bool placed = false;
            foreach (float dx in new[] { 1.2f, -1.2f, 2f, -2f, 2.8f, -2.8f })
                if (Chests.TryPlace(hero, new Vector2(feet.center.x + dx, feet.min.y + 0.3f))) { placed = true; break; }
            Chests.DevOpenNear(feet.center);
            var items = Chests.OpenItems;
            if (!placed || items == null) Check("箱子", false, $"放箱子 {placed}，打开 {items != null}");
            else
            {
                items[0] = ItemStack.Of(ItemType.Wood, 30);
                items[1] = ItemStack.Of(ItemType.CopperBroadsword);
                items[2] = ItemStack.Of(ItemType.Wood, 50);
                items[3] = ItemStack.Of(ItemType.LesserHealingPotion, 3);
                items[5] = ItemStack.Of(ItemType.CopperPickaxe);
                Chests.SortOpen();
                var order = new StringBuilder();
                for (int i = 0; i < 6; i++) order.Append(items[i].IsEmpty ? "空 " : $"{items[i].Type}×{items[i].Count} ");
                bool sorted = items[0].Type == ItemType.CopperBroadsword && items[1].Type == ItemType.CopperPickaxe && items[4].IsEmpty;
                int woodSlot = Inventory.HotbarSize + 6;
                inv.Slots[woodSlot] = ItemStack.Of(ItemType.Wood, 5);
                Chests.Restock(inv);
                int woodAfter = inv.Slots[woodSlot].Count;
                // Some wood back in the chest, so it holds some to stack onto.
                items[9] = ItemStack.Of(ItemType.Wood, 3);
                Chests.Close(false);
                inv.Slots[woodSlot] = ItemStack.Of(ItemType.Wood, 7);
                int moved = Chests.QuickStackNearby(inv, feet.center);
                Check("箱子", sorted && woodAfter > 5 && moved > 0 && inv.Slots[woodSlot].IsEmpty, $"排序后 {order}；补货后背包木材 5 → {woodAfter}；快速堆叠到附近箱子 {moved} 堆，背包木材剩 {inv.Slots[woodSlot].Count}");
            }

            // --- Boss rooms: Gruz Mother's room left as it is, an ordinary room not.
            yield return GoTo("Crossroads_04", EntryGateOf("Crossroads_04"));
            yield return new WaitForSeconds(1f);
            var c = CellGrid.WorldToCell(hero.GetComponent<BoxCollider2D>().bounds.center);
            Vector2Int solid = c, empty = c;
            for (int dy = 0; dy < 20; dy++) if (TerraWorld.IsSolid(c.x, c.y - dy)) { solid = new Vector2Int(c.x, c.y - dy); break; }
            for (int dx = 2; dx < 10; dx++) if (!TerraWorld.IsSolid(c.x + dx, c.y)) { empty = new Vector2Int(c.x + dx, c.y); break; }
            bool dig = TerraWorld.CanDig(solid.x, solid.y), put = TerraMod.CanPlace(empty);
            Check("首领房间", !dig && !put && BossRooms.Locked(solid.x, solid.y), $"格鲁兹之母的房间：能挖 {dig}，能放 {put}，锁着 {BossRooms.Locked(solid.x, solid.y)}");
            yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            yield return new WaitForSeconds(1f);
            c = CellGrid.WorldToCell(hero.GetComponent<BoxCollider2D>().bounds.center);
            solid = c;
            for (int dy = 0; dy < 20; dy++) if (TerraWorld.IsSolid(c.x, c.y - dy)) { solid = new Vector2Int(c.x, c.y - dy); break; }
            Check("普通房间", TerraWorld.CanDig(solid.x, solid.y) && !BossRooms.Locked(solid.x, solid.y), $"遗忘十字路第一个房间：能挖 {TerraWorld.CanDig(solid.x, solid.y)}");
            Plugin.Log.LogInfo($"[AutoTest] 批5 首领房间名单：{BossRooms.DevRooms()}");
        }
    }
}
