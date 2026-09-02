using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Tracker;
using System;
using JingleBox2.Audio.Plugins.Interfaces;
using JingleBox2.ViewModels.Records;
using JingleBox2.Devices.SoundMachines;
using JingleBox2.Devices.SoundMachines.Interfaces;
using JingleBox2.Rack.Faces.Interfaces;
using JingleBox2.Rack.Faces;

namespace JingleBox2.ViewModels;

/// <summary>
/// The plugin a track plays, as a box in that track's strip.
/// </summary>
/// <remarks>
/// A track's instrument is a plugin like the effects after it, so it belongs in the same row
/// rather than on another page. It sits at the head of the strip because that is where it is
/// in the audio: it makes the sound, and everything else in the row works on what it made.
///
/// The plugin itself is not loaded until somebody opens it. A track selection should not cost
/// the two hundred milliseconds Vital takes to come up, and the tracker loads its own copy on
/// the first note anyway; this asks for that same one rather than making a second.
/// </remarks>
public sealed partial class PluginInstrumentViewModel : ObservableObject
{
    /// <summary>The order a panel reads in. Holds nothing, so one is enough.</summary>
    private readonly IPanelOrder _order = new PanelOrder();

    /// <summary>Which values adapter reads a given instrument.</summary>
    /// <remarks>Shared rather than one apiece: it holds nothing of its own.</remarks>
    private static readonly IMachineValuesFor ValuesFor = new MachineValuesFor();

    /// <summary>The machines this run has.</summary>
    private readonly IMachineProjects _machines;

    /// <summary>How a machine that is not here is named, which its own kind cannot do.</summary>
    private readonly Devices.SoundMachines.Interfaces.IMissingMachines _missing;

    /// <summary>The song's instrument: its name, its machine, and its patch.</summary>
    private readonly TrackerInstrument _instrument;

    /// <summary>
    /// Asks for the plugin the tracker is actually playing, rather than loading a second one.
    /// </summary>
    /// <remarks>
    /// A function and not the plugin itself, because at the moment this box is made there may be
    /// no plugin at all: the tracker loads its copy on the first note, and asking early would
    /// cost a load nobody wanted.
    /// </remarks>
    private readonly Func<IPluginInstrument?> _live;

    /// <summary>Told when a knob moves, so the song knows it has something to save.</summary>
    private readonly Action? _changed;

    /// <summary>Builds the designer for a machine of ours, on the first ask.</summary>
    private readonly Func<TrackInstrumentDesigner>? _designer;

    /// <summary>The plugin, once something has asked for it, so its patch can be read back.</summary>
    private IPluginInstrument? _plugin;

    /// <summary>
    /// Makes the head of a track's strip for one instrument, without loading anything.
    /// </summary>
    /// <param name="instrument">What the track plays, which is where the name and the settings are read from.</param>
    /// <param name="live">
    /// Asks for the plugin the tracker is actually playing. A function rather than the plugin
    /// itself, since at the moment this block is made there may be none loaded yet.
    /// </param>
    /// <param name="changed">Told when a knob moves, so the song knows it has something to save.</param>
    /// <param name="designer">
    /// Builds the designer for a machine of ours, on the first ask, and is null for a plugin,
    /// which has no described panel to design.
    /// </param>
    /// <param name="remove">
    /// Takes it off the track, or null where there is no track to take it off, which is what
    /// makes the cross on the block appear at all.
    /// </param>
    /// <param name="machines">
    /// The machines this run has, the one instance everything shares. Required rather than
    /// defaulted: a fresh one is empty, so a default would draw blank panels and report every
    /// machine missing, without an error anywhere to say why.
    /// </param>
    /// <param name="missing">
    /// How a machine that is not installed is named, which is the one thing its own kind cannot
    /// say. Left out, the ordinary one.
    /// </param>
    public PluginInstrumentViewModel(
        TrackerInstrument instrument,
        Func<IPluginInstrument?> live,
        IMachineProjects machines,
        Action? changed = null,
        Func<TrackInstrumentDesigner>? designer = null,
        Action? remove = null,
        Devices.SoundMachines.Interfaces.IMissingMachines? missing = null)
    {
        _machines = machines;
        _missing = missing ?? new Devices.SoundMachines.MissingMachines();
        _instrument = instrument;
        _live = live;
        _changed = changed;
        _designer = designer;
        _remove = remove;
    }

    /// <summary>What the cross on the block does, or null when the box is not on a track.</summary>
    private readonly Action? _remove;

