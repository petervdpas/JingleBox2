using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.VisualTree;
using JingleBox2.UI;
using JingleBox2.UI.Interfaces;
using System;

namespace JingleBox2.Views;

/// <summary>
/// Something under the pattern with a name on it, folded away by a line and sized by a grip.
/// </summary>
/// <remarks>
/// One shape for both of the things that live down there, the chain and the automation, because
/// they are the same offer: a track's business, taking room the pattern would otherwise have,
/// worth keeping only while you are working on it. Two spellings of that would eventually
/// disagree about which way the mark points.
///
/// It carries its own grip rather than standing between two rows of a grid with a splitter
/// across them, and that is the whole reason this exists as a control. A splitter shares one
/// length between the two rows it lies between, so the automation's handle was taking its room
/// off the chain above it rather than off the pattern: move one and the other moved with it. A
/// strip that owns its height answers only for itself, and the pattern, being the one thing
/// measured in what is left, gives up or takes back the difference without being asked.
///
/// The grip is a short bar rather than a hairline running the width of the strip, because along
/// the bottom of a card a hairline is exactly what that card's own edge looks like, so nobody
/// would ever try to pull it.
/// </remarks>
public sealed class FoldStrip : ContentControl
{
    /// <summary>Enough to be worth unfolding, and not so much that a drag can hide the music.</summary>
    private const double Least = 56;

    /// <summary>Past this the strip is a page, and a page is somewhere you go instead of the music.</summary>
    private const double Most = 720;

    /// <summary>The name on the line, which is what says whose business is folded away under it.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<FoldStrip, string>(nameof(Title), "");

    /// <summary>
    /// Whether what is under the line is showing.
    /// </summary>
    /// <remarks>
    /// Two way by default: the line is the only thing that changes it, and whoever is keeping
    /// the answer between one visit and the next has to hear about it.
    /// </remarks>
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<FoldStrip, bool>(
            nameof(IsOpen), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <summary>How tall the strip stands while it is open, which the grip sets.</summary>
    public static readonly StyledProperty<double> StripHeightProperty =
        AvaloniaProperty.Register<FoldStrip, double>(
            nameof(StripHeight), 120, defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    /// <inheritdoc cref="TitleProperty"/>
    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <inheritdoc cref="IsOpenProperty"/>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <inheritdoc cref="StripHeightProperty"/>
    /// <remarks>
    /// Held between <see cref="Least"/> and <see cref="Most"/> on the way in rather than in the
    /// drag, since a height also arrives from whatever remembered it between one visit and the
    /// next and a stored nonsense would be as bad as a dragged one.
    ///
    /// **And never under what is inside it**, since the room here is given rather than scrolled:
    /// a strip shorter than its contents does not hide the bottom of them, it cuts them off.
    /// See <see cref="ContentLeast"/>.
    /// </remarks>
    public double StripHeight
    {
        get => GetValue(StripHeightProperty);
        set => SetValue(StripHeightProperty, Math.Clamp(value, Math.Max(Least, ContentLeast), Most));
    }

    /// <summary>
    /// How tall what is inside it needs to be, so the grip cannot cut it off.
    /// </summary>
    /// <remarks>
    /// **A strip dragged shorter than what it holds does not hide the bottom of it, it cuts it
    /// off**, since the room inside is given rather than scrolled: the automation's own picker
    /// came out with half its second row missing and nothing anywhere saying why. So the least a
    /// strip can be is what is in it, measured rather than guessed at, because the chain and the
    /// automation hold different things and one number for both would be the larger of the two
    /// forced on the smaller.
    ///
    /// Held to <see cref="Most"/> as well, or a strip holding something very tall could not be
    /// folded down at all and would take the page.
    /// </remarks>
    public static readonly StyledProperty<double> ContentLeastProperty =
        AvaloniaProperty.Register<FoldStrip, double>(nameof(ContentLeast));

    /// <inheritdoc cref="ContentLeastProperty"/>
    public double ContentLeast
    {
        get => GetValue(ContentLeastProperty);
        set => SetValue(ContentLeastProperty, value);
    }

    /// <summary>
    /// Asks what is inside how tall it wants to be, before the room is shared out.
    /// </summary>
    /// <remarks>
    /// Measured with no ceiling, which is the only way to be told what something wants rather
    /// than what it was given: the presenter is handed the strip's own height, so asking it
    /// answers that height back.
    /// </remarks>
    protected override Size MeasureOverride(Size available)
    {
        if (IsOpen && Presenter?.Child is Layoutable inside)
        {
            inside.Measure(new Size(available.Width, double.PositiveInfinity));

            ContentLeast = Math.Min(Most, inside.DesiredSize.Height);

            if (StripHeight < ContentLeast) StripHeight = ContentLeast;
        }

        return base.MeasureOverride(available);
    }

    /// <summary>The bar along the top edge, once the template has been applied and there is one.</summary>
    private Thumb? _grip;

    /// <summary>
    /// Takes hold of the grip, and lets go of whichever one was there before.
    /// </summary>
    /// <remarks>
    /// A template can be applied more than once to the same control, and a handler left on the
    /// old thumb is a strip that resizes twice as fast as the hand moving it.
    /// </remarks>
    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (_grip is not null) _grip.DragDelta -= Dragged;

        _grip = e.NameScope.Find<Thumb>("Grip");

        if (_grip is not null) _grip.DragDelta += Dragged;
    }

    /// <summary>How tall a strip may be given the room. Holds nothing, so one serves them all.</summary>
    private static readonly IStripRoom Room = new StripRoom();

    /// <summary>
    /// Up is taller, which is the way round a hand expects when it is pulling a lid, and it
    /// stops where the thing above it would start to disappear.
    /// </summary>
    /// <remarks>
    /// A strip takes its room off whatever it is folded under, and the grid it is in gives an
    /// Auto row exactly what it asks for, so nothing else in that grid can refuse: without this
    /// the pattern goes to nothing and the strips fill the page. Shrinking is never refused,
    /// since the way back from a page full of strips has to be the same grip.
    /// </remarks>
    private void Dragged(object? sender, VectorEventArgs e)
    {
        double wanted = StripHeight - e.Vector.Y;

        if (wanted > StripHeight)
        {
            double tallest = Room.Tallest(
                (this.GetVisualParent() as Visual)?.Bounds.Height ?? 0,
                Siblings(),
                StripRoom.DefaultLeast,
                ContentLeast);

            if (StripHeight >= tallest) return;

            wanted = Math.Min(wanted, tallest);
        }

        StripHeight = wanted;
    }

    /// <summary>How much room the other strips beside this one are taking.</summary>
    /// <remarks>
    /// Read off what they were actually given rather than off what they asked for, since a
    /// folded strip is a line and an open one is a line and a card, and only the layout knows
    /// which of those each of them is right now.
    /// </remarks>
    private double Siblings()
    {
        double taken = 0;

        if (this.GetVisualParent() is not Visual parent) return taken;

        foreach (var child in parent.GetVisualChildren())
        {
            if (ReferenceEquals(child, this)) continue;
            if (child is not FoldStrip strip) continue;

            taken += strip.Bounds.Height;
        }

        return taken;
    }
}
