using System;
using System.Collections.Generic;

namespace JingleBox2.Audio.Records;

/// <summary>
/// What has to happen to the working copy to stand somewhere else in the history.
/// </summary>
/// <remarks>
/// Two answers rather than one, because there are two ways to get somewhere and the cheap one is
/// the common one. Going forward by a single step is that step done to the take as it already
/// is, which is what redo is; everything else is the take copied again and the steps up to that
/// point done in order, since no edit here has an inverse and none needs one.
///
/// **A rule and not a method on the editor**, which is the whole reason it exists: the version
/// of this that lived in the page worked out which step to do again before it had decided
/// whether it was going forward at all, so walking back to the top of the history read the step
/// before the first one. That is an index of minus one, on a list, from a button, and no test
/// anywhere could reach it because the arithmetic sat in a view model that needs a window, a
/// disc and a take to build.
/// </remarks>
/// <param name="Fresh">True where the take has to be copied again before any of it is done.</param>
/// <param name="Steps">What to do, in order, which is empty for a walk to the top.</param>
public sealed record TakeWalk(bool Fresh, IReadOnlyList<TakeStep> Steps)
{
    /// <summary>The walk to where you are already standing, which is no work at all.</summary>
    public static readonly TakeWalk Nowhere = new(false, Array.Empty<TakeStep>());

    /// <summary>True when there is nothing to do, so nothing has to be stopped or redrawn.</summary>
    public bool Idle => !Fresh && Steps.Count == 0;
}
