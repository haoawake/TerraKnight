using System.Collections;
using System.Linq;
using UnityEngine;
using TerraKnight.Items;
using TerraKnight.World;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "craftchest"): a recipe made by hand with its ingredients only in a chest beside the
    /// player: offered, made, and the ingredients taken out of the chest; with one ingredient in the
    /// inventory as well, the inventory's goes first; 12 tiles away the chest no longer counts.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator CraftChestTest()
        {
            yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            yield return new WaitForSeconds(1.5f);
            PlayerData.instance.isInvincible = true;
            var hero = HeroController.instance;
            var inv = TerraMod.Instance.Inventory;
            // Crafting that costs: not the test save's free one.
            bool allInfinite = TerraMod.DevAllInfinite;
            TerraMod.DevAllInfinite = false;
            mod.ApplyCreative(false);
            var kit = inv.LoadoutKit;
            // A recipe made by hand of two stackable ingredients, of the general kit or the one worn.
            var r = Recipes.All.FirstOrDefault(x => !x.IsBought && x.Station == "徒手" && x.Ingredients.Length == 2 && x.Def.MaxStack > 1
                                                     && (x.Def.Kit == Kit.General || x.Def.Kit == kit)
                                                     && x.Ingredients.All(g => ItemDb.Get(g.Type).MaxStack >= g.Count * 3));
            if (r == null) { Plugin.Log.LogInfo("[AutoTest] 附近箱子合成 找不到合适的徒手配方 不通过"); yield break; }
            var (a, na) = r.Ingredients[0];
            var (b, nb) = r.Ingredients[1];
            // Nothing of them carried.
            for (int i = 0; i < inv.Slots.Length; i++)
                if (!inv.Slots[i].IsEmpty && (inv.Slots[i].Type == a || inv.Slots[i].Type == b || inv.Slots[i].Type == r.Result)) inv.Slots[i] = default;
            inv.Mouse = default;
            float settle = Time.time + 3f;
            while (!hero.cState.onGround && Time.time < settle) yield return null;
            var feet = hero.GetComponent<BoxCollider2D>().bounds;
            bool placed = false;
            foreach (float dx in new[] { 1.2f, -1.2f, 2f, -2f, 2.8f, -2.8f })
                if (Chests.TryPlace(hero, new Vector2(feet.center.x + dx, feet.min.y + 0.3f))) { placed = true; break; }
            Chests.DevOpenNear(feet.center);
            var items = Chests.OpenItems;
            if (!placed || items == null) { Plugin.Log.LogInfo($"[AutoTest] 附近箱子合成 放箱子 {placed}，打开 {items != null} 不通过"); yield break; }
            for (int i = 0; i < items.Length; i++) items[i] = default;
            items[0] = ItemStack.Of(a, na * 3);
            items[1] = ItemStack.Of(b, nb * 3);
            Chests.Close(false);
            InventoryGui.Open = true;
            yield return null;
            int ChestHas(ItemType t) => items.Where(s => !s.IsEmpty && s.Type == t).Sum(s => s.Count);

            // 1. Only in the chest: offered and made from it.
            bool offered = InventoryGui.DevOffered().Contains(r.Result);
            bool made = InventoryGui.DevCraft(r.Result);
            int chestA1 = ChestHas(a), chestB1 = ChestHas(b);
            bool step1 = offered && made && chestA1 == na * 2 && chestB1 == nb * 2;
            Plugin.Log.LogInfo($"[AutoTest] 附近箱子合成 1 只在箱子里：{r.Result}（{a}×{na} + {b}×{nb}）列出 {offered}，做出 {made}，箱子里剩 {a}×{chestA1}、{b}×{chestB1} {(step1 ? "通过" : "不通过")}");
            Shot("Crossroads_01", "craftchest");
            yield return null;

            // 2. One ingredient carried as well: the inventory's goes first.
            inv.Mouse = default;
            int slot = Inventory.HotbarSize + 3;
            inv.Slots[slot] = ItemStack.Of(a, na);
            bool made2 = InventoryGui.DevCraft(r.Result);
            int carriedA = Recipes.Count(inv, a), chestA2 = ChestHas(a), chestB2 = ChestHas(b);
            bool step2 = made2 && carriedA == 0 && chestA2 == na * 2 && chestB2 == nb;
            Plugin.Log.LogInfo($"[AutoTest] 附近箱子合成 2 背包也有一份 {a}：做出 {made2}，背包剩 {carriedA}，箱子里剩 {a}×{chestA2}、{b}×{chestB2} {(step2 ? "通过" : "不通过")}");

            // 3. Twelve tiles away: the chest doesn't count.
            inv.Mouse = default;
            InventoryGui.Open = false;
            var from = hero.transform.position;
            var far = Clear(hero, (Vector2)from + new Vector2(12f * CellGrid.CellSize + 1f, 0.5f));
            hero.transform.position = far;
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.5f);
            InventoryGui.Open = true;
            yield return null;
            int nearFar = Chests.ForCrafting(hero).Count;
            bool offeredFar = InventoryGui.DevOffered().Contains(r.Result);
            int nearHere = 0;
            hero.transform.position = from;
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.3f);
            nearHere = Chests.ForCrafting(hero).Count;
            bool step3 = nearFar == 0 && !offeredFar && nearHere >= 1;
            Plugin.Log.LogInfo($"[AutoTest] 附近箱子合成 3 走开 12 格（{Vector2.Distance(from, far):F1}）：附近箱子 {nearFar} 个，列出 {offeredFar}；回来后附近箱子 {nearHere} 个 {(step3 ? "通过" : "不通过")}");

            // 4. The chest open: its column still shown (Terraria's), the open chest's items counted.
            inv.Mouse = default;
            items[0] = ItemStack.Of(a, na * 3);
            items[1] = ItemStack.Of(b, nb * 3);
            Chests.DevOpenNear(hero.GetComponent<BoxCollider2D>().bounds.center);
            InventoryGui.Open = true;
            yield return null;
            yield return null;
            bool offeredOpen = InventoryGui.DevOffered().Contains(r.Result);
            bool shown = InventoryGui.DevCraftingDrawn;
            bool madeOpen = InventoryGui.DevCraft(r.Result);
            bool step4 = Chests.IsOpen && offeredOpen && shown && madeOpen;
            Plugin.Log.LogInfo($"[AutoTest] 附近箱子合成 4 开着箱子：开着 {Chests.IsOpen}，列出 {offeredOpen}，合成栏画出 {shown}，做出 {madeOpen} {(step4 ? "通过" : "不通过")}");
            Shot("Crossroads_01", "craftchest_open");
            Chests.Close(false);
            InventoryGui.Open = false;
            inv.Mouse = default;

            // 5. Recalled home and back: the chest counts again.
            string gate = EntryGateOf("Crossroads_01");
            bool recalled = Pylons.Recall();
            float until = Time.time + 20f;
            while (Time.time < until && (GameManager.instance.sceneName == "Crossroads_01" || GameManager.instance.IsInSceneTransition || !GameHooks.PlayerHasControl())) yield return null;
            string home = GameManager.instance.sceneName;
            yield return new WaitForSeconds(1f);
            yield return GoTo("Crossroads_01", gate);
            hero = HeroController.instance;
            hero.transform.position = from;
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            InventoryGui.Open = true;
            yield return null;
            int nearBack = Chests.ForCrafting(hero).Count;
            bool offeredBack = InventoryGui.DevOffered().Contains(r.Result);
            bool step5 = nearBack >= 1 && offeredBack;
            Plugin.Log.LogInfo($"[AutoTest] 附近箱子合成 5 回城（{recalled}，到了 {home}）再回来：附近箱子 {nearBack} 个，列出 {offeredBack}，{Chests.DevReport()} {(step5 ? "通过" : "不通过")}");

            InventoryGui.Open = false;
            inv.Mouse = default;
            for (int i = 0; i < items.Length; i++) items[i] = default;
            TerraMod.DevAllInfinite = allInfinite;
            mod.ApplyCreative(false);
            int passed = (step1 ? 1 : 0) + (step2 ? 1 : 0) + (step3 ? 1 : 0) + (step4 ? 1 : 0) + (step5 ? 1 : 0);
            Plugin.Log.LogInfo($"[AutoTest] 附近箱子合成 合计 {passed}/5 {(passed == 5 ? "通过" : "不通过")}");
        }
    }
}
