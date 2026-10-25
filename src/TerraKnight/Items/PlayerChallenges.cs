using UnityEngine;
using TerraKnight.World;

namespace TerraKnight.Items
{
    /// <summary>Player-side Pantheon bindings. Never edits an enemy, an item definition or saved equipment.</summary>
    internal static class PlayerChallenges
    {
        internal static BossSequenceController.ChallengeBindings? DevBindings;
        internal static bool? DevResting;
        static bool InSequence => BossSequenceController.IsInSequence
            && BossStats.InGodhome(GameManager.UnsafeInstance?.sceneName);
        internal static BossSequenceController.ChallengeBindings Bindings
        {
            get
            {
                if (DevBindings.HasValue) return DevBindings.Value;
                if (!InSequence) return BossSequenceController.ChallengeBindings.None;
                var b = BossSequenceController.ChallengeBindings.None;
                if (BossSequenceController.BoundNail) b |= BossSequenceController.ChallengeBindings.Nail;
                if (BossSequenceController.BoundShell) b |= BossSequenceController.ChallengeBindings.Shell;
                if (BossSequenceController.BoundCharms) b |= BossSequenceController.ChallengeBindings.Charms;
                if (BossSequenceController.BoundSoul) b |= BossSequenceController.ChallengeBindings.Soul;
                return b;
            }
        }
        static bool Has(BossSequenceController.ChallengeBindings b) => (Bindings & b) != 0;
        internal static bool AccessoriesLocked => Has(BossSequenceController.ChallengeBindings.Charms);
        internal static bool Hollow => GameDifficulty.Current == DifficultyLevel.Hollow;
        internal static float LifeShare => Has(BossSequenceController.ChallengeBindings.Shell) ? .7f : Hollow ? .85f : 1f;
        internal static float DamageShare => Has(BossSequenceController.ChallengeBindings.Nail) ? .85f : 1f;
        internal static int EnvironmentDamage(int maximum) => Mathf.Max(1, maximum / 2 + maximum % 2);
        internal static float HollowReward => Hollow ? 1.4f : 1f;
        internal static bool RecoveryLocked => Has(BossSequenceController.ChallengeBindings.Soul)
            && !(DevResting ?? (PlayerData.instance != null && PlayerData.instance.atBench || GameManager.UnsafeInstance?.sceneName == "GG_Spa"));
        internal static int LifeLimit(int normal) => Mathf.Max(1, Mathf.FloorToInt(normal * LifeShare));
        internal static int Outgoing(int normal) => Mathf.Max(1, Mathf.RoundToInt(normal * DamageShare));
        internal static int PotionHealing(int normal) => Mathf.Max(1, Mathf.FloorToInt(normal * (RecoveryLocked ? .7f : 1f)));
        internal static float PotionCooldown(float normal) => normal * (Hollow ? 1.2f : 1f);

        internal static bool DashComponent(ItemType type) => type == ItemType.ShadeCloak || type == ItemType.CrystalHeart;
        internal static float CompletionShare(BossSequenceController.ChallengeBindings bindings)
        {
            // Three ordinary bindings add 20% each; charms add 40%; all four receive a 50% combination bonus.
            int bits = (int)bindings & 15;
            return 1f + ((bits & 1) != 0 ? .2f : 0f) + ((bits & 2) != 0 ? .2f : 0f)
                + ((bits & 4) != 0 ? .4f : 0f) + ((bits & 8) != 0 ? .2f : 0f) + (bits == 15 ? .5f : 0f);
        }
        internal static int ScaleCount(int n, float factor)
        {
            if (n <= 0 || factor <= 1f) return n;
            double value = n * (double)factor;
            if (value >= int.MaxValue) return int.MaxValue;
            int whole = (int)value;
            return whole + (Random.value < value - whole ? 1 : 0);
        }
        internal static bool Material(ItemType t)
        {
            var d = ItemDb.Get(t);
            return d != null && d.MaxStack > 1 && !d.IsWeapon && !d.IsArmor && !d.IsAccessory && !d.IsMount
                && d.HealLife == 0 && d.HealMana == 0 && !d.LifeCrystal && !d.ManaCrystal
                && d.Boost == PermanentBoost.None && t != ItemType.Geo && t != ItemType.Heart
                && t != ItemType.LunarCraftingStation && Recipes.IsMaterial(t);
        }
    }
}
