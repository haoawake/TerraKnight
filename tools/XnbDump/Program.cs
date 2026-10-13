using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using TerraKnight.Terraria;
using TerraKnight.Terraria.Xnb;

// Dev tool: decodes Terraria XNB textures to PNG so the decoder can be checked without
// launching the game.
//   XnbDump <Content\Images dir> <out dir> Item_1 Tiles_0 ...   decode the named files
//   XnbDump <Content\Images dir> --all                          decode everything, report failures
static class Program
{
    static int Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("usage: XnbDump <ImagesDir> <outDir> <name>... | XnbDump <ImagesDir> --all");
            return 2;
        }
        string imagesDir = args[0];
        if (args[1] == "--all") return DecodeAll(imagesDir);
        if (args[1] == "--frametest") return FrameTest(imagesDir, args[2], args[3]);
        if (args[1] == "--sounds") return DecodeSounds(imagesDir);

        string outDir = args[1];
        Directory.CreateDirectory(outDir);
        for (int i = 2; i < args.Length; i++)
        {
            string name = args[i];
            var img = XnbTexture.Load(Path.Combine(imagesDir, name + ".xnb"));
            string png = Path.Combine(outDir, name + ".png");
            WritePng(png, img);
            Console.WriteLine($"{name}: {img.Width}x{img.Height} -> {png}");
        }
        return 0;
    }

    static int DecodeAll(string imagesDir)
    {
        var sw = Stopwatch.StartNew();
        int ok = 0, failed = 0;
        foreach (string file in Directory.EnumerateFiles(imagesDir, "*.xnb", SearchOption.AllDirectories))
        {
            try
            {
                XnbTexture.Load(file);
                ok++;
            }
            catch (Exception e)
            {
                failed++;
                if (failed <= 20) Console.WriteLine($"FAIL {Path.GetRelativePath(imagesDir, file)}: {e.Message}");
            }
        }
        Console.WriteLine($"decoded {ok}, failed {failed}, {sw.ElapsedMilliseconds} ms");
        return failed == 0 ? 0 : 1;
    }

    static int DecodeSounds(string soundsDir)
    {
        int ok = 0, failed = 0;
        var formats = new System.Collections.Generic.SortedDictionary<string, int>();
        foreach (string file in Directory.EnumerateFiles(soundsDir, "*.xnb", SearchOption.AllDirectories))
        {
            try
            {
                var a = XnbSound.Load(file);
                string key = $"{a.Channels}ch {a.SampleRate}Hz";
                formats[key] = formats.TryGetValue(key, out int n) ? n + 1 : 1;
                ok++;
            }
            catch (Exception e)
            {
                failed++;
                if (failed <= 20) Console.WriteLine($"FAIL {Path.GetRelativePath(soundsDir, file)}: {e.Message}");
            }
        }
        foreach (var kv in formats) Console.WriteLine($"  {kv.Key}: {kv.Value}");
        Console.WriteLine($"sounds decoded {ok}, failed {failed}");
        return failed == 0 ? 0 : 1;
    }

    // Renders a hand-drawn cave with TileFraming to check every neighbour case picks the right frame.
    static int FrameTest(string imagesDir, string sheet, string outPng)
    {
        string[] map =
        {
            "##################",
            "##################",
            "####......########",
            "###........#######",
            "##....##....######",
            "##...####.........",
            "###..#..#.....#...",
            "####........###.#.",
            "######..#.....#...",
            "##################",
        };
        var src = XnbTexture.Load(Path.Combine(imagesDir, sheet + ".xnb"));
        int w = map[0].Length, h = map.Length, s = TileFraming.FrameSize;
        var img = new XnbImage { Width = w * s, Height = h * s, Rgba = new byte[w * s * h * s * 4] };
        bool Solid(int x, int y) => x < 0 || y < 0 || x >= w || y >= h || map[y][x] == '#';
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            if (!Solid(x, y)) continue;
            var (fx, fy) = TileFraming.Frame(Solid(x, y - 1), Solid(x, y + 1), Solid(x - 1, y), Solid(x + 1, y), TileFraming.Variant(x, y));
            for (int py = 0; py < s; py++)
                Array.Copy(src.Rgba, ((fy + py) * src.Width + fx) * 4, img.Rgba, ((y * s + py) * img.Width + x * s) * 4, s * 4);
        }
        WritePng(outPng, img);
        Console.WriteLine($"frame test -> {outPng}");
        return 0;
    }

    static void WritePng(string path, XnbImage img)
    {
        var raw = new MemoryStream();
        int stride = img.Width * 4;
        for (int y = 0; y < img.Height; y++)
        {
            raw.WriteByte(0); // filter: none
            raw.Write(img.Rgba, y * stride, stride);
        }
        var idat = new MemoryStream();
        using (var z = new ZLibStream(idat, CompressionLevel.Optimal, true)) raw.WriteTo(z);

        using var fs = File.Create(path);
        fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var ihdr = new byte[13];
        WriteBE(ihdr, 0, (uint)img.Width);
        WriteBE(ihdr, 4, (uint)img.Height);
        ihdr[8] = 8; // bit depth
        ihdr[9] = 6; // RGBA
        WriteChunk(fs, "IHDR", ihdr);
        WriteChunk(fs, "IDAT", idat.ToArray());
        WriteChunk(fs, "IEND", Array.Empty<byte>());
    }

    static void WriteChunk(Stream s, string type, byte[] data)
    {
        var buf = new byte[4];
        WriteBE(buf, 0, (uint)data.Length);
        s.Write(buf);
        var typed = new byte[4 + data.Length];
        for (int i = 0; i < 4; i++) typed[i] = (byte)type[i];
        data.CopyTo(typed, 4);
        s.Write(typed);
        WriteBE(buf, 0, Crc32(typed));
        s.Write(buf);
    }

    static void WriteBE(byte[] b, int o, uint v)
    {
        b[o] = (byte)(v >> 24);
        b[o + 1] = (byte)(v >> 16);
        b[o + 2] = (byte)(v >> 8);
        b[o + 3] = (byte)v;
    }

    static uint Crc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (byte b in data)
        {
            crc ^= b;
            for (int k = 0; k < 8; k++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
        }
        return ~crc;
    }
}
