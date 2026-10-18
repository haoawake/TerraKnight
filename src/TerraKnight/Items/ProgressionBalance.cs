using System;
using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>Acquisition tiers and weapon budgets. Equipment retains its original attributes.</summary>
    internal static partial class ProgressionBalance
    {
        internal sealed class ArmorPath
        {
            public readonly int Stage;
            public readonly Kit Class;
            public readonly ItemType[] Items;
            public ArmorPath(int stage, Kit cls, ItemType[] items) { Stage = stage; Class = cls; Items = items; }
        }
        internal sealed class BossReward
        {
            public readonly int Stage;
            public readonly ItemType[] Weapons;
            public readonly ItemType Accessory;
            public BossReward(int stage, ItemType[] weapons, ItemType accessory) { Stage = stage; Weapons = weapons; Accessory = accessory; }
        }
        static readonly float[] HitBudget = { 17, 29, 46, 69, 102, 151 };
        static readonly int[] Minions = { 2,3,3,4,4,5 };

        internal static int ClassIndex(DamageClass c) => c == DamageClass.Melee ? 0 : c == DamageClass.Ranged ? 1 : c == DamageClass.Magic ? 2 : c == DamageClass.Summon ? 3 : -1;
        internal static int ClassIndex(Kit c) => c == Kit.Melee ? 0 : c == Kit.Ranged ? 1 : c == Kit.Magic ? 2 : c == Kit.Summon ? 3 : -1;
        internal static bool EliteReward(string key) => key == "BigBuzzer" || key == "MageKnight" || key == "PaleLurker";
        internal static bool FormalBoss(string key) => key != "Zote" && !EliteReward(key) && BossRewards.ContainsKey(key);

        internal static int AbilityStage(Func<string, bool> has)
        {
            int stage = 0;
            foreach (var flag in new[] {"hasDash","hasWalljump","hasSuperDash","hasDoubleJump","hasShadowDash"})
            { if (!has(flag)) break; stage++; }
            return stage;
        }
        internal static bool EntryClear
        {
            get
            {
                if (Bestiary.KillsOf("InfectedKnight") > 0) return true;
                foreach (var b in BossRewards)
                    if (b.Value.Stage == 4 && FormalBoss(b.Key) && Bestiary.KillsOf(b.Key) > 0) return true;
                return false;
            }
        }
        internal static bool FinalClear => Bestiary.KillsOf("FinalBoss") > 0;
        // Complete recipe families unlock together with their ingredients.
        internal static int ExtraGate(ItemType t) => 0;
        internal static bool Unlocked(ItemType t, int stage, bool entry, bool final)
        {
            var d = ItemDb.Get(t);
            return d != null && d.Stage <= stage && (ExtraGate(t) == 0 || (ExtraGate(t) == 1 ? entry : final));
        }
        internal static bool Unlocked(ItemType t) => Shop.Free || Unlocked(t, Shop.UnlockedStage(), EntryClear, FinalClear);

        internal static void ApplyItems(ItemDef[] items)
        {
            foreach (var d in items)
            {
                if (d != null && d.IsAccessory) OriginalAccessories[d.Type] = (d.Bonus, d.Defense, d.Tooltip, d.Damage);
                if (d != null && d.IsArmor) OriginalArmor[d.Type] = (d.Bonus, d.Defense, d.Tooltip, d.Set, d.SetBonus);
            }
            var tiers = new Dictionary<ItemType, int>(ResourceStages);
            for (int s = 0; s < Core.Length; s++)
                foreach (var cls in Core[s]) foreach (var t in cls) tiers[t] = s;
            for (int s = 0; s < AccessoryPaths.Length; s++)
                foreach (var cls in AccessoryPaths[s]) foreach (var t in cls)
                    if (!tiers.TryGetValue(t, out int old) || s < old) tiers[t] = s;
            var armourTiers = new Dictionary<ItemType, int>();
            foreach (var p in ArmorPaths) foreach (var t in p.Items)
                if (!armourTiers.TryGetValue(t, out int old) || p.Stage < old) armourTiers[t] = p.Stage;
            foreach (var p in armourTiers) tiers[p.Key] = p.Value;
            foreach(var pair in BossSignatures)
                if(FormalBoss(pair.Key)) {
                    int target=BossRewards[pair.Key].Stage;
                    if(!tiers.TryGetValue(pair.Value,out int old) || target<old)tiers[pair.Value]=target;
                }
            foreach(var t in EarlyExplorationItems)tiers[t]=0;
            tiers[ItemType.HoneyComb]=1;
            tiers[ItemType.PygmyNecklace] = 0;
            tiers[ItemType.IronBow] = tiers[ItemType.FlintlockPistol] = 0;
            tiers[ItemType.FeralClaws] = tiers[ItemType.PoisonBarb] = tiers[ItemType.BandofStarpower] = 0;
            tiers[ItemType.Gel] = tiers[ItemType.Feather] = tiers[ItemType.FallenStar] = tiers[ItemType.Amethyst] = 0;
            tiers[ItemType.WarriorEmblem] = tiers[ItemType.RangerEmblem] = tiers[ItemType.SorcererEmblem] = tiers[ItemType.SummonerEmblem] = 3;
            tiers[ItemType.MagicQuiver] = tiers[ItemType.MagicCuffs] = 2;
            tiers[ItemType.Hellforge] = 2;
            tiers[ItemType.MythrilAnvil] = tiers[ItemType.OrichalcumAnvil] = 3;
            tiers[ItemType.AdamantiteForge] = 4;
            tiers[ItemType.Autohammer] = 5;
            tiers[ItemType.ZapinatorGray] = 1;
            tiers[ItemType.LunarCraftingStation] = 5;
            tiers[ItemType.TinkerersWorkshop] = 1;
            tiers[ItemType.WoodenBoomerang] = 0;
            tiers[ItemType.TitanGlove] = 2;
            // Actual world/merchant sources, rather than the old generic material tier.
            tiers[ItemType.MetalDetector] = 2;
            tiers[ItemType.ArgonMoss] = 2;
            tiers[ItemType.VioletMoss] = 3;
            tiers[ItemType.RainbowMoss] = 4;
            tiers[ItemType.Honeyfin] = 4;
            // Early flying insects supply wax for the stage-1 Hornet Staff; the Hive
            // remains the large local source for its boss and late bee equipment.
            tiers[ItemType.BeeWax] = 1;
            tiers[ItemType.FishermansGuide] = tiers[ItemType.WeatherRadio] = tiers[ItemType.Sextant] = 2;
            tiers[ItemType.AnglerHat] = tiers[ItemType.AnglerVest] = tiers[ItemType.AnglerPants] = 2;
            foreach (var d in items)
            {
                if (d == null || d.Type == ItemType.None) continue;
                if (tiers.TryGetValue(d.Type, out int stage)) d.Stage = stage;
                if(ShopOnlyWeapons.Contains(d.Type))d.Source=ItemSource.Shop;
                if (d.Stage < 0 || d.Stage > 5) continue;
                // Stage changes acquisition only. Preserve all piece stats and descriptions.
            }
            // Keep the existing regional acquisition tier without rewriting Bee armour bonuses.
            foreach (var t in new[] {ItemType.BeeHeadgear,ItemType.BeeBreastplate,ItemType.BeeGreaves})
                items[(int)t].Stage = 4;
            foreach (var t in new[] {ItemType.WoodenSword,ItemType.WoodenBow,ItemType.WandofSparking,ItemType.FinchStaff})
            { items[(int)t].Value = 3000; items[(int)t].Price = 30; }
            foreach (var t in new[] {ItemType.WoodenArrow,ItemType.Seed,ItemType.MusketBall})
            { items[(int)t].Stage = 0; items[(int)t].Value = 5; }
            items[(int)ItemType.LesserManaPotion].Value = 100;
        }

        internal static readonly Dictionary<ItemType, (Bonuses Bonus, int Defense, string Tooltip, int Damage)> OriginalAccessories
            = new Dictionary<ItemType, (Bonuses, int, string, int)>();

        internal static readonly Dictionary<ItemType, (Bonuses Bonus, int Defense, string Tooltip, ArmorSet Set, ArmorSet SetBonus)> OriginalArmor
            = new Dictionary<ItemType, (Bonuses, int, string, ArmorSet, ArmorSet)>();

        internal static int TagDamage(WhipKind whip,int original)
        {
            if(original==0) return 0;
            foreach(var row in Core) foreach(var cls in row) foreach(var t in cls)
            {
                var d=ItemDb.Get(t);
                if(d.Whip==whip) return Math.Min(original,new[] {2,3,4,6,8,12}[d.Stage]);
            }
            return Math.Min(original,12);
        }
        internal static int WhipProcDamage => new[] {3,5,8,12,18,26}[Mathf.Clamp(ItemDb.Get(ItemType.Durendal).Stage,0,5)];
        internal static ItemType[] FunctionPool(int stage)
        {
            var paths = AccessoryPaths[Mathf.Clamp(stage,0,5)];
            return new[] {paths[0][0],paths[1][0],paths[2][0],paths[3][0]};
        }
        internal static IEnumerable<ItemType> ArmorFor(int stage, int cls)
        {
            foreach (var p in ArmorPaths)
                if (p.Stage == stage && ClassIndex(p.Class) == cls)
                    foreach (var t in p.Items) yield return t;
        }
    }
}
