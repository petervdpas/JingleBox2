using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.Versioning;
using JingleBox2.Audio.Interfaces;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using ManagedBass;

namespace JingleBox2.Audio;

/// <inheritdoc/>
/// <remarks>
/// Through <c>PipeWireSharp</c>, which is managed and wraps the server's own library: nothing
/// native is shipped for it and a machine without the server simply has no library to load.
///
/// **Every call into it is behind a method the compiler will not inline**, and that is not
/// tidiness. A type from an assembly is loaded the first time a method mentioning it is prepared,
/// so a check and a use in one method loads the assembly on the way to deciding whether to load
/// it: on Windows, where the answer is always no, that would be a library being reached for on
/// every start for nothing.
/// </remarks>
public sealed class PipeWireOutput : IPipeWireOutput
{
    /// <summary>Held while the node is made, started or let go.</summary>
    private readonly object _lock = new();

    /// <summary>What was answered about the server being here, once it has been asked.</summary>
    private bool? _present;

    /// <summary>Why it is not, where it is not.</summary>
    private string _missing = "";

    /// <summary>The context, or nothing while nothing is open.</summary>
    private IDisposable? _context;

    /// <summary>The node, or nothing.</summary>
    private IDisposable? _stream;

    /// <summary>The mix being pulled, by its library handle, or nought.</summary>
    private int _mix;

    /// <summary>Whether a block that could not be read has already been said.</summary>
    private bool _saidQuiet;

    /// <inheritdoc/>
    public bool Present
    {
        get
        {
            lock (_lock)
            {
                if (_present is { } known) return known;

                if (!OperatingSystem.IsLinux())
                {
                    _missing = "The sound server is a Linux arrangement, and this is not Linux.";

                    return (_present = false).Value;
                }

                _present = Ask();

                return _present.Value;
            }
        }
    }

    /// <inheritdoc/>
    public string Missing
    {
        get
        {
            _ = Present;

            lock (_lock) return _missing;
        }
    }

