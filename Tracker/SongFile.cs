namespace JingleBox2.Tracker;

/// <summary>A saved song on disk. Name is what the user sees, path is what the loader needs.</summary>
public sealed record SongFile(string Name, string Path)
{
    public override string ToString() => Name;
}
