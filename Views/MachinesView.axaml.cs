using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using JingleBox2.Models;
using JingleBox2.Tracker;
using JingleBox2.ViewModels;
using JingleBox2.Audio.Plugins.Records;

namespace JingleBox2.Views;

/// <summary>
/// The instrument library and its editor: the shelf a sound starts from.
/// </summary>
/// <remarks>
/// Taking an instrument into a song copies it, and the copy is then the song's own. Editing it
/// here changes what new songs start from, not what an existing song sounds like.
/// </remarks>
public partial class MachinesView : UserControl
{
    /// <summary>Builds the page. The rack and its machines come through the data context.</summary>
    public MachinesView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Opens the machine in hand, in its own window.
    /// </summary>
    /// <remarks>
    /// The rack is a list of what you have; a machine is a panel full of knobs. Standing the
    /// second inside the first left both cramped, so the panel opens in a window that can be
    /// made as big as it wants and left up while you write a pattern.
    /// </remarks>
    private void OpenMachine(object? sender, RoutedEventArgs e) =>
        MachineWindow.Show(ViewModel, TopLevel.GetTopLevel(this) as Window);

    /// <summary>
    /// Whether the page is up. Half of what decides where a played note goes, the other half
    /// being whether there is a rack to send it to at all.
    /// </summary>
    private bool _onScreen;

    /// <summary>
    /// The rack this page last armed, kept so it can be disarmed when the page is pointed at
    /// another one. Without it a rack the page has let go of would stay armed and would go on
    /// taking notes meant for the pattern.
    /// </summary>
    private MachineRackViewModel? _bound;

    /// <summary>The rack this page is showing, or nothing when it has not been given one.</summary>
    private MachineRackViewModel? ViewModel => DataContext as MachineRackViewModel;

    /// <summary>
    /// While this page is up, notes from the MIDI keyboard audition the instrument being
    /// edited instead of landing in the pattern.
    /// </summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _onScreen = true;
        UpdateEditingFlag();
    }

    /// <summary>
    /// Leaving the page hands the MIDI keyboard back, so notes land in the pattern again.
    /// </summary>
    /// <remarks>
    /// The keys typed on the computer keyboard are the panel's own: it listens for them
    /// wherever it is opened, so this page does not have to hear them on its behalf. What is
    /// still this page's is the MIDI routing, which is about which page is up rather than about
    /// which panel is on it.
    /// </remarks>
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

    /// <summary>
    /// Arms the rack the page is showing, and disarms whichever one it was showing before.
    /// </summary>
    /// <remarks>
    /// A rack this page has let go of must not stay armed, or two racks would both believe they
    /// are being edited and a played note would sound twice.
    /// </remarks>
    private void UpdateEditingFlag()
    {
        var current = ViewModel;

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
        if (ViewModel != null && PluginPicker.SelectedItem is JingleBox2.Audio.Plugins.Records.PluginInfo plugin)
            ViewModel.NewFromPluginCommand.Execute(plugin);
    }

}
