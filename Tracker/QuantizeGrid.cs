using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using JingleBox2.Tracker.Interfaces;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Tracker;

/// <inheritdoc/>
public sealed class QuantizeGrid : IQuantizeGrid
{
    /// <summary>
    /// The note values worth offering, as the denominator under the one and whether it is a
    /// triplet.
    /// </summary>
    /// <remarks>
    /// The set every sequencer offers, which is why it is written down rather than worked out:
    /// a whole note down to a thirty-second, and the three triplets anybody uses. Order does
    /// not matter here, since the list comes back sorted by how many lines each works out at.
    /// </remarks>
    private static readonly (int Under, bool Triplet)[] Values =
    {
        (1, false), (2, false), (4, false), (8, false), (16, false), (32, false),
        (4, true), (8, true), (16, true)
    };

    /// <summary>How many beats there are in a whole note, which is what a note value is against.</summary>
    /// <remarks>
    /// A beat is a quarter note. Nothing here stores a time signature, and this is the
    /// assumption every sequencer makes when it prints 1/16 beside a number of lines.
    /// </remarks>
    private const int BeatsPerWhole = 4;

    /// <inheritdoc/>
    public IReadOnlyList<QuantizeChoice> Choices(int linesPerBeat)
    {
        int beat = Math.Max(1, linesPerBeat);

        var found = new List<QuantizeChoice>();
        var already = new HashSet<int>();

        foreach (var (under, triplet) in Values)
        {
            int over = beat * BeatsPerWhole * (triplet ? 2 : 1);
            int by = under * (triplet ? 3 : 1);

            if (over % by != 0) continue;

            int lines = over / by;
            if (lines < 1) continue;

            found.Add(new QuantizeChoice(Named(under, triplet, lines), lines));
        }

        var choices = found
            .OrderBy(c => c.Lines)
            .Where(c => already.Add(c.Lines))
            .ToList();

        if (choices.Count == 0) choices.Add(new QuantizeChoice(Named(BeatsPerWhole, false, beat), beat));

        return choices;
    }

    /// <summary>
    /// The value as everybody writes it, with what it works out at after it.
    /// </summary>
    /// <remarks>
    /// The lines are in the label rather than left to be guessed, because they are what the
    /// pattern will actually do and because two settings can put the same value on different
    /// numbers of lines.
    /// </remarks>
    private static string Named(int under, bool triplet, int lines)
    {
        string value = "1/" + under.ToString(CultureInfo.InvariantCulture) + (triplet ? "T" : "");

        return lines == 1
            ? value + "  (1 line)"
            : value + "  (" + lines.ToString(CultureInfo.InvariantCulture) + " lines)";
    }
}
