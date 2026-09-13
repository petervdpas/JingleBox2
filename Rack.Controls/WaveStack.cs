using System;
using Avalonia;
using Avalonia.Media;
using JingleBox2.Rack.Controls.Records;
using JingleBox2.Rack.SoundDevices.Faces;
using JingleBox2.Rack.SoundDevices.Faces.Interfaces;

namespace JingleBox2.Rack.Controls;

/// <summary>
/// Every wave a sound goes through, stacked back to front the way the Fairlight drew them.
/// </summary>
/// <remarks>
/// The first wave is at the front and the last at the back, each one up and to the right of the
/// one before it, so the stack reads as the sound moving away from you as it goes on. The two
/// drawn by hand are in the accent colour and the thirty between them are fainter, since those are
/// worked out rather than drawn, and they are worked out by <see cref="IWaveSegments"/>, which is
/// what the sound is worked out by as well.
///
/// The background is laid under each wave, from its line down to the foot of its room, before the
/// line is drawn, so a wave nearer the front hides the part of the one behind it that it covers. Without that thirty two lines on top
/// of each other are a scribble; with it they are a surface.
/// </remarks>
public sealed class WaveStack : ThemedControl
{
    /// <summary>Backs <see cref="Begin"/>, the first wave.</summary>
    public static readonly StyledProperty<double[]?> BeginProperty =
        AvaloniaProperty.Register<WaveStack, double[]?>(nameof(Begin));

    /// <summary>Backs <see cref="End"/>, the last wave.</summary>
    public static readonly StyledProperty<double[]?> EndProperty =
        AvaloniaProperty.Register<WaveStack, double[]?>(nameof(End));

    /// <summary>Backs <see cref="InHand"/>, which of the two ends is being drawn.</summary>
    public static readonly StyledProperty<bool> InHandProperty =
        AvaloniaProperty.Register<WaveStack, bool>(nameof(InHand));

    /// <summary>How far across the control one wave takes, the rest being the room the stack leans into.</summary>
    private const double WaveWidth = 0.66;

    /// <summary>How far up the control one wave takes.</summary>
    private const double WaveHeight = 0.46;

    /// <summary>The merge, shared with the sound.</summary>
    private readonly IWaveSegments _segments = new WaveSegments();

    /// <summary>One wave between the ends, kept so drawing thirty of them allocates nothing but the geometry.</summary>
    private readonly double[] _wave = new double[WaveSegments.Points];

    /// <summary>Says which properties change the picture. None of them changes the size.</summary>
    static WaveStack()
    {
        AffectsRender<WaveStack>(BeginProperty, EndProperty, InHandProperty);
    }

    /// <summary>Sets the least room a stack takes; it is otherwise as big as it is given.</summary>
    public WaveStack()
    {
        MinWidth = 120;
        MinHeight = 90;
    }

    /// <summary>The first wave, each point from -1 to 1.</summary>
    public double[]? Begin
    {
        get => GetValue(BeginProperty);
        set => SetValue(BeginProperty, value);
    }

    /// <summary>The last wave, each point from -1 to 1.</summary>
    public double[]? End
    {
        get => GetValue(EndProperty);
        set => SetValue(EndProperty, value);
    }

    /// <summary>True while the last wave is the one being drawn, which is drawn brighter than the first.</summary>
    public bool InHand
    {
        get => GetValue(InHandProperty);
        set => SetValue(InHandProperty, value);
    }

    /// <summary>The frame, then every wave from the back to the front.</summary>
    public override void Render(DrawingContext context)
    {
        double width = Bounds.Width;
        double height = Bounds.Height;

        if (width <= 8 || height <= 8) return;

        var palette = ThemePalette.From(this);
        var ground = new SolidColorBrush(palette.Background);

        context.DrawRectangle(ground, new Pen(palette.BorderBrush, 1), new RoundedRect(new Rect(0, 0, width, height), 4));

        const double inset = 6;
        double room = width - (inset * 2);
        double tall = height - (inset * 2);
        double waveWidth = room * WaveWidth;
        double waveHeight = tall * WaveHeight;
        double stepAcross = (room - waveWidth) / (WaveSegments.Count - 1);
        double stepUp = (tall - waveHeight) / (WaveSegments.Count - 1);

        var begin = Begin ?? Array.Empty<double>();
        var end = End ?? Array.Empty<double>();

        var faint = new Pen(palette.AccentTint(90), 1);
        var first = new Pen(InHand ? palette.AccentTint(170) : palette.AccentBrush, InHand ? 1.5 : 2);
        var last = new Pen(InHand ? palette.AccentBrush : palette.AccentTint(170), InHand ? 2 : 1.5);

        for (int wave = WaveSegments.Count - 1; wave >= 0; wave--)
        {
            double left = inset + (wave * stepAcross);
            double top = inset + tall - waveHeight - (wave * stepUp);

            _segments.Between(begin, end, (double)wave / (WaveSegments.Count - 1), _wave);

            var pen = wave == 0 ? first : wave == WaveSegments.Count - 1 ? last : faint;

            Draw(context, ground, pen, left, top, waveWidth, waveHeight);
        }
    }

    /// <summary>One wave in its place: the ground under it down to the foot of its room, then the line.</summary>
    private void Draw(DrawingContext context, IBrush ground, IPen pen, double left, double top, double width, double height)
    {
        double step = width / (WaveSegments.Points - 1);
        double middle = top + (height / 2);

        var line = new StreamGeometry();
        var under = new StreamGeometry();

        using (var sink = line.Open())
        using (var fill = under.Open())
        {
            fill.BeginFigure(new Point(left, top + height), true);

            for (int point = 0; point < WaveSegments.Points; point++)
            {
                var at = new Point(left + (point * step), middle - (_wave[point] * height / 2));

                if (point == 0) sink.BeginFigure(at, false);
                else sink.LineTo(at);

                fill.LineTo(at);
            }

            fill.LineTo(new Point(left + width, top + height));
            fill.EndFigure(true);
            sink.EndFigure(false);
        }

        context.DrawGeometry(ground, null, under);
        context.DrawGeometry(null, pen, line);
    }
}
