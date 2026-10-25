using System.Collections.Generic;
using TerraKnight.World;

namespace TerraKnight.Items
{
    internal static partial class ItemDb
    {
        /// <summary>
        /// The Demon Altar and the Crimson Altar as items (Terraria's 5532 and 5533, placing tile 26 styles 0 and 1): what
        /// a hammer of 80 or more knocks off an altar (World.Stations.TrySmashAltar), Dirtmouth's own included, to be set
        /// down anywhere as the station and knocked off again.
        /// </summary>
        static void AddAltarItems(List<ItemDef> list)
        {
            foreach (var (type, name, id, kind) in new[]
            {
                (ItemType.DemonAltar, "恶魔祭坛", 5532, StationKind.DemonAltar),
                (ItemType.CrimsonAltar, "猩红祭坛", 5533, StationKind.CrimsonAltar),
            })
            {
                var d = Mat(type, name, id, 1, "“货真价实”\n可以放置，在旁边制作要用恶魔祭坛或猩红祭坛的物品\n用锤力 80 以上的锤子（比如神锤）敲下来，可以换个地方放");
                d.Material = false;
                d.PlacesStation = kind;
                d.Consumable = true;
                d.UseAnimation = 15 * Tick;
                d.UseTime = 10 * Tick;
                d.MaxStack = 99;
                d.Value = 0;
                list.Add(d);
            }
        }

        static IEnumerable<(ItemType, int, ItemSource, int)> AltarSources()
        {
            yield return (ItemType.DemonAltar, 3, ItemSource.Drop, 0);
            yield return (ItemType.CrimsonAltar, 3, ItemSource.Drop, 0);
        }
    }
}
