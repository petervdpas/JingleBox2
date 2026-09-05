using JingleBox2.Audio.Plugins.Bridge.Enums;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using System;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using JingleBox2.Audio.Plugins.Enums;
using JingleBox2.Audio.Plugins.Records;
using JingleBox2.Audio.Plugins.Bridge.Interfaces;

namespace JingleBox2.Audio.Plugins.Bridge;

/// <summary>
/// One plugin, running as a program of its own, and the wire to it.
/// </summary>
/// <remarks>
/// This is the whole point of the bridge. A plugin is somebody else's code, running in the same
/// address space as an hour of somebody's work, and a plugin that dereferences a null pointer
/// takes the lot. Out here it cannot: the worst it can do is stop, and stopping is something
/// this class can see, say out loud, and offer to undo.
///
/// Two sockets rather than one. The audio thread has a socket to itself so that a slow question
/// from the interface, reading a Serum patch for instance, can never sit in front of the block
/// that is due in ten milliseconds.
///
/// **The thread contract, which is written down in full in <c>docs/threads.md</c>.**
///
/// Three threads meet here. The audio path sends blocks, the drawing thread asks about
/// parameters and windows, and a reader thread of this class's own takes replies off the child's
/// control socket. The two sockets are what keeps the first two from queueing behind each other.
///
/// The shared block is memory two processes have mapped, and freeing it while the audio thread
/// is copying into it is a fault in this process rather than in a plugin. So it is a counted
/// gate rather than a lock: <see cref="Enter"/> counts up before it checks, so whoever got in
/// before the door closed is waited for and whoever came after is turned away, and disposal
/// waits a bounded moment for the count to reach nought before the memory goes.
///
/// A plugin's process going away is not a fault to be guarded against on the caller's thread.
/// It is the reason this class exists: an effect passes its audio through, an instrument goes
/// quiet, and the panel offers to start it again.
/// </remarks>
internal sealed class PluginProcess : IDisposable
{
    /// <summary>How a message body is written down and read back. Holds nothing, so one is enough.</summary>
    private readonly IBridgeBody _body = new BridgeBody();

    /// <summary>How long a wait for one of the child's sockets sits in the kernel before looking up.</summary>
    private const int AcceptPollMicroseconds = 200_000;

    /// <summary>
    /// How long disposal waits for the audio thread to come out of the shared block before the
    /// memory underneath it goes. Longer than any block, and short enough that closing a track
    /// still feels like closing a track.
    /// </summary>
    private const int DrainMilliseconds = 1000;

    /// <summary>How long a child is given to go away politely before it is killed.</summary>
    private const int QuitMilliseconds = 2000;

    /// <summary>
    /// One question at a time on the control socket. The answers come back in the order the
    /// questions went out and nothing in a message says which question it belongs to.
    /// </summary>
    private readonly object _callGate = new();

    /// <summary>
    /// Raised by the reader thread when an answer has arrived, and by <see cref="Bury"/> so
    /// that a plugin dying releases whoever is waiting rather than leaving them on the timeout.
    /// </summary>
    private readonly SemaphoreSlim _answered = new(0, 1);

    /// <summary>The plugin's process, for its number, its exit code, and killing it.</summary>
    private readonly Process _child;

    /// <summary>Everything that is said, except audio.</summary>
    private readonly BridgeLink _control;

    /// <summary>
    /// The audio socket, used by the audio thread alone. It exists so a slow question from the
    /// interface, reading a Serum patch for instance, can never sit in front of a block.
    /// </summary>
    private readonly BridgeLink _audio;

    /// <summary>Reads the control socket for the life of the process.</summary>
    private readonly Thread _reader;

    /// <summary>
    /// The last answer read off the control socket. Written by the reader thread and read by
    /// whoever is waiting on <see cref="_answered"/>, which is the only thing keeping the two
    /// in order.
    /// </summary>
    private (BridgeCall Call, byte[] Payload)? _answer;

    /// <summary>
    /// How many callers are inside the shared block right now. Disposal waits for this to come
    /// down before the memory underneath them goes.
    /// </summary>
    private int _rendering;

