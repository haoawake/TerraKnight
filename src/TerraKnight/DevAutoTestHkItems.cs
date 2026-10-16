using System.Collections;
using System.Linq;
using TerraKnight.Items;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "hkitems"): Hallownest's key items as items. Given into the Hallownest bag as the game's records say
    /// (the City Crest, two Simple Keys, the Lantern, the Kingsoul), working only in hand (the records read false or 0
    /// from the bag and from an unselected hotbar slot), the Kingsoul turned Void Heart, a key used up, refused anywhere
    /// but the hotbar and the bag, a stray one sent back, the game's own pictures, the bag drawn.
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
            Check("在背包里不生效", !pd.GetBool("hasCityKey") && pd.GetInt("simpleKeys") == 0 && !pd.GetBool("equippedCharm_36"),
                  $"hasCityKey {pd.GetBool("hasCityKey")}，simpleKeys {pd.GetInt("simpleKeys")}，equippedCharm_36 {pd.GetBool("equippedCharm_36")}");
            InventoryGui.Open = true;
            for (int i = 0; i < inv.HkBag.Length; i++)
                if (!inv.HkBag[i].IsEmpty && inv.HkBag[i].Type != ItemType.HkLumaflyLantern) InventoryGui.DevClickHkBag(i, shift: true);
            InventoryGui.Open = false;
            int Slot(ItemType t) => System.Array.FindIndex(inv.Slots, s => !s.IsEmpty && s.Type == t);
            inv.Override = -1;
            inv.Selected = Slot(ItemType.HkSimpleKey);
            Check("快捷栏里没拿在手上不生效", Bar(ItemType.HkCityCrest) == 1 && !pd.GetBool("hasCityKey") && !pd.GetBool("equippedCharm_36"),
                  $"手里 {inv.Current.Def?.Name}，hasCityKey {pd.GetBool("hasCityKey")}，equippedCharm_36 {pd.GetBool("equippedCharm_36")}");
            Check("拿着钥匙生效", pd.GetInt("simpleKeys") == 2, $"simpleKeys {pd.GetInt("simpleKeys")}");
            inv.Selected = Slot(ItemType.HkCityCrest);
            Check("拿着城市纹章生效", pd.GetBool("hasCityKey") && pd.GetInt("simpleKeys") == 0, $"hasCityKey {pd.GetBool("hasCityKey")}，simpleKeys {pd.GetInt("simpleKeys")}");
            inv.Selected = Slot(ItemType.HkKingsoul);
            Check("拿着国王之魂生效", pd.GetBool("equippedCharm_36"), $"equippedCharm_36 {pd.GetBool("equippedCharm_36")}");
            InventoryGui.Open = true;
            bool lit = pd.GetBool("hasLantern");
            Check("灯笼在背包里不照明（没戴矿工头盔）", !lit || Equipment.Current.Light || Equipment.Current.NightVision, $"hasLantern {lit}");

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
