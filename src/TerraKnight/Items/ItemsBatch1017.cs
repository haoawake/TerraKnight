using System.Collections.Generic;

namespace TerraKnight.Items
{
    internal static partial class ItemDb
    {
        /// <summary>
        /// The 10-17 batch's items: the Bed (Item 224: placed as tile 79, a right click on it sets the spawn point,
        /// World.Beds); the player's storages (World.Chests): the Safe, the Defender's Forge and the Void Vault placed, the
        /// Money Trough and the Void Bag used. Names and tooltips are zh-Hans (Terraria's own names and lines).
        /// </summary>
        static void AddBatch1017(List<ItemDef> list)
        {
            list.Add(new ItemDef
            {
                Type = ItemType.Bed, Name = "床", Texture = "Item_224", PlacesStation = World.StationKind.Bed, Consumable = true,
                MaxStack = 99, UseAnimation = 15 * Tick, UseTime = 10 * Tick, Tooltip = "右键点击设为重生点：死后在床边复活",
            });
            // The player's other storages (CommonItemTooltip.PersonalStorage), placed as the Piggy Bank is; the Void Vault
            // also holds what the Void Bag picks up.
            const string personal = "可用于存放你的物品\n只有你能取用存放的物品\n存放的钱币可在商店中使用";
            ItemDef Storage(ItemType t, string name, int id, World.StationKind kind, int rare, string tip) => new ItemDef
            {
                Type = t, Name = name, Texture = "Item_" + id, PlacesStation = kind, Consumable = true, Rare = rare,
                MaxStack = 99, UseAnimation = 15 * Tick, UseTime = 10 * Tick, Tooltip = tip,
            };
            list.Add(Storage(ItemType.Safe, "保险箱", 346, World.StationKind.Safe, 0, personal));
            list.Add(Storage(ItemType.DefendersForge, "护卫熔炉", 3813, World.StationKind.DefendersForge, 3, personal));
            list.Add(Storage(ItemType.VoidVault, "虚空保险库", 4076, World.StationKind.VoidVault, 3, personal + "\n将包含由虚空袋拾取的物品"));
            // The Money Trough and the Void Bag (Item 3213, 4131: useStyle 1, 20 ticks): their storage opens where the player is.
            list.Add(new ItemDef
            {
                Type = ItemType.MoneyTrough, Name = "钱币槽", Texture = "Item_3213", Rare = 3, UseAnimation = 20 * Tick, UseTime = 20 * Tick,
                AutoReuse = false, NoMelee = true, UseSound = "Item_59", Tooltip = "召唤飞猪存钱罐\n" + personal,
            });
            list.Add(new ItemDef
            {
                Type = ItemType.VoidBag, Name = "虚空袋", Texture = "Item_4131", Rare = 3, UseAnimation = 20 * Tick, UseTime = 20 * Tick,
                AutoReuse = false, NoMelee = true, UseSound = "Item_130",
                Tooltip = "召唤虚空保险库\n" + personal + "\n带在身上时，背包装不下的物品会收进虚空保险库\n“这个口袋的大小太了不得了！”",
            });
            // Item 2997: not drunk, used up by a click on a teammate's head on the full map (World.Wormhole).
            list.Add(new ItemDef
            {
                Type = ItemType.WormholePotion, Name = "虫洞药水", Texture = "Item_2997", Rare = 1, MaxStack = 9999, Consumable = true,
                Tooltip = "将你传送至队友处\n在全屏地图上点击他们的头像",
            });
            // Item 4870: drunk (useStyle 6, 30 ticks, Item6): home, and a gate there back (World.Wormhole).
            list.Add(new ItemDef
            {
                Type = ItemType.PotionOfReturn, Name = "返回药水", Texture = "Item_4870", Rare = 1, MaxStack = 9999, Consumable = true,
                UseAnimation = 30 * Tick, UseTime = 30 * Tick, AutoReuse = false, NoMelee = true, UseSound = "Item_6",
                Tooltip = "将你传送回家并创建一个传送门\n完事后用传送门返回\n“适合往返行程！”",
            });
            // Item 2266: drunk as Ale is, Tipsy (25) for 4 minutes.
            list.Add(new ItemDef
            {
                Type = ItemType.Sake, Name = "清酒", Texture = "Item_2266", Rare = 1, MaxStack = 9999, Consumable = true,
                UseAnimation = 17 * Tick, UseTime = 17 * Tick, UseSound = "Item_3", BuffType = 25, BuffTime = 14400,
                Tooltip = "近战属性小幅提升、防御力降低\n“喝多了你会变成空手道大师。”",
            });
        }

