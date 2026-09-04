using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using JingleBox2.Views.Interfaces;

namespace JingleBox2.Views;

/// <summary>
/// What a pad wears while it is sounding: its own colour, walking through the ones beside it.
/// </summary>
/// <remarks>
/// Laid over the pad and under everything on it, so the lettering, the meters and the countdown
/// are not what is moving. It paints nothing at all while the pad is quiet, which is what leaves
/// the pad wearing its own background the rest of the time.
///
/// A control that draws rather than a style that animates, because what it draws depends on the
/// pad's own colour and an animation in a style can only move between colours written into it.
/// Which colour at which moment is <see cref="IPulseColour"/> and has no control in it.
///
/// A frame rather than a timer, the same way the meter's peak falls: it runs at whatever rate
/// the window is drawing at, and it stops asking the moment the pad does, so nine quiet pads
/// cost nothing.
/// </remarks>
public sealed class PadPulse : Control
{
    /// <summary>Which colour at which moment.</summary>
    private readonly IPulseColour _pulse = new PulseColour();

    /// <summary>How long the walk takes, there and back.</summary>
    /// <remarks>
    /// Slow enough to read as breathing rather than as flashing. A pad box under the hands is
    /// somebody's peripheral vision, and anything faster than about a second reads as an alarm.
    /// </remarks>
    private static readonly TimeSpan Cycle = TimeSpan.FromSeconds(1.8);

    /// <summary>
    /// How often it is redrawn at most, which is well under what a screen offers.
    /// </summary>
    /// <remarks>
    /// A walk that takes the better part of two seconds is smooth at thirty a second, and the
    /// difference between that and whatever the display runs at is work nobody can see. It
    /// matters because of what else is in this process: a managed language pauses every thread
    /// to collect, so drawing that allocates or runs hotter than it needs to is heard rather
    /// than seen. See <c>docs/threads.md</c>.
    /// </remarks>
    private static readonly TimeSpan Rest = TimeSpan.FromMilliseconds(33);

    /// <summary>Where the cycle has got to, running while anything is playing.</summary>
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    /// <summary>
    /// The one brush this ever makes, its colour moved rather than replaced.
    /// </summary>
    /// <remarks>
    /// A brush per frame is an allocation per frame per playing pad, which is nothing on its own
    /// and is exactly the kind of nothing that adds up to a collection: what that costs here is
    /// not a dropped frame but every thread in the process stopped, the audio ones included.
    /// </remarks>
    private readonly SolidColorBrush _brush = new();

    /// <summary>Whether a frame has been asked for and not yet arrived.</summary>
    private bool _waiting;

    /// <summary>
    /// What is handed to the frame clock, made once.
    /// </summary>
    /// <remarks>
    /// The same reasoning as the brush. A lambda that reaches for anything of this control's is
    /// an object made every time it is handed over, which is thirty a second per playing pad and
    /// nothing at all until it is a collection: what that costs in this process is every thread
    /// stopped, the audio ones included.
    /// </remarks>
    private Action<TimeSpan>? _frame;

    /// <summary>When it was last drawn, so the rest between frames can be kept.</summary>
    private TimeSpan _drawn = TimeSpan.MinValue;

    /// <inheritdoc cref="Colour"/>
    public static readonly StyledProperty<Color> ColourProperty =
        AvaloniaProperty.Register<PadPulse, Color>(nameof(Colour));

    /// <inheritdoc cref="Playing"/>
    public static readonly StyledProperty<bool> PlayingProperty =
        AvaloniaProperty.Register<PadPulse, bool>(nameof(Playing));

    /// <inheritdoc cref="Corner"/>
    public static readonly StyledProperty<double> CornerProperty =
        AvaloniaProperty.Register<PadPulse, double>(nameof(Corner), 12);

    /// <summary>Both of the things it draws from, and the shape it draws in.</summary>
    static PadPulse()
    {
        AffectsRender<PadPulse>(ColourProperty, PlayingProperty, CornerProperty);
    }

    /// <summary>Builds one. It takes no clicks, since the pad underneath is what is pressed.</summary>
    public PadPulse() => IsHitTestVisible = false;

    /// <summary>The pad's own colour, which is where the walk starts and ends.</summary>
    public Color Colour
    {
        get => GetValue(ColourProperty);
        set => SetValue(ColourProperty, value);
    }

    /// <summary>Whether the pad is sounding, which is the whole of when this draws anything.</summary>
    public bool Playing
    {
        get => GetValue(PlayingProperty);
        set => SetValue(PlayingProperty, value);
    }

    /// <summary>How round the corners are, to sit inside the pad's own.</summary>
    public double Corner
    {
        get => GetValue(CornerProperty);
        set => SetValue(CornerProperty, value);
    }

    /// <summary>
    /// The pad's colour where the cycle has got to, and a frame asked for to move it on.
    /// </summary>
    /// <remarks>
    /// The next frame is asked for from inside the drawing, so the asking stops on its own the
    /// moment the pad stops playing: there is no timer to remember to switch off and nothing to
    /// leak when the page is left.
    /// </remarks>
    /// <param name="context">Where it is drawn.</param>
    public override void Render(DrawingContext context)
    {
        base.Render(context);

        if (!Playing || Bounds.Width <= 0 || Bounds.Height <= 0) return;

        var now = _clock.Elapsed;

        _drawn = now;

        double phase = now.TotalSeconds % Cycle.TotalSeconds / Cycle.TotalSeconds;

        _brush.Color = _pulse.At(Colour, phase);

        context.DrawRectangle(_brush, null, new Rect(Bounds.Size), Corner, Corner);

        NextFrame();
    }

    /// <summary>
    /// Asks for one more frame, and only ever one at a time.
    /// </summary>
    /// <remarks>
    /// The frame is asked for at the screen's rate and only acted on once <see cref="Rest"/> has
    /// passed, so what this costs between redraws is one comparison rather than a control drawn
    /// again. Asking again from inside the callback is what keeps the walk going while nothing
    /// else on the page changes.
    /// </remarks>
    private void NextFrame()
    {
        if (_waiting || TopLevel.GetTopLevel(this) is not { } top) return;

        _waiting = true;

        _frame ??= _ =>
        {
            _waiting = false;

            if (!Playing) return;

            if (_clock.Elapsed - _drawn < Rest) NextFrame();
            else InvalidateVisual();
        };

        top.RequestAnimationFrame(_frame);
    }
}
