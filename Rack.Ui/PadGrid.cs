using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using JingleBox2.Rack.Machines.Interfaces;
using JingleBox2.Rack.Ui.Records;

namespace JingleBox2.Rack.Ui;

/// <summary>
/// The pads of a kit, laid out as they are on the machine.
/// </summary>
/// <remarks>
/// Buttons and nothing more clever. A pad is a button that says what is on it, lights while it
/// is sounding, and is the one the controls beside the grid are about once it has been pressed:
/// exactly what a drum machine's pads have always done.
///
/// It builds its buttons once and then only tells them what to say, because the lighting moves
/// as fast as the music does. Rebuilding sixteen controls on every note would blink the whole
/// grid, and the pads are the part of the panel somebody is watching while they play.
///
/// Which pads there are is not described and cannot be: it is the kit, and the kit is whoever's
/// song this is. So the grid is handed a <see cref="IMachinePads"/> and the machine says only
/// how many across and how big.
/// </remarks>
public class PadGrid : Decorator
{
    /// <summary>Backs <see cref="Pads"/>, the kit behind the grid.</summary>
    public static readonly StyledProperty<IMachinePads?> PadsProperty =
        AvaloniaProperty.Register<PadGrid, IMachinePads?>(nameof(Pads));

    /// <summary>
    /// Backs <see cref="Columns"/>: how many pads stand side by side. Four, on every drum
    /// machine ever made.
    /// </summary>
    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<PadGrid, int>(nameof(Columns), 4);

    /// <summary>Backs <see cref="CapWidth"/> and <see cref="CapHeight"/>: how big one pad is.</summary>
    public static readonly StyledProperty<double> CapWidthProperty =
        AvaloniaProperty.Register<PadGrid, double>(nameof(CapWidth), 86);

    /// <inheritdoc cref="CapWidthProperty"/>
    public static readonly StyledProperty<double> CapHeightProperty =
        AvaloniaProperty.Register<PadGrid, double>(nameof(CapHeight), 42);

    /// <summary>Backs <see cref="Gap"/>, the air between one pad and the next.</summary>
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<PadGrid, double>(nameof(Gap), 5);

    /// <summary>
    /// The buttons the machine declares: a name and a key apiece.
    /// </summary>
    /// <remarks>
    /// How many there are and what they are called is the machine's, so a kit of eight and a kit
    /// of twenty four are two descriptions rather than two programs. Empty means however many the
    /// host has, which is what a machine written before the buttons existed expects.
    /// </remarks>
    public static readonly StyledProperty<IReadOnlyList<PadCell>?> CellsProperty =
        AvaloniaProperty.Register<PadGrid, IReadOnlyList<PadCell>?>(nameof(Cells));

    /// <summary>
    /// Backs <see cref="Colour"/>: what an unlit pad is painted, before the machine's own colour
    /// is put on it.
    /// </summary>
    public static readonly StyledProperty<Color> ColourProperty =
        AvaloniaProperty.Register<PadGrid, Color>(nameof(Colour), Color.FromRgb(0x4A, 0x4E, 0x57));

    /// <summary>
    /// What the buttons are laid out in.
    /// </summary>
    /// <remarks>
    /// A grid rather than a wrapping row, so a kit whose last row is short still has its pads
    /// under the ones above them. A hand reaching for the bottom left pad is reaching for a
    /// place, not for the twelfth item in a list.
    /// </remarks>
    private readonly Grid _grid = new();

    /// <summary>The buttons, in the order they were declared, so a note can find its own.</summary>
    private readonly List<PushButton> _caps = new();

    /// <summary>
    /// The buttons themselves, in the order they were declared.
    /// </summary>
    /// <remarks>
    /// Handed out so that whoever built the grid can say which element each button stands for. A
    /// pad has a name of its own and a line of its own in every preset, so it has to be
    /// selectable on its own while the panel is being laid out, and only the thing that read the
    /// description knows which button is which.
    /// </remarks>
    public IReadOnlyList<Control> Caps => _caps;

