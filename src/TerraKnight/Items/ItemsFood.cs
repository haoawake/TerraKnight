using System.Collections.Generic;

namespace TerraKnight.Items
{
    internal static partial class ItemDb
    {
        /// <summary>
        /// Terraria's food (Item.SetFoodDefaults; gen_food.py): eaten or drunk, each gives its fed state for its time,
        /// Well Fed, Plenty Satisfied or Exquisitely Stuffed (Player.cs:12018-12065), in place of any other (5269-5278).
        /// The ones made of what Hallownest has: fish, shrimp and lobster, the bait critters, the gold critters with the Golden
        /// Carp, rotten chunks, pumpkins. The Shucked Oyster and the Joja Cola fished up are food too.
        /// </summary>
        static void AddFood(List<ItemDef> list)
        {
            ItemDef F(ItemType t, string name, int id, int buff, int ticks, int rare, bool drink, string tip)
            {
                var lines = new List<string>();
                if (!string.IsNullOrEmpty(tip)) lines.Add(tip);
                lines.Add(ticks >= 3600 ? $"持续 {ticks / 3600} 分钟" : $"持续 {ticks / 60} 秒");
                return new ItemDef
                {
                    Type = t, Name = name, Texture = "Item_" + id, Consumable = true, MaxStack = 9999, UseAnimation = 17 * Tick, UseTime = 17 * Tick,
                    UseSound = drink ? "Item_3" : "Item_2", Rare = rare, BuffType = buff, BuffTime = ticks, Tooltip = string.Join("\n", lines),
                };
            }
            list.Add(F(ItemType.PumpkinPie, "南瓜派", 1787, 26, 28800, 1, false, "所有属性小幅提升\n“一口就吃完了！”"));
            list.Add(F(ItemType.CookedFish, "熟鱼", 2425, 26, 28800, 1, false, "所有属性小幅提升\n“薯条在哪里！？”"));
            list.Add(F(ItemType.CookedShrimp, "熟虾", 2426, 206, 36000, 2, false, "所有属性中幅提升\n“烧、煮、烤、烘……”"));
            list.Add(F(ItemType.Sashimi, "生鱼片", 2427, 206, 28800, 2, false, "所有属性中幅提升\n“是生的！异国风味！”"));
            list.Add(F(ItemType.GrubSoup, "蛆虫汤", 3195, 206, 50400, 2, true, "所有属性中幅提升\n“开饭啦！”"));
            list.Add(F(ItemType.Escargot, "食用蜗牛", 4019, 206, 36000, 2, false, "所有属性中幅提升\n“看，蜗轮增压！”"));
            list.Add(F(ItemType.GoldenDelight, "金美味", 4022, 207, 172800, 6, false, "所有属性大幅提升\n“（金）好吃！”"));
            list.Add(F(ItemType.SeafoodDinner, "海鲜大餐", 4034, 206, 50400, 2, false, "所有属性中幅提升\n“我，海鲜，吃掉。”"));
            list.Add(F(ItemType.LobsterTail, "龙虾尾", 4403, 206, 36000, 2, false, "所有属性中幅提升\n“加一点黄油就很美味了”"));
            list.Add(F(ItemType.MonsterLasagna, "怪物千层面", 5092, 206, 28800, 2, false, "所有属性中幅提升\n“味道像是面条配上毛和肉。”"));
            list.Add(F(ItemType.BlackenedFish, "焦黑鱼", 5537, 26, 18000, 1, false, "所有属性小幅提升"));
            // Fished up (FishingItems.cs) as food: the Shucked Oyster out of an Oyster, the Joja Cola among the junk.
            foreach (var d in list)
            {
                if (d.Type == ItemType.ShuckedOyster) Feed(d, 26, 36000, false);
                else if (d.Type == ItemType.JojaCola) Feed(d, 26, 7200, true);
                // The Ale (Ale Tosser ammo too): Tipsy for 2 minutes.
                else if (d.Type == ItemType.Ale) Feed(d, 25, 7200, true);
            }
        }

        static void Feed(ItemDef d, int buff, int ticks, bool drink)
        {
            d.Consumable = true;
            d.UseAnimation = d.UseTime = 17 * Tick;
            d.UseSound = drink ? "Item_3" : "Item_2";
            d.BuffType = buff;
            d.BuffTime = ticks;
            string time = ticks >= 3600 ? $"持续 {ticks / 3600} 分钟" : $"持续 {ticks / 60} 秒";
            string tip = (d.Tooltip ?? "").Replace("材料\n", "");
            d.Tooltip = string.IsNullOrEmpty(tip) ? time : tip + "\n" + time;
        }

