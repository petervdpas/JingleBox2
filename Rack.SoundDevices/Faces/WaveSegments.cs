using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using JingleBox2.Rack.SoundDevices.Faces.Interfaces;

namespace JingleBox2.Rack.SoundDevices.Faces;

/// <inheritdoc/>
public sealed class WaveSegments : IWaveSegments
{
    /// <summary>How many waves a sound goes through, the first and the last included.</summary>
    public const int Count = 32;

    /// <summary>How many points one wave is drawn with.</summary>
    public const int Points = 128;

    /// <summary>How many whole numbers there are above nought in a written point: eight bits.</summary>
    public const int Steps = 127;

    /// <inheritdoc/>
    public void Between(IReadOnlyList<double> begin, IReadOnlyList<double> end, double along, double[] into)
    {
        if (into is null) return;

        double at = double.IsNaN(along) ? 0 : Math.Clamp(along, 0, 1);

        for (int point = 0; point < into.Length; point++)
        {
            double from = Point(begin, point);
            double to = Point(end, point);

            into[point] = at <= 0 ? from : at >= 1 ? to : from + ((to - from) * at);
        }
    }

    /// <inheritdoc/>
    public double Stepped(double along) => (double)Wave(along) / (Count - 1);

    /// <inheritdoc/>
    public int Wave(double along)
    {
        double at = double.IsNaN(along) ? 0 : Math.Clamp(along, 0, 1);

        return Math.Min(Count - 1, (int)Math.Floor((at * (Count - 1)) + 1e-9));
    }

    /// <inheritdoc/>
    public string Spell(IReadOnlyList<double> points)
    {
        var said = new StringBuilder(Points * 4);

        for (int point = 0; point < Points; point++)
        {
            if (point > 0) said.Append(' ');

            double value = Point(points, point);

            said.Append(((int)Math.Round(value * Steps, MidpointRounding.AwayFromZero))
                .ToString(CultureInfo.InvariantCulture));
        }

        return said.ToString();
    }

    /// <inheritdoc/>
    public double[] Read(string said)
    {
        if (string.IsNullOrWhiteSpace(said)) return Array.Empty<double>();

        var read = new List<double>();
        bool any = false;

        foreach (string word in said.Split(new[] { ' ', ',', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!double.TryParse(word, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
            {
                read.Add(0);
                continue;
            }

            any |= double.IsFinite(number);

            read.Add(Level(double.IsFinite(number) ? number / Steps : 0));
        }

        if (!any) return Array.Empty<double>();

        var line = new double[Points];

        for (int point = 0; point < Points; point++)
        {
            if (read.Count == 1)
            {
                line[point] = read[0];
                continue;
            }

            double place = (double)point * (read.Count - 1) / (Points - 1);
            int below = Math.Min((int)Math.Floor(place), read.Count - 2);
            double past = place - below;

            line[point] = Level(read[below] + ((read[below + 1] - read[below]) * past));
        }

        return line;
    }

    /// <summary>One point of a line, or silence where the line has none or a bad one.</summary>
    private static double Point(IReadOnlyList<double>? line, int point) =>
        line is not null && point < line.Count && double.IsFinite(line[point]) ? Math.Clamp(line[point], -1, 1) : 0;

    /// <summary>A point held to the range and moved onto the nearest of the eight bit steps.</summary>
    private static double Level(double value) =>
        Math.Round(Math.Clamp(value, -1, 1) * Steps, MidpointRounding.AwayFromZero) / Steps;
}
