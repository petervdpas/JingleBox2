using System;

namespace JingleBox2.Tracker.Records;

/// <summary>
/// What a slot of the order says about repeating: how many times its pattern plays, and a stretch
/// of its lines gone round a number of times on each pass.
/// </summary>
/// <remarks>
/// The stretch is the old trackers' pattern loop, E60 and E6x, set on the slot rather than hidden
/// in a column: when the song reaches the last line of the stretch it goes back to the first,
/// until it has played the stretch <see cref="StretchTimes"/> times, and then carries on. The
/// whole pass then counts once towards <see cref="Times"/>.
///
/// Kept beside the order rather than in the pattern, since a pattern in the order twice can repeat
/// in one place and not in the other. Nonsense, a count under one or a stretch outside the pattern,
/// is read as playing once with no stretch; see <see cref="Held"/>.
/// </remarks>
/// <param name="Times">How many times the pattern plays before the song moves on.</param>
/// <param name="From">The first line of the stretch, or negative for none.</param>
/// <param name="To">The last line of the stretch, or negative for none.</param>
/// <param name="StretchTimes">How many times the stretch plays on each pass.</param>
public sealed record SlotRepeat(int Times = 1, int From = -1, int To = -1, int StretchTimes = 1)
{
    /// <summary>Playing once, with no stretch, which is what every slot does until told otherwise.</summary>
    public static readonly SlotRepeat None = new();

    /// <summary>Whether there is a stretch that goes round more than once.</summary>
    public bool HasStretch => From >= 0 && To >= From && StretchTimes > 1;

    /// <summary>Whether this says anything at all beyond playing once.</summary>
    public bool Repeats => Times > 1 || HasStretch;

    /// <summary>
    /// The same, held inside a pattern of so many lines: counts of at least one, a stretch drawn
    /// either way round put the right way, and a stretch that is not inside the pattern dropped.
    /// </summary>
    /// <param name="lines">How many lines the slot's pattern has.</param>
    public SlotRepeat Held(int lines)
    {
        int times = Math.Max(1, Times);
        int stretch = Math.Max(1, StretchTimes);

        int first = Math.Min(From, To);
        int last = Math.Max(From, To);

        bool inside = first >= 0 && last < lines;

        return inside
            ? new SlotRepeat(times, first, last, stretch)
            : new SlotRepeat(times, -1, -1, 1);
    }
}
