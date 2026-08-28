using System.Collections.Generic;
using JingleBox2.Machines;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tracker.Machines.Interfaces;

/// <summary>
/// The machines this run is working with, kept where a panel can ask for one.
/// </summary>
/// <remarks>
/// <see cref="IMachineRegistry"/> reads the machines off the disc once, at startup, and hands
/// back what it found. Until now that list was counted into the log and dropped, because the
/// only thing anything wanted from a machine was its name and its colour, and those had already
/// been pushed into <see cref="Machine"/>. A panel drawn from a machine's own description wants
/// the machine itself: the parameters and the face. So the list is kept.
///
/// It was a static class holding a static dictionary, and that is the one thing here that was
/// plainly wrong. Which machines are installed is a fact about the run, not about the program,
/// and a static one is a fact about the process: one test calling <see cref="Keep"/> changed
/// what every test after it saw, in whatever order the runner happened to pick. The dictionary
/// is an ordinary field on an ordinary object now, so a test makes its own and it is thrown away
/// with the test, and the application keeps the one it made at startup exactly as before.
///
/// Read many times a second while a panel is on screen, written once when the rack is read.
/// </remarks>
public interface IMachineProjects
{
    /// <summary>Takes what the registry read, replacing whatever was known before.</summary>
    /// <param name="machines">The machines this installation has, as the registry found them.</param>
    void Keep(IEnumerable<MachineProject> machines);

    /// <summary>The machine with that id, or nothing when this installation has none.</summary>
    /// <remarks>
    /// Case is ignored because an id is a folder name and Windows would call two spellings of
    /// one machine the same folder while Linux would not. Agreeing with the file system is the
    /// only answer that does not depend on which computer the machine was built on.
    /// </remarks>
    /// <param name="id">The machine's id, as a song writes it down.</param>
    MachineProject? For(string? id);

    /// <summary>
    /// That machine's face, or nothing when it has none worth drawing.
    /// </summary>
    /// <remarks>
    /// A machine that has been made but never laid out has an empty panel, and drawing that
    /// would put a blank page where the instrument's controls used to be. Nothing is the right
    /// answer, and whoever asked falls back to the panel written by hand.
    /// </remarks>
    /// <param name="id">The machine's id, as a song writes it down.</param>
    MachinePanel? PanelFor(string? id);
}
