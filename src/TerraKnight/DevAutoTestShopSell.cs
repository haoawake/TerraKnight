using System.Collections;
using UnityEngine;
using TerraKnight.Items;
using TerraKnight.Town;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "shopsell"): the Merchant's shop open, and sales made the way a player makes them, with
    /// the pointer driven onto the slots and clicked: the cursor's stack onto the first empty shop slot
    /// (right after the stock) and onto the one after that, Ctrl+click and Shift+click on inventory slots;
    /// what the topmost click region was at each spot, and where the stack ended up.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator ShopSellTest()
        {
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            PlayerData.instance.isInvincible = true;
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            bool allInfinite = TerraMod.DevAllInfinite;
            var pos = TownNpcs.DevPos("Merchant");
            if (!pos.HasValue) { Plugin.Log.LogInfo("[AutoTest] 出售 商人不在德特茅斯 不通过"); yield break; }
            float bottom = hero.GetComponent<BoxCollider2D>().bounds.min.y - hero.transform.position.y;
            hero.transform.position = Clear(hero, new Vector2(pos.Value.x - 1.5f, pos.Value.y - 20f / 32f - bottom + 0.05f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            TownNpcs.TryRightClick(TownNpcs.DevPos("Merchant").Value);
            yield return new WaitForSeconds(0.3f);
            TownNpcs.OpenShop();
            yield return new WaitForSeconds(0.6f);
            int ok = 0, all = 0;
            {
                all++;
                string bad = TownNpcs.DevBadIcons(out int icons);
                if (bad.Length == 0) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 出售 所有商店的 {icons} 件货里图标读不出来的：{(bad.Length == 0 ? "没有" : bad)} {(bad.Length == 0 ? "通过" : "不通过")}");
            }

            // Every slot of the late shop (the save's whole progress: the Merchant's full stock): what a click on its middle meets.
            {
                all++;
                int stock = 0, wrong = 0;
                var sb = new System.Text.StringBuilder();
                for (int i = 0; i < TownNpcs.ShopItems.Length; i++)
                {
                    if (!TownNpcs.ShopItems[i].IsEmpty) stock++;
                    var r = InventoryGui.DevRectOf("ShopSlot", i);
                    string top = r.HasValue ? InventoryGui.DevHitAt(r.Value.center) : "没画";
                    if (top != "ShopSlot#" + i) { wrong++; sb.Append($" 第{i}格→{top}"); }
                }
                if (wrong == 0) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 出售 后期商店 {stock} 件货，40 格里点击区域不对的 {wrong} 格{sb} {(wrong == 0 ? "通过" : "不通过")}");
                // A sale onto the slot right after the stock (the start of a new row when the stock fills rows).
                all++;
                int next = -1;
                for (int i = 0; i < TownNpcs.ShopItems.Length; i++) if (TownNpcs.ShopItems[i].IsEmpty) { next = i; break; }
                var nr = InventoryGui.DevRectOf("ShopSlot", next);
                UI.TerraCursor.DevDrives = true;
                inv.Mouse = ItemStack.Of(ItemType.Gel, 5);
                if (nr.HasValue)
                {
                    UI.TerraCursor.DevAt = nr.Value.center;
                    yield return null;
                    yield return null;
                    UI.TerraCursor.DevClick();
                    yield return null;
                    yield return null;
                    yield return null;
                }
                bool soldLate = inv.Mouse.IsEmpty && TownNpcs.BuyBack[next] && TownNpcs.ShopItems[next].Type == ItemType.Gel;
                if (soldLate) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 出售 后期商店 光标上的 Gel 点在第 {next} 格（第 {next / 10 + 1} 行第 {next % 10 + 1} 个）：光标{(inv.Mouse.IsEmpty ? "空了" : "还拿着")}，那格现在 {TownNpcs.ShopItems[next].Type} {(soldLate ? "通过" : "不通过")}");
                Shot("Town", "shopsell_late");
                yield return null;
                inv.Mouse = default;
                UI.TerraCursor.DevDrives = false;
                UI.TerraCursor.DevAt = null;
            }

            // The early shop (no progress: what a new save sells) for the rest.
            TownNpcs.Close(true);
            InventoryGui.Open = false;
            TerraMod.DevAllInfinite = false;
            mod.ApplyCreative(false);
            yield return new WaitForSeconds(0.4f);
            TownNpcs.TryRightClick(TownNpcs.DevPos("Merchant").Value);
            yield return new WaitForSeconds(0.3f);
            TownNpcs.OpenShop();
            yield return new WaitForSeconds(0.6f);
            UI.TerraCursor.DevDrives = true;

            IEnumerator ClickAt(Vector2 at)
            {
                UI.TerraCursor.DevAt = at;
                yield return null;
                yield return null;
                UI.TerraCursor.DevClick();
                yield return null;
                yield return null;
                yield return null;
            }
            int FirstEmpty()
            {
                for (int i = 0; i < TownNpcs.ShopItems.Length; i++) if (TownNpcs.ShopItems[i].IsEmpty) return i;
                return -1;
            }

            // 1, 2. The cursor's stack dropped on the first empty slot, then on the one after it.
            foreach (var (type, gap) in new[] { (ItemType.Gel, 0), (ItemType.Wood, 1) })
            {
                all++;
                int target = FirstEmpty() + gap;
                inv.Mouse = ItemStack.Of(type, 5);
                var rect = InventoryGui.DevRectOf("ShopSlot", target);
                if (rect == null) { Plugin.Log.LogInfo($"[AutoTest] 出售 商店第 {target} 格没画出来 不通过"); continue; }
                string top = InventoryGui.DevHitAt(rect.Value.center);
                yield return ClickAt(rect.Value.center);
                int where = -1;
                for (int i = 0; i < TownNpcs.ShopItems.Length; i++) if (TownNpcs.BuyBack[i] && TownNpcs.ShopItems[i].Type == type) where = i;
                bool pass = inv.Mouse.IsEmpty && where >= 0;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 出售 光标上的 {type}×5 点在商店第 {target} 格（{(gap == 0 ? "已有商品的下一格" : "再下一格")}，那里最上层的点击区域 {top}）：光标{(inv.Mouse.IsEmpty ? "空了" : "还拿着")}，进了商店第 {where} 格 {(pass ? "通过" : "不通过")}");
                inv.Mouse = default;
            }

            // 3, 4. Ctrl+click and Shift+click on an inventory slot.
            foreach (var (type, mod2) in new[] { (ItemType.Torch, "Ctrl"), (ItemType.Mushroom, "Shift") })
            {
                all++;
                int slotIndex = Inventory.HotbarSize + 12;
                inv.Slots[slotIndex] = ItemStack.Of(type, 3);
                yield return null;
                var rect = InventoryGui.DevRectOf("Slot", slotIndex);
                if (rect == null) { Plugin.Log.LogInfo($"[AutoTest] 出售 背包第 {slotIndex} 格没画出来 不通过"); continue; }
                string top = InventoryGui.DevHitAt(rect.Value.center);
                InventoryGui.DevCtrl = mod2 == "Ctrl";
                InventoryGui.DevShift = mod2 == "Shift";
                yield return ClickAt(rect.Value.center);
                InventoryGui.DevCtrl = InventoryGui.DevShift = false;
                int where = -1;
                for (int i = 0; i < TownNpcs.ShopItems.Length; i++) if (TownNpcs.BuyBack[i] && TownNpcs.ShopItems[i].Type == type) where = i;
                bool pass = inv.Slots[slotIndex].IsEmpty && inv.Mouse.IsEmpty && where >= 0;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 出售 {mod2}+左键 背包第 {slotIndex} 格的 {type}×3（那里最上层的点击区域 {top}）：格子{(inv.Slots[slotIndex].IsEmpty ? "空了" : "还在")}，光标{(inv.Mouse.IsEmpty ? "空" : "拿着 " + inv.Mouse.Type)}，进了商店第 {where} 格 {(pass ? "通过" : "不通过")}");
                inv.Slots[slotIndex] = default;
                inv.Mouse = default;
            }
            Shot("Town", "shopsell");
            yield return null;
            UI.TerraCursor.DevDrives = false;
            UI.TerraCursor.DevAt = null;
            TownNpcs.Close(true);
            InventoryGui.Open = false;
            TerraMod.DevAllInfinite = allInfinite;
            mod.ApplyCreative(false);
            Plugin.Log.LogInfo($"[AutoTest] 出售 合计 {ok}/{all}");
        }
    }
}
