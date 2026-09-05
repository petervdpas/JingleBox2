using System;
using System.Globalization;
using Avalonia;
using Avalonia.Media;
using JingleBox2.Rack.Controls;
using JingleBox2.Rack.Controls.Records;

namespace JingleBox2.Views;

/// <summary>
/// How far something has got, in minutes, seconds and milliseconds.
/// </summary>
/// <remarks>
/// A control that draws its own reading rather than a text block bound to a string, and the reason
/// is the rate. A clock with milliseconds on it is a property that changes many times a second,
/// which is exactly the shape this application has already paid for twice: a value that costs
/// nothing to say landing on something that costs a great deal to draw. Here it is the cheapest
/// possible thing to draw, one piece of lettering in its own box, and nothing around it is
/// invalidated when it moves.
///
/// Monospaced, in the same face the pattern is drawn in, because a proportional font makes the
/// digits shuffle sideways as they count and a clock nobody can read at a glance is furniture.
///
/// **It is told the time rather than keeping it.** Two pages show one, the transport's and the
/// recorder's, and each already has a timer running at the rate its meters want; a control that
/// ran a clock of its own would be a third one, ticking on a page nobody is looking at.
/// </remarks>
public sealed class TimeReadout : ThemedControl
{
    /// <summary>How far it has got. Anything below nought reads as nought.</summary>
    public static readonly StyledProperty<TimeSpan> TimeProperty =
        AvaloniaProperty.Register<TimeReadout, TimeSpan>(nameof(Time));

    /// <summary>How tall the lettering is.</summary>
    public static readonly StyledProperty<double> ReadingSizeProperty =
        AvaloniaProperty.Register<TimeReadout, double>(nameof(ReadingSize), 24);

    /// <summary>
    /// The longest reading the box is measured against.
    /// </summary>
    /// <remarks>
    /// The rule every value control here keeps: a box is as wide as the widest thing it can be
    /// asked to show, so it does not resize under its own reading and shove its neighbours about
    /// as the digits roll over. Three places for the minutes, which is sixteen hours, and past
    /// that the reading is drawn a little wider rather than the layout being disturbed for a case
    /// nobody has.
    ///
    /// Deliberately not in the measuring itself: <see cref="TimeProperty"/> is in
    /// <c>AffectsRender</c> and never in <c>AffectsMeasure</c>, since a control that remeasures
    /// twenty times a second is the fault this is written to avoid.
    /// </remarks>
    private const string Widest = "000:00.000";

    /// <summary>How much room is left either side of the reading.</summary>
    private const double Side = 4;

    static TimeReadout()
    {
        AffectsRender<TimeReadout>(TimeProperty, ReadingSizeProperty);
        AffectsMeasure<TimeReadout>(ReadingSizeProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<TimeReadout>(false);
    }

    /// <inheritdoc cref="TimeProperty"/>
    public TimeSpan Time
    {
        get => GetValue(TimeProperty);
        set => SetValue(TimeProperty, value);
    }

    /// <inheritdoc cref="ReadingSizeProperty"/>
    public double ReadingSize
    {
        get => GetValue(ReadingSizeProperty);
        set => SetValue(ReadingSizeProperty, value);
    }

    /// <summary>
    /// The reading, as minutes, seconds and thousandths.
    /// </summary>
    /// <remarks>
    /// The minutes are the whole span rather than the minutes within an hour, so a long take
    /// reads 74:12.480 and not 1:14:12.480. One field fewer to read, no hour sitting at nought
    /// for the whole of every ordinary use, and nothing that appears and disappears as the
    /// reading crosses an hour.
    /// </remarks>
    /// <param name="time">How far it has got.</param>
    /// <returns>The reading, which is <c>00:00.000</c> at nought and never blank.</returns>
    public static string Reading(TimeSpan time)
    {
        if (time < TimeSpan.Zero || time == default) return "00:00.000";

        return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}.{2:000}",
            (int)time.TotalMinutes, time.Seconds, time.Milliseconds);
    }

    /// <summary>The reading, drawn once, in the middle of whatever room it was given.</summary>
    public override void Render(DrawingContext context)
    {
        var palette = ThemePalette.From(this);
        var text = Lettering(Reading(Time), palette.MutedBrush);

        context.DrawText(text, new Point(
            (Bounds.Width - text.Width) / 2,
            (Bounds.Height - text.Height) / 2));
    }

    /// <inheritdoc/>
    /// <remarks>Against <see cref="Widest"/> and never against the reading, so it does not move.</remarks>
    protected override Size MeasureOverride(Size available)
    {
        var widest = Lettering(Widest, Brushes.Black);

        return new Size(widest.Width + Side * 2, widest.Height);
    }

    /// <summary>One piece of lettering in the pattern's own face.</summary>
    /// <param name="text">What it says.</param>
    /// <param name="brush">What colour it is drawn in.</param>
    private FormattedText Lettering(string text, IBrush brush) =>
        new(text,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(PatternFont.Family),
            ReadingSize,
            brush);
}
