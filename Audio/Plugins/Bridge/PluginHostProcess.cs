using System;
using JingleBox2.Diagnostics;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace JingleBox2.Audio.Plugins.Bridge;

/// <summary>
/// The other side of the bridge: this program, started again, being one plugin and nothing else.
/// </summary>
/// <remarks>
/// No window, no audio device, no configuration, no user interface. It loads one plugin, does
/// what it is told through a socket, and goes away when the parent does. If it falls over, it
/// falls over alone, which is the entire reason it exists.
///
/// One thread does everything the plugin can see except audio. A plugin's toolkit expects its
/// timers, its window and its parameters all on the same thread, and giving it anything else is
/// a crash in somebody else's code rather than a bug report. Audio has a thread of its own
/// because it has a deadline.
/// </remarks>
public static class PluginHostProcess
{
    private static readonly ConcurrentQueue<(BridgeCall Call, byte[] Payload)> Incoming = new();
    private static readonly ConcurrentQueue<Action> Errands = new();

    /// <summary>Knobs the plugin turned in its own window, waiting to be sent home.</summary>
    private static readonly ConcurrentQueue<(uint Id, double Value)> Moves = new();

    /// <summary>Set when the plugin has loaded a whole new sound and the parent has not heard.</summary>
    private static volatile bool Reloads;

    /// <summary>The moment the plugin's window opened, so what it asked for can go with it.</summary>
    private static long _windowMark;

    /// <summary>How long the plugin has been inside its own run loop, and how many rounds.</summary>
    private static long _inLoop;
    private static long _rounds;
    private static readonly AutoResetEvent Knock = new(false);

    /// <summary>How often the audio thread looks up when nothing is being played through it.</summary>
    private const int IdleMilliseconds = 40;

    private static BridgeLink? _control;
    private static BridgeBlock? _block;
    private static IPluginParameters? _plugin;
    private static IPluginEditor? _editor;
    private static volatile bool _running = true;
    private static bool _trace;

    /// <summary>How many blocks have gone through, so the log can say whether audio is running.</summary>
    private static long _blocks;

    /// <summary>Blocks that came out with something in them, and the loudest sample among them.</summary>
    private static long _sounded;
    private static float _loudest;

    /// <summary>True when these arguments mean this process is meant to be a plugin's process.</summary>
    public static bool Claims(string[] args) =>
        args != null && args.Length > 0 &&
        (args[0] == PluginBridge.HostArgument || args[0] == PluginBridge.ScanArgument);

    /// <summary>
    /// Does whatever the arguments asked for and returns the exit code. Nothing here ever
    /// returns to the application: a process that is a plugin is only ever a plugin.
    /// </summary>
    public static int Run(string[] args)
    {
        _trace = Environment.GetEnvironmentVariable(PluginBridge.TraceVariable) == "1";

        // The same log the application writes, in the same folder, with this process's number
        // on every line. A plugin falling over then leaves its account of it next to what the
        // application was doing at the time.
        string folder = Environment.GetEnvironmentVariable(PluginBridge.LogFolderVariable) ?? "";

        Log.Open(folder.Length > 0 ? folder : Config.AppFolder.Path(), _trace, LogArea.Plugins);

        return args[0] == PluginBridge.ScanArgument ? Scan(args) : Serve(args);
    }

    /// <summary>Writes a line about what this plugin's process is doing.</summary>
    private static void Say(string message)
    {
        Log.Write(LogArea.Plugins, message);
    }

    /// <summary>
    /// Reads folders full of plugins and writes what is in them to a file.
    /// </summary>
    /// <remarks>
    /// Scanning means loading somebody's library and asking it questions, and a library that
    /// falls over while being asked used to take the scan and the application with it. Out here
    /// a bad plugin costs one empty scan. The answer goes to a file rather than to the output,
    /// because plugins print things and there is no telling what would end up mixed in with it.
    /// </remarks>
    private static int Scan(string[] args)
    {
        if (args.Length < 2) return 2;

        string destination = args[1];
        var folders = new List<string>();

        for (int index = 2; index < args.Length; index++) folders.Add(args[index]);

        try
        {
            var found = PluginHost.ScanHere(folders);

            var json = System.Text.Json.JsonSerializer.Serialize(found);

            File.WriteAllText(destination, json, new UTF8Encoding(false));

            return 0;
        }
        catch (Exception error)
        {
            Say("scan failed: " + error.Message);
            return 1;
        }
    }

