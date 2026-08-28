namespace JingleBox2.UI.Interfaces;

/// <summary>
/// Frequency as a knob position. Hearing works in octaves rather than in hertz: half the dial
/// between 20 Hz and 20 kHz is around 630 Hz, not 10 kHz, which is why a filter knob that
/// moves linearly does nothing at all until the last part of its travel.
/// </summary>
public interface IFrequencyScale
{
    /// <summary>The bottom of the dial, which is about as low as hearing goes.</summary>
    double MinHz { get; }

    /// <summary>The top of it, which is about as high as hearing goes.</summary>
    double MaxHz { get; }

    /// <summary>Where a frequency sits on the dial, 0 at the bottom and 1 at the top.</summary>
    /// <param name="hz">The frequency, clamped to the dial's ends.</param>
    /// <returns>The dial position, 0 to 1.</returns>
    double ToPosition(double hz);

    /// <summary>The frequency a dial position means.</summary>
    /// <param name="position">Where the dial is, 0 to 1.</param>
    /// <returns>The frequency in hertz.</returns>
    double ToHz(double position);

    /// <summary>How a frequency reads on a control: hertz up close, kilohertz further out.</summary>
    /// <param name="hz">The frequency to word.</param>
    /// <returns>The reading, "off" at the top of the dial and "-" for nothing.</returns>
    string Text(double hz);
}
