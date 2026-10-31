using System.Collections.Generic;

namespace TerraKnight.Items
{
    internal static partial class ItemDb
    {
        /// <summary>
        /// Terraria's Snow Block (593, tile 147; World.VanillaBlocks places it): fifteen Snowballs by hand (Recipe.cs:2065), the
        /// Snowball Cannon's ammo, from the cold places' enemies (EssentialDrops). The user's 10-31 "给一些敌人加入雪块这种可以
        /// 制作成子弹的材料".
        /// </summary>
        static void AddSnow(List<ItemDef> list) => list.Add(Mat(ItemType.SnowBlock, "雪块", 593, 0, "材料"));

        /// <summary>
        /// Stage, source and value after the generated table's (which these override): the Snow Block's; the Mushroom's stage
        /// down to Terraria's pre-boss one, so the Fungal Wastes' enemies drop it from their first meeting (the Lesser Healing
        /// Potion's, a stage-0 recipe, had it from stage 2 on).
        /// </summary>
        static IEnumerable<(ItemType, int, ItemSource, int)> SnowSources()
        {
            yield return (ItemType.SnowBlock, 0, ItemSource.Drop, 0);
            yield return (ItemType.Mushroom, 0, ItemSource.Drop, 1250);
        }
    }
}
