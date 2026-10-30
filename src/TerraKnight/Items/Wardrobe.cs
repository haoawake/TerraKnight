using System;
using System.Collections.Generic;
using System.Linq;
using TerraKnight.World;

namespace TerraKnight.Items
{
    internal sealed partial class Inventory
    {
        // Personal collection shared by every dresser in this save, including unbuilt ones.
        public readonly HashSet<ItemType> WardrobeItems = new HashSet<ItemType>();
    }

    internal static class Wardrobe
    {
        internal static bool Clothing(ItemType type) => ItemDb.Get(type)?.Vanity == true;
        static readonly Dictionary<ItemType, ItemDef> copyDefinitions = new Dictionary<ItemType, ItemDef>();
        internal static ItemDef CopyDefinition(ItemDef original)
        {
            if (copyDefinitions.TryGetValue(original.Type, out var found)) return found;
            var copy = original.Clone();
            copy.Value = 0;
            copy.Tooltip = (copy.Tooltip ?? "") + "\n" + Lang.T("衣柜穿戴副本：无价值，丢弃即消失");
            return copyDefinitions[original.Type] = copy;
        }

        // Called before bag-space checks and before the optional auto-sell bag.
        internal static bool Collect(Inventory inventory, ItemStack stack)
        {
            if (inventory == null || stack.IsEmpty || stack.WardrobeCopy || !Clothing(stack.Type)) return false;
            bool first = inventory.WardrobeItems.Add(stack.Type);
            inventory.RecordCollection(stack.Type);
            int duplicates = stack.Count - (first ? 1 : 0);
            if (duplicates > 0)
            {
                stack.Count = duplicates;
                Shop.Earn(Shop.SellValue(stack));
            }
            if (ReferenceEquals(inventory, TerraMod.Instance?.Inventory))
                UI.CenterNotice.Show(first ? Lang.F("已收纳时装：{0}", stack.Def.Name) : Lang.F("重复时装已出售：{0}", stack.Def.Name));
            return true;
        }

        internal static ItemStack Copy(Inventory inventory, ItemType type)
        {
            if (!Clothing(type) || !inventory.WardrobeItems.Contains(type)) return default;
            var stack = ItemStack.Of(type);
            stack.WardrobeCopy = true;
            return stack;
        }

        // Upgrade old saves once. Existing worn outfits stay worn, becoming free collection copies.
        internal static void Migrate(Inventory inventory)
        {
            void Scan(ItemStack[] slots, bool worn)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    var s = slots[i];
                    if (s.IsEmpty || !Clothing(s.Type) || s.WardrobeCopy) continue;
                    Collect(inventory, s);
                    slots[i] = worn ? Copy(inventory, s.Type) : default;
                    if (worn) slots[i].Favorite = s.Favorite;
                }
            }
            Scan(inventory.Slots, false); Scan(inventory.Bank, false); Scan(inventory.Safe, false);
            Scan(inventory.Forge, false); Scan(inventory.Void, false);
            Scan(inventory.Armor, true); Scan(inventory.Accessories, true);
            Scan(inventory.VanityArmor, true); Scan(inventory.VanityAccessories, true);
            foreach (var loadout in inventory.Loadouts)
            {
                Scan(loadout.Bag, false); Scan(loadout.Armor, true); Scan(loadout.Accessories, true);
                Scan(loadout.VanityArmor, true); Scan(loadout.VanityAccessories, true);
            }
        }

        internal sealed class Outfit
        {
            internal string Name;
            internal readonly ItemType[] Parts = new ItemType[3];
            internal readonly List<ItemType> Extras = new List<ItemType>();
            internal readonly HashSet<int> Regions = new HashSet<int>();
            internal IEnumerable<ItemType> Items => Parts.Where(t => t != ItemType.None).Concat(Extras);
        }
        static List<Outfit> outfits;
        internal static IReadOnlyList<Outfit> Outfits
        {
            get
            {
                if (outfits != null) return outfits;
                outfits = new List<Outfit>();
                var grouped = new HashSet<ItemType>();
                void Add(IEnumerable<ItemType> types, string name = null, int region = -1)
                {
                    var pieces = types.Distinct().Where(Clothing).ToArray();
                    if (pieces.Length == 0) return;
                    var outfit = new Outfit { Name = name ?? ItemDb.Get(pieces[0]).Name };
                    foreach (var type in pieces)
                    {
                        var d = ItemDb.Get(type);
                        int slot = d.Armor == ArmorSlot.Head ? 0 : d.Armor == ArmorSlot.Body ? 1 : d.Armor == ArmorSlot.Legs ? 2 : -1;
                        if (slot >= 0 && outfit.Parts[slot] == ItemType.None) outfit.Parts[slot] = type;
                        else outfit.Extras.Add(type);
                        grouped.Add(type);
                    }
                    if (region >= 0) outfit.Regions.Add(region);
                    foreach (var row in VanityDrops.All)
                        if (pieces.Contains(row.Lead) || VanityDrops.Mates(row.Lead).Any(pieces.Contains))
                            foreach (var habitat in BestiaryHabitats.Of(row.Record))
                            {
                                string area = Areas.AreaName(habitat.Scene);
                                for (int a = 0; a < AreaBlocks.All.Length; a++)
                                    if (AreaBlocks.All[a].Name == area) outfit.Regions.Add(a);
                            }
                    // Developer clothing belongs to every boss habitat that can award that developer set.
                    if (VanityDrops.DeveloperReps.Any(rep => pieces.Contains(rep) || VanityDrops.Mates(rep).Any(pieces.Contains)))
                        foreach (var record in EnemyDrops.DevRecords.Where(r => VanillaBossProgression.All.TryGetValue(r, out var boss) && boss.Band >= 3))
                            foreach (var habitat in BestiaryHabitats.Of(record))
                            {
                                string area = Areas.AreaName(habitat.Scene);
                                for (int a = 0; a < AreaBlocks.All.Length; a++)
                                    if (AreaBlocks.All[a].Name == area) outfit.Regions.Add(a);
                            }
                    outfits.Add(outfit);
                }
                for (int a = 0; a < AreaBlocks.All.Length; a++)
                    Add(ItemDb.All.Where(d => d.Vanity && d.Region == a + 1).Select(d => d.Type), Lang.F("{0}时装", Areas.Display(AreaBlocks.All[a].Name)), a);
                foreach (var lead in VanityDrops.All.Select(r => r.Lead).Concat(VanityDrops.DeveloperReps).Distinct())
                    Add(new[] { lead }.Concat(VanityDrops.Mates(lead)));
                foreach (var d in ItemDb.All.Where(d => d.Vanity && !grouped.Contains(d.Type))) Add(new[] { d.Type });
                return outfits;
            }
        }
    }
}
