using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Audio.Plugins;
using JingleBox2.Diagnostics;
using System;
using System.Collections.ObjectModel;
using JingleBox2.Diagnostics.Enums;
using JingleBox2.Audio.Plugins.Interfaces;
using JingleBox2.ViewModels.Interfaces;
using JingleBox2.Audio.Plugins.Records;

namespace JingleBox2.ViewModels;

/// <summary>
/// A chain of effects as the screen sees it: a row of boxes, a plus to add another, and the
/// knobs of whichever box is picked.
/// </summary>
/// <remarks>
/// The chain is deliberately ignorant of what it is attached to, which is what
/// <see cref="IChainOwner"/> is for. The tracker points it at the track under the cursor and
/// moves it as the cursor moves; a pad points it at itself; the master has one of its own on the
/// mixer rather than the one under the pattern, because that one follows the cursor and the
/// master is not somewhere a cursor can be. All of them get the same control and the same
/// behaviour.
///
/// Nothing here holds the chain itself. The chain belongs to the track or the pad and outlives
/// this view, so pointing the view somewhere else and back shows what was already there rather
/// than building it again.
/// </remarks>
public sealed partial class PluginChainViewModel : ObservableObject
{
    /// <summary>The one place that knows both plugin standards. Holds nothing, so one is enough.</summary>
    private readonly IPluginHost _plugins = new PluginHost();

    /// <summary>Big enough for any block the engines hand out, so nothing is split needlessly.</summary>
    public const int MaxFrames = 2048;

