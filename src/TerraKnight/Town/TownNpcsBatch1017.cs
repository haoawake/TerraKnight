namespace TerraKnight.Town
{
    internal static partial class TownNpcs
    {
        static bool batch1017Stocked;

        static TownNpcs() => AddBatch1017Stock();

        /// <summary>
        /// The 10-17 batch's stock (the roster is generated): the Merchant's Safe (Chest.SetupShop, after Skeletron) and
        /// here his too the Money Trough (a Blood Moon's in Terraria, no one's shop there); the Tavernkeep's Defender's
        /// Forge (shop 21).
        /// </summary>
        internal static void AddBatch1017Stock()
        {
            if (batch1017Stocked) return;
            batch1017Stocked = true;
            foreach (var npc in System.Linq.Enumerable.Concat(roster, more))
            {
                if (npc.Internal != "Merchant" && npc.Internal != "DD2Bartender") continue;
                var list = new System.Collections.Generic.List<Stock>(npc.Items);
                if (npc.Internal == "Merchant")
                {
                    list.Add(new Stock(Items.ItemType.MoneyTrough, 1, null));
                    list.Add(new Stock(Items.ItemType.Safe, 2, null));
                }
                else list.Add(new Stock(Items.ItemType.DefendersForge, 3, null));
                npc.Items = list.ToArray();
            }
        }
    }
}
