using CommunityToolkit.Mvvm.ComponentModel;

namespace JingleBox2.ViewModels;

/// <summary>
/// One plugin in a window of its own: what is inside it, what it is called, and the one
/// control that is the host's rather than the plugin's.
/// </summary>
/// <remarks>
/// A plugin in a chain can be switched off without being taken out, and that button belongs to
/// the host because the chain does. A plugin being an instrument has no chain and no bypass, so
/// the window it opens in has a title and nothing else.
/// </remarks>
public sealed class PluginWindowViewModel : ObservableObject
{
    /// <summary>
    /// The box in the chain this window is over, or null when the plugin is an instrument.
    /// </summary>
    /// <remarks>
    /// The only reason to hold it is the bypass, which is why null is an ordinary state here
    /// rather than something to guard against.
    /// </remarks>
    private readonly PluginSlotViewModel? _device;

    /// <summary>
    /// Makes the window's contents around a panel that is already built.
    /// </summary>
    /// <param name="panel">The controls already built for the plugin, which the window only frames.</param>
    /// <param name="name">
    /// What the title bar says. Passed in rather than read off the plugin, because a track's
    /// instrument is named by the person who put it there and the plugin's own name is only
    /// part of that.
    /// </param>
    /// <param name="device">The chain box, when there is one, which is what carries the bypass.</param>
    public PluginWindowViewModel(PluginControlsViewModel panel, string name, PluginSlotViewModel? device = null)
    {
        Panel = panel;
        Name = name;
        _device = device;
    }

    /// <summary>The plugin's controls: its own interface if it has one, our knobs if not.</summary>
    public PluginControlsViewModel Panel { get; }

    /// <summary>What the title bar says.</summary>
    public string Name { get; }

    /// <summary>Only a plugin in a chain has one, because only a chain has somewhere to be off.</summary>
    public bool HasBypass => _device != null;

    /// <summary>
    /// Switched off but still loaded, the same flag the block in the strip shows.
    /// </summary>
    /// <remarks>
    /// Read and written through the chain box so that the window and the strip cannot disagree.
    /// An instrument's window has nowhere to put this and reads false for ever.
    /// </remarks>
    public bool IsBypassed
    {
        get => _device?.IsBypassed ?? false;
        set
        {
            if (_device == null) return;

            _device.IsBypassed = value;
            OnPropertyChanged();
        }
    }
}
