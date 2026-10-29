using System;
using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>A weapon's own edition: 0 inherits the world, 1 original, 2 remade.</summary>
    internal static class WeaponRefitting
    {
        static readonly Dictionary<ItemDef, ItemDef[]> copies = new Dictionary<ItemDef, ItemDef[]>();
        static bool? context;
        public static bool Effective(byte version) => version == 0 ? World.WeaponMode.New : version == 2;
        public static bool Enabled => context ?? Effective(TerraMod.Instance?.Inventory.Current.WeaponVersion ?? 0);
        public static bool EnabledFor(ItemDef d) => d != null && Effective(d.WeaponVersion);
        public readonly struct Scope : IDisposable
        {
            readonly bool? before;
            public Scope(bool enabled) { before = context; context = enabled; }
            public void Dispose() { context = before; }
        }
        public static ItemDef Apply(ItemDef d, byte version)
        {
            if (version == 0) return d;
            if (!copies.TryGetValue(d, out var editions)) copies.Add(d, editions = new ItemDef[2]);
            int i = version == 2 ? 1 : 0;
            if (editions[i] == null) { editions[i] = d.Clone(); editions[i].WeaponVersion = (byte)(i + 1); }
            return editions[i];
        }
        public static bool CanRefit(ItemDef d)
        {
            if (d == null || !d.IsWeapon || d.IsAmmo || d.Consumable || d.MaxStack != 1 || d.GameTool
                || d.PickPower > 0 || d.AxePower > 0 || d.HammerPower > 0) return false;
            var remade = Apply(ItemDb.Get(d.Type), 2);
            return BladeArts.Is(remade) || d.Class == DamageClass.Magic || d.Class == DamageClass.Ranged && !RangedArts.OwnWay(d.Type)
                || BladeSkills.KindArtOf(remade) != null || d.IsWhip && d.Whip != WhipKind.MoonLordWhip;
        }
        static readonly int[] prices = { 250, 450, 800, 1300, 2000, 3000 };
        public static long Cost(ItemStack item)
        {
            if (item.IsEmpty || !CanRefit(item.Def)) return 0;
            var d = ItemDb.Get(item.Type);
            return (prices[Mathf.Clamp(d.Stage, 0, 5)] + 75L * Mathf.Clamp(d.Rare, 0, 11)) * 100L;
        }
        public static bool Convert(ref ItemStack item)
        {
            if (item.IsEmpty || item.Count != 1 || !CanRefit(item.Def)) return false;
            long cost = Cost(item);
            if (!Shop.Spend(cost)) return false;
            item.WeaponVersion = Effective(item.WeaponVersion) ? (byte)1 : (byte)2;
            return true;
        }
        public static string Name(ItemStack item) => World.WeaponMode.NameOf(Effective(item.WeaponVersion) ? World.WeaponKind.New : World.WeaponKind.Vanilla);
    }
}
