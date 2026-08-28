using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;
using System.Collections.Generic;

namespace JingleBox2.Machines.Ui;

/// <summary>
/// The ring the pointer leaves on a control while the controller is being laid out.
/// </summary>
/// <remarks>
/// Its own colour rather than the machine's accent, and deliberately not a colour a machine
/// is likely to be wearing: the point of it is to be plainly not part of the panel. Drawn as
/// rings that fade outward, because a single line reads as a border and a border on a panel
/// covered in borders is not something the eye finds.
///
/// Controls that already have something pointed at them get one quiet ring, so that turning
/// the mode on is also how you see what your controller is wired to.
/// </remarks>
public sealed class LinkGlow : Control
{
    /// <summary>The one under the pointer. Hot, and not any machine's accent.</summary>
    private static readonly Color Hot = Color.FromRgb(0xFF, 0x2D, 0x2D);

    /// <summary>
    /// True on the one control the pointer is offering, so it can light itself.
    /// </summary>
    /// <remarks>
    /// A panel draws the ring for its own elements because they are rectangles it knows and not
    /// controls at all. Everything else on the screen is a real control, and a control knows
    /// where it is: it lights itself in <c>Render</c> and no layer above it has to be told where
    /// it stands or kept in step when it moves.
    ///
    /// Here rather than beside the pointing, because this is the file that knows what the glow
    /// looks like, and the flag and the look should not be able to drift apart.
    /// </remarks>
    public static readonly AttachedProperty<bool> LitProperty =
        AvaloniaProperty.RegisterAttached<LinkGlow, Control, bool>("Lit");

    public static bool GetLit(Control control) => control.GetValue(LitProperty);

    public static void SetLit(Control control, bool value) => control.SetValue(LitProperty, value);

    /// <summary>
    /// The ring, on a control that is drawing itself.
    /// </summary>
    /// <remarks>
    /// Drawn inside the control rather than spreading beyond it, which is the one difference
    /// from the panel's version and it is forced: a control has no room outside itself that it
    /// is allowed to paint. So the rings run inward, and the innermost is the brightest.
    /// </remarks>
    public static void Paint(DrawingContext context, Rect area)
    {
        if (area.Width <= 0 || area.Height <= 0) return;

        for (int ring = Rings; ring >= 1; ring--)
        {
            double fade = 0.30 * (Rings - ring + 1) / Rings;
            double inset = ring * 1.5;

            if (area.Width <= inset * 2 || area.Height <= inset * 2) continue;

            context.DrawRectangle(
                null,
                new Pen(new SolidColorBrush(Hot, fade), 2),
                area.Deflate(inset),
                4, 4);
        }

        context.DrawRectangle(
            new SolidColorBrush(Hot, 0.14),
            new Pen(new SolidColorBrush(Hot), 1.5),
            area.Deflate(0.75),
            4, 4);
    }

    /// <summary>How far the rings spread beyond the control.</summary>
    private const int Rings = 4;

    private Rect? _offered;
    private IReadOnlyList<Rect> _taken = Array.Empty<Rect>();

    public void Showing(Rect? offered, IReadOnlyList<Rect> taken)
    {
        _offered = offered;
        _taken = taken ?? Array.Empty<Rect>();

        InvalidateVisual();
    }

    private static Rect Around(Rect area, double by) => area.Inflate(by);

    public override void Render(DrawingContext context)
    {
        var quiet = new SolidColorBrush(Hot, 0.45);

        foreach (var area in _taken)
            context.DrawRectangle(new SolidColorBrush(Hot, 0.07), new Pen(quiet, 1), Around(area, 2), 4, 4);

        if (_offered is not { } wanted) return;

        // Outward and fainter, so what is left is a glow rather than an outline.
        for (int ring = Rings; ring >= 1; ring--)
        {
            double fade = 0.30 * (Rings - ring + 1) / Rings;

            context.DrawRectangle(
                null,
                new Pen(new SolidColorBrush(Hot, fade), 2),
                Around(wanted, 2 + ring * 2),
                4 + ring, 4 + ring);
        }

        context.DrawRectangle(
            new SolidColorBrush(Hot, 0.16),
            new Pen(new SolidColorBrush(Hot), 2),
            Around(wanted, 2), 4, 4);
    }
}
