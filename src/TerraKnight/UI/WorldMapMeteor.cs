using System.Collections.Generic;

namespace TerraKnight.UI
{
    /// <summary>
    /// The map's side of a meteorite (World.Meteorites): the rooms the player has been in as the map has them, where one
    /// may come down, and its cells drawn bright once it has (seen at once, wherever the player is).
    /// </summary>
    internal static partial class WorldMap
    {
        /// <summary>What a room's cell is for a landing: none (off the room), open air, the room's own rock, a block the player set, something else.</summary>
        internal enum MeteorCell : byte { None, Air, Rock, Block, Other }

        /// <summary>The rooms the player has walked through themself (not bought maps nor the atlas's), with what they saw there.</summary>
        internal static List<string> VisitedRooms()
        {
            var list = new List<string>();
            foreach (var kv in rooms)
            {
                var r = kv.Value;
                if (r.Tiles == null || r.FromAtlas || !r.Captured || !r.AnySeen) continue;
                list.Add(kv.Key);
            }
            return list;
        }

        internal static bool RoomSize(string scene, out int w, out int h)
        {
            w = h = 0;
            if (scene == null || !rooms.TryGetValue(scene, out var r) || r.Tiles == null) return false;
            w = r.W;
            h = r.H;
            return true;
        }

        internal static MeteorCell CellOf(string scene, int x, int y)
        {
            if (scene == null || !rooms.TryGetValue(scene, out var r) || r.Tiles == null || x < 0 || y < 0 || x >= r.W || y >= r.H) return MeteorCell.None;
            switch ((Tile)r.Tiles[y * r.W + x])
            {
                case Tile.Air:
                case Tile.Dug:
                case Tile.None: return MeteorCell.Air;
                case Tile.Terrain: return MeteorCell.Rock;
                case Tile.Dirt:
                case Tile.Stone:
                case Tile.Wood:
                case Tile.Sand:
                case Tile.Meteorite: return MeteorCell.Block;
                default: return MeteorCell.Other;
            }
        }

        /// <summary>A cell the player has seen there (walked by: somewhere they can get to).</summary>
        internal static bool SeenIn(string scene, int x, int y) =>
            scene != null && rooms.TryGetValue(scene, out var r) && r.Seen != null && x >= 0 && y >= 0 && x < r.W && y < r.H && r.IsSeen(x, y);

        /// <summary>Something of the room's within so many cells: a bench, a door, a lever, a chest, a stag, an NPC, a pylon.</summary>
        internal static bool ThingNear(string scene, int x, int y, int within)
        {
            if (scene == null || !rooms.TryGetValue(scene, out var r)) return false;
            foreach (var t in r.Things)
            {
                if (t.Kind == Obj.Item || t.Kind == Obj.GeoRock || t.Kind == Obj.Grub || t.Kind == Obj.LifeCrystal) continue;
                int dx = x < t.Cells.xMin ? t.Cells.xMin - x : x >= t.Cells.xMax ? x - t.Cells.xMax + 1 : 0;
                int dy = y < t.Cells.yMin ? t.Cells.yMin - y : y >= t.Cells.yMax ? y - t.Cells.yMax + 1 : 0;
                if (dx <= within && dy <= within) return true;
            }
            return false;
        }

        /// <summary>A meteorite down: its cells drawn as Meteorite and its pit as dug rock, all of it on the map at once.</summary>
        internal static void MeteorLanded(string scene, List<(int x, int y, bool meteorite)> cells)
        {
            if (scene == null || !rooms.TryGetValue(scene, out var r) || r.Tiles == null) return;
            foreach (var (x, y, meteorite) in cells)
            {
                if (x < 0 || y < 0 || x >= r.W || y >= r.H) continue;
                r.Tiles[y * r.W + x] = (byte)(meteorite ? Tile.Meteorite : Tile.Dug);
                r.SetSeen(x, y);
            }
            r.AnySeen = true;
            r.Dirty = true;
            DevMeteorCells += cells.Count;
        }

        public static int DevMeteorCells;
    }
}
