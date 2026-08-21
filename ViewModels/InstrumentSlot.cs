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
    public InstrumentSlot(int index, TrackerInstrument instrument)
    {
        Index = index;
        Instrument = instrument;
    }

    public int Index { get; }
    public TrackerInstrument Instrument { get; }

    public string Number => Index.ToString("00", CultureInfo.InvariantCulture);
    public string Name => Instrument.Name;
    public string BaseNoteText => Instrument.BaseNote.ToString();

    public override string ToString() => $"{Number}  {Name}";
}
