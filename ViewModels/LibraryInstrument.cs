using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Tracker;

namespace JingleBox2.ViewModels;

/// <summary>One row in the instrument library. No number: a library has no cell to answer to.</summary>
public sealed partial class LibraryInstrument : ObservableObject
{
    public LibraryInstrument(TrackerInstrument instrument) => Instrument = instrument;

    public TrackerInstrument Instrument { get; }

    public string Id => Instrument.Id;

    public string Name => Instrument.Name;

    /// <summary>The second line: a synth says what it is, a sample says its pitch.</summary>
    public string DetailText => Instrument.IsSynth
        ? Instrument.Patch.Wave.ToString().ToLowerInvariant() + " synth"
        : Instrument.BaseNote.ToString();

    /// <summary>Redraws the row after the editor changed the instrument behind it.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(DetailText));
    }

    public override string ToString() => Name;
}
