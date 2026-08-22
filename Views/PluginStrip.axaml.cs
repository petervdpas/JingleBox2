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
    private PluginChainViewModel? _chain;

    public PluginStrip()
    {
        InitializeComponent();

        // A device that leaves the chain takes its window with it, wherever the removal came
        // from: the strip's menu, a song being opened, or a pad profile changing.
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
    /// </remarks>
    private void OnOpenInstrument(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not PluginChainViewModel chain) return;

        var instrument = chain.Instrument;
        if (instrument == null) return;

        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        // An instrument of ours opens the designer; a plugin opens its own interface. The box
        // is the same box either way, because to the track they are the same thing.
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

    private void OnOpenDevice(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control) return;
        if (control.DataContext is not PluginDeviceViewModel device) return;
        if (TopLevel.GetTopLevel(this) is not Window owner) return;

        PluginWindow.Show(device, owner);
    }
}
