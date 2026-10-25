using HarmonyLib;
using UnityEngine;
using UnityStandardAssets.ImageEffects;

namespace TerraKnight.Effects
{
    /// <summary>Bloom's integer downsampling must not request a zero-sized temporary texture.</summary>
    [HarmonyPatch(typeof(BloomOptimized), "OnRenderImage")]
    internal static class BloomGuard
    {
        internal static int DevBypassed;

        [HarmonyPrefix]
        static bool BeforeRender(RenderTexture source, RenderTexture destination, BloomOptimized.Resolution ___resolution)
        {
            if (source == null) return false;
            int divisor = ___resolution == BloomOptimized.Resolution.Low ? 4 : 2;
            if (source.width >= divisor && source.height >= divisor) return true;
            // Window/viewport transitions and small offscreen targets can temporarily be below
            // the downsample size. Keep the frame, and allow bloom again on the next valid frame.
            Graphics.Blit(source, destination);
            DevBypassed++;
            return false;
        }
    }
}
