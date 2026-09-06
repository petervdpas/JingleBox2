using System;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using ManagedBass;

namespace JingleBox2.Audio;

/// <inheritdoc/>
/// <remarks>
/// A push stream on the monitor bus. Push rather than a stream that asks for audio, because the
/// audio arrives when the capture says so rather than when the card asks, and there is nothing
/// here that could wait for the one on the other's thread.
/// </remarks>
public sealed class MonitorFeed : IMonitorFeed
{
    /// <summary>The bus this is the only source on, which is the IN strip's own.</summary>
    private readonly IOutputBus _bus;

    /// <summary>How a captured block becomes the floats the stream takes.</summary>
    private readonly IStereoFloats _floats;

    /// <summary>A block through the chain, the same pass a pad's chain goes through.</summary>
    private readonly IInsertPass _pass;

    /// <summary>Held while the stream is made, hooked or let go.</summary>
    private readonly object _lock = new();

    /// <summary>The push stream, or nought.</summary>
    private int _stream;

    /// <summary>The chain's hook on it, or nought.</summary>
    private int _dsp;

    /// <summary>Kept so the delegate is not collected while BASS is holding it.</summary>
    private readonly DSPProcedure _dspProcedure;

    /// <summary>The stereo buffer the chain works in.</summary>
    private float[] _scratch = new float[MostFrames * 2];

    /// <summary>What a captured block is read into on its way to the stream.</summary>
    private float[] _ready = new float[MostFrames * 2];

    /// <summary>
    /// The longest block the chain is prepared for, which is what decides the piece size.
    /// </summary>
    /// <remarks>
    /// The same number the pads use, and for the same reason: BASS hands out far less than this
    /// and a longer block is worked through in pieces rather than allocated for on the audio
    /// thread.
    /// </remarks>
    private const int MostFrames = 8192;

    /// <summary>
    /// How much audio may sit waiting to be heard before a block is dropped instead.
    /// </summary>
    /// <remarks>
    /// **A source that is paused is not pulled at all**, which is what somebody else's solo does
    /// to this one, so without a limit the queue would grow for as long as the input is open.
    /// Half a second is far more than the path ever holds when it is running and is a plain
    /// answer when it is not: dropping the newest block is right here, since what is wanted is
    /// the sound now rather than every sample of a stretch nobody was listening to.
    /// </remarks>
    private const double MostWaitingSeconds = 0.5;

    /// <summary>How many bytes that comes to at the rate the stream was opened at.</summary>
    private int _mostWaiting;

    /// <summary>Whether a block has already been dropped, so the log says it once.</summary>
    private bool _saidFull;

    /// <inheritdoc/>
    public Plugins.Interfaces.IAudioInsert? Insert { get; set; }

    /// <summary>
    /// Makes one over the bus it is the source of.
    /// </summary>
    /// <param name="bus">The monitor bus, which is the IN strip's own.</param>
    /// <param name="floats">How a captured block is read, or the ordinary rule.</param>
    /// <param name="pass">How a block goes through the chain, or the ordinary one.</param>
    public MonitorFeed(IOutputBus bus, IStereoFloats? floats = null, IInsertPass? pass = null)
    {
        _bus = bus;
        _floats = floats ?? new StereoFloats();
        _pass = pass ?? new InsertPass();
        _dspProcedure = OnDsp;
    }

    /// <inheritdoc/>
    public bool IsOpen
    {
        get { lock (_lock) return _stream != 0; }
    }

    /// <inheritdoc/>
    public bool Open(int rate, int channels)
    {
        lock (_lock)
        {
            CloseLocked();

            int made = Bass.CreateStream(
                Math.Max(1, rate), StreamChannels, BassFlags.Decode | BassFlags.Float, StreamProcedureType.Push);

            if (made == 0)
            {
                Log.Write(LogArea.Audio, () => "monitor: the input could not be opened for listening: " + Bass.LastError);

                return false;
            }

            _stream = made;
            _width = Math.Max(1, channels);
            _mostWaiting = (int)(Math.Max(1, rate) * MostWaitingSeconds) * StreamChannels * sizeof(float);
            _saidFull = false;

            _dsp = Bass.ChannelSetDSP(_stream, _dspProcedure);

            Attach();

            Log.Write(LogArea.Audio, () => $"monitor: listening to the input at {rate} Hz, {channels} channel(s)");

            return true;
        }
    }

