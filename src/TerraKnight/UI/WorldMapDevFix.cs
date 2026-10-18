using System.Collections.Generic;
using System.IO;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.UI
{
    /// <summary>Dev checks for the map's layout, its live cells, its marks (suite fx_17mapfix).</summary>
    internal static partial class WorldMap
    {
        /// <summary>Dev: a room cell's state and what its two pictures hold there now (nothing made again first).</summary>
        public static string DevCellPixels(string scene, int x, int y, out Color32 solid, out Color32 open)
        {
            solid = open = default;
            if (scene == null || !rooms.TryGetValue(scene, out var room) || room.Tiles == null) return "没有这个房间";
            if (x < 0 || y < 0 || x >= room.W || y >= room.H) return "房间外";
            int i = y * room.W + x;
            if (room.Solid != null) { solid = room.Solid.GetPixel(x, y); open = room.Open.GetPixel(x, y); }
            return $"{(Tile)room.Tiles[i]}，水/危险 {(Wet)room.Wets[i]}→{WetOf(room, i)}，见过 {room.IsSeen(x, y)}，待画 {room.Pending.Count}，要重画 {room.Dirty}，" +
                   $"实心图 ({solid.r},{solid.g},{solid.b},{solid.a}) 空心图 ({open.r},{open.g},{open.b},{open.a})";
        }

        /// <summary>Dev: what Build would draw at a room's cell (solid and open pictures).</summary>
        public static bool DevExpected(string scene, int x, int y, out Color32 solid, out Color32 open)
        {
            solid = open = default;
            if (scene == null || !rooms.TryGetValue(scene, out var room) || room.Tiles == null || x < 0 || y < 0 || x >= room.W || y >= room.H) return false;
            CellColors(room, x, y, out solid, out open);
            return true;
        }

        /// <summary>Dev: the map's colour of a kind of cell ("dug", "dirt", "spikes", "air").</summary>
        public static Color32 DevColor(string scene, string what)
        {
            rooms.TryGetValue(scene ?? "", out var room);
            switch (what)
            {
                case "dug": return TileColor(room, Tile.Dug);
                case "dirt": return TileColor(room, Tile.Dirt);
                case "spikes": return WetColor(Wet.Spikes);
                default: return room != null ? (Color32)TileColor(room, Tile.Air) : default;
            }
        }

        /// <summary>Dev: the full map's mouse at a GUI point (null: the real one).</summary>
        public static void DevMouse(Vector2? gui) { devMouse = gui; TerraCursor.DevAt = gui; }

        /// <summary>Dev: a room's open cells under spikes, not dug yet (its own cells).</summary>
        public static List<Vector2Int> DevSpikeCells(string scene)
        {
            var list = new List<Vector2Int>();
            if (scene == null || !rooms.TryGetValue(scene, out var room) || room.Tiles == null) return list;
            for (int i = 0; i < room.Tiles.Length; i++)
                if ((Wet)room.Wets[i] == Wet.Spikes && !IsSolid((Tile)room.Tiles[i]) && (Tile)room.Tiles[i] != Tile.Dug) list.Add(new Vector2Int(i % room.W, i / room.W));
            return list;
        }

        /// <summary>A room's insides: open cells with its own rock somewhere left, right, above and below them.</summary>
        static bool[] Insides(Room room)
        {
            int w = room.W, h = room.H;
            var solid = new bool[w * h];
            for (int i = 0; i < solid.Length; i++) solid[i] = IsSolid((Tile)room.Tiles[i]);
            var left = new bool[w * h]; var below = new bool[w * h]; var above = new bool[w * h]; var inside = new bool[w * h];
            for (int y = 0; y < h; y++) { bool s = false; for (int x = 0; x < w; x++) { s |= solid[y * w + x]; left[y * w + x] = s; } }
            for (int x = 0; x < w; x++)
            {
                bool s = false;
                for (int y = 0; y < h; y++) { s |= solid[y * w + x]; below[y * w + x] = s; }
                s = false;
                for (int y = h - 1; y >= 0; y--) { s |= solid[y * w + x]; above[y * w + x] = s; }
            }
            for (int y = 0; y < h; y++)
            {
                bool right = false;
                for (int x = w - 1; x >= 0; x--)
                {
                    int i = y * w + x;
                    if (solid[i]) { right = true; continue; }
                    inside[i] = room.Tiles[i] != (byte)Tile.None && right && left[i] && below[i] && above[i];
                }
            }
            return inside;
        }

        /// <summary>
        /// Dev: every scene of the world atlas where the layout puts it, per layer: the cells of one room's inside that lie
        /// on another's inside, the pairs of rooms that do, the worst; the rooms named, each one's pairs.
        /// </summary>
        public static string DevOverlap(out int doubledShallow, out int pairsShallow, out int bigShallow, params string[] focus)
        {
            LoadAtlas();
            doubledShallow = pairsShallow = bigShallow = 0;
            var ins = new Dictionary<string, bool[]>();
            foreach (var kv in atlas) if (layout.ContainsKey(kv.Key) && kv.Value.Tiles != null) ins[kv.Key] = Insides(kv.Value);
            var sb = new System.Text.StringBuilder();
            var focusSet = new HashSet<string>(focus);
            foreach (bool deep in new[] { false, true })
            {
                var names = new List<string>();
                foreach (var n in ins.Keys) if (IsDeep(n) == deep) names.Add(n);
                names.Sort(System.StringComparer.Ordinal);
                long doubled = 0;
                int pairs = 0, big = 0;
                var worst = new List<(int, string)>();
                for (int a = 0; a < names.Count; a++)
                {
                    var A = atlas[names[a]]; var at = layout[names[a]]; var ia = ins[names[a]];
                    for (int b = a + 1; b < names.Count; b++)
                    {
                        var B = atlas[names[b]]; var bt = layout[names[b]];
                        int x0 = Mathf.Max(at.x, bt.x), x1 = Mathf.Min(at.x + A.W, bt.x + B.W), y0 = Mathf.Max(at.y, bt.y), y1 = Mathf.Min(at.y + A.H, bt.y + B.H);
                        if (x0 >= x1 || y0 >= y1) continue;
                        var ib = ins[names[b]];
                        int n = 0;
                        for (int y = y0; y < y1; y++)
                            for (int x = x0; x < x1; x++)
                                if (ia[(y - at.y) * A.W + x - at.x] && ib[(y - bt.y) * B.W + x - bt.x]) n++;
                        if (n == 0) continue;
                        doubled += n;
                        pairs++;
                        if (n > 200) big++;
                        worst.Add((n, names[a] + "×" + names[b]));
                        if (focusSet.Contains(names[a]) || focusSet.Contains(names[b])) sb.Append($" [关注 {names[a]}×{names[b]} {n} 格]");
                    }
                }
                worst.Sort((p, q) => q.Item1.CompareTo(p.Item1));
                sb.Append($"｜{(deep ? "深层" : "浅层")}：两间房里面叠在一起的格子 {doubled}，这样的房间对 {pairs}（超过 200 格的 {big}），最重的");
                for (int k = 0; k < Mathf.Min(6, worst.Count); k++) sb.Append($" {worst[k].Item2} {worst[k].Item1}");
                if (!deep) { doubledShallow = (int)doubled; pairsShallow = pairs; bigShallow = big; }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Dev: a loaded scene's open map cells where a solid collider of the game's terrain stands that isn't the mod's own
        /// (a gate, a floor, a wall the grid doesn't have), and its solid map cells where nothing solid is; the colliders' names.
        /// </summary>
        public static string DevPhantoms(string scene, out int openButSolid)
        {
            openButSolid = 0;
            var region = TerrainSystem.Find(scene);
            if (region == null || !rooms.TryGetValue(scene, out var room) || room.Tiles == null || room.W != region.Grid.Width) return "场景没载入或没采集";
            var names = new Dictionary<string, int>();
            int mask = 1 << 8;
            for (int y = 0; y < room.H; y++)
                for (int x = 0; x < room.W; x++)
                {
                    var t = (Tile)room.Tiles[y * room.W + x];
                    if (IsSolid(t) || t == Tile.Platform) continue;
                    var p = CellGrid.CellCenter(region.CellOffset.x + x, region.CellOffset.y + y);
                    foreach (var col in Physics2D.OverlapPointAll(p, mask))
                    {
                        if (col == null || col.isTrigger || col.name.StartsWith("Terra ")) continue;
                        openButSolid++;
                        string key = col.name + (col.transform.parent != null ? "<" + col.transform.parent.name : "");
                        names[key] = names.TryGetValue(key, out int k) ? k + 1 : 1;
                        break;
                    }
                }
            var sb = new System.Text.StringBuilder($"{scene}：地图上空着、游戏里有实心碰撞的格子 {openButSolid}");
            foreach (var kv in names) sb.Append($" {kv.Key}×{kv.Value}");
            return sb.ToString();
        }

        /// <summary>Dev: a room's cells as the map has them, written as a picture (rock grey, open dark, dug brown, spikes red, water blue).</summary>
        public static string DevRoomPicture(string scene, string path)
        {
            if (scene == null || !rooms.TryGetValue(scene, out var room) || room.Tiles == null) return "没有这个房间";
            var tex = new Texture2D(room.W, room.H, TextureFormat.RGBA32, false);
            var px = new Color32[room.W * room.H];
            for (int i = 0; i < px.Length; i++)
            {
                var t = (Tile)room.Tiles[i];
                var w = WetOf(room, i);
                px[i] = IsSolid(t) ? new Color32(150, 150, 160, 255) : w == Wet.Spikes ? new Color32(220, 50, 50, 255) : w == Wet.Water ? new Color32(30, 70, 200, 255)
                    : t == Tile.Dug ? new Color32(110, 80, 50, 255) : t == Tile.Platform ? new Color32(210, 160, 120, 255) : new Color32(12, 12, 20, 255);
            }
            tex.SetPixels32(px);
            tex.Apply(false);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
            return $"{scene} {room.W}x{room.H} → {path}";
        }
    }
}
