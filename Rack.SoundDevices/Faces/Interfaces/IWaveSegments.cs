using System.Collections.Generic;

namespace JingleBox2.Rack.SoundDevices.Faces.Interfaces;

/// <summary>
/// A sound drawn as two waves, and the waves between them worked out.
/// </summary>
/// <remarks>
/// The Fairlight CMI kept a voice as thirty two waveforms of 128 points each and played them one
/// after another, so a sound could change as it went on. Nobody drew thirty two: you drew the
/// first and the last and asked the machine to merge the ones between, which it did point by
/// point in a straight line. That is all of what this is, and it is published because the panel
/// that draws the waves and the engine that plays them have to agree on it to the point: a
/// picture of the in-between waves worked out one way beside a sound worked out another is a
/// picture of something nobody hears.
///
/// A point is a number from -1 to 1. A line is written down as whole numbers from
/// -<c>Steps</c> to <c>Steps</c>, which is the eight bits the CMI stored and is a file somebody
/// can read, copy and edit by hand.
/// </remarks>
public interface IWaveSegments
{
    /// <summary>
    /// Fills that buffer with the wave that far from the beginning to the end.
    /// </summary>
    /// <remarks>
    /// Nought is the beginning and one the end; anything past either is that end, and something
    /// that is not a number is the beginning. A line shorter than the buffer is silent where it
    /// has no points, so a line damaged in a file costs the part that is missing and nothing more.
    /// </remarks>
    /// <param name="begin">The first wave.</param>
    /// <param name="end">The last wave.</param>
    /// <param name="along">How far from one to the other, nought to one.</param>
    /// <param name="into">Where the wave goes, one value per point.</param>
    void Between(IReadOnlyList<double> begin, IReadOnlyList<double> end, double along, double[] into);

    /// <summary>
    /// That position moved back to the wave it is on, for a sound that steps rather than glides.
    /// </summary>
    /// <param name="along">How far from the beginning to the end, nought to one.</param>
    /// <returns>The position of the wave at or before it, nought to one.</returns>
    double Stepped(double along);

    /// <summary>Which of the waves that position is on, counting from nought at the beginning.</summary>
    /// <param name="along">How far from the beginning to the end, nought to one.</param>
    int Wave(double along);

    /// <summary>
    /// A line as it is written down: whole numbers with a space between them.
    /// </summary>
    /// <remarks>
    /// Always the whole drawing, so a line with fewer points is written with silence after them.
    /// </remarks>
    /// <param name="points">The line, each point from -1 to 1.</param>
    string Spell(IReadOnlyList<double> points);

    /// <summary>
    /// A line read back off what was written down.
    /// </summary>
    /// <remarks>
    /// Always the whole drawing where anything could be read, stretched straight across it where
    /// the line has another number of points, so a line of two numbers is a ramp. A point past
    /// the range is held to it and one that is not a number is nought. Spaces and commas both
    /// separate, since both are what people type.
    ///
    /// **Nothing readable at all is an empty line**, and not a line of silence: whoever asked
    /// keeps the line it had, since a file that says nothing sensible has not said to draw a
    /// flat line.
    /// </remarks>
    /// <param name="said">The line as written down.</param>
    double[] Read(string said);
}
