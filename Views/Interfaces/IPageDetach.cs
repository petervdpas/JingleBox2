namespace JingleBox2.Views.Interfaces;

/// <summary>
/// One page's ability to be taken out into a window of its own and put back.
/// </summary>
/// <remarks>
/// The page itself is moved rather than a second one built, which is the whole reason this is
/// worth having: two views of one page are two pictures that can disagree, and both do their own
/// work. A detached mixer that was a copy would poll its meters at the window's own rate for a
/// second set of levels, and could disagree with the first about which strip is picked.
///
/// A page has one home and one window, so this holds both and answers for the pair. What differs
/// between one page and the next is the title, what the page is bound to, and whether there is a
/// transport, and all three are handed in, so the second page that wants this needs nothing here.
///
/// **What it was bound to is asked for rather than read off the page.** A page usually sets its
/// own context with a binding written against whatever is above it, so reading the page's own
/// context after taking it out of its home gives nothing, and giving the window that nothing
/// opens a window with the furniture and none of the contents. Both of those were real: the first
/// took the tracks and the master out of the detached mixer, the second took everything.
/// </remarks>
public interface IPageDetach
{
    /// <summary>Whether the page is in a window of its own rather than at home.</summary>
    bool Detached { get; }

    /// <summary>
    /// Takes the page out into a window, or brings that window forward when it is already out.
    /// </summary>
    void Out();

    /// <summary>
    /// Closes the window, which is what puts the page back, and does nothing when it is home.
    /// </summary>
    /// <remarks>
    /// Closing rather than moving the page directly, so there is one way back and it is the one a
    /// window's own frame already offers.
    /// </remarks>
    void Back();
}
