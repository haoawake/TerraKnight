using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using HutongGames.PlayMaker;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// The Radiance's end (the Black Egg's dream, Godhome's Absolute Radiance). Her script ends the fight itself, never by her
    /// life running out: at the climb's top (Control's "Ascend Cast", Godhome's "Death Ready") the nail's touch sends FINAL
    /// HIT, then Final Impact, the Knight's void and the ending. Terraria's weapons emptied her life on the climb (or before
    /// it) and left her beaming on at a fight with no end (the user's, 10-25). So:
    /// - her life keeps its last point (her own phases still go by it, a step a hit);
    /// - where her script waits for that touch, a hit of the player's from near her (<see cref="Reach"/>) is it, a shot as
    ///   much as a swing (a swing's stand-in touches her as the nail would anyway);
    /// - a book challenge's fight (BossArena) ends at Final Impact (her own death clip and flash): instead of the Knight's
    ///   void and the ending, the dream's own waking (the white of its fall catcher), back where the challenge was asked for.
    /// </summary>
    [HarmonyPatch]
    internal static class RadianceEnding
    {
        /// <summary>How near her the player strikes from to end the climb (units: her body is about 4 across).</summary>
        const float Reach = 8f;

        public static int DevHeld, DevTouches, DevChallengeWakes, DevCredited;
        public static string DevLast = "";

        static bool Is(HealthManager hm) => hm != null && UI.EnemyHover.RecordOf(hm) == "FinalBoss";

        static readonly Dictionary<FsmState, bool> awaits = new Dictionary<FsmState, bool>();

        /// <summary>A state that waits for the nail's touch to send FINAL HIT (a trigger tagged "Nail Attack").</summary>
        static bool AwaitsTouch(FsmState s)
        {
            if (s == null) return false;
            if (awaits.TryGetValue(s, out bool known)) return known;
            bool final = false, touch = false;
            foreach (var t in s.Transitions) if (t.EventName == "FINAL HIT") final = true;
            foreach (var a in s.Actions)
                if (a is HutongGames.PlayMaker.Actions.Trigger2dEvent te && te.collideTag != null && te.collideTag.Value == "Nail Attack" && te.sendEvent != null && te.sendEvent.Name == "FINAL HIT")
                    touch = true;
            if (awaits.Count > 256) awaits.Clear();
            return awaits[s] = final && touch;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(HealthManager), "TakeDamage")]
        static void Hit(HealthManager __instance, ref HitInstance hitInstance)
        {
            if (!Is(__instance)) return;
            var control = PlayMakerFSM.FindFsmOnGameObject(__instance.gameObject, "Control");
            var hero = HeroController.SilentInstance;
            if (control != null && control.enabled && hero != null && AwaitsTouch(control.Fsm.ActiveState)
                && Vector2.Distance(hero.transform.position, __instance.transform.position) <= Reach)
            {
                DevTouches++;
                DevLast = $"{__instance.gameObject.scene.name} {control.ActiveStateName}：玩家在她身边打中，算作骨钉的一击";
                Plugin.Log.LogInfo($"[辐光] {DevLast}");
                control.SendEvent("FINAL HIT");
            }
            // HealthManager.TakeDamage takes off the damage times the hit's multiplier.
            float mult = Mathf.Max(0.001f, hitInstance.Multiplier);
            if (Mathf.RoundToInt(hitInstance.DamageDealt * mult) < __instance.hp) return;
            hitInstance.DamageDealt = Mathf.Max(0, Mathf.FloorToInt((__instance.hp - 1) / mult));
            DevHeld++;
        }

        /// <summary>
        /// Final Impact: her win written down when the flag it sets was set already (a save that beat her before, whose
        /// journal kill the game doesn't write twice).
        /// </summary>
        [HarmonyPostfix]
        [HarmonyPatch(typeof(FsmState), nameof(FsmState.OnEnter))]
        static void Entered(FsmState __instance)
        {
            if (__instance.Name != "Final Impact") return;
            var hm = Owner(__instance);
            if (hm == null || Bestiary.Credited(hm)) return;
            Bestiary.Credit(hm, whole: true);
            if (!Bestiary.Credited(hm)) return;
            EnemyStats.MarkDefeated(hm);
            DevCredited++;
            Plugin.Log.LogInfo($"[辐光] {hm.gameObject.scene.name}：最后一击，记入图鉴、掉落");
        }

        /// <summary>A book challenge's Radiance after Final Impact: not the Knight's void (and the ending after it), the dream's waking.</summary>
        [HarmonyPrefix]
        [HarmonyPatch(typeof(FsmState), nameof(FsmState.OnEnter))]
        static bool Entering(FsmState __instance)
        {
            if (__instance.Name != "Knight Pull" || BossArena.VisitKey != "FinalBoss") return true;
            var hm = Owner(__instance);
            if (hm == null || !(__instance.Fsm.Owner is PlayMakerFSM control)) return true;
            control.enabled = false;
            DevChallengeWakes++;
            Plugin.Log.LogInfo($"[辐光] 图鉴挑战：{hm.gameObject.scene.name} 最后一击之后不进虚空和结局，梦醒回到出发的地方");
            TerraMod.Instance?.StartCoroutine(Wake());
            return false;
        }

        /// <summary>Her "Control" script's state's boss (the Radiance's, else null).</summary>
        static HealthManager Owner(FsmState s)
        {
            var fsm = s.Fsm;
            if (fsm == null || fsm.Name != "Control" || fsm.GameObject == null) return null;
            var hm = fsm.GameObject.GetComponent<HealthManager>();
            return Is(hm) ? hm : null;
        }

        /// <summary>The dream's own waking (Dream Fall Catcher's Fade Out and New Scene), a moment after her death clip.</summary>
        static IEnumerator Wake()
        {
            yield return new WaitForSeconds(1.2f);
            var gm = GameManager.UnsafeInstance;
            var hero = HeroController.SilentInstance;
            var pd = PlayerData.instance;
            if (gm == null || hero == null || pd == null || gm.IsInSceneTransition) yield break;
            hero.RelinquishControl();
            hero.StopAnimationControl();
            foreach (var f in Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None))
                if (f.FsmName == "Blanker Control" && f.gameObject.name.StartsWith("Blanker White"))
                {
                    var time = f.FsmVariables.FindFsmFloat("Fade Time");
                    if (time != null) time.Value = 0.9f;
                    f.SendEvent("FADE IN");
                }
            yield return new WaitForSeconds(1f);
            gm = GameManager.UnsafeInstance;
            hero = HeroController.SilentInstance;
            if (gm == null || hero == null || gm.IsInSceneTransition) yield break;
            // Final Impact's rumble, left on for the void that isn't coming.
            var shake = GameCameras.instance != null ? GameCameras.instance.cameraShakeFSM : null;
            var rumble = shake != null ? shake.FsmVariables.FindFsmBool("RumblingMed") : null;
            if (rumble != null) rumble.Value = false;
            gm.TimePasses();
            gm.ResetSemiPersistentItems();
            var fade = GameCameras.instance != null ? GameCameras.instance.cameraFadeFSM : null;
            var noFade = fade != null ? fade.FsmVariables.FindFsmBool("No Fade") : null;
            if (noFade != null) noFade.Value = true;
            foreach (var f in hero.GetComponents<PlayMakerFSM>())
                if (f.FsmName == "Dream Return")
                {
                    var returning = f.FsmVariables.FindFsmBool("Dream Returning");
                    if (returning != null) returning.Value = true;
                }
            hero.EnterWithoutInput(true);
            gm.BeginSceneTransition(new GameManager.SceneLoadInfo
            {
                SceneName = pd.dreamReturnScene, EntryGateName = "door_dreamReturn", EntryDelay = 0f,
                Visualization = GameManager.SceneLoadVisualizations.Dream,
                PreventCameraFadeOut = true, WaitForSceneTransitionCameraFade = false,
            });
        }
    }
}
