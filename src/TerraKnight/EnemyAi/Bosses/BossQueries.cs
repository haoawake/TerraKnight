using HarmonyLib;
using HutongGames.PlayMaker;
using HutongGames.PlayMaker.Actions;
using UnityEngine;

namespace TerraKnight.EnemyAi.Bosses
{
    /// <summary>
    /// What the scripts of a boss fight (the new AI's: BossAi) look at to find a wall, a floor or a ceiling (HK's
    /// CheckCollisionSide, RayCast2d...) sees only the room's own ground: a block the player built is not a wall to turn
    /// at, nor a floor to land on. While one of those runs, a ray that would stop on the player's blocks goes on to the
    /// first thing past them.
    /// </summary>
    internal static class BossQueries
    {
        /// <summary>A boss fight's script is asking now.</summary>
        static int asking;
        public static int DevFiltered;
        static bool installed;

        /// <summary>
        /// The patches, the first time a boss of the new AI starts (one by one: one that won't take is left out, the
        /// rest of the mod untouched).
        /// </summary>
        internal static void Install()
        {
            if (installed) return;
            installed = true;
            var h = new Harmony(Plugin.Guid + ".bossqueries");
            int ok = 0;
            void Try(System.Reflection.MethodBase target, string prefix, string finalizer, string postfix)
            {
                try
                {
                    if (target == null) throw new System.MissingMethodException("target");
                    h.Patch(target,
                        prefix: prefix != null ? new HarmonyMethod(AccessTools.Method(typeof(BossQueries), prefix)) : null,
                        postfix: postfix != null ? new HarmonyMethod(AccessTools.Method(typeof(BossQueries), postfix)) : null,
                        finalizer: finalizer != null ? new HarmonyMethod(AccessTools.Method(typeof(BossQueries), finalizer)) : null);
                    ok++;
                }
                catch (System.Exception e) { Plugin.Log.LogWarning($"[首领AI] 射线补丁 {target?.Name} 没装上：{e.Message}"); }
            }
            Try(AccessTools.Method(typeof(CheckCollisionSide), "CheckTouching"), nameof(Side), nameof(SideDone), null);
            Try(AccessTools.Method(typeof(CheckCollisionSideEnter), "CheckTouching"), nameof(SideEnter), nameof(SideEnterDone), null);
            Try(AccessTools.Method(typeof(RayCast2d), "DoRaycast"), nameof(Ray), nameof(RayDone), null);
            Try(AccessTools.Method(typeof(RayCast2dV2), "DoRaycast"), nameof(RayV2), nameof(RayV2Done), null);
            Try(AccessTools.Method(typeof(Physics2D), nameof(Physics2D.Raycast), new[] { typeof(Vector2), typeof(Vector2), typeof(float), typeof(int) }), null, null, nameof(Cast));
            Try(AccessTools.Method(typeof(Physics2D), nameof(Physics2D.Raycast), new[] { typeof(Vector2), typeof(Vector2), typeof(float), typeof(int), typeof(float), typeof(float) }), null, null, nameof(CastDepth));
            Plugin.Log.LogInfo($"[首领AI] 射线补丁装上 {ok}/6");
        }

        static void Ask(FsmStateAction a)
        {
            var go = a?.Fsm?.GameObject;
            if (BossAi.OfFight(go)) asking++;
        }

        static void Side(CheckCollisionSide __instance, out bool __state) { int was = asking; Ask(__instance); __state = asking != was; }
        static void SideDone(bool __state) { if (__state) asking--; }

        static void SideEnter(CheckCollisionSideEnter __instance, out bool __state) { int was = asking; Ask(__instance); __state = asking != was; }
        static void SideEnterDone(bool __state) { if (__state) asking--; }

        static void Ray(RayCast2d __instance, out bool __state) { int was = asking; Ask(__instance); __state = asking != was; }
        static void RayDone(bool __state) { if (__state) asking--; }

        static void RayV2(RayCast2dV2 __instance, out bool __state) { int was = asking; Ask(__instance); __state = asking != was; }
        static void RayV2Done(bool __state) { if (__state) asking--; }

        static readonly RaycastHit2D[] hits = new RaycastHit2D[16];

        static void Cast(Vector2 origin, Vector2 direction, float distance, int layerMask, ref RaycastHit2D __result)
        {
            if (asking <= 0 || __result.collider == null || !World.ChunkColliders.IsBuilt(__result.collider)) return;
            __result = Past(Physics2D.RaycastNonAlloc(origin, direction, hits, distance, layerMask));
        }

        static void CastDepth(Vector2 origin, Vector2 direction, float distance, int layerMask, float minDepth, float maxDepth, ref RaycastHit2D __result)
        {
            if (asking <= 0 || __result.collider == null || !World.ChunkColliders.IsBuilt(__result.collider)) return;
            __result = Past(Physics2D.RaycastNonAlloc(origin, direction, hits, distance, layerMask, minDepth, maxDepth));
        }

        /// <summary>The nearest hit that is not the player's.</summary>
        static RaycastHit2D Past(int n)
        {
            DevFiltered++;
            RaycastHit2D best = default;
            float d = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var c = hits[i].collider;
                if (c == null || World.ChunkColliders.IsBuilt(c) || hits[i].distance >= d) continue;
                best = hits[i];
                d = hits[i].distance;
            }
            return best;
        }
    }
}
