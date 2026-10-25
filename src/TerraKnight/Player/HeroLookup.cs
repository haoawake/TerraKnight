using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Player
{
    /// <summary>
    /// HeroController.SilentInstance with no hero (the title screen, the start menu, a load): the game searches the whole
    /// scene for one (FindObjectOfType) every time it is asked, and the mod asks dozens of times a frame. On the start
    /// menu, with its pictures loaded, that came to about 1 ms a call and held the screen at 40 frames a second. A hero
    /// sets itself as the instance in its Awake, so the search is only a fallback: here it runs at most twice a second
    /// while there is none.
    /// </summary>
    [HarmonyPatch(typeof(HeroController), nameof(HeroController.SilentInstance), MethodType.Getter)]
    internal static class HeroLookup
    {
        static float nextSearch;

        static bool Prefix(ref HeroController __result)
        {
            var hero = HeroController.UnsafeInstance;
            if (hero != null)
            {
                __result = hero;
                return false;
            }
            float now = Time.unscaledTime;
            if (now < nextSearch)
            {
                __result = null;
                return false;
            }
            nextSearch = now + 0.5f;
            return true;
        }
    }
}
