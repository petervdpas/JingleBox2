using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Diagnostics.Interfaces;

namespace JingleBox2.Diagnostics;

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
///
/// Static, and the only door here that is. A log is one queue, one writing thread and one file
/// for the whole process and every process it starts, so handing one about would be handing
/// about the same object under another name, and fifty three callers including the thread that
/// fills the audio buffer would pay a field lookup for it. What it decides does not live here:
/// <see cref="ILogAreas"/>, <see cref="ILogLine"/> and <see cref="ILogFile"/> are the rules on
/// their own and can be asked anything without a disc or a thread. What is left is the
/// plumbing.
/// </remarks>
public static class Log
{
    /// <summary>Which areas are on, and what each is called. Holds nothing, so one is enough.</summary>
    private static readonly ILogAreas Switch = new LogAreas();

    /// <summary>The shape of a line, which is the same for every line this process writes.</summary>
    private static readonly ILogLine Shape = new LogLine(Switch);

    /// <summary>The file itself: appending to it, and starting a new one when it gets big.</summary>
    private static readonly ILogFile Store = new LogFile();

    /// <summary>
    /// Set this to log without the settings, for a run that will not get that far.
    /// </summary>
    /// <remarks>
    /// <c>JB_LOG=1</c> for everything, or the areas by name: <c>JB_LOG=midi</c>, or
    /// <c>JB_LOG=midi,plugin</c>. A name this build does not know is passed over rather than
    /// refused, so a variable left set from a later version still starts the application.
    /// </remarks>
    public const string Variable = "JB_LOG";

    /// <summary>What the file is called. The one before it keeps the same name with .old on it.</summary>
    public const string FileName = "jinglebox.log";

    /// <summary>How big the file gets before it is rolled over.</summary>
    private const long RollBytes = 4 * 1024 * 1024;

    /// <summary>How long the writing thread waits to be told before looking anyway.</summary>
    /// <remarks>
    /// A wake-up can be missed while the last batch is being written, and a line that sits in
    /// the queue until the next one arrives is a line missing from the log of a run that hung.
    /// </remarks>
    private const int WakeMs = 250;

    /// <summary>Guards the settings and the folder, which are read from every thread that writes.</summary>
    private static readonly object Gate = new();

    /// <summary>Which areas are being written, and none of them when the log is off.</summary>
    private static LogArea _areas;

    /// <summary>Where the file goes, which is empty until somebody has said.</summary>
    private static string _folder = "";

    /// <summary>Whether the line naming the build has been written, so it is written once.</summary>
    private static bool _started;

    /// <summary>Whether the handler that writes out what is waiting at the end is on.</summary>
    /// <remarks>
    /// Its own flag rather than hooking in <see cref="Open"/> unconditionally, since Open is
    /// called again every time the setting is changed and one handler is enough.
    /// </remarks>
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

    /// <summary>How the writing thread is told there is something to write.</summary>
    private static readonly AutoResetEvent Knock = new(false);

    /// <summary>The one thread that writes, made the first time there is a line to write.</summary>
    private static Thread? _writer;

    /// <summary>
    /// How many lines may be waiting before they start being dropped.
    /// </summary>
    /// <remarks>
    /// A bound, not a target. Something writing faster than a disc can keep up must lose lines
    /// rather than memory, and a log is worth less than the thing it is a log of.
    /// </remarks>
    private const int MostWaiting = 40000;

    /// <summary>How many lines went unwritten, said once in the file rather than per line.</summary>
    private static int _lost;

    /// <summary>Which areas are being written. None means the log is off.</summary>
    public static LogArea Areas => _areas;

    /// <summary>True when anything at all is being written.</summary>
    public static bool IsOn => _areas != LogArea.None;

    /// <summary>
    /// Whether anything written about that area would be kept.
    /// </summary>
    /// <remarks>
    /// For the handful of places that write a line per MIDI message. The guard inside
    /// <see cref="Write(LogArea, Func{string})"/> is checked after the caller has already built
    /// the closure holding whatever the line mentions, which is an object and a delegate
    /// allocated on every message whether or not anybody is reading. Asked first, that goes to
    /// one comparison. Not worth doing anywhere else: a line written when something is decided
    /// or has gone wrong costs nothing worth counting.
    /// </remarks>
    public static bool On(LogArea area) => (_areas & area) != 0;

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
    /// The environment variable wins over the setting, and says which areas as well as whether,
    /// so a build that will not start far enough to reach its settings can still be made to
    /// talk, and a run nobody can start is exactly the run worth narrowing by hand.
    ///
    /// Whatever is still waiting when the process ends is written before it goes. A log that
    /// loses its last lines loses exactly the ones anybody wanted.
    /// </remarks>
    public static void Open(string folder, bool on, LogArea areas = LogArea.Everything)
    {
        string? said = Environment.GetEnvironmentVariable(Variable);

        lock (Gate)
        {
            _folder = folder ?? "";
            _areas = Switch.Wanted(on, areas, said);

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

    /// <summary>Writes the line saying what is being logged and by which build, once a run.</summary>
    /// <remarks>
    /// It is the first thing anybody reading a log wants and the last thing they would think to
    /// ask for, since a log from a version nobody can name says nothing about the version.
    /// </remarks>
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

    /// <summary>
    /// Forms the line and hands it over. Nothing here waits for a disc.
    /// </summary>
    /// <remarks>
    /// The time, the area and the process go on the front: the process because plugins run in
    /// their own and write here too, so their account of what happened sits beside the
    /// application's in the order it happened.
    /// </remarks>
    private static void Put(LogArea area, string message)
    {
        string folder;

        lock (Gate) folder = _folder;

        if (folder.Length == 0) return;

        if (Waiting.Count >= MostWaiting)
        {
            Interlocked.Increment(ref _lost);
            return;
        }

        Waiting.Enqueue(Shape.Format(area, DateTime.Now, Environment.ProcessId, message));

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

    /// <summary>
    /// The writing thread's whole life: wait to be told, write what is waiting, wait again.
    /// </summary>
    /// <remarks>
    /// Woken by a line, and looked at anyway every quarter of a second in case a wake-up was
    /// missed while the last batch was being written.
    /// </remarks>
    private static void Writing()
    {
        while (true)
        {
            Knock.WaitOne(WakeMs);

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
    ///
    /// A file that cannot be written is passed over in silence. A log is not worth an exception
    /// in the thing it is a log of.
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

        if (lost > 0) batch.Append(Shape.Lost(lost));

        if (batch.Length == 0) return;

        string path = System.IO.Path.Combine(folder, FileName);

        Store.Roll(path, RollBytes);
        Store.Append(path, batch.ToString());
    }

    /// <inheritdoc cref="ILogAreas.Everywhere"/>
    public static IReadOnlyDictionary<LogArea, string> Everywhere => Switch.Everywhere;
}
