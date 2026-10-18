using System;
using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.World
{
    /// <summary>What occupies one grid cell.</summary>
    internal enum CellKind : byte
    {
        Empty = 0,
        /// <summary>The scene's own terrain, untouched.</summary>
        Original = 1,
        Dirt = 2,
        Stone = 3,
        Wood = 4,
        /// <summary>One-way: stood on from above, jumped through from below, dropped through holding down.</summary>
        Platform = 5,
        /// <summary>Falls while nothing holds it up.</summary>
        Sand = 6,
        /// <summary>Lights the area; nothing collides with it.</summary>
        Torch = 7,
        /// <summary>Climbed holding up or down; nothing collides with it.</summary>
        Rope = 8,
        /// <summary>The Ice Rod's block (tile 127): solid until its bolt thaws it; never kept in the save.</summary>
        MagicIce = 9,
        /// <summary>Meteorite (tile 37, World.Meteorites): a 50% pickaxe's, glowing, burning to the touch.</summary>
        Meteorite = 10,
        /// <summary>Each Hallownest area's own block (World/AreaBlocks.cs): 24 kinds from here, in that file's order.</summary>
        AreaBlockFirst = 32,
        AreaBlockLast = 55,
    }

    internal static class Cells
    {
        /// <summary>Kinds that block movement as a full block.</summary>
        public static bool Blocks(CellKind k) =>
            k == CellKind.Original || k == CellKind.Dirt || k == CellKind.Stone || k == CellKind.Wood || k == CellKind.Sand || k == CellKind.MagicIce
            || k == CellKind.Meteorite || IsAreaBlock(k);

        /// <summary>An area's own block (World/AreaBlocks.cs).</summary>
        public static bool IsAreaBlock(CellKind k) => k >= CellKind.AreaBlockFirst && k <= CellKind.AreaBlockLast;
    }

    /// <summary>
    /// The editable grid laid over one scene. Cells are CellSize world units square and aligned
    /// to the world origin, so the game's 1-unit tilemap maps onto exactly 2x2 cells.
    /// </summary>
    internal sealed class CellGrid
    {
        public const float CellSize = 0.5f;

        public readonly int Width;
        public readonly int Height;
        readonly CellKind[] cells;
        // Cells of the original terrain that were dug out; drawn with a Terraria wall behind them.
        readonly bool[] dug;
        // Original cells that come from the scene's hand-placed colliders rather than its tilemap:
        // the scene's own art draws them, so the grid's black fill leaves them alone.
        readonly bool[] art;
        // Tile shape: 0 full/flat, 1 "\" (high on the left), 2 "/" (high on the right).
        readonly byte[] slopes;

        readonly Dictionary<int, int> walls = new Dictionary<int, int>();
        public event Action<int, int> WallChanged;
        public int WallAt(int index) => walls.TryGetValue(index, out var wall) ? wall : -1;
        public int GetWall(int x, int y) => InBounds(x, y) ? WallAt(y * Width + x) : -1;
        public bool HasWall(int x, int y) => InBounds(x, y) && (GetWall(x, y) > 0 || (GetWall(x, y) < 0 && IsDug(x, y)));
        public void RestoreWall(int index, int wall)
        {
            if (index < 0 || index >= cells.Length) return;
            if (wall < 0) walls.Remove(index); else walls[index] = wall;
        }
        public void SetWall(int x, int y, int wall)
        {
            if (!InBounds(x, y) || GetWall(x, y) == wall) return;
            RestoreWall(y * Width + x, wall);
            WallChanged?.Invoke(x, y);
        }
        public void ResetWalls()
        {
            var indices = new List<int>(walls.Keys);
            foreach (int i in indices) SetWall(i % Width, i / Width, -1);
        }

        // A torch cell's regional style (World.RegionalTorches, 1..48; none the plain torch). It stays after the torch is
        // mined (what it gives back reads it) until another torch is placed there.
        readonly Dictionary<int, byte> torchStyles = new Dictionary<int, byte>();
        public int TorchStyleAt(int index) => torchStyles.TryGetValue(index, out var s) ? s : 0;
        public int GetTorchStyle(int x, int y) => InBounds(x, y) ? TorchStyleAt(y * Width + x) : 0;
        public void RestoreTorchStyle(int index, int style)
        {
            if (index < 0 || index >= cells.Length) return;
            if (style <= 0) torchStyles.Remove(index); else torchStyles[index] = (byte)style;
        }
        /// <summary>A cell's torch style changed: a torch there is drawn again (and the change recorded) as any change is.</summary>
        public void SetTorchStyle(int x, int y, int style)
        {
            if (!InBounds(x, y) || GetTorchStyle(x, y) == Math.Max(0, style)) return;
            RestoreTorchStyle(y * Width + x, style);
            if (cells[y * Width + x] == CellKind.Torch) CellChanged?.Invoke(x, y);
        }
        public void ResetTorchStyles()
        {
            var indices = new List<int>(torchStyles.Keys);
            foreach (int i in indices) SetTorchStyle(i % Width, i / Width, 0);
        }

        public event Action<int, int> CellChanged;

        public CellGrid(int width, int height)
        {
            Width = width;
            Height = height;
            cells = new CellKind[width * height];
            dug = new bool[width * height];
            art = new bool[width * height];
            slopes = new byte[width * height];
        }

        /// <summary>Marks an empty cell as solid scene terrain drawn by the scene's art, during setup.</summary>
        public void InitArt(int x, int y)
        {
            int i = y * Width + x;
            if (cells[i] != CellKind.Empty) return;
            cells[i] = CellKind.Original;
            art[i] = true;
        }

        public bool IsArt(int x, int y) => InBounds(x, y) && art[y * Width + x];

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        /// <summary>
        /// Areas this grid gives up when joined to a neighbour: past a joined gate, over the rows
        /// the neighbour covers, the neighbour's cells take over, so this grid neither collides,
        /// draws nor digs there.
        /// </summary>
        readonly List<RectInt> cuts = new List<RectInt>();
        public IReadOnlyList<RectInt> Cuts => cuts;

        /// <summary>Replaces the cut areas; false when they were already the same.</summary>
        public bool SetCuts(IEnumerable<RectInt> areas)
        {
            var next = new List<RectInt>(areas);
            if (next.Count == cuts.Count && next.TrueForAll(cuts.Contains)) return false;
            cuts.Clear();
            cuts.AddRange(next);
            return true;
        }

        public bool InClip(int x, int y)
        {
            if (!InBounds(x, y)) return false;
            for (int i = 0; i < cuts.Count; i++)
                if (cuts[i].Contains(new Vector2Int(x, y))) return false;
            return true;
        }

        public CellKind Get(int x, int y) => InBounds(x, y) ? cells[y * Width + x] : CellKind.Original;

        public bool IsSolid(int x, int y) => Cells.Blocks(Get(x, y));

        public bool IsDug(int x, int y) => InBounds(x, y) && dug[y * Width + x];

        /// <summary>Cells where the game's own terrain art is cut away: exactly the dug cells.</summary>
        public bool IsCut(int x, int y) => IsDug(x, y);

        /// <summary>Sets a cell during scene setup without raising change events.</summary>
        public void Init(int x, int y, CellKind kind) => cells[y * Width + x] = kind;

        /// <summary>Restores a saved cell during scene setup without raising change events.</summary>
        public void Restore(int index, CellKind kind, bool wasDug, int slope = 0)
        {
            if (index < 0 || index >= cells.Length) return;
            cells[index] = kind;
            dug[index] = wasDug;
            slopes[index] = ValidShape(kind, slope);
        }

        CellKind[] networkBase;
        public void CaptureNetworkBase() => networkBase = (CellKind[])cells.Clone();

        public void ResetNetworkEdits()
        {
            if (networkBase == null) return;
            for (int i = 0; i < cells.Length; i++) ApplyNetwork(i, networkBase[i], false, 0);
        }

        public void ApplyNetwork(int index, CellKind kind, bool wasDug, int slope)
        {
            if (index < 0 || index >= cells.Length) return;
            byte s = ValidShape(kind, slope);
            if (cells[index] == kind && dug[index] == wasDug && slopes[index] == s) return;
            Restore(index, kind, wasDug, slope);
            CellChanged?.Invoke(index % Width, index / Width);
        }

        public CellKind GetAt(int index) => cells[index];
        public bool IsDugAt(int index) => dug[index];
        public int SlopeAt(int index) => slopes[index];

        /// <summary>Shape 5 is a lower half brick; 1..4 are Terraria slope values.</summary>
        public const int HalfBrick = 5;
        static byte ValidShape(CellKind kind, int shape) =>
            kind == CellKind.Platform ? (byte)(shape == 1 || shape == 2 || shape == HalfBrick ? shape : 0) :
            Cells.Blocks(kind) && kind != CellKind.Original && shape >= 0 && shape <= HalfBrick ? (byte)shape : (byte)0;

        public bool IsFullSolid(int x, int y) => IsSolid(x, y) && GetSlope(x, y) == 0;

        public int GetSlope(int x, int y) => InBounds(x, y) ? slopes[y * Width + x] : 0;

        /// <summary>WorldGen.SlopeTile: raises the change even though the kind stays.</summary>
        public void SetSlope(int x, int y, int slope)
        {
            if (!InBounds(x, y)) return;
            int i = y * Width + x;
            byte shape = ValidShape(cells[i], slope);
            if (slopes[i] == shape) return;
            slopes[i] = shape;
            CellChanged?.Invoke(x, y);
        }

        /// <summary>An independent copy of the cells, for a snapshot of the scene being left.</summary>
        public CellGrid Clone()
        {
            var copy = new CellGrid(Width, Height);
            Array.Copy(cells, copy.cells, cells.Length);
            Array.Copy(dug, copy.dug, dug.Length);
            Array.Copy(art, copy.art, art.Length);
            Array.Copy(slopes, copy.slopes, slopes.Length);
            foreach (var wall in walls) copy.walls[wall.Key] = wall.Value;
            foreach (var style in torchStyles) copy.torchStyles[style.Key] = style.Value;
            copy.networkBase = networkBase != null ? (CellKind[])networkBase.Clone() : null;
            return copy;
        }

        /// <summary>
        /// A cell of open air holding a spike's or a thorn's art dug out (the art cut away there, no dug wall drawn behind it):
        /// dug, with its wall explicitly none; raises the changes so the edit is kept.
        /// </summary>
        public void CutOpenArt(int x, int y)
        {
            if (!InBounds(x, y)) return;
            int i = y * Width + x;
            if (cells[i] != CellKind.Empty || dug[i]) return;
            dug[i] = true;
            if (WallAt(i) < 0) RestoreWall(i, 0);
            CellChanged?.Invoke(x, y);
            WallChanged?.Invoke(x, y);
        }

        public void Set(int x, int y, CellKind kind)
        {
            if (!InBounds(x, y)) return;
            int i = y * Width + x;
            if (cells[i] == kind) return;
            if (cells[i] == CellKind.Original && kind == CellKind.Empty) dug[i] = true;
            cells[i] = kind;
            // WorldGen.PlaceTile clears the slope: a new platform starts flat.
            slopes[i] = 0;
            // A torch set down starts as the plain one; the item placing it gives its style (RegionalTorches.Placed).
            if (kind == CellKind.Torch) torchStyles.Remove(i);
            CellChanged?.Invoke(x, y);
        }

        public static Vector2Int WorldToCell(Vector2 world) =>
            new Vector2Int(Mathf.FloorToInt(world.x / CellSize), Mathf.FloorToInt(world.y / CellSize));

        public static Vector2 CellCenter(int x, int y) =>
            new Vector2((x + 0.5f) * CellSize, (y + 0.5f) * CellSize);
    }
}
