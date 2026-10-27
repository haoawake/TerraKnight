using System;
using System.Collections.Generic;

namespace TerraKnight.Items
{
    /// <summary>
    /// Terraria's vanity, worn by Hallownest's enemies big and small (the user's 10-25 "塞进大大小小的怪里"): each costume is
    /// one enemy kind's (a set by its first piece, the rest paid with it), the bosses' masks their bosses' as Terraria's bags
    /// give them (1 in 7), the developer sets whole with the piece a hardmode boss rolls (Player.TryGettingDevArmor). The
    /// table is VanityDropsData's (tools/vanity/gen_vanity.py).
    /// </summary>
    internal static partial class VanityDrops
    {
        static Dictionary<string, List<(ItemType Lead, int Den)>> byRecord;

        /// <summary>A kill's costume rows (ProgressionLoot.Of, beside the mounts'): its own, unlocked at the stage.</summary>
        internal static void AddDrops(List<EnemyDrops.Drop> rows, string key, int stage, bool entry, bool final, string scene = null)
        {
            if (byRecord == null)
            {
                byRecord = new Dictionary<string, List<(ItemType, int)>>();
                foreach (var (record, lead, den) in VanityDropsData())
                {
                    if (!byRecord.TryGetValue(record, out var list)) byRecord[record] = list = new List<(ItemType, int)>();
                    list.Add((lead, den));
                }
            }
            if (key == null || !byRecord.TryGetValue(key, out var mine)) return;
            // A Godhome rematch of a boss not yet beaten in the world pays nothing of its own yet (as its mounts).
            if (BossStats.InGodhome(scene) && VanillaBossProgression.All.ContainsKey(key) && !VanillaBossProgression.Cleared(key)) return;
            foreach (var (lead, den) in mine)
                if (ItemDb.Has(lead) && !CraftOnlyLoot.Blocked(lead) && ProgressionBalance.Unlocked(lead, stage, entry, final))
                    rows.Add(new EnemyDrops.Drop { Type = lead, Num = 1, Den = den, Min = 1, Max = 1, LockStage = 0 });
        }

        static IEnumerable<(string, ItemType, int)> VanityDropsData() => rows;

        /// <summary>The rest of the set a piece leads (paid with it), none for any other.</summary>
        internal static IReadOnlyList<ItemType> Mates(ItemType lead) => mates.TryGetValue(lead, out var m) ? m : (IReadOnlyList<ItemType>)Array.Empty<ItemType>();

        /// <summary>Every costume's carriers (the audits).</summary>
        internal static IEnumerable<(string Record, ItemType Lead, int Den)> All => rows;
    }
}
