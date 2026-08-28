using ManagedBass;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
public sealed class AudioDecode : IAudioDecode
{
    /// <summary>Which add-ons are beside the program, and so which formats can be read.</summary>
    private readonly IBassPlugins _plugins = new BassPlugins();

    /// <inheritdoc/>
    public IReadOnlyList<string> Kinds =>
        _plugins.Kinds.Where(one => !string.Equals(one, ".wav", StringComparison.OrdinalIgnoreCase)).ToArray();

    /// <inheritdoc/>
    public bool Handles(string path) =>
        Kinds.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    /// <summary>How much is read from BASS at a time. One page of samples, not one file.</summary>
    private const int Block = 32768;

    /// <summary>How wide one sample is here, which is sixteen bits everywhere in this app.</summary>
    private const int BytesPerSample = 2;

    /// <inheritdoc/>
    public (short[] Samples, int SampleRate, int Channels)? Read(string path)
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
    private int Room(int channel)
    {
        long length = Bass.ChannelGetLength(channel, PositionFlags.Bytes);

        if (length <= 0 || length / BytesPerSample > int.MaxValue) return Block;

        return Math.Max(Block, (int)(length / BytesPerSample));
    }

    /// <inheritdoc/>
    public string Trouble(string path) =>
        Handles(path)
            ? "'" + Path.GetFileName(path) + "' could not be decoded."
            : "'" + Path.GetExtension(path).TrimStart('.') + "' needs a BASS add-on this build has not got.";

    /// <summary>
    /// Held while BASS is brought up, since two imports can start at once.
    /// </summary>
    /// <remarks>
    /// Static, and the one thing here that is. BASS is one library in one process, so bringing
    /// it up is a fact about the process rather than about this object, and a second decoder
    /// made while the first was still initialising would race with it over the same library.
    /// </remarks>
    private static readonly object Gate = new();

    /// <summary>Whether BASS has been brought up far enough to decode.</summary>
    private static bool _ready;

    /// <summary>
    /// Brings BASS up far enough to decode, if nothing else has.
    /// </summary>
    /// <remarks>
    /// Only when it is not already up. Initialising a second device while the pads are playing
    /// through the first would move what BASS calls the current device out from under them, and
    /// a recording imported would stop a jingle mid word.
    /// </remarks>
    private bool Ready()
    {
        lock (Gate)
        {
            if (_ready) return true;

            if (Bass.CurrentDevice < 0 && !Bass.Init(NoDevice, SampleRate) && Bass.LastError != Errors.Already)
                return false;

            _plugins.Load();

            _ready = true;

            return true;
        }
    }

    /// <summary>BASS's own silent device, which decodes and plays nothing.</summary>
    private const int NoDevice = 0;

    /// <summary>The rate the silent device is opened at. Decoding is unaffected by it.</summary>
    private const int SampleRate = 44100;
}
