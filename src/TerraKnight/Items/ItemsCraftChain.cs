using System.Collections.Generic;

namespace TerraKnight.Items
{
    internal static partial class ItemDb
    {
        /// <summary>
        /// The materials Terraria's recipes go through that the mod lacked, so that what Terraria makes
        /// by crafting is made here too, from what enemies drop (scratchpad/gen2/craftchain.py): crimtane
        /// (its ore with the others in <see cref="Ores"/>) and the Tissue Sample for the crimson weapons,
        /// the Spell Tome for the tomes, the Harp for the Magical Harp, the Djinn Lamp for the Spirit
        /// Flame, the Diving Helmet for the Diving Gear, the Flame Waker Boots for the Hellfire Treads,
        /// luminite ore for its bars (Item.SetDefaults; zh-Hans names and tooltips).
        /// </summary>
        static void AddCraftChain(List<ItemDef> list)
        {
            list.Add(Mat(ItemType.CrimtaneBar, "猩红矿锭", 1257, 1, "材料"));
            list.Add(Mat(ItemType.TissueSample, "组织样本", 1329, 1, "材料"));
            list.Add(Mat(ItemType.SpellTome, "魔法书", 531, 1, "材料\n可附魔"));
            list.Add(Mat(ItemType.Harp, "竖琴", 508, 3, "材料"));
            list.Add(Mat(ItemType.DjinnLamp, "沙漠幽魂灯", 3795, 3, "材料"));
            // I:268: head gear of 2 defense (headSlot 27); its breath has nothing to act on in Hallownest.
            list.Add(Armor(ItemType.DivingHelmet, "潜水头盔", 268, ArmorSlot.Head, 27, 2, ArmorSet.None, price: 0, tier: 0, rare: 2, tip: "大大延长水下呼吸时间", bonus: new Bonuses { DivingHelm = true }));
            // I:4822: the Hellfire Treads' trail of flames on its own (Player.flameWakerBoots, DoBootsEffect_PlaceFlamesOnTile).
            list.Add(Acc2(ItemType.FlameWakerBoots, "烈焰靴", 4822, 3, "在身后留下一道火焰痕迹\n“脚再也不冷了”", bonus: new Bonuses { FlameTrail = true }));
        }

        /// <summary>The craft chain's materials: stage, source and value (Item.value; sellPrice × 5).</summary>
        static IEnumerable<(ItemType, int, ItemSource, int)> ChainSources()
        {
            yield return (ItemType.CrimtaneBar, 1, ItemSource.Craft, 19500);
            yield return (ItemType.TissueSample, 1, ItemSource.Drop, 750);
            yield return (ItemType.SpellTome, 4, ItemSource.Drop, 50000);
            yield return (ItemType.Harp, 4, ItemSource.Drop, 10000);
            yield return (ItemType.DjinnLamp, 4, ItemSource.Drop, 25000);
            yield return (ItemType.DivingHelmet, 1, ItemSource.Drop, 1000);
            yield return (ItemType.FlameWakerBoots, 3, ItemSource.Drop, 100000);
        }
    }
}
