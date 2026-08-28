using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using System;

namespace JingleBox2.Views;

/// <summary>
/// Picking a colour by looking at it: a field of every shade of one hue, and the hues under it.
/// </summary>
/// <remarks>
/// A machine's colour is the one setting on it that cannot be typed. Six hex digits say nothing
/// about whether the thing will look like a rack device or like a mistake, and the only way to
/// find out is to move across the shades until it does. So the control is the picture rather
/// than the number, and the number is beside it for somebody who already has one.
///
/// Hue is kept in its own field rather than read back off the colour, because black has no hue
/// and grey has no hue: taking the value to nought and back would otherwise land on red every
/// time, and the shade under the hand would jump out from under it.
/// </remarks>
public class ColourPicker : Control
{
    /// <summary>What is picked. Written back as the hand moves, not when it is let go.</summary>
    public static readonly StyledProperty<Color> ColourProperty =
        AvaloniaProperty.Register<ColourPicker, Color>(
            nameof(Colour),
            Color.FromRgb(0x7B, 0x83, 0x8C),
            defaultBindingMode: BindingMode.TwoWay);

    /// <inheritdoc cref="ColourProperty"/>
    public Color Colour
    {
        get => GetValue(ColourProperty);
        set => SetValue(ColourProperty, value);
    }

    /// <summary>How deep the strip of hues along the bottom is.</summary>
    private const double BarHeight = 16;

    /// <summary>And how far it stands off the field above it.</summary>
    private const double Gap = 8;

    /// <summary>The rounding on the field and the strip, so they read as parts of one control.</summary>
    private const double Corner = 3;

    /// <summary>How big the ring around the picked shade is.</summary>
    private const double RingRadius = 6;

    /// <summary>Sets the smallest the picture is worth drawing at, and the crosshair over it.</summary>
    public ColourPicker()
    {
        MinWidth = 140;
        MinHeight = 96;

        Cursor = new Cursor(StandardCursorType.Cross);
    }

    /// <summary>Hue in degrees, and how far along the field the shade sits.</summary>
    private double _hue = 210;

    /// <summary>How far across the field the shade sits, nought at the pale edge.</summary>
    private double _saturation;

    /// <summary>How far down it sits, one at the top and nought in the black.</summary>
    private double _value = 0.55;

    /// <summary>True while this control is writing the colour, so it does not answer itself.</summary>
    private bool _writing;

    /// <summary>Follows a colour set from outside, and ignores the ones this control wrote itself.</summary>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property != ColourProperty || _writing) return;

        Read(Colour);

