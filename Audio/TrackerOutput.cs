using System;
using System.Runtime.InteropServices;
using JingleBox2.Tracker.Synth;
using ManagedBass;
using JingleBox2.Audio.Interfaces;

namespace JingleBox2.Audio;

/// <inheritdoc/>
/// <remarks>
/// Through BASS, as a stream BASS pulls from on its own thread.
/// </remarks>
public sealed class TrackerOutput : ITrackerOutput
{
    /// <summary>What the engine runs at when nothing better is known.</summary>
    public const int DefaultSampleRate = 44100;

    /// <summary>Asking for the device's own rate rather than naming one.</summary>
    public const int FollowDevice = 0;

    /// <summary>How many channels the stream carries, which is what the mixer works in.</summary>
    public const int Channels = 2;

    /// <summary>
    /// How far ahead this stream is buffered. Short, because a note typed on a keyboard has to
    /// sound now; BASS is told to update more often to keep a buffer this small fed.
    /// </summary>
    public const float BufferSeconds = 0.06f;

    /// <summary>Milliseconds between BASS buffer updates. The default is far too slow for the above.</summary>
    public const int UpdatePeriodMs = 10;

    /// <summary>
    /// What the audio is sized at, which is the two constants above until somebody says otherwise.
    /// </summary>
    /// <remarks>
    /// Held rather than read from the settings here, because this class knows about a sound card
    /// and nothing about a settings file. Whoever has both hands it over.
    /// </remarks>
    private Records.AudioSizes _sizes = new(2048, UpdatePeriodMs, 0);

    /// <summary>What the buffer comes to in milliseconds at the rate in force.</summary>
    private int BufferMs =>
        Math.Max(1, (int)Math.Round(_sizes.BufferFrames * 1000.0 / Math.Max(1, SampleRate)));

    /// <inheritdoc/>
    public void UseSizes(Records.AudioSizes sizes) => _sizes = sizes;

    /// <summary>Held while the stream is opened or closed.</summary>
    private readonly object _lock = new();

    /// <summary>
    /// What BASS calls to be given audio, kept here because it has to outlive the call that
    /// handed it over: BASS goes on calling it from its own thread, and a collected delegate is a
    /// crash rather than a silence.
    /// </summary>
    private StreamProcedure? _procedure;

    /// <summary>Where a block is put together, kept so the audio thread does not allocate.</summary>
    private float[] _scratch = Array.Empty<float>();

    /// <summary>The BASS stream, or 0 when it is not open.</summary>
    private int _handle;

    /// <summary>Whether this has been thrown away, so nothing opens the stream again after.</summary>
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
    ///
    /// Volatile because it is the answer to "which of the two ways is running", written by the
    /// drawing thread while starting or stopping and read by the sound card's own thread on
    /// every block. See <c>docs/threads.md</c>.
    /// </remarks>
    private volatile int _cushion;

    /// <summary>Finished audio, waiting to be asked for. Written by the mixing thread, read by BASS.</summary>
    private float[] _queue = Array.Empty<float>();

    /// <summary>Where the next sample to be taken sits in the ring.</summary>
    private int _queueHead;

    /// <summary>How many samples the ring is holding.</summary>
    private int _queueCount;

    /// <summary>Held while the ring is read or written, by either thread.</summary>
    private readonly object _queueLock = new();

    /// <summary>Raised when the sound card takes some, so the mixing thread wakes and refills.</summary>
    private readonly System.Threading.AutoResetEvent _askedForMore = new(false);

    /// <summary>The thread mixing ahead, or null when the mixing is in step.</summary>
    private System.Threading.Thread? _ahead;

    /// <summary>Whether that thread should carry on. Volatile, since it is cleared from another.</summary>
    private volatile bool _mixing;

    /// <summary>Where that thread mixes each chunk before putting it in the ring.</summary>
    private float[] _aheadScratch = Array.Empty<float>();

    /// <summary>Frames mixed at a time by the thread running ahead. Fixed, so plugins see one size.</summary>
    private const int AheadChunkFrames = 512;

    /// <summary>How long the mixing thread sleeps on a full queue before looking again.</summary>
    private const int FullCheckMs = 4;

    /// <summary>How many frames the queue could not supply, for the log to say so.</summary>
    private long _short;