    /// <summary>Loads one plugin and serves it until the parent has had enough.</summary>
    private static int Serve(string[] args)
    {
        if (args.Length < 9) return 2;

        string socketPath = args[1];
        string blockPath = args[2];
        bool isVst3 = args[3] == "vst3";
        string path = args[4];
        string id = args[5];

        int sampleRate = int.TryParse(args[6], out int rate) ? rate : 48000;
        int maxFrames = int.TryParse(args[7], out int frames) ? frames : 512;
        bool asInstrument = args[8] == "instrument";

        Socket control;
        Socket audio;

        try
        {
            control = Connect(socketPath);
            audio = Connect(socketPath);
        }
        catch (Exception error)
        {
            Say("could not reach the parent: " + error.Message);
            return 3;
        }

        _control = new BridgeLink(control);
        _block = BridgeBlock.Open(blockPath);

        if (_block == null)
        {
            Say("the shared block was not there");
            return 4;
        }

        // Before the plugin is loaded, not after. Until somebody takes the loop over it pumps
        // on a thread of its own, and a plugin registering something during load would then be
        // called from one thread while this one is still building it.
        Vst3RunLoopDriveHere();

        Say("loading " + path);

        _plugin = isVst3
            ? Vst3Plugin.Load(path, id, sampleRate, maxFrames)
            : ClapEffect.Load(path, id, sampleRate, maxFrames);

        if (_plugin == null)
        {
            Say("the plugin would not load");
            _control.Send(BridgeCall.Fail, BridgeBody.Words("the plugin would not load"));
            return 5;
        }

        // A knob the plugin turns itself. Queued rather than sent where it happens: for CLAP
        // that is the audio thread, and an audio thread has no business waiting on a socket.
        _plugin.Edited += (id, value) =>
        {
            Moves.Enqueue((id, value));
            Knock.Set();
        };

        _plugin.Reloaded += () =>
        {
            Reloads = true;
            Knock.Set();
        };

        bool hasWindow = _plugin is IPluginWindowSource;

        _control.Send(BridgeCall.Hello, BridgeBody.Words(hasWindow ? "window" : "plain"));

        var reader = new Thread(() => Listen(_control)) { IsBackground = true, Name = "bridge control" };
        reader.Start();

        var mixer = new Thread(() => Mix(audio, asInstrument, maxFrames)) { IsBackground = true, Name = "bridge audio" };
        mixer.Start();

        Pump();

        Say("stopping");

        DropEditor();

        try { (_plugin as IDisposable)?.Dispose(); } catch (Exception) { }

        return 0;
    }

    /// <summary>
    /// Says that timers and windows belong to this thread, whoever asks for them.
    /// </summary>
    private static void Vst3RunLoopDriveHere()
    {
        PluginRunLoop.DriveWith(round => { Errands.Enqueue(round); Knock.Set(); });
    }

    private static Socket Connect(string path)
    {
        var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);

        socket.Connect(new UnixDomainSocketEndPoint(path));

