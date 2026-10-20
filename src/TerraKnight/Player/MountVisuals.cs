using System.Collections.Generic;
using UnityEngine;
namespace TerraKnight.Player
{
    internal static partial class Mounts
    {
        // Exported from this installation's Terraria ArmorIDs and Player.GetHelmetOffsetAddonFromMount.
        static readonly HashSet<int> raptorHelmets = new HashSet<int> { 212, 241, 274, 277, 5, 278, 34, 205, 41, 42, 30, 8, 84, 79, 33, 91, 90, 99, 35, 4, 256, 257, 36, 85, 80, 1, 112, 2, 215, 201, 202, 199, 194, 206, 262, 259, 234, 226, 238, 250, 280, 232, 225, 223, 224, 219, 220, 216, 263, 273, 264, 267, 18, 19, 24, 21, 26, 25, 16, 14, 12, 275, 65, 51, 56, 40, 29, 81, 94, 95, 62, 67, 44, 68, 121, 114, 116, 113, 92, 106, 100, 96, 97, 141, 143, 145, 139, 140, 144, 126, 130, 133, 159, 158, 161, 166, 183, 181, 178, 184, 182, 13, 64, 60, 59, 73, 63, 117, 138, 160, 10, 235, 197, 195, 203, 191, 228, 233, 227, 217, 218, 245, 243, 244, 242, 231, 229, 222, 265, 272, 279, 15, 163, 288, 289, 290 };
        static readonly HashSet<int> raptorFaces = new HashSet<int> { 1, 6, 9, 8, 19, 7, 20, 21, 23 };
        internal static bool RaptorHelmet(int slot) => raptorHelmets.Contains(slot);
        internal static bool RaptorFace(int slot) => raptorFaces.Contains(slot);
        internal static Vector2 RaptorHelmetOffset(int slot)
        {
            switch (slot)
            {
                case 13: case 15: case 16: case 59: case 60: case 63: case 64: case 81: case 126: case 231: case 279: return new Vector2(0f, -2f);
                case 2: case 5: case 10: case 12: case 25: case 84: case 85: case 116: case 117: case 138: case 141: case 143: case 160: case 178: case 183: case 191: case 194: case 197: case 217: case 218: case 233: case 245: case 265: case 274: case 277: return new Vector2(-2f, 0f);
                case 163: case 228: case 229: case 235: return new Vector2(-2f, 2f);
                case 222: case 242: case 243: case 244: return new Vector2(0f, 2f);
                case 73: case 91: case 227: case 280: return new Vector2(-2f, -2f);
                case 195: return new Vector2(-4f, -2f);
                case 241: return new Vector2(-4f, -14f);
                case 272: return new Vector2(-4f, 4f);
                case 203: return new Vector2(0f, -4f);
                case 267: return new Vector2(-2f, -4f);
                default: return Vector2.zero;
            }
        }
        internal static int VisualFrame(MountDef m, Vector2 velocity, bool grounded)
        {
            if (!grounded) return m.FlyCount > 0 ? m.FlyStart + (int)(Time.time*60/Mathf.Max(1,m.FlyDelay)) % m.FlyCount : m.Air;
            if (Mathf.Abs(velocity.x) > .2f && m.WalkCount > 0) return m.WalkStart + (int)(Time.time*60/Mathf.Max(1,m.WalkDelay)) % m.WalkCount;
            return m.Stand;
        }
        internal static Vector2 RaptorHeadOffset(int frame)
        {
            // Player.ApplyHeadOffsetFromMount starts at (23,-8) in screen pixels.
            // Our mount image is centred on the feet, rather than using native XOffset=4.
            var offset = new Vector2(19f, 8f);
            switch (frame)
            {
                case 5: return offset + new Vector2(0f, -4f);
                case 6: return offset + new Vector2(2f, -2f);
                case 7: return offset + new Vector2(2f, 0f);
                case 8: case 9: case 11: case 12: case 13: case 15:
                case 16: case 17: case 18: case 19: case 20: return offset + new Vector2(4f, -4f);
                case 10: case 14: return offset + new Vector2(4f, -2f);
                default: return offset;
            }
        }
    }
}
