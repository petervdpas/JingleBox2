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
    /// <summary>Where in the order, as two digits.</summary>
    /// <remarks>
    /// Its own property rather than formatted in the view, so the two numbers on a row are
    /// written the same way in one place. They are two different things and are drawn as two
    /// cells: where you are in the song, and which pattern is there.
    /// </remarks>
    public string Place => Slot.ToString("00", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Both numbers, for anything that wants the row as one string.</summary>
    /// <remarks>What the picture in the hand shows while a slot is being dragged.</remarks>
    public string Text => Place + "   " + Pattern;

    /// <summary>The text, which is what a list with no template shows.</summary>
    public override string ToString() => Text;
}
