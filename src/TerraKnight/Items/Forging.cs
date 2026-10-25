using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The Goblin Tinkerer's forging (the mod's own, beside Terraria's reforging): a weapon in his slot, paid for in Geo,
    /// goes up a level, each level +5% of its damage (on its damage before forging, prefix included). A weapon is forged
    /// up to its own stage's modest cap (<see cref="StageCap"/>: +2, +3, +4, +5, +6, +6), its base
    /// damage raised the more the earlier it is (ProgressionBalance.WeaponBoost): equipment supplies the main progression; the Knight's nail and
    /// the Dream Nail, whose damage follows the stage reached, to the stage reached's. One forged past
    /// its cap before (an old save's) keeps its level and is forged no further. The price rises with the weapon's worth
    /// (its plain reforge price) and its level; a forging takes three times in four, the money gone and the level kept. Each
    /// weapon carries its own level (ItemStack.Forge, kept in the save); a reforge keeps it. A shop pays for the levels too:
    /// a fifth of their prices, with the total sale capped at the cheapest forging investment (Shop.SellValue).
    /// A piece of armor is forged too, up
    /// to the same stage caps as weapons, each level +1 defense, a warrior's piece +2 (<see cref="IsWarriorArmor"/>).
    /// Its full-upgrade price is about 10000 Geo a stage. Forging is dear on purpose (the user's, 2026-10-21: few should lean on it,
    /// the game's own gear first).
    /// </summary>
    internal static class Forging
    {
        public const float DamagePerLevel = 0.05f;
        /// <summary>Armor uses the shared stage cap; each level adds one defense (a warrior's two).</summary>
        public const int DefensePerLevel = 1, WarriorDefensePerLevel = 2;

        /// <summary>The defense a level of forging adds to a piece of armor: two for a warrior's, one for any other.</summary>
        public static int DefensePerLevelOf(ItemDef d) => IsWarriorArmor(d) ? WarriorDefensePerLevel : DefensePerLevel;

        static readonly Dictionary<ItemType, bool> warrior = new Dictionary<ItemType, bool>();

        /// <summary>
        /// A warrior's (melee) head, body or legs: one of the warrior's armor path (ProgressionBalance.ArmorPaths' melee kits: iron,
        /// gold, shadow, molten, titanium, beetle), or a piece whose own bonuses with its set's lean to melee at least as much as to
        /// any other class (the cobalt, mythril and adamantite helmets, the turtle, the solar flare, the frost, the monk's...). A piece
        /// for no class (copper, wood), for every class alike or for another isn't.
        /// </summary>
        public static bool IsWarriorArmor(ItemDef d)
        {
            if (d == null) return false;
            var plain = ItemDb.Get(d.Type);
            if (plain == null || !plain.IsArmor) return false;
            if (warrior.TryGetValue(plain.Type, out bool known)) return known;
            bool yes = false;
            foreach (var path in ProgressionBalance.ArmorPaths)
                if (path.Class == Kit.Melee && System.Array.IndexOf(path.Items, plain.Type) >= 0) yes = true;
            if (!yes)
            {
                var own = plain.Bonus;
                var set = Equipment.SetBonus(plain.SetBonus != ArmorSet.None ? plain.SetBonus : plain.Set);
                float melee = own.MeleeDamage + own.MeleeCrit + own.MeleeSpeed + set.MeleeDamage + set.MeleeCrit + set.MeleeSpeed;
                float ranged = own.RangedDamage + own.RangedCrit + own.ArrowDamage + set.RangedDamage + set.RangedCrit + set.ArrowDamage;
                float magic = own.MagicDamage + own.MagicCrit + own.ManaCost + own.ManaMax / 200f + set.MagicDamage + set.MagicCrit + set.ManaCost + set.ManaMax / 200f;
                float summon = own.MinionDamage + 0.1f * (own.MaxMinions + own.MaxTurrets) + set.MinionDamage + 0.1f * (set.MaxMinions + set.MaxTurrets);
                // A piece for every class alike (the ninja's, the hardmode families' shared chests and legs) is no warrior's.
                bool everyClass = Mathf.Approximately(melee, ranged) && Mathf.Approximately(melee, magic);
                yes = melee > 0f && melee >= ranged && melee >= magic && melee >= summon && !everyClass;
            }
            warrior[plain.Type] = yes;
            return yes;
        }

        public static int ArmorStageCap(int stage) => StageCap(stage);

        /// <summary>A small optional upgrade: stages 0 to 5 allow +2, +3, +4, +5, +6, +6 (the user's, 2026-10-23).</summary>
        public static int StageCap(int stage) => stageCaps[Mathf.Clamp(stage, 0, 5)];

        static readonly int[] stageCaps = { 2, 3, 4, 5, 6, 6 };

        /// <summary>The cap of the stage reached (the Knight's nail's and the Dream Nail's).</summary>
        public static int MaxLevel => StageCap(Shop.UnlockedStage());

        /// <summary>The highest level a weapon is forged to: its own stage's cap; the nails', the stage reached's.</summary>
        public static int MaxLevelOf(ItemType t)
        {
            if (t == ItemType.KnightNail || t == ItemType.DreamNail) return MaxLevel;
            var d = ItemDb.Get(t);
            if (IsArmorPiece(d)) return ArmorStageCap(d.Stage);
            return StageCap(d != null ? d.Stage : 0);
        }

        public static bool AtMax(ItemStack s) => !s.IsEmpty && s.Forge >= MaxLevelOf(s.Type);

        /// <summary>A weapon: an item that deals damage, of a class, one to a stack (not ammo, not thrown stacks).</summary>
        public static bool CanForge(ItemDef d) =>
            d != null && d.Type != ItemType.None && d.Damage > 0 && d.Class != DamageClass.None && d.MaxStack <= 1 && !d.IsAmmo && !d.Consumable;

        /// <summary>A piece of armor (a helmet, a chestpiece, leggings), one to a stack.</summary>
        public static bool IsArmorPiece(ItemDef d) => d != null && d.Type != ItemType.None && d.IsArmor && d.MaxStack <= 1 && !CanForge(d);

        /// <summary>What the Goblin Tinkerer forges: a weapon or a piece of armor.</summary>
        public static bool Forgeable(ItemDef d) => CanForge(d) || IsArmorPiece(d);

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
        /// reach for the strongest (whose damage isn't that much more): 40 Geo × the weapon's strength to the power 0.8
        /// (the Night's Edge about 1000 Geo, the Terra Blade 2300, the Zenith 6300), 40 Geo at the least; ×(1 + level/2).
        /// Four fifths of it with the Discount Card (<see cref="Shop.Discounted"/>).
        /// </summary>
        public static long Cost(ItemStack s) => Shop.Discounted(ListCost(s));

        /// <summary>The next level's price before any discount (what a sale's levels are worth).</summary>
        static long ListCost(ItemStack s)
        {
            if (s.IsEmpty) return 0;
            var plain = ItemDb.Get(s.Type);
            if (IsArmorPiece(plain)) return ArmorCost(plain, s.Forge);
            double basePrice = System.Math.Pow(Strength(plain), 0.8) * 4000.0;
            if (basePrice < 4000) basePrice = 4000;
            long cost = (long)System.Math.Round(basePrice * (1.0 + 0.5 * s.Forge));
            return (cost + 99) / 100 * 100;
        }

        /// <summary>
        /// Armor's full-upgrade budget is about 10000 Geo per stage (10000 .. 60000).
        /// Spread it across its stage's cap, with the last step costing three times the first.
        /// Round each attempt up to whole Geo; this adds less than one Geo per available level.
        /// </summary>
        public static long ArmorCost(ItemDef plain, int level)
        {
            int stage = Mathf.Clamp(plain.Stage, 0, 5), cap = ArmorStageCap(stage);
            long budget = 1000000L * (stage + 1);
            long numerator = budget * (cap - 1 + 2L * Mathf.Max(0, level));
            long denominator = 2L * cap * (cap - 1) * 100;
            return (numerator + denominator - 1) / denominator * 100;
        }

        /// <summary>Prices for each successful step, in copper. Minimum paid assumes a discount and no failed attempts.</summary>
        public static long PricesTo(ItemType t, int toLevel, bool minimumPaid = false)
        {
            long sum = 0;
            var s = ItemStack.Of(t);
            for (int l = 0; l < toLevel; l++)
            {
                s.Forge = (byte)l;
                long cost = ListCost(s);
                sum += minimumPaid ? Shop.Discounted(cost, true) : cost;
            }
            return sum;
        }

        public static long DevTotal(ItemType t, int toLevel) => PricesTo(t, toLevel);

        /// <summary>What a shop pays for a weapon's levels, in copper: a fifth of their prices (its levels' worth, as a sale pays a fifth of an item's).</summary>
        public static long SellBonus(ItemStack s) => s.IsEmpty || s.Forge == 0 || !Forgeable(ItemDb.Get(s.Type)) ? 0 : PricesTo(s.Type, s.Forge) / 5;

        /// <summary>The chance the next level takes: three in four, whatever the weapon and its level.</summary>
        public const float Odds = 0.75f;

        public static float Chance(ItemStack s) => s.IsEmpty ? 0f : Odds;

        public enum Result { Forged, Failed, Poor, AtMax, NotWeapon }

        public static int DevForged, DevFailed;
        /// <summary>Dev: the next roll's outcome forced (null: the dice).</summary>
        public static bool? DevOutcome;

        /// <summary>One forging: paid, then the dice. The stack's level up a step when it takes.</summary>
        public static Result Try(ref ItemStack s)
        {
            if (s.IsEmpty || !Forgeable(ItemDb.Get(s.Type))) return Result.NotWeapon;
            if (AtMax(s)) return Result.AtMax;
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

        /// <summary>
        /// The forged copy of a definition (its prefix's, if any): a weapon's damage up 5% a level, " +N" after its name; a
        /// piece of armor's defense +1 a level, its level and its highest after its name, unforged too ("Wood Helmet(Lv.1/Lv.3)").
        /// </summary>
        public static ItemDef Apply(ItemDef b, byte level)
        {
            if (b == null) return b;
            bool armor = IsArmorPiece(b.Unprefixed ?? b);
            if (level == 0 && !armor) return b;
            // Only a weapon or armor is forged (a treasure bag keeps a mark in the same byte: TreasureBags).
            if (!armor && !Forgeable(b.Unprefixed ?? b)) return TreasureBags.Is(b.Type) ? TreasureBags.Marked(b,level) : b;
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
            if (armor)
            {
                c.Defense = b.Defense + DefensePerLevelOf(b) * level;
                c.Name = $"{b.Name}(Lv.{level}/Lv.{MaxLevelOf((b.Unprefixed ?? b).Type)})";
            }
            else c.Name = b.Name + " +" + level;
            cache[(b, level)] = new Entry { Def = c, BaseDamage = b.Damage };
            return c;
        }

        public static int Forged(int damage, int level) => Mathf.RoundToInt(damage * (1f + DamagePerLevel * level));

        /// <summary>
        /// A ranged weapon's levels (a bow's, a gun's, a launcher's...) each add, to what it fires (the user's): a point of armor
        /// penetration, half a point of crit chance, and a hundredth more on a crit (2× at +0, 2.06× at +6).
        /// </summary>
        public const int RangedPenPerLevel = 1;
        public const float RangedCritPerLevel = 0.5f, RangedCritDamagePerLevel = 0.01f;

        /// <summary>The forging level a weapon's shots carry: a forged ranged weapon's, 0 for the rest.</summary>
        public static int RangedLevel(ItemDef d) => d != null && d.Forge > 0 && d.Class == DamageClass.Ranged && !IsArmorPiece(d.Unforged ?? d) ? d.Forge : 0;

        public static int RangedPen(int level) => level * RangedPenPerLevel;
        /// <summary>Crit chance in points (Equipment.Crit's scale).</summary>
        public static float RangedCrit(int level) => level * RangedCritPerLevel;
        /// <summary>What a crit does on top of its double (0.06 at +6: 2.06×).</summary>
        public static float RangedCritDamage(int level) => level * RangedCritDamagePerLevel;

        public static int ArmorPenOf(ItemDef d) => RangedPen(RangedLevel(d));
    }
}
