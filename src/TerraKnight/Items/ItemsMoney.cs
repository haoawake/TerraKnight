using System.Collections.Generic;

namespace TerraKnight.Items
{
    internal static partial class ItemDb
    {
        /// <summary>
        /// Money as an item (GeoWallet): the Geo, one stack up to int.MaxValue, shown with the game's
        /// own geo picture; and the Piggy Bank (item 87), the Merchant's, set down like furniture and
        /// opened with a right click onto the player's own bank (Player.bank, 40 slots, the same in
        /// every Piggy Bank), which buying also draws on.
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
                Type = ItemType.PiggyBank, Name = "猪猪存钱罐", Texture = "Item_87", PlacesStation = World.StationKind.PiggyBank, Consumable = true,
                MaxStack = 9999, UseAnimation = 15 * Tick, UseTime = 10 * Tick, Tooltip = "存储物品的私人仓库",
            });
        }

        static IEnumerable<(ItemType, int, ItemSource, int)> MoneySources()
        {
            yield return (ItemType.Geo, 0, ItemSource.Drop, 0);
            yield return (ItemType.PiggyBank, 0, ItemSource.Shop, 10000);
        }
    }
}
