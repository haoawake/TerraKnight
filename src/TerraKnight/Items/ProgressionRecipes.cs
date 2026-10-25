using System;
using System.Collections.Generic;

namespace TerraKnight.Items
{
    internal static partial class ProgressionBalance
    {
        // RecipeData's old StationStage also encoded the old item tier. The physical
        // Terraria station is unchanged; its actual acquisition tier is the gate now.
        static int StationStage(Recipe r, Func<ItemType,ItemDef> item)
        {
            ItemType station;
            switch(r.Station)
            {
                case "": case "徒手": case "工作台": case "砧": case "熔炉":
                case "锯木机": case "织布机": case "玻璃瓶": case "烹饪锅":
                case "恶魔祭坛 / 猩红祭坛": return 0;
                case "地狱熔炉": station=ItemType.Hellforge; break;
                case "秘银砧": station=ItemType.MythrilAnvil; break;
                case "精金熔炉": station=ItemType.AdamantiteForge; break;
                case "工匠作坊": station=ItemType.TinkerersWorkshop; break;
                case "水晶球": station=ItemType.CrystalBall; break;
                case "书架": station=ItemType.Bookcase; break;
                case "自动锤炼机": station=ItemType.Autohammer; break;
                case "远古操纵机": station=ItemType.LunarCraftingStation; break;
                case "固化机": station=ItemType.Solidifier; break;
                case "染缸": station=ItemType.DyeVat; break;
                case "灌注站": station=ItemType.ImbuingStation; break;
                case "骨头焊机": station=ItemType.BoneWelder; break;
                default: return r.StationStage;
            }
            return item(station).Stage;
        }
        static int IngredientStage(Recipe r, Func<ItemType,ItemDef> item)
        {
            int stage=StationStage(r,item);
            if(r.Environment==CraftEnvironment.Graveyard) stage=Math.Max(stage,2);
            if(r.Environment==CraftEnvironment.Honey) stage=Math.Max(stage,Math.Min(4,item(ItemType.HoneyDispenser).Stage));
            foreach(var ing in r.Ingredients) stage=Math.Max(stage,item(ing.Type).Stage);
            return stage;
        }
        internal static void NormalizeRecipeStages(ItemDef[] items, List<Recipe> recipes)
        {
            // An optional high-tier recycling recipe must not delay a directly obtainable item.
            // For crafted items, use the earliest complete vanilla recipe, not a local substitute.
            for (int pass = 0; pass < items.Length; pass++)
            {
                var earliest = new Dictionary<ItemType, int>();
                foreach (var r in recipes)
                {
                    int stage = IngredientStage(r,t=>items[(int)t]);
                    if (!earliest.TryGetValue(r.Result, out int old) || stage < old) earliest[r.Result] = stage;
                }
                bool changed = false;
                foreach (var pair in earliest)
                {
                    var d = items[(int)pair.Key];
                    if (d.Source != ItemSource.Craft || d.Stage == pair.Value) continue;
                    d.Stage = pair.Value;
                    changed = true;
                }
                if (!changed) break;
            }
        }

        // Acquisition estimates for documentation/loot planning only. Crafting itself has no stage gate.
        internal static void ApplyRecipes(List<Recipe> recipes)
        {
            // Record the expected source tier without changing vanilla ingredients or stations.
            for(int i=0;i<recipes.Count;i++)
            {
                var r=recipes[i];
                if(r.IsBought) continue; // Preserve the original purchase cost; do not turn purchases into free recipes.
                int required=IngredientStage(r,ItemDb.Get);
                if(required!=r.StationStage)
                    recipes[i]=new Recipe(r.Result,r.Count,r.Station,required,r.Ingredients) { Environment=r.Environment, IngredientAlternative=r.IngredientAlternative };
            }
        }
    }
}
