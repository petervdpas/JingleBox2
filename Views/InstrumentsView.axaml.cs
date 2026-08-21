using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using JingleBox2.Models;
using JingleBox2.Tracker;
using JingleBox2.ViewModels;

namespace JingleBox2.Views;

/// <summary>
/// The instrument library and its editor. Instruments live here rather than in a song, so the
/// same voice plays in all of them.
/// </summary>
public partial class InstrumentsView : UserControl
{
    private TopLevel? _keySource;

    public InstrumentsView()
    {
        InitializeComponent();
    }

    private bool _onScreen;
    private InstrumentLibraryViewModel? _bound;

    private InstrumentLibraryViewModel? ViewModel => DataContext as InstrumentLibraryViewModel;

    /// <summary>
    /// While this page is up, notes from the MIDI keyboard audition the instrument being
    /// edited instead of landing in the pattern.
    /// </summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _onScreen = true;
        UpdateEditingFlag();

        // The handler goes on the window, not on this control: a key press only tunnels through
        // the controls between the root and whatever has focus, and after clicking a combo box
        // or a knob that route does not have to come past here.
        _keySource = TopLevel.GetTopLevel(this);
        _keySource?.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _onScreen = false;
        UpdateEditingFlag();

        _keySource?.RemoveHandler(KeyDownEvent, OnKeyDown);
        _keySource = null;
    }

    /// <summary>
    /// The data context can arrive before or after the view goes on screen, so the flag is set
    /// from both. Getting this wrong is silent: notes go to the pattern and nothing sounds.
    /// </summary>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        UpdateEditingFlag();
    }

    private void UpdateEditingFlag()
    {
        var current = ViewModel;

        // A view model this page has let go of must not stay armed.
        if (!ReferenceEquals(_bound, current) && _bound != null) _bound.IsEditing = false;

        _bound = current;
        if (current != null) current.IsEditing = _onScreen;
    }

    private void NewFromRecording_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel != null && RecordingPicker.SelectedItem is Recording recording)
            ViewModel.NewFromRecordingCommand.Execute(recording);
    }

    /// <summary>
    /// Opens the plugin's own interface for the instrument being edited, in a window of its
    /// own. It closes itself when the instrument is left.
    /// </summary>
    private void OpenPluginWindow_Click(object? sender, RoutedEventArgs e)
    {
        var editor = ViewModel?.Editor;
        if (editor?.PluginPanel == null) return;

        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        // Held in a field so the same handler can be taken off again. A local function makes
        // a new delegate each time and would pile up instead.
        if (_openEditor != null) _openEditor.Closing -= CloseOpenEditor;

        _openEditor = editor;
        editor.Closing += CloseOpenEditor;

        PluginWindow.Show(editor, editor.PluginPanel, editor.PluginText, owner);
    }

    /// <summary>The instrument whose plugin window this page opened, if any.</summary>
    private InstrumentEditorViewModel? _openEditor;

    private void CloseOpenEditor()
    {
        var editor = _openEditor;
        if (editor == null) return;

        editor.Closing -= CloseOpenEditor;
        _openEditor = null;

        PluginWindow.CloseFor(editor);
    }

    private void NewFromPlugin_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel != null && PluginPicker.SelectedItem is JingleBox2.Audio.Plugins.PluginInfo plugin)
            ViewModel.NewFromPluginCommand.Execute(plugin);
    }

    /// <summary>
    /// The tracker's piano layout, auditioning rather than writing: a knob is easier to judge
    /// while you are playing than one Test button at a time.
    /// </summary>
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var vm = ViewModel;
        if (vm == null || !_onScreen) return;

        // Typing a name is typing a name, not playing a tune.
        if (e.Source is TextBox) return;
        if (_keySource?.FocusManager?.GetFocusedElement() is TextBox) return;
        if (e.KeyModifiers != KeyModifiers.None) return;

        if (KeyboardNoteMap.NoteFor(e.Key.ToString(), vm.Octave) is not Note note) return;

        vm.PlayNote(note);
        e.Handled = true;
    }
}
