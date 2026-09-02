using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Files;
using JingleBox2.Files.Interfaces;
using JingleBox2.Tracker.Interfaces;

namespace JingleBox2.Tracker;

/// <inheritdoc/>
/// <remarks>
/// The two folders are worked out on each reading rather than kept, since the app folder is
/// pointed elsewhere by a test and by the portable build and neither of those asks anybody's
/// permission.
///
/// What a subclass supplies is the four things that differ between one kind of box and another:
/// how a folder is read into a manifest, whether this build will have what it read, how a
/// shipped one is taken, and the word the log calls it by. Everything else here is folders.
/// </remarks>
/// <typeparam name="T">The manifest a box of this kind is read into.</typeparam>
public abstract class RackRegistry<T> : IRackRegistry<T> where T : class, IRackProject
{
    /// <summary>How two paths are compared, which is a fact about the disc and not about here.</summary>
    private readonly IFilePaths _paths;

    /// <summary>Where the application keeps its things, which the installed folder sits under.</summary>
    private readonly IAppFolder _folder;

    /// <summary>What the log calls one of these, in the singular.</summary>
    private readonly string _word;

    /// <summary>How a folder is carried whole, which is what taking a shipped one is.</summary>
    private readonly IFolderCopy _copy = new FolderCopy();

    /// <summary>
    /// Takes what every registry needs, whatever is being kept.
    /// </summary>
    /// <param name="folderName">
    /// What the folder holding these is called, in both places it appears. Handed in rather than
    /// asked for through a property, so the one name on disc is settled where the registry is
    /// made and cannot differ between the two folders.
    /// </param>
    /// <param name="word">What the log calls one of these, so a line reads as a sentence.</param>
    /// <param name="paths">
    /// How this system decides two paths are the same. Left out, the rule this system really
    /// has; given, whatever a test wants to hold it to.
    /// </param>
    /// <param name="folder">Where the application keeps its things, defaulted to the real one.</param>
    /// <param name="shipped">
    /// Where the ones that ship live, defaulted to the folder beside the program, which is the
    /// only answer outside a test. Handed in so the two folder rules can be put a question to at
    /// all: what a shipped folder does on a first run, what happens to one already offered, and
    /// what is brought up to date against what are the parts nobody can see going wrong, since
    /// they only show on somebody else's installation.
    /// </param>
    protected RackRegistry(
        string folderName,
        string word,
        IFilePaths? paths = null,
        IAppFolder? folder = null,
        string? shipped = null)
    {
        FolderName = folderName;
        _word = word;
        _paths = paths ?? new FilePaths();
        _folder = folder ?? new AppFolder();
        _shipped = shipped;
    }

    /// <summary>Where the shipped ones were said to be, or nothing for the ordinary answer.</summary>
    private readonly string? _shipped;

    /// <summary>How this system decides two paths are the same, for a subclass that needs it.</summary>
    protected IFilePaths Paths => _paths;

    /// <inheritdoc/>
    public string FolderName { get; }

    /// <inheritdoc/>
    public string Shipped => _shipped ?? Path.Combine(AppContext.BaseDirectory, FolderName);

    /// <inheritdoc/>
    public string Installed => Path.Combine(_folder.Path(), FolderName);

    /// <summary>Reads one folder into a manifest, or nothing when there is no box in it.</summary>
    /// <param name="folder">The folder to read.</param>
    protected abstract T? Open(string folder);

    /// <summary>
    /// Whether this build will have what was read, and takes it if so.
    /// </summary>
    /// <remarks>
    /// The engine gate. An id this build has nothing behind is refused here, which is what keeps
    /// a folder from a later version off the rack rather than on it as a box that cannot sound.
    /// </remarks>
    /// <param name="project">What was read off the disc.</param>
    protected abstract bool Register(T project);

    /// <summary>Forgets everything taken last time, before a list is read again.</summary>
    /// <remarks>
    /// Nothing by default, for a world that keeps its list nowhere but in what
    /// <see cref="Load"/> hands back.
    /// </remarks>
    protected virtual void Forget() { }

    /// <summary>
    /// Puts a shipped box into the installed folder, under a folder named for its id.
    /// </summary>
    /// <remarks>
    /// A plain copy, which is all a box whose folder is only files needs. A world that has more
    /// to do about it than copy, because it also arrives as a zip and has to be named around a
    /// folder that is already there, overrides this.
    /// </remarks>
    /// <param name="project">The shipped box being taken.</param>
    /// <returns>False when nothing was taken, which is written to the log by the caller.</returns>
    protected virtual bool Take(T project)
    {
        try
        {
            if (project.Folder.Length == 0 || !Directory.Exists(project.Folder)) return false;

            string into = Path.Combine(Installed, Path.GetFileName(project.Folder.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));

            _copy.Into(project.Folder, into);

            return true;
        }
        catch (Exception ex)
        {
            Log.Fault(LogArea.Machines, "A " + _word + " could not be taken from " + project.Folder, ex);

            return false;
        }
    }

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
    public IReadOnlyList<T> Load()
    {
        Seed();

        Forget();

        var taken = new List<T>();

        foreach (var project in In(Installed))
        {
            if (!Register(project)) continue;

            taken.Add(project);

            Log.Write(LogArea.Machines, () => _word + " " + project.Id + " from " + project.Folder);
        }

        return taken;
    }

