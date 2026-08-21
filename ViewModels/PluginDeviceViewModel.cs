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
        {
            // A hidden parameter is one the plugin does not want shown, and its own bypass is
            // something the host offers in its own way.
            if (parameter.IsHidden || parameter.IsBypass) continue;

            var row = new PluginParameterViewModel(effect, parameter);

            Parameters.Add(row);

            if (parameter.IsReadOnly) Readouts.Add(row);
            else if (row.IsSwitch) Switches.Add(row);
            else Controls.Add(row);
        }
    }

    public ClapEffect Effect { get; }

    public PluginChain.Device Device { get; }

    public string Name => Effect.Info.Name;

    /// <summary>Everything shown, controls and readings alike, for polling.</summary>
    public ObservableCollection<PluginParameterViewModel> Parameters { get; } = new();

    /// <summary>The knobs: what you set.</summary>
    public ObservableCollection<PluginParameterViewModel> Controls { get; } = new();

    /// <summary>The two-position ones, which are tick boxes rather than dials.</summary>
    public ObservableCollection<PluginParameterViewModel> Switches { get; } = new();

    /// <summary>The readings: what the plugin reports back, such as gain reduction.</summary>
    public ObservableCollection<PluginParameterViewModel> Readouts { get; } = new();

    public bool HasSwitches => Switches.Count > 0;

    public bool HasParameters => Parameters.Count > 0;

    public bool HasReadouts => Readouts.Count > 0;

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
        }
    }

    public IRelayCommand RemoveCommand => new RelayCommand(() => _chain.Remove(this));

    public IRelayCommand MoveLeftCommand => new RelayCommand(() => _chain.Move(this, -1));

    public IRelayCommand MoveRightCommand => new RelayCommand(() => _chain.Move(this, 1));
}
