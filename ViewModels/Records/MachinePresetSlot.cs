
namespace JingleBox2.ViewModels.Records;

/// <summary>
/// One preset in the machine being built: the file, and what it is called.
/// </summary>
/// <remarks>
/// The name on the picker comes from inside the file, and the file's own name only decides the
/// order they are offered in. Both are shown, because a folder of presets is a folder somebody
/// is going to open.
/// </remarks>
/// <param name="Name">What it calls itself inside the file, which is what the picker shows.</param>
/// <param name="Path">Where the file is, whose own name decides the order they are offered in.</param>
public sealed record MachinePresetSlot(string Name, string Path)
{
    /// <summary>Just the file, since the folder is already known wherever this is shown.</summary>
    public string FileName => System.IO.Path.GetFileName(Path);

    /// <summary>The name, which is what a picker with no template shows.</summary>
    public override string ToString() => Name;
}
