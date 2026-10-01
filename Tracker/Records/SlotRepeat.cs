using System;
using System.Collections.Generic;
using System.Linq;

namespace JingleBox2.Tracker.Records;

/// <summary>
/// What a slot of the order says about repeating: how many times its pattern plays, and stretches
/// of its lines that each go round a number of times on each pass.
/// </summary>
/// <remarks>
/// A stretch is the old trackers' pattern loop, E60 and E6x, set on the slot rather than hidden in
/// a column: when the song reaches the last line of a stretch it goes back to the first, until it
/// has played the stretch its number of times, and then carries on. A slot can hold several, as
/// long as no two share a line, since the pattern plays a whole row at a time and a line can only
/// be in one stretch. The whole pass then counts once towards <see cref="Times"/>.
///
/// Kept beside the order rather than in the pattern, since a pattern in the order twice can repeat
/// in one place and not in the other. A stretch written the single way, in <see cref="From"/>,
/// <see cref="To"/> and <see cref="StretchTimes"/>, is read as the first of <see cref="Loops"/>.
/// Everything is held to sense by <see cref="Held"/>: counts of at least one, stretches put the
/// right way round, inside the pattern, sorted, and none sharing a line.
/// </remarks>
/// <param name="Times">How many times the pattern plays before the song moves on.</param>
/// <param name="From">The first line of a stretch written the single way, or negative for none.</param>
/// <param name="To">The last line of a stretch written the single way, or negative for none.</param>
/// <param name="StretchTimes">How many times a stretch written the single way plays.</param>
/// <param name="Loops">The stretches, or nothing for none.</param>
public sealed record SlotRepeat(int Times = 1, int From = -1, int To = -1, int StretchTimes = 1,
                                IReadOnlyList<LineLoop>? Loops = null)
{
    /// <summary>Playing once, with no stretch, which is what every slot does until told otherwise.</summary>
    public static readonly SlotRepeat None = new();

    /// <summary>The stretches as they stand, the single way included, before anything is held to sense.</summary>
    private IEnumerable<LineLoop> Written()
    {
        if (From >= 0 || To >= 0) yield return new LineLoop(From, To, StretchTimes);

        if (Loops == null) yield break;

        foreach (var loop in Loops) yield return loop;
    }

    /// <summary>The stretches, sorted by line; empty for none.</summary>
    /// <remarks>Read off a slot that has been held, which is what <c>Song.RepeatAt</c> hands out.</remarks>
    public IReadOnlyList<LineLoop> Lines() => Loops ?? (IReadOnlyList<LineLoop>)Array.Empty<LineLoop>();

    /// <summary>
    /// The same, held inside a pattern of so many lines: counts of at least one, each stretch put
    /// the right way round and kept only where it is inside the pattern and goes round more than
    /// once, sorted, and a stretch sharing a line with one before it in the list dropped.
    /// </summary>
    /// <param name="lines">How many lines the slot's pattern has.</param>
    public SlotRepeat Held(int lines)
    {
        var kept = new List<LineLoop>();

        foreach (var loop in Written())
        {
            int first = Math.Min(loop.From, loop.To);
            int last = Math.Max(loop.From, loop.To);

            if (first < 0 || last >= lines || loop.Times < 2) continue;

            var right = new LineLoop(first, last, loop.Times);

            if (kept.Any(other => other.Overlaps(right))) continue;

            kept.Add(right);
        }

        return new SlotRepeat(Math.Max(1, Times), Loops: kept.Count == 0 ? null : kept.OrderBy(loop => loop.From).ToArray());
    }

    /// <summary>
    /// The same with a stretch added: the same lines again change their count, and lines shared
    /// with a stretch already there are refused, leaving this as it was.
    /// </summary>
    /// <param name="loop">The stretch to add.</param>
    /// <param name="lines">How many lines the slot's pattern has.</param>
    /// <param name="blocking">The stretch in the way where it was refused, nothing otherwise.</param>
    public SlotRepeat WithLoop(LineLoop loop, int lines, out LineLoop? blocking)
    {
        var held = Held(lines);
        var adding = new SlotRepeat(1, loop.From, loop.To, loop.Times).Held(lines).Lines();

        blocking = null;

        if (adding.Count == 0) return held;

        var wanted = adding[0];
        var kept = held.Lines().Where(other => !other.SameLines(wanted)).ToList();

        blocking = kept.FirstOrDefault(other => other.Overlaps(wanted));

        if (blocking != null) return held;

        kept.Add(wanted);

        return new SlotRepeat(held.Times, Loops: kept.OrderBy(other => other.From).ToArray());
    }

    /// <summary>The same with every stretch that shares a line with the given lines taken off.</summary>
    /// <param name="first">The first of the lines.</param>
    /// <param name="last">The last of the lines.</param>
    /// <param name="lines">How many lines the slot's pattern has.</param>
    public SlotRepeat WithoutLoopsIn(int first, int last, int lines)
    {
        var held = Held(lines);
        var span = new LineLoop(Math.Min(first, last), Math.Max(first, last), 1);
        var kept = held.Lines().Where(loop => !loop.Overlaps(span)).ToArray();

        return new SlotRepeat(held.Times, Loops: kept.Length == 0 ? null : kept);
    }
}
