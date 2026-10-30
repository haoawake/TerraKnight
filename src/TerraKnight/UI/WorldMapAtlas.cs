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

        internal static void DevExtendAtlas() => LoadAtlas();
        internal static void DevPlaceAtlasRoom(string name, Vector2Int at) => layout[name] = at;
        internal static bool DevAtlasContains(string name) => atlas.ContainsKey(name);

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

        /// <summary>
        /// A scene's room to walk on the world atlas: its open cells (air, water, platforms) with rock somewhere left, right,
        /// above and below them in the scene (the void round a scene's walls not counted); 0 when the atlas lacks it.
        /// </summary>
        internal static int OpenCells(string scene)
        {
            LoadAtlas();
            if (scene == null || !atlas.TryGetValue(scene, out var room) || room.Tiles == null) return 0;
            int w = room.W, h = room.H;
            bool Solid(int x, int y) => IsSolid((Tile)room.Tiles[y * w + x]);
            var left = new bool[w * h];
            var below = new bool[w * h];
            var above = new bool[w * h];
            for (int y = 0; y < h; y++)
            {
                bool seen = false;
                for (int x = 0; x < w; x++) { seen |= Solid(x, y); left[y * w + x] = seen; }
            }
            for (int x = 0; x < w; x++)
            {
                bool seen = false;
                for (int y = 0; y < h; y++) { seen |= Solid(x, y); below[y * w + x] = seen; }
                seen = false;
                for (int y = h - 1; y >= 0; y--) { seen |= Solid(x, y); above[y * w + x] = seen; }
            }
            int n = 0;
            for (int y = 0; y < h; y++)
            {
                bool right = false;
                for (int x = w - 1; x >= 0; x--)
                {
                    int i = y * w + x;
                    var t = (Tile)room.Tiles[i];
                    if (IsSolid(t)) { right = true; continue; }
                    if (t != Tile.None && right && left[i] && below[i] && above[i]) n++;
                }
            }
            return n;
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

        /// <summary>The game's map of each area, as Cornifer and Iselda sell them (PlayerData.mapCrossroads...), by the mod's area names.</summary>
        static readonly Dictionary<string, string> AreaMaps = new Dictionary<string, string>
        {
            ["国王山道"] = "mapDirtmouth", ["德特茅斯"] = "mapDirtmouth", ["遗忘十字路"] = "mapCrossroads", ["苍绿之径"] = "mapGreenpath",
            ["雾之峡谷"] = "mapFogCanyon", ["王后花园"] = "mapRoyalGardens", ["真菌荒地"] = "mapFungalWastes", ["泪水之城"] = "mapCity",
            ["皇家水道"] = "mapWaterways", ["水晶山峰"] = "mapMines", ["深巢"] = "mapDeepnest", ["野兽巢穴"] = "mapDeepnest",
            ["呼啸悬崖"] = "mapCliffs", ["王国边缘"] = "mapOutskirts", ["蜂巢"] = "mapOutskirts", ["愚人斗兽场"] = "mapOutskirts",
            ["安息之地"] = "mapRestingGrounds", ["远古盆地"] = "mapAbyss",
        };

        static string chartedKey = "";
        public static int DevCharted;

        /// <summary>
        /// The areas whose map the save has bought. A new game already has mapDirtmouth (PlayerData.SetupNewPlayerData):
        /// the game shows King's Pass and Dirtmouth only once there is a map at all (hasMap, the first one bought).
        /// </summary>
        static HashSet<string> BoughtAreas()
        {
            var set = new HashSet<string>();
            var pd = PlayerData.instance;
            if (pd == null) return set;
            foreach (var kv in AreaMaps)
                if (pd.GetBool(kv.Value) && (kv.Value != "mapDirtmouth" || pd.GetBool("hasMap"))) set.Add(kv.Key);
            return set;
        }

        /// <summary>
        /// A bought map (Cornifer, Iselda) charts its area as in the game: every room of it on the map,
        /// those not visited yet from the world atlas, unexplored cells at 30% opacity. Its Life Crystals stay
        /// hidden until the player has seen their spot.
        /// </summary>
        static void ChartBought()
        {
            if (!Enabled) return;
            var bought = BoughtAreas();
            var names = new List<string>(bought);
            names.Sort(System.StringComparer.Ordinal);
            string key = string.Join(",", names);
            if (key != chartedKey)
            {
                chartedKey = key;
                if (bought.Count > 0)
                {
                    LoadAtlas();
                    int added = 0;
                    foreach (var kv in atlas)
                    {
                        if (!bought.Contains(Areas.AreaName(kv.Key))) continue;
                        if (rooms.TryGetValue(kv.Key, out var have) && have.Captured && have.Tiles != null) continue;
                        var a = kv.Value;
                        var room = new Room
                        {
                            Name = a.Name, W = a.W, H = a.H, At = a.At, Tiles = (byte[])a.Tiles.Clone(), Wets = (byte[])a.Wets.Clone(), Seen = new byte[a.Seen.Length],
                            Captured = true, FromAtlas = true, Area = Areas.AreaName(a.Name), Hue = Player.OpeningPortal.AreaColor(Areas.AreaName(a.Name)),
                            RevealAll = have != null && have.RevealAll,
                        };
                        room.Things.AddRange(a.Things);
                        rooms[kv.Key] = room;
                        added++;
                    }
                    Plugin.Log.LogInfo($"[地图] 买到的地图：{key}，从世界图集补上 {added} 个没去过的场景");
                }
            }
            foreach (var room in rooms.Values)
            {
                bool charted = bought.Contains(Areas.AreaName(room.Name));
                if (room.Charted == charted) continue;
                room.Charted = charted;
                room.Dirty = true;
                if (charted) DevCharted++;
            }
        }

        public static int DevAtlasCount { get { LoadAtlas(); return atlas.Count; } }

        /// <summary>Dev: an area's rooms on the map: how many, charted, from the atlas, drawn at all.</summary>
        public static string DevChart(string area)
        {
            int n = 0, charted = 0, fromAtlas = 0, shown = 0;
            foreach (var room in rooms.Values)
            {
                if (Areas.AreaName(room.Name) != area) continue;
                n++;
                if (room.Charted) charted++;
                if (room.FromAtlas) fromAtlas++;
                if (room.Captured && room.AnyShown && Shown(room)) shown++;
            }
            int inAtlas = 0;
            LoadAtlas();
            foreach (var k in atlas.Keys) if (Areas.AreaName(k) == area) inAtlas++;
            return $"{area}：地图上 {n} 个场景（图集里 {inAtlas} 个），买到地图的 {charted}，从图集补的 {fromAtlas}，画出来的 {shown}";
        }

        /// <summary>Dev: each placed Life Crystal of an area: its scene, whether its spot was seen, whether the map draws it.</summary>
        public static string DevCrystals(string area, out int drawnUnseen)
        {
            drawnUnseen = 0;
            var sb = new System.Text.StringBuilder();
            foreach (var (scene, local) in LifeCrystals.Standing())
            {
                if (Areas.AreaName(scene) != area || !rooms.TryGetValue(scene, out var room) || room.Seen == null) continue;
                int x = Mathf.FloorToInt(local.x / C + 0.5f) - 1, y = Mathf.FloorToInt(local.y / C + 0.5f);
                if (x < 0 || y < 0 || x >= room.W || y >= room.H) continue;
                bool seen = room.IsSeen(x, y);
                // What the room's picture actually has there.
                if (room.Dirty || room.Open == null) Build(room);
                Color32 px = room.Open.GetPixel(x, y), want = ObjColor(Obj.LifeCrystal);
                bool drawn = px.r == want.r && px.g == want.g && px.b == want.b && px.a > 0;
                if (drawn && !seen) drawnUnseen++;
                sb.Append($" {scene}(看到 {seen}，画出 {drawn}，房间已买 {room.Charted})");
            }
            return sb.ToString();
        }

        public static int DevChartCount(string area)
        {
            int n = 0;
            foreach (var room in rooms.Values) if (room.Charted && Areas.AreaName(room.Name) == area) n++;
            return n;
        }

        /// <summary>Dev: centre the open map on a scene's middle.</summary>
        public static void DevCentreScene(string scene)
        {
            if (rooms.TryGetValue(scene, out var room)) pan = new Vector2(room.At.x + room.DrawnW / 2f, room.At.y + room.DrawnH / 2f);
            else if (layout.TryGetValue(scene, out var at)) pan = at;
        }
    }
}
