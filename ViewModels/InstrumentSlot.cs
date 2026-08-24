using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Media;
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

    /// <summary>The machine's own theme, which is what everything about it is painted from.</summary>
    public MachineTheme Theme => Machine.For(Instrument.Kind).Theme;

    /// <summary>Its colour on its own, for the bar down the side of the row.</summary>
    public string Colour => Theme.Accent;

    /// <summary>The row's own wash, and the two it takes under the pointer and in hand.</summary>
    public IBrush Row => Wash(Theme.Row);
    public IBrush RowOver => Wash(Theme.RowOver);
    public IBrush RowPicked => Wash(Theme.RowPicked);

    private IBrush Wash(double amount) =>
        Views.MachineTint.Hue(Theme.Accent, out var hue)
            ? new SolidColorBrush(hue, amount)
            : Brushes.Transparent;

    /// <summary>
    /// The second line of the row: which machine it is on, and a word about how it is set.
    /// </summary>
    /// <remarks>
    /// The machine comes first, the same as on the rack, because the machine is the organising
    /// idea and because a name you chose says nothing about which panel you get when you open
    /// it. It used to say "square synth", from before there were machines at all.
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
