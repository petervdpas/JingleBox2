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
    private readonly PluginDeviceViewModel? _device;

    public PluginWindowViewModel(PluginControlsViewModel panel, string name, PluginDeviceViewModel? device = null)
    {
        Panel = panel;
        Name = name;
        _device = device;
    }

    public PluginControlsViewModel Panel { get; }

    public string Name { get; }

    /// <summary>Only a plugin in a chain has one, because only a chain has somewhere to be off.</summary>
    public bool HasBypass => _device != null;

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
