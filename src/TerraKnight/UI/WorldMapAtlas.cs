using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>
    /// The world atlas: every scene's cells and things, baked by the dev suite "layout" (it loads
    /// each scene, builds its grid as the terrain system would) into BepInEx/TerraKnight/worldatlas.bin,
    /// from the game's own files on this machine. A creative save shows the whole map from it:
    /// every scene there, all revealed (rooms the save has seen itself keep their own cells).
    /// </summary>
    internal static partial class WorldMap
    {
        static readonly Dictionary<string, Room> atlas = new Dictionary<string, Room>();
        static bool atlasLoaded;
        static string AtlasPath => Path.Combine(BepInEx.Paths.BepInExRootPath, "TerraKnight", "worldatlas.bin");

        /// <summary>Dev (the layout survey): a scene's cells and things into the atlas.</summary>
        public static void BakeScene(string name, CellGrid grid, Vector2Int offset, UnityEngine.SceneManagement.Scene scene)
        {
            if (!layout.TryGetValue(name, out var at)) return;
            var room = new Room { Name = name, W = grid.Width, H = grid.Height, At = at, Tiles = new byte[grid.Width * grid.Height], Wets = new byte[grid.Width * grid.Height], Seen = new byte[(grid.Width * grid.Height + 7) / 8] };
            for (int y = 0; y < grid.Height; y++)
                for (int x = 0; x < grid.Width; x++)
                    room.Tiles[y * grid.Width + x] = (byte)TileOf(grid, x, y);
            CaptureThings(room, offset, scene);
            atlas[name] = room;
        }

        /// <summary>Dev: the atlas written out; where and how big.</summary>
        public static string WriteAtlas()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(AtlasPath));
                using (var fs = File.Create(AtlasPath))
                using (var gz = new GZipStream(fs, CompressionMode.Compress))
                using (var w = new BinaryWriter(gz))
                {
                    w.Write("TKATLAS");
                    w.Write(1);
                    w.Write(atlas.Count);
                    foreach (var room in atlas.Values)
                    {
                        w.Write(room.Name);
                        w.Write(room.W);
                        w.Write(room.H);
                        WriteRle(w, room.Tiles);
                        WriteRle(w, room.Wets);
                        w.Write(room.Things.Count);
                        foreach (var o in room.Things)
                        {
                            w.Write((byte)o.Kind);
                            w.Write(o.Name ?? "");
                            w.Write(o.Cells.x); w.Write(o.Cells.y); w.Write(o.Cells.width); w.Write(o.Cells.height);
                        }
                    }
                }
                return $"{atlas.Count} 个场景，{new FileInfo(AtlasPath).Length / 1024} KB，{AtlasPath}";
            }
            catch (Exception e) { return "写不了：" + e.Message; }
        }

        static void LoadAtlas()
        {
            if (atlasLoaded) return;
            atlasLoaded = true;
            if (!File.Exists(AtlasPath))
            {
                Plugin.Log.LogWarning($"[地图] 没有世界图集（{AtlasPath}），创造模式只显示去过的地方");
                return;
            }
            try
            {
                using (var fs = File.OpenRead(AtlasPath))
                using (var gz = new GZipStream(fs, CompressionMode.Decompress))
                using (var r = new BinaryReader(gz))
                {
                    if (r.ReadString() != "TKATLAS") throw new InvalidDataException("not an atlas");
                    r.ReadInt32();
                    int count = r.ReadInt32();
                    for (int k = 0; k < count; k++)
                    {
                        string name = r.ReadString();
                        int w = r.ReadInt32(), h = r.ReadInt32();
                        var room = new Room { Name = name, W = w, H = h, Tiles = ReadRle(r), Wets = ReadRle(r), Seen = new byte[(w * h + 7) / 8] };
                        int things = r.ReadInt32();
                        for (int i = 0; i < things; i++)
                        {
                            var kind = (Obj)r.ReadByte();
                            string tn = r.ReadString();
                            int x = r.ReadInt32(), y = r.ReadInt32(), tw = r.ReadInt32(), th = r.ReadInt32();
                            room.Things.Add(new Thing { Kind = kind, Name = tn, Cells = new RectInt(x, y, tw, th) });
                        }
                        if (layout.TryGetValue(name, out room.At)) atlas[name] = room;
                    }
                }
                Plugin.Log.LogInfo($"[地图] 读入世界图集：{atlas.Count} 个场景");
            }
            catch (Exception e) { Plugin.Log.LogWarning($"[地图] 世界图集读不了：{e.Message}"); }
        }

        /// <summary>
        /// Creative: the whole world on the map. Every scene of the atlas the save hasn't captured
        /// itself comes from the atlas; every room is revealed, now and when it is captured again.
        /// </summary>
        public static void RevealWorld()
        {
            if (!Enabled) return;
            LoadAtlas();
            foreach (var kv in atlas)
            {
                if (rooms.TryGetValue(kv.Key, out var have) && have.Captured && have.Tiles != null) continue;
                var a = kv.Value;
                rooms[kv.Key] = new Room
                {
                    Name = a.Name, W = a.W, H = a.H, At = a.At, Tiles = (byte[])a.Tiles.Clone(), Wets = (byte[])a.Wets.Clone(), Seen = new byte[a.Seen.Length],
                    Captured = true, FromAtlas = true, Area = Areas.AreaName(a.Name), Hue = Player.OpeningPortal.AreaColor(Areas.AreaName(a.Name)),
                };
                rooms[kv.Key].Things.AddRange(a.Things);
            }
            int shown = 0;
            foreach (var room in rooms.Values)
            {
                room.RevealAll = true;
                if (room.Seen == null) continue;
                for (int i = 0; i < room.Seen.Length; i++) room.Seen[i] = 0xFF;
                room.AnySeen = true;
                room.Dirty = true;
                shown++;
            }
            Plugin.Log.LogInfo($"[地图] 创造模式：整个地图 {shown} 个场景都显示");
        }

        public static int DevAtlasCount { get { LoadAtlas(); return atlas.Count; } }
    }
}
