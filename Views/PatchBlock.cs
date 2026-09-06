using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using JingleBox2.Rack.Controls;
using JingleBox2.Rack.Controls.Records;
using JingleBox2.UI;
using JingleBox2.UI.Enums;
using JingleBox2.UI.Interfaces;
using JingleBox2.UI.Records;

namespace JingleBox2.Views;

/// <summary>
/// One thing on the patchbay: a titled block with connection points down its sides, moved by
/// its title bar.
/// </summary>
/// <remarks>
/// A control of its own rather than a shape drawn by the surface, because a block is a thing a
/// hand takes hold of: it is dragged, it is where a cable starts and ends, and what is under
/// the pointer is a question about one block rather than about the picture. The surface above
/// places them and draws the cables between them, and knows nothing about what is inside one.
///
/// Drawn rather than templated, like the knobs and the pattern grid: what it paints is a plate,
/// a word and a handful of dots, and a template would be six controls per block for the same
/// picture.
///
/// Where its parts sit is <see cref="IPatchGeometry"/> and never worked out here, since the
/// surface has to put a cable exactly on a dot this block drew.
/// </remarks>
public sealed class PatchBlock : ThemedControl
{
    /// <summary>How wide a block stands, whatever is written on it.</summary>
    /// <remarks>
    /// One width for all of them, so a column of blocks reads as a column. A name too long for
    /// it is trimmed rather than allowed to set the width, the same rule the mixer's source
    /// picker keeps: what a source is called is decided by whoever wrote the program, so a
    /// picture measured against it is a picture that moves about on its own.
    /// </remarks>
    public const double Across = 168;

    /// <summary>Where the block's parts sit. Holds nothing, so one serves every block.</summary>
    private static readonly IPatchGeometry Shape = new PatchGeometry();

    /// <summary>What this block is, by an id nothing draws.</summary>
    public static readonly StyledProperty<string> NodeProperty =
        AvaloniaProperty.Register<PatchBlock, string>(nameof(Node), "");

    /// <summary>What is written on its title bar.</summary>
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<PatchBlock, string>(nameof(Title), "");

    /// <summary>What it takes in, drawn down the left.</summary>
    public static readonly StyledProperty<IReadOnlyList<PatchPort>> InsProperty =
        AvaloniaProperty.Register<PatchBlock, IReadOnlyList<PatchPort>>(
            nameof(Ins), Array.Empty<PatchPort>());

    /// <summary>What it gives out, drawn down the right.</summary>
    public static readonly StyledProperty<IReadOnlyList<PatchPort>> OutsProperty =
        AvaloniaProperty.Register<PatchBlock, IReadOnlyList<PatchPort>>(
            nameof(Outs), Array.Empty<PatchPort>());

    /// <summary>Whether this block is the application itself rather than something outside it.</summary>
    /// <remarks>
    /// Drawn in the accent colour, because a patchbay from this program's own point of view has
    /// exactly one block in the middle that is us and everything else is somebody else's.
    /// </remarks>
    public static readonly StyledProperty<bool> IsOursProperty =
        AvaloniaProperty.Register<PatchBlock, bool>(nameof(IsOurs));

    /// <summary>The picture changes with all of it, and the height with the two port lists.</summary>
    static PatchBlock()
    {
        AffectsRender<PatchBlock>(
            TitleProperty, InsProperty, OutsProperty, IsOursProperty, IsSelectedProperty);
        AffectsMeasure<PatchBlock>(InsProperty, OutsProperty);
    }

    /// <inheritdoc cref="NodeProperty"/>
    public string Node
    {
        get => GetValue(NodeProperty);
        set => SetValue(NodeProperty, value);
    }

    /// <inheritdoc cref="TitleProperty"/>
    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <inheritdoc cref="InsProperty"/>
    public IReadOnlyList<PatchPort> Ins
    {
        get => GetValue(InsProperty);
        set => SetValue(InsProperty, value);
    }

    /// <inheritdoc cref="OutsProperty"/>
    public IReadOnlyList<PatchPort> Outs
    {
        get => GetValue(OutsProperty);
        set => SetValue(OutsProperty, value);
    }

