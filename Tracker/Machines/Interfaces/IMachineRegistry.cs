using System.Collections.Generic;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tracker.Machines.Interfaces;

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
///
/// A seam rather than a static class because every method here reaches the disc: it makes
/// folders, copies files into them and writes down what it has offered. There was no way to
/// stand in front of that, so none of it was tested, and the parts of it that are easy to get
/// wrong are exactly the parts that only show on somebody else's installation.
/// </remarks>
public interface IMachineRegistry
{
    /// <summary>What the folder holding the machines is called, in both places it appears.</summary>
    /// <remarks>
    /// Written out rather than built, so the one folder name this depends on can be found by
    /// looking for it.
    /// </remarks>
    string FolderName { get; }

    /// <summary>Where the machines that ship with the program live.</summary>
    string Shipped { get; }

    /// <summary>And where the ones this installation has live.</summary>
    string Installed { get; }

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
    /// <param name="path">The file being asked about.</param>
    bool Ships(string path);

    /// <summary>
    /// Reads the machines this installation has and takes them into the list the app works from.
    /// </summary>
    /// <remarks>
    /// Everything read last time is forgotten first. A machine thrown out in SETTINGS has to be
    /// gone from the list the moment it is rebuilt, not at the next start.
    ///
    /// Read is not taken, and the difference is the whole of why a machine can be on disc and
    /// not on the rack. Every folder here is read; each is then offered to
    /// <c>Machine.Register</c>, which refuses any id it has no engine for and is passed over
    /// without a word. So what comes back is what the rack will show, which is a subset of what
    /// is installed, and a machine designed under an id of its own is in neither.
    /// </remarks>
    /// <returns>What was taken, for the log and for the settings page to show.</returns>
    IReadOnlyList<MachineProject> Load();

    /// <summary>The machines that ship and are not installed here, which are the ones on offer.</summary>
    IReadOnlyList<MachineProject> Available();

    /// <summary>The projects in that folder, or none when there is no folder.</summary>
    /// <remarks>
    /// A folder that will not read is nothing rather than a fault: this is called on the way to
    /// drawing the rack, and one unreadable folder should not take the rack with it.
    /// </remarks>
    /// <param name="folder">The folder to read, which need not exist.</param>
    IReadOnlyList<MachineProject> In(string folder);
}