    /// <summary>Whether the server's library is really here, asked by asking it.</summary>
    /// <remarks>
    /// The wrapper answers rather than throwing where the library is absent, and anything thrown
    /// is read as absent too: a machine that cannot be asked is one that cannot be promised.
    ///
    /// **Marked for the one platform it belongs to**, which is the compiler being told what
    /// <see cref="Present"/> already guarantees: everything the wrapper offers is Linux's, and
    /// saying so here is what stops a call to it ever being compiled into a path Windows takes.
    /// </remarks>
    [SupportedOSPlatform("linux")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool Ask()
    {
        try
        {
            if (PipeWireSharp.PipeWire.IsAvailable) return true;

            _missing = "PipeWire is not running on this machine, so there is nothing to play into.";

            return false;
        }
        catch (Exception bad)
        {
            _missing = "PipeWire could not be reached from this build: " + bad.Message;

            return false;
        }
    }

    /// <inheritdoc/>
    public bool IsOpen
    {
        get { lock (_lock) return _stream != null; }
    }

    /// <inheritdoc/>
    public bool Open(int stream, int rate)
    {
        if (!Present || stream == 0 || rate <= 0) return false;

        if (!OperatingSystem.IsLinux()) return false;

        lock (_lock)
        {
            CloseLocked();

            _mix = stream;
            _saidQuiet = false;

            try
            {
                return Make(rate);
            }
            catch (Exception bad)
            {
                Log.Fault(LogArea.Audio, "the sound server would not take the mix", bad);

                CloseLocked();

                return false;
            }
        }
    }

    /// <summary>Stereo, which is what the whole application is and what the node is made as.</summary>
    private const int Channels = 2;

    /// <summary>
    /// Makes the node and starts it, with the lock held.
    /// </summary>
    /// <remarks>
    /// The properties are what somebody reads off their own patchbay, so they say what this is
    /// rather than what the library happened to call the stream: the application's name on the
    /// node, and the category that puts it among the things that play rather than among the
    /// things that are played.
    ///
    /// **Not asked to run on the server's own real-time thread.** What fills the buffer is the
    /// mixer, which is where the plugins are, and a plugin in a process of its own can be late;
    /// late on that thread is somebody's whole graph stuttering rather than this application's
    /// own audio.
    /// </remarks>
    /// <param name="rate">Frames a second.</param>
    [SupportedOSPlatform("linux")]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private bool Make(int rate)
    {
        var context = new PipeWireSharp.PipeWireContext();

        _context = context;

        var options = new PipeWireSharp.PipeWireStreamOptions(
            Name: Named,
            Format: BitConverter.IsLittleEndian
                ? PipeWireSharp.SpaAudioFormat.F32_LE
                : PipeWireSharp.SpaAudioFormat.F32_BE,
            Rate: rate,
            Channels: Channels,
            WriteData: Fill,
            Properties: new Dictionary<string, string>
            {
                ["media.type"] = "Audio",
                ["media.category"] = "Playback",
                ["media.role"] = "Production",
                ["application.name"] = Named,
                ["node.name"] = Named,
                ["node.description"] = Named
            },
            RealTimeProcess: false);

        var made = context.CreateStream(options);

        _stream = made;

        made.SetActive(true);

        Log.Write(LogArea.Audio, () =>
            "server: on the graph as '" + Named + "', " + Channels + " channels at " + rate + " Hz");

        return true;
    }

    /// <summary>What the node is called wherever anybody looks at it.</summary>
    private const string Named = "JingleBox2";

    /// <summary>
    /// Fills a block for the server out of the mix.
    /// </summary>
    /// <remarks>
    /// **Silence is the answer to everything that goes wrong here**, since this runs on the
    /// server's own thread and what it is filling goes straight out of somebody's speakers: a
    /// block nobody could fill is a moment of nothing, where anything thrown is this application
    /// taking a part of the machine's audio down with it.
    ///
    /// A decoding stream hands back what it has and says how much, and less than was asked for is
    /// ordinary while the mixer is still filling: the rest of the block is left at nought rather
    /// than held for, because holding is the one thing a thread like this may not do.
    /// </remarks>
    /// <param name="buffer">Where the block goes.</param>
    /// <returns>How many bytes of it were filled.</returns>
    private int Fill(Span<byte> buffer)
    {
        int mix;

        lock (_lock) mix = _mix;

        if (mix == 0 || buffer.Length == 0) return 0;

        try
        {
            int got = Take(mix, buffer);

            if (got > 0) return got;

            Quiet(got);

            buffer.Clear();

            return buffer.Length;
        }
        catch (Exception)
        {
            buffer.Clear();

            return buffer.Length;
        }
    }

    /// <summary>
    /// Reads a block out of the mix into the server's own buffer.
    /// </summary>
    /// <remarks>
    /// The library takes a pointer rather than a span, so the block is pinned for the length of
    /// the call. It is the server's buffer and it does not move while this is inside it, but
    /// pinning is what the call's shape asks for and costs nothing here.
    ///
    /// The length carries the flag saying the samples wanted are floats, which is how that
    /// library has always been told: the count and the format are one argument.
    /// </remarks>
    /// <param name="mix">The decoding mix, by its handle.</param>
    /// <param name="buffer">Where the block goes.</param>
    /// <returns>How many bytes came back, or negative where nothing did.</returns>
    private static unsafe int Take(int mix, Span<byte> buffer)
    {
        fixed (byte* held = buffer)
        {
            return Bass.ChannelGetData(mix, (IntPtr)held, buffer.Length | (int)DataFlags.Float);
        }
    }

    /// <summary>Says once that the mix had nothing to give, so a log is not filled with it.</summary>
    /// <param name="answered">What the library answered, which is negative for an error.</param>
    private void Quiet(int answered)
    {
        lock (_lock)
        {
            if (_saidQuiet) return;

            _saidQuiet = true;
        }

        var error = Bass.LastError;

        Log.Write(LogArea.Audio, () =>
            "server: the mix gave nothing back (" + answered + ", " + error
            + "), so silence is going out until it does");
    }

    /// <inheritdoc/>
    public void Close()
    {
        lock (_lock) CloseLocked();
    }

    /// <summary>Takes it down with the lock held.</summary>
    /// <remarks>
    /// The node first and the context after it, which is the order they were made in reversed: a
    /// context let go while a node is still on it is a node with nothing behind it, and what that
    /// costs is inside somebody else's library.
    /// </remarks>
    private void CloseLocked()
    {
        _mix = 0;

        var stream = _stream;
        var context = _context;

        _stream = null;
        _context = null;

        try
        {
            stream?.Dispose();
        }
        catch (Exception)
        {
        }

        try
        {
            context?.Dispose();
        }
        catch (Exception)
        {
        }

        if (stream != null) Log.Write(LogArea.Audio, () => "server: off the graph");
    }
}
