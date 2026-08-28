using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;

namespace JingleBox2.Tracker.Machines;

/// <summary>
/// A machine as it travels: one zip of the project folder, and the same folder again on
/// somebody else's disc.
/// </summary>
/// <remarks>
/// A machine is already a folder with a manifest at the top of it, so there is nothing to
/// invent here. The zip is that folder, and installing is putting it under
/// <see cref="MachineRegistry.Installed"/> in a folder named after the machine's id, which is
/// the name songs write down and therefore the only name that cannot collide by accident.
///
/// Two ways in and one door. A zip somebody was handed is unpacked; a machine the program ships
/// with is copied off the shelf beside the program. What arrives is different, where it lands is
/// not, so both go through <see cref="Install"/> and get the same checking and the same swap.
///
/// Everything a bundle says about where its contents go is a claim made by whoever built it, so
/// none of it is believed: the id has to name a folder and not a path, and a file has to land
/// inside the folder it is being written into. The rest of the app reads what is on the disc
/// through <see cref="MachineProject.Open"/>, and this is the one place a stranger's file gets
/// to put anything there.
///
/// Everything here writes to <see cref="Diagnostics.LogArea.Machines"/> rather than to the
/// application's own area, as everything under this folder does. What a bundle was refused for
/// is the sort of thing somebody goes looking for, and it should not be buried under everything
/// else the application had to say that session.
/// </remarks>
public static class MachineArchive
{
    /// <summary>What a half-finished install is called while it is being written.</summary>
    /// <remarks>
    /// Beside the machine rather than in the temp folder, so the swap is a rename within one
    /// folder and cannot fail half way across a volume boundary.
    /// </remarks>
    private const string IncomingSuffix = ".incoming";

    /// <summary>And what the install being replaced is called for the moment it takes.</summary>
    private const string OutgoingSuffix = ".outgoing";

    /// <summary>Zips the project folder, manifest and sounds and all, into that file.</summary>
    /// <remarks>
    /// Throws rather than reporting: this is asked for by somebody who has just pressed Export
    /// and is waiting to be told either where the file went or what stopped it.
    ///
    /// An existing file is overwritten, which is the ordinary case: exporting twice in a row is
    /// how a machine gets corrected, and being made to delete the old file first would only be
    /// in the way.
    /// </remarks>
    /// <param name="project">The machine to pack, which has to have been saved.</param>
    /// <param name="zipPath">Where the zip goes, folders made as needed.</param>
    public static void Export(MachineProject project, string zipPath)
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

    /// <summary>
    /// Unpacks a machine out of that zip and into the installed machines.
    /// </summary>
    /// <returns>The machine as it now sits on the disc, or null when the zip held none.</returns>
    /// <remarks>
    /// Both shapes of zip are read: the folder's contents at the top, which is what
    /// <see cref="Export"/> writes, and the folder itself at the top, which is what somebody
    /// gets who right-clicks the folder and zips that. Refusing the second would only teach
    /// people that the importer is broken.
    ///
    /// Reported rather than thrown, unlike <see cref="Export"/>: a zip somebody was handed can
    /// be anything at all, and every way it can be wrong ends the same way, with nothing
    /// installed and a line in the log.
    /// </remarks>
    /// <param name="zipPath">The zip somebody was handed.</param>
    public static MachineProject? Import(string zipPath)
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
            Diagnostics.Log.Fault(Diagnostics.LogArea.Machines, "Machine could not be imported from " + zipPath, ex);

