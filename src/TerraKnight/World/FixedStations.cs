using System.Linq;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// The Demon Altar and the Crimson Altar, which no one can make (Terraria's world puts them in its evil
    /// biomes): here they stand in the Temple of the Black Egg (Room_temple), where the Radiance's infection
    /// comes from, on the floor a few steps in from its door, side by side. They are stations like the ones
    /// set down (World.Stations: what is made "at a Demon Altar" is made by them), fixed: never knocked down,
    /// not saved, set again whenever the temple is loaded.
    /// </summary>
    internal static class FixedStations
    {
        const string Temple = "Room_temple";
        static string placedIn;
        static float nextTry;
        public static Vector2 DevDemon, DevCrimson;

        /// <summary>The stations were cleared (a save loaded): set them again.</summary>
        public static void Reset() => placedIn = null;

        /// <summary>Each frame: the altars into the temple once it is loaded.</summary>
        public static void Ensure()
        {
            var region = TerrainSystem.Find(Temple);
            if (region == null)
            {
                placedIn = null;
                return;
            }
            if (placedIn == Temple || Time.time < nextTry) return;
            nextTry = Time.time + 1f;
            // The door in: the temple's own gate, the floor to its right.
            var gate = Object.FindObjectsByType<TransitionPoint>(FindObjectsSortMode.None)
                .Where(g => g.gameObject.scene.name == Temple).OrderBy(g => g.transform.position.x).FirstOrDefault();
            if (gate == null) return;
            Vector2 from = gate.transform.position;
            int found = 0;
            Vector2 last = new Vector2(float.NegativeInfinity, 0f);
            for (float dx = 3f; dx < 30f && found < 2; dx += 0.5f)
            {
                var at = new Vector2(from.x + dx, from.y + 2f);
                if (at.x - last.x < 3.5f || !Flat(at, out var bottom)) continue;
                var local = bottom - (Vector2)region.Root.position;
                Stations.PlaceFixed(found == 0 ? StationKind.DemonAltar : StationKind.CrimsonAltar, Temple, local);
                if (found == 0) DevDemon = bottom; else DevCrimson = bottom;
                last = bottom;
                found++;
            }
            if (found == 2)
            {
                placedIn = Temple;
                Plugin.Log.LogInfo($"[祭坛] 黑卵神殿：恶魔祭坛 {DevDemon:F1}，猩红祭坛 {DevCrimson:F1}");
            }
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
