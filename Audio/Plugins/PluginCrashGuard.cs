using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace JingleBox2.Audio.Plugins;

/// <summary>What the host was doing with a plugin when everything stopped.</summary>
public enum PluginStage
{
    /// <summary>Opening or closing the plugin's own window.</summary>
    Window = 0,

    /// <summary>Loading the plugin at all, before any audio or any window.</summary>
    Load = 1
}

/// <summary>One plugin that took the application down, and when.</summary>
public sealed class PluginCrash
{
    public string Path { get; set; } = "";

    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    /// <summary>When it happened, so a note about it can say something better than "once".</summary>
    public DateTime When { get; set; } = DateTime.Now;

    /// <summary>
    /// What was being done at the time. A plugin that cannot open a window is still a plugin
    /// that plays; one that cannot be loaded is of no use at all, and the two are not the
    /// same punishment.
    /// </summary>
    public PluginStage Stage { get; set; } = PluginStage.Window;
}

/// <summary>
/// Remembers which plugins have crashed the application while opening their own window, and
/// refuses to open those again.
/// </summary>
/// <remarks>
/// A plugin runs inside this process, so a plugin that dereferences a null pointer takes the
/// whole application with it. Nothing managed can catch that: there is no exception, no
/// unwinding and no finally block. The only thing a host can do is notice afterwards.
///
/// So the note is written to disk before the plugin is handed a window, and rubbed out once
/// the window has been up long enough to be believed. If the note is still there at the next
/// start, that plugin is what killed the last one, and its window is not offered again until
/// somebody asks for it explicitly.
///
/// This is what a real desk does. Bitwig and Reaper both keep a list like this, for the same
/// reason and by the same method. It costs one small file and it is the difference between
/// losing a plugin's interface and losing the song.
/// </remarks>
public static class PluginCrashGuard
{
    /// <summary>
    /// How long a window has to stay up before it counts as having opened. The faults seen so
    /// far happen within a few hundred milliseconds, on the plugin's first timers.
    /// </summary>
    public const int SettleSeconds = 6;

    private const string MarkerFile = "plugin-opening.json";
    private const string BlockedFile = "plugin-blocked.json";

    private static readonly object Gate = new();
    private static readonly List<PluginCrash> Blocked_ = new();

    private static string? _folder;
    private static bool _read;

    /// <summary>Where the notes live. The same folder as the rest of the settings.</summary>
    public static string Folder
    {
        get
        {
            if (_folder != null) return _folder;

            string root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _folder = System.IO.Path.Combine(root, "JingleBox2");

            try
            {
                Directory.CreateDirectory(_folder);
            }
            catch (Exception)
            {
                // Nowhere to write means no guard, which is how this worked before there was
                // one. It must not be a reason for the application not to start.
            }

            return _folder;
        }
    }

    /// <summary>
    /// Somewhere else to keep the notes, for checking. Also forgets what has been read, so a
    /// check starts from what is actually in that folder.
    /// </summary>
    public static void UseFolderForTest(string folder)
    {
        lock (Gate)
        {
            _folder = folder;
            _read = false;
            Blocked_.Clear();
        }

        if (!string.IsNullOrWhiteSpace(folder)) Directory.CreateDirectory(folder);
    }

    /// <summary>Every plugin that is not to be opened, newest first.</summary>
    public static IReadOnlyList<PluginCrash> Blocked
    {
        get
        {
            Read();
            lock (Gate) return Blocked_.ToArray();
        }
    }

    /// <summary>
    /// Reads what is on disk, once. A note left behind by the last run is turned into a block
    /// here: nothing else could have written it, because it is rubbed out on the way out.
    /// </summary>
    private static void Read()
    {
        lock (Gate)
        {
            if (_read) return;
            _read = true;

            Blocked_.AddRange(Load<List<PluginCrash>>(BlockedFile) ?? new List<PluginCrash>());

            var left = Load<List<PluginCrash>>(MarkerFile);
            if (left == null || left.Count == 0) return;

            // The last run died with these in the middle of something. That is what the notes
            // are for, and the only way they survive a run is if nobody got to rub them out.
            bool added = false;

            foreach (var note in left)
            {
                if (Holds(note.Path, note.Id)) continue;

                Blocked_.Insert(0, note);
                added = true;
            }

            if (added) Save(BlockedFile, Blocked_);

            Marks.Clear();
            Write();
        }
    }

    /// <summary>True when this plugin is not to be given a window of its own.</summary>
    public static bool IsBlocked(PluginInfo? plugin)
    {
        if (plugin == null) return false;

        Read();
        lock (Gate) return Holds(plugin.Path, plugin.Id);
    }

    /// <summary>
    /// True when this plugin is not to be loaded at all, because loading it is what killed
    /// the application last time.
    /// </summary>
    public static bool IsLoadBlocked(PluginInfo? plugin)
    {
        if (plugin == null) return false;

        Read();

        lock (Gate)
        {
            foreach (var blocked in Blocked_)
            {
                if (blocked.Stage == PluginStage.Load && Same(blocked.Path, blocked.Id, plugin.Path, plugin.Id))
                    return true;
            }
        }

        return false;
    }

