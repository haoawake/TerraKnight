using System.Collections.Generic;

namespace TerraKnight.Items
{
    /// <summary>
    /// The areas an enemy lives in (its scenes from the scan, BestiaryHabitatData.cs), each with one of its scenes there, in
    /// the book's route order; a stage's own table ("Hornet@4") or a record the scan lacks: its roster scene's area. A boss:
    /// its world rooms (the scan's, its arena's, the roster's) and its Godhome room apart (Godhome pays the player's stage,
    /// the lunar weapons at the last: ProgressionLoot.Build), so that the book shows a world fight's drops for it first.
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
            bool boss = EnemyDrops.IsBoss(record);
            if (boss)
            {
                // The world's rooms first, each a kill there of this record (Hornet's Kingdom's Edge room is "Hornet@4"'s); a
                // Godhome room in the scan or the roster is its Godhome one, below.
                bool Ours(string s) => s != null && !World.Areas.GodhomeRoom(s) && EnemyDrops.KeyFor(record, EnemyDrops.VariantLevel(s)) == record;
                if (!record.Contains("@") && Scenes().TryGetValue(record, out var rooms))
                    foreach (var s in rooms)
                        if (Ours(s)) Add(s);
                string arena = BossArena.SceneOf(record), roster = EnemyDrops.SceneOf(record);
                if (Ours(arena)) Add(arena);
                if (Ours(roster)) Add(roster);
                if (GodhomeRooms.TryGetValue(record, out var gg)) Add(gg);
                else if (World.Areas.GodhomeRoom(roster)) Add(roster);
            }
            else if (!record.Contains("@") && Scenes().TryGetValue(record, out var found))
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

        /// <summary>A boss's room outside Godhome (its world fight's, whose loot a world bag holds); null for a Godhome-only boss.</summary>
        public static string WorldScene(string record)
        {
            foreach (var h in Of(record))
                if (!World.Areas.GodhomeRoom(h.Scene)) return h.Scene;
            return null;
        }

        /// <summary>
        /// The Godhome room each boss is fought in there (the Hall of Gods' statues, the Pantheons' own); its kill there is the
        /// record's (EnemyDrops.KeyFor: the dream bosses' "@5" ones in their own rooms).
        /// </summary>
        static readonly Dictionary<string, string> GodhomeRooms = new Dictionary<string, string>
        {
            ["BigFly"] = "GG_Gruz_Mother", ["BigBuzzer"] = "GG_Vengefly", ["Mawlek"] = "GG_Brooding_Mawlek", ["FalseKnight"] = "GG_False_Knight",
            ["Hornet"] = "GG_Hornet_1", ["MegaMossCharger"] = "GG_Mega_Moss_Charger", ["FlukeMother"] = "GG_Flukemarm",
            ["MantisLord"] = "GG_Mantis_Lords", ["HiveKnight"] = "GG_Hive_Knight", ["InfectedKnight"] = "GG_Broken_Vessel",
            ["MimicSpider"] = "GG_Nosk", ["JarCollector"] = "GG_Collector", ["MegaBeamMiner"] = "GG_Crystal_Guardian",
            ["MegaJellyfish"] = "GG_Uumuu", ["TraitorLord"] = "GG_Traitor_Lord", ["GreyPrince"] = "GG_Grey_Prince_Zote",
            ["MageLord"] = "GG_Soul_Master", ["MageKnight"] = "GG_Mage_Knight", ["BlackKnight"] = "GG_Watcher_Knights",
            ["DungDefender"] = "GG_Dung_Defender", ["WhiteDefender"] = "GG_White_Defender", ["Grimm"] = "GG_Grimm",
            ["NightmareGrimm"] = "GG_Grimm_Nightmare", ["FalseKnight@5"] = "GG_Failed_Champion", ["MageLord@5"] = "GG_Soul_Tyrant",
            ["InfectedKnight@5"] = "GG_Lost_Kin", ["GhostXero"] = "GG_Ghost_Xero", ["GhostMarmu"] = "GG_Ghost_Marmu",
            ["GhostGalien"] = "GG_Ghost_Galien", ["GhostHu"] = "GG_Ghost_Hu", ["GhostAladar"] = "GG_Ghost_Gorb",
            ["GhostNoEyes"] = "GG_Ghost_No_Eyes", ["GhostMarkoth"] = "GG_Ghost_Markoth", ["FinalBoss"] = "GG_Radiance",
            ["LobsterLancer"] = "GG_God_Tamer", ["Oblobble"] = "GG_Oblobbles", ["NailBros"] = "GG_Nailmasters", ["Nailsage"] = "GG_Sly",
            ["Paintmaster"] = "GG_Painter", ["HollowKnightPrime"] = "GG_Hollow_Knight",
        };

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
