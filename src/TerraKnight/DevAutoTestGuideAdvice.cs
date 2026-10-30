using System;
using System.Collections;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.Terraria;
using TerraKnight.Town;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "guideadvice", the user's 10-29): the Guide's advice — its name, words and picture, sold by the Guide from
    /// the start for 10000 geo; carried, the crafting column's scroll switches it to his recipe list (wood put in lists the
    /// Work Bench), back again (the wood home in the bag), the bag shut or the item gone (the same); the stat viewer's, the
    /// drop blacklist's and the monster book's keys unbound at first, in the key list, each opening its window (the bag
    /// with it) and shutting it. Lines "[AutoTest] GuideAdvice …".
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator GuideAdviceTest()
        {
            if (!DevCopy.Verified) throw new InvalidOperationException("Isolated saves required");
            int pass = 0, total = 0;
            void Check(string name, bool ok, string detail = "")
            { total++; if (ok) pass++; Plugin.Log.LogInfo($"[AutoTest] GuideAdvice {name}：{detail} {(ok ? "通过" : "不通过")}"); }
            Player.TerraStats.DevImmortal = true; PlayerData.instance.isInvincible = true; mod.DevNoFallDamage = true;
            yield return GoTo("Town", "left1"); yield return new WaitForSeconds(3f);
            var inv = mod.Inventory;
            var oldSlots = (ItemStack[])inv.Slots.Clone();
            bool infinite = inv.Infinite;
            try
            {
                inv.Clear(); inv.Mouse = default;
                inv.Infinite = false; TerraMod.DevAllInfinite = false; mod.ApplyCreative(false);

                var def = ItemDb.Get(ItemType.GuideAdvice);
                Check("名字和描述", def.Name == "向导的叮嘱" && def.Tooltip == "拿着我的合成表滚吧，把我丢弃在圣巢的烂人", $"{def.Name}：{def.Tooltip}");
                var tex = TerrariaAssets.Texture(def.Texture);
                Check("图标是像素图", tex != null && tex.width == 37 && tex.height == 47 && tex.filterMode == FilterMode.Point, tex != null ? $"{tex.width}x{tex.height} {tex.filterMode}" : "无");
                var guide = Array.Find(TownNpcs.AllDefs, d => d.Internal == "Guide");
                var stock = guide?.Items?.FirstOrDefault(s => s.Type == ItemType.GuideAdvice);
                Check("向导开局就卖", stock != null && stock.Stage == 0, stock != null ? $"阶段 {stock.Stage}" : "没有");
                long price = Shop.PriceOf(def);
                Check("售价 10000 吉欧", price == 1000000, Shop.Money(price));

                InventoryGui.Open = true; yield return new WaitForSeconds(.3f);
                Check("没带着时不能切换", !GuideAdvice.Carried && !TownNpcs.GuideCraftOpen && InventoryGui.DevCraftTitle == "制作");
                inv.Slots[20] = ItemStack.Of(ItemType.GuideAdvice);
                inv.Slots[21] = ItemStack.Of(ItemType.Wood, 30);
                // The bag's wood, all told (a new save starts with some).
                int wood = Recipes.Count(inv, ItemType.Wood);
                yield return null;
                InventoryGui.DevGuideBookToggle(); yield return new WaitForSeconds(.3f);
                Check("带着时切换为合成表", GuideAdvice.On && TownNpcs.GuideCraftOpen && InventoryGui.DevCraftTitle == "合成表", InventoryGui.DevCraftTitle);
                Shot("Town", "guideadvice_empty");
                yield return new WaitForSeconds(.3f);
                inv.Mouse = inv.Slots[21]; inv.Slots[21] = default;
                InventoryGui.DevGuideSlot(); yield return new WaitForSeconds(.3f);
                string names = InventoryGui.DevRecipeNames(200);
                Check("放入木材列出用到它的配方", TownNpcs.GuideItem.Type == ItemType.Wood && inv.Mouse.IsEmpty && names.Contains("工作台"), names.Length > 120 ? names.Substring(names.Length - 120) : names);
                Shot("Town", "guideadvice_wood");
                yield return new WaitForSeconds(.3f);
                InventoryGui.DevGuideBookToggle(); yield return new WaitForSeconds(.3f);
                Check("切回制作，木材回到背包", !GuideAdvice.On && TownNpcs.GuideItem.IsEmpty && Recipes.Count(inv, ItemType.Wood) == wood && InventoryGui.DevCraftTitle == "制作",
                    $"槽里 {TownNpcs.GuideItem.Def?.Name}，背包木材 {Recipes.Count(inv, ItemType.Wood)}");

                // Switched on, wood in, the bag shut: the wood home.
                InventoryGui.DevGuideBookToggle(); yield return null;
                PutWood(inv);
                InventoryGui.DevGuideSlot(); yield return null;
                InventoryGui.Open = false; yield return new WaitForSeconds(.3f);
                Check("关背包时木材回到背包", TownNpcs.GuideItem.IsEmpty && Recipes.Count(inv, ItemType.Wood) == wood, $"背包木材 {Recipes.Count(inv, ItemType.Wood)}");
                InventoryGui.Open = true; yield return new WaitForSeconds(.3f);
                Check("再开背包还是合成表", GuideAdvice.On && InventoryGui.DevCraftTitle == "合成表");
                // The item gone (sold, dropped): the crafting column again, the wood home.
                PutWood(inv);
                InventoryGui.DevGuideSlot(); yield return null;
                inv.Slots[20] = default;
                yield return new WaitForSeconds(.3f);
                Check("物品不在身上就回到制作", !GuideAdvice.On && !TownNpcs.GuideCraftOpen && TownNpcs.GuideItem.IsEmpty && Recipes.Count(inv, ItemType.Wood) == wood,
                    $"槽里 {TownNpcs.GuideItem.Def?.Name}，背包木材 {Recipes.Count(inv, ItemType.Wood)}");
                // In the Hallownest bag it counts too.
                inv.HkBag[0] = ItemStack.Of(ItemType.GuideAdvice);
                yield return null;
                Check("放在圣巢背包里也算带着", GuideAdvice.Carried && GuideAdvice.On);
                inv.HkBag[0] = default;
                if (GuideAdvice.Chosen) GuideAdvice.Toggle();

                // The windows' keys.
                var keys = new[] { Player.TerraKeys.Action.StatViewer, Player.TerraKeys.Action.DropBlacklist, Player.TerraKeys.Action.Bestiary };
                Check("三个按键默认不绑定", keys.All(k => Player.TerraKeys.Default(k) == Key.None));
                Check("设置的按键列表里有它们", keys.All(k => Array.IndexOf(Player.TerraKeys.Ordered, k) >= 0) && Player.TerraKeys.Ordered.Length == Player.TerraKeys.All.Length,
                    string.Join("、", keys.Select(Player.TerraKeys.Name)));
                InventoryGui.Open = false; yield return null;
                InventoryGui.PanelKey(0); yield return new WaitForSeconds(.3f);
                Check("属性仪键：背包关着时连背包一起打开", InventoryGui.Open && InventoryGui.DevStatsOpen);
                Shot("Town", "guideadvice_statskey");
                InventoryGui.PanelKey(0); yield return null;
                Check("再按一次关掉属性仪，背包留着", InventoryGui.Open && !InventoryGui.DevStatsOpen);
                InventoryGui.PanelKey(1); yield return null;
                Check("黑名单键打开黑名单", InventoryGui.DevBlacklistOpen);
                InventoryGui.PanelKey(2); yield return null;
                Check("图鉴键打开图鉴，黑名单收起", InventoryGui.BestiaryOpen && !InventoryGui.DevBlacklistOpen);
                InventoryGui.PanelKey(2); yield return null;
                Check("再按一次关掉图鉴", !InventoryGui.BestiaryOpen);
                InventoryGui.Open = false; yield return null;
                InventoryGui.PanelKey(2); yield return new WaitForSeconds(.3f);
                Check("背包关着时图鉴键连背包一起打开", InventoryGui.Open && InventoryGui.BestiaryOpen);
                InventoryGui.PanelKey(2); yield return null;
                InventoryGui.PanelKey(1); yield return null;
                Check("背包开着时黑名单键只开黑名单", InventoryGui.Open && InventoryGui.DevBlacklistOpen);
                InventoryGui.Open = false; yield return null;
            }
            finally
            {
                if (GuideAdvice.Chosen) GuideAdvice.Toggle();
                TownNpcs.ReturnGuideItem();
                Array.Copy(oldSlots, inv.Slots, oldSlots.Length);
                inv.Mouse = default;
                inv.Infinite = infinite;
                InventoryGui.Open = false;
            }
            Plugin.Log.LogInfo($"[AutoTest] GuideAdvice 结果：{pass}/{total} 通过");
        }

        /// <summary>The bag's wood onto the cursor (to put in the Guide's slot).</summary>
        static void PutWood(Inventory inv)
        {
            for (int i = 0; i < inv.Slots.Length; i++)
                if (inv.Slots[i].Type == ItemType.Wood) { inv.Mouse = inv.Slots[i]; inv.Slots[i] = default; return; }
        }
    }
}
