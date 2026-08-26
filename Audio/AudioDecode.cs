using ManagedBass;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace JingleBox2.Audio;

/// <summary>
/// Turns a compressed recording into the samples this app works in.
/// </summary>
/// <remarks>
/// One thing only: read somebody's mp3, ogg or flac and hand back sixteen bit samples. Nothing
/// downstream ever meets one of those files, because nothing downstream is ever given one:
/// <see cref="RecordingImport"/> decodes at the door and writes a WAV, and the shelf stays the
/// single format it has always been.
///
/// Through BASS, which is already here for playing pads and already knows these formats. Writing
/// three decoders would be three decoders to be wrong in, and the one already loaded is the one
/// the pads have been playing mp3s through all along.
/// </remarks>
public static class AudioDecode
{
    /// <summary>What can be read, which depends on which add-ons are beside the program.</summary>
    /// <remarks>
    /// WAV is left out. It is read here, but this app has its own reader for it and that reader
    /// knows what a file was stored as, which is what decides whether it is copied or rewritten.
    /// </remarks>
    public static IReadOnlyList<string> Kinds =>
        BassPlugins.Kinds.Where(one => !string.Equals(one, ".wav", StringComparison.OrdinalIgnoreCase)).ToArray();

    /// <summary>True when this is a file to be decoded rather than read as a WAV.</summary>
    public static bool Handles(string path) =>
        Kinds.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>How much is read from BASS at a time. One page of samples, not one file.</summary>
    private const int Block = 32768;

    private const int BytesPerSample = 2;

    /// <summary>
    /// Reads the whole thing, or nothing when it cannot be read.
    /// </summary>
    /// <remarks>
    /// A decoding channel rather than a playing one: BASS hands the samples back instead of
    /// sending them to a device, so this neither makes a sound nor needs one. Sixteen bit is
    /// what a channel gives without being asked, which is what this app keeps anyway.
    /// </remarks>
    public static (short[] Samples, int SampleRate, int Channels)? Read(string path)
    {
        if (!Ready()) return null;

        int channel = Bass.CreateStream(path, 0, 0, BassFlags.Decode);

        if (channel == 0) return null;

        try
        {
            var info = Bass.ChannelGetInfo(channel);

            if (info.Channels <= 0 || info.Frequency <= 0) return null;

            var samples = new short[Room(channel)];
            var block = new short[Block];
            int filled = 0;

            while (true)
            {
                int bytes = Bass.ChannelGetData(channel, block, Block * BytesPerSample);

                // Nought is the end of the file. Below nought is BASS saying it went wrong,
                // which for a file that is already open means a truncated one: what was read
                // before it is still good, and is kept.
                if (bytes <= 0) break;

                int read = bytes / BytesPerSample;

                if (filled + read > samples.Length)
                    Array.Resize(ref samples, Math.Max(samples.Length * 2, filled + read));

                Buffer.BlockCopy(block, 0, samples, filled * BytesPerSample, read * BytesPerSample);

                filled += read;
            }

            if (filled == 0) return null;

            Array.Resize(ref samples, filled);

            return (samples, info.Frequency, info.Channels);
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            Bass.StreamFree(channel);
        }
    }

    /// <summary>
    /// How long the thing is likely to be, so the whole of it is not copied about while reading.
    /// </summary>
    /// <remarks>
    /// An estimate for anything that is not laid out in frames, which mp3 is not. Being short is
    /// only a resize, so a guess that is roughly right is worth more than a guess that is safe.
    /// </remarks>
    private static int Room(int channel)
    {
        long length = Bass.ChannelGetLength(channel, PositionFlags.Bytes);

        if (length <= 0 || length / BytesPerSample > int.MaxValue) return Block;

        return Math.Max(Block, (int)(length / BytesPerSample));
    }

    /// <summary>The one that reports what a file could not be read as.</summary>
    public static string Trouble(string path) =>
        Handles(path)
            ? "'" + Path.GetFileName(path) + "' could not be decoded."
            : "'" + Path.GetExtension(path).TrimStart('.') + "' needs a BASS add-on this build has not got.";

    private static readonly object Gate = new();

    private static bool _ready;

    /// <summary>
    /// Brings BASS up far enough to decode, if nothing else has.
    /// </summary>
    /// <remarks>
    /// Only when it is not already up. Initialising a second device while the pads are playing
    /// through the first would move what BASS calls the current device out from under them, and
    /// a recording imported would stop a jingle mid word.
    /// </remarks>
    private static bool Ready()
    {
        lock (Gate)
        {
            if (_ready) return true;

            if (Bass.CurrentDevice < 0 && !Bass.Init(NoDevice, SampleRate) && Bass.LastError != Errors.Already)
                return false;

            BassPlugins.Load();

            _ready = true;

            return true;
        }
    }

    /// <summary>BASS's own silent device, which decodes and plays nothing.</summary>
    private const int NoDevice = 0;

    private const int SampleRate = 44100;
}
