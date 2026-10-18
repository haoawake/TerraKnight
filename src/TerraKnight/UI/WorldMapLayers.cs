// Based on the layout survey, with explicit indoor overrides: side gates can also enter caves.
using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.UI
{
    internal static partial class WorldMap
    {
        // Native world units, following the roofs of the connected caves on the atlas.
        // These rooms contain both exposed cliffs and enclosed passages; room-name tags alone cannot split them.
        static readonly Dictionary<string, Rect[]> interiorParts = new Dictionary<string, Rect[]>
        {
            ["Cliffs_01"] = new[] { new Rect(105f, 0f, 35f, 145f) },
            ["Cliffs_02"] = new[]
            {
                new Rect(0f, 0f, 28f, 50f), new Rect(28f, 0f, 16f, 24f),
                new Rect(44f, 0f, 24f, 34f), new Rect(68f, 0f, 16f, 25f),
                new Rect(84f, 0f, 16f, 29f), new Rect(100f, 0f, 40f, 32f),
                new Rect(140f, 0f, 20f, 24f), new Rect(160f, 0f, 45f, 29f),
            },
        };

        static bool IsDeepAt(string scene, Vector2 local)
        {
            if (IsDeep(scene)) return true;
            if (scene != null && interiorParts.TryGetValue(scene, out var parts))
                foreach (var part in parts) if (part.Contains(local)) return true;
            return false;
        }

        static bool CellOnLayer(Room room, int x, int y, bool deep) =>
            IsDeepAt(room.Name, new Vector2((x + .5f) * C, (y + .5f) * C)) == deep;

        static bool Shown(string scene, Vector2 local) => IsDeepAt(scene, local) == ViewDeep;

        public static bool DevDeepAt(string scene, Vector2 local) => IsDeepAt(scene, local);
        /// <summary>
        /// The deep layer: the scenes no side, top or bottom gate joins to the world King's Pass is in (the seamless
        /// world's), reached only by a door, a dream or a tram: shops and houses, the Colosseum, the Troupe's tents, the
        /// archives, the White Palace, the dreams, the trams. Indoor caves reached through side gates are explicitly
        /// included too: being connected seamlessly does not make a cave part of the outdoor map layer.
        /// </summary>
        static readonly HashSet<string> deepRooms = new HashSet<string>
        {
            // Howling Cliffs: Stag Nest, the approach to Joni's Repose, Joni's room and the Troupe lantern cave.
            // The exposed parts of Cliffs_01 and Cliffs_02 stay outside; interiorParts splits their caves.
            "Cliffs_03", "Cliffs_04", "Cliffs_05", "Cliffs_06",
            // Interior rooms with side entrances omitted by the original door-only reachability survey.
            "Room_Colosseum_01", "Room_Colosseum_02", "Room_Colosseum_Spectate", "Room_GG_Shortcut", "Room_Mask_Maker", "Room_Wyrm",
            "Crossroads_ShamanTemple", "Deepnest_45_v02", "Deepnest_East_17", "Deepnest_Spider_Town", "Dream_01_False_Knight", "Dream_02_Mage_Lord",
            "Dream_03_Infected_Knight", "Dream_04_White_Defender", "Dream_Abyss", "Dream_Backer_Shrine", "Dream_Guardian_Hegemol", "Dream_Guardian_Lurien",
            "Dream_Guardian_Monomon", "Dream_Mighty_Zote", "Dream_Nailcollection", "Dream_Room_Believer_Shrine", "Fungus1_35", "Fungus1_36",
            "Fungus3_archive", "Fungus3_archive_02", "Grimm_Divine", "Grimm_Main_Tent", "Grimm_Nightmare", "Mines_35",
            "Room_Bretta", "Room_Bretta_Basement", "Room_Charm_Shop", "Room_Colosseum_Bronze", "Room_Colosseum_Gold", "Room_Colosseum_Silver",
            "Room_Fungus_Shaman", "Room_Jinn", "Room_Mansion", "Room_Mender_House", "Room_Ouiji", "Room_Queen",
            "Room_Slug_Shrine", "Room_Sly_Storeroom", "Room_Town_Stag_Station", "Room_Tram", "Room_Tram_RG", "Room_mapper",
            "Room_nailmaster", "Room_nailmaster_02", "Room_nailmaster_03", "Room_nailsmith", "Room_ruinhouse", "Room_shop",
            "Room_spider_small", "Room_temple", "Ruins_Elevator", "Ruins_House_01", "Ruins_House_02", "Ruins_House_03",
            "White_Palace_01", "White_Palace_02", "White_Palace_03_hub", "White_Palace_04", "White_Palace_05", "White_Palace_06",
            "White_Palace_07", "White_Palace_08", "White_Palace_09", "White_Palace_11", "White_Palace_12", "White_Palace_13",
            "White_Palace_14", "White_Palace_15", "White_Palace_16", "White_Palace_17", "White_Palace_18", "White_Palace_19",
            "White_Palace_20",
        };
    }
}
