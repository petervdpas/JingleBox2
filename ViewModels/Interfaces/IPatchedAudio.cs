using JingleBox2.UI.Records;

namespace JingleBox2.ViewModels.Interfaces;

/// <summary>
/// Makes the busses agree with the routing as it is drawn.
/// </summary>
/// <remarks>
/// **There is one description of how audio moves through this application, and the patchbay is
/// that description.** The picture draws it, the flow rule reads it to decide which cable is
/// carrying, the levels are keyed on its points, and this executes it. They are not four things
/// kept in step; they are one thing read four ways, and the test of that is that none of them can
/// be changed without the others changing, because there is nothing to keep in step.
///
/// **It remembers nothing**, which is the whole of why it is safe. Where a source is now is
/// answered by asking the busses what they are holding, so this cannot drift from the machine the
/// way a private list of what it once did would. That list was the fault the first attempt had:
/// the picture said one thing, the stored settings said the same thing, and a third record inside
/// the engine said something else, and the one that decided what you heard was the third.
///
/// Called whenever the routing is read, which is often and costs a comparison per source where
/// there is nothing to do. Idempotent by construction: a source already where its cable says is
/// left alone.
/// </remarks>
public interface IPatchedAudio
{
    /// <summary>Puts each of our own sources on the bus its cable lands on.</summary>
    /// <remarks>
    /// A source whose stream is not running yet is left where it is rather than reported, since a
    /// song that has not started has nothing to move and is moved the moment it has: the routing
    /// is read again and again, so this is asked rather than answered once.
    /// </remarks>
    /// <param name="scene">The routing as it is drawn.</param>
    void Follow(PatchScene scene);
}
