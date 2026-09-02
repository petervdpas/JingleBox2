namespace JingleBox2.SoundDevices.SoundMachines.Interfaces;

/// <summary>
/// The two questions asked of every path a machine holds: is it inside the machine, and what is
/// it called in there.
/// </summary>
/// <remarks>
/// A machine travels as a folder, so a recording it carries has to be written down relative to
/// that folder and with forward slashes, or the machine only works on the computer it was built
/// on. Reading one back is the same act in reverse, and both were written out separately in four
/// places, which is four chances for one of them to compare paths the wrong way.
///
/// The wrong way is what this exists to settle. Windows treats two paths that differ only in
/// case as the same path and Linux does not, so a test done with an exact comparison is a test
/// that says no on Windows for a file that is plainly there. What comes of that is not a crash:
/// a machine quietly refuses to be removed, or writes an absolute path into a zip that will not
/// open anywhere else.
///
/// A seam rather than a static class, and for the same reason the path rule underneath it is
/// one. The comparison is read off the machine the program is running on, so a program running
/// on Linux cannot ask what Windows would have decided, and every fault this class exists to
/// prevent is a fault that only shows on the other system. Handed the rule instead of reading
/// it, both answers can be put a question to on either machine, which is the whole point of the
/// change: a machine that quietly refuses to be removed is a bug nobody can reproduce, and a
/// test that can hold the Windows rule on a Linux box is one that can.
/// </remarks>
public interface ISoundMachinePaths
{
    /// <summary>True when that path is somewhere inside that folder.</summary>
    /// <remarks>
    /// The folder itself does not count as being inside itself, which is what the length test is
    /// for: a machine cannot be installed over the folder it is being copied from, and a file
    /// that is the folder is not a file in it.
    /// </remarks>
    /// <param name="path">The path being asked about.</param>
    /// <param name="folder">The folder it might be under.</param>
    bool Under(string path, string folder);

    /// <summary>
    /// What that path is called inside that folder, or nothing when it is not in there.
    /// </summary>
    /// <remarks>
    /// Forward slashes whatever this system uses, because the answer is going into a file that
    /// another system will read. <see cref="Outside"/> puts it back.
    /// </remarks>
    /// <param name="path">The path being written down.</param>
    /// <param name="folder">The machine's folder, which the name is said from.</param>
    string? Named(string path, string folder);

    /// <summary>Where a name written down inside a machine really is on this disc.</summary>
    /// <param name="named">The name as the machine wrote it, with forward slashes.</param>
    /// <param name="folder">The machine's folder, which the name is said from.</param>
    string Outside(string named, string folder);
}
