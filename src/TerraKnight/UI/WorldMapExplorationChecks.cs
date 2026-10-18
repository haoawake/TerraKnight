using System;
using System.IO;
using System.IO.Compression;
using UnityEngine;

namespace TerraKnight.UI
{
    internal static partial class WorldMap
    {
        /// <summary>Regression checks for the actual textures shared by all three map views.</summary>
        public static bool DevExplorationChecks(out string report)
        {
            var room = new Room
            {
                Name = "__fog_probe", W = 4, H = 2, Tiles = new byte[8], Wets = new byte[8], Seen = new byte[1], Hue = Color.gray,
            };
            room.Tiles[0] = room.Tiles[1] = (byte)Tile.Terrain;
            room.Tiles[2] = room.Tiles[3] = (byte)Tile.Air;
            room.Things.Add(new Thing { Kind = Obj.Door, Name = "door", Cells = new RectInt(3, 0, 1, 1) });
            try
            {
                room.SetSeen(0, 0);
                Build(room);
                bool hidden = room.Solid.GetPixel(1, 0).a == 0f && room.Open.GetPixel(2, 0).a == 0f && room.Open.GetPixel(3, 0).a == 0f;
                room.Charted = true;
                Build(room);
                byte Alpha(Texture2D t, int x) => ((Color32)t.GetPixel(x, 0)).a;
                // A bought map's unexplored cells: 30% of the colour, drawn whole (overlaps don't add up); its plain air not drawn.
                Color32 rock = TileColor(room, Tile.Terrain), dimRock = (Color32)room.Solid.GetPixel(1, 0), door = (Color32)room.Open.GetPixel(3, 0);
                bool dim = Alpha(room.Solid, 0) == 255 && Alpha(room.Solid, 1) == 255 && dimRock.r == (byte)(rock.r * MapExploration.ChartedAlpha / 255)
                    && Alpha(room.Open, 2) == 0 && Alpha(room.Open, 3) == 255 && door.r <= (byte)(((Color32)ObjColor(Obj.Door)).r * 0.5f + 1f);
                bool separate = !room.IsSeen(1, 0) && !room.IsSeen(2, 0) && !room.IsSeen(3, 0);
                room.SetSeen(1, 0); room.SetSeen(2, 0); room.SetSeen(3, 0);
                Build(room);
                bool walked = Alpha(room.Solid, 1) == 255 && Alpha(room.Open, 2) == 255 && Alpha(room.Open, 3) == 255;
                var seen = new bool[64 * 64];
                bool Mark(int x, int y) { int i = y * 64 + x; bool first = !seen[i]; seen[i] = true; return first; }
                int count = MapExploration.Reveal(64, 64, 20, 20, (x, y) => x == 23, Mark);
                int open = MapExploration.Reveal(64, 64, 20, 20, (x, y) => false, (x, y) => true);
                bool local = count > 0 && count < open && seen[20 * 64 + 23] && !seen[20 * 64 + 24] && !seen[20 * 64 + 29];
                bool still = MapExploration.Reveal(64, 64, 20, 20, (x, y) => x == 23, Mark) == 0;
                bool move = MapExploration.Reveal(64, 64, 30, 20, (x, y) => x == 23, Mark) > 0 && seen[20 * 64 + 29];
                report = $"未买隐藏 {hidden}，买图未探索 30% 亮度、通道不画 {dim}，购买不改探索 {separate}，走过 alpha=255 {walked}，局部/遮挡 {local}，原地不扩散 {still}，移动揭图 {move}";
                return hidden && dim && separate && walked && local && still && move;
            }
            finally
            {
                if (room.Solid != null) UnityEngine.Object.Destroy(room.Solid);
                if (room.Open != null) UnityEngine.Object.Destroy(room.Open);
            }
        }

        /// <summary>Exercise the real v1 backup/migration and v2 save/load in an isolated test slot.</summary>
        public static bool DevExplorationMigration(out string report)
        {
            if (!DevCopy.Verified) { report = "不是隔离测试存档，拒绝写入"; return false; }
            const int slot = 49;
            string path = PathFor(slot);
            Save(slot);
            byte[] current = File.ReadAllBytes(path);
            bool bought = PlayerData.instance.mapCrossroads;
            int seenBefore = 0;
            foreach (var room in rooms.Values) if (!room.FromAtlas && room.Seen != null) foreach (byte b in room.Seen) seenBefore += CountBits(b);
            try
            {
                byte[] raw;
                using (var fs = File.OpenRead(path))
                using (var gz = new GZipStream(fs, CompressionMode.Decompress))
                using (var data = new MemoryStream()) { gz.CopyTo(data); raw = data.ToArray(); }
                using (var data = new MemoryStream(raw))
                using (var reader = new BinaryReader(data))
                {
                    reader.ReadString();
                    long offset = data.Position;
                    using (var writer = new BinaryWriter(data)) { data.Position = offset; writer.Write(1); }
                }
                using (var fs = File.Create(path))
                using (var gz = new GZipStream(fs, CompressionMode.Compress)) gz.Write(raw, 0, raw.Length);
                byte[] legacy = File.ReadAllBytes(path);
                Load(slot);
                int seenAfter = 0;
                foreach (var room in rooms.Values) if (room.Seen != null) foreach (byte b in room.Seen) seenAfter += CountBits(b);
                string backup = path + ".before-exploration-v2.bak";
                bool backed = File.Exists(backup) && Convert.ToBase64String(File.ReadAllBytes(backup)) == Convert.ToBase64String(legacy);
                bool reset = seenAfter == 0;
                bool retained = PlayerData.instance.mapCrossroads == bought;
                // Restoring the pre-test v2 file must retain genuinely walked cells, not reset again.
                File.WriteAllBytes(path, current);
                Load(slot);
                int reloaded = 0;
                foreach (var room in rooms.Values) if (room.Seen != null) foreach (byte b in room.Seen) reloaded += CountBits(b);
                bool persists = reloaded == seenBefore;
                report = $"旧图备份逐字节相同 {backed}，旧探索归零 {reset}，已买地图保留 {retained}，v2 重载探索 {seenBefore}→{reloaded} 保留 {persists}";
                return backed && reset && retained && persists;
            }
            finally { File.WriteAllBytes(path, current); Load(slot); }
        }
    }
}
