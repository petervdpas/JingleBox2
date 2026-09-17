using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using JingleBox2.Tracker;
using JingleBox2.Rack.Controls;
using JingleBox2.Rack.Controls.Records;
using JingleBox2.Tracker.Records;
using JingleBox2.ViewModels.Interfaces;

namespace JingleBox2.Views;

/// <summary>
/// The track names above a pattern, with the selected one picked out. Sits outside the
/// pattern's scroll area so it stays put vertically, and takes the horizontal scroll offset
/// so it stays aligned with the columns it names.
/// </summary>
/// <remarks>
/// The square above the line numbers names no track, which makes it the one place in this row
/// where something that is not a thing you touch can sit. The pattern's help badge is laid over
/// it for exactly that reason.
///
/// **The two switches on a tab are the mixer's own, reached rather than copied.** A hand
/// writing a part wants to hear one track without leaving the pattern, and the desk is a page
/// away; what is pressed here is the very same <see cref="IStripSwitches"/> the strip on the
/// mixer holds, so the two can never disagree and nothing had to learn a second way of saying
/// mute.
/// </remarks>
public sealed class PatternHeader : ThemedControl
{
    /// <summary>How many tracks are named, which is how many the pattern has.</summary>
    public static readonly StyledProperty<int> TrackCountProperty =
        AvaloniaProperty.Register<PatternHeader, int>(nameof(TrackCount), Song.DefaultTrackCount);

    /// <summary>Which one the cursor is in, drawn picked out from the rest.</summary>
    public static readonly StyledProperty<int> SelectedTrackProperty =
        AvaloniaProperty.Register<PatternHeader, int>(nameof(SelectedTrack));

    /// <inheritdoc cref="CharWidth"/>
    public static readonly StyledProperty<double> CharWidthProperty =
        AvaloniaProperty.Register<PatternHeader, double>(nameof(CharWidth), 8);

    /// <inheritdoc cref="ScrollOffset"/>
    public static readonly StyledProperty<double> ScrollOffsetProperty =
        AvaloniaProperty.Register<PatternHeader, double>(nameof(ScrollOffset));

    /// <inheritdoc cref="Columns"/>
    public static readonly StyledProperty<NoteColumns> ColumnsProperty =
        AvaloniaProperty.Register<PatternHeader, NoteColumns>(nameof(Columns));

    /// <summary>The pattern's own row height, which the header's height and lettering follow.</summary>
    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<PatternHeader, double>(nameof(RowHeight), 18);

    /// <inheritdoc cref="DropTargetTrack"/>
    public static readonly StyledProperty<int> DropTargetTrackProperty =
        AvaloniaProperty.Register<PatternHeader, int>(nameof(DropTargetTrack), -1);

    /// <inheritdoc cref="Switches"/>
    public static readonly StyledProperty<IReadOnlyList<IStripSwitches>?> SwitchesProperty =
        AvaloniaProperty.Register<PatternHeader, IReadOnlyList<IStripSwitches>?>(nameof(Switches));

    /// <summary>Only the row height changes the room asked for; the rest only changes the paint.</summary>
    static PatternHeader()
    {
        AffectsRender<PatternHeader>(TrackCountProperty, SelectedTrackProperty,
            CharWidthProperty, ScrollOffsetProperty, RowHeightProperty, DropTargetTrackProperty,
            ColumnsProperty, SwitchesProperty);
        AffectsMeasure<PatternHeader>(RowHeightProperty);
    }

    /// <inheritdoc cref="TrackCountProperty"/>
    public int TrackCount
    {
        get => GetValue(TrackCountProperty);
        set => SetValue(TrackCountProperty, value);
    }

    /// <inheritdoc cref="SelectedTrackProperty"/>
    public int SelectedTrack
    {
        get => GetValue(SelectedTrackProperty);
        set => SetValue(SelectedTrackProperty, value);
    }

    /// <summary>Taken from the grid, so both lay out on identical measurements.</summary>
    public double CharWidth
    {
        get => GetValue(CharWidthProperty);
        set => SetValue(CharWidthProperty, value);
    }

    /// <summary>How far the pattern below has been scrolled sideways.</summary>
    public double ScrollOffset
    {
        get => GetValue(ScrollOffsetProperty);
        set => SetValue(ScrollOffsetProperty, value);
    }

    /// <summary>
    /// How many note columns each track shows, which is what makes them different widths.
    /// </summary>
    /// <remarks>
    /// Told rather than worked out, and it has to be told: the header draws a box per track
    /// across the same row the grid draws its cells in, and a header measuring tracks one way
    /// while the grid draws them another puts every label over the wrong track.
    /// </remarks>
    public NoteColumns Columns
    {
        get => GetValue(ColumnsProperty);
        set => SetValue(ColumnsProperty, value);
    }

