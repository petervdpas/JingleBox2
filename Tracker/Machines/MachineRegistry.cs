using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JingleBox2.Tracker.Records;
using JingleBox2.Files;
using JingleBox2.Files.Interfaces;
using JingleBox2.Tracker.Machines.Interfaces;

namespace JingleBox2.Tracker.Machines;

/// <inheritdoc/>
/// <remarks>
/// The two folders are worked out on each reading rather than kept, since the app folder is
/// pointed elsewhere by a test and by the portable build and neither of those asks anybody's
/// permission.
/// </remarks>
public sealed class MachineRegistry : IMachineRegistry
{
    /// <summary>How two paths are compared, which is a fact about the disc and not about here.</summary>
    private readonly IFilePaths _paths;

    /// <summary>Who puts a machine's files where the machine goes.</summary>
    private readonly IMachineArchive _archive;

    /// <summary>
    /// Takes the two things this needs, or makes the ordinary ones.
    /// </summary>
    /// <remarks>
    /// The registry and the archive each need the other: an archive installs into the folder the
    /// registry names, and the registry hands the archive every shipped machine it has not yet
    /// offered. Made without one, each builds the other and hands itself over, so the pair is
    /// built once and there is no third instance to go looking for.
    /// </remarks>
    /// <param name="archive">
    /// Who unpacks and copies machines into the installed folder. Left out, the ordinary one,
    /// pointed back at this registry.
    /// </param>
    /// <param name="paths">
    /// How this system decides two paths are the same. Left out, the rule this system really
    /// has; given, whatever a test wants to hold it to.
    /// </param>
    /// <param name="folder">Where the application keeps its things, defaulted to the real one.</param>
    public MachineRegistry(IMachineArchive? archive = null, IFilePaths? paths = null, IAppFolder? folder = null)
    {
        _paths = paths ?? new FilePaths();
        _folder = folder ?? new AppFolder();
        _archive = archive ?? new MachineArchive(this, new MachinePaths(_paths));
    }

    /// <summary>Where the application keeps its things, which the installed folder sits under.</summary>
    private readonly IAppFolder _folder;

    /// <summary>What the folder holding the machines is called, in both places it appears.</summary>
    /// <remarks>
    /// Written out rather than built, so the one folder name this depends on can be found by
    /// looking for it. Kept as a constant as well as answered as a property, since it is a fact
    /// about the layout on disc and callers that have never held a registry still name it.
    /// </remarks>
    public const string FolderName = "machines";

    /// <inheritdoc/>
    string IMachineRegistry.FolderName => FolderName;

    /// <inheritdoc/>
    public string Shipped =>
        Path.Combine(AppContext.BaseDirectory, FolderName);

    /// <inheritdoc/>
    public string Installed =>
        Path.Combine(_folder.Path(), FolderName);

    /// <inheritdoc/>
    public bool Ships(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return false;

        try
        {
            string installed = Path.GetFullPath(Installed);
            string full = Path.GetFullPath(path);

            if (!full.StartsWith(installed + Path.DirectorySeparatorChar, _paths.Comparison))
                return false;

            return File.Exists(Path.Combine(Shipped, full.Substring(installed.Length + 1)));
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<MachineProject> Load()
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

    /// <inheritdoc/>
    public IReadOnlyList<MachineProject> Available()
    {
        var here = new HashSet<string>(In(Installed).Select(p => p.Id), StringComparer.Ordinal);

        return In(Shipped).Where(p => !here.Contains(p.Id)).ToList();
    }

    /// <inheritdoc/>
    public IReadOnlyList<MachineProject> In(string folder)
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
    private void Seed()
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

            if (_archive.Add(project) != null) continue;

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
    private HashSet<string> Offered()
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
    private void Remember(IEnumerable<string> offered)
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
