using System.Linq;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// The Demon Altar and the Crimson Altar, which no one can make (Terraria's world puts them in its evil
    /// biomes): here they stand in Dirtmouth beside its bench, where every game passes from the start (the Night's
    /// Edge and the other things made at an altar are made there), on the even ground nearest the bench, one each
    /// side where there is room. They are stations like the ones set down (World.Stations: what is made "at a
    /// Demon Altar" is made by them), fixed: never knocked down, not saved, set again whenever Dirtmouth is loaded.
    /// </summary>
    internal static class FixedStations
    {
        const string Town = "Town";
        static string placedIn;
        static float nextTry;
        public static Vector2 DevDemon, DevCrimson;

        /// <summary>The stations were cleared (a save loaded): set them again.</summary>
        public static void Reset() => placedIn = null;

        /// <summary>Each frame: the altars beside Dirtmouth's bench once it is loaded.</summary>
        public static void Ensure()
        {
            var region = TerrainSystem.Find(Town);
            if (region == null)
            {
                placedIn = null;
                return;
            }
            if (placedIn == Town || Time.time < nextTry) return;
            nextTry = Time.time + 1f;
            var bench = Object.FindObjectsByType<PlayMakerFSM>(FindObjectsSortMode.None)
                .FirstOrDefault(f => f.FsmName == "Bench Control" && f.gameObject.scene.name == Town);
            if (bench == null) return;
            Vector2 from = bench.transform.position;
            // Nearest the bench first, right and left in turn, clear of the bench itself and of each other.
            var spots = new System.Collections.Generic.List<Vector2>();
            for (float dx = 2.5f; dx < 24f && spots.Count < 2; dx += 0.5f)
                foreach (float side in new[] { 1f, -1f })
                {
                    if (spots.Count >= 2) break;
                    var at = new Vector2(from.x + side * dx, from.y + 2f);
                    if (!Flat(at, out var bottom) || spots.Any(o => Mathf.Abs(o.x - bottom.x) < 3.5f)) continue;
                    spots.Add(bottom);
                }
            if (spots.Count < 2) return;
            spots.Sort((a, b) => a.x.CompareTo(b.x));
            Stations.PlaceFixed(StationKind.DemonAltar, Town, spots[0] - (Vector2)region.Root.position);
            Stations.PlaceFixed(StationKind.CrimsonAltar, Town, spots[1] - (Vector2)region.Root.position);
            DevDemon = spots[0];
            DevCrimson = spots[1];
            placedIn = Town;
            Plugin.Log.LogInfo($"[祭坛] 德特茅斯长椅 {from:F1} 旁：恶魔祭坛 {DevDemon:F1}，猩红祭坛 {DevCrimson:F1}");
        }

        /// <summary>Three cells of even ground under a point, with room above for an altar (3x2 tiles).</summary>
        static bool Flat(Vector2 at, out Vector2 bottom)
        {
            bottom = default;
            const int mask = 1 << ChunkColliders.TerrainLayer;
            const float cell = CellGrid.CellSize;
            float x = (Mathf.Floor(at.x / cell) + 0.5f) * cell;
            var down = Physics2D.Raycast(new Vector2(x, at.y), Vector2.down, 6f, mask);
            if (down.collider == null) return false;
            float y = down.point.y;
            for (int i = -1; i <= 1; i++)
            {
                var side = Physics2D.Raycast(new Vector2(x + i * cell, y + 0.1f), Vector2.down, 0.2f, mask);
                if (side.collider == null || Mathf.Abs(side.point.y - y) > 0.06f) return false;
            }
            if (Physics2D.OverlapBox(new Vector2(x, y + cell + 0.02f), new Vector2(3f * cell, 2f * cell) * 0.9f, 0f, mask) != null) return false;
            bottom = new Vector2(x, y);
            return true;
        }
    }
}
