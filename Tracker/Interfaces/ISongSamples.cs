using System.Collections.Generic;
using System.IO.Compression;

namespace JingleBox2.Tracker.Interfaces;

/// <summary>
/// The recordings a song can carry inside it, and what happens to them when one is opened.
/// </summary>
/// <remarks>
/// A song normally names its recordings and does not hold them, which is right for the song
/// you are working on: it stays small, it saves in milliseconds, and a take you improve is
/// improved everywhere it plays. It is wrong for a song you are handing to somebody, who has
/// none of your takes and will open a track that makes no sound.
///
/// So a song can be packed, which is the same file with the recordings in it. Packing is asked
/// for rather than done on every save, because a song built on a long take is tens of megabytes
/// and the open song is written out every twenty seconds whether anybody asked or not.
///
/// What goes in is decided per recording, not per song, and decided by where the recording came
/// from. A machine's own presets ship with the program and are on every installation there is,
/// so carrying them would be sending somebody a second copy of what they already have, once per
/// song. What is worth carrying is what only this machine has: the takes on your own shelf.
/// Reason has done it this way for twenty years, and its rule is the same one: everything
/// outside the factory bank travels, everything inside it is named.
///
/// Opening a packed song puts its recordings on the shelf, through the same door anything else
/// gets there by, and points the instruments at what landed. After that it is an ordinary song
/// playing ordinary takes, which is the point: what arrives is yours, not something hidden
/// inside a file that only one song can reach.
/// </remarks>
public interface ISongSamples
{
    /// <summary>What the container calls the list of what it carries.</summary>
    string ManifestEntry { get; }

    /// <summary>
    /// The recordings this song would carry: what it plays, less what ships with the program.
    /// </summary>
    /// <param name="song">The song to go over. Nothing carries nothing.</param>
    IReadOnlyList<string> Wanted(Song song);

    /// <summary>
    /// Puts those recordings in the container, and the list of them beside.
    /// </summary>
    /// <remarks>
    /// The audio goes in stored rather than compressed. Sixteen bit audio gives up very little to
    /// deflate and a long take is tens of megabytes: the wait is real and what it buys is not.
    /// The manifest beside it is compressed, since it is text and it is small.
    ///
    /// One recording that will not go in is one silent instrument, not a failed save, so each is
    /// tried on its own and a failure is passed over. Nothing is written at all when none of them
    /// went in, so a container with a manifest is a container that really is carrying something.
    /// </remarks>
    /// <param name="container">The song file, open for writing.</param>
    /// <param name="files">What to carry, as <see cref="Wanted"/> answered.</param>
    void Write(ZipArchive container, IReadOnlyList<string> files);

    /// <summary>True when this container is carrying its recordings.</summary>
    /// <param name="container">The song file, open for reading.</param>
    bool Packed(ZipArchive container);

    /// <summary>
    /// Puts a packed song's recordings on the shelf and points its instruments at them.
    /// </summary>
    /// <remarks>
    /// One recording at a time, and one that will not come out is passed over: its instrument is
    /// left pointing where the song said, which is a path this machine may well have anyway.
    ///
    /// The instruments are repointed from where the recording was on the machine that packed it
    /// to where it has just landed here, which is why the manifest keeps the old path at all.
    /// </remarks>
    /// <param name="container">The song file, open for reading.</param>
    /// <param name="song">The song just read out of it, whose instruments are repointed in place.</param>
    /// <returns>What landed, so the shelf can be told to look again.</returns>
    IReadOnlyList<string> Read(ZipArchive container, Song song);
}
