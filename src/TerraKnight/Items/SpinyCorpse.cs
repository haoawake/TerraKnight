using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// A Spiny Husk's corpse (CorpseSpineBurst) as the game has it: a second after landing it shakes and bursts its spines,
    /// and the burst ends its invincibility, so it can be struck apart (the user's 10-30 "改完之后好像不会爆刺了，死不了了").
    /// One change (10-28 "多刺躯壳死了还在放刺"): the game leaves the burst's ring (Spine Hit, a DamageHero circle) on until
    /// the corpse is struck apart, a dead husk still hurting whoever comes near; here only the burst itself hurts.
    /// </summary>
    [HarmonyPatch(typeof(CorpseSpineBurst), "LandEffects")]
    internal static class SpinyCorpse
    {
        /// <summary>How long the burst's ring hurts once it is on.</summary>
        public const float Hurts = 0.5f;

        public static int DevBursts;

        [HarmonyPostfix]
        static void Landed(CorpseSpineBurst __instance)
        {
            if (__instance != null && __instance.spineHit != null) __instance.StartCoroutine(RingOff(__instance.spineHit));
        }

        static IEnumerator RingOff(GameObject ring)
        {
            // The burst comes 1.9 s after landing (DoLandEffects); a spell's kill has none.
            float until = Time.time + 4f;
            while (ring != null && !ring.activeSelf)
            {
                if (Time.time > until) yield break;
                yield return null;
            }
            if (ring == null) yield break;
            DevBursts++;
            yield return new WaitForSeconds(Hurts);
            if (ring != null) ring.SetActive(false);
        }
    }
}
