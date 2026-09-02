using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Media;
using JingleBox2.Tracker;
using JingleBox2.Rack.Faces.Records;
using JingleBox2.Views.Interfaces;
using JingleBox2.Views;
using JingleBox2.ViewModels.Interfaces;
using JingleBox2.Devices.SoundMachines.Records;

namespace JingleBox2.ViewModels;

/// <summary>One row in the instrument rack. No number: a rack has no cell to answer to.</summary>
public sealed partial class RackMachine : ObservableObject, IRackRow
{
    /// <summary>A machine's colour mixed into the theme's. Holds nothing, so one is enough.</summary>
    private readonly IPanelTint _tint = new PanelTint();

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
    public PanelTheme Theme => SoundMachine.For(Instrument.Kind).Theme;

    /// <summary>Its colour on its own, for the bar down the side of the row.</summary>
    public string Colour => Theme.Accent;

    /// <summary>The row's own wash: the machine's colour at the weight its theme asks for.</summary>
    public IBrush Row => Wash(Theme.Row);

    /// <summary>The same colour, heavier, for the row under the pointer.</summary>
    public IBrush RowOver => Wash(Theme.RowOver);

    /// <summary>And heavier again for the row that is picked.</summary>
    public IBrush RowPicked => Wash(Theme.RowPicked);

    /// <summary>The machine's accent at a given weight, which the tint is the one rule for.</summary>
    private IBrush Wash(double amount) => _tint.Wash(Theme.Accent, amount);

    /// <summary>
    /// True for a machine's own slot: always there, called what the machine is called, and
    /// neither renamed nor deleted.
    /// </summary>
    public bool IsSlot => SoundMachine.IsSlot(Id);

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
    /// The instrument's own answer, not a second one written here. It was written out twice
    /// before, once on the instrument and once on this row, and the two had already drifted: an
    /// effect said the same thing a plugin instrument did, since only one of the two copies had
    /// been told that effects exist. Two lists printing one instrument have to agree, and the
    /// only way they can be made to is by there being one sentence.
    /// </remarks>
    public string DetailText => Instrument.Detail;

    /// <summary>Redraws the row after the editor changed the instrument behind it.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(DetailText));
    }

    /// <summary>Its name, so a list that was handed rows rather than text still reads.</summary>
    public override string ToString() => Name;
}
