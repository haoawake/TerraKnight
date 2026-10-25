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
                Check("四个弹药栏旁的按钮/" + ui, rects.All(r => r.x == rects[0].x) && rects[0].y < rects[1].y && rects[1].y < rects[2].y && rects[2].y < rects[3].y);
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
            // Exercise the real inventory click route, including its priority over ordinary trash.
            void Click(int index, bool shift = false, bool ctrl = false)
            {
                InventoryGui.DevShift = shift; InventoryGui.DevCtrl = ctrl;
                try { InventoryGui.DevLeftClick(index); }
                finally { InventoryGui.DevShift = InventoryGui.DevCtrl = false; }
            }
            void State(string label) => Plugin.Log.LogInfo($"[AutoTest] 出售快捷键/{label}: purse={Shop.Purse}, expected={purse}, value={value}, slot0={inv.Slots[0].Type}/{inv.Slots[0].Count}, nail={inv.Slots[2].Type}, mouse={inv.Mouse.Type}/{inv.Mouse.Count}, sold={InventoryGui.DevSold.Count}, ironlisted={Containers.Listed(inv, ItemType.IronBar)}, silverlisted={Containers.Listed(inv, ItemType.SilverBar)}, panel={World.Chests.SellListOpen}, ironprice={Shop.SellValue(ItemStack.Of(ItemType.IronBar))}");
            inv.Clear();
            InventoryGui.ClearSold();
            inv.Slots[0] = ItemStack.Of(ItemType.SilverBar, 7);
            purse = Shop.Purse;
            value = Shop.SellValue(inv.Slots[0]);
            Click(0, ctrl: true);
            Check("黑名单Ctrl整堆出售且不加入名单", inv.Slots[0].IsEmpty && Shop.Purse == purse + value && inv.DropBlacklist.Count == 0 && inv.Trash.IsEmpty && InventoryGui.DevSold.Count == 1);
            inv.Slots[0] = ItemStack.Of(ItemType.IronBar, 8);
            purse = Shop.Purse;
            Click(0, shift: true);
            Check("黑名单Shift加入模板不消耗", inv.DropBlacklist.Contains(ItemType.IronBar) && inv.Slots[0].Count == 8 && Shop.Purse == purse && inv.Mouse.IsEmpty);
            inv.Slots[1] = ItemStack.Of(ItemType.SilverBar, 3);
            inv.Slots[1].Favorite = true;
            purse = Shop.Purse;
            Click(1, ctrl: true);
            Check("黑名单Ctrl保护收藏", inv.Slots[1].Count == 3 && inv.Slots[1].Favorite && Shop.Purse == purse && inv.Trash.IsEmpty);
            inv.Slots[2] = ItemStack.Of(ItemType.KnightNail);
            Click(2, ctrl: true);
            Check("黑名单Ctrl保护骨钉", inv.Slots[2].Type == ItemType.KnightNail && inv.Trash.IsEmpty);
            inv.Slots[3] = ItemStack.Of(ItemType.Geo, 10);
            purse = Shop.Purse;
            Click(3, ctrl: true);
            Check("黑名单Ctrl保护货币", inv.Slots[3].Type == ItemType.Geo && inv.Slots[3].Count == 10 && Shop.Purse == purse && inv.Trash.IsEmpty);
            inv.Mouse = ItemStack.Of(ItemType.Silk, 2);
            purse = Shop.Purse;
            Click(0, ctrl: true);
            Check("持物时Ctrl不交换或误删", inv.Mouse.Type == ItemType.Silk && inv.Mouse.Count == 2 && inv.Slots[0].Count == 8 && Shop.Purse == purse && inv.Trash.IsEmpty);
            inv.Mouse = default;
            InventoryGui.DevUtilityClick(0);
            inv.Slots[4] = ItemStack.Of(ItemType.AutoSellBag);
            Containers.Toggle(ItemType.AutoSellBag);
            Check("自动出售袋正常打开", World.Chests.SellListOpen);
            purse = Shop.Purse;
            value = Shop.SellValue(inv.Slots[0]);
            Click(0, shift: true);
            State("出售袋Shift");
            Check("出售袋拒绝零售价物品时原物保留", value == 0 && inv.Slots[0].Type == ItemType.IronBar && inv.Slots[0].Count == 8 && !Containers.Listed(inv, ItemType.IronBar) && Shop.Purse == purse && inv.Trash.IsEmpty);
            inv.Slots[0] = ItemStack.Of(ItemType.SilverBar, 4);
            purse = Shop.Purse;
            value = Shop.SellValue(inv.Slots[0]);
            Click(0, ctrl: true);
            State("出售袋Ctrl");
            Check("出售袋Ctrl只出售不改名单", inv.Slots[0].IsEmpty && !Containers.Listed(inv, ItemType.SilverBar) && Shop.Purse == purse + value && InventoryGui.DevSold.Count == 2 && inv.Trash.IsEmpty);
            inv.Slots[0] = ItemStack.Of(ItemType.SilverBar, 8);
            purse = Shop.Purse;
            value = Shop.SellValue(inv.Slots[0]);
            Click(0, shift: true);
            Check("出售袋Shift加入并卖整堆", value > 0 && inv.Slots[0].IsEmpty && Containers.Listed(inv, ItemType.SilverBar) && Shop.Purse == purse + value && inv.Trash.IsEmpty);
            purse = Shop.Purse;
            Click(1, ctrl: true);
            Click(2, ctrl: true);
            Check("出售袋Ctrl同样保护收藏和骨钉", inv.Slots[1].Count == 3 && inv.Slots[2].Type == ItemType.KnightNail && Shop.Purse == purse && inv.Trash.IsEmpty);
            World.Chests.Close(false);
            purse = Shop.Purse;
            value = InventoryGui.DevSold[0].Price;
            InventoryGui.DevBuyBack(0);
            State("买回");
            Check("快捷出售可按原价买回整堆", inv.Mouse.Type == ItemType.SilverBar && inv.Mouse.Count == 4 && Shop.Purse == purse - value && InventoryGui.DevSold.Count == 1);
            inv.Mouse = default;
            inv.Slots[0] = ItemStack.Of(ItemType.SilverBar, 4);
            Click(0, ctrl: true);
            Check("关闭两面板后Ctrl保持原垃圾桶行为", inv.Slots[0].IsEmpty && inv.Trash.Type == ItemType.SilverBar && inv.Trash.Count == 4);
            var pouch = ItemDb.Get(ItemType.AutoSellBag);
            var pouchArt = TerrariaAssets.Texture(pouch.Texture);
            Check("出售袋独立像素图标", pouch.Texture == Relics.TexturePrefix + "auto_sell_bag" && pouchArt.width == 32 && pouchArt.height == 32 && pouchArt.filterMode == FilterMode.Point);
            InventoryGui.Open = false;
            Check("关闭背包收起黑名单", !InventoryGui.DevBlacklistOpen);
            File.WriteAllText(Path.Combine(DevCopy.SaveDir, "utility-blacklist-result.json"), JsonConvert.SerializeObject(new { Checks = checks, Failures = failures, NativePickupTest = true, VisualUiVerified = false, Evidence = evidence }, Formatting.Indented));
            Plugin.Log.LogInfo($"[AutoTest] 黑名单 {checks} 项，失败 {failures}");
        }
    }
}
