using System.Collections.Generic;

namespace TerraKnight.Items
{
    internal static partial class ItemDb
    {
        /// <summary>
        /// The Ultimate Healing Potion (Resources/item_ultimate_healing_potion.png, from 贴图改动资料/HollowHeal.png): 600 life, past the
        /// Super Healing Potion's 200, with the potion sickness of any healing potion; a stage 5 item, the Merchant's then
        /// and the stage's bosses' (BossPotions).
        /// </summary>
        static void AddUltimatePotion(List<ItemDef> list) => list.Add(new ItemDef
        {
            Type = ItemType.UltimateHealingPotion, Name = "究极治疗药水", Texture = Relics.TexturePrefix + "item_ultimate_healing_potion",
            Consumable = true, MaxStack = 9999, UseAnimation = 17 * Tick, UseTime = 17 * Tick, UseSound = "Item_3", Rare = 10,
            HealLife = 600, PotionSick = true, Tooltip = "恢复 600 生命值",
        });

        /// <summary>Its stage, source and value: stage 5, bought (three Super Healing Potions' worth) and dropped.</summary>
        static IEnumerable<(ItemType, int, ItemSource, int)> UltimatePotionSources()
        {
            yield return (ItemType.UltimateHealingPotion, 5, ItemSource.Shop, 45000);
        }
    }
}
