using System;
using JingleBox2.Rack.Controls.Interfaces;

namespace JingleBox2.Rack.Controls;

/// <inheritdoc/>
internal sealed class WaveformGrab : IWaveformGrab
{
    /// <inheritdoc/>
    /// <remarks>
    /// A tie within a half goes to the line asked about first, the start before the end, which is
    /// the order they sit in on a window that has been closed right down.
    /// </remarks>
    public int Grabbed(double x, double y, double height, double start, double end,
                       double loopStart, double loopEnd, bool loop, double reach)
    {
        if (!double.IsFinite(x) || !(reach > 0)) return -1;

        int window = Nearer(x, reach, 0, start, 1, end);

        if (!loop) return window;

        int looping = Nearer(x, reach, 2, loopStart, 3, loopEnd);

        bool low = double.IsFinite(y) && y > height / 2;

        return low
            ? (looping >= 0 ? looping : window)
            : (window >= 0 ? window : looping);
    }

    /// <summary>The nearer of two lines in reach of the press, or minus one for neither.</summary>
    /// <param name="x">Where the press landed.</param>
    /// <param name="reach">How close is close enough.</param>
    /// <param name="first">The first line's number.</param>
    /// <param name="firstAt">Where it is drawn.</param>
    /// <param name="second">The second line's number.</param>
    /// <param name="secondAt">Where it is drawn.</param>
    private static int Nearer(double x, double reach, int first, double firstAt, int second, double secondAt)
    {
        double toFirst = Math.Abs(firstAt - x);
        double toSecond = Math.Abs(secondAt - x);

        if (!(toFirst <= reach) && !(toSecond <= reach)) return -1;

        if (!(toSecond <= reach)) return first;
        if (!(toFirst <= reach)) return second;

        return toSecond < toFirst ? second : first;
    }
}
