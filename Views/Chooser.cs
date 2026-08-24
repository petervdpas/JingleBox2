using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using JingleBox2.Machines.Ui;

namespace JingleBox2.Views;

/// <summary>
/// One of a list, with the one before and the one after a press away.
/// </summary>
/// <remarks>
/// Opening a list, reading it and picking is three actions and a decision. Trying the next one
/// is what you actually do while hunting for a sound, and it should be one press. So the arrows
/// are the control and the list is behind the name, for when you already know which you want.
///
/// Drawn rather than a row of three controls, because it is one thing: the arrows, the name and
/// how far through you are all say the same fact and have to move together.
/// </remarks>
public class Chooser : ThemedControl
{
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<Chooser, IEnumerable?>(nameof(ItemsSource));

    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<Chooser, object?>(
            nameof(SelectedItem), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>What the field says when nothing is picked.</summary>
    public static readonly StyledProperty<string> PlaceholderProperty =
        AvaloniaProperty.Register<Chooser, string>(nameof(Placeholder), "");

    /// <summary>Off to stop at the ends instead of coming round again.</summary>
    public static readonly StyledProperty<bool> WrapsProperty =
        AvaloniaProperty.Register<Chooser, bool>(nameof(Wraps), true);

    /// <summary>How wide the name is. The arrows and the count are added to it.</summary>
    public static readonly StyledProperty<double> FieldWidthProperty =
        AvaloniaProperty.Register<Chooser, double>(nameof(FieldWidth), 150);

    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<Chooser, double>(nameof(FontSize), 12);

    /// <summary>Draws "3 / 20" after the arrow, so the list has a size without being opened.</summary>
    public static readonly StyledProperty<bool> ShowPositionProperty =
        AvaloniaProperty.Register<Chooser, bool>(nameof(ShowPosition), true);

    private const double ArrowWidth = 26;

    private const double BarHeight = 30;

    private const double Gap = 4;

    private const double CountWidth = 44;

    private const double Corner = 3;

    /// <summary>Which part the pointer is on.</summary>
    private enum Part
    {
        None,
        Back,
        Field,
        Forward
    }

    private Part _down = Part.None;
    private Part _over = Part.None;

    private IEnumerable? _watching;

    static Chooser()
    {
        AffectsRender<Chooser>(
            ItemsSourceProperty, SelectedItemProperty, PlaceholderProperty,
            FieldWidthProperty, FontSizeProperty, ShowPositionProperty);

        AffectsMeasure<Chooser>(FieldWidthProperty, ShowPositionProperty);
    }

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public string Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public bool Wraps
    {
        get => GetValue(WrapsProperty);
        set => SetValue(WrapsProperty, value);
    }

    public double FieldWidth
    {
        get => GetValue(FieldWidthProperty);
        set => SetValue(FieldWidthProperty, value);
    }

    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    public bool ShowPosition
    {
        get => GetValue(ShowPositionProperty);
        set => SetValue(ShowPositionProperty, value);
    }

    /// <summary>The items as a list, since a chooser has to count them and index them.</summary>
    private IReadOnlyList<object> Items =>
        ItemsSource?.Cast<object>().ToList() ?? (IReadOnlyList<object>)Array.Empty<object>();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != ItemsSourceProperty) return;

        // The list is the property, so a change to it is a change to the picture.
        if (_watching is INotifyCollectionChanged was) was.CollectionChanged -= OnItemsChanged;

        _watching = ItemsSource;

        if (_watching is INotifyCollectionChanged now) now.CollectionChanged += OnItemsChanged;

        InvalidateVisual();
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();

    protected override Size MeasureOverride(Size availableSize) =>
        new(ArrowWidth * 2 + FieldWidth + Gap * 3 + (ShowPosition ? CountWidth : 0), BarHeight);

    public override void Render(DrawingContext context)
    {
        if (Bounds.Width <= 1 || Bounds.Height <= 1) return;

        var palette = ThemePalette.From(this);
        var items = Items;

        Cap(context, palette, Seat(Part.Back), Part.Back, items.Count > 1);
        Field(context, palette, items);
        Cap(context, palette, Seat(Part.Forward), Part.Forward, items.Count > 1);

        if (!ShowPosition) return;

        var count = Text(Position(items), palette.Muted);

        context.DrawText(count, new Point(
            Seat(Part.Forward).Right + Gap + 4,
            (Bounds.Height - count.Height) / 2));
    }

    private string Position(IReadOnlyList<object> items)
    {
        if (items.Count == 0) return "";

        int at = At(items);

        return (at < 0 ? "-" : (at + 1).ToString(CultureInfo.InvariantCulture)) + " / " + items.Count;
    }

    private void Cap(DrawingContext context, ThemePalette palette, Rect seat, Part part, bool live)
    {
        bool down = _down == part;
        var colour = palette.Surface;

        if (_over == part && !down && live) colour = ThemePalette.Shade(colour, 0.10);

        var moulding = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            GradientStops = down
                ? new GradientStops
                {
                    new GradientStop(ThemePalette.Shade(colour, -0.10), 0),
                    new GradientStop(ThemePalette.Shade(colour, 0.20), 1)
                }
                : new GradientStops
                {
                    new GradientStop(ThemePalette.Shade(colour, 0.38), 0),
                    new GradientStop(ThemePalette.Shade(colour, 0.08), 1)
                }
        };

