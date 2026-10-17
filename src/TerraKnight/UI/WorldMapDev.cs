using UnityEngine;

namespace TerraKnight.UI
{
    internal static partial class WorldMap
    {
        /// <summary>Dev: every cell of a room the player has had on screen marked seen, as if walked all over.</summary>
        public static bool DevSeeAll(string scene)
        {
            if (scene == null || !rooms.TryGetValue(scene, out var room) || room.Seen == null) return false;
            for (int i = 0; i < room.Seen.Length; i++) room.Seen[i] = 0xFF;
            room.AnySeen = true;
            room.Dirty = true;
            return true;
        }

        /// <summary>Dev: centre the map on a cell of a room.</summary>
        public static void DevCentreCell(string scene, Vector2Int cell)
        {
            if (rooms.TryGetValue(scene, out var room)) pan = new Vector2(room.At.x + cell.x + 0.5f, room.At.y + cell.y + 0.5f);
        }

        /// <summary>Dev: what the map draws at a room's cell now (its picture rebuilt first), and whether it is seen.</summary>
        public static string DevCellDrawn(string scene, int x, int y, out Color32 drawn)
        {
            drawn = default;
            if (scene == null || !rooms.TryGetValue(scene, out var room) || room.Tiles == null) return "没有这个房间";
            if (x < 0 || y < 0 || x >= room.W || y >= room.H) return "房间外";
            if (room.Dirty || room.Open == null) Build(room);
            var t = (Tile)room.Tiles[y * room.W + x];
            drawn = IsSolid(t) ? room.Solid.GetPixel(x, y) : room.Open.GetPixel(x, y);
            return $"{t} 见过 {room.IsSeen(x, y)} 画成 ({drawn.r},{drawn.g},{drawn.b},{drawn.a}) 在 {(IsDeep(scene) ? "深层" : "表层")}";
        }

        /// <summary>Dev: how many of a room's cells are Meteorite on the map.</summary>
        public static int DevMeteorCellsIn(string scene)
        {
            if (scene == null || !rooms.TryGetValue(scene, out var room) || room.Tiles == null) return -1;
            int n = 0;
            foreach (var b in room.Tiles) if ((Tile)b == Tile.Meteorite) n++;
            return n;
        }
    }
}
