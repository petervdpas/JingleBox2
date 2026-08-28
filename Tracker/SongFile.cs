namespace JingleBox2.Tracker;

/// <summary>
/// A saved song on disk. Name is what the user sees, path is what the loader needs, and the
/// description is what the song says about itself, for telling two of them apart in a list.
/// </summary>
/// <param name="Name">The file's own name without its extension, which is what a picker shows.</param>
/// <param name="Path">Where the file is, which is all the loader needs.</param>
/// <param name="Description">
/// What the song says about itself, read out of the file rather than remembered anywhere else,
/// so a song written on another machine still describes itself here.
/// </param>
public sealed record SongFile(string Name, string Path, string Description = "")
{
    /// <summary>True when there is something to show under the name.</summary>
    public bool HasDescription => Description.Length > 0;

    /// <summary>The name alone, since a list of these is bound straight to a picker.</summary>
    public override string ToString() => Name;
}
