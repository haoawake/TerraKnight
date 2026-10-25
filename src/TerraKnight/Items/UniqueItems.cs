namespace TerraKnight.Items
{
    /// <summary>
    /// The items there is only ever one of, the Knight's Nail and the Dream Nail: wherever the player
    /// keeps things (the bag, the cursor, the other loadouts' bags, the Piggy Bank, the trash slot,
    /// every chest, the ground) the first one found stays and any more go, so an older save that ended
    /// up with two keeps one.
    /// </summary>
    internal static class UniqueItems
    {
        /// <summary>Dev: how many extra ones were taken away.</summary>
        public static int DevRemoved;

        /// <summary>Whether the player owns one; optionally exclude ground drops awaiting safe recovery.</summary>
        public static bool Keep(Inventory inv, ItemType type, bool includeGround = true)
        {
            bool found = false;
            int removed = 0;
            void One(ref ItemStack s)
            {
                if (s.IsEmpty || s.Type != type) return;
                if (!found)
                {
                    found = true;
                    if (s.Count > 1)
                    {
                        removed += s.Count - 1;
                        s.Count = 1;
                    }
                    return;
                }
                removed += s.Count;
                s = default;
            }
            for (int i = 0; i < inv.Slots.Length; i++) One(ref inv.Slots[i]);
            One(ref inv.Mouse);
            for (int i = 0; i < inv.HkBag.Length; i++) One(ref inv.HkBag[i]);
            foreach (var l in inv.Loadouts)
                for (int i = 0; i < l.Bag.Length; i++) One(ref l.Bag[i]);
            foreach (var bank in new[] { inv.Bank, inv.Safe, inv.Forge, inv.Void })
                for (int i = 0; i < bank.Length; i++) One(ref bank[i]);
            One(ref inv.Trash);
            One(ref Town.TownNpcs.ReforgeItem);
            if (Net.NetSession.Active) found |= World.Chests.Holds(type);
            else
                foreach (var slots in World.Chests.AllSlots())
                    for (int i = 0; i < slots.Length; i++) One(ref slots[i]);
            // When recovering, leave drops untouched until storage succeeds. In multiplayer a
            // shared drop must neither hide nor discard a character's own stranded stack.
            if ((found || includeGround) && WorldItems.KeepOne(type, found) && includeGround) found = true;
            if (removed > 0)
            {
                DevRemoved += removed;
                Plugin.Log.LogInfo($"[唯一物品] {ItemDb.Get(type)?.Name} 多了 {removed} 个，只留一个");
            }
            return found;
        }
    }
}
