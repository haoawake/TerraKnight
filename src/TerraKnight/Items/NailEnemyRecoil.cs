using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The nail never throws the Knight back off enemies (the user's): a side or up slash landing on an enemy, on its
    /// shield (HealthManager.Invincible) or on a hard shell of its own (a TinkEffect on the enemy: Uumuu's) leaves him where
    /// he is. What isn't an enemy still pushes back as in the game: the hard things of the room (TinkEffect: spikes,
    /// crystal), geo rocks, levers, the bounce shrooms; and the down slash still bounces off enemies (the pogo).
    /// </summary>
    [HarmonyPatch]
    internal static class NailEnemyRecoil
    {
        static readonly AccessTools.FieldRef<NailSlash, float> slashAngle = AccessTools.FieldRefAccess<NailSlash, float>("slashAngle");
        static bool blocking;
        public static int DevStopped;

        /// <summary>NailSlash.OnTriggerEnter2D (and Stay, which calls it): a side or up slash's recoil off an enemy's body skipped.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(NailSlash), "OnTriggerEnter2D")]
        static bool OffEnemy(NailSlash __instance, Collider2D otherCollider)
        {
            if (otherCollider == null || __instance == null || slashAngle(__instance) == 270f) return true;
            var go = otherCollider.gameObject;
            if (go.layer != 11 || go.GetComponent<BounceShroom>() != null || go.GetComponentInParent<HealthManager>() == null) return true;
            DevStopped++;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HealthManager), "Invincible")]
        static void Blocking() => blocking = true;

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(HealthManager), "Invincible")]
        static void Blocked() => blocking = false;

        /// <summary>A hard thing struck: an enemy's own shell blocks like its shield (no recoil), the room's pushes back.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(TinkEffect), "OnTriggerEnter2D")]
        static void Tink(TinkEffect __instance) => blocking = __instance != null && __instance.GetComponentInParent<HealthManager>() != null;

        [HarmonyFinalizer]
        [HarmonyPatch(typeof(TinkEffect), "OnTriggerEnter2D")]
        static void Tinked() => blocking = false;

        /// <summary>An enemy's shield or shell struck by the nail: its clank and hit-stop as ever, no recoil.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.RecoilLeft))]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.RecoilRight))]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.RecoilDown))]
        static bool OffShield()
        {
            if (!blocking) return true;
            DevStopped++;
            return false;
        }
    }
}
