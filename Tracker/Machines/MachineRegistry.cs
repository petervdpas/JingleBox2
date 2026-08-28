using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JingleBox2.Diagnostics.Enums;

namespace JingleBox2.Tracker.Machines;

/// <summary>
/// What machines this installation has.
/// </summary>
/// <remarks>
/// A machine is a project: a folder with a manifest in it. This reads the folders and hands
/// what it finds to <see cref="Machine.Register"/>, so what the rack shows, what the panels are
/// painted with and what a song writes down all come off the machines themselves rather than
/// out of the application's own code.
///
/// Two folders, and only one of them is this installation's. Beside the program is what the
/// application ships: a source to take a machine from, never written to and never read as the
/// answer to what is on the rack. Under the app folder is what this installation actually has,
/// and that one alone decides. The point of the split is that removing a machine is not losing
/// it: the shipped copy stays where it was and the machine can be taken again.
///
/// The one moment the two touch is the first run, when there is no installation folder at all
/// and it is filled from what ships. An installation folder that exists and is empty is not the
/// same thing: that is somebody who took every machine out, and filling it again would be
/// undoing what they did.
///
/// A machine the app has no engine for is read and ignored for now. That is the piece the
/// contract still needs, and until it lands, importing one would put a box on the rack that
/// cannot make a sound.
///
/// Everything here writes to <see cref="Diagnostics.Enums.LogArea.Machines"/> rather than to the
/// application's own area, as everything under this folder does. What machines were found and
/// which of them could not be read is a whole half of the program, and reading it out of
/// everything the application did at startup is exactly what nobody wants on the day a machine
/// comes back missing its picture.
/// </remarks>
public static class MachineRegistry
{
    /// <summary>What the folder holding the machines is called, in both places it appears.</summary>
    /// <remarks>
    /// Written out rather than built, so the one folder name this depends on can be found by
    /// looking for it.
    /// </remarks>
    public const string FolderName = "machines";

    /// <summary>Where the machines that ship with the program live.</summary>
    public static string Shipped =>
        Path.Combine(AppContext.BaseDirectory, FolderName);

    /// <summary>And where the ones this installation has live.</summary>
    public static string Installed =>
        Path.Combine(Config.AppFolder.Path(), FolderName);

