using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace JingleBox2.Diagnostics;

/// <summary>
/// What the application leaves behind when it stops in a way nobody meant.
/// </summary>
/// <remarks>
/// Two kinds of ending, and they need different handling.
///
/// A managed exception nobody caught can be written down as it happens, with everything the
/// exception knows. A plugin dereferencing a null pointer inside this process cannot: the
/// process is gone, and code that runs afterwards does not exist. So the second kind is caught
/// the only way it can be, by leaving a note on the way in and looking for it on the way back
/// up. A note still lying there next time means the run that wrote it never finished.
///
/// The report is a file somebody can read and send, not a dump for a debugger: what was being
/// done, what the machine is, what happened lately, and the exception if there was one. It is
/// written whether or not the log is switched on, because the run that goes wrong is nearly
/// always the run nobody was logging.
/// </remarks>
public static class CrashReport
{
    /// <summary>Where reports are kept, under the folder the settings live in.</summary>
    public const string FolderName = "crashes";

    /// <summary>Written on the way in, deleted on the way out. Still there means it went wrong.</summary>
    private const string RunningFile = "running.marker";

    /// <summary>How many of the last things that happened a report carries.</summary>
    private const int Remembered = 200;

    private static readonly object Gate = new();

    private static readonly Queue<string> Lately = new();

    private static string _folder = "";
    private static bool _watching;
    private static DateTime _started;

    /// <summary>The report written for the run before this one, or empty when it ended properly.</summary>
    public static string FromLastTime { get; private set; } = "";

    /// <summary>
    /// Starts watching, and writes a report for the last run if it never came back.
    /// </summary>
    /// <remarks>
    /// Called once, as early as there is a folder to write into. The looking back has to happen
    /// before the marker for this run is written, or every run would report the one before it.
    /// </remarks>
    public static void Watch(string folder)
    {
        lock (Gate)
        {
            if (_watching) return;

            _watching = true;
            _folder = folder ?? "";
            _started = DateTime.Now;
        }

        LookBack();
        Mark();

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Write("something went wrong that nothing was watching for", e.ExceptionObject as Exception);

        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Write("a background job failed and nobody asked how it went", e.Exception);
            e.SetObserved();
        };