    /// <inheritdoc cref="IsOursProperty"/>
    public bool IsOurs
    {
        get => GetValue(IsOursProperty);
        set => SetValue(IsOursProperty, value);
    }

    /// <summary>Whether this is the block the sidebar is about.</summary>
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<PatchBlock, bool>(nameof(IsSelected));

    /// <inheritdoc cref="IsSelectedProperty"/>
    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    /// <summary>Raised when the block is touched anywhere, so the surface can pick it out.</summary>
    /// <remarks>
    /// Raised on the press rather than on a click that goes nowhere, since a block is picked out
    /// by the same press that starts dragging it: waiting for the release would mean a block
    /// dragged across the surface was never picked, which is the one you were plainly working on.
    /// </remarks>
    public event Action? Touched;

    /// <summary>Raised when a press lands on one of the block's dots, with the port it is on.</summary>
    /// <remarks>
    /// The surface answers this rather than the block, because a cable has two ends on two
    /// different blocks and neither of them can see the other.
    /// </remarks>
    public event Action<PatchPort, PointerPressedEventArgs>? PortPressed;

    /// <summary>Raised while the title bar is dragged, with how far the pointer has moved.</summary>
    public event Action<Vector>? Dragged;

    /// <summary>Where the pointer was when the block was taken hold of, or nothing.</summary>
    private Point? _held;

    /// <summary>Takes no focus: the keyboard belongs to the page, and a block is pointed at.</summary>
    public PatchBlock() => Focusable = false;

    /// <inheritdoc/>
    /// <remarks>
    /// One width for every block and a height that follows whichever side has more ports, so two
    /// blocks with the same number of connections stand the same height wherever they are.
    /// </remarks>
    protected override Size MeasureOverride(Size available) =>
        new(Across, Shape.BlockHeight(Math.Max(Ins.Count, Outs.Count)));

    /// <summary>
    /// Where one of this block's dots sits, in the block's own coordinates.
    /// </summary>
    /// <remarks>
    /// Asked by the surface for both ends of every cable, which is why it is public: a cable
    /// that worked its own dot positions out would be a second spelling of this and would meet
    /// the dot on one theme and miss it on the next.
    /// </remarks>
    /// <param name="port">Which port, which must be one of this block's own.</param>
    /// <param name="channel">Which channel of it, counting from nought.</param>
    public Point Dot(PatchPort port, int channel)
    {
        var side = port.Side == PatchSide.In ? Ins : Outs;
        int row = IndexOf(side, port);

        if (row < 0) return default;

        double x = port.Side == PatchSide.In ? Shape.EdgeInset : Across - Shape.EdgeInset;
        var centres = Shape.ChannelCentres(Shape.RowCentre(row), (int)port.Channels);

        return new Point(x, centres[Math.Clamp(channel, 0, centres.Count - 1)]);
    }

    /// <summary>Which port a place on the block is on, or nothing where it is on none.</summary>
    /// <remarks>
    /// The side is decided by which half of the block the pointer is in rather than by how near
    /// the edge it is, so a hand aiming at a row lands on that row's port even when it is short
    /// of the dot: the dots are the target and the row is the reach.
    /// </remarks>
    /// <param name="at">Where the pointer is, in the block's own coordinates.</param>
    public PatchPort? PortAt(Point at)
    {
        bool left = at.X < Across / 2;
        var side = left ? Ins : Outs;

        int row = Shape.RowAt(at.Y, side.Count);
        if (row < 0) return null;

        double x = left ? Shape.EdgeInset : Across - Shape.EdgeInset;

        return Math.Abs(at.X - x) <= Shape.GrabRadius * 2 ? side[row] : null;
    }

    /// <summary>
    /// A press on a dot starts a cable; a press anywhere else takes hold of the block.
    /// </summary>
    /// <remarks>
    /// The whole block is a grip rather than the title bar alone. A block is a small thing on a
    /// large surface and the rows between its dots are most of it, so a grip that was the bar
    /// only would leave the middle of the block dead to a hand that is plainly on it.
    /// </remarks>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);

        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        var at = e.GetPosition(this);

