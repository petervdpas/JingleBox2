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
