using System.Collections.Generic;

namespace JingleBox2.Tracker.Interfaces;

/// <summary>
/// What boxes of one kind this installation has.
/// </summary>
/// <remarks>
/// A box is a project: a folder with a manifest in it. This reads the folders and offers each
/// one to whatever decides whether this build can have it, so what the rack shows, what the
/// panels are painted with and what a song writes down all come off the boxes themselves rather
/// than out of the application's own code.
///
/// Two folders, and only one of them is this installation's. Beside the program is what the
/// application ships: a source to take a box from, never written to and never read as the answer
/// to what is on the rack. Under the app folder is what this installation actually has, and that
/// one alone decides. The point of the split is that removing a box is not losing it: the
/// shipped copy stays where it was and it can be taken again.
///
/// Registering is a deliberate act and so is unregistering, which is why what has been offered
/// is recorded rather than what is present. A shipped box this installation has never been
/// offered is put on the rack; one it has been offered is left alone whether or not it is still
/// there. So a box written after the folder was made still arrives, and one somebody threw out
/// stays thrown out.
///
/// A box this build has no engine for is read and passed over rather than put on the rack as
/// something that cannot sound. That is what makes a folder from a later version harmless, and
/// it is the gate that has to move before a box written by somebody else can be had at all.
///
/// Everything here writes to <see cref="Diagnostics.Enums.LogArea.Machines"/> rather than to the
/// application's own area. What was found and what could not be read is a whole half of the
/// program, and reading it out of everything the application did at startup is exactly what
/// nobody wants on the day a box comes back missing its picture.
///
/// A seam rather than a static class because every method here reaches the disc: it makes
/// folders, copies files into them and writes down what it has offered. There was no way to
/// stand in front of that, so none of it was tested, and the parts of it that are easy to get
/// wrong are exactly the parts that only show on somebody else's installation.
///
/// It is a rack registry and not a machine one because there are two worlds on the rack now.
/// Not one of these members says anything about notes, audio, or which of the two it is holding:
/// machines were merely first.
/// </remarks>
/// <typeparam name="T">The manifest a box of this kind is read into.</typeparam>
public interface IRackRegistry<T> where T : class, IRackProject
{
    /// <summary>What the folder holding these is called, in both places it appears.</summary>
    string FolderName { get; }

    /// <summary>Where the ones that ship with the program live.</summary>
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
    /// Answered by looking, not by where the path points. Both folders hold boxes and the
    /// installed one holds the user's as well, so the only honest test is whether the same file
    /// is also in the folder beside the program.
    /// </remarks>
    /// <param name="path">The file being asked about.</param>
    bool Ships(string path);

    /// <summary>
    /// Reads what this installation has and takes it into the list the app works from.
    /// </summary>
    /// <remarks>
    /// Everything read last time is forgotten first. A box thrown out in SETTINGS has to be gone
    /// from the list the moment it is rebuilt, not at the next start.
    ///
    /// Read is not taken, and the difference is the whole of why a box can be on disc and not on
    /// the rack. Every folder here is read; each is then offered to whatever knows which engines
    /// this build has, and one it refuses is passed over without a word. So what comes back is
    /// what the rack will show, which is a subset of what is installed.
    /// </remarks>
    /// <returns>What was taken, for the log and for the settings page to show.</returns>
    IReadOnlyList<T> Load();

    /// <summary>The ones that ship and are not installed here, which are the ones on offer.</summary>
    IReadOnlyList<T> Available();

    /// <summary>The projects in that folder, or none when there is no folder.</summary>
    /// <remarks>
    /// A folder that will not read is nothing rather than a fault: this is called on the way to
    /// drawing the rack, and one unreadable folder should not take the rack with it.
    /// </remarks>
    /// <param name="folder">The folder to read, which need not exist.</param>
    IReadOnlyList<T> In(string folder);
}
