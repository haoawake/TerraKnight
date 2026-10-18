using System.Collections;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "ammoslots"): the four ammo slots. A bow takes its arrows from the ammo slots top
    /// to bottom, then from the bag front to back (Player.AmmoSlotOrder_Default); ammo picked up goes
    /// into the ammo slots (Player.FillAmmo), gel only onto a stack of it already there; anything else
    /// can't be put in them.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator AmmoSlotsTest(HeroController hero)
        {
            var inv = mod.Inventory;
            var saved = (ItemStack[])inv.Slots.Clone();
            bool all = TerraMod.DevAllInfinite;
            TerraMod.DevAllInfinite = false;
            mod.ApplyCreative(false);
            for (int i = 0; i < Inventory.Total; i++) inv.Slots[i] = default;

            // The order: ammo slots 2 and 3 (top first), then the bag's slots 7 and 30 (front first).
            int a1 = Inventory.Size + 1, a2 = Inventory.Size + 2;
            inv.Slots[30] = ItemStack.Of(ItemType.WoodenArrow, 5);
            inv.Slots[7] = ItemStack.Of(ItemType.FlamingArrow, 5);
            inv.Slots[a2] = ItemStack.Of(ItemType.FrostburnArrow, 5);
            inv.Slots[a1] = ItemStack.Of(ItemType.BoneArrow, 5);
            var order = new System.Text.StringBuilder();
            for (int n = 0; n < 4; n++)
            {
                int at = inv.FindAmmo(AmmoKind.Arrow);
                order.Append(at < 0 ? "无 " : $"{(Inventory.IsAmmoSlot(at) ? "弹药栏" + (at - Inventory.Size + 1) : "背包" + at)}:{inv.Slots[at].Def.Name} ");
                if (at >= 0) inv.Slots[at] = default;
            }
            Plugin.Log.LogInfo($"[AutoTest] 弹药栏 找箭的顺序：{order}（应为 弹药栏2 → 弹药栏3 → 背包7 → 背包30）");

            // Pickups: arrows into the first empty ammo slot; gel not into an empty one, but onto one there.
            for (int i = 0; i < Inventory.Total; i++) inv.Slots[i] = default;
            inv.Pickup(ItemType.WoodenArrow, 30);
            int arrowsAt = inv.FindAmmo(AmmoKind.Arrow);
            inv.Pickup(ItemType.Gel, 10);
            int gelAt = inv.Find(d => d.Type == ItemType.Gel);
            inv.Slots[Inventory.Size + 3] = ItemStack.Of(ItemType.Gel, 5);
            inv.Pickup(ItemType.Gel, 7);
            Plugin.Log.LogInfo($"[AutoTest] 弹药栏 捡起：木箭进了 {(Inventory.IsAmmoSlot(arrowsAt) ? "弹药栏" + (arrowsAt - Inventory.Size + 1) : "背包" + arrowsAt)}（应为弹药栏1），" +
                               $"凝胶先进了 背包{gelAt}（不占空弹药格），弹药栏4 已有凝胶后再捡 7 个：弹药栏4 {inv.Slots[Inventory.Size + 3].Count}（应为 12），背包里 {(gelAt >= 0 ? inv.Slots[gelAt].Count : 0)}（应仍为 10）");

            // Something that isn't ammo can't go in.
            inv.Mouse = ItemStack.Of(ItemType.CopperShortsword);
            InventoryGui.Open = true;
            InventoryGui.DevLeftClick(Inventory.Size + 2);
            Plugin.Log.LogInfo($"[AutoTest] 弹药栏 把铜短剑放进弹药栏3：弹药栏3 {(inv.Slots[Inventory.Size + 2].IsEmpty ? "空" : inv.Slots[Inventory.Size + 2].Def.Name)}（应为空），光标上 {(inv.Mouse.IsEmpty ? "空" : inv.Mouse.Def.Name)}（应为铜短剑）");
            inv.Mouse = ItemStack.Of(ItemType.HellfireArrow, 20);
            InventoryGui.DevLeftClick(Inventory.Size + 2);
            Plugin.Log.LogInfo($"[AutoTest] 弹药栏 把狱炎箭放进弹药栏3：弹药栏3 {(inv.Slots[Inventory.Size + 2].IsEmpty ? "空" : inv.Slots[Inventory.Size + 2].Def.Name + " ×" + inv.Slots[Inventory.Size + 2].Count)}（应为狱炎箭 ×20）");
            inv.Mouse = default;
            yield return new WaitForSeconds(0.5f);
            Shot(GameManager.instance.sceneName, "ammo_slots");
            yield return null;
            InventoryGui.Open = false;

            // A shot takes from the ammo slots first: the wooden arrows in slot 1 (above the hellfire ones in 3), not the bag's.
            inv.Slots[20] = ItemStack.Of(ItemType.FlamingArrow, 10);
            int before = inv.Slots[Inventory.Size].Count, bag0 = inv.Slots[20].Count;
            var col = hero.GetComponent<BoxCollider2D>();
            for (int n = 0; n < 3; n++)
            {
                while (!mod.DevReady) yield return null;
                mod.DevUse(ItemType.WoodenBow, (Vector2)col.bounds.center + Vector2.right * 6f);
                yield return new WaitForSeconds(0.5f);
            }
            Plugin.Log.LogInfo($"[AutoTest] 弹药栏 木弓射 3 箭：弹药栏1 木箭 {before} -> {inv.Slots[Inventory.Size].Count}（应少 3），背包20 烈焰箭 {bag0} -> {inv.Slots[20].Count}（应不变）");

            for (int i = 0; i < Inventory.Total; i++) inv.Slots[i] = saved[i];
            TerraMod.DevAllInfinite = all;
            mod.ApplyCreative(false);
        }
    }
}
