using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Terraria's Sort buttons (ItemSorting.SortInventory, SortChest), by its layers in their
    /// order: weapons by class then damage, ammo, tools by power, armor by defense, accessories,
    /// equipment of the game's own (the Dream Nail), healing then mana potions, buff potions and
    /// boosters, what is placed, materials, the rest; within a layer the rarer and dearer first.
    /// Stacks of one item are merged as they are sorted; favourites keep their slots.
    /// </summary>
    internal static class ItemSorting
    {
        public static void Sort(ItemStack[] items, int from, int to, bool keepFavorites)
        {
            var slots = new List<int>();
            for (int i = from; i < to && i < items.Length; i++)
                if (!(keepFavorites && !items[i].IsEmpty && items[i].Favorite)) slots.Add(i);
            // Merged: each kind of item filled up to its stack size, the rest a stack of its own.
            var stacks = new List<ItemStack>();
            foreach (int i in slots)
            {
                var s = items[i];
                if (s.IsEmpty) continue;
                s.Favorite = false;
                int max = Mathf.Max(1, s.Def.MaxStack);
                for (int k = 0; k < stacks.Count && s.Count > 0; k++)
                {
                    var t = stacks[k];
                    if (t.Type != s.Type || t.Count >= max) continue;
                    int moved = Mathf.Min(s.Count, max - t.Count);
                    t.Count += moved;
                    s.Count -= moved;
                    stacks[k] = t;
                }
                if (s.Count > 0) stacks.Add(s);
            }
            stacks.Sort(Compare);
            for (int k = 0; k < slots.Count; k++) items[slots[k]] = k < stacks.Count ? stacks[k] : default;
        }

        static int Compare(ItemStack a, ItemStack b)
        {
            var da = a.Def;
            var db = b.Def;
            int c = Layer(da).CompareTo(Layer(db));
            if (c != 0) return c;
            c = Strength(db).CompareTo(Strength(da));
            if (c != 0) return c;
            c = db.Rare.CompareTo(da.Rare);
            if (c != 0) return c;
            c = db.Value.CompareTo(da.Value);
            if (c != 0) return c;
            c = ((int)a.Type).CompareTo((int)b.Type);
            if (c != 0) return c;
            return b.Count.CompareTo(a.Count);
        }

        static int Layer(ItemDef d)
        {
            if (d.IsWeapon && !d.GameTool && d.PickPower <= 0 && d.AxePower <= 0 && d.HammerPower <= 0)
                return d.Class == DamageClass.Melee ? 0 : d.Class == DamageClass.Ranged ? 1 : d.Class == DamageClass.Magic ? 2 : d.Class == DamageClass.Summon ? 3 : 4;
            if (d.IsAmmo) return 5;
            if (d.PickPower > 0) return 6;
            if (d.AxePower > 0 || d.HammerPower > 0) return 7;
            if (d.IsArmor) return 8 + (int)d.Armor;
            if (d.IsAccessory) return 12;
            if (d.GameTool) return 13;
            if (d.HealLife > 0) return 14;
            if (d.HealMana > 0) return 15;
            if (d.BuffType > 0 || d.Boost != PermanentBoost.None || d.LifeCrystal || d.ManaCrystal) return 16;
            if (d.Places != World.CellKind.Empty || d.PlacesChest || d.PlacesStation != World.StationKind.None) return 17;
            if (d.Material) return 18;
            return 19;
        }

        /// <summary>Within a layer: the damage of a weapon or an ammo, a tool's power, armor's defense, a potion's healing.</summary>
        static int Strength(ItemDef d)
        {
            if (d.PickPower > 0) return d.PickPower;
            if (d.AxePower > 0 || d.HammerPower > 0) return Mathf.Max(d.AxePower * 5, d.HammerPower);
            if (d.IsArmor) return d.Defense;
            if (d.HealLife > 0) return d.HealLife;
            if (d.HealMana > 0) return d.HealMana;
            return d.Damage;
        }
    }
}
