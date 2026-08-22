using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Tracker;
using System.Globalization;

namespace JingleBox2.ViewModels;

/// <summary>
/// One instrument together with the number a pattern cell uses to refer to it. The number is
/// the link between the list and the grid, so it has to be on screen next to the name.
/// </summary>
public sealed partial class InstrumentSlot : ObservableObject
{
    public InstrumentSlot(int index, TrackerInstrument instrument, int track)
    {
        Index = index;
        Instrument = instrument;
        Track = track;
    }

    public int Index { get; }
    public TrackerInstrument Instrument { get; }

    /// <summary>The track this instrument sits on, or -1 when it is not on one.</summary>
    public int Track { get; }

    [ObservableProperty] private float level;

    public string Number => Index.ToString("00", CultureInfo.InvariantCulture);
    public string Name => Instrument.Name;

    /// <summary>The second line of the row: a synth says what it is, a sample says its pitch.</summary>
    public string DetailText => Instrument.IsSynth
        ? Instrument.Patch.Wave.ToString().ToLowerInvariant() + " synth"
        : Instrument.BaseNote.ToString();

    public bool HasTrack => Track >= 0;

    /// <summary>Short form for the corner badge. Two digits covers every track a song can have.</summary>
    public string TrackBadge => HasTrack
        ? "TR-" + (Track + 1).ToString("00", CultureInfo.InvariantCulture)
        : "";

    /// <summary>Redraws the row after the editor changed the instrument behind it.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(DetailText));
    }

    public override string ToString() => $"{Number}  {Name}";
}
