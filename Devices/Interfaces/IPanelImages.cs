using System.Collections.Generic;

namespace JingleBox2.Devices.Interfaces;

/// <summary>
/// The pictures on a face, as files in the folder the face travels in.
/// </summary>
/// <remarks>
/// A face can carry artwork, and the artwork is files: a logo, a plate, a picture of the thing
/// itself. They live in an <c>images</c> folder inside the box's own folder and are named
/// <c>image1</c>, <c>image2</c> and so on, so the panel names one by what it is called in there
/// and the two find each other again on whatever disc they land on.
///
/// The rules are the same for a machine and for an effect, which is why they are here rather
/// than on either: added under the next free number, swept when nothing names them any more,
/// renumbered so the folder has no gaps, and removed one at a time when the last element showing
/// one has gone. Written for machines first, and not one line of it was about a machine.
///
/// Anything in the folder that is not one of ours is left alone throughout. A box is a folder
/// somebody can put things in, and tidying is not a licence to rearrange it.
/// </remarks>
public interface IPanelImages
{
    /// <summary>
    /// Copies a picture into the folder under the next free number.
    /// </summary>
    /// <remarks>
    /// Copied rather than named where it lies, because the folder is what travels: a face
    /// pointing at somebody's desktop draws nothing on anybody else's machine.
    ///
    /// A number is taken if anything at all is called it, whatever the extension: a png and a jpg
    /// both landing on image3 would be two pictures nobody could tell apart in the folder.
    /// </remarks>
    /// <param name="folder">The box's own folder, or empty for one never saved.</param>
    /// <param name="path">The picture being added, wherever it is now.</param>
    /// <returns>What the panel should call it, or nothing when there was nowhere to put it.</returns>
    string? Add(string folder, string path);

    /// <summary>
    /// Deletes every picture the face no longer names, and says how many went.
    /// </summary>
    /// <remarks>
    /// A picture can stop being used without any element being removed: point one at a different
    /// file and the old one is nobody's. Asked at the moment the box is written down, so what is
    /// saved and what is in the folder are the same thing.
    /// </remarks>
    /// <param name="folder">The box's own folder.</param>
    /// <param name="kept">What the face still names, as the panel writes it: "images/image1.png".</param>
    int Sweep(string folder, ISet<string> kept);

    /// <summary>
    /// Closes the gaps in the picture numbers, and says what became what.
    /// </summary>
    /// <remarks>
    /// A folder holding image2 and no image1 has plainly lost something, and the folder is the
    /// first place anybody looks when a picture does not draw. So after one goes, the rest
    /// shuffle down and the panel is told what everything is called now.
    ///
    /// The order is the order the numbers were in, so the pictures keep their sequence and
    /// nobody's second logo becomes their first. Renaming downwards can never land on a file that
    /// has not been dealt with yet, since every new number is at or below the old one.
    /// </remarks>
    /// <param name="folder">The box's own folder.</param>
    /// <returns>What each picture was called, against what it is called now.</returns>
    IReadOnlyDictionary<string, string> Renumber(string folder);

    /// <summary>
    /// Takes a picture out of the folder, file and all.
    /// </summary>
    /// <remarks>
    /// Called when the last element naming it has gone. The folder gets zipped and handed to
    /// somebody, so a picture nothing shows is weight in the parcel and a puzzle for whoever
    /// opens it. The original is still wherever it was picked from: what is deleted here is this
    /// box's copy of it.
    ///
    /// The name is checked before anything is deleted, the same way the importer checks a zip's:
    /// it has to land inside this box's own pictures folder. A name is a claim, and this one
    /// arrives out of a file somebody else may have written.
    /// </remarks>
    /// <param name="folder">The box's own folder.</param>
    /// <param name="named">What the panel calls it, relative to that folder.</param>
    bool Remove(string folder, string named);
}
