using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The Goblin Tinkerer's forging (the mod's own, beside Terraria's reforging): a weapon in his slot, paid for in Geo,
    /// goes up a level, each level +10% of its damage (on its damage before forging, prefix included). Each stage unlocked
    /// allows five levels more (+5 at the start, +30 at the last). The price rises with the weapon's worth (its plain
    /// reforge price) and its level; a forging takes one time in two,
    /// the money gone and the level kept. Each weapon carries its own level (ItemStack.Forge, kept in the save); a reforge
    /// keeps it.
    /// </summary>
    internal static class Forging
    {
        public const int LevelsPerStage = 5;
        public const float DamagePerLevel = 0.1f;

        /// <summary>The highest level the stage reached allows.</summary>
        public static int MaxLevel => LevelsPerStage * (Mathf.Clamp(Shop.UnlockedStage(), 0, 5) + 1);

        /// <summary>A weapon: an item that deals damage, of a class, one to a stack (not ammo, not thrown stacks).</summary>
        public static bool CanForge(ItemDef d) =>
            d != null && d.Type != ItemType.None && d.Damage > 0 && d.Class != DamageClass.None && d.MaxStack <= 1 && !d.IsAmmo && !d.Consumable;

        /// <summary>
        /// What a kind of weapon makes of its damage: a minion staff's lasts (and summons more than one), a sentry's
        /// stands, a whip tags; a pickaxe, axe or hammer is a tool first.
        /// </summary>
        static float KindFactor(ItemDef d)
        {
            if (d.Minion != MinionKind.None) return 1.5f;
            if (d.Sentry) return 1.3f;
            if (d.Whip != WhipKind.None) return 1.2f;
            if (d.PickPower > 0 || d.AxePower > 0 || d.HammerPower > 0) return 0.6f;
            return 1f;
        }

        /// <summary>The weapon's own strength, before its prefix and its forging: its damage a use, by how fast it is used (one use in half a second as one), times its kind's.</summary>
        public static float Strength(ItemDef plain)
        {
            float ticks = Mathf.Max(1f, plain.UseTime * 60f);
            return plain.Damage * Mathf.Clamp(30f / ticks, 0.5f, 3f) * KindFactor(plain);
        }

        /// <summary>
        /// The cost of the next level, in copper (a Geo is 100): dear, as the plainest way to more damage, yet not out of
        /// reach for the strongest (whose damage isn't that much more): 20 Geo × the weapon's strength to the power 0.8
        /// (the Night's Edge about 500 Geo, the Terra Blade 1150, the Zenith 3150), 20 Geo at the least; ×(1 + level/2).
        /// </summary>
        public static long Cost(ItemStack s)
        {
            if (s.IsEmpty) return 0;
            var plain = ItemDb.Get(s.Type);
            double basePrice = System.Math.Pow(Strength(plain), 0.8) * 2000.0;
            if (basePrice < 2000) basePrice = 2000;
            long cost = (long)System.Math.Round(basePrice * (1.0 + 0.5 * s.Forge));
            return (cost + 99) / 100 * 100;
        }

        /// <summary>Dev: a weapon's price for each step up to a level, and their sum, in copper.</summary>
        public static long DevTotal(ItemType t, int toLevel)
        {
            long sum = 0;
            var s = ItemStack.Of(t);
            for (int l = 0; l < toLevel; l++) { s.Forge = (byte)l; sum += Cost(s); }
            return sum;
        }

        /// <summary>The chance the next level takes: one in two, whatever the weapon and its level.</summary>
        public const float Odds = 0.5f;

        public static float Chance(ItemStack s) => s.IsEmpty ? 0f : Odds;

        public enum Result { Forged, Failed, Poor, AtMax, NotWeapon }

        public static int DevForged, DevFailed;
        /// <summary>Dev: the next roll's outcome forced (null: the dice).</summary>
        public static bool? DevOutcome;

        /// <summary>One forging: paid, then the dice. The stack's level up a step when it takes.</summary>
        public static Result Try(ref ItemStack s)
        {
            if (s.IsEmpty || !CanForge(ItemDb.Get(s.Type))) return Result.NotWeapon;
            if (s.Forge >= MaxLevel) return Result.AtMax;
            if (!Shop.Spend(Cost(s))) return Result.Poor;
            bool took = DevOutcome ?? Random.value < Chance(s);
            if (!took) { DevFailed++; return Result.Failed; }
            s.Forge++;
            DevForged++;
            return Result.Forged;
        }

        sealed class Entry
        {
            public ItemDef Def;
            public int BaseDamage;
        }

        static readonly Dictionary<(ItemDef, byte), Entry> cache = new Dictionary<(ItemDef, byte), Entry>();
        static string cacheCulture;

        /// <summary>The forged copy of a definition (its prefix's, if any): its damage up 10% a level, " +N" after its name.</summary>
        public static ItemDef Apply(ItemDef b, byte level)
        {
            // Only a weapon is forged (a treasure bag keeps a mark in the same byte: TreasureBags).
            if (b == null || level == 0) return b;
            if (!CanForge(b.Unprefixed ?? b)) return TreasureBags.Is(b.Type) ? TreasureBags.FirstKill(b) : b;
            if (cacheCulture != Lang.Culture)
            {
                cache.Clear();
                cacheCulture = Lang.Culture;
            }
            // The nails' damage grows with the stage: the copy follows its base.
            if (cache.TryGetValue((b, level), out var e))
            {
                if (e.BaseDamage != b.Damage)
                {
                    e.Def.Damage = Forged(b.Damage, level);
                    e.BaseDamage = b.Damage;
                }
                return e.Def;
            }
            var c = b.Clone();
            c.Forge = level;
            c.Unforged = b;
            c.Damage = Forged(b.Damage, level);
            c.Name = b.Name + " +" + level;
            cache[(b, level)] = new Entry { Def = c, BaseDamage = b.Damage };
            return c;
        }

        public static int Forged(int damage, int level) => Mathf.RoundToInt(damage * (1f + DamagePerLevel * level));
    }
}
