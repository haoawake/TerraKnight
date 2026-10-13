using System;
using System.IO;

namespace TerraKnight.Terraria.Xnb
{
    /// <summary>A decoded sound: interleaved float samples in [-1, 1].</summary>
    public sealed class XnbAudio
    {
        public int Channels;
        public int SampleRate;
        public float[] Samples;
    }

    /// <summary>Reads SoundEffect assets from Terraria's Content/Sounds/*.xnb (16-bit or 8-bit PCM).</summary>
    public static class XnbSound
    {
        const int WaveFormatPcm = 1;

        public static XnbAudio Load(string path) => Load(File.ReadAllBytes(path));

        public static XnbAudio Load(byte[] file)
        {
            var r = XnbFile.Open(file, out string readerName);
            if (!readerName.Contains("SoundEffectReader")) throw new InvalidDataException("XNB does not contain a SoundEffect");

            int formatSize = r.ReadInt32();
            byte[] format = r.ReadBytes(formatSize);
            int formatTag = BitConverter.ToUInt16(format, 0);
            int channels = BitConverter.ToUInt16(format, 2);
            int sampleRate = BitConverter.ToInt32(format, 4);
            int bitsPerSample = BitConverter.ToUInt16(format, 14);
            if (formatTag != WaveFormatPcm) throw new NotSupportedException($"wave format {formatTag}");

            int dataSize = r.ReadInt32();
            byte[] data = r.ReadBytes(dataSize);

            float[] samples;
            if (bitsPerSample == 16)
            {
                samples = new float[dataSize / 2];
                for (int i = 0; i < samples.Length; i++) samples[i] = BitConverter.ToInt16(data, i * 2) / 32768f;
            }
            else if (bitsPerSample == 8)
            {
                samples = new float[dataSize];
                for (int i = 0; i < samples.Length; i++) samples[i] = (data[i] - 128) / 128f;
            }
            else
            {
                throw new NotSupportedException($"{bitsPerSample}-bit PCM");
            }
            return new XnbAudio { Channels = channels, SampleRate = sampleRate, Samples = samples };
        }
    }
}
