using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia;
using JingleBox2.Tracker;
using JingleBox2.ViewModels;
using System;

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

    private IInstrumentDesigner? Designer => DataContext as IInstrumentDesigner;

    /// <summary>Whatever window this panel is in, since that is where the keys arrive.</summary>
    private TopLevel? _keySource;

    /// <summary>
    /// The panel listens for the tracker's piano layout so it can be played where it stands.
    /// </summary>
    /// <remarks>
    /// The handler goes on the window rather than on this control: a key press only tunnels
    /// through the controls between the root and whatever has focus, and after clicking a knob
    /// or a combo box that route does not have to come past here.
    ///
    /// One window, one panel, so the INSTRUMENTS tab and an instrument's own window each get
    /// their own and neither hears the other's keys.
    /// </remarks>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _keySource = TopLevel.GetTopLevel(this);
        _keySource?.AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _keySource?.RemoveHandler(KeyDownEvent, OnKeyDown);
        _keySource = null;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        var designer = Designer;

        // A panel on a tab nobody is looking at must not answer for the one they are.
        if (designer?.Editor == null || !IsEffectivelyVisible) return;

        // Typing a name is typing a name, not playing a tune.
        if (e.Source is TextBox) return;
        if (_keySource?.FocusManager?.GetFocusedElement() is TextBox) return;
        if (e.KeyModifiers != KeyModifiers.None) return;

        if (KeyboardNoteMap.NoteFor(e.Key.ToString(), designer.Octave) is not Note note) return;

        designer.Play(note, TrackerCell.NoVolume);
        e.Handled = true;
    }

    /// <summary>
    /// Puts one of your recordings on the pad in hand.
    /// </summary>
    /// <remarks>
    /// The picker belongs to the window, so it is opened here and only the answer goes to the
    /// view model, the same way the plugin folder is chosen in SETTINGS.
    /// </remarks>
    private async void LoadPadSample_Click(object? sender, RoutedEventArgs e)
    {
        var pad = Designer?.Editor?.Kit?.Selected;
        if (pad == null) return;

        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage == null) return;

        var picked = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "A recording for " + pad.CapText,
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Recordings")
                {
                    Patterns = new[] { "*.wav", "*.mp3", "*.ogg", "*.flac" }
                }
            }
        });

        if (picked.Count == 0) return;

        string? path = picked[0].TryGetLocalPath();
        if (!string.IsNullOrWhiteSpace(path)) pad.Take(path);
    }

    /// <summary>Takes the recording off the pad in hand, leaving the pad where it is.</summary>
    private void ClearPadSample_Click(object? sender, RoutedEventArgs e) =>
        Designer?.Editor?.Kit?.Selected?.Take(null);

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
