using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

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
/// </remarks>
internal sealed class PluginProcess : IDisposable
{
    private readonly object _callGate = new();
    private readonly SemaphoreSlim _answered = new(0, 1);
    private readonly Process _child;
    private readonly BridgeLink _control;
    private readonly BridgeLink _audio;
    private readonly Thread _reader;

    private (BridgeCall Call, byte[] Payload)? _answer;
    private int _rendering;
    private bool _patient = true;
    private volatile bool _alive = true;
    private volatile bool _stopping;
    private string _epitaph = "";

    private PluginProcess(Process child, BridgeLink control, BridgeLink audio, BridgeBlock block, string blockPath)
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
    }

    public BridgeBlock Block { get; }

    /// <summary>Which process the plugin is, for a log or a list of what is running.</summary>
    public int ProcessId
    {
        get { try { return _child.Id; } catch (Exception) { return 0; } }
    }

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
    /// </remarks>
    public static PluginProcess? Start(PluginInfo plugin, int sampleRate, int maxFrames, bool asInstrument)
    {
        if (plugin == null) return null;

        maxFrames = Math.Max(64, maxFrames);

        BridgeBlock? block = null;
        TcpListener? listener = null;
        Process? child = null;

        try
        {
            block = BridgeBlock.Create(maxFrames, out string blockPath);

            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start(2);

            var endpoint = (IPEndPoint)listener.LocalEndpoint;
            int port = endpoint.Port;

            child = Launch(plugin, port, blockPath, sampleRate, maxFrames, asInstrument);
            if (child == null)
            {
                block.Dispose();
                return null;
            }

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

            audioSocket.ReceiveTimeout = PluginBridge.FirstBlockTimeoutMilliseconds;

            var bridge = new PluginProcess(child, new BridgeLink(controlSocket), new BridgeLink(audioSocket), block, blockPath);

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
            listener?.Stop();
        }
    }

    /// <summary>Waits for the child to say the plugin loaded, and collects its parameters.</summary>
    private bool Greet()
    {
        var hello = Answer(PluginBridge.StartTimeoutMilliseconds);

        if (hello == null || hello.Value.Call != BridgeCall.Hello) return false;

        var words = BridgeBody.ReadWords(hello.Value.Payload);

        HasOwnWindow = words.Length > 0 && words[0] == "window";

        var list = Call(BridgeCall.Parameters, null);

        Parameters = list.Call == BridgeCall.Parameters
            ? BridgeBody.ReadParameters(list.Payload)
            : Array.Empty<PluginParameter>();

        return true;
    }

    /// <summary>True when the plugin draws itself rather than leaving it to the host's knobs.</summary>
    public bool HasOwnWindow { get; private set; }

    /// <summary>Everything the plugin exposes, read once when it loaded.</summary>
    public PluginParameter[] Parameters { get; private set; } = Array.Empty<PluginParameter>();

    private static Socket? Accept(TcpListener listener, Process child)
    {
        var end = DateTime.UtcNow.AddMilliseconds(PluginBridge.StartTimeoutMilliseconds);

        while (DateTime.UtcNow < end)
        {
            if (listener.Pending()) return listener.AcceptSocket();

            if (child.HasExited) return null;

            System.Threading.Thread.Sleep(50);
        }

        return null;
    }

    /// <summary>
    /// Starts this same program again, as somebody else.
    /// </summary>
    /// <remarks>
    /// Running the same executable means the child is always the same build, with the same
    /// plugin code in it, and there is nothing extra to install or to keep in step. When the
    /// program was started through the dotnet launcher there is no executable of our own to
    /// run, so the launcher is asked to run the same assembly again.
    /// </remarks>
    private static Process? Launch(PluginInfo plugin, int port, string blockPath, int sampleRate, int maxFrames, bool asInstrument)
    {
        string? self = Environment.ProcessPath;
        if (string.IsNullOrEmpty(self)) return null;

        var start = new ProcessStartInfo
        {
            FileName = self,
            UseShellExecute = false,
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
        start.ArgumentList.Add(port.ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.ArgumentList.Add(blockPath);
        start.ArgumentList.Add(plugin.Format == PluginFormat.Vst3 ? "vst3" : "clap");
        start.ArgumentList.Add(plugin.Path);
        start.ArgumentList.Add(plugin.Id);
        start.ArgumentList.Add(sampleRate.ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.ArgumentList.Add(maxFrames.ToString(System.Globalization.CultureInfo.InvariantCulture));
        start.ArgumentList.Add(asInstrument ? "instrument" : "effect");

        // A child logs when the application does. It cannot read the setting itself: it has no
        // settings, on purpose.
        if (Diagnostics.Log.IsOn)
        {
            start.Environment[PluginBridge.TraceVariable] = "1";
            start.Environment[PluginBridge.LogFolderVariable] = System.IO.Path.GetDirectoryName(Diagnostics.Log.Path) ?? "";
        }

        Diagnostics.Log.Write(Diagnostics.LogArea.Plugins, () =>
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
    /// </remarks>
    public bool Render(int frames)
    {
        if (!_alive) return false;

        Span<byte> message = stackalloc byte[8];

        message[0] = (byte)BridgeCall.Process;
        BitConverter.TryWriteBytes(message.Slice(4, 4), frames);

        try
        {
            int sent = _audio.Socket.Send(message);
            if (sent != 8) { Bury("stopped mid-block"); return false; }

            Span<byte> reply = stackalloc byte[8];
            int read = 0;

            while (read < 8)
            {
                int got = _audio.Socket.Receive(reply.Slice(read));
                if (got <= 0) { Bury("stopped while a block was in it"); return false; }
                read += got;
            }

            // After the first block a plugin has done its loading, so the patience drops to
            // something an audio thread can afford. Set once, not every block: it is a call
            // into the kernel and this is the audio thread.
            if (_patient)
            {
                _patient = false;
                _audio.Socket.ReceiveTimeout = PluginBridge.BlockTimeoutMilliseconds;
            }

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
                    var size = BridgeBody.ReadPair(message.Value.Payload);
                    ResizeRequested?.Invoke(size.First, size.Second);
                    break;

                case BridgeCall.Edited:
                    var move = BridgeBody.ReadNumber(message.Value.Payload);
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

        Diagnostics.Log.Write(Diagnostics.LogArea.Plugins, () =>
            _stopping ? "a plugin process was closed on purpose" : "a plugin process " + _epitaph);

        if (!_stopping) Died?.Invoke();
    }

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

    public void Dispose()
    {
        _stopping = true;

        bool was = _alive;

        // Nothing new gets into the shared block from here, and whatever is already in there
        // is waited for before the memory underneath it goes.
        _alive = false;

        var end = Environment.TickCount64 + 1000;

        while (Volatile.Read(ref _rendering) > 0 && Environment.TickCount64 < end) Thread.Sleep(1);

        if (was)
        {
            try { _control.Send(BridgeCall.Quit); } catch (Exception) { }

            try
            {
                if (!_child.WaitForExit(2000)) _child.Kill(entireProcessTree: true);
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
