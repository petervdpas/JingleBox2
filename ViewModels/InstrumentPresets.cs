using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Tracker;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JingleBox2.Tracker.Enums;
using JingleBox2.Rack.SoundDevices.Faces.Interfaces;
using JingleBox2.SoundDevices.SoundMachines.Interfaces;
using JingleBox2.SoundDevices.SoundMachines.Records;
using JingleBox2.SoundDevices.SoundMachines;

namespace JingleBox2.ViewModels;

/// <summary>
/// The presets a machine offers, read from the machine's own folder.
/// </summary>
/// <remarks>
/// Presets belong to the machine and are not instruments: they ship with the program, they are
/// never on your shelf, and picking one has nothing to do with them afterwards. Every machine
/// has a folder of its own beside the program, so OddSkilla's presets can never turn up on
/// Ouroboros and neither can crowd the rack.
///
/// Picking one copies its settings into the instrument being edited and stops there. The name,
/// the id and the level stay, because this is still the same instrument standing in the same
/// track; it has just been given a different sound to make.
/// </remarks>
public sealed partial class InstrumentPresets : ObservableObject, IPanelPresets, Interfaces.IPresetKeeping
{
    /// <summary>The machines this run has.</summary>
    private readonly ISoundMachineProjects _machines;

    /// <summary>The presets those machines ship with.</summary>
    private readonly IPresetLibrary _presets;

    /// <summary>The instrument a picked preset is poured into.</summary>
    private readonly TrackerInstrument _instrument;

    /// <summary>Told after a preset has been put on, so the panel and the sound catch up.</summary>
    private readonly Action _applied;

    /// <summary>
    /// Your recordings, for the one machine whose starting points are recordings.
    /// </summary>
    /// <remarks>
    /// The Recording machine has no settings worth shipping: what it is, is the take on it. So
    /// its presets are your takes, and picking one puts it on the machine. Every other machine
    /// reads its own folder beside the program.
    /// </remarks>
    private readonly ObservableCollection<Audio.Records.Recording>? _takes;

    /// <summary>True while the list is being rebuilt, so filling it does not load anything.</summary>
    private bool _filling;

    /// <summary>
    /// What the takes are being narrowed by, when the list is takes.
    /// </summary>
    /// <remarks>
    /// Held so that a panel drawn from a machine's own description can offer the categories
    /// itself. The hand written panel puts the category picker beside this one and works the
    /// filter directly; a described panel has one control where those are two, and asks here.
    /// </remarks>
    private readonly TakeFilter? _narrowing;

    /// <summary>Reads what this machine has to offer and watches the takes if it offers those.</summary>
    /// <param name="instrument">The instrument being edited, which is what a pick writes into.</param>
    /// <param name="applied">Called once a preset has been put on.</param>
    /// <param name="takes">Your recordings, for the one machine whose presets are recordings.</param>
    /// <param name="narrowing">
    /// The category filter in front of the takes, so a described panel can offer the categories
    /// through the one control it has.
    /// </param>
    /// <param name="machines">
    /// The machines this run has, the one instance everything shares. Required rather than
    /// defaulted: a fresh one is empty, so a default would draw blank panels and report every
    /// machine missing, without an error anywhere to say why.
    /// </param>
    /// <param name="library">Where the presets are read from and kept. Left out, each machine's own folder.</param>
    public InstrumentPresets(
        TrackerInstrument instrument,
        Action applied,
        ISoundMachineProjects machines,
        ObservableCollection<Audio.Records.Recording>? takes = null,
        TakeFilter? narrowing = null,
        IPresetLibrary? library = null)
    {
        _machines = machines;
        _presets = library ?? new SoundMachinePresets(machines);
        _instrument = instrument;
        _applied = applied;
        _takes = takes;
        _narrowing = narrowing;

        if (_takes != null) _takes.CollectionChanged += (_, _) => Refresh();

        Refresh();
    }

    /// <summary>What the picker is called, since on one machine it is offering something else.</summary>
    public string Caption => PicksTakes ? "Take" : "Preset";

