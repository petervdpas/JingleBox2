using System.Collections.Generic;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tracker.Interfaces;

/// <summary>
/// What quantizing can snap to, worked out from how many lines there are to a beat.
/// </summary>
/// <remarks>
/// A number of lines means nothing on its own. Every 4 lines is a beat at four lines to the
/// beat and half a beat at eight, so a menu offering 2, 3, 4, 6, 8 and 16 lines was asking
/// somebody to do that arithmetic in their head before they could tell which entry was the one
/// they wanted, and half of those entries landed on nothing musical at any given setting.
///
/// So the choices are note values, which is how every sequencer ever built has asked this
/// question, and the lines follow from the setting. A value that does not come out whole is
/// not offered: at four lines to the beat that leaves 1/16 down to 1/1 and no triplets, and at
/// six it leaves the triplets and drops the 1/16, which is exactly what those two settings are
/// each good for.
/// </remarks>
public interface IQuantizeGrid
{
    /// <summary>The values worth offering, finest first.</summary>
    /// <remarks>
    /// Never empty. Where nothing divides evenly the beat itself is still offered, since the
    /// beat is always a whole number of lines.
    /// </remarks>
    /// <param name="linesPerBeat">How many lines the song puts in a beat.</param>
    IReadOnlyList<QuantizeChoice> Choices(int linesPerBeat);
}
