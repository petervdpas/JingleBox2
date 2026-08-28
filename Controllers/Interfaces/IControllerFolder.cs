using JingleBox2.Midi.Enums;

namespace JingleBox2.Controllers.Interfaces;

/// <summary>
/// Where a controller's own files live, and how one is matched to a port.
/// </summary>
/// <remarks>
/// A controller can have two files and needs neither. A <c>.json</c> saying what it is, and a
/// <c>.lua</c> saying what it does, both named after the device and sitting side by side. The
/// split is the whole design: what a MiniLab 3 <i>is</i> is a fact about every MiniLab 3 there
/// will ever be and belongs in a file anybody can read, and what one <i>does</i> is behaviour
/// and needs a language. Most controllers will only ever want the first.
///
/// Two folders, as machines have two. Beside the program is what ships and is never written to;
/// under the application folder is what this installation has. The first run fills the second
/// from the first, and only when it is not there at all: empty is somebody who threw them out,
/// and putting them back would be undoing that.
/// </remarks>
public interface IControllerFolder
{
    /// <summary>What the folder is called, in both places it exists.</summary>
    /// <remarks>
    /// The same word beside the program and under the application folder, so somebody told
    /// where their controller files are has been told where both of them are.
    /// </remarks>
    string Name { get; }

    /// <summary>Where the controller files that ship with the program live.</summary>
    string Shipped { get; }

    /// <summary>And where the ones this installation has live.</summary>
    string Installed { get; }

    /// <summary>
    /// Gives this installation any controller file the program ships that it has never been
    /// offered.
    /// </summary>
    /// <remarks>
    /// It was the absence of the folder that decided, which is right while the set of files
    /// never changes and wrong the moment one is added. The folder was made the first time a
    /// codec shipped, so the profile that shipped an hour later could never arrive: the folder
    /// was there, so there was nothing to do. Exactly the mistake
    /// <see cref="Tracker.Machines.MachineRegistry"/> had already made and already fixed, which
    /// is where this is copied from.
    ///
    /// So what is recorded is the offer, not the folder. A file this installation has never been
    /// offered is put in; one it has been offered is left alone whether or not it is still
    /// there, which is what keeps a codec somebody deleted deleted.
    ///
    /// A folder from before this record existed is taken to have been offered whatever it holds.
    /// Right for everything anybody kept, and wrong once for anything they had already thrown
    /// out, which comes back a single time and stays gone after.
    ///
    /// The offer is recorded whether or not the file went in. One that cannot be copied has
    /// still been offered, and trying again on every start would write the same fault into the
    /// log for ever.
    ///
    /// Unlike machines, nothing is ever refreshed from what ships. A machine that ships is the
    /// machine and an update to it should reach the rack. A controller file is the opposite: the
    /// entire point of the folder is that you edit what is in it, and overwriting somebody's
    /// codec with ours because ours is newer would throw away the work the folder exists for.
    /// </remarks>
    void FirstRun();

    /// <summary>A port's name against a pattern, where a star stands for anything at all.</summary>
    /// <remarks>
    /// Deliberately the smallest possible matcher. A port is called `Minilab3 MIDI` on Linux and
    /// the same thing with a number in front of it on Windows, so a pattern is the least a match
    /// can be and still work in both places. Anything more is a language nobody asked for.
    ///
    /// A pattern with no star is a contains, since that is what somebody writing one means.
    /// </remarks>
    bool Like(string pattern, string text);
}
