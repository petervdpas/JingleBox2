using System;
using System.IO;
using System.Text;

namespace JingleBox2.Audio;

/// <summary>
/// Minimal reader/writer for the 16-bit PCM WAV files this app records and edits.
/// </summary>
internal static class WavFile
{
    public const int BitsPerSample = 16;
    private const int BytesPerSample = BitsPerSample / 8;

    /// <summary>Format of a WAV file. FrameCount counts sample frames, not individual samples.</summary>
    public readonly record struct Info(int SampleRate, int Channels, long FrameCount);

    /// <summary>Reads just the headers, without pulling the audio into memory.</summary>
    public static Info ReadInfo(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(fs);
        return ReadHeader(fs, reader, out _);
    }

    /// <summary>Reads the interleaved samples along with the format.</summary>
    public static (short[] Samples, Info Info) Read(string filePath)
    {
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
        using var reader = new BinaryReader(fs);

        var info = ReadHeader(fs, reader, out int dataSize);
        byte[] raw = reader.ReadBytes(dataSize);

        short[] samples = new short[raw.Length / BytesPerSample];
        Buffer.BlockCopy(raw, 0, samples, 0, samples.Length * BytesPerSample);

        return (samples, info);
    }

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
    private static Info ReadHeader(FileStream fs, BinaryReader reader, out int dataSize)
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
            }
            else if (chunkId == "data")
            {
                if (!haveFormat)
                    throw new InvalidOperationException("Invalid WAV file: data chunk before format chunk.");
                if (format != 1 || bits != BitsPerSample)
                    throw new InvalidOperationException($"Only 16-bit PCM WAV files are supported (found format {format}, {bits}-bit).");
                if (channels <= 0 || sampleRate <= 0)
                    throw new InvalidOperationException("Invalid WAV file: bad channel count or sample rate.");

                // Trust the file length over the declared size; truncated recordings are common.
                dataSize = (int)Math.Min(chunkSize, fs.Length - chunkStart);
                return new Info(sampleRate, channels, dataSize / (BytesPerSample * (long)channels));
            }

            // RIFF chunks are word-aligned, so an odd size carries a trailing pad byte.
            fs.Seek(chunkStart + chunkSize + (chunkSize & 1), SeekOrigin.Begin);
        }

        throw new InvalidOperationException("Invalid WAV file: missing data chunk.");
    }

    private static byte[] Tag(string value) => Encoding.ASCII.GetBytes(value);

    private static string ReadTag(BinaryReader reader) => Encoding.ASCII.GetString(reader.ReadBytes(4));
}
