using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Player
{
    /// <summary>
    /// Silences the Knight's own movement effects that Terraria doesn't have: jump and wall jump
    /// sounds and puffs, the wall slide scrape and dust, the hurt grunt (Terraria's hit sound plays
    /// instead), the falling whistle and the hard landing. Footsteps stay.
    /// </summary>
    [HarmonyPatch]
    internal static class KnightEffects
    {
        static bool Active => TerraMovement.Enabled && TerraMod.Instance != null && World.TerrainSystem.Current != null;

        static readonly AccessTools.FieldRef<HeroController, bool> playedMantisClawClip = AccessTools.FieldRefAccess<HeroController, bool>("playedMantisClawClip");
        static readonly AccessTools.FieldRef<HeroController, bool> playingWallslideClip = AccessTools.FieldRefAccess<HeroController, bool>("playingWallslideClip");
        static readonly AccessTools.FieldRef<HeroController, int> jumpQueueSteps = AccessTools.FieldRefAccess<HeroController, int>("jumpQueueSteps");
        static readonly AccessTools.FieldRef<HeroController, int> jumpedSteps = AccessTools.FieldRefAccess<HeroController, int>("jumped_steps");
        static readonly AccessTools.FieldRef<HeroController, bool> doubleJumpQueuing = AccessTools.FieldRefAccess<HeroController, bool>("doubleJumpQueuing");
        static readonly System.Action<HeroController> resetLook =
            AccessTools.MethodDelegate<System.Action<HeroController>>(AccessTools.Method(typeof(HeroController), "ResetLook"));

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroAudioController), nameof(HeroAudioController.PlaySound))]
        static bool PlaySound(GlobalEnums.HeroSounds soundEffect)
        {
            if (!Active) return true;
            switch (soundEffect)
            {
                case GlobalEnums.HeroSounds.JUMP:
                case GlobalEnums.HeroSounds.WALLJUMP:
                case GlobalEnums.HeroSounds.WALLSLIDE:
                case GlobalEnums.HeroSounds.DASH:
                case GlobalEnums.HeroSounds.TAKE_HIT:
                case GlobalEnums.HeroSounds.FALLING:
                case GlobalEnums.HeroSounds.HARD_LANDING:
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>HeroJump without the dust puff it spawns (and the sound, already skipped above).</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroController), "HeroJump")]
        static bool HeroJump(HeroController __instance)
        {
            if (!Active) return true;
            resetLook(__instance);
            __instance.cState.recoiling = false;
            __instance.cState.jumping = true;
            jumpQueueSteps(__instance) = 0;
            jumpedSteps(__instance) = 0;
            doubleJumpQueuing(__instance) = false;
            return false;
        }

        /// <summary>
        /// The wall slide's claw click and scrape loop: mark them as already playing so the hero
        /// never starts them; its dust emitter is switched back off (Terraria's dust replaces it).
        /// No hard landings either.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(HeroController), "Update")]
        static void BeforeUpdate(HeroController __instance)
        {
            if (!Active) return;
            if (__instance.cState.wallSliding)
            {
                playedMantisClawClip(__instance) = true;
                playingWallslideClip(__instance) = true;
            }
            __instance.cState.willHardLand = false;
            var dust = __instance.wallslideDustPrefab;
            if (dust != null)
            {
                var emission = dust.emission;
                if (emission.enabled) emission.enabled = false;
            }
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), "FallCheck")]
        static void AfterFallCheck(HeroController __instance)
        {
            if (Active) __instance.cState.willHardLand = false;
        }

        /// <summary>
        /// The game stops time for a moment whenever the hero is hit, an enemy dies or a lever is
        /// pulled (FSMs call the same method). Terraria never does; to a Terraria player it's a hitch.
        /// </summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.FreezeMoment), typeof(int))]
        static bool FreezeMomentByType() => !Active;

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.FreezeMoment), typeof(float), typeof(float), typeof(float), typeof(float))]
        static void FreezeMomentTimed(ref System.Collections.IEnumerator __result)
        {
            if (Active) __result = NoFreeze();
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(GameManager), nameof(GameManager.FreezeMoment), typeof(float), typeof(float), typeof(float), typeof(bool))]
        static void FreezeMomentTimedGc(ref System.Collections.IEnumerator __result)
        {
            if (Active) __result = NoFreeze();
        }

        static System.Collections.IEnumerator NoFreeze()
        {
            yield break;
        }
    }
}
