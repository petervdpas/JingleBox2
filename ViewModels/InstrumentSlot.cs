using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Media;
using JingleBox2.Tracker;
using System.Globalization;
using JingleBox2.Rack.SoundDevices.Faces.Records;
using JingleBox2.Views.Interfaces;
using JingleBox2.Views;

namespace JingleBox2.ViewModels;

/// <summary>
/// One instrument together with the number a pattern cell uses to refer to it. The number is
/// the link between the list and the grid, so it has to be on screen next to the name.
/// </summary>
public sealed partial class InstrumentSlot : ObservableObject
{
    /// <summary>A machine's colour mixed into the theme's. Holds nothing, so one is enough.</summary>
    private readonly IPanelTint _tint = new PanelTint();

    /// <summary>Builds one row of the instrument list.</summary>
    /// <param name="index">The number a pattern cell writes to reach this instrument.</param>
    /// <param name="instrument">The instrument itself, held rather than copied.</param>
    /// <param name="track">The track playing it, or -1 when no track does.</param>
    /// <param name="offered">
    /// Whether this installation is offering the machine it is on, which is registered and on
    /// the rack. Left out, yes, which is what a caller with no rack wants.
    /// </param>
    public InstrumentSlot(int index, TrackerInstrument instrument, int track, bool offered = true)
    {
        Index = index;
        Instrument = instrument;
        Track = track;
        _offered = offered;
    }

    /// <summary>Whether this installation is offering the machine this instrument is on.</summary>
    private readonly bool _offered;

    /// <summary>Its place in the song's list, which is what a cell's instrument column holds.</summary>
    public int Index { get; }

    /// <summary>The instrument this row stands for, live rather than a description of one.</summary>
    public TrackerInstrument Instrument { get; }

    /// <summary>The track this instrument sits on, or -1 when it is not on one.</summary>
    public int Track { get; }

    /// <summary>How loud this instrument is sounding, for the little meter on the row.</summary>
    [ObservableProperty] private float level;

    /// <summary>The index as the pattern prints it, two digits so the column does not jump.</summary>
    public string Number => Index.ToString("00", CultureInfo.InvariantCulture);

    /// <summary>The name you gave it in this song.</summary>
    public string Name => Instrument.Name;

    /// <summary>The machine's own theme, which is what everything about it is painted from.</summary>
    /// <remarks>
    /// Grey where the machine is not being offered, whether it was never registered or has been
    /// taken off the rack: both mean the instrument is silent and has no panel, so both read as
    /// absent rather than as an ordinary instrument that happens not to sound.
    /// </remarks>
    public PanelTheme Theme =>
        _offered ? Instrument.Machine.Theme : JingleBox2.SoundDevices.SoundMachines.Records.SoundMachine.Absent;

    /// <summary>Its colour on its own, for the bar down the side of the row.</summary>
    public string Colour => Theme.Accent;

    /// <summary>The row's own wash, and the two it takes under the pointer and in hand.</summary>
    public IBrush Row => Wash(Theme.Row);

    /// <inheritdoc cref="Row"/>
    public IBrush RowOver => Wash(Theme.RowOver);

    /// <inheritdoc cref="Row"/>
    public IBrush RowPicked => Wash(Theme.RowPicked);

    /// <summary>
    /// The machine's colour at the given strength, or nothing at all when it cannot be read.
    /// </summary>
    /// <remarks>
    /// Transparent rather than a guessed grey, because a row with no colour behind it is the
    /// list's own background and reads as an ordinary row; a grey would read as a state.
    /// </remarks>
    private IBrush Wash(double amount) =>
        _tint.Hue(Theme.Accent, out var hue)
            ? new SolidColorBrush(hue, amount)
            : Brushes.Transparent;

    /// <summary>The second line of the row: which machine it is on, and how it is set.</summary>
    /// <remarks>
    /// The instrument's own sentence, said here rather than worked out here. The block at the
    /// head of a track's chain says the same one.
    /// </remarks>
    public string DetailText => Instrument.Detail;

    /// <summary>True when a track plays this instrument, which is what puts the badge on the row.</summary>
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

    /// <summary>The number and the name, which is what a list with no template shows.</summary>
    public override string ToString() => $"{Number}  {Name}";
}
