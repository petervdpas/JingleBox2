using System;
using System.Linq;
using JingleBox2.Rack.SoundDevices.Faces;
using Xunit;

namespace JingleBox2.Tests;

/// <summary>
/// The waves between two drawn ones, and how a drawn one is written down.
/// </summary>
/// <remarks>
/// The picture and the sound both go through this, so what is checked is the arithmetic and every
/// way a line written into a file can be wrong: too short, too long, not numbers, past the end of
/// the range, and nothing at all.
/// </remarks>
public class WaveSegmentsTests
{
    private readonly WaveSegments _segments = new();

    /// <summary>A line of that many points, each worked out from its place.</summary>
    private static double[] Line(Func<int, double> at) =>
        Enumerable.Range(0, WaveSegments.Points).Select(at).ToArray();

    /// <summary>The first wave is the beginning, the last is the end, and the middle is halfway between.</summary>
    [Fact]
    public void The_waves_between_run_straight_from_one_line_to_the_other()
    {
        var begin = Line(i => -1);
        var end = Line(i => 1);
        var into = new double[WaveSegments.Points];

        _segments.Between(begin, end, 0, into);
        Assert.All(into, one => Assert.Equal(-1, one));

        _segments.Between(begin, end, 1, into);
        Assert.All(into, one => Assert.Equal(1, one));

        _segments.Between(begin, end, 0.5, into);
        Assert.All(into, one => Assert.Equal(0, one, 12));
    }

    /// <summary>Where along the way is held to the two ends, and nonsense is the beginning.</summary>
    [Fact]
    public void Along_past_either_end_is_the_end()
    {
        var begin = Line(i => 0.25);
        var end = Line(i => -0.5);
        var into = new double[WaveSegments.Points];

        _segments.Between(begin, end, 7, into);
        Assert.Equal(-0.5, into[3]);

        _segments.Between(begin, end, -3, into);
        Assert.Equal(0.25, into[3]);

        _segments.Between(begin, end, double.NaN, into);
        Assert.Equal(0.25, into[3]);
    }

    /// <summary>A line shorter than the drawing is silence where it has no points.</summary>
    [Fact]
    public void A_short_line_is_silent_past_its_end()
    {
        var into = new double[WaveSegments.Points];

        _segments.Between(new[] { 1.0 }, Array.Empty<double>(), 0, into);

        Assert.Equal(1, into[0]);
        Assert.Equal(0, into[1]);
    }

    /// <summary>A stepped position lands on one of the thirty two waves and never between them.</summary>
    [Fact]
    public void A_step_is_one_of_the_waves()
    {
        Assert.Equal(0, _segments.Stepped(0));
        Assert.Equal(1, _segments.Stepped(1));
        Assert.Equal(1, _segments.Stepped(5));
        Assert.Equal(0, _segments.Stepped(double.NaN));
        Assert.Equal(15.0 / 31, _segments.Stepped(15.9 / 31), 12);
        Assert.Equal(15, _segments.Wave(15.9 / 31));
        Assert.Equal(WaveSegments.Count - 1, _segments.Wave(1));
    }

    /// <summary>A line written and read back is the same line, to the eighth bit.</summary>
    [Fact]
    public void A_line_written_down_reads_back()
    {
        var line = Line(i => Math.Sin(2 * Math.PI * i / WaveSegments.Points));

        string said = _segments.Spell(line);
        var back = _segments.Read(said);

        Assert.Equal(WaveSegments.Points, back.Length);
        Assert.DoesNotContain('/', said);

        for (int at = 0; at < line.Length; at++)
        {
            Assert.True(Math.Abs(back[at] - line[at]) <= 0.5 / WaveSegments.Steps + 1e-12);
            Assert.Equal(back[at], Math.Round(back[at] * WaveSegments.Steps) / WaveSegments.Steps, 12);
        }

        Assert.Equal(said, _segments.Spell(back));
    }

    /// <summary>A line of the wrong length is stretched across the drawing rather than refused.</summary>
    [Fact]
    public void A_line_of_another_length_is_stretched_to_fit()
    {
        var two = _segments.Read("-127 127");

        Assert.Equal(WaveSegments.Points, two.Length);
        Assert.Equal(-1, two[0]);
        Assert.Equal(1, two[^1]);
        Assert.True(two[WaveSegments.Points / 2] > -0.1 && two[WaveSegments.Points / 2] < 0.1);

        var one = _segments.Read("64");

        Assert.All(one, point => Assert.Equal(64.0 / WaveSegments.Steps, point, 12));

        var many = _segments.Read(string.Join(" ", Enumerable.Repeat("127", 500)));

        Assert.Equal(WaveSegments.Points, many.Length);
        Assert.All(many, point => Assert.Equal(1, point));
    }

    /// <summary>Past the range is held to it, and what is not a number is nought.</summary>
    [Fact]
    public void Nonsense_in_a_line_is_held_or_silenced()
    {
        var read = _segments.Read("900, -900, NaN, banana, 0");

        Assert.Equal(WaveSegments.Points, read.Length);
        Assert.Equal(1, read[0]);
        Assert.All(read, point => Assert.True(double.IsFinite(point) && point is >= -1 and <= 1));
    }

    /// <summary>Nothing readable at all is no line, so whoever asked keeps the one it had.</summary>
    [Fact]
    public void Nothing_readable_is_no_line()
    {
        Assert.Empty(_segments.Read(""));
        Assert.Empty(_segments.Read("   "));
        Assert.Empty(_segments.Read(null!));
        Assert.Empty(_segments.Read("banana"));
    }

    /// <summary>A line with nothing in it is spelled as silence the length of the drawing.</summary>
    [Fact]
    public void An_empty_line_is_spelled_as_silence()
    {
        var read = _segments.Read(_segments.Spell(Array.Empty<double>()));

        Assert.Equal(WaveSegments.Points, read.Length);
        Assert.All(read, point => Assert.Equal(0, point));
    }
}
