using HarmonyLib;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// Hallownest's falling traps (StalactiteControl: a spike of rock that drops when the player walks
    /// under it) hit as Terraria's boulder does: the Boulder trap's projectile (99) is made with 70 damage
    /// (WorldGen.KillTile, tile 138), and a hostile projectile's hit on a player is its damage times the
    /// world's HostileProjectileDamageMultiplier (Classic 1, Expert 2, Master 3) times 2 (Projectile.Damage),
    /// then the player's defense (TerraStats.TakeDirect). The Dead Man's Sweater halves a trap's hit, to at
    /// most 300. Hard, between Normal and Expert, takes 1.5.
    /// </summary>
    [HarmonyPatch]
    internal static class Stalactites
    {
        /// <summary>The Boulder's damage as made (WorldGen.cs:49180, ProjectileID 99).</summary>
        public const int BoulderDamage = 70;

        static bool inHit;

        /// <summary>HostileProjectileDamageMultiplier at the save's difficulty (Normal is Terraria's Classic).</summary>
        static float HostileScale(DifficultyLevel d) => d switch
        {
            DifficultyLevel.Normal => 1f,
            DifficultyLevel.Hard => 1.5f,
            DifficultyLevel.Expert => 2f,
            _ => 3f,
        };

        /// <summary>A boulder's hit on the player before defense, at the save's difficulty.</summary>
        public static int Damage()
        {
            int damage = Mathf.RoundToInt(BoulderDamage * HostileScale(GameDifficulty.Current)) * 2;
            if (Items.Equipment.Current.DeadMansSweater) damage = Mathf.Min(damage / 2, 300);
            return damage;
        }

        public static int DevHits;

        public static bool IsStalactite(GameObject go) => go != null && go.GetComponentInParent<StalactiteControl>() != null;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.TakeDamage))]
        [HarmonyPriority(Priority.High)]
        static bool BeforeTakeDamage(HeroController __instance, GameObject go, int damageAmount)
        {
            if (inHit || damageAmount <= 0 || TerraMod.Instance == null || !IsStalactite(go)) return true;
            inHit = true;
            try
            {
                DevHits++;
                int dir = __instance.transform.position.x >= go.transform.position.x ? 1 : -1;
                Player.PlayerHooks.HurtPlayer(__instance, Damage(), dir, go);
            }
            finally
            {
                inHit = false;
            }
            return false;
        }
    }
}
