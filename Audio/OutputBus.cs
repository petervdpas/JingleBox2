using System;
using System.Collections.Generic;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using ManagedBass;
using ManagedBass.Mix;

namespace JingleBox2.Audio;

/// <inheritdoc/>
/// <remarks>
/// Through BASSmix, which is un4seen's add-on for exactly this and is a file beside the program
/// like the rest of them. Every call into it is guarded, because it may not be there at all: a
/// checkout that has not fetched it, or a platform nothing was built for.
///
/// One lock over the whole of it. What is on the bus is read by the thread that renders and
/// written by whichever thread started a pad or opened a song, and the list is small enough that
/// nothing is bought by being clever about it.
/// </remarks>
public sealed class OutputBus : IOutputBus
{
    /// <summary>Held while the bus is opened, closed, or its sources change.</summary>
    private readonly object _lock = new();

    /// <summary>What is plugged in, so a source is not added twice and can all be let go at once.</summary>
    private readonly HashSet<int> _sources = new();

    /// <summary>Whether the add-on answered, or nothing until it has been asked.</summary>
    private bool? _present;

    /// <summary>The mixer stream, or nought.</summary>
    private int _handle;

    /// <summary>How loud this bus is, kept so it survives the stream being made again.</summary>
    private float _level = 1f;

    /// <summary>How much is held ahead of the card, kept for the same reason as the level.</summary>
    private int _bufferMs;

    /// <inheritdoc/>
    public bool Present
    {
        get
        {
            lock (_lock)
            {
                if (_present is { } already) return already;

                try
                {
                    int probe = BassMix.CreateMixerStream(44100, 2, BassFlags.Decode);

                    if (probe != 0) Bass.StreamFree(probe);

                    _present = true;
                }
                catch (Exception ex)
                {
                    _present = false;

                    Log.Write(LogArea.Audio, () => "bus: bassmix is not available, " + ex.GetType().Name);
                }

                return _present.Value;
            }
        }
    }

