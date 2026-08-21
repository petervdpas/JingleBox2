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

    public PluginDeviceViewModel(PluginChainViewModel chain, ClapEffect effect, PluginChain.Device device)
    {
        _chain = chain;
        Effect = effect;
        Device = device;

        foreach (var parameter in effect.Parameters())
            Parameters.Add(new PluginParameterViewModel(effect, parameter));
    }

    public ClapEffect Effect { get; }

    public PluginChain.Device Device { get; }

    public string Name => Effect.Info.Name;

    public ObservableCollection<PluginParameterViewModel> Parameters { get; } = new();

    public bool HasParameters => Parameters.Count > 0;

    [ObservableProperty] private bool isSelected;

    /// <summary>Switched off but still in the chain, so it can be switched back on.</summary>
    public bool IsBypassed
    {
        get => Device.Bypassed;
        set
        {
            if (Device.Bypassed == value) return;

            Device.Bypassed = value;
            OnPropertyChanged();
        }
    }

    public IRelayCommand SelectCommand => new RelayCommand(() => _chain.Select(this));

    public IRelayCommand RemoveCommand => new RelayCommand(() => _chain.Remove(this));

    public IRelayCommand MoveLeftCommand => new RelayCommand(() => _chain.Move(this, -1));

    public IRelayCommand MoveRightCommand => new RelayCommand(() => _chain.Move(this, 1));
}
