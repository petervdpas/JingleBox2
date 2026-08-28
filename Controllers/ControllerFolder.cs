using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JingleBox2.Diagnostics;

namespace JingleBox2.Controllers;

/// <summary>
/// Where a controller's own files live, and how one is matched to a port.
/// </summary>
/// <remarks>
/// A controller can have two files and needs neither. A <c>.json</c> saying what it is, and a
/// <c>.lua</c> saying what it does, both named after the device and sitting side by side. The
/// split is the whole design: what a MiniLab 3 <i>is</i> is a fact about every MiniLab 3 there
/// will ever be and belongs in a file anybody can read, and what one <i>does</i> is behaviour
/// and needs a language. Most controllers will only ever want the first.
///
/// Two folders, as machines have two. Beside the program is what ships and is never written to;
/// under the application folder is what this installation has. The first run fills the second
/// from the first, and only when it is not there at all: empty is somebody who threw them out,
/// and putting them back would be undoing that.
/// </remarks>
public static class ControllerFolder
{
    /// <summary>What the folder is called, in both places it exists.</summary>
    /// <remarks>
    /// The same word beside the program and under the application folder, so somebody told
    /// where their controller files are has been told where both of them are.
    /// </remarks>
    public const string Name = "controllers";

    /// <summary>Where the controller files that ship with the program live.</summary>
    public static string Shipped => Path.Combine(AppContext.BaseDirectory, Name);

    /// <summary>And where the ones this installation has live.</summary>
    public static string Installed => Path.Combine(Config.AppFolder.Path(), Name);

    /// <summary>What has already been offered, so a new file can arrive and a deleted one cannot.</summary>
    private const string OfferedName = "offered.txt";

    /// <summary>
    /// Gives this installation any controller file the program ships that it has never been
    /// offered.
    /// </summary>
    /// <remarks>
    /// It was the absence of the folder that decided, which is right while the set of files
    /// never changes and wrong the moment one is added. The folder was made the first time a
    /// codec shipped, so the profile that shipped an hour later could never arrive: the folder
    /// was there, so there was nothing to do. Exactly the mistake
    /// <see cref="Tracker.Machines.MachineRegistry"/> had already made and already fixed, which
    /// is where this is copied from.
    ///
    /// So what is recorded is the offer, not the folder. A file this installation has never been
    /// offered is put in; one it has been offered is left alone whether or not it is still
    /// there, which is what keeps a codec somebody deleted deleted.
    ///
    /// A folder from before this record existed is taken to have been offered whatever it holds.
    /// Right for everything anybody kept, and wrong once for anything they had already thrown
    /// out, which comes back a single time and stays gone after.
    ///
    /// The offer is recorded whether or not the file went in. One that cannot be copied has
    /// still been offered, and trying again on every start would write the same fault into the
    /// log for ever.
    ///
    /// Unlike machines, nothing is ever refreshed from what ships. A machine that ships is the
    /// machine and an update to it should reach the rack. A controller file is the opposite: the
    /// entire point of the folder is that you edit what is in it, and overwriting somebody's
    /// codec with ours because ours is newer would throw away the work the folder exists for.
    /// </remarks>
    public static void FirstRun()
    {
        try
        {
            bool fresh = !Directory.Exists(Installed);

            Directory.CreateDirectory(Installed);

            if (!Directory.Exists(Shipped)) return;

            var offered = Offered(fresh);
            bool moved = false;

            foreach (string file in Directory.GetFiles(Shipped))
            {
                string name = Path.GetFileName(file);

                if (string.Equals(name, OfferedName, StringComparison.OrdinalIgnoreCase)) continue;
                if (!offered.Add(name)) continue;

                moved = true;

                try
                {
                    File.Copy(file, Path.Combine(Installed, name), overwrite: false);

                    Log.Write(LogArea.Midi, () => "controllers: '" + name + "' is new, so it has been put in");
                }
                catch (Exception bad)
                {
                    Log.Write(LogArea.Midi, () => "controllers: '" + name + "' could not be copied: " + bad.Message);
                }
            }

            if (moved) Remember(offered);
        }
        catch (Exception bad)
        {
            Log.Write(LogArea.Midi, () => "controllers: cannot prepare '" + Installed + "': " + bad.Message);
        }
    }

    /// <summary>
    /// The files this installation has been offered before now.
    /// </summary>
    /// <remarks>
    /// A folder with no record is a folder from before there was one, and everything in it
    /// counts as offered. Otherwise the first start after this lands would put back every file
    /// anybody had ever deleted.
    /// </remarks>
    private static HashSet<string> Offered(bool fresh)
    {
        var offered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string record = Path.Combine(Installed, OfferedName);

        try
        {
            if (File.Exists(record))
            {
                foreach (string line in File.ReadAllLines(record))
                    if (line.Trim() is { Length: > 0 } one) offered.Add(one);

                return offered;
            }

            if (!fresh)
                foreach (string file in Directory.GetFiles(Installed))
                    offered.Add(Path.GetFileName(file));
        }
        catch (Exception bad)
        {
            Log.Write(LogArea.Midi, () => "controllers: cannot read what has been offered: " + bad.Message);
        }

        return offered;
    }

    /// <summary>Writes down what has been offered, so the next start does not offer it again.</summary>
    /// <remarks>
    /// Sorted, because this is a file a person may open when they want to know why a codec they
    /// deleted has not come back.
    /// </remarks>
    private static void Remember(HashSet<string> offered)
    {
        try
        {
            File.WriteAllLines(Path.Combine(Installed, OfferedName), offered.OrderBy(one => one, StringComparer.Ordinal));
        }
        catch (Exception bad)
        {
            Log.Write(LogArea.Midi, () => "controllers: cannot write what has been offered: " + bad.Message);
        }
    }

    /// <summary>A port's name against a pattern, where a star stands for anything at all.</summary>
    /// <remarks>
    /// Deliberately the smallest possible matcher. A port is called `Minilab3 MIDI` on Linux and
    /// the same thing with a number in front of it on Windows, so a pattern is the least a match
    /// can be and still work in both places. Anything more is a language nobody asked for.
    ///
    /// A pattern with no star is a contains, since that is what somebody writing one means.
    /// </remarks>
    public static bool Like(string pattern, string text)
    {
        if (string.IsNullOrWhiteSpace(pattern) || string.IsNullOrWhiteSpace(text)) return false;

        var parts = pattern.Split('*');

        if (parts.Length == 1)
            return text.Contains(pattern, StringComparison.OrdinalIgnoreCase);

        int at = 0;

        for (int part = 0; part < parts.Length; part++)
        {
            if (parts[part].Length == 0) continue;

            int found = text.IndexOf(parts[part], at, StringComparison.OrdinalIgnoreCase);
            if (found < 0) return false;
            if (part == 0 && found != 0) return false;

            at = found + parts[part].Length;
        }

        return parts[^1].Length == 0 || text.EndsWith(parts[^1], StringComparison.OrdinalIgnoreCase);
    }
}
