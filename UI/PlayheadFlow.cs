using System;
using System.Globalization;
using JingleBox2.UI.Interfaces;

namespace JingleBox2.UI;

/// <inheritdoc/>
public sealed class PlayheadFlow : IPlayheadFlow
{
    /// <summary>How long a window is, which is what the render cost already reports in.</summary>
    /// <remarks>
    /// The same five seconds, so a line here and a line there can be read against each other:
    /// blocks going over their budget and the picture limping in the same window is one story and
    /// either one alone is another.
    /// </remarks>
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromSeconds(5);

    /// <summary>How long this one gathers for before it says anything.</summary>
    private readonly TimeSpan _window;

    /// <summary>When the last step arrived, or nothing before a run has started.</summary>
    private TimeSpan? _last;

    /// <summary>When this window opened.</summary>
    private TimeSpan? _opened;

    /// <summary>How many gaps have been counted in it.</summary>
    private int _count;

    /// <summary>What they add up to, in milliseconds.</summary>
    private double _total;

    /// <summary>The shortest of them.</summary>
    private double _least;

    /// <summary>And the longest.</summary>
    private double _most;

    /// <summary>Builds one over a window of its own.</summary>
    /// <param name="window">How long to gather for, or nothing for <see cref="DefaultWindow"/>.</param>
    public PlayheadFlow(TimeSpan? window = null) => _window = window ?? DefaultWindow;

    /// <inheritdoc/>
    public string? Stepped(TimeSpan at)
    {
        if (_last is { } last) Count((at - last).TotalMilliseconds);

        _last = at;
        _opened ??= at;

        if (at - _opened.Value < _window) return null;

        _opened = at;

        return Said();
    }

    /// <inheritdoc/>
    public string? Stopped()
    {
        string? said = Said();

        _last = null;
        _opened = null;

        return said;
    }

    /// <summary>Takes one gap into the window.</summary>
    /// <remarks>
    /// A gap of less than nothing is a clock that went backwards, which is not a step that was
    /// early: it is dropped rather than reported, since one of those would drag the least and the
    /// spread with it for the whole window.
    /// </remarks>
    private void Count(double milliseconds)
    {
        if (milliseconds < 0) return;

        if (_count == 0)
        {
            _least = milliseconds;
            _most = milliseconds;
        }
        else
        {
            _least = Math.Min(_least, milliseconds);
            _most = Math.Max(_most, milliseconds);
        }

        _count++;
        _total += milliseconds;
    }

    /// <summary>
    /// What the window came to, and nothing where nothing was counted.
    /// </summary>
    /// <remarks>
    /// **The spread is the whole point of the line and the mean is only there to read it
    /// against.** A pattern at 120 to the minute and four lines to the beat steps every 125
    /// milliseconds, and a mean of 125 is what a transport that is working says whether the steps
    /// came evenly or alternated 109 and 141 for ever.
    ///
    /// In the invariant reading of a number, deliberately: a log is read by whoever is fixing
    /// something, often on another machine than it was written on, and a decimal comma in a file
    /// of measurements is one more thing to decode.
    /// </remarks>
    private string? Said()
    {
        if (_count == 0) return null;

        double mean = _total / _count;
        double spread = Math.Max(_most - mean, mean - _least);

        string line =
            "tracker: the picture stepped " + _count.ToString(CultureInfo.InvariantCulture) +
            " time(s), mean " + Ms(mean) +
            " ms, " + Ms(_least) + " to " + Ms(_most) +
            ", worst " + Ms(spread) + " ms out";

        _count = 0;
        _total = 0;
        _least = 0;
        _most = 0;

        return line;
    }

    /// <summary>A length of time as the line prints it.</summary>
    private static string Ms(double milliseconds) =>
        milliseconds.ToString("0.0", CultureInfo.InvariantCulture);
}
