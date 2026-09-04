namespace JingleBox2.Shortcuts.Interfaces;

/// <summary>
/// Which letter of a page's name its shortcut uses, so the tab strip can mark it.
/// </summary>
/// <remarks>
/// A page shortcut is Ctrl+Alt and a letter, and the tab along the top already says the word
/// that letter came out of. Underlining it is how every application that has ever had a menu bar
/// tells you the key without spending a line on it, and it is the only place this information
/// can be where somebody is actually looking when they want it.
///
/// The first occurrence, case blind, since the names on the strip are capitals and a key is a
/// letter. A letter that is not in the word at all is answered with nothing rather than with a
/// guess: <c>Ctrl+Alt+Q</c> on MIXER is a perfectly good shortcut and there is nothing in MIXER
/// to underline, so the tab is drawn plain and the page in SETTINGS is where it is read.
///
/// A rule of its own so what gets marked can be put a question to without a window, a keyboard
/// or a tab strip.
/// </remarks>
public interface IShortcutLetter
{
    /// <summary>
    /// Where in that word its shortcut's letter is, or minus one when it is not in it.
    /// </summary>
    /// <param name="word">The page's name as the tab strip spells it.</param>
    /// <param name="keys">The shortcut as a person writes it, or nothing when it is on no key.</param>
    int In(string? word, string? keys);
}
