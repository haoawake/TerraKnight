using System.Collections.Generic;
using UnityEngine;

namespace TerraKnight.Items
{
    /// <summary>
    /// Item pictures that are animation sheets, their frames stacked top to bottom
    /// (Main.RegisterItemAnimation): the Fallen Star (8 frames of 5 ticks, back and forth), the
    /// souls and lunar wings' frames (4 of 6 ticks), Terraria's food (3 frames that never move:
    /// the first). Whatever draws an item's picture (the inventory, items lying in the world, a
    /// held item) shows the frame of the moment instead of the whole sheet.
    /// </summary>
    internal static class ItemFrames
    {
        readonly struct Anim
        {
            public readonly int Frames, Ticks;
            public readonly bool PingPong;
            public Anim(int frames, int ticks, bool pingPong = false) { Frames = frames; Ticks = ticks; PingPong = pingPong; }
        }

        static readonly Dictionary<int, Anim> byId = Build();

        static Dictionary<int, Anim> Build()
        {
            var d = new Dictionary<int, Anim>
            {
                [3581] = new Anim(4, 4), [3580] = new Anim(4, 6), [75] = new Anim(8, 5, true),
                [575] = new Anim(4, 6), [547] = new Anim(4, 6), [520] = new Anim(4, 6), [548] = new Anim(4, 6), [521] = new Anim(4, 6), [549] = new Anim(4, 6),
                [3453] = new Anim(4, 6), [3454] = new Anim(4, 6), [3455] = new Anim(4, 6),
                // NotActuallyAnimating: the first frame only.
                [4068] = new Anim(4, int.MaxValue), [4069] = new Anim(4, int.MaxValue), [4070] = new Anim(4, int.MaxValue),
                [5644] = new Anim(9, 7),
            };
            // ItemID.Sets.IsFood: three frames (the item, then the eating ones), never animating.
            foreach (int id in new[] { 353, 357, 1787, 1911, 1912, 1919, 1920, 2266, 2267, 2268, 2425, 2426, 2427, 3195, 3532, 4009, 4010, 4011, 4012, 4013,
                4014, 4015, 4016, 4017, 4018, 4019, 4020, 4021, 4022, 4023, 4024, 4025, 4026, 4027, 4028, 4029, 4030, 4031, 4032, 4033, 4034, 4035, 4036,
                4037, 967, 969, 4282, 4283, 4284, 4285, 4286, 4287, 4288, 4289, 4290, 4291, 4292, 4293, 4294, 4295, 4296, 4297, 4403, 4411, 4614, 4615,
                4616, 4617, 4618, 4619, 4620, 4621, 4622, 4623, 4624, 4625, 5009, 5042, 5041, 5092, 5093, 5275, 5277, 5278, 5537, 5645 })
                d[id] = new Anim(3, int.MaxValue);
            return d;
        }

        static bool Find(string texture, out Anim anim)
        {
            anim = default;
            return texture != null && texture.StartsWith("Item_") && int.TryParse(texture.Substring(5), out int id) && byId.TryGetValue(id, out anim);
        }

        /// <summary>How many frames a picture has (1 for a still one).</summary>
        public static int FramesOf(string texture) => Find(texture, out var a) ? a.Frames : 1;

        /// <summary>DrawAnimationVertical.Update at 60 ticks a second: the frame shown now, 0 at the top.</summary>
        public static int FrameNow(string texture)
        {
            if (!Find(texture, out var a) || a.Ticks == int.MaxValue) return 0;
            int step = (int)(Time.time * 60f) / a.Ticks;
            if (!a.PingPong) return step % a.Frames;
            int cycle = a.Frames * 2 - 2;
            int k = step % cycle;
            return k < a.Frames ? k : cycle - k;
        }

        /// <summary>The frame's part of the texture as GUI texture coordinates (Unity's rows run bottom to top).</summary>
        public static Rect CoordsNow(string texture)
        {
            int n = FramesOf(texture);
            if (n <= 1) return new Rect(0f, 0f, 1f, 1f);
            int f = FrameNow(texture);
            return new Rect(0f, 1f - (f + 1f) / n, 1f, 1f / n);
        }

        /// <summary>The frame's rectangle in the texture's pixels, for a sprite (bottom-left origin).</summary>
        public static Rect PixelsOf(Texture2D tex, string texture, int frame)
        {
            int n = FramesOf(texture);
            float h = tex.height / (float)n;
            return new Rect(0f, tex.height - (frame + 1) * h, tex.width, h);
        }
    }
}
