
namespace JingleBox2.ViewModels.Records;

/// <summary>One of the jobs the page offers.</summary>
/// <param name="Key">Which one it is. Declared, not made up, so the page can be read.</param>
/// <param name="Name">What it is called in the list.</param>
/// <param name="Blurb">One line under the name, for somebody choosing between them.</param>
public sealed record PresetTool(string Key, string Name, string Blurb)
{
    /// <summary>Renaming a preset, and everything named after it.</summary>
    public const string Rename = "rename";

    /// <summary>Putting a set of recordings on one level.</summary>
    public const string Level = "level";

    /// <summary>The name, which is what a list with no template shows.</summary>
    public override string ToString() => Name;
}
