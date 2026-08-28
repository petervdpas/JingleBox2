using System;
using System.IO;
using System.Text;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Records;

namespace JingleBox2.Audio;

/// <inheritdoc/>
/// <remarks>
/// Public, as its contract is. It was internal when it was a static nobody could stand in front
/// of; with an interface over it, an internal implementation would be a contract nobody outside
/// could hold, which includes the tests.
/// </remarks>
public sealed class WavFile : IWavFile
{
    /// <inheritdoc cref="IWavFile.BitsPerSample"/>
    public const int BitsPerSample = WavStored.OurBits;

    /// <inheritdoc/>
    int IWavFile.BitsPerSample => BitsPerSample;

    /// <summary>How wide one of those samples is in bytes.</summary>
    private const int BytesPerSample = BitsPerSample / 8;

    /// <summary>
    /// The shortest thing that can call itself a WAV: "RIFF", a size, and "WAVE".
    /// </summary>
    /// <remarks>
    /// Checked before any of the three are read rather than after, since reading them off a
    /// file too short to hold them is what the runtime complains about, and its complaint is
    /// about a stream rather than about the file somebody just tried to open.
    /// </remarks>
    private const int PreambleSize = 12;



    /// <inheritdoc/>
    public WavInfo ReadInfo(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(fs);
        return ReadHeader(fs, reader, out _, out _);
    }

    /// <inheritdoc/>
    public WavStored StoredAs(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(fs);

        ReadHeader(fs, reader, out _, out var stored);

        return stored;
    }

    /// <inheritdoc/>
    public (short[] Samples, WavInfo Info) Read(string filePath)
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
    private short[] ToShorts(byte[] raw, WavStored stored)
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
                WavStored.Float when stored.Bits == 32 => FromFloat(BitConverter.ToSingle(raw, at)),
                WavStored.Float => FromFloat(BitConverter.ToDouble(raw, at)),

                _ when stored.Bits == 8 => (short)((raw[at] - 128) << 8),

                _ when stored.Bits == 24 => (short)((raw[at + 2] << 8) | raw[at + 1]),

                _ when stored.Bits == 32 => (short)(BitConverter.ToInt32(raw, at) >> 16),

                _ => 0
            };
        }

        return samples;
    }

    /// <summary>One floating point sample as a short, with silence for a value that is not a number.</summary>
    private short FromFloat(double value) =>
        double.IsNaN(value) ? (short)0 : (short)Math.Clamp(value * short.MaxValue, short.MinValue, short.MaxValue);

    /// <inheritdoc/>
    public void Write(string filePath, short[] samples, int sampleRate, int channels)
    {
        byte[] pcmData = new byte[samples.Length * BytesPerSample];
        Buffer.BlockCopy(samples, 0, pcmData, 0, pcmData.Length);
        Write(filePath, pcmData, sampleRate, channels);
    }

    /// <inheritdoc/>
    public void Write(string filePath, byte[] pcmData, int sampleRate, int channels)
    {
        using var fs = new FileStream(filePath, FileMode.Create);
        using var writer = new BinaryWriter(fs);

        int blockAlign = channels * BytesPerSample;

        writer.Write(Tag("RIFF"));
        writer.Write(HeaderSize + pcmData.Length);
        writer.Write(Tag("WAVE"));
        writer.Write(Tag("fmt "));
        writer.Write(FormatChunkSize);
        writer.Write((ushort)WavStored.Pcm);
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
    /// <remarks>
    /// A chunk whose body is not all there is refused in the file's own words rather than being
    /// read past the end of. The walk only ever checked that a chunk's eight byte header would
    /// fit, so a file cut off inside its format chunk came back as the runtime saying it could
    /// not read beyond the end of a stream, which reaches RECORD as a message about a stream
    /// rather than about the file somebody just tried to open. The audio itself is the one part
    /// that is salvaged rather than refused, and that is deliberate: what a take really holds
    /// beats what its header claims, so a recording interrupted by the machine going down plays
    /// whatever got as far as the disc.
    /// </remarks>
    private WavInfo ReadHeader(FileStream fs, BinaryReader reader, out int dataSize, out WavStored stored)
    {
        if (fs.Length < PreambleSize)
            throw new InvalidOperationException("Not a WAV file: it is too short to hold a header.");

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
                if (fs.Length - chunkStart < FormatChunkSize)
                    throw new InvalidOperationException("Invalid WAV file: it ends part way through its headers.");

                format = reader.ReadUInt16();
                channels = reader.ReadUInt16();
                sampleRate = reader.ReadInt32();
                reader.ReadInt32();
                reader.ReadUInt16();
                bits = reader.ReadUInt16();
                haveFormat = true;

                if (format == WavStored.Extensible && chunkSize >= ExtensibleChunkSize
                    && fs.Length - chunkStart >= ExtensibleChunkSize)
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

                stored = new WavStored(format, bits);

                if (!stored.Known)
                    throw new InvalidOperationException($"This WAV file is written in a way JingleBox cannot read (format {format}, {bits}-bit).");

                if (channels <= 0 || sampleRate <= 0)
                    throw new InvalidOperationException("Invalid WAV file: bad channel count or sample rate.");

                dataSize = (int)Math.Min(chunkSize, fs.Length - chunkStart);
                return new WavInfo(sampleRate, channels, dataSize / (stored.Bytes * (long)channels));
            }

            fs.Seek(chunkStart + chunkSize + (chunkSize & 1), SeekOrigin.Begin);
        }

        throw new InvalidOperationException("Invalid WAV file: missing data chunk.");
    }

    /// <summary>How many bytes an extensible fmt chunk holds at the least.</summary>
    private const int ExtensibleChunkSize = 40;

    /// <summary>A four character chunk name as the bytes a file holds it in.</summary>
    private byte[] Tag(string value) => Encoding.ASCII.GetBytes(value);

    /// <summary>The next four characters, which is what every chunk begins with.</summary>
    private string ReadTag(BinaryReader reader) => Encoding.ASCII.GetString(reader.ReadBytes(4));
}
