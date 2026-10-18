using UnityEngine;

namespace TerraKnight.UI
{
    internal static partial class WorldMap
    {
        /// <summary>Dev: inspect the exact textures used by both map sizes without depending on a visible window.</summary>
        public static (int Shallow, int Deep, int Overlap, int Wrong) DevLayerPixels(string scene, string folder)
        {
            if (!rooms.TryGetValue(scene, out var room)) return (-1, -1, -1, -1);
            var pixels = new Color32[2][];
            for (int layer = 0; layer < 2; layer++)
            {
                Build(room, layer == 1);
                var solid = room.Solid.GetPixels32();
                var open = room.Open.GetPixels32();
                var combined = new Color32[solid.Length];
                for (int i = 0; i < solid.Length; i++) combined[i] = open[i].a > 0 ? open[i] : solid[i];
                pixels[layer] = combined;
                var image = new Texture2D(room.W, room.H, TextureFormat.RGBA32, false);
                image.SetPixels32(combined); image.Apply(false);
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(folder, scene + (layer == 1 ? "-inner.png" : "-outer.png")), image.EncodeToPNG());
                Object.Destroy(image);
            }
            int shallow = 0, deep = 0, overlap = 0, wrong = 0;
            for (int i = 0; i < pixels[0].Length; i++)
            {
                bool a = pixels[0][i].a > 0, b = pixels[1][i].a > 0;
                if (a) shallow++;
                if (b) deep++;
                if (a && b) overlap++;
                bool expected = IsDeepAt(scene, new Vector2((i % room.W + .5f) * C, (i / room.W + .5f) * C));
                if (a && expected || b && !expected) wrong++;
            }
            room.Dirty = true;
            return (shallow, deep, overlap, wrong);
        }

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
            if (room.Dirty || room.Open == null || room.BuiltDeep != ViewDeep) Build(room);
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
