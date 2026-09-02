
namespace JingleBox2.Rack.Controls.Records;

/// <summary>
/// One button of a pad grid, as the machine declared it.
/// </summary>
/// <remarks>
/// The name is what a preset writes its line against, and the note is what fires it in a
/// pattern. Neither is a setting: they are what the button is, and they change only when
/// somebody edits the machine.
/// </remarks>
/// <param name="Name">The text setting this pad's caption lives in.</param>
/// <param name="Note">The key it answers to, written the way a note is written.</param>
public sealed record PadCell(string Name, string Note);
