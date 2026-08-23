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
/// The instrument library and its editor: the shelf a sound starts from.
/// </summary>
/// <remarks>
/// Taking an instrument into a song copies it, and the copy is then the song's own. Editing it
/// here changes what new songs start from, not what an existing song sounds like.
/// </remarks>
public partial class InstrumentsView : UserControl
{
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

        // The keys are the panel's own: it listens for them wherever it is opened, so this
        // page does not have to hear them on its behalf. What is still this page's is the MIDI
        // routing above, which is about which page is up rather than about which panel is on it.
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        _onScreen = false;
        UpdateEditingFlag();

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

    /// <summary>
    /// Opens the plugin's own interface for the instrument being edited, in a window of its
    /// own. It closes itself when the instrument is left.
    /// </summary>
    private void NewFromPlugin_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel != null && PluginPicker.SelectedItem is JingleBox2.Audio.Plugins.PluginInfo plugin)
            ViewModel.NewFromPluginCommand.Execute(plugin);
    }

}
