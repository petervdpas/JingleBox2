using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using JingleBox2.ViewModels;

namespace JingleBox2.Views;

/// <summary>
/// The instrument designer: everything about one instrument's sound, and nothing about which
/// instrument it is.
/// </summary>
/// <remarks>
/// Its own control rather than a column of the INSTRUMENTS tab, because the same designer is
/// wanted in two places: on that tab, against whatever the library has picked, and in a window
/// of its own, against the instrument a track is playing. A machine's front panel does not care
/// which rack it is standing in.
/// </remarks>
public partial class InstrumentEditor : UserControl
{
    public InstrumentEditor()
    {
        InitializeComponent();
    }

    private InstrumentEditorViewModel? Editor => (DataContext as IInstrumentDesigner)?.Editor;

    /// <summary>The plugin instrument this stands for, opened in the same window a chain uses.</summary>
    private void OpenPluginWindow_Click(object? sender, RoutedEventArgs e)
    {
        var editor = Editor;
        if (editor?.PluginPanel == null) return;

        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        // Held so the same handler can be taken off again. A local function would make a new
        // delegate every time and pile them up.
        if (_openEditor != null) _openEditor.Closing -= CloseOpenEditor;

        _openEditor = editor;
        editor.Closing += CloseOpenEditor;

        PluginWindow.Show(editor, editor.PluginPanel, editor.PluginText, owner);
    }

    /// <summary>The instrument whose plugin window this designer opened, if any.</summary>
    private InstrumentEditorViewModel? _openEditor;

    private void CloseOpenEditor()
    {
        var editor = _openEditor;
        if (editor == null) return;

        editor.Closing -= CloseOpenEditor;
        _openEditor = null;

        PluginWindow.CloseFor(editor);
    }
}
