using System;
using System.Threading;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Sync.Interfaces;

namespace JingleBox2.Sync;

/// <inheritdoc/>
/// <remarks>
/// The native instance is made the first time somebody joins rather than when this is, since
/// Link starts threads of its own when it is made and a machine that never chooses it should
/// carry none of them.
///
/// One session state is kept and every read captures into it under one lock. Link's application
/// side takes a lock of its own anyway, so a second one here costs nothing, and it is what lets
/// one state serve the clock thread and the drawing thread without either making one per call.
///
/// The callbacks are static functions handed a <see cref="GCHandle"/> to this instance, because
/// a function Link calls from its own thread cannot be an instance method, and nothing thrown in
/// one may leave it: an exception crossing back into native code is the process gone.
/// </remarks>
public sealed class AbletonLink : IAbletonLink
{
    /// <summary>A tempo this close to another is the same tempo, which is what the echo guards ask.</summary>
    private const double SameTempo = 0.01;

    /// <summary>The tempo the timeline starts at before anybody has said otherwise.</summary>
    private const double StartingBpm = 120.0;

    /// <summary>Held around every call that reads or writes the session state.</summary>
    private readonly object _lock = new();

    /// <summary>The native instance, or nought until somebody first joins.</summary>
    private nint _link;

    /// <summary>The one session state every read captures into.</summary>
    private nint _state;

    /// <summary>What the callbacks are handed to find this instance again.</summary>
    private GCHandle _self;

    /// <summary>Whether the library loaded, worked out on the first ask.</summary>
    private bool? _present;

    /// <summary>The tempo last heard, kept as its bits so it is read and written whole.</summary>
    private long _tempoBits = BitConverter.DoubleToInt64Bits(StartingBpm);

    /// <summary>The tempo this peer last put to the session, so its own echo can be told apart.</summary>
    private long _proposedBits = BitConverter.DoubleToInt64Bits(double.NaN);

    /// <summary>The peers last heard.</summary>
    private int _peers;

    /// <summary>Whether start and stop are shared, kept so it can be said again to a fresh instance.</summary>
    private bool _sharesStartStop = true;

    /// <inheritdoc/>
    public bool Present => _present ??= Load();

    /// <inheritdoc/>
    public string? Missing => Present
        ? null
        : "The Ableton Link library (" + LibraryFile + ") is not beside the program, so Link cannot be used on this machine.";

    /// <inheritdoc/>
    public bool IsOn
    {
        get
        {
            lock (_lock) return _link != 0 && AbletonLinkAbi.IsEnabled(_link);
        }
    }

    /// <inheritdoc/>
    public int OffsetMilliseconds { get; set; }

    /// <inheritdoc/>
    public double Quantum { get; set; } = AbletonLinkLines.UsualQuantum;

    /// <inheritdoc/>
    public int Peers => Volatile.Read(ref _peers);

    /// <inheritdoc/>
    public double Tempo => BitConverter.Int64BitsToDouble(Interlocked.Read(ref _tempoBits));

    /// <inheritdoc/>
    public bool SharesStartStop
    {
        get => _sharesStartStop;
        set
        {
            _sharesStartStop = value;

            lock (_lock)
            {
                if (_link != 0) AbletonLinkAbi.EnableStartStopSync(_link, value);
            }
        }
    }

    /// <inheritdoc/>
    public event Action<int>? PeersMoved;

    /// <inheritdoc/>
    public event Action<double>? TempoHeard;

    /// <inheritdoc/>
    public event Action<bool>? PlayingHeard;

    /// <inheritdoc/>
    public void Use(bool on)
    {
        if (on && !Present)
        {
            Log.Write(LogArea.Tracker, () => "link: not joining, " + Missing);
            return;
        }

        lock (_lock)
        {
            if (_link == 0)
            {
                if (!on) return;

                Make();
            }

            if (AbletonLinkAbi.IsEnabled(_link) == on) return;

            AbletonLinkAbi.Enable(_link, on);
        }

        Log.Write(LogArea.Tracker, () => on
            ? "link: joined the network at " + Tempo.ToString("0.0") + " bpm, start and stop "
              + (SharesStartStop ? "shared" : "not shared")
            : "link: left the network");
    }

    /// <summary>Makes the native instance without joining the network.</summary>
    /// <remarks>
    /// For the tests alone, which have to reach the real library without becoming a peer: a test
    /// that joined would land in whatever session is on the network it runs on, and a tempo it
    /// proposed would move somebody's Live.
    /// </remarks>
    internal void Ready()
    {
        if (!Present) return;

        lock (_lock)
        {
            if (_link == 0) Make();
        }
    }

    /// <inheritdoc/>
    public long Now
    {
        get
        {
            lock (_lock) return _link == 0 ? 0 : AbletonLinkAbi.ClockMicros(_link);
        }
    }

    /// <inheritdoc/>
    public double BeatAt(long micros, double quantum)
    {
        lock (_lock)
        {
            if (!Capture()) return double.NaN;

            return AbletonLinkAbi.BeatAtTime(_state, micros, quantum);
        }
    }

    /// <inheritdoc/>
    public long TimeAt(double beat, double quantum)
    {
        lock (_lock)
        {
            if (!Capture()) return 0;

            return AbletonLinkAbi.TimeAtBeat(_state, beat, quantum);
        }
    }

    /// <inheritdoc/>
    public bool IsPlaying
    {
        get
        {
            lock (_lock) return Capture() && AbletonLinkAbi.IsPlaying(_state);
        }
    }

