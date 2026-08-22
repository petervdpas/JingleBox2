using CommunityToolkit.Mvvm.ComponentModel;
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
/// Ouroboros and neither can crowd the library.
///
/// Picking one copies its settings into the instrument being edited and stops there. The name,
/// the id and the level stay, because this is still the same instrument standing in the same
/// track; it has just been given a different sound to make.
/// </remarks>
public sealed partial class InstrumentPresets : ObservableObject
{
    private readonly TrackerInstrument _instrument;
    private readonly Action _applied;

    /// <summary>True while the list is being rebuilt, so filling it does not load anything.</summary>
    private bool _filling;

    public InstrumentPresets(TrackerInstrument instrument, Action applied)
    {
        _instrument = instrument;
        _applied = applied;

        Refresh();
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

    /// <summary>Reads the machine's folder again.</summary>
    public void Refresh()
    {
        _filling = true;

        try
        {
            Items.Clear();

            foreach (var preset in MachinePresets.For(_instrument.Machine)) Items.Add(preset);

            Selected = null;
        }
        finally
        {
            _filling = false;
            OnPropertyChanged(nameof(Any));
        }
    }
}
