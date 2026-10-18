using System.Collections.Generic;
using TerraKnight.Items;

namespace TerraKnight.Town
{
    /// <summary>
    /// What the town NPCs sell beyond the generated stock (TownShopData.cs): the crafting and buff stations
    /// Terraria's own shops have (Chest.SetupShop): the Witch Doctor's Imbuing Station and Bewitching Table, the
    /// Goblin Tinkerer's Sharpening Station, the Steampunker's Steampunk Boiler and Blend-O-Matic, the Arms
    /// Dealer's Ammo Box, the Party Girl's Slice of Cake, each from its stage; the Tavernkeep's Old One's Army armor at
    /// him (shop 21), tier 2 after the mechanical bosses and tier 3 after Golem, beside his sentry rods; the Clothier's
    /// Pink Thread at the Merchant.
    /// </summary>
    internal static class TownShopExtra
    {
        static readonly (string Npc, ItemType Type, int Stage)[] extra =
        {
            ("WitchDoctor", ItemType.ImbuingStation, 2), ("WitchDoctor", ItemType.BewitchingTable, 2),
            ("GoblinTinkerer", ItemType.SharpeningStation, 2),
            ("Steampunker", ItemType.SteampunkBoiler, 4), ("Steampunker", ItemType.BlendOMatic, 4),
            ("ArmsDealer", ItemType.AmmoBox, 4), ("PartyGirl", ItemType.SliceOfCake, 4),
            ("Merchant", ItemType.PinkThread, 0),
            // Hallownest's original terrain mines as stone, so early potion
            // crafting cannot assume Terraria's naturally available sand/glass.
            // One purchased bottle supplies the original placed-bottle station.
            ("Merchant", ItemType.Bottle, 0),
            ("DD2Bartender", ItemType.ApprenticeHat, 4), ("DD2Bartender", ItemType.ApprenticeRobe, 4), ("DD2Bartender", ItemType.ApprenticeTrousers, 4),
            ("DD2Bartender", ItemType.SquireGreatHelm, 4), ("DD2Bartender", ItemType.SquirePlating, 4), ("DD2Bartender", ItemType.SquireGreaves, 4),
            ("DD2Bartender", ItemType.HuntressWig, 4), ("DD2Bartender", ItemType.HuntressJerkin, 4), ("DD2Bartender", ItemType.HuntressPants, 4),
            ("DD2Bartender", ItemType.MonkBrows, 4), ("DD2Bartender", ItemType.MonkShirt, 4), ("DD2Bartender", ItemType.MonkPants, 4),
            ("DD2Bartender", ItemType.SquireAltHead, 5), ("DD2Bartender", ItemType.SquireAltShirt, 5), ("DD2Bartender", ItemType.SquireAltPants, 5),
            ("DD2Bartender", ItemType.ApprenticeAltHead, 5), ("DD2Bartender", ItemType.ApprenticeAltShirt, 5),
            ("DD2Bartender", ItemType.ApprenticeAltPants, 5), ("DD2Bartender", ItemType.HuntressAltHead, 5),
            ("DD2Bartender", ItemType.HuntressAltShirt, 5), ("DD2Bartender", ItemType.HuntressAltPants, 5), ("DD2Bartender", ItemType.MonkAltHead, 5),
            ("DD2Bartender", ItemType.MonkAltShirt, 5), ("DD2Bartender", ItemType.MonkAltPants, 5),
        };

        public static Stock[] With(string npc, Stock[] items)
        {
            List<Stock> more = null;
            foreach (var (n, t, stage) in System.Linq.Enumerable.Concat(extra, CraftingSupplementShops.All))
            {
                if (n != npc) continue;
                if (more == null) more = new List<Stock>(items);
                more.Add(new Stock(t, stage, null));
            }
            return more != null ? more.ToArray() : items;
        }
    }
}