    /// <inheritdoc/>
    public int Handle
    {
        get { lock (_lock) return _handle; }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Written into the channel as well as remembered, so it can be moved while the music runs,
    /// which is what changing it in SETTINGS does. Meaningless on a bus a driver pulls, since
    /// there the driver's own block is the buffer, and harmless to set there.
    /// </remarks>
    public int BufferMs
    {
        get { lock (_lock) return _bufferMs; }

        set
        {
            lock (_lock)
            {
                _bufferMs = Math.Max(0, value);

                if (_handle == 0 || _bufferMs == 0) return;

                Bass.ChannelSetAttribute(_handle, ChannelAttribute.Buffer, _bufferMs / 1000f);
            }
        }
    }

    /// <inheritdoc/>
    public bool IsOpen
    {
        get { lock (_lock) return _handle != 0; }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Clamped rather than refused, since a level is a number somebody dragged and the ends are
    /// where a drag lands. Written into the channel where there is one, and remembered either way.
    ///
    /// NaN is not clamped, it is refused, and that is not the same rule said twice.
    /// <see cref="Math.Clamp(float, float, float)"/> hands NaN straight back by design, so a
    /// clamp reads as a guard and is not one: the NaN would go into the channel and the whole bus
    /// with it. This codebase has paid for that exact line twice already, in <c>ToneFilter</c>'s
    /// resonance and in the ducker, and this is the third place it would have been true. A level
    /// that is not a number is nothing anybody dragged to, so what was there stays.
    /// </remarks>
    public float Level
    {
        get { lock (_lock) return _level; }

        set
        {
            if (float.IsNaN(value)) return;

            lock (_lock)
            {
                _level = Math.Clamp(value, 0f, 1f);

                if (_handle == 0) return;

                Bass.ChannelSetAttribute(_handle, ChannelAttribute.Volume, _level);
            }
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// <see cref="BassFlags.MixerNonStop"/> is load bearing rather than a nicety. Without it a bus
    /// with nothing plugged in stalls, and a stalled bus under a driver is the driver pulling from
    /// something that has stopped producing. A stopped transport with no pad down is the ordinary
    /// state of this application, so that is the state it would spend most of its life in.
    ///
    /// Float, because everything above it is float already and a conversion each way on the whole
    /// mix is the one place it would cost something.
    /// </remarks>
    public bool Open(int rate, int channels, bool pulled)
    {
        if (!Present || rate <= 0 || channels <= 0) return false;

        lock (_lock)
        {
            CloseLocked();

            var flags = BassFlags.Float | BassFlags.MixerNonStop | (pulled ? BassFlags.Decode : BassFlags.Default);

            try
            {
                _handle = BassMix.CreateMixerStream(rate, channels, flags);
            }
            catch (Exception ex)
            {
                Log.Fault(LogArea.Audio, "the output bus could not be made", ex);
                _handle = 0;

                return false;
            }

            if (_handle == 0)
            {
                Log.Write(LogArea.Audio, () => "bus: the bus would not open: " + Bass.LastError);

                return false;
            }

            Bass.ChannelSetAttribute(_handle, ChannelAttribute.Volume, _level);

            if (_bufferMs > 0)
                Bass.ChannelSetAttribute(_handle, ChannelAttribute.Buffer, _bufferMs / 1000f);

            Log.Write(LogArea.Audio, () =>
            {
                Bass.ChannelGetAttribute(_handle, ChannelAttribute.Buffer, out float held);

                return "bus: open at " + rate + " Hz, " + channels + " channels, "
                    + (pulled ? "pulled by the driver" : "playing itself")
                    + ", holding " + (int)Math.Round(held * 1000) + " ms";
            });

            return true;
        }
    }

    /// <inheritdoc/>
    public bool Add(int source)
    {
        if (source == 0) return false;

        lock (_lock)
        {
            if (_handle == 0) return false;
            if (_sources.Contains(source)) return true;

            bool took;

            try
            {
                took = BassMix.MixerAddChannel(_handle, source, BassFlags.MixerChanBuffer);
            }
            catch (Exception ex)
            {
                Log.Fault(LogArea.Audio, "a source could not be put on the output bus", ex);

                return false;
            }

            if (!took)
            {
                Log.Write(LogArea.Audio, () =>
                    "bus: source " + source + " was refused: " + Bass.LastError
                    + ", which is usually a channel that is not a decoding one");

                return false;
            }

            _sources.Add(source);

            Log.Write(LogArea.Audio, () => "bus: source " + source + " is on, " + _sources.Count + " in all");

            return true;
        }
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Said out loud, and it is the other half of the line <see cref="Add"/> writes. Without it a
    /// log shows sources joining and never leaving, so whether anything is taken off has to be
    /// inferred from the count starting again rather than read: a pad that finished and stayed on
    /// the bus for ever looks exactly like a pad that was never fired again. Only for a source
    /// that really was on, so a stranger stays as quiet as it always was.
    /// </remarks>
    public void Remove(int source)
    {
        lock (_lock)
        {
            if (!_sources.Remove(source)) return;

            RemoveLocked(source);

            Log.Write(LogArea.Audio, () => "bus: source " + source + " is off, " + _sources.Count + " left");
        }
    }

    /// <inheritdoc/>
    public bool Holds(int source)
    {
        lock (_lock) return _sources.Contains(source);
    }

    /// <inheritdoc/>
    public void Close()
    {
        lock (_lock) CloseLocked();
    }

    /// <inheritdoc/>
    public void Dispose() => Close();

    /// <summary>Unplugs one source, with the lock already held and the record already updated.</summary>
    /// <remarks>
    /// Guarded and quiet about a refusal. The add-on answers false for a channel that is not on a
    /// bus, and the two ways to reach that are a source somebody freed and a bus that has already
    /// gone, neither of which is worth a line.
    /// </remarks>
    /// <param name="source">The channel to unplug.</param>
    private static void RemoveLocked(int source)
    {
        try
        {
            BassMix.MixerRemoveChannel(source);
        }
        catch (Exception ex)
        {
            Log.Fault(LogArea.Audio, "a source could not be taken off the output bus", ex);
        }
    }

    /// <summary>Lets the bus go, with the lock already held.</summary>
    /// <remarks>
    /// The sources are unplugged first and never freed: they belong to the pads, the tracker and
    /// RECORD, each of which frees its own. Freeing the bus with sources still on it is the
    /// add-on's business rather than a crash, but unplugging says what is meant.
    /// </remarks>
    private void CloseLocked()
    {
        if (_handle == 0)
        {
            _sources.Clear();

            return;
        }

        foreach (int source in _sources) RemoveLocked(source);

        if (_sources.Count > 0)
            Log.Write(LogArea.Audio, () => "bus: " + _sources.Count + " source(s) taken off as the bus closes");

        _sources.Clear();

        try
        {
            Bass.StreamFree(_handle);

            Log.Write(LogArea.Audio, "bus: the bus has been let go");
        }
        catch (Exception ex)
        {
            Log.Fault(LogArea.Audio, "the output bus would not let go", ex);
        }

        _handle = 0;
    }
}
