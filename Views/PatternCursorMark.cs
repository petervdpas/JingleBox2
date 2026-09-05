using Avalonia;
using Avalonia.Media;
using JingleBox2.Rack.Controls;
using JingleBox2.Rack.Controls.Records;

namespace JingleBox2.Views;

/// <summary>
/// The box round the cell being typed into, laid over the pattern rather than drawn into it.
/// </summary>
/// <remarks>
/// The third thing on this page to be given a layer of its own, and the three are one lesson:
/// **a thing that moves many times a second must not share an invalidation with a thing that is
/// expensive to draw.** The transport's band was the first, the automation lane's line the second,
/// and this is the one a hand causes rather than the clock. Sixty arrow keys repainted the whole
/// pattern sixty times, which allocated 25 megabytes a second on the drawing thread, stopped every
/// thread in the process for a quarter of a second in every five, and took a block of audio 149%
/// past the time it had. Somebody typing a part into a looping song was doing that to their own
/// playback.
///
/// It is told where to draw rather than working it out, because the geometry belongs to the grid:
/// see <see cref="PatternGrid.CursorBoxProperty"/>.
///
/// Over the lettering rather than under it, which is what the grid did anyway: the box was drawn
/// last of all, and its fill is a fifth opaque so the cell reads through it.
/// </remarks>
public sealed class PatternCursorMark : ThemedControl
{
    /// <summary>Where the box goes, in the grid's coordinates, which are this control's too.</summary>
    public static readonly StyledProperty<Rect> BoxProperty =
        AvaloniaProperty.Register<PatternCursorMark, Rect>(nameof(Box));

    /// <summary>How much of the accent the fill carries.</summary>
    /// <remarks>The grid's own number, since this is the same box it used to draw itself.</remarks>
    private const byte Tint = 48;

    static PatternCursorMark()
    {
        AffectsRender<PatternCursorMark>(BoxProperty);
        IsHitTestVisibleProperty.OverrideDefaultValue<PatternCursorMark>(false);
    }

    /// <inheritdoc cref="BoxProperty"/>
    public Rect Box
    {
        get => GetValue(BoxProperty);
        set => SetValue(BoxProperty, value);
    }

    /// <summary>The box, or nothing at all before the grid has said where it goes.</summary>
    public override void Render(DrawingContext context)
    {
        var box = Box;

        if (box.Width <= 0 || box.Height <= 0) return;

        var palette = ThemePalette.From(this);

        context.FillRectangle(palette.AccentTint(Tint), box);
        context.DrawRectangle(new Pen(palette.AccentBrush, 1), box);
    }
}
