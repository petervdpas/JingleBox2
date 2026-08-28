using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using JingleBox2.Machines;
using System;

namespace JingleBox2.Machines.Ui;

/// <summary>
/// Where the track playing this instrument has got to: a button per page of rows, over the
/// lamps that count them.
/// </summary>
/// <remarks>
/// The pages stand over the lamps they choose, so the row reads downwards: pick a run of rows,
/// watch them. It reports and does not sequence, which is why the only thing the buttons do is
/// choose what the lamps are showing.
///
/// Lamps rather than a number, and eight of them where one figure would have done, because a
/// row is read without being read. That is the whole reason a panel spends the room.
///
/// Dimmed rather than taken away when nothing is playing it, so the panel is the same panel
/// wherever it is opened and where things are is learned once. The buttons still work while it
/// is dimmed: choosing which rows the lamps show is a thing you can do to a pattern nobody is
/// playing, and a disabled cap does not print what is written on it.
/// </remarks>
public class LocationView : StackPanel
{
    /// <summary>Where the lamps read from.</summary>
    public static readonly StyledProperty<IMachineLocation?> LocationProperty =
        AvaloniaProperty.Register<LocationView, IMachineLocation?>(nameof(Location));

    /// <summary>Written over the lamps. Empty for none.</summary>
    public static readonly StyledProperty<string?> CaptionProperty =
        AvaloniaProperty.Register<LocationView, string?>(nameof(Caption), "LOCATION");

    /// <summary>Whether the page buttons are drawn at all.</summary>
    /// <remarks>
    /// A machine with room for the lamps and none for the buttons can have the lamps alone. It
    /// then shows whichever page the playhead is on, which is what the buttons leave it doing.
    /// </remarks>
    public static readonly StyledProperty<bool> HasPagesProperty =
        AvaloniaProperty.Register<LocationView, bool>(nameof(HasPages), true);

    /// <summary>
    /// Backs <see cref="Colour"/>, what the lamps are lit in.
    /// </summary>
    /// <remarks>
    /// Amber unless a machine says otherwise, which is what a counting lamp on hardware is, and
    /// fixed rather than taken from the theme for the same reason a meter's red is fixed.
    /// </remarks>
    public static readonly StyledProperty<Color> ColourProperty =
        AvaloniaProperty.Register<LocationView, Color>(nameof(Colour), Color.FromRgb(0xE5, 0xB3, 0x39));

    /// <summary>How big one lamp is across.</summary>
    public static readonly StyledProperty<double> LampSizeProperty =
        AvaloniaProperty.Register<LocationView, double>(nameof(LampSize), 9.0);

    /// <summary>The air between one lamp and the next.</summary>
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<LocationView, double>(nameof(Gap), 9.0);

    /// <summary>How wide the page buttons are allowed to run before they wrap.</summary>
    public static readonly StyledProperty<double> PageWidthProperty =
        AvaloniaProperty.Register<LocationView, double>(nameof(PageWidth), 440.0);

    /// <summary>How much of it is left when there is no track behind it.</summary>
    private const double DimmedTo = 0.45;

    /// <summary>How tall one page button is, and how big the writing on it is.</summary>
    private const double CapHeight = 18;

    /// <inheritdoc cref="CapHeight"/>
    private const double CapFontSize = 9;

    /// <summary>The air to the right of each page button, which is what spaces the row.</summary>
    private const double CapGap = 4;

    /// <summary>
    /// The page buttons' own grey.
    /// </summary>
    /// <remarks>
    /// Not the lamps' colour: the buttons choose what is shown and the lamps report where the
    /// playhead is, and two things in one colour read as one thing.
    /// </remarks>
    private static readonly Color CapColour = Color.FromRgb(0xB0, 0xB3, 0xB8);

    /// <summary>Stacks the buttons over the lamps, and lets go of the track when it leaves the tree.</summary>
    public LocationView()
    {
        Spacing = 6;
        Orientation = Orientation.Vertical;
        HorizontalAlignment = HorizontalAlignment.Left;

        DetachedFromVisualTree += (_, _) => Unwatch();
    }

    /// <inheritdoc cref="LocationProperty"/>
    public IMachineLocation? Location
    {
        get => GetValue(LocationProperty);
        set => SetValue(LocationProperty, value);
    }

