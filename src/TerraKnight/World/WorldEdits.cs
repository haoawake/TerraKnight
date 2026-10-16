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

        static readonly Dictionary<string, Dictionary<int, byte>> scenes = new Dictionary<string, Dictionary<int, byte>>();

        public static IEnumerable<KeyValuePair<string, Dictionary<int, byte>>> All => scenes;

        public static void Clear() => scenes.Clear();

        // Live TerrainRegions retain references to the per-scene dictionaries.
        public static void ClearInPlace()
        {
            foreach (var edits in scenes.Values) edits.Clear();
        }

        public static Dictionary<int, byte> For(string scene)
        {
            scene = scene ?? "";
            if (!scenes.TryGetValue(scene, out var edits))
            {
                edits = new Dictionary<int, byte>();
                scenes[scene] = edits;
            }
            return edits;
        }

        /// <summary>The kind in bits 0-3, shape in bits 4-6, dug in bit 7 (older saves read back as flat).</summary>
        public static byte Pack(CellKind kind, bool dug, int slope = 0) => (byte)((byte)kind | (slope & 7) << 4 | (dug ? DugFlag : 0));

        public static void Unpack(byte value, out CellKind kind, out bool dug) => Unpack(value, out kind, out dug, out _);

        public static void Unpack(byte value, out CellKind kind, out bool dug, out int slope)
        {
            kind = (CellKind)(value & 0x0F);
            // The Ice Rod's ice melts with its bolt; a scene left before it thawed comes back without it.
            if (kind == CellKind.MagicIce) kind = CellKind.Empty;
            slope = (value >> 4) & 7;
            dug = (value & DugFlag) != 0;
        }
    }
}