            return null;
        }
    }

    /// <summary>
    /// Takes a machine the program ships with and puts a copy of it among the installed ones.
    /// </summary>
    /// <returns>The machine as it now sits in the installed folder, or null when it could not go.</returns>
    /// <remarks>
    /// The folder beside the program is a shelf to take from and is never written to, so this is
    /// a copy in one direction and the shipped machine is left exactly as it was. That is what
    /// makes removing a machine reversible: the copy goes, the original is still on the shelf.
    ///
    /// It ends where <see cref="Import"/> ends, by the same route, because a machine arriving
    /// from a zip and a machine arriving from the shelf are the same event once the files are in
    /// hand. Both are checked the same way, both land through the same swap, and both are read
    /// back off the disc rather than believed.
    ///
    /// Copying the installed folder onto itself is refused. That is not adding a machine, and
    /// the swap that finishes an install would be moving a folder out from under its own source.
    /// </remarks>
    /// <param name="fromCrate">The machine on the shelf beside the program.</param>
    public static MachineProject? Add(MachineProject fromCrate)
    {
        try
        {
            if (!fromCrate.IsSaved || !Directory.Exists(fromCrate.Folder)) return null;

            string source = Path.GetFullPath(fromCrate.Folder);

            if (Under(source, MachineRegistry.Installed)) return null;

            string id = Named(fromCrate.Id);
            if (id.Length == 0) return null;

            return Install(id, staging => Copy(source, staging));
        }
        catch (Exception ex)
        {
            Diagnostics.Log.Fault(Diagnostics.LogArea.Machines, "Machine could not be taken from " + fromCrate.Folder, ex);

            return null;
        }
    }

    /// <summary>Deletes an installed machine's folder.</summary>
    /// <remarks>
    /// Only one that is installed. The shelf beside the program is what the application ships
    /// and is never written to, which is exactly what lets this delete freely: a machine that
    /// ships can be taken again with <see cref="Add"/> the moment it is gone.
    /// </remarks>
    /// <param name="project">The machine to delete, which has to be an installed one.</param>
    public static bool Remove(MachineProject project)
    {
        try
        {
            if (!project.IsSaved) return false;

            string folder = Path.GetFullPath(project.Folder);

            if (!Under(folder, MachineRegistry.Installed)) return false;

            if (!Directory.Exists(folder)) return false;

            Directory.Delete(folder, recursive: true);

            Diagnostics.Log.Write(Diagnostics.LogArea.Machines, () => "machine removed from " + folder);

            return true;
        }
        catch (Exception ex)
        {
            Diagnostics.Log.Fault(Diagnostics.LogArea.Machines, "Machine could not be removed from " + project.Folder, ex);

            return false;
        }
    }

    /// <summary>The manifest in that zip, at the top of it or one folder down, or null.</summary>
    /// <remarks>
    /// One folder down and no further. A machine is a flat folder with a manifest at the top of
    /// it, so anything deeper is not a machine's zip and searching for it would only find a
    /// manifest somebody had put inside their sounds.
    /// </remarks>
    private static ZipArchiveEntry? Manifest(ZipArchive zip) =>
        zip.Entries.FirstOrDefault(e => Slashed(e.FullName) == MachineProject.ManifestName)
        ?? zip.Entries.FirstOrDefault(e =>
        {
            string name = Slashed(e.FullName);

            int cut = name.IndexOf('/');

            return cut > 0 && name.IndexOf('/', cut + 1) < 0
                   && name[(cut + 1)..] == MachineProject.ManifestName;
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

        return JsonSerializer.Deserialize<MachineProject>(reading)?.Id ?? "";
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
    private static MachineProject? Install(string id, Func<string, bool> fill)
    {
        Directory.CreateDirectory(MachineRegistry.Installed);

        string target = Path.Combine(MachineRegistry.Installed, id);

        string staging = target + IncomingSuffix;

        try
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);

            Directory.CreateDirectory(staging);

            if (!fill(staging)) return null;

            var written = MachineProject.Open(staging);

            if (written == null || written.Id != id) return null;

            Swap(staging, target);

            staging = "";

            var installed = MachineProject.Open(target);

            if (installed != null)
            {
                Diagnostics.Log.Write(Diagnostics.LogArea.Machines,
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
    private static bool Copy(string from, string into)
    {
        foreach (string file in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
        {
            string full = Path.GetFullPath(Path.Combine(into, Path.GetRelativePath(from, file)));

            if (!MachinePaths.Under(full, into)) return false;

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
    private static bool Unpack(ZipArchive zip, string prefix, string into)
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

            if (!MachinePaths.Under(full, into))
            {
                Diagnostics.Log.Write(Diagnostics.LogArea.Machines,
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
            Diagnostics.Log.Fault(Diagnostics.LogArea.Machines, "Half an imported machine is left in " + staging, ex);
        }
    }

    /// <summary>Zip entries are written with forward slashes, whatever wrote them.</summary>
    private static string Slashed(string name) => name.Replace('\\', '/');

    /// <summary>Whether that path is inside that folder, rather than beside it or above it.</summary>
    private static bool Under(string path, string folder) => MachinePaths.Under(path, folder);
}
