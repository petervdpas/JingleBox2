namespace JingleBox2.ViewModels.Records;

/// <summary>One tick of a line as the command popup's ruler draws it.</summary>
/// <param name="Index">Which tick, counting from nought.</param>
/// <param name="On">Whether something happens on it: the note starting, stopping or sounding again.</param>
/// <param name="Text">What is written in it, such as an arpeggio's interval; empty for nothing.</param>
public sealed record TickMark(int Index, bool On, string Text);
