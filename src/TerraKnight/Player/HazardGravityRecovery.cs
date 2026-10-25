using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Player
{
    /// <summary>A hazard may interrupt a zero-gravity dash after the native gravity cache was cleared.
    /// Remember the pre-hit gravity, then restore only after that specific hazard has returned control.</summary>
    [HarmonyPatch]
    internal static class HazardGravityRecovery
    {
        static HeroController owner;
        static bool pending;
        static float gravity;
        public static int DevRepairs;

        [HarmonyPrefix, HarmonyPriority(Priority.First)]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.TakeDamage))]
        static void BeforeHazard(HeroController __instance, int hazardType, Rigidbody2D ___rb2d, out float __state)
        {
            __state = float.NaN;
            if (hazardType <= 1 || !TerraMovement.Enabled || TerraMod.Instance == null || World.TerrainSystem.Current == null || ___rb2d == null) return;
            __state = ___rb2d.gravityScale;
            if (Mathf.Abs(__state) <= .01f)
                __state = (GravityGlobe.Flipped ? -1f : 1f) * Mathf.Max(.01f, __instance.DEFAULT_GRAVITY);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.TakeDamage))]
        static void AfterHazard(HeroController __instance, float __state)
        {
            if (float.IsNaN(__state) || !(__instance.cState.hazardDeath || __instance.cState.hazardRespawning)) return;
            if (!pending || owner != __instance) gravity = __state;
            owner = __instance;
            pending = true;
        }

        [HarmonyPostfix, HarmonyPriority(Priority.Last)]
        [HarmonyPatch(typeof(HeroController), "FixedUpdate")]
        static void AfterPhysics(HeroController __instance, Rigidbody2D ___rb2d)
        {
            if (!pending || __instance != HeroController.SilentInstance) return;
            if (owner != __instance || __instance.cState.dead) { pending = false; owner = null; return; }
            if (World.TerrainSystem.Current == null || !GameHooks.IsGameplay(GameManager.UnsafeInstance)) return;
            var cs = __instance.cState;
            if (cs.hazardDeath || cs.hazardRespawning || cs.transitioning || cs.recoiling || cs.recoilFrozen
                || !__instance.acceptingInput || __instance.controlReqlinquished || CutsceneAttack.KnightHidden()) return;
            if (___rb2d == null || !___rb2d.simulated || ___rb2d.bodyType != RigidbodyType2D.Dynamic
                || cs.superDashing || cs.shadowDashing || TerraMovement.IsDashing || TerraMovement.DownDashing || TerraMovement.OnRope
                || Mounts.OwnMovement || GrapplingHook.IsLatched || AcidLiquid.Swimming(__instance) || Items.CharmEffects.HidePlayer) return;
            if (Mathf.Abs(___rb2d.gravityScale) <= .01f)
            {
                ___rb2d.gravityScale = (GravityGlobe.Flipped ? -1f : 1f) * Mathf.Abs(gravity);
                DevRepairs++;
                Plugin.Log.LogInfo($"[移动] 地形复生交还重力 {gravity:F2}");
            }
            pending = false;
            owner = null;
        }
    }
}
