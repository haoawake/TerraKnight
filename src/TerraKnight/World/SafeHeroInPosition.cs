using System;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// HeroController.SendHeroInPosition calls the scene's listeners one after the other, and one
    /// that throws stops the rest and the whole entry with it (EnterScene never hands back control:
    /// the player frozen where he stands). The seamless world makes such a listener: a scene loaded
    /// beside the current one while the player is between scenes has its IgnoreHeroCollision wait
    /// for him to be in position, and when that scene is put away the listener stays behind, its
    /// object gone (the freeze going down into the Crossroads, and after bosses). Each listener is
    /// called on its own here; one that fails is logged and the rest go on.
    /// </summary>
    [HarmonyPatch(typeof(HeroController), "SendHeroInPosition")]
    internal static class SafeHeroInPosition
    {
        static readonly AccessTools.FieldRef<HeroController, HeroController.HeroInPosition> inPosition =
            AccessTools.FieldRefAccess<HeroController, HeroController.HeroInPosition>("heroInPosition");
        static readonly AccessTools.FieldRef<HeroController, HeroController.HeroInPosition> delayedA =
            AccessTools.FieldRefAccess<HeroController, HeroController.HeroInPosition>("heroInPositionDelayedA");
        static readonly AccessTools.FieldRef<HeroController, HeroController.HeroInPosition> delayedB =
            AccessTools.FieldRefAccess<HeroController, HeroController.HeroInPosition>("heroInPositionDelayedB");
        static readonly AccessTools.FieldRef<HeroController, HeroAnimationController> animCtrl =
            AccessTools.FieldRefAccess<HeroController, HeroAnimationController>("animCtrl");

        public static int DevFailures;

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        static bool Prefix(HeroController __instance, bool forceDirect)
        {
            __instance.isHeroInPosition = true;
            var anim = animCtrl(__instance);
            if (anim != null) anim.waitingToEnter = false;
            Call(__instance, inPosition, forceDirect);
            Call(__instance, delayedA, forceDirect);
            Call(__instance, delayedB, forceDirect);
            return false;
        }

        static void Call(HeroController hero, AccessTools.FieldRef<HeroController, HeroController.HeroInPosition> field, bool forceDirect)
        {
            var listeners = field(hero);
            if (listeners == null) return;
            foreach (var d in listeners.GetInvocationList())
            {
                try
                {
                    ((HeroController.HeroInPosition)d)(forceDirect);
                }
                catch (Exception e)
                {
                    DevFailures++;
                    // A listener that failed would have taken itself off after its work (IgnoreHeroCollision's does): taken off here.
                    field(hero) = (HeroController.HeroInPosition)Delegate.Remove(field(hero), d);
                    string owner = d.Target is UnityEngine.Object o ? (o != null ? o.name : "已销毁的物体") : d.Method.DeclaringType?.Name;
                    Plugin.Log.LogWarning($"[进场] 主角就位的监听 {owner} 出错，跳过它继续进场：{e.GetType().Name} {e.Message}");
                }
            }
        }
    }
}