    /// <inheritdoc cref="RowHeightProperty"/>
    public double RowHeight
    {
        get => GetValue(RowHeightProperty);
        set => SetValue(RowHeightProperty, value);
    }

    /// <summary>The track a drag is currently hovering, or -1. Drawn as a drop outline.</summary>
    public int DropTargetTrack
    {
        get => GetValue(DropTargetTrackProperty);
        set => SetValue(DropTargetTrackProperty, value);
    }

    /// <summary>
    /// One strip per track, in track order, for the two switches drawn on each tab.
    /// </summary>
    /// <remarks>
    /// The strips themselves rather than a copy of what they read, so a press here is the press
    /// the mixer would have made: the strip is what raises the undo step, marks the song as
    /// changed and pushes the mix at what is already sounding, and a second spelling of any of
    /// that is how two pictures of one fader come to disagree.
    ///
    /// A track with no strip is an ordinary state rather than a fault, since this is told the
    /// track count and the list separately: its tab is drawn with no switches on it.
    /// </remarks>
    public IReadOnlyList<IStripSwitches>? Switches
    {
        get => GetValue(SwitchesProperty);
        set => SetValue(SwitchesProperty, value);
    }

    /// <summary>Raised when a header is clicked, so the cursor can jump to that track.</summary>
    public event EventHandler<int>? TrackClicked;

    /// <summary>The list currently being listened to, so it can be let go of again.</summary>
    private IReadOnlyList<IStripSwitches>? _listening;

    /// <summary>The strips currently being listened to, one by one.</summary>
    private readonly List<INotifyPropertyChanged> _watched = new();

