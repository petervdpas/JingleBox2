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

namespace JingleBox2.Machines.Ui;

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
    /// <summary>
    /// Backs <see cref="ItemsSource"/>, the list being walked.
    /// </summary>
    /// <remarks>
    /// The same pair of properties a combo box takes, so one can be swapped for the other
    /// without touching the binding underneath.
    /// </remarks>
    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<Chooser, IEnumerable?>(nameof(ItemsSource));

    /// <summary>
    /// Backs <see cref="SelectedItem"/>, which one of them is showing.
    /// </summary>
    /// <remarks>
    /// Two way, since walking the list is the whole purpose of the control and whatever it is
    /// bound to has to hear about it.
    /// </remarks>
    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<Chooser, object?>(
            nameof(SelectedItem), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>Backs <see cref="Placeholder"/>, what the field says when nothing is picked.</summary>
    public static readonly StyledProperty<string> PlaceholderProperty =
        AvaloniaProperty.Register<Chooser, string>(nameof(Placeholder), "");

    /// <summary>
    /// Backs <see cref="Wraps"/>: off to stop at the ends instead of coming round again.
    /// </summary>
    /// <remarks>
    /// On here, because the case this control exists for is hunting through a shelf of sounds by
    /// holding an arrow down, and stopping dead at the end of that is a control that has broken
    /// rather than a list that has finished. A picker where the ends matter turns it off.
    /// </remarks>
    public static readonly StyledProperty<bool> WrapsProperty =
        AvaloniaProperty.Register<Chooser, bool>(nameof(Wraps), true);

    /// <summary>
    /// Backs <see cref="FieldWidth"/>: how wide the name is, with the arrows and the count added
    /// to it.
    /// </summary>
    /// <remarks>
    /// The name is the only part of the control that can give, which is why the width is set on
    /// it rather than on the whole thing. See <see cref="Chrome"/> for working back the other
    /// way.
    /// </remarks>
    public static readonly StyledProperty<double> FieldWidthProperty =
        AvaloniaProperty.Register<Chooser, double>(nameof(FieldWidth), 150);

    /// <summary>Backs <see cref="FontSize"/>, which sizes the name and the count alike.</summary>
    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<Chooser, double>(nameof(FontSize), 12);

    /// <summary>
    /// Backs <see cref="ShowPosition"/>: draws "3 / 20" after the arrow, so the list has a size
    /// without being opened.
    /// </summary>
    /// <remarks>
    /// How far through a shelf you are is most of what tells you whether stepping is going to
    /// find the thing or whether you should open the list and read it.
    /// </remarks>
    public static readonly StyledProperty<bool> ShowPositionProperty =
        AvaloniaProperty.Register<Chooser, bool>(nameof(ShowPosition), true);

    /// <summary>How wide each arrow cap is, and how tall the whole bar stands.</summary>
    private const double ArrowWidth = 26;

    /// <inheritdoc cref="ArrowWidth"/>
    private const double BarHeight = 30;

    /// <summary>The air between the three parts, and again before the count.</summary>
    private const double Gap = 4;

    /// <summary>
    /// Room set aside for the count.
    /// </summary>
    /// <remarks>
    /// Fixed rather than measured, so the control does not change width as the list is walked:
    /// "3 / 20" and "17 / 200" are different widths and a bar that shuffled while an arrow was
    /// held down would be a bar with the arrow moving out from under the finger.
    /// </remarks>
    private const double CountWidth = 44;

    /// <summary>How much the corners of the caps and the field are rounded.</summary>
    private const double Corner = 3;

    /// <summary>Which part the pointer is on.</summary>
    private enum Part
    {
        /// <summary>None of them: the pointer is on the control but between its parts.</summary>
        None,

        /// <summary>The left arrow, one back.</summary>
        Back,

        /// <summary>The name in the middle, which opens the whole list.</summary>
        Field,

        /// <summary>The right arrow, one on.</summary>
        Forward
    }

    /// <summary>Which part the press landed on, and which the pointer is resting over.</summary>
    private Part _down = Part.None;

    /// <inheritdoc cref="_down"/>
    private Part _over = Part.None;

    /// <summary>
    /// The list this is currently subscribed to, kept so the subscription can be taken off again.
    /// </summary>
    /// <remarks>
    /// <see cref="ItemsSource"/> cannot be read for this at the moment it changes: by then it is
    /// already the new list and the old one would be left with a handler on it for ever.
    /// </remarks>
    private IEnumerable? _watching;

    /// <summary>Says which properties change the picture and which change the size.</summary>
    static Chooser()
    {
        AffectsRender<Chooser>(
            ItemsSourceProperty, SelectedItemProperty, PlaceholderProperty,
            FieldWidthProperty, FontSizeProperty, ShowPositionProperty);

        AffectsMeasure<Chooser>(FieldWidthProperty, ShowPositionProperty);
    }

    /// <summary>The list being walked.</summary>
    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    /// <summary>Which one of them is showing, and what an arrow writes.</summary>
    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    /// <inheritdoc cref="PlaceholderProperty"/>
    public string Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    /// <inheritdoc cref="WrapsProperty"/>
    public bool Wraps
    {
        get => GetValue(WrapsProperty);
        set => SetValue(WrapsProperty, value);
    }

    /// <inheritdoc cref="FieldWidthProperty"/>
    public double FieldWidth
    {
        get => GetValue(FieldWidthProperty);
        set => SetValue(FieldWidthProperty, value);
    }

    /// <summary>How large the name and the count are written.</summary>
    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <inheritdoc cref="ShowPositionProperty"/>
    public bool ShowPosition
    {
        get => GetValue(ShowPositionProperty);
        set => SetValue(ShowPositionProperty, value);
    }

    /// <summary>
    /// How much wider the whole control is than the name field.
    /// </summary>
    /// <remarks>
    /// The arrows, the gaps between them and the count, none of which change with the list.
    /// Asked from outside by anyone who has been told how wide the control is to be and has to
    /// work back to how wide the name can be, which is the only part of it that can give.
    /// </remarks>
    public double Chrome => ArrowWidth * 2 + Gap * 3 + (ShowPosition ? CountWidth : 0);

    /// <summary>The items as a list, since a chooser has to count them and index them.</summary>
    private IReadOnlyList<object> Items =>
        ItemsSource?.Cast<object>().ToList() ?? (IReadOnlyList<object>)Array.Empty<object>();

    /// <summary>
    /// Follows the list itself, not only the property that holds it.
    /// </summary>
    /// <remarks>
    /// The count is drawn on the control, so a list that grows or shrinks changes the picture
    /// without the property ever being set again. A shelf of presets does exactly that: saving
    /// one adds a row to a collection the chooser is already holding.
    /// </remarks>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != ItemsSourceProperty) return;

        if (_watching is INotifyCollectionChanged was) was.CollectionChanged -= OnItemsChanged;

        _watching = ItemsSource;

        if (_watching is INotifyCollectionChanged now) now.CollectionChanged += OnItemsChanged;

        InvalidateVisual();
    }

    /// <summary>The list moved, so the count and possibly the name are out of date.</summary>
    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => InvalidateVisual();

    /// <summary>
    /// One bar, as wide as the name plus everything fixed around it.
    /// </summary>
    /// <remarks>
    /// What it was offered is ignored: a chooser asks for exactly what it was told to be, so a
    /// row of them in a panel comes out the same width whatever is in the lists.
    /// </remarks>
    protected override Size MeasureOverride(Size availableSize) => new(FieldWidth + Chrome, BarHeight);

    /// <summary>Paints the two arrow caps, the name field between them, and the count after.</summary>
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

    /// <summary>
    /// How far through: "3 / 20", or a dash for the number when what is picked is not in the
    /// list at all.
    /// </summary>
    /// <remarks>
    /// Invariant culture, since these are ordinals rather than measurements and the separator is
    /// written out here anyway.
    /// </remarks>
    private string Position(IReadOnlyList<object> items)
    {
        if (items.Count == 0) return "";

        int at = At(items);

        return (at < 0 ? "-" : (at + 1).ToString(CultureInfo.InvariantCulture)) + " / " + items.Count;
    }

    /// <summary>
    /// One arrow cap, moulded and shaded the same way a push button is.
    /// </summary>
    /// <remarks>
    /// Lit from above when it is up and from below when it is down, so a pressed cap sits in its
    /// own shadow. The arrow's apex is what it points at, so the one going back has its flat
    /// edge on the right.
    ///
    /// A list with nothing to step through draws its arrows faint rather than hiding them: a
    /// control that changed shape when its list emptied would be a control that moved everything
    /// beside it.
    /// </remarks>
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

    /// <summary>
    /// The name in its recess, with a little mark to say it can be pressed for the whole list.
    /// </summary>
    /// <remarks>
    /// A name too long for the field is trimmed with an ellipsis rather than folded, since the
    /// bar is one line tall and a second line would be drawn outside the control.
    /// </remarks>
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

    /// <summary>
    /// A piece of text laid out for the bar: one line, trimmed with an ellipsis if it is too
    /// long for the room given.
    /// </summary>
    private FormattedText Text(string what, Color colour, double room = 0) =>
        new(what, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface.Default,
            FontSize, new SolidColorBrush(colour))
        {
            MaxTextWidth = room > 0 ? room : double.PositiveInfinity,
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis
        };

    /// <summary>
    /// Where each part sits, which is the one place the layout is written down.
    /// </summary>
    /// <remarks>
    /// Both the drawing and the hit testing go through here, so a cap cannot end up drawn
    /// somewhere other than where a press on it is looked for.
    /// </remarks>
    private Rect Seat(Part part) => part switch
    {
        Part.Back => new Rect(0, 0, ArrowWidth, BarHeight),
        Part.Field => new Rect(ArrowWidth + Gap, 0, FieldWidth, BarHeight),
        Part.Forward => new Rect(ArrowWidth + Gap + FieldWidth + Gap, 0, ArrowWidth, BarHeight),
        _ => default
    };

    /// <summary>Which part a point is on, or none when it is on the gaps between them.</summary>
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

    /// <summary>
    /// The whole list, for when you already know which one you are after.
    /// </summary>
    /// <remarks>
    /// Built fresh each time rather than kept, since it exists for as long as it takes to pick
    /// something and a kept one would go stale against a list that has moved. It is at least as
    /// wide as the field plus an arrow, so a name that was trimmed in the bar can be read whole
    /// here, and capped in height so a shelf of two hundred sounds does not become a flyout
    /// taller than the window.
    /// </remarks>
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

    /// <summary>
    /// Puts a part down.
    /// </summary>
    /// <remarks>
    /// Nothing happens yet: the press is worked on the release, so that sliding off is how you
    /// change your mind. The pointer is captured so the release is heard even when it lands
    /// outside the control.
    /// </remarks>
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

    /// <summary>
    /// Works whatever was pressed, so long as the release landed on the same part.
    /// </summary>
    /// <remarks>
    /// Let go somewhere else and the press was thought better of.
    /// </remarks>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_down == Part.None) return;

        var part = _down;

        _down = Part.None;

        e.Pointer.Capture(null);
        InvalidateVisual();

        if (PartAt(e.GetPosition(this)) != part) return;

        if (part == Part.Back) Step(-1);
        else if (part == Part.Forward) Step(1);
        else OpenList();

        e.Handled = true;
    }

    /// <summary>
    /// Lights whichever part the pointer has moved onto.
    /// </summary>
    /// <remarks>
    /// Redrawn only when the answer actually changes, since this fires for every pixel of a
    /// pointer crossing the bar and three quarters of those land on the part it was already on.
    /// </remarks>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        var over = PartAt(e.GetPosition(this));

        if (over == _over) return;

        _over = over;

        InvalidateVisual();
    }

    /// <summary>Puts the lit part back when the pointer leaves the control entirely.</summary>
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);

        if (_over == Part.None) return;

        _over = Part.None;

        InvalidateVisual();
    }
}
