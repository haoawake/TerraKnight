using System.Collections;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "flinx"): the Flinx gear from what drops, in a save that pays for crafting: in Dirtmouth with
    /// Flinx Fur, gold bars and cobwebs, every loadout offers the Silk and the Flinx Staff, and with the Silk the
    /// Flinx Fur Coat; in a free save every loadout offers every item.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator FlinxTest()
        {
            yield return GoTo("Town", "left1");
            PlayerData.instance.isInvincible = true;
            var hero = HeroController.instance;
            hero.transform.position = new Vector2(110f, 12f);
            yield return new WaitForSeconds(1f);
            var inv = mod.Inventory;
            bool allInfinite = TerraMod.DevAllInfinite;
            TerraMod.DevAllInfinite = false;
            mod.ApplyCreative(false);
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 小雪怪 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            var mine = new[] { ItemType.FlinxFur, ItemType.GoldBar, ItemType.Cobweb, ItemType.Silk, ItemType.FlinxStaff, ItemType.FlinxFurCoat };
            for (int i = 0; i < inv.Slots.Length; i++)
                if (!inv.Slots[i].IsEmpty && mine.Contains(inv.Slots[i].Type)) inv.Slots[i] = default;
            inv.Mouse = default;
            inv.Add(ItemType.FlinxFur, 14);
            inv.Add(ItemType.GoldBar, 18);
            inv.Add(ItemType.Cobweb, 70);
            string Names() => string.Join("、", InventoryGui.DevOffered().Where(t => mine.Contains(t)).Select(t => ItemDb.Get(t).Name));
            Check("能在德特茅斯做", Shop.Available(hero), $"长椅或德特茅斯 {Shop.Available(hero)}，已解锁阶段 {Shop.UnlockedStage()}，免费 {Shop.Free}");
            foreach (int loadout in new[] { 0, 1, 2, 3 })
            {
                inv.SwitchLoadout(loadout);
                yield return null;
                var offered = InventoryGui.DevOffered();
                Check($"配装 {loadout + 1}（{inv.LoadoutKit}）只有材料时", offered.Contains(ItemType.Silk) && offered.Contains(ItemType.FlinxStaff) && !offered.Contains(ItemType.FlinxFurCoat),
                    $"制作栏里：{Names()}；{InventoryGui.DevRecipeKits()}");
            }
            inv.Add(ItemType.Silk, 10);
            foreach (int loadout in new[] { 0, 3 })
            {
                inv.SwitchLoadout(loadout);
                yield return null;
                var withSilk = InventoryGui.DevOffered();
                Check($"配装 {loadout + 1}（{inv.LoadoutKit}）加了丝绸", withSilk.Contains(ItemType.FlinxFurCoat) && withSilk.Contains(ItemType.FlinxStaff), $"制作栏里：{Names()}");
            }
            var coat = Recipes.For(ItemType.FlinxFurCoat);
            var staff = Recipes.For(ItemType.FlinxStaff);
            var silk = Recipes.For(ItemType.Silk);
            Plugin.Log.LogInfo($"[AutoTest] 小雪怪 配方：外套 {coat?.Station} {string.Join(" + ", coat.Ingredients.Select(g => $"{ItemDb.Get(g.Type).Name}×{g.Count}"))}；法杖 {staff?.Station} {string.Join(" + ", staff.Ingredients.Select(g => $"{ItemDb.Get(g.Type).Name}×{g.Count}"))}；丝绸 {silk?.Station} {string.Join(" + ", silk.Ingredients.Select(g => $"{ItemDb.Get(g.Type).Name}×{g.Count}"))}");
            for (int i = 0; i < inv.Slots.Length; i++)
                if (!inv.Slots[i].IsEmpty && mine.Contains(inv.Slots[i].Type)) inv.Slots[i] = default;
            // A free save: every loadout offers the whole catalogue.
            TerraMod.DevAllInfinite = true;
            mod.ApplyCreative(false);
            int everything = Shop.Everything.Count();
            foreach (int loadout in new[] { 0, 1, 2, 3 })
            {
                inv.SwitchLoadout(loadout);
                yield return null;
                int offered = InventoryGui.DevOffered().Count;
                Check($"免费存档 配装 {loadout + 1}（{inv.LoadoutKit}）", offered == everything, $"制作栏 {offered} 个，全部物品 {everything} 个");
            }
            inv.SwitchLoadout(0);
            TerraMod.DevAllInfinite = allInfinite;
            mod.ApplyCreative(false);
            Plugin.Log.LogInfo($"[AutoTest] 小雪怪 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
