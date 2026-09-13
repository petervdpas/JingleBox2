using System;
using JingleBox2.Rack.Controls.Interfaces;
using JingleBox2.Rack.SoundDevices.Faces;

namespace JingleBox2.Rack.Controls;

/// <inheritdoc/>
internal sealed class WavePen : IWavePen
{
    /// <summary>A sine, starting at nought and rising.</summary>
    public const string Sine = "Sine";

    /// <summary>A triangle, starting at nought and rising.</summary>
    public const string Triangle = "Triangle";

    /// <summary>A saw, falling from the top to the bottom.</summary>
    public const string Saw = "Saw";

    /// <summary>A square, the top for half the cycle and the bottom for the rest.</summary>
    public const string Square = "Square";

    /// <summary>The shapes in the order they are offered.</summary>
    public static readonly string[] Shapes = { Sine, Triangle, Saw, Square };

    /// <inheritdoc/>
    public double[] Stroke(double[] line, double fromAcross, double fromLevel, double toAcross, double toLevel)
    {
        var drawn = Whole(line);

        if (!double.IsFinite(fromAcross) || !double.IsFinite(toAcross)) return drawn;

        double fromAt = Place(fromAcross);
        double toAt = Place(toAcross);
        double fromValue = Level(fromLevel);
        double toValue = Level(toLevel);

        if (toAt < fromAt)
        {
            (fromAt, toAt) = (toAt, fromAt);
            (fromValue, toValue) = (toValue, fromValue);
        }

        int first = (int)Math.Round(fromAt);
        int last = (int)Math.Round(toAt);

        for (int point = first; point <= last; point++)
        {
            double past = last == first ? 1 : (double)(point - first) / (last - first);

            drawn[point] = fromValue + ((toValue - fromValue) * past);
        }

        return drawn;
    }

    /// <inheritdoc/>
    public double[]? Shape(string word)
    {
        Func<double, double>? shape = word switch
        {
            Sine => at => Math.Sin(2 * Math.PI * at),
            Triangle => at => at < 0.25 ? at * 4 : at < 0.75 ? 2 - (at * 4) : (at * 4) - 4,
            Saw => at => 1 - (2 * at),
            Square => at => at < 0.5 ? 1 : -1,
            _ => null,
        };

        if (shape is null) return null;

        var line = new double[WaveSegments.Points];

        for (int point = 0; point < line.Length; point++)
            line[point] = Math.Clamp(shape((double)point / WaveSegments.Points), -1, 1);

        return line;
    }

    /// <inheritdoc/>
    public double[] Smoothed(double[] line)
    {
        var from = Whole(line);
        var smooth = new double[from.Length];

        for (int point = 0; point < from.Length; point++)
        {
            double before = from[(point + from.Length - 1) % from.Length];
            double after = from[(point + 1) % from.Length];

            smooth[point] = (before + (2 * from[point]) + after) / 4;
        }

        return smooth;
    }

    /// <summary>A copy of the line, the length of the drawing, with anything that is not a level made silent.</summary>
    private static double[] Whole(double[]? line)
    {
        var copy = new double[WaveSegments.Points];

        if (line is null) return copy;

        for (int point = 0; point < copy.Length && point < line.Length; point++) copy[point] = Level(line[point]);

        return copy;
    }

    /// <summary>A place across the pad as a point, held to the drawing.</summary>
    private static double Place(double across) => Math.Clamp(across, 0, 1) * (WaveSegments.Points - 1);

    /// <summary>A level held to the range, with something that is not a number as nought.</summary>
    private static double Level(double level) => double.IsFinite(level) ? Math.Clamp(level, -1, 1) : 0;
}
