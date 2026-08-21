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

    public string Number => Index.ToString("00", CultureInfo.InvariantCulture);
    public string Name => Instrument.Name;
    public string BaseNoteText => Instrument.BaseNote.ToString();

    public bool HasTrack => Track >= 0;

    public string TrackText => HasTrack
        ? "Track " + (Track + 1).ToString("00", CultureInfo.InvariantCulture)
        : "not on a track";

    public override string ToString() => $"{Number}  {Name}";
}
