using Avalonia;
using Avalonia.Controls;
using System;

namespace JingleBox2.Views;

/// <summary>
/// Two things side by side while there is room for both, and one under the other when there
/// is not.
/// </summary>
/// <remarks>
/// Avalonia has no way to say this in a style: there are no width triggers, so a layout that
/// wants to change shape has to be a panel that knows how. A wrap panel is nearly it, except
/// that it gives every child the size it asks for, and the point here is that the first child
/// takes whatever the second one leaves.
///
/// The first child is the one that stretches. The second keeps the width it asks for, because
/// it is a fixed thing standing beside the first: a grid of pads, a picture, a set of readouts.
/// <see cref="Least"/> is how narrow the first is allowed to get before they stop standing
/// side by side.
/// </remarks>
public class Beside : Panel
{
    /// <summary>How much room between the two, either way round.</summary>
    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<Beside, double>(nameof(Gap), 12);

    /// <summary>The narrowest the first child may be squeezed to before they stack.</summary>
    public static readonly StyledProperty<double> LeastProperty =
        AvaloniaProperty.Register<Beside, double>(nameof(Least), 360);

    /// <summary>
    /// Half each while they stand side by side, rather than one taking what the other leaves.
    /// </summary>
    /// <remarks>
    /// For two of a kind: two cards saying different things about the same subject, which look
    /// wrong when one is twice the other for no reason anybody can see.
    /// </remarks>
    public static readonly StyledProperty<bool> EvenProperty =
        AvaloniaProperty.Register<Beside, bool>(nameof(Even));

    static Beside()
    {
        AffectsMeasure<Beside>(GapProperty, LeastProperty, EvenProperty);
    }

    public double Gap
    {
        get => GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    public double Least
    {
        get => GetValue(LeastProperty);
        set => SetValue(LeastProperty, value);
    }

    public bool Even
    {
        get => GetValue(EvenProperty);
        set => SetValue(EvenProperty, value);
    }

    /// <summary>True while the two are standing side by side rather than stacked.</summary>
    private bool _abreast;

    protected override Size MeasureOverride(Size available)
    {
        if (Children.Count == 0) return default;

        var main = Children[0];

        if (Children.Count == 1)
        {
            main.Measure(available);
            return main.DesiredSize;
        }

        var aside = Children[1];

        if (Even)
        {
            double half = (available.Width - Gap) / 2;

            _abreast = !double.IsInfinity(available.Width) && half >= Least;

            if (_abreast)
            {
                main.Measure(new Size(half, available.Height));
                aside.Measure(new Size(half, available.Height));

                return new Size(
                    available.Width,
                    Math.Max(main.DesiredSize.Height, aside.DesiredSize.Height));
            }

            main.Measure(available);
            aside.Measure(available);

            return new Size(
                Math.Max(main.DesiredSize.Width, aside.DesiredSize.Width),
                main.DesiredSize.Height + Gap + aside.DesiredSize.Height);
        }

        // Asked for its own size first: what it wants is what decides whether the other one
        // still has enough room to stand next to it.
        aside.Measure(new Size(double.PositiveInfinity, available.Height));

        double room = available.Width - Gap - aside.DesiredSize.Width;

        _abreast = !double.IsInfinity(available.Width) && room >= Least;

        if (_abreast)
        {
            main.Measure(new Size(room, available.Height));

            return new Size(
                room + Gap + aside.DesiredSize.Width,
                Math.Max(main.DesiredSize.Height, aside.DesiredSize.Height));
        }

        main.Measure(available);

        return new Size(
            Math.Max(main.DesiredSize.Width, aside.DesiredSize.Width),
            main.DesiredSize.Height + Gap + aside.DesiredSize.Height);
    }

    protected override Size ArrangeOverride(Size final)
    {
        if (Children.Count == 0) return final;

        var main = Children[0];

        if (Children.Count == 1)
        {
            main.Arrange(new Rect(final));
            return final;
        }

        var aside = Children[1];

        if (_abreast)
        {
            double kept = Even ? Math.Max(0, (final.Width - Gap) / 2) : aside.DesiredSize.Width;
            double room = Math.Max(0, final.Width - Gap - kept);

            main.Arrange(new Rect(0, 0, room, final.Height));
            aside.Arrange(new Rect(room + Gap, 0, kept, final.Height));

            return final;
        }

        double first = main.DesiredSize.Height;

        main.Arrange(new Rect(0, 0, final.Width, first));
        aside.Arrange(new Rect(0, first + Gap, final.Width, Math.Max(0, final.Height - first - Gap)));

        return final;
    }
}
