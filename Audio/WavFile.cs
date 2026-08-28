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
    /// <summary>What this app keeps, and the only width it writes.</summary>
    public const int BitsPerSample = 16;

    /// <summary>How wide one of those samples is in bytes.</summary>
    private const int BytesPerSample = BitsPerSample / 8;

    /// <summary>How the samples are written in a file, before they are turned into shorts.</summary>
    internal readonly record struct Stored(int Format, int Bits)
    {
        /// <summary>Whole numbers, which is what most files hold.</summary>
        public const int Pcm = 1;

        /// <summary>Floating point, which editors write when asked for the highest quality.</summary>
        public const int Float = 3;

        /// <summary>What a modern editor writes: the real format is inside the sub-format GUID.</summary>
        public const int Extensible = 0xFFFE;

        /// <summary>How wide one sample is in the file.</summary>
        public int Bytes => Bits / 8;

        /// <summary>Whether this is a layout that can be turned into shorts here.</summary>
        public bool Known =>
            (Format == Pcm && Bits is 8 or 16 or 24 or 32) ||
            (Format == Float && Bits is 32 or 64);

        /// <summary>True when the file is already what this app keeps, so a copy is a copy.</summary>
        public bool IsOurs => Format == Pcm && Bits == BitsPerSample;

        /// <summary>How the layout reads in a message somebody is shown.</summary>
        public override string ToString() =>
            Format == Float ? Bits + "-bit float" : Bits + "-bit";
    }

    /// <summary>Format of a WAV file.</summary>
    /// <param name="SampleRate">Frames a second.</param>
    /// <param name="Channels">How many samples one frame holds.</param>
    /// <param name="FrameCount">How many frames the file holds, which is not its sample count.</param>
    public readonly record struct Info(int SampleRate, int Channels, long FrameCount);

    /// <summary>Reads just the headers, without pulling the audio into memory.</summary>
    /// <param name="filePath">The file.</param>
    /// <exception cref="InvalidOperationException">It is not a WAV this app can read.</exception>
    public static Info ReadInfo(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(fs);
        return ReadHeader(fs, reader, out _, out _);
    }

    /// <summary>How the samples are written in a file, for deciding whether it needs converting.</summary>
    /// <param name="filePath">The file.</param>
    /// <exception cref="InvalidOperationException">It is not a WAV this app can read.</exception>
    public static Stored StoredAs(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(fs);

        ReadHeader(fs, reader, out _, out var stored);

        return stored;
    }

    /// <summary>Reads the interleaved samples along with the format, whatever it was written as.</summary>
    /// <param name="filePath">The file.</param>
    /// <exception cref="InvalidOperationException">It is not a WAV this app can read.</exception>
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
    ///
    /// Each width has its own trap. Eight bit is unsigned, alone among them, and centred on 128
    /// rather than on nought. Twenty four bit is little endian, so the two bytes worth keeping
    /// are the last two of the three. Thirty two bit whole numbers keep their top sixteen.
    /// </remarks>
    /// <param name="raw">The data chunk, exactly as it sat in the file.</param>
    /// <param name="stored">How those bytes are laid out.</param>
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

                _ when stored.Bits == 8 => (short)((raw[at] - 128) << 8),

                _ when stored.Bits == 24 => (short)((raw[at + 2] << 8) | raw[at + 1]),

                _ when stored.Bits == 32 => (short)(BitConverter.ToInt32(raw, at) >> 16),

                _ => 0
            };
        }

        return samples;
    }

    /// <summary>One floating point sample as a short, with silence for a value that is not a number.</summary>
    private static short FromFloat(double value) =>
        double.IsNaN(value) ? (short)0 : (short)Math.Clamp(value * short.MaxValue, short.MinValue, short.MaxValue);

    /// <summary>Writes samples out as the one format this app keeps.</summary>
    /// <param name="filePath">Where to write, overwriting whatever is there.</param>
    /// <param name="samples">The interleaved samples.</param>
    /// <param name="sampleRate">Frames a second.</param>
    /// <param name="channels">How many samples one frame holds.</param>
    public static void Write(string filePath, short[] samples, int sampleRate, int channels)
    {
        byte[] pcmData = new byte[samples.Length * BytesPerSample];
        Buffer.BlockCopy(samples, 0, pcmData, 0, pcmData.Length);
        Write(filePath, pcmData, sampleRate, channels);
    }

    /// <summary>Writes bytes that are already sixteen bit whole numbers.</summary>
    /// <remarks>
    /// The header is the plain forty four byte one: a RIFF tag, a fmt chunk of sixteen bytes
    /// saying PCM, and a data chunk. Nothing extensible is written, because the one thing that
    /// needs is a width this app does not use.
    /// </remarks>
    /// <param name="filePath">Where to write, overwriting whatever is there.</param>
    /// <param name="pcmData">The data chunk, as it will sit in the file.</param>
    /// <param name="sampleRate">Frames a second.</param>
    /// <param name="channels">How many samples one frame holds.</param>
    public static void Write(string filePath, byte[] pcmData, int sampleRate, int channels)
    {
        using var fs = new FileStream(filePath, FileMode.Create);
        using var writer = new BinaryWriter(fs);

        int blockAlign = channels * BytesPerSample;

        writer.Write(Tag("RIFF"));
        writer.Write(HeaderSize + pcmData.Length);
        writer.Write(Tag("WAVE"));
        writer.Write(Tag("fmt "));
        writer.Write(FormatChunkSize);
        writer.Write((ushort)Stored.Pcm);
        writer.Write((ushort)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * blockAlign);
        writer.Write((ushort)blockAlign);
        writer.Write((ushort)BitsPerSample);
        writer.Write(Tag("data"));
        writer.Write(pcmData.Length);
        writer.Write(pcmData);
    }

    /// <summary>How many bytes a plain fmt chunk holds, which is what is written here.</summary>
    private const int FormatChunkSize = 16;

    /// <summary>What the RIFF size counts besides the audio: everything after its own field.</summary>
    private const int HeaderSize = 36;

    /// <summary>
    /// Walks the chunk list, validates the format, and leaves the stream positioned at the
    /// start of the audio data.
    /// </summary>
    /// <remarks>
    /// Three things here are about files people really have rather than about the format. An
    /// extensible header says PCM or float in the first two bytes of its sub-format GUID and
    /// 0xFFFE in the field everything else reads, so without reading it every file an editor
    /// saved with its defaults looks like an unknown format. The declared data size is not
    /// trusted over the file's own length, since a truncated recording is common and the whole
    /// of what survived is worth keeping. And a chunk of odd size carries a trailing pad byte,
    /// because RIFF chunks are word aligned.
    /// </remarks>
    /// <param name="fs">The file, left at the start of the audio.</param>
    /// <param name="reader">A reader over that same file.</param>
    /// <param name="dataSize">How many bytes of audio follow.</param>
    /// <param name="stored">How those bytes are laid out.</param>
    private static Info ReadHeader(FileStream fs, BinaryReader reader, out int dataSize, out Stored stored)
    {
        if (ReadTag(reader) != "RIFF")
            throw new InvalidOperationException("Not a WAV file: missing RIFF header.");

        reader.ReadInt32();

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
                reader.ReadInt32();
                reader.ReadUInt16();
                bits = reader.ReadUInt16();
                haveFormat = true;

                if (format == Stored.Extensible && chunkSize >= ExtensibleChunkSize)
                {
                    reader.ReadUInt16();
                    reader.ReadUInt16();
                    reader.ReadUInt32();
                    format = reader.ReadUInt16();
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

                dataSize = (int)Math.Min(chunkSize, fs.Length - chunkStart);
                return new Info(sampleRate, channels, dataSize / (stored.Bytes * (long)channels));
            }

            fs.Seek(chunkStart + chunkSize + (chunkSize & 1), SeekOrigin.Begin);
        }

        throw new InvalidOperationException("Invalid WAV file: missing data chunk.");
    }

    /// <summary>How many bytes an extensible fmt chunk holds at the least.</summary>
    private const int ExtensibleChunkSize = 40;

    /// <summary>A four character chunk name as the bytes a file holds it in.</summary>
    private static byte[] Tag(string value) => Encoding.ASCII.GetBytes(value);

    /// <summary>The next four characters, which is what every chunk begins with.</summary>
    private static string ReadTag(BinaryReader reader) => Encoding.ASCII.GetString(reader.ReadBytes(4));
}
