using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using JingleBox2.Diagnostics;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Devices.SoundMachines;
using JingleBox2.Devices.SoundMachines.Interfaces;
using JingleBox2.Devices.Interfaces;

namespace JingleBox2.Devices;

/// <inheritdoc/>
/// <remarks>
/// What a subclass supplies is the two things that differ between one kind of box and another:
/// what the file at the top of its folder is called, and how a folder is read into a manifest.
/// Everything else here is zips, staging folders and the swap at the end, none of which knows
/// what is in the folder it is carrying.
/// </remarks>
/// <typeparam name="T">The manifest a box of this kind is read into.</typeparam>
public abstract class RackArchive<T> : IRackArchive<T> where T : class, IRackProject
{
    /// <summary>Who names the folder the installed ones live in.</summary>
    private readonly IRackRegistry<T> _registry;

    /// <summary>The two questions asked of every path written into a staging folder.</summary>
    private readonly ISoundMachinePaths _paths;

    /// <summary>
    /// Takes the two things this needs, or makes the ordinary ones.
    /// </summary>
    /// <remarks>
    /// The registry and the archive each need the other, so one made without a registry builds
    /// one and hands itself over, which is what stops the two defaults building each other for
    /// ever. Anything wiring these up on purpose makes the registry and lets it make the archive.
    /// </remarks>
    /// <param name="registry">
    /// Who names the installed folder. Left out, the ordinary one, pointed back at this archive.
    /// </param>
    /// <param name="paths">
    /// How a path is tested for being inside a folder. Left out, the ordinary one, which reads
    /// the rule off this system.
    /// </param>
    protected RackArchive(IRackRegistry<T> registry, ISoundMachinePaths? paths = null)
    {
        _paths = paths ?? new SoundMachinePaths();
        _registry = registry;
    }

    /// <summary>What the file at the top of one of these folders is called.</summary>
    protected abstract string ManifestName { get; }

    /// <summary>Reads one folder into a manifest, or nothing when there is no box in it.</summary>
    /// <param name="folder">The folder to read.</param>
    protected abstract T? Open(string folder);

    /// <summary>What a half-finished install is called while it is being written.</summary>
    /// <remarks>
    /// Beside the machine rather than in the temp folder, so the swap is a rename within one
    /// folder and cannot fail half way across a volume boundary.
    /// </remarks>
    private const string IncomingSuffix = ".incoming";

    /// <summary>And what the install being replaced is called for the moment it takes.</summary>
    private const string OutgoingSuffix = ".outgoing";

    /// <inheritdoc/>
    public void Export(T project, string zipPath)
    {
        if (string.IsNullOrWhiteSpace(zipPath)) throw new ArgumentException("A zip needs a name.", nameof(zipPath));

        if (!project.IsSaved || !Directory.Exists(project.Folder))
            throw new InvalidOperationException("A machine has to be saved before it can be exported.");

        string full = Path.GetFullPath(zipPath);

        string? holds = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(holds)) Directory.CreateDirectory(holds);

        if (File.Exists(full)) File.Delete(full);

        ZipFile.CreateFromDirectory(project.Folder, full, CompressionLevel.Optimal, includeBaseDirectory: false);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A folder copied onto itself is nothing to do, rather than an error: somebody who picks
    /// the folder the machine is already in has asked for a save, and that is what they get.
    /// </remarks>
    public void CopyInto(T project, string folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) throw new ArgumentException("A machine needs a folder.", nameof(folder));

        if (!project.IsSaved || !Directory.Exists(project.Folder))
            throw new InvalidOperationException("A machine has to be saved before it can be copied.");

        string from = Path.GetFullPath(project.Folder);
        string into = Path.GetFullPath(folder);

        if (Same(from, into)) return;

        Directory.CreateDirectory(into);

        if (!Copy(from, into, ManifestName))
            throw new InvalidOperationException("A file in " + from + " points outside the machine.");

