namespace JingleBox2.Files.Interfaces;

/// <summary>
/// A folder copied whole, which is what a thing that travels as a folder is copied by.
/// </summary>
/// <remarks>
/// The empty folders as well as the files. Everything here that travels is a folder with a shape:
/// pictures in one place, presets in another, the manifest at the top. Arriving without the empty
/// ones, the first thing anybody does with what they were given is make a folder it was supposed
/// to have.
///
/// Nothing in what it lands in is deleted, which is the same rule everything else here keeps
/// about somebody's own folder: a file already there and not in the copy is theirs.
/// </remarks>
public interface IFolderCopy
{
    /// <summary>Copies a folder and everything under it, making what it lands in.</summary>
    /// <param name="from">The folder being copied.</param>
    /// <param name="into">Where it goes, which is made if it is not there.</param>
    void Into(string from, string into);
}
