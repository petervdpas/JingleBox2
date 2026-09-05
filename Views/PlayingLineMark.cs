using Avalonia;
using Avalonia.Media;
using JingleBox2.Rack.Controls;
using JingleBox2.Rack.Controls.Records;

namespace JingleBox2.Views;

/// <summary>
/// The band across the line the transport is on, laid over the pattern rather than drawn into it.
/// </summary>
/// <remarks>
/// It is one filled rectangle, and it used to cost the whole page. The grid draws a piece of
/// lettering for every field of every cell, and moving the band was a property on the grid, so
/// every line the transport reached repainted the lot: measured on an empty four track pattern at
/// 120 beats a minute that is eight repaints a second, thirteen hundred pieces of lettering
/// apiece, and **48 megabytes a second** allocated on the drawing thread. The runtime answered
/// with sixty collections and a third of a second of every thread stopped in every five, and a
/// stopped thread is a stumble in the audio however little the mixing itself is doing. The same
/// transport with any other page in front allocated 0.1 MB/s.
///
/// Laying the lettering out once and keeping it took that to 20. What was left is Avalonia's own
/// cost of asking for a piece of text to be drawn, about two kilobytes a call whatever it says,
/// and the only way past that is to make fewer calls. So the band moved out: the grid now repaints
/// when somebody edits something, and the transport repaints one rectangle.
///
/// A layer over the grid rather than under it, which is a real difference and a small one: the
/// tint is a fifth opaque, so it washes the lettering instead of sitting behind it. Under it would
/// mean the grid painting no background of its own, and a control with no background is a control
/// that takes no clicks, which the grid very much does.
///
/// It takes no clicks itself, and that is why it can sit on top at all.
/// </remarks>
public sealed class PlayingLineMark : ThemedControl
{
    /// <summary>Which line the transport is on, or below nought when it is not running.</summary>
    public static readonly StyledProperty<int> PlayingLineProperty =
        AvaloniaProperty.Register<PlayingLineMark, int>(nameof(PlayingLine), -1);

    /// <summary>How tall one line is, which has to be the grid's own.</summary>
    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<PlayingLineMark, double>(nameof(RowHeight), 18);

    /// <summary>
    /// How much empty room stands above line nought, which is the grid's own top pad.
    /// </summary>
    /// <remarks>
    /// Half a screen, because the cursor rests on the middle of the page and the pattern runs
    /// under it. Told rather than worked out, for the reason the grid is told it: a control
    /// measured inside a scroll viewer with no height limit never learns how tall the hole it is
    /// seen through is.
    /// </remarks>
    public static readonly StyledProperty<double> TopPadProperty =
        AvaloniaProperty.Register<PlayingLineMark, double>(nameof(TopPad), 0);

    /// <summary>How much of the accent the band carries.</summary>
    /// <remarks>The grid's own number, since this is the same band it used to draw itself.</remarks>
    private const byte Tint = 60;

    static PlayingLineMark()
    {
        AffectsRender<PlayingLineMark>(PlayingLineProperty, RowHeightProperty, TopPadProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<PlayingLineMark>(false);
    }

    /// <inheritdoc cref="PlayingLineProperty"/>
    public int PlayingLine
    {
        get => GetValue(PlayingLineProperty);
        set => SetValue(PlayingLineProperty, value);
    }

    /// <inheritdoc cref="RowHeightProperty"/>
    public double RowHeight
    {
        get => GetValue(RowHeightProperty);
        set => SetValue(RowHeightProperty, value);
    }

    /// <inheritdoc cref="TopPadProperty"/>
    public double TopPad
    {
        get => GetValue(TopPadProperty);
        set => SetValue(TopPadProperty, value);
    }

    /// <summary>The band, or nothing at all while the transport is stopped.</summary>
    public override void Render(DrawingContext context)
    {
        if (PlayingLine < 0 || RowHeight <= 0) return;

        var palette = ThemePalette.From(this);

        context.FillRectangle(
            palette.AccentTint(Tint),
            new Rect(0, TopPad + PlayingLine * RowHeight, Bounds.Width, RowHeight));
    }
}