    /// <summary>
    /// The kit the grid is currently listening to, and the handler it gave it.
    /// </summary>
    /// <remarks>
    /// Both are kept because the subscription has to come off the kit it went on, and by the
    /// time the grid is handed a different kit the property already holds the new one.
    /// </remarks>
    private IMachinePads? _watching;

    /// <inheritdoc cref="_watching"/>
    private EventHandler? _listening;

    /// <summary>
    /// Builds an empty grid, and drops the kit's subscription when the grid leaves the tree.
    /// </summary>
    /// <remarks>
    /// A kit outlives the panel showing it: it belongs to the instrument, and a panel is opened
    /// and shut. Left subscribed, every panel ever opened would still be redrawing itself on
    /// every note.
    /// </remarks>
    public PadGrid()
    {
        Build();

        DetachedFromVisualTree += (_, _) => Unwatch();
    }

    /// <summary>The kit behind the grid: what is on each pad, which is lit, which is picked.</summary>
    public IMachinePads? Pads
    {
        get => GetValue(PadsProperty);
        set => SetValue(PadsProperty, value);
    }

    /// <inheritdoc cref="ColumnsProperty"/>
    public int Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <summary>How wide one pad's cap is.</summary>
    public double CapWidth
    {
        get => GetValue(CapWidthProperty);
        set => SetValue(CapWidthProperty, value);
    }

    /// <summary>And how tall.</summary>
    public double CapHeight
    {
        get => GetValue(CapHeightProperty);
        set => SetValue(CapHeightProperty, value);
    }

    /// <summary>The air between one pad and the next.</summary>
    public double Gap
    {
        get => GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    /// <summary>What an unlit pad is painted.</summary>
    public Color Colour
    {
        get => GetValue(ColourProperty);
        set => SetValue(ColourProperty, value);
    }

    /// <inheritdoc cref="CellsProperty"/>
    public IReadOnlyList<PadCell>? Cells
    {
        get => GetValue(CellsProperty);
        set => SetValue(CellsProperty, value);
    }

    /// <summary>How many buttons there are: what the machine declares, or what the host has.</summary>
    private int Count => Cells is { Count: > 0 } cells ? cells.Count : Pads?.Count ?? 0;

    /// <summary>
    /// Rebuilds when the shape of the grid changes, and moves the subscription when the kit does.
    /// </summary>
    /// <remarks>
    /// A new kit means both: the old subscription has to come off before the buttons are made
    /// again, or a kit that has been put down goes on redrawing a grid that is no longer about
    /// it.
    /// </remarks>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == PadsProperty)
        {
            Unwatch();
            Build();
            Watch();
        }
        else if (change.Property == CellsProperty ||
                 change.Property == ColumnsProperty ||
                 change.Property == CapWidthProperty ||
                 change.Property == CapHeightProperty ||
                 change.Property == GapProperty ||
                 change.Property == ColourProperty)
        {
            Build();
        }
    }

    /// <summary>Follows the kit, so a note that lands anywhere else still lights its pad here.</summary>
    private void Watch()
    {
        if (Pads is not { } kit) return;

        _watching = kit;
        _listening = (_, _) => Refresh();

        kit.Changed += _listening;
    }

    /// <summary>Stops following it, and forgets which one it was following.</summary>
    private void Unwatch()
    {
        if (_watching != null && _listening != null) _watching.Changed -= _listening;

        _watching = null;
        _listening = null;
    }

