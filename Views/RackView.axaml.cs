using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using JingleBox2.ViewModels;

namespace JingleBox2.Views;

/// <summary>
/// The instrument library and its editor: the shelf a sound starts from.
/// </summary>
/// <remarks>
/// Taking an instrument into a song copies it, and the copy is then the song's own. Editing it
/// here changes what new songs start from, not what an existing song sounds like.
/// </remarks>
public partial class RackView : UserControl
{
    /// <summary>What makes the effect's face pointable at the effect it is drawing.</summary>
    private readonly SoundDeviceRemote _remote;

    /// <summary>A box's colour mixed into the theme's. Holds nothing, so one is enough.</summary>
    private readonly Interfaces.IPanelTint _tint = new PanelTint();

    /// <summary>Builds the page. The rack and what is on it come through the data context.</summary>
    /// <remarks>
    /// An effect's face is painted in the effect's own colours, the same as a machine's panel is
    /// and for the same reason: a box on a rack looks the way it looks whatever the room around
    /// it is painted, and you know which one you are in front of before you have read anything on
    /// it. Repainted when the picked effect changes, since the colours are its own.
    ///
    /// The pointing gesture is answered here as well. Resting on a control while the link mode is
    /// on offers the effect and that parameter, which is a fact about your hardware and this
    /// effect rather than about the track it may later stand on.
    /// </remarks>
    public RackView()
    {
        InitializeComponent();

        _remote = new SoundDeviceRemote(EffectFace, () => Rack?.SelectedEffect?.Effect);

        LinkKey.Watch(EffectFace);

        AttachedToVisualTree += (_, _) => _remote.Watch();

        DetachedFromVisualTree += (_, _) => _remote.Stop();

        DataContextChanged += (_, _) => Watch();
    }

    /// <summary>The rack this page is showing, or nothing before it has one.</summary>
    private RackViewModel? Rack => DataContext as RackViewModel;


    /// <summary>Follows the picked effect, so the face is painted in that effect's colours.</summary>
    private void Watch()
    {
        if (Rack is not { } rack) return;

        rack.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not nameof(RackViewModel.SelectedEffect)) return;

            Retint();

            _remote.Show();
        };

        Retint();
    }

    /// <summary>Paints the effect's face in its own shades, or leaves it alone when there is none.</summary>
    private void Retint()
    {
        if (Rack?.SelectedEffect is { } effect) _tint.Apply(EffectPlate, effect.Theme);
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
        SoundMachineWindow.Show(ViewModel, TopLevel.GetTopLevel(this) as Window);

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
    private RackViewModel? _bound;

    /// <summary>The rack this page is showing, or nothing when it has not been given one.</summary>
    private RackViewModel? ViewModel => DataContext as RackViewModel;

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

    /// <summary>Puts another box on the machine picked, under a name nothing else has.</summary>
    private void NewFromMachine_Click(object? sender, RoutedEventArgs e)
    {
        if (ViewModel != null && MachinePicker.SelectedItem is JingleBox2.SoundDevices.SoundMachines.Records.SoundMachine machine)
            ViewModel.NewFromMachineCommand.Execute(machine);
    }

}
