using System;
using System.Runtime.InteropServices;
using JingleBox2.Tracker.Synth;
using ManagedBass;

namespace JingleBox2.Audio;

/// <summary>
/// The synth's way out to the speakers: one BASS stream that BASS pulls from, filled by the
/// mixer. A single stream for every synth voice rather than a channel per note, because the
/// voices are generated here and mixing them in managed code is cheaper than handing BASS
/// dozens of channels.
/// </summary>
public sealed class SynthOutput : IDisposable
{
    /// <summary>What the engine runs at when nothing better is known.</summary>
    public const int DefaultSampleRate = 44100;

    /// <summary>Asking for the device's own rate rather than naming one.</summary>
    public const int FollowDevice = 0;

    public const int Channels = 2;

    /// <summary>
    /// How far ahead this stream is buffered. Short, because a note typed on a keyboard has to
    /// sound now; BASS is told to update more often to keep a buffer this small fed.
    /// </summary>
    public const float BufferSeconds = 0.06f;

    /// <summary>Milliseconds between BASS buffer updates. The default is far too slow for the above.</summary>
    public const int UpdatePeriodMs = 10;

    private readonly object _lock = new();

    // The delegate has to outlive the call that hands it to BASS: BASS keeps calling it from
    // its own thread, and a collected delegate is a crash rather than a silence.
    private StreamProcedure? _procedure;

    private float[] _scratch = Array.Empty<float>();
    private int _handle;
    private bool _disposed;

    /// <summary>
    /// How far ahead of the speakers the mixer is allowed to work, in frames. Zero is in step:
    /// the block is mixed inside the call that asked for it.
    /// </summary>
    /// <remarks>
    /// The reason this exists is plugins. A plugin runs in a process of its own and every block
    /// it plays is a message out and a message back, made from the thread that has ten
    /// milliseconds to fill a buffer. That thread cannot be asked to wait on somebody else's
    /// scheduler, and when it does, what comes out is a hole.
    ///
    /// So the mixing can be moved off it. A thread of our own runs ahead and leaves finished
    /// audio in a queue; the call from the sound card takes what is there and returns. A plugin
    /// that takes an extra few milliseconds now eats into the queue instead of into the
    /// output, and nothing inside the mixer changes, so every track still lines up with every
    /// other one exactly as it did.
    ///
    /// What it costs is the size of the queue: the sound you hear was mixed that long ago.
    /// </remarks>
    private int _cushion;

    /// <summary>Finished audio, waiting to be asked for. Written by the mixing thread, read by BASS.</summary>
    private float[] _queue = Array.Empty<float>();

    private int _queueHead;
    private int _queueCount;

    private readonly object _queueLock = new();
    private readonly System.Threading.AutoResetEvent _askedForMore = new(false);

    private System.Threading.Thread? _ahead;
    private volatile bool _mixing;
    private float[] _aheadScratch = Array.Empty<float>();

    /// <summary>Frames mixed at a time by the thread running ahead. Fixed, so plugins see one size.</summary>
    private const int AheadChunkFrames = 512;

    /// <summary>How many frames the queue could not supply, for the log to say so.</summary>
    private long _short;

    private long _complained;

    private SynthMixer? _mixer;
    private int _wanted = FollowDevice;

    /// <summary>
    /// What the engine is running at. Fixed for the life of the mixer: voices, filters and
    /// plugins all work their timings out from it, so it cannot move under them.
    /// </summary>
    public int SampleRate { get; private set; } = DefaultSampleRate;

    /// <summary>
    /// The mixer, built the first time anything asks for it. Late on purpose: until the audio
    /// device has been opened there is no way to know what rate to build it for.
    /// </summary>
    public SynthMixer Mixer => _mixer ??= new SynthMixer(SampleRate);

    /// <summary>True once the mixer exists, so a meter can ask without building one.</summary>
    public bool HasMixer => _mixer != null;

    /// <summary>
    /// Asks for a rate, or for the device's own with <see cref="FollowDevice"/>. Only heard
    /// before the mixer is built, which is why it comes from settings at startup.
    /// </summary>
    public void UseSampleRate(int rate)
    {
        if (_mixer != null) return;

        _wanted = rate;
        if (rate > 0) SampleRate = rate;
    }

    /// <summary>
    /// How far ahead to mix, in milliseconds. Zero mixes in step, which is what this did
    /// before there was a choice.
    /// </summary>
    /// <remarks>
    /// Read when the stream is opened, so a change takes effect the next time the audio starts.
    /// </remarks>
    public void UseRenderAhead(int milliseconds) => _aheadMilliseconds = Math.Clamp(milliseconds, 0, 200);

    private int _aheadMilliseconds;

    /// <summary>What the cushion actually works out to, for a page that wants to say so.</summary>
    public int RenderAheadMilliseconds => _aheadMilliseconds;

    public bool IsRunning => _handle != 0;

