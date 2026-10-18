using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Reviewed feet positions on each encounter's starting floor, in the native room coordinates.
    /// Source: live boss-landings-survey.json, native battle gates/camera locks and worldatlas terrain.
    /// Dormant bosses and second-phase bodies are often outside the starting arena; never average them
    /// or let the entry door's height choose a different floor. Both summoning routes use these points.
    /// </summary>
    internal static class BossArrivalPoints
    {
        internal static readonly Dictionary<string, Vector2> Feet = new Dictionary<string, Vector2>
        {
            ["FalseKnight"] = new Vector2(19f, 26.15f),
            ["BigFly"] = new Vector2(94f, 14.1f),
            ["Mawlek"] = new Vector2(68f, 3.1f),
            ["Hornet"] = new Vector2(26.5f, 27.1f),
            ["MegaMossCharger"] = new Vector2(42.5f, 6.1f),
            ["DungDefender"] = new Vector2(76.5f, 6.1f),
            ["TraitorLord"] = new Vector2(39f, 28.1f),
            ["MageLord"] = new Vector2(20f, 28.1f),
            ["MegaJellyfish"] = new Vector2(46f, 101.1f),
            ["FlukeMother"] = new Vector2(26f, 9.1f),
            ["MegaBeamMiner"] = new Vector2(22.5f, 10.1f),
            ["BlackKnight"] = new Vector2(35.5f, 69.1f),
            ["HiveKnight"] = new Vector2(68f, 26.1f),
            ["JarCollector"] = new Vector2(53f, 94.1f),
            ["MimicSpider"] = new Vector2(93f, 6.1f),
            ["Hornet@4"] = new Vector2(24f, 27.1f),
            ["InfectedKnight"] = new Vector2(30f, 27.1f),
            ["Grimm"] = new Vector2(85f, 5.1f),
            ["HollowKnight"] = new Vector2(39.5f, 5.1f),
            ["FalseKnight@5"] = new Vector2(50.5f, 26.15f),
            ["MageLord@5"] = new Vector2(20f, 28.1f),
            ["InfectedKnight@5"] = new Vector2(32.5f, 27.1f),
            ["WhiteDefender"] = new Vector2(73.5f, 6.1f),
            ["GreyPrince"] = new Vector2(23f, 5.1f),
            ["NightmareGrimm"] = new Vector2(78f, 5.1f),
        };

        static readonly HashSet<string> nativeChallenges = new HashSet<string>
        {
            "MantisLord", "FinalBoss", "GhostAladar", "GhostNoEyes", "GhostHu", "GhostMarmu", "GhostGalien", "GhostXero", "GhostMarkoth",
        };

        internal static string PolicyOf(string key) => Feet.ContainsKey(key) ? "reviewed-floor"
            : nativeChallenges.Contains(key) ? "native-challenge"
            : BossArena.SceneOf(key)?.StartsWith("GG_") == true ? "native-godhome" : null;

        internal static bool TryFeet(string key, out Vector2 feet)
        {
            if (!Feet.TryGetValue(key, out feet)) return false;
            var terrain = World.TerrainSystem.Find(BossArena.SceneOf(key));
            // The same room may be loaded as a seamless neighbour or for a LAN battle.
            if (terrain?.Root != null) feet += (Vector2)terrain.Root.position;
            return true;
        }
    }
}