    /// <summary>
    /// Takes the instrument off the track, which is what the corner of its block does.
    /// </summary>
    /// <remarks>
    /// Off the track and not out of the song. An instrument is the song's and can be on no
    /// track at all, so this is the same thing as pointing the track at nothing: the sound is
    /// still in the list beside the pattern and can be put back on any track. Taking it out of
    /// the song is a different act with its own button, and it is destructive where this is not.
    ///
    /// Passed in rather than done here, because which track this instrument is on is the
    /// tracker's business and this box is deliberately ignorant of it.
    /// </remarks>
    public CommunityToolkit.Mvvm.Input.IRelayCommand RemoveCommand =>
        new CommunityToolkit.Mvvm.Input.RelayCommand(() => _remove?.Invoke(), () => _remove is not null);

    /// <summary>True when there is a track to take it off.</summary>
    public bool CanRemove => _remove is not null;

    /// <summary>
    /// True when the sound is a plugin's, false when it is one of ours.
    /// </summary>
    /// <remarks>
    /// The box in the strip is the same either way, because to a track they are the same thing:
    /// the machine at the head of the row that makes the sound. Only what opens differs, the
    /// plugin's own window or our designer.
    /// </remarks>
    public bool IsPlugin => _instrument.IsPlugin;

    /// <summary>
    /// The designer for an instrument of ours, built the first time somebody opens it.
    /// </summary>
    /// <remarks>
    /// Kept rather than made afresh, so coming back to a track shows the panel as it was left
    /// and does not build a second editor over the same instrument.
    /// </remarks>
    public TrackInstrumentDesigner? Designer =>
        IsPlugin ? null : _built ??= _designer?.Invoke();

    /// <summary>The designer once it exists, so coming back to a track shows the same one.</summary>
    private TrackInstrumentDesigner? _built;

    /// <summary>What the instrument is called in this song, which is the name on the block.</summary>
    public string Name => _instrument.Name;

    /// <summary>What the plugin is called, for a window title that says which is which.</summary>
    public string Title => _instrument.Plugin?.Name is { Length: > 0 } plugin && plugin != _instrument.Name
        ? _instrument.Name + " (" + plugin + ")"
        : _instrument.Name;

    /// <summary>The instrument this stands for, so the strip can tell one from another.</summary>
    public TrackerInstrument Instrument => _instrument;

    /// <summary>
    /// True when this instrument's machine is not installed, so there is nothing to open.
    /// </summary>
    /// <remarks>
    /// A plugin is never this. It is not on a machine project at all, and a plugin that will not
    /// load reports itself in its own words from its own process.
    /// </remarks>
    public bool MachineMissing => !_instrument.IsPlugin && !_machines.Has(_instrument.Kind);

    /// <summary>
    /// What the missing machine is called, or nothing when it is not missing.
    /// </summary>
    /// <remarks>
    /// Asked of the one thing that knows how to name a machine that is not here. Reading
    /// <c>Machine.Name</c> instead gives the engine behind it, so an absent Ouroboros reports
    /// itself as "Mono synth" and the message tells somebody to register a thing that is not on
    /// the list under that name.
    /// </remarks>
    public Devices.SoundMachines.Records.MissingMachine? Missing => _missing.For(_instrument);

    /// <summary>The second line of its block: which machine it is, and how it is set.</summary>
    /// <remarks>
    /// The instrument's own sentence, the same one the song's instrument list prints. Two lists
    /// saying two different things about one instrument would be worse than either.
    /// </remarks>
    public string Detail => _instrument.Detail;

    /// <summary>
    /// The machine's own colour, so a block is recognised before it is read.
    /// </summary>
    /// <remarks>
    /// The same colour the rack and the song's instrument list use for the same machine. A
    /// plugin has none of ours, so it gets nothing and is told apart by saying VST3 or CLAP.
    /// </remarks>
    public string Colour =>
        _instrument.IsPlugin ? "" : _instrument.Machine.Theme.Accent;

    /// <summary>What kind of thing it is, for the corner of the block.</summary>
    public string Badge => _instrument.IsPlugin ? "PLUGIN" : "MACHINE";

    /// <summary>
    /// The first few of its machine's controls, printed on the block itself.
    /// </summary>
    /// <remarks>
    /// The same thing the effects after it print, and for the same reason: a row of boxes with
    /// names on them tells you the order of the chain and nothing at all about the sound. Which
    /// few is <see cref="JingleBox2.Rack.Faces.PanelOrder"/>, the order the panel reads, so it is the first
    /// three controls your eye lands on when you open the machine rather than the first three
    /// lines of a file.
    ///
    /// Read through a throwaway adapter from <see cref="Devices.SoundMachines.MachineValuesFor"/>,
    /// which is cheap: it is a view model over the patch the instrument already holds. What it
    /// is not is the machine's editor, which is what the panel needs and what this deliberately
    /// does not build.
    ///
    /// A plugin instrument prints nothing here, because its settings are its own and are read
    /// through the plugin. Its block says the plugin's name instead.
    /// </remarks>
    public System.Collections.Generic.IReadOnlyList<DeviceReading> Summary => _summary ??= Pick();

