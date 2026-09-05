using System;
using System.Globalization;
using JingleBox2.Audio.Plugins.Bridge.Interfaces;

namespace JingleBox2.Audio.Plugins.Bridge;

/// <inheritdoc/>
/// <remarks>
/// One of these per plugin process, read and written only by whichever thread is rendering, which
/// is one at a time by <c>PluginProcess.Render</c>'s own gate.
/// </remarks>
/// <param name="name">The plugin this is about, so a line names it without being asked.</param>
public sealed class BridgeCost(string name) : IBridgeCost
{
    /// <summary>How long a stretch is before it says something.</summary>
    /// <remarks>The render cost's own, so the two lines land together and can be read as a pair.</remarks>
    private const long StretchMs = 5000;

    /// <summary>When the stretch began, on the clock the caller shares with everything else.</summary>
    private long _began = Environment.TickCount64;

    /// <summary>The shares added up, so a mean can be taken without keeping every crossing.</summary>
    private double _total;

    /// <summary>The milliseconds added up, for the half of the answer a share cannot give.</summary>
    private double _spent;

    /// <inheritdoc/>
    public double Worst { get; private set; }

    /// <inheritdoc/>
    public int Crossings { get; private set; }

    /// <inheritdoc/>
    public string? Crossed(int frames, double milliseconds, int rate)
    {
        if (frames <= 0 || rate <= 0 || milliseconds < 0 || double.IsNaN(milliseconds)) return null;

        double had = frames * 1000.0 / rate;

        Crossings++;
        _total += milliseconds / had;
        _spent += milliseconds;

        if (milliseconds / had > Worst) Worst = milliseconds / had;

        if (Environment.TickCount64 - _began < StretchMs) return null;

        string line = Said();

        Fresh();

        return line;
    }

    /// <summary>The stretch in one sentence, beside the mixing's own.</summary>
    private string Said() =>
        "bridge: " + name + " " + Crossings + " crossings, worst "
        + Percent(Worst) + " of the time they had, mean " + Percent(_total / Crossings)
        + ", " + (_spent / Crossings).ToString("0.000", CultureInfo.InvariantCulture) + " ms each";

    /// <summary>Starts the next stretch, keeping nothing from the last one.</summary>
    private void Fresh()
    {
        _began = Environment.TickCount64;
        _total = 0;
        _spent = 0;
        Worst = 0;
        Crossings = 0;
    }

    /// <summary>A share, as whole percent, which is what anybody compares.</summary>
    /// <param name="part">One being all of the time the audio had.</param>
    private static string Percent(double part) =>
        (part * 100).ToString("0", CultureInfo.InvariantCulture) + "%";
}