    /// <summary>
    /// The loudest thing this stream is putting out, 0 to 1. The tracker's half of the main
    /// output meter; the pads are the other half and are their own channels.
    /// </summary>
    public float Level
    {
        get
        {
            int handle = _handle;

            if (handle == 0) return 0;

            int raw = Bass.ChannelGetLevel(handle);

            if (raw == -1) return 0;

            return Math.Clamp(Math.Max((raw >> 16) & 0xFFFF, raw & 0xFFFF) / 32768f, 0f, 1f);
        }
    }

    /// <summary>
    /// Opens the stream on first use, and opens it again if it has gone. Safe to call before
    /// every note.
    /// </summary>
    /// <remarks>
    /// Changing the output device closes BASS and opens it again, which takes this stream with
    /// it without telling anybody. So the handle is not taken as proof: the stream has to still
    /// be running, or it is made again.
    /// </remarks>
    public void EnsureStarted(IAudioEngine audio)
    {
        lock (_lock)
        {
            if (_disposed) return;

            if (_handle != 0)
            {
                if (Bass.ChannelIsActive(_handle) == PlaybackState.Playing) return;

                Diagnostics.Log.Write(Diagnostics.LogArea.Audio, "the synth stream had gone; opening another");

                StopMixingAhead();

                _handle = 0;
                _procedure = null;
            }

            audio.EnsureInitialized();

            // The device is open now, so this is the first moment its rate can be asked for.
            // Running at the device's own rate means nothing is resampled on the way out, and
            // a plugin is told the rate it is actually being fed at.
            if (_mixer == null && _wanted == FollowDevice)
            {
                int rate = DeviceRate();
                if (rate > 0) SampleRate = rate;
            }

            Bass.Configure(Configuration.UpdatePeriod, UpdatePeriodMs);

            _procedure = Fill;
            _handle = Bass.CreateStream(SampleRate, Channels, BassFlags.Float, _procedure, IntPtr.Zero);

            Diagnostics.Log.Write(Diagnostics.LogArea.Audio, () =>
                _handle == 0
                    ? "the synth stream would not open: " + Bass.LastError
                    : "the synth stream is open at " + SampleRate + " Hz");

            if (_handle == 0)
            {
                _procedure = null;
                return;
            }

            StartMixingAhead();

            Bass.ChannelSetAttribute(_handle, ChannelAttribute.Buffer, BufferSeconds);
            Bass.ChannelPlay(_handle);
        }
    }

    /// <summary>What the output device is running at, or zero when it will not say.</summary>
    private static int DeviceRate()
    {
        try
        {
            var info = Bass.Info;
            return info.SampleRate;
        }
        catch (Exception)
        {
            return 0;
        }
    }

    /// <summary>Silences the voices. The stream stays open, ready for the next note.</summary>
    public void Silence()
    {
        if (_mixer != null) _mixer.StopAll();
    }

    /// <summary>
    /// The smallest and largest block asked for so far, so the size is written down when it is
    /// something new and not once a block.
    /// </summary>
    /// <remarks>
    /// A pair rather than the last size seen. On Linux the device asks for two sizes turn and
    /// turn about, so "say it when it changes" was every block, which is the audio thread
    /// opening and closing a file eighty times a second and a log too full to read.
    /// </remarks>
    private int _smallest;
    private int _largest;

    /// <summary>
    /// Starts the thread that mixes ahead, if a cushion has been asked for.
    /// </summary>
    private void StartMixingAhead()
    {
        StopMixingAhead();

        _cushion = _aheadMilliseconds <= 0 ? 0 : _aheadMilliseconds * SampleRate / 1000;

        if (_cushion <= 0)
        {
            Diagnostics.Log.Write(Diagnostics.LogArea.Audio, "the mixer runs in step with the sound card");
            return;
        }

        // Room for the cushion, a chunk being added and a big block being taken, so neither end
        // has to wait for the other.
        _queue = new float[(_cushion + AheadChunkFrames * 4) * Channels];
        _queueHead = 0;
        _short = 0;

        // The cushion starts as the silence it is. Without this the sound card asks for its
        // first block before the mixing thread has made anything, and gets a gap where the
        // whole point was to stop having gaps. What it costs is what it says on the setting:
        // this much of the beginning is quiet, once, and everything after it is on time.
        _queueCount = _cushion * Channels;
        Array.Clear(_queue, 0, _queueCount);

        _mixing = true;

        _ahead = new System.Threading.Thread(MixAhead)
        {
            IsBackground = true,
            Name = "mixing ahead",

            // Above everything ordinary, below the sound card's own. This thread has a deadline
            // of its own now: what it does not finish in time is a hole in the output.
            Priority = System.Threading.ThreadPriority.AboveNormal
        };

        _ahead.Start();

        Diagnostics.Log.Write(Diagnostics.LogArea.Audio, () =>
            "the mixer runs " + _aheadMilliseconds + " ms ahead of the sound card (" + _cushion + " frames)");
    }