    /// <summary>
    /// True until the first block has come back. A plugin does its lazy loading on that block,
    /// so it is given seconds rather than the audio thread's usual patience.
    /// </summary>
    private bool _patient = true;

    /// <summary>False once the plugin has gone, whichever thread noticed first.</summary>
    private volatile bool _alive = true;

    /// <summary>
    /// Set when the process is being closed on purpose, which is what makes the difference
    /// between a death worth reporting and an ordinary shutdown.
    /// </summary>
    private volatile bool _stopping;

    /// <summary>What happened to it, in words fit to put on a page. Empty until it has gone.</summary>
    private string _epitaph = "";

    /// <summary>
    /// Takes a child that is already running with both sockets connected, and starts reading.
    /// </summary>
    /// <remarks>
    /// The operating system telling us the child exited is subscribed to here as well as the
    /// two sockets noticing, because a process can die without either socket being in use.
    /// </remarks>
    private PluginProcess(Process child, BridgeLink control, BridgeLink audio, BridgeBlock block, string blockPath,
                          string name, int sampleRate)
    {
        _child = child;
        _control = control;
        _audio = audio;

        Block = block;
        BlockPath = blockPath;

        _reader = new Thread(Read)
        {
            IsBackground = true,
            Name = "plugin bridge"
        };

        _reader.Start();

        _child.EnableRaisingEvents = true;
        _child.Exited += (_, _) => Bury();

        _rate = sampleRate;
        _cost = new BridgeCost(name);
    }

    /// <summary>The rate the audio is made at, which is what turns a crossing's frames into time.</summary>
    private readonly int _rate;

    /// <summary>
    /// What the crossings are costing, said every few seconds beside the mixing's own line.
    /// </summary>
    /// <remarks>
    /// Read and written only from inside <see cref="Render"/>, which one thread is in at a time,
    /// so it needs no lock of its own on a path that may not take one.
    /// </remarks>
    private readonly BridgeCost _cost;

    /// <summary>
    /// Counts one crossing and writes the stretch down when there is a stretch to write.
    /// </summary>
    /// <remarks>
    /// The line is built only when there is one to build, so an ordinary crossing costs the
    /// arithmetic and nothing else: this is the audio thread, called once per plugin per block.
    /// </remarks>
    /// <param name="frames">How many frames that crossing carried.</param>
    /// <param name="milliseconds">How long the round trip took.</param>
    private void Counted(int frames, double milliseconds)
    {
        string? line = _cost.Crossed(frames, milliseconds, _rate);

        if (line != null) Log.Write(LogArea.Audio, line);
    }

    /// <summary>The shared memory the audio crosses in. Only touched between Enter and Leave.</summary>
    public BridgeBlock Block { get; }

    /// <summary>Which process the plugin is, for a log or a list of what is running.</summary>
    public int ProcessId
    {
        get { try { return _child.Id; } catch (Exception) { return 0; } }
    }

    /// <summary>Where the shared block's file is, which is what the child was told to open.</summary>
    public string BlockPath { get; }

    /// <summary>True while the plugin's process is still running and still answering.</summary>
    public bool Alive => _alive;

    /// <summary>What happened to it, once it is gone.</summary>
    public string Epitaph => _epitaph;

    /// <summary>
    /// Says the audio thread is about to use the plugin, and holds the shared block open until
    /// it says otherwise. False when there is no plugin to use.
    /// </summary>
    /// <remarks>
    /// The block is memory two processes have mapped, and freeing it while the audio thread is
    /// copying a block into it is a fault in this process, not a plugin's. Counted up before
    /// the check rather than after, so that anybody who got in before the door closed is
    /// waited for and anybody who came after is turned away.
    /// </remarks>
    public bool Enter()
    {
        Interlocked.Increment(ref _rendering);

        if (_alive) return true;

        Interlocked.Decrement(ref _rendering);
        return false;
    }

    /// <summary>Done with it.</summary>
    public void Leave() => Interlocked.Decrement(ref _rendering);

    /// <summary>Raised once, on whichever thread noticed, when the plugin's process goes away.</summary>
    public event Action? Died;

