using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
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

    /// <summary>Where it sits between the speakers, kept for the same reason as the level.</summary>
    private double _pan;

    /// <summary>Whether it is silenced, kept for the same reason.</summary>
    private bool _mute;

    /// <summary>
    /// The peak reader hung on the bus, kept because BASS holds it and calls it later.
    /// </summary>
    /// <remarks>
    /// Made once and kept, since a delegate made at the call to <c>Bass.ChannelSetDSP</c> is
    /// collected while BASS is still holding it, and the next block is then a crash inside the
    /// library.
    /// </remarks>
    private readonly DSPProcedure _peakProcedure;

    /// <summary>The peak reader's own handle, or nought while the bus is not open.</summary>
    private int _peak;

    /// <summary>What the last block peaked at on the left, written by whichever thread mixes.</summary>
    private volatile float _left;

    /// <summary>And on the right.</summary>
    private volatile float _right;

    /// <summary>The block as BASS wrote it, kept so a reading does not allocate per block.</summary>
    private float[] _block = Array.Empty<float>();

    /// <summary>How loud a block was, which is the whole of what a meter reads.</summary>
    private readonly IStereoPeak _peaks;

    /// <summary>Builds a bus that is not open.</summary>
    /// <param name="peaks">How a block is measured, or the ordinary walk.</param>
    public OutputBus(IStereoPeak? peaks = null)
    {
        _peaks = peaks ?? new StereoPeak();
        _peakProcedure = ReadPeak;
    }

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

                SayLevelLocked();
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

            SayLevelLocked();

            WatchLocked();

            Bass.ChannelSetAttribute(_handle, ChannelAttribute.Pan, (float)_pan);

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
    /// <remarks>
    /// NaN is refused rather than clamped, the same rule and the same reason as <see cref="Level"/>.
    /// </remarks>
    public double Pan
    {
        get { lock (_lock) return _pan; }

        set
        {
            if (double.IsNaN(value)) return;

            lock (_lock)
            {
                _pan = Math.Clamp(value, -1, 1);

                if (_handle == 0) return;

                Bass.ChannelSetAttribute(_handle, ChannelAttribute.Pan, (float)_pan);
            }
        }
    }

    /// <inheritdoc/>
    public bool Mute
    {
        get { lock (_lock) return _mute; }

        set
        {
            lock (_lock)
            {
                if (_mute == value) return;

                _mute = value;

                SayLevelLocked();
            }
        }
    }

    /// <summary>
    /// Writes the level and the mute into the channel as the one number BASS has for both.
    /// </summary>
    /// <remarks>
    /// Both go through here so neither can overwrite the other: setting the fader while muted
    /// must not unmute, and unmuting must put back where the fader stands rather than unity.
    /// </remarks>
    private void SayLevelLocked()
    {
        if (_handle == 0) return;

        Bass.ChannelSetAttribute(_handle, ChannelAttribute.Volume, _mute ? 0f : _level);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The bus reads itself, which is the whole of why it is the same answer everywhere. Nothing
    /// here asks BASS what the level is, because that question has a different right form for
    /// each of the three ways a bus can be driven and one of the three only happens on Windows.
    /// </remarks>
    public (float Left, float Right) Reading
    {
        get
        {
            lock (_lock)
                if (_handle == 0) return (0, 0);

            return (_left, _right);
        }
    }

    /// <summary>Puts the peak reader on the bus, with the lock already held.</summary>
    /// <remarks>
    /// **A bus is metered from the audio going through it and never by asking BASS for a level,
    /// and that is what makes the answer the same on every platform.** There are three ways a bus
    /// here is driven and the ordinary call is right for exactly one of them. A bus that plays
    /// itself can be asked, and the answer costs nothing. A bus that is a source on another one
    /// is a decoding channel, where the ordinary call measures by decoding data out and throwing
    /// it away, so the mix loses whatever the meter took: that is not a theory, it is what the
    /// tracker's own meter did for an afternoon, and it presented as the whole song wandering out
    /// of time rather than as anything to do with a meter. The add-on's own call is the answer
    /// there, and it is the answer only there, because it wants a channel that is plugged into a
    /// mixer and refuses anything else.
    ///
    /// The third way is the one no call of BASS's answers. **A bus something pulls is plugged
    /// into nothing**: it is a decoding channel, so the ordinary call would eat the audio, and it
    /// is a source on no mixer, so the add-on's call answers that it is not available. That is an
    /// ASIO driver holding the output on Windows and the sound server holding it on Linux, and
    /// the meters on the desk and on the patchbay are the ones that would read nought.
    ///
    /// A reader on the block has none of those cases in it. It runs where the audio is, whatever
    /// is pulling it, it takes nothing out of the mix, and it needs to know nothing about how the
    /// bus was opened. What it reads is what is on the bus before the bus's own fader and mute,
    /// which is what the calls it replaces read as well.
    /// </remarks>
    private void WatchLocked()
    {
        _left = 0;
        _right = 0;

        _peak = Bass.ChannelSetDSP(_handle, _peakProcedure);

        if (_peak == 0)
            Log.Write(LogArea.Audio, () => "bus: the meter would not go on the bus: " + Bass.LastError);
    }

    /// <summary>Takes the peak reader off, with the lock already held.</summary>
    /// <remarks>
    /// Freeing the stream would take it with it. Said out loud all the same, because the reading
    /// has to fall to nought as well: a bus that is closed while something was going through it
    /// would otherwise leave its last peak standing on a meter for the rest of the session.
    /// </remarks>
    private void UnwatchLocked()
    {
        if (_handle != 0 && _peak != 0) Bass.ChannelRemoveDSP(_handle, _peak);

        _peak = 0;

        _left = 0;
        _right = 0;
    }

    /// <summary>One block on its way through, measured and left exactly as it was.</summary>
    /// <remarks>
    /// On whichever thread is mixing, so it allocates nothing once it has a block to copy into
    /// and grows only where a longer one arrives.
    ///
    /// The bus is float and stereo, which is how it is opened, so the block is pairs, and how
    /// loud a block of those is is <see cref="IStereoPeak"/> rather than a walk written out here:
    /// that is the half that can be put a question to without a sound card.
    /// </remarks>
    /// <param name="handle">The reader's own handle, which is not used.</param>
    /// <param name="channel">The bus, which is not used.</param>
    /// <param name="buffer">The block.</param>
    /// <param name="length">How many bytes of it there are.</param>
    /// <param name="user">Nothing was handed over.</param>
    private void ReadPeak(int handle, int channel, IntPtr buffer, int length, IntPtr user)
    {
        if (buffer == IntPtr.Zero || length <= 0) return;

        int floats = length / sizeof(float);

        if (floats < 2) return;

        if (_block.Length < floats) _block = new float[floats];

        Marshal.Copy(buffer, _block, 0, floats);

        var (left, right) = _peaks.Of(_block, floats);

        _left = left;
        _right = right;
    }

    /// <inheritdoc/>
    public bool Add(int source)
    {
        if (source == 0) return false;

        lock (_lock)
        {
            if (_handle == 0) return false;

            if (HoldsLocked(source))
            {
                _sources.Add(source);

                return true;
            }

            bool took;

            try
            {
                RemoveLocked(source);

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
    public void HearOnly(IReadOnlyCollection<int> sources)
    {
        lock (_lock)
        {
            if (_handle == 0) return;

            foreach (int source in _sources)
            {
                bool heard = sources.Count == 0 || sources.Contains(source);

                try
                {
                    BassMix.ChannelFlags(
                        source,
                        heard ? BassFlags.Default : BassFlags.MixerChanPause,
                        BassFlags.MixerChanPause);
                }
                catch (Exception ex)
                {
                    Log.Fault(LogArea.Audio, "a source could not be paused or let go on the bus", ex);
                }
            }

            Log.Write(LogArea.Audio, () =>
                sources.Count == 0
                    ? "bus: hearing all " + _sources.Count + " sources again"
                    : "bus: hearing " + sources.Count + " of " + _sources.Count + " sources");
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
            if (!HoldsLocked(source)) return;

            RemoveLocked(source);

            Log.Write(LogArea.Audio, () => "bus: source " + source + " is off, " + _sources.Count + " left");
        }
    }

    /// <inheritdoc/>
    public bool Holds(int source)
    {
        lock (_lock) return HoldsLocked(source);
    }

    /// <inheritdoc/>
    public int Sources
    {
        get
        {
            lock (_lock)
            {
                int on = 0;

                foreach (int source in _sources)
                    if (HoldsLocked(source))
                        on++;

                return on;
            }
        }
    }

    /// <summary>Whether this bus really has the channel, with the lock already held.</summary>
    /// <remarks>
    /// **The mixer is asked and <see cref="_sources"/> is not, and that is the whole of the
    /// switch.** A connect point is a turnout: sending a source to another bus throws it away from
    /// this one, and it is thrown by the add-on rather than by anybody here, so the first this bus
    /// hears of it is nothing at all. Its record still names the channel, and a record that
    /// outlives the fact is worse than no record: asked whether it holds the song it says yes,
    /// <see cref="Add"/> does nothing on the strength of that, and the cable is drawn over a
    /// turnout that never moved. Both ways round, which is how it was found: pointed at the desk
    /// the song was silent, and pointed at the recorder it was still coming out of the desk.
    ///
    /// The set is what this bus asked for, which is what tells a source of ours from a stranger
    /// when the bus closes. It is the answer only where the add-on will not speak at all.
    /// </remarks>
    /// <param name="source">The channel being asked about.</param>
    private bool HoldsLocked(int source)
    {
        if (_handle == 0 || source == 0) return false;

        try
        {
            return BassMix.ChannelGetMixer(source) == _handle;
        }
        catch (Exception)
        {
            return _sources.Contains(source);
        }
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

        foreach (int source in _sources)
            if (HoldsLocked(source))
                RemoveLocked(source);

        if (_sources.Count > 0)
            Log.Write(LogArea.Audio, () => "bus: " + _sources.Count + " source(s) taken off as the bus closes");

        _sources.Clear();

        UnwatchLocked();

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