    /// <summary>
    /// True when a file is one the program ships, and so is on every installation there is.
    /// </summary>
    /// <remarks>
    /// Asked by a song about to be packed for somebody else. A machine's own presets are
    /// installed with the application, so putting them in the file would be sending a person a
    /// copy of something they already have, once per song. What is worth carrying is what only
    /// this machine has: the user's own takes.
    ///
    /// Answered by looking, not by where the path points. Both folders hold machines and the
    /// installed one holds the user's as well, so the only honest test is whether the same file
    /// is also in the folder beside the program.
    /// </remarks>
    public static bool Ships(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        try
        {
            string installed = Path.GetFullPath(Installed);
            string full = Path.GetFullPath(path);

            if (!full.StartsWith(installed + Path.DirectorySeparatorChar, FilePaths.Comparison))
                return false;

            return File.Exists(Path.Combine(Shipped, full.Substring(installed.Length + 1)));
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>
    /// Reads the machines this installation has and takes them into the list the app works from.
    /// </summary>
    /// <remarks>
    /// Everything read last time is forgotten first. A machine thrown out in SETTINGS has to be
    /// gone from the list the moment it is rebuilt, not at the next start.
    /// </remarks>
    /// <returns>What was taken, for the log and for the settings page to show.</returns>
    public static IReadOnlyList<MachineProject> Load()
    {
        Seed();

        Machine.Forget();

        var taken = new List<MachineProject>();

        foreach (var project in In(Installed))
        {
            if (!Machine.Register(project.Id, project.Name, project.Summary, project.Theme)) continue;

            taken.Add(project);

            Diagnostics.Log.Write(Diagnostics.Enums.LogArea.Machines,
                () => "machine " + project.Id + " from " + project.Folder);
        }

        return taken;
    }

    /// <summary>The machines that ship and are not installed here, which are the ones on offer.</summary>
    public static IReadOnlyList<MachineProject> Available()
    {
        var here = new HashSet<string>(In(Installed).Select(p => p.Id), StringComparer.Ordinal);

        return In(Shipped).Where(p => !here.Contains(p.Id)).ToList();
    }

    /// <summary>The projects in that folder, or none when there is no folder.</summary>
    /// <remarks>
    /// A folder that will not read is nothing rather than a fault: this is called on the way to
    /// drawing the rack, and one unreadable folder should not take the rack with it.
    /// </remarks>
    public static IReadOnlyList<MachineProject> In(string folder)
    {
        if (!Directory.Exists(folder)) return Array.Empty<MachineProject>();

        try
        {
            return Directory.GetDirectories(folder)
                .Select(MachineProject.Open)
                .Where(p => p != null && p.Id.Length > 0)
                .Select(p => p!)
                .ToList();
        }
        catch (Exception ex)
        {
            Diagnostics.Log.Fault(Diagnostics.Enums.LogArea.Machines, "Machines could not be read from " + folder, ex);

            return Array.Empty<MachineProject>();
        }
    }

    /// <summary>What the file recording the offer is called.</summary>
    /// <remarks>
    /// Written out rather than built from the folder's name, so the one file this depends on can
    /// be found by looking for it.
    /// </remarks>
    private const string OfferedName = "offered.txt";

    /// <summary>
    /// Gives the installation any machine the program ships that it has never been offered.
    /// </summary>
    /// <remarks>
    /// It used to be the absence of the folder that decided, which was right while the set of
    /// machines never changed and wrong the moment one was added: a machine written after the
    /// folder was made could never arrive, because the folder was there. Every new machine then
    /// cost a trip to SETTINGS before it could be seen at all, and the panel it draws stayed
    /// hidden behind the hand written one with nothing saying why.
    ///
    /// So the offer is what is recorded, not the folder. A shipped machine this installation has
    /// never been offered is put on the rack; one it has been offered is left alone whether or
    /// not it is still there, which is what keeps a machine somebody threw out thrown out.
    ///
    /// An installation from before this file existed is taken to have been offered whatever it
    /// currently holds. That is right for everything anybody kept and wrong once for anything
    /// they had already removed: it comes back a single time, and stays gone after that.
    ///
    /// A machine already offered is not frozen: it is this installation's to keep or throw out,
    /// but a machine that ships is the machine, and one edited in its own project has to reach
    /// the rack without anybody copying folders about by hand. That is <see cref="Refresh"/>.
    ///
    /// The offer is recorded whether or not the copy went in. A machine that cannot be copied is
    /// a machine this installation has still been offered, and trying again on every start would
    /// only write the same fault into the log for ever.
    /// </remarks>
    private static void Seed()
    {
        try
        {
            Directory.CreateDirectory(Installed);
        }
        catch (Exception ex)
        {
            Diagnostics.Log.Fault(Diagnostics.Enums.LogArea.Machines, "Machines folder could not be made at " + Installed, ex);

            return;
        }

        var offered = Offered();

        bool moved = false;

        var here = In(Installed).ToDictionary(one => one.Id, one => one.Folder, StringComparer.Ordinal);

        foreach (var project in In(Shipped))
        {
            if (project.Id.Length == 0) continue;

            if (offered.Contains(project.Id))
            {
                if (here.TryGetValue(project.Id, out string? mine)) Refresh(project.Folder, mine);

                continue;
            }

            offered.Add(project.Id);

            moved = true;

            if (MachineArchive.Add(project) != null) continue;

            Diagnostics.Log.Write(Diagnostics.Enums.LogArea.Machines,
                () => "machine " + project.Id + " could not be taken from " + project.Folder);
        }

        if (moved) Remember(offered);
    }

    /// <summary>
    /// Brings an installed machine up to date with the one that ships, where that is newer.
    /// </summary>
    /// <remarks>
    /// File by file, and nothing is deleted. What ships is overwritten because that is the
    /// machine; anything else in the folder is yours, which is how a preset you saved onto a
    /// machine survives the next version of it arriving.
    ///
    /// By the clock on each file rather than by the version in the manifest, because a version
    /// is bumped when somebody remembers and a machine being worked on changes twenty times
    /// between two of them. A file nobody has touched is copied over nothing.
    /// </remarks>
    /// <param name="shipped">The machine's folder beside the program.</param>
    /// <param name="installed">And this installation's copy of it.</param>
    private static void Refresh(string shipped, string installed)
    {
        try
        {
            if (!Directory.Exists(shipped) || !Directory.Exists(installed)) return;

            foreach (string from in Directory.EnumerateFiles(shipped, "*", SearchOption.AllDirectories))
            {
                string named = Path.GetRelativePath(shipped, from);
                string to = Path.Combine(installed, named);

                if (File.Exists(to) && File.GetLastWriteTimeUtc(to) >= File.GetLastWriteTimeUtc(from)) continue;

                if (Path.GetDirectoryName(to) is { Length: > 0 } folder) Directory.CreateDirectory(folder);

                File.Copy(from, to, overwrite: true);

                Diagnostics.Log.Write(Diagnostics.Enums.LogArea.Machines,
                    () => "machine " + named + " brought up to date in " + installed);
            }
        }
        catch (Exception ex)
        {
            Diagnostics.Log.Fault(
                Diagnostics.Enums.LogArea.Machines, "A machine could not be brought up to date from " + shipped, ex);
        }
    }

    /// <summary>
    /// Which shipped machines this installation has already been offered.
    /// </summary>
    /// <remarks>
    /// No file means this installation is either brand new or older than the file. Whatever it
    /// holds now is what it counts as having been offered: nothing at all in the first case,
    /// which is what puts every shipped machine on a new rack.
    /// </remarks>
    private static HashSet<string> Offered()
    {
        string file = Path.Combine(Installed, OfferedName);

        try
        {
            if (File.Exists(file))
                return new HashSet<string>(File.ReadAllLines(file).Where(id => id.Length > 0), StringComparer.Ordinal);
        }
        catch (Exception ex)
        {
            Diagnostics.Log.Fault(Diagnostics.Enums.LogArea.Machines, "The machines already offered could not be read", ex);
        }

        return new HashSet<string>(In(Installed).Select(project => project.Id), StringComparer.Ordinal);
    }

    /// <summary>Writes the offer down, so the next start does not make it again.</summary>
    /// <remarks>
    /// A write that fails is logged and let go. The worst that comes of it is every shipped
    /// machine being offered once more on the next start, which is a machine coming back rather
    /// than one going missing.
    /// </remarks>
    /// <param name="offered">Every machine id this installation has now been offered.</param>
    private static void Remember(IEnumerable<string> offered)
    {
        try
        {
            File.WriteAllLines(Path.Combine(Installed, OfferedName), offered);
        }
        catch (Exception ex)
        {
            Diagnostics.Log.Fault(Diagnostics.Enums.LogArea.Machines, "The machines already offered could not be written", ex);
        }
    }
}
