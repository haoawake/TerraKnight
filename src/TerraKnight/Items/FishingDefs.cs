using System.Collections.Generic;

namespace TerraKnight.Items
{
    /// <summary>What an area's water stands for in Terraria (Player zone flags, its sea, its rain).</summary>
    [System.Flags]
    internal enum Zone : ushort
    {
        None = 0, Corrupt = 1, Crimson = 2, Hallow = 4, HalfHallow = 8, Jungle = 16, Snow = 32, Desert = 64, Glowshroom = 128, Dungeon = 256, Beach = 512, Ocean = 1024, Rain = 2048,
    }

    /// <summary>A crate's kind: by quality (Wooden, Iron, Golden and their hardmode ones), an area's, the acid one; the Oyster.</summary>
    internal enum CrateKind : byte { Tier, Biome, Acid, Oyster }

    /// <summary>A crate's contents: its piece of equipment (its pool, the special one at 1/5, the stages it may be of), the rest by kind.</summary>
    internal sealed class CrateDef
    {
        public readonly CrateKind Kind;
        /// <summary>Wooden 0, Iron 1, Golden 2; an area's 3.</summary>
        public readonly int Level;
        public readonly bool Hard;
        /// <summary>The chance of the piece of equipment: one in this.</summary>
        public readonly int Den;
        public readonly int Lo, Hi;
        public readonly ItemType Special;
        public readonly ItemType[] Pool;

        public CrateDef(CrateKind kind, int level, bool hard, int den, int lo, int hi, ItemType special, ItemType[] pool)
        {
            Kind = kind; Level = level; Hard = hard; Den = den; Lo = lo; Hi = hi; Special = special; Pool = pool;
        }
    }

    /// <summary>
    /// One of Terraria's catch rules (FishDropRule): its conditions (a mask of <see cref="Fishing"/>'s bits, a quest fish),
    /// its own chance, the rarity roll it needs (0 any, 1 common .. 5 legendary, 6 the Bomb Fish's, 7 uncommon or common)
    /// and what it gives (none: a stopper, nothing caught).
    /// </summary>
    internal readonly struct Rule
    {
        public readonly int Index;
        public readonly bool Lava, Stopper;
        public readonly int Rarity, Num, Den;
        public readonly ulong Mask;
        public readonly ItemType Quest;
        public readonly ItemType[] Items;

        public Rule(int index, bool lava, bool stopper, int rarity, int num, int den, ulong mask, ItemType quest, ItemType[] items)
        {
            Index = index; Lava = lava; Stopper = stopper; Rarity = rarity; Num = num; Den = den; Mask = mask; Quest = quest; Items = items;
        }
    }

    internal static partial class ItemDb
    {
        /// <summary>A fishing pole (useStyle 1, useTime 8): its power, the bobber it casts and how fast.</summary>
        static ItemDef Pole(ItemType t, string name, int id, int power, float speed, int bobber, int rare, string tip, bool acid) => new ItemDef
        {
            Type = t, Name = name, Texture = "Item_" + id, FishingPower = power, ShootSpeed = speed, Bobber = bobber, AcidPole = acid, Rare = rare, Tooltip = tip,
            UseAnimation = 8 * Tick, UseTime = 8 * Tick, AutoReuse = false, NoMelee = true, UseSound = "Item_1",
        };

        /// <summary>Bait (Item.bait), fit for the ammo slots; the acid bait fishes in acid.</summary>
        static ItemDef Bait(ItemType t, string name, int id, int power, int rare, string tip, bool acid) => new ItemDef
        {
            Type = t, Name = name, Texture = "Item_" + id, BaitPower = power, AcidBait = acid, MaxStack = 9999, Rare = rare, Tooltip = tip, UseSound = null,
        };

        /// <summary>One of the Angler's quest fish (questItem, rare -11): kept one at a time, worth nothing.</summary>
        static ItemDef Quest(ItemType t, string name, int id, string tip) => new ItemDef
        {
            Type = t, Name = name, Texture = "Item_" + id, QuestFish = true, MaxStack = 1, Rare = -11, Tooltip = tip, UseSound = null,
        };

        /// <summary>A crate, opened by a right click.</summary>
        static ItemDef Crate(ItemType t, string name, int id, int rare, string tip) => new ItemDef
        {
            Type = t, Name = name, Texture = "Item_" + id, Opens = true, MaxStack = 9999, Rare = rare, Tooltip = tip, UseSound = null,
        };

        /// <summary>A fish or a catch with no use of its own (sold; some go into recipes).</summary>
        static ItemDef Fish(ItemType t, string name, int id, int rare, string tip) => new ItemDef
        {
            Type = t, Name = name, Texture = "Item_" + id, MaxStack = 9999, Rare = rare, Tooltip = tip, UseSound = null,
        };
    }
}
