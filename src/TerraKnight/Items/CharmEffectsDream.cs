using HarmonyLib;
using TerraKnight.Effects;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Dream Wielder (charm 30, 护符设计记录.md): the Dream Nail charges in the air as well as on the ground, and neither its
    /// charge nor its slash holds the player still; the game's own quicker charge and its more Essence stay. A faint
    /// lilac shimmer gathers round the player as it charges.
    /// </summary>
    internal static partial class CharmEffects
    {
        public const int DreamWielder = 30;
        static readonly Color DreamColor = new Color(0.85f, 0.75f, 1f);
        public static int DevAirDreams;

        /// <summary>The Dream Nail free (DreamNailItem.HoldStill): Dream Wielder's, not held still while it charges and slashes.</summary>
        public static bool DreamFree => On(DreamWielder);

        static float dreamCharging;

        static void TickDream(HeroController hero)
        {
            if (!On(DreamWielder) || hero == null) { dreamCharging = 0f; return; }
            var fsm = DreamNailItem.DevFsm(hero);
            string st = fsm != null ? fsm.ActiveStateName : null;
            bool charging = st != null && st.Contains("Charge");
            dreamCharging = charging ? Mathf.Min(1f, dreamCharging + Time.deltaTime * 2f) : Mathf.Max(0f, dreamCharging - Time.deltaTime * 4f);
        }

        static void DrawDream(FxBatch fx, Vector2 c, float opacity)
        {
            if (!On(DreamWielder) || dreamCharging <= 0f) return;
            fx.OnTop = true;
            float t = Time.time;
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f - t * 1.4f;
                float r = 30f - 8f * Mathf.Repeat(t * 0.8f + i * 0.2f, 1f);
                fx.Draw(Dot, c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, Glow(DreamColor, 0.45f * dreamCharging * opacity), 0f, 0.2f, overPlayer: true);
            }
            fx.Draw(Dot, c, Glow(DreamColor, 0.18f * dreamCharging * opacity), 0f, 2f, overPlayer: true);
        }

        public static int DevDreamKeptControl;

        /// <summary>
        /// The Dream Nail machine's Take Control (RelinquishControl, what holds the player still through the charge and the
        /// slash): with Dream Wielder, left out; the player moves, jumps and falls as ever while it charges and slashes. Its
        /// Regain Control after is harmless.
        /// </summary>
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.RelinquishControl))]
        static class DreamKeepsControl
        {
            static bool Prefix(HeroController __instance)
            {
                if (!On(DreamWielder) || TerraMod.Instance == null) return true;
                var fsm = DreamNailItem.DevFsm(__instance);
                if (fsm == null || fsm.ActiveStateName != "Take Control") return true;
                DevDreamKeptControl++;
                return false;
            }
        }

        /// <summary>HeroController.CanDreamNail: with Dream Wielder, in the air too (falling or rising), everything else as the game asks.</summary>
        [HarmonyPatch(typeof(HeroController), nameof(HeroController.CanDreamNail))]
        static class DreamInAir
        {
            static readonly AccessTools.FieldRef<HeroController, float> attackTime = AccessTools.FieldRefAccess<HeroController, float>("attack_time");
            static void Postfix(HeroController __instance, ref bool __result)
            {
                if (__result || !On(DreamWielder) || TerraMod.Instance == null || !DreamNailItem.Holding()) return;
                var cs = __instance.cState;
                var gm = GameManager.UnsafeInstance;
                var pd = PlayerData.instance;
                if (gm == null || gm.isPaused || pd == null || !pd.hasDreamNail) return;
                if (cs.dead || __instance.hero_state == GlobalEnums.ActorStates.no_input || cs.dashing || cs.backDashing
                    || cs.attacking && attackTime(__instance) < __instance.ATTACK_RECOVERY_TIME || __instance.controlReqlinquished
                    || cs.hazardDeath || cs.hazardRespawning || cs.recoilFrozen || cs.recoiling || cs.transitioning) return;
                __result = true;
                if (!cs.onGround) DevAirDreams++;
            }
        }
    }
}
