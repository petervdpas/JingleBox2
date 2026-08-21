using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Audio.Plugins;
using System.Collections.ObjectModel;

namespace JingleBox2.ViewModels;

/// <summary>
/// One box in a chain: a loaded plugin, its knobs, and whether it is switched on.
/// </summary>
public sealed partial class PluginDeviceViewModel : ObservableObject
{
    private readonly PluginChainViewModel _chain;

    public PluginDeviceViewModel(PluginChainViewModel chain, IPluginEffect effect, PluginChain.Device device)
    {
        _chain = chain;
        Effect = effect;
        Device = device;

        Panel = new PluginControlsViewModel(effect, chain.NotifyChanged);
    }

    /// <summary>This plugin's knobs. The panel is shared with the instrument editor.</summary>
    public PluginControlsViewModel Panel { get; }

    public IPluginEffect Effect { get; }

    public PluginChain.Device Device { get; }

    public string Name => Effect.Info.Name;

    /// <summary>The readings, which is all a poll needs to touch.</summary>
    public ObservableCollection<PluginParameterViewModel> Readouts => Panel.Readouts;

    public bool HasParameters => Panel.HasParameters;

    /// <summary>True while this one's window is open, so the box in the strip says so.</summary>
    [ObservableProperty] private bool isOpen;

    /// <summary>Switched off but still in the chain, so it can be switched back on.</summary>
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

    public IRelayCommand RemoveCommand => new RelayCommand(() => _chain.Remove(this));

    public IRelayCommand MoveLeftCommand => new RelayCommand(() => _chain.Move(this, -1));

    public IRelayCommand MoveRightCommand => new RelayCommand(() => _chain.Move(this, 1));
}
