namespace JingleBox2.Tracker.Interfaces;

/// <summary>
/// A box as it travels: one zip of the project folder, and the same folder again on somebody
/// else's disc.
/// </summary>
/// <remarks>
/// A machine and an effect are each already a folder with a manifest at the top of it, so there
/// is nothing to invent here and nothing in it is about which of the two is in the folder. The
/// zip is that folder, and installing is putting it under
/// <see cref="IRackRegistry{T}.Installed"/> in a folder named after the box's id, which is the
/// name songs write down and therefore the only name that cannot collide by accident.
///
/// Two ways in and one door. A zip somebody was handed is unpacked; one the program ships with
/// is copied off the shelf beside the program. What arrives is different, where it lands is
/// not, so both go through one install and get the same checking and the same swap.
///
/// Everything a bundle says about where its contents go is a claim made by whoever built it, so
/// none of it is believed: the id has to name a folder and not a path, and a file has to land
/// inside the folder it is being written into. The rest of the app reads what is on the disc
/// off the disc by reading the manifest, and this is the one place a stranger's file gets to put
/// anything there.
///
/// Everything here writes to <see cref="Diagnostics.Enums.LogArea.Machines"/> rather than to the
/// application's own area, as everything under this folder does. What a bundle was refused for
/// is the sort of thing somebody goes looking for, and it should not be buried under everything
/// else the application had to say that session.
///
/// A seam rather than a static class, and this is the one where it matters most. Every method
/// makes folders, unpacks a stranger's zip into them and swaps one folder for another. The
/// guards against a bundle writing outside its own folder are the whole reason the class exists
/// and there was no way to put a question to any of them without installing something on the
/// person running the test.
/// </remarks>
/// <typeparam name="T">The manifest a box of this kind is read into.</typeparam>
public interface IRackArchive<T> where T : class, IRackProject
{
    /// <summary>Zips the project folder, manifest and sounds and all, into that file.</summary>
    /// <remarks>
    /// Throws rather than reporting: this is asked for by somebody who has just pressed Export
    /// and is waiting to be told either where the file went or what stopped it.
    ///
    /// An existing file is overwritten, which is the ordinary case: exporting twice in a row is
    /// how a machine gets corrected, and being made to delete the old file first would only be
    /// in the way.
    /// </remarks>
    /// <param name="project">The box to pack, which has to have been saved.</param>
    /// <param name="zipPath">Where the zip goes, folders made as needed.</param>
    void Export(T project, string zipPath);

    /// <summary>
    /// Copies everything a box keeps beside its manifest into another folder.
    /// </summary>
    /// <remarks>
    /// What a box is, is the folder: the manifest names pictures, presets and sounds by the
    /// names they have inside it, so a manifest written into an empty folder somewhere else is a
    /// face that draws nothing and has no presets. Writing the manifest is
    /// the project's own save's job and it does only that, correctly, since it is
    /// called on every ordinary save and copying the whole folder onto itself each time would be
    /// absurd. This is the other half, for the one case where the folder changes.
    ///
    /// The manifest itself is not copied. The one in the source folder is what was last written
    /// and is behind whatever is on screen, so the caller writes the current one afterwards
    /// rather than copying a stale one and overwriting it a moment later.
    ///
    /// Nothing in the destination is deleted, including a file this box no longer has. The
    /// same rule the registry keeps for a shipped box being updated, and for the same
    /// reason: what else is in that folder is not this box's business.
    ///
    /// Throws rather than reporting, as <see cref="Export"/> does: somebody has just pressed
    /// Save as and is waiting to be told either where it went or what stopped it.
    /// </remarks>
    /// <param name="project">The box to carry, which has to have been saved.</param>
    /// <param name="folder">Where its files go, made as needed.</param>
    void CopyInto(T project, string folder);

    /// <summary>
    /// Unpacks a box out of that zip and in among the installed ones.
    /// </summary>
    /// <returns>The box as it now sits on the disc, or null when the zip held none.</returns>
    /// <remarks>
    /// Both shapes of zip are read: the folder's contents at the top, which is what
    /// <see cref="Export"/> writes, and the folder itself at the top, which is what somebody
    /// gets who right-clicks the folder and zips that. Refusing the second would only teach
    /// people that the importer is broken.
    ///
    /// Reported rather than thrown, unlike <see cref="Export"/>: a zip somebody was handed can
    /// be anything at all, and every way it can be wrong ends the same way, with nothing
    /// installed and a line in the log.
    /// </remarks>
    /// <param name="zipPath">The zip somebody was handed.</param>
    T? Import(string zipPath);

    /// <summary>
    /// Takes one the program ships with and puts a copy of it among the installed ones.
    /// </summary>
    /// <returns>The box as it now sits in the installed folder, or null when it could not go.</returns>
    /// <remarks>
    /// The folder beside the program is a shelf to take from and is never written to, so this is
    /// a copy in one direction and the shipped copy is left exactly as it was. That is what
    /// makes removing one reversible: the copy goes, the original is still on the shelf.
    ///
    /// It ends where <see cref="Import"/> ends, by the same route, because one arriving from a
    /// zip and one arriving from the shelf are the same event once the files are in
    /// hand. Both are checked the same way, both land through the same swap, and both are read
    /// back off the disc rather than believed.
    ///
    /// Copying the installed folder onto itself is refused. That is not adding anything, and
    /// the swap that finishes an install would be moving a folder out from under its own source.
    /// </remarks>
    /// <param name="fromCrate">The box on the shelf beside the program.</param>
    T? Add(T fromCrate);

    /// <summary>Deletes an installed box's folder.</summary>
    /// <remarks>
    /// Only one that is installed. The shelf beside the program is what the application ships
    /// and is never written to, which is exactly what lets this delete freely: one that
    /// ships can be taken again with <see cref="Add"/> the moment it is gone.
    /// </remarks>
    /// <param name="project">The box to delete, which has to be an installed one.</param>
    /// <returns>True when the folder is gone.</returns>
    bool Remove(T project);
}
