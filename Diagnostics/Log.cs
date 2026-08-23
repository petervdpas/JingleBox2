using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace JingleBox2.Diagnostics;

/// <summary>What a line in the log is about, so a log can be read without reading all of it.</summary>
/// <remarks>
/// Flags rather than a list, because the useful question is nearly always "everything about
/// plugins and nothing else" and that has to be one comparison in a place where lines are
/// written thousands of times a second.
/// </remarks>
[Flags]
public enum LogArea
{
    None = 0,

    /// <summary>Starting up, settings, files.</summary>
    App = 1 << 0,

    /// <summary>The audio engine, devices, pads.</summary>
    Audio = 1 << 1,

    /// <summary>Plugins: loading, windows, parameters, the processes they run in.</summary>
    Plugins = 1 << 2,

    /// <summary>The tracker: patterns, the song, what marks it as unsaved.</summary>
    Tracker = 1 << 3,

    /// <summary>MIDI in and where it is routed.</summary>
    Midi = 1 << 4,

    Everything = App | Audio | Plugins | Tracker | Midi
}

/// <summary>
/// One log, for the app and for every process it starts, switched on and off in one place.
/// </summary>
/// <remarks>
/// Off by default and free when off: a line costs one comparison against a field, and the
/// message is not even built unless somebody asked for it, which is what the callback form of
/// <see cref="Write(LogArea, Func{string})"/> is for.
///
/// Plugins run in processes of their own and they write here too. Every line carries the
/// process it came from, so a plugin falling over in its own process leaves its account of it
/// next to what the application was doing at the time, in the order it happened.
///
/// The file is kept where the settings are and rolled over when it gets big, so a log left
/// switched on for a week cannot quietly fill a disk.
/// </remarks>
public static class Log
{
    /// <summary>Set this to 1 to log without the settings, for a run that will not get that far.</summary>
    public const string Variable = "JB_LOG";

    /// <summary>What the file is called. The one before it keeps the same name with .old on it.</summary>
    public const string FileName = "jinglebox.log";

    /// <summary>How big the file gets before it is rolled over.</summary>
    private const long RollBytes = 4 * 1024 * 1024;

    private static readonly object Gate = new();

    private static readonly UTF8Encoding Text = new(encoderShouldEmitUTF8Identifier: false);

    private static LogArea _areas;
    private static string _folder = "";
    private static bool _started;
    private static bool _hooked;

    /// <summary>
    /// Lines waiting to be written, and the thread that writes them.
    /// </summary>
    /// <remarks>
    /// Whoever writes a line does not wait for a disc. Some of these lines come from the
    /// thread filling the audio buffer, and opening a file, appending to it and closing it
    /// again is not something that thread can be asked to do: it has a block to finish and a
    /// few milliseconds to finish it in. So a line is formed where it happens, which is
    /// nothing but a string, and handed over.
    ///
    /// One queue and one writer, so the file still reads in the order things happened.
    /// </remarks>
    private static readonly ConcurrentQueue<string> Waiting = new();

    private static readonly AutoResetEvent Knock = new(false);

    private static Thread? _writer;

    /// <summary>
    /// How many lines may be waiting before they start being dropped.
    /// </summary>
    /// <remarks>
    /// A bound, not a target. Something writing faster than a disc can keep up must lose lines
    /// rather than memory, and a log is worth less than the thing it is a log of.
    /// </remarks>
    private const int MostWaiting = 40000;

    private static int _lost;

    /// <summary>Which areas are being written. None means the log is off.</summary>
    public static LogArea Areas => _areas;

    /// <summary>True when anything at all is being written.</summary>
    public static bool IsOn => _areas != LogArea.None;

    /// <summary>Where the file is, for a page that wants to say so.</summary>
    public static string Path
    {
        get
        {
            lock (Gate) return _folder.Length == 0 ? "" : System.IO.Path.Combine(_folder, FileName);
        }
    }

    /// <summary>
    /// Says where the log lives and whether it is on. Called once at startup, and again
    /// whenever the setting is changed.
    /// </summary>
    /// <remarks>
    /// The environment variable wins over the setting, so a build that will not start far
    /// enough to reach its settings can still be made to talk.
    /// </remarks>
    public static void Open(string folder, bool on, LogArea areas = LogArea.Everything)
    {
        bool forced = Environment.GetEnvironmentVariable(Variable) == "1";

        lock (Gate)
        {
            _folder = folder ?? "";
            _areas = on || forced ? areas : LogArea.None;

            // Whatever is still waiting when this stops is written before it does. A log that
            // loses its last lines loses exactly the ones anybody wanted.
            if (!_hooked)
            {
                _hooked = true;
                AppDomain.CurrentDomain.ProcessExit += (_, _) => Flush();
            }
        }

        if (!IsOn) return;

        Announce();
    }

