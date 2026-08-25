using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using JingleBox2.Machines;
using System;
using System.Collections.Generic;

namespace JingleBox2.Machines.Ui;

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
    /// <summary>The kit behind it.</summary>
    public static readonly StyledProperty<IMachinePads?> PadsProperty =
        AvaloniaProperty.Register<PadGrid, IMachinePads?>(nameof(Pads));

    /// <summary>How many pads stand side by side. Four, on every drum machine ever made.</summary>
    public static readonly StyledProperty<int> ColumnsProperty =
        AvaloniaProperty.Register<PadGrid, int>(nameof(Columns), 4);

    /// <summary>How wide and tall one pad's cap is.</summary>
    public static readonly StyledProperty<double> CapWidthProperty =
        AvaloniaProperty.Register<PadGrid, double>(nameof(CapWidth), 86);

    public static readonly StyledProperty<double> CapHeightProperty =
        AvaloniaProperty.Register<PadGrid, double>(nameof(CapHeight), 42);

    /// <summary>The air between them.</summary>
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

    /// <summary>What an unlit pad is painted, before the machine's own colour is put on it.</summary>
    public static readonly StyledProperty<Color> ColourProperty =
        AvaloniaProperty.Register<PadGrid, Color>(nameof(Colour), Color.FromRgb(0x4A, 0x4E, 0x57));

    private readonly Grid _grid = new();
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

    private IMachinePads? _watching;
    private EventHandler? _listening;

    public PadGrid()
    {
        Build();

        DetachedFromVisualTree += (_, _) => Unwatch();
    }

    public IMachinePads? Pads
    {
        get => GetValue(PadsProperty);
        set => SetValue(PadsProperty, value);
    }

    public int Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    public double CapWidth
    {
        get => GetValue(CapWidthProperty);
        set => SetValue(CapWidthProperty, value);
    }

    public double CapHeight
    {
        get => GetValue(CapHeightProperty);
        set => SetValue(CapHeightProperty, value);
    }

    public double Gap
    {
        get => GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    public Color Colour
    {
        get => GetValue(ColourProperty);
        set => SetValue(ColourProperty, value);
    }

    public IReadOnlyList<PadCell>? Cells
    {
        get => GetValue(CellsProperty);
        set => SetValue(CellsProperty, value);
    }

    /// <summary>How many buttons there are: what the machine declares, or what the host has.</summary>
    private int Count => Cells is { Count: > 0 } cells ? cells.Count : Pads?.Count ?? 0;

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

    private void Watch()
    {
        if (Pads is not { } kit) return;

        _watching = kit;
        _listening = (_, _) => Refresh();

        kit.Changed += _listening;
    }

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
                Margin = new Thickness(
                    0, 0,
                    held % across == across - 1 ? 0 : Gap,
                    held / across == down - 1 ? 0 : Gap),
            };

            // Pressed rather than commanded, because a pad does two things at once: it sounds,
            // and it becomes the pad the controls beside the grid are about. A command would be
            // one of those and the panel would have to arrange the other.
            cap.Pressed += (_, _) => Struck(held);

            Grid.SetColumn(cap, held % across);
            Grid.SetRow(cap, held / across);

            _grid.Children.Add(cap);
            _caps.Add(cap);
        }

        Child = _grid;

        Refresh();
    }

    private void Struck(int at)
    {
        if (Pads is not { } kit) return;

        kit.Picked = at;
        kit.Hit(at);

        Refresh();
    }

    /// <summary>Tells every pad what it says and whether it is lit, without rebuilding one.</summary>
    private void Refresh()
    {
        if (Pads is not { } kit) return;

        for (int at = 0; at < _caps.Count; at++)
        {
            var cap = _caps[at];
            var cell = Cells is { } cells && at < cells.Count ? cells[at] : null;

            // What is on it comes from the kit; what it is called and what it answers to come
            // from the machine, where they were declared.
            cap.CapText = at < kit.Count ? kit.Cap(at) : cell?.Name ?? "";
            cap.Label = cell?.Note ?? (at < kit.Count ? kit.Note(at) : "");

            if (at >= kit.Count)
            {
                // Declared, but the kit behind the panel is not that big. Drawn as an empty pad
                // rather than left out, so the grid is the shape the machine says it is.
                cap.Lit = false;
                cap.IsSelected = false;
                cap.Opacity = 0.4;

                continue;
            }

            cap.Lit = kit.Lit(at);
            cap.IsSelected = kit.Picked == at;

            // An empty pad is drawn as an empty pad rather than as a pad whose name is blank.
            cap.Opacity = kit.Filled(at) ? 1 : 0.55;
        }
    }

}

/// <summary>
/// One button of a pad grid, as the machine declared it.
/// </summary>
/// <remarks>
/// The name is what a preset writes its line against, and the note is what fires it in a
/// pattern. Neither is a setting: they are what the button is, and they change only when
/// somebody edits the machine.
/// </remarks>
public sealed record PadCell(string Name, string Note);
