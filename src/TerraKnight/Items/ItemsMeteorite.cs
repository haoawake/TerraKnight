using System.Collections.Generic;

namespace TerraKnight.Items
{
    internal static partial class ItemDb
    {
        /// <summary>
        /// The Meteorite (item 116, Item.cs:2773-2784): an ore that places its block (createTile 37, World.Meteorites),
        /// used like any block (useTime 10, useAnimation 15); its tooltip CommonItemTooltip.BurningBlock.
        /// </summary>
        static void MeteoriteBlock(List<ItemDef> list)
        {
            foreach (var d in list)
            {
                if (d.Type != ItemType.Meteorite) continue;
                d.Places = World.CellKind.Meteorite;
                d.UseAnimation = 15 * Tick;
                d.UseTime = 10 * Tick;
                d.UseSound = "Item_1";
                string tip = d.Tooltip ?? "";
                if (!tip.Contains("灼伤")) d.Tooltip = (tip.Length > 0 ? tip + "\n" : "") + "若无特殊防护，触碰会灼伤";
            }
        }
    }
}
