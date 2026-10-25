using System.Collections.Generic;

namespace TerraKnight.Items
{
    internal sealed partial class Inventory
    {
        // Character history, not current ownership. Selling or storing an item never erases it.
        public readonly HashSet<ItemType> CollectedItems = new HashSet<ItemType>();
        internal void RecordCollection(ItemType type)
        {
            if (type != ItemType.None && ItemDb.Get(type)?.MaxStack == 1) CollectedItems.Add(type);
        }
        internal void ObserveCollections()
        {
            void Add(ItemStack[] slots) { foreach (var s in slots) if (!s.IsEmpty) RecordCollection(s.Type); }
            Add(Slots); Add(Accessories); Add(Armor); Add(VanityArmor); Add(VanityAccessories); Add(Misc);
            Add(Bank); Add(Safe); Add(Forge); Add(Void); Add(HkBag);
            foreach (var l in Loadouts) { Add(l.Bag); Add(l.Armor); Add(l.Accessories); Add(l.VanityArmor); Add(l.VanityAccessories); }
            if (!Mouse.IsEmpty) RecordCollection(Mouse.Type);
        }
    }
}
