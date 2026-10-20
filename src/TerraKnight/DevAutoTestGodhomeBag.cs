using System.Collections;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "gghkbag"): a player's report — equipment changed in Godhome, the Hallownest bag gone. In Dirtmouth and
    /// then in Godhome's Atrium and Hall of Gods: Hallownest items and the Shade Cloak in the Hallownest bag, the inventory
    /// open on it, armor and accessories put on and taken off by clicks, the Shade Cloak worn out of the bag and back, the
    /// loadouts switched (a normal save's one bag, then a creative save's bag per loadout); after each step the bag's
    /// contents, its button drawn, and what hides it (a chest, a shop) logged.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator GodhomeBagTest()
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail = "")
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 神居背包 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            var pd = PlayerData.instance;
            var inv = mod.Inventory;
            inv.Infinite = false;
            pd.hasCityKey = true;
            pd.simpleKeys = 2;
            pd.hasLantern = true;
            pd.hasShadowDash = true;
            pd.hasDash = true;
            string Bag() => string.Join(" ", inv.HkBag.Select((s, i) => s.IsEmpty ? null : $"{i}:{s.Def?.Name}×{s.Count}").Where(x => x != null));
            int HkCount() => inv.HkBag.Count(s => !s.IsEmpty);
            string Hidden() => $"箱子 {World.Chests.IsOpen}，商店 {Town.TownNpcs.ShopOpen}，锻造 {Town.TownNpcs.ForgeMode}，重铸 {Town.TownNpcs.ReforgeOpen}";

            foreach (var (scene, creative) in new[] { ("Town", false), ("GG_Atrium", false), ("GG_Workshop", false), ("GG_Atrium", true) })
            {
                yield return GoTo(scene, EntryGateOf(scene));
                yield return new WaitForSeconds(1.5f);
                string at = $"{scene}{(creative ? "（每套配装各自背包）" : "")}";
                inv.BagPerLoadout = creative;
                for (int i = 0; i < inv.Slots.Length; i++) inv.Slots[i] = default;
                for (int i = 0; i < inv.Accessories.Length; i++) inv.Accessories[i] = default;
                for (int i = 0; i < inv.Armor.Length; i++) inv.Armor[i] = default;
                inv.Mouse = default;
                if (!HkItems.Carried(inv, ItemType.ShadeCloak)) HkItems.TryStoreAbility(inv, ItemStack.Of(ItemType.ShadeCloak));
                inv.Slots[20] = ItemStack.Of(ItemType.MiningHelmet);
                inv.Slots[21] = ItemStack.Of(ItemType.CloudinaBottle);
                inv.Slots[22] = ItemStack.Of(ItemType.HermesBoots);
                yield return null;
                yield return null;
                int before = HkCount();
                string bagBefore = Bag();
                InventoryGui.Open = true;
                InventoryGui.DevOpenHkBag(true);
                yield return null;
                yield return null;
                Check($"{at} 打开背包：圣巢背包按钮和格子都画出来", InventoryGui.DevHitRect("HkButton", 0) != null && InventoryGui.DevHitIndexes("HkBag").Count > 0,
                    $"按钮 {InventoryGui.DevHitRect("HkButton", 0)?.ToString() ?? "没画"}，格子 {InventoryGui.DevHitIndexes("HkBag").Count}，{Hidden()}，圣巢背包 {bagBefore}");

                // Armor and accessories on by clicks, then the Shade Cloak out of the Hallownest bag onto an accessory slot.
                InventoryGui.DevLeftClick(20);
                InventoryGui.DevLeftClickEquip(true, 0);
                InventoryGui.DevLeftClick(21);
                InventoryGui.DevLeftClickEquip(false, 0);
                InventoryGui.DevLeftClick(22, shift: true);
                int cloak = System.Array.FindIndex(inv.HkBag, s => !s.IsEmpty && s.Type == ItemType.ShadeCloak);
                if (cloak >= 0)
                {
                    InventoryGui.DevClickHkBag(cloak);
                    InventoryGui.DevLeftClickEquip(false, 2);
                }
                yield return null;
                yield return null;
                yield return null;
                string worn = string.Join(" ", inv.Armor.Concat(inv.Accessories).Where(s => !s.IsEmpty).Select(s => s.Def?.Name));
                Check($"{at} 穿上装备后圣巢背包按钮还在", InventoryGui.DevHitRect("HkButton", 0) != null && InventoryGui.HkBagOpen,
                    $"穿着 {worn}，手上 {inv.Mouse.Def?.Name ?? "空"}，圣巢背包 {Bag()}，开着 {InventoryGui.HkBagOpen}，{Hidden()}");
                Check($"{at} 穿上后圣巢物品没少（披风穿走一件）", HkCount() >= before - 1, $"之前 {before} 格（{bagBefore}），现在 {HkCount()} 格（{Bag()}）");

                // Off again: the cloak back into the bag by a click on its slot.
                InventoryGui.DevLeftClickEquip(false, 2);
                if (!inv.Mouse.IsEmpty && HkItems.AbilityBagSlot(inv.Mouse.Type) >= 0) InventoryGui.DevClickHkBag(HkItems.AbilityBagSlot(inv.Mouse.Type));
                InventoryGui.DevLeftClickEquip(true, 0);
                if (!inv.Mouse.IsEmpty) InventoryGui.DevLeftClick(30);
                yield return null;
                yield return null;
                Check($"{at} 脱下后圣巢背包按钮还在、物品都在", InventoryGui.DevHitRect("HkButton", 0) != null && HkCount() >= before,
                    $"手上 {inv.Mouse.Def?.Name ?? "空"}，圣巢背包 {Bag()}，{Hidden()}");

                // The loadouts switched there and back, the inventory open and shut.
                bool sw1 = mod.TrySwitchLoadout(1);
                yield return null;
                yield return null;
                string mid = Bag();
                bool midButton = InventoryGui.DevHitRect("HkButton", 0) != null;
                bool sw2 = mod.TrySwitchLoadout(0);
                yield return null;
                yield return null;
                Check($"{at} 换配装后圣巢背包按钮还在、物品都在", sw1 && sw2 && midButton && InventoryGui.DevHitRect("HkButton", 0) != null && HkCount() >= before,
                    $"换到 2 {sw1}（那时 {mid}，按钮 {midButton}），换回 1 {sw2}，圣巢背包 {Bag()}，{Hidden()}");
                InventoryGui.Open = false;
                yield return null;
                InventoryGui.Open = true;
                InventoryGui.DevOpenHkBag(true);
                yield return null;
                yield return null;
                Check($"{at} 关上再打开背包，圣巢背包照样在", InventoryGui.DevHitRect("HkButton", 0) != null && InventoryGui.DevHitIndexes("HkBag").Count > 0 && HkCount() >= before,
                    $"按钮 {InventoryGui.DevHitRect("HkButton", 0)?.ToString() ?? "没画"}，格子 {InventoryGui.DevHitIndexes("HkBag").Count}，圣巢背包 {Bag()}");
                Shot(scene, $"gghkbag_{scene}{(creative ? "_creative" : "")}");
                yield return null;
                InventoryGui.Open = false;
            }
            inv.BagPerLoadout = false;
            Plugin.Log.LogInfo($"[AutoTest] 神居背包 结果 {ok}/{all}");
        }
    }
}