    /// <inheritdoc cref="CaptionProperty"/>
    public string? Caption
    {
        get => GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    /// <inheritdoc cref="HasPagesProperty"/>
    public bool HasPages
    {
        get => GetValue(HasPagesProperty);
        set => SetValue(HasPagesProperty, value);
    }

    /// <inheritdoc cref="ColourProperty"/>
    public Color Colour
    {
        get => GetValue(ColourProperty);
        set => SetValue(ColourProperty, value);
    }

    /// <inheritdoc cref="LampSizeProperty"/>
    public double LampSize
    {
        get => GetValue(LampSizeProperty);
        set => SetValue(LampSizeProperty, value);
    }

    /// <inheritdoc cref="GapProperty"/>
    public double Gap
    {
        get => GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    /// <inheritdoc cref="PageWidthProperty"/>
    public double PageWidth
    {
        get => GetValue(PageWidthProperty);
        set => SetValue(PageWidthProperty, value);
    }

    /// <summary>
    /// Anything about the shape of the row means building it again; the track arriving also
    /// moves the listening.
    /// </summary>
    /// <remarks>
    /// Every one of these decides how many controls there are or how big they are, so none of
    /// them can be applied to a row that is already built.
    /// </remarks>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == LocationProperty)
        {
            Unwatch();
            Watch();
        }

        if (change.Property == LocationProperty ||
            change.Property == CaptionProperty ||
            change.Property == HasPagesProperty ||
            change.Property == ColourProperty ||
            change.Property == LampSizeProperty ||
            change.Property == GapProperty ||
            change.Property == PageWidthProperty)
        {
            Rebuild();
        }
    }

    /// <summary>
    /// The track being listened to, and the handler doing it.
    /// </summary>
    /// <remarks>
    /// Both are kept so the subscription can be taken off again: the handler is a closure rather
    /// than a method, so it is not the same delegate twice and could not be unsubscribed without
    /// having been held on to.
    /// </remarks>
    private IMachineLocation? _watching;

    /// <inheritdoc cref="_watching"/>
    private EventHandler? _listening;

    /// <summary>Starts following whichever track has just arrived.</summary>
    private void Watch()
    {
        if (Location is not { } place) return;

        _watching = place;
        _listening = (_, _) => Follow();

        place.Changed += _listening;
    }

    /// <summary>Stops following, so nothing here keeps a track alive after the panel has gone.</summary>
    private void Unwatch()
    {
        if (_watching != null && _listening != null) _watching.Changed -= _listening;

        _watching = null;
        _listening = null;
    }

    /// <summary>
    /// The two halves of the row, held so the playhead can be followed without rebuilding.
    /// </summary>
    /// <remarks>
    /// The page buttons are null on a machine that asked for none, and both are null between a
    /// rebuild starting and the new controls being added.
    /// </remarks>
    private WrapPanel? _pages;

    /// <inheritdoc cref="_pages"/>
    private LedRow? _lamps;

    /// <summary>
    /// Builds the row, which is a page count's worth of buttons and one lamp row.
    /// </summary>
    /// <remarks>
    /// Built once and then only followed, because the pattern's length is what decides how many
    /// buttons there are and that changes far less often than the playhead does.
    /// </remarks>
    private void Rebuild()
    {
        Children.Clear();

        _pages = null;
        _lamps = null;

        var place = Location;

        Opacity = place?.Live == false ? DimmedTo : 1;

        if (HasPages)
        {
            _pages = new WrapPanel
            {
                Orientation = Orientation.Horizontal,
                MaxWidth = PageWidth,
                HorizontalAlignment = HorizontalAlignment.Left,
            };

            Children.Add(_pages);
        }

        _lamps = new LedRow
        {
            Caption = Caption,
            Colour = Colour,
            Size = LampSize,
            Gap = Gap,
            Count = Math.Max(1, place?.Lamps ?? 8),
            HorizontalAlignment = HorizontalAlignment.Left,
        };

        Children.Add(_lamps);

        Pages();
        Follow();
    }

    /// <summary>One button per page, with its lamp under the cap the way the machine has it.</summary>
    private void Pages()
    {
        if (_pages is null || Location is not { } place) return;

        _pages.Children.Clear();

        for (int at = 0; at < place.Pages.Count; at++)
        {
            int index = at;

            var cap = new PushButton
            {
                CapText = place.Pages[at],
                CapHeight = CapHeight,
                FontSize = CapFontSize,
                Colour = CapColour,
                HasLamp = true,
                LampBelow = true,
                Margin = new Thickness(0, 0, CapGap, 0),
            };

            cap.Pressed += (_, _) => place.Show(index);

            _pages.Children.Add(cap);
        }
    }

    /// <summary>What moves while it is up: which page is shown and which lamp is lit.</summary>
    /// <remarks>
    /// The buttons are counted rather than rebuilt each time, since the pattern changing length
    /// is the one thing that adds a button or takes one away, and that is rare next to the
    /// playhead moving.
    /// </remarks>
    private void Follow()
    {
        if (Location is not { } place) return;

        if (_lamps is { } lamps)
        {
            if (lamps.Count != Math.Max(1, place.Lamps)) lamps.Count = Math.Max(1, place.Lamps);

            lamps.FirstNumber = place.FirstNumber;
            lamps.Selected = place.Lit;
        }

        if (_pages is null) return;

        if (_pages.Children.Count != place.Pages.Count)
        {
            Pages();

            return;
        }

        for (int at = 0; at < _pages.Children.Count; at++)
            if (_pages.Children[at] is PushButton cap)
                cap.Lit = at == place.Page;
    }
}
