namespace JingleBox2.Shortcuts.Records;

/// <summary>
/// One key the application answers and nobody may change: the keystroke and what it does.
/// </summary>
/// <param name="Keys">As a person writes it: "Space", "Ctrl+Shift+M".</param>
/// <param name="Does">What it does, in a sentence with no full stop, for a row in a list.</param>
public sealed record SystemKey(string Keys, string Does);
