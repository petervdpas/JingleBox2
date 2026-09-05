namespace JingleBox2.SoundDevices.Interfaces;

/// <summary>
/// The page a device carries about itself, kept in its own folder.
/// </summary>
/// <remarks>
/// Every device needs help and none of it belongs in the application's. The pages under
/// <c>help/</c> are what this program does and are written when this program changes; what a
/// soundmachine's third knob does is written by whoever built the machine, changes when they
/// change it, and has to travel with the box to somebody who has never seen this repository.
/// So it is a file in the device's folder, which means the zip carries it, Save as carries it,
/// and a shipped device is brought up to date with it file by file like everything else it has.
///
/// Markdown, and the same markdown the application's own help is written in, so it is read by
/// one reader and drawn by one control. A device with nothing to say has no file rather than an
/// empty one: what is not there is what the Menu greys out.
///
/// A seam rather than two lines inside each project, because both worlds keep it the same way
/// and because reading and writing somebody's folder is exactly the sort of thing that should
/// be answerable without a disc underneath it.
/// </remarks>
public interface ISoundDeviceHelp
{
    /// <summary>What the file is called inside a device's folder.</summary>
    string FileName { get; }

    /// <summary>
    /// The page that folder holds, or nothing where there is none.
    /// </summary>
    /// <remarks>
    /// Nothing rather than a fault for a folder that is not there, a file that cannot be read,
    /// or a device that has never been saved: this is asked on the way to drawing a menu, and a
    /// help page nobody can read should cost a greyed line rather than a panel that will not
    /// open.
    /// </remarks>
    /// <param name="folder">The device's folder.</param>
    string Read(string? folder);

    /// <summary>
    /// Writes the page into that folder, or takes the file away when there is nothing to say.
    /// </summary>
    /// <remarks>
    /// Written whole through the same rule everything else here is written by, so a save that
    /// fails part way leaves the page that was there. Emptied means deleted rather than a file
    /// holding no words: a device either has a page or it has not, and an empty file would leave
    /// the Menu offering a line that opens nothing.
    /// </remarks>
    /// <param name="folder">The device's folder.</param>
    /// <param name="text">The page, or nothing to take it away.</param>
    void Write(string? folder, string? text);
}
