using System.Collections.Generic;
using TerraKnight.World;

namespace TerraKnight.Items
{
    internal static partial class ItemDb
    {
        /// <summary>
        /// The crafting stations that are set down (World.Stations) and the wood gear made at the
        /// first of them: as in Terraria, wood from a kill makes a Work Bench by hand, platforms and
        /// torches by hand too, and at the bench the Furnace, an anvil, wooden armor and weapons.
        /// The placed bottle (the Bottle itself) is the alchemy station.
        /// </summary>
        static void AddStations(List<ItemDef> list)
        {
            ItemDef Station(ItemType t, string name, int id, StationKind kind, int stage, ItemSource source, int value, string tip) => new ItemDef
            {
                Type = t, Name = name, Texture = "Item_" + id, PlacesStation = kind, Consumable = true, MaxStack = 9999, UseAnimation = 15 * Tick, UseTime = 10 * Tick,
                Stage = stage, Source = source, Value = value, Price = UnityEngine.Mathf.Max(1, UnityEngine.Mathf.CeilToInt(value / 100f)), Tooltip = tip,
            };
            list.Add(Station(ItemType.WorkBench, "工作台", 36, StationKind.WorkBench, 0, ItemSource.Craft, 30, "用于基础制作"));
            list.Add(Station(ItemType.Furnace, "熔炉", 33, StationKind.Furnace, 0, ItemSource.Craft, 300, "用于熔炼矿石"));
            list.Add(Station(ItemType.IronAnvil, "铁砧", 35, StationKind.IronAnvil, 0, ItemSource.Craft, 5000, "用于用锭制作物品"));
            list.Add(Station(ItemType.LeadAnvil, "铅砧", 716, StationKind.LeadAnvil, 0, ItemSource.Craft, 5000, "用于用锭制作物品"));
            list.Add(Station(ItemType.MythrilAnvil, "秘银砧", 525, StationKind.MythrilAnvil, 4, ItemSource.Craft, 39000, "用于用高级锭制作物品"));
            list.Add(Station(ItemType.OrichalcumAnvil, "山铜砧", 1220, StationKind.OrichalcumAnvil, 4, ItemSource.Craft, 39000, "用于用高级锭制作物品"));
            list.Add(Station(ItemType.Sawmill, "锯木机", 363, StationKind.Sawmill, 0, ItemSource.Craft, 1500, "用于高级木材制作"));
            list.Add(Station(ItemType.Loom, "织布机", 332, StationKind.Loom, 0, ItemSource.Craft, 300, "用于制作布料"));
            list.Add(Station(ItemType.TinkerersWorkshop, "工匠作坊", 398, StationKind.TinkerersWorkshop, 2, ItemSource.Shop, 100000, "可将某些配饰组合在一起"));
            var chain = Mat(ItemType.Chain, "链条", 85, 0, "材料");
            chain.Stage = 0;
            chain.Source = ItemSource.Craft;
            chain.Value = 150;
            list.Add(chain);
            // Wood armor (I:727-729): a point of defense each for the helmet and breastplate, one more for the set.
            list.Add(Armor(ItemType.WoodHelmet, "木头盔", 727, ArmorSlot.Head, 52, 1, ArmorSet.Wood, price: 1, tier: 0));
            list.Add(Armor(ItemType.WoodBreastplate, "木胸甲", 728, ArmorSlot.Body, 32, 1, ArmorSet.Wood, price: 1, tier: 0));
            list.Add(Armor(ItemType.WoodGreaves, "木护胫", 729, ArmorSlot.Legs, 31, 0, ArmorSet.Wood, price: 1, tier: 0));
            foreach (var t in new[] { ItemType.WoodHelmet, ItemType.WoodBreastplate, ItemType.WoodGreaves })
            {
                var d = list.Find(x => x.Type == t);
                d.Stage = 0;
                d.Source = ItemSource.Craft;
            }
            // The bottle stands on the ground as the alchemy station (tile 13).
            var bottle = list.Find(d => d.Type == ItemType.Bottle);
            if (bottle != null)
            {
                bottle.PlacesStation = StationKind.Bottle;
                bottle.Consumable = true;
                bottle.UseAnimation = 15 * Tick;
                bottle.UseTime = 10 * Tick;
            }
        }
    }

    internal static partial class Recipes
    {
        /// <summary>Terraria's recipes for the stations and the wood made by hand or at the bench (Recipe.cs, 1.4.5.8).</summary>
        static Recipe[] StationRecipes() => new[]
        {
            new Recipe(ItemType.Campfire, 1, "徒手", 0, (ItemType.Wood, 10), (ItemType.Torch, 5)),
            new Recipe(ItemType.WorkBench, 1, "徒手", 0, (ItemType.Wood, 10)), // Recipe.cs:4751
            new Recipe(ItemType.WoodPlatform, 2, "徒手", 0, (ItemType.Wood, 1)), // Recipe.cs:2376
            new Recipe(ItemType.Torch, 3, "徒手", 0, (ItemType.Gel, 1), (ItemType.Wood, 1)), // Recipe.cs:610
            new Recipe(ItemType.WoodenArrow, 25, "工作台", 0, (ItemType.Wood, 1), (ItemType.StoneBlock, 1)), // Recipe.cs:2077
            new Recipe(ItemType.Furnace, 1, "工作台", 0, (ItemType.StoneBlock, 20), (ItemType.Wood, 4), (ItemType.Torch, 3)), // Recipe.cs:9606
            new Recipe(ItemType.IronAnvil, 1, "工作台", 0, (ItemType.IronBar, 5)), // Recipe.cs:9822
            new Recipe(ItemType.LeadAnvil, 1, "工作台", 0, (ItemType.LeadBar, 5)), // Recipe.cs:9902
            new Recipe(ItemType.Chain, 15, "砧", 0, (ItemType.IronBar, 1)), // Recipe.cs:10056
            new Recipe(ItemType.Sawmill, 1, "工作台", 0, (ItemType.Wood, 10), (ItemType.IronBar, 2), (ItemType.Chain, 1)), // Recipe.cs:6743
            new Recipe(ItemType.Loom, 1, "锯木机", 0, (ItemType.Wood, 12)), // Recipe.cs:6694
            new Recipe(ItemType.WoodHelmet, 1, "工作台", 0, (ItemType.Wood, 20)), // Recipe.cs:6785
            new Recipe(ItemType.WoodBreastplate, 1, "工作台", 0, (ItemType.Wood, 30)), // Recipe.cs:6790
            new Recipe(ItemType.WoodGreaves, 1, "工作台", 0, (ItemType.Wood, 25)), // Recipe.cs:6795
            new Recipe(ItemType.MythrilAnvil, 1, "砧", 4, (ItemType.MythrilBar, 10)), // Recipe.cs:11536
            new Recipe(ItemType.OrichalcumAnvil, 1, "砧", 4, (ItemType.OrichalcumBar, 12)), // Recipe.cs:11606
        };
    }
}
