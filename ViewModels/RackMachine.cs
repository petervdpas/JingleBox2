using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Media;
using JingleBox2.Tracker;
using JingleBox2.Machines;
using JingleBox2.Tracker.Enums;
using JingleBox2.Machines.Records;
using JingleBox2.Tracker.Records;
using JingleBox2.Views.Interfaces;
using JingleBox2.Views;

namespace JingleBox2.ViewModels;

/// <summary>One row in the instrument rack. No number: a rack has no cell to answer to.</summary>
public sealed partial class RackMachine : ObservableObject
{
    /// <summary>A machine's colour mixed into the theme's. Holds nothing, so one is enough.</summary>
    private readonly IMachineTint _tint = new MachineTint();

    /// <summary>Shows one instrument off the rack. The instrument itself is held, not copied.</summary>
    public RackMachine(TrackerInstrument instrument) => Instrument = instrument;

    /// <summary>
    /// The instrument this row is about, which is the rack's own object rather than a copy.
    /// </summary>
    /// <remarks>
    /// The editor edits that instrument in place, which is why <see cref="Refresh"/> exists:
    /// the row has nothing of its own to update, it only has to be told to read again.
    /// </remarks>
    public TrackerInstrument Instrument { get; }

    /// <summary>Its id, which is the name of its file under the instruments folder.</summary>
    public string Id => Instrument.Id;

    /// <summary>What it is called. A machine's own slot is called what the machine is called.</summary>
    public string Name => Instrument.Name;

    /// <summary>The machine's own theme, which is what everything about it is painted from.</summary>
    public MachineTheme Theme => Machine.For(Instrument.Kind).Theme;

    /// <summary>Its colour on its own, for the bar down the side of the row.</summary>
    public string Colour => Theme.Accent;

    /// <summary>The row's own wash: the machine's colour at the weight its theme asks for.</summary>
    public IBrush Row => Wash(Theme.Row);

    /// <summary>The same colour, heavier, for the row under the pointer.</summary>
    public IBrush RowOver => Wash(Theme.RowOver);

    /// <summary>And heavier again for the row that is picked.</summary>
    public IBrush RowPicked => Wash(Theme.RowPicked);

    /// <summary>
    /// The machine's accent at a given weight, or nothing at all when the theme's colour cannot
    /// be read: a row painted a colour nobody chose is worse than a row painted none.
    /// </summary>
    private IBrush Wash(double amount) =>
        _tint.Hue(Theme.Accent, out var hue)
            ? new SolidColorBrush(hue, amount)
            : Brushes.Transparent;

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
                TrackerInstrumentKind.MonoSynth => machine + ", " + (Instrument.MonoSynth?.Wave.ToString().ToLowerInvariant() ?? "saw"),
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

    /// <summary>Its name, so a list that was handed rows rather than text still reads.</summary>
    public override string ToString() => Name;
}
