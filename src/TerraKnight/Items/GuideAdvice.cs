using System.Collections.Generic;

namespace TerraKnight.Items
{
    internal static partial class ItemDb
    {
        /// <summary>The Guide's advice (the user's 10-29): sold by him, its picture Resources/hk_GuideAdvice_Item.png.</summary>
        static void AddGuideAdvice(List<ItemDef> list)
        {
            list.Add(new ItemDef { Type = ItemType.GuideAdvice, Name = "向导的叮嘱", Texture = "GuideAdvice_Item", MaxStack = 1, Rare = 3,
                Tooltip = "拿着我的合成表滚吧，把我丢弃在圣巢的烂人" });
        }
    }

    /// <summary>
    /// The Guide's advice carried (in the bag or the Hallownest bag): the inventory's crafting column switches to his recipe
    /// list (Town.TownNpcs.GuideCraftOpen: his slot over the column, every recipe the item put in it goes into) without
    /// talking to him. Switched on the scroll by the column; the item gone, the column is the crafting one again.
    /// </summary>
    internal static class GuideAdvice
    {
        /// <summary>The column switched to the recipe list (this session).</summary>
        public static bool Chosen { get; private set; }

        static int seenFrame = -1;
        static bool carried;

        /// <summary>The item in the bag or the Hallownest bag (looked for once a frame).</summary>
        public static bool Carried
        {
            get
            {
                int frame = UnityEngine.Time.frameCount;
                if (frame != seenFrame)
                {
                    seenFrame = frame;
                    var inv = TerraMod.Instance?.Inventory;
                    carried = inv != null && Recipes.Count(inv, ItemType.GuideAdvice) > 0;
                }
                return carried;
            }
        }

        /// <summary>The recipe list is up in the column.</summary>
        public static bool On => Chosen && Carried;

        /// <summary>The scroll by the column clicked: the recipe list or the crafting column again (the item shown goes back to the bag).</summary>
        public static void Toggle()
        {
            Chosen = !Chosen;
            if (!Chosen) Town.TownNpcs.ReturnGuideItem();
        }
    }
}
