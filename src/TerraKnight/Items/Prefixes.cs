using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Terraria 1.4.5's item prefixes (PrefixID; Item.TryGetPrefixStatMultipliersForItem, Item.Prefix, PrefixLegacy,
    /// Player.GrantPrefixBenefits, Main.MouseText_DrawItemTooltip's prefix lines). A stack's prefix
    /// (<see cref="ItemStack.Prefix"/>) makes its definition a copy with the prefix's numbers: damage, use time, mana,
    /// knockback, size, velocity, crit, a summon weapon's armour penetration and tag damage, an accessory's defense,
    /// mana, crit, damage, movement or melee speed; its value (×the prefix's worth squared), rarity and name. Which
    /// prefixes an item may have goes by its Terraria item (PrefixLegacy.ItemSets, its Item_N picture). Reforging at the
    /// Goblin Tinkerer (Item.Prefix(-2)) always gives one.
    /// </summary>
    internal static class Prefixes
    {
        /// <summary>PrefixID's names, by id (the localization's "Prefix.Name").</summary>
        static readonly string[] names =
        {
            "", "Large", "Massive", "Dangerous", "Savage", "Sharp", "Pointy", "Tiny", "Terrible", "Small", "Dull", "Unhappy", "Bulky", "Shameful",
            "Heavy", "Light", "Sighted", "Rapid", "Hasty", "Intimidating", "Deadly", "Staunch", "Awful", "Lethargic", "Awkward", "Powerful", "Mystic",
            "Adept", "Masterful", "Inept", "Ignorant", "Deranged", "Intense", "Taboo", "Celestial", "Furious", "Keen", "Superior", "Forceful", "Broken",
            "Damaged", "Shoddy", "Quick", "Deadly2", "Agile", "Nimble", "Murderous", "Slow", "Sluggish", "Lazy", "Annoying", "Nasty", "Manic", "Hurtful",
            "Strong", "Unpleasant", "Weak", "Ruthless", "Frenzying", "Godly", "Demonic", "Zealous", "Hard", "Guarding", "Armored", "Warding", "Arcane",
            "Precise", "Lucky", "Jagged", "Spiked", "Angry", "Menacing", "Brisk", "Fleeting", "Hasty2", "Quick2", "Wild", "Rash", "Intrepid", "Violent",
            "Legendary", "Unreal", "Mythical", "Legendary2", "Fabled", "Loyal", "Worthy", "Focused", "Patient", "Rabid", "IllTempered", "Petty", "Feeble",
            "Skittish", "Eager", "Ballistic", "Scraggling",
        };

        public static int Count => names.Length;

        /// <summary>Hard, Guarding, Armored: +1, +2, +3 defense as in Terraria; Warding (护佑) +6, Terraria's +4 raised.</summary>
        public static int AccessoryDefense(int prefix) => prefix == 65 ? 6 : prefix >= 62 && prefix <= 64 ? prefix - 61 : 0;

        /// <summary>A prefix's multipliers and additions (TryGetPrefixStatMultipliersForItem's dmg, kb, spd, size, shtspd, mcst, crt, tagdmg, arpen).</summary>
        struct Stat
        {
            public float Dmg, Kb, Spd, Size, Sht, Mcst;
            public int Crt, Tag, Pen;
        }

        static Stat S(float dmg = 1f, float kb = 1f, float spd = 1f, float size = 1f, float sht = 1f, float mcst = 1f, int crt = 0, int tag = 0, int pen = 0) =>
            new Stat { Dmg = dmg, Kb = kb, Spd = spd, Size = size, Sht = sht, Mcst = mcst, Crt = crt, Tag = tag, Pen = pen };

        static readonly Dictionary<int, Stat> stats = new Dictionary<int, Stat>
        {
            [1] = S(size: 1.12f), [2] = S(size: 1.18f), [3] = S(dmg: 1.05f, crt: 2, size: 1.05f), [4] = S(dmg: 1.1f, size: 1.1f, kb: 1.1f), [5] = S(dmg: 1.15f),
            [6] = S(dmg: 1.1f), [81] = S(kb: 1.15f, dmg: 1.15f, crt: 5, spd: 0.9f, size: 1.1f), [7] = S(size: 0.82f), [8] = S(kb: 0.85f, dmg: 0.85f, size: 0.87f),
            [9] = S(size: 0.9f), [10] = S(dmg: 0.85f), [11] = S(spd: 1.1f, kb: 0.9f, size: 0.9f), [12] = S(kb: 1.1f, dmg: 1.05f, size: 1.1f, spd: 1.15f),
            [13] = S(kb: 0.8f, dmg: 0.9f, size: 1.1f), [14] = S(kb: 1.15f, spd: 1.1f), [15] = S(kb: 0.9f, spd: 0.85f), [16] = S(dmg: 1.1f, crt: 3),
            [17] = S(spd: 0.85f, sht: 1.1f), [18] = S(spd: 0.9f, sht: 1.15f), [19] = S(kb: 1.15f, sht: 1.05f), [20] = S(kb: 1.05f, sht: 1.05f, dmg: 1.1f, spd: 0.95f, crt: 2),
            [21] = S(kb: 1.15f, dmg: 1.1f), [82] = S(kb: 1.15f, dmg: 1.15f, crt: 5, spd: 0.9f, sht: 1.1f), [22] = S(kb: 0.9f, sht: 0.9f, dmg: 0.85f),
            [23] = S(spd: 1.15f, sht: 0.9f), [24] = S(spd: 1.1f, kb: 0.8f), [25] = S(spd: 1.1f, dmg: 1.15f, crt: 1), [58] = S(spd: 0.85f, dmg: 0.85f),
            [26] = S(mcst: 0.85f, dmg: 1.1f), [27] = S(mcst: 0.85f), [28] = S(mcst: 0.85f, dmg: 1.15f, kb: 1.05f), [83] = S(kb: 1.15f, dmg: 1.15f, crt: 5, spd: 0.9f, mcst: 0.9f),
            [29] = S(mcst: 1.1f), [30] = S(mcst: 1.2f, dmg: 0.9f), [31] = S(kb: 0.9f, dmg: 0.9f), [32] = S(mcst: 1.15f, dmg: 1.1f), [33] = S(mcst: 1.1f, kb: 1.1f, spd: 0.9f),
            [34] = S(mcst: 0.9f, kb: 1.1f, spd: 1.1f, dmg: 1.1f), [35] = S(mcst: 1.2f, dmg: 1.15f, kb: 1.15f), [52] = S(mcst: 0.9f, dmg: 0.9f, spd: 0.9f),
            [85] = S(dmg: 1.15f, kb: 1.15f, pen: 10, tag: 3), [86] = S(dmg: 1.1f, kb: 1.05f, pen: 5, tag: 3), [87] = S(dmg: 1.15f, pen: 8), [88] = S(dmg: 1.1f, tag: 3),
            [89] = S(dmg: 0.95f, tag: 3), [90] = S(dmg: 1.1f, kb: 0.9f), [91] = S(dmg: 0.95f, pen: 10), [92] = S(dmg: 0.7f), [93] = S(kb: 0.75f), [94] = S(dmg: 0.85f, kb: 0.9f),
            [95] = S(pen: 25), [96] = S(tag: 5), [97] = S(kb: 1.25f), [84] = S(kb: 1.17f, dmg: 1.17f, crt: 8), [36] = S(crt: 3), [37] = S(dmg: 1.1f, crt: 3, kb: 1.1f),
            [38] = S(kb: 1.15f), [53] = S(dmg: 1.1f), [54] = S(kb: 1.15f), [55] = S(kb: 1.15f, dmg: 1.05f), [59] = S(kb: 1.15f, dmg: 1.15f, crt: 5), [60] = S(dmg: 1.15f, crt: 5),
            [61] = S(crt: 5), [39] = S(dmg: 0.7f, kb: 0.8f), [40] = S(dmg: 0.85f), [56] = S(kb: 0.8f), [41] = S(kb: 0.85f, dmg: 0.9f), [57] = S(kb: 0.9f, dmg: 1.18f),
            [42] = S(spd: 0.9f), [43] = S(dmg: 1.1f, spd: 0.9f), [44] = S(spd: 0.9f, crt: 3), [45] = S(spd: 0.95f), [46] = S(crt: 3, spd: 0.94f, dmg: 1.07f),
            [47] = S(spd: 1.15f), [48] = S(spd: 1.2f), [49] = S(spd: 1.08f), [50] = S(dmg: 0.8f, spd: 1.15f), [51] = S(kb: 0.9f, spd: 0.9f, dmg: 1.05f, crt: 2),
        };

        // PrefixLegacy.Prefixes: what each kind of item may roll.
        static readonly int[] swords = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 36, 37, 38, 53, 54, 55, 39, 40, 56, 41, 57, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 59, 60, 61, 81 };
        static readonly int[] spears = { 36, 37, 38, 53, 54, 55, 39, 40, 56, 41, 57, 59, 60, 61 };
        static readonly int[] gunsBows = { 16, 17, 18, 19, 20, 21, 22, 23, 24, 25, 58, 36, 37, 38, 53, 54, 55, 39, 40, 56, 41, 57, 42, 44, 45, 46, 47, 48, 49, 50, 51, 59, 60, 61, 82 };
        static readonly int[] magic = { 26, 27, 28, 29, 30, 31, 32, 33, 34, 35, 52, 36, 37, 38, 53, 54, 55, 39, 40, 56, 41, 57, 42, 43, 44, 45, 46, 47, 48, 49, 50, 51, 59, 60, 61, 83 };
        static readonly int[] summons = { 85, 86, 87, 88, 89, 90, 91, 92, 93, 94, 95, 96, 97, 55, 38, 54, 53, 57, 40, 56, 41, 39 };
        static readonly int[] boomerangs = { 36, 37, 38, 53, 54, 55, 39, 40, 56, 41, 57, 59, 60, 61 };
        static readonly int[] terrarian = { 36, 37, 38, 53, 54, 55, 39, 40, 56, 41, 57, 59, 60, 61, 84 };
        static readonly int[] accessories = { 62, 63, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 80 };

        // PrefixLegacy.ItemSets: Terraria's items of each kind.
        static readonly HashSet<int> setBoomerangs = new HashSet<int> { 55, 119, 191, 284, 670, 1122, 1513, 1569, 1571, 1825, 1918, 3054, 3262, 3278, 3279, 3280, 3281, 3282, 3283, 3284, 3285, 3286, 3287, 3288, 3289, 3290, 3291, 3292, 3315, 3316, 3317, 5294, 3030, 3543, 4764, 4818, 4760, 561, 1324, 5298, 5519 };
        static readonly HashSet<int> setMagic = new HashSet<int> { 64, 112, 113, 127, 157, 165, 218, 272, 494, 495, 496, 514, 517, 518, 519, 683, 726, 739, 740, 741, 742, 743, 744, 788, 1121, 1155, 1178, 1244, 1256, 1260, 1264, 1266, 1295, 1296, 1308, 1313, 1336, 1444, 1445, 1446, 1801, 1930, 1931, 2188, 2622, 2623, 2750, 2795, 2882, 3006, 3014, 3051, 3053, 3069, 3105, 3209, 3269, 3377, 3476, 3541, 3542, 3570, 3779, 3787, 3852, 3870, 4062, 4270, 4347, 4348, 4715, 4952, 5065, 5118, 5147, 5461, 6152, 6153, 6154, 6155, 6171, 6173 };
        static readonly HashSet<int> setSummon = new HashSet<int> { 1157, 1309, 1572, 1802, 2364, 2365, 2366, 2535, 2551, 2584, 2621, 2749, 3249, 3474, 3531, 3569, 3571, 3818, 3819, 3820, 3824, 3825, 3826, 3829, 3830, 3831, 3832, 3833, 3834, 4269, 4273, 4281, 4607, 4758, 5005, 5069, 5114, 5119, 5456, 5463, 5663, 5664, 6148, 6149, 6161, 6164 };
        static readonly HashSet<int> setGunsBows = new HashSet<int> { 39, 44, 95, 96, 98, 99, 120, 164, 197, 219, 266, 281, 434, 435, 436, 481, 506, 533, 534, 578, 655, 658, 661, 679, 682, 725, 758, 759, 760, 796, 800, 905, 923, 964, 986, 1156, 1187, 1194, 1201, 1229, 1254, 1255, 1258, 1265, 1319, 1553, 1782, 1784, 1835, 1870, 1910, 1929, 1946, 2223, 2269, 2270, 2624, 2515, 2747, 2796, 2797, 3052, 2888, 3019, 3029, 3007, 3008, 3210, 3107, 3475, 3540, 3854, 3859, 3821, 3930, 3480, 3486, 3492, 3498, 3504, 3510, 3516, 3350, 3546, 3788, 4058, 4060, 4381, 4703, 4953, 5117, 5282, 5460 };
        static readonly HashSet<int> setSpears = new HashSet<int> { 4463, 162, 5011, 5012, 160, 163, 220, 274, 277, 280, 383, 384, 385, 386, 387, 388, 389, 390, 406, 537, 550, 579, 756, 801, 802, 1186, 1189, 1190, 1193, 1196, 1197, 1200, 1203, 1204, 1228, 1231, 1232, 1259, 1262, 1297, 1314, 1325, 1947, 2332, 2331, 2342, 2424, 2611, 5526, 2798, 3012, 3473, 3098, 3368, 3835, 3836, 3858, 4061, 4144, 4272, 2774, 2773, 2779, 2778, 2784, 2783, 3464, 3463, 4788, 4789, 4790, 5687 };
        static readonly HashSet<int> setSwords = new HashSet<int> { 1, 4, 6, 7, 10, 24, 45, 46, 65, 103, 104, 121, 122, 155, 190, 196, 198, 199, 200, 201, 202, 203, 4258, 204, 213, 217, 273, 367, 368, 426, 482, 483, 484, 653, 654, 656, 657, 659, 660, 671, 672, 674, 675, 676, 723, 724, 757, 776, 777, 778, 787, 795, 797, 798, 799, 881, 882, 921, 922, 989, 990, 991, 992, 993, 1123, 1166, 1185, 1188, 1192, 1195, 1199, 1202, 1222, 1223, 1224, 1226, 1227, 1230, 1233, 1234, 1294, 1304, 1305, 1306, 1320, 1327, 1506, 1507, 1786, 1826, 1827, 1909, 1917, 1928, 2176, 2273, 2608, 2341, 2330, 2320, 2516, 2517, 2746, 2745, 3063, 3018, 3211, 3013, 3258, 3106, 3065, 2880, 3481, 3482, 3483, 3484, 3485, 3487, 3488, 3489, 3490, 3491, 3493, 3494, 3495, 3496, 3497, 3499, 3500, 3501, 3502, 3503, 3505, 3506, 3507, 3508, 3509, 3511, 3512, 3513, 3514, 3515, 3517, 3518, 3519, 3520, 3521, 3522, 3523, 3524, 3525, 3462, 3465, 3466, 2772, 2775, 2776, 2777, 2780, 2781, 2782, 2785, 2786, 3349, 3352, 3351, 3764, 3765, 3766, 3767, 3768, 3769, 4259, 3772, 3823, 3827, 186, 946, 4059, 4317, 486, 4707, 4711, 4956, 4923, 4672, 4913, 4912, 4911, 4678, 4679, 4680, 4914, 5074, 5094, 5095, 5096, 5097, 5283, 5284, 5129, 5295, 5382, 5462, 5473, 5474, 5475, 5476, 5477, 5478, 5479, 5480, 5535, 5536, 5669, 5670, 5671, 5688 };
        /// <summary>ItemID.Sets.CanGetPrefixes false: accessories that never take one (music boxes and the like).</summary>
        static readonly HashSet<int> noPrefix = new HashSet<int> { 267, 1307, 562, 563, 564, 565, 566, 567, 568, 569, 570, 571, 572, 573, 574, 576, 1596, 1597, 1598, 1599, 1600, 1601, 1602, 1603, 1604, 1605, 1606, 1607, 1608, 1609, 1610, 1963, 1964, 1965, 2742, 3044, 3235, 3236, 3237, 3370, 3371, 3796, 3869, 4077, 4078, 4079, 4080, 4081, 4082, 4237, 4356, 4357, 4358, 4421, 4606, 4979, 4985, 4990, 4991, 4992, 5006, 5014, 5015, 5016, 5017, 5018, 5019, 5020, 5021, 5022, 5023, 5024, 5025, 5026, 5027, 5028, 5029, 5030, 5031, 5032, 5033, 5034, 5035, 5036, 5037, 5038, 5039, 5040, 5044, 5112, 5362, 5538, 5578, 5579, 5580, 5581, 5582, 5637, 5638, 5639, 6144, 6145, 6146 };

        /// <summary>The Terraria item a definition stands for (its Item_N picture), 0 for the mod's own.</summary>
        public static int TerrariaId(ItemDef d)
        {
            if (d == null || d.Texture == null || !d.Texture.StartsWith("Item_")) return 0;
            return int.TryParse(d.Texture.Substring(5), out int id) ? id : 0;
        }

        /// <summary>Item.GetRollablePrefixes: the prefixes an item may have (null: none).</summary>
        static int[] Rollable(ItemDef d)
        {
            if (d == null) return null;
            // Hollow Knight's own weapons: the Nail as a sword (its size on the slash, its speed on the wait between
            // slashes), the Dream Nail with the prefixes every weapon may have (damage, crit, knockback).
            if (d.Type == ItemType.KnightNail) return swords;
            if (d.Type == ItemType.DreamNail) return spears;
            // The Knight's dashes worn (the game's own pictures): any accessory's prefixes.
            if (d.Type == ItemType.ShadeCloak || d.Type == ItemType.CrystalHeart || AbilityShields.Is(d.Type) || d.Type == ItemType.DescendingDarkRune) return accessories;
            if (d.MaxStack > 1 || d.Permanent || d.HkItem) return null;
            int id = TerrariaId(d);
            if (id <= 0) return OwnItem(d);
            if (setSwords.Contains(id)) return swords;
            if (setSpears.Contains(id)) return spears;
            if (setGunsBows.Contains(id)) return gunsBows;
            if (setMagic.Contains(id)) return magic;
            if (setSummon.Contains(id)) return summons;
            if (setBoomerangs.Contains(id)) return boomerangs;
            if (id == 3389) return terrarian;
            if (d.IsAccessory && !noPrefix.Contains(id)) return accessories;
            return null;
        }

        /// <summary>
        /// The mod's own weapons and accessories (their own pictures: the spells' weapons of the Vengeful Spirit, Howling
        /// Wraiths, Shade Soul and Abyss Shriek), sorted as Terraria's PrefixLegacy.ItemSets sort its own: a melee weapon
        /// swung with the swords, one thrown or held out (a yoyo, a bell) with the spears and boomerangs (the same prefixes),
        /// the rest by their class (the user's 10-27 "黑波装备无法重铸，是否其他都这样？": none of the sixteen could be reforged).
        /// </summary>
        static int[] OwnItem(ItemDef d)
        {
            if (d.IsAccessory) return accessories;
            if (d.Damage <= 0 || d.Consumable) return null;
            switch (d.Class)
            {
                case DamageClass.Melee: return d.NoMelee ? spears : swords;
                case DamageClass.Ranged: return gunsBows;
                case DamageClass.Magic: return magic;
                case DamageClass.Summon: return summons;
                default: return null;
            }
        }

        /// <summary>Item.CanHavePrefixes: whether it can be reforged.</summary>
        public static bool CanHave(ItemDef d) => Rollable(d) != null;

        public static bool IsAccessoryPrefix(int p) => p >= 62 && p <= 80;

        /// <summary>
        /// TryGetPrefixStatMultipliersForItem: the prefix's numbers and its worth (value), or false where it would change
        /// nothing it claims to (damage, use time, mana that round back to the same, knockback on none).
        /// </summary>
        static bool TryStats(ItemDef d, int p, out Stat s, out float value)
        {
            s = stats.TryGetValue(p, out var st) ? st : S();
            value = s.Dmg * (2f - s.Spd) * (2f - s.Mcst) * s.Size * s.Kb * s.Sht * (1f + s.Crt * 0.02f) * (1f + s.Pen * 0.015f) * (1f + s.Tag * 0.03f);
            if (p == 62 || p == 69 || p == 73 || p == 77) value *= 1.05f;
            if (p == 63 || p == 70 || p == 74 || p == 78 || p == 67) value *= 1.1f;
            if (p == 64 || p == 71 || p == 75 || p == 79 || p == 66) value *= 1.15f;
            if (p == 65 || p == 72 || p == 76 || p == 80 || p == 68) value *= 1.2f;
            int anim = Ticks(d.UseAnimation);
            if (s.Dmg != 1f && System.Math.Round(d.Damage * s.Dmg) == d.Damage) return false;
            if (s.Spd != 1f && System.Math.Round(anim * s.Spd) == anim) return false;
            if (s.Mcst != 1f && System.Math.Round(d.Mana * s.Mcst) == d.Mana) return false;
            if (s.Kb != 1f && d.Knockback == 0f) return false;
            return true;
        }

        static int Ticks(float seconds) => Mathf.RoundToInt(seconds * 60f);

        /// <summary>Item.BestPrefixValue: the worth of the best prefix the item may have.</summary>
        static float BestValue(ItemDef d)
        {
            var list = Rollable(d);
            float best = 0f;
            if (list == null) return best;
            foreach (int p in list)
                if (TryStats(d, p, out _, out float v)) best = Mathf.Max(best, v);
            return best;
        }

        /// <summary>Whether a prefix can stand on an item (a save's or a packet's checked).</summary>
        public static bool Valid(ItemDef d, int p)
        {
            var list = Rollable(d);
            return p > 0 && list != null && System.Array.IndexOf(list, p) >= 0 && TryStats(d, p, out _, out _);
        }

        /// <summary>Item.Prefix(-2) (a reforge): one of the item's prefixes at random, rolled again where it would change nothing; and whether it is the best.</summary>
        public static int Roll(ItemDef d, out bool best)
        {
            best = false;
            var list = Rollable(d);
            if (list == null) return 0;
            for (int tries = 0; tries < 200; tries++)
            {
                int p = list[Random.Range(0, list.Length)];
                if (!TryStats(d, p, out _, out float v)) continue;
                best = v == BestValue(d);
                return p;
            }
            return 0;
        }

        /// <summary>PrefixID.Sets.ReducedNaturalChance: the poor prefixes a natural roll keeps only one time in three.</summary>
        static readonly HashSet<int> reducedNatural = new HashSet<int> { 7, 8, 9, 10, 11, 22, 23, 24, 29, 30, 31, 39, 40, 56, 41, 47, 48, 49 };

        /// <summary>
        /// Item.Prefix(-1), an item found (an enemy's loot): one time in four none; else one of its prefixes, a poor one
        /// (ReducedNaturalChance) kept only one time in three; rolled again where it would change nothing. 0: none.
        /// </summary>
        public static int RollNatural(ItemDef d)
        {
            var list = Rollable(d);
            if (list == null || d.Type == ItemType.KnightNail || d.Type == ItemType.DreamNail) return 0;
            for (int tries = 0; tries < 200; tries++)
            {
                if (Random.Range(0, 4) == 0) return 0;
                int p = list[Random.Range(0, list.Length)];
                if (reducedNatural.Contains(p) && Random.Range(0, 3) != 0) return 0;
                if (TryStats(d, p, out _, out _)) return p;
            }
            return 0;
        }

        /// <summary>
        /// The cost of reforging (Main.DrawInventory: the item's value with its prefix, times the stack, a third), in copper;
        /// never under its rarity's and stage's floor (a geo a rarity step and a stage), so a rarer or later item never
        /// reforges for less than a plainer one whose value happens to be set higher. Four fifths of it with the Discount Card.
        /// </summary>
        public static long ReforgeCost(ItemStack s) => Shop.Discounted(ListReforgeCost(s));

        static long ListReforgeCost(ItemStack s)
        {
            if (s.IsEmpty) return 0;
            var d = s.Def;
            var plain = ItemDb.Get(s.Type);
            long floor = 100L * (Mathf.Max(0, plain.Rare) + 1) * (Mathf.Clamp(plain.Stage, 0, 5) + 1) * s.Count;
            long price = Shop.PriceOf(d);
            // The Nail and the Dream Nail have no price: 4 silver a point of damage, times the prefix's worth squared.
            if (price <= 0 && (s.Type == ItemType.KnightNail || s.Type == ItemType.DreamNail))
            {
                var b = ItemDb.Get(s.Type);
                float worth = 1f;
                if (s.Prefix > 0) TryStats(b, s.Prefix, out _, out worth);
                price = (long)(b.Damage * 400L * worth * worth);
            }
            if (s.Type == ItemType.KnightNail || s.Type == ItemType.DreamNail) return System.Math.Max(1L, price * s.Count / 3);
            return System.Math.Max(floor, price * s.Count / 3);
        }

        /// <summary>A prefixed copy, its prefix's numbers and the plain damage it was made from (the nails' grows with the stage).</summary>
        sealed class Entry
        {
            public ItemDef Def;
            public Stat S;
            public int BaseDamage;
        }

        static readonly Dictionary<(ItemDef, byte), Entry> cache = new Dictionary<(ItemDef, byte), Entry>();
        static string cacheCulture;

        /// <summary>The definition of an item with a prefix: a copy with its numbers, value, rarity and name (cached per language).</summary>
        public static ItemDef Apply(ItemDef b, byte p)
        {
            if (b == null || p == 0 || p >= names.Length) return b;
            if (cacheCulture != Lang.Culture)
            {
                cache.Clear();
                cacheCulture = Lang.Culture;
            }
            if (cache.TryGetValue((b, p), out var e))
            {
                if (e.BaseDamage != b.Damage)
                {
                    e.Def.Damage = (int)System.Math.Round(b.Damage * e.S.Dmg);
                    e.BaseDamage = b.Damage;
                }
                return e.Def;
            }
            var c = b.Clone();
            c.Prefix = p;
            c.Unprefixed = b;
            TryStats(b, p, out var s, out float value);
            c.Damage = (int)System.Math.Round(b.Damage * s.Dmg);
            c.UseAnimation = (float)System.Math.Round(Ticks(b.UseAnimation) * s.Spd) / 60f;
            c.UseTime = (float)System.Math.Round(Ticks(b.UseTime) * s.Spd) / 60f;
            c.Mana = (int)System.Math.Round(b.Mana * s.Mcst);
            c.Knockback = b.Knockback * s.Kb;
            c.Scale = b.Scale * s.Size;
            c.ShootSpeed = b.ShootSpeed * s.Sht;
            c.Crit = b.Crit + s.Crt;
            c.PrefixTagDamage = b.PrefixTagDamage + s.Tag;
            c.PrefixArmorPen = b.PrefixArmorPen + s.Pen;
            // Player.GrantPrefixBenefits: an accessory's own, while worn.
            switch (p)
            {
                case 62: case 63: case 64: case 65: c.Bonus.Defense += AccessoryDefense(p); break;
                case 66: c.Bonus.ManaMax += 20; break;
                case 67: case 68:
                {
                    float crit = p == 67 ? 0.02f : 0.04f;
                    c.Bonus.MeleeCrit += crit; c.Bonus.RangedCrit += crit; c.Bonus.MagicCrit += crit;
                    break;
                }
                case 69: case 70: case 71: case 72:
                {
                    float dmg = (p - 68) * 0.01f;
                    c.Bonus.MeleeDamage += dmg; c.Bonus.RangedDamage += dmg; c.Bonus.MagicDamage += dmg; c.Bonus.MinionDamage += dmg;
                    break;
                }
                case 73: case 74: case 75: case 76: c.Bonus.MoveSpeed += (p - 72) * 0.01f; break;
                case 77: case 78: case 79: case 80: c.Bonus.MeleeSpeed += (p - 76) * 0.01f; break;
            }
            int rare = b.Rare;
            // The mod's own colours (the nails' -11) stay.
            if (rare > -11)
            {
                if (value >= 1.2f) rare += 2;
                else if (value >= 1.05f) rare++;
                else if (value <= 0.8f) rare -= 2;
                else if (value <= 0.95f) rare--;
                rare = Mathf.Clamp(rare, -1, 11);
            }
            c.Rare = rare;
            c.Value = (int)(b.Value * value * value);
            c.Name = NameWith(b.Name, p);
            cache[(b, p)] = new Entry { Def = c, S = s, BaseDamage = b.Damage };
            return c;
        }

        /// <summary>A prefix's name in the language now (Terraria's "Prefix.Name").</summary>
        public static string Name(int p)
        {
            if (p <= 0 || p >= names.Length) return "";
            return Terraria.TerrariaText.Get(Lang.Culture, "Prefix." + names[p]) ?? Terraria.TerrariaText.Get("en-US", "Prefix." + names[p]) ?? names[p];
        }

        /// <summary>Lang.GetPrefixedItemName: CombineFormat.Prefix ("{PrefixName} {ItemName}", Chinese "‹{PrefixName}›{ItemName}").</summary>
        static string NameWith(string item, int p)
        {
            string format = Terraria.TerrariaText.Get(Lang.Culture, "CombineFormat.Prefix") ?? "{PrefixName} {ItemName}";
            return format.Replace("{PrefixName}", Name(p)).Replace("{ItemName}", item);
        }

        static string Tip(int n) => Terraria.TerrariaText.Get(Lang.Culture, "LegacyTooltip." + n) ?? Terraria.TerrariaText.Get("en-US", "LegacyTooltip." + n) ?? "";

        static string Signed(double v) => (v > 0 ? "+" : "") + v;

        /// <summary>Main.MouseText_DrawItemTooltip's prefix lines: each change from the plain item, good (green) or bad (red).</summary>
        public static List<(string Text, bool Good)> TooltipLines(ItemDef d)
        {
            var lines = new List<(string, bool)>();
            var b = d?.Unprefixed;
            if (b == null) return lines;
            if (b.Damage != d.Damage && b.Damage > 0)
            {
                double v = System.Math.Round((d.Damage - (double)b.Damage) / b.Damage * 100.0);
                lines.Add((Signed(v) + Tip(39), v >= 0));
            }
            int ba = Ticks(b.UseAnimation), da = Ticks(d.UseAnimation);
            if (ba != da && ba > 0)
            {
                double v = -System.Math.Round((da - (double)ba) / ba * 100.0);
                lines.Add((Signed(v) + Tip(40), v >= 0));
            }
            if (b.Crit != d.Crit) lines.Add((Signed(d.Crit - b.Crit) + Tip(41), d.Crit >= b.Crit));
            if (b.Mana != d.Mana && b.Mana > 0)
            {
                double v = System.Math.Round((d.Mana - (double)b.Mana) / b.Mana * 100.0);
                lines.Add((Signed(v) + Tip(42), v <= 0));
            }
            if (b.Scale != d.Scale && b.Scale > 0f)
            {
                double v = System.Math.Round((d.Scale - (double)b.Scale) / b.Scale * 100.0);
                lines.Add((Signed(v) + Tip(43), v >= 0));
            }
            if (b.ShootSpeed != d.ShootSpeed && b.ShootSpeed > 0f)
            {
                double v = System.Math.Round((d.ShootSpeed - (double)b.ShootSpeed) / b.ShootSpeed * 100.0);
                lines.Add((Signed(v) + Tip(44), v >= 0));
            }
            if (b.Knockback != d.Knockback && b.Knockback > 0f)
            {
                double v = System.Math.Round((d.Knockback - (double)b.Knockback) / b.Knockback * 100.0);
                lines.Add((Signed(v) + Tip(45), v >= 0));
            }
            if (b.PrefixArmorPen != d.PrefixArmorPen)
            {
                int n = d.PrefixArmorPen - b.PrefixArmorPen;
                lines.Add((Format("CommonItemTooltip.PrefixArmorPenetration", n), n >= 0));
            }
            if (b.PrefixTagDamage != d.PrefixTagDamage)
            {
                int n = d.PrefixTagDamage - b.PrefixTagDamage;
                lines.Add((Format("CommonItemTooltip.PrefixTagDamage", n), n >= 0));
            }
            int p = d.Prefix;
            if (p >= 62 && p <= 65) lines.Add(("+" + AccessoryDefense(p) + Tip(25), true));
            if (p == 66) lines.Add(("+20 " + Tip(31), true));
            if (p == 67 || p == 68) lines.Add(("+" + (p == 67 ? 2 : 4) + Tip(5), true));
            if (p >= 69 && p <= 72) lines.Add(("+" + (p - 68) + Tip(39), true));
            if (p >= 73 && p <= 76) lines.Add(("+" + (p - 72) + Tip(46), true));
            if (p >= 77 && p <= 80) lines.Add(("+" + (p - 76) + Tip(47), true));
            return lines;
        }

        static string Format(string key, int n)
        {
            string f = Terraria.TerrariaText.Get(Lang.Culture, key) ?? Terraria.TerrariaText.Get("en-US", key) ?? "{0:+0;-#}";
            try { return string.Format(f, n); }
            catch (System.FormatException) { return (n > 0 ? "+" : "") + n; }
        }

        /// <summary>Dev: every prefix an item may have, with its name.</summary>
        public static List<(int Id, string Name)> DevRollable(ItemDef d)
        {
            var list = new List<(int, string)>();
            var r = Rollable(d);
            if (r != null) foreach (int p in r) if (TryStats(d, p, out _, out _)) list.Add((p, Name(p)));
            return list;
        }
    }
}
