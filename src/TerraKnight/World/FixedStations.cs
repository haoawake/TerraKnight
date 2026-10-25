using System.Linq;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>
    /// The Demon Altar and the Crimson Altar, which no one can make (Terraria's world puts them in its evil
    /// biomes): here they stand in Dirtmouth beside its bench, where every game passes from the start (the Night's
    /// Edge and the other things made at an altar are made there), on the even ground nearest the bench, one each
    /// side where there is room. They are stations like the ones set down (World.Stations: what is made "at a
    /// Demon Altar" is made by them), fixed: never knocked down, not saved, set again whenever Dirtmouth is loaded,
    /// always in the same two places: only the scene's own ground is looked at, never what the player has built on it.
    /// </summary>
    internal static class FixedStations
    {
        const string Town = "Town";
        static string placedIn;
        static float nextTry;
        public static Vector2 DevDemon, DevCrimson;

        /// <summary>The stations were cleared (a save loaded): set them again.</summary>
        public static void Reset()
        {
            placedIn = null;
            netTaken.Clear();
        }

        /// <summary>The save's flag for one of the two knocked off (TerraMod.GrantedAbilities).</summary>
        static string TakenFlag(StationKind kind) => "altar.taken." + kind;

        /// <summary>The host's world's altars knocked off, as its records say (a LAN guest's game).</summary>
        static readonly System.Collections.Generic.HashSet<StationKind> netTaken = new System.Collections.Generic.HashSet<StationKind>();

        /// <summary>One of Dirtmouth's knocked off (Stations.TrySmashAltar): in this world for good, the host's in a LAN game.</summary>
        public static bool Taken(StationKind kind)
        {
            if (Net.WorldSync.Guest) return netTaken.Contains(kind);
            var mod = TerraMod.Instance;
            return mod != null && mod.GrantedAbilities.Contains(TakenFlag(kind));
        }

        public static void MarkTaken(StationKind kind)
        {
            if (Net.WorldSync.Guest) netTaken.Add(kind);
            else TerraMod.Instance?.GrantedAbilities.Add(TakenFlag(kind));
        }

        /// <summary>A LAN guest: the host's record that one of them is gone.</summary>
        public static void NetTaken(StationKind kind) => netTaken.Add(kind);

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
                    if (!Flat(region, at, out var bottom) || spots.Any(o => Mathf.Abs(o.x - bottom.x) < 3.5f)) continue;
                    spots.Add(bottom);
                }
            if (spots.Count < 2) return;
            spots.Sort((a, b) => a.x.CompareTo(b.x));
            if (!Taken(StationKind.DemonAltar)) Stations.PlaceFixed(StationKind.DemonAltar, Town, spots[0] - (Vector2)region.Root.position);
            if (!Taken(StationKind.CrimsonAltar)) Stations.PlaceFixed(StationKind.CrimsonAltar, Town, spots[1] - (Vector2)region.Root.position);
            DevDemon = spots[0];
            DevCrimson = spots[1];
            placedIn = Town;
            Plugin.Log.LogInfo($"[祭坛] 德特茅斯长椅 {from:F1} 旁：恶魔祭坛 {DevDemon:F1}，猩红祭坛 {DevCrimson:F1}");
        }

        /// <summary>
        /// Three cells of the scene's own even ground under a point, with room above for an altar (3x2 tiles). Only
        /// the scene's ground counts (its Original cells, and those dug out of it): a platform or a block the player
        /// puts over an altar neither holds it up at the next load nor pushes it aside.
        /// </summary>
        static bool Flat(TerrainRegion region, Vector2 at, out Vector2 bottom)
        {
            bottom = default;
            const float cell = CellGrid.CellSize;
            var c = CellGrid.WorldToCell(at);
            for (int y = c.y; y > c.y - 12; y--)
            {
                if (!Ground(region, c.x, y) || Ground(region, c.x, y + 1)) continue;
                for (int i = -1; i <= 1; i++)
                    if (!Ground(region, c.x + i, y) || Ground(region, c.x + i, y + 1) || Ground(region, c.x + i, y + 2)) return false;
                float x = (c.x + 0.5f) * cell, top = (y + 1) * cell;
                bottom = new Vector2(x, Surface(region, x, top));
                return true;
            }
            return false;
        }

        static bool Ground(TerrainRegion region, int gx, int gy)
        {
            if (!region.Contains(gx, gy)) return false;
            int x = gx - region.CellOffset.x, y = gy - region.CellOffset.y;
            return region.Grid.Get(x, y) == CellKind.Original || region.Grid.IsDugGround(x, y);
        }

        /// <summary>Where the scene's ground's collider is, in the top cell of its ground at x (its top, when nothing of the scene's is hit there).</summary>
        static float Surface(TerrainRegion region, float x, float top)
        {
            const int mask = 1 << ChunkColliders.TerrainLayer;
            foreach (var hit in Physics2D.RaycastAll(new Vector2(x, top + 0.3f), Vector2.down, 0.3f + CellGrid.CellSize, mask))
            {
                if (hit.point.y > top + 0.05f) continue;
                var under = CellGrid.WorldToCell(new Vector2(x, hit.point.y - 0.02f));
                if (Ground(region, under.x, under.y)) return hit.point.y;
            }
            return top;
        }

        /// <summary>Dev: the altars set again (as at a load), from what is in Dirtmouth now.</summary>
        public static void DevReplace()
        {
            Stations.DevRemoveFixed();
            placedIn = null;
            nextTry = 0f;
        }
    }
}
