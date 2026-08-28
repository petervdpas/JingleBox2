using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Tracker.Machines;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Tracker.Machines.Interfaces;

namespace JingleBox2.ViewModels;

/// <summary>
/// One machine on the list, either one this installation has or one it is offered.
/// </summary>
/// <remarks>
/// Read off the manifest and not kept in step with it: the list is rebuilt whenever anything is
/// added, removed or imported, which is the only way the folders change while the app is running.
/// </remarks>
public sealed class MachineShelfEntry
{
    /// <summary>One row over one machine, said to be installed or on offer.</summary>
    public MachineShelfEntry(MachineProject project, bool installed)
    {
        Project = project;
        IsInstalled = installed;
    }

    /// <summary>The machine itself, for the one operation that acts on it.</summary>
    public MachineProject Project { get; }

    /// <summary>What it is called, falling back to its id for one that has not been named.</summary>
    public string Name => Project.Name.Length > 0 ? Project.Name : Project.Id;

    /// <summary>Its own name in the folders and in a song, which never changes.</summary>
    public string Id => Project.Id;

    /// <summary>Which version of it this is.</summary>
    public string Version => Project.Version;

    /// <summary>Who made it.</summary>
    public string Author => Project.Author;

    /// <summary>The one line it says about itself.</summary>
    public string Summary => Project.Summary;

    /// <summary>Where it lives on the disc.</summary>
    public string Folder => Project.Folder;

    /// <summary>
    /// Whether this installation has the machine, as opposed to being offered it.
    /// </summary>
    /// <remarks>
    /// Which of the two buttons the row carries, and nothing else. An installed machine can be
    /// removed; one that is only on offer can be added. The same machine is often both in one
    /// session, since removing a machine the program ships with is what puts it back on offer.
    /// </remarks>
    public bool IsInstalled { get; }

    /// <summary>The other half of the same fact, because a row cannot bind to "not".</summary>
    public bool IsAvailable => !IsInstalled;

    /// <summary>Where the row stands, in a word, since it has no room for a path.</summary>
    public string State => IsInstalled ? "installed" : "available";
}

/// <summary>
/// What machines this installation has, what it is offered, and the three things that can be
/// done about it: take one off the shelf, throw one out, and bring one in from a zip.
/// </summary>
/// <remarks>
/// This is the page where an installation is made up. The application ships machines rather than
/// fixing them in place, so what is on the rack is a decision somebody made here, and every part
/// of it can be undone: a machine that ships and is thrown out comes straight back onto the list
/// as one to add.
///
/// It reads the folders itself rather than asking the rack. The rack is built once at startup out
/// of what <see cref="IMachineRegistry.Load"/> found, so it cannot answer for a machine that
/// arrived a minute ago, and it says nothing about what is only on offer.
/// </remarks>
public sealed partial class MachineShelfViewModel : ObservableObject
{
    /// <summary>The machines folder on disc.</summary>
    /// <remarks>Shared rather than one apiece: it holds nothing of its own.</remarks>
    private static readonly IMachineRegistry Registry = new MachineRegistry();

    /// <summary>A machine going into a zip and coming back out.</summary>
    /// <remarks>Shared rather than one apiece: it holds nothing of its own.</remarks>
    private static readonly IMachineArchive Crates = new MachineArchive();

    /// <summary>The machines this run has, the one instance everything shares.</summary>
    private readonly IMachineProjects _machines;

    /// <summary>Reads what is installed and what is on offer.</summary>
    public MachineShelfViewModel(IMachineProjects machines)
    {
        _machines = machines;
        Refresh();
    }

    /// <summary>Every machine there is to show, installed and available together, by name.</summary>
    public ObservableCollection<MachineShelfEntry> Machines { get; } = new();

    /// <summary>What the last thing done here did, in the words the page shows.</summary>
    [ObservableProperty]
    private string _status = "";

    /// <summary>
    /// The two things somebody has to be told before they use this list.
    /// </summary>
    /// <remarks>
    /// <see cref="Tracker.Records.Machine.Register"/> runs once, at startup, and everything downstream of
    /// it, the rack, the panels and the names songs are read with, is built from what it took. A
    /// machine registered half way through a session would be missing from all of that, so adding
    /// stops at the disc and the page says so instead of pretending otherwise.
    ///
    /// The second is what removing costs. For a machine the program ships with it costs nothing,
    /// which is the whole design. For one that came in from a zip there is no shelf to take it
    /// from again, and saying that plainly is better than letting somebody find out.
    /// </remarks>
    public string Hint =>
        "A machine added here is on the rack at once, and a panel showing it is drawn again. "
        + "The machines the program ships with can be added back after they are removed; "
        + "one that came in from a zip is gone until that zip is imported again.";

