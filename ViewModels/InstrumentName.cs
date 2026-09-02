using JingleBox2.Rack.SoundDevices.Faces.Interfaces;

namespace JingleBox2.ViewModels;

/// <inheritdoc/>
/// <remarks>
/// One line over the editor rather than the editor itself, because what a panel needs to know
/// about a name is two things and the editor knows several hundred. It holds no name of its own:
/// the instrument is where the name lives, and two of these over one editor would be two answers
/// to one question.
/// </remarks>
public sealed class InstrumentName : IInstrumentName
{
    /// <summary>The instrument being shown, which is where the name really is.</summary>
    private readonly InstrumentEditorViewModel _editor;

    /// <summary>What one instrument is called, for the badge on its machine's face.</summary>
    /// <param name="editor">The instrument being shown.</param>
    public InstrumentName(InstrumentEditorViewModel editor) => _editor = editor;

    /// <inheritdoc/>
    public string Said
    {
        get => _editor.Name;
        set => _editor.Name = value;
    }

    /// <inheritdoc/>
    public bool Fixed => !_editor.CanRename;
}