    /// <summary>What the picker says when you rest on it, which differs for the same reason.</summary>
    public string Hint => PicksTakes
        ? "One of your recordings, put straight on this machine."
        : "Loads the settings of another sound on this machine. Your name and level are kept; only the sound is replaced.";

    /// <summary>True on the one machine whose starting points are your own recordings.</summary>
    /// <remarks>
    /// Asked from outside as well, since the category filter in front of the picker is about
    /// takes and there is nothing to narrow on a machine offering its own presets.
    /// </remarks>
    public bool PicksTakes => _takes != null && StartsFromTakes();

    /// <summary>
    /// Whether this machine says its picker offers your recordings rather than its own presets.
    /// </summary>
    /// <remarks>
    /// The machine says so, in its own file. It used to be asked of the instrument's kind, which
    /// is the app naming one of its own machines: a machine somebody else built would have had
    /// no way of asking for the same treatment, however plainly it was nothing but the recording
    /// on it.
    ///
    /// The kind is still the answer for a machine that has not been converted to a project yet,
    /// which is the state the rack is in while they move over one at a time, and for one
    /// installed before it could say anything about this.
    /// </remarks>
    private bool StartsFromTakes()
    {
        string id = _instrument.Machine.SlotId;

        if (_machines.For(id)?.BrowsesTakes() is { } said) return said;

        return _instrument.Kind == TrackerInstrumentKind.Sample;
    }

    /// <summary>What this machine has to offer, in the order its folder lists them.</summary>
    public ObservableCollection<SoundMachinePreset> Items { get; } = new();

    /// <summary>True when there is anything to pick, so the panel can grey the picker.</summary>
    public bool Any => Items.Count > 0;

    /// <summary>The last one loaded. Setting it loads it.</summary>
    [ObservableProperty] private SoundMachinePreset? selected;

    /// <summary>
    /// Puts the picked sound on the instrument, and says so.
    /// </summary>
    /// <remarks>
    /// Nothing happens while the list is being rebuilt: the rebuild clears the selection and would
    /// otherwise be read as somebody picking nothing, and then picking the first thing again.
    /// </remarks>
    partial void OnSelectedChanged(SoundMachinePreset? value)
    {
        if (_filling || value == null) return;

        _instrument.Preset = value.Shown;
        _instrument.TakeSoundFrom(value.Sound);
        _applied();
    }

    /// <summary>A recording, dressed as a preset so one picker serves every machine.</summary>
    /// <remarks>
    /// The take is the whole of what changes. The shape is cleared so that picking a recording
    /// leaves everything else about the machine where it was, which is the same promise a real
    /// preset makes about the name and the level.
    /// </remarks>
    private SoundMachinePreset Take(Audio.Records.Recording recording)
    {
        var sound = TrackerInstrument.CreateSample(recording.Name, recording.FilePath, _instrument.BaseNote);

        sound.Shape = null;

        return new SoundMachinePreset(recording.Name, sound);
    }

    /// <summary>Reads the machine's folder again, or your takes on the machine that offers those.</summary>
    /// <remarks>
    /// Everything worked out from the list is said at the end rather than as the list is filled,
    /// since none of it is worth a message per entry and a picker redrawn a hundred times shows
    /// exactly what one redraw would.
    /// </remarks>
    public void Refresh()
    {
        _filling = true;

        try
        {
            Items.Clear();

            if (PicksTakes)
            {
                foreach (var recording in _takes!)
                    if (recording.FilePath.Length > 0) Items.Add(Take(recording));
            }
            else
            {
                foreach (var preset in _presets.For(_instrument.Machine)) Items.Add(preset);
            }

            /* The one the instrument says was picked, which is what a song opened again shows. By
               the name the picker shows, which is what the instrument keeps. */
            Selected = _instrument.Preset is { Length: > 0 } said
                ? Items.FirstOrDefault(one => string.Equals(one.Shown, said, StringComparison.Ordinal))
                : null;
        }
        finally
        {
            _filling = false;

            OnPropertyChanged(nameof(Any));
            OnPropertyChanged(nameof(Caption));
            OnPropertyChanged(nameof(PicksTakes));
            OnPropertyChanged(nameof(Hint));
            OnPropertyChanged(nameof(Items));
            OnPropertyChanged(nameof(IPanelPresets.Names));
        }
    }

