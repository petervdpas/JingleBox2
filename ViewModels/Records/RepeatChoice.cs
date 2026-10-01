namespace JingleBox2.ViewModels.Records;

/// <summary>One line of a menu that sets how many times something plays.</summary>
/// <param name="Times">How many times.</param>
/// <param name="Label">What the menu says.</param>
public sealed record RepeatChoice(int Times, string Label);