    private void StopMixingAhead()
    {
        _mixing = false;
        _askedForMore.Set();

        var ahead = _ahead;
        _ahead = null;

        try { ahead?.Join(200); } catch (Exception) { }

        _cushion = 0;
    }

    /// <summary>
    /// Mixes whenever the queue has room, and waits when it is full.
    /// </summary>
    /// <remarks>
    /// This is where a plugin's round trip to its own process now happens, and it is a thread
    /// with a whole cushion of slack rather than the one the sound card is waiting on.
    /// </remarks>
    private void MixAhead()
    {
        var mixer = Mixer;

        if (_aheadScratch.Length < AheadChunkFrames * Channels)
            _aheadScratch = new float[AheadChunkFrames * Channels];

        while (_mixing)
        {
            int held;
            lock (_queueLock) held = _queueCount;

            if (held >= (_cushion + AheadChunkFrames) * Channels)
            {
                // Full enough. Woken by whatever takes some, and looked at anyway now and then
                // in case a wake-up was missed.
                _askedForMore.WaitOne(4);
                continue;
            }

            try
            {
                mixer.Render(_aheadScratch, AheadChunkFrames);
            }
            catch (Exception error)
            {
                Diagnostics.Log.Fault(Diagnostics.LogArea.Audio, "mixing ahead", error);
                Array.Clear(_aheadScratch, 0, AheadChunkFrames * Channels);
            }

            lock (_queueLock)
            {
                int room = _queue.Length - _queueCount;
                int put = Math.Min(room, AheadChunkFrames * Channels);

                // Two runs rather than a sample at a time: the tail of the ring, then the head.
                int at = (_queueHead + _queueCount) % _queue.Length;
                int first = Math.Min(put, _queue.Length - at);

                Array.Copy(_aheadScratch, 0, _queue, at, first);
                if (put > first) Array.Copy(_aheadScratch, first, _queue, 0, put - first);

                _queueCount += put;
            }
        }
    }

    /// <summary>
    /// Takes finished audio out of the queue, and silence for whatever is not there yet.
    /// </summary>
    /// <remarks>
    /// Silence rather than waiting. A queue that has run dry means the mixing thread is late,
    /// and the answer to being late is never to make the sound card late as well: one quiet
    /// moment is a click, and a blocked callback is every stream on the device stuttering.
    /// </remarks>
    private void TakeAhead(float[] into, int samples)
    {
        int got;

        lock (_queueLock)
        {
            got = Math.Min(samples, _queueCount);

            int first = Math.Min(got, _queue.Length - _queueHead);

            Array.Copy(_queue, _queueHead, into, 0, first);
            if (got > first) Array.Copy(_queue, 0, into, first, got - first);

            _queueHead = (_queueHead + got) % _queue.Length;
            _queueCount -= got;
        }

        if (got < samples)
        {
            Array.Clear(into, got, samples - got);
            _short += (samples - got) / Channels;

            // Said once a second at most, and from here rather than from the mixing thread,
            // because this is the end that knows the sound card went without.
            if (Diagnostics.Log.On(Diagnostics.LogArea.Audio) && Environment.TickCount64 - _complained > 1000)
            {
                _complained = Environment.TickCount64;

                long missing = _short;

                Diagnostics.Log.Write(Diagnostics.LogArea.Audio, () =>
                    "the cushion ran dry: " + missing + " frame(s) of silence so far. " +
                    "A bigger one in SETTINGS is what this is asking for.");
            }
        }

        _askedForMore.Set();
    }

    /// <summary>How many frames the cushion has failed to supply since the stream opened.</summary>
    public long Underruns => _short;

    private int Fill(int handle, IntPtr buffer, int length, IntPtr user)
    {
        int samples = length / sizeof(float);
        if (samples <= 0) return 0;

        if (Diagnostics.Log.On(Diagnostics.LogArea.Audio) && (samples < _smallest || samples > _largest || _largest == 0))
        {
            if (_smallest == 0 || samples < _smallest) _smallest = samples;
            if (samples > _largest) _largest = samples;

            int low = _smallest / Channels;
            int high = _largest / Channels;

            Diagnostics.Log.Write(Diagnostics.LogArea.Audio, () =>
                low == high
                    ? "the synth stream is asking for " + low + " frames at a time"
                    : "the synth stream is asking for between " + low + " and " + high + " frames at a time");
        }

        if (_scratch.Length < samples) _scratch = new float[samples];

        if (_cushion > 0) TakeAhead(_scratch, samples);
        else Mixer.Render(_scratch, samples / Channels);

        Marshal.Copy(_scratch, 0, buffer, samples);

        // Always a full buffer: returning less would tell BASS the stream has ended.
        return samples * sizeof(float);
    }

    public void Dispose()
    {
        int handle;
        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;

            handle = _handle;
            _handle = 0;
        }

        // The thread first: it is holding a mixer that is about to be told to stop.
        StopMixingAhead();

        Mixer.StopAll();

        if (handle != 0) Bass.StreamFree(handle);
        _procedure = null;
    }
}
