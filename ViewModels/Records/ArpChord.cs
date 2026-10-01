namespace JingleBox2.ViewModels.Records;

/// <summary>A chord an arpeggio can step through: the note, and two intervals above it.</summary>
/// <param name="Name">What the chord is called.</param>
/// <param name="Up">The first interval, in semitones.</param>
/// <param name="Up2">The second interval, in semitones.</param>
public sealed record ArpChord(string Name, int Up, int Up2);