    /// <summary>
    /// Said when what this installation has has changed, so whatever is showing a machine can
    /// show it again.
    /// </summary>
    /// <remarks>
    /// Importing a machine used to mean restarting the app, because the machines are read once
    /// at startup and everything downstream was built from what was read. Reading them again and
    /// saying so is the difference between a designer you can work in and one you have to leave:
    /// export, import, look at it on the rack, change it, and round again.
    /// </remarks>
    public event Action? Changed;

    /// <summary>Reads the machines off the disc again and tells everybody that showed one.</summary>
    private void Reload()
    {
        var machines = Registry.Load();

        _machines.Keep(machines);

        Changed?.Invoke();
    }

    /// <summary>Rebuilds the list out of what is installed and what is on offer.</summary>
    /// <remarks>
    /// One list rather than two, in one order that does not move: a machine that changes side
    /// stays where the eye left it, so adding one back reads as the row changing its mind rather
    /// than as something jumping to the bottom of a second list.
    /// </remarks>
    public void Refresh()
    {
        var rows = new List<MachineShelfEntry>();

        foreach (var project in Registry.In(Registry.Installed))
        {
            rows.Add(new MachineShelfEntry(project, installed: true));
        }

        foreach (var project in Registry.Available())
        {
            rows.Add(new MachineShelfEntry(project, installed: false));
        }

        Machines.Clear();

        foreach (var row in rows.OrderBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase)
                                .ThenBy(r => r.Id, StringComparer.Ordinal))
        {
            Machines.Add(row);
        }
    }

    /// <summary>
    /// Installs the machine in that zip, and says what happened either way.
    /// </summary>
    /// <remarks>
    /// The picker that produced the path belongs to the window and is opened in the settings
    /// page's code behind, the same way the plugin folders are picked. Nothing here throws: a
    /// zip somebody was handed is exactly the kind of file that is the wrong file, and being
    /// told so is the answer.
    /// </remarks>
    public void Import(string zipPath)
    {
        if (string.IsNullOrWhiteSpace(zipPath)) return;

        MachineProject? installed;

        try
        {
            installed = Crates.Import(zipPath);
        }
        catch (Exception ex)
        {
            Diagnostics.Log.Fault(Diagnostics.Enums.LogArea.App, "Machine import failed", ex);

            Status = "Could not read " + Path.GetFileName(zipPath) + ".";

            return;
        }

        if (installed == null)
        {
            Status = "There is no machine in " + Path.GetFileName(zipPath) + ".";

            return;
        }

        Refresh();

        Reload();

        Status = "Imported " + Called(installed) + " " + installed.Version + ".";
    }

    /// <summary>Takes a machine the program ships with and gives it to this installation.</summary>
    /// <remarks>
    /// Always enabled; a row that is already installed is refused rather than greyed, since a row
    /// carries the one button that belongs to the side it is on.
    /// </remarks>
    public IRelayCommand<MachineShelfEntry> AddCommand => new RelayCommand<MachineShelfEntry>(Add);

    /// <summary>Copies a shipped machine into this installation, and says what happened.</summary>
    /// <remarks>
    /// The name is read before the copy, since afterwards the row is about to be replaced by the
    /// rebuild and there would be nothing left to name in the status line.
    /// </remarks>
    private void Add(MachineShelfEntry? entry)
    {
        if (entry == null || entry.IsInstalled) return;

        string name = entry.Name;

        var installed = Crates.Add(entry.Project);

        Refresh();

        Reload();

        Status = installed == null
            ? "Could not add " + name + "."
            : "Added " + name + ". It is on the rack now.";
    }

    /// <summary>Throws a machine out of this installation, and says whether it can come back.</summary>
    /// <remarks>
    /// Whether it can is asked before the folder goes, since afterwards the two lists no longer
    /// hold the answer. The shelf beside the program is not touched by any of this, so a machine
    /// that ships is on offer again by the time the list is rebuilt.
    ///
    /// Always enabled; a row that is not installed is refused rather than greyed.
    /// </remarks>
    public IRelayCommand<MachineShelfEntry> RemoveCommand => new RelayCommand<MachineShelfEntry>(Remove);

    /// <summary>Deletes a machine's folder, and says whether it can be had again.</summary>
    /// <remarks>
    /// Whether it ships is asked before the folder goes, since afterwards the two lists no longer
    /// hold the answer.
    /// </remarks>
    private void Remove(MachineShelfEntry? entry)
    {
        if (entry == null || !entry.IsInstalled) return;

        string name = entry.Name;

        bool ships = Registry.In(Registry.Shipped).Any(p => p.Id == entry.Id);

        if (!Crates.Remove(entry.Project))
        {
            Status = "Could not remove " + name + ".";

            return;
        }

        Refresh();

        Reload();

        Status = ships
            ? "Removed " + name + ". It is on the list to add back."
            : "Removed " + name + ". It came in from a zip, so it is gone until that zip is imported again.";
    }

    /// <summary>What to call a machine that has just arrived, before there is a row for it.</summary>
    private static string Called(MachineProject project) =>
        project.Name.Length > 0 ? project.Name : project.Id;
}
