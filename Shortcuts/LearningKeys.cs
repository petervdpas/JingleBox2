namespace JingleBox2.Shortcuts;

/// <summary>
/// Whether somebody is in the middle of putting a shortcut on a key.
/// </summary>
/// <remarks>
/// A door, like the transport's keys and the pointing mode, and it holds one fact and decides
/// nothing. An application is learning a key or it is not, and handing that about would be
/// handing the same answer about under another name.
///
/// It has to exist because of the order keys arrive in. Every key this application answers is
/// heard on the way down, at the window, before whatever has the keyboard sees it, which is
/// right the rest of the time: it is what stops the last button pressed keeping the space bar.
/// While a row is listening it is exactly wrong, and worst for the keys somebody is most likely
/// to want: the space bar, Ctrl+S and Ctrl+H would each be taken by their own door and the row
/// waiting for a keystroke would never see one.
///
/// So the four doors ask this first and stand down. Four places asking one question rather than
/// one place answering for all of them, because they are four different keystrokes on four
/// different routes, and what they share is only the moment.
/// </remarks>
public static class LearningKeys
{
    /// <summary>
    /// True while a shortcut row is waiting for the keystroke that will become its shortcut.
    /// </summary>
    /// <remarks>
    /// Set by the page that is listening and cleared by it, including when that page loses the
    /// keyboard: a row left waiting would take whatever was pressed on the way back to it, and
    /// a flag left set would leave the application deaf to every key it has.
    /// </remarks>
    public static bool On { get; set; }
}
