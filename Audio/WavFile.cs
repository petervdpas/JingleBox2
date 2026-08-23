using System;
using System.IO;
using System.Text;

namespace JingleBox2.Audio;

/// <summary>
/// Reads the WAV files people have, writes the 16-bit ones this app keeps.
/// </summary>
/// <remarks>
/// Reading is generous and writing is not, on purpose. A sample folder is full of 24-bit and
/// float files saved by editors that default to them, and refusing those means a quarter of
/// somebody's own samples will not load with nothing said about why. What the app keeps is one
/// format, because everything downstream, the trim, the normalise, the voices, works in shorts.
/// Anything else is turned into that on the way in and never seen again.
/// </remarks>
internal static class WavFile
{
    public const int BitsPerSample = 16;
    private const int BytesPerSample = BitsPerSample / 8;

    /// <summary>How the samples are written in a file, before they are turned into shorts.</summary>
    internal readonly record struct Stored(int Format, int Bits)
    {
        public const int Pcm = 1;
        public const int Float = 3;

        /// <summary>What a modern editor writes: the real format is inside the sub-format GUID.</summary>
        public const int Extensible = 0xFFFE;

        public int Bytes => Bits / 8;

        public bool Known =>
            (Format == Pcm && Bits is 8 or 16 or 24 or 32) ||
            (Format == Float && Bits is 32 or 64);

        /// <summary>True when the file is already what this app keeps, so a copy is a copy.</summary>
        public bool IsOurs => Format == Pcm && Bits == BitsPerSample;

        public override string ToString() =>
            Format == Float ? Bits + "-bit float" : Bits + "-bit";
    }

    /// <summary>Format of a WAV file. FrameCount counts sample frames, not individual samples.</summary>
    public readonly record struct Info(int SampleRate, int Channels, long FrameCount);

    /// <summary>Reads just the headers, without pulling the audio into memory.</summary>
    public static Info ReadInfo(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(fs);
        return ReadHeader(fs, reader, out _, out _);
    }

    /// <summary>How the samples are written in a file, for deciding whether it needs converting.</summary>
    public static Stored StoredAs(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(fs);

        ReadHeader(fs, reader, out _, out var stored);

        return stored;
    }

    /// <summary>Reads the interleaved samples along with the format, whatever it was written as.</summary>
    public static (short[] Samples, Info Info) Read(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(fs);

        var info = ReadHeader(fs, reader, out int dataSize, out var stored);
        byte[] raw = reader.ReadBytes(dataSize);

        return (ToShorts(raw, stored), info);
    }

    /// <summary>
    /// Turns whatever was in the file into the shorts everything here works in.
    /// </summary>
    /// <remarks>
    /// A wider file loses its lower bits and a float file is clipped to the range a short can
    /// hold. Both are what converting to sixteen bits means, and both happen once, at the door,
    /// rather than quietly the next time a recording is trimmed.
    /// </remarks>
    private static short[] ToShorts(byte[] raw, Stored stored)
    {
        int bytes = stored.Bytes;
        int count = bytes <= 0 ? 0 : raw.Length / bytes;

        if (stored.IsOurs)
        {
            var already = new short[count];
            Buffer.BlockCopy(raw, 0, already, 0, count * BytesPerSample);
            return already;
        }

        var samples = new short[count];

        for (int i = 0; i < count; i++)
        {
            int at = i * bytes;

            samples[i] = stored.Format switch
            {
                Stored.Float when stored.Bits == 32 => FromFloat(BitConverter.ToSingle(raw, at)),
                Stored.Float => FromFloat(BitConverter.ToDouble(raw, at)),

                // Unsigned, alone among them, and centred on 128 rather than on nought.
                _ when stored.Bits == 8 => (short)((raw[at] - 128) << 8),

                // Little-endian, so the two bytes worth keeping are the last two.
                _ when stored.Bits == 24 => (short)((raw[at + 2] << 8) | raw[at + 1]),

                _ when stored.Bits == 32 => (short)(BitConverter.ToInt32(raw, at) >> 16),

                _ => 0
            };
        }

        return samples;
    }

