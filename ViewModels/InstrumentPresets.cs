using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Tracker;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using JingleBox2.Machines;

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
public sealed partial class InstrumentPresets : ObservableObject, IMachinePresets
{
    private readonly TrackerInstrument _instrument;
    private readonly Action _applied;

    /// <summary>
    /// Your recordings, for the one machine whose starting points are recordings.
    /// </summary>
    /// <remarks>
    /// The Recording machine has no settings worth shipping: what it is, is the take on it. So
    /// its presets are your takes, and picking one puts it on the machine. Every other machine
    /// reads its own folder beside the program.
    /// </remarks>
    private readonly ObservableCollection<Models.Recording>? _takes;

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

    public InstrumentPresets(
        TrackerInstrument instrument,
        Action applied,
        ObservableCollection<Models.Recording>? takes = null,
        TakeFilter? narrowing = null)
    {
        _instrument = instrument;
        _applied = applied;
        _takes = takes;
        _narrowing = narrowing;

        if (_takes != null) _takes.CollectionChanged += (_, _) => Refresh();

        Refresh();
    }

    /// <summary>What the picker is called, since on one machine it is offering something else.</summary>
    public string Caption => PicksTakes ? "Take" : "Preset";

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
    /// which is the state the rack is in while they move over one at a time.
    /// </remarks>
    private bool StartsFromTakes()
    {
        string id = Machine.For(_instrument.Kind).SlotId;

        if (Tracker.Machines.MachineProjects.For(id)?.BrowsesTakes() is { } said) return said;

        // The machine says nothing, either because it is not installed as a project or because
        // it was installed before it could say. The kind is what decided before this field.
        return _instrument.Kind == TrackerInstrumentKind.Sample;
    }

    /// <summary>What this machine has to offer, in the order its folder lists them.</summary>
    public ObservableCollection<MachinePreset> Items { get; } = new();

    /// <summary>True when there is anything to pick, so the panel can grey the picker.</summary>
    public bool Any => Items.Count > 0;

    /// <summary>The last one loaded. Setting it loads it.</summary>
    [ObservableProperty] private MachinePreset? selected;

    partial void OnSelectedChanged(MachinePreset? value)
    {
        if (_filling || value == null) return;

        _instrument.TakeSoundFrom(value.Sound);
        _applied();
    }

    /// <summary>A recording, dressed as a preset so one picker serves every machine.</summary>
    private MachinePreset Take(Models.Recording recording)
    {
        var sound = TrackerInstrument.CreateSample(recording.Name, recording.FilePath, _instrument.BaseNote);

        // The take is the whole of what changes: everything else about the machine stays put.
        sound.Shape = null;

        return new MachinePreset(recording.Name, sound);
    }

    /// <summary>Reads the machine's folder again.</summary>
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
                foreach (var preset in MachinePresets.For(_instrument.Machine)) Items.Add(preset);
            }

            Selected = null;
        }
        finally
        {
            _filling = false;

            OnPropertyChanged(nameof(Any));
            OnPropertyChanged(nameof(Caption));
            OnPropertyChanged(nameof(PicksTakes));
            OnPropertyChanged(nameof(Hint));
            OnPropertyChanged(nameof(Items));
        }
    }

    /// <summary>What is on offer, by name, for a panel that draws its own picker.</summary>
    /// <remarks>
    /// The same list the hand written picker shows, said as plain strings, because a machine
    /// described in a file has no way of knowing what a preset object is. Which one is picked
    /// travels back as a number for the same reason.
    /// </remarks>
    IReadOnlyList<string> IMachinePresets.Names => Items.Select(one => one.Name).ToList();

    /// <summary>Which one is showing, or -1 for none. Setting it loads that one.</summary>
    int IMachinePresets.Picked
    {
        get => Selected == null ? -1 : Items.IndexOf(Selected);
        set
        {
            if (value < 0 || value >= Items.Count) return;

            Selected = Items[value];
        }
    }

    /// <summary>The categories the takes are filed under, or none on a machine offering presets.</summary>
    IReadOnlyList<string> IMachinePresets.Filters =>
        PicksTakes && _narrowing != null ? _narrowing.Filters.ToList() : Array.Empty<string>();

    /// <summary>Which category is in force. Setting it narrows what is on offer.</summary>
    string IMachinePresets.Filter
    {
        get => _narrowing?.Filter ?? "";
        set
        {
            if (_narrowing == null || value.Length == 0) return;

            _narrowing.Filter = value;
        }
    }
}
