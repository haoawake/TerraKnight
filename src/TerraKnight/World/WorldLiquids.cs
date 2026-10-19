using System.Collections.Generic;

namespace TerraKnight.World
{
    /// <summary>
    /// The liquid each scene holds, kept for the save and for the scene's next visit: cell index → amount (low byte) and
    /// kind (high byte). A loaded scene's grid is the live copy; it is written here when the scene goes and before a save.
    /// </summary>
    internal static class WorldLiquids
    {
        static readonly Dictionary<string, Dictionary<int, ushort>> scenes = new Dictionary<string, Dictionary<int, ushort>>();
        public static IEnumerable<KeyValuePair<string, Dictionary<int, ushort>>> All => scenes;

        public static Dictionary<int, ushort> For(string scene)
        {
            scene = scene ?? "";
            if (!scenes.TryGetValue(scene, out var cells)) scenes[scene] = cells = new Dictionary<int, ushort>();
            return cells;
        }

        public static ushort Pack(int amount, LiquidKind kind) => (ushort)((amount & 0xFF) | ((int)kind << 8));
        public static void Unpack(ushort v, out int amount, out LiquidKind kind)
        {
            amount = v & 0xFF;
            kind = (LiquidKind)(v >> 8);
        }

        /// <summary>A grid's liquid as the scene's record (every cell holding some).</summary>
        public static void Capture(string scene, CellGrid grid)
        {
            if (grid == null) return;
            var cells = For(scene);
            cells.Clear();
            if (!grid.HasLiquids) return;
            var amount = grid.LiquidArray;
            var kind = grid.LiquidKindArray;
            for (int i = 0; i < amount.Length; i++)
                if (amount[i] > 0) cells[i] = Pack(amount[i], (LiquidKind)kind[i]);
        }

        /// <summary>Every loaded scene's grid into its record (before a save).</summary>
        public static void CaptureLive()
        {
            foreach (var t in TerrainSystem.All)
                if (t != null && t.Region != null) t.Region.CaptureLiquids();
        }


        /// <summary>A scene's record into a freshly rasterised grid.</summary>
        public static void Replay(CellGrid grid, string scene)
        {
            if (!scenes.TryGetValue(scene ?? "", out var cells)) return;
            foreach (var c in cells)
            {
                Unpack(c.Value, out int amount, out var kind);
                if (amount > 0 && !Cells.Blocks(grid.GetAt(c.Key))) grid.RestoreLiquid(c.Key, amount, kind);
            }
        }

        public static void Clear() => scenes.Clear();
    }
}