    /// <summary>What to tell somebody who asked for a window they are not getting.</summary>
    public static string Reason(PluginInfo? plugin)
    {
        if (plugin == null || !IsBlocked(plugin)) return "";

        PluginCrash? crash = null;

        lock (Gate)
        {
            foreach (var blocked in Blocked_)
            {
                if (Same(blocked.Path, blocked.Id, plugin.Path, plugin.Id)) { crash = blocked; break; }
            }
        }

        string when = crash == null ? "" : " on " + crash.When.ToString("d MMMM, HH:mm");

        if (crash != null && crash.Stage == PluginStage.Load)
        {
            return $"'{plugin.Name}' brought the application down while being loaded{when}, " +
                   "so it is not being loaded again.";
        }

        return $"'{plugin.Name}' brought the application down while its own window was opening " +
               $"or closing{when}. Its knobs are shown here instead. The sound is unaffected: it " +
               "still plays and still saves.";
    }

    /// <summary>What is in the middle of happening, ready to be written down.</summary>
    private static readonly List<PluginCrash> Marks = new();

    /// <summary>
    /// About to do something with this plugin that has been known to kill a host. Written down
    /// before rather than after, because afterwards may not happen.
    /// </summary>
    /// <remarks>
    /// More than one can be in the air at once: a song opening loads a chain while somebody
    /// has a window up, so these are kept as a list rather than a single note. If the worst
    /// happens with two in flight both are blocked, which is the safe way round and either
    /// can be let through again.
    /// </remarks>
    public static void Risky(PluginInfo? plugin, PluginStage stage)
    {
        if (plugin == null) return;

        Read();

        lock (Gate)
        {
            Marks.RemoveAll(mark => Same(mark.Path, mark.Id, plugin.Path, plugin.Id) && mark.Stage == stage);

            Marks.Add(new PluginCrash
            {
                Path = plugin.Path,
                Id = plugin.Id,
                Name = plugin.Name,
                Stage = stage,
                When = DateTime.Now
            });

            Write();
        }
    }

    /// <summary>
    /// That went well. The note comes off.
    /// </summary>
    public static void Survived(PluginInfo? plugin)
    {
        if (plugin == null) return;

        lock (Gate)
        {
            if (Marks.RemoveAll(mark => Same(mark.Path, mark.Id, plugin.Path, plugin.Id)) == 0) return;

            Write();
        }
    }

    /// <summary>Writes down whatever is in the air, or rubs the note out when nothing is.</summary>
    private static void Write()
    {
        if (Marks.Count == 0)
        {
            Rub();
            return;
        }

        Save(MarkerFile, Marks);
    }

    /// <summary>
    /// Lets a blocked plugin be tried again, for somebody who has updated it or simply wants
    /// to find out. If it goes down again it goes straight back on the list.
    /// </summary>
    public static void Allow(PluginInfo? plugin)
    {
        if (plugin == null) return;

        Read();

        lock (Gate)
        {
            Blocked_.RemoveAll(blocked => Same(blocked.Path, blocked.Id, plugin.Path, plugin.Id));
            Save(BlockedFile, Blocked_);
        }
    }

    /// <summary>Forgets every block, for a settings page that offers it.</summary>
    public static void AllowEverything()
    {
        Read();

        lock (Gate)
        {
            Blocked_.Clear();
            Save(BlockedFile, Blocked_);
        }
    }

    private static bool Holds(string path, string id)
    {
        foreach (var blocked in Blocked_)
        {
            if (Same(blocked.Path, blocked.Id, path, id)) return true;
        }

        return false;
    }

    /// <summary>
    /// The same plugin. Matched on the class inside the file as well as the file: one bundle
    /// can hold a synth and an effect, and one of them crashing says nothing about the other.
    /// </summary>
    private static bool Same(string leftPath, string leftId, string rightPath, string rightId)
    {
        return string.Equals(leftPath, rightPath, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(leftId, rightId, StringComparison.OrdinalIgnoreCase);
    }

    private static void Rub()
    {
        try
        {
            string path = System.IO.Path.Combine(Folder, MarkerFile);
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception)
        {
            // A note that will not come off means one plugin gets blocked that need not have
            // been, which is the safe way round.
        }
    }

    private static T? Load<T>(string name) where T : class
    {
        try
        {
            string path = System.IO.Path.Combine(Folder, name);
            if (!File.Exists(path)) return null;

            return JsonSerializer.Deserialize<T>(File.ReadAllText(path));
        }
        catch (Exception)
        {
            // A note nobody can read is no note.
            return null;
        }
    }

    private static void Save(string name, object what)
    {
        try
        {
            string path = System.IO.Path.Combine(Folder, name);
            File.WriteAllText(path, JsonSerializer.Serialize(what, Indented));
        }
        catch (Exception)
        {
            // Nowhere to write means no guard. Not a reason to refuse to open a plugin.
        }
    }

    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };
}
