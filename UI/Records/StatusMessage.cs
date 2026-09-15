using System;
using JingleBox2.UI.Enums;

namespace JingleBox2.UI.Records;

/// <summary>One thing that happened, with who said it and when.</summary>
/// <param name="Text">What was said.</param>
/// <param name="Kind">How it wants to be read.</param>
/// <param name="From">Who said it, or empty where nobody signed it.</param>
/// <param name="At">When it was said, for working out whether it is still standing.</param>
/// <param name="Toast">Whether it is also shown as a toast, for what is worth looking up from the work for.</param>
/// <param name="Link">Where the toast takes you when it is clicked, or empty where it goes nowhere.</param>
public sealed record StatusMessage(string Text, StatusKind Kind, string From, DateTime At,
                                   bool Toast = false, string Link = "")
{
    /// <summary>The message as one line, timed and signed, for the list of what has been said.</summary>
    public override string ToString() =>
        At.ToString("HH:mm:ss") + "  " + (From.Length > 0 ? From + ": " : "") + Text;
}