    /// <summary>Stereo, which is what the effect on it and the bus under it both deal in.</summary>
    private const int StreamChannels = 2;

    /// <inheritdoc/>
    public void Push(byte[] data, int bytes)
    {
        if (data == null || bytes <= 0) return;

        lock (_lock)
        {
            if (_stream == 0) return;

            if (Waiting() > _mostWaiting)
            {
                if (!_saidFull)
                {
                    _saidFull = true;

                    Log.Write(LogArea.Audio, "monitor: nothing is emptying the input's queue, so blocks are being dropped");
                }

                return;
            }

            int room = _floats.Room(bytes, _width);

            if (room <= 0) return;

            if (_ready.Length < room) _ready = new float[room];

            int written = _floats.Read(data, bytes, _width, _ready);

            if (written <= 0) return;

            Bass.StreamPutData(_stream, _ready, written * sizeof(float));

            Attach();
        }
    }

    /// <summary>How wide the capture handing blocks over is.</summary>
    /// <remarks>
    /// Kept beside the stream rather than read off a block, since a block of bytes says nothing
    /// about its own width. Set when the path is opened, which is when the capture is.
    /// </remarks>
    private int _width = 2;

    /// <summary>How many bytes are waiting to be pulled, which is what the library answers to nothing.</summary>
    private int Waiting()
    {
        int waiting = Bass.StreamPutData(_stream, IntPtr.Zero, 0);

        return waiting < 0 ? 0 : waiting;
    }

    /// <summary>
    /// Puts the stream on the bus, and does nothing where it is already there.
    /// </summary>
    /// <remarks>
    /// Asked on every block rather than once, because the bus is made again whenever the output
    /// device changes and a source added to the bus that was there before is a source on a
    /// stream nobody plays. It is a lookup in a small set beside a block of audio.
    /// </remarks>
    private void Attach()
    {
        if (_stream == 0 || !_bus.IsOpen || _bus.Holds(_stream)) return;

        _bus.Add(_stream);
    }

    /// <summary>
    /// What is coming in, on its way to the bus, handed to the recording chain.
    /// </summary>
    /// <remarks>
    /// Runs where the bus is pulled rather than where the audio arrived, which is the whole
    /// arrangement: see the contract. Nothing here takes the lock, since BASS waits for this to
    /// return and the thread that opens the path may be inside a BASS call holding it.
    /// </remarks>
    /// <param name="handle">The hook this came from.</param>
    /// <param name="channel">The stream being pulled.</param>
    /// <param name="buffer">The samples, changed where they lie.</param>
    /// <param name="length">How many bytes of them there are.</param>
    /// <param name="user">Unused, since there is one of these.</param>
    private void OnDsp(int handle, int channel, IntPtr buffer, int length, IntPtr user) =>
        _pass.Run(Insert, _scratch, buffer, length, StreamChannels);

    /// <inheritdoc/>
    public void Close()
    {
        lock (_lock) CloseLocked();
    }

    /// <summary>Takes the path down with the lock held.</summary>
    /// <remarks>
    /// Off the bus before it is freed, or the bus is left holding a handle to nothing and the
    /// next thing plugged in could be given the same number.
    /// </remarks>
    private void CloseLocked()
    {
        if (_stream == 0) return;

        _bus.Remove(_stream);

        if (_dsp != 0)
        {
            Bass.ChannelRemoveDSP(_stream, _dsp);
            _dsp = 0;
        }

        Bass.StreamFree(_stream);
        _stream = 0;
    }
}
