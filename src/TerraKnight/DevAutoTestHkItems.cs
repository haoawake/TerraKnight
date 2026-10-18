using System.Collections;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "hkitems"): Hallownest's key items as items. Given into the Hallownest bag as the game's records say
    /// (the City Crest, two Simple Keys, the Lantern, the Kingsoul), working wherever carried (the records read true from
    /// the bag, from an unselected hotbar slot, in hand), not while both bags are full and back once a slot is free, the
    /// Kingsoul turned Void Heart, a key used up, refused anywhere but the hotbar and the bag, a stray one sent back, the
    /// game's own pictures, the bag drawn.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator HkItemsTest()
        {
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail = "")
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 圣巢物品 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(1.5f);
            var pd = PlayerData.instance;
            var inv = mod.Inventory;
            bool infinite = inv.Infinite;
            inv.Infinite = false;
            for (int i = 0; i < inv.Slots.Length; i++) inv.Slots[i] = default;
            for (int i = 0; i < inv.HkBag.Length; i++) inv.HkBag[i] = default;
            inv.Mouse = default;
            pd.hasCityKey = true;
            pd.simpleKeys = 2;
            pd.hasLantern = true;
            pd.gotCharm_36 = true;
            pd.royalCharmState = 3;
            yield return null;
            yield return null;
            int Bag(ItemType t) => inv.HkBag.Where(s => !s.IsEmpty && s.Type == t).Sum(s => s.Count);
            int Bar(ItemType t) => inv.Slots.Take(Inventory.HotbarSize).Where(s => !s.IsEmpty && s.Type == t).Sum(s => s.Count);
            Check("按存档给进圣巢背包", Bag(ItemType.HkCityCrest) == 1 && Bag(ItemType.HkSimpleKey) == 2 && Bag(ItemType.HkLumaflyLantern) == 1 && Bag(ItemType.HkKingsoul) == 1,
                  $"纹章 {Bag(ItemType.HkCityCrest)}，钥匙 {Bag(ItemType.HkSimpleKey)}，灯笼 {Bag(ItemType.HkLumaflyLantern)}，国王之魂 {Bag(ItemType.HkKingsoul)}");
            inv.Override = -1;
            inv.Selected = 0;
            Check("在圣巢背包里就生效", pd.GetBool("hasCityKey") && pd.GetInt("simpleKeys") == 2 && pd.GetBool("equippedCharm_36") && pd.GetBool("hasLantern"),
                  $"手里 {inv.Current.Def?.Name ?? "空"}，hasCityKey {pd.GetBool("hasCityKey")}，simpleKeys {pd.GetInt("simpleKeys")}，equippedCharm_36 {pd.GetBool("equippedCharm_36")}，hasLantern {pd.GetBool("hasLantern")}");
            InventoryGui.Open = true;
            for (int i = 0; i < inv.HkBag.Length; i++)
                if (!inv.HkBag[i].IsEmpty && inv.HkBag[i].Type != ItemType.HkLumaflyLantern) InventoryGui.DevClickHkBag(i, shift: true);
            InventoryGui.Open = false;
            int Slot(ItemType t) => System.Array.FindIndex(inv.Slots, s => !s.IsEmpty && s.Type == t);
            inv.Selected = Slot(ItemType.HkSimpleKey);
            Check("在快捷栏里没拿在手上也生效", Bar(ItemType.HkCityCrest) == 1 && pd.GetBool("hasCityKey") && pd.GetBool("equippedCharm_36"),
                  $"手里 {inv.Current.Def?.Name}，hasCityKey {pd.GetBool("hasCityKey")}，equippedCharm_36 {pd.GetBool("equippedCharm_36")}");
            Check("拿着钥匙生效", pd.GetInt("simpleKeys") == 2, $"simpleKeys {pd.GetInt("simpleKeys")}");
            inv.Selected = Slot(ItemType.HkCityCrest);
            Check("拿着城市纹章生效，钥匙照样算", pd.GetBool("hasCityKey") && pd.GetInt("simpleKeys") == 2, $"hasCityKey {pd.GetBool("hasCityKey")}，simpleKeys {pd.GetInt("simpleKeys")}");
            inv.Selected = Slot(ItemType.HkKingsoul);
            Check("拿着国王之魂生效", pd.GetBool("equippedCharm_36"), $"equippedCharm_36 {pd.GetBool("equippedCharm_36")}");

            // Both bags full as the Tram Pass is got: not carried, not counted; a hotbar slot freed, it comes in and counts.
            bool tramWas = pd.hasTramPass;
            pd.hasTramPass = false;
            yield return null;
            var fillers = new System.Collections.Generic.List<int>();
            for (int i = 0; i < HkItems.KeyBagSlots; i++) if (inv.HkBag[i].IsEmpty) { inv.HkBag[i] = ItemStack.Of(ItemType.Torch, 1); fillers.Add(i); }
            var barFillers = new System.Collections.Generic.List<int>();
            for (int i = 0; i < Inventory.HotbarSize; i++) if (inv.Slots[i].IsEmpty) { inv.Slots[i] = ItemStack.Of(ItemType.Torch, 1); barFillers.Add(i); }
            pd.hasTramPass = true;
            yield return null;
            bool fullRefused = !pd.GetBool("hasTramPass") && !HkItems.Carried(inv, ItemType.HkTramPass);
            if (barFillers.Count > 0) inv.Slots[barFillers[0]] = default;
            yield return null;
            Check("两个背包都满时没带上不算，腾出一格放进来就算", barFillers.Count > 0 && fullRefused && pd.GetBool("hasTramPass") && Bar(ItemType.HkTramPass) == 1,
                  $"空出快捷栏 {barFillers.Count} 格，满时 {fullRefused}，腾出后 hasTramPass {pd.GetBool("hasTramPass")}，快捷栏里 {Bar(ItemType.HkTramPass)}");
            foreach (int i in fillers) inv.HkBag[i] = default;
            foreach (int i in barFillers) if (!inv.Slots[i].IsEmpty && inv.Slots[i].Type == ItemType.Torch) inv.Slots[i] = default;
            pd.hasTramPass = tramWas;
            yield return null;
            InventoryGui.Open = true;
            Check("灯笼放在圣巢背包里就照明", pd.GetBool("hasLantern") && Bag(ItemType.HkLumaflyLantern) == 1, $"hasLantern {pd.GetBool("hasLantern")}，灯笼在圣巢背包 {Bag(ItemType.HkLumaflyLantern)}");

            // The Kingsoul turned Void Heart: the item follows.
            pd.royalCharmState = 4;
            yield return null;
            Check("国王之魂变虚空之心", Bar(ItemType.HkKingsoul) + Bag(ItemType.HkKingsoul) == 0 && Bag(ItemType.HkVoidHeart) == 1,
                  $"国王之魂 {Bar(ItemType.HkKingsoul) + Bag(ItemType.HkKingsoul)}，虚空之心 {Bag(ItemType.HkVoidHeart)}");
            // A key used: the game takes one.
            pd.simpleKeys = 1;
            yield return null;
            Check("用掉一把钥匙", Bar(ItemType.HkSimpleKey) + Bag(ItemType.HkSimpleKey) == 1);

            // Only the hotbar and the bag.
            inv.Mouse = default;
            int crest = System.Array.FindIndex(inv.Slots, s => !s.IsEmpty && s.Type == ItemType.HkCityCrest);
            InventoryGui.DevLeftClick(crest);
            bool onMouse = inv.Mouse.Type == ItemType.HkCityCrest;
            InventoryGui.DevLeftClick(25);
            Check("拿起后不能放进背包主格", onMouse && inv.Mouse.Type == ItemType.HkCityCrest && inv.Slots[25].IsEmpty,
                  $"纹章在 {crest}，拿起 {onMouse}，鼠标 {inv.Mouse.Def?.Name}，第25格 {inv.Slots[25].Def?.Name}");
            int free = System.Array.FindIndex(inv.HkBag, s => s.IsEmpty);
            InventoryGui.DevClickHkBag(free);
            Check("放回圣巢背包", inv.Mouse.IsEmpty && free >= 0 && inv.HkBag[free].Type == ItemType.HkCityCrest, $"空格 {free}，鼠标 {inv.Mouse.Def?.Name}");
            inv.Mouse = ItemStack.Of(ItemType.Torch, 5);
            InventoryGui.DevClickHkBag(HkItems.BagSlots - 1);
            Check("圣巢背包不收别的物品", inv.Mouse.Type == ItemType.Torch && inv.HkBag[HkItems.BagSlots - 1].IsEmpty);
            inv.Mouse = default;
            inv.Slots[30] = ItemStack.Of(ItemType.HkLoveKey, 1);
            pd.hasLoveKey = true;
            yield return null;
            Check("跑到别处的回到圣巢背包", inv.Slots[30].IsEmpty && Bag(ItemType.HkLoveKey) == 1);

            // Pictures and names: the game's own.
            var tex = Terraria.TerrariaAssets.Texture(ItemDb.Get(ItemType.HkCityCrest).Texture);
            Check("用游戏原图", tex != null && tex.name.Contains("HkCityCrest"), $"{tex?.name} {tex?.width}x{tex?.height}，名字 {ItemDb.Get(ItemType.HkCityCrest).Name}，{ItemDb.Get(ItemType.HkVoidHeart).Name}");
            InventoryGui.DevOpenHkBag(true);
            yield return new WaitForSecondsRealtime(0.5f);
            Shot("Town", "hk_bag");
            yield return null;
            InventoryGui.DevOpenHkBag(false);
            InventoryGui.Open = false;
            inv.Infinite = infinite;
            Plugin.Log.LogInfo($"[AutoTest] 圣巢物品 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
