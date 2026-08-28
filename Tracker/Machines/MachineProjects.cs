using JingleBox2.Machines;
using System;
using System.Collections.Generic;

namespace JingleBox2.Tracker.Machines;

/// <summary>
/// The machines this run is working with, kept where a panel can ask for one.
/// </summary>
/// <remarks>
/// <see cref="MachineRegistry"/> reads the machines off the disc once, at startup, and hands
/// back what it found. Until now that list was counted into the log and dropped, because the
/// only thing anything wanted from a machine was its name and its colour, and those had already
/// been pushed into <see cref="Machine"/>. A panel drawn from a machine's own description wants
/// the machine itself: the parameters and the face. So the list is kept.
///
/// Static, and deliberately: which machines are installed is a fact about the installation and
/// not about any one window, the same way the list of machines is. Read many times a second
/// while a panel is on screen, written once at startup.
/// </remarks>
public static class MachineProjects
{
    /// <summary>The machines this installation has, by id.</summary>
    /// <remarks>
    /// Case is ignored because an id is a folder name and Windows would call two spellings of
    /// one machine the same folder while Linux would not. Agreeing with the file system is the
    /// only answer that does not depend on which computer the machine was built on.
    /// </remarks>
    private static readonly Dictionary<string, MachineProject> Found =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Takes what the registry read, replacing whatever was known before.</summary>
    public static void Keep(IEnumerable<MachineProject> machines)
    {
        Found.Clear();

        foreach (var machine in machines)
        {
            if (machine.Id.Length > 0) Found[machine.Id] = machine;
        }
    }

    /// <summary>The machine with that id, or nothing when this installation has none.</summary>
    public static MachineProject? For(string? id) =>
        id is { Length: > 0 } && Found.TryGetValue(id, out var machine) ? machine : null;

    /// <summary>
    /// That machine's face, or nothing when it has none worth drawing.
    /// </summary>
    /// <remarks>
    /// A machine that has been made but never laid out has an empty panel, and drawing that
    /// would put a blank page where the instrument's controls used to be. Nothing is the right
    /// answer, and whoever asked falls back to the panel written by hand.
    /// </remarks>
    public static MachinePanel? PanelFor(string? id)
    {
        var machine = For(id);

        if (machine?.Panel.Root is not { } root) return null;

        return root.Children.Count == 0 && root.Parameter.Length == 0 ? null : machine.Panel;
    }
}