    /// <summary>Turns it off without forgetting where it was, and writes what is waiting.</summary>
    public static void Close()
    {
        lock (Gate) _areas = LogArea.None;

        Flush();
    }

    private static void Announce()
    {
        if (_started) return;
        _started = true;

        Write(LogArea.App, () =>
            "logging on: " + _areas + ", " + Environment.ProcessPath +
            " version " + (System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "?"));
    }

    /// <summary>Writes a line, if that area is being written.</summary>
    public static void Write(LogArea area, string message)
    {
        if ((_areas & area) == 0) return;

        Put(area, message);
    }

    /// <summary>
    /// The same, for a message that costs something to build. The callback is not run unless
    /// the line is going to be written.
    /// </summary>
    public static void Write(LogArea area, Func<string> message)
    {
        if ((_areas & area) == 0 || message == null) return;

        string text;

        try
        {
            text = message();
        }
        catch (Exception error)
        {
            text = "a log line threw while being written: " + error.Message;
        }

        Put(area, text);
    }

    /// <summary>Writes what went wrong, with everything the exception knows.</summary>
    public static void Fault(LogArea area, string what, Exception error)
    {
        if ((_areas & area) == 0) return;

        Put(area, what + ": " + (error?.ToString() ?? "no reason given"));
    }

    private static void Put(LogArea area, string message)
    {
        string folder;

        lock (Gate) folder = _folder;

        if (folder.Length == 0) return;

        var line = new StringBuilder(message.Length + 64);

        line.Append(DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture))
            .Append("  ")
            .Append(Short(area).PadRight(7))
            .Append(Environment.ProcessId.ToString(CultureInfo.InvariantCulture).PadLeft(7))
            .Append("  ")
            .Append(message)
            .Append('\n');

        if (Waiting.Count >= MostWaiting)
        {
            Interlocked.Increment(ref _lost);
            return;
        }

        Waiting.Enqueue(line.ToString());

        Scribe();

        Knock.Set();
    }

    /// <summary>The thread that does the writing, started the first time there is anything to write.</summary>
    private static void Scribe()
    {
        if (_writer != null) return;

        lock (Gate)
        {
            if (_writer != null) return;

            _writer = new Thread(Writing)
            {
                IsBackground = true,
                Name = "log"
            };

            _writer.Start();
        }
    }

    private static void Writing()
    {
        while (true)
        {
            // Woken by a line, and looked at anyway now and then in case a wake-up was missed
            // while the last batch was being written.
            Knock.WaitOne(250);

            Flush();
        }
    }

    /// <summary>
    /// Writes whatever is waiting, in one go.
    /// </summary>
    /// <remarks>
    /// A batch rather than a line at a time: opening and closing the file is most of the cost
    /// of writing to it, and a busy second produces hundreds of lines that all belong in the
    /// same open.
    /// </remarks>
    public static void Flush()
    {
        if (Waiting.IsEmpty && _lost == 0) return;

        string folder;

        lock (Gate) folder = _folder;

        if (folder.Length == 0) return;

        var batch = new StringBuilder(4096);

        while (Waiting.TryDequeue(out var line)) batch.Append(line);

        int lost = Interlocked.Exchange(ref _lost, 0);

        if (lost > 0)
        {
            batch.Append("(")
                .Append(lost.ToString(CultureInfo.InvariantCulture))
                .Append(" line(s) went unwritten: the log could not keep up)\n");
        }

        if (batch.Length == 0) return;

        try
        {
            Directory.CreateDirectory(folder);

            string path = System.IO.Path.Combine(folder, FileName);

            Roll(path);

            // Without the byte order mark: this is a file people open in a text editor and
            // paste out of, not one anything parses.
            File.AppendAllText(path, batch.ToString(), Text);
        }
        catch (Exception)
        {
            // A log that cannot be written is not worth an exception in the thing being logged.
        }
    }

    /// <summary>
    /// Keeps one old file and starts a new one when the current gets big. Two files of a few
    /// megabytes is a bounded cost for something somebody may leave switched on.
    /// </summary>
    private static void Roll(string path)
    {
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists || file.Length < RollBytes) return;

            string old = path + ".old";

            if (File.Exists(old)) File.Delete(old);

            File.Move(path, old);
        }
        catch (Exception)
        {
        }
    }

    private static readonly Dictionary<LogArea, string> Names = new()
    {
        [LogArea.App] = "app",
        [LogArea.Audio] = "audio",
        [LogArea.Plugins] = "plugin",
        [LogArea.Tracker] = "tracker",
        [LogArea.Midi] = "midi"
    };

    private static string Short(LogArea area) => Names.TryGetValue(area, out var name) ? name : "log";
}
