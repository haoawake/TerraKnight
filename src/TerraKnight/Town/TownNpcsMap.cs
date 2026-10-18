using System.Collections.Generic;
using TerraKnight.Items;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Town
{
    /// <summary>
    /// The townsfolk on the map (Main.DrawNPCHeadOnMap): every resident in town, where it is. One whose scene is loaded
    /// stands where it walks; one with a house elsewhere, at its house; one on Dirtmouth's street while Dirtmouth isn't
    /// loaded, where it was last seen, or at Dirtmouth's bench before it has been seen this session.
    /// </summary>
    internal static partial class TownNpcs
    {
        /// <summary>A townsperson's head on the map: who, its name, the scene and the point in it (scene units), or the town's bench.</summary>
        public struct MapHead
        {
            public string Internal, Name, Scene;
            public Vector2 Local;
            public bool AtTownBench;
        }

        static readonly Dictionary<string, (string Scene, Vector2 Local)> lastSeen = new Dictionary<string, (string, Vector2)>();

        /// <summary>Forgets where the townsfolk were last seen (another save loaded).</summary>
        public static void ForgetMapHeads() => lastSeen.Clear();

        public static List<MapHead> MapHeads()
        {
            var list = new List<MapHead>();
            var done = new HashSet<string>();
            const float c = CellGrid.CellSize;
            foreach (var n in npcs)
            {
                var body = new Vector2(n.X, n.FeetY + 20f * Px);
                var region = TerraWorld.RegionAt(Mathf.FloorToInt(body.x / c), Mathf.FloorToInt(body.y / c));
                if (region?.SceneName == null) continue;
                var local = body - (Vector2)region.CellOffset * c;
                lastSeen[n.Def.Internal] = (region.SceneName, local);
                done.Add(n.Def.Internal);
                list.Add(new MapHead { Internal = n.Def.Internal, Name = n.Name, Scene = region.SceneName, Local = local });
            }
            int stage = Shop.UnlockedStage();
            foreach (var def in All)
            {
                if (done.Contains(def.Internal) || !Arrived.Contains(def.Internal) || !InTown(def, stage)) continue;
                string name = Names.TryGetValue(def.Internal, out var given) ? new Npc { Def = def, Given = given }.Name : Label(def);
                if (Houses.TryGetValue(def.Internal, out var h) && !string.IsNullOrEmpty(h.Scene))
                    list.Add(new MapHead { Internal = def.Internal, Name = name, Scene = h.Scene, Local = new Vector2((h.X + 0.5f) * c, (h.Y + 1.5f) * c) });
                else if (lastSeen.TryGetValue(def.Internal, out var seen))
                    list.Add(new MapHead { Internal = def.Internal, Name = name, Scene = seen.Scene, Local = seen.Local });
                else
                    list.Add(new MapHead { Internal = def.Internal, Name = name, Scene = "Town", AtTownBench = true });
            }
            return list;
        }
    }
}
