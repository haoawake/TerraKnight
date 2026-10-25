using BepInEx.Configuration;
using UnityEngine;

namespace TerraKnight.Effects
{
    /// <summary>Local display preference only: never changes attacks, simulation or network state.</summary>
    internal static class WeaponEffects
    {
        static ConfigEntry<float> setting;
        public static void Bind(ConfigFile config) => setting = config.Bind("Video", "WeaponEffectsOpacity", 1f,
            new ConfigDescription("武器特效不透明度：0 隐藏，1 正常显示。仅影响本机显示。\nWeapon effects opacity: 0 hidden, 1 normal. Local display only.", new AcceptableValueRange<float>(0f, 1f)));
        public static float Opacity => setting == null ? 1f : Sanitize(setting.Value);
        internal static string DevConfigPath => setting?.ConfigFile.ConfigFilePath;
        static float Sanitize(float value) => float.IsNaN(value) || float.IsInfinity(value) ? 1f : Mathf.Clamp01(value);
        public static void Set(float value) { if(setting != null) setting.Value = Mathf.Round(Sanitize(value)*100f)/100f; }
        // FxBatch receives premultiplied colours: both RGB and alpha must fade, including A=0 additive light.
        public static Color Premultiplied(Color color) => color * Opacity;
        public static Color Straight(Color color) { color.a *= Opacity; return color; }
        // TerraParticles' additive sprites already premultiply their RGB and fix their alpha to one.
        public static Color Additive(Color color) { float k=Opacity; color.r*=k; color.g*=k; color.b*=k; return color; }
    }
}
