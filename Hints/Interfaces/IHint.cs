namespace JingleBox2.Hints.Interfaces;

/// <summary>
/// One piece of work that happens after whatever asks for it stops asking.
/// </summary>
/// <remarks>
/// **The shape every deferred write in this application had, written out five times.** Something
/// moves many times in a second and something expensive has to follow it once: a level dragged
/// across a strip is one thing a person did and a hundred messages, and the chain under it is a
/// round trip to every plugin on it. So the asking is cheap and says only that it moved, and the
/// answer happens once the asking stops.
///
/// It was a timer per place, stopped and started by hand, with a number apiece that nobody had
/// compared: 400, 600, 600, 600 and 1000 milliseconds, five clocks, and each one a chance to
/// forget the stop. What that costs is not the clocks, it is that a fault in one of them is a
/// fault in one of them: the same thing fixed five times, and the fifth found a year later.
///
/// **Saying it moved is cheap enough to say every time.** It is a field and a comparison, so
/// nothing anywhere has to decide whether this change is worth mentioning, which is the decision
/// that goes wrong.
/// </remarks>
public interface IHint
{
    /// <summary>Says the thing this is about has moved, and that an answer is owed.</summary>
    /// <remarks>
    /// Said as often as anybody likes. The answer follows once the saying stops for as long as
    /// this hint was made to wait, or sooner if it has been waiting as long as it is allowed to.
    /// </remarks>
    void Moved();

    /// <summary>Answers now if one is owed, and forgets that it was.</summary>
    /// <remarks>
    /// **For the way out**, which is the one moment that cannot be waited through: whatever moved
    /// in the last fraction of a second is otherwise the thing somebody comes back to find
    /// missing. Nothing happens where nothing is owed.
    /// </remarks>
    void Now();

    /// <summary>Drops what is owed without answering it.</summary>
    /// <remarks>
    /// **For work that has stopped being worth doing**, which is a different thing from work that
    /// is finished: a rescue copy is not written for a song whose window is closing, and a rack
    /// entry is not written back after it has been deleted. Said any other way it is a race, since
    /// the answer is owed on a clock that is not the one asking.
    /// </remarks>
    void Forget();

    /// <summary>Whether an answer is waiting to happen.</summary>
    bool Owed { get; }
}
