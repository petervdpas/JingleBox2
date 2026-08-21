using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Audio.Plugins;
using System;
using System.Collections.ObjectModel;

namespace JingleBox2.ViewModels;

/// <summary>
/// Somewhere a chain of effects can run: a tracker track, a pad, or anything else that owns a
/// piece of audio. The host owns the chain and knows what rate it runs at; it does not know
/// what is in it.
/// </summary>
public interface IPluginHost
{
    /// <summary>What this chain is called on screen: "TR-01", "Pad 03".</summary>
    string Label { get; }

    /// <summary>The chain, made and put into the audio path the first time it is asked for.</summary>
    PluginChain Chain { get; }

    /// <summary>The rate the audio here runs at, which is what a plugin has to be built for.</summary>
    int SampleRate { get; }
}

/// <summary>
/// A chain of effects as the screen sees it: a row of boxes, a plus to add another, and the
/// knobs of whichever box is picked.
/// </summary>
/// <remarks>
/// The chain is deliberately ignorant of what it is attached to. The tracker points it at the
/// track under the cursor and moves it as the cursor moves; a pad points it at itself. Both
/// get the same control and the same behaviour.
/// </remarks>
public sealed partial class PluginChainViewModel : ObservableObject
{
    /// <summary>Big enough for any block the engines hand out, so nothing is split needlessly.</summary>
    public const int MaxFrames = 2048;

    public PluginChainViewModel(PluginLibraryViewModel plugins)
    {
        Plugins = plugins;

        // Plugins move their own parameters: a compressor's gain reduction and output level
        // are the plugin reporting, not the user setting. Without this they would sit at
        // whatever they read when the device was loaded and look broken.
        _poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(PollMilliseconds) };
        _poll.Tick += (_, _) => Poll();
    }

    /// <summary>Fast enough for a meter to move, slow enough to cost nothing.</summary>
    private const int PollMilliseconds = 120;

    private readonly DispatcherTimer _poll;

    private void Poll()
    {
        var device = Selected;
        if (device == null) return;

        foreach (var parameter in device.Parameters) parameter.Refresh();
    }

    /// <summary>Everything installed, as scanned in SETTINGS.</summary>
    public PluginLibraryViewModel Plugins { get; }

    /// <summary>The boxes in this chain, in the order the audio goes through them.</summary>
    public ObservableCollection<PluginDeviceViewModel> Devices { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTarget))]
    [NotifyPropertyChangedFor(nameof(Label))]
    private IPluginHost? target;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelection))]
    private PluginDeviceViewModel? selected;

    [ObservableProperty] private string status = "";

    public bool HasTarget => Target != null;

    public bool HasSelection => Selected != null;

    public bool IsEmpty => Devices.Count == 0;

    public string Label => Target?.Label ?? "Nothing picked";

    /// <summary>Adds a plugin to the end of the chain. What the plus button does.</summary>
    public IRelayCommand<ClapPluginInfo> AddCommand => new RelayCommand<ClapPluginInfo>(Add);

    partial void OnTargetChanged(IPluginHost? value)
    {
        // The chain belongs to the track or pad, not to this view: moving to another track
        // shows what is already there rather than building it again.
        Rebuild();
    }

    public void Add(ClapPluginInfo? plugin)
    {
        if (plugin == null || Target == null) return;

        var effect = ClapEffect.Load(plugin.Path, plugin.Id, Target.SampleRate, MaxFrames);
        if (effect == null)
        {
            Status = $"'{plugin.Name}' would not load";
            return;
        }

        var device = Target.Chain.Add(effect);
        var row = new PluginDeviceViewModel(this, effect, device);

        Devices.Add(row);
        Select(row);

        Status = "";
        OnPropertyChanged(nameof(IsEmpty));
    }

    public void Remove(PluginDeviceViewModel device)
    {
        if (Target == null) return;

        // Out of the chain first, then let go: the audio thread must not be inside something
        // that is being taken apart.
        Target.Chain.Remove(device.Device);
        Devices.Remove(device);
        device.Effect.Dispose();

        if (ReferenceEquals(Selected, device)) Select(Devices.Count > 0 ? Devices[0] : null);

        OnPropertyChanged(nameof(IsEmpty));
    }

    public void Move(PluginDeviceViewModel device, int offset)
    {
        if (Target == null) return;
        if (!Target.Chain.Move(device.Device, offset)) return;

        int from = Devices.IndexOf(device);
        int to = from + offset;

        if (from < 0 || to < 0 || to >= Devices.Count) return;

        Devices.Move(from, to);
    }

    public void Select(PluginDeviceViewModel? device)
    {
        foreach (var row in Devices) row.IsSelected = ReferenceEquals(row, device);

        Selected = device;

        // Only the device on screen is polled, and only while there is one.
        if (device == null) _poll.Stop();
        else _poll.Start();
    }

    /// <summary>Reads the chain back out of its host, after something else has changed it.</summary>
    public void Reload() => Rebuild();

    /// <summary>
    /// Reads the chain back out of whatever it is attached to. Only the boxes this view made
    /// are shown; a chain built elsewhere would need its own rows, which nothing does yet.
    /// </summary>
    private void Rebuild()
    {
        Devices.Clear();
        Selected = null;

        if (Target == null)
        {
            OnPropertyChanged(nameof(IsEmpty));
            return;
        }

        foreach (var device in Target.Chain.Devices)
        {
            if (device.Insert is not ClapEffect effect) continue;

            Devices.Add(new PluginDeviceViewModel(this, effect, device));
        }

        if (Devices.Count > 0) Select(Devices[0]);

        OnPropertyChanged(nameof(IsEmpty));
    }
}
