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
/// Two places are looked in, in this order: beside the program, which is where the installer
/// puts the ones that ship, and in the folder the app keeps its own things in, which is where a
/// machine you install later goes. The second wins, so a machine can be replaced by a newer one
/// without touching the installation.
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

    /// <summary>And where one installed afterwards goes.</summary>
    public static string Installed =>
        Path.Combine(Config.AppFolder.Path(), FolderName);

    /// <summary>
    /// Reads every machine there is and takes it into the list the app works from.
    /// </summary>
    /// <returns>What was taken, for the log and for the settings page to show.</returns>
    public static IReadOnlyList<MachineProject> Load()
    {
        var taken = new List<MachineProject>();

        foreach (string folder in new[] { Shipped, Installed })
        {
            foreach (var project in In(folder))
            {
                if (!Machine.Register(project.Id, project.Name, project.Summary, project.Theme)) continue;

                taken.Add(project);

                Diagnostics.Log.Write(Diagnostics.LogArea.App,
                    () => "machine " + project.Id + " from " + project.Folder);
            }
        }

        return taken;
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
}