        context.DrawRectangle(
            moulding,
            new Pen(new SolidColorBrush(ThemePalette.Shade(colour, -0.35)), 1),
            seat, Corner, Corner);

        var ink = new SolidColorBrush(palette.Text, live ? 0.9 : 0.3);
        double size = 4.5;
        double middleY = seat.Center.Y + (down ? 0.5 : 0);
        double middleX = seat.Center.X;
        // The apex is what it points at, so back has its flat edge on the right.
        int way = part == Part.Back ? 1 : -1;

        var arrow = new StreamGeometry();

        using (var draw = arrow.Open())
        {
            draw.BeginFigure(new Point(middleX + size * way, middleY - size), true);
            draw.LineTo(new Point(middleX + size * way, middleY + size));
            draw.LineTo(new Point(middleX - size * way, middleY));
            draw.EndFigure(true);
        }

        context.DrawGeometry(ink, null, arrow);
    }

    private void Field(DrawingContext context, ThemePalette palette, IReadOnlyList<object> items)
    {
        var seat = Seat(Part.Field);

        context.DrawRectangle(
            new SolidColorBrush(palette.Background, 0.85),
            new Pen(new SolidColorBrush(_over == Part.Field ? palette.Accent : palette.Border, 0.9), 1),
            seat, Corner, Corner);

        string said = SelectedItem?.ToString() ?? "";
        bool empty = said.Length == 0;

        var text = Text(
            empty ? Placeholder : said,
            empty ? palette.Muted : palette.Text,
            seat.Width - 18);

        context.DrawText(text, new Point(seat.X + 7, seat.Center.Y - text.Height / 2));

        if (items.Count == 0) return;

        // A little mark to say the name can be pressed for the whole list.
        var mark = new StreamGeometry();

        using (var draw = mark.Open())
        {
            double x = seat.Right - 10;
            double y = seat.Center.Y - 1;

            draw.BeginFigure(new Point(x - 3.5, y - 1.5), true);
            draw.LineTo(new Point(x + 3.5, y - 1.5));
            draw.LineTo(new Point(x, y + 2.5));
            draw.EndFigure(true);
        }

        context.DrawGeometry(new SolidColorBrush(palette.Muted, 0.8), null, mark);
    }

    private FormattedText Text(string what, Color colour, double room = 0) =>
        new(what, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default,
            FontSize, new SolidColorBrush(colour))
        {
            MaxTextWidth = room > 0 ? room : double.PositiveInfinity,
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis
        };

    private Rect Seat(Part part) => part switch
    {
        Part.Back => new Rect(0, 0, ArrowWidth, BarHeight),
        Part.Field => new Rect(ArrowWidth + Gap, 0, FieldWidth, BarHeight),
        Part.Forward => new Rect(ArrowWidth + Gap + FieldWidth + Gap, 0, ArrowWidth, BarHeight),
        _ => default
    };

    private Part PartAt(Point point)
    {
        foreach (var part in new[] { Part.Back, Part.Field, Part.Forward })
            if (Seat(part).Contains(point)) return part;

        return Part.None;
    }

    /// <summary>Where the picked one is in the list, or -1 when it is not in it.</summary>
    private int At(IReadOnlyList<object> items)
    {
        if (SelectedItem == null) return -1;

        for (int i = 0; i < items.Count; i++)
            if (Equals(items[i], SelectedItem)) return i;

        return -1;
    }

    /// <summary>
    /// One along, either way. Nothing picked yet means forwards takes the first and backwards
    /// the last, which is what an empty field and an arrow ought to mean.
    /// </summary>
    private void Step(int by)
    {
        var items = Items;

        if (items.Count == 0) return;

        int at = At(items);

        int wanted = at < 0
            ? (by > 0 ? 0 : items.Count - 1)
            : Wraps
                ? ((at + by) % items.Count + items.Count) % items.Count
                : Math.Clamp(at + by, 0, items.Count - 1);

        SelectedItem = items[wanted];
    }

    /// <summary>The whole list, for when you already know which one you are after.</summary>
    private void OpenList()
    {
        var items = Items;

        if (items.Count == 0) return;

        var list = new ListBox
        {
            ItemsSource = items,
            SelectedItem = SelectedItem,
            MaxHeight = 320,
            MinWidth = FieldWidth + ArrowWidth
        };

        var flyout = new Flyout { Content = list, Placement = PlacementMode.Bottom };

        list.SelectionChanged += (_, _) =>
        {
            if (list.SelectedItem != null) SelectedItem = list.SelectedItem;

            flyout.Hide();
        };

        flyout.ShowAt(this);
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        var part = PartAt(e.GetPosition(this));

        if (part == Part.None) return;

        _down = part;

        e.Pointer.Capture(this);
        InvalidateVisual();

        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_down == Part.None) return;

        var part = _down;

        _down = Part.None;

        e.Pointer.Capture(null);
        InvalidateVisual();

        // Let go somewhere else and the press was thought better of.
        if (PartAt(e.GetPosition(this)) != part) return;

        if (part == Part.Back) Step(-1);
        else if (part == Part.Forward) Step(1);
        else OpenList();

        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var over = PartAt(e.GetPosition(this));

        if (over == _over) return;

        _over = over;

        InvalidateVisual();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);

        if (_over == Part.None) return;

        _over = Part.None;

        InvalidateVisual();
    }
}