    /// <inheritdoc/>
    /// <remarks>
    /// A drawn control is only redrawn when something tells it to, and a switch turned over
    /// anywhere else is exactly that: the mixer's own button, a knob pointed at it, or a lane
    /// playing back. So each strip is listened to for as long as this is on the screen.
    /// </remarks>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        Watch(Switches);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Let go of on the way out, since the strips outlive this control and one holding a
    /// handler would hold the page it was drawn on with it.
    /// </remarks>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        Watch(null);
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SwitchesProperty) Watch(Switches);
    }

    /// <summary>
    /// Listens to a list of strips and to each strip in it, letting go of whatever was before.
    /// </summary>
    /// <remarks>
    /// Both halves are needed and for different reasons. A strip says when its own switch moves;
    /// the list says when the song is opened or the track count changes, which empties and fills
    /// the very same collection, so nothing about the property this was handed ever changes.
    /// </remarks>
    /// <param name="strips">What to listen to, or nothing to let go of everything.</param>
    private void Watch(IReadOnlyList<IStripSwitches>? strips)
    {
        if (_listening is INotifyCollectionChanged was) was.CollectionChanged -= Refilled;

        foreach (var strip in _watched) strip.PropertyChanged -= Moved;

        _watched.Clear();
        _listening = strips;

        if (strips == null) return;

        if (strips is INotifyCollectionChanged now) now.CollectionChanged += Refilled;

        foreach (var strip in strips)
        {
            if (strip is not INotifyPropertyChanged said) continue;

            said.PropertyChanged += Moved;
            _watched.Add(said);
        }
    }

    /// <summary>The list was emptied or filled, so the strips are listened to again.</summary>
    private void Refilled(object? sender, NotifyCollectionChangedEventArgs e)
    {
        Watch(Switches);
        InvalidateVisual();
    }

    /// <summary>One strip moved: redrawn only for the two things drawn here.</summary>
    private void Moved(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(IStripSwitches.Mute) or nameof(IStripSwitches.Solo))) return;

        InvalidateVisual();
    }

    /// <summary>The track under a point, for drag and drop. Takes the scroll offset into account.</summary>
    public int TrackAtPoint(Point point)
    {
        double x = point.X + ScrollOffset;
        return x < Metrics.GutterWidth ? -1 : Metrics.TrackAt(x);
    }

    /// <summary>Above and below the names, so the tabs stand off the pattern under them.</summary>
    private const double VerticalPadding = 6;

    /// <summary>How big each of the two switches on a tab is drawn, square.</summary>
    /// <remarks>
    /// Small enough to sit beside the track's name on one line and big enough to be hit without
    /// aiming, which is what decides the row's height: a tab is as tall as its tallest part,
    /// and here that is no longer the lettering.
    /// </remarks>
    private const double MarkSize = 18;

    /// <summary>Between the two switches.</summary>
    private const double MarkGap = 3;

    /// <summary>How far in from the tab's left edge the track's name starts.</summary>
    /// <remarks>
    /// The name is set against that edge rather than centred in whatever room the switches
    /// leave, so every tab's name starts on the same line down the row: centred, a track two
    /// note columns wide put its name half an inch further along than the one beside it, and a
    /// row of tabs whose lettering wanders is one nobody can read across.
    /// </remarks>
    private const double NameInset = 8;

    /// <summary>
    /// Between the solo and the tab's own right edge, which is wider than the gap between the
    /// two switches.
    /// </summary>
    /// <remarks>
    /// A switch hard against a rounded corner reads as tighter than the same distance between
    /// two square edges does, so the two are set by eye rather than by one number said twice.
    /// </remarks>
    private const double MarkEdge = 7;

    /// <summary>
    /// The same layout the grid uses, built from the same character width so the two cannot
    /// drift apart.
    /// </summary>
    /// <remarks>
    /// Without the pattern's padding, since the header has no lines above or below it: it is one
    /// row standing outside the scroll area.
    /// </remarks>
    private PatternMetrics Metrics => new(CharWidth, RowHeight, TrackCount, 0, 0, Columns);

    /// <summary>
    /// One row tall, and no width of its own: the header is stretched to whatever the pattern
    /// beneath it is being seen through, and the tabs are placed inside that by the transform.
    /// </summary>
    protected override Size MeasureOverride(Size availableSize) =>
        new(0, Math.Max(RowHeight, MarkSize + MarkGap * 2) + VerticalPadding * 2);

    /// <summary>
    /// Where one tab's two switches sit, mute then solo, along its right edge.
    /// </summary>
    /// <remarks>
    /// One spelling for the drawing and for the press, since two of them would eventually put a
    /// switch somewhere other than where it is drawn, and the way that fails is a button that
    /// does nothing until the pointer is an inch off it.
    ///
    /// Mute to the left of solo, the order the mixer's own strips read in.
    /// </remarks>
    /// <param name="area">The tab itself.</param>
    private static (Rect Mute, Rect Solo) Marks(Rect area)
    {
        double y = area.Y + (area.Height - MarkSize) / 2;
        double solo = area.Right - MarkEdge - MarkSize;

        return (new Rect(solo - MarkGap - MarkSize, y, MarkSize, MarkSize),
                new Rect(solo, y, MarkSize, MarkSize));
    }

    /// <summary>
    /// One tab's box, in the pattern's own columns and before the scroll offset is taken off.
    /// </summary>
    /// <param name="metrics">The layout the grid settled on.</param>
    /// <param name="track">Which track.</param>
    /// <param name="height">How tall this row is.</param>
    private static Rect Tab(PatternMetrics metrics, int track, double height) =>
        new(metrics.TrackDividerX(track) + 1, 2, metrics.TrackWidth(track) - 2, height - 4);

    /// <summary>The strip one track's switches write into, or nothing where there is none.</summary>
    /// <param name="track">Which track.</param>
    private IStripSwitches? StripFor(int track)
    {
        var strips = Switches;

        return strips != null && track >= 0 && track < strips.Count ? strips[track] : null;
    }

    /// <summary>
    /// A tab per track, in the pattern's own columns.
    /// </summary>
    /// <remarks>
    /// Everything is shifted by the pattern's sideways scroll, so a name stays over the column it
    /// names rather than over whichever column happens to be at that place on screen.
    /// </remarks>
    public override void Render(DrawingContext context)
    {
        if (TrackCount <= 0 || CharWidth <= 0) return;

        var metrics = Metrics;
        double height = Bounds.Height;

        var palette = ThemePalette.From(this);
        var text = palette.TextBrush;
        var muted = palette.MutedBrush;
        var selectedPen = new Pen(palette.AccentBrush, 1);
        var idlePen = new Pen(palette.BorderBrush, 1);
        var dropPen = new Pen(palette.AccentBrush, 2);
        var selectedFill = palette.AccentTint(56);
        var idleFill = palette.RowShade(0x12);

        using var _ = context.PushTransform(Matrix.CreateTranslation(-ScrollOffset, 0));

        double fontSize = Math.Max(9, RowHeight - 6);
        var typeface = new Typeface(PatternFont.Family);

        for (int track = 0; track < TrackCount; track++)
        {
            var area = Tab(metrics, track, height);
            bool selected = track == SelectedTrack;

            bool dropTarget = track == DropTargetTrack;

            context.FillRectangle(dropTarget ? palette.AccentTint(90) : selected ? selectedFill : idleFill, area, 3);
            context.DrawRectangle(dropTarget ? dropPen : selected ? selectedPen : idlePen, area, 3);

            var strip = StripFor(track);
            var (mute, solo) = Marks(area);

            double named = strip == null ? area.Width : mute.X - area.X;

            string label = "Track " + (track + 1).ToString("00", CultureInfo.InvariantCulture);
            var formatted = new FormattedText(label, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, typeface, fontSize, selected || dropTarget ? text : muted)
            {
                MaxTextWidth = Math.Max(1, named - NameInset - MarkGap),
                Trimming = TextTrimming.CharacterEllipsis
            };

            context.DrawText(formatted, new Point(
                area.X + NameInset,
                area.Y + (area.Height - formatted.Height) / 2));

            if (strip == null) continue;

            Mark(context, palette, typeface, mute, "M", strip.Mute, strip.CanMute);
            Mark(context, palette, typeface, solo, "S", strip.Solo, strip.CanSolo);
        }
    }

    /// <summary>
    /// One switch on a tab: lit, unlit, or dark where it cannot be pressed at all.
    /// </summary>
    /// <remarks>
    /// Lit in the accent, which is what the mixer's own M and S are washed with when they are
    /// checked, since two drawings of one switch that agreed about the letter and not about the
    /// colour would read as two different switches.
    ///
    /// A switch that cannot be pressed is drawn dark rather than left out, the rule the mixer
    /// keeps for the master's solo: a control that vanishes takes the row's shape with it.
    /// </remarks>
    /// <param name="context">What is being drawn into.</param>
    /// <param name="palette">The theme's colours.</param>
    /// <param name="typeface">The pattern's own face, so the letter sits on the name's line.</param>
    /// <param name="box">Where it goes.</param>
    /// <param name="letter">M or S.</param>
    /// <param name="lit">Whether it is on.</param>
    /// <param name="can">Whether it can be pressed at all.</param>
    private static void Mark(DrawingContext context, ThemePalette palette, Typeface typeface,
                             Rect box, string letter, bool lit, bool can)
    {
        bool on = lit && can;

        context.FillRectangle(on ? palette.AccentBrush : palette.RowShade(0x16), box, 2);
        context.DrawRectangle(new Pen(on ? palette.AccentBrush : palette.BorderBrush, 1), box, 2);

        var formatted = new FormattedText(letter, CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, typeface, MarkSize - 5,
            on ? palette.SurfaceBrush : can ? palette.MutedBrush : palette.BorderBrush);

        context.DrawText(formatted, new Point(
            box.X + (box.Width - formatted.Width) / 2,
            box.Y + (box.Height - formatted.Height) / 2));
    }

    /// <summary>
    /// A click on a tab puts the cursor in that track, and turns a switch over as well where it
    /// landed on one.
    /// </summary>
    /// <remarks>
    /// A click over the line number gutter does nothing, since that square names no track.
    ///
    /// **Anywhere on a tab picks its track, the two switches included.** A tab is one thing to
    /// a hand, and a press that silenced a track while leaving the cursor where it was would be
    /// the same gesture answering two ways depending on which few pixels it landed on.
    /// </remarks>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (TrackCount <= 0) return;

        var point = e.GetPosition(this);
        double x = point.X + ScrollOffset;

        var metrics = Metrics;
        if (x < metrics.GutterWidth) return;

        int track = metrics.TrackAt(x);

        if (Pressed(metrics, track, new Point(x, point.Y))) InvalidateVisual();

        TrackClicked?.Invoke(this, track);
        e.Handled = true;
    }

    /// <summary>
    /// Turns over whichever switch is under a point, and says whether one was.
    /// </summary>
    /// <remarks>
    /// The strip is written into rather than a copy of it, so the undo step, the song being
    /// marked as changed and the mix reaching what is already sounding all happen exactly as
    /// they do when the same switch is pressed on the desk.
    /// </remarks>
    /// <param name="metrics">The layout the grid settled on.</param>
    /// <param name="track">The track the point is over.</param>
    /// <param name="point">Where the press landed, in the pattern's own columns.</param>
    private bool Pressed(PatternMetrics metrics, int track, Point point)
    {
        var strip = StripFor(track);
        if (strip == null) return false;

        var (mute, solo) = Marks(Tab(metrics, track, Bounds.Height));

        if (mute.Contains(point))
        {
            if (strip.CanMute) strip.Mute = !strip.Mute;
            return true;
        }

        if (solo.Contains(point))
        {
            if (strip.CanSolo) strip.Solo = !strip.Solo;
            return true;
        }

        return false;
    }

}