        InvalidateVisual();
    }

    /// <summary>
    /// Where a colour handed to us from outside puts the two markers.
    /// </summary>
    /// <remarks>
    /// Black and grey have no hue to read, so the one already showing is kept: it is the hue
    /// whose column the hand is standing in, and the only one that is not a guess.
    /// </remarks>
    private void Read(Color colour)
    {
        var hsv = colour.ToHsv();

        _saturation = hsv.S;
        _value = hsv.V;

        if (hsv.S > 0.0001 && hsv.V > 0.0001) _hue = hsv.H;
    }

    /// <summary>The field of shades, above the hues.</summary>
    private Rect Field => new(0, 0, Bounds.Width, Math.Max(0, Bounds.Height - BarHeight - Gap));

    /// <summary>The strip of hues, along the bottom.</summary>
    private Rect Bar => new(0, Math.Max(0, Bounds.Height - BarHeight), Bounds.Width, BarHeight);

    /// <summary>
    /// Draws the field as the pure hue washed towards white across and towards black down, with
    /// the ring on the shade, and the strip of hues under it with a marker in it.
    /// </summary>
    public override void Render(DrawingContext context)
    {
        var field = Field;

        if (field.Width > 0 && field.Height > 0)
        {
            var pure = new SolidColorBrush(HsvColor.FromHsv(_hue, 1, 1).ToRgb());

            context.DrawRectangle(pure, null, field, Corner, Corner);
            context.DrawRectangle(TowardsWhite, null, field, Corner, Corner);
            context.DrawRectangle(TowardsBlack, null, field, Corner, Corner);

            var at = new Point(
                field.X + _saturation * field.Width,
                field.Y + (1 - _value) * field.Height);

            Ring(context, at);
        }

        var bar = Bar;

        if (bar.Width > 0)
        {
            context.DrawRectangle(Hues, null, bar, Corner, Corner);

            double x = Math.Clamp(bar.X + _hue / 360 * bar.Width, bar.X + 3, bar.Right - 3);

            var mark = new Rect(x - 3, bar.Y - 1, 6, bar.Height + 2);

            context.DrawRectangle(null, Shadow, mark, Corner, Corner);
            context.DrawRectangle(null, Glint, mark, Corner, Corner);
        }
    }

    /// <summary>
    /// A ring rather than a dot, so the shade it is standing on can still be seen.
    /// </summary>
    /// <remarks>
    /// Twice, dark under pale: a white ring vanishes on white and a black one vanishes on black,
    /// and the field has both corners.
    /// </remarks>
    private static void Ring(DrawingContext context, Point at)
    {
        context.DrawEllipse(null, Shadow, at, RingRadius, RingRadius);
        context.DrawEllipse(null, Glint, at, RingRadius, RingRadius);
    }

    /// <summary>The dark half of every marker, drawn wide and under the pale one.</summary>
    private static readonly IPen Shadow = new Pen(new SolidColorBrush(Color.FromArgb(0xAA, 0, 0, 0)), 3);

    /// <summary>And the pale half, drawn thin over it.</summary>
    private static readonly IPen Glint = new Pen(Brushes.White, 1.6);

    /// <summary>The hue washed out to the left, which is saturation.</summary>
    private static readonly IBrush TowardsWhite = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(Colors.White, 0),
            new GradientStop(Color.FromArgb(0, 0xFF, 0xFF, 0xFF), 1),
        },
    };

    /// <summary>And put out towards the bottom, which is brightness.</summary>
    private static readonly IBrush TowardsBlack = new LinearGradientBrush
    {
        StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
        EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
        GradientStops =
        {
            new GradientStop(Color.FromArgb(0, 0, 0, 0), 0),
            new GradientStop(Colors.Black, 1),
        },
    };

    /// <summary>Every hue there is, left to right, ending where it started.</summary>
    private static readonly IBrush Hues = Wheel();

    /// <summary>Seven stops sixty degrees apart, so red is at both ends and the strip has no seam.</summary>
    private static IBrush Wheel()
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative),
        };

        for (int i = 0; i <= 6; i++)
        {
            brush.GradientStops.Add(new GradientStop(HsvColor.FromHsv(i * 60, 1, 1).ToRgb(), i / 6.0));
        }

        return brush;
    }

    /// <summary>Which of the two the hand took hold of, so a drag cannot wander into the other.</summary>
    private enum Held
    {
        Nothing,
        Shade,
        Hue,
    }

    /// <summary>What the hand took hold of, and nothing while it is off the control.</summary>
    private Held _held;

    /// <summary>
    /// Takes hold of whichever of the two the press landed in, and moves it there at once.
    /// </summary>
    /// <remarks>
    /// The pointer is captured, so a hand that runs off the side of the control while dragging
    /// keeps the marker rather than dropping it wherever it left.
    /// </remarks>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var at = e.GetPosition(this);

        _held = at.Y >= Bar.Y ? Held.Hue : Held.Shade;

        Take(at);

        e.Pointer.Capture(this);
        e.Handled = true;
    }

    /// <summary>Moves whatever was taken hold of, and nothing at all if it was neither.</summary>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_held == Held.Nothing) return;

        Take(e.GetPosition(this));

        e.Handled = true;
    }

    /// <summary>Lets go of the marker and of the pointer.</summary>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        if (_held == Held.Nothing) return;

        _held = Held.Nothing;

        e.Pointer.Capture(null);
        e.Handled = true;
    }

    /// <summary>Moves whichever marker is being dragged to where the hand is.</summary>
    private void Take(Point at)
    {
        if (_held == Held.Hue)
        {
            var bar = Bar;

            if (bar.Width <= 0) return;

            _hue = Math.Clamp((at.X - bar.X) / bar.Width, 0, 1) * 360;
        }
        else
        {
            var field = Field;

            if (field.Width <= 0 || field.Height <= 0) return;

            _saturation = Math.Clamp((at.X - field.X) / field.Width, 0, 1);
            _value = 1 - Math.Clamp((at.Y - field.Y) / field.Height, 0, 1);
        }

        Push();
    }

    /// <summary>Says what the two markers now come to, without hearing it back.</summary>
    private void Push()
    {
        _writing = true;

        try
        {
            SetCurrentValue(ColourProperty, HsvColor.FromHsv(_hue, _saturation, _value).ToRgb());
        }
        finally
        {
            _writing = false;
        }

        InvalidateVisual();
    }
}