    /// <summary>When the log last said the cushion had run dry, so it is said once a second.</summary>
    private long _complained;

    /// <summary>The mixer, once something has asked for it.</summary>
    private TrackMixer? _mixer;

    /// <summary>What rate was asked for, which may be <see cref="FollowDevice"/>.</summary>
    private int _wanted = FollowDevice;

    /// <inheritdoc/>
    public int SampleRate { get; private set; } = DefaultSampleRate;

    /// <inheritdoc/>
    public TrackMixer Mixer => _mixer ??= new TrackMixer(SampleRate);

    /// <inheritdoc/>
    public bool HasMixer => _mixer != null;

    /// <inheritdoc/>
    public void UseSampleRate(int rate)
    {
        if (_mixer != null) return;

        _wanted = rate;
        if (rate > 0) SampleRate = rate;
    }

    /// <inheritdoc/>
    /// <remarks>Clamped to a fifth of a second, past which the delay is the fault it was fixing.</remarks>
    public void UseRenderAhead(int milliseconds) => _aheadMilliseconds = Math.Clamp(milliseconds, 0, MostAheadMs);

    /// <summary>The largest cushion that can be asked for, in milliseconds.</summary>
    private const int MostAheadMs = 200;

    /// <summary>How far ahead to mix, in milliseconds.</summary>
    private int _aheadMilliseconds;

    /// <inheritdoc/>
    public int RenderAheadMilliseconds => _aheadMilliseconds;

    /// <inheritdoc/>
    public bool IsRunning => _handle != 0;

    /// <inheritdoc/>
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

    /// <inheritdoc/>
    /// <remarks>
    /// The device is open by the time the rate is asked for, which is the first moment it can be:
    /// running at the device's own rate means nothing is resampled on the way out, and a plugin is
    /// told the rate it is really being fed at.
    /// </remarks>
    public void EnsureStarted(IAudioEngine audio)
    {
        lock (_lock)
        {
            if (_disposed) return;

            if (_handle != 0)
            {
                if (Bass.ChannelIsActive(_handle) == PlaybackState.Playing) return;

                Diagnostics.Log.Write(Diagnostics.Enums.LogArea.Audio, "the tracker stream had gone; opening another");

                StopMixingAhead();

                _handle = 0;
                _procedure = null;
            }

            audio.EnsureInitialized();

            if (_mixer == null && _wanted == FollowDevice)
            {
                int rate = DeviceRate();
                if (rate > 0) SampleRate = rate;
            }

            Bass.Configure(Configuration.UpdatePeriod, _sizes.UpdatePeriodMs);

            if (_sizes.UpdateThreads > 0)
                Bass.Configure(Configuration.UpdateThreads, _sizes.UpdateThreads);

            bool driven = audio.OutputKind == Enums.AudioOutputKind.Asio;

            _procedure = Fill;
            _handle = Bass.CreateStream(SampleRate, Channels,
                driven ? BassFlags.Float | BassFlags.Decode : BassFlags.Float,
                _procedure, IntPtr.Zero);

            Diagnostics.Log.Write(Diagnostics.Enums.LogArea.Audio, () =>
                _handle == 0
                    ? "the tracker stream would not open: " + Bass.LastError
                    : "the tracker stream is open at " + SampleRate + " Hz, buffered "
                      + _sizes.BufferFrames + " frames (" + BufferMs + " ms), updated every " + _sizes.UpdatePeriodMs + " ms by "
                      + (_sizes.UpdateThreads > 0 ? _sizes.UpdateThreads + " threads" : "the library's own thread"));

            if (_handle == 0)
            {
                _procedure = null;
                return;
            }

            StartMixingAhead();

            if (driven && audio.Feed(_handle, SampleRate, _sizes.BufferFrames)) return;

            if (driven)
            {
                Diagnostics.Log.Write(Diagnostics.Enums.LogArea.Audio,
                    "the driver would not take the mix; playing it the ordinary way instead");

                Bass.StreamFree(_handle);

                _procedure = Fill;
                _handle = Bass.CreateStream(SampleRate, Channels, BassFlags.Float, _procedure, IntPtr.Zero);

                if (_handle == 0)
                {
                    _procedure = null;
                    return;
                }
            }

            Bass.ChannelSetAttribute(_handle, ChannelAttribute.Buffer, BufferMs / 1000f);
            Bass.ChannelPlay(_handle);
        }
    }

