using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>
    /// The rooms' stretch on the map (the user's 10-30 choice: "保留真实比例，只让每个房间小幅伸缩去闭合环路"). A room keeps its
    /// own cells, drawn from its origin a little wider or taller (at most a quarter, most of them a few hundredths), as
    /// Hollow Knight's own map bends its rooms, so the loops of gates the world can't lay flat close (maplayout3's
    /// stretch.py). Every point of a scene goes onto the map through it, and back.
    /// </summary>
    internal static partial class WorldMap
    {
        /// <summary>A scene's stretch on the map, across and up (one where the layout has none).</summary>
        static Vector2 StretchOf(string scene) => scene != null && stretch.TryGetValue(scene, out var s) ? s : Vector2.one;

        /// <summary>A point of a scene in its own cells, on the map (global cells): from its origin, stretched with it.</summary>
        static Vector2 OnMap(string scene, Vector2Int at, Vector2 cells)
        {
            var s = StretchOf(scene);
            return new Vector2(at.x + cells.x * s.x, at.y + cells.y * s.y);
        }

        /// <summary>A point of the map (global cells) in a scene's own cells.</summary>
        static Vector2 InScene(string scene, Vector2Int at, Vector2 cell)
        {
            var s = StretchOf(scene);
            return new Vector2((cell.x - at.x) / s.x, (cell.y - at.y) / s.y);
        }

        /// <summary>A point of a scene in its own cells on the map (global cells); false: the scene has no place there.</summary>
        public static bool TryMapCell(string scene, Vector2 cells, out Vector2 map)
        {
            map = default;
            if (scene == null || !layout.TryGetValue(scene, out var at)) return false;
            map = OnMap(scene, at, cells);
            return true;
        }

        /// <summary>Dev: a scene's stretch.</summary>
        public static Vector2 DevStretch(string scene) => StretchOf(scene);
        public static int DevStretched => stretch.Count;
    }
}