        Touched?.Invoke();

        if (PortAt(at) is { } port)
        {
            PortPressed?.Invoke(port, e);
            return;
        }

        _held = at;
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The movement is handed over rather than acted on, since where a block is placed belongs
    /// to the surface: a block that moved itself would have to know how big the surface is and
    /// where every other block has got to.
    /// </remarks>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);

        if (_held is not { } from) return;

        var at = e.GetPosition(this);

        Dragged?.Invoke(at - from);
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);

        _held = null;
        e.Pointer.Capture(null);
    }

    /// <summary>Draws the plate, the title bar, the name and every dot with its own name.</summary>
    public override void Render(DrawingContext context)
    {
        var palette = ThemePalette.From(this);
        var size = Bounds.Size;

        var plate = new SolidColorBrush(ThemePalette.Shade(palette.Surface, 0.06));
        var edge = IsSelected
            ? new Pen(new SolidColorBrush(palette.Text), 2)
            : new Pen(new SolidColorBrush(IsOurs ? palette.Accent : palette.Border), IsOurs ? 1.5 : 1);

        context.DrawRectangle(plate, edge, new Rect(size), 4, 4);

        context.DrawRectangle(
            new SolidColorBrush(ThemePalette.Alpha(IsOurs ? palette.Accent : palette.Border, 0x40)),
            null,
            new Rect(0, 0, size.Width, Shape.HeaderHeight),
            4, 4);

        Write(context, Title, palette.Text, 11.5, new Point(8, 4), size.Width - 16);

        Dots(context, palette, Ins);
        Dots(context, palette, Outs);
    }

    /// <summary>Draws one side's dots and the word beside each of them.</summary>
    /// <remarks>
    /// The name is set inwards from the dots rather than outside the block, so two blocks side
    /// by side cannot have their lettering run into each other, and a cable arriving at the dot
    /// has clear air to arrive in.
    /// </remarks>
    private void Dots(DrawingContext context, ThemePalette palette, IReadOnlyList<PatchPort> ports)
    {
        var fill = new SolidColorBrush(IsOurs ? palette.Accent : palette.Muted);

        for (int row = 0; row < ports.Count; row++)
        {
            var port = ports[row];
            var centres = Shape.ChannelCentres(Shape.RowCentre(row), (int)port.Channels);
            double x = port.Side == PatchSide.In ? Shape.EdgeInset : Across - Shape.EdgeInset;

            foreach (double y in centres)
                context.DrawEllipse(fill, null, new Point(x, y), Shape.DotRadius, Shape.DotRadius);

            double left = port.Side == PatchSide.In ? Shape.EdgeInset + 10 : 0;
            double room = Across - Shape.EdgeInset - 10 - left;

            var words = Text(port.Name, palette.Muted, 10, room);

            context.DrawText(words, new Point(
                port.Side == PatchSide.In ? left : Across - Shape.EdgeInset - 10 - words.Width,
                Shape.RowCentre(row) - words.Height / 2));
        }
    }

    /// <summary>Draws one piece of lettering, trimmed to the room it has.</summary>
    private void Write(DrawingContext context, string words, Color colour, double size, Point at, double room) =>
        context.DrawText(Text(words, colour, size, room), at);

    /// <summary>Lays a piece of lettering out, trimmed rather than allowed to overrun.</summary>
    private static FormattedText Text(string words, Color colour, double size, double room) =>
        new(words ?? "", System.Globalization.CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight, Typeface.Default, size, new SolidColorBrush(colour))
        {
            MaxTextWidth = Math.Max(10, room),
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis
        };

    /// <summary>Which row a port is on, compared by what it says rather than by reference.</summary>
    private static int IndexOf(IReadOnlyList<PatchPort> ports, PatchPort port)
    {
        for (int index = 0; index < ports.Count; index++)
            if (ports[index] == port) return index;

        return -1;
    }
}
