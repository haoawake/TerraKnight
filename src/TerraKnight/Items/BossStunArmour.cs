using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>Native damage paths must respect the same armour threshold as Terraria and network hits.</summary>
    [HarmonyPatch(typeof(HealthManager), "TakeDamage")]
    internal static class BossStunArmour
    {
        internal static int DevHeld;

        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        static void BeforeHit(HealthManager __instance, HitInstance hitInstance, bool __runOriginal, ref PlayMakerFSM ___stunControlFSM, out PlayMakerFSM __state)
        {
            __state = null;
            if (!__runOriginal || TerraMod.Instance == null || ___stunControlFSM == null) return;
            var info = EnemyStats.Of(__instance);
            int damage = Mathf.Max(0, Mathf.RoundToInt(hitInstance.DamageDealt * hitInstance.Multiplier));
            if (!BossBalance.SuperArmour(info, damage)) return;
            __state = ___stunControlFSM;
            ___stunControlFSM = null;
            DevHeld++;
        }

        [HarmonyFinalizer]
        static void RestoreStun(HealthManager __instance, PlayMakerFSM __state, ref PlayMakerFSM ___stunControlFSM)
        {
            if (__instance != null && __state != null) ___stunControlFSM = __state;
        }
    }
}
