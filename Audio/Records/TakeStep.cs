using JingleBox2.Audio.Enums;

namespace JingleBox2.Audio.Records;

/// <summary>
/// One thing somebody did to a take, written down so it can be done again.
/// </summary>
/// <remarks>
/// **The edit rather than the audio**, which is the whole of how the history is affordable here.
/// A pattern's undo keeps whole copies of the pattern because a pattern is five kilobytes; a take
/// is up to a hundred megabytes, so twenty steps of copies is two gigabytes and a step is
/// instead a sentence a dozen bytes long. Going back is a fresh copy of the take with the steps
/// before that point done again, which needs no inverse for any of them: the trap this codebase
/// already named about describing edits rather than copying them is that every edit then needs an
/// undo of its own and one of them will be wrong, and replaying from the original has no such
/// half.
///
/// The region is in frames rather than in fractions of the take, and against the take **as it
/// was when the step was taken**, which a trim changes underneath. That is exactly right under
/// replay, since replay puts the take back into that state before this step is reached; it is
/// also why a step cannot be reordered or applied to some other take.
/// </remarks>
/// <param name="Kind">Which edit it was.</param>
/// <param name="From">The first frame it worked on.</param>
/// <param name="To">One past the last, so <c>To - From</c> is how many frames it covered.</param>
/// <param name="Peak">
/// Where a normalize put the loudest moment, in dBFS. Nothing to every other kind, and kept on
/// the step rather than read from the page when the step is replayed: a peak somebody has since
/// changed in the box would otherwise rewrite what an old step did.
/// </param>
public readonly record struct TakeStep(TakeEditKind Kind, long From, long To, double Peak);
