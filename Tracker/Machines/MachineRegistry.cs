using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
/// </remarks>
public static class MachineRegistry
{
    public const string FolderName = "machines";

    /// <summary>Where the machines that ship with the program live.</summary>
    public static string Shipped =>
        Path.Combine(AppContext.BaseDirectory, FolderName);

    /// <summary>And where the ones this installation has live.</summary>
    public static string Installed =>
        Path.Combine(Config.AppFolder.Path(), FolderName);

    /// <summary>
    /// Reads the machines this installation has and takes them into the list the app works from.
    /// </summary>
    /// <returns>What was taken, for the log and for the settings page to show.</returns>
    public static IReadOnlyList<MachineProject> Load()
    {
        Seed();

        var taken = new List<MachineProject>();

        foreach (var project in In(Installed))
        {
            if (!Machine.Register(project.Id, project.Name, project.Summary, project.Theme)) continue;

            taken.Add(project);

            Diagnostics.Log.Write(Diagnostics.LogArea.App,
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
            Diagnostics.Log.Fault(Diagnostics.LogArea.App, "Machines could not be read from " + folder, ex);

            return Array.Empty<MachineProject>();
        }
    }

    /// <summary>Gives a brand new installation the machines the program ships with.</summary>
    /// <remarks>
    /// The absence of the folder is the whole test, and it has to be, because there is no other
    /// mark on disc that says whether somebody has been here before. So the folder is made even
    /// when there is nothing to put in it: what matters afterwards is that this never runs a
    /// second time and never quietly puts back a machine that was thrown out on purpose.
    /// </remarks>
    private static void Seed()
    {
        if (Directory.Exists(Installed)) return;

        try
        {
            Directory.CreateDirectory(Installed);
        }
        catch (Exception ex)
        {
            Diagnostics.Log.Fault(Diagnostics.LogArea.App, "Machines folder could not be made at " + Installed, ex);

            return;
        }

        foreach (var project in In(Shipped))
        {
            if (MachineArchive.Add(project) != null) continue;

            Diagnostics.Log.Write(Diagnostics.LogArea.App,
                () => "machine " + project.Id + " could not be taken from " + project.Folder);
        }
    }
}
