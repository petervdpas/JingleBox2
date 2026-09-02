namespace JingleBox2.Machines.Ui.Enums;

/// <summary>
/// A mark drawn on a push button's cap instead of a word.
/// </summary>
/// <remarks>
/// Drawn rather than written, which is the whole reason this exists. The three bars every
/// program uses for a menu are a character, U+2630, and a character is at the mercy of whichever
/// font the machine running this happens to fall back to: it came out a third of the height of
/// the cap it was on and sitting left of the middle, because the fallback's advance is wider
/// than its ink. Drawn, it is the size the cap says and it is centred on it.
/// </remarks>
public enum CapMark
{
    /// <summary>Nothing, which is every button that says a word instead.</summary>
    None = 0,

    /// <summary>Three bars, which is what a menu button has looked like since menus existed.</summary>
    Bars = 1
}
