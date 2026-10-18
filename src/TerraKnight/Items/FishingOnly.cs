using System.Collections.Generic;

namespace TerraKnight.Items
{
    internal static partial class Fishing
    {
        /// <summary>
        /// What only fishing gives: every catch of Terraria's catch rules and the Angler's quest fish (the fish, the fish
        /// weapons and tools, the seafood, the fishing accessories), less what the waters only also give — the crates, the
        /// junk, the two crafting tables, the Frog Leg and Cap'n Tunabeard's hat.
        /// </summary>
        static HashSet<ItemType> fishOnly;

        static readonly HashSet<ItemType> alsoElsewhere = new HashSet<ItemType>
        {
            ItemType.JojaCola, ItemType.OldShoe, ItemType.FishingSeaweed, ItemType.TinCan, ItemType.AlchemyTable, ItemType.BewitchingTable,
            ItemType.FrogLeg, ItemType.CapnTunabeard,
        };

        public static bool FishOnly(ItemType t)
        {
            if (fishOnly == null)
            {
                var set = new HashSet<ItemType>();
                foreach (var r in rules)
                    foreach (var i in r.Items)
                        if (i != ItemType.None && !alsoElsewhere.Contains(i) && !i.ToString().Contains("Crate")) set.Add(i);
                foreach (var q in quests) set.Add(q.Fish);
                fishOnly = set;
            }
            return fishOnly.Contains(t);
        }
    }
}