    /// <inheritdoc/>
    public IReadOnlyList<T> Available()
    {
        var here = new HashSet<string>(In(Installed).Select(p => p.Id), StringComparer.Ordinal);

        return In(Shipped).Where(p => !here.Contains(p.Id)).ToList();
    }

    /// <inheritdoc/>
    public IReadOnlyList<T> In(string folder)
    {
        if (!Directory.Exists(folder)) return Array.Empty<T>();

        try
        {
            return Directory.GetDirectories(folder)
                .Select(Open)
                .Where(p => p != null && p.Id.Length > 0)
                .Select(p => p!)
                .ToList();
        }
        catch (Exception ex)
        {
            Log.Fault(LogArea.Machines, "The " + _word + " folders could not be read from " + folder, ex);

            return Array.Empty<T>();
        }
    }

    /// <summary>What the file recording the offer is called.</summary>
    /// <remarks>
    /// Written out rather than built from the folder's name, so the one file this depends on can
    /// be found by looking for it.
    /// </remarks>
    private const string OfferedName = "offered.txt";

    /// <summary>
    /// Gives the installation anything the program ships that it has never been offered.
    /// </summary>
    /// <remarks>
    /// It used to be the absence of the folder that decided, which was right while the set of
    /// machines never changed and wrong the moment one was added: a machine written after the
    /// folder was made could never arrive, because the folder was there. Every new machine then
    /// cost a trip to SETTINGS before it could be seen at all, and the panel it draws stayed
    /// hidden behind the hand written one with nothing saying why.
    ///
    /// So the offer is what is recorded, not the folder. A shipped box this installation has
    /// never been offered is put on the rack; one it has been offered is left alone whether or
    /// not it is still there, which is what keeps something somebody threw out thrown out.
    ///
    /// An installation from before this file existed is taken to have been offered whatever it
    /// currently holds. That is right for everything anybody kept and wrong once for anything
    /// they had already removed: it comes back a single time, and stays gone after that.
    ///
    /// Something already offered is not frozen: it is this installation's to keep or throw out,
    /// but what ships is the thing itself, and one edited in its own project has to reach the
    /// rack without anybody copying folders about by hand. That is <see cref="Refresh"/>.
    ///
    /// The offer is recorded whether or not the copy went in. Something that cannot be copied is
    /// something this installation has still been offered, and trying again on every start would
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
            Log.Fault(LogArea.Machines, "The " + _word + " folder could not be made at " + Installed, ex);

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

            if (Take(project)) continue;

            Log.Write(LogArea.Machines, () => _word + " " + project.Id + " could not be taken from " + project.Folder);
        }

        if (moved) Remember(offered);
    }

    /// <summary>
    /// Brings an installed box up to date with the one that ships, where that is newer.
    /// </summary>
    /// <remarks>
    /// File by file, and nothing is deleted. What ships is overwritten because that is the thing;
    /// anything else in the folder is yours, which is how a preset you saved onto a machine
    /// survives the next version of it arriving.
    ///
    /// By the clock on each file rather than by the version in the manifest, because a version is
    /// bumped when somebody remembers and something being worked on changes twenty times between
    /// two of them. A file nobody has touched is copied over nothing.
    /// </remarks>
    /// <param name="shipped">The folder beside the program.</param>
    /// <param name="installed">And this installation's copy of it.</param>
    private void Refresh(string shipped, string installed)
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

                Log.Write(LogArea.Machines, () => _word + " file " + named + " brought up to date in " + installed);
            }
        }
        catch (Exception ex)
        {
            Log.Fault(LogArea.Machines, "A " + _word + " could not be brought up to date from " + shipped, ex);
        }
    }

    /// <summary>
    /// Which shipped boxes this installation has already been offered.
    /// </summary>
    /// <remarks>
    /// No file means this installation is either brand new or older than the file. Whatever it
    /// holds now is what it counts as having been offered: nothing at all in the first case,
    /// which is what puts everything shipped on a new rack.
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
            Log.Fault(LogArea.Machines, "The " + _word + " offers already made could not be read", ex);
        }

        return new HashSet<string>(In(Installed).Select(project => project.Id), StringComparer.Ordinal);
    }

    /// <summary>Writes the offer down, so the next start does not make it again.</summary>
    /// <remarks>
    /// A write that fails is logged and let go. The worst that comes of it is everything shipped
    /// being offered once more on the next start, which is a box coming back rather than one
    /// going missing.
    /// </remarks>
    /// <param name="offered">Every id this installation has now been offered.</param>
    private void Remember(IEnumerable<string> offered)
    {
        try
        {
            File.WriteAllLines(Path.Combine(Installed, OfferedName), offered);
        }
        catch (Exception ex)
        {
            Log.Fault(LogArea.Machines, "The " + _word + " offers already made could not be written", ex);
        }
    }
}
