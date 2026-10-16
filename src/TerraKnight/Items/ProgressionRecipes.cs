using System;
using System.Collections.Generic;

namespace TerraKnight.Items
{
    internal static partial class ProgressionBalance
    {
        internal static ItemType LocalMaterial(int stage,int cls,bool armour=false)
        {
            ItemType[][] materials = {
                new[] {ItemType.IronBar,ItemType.Wood,ItemType.Amethyst,ItemType.Gel},
                new[] {ItemType.GoldBar,ItemType.SturdyFossil,ItemType.JungleSpores,ItemType.FlinxFur},
                new[] {ItemType.DemoniteBar,ItemType.Bone,ItemType.MeteoriteBar,ItemType.Obsidian},
                new[] {ItemType.HellstoneBar,ItemType.Silk,ItemType.JungleSpores,ItemType.SpiderFang},
                new[] {ItemType.CobaltBar,ItemType.CobaltBar,ItemType.CrystalShard,ItemType.BeeWax},
                new[] {ItemType.ChlorophyteBar,ItemType.ChlorophyteBar,ItemType.CrystalShard,ItemType.SpiderFang}
            };
            if(armour && stage==5) return cls==0 ? ItemType.BeetleHusk : cls==1 ? ItemType.ChlorophyteBar : cls==2 ? ItemType.Ectoplasm : ItemType.SpookyWood;
            if(armour && stage==4) return cls==0 ? ItemType.TitaniumBar : cls==1 || cls==2 ? ItemType.HallowedBar : ItemType.BeeWax;
            return materials[stage][cls];
        }
        internal static void ApplyRecipes(List<Recipe> recipes)
        {
            void Replace(Recipe r)
            { recipes.RemoveAll(old=>old.Result==r.Result); recipes.Add(r); }
            for(int s=0;s<6;s++)
            {
                for(int cls=0;cls<4;cls++)
                {
                    // Two reliable paths per class; boss candidates retain their distinct drop route.
                    for(int i=0;i<2;i++)
                        Replace(new Recipe(Core[s][cls][i],1,s==0 ? "工作台" : "砧",s,
                            (LocalMaterial(s,cls),s==5 && cls<2 ? 4 : 6+s*2),(ItemType.Wood,4)));
                    foreach(var t in AccessoryPaths[s][cls])
                    {
                        int actual=ItemDb.Get(t).Stage;
                        if(actual!=s) continue; // Shared lower-tier support accessories keep the earlier recipe.
                        Replace(new Recipe(t,1,s==0 ? "工作台" : "砧",s,
                            (LocalMaterial(s,cls),3+s),(ItemType.Wood,4)));
                    }
                }
            }
            var made=new HashSet<ItemType>();
            foreach(var path in ArmorPaths)
            {
                int cls=ClassIndex(path.Class),s=path.Stage;
                for(int slot=0;slot<3;slot++)
                {
                    var t=path.Items[slot];
                    if(!made.Add(t) || ItemDb.Get(t).Stage!=s) continue;
                    int cost=(slot==1 ? 5 : slot==0 ? 3 : 4)+s;
                    Replace(new Recipe(t,1,s==0 ? "工作台" : "砧",s,
                        (LocalMaterial(s,cls,true),cost),(ItemType.Wood,4)));
                }
            }
            Replace(new Recipe(ItemType.WoodenSword,1,"工作台",0,(ItemType.Wood,7)));
            Replace(new Recipe(ItemType.WoodenBow,1,"工作台",0,(ItemType.Wood,10)));
            Replace(new Recipe(ItemType.IronBroadsword,1,"砧",0,(ItemType.IronBar,6)));
            // Alternative metals are separate recipes, never jointly consumed.
            recipes.Add(new Recipe(ItemType.IronBroadsword,1,"砧",0,(ItemType.LeadBar,6)));
            Replace(new Recipe(ItemType.Blowpipe,1,"工作台",0,(ItemType.Wood,8),(ItemType.Vine,1)));
            Replace(new Recipe(ItemType.WandofSparking,1,"工作台",0,(ItemType.Wood,8),(ItemType.FallenStar,1)));
            Replace(new Recipe(ItemType.AmethystStaff,1,"砧",0,(ItemType.CopperBar,6),(ItemType.Amethyst,2)));
            recipes.Add(new Recipe(ItemType.AmethystStaff,1,"砧",0,(ItemType.TinBar,6),(ItemType.Amethyst,2)));
            Replace(new Recipe(ItemType.FinchStaff,1,"工作台",0,(ItemType.Wood,8),(ItemType.Feather,2)));
            Replace(new Recipe(ItemType.SlimeStaff,1,"工作台",0,(ItemType.Wood,8),(ItemType.Gel,12)));
            Replace(new Recipe(ItemType.LeatherWhip,1,"工作台",0,(ItemType.Wood,6),(ItemType.Vine,2)));
            Replace(new Recipe(ItemType.PygmyNecklace,1,"工作台",0,(ItemType.Wood,6),(ItemType.Silk,1),(ItemType.Gel,4)));
            Replace(new Recipe(ItemType.BandofStarpower,1,"砧",0,(ItemType.FallenStar,3),(ItemType.IronBar,2)));
            Replace(new Recipe(ItemType.BandofRegeneration,1,"工作台",0,(ItemType.Gel,6),(ItemType.Daybloom,2)));
            Replace(new Recipe(ItemType.Silk,1,"工作台",0,(ItemType.Cobweb,7)));
            Replace(new Recipe(ItemType.Furnace,1,"工作台",0,(ItemType.StoneBlock,20),(ItemType.Wood,4)));
            Replace(new Recipe(ItemType.IronAnvil,1,"工作台",0,(ItemType.IronBar,5)));
            Replace(new Recipe(ItemType.Hellforge,1,"砧",3,(ItemType.StoneBlock,20),(ItemType.Obsidian,6)));
            Replace(new Recipe(ItemType.MythrilAnvil,1,"砧",4,(ItemType.IronAnvil,1),(ItemType.MythrilBar,6)));
            recipes.Add(new Recipe(ItemType.MythrilAnvil,1,"砧",4,(ItemType.IronAnvil,1),(ItemType.OrichalcumBar,6)));
            Replace(new Recipe(ItemType.AdamantiteForge,1,"砧",4,(ItemType.Hellforge,1),(ItemType.AdamantiteOre,12),(ItemType.StoneBlock,20)));
            recipes.Add(new Recipe(ItemType.AdamantiteForge,1,"砧",4,(ItemType.Hellforge,1),(ItemType.TitaniumOre,12),(ItemType.StoneBlock,20)));
            Replace(new Recipe(ItemType.BeeHeadgear,1,"砧",4,(ItemType.BeeWax,8)));
            Replace(new Recipe(ItemType.BeeBreastplate,1,"砧",4,(ItemType.BeeWax,12)));
            Replace(new Recipe(ItemType.BeeGreaves,1,"砧",4,(ItemType.BeeWax,10)));
            Replace(new Recipe(ItemType.NightsEdge,1,"砧",2,(ItemType.Muramasa,1),(ItemType.DemoniteBar,8),(ItemType.ShadowScale,6)));
            Replace(new Recipe(ItemType.ImpStaff,1,"砧",2,(ItemType.MeteoriteBar,8),(ItemType.FallenStar,3),(ItemType.Bone,4)));
            Replace(new Recipe(ItemType.MoltenFury,1,"砧",2,(ItemType.DemoniteBar,8),(ItemType.Bone,6),(ItemType.Wood,10)));
            // Smelting and non-curated recipes cannot open before their ingredients/result tiers.
            for(int i=0;i<recipes.Count;i++)
            {
                var r=recipes[i];
                if(r.IsBought) continue; // Preserve the original purchase cost; do not turn purchases into free recipes.
                int required=Math.Max(r.StationStage,r.Def.Stage);
                foreach(var ing in r.Ingredients) required=Math.Max(required,ItemDb.Get(ing.Type).Stage);
                if(required!=r.StationStage)
                    recipes[i]=new Recipe(r.Result,r.Count,r.Station,required,r.Ingredients) { Environment=r.Environment };
            }
        }
    }
}
