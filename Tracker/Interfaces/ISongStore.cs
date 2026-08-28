using System.Collections.Generic;
using JingleBox2.Tracker;

namespace JingleBox2.Tracker.Interfaces;

/// <summary>
/// Reads and writes songs, one file per song, alongside the recordings.
/// </summary>
/// <remarks>
/// Separate files rather than entries in config.json: a song is a document the user names,
/// copies, and can hand to someone else, and a pad refers to one by path.
///
/// A song file is a zip, and what is in it is this:
/// <code>
/// song.json        the patterns, the order, the mix, the instruments
/// state/00.bin     what a plugin instrument saved, as the plugin handed it over
/// state/t00-00.bin the same for one effect on one track's chain
/// state/m00.bin    and for one on the master, which is a strip without being a track
/// samples/...      the recordings, but only in a song that was packed
/// </code>
///
/// One file with the patches inside it was the obvious thing and it was the wrong thing. A
/// plugin's state is the bulk of a song by a wide margin: of one song here, 348 KB, the music
/// is 781 bytes and one synth's patch is 331 KB of it, base64, which is a third larger than the
/// bytes it stands for and has to be encoded on the way out and decoded on the way in. Worse,
/// it was in the same document as the patterns, and a document is all or nothing: a patch that
/// came back damaged from a plugin did not cost the patch, it cost the song.
///
/// Kept apart, a patch is read as the bytes it is, straight into the plugin that wants it, and
/// a patch that will not read costs that instrument its sound and nothing else. song.json stays
/// small enough to read in a text editor and to parse before anything heavy is touched, which
/// is what lets the plugins a song needs be started while the rest of it is still loading.
///
/// It is also one of the two places asked before a recording is deleted, which is why it is an
/// <see cref="ISampleUsage"/>: a song owns its instruments, so a take nothing on the rack plays
/// can still be the sound of three songs.
/// </remarks>
public interface ISongStore : ISampleUsage
{
    /// <summary>Where songs are kept, which is made if it is not there.</summary>
    string SongsDirectory { get; }

    /// <summary>Where a song of that name would live.</summary>
    string PathFor(string songName);

    /// <summary>Every saved song's path, in name order. Empty when the folder is not there.</summary>
    IReadOnlyList<string> List();

    /// <summary>Saved songs as name, path and what they say about themselves, for a picker.</summary>
    /// <remarks>
    /// The description is read out of each file rather than remembered anywhere else, so the one
    /// a list shows is the one the song carries even when it was written on another machine or
    /// edited by hand. A song that will not parse simply has nothing to say: this is a list to
    /// read, not the load that has to report a broken file.
    /// </remarks>
    IReadOnlyList<SongFile> ListSongs();

    /// <summary>Whether a song of that name is already there.</summary>
    bool Exists(string songName);

    /// <summary>Removes a song file. Silent when it is already gone.</summary>
    void Delete(string filePath);

    /// <summary>
    /// Writes a song, and when asked, the recordings it plays along with it.
    /// </summary>
    /// <remarks>
    /// Packing is the deliberate act, not the default. An ordinary save names its recordings,
    /// which is what keeps it in milliseconds and what keeps the twenty second keep from
    /// writing tens of megabytes behind your back. See <see cref="SongSamples"/> for what
    /// travels and what is left named.
    ///
    /// The song is normalised on the way out, so what is written is always something that will
    /// open, whatever state it was in while somebody was working on it.
    /// </remarks>
    void Save(Song song, string filePath, bool withSamples = false);

    /// <summary>Loads a song, or null when the file is missing or not a song.</summary>
    Song? Load(string filePath);

    /// <summary>
    /// The same, saying what recordings the song brought with it.
    /// </summary>
    /// <remarks>
    /// A packed song puts its recordings on the shelf as it opens, so the shelf has to be told
    /// to look again. What comes back is what was not already there: opening the same packed
    /// song twice adds nothing the second time.
    /// </remarks>
    Song? Load(string filePath, out IReadOnlyList<string> arrived);
}
