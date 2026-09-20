using Avalonia;
using Avalonia.Media;
using System;
using System.Globalization;
using JingleBox2.Rack.Controls.Enums;
using JingleBox2.Rack.Controls.Records;
using JingleBox2.Rack.SoundDevices.Faces.Interfaces;

namespace JingleBox2.Rack.Controls;

/// <summary>
/// One of the two wheels beside a keyboard, drawn where it is being held.
/// </summary>
/// <remarks>
/// **It reports and never moves anything**, which is the whole of what it is for and is not a
/// limitation. A wheel on the screen is a picture of the wheel under somebody's hand, the way
/// the drawn keyboard is a picture of which keys are down: a mouse dragging it would be a second
/// wheel disagreeing with the first, and the moment the hardware moved again the picture would
/// jump. So it takes no pointer, and what it shows comes from the monitor every keyboard here
/// reads.
///
/// A pitch wheel rests in the middle and a modulation wheel rests at the bottom, which is
/// <see cref="Reads"/>, and it is the only difference between the two: both are a position on a
/// vertical face and neither is a control.
///
/// Drawn rather than templated for the reason every other control in here is: what it looks like
/// is a rounded face with a mark on it, and a value that moves tens of times a second has no
/// business rebuilding a visual tree.
/// </remarks>
public class Wheel : ThemedControl
{
    /// <summary>Backs <see cref="Value"/>: where the wheel is being held.</summary>
    public static readonly StyledProperty<double> ValueProperty =
        AvaloniaProperty.Register<Wheel, double>(nameof(Value));

    /// <summary>Backs <see cref="Reads"/>: which of the two wheels this is.</summary>
    public static readonly StyledProperty<WheelKind> ReadsProperty =
        AvaloniaProperty.Register<Wheel, WheelKind>(nameof(Reads));

    /// <summary>Backs <see cref="Watching"/>: where the wheels are being held.</summary>
    public static readonly StyledProperty<IPanelWheels?> WatchingProperty =
        AvaloniaProperty.Register<Wheel, IPanelWheels?>(nameof(Watching));

    /// <summary>Backs <see cref="Label"/>, written under the wheel.</summary>
    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<Wheel, string?>(nameof(Label));

    /// <summary>Backs <see cref="Face"/>: how tall the wheel's own face is.</summary>
    public static readonly StyledProperty<double> FaceProperty =
        AvaloniaProperty.Register<Wheel, double>(nameof(Face), 72.0);

    /// <summary>Backs <see cref="FontSize"/>, the size of the label under it.</summary>
    public static readonly StyledProperty<double> FontSizeProperty =
        AvaloniaProperty.Register<Wheel, double>(nameof(FontSize), 9.0);

    /// <summary>How wide the face is, which is not a setting: a wheel is a wheel.</summary>
    private const double Across = 20;

    /// <summary>The air between the face and the label under it.</summary>
    private const double LabelGap = 4;

    /// <summary>How far in from the top and bottom the mark may travel.</summary>
    /// <remarks>
    /// The mark is drawn with a thickness of its own, so without this a wheel at either end
    /// would have half of it outside the face it is supposed to be on.
    /// </remarks>
    private const double Inset = 5;

    /// <summary>And how far in from each side it is drawn.</summary>
    /// <remarks>
    /// The corners are rounded, so at the two ends of the travel the face is narrower than it is
    /// in the middle: a mark drawn the full width there has its ends hanging past the edge, and a
    /// modulation wheel rests at exactly that end. Held in by a little more than the corner takes
    /// away.
    /// </remarks>
    private const double Sides = 4;

    /// <summary>How many ridges are drawn across the face.</summary>
    /// <remarks>
    /// Enough to read as a wheel and not so many that they are a texture. They are what says
    /// this is a thing that rolls rather than a slider, which matters because it cannot be
    /// dragged and a control that looks draggable and is not reads as broken.
    /// </remarks>
    private const int Ridges = 7;

    /// <summary>Says which properties change the picture and which change the size.</summary>
    static Wheel()
    {
        AffectsRender<Wheel>(ValueProperty, ReadsProperty, LabelProperty, FaceProperty, FontSizeProperty);
        AffectsMeasure<Wheel>(LabelProperty, FaceProperty, FontSizeProperty);
    }

    /// <summary>
    /// Where the wheel is being held: minus one to one for a centred one, nought to one otherwise.
    /// </summary>
    public double Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    /// <summary>Which of the two wheels this is, which is also where it rests.</summary>
    public WheelKind Reads
    {
        get => GetValue(ReadsProperty);
        set => SetValue(ReadsProperty, value);
    }

    /// <summary>
    /// Where the wheels are being held, or nothing for a wheel that is told its own value.
    /// </summary>
    /// <remarks>
    /// Watched here rather than bound, because what it reports is an event and not a property
    /// that announces itself: a wheel is a monitor's reading, and a plain binding on to it would
    /// be read once and then never again, which is a wheel that works until the first time
    /// somebody moves it.
    ///
    /// Taken off when the control leaves the tree. A panel is rebuilt whenever anything it is
    /// drawn from moves and the monitor outlives every one of them, so a listener left on would
    /// have the application redrawing panels nobody is looking at once per wheel message per
    /// time each had ever been built.
    /// </remarks>
    public IPanelWheels? Watching
    {
        get => GetValue(WatchingProperty);
        set => SetValue(WatchingProperty, value);
    }