        Log.Write(
            LogArea.Machines,
            () => "machine " + project.Id + " copied from " + from + " to " + into);
    }

    /// <summary>Whether two paths name one folder, by this machine's rules about case.</summary>
    private bool Same(string one, string other) =>
        string.Equals(
            one.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            other.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    /// <inheritdoc/>
    public T? Import(string zipPath)
    {
        if (string.IsNullOrWhiteSpace(zipPath) || !File.Exists(zipPath)) return null;

        try
        {
            using var zip = ZipFile.OpenRead(zipPath);

            var manifest = Manifest(zip);
            if (manifest == null) return null;

            string prefix = Ahead(manifest.FullName);

            string id = Named(Announced(manifest));
            if (id.Length == 0) return null;

            return Install(id, staging => Unpack(zip, prefix, staging));
        }
        catch (Exception ex)
        {
            Log.Fault(LogArea.Machines, "Machine could not be imported from " + zipPath, ex);

            return null;
        }
    }

    /// <inheritdoc/>
    public T? Add(T fromCrate)
    {
        try
        {
            if (!fromCrate.IsSaved || !Directory.Exists(fromCrate.Folder)) return null;

            string source = Path.GetFullPath(fromCrate.Folder);

            if (Under(source, _registry.Installed)) return null;

            string id = Named(fromCrate.Id);
            if (id.Length == 0) return null;

            return Install(id, staging => Copy(source, staging));
        }
        catch (Exception ex)
        {
            Log.Fault(LogArea.Machines, "Machine could not be taken from " + fromCrate.Folder, ex);

            return null;
        }
    }

    /// <inheritdoc/>
    public bool Remove(T project)
    {
        try
        {
            if (!project.IsSaved) return false;

            string folder = Path.GetFullPath(project.Folder);

            if (!Under(folder, _registry.Installed)) return false;

            if (!Directory.Exists(folder)) return false;

            Directory.Delete(folder, recursive: true);

            Log.Write(LogArea.Machines, () => "machine removed from " + folder);

            return true;
        }
        catch (Exception ex)
        {
            Log.Fault(LogArea.Machines, "Machine could not be removed from " + project.Folder, ex);

            return false;
        }
    }

    /// <summary>The manifest in that zip, at the top of it or one folder down, or null.</summary>
    /// <remarks>
    /// One folder down and no further. A machine is a flat folder with a manifest at the top of
    /// it, so anything deeper is not a machine's zip and searching for it would only find a
    /// manifest somebody had put inside their sounds.
    /// </remarks>
    private ZipArchiveEntry? Manifest(ZipArchive zip) =>
        zip.Entries.FirstOrDefault(e => Slashed(e.FullName) == ManifestName)
        ?? zip.Entries.FirstOrDefault(e =>
        {
            string name = Slashed(e.FullName);

            int cut = name.IndexOf('/');

            return cut > 0 && name.IndexOf('/', cut + 1) < 0
                   && name[(cut + 1)..] == ManifestName;
        });

    /// <summary>What the entries are all kept under, or nothing when they are at the top.</summary>
    private static string Ahead(string manifest)
    {
        string name = Slashed(manifest);

        int cut = name.IndexOf('/');

        return cut < 0 ? "" : name[..(cut + 1)];
    }

    /// <summary>What id the manifest in that zip claims, before anybody believes it.</summary>
    /// <remarks>
    /// Read straight out of the entry rather than from a file, so nothing is written anywhere
    /// until the id has been through <see cref="Named"/>.
    /// </remarks>
    private static string Announced(ZipArchiveEntry manifest)
    {
        using var reading = manifest.Open();

        return JsonSerializer.Deserialize<T>(reading)?.Id ?? "";
    }

    /// <summary>That id, when it is a name and not a path, and nothing when it is not.</summary>
    /// <remarks>
    /// The id decides which folder is written to, so an id of "../../something" is a machine
    /// choosing a folder on this disc. It names a machine or it names nothing. A zip is the
    /// obvious place for that to be done on purpose, but a shipped machine goes through it too:
    /// the check costs nothing and it is the only guard the write has.
    /// </remarks>
    private static string Named(string? announced)
    {
        string id = announced?.Trim() ?? "";

        if (id.Length == 0) return "";

        if (id.Contains('/') || id.Contains('\\') || id.Contains("..")) return "";

        if (id != Path.GetFileName(id)) return "";

        if (id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return "";

        return id;
    }

    /// <summary>
    /// Puts a machine's files where that machine goes, whatever they were carried in.
    /// </summary>
    /// <remarks>
    /// The one door into the installed folder. Everything an arriving machine says about itself
    /// has already been reduced to an id that is a plain folder name; from here the path is
    /// fixed and the caller only gets to fill a staging folder. What it filled it with is read
    /// back and has to be the machine that was announced, or the files are swept away: a bundle
    /// that installs one machine under another's name is a bundle built to do that.
    ///
    /// Any staging folder already there is cleared first. A crash part way through an earlier
    /// install leaves one behind, and what is in it is half of somebody else's machine.
    /// </remarks>
    /// <param name="id">The machine's id, already known to be a plain folder name.</param>
    /// <param name="fill">
    /// Fills the staging folder and says whether it could. Where the files come from is the only
    /// difference between a zip and the shelf beside the program.
    /// </param>
    private T? Install(string id, Func<string, bool> fill)
    {
        Directory.CreateDirectory(_registry.Installed);

        string target = Path.Combine(_registry.Installed, id);

        string staging = target + IncomingSuffix;

        try
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);

            Directory.CreateDirectory(staging);

            if (!fill(staging)) return null;

            var written = Open(staging);

            if (written == null || written.Id != id) return null;

            Swap(staging, target);

            staging = "";

            var installed = Open(target);

            if (installed != null)
            {
                Log.Write(LogArea.Machines,
                    () => "machine " + installed.Id + " installed into " + installed.Folder);
            }

            return installed;
        }
        finally
        {
            Sweep(staging);
        }
    }

    /// <summary>Copies a machine's folder, sounds and all, into the folder being staged.</summary>
    /// <remarks>
    /// Every destination is checked to be inside the staging folder, the same as an unpacked
    /// zip's is. A link inside the source folder pointing out of it would otherwise copy a file
    /// from somewhere else in under the machine's name.
    /// </remarks>
    private bool Copy(string from, string into, string? except = null)
    {
        foreach (string folder in Directory.GetDirectories(from, "*", SearchOption.AllDirectories))
        {
            string full = Path.GetFullPath(Path.Combine(into, Path.GetRelativePath(from, folder)));

            if (!Under(full, into)) return false;

            Directory.CreateDirectory(full);
        }

        foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            string named = Path.GetRelativePath(from, file);

            if (except != null && string.Equals(named, except, StringComparison.OrdinalIgnoreCase)) continue;

            string full = Path.GetFullPath(Path.Combine(into, named));

            if (!Under(full, into)) return false;

            string? holds = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(holds)) Directory.CreateDirectory(holds);

            File.Copy(file, full, overwrite: true);
        }

        return true;
    }

    /// <summary>Writes every entry under that prefix into the folder, and says whether it could.</summary>
    /// <remarks>
    /// One entry landing outside the folder stops the whole unpack rather than being skipped:
    /// a zip carrying such an entry was built to do that, and the half of it that is honest is
    /// not worth installing.
    ///
    /// An entry naming a folder is passed over. A zip may or may not have bothered to record
    /// them, and the files themselves make the folders they need.
    /// </remarks>
    private bool Unpack(ZipArchive zip, string prefix, string into)
    {
        foreach (var entry in zip.Entries)
        {
            string name = Slashed(entry.FullName);

            if (prefix.Length > 0)
            {
                if (!name.StartsWith(prefix, StringComparison.Ordinal)) continue;

                name = name[prefix.Length..];
            }

            if (name.Length == 0) continue;

            if (name.EndsWith('/')) continue;

            string full = Path.GetFullPath(Path.Combine(into, name));

            if (!Under(full, into))
            {
                Log.Write(LogArea.Machines,
                    () => "machine zip refused: " + entry.FullName + " lands outside " + into);

                return false;
            }

            string? holds = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(holds)) Directory.CreateDirectory(holds);

            entry.ExtractToFile(full, overwrite: true);
        }

        return true;
    }

    /// <summary>Puts what was just unpacked where the machine goes, keeping the old one until it is.</summary>
    /// <remarks>
    /// Installing over a machine that is already there is how a machine is updated, so the
    /// question is only what happens when the second half of that goes wrong. The old folder is
    /// moved aside rather than deleted, and goes back if the new one cannot be put in its place.
    /// </remarks>
    /// <param name="staging">The folder that was just filled.</param>
    /// <param name="target">Where the machine goes, which may already hold an older one.</param>
    private static void Swap(string staging, string target)
    {
        string aside = target + OutgoingSuffix;

        if (Directory.Exists(aside)) Directory.Delete(aside, recursive: true);

        bool replacing = Directory.Exists(target);

        if (replacing) Directory.Move(target, aside);

        try
        {
            Directory.Move(staging, target);
        }
        catch
        {
            if (replacing) Directory.Move(aside, target);

            throw;
        }

        if (replacing) Directory.Delete(aside, recursive: true);
    }

    /// <summary>Clears away a staging folder an import gave up on.</summary>
    /// <remarks>
    /// The empty string means there is nothing to sweep, which is what
    /// <see cref="Install"/> sets once the swap has taken the folder. A failure here is logged
    /// and let go: half a machine left on the disc is untidy, and throwing out of a finally
    /// block would hide whatever really went wrong.
    /// </remarks>
    private static void Sweep(string staging)
    {
        if (staging.Length == 0 || !Directory.Exists(staging)) return;

        try
        {
            Directory.Delete(staging, recursive: true);
        }
        catch (Exception ex)
        {
            Log.Fault(LogArea.Machines, "Half an imported machine is left in " + staging, ex);
        }
    }

    /// <summary>Zip entries are written with forward slashes, whatever wrote them.</summary>
    private static string Slashed(string name) => name.Replace('\\', '/');

    /// <summary>Whether that path is inside that folder, rather than beside it or above it.</summary>
    private bool Under(string path, string folder) => _paths.Under(path, folder);
}