    /// <summary>The plugin asking for a different window size.</summary>
    public event Action<int, int>? ResizeRequested;

    /// <summary>The plugin reporting a knob it moved itself, in its own window.</summary>
    public event Action<uint, double>? Edited;

    /// <summary>The plugin reporting that everything about it may have changed, which is a preset.</summary>
    public event Action? Reloaded;

    /// <summary>
    /// Starts a plugin in its own process and waits until it says it is ready.
    /// </summary>
    /// <remarks>
    /// Everything that can go wrong here ends as null: no executable to start, a plugin that
    /// will not load, a child that dies on the way up. None of them is worth a crash in the
    /// caller, and all of them mean the same thing, which is that there is no plugin.
    ///
    /// Both accepted sockets are given their own patience, and the control one has to be said
    /// out loud rather than left alone. The listener is given <see cref="PluginBridge.StartTimeoutMilliseconds"/>
    /// so a plugin that never connects cannot hold the caller for ever, and on Windows a socket
    /// handed back by <c>Accept</c> carries a copy of the listening socket's options, timeout
    /// included; on Linux it does not. Left as it arrives, a number meaning "how long to wait for
    /// a plugin to turn up" becomes "how long a running plugin may go without speaking", and a
    /// control socket is quiet by design, since it carries knob moves and window resizes rather
    /// than audio. So a plugin doing its job perfectly said nothing for thirty seconds, the read
    /// timed out, and <see cref="Bury"/> read that as the far end having gone: every plugin on
    /// Windows was declared dead half a minute in, while alive and rendering.
    ///
    /// Nought is waiting for ever, which is what the reader thread wants and what Linux was
    /// already doing. Nothing is lost by it: the reader is the only thing that reads this socket
    /// and it has nowhere else to be, and a question that goes unanswered is already bounded by
    /// <see cref="Answer"/>, which waits on the semaphore rather than on the socket.
    /// </remarks>
    /// <param name="plugin">Which plugin, and in which format.</param>
    /// <param name="sampleRate">What the audio is running at on this side.</param>
    /// <param name="maxFrames">
    /// The most frames one crossing carries, and the size of the shared block. Held to at least
    /// sixty four, since a block of a handful of frames costs a crossing for almost no audio.
    /// </param>
    /// <param name="asInstrument">True to play notes, false to work on audio handed to it.</param>
    public static PluginProcess? Start(PluginInfo plugin, int sampleRate, int maxFrames, bool asInstrument)
    {
        if (plugin == null) return null;

        maxFrames = Math.Max(64, maxFrames);

        string socketPath = SocketPath();
        BridgeBlock? block = null;
        Socket? listener = null;
        Process? child = null;

        try
        {
            block = BridgeBlock.Create(maxFrames, out string blockPath);

            listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
            listener.Bind(new UnixDomainSocketEndPoint(socketPath));
            listener.Listen(2);

            child = Launch(plugin, socketPath, blockPath, sampleRate, maxFrames, asInstrument);
            if (child == null)
            {
                block.Dispose();
                return null;
            }

            listener.ReceiveTimeout = PluginBridge.StartTimeoutMilliseconds;

            var controlSocket = Accept(listener, child);
            var audioSocket = Accept(listener, child);

            if (controlSocket == null || audioSocket == null)
            {
                controlSocket?.Dispose();
                audioSocket?.Dispose();
                Stop(child);
                block.Dispose();
                return null;
            }

            controlSocket.ReceiveTimeout = PluginBridge.WaitForEver;
            audioSocket.ReceiveTimeout = PluginBridge.FirstBlockTimeoutMilliseconds;

            var bridge = new PluginProcess(child, new BridgeLink(controlSocket), new BridgeLink(audioSocket), block, blockPath,
                                          plugin.Name, sampleRate);

            if (!bridge.Greet())
            {
                bridge.Dispose();
                return null;
            }

            return bridge;
        }
        catch (Exception)
        {
            block?.Dispose();
            if (child != null) Stop(child);
            return null;
        }
        finally
        {
            listener?.Dispose();
            try { if (File.Exists(socketPath)) File.Delete(socketPath); } catch (Exception) { }
        }
    }

