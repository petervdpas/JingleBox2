using JingleBox2.Tracker;

namespace JingleBox2.ViewModels.Records;

/// <summary>A track a side chain can listen to, as the mixer's picker shows it.</summary>
/// <param name="Track">
/// Which track, or <see cref="TrackMix.NoKey"/> for the row that means no side chain.
/// </param>
/// <param name="Label">
/// What the row says: the same two-digit track name the pattern header uses, so the picker and
/// the pattern cannot come to call one track two things.
/// </param>
public sealed record DuckKey(int Track, string Label)
{
    /// <summary>No side chain at all, which is the row at the top of every picker.</summary>
    public static readonly DuckKey None = new(TrackMix.NoKey, "None");

    /// <summary>Its label, so a picker handed rows rather than text still reads.</summary>
    public override string ToString() => Label;
}