    /// <summary>
    /// Lays the pads out, once.
    /// </summary>
    /// <remarks>
    /// The grid is made rather than a wrapping row used, so that a kit whose last row is short
    /// still has its pads under the ones above them. A hand reaching for the bottom left pad is
    /// reaching for a place, not for the twelfth item in a list.
    ///
    /// The gap goes on every pad, the last column and the last row included. It is what a kit
    /// written by hand puts on each of its buttons, and trimming the two outer edges made the
    /// grid narrower and shorter than the same grid drawn in XAML, so the box round it came out
    /// the wrong size.
    ///
    /// Each cap is wired through its press rather than given a command, because a pad does two
    /// things at once: it sounds, and it becomes the pad the controls beside the grid are about.
    /// A command would be one of those and the panel would have to arrange the other.
    /// </remarks>
    private void Build()
    {
        _grid.Children.Clear();
        _grid.RowDefinitions.Clear();
        _grid.ColumnDefinitions.Clear();
        _caps.Clear();

        int count = Count;
        int across = Math.Max(1, Columns);
        int down = count == 0 ? 0 : (count + across - 1) / across;

        for (int column = 0; column < across; column++)
            _grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));

        for (int row = 0; row < down; row++)
            _grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        for (int at = 0; at < count; at++)
        {
            int held = at;

            var cap = new PushButton
            {
                CapWidth = CapWidth,
                CapHeight = CapHeight,
                FontSize = 11,
                Colour = Colour,
                HasLamp = true,
                LampBelow = false,
                Margin = new Thickness(0, 0, Gap, Gap),
            };

            cap.Pressed += (_, _) => Struck(held);

            cap.AddHandler(InputElement.PointerPressedEvent, (_, _) => Down(held), RoutingStrategies.Tunnel);
            cap.AddHandler(InputElement.PointerReleasedEvent, (_, _) => Up(held), RoutingStrategies.Tunnel);
            cap.AddHandler(InputElement.PointerCaptureLostEvent, (_, _) => Up(held), RoutingStrategies.Tunnel);

            Grid.SetColumn(cap, held % across);
            Grid.SetRow(cap, held / across);

            _grid.Children.Add(cap);
            _caps.Add(cap);
        }

        Child = _grid;

        Refresh();
    }

    /// <summary>
    /// A pad was hit: it sounds, and it becomes the one the controls beside the grid are about.
    /// </summary>
    /// <remarks>
    /// Picked before hit, so anything the sounding reaches is already looking at the right pad.
    /// </remarks>
    private void Struck(int at)
    {
        if (Pads is not { } kit) return;

        kit.Picked = at;
        kit.Hit(at);

        Refresh();
    }

    /// <summary>
    /// A hand has gone down on a pad, so its key lights on whatever keyboard is drawn.
    /// </summary>
    /// <remarks>
    /// The light and nothing else. What sounds a pad is still its own press, which fires on the
    /// way back up, so sliding off is still how a press is changed one's mind about: the key
    /// lights while the hand is down and goes out again with nothing having sounded.
    ///
    /// Heard on the way down rather than bubbling, because the cap marks a press handled: it
    /// captures nothing and answers on the release, which is the whole of how sliding off works.
    /// </remarks>
    private void Down(int at) => Pads?.Held(at);

    /// <summary>The hand has come up, so the key goes out again.</summary>
    /// <remarks>
    /// Losing the pointer counts as coming up. A press that ends because the window went away or
    /// something else grabbed the mouse never gets a release, and a key lit by a hand that is no
    /// longer there stays lit for the rest of the session.
    /// </remarks>
    private void Up(int at) => Pads?.Let(at);

    /// <summary>
    /// Tells every pad what it says and whether it is lit, without rebuilding one.
    /// </summary>
    /// <remarks>
    /// What is on a pad comes from the kit; what it is called and what note it answers to come
    /// from the machine, where they were declared.
    ///
    /// A pad the machine declares that the kit behind the panel is not big enough to have is
    /// drawn as an empty pad rather than left out, so the grid keeps the shape the machine says
    /// it is. A pad the kit has but has nothing on is dimmed too, less far, since an empty pad
    /// should read as empty rather than as a pad whose name happens to be blank.
    /// </remarks>
    private void Refresh()
    {
        if (Pads is not { } kit) return;

        for (int at = 0; at < _caps.Count; at++)
        {
            var cap = _caps[at];
            var cell = Cells is { } cells && at < cells.Count ? cells[at] : null;

            cap.CapText = at < kit.Count ? kit.Cap(at) : cell?.Name ?? "";
            cap.Label = cell?.Note ?? (at < kit.Count ? kit.Note(at) : "");

            if (at >= kit.Count)
            {
                cap.Lit = false;
                cap.IsSelected = false;
                cap.Opacity = 0.4;

                continue;
            }

            cap.Lit = kit.Lit(at);
            cap.IsSelected = kit.Picked == at;

            cap.Opacity = kit.Filled(at) ? 1 : 0.55;
        }
    }

}
