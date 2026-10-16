using System.Collections.Generic;
using TerraKnight.World;

namespace TerraKnight.Items
{
    internal static partial class ItemDb
    {
        /// <summary>
        /// The rest of Terraria's crafting stations as furniture set down (World.Stations): the Hellforge and the
        /// two forges, the Crystal Ball, the Bookcase, the Imbuing Station, the Autohammer, the Ancient
        /// Manipulator, the Solidifier, the Keg, the Cooking Pot, the Alchemy Table, the Heavy Work Bench, the
        /// themed ones (Bone Welder, Glass Kiln, Living Loom, Sky Mill, Ice Machine, Lihzahrd Furnace, Steampunk
        /// Boiler, Flesh Cloning Vat, Honey Dispenser, Blend-O-Matic, Meat Grinder), and the buff ones a right
        /// click on gives a buff from (the Ammo Box, the Bewitching Table, the Sharpening Station, the War Table,
        /// the Slice of Cake; the Crystal Ball too). Names and tooltips are zh-Hans (Item.SetDefaults).
        /// </summary>
        static void AddStations2(List<ItemDef> list)
        {
            ItemDef S(ItemType t, string name, int id, StationKind kind, int rare, string tip) => new ItemDef
            {
                Type = t, Name = name, Texture = "Item_" + id, PlacesStation = kind, Consumable = true, MaxStack = 9999, UseAnimation = 15 * Tick, UseTime = 10 * Tick,
                Rare = rare, Tooltip = tip,
            };
            list.Add(S(ItemType.Campfire, "篝火", 966, StationKind.Campfire, 0, "用于烤制焦黑鱼"));
            list.Add(S(ItemType.Hellforge, "地狱熔炉", 221, StationKind.Hellforge, 0, "用于熔炼狱石"));
            list.Add(S(ItemType.AdamantiteForge, "精金熔炉", 524, StationKind.AdamantiteForge, 3, "用于熔炼精金矿和钛金矿"));
            list.Add(S(ItemType.TitaniumForge, "钛金熔炉", 1221, StationKind.TitaniumForge, 3, "用于熔炼精金矿和钛金矿"));
            list.Add(S(ItemType.CrystalBall, "水晶球", 487, StationKind.CrystalBall, 3, "放置后即可使用\n提高魔能"));
            list.Add(S(ItemType.Bookcase, "书架", 354, StationKind.Bookcase, 0, "用于制作魔法书"));
            list.Add(S(ItemType.ImbuingStation, "灌注站", 1430, StationKind.ImbuingStation, 2, "用于制作武器灌注药剂"));
            list.Add(S(ItemType.Autohammer, "自动锤炼机", 1551, StationKind.Autohammer, 0, "将叶绿锭转化成蘑菇矿锭"));
            list.Add(S(ItemType.LunarCraftingStation, "远古操纵机", 3549, StationKind.AncientManipulator, 0, "用于制作以月亮碎片和夜明矿为原料的物品"));
            list.Add(S(ItemType.Solidifier, "固化机", 998, StationKind.Solidifier, 0, "用于制作物件"));
            list.Add(S(ItemType.Keg, "酒桶", 352, StationKind.Keg, 0, "用于酿造麦芽酒"));
            list.Add(S(ItemType.CookingPot, "烹饪锅", 345, StationKind.CookingPot, 0, "用于烹饪食物"));
            list.Add(S(ItemType.AlchemyTable, "炼药桌", 3000, StationKind.AlchemyTable, 1, "可作为放置的玻璃瓶使用"));
            list.Add(S(ItemType.HeavyWorkBench, "重型组装机", 2172, StationKind.HeavyWorkBench, 0, "用于制作新奇的装饰品"));
            list.Add(S(ItemType.BoneWelder, "骨头焊机", 2192, StationKind.BoneWelder, 0, "用于特殊制作"));
            list.Add(S(ItemType.GlassKiln, "玻璃窑", 2194, StationKind.GlassKiln, 0, "用于特殊制作"));
            list.Add(S(ItemType.LivingLoom, "生命木织机", 2196, StationKind.LivingLoom, 0, "用于特殊制作"));
            list.Add(S(ItemType.SkyMill, "天磨", 2197, StationKind.SkyMill, 0, "用于特殊制作"));
            list.Add(S(ItemType.IceMachine, "冰雪机", 2198, StationKind.IceMachine, 0, "用于特殊制作"));
            list.Add(S(ItemType.LihzahrdFurnace, "丛林蜥蜴熔炉", 2195, StationKind.LihzahrdFurnace, 0, "用于特殊制作"));
            list.Add(S(ItemType.SteampunkBoiler, "蒸汽朋克锅炉", 2203, StationKind.SteampunkBoiler, 0, "用于特殊制作"));
            list.Add(S(ItemType.FleshCloningVaat, "血肉克隆台", 2193, StationKind.FleshCloningVat, 0, "用于特殊制作"));
            list.Add(S(ItemType.HoneyDispenser, "蜂蜜分配机", 2204, StationKind.HoneyDispenser, 0, "用于特殊制作"));
            list.Add(S(ItemType.BlendOMatic, "搅拌机", 995, StationKind.BlendOMatic, 0, "用于制作物件"));
            list.Add(S(ItemType.MeatGrinder, "绞肉机", 996, StationKind.MeatGrinder, 0, "用于制作物件"));
            list.Add(S(ItemType.AmmoBox, "弹药箱", 2177, StationKind.AmmoBox, 6, "放置后即可使用\n20%几率省下弹药"));
            list.Add(S(ItemType.BewitchingTable, "施法桌", 2999, StationKind.BewitchingTable, 1, "放置后即可使用\n仆从数量上限增加1"));
            list.Add(S(ItemType.SharpeningStation, "利器站", 3198, StationKind.SharpeningStation, 1, "放置后即可使用\n近战武器具有盔甲穿透力"));
            list.Add(S(ItemType.WarTable, "战争桌", 3814, StationKind.WarTable, 1, "放置后即可使用\n哨兵数量上限增加1"));
            list.Add(S(ItemType.SliceOfCake, "蛋糕块", 3750, StationKind.SliceOfCake, 3, "放置后即可使用\n“抹你脸上。抹别人脸上。管它呢。”"));
            list.Add(Mat(ItemType.Book, "书", 149, 0, "材料\n“上面有些奇怪的符号”"));
        }

