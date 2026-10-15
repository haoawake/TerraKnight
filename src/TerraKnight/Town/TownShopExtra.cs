using System.Collections.Generic;
using TerraKnight.Items;

namespace TerraKnight.Town
{
    /// <summary>
    /// What the town NPCs sell beyond the generated stock (TownShopData.cs): the crafting and buff stations
    /// Terraria's own shops have (Chest.SetupShop): the Witch Doctor's Imbuing Station and Bewitching Table, the
    /// Goblin Tinkerer's Sharpening Station, the Steampunker's Steampunk Boiler and Blend-O-Matic, the Arms
    /// Dealer's Ammo Box, the Party Girl's Slice of Cake, each from its stage; the Tavernkeep's Old One's Army armor, tier
    /// 2 after the mechanical bosses at the Witch Doctor (who sells its sentries here), tier 3 after Golem at the Cyborg
    /// (the Witch Doctor's 40 slots are full by then); the Clothier's Pink Thread at the Merchant.
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
            ("WitchDoctor", ItemType.ApprenticeHat, 4), ("WitchDoctor", ItemType.ApprenticeRobe, 4), ("WitchDoctor", ItemType.ApprenticeTrousers, 4),
            ("WitchDoctor", ItemType.SquireGreatHelm, 4), ("WitchDoctor", ItemType.SquirePlating, 4), ("WitchDoctor", ItemType.SquireGreaves, 4),
            ("WitchDoctor", ItemType.HuntressWig, 4), ("WitchDoctor", ItemType.HuntressJerkin, 4), ("WitchDoctor", ItemType.HuntressPants, 4),
            ("WitchDoctor", ItemType.MonkBrows, 4), ("WitchDoctor", ItemType.MonkShirt, 4), ("WitchDoctor", ItemType.MonkPants, 4),
            ("Cyborg", ItemType.SquireAltHead, 5), ("Cyborg", ItemType.SquireAltShirt, 5), ("Cyborg", ItemType.SquireAltPants, 5),
            ("Cyborg", ItemType.ApprenticeAltHead, 5), ("Cyborg", ItemType.ApprenticeAltShirt, 5),
            ("Cyborg", ItemType.ApprenticeAltPants, 5), ("Cyborg", ItemType.HuntressAltHead, 5),
            ("Cyborg", ItemType.HuntressAltShirt, 5), ("Cyborg", ItemType.HuntressAltPants, 5), ("Cyborg", ItemType.MonkAltHead, 5),
            ("Cyborg", ItemType.MonkAltShirt, 5), ("Cyborg", ItemType.MonkAltPants, 5),
        };

        public static Stock[] With(string npc, Stock[] items)
        {
            List<Stock> more = null;
            foreach (var (n, t, stage) in extra)
            {
                if (n != npc) continue;
                if (more == null) more = new List<Stock>(items);
                more.Add(new Stock(t, stage, null));
            }
            return more != null ? more.ToArray() : items;
        }
    }
}
