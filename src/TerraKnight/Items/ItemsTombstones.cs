using System.Collections.Generic;
using TerraKnight.World;

namespace TerraKnight.Items
{
    internal static partial class ItemDb
    {
        /// <summary>
        /// The tombstones a death leaves (Items.Tombstones): set down like any furniture once picked up (World.Stations,
        /// tile 85's styles), the gold ones those of a rich death. Names are zh-Hans (Terraria's own).
        /// </summary>
        static void AddTombstones(List<ItemDef> list)
        {
            ItemDef G(ItemType t, string name, int id, StationKind kind, int rare) => new ItemDef
            {
                Type = t, Name = name, Texture = "Item_" + id, PlacesStation = kind, Consumable = true, MaxStack = 9999, UseAnimation = 15 * Tick, UseTime = 10 * Tick,
                Rare = rare, Tooltip = "",
            };
            list.Add(G(ItemType.Tombstone, "墓石", 321, StationKind.Tombstone, 0));
            list.Add(G(ItemType.GraveMarker, "墓石碑", 1173, StationKind.GraveMarker, 0));
            list.Add(G(ItemType.CrossGraveMarker, "十字墓石碑", 1174, StationKind.CrossGraveMarker, 0));
            list.Add(G(ItemType.Headstone, "碑石", 1175, StationKind.Headstone, 0));
            list.Add(G(ItemType.Gravestone, "墓碑", 1176, StationKind.Gravestone, 0));
            list.Add(G(ItemType.Obelisk, "方尖碑", 1177, StationKind.Obelisk, 0));
            list.Add(G(ItemType.RichGravestone1, "金十字墓石碑", 3229, StationKind.RichGravestone1, 2));
            list.Add(G(ItemType.RichGravestone2, "金墓石", 3230, StationKind.RichGravestone2, 2));
            list.Add(G(ItemType.RichGravestone3, "金墓石碑", 3231, StationKind.RichGravestone3, 2));
            list.Add(G(ItemType.RichGravestone4, "金墓碑", 3232, StationKind.RichGravestone4, 2));
            list.Add(G(ItemType.RichGravestone5, "金碑石", 3233, StationKind.RichGravestone5, 2));
        }

        /// <summary>Their stage, source and value (Item.value: the gold ones 5 gold).</summary>
        static IEnumerable<(ItemType, int, ItemSource, int)> TombstoneSources()
        {
            foreach (var t in new[] { ItemType.Tombstone, ItemType.GraveMarker, ItemType.CrossGraveMarker, ItemType.Headstone, ItemType.Gravestone, ItemType.Obelisk })
                yield return (t, 0, ItemSource.Drop, 0);
            foreach (var t in new[] { ItemType.RichGravestone1, ItemType.RichGravestone2, ItemType.RichGravestone3, ItemType.RichGravestone4, ItemType.RichGravestone5 })
                yield return (t, 0, ItemSource.Drop, 50000);
        }
    }
}
