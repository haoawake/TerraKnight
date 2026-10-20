using System.Collections.Generic;

namespace TerraKnight.Items
{
    /// <summary>
    /// The areas an enemy lives in (its scenes from the scan, BestiaryHabitatData.cs), each with one of its scenes there, in
    /// the book's route order; a boss, a stage's own table ("Hornet@4") or a record the scan lacks: its roster scene's area.
    /// A kill's drops are its area's (ProgressionLoot by the kill's scene: the area's ores, materials and equipment), so the
    /// book shows them area by area, each worked out for one of the enemy's own scenes there, as a kill there gives them.
    /// </summary>
    internal static partial class BestiaryHabitats
    {
        public struct Home
        {
            public string Area, Scene;
        }

        static readonly Dictionary<string, List<Home>> homes = new Dictionary<string, List<Home>>();

        public static IReadOnlyList<Home> Of(string record)
        {
            if (record == null) return System.Array.Empty<Home>();
            if (homes.TryGetValue(record, out var list)) return list;
            list = new List<Home>();
            var areas = new HashSet<string>();
            void Add(string scene)
            {
                if (string.IsNullOrEmpty(scene)) return;
                string area = World.Areas.AreaName(scene);
                if (string.IsNullOrEmpty(area) || area == "未知" || !areas.Add(area)) return;
                list.Add(new Home { Area = area, Scene = scene });
            }
            if (!record.Contains("@") && !EnemyDrops.IsBoss(record) && Scenes().TryGetValue(record, out var found))
                foreach (var s in found)
                    if (EnemyDrops.KeyFor(record, EnemyDrops.VariantLevel(s)) == record) Add(s);
            if (list.Count == 0) Add(EnemyDrops.SceneOf(record));
            var route = BestiaryRoute.Areas;
            int Order(Home h)
            {
                for (int i = 0; i < route.Count; i++) if (route[i] == h.Area) return i;
                return route.Count;
            }
            list.Sort((a, b) => Order(a).CompareTo(Order(b)));
            homes[record] = list;
            return list;
        }

        /// <summary>Whether the player has been in that area (a scene of it among the save's visited scenes).</summary>
        public static bool Visited(string area)
        {
            var pd = PlayerData.instance;
            if (pd == null || pd.scenesVisited == null) return true;
            if (visitedAt != pd.scenesVisited.Count)
            {
                visitedAt = pd.scenesVisited.Count;
                visited.Clear();
                foreach (var s in pd.scenesVisited) visited.Add(World.Areas.AreaName(s));
            }
            return visited.Contains(area);
        }

        static readonly HashSet<string> visited = new HashSet<string>();
        static int visitedAt = -1;
    }
}
