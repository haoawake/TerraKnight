using System.Collections.Generic;

namespace TerraKnight.World
{
    /// <summary>Independent background layer. 0 is a removed wall; absence keeps the scene's natural dug wall.</summary>
    internal static class WorldWalls
    {
        static readonly Dictionary<string, Dictionary<int, int>> scenes = new Dictionary<string, Dictionary<int, int>>();
        public static IEnumerable<KeyValuePair<string, Dictionary<int, int>>> All => scenes;
        public static Dictionary<int, int> For(string scene)
        {
            scene = scene ?? "";
            if (!scenes.TryGetValue(scene, out var walls)) scenes[scene] = walls = new Dictionary<int, int>();
            return walls;
        }
        public static void Clear() => scenes.Clear();
        public static void ClearInPlace() { foreach (var walls in scenes.Values) walls.Clear(); }
    }
}
