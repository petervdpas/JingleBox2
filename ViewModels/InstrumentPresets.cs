using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using JingleBox2.Tracker;
using System;
using System.Collections.ObjectModel;

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
public sealed partial class InstrumentPresets : ObservableObject
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

    public InstrumentPresets(
        TrackerInstrument instrument,
        Action applied,
        ObservableCollection<Models.Recording>? takes = null)
    {
        _instrument = instrument;
        _applied = applied;
        _takes = takes;

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
    public bool PicksTakes =>
        _takes != null && _instrument.Kind == TrackerInstrumentKind.Sample;

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
        }
    }
}
