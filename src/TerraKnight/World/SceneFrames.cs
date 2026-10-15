using System.Collections.Generic;
using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TerraKnight.World
{
    /// <summary>
    /// A neighbouring scene is moved whole to sit behind its gate, but its FSMs were written for
    /// the scene at its own coordinates: the Howling Cliffs stag lift compares the Knight's height
    /// with fixed numbers and parks itself at a fixed height, enemies patrol between fixed x
    /// values. The position actions those FSMs use are made to work in their scene's own frame:
    /// positions read in world space come out relative to the scene's origin, positions set in
    /// world space go back in shifted by it. A root object's local position is its world
    /// position, so it counts as world space too.
    /// </summary>
    [HarmonyPatch]
    internal static class SceneFrames
    {
        static readonly Dictionary<int, Vector3> offsets = new Dictionary<int, Vector3>();
        static bool hooked;

        /// <summary>Where a moved scene's origin now is. Zero clears it.</summary>
        public static void Set(Scene scene, Vector2 offset)
        {
            if (!hooked)
            {
                hooked = true;
                UnityEngine.SceneManagement.SceneManager.sceneUnloaded += s => offsets.Remove(s.handle);
            }
            if (offset == Vector2.zero) offsets.Remove(scene.handle);
            else offsets[scene.handle] = offset;
        }

        /// <summary>Where a loaded scene's origin now is (zero for one at its own coordinates).</summary>
        public static Vector3 OfScene(Scene scene) =>
            offsets.Count == 0 || !scene.IsValid() ? Vector3.zero : offsets.TryGetValue(scene.handle, out var o) ? o : Vector3.zero;

        public static Vector3 Of(GameObject go)
        {
            if (offsets.Count == 0 || go == null) return Vector3.zero;
            return offsets.TryGetValue(go.scene.handle, out var o) ? o : Vector3.zero;
        }

        /// <summary>The offset of the scene the FSM running an action belongs to.</summary>
        static Vector3 Of(FsmStateAction action) => offsets.Count == 0 ? Vector3.zero : Of(action.Fsm?.GameObject);

        static bool InWorldFrame(GameObject target, Space space) => space == Space.World || target.transform.parent == null;

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GetPosition), "DoGetPosition")]
        static bool GetPositionLocal(GetPosition __instance)
        {
            var off = Of(__instance);
            if (off == Vector3.zero) return true;
            var target = __instance.Fsm.GetOwnerDefaultTarget(__instance.gameObject);
            if (target == null || !InWorldFrame(target, __instance.space)) return true;
            var v = target.transform.position - off;
            __instance.vector.Value = v;
            __instance.x.Value = v.x;
            __instance.y.Value = v.y;
            __instance.z.Value = v.z;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(GetPosition2D), "DoGetPosition")]
        static bool GetPosition2DLocal(GetPosition2D __instance)
        {
            var off = Of(__instance);
            if (off == Vector3.zero) return true;
            var target = __instance.Fsm.GetOwnerDefaultTarget(__instance.gameObject);
            if (target == null || !InWorldFrame(target, __instance.space)) return true;
            var v = target.transform.position - off;
            __instance.vector.Value = v;
            __instance.x.Value = v.x;
            __instance.y.Value = v.y;
            return false;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SetPosition), "DoSetPosition")]
        static bool SetPositionLocal(SetPosition __instance)
        {
            if (!Net.BattleIsolation.Allows(__instance)) return false;
            var off = Of(__instance);
            if (off == Vector3.zero) return true;
            var target = __instance.Fsm.GetOwnerDefaultTarget(__instance.gameObject);
            if (target == null || !InWorldFrame(target, __instance.space)) return true;
            var t = target.transform;
            var v = !__instance.vector.IsNone ? __instance.vector.Value : t.position - off;
            if (!__instance.x.IsNone) v.x = __instance.x.Value;
            if (!__instance.y.IsNone) v.y = __instance.y.Value;
            if (!__instance.z.IsNone) v.z = __instance.z.Value;
            t.position = v + off;
            return false;
        }

        // A move to a fixed point (no target object) is to a point in the scene's own frame.

        [HarmonyPrefix]
        [HarmonyPatch(typeof(iTweenMoveTo), "DoiTween")]
        static bool MoveToLocal(iTweenMoveTo __instance, out Vector3 __state)
        {
            __state = Vector3.zero;
            var target = __instance.Fsm.GetOwnerDefaultTarget(__instance.gameObject);
            if (!Net.BattleIsolation.AllowsTarget(__instance.Fsm, target)) return false;
            var off = Of(__instance);
            if (off == Vector3.zero || __instance.vectorPosition.IsNone) return true;
            if (!__instance.transformPosition.IsNone && __instance.transformPosition.Value != null) return true;
            if (target == null || !InWorldFrame(target, __instance.space)) return true;
            __state = off;
            __instance.vectorPosition.Value += off;
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(iTweenMoveTo), "DoiTween")]
        static void MoveToRestore(iTweenMoveTo __instance, Vector3 __state)
        {
            if (__state != Vector3.zero) __instance.vectorPosition.Value -= __state;
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(MoveTowards), nameof(MoveTowards.UpdateTargetPos))]
        static void MoveTowardsLocal(MoveTowards __instance, bool __result, GameObject ___go, GameObject ___goTarget,
            ref Vector3 ___targetPos, ref Vector3 ___targetPosWithVertical)
        {
            if (!__result || ___goTarget != null) return;
            var off = Of(__instance);
            if (off == Vector3.zero) return;
            ___targetPosWithVertical += off;
            ___targetPos += off;
            if (__instance.ignoreVertical.Value) ___targetPos.y = ___go.transform.position.y;
        }

        // Objects made at a fixed point (no spawn point) appear at that point of the scene.

        [HarmonyPrefix]
        [HarmonyPatch(typeof(CreateObject), nameof(CreateObject.OnEnter))]
        static void CreateLocal(CreateObject __instance, out Vector3 __state) =>
            __state = ShiftSpawn(__instance, __instance.spawnPoint, __instance.position);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(CreateObject), nameof(CreateObject.OnEnter))]
        static void CreateRestore(CreateObject __instance, Vector3 __state)
        {
            if (__state != Vector3.zero) __instance.position.Value -= __state;
        }

        [HarmonyPrefix]
        [HarmonyPatch(typeof(SpawnObjectFromGlobalPool), nameof(SpawnObjectFromGlobalPool.OnEnter))]
        static void SpawnLocal(SpawnObjectFromGlobalPool __instance, out Vector3 __state) =>
            __state = ShiftSpawn(__instance, __instance.spawnPoint, __instance.position);

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SpawnObjectFromGlobalPool), nameof(SpawnObjectFromGlobalPool.OnEnter))]
        static void SpawnRestore(SpawnObjectFromGlobalPool __instance, Vector3 __state)
        {
            if (__state != Vector3.zero) __instance.position.Value -= __state;
        }

        static Vector3 ShiftSpawn(FsmStateAction action, FsmGameObject spawnPoint, FsmVector3 position)
        {
            if (spawnPoint.Value != null || position.IsNone) return Vector3.zero;
            var off = Of(action);
            if (off == Vector3.zero) return Vector3.zero;
            position.Value += off;
            return off;
        }
    }
}
