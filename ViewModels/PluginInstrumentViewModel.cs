using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Audio.Plugins;
using JingleBox2.Tracker;
using System;

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
    private readonly TrackerInstrument _instrument;
    private readonly Func<IPluginInstrument?> _live;
    private readonly Action? _changed;
    private readonly Func<TrackInstrumentDesigner>? _designer;

    private IPluginInstrument? _plugin;

    public PluginInstrumentViewModel(
        TrackerInstrument instrument,
        Func<IPluginInstrument?> live,
        Action? changed = null,
        Func<TrackInstrumentDesigner>? designer = null)
    {
        _instrument = instrument;
        _live = live;
        _changed = changed;
        _designer = designer;
    }

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

    private TrackInstrumentDesigner? _built;

    public string Name => _instrument.Name;

    /// <summary>What the plugin is called, for a window title that says which is which.</summary>
    public string Title => _instrument.Plugin?.Name is { Length: > 0 } plugin && plugin != _instrument.Name
        ? _instrument.Name + " (" + plugin + ")"
        : _instrument.Name;

    /// <summary>The instrument this stands for, so the strip can tell one from another.</summary>
    public TrackerInstrument Instrument => _instrument;

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
    /// </remarks>
    public void SyncPatch()
    {
        if (_plugin == null) return;

        var patch = _plugin.SaveState();

        // A plugin that will not give its patch back keeps whatever the instrument already
        // had. Writing nothing over something is how a saved sound gets lost.
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
