using Avalonia.Controls;
using Avalonia.Interactivity;
using JingleBox2.ViewModels;

namespace JingleBox2.Views;

/// <summary>
/// The plugin chain control. Its behaviour is in PluginChainViewModel, so the same strip works
/// for a pad, a tracker track, or anything else that grows one later. Opening a window is the
/// one thing that has to happen here: windows are a view's business.
/// </summary>
public partial class PluginStrip : UserControl
{
    /// <summary>
    /// The chain currently shown, kept only so its announcement can be let go of when the strip
    /// is pointed at another one. A strip is reused as the cursor moves between tracks, so
    /// without this it would be subscribed to every chain it had ever shown.
    /// </summary>
    private PluginChainViewModel? _chain;

    /// <summary>
    /// Builds the strip and keeps the plugin windows in step with what is on the chain.
    /// </summary>
    /// <remarks>
    /// A device that leaves the chain takes its window with it, wherever the removal came from:
    /// the strip's menu, a song being opened, or a pad profile changing. A window left open
    /// over a disposed plugin draws into nothing, which is a crash inside the plugin's own
    /// toolkit rather than an exception anything here could catch.
    /// </remarks>
    public PluginStrip()
    {
        InitializeComponent();

        DataContextChanged += (_, _) =>
        {
            if (_chain != null) _chain.DeviceClosing -= PluginWindow.CloseFor;

            _chain = DataContext as PluginChainViewModel;

            if (_chain != null) _chain.DeviceClosing += PluginWindow.CloseFor;
        };
    }

    /// <summary>
    /// Opens the plugin the track plays, in the same kind of window an effect gets.
    /// </summary>
    /// <remarks>
    /// The plugin is loaded here rather than when the track was picked: a track selection
    /// should not cost the time a big synth takes to come up. It is the one the notes go to,
    /// so a knob turned in it changes what is actually heard.
    ///
    /// An instrument of ours opens the designer's panel; a plugin opens its own interface. The
    /// window is the same window either way, because to the track they are the same thing.
    /// </remarks>
    private void OnOpenInstrument(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PluginChainViewModel chain) return;

        var instrument = chain.Instrument;
        if (instrument == null) return;

        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        if (instrument.MachineMissing)
        {
            _ = Missing(instrument);
            return;
        }

        if (!instrument.IsPlugin)
        {
            var designer = instrument.Designer;
            if (designer == null) return;

            instrument.IsOpen = true;

            InstrumentWindow.Show(instrument, designer, owner, () => instrument.IsOpen = false);
            return;
        }

        var panel = instrument.Prepare();
        if (panel == null) return;

        instrument.IsOpen = true;

        PluginWindow.Show(instrument, panel, instrument.Title, owner, () => instrument.Close());
    }

    /// <summary>
    /// Says why an instrument will not open, which is that its machine is not here.
    /// </summary>
    /// <remarks>
    /// Said when it is asked for and nowhere else. An instrument whose machine is missing is a
    /// row in a song like any other until somebody tries to use it, and that is the moment the
    /// answer is wanted: told on the way in, while opening a song, it is a dialog about
    /// something nobody had asked about yet and is gone by the time it matters.
    ///
    /// The window does not open behind it. There is no panel to draw, so what would open is an
    /// empty frame with a keyboard that cannot sound a note, which reads as a machine that is
    /// broken rather than one that is absent.
    ///
    /// The machine is labelled in the heading because an instrument takes its machine's name
    /// unless somebody renames it, so the two are the same word more often than not and
    /// "Ouroboros is not registered" leaves somebody wondering which of the two is meant. The
    /// body then names the instrument and says "on it", which needs no second label.
    ///
    /// It points at the registry and stops there. It used to spell out what somebody would find
    /// when they arrived, that the machine is either waiting to be added or not present at all
    /// and needs its zip imported, which is a paragraph describing a page they have not opened.
    /// That page says it better, and says it while they are looking at it.
    ///
    /// Register rather than install, and that is the whole instruction: registering is one page
    /// to go and look at, where installing asks somebody to know which of those two cases they
    /// are in before they have.
    ///
    /// Not awaited by the caller: it is an event handler, and the dialog owns itself once it is
    /// up. What it is waiting on is somebody pressing OK.
    /// </remarks>
    /// <param name="instrument">The instrument whose machine has gone.</param>
    private static async System.Threading.Tasks.Task Missing(PluginInstrumentViewModel instrument)
    {
        string machine = instrument.Missing?.Name ?? instrument.Instrument.Machine.Name;

        await ConfirmDialog.ErrorAsync(
            "Machine not registered",
            machine + "(machine) is not registered",
            "'" + instrument.Instrument.Name + "' is on it, so it has no panel and makes no "
            + "sound. Check the machine registry under SETTINGS, System.");
    }

    /// <summary>
    /// Opens the effect whose block was pressed.
    /// </summary>
    /// <remarks>
    /// Read off the button's own row rather than off anything the strip has picked, since a
    /// chain has no selection: a press on a block is about that block.
    /// </remarks>
    private void OnOpenDevice(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control) return;
        if (control.DataContext is not PluginDeviceViewModel device) return;
        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        PluginWindow.Show(device, owner);
    }
}