        /// <summary>Their stage, source and value (Item.value, copper).</summary>
        static IEnumerable<(ItemType, int, ItemSource, int)> Batch1017Sources()
        {
            yield return (ItemType.Bed, 0, ItemSource.Craft, 2000);
            // The Merchant's Safe after Skeletron (Chest.SetupShop: downedBoss3), here its stage; the Defender's Forge (the
            // Tavernkeep's, for Defender Medals: no Tavernkeep or Old One's Army here) and the Money Trough (Blood Moon
            // drops: no Blood Moon here) his too, a stage on each.
            yield return (ItemType.Safe, 2, ItemSource.Shop, 200000);
            yield return (ItemType.DefendersForge, 3, ItemSource.Shop, 100000);
            yield return (ItemType.MoneyTrough, 1, ItemSource.Shop, 100000);
            yield return (ItemType.VoidVault, 1, ItemSource.Craft, 100000);
            yield return (ItemType.VoidBag, 1, ItemSource.Craft, 100000);
            yield return (ItemType.WormholePotion, 0, ItemSource.Craft, 1000);
            yield return (ItemType.PotionOfReturn, 0, ItemSource.Craft, 1000);
            // Chest.SetupTravelShop's Sake.
            yield return (ItemType.Sake, 0, ItemSource.Shop, 500);
        }

        /// <summary>Their recipes (Recipe.cs).</summary>
        internal static IEnumerable<Recipe> Batch1017Recipes()
        {
            // Recipe.cs:4763-4769: 15 Wood, 5 Silk at a Sawmill.
            yield return new Recipe(ItemType.Bed, 1, "锯木机", 0, (ItemType.Wood, 15), (ItemType.Silk, 5));
            // Recipe.cs:14922-14951: at a Demon (Crimson) Altar, with Shadow Scales or Tissue Samples.
            yield return new Recipe(ItemType.VoidBag, 1, "恶魔祭坛 / 猩红祭坛", 1, (ItemType.Bone, 30), (ItemType.JungleSpores, 15), (ItemType.ShadowScale, 30));
            yield return new Recipe(ItemType.VoidBag, 1, "恶魔祭坛 / 猩红祭坛", 1, (ItemType.Bone, 30), (ItemType.JungleSpores, 15), (ItemType.TissueSample, 30));
            yield return new Recipe(ItemType.VoidVault, 1, "恶魔祭坛 / 猩红祭坛", 1, (ItemType.Bone, 15), (ItemType.JungleSpores, 8), (ItemType.ShadowScale, 15));
            yield return new Recipe(ItemType.VoidVault, 1, "恶魔祭坛 / 猩红祭坛", 1, (ItemType.Bone, 15), (ItemType.JungleSpores, 8), (ItemType.TissueSample, 15));
            // Recipe.cs:1940-1947, 1996-2000: at a placed bottle (an alchemy table).
            yield return new Recipe(ItemType.WormholePotion, 3, "玻璃瓶", 0, (ItemType.BottledWater, 3), (ItemType.SpecularFish, 1), (ItemType.Blinkroot, 1));
            yield return new Recipe(ItemType.PotionOfReturn, 1, "玻璃瓶", 0, (ItemType.RecallPotion, 1), (ItemType.Obsidifish, 1));
        }
    }
}
