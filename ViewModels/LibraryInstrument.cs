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

    /// <summary>
    /// The second line: which machine this instrument is on, and one word about how it is set.
    /// </summary>
    /// <remarks>
    /// The machine comes first because the machine is the organising idea. "Bass" on its own
    /// says nothing about what you would be editing if you opened it; "OddSkilla, square" says
    /// which panel you will get and roughly what it is doing. A plugin names itself instead,
    /// since which plugin it is matters more than the fact that it is one.
    /// </remarks>
    public string DetailText
    {
        get
        {
            if (Instrument.IsPlugin)
                return Instrument.PluginName is { Length: > 0 } plugin ? plugin : "Plugin";

            string machine = Instrument.Machine.Name;

            return Instrument.Kind switch
            {
                TrackerInstrumentKind.Synth => machine + ", " + Instrument.Patch.Wave.ToString().ToLowerInvariant(),
                TrackerInstrumentKind.Ouroboros => machine + ", " + (Instrument.Ouroboros?.Wave.ToString().ToLowerInvariant() ?? "saw"),
                _ => machine + ", " + Instrument.BaseNote
            };
        }
    }

    /// <summary>Redraws the row after the editor changed the instrument behind it.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(DetailText));
    }

    public override string ToString() => Name;
}
