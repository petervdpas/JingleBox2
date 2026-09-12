namespace JingleBox2.UI.Interfaces;

/// <summary>
/// Lengths and positions held to whole numbers of the screen's own pixels.
/// </summary>
/// <remarks>
/// **A picture that steps by a length that is not a whole number of device pixels limps**, and it
/// limps at exactly the scalings where that length is not one. A row of 18 is 18 pixels at 100%
/// and 27 at 150%, both whole, and 22.5 at 125%: the pattern then moves 22 pixels under the
/// playhead and then 23, for ever, and every piece of lettering on it is rasterised at a different
/// fraction of a pixel on each step. Nothing about that depends on how fast the steps arrive, so
/// it is there whether or not the clock is keeping up.
///
/// The answer is worked out at whatever scaling the screen is really at rather than being a number
/// that comes out whole on one of them, which is the whole reason it is a rule of its own: a row
/// height that happens to land on a pixel on the machine it was written on says nothing about
/// anybody else's.
///
/// **How long and where are two questions and not one**, which is why there are two members. A row
/// of no height is a pattern nobody can see, so a length is never nought; the top of a pattern is
/// an offset of nought and always was, so a position is left exactly where it is asked for when
/// that is where it is.
/// </remarks>
public interface IDevicePixels
{
    /// <summary>
    /// The nearest length that is a whole number of device pixels, and never nought.
    /// </summary>
    /// <param name="length">How long it wants to be, in device independent pixels.</param>
    /// <param name="scaling">What this screen multiplies those by, which is 1 at 100%.</param>
    /// <returns>The length to use, still in device independent pixels.</returns>
    double Whole(double length, double scaling);

    /// <summary>
    /// The nearest position that lands on a whole device pixel, nought and below included.
    /// </summary>
    /// <param name="position">Where it wants to be, in device independent pixels.</param>
    /// <param name="scaling">What this screen multiplies those by, which is 1 at 100%.</param>
    /// <returns>Where to put it, still in device independent pixels.</returns>
    double Lands(double position, double scaling);
}
