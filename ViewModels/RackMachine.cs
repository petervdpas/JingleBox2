using CommunityToolkit.Mvvm.ComponentModel;
using JingleBox2.Tracker;

namespace JingleBox2.ViewModels;

/// <summary>One row in the instrument rack. No number: a rack has no cell to answer to.</summary>
public sealed partial class RackMachine : ObservableObject
{
    public RackMachine(TrackerInstrument instrument) => Instrument = instrument;

    public TrackerInstrument Instrument { get; }

    public string Id => Instrument.Id;

    public string Name => Instrument.Name;

    /// <summary>
    /// True for a machine's own slot: always there, called what the machine is called, and
    /// neither renamed nor deleted.
    /// </summary>
    public bool IsSlot => Machine.IsSlot(Id);

    /// <summary>False for a slot, which cannot be deleted. A plugin can.</summary>
    public bool IsYours => !IsSlot;

    /// <summary>
    /// False for anything that is called what something else calls it: a machine's slot, and a
    /// plugin, which takes its name from the VST3 or CLAP itself.
    /// </summary>
    public bool CanRename => !IsSlot && !Instrument.IsPlugin;

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
