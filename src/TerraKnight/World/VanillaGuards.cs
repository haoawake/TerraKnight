using System.Collections.Generic;
using HarmonyLib;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// A vanilla FSM action that keeps a component from its state's start and sets it every frame
    /// after it is gone: SetParticleEmissionSpeed (everyFrame) whose particle system was destroyed
    /// before its FSM (a room let go mid-effect). Nothing is left to set: the frame's set is skipped
    /// instead of an exception each time.
    /// </summary>
    [HarmonyPatch]
    internal static class VanillaGuards
    {
        static readonly AccessTools.FieldRef<SetParticleEmissionSpeed, ParticleSystem> emitter =
            AccessTools.FieldRefAccess<SetParticleEmissionSpeed, ParticleSystem>("emitter");
        static readonly HashSet<string> told = new HashSet<string>();
        public static int DevSkipped;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SetParticleEmissionSpeed), "DoSetEmitSpeed")]
        static bool EmissionSpeed(SetParticleEmissionSpeed __instance)
        {
            if (emitter(__instance) != null) return true;
            DevSkipped++;
            var fsm = __instance.Fsm;
            string who = fsm != null ? $"{(fsm.GameObject != null ? fsm.GameObject.scene.name + "/" + fsm.GameObject.name : "?")} 的 {fsm.Name}:{__instance.State?.Name}" : "?";
            if (told.Count < 20 && told.Add(who)) Plugin.Log.LogInfo($"[原版] {who} 要设的粒子已经没了：跳过");
            return false;
        }
    }
}
