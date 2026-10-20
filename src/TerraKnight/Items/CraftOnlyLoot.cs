using System;
using System.Collections.Generic;

namespace TerraKnight.Items
{
    // Source is the vanilla acquisition classification, not "has a recipe".
    // A Blizzard in a Bottle / Band of Starpower can have a recipe AND a world
    // source; those remain Drop. Crafted equipment and crafting stations are Craft.
    internal static class CraftOnlyLoot
    {
        static readonly Dictionary<IReadOnlyList<EnemyDrops.Drop>,IReadOnlyList<EnemyDrops.Drop>> cache=
            new Dictionary<IReadOnlyList<EnemyDrops.Drop>,IReadOnlyList<EnemyDrops.Drop>>();

        internal static bool OnlyCrafted(ItemType type)=>ItemDb.Get(type).Source==ItemSource.Craft;

        internal static IReadOnlyList<EnemyDrops.Drop> Filter(IReadOnlyList<EnemyDrops.Drop> drops)
        {
            if(cache.TryGetValue(drops,out var cached))return cached;
            bool changed=false;
            var totals=new Dictionary<int,float>();
            var reserved=new Dictionary<int,float>();
            foreach(var row in drops) {
                changed|=OnlyCrafted(row.Type);
                if(row.Group<=0)continue;
                totals[row.Group]=(totals.TryGetValue(row.Group,out float sum) ? sum : 0)+ProgressionLoot.Weight(row);
                reserved[row.Group]=Math.Max(reserved.TryGetValue(row.Group,out float old) ? old : 0,row.GroupWeightTotal);
            }
            if(!changed)return drops;
            var kept=new List<EnemyDrops.Drop>();
            foreach(var row in drops) {
                if(OnlyCrafted(row.Type))continue;
                var copy=row;
                // Deleted outcomes become empty outcomes. Renormalising the
                // remaining weights would silently raise every legal item's rate.
                if(row.Group>0)copy.GroupWeightTotal=Math.Max(totals[row.Group],reserved[row.Group]);
                kept.Add(copy);
            }
            if(cache.Count>4000)cache.Clear();
            return cache[drops]=kept.ToArray();
        }
    }
}