    /// <summary>What is on offer, by name, for a panel that draws its own picker.</summary>
    /// <remarks>
    /// The same list the hand written picker shows, said as plain strings and with yours marked,
    /// because a machine described in a file has no way of knowing what a preset object is.
    /// Which one is picked travels back as a number for the same reason.
    /// </remarks>
    IReadOnlyList<string> IPanelPresets.Names => Items.Select(one => one.Shown).ToList();

    /// <inheritdoc/>
    /// <remarks>
    /// Not on the machine whose starting points are your recordings, since what it would keep is
    /// a take that is already on your shelf; and not on a machine that is not installed here,
    /// which has no folder to keep one in.
    /// </remarks>
    public bool CanKeep => !PicksTakes && _machines.For(_instrument.Machine.SlotId) is not null;

    /// <inheritdoc/>
    public string DeviceName => _instrument.Machine.Name;

    /// <inheritdoc/>
    /// <remarks>
    /// One of yours offers its own name, so keeping again is saving over it, which is the ordinary
    /// thing after a tweak. One the machine ships with cannot be kept under, so the instrument's
    /// name is offered instead.
    /// </remarks>
    public string Suggested => Selected is { Yours: true } yours ? yours.Name : _instrument.Name;

    /// <inheritdoc/>
    public string? PickedYours => Selected is { Yours: true } yours ? yours.Name : null;

    /// <inheritdoc/>
    public string Refusal(string name) =>
        CanKeep ? _presets.Refusal(_instrument.Machine, name) : "This machine has no presets of its own to keep one beside.";

    /// <inheritdoc/>
    public bool Replaces(string name) => _presets.Yours(_instrument.Machine, name) is not null;

    /// <inheritdoc/>
    /// <remarks>
    /// The kept preset is put on the instrument straight away, so it plays the copies of its
    /// recordings in the preset's own folder rather than the ones on the shelf: an edit to a wave
    /// there is then heard here. Shown as picked without being put on a second time.
    /// </remarks>
    public bool Keep(string name)
    {
        if (!CanKeep || _presets.Keep(_instrument.Machine, _instrument, name) is not { } kept) return false;

        _instrument.TakeSoundFrom(kept.Sound);
        _applied();

        Refresh();
        Quietly(kept.File);

        _instrument.Preset = Selected?.Shown;

        return true;
    }

    /// <inheritdoc/>
    public bool RemovePicked()
    {
        if (Selected is not { Yours: true } yours || !_presets.Remove(_instrument.Machine, yours)) return false;

        _instrument.Preset = null;

        Refresh();

        return true;
    }

    /// <summary>Shows the preset read from that file as the one picked, without putting it on.</summary>
    /// <param name="file">The file it was read from.</param>
    private void Quietly(string file)
    {
        _filling = true;

        try
        {
            Selected = Items.FirstOrDefault(one => string.Equals(one.File, file, StringComparison.Ordinal));
        }
        finally
        {
            _filling = false;
        }

        OnPropertyChanged(nameof(IPanelPresets.Names));
    }

    /// <summary>Which one is showing, or -1 for none. Setting it loads that one.</summary>
    int IPanelPresets.Picked
    {
        get => Selected == null ? -1 : Items.IndexOf(Selected);
        set
        {
            if (value < 0 || value >= Items.Count) return;

            Selected = Items[value];
        }
    }

    /// <summary>The categories the takes are filed under, or none on a machine offering presets.</summary>
    IReadOnlyList<string> IPanelPresets.Filters =>
        PicksTakes && _narrowing != null ? _narrowing.Filters.ToList() : Array.Empty<string>();

    /// <summary>Which category is in force. Setting it narrows what is on offer.</summary>
    string IPanelPresets.Filter
    {
        get => _narrowing?.Filter ?? "";
        set
        {
            if (_narrowing == null || value.Length == 0) return;

            _narrowing.Filter = value;
        }
    }
}
