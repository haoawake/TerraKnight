using System;
using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using TerraKnight.Items;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator FoodCarryTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            yield return GoTo("Town", "left1");
            mod.ApplyCreative(false); TerraMod.DevAllInfinite = false; mod.SetInfiniteItems(false);
            PlayerData.instance.isInvincible = true;
            Equip(Array.Empty<ItemType>(), Array.Empty<ItemType>());
            var inv = mod.Inventory;
            var foods = ItemDb.All.Where(d => d != null && PotionBuffs.FoodRank(d.BuffType) > 0).ToArray();
            int checks = 0, failures = 0;
            void Check(string name, bool pass) { checks++; if (!pass) failures++; Plugin.Log.LogInfo($"[AutoTest] FOOD_CARRY {name} {(pass ? "通过" : "不通过")}"); }
            void Clear() {
                mod.CancelUse(); mod.SetUseCooldown(0);
                Array.Clear(inv.Slots, 0, inv.Slots.Length); Array.Clear(inv.Bank, 0, inv.Bank.Length);
                foreach (var page in inv.PotionBox) Array.Clear(page, 0, page.Length);
                inv.Mouse = default; inv.Selected = 0; inv.Override = -1;
                PlayerBuffs.Clear(); Containers.Tick(mod);
            }
            int Tier() => PlayerBuffs.Has(207) ? 3 : PlayerBuffs.Has(206) ? 2 : PlayerBuffs.Has(26) ? 1 : 0;
            int NativeTier(ItemType type) => type == ItemType.GoldenDelight ? 3 : new[] { ItemType.JojaCola, ItemType.ShuckedOyster, ItemType.PumpkinPie, ItemType.CookedFish, ItemType.BlackenedFish }.Contains(type) ? 1 : 2;
            var aim = (Vector2)HeroController.instance.transform.position + Vector2.right * 2;
            foreach (var d in foods) {
                Clear(); yield return null;
                int tier = NativeTier(d.Type);
                Check("definition/" + d.Type, PotionBuffs.FoodRank(d.BuffType) == tier && d.Tooltip.Contains("同种食物"));
                inv.Slots[0] = ItemStack.Of(d.Type, 2);
                mod.DevUseSelected(aim);
                yield return new WaitForSeconds(.3f);
                var b = PotionBuffs.Bonuses();
                Check("consume-native-tier/" + d.Type, inv.Slots[0].Count == 1 && Tier() == tier && b.Defense == 1 + tier && Mathf.Abs(b.MeleeDamage - .025f * (1 + tier)) < .001f && PlayerBuffs.TimeLeft(d.BuffType) > d.BuffTime - 180);
                Clear(); inv.Slots[0] = ItemStack.Of(d.Type, 20);
                yield return new WaitForSeconds(.3f);
                Check("bag20/" + d.Type, Tier() == tier && PlayerBuffs.NoTime(d.BuffType) && inv.Slots[0].Count == 20);
                Clear(); inv.Bank[0] = ItemStack.Of(d.Type, 20);
                yield return new WaitForSeconds(.3f);
                Check("piggy20/" + d.Type, Tier() == tier && PlayerBuffs.NoTime(d.BuffType) && inv.Bank[0].Count == 20);
            }
            Clear(); inv.Slots[0] = ItemStack.Of(ItemType.CookedFish, 19);
            yield return new WaitForSeconds(.3f);
            Check("19-not-held", Tier() == 0 && Containers.HeldFoodBuff(inv) == 0);
            inv.Bank[0] = ItemStack.Of(ItemType.PumpkinPie, 1);
            yield return new WaitForSeconds(.3f);
            Check("different-types-do-not-combine-19-plus-1", Tier() == 0 && Containers.HeldFoodBuff(inv) == 0);
            inv.Bank[0] = ItemStack.Of(ItemType.CookedFish, 1);
            yield return new WaitForSeconds(.3f);
            Check("same-type-split-bag19-piggy1", Tier() == 1 && inv.Slots[0].Count == 19 && inv.Bank[0].Count == 1);
            Clear(); inv.Slots[0] = ItemStack.Of(ItemType.CookedFish, 20); inv.Bank[0] = ItemStack.Of(ItemType.SeafoodDinner, 20);
            yield return new WaitForSeconds(.3f);
            Check("best-of-two-qualified-types", Tier() == 2 && Containers.HeldFoodBuff(inv) == 206 && PotionBuffs.Bonuses().Defense == 3);
            inv.Bank[1] = ItemStack.Of(ItemType.GoldenDelight, 19);
            yield return new WaitForSeconds(.3f);
            Check("19-best-food-does-not-upgrade", Tier() == 2);
            inv.Bank[1].Count = 20;
            yield return new WaitForSeconds(.3f);
            Check("20-best-food-upgrades", Tier() == 3 && PotionBuffs.Bonuses().Defense == 4);
            inv.Bank[1].Count = 19;
            yield return new WaitForSeconds(.4f);
            Check("best-falls-back-to-medium", Tier() == 2 && PotionBuffs.Bonuses().Defense == 3);
            inv.Bank[0].Count = 19;
            yield return new WaitForSeconds(.4f);
            Check("medium-falls-back-to-low", Tier() == 1 && PotionBuffs.Bonuses().Defense == 2);
            inv.Slots[0].Count = 19;
            yield return new WaitForSeconds(.4f);
            Check("none-qualified-stops", Tier() == 0);
            Clear(); inv.Slots[0] = ItemStack.Of(ItemType.GoldenDelight, 2); inv.Bank[0] = ItemStack.Of(ItemType.CookedFish, 20);
            yield return new WaitForSeconds(.3f);
            mod.DevUseSelected(aim);
            yield return new WaitForSeconds(.3f);
            Check("manual-stronger-food-beats-held-low", inv.Slots[0].Count == 1 && Tier() == 3 && PotionBuffs.Bonuses().Defense == 4);
            inv.Bank[0] = default;
            yield return new WaitForSeconds(.4f);
            Check("manual-duration-survives-removing-held-food", Tier() == 3 && !PlayerBuffs.NoTime(207) && PlayerBuffs.TimeLeft(207) > 1000);
            Clear(); inv.Slots[0] = ItemStack.Of(ItemType.CookedFish, 2); inv.Bank[0] = ItemStack.Of(ItemType.GoldenDelight, 20);
            yield return new WaitForSeconds(.3f);
            mod.DevUseSelected(aim);
            yield return new WaitForSeconds(.3f);
            Check("manual-low-does-not-downgrade-held-best", Tier() == 3 && PlayerBuffs.TimeLeft(26) > 1000);
            inv.Bank[0] = default;
            yield return new WaitForSeconds(.4f);
            Check("held-best-removed-restores-timed-low", Tier() == 1 && !PlayerBuffs.NoTime(26) && PlayerBuffs.TimeLeft(26) > 1000);
            Clear(); inv.Slots[0] = ItemStack.Of(ItemType.Pumpkin, 20); inv.Bank[0] = ItemStack.Of(ItemType.Ale, 20);
            yield return new WaitForSeconds(.3f);
            Check("raw-ingredients-and-alcohol-excluded", Tier() == 0 && !PlayerBuffs.Has(PotionBuffs.Tipsy) && Containers.HeldFoodBuff(inv) == 0);
            Clear(); inv.Bank[0] = ItemStack.Of(ItemType.CookedShrimp, 20);
            yield return new WaitForSeconds(.3f);
            Save.ModSave.Write(GameManager.instance.profileID, mod);
            Array.Clear(inv.Bank, 0, inv.Bank.Length);
            Save.ModSave.Read(GameManager.instance.profileID, mod);
            yield return new WaitForSeconds(.3f);
            Check("saved-piggy-food-restores-native-tier", inv.Bank[0].Type == ItemType.CookedShrimp && inv.Bank[0].Count == 20 && Tier() == 2);
            Check("actual-equipment-native-tier", Equipment.Current.Defense >= 3 && Mathf.Abs(PotionBuffs.Bonuses().MeleeDamage - .075f) < .001f);
            InventoryGui.Open = true; InventoryGui.DevUtilityClick(4); InventoryGui.DevStatsTab(0);
            yield return new WaitForSeconds(.4f);
            Shot("Town", "food_carry_actual_stats");
            yield return new WaitForSeconds(.3f);
            InventoryGui.Open = false;
            File.WriteAllText(Path.Combine(DevCopy.SaveDir, "food-carry.json"), JsonConvert.SerializeObject(new { Checks = checks, Failures = failures, FoodCount = foods.Length, Foods = foods.Select(d => d.Type.ToString()) }, Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] FOOD_CARRY complete {checks - failures}/{checks}");
        }
    }
}