    /// <summary>Waits for the child to say the plugin loaded, and collects its parameters.</summary>
    private bool Greet()
    {
        var hello = Answer(PluginBridge.StartTimeoutMilliseconds);

        if (hello == null || hello.Value.Call != BridgeCall.Hello) return false;

        var words = _body.ReadWords(hello.Value.Payload);

        HasOwnWindow = words.Length > 0 && words[0] == "window";

        var list = Call(BridgeCall.Parameters, null);

        Parameters = list.Call == BridgeCall.Parameters
            ? _body.ReadParameters(list.Payload)
            : Array.Empty<PluginParameter>();

        return true;
    }

    /// <summary>True when the plugin draws itself rather than leaving it to the host's knobs.</summary>
    public bool HasOwnWindow { get; private set; }

    /// <summary>Everything the plugin exposes, read once when it loaded.</summary>
    public PluginParameter[] Parameters { get; private set; } = Array.Empty<PluginParameter>();

    /// <summary>
    /// Waits for one of the child's two sockets, or gives up.
    /// </summary>
    /// <remarks>
    /// Accept has no timeout of its own, so the wait is done on the poll and the child is
    /// looked at between polls: a plugin that dies while loading should cost a moment rather
    /// than the whole thirty seconds a slow one is allowed.
    /// </remarks>
    private static Socket? Accept(Socket listener, Process child)
    {
        var end = DateTime.UtcNow.AddMilliseconds(PluginBridge.StartTimeoutMilliseconds);

        while (DateTime.UtcNow < end)
        {
            if (listener.Poll(AcceptPollMicroseconds, SelectMode.SelectRead)) return listener.Accept();

            if (child.HasExited) return null;
        }

        return null;
    }

    /// <summary>
    /// A fresh name for the pair of sockets. The process number and a new identifier are both
    /// in it, so two plugins, or two copies of the application, cannot land on the same one.
    /// </summary>
    private static string SocketPath()
    {
        string folder = Directory.Exists("/tmp") ? "/tmp" : Path.GetTempPath();

        return Path.Combine(folder, "jb-plug-" + Environment.ProcessId + "-" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".sock");
    }

