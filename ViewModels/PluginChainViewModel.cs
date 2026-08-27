using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Audio.Plugins;
using JingleBox2.Diagnostics;
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
        // Only what is on screen. A plugin reports its own meters, and reading them back for
        // a device nobody is looking at is work for nothing.
        foreach (var device in Devices)
        {
            if (!device.IsOpen) continue;

            // Only the readings. Those are the ones the plugin moves by itself; a knob only
            // moves when a hand moves it, and reading every knob back would mean thousands of
            // calls into a plugin every tick. Serum alone declares 2622 parameters.
            foreach (var parameter in device.Readouts) parameter.Refresh();
        }
    }

    /// <summary>Everything installed, as scanned in SETTINGS.</summary>
    public PluginLibraryViewModel Plugins { get; }

    /// <summary>The boxes in this chain, in the order the audio goes through them.</summary>
    public ObservableCollection<PluginDeviceViewModel> Devices { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTarget))]
    [NotifyPropertyChangedFor(nameof(Label))]
    private IPluginHost? target;

    [ObservableProperty] private string status = "";

    public bool HasTarget => Target != null;

    public bool IsEmpty => Devices.Count == 0;

    public string Label => Target?.Label ?? "Nothing picked";

    /// <summary>Adds a plugin to the end of the chain. What the plus button does.</summary>
    public IRelayCommand<PluginInfo> AddCommand => new RelayCommand<PluginInfo>(Add);

    partial void OnTargetChanged(IPluginHost? value)
    {
        // The chain belongs to the track or pad, not to this view: moving to another track
        // shows what is already there rather than building it again.
        Rebuild();
    }

    public void Add(PluginInfo? plugin)
    {
        if (plugin == null || Target == null) return;

        // An instrument has no audio input, so in a chain it would put out its own silence
        // over whatever the track was playing. Refused with a reason rather than accepted and
        // then wondered about.
        if (plugin.IsInstrument)
        {
            Status = $"'{plugin.Name}' is an instrument, not an effect";
            return;
        }

        if (PluginCrashGuard.IsLoadBlocked(plugin))
        {
            Status = PluginCrashGuard.Reason(plugin);
            return;
        }

        var effect = PluginHost.Load(plugin, Target.SampleRate, MaxFrames);
        if (effect == null)
        {
            Status = $"'{plugin.Name}' would not load";
            return;
        }

        AboutTo();

        var device = Target.Chain.Add(effect);
        var row = new PluginDeviceViewModel(this, effect, device);

        Devices.Add(row);
        _poll.Start();

        NotifyChanged();

        Status = "";
        OnPropertyChanged(nameof(IsEmpty));
    }

    public void Remove(PluginDeviceViewModel device)
    {
        if (Target == null) return;

        AboutTo();

        // Out of the chain first, then let go: the audio thread must not be inside something
        // that is being taken apart.
        // A device on its way out must not leave a window of itself behind.
        DeviceClosing?.Invoke(device);

        Target.Chain.Remove(device.Device);
        Devices.Remove(device);

        // Its own interface goes before the plugin does: a plugin drawing into a window that
        // has been taken apart is a crash inside its own toolkit.
        device.Panel.Close();
        device.Effect.Dispose();

        if (Devices.Count == 0) _poll.Stop();

        OnPropertyChanged(nameof(IsEmpty));
        NotifyChanged();
    }

    public void Move(PluginDeviceViewModel device, int offset)
    {
        if (Target == null) return;
        if (!Target.Chain.Move(device.Device, offset)) return;

        int from = Devices.IndexOf(device);
        int to = from + offset;

        if (from < 0 || to < 0 || to >= Devices.Count) return;

        Devices.Move(from, to);

        NotifyChanged();
    }

    /// <summary>Reads the chain back out of its host, after something else has changed it.</summary>
    public void Reload() => Rebuild();

    /// <summary>
    /// Reads the chain back out of whatever it is attached to. Only the boxes this view made
    /// are shown; a chain built elsewhere would need its own rows, which nothing does yet.
    /// </summary>
    /// <summary>
    /// Raised for a device that is going away, so anything showing it can let go. A view
    /// concern reaching back into the view model would be worse than one event.
    /// </summary>
    public event System.Action<PluginDeviceViewModel>? DeviceClosing;

    /// <summary>
    /// Raised before the chain is about to gain or lose a plugin.
    /// </summary>
    /// <remarks>
    /// Apart from <see cref="Changed"/>, which says it already happened. A history needs the
    /// state being left rather than the one arrived at, and afterwards the first is gone.
    /// </remarks>
    public event System.Action? Changing;

    private void AboutTo() => Changing?.Invoke();

    /// <summary>
    /// Raised whenever the chain or anything in it changes: a device added, moved, removed,
    /// bypassed, or a knob turned. What a pad or a song listens to in order to know that it
    /// has something new to save.
    /// </summary>
    /// <summary>
    /// The plugin this track plays, when it plays one, shown at the head of the strip.
    /// </summary>
    /// <remarks>
    /// Null for anything that is not a track: a pad has effects but nothing that makes the
    /// sound in the first place.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasInstrument))]
    private PluginInstrumentViewModel? instrument;

    public bool HasInstrument => Instrument != null;

    public event System.Action? Changed;

    /// <summary>Says something changed here. Called by the devices as well as by this class.</summary>
    public void NotifyChanged()
    {
        Log.Write(LogArea.Plugins, () =>
            "the chain on " + Label + " changed, " + (Changed?.GetInvocationList().Length ?? 0) + " listening");

        // The readings printed on each block, which are otherwise whatever they were when the
        // block was first drawn. Not a poll: this is called when something is known to have
        // moved, and a chain is four devices rather than four hundred.
        foreach (var device in Devices) device.Reread();

        Changed?.Invoke();
    }

    private void Rebuild()
    {
        foreach (var device in Devices) DeviceClosing?.Invoke(device);

        Devices.Clear();

        if (Target == null)
        {
            OnPropertyChanged(nameof(IsEmpty));
            return;
        }

        foreach (var device in Target.Chain.Devices)
        {
            if (device.Insert is not IPluginEffect effect) continue;

            Devices.Add(new PluginDeviceViewModel(this, effect, device));
        }

        if (Devices.Count > 0) _poll.Start();
        else _poll.Stop();

        OnPropertyChanged(nameof(IsEmpty));
    }
}
