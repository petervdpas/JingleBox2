using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Avalonia;
using JingleBox2.Audio;
using JingleBox2.Models;
using JingleBox2.Tracker;
using JingleBox2.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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
    /// One of your own takes, straight onto the pad in hand.
    /// </summary>
    /// <remarks>
    /// The picker is cleared afterwards so it reads as an action rather than a setting: what
    /// is on the pad is written under it, and a box still showing the last thing you put there
    /// would be claiming to be the pad's own.
    /// </remarks>
    private void PadRecording_Changed(object? sender, SelectionChangedEventArgs e) =>
        Took(sender, path => Designer?.Editor?.Kit?.Selected?.Take(path));

    /// <summary>One of your own takes, straight onto the zone in hand.</summary>
    private void ZoneRecording_Changed(object? sender, SelectionChangedEventArgs e) =>
        Took(sender, path => Designer?.Editor?.Zones?.Selected?.Take(path));

    /// <summary>Hands a picked take's path on, then puts the picker back to empty.</summary>
    private void Took(object? sender, Action<string> onto)
    {
        if (sender is not ComboBox picker) return;

        if (picker.SelectedItem is Recording recording && recording.FilePath.Length > 0)
            onto(recording.FilePath);

        // Cleared without running this again: a null selection has nothing to put anywhere.
        picker.SelectedItem = null;
    }

    /// <summary>
    /// Brings samples in from the disc and fills the pads with them.
    /// </summary>
    /// <remarks>
    /// Many at once, because a kit is a folder of hits rather than one file. They are copied
    /// into JingleBox on the way, so from here on the machine is playing its own.
    /// </remarks>
    private async void ImportPads_Click(object? sender, RoutedEventArgs e)
    {
        var editor = Designer?.Editor;
        if (editor?.Kit == null) return;

        var found = await AskFiles("Samples to load onto the pads");
        if (found.Count == 0) return;

        editor.Kit.Fill(editor.Import(found));
    }

    /// <summary>Brings samples in from the disc and builds the whole map from them.</summary>
    private async void ImportZones_Click(object? sender, RoutedEventArgs e)
    {
        var editor = Designer?.Editor;
        if (editor?.Zones == null) return;

        var found = await AskFiles("Samples to load onto the keyboard");
        if (found.Count == 0) return;

        editor.Zones.Fill(editor.Import(found));
    }

    /// <summary>
    /// Asks for any number of samples, sorted by name.
    /// </summary>
    /// <remarks>
    /// Sorted the way the folder would be read, since a set of samples is nearly always named
    /// so that it sorts, and that order is very often the order across the pads or up the
    /// keyboard. The picker belongs to the window, so it is opened here and only the answer
    /// goes to the view model.
    /// </remarks>
    private async System.Threading.Tasks.Task<IReadOnlyList<string>> AskFiles(string title)
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage == null) return Array.Empty<string>();

        var picked = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = true,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Samples")
                {
                    Patterns = RecordingImport.Kinds.Select(k => "*" + k).ToArray()
                }
            }
        });

        return picked
            .Select(f => f.TryGetLocalPath())
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>Takes the recording off the zone in hand, leaving the zone where it is.</summary>
    private void ClearZoneSample_Click(object? sender, RoutedEventArgs e) =>
        Designer?.Editor?.Zones?.Selected?.Take(null);

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