        /// <summary>Their stage, source and value (Item.value).</summary>
        static IEnumerable<(ItemType, int, ItemSource, int)> Station2Sources()
        {
            yield return (ItemType.Campfire, 0, ItemSource.Craft, 0);
            yield return (ItemType.Hellforge, 3, ItemSource.Drop, 3000);
            yield return (ItemType.AdamantiteForge, 4, ItemSource.Craft, 50000);
            yield return (ItemType.TitaniumForge, 4, ItemSource.Craft, 50000);
            yield return (ItemType.CrystalBall, 4, ItemSource.Drop, 100000);
            yield return (ItemType.Bookcase, 2, ItemSource.Craft, 300);
            yield return (ItemType.ImbuingStation, 2, ItemSource.Shop, 70000);
            yield return (ItemType.Autohammer, 5, ItemSource.Drop, 1000000);
            yield return (ItemType.LunarCraftingStation, 5, ItemSource.Drop, 250000);
            yield return (ItemType.Solidifier, 0, ItemSource.Drop, 100000);
            yield return (ItemType.Keg, 0, ItemSource.Craft, 600);
            yield return (ItemType.CookingPot, 0, ItemSource.Craft, 500);
            yield return (ItemType.AlchemyTable, 2, ItemSource.Drop, 100000);
            yield return (ItemType.HeavyWorkBench, 0, ItemSource.Craft, 500);
            yield return (ItemType.BoneWelder, 2, ItemSource.Drop, 0);
            yield return (ItemType.GlassKiln, 0, ItemSource.Craft, 0);
            yield return (ItemType.LivingLoom, 0, ItemSource.Drop, 0);
            yield return (ItemType.SkyMill, 1, ItemSource.Drop, 0);
            yield return (ItemType.IceMachine, 1, ItemSource.Drop, 0);
            yield return (ItemType.LihzahrdFurnace, 5, ItemSource.Drop, 0);
            yield return (ItemType.SteampunkBoiler, 4, ItemSource.Shop, 0);
            yield return (ItemType.FleshCloningVaat, 1, ItemSource.Drop, 0);
            yield return (ItemType.HoneyDispenser, 3, ItemSource.Drop, 0);
            yield return (ItemType.BlendOMatic, 4, ItemSource.Shop, 100000);
            yield return (ItemType.MeatGrinder, 4, ItemSource.Drop, 100000);
            yield return (ItemType.AmmoBox, 4, ItemSource.Shop, 100000);
            yield return (ItemType.BewitchingTable, 2, ItemSource.Shop, 100000);
            yield return (ItemType.SharpeningStation, 2, ItemSource.Shop, 100000);
            yield return (ItemType.WarTable, 4, ItemSource.Drop, 100000);
            yield return (ItemType.SliceOfCake, 4, ItemSource.Shop, 25000);
            yield return (ItemType.Book, 2, ItemSource.Drop, 1500);
        }
    }
}