    /// <summary>
    /// Starts this same program again, as somebody else.
    /// </summary>
    /// <remarks>
    /// Running the same executable means the child is always the same build, with the same
    /// plugin code in it, and there is nothing extra to install or to keep in step. When the
    /// program was started through the dotnet launcher there is no executable of our own to
    /// run, so the launcher is asked to run the same assembly again.
    ///
    /// The window that is asked for is the console and not the plugin's own: without it a black
    /// box flashes up on Windows for every plugin opened, and it says nothing about what the
    /// plugin is allowed to draw.
    ///
    /// A child logs when the application does, and is told where. It cannot read the setting
    /// itself because it has no settings, on purpose: it is one plugin and nothing else.
    /// </remarks>
    /// <param name="plugin">Which plugin, and in which format. Both go on the command line.</param>
    /// <param name="socketPath">Where the child should connect, twice.</param>
    /// <param name="blockPath">The shared block's file, which the child maps.</param>
    /// <param name="sampleRate">What the audio is running at, so both sides agree.</param>
    /// <param name="maxFrames">The most frames one crossing carries.</param>
    /// <param name="asInstrument">Whether the child loads it to play notes or to work on audio.</param>
    private static Process? Launch(PluginInfo plugin, string socketPath, string blockPath, int sampleRate, int maxFrames, bool asInstrument)
    {
        string? self = Environment.ProcessPath;
        if (string.IsNullOrEmpty(self)) return null;

        var start = new ProcessStartInfo
        {
            FileName = self,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = AppContext.BaseDirectory
        };

        string name = Path.GetFileNameWithoutExtension(self);

        if (string.Equals(name, "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            string assembly = System.Reflection.Assembly.GetEntryAssembly()?.Location ?? "";
            if (string.IsNullOrEmpty(assembly)) return null;

            start.ArgumentList.Add(assembly);
        }

        start.ArgumentList.Add(PluginBridge.HostArgument);
        start.ArgumentList.Add(socketPath);
        start.ArgumentList.Add(blockPath);
        start.ArgumentList.Add(plugin.Format == PluginFormat.Vst3 ? "vst3" : "clap");
        start.ArgumentList.Add(plugin.Path);
        start.ArgumentList.Add(plugin.Id);
        start.ArgumentList.Add(sampleRate.ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.ArgumentList.Add(maxFrames.ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.ArgumentList.Add(asInstrument ? "instrument" : "effect");

        if (Diagnostics.Log.On(Diagnostics.Enums.LogArea.Plugins))
        {
            start.Environment[PluginBridge.TraceVariable] = "1";
            start.Environment[PluginBridge.LogFolderVariable] = System.IO.Path.GetDirectoryName(Diagnostics.Log.Path) ?? "";
        }

        Diagnostics.Log.Write(Diagnostics.Enums.LogArea.Plugins, () =>
            "starting a process for " + plugin.Name + " (" + plugin.FormatName + ") at " + plugin.Path);

        try
        {
            return Process.Start(start);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Kills a child that never got as far as being a plugin, on the way out of a failed start.
    /// Nothing is checked, because there is nothing to be done about a kill that fails.
    /// </summary>
    private static void Stop(Process child)
    {
        try
        {
            if (!child.HasExited) child.Kill(entireProcessTree: true);
        }
        catch (Exception)
        {
        }

        try { child.Dispose(); } catch (Exception) { }
    }

    /// <summary>
    /// Asks the plugin's process something and waits for the answer.
    /// </summary>
    /// <remarks>
    /// One question at a time, because the answers come back in the order the questions went
    /// out and there is nothing in a message saying which question it belongs to. A plugin that
    /// stops answering is treated as gone: waiting forever on somebody else's process is how a
    /// host that meant to be careful ends up frozen.
    /// </remarks>
    public (BridgeCall Call, byte[] Payload) Call(BridgeCall call, byte[]? payload, int timeout = PluginBridge.CallTimeoutMilliseconds)
    {
        lock (_callGate)
        {
            if (!_alive) return (BridgeCall.Fail, Array.Empty<byte>());

            while (_answered.Wait(0)) { }
            _answer = null;

            try
            {
                _control.Send(call, payload);
            }
            catch (Exception)
            {
                Bury("stopped while it was being spoken to");
                return (BridgeCall.Fail, Array.Empty<byte>());
            }

            var answer = Answer(timeout);

            if (answer == null)
            {
                Bury("stopped answering");
                return (BridgeCall.Fail, Array.Empty<byte>());
            }

            return answer.Value;
        }
    }

    /// <summary>
    /// Waits for the reader thread to put an answer down. Null when nothing arrived in time,
    /// which the caller reads as the plugin having stopped answering.
    /// </summary>
    private (BridgeCall Call, byte[] Payload)? Answer(int timeout)
    {
        if (!_answered.Wait(timeout)) return null;

        return _answer;
    }

    /// <summary>
    /// Runs one block through the plugin, in the other process, and waits for it to come back.
    /// </summary>
    /// <remarks>
    /// This is on the audio thread, so everything it does is either already allocated or a
    /// syscall. The wait has a limit: a plugin that has stopped answering costs one late block
    /// and then never again, rather than a locked-up application.
    ///
    /// The first block is given seconds because that is where a plugin does its lazy loading.
    /// Once it has come back the patience drops to something an audio thread can afford, and
    /// that is set once rather than on every block: it is a call into the kernel, and this is
    /// the audio thread.
    ///
    /// A message is eight bytes each way, the kind in the first and the frame count at the
    /// fourth. Written by hand on the stack rather than through <see cref="BridgeLink"/>,
    /// because this is the one path where a message has to cost nothing.
    /// </remarks>
    public bool Render(int frames)
    {
        if (!Ask(frames)) return false;

        return Collect(frames);
    }

    /// <summary>When the block outstanding now was asked for, so the crossing can be measured.</summary>
    /// <remarks>
    /// Written by <see cref="Ask"/> and read by <see cref="Collect"/>, both of which run on the
    /// mixing thread and never at the same time for one process: a second block cannot be asked
    /// for while one is outstanding, because the shared memory a block crosses in is one buffer.
    /// </remarks>
    private long _asked;

    /// <summary>Whether a block has been asked for and not yet collected.</summary>
    private bool _outstanding;

    /// <summary>Whether a block has been asked for and is still owed.</summary>
    public bool Outstanding => _outstanding;

    /// <summary>
    /// Asks for a block and comes straight back without waiting for it.
    /// </summary>
    /// <remarks>
    /// **The half of a crossing that costs almost nothing.** What a crossing really costs is
    /// waking a process that has been asleep for a block, which is around 145 microseconds here
    /// against 8 for the socket itself, and that cost is paid between this and
    /// <see cref="Collect"/> rather than inside either. So several plugins asked before any of
    /// them is collected wake at the same time and are woken once between them rather than once
    /// each.
    ///
    /// The input for the block has to be in the shared memory already, since this is the moment
    /// the other side starts reading it, and nothing may touch it again until the answer is back.
    ///
    /// Asking twice without collecting is refused rather than allowed to overwrite: there is one
    /// buffer each way, so the second ask would be handing the plugin a block it is already
    /// halfway through.
    /// </remarks>
    /// <param name="frames">How many frames this crossing carries.</param>
    /// <returns>Whether the request went. False means the plugin has gone and nothing is owed.</returns>
    public bool Ask(int frames)
    {
        if (!_alive || _outstanding) return false;

        Span<byte> message = stackalloc byte[8];

        message[0] = (byte)BridgeCall.Process;
        BitConverter.TryWriteBytes(message.Slice(4, 4), frames);

        _asked = Stopwatch.GetTimestamp();

        try
        {
            int sent = _audio.Socket.Send(message);
            if (sent != 8) { Bury("stopped mid-block"); return false; }

            _outstanding = true;

            return true;
        }
        catch (Exception)
        {
            Bury("stopped while a block was in it");
            return false;
        }
    }

    /// <summary>
    /// Waits for a block that <see cref="Ask"/> has already asked for.
    /// </summary>
    /// <remarks>
    /// Answering false leaves nothing outstanding, whether the plugin died, timed out or was
    /// never asked, so a caller that gives up on one crossing is not left owing a collection that
    /// will never come.
    /// </remarks>
    /// <param name="frames">The frames that crossing carried, for the measurement.</param>
    /// <returns>Whether the block came back.</returns>
    public bool Collect(int frames)
    {
        if (!_outstanding) return false;

        _outstanding = false;

        if (!_alive) return false;

        try
        {
            Span<byte> reply = stackalloc byte[8];
            int read = 0;

            while (read < 8)
            {
                int got = _audio.Socket.Receive(reply.Slice(read));
                if (got <= 0) { Bury("stopped while a block was in it"); return false; }
                read += got;
            }

            if (_patient)
            {
                _patient = false;
                _audio.Socket.ReceiveTimeout = PluginBridge.BlockTimeoutMilliseconds;
            }

            Counted(frames, Stopwatch.GetElapsedTime(_asked).TotalMilliseconds);

            return reply[0] == (byte)BridgeCall.Rendered;
        }
        catch (SocketException error)
        {
            Bury(error.SocketErrorCode == SocketError.TimedOut
                ? "stopped keeping up and was let go"
                : "stopped while a block was in it");

            return false;
        }
        catch (Exception)
        {
            Bury("stopped while a block was in it");
            return false;
        }
    }

    /// <summary>
    /// The reader thread: everything the child says, for the life of the process.
    /// </summary>
    /// <remarks>
    /// The three things a plugin says without being asked are raised as events from here, on
    /// this thread. Everything else is an answer to a question somebody is waiting on, so it is
    /// put down and the waiter is released. A note is read and dropped: the child writes to the
    /// same log this process does, so there is nothing left to do with one on this side.
    ///
    /// A message that cannot be read means the socket has gone, which means the child has, and
    /// the thread ends there.
    /// </remarks>
    private void Read()
    {
        while (true)
        {
            var message = _control.Receive();

            if (message == null)
            {
                Bury();
                return;
            }

            switch (message.Value.Call)
            {
                case BridgeCall.ResizeRequested:
                    var size = _body.ReadPair(message.Value.Payload);
                    ResizeRequested?.Invoke(size.First, size.Second);
                    break;

                case BridgeCall.Edited:
                    var move = _body.ReadNumber(message.Value.Payload);
                    Edited?.Invoke(move.Id, move.Value);
                    break;

                case BridgeCall.Reloaded:
                    Reloaded?.Invoke();
                    break;

                case BridgeCall.Note:
                    break;

                default:
                    _answer = message;
                    try { _answered.Release(); } catch (SemaphoreFullException) { }
                    break;
            }
        }
    }

    /// <summary>
    /// Writes down that the plugin has gone, once, and lets everybody waiting on it go.
    /// </summary>
    /// <remarks>
    /// Three things can notice a death, the reader thread, the audio thread and the operating
    /// system telling us the child exited, and they can notice it at the same time. The first
    /// one wins and the rest are ignored, so the reason given is the one closest to what
    /// actually happened.
    /// </remarks>
    private void Bury(string reason = "")
    {
        if (!_alive) return;

        lock (this)
        {
            if (!_alive) return;
            _alive = false;

            if (_stopping)
            {
                _epitaph = "";
            }
            else
            {
                int code = ExitCode();

                _epitaph = reason.Length > 0
                    ? reason + Signal(code)
                    : "stopped unexpectedly" + Signal(code);
            }
        }

        try { _answered.Release(); } catch (SemaphoreFullException) { }

        Diagnostics.Log.Write(Diagnostics.Enums.LogArea.Plugins, () =>
            _stopping ? "a plugin process was closed on purpose" : "a plugin process " + _epitaph);

        if (!_stopping) Died?.Invoke();
    }

    /// <summary>
    /// What the child exited with, or nought when it cannot be asked. Only read while working
    /// out an epitaph, so an unknown code costs a few words rather than anything real.
    /// </summary>
    private int ExitCode()
    {
        try { return _child.HasExited ? _child.ExitCode : 0; }
        catch (Exception) { return 0; }
    }

    /// <summary>
    /// Turns an exit code into something worth reading. A process killed by a signal exits with
    /// 128 plus the signal, and signal 11 is the one everybody has seen.
    /// </summary>
    private static string Signal(int code)
    {
        if (code == 139 || code == 11) return " (it crashed)";
        if (code == 134 || code == 6) return " (it gave up on itself)";
        if (code > 128 && code < 160) return " (signal " + (code - 128) + ")";
        if (code == 0) return "";

        return " (exit code " + code + ")";
    }

    /// <summary>
    /// Closes the plugin down on purpose.
    /// </summary>
    /// <remarks>
    /// Marked as stopping first, so <see cref="Bury"/> knows this is not a death worth
    /// reporting and nobody is told the plugin crashed when it was taken off a track.
    ///
    /// Then the door is shut and whoever is already inside the shared block is waited for.
    /// Nothing new can get in once the plugin is no longer alive, and the memory is only freed
    /// once the audio thread has come back out of it: freeing it underneath a copy in progress
    /// is a fault in this process, not in anybody's plugin.
    ///
    /// The child is asked to stop and then killed if it will not, which is the only honest way
    /// to end a conversation with somebody else's code.
    /// </remarks>
    public void Dispose()
    {
        _stopping = true;

        bool was = _alive;

        _alive = false;

        var end = Environment.TickCount64 + DrainMilliseconds;

        while (Volatile.Read(ref _rendering) > 0 && Environment.TickCount64 < end) Thread.Sleep(1);

        if (was)
        {
            try { _control.Send(BridgeCall.Quit); } catch (Exception) { }

            try
            {
                if (!_child.WaitForExit(QuitMilliseconds)) _child.Kill(entireProcessTree: true);
            }
            catch (Exception)
            {
            }
        }

        _control.Dispose();
        _audio.Dispose();

        try { _child.Dispose(); } catch (Exception) { }

        Block.Dispose();
    }
}