        return socket;
    }

    /// <summary>
    /// The one thread the plugin is allowed to see: its timers, its window, its parameters.
    /// </summary>
    private static void Pump()
    {
        Say("pump running");

        long census = Environment.TickCount64;
        long asked = census;
        long counted = 0;

        while (_running)
        {
            Knock.WaitOne(5);

            if (Log.IsOn && Environment.TickCount64 - census > 2000)
            {
                census = Environment.TickCount64;
                long blocks = _blocks;

                long rounds = _rounds;
                double spent = _inLoop * 1000.0 / System.Diagnostics.Stopwatch.Frequency;

                _rounds = 0;
                _inLoop = 0;

                long sounded = _sounded;
                float loudest = _loudest;

                _sounded = 0;
                _loudest = 0;

                Say("run loop: " + rounds + " rounds taking " + spent.ToString("0") + " ms in the plugin; " +
                    PluginRunLoop.Census() +
                    "; " + (blocks - counted) + " blocks of audio in the last two seconds, " +
                    sounded + " of them with something in them, loudest " +
                    loudest.ToString("F4", System.Globalization.CultureInfo.InvariantCulture) +
                    ((_plugin as ClapEffect)?.IsWaitingToSpeak == true ? "; the plugin is waiting to say something" : ""));

                counted = blocks;
            }

            while (Errands.TryDequeue(out var errand))
            {
                long began = System.Diagnostics.Stopwatch.GetTimestamp();

                try { errand(); } catch (Exception error) { Say("run loop: " + error.Message); }

                _inLoop += System.Diagnostics.Stopwatch.GetTimestamp() - began;
                _rounds++;
            }

            // A CLAP plugin with a window open is asked now and then what its knobs are set to.
            // It hands a knob back at the end of a block and at no other time, so a plugin on a
            // track nobody is playing would otherwise keep its own window a secret. Only while
            // there is a window, because that is the only time a knob can be turned.
            if (_editor != null && Environment.TickCount64 - asked > 40)
            {
                asked = Environment.TickCount64;

                try { (_plugin as ClapEffect)?.Poll(); }
                catch (Exception error) { Say("asking the plugin about its knobs went wrong: " + error.Message); }
            }

            if (Reloads)
            {
                Reloads = false;

                try { _control?.Send(BridgeCall.Reloaded); } catch (Exception) { }
            }

            while (Moves.TryDequeue(out var move))
            {
                try { _control?.Send(BridgeCall.Edited, BridgeBody.Number(move.Id, move.Value)); }
                catch (Exception) { }
            }

            while (Incoming.TryDequeue(out var message))
            {
                Say("-> " + message.Call);

                try { Handle(message.Call, message.Payload); }
                catch (Exception error) { Say("message " + message.Call + ": " + error.Message); }

                Say("<- " + message.Call);
            }
        }
    }

    private static void Listen(BridgeLink control)
    {
        while (_running)
        {
            var message = control.Receive();

            if (message == null)
            {
                // The parent is gone. There is nobody left to be a plugin for.
                _running = false;
                Knock.Set();
                return;
            }

            Incoming.Enqueue(message.Value);
            Knock.Set();
        }
    }

    private static void Handle(BridgeCall call, byte[] payload)
    {
        var control = _control;
        var plugin = _plugin;

        if (control == null || plugin == null) return;

        switch (call)
        {
            case BridgeCall.Parameters:
                control.Send(BridgeCall.Parameters, BridgeBody.Parameters(plugin.Parameters()));
                break;

            case BridgeCall.SetValue:
            {
                var move = BridgeBody.ReadNumber(payload);
                plugin.SetValue(move.Id, move.Value);
                control.Send(BridgeCall.Ok);
                break;
            }

            case BridgeCall.ValueOf:
            {
                var ask = BridgeBody.ReadNumber(payload);
                control.Send(BridgeCall.Value, BridgeBody.Double(plugin.ValueOf(ask.Id)));
                break;
            }

            case BridgeCall.TextFor:
            {
                var ask = BridgeBody.ReadNumber(payload);
                control.Send(BridgeCall.Text, BridgeBody.Words(plugin.TextFor(ask.Id, ask.Value)));
                break;
            }

            case BridgeCall.Flush:
                (plugin as IPluginEffect)?.FlushParameters();
                control.Send(BridgeCall.Ok);
                break;

            case BridgeCall.SaveState:
                control.Send(BridgeCall.State, (plugin as IPluginInstrument)?.SaveState() ?? Array.Empty<byte>());
                break;

            case BridgeCall.LoadState:
                (plugin as IPluginInstrument)?.LoadState(payload);
                control.Send(BridgeCall.Ok);
                break;

            case BridgeCall.OpenEditor:
                OpenEditor(control, plugin);
                break;

            case BridgeCall.Attach:
                Attach(control, BridgeBody.ReadHandle(payload));
                break;

            case BridgeCall.Detach:
                _editor?.Detach();
                control.Send(BridgeCall.Ok);
                break;

            case BridgeCall.Resized:
            {
                var size = BridgeBody.ReadPair(payload);
                _editor?.Resized(size.First, size.Second);
                control.Send(BridgeCall.Ok);
                break;
            }

            case BridgeCall.CloseEditor:
                CloseEditor();
                control.Send(BridgeCall.Ok);
                break;

            case BridgeCall.Quit:
                _running = false;
                Knock.Set();
                break;
        }
    }

    /// <summary>
    /// The plugin's own interface, made the first time it is asked for and kept after that.
    /// </summary>
    /// <remarks>
    /// Built once per plugin, not once per window. A second window gets the same interface put
    /// into it, because building a second one does not work: what a plugin registers with the
    /// host when it first draws (its connection to X, above all) belongs to the plugin rather
    /// than to the window, and it registers it once and never mentions it again. Tear the
    /// interface down and that registration is gone, or worse, pointing at memory the plugin
    /// has freed, and the next window draws perfectly and hears nothing at all.
    ///
    /// So a window closing takes the interface out of it and leaves it standing. See
    /// <see cref="CloseEditor"/>.
    /// </remarks>
    private static void OpenEditor(BridgeLink control, IPluginParameters plugin)
    {
        var editor = _editor;

        if (editor == null)
        {
            editor = (plugin as IPluginWindowSource)?.OpenEditor();

            if (editor == null)
            {
                control.Send(BridgeCall.Fail, BridgeBody.Words("this plugin has no window of its own"));
                return;
            }

            _editor = editor;

            // Everything the plugin asks for from here belongs to the interface, and goes when
            // the interface finally does. See PluginRunLoop.DropSince.
            _windowMark = PluginRunLoop.Mark();

            editor.ResizeRequested += (width, height) =>
                control.Send(BridgeCall.ResizeRequested, BridgeBody.Pair(width, height));
        }

        control.Send(BridgeCall.Ok, BridgeBody.Three(editor.Size.Width, editor.Size.Height, editor.CanResize ? 1 : 0));
    }

    private static void Attach(BridgeLink control, nint window)
    {
        var editor = _editor;

        if (editor == null || window == 0)
        {
            control.Send(BridgeCall.Fail, BridgeBody.Words("there is no interface to put in a window"));
            return;
        }

        bool attached = editor.Attach(window);

        if (!attached)
        {
            control.Send(BridgeCall.Fail, BridgeBody.Words("the plugin would not take the window"));
            return;
        }

        // Whatever the plugin put inside the window is told it is embedded, which some toolkits
        // wait for before they will draw anything. See XEmbed.
        Say("embedding: " + XEmbed.Complete(window));

        control.Send(BridgeCall.Ok, BridgeBody.Pair(editor.Size.Width, editor.Size.Height));
    }

    /// <summary>
    /// Takes the plugin's interface out of the window it was in, and leaves it standing.
    /// </summary>
    /// <remarks>
    /// Not disposed. See <see cref="OpenEditor"/> for why: an interface built a second time is
    /// one the host has stopped listening to. What the plugin holds costs a window's worth of
    /// memory in a process that is this plugin and nothing else, and it goes when the process
    /// does.
    /// </remarks>
    private static void CloseEditor()
    {
        var editor = _editor;

        if (editor == null) return;

        // The last moment anybody can ask. Whatever was turned in that window is still worth
        // knowing about, and after this nobody is looking at it.
        Settle();

        try { editor.Detach(); } catch (Exception error) { Say("taking the window back: " + error.Message); }
    }

    /// <summary>
    /// Lets the interface go for good, for a process that is stopping.
    /// </summary>
    /// <remarks>
    /// The one place a plugin's interface is destroyed, and the only place it is safe: nothing
    /// is going to draw again. Whatever the plugin asked the host to hold goes with it, whether
    /// the plugin remembered to give it back or not. Vital does not, and calling what it has
    /// freed is how its process dies.
    /// </remarks>
    private static void DropEditor()
    {
        var editor = _editor;
        _editor = null;

        if (editor == null) return;

        try { editor.Dispose(); } catch (Exception error) { Say("closing the window: " + error.Message); }

        PluginRunLoop.DropSince(_windowMark);
    }

    /// <summary>
    /// Takes a last reading of the plugin's own knobs, for a window on its way out.
    /// </summary>
    /// <remarks>
    /// The handing over belongs to the audio thread, so this asks for it and waits a moment
    /// rather than doing it here. A fifth of a second is longer than the audio thread's own
    /// round and short enough that closing a window still feels like closing a window.
    /// </remarks>
    private static void Settle()
    {
        if (_plugin is not ClapEffect clap) return;

        clap.WantsFlush();

        long end = Environment.TickCount64 + 200;

        while (clap.IsWaitingToSpeak && Environment.TickCount64 < end) Thread.Sleep(5);

        try { clap.Poll(); } catch (Exception) { }

        Say("last reading of " + clap.Info.Name + " before its window closed: " + clap.Reading());
    }

    /// <summary>
    /// The audio thread: waits for a block, runs it, says it is done.
    /// </summary>
    /// <remarks>
    /// Nothing here allocates once it is going. The buffer is made at the size the parent asked
    /// for and reused for every block after that, because a garbage collection in the middle of
    /// an audio block is a click.
    /// </remarks>
    private static unsafe void Mix(Socket audio, bool asInstrument, int maxFrames)
    {
        var block = _block;
        var plugin = _plugin;

        if (block == null || plugin == null) return;

        var buffer = new float[maxFrames * PluginBridge.Channels];

        var message = new byte[8];
        var reply = new byte[8];

        reply[0] = (byte)BridgeCall.Rendered;

        var effect = plugin as IPluginEffect;
        var instrument = plugin as IPluginInstrument;
        var clap = plugin as ClapEffect;

        // A plugin nobody is playing still needs a moment of this thread now and then. CLAP
        // says a switched-on plugin's flush belongs to the audio thread, and the flush is the
        // only way what its own window did reaches the rest of it. So the wait gives up every
        // so often, does that, and goes back to waiting.
        audio.ReceiveTimeout = IdleMilliseconds;

        while (_running)
        {
            int read = 0;

            while (read < 8)
            {
                int got;

                try
                {
                    got = audio.Receive(message, read, 8 - read, SocketFlags.None);
                }
                catch (SocketException error) when (error.SocketErrorCode == SocketError.TimedOut)
                {
                    // Nothing to play. Only between messages: half a message is still a message.
                    if (read == 0)
                    {
                        try { clap?.Idle(); } catch (Exception) { }
                        continue;
                    }

                    continue;
                }
                catch (Exception)
                {
                    return;
                }

                if (got <= 0) return;
                read += got;
            }

            if (message[0] != (byte)BridgeCall.Process) continue;

            _blocks++;

            int frames = BitConverter.ToInt32(message, 4);
            if (frames <= 0 || frames > maxFrames) frames = Math.Min(Math.Max(frames, 0), maxFrames);

            Deliver(block, instrument, plugin);

            int samples = frames * PluginBridge.Channels;

            if (asInstrument && instrument != null)
            {
                instrument.Render(buffer, frames);
            }
            else
            {
                fixed (float* destination = buffer)
                {
                    Buffer.MemoryCopy(block.Input, destination, (long)samples * sizeof(float), (long)samples * sizeof(float));
                }

                effect?.Process(buffer, frames);
            }

            // Counted rather than said. A line of the log is a file opened, written and closed
            // under a lock, and this is the audio thread; saying it once a block was the same
            // lock the thread driving the plugin's own window needs, taken eighty times a
            // second. What came out is reported with the run loop's census below instead.
            //
            // The whole block rather than the first hundred samples: a block that starts
            // quiet and ends loud is a note beginning, which is exactly the one worth seeing.
            if (Log.IsOn)
            {
                float peak = 0;
                for (int index = 0; index < samples; index++)
                {
                    float magnitude = Math.Abs(buffer[index]);
                    if (magnitude > peak) peak = magnitude;
                }

                if (peak > 0.0001f) _sounded++;
                if (peak > _loudest) _loudest = peak;
            }

            fixed (float* source = buffer)
            {
                Buffer.MemoryCopy(source, block.Output, (long)samples * sizeof(float), (long)samples * sizeof(float));
            }

            BitConverter.TryWriteBytes(reply.AsSpan(4, 4), frames);

            try
            {
                int sent = 0;
                while (sent < 8) sent += audio.Send(reply, sent, 8 - sent, SocketFlags.None);
            }
            catch (Exception)
            {
                return;
            }
        }
    }

    /// <summary>Hands the plugin everything the parent queued since the last block.</summary>
    private static void Deliver(BridgeBlock block, IPluginInstrument? instrument, IPluginParameters plugin)
    {
        var events = block.Take();

        foreach (var queued in events)
        {
            switch (queued.Kind)
            {
                case BridgeEvent.ParameterValue:
                    plugin.SetValue(queued.Id, queued.Value);
                    break;

                case BridgeEvent.NoteOn:
                    instrument?.NoteOn((int)queued.Id, queued.Value);
                    break;

                case BridgeEvent.NoteOff:
                    instrument?.NoteOff((int)queued.Id);
                    break;

                case BridgeEvent.AllNotesOff:
                    instrument?.AllNotesOff();
                    break;
            }
        }
    }
}
