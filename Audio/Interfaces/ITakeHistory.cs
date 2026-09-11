using System.Collections.Generic;
using JingleBox2.Audio.Records;

namespace JingleBox2.Audio.Interfaces;

/// <summary>
/// What has been done to the take that is open, and where in it you are standing.
/// </summary>
/// <remarks>
/// A list and a place in it, which is what an undo really is: the steps do not come off the list
/// when they are undone, so the way forward is still there until something new is done. That is
/// the shape everything with a history palette has, and it is why the list is worth showing.
///
/// Nothing here touches a file or a sample. What it decides is which steps are supposed to have
/// happened, and something else does them: kept apart, the awkward half, which is a hand going
/// back three steps and then doing something new, can be put a question to without a take.
/// </remarks>
public interface ITakeHistory
{
    /// <summary>Every step, the ones ahead of where you are standing included.</summary>
    IReadOnlyList<TakeStep> Steps { get; }

    /// <summary>How many of them have been done, which is where you are standing.</summary>
    int Done { get; }

    /// <summary>The steps that have been done, in the order they were.</summary>
    IEnumerable<TakeStep> Applied { get; }

    /// <summary>True when there is something to go back past.</summary>
    bool CanUndo { get; }

    /// <summary>And true when there is something in front of you to do again.</summary>
    bool CanRedo { get; }

    /// <summary>
    /// Writes a step down as the newest thing done.
    /// </summary>
    /// <remarks>
    /// Anything that was in front of where you were standing is gone, which is what every undo
    /// everywhere does: once you go back and do something else, the way forward was a different
    /// take and there is nothing honest to do with it.
    /// </remarks>
    /// <param name="step">What was done.</param>
    void Add(TakeStep step);

    /// <summary>Stands one step further back, or stays where it is at the beginning.</summary>
    void Undo();

    /// <summary>And one step further forward.</summary>
    void Redo();

    /// <summary>
    /// Stands at a place in the list outright, which is what picking a line in it means.
    /// </summary>
    /// <param name="done">How many steps should have been done, held inside the list.</param>
    void GoTo(int done);

    /// <summary>
    /// What would have to happen to the working copy to stand at a place in the list.
    /// </summary>
    /// <remarks>
    /// Answered here rather than worked out by whoever does it, because it is a question about
    /// the list and about nothing else: which steps are between here and there, and whether
    /// there is a way that does not start from the take again.
    ///
    /// Nothing moves. The walk is what it would take, and where you are standing changes when
    /// somebody says <see cref="GoTo"/>, which is after the work rather than before it: a walk
    /// that failed part way must not leave the list claiming to be somewhere the take is not.
    /// </remarks>
    /// <param name="done">How many steps should have been done, held inside the list.</param>
    /// <returns>The walk, which is <c>Nowhere</c> where you are already standing there.</returns>
    TakeWalk Toward(int done);

    /// <summary>Forgets the lot, which is what saving and starting again both leave behind.</summary>
    void Clear();
}
