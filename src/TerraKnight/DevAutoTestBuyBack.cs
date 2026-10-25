using System.Collections;
using System.Linq;
using TerraKnight.Items;
using TerraKnight.Town;
using UnityEngine;

namespace TerraKnight
{
    /// <summary>
    /// Dev (suite "buyback"): a player's report — weapons and armour sold to a townsperson can't be bought back for what
    /// they fetched. At the Merchant's: something bought from the stock and sold straight back (Terraria's
    /// ItemShopSellbackHelper: what it cost, back to the stock); a forged weapon and a worthless crafted one sold and
    /// bought back (for exactly what each fetched); the purse before and after each, in copper.
    /// </summary>
    internal sealed partial class DevAutoTest
    {
        IEnumerator BuyBackTest()
        {
            yield return GoTo("Town", "left1");
            yield return new WaitForSeconds(2f);
            PlayerData.instance.isInvincible = true;
            var hero = HeroController.instance;
            var inv = mod.Inventory;
            var pos = TownNpcs.DevPos("Merchant");
            if (!pos.HasValue) { Plugin.Log.LogInfo("[AutoTest] 回购 商人不在德特茅斯 不通过"); yield break; }
            float bottom = hero.GetComponent<BoxCollider2D>().bounds.min.y - hero.transform.position.y;
            hero.transform.position = Clear(hero, new Vector2(pos.Value.x - 1.5f, pos.Value.y - 20f / 32f - bottom + 0.05f));
            hero.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
            yield return new WaitForSeconds(0.6f);
            TownNpcs.TryRightClick(TownNpcs.DevPos("Merchant").Value);
            yield return new WaitForSeconds(0.3f);
            TownNpcs.OpenShop();
            yield return new WaitForSeconds(0.6f);
            Shop.Earn(5000000);
            int ok = 0, all = 0;
            void Check(string what, bool pass, string detail)
            {
                all++;
                if (pass) ok++;
                Plugin.Log.LogInfo($"[AutoTest] 回购 {what}：{detail} {(pass ? "通过" : "不通过")}");
            }
            int SlotOf(ItemType t)
            {
                for (int i = 0; i < TownNpcs.ShopItems.Length; i++) if (TownNpcs.BuyBack[i] && TownNpcs.ShopItems[i].Type == t) return i;
                return -1;
            }

            // Bought from the stock, sold straight back: what it cost comes back, and it isn't a buy-back slot.
            int stockSlot = -1;
            for (int i = 0; i < TownNpcs.ShopItems.Length; i++)
                if (!TownNpcs.ShopItems[i].IsEmpty && !TownNpcs.BuyBack[i] && !TownNpcs.Locked(i) && TownNpcs.ShopItems[i].Def.Value > 0) { stockSlot = i; break; }
            if (stockSlot >= 0)
            {
                inv.Mouse = default;
                long p0 = Shop.Purse, price = TownNpcs.BuyPrice(stockSlot);
                var type = TownNpcs.ShopItems[stockSlot].Type;
                TownNpcs.Buy(stockSlot, 1, inv);
                long p1 = Shop.Purse;
                var stack = inv.Mouse;
                TownNpcs.Sell(ref stack);
                inv.Mouse = stack;
                long p2 = Shop.Purse;
                Check($"买了 {ItemDb.Get(type).Name} 再卖回去", p1 == p0 - price && p2 == p0 && SlotOf(type) < 0,
                    $"价格 {price}，钱 {p0}→{p1}→{p2}，回购格 {SlotOf(type)}");
            }
            else Check("买了再卖回去", false, "商店里没有能买的");

            foreach (var (label, item) in new[]
            {
                ("锻造过的武器", new ItemStack { Type = ItemType.IronBroadsword, Count = 1, Forge = 6 }),
                ("值钱的锻造武器", new ItemStack { Type = ItemDb.All.First(d => d != null && d.Type != ItemType.None && d.IsWeapon && d.MaxStack == 1 && d.Value > 0
                    && !Recipes.Worthless(d.Type) && Forging.Forgeable(d)).Type, Count = 1, Forge = 8 }),
                ("不值钱的合成武器", ItemStack.Of(ItemDb.All.First(d => d != null && d.Type != ItemType.None && d.IsWeapon && d.MaxStack == 1 && Recipes.Worthless(d.Type)).Type)),
                ("普通盔甲", ItemStack.Of(ItemType.IronHelmet)),
            })
            {
                var stack = item;
                long value = Shop.SellValue(stack);
                long p0 = Shop.Purse;
                TownNpcs.Sell(ref stack);
                long p1 = Shop.Purse;
                int slot = SlotOf(item.Type);
                long back = slot >= 0 ? TownNpcs.BuyPrice(slot) : -1;
                inv.Mouse = default;
                bool bought = slot >= 0 && TownNpcs.Buy(slot, 1, inv);
                long p2 = Shop.Purse;
                bool same = inv.Mouse.Type == item.Type && inv.Mouse.Forge == item.Forge;
                Check($"{label} {ItemDb.Get(item.Type).Name} 卖了再买回", p1 == p0 + value && back == value && (bought || value == 0 && slot >= 0) && p2 == p0 && same,
                    $"卖得 {value}，钱 {p0}→{p1}→{p2}，回购价 {back}，买回 {bought}，拿到 {inv.Mouse.Type} 锻造 {inv.Mouse.Forge}");
                inv.Mouse = default;
            }
            TownNpcs.Close(false);
            Plugin.Log.LogInfo($"[AutoTest] 回购 合计 {ok}/{all} {(ok == all ? "通过" : "不通过")}");
        }
    }
}
