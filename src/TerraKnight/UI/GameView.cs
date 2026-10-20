using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>
    /// The part of the screen the game actually draws to. Hollow Knight letterboxes to its own
    /// aspect range, so on a 16:10 screen there are bars above and below; the mod's UI is laid out
    /// inside the game view, scaled from a 1080-pixel-tall reference, times the player's interface
    /// scale (Terraria's UI Scale: 50% to 200%, never past what the screen holds, Main.UIScaleMax).
    /// </summary>
    internal static class GameView
    {
        /// <summary>Game view in GUI coordinates (origin top-left).</summary>
        public static Rect Rect
        {
            get
            {
                var cam = GameCameras.instance != null ? GameCameras.instance.mainCamera : null;
                if (cam == null) return new Rect(0, 0, Screen.width, Screen.height);
                var r = cam.pixelRect;
                return new Rect(r.x, Screen.height - r.y - r.height, r.width, r.height);
            }
        }

        public static BepInEx.Configuration.ConfigEntry<float> UiScaleSetting;
        public const float MinUiScale = 0.5f, MaxUiScale = 2f;

        public static void Bind(BepInEx.Configuration.ConfigFile cfg) =>
            UiScaleSetting = cfg.Bind("Interface", "UIScale", 1f,
                "界面缩放（0.5～2，即 50%～200%，泰拉瑞亚设置的“界面”页里调）：mod 的全部界面一起放大或缩小，最大按屏幕大小限制。\nInterface scale (0.5-2, 50%-200%, set on the Interface page of Terraria settings): the mod's whole interface larger or smaller, at most what the screen holds.");

        /// <summary>The scale asked for (Main.UIScaleWanted).</summary>
        public static float WantedScale => UiScaleSetting != null ? Mathf.Clamp(UiScaleSetting.Value, MinUiScale, MaxUiScale) : 1f;

        /// <summary>The largest the screen holds (Main.UIScaleMax: the screen at least 800 by 600 of the scaled interface's pixels), in the mod's own scale.</summary>
        public static float MaxScale
        {
            get
            {
                var r = Rect;
                float base1080 = Mathf.Max(0.01f, r.height / 1080f);
                return Mathf.Max(1f, Mathf.Min(r.height / 600f, r.width / 800f) / base1080);
            }
        }

        /// <summary>The scale in use (Main.UIScale).</summary>
        public static float UiScale => Mathf.Min(WantedScale, MaxScale);

        public static void SetUiScale(float scale)
        {
            if (UiScaleSetting != null) UiScaleSetting.Value = Mathf.Round(Mathf.Clamp(scale, MinUiScale, MaxUiScale) * 100f) / 100f;
        }

        public static float Scale
        {
            get
            {
                var r = Rect;
                float base1080 = r.height / 1080f;
                float wanted = WantedScale;
                if (wanted == 1f) return base1080;
                float max = Mathf.Max(1f, Mathf.Min(r.height / 600f, r.width / 800f) / Mathf.Max(0.01f, base1080));
                return base1080 * Mathf.Min(wanted, max);
            }
        }
    }
}
