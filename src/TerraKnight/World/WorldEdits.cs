using System.Collections.Generic;

namespace TerraKnight.World
{
    /// <summary>
    /// Every cell the player has changed, per scene, for the loaded save slot. A scene's grid is
    /// rebuilt from the original terrain each time it loads, then these edits are replayed.
    /// </summary>
    internal static class WorldEdits
    {
        const byte DugFlag = 0x80;

        static readonly Dictionary<string, Dictionary<int, ushort>> scenes = new Dictionary<string, Dictionary<int, ushort>>();

        public static IEnumerable<KeyValuePair<string, Dictionary<int, ushort>>> All => scenes;

        public static void Clear() => scenes.Clear();

        // Live TerrainRegions retain references to the per-scene dictionaries.
        public static void ClearInPlace()
        {
            foreach (var edits in scenes.Values) edits.Clear();
        }

        public static Dictionary<int, ushort> For(string scene)
        {
            scene = scene ?? "";
            if (!scenes.TryGetValue(scene, out var edits))
            {
                edits = new Dictionary<int, ushort>();
                scenes[scene] = edits;
            }
            return edits;
        }

        /// <summary>
        /// The kind's low four bits in bits 0-3, shape in bits 4-6, dug in bit 7, the kind's high four bits in bits 8-11 (the
        /// areas' blocks, CellKind 32 on); the low byte alone is what a save keeps of the first eleven kinds (older saves
        /// read back as flat).
        /// </summary>
        public static ushort Pack(CellKind kind, bool dug, int slope = 0) =>
            (ushort)(((byte)kind & 0x0F) | (slope & 7) << 4 | (dug ? DugFlag : 0) | ((byte)kind >> 4) << 8);

        public static void Unpack(ushort value, out CellKind kind, out bool dug) => Unpack(value, out kind, out dug, out _);

        public static void Unpack(ushort value, out CellKind kind, out bool dug, out int slope)
        {
            kind = (CellKind)((value & 0x0F) | ((value >> 8) & 0x0F) << 4);
            // The Ice Rod's ice melts with its bolt; a scene left before it thawed comes back without it.
            if (kind == CellKind.MagicIce) kind = CellKind.Empty;
            slope = (value >> 4) & 7;
            dug = (value & DugFlag) != 0;
        }
    }
}
