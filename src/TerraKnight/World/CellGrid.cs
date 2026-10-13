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
    }

    internal static class Cells
    {
        /// <summary>Kinds that block movement as a full block.</summary>
        public static bool Blocks(CellKind k) =>
            k == CellKind.Original || k == CellKind.Dirt || k == CellKind.Stone || k == CellKind.Wood || k == CellKind.Sand || k == CellKind.MagicIce;
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
        // Platforms only: Tile.slope(), 0 flat, 1 "\" (high on the left), 2 "/" (high on the right).
        readonly byte[] slopes;

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
            slopes[index] = kind == CellKind.Platform ? (byte)slope : (byte)0;
        }

        public CellKind GetAt(int index) => cells[index];
        public bool IsDugAt(int index) => dug[index];
        public int SlopeAt(int index) => slopes[index];

        /// <summary>A platform's slope (0 flat, 1 "\", 2 "/"); 0 for anything else.</summary>
        public int GetSlope(int x, int y) => InBounds(x, y) ? slopes[y * Width + x] : 0;

        /// <summary>WorldGen.SlopeTile on a platform: raises the change even though the kind stays.</summary>
        public void SetSlope(int x, int y, int slope)
        {
            if (!InBounds(x, y)) return;
            int i = y * Width + x;
            if (cells[i] != CellKind.Platform || slopes[i] == slope) return;
            slopes[i] = (byte)slope;
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
            return copy;
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
            CellChanged?.Invoke(x, y);
        }

        public static Vector2Int WorldToCell(Vector2 world) =>
            new Vector2Int(Mathf.FloorToInt(world.x / CellSize), Mathf.FloorToInt(world.y / CellSize));

        public static Vector2 CellCenter(int x, int y) =>
            new Vector2((x + 0.5f) * CellSize, (y + 0.5f) * CellSize);
    }
}
