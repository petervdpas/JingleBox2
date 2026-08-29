using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Audio.Plugins;
using System.Collections.ObjectModel;
using JingleBox2.Audio.Plugins.Interfaces;
using JingleBox2.ViewModels.Records;

namespace JingleBox2.ViewModels;

/// <summary>
/// One box in a chain: a loaded plugin, its knobs, and whether it is switched on.
/// </summary>
/// <remarks>
/// A row rather than a thing: the plugin and its place in the chain both belong to somebody
/// else, and this holds neither. What it does own is the panel, which is built here so that
/// closing a window and opening it again shows the same one.
/// </remarks>
public sealed partial class PluginDeviceViewModel : ObservableObject
{
    /// <summary>The chain this box is in, which is what a bypass or a move has to be told.</summary>
    private readonly PluginChainViewModel _chain;

    /// <summary>
    /// Makes a row for a plugin that is already loaded and already in the chain.
    /// </summary>
    /// <param name="chain">The chain this row is in, which is what a bypass or a move is asked of.</param>
    /// <param name="effect">The loaded plugin, in its own process unless that was switched off.</param>
    /// <param name="device">Its place in the chain, which is what carries the bypass.</param>
    public PluginDeviceViewModel(PluginChainViewModel chain, IPluginEffect effect, PluginChain.Device device)
    {
        _chain = chain;
        Effect = effect;
        Device = device;

        Panel = new PluginControlsViewModel(effect, chain.NotifyChanged);
    }

    /// <summary>This plugin's knobs. The panel is shared with the instrument editor.</summary>
    public PluginControlsViewModel Panel { get; }

    /// <summary>The plugin itself, for the few things that have to talk to it directly.</summary>
    public IPluginEffect Effect { get; }

    /// <summary>Its place in the chain, which is what says whether it is bypassed.</summary>
    public PluginChain.Device Device { get; }

    /// <summary>What the plugin calls itself, which is the name on the block.</summary>
    public string Name => Effect.Info.Name;

    /// <summary>VST3 or CLAP, for the corner of the block.</summary>
    /// <remarks>
    /// Worth printing because two plugins of the same name in two standards are two different
    /// pieces of software, and because it is the one thing about a plugin that is certain
    /// before it has been loaded.
    /// </remarks>
    public string Format => Effect.Info.FormatName;

    /// <summary>Who made it, when they said.</summary>
    public string Vendor => Effect.Info.Vendor ?? "";

    /// <summary>
    /// The first few of its controls, printed on the block itself.
    /// </summary>
    /// <remarks>
    /// The point of a chain you can read: what a device is set to, without opening it. Renoise
    /// and Bitwig both put the controls in the chain rather than behind it, and the reason is
    /// that a chain of four boxes with names on them tells you the order of the effects and
    /// nothing whatever about the sound.
    ///
    /// The first few and not all of them, because a big plugin declares thousands: Serum has
    /// 2622. Which few is the plugin's own answer, since the order it declares them in is the
    /// order it thinks they matter, and a host that reordered them would be guessing. Anything
    /// the plugin hides, its own bypass, and anything that cannot be moved are all out already,
    /// which is done where the panel is built.
    ///
    /// Worked out once and kept until <see cref="Reread"/> says otherwise, because reading a
    /// value off a plugin in another process is a synchronous round trip and this is drawn on
    /// every block of every chain.
    /// </remarks>
    public System.Collections.Generic.IReadOnlyList<DeviceReading> Summary => _summary ??= Pick();

    /// <summary>What was last read off the plugin, or null when it has to be asked again.</summary>
    private System.Collections.Generic.List<DeviceReading>? _summary;

