namespace JingleBox2.Audio.Plugins.Records;

/// <summary>
/// One note a plugin played of its own accord, on its way out of it.
/// </summary>
/// <remarks>
/// A drum machine running its own pattern, an arpeggiator, a plugin echoing what it was sent:
/// each of those hands the host notes at the end of a block, and this is one of them.
/// </remarks>
/// <param name="Frame">Which frame of the block it falls on, counted from the start of it.</param>
/// <param name="Channel">The MIDI channel, counted from one.</param>
/// <param name="Note">The key, as a MIDI note number.</param>
/// <param name="Velocity">How hard, nought to one. Nought on a note ending.</param>
/// <param name="On">Whether the note is starting or ending.</param>
public readonly record struct PlayedNote(int Frame, int Channel, int Note, float Velocity, bool On);
