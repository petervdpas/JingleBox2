using System.Threading.Tasks;
using JingleBox2.ViewModels.Interfaces;
using JingleBox2.Views;

namespace JingleBox2.ViewModels;

/// <inheritdoc/>
/// <remarks>The application's own dialogs, over whichever window is in front.</remarks>
public sealed class PresetQuestions : IPresetQuestions
{
    /// <inheritdoc/>
    public Task<string?> Name(string machine, string suggested) =>
        NameDialog.AskAsync("Save as preset", "What should this " + machine + " preset be called?", suggested, "Save");

    /// <inheritdoc/>
    public Task<bool> Replace(string name) =>
        ConfirmDialog.AskAsync("Replace preset", "You already have a preset called '" + name + "'. Replace it with this sound?", "Replace");

    /// <inheritdoc/>
    public Task<bool> Delete(string name) =>
        ConfirmDialog.AskAsync("Delete preset", "Delete your preset '" + name + "'? The sound on this instrument stays as it is.", "Delete");

    /// <inheritdoc/>
    public Task Refused(string why) => ConfirmDialog.NoteAsync("Save as preset", why);
}
