using System;
using System.IO;

namespace TerraKnight.Terraria.Xnb
{
    /// <summary>A decoded texture: straight-alpha RGBA8, rows top to bottom.</summary>
    public sealed class XnbImage
    {
        public int Width;
        public int Height;
        public byte[] Rgba;
    }

    /// <summary>
    /// Reads Texture2D assets from Terraria's Content/Images/*.xnb. Nothing here touches
    /// Unity, so the same code runs in the plugin and in the XnbDump command line tool.
    /// </summary>
    public static class XnbTexture
    {
        const int SurfaceFormatColor = 0;

        public static XnbImage Load(string path) => Load(File.ReadAllBytes(path));

        public static XnbImage Load(byte[] file)
        {
            var r = XnbFile.Open(file, out string readerName);
            if (!readerName.Contains("Texture2DReader")) throw new InvalidDataException("XNB does not contain a Texture2D");

            int format = r.ReadInt32();
            int width = r.ReadInt32();
            int height = r.ReadInt32();
            r.ReadInt32(); // mip count, only the first level is used
            int dataSize = r.ReadInt32();
            if (format != SurfaceFormatColor) throw new NotSupportedException($"surface format {format}");
            if (dataSize != width * height * 4) throw new InvalidDataException("unexpected texture data size");

            byte[] rgba = r.ReadBytes(dataSize);
            Unpremultiply(rgba);
            return new XnbImage { Width = width, Height = height, Rgba = rgba };
        }

        // The XNA content pipeline stores premultiplied alpha; Unity sprites expect straight alpha.
        static void Unpremultiply(byte[] rgba)
        {
            for (int i = 0; i < rgba.Length; i += 4)
            {
                int a = rgba[i + 3];
                if (a == 0 || a == 255) continue;
                rgba[i] = (byte)Math.Min(255, rgba[i] * 255 / a);
                rgba[i + 1] = (byte)Math.Min(255, rgba[i + 1] * 255 / a);
                rgba[i + 2] = (byte)Math.Min(255, rgba[i + 2] * 255 / a);
            }
        }
    }
}
