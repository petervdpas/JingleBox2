using System;
using Avalonia;
using Avalonia.Media;
using JingleBox2.Rack.Controls;
using JingleBox2.Rack.Controls.Records;

namespace JingleBox2.Views;

/// <summary>
/// Where the song has got to, drawn down the automation lane rather than into it.
/// </summary>
/// <remarks>
/// The same separation the pattern's own band was given, and for the same reason: a line the
/// transport moves several times a second was a property on a control that redraws its whole
/// picture, so the picture was being made again to move one line. Here the picture is the ground,
/// a grid line for every line of the pattern, and the shape with a point on it, which is a
/// hundred or so drawing calls on a sixty four line pattern and two hundred and fifty seven on
/// the longest one this application allows.
///
/// It is a smaller bill than the pattern grid's, because a line is cheaper than a piece of
/// lettering, and it is the same fault: **<c>AffectsRender</c> says nothing about how much work a
/// repaint is**, so the cheapest thing to say and the dearest thing to draw end up on one
/// invalidation. The strip is folded away to begin with and only one track's lane is ever shown,
/// so nobody was going to notice it until they opened it and left it open.
///
/// A layer over the lane rather than under it. The line is a hair wide and a fifth opaque, so
/// which side of the shape it falls on is not something anybody can see; being on top is what
/// lets it take no clicks and leave the lane underneath answering the mouse, which it must, since
/// a lane is edited by clicking on it.
/// </remarks>
public sealed class AutomationPlayhead : ThemedControl
{
    /// <summary>Which line the transport is on, or below nought when it is not running.</summary>
    public static readonly StyledProperty<int> PlayingLineProperty =
        AvaloniaProperty.Register<AutomationPlayhead, int>(nameof(PlayingLine), -1);

    /// <summary>How many lines the pattern has, which is what the width is shared out between.</summary>
    public static readonly StyledProperty<int> LinesProperty =
        AvaloniaProperty.Register<AutomationPlayhead, int>(nameof(Lines), 64);

    /// <summary>How much of the lettering colour the line carries.</summary>
    /// <remarks>The lane's own number, since this is the same line it used to draw itself.</remarks>
    private const byte Tint = 0x70;

    static AutomationPlayhead()
    {
        AffectsRender<AutomationPlayhead>(PlayingLineProperty, LinesProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<AutomationPlayhead>(false);
    }

    /// <inheritdoc cref="PlayingLineProperty"/>
    public int PlayingLine
    {
        get => GetValue(PlayingLineProperty);
        set => SetValue(PlayingLineProperty, value);
    }

    /// <inheritdoc cref="LinesProperty"/>
    public int Lines
    {
        get => GetValue(LinesProperty);
        set => SetValue(LinesProperty, value);
    }

    /// <summary>The line, or nothing at all while the transport is stopped.</summary>
    /// <remarks>
    /// Rounded onto a half pixel, the same as every other rule on this picture, so a one pixel
    /// line lands on one pixel rather than being washed across two.
    /// </remarks>
    public override void Render(DrawingContext context)
    {
        int lines = Math.Max(1, Lines);

        if (PlayingLine < 0 || PlayingLine >= lines) return;

        var size = Bounds.Size;

        if (size.Width <= 0 || size.Height <= 0) return;

        var palette = ThemePalette.From(this);
        var pen = new Pen(new SolidColorBrush(ThemePalette.Alpha(palette.Text, Tint)), 1);
        double x = Math.Round(PlayingLine / (double)lines * size.Width) + 0.5;

        context.DrawLine(pen, new Point(x, 0), new Point(x, size.Height));
    }
}
