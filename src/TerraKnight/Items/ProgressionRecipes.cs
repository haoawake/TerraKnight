using System;
using System.Collections.Generic;

namespace TerraKnight.Items
{
    internal static partial class ProgressionBalance
    {
        internal static void NormalizeRecipeStages(ItemDef[] items, List<Recipe> recipes)
        {
            // An optional high-tier recycling recipe must not delay a directly obtainable item.
            // For crafted items, use the earliest complete vanilla recipe, not a local substitute.
            for (int pass = 0; pass < 6; pass++)
            {
                var earliest = new Dictionary<ItemType, int>();
                foreach (var r in recipes)
                {
                    int stage = r.StationStage;
                    if (r.Environment == CraftEnvironment.Graveyard) stage = Math.Max(stage, 2);
                    if (r.Environment == CraftEnvironment.Honey) stage = Math.Max(stage, Math.Min(4, items[(int)ItemType.HoneyDispenser].Stage));
                    foreach (var ing in r.Ingredients) stage = Math.Max(stage, items[(int)ing.Type].Stage);
                    if (!earliest.TryGetValue(r.Result, out int old) || stage < old) earliest[r.Result] = stage;
                }
                bool changed = false;
                foreach (var pair in earliest)
                {
                    var d = items[(int)pair.Key];
                    if (d.Source != ItemSource.Craft || d.Stage >= pair.Value) continue;
                    d.Stage = pair.Value;
                    changed = true;
                }
                if (!changed) break;
            }
        }

        // Progression only gates recipes; Terraria ingredients, quantities and stations stay intact.
        internal static void ApplyRecipes(List<Recipe> recipes)
        {
            // Smelting and non-curated recipes cannot open before their ingredients/result tiers.
            for(int i=0;i<recipes.Count;i++)
            {
                var r=recipes[i];
                if(r.IsBought) continue; // Preserve the original purchase cost; do not turn purchases into free recipes.
                int required=Math.Max(r.StationStage,r.Def.Stage);
                if (r.Environment == CraftEnvironment.Graveyard) required = Math.Max(required, 2);
                if (r.Environment == CraftEnvironment.Honey) required = Math.Max(required, Math.Min(4, ItemDb.Get(ItemType.HoneyDispenser).Stage));
                foreach(var ing in r.Ingredients) required=Math.Max(required,ItemDb.Get(ing.Type).Stage);
                if(required!=r.StationStage)
                    recipes[i]=new Recipe(r.Result,r.Count,r.Station,required,r.Ingredients) { Environment=r.Environment };
            }
        }
    }
}
