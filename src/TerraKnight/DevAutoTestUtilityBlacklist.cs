using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TerraKnight.Items;
using TerraKnight.Terraria;
using UnityEngine;

namespace TerraKnight
{
    internal sealed partial class DevAutoTest
    {
        IEnumerator UtilityBlacklistTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            var evidence = new List<object>();
            int checks = 0, failures = 0;
            void Check(string name, bool pass)
            {
                checks++;
                evidence.Add(new { Name = name, Passed = pass });
                if (!pass) { failures++; Plugin.Log.LogError("[AutoTest] 黑名单/" + name + " 不通过"); }
            }
            yield return GoTo("Town", "left1");
            mod.ApplyCreative(false);
            Player.TerraStats.DevImmortal = true;
            mod.DevNoFallDamage = true;
            PlayerData.instance.isInvincible = true;
            var inv = mod.Inventory;
            inv.Clear();
            InventoryGui.Open = true;
            InventoryGui.DevUtilityClick(0);
            Check("图标入口打开黑名单", InventoryGui.DevBlacklistOpen);
            inv.Mouse = ItemStack.Of(ItemType.IronBar, 19);
            InventoryGui.DevBlacklistClick(0);
            Check("添加模板不消耗物品", inv.Mouse.Type == ItemType.IronBar && inv.Mouse.Count == 19 && inv.DropBlacklist.Contains(ItemType.IronBar));
            InventoryGui.DevBlacklistClick(1);
            Check("重复添加不增加条目", inv.DropBlacklist.Count == 1);
            Check("无需携带自动出售袋", Containers.Sells(inv, ItemType.IronBar) && !inv.Slots.Any(s => s.Type == ItemType.AutoSellBag));
            inv.Mouse = default;
            InventoryGui.DevBlacklistClick(0, true);
            Check("右键移除模板", inv.DropBlacklist.Count == 0);
            LootBlacklist.Add(inv, ItemStack.Of(ItemType.IronBar), out _);
            long purse = Shop.Purse;
            var stack = ItemStack.Of(ItemType.IronBar, 19);
            long value = Shop.SellValue(stack);
            Check("拾取按整堆售价结算", Containers.AutoSell(inv, stack, HeroController.instance.transform.position) && Shop.Purse == purse + value);
            Check("自动出售不占背包", !inv.Slots.Any(s => s.Type == ItemType.IronBar));
            stack.Favorite = true;
            purse = Shop.Purse;
            Check("收藏物不出售", !Containers.AutoSell(inv, stack, Vector2.zero) && Shop.Purse == purse);
            foreach (var type in new[] { ItemType.Geo, ItemType.KnightNail, ItemType.Heart, ItemType.Star })
                Check("关键物品不允许加入/" + type, !LootBlacklist.Add(inv, ItemStack.Of(type), out _));
            int left = inv.Pickup(ItemType.IronBar, 3);
            Check("手动取物不被黑名单出售", left == 0 && inv.Slots.Any(s => s.Type == ItemType.IronBar && s.Count == 3) && Shop.Purse == purse);
            // A genuine touching world pickup, including the full-bag case.
            for (int i = 0; i < Inventory.Total; i++) inv.Slots[i] = ItemStack.Of(ItemType.StoneBlock, 9999);
            inv.Slots[Inventory.CoinSlot] = ItemStack.Of(ItemType.Geo, 100);
            int sold = Containers.DevAutoSold;
            purse = Shop.Purse;
            WorldItems.Drop(ItemStack.Of(ItemType.IronBar, 7), HeroController.instance.GetComponent<BoxCollider2D>().bounds.center);
            for (float until = Time.realtimeSinceStartup + 8f; Containers.DevAutoSold == sold && Time.realtimeSinceStartup < until;) yield return null;
            Check("满背包真实接触拾取自动出售", Containers.DevAutoSold == sold + 7 && Shop.Purse >= purse + Shop.SellValue(ItemStack.Of(ItemType.IronBar, 7)));
            // Stored as stable names in the actual per-slot mod save.
            int slot = GameManager.instance.profileID;
            Save.ModSave.Write(slot, mod);
            string path = Path.Combine(TerraMod.SaveDir, $"TerraKnight_slot{slot}.json");
            var file = JObject.Parse(File.ReadAllText(path));
            Check("存档记录物品名称", file["DropBlacklist"]?.Values<string>().Contains("IronBar") == true);
            inv.DropBlacklist.Clear();
            Save.ModSave.Read(slot, mod);
            Check("读档恢复黑名单", inv.DropBlacklist.SetEquals(new[] { ItemType.IronBar }));
            file.Remove("DropBlacklist");
            File.WriteAllText(path, file.ToString());
            Save.ModSave.Read(slot, mod);
            Check("旧存档加载为空名单", inv.DropBlacklist.Count == 0);
            LootBlacklist.Load(inv, new[] { "IronBar", "not_an_item", "IronBar" });
            Check("未知名称及重复项兼容", inv.DropBlacklist.SetEquals(new[] { ItemType.IronBar }));
            Check("零售价杂物可加入黑名单", LootBlacklist.Add(inv, ItemStack.Of(ItemType.Wood), out _));
            purse = Shop.Purse;
            Check("零售价按零出售不产生金币", Containers.AutoSell(inv, ItemStack.Of(ItemType.Wood, 4), Vector2.zero) && Shop.Purse == purse);
            foreach (var name in new[] { "UtilityBlacklist", "UtilityBestiary", "UtilityHallownest", "UtilityQuickShop" })
            {
                var tex = TerrariaAssets.Texture(name);
                Check("嵌入贴图/" + name, tex.width == 32 && tex.height == 30 && tex.filterMode == FilterMode.Point);
            }
            foreach (float ui in new[] { .65f, .85f, 1f, 1.25f })
            {
                var rects = Enumerable.Range(0, 4).Select(i => InventoryGui.UtilityButtonRect(new Rect(0, 0, 1280, 720), ui, i)).ToArray();
                Check("四按钮同尺寸/" + ui, rects.All(r => Mathf.Abs(r.width - 32 * ui) < .01f && Mathf.Abs(r.height - 30 * ui) < .01f));
                Check("按钮互不重叠/" + ui, !rects[0].Overlaps(rects[1]) && !rects[0].Overlaps(rects[2]) && !rects[2].Overlaps(rects[3]));
                Check("两行两列/" + ui, rects[0].y == rects[1].y && rects[2].y == rects[3].y && rects[0].x == rects[2].x && rects[2].y > rects[0].y);
            }
            InventoryGui.ClearUtilities();
            InventoryGui.DevUtilityClick(1);
            Check("图鉴图标功能", InventoryGui.BestiaryOpen);
            InventoryGui.DevUtilityClick(2);
            Check("圣巢背包图标功能", InventoryGui.HkBagOpen && !InventoryGui.BestiaryOpen);
            InventoryGui.DevUtilityClick(3);
            Check("快捷商店图标功能", InventoryGui.SellMode && !InventoryGui.HkBagOpen);
            InventoryGui.DevUtilityClick(0);
            Check("面板互斥", InventoryGui.DevBlacklistOpen && !InventoryGui.SellMode);
            InventoryGui.Open = false;
            Check("关闭背包收起黑名单", !InventoryGui.DevBlacklistOpen);
            File.WriteAllText(Path.Combine(DevCopy.SaveDir, "utility-blacklist-result.json"), JsonConvert.SerializeObject(new { Checks = checks, Failures = failures, NativePickupTest = true, VisualUiVerified = false, Evidence = evidence }, Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] 黑名单 {checks} 项，失败 {failures}");
        }
    }
}
