using System;
using Avalonia.Media;
using JingleBox2.Views.Interfaces;

namespace JingleBox2.Views;

/// <inheritdoc/>
/// <remarks>
/// Worked out in hue, saturation and value rather than in red, green and blue, because the
/// neighbours of a colour are a fact about the wheel and there is no way to say "the two either
/// side of this" in the other one: adding to the channels of a teal gives grey, white or a
/// different teal, never the green and the blue beside it.
///
/// A sine rather than a triangle, so the pad slows as it turns round and there is no moment
/// where it visibly changes direction. Both ends of the swing are the same distance out, so the
/// walk is symmetrical about the pad's own colour and the pad is itself at the start of every
/// cycle and at the middle of it.
/// </remarks>
public sealed class PulseColour : IPulseColour
{
    /// <summary>
    /// How far round the wheel the walk reaches, in degrees, either side.
    /// </summary>
    /// <remarks>
    /// Twenty two is about the width of one of the eight colours the palette offers, so a pad
    /// walks as far as its own neighbours on that palette and no further. Wider and a red pad
    /// passes through orange, which is another pad's colour and reads as the wrong pad lighting
    /// up; narrower and it is a shimmer nobody notices from a chair.
    /// </remarks>
    private const double Spread = 22;

    /// <summary>How much brighter and darker it gets along the way.</summary>
    /// <remarks>
    /// Small, and it is here for the grey pads: a pad with no colour of its own has no hue to
    /// walk, so without this the one thing that says it is playing would be the ring around it.
    /// On a coloured pad it reads as the colour turning towards the light rather than as a
    /// separate flash.
    /// </remarks>
    private const double Lift = 0.10;

    /// <inheritdoc/>
    public Color At(Color own, double phase)
    {
        var hsv = own.ToHsv();

        double swing = Math.Sin(phase * Math.PI * 2);

        double hue = (hsv.H + Spread * swing) % 360;

        if (hue < 0) hue += 360;

        double value = Math.Clamp(hsv.V + Lift * swing, 0, 1);

        return new HsvColor(hsv.A, hue, hsv.S, value).ToRgb();
    }
}