    /// <summary>What was last read off the patch, or null when it has to be read again.</summary>
    private System.Collections.Generic.List<DeviceReading>? _summary;

    /// <summary>True when there is anything to print under the name.</summary>
    public bool HasSummary => Summary.Count > 0;

    /// <summary>How many fit on a block without it becoming a panel of its own.</summary>
    private const int Shown = 3;

    /// <summary>
    /// Reads the machine's first few controls, in the order its own panel reads.
    /// </summary>
    /// <remarks>
    /// Through a throwaway values adapter, since nothing here is edited: the editor is what the
    /// panel needs and is deliberately not built. A plugin instrument answers with nothing,
    /// because its settings live inside the plugin and its block says the plugin's name instead.
    /// </remarks>
    private System.Collections.Generic.List<DeviceReading> Pick()
    {
        var found = new System.Collections.Generic.List<DeviceReading>(Shown);

        if (_instrument.IsPlugin) return found;

        if (_machines.For(_instrument.Machine.SlotId) is not { } project) return found;
        if (ValuesFor.Instrument(_instrument) is not { } values) return found;

        foreach (string key in _order.Of(project.Panel))
        {
            if (found.Count >= Shown) break;

            if (project.Parameters.Find(one => one.Key == key) is not { } parameter) continue;

            found.Add(new DeviceReading(
                parameter.Name.Length > 0 ? parameter.Name : parameter.Key,
                Reads(values.Get(key), parameter.Unit)));
        }

        return found;
    }

    /// <summary>A value as the block prints it: the number, and its unit when it has one.</summary>
    private static string Reads(double value, string unit)
    {
        string said = value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

        return unit.Length > 0 ? said + " " + unit : said;
    }

    /// <summary>Reads the printed values again, for when something else moved them.</summary>
    public void Reread()
    {
        if (_summary is null) return;

        _summary = null;

        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(HasSummary));
    }

    /// <summary>This plugin's controls, once somebody has asked for them.</summary>
    public PluginControlsViewModel? Panel { get; private set; }

    /// <summary>True while its window is open, so the box in the strip says so.</summary>
    [ObservableProperty] private bool isOpen;

    /// <summary>
    /// Loads the plugin if it is not already playing and builds its panel. Null when the
    /// plugin will not load, which is a box that does nothing rather than a crash.
    /// </summary>
    public PluginControlsViewModel? Prepare()
    {
        if (Panel != null) return Panel;

        _plugin = _live();
        if (_plugin == null) return null;

        Panel = new PluginControlsViewModel(_plugin, Moved);

        return Panel;
    }

    /// <summary>
    /// A knob moved, in the plugin's window or in the host's. The patch is not read back here:
    /// asking a plugin for its state means asking it to write out everything it holds, which
    /// for Vital is a couple of hundred kilobytes, and doing that on every degree of a knob
    /// would make the knob stutter. It is read when the song is saved.
    /// </summary>
    private void Moved()
    {
        _changed?.Invoke();
    }

    /// <summary>
    /// Takes the sound back out of the plugin and onto the instrument, so that what is saved
    /// with the song is what is being heard.
    /// </summary>
    /// <remarks>
    /// Asked every time rather than only when something is known to have moved. The note that
    /// a knob turned is made by the plugin telling us so, and a plugin that changes without
    /// saying, or that says it before the panel exists to hear it, leaves the note unmade and
    /// the song keeping a sound nobody is listening to. Reading a patch costs a plugin one
    /// call and a couple of hundred kilobytes at worst, and this happens when a song is saved
    /// or a window is closed, not while anything is being played.
    ///
    /// A plugin that will not give its patch back keeps whatever the instrument already had.
    /// Writing nothing over something is how a saved sound gets lost.
    ///
    /// A patch is not a set of knob positions, which is why this exists at all: the wavetables,
    /// the effect rack and the preset's own name are in the lump and in none of the parameters.
    /// </remarks>
    public void SyncPatch()
    {
        if (_plugin == null) return;

        var patch = _plugin.SaveState();

        if (patch == null || patch.Length == 0) return;

        _instrument.PluginState = patch;
    }

    /// <summary>
    /// Lets go of this box for good, for a track that has been given a different instrument.
    /// </summary>
    /// <remarks>
    /// Not the same as closing the window. A designer watches the tracker so it can show where
    /// its track is, and a box nobody can reach any more must stop watching or it never stops.
    /// </remarks>
    public void Discard()
    {
        Close();
        _built?.Close();
    }

    /// <summary>Puts the plugin's window away. The plugin carries on playing.</summary>
    public void Close()
    {
        SyncPatch();

        Panel?.Close();
        IsOpen = false;
    }
}