    /// <summary>
    /// Makes a chain view over the plugins that are installed, with nothing picked yet.
    /// </summary>
    /// <param name="plugins">
    /// What SETTINGS scanned, which is what the plus button offers. Shared rather than scanned
    /// again here: a scan opens every plugin library it finds and is not a thing to do twice.
    /// </param>
    /// <param name="effects">What effects of ours this installation has, or nothing.</param>
    /// <param name="engines">Which of them this build can make. Left out, the real list.</param>
    /// <param name="front">
    /// Where a face opened off this chain says it is in front, so a knob pointed at one of ours
    /// reaches the one you are looking at. Nothing where there is nobody to tell, which is every
    /// chain view built without a link layer behind it.
    /// </param>
    public PluginChainViewModel(
        PluginLibraryViewModel plugins,
        SoundDevices.SoundEffects.Interfaces.ISoundEffectProjects? effects = null,
        SoundDevices.SoundEffects.Interfaces.ISoundEffectEngines? engines = null,
        ISoundEffectInFront? front = null)
    {
        Plugins = plugins;
        _effects = effects;
        _engines = engines ?? new SoundDevices.SoundEffects.SoundEffectEngines();
        Front = front;

        _poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(PollMilliseconds) };
        _poll.Tick += (_, _) => Poll();
    }

    /// <summary>Fast enough for a meter to move, slow enough to cost nothing.</summary>
    private const int PollMilliseconds = 120;

    /// <summary>
    /// Reads back the parameters a plugin moves by itself, while there is anything in the chain.
    /// </summary>
    /// <remarks>
    /// A compressor's gain reduction and its output level are the plugin reporting, not somebody
    /// setting: nothing tells the host they moved, so without this they would sit at whatever
    /// they read when the device was loaded and look broken. Started when the first device
    /// arrives and stopped when the last one goes, so an empty chain costs nothing.
    /// </remarks>
    private readonly DispatcherTimer _poll;

    /// <summary>
    /// One pass of the readings, over the devices whose windows are open.
    /// </summary>
    /// <remarks>
    /// Only what is on screen, and within that only the readings. A knob moves when a hand moves
    /// it and reading every knob back would mean thousands of calls into a plugin every tick;
    /// Serum alone declares 2622 parameters, and for a plugin in another process each one of
    /// those is a round trip.
    /// </remarks>
    private void Poll()
    {
        foreach (var device in Devices)
        {
            if (!device.IsOpen) continue;

            if (device is PluginSlotViewModel plugin)
            {
                foreach (var parameter in plugin.Readouts) parameter.Refresh();
            }
        }
    }

    /// <summary>Everything installed, as scanned in SETTINGS.</summary>
    public PluginLibraryViewModel Plugins { get; }

    /// <summary>What effects of ours this installation has, which the plus offers first.</summary>
    /// <remarks>
    /// Ours before somebody else's, deliberately: this list is short and known, and a plugin
    /// list can run to hundreds. Nothing at all when there are none, which is what the tab in
    /// SETTINGS is for.
    /// </remarks>
    public System.Collections.Generic.IReadOnlyList<SoundDevices.SoundEffects.SoundEffectProject> Ours =>
        _effects?.All ?? System.Array.Empty<SoundDevices.SoundEffects.SoundEffectProject>();

    /// <summary>True when there is one of ours to offer, so the list can be left out.</summary>
    public bool HasOurs => Ours.Count > 0;

    /// <summary>What effects of ours this installation has, or nothing when none were read.</summary>
    private readonly SoundDevices.SoundEffects.Interfaces.ISoundEffectProjects? _effects;

    /// <summary>Which of them this build can actually make.</summary>
    private readonly SoundDevices.SoundEffects.Interfaces.ISoundEffectEngines _engines;

    /// <summary>The boxes in this chain, in the order the audio goes through them.</summary>
    public ObservableCollection<IChainSlot> Devices { get; } = new();

    /// <summary>
    /// Where a face opened off this chain says it is in front, or nothing.
    /// </summary>
    /// <remarks>
    /// Held rather than reached for, and handed down to each box, because the box is what a
    /// window is opened on and the box is what the window can tell. This view knows nothing
    /// about links and does not want to.
    /// </remarks>
    public ISoundEffectInFront? Front { get; }

    /// <summary>
    /// What this view is pointed at, which is where the chain really lives.
    /// </summary>
    /// <remarks>
    /// Setting it shows that host's chain and forgets the last one's rows. Null is an ordinary
    /// state and not a fault: the pattern's chain has nothing under it until the cursor is on a
    /// track with a song open.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTarget))]
    [NotifyPropertyChangedFor(nameof(Label))]
    private IChainOwner? target;

    /// <summary>Why the last thing somebody asked for did not happen, or empty.</summary>
    /// <remarks>
    /// Said rather than swallowed. A plugin that will not load, or that is an instrument and so
    /// cannot go in a chain, is refused with a reason on the strip; the alternative is a plus
    /// button that sometimes does nothing.
    /// </remarks>
    [ObservableProperty] private string status = "";

    /// <summary>True when there is somewhere for a plugin to go.</summary>
    public bool HasTarget => Target != null;

    /// <summary>True when there is nothing in the chain, which is what the empty line is for.</summary>
    public bool IsEmpty => Devices.Count == 0;

    /// <summary>Whose chain this is, in the host's own words, or that nothing is picked.</summary>
    public string Label => Target?.Label ?? "Nothing picked";

    /// <summary>
    /// Adds a plugin to the end of the chain, which is what the plus button does.
    /// </summary>
    /// <remarks>
    /// Always enabled: whether the plugin can actually go on is decided in <see cref="Add(PluginInfo)"/>,
    /// which can say why on the strip. A greyed-out plus with no reason beside it would be
    /// worse.
    /// </remarks>
    public IRelayCommand<PluginInfo> AddCommand => new RelayCommand<PluginInfo>(Add);

    /// <summary>Adds one of ours to the end of the chain, which is the other half of the plus.</summary>
    public IRelayCommand<SoundDevices.SoundEffects.SoundEffectProject> AddOursCommand =>
        new RelayCommand<SoundDevices.SoundEffects.SoundEffectProject>(Add);

    /// <summary>
    /// Puts one of our effects on the end of the chain.
    /// </summary>
    /// <remarks>
    /// The same act as adding a plugin and a good deal less to go wrong: the engine is in this
    /// process, it cannot fail to load, it has no window of its own to crash in, and it is built
    /// for the host's own rate because that is the rate of the audio it is about to be handed.
    ///
    /// An id this build has no engine for is refused with a reason rather than added as a box
    /// that passes the audio through, which is the same gate the rack keeps.
    /// </remarks>
    /// <param name="effect">Which effect, as the registry read it off the disc.</param>
    public void Add(SoundDevices.SoundEffects.SoundEffectProject? effect)
    {
        if (effect == null || Target == null) return;

        if (_engines.Make(effect.Id, Target.SampleRate, MaxFrames) is not { } engine)
        {
            Status = $"'{effect.Name}' has no engine in this version";
            return;
        }

        foreach (var parameter in effect.Parameters) engine.SetValue(parameter.Key, parameter.Default);

        AboutTo();

        var device = Target.Chain.Add(engine);

        Devices.Add(new SoundEffectViewModel(this, effect, engine, device));

        NotifyChanged();

        Status = "";
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>Shows the new host's chain, without touching either host's audio.</summary>
    partial void OnTargetChanged(IChainOwner? value)
    {
        Rebuild();
    }

    /// <summary>
    /// Loads a plugin and puts it on the end of the chain.
    /// </summary>
    /// <remarks>
    /// Three things are refused with a reason rather than accepted and then wondered about. An
    /// instrument has no audio input, so in a chain it would put out its own silence over
    /// whatever the track was playing. A plugin that took the application down while loading is
    /// held back by <see cref="PluginCrashGuard"/> until somebody says otherwise. And one that
    /// simply will not load says so.
    ///
    /// The plugin is built for the host's own rate, not the device's, because that is the rate
    /// of the audio it is about to be handed.
    /// </remarks>
    public void Add(PluginInfo? plugin)
    {
        if (plugin == null || Target == null) return;

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

        var effect = _plugins.Load(plugin, Target.SampleRate, MaxFrames);
        if (effect == null)
        {
            Status = $"'{plugin.Name}' would not load";
            return;
        }

        AboutTo();

        var device = Target.Chain.Add(effect);
        var row = new PluginSlotViewModel(this, effect, device);

        Devices.Add(row);
        _poll.Start();

        NotifyChanged();

        Status = "";
        OnPropertyChanged(nameof(IsEmpty));
    }

    /// <summary>
    /// Takes a device out of the chain and lets go of the plugin behind it.
    /// </summary>
    /// <remarks>
    /// In that order, and the order is the whole of it. Anything showing the device is told
    /// first, because a device on its way out must not leave a window of itself behind. Then it
    /// comes out of the chain, so the audio thread is not inside something that is being taken
    /// apart. Then its own interface goes, before the plugin does: a plugin drawing into a
    /// window that has already been disposed is a crash inside its own toolkit.
    /// </remarks>
    public void Remove(IChainSlot device)
    {
        if (Target == null) return;

        AboutTo();

        DeviceClosing?.Invoke(device);

        Target.Chain.Remove(device.Device);
        Devices.Remove(device);

        if (device is PluginSlotViewModel plugin)
        {
            plugin.Panel.Close();
            plugin.Effect.Dispose();
        }

        if (Devices.Count == 0) _poll.Stop();

        OnPropertyChanged(nameof(IsEmpty));
        NotifyChanged();
    }

    /// <summary>
    /// Moves a device along the chain, which changes the order the audio goes through them.
    /// </summary>
    /// <remarks>
    /// The chain is asked first and the row only follows if it agreed, so the picture cannot
    /// end up saying an order the audio is not in. Both ends refuse rather than wrap: a device
    /// pushed off the front of a chain has nowhere to be.
    /// </remarks>
    /// <param name="device">The row being moved, which carries the chain entry the audio order is kept in.</param>
    /// <param name="offset">Minus one for earlier in the chain, plus one for later.</param>
    public void Move(IChainSlot device, int offset)
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
    /// <remarks>
    /// An undo is the case this exists for: the song's description of a chain is put back and
    /// the plugins are rebuilt underneath, and this is how the row of boxes catches up with what
    /// is really loaded.
    /// </remarks>
    public void Reload() => Rebuild();

    /// <summary>
    /// Raised for a device that is going away, so anything showing it can let go.
    /// </summary>
    /// <remarks>
    /// A view concern reaching back into the view model would be worse than one event. It is
    /// raised for every device when the view is pointed somewhere else, not only when one is
    /// removed, because a window belonging to the track you have just left is a window over
    /// somebody else's plugin.
    /// </remarks>
    public event System.Action<IChainSlot>? DeviceClosing;

    /// <summary>
    /// Raised before the chain is about to gain or lose a plugin.
    /// </summary>
    /// <remarks>
    /// Apart from <see cref="Changed"/>, which says it already happened. A history needs the
    /// state being left rather than the one arrived at, and afterwards the first is gone.
    /// </remarks>
    public event System.Action? Changing;

    /// <summary>Says the chain is about to change, for whoever is keeping a history.</summary>
    private void AboutTo() => Changing?.Invoke();

    /// <summary>
    /// The plugin this track plays, when it plays one, shown at the head of the strip.
    /// </summary>
    /// <remarks>
    /// Null for anything that is not a track: a pad has effects but nothing that makes the
    /// sound in the first place, and the master has everything already played by the time it is
    /// reached.
    /// </remarks>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasInstrument))]
    private PluginInstrumentViewModel? instrument;

    /// <summary>True when there is something at the head of the strip to draw.</summary>
    public bool HasInstrument => Instrument != null;

    /// <summary>
    /// What to say when the strip is empty, since not every strip could hold an instrument.
    /// </summary>
    /// <remarks>
    /// A track's chain begins with whatever the track plays, so an empty one has neither. The
    /// master has no instrument and never will: everything has already been played by the time
    /// it is reached. Offering it one in the only sentence it ever shows would be an offer
    /// nothing could accept.
    /// </remarks>
    public string Nothing { get; init; } = "No instrument or effect yet.";

    /// <summary>
    /// Raised whenever the chain or anything in it changes: a device added, moved, removed,
    /// bypassed, or a knob turned.
    /// </summary>
    /// <remarks>
    /// What a pad or a song listens to in order to know it has something new to save. It says
    /// nothing about what changed, deliberately: everything that listens saves the lot anyway.
    /// </remarks>
    public event System.Action? Changed;

    /// <summary>
    /// Says something changed here, and refreshes what the blocks print.
    /// </summary>
    /// <remarks>
    /// Called by the devices as well as by this class, since a knob turned inside one is a
    /// change to the chain as far as anything above is concerned.
    ///
    /// The readings on each block are read again here rather than polled. They are otherwise
    /// whatever they were when the block was first drawn, and this is called when something is
    /// known to have moved; a chain is four devices rather than four hundred, so asking all of
    /// them costs nothing worth counting.
    /// </remarks>
    public void NotifyChanged()
    {
        Log.Write(LogArea.Plugins, () =>
            "the chain on " + Label + " changed, " + (Changed?.GetInvocationList().Length ?? 0) + " listening");

        foreach (var device in Devices) device.Reread();

        Changed?.Invoke();
    }

    /// <summary>
    /// Reads the chain back out of whatever it is attached to.
    /// </summary>
    /// <remarks>
    /// Only the plugin effects are shown. A chain can hold any insert, and one built elsewhere
    /// would need rows of its own, which nothing does yet.
    ///
    /// Every existing row is told it is closing on the way past, or a window left open over a
    /// device belonging to the track you have just left goes on drawing something that is no
    /// longer being shown.
    /// </remarks>
    private void Rebuild()
    {
        foreach (var device in Devices) DeviceClosing?.Invoke(device);

        Devices.Clear();

        if (Target == null)
        {
            OnPropertyChanged(nameof(IsEmpty));
            return;
        }

        foreach (var device in Target.Chain.Slots)
        {
            if (device.Insert is IPluginEffect effect)
            {
                Devices.Add(new PluginSlotViewModel(this, effect, device));

                continue;
            }

            if (device.Insert is not SoundDevices.SoundEffects.Interfaces.ISoundEffectEngine engine) continue;

            if (_effects?.For(engine.Id) is not { } ours) continue;

            Devices.Add(new SoundEffectViewModel(this, ours, engine, device));
        }

        if (Devices.Count > 0) _poll.Start();
        else _poll.Stop();

        OnPropertyChanged(nameof(IsEmpty));
    }
}
