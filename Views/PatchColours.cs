using Avalonia.Media;
using JingleBox2.Views.Interfaces;

namespace JingleBox2.Views;

/// <inheritdoc/>
public sealed class PatchColours : IPatchColours
{
    /// <summary>Half a turn, in degrees.</summary>
    private const double Half = 180;

    /// <summary>Below this there is no hue worth turning.</summary>
    /// <remarks>
    /// A grey has a hue in the arithmetic and none to the eye, so turning it produces another
    /// grey and a wire that looks like a mistake rather than a distinction.
    /// </remarks>
    private const double Colourless = 0.05;

    /// <inheritdoc/>
    public Color Counter(Color colour)
    {
        var hsv = colour.ToHsv();

        if (hsv.S < Colourless) return colour;

        double hue = (hsv.H + Half) % 360;

        return new HsvColor(hsv.A, hue, hsv.S, hsv.V).ToRgb();
    }
}
