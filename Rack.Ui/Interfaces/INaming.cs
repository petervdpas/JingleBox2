namespace JingleBox2.Rack.Ui.Interfaces;

/// <summary>
/// How a value's name is written on a panel.
/// </summary>
/// <remarks>
/// A front panel is printed, not typed: it reads "Low pass" rather than "LowPass" or
/// "Low Pass". So only the first letter is left as it was written and the rest of the words are
/// lowered, which turns an enum's name into a phrase.
///
/// Acronyms are spelled out in a list rather than guessed at from the length or the vowels. The
/// set is small and is the one this application actually uses, and a rule clever enough to find
/// them would also find words that are not acronyms at all.
/// </remarks>
public interface INaming
{
    /// <summary>The name with its words spaced out, or an acronym left in capitals.</summary>
    /// <param name="value">Usually an enum member; anything that can say what it is called.</param>
    string Of(object? value);
}
