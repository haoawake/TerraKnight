using System.Collections.Generic;

namespace TerraKnight.Items
{
    internal static partial class ItemDb
    {
        /// <summary>
        /// Money as an item (GeoWallet): the Geo, one stack up to int.MaxValue, shown with the game's
        /// own geo picture; the Dream Essence, Hallownest's other currency, the same way (EssenceWallet),
        /// with the HUD's essence picture; and the Piggy Bank (item 87), the Merchant's, set down like
        /// furniture and opened with a right click onto the player's own bank (Player.bank, 40 slots, the
        /// same in every Piggy Bank), which buying also draws on.
        /// </summary>
        static void AddMoney(List<ItemDef> list)
        {
            list.Add(new ItemDef
            {
                Type = ItemType.Geo, Name = "吉欧", Texture = GeoItem.Icon, MaxStack = int.MaxValue, UseSound = null,
                Tooltip = "圣巢的货币：向德特茅斯的居民买东西、付车站和长椅的钱都用它\n可以放进箱子和猪猪存钱罐，死后会被亡魂带走（存钱罐里的不会）",
            });
            list.Add(new ItemDef
            {
                Type = ItemType.DreamEssence, Name = "梦境精华", Texture = EssenceWallet.Icon, MaxStack = int.MaxValue, UseSound = null,
                Tooltip = "梦之钉收集的精华：先知按它的多少给奖励，用梦之门传送一次消耗一点\n背包和猪猪存钱罐里的都算数（放在箱子里的不算），死后不会丢失",
            });
            list.Add(new ItemDef
            {
                Type = ItemType.PiggyBank, Name = "猪猪存钱罐", Texture = "Item_87", PlacesStation = World.StationKind.PiggyBank, Consumable = true,
                MaxStack = 9999, UseAnimation = 15 * Tick, UseTime = 10 * Tick, Tooltip = "存储物品的私人仓库\n放进去的增益站（水晶球、施法桌、利器站、弹药箱、战争桌、蛋糕块）和增益家具（篝火、红心灯笼、星星瓶、巴斯特雕像、向日葵、花园侏儒）照样有增益",
            });
        }

        static IEnumerable<(ItemType, int, ItemSource, int)> MoneySources()
        {
            yield return (ItemType.Geo, 0, ItemSource.Drop, 0);
            yield return (ItemType.DreamEssence, 0, ItemSource.Drop, 0);
            yield return (ItemType.PiggyBank, 0, ItemSource.Shop, 10000);
        }
    }
}
