using System;
using System.IO;

namespace TerraKnight.Terraria.Xnb
{
    /// <summary>
    /// XNA 4.0 .xnb container: header, optional LZX compression and the type-reader table.
    /// Open() returns a reader positioned at the primary object's data.
    /// </summary>
    internal static class XnbFile
    {
        public static BinaryReader Open(byte[] file, out string readerName)
        {
            var raw = new MemoryStream(file, false);
            var header = new BinaryReader(raw);
            if (header.ReadByte() != 'X' || header.ReadByte() != 'N' || header.ReadByte() != 'B')
                throw new InvalidDataException("not an XNB file");
            header.ReadByte(); // target platform
            byte version = header.ReadByte();
            if (version != 5) throw new NotSupportedException($"XNB version {version}");
            byte flags = header.ReadByte();
            int fileSize = header.ReadInt32();

            Stream content = raw;
            if ((flags & 0x80) != 0)
            {
                int decompressedSize = header.ReadInt32();
                content = DecompressLzx(raw, fileSize - 14, decompressedSize);
            }
            else if ((flags & 0x40) != 0)
            {
                throw new NotSupportedException("LZ4 compressed XNB");
            }

            var r = new BinaryReader(content);
            int readerCount = Read7BitInt(r);
            var readers = new string[readerCount];
            for (int i = 0; i < readerCount; i++)
            {
                readers[i] = r.ReadString();
                r.ReadInt32(); // reader version
            }
            Read7BitInt(r); // shared resource count

            int typeId = Read7BitInt(r);
            if (typeId == 0 || typeId > readerCount) throw new InvalidDataException("XNB has no primary object");
            readerName = readers[typeId - 1];
            return r;
        }

        // XNB LZX payload is a series of frames, each prefixed with its sizes.
        static MemoryStream DecompressLzx(Stream input, int compressedSize, int decompressedSize)
        {
            var output = new MemoryStream(decompressedSize);
            var decoder = new LzxDecoder(16);
            long start = input.Position;
            long pos = start;
            while (pos - start < compressedSize)
            {
                int hi = input.ReadByte();
                int lo = input.ReadByte();
                int blockSize = (hi << 8) | lo;
                int frameSize = 0x8000;
                if (hi == 0xFF)
                {
                    hi = lo;
                    lo = input.ReadByte();
                    frameSize = (hi << 8) | lo;
                    hi = input.ReadByte();
                    lo = input.ReadByte();
                    blockSize = (hi << 8) | lo;
                    pos += 5;
                }
                else
                {
                    pos += 2;
                }
                if (blockSize == 0 || frameSize == 0) break;
                if (decoder.Decompress(input, blockSize, output, frameSize) != 0)
                    throw new InvalidDataException("LZX decompression failed");
                pos += blockSize;
                input.Seek(pos, SeekOrigin.Begin);
            }
            if (output.Length != decompressedSize)
                throw new InvalidDataException($"LZX produced {output.Length} bytes, expected {decompressedSize}");
            output.Position = 0;
            return output;
        }

        static int Read7BitInt(BinaryReader r)
        {
            int result = 0, shift = 0;
            byte b;
            do
            {
                b = r.ReadByte();
                result |= (b & 0x7F) << shift;
                shift += 7;
            } while ((b & 0x80) != 0);
            return result;
        }
    }
}
