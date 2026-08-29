namespace JingleBox2.ViewModels.Records;

/// <summary>One row of the order list: where it is, what it plays, and whether it is looped.</summary>
/// <remarks>
/// A shape rather than the string the list used to hold, because a row is two things now: the
/// slot and the mark down its left saying it is inside the loop range. The whole list is built
/// again whenever any of it moves, so nothing here has to say when it changes.
/// </remarks>
/// <param name="Slot">Where in the order, which is what the left column shows.</param>
/// <param name="Pattern">What the slot plays, by the pattern's own name.</param>
/// <param name="Loops">Whether this slot is inside the loop range.</param>
public sealed record OrderSlot(int Slot, string Pattern, bool Loops)
{
    /// <summary>The slot and its pattern, as the list has always printed them.</summary>
    public string Text => $"{Slot:00}   {Pattern}";

    /// <summary>The text, which is what a list with no template shows.</summary>
    public override string ToString() => Text;
}
