using System.Collections.Generic;
namespace TerraKnight.Items
{
    internal sealed partial class Inventory
    {
        public readonly HashSet<ItemType> HallownestObtained = new HashSet<ItemType>();
        // Difference from the native inventory for uncounted story items after explicit shop trades or storage moves.
        public readonly Dictionary<ItemType, int> HallownestTradeOffsets = new Dictionary<ItemType, int>();
        public readonly Dictionary<ItemType, int> HallownestNativeCounts = new Dictionary<ItemType, int>();
    }
    internal static class HallownestRecovery
    {
        public static bool IsUnique(ItemType type) => HkItems.CanStoreInBag(ItemStack.Of(type))
            || type == ItemType.DescendingDarkRune || AbilityShields.Is(type);
        public static void Record(Inventory inv, ItemType type)
        { if (inv != null && IsUnique(type)) inv.HallownestObtained.Add(type); }
        public static void Observe(Inventory inv)
        {
            if (inv == null) return;
            void Scan(ItemStack[] xs) { foreach (var s in xs) if (!s.IsEmpty) Record(inv, s.Type); }
            Scan(inv.Slots); Scan(inv.HkBag); Scan(inv.Accessories); Scan(inv.VanityAccessories);
            Scan(inv.Armor); Scan(inv.VanityArmor); Scan(inv.Misc);
            Scan(inv.Bank); Scan(inv.Safe); Scan(inv.Forge); Scan(inv.Void);
            foreach (var l in inv.Loadouts)
            { Scan(l.Bag); Scan(l.Accessories); Scan(l.VanityAccessories); Scan(l.Armor); Scan(l.VanityArmor); }
            if (!inv.Mouse.IsEmpty) Record(inv, inv.Mouse.Type);
            if (!Town.TownNpcs.ReforgeItem.IsEmpty) Record(inv, Town.TownNpcs.ReforgeItem.Type);
            if (!Town.TownNpcs.GuideItem.IsEmpty) Record(inv, Town.TownNpcs.GuideItem.Type);
        }
        // History is also used by save synchronization; recovery must not mint consumed resources.
        internal static bool Recoverable(ItemType t) => IsUnique(t) && t != ItemType.HkSimpleKey
            && t != ItemType.HkCityCrest && t != ItemType.HkElegantKey && t != ItemType.HkLoveKey
            && t != ItemType.HkShopkeepersKey && t != ItemType.HkDelicateFlower
            && t != ItemType.HkRancidEgg && t != ItemType.HkPaleOre && RelicValue(t) == 0;
        internal static long RelicValue(ItemType t) => t == ItemType.HkWanderersJournal ? 20000
            : t == ItemType.HkHallownestSeal ? 45000 : t == ItemType.HkKingsIdol ? 80000
            : t == ItemType.HkArcaneEgg ? 120000 : 0;
        public static bool CanBuy(Inventory inv, ItemType t) => inv != null && Recoverable(t) && inv.HallownestObtained.Contains(t);
        public static bool SoldTool(Inventory inv, ItemType t) => inv != null && inv.HallownestTradeOffsets.TryGetValue(t, out int offset) && offset < 0;
        public static IEnumerable<ItemType> Stock(Inventory inv)
        {
            Observe(inv);
            if (inv == null) yield break;
            // Definition order is stable; only this character's actual acquisitions unlock stock.
            foreach (var d in ItemDb.All) if (CanBuy(inv, d.Type)) yield return d.Type;
        }
        public static long Price(ItemDef d) => System.Math.Max(10000L, Shop.PriceOf(d));
    }
}