    /// <inheritdoc/>
    public void Propose(double bpm)
    {
        if (!double.IsFinite(bpm) || bpm <= 0) return;

        lock (_lock)
        {
            if (!Capture()) return;
            if (Math.Abs(AbletonLinkAbi.Tempo(_state) - bpm) < SameTempo) return;

            Interlocked.Exchange(ref _proposedBits, BitConverter.DoubleToInt64Bits(bpm));
            Interlocked.Exchange(ref _tempoBits, BitConverter.DoubleToInt64Bits(bpm));

            AbletonLinkAbi.SetTempo(_state, bpm, AbletonLinkAbi.ClockMicros(_link));
            AbletonLinkAbi.CommitAppSessionState(_link, _state);
        }

        Log.Write(LogArea.Tracker, () => "link: put " + bpm.ToString("0.0") + " bpm to the session");
    }

    /// <inheritdoc/>
    public void Play(bool playing)
    {
        if (!SharesStartStop) return;

        lock (_lock)
        {
            if (!Capture()) return;
            if (AbletonLinkAbi.IsPlaying(_state) == playing) return;

            AbletonLinkAbi.SetIsPlaying(_state, playing, AbletonLinkAbi.ClockMicros(_link));
            AbletonLinkAbi.CommitAppSessionState(_link, _state);
        }

        Log.Write(LogArea.Tracker, () => "link: told the session it " + (playing ? "started" : "stopped"));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_link == 0) return;

            AbletonLinkAbi.Enable(_link, false);
            AbletonLinkAbi.Destroy(_link);
            AbletonLinkAbi.DestroySessionState(_state);

            _link = 0;
            _state = 0;
        }

        if (_self.IsAllocated) _self.Free();
    }

    /// <summary>The library's file name on this platform, for the sentence that says it is missing.</summary>
    private static string LibraryFile => OperatingSystem.IsWindows() ? "abl_link.dll" : "libabl_link.so";

    /// <summary>Whether the library can be loaded, without calling anything in it.</summary>
    private static bool Load()
    {
        bool loaded = NativeLibrary.TryLoad(AbletonLinkAbi.Library, typeof(AbletonLink).Assembly, null, out _);

        if (!loaded) Log.Write(LogArea.Tracker, () => "link: " + LibraryFile + " could not be loaded");

        return loaded;
    }

    /// <summary>Makes the native instance and hangs the three callbacks on it. Called under the lock.</summary>
    private unsafe void Make()
    {
        _link = AbletonLinkAbi.Create(StartingBpm);
        _state = AbletonLinkAbi.CreateSessionState();
        _self = GCHandle.Alloc(this);

        nint context = GCHandle.ToIntPtr(_self);

        AbletonLinkAbi.EnableStartStopSync(_link, _sharesStartStop);
        AbletonLinkAbi.SetNumPeersCallback(_link, &OnPeers, context);
        AbletonLinkAbi.SetTempoCallback(_link, &OnTempo, context);
        AbletonLinkAbi.SetStartStopCallback(_link, &OnStartStop, context);

        AbletonLinkAbi.CaptureAppSessionState(_link, _state);
        Interlocked.Exchange(ref _tempoBits, BitConverter.DoubleToInt64Bits(AbletonLinkAbi.Tempo(_state)));
    }

    /// <summary>Reads the session into the kept state. Called under the lock; false where there is no session yet.</summary>
    private bool Capture()
    {
        if (_link == 0) return false;

        AbletonLinkAbi.CaptureAppSessionState(_link, _state);

        return true;
    }

    /// <summary>The instance a callback's context names, or null where it has gone.</summary>
    private static AbletonLink? From(nint context) =>
        context == 0 ? null : GCHandle.FromIntPtr(context).Target as AbletonLink;

    /// <summary>Link's own thread saying how many peers there are.</summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnPeers(ulong peers, nint context)
    {
        try
        {
            if (From(context) is not { } self) return;

            int count = (int)Math.Min(peers, int.MaxValue);

            Volatile.Write(ref self._peers, count);
            Log.Write(LogArea.Tracker, () => "link: " + count + " other peer(s) in the session");
            self.PeersMoved?.Invoke(count);
        }
        catch (Exception ex)
        {
            Log.Write(LogArea.Tracker, () => "link: a peer count listener threw " + ex);
        }
    }

    /// <summary>Link's own thread saying the tempo moved.</summary>
    /// <remarks>
    /// Kept whoever moved it, and only told onwards where it was not this peer's own proposal
    /// coming back: written into the song from here, an automation lane moving the tempo would
    /// otherwise edit the song's own tempo on every line it moved.
    /// </remarks>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnTempo(double bpm, nint context)
    {
        try
        {
            if (From(context) is not { } self) return;

            Interlocked.Exchange(ref self._tempoBits, BitConverter.DoubleToInt64Bits(bpm));

            double proposed = BitConverter.Int64BitsToDouble(Interlocked.Read(ref self._proposedBits));

            if (Math.Abs(proposed - bpm) < SameTempo) return;

            Log.Write(LogArea.Tracker, () => "link: the session moved to " + bpm.ToString("0.0") + " bpm");
            self.TempoHeard?.Invoke(bpm);
        }
        catch (Exception ex)
        {
            Log.Write(LogArea.Tracker, () => "link: a tempo listener threw " + ex);
        }
    }

    /// <summary>Link's own thread saying the session started or stopped.</summary>
    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvCdecl)])]
    private static void OnStartStop(byte playing, nint context)
    {
        try
        {
            if (From(context) is not { } self) return;

            bool on = playing != 0;

            Log.Write(LogArea.Tracker, () => "link: the session " + (on ? "started" : "stopped"));
            self.PlayingHeard?.Invoke(on);
        }
        catch (Exception ex)
        {
            Log.Write(LogArea.Tracker, () => "link: a start and stop listener threw " + ex);
        }
    }
}
