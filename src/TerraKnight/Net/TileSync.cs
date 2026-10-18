using System.Collections.Generic;
using System.IO;
using TerraKnight.World;
using UnityEngine;

namespace TerraKnight.Net
{
    /// <summary>
    /// Terraria's TileManipulation (17): a block dug, placed or turned by anyone is changed for
    /// everyone. Each player's change to a scene's grid (mining, placing, explosions, falling sand,
    /// stairs) goes out once a frame as the scene, the cell (its index in that scene's own grid) and
    /// what it is now; the others make the same change, with its dust where they can see it. A
    /// change to a scene not loaded here is kept in the save's edits, so the hole is there when they
    /// get to it: Hallownest is one world for the players in it.
    /// </summary>
    internal static class TileSync
    {
        static bool hooked, applying;
        static readonly Dictionary<string, List<(int Index, ushort Packed, int Wall)>> pending = new Dictionary<string, List<(int, ushort, int)>>();

        public static int DevSent, DevApplied;

        public static void Reset() => pending.Clear();

        static void Hook()
        {
            if (hooked) return;
            hooked = true;
            TerrainRegion.Edited += OnEdited;
            TerrainRegion.WallEdited += OnEdited;
        }

        static void OnEdited(TerrainRegion region, int x, int y)
        {
            if (applying || WorldSync.Applying || !NetSession.Active || !WorldSync.Ready || region == null || string.IsNullOrEmpty(region.SceneName)) return;
            int i = y * region.Grid.Width + x;
            if (!pending.TryGetValue(region.SceneName, out var list)) pending[region.SceneName] = list = new List<(int, ushort, int)>();
            list.Add((i, WorldEdits.Pack(region.Grid.GetAt(i), region.Grid.IsDugAt(i), region.Grid.SlopeAt(i)), region.Grid.WallAt(i)));
        }

        /// <summary>Once a frame: this frame's changes, a message per scene.</summary>
        public static void Flush()
        {
            Hook();
            if (pending.Count == 0) return;
            foreach (var kv in pending)
            {
                var list = kv.Value;
                for (int start = 0; start < list.Count; start += 6000)
                {
                    int n = Mathf.Min(6000, list.Count - start);
                    var p = NetSession.Mine(Msg.TileChange);
                    p.W.Write(kv.Key);
                    p.W.Write((ushort)n);
                    for (int k = start; k < start + n; k++)
                    {
                        p.W.Write(list[k].Index);
                        p.W.Write(list[k].Packed);
                        p.W.Write(list[k].Wall);
                    }
                    // After the cells, the torches' styles among them (World.RegionalTorches): a count, then index and style.
                    var region = TerrainSystem.Find(kv.Key);
                    var torches = new List<(int, byte)>();
                    for (int k = start; k < start + n; k++)
                    {
                        WorldEdits.Unpack(list[k].Packed, out var kind, out _);
                        if (kind == CellKind.Torch && region != null) torches.Add((list[k].Index, (byte)region.Grid.TorchStyleAt(list[k].Index)));
                    }
                    p.W.Write((ushort)torches.Count);
                    foreach (var (index, style) in torches) { p.W.Write(index); p.W.Write(style); }
                    NetSession.Send(p.Done());
                    DevSent += n;
                }
            }
            pending.Clear();
        }

        public static void Receive(BinaryReader r)
        {
            Hook();
            string scene = r.ReadString();
            int n = r.ReadUInt16();
            var region = TerrainSystem.Find(scene);
            var hero = HeroController.SilentInstance;
            Vector2 heroAt = hero != null ? (Vector2)hero.transform.position : Vector2.zero;
            int dust = 0;
            for (int k = 0; k < n; k++)
            {
                int i = r.ReadInt32();
                ushort packed = r.ReadUInt16();
                int wall = r.ReadInt32();
                WorldEdits.Unpack(packed, out var kind, out bool dug, out int slope);
                if (region == null || i < 0 || i >= region.Grid.Width * region.Grid.Height)
                {
                    // Not loaded here: kept with the save's edits of that scene, replayed when it loads.
                    if (i >= 0) { WorldEdits.For(scene)[i] = packed; WorldWalls.For(scene)[i] = wall; }
                    continue;
                }
                int x = i % region.Grid.Width, y = i / region.Grid.Width;
                var before = region.Grid.GetAt(i);
                applying = true;
                try
                {
                    region.Grid.ApplyNetwork(i, kind, dug, slope);
                    region.Grid.SetWall(x, y, wall);
                }
                finally
                {
                    applying = false;
                }
                DevApplied++;
                // WorldGen.KillTile / PlaceTile on every client: the block's dust where it changed, a sound if near.
                if (dust < 24)
                {
                    var cell = new Vector2Int(x + region.CellOffset.x, y + region.CellOffset.y);
                    var at = CellGrid.CellCenter(cell.x, cell.y);
                    if ((at - heroAt).sqrMagnitude < 30f * 30f)
                    {
                        TerraMod.MineDust(cell, kind == CellKind.Empty ? before : kind, kind == CellKind.Empty ? 6 : 2);
                        if (dust == 0) TerraMod.Instance?.PlaySound("Dig_" + Random.Range(0, 3), 0.6f);
                        dust++;
                    }
                }
            }
            // The torches' styles, from a sender that has them.
            if (r.BaseStream.Position + 2 > r.BaseStream.Length) return;
            int styles = r.ReadUInt16();
            applying = true;
            try
            {
                for (int k = 0; k < styles; k++)
                {
                    int i = r.ReadInt32();
                    int style = r.ReadByte();
                    RegionalTorches.SetNet(scene, i, style);
                }
            }
            finally
            {
                applying = false;
            }
        }
    }
}
