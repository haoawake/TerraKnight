using System.Collections.Generic;

namespace TerraKnight.Items
{
    /// <summary>
    /// Where the monster book lists each enemy: the area a first run meets it in, the areas in
    /// the order they are reached (<see cref="areaNames"/>, from BestiaryRouteData.cs), and the
    /// enemy's place among that area's in the order they are met. An enemy not on the route goes
    /// by the area of its roster scene, those areas after the route's.
    /// </summary>
    internal static partial class BestiaryRoute
    {
        static Dictionary<string, (int Area, int Order)> index;
        static readonly List<string> areas = new List<string>();

        static void Build()
        {
            if (index != null) return;
            index = new Dictionary<string, (int, int)>();
            areas.AddRange(areaNames);
            for (int a = 0; a < areaRecords.Length; a++)
                for (int k = 0; k < areaRecords[a].Length; k++)
                    index[areaRecords[a][k]] = (a, k);
        }

        /// <summary>The book's areas, in route order.</summary>
        public static IReadOnlyList<string> Areas
        {
            get { Build(); return areas; }
        }

        /// <summary>The area (an index into <see cref="Areas"/>) and the order within it of a record.</summary>
        public static (int Area, int Order) Of(string record)
        {
            Build();
            if (record == null) return (0, 0);
            if (index.TryGetValue(record, out var at)) return at;
            string name = World.Areas.AreaName(EnemyDrops.SceneOf(record));
            int a = areas.IndexOf(name);
            if (a < 0)
            {
                areas.Add(name);
                a = areas.Count - 1;
            }
            at = (a, 1000);
            index[record] = at;
            return at;
        }

        public static string AreaOf(string record) => Areas[Of(record).Area];
    }
}
