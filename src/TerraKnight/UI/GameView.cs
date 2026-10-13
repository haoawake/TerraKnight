using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>
    /// The part of the screen the game actually draws to. Hollow Knight letterboxes to its own
    /// aspect range, so on a 16:10 screen there are bars above and below; the mod's UI is laid out
    /// inside the game view, scaled from a 1080-pixel-tall reference.
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

        public static float Scale => Rect.height / 1080f;
    }
}
