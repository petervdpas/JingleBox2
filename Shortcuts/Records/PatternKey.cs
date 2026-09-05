using Avalonia.Input;
using JingleBox2.Shortcuts.Enums;

namespace JingleBox2.Shortcuts.Records;

/// <summary>
/// One key of the pattern's, and what it asks for.
/// </summary>
/// <remarks>
/// Several of these may name one action, which is how the octave is on both the numeric keypad
/// and a pair of brackets: the list says the keys and the words are said once per action.
/// </remarks>
/// <param name="Key">The key itself.</param>
/// <param name="Modifiers">What has to be held with it. None means it stands alone.</param>
/// <param name="Does">What it asks the pattern for.</param>
/// <param name="Said">The key in the words a page shows, such as <c>Ctrl+]</c>.</param>
public readonly record struct PatternKey(Key Key, KeyModifiers Modifiers, PatternAction Does, string Said);