    /// <summary>Follows whatever it has been pointed at, and lets go of whatever it had.</summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != WatchingProperty && change.Property != ReadsProperty) return;

        if (change.Property == WatchingProperty)
        {
            if (change.OldValue is IPanelWheels had) had.Moved -= Held;

            if (Watching is { } wheels) wheels.Moved += Held;
        }

        Held(this, EventArgs.Empty);
    }

    /// <summary>Takes the reading, wherever the event arrived from.</summary>
    /// <remarks>
    /// **Nothing here reads a property of this control until it is on the drawing thread**, and
    /// that is the whole of what this method is careful about. A styled property is the drawing
    /// thread's in both directions: <c>GetValue</c> verifies the thread exactly as <c>SetValue</c>
    /// does, so reading <see cref="Watching"/> to decide whether there is anything to do is
    /// already the fault. Guarding the write and not the read is the easy half done twice.
    ///
    /// What that cost is worth naming, because it is not a picture going wrong. A wheel arrives
    /// on the port's thread, so the throw left through the port's own delivery, and the message
    /// reached nothing: not the picture, not the sound. The bend never happened.
    ///
    /// **Coalesced, because a hand on a wheel is a hundred messages a second** and each one would
    /// otherwise be a trip to the drawing thread to move one mark. A trip already pending is left
    /// to do the work, since what it reads is wherever the wheel has got to by the time it runs,
    /// which is newer than what it would have been told.
    /// </remarks>
    private void Held(object? sender, EventArgs e)
    {
        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Take();
            return;
        }

        if (System.Threading.Interlocked.Exchange(ref _posted, 1) == 1) return;

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            System.Threading.Interlocked.Exchange(ref _posted, 0);

            Take();
        });
    }

    /// <summary>Whether a trip to the drawing thread is already on its way. See <see cref="Held"/>.</summary>
    private int _posted;

    /// <summary>Reads where the wheel is and draws it, on the thread that owns this control.</summary>
    private void Take()
    {
        if (Watching is not { } wheels) return;

        Value = Reads == WheelKind.Pitch ? wheels.Lean : wheels.Amount;
    }

    /// <summary>Stops listening, so a panel thrown away does not go on being drawn.</summary>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        if (Watching is { } wheels) wheels.Moved -= Held;
    }

    /// <summary>Picks the listening up again, for a panel shown, hidden and shown once more.</summary>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        if (Watching is not { } wheels) return;

        wheels.Moved -= Held;
        wheels.Moved += Held;

        Held(this, EventArgs.Empty);
    }

    /// <summary>What is written under it.</summary>
    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    /// <summary>How tall the wheel's own face is, not counting the label under it.</summary>
    public double Face
    {
        get => GetValue(FaceProperty);
        set => SetValue(FaceProperty, value);
    }

    /// <summary>How big that label is.</summary>
    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>Room for the face, and for the label under it where there is one.</summary>
    protected override Size MeasureOverride(Size availableSize)
    {
        double width = Across;
        double height = Face;

        if (string.IsNullOrEmpty(Label)) return new Size(width, height);

        var text = Written(Label, Brushes.Black);

        return new Size(Math.Max(width, text.Width), height + LabelGap + text.Height);
    }

    /// <summary>The face, its ridges, the mark where the wheel is, and the label.</summary>
    public override void Render(DrawingContext context)
    {
        var palette = ThemePalette.From(this);
        double left = (Bounds.Width - Across) / 2;
        var face = new Rect(left, 0, Across, Face);

        context.DrawRectangle(
            palette.RowShade(0x22),
            new Pen(palette.BorderBrush, 1),
            new RoundedRect(face, Across / 3));

        var ridge = new Pen(palette.BorderBrush, 1);

        for (int at = 1; at <= Ridges; at++)
        {
            double y = Face * at / (Ridges + 1);

            context.DrawLine(ridge, new Point(left + 3, y), new Point(left + Across - 3, y));
        }

        if (Reads == WheelKind.Pitch)
        {
            context.DrawLine(
                new Pen(palette.MutedBrush, 1),
                new Point(left, Face / 2),
                new Point(left + Across, Face / 2));
        }

        context.DrawLine(
            new Pen(palette.AccentBrush, 3),
            new Point(left + Sides, Mark()),
            new Point(left + Across - Sides, Mark()));

        if (string.IsNullOrEmpty(Label)) return;

        var label = Written(Label, palette.MutedBrush);

        context.DrawText(label, new Point((Bounds.Width - label.Width) / 2, Face + LabelGap));
    }

    /// <summary>
    /// How far down the face the mark sits, with the top of the travel as one.
    /// </summary>
    /// <remarks>
    /// Up is more, which is what a wheel does under a thumb and is the opposite of what a
    /// coordinate does on a screen. A centred wheel reads its own minus one to one and one that
    /// is not reads nought to one, so both come to the same fraction of the same face.
    /// </remarks>
    private double Mark()
    {
        double up = Reads == WheelKind.Pitch
            ? (Math.Clamp(Value, -1, 1) + 1) / 2
            : Math.Clamp(Value, 0, 1);

        return Face - Inset - (up * (Face - (Inset * 2)));
    }

    /// <summary>The label, laid out in the panel's own face.</summary>
    private FormattedText Written(string? text, IBrush brush) =>
        new(text ?? "", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            Typeface.Default, FontSize, brush);
}