    private static short FromFloat(double value) =>
        double.IsNaN(value) ? (short)0 : (short)Math.Clamp(value * short.MaxValue, short.MinValue, short.MaxValue);

    public static void Write(string filePath, short[] samples, int sampleRate, int channels)
    {
        byte[] pcmData = new byte[samples.Length * BytesPerSample];
        Buffer.BlockCopy(samples, 0, pcmData, 0, pcmData.Length);
        Write(filePath, pcmData, sampleRate, channels);
    }

    public static void Write(string filePath, byte[] pcmData, int sampleRate, int channels)
    {
        using var fs = new FileStream(filePath, FileMode.Create);
        using var writer = new BinaryWriter(fs);

        int blockAlign = channels * BytesPerSample;

        writer.Write(Tag("RIFF"));
        writer.Write(36 + pcmData.Length);
        writer.Write(Tag("WAVE"));
        writer.Write(Tag("fmt "));
        writer.Write(16);                       // fmt chunk size
        writer.Write((ushort)1);                // PCM
        writer.Write((ushort)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * blockAlign);  // byte rate
        writer.Write((ushort)blockAlign);
        writer.Write((ushort)BitsPerSample);
        writer.Write(Tag("data"));
        writer.Write(pcmData.Length);
        writer.Write(pcmData);
    }

    /// <summary>
    /// Walks the chunk list, validates the format, and leaves the stream positioned at the
    /// start of the audio data.
    /// </summary>
    private static Info ReadHeader(FileStream fs, BinaryReader reader, out int dataSize, out Stored stored)
    {
        if (ReadTag(reader) != "RIFF")
            throw new InvalidOperationException("Not a WAV file: missing RIFF header.");

        reader.ReadInt32(); // riff size

        if (ReadTag(reader) != "WAVE")
            throw new InvalidOperationException("Not a WAV file: missing WAVE header.");

        int format = 0, channels = 0, sampleRate = 0, bits = 0;
        bool haveFormat = false;

        while (fs.Position + 8 <= fs.Length)
        {
            string chunkId = ReadTag(reader);
            int chunkSize = reader.ReadInt32();
            long chunkStart = fs.Position;

            if (chunkId == "fmt ")
            {
                format = reader.ReadUInt16();
                channels = reader.ReadUInt16();
                sampleRate = reader.ReadInt32();
                reader.ReadInt32();  // byte rate
                reader.ReadUInt16(); // block align
                bits = reader.ReadUInt16();
                haveFormat = true;

                // An extensible header says PCM or float in the first two bytes of its
                // sub-format GUID, and 0xFFFE in the field everything else reads. Without this
                // every file an editor saved with its defaults looks like an unknown format.
                if (format == Stored.Extensible && chunkSize >= 40)
                {
                    reader.ReadUInt16();          // cbSize
                    reader.ReadUInt16();          // bits actually used, which may be fewer
                    reader.ReadUInt32();          // which speaker each channel is
                    format = reader.ReadUInt16(); // the sub-format GUID starts with the real one
                }
            }
            else if (chunkId == "data")
            {
                if (!haveFormat)
                    throw new InvalidOperationException("Invalid WAV file: data chunk before format chunk.");

                stored = new Stored(format, bits);

                if (!stored.Known)
                    throw new InvalidOperationException($"This WAV file is written in a way JingleBox cannot read (format {format}, {bits}-bit).");

                if (channels <= 0 || sampleRate <= 0)
                    throw new InvalidOperationException("Invalid WAV file: bad channel count or sample rate.");

                // Trust the file length over the declared size; truncated recordings are common.
                dataSize = (int)Math.Min(chunkSize, fs.Length - chunkStart);
                return new Info(sampleRate, channels, dataSize / (stored.Bytes * (long)channels));
            }

            // RIFF chunks are word-aligned, so an odd size carries a trailing pad byte.
            fs.Seek(chunkStart + chunkSize + (chunkSize & 1), SeekOrigin.Begin);
        }

        throw new InvalidOperationException("Invalid WAV file: missing data chunk.");
    }

    private static byte[] Tag(string value) => Encoding.ASCII.GetBytes(value);

    private static string ReadTag(BinaryReader reader) => Encoding.ASCII.GetString(reader.ReadBytes(4));
}
