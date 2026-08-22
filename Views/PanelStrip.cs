using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using System;
using System.Linq;

namespace JingleBox2.Views;

/// <summary>
/// A run of panel cells, across or down, that the things standing on it are placed against.
/// </summary>
/// <remarks>
/// What makes a machine's face read as one thing rather than a heap of parts is that everything
/// on it stands on the same grid. A knob is three cells wide, a toggle is two, a lamp is one,
/// and because every strip counts in the same cells the rows line up down the panel even though
/// nothing in them is the same size.
///
/// How fine the grid is, is <see cref="CellSize"/>. Small cells and wide spans is a fine grid
/// that can put a lamp between two knobs; large cells and single spans is a rough one that is
/// quicker to lay out and harder to misalign. A machine can use either, or one of each on
/// different parts of its panel, so long as the strips that ought to line up agree.
///
/// Strips are meant to be stacked: several horizontal ones under each other make a panel, and a
/// vertical one makes a column of a mixer.
/// </remarks>
public class PanelStrip : Panel
{
    /// <summary>How many cells a child stands on. One unless it says otherwise.</summary>
    public static readonly AttachedProperty<int> SpanProperty =
        AvaloniaProperty.RegisterAttached<PanelStrip, Control, int>("Span", 1);

    public static int GetSpan(Control control) => control.GetValue(SpanProperty);

    public static void SetSpan(Control control, int value) => control.SetValue(SpanProperty, value);

    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<PanelStrip, Orientation>(nameof(Orientation), Orientation.Horizontal);

    /// <summary>How big one cell is. The whole of how fine or rough the grid is.</summary>
    public static readonly StyledProperty<double> CellSizeProperty =
        AvaloniaProperty.Register<PanelStrip, double>(nameof(CellSize), 24.0);

    /// <summary>A gap left between one child and the next, on top of their cells.</summary>
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<PanelStrip, double>(nameof(Gap));

    /// <summary>
    /// The columns of the region this strip belongs to, as cell counts: "3 2 3 3 3 3 2 3".
    /// </summary>
    /// <remarks>
    /// Given the same columns, two strips put their children in the same places whatever those
    /// children are, which is what makes rows line up down a panel. Without it each strip
    /// counts its own cells and a row with six things in it agrees with a row of eight only by
    /// accident.
    ///
    /// Children take the columns in the order they are written, so a shorter row simply leaves
    /// the later columns empty rather than sliding everything along.
    /// </remarks>
    public static readonly StyledProperty<string> ColumnsProperty =
        AvaloniaProperty.Register<PanelStrip, string>(nameof(Columns), "");

    static PanelStrip()
    {
        AffectsMeasure<PanelStrip>(OrientationProperty, CellSizeProperty, GapProperty, ColumnsProperty);
        AffectsParentMeasure<PanelStrip>(SpanProperty);
    }

    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public double CellSize
    {
        get => GetValue(CellSizeProperty);
        set => SetValue(CellSizeProperty, value);
    }

    public double Gap
    {
        get => GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    public string Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <summary>The declared columns, or nothing when the strip counts its own cells.</summary>
    private int[] Declared =>
        string.IsNullOrWhiteSpace(Columns)
            ? Array.Empty<int>()
            : Columns.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
                     .Select(part => int.TryParse(part, out int cells) ? Math.Max(1, cells) : 1)
                     .ToArray();

    /// <summary>How many cells the child at this position stands on.</summary>
    private int CellsFor(Control child, int position)
    {
        var declared = Declared;

        return declared.Length > 0
            ? (position < declared.Length ? declared[position] : declared[^1])
            : Math.Max(1, GetSpan(child));
    }

    private bool Across => Orientation == Orientation.Horizontal;

    protected override Size MeasureOverride(Size availableSize)
    {
        double cell = Math.Max(1, CellSize);
        double along = 0;
        double across = 0;
        int shown = 0;

        int position = 0;

        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;

            double room = cell * CellsFor(child, position++);

            child.Measure(Across
                ? new Size(room, availableSize.Height)
                : new Size(availableSize.Width, room));

            along += room;
            across = Math.Max(across, Across ? child.DesiredSize.Height : child.DesiredSize.Width);
            shown++;
        }

        if (shown > 1) along += Gap * (shown - 1);

        return Across ? new Size(along, across) : new Size(across, along);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double cell = Math.Max(1, CellSize);
        double at = 0;
        bool first = true;
        int position = 0;

        foreach (var child in Children)
        {
            if (!child.IsVisible) continue;

            if (!first) at += Gap;
            first = false;

            double room = cell * CellsFor(child, position++);

            // Centred on its cells, so a narrow thing on a wide span sits under the middle of
            // it rather than shoved against one edge.
            if (Across)
            {
                double width = Math.Min(child.DesiredSize.Width, room);
                child.Arrange(new Rect(at + (room - width) / 2, 0, width, finalSize.Height));
            }
            else
            {
                double height = Math.Min(child.DesiredSize.Height, room);
                child.Arrange(new Rect(0, at + (room - height) / 2, finalSize.Width, height));
            }

            at += room;
        }

        return finalSize;
    }
}
