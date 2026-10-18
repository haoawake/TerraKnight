using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TerraKnight.Items;
using TerraKnight.Player;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator AbilityBagTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            PlayerData.instance.isInvincible = true;
            TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            yield return GoTo("Town", "left1");
            mod.ApplyCreative(false);
            var inv = mod.Inventory;
            inv.Clear();
            int checks = 0, failures = 0;
            var rows = new List<object>();
            void Check(string name, bool pass)
            {
                checks++; rows.Add(new { Name = name, Passed = pass });
                if (!pass) { failures++; Plugin.Log.LogError("[AutoTest] 能力背包/" + name + " 不通过"); }
            }
            void Grant() => AccessTools.Method(typeof(TerraMod), "GrantAbilityAccessories").Invoke(mod, new object[] { 100f });
            foreach (var (flag, item) in PlayerHooks.AbilityItems)
            {
                mod.GrantedAbilities.Remove(flag);
                mod.GrantedAbilities.Remove(HkItems.AbilityDeliveryKey(flag));
                if (flag == "quakeLevel") PlayerData.instance.quakeLevel = 1;
                else PlayerData.instance.SetBool(flag, true);
            }
            for (int i = 0; i < Inventory.Total; i++) inv.Slots[i] = ItemStack.Of(ItemType.StoneBlock, 999);
            for (int i = 0; i < HkItems.KeyBagSlots; i++) inv.HkBag[i] = ItemStack.Of(ItemType.HkRancidEgg, 9999);
            inv.HkBag[HkItems.DreamNailSlot] = ItemStack.Of(ItemType.DreamNail);
            Check("普通包无空位", inv.Room(ItemType.CrystalHeart) == 0);
            Grant();
            Check("容量为20普通加梦钉加8能力格", inv.HkBag.Length == 29 && HkItems.AbilityBagSlots == PlayerHooks.AbilityItems.Length);
            foreach (var (flag, item) in PlayerHooks.AbilityItems)
            {
                int slot = HkItems.AbilityBagSlot(item);
                Check("满包领取进入对应能力格/" + flag, inv.HkBag[slot].Type == item && inv.HkBag[slot].Count == 1 && mod.GrantedAbilities.Contains(flag));
                Check("未挤占普通背包/" + flag, !inv.Slots.Any(s => s.Type == item));
                Check("可识别背包里的已有能力/" + flag, inv.Owns(item));
            }
            Check("原普通格与梦钉格无覆盖", inv.HkBag.Take(HkItems.KeyBagSlots).All(s => s.Type == ItemType.HkRancidEgg && s.Count == 9999)
                && inv.HkBag[HkItems.DreamNailSlot].Type == ItemType.DreamNail);
            Grant();
            Check("重复检查不重复发奖", PlayerHooks.AbilityItems.All(p => inv.HkBag.Count(s => s.Type == p.item && !s.IsEmpty) == 1));
            int shade = HkItems.AbilityBagSlot(ItemType.ShadeCloak);
            inv.HkBag[shade] = default;
            mod.GrantedAbilities.Remove(HkItems.AbilityDeliveryKey("hasShadowDash"));
            Grant();
            Check("旧版已记领取但遗失暗影披风补发一次", inv.HkBag[shade].Type == ItemType.ShadeCloak && mod.GrantedAbilities.Contains(HkItems.AbilityDeliveryKey("hasShadowDash")));
            inv.HkBag[shade] = default;
            Grant();
            Check("完成迁移后不会无限补发", inv.HkBag[shade].IsEmpty);
            inv.Slots[1] = ItemStack.Of(ItemType.HallownestShield);
            mod.GrantedAbilities.Remove(HkItems.AbilityDeliveryKey("hasShadowDash"));
            Grant();
            Check("已合成圣巢之盾不重复补材料", inv.HkBag[shade].IsEmpty && mod.GrantedAbilities.Contains(HkItems.AbilityDeliveryKey("hasShadowDash")));
            inv.Slots[1] = ItemStack.Of(ItemType.StoneBlock, 999);
            inv.HkBag[shade] = ItemStack.Of(ItemType.ShadeCloak);
            // Delivery cannot be committed if the reserved target was obstructed.
            int crystal = HkItems.AbilityBagSlot(ItemType.CrystalHeart);
            inv.HkBag[crystal] = ItemStack.Of(ItemType.IronBar, 7);
            mod.GrantedAbilities.Remove("hasSuperDash");
            Grant();
            Check("未送达不标记已领取且不覆盖原物", !mod.GrantedAbilities.Contains("hasSuperDash") && inv.HkBag[crystal].Type == ItemType.IronBar);
            inv.HkBag[crystal] = default;
            Grant();
            Check("腾出专用格后重试补发", inv.HkBag[crystal].Type == ItemType.CrystalHeart && mod.GrantedAbilities.Contains("hasSuperDash"));
            // Same movement behavior as before: storage does not silently equip an accessory.
            Check("存入背包不会自动获得佩戴效果", !AbilityAccessories.CrystalWorn && !AbilityAccessories.ShadeWorn);
            InventoryGui.Open = true;
            InventoryGui.DevClickHkBag(crystal);
            Check("从圣巢背包取出能力饰品", inv.Mouse.Type == ItemType.CrystalHeart && inv.HkBag[crystal].IsEmpty);
            inv.Mouse.Prefix = 65;
            inv.Mouse.Favorite = true;
            var held = inv.Mouse;
            mod.ReturnOrDropMouseItem();
            Check("满包关闭鼠标物品回专用格", inv.Mouse.IsEmpty && inv.HkBag[crystal].Type == held.Type && inv.HkBag[crystal].Prefix == held.Prefix && inv.HkBag[crystal].Favorite == held.Favorite);
            Check("专用格拒绝其他物品", !HkItems.BagAccepts(crystal, ItemStack.Of(ItemType.Wood)));
            inv.Slots[0] = inv.HkBag[crystal]; inv.HkBag[crystal] = default;
            InventoryGui.DevLeftClick(0, shift: true);
            Check("Shift点击返回能力格保留属性", inv.Slots[0].IsEmpty && inv.HkBag[crystal].Prefix == held.Prefix && inv.HkBag[crystal].Favorite == held.Favorite);
            AbilityAccessories.GiveForCreative(inv);
            Check("创造补齐不重复能力饰品", inv.HkBag.Count(s => s.Type == ItemType.CrystalHeart && !s.IsEmpty) == 1 && !inv.Slots.Any(s => s.Type == ItemType.CrystalHeart));
            int profile = GameManager.instance.profileID;
            Save.ModSave.Write(profile, mod);
            inv.HkBag[crystal] = default;
            Save.ModSave.Read(profile, mod);
            Check("能力格随存档恢复", inv.HkBag[crystal].Type == ItemType.CrystalHeart && inv.HkBag[crystal].Prefix == held.Prefix && inv.HkBag[crystal].Favorite == held.Favorite);
            string path = Path.Combine(TerraMod.SaveDir, $"TerraKnight_slot{profile}.json");
            var old = JObject.Parse(File.ReadAllText(path));
            old["HkBag"] = new JArray(((JArray)old["HkBag"]).Take(21));
            File.WriteAllText(path, old.ToString());
            Save.ModSave.Read(profile, mod);
            Check("旧21格存档梦钉位置不变", inv.HkBag[20].Type == ItemType.DreamNail && inv.HkBag.Length == 29);
            Check("旧存档追加能力格为空", inv.HkBag.Skip(21).All(s => s.IsEmpty));
            // Scene changes cannot strand rewards on the previous room's floor.
            mod.GrantedAbilities.Remove("hasSuperDash");
            Grant();
            yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            Check("切场景后领取物品仍在圣巢背包", inv.HkBag[crystal].Type == ItemType.CrystalHeart);
            File.WriteAllText(Path.Combine(DevCopy.SaveDir, "ability-bag-result.json"), JsonConvert.SerializeObject(new { Checks = checks, Failures = failures, Slots = inv.HkBag.Length, Evidence = rows }, Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] 能力背包 {checks} 项，失败 {failures}");
        }
    }
}
