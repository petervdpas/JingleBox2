using System;
using System.Runtime.InteropServices;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Audio.Records;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using ManagedBass;

namespace JingleBox2.Audio;

/// <inheritdoc/>
/// <remarks>
/// A hook on the bus itself, which is what puts it before the level: BASS runs it as the bus
/// produces its block and whatever pulls the bus applies the level afterwards.
///
/// It allocates a block per block, which the mixing path may not and this may. It is hooked only
/// while a take is being made and only where something is patched into RECORD, so what it costs
/// is paid during a recording rather than for the life of the session, and the alternative is a
/// second spelling of <see cref="ISixteenBit"/> written to avoid one small array.
/// </remarks>
public sealed class TakeTap : ITakeTap
{
    /// <summary>Held while the hook is made, dropped or read.</summary>
    private readonly object _lock = new();

    /// <summary>What was kept, which is the same buffer a take off the capture goes into.</summary>
    private readonly ITakeBuffer _kept;

    /// <summary>How a block of the bus's floats becomes what everything above meets.</summary>
    private readonly ISixteenBit _sixteen;

    /// <summary>The bus being kept, or nothing.</summary>
    private IOutputBus? _bus;

    /// <summary>The hook on it, or nought.</summary>
    private int _dsp;

    /// <summary>The channel the hook is on, so it can be taken off the one it was put on.</summary>
    private int _hooked;

    /// <summary>Kept so the delegate is not collected while BASS is holding it.</summary>
    private readonly DSPProcedure _dspProcedure;

    /// <summary>What a block is read into on its way out of the pointer BASS hands over.</summary>
    private byte[] _block = Array.Empty<byte>();

    /// <summary>Makes one over the buffer and the conversion it uses.</summary>
    /// <param name="kept">Where what is read goes, or the ordinary buffer.</param>
    /// <param name="sixteen">How the bus's floats are read, or the ordinary rule.</param>
    public TakeTap(ITakeBuffer? kept = null, ISixteenBit? sixteen = null)
    {
        _kept = kept ?? new TakeBuffer();
        _sixteen = sixteen ?? new SixteenBit();
        _dspProcedure = OnDsp;
    }

    /// <inheritdoc/>
    public void Follow(IOutputBus bus)
    {
        lock (_lock) _bus = bus;
    }

    /// <inheritdoc/>
    public int Rate { get; private set; }

    /// <inheritdoc/>
    public int Channels { get; private set; }

    /// <inheritdoc/>
    public bool Mixed { get; private set; }

    /// <inheritdoc/>
    public byte[] Take => _kept.Take;

    /// <inheritdoc/>
    public void Start()
    {
        lock (_lock)
        {
            _kept.Reset();

            Mixed = false;
            Rate = 0;
            Channels = 0;

            if (_bus is not { } bus || bus.Handle == 0 || bus.Sources <= 1) return;

            if (!Bass.ChannelGetInfo(bus.Handle, out var info)) return;

            Rate = info.Frequency;
            Channels = info.Channels;
            Mixed = true;

            HookLocked(bus.Handle);

            _kept.Start();

            Log.Write(LogArea.Audio, () =>
                "take: reading the recorder's bus, " + bus.Sources + " source(s) at "
                + Rate + " Hz, " + Channels + " channels");
        }
    }

    /// <inheritdoc/>
    public byte[] Stop()
    {
        lock (_lock)
        {
            DropLocked();

            byte[] kept = _kept.Stop();

            if (Mixed)
                Log.Write(LogArea.Audio, () => "take: " + kept.Length + " byte(s) came off the recorder's bus");

            return kept;
        }
    }

    /// <inheritdoc/>
    public void Close()
    {
        lock (_lock)
        {
            DropLocked();

            _bus = null;
        }
    }

    /// <summary>Puts the hook on one channel, with the lock held.</summary>
    /// <param name="channel">The bus's stream.</param>
    private void HookLocked(int channel)
    {
        DropLocked();

        _dsp = Bass.ChannelSetDSP(channel, _dspProcedure);
        _hooked = _dsp == 0 ? 0 : channel;

        if (_dsp == 0)
            Log.Write(LogArea.Audio, () => "take: the recorder's bus could not be read: " + Bass.LastError);
    }

    /// <summary>Takes the hook off whatever it was on, with the lock held.</summary>
    private void DropLocked()
    {
        if (_dsp == 0) return;

        Bass.ChannelRemoveDSP(_hooked, _dsp);

        _dsp = 0;
        _hooked = 0;
    }

    /// <summary>
    /// A block of the bus, on its way past.
    /// </summary>
    /// <remarks>
    /// Nothing here takes the lock. BASS waits for this to return, and the thread that starts or
    /// stops a take is inside a BASS call while it holds one, which is the deadlock this codebase
    /// has already written down once. What it touches is its own buffer and one array, and the
    /// buffer takes a lock of its own.
    /// </remarks>
    /// <param name="handle">The hook this came from.</param>
    /// <param name="channel">The bus being pulled.</param>
    /// <param name="buffer">The block, which is left exactly as it is.</param>
    /// <param name="length">How many bytes of it there are.</param>
    /// <param name="user">Unused, since there is one of these.</param>
    private void OnDsp(int handle, int channel, IntPtr buffer, int length, IntPtr user)
    {
        if (buffer == IntPtr.Zero || length <= 0) return;

        if (_block.Length < length) _block = new byte[length];

        Marshal.Copy(buffer, _block, 0, length);

        _kept.Add(_sixteen.Down(_block, length, new CaptureFormat(Rate, Channels, 32, Floats: true)));
    }
}
