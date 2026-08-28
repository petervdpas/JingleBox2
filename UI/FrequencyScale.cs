using System;

namespace JingleBox2.UI;

/// <summary>
/// Frequency as a knob position. Hearing works in octaves rather than in hertz: half the dial
/// between 20 Hz and 20 kHz is around 630 Hz, not 10 kHz, which is why a filter knob that
/// moves linearly does nothing at all until the last part of its travel.
/// </summary>
public static class FrequencyScale
{
    /// <summary>The bottom of the dial, which is about as low as hearing goes.</summary>
    public const double MinHz = 20;

    /// <summary>The top of it, which is about as high as hearing goes.</summary>
    public const double MaxHz = 20000;

    /// <summary>Where a frequency sits on the dial, 0 at the bottom and 1 at the top.</summary>
    public static double ToPosition(double hz)
    {
        if (double.IsNaN(hz)) return 1;

        double clamped = Math.Clamp(hz, MinHz, MaxHz);
        return Math.Log(clamped / MinHz) / Math.Log(MaxHz / MinHz);
    }

    /// <summary>The frequency a dial position means.</summary>
    public static double ToHz(double position)
    {
        if (double.IsNaN(position)) return MaxHz;

        double clamped = Math.Clamp(position, 0, 1);
        return MinHz * Math.Pow(MaxHz / MinHz, clamped);
    }

    /// <summary>How a frequency reads on a control: hertz up close, kilohertz further out.</summary>
    public static string Text(double hz)
    {
        if (double.IsNaN(hz)) return "-";
        if (hz >= MaxHz) return "off";

        return hz >= 1000
            ? (hz / 1000).ToString("0.0") + " kHz"
            : hz.ToString("0") + " Hz";
    }
}
