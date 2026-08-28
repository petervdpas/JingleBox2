using Avalonia;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using System;
using JingleBox2.Machines.Ui;

namespace JingleBox2.Views;

/// <summary>
/// The bracket between two fields that says they move together.
/// </summary>
/// <remarks>
/// The one from a drawing program's width and height, and it means the same thing here: while
/// it is closed, typing in either field puts the same number in the other. Closed it is a whole
/// chain link in the accent colour; open it is the same link pulled apart, in the colour of a
/// line rather than of a control.
///
/// Drawn rather than a check box with a word beside it, because what it does is entirely about
/// which two fields it stands next to, and a box saying "link" would have to be read and then
/// have its subject worked out. A bracket reaching from one field to the other has no subject
/// to work out.
/// </remarks>
public class Linker : ThemedControl
{
    /// <summary>Whether the two fields move together. Two way: the bracket is what sets it.</summary>
    public static readonly StyledProperty<bool> IsLinkedProperty =
        AvaloniaProperty.Register<Linker, bool>(nameof(IsLinked), defaultBindingMode: BindingMode.TwoWay);

    /// <summary>How far the arms reach back towards the fields.</summary>
    private const double ArmLength = 7;

    /// <summary>The break in the middle the link sits in.</summary>
    private const double LinkHeight = 16;

    /// <summary>How wide the link is, which with the arm decides the whole width asked for.</summary>
    private const double LinkWidth = 9;

    /// <summary>Lit the same as focused, so a pointer and a Tab key say the same thing.</summary>
    private bool _hovered;

    /// <summary>Focusable, since the bracket answers Enter as well as a click.</summary>
    static Linker()
    {
        AffectsRender<Linker>(IsLinkedProperty);
        FocusableProperty.OverrideDefaultValue<Linker>(true);
    }

    /// <inheritdoc cref="IsLinkedProperty"/>
    public bool IsLinked
    {
        get => GetValue(IsLinkedProperty);
        set => SetValue(IsLinkedProperty, value);
    }

    /// <summary>
    /// As wide as the bracket needs and as tall as it is given, since the two fields it spans
    /// are what decide the height.
    /// </summary>
    /// <remarks>
    /// Offered an unbounded height, which is what a stack panel does, it asks for enough to draw
    /// two arms and a link rather than nothing at all.
    /// </remarks>
    protected override Size MeasureOverride(Size available)
    {
        double height = double.IsInfinity(available.Height) ? 48 : available.Height;

        return new Size(ArmLength + LinkWidth + 2, height);
    }

    /// <summary>
    /// The bracket: an arm at the top reaching back over the first field, an arm at the foot
    /// reaching over the second, and the spine between them broken for the link.
    /// </summary>
    /// <remarks>
    /// The spine is put on a half pixel so a one and a half pixel line lands on the grid rather
    /// than being smeared across two columns of it.
    /// </remarks>
    public override void Render(DrawingContext context)
    {
        double width = Bounds.Width;
        double height = Bounds.Height;

        if (width <= 2 || height <= LinkHeight) return;

        var palette = ThemePalette.From(this);

        var colour = IsLinked ? palette.Accent : palette.Border;
        if (_hovered || IsFocused) colour = IsLinked ? palette.Accent : palette.Muted;

        var pen = new Pen(new SolidColorBrush(colour, IsLinked ? 1 : 0.9), 1.5)
        {
            LineCap = PenLineCap.Round
        };

        double spine = Math.Floor(width - LinkWidth / 2) + 0.5;
        double middle = height / 2;
        double gap = LinkHeight / 2;

        context.DrawLine(pen, new Point(spine - ArmLength, 0.5), new Point(spine, 0.5));
        context.DrawLine(pen, new Point(spine, 0.5), new Point(spine, middle - gap));

        context.DrawLine(pen, new Point(spine, middle + gap), new Point(spine, height - 0.5));
        context.DrawLine(pen, new Point(spine - ArmLength, height - 0.5), new Point(spine, height - 0.5));

        DrawLink(context, pen, spine, middle);
    }

    /// <summary>
    /// The link itself: two halves that meet when it is closed and are pulled apart when it
    /// is not.
    /// </summary>
    private void DrawLink(DrawingContext context, Pen pen, double spine, double middle)
    {
        double half = LinkWidth / 2;
        double reach = IsLinked ? 0 : 2.5;

        var top = new Rect(spine - half, middle - LinkHeight / 2 - reach + 1, LinkWidth, LinkHeight / 2 + 1);
        var foot = new Rect(spine - half, middle + reach - 1, LinkWidth, LinkHeight / 2 + 1);

        context.DrawRectangle(null, pen, new RoundedRect(top, half, half, 0, 0));
        context.DrawRectangle(null, pen, new RoundedRect(foot, 0, 0, half, half));
    }

    /// <summary>Lights the bracket, since the lit colour is worked out in <see cref="Render"/>.</summary>
    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);

        _hovered = true;
        InvalidateVisual();
    }

    /// <summary>And puts it back down again.</summary>
    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);

        _hovered = false;
        InvalidateVisual();
    }

    /// <summary>A click opens or closes the link, and takes the focus so Enter works next.</summary>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        IsLinked = !IsLinked;
        Focus();

        e.Handled = true;
    }

    /// <summary>
    /// Enter works it from the keyboard. Not space: the window takes that for the transport
    /// before any control sees it.
    /// </summary>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key != Key.Enter) return;

        IsLinked = !IsLinked;
        e.Handled = true;
    }
}
