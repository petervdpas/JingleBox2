using System;
using JingleBox2.Music.Interfaces;
using JingleBox2.Tracker.Records;

namespace JingleBox2.Music;

/// <inheritdoc/>
public sealed class NoteFrequency : INoteFrequency
{
    /// <inheritdoc cref="INoteFrequency.A4Hz"/>
    public const double A4Hz = 440.0;

    /// <inheritdoc cref="INoteFrequency.A4Semitone"/>
    public const int A4Semitone = 57;

    /// <inheritdoc/>
    double INoteFrequency.A4Hz => A4Hz;

    /// <inheritdoc/>
    int INoteFrequency.A4Semitone => A4Semitone;

    /// <inheritdoc/>
    public double Hz(Note note) => Hz(note.Semitone);

    /// <inheritdoc/>
    public double Hz(int semitone) => A4Hz * Math.Pow(2.0, (semitone - A4Semitone) / 12.0);
}
