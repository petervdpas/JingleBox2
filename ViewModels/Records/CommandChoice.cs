using JingleBox2.ViewModels.Enums;

namespace JingleBox2.ViewModels.Records;

/// <summary>One command the popup offers, as its button shows it.</summary>
/// <param name="Kind">Which command.</param>
/// <param name="Label">The button's word.</param>
/// <param name="Letter">The letter it is written with in the pattern.</param>
/// <param name="Summary">One line saying what it does.</param>
public sealed record CommandChoice(CommandKind Kind, string Label, char Letter, string Summary);