        // The ordinary way out. What is left of the marker says whether this ever happened.
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Rub();
    }

    /// <summary>
    /// Something worth having in a report if the next thing that happens is the end.
    /// </summary>
    /// <remarks>
    /// Always on, unlike the log: this is a few dozen lines an hour held in memory, and the
    /// point of it is to be there on the run nobody thought to switch logging on for.
    /// </remarks>
    public static void Note(string what)
    {
        if (string.IsNullOrEmpty(what)) return;

        string line = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + "  " + what;

        lock (Gate)
        {
            Lately.Enqueue(line);

            while (Lately.Count > Remembered) Lately.Dequeue();
        }
    }

    /// <summary>
    /// Writes a report. Safe to call from anywhere, including from a handler for the end.
    /// </summary>
    public static string Write(string reason, Exception? error = null, DateTime? began = null)
    {
        string folder;
        string[] lately;
        DateTime started;

        lock (Gate)
        {
            folder = _folder;
            lately = Lately.ToArray();
            started = began ?? _started;
        }

        if (folder.Length == 0) return "";

        try
        {
            string home = System.IO.Path.Combine(folder, FolderName);
            Directory.CreateDirectory(home);

            string path = System.IO.Path.Combine(
                home, "crash-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + ".txt");

            File.WriteAllText(path, Compose(reason, error, started, lately, began != null), new UTF8Encoding(false));

            Log.Write(LogArea.App, () => "a crash report was written to " + path);

            return path;
        }
        catch (Exception)
        {
            // A report that cannot be written is not worth an exception of its own on the way
            // out of an application that is already ending badly.
            return "";
        }
    }

    private static string Compose(
        string reason, Exception? error, DateTime started, string[] lately, bool lastTime)
    {
        var report = new StringBuilder(4096);

        report.Append("JingleBox ")
            .Append(System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "?")
            .Append(" stopped unexpectedly\n\n");

        report.Append("What happened : ").Append(reason).Append('\n');
        report.Append("When          : ").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append('\n');
        report.Append("That run began: ").Append(started.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)).Append('\n');
        report.Append("System        : ").Append(Environment.OSVersion).Append(", ")
            .Append(System.Runtime.InteropServices.RuntimeInformation.OSArchitecture).Append(", .NET ")
            .Append(Environment.Version).Append('\n');
        report.Append("Plugins       : ").Append(Audio.Plugins.PluginHost.Isolated
            ? "run in processes of their own"
            : "run inside this one, so a plugin that falls over takes the app with it").Append('\n');

        // A report written as it happens can ask what is in the air. One written for a run that
        // is already over cannot: that run's memory went with it, and what is left is the note
        // it wrote to disc before it tried, which the guard has turned into a block by now.
        var marks = lastTime
            ? Held(Audio.Plugins.PluginCrashGuard.Blocked, started)
            : Audio.Plugins.PluginCrashGuard.InFlight;

        if (marks.Count > 0)
        {
            report.Append("\nIn the middle of, when it stopped:\n");

            foreach (var mark in marks)
            {
                report.Append("  ").Append(mark.Name)
                    .Append("  (").Append(mark.Stage == Audio.Plugins.PluginStage.Load ? "loading" : "opening its window").Append(")  ")
                    .Append(mark.Path).Append('\n');
            }

            report.Append("\nThat is the plugin to suspect. It will not be tried again until you\n")
                .Append("let it through in SETTINGS.\n");
        }

        if (error != null)
        {
            report.Append("\nThe fault:\n").Append(error).Append('\n');
        }

        if (lately.Length > 0)
        {
            report.Append("\nWhat had been happening:\n");

            foreach (string line in lately) report.Append("  ").Append(line).Append('\n');
        }

        report.Append("\nThe log is ").Append(Log.IsOn ? "on, so " + Log.Path + " has the rest." :
            "off. Switch it on in SETTINGS before doing whatever went wrong, and there will be more to go on next time.").Append('\n');

        return report.ToString();
    }

    /// <summary>
    /// The notes that belong to the run that stopped, rather than to any run before it.
    /// </summary>
    private static IReadOnlyList<Audio.Plugins.PluginCrash> Held(
        IReadOnlyList<Audio.Plugins.PluginCrash> blocked, DateTime since)
    {
        var held = new List<Audio.Plugins.PluginCrash>();

        foreach (var mark in blocked)
        {
            if (mark.When >= since) held.Add(mark);
        }

        return held;
    }

    /// <summary>
    /// Looks for a marker from the run before this one and reports it if it is still there.
    /// </summary>
    private static void LookBack()
    {
        string path = Marker();
        if (path.Length == 0 || !File.Exists(path)) return;

        string[] said;

        try { said = File.ReadAllLines(path); }
        catch (Exception) { said = Array.Empty<string>(); }

        DateTime? began = null;

        foreach (string line in said)
        {
            Note("last time: " + line);

            // The marker says when the run that never came back started. Without it the report
            // would date the run by the moment it was noticed, which is this one starting up.
            const string Began = "started ";

            if (line.StartsWith(Began, StringComparison.Ordinal) &&
                DateTime.TryParse(line[Began.Length..], CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var when))
            {
                began = when;
            }
        }

        FromLastTime = Write(
            "the last run ended without saying goodbye, which is what a plugin taking the application down looks like",
            null,
            began);

        try { File.Delete(path); } catch (Exception) { }
    }

    /// <summary>Says this run is under way. See <see cref="LookBack"/>.</summary>
    private static void Mark()
    {
        string path = Marker();
        if (path.Length == 0) return;

        try
        {
            Directory.CreateDirectory(_folder);

            File.WriteAllText(path,
                "started " + _started.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + "\n" +
                "version " + (System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version?.ToString() ?? "?") + "\n",
                new UTF8Encoding(false));
        }
        catch (Exception)
        {
        }
    }

    /// <summary>Takes the marker off, for an application stopping the way it should.</summary>
    private static void Rub()
    {
        string path = Marker();
        if (path.Length == 0) return;

        try { if (File.Exists(path)) File.Delete(path); } catch (Exception) { }
    }

    private static string Marker() =>
        _folder.Length == 0 ? "" : System.IO.Path.Combine(_folder, RunningFile);
}
