namespace JingleBox2.ViewModels.Interfaces;

/// <summary>
/// Opens RECORD's wave editor on one file.
/// </summary>
/// <remarks>
/// One editor for every wave in the application: the take on RECORD's shelf and a pad's wave kept
/// in a preset of yours are both a file somebody wants to trim, fade or normalise, and a second
/// editor would be the same tools written twice. What is edited is a working copy until Save, and
/// saving tells everything holding the file in memory to read it again, so a pad plays the edit at
/// once.
///
/// A file that is not a take on the shelf is edited without its name and category, since renaming
/// a pad's wave would be moving a file its preset names.
/// </remarks>
public interface IWaveEditing
{
    /// <summary>Opens the editor on that file.</summary>
    /// <param name="path">The wave to edit.</param>
    /// <param name="name">What to call it in the editor.</param>
    /// <returns>Done when the editor is closed, so whoever opened it can draw the wave as it now is.</returns>
    System.Threading.Tasks.Task Edit(string path, string name);
}