        static IEnumerable<(ItemType, int, ItemSource, int)> FoodSources()
        {
            yield return (ItemType.PumpkinPie, 0, ItemSource.Craft, 1000);
            yield return (ItemType.CookedFish, 0, ItemSource.Craft, 2500);
            yield return (ItemType.CookedShrimp, 2, ItemSource.Craft, 7500);
            yield return (ItemType.Sashimi, 0, ItemSource.Craft, 2500);
            yield return (ItemType.GrubSoup, 3, ItemSource.Craft, 10000);
            yield return (ItemType.Escargot, 0, ItemSource.Craft, 10000);
            yield return (ItemType.GoldenDelight, 1, ItemSource.Craft, 500000);
            yield return (ItemType.SeafoodDinner, 0, ItemSource.Craft, 5000);
            yield return (ItemType.LobsterTail, 4, ItemSource.Craft, 10000);
            yield return (ItemType.MonsterLasagna, 0, ItemSource.Craft, 5000);
            yield return (ItemType.BlackenedFish, 0, ItemSource.Craft, 1500);
        }
    }

    internal static partial class Recipes
    {
        /// <summary>The food's recipes (Recipe.cs), at the Cooking Pot, the Work Bench or the Furnace.</summary>
        static IEnumerable<Recipe> FoodRecipes()
        {
            yield return new Recipe(ItemType.PumpkinPie, 1, "熔炉", 0, (ItemType.Pumpkin, 15)); // Recipe.cs:1136-1140
            yield return new Recipe(ItemType.CookedFish, 1, "烹饪锅", 0, (ItemType.Bass, 1)); // Recipe.cs:1303-1307
            yield return new Recipe(ItemType.CookedFish, 1, "烹饪锅", 0, (ItemType.Trout, 1)); // Recipe.cs:1308-1312
            yield return new Recipe(ItemType.CookedFish, 1, "烹饪锅", 0, (ItemType.AtlanticCod, 1)); // Recipe.cs:1313-1317
            yield return new Recipe(ItemType.CookedShrimp, 1, "烹饪锅", 0, (ItemType.Shrimp, 1)); // Recipe.cs:1338-1342
            yield return new Recipe(ItemType.Sashimi, 1, "工作台", 0, (ItemType.Tuna, 1)); // Recipe.cs:1318-1322
            yield return new Recipe(ItemType.Sashimi, 1, "工作台", 0, (ItemType.Salmon, 1)); // Recipe.cs:1323-1327
            yield return new Recipe(ItemType.Sashimi, 1, "工作台", 0, (ItemType.RedSnapper, 1)); // Recipe.cs:1328-1332
            yield return new Recipe(ItemType.Sashimi, 1, "工作台", 0, (ItemType.Flounder, 1)); // Recipe.cs:1333-1337
            yield return new Recipe(ItemType.GrubSoup, 1, "烹饪锅", 0, (ItemType.Grubby, 1), (ItemType.Sluggy, 1), (ItemType.Buggy, 1)); // Recipe.cs:1129-1135
            yield return new Recipe(ItemType.Escargot, 1, "烹饪锅", 0, (ItemType.Snail, 1)); // Recipe.cs:1228-1232
            yield return new Recipe(ItemType.GoldenDelight, 1, "烹饪锅", 0, (ItemType.GoldenCarp, 1)); // Recipe.cs:1233-1237
            yield return new Recipe(ItemType.GoldenDelight, 1, "烹饪锅", 0, (ItemType.GoldGrasshopper, 1)); // Recipe.cs:1263-1267
            yield return new Recipe(ItemType.GoldenDelight, 1, "烹饪锅", 0, (ItemType.GoldWorm, 1)); // Recipe.cs:1268-1272
            yield return new Recipe(ItemType.GoldenDelight, 1, "烹饪锅", 0, (ItemType.GoldButterfly, 1)); // Recipe.cs:1273-1277
            yield return new Recipe(ItemType.GoldenDelight, 1, "烹饪锅", 0, (ItemType.GoldDragonfly, 1)); // Recipe.cs:1283-1287
            yield return new Recipe(ItemType.GoldenDelight, 1, "烹饪锅", 0, (ItemType.GoldLadyBug, 1)); // Recipe.cs:1288-1292
            yield return new Recipe(ItemType.GoldenDelight, 1, "烹饪锅", 0, (ItemType.GoldWaterStrider, 1)); // Recipe.cs:1298-1302
            yield return new Recipe(ItemType.SeafoodDinner, 1, "烹饪锅", 0, (ItemType.ArmoredCavefish, 2)); // Recipe.cs:1424-1428
            yield return new Recipe(ItemType.SeafoodDinner, 1, "烹饪锅", 0, (ItemType.ChaosFish, 2)); // Recipe.cs:1429-1433
            yield return new Recipe(ItemType.SeafoodDinner, 1, "烹饪锅", 0, (ItemType.CrimsonTigerfish, 2)); // Recipe.cs:1434-1438
            yield return new Recipe(ItemType.SeafoodDinner, 1, "烹饪锅", 0, (ItemType.Damselfish, 2)); // Recipe.cs:1439-1443
            yield return new Recipe(ItemType.SeafoodDinner, 1, "烹饪锅", 0, (ItemType.DoubleCod, 2)); // Recipe.cs:1444-1448
            yield return new Recipe(ItemType.SeafoodDinner, 1, "烹饪锅", 0, (ItemType.Ebonkoi, 2)); // Recipe.cs:1449-1453
            yield return new Recipe(ItemType.SeafoodDinner, 1, "烹饪锅", 0, (ItemType.FlarefinKoi, 2)); // Recipe.cs:1454-1458
            yield return new Recipe(ItemType.SeafoodDinner, 1, "烹饪锅", 0, (ItemType.FrostMinnow, 2)); // Recipe.cs:1459-1463
            yield return new Recipe(ItemType.SeafoodDinner, 1, "烹饪锅", 0, (ItemType.Hemopiranha, 2)); // Recipe.cs:1464-1468
            yield return new Recipe(ItemType.SeafoodDinner, 1, "烹饪锅", 0, (ItemType.Honeyfin, 2)); // Recipe.cs:1469-1473
            yield return new Recipe(ItemType.SeafoodDinner, 1, "烹饪锅", 0, (ItemType.NeonTetra, 2)); // Recipe.cs:1474-1478
            yield return new Recipe(ItemType.SeafoodDinner, 1, "烹饪锅", 0, (ItemType.Obsidifish, 2)); // Recipe.cs:1479-1483
            yield return new Recipe(ItemType.SeafoodDinner, 1, "烹饪锅", 0, (ItemType.PrincessFish, 2)); // Recipe.cs:1484-1488
            yield return new Recipe(ItemType.SeafoodDinner, 1, "烹饪锅", 0, (ItemType.Prismite, 2)); // Recipe.cs:1489-1493
            yield return new Recipe(ItemType.SeafoodDinner, 1, "烹饪锅", 0, (ItemType.SpecularFish, 2)); // Recipe.cs:1494-1498
            yield return new Recipe(ItemType.SeafoodDinner, 1, "烹饪锅", 0, (ItemType.Stinkfish, 2)); // Recipe.cs:1499-1503
            yield return new Recipe(ItemType.SeafoodDinner, 1, "烹饪锅", 0, (ItemType.VariegatedLardfish, 2)); // Recipe.cs:1504-1508
            yield return new Recipe(ItemType.LobsterTail, 1, "烹饪锅", 0, (ItemType.RockLobster, 1)); // Recipe.cs:1343-1347
            yield return new Recipe(ItemType.MonsterLasagna, 1, "烹饪锅", 0, (ItemType.RottenChunk, 8)); // Recipe.cs:1141-1145
            yield return new Recipe(ItemType.BlackenedFish, 1, "篝火", 0, (ItemType.Bass, 1)); // Recipe.cs:1509-1513
            yield return new Recipe(ItemType.BlackenedFish, 1, "篝火", 0, (ItemType.Trout, 1)); // Recipe.cs:1514-1518
            yield return new Recipe(ItemType.BlackenedFish, 1, "篝火", 0, (ItemType.AtlanticCod, 1)); // Recipe.cs:1519-1523
            yield return new Recipe(ItemType.BlackenedFish, 1, "篝火", 0, (ItemType.Flounder, 1)); // Recipe.cs:1524-1528
            yield return new Recipe(ItemType.BlackenedFish, 1, "篝火", 0, (ItemType.NeonTetra, 1)); // Recipe.cs:1529-1533
            yield return new Recipe(ItemType.BlackenedFish, 1, "篝火", 0, (ItemType.Salmon, 1)); // Recipe.cs:1534-1538
            yield return new Recipe(ItemType.BlackenedFish, 1, "篝火", 0, (ItemType.Tuna, 1)); // Recipe.cs:1539-1543
            yield return new Recipe(ItemType.BlackenedFish, 1, "篝火", 0, (ItemType.RedSnapper, 1)); // Recipe.cs:1544-1548
        }
    }
}