    /// <inheritdoc/>
    public void Restart(IAudioEngine audio)
    {
        lock (_lock)
        {
            if (_disposed) return;

            if (_handle != 0)
            {
                StopMixingAhead();

                Bass.StreamFree(_handle);

                _handle = 0;
                _procedure = null;
            }
        }

        EnsureStarted(audio);
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

    /// <inheritdoc/>
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

    /// <inheritdoc cref="_smallest"/>
    private int _largest;

    /// <summary>Starts the thread that mixes ahead, if a cushion has been asked for.</summary>
    /// <remarks>
    /// The ring holds the cushion, a chunk being added and a big block being taken, so neither end
    /// has to wait for the other. It starts full of the silence it is: without that the sound card
    /// asks for its first block before the mixing thread has made anything and gets a gap, where
    /// the whole point was to stop having gaps. What that costs is what the setting says, this
    /// much of the beginning quiet, once, and everything after it on time.
    ///
    /// The thread runs above everything ordinary and below the sound card's own, because it has a
    /// deadline of its own now: what it does not finish in time is a hole in the output.
    /// </remarks>
    private void StartMixingAhead()
    {
        StopMixingAhead();

        _cushion = _aheadMilliseconds <= 0 ? 0 : _aheadMilliseconds * SampleRate / 1000;

        if (_cushion <= 0)
        {
            Diagnostics.Log.Write(Diagnostics.Enums.LogArea.Audio, "the mixer runs in step with the sound card");
            return;
        }

        _queue = new float[(_cushion + AheadChunkFrames * 4) * Channels];
        _queueHead = 0;
        _short = 0;

        _queueCount = _cushion * Channels;
        Array.Clear(_queue, 0, _queueCount);

        _mixing = true;

        _ahead = new System.Threading.Thread(MixAhead)
        {
            IsBackground = true,
            Name = "mixing ahead",
            Priority = System.Threading.ThreadPriority.AboveNormal
        };

        _ahead.Start();

        Diagnostics.Log.Write(Diagnostics.Enums.LogArea.Audio, () =>
            "the mixer runs " + _aheadMilliseconds + " ms ahead of the sound card (" + _cushion + " frames)");
    }

    /// <summary>Stops that thread and waits a moment for it, and does nothing when there is none.</summary>
    /// <remarks>
    /// A moment and not for ever: a plugin taking its time inside a block must not hang the
    /// application, so this carries on regardless once the wait is up. That does mean the thread
    /// can still be inside the mixer when the sound card's own thread starts rendering in step,
    /// which is a real overlap and is guarded where it matters, in
    /// <see cref="Tracker.Synth.TrackMixer.Render"/>. It is said here because a thread that
    /// would not stop is worth knowing about on its own: it means a plugin took longer than a
    /// fifth of a second over one block.
    /// </remarks>
    private void StopMixingAhead()
    {
        _mixing = false;
        _askedForMore.Set();

        var ahead = _ahead;
        _ahead = null;

        bool stopped = true;

        try { if (ahead != null) stopped = ahead.Join(AheadStopMs); } catch (Exception) { }

        if (!stopped)
        {
            Diagnostics.Log.Write(Diagnostics.Enums.LogArea.Audio,
                "the mixing thread did not stop within " + AheadStopMs + " ms and was left to finish on its own");
        }

        _cushion = 0;
    }

    /// <summary>How long the mixing thread is given to notice it should stop.</summary>
    private const int AheadStopMs = 200;

    /// <summary>
    /// Mixes whenever the queue has room, and waits when it is full.
    /// </summary>
    /// <remarks>
    /// This is where a plugin's round trip to its own process now happens, and it is a thread
    /// with a whole cushion of slack rather than the one the sound card is waiting on.
    ///
    /// A full queue waits to be woken by whatever takes some, and looks anyway now and then in
    /// case a wake-up was missed. Each chunk goes into the ring in two runs rather than a sample
    /// at a time: the tail of it, then the head.
    ///
    /// **It asks for real-time scheduling from inside itself**, which is the only place it can:
    /// what the operating system is being asked about is the calling thread. Asking anywhere the
    /// sound library might be calling from is how the drawing thread once ended up under that
    /// scheduler, with fourteen other threads inheriting it, since a new thread on this platform
    /// takes the policy of the thread that made it. Off unless <c>JB_REALTIME=1</c>, and it may be
    /// refused: a refusal is ordinary and is written down, because "the buffer has to be enormous
    /// here" and "this machine will not grant real time" are the same fact and only one of them is
    /// findable.
    /// </remarks>
    private void MixAhead()
    {
        var scheduler = new RealtimeThread();

        scheduler.Take();

        Diagnostics.Log.Write(Diagnostics.Enums.LogArea.Audio, () =>
            "the mixing thread runs under " + scheduler.Said());

        var mixer = Mixer;

        if (_aheadScratch.Length < AheadChunkFrames * Channels)
            _aheadScratch = new float[AheadChunkFrames * Channels];

        while (_mixing)
        {
            int held;
            lock (_queueLock) held = _queueCount;

            if (held >= (_cushion + AheadChunkFrames) * Channels)
            {
                _askedForMore.WaitOne(FullCheckMs);
                continue;
            }

            try
            {
                mixer.Render(_aheadScratch, AheadChunkFrames);
            }
            catch (Exception error)
            {
                Diagnostics.Log.Fault(Diagnostics.Enums.LogArea.Audio, "mixing ahead", error);
                Array.Clear(_aheadScratch, 0, AheadChunkFrames * Channels);
            }

            lock (_queueLock)
            {
                int room = _queue.Length - _queueCount;
                int put = Math.Min(room, AheadChunkFrames * Channels);

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
    ///
    /// A dry queue is said once a second at most, and said from here rather than from the mixing
    /// thread, because this is the end that knows the sound card went without.
    /// </remarks>
    /// <param name="into">Where to put what there is.</param>
    /// <param name="samples">How many samples are wanted.</param>
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

            if (Diagnostics.Log.On(Diagnostics.Enums.LogArea.Audio) && Environment.TickCount64 - _complained > 1000)
            {
                _complained = Environment.TickCount64;

                long missing = _short;

                Diagnostics.Log.Write(Diagnostics.Enums.LogArea.Audio, () =>
                    "the cushion ran dry: " + missing + " frame(s) of silence so far. " +
                    "A bigger one in SETTINGS is what this is asking for.");
            }
        }

        _askedForMore.Set();
    }

    /// <inheritdoc/>
    public long Underruns => _short;

    /// <summary>Fills one block for the sound card, on its own thread.</summary>
    /// <remarks>
    /// A full buffer is always returned: handing back less would tell BASS the stream has ended.
    /// </remarks>
    /// <param name="handle">The stream being asked for.</param>
    /// <param name="buffer">Where the audio goes.</param>
    /// <param name="length">How many bytes are wanted.</param>
    /// <param name="user">Unused, since what this needs is on the instance.</param>
    /// <returns>How many bytes were written.</returns>
    private int Fill(int handle, IntPtr buffer, int length, IntPtr user)
    {
        int samples = length / sizeof(float);
        if (samples <= 0) return 0;

        if (Diagnostics.Log.On(Diagnostics.Enums.LogArea.Audio) && (samples < _smallest || samples > _largest || _largest == 0))
        {
            if (_smallest == 0 || samples < _smallest) _smallest = samples;
            if (samples > _largest) _largest = samples;

            int low = _smallest / Channels;
            int high = _largest / Channels;

            Diagnostics.Log.Write(Diagnostics.Enums.LogArea.Audio, () =>
                low == high
                    ? "the tracker stream is asking for " + low + " frames at a time"
                    : "the tracker stream is asking for between " + low + " and " + high + " frames at a time");
        }

        if (_scratch.Length < samples) _scratch = new float[samples];

        if (_cushion > 0) TakeAhead(_scratch, samples);
        else Mixer.Render(_scratch, samples / Channels);

        Marshal.Copy(_scratch, 0, buffer, samples);

        return samples * sizeof(float);
    }

    /// <summary>Stops the mixing, silences the voices and lets the stream go.</summary>
    /// <remarks>
    /// The thread first, since it is holding a mixer that is about to be told to stop.
    /// </remarks>
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

        StopMixingAhead();

        Mixer.StopAll();

        if (handle != 0) Bass.StreamFree(handle);
        _procedure = null;
    }
}
