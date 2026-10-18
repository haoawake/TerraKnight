using System.Collections.Generic;
using TerraKnight.World;

namespace TerraKnight.Items
{
    internal static partial class ItemDb
    {
        /// <summary>
        /// Hallownest's 48 regional torches (World.RegionalTorches), Terraria's biome torches' way: Item_8's use and hold
        /// (15 / 10 ticks, held low in front, lighting round the hand), a torch placed (CellKind.Torch, the cell keeping
        /// the torch's style), Item.value 50 (a geo to buy, ten copper to sell), named as the pack names them. Each comes
        /// from breaking its area's breakables (Items.BreakableDrops): its stage is that area's.
        /// </summary>
        static void AddRegionalTorches(List<ItemDef> list)
        {
            for (int style = 1; style <= RegionalTorches.Count; style++)
            {
                var t = RegionalTorches.All[style - 1];
                list.Add(new ItemDef
                {
                    Type = RegionalTorches.ItemOf(style), Name = t.Name, Texture = RegionalTorches.ItemTexture(style),
                    UseAnimation = 15 * Tick, UseTime = 10 * Tick, Places = CellKind.Torch, MaxStack = 999, HoldStyle = 1, Tier = 0,
                    Stage = Areas.LevelOf(t.Scene), Source = ItemSource.Drop, Value = 50, Price = 1, Tooltip = "提供照明",
                });
            }
        }
    }
}
