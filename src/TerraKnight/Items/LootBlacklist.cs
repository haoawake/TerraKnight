using System;
using System.Collections.Generic;

namespace TerraKnight.Items
{
    internal sealed partial class Inventory
    {
        // Per save, shared by loadouts. Entries are templates, never stored item stacks.
        public readonly HashSet<ItemType> DropBlacklist = new HashSet<ItemType>();
    }

    internal static class LootBlacklist
    {
        public static bool Add(Inventory inv, ItemStack stack, out string why)
        {
            if (!Containers.Listable(stack, out why, requireValue: false)) return false;
            inv.DropBlacklist.Add(stack.Type);
            return true;
        }

        public static List<string> Save(Inventory inv)
        {
            var names = new List<string>();
            foreach (var type in inv.DropBlacklist) names.Add(type.ToString());
            names.Sort(StringComparer.Ordinal);
            return names;
        }

        public static void Load(Inventory inv, IEnumerable<string> names)
        {
            inv.DropBlacklist.Clear();
            if (names == null) return;
            foreach (var name in names)
                if (Enum.TryParse(name, out ItemType type) && ItemDb.Has(type))
                    Add(inv, ItemStack.Of(type), out _);
        }
    }
}
