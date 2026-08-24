namespace JingleBox2.Tracker;

/// <summary>
/// A saved song on disk. Name is what the user sees, path is what the loader needs, and the
/// description is what the song says about itself, for telling two of them apart in a list.
/// </summary>
public sealed record SongFile(string Name, string Path, string Description = "")
{
    /// <summary>True when there is something to show under the name.</summary>
    public bool HasDescription => Description.Length > 0;

    public override string ToString() => Name;
}
