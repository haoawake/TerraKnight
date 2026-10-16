using System.Collections;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>Regression for a full bag at the one-time Dream Nail pickup and the immediate scene transition.</summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator DreamNailFullTest()
        {
            int ok = 0, all = 0;
            void Check(string label, bool pass, string detail = "")
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 满包梦钉 {label}：{detail} {(pass ? "通过" : "不通过")}");
            }
            yield return GoTo("Town", "left1");
            var pd = PlayerData.instance;
            pd.isInvincible = true;
            pd.hasDreamNail = false;
            mod.DevNoFallDamage = true;
            var inv = mod.Inventory;
            // Genuine saturated inventory, all ordinary stacks favourited; do not evict any of them.
            for (int i = 0; i < Inventory.Size; i++)
                if (inv.Slots[i].Type != ItemType.KnightNail)
                {
                    inv.Slots[i] = ItemStack.Of(ItemType.DirtBlock, 9999);
                    inv.Slots[i].Favorite = true;
                }
            for (int i = 0; i < HkItems.KeyBagSlots; i++) inv.HkBag[i] = ItemStack.Of(ItemType.StoneBlock, 9999);
            inv.HkBag[HkItems.DreamNailSlot] = default;
            inv.Mouse = default;
            WorldItems.Clear();
            int CountDirt() => inv.Slots.Where(s => !s.IsEmpty && s.Type == ItemType.DirtBlock).Sum(s => s.Count);
            int dirt = CountDirt();
            yield return new WaitForSeconds(1.3f);
            Check("未获得时不提前发放", inv.HkBag[HkItems.DreamNailSlot].IsEmpty && SlotOf(ItemType.DreamNail) < 0 && WorldItems.DevCount(ItemType.DreamNail) == 0);
            Check("主背包确实无空间", inv.Room(ItemType.DreamNail) == 0, $"余量={inv.Room(ItemType.DreamNail)}");

            pd.hasDreamNail = true;
            yield return new WaitForSeconds(1.3f);
            Check("首次满包获得自动保管", inv.HkBag[HkItems.DreamNailSlot].Type == ItemType.DreamNail && WorldItems.DevCount(ItemType.DreamNail) == 0,
                $"保管格={inv.HkBag[HkItems.DreamNailSlot].Type}，地面={WorldItems.DevCount(ItemType.DreamNail)}");
            Check("未顶替或丢出原物品", CountDirt() == dirt && WorldItems.DevCount(ItemType.DirtBlock) == 0 && inv.Slots.Where(s => s.Type == ItemType.DirtBlock).All(s => s.Favorite), $"泥土总数={CountDirt()}/{dirt}");
            InventoryGui.Open = true;
            InventoryGui.DevOpenHkBag(true);
            yield return null;
            Shot("Town", "dreamnail_full_safekeeping");

            // Only this tool is accepted by the reserved slot, so ordinary keys cannot fill it first.
            inv.Mouse = ItemStack.Of(ItemType.Wood, 123);
            InventoryGui.DevClickHkBag(HkItems.DreamNailSlot);
            Check("保管格拒绝普通物品", inv.Mouse.Type == ItemType.Wood && inv.Mouse.Count == 123 && inv.HkBag[HkItems.DreamNailSlot].Type == ItemType.DreamNail);
            inv.Mouse = default;
            InventoryGui.DevClickHkBag(HkItems.DreamNailSlot, shift: true);
            Check("快捷栏满时取出不丢失", inv.HkBag[HkItems.DreamNailSlot].Type == ItemType.DreamNail);
            InventoryGui.Open = false;
            yield return GoTo("Crossroads_01", EntryGateOf("Crossroads_01"));
            yield return new WaitForSeconds(1.3f);
            Check("换场景后仍可领取", inv.HkBag[HkItems.DreamNailSlot].Type == ItemType.DreamNail && WorldItems.DevCount(ItemType.DreamNail) == 0);

            // Migrate an actual old-build stranded stack, preserving its reforge data.
            inv.HkBag[HkItems.DreamNailSlot] = default;
            var old = ItemStack.Of(ItemType.DreamNail);
            old.Prefix = 59; // Godly: legal on the Dream Nail; sword-only size prefixes are rejected on load.
            old.Forge = 2;
            Check("测试词缀适用于梦钉", Prefixes.Valid(ItemDb.Get(ItemType.DreamNail), old.Prefix), $"词缀={old.Prefix}");
            WorldItems.DropIn(old, "Dream_Nail", new Vector2(15f, 8f));
            Check("复现旧场景遗留梦钉", WorldItems.DevCount(ItemType.DreamNail) == 1 && inv.Room(ItemType.DreamNail) == 0);
            yield return new WaitForSeconds(1.3f);
            var recovered = inv.HkBag[HkItems.DreamNailSlot];
            Check("旧场景遗留自动回收", recovered.Type == ItemType.DreamNail && recovered.Prefix == old.Prefix && recovered.Forge == old.Forge && WorldItems.DevCount(ItemType.DreamNail) == 0,
                $"保管格={recovered.Type}，词缀={recovered.Prefix}，强化={recovered.Forge}，地面={WorldItems.DevCount(ItemType.DreamNail)}");
            inv.Bank[0] = ItemStack.Of(ItemType.DreamNail);
            WorldItems.DropIn(ItemStack.Of(ItemType.DreamNail), "Dream_Nail", Vector2.zero);
            yield return new WaitForSeconds(1.3f);
            Check("保管后不会重复补发", inv.HkBag[HkItems.DreamNailSlot].Count == 1 && inv.Bank[0].IsEmpty && WorldItems.DevCount(ItemType.DreamNail) == 0);

            // Already-gone drops are repaired from the original game's permanent acquisition flag too.
            inv.HkBag[HkItems.DreamNailSlot] = default;
            yield return new WaitForSeconds(1.3f);
            Check("掉落已消失也会补回", pd.hasDreamNail && inv.HkBag[HkItems.DreamNailSlot].Type == ItemType.DreamNail && WorldItems.DevCount(ItemType.DreamNail) == 0);

            InventoryGui.Open = true;
            inv.Slots[9] = default;
            InventoryGui.DevClickHkBag(HkItems.DreamNailSlot, shift: true);
            Check("可由界面取到快捷栏", inv.Slots[9].Type == ItemType.DreamNail && inv.HkBag[HkItems.DreamNailSlot].IsEmpty);
            inv.Selected = 9;
            inv.Override = -1;
            InventoryGui.Open = false;
            Check("取出后可持有使用", DreamNailItem.Holding(), $"手持={inv.Current.Type}");
            inv.Mouse = inv.Slots[9];
            inv.Mouse.Prefix = old.Prefix;
            inv.Mouse.Forge = old.Forge;
            inv.Slots[9] = ItemStack.Of(ItemType.DirtBlock, 9999);
            inv.Slots[9].Favorite = true;
            mod.ReturnOrDropMouseItem();
            Check("满包关闭界面安全保管", inv.Mouse.IsEmpty && inv.HkBag[HkItems.DreamNailSlot].Type == ItemType.DreamNail && inv.Slots[9].Type == ItemType.DirtBlock && WorldItems.DevCount(ItemType.DreamNail) == 0,
                $"保管格={inv.HkBag[HkItems.DreamNailSlot].Type}，光标={inv.Mouse.Type}");

            yield return QuitTest(1);
            inv = mod.Inventory;
            pd = PlayerData.instance;
            recovered = inv.HkBag[HkItems.DreamNailSlot];
            Check("退出读档保管内容不丢", pd.hasDreamNail && recovered.Type == ItemType.DreamNail && recovered.Prefix == old.Prefix && recovered.Forge == old.Forge,
                $"原版已获得={pd.hasDreamNail}，保管格={recovered.Type}，词缀={recovered.Prefix}，强化={recovered.Forge}");
            yield return new WaitForSeconds(1.3f);
            Check("读档后仍然只有一个", inv.HkBag.Count(s => s.Type == ItemType.DreamNail && !s.IsEmpty) == 1 && SlotOf(ItemType.DreamNail) < 0 && WorldItems.DevCount(ItemType.DreamNail) == 0);
            inv.HkBag[HkItems.DreamNailSlot] = default;
            inv.Slots[9] = default;
            WorldItems.DropIn(recovered, "Dream_Nail", Vector2.zero);
            yield return new WaitForSeconds(1.3f);
            int recoveredSlot = SlotOf(ItemType.DreamNail);
            Check("有空格时直接回主背包", recoveredSlot >= 0 && inv.Slots[recoveredSlot].Prefix == old.Prefix && inv.Slots[recoveredSlot].Forge == old.Forge && inv.HkBag[HkItems.DreamNailSlot].IsEmpty && WorldItems.DevCount(ItemType.DreamNail) == 0,
                $"物品格={recoveredSlot}");
            Plugin.Log.LogInfo($"[AutoTest] 满包梦钉 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