    /// <summary>
    /// Asked of the plugin itself rather than of its panel.
    /// </summary>
    /// <remarks>
    /// The panel is built the first time somebody opens the window, and a device nobody has
    /// opened would otherwise have nothing to print, which is exactly the device you most want
    /// to read. So this goes straight to the plugin: for one in another process that list is a
    /// round trip, and Serum answers with 2622 of them, so it is asked for once and the few that
    /// are printed are kept.
    ///
    /// Each value is worded through <see cref="PluginParameterViewModel"/>, the same wrapper the
    /// plugin's own window uses, and then thrown away. How a value is worded is real work: a
    /// VST3 parameter is nought to one whatever it means, so the plugin's own words are all
    /// there is, and the many that hand back "50.000000" have to be cut down and given the unit
    /// the plugin declared separately. Doing that again here is how two places come to word one
    /// value differently, and it did: the strip printed 0.5000 where the window printed 0.5.
    ///
    /// A plugin that has stopped answering leaves the block saying what it is and nothing else,
    /// which is better than a chain strip that throws while it is being drawn.
    /// </remarks>
    private System.Collections.Generic.List<DeviceReading> Pick()
    {
        var found = new System.Collections.Generic.List<DeviceReading>(Shown);

        try
        {
            foreach (var parameter in Wanted())
            {
                var reading = new PluginParameterViewModel(Effect, parameter);

                found.Add(new DeviceReading(reading.Name, reading.Text));
            }
        }
        catch (System.Exception)
        {
        }

        return found;
    }

    /// <summary>The few worth printing, in the order the plugin declares them.</summary>
    /// <remarks>
    /// Which few is the plugin's own answer: the order it lists them in is the order it thinks
    /// they matter, and a host that reordered them would be guessing. Hidden is the plugin
    /// saying not to show it, and its own bypass is something the host offers in its own way,
    /// on the block already.
    /// </remarks>
    private System.Collections.Generic.IEnumerable<JingleBox2.Audio.Plugins.Records.PluginParameter> Wanted()
    {
        int taken = 0;

        foreach (var parameter in Effect.Parameters())
        {
            if (parameter.IsHidden || parameter.IsBypass) continue;

            yield return parameter;

            if (++taken >= Shown) break;
        }
    }

    /// <summary>How many fit on a block without it becoming a panel of its own.</summary>
    private const int Shown = 3;

    /// <summary>Reads the printed values again, for when something else moved them.</summary>
    /// <remarks>
    /// Not a poll. The chain says when something is known to have moved, and only then is the
    /// plugin asked again, because <c>ValueOf</c> on a plugin in another process is a
    /// synchronous round trip and three of those per device per tick is a cost nobody asked for.
    /// A block nobody has drawn yet is left alone: there is nothing kept to be out of date.
    /// </remarks>
    public void Reread()
    {
        if (_summary is null) return;

        _summary = null;

        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(HasSummary));
    }

    /// <summary>True when there is anything to print under the name.</summary>
    public bool HasSummary => Summary.Count > 0;

    /// <summary>The readings, which is all a poll needs to touch.</summary>
    public ObservableCollection<PluginParameterViewModel> Readouts => Panel.Readouts;

    /// <summary>True when the panel has anything to draw, which a plugin that failed has not.</summary>
    public bool HasParameters => Panel.HasParameters;

    /// <summary>True while this one's window is open, so the box in the strip says so.</summary>
    /// <remarks>
    /// Also what the chain's poll goes by: a plugin nobody is looking at has its readings left
    /// alone.
    /// </remarks>
    [ObservableProperty] private bool isOpen;

    /// <summary>Switched off but still in the chain, so it can be switched back on.</summary>
    /// <remarks>
    /// The flag lives on the chain's own device rather than here, because it is the audio that
    /// has to honour it; this only carries the tick box to it and tells the chain there is
    /// something new to save.
    /// </remarks>
    public bool IsBypassed
    {
        get => Device.Bypassed;
        set
        {
            if (Device.Bypassed == value) return;

            Device.Bypassed = value;
            OnPropertyChanged();
            _chain.NotifyChanged();
        }
    }

    /// <summary>Takes this device out of the chain, which is the red cross on the block.</summary>
    /// <remarks>Always enabled: a device in a chain can always come out of it.</remarks>
    public IRelayCommand RemoveCommand => new RelayCommand(() => _chain.Remove(this));

    /// <summary>Moves it one place earlier in the chain, so it hears the audio sooner.</summary>
    /// <remarks>
    /// Always enabled, and refused by the chain at the front rather than greyed out here: the
    /// command would have to be told to re-evaluate every time anything in the chain moved.
    /// </remarks>
    public IRelayCommand MoveLeftCommand => new RelayCommand(() => _chain.Move(this, -1));

    /// <summary>And one place later, on the same terms.</summary>
    public IRelayCommand MoveRightCommand => new RelayCommand(() => _chain.Move(this, 1));
}
